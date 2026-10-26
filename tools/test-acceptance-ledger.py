#!/usr/bin/env python3
"""Unit tests for the v2 structural acceptance-ledger contract."""
import importlib.util
import pathlib
import unittest

module_path = pathlib.Path(__file__).with_name("validate-acceptance-ledger.py")
spec = importlib.util.spec_from_file_location("causa_ledger", module_path)
ledger = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ledger)

SHA = "a" * 40
DIGEST = "b" * 64


def base():
    return {
        "schema": "causa.acceptance-ledger/2",
        "run_id": "preflight",
        "source_commit_sha": SHA,
        "deployed_commit_sha": None,
        "contract_version": "1.0.0",
        "frozen_fixture_digest": "fixture",
        "dependency_lock_sha": "lock",
        "interface_manifest_sha": "interface",
        "requirements_baseline": {
            "id": "BL-TEST",
            "repository": "example/requirements",
            "commit": SHA,
            "sha256": DIGEST,
        },
        "approvals_sha256": DIGEST,
        "evidence_manifest_sha256": DIGEST,
        "scope_changes": [],
        "accepted": False,
        "requirements": [],
        "defect_classes": [],
        "gates": [
            {"id": f"G{i}", "status": "NOT_RUN", "verified_sha": None, "evidence_paths": []}
            for i in range(10)
        ],
    }


class LedgerTests(unittest.TestCase):
    def test_pending_valid(self):
        self.assertEqual(ledger.check(base()), [])

    def test_computed_blocked_even_if_agent_declares_false(self):
        self.assertEqual(ledger.compute_verdict(base()), "BLOCKED")

    def test_computed_accepted_complete_synthetic_ledger(self):
        item = base()
        item["deployed_commit_sha"] = SHA
        item["gates"] = [
            {"id": f"G{i}", "status": "PASS", "verified_sha": SHA, "evidence_paths": [f"g{i}.json"]}
            for i in range(10)
        ]
        item["requirements"] = [{
            "id": "P0-1", "priority": "P0", "status": "PASS",
            "tests": ["test-1"], "evidence_paths": ["p0.json"],
        }]
        self.assertEqual(ledger.compute_verdict(item), "ACCEPTED")
        item["gates"][1]["status"] = "NOT_RUN"
        self.assertEqual(ledger.compute_verdict(item), "BLOCKED")

    def test_false_accepted_flag_rejected_with_blocked_gate(self):
        item = base()
        item["accepted"] = True
        self.assertTrue(any("ledger-local status" in error for error in ledger.check(item)))

    def test_missing_gate_rejected(self):
        item = base()
        item["gates"].pop()
        self.assertTrue(ledger.check(item))

    def test_duplicate_requirement_rejected(self):
        item = base()
        item["requirements"] = [
            {"id": "UI-001", "priority": "P0", "status": "NOT_RUN"},
            {"id": "UI-001", "priority": "P0", "status": "NOT_RUN"},
        ]
        self.assertTrue(any("duplicate" in error for error in ledger.check(item)))

    def test_unproven_pass_rejected(self):
        item = base()
        item["requirements"] = [{"id": "UI-001", "status": "PASS", "priority": "P0"}]
        self.assertTrue(any("requires tests" in error for error in ledger.check(item)))

    def test_verified_gate_sha_mismatch_rejected(self):
        item = base()
        item["gates"][0] = {
            "id": "G0", "status": "PASS", "verified_sha": "c" * 40,
            "evidence_paths": ["proof.json"],
        }
        self.assertTrue(any("differs from source" in error for error in ledger.check(item)))

    def test_inline_approval_rejected(self):
        item = base()
        item["approvals"] = [{"authority": "agent"}]
        self.assertTrue(any("inline approvals" in error for error in ledger.check(item)))

    def test_scope_change_requires_external_approval_reference(self):
        item = base()
        item["scope_changes"] = [{"requirement_id": "P0-1", "action": "exclude"}]
        self.assertTrue(any("approval_id" in error for error in ledger.check(item)))

    def test_critical_defect_pass_requires_evidence_path(self):
        item = base()
        item["defect_classes"] = [{
            "id": "DC-1", "severity": "critical", "status": "PASS",
            "fix_sha": SHA, "reviewer_evidence": "independent review",
        }]
        self.assertTrue(any("critical/high PASS" in error for error in ledger.check(item)))


if __name__ == "__main__":
    unittest.main()
