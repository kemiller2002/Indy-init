# Rehearsal 7 preparation: persistence requirements

Requirements-only preparation for Rehearsal 7 under owner decision ADR-R7-001. Rehearsal 7 has not started, and no application code exists.

| # | Deliverable | Where |
|---|---|---|
| 1 | Persistence architecture decision | [PERSISTENCE-ARCHITECTURE-DECISION.md](PERSISTENCE-ARCHITECTURE-DECISION.md) |
| 2 | Functional and nonfunctional requirements | [CAUSA-PERSISTENCE-REQUIREMENTS.md](../CAUSA-PERSISTENCE-REQUIREMENTS.md) PERSIST-001 to PERSIST-026, and the amended R9.7, R10.5 to R10.7, AR-035, D-204, ADRP-011, ADRP-012, the kickoff packet and the interface freeze |
| 3 | UI requirements | PERSIST-030 to PERSIST-033 and the amended UI-006, UI-010 and S23 |
| 4 | Acceptance obligations | PERSIST-040 to PERSIST-055 (PAT-01 to PAT-15), and the acceptance obligation in every PERSIST requirement |
| 5 | Historical defect-to-requirement traceability | [DEFECT-TRACEABILITY.md](DEFECT-TRACEABILITY.md), [traceability.json](traceability.json) |
| 6 | Requirements validation report | [REQUIREMENTS-VALIDATION-REPORT.md](REQUIREMENTS-VALIDATION-REPORT.md), with machine output in [evidence/](evidence/) |
| 7 | Rehearsal 7 readiness assessment | [READINESS-ASSESSMENT.md](READINESS-ASSESSMENT.md) |

The baseline is [`requirements/causa-requirements-baseline-r7.json`](../../requirements/causa-requirements-baseline-r7.json) (`BL-CAUSA-INDY-INIT-8445ca11986f`). The Rehearsal 7 owner decisions are in [`kickoff/rehearsal-007/`](../../kickoff/rehearsal-007/OWNER-DECISIONS.md). The validator is `tools/validate-r7-persistence-requirements.fsx`.

## Rehearsal 6 remediation and Rehearsal 7 readiness

The remediation initiative (R7-REM, intake [`input-documents/2026-10-29-R7-REM-rehearsal-6-remediation.md`](../../input-documents/2026-10-29-R7-REM-rehearsal-6-remediation.md)) covers everything outside persistence that Rehearsal 6 left open: CI starvation, credentials, unattended merging, evidence, candidate gates, defect-class closure, Forma contrast and release comparison. It also covers library fixes in Arca.

| Deliverable | Where |
|---|---|
| Final report | [REMEDIATION-RESULTS.md](REMEDIATION-RESULTS.md) |
| Rehearsal 6 findings and their evidence | [REMEDIATION-BASELINE.md](REMEDIATION-BASELINE.md), [remediation-baseline.json](remediation-baseline.json) |
| Work packages R7-REM-001..012 | [REMEDIATION-PLAN.md](REMEDIATION-PLAN.md), generated from [work-packages.json](work-packages.json) |
| Preflight gates R7-PF-01..14 | [READINESS.md](READINESS.md), [readiness-gates.json](readiness-gates.json) |
| Timed-run plan and early adversarial testing | [EXECUTION-PLAN.md](EXECUTION-PLAN.md) |
| Verification matrix | [VERIFICATION-MATRIX.md](VERIFICATION-MATRIX.md), [verification-matrix.json](verification-matrix.json) |
| Credentials and tool security | [SECURITY-REVIEW.md](SECURITY-REVIEW.md) |
| CI starvation analysis | [CI-PERFORMANCE.md](CI-PERFORMANCE.md) |
| Unattended execution (Experiment A) | [AUTONOMOUS-EXECUTION.md](AUTONOMOUS-EXECUTION.md) |
| Verifier outputs on Rehearsal 6 and the release demonstration | [evidence/](evidence/) |
