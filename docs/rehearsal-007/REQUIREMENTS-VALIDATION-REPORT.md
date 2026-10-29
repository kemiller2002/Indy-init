# Rehearsal 7 persistence requirements: validation report

**Verdict: the requirements are valid.** All ten persistence checks pass, and all fifteen planted defects are caught. The existing compiler suites pass, and every earlier baseline and package still reproduces. Compiled against the R7 baseline, every requirements-level compiler check passes. Four package-level checks fail, because the v3 curation predates these requirements. That is the v4 package cut, a separate step (see [READINESS-ASSESSMENT.md](READINESS-ASSESSMENT.md)).

| | |
|---|---|
| Requirements commit | `8445ca11986f7808190e4e33e30a4fe2d37981cd` |
| Baseline | `BL-CAUSA-INDY-INIT-8445ca11986f`, [`requirements/causa-requirements-baseline-r7.json`](../../requirements/causa-requirements-baseline-r7.json), sha256 `579cd8f5953c4bd218332426e86cc87edb029ebc51968c8ad417ff50b0898dab` |
| Size | 20 documents, 517 requirements (161 P0), 2595 obligations (475 acceptance, 78 value, 2042 constraint) |
| New | `docs/CAUSA-PERSISTENCE-REQUIREMENTS.md`: 46 requirements, PERSIST-001 to PERSIST-055, all P0, 255 obligations |
| Changed against Rehearsal 6 | 30 requirements amended (22 by ADR-R7-001, 8 by owner decisions DEC-R7-001..007), 46 obligations superseded, no requirement identifier removed |
| Owner decisions | [`kickoff/rehearsal-007/OWNER-DECISIONS.md`](../../kickoff/rehearsal-007/OWNER-DECISIONS.md), applied to the requirement texts before this pin |
| Previous baseline | `BL-CAUSA-INDY-INIT-856157a78d47` (Rehearsal 6), unchanged |
| Run | 2026-10-29, .NET SDK 10.0.402 (global.json), Python 3 |

## What was run

| # | Command | Result |
|---|---|---|
| 1 | `python3 tools/test-preflight.py`, `test-acceptance-ledger.py`, `test-acceptance-verdict.py`, `test-requirements-baseline.py` | All OK |
| 2 | `python3 -I tools/build-requirements-baseline.py . 8445ca1…` and `requirements-compiler baseline . 8445ca1…` | Byte-identical. The output is the committed R7 baseline |
| 3 | `dotnet run --project tools/requirements-compiler/tests -- --offline --source .` | All passed. This includes the new check that the committed R7 baseline is the port's output at its pinned commit. A planted change to one priority in the file made it fail |
| 4 | The compiler test suite in full mode, with the v3 curation and all 14 source repositories at their recorded commits | 24 negative/positive cases, determinism and committed-package check: all passed |
| 5 | `requirements-compiler … --curation requirements-compiled/v3/curation --out requirements-compiled/v3 --check` | Exit 0: v3 rebuilds byte for byte. v1, v2, the frozen R4/R5 baseline and the R6 baseline are unchanged |
| 6 | `dotnet fsi tools/validate-r7-persistence-requirements.fsx -- --root . --baseline requirements/causa-requirements-baseline-r7.json --previous requirements/causa-requirements-baseline-r6.json --trace docs/rehearsal-007/traceability.json` | **PASS**, 10 of 10 checks. Output: [`evidence/r7-persistence-validation.json`](evidence/r7-persistence-validation.json) |
| 7 | The same with `--self-test true` | 15 of 15 planted defects caught; unmodified inputs pass. Output: [`evidence/r7-persistence-validation-self-test.txt`](evidence/r7-persistence-validation-self-test.txt) |
| 8 | `requirements-compiler` against the R7 baseline, with a scratch copy of the v3 curation re-pinned to `8445ca1` | Requirements-level checks pass; package-level V09, V11, V17 and V18 fail (below). Output: [`evidence/compile-r7-baseline-with-v3-curation.json`](evidence/compile-r7-baseline-with-v3-curation.json). Nothing was written to `requirements-compiled/` |

Steps 3 to 8 depend on the generator change that adds the persistence document. A document listed in `INTRODUCED_DOCUMENTS` enters the baseline at every commit that has it. Steps 3 to 5 show that this changes no earlier baseline or package.

## The verifications asked for

| Verification | Check | Result | How it is decided |
|---|---|---|---|
| No contradictory offline-persistence requirements remain | R7-02 | PASS | Every obligation of the baseline is scanned for offline durability: saved or saving locally, queued offline, offline or local queue, local durability, syncing remotely, remotely synced, recovery after restart, crash recovery, pending writes. A match fails unless the sentence states a prohibition or a supersession. 7 negated matches are listed for review, all prohibitions in PERSIST, AR-035 or the interface freeze. Compiler V03 (no unresolved contradiction) also passes |
| No shared investigation-storage requirement remains | R7-03 | PASS | Every obligation is scanned for localStorage, sessionStorage, IndexedDB, Cache Storage, OPFS, BroadcastChannel, workers, and browser, device or shared stores. Each match must be a prohibition, and PERSIST-003's prohibition must be present. 6 prohibitions are listed for review, including EXT-DATA-004 (credentials) and D-204 |
| No Web Locks dependency remains | R7-04 | PASS | `navigator.locks`, Web Locks, storage leases, cross-tab locks and messaging. The 6 matches are PERSIST-004's prohibition and the PAT-11 and PAT-13 tests that remove or forbid them |
| GitHub conflict handling is fully specified | R7-05 | PASS | Eleven topics are each required as a P0 duty, by exact phrase, in their requirement: expected-head atomic commit (PERSIST-006), read-back confirmation (007), stale-head classification (008), automatic rebase (009), rebase limit of three (010), preservation of unsaved changes (011), explicit resolution (012), no overwrite (013), unique markers (014), reconciliation before retry (015), bounded retries (016) |
| Every new requirement has acceptance criteria | R7-06 | PASS | Each of the 46 PERSIST requirements has at least one acceptance obligation. Each functional, nonfunctional and interface requirement names an existing PAT test and is covered by a test requirement. PAT-01 to PAT-15 all exist and name their requirements, and the eleven named tests are present under their names. Compiler V06 (every requirement has verification coverage) also passes |
| No requirement instructs the agent to copy Causa implementation code | R7-07 | PASS | Obligations and current documents are scanned for copy, vendor, reuse, port, extract, adapt or translate next to source, code, implementation, tests or scripts and an earlier build. The former "Extract actual types and exported interfaces from accepted R001 F# source" was replaced. The two remaining matches are PERSIST-026's prohibition and a verification-plan freshness rule about reusing test evidence |
| All affected requirement identifiers remain traceable | R7-08 | PASS | All 471 Rehearsal 6 requirement identifiers are still present. Each of the 46 obligations whose text changed is in `traceability.json` with its exact Rehearsal 6 text and what supersedes it, and every replacement identifier exists. A stale or missing entry fails |
| Historical defects are linked | R7-09 | PASS | R6-D-002, R6-D-003 and six Rehearsal 6 GitHub conflict and rebase findings (R1-F-2, RF-F-3, RF-F-9, R6-D-001, R1-F-1, RF-F-7), plus deviation D-07. Each names existing requirements and tests |
| Long-term documents do not contradict | R7-10 | PASS | Documents outside the baseline are scanned with the R7-02 to R7-04 patterns. Each match must be negated or sit in a section that records ADR-R7-001, or be listed in `permittedMentions` with a reason; two are listed. 46 such matches are listed for review |
| The persistence document is pinned | R7-01 | PASS | It is in the baseline, and its pinned hash equals the checkout |

### Negative controls (step 7)

Each planted defect makes its check fail:

- **R7-02:** AR-035-O4's old device-local sentence restored; an offline queue added to R10.5.
- **R7-03:** D-204's "IndexedDB … may be used" restored; PERSIST-003's prohibition removed.
- **R7-04:** an exclusive `navigator.locks` section required.
- **R7-05:** the rebase limit removed; reconcile-before-retry removed.
- **R7-06:** PERSIST-017's acceptance removed; PAT-08 removed.
- **R7-07:** "copy the persistence module source code from indy-init-rehearsal-006" planted.
- **R7-08:** R10.7 removed; an untraced edit to UI-004; a supersession entry dropped.
- **R7-09:** R6-D-002 untraced; R6-D-003 untraced.

## Compiling the R7 baseline with the v3 curation (step 8)

| Check | Result | Cause |
|---|---|---|
| V01 verbatim representation, V02 trace to source, V03 contradictions, V04 references, V05 DAG, V06 verification coverage, V07 contracts, V08 decisions, V10 nothing marked complete and no application code at the source commit, V12 repository commits, V13 Percepta, V14 drift, V15 blockers, V16 manifests | PASS | |
| V09, V17 | FAIL | The 46 PERSIST requirements are in no work item of `curation/tasks.json` |
| V11 | FAIL | Four v3 citations quote Rehearsal 6 text this change replaced: the interface freeze's "Provider-neutral operations: load, append with expected version…" and "Extract actual types … from accepted R001 F# source …", ADRP's "physical local browser durability", and UI-015's "three active-catalog investigations" (DEC-R7-006). This is the intended effect |
| V18 | FAIL | The pinned approvals file `kickoff/rehearsal-006/approvals.causa.json` is bound to the Rehearsal 6 baseline, and v3's derived-obligation map for UI-015-O2 quotes its three-investigation wording |

The scratch plan has 161 P0 requirements and 613 obligations. Each has an acceptance contract. The 255 PERSIST obligations are unclassified, because no verification rule of the v3 curation covers the new document yet. Three new obligations (UI-006-O7, PF-015-O5 and UI-016-O18) are classified but have no bound test. The other 355 are bound, but by obligation identifier. So the v3 bindings of the superseded P0 obligations now point at Rehearsal 6 tests of text that has changed, and v4 must re-bind them. Those obligations are UI-006-O1 to O6, UI-010-O2, UI-015-O2, UI-031-O5, PF-014-O2, PF-015-O1 and O2, EXT-DATA-013-O1 and O2, AR-035-O4 and AR-035-O5. None of these is a defect in the requirements. Each is a curation input of the v4 package.

## Limits

- The pattern checks are mechanical. A negation anywhere in a sentence permits a match, so every permitted match is listed in the JSON output for a human to read; none was found to be a hidden duty. A contradiction phrased in words outside the patterns would not be found. The negative controls show the patterns catch the Rehearsal 6 wording itself.
- R7-06 checks that each requirement names a test and that the test names the requirement. Whether a test proves the whole obligation is a review judgement (AR-034), made when Rehearsal 7 binds its tests.
- The validator does not run the compiler; steps 3 to 8 do.
