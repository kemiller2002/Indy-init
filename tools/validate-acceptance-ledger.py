#!/usr/bin/env python3
"""Validate the structural Causa acceptance-ledger contract.

This module deliberately does not establish external trust. The authoritative
verdict is produced by compute-acceptance-verdict.py, which also verifies the
pinned requirements baseline, approvals, source identity, and evidence bytes.
"""
import json
import re
import sys
from pathlib import Path

SCHEMA = "causa.acceptance-ledger/2"
STATES = {"NOT_RUN", "PASS", "FAIL", "BLOCKED"}
REQUIRED_GATES = {f"G{i}" for i in range(10)}
SHA_RE = re.compile(r"[0-9a-f]{40}")
DIGEST_RE = re.compile(r"[0-9a-f]{64}")


def is_sha(value):
    return isinstance(value, str) and SHA_RE.fullmatch(value) is not None


def is_digest(value):
    return isinstance(value, str) and DIGEST_RE.fullmatch(value) is not None


def check(data):
    errors = []

    def require(condition, message):
        if not condition:
            errors.append(message)

    require(isinstance(data, dict), "root must be an object")
    if not isinstance(data, dict):
        return errors

    require(data.get("schema") == SCHEMA, f"schema must be {SCHEMA}")
    for key in (
        "run_id",
        "source_commit_sha",
        "contract_version",
        "frozen_fixture_digest",
        "dependency_lock_sha",
        "interface_manifest_sha",
    ):
        require(isinstance(data.get(key), str) and bool(data.get(key)), f"missing {key}")

    source = data.get("source_commit_sha")
    deployed = data.get("deployed_commit_sha")
    require(is_sha(source), "source_commit_sha must be 40 lowercase hex")
    if deployed is not None:
        require(is_sha(deployed), "deployed_commit_sha must be 40 lowercase hex or null")

    baseline = data.get("requirements_baseline")
    require(isinstance(baseline, dict), "requirements_baseline must be an object")
    if isinstance(baseline, dict):
        require(isinstance(baseline.get("id"), str) and bool(baseline.get("id")),
                "requirements_baseline missing id")
        require(isinstance(baseline.get("repository"), str) and bool(baseline.get("repository")),
                "requirements_baseline missing repository")
        require(is_sha(baseline.get("commit")), "requirements_baseline commit must be 40 lowercase hex")
        require(is_digest(baseline.get("sha256")), "requirements_baseline sha256 must be 64 lowercase hex")
    require(is_digest(data.get("approvals_sha256")), "approvals_sha256 must be 64 lowercase hex")
    require(is_digest(data.get("evidence_manifest_sha256")),
            "evidence_manifest_sha256 must be 64 lowercase hex")
    require("approvals" not in data, "inline approvals are not trusted; use the externally pinned approvals file")

    scope_changes = data.get("scope_changes", [])
    require(isinstance(scope_changes, list), "scope_changes must be an array")
    if isinstance(scope_changes, list):
        seen_changes = set()
        for index, change in enumerate(scope_changes):
            prefix = f"scope_changes[{index}]"
            if not isinstance(change, dict):
                errors.append(f"{prefix} must be an object")
                continue
            key = (change.get("requirement_id"), change.get("action"))
            require(all(isinstance(value, str) and bool(value) for value in key),
                    f"{prefix} requires requirement_id and action")
            require(change.get("action") in ("exclude", "priority-change"),
                    f"{prefix} action must be exclude or priority-change")
            require(isinstance(change.get("approval_id"), str) and bool(change.get("approval_id")),
                    f"{prefix} missing approval_id")
            if change.get("action") == "priority-change":
                require(change.get("to") in ("P1", "P2"), f"{prefix} priority-change requires to=P1 or P2")
            if key in seen_changes:
                errors.append(f"duplicate scope change: {key[0]} {key[1]}")
            seen_changes.add(key)

    for collection in ("requirements", "defect_classes", "gates"):
        items = data.get(collection)
        require(isinstance(items, list), f"{collection} must be an array")
        if not isinstance(items, list):
            continue
        seen = set()
        for index, item in enumerate(items):
            prefix = f"{collection}[{index}]"
            if not isinstance(item, dict):
                errors.append(f"{prefix} must be an object")
                continue
            name = item.get("id")
            require(isinstance(name, str) and bool(name), f"{prefix} missing id")
            if name in seen:
                errors.append(f"duplicate {collection} id: {name}")
            seen.add(name)
            require(item.get("status") in STATES, f"{prefix} invalid status")
            if collection == "requirements":
                require(item.get("priority") in ("P0", "P1", "P2", "unstated"),
                        f"{prefix} invalid priority")
            if item.get("status") == "PASS":
                if collection == "requirements":
                    require(bool(item.get("tests")) and bool(item.get("evidence_paths")),
                            f"{prefix} PASS requires tests and evidence_paths")
                elif collection == "gates":
                    require(is_sha(item.get("verified_sha")), f"{prefix} PASS requires verified_sha")
                    require(bool(item.get("evidence_paths")), f"{prefix} PASS requires evidence_paths")
                    require(item.get("verified_sha") == source, f"{prefix} verified SHA differs from source")
                elif collection == "defect_classes":
                    require(is_sha(item.get("fix_sha")) and bool(item.get("reviewer_evidence")),
                            f"{prefix} PASS requires full fix_sha and reviewer_evidence")
                    if str(item.get("severity", "")).upper() in ("CRITICAL", "HIGH"):
                        require(bool(item.get("evidence_paths")),
                                f"{prefix} critical/high PASS requires evidence_paths")

    gates = {
        gate.get("id"): gate
        for gate in data.get("gates", [])
        if isinstance(gate, dict) and isinstance(gate.get("id"), str)
    }
    require(REQUIRED_GATES.issubset(gates),
            "missing required gates: " + ", ".join(sorted(REQUIRED_GATES - set(gates))))
    accepted = data.get("accepted")
    require(type(accepted) is bool, "accepted must be boolean")
    if accepted is True:
        require(compute_verdict(data) == "ACCEPTED",
                "declared acceptance conflicts with ledger-local status")
        require(is_sha(deployed) and deployed == source, "accepted requires deployed SHA equal to source SHA")
        for gate in REQUIRED_GATES:
            require(gates.get(gate, {}).get("status") == "PASS", f"accepted requires {gate} PASS")
        for item in data.get("requirements", []):
            if isinstance(item, dict) and item.get("priority") == "P0":
                require(item.get("status") == "PASS",
                        f"accepted requires P0 requirement {item.get('id')} PASS")
        for item in data.get("defect_classes", []):
            if isinstance(item, dict) and str(item.get("severity", "")).upper() in ("CRITICAL", "HIGH"):
                require(item.get("status") == "PASS",
                        f"accepted requires critical/high defect class {item.get('id')} closed")
    return errors


def compute_verdict(data):
    """Compute only from ledger-local state; external trust is checked elsewhere."""
    if not isinstance(data, dict) or not is_sha(data.get("source_commit_sha")):
        return "BLOCKED"
    source = data["source_commit_sha"]
    if data.get("deployed_commit_sha") != source:
        return "BLOCKED"
    if any(not isinstance(data.get(key), list) for key in ("gates", "requirements", "defect_classes")):
        return "BLOCKED"
    gates = {gate.get("id"): gate for gate in data["gates"] if isinstance(gate, dict)}
    if not REQUIRED_GATES.issubset(gates):
        return "BLOCKED"
    for gate in (gates[key] for key in REQUIRED_GATES):
        if gate.get("status") == "FAIL":
            return "FAIL"
        if gate.get("status") != "PASS" or gate.get("verified_sha") != source or not gate.get("evidence_paths"):
            return "BLOCKED"
    for item in data["requirements"]:
        if not isinstance(item, dict):
            return "BLOCKED"
        if item.get("priority") == "P0":
            if item.get("status") == "FAIL":
                return "FAIL"
            if item.get("status") != "PASS" or not item.get("tests") or not item.get("evidence_paths"):
                return "BLOCKED"
    for item in data["defect_classes"]:
        if not isinstance(item, dict):
            return "BLOCKED"
        if str(item.get("severity", "")).upper() in ("HIGH", "CRITICAL"):
            if item.get("status") == "FAIL":
                return "FAIL"
            if item.get("status") != "PASS" or not item.get("reviewer_evidence"):
                return "BLOCKED"
    if any(not isinstance(item, dict) or item.get("disposition") not in ("APPROVED", "RESOLVED")
           for item in data.get("unresolved_decisions", [])):
        return "BLOCKED"
    return "ACCEPTED"


def main():
    if len(sys.argv) != 2:
        print("Usage: validate-acceptance-ledger.py LEDGER.json", file=sys.stderr)
        return 2
    try:
        data = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    except (OSError, ValueError) as ex:
        print(f"Invalid ledger: {ex}", file=sys.stderr)
        return 2
    errors = check(data)
    for error in errors:
        print("FAIL: " + error, file=sys.stderr)
    if errors:
        print(f"Ledger rejected: {len(errors)} violation(s)", file=sys.stderr)
        return 1
    print("Ledger structure valid; independent acceptance requires compute-acceptance-verdict.py with trust inputs.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
