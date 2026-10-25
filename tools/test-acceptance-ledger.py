#!/usr/bin/env python3
"""Unit tests for structural acceptance gate. No third-party dependencies."""
import importlib.util
import json
import pathlib
import unittest

module_path = pathlib.Path(__file__).with_name("validate-acceptance-ledger.py")
spec = importlib.util.spec_from_file_location("causa_ledger", module_path)
ledger = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ledger)
SHA = "a" * 40

def base():
    return {
        "run_id": "preflight", "source_commit_sha": SHA, "deployed_commit_sha": None,
        "contract_version": "1.0.0", "frozen_fixture_digest": "fixture",
        "dependency_lock_sha": "lock", "interface_manifest_sha": "interface",
        "accepted": False, "requirements": [],
        "defect_classes": [],
        "gates": [{"id": f"G{i}", "status": "NOT_RUN", "verified_sha": None, "evidence_paths": []} for i in range(10)]
    }

class LedgerTests(unittest.TestCase):
    def test_pending_valid(self):
        self.assertEqual(ledger.check(base()), [])

    def test_computed_blocked_even_if_agent_declares_false(self):
        self.assertEqual(ledger.compute_verdict(base()), "BLOCKED")

    def test_blocked_original_rehearsal_shape(self):
        x = base()
        x["deployed_commit_sha"] = SHA
        x["gates"][2]["status"] = "BLOCKED"
        self.assertEqual(ledger.compute_verdict(x), "BLOCKED")

    def test_computed_accepted_complete_synthetic_ledger(self):
        x = base()
        x["deployed_commit_sha"] = SHA
        x["gates"] = [{"id": f"G{i}", "status": "PASS", "verified_sha": SHA,
                       "evidence_paths": ["evidence/test.json"]} for i in range(10)]
        x["requirements"] = [{"id": "P0-1", "priority": "P0", "status": "PASS",
                              "tests": ["test-1"], "evidence_paths": ["evidence/test.json"]}]
        self.assertEqual(ledger.compute_verdict(x), "ACCEPTED")
        x["gates"][1]["status"] = "NOT_RUN"
        self.assertEqual(ledger.compute_verdict(x), "BLOCKED")

    def test_false_accepted_flag_rejected_with_blocked_gate(self):
        x = base()
        x["accepted"] = True
        self.assertTrue(any("computed verdict" in e for e in ledger.check(x)))

    def test_missing_gate_rejected(self):
        x = base()
        x["gates"].pop()
        self.assertTrue(ledger.check(x))

    def test_duplicate_requirement_rejected(self):
        x = base()
        x["requirements"] = [{"id": "UI-001", "status": "NOT_RUN"}] * 2
        self.assertTrue(any("duplicate" in e for e in ledger.check(x)))

    def test_false_acceptance_rejected(self):
        x = base()
        x["accepted"] = True
        self.assertTrue(ledger.check(x))

    def test_unproven_pass_rejected(self):
        x = base()
        x["requirements"] = [{"id": "UI-001", "status": "PASS", "priority": "P0"}]
        self.assertTrue(ledger.check(x))

    def test_wrong_deployment_rejected(self):
        x = base()
        x["accepted"] = True
        x["deployed_commit_sha"] = "b" * 40
        self.assertTrue(ledger.check(x))

    def test_verified_gate_sha_mismatch_rejected(self):
        x = base()
        x["gates"][0] = {"id": "G0", "status": "PASS", "verified_sha": "b" * 40, "evidence_paths": ["proof.json"]}
        self.assertTrue(ledger.check(x))

if __name__ == "__main__":
    unittest.main()
