# Independent review

**Reviewer.** A separate review agent that took no part in the implementation or in the initial classification. It received the code, the frozen evidence, the post-rehearsal evidence and the pre-review matrix. Its brief was to falsify every IMPLEMENTED and NOT IMPLEMENTED classification, every "existing evidence is sufficient" claim, every claim that a process was performed, and every cause code. It was read-only, and its raw output is kept unedited in [post-rehearsal-evidence/independent-review-raw.json](post-rehearsal-evidence/independent-review-raw.json).

**How disagreements were resolved.** By evidence, never by vote. Where the reviewer's evidence held, the audit adopted the change. Where it did not, the audit kept its position and says why. Where neither side's evidence settled the question, the result was downgraded toward INDETERMINATE or UNKNOWN.

**Limit on independence.** The reviewer is an agent in the same operator session as the auditor. It is independent of the classification, not of the Echelon tooling or of the operator. That is the same structural weakness this audit reports for AR-008.

## Resolution

| Item | Reviewer | Audit resolution | Evidence |
|---|---|---|---|
| EXT-DATA-005 | A → B | **Adopted.** Primary cause set to 11, not the reviewer's 1, because the root is the same unreconciled DEC-025 that the reviewer itself named as the cause for PF-015-O1. | `RepositoryInfo.CanPush` is parsed and never used; OD-INDY-10: "Permission fields are advisory (DC-004)" |
| AR-019 | D → B | **Adopted** | The R3 clause's outcome (BLOCKED) is not produced; the verifier rejects R3 structurally, and Indy-init classifies it FAIL |
| WDL-014 | A, upheld only if the deployed run is published | **Changed by the audit to D** | The deployed run is b972cb4 and post-rehearsal; its graph-layout specs load the vendored frame directly; the app-invocation spec ran only on the local build |
| AR-008, AR-012, AR-024, VER-001, VER-003, VER-010 | PERFORMED_EVIDENCE_INADEQUATE → UNKNOWN | **Adopted** | One session id on every trailered commit; decisions attested only by agent transcription; no scope, routing or reconciliation records |
| AR-015 | NOT_PERFORMED → PERFORMED_EVIDENCE_INADEQUATE | **Rejected** | The single checkpoint is the WI-0002 bootstrap item at 20:20Z. None was kept during implementation or across the two restarts, so the obligation ("maintain … checkpoints") was not performed |
| AR-020 | sub-status upheld; O2 "contradicted" by 5333387 | **Changed by the audit to UNKNOWN** | 5333387 is an acceptance boundary (a Percepta NOT-APPLICABLE gate), which O3 allows, so compliance is unresolved either way |
| "Sufficient at deadline": AR-020, AR-023, UI-021, UI-040, VER-004 | not admissible | **Adopted.** Question 5 is 1, not 6 | Pre-run, agent-local preflight with no run id or SHA; attribution reconciled after the fact; no review of 1b9fb56; live freshness |
| Causes: EXT-DATA-005-O1, UI-011-O4, UI-016-O11, AR-019-O3 → 1; PF-015-O1 → 11; secondaries for AR-008-O1 and PF-015-O2 | corrections | **Adopted**, except EXT-DATA-005-O1 (11, see above) | A sufficient test would fail on each of the first four; DEC-025 for PF-015-O1 |
| "198 artifacts differ" (AR-011) | mis-measured: 193 rows are Pages 404 bodies | **Adopted and corrected** | The 404 body hash was confirmed (`b6205073…`); the corrected comparison shows 193 fingerprints absent from the deployed site, 2 runtime files with different bytes and 3 expected metadata differences |
| DEC-025 not cited | finding | **Adopted** | Now cited on EXT-DATA-005, EXT-DATA-006 and PF-015 |
| Agent statements treated as fact | finding | **Adopted** | "Independent" reviews, chat decisions, container restarts and "no hand workaround" are now attributed as claims; none of the A classifications depends on them |
| Commit count | "284 agent commits" | **Corrected** to 282 of 283 up to 1b9fb56 | `git log 1b9fb56` |

Net effect: 3 classification changes (A 5 → 3, B 5 → 7, D 3 → 3 with a different membership), 7 process sub-status changes, 5 withdrawn "sufficient" claims and 7 cause corrections. 1 proposal was rejected.

## Caveats the reviewer attached to upheld classifications

- **EXT-DATA-006 (A).** "Separately verified authorization" holds only under DEC-025.
- **PF-005 (A).** The offline .NET run redirected proxy variables rather than isolating the network. The browser suite was not run offline, although its guard fails on any non-local host.
- **PF-009 (A).** The fixture-versus-GitHub provider switch is covered by code, not by an executed test.

## Reviewer's summary (verbatim)

### Independent review of the Rehearsal 5 NOT_RUN matrix

Reviewer: an independent review agent with no implementation or classification role. The review was read-only. Full detail is in `independent-review.json`.

#### What I checked
- **Obligation text and status.** All 102 obligation texts match the frozen plan VP-e77a2134e1b0 (indy-init 4b38213). All 49 NOT_RUN statuses match the frozen `traceability.json`.
- **Post-rehearsal tests.** I re-ran the audit's 13 post-rehearsal tests against r5-wt at 1b9fb56. All 13 passed.
- **Deployed browser run.** `pw-deployed.log` finished at 82 passed and 4 failed. Three failures are evidence-screens tests, which fail because the test compares the local and deployed `proof.json`. The fourth is Percepta, whose CLI is not installed.

#### Classification changes
| Id | Audit | Recommended | Reason |
| --- | --- | --- | --- |
| EXT-DATA-005 | A | **B** | Branch permissions are never checked. `CanPush` is parsed but never used. No test asserts a permission check. The clause conflicts with DEC-025/DC-004 (permission fields are advisory). |
| AR-019 | D | **B** | The R2 clause is met (INVALID). The R3 clause, "must yield BLOCKED", is demonstrably not met: R3 is structurally rejected (exit 1) and classified FAIL. |

#### Process sub-status changes
| Id | Audit | Recommended | Reason |
| --- | --- | --- | --- |
| AR-008 | PERFORMED_EVIDENCE_INADEQUATE | **UNKNOWN** | Reviewer distinctness is self-asserted. Every commit with a session trailer names one session, the review commits included. |
| AR-012 | PERFORMED_EVIDENCE_INADEQUATE | **UNKNOWN** | Work-item paths are empty. No record shows that unrelated changes were detected or reviewed. |
| AR-015 | NOT_PERFORMED | **PERFORMED_EVIDENCE_INADEQUATE** | One real `work.checkpointed` event exists. Checkpoints were then not maintained. |
| AR-024 | PERFORMED_EVIDENCE_INADEQUATE | **UNKNOWN** | The owner's decisions are attested only by the agent's transcription. |
| VER-001 | PERFORMED_EVIDENCE_INADEQUATE | **UNKNOWN** | There is no owner or channel discovery record except for forma-workflow. That is the same evidence situation as AR-005, which the audit rated UNKNOWN. |
| VER-003 | PERFORMED_EVIDENCE_INADEQUATE | **UNKNOWN** | The exception's authorization is attested only by the agent. |
| VER-010 | PERFORMED_EVIDENCE_INADEQUATE | **UNKNOWN** | The audit's own investigation says there is no reconciliation record and no routing record. |

#### existingEvidenceAtDeadline = sufficient
| Id | Holds? | Reason |
| --- | --- | --- |
| EXT-DATA-006 | **Yes** | CI and live results come from run 40620099844 at 1b9fb56. Only the binding was missing. |
| AR-020 | No | No review names 1b9fb56, and FP-REVIEW-PER-COMMIT needs one. O2 is contradicted: executable gates were added during the run (for example 5333387) with no measured-benefit record. |
| AR-023 | No | The preflight was local, pre-run and agent-run, with no run_id or source_sha. PRE-TOOL-02 only checks that a string is present. |
| UI-021 | No | The attribution was reconciled after the fact, and the evidence is not bound to the candidate. |
| UI-040 | No | FP-LIVE-PER-COMMIT needs live evidence; these are repository documents. |
| VER-004 | No | The record is a pre-run preflight outside the bundle, with no run_id and no Dokimos reuse attestation. |

#### Cause-code corrections
| Obligation | From | To | Reason |
| --- | --- | --- | --- |
| EXT-DATA-005-O1 | 4 | **1** | The branch-permission check is not implemented. Secondary 11 for the DEC-025 conflict. |
| UI-011-O4 | 4 | **1** | The implementation is incomplete (focus is lost on back navigation), so a sufficient test would fail. |
| UI-016-O11 | 4 | **1** | Amber indicators measure 1.95 to 2.17:1, below 3:1, so a sufficient test would fail. |
| AR-019-O3 | 11 | **1** | Calling the plan defective needs an owner ruling, and none exists. |
| PF-015-O1 | 1 | **11** | The missing anonymous mode was decided by DEC-025/OD-INDY-10. The plan never reconciled the obligation with it. |

Secondary-cause additions:
- AR-008-O1: add 9.
- PF-015-O2: add 11.

#### Upheld, with caveats
- **EXT-DATA-006.** "Separately verified authorization" holds only under DEC-025, where GitHub's answer to the write decides.
- **PF-005.** The offline run only redirected the proxy variables, and the browser suite was not run offline.
- **PF-009.** "Active provider" means fixture versus GitHub. That switch is covered by the mechanism (`PersistencePort.fs:318` plus the Generation counter) but was never executed.
- **WDL-014.** The deployed-site result now exists only in the scratchpad log, and it is for b972cb4. Most graph-layout specs load the vendored frame directly; only `shell.spec.js:258` proves that the application invokes it.

#### Main general findings
- **`deployed-compare.tsv` is mis-measured.** 193 of its 198 DIFF rows are GitHub Pages 404 bodies (hash b6205073…). They are not changed artifacts.
- **DEC-025 is never cited.** It affects EXT-DATA-005, EXT-DATA-006 and PF-015.
- **Missing cited file.** The matrix cites `browser-deployed-suite.txt` with the result "see file", but the file did not exist at review time.
