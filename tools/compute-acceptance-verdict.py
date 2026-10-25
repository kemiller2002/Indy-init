#!/usr/bin/env python3
"""Compute a rehearsal's acceptance verdict from its ledger. stdlib only; fail closed.

The verdict is derived, never written by hand (owner decision OD-INDY-7,
docs/OWNER-DECISIONS-2026-10-24.md). An agent may record gate and requirement
results in the ledger; it may not decide what they add up to.

    python3 -I tools/compute-acceptance-verdict.py LEDGER.json [--repo DIR] [--report REPORT.md]

Verdicts, in precedence order:
    INVALID     the ledger fails the structural contract (validate-acceptance-ledger.py),
                or a cited evidence path does not exist at the cited commit
    FAILED      any mandatory item is FAIL
    BLOCKED     no FAIL, but a mandatory item is BLOCKED
    INCOMPLETE  no FAIL or BLOCKED, but a mandatory item is NOT_RUN
    ACCEPTED    G0-G9 PASS on the source SHA, deployed SHA equals source SHA,
                every P0 requirement PASS, every critical/high defect class PASS

Mandatory items: gates G0-G9, P0 requirements, critical/high defect classes.
There is no exclusion mechanism here: a P0 requirement can leave the verdict
only by leaving the owner-approved requirements baseline.

Checks of claims (exit 1 when either disagrees with the computed verdict):
    the ledger's own "accepted" flag;
    --report: the report must contain exactly one line
        <!-- acceptance-verdict: VERDICT -->
    and no "Verdict:"/"Outcome:" line may claim ACCEPTED unless the computed
    verdict is ACCEPTED.

--repo: each evidence path of a PASS item must exist in that git repository at
the item's verified SHA (gates) or the ledger's source SHA (other items).

Exit codes: 0 ACCEPTED and consistent; 3 not accepted and consistent;
1 a claim disagrees with the computed verdict; 2 unreadable input.
The JSON result is written to stdout.
"""
import importlib.util
import json
import re
import subprocess
import sys
from pathlib import Path

_spec = importlib.util.spec_from_file_location(
    "causa_ledger", Path(__file__).with_name("validate-acceptance-ledger.py"))
_ledger = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_ledger)

REQUIRED_GATES = tuple(f"G{i}" for i in range(10))
PRECEDENCE = ("FAIL", "BLOCKED", "NOT_RUN")
VERDICT_FOR = {"FAIL": "FAILED", "BLOCKED": "BLOCKED", "NOT_RUN": "INCOMPLETE"}
MARKER = re.compile(r"^<!-- acceptance-verdict: ([A-Z]+) -->$")
CLAIM = re.compile(r"^\W*(verdict|outcome)\W*:?(.*)$", re.IGNORECASE)


def as_list(value):
    return tuple(x for x in value if isinstance(x, dict)) if isinstance(value, list) else ()


def mandatory_items(data):
    """(label, status, evidence_paths, sha) for every item the verdict depends on."""
    source = data.get("source_commit_sha")
    gates = {g.get("id"): g for g in as_list(data.get("gates"))}
    gate_items = tuple(
        (f"gate {gid}", gates.get(gid, {}).get("status", "NOT_RUN"),
         tuple(gates.get(gid, {}).get("evidence_paths") or ()), gates.get(gid, {}).get("verified_sha"))
        for gid in REQUIRED_GATES)
    requirement_items = tuple(
        (f"P0 requirement {r.get('id')}", r.get("status"), tuple(r.get("evidence_paths") or ()), source)
        for r in as_list(data.get("requirements")) if r.get("priority") == "P0")
    defect_items = tuple(
        # reviewer_evidence is free text in the ledger contract, so it carries no path to check.
        (f"defect class {c.get('id')}", c.get("status"), (), source)
        for c in as_list(data.get("defect_classes"))
        if str(c.get("severity", "")).upper() in ("CRITICAL", "HIGH"))
    return gate_items + requirement_items + defect_items


def identity_problems(data):
    source, deployed = data.get("source_commit_sha"), data.get("deployed_commit_sha")
    return () if deployed is not None and deployed == source else (
        "deployed_commit_sha is missing or differs from source_commit_sha",)


def missing_evidence(items, exists):
    return tuple(
        f"{label}: evidence {path} not found at {sha}"
        for label, status, paths, sha in items if status == "PASS"
        for path in paths if not exists(sha, path))


def verdict_of(structural, evidence, items, identity):
    if structural or evidence:
        return "INVALID"
    statuses = {status for _, status, _, _ in items}
    first = next((s for s in PRECEDENCE if s in statuses), None)
    if first is not None:
        return VERDICT_FOR[first]
    return "INCOMPLETE" if identity else "ACCEPTED"


def reasons_of(items, identity):
    return tuple(f"{label}: {status}" for label, status, _, _ in items if status != "PASS") + identity


def report_problems(text, verdict):
    lines = text.splitlines()
    markers = tuple(m.group(1) for m in (MARKER.match(l.strip()) for l in lines) if m)
    marker = (() if markers == (verdict,) else
              (f"report must contain exactly one '<!-- acceptance-verdict: {verdict} -->' line; found {list(markers)}",))
    claims = tuple(
        l.strip() for l in lines
        if (m := CLAIM.match(l)) and re.search(r"(?<!NOT )ACCEPTED", m.group(2).upper()))
    overclaim = () if verdict == "ACCEPTED" else tuple(
        f"report claims acceptance while the computed verdict is {verdict}: {c}" for c in claims)
    return marker + overclaim


def compute(data, exists=lambda sha, path: True, report=None):
    # The "accepted" flag is a claim, judged below against the computed verdict; the
    # structure is checked as if nothing had been claimed.
    unclaimed = {**data, "accepted": False} if isinstance(data, dict) else data
    structural = tuple(_ledger.check(unclaimed))
    items = mandatory_items(data) if isinstance(data, dict) else ()
    identity = identity_problems(data) if isinstance(data, dict) else ()
    evidence = missing_evidence(items, exists)
    verdict = verdict_of(structural, evidence, items, identity)
    flag = data.get("accepted") if isinstance(data, dict) else None
    flag_problem = () if flag is (verdict == "ACCEPTED") else (
        f"ledger 'accepted' is {json.dumps(flag)} but the computed verdict is {verdict}",)
    claims = flag_problem + (report_problems(report, verdict) if report is not None else ())
    return {
        "verdict": verdict,
        "reasons": list(structural + evidence + reasons_of(items, identity)),
        "claim_problems": list(claims),
    }


def git_exists(repo):
    def exists(sha, path):
        if not isinstance(sha, str):
            return False
        result = subprocess.run(["git", "-C", str(repo), "cat-file", "-e", f"{sha}:{path.rstrip('/')}"],
                                capture_output=True)
        return result.returncode == 0
    return exists


def parse_args(argv):
    def value(flag):
        return argv[argv.index(flag) + 1] if flag in argv and argv.index(flag) + 1 < len(argv) else None
    positional = tuple(a for i, a in enumerate(argv) if not a.startswith("--") and (i == 0 or not argv[i - 1].startswith("--")))
    return (positional[0] if len(positional) == 1 else None), value("--repo"), value("--report")


def main(argv):
    ledger_path, repo, report_path = parse_args(argv)
    if ledger_path is None:
        print(__doc__.split("\n\n")[2], file=sys.stderr)
        return 2
    try:
        data = json.loads(Path(ledger_path).read_text(encoding="utf-8"))
        report = Path(report_path).read_text(encoding="utf-8") if report_path else None
    except (OSError, ValueError) as ex:
        print(f"Unreadable input: {ex}", file=sys.stderr)
        return 2
    result = compute(data, git_exists(repo) if repo else (lambda sha, path: True), report)
    print(json.dumps(result, indent=2))
    if result["claim_problems"]:
        return 1
    return 0 if result["verdict"] == "ACCEPTED" else 3


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
