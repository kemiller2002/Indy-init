#!/usr/bin/env python3
"""Independent positive and adversarial acceptance-verdict tests."""
import copy
import hashlib
import importlib.util
import json
import pathlib
import subprocess
import tempfile
import unittest

HERE = pathlib.Path(__file__).parent
TOOL = HERE / "compute-acceptance-verdict.py"
spec = importlib.util.spec_from_file_location("verdict", TOOL)
verdict = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verdict)


def json_bytes(value):
    return (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")


def sha256(raw):
    return hashlib.sha256(raw).hexdigest()


def git(directory, *args):
    return subprocess.run(
        ["git", "-C", str(directory), *args], check=True, capture_output=True, text=True).stdout.strip()


class AcceptanceFixture:
    def __init__(self, root):
        self.root = pathlib.Path(root)
        self.baseline_repo = self.root / "requirements-source"
        self.candidate_repo = self.root / "candidate"
        self.trust_dir = self.root / "trust"
        self.evidence_root = self.root / "evidence"
        for path in (self.baseline_repo, self.candidate_repo, self.trust_dir, self.evidence_root):
            path.mkdir(parents=True)

        git(self.baseline_repo, "init", "-q")
        source_text = b"# Requirements\n\nP0 core, write, and scope requirements.\n"
        (self.baseline_repo / "requirements.md").write_bytes(source_text)
        git(self.baseline_repo, "add", "requirements.md")
        subprocess.run([
            "git", "-C", str(self.baseline_repo), "-c", "user.name=test",
            "-c", "user.email=test@example.invalid", "commit", "-qm", "requirements",
        ], check=True)
        self.baseline_commit = git(self.baseline_repo, "rev-parse", "HEAD")
        self.baseline = {
            "schema": "praxis.requirements-baseline/1",
            "id": "BL-CAUSA-TEST",
            "source": {
                "repository": "example/causa-requirements",
                "commit": self.baseline_commit,
                "documents": [{"path": "requirements.md", "sha256": sha256(source_text)}],
            },
            "requirements": [
                {"id": "P0-CORE", "priority": "P0", "obligations": []},
                {"id": "WRITE-001", "priority": "P0", "obligations": []},
                {"id": "P0-SCOPE", "priority": "P0", "obligations": []},
                {"id": "P1-OPTIONAL", "priority": "P1", "obligations": []},
            ],
            "conflicts": [],
        }
        self.baseline_path = self.trust_dir / "baseline.json"
        self._write(self.baseline_path, self.baseline)
        self.baseline_digest = sha256(self.baseline_path.read_bytes())

        self.approvals = {
            "schema": "causa.requirements-approvals/1",
            "baseline_sha256": self.baseline_digest,
            "approvals": [],
        }
        self.approvals_path = self.trust_dir / "approvals.json"
        self.save_approvals()

        git(self.candidate_repo, "init", "-q")
        (self.candidate_repo / "app.txt").write_text("candidate\n", encoding="utf-8")
        git(self.candidate_repo, "add", "app.txt")
        subprocess.run([
            "git", "-C", str(self.candidate_repo), "-c", "user.name=test",
            "-c", "user.email=test@example.invalid", "commit", "-qm", "candidate",
        ], check=True)
        self.source_sha = git(self.candidate_repo, "rev-parse", "HEAD")

        self.ledger = {
            "schema": "causa.acceptance-ledger/2",
            "run_id": "acceptance-run-1",
            "source_commit_sha": self.source_sha,
            "deployed_commit_sha": self.source_sha,
            "contract_version": "causa-storage-v1.0.0",
            "frozen_fixture_digest": "fixture",
            "dependency_lock_sha": "lock",
            "interface_manifest_sha": "interface",
            "requirements_baseline": {
                "id": self.baseline["id"],
                "repository": self.baseline["source"]["repository"],
                "commit": self.baseline_commit,
                "sha256": self.baseline_digest,
            },
            "approvals_sha256": self.approvals_digest,
            "evidence_manifest_sha256": "0" * 64,
            "scope_changes": [],
            "accepted": True,
            "requirements": [
                {"id": rid, "priority": "P0", "status": "PASS", "tests": [f"test-{rid}"],
                 "evidence_paths": [f"requirements/{rid}.json"]}
                for rid in ("P0-CORE", "WRITE-001", "P0-SCOPE")
            ] + [{"id": "P1-OPTIONAL", "priority": "P1", "status": "NOT_RUN",
                  "tests": [], "evidence_paths": []}],
            "defect_classes": [{
                "id": "DC-001", "severity": "critical", "status": "PASS",
                "fix_sha": self.source_sha, "reviewer_evidence": "independent review",
                "evidence_paths": ["defects/DC-001.json"],
            }],
            "gates": [
                {"id": f"G{i}", "status": "PASS", "verified_sha": self.source_sha,
                 "evidence_paths": [f"gates/G{i}.json"]}
                for i in range(10)
            ],
            "unresolved_decisions": [],
        }
        self.manifest = {
            "schema": "causa.acceptance-evidence-manifest/1",
            "run_id": self.ledger["run_id"],
            "source_sha": self.source_sha,
            "entries": [],
        }
        for gate in self.ledger["gates"]:
            evidence_type = "ci" if gate["id"] == "G8" else "deployment" if gate["id"] == "G9" else "test"
            self.add_evidence(gate["evidence_paths"][0], "gate", gate["id"], evidence_type)
        for requirement in self.ledger["requirements"]:
            if requirement["status"] == "PASS":
                self.add_evidence(requirement["evidence_paths"][0], "requirement", requirement["id"], "test")
        self.add_evidence("defects/DC-001.json", "defect", "DC-001", "review")
        self.manifest_path = self.trust_dir / "evidence-manifest.json"
        self.save_manifest()
        self.ledger_path = self.root / "ledger.json"
        self.save_ledger()

    @staticmethod
    def _write(path, value):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(json_bytes(value))

    def add_evidence(self, relative, subject_type, subject_id, evidence_type):
        payload = {
            "schema": "causa.acceptance-evidence/1",
            "run_id": self.ledger["run_id"],
            "source_sha": self.source_sha,
            "subject_type": subject_type,
            "subject_id": subject_id,
            "evidence_type": evidence_type,
            "status": "PASS",
            "command": f"verify {subject_type} {subject_id}",
            "exit_code": 0,
        }
        path = self.evidence_root / relative
        self._write(path, payload)
        self.manifest["entries"].append({"path": relative, "sha256": sha256(path.read_bytes())})

    def save_approvals(self):
        self._write(self.approvals_path, self.approvals)
        self.approvals_digest = sha256(self.approvals_path.read_bytes())
        if hasattr(self, "ledger"):
            self.ledger["approvals_sha256"] = self.approvals_digest

    def save_manifest(self):
        self._write(self.manifest_path, self.manifest)
        self.manifest_digest = sha256(self.manifest_path.read_bytes())
        self.ledger["evidence_manifest_sha256"] = self.manifest_digest

    def save_ledger(self):
        self._write(self.ledger_path, self.ledger)

    def refresh_entry(self, relative):
        for entry in self.manifest["entries"]:
            if entry["path"] == relative:
                entry["sha256"] = sha256((self.evidence_root / relative).read_bytes())
                return
        raise AssertionError(relative)

    def command(self):
        return [
            "python3", "-I", str(TOOL), str(self.ledger_path),
            "--repo", str(self.candidate_repo),
            "--baseline", str(self.baseline_path),
            "--baseline-sha256", self.baseline_digest,
            "--baseline-source", str(self.baseline_repo),
            "--approvals", str(self.approvals_path),
            "--approvals-sha256", self.approvals_digest,
            "--evidence-manifest", str(self.manifest_path),
            "--evidence-manifest-sha256", self.manifest_digest,
            "--evidence-root", str(self.evidence_root),
            "--trusted-source-sha", self.source_sha,
            "--trusted-operator-id", "repository-owner",
        ]

    def run(self):
        self.save_ledger()
        result = subprocess.run(self.command(), capture_output=True, text=True)
        return result, json.loads(result.stdout)


class VerdictTests(unittest.TestCase):
    def assert_rejected(self, mutate, diagnostic):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = AcceptanceFixture(temporary)
            mutate(fixture)
            result, payload = fixture.run()
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertNotEqual(payload["verdict"], "ACCEPTED")
            messages = "\n".join(payload["reasons"] + payload["claim_problems"])
            self.assertIn(diagnostic, messages)
            self.assertEqual(git(fixture.candidate_repo, "status", "--porcelain"), "")

    def test_a01_complete_valid_acceptance(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = AcceptanceFixture(temporary)
            result, payload = fixture.run()
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertEqual(payload, {"verdict": "ACCEPTED", "reasons": [], "claim_problems": []})
            self.assertEqual(git(fixture.candidate_repo, "status", "--porcelain"), "")

    def test_a02_remove_p0_requirement(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.ledger["requirements"] = [
                item for item in fixture.ledger["requirements"] if item["id"] != "P0-CORE"]
        self.assert_rejected(mutate, "missing mandatory baseline P0 requirement: P0-CORE")

    def test_a03_downgrade_p0_without_approval(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.ledger["requirements"][0]["priority"] = "P2"
        self.assert_rejected(mutate, "was downgraded without operator approval")

    def test_a04_blocked_gate(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.ledger["gates"][4].update(status="BLOCKED", verified_sha=None, evidence_paths=[])
        self.assert_rejected(mutate, "gate G4: BLOCKED")

    def test_a05_not_run_gate(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.ledger["gates"][5].update(status="NOT_RUN", verified_sha=None, evidence_paths=[])
        self.assert_rejected(mutate, "gate G5: NOT_RUN")

    def test_a06_missing_ci_evidence(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.ledger["gates"][8]["evidence_paths"] = []
        self.assert_rejected(mutate, "PASS requires evidence_paths")

    def test_a07_stale_evidence(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.manifest["source_sha"] = "b" * 40
            fixture.save_manifest()
        self.assert_rejected(mutate, "evidence manifest is stale")

    def test_a08_source_deployment_mismatch(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.ledger["deployed_commit_sha"] = "b" * 40
        self.assert_rejected(mutate, "deployed_commit_sha is missing or differs")

    def test_a09_nonexistent_evidence(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            (fixture.evidence_root / "gates/G3.json").unlink()
        self.assert_rejected(mutate, "evidence artifact cannot be retrieved")

    def test_a10_agent_self_approves_scope_reduction(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.ledger["requirements"] = [
                item for item in fixture.ledger["requirements"] if item["id"] != "P0-SCOPE"]
            fixture.ledger["scope_changes"] = [{
                "requirement_id": "P0-SCOPE", "action": "exclude", "approval_id": "SELF-1"}]
            fixture.ledger["approvals"] = [{"id": "SELF-1", "authority": "agent"}]
        self.assert_rejected(mutate, "inline approvals are not trusted")

    def test_a11_unauthenticated_approval(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.ledger["requirements"] = [
                item for item in fixture.ledger["requirements"] if item["id"] != "P0-SCOPE"]
            fixture.ledger["scope_changes"] = [{
                "requirement_id": "P0-SCOPE", "action": "exclude", "approval_id": "FORGED-1"}]
            fixture.approvals["approvals"] = [{
                "id": "FORGED-1",
                "authority": {"kind": "operator", "id": "operator-impostor"},
                "source": {"repository": "example/decisions", "commit": "c" * 40, "path": "decision.json"},
                "changes": [{"requirement_id": "P0-SCOPE", "action": "exclude"}],
            }]
            fixture.save_approvals()
        self.assert_rejected(mutate, "is not authenticated as a trusted operator decision")

    def test_externally_pinned_trusted_operator_approval_is_honored(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = AcceptanceFixture(temporary)
            fixture.ledger["requirements"] = [
                item for item in fixture.ledger["requirements"] if item["id"] != "P0-SCOPE"]
            fixture.ledger["scope_changes"] = [{
                "requirement_id": "P0-SCOPE", "action": "exclude", "approval_id": "OP-1"}]
            fixture.approvals["approvals"] = [{
                "id": "OP-1",
                "authority": {"kind": "operator", "id": "repository-owner"},
                "source": {
                    "repository": "example/decisions",
                    "commit": "c" * 40,
                    "path": "decision.json",
                },
                "changes": [{"requirement_id": "P0-SCOPE", "action": "exclude"}],
            }]
            fixture.save_approvals()
            result, payload = fixture.run()
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertEqual(payload["verdict"], "ACCEPTED")

    def test_a12_unresolved_critical_defect(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.ledger["defect_classes"][0].update(status="BLOCKED", evidence_paths=[])
        self.assert_rejected(mutate, "defect class DC-001: BLOCKED")

    def test_a13_accepted_flag_cannot_override_blocked_gate(self):
        def mutate(fixture):
            fixture.ledger["gates"][2].update(status="BLOCKED", verified_sha=None, evidence_paths=[])
        self.assert_rejected(mutate, "ledger 'accepted' is true")

    def test_a14_altered_evidence_artifact(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            path = fixture.evidence_root / "gates/G1.json"
            payload = json.loads(path.read_text(encoding="utf-8"))
            payload["command"] = "altered after verification"
            fixture._write(path, payload)
        self.assert_rejected(mutate, "evidence artifact digest mismatch")

    def test_a15_remove_required_write_functionality(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.ledger["requirements"] = [
                item for item in fixture.ledger["requirements"] if item["id"] != "WRITE-001"]
        self.assert_rejected(mutate, "missing mandatory baseline P0 requirement: WRITE-001")

    def test_a16_cross_run_evidence_reuse(self):
        def mutate(fixture):
            fixture.ledger["accepted"] = False
            fixture.manifest["run_id"] = "another-run"
            for entry in fixture.manifest["entries"]:
                path = fixture.evidence_root / entry["path"]
                payload = json.loads(path.read_text(encoding="utf-8"))
                payload["run_id"] = "another-run"
                fixture._write(path, payload)
                fixture.refresh_entry(entry["path"])
            fixture.save_manifest()
        self.assert_rejected(mutate, "evidence manifest belongs to a different run")

    def test_report_overclaim_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = AcceptanceFixture(temporary)
            fixture.ledger["accepted"] = False
            fixture.ledger["gates"][0].update(status="BLOCKED", verified_sha=None, evidence_paths=[])
            fixture.save_ledger()
            report = fixture.root / "report.md"
            report.write_text("<!-- acceptance-verdict: BLOCKED -->\nOutcome: ACCEPTED\n", encoding="utf-8")
            command = fixture.command() + ["--report", str(report)]
            result = subprocess.run(command, capture_output=True, text=True)
            payload = json.loads(result.stdout)
            self.assertEqual(result.returncode, 1)
            self.assertTrue(any("claims acceptance" in problem for problem in payload["claim_problems"]))

    def test_rehearsal_2_original_ledger_is_not_independently_acceptable(self):
        historical = json.loads((HERE / "fixtures/rehearsal-002-ledger.json").read_text(encoding="utf-8"))
        self.assertEqual(
            next(item for item in historical["requirements"] if item["id"] == "ADRP-016")["status"],
            "NOT_RUN",
        )
        result = verdict.compute(historical)
        self.assertEqual(result["verdict"], "INVALID")
        self.assertTrue(result["claim_problems"])


class PreflightTests(unittest.TestCase):
    """Pre-T0 trust preflight: the same baseline and approval rules acceptance applies."""

    def preflight(self, fixture, operator="repository-owner"):
        command = [
            "python3", "-I", str(TOOL), "--preflight",
            "--baseline", str(fixture.baseline_path),
            "--baseline-sha256", fixture.baseline_digest,
            "--baseline-source", str(fixture.baseline_repo),
            "--approvals", str(fixture.approvals_path),
            "--approvals-sha256", fixture.approvals_digest,
            "--trusted-operator-id", operator,
        ]
        result = subprocess.run(command, capture_output=True, text=True)
        return result.returncode, json.loads(result.stdout) if result.stdout else None

    def approval(self, **overrides):
        value = {
            "id": "APR-TEST-1",
            "authority": {"kind": "operator", "id": "repository-owner"},
            "source": {"repository": "example/causa-requirements", "commit": "a" * 40,
                       "path": "decisions/APR-TEST-1.md"},
            "changes": [{"requirement_id": "P0-SCOPE", "action": "exclude"}],
        }
        value.update(overrides)
        return value

    def test_p01_valid_trust_inputs_are_ready_without_a_ledger(self):
        with tempfile.TemporaryDirectory() as tmp:
            fixture = AcceptanceFixture(tmp)
            fixture.approvals["approvals"] = [self.approval()]
            fixture.save_approvals()
            code, payload = self.preflight(fixture)
            self.assertEqual((code, payload["verdict"], payload["problems"]), (0, "READY", []))
            self.assertEqual(payload["trusted_changes"], 1)

    def test_p02_approval_without_immutable_source_is_invalid_with_owner_action(self):
        with tempfile.TemporaryDirectory() as tmp:
            fixture = AcceptanceFixture(tmp)
            approval = self.approval()
            del approval["source"]
            fixture.approvals["approvals"] = [approval]
            fixture.save_approvals()
            code, payload = self.preflight(fixture)
            self.assertEqual((code, payload["verdict"]), (3, "INVALID"))
            problem = payload["problems"][0]
            self.assertIn("lacks an immutable authority source", problem["problem"])
            self.assertTrue(problem["owner_action"].startswith("owner:"))

    def test_p03_rehearsal_8_change_shape_is_invalid(self):
        """Rehearsal 8: changes named obligation_id/decision_id, which the verifier cannot bind."""
        with tempfile.TemporaryDirectory() as tmp:
            fixture = AcceptanceFixture(tmp)
            fixture.approvals["approvals"] = [self.approval(changes=[
                {"obligation_id": "PERSIST-026-O2", "action": "amend", "criterion": "..."},
                {"decision_id": "ODR-V41-06", "action": "select", "option": "A"}])]
            fixture.save_approvals()
            code, payload = self.preflight(fixture)
            self.assertEqual(code, 3)
            self.assertEqual(sum("invalid change identity" in p["problem"] for p in payload["problems"]), 2)

    def test_p04_untrusted_operator_and_tampered_digest_are_invalid(self):
        with tempfile.TemporaryDirectory() as tmp:
            fixture = AcceptanceFixture(tmp)
            fixture.approvals["approvals"] = [self.approval()]
            fixture.save_approvals()
            code, payload = self.preflight(fixture, operator="someone-else")
            self.assertEqual(code, 3)
            self.assertTrue(any("not authenticated" in p["problem"] for p in payload["problems"]))
            fixture.approvals_digest = "0" * 64
            code, payload = self.preflight(fixture)
            self.assertEqual(code, 3)
            self.assertTrue(any("SHA-256 mismatch" in p["problem"] for p in payload["problems"]))

    def test_p05_preflight_and_acceptance_agree_on_trust_problems(self):
        """No second interpretation: every preflight problem is also an acceptance trust problem."""
        with tempfile.TemporaryDirectory() as tmp:
            fixture = AcceptanceFixture(tmp)
            approval = self.approval(changes=[{"obligation_id": "X-O1", "action": "amend"}])
            del approval["source"]
            fixture.approvals["approvals"] = [approval]
            fixture.save_approvals()
            _, payload = self.preflight(fixture)
            result = subprocess.run(fixture.command(), capture_output=True, text=True)
            reasons = json.loads(result.stdout)["reasons"]
            for problem in payload["problems"]:
                self.assertIn(problem["problem"], reasons)

    def test_p06_ledger_still_required_without_preflight(self):
        result = subprocess.run(["python3", "-I", str(TOOL)], capture_output=True, text=True)
        self.assertEqual(result.returncode, 2)


class DryRunVerdictTests(unittest.TestCase):
    """Synthetic evidence: the verifier distinguishes every verdict (ACCEPTED, FAILED, BLOCKED,
    INCOMPLETE for NOT_RUN, INVALID), and FAIL takes precedence over BLOCKED over NOT_RUN."""

    def verdict_with(self, statuses, break_trust=False):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = AcceptanceFixture(temporary)
            if statuses or break_trust:
                fixture.ledger["accepted"] = False
            for index, status in statuses.items():
                fixture.ledger["gates"][index].update(status=status, verified_sha=None, evidence_paths=[])
            if break_trust:
                fixture.approvals_digest = "0" * 64
            result, payload = fixture.run()
            return result.returncode, payload["verdict"]

    def test_d01_every_verdict_is_distinguished(self):
        self.assertEqual(self.verdict_with({}), (0, "ACCEPTED"))
        self.assertEqual(self.verdict_with({3: "FAIL"}), (3, "FAILED"))
        self.assertEqual(self.verdict_with({0: "BLOCKED"}), (3, "BLOCKED"))
        self.assertEqual(self.verdict_with({5: "NOT_RUN"}), (3, "INCOMPLETE"))
        self.assertEqual(self.verdict_with({}, break_trust=True), (3, "INVALID"))

    def test_d02_precedence(self):
        self.assertEqual(self.verdict_with({0: "BLOCKED", 3: "FAIL", 5: "NOT_RUN"})[1], "FAILED")
        self.assertEqual(self.verdict_with({0: "BLOCKED", 5: "NOT_RUN"})[1], "BLOCKED")


if __name__ == "__main__":
    unittest.main()
