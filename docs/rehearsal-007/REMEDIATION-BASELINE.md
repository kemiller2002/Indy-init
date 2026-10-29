# Rehearsal 7 remediation baseline

**Status:** baseline for the Rehearsal 6 remediation (R7-REM-001..012). It is built from Rehearsal 6's **original evidence**, not from the summary in [`docs/REHEARSAL-006-RESULTS.md`](../REHEARSAL-006-RESULTS.md). It changes no Rehearsal 6 evidence.

**Machine-readable:** [`remediation-baseline.json`](remediation-baseline.json). It holds every requirement with every obligation, plus defects, review findings, tag changes, deviations, gates, defect classes, CI, evidence and governance failures, and the summary discrepancies. Every original identity is kept.

**How it was built.** An inventory agent read the final-acceptance artifact of run 40803128103 and the candidate CI run 40798638092 (job list, the logs of the browser (1), browser (2) and live jobs, and the browser-results artifacts). It also read the R6 repository at `main` 41fb6d6 and this repository's plan, baseline and kickoff.

**Spot checks by the coordinating agent, made against the artifact:**
- The plan verifier's verdict: FAILED, 58 / 19 / 26 / 12, obligations 178 / 89 / 68 / 20.
- The ledger: 70 / 19 / 26.
- The `CAUSA_PLAN` finding:
  - both browser shards log "BLOCKED: … set CAUSA_PLAN";
  - R6's `build-and-test.yml` sets `CAUSA_PLAN` only at lines 368 and 1167, not in the browser shards.

## Where each item is remediated

| Baseline item | Work package (see [REMEDIATION-PLAN.md](REMEDIATION-PLAN.md)) |
|---|---|
| R6-D-002, AR-035-O4 (no cross-tab exclusion without Web Locks) | R7-REM-001 |
| R6-D-003, UI-010-O2 offline half (unsendable save retried without backoff) | R7-REM-002 |
| RF-F-3 (network failure reported as a conflict) | R7-REM-002, R7-REM-011 |
| RF-F-8 and the unflagged UI-034-O1 loosening (weakened assertions) | R7-REM-011 |
| D-09: EXT-DATA-004-O2, EXT-DATA-005-O1, PF-015-O2 (write-capable read credential) | R7-REM-003 |
| CI starvation 22:22–23:54, per-commit group still at `build-and-test.yml:39` | R7-REM-004 |
| D-06, D-10, D-13; the 26 NOT_RUN and 12 UNVERIFIED (owner and review attestation) | R7-REM-005, R7-REM-006 |
| `CAUSA_PLAN` missing in the browser shards (UI-015, UI-021, UI-040, WDL-015); PF-013-O2 fixture drift; EXT-DATA-004-O1 and PF-015-O3 (fixtures tripping the credential scan) | R7-REM-006 |
| The first candidate was not deployable; deploy was decoupled from acceptance (D-08) | R7-REM-007 |
| G7: defect-class closures cite R5 commits; DCT-004-A missing; DCT-LIVE-A failed (D-11) | R7-REM-008 |
| UI-016-O11, PF-016-O1 (graph outline 1.95:1) | R7-REM-009 |
| VER-001-O1, VER-002-O2 (no pre-lock release comparison) | R7-REM-010 |
| Final-review findings RF-F-1..9 and the 7 tag changes; REVIEW-1 R1-F-3/4/5 | R7-REM-011 |
| UI-018-O2 copy mismatch, and the UI-004-O1, UI-016-O13 and UI-006-O1/O4 failures (root cause not found) | R7-REM-011 (carried into the Rehearsal 7 plan as known failure modes) |
| MSB3030 build race | R7-REM-004 (assessment in [CI-PERFORMANCE.md](CI-PERFORMANCE.md)) |

Rehearsal 7 builds the application from zero source code ([HACKATHON-BOUNDARIES](../HACKATHON-BOUNDARIES.md)). An application defect therefore stays corrected only where its behaviour is owned by a reusable component (Arca, Forma, Conditor, Praxis) or by a frozen requirement and test that Rehearsal 7 must pass. The plan follows that rule.

**Update, 2026-10-29.** For persistence, the owner chose the second route: ADR-R7-001 ([PERSISTENCE-ARCHITECTURE-DECISION.md](PERSISTENCE-ARCHITECTURE-DECISION.md)) removes the shared browser store and the offline queue that R6-D-002 and R6-D-003 lived in, and binds Rehearsal 7 to PAT-01..15. The Rehearsal 6 findings recorded below are unchanged.


Built from original evidence, not from the summary. Primary sources:
- Final-acceptance artifact `acceptance-final-46d5e1129a798549dcf37c2a6460c4f623298761` of run 40803128103 (ledger.json, traceability.json, plan-assessment.json, verdict.json, producers, gate records, acceptance-run TRX)
- Candidate CI run 40798638092: job list, and logs of the browser (1), browser (2) and live jobs; browser-results-1/2 artifacts
- `/home/user/indy-init-rehearsal-006` at main 41fb6d6: docs/r6/DEVIATIONS.md, WORK-PLAN.md, reviews/*.json, acceptance/*, evidence/progress/progress.json, tests/
- `/home/user/indy-init`: requirements-compiled/v3/verification-plan.json, requirements/causa-requirements-baseline-r6.json (sha256 verified 40599abe…befd), kickoff/rehearsal-006/*

## Counts (all verified against the artifact)

| Measure | PASS | FAIL | NOT_RUN | UNVERIFIED |
|---|---|---|---|---|
| Requirements, ledger (115) | 70 | 19 | 26 | - |
| Requirements, plan verifier (115) | 58 | 19 | 26 | 12 |
| Obligations, ledger traceability (355; 283 bound, 72 producer-only) | 259 | 24 | 72 | - |
| Obligations, plan verifier (355) | 178 | 89 | 68 | 20 |

Plan-verifier obligation FAIL (89) decomposes as: 24 whose bound test failed + 64 whose bound tests passed but whose requirement evidence is FAIL (evidence-invalid propagation) + 1 producer-only (AR-035-O5, ledger NOT_RUN).

Category assignment (NOT_RUN/UNVERIFIED in either verifier): requirements {"independent-review-requirement": 28, "missing-evidence": 6, "missing-test": 3, "owner-authorization": 1}; obligations {"independent-review-requirement": 72, "missing-evidence": 8, "owner-authorization": 4, "missing-test": 3, "requirement-interpretation": 2}. No item was categorized as missing-implementation, missing-external-dependency or verification-tool-limitation, because every NOT_RUN/UNVERIFIED item is blocked on process evidence, a tag change or an interpretation; product and environment problems show up as FAIL, listed separately.

## Requirements not PASS in both verifiers

| Requirement | Ledger | Plan | Category | Non-PASS obligations (ledger/plan; reason or category) |
|---|---|---|---|---|
| AR-001 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-001-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-001-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-002 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-002-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-002-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-003 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-003-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-003-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-004 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-004-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-004-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-005 | NOT_RUN | NOT_RUN | missing-evidence | AR-005-O1 NOT_RUN/NOT_RUN: missing-evidence; AR-005-O2 NOT_RUN/NOT_RUN: missing-evidence |
| AR-006 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-006-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-006-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-007 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-007-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-007-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-008 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-008-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-008-O2 NOT_RUN/NOT_RUN: owner-authorization |
| AR-009 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-009-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-009-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-010 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-010-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-010-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-011 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-011-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-011-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-012 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-012-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-012-O2 NOT_RUN/NOT_RUN: owner-authorization |
| AR-013 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-013-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-013-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-014 | PASS | UNVERIFIED | missing-test | AR-014-O1 PASS/UNVERIFIED: missing-test; AR-014-O2 PASS/UNVERIFIED: independent-review-requirement |
| AR-015 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-015-O1 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-017 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-017-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-017-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-018 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-018-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-018-O2 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-019 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-019-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-019-O2 NOT_RUN/NOT_RUN: independent-review-requirement; AR-019-O3 NOT_RUN/NOT_RUN: requirement-interpretation; AR-019-O4 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-020 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-020-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-020-O2 NOT_RUN/NOT_RUN: independent-review-requirement; AR-020-O3 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-021 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-021-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-021-O2 NOT_RUN/NOT_RUN: independent-review-requirement; AR-021-O3 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-022 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-022-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-022-O2 NOT_RUN/NOT_RUN: independent-review-requirement; AR-022-O3 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-023 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-023-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-023-O2 NOT_RUN/NOT_RUN: independent-review-requirement; AR-023-O3 NOT_RUN/NOT_RUN: independent-review-requirement; AR-023-O4 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-024 | NOT_RUN | NOT_RUN | owner-authorization | AR-024-O1 NOT_RUN/NOT_RUN: owner-authorization; AR-024-O2 NOT_RUN/NOT_RUN: owner-authorization |
| AR-027 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-027-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-027-O2 NOT_RUN/NOT_RUN: independent-review-requirement; AR-027-O3 NOT_RUN/NOT_RUN: independent-review-requirement; AR-027-O4 NOT_RUN/NOT_RUN: independent-review-requirement; AR-027-O5 NOT_RUN/NOT_RUN: independent-review-requirement; AR-027-O6 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-028 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-028-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-028-O2 NOT_RUN/NOT_RUN: independent-review-requirement; AR-028-O3 NOT_RUN/NOT_RUN: independent-review-requirement; AR-028-O4 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-029 | PASS | UNVERIFIED | independent-review-requirement | AR-029-O1 PASS/UNVERIFIED: independent-review-requirement; AR-029-O2 NOT_RUN/UNVERIFIED: independent-review-requirement; AR-029-O3 NOT_RUN/UNVERIFIED: independent-review-requirement |
| AR-030 | PASS | UNVERIFIED | independent-review-requirement | AR-030-O1 PASS/UNVERIFIED: independent-review-requirement; AR-030-O2 PASS/UNVERIFIED: independent-review-requirement; AR-030-O3 NOT_RUN/UNVERIFIED: independent-review-requirement |
| AR-031 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-031-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-031-O2 NOT_RUN/NOT_RUN: independent-review-requirement; AR-031-O3 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-034 | NOT_RUN | NOT_RUN | independent-review-requirement | AR-034-O1 NOT_RUN/NOT_RUN: independent-review-requirement; AR-034-O2 NOT_RUN/NOT_RUN: independent-review-requirement; AR-034-O3 NOT_RUN/NOT_RUN: independent-review-requirement; AR-034-O4 NOT_RUN/NOT_RUN: independent-review-requirement; AR-034-O5 NOT_RUN/NOT_RUN: independent-review-requirement |
| AR-035 | FAIL | FAIL | - | AR-035-O4 FAIL/FAIL: product defect R6-D-002: no-Web-Locks half loses tabs 4 and 5 (CI) / 3 and 5 (review); AR-035-O5 NOT_RUN/FAIL: requirement-interpretation |
| EXT-DATA-004 | FAIL | FAIL | - | EXT-DATA-004-O1 FAIL/FAIL: credential scan flags 6 events.ndjson/snapshot.json PF-007 fixtures under tests/Causa.Acceptance.Live/fixtures/pf-007 (commit 4b0d434) as storage-contract record files (Repo.fs:58 scan); EXT-DATA-004-O2 FAIL/FAIL: CAUSA_GITHUB_TOKEN is write-capable (D-09): "GitHub accepted a write with the read-only credential" |
| EXT-DATA-005 | FAIL | FAIL | - | EXT-DATA-005-O1 FAIL/FAIL: Expected Not WriteGranted, Actual WriteGranted (read token write-capable, D-09) |
| EXT-DATA-009 | PASS | UNVERIFIED | independent-review-requirement | EXT-DATA-009-O1 PASS/UNVERIFIED: independent-review-requirement; EXT-DATA-009-O2 PASS/UNVERIFIED: independent-review-requirement |
| PF-011 | FAIL | FAIL | - | PF-011-O2 FAIL/FAIL: derivative: asserts all UI results pass; UI-016-O11 unexpected |
| PF-013 | FAIL | FAIL | - | PF-013-O2 FAIL/FAIL: recorded-fixture drift: "repository.read.authorized diverged: live answer lacks allow_auto_merge" |
| PF-015 | FAIL | FAIL | - | PF-015-O2 FAIL/FAIL: "CAUSA_GITHUB_TOKEN can write: it is not read-only" (D-09); PF-015-O3 FAIL/FAIL: same fixture-scan collision as EXT-DATA-004-O1 (storage-contract record files under tests/) |
| PF-016 | FAIL | FAIL | - | PF-016-O1 FAIL/FAIL: graph stroke amber #e9a23b on ivory 1.95:1 (needs Forma outline token) |
| PF-019 | PASS | UNVERIFIED | missing-evidence | PF-019-O1 PASS/UNVERIFIED: missing-evidence |
| PF-020 | PASS | UNVERIFIED | independent-review-requirement | PF-020-O1 PASS/UNVERIFIED: independent-review-requirement; PF-020-O2 PASS/UNVERIFIED: independent-review-requirement |
| UI-004 | FAIL | FAIL | - | UI-004-O1 FAIL/FAIL: test hook: "The injected state was not rendered in time" (root cause not determined) |
| UI-006 | FAIL | FAIL | - | UI-006-O1 FAIL/FAIL: live: expected "Waiting for the connection", got "may or may not have reached GitHub" (R6-D-003 family); UI-006-O4 FAIL/FAIL: live: "My change on the older version." not visible after conflict (root cause not determined; RF-F-3 area) |
| UI-010 | FAIL | FAIL | - | UI-010-O2 FAIL/FAIL: 403 "forbidden" case shows "Saving to GitHub… not confirmed yet." (R6-D-003 family) |
| UI-015 | FAIL | FAIL | - | UI-015-O1 FAIL/FAIL: BLOCKED: verification plan not available in CI browser job (CAUSA_PLAN unset; _plan.js) |
| UI-016 | FAIL | FAIL | - | UI-016-O11 FAIL/FAIL: amber outline 1.95:1 < 3:1 (Forma has no outline token); UI-016-O13 FAIL/FAIL: test hook: "The injected state was not rendered in time" (root cause not determined) |
| UI-018 | FAIL | FAIL | - | UI-018-O2 FAIL/FAIL: copy/test mismatch: row reads "connected", test expects /Connected/ |
| UI-021 | FAIL | FAIL | - | UI-021-O3 FAIL/FAIL: BLOCKED: verification plan not available in CI browser job |
| UI-030 | PASS | UNVERIFIED | missing-evidence | UI-030-O3 PASS/UNVERIFIED: missing-evidence |
| UI-031 | FAIL | FAIL | - | UI-031-O5 FAIL/FAIL: administration screen element not found on first assertion; verified as written (DEC-R6-011, no role model) |
| UI-034 | PASS | UNVERIFIED | missing-test | UI-034-O2 PASS/UNVERIFIED: missing-test |
| UI-040 | FAIL | FAIL | - | UI-040-O2 FAIL/FAIL: BLOCKED: verification plan not available in CI browser job |
| VER-001 | FAIL | FAIL | - | VER-001-O1 FAIL/FAIL: no pre-lock newest-release comparison was recorded |
| VER-002 | FAIL | FAIL | - | VER-002-O2 FAIL/FAIL: no pre-lock newest-release comparison was recorded |
| VER-003 | PASS | UNVERIFIED | missing-evidence | VER-003-O1 PASS/UNVERIFIED: missing-evidence; VER-003-O2 PASS/UNVERIFIED: missing-evidence |
| VER-004 | PASS | UNVERIFIED | missing-evidence | VER-004-O3 PASS/UNVERIFIED: missing-evidence |
| VER-010 | PASS | UNVERIFIED | missing-evidence | VER-010-O1 PASS/UNVERIFIED: missing-evidence |
| WDL-001 | PASS | UNVERIFIED | missing-test | WDL-001-O1 PASS/UNVERIFIED: missing-test |
| WDL-015 | FAIL | FAIL | - | WDL-015-O1 FAIL/FAIL: BLOCKED: verification plan not available in CI browser job; WDL-015-O2 FAIL/FAIL: BLOCKED: verification plan not available in CI browser job |

Obligations that are FAIL in the plan verifier only because a sibling failed are omitted from the table above; they are in baseline.json with that reason.

## Category justifications (obligation level)

| Obligation | Ledger/Plan | Category | Justification |
|---|---|---|---|
| AR-001-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-001-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-002-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-002-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-003-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-003-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-004-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-004-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-005-O1 | NOT_RUN/NOT_RUN | missing-evidence | No praxis dependency-decision record in events.jsonl (only work.* events; D-05 record transcribed). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)]; gate-records/final-acceptance-records/events.jsonl |
| AR-005-O2 | NOT_RUN/NOT_RUN | missing-evidence | No praxis dependency-decision record in events.jsonl (only work.* events; D-05 record transcribed). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)]; gate-records/final-acceptance-records/events.jsonl |
| AR-006-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-006-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-007-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-007-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-008-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-008-O2 | NOT_RUN/NOT_RUN | owner-authorization | Needs owner decision DEC-R6-KICKOFF via approving GitHub review; owner ratified by merge only (D-06); attestations.json empty. evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-009-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-009-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-010-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-010-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-011-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-011-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-012-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-012-O2 | NOT_RUN/NOT_RUN | owner-authorization | Needs operator-approved scope-change record; approvals.causa.json has none. evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-013-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-013-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-014-O1 | PASS/UNVERIFIED | missing-test | Bound test unbound by final-review tag change (partial proof); evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] unboundAfterReview; acceptance/reviews/final/review-tag-changes.json |
| AR-014-O2 | PASS/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-015-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-017-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-017-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-018-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-018-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-019-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-019-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-019-O3 | NOT_RUN/NOT_RUN | requirement-interpretation | Verified as written: DEC-R6-004 amendment not applied (DEC-R6-011, D-02); expected not to verify. evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)]; docs/r6/DEVIATIONS.md D-02 |
| AR-019-O4 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-020-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-020-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-020-O3 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-021-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-021-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-021-O3 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-022-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-022-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-022-O3 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-023-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-023-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-023-O3 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-023-O4 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-024-O1 | NOT_RUN/NOT_RUN | owner-authorization | Needs owner decision DEC-R6-KICKOFF via approving GitHub review; owner ratified by merge only (D-06); attestations.json empty. evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-024-O2 | NOT_RUN/NOT_RUN | owner-authorization | Needs owner decision DEC-R6-KICKOFF via approving GitHub review; owner ratified by merge only (D-06); attestations.json empty. evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-027-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-027-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-027-O3 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-027-O4 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-027-O5 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-027-O6 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-028-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-028-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-028-O3 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-028-O4 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-029-O1 | PASS/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-029-O2 | NOT_RUN/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-029-O3 | NOT_RUN/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-030-O1 | PASS/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-030-O2 | PASS/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-030-O3 | NOT_RUN/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-031-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-031-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-031-O3 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-034-O1 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-034-O2 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-034-O3 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-034-O4 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-034-O5 | NOT_RUN/NOT_RUN | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| AR-035-O5 | NOT_RUN/FAIL | requirement-interpretation | Obligation text is a historical Rehearsal 5 statement bound only to praxis:independent-review; ledger NOT_RUN, plan FAIL via sibling AR-035-O4. evidence-out/bundle/traceability.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| EXT-DATA-009-O1 | PASS/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| EXT-DATA-009-O2 | PASS/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| PF-019-O1 | PASS/UNVERIFIED | missing-evidence | Required process record praxis:test-result not verified (no-process-evidence). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| PF-020-O1 | PASS/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| PF-020-O2 | PASS/UNVERIFIED | independent-review-requirement | Independent review exists only as unattested record (D-10/D-13); no approving GitHub review (attestations.json []). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| UI-030-O3 | PASS/UNVERIFIED | missing-evidence | Required process record praxis:acceptance-reference,praxis:release-selection not verified (no-process-evidence). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| UI-034-O2 | PASS/UNVERIFIED | missing-test | Bound test unbound by final-review tag change (partial proof); evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] unboundAfterReview; acceptance/reviews/final/review-tag-changes.json |
| VER-003-O1 | PASS/UNVERIFIED | missing-evidence | No praxis dependency-decision record in events.jsonl (only work.* events; D-05 record transcribed). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)]; gate-records/final-acceptance-records/events.jsonl |
| VER-003-O2 | PASS/UNVERIFIED | missing-evidence | No praxis dependency-decision record in events.jsonl (only work.* events; D-05 record transcribed). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)]; gate-records/final-acceptance-records/events.jsonl |
| VER-004-O3 | PASS/UNVERIFIED | missing-evidence | No praxis dependency-decision record in events.jsonl (only work.* events; D-05 record transcribed). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)]; gate-records/final-acceptance-records/events.jsonl |
| VER-010-O1 | PASS/UNVERIFIED | missing-evidence | Required process record praxis:acceptance-reference not verified (no-process-evidence). evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| WDL-001-O1 | PASS/UNVERIFIED | missing-test | Bound test unbound by final-review tag change (partial proof); evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] unboundAfterReview; acceptance/reviews/final/review-tag-changes.json |

## Ledger FAIL obligations (24) with root cause from CI logs

| Obligation | Root cause | Evidence |
|---|---|---|
| AR-035-O4 | product defect R6-D-002: no-Web-Locks half loses tabs 4 and 5 (CI) / 3 and 5 (review) | browser-results-1.json (run 40798638092 job browser (1)); final-review.json RF-F-1 |
| EXT-DATA-004-O1 | credential scan flags 6 events.ndjson/snapshot.json PF-007 fixtures under tests/Causa.Acceptance.Live/fixtures/pf-007 (commit 4b0d434) as storage-contract record files (Repo.fs:58 scan) | live job 123210630378 log; tests/Causa.Acceptance.Live/Repo.fs:52-60 |
| EXT-DATA-004-O2 | CAUSA_GITHUB_TOKEN is write-capable (D-09): "GitHub accepted a write with the read-only credential" | live job 123210630378 log; DEVIATIONS.md D-09 |
| EXT-DATA-005-O1 | Expected Not WriteGranted, Actual WriteGranted (read token write-capable, D-09) | live job 123210630378 log |
| PF-011-O2 | derivative: asserts all UI results pass; UI-016-O11 unexpected | live job 123210630378 log (PfTests.fs:721) |
| PF-013-O2 | recorded-fixture drift: "repository.read.authorized diverged: live answer lacks allow_auto_merge" | live job 123210630378 log (PfTests.fs:913) |
| PF-015-O2 | "CAUSA_GITHUB_TOKEN can write: it is not read-only" (D-09) | live job 123210630378 log |
| PF-015-O3 | same fixture-scan collision as EXT-DATA-004-O1 (storage-contract record files under tests/) | live job 123210630378 log (PfTests.fs:1071) |
| PF-016-O1 | graph stroke amber #e9a23b on ivory 1.95:1 (needs Forma outline token) | live job 123210630378 log |
| UI-004-O1 | test hook: "The injected state was not rendered in time" (root cause not determined) | browser-results-1.json |
| UI-006-O1 | live: expected "Waiting for the connection", got "may or may not have reached GitHub" (R6-D-003 family) | live job 123210630378 log (UI-006.spec.js:137) |
| UI-006-O4 | live: "My change on the older version." not visible after conflict (root cause not determined; RF-F-3 area) | live job 123210630378 log (UI-006.spec.js:240) |
| UI-010-O2 | 403 "forbidden" case shows "Saving to GitHub… not confirmed yet." (R6-D-003 family) | browser-results-1.json |
| UI-015-O1 | BLOCKED: verification plan not available in CI browser job (CAUSA_PLAN unset; _plan.js) | browser-results-1.json; .github/workflows/build-and-test.yml browser job |
| UI-016-O11 | amber outline 1.95:1 < 3:1 (Forma has no outline token) | browser-results-1.json |
| UI-016-O13 | test hook: "The injected state was not rendered in time" (root cause not determined) | browser-results-1.json |
| UI-018-O2 | copy/test mismatch: row reads "connected", test expects /Connected/ | browser-results-1.json (UI-018.spec.js:60) |
| UI-021-O3 | BLOCKED: verification plan not available in CI browser job | browser-results-1.json |
| UI-031-O5 | administration screen element not found on first assertion; verified as written (DEC-R6-011, no role model) | browser-results-2.json |
| UI-040-O2 | BLOCKED: verification plan not available in CI browser job | browser-results-2.json |
| VER-001-O1 | no pre-lock newest-release comparison was recorded | acceptance-deterministic job; progress.json knownHonestFailures |
| VER-002-O2 | no pre-lock newest-release comparison was recorded | acceptance-deterministic job; progress.json knownHonestFailures |
| WDL-015-O1 | BLOCKED: verification plan not available in CI browser job | browser-results-2.json |
| WDL-015-O2 | BLOCKED: verification plan not available in CI browser job | browser-results-2.json |

## Gates (ledger, run 40798638092-1)

| Gate | Title | Status | Reason |
|---|---|---|---|
| G0 | Scope | PASS | every producer succeeded in run 40798638092-1: preservation-gate |
| G1 | Dependencies | PASS | every producer succeeded in run 40798638092-1: lock-gate, wasm-smoke, stack-smoke |
| G2 | Interfaces | PASS | every producer succeeded in run 40798638092-1: interface-manifest |
| G3 | Domain | PASS | every producer succeeded in run 40798638092-1: core, domain-obligations |
| G4 | Frozen data | PASS | every producer succeeded in run 40798638092-1: frozen-data |
| G5 | UI | FAIL | producer(s) failed in this run: browser |
| G6 | Persistence | FAIL | producer(s) failed in this run: live |
| G7 | Defect class | FAIL | producer(s) failed in this run: defect-class-review |
| G8 | Candidate | FAIL | producer(s) failed in this run: browser, live, traceability |
| G9 | Deployment | FAIL | producer(s) failed in this run: browser, live, traceability |

## Defect classes (G7)

| Class | Status | Tests | Reason |
|---|---|---|---|
| DC-001 | FAIL | DCT-001-A, DCT-001-B | the recorded fix 5336ec40372323dbdbbb821878ad87c51597af7a is not in the candidate's history |
| DC-002 | FAIL | DCT-002-A, DCT-002-B | the recorded fix e8e32c1ebf6db77344cb8e8bc5bd30b422cdd379 is not in the candidate's history |
| DC-003 | FAIL | DCT-003-A | the recorded fix b07d2a5def150e1cd741f0540667eac31b78330e is not in the candidate's history |
| DC-004 | FAIL | DCT-004-A | the recorded fix 895fb6da3031da689cd3a6f54b4005f17f07edbe is not in the candidate's history |
| DC-005 | FAIL | DCT-005-A | the recorded fix 895fb6da3031da689cd3a6f54b4005f17f07edbe is not in the candidate's history |
| DC-006 | FAIL | DCT-006-A | the recorded fix 578ebd3b0bbc99862dc2da945e9c3b9e3f719bba is not in the candidate's history |
| R3-LESSON-3 | FAIL | DCT-INPUT-A | the recorded fix 578ebd3b0bbc99862dc2da945e9c3b9e3f719bba is not in the candidate's history |
| DC-001..DC-005 | FAIL | DCT-LIVE-A | the recorded fix b07d2a5def150e1cd741f0540667eac31b78330e is not in the candidate's history |

In `producers/defect-class-review.json` the failing-first tests pass except DCT-004-A (NOT_RUN: it does not exist) and DCT-LIVE-A (FAIL: it timed out waiting for the conflict Reload button).

## Defects

| Id | Severity | State | Evidence |
|---|---|---|---|
| R6-D-001 | critical | fixed (re-verified with Web Locks) | docs/r6/reviews/final-review.json defects; evidence/progress/progress.json; commits f91d2c5, a72bc6a, 23ac222 |
| R6-D-002 | critical | open | final-review.json RF-F-1/defects; AR-035-O4 failed in run 40798638092 (tabs 4,5 lost); progress.json |
| R6-D-003 | critical | open | final-review.json RF-F-2 (151 POSTs/60 s); progress.json (~5,000 POSTs/tab in 4-5 min); WI-R6-C2 abandoned |
| R1-F-1 | critical | closed | review-1.json; final-review.json closure (23ac222 per progress.json) |
| RF-F-3 | major | open | final-review.json RF-F-3 |
| RF-F-8 | minor | open (weakened test, tag change) | final-review.json RF-F-8; review-tag-changes.json UI-034-O2 |
| BUILD-RACE-MSB3030 | unrated in evidence (summary says high) | fixed (8a13e6a, daad1e5) | progress.json ciNotes; commit 8a13e6a |
| R1-F-6 (AR-030 fail-open test hooks) | minor (REVIEW-1); summary says high | closed | review-1.json R1-F-6; final-review.json closure; 323e436, daad1e5 |

## Final independent review (46d5e11): BLOCKING_FINDINGS, 9 findings

The tally is whole 35, partial 7, not-a-real-assertion 0 (42 spot checks). Source: `docs/r6/reviews/final-review.json`. Every finding is open at the candidate.

| Id | Severity | Summary | Obligations |
|---|---|---|---|
| RF-F-1 | critical | R6-D-002 open: without Web Locks the shared device store loses tabs' changes | AR-035-O4 |
| RF-F-2 | critical | R6-D-003 open: a save that cannot be sent is retried in a tight loop and reported as 'Saving to GitHub' | UI-006-O1, UI-010-O2, AR-035-O4 |
| RF-F-3 | major | A network failure is reported as another writer's conflict, and 'Discard my local change' is offered | UI-006-O1, UI-006-O4, UI-010-O2, EXT-DATA-007-O1 |
| RF-F-4 | major | The first cut (02c37d1) failed CI core on a stale unit test and was never deployed; C2 fixes that test; C2 CI pending, run 40798638092; the core step still hides its own output | AR-029-O1, PF-016-O2, UI-016-O15, VER-001-O1, VER-002-O2 |
| RF-F-5 | major | Carried open: amber indicators 1.95:1 (R1-F-5) and the write-capable read token (R1-F-4, D-09) | UI-016-O11, PF-016-O1, PF-015-O2, EXT-DATA-004-O2, EXT-DATA-005-O1, AR-014-O1 |
| RF-F-6 | major | G7 cannot close for this candidate: defect classes cite R5 fix SHAs absent from R6 history, and DCT-004-A no longer exists | AR-034-O1, AR-034-O2 |
| RF-F-7 | minor | Header claims 'the workspace has no investigation yet' while it holds investigations | EXT-DATA-007-O1, UI-006-O1 |
| RF-F-8 | minor | A test was weakened: UI-034-O2 no longer checks the selection while the inspector is open | UI-034-O2 |
| RF-F-9 | minor | UI-010-O2's judge accepts a false description of what happened | UI-010-O2 |

### The 7 final-review tag changes (`acceptance/reviews/final/review-tag-changes.json`; all have resolution null and appear in plan-assessment unboundAfterReview as "open")

| Obligation | Test | Review basis | Ledger / Plan status of obligation |
|---|---|---|---|
| UI-031-O5 | `playwright:acceptance/UI-031.spec.js > UI-031-O5 [chromium]` | FINAL-REVIEW spot-check UI-031-O5 (partial) | FAIL / FAIL |
| UI-006-O1 | `playwright:live/UI-006.spec.js > UI-006-O1 [chromium]` | FINAL-REVIEW spot-check UI-006-O1 (partial) | FAIL / FAIL |
| UI-010-O2 | `playwright:acceptance/UI-010.spec.js > UI-010-O2 [chromium]` | FINAL-REVIEW RF-F-9 (partial) | FAIL / FAIL |
| WDL-001-O1 | `playwright:acceptance/WDL-001.spec.js > WDL-001-O1 [chromium]` | FINAL-REVIEW spot-check WDL-001-O1 (partial) | PASS / UNVERIFIED |
| UI-016-O15 | `playwright:acceptance/UI-016.spec.js > UI-016-O15 [chromium]` | FINAL-REVIEW RF-F-4 (partial) | PASS / FAIL |
| UI-034-O2 | `playwright:acceptance/UI-034.spec.js > UI-034-O2 [chromium]` | FINAL-REVIEW RF-F-8 (partial) | PASS / UNVERIFIED |
| AR-014-O1 | `dotnet:Causa.Acceptance.Deterministic.AR014Tests.AR-014-O1` | FINAL-REVIEW RF-F-5 (partial) | PASS / UNVERIFIED |

## REVIEW-1 (f91d2c5): BLOCKING_FINDINGS, 10 findings (1 critical, 4 major, 5 minor)

The tally is 22 proves whole, 17 partial, 1 not a real assertion. Its 18 tag changes are in `docs/r6/reviews/review-1-tag-changes.json`, and the final acceptance does not read them. Closure states come from the final review: 7 closed, 1 partial, 2 open.

| Id | Severity | Summary | Closure at final review |
|---|---|---|---|
| R1-F-1 | critical | An investigation's page shows another investigation's save state: 'Saved to GitHub' is claimed for a change that never reached GitHub | closed: Adapter.syncFor (src/app/Adapter.fs:391-394) is used by View.fs:280 and AnalysisView.fs:158; no other ws.Sync read. My R1-F-1 reproduction against C's productio |
| R1-F-2 | major | A single writer in one tab gets a spurious conflict: after saving investigation B, a change to investigation A is not saved | closed: a72bc6a: r2.mjs (create A, create B, change A, no fault) saves A's change to GitHub, showing A's own confirmation. A new false-conflict path appears under a net |
| R1-F-3 | major | The candidate was deployed although CI's bound acceptance suites failed: deploy is not gated on acceptance-deterministic (AR-030 proof) or live | partial: Deploy is gated by build-site.mjs's own AR-030 scan (scripts/build-site.mjs:156-168, part of the site job) but still not by the bound acceptance suites (build-a |
| R1-F-4 | major | The 'read-only' secret CAUSA_GITHUB_TOKEN can write: least privilege is not met | open: D-09: owner kept the write-capable CAUSA_GITHUB_TOKEN. PF-015-O2, EXT-DATA-004-O2, EXT-DATA-005-O1 fail honestly. |
| R1-F-5 | major | Amber state indicators measure 1.95:1 on ivory, below the 3:1 DEC-R6-002 requires | open: Amber 1.95:1 unchanged; PF-016-O1 and UI-016-O11 remain failing (progress.json knownHonestFailures). |
| R1-F-6 | minor | Test hooks are the default .NET configuration (fail-open) | closed: 323e436/daad1e5: every fsproj/csproj condition is CausaTestBuild == 'true' (Causa.App.fsproj:10,17,36,44; Causa.Persistence.fsproj:11,22,25,35,36; Causa.Wasm.cs |
| R1-F-7 | minor | AR-030's marker scan cannot see the state-injection modules it is meant to prove absent | closed: AR030Tests.fs derivedMarkers() derives every module and top-level binding of the conditioned Compile items and asserts TestState, UxFixture and RecordedGitHub a |
| R1-F-8 | minor | EXT-DATA-007-O1's test adds the 'conflict' state to the set of reached states itself | closed: ExtDataTests.fs EXT-DATA-007-O1 reaches every label from program state (w.ConflictModel for conflict) and asserts eight distinct texts from the application's ow |
| R1-F-9 | minor | The write-path validator the tests assert on is never called by the product | closed: 330bcf5: Sync.writeFiles (src/persistence/Sync.fs:276-280) applies Target.pathAllowed before a change reaches the book and again at send (Sync.fs:564); used at  |
| R1-F-10 | minor | UI-006 live specs: harness defects and no accessibility assertion despite the required category | closed: UI-006.spec.js: holdWrites never unroutes while pending; every test ends with the accessibility audit; O6 checks the empty token on the Connect form. Not run by |

## Deviations (docs/r6/DEVIATIONS.md)

| Id | When | Summary |
|---|---|---|
| D-01 | 2026-10-28 (preflight) | The independent scope review is not attested by the owner |
| D-02 | 2026-10-28 (preflight) | The UI-031-O5 and AR-019-O3 amendments are not applied, so both are verified as written. AR-019-O3 is expected not to verify |
| D-03 | 20:06 | `conditor init` ran with `GH_TOKEN` and `GITHUB_TOKEN` unset, because Conditor sends the session credential on public-repository fetches and the proxy refuses it. The materialized files come from the pinned Percepta commit |
| D-04 | 20:45 | 775 equivalence reviews for baseline 856157a were transcribed by an agent, naming the owner as reviewer (`requirements/preservation/equivalence-reviews.json`, commit 7ace2ce) |
| D-05 | 20:14 | `@echelon-foundry/forma-workflow` 1.1.0, which is outside echelon-current 1.32.0, is pinned by an agent-transcribed decision record (DF-CAUSA-2026-0601) |
| D-06 | 21:30 | The owner ratified DEC-R6-KICKOFF (write scope, Percepta release, DF-CAUSA-2026-0003, frozen requirements and stack) by merging #2 and #3, two pull requests opened by `github-actions[bot]`. The owner posted no approving review on either. The frozen acceptance tooling (`review-attestations`, `verify- |
| D-07 | 21:40 | Arca 0.6.0 is not adopted for the cross-tab local read-merge-write. AR-035-O4 is met instead by a kernel Web Locks exclusive section, with an explicit fallback. Arca.Limen 0.6.0 also needs Limen.Store and Aegis.Integration.GitHub, an integration too large for the remaining clock |
| D-08 | 21:05 (recorded 22:40) | Deploy and deployed-smoke depend only on the jobs that build and check the deployable artifact: core build and unit tests, the site build with `limen wasm-check` and the AR-030 production-artifact check, and start-smoke. They do not wait for the bound acceptance suites. A candidate can therefore be  |
| D-09 | 22:45 | `CAUSA_GITHUB_TOKEN` stays write-capable: it is not reissued as read-only |
| D-10 | 22:50 | REVIEW-1, the independent review of f91d2c5 (#4), is attested by the owner in chat ("Approve"), not by an approving GitHub review, and #4 is merged by the coordinator under the owner's approval of merges. The acceptance job reads reviewer attestations only from approving GitHub reviews (ESO-R6-05),  |
| D-11 | 00:55 | Defect classes (G7) were not re-reviewed before the release candidate. `acceptance/defect-classes.json` in candidates 02c37d1 and 46d5e11 still cites Rehearsal 5 fix commits and reviewer evidence that are not in Rehearsal 6's history, and the file is part of the candidate, so G7 cannot close for thi |
| D-12 | 00:15 (method) | The candidate's process records (release selection, final review, final disposition) are read from a later records-only commit on `main` (only `.ros/`, `docs/r6/`, `evidence/`, `acceptance/reviews/final/` may differ from the candidate), through the frozen verifier's own `--process-events/--process-e |
| D-13 | 01:05 | The final independent review of 02c37d1 and the owner's final disposition are attested by the owner in chat, not by approving GitHub reviews. No attestation pull requests are opened. As with D-10, `verify-process` sees no attestation, so the independent-review, final-disposition and owner-decision o |

## CI defects

| Id | Summary | Evidence |
|---|---|---|
| CI-01 | Browser job never provides the frozen verification plan (no Indy-init checkout, CAUSA_PLAN unset); _plan.js falls back to a sandbox-only path /home/user/indy-init. 5 bound tests fail as BLOCKED: UI-015-O1, UI-021-O3, UI-040-O2, WDL-015-O1, WDL-015-O2, making UI-015, UI-021, UI-040, WDL-015 FAIL | browser-results-1/2.json (run 40798638092 artifacts 12866835293, 12866491324); .github/workflows/build-and-test.yml lines 419-581; tests/browser/acceptance/_plan.js |
| CI-02 | Core test step hides its own output (output=$(dotnet test ...) under bash -e) | final-review.json RF-F-4 |
| CI-03 | First candidate 02c37d1 core job failed on a stale copy expectation (ProjectionTests.fs:99); never deployed; re-cut as 46d5e11 | final-review.json RF-F-4; CI run 40796631060 |
| CI-04 | CI starvation 22:22-23:54: per-SHA concurrency + job-level always(); fixed by WI-R6-A4 (!cancelled()) and WI-R6-A5 force-cancel | progress.json ciNotes |
| CI-05 | Intermittent Release build race MSB3030 (shared obj/Release); fixed 8a13e6a, daad1e5 | progress.json ciNotes |
| CI-06 | Deploy and deployed-smoke not gated on bound acceptance suites (D-08; R1-F-3 partial) | DEVIATIONS.md D-08; final-review.json closure R1-F-3 |
| CI-07 | Evidence test AR-004 still expects the pre-WI-R6-A4 workflow text "if: always() && needs.reuse.result != ..."; fails against the !cancelled() workflow | TestResults/acceptance-run/*.trx (run 40803128103) |
| CI-08 | Unbound identity.spec.js test fails: .causa-attention border is rgb(158,100,16), test expects #E9A23B (233,162,59) | browser-results-2.json / browser (2) job log |
| CI-09 | "Injected state was not rendered in time" test-hook timeouts in UI-004-O1 and UI-016-O13 (root cause not determined: harness timing or regression) | browser-results-1.json |
| CI-10 | PF-013-O2 recorded GitHub fixture drifted from the live API (allow_auto_merge missing in live answer) | live job 123210630378 log |
| CI-11 | Credential scan (Repo.fs scan) treats PF-007 synthetic fixtures (events.ndjson/snapshot.json under tests/, commit 4b0d434) as committed record files; fails EXT-DATA-004-O1 and PF-015-O3 | live job 123210630378 log; tests/Causa.Acceptance.Live/Repo.fs:58 |
| CI-12 | Percepta CI job succeeded but verify-app reported complete:false (exit 3; revisionBoundEvidence false, allowNotApplicable false) and the final acceptance recorded producer percepta-verify-app as not-produced (G5) | evidence-out/gate-records/percepta/{exit-code,verify-app.json,verify-app.log}; producers.json |
| CI-13 | build-and-test aggregate job and candidate-run acceptance job failed (browser, live, acceptance-deterministic red) | run 40798638092 jobs 123213770724, 123213802839 |

## Evidence failures

| Id | Summary | Evidence |
|---|---|---|
| EV-01 | acceptance-run evidence tests: 4 of 21 failed - PF-018 (test-requirement map collection empty), PF-012 (layer lookup KeyNotFound), PF-011 browser layer (14 of 310 items failed), AR-004 (stale workflow text). These are not bound tests; ledger statuses of PF-012/PF-018 stay PASS and AR-004 NOT_RUN | TestResults/acceptance-run/acceptance-run_net10.0_20261029015519.trx |
| EV-02 | traceability producer failed (G8, G9) | bundle/ledger.json gates G8/G9; bundle/evidence/gates/G8.json |
| EV-03 | defect-class-review producer failed: all 8 class closures cite R5 fix SHAs absent from R6 history; DCT-004-A NOT_RUN (does not exist, superseded by DCT-004-B under DEC-R6-001); DCT-LIVE-A failed (timeout waiting for conflict Reload button) | producers/defect-class-review.json; live job log |
| EV-04 | percepta-verify-app not-produced in final acceptance (see CI-12) | producers.json |
| EV-05 | No owner/reviewer attestations: attestations.json empty; PRs #1-#4 have zero reviews | inputs/attestations.json; gate-records/attestations/pulls.json |
| EV-06 | No praxis process records of kinds dependency-decision, owner-approval, scope-change, acceptance-reference, test-result; release-selection and final-disposition exist only as agent assertions/transcriptions; reviews unattested | gate-records/final-acceptance-records/events.jsonl |
| EV-07 | VER-001/VER-002: no pre-lock newest-release comparison recorded; cannot be recreated | progress.json knownHonestFailures |
| EV-08 | Plan verifier marks 64 obligations FAIL whose own bound tests passed (evidence-invalid propagated from a failing sibling in the same requirement) | evidence-out/plan-assessment.json [artifact acceptance-final-46d5e11 (run 40803128103)] vs evidence-out/bundle/traceability.json [artifact acceptance-final-46d5e11 (run 40803128103)] |
| EV-09 | Final review ran its checks against 02c37d1 and carried them to 46d5e11 (restart 01:10:14, finish 01:11:44); candidate CI pending and no deployment at review time | docs/r6/reviews/final-review.json candidate, restartedAtC2 |

## Governance failures

| Id | Summary | Evidence |
|---|---|---|
| GF-01 | D-01 scope review unattested (DEC-R6-012) | DEVIATIONS.md D-01 |
| GF-02 | D-02 UI-031-O5 / AR-019-O3 amendments not applied (no operator approval records; approvals.causa.json approvals: []) | DEVIATIONS.md D-02; evidence-out/inputs/approvals.causa.json |
| GF-03 | Agent-written approvals file (commit 1865da2) violated RDP-003 and was withdrawn | evidence-out/inputs/approvals.causa.json description |
| GF-04 | D-04: 775 equivalence reviews transcribed by an agent naming the owner as reviewer | DEVIATIONS.md D-04; requirements/preservation/equivalence-reviews.json (7ace2ce) |
| GF-05 | D-05: forma-workflow 1.1.0 decision record (DF-CAUSA-2026-0601) agent-transcribed | DEVIATIONS.md D-05 |
| GF-06 | D-06: DEC-R6-KICKOFF ratified by merging #2/#3, no approving review | DEVIATIONS.md D-06; pulls.json |
| GF-07 | D-10: REVIEW-1 attested in chat; #4 merged by coordinator | DEVIATIONS.md D-10 |
| GF-08 | D-13: final review and final disposition attested in chat; no attestation PRs | DEVIATIONS.md D-13 |
| GF-09 | D-09: owner kept write-capable CAUSA_GITHUB_TOKEN contrary to DEC-R6-010 | DEVIATIONS.md D-09; kickoff/rehearsal-006/OWNER-DECISIONS.md DEC-R6-010 |
| GF-10 | D-11: defect classes not re-reviewed before freeze; defect-classes.json carries R5 PASS closures into R6 candidate | DEVIATIONS.md D-11; acceptance/defect-classes.json |
| GF-11 | Test weakened (UI-034-O2 assertion removed after f91d2c5) despite WORK-PLAN rule "Never edit or delete existing tests to make them pass" | final-review.json RF-F-8; docs/r6/WORK-PLAN.md |
| GF-12 | Agent merges via API appear under owner account | REHEARSAL-006-RESULTS.md (no independent evidence file found) |
| GF-13 | D-03: Conditor run without session token (Conditor credential defect) | DEVIATIONS.md D-03 |
| GF-14 | D-07: Arca 0.6.0 not adopted contrary to ESO-R6-09 owner decision; Web Locks fallback fails (R6-D-002) | DEVIATIONS.md D-07 |

## Discrepancies between the summary and the evidence

Summary: `/home/user/indy-init/docs/REHEARSAL-006-RESULTS.md`. Each row gives the summary's claim, what the evidence shows, and the evidence files.

| Id | Summary claim | Evidence | Files |
|---|---|---|---|
| DX-01 | Percepta: "23 of 24 in-scope screen contracts PASS at the candidate's run" | verify-app.json counts PASS 24, FAIL 0, NOT-APPLICABLE 1, so all 24 in-scope contracts pass, but complete=false, exit-code 3, revisionBoundEvidence=false. The final acceptance records producer percepta-verify-app as "not-produced" (G5 inputs). The CI percepta job reports success. | acceptance-final artifact (run 40803128103) evidence-out/gate-records/percepta/{verify-app.json,exit-code,verify-app.log}; acceptance-final artifact (run 40803128103) evidence-out/producers.json; bundle/evidence/gates/G5.json |
| DX-02 | "Other FAIL requirements (19)", followed by a list | The list contains all 19 FAIL requirements, AR-035 included. 19 is the total, not "other". | acceptance-final artifact (run 40803128103) evidence-out/verdict.json |
| DX-03 | The failure table row "Tests of the run's own evidence: PF-011, PF-012, PF-018 and AR-004 (4 of 21 failed)" is presented as an explanation of FAIL requirements | These are unbound acceptance-run evidence tests. PF-012 and PF-018 are PASS in both verifiers, and AR-004 is NOT_RUN. PF-011 is FAIL because of the bound live test PF-011-O2, which re-asserts that UI-016-O11 passed. | TestResults/acceptance-run/acceptance-run_net10.0_20261029015519.trx; acceptance-final artifact (run 40803128103) evidence-out/bundle/ledger.json; live job 123210630378 log |
| DX-04 | "The rest: failing bound browser specs in shards 1 and 2" | Wrong or incomplete. (a) UI-015-O1, UI-021-O3, UI-040-O2, WDL-015-O1 and WDL-015-O2 failed as "BLOCKED: the frozen verification plan … is not available", because the CI browser job never provides CAUSA_PLAN. UI-015, UI-021, UI-040 and WDL-015 FAIL only for this reason. (b) UI-006-O1 and UI-006-O4 failed in the live job, not in a shard. (c) PF-013-O2 (recorded-fixture drift, allow_auto_merge), EXT-DATA-004-O1 and PF-015-O3 (the credential scan flags the PF-007 fixtures in tests/) are .NET live tests. (d) UI-018-O2 is a copy mismatch ("connected" against /Connected/). (e) UI-004-O1 and UI-016-O13 are test-hook timeouts. The summary never names UI-016-O13, UI-006-O4, PF-013-O2, PF-015-O3 or EXT-DATA-004-O1. | browser-results-1.json, browser-results-2.json (run 40798638092); live job 123210630378 log; .github/workflows/build-and-test.yml browser job (lines 419-581); tests/browser/acceptance/_plan.js; tests/Causa.Acceptance.Live/Repo.fs:58 |
| DX-05 | UI-010-O2: "Offline case fails because of R6-D-003" | In the candidate run, UI-010-O2 failed on the 403 "forbidden" write-path case, which showed "Saving to GitHub… not confirmed yet.". The offline case was not the failing assertion. The obligation's test is also unbound by a tag change (RF-F-9). | browser-results-1.json; acceptance/reviews/final/review-tag-changes.json |
| DX-06 | UI-031-O5: "No role model; verified as written" | The CI failure is "element(s) not found" for the administration screen at the first not.toContainText assertion. That is not a demonstrated role-model assertion failure. The test is also unbound after review (partial). | browser-results-2.json; plan-assessment.json unboundAfterReview |
| DX-07 | The defect table rates AR-030 (R1-F-6) "high" and the build race "high (CI)" | REVIEW-1 rates R1-F-6 minor. No evidence file assigns a severity to the MSB3030 build race. | docs/r6/reviews/review-1.json; evidence/progress/progress.json |
| DX-08 | "main holds the candidate and two records-only commits after it: f64a287 and bf190b6" | origin/main has a third commit after the candidate: 41fb6d6, which changes only evidence/progress/progress.json and was made after the verdict. | git log origin/main in /home/user/indy-init-rehearsal-006 |
| DX-09 | Unused branches: "r6/candidate, r6/attest-base-review and r6/attest-base-final" | The remote also has r6/kickoff-decisions and r6/review-1. r6/candidate points to 02c37d1 (the first cut), not to the final candidate 46d5e11. | git ls-remote origin |
| DX-10 | R5 vs R6 table: the plan "adds AR-027..AR-035" | The P0 plan adds 7 requirements: AR-027, AR-028, AR-029, AR-030, AR-031, AR-034 and AR-035. AR-032 and AR-033 stay P1 (DEC-R6-005) and are not among the 115. | /home/user/indy-init/requirements-compiled/v3/verification-plan.json; kickoff/rehearsal-006/OWNER-DECISIONS.md DEC-R6-005 |
| DX-11 | R7 item 3: attesting through approving GitHub reviews means "the 26 process requirements can verify" | The evidence does not support this. AR-019-O3 is expected not to verify (DEC-R6-011, D-02). AR-005, AR-008-O2, AR-012-O2 and AR-024 also need dependency-decision, owner-approval or scope-change records, and events.jsonl has none (only work.* events). AR-034 depends on G7 closures that cannot close for this candidate. | plan-assessment.json; gate-records/final-acceptance-records/events.jsonl; DEVIATIONS.md D-02, D-11 |
| DX-12 | "One test weakening (UI-034-O2) … is in its tag changes, so it does not count" | It does not count only in the plan verifier. The ledger verifier counts UI-034 as PASS, and the same holds for WDL-001 and AR-014, whose tests were also tag-changed. The ledger's 70 PASS includes all three. The weakening also breaks the WORK-PLAN rule "Never edit or delete existing tests to make them pass". | bundle/ledger.json; plan-assessment.json; docs/r6/WORK-PLAN.md |
| DX-13 | The final review "re-verified" R6-D-001 and reviewed candidate 46d5e11 | The final review ran its reproductions and spot checks on 02c37d1 and carried them over to 46d5e11. It restarted on C2 at 01:10:14 and finished at 01:11:44. At review time the candidate's CI run 40798638092 was pending and candidate.deployed was null. The summary does not disclose this. | docs/r6/reviews/final-review.json (candidate, restartedAtC2, finishedAt); FINAL-REVIEW.md "What carries over" |
| DX-14 | D-13: "the final review and disposition were attested in chat", with the final review implied to be of 46d5e11 | DEVIATIONS.md D-13 says "The final independent review of 02c37d1", but final-review.json has candidateSha 46d5e11. The source records disagree with each other. | docs/r6/DEVIATIONS.md D-13; docs/r6/reviews/final-review.json |
| DX-15 | R6-D-003: "151 POSTs in 60 s for one change" | progress.json records a different measurement: about 5,000 failed POSTs per tab in 4-5 minutes. The summary reports only the final-review figure. | evidence/progress/progress.json (00:50 snapshot); final-review.json RF-F-2 |
| DX-16 | "Open critical defects (G6 …)" | G6 failed because the "live" producer failed: UI-006-O1/O4 live, DCT-LIVE-A and 8 live .NET tests, including the D-09 token and fixture-scan failures. R6-D-002 (AR-035-O4) failed in the browser producer, which is G5, G8 and G9. | bundle/ledger.json gates; bundle/evidence/gates/G5.json, G6.json; live job log |
| DX-17 | "26 NOT_RUN requirements, mostly AR-*" | All 26 are AR-*. | acceptance-final artifact (run 40803128103) evidence-out/verdict.json |
| DX-18 | Obligations (plan verifier): 89 FAIL | The number is correct, but 64 of the 89 have bound tests that passed. They are FAIL only because a sibling obligation's failure made their requirement evidence invalid. 24 have failing tests, and 1 (AR-035-O5) is producer-only. The summary does not explain this. | plan-assessment.json vs bundle/traceability.json |
| DX-19 | "Was evidence manufactured …? No" | The candidate's acceptance/defect-classes.json marks all 8 classes PASS, citing R5 fix SHAs absent from R6 history and reviewer evidence files that do not exist in the tree (acceptance/reviews/R5-*.md). These are carried-over PASS claims without evidence. The verifier rejects them, but the summary frames the issue only as a missed re-review. | acceptance/defect-classes.json; producers/defect-class-review.json; final-review.json RF-F-6 |
| DX-20 | The final review closed R1-F-1 "(23ac222)" | final-review.json closure for R1-F-1 cites Adapter.syncFor and reproductions, not a commit. 23ac222 comes from progress.json. This is a minor sourcing gap, not a contradiction. | final-review.json closure; progress.json |

## Not found / limits

- The owner chat transcripts behind D-04/D-06/D-10/D-13 are not in the repository, so only the transcriptions were checked.
- The merge attribution of #4 to the owner (GF-12) was not independently verified. The pull request API was not queried for actor details.
- Root causes of UI-004-O1 and UI-016-O13 ("injected state was not rendered in time") and UI-006-O4 were not determined from the logs.
- The CI starvation window (22:22-23:54) and the MSB3030 race are taken from progress.json. The individual runs were not re-fetched.
- Playwright trace artifacts (134 MB) were not downloaded.
