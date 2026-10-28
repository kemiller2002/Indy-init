# Verification findings

## Why each obligation was NOT_RUN

49 obligations had no proving test in run 40620099844. The primary causes (codes as in [requirement-matrix.md](requirement-matrix.md)) group by bottleneck:

| Bottleneck | Cause codes | Obligations |
|---|---|---|
| Verification-plan defect: a process, aggregate or decision-conflicted obligation that the plan could only bind to an executed test | 11 | 19 |
| Process or governance evidence never generated, or owner/reviewer action missing | 9, 10 | 14 |
| Testing: no test, test not executed, or insufficient assertions | 2, 3, 4 | 6 |
| Development: the behaviour does not exist | 1 | 5 |
| Evidence existed but was not collected or bound | 5, 6 | 4 |
| Other (a process that deviated by design: AR-021-O1) | 12 | 1 |

Secondary causes add to this picture: evidence that existed but was not collected (5) is a secondary cause on 14 obligations, and plan defects (11) on 15.

The bottleneck is therefore **verification design and governance evidence (33 of 49), not development (5 of 49)**.

### Per obligation

| Obligation | Primary | Secondary | Missing evidence and why |
|---|---|---|---|
| AR-005-O1 | 9 | 11 | No record that Echelon capabilities, owners, versions and contracts were discovered before each component was designed. WDL-001 records this for the layout engine only. |
| AR-005-O2 | 9 | 10 | No reuse inventory and no approved justification for duplicated implementations; for example src/persistence/Json.fs and src/engine/Json.fs are two JSON codecs with no recorded justification. |
| AR-008-O1 | 11 | 5, 10, 9 | Two review files exist (acceptance/reviews/R5-independent-review.md, R5-final-review.md). The plan binds obligations only to executed tests, so no review record can satisfy them. Reviewer independence is asserted by the files, not authenticated, and the final review covered be01851, not the final candidate 1b9fb56. |
| AR-008-O2 | 11 | 5 | Both reviews list decisions 'recorded as given (not ratified)'. That is a process record the plan cannot bind. |
| AR-011-O2 | 1 | 2, 12 | Nothing compares a re-run with the original and separates reproducible differences from environment drift. CI installs dotnet-version 10.0.x, and the rehearsal repository has no global.json. A post-rehearsal rebuild of 1b9fb56 with SDK 10.0.402 gives 193 .NET WebAssembly framework files different content-hash fingerprints (absent from the deployed site) and changes the bytes of 2 same-named runtime files (wasm/_framework/dotnet.js, System.Collections.Immutable.7hl5l3lm5d.wasm). 3 metadata files differ by design. No tool classified this drift. |
| AR-012-O1 | 9 | 11 | Stream branches have no per-agent scope declaration (named requirements and paths). Attribution was reconciled after the fact: 58 work.attribution.reconciled events in .ros/events/events.jsonl. |
| AR-012-O2 | 9 | 1 | No detector for unrelated changes and no review record for including them. EII-001-B1 proposes a stream contract. |
| AR-015-O1 | 9 | 11 | One Praxis work.checkpointed event in a 392-minute run with two container restarts. The streams resumed from agent transcripts, not from durable machine-readable checkpoints. |
| AR-017-O1 | 9 | 11 | No record of routing during the run. The Praxis preservation-gate finding was routed only after the run (EII-001). |
| AR-017-O2 | 9 | 11 | No ownership-discovery record per component. |
| AR-019-O3 | 1 | 11, 5 | Rehearsal 3's original results are not a machine ledger; validate-acceptance-ledger.py rejects them as structurally invalid (exit 1), and Indy-init's own verification classifies R3 as FAIL, not BLOCKED. Rehearsal 2 (45644a7) is INVALID, not ACCEPTED, which the Indy-init test test_rehearsal_2_original_ledger_is_not_independently_acceptable shows. That evidence lives in Indy-init and was never brought into the R5 bundle. |
| AR-020-O1 | 11 | 5 | The four-tier brief is retained at .sde/architecture/FOUR-TIER-ARCHITECTURE.md in the candidate. It is a repository artifact the plan cannot bind. |
| AR-020-O2 | 11 | 9 | A negative obligation: absence of per-step Ordo enforcement can be observed in the repository but not proven by a test. |
| AR-020-O3 | 11 | 5 | Typed F# domain model (src/engine/Model.fs) and documented boundaries (docs/architecture/BOUNDARIES.md) exist. There is no binding path for them. |
| AR-021-O1 | 12 | 11 | The process deviated: R5 ran five implementation streams and two reviewers under a coordinator, not one long-running implementation agent. The obligation states a preference, so this is not a violation. |
| AR-021-O2 | 11 | 9 | A negative obligation (do not require structured handoffs), with no evidence path. |
| AR-021-O3 | 9 | 10 | Correctness and requirement retention after compaction were not measured. The final independent review ran at be01851, not at the final candidate. |
| AR-023-O3 | 5 | 11 | Conditor SD-1 was verified in the Indy-init preflight (PRE-TOOL-02 PASS: v0.10.14 contains CON-367), and the bootstrap commit cf86744 came from Conditor 0.10.14 without the hand workaround. Neither was collected into the run bundle. |
| AR-024-O1 | 10 | 5 | Owner decisions were given in chat and transcribed by the agent (RDP-003): approvals files, equivalence reviews, registry approval, DF-CAUSA-2026-0001 and -0002, AI provider. None is authenticated as the owner's action. |
| AR-024-O2 | 10 | 11 | The agent wrote kickoff/rehearsal-005/approvals.json and approvals.causa.json on the owner's chat instruction. That is a recorded deviation, and no authenticated owner action closes it. |
| EXT-DATA-005-O1 | 11 | 1, 4, 6 | The only bound test (DCT-004-A) asserted that permission fields do not gate saving, so the independent review removed the tag (5336ec4). Repository and branch permissions are not checked anywhere in src/: RepositoryInfo.CanPush is parsed (GitHub.fs) and stored but never used, because owner decision DEC-025 / OD-INDY-10 makes permission fields advisory (DC-004). The frozen plan never reconciled that decision with this clause. Path, manifest, schema and admissibility checks are implemented; their tests ran and passed under other VPO ids, and post-rehearsal tests add more. |
| EXT-DATA-006-O2 | 6 | 10 | Tagged tests were falsified by F-1 at the independent review. F-1 was fixed in 5336ec4 and the tags were removed in the same commit. The fix's tests (DCT-001-A/B, the conflict test, DCT-004-A) and the live two-tab DCT-LIVE-A variant passed in run 40620099844, but nobody re-bound them after the fix was re-reviewed. |
| PF-005-O1 | 2 | 11 | No test asserts that UI and domain tests are independent of GitHub, network and credentials. The isolation holds by construction: a recorded-behaviour fake, and a network guard in tests/browser/support/fixtures.js. A post-rehearsal run of all 252 .NET tests with networking forced to a dead proxy and no credentials passed. |
| PF-009-O3 | 4 | 6 | Bound tests proved request identity and an earlier connection only, so the tag was removed (5336ec4). Active-target switching and credential-generation refusal were not asserted. Post-rehearsal tests assert both. |
| PF-014-O2 | 3 | 4, 12 | Live browser specs ran against a locally served dist/, not the deployed application, although playwright.config.js supports CAUSA_BASE_URL. No version or digest is asserted in the browser; the live catalog load (causa-persistence catalog) does check every manifest digest. The live browser and catalog jobs checked out causa-data at ref main, not a pinned commit. |
| PF-015-O1 | 11 | 1 | Owner decision DEC-025 / OD-INDY-10: every connection needs a credential and there is no anonymous mode, because R3 exhausted the anonymous quota. Credential.create refuses a blank token, so no catalog test can run without a PAT (F-3). The plan kept this obligation without reconciling it with the decision. |
| PF-015-O2 | 2 | 10, 11 | No test or record shows that CAUSA_GITHUB_TOKEN has least-privilege permissions. The live write tests need write access to the test repository, so the obligation's 'read-only' wording conflicts with PF-014 and needs an owner decision. The application refuses broad classic scopes (Sync ProbeRepository, GitHub.broadClassicScopes). |
| PF-019-O1 | 9 | 11 | The development-order workflow (smallest tests first, batched integration, full live suite before release) was not recorded per stream. Layer timings exist only for CI (gate-records/layer-timings.json). |
| UI-011-O4 | 1 | 4 | The only tagged test checked focus after the WebAssembly handoff, so the final review removed the tag (f412598). Post-rehearsal probe: after a command re-render, scroll and focus are kept exactly (the control's viewport top is unchanged at 313.125 px); after back navigation, scroll is restored but focus falls to body. |
| UI-015-O2 | 11 | 6 | An aggregate obligation that restates other obligations: catalog load and replay (EXT-DATA-013), frozen fixtures (FixtureReplayTests), keyboard and touch (UI-011-O1/O3), graph text (UI-011-O5), command enforcement (UI-004), save/sync truthfulness (UI-006), conflict recovery (PF-*), reflow (UI-011-O2), report and startup failure (WASM-LOAD). Each element passed under its own VPO; no test names this one. |
| UI-015-O3 | 3 | 12, 7 | The production browser suite never ran against the deployed exact SHA, although the config supports CAUSA_BASE_URL. The mocked-persistence negative control ran against the local test build. Pages now serves b972cb4 (same product source, different build), so the exact-SHA run can no longer be made. Secondary (12): the deployed mode is unusable for mocked specs, because the network guard in tests/browser/support/fixtures.js aborts the deployed host (reproduced post-rehearsal: "a mocked browser test tried to reach the real network"). |
| UI-016-O11 | 1 | 4 | The tagged test measured only declared ink tokens on ivory (tag removed in f412598). Post-rehearsal probe over eight views: every rendered text run meets AA, wash surfaces included. Amber indicator borders (#E9A23B) are 1.95:1 on ivory and 2.17:1 on white, below the 3:1 non-text threshold. |
| UI-016-O15 | 10 | 6 | Deployed exact-SHA screenshots at desktop, iPad portrait and landscape, and mobile exist (gate-records/deployed-screenshots, 1b9fb56873fe) but are not bound. Token, contrast, focus and reduced-motion checks passed under other VPOs. No independent visual review of the screenshots was recorded. |
| UI-021-O3 | 11 | 5 | A Praxis traceability obligation with a test-only binding. Praxis work-item attribution (WI-0001..WI-0008) and 'approvedExclusions: none' exist. |
| UI-030-O3 | 11 | 7, 10 | traceability.json records requirement ids, tests and the deployed SHA. The independent reviews covered be01851 and 543ddc2, not the final candidate 1b9fb56. |
| UI-031-O5 | 1 | 10 | No role or access control exists for operational information. Administration is visible to anyone who opens the page; the credential part is met (the token never reaches page, URL or storage). Whether a single-user browser application needs roles is an owner decision. |
| UI-040-O2 | 11 | 5 | The R5 derived-requirements package is in .praxis/inbox/documents/2026-10-27-R5-REQ-001-derived-requirements-package.md and the preflight records 'approvedExclusions: none'. The plan has no way to bind either. |
| VER-001-O1 | 11 | 5 | PRE-DEP-01 PASS (echelon-current 1.29.0 approved) and the lock gate (PASS 55) exist. The 'newest at lock time' claim rests on the registry approval, not on a bound selection record. |
| VER-001-O2 | 11 | 5 | The applicable-system list is in acceptance/stack/pin.json and the preflight record. There is no binding path. |
| VER-001-O3 | 11 | 9 | No per-system owner and release-channel discovery record, apart from forma-workflow (WDL-001). |
| VER-002-O1 | 5 | 11 | The preflight ran at 2026-10-27T19:51:18Z against registry commit 402bec21, before the freeze at 19:51:59Z. It was not collected as VER-002 evidence. |
| VER-002-O2 | 9 | 5 | The checked timestamp and source exist (preflight). No candidate list and no 'why selected' rationale were recorded; the 1.28.0 to 1.29.0 move rests on the owner's chat instruction 'go ahead and update it'. |
| VER-002-O3 | 11 | 5 | Releases are pinned by registry digest, not by main commits. That is observable, but there is no binding path. |
| VER-003-O1 | 11 | 5 | The lock gate and integration tests passed on the selected set. There is no binding path. |
| VER-003-O2 | 11 | 10 | DF-CAUSA-2026-0001 records the forma-workflow 1.1.0 exception, owner-approved in chat (agent-transcribed). |
| VER-004-O3 | 5 | 11 | R5 re-resolved to echelon-current 1.29.0 (preflight-record.json and the registry approval). It was not collected into the bundle. |
| VER-010-O1 | 11 | 5 | VER-005..VER-009 PASS and VER-001..VER-004 are classified here. There is no reconciliation record that binds them. |
| VER-010-O2 | 9 | 11 | No record that work items were routed through each owning repository's Praxis input path during the run. |
| WDL-014-O1 | 3 | 12, 6 | Invocation of the real forma-workflow frame, the pinned version check (graph-layout.spec.js, which ran but is unbound) and the accessibility specs ran against the local site only. The final review removed the partial tag (f412598). No graph spec ran against the deployed site. Secondary (12): CAUSA_BASE_URL deployed mode is blocked by the mocked-spec network guard. |

## Verification-plan curation (Indy-init PR #32)

- **State.** PR #32 ("Curate the verification plan with Rehearsal 5's test bindings (VP-5d1202ddec1e)") was merged on 2026-10-28 at 06:06 UTC, before this audit. The audit did not merge or change it.
- **What changed.** The frozen plan is VP-e77a2134e1b0 (`4b38213`, file sha256 `f79efdfa…`, identical to the preflight record). The curated plans are VP-926d0d1a223c (`5004117`, 281 bound) and VP-5d1202ddec1e (`43707d9`, 277 bound). With `testIdentity` and `planIdentity` removed, the frozen and curated plans differ in exactly two places, and both are freshness policies:
  - AR-010: FP-REVIEW-PER-COMMIT → FP-LIVE-PER-COMMIT
  - WDL-012: FP-DETERMINISTIC-INPUT-DIGEST → FP-LIVE-PER-COMMIT
- **Assessment.** Curation is almost entirely evidence binding, but **it also changes verification semantics** for those two requirements, both already PASS under their bound tests. Obligation texts, kinds, acceptance criteria, gates, rules, required evidence and dependencies are unchanged, as are all 31 audited requirements.
- **The four partial proofs.** Curation from VP-926d0d1a223c to VP-5d1202ddec1e removed four partial tags (UI-011-O4, UI-015-O3, UI-016-O11, WDL-014-O1) after the final review. That is a correct downgrade, not a semantic change.
- **Not counted as historical passes.** The 277 curated bindings are post hoc. They are not counted here as historical passing obligations: the frozen verdict was computed against VP-e77a2134e1b0, where `verify-plan` reports the 77 as UNBOUND.

### Classification of the open obligations for future curation

| Category | Obligations |
|---|---|
| Correct evidence, binding missing (tests ran and passed at 1b9fb56) | EXT-DATA-006-O2 |
| Proof incomplete (a test exists but does not cover every clause) | EXT-DATA-005-O1, PF-009-O3 (now covered by post-rehearsal tests), UI-011-O4, UI-016-O11, PF-014-O2 |
| Needs new tests or a working deployed-mode harness | PF-005-O1, PF-015-O2, UI-015-O3, UI-016-O15, WDL-014-O1 |
| Needs new process records (Praxis, `verify-process`) | AR-005, AR-008, AR-012, AR-015, AR-017, AR-020, AR-021, AR-024, PF-019-O1, UI-021-O3, UI-030-O3, UI-040-O2, VER-001, VER-002, VER-003, VER-004-O3, VER-010 |
| Needs a plan correction or owner decision before it can be satisfied | AR-019-O3, PF-015-O1, EXT-DATA-005-O1 (DEC-025), UI-015-O2 (aggregate), UI-031-O5 |
| Cannot be satisfied retrospectively | every deployed-exact-SHA obligation (UI-015-O3, WDL-014-O1, PF-014-O2, UI-016-O15's independent review of that deployment), because Pages now serves b972cb4. Also every candidate-bound review (AR-008-O1, UI-030-O3, AR-021-O3), every owner action that had to precede the timed run (AR-024-O1/O2), and per-step process records that had to be made during the run (AR-012, AR-015, AR-017, PF-019-O1) |

### Requirements independently verifiable with evidence that already existed (Question 5)

**1 requirement: EXT-DATA-006.** Its only open obligation is proven by tests that ran and passed in run 40620099844 at 1b9fb56, including the live DCT-LIVE-A two-tab variant.

The pre-review draft also counted AR-020, AR-023, UI-021, UI-040 and VER-004. The independent review withdrew all five, and the audit accepted that:
- AR-023 and VER-004 rest on a pre-run, agent-local preflight record with no run id or source SHA.
- UI-021's attribution was reconciled after the fact.
- UI-040's plan freshness needs live per-commit evidence.
- AR-020 needs a review naming the candidate.
