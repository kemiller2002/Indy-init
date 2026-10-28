# Rehearsal 6 owner decisions

The machine-readable record is [`owner-decisions.json`](owner-decisions.json). This page is its readable copy.

## Provenance

- **Who wrote this:** an agent (Claude Code) transcribed these decisions on 2026-10-28, on the owner's instruction. The owner did not edit this file.
- **The questions:** from [`docs/rehearsal-006/R6-OWNER-DECISIONS-SCRIPT.md`](../../docs/rehearsal-006/R6-OWNER-DECISIONS-SCRIPT.md) and the owner's script "Owner Decisions, Acceptance Scope, and Preflight Resolution". Each question carried its options, the engineering recommendation and the consequence of each option.
- **The owner's answers:**
  - For Q1–Q6 and the R5A-001-01..05 dispositions: **"Do what you recommend to get it started"**.
  - For Q7, asked separately: **"Keep three cases (Recommended)"**.
- **When it takes effect (RDP-003):** only when the owner merges the pull request that adds this file. Until then every decision is `APPROVAL_PENDING_MERGE`.
- **What it does not do:** a decision changes scope. It never verifies an obligation.

## Already decided (context)

- The causa-data four-case requirement is waived. Rehearsal 6 uses `historical-v1` and writes only to isolated `workspaces/demo/<runId>/` paths.
- CI and deployment are kickoff work. They count against the seven-hour window.
- The stack is echelon-current 1.32.0.

## Decisions

| Decision | Question | Selected | Effect |
|---|---|---|---|
| DEC-R6-001 | DEC-025 against PF-015 and EXT-DATA-005 (R5A-001-08) | **B**, revise DEC-025 | No scope change. Rehearsal 6 must implement anonymous read-only access to public catalogs, authenticated writes, and real repository and branch permission checks. PF-015-O1, PF-015-O2 and EXT-DATA-005-O1 are verified as written. |
| DEC-R6-002 | Amber indicators (R5A-001-10, UI-016-O11) | **A**, 3:1 applies | No scope change. Amber indicators must reach 3:1 by measurement. #E9A23B stays the identity color. |
| DEC-R6-003 | Role and access control (R5A-001-11, UI-031-O5) | **A**, roles not applicable | UI-031-O5 is amended by approval `APR-R6-003`. Credential protection, authorization against the provider's permissions, and isolation still apply. |
| DEC-R6-004 | Rehearsal 3 verdict (R5A-001-11, AR-019-O3) | **A**, any authoritative non-accepting verdict | AR-019-O3 is amended by approval `APR-R6-004`. INVALID, FAIL and BLOCKED stay distinct in reports. |
| DEC-R6-005 | AR-027..AR-035 | **B**, gate on them | New baseline `BL-CAUSA-INDY-INIT-856157a78d47` and new plan `VP-13516e0d5930` (`requirements-compiled/v3`). AR-032 and AR-033 stay P1. |
| DEC-R6-006 | Screen-contract scope (AR-031) | S16 NOT-APPLICABLE; S01–S15 and S17–S25 IN SCOPE | 24 contracts in scope. S16 may be bound NOT-APPLICABLE only by citing DEC-R6-006. |
| DEC-R6-007 | Four investigations or three | **B**, three (`historical-v1`) | The waiver stands. Preflight stages 7–10 stay waived. |
| DEC-R6-008 | R5A-001-01..05 dispositions | As recommended | 01–03 are kickoff work. 04 and 05 are preflight work, now implemented. |

### DEC-R6-001: open point, left to the implementation and the reviewer

PF-015-O2 asks for a least-privilege, **read-only** test secret. The live write tests need write access to the test repository.

One reading resolves this:

- `CAUSA_GITHUB_TOKEN` stays read-only.
- The live write tests get a separate token, fine-grained to `contents:write` on the test repository only.

This record does not decide it.

### Amended acceptance criteria

The baseline text of both obligations stays verbatim in the plan. Each amendment sits beside it.

- **UI-031-O5:** "Never expose credentials. Application-level role management does not apply to Causa's approved single-user scope. Credential protection, authorization of every repository operation against the provider's actual permissions, workspace data isolation and secure handling of operational information still apply and must be verified."
- **AR-019-O3:** "Original Rehearsal 3 must yield an authoritative non-accepting verdict (INVALID, FAIL or BLOCKED), and the report must state the exact verdict and its diagnostic classification without merging INVALID, FAIL and BLOCKED into one outcome; Rehearsal 2 candidate 45644a7 should yield ACCEPTED only if its actual ledger and evidence meet the frozen contract."

### Screen contracts (DEC-R6-006)

S01 Investigation Start, S02 Investigation Workspace, S03 Hypothesis Battlefield, S04 Hypothesis Detail, S05 Evidence Capture, S06 Evidence Detail, S07 Evidence Dependency, S08 Unknown / Assumption / Contradiction, S09 Action / Experiment Designer, S10 Finding / Result Capture, S11 Cause Review, S12 Corrective Action, S13 Verification, S14 History / Time Travel, S15 Semantic Diff, S17 Multi-Agent Panel, S18 Research Tournament, S19 Replay / Spectator, S20 Factory View, S21 Requirements Proof, S22 Report Preview, S23 Persistence / Conflict Recovery, S24 Settings / Semantic Manifest and S25 Demo Health are **IN SCOPE**.

S16 Counterfactual Sandbox is **NOT-APPLICABLE**. The Causa engine has no counterfactual capability, and no Causa requirement beyond the generic contract asks for one.

Unless they are implemented and bound, S17, S18, S20 and S21 leave gate G5 unpassable, as in Rehearsal 5.

### R5A-001-01..05 (DEC-R6-008)

| Item | Disposition | Status |
|---|---|---|
| 01: network guard admits the deployed origin | kickoff (critical path, about 20 min, estimated) | not implemented |
| 02: causa-data pinned in every CI job | kickoff (about 10 min, estimated) | not implemented |
| 03: .NET SDK pinned, exact workload | kickoff (about 15 min, estimated) | not implemented |
| 04: unbound-after-review listing | preflight | implemented: `verify-plan --review-tag-changes` |
| 05: UI-015-O2 derived from its constituents | preflight | implemented: `derivedObligations` in the v3 curation |

UI-015-O2 has 11 clauses:

- **Seven are verified by their constituents:** EXT-DATA-013-O2, UI-004-O1/O5/O6, UI-006-O1/O2/O4, UI-010-O1 and UI-011-O1/O2/O3/O5.
- **Four are residual:** frozen fixtures, timeline ordering, report fidelity and startup failure. No P0 obligation states them, so UI-015-O2's own bound test still proves them.
