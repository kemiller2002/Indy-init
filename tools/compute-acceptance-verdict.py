#!/usr/bin/env python3
"""Compute Causa acceptance from independently pinned inputs.

The ledger supplies claims. Acceptance additionally requires a pinned
requirements baseline and source checkout, an externally pinned approvals
file, a content-addressed evidence bundle, and an independently supplied source
SHA. All inputs are read-only.

Exit codes: 0 ACCEPTED; 3 consistently non-accepted; 1 contradictory claim;
2 unreadable arguments or files.
"""
import argparse
import hashlib
import importlib.util
import json
import re
import subprocess
import sys
from pathlib import Path, PurePosixPath

_spec = importlib.util.spec_from_file_location(
    "causa_ledger", Path(__file__).with_name("validate-acceptance-ledger.py"))
_ledger = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_ledger)

REQUIRED_GATES = tuple(f"G{i}" for i in range(10))
PRECEDENCE = ("FAIL", "BLOCKED", "NOT_RUN")
VERDICT_FOR = {"FAIL": "FAILED", "BLOCKED": "BLOCKED", "NOT_RUN": "INCOMPLETE"}
MARKER = re.compile(r"^<!-- acceptance-verdict: ([A-Z]+) -->$")
CLAIM = re.compile(r"^\W*(verdict|outcome)\W*:?(.*)$", re.IGNORECASE)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def as_list(value):
    return tuple(item for item in value if isinstance(item, dict)) if isinstance(value, list) else ()


def parse_json(raw, label, problems):
    if raw is None:
        problems.append(f"trusted {label} is required")
        return {}
    try:
        value = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, ValueError) as ex:
        problems.append(f"trusted {label} is invalid JSON: {ex}")
        return {}
    if not isinstance(value, dict):
        problems.append(f"trusted {label} must be an object")
        return {}
    return value


def normalized_text_digest(raw):
    try:
        text = raw.decode("utf-8").replace("\r\n", "\n").replace("\r", "\n")
    except UnicodeDecodeError:
        return None
    return digest(text.encode("utf-8"))


def safe_relative(path):
    if not isinstance(path, str) or not path:
        return False
    candidate = PurePosixPath(path)
    return not candidate.is_absolute() and ".." not in candidate.parts and "." not in candidate.parts


def build_trust(
    *,
    baseline_raw=None,
    expected_baseline_digest=None,
    baseline_read=lambda commit, path: None,
    approvals_raw=None,
    expected_approvals_digest=None,
    manifest_raw=None,
    expected_manifest_digest=None,
    evidence_read=lambda path: None,
    trusted_source_sha=None,
    source_exists=False,
    trusted_operator_ids=(),
):
    problems = []
    baseline = parse_json(baseline_raw, "requirements baseline", problems)
    approvals = parse_json(approvals_raw, "approvals file", problems)
    manifest = parse_json(manifest_raw, "evidence manifest", problems)

    actual_baseline_digest = digest(baseline_raw) if baseline_raw is not None else None
    actual_approvals_digest = digest(approvals_raw) if approvals_raw is not None else None
    actual_manifest_digest = digest(manifest_raw) if manifest_raw is not None else None
    for label, actual, expected in (
        ("requirements baseline", actual_baseline_digest, expected_baseline_digest),
        ("approvals file", actual_approvals_digest, expected_approvals_digest),
        ("evidence manifest", actual_manifest_digest, expected_manifest_digest),
    ):
        if not _ledger.is_digest(expected):
            problems.append(f"trusted {label} expected SHA-256 is required")
        elif actual != expected:
            problems.append(f"trusted {label} SHA-256 mismatch: expected {expected}, actual {actual}")

    baseline_source = baseline.get("source") if isinstance(baseline.get("source"), dict) else {}
    if baseline.get("schema") != "praxis.requirements-baseline/1":
        problems.append("trusted requirements baseline has unsupported schema")
    if not isinstance(baseline.get("id"), str) or not baseline.get("id"):
        problems.append("trusted requirements baseline missing id")
    baseline_commit = baseline_source.get("commit")
    if not _ledger.is_sha(baseline_commit):
        problems.append("trusted requirements baseline source commit is invalid")
    documents = baseline_source.get("documents")
    if not isinstance(documents, list) or not documents:
        problems.append("trusted requirements baseline has no source documents")
    else:
        for item in documents:
            if not isinstance(item, dict) or not safe_relative(item.get("path")) or not _ledger.is_digest(item.get("sha256")):
                problems.append("trusted requirements baseline has an invalid source document entry")
                continue
            raw = baseline_read(baseline_commit, item["path"])
            if raw is None:
                problems.append(f"requirements source artifact unavailable: {item['path']} at {baseline_commit}")
            elif normalized_text_digest(raw) != item["sha256"]:
                problems.append(f"requirements source digest mismatch: {item['path']} at {baseline_commit}")
    requirements = baseline.get("requirements")
    if not isinstance(requirements, list):
        problems.append("trusted requirements baseline requirements must be an array")

    if approvals.get("schema") != "causa.requirements-approvals/1":
        problems.append("trusted approvals file has unsupported schema")
    if approvals.get("baseline_sha256") != actual_baseline_digest:
        problems.append("trusted approvals file is bound to a different requirements baseline")
    approval_changes = {}
    trusted_operator_ids = frozenset(trusted_operator_ids)
    approval_items = approvals.get("approvals")
    if not isinstance(approval_items, list):
        problems.append("trusted approvals must be an array")
    else:
        for approval in approval_items:
            if not isinstance(approval, dict):
                problems.append("trusted approval entry must be an object")
                continue
            approval_id = approval.get("id")
            authority = approval.get("authority") if isinstance(approval.get("authority"), dict) else {}
            source = approval.get("source") if isinstance(approval.get("source"), dict) else {}
            if not isinstance(approval_id, str) or not approval_id:
                problems.append("trusted approval entry missing id")
                continue
            if (authority.get("kind") != "operator" or
                    authority.get("id") not in trusted_operator_ids):
                problems.append(
                    f"approval {approval_id} is not authenticated as a trusted operator decision")
            if not source.get("repository") or not _ledger.is_sha(source.get("commit")) or not safe_relative(source.get("path")):
                problems.append(f"approval {approval_id} lacks an immutable authority source")
            changes = approval.get("changes")
            if not isinstance(changes, list) or not changes:
                problems.append(f"approval {approval_id} has no changes")
                continue
            for change in changes:
                if not isinstance(change, dict):
                    problems.append(f"approval {approval_id} contains an invalid change")
                    continue
                key = (change.get("requirement_id"), change.get("action"))
                if not all(isinstance(value, str) and value for value in key):
                    problems.append(f"approval {approval_id} contains an invalid change identity")
                    continue
                approval_changes[(approval_id,) + key] = change

    if manifest.get("schema") != "causa.acceptance-evidence-manifest/1":
        problems.append("trusted evidence manifest has unsupported schema")
    entries = manifest.get("entries")
    entry_map = {}
    if not isinstance(entries, list):
        problems.append("trusted evidence manifest entries must be an array")
    else:
        for entry in entries:
            if not isinstance(entry, dict) or not safe_relative(entry.get("path")) or not _ledger.is_digest(entry.get("sha256")):
                problems.append("trusted evidence manifest contains an invalid entry")
                continue
            if entry["path"] in entry_map:
                problems.append(f"duplicate evidence manifest path: {entry['path']}")
            entry_map[entry["path"]] = entry

    if not _ledger.is_sha(trusted_source_sha):
        problems.append("independently trusted source SHA is required")
    elif not source_exists:
        problems.append(f"trusted source commit is unavailable in the candidate repository: {trusted_source_sha}")

    return {
        "baseline": baseline,
        "baseline_digest": actual_baseline_digest,
        "approvals_digest": actual_approvals_digest,
        "manifest": manifest,
        "manifest_digest": actual_manifest_digest,
        "approval_changes": approval_changes,
        "entries": entry_map,
        "evidence_read": evidence_read,
        "trusted_source_sha": trusted_source_sha,
        "problems": tuple(problems),
    }


def mandatory_items(data):
    source = data.get("source_commit_sha")
    gates = {gate.get("id"): gate for gate in as_list(data.get("gates"))}
    gate_items = tuple(
        (f"gate {gid}", gates.get(gid, {}).get("status", "NOT_RUN"),
         tuple(gates.get(gid, {}).get("evidence_paths") or ()), source, "gate", gid)
        for gid in REQUIRED_GATES
    )
    requirement_items = tuple(
        (f"P0 requirement {item.get('id')}", item.get("status"),
         tuple(item.get("evidence_paths") or ()), source, "requirement", item.get("id"))
        for item in as_list(data.get("requirements")) if item.get("priority") == "P0"
    )
    defect_items = tuple(
        (f"defect class {item.get('id')}", item.get("status"),
         tuple(item.get("evidence_paths") or ()), source, "defect", item.get("id"))
        for item in as_list(data.get("defect_classes"))
        if str(item.get("severity", "")).upper() in ("CRITICAL", "HIGH")
    )
    return gate_items + requirement_items + defect_items


def identity_problems(data, trust):
    source, deployed = data.get("source_commit_sha"), data.get("deployed_commit_sha")
    problems = []
    if source != trust.get("trusted_source_sha"):
        problems.append("ledger source_commit_sha differs from the independently trusted source SHA")
    if deployed is None or deployed != source:
        problems.append("deployed_commit_sha is missing or differs from source_commit_sha")
    return tuple(problems)


def coverage_problems(data, trust):
    problems = []
    baseline = trust.get("baseline", {})
    ledger_baseline = data.get("requirements_baseline") if isinstance(data.get("requirements_baseline"), dict) else {}
    baseline_source = baseline.get("source") if isinstance(baseline.get("source"), dict) else {}
    expected_identity = {
        "id": baseline.get("id"),
        "repository": baseline_source.get("repository"),
        "commit": baseline_source.get("commit"),
        "sha256": trust.get("baseline_digest"),
    }
    for key, expected in expected_identity.items():
        if ledger_baseline.get(key) != expected:
            problems.append(f"ledger requirements baseline {key} differs from the independently pinned baseline")
    if data.get("approvals_sha256") != trust.get("approvals_digest"):
        problems.append("ledger approvals_sha256 differs from the independently pinned approvals file")
    if data.get("evidence_manifest_sha256") != trust.get("manifest_digest"):
        problems.append("ledger evidence_manifest_sha256 differs from the independently pinned evidence manifest")

    requirements = {item.get("id"): item for item in as_list(data.get("requirements"))}
    baseline_p0 = {
        item.get("id"): item
        for item in as_list(baseline.get("requirements"))
        if item.get("priority") == "P0" and isinstance(item.get("id"), str)
    }
    scope_changes = as_list(data.get("scope_changes"))
    declared = {}
    for change in scope_changes:
        key = (change.get("requirement_id"), change.get("action"))
        declared[key] = change
        trusted = trust.get("approval_changes", {}).get((change.get("approval_id"),) + key)
        if trusted is None:
            problems.append(
                f"scope change {key[0]} {key[1]} lacks a matching externally pinned operator approval")
        elif change.get("action") == "priority-change" and trusted.get("to") != change.get("to"):
            problems.append(f"scope change {key[0]} priority target differs from its operator approval")
    for requirement_id in sorted(baseline_p0):
        item = requirements.get(requirement_id)
        exclusion = declared.get((requirement_id, "exclude"))
        priority_change = declared.get((requirement_id, "priority-change"))
        if item is None:
            if exclusion is None:
                problems.append(f"missing mandatory baseline P0 requirement: {requirement_id}")
            continue
        if item.get("priority") != "P0":
            if priority_change is None or priority_change.get("to") != item.get("priority"):
                problems.append(
                    f"mandatory baseline P0 requirement {requirement_id} was downgraded without operator approval")
        elif exclusion is not None:
            problems.append(f"excluded baseline requirement {requirement_id} is still represented as P0")
    for change in scope_changes:
        if change.get("requirement_id") not in baseline_p0:
            problems.append(f"scope change targets unknown or non-P0 baseline requirement: {change.get('requirement_id')}")
    return tuple(problems)


def evidence_problems(data, items, trust):
    problems = []
    manifest = trust.get("manifest", {})
    if manifest.get("run_id") != data.get("run_id"):
        problems.append("evidence manifest belongs to a different run")
    if manifest.get("source_sha") != data.get("source_commit_sha"):
        problems.append("evidence manifest is stale or bound to a different source revision")
    for label, status, paths, source, subject_type, subject_id in items:
        if status != "PASS":
            continue
        observed_types = set()
        for path in paths:
            entry = trust.get("entries", {}).get(path)
            if entry is None:
                problems.append(f"{label}: evidence {path} is absent from the trusted manifest")
                continue
            raw = trust["evidence_read"](path)
            if raw is None:
                problems.append(f"{label}: evidence artifact cannot be retrieved: {path}")
                continue
            if digest(raw) != entry.get("sha256"):
                problems.append(f"{label}: evidence artifact digest mismatch: {path}")
                continue
            try:
                evidence = json.loads(raw.decode("utf-8"))
            except (UnicodeDecodeError, ValueError) as ex:
                problems.append(f"{label}: evidence artifact is not valid JSON: {path}: {ex}")
                continue
            if not isinstance(evidence, dict) or evidence.get("schema") != "causa.acceptance-evidence/1":
                problems.append(f"{label}: evidence artifact has unsupported schema: {path}")
                continue
            expected = {
                "run_id": data.get("run_id"),
                "source_sha": source,
                "subject_type": subject_type,
                "subject_id": subject_id,
                "status": "PASS",
                "exit_code": 0,
            }
            for key, value in expected.items():
                if evidence.get(key) != value:
                    problems.append(f"{label}: evidence {path} {key} differs from the verified subject")
            if not isinstance(evidence.get("command"), str) or not evidence.get("command"):
                problems.append(f"{label}: evidence {path} lacks the executed command")
            if isinstance(evidence.get("evidence_type"), str):
                observed_types.add(evidence["evidence_type"])
        if subject_type == "gate" and subject_id == "G8" and "ci" not in observed_types:
            problems.append("gate G8: required CI evidence is missing")
        if subject_type == "gate" and subject_id == "G9" and "deployment" not in observed_types:
            problems.append("gate G9: required deployment evidence is missing")
        if subject_type == "defect" and "review" not in observed_types:
            problems.append(f"{label}: independent review evidence is missing")
    return tuple(problems)


def verdict_of(structural, trust, coverage, evidence, items, identity):
    if structural or trust or coverage or evidence:
        return "INVALID"
    statuses = {status for _, status, _, _, _, _ in items}
    first = next((state for state in PRECEDENCE if state in statuses), None)
    if first is not None:
        return VERDICT_FOR[first]
    return "INCOMPLETE" if identity else "ACCEPTED"


def reasons_of(items, identity):
    return tuple(f"{label}: {status}" for label, status, _, _, _, _ in items if status != "PASS") + identity


def report_problems(text, verdict):
    lines = text.splitlines()
    markers = tuple(match.group(1) for match in (MARKER.match(line.strip()) for line in lines) if match)
    marker = (() if markers == (verdict,) else
              (f"report must contain exactly one '<!-- acceptance-verdict: {verdict} -->' line; found {list(markers)}",))
    claims = tuple(
        line.strip() for line in lines
        if (match := CLAIM.match(line)) and re.search(r"(?<!NOT )ACCEPTED", match.group(2).upper()))
    overclaim = () if verdict == "ACCEPTED" else tuple(
        f"report claims acceptance while the computed verdict is {verdict}: {claim}" for claim in claims)
    return marker + overclaim


def compute(data, trust=None, report=None):
    unclaimed = {**data, "accepted": False} if isinstance(data, dict) else data
    structural = tuple(_ledger.check(unclaimed))
    trust = trust or {"problems": ("trusted verification inputs are required",)}
    trust_problems = tuple(trust.get("problems", ()))
    items = mandatory_items(data) if isinstance(data, dict) else ()
    identity = identity_problems(data, trust) if isinstance(data, dict) else ()
    coverage = coverage_problems(data, trust) if isinstance(data, dict) and not trust_problems else ()
    evidence = evidence_problems(data, items, trust) if isinstance(data, dict) and not trust_problems else ()
    verdict = verdict_of(structural, trust_problems, coverage, evidence, items, identity)
    flag = data.get("accepted") if isinstance(data, dict) else None
    flag_problem = () if flag is (verdict == "ACCEPTED") else (
        f"ledger 'accepted' is {json.dumps(flag)} but the computed verdict is {verdict}",)
    claims = flag_problem + (report_problems(report, verdict) if report is not None else ())
    return {
        "verdict": verdict,
        "reasons": list(structural + trust_problems + coverage + evidence + reasons_of(items, identity)),
        "claim_problems": list(claims),
    }


def git_blob_reader(repo):
    def read(commit, path):
        if not _ledger.is_sha(commit) or not safe_relative(path):
            return None
        result = subprocess.run(
            ["git", "-C", str(repo), "show", f"{commit}:{path}"], capture_output=True)
        return result.stdout if result.returncode == 0 else None
    return read


def git_commit_exists(repo, commit):
    if not repo or not _ledger.is_sha(commit):
        return False
    return subprocess.run(
        ["git", "-C", str(repo), "cat-file", "-e", f"{commit}^{{commit}}"],
        capture_output=True).returncode == 0


def directory_reader(root):
    root = Path(root).resolve() if root else None

    def read(path):
        if root is None or not safe_relative(path):
            return None
        candidate = (root / path).resolve()
        if root != candidate and root not in candidate.parents:
            return None
        try:
            return candidate.read_bytes()
        except OSError:
            return None
    return read


def read_optional(path):
    return Path(path).read_bytes() if path else None


def parse_args(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("ledger")
    parser.add_argument("--repo")
    parser.add_argument("--baseline")
    parser.add_argument("--baseline-sha256")
    parser.add_argument("--baseline-source")
    parser.add_argument("--approvals")
    parser.add_argument("--approvals-sha256")
    parser.add_argument("--evidence-manifest")
    parser.add_argument("--evidence-manifest-sha256")
    parser.add_argument("--evidence-root")
    parser.add_argument("--trusted-source-sha")
    parser.add_argument("--trusted-operator-id", action="append", default=[])
    parser.add_argument("--report")
    return parser.parse_args(argv)


def main(argv):
    args = parse_args(argv)
    try:
        data = json.loads(Path(args.ledger).read_text(encoding="utf-8"))
        report = Path(args.report).read_text(encoding="utf-8") if args.report else None
        baseline_raw = read_optional(args.baseline)
        approvals_raw = read_optional(args.approvals)
        manifest_raw = read_optional(args.evidence_manifest)
    except (OSError, ValueError) as ex:
        print(f"Unreadable input: {ex}", file=sys.stderr)
        return 2
    trust = build_trust(
        baseline_raw=baseline_raw,
        expected_baseline_digest=args.baseline_sha256,
        baseline_read=git_blob_reader(args.baseline_source) if args.baseline_source else (lambda commit, path: None),
        approvals_raw=approvals_raw,
        expected_approvals_digest=args.approvals_sha256,
        manifest_raw=manifest_raw,
        expected_manifest_digest=args.evidence_manifest_sha256,
        evidence_read=directory_reader(args.evidence_root),
        trusted_source_sha=args.trusted_source_sha,
        source_exists=git_commit_exists(args.repo, args.trusted_source_sha),
        trusted_operator_ids=args.trusted_operator_id,
    )
    result = compute(data, trust, report)
    print(json.dumps(result, indent=2))
    if result["claim_problems"]:
        return 1
    return 0 if result["verdict"] == "ACCEPTED" else 3


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
