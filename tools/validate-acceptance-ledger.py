#!/usr/bin/env python3
"""Validate Causa rehearsal evidence ledger. stdlib only; fail closed."""
import json
import re
import sys
from pathlib import Path

STATES = {"NOT_RUN", "PASS", "FAIL", "BLOCKED"}
REQUIRED_GATES = {f"G{i}" for i in range(10)}

def check(data):
    errors = []
    def require(cond, msg):
        if not cond:
            errors.append(msg)
    require(isinstance(data, dict), "root must be an object")
    if not isinstance(data, dict):
        return errors
    for key in ("run_id", "source_commit_sha", "contract_version", "frozen_fixture_digest", "dependency_lock_sha", "interface_manifest_sha"):
        require(isinstance(data.get(key), str) and bool(data.get(key)), f"missing {key}")
    source = data.get("source_commit_sha")
    deployed = data.get("deployed_commit_sha")
    sha = lambda v: isinstance(v, str) and re.fullmatch(r"[0-9a-f]{40}", v) is not None
    require(sha(source), "source_commit_sha must be 40 lowercase hex")
    if deployed is not None:
        require(sha(deployed), "deployed_commit_sha must be 40 lowercase hex or null")
    for collection, identity in (("requirements", "id"), ("defect_classes", "id"), ("gates", "id")):
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
            name = item.get(identity)
            require(isinstance(name, str) and bool(name), f"{prefix} missing id")
            if name in seen:
                errors.append(f"duplicate {collection} id: {name}")
            seen.add(name)
            require(item.get("status") in STATES, f"{prefix} invalid status")
            if item.get("status") == "PASS":
                if collection == "requirements":
                    require(bool(item.get("tests")) and bool(item.get("evidence_paths")), f"{prefix} PASS requires tests and evidence_paths")
                if collection == "gates":
                    require(sha(item.get("verified_sha")), f"{prefix} PASS requires verified_sha")
                    require(bool(item.get("evidence_paths")), f"{prefix} PASS requires evidence_paths")
                    require(item.get("verified_sha") == source, f"{prefix} verified SHA differs from source")
                if collection == "defect_classes":
                    require(sha(item.get("fix_sha")) and bool(item.get("reviewer_evidence")), f"{prefix} PASS requires fix_sha and reviewer_evidence")
    gates = {g.get("id"): g for g in data.get("gates", []) if isinstance(g, dict) and isinstance(g.get("id"), str)}
    require(REQUIRED_GATES.issubset(gates), "missing required gates: " + ", ".join(sorted(REQUIRED_GATES - set(gates))))
    accepted = data.get("accepted")
    require(type(accepted) is bool, "accepted must be boolean")
    if accepted is True:
        require(compute_verdict(data) == "ACCEPTED", "declared acceptance conflicts with computed verdict")
        require(sha(deployed) and deployed == source, "accepted requires deployed SHA equal to source SHA")
        for gate in REQUIRED_GATES:
            require(gates.get(gate, {}).get("status") == "PASS", f"accepted requires {gate} PASS")
        for item in data.get("requirements", []):
            if isinstance(item, dict) and item.get("priority") == "P0":
                require(item.get("status") == "PASS", f"accepted requires P0 requirement {item.get('id')} PASS")
        for item in data.get("defect_classes", []):
            # Case-insensitive: Rehearsal 2's ledger wrote "critical"/"high", which an exact
            # uppercase match silently treated as not critical.
            if isinstance(item, dict) and str(item.get("severity", "")).upper() in ("CRITICAL", "HIGH"):
                require(item.get("status") == "PASS", f"accepted requires critical/high defect class {item.get('id')} closed")
    return errors

def compute_verdict(data):
    """Compute from gate and requirement evidence, never from agent's accepted flag."""
    if not isinstance(data, dict):
        return "BLOCKED"
    source = data.get("source_commit_sha")
    if not isinstance(source, str) or not re.fullmatch(r"[0-9a-f]{40}", source):
        return "BLOCKED"
    if data.get("deployed_commit_sha") != source:
        return "BLOCKED"
    if any(not isinstance(data.get(k), list) for k in ("gates", "requirements", "defect_classes")):
        return "BLOCKED"
    gates = {g.get("id"): g for g in data["gates"] if isinstance(g, dict)}
    if not REQUIRED_GATES.issubset(gates):
        return "BLOCKED"
    for g in (gates[k] for k in REQUIRED_GATES):
        if g.get("status") == "FAIL":
            return "FAIL"
        if g.get("status") != "PASS" or g.get("verified_sha") != source or not g.get("evidence_paths"):
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
        if item.get("severity") in ("HIGH", "CRITICAL"):
            if item.get("status") == "FAIL":
                return "FAIL"
            if item.get("status") != "PASS" or not item.get("reviewer_evidence"):
                return "BLOCKED"
    if any(not isinstance(x, dict) or x.get("disposition") not in ("APPROVED", "RESOLVED") for x in data.get("unresolved_decisions", [])):
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
    print(f"Computed verdict: {compute_verdict(data)}; evidence existence and CI require independent verification.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
