#!/usr/bin/env python3
"""Tests for the computed acceptance verdict. stdlib only."""
import importlib.util
import json
import pathlib
import subprocess
import tempfile
import unittest

HERE = pathlib.Path(__file__).parent
spec = importlib.util.spec_from_file_location("verdict", HERE / "compute-acceptance-verdict.py")
verdict = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verdict)

SHA = "a" * 40
R3_CANDIDATE = "92ac7f32cdb6d32bfbdeeda0c3c892d5860ddc03"


def gate(gid, status="PASS", sha=SHA, paths=("proof.json",)):
    return {"id": gid, "status": status, "verified_sha": sha if status == "PASS" else None,
            "evidence_paths": list(paths) if status == "PASS" else []}


def requirement(rid, status="PASS", priority="P0"):
    return {"id": rid, "priority": priority, "status": status,
            "tests": ["t"] if status == "PASS" else [], "evidence_paths": ["proof.json"] if status == "PASS" else []}


def accepted_ledger(sha=SHA, **overrides):
    base = {
        "run_id": "synthetic", "source_commit_sha": sha, "deployed_commit_sha": sha,
        "contract_version": "causa-storage-v1.0.0", "frozen_fixture_digest": "fixture",
        "dependency_lock_sha": "lock", "interface_manifest_sha": "interface",
        "accepted": True,
        "requirements": [requirement("UI-016"), requirement("EXT-DATA-009"), requirement("UI-035", "NOT_RUN", "P1")],
        "defect_classes": [{"id": "DC-001", "severity": "critical", "status": "PASS", "fix_sha": sha, "reviewer_evidence": "review 1"}],
        "gates": [gate(f"G{i}", sha=sha) for i in range(10)],
    }
    return {**base, **overrides}


def rehearsal_3_original():
    """Rehearsal 3's original outcome (docs/REHEARSAL-003-REPORT.md section 3), encoded in the
    ledger contract: every gate PASS on the candidate, two mandatory conditions BLOCKED,
    and the first draft's claim of acceptance (db89a3a)."""
    return accepted_ledger(
        sha=R3_CANDIDATE,
        run_id="causa-rehearsal-003",
        requirements=[requirement("TEST-A-CAUSA_GITHUB_TOKEN", "BLOCKED"),
                      requirement("UI-COMPLETION-PERCEPTA", "BLOCKED"),
                      requirement("HYP-01")])


class VerdictTests(unittest.TestCase):
    def test_positive_control_accepted(self):
        result = verdict.compute(accepted_ledger())
        self.assertEqual(result["verdict"], "ACCEPTED")
        self.assertEqual(result["claim_problems"], [])

    def test_p1_not_run_does_not_block(self):
        self.assertEqual(verdict.compute(accepted_ledger())["verdict"], "ACCEPTED")

    def test_rehearsal_3_original_is_blocked_and_claim_rejected(self):
        result = verdict.compute(rehearsal_3_original())
        self.assertEqual(result["verdict"], "BLOCKED")
        self.assertTrue(any("accepted" in p for p in result["claim_problems"]))

    def test_rehearsal_3_honest_ledger_is_consistent(self):
        result = verdict.compute({**rehearsal_3_original(), "accepted": False})
        self.assertEqual(result["verdict"], "BLOCKED")
        self.assertEqual(result["claim_problems"], [])

    def test_rehearsal_2_ledger_is_not_accepted(self):
        """R2 accepted itself with its own tool. Under this repository's ledger contract its
        ledger is invalid (defect classes 'CLOSED', P0 PASS without tests) and P0 ADRP-016 is
        NOT_RUN, so the self-declared acceptance is a claim problem."""
        data = json.loads((HERE / "fixtures" / "rehearsal-002-ledger.json").read_text(encoding="utf-8"))
        result = verdict.compute(data)
        self.assertEqual(result["verdict"], "INVALID")
        self.assertTrue(any("P0 requirement ADRP-016: NOT_RUN" in r for r in result["reasons"]))
        self.assertTrue(result["claim_problems"])

    def test_fail_outranks_blocked(self):
        data = accepted_ledger(accepted=False, requirements=[requirement("A", "BLOCKED"), requirement("B", "FAIL")])
        self.assertEqual(verdict.compute(data)["verdict"], "FAILED")

    def test_not_run_p0_is_incomplete(self):
        data = accepted_ledger(accepted=False, requirements=[requirement("A", "NOT_RUN")])
        self.assertEqual(verdict.compute(data)["verdict"], "INCOMPLETE")

    def test_open_lowercase_critical_defect_blocks(self):
        data = accepted_ledger(accepted=False, defect_classes=[
            {"id": "DC-9", "severity": "high", "status": "BLOCKED", "fix_sha": None, "reviewer_evidence": ""}])
        self.assertEqual(verdict.compute(data)["verdict"], "BLOCKED")

    def test_undeployed_candidate_is_incomplete(self):
        data = accepted_ledger(accepted=False, deployed_commit_sha=None)
        self.assertEqual(verdict.compute(data)["verdict"], "INCOMPLETE")

    def test_missing_evidence_is_invalid(self):
        result = verdict.compute(accepted_ledger(), exists=lambda sha, path: False)
        self.assertEqual(result["verdict"], "INVALID")

    def test_report_overclaim_rejected(self):
        report = "<!-- acceptance-verdict: BLOCKED -->\n**Outcome: ACCEPTED, with recorded limitations.**\n"
        result = verdict.compute({**rehearsal_3_original(), "accepted": False}, report=report)
        self.assertTrue(any("claims acceptance" in p for p in result["claim_problems"]))

    def test_report_not_accepted_wording_allowed(self):
        report = "<!-- acceptance-verdict: BLOCKED -->\n**Outcome: NOT ACCEPTED (blocked).**\n"
        result = verdict.compute({**rehearsal_3_original(), "accepted": False}, report=report)
        self.assertEqual(result["claim_problems"], [])

    def test_report_marker_required(self):
        result = verdict.compute(accepted_ledger(), report="**Verdict: ACCEPTED**\n")
        self.assertTrue(any("exactly one" in p for p in result["claim_problems"]))

    def test_git_evidence_check(self):
        with tempfile.TemporaryDirectory() as tmp:
            run = lambda *args: subprocess.run(["git", "-C", tmp, *args], check=True, capture_output=True, text=True)
            run("init", "-q")
            (pathlib.Path(tmp) / "proof.json").write_text("{}", encoding="utf-8")
            run("add", "proof.json")
            run("-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-qm", "proof")
            head = run("rev-parse", "HEAD").stdout.strip()
            exists = verdict.git_exists(tmp)
            self.assertEqual(verdict.compute(accepted_ledger(sha=head), exists=exists)["verdict"], "ACCEPTED")
            moved = accepted_ledger(sha=head, gates=[gate(f"G{i}", sha=head, paths=("absent.json",)) for i in range(10)])
            self.assertEqual(verdict.compute(moved, exists=exists)["verdict"], "INVALID")


if __name__ == "__main__":
    unittest.main()
