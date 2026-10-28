<!-- Investigation note written 2026-10-28 by a read-only investigation agent for the Echelon Engineering Improvement Initiative (Phase One). Local paths refer to that session's clones. Numbers marked as recomputed in ../BASELINE-2026-10-28.md were re-derived independently. -->

# Rehearsal 4 evidence baseline and Indy-init requirements compiler inventory

Collected 2026-10-28. This was a read-only investigation. No edits, commits, pushes, comments, issues, PRs or CI re-runs were made. kemiller2002/indy-init-rehearsal-005 was not touched.

Sources:
- Clone `/home/user/w/indy-init-rehearsal-004` (R4). Full history was already present. `git fetch` pulled every remote branch, including `acceptance-evidence`, `remediation/r4`, `remediation/r4-acceptance` and `preflight/t07-derivation`.
- Clone `/home/user/w/Indy-init` at HEAD `459f65a`.

**Limit: the GitHub Actions API could not be read.** The GitHub MCP tools refused `kemiller2002/indy-init-rehearsal-004` ("not configured for this session; allowed: kemiller2002/causa-data"). `gh` returned HTTP 403. The auto-mode classifier denied an `add_repo` (read) request. Every CI count below therefore comes from records committed in the repositories, not from the Actions API. These records are named, and none of them was re-derived independently.

Classification labels: **[DEFECT]** confirmed defect, **[INEFFICIENCY]** confirmed inefficiency, **[ARCH?]** suspected architectural weakness.

---

## Part 1: Rehearsal 4 baseline

### 1.1 Identity, start and kickoff

| Item | Value | Where |
|---|---|---|
| Rehearsal repo | kemiller2002/indy-init-rehearsal-004 | |
| Bootstrap commit | `e0cc0430743c15b43550f00d606c1e63d55a5a3c`, 2026-10-27 02:03:52 EDT (06:03:52Z), "Conditor bootstrap: conditor init --preset indy-init (Conditor 0.10.13, echelon-current 1.28.0)" | git |
| Clock start | 2026-10-27T06:04:39Z: the first green check on e0cc043 (Praxis validation run 40458934132). Build-and-test 40458934652 went green at 06:05:13Z and Echelon foundations 40458935473 at 06:05:51Z. Ceiling 420 min, deadline 13:04:39Z. | `evidence/rehearsal/clock.json` |
| Preflight freeze | Indy-init `ad2f1af` (2026-10-27 02:04:14 EDT), "Freeze the Rehearsal 4 preflight record before the timed run" | `Indy-init/kickoff/rehearsal-004/preflight-record.json` (`frozenAt` 06:04:14Z) |
| Requirements baseline | id `BL-CAUSA-INDY-INIT-aab8f80c7743`; sourceCommit `aab8f80c7743392e794b3b631542903ed4ed6f64`; file sha256 `621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9`; repin commit `45c9657` | preflight-record.json; `Indy-init/requirements/causa-requirements-baseline.json` |
| Compiled package frozen | `requirements-compiled/v1`, `package-manifest.json` sha256 `0cd4c3f5…ace7` (checked: `git show ad2f1af:…/package-manifest.json \| sha256sum` = `0cd4c3f5fe8d…`) | preflight-record.json |
| Dependency lock | `.conditor/lock.json` sha256 `719be140…3337`; echelon-current 1.28.0 | clock.json, preflight-record.json |
| Kickoff approvals | `approvals.json` (praxis format, APR-R4-001, excludes T27/T28 scope: 5 requirements, all priority `unstated`) sha `f893adea…`; `approvals.causa.json` (verifier format, empty approvals) sha `3e70dcd2…` | `Indy-init/kickoff/rehearsal-004/` |
| Equivalence reviews | 750 obligations, sha `164fc216…` (an agent transcribed the owner's chat approval: an RDP-003 deviation, recorded) | `kickoff/rehearsal-004/README.md` |

**The baseline file is committed at `45c9657`, not at `aab8f80`.** The baseline names `aab8f80` as its *source* commit. The JSON was committed one commit later, in `45c9657` ("Re-pin the baseline at aab8f80 …"). Checks:
- `git show aab8f80:requirements/causa-requirements-baseline.json | sha256sum` gives `76ef3158…`, the previous pin.
- `git show 45c9657:…` gives `621e2f96…`.
- No later commit touches the file.
- Running `python3 -I tools/build-requirements-baseline.py . aab8f80c7743392e794b3b631542903ed4ed6f64` in this session reproduced `621e2f96…57b9` byte for byte.

**108 P0 requirements, verified.** Computed from the baseline at `45c9657`:

```
python3: Counter(r['priority'] for r in baseline['requirements'])
 -> {'unstated': 337, 'P0': 108, 'P1': 16}   (461 requirements in total)
P0 by prefix: UI 30, AR 23, PF 19, WDL 14, EXT-DATA 13, VER 9
P0 obligations: 326   (2,293 obligations in the whole baseline)
```

The count agrees with:
- R4 `acceptance/traceability.json`: 108 entries;
- each ledger's `requirements: 108`;
- Indy-init `requirements-compiled/v1/verification-plan.json` `scope.requirements=108, obligations=326`.

### 1.2 Commit statistics (main at `78a1b15`)

| Metric | Value |
|---|---|
| Total commits on main | 90 (`git rev-list --count HEAD`) |
| Timed run (e0cc043..b6947b8 inclusive) | 53 commits, 06:03:52Z → 11:00:46Z |
| Remediation branch (PR #1 head `c0ee1b5`) | 36 commits, `c01aa64` (07:43 EDT) → `c0ee1b5` (16:10 EDT) |
| Merge commits | 1: `78a1b15` "Merge PR #1: Rehearsal 4 remediation", 16:17:07 EDT, by Kevin Miller |
| Reconciliation commits | 1 explicit: `276cdfc` "Praxis: remediation work items WI-0001..WI-0006 ready; **post-hoc reconciliation** of the commits made before they were begun". Two more are Praxis bookkeeping only: `b12be88` (checkpoint) and `b795150` (WI-0005 complete). All three fall within 18 s, 16:07:51 to 16:08:13 EDT, after all the remediation code was committed. |
| Run elapsed | 296 min: 06:04:39Z → verdict at 11:00:31Z (`checkpoints.json` END). The owner stopped the run early. |
| Remediation elapsed (wall) | about 274 min from `c01aa64` to the merge. It contains an idle gap of about 5 h between `39607d5` (10:37 EDT) and `c5d9941` (15:40 EDT, the R5 review), so wall time is not effort. |
| Diff of the remediation | `git diff --stat b6947b8 78a1b15`: 98 files, +21,718 / −245 |

Other remote branches exist in the R4 repo: `r5/*`, `claude/rehearsal-5-prep-*`, and PRs #2–#5. They belong to Rehearsal 5 preparation and are outside this baseline.

### 1.3 PR #1 "Rehearsal 4 remediation": what it fixed

The record is `evidence/remediation/corrections.json` (`causa.remediation-corrections/1`), plus the commit messages. The plan is `docs/rehearsal/R4-REMEDIATION-PLAN.md` (WP1–WP7 = WI-0001..WI-0007).

| Finding | Commits | Fix |
|---|---|---|
| F-DHIST-01 D-HIST-WASM | 5060bc6, 9d06f38, 525c13c, 2e78580, 39607d5, 0712465 | Root cause recorded as INTERACTION_DEFECT_CONFIRMED: a trap in the Mono WASM interpreter's tier-up optimiser (`interp_link_bblocks`), triggered by Release builds *with debug information*. The CI build order shipped that variant. Fix: a non-incremental build, refusal of debug-flavour assemblies, an engine-identity proof, and eager verification of every catalog case after Connect again. The upstream runtime bug is **not fixed** (`docs/rehearsal/D-HIST-WASM-INVESTIGATION.md`). |
| T33 R2-08 Boundary A | 896c3fb | Expected-version is captured when editing starts. |
| T33 R2-09 | 559d9c2 | An investigation no longer in the repo is never shown as saved. |
| T33 R2-11 | d16548a | U+FEFF and unpaired surrogates are refused (TEXT.MALFORMED). |
| T30 R1-15 | e9be7df | forma-workflow iframe sandboxed (`allow-scripts`, opaque origin). |
| T33 R2-10 | 57ba0ce | Token reprobe carries a generation. |
| BLOCKED classification | 560fb97, 9f85192 | A missing live secret gives BLOCKED, never FAIL or PASS; evidence-builder self-test. |
| F-PARTIAL-T30 (R1-06/07/13/17, R1-08/16 residuals) | 6e1259c, 15503a3 | Empty attempts never "applied"; strict UTF-8; correlation reuse refused. |
| F-G6, R2-02 tests | 2f12288, 116b841, 4c2dd55 | Regression tests. |
| Stale traceability gaps | 9f85192 | Re-mapped. |
| Accessibility (OD-2) | e7a0a8e | axe-core 4.13.0 audit. |
| R5-01 (blocker found by the R5 review) | 4134418 | An unreadable remote investigation is held, never saved over. |
| R5-02, R5-07, R5 over-claims | bfc0488 | Review evidence bound to the exact SHA (DCC-005); duplicated diagnostics publish block removed; over-claims recorded as gaps. |
| Evidence self-test | c0ee1b5 | Fixed a stale self-test that failed PR run 40575341449. |

The independent review R5 is `evidence/reviews/R5-findings-e7a0a8e.json`: a fresh agent with mutation runs. It reported 1 blocker, 2 majors and 4 minors. R2-05 was still OPEN; DCT-001-A and DCT-INPUT-A were OPEN.

Post-merge verdict: on `78a1b15` the authoritative verifier returned **INCOMPLETE** (exit 3). The figures:
- P0: 1 PASS, 0 FAIL, 107 NOT_RUN;
- gates: G0, G3, G8 and G9 PASS, the other six NOT_RUN;
- defect classes: 10 NOT_RUN, because no review exists of that SHA.

These records are kept off main, on `origin/remediation/r4-acceptance`:
- `986c671`;
- `evidence/acceptance/verdict-78a1b15….json`;
- `docs/rehearsal/R4-REMEDIATION-READINESS.md` (`711c0d9`).

The evidence comes from Build and test run 40576664266 and deploy 40577787824. The ledger records 216/216 checks passed.

### 1.4 CI runs (from repository records only; the Actions API was not readable)

| Source | Recorded figure |
|---|---|
| `checkpoints.json` T+60 (07:01Z) | "36 workflow runs since clock start (18 success, 10 failure, rest other workflows)" |
| `checkpoints.json` END (11:00Z) and the R4 report | "152 workflow runs since clock start; Build and test 38 (6 success, 25 failure, 7 cancelled)". For the other ~114 runs (Praxis validation, Echelon foundations, Deploy, wasm-diagnostics), **no per-conclusion breakdown was recorded**. |
| Candidate runs | Build and test 40490687398 (success) and Deploy 40491999050 (build, deploy, verify, release-candidate: success) for `ab08b76`. Evidence runs: 40486246734 (6931477), 40488432023 (5b48de2), 40492908619 (b6947b8), 40576664266 (78a1b15). |

Workflows: `build-and-test.yml`, `deploy-pages.yml`, `echelon-foundations.yml`, `praxis-validation.yml`, `wasm-diagnostics.yml`.

Distinct failure causes named in commits and checkpoints. This list cannot be checked against the logs.
1. **CI build failure, xunit nullness.** The wrapper did not build with nullness enabled (`55c6b82`).
2. **Test locator ambiguity.** Ambiguous test locators in the reasoning journey (Build and test at `ca52aae`; fixed in `bcb78d9`).
3. **Token exposure.** The live-suite Playwright traces contained the token (run at `6269dd3`). The token was rotated and the cause fixed in `64ae19b`. **[DEFECT]**, fixed.
4. **D-HIST-WASM.** `RuntimeError: memory access out of bounds` after about 900–1,000 replayed events. The investigation took about 8–10 CI cycles of about 12–15 min each, with commits `d37d874`..`6950158`. It also made the ab08b76 browser and deployed suites fail.
5. **`Environment.WorkingSet`.** Not supported in browser WASM; it broke every engine answer (`b7d409b`).
6. **Fake GitHub broken.** Editing tests by text replacement broke the fake GitHub once (R4 report, finding 6).
7. **Diagnostics workflow YAML.** A JavaScript `$'` replacement broke the YAML (`b440258`).
8. **Site publish.** `dotnet publish --no-incremental` is invalid and gave MSB1001 (`9d06f38`).
9. **R2-07 fault-injection test.** It overwrote linear memory and crashed Chromium ("Target crashed", PR run 40521224170; `39607d5`).
10. **Evidence self-test.** Stale after the R5 traceability update (PR run 40575341449; `c0ee1b5`).

Separately, a workflow on an `r5/preflight` branch got a 401 with the revoked token (run 40528892899). That is R5 preparation, outside this baseline.

**[INEFFICIENCY]** The R4 report states it directly: every push re-ran all four workflows, and Echelon foundations and Praxis validation re-ran unchanged. The whole browser suite ran to test one live scenario.

The checks recorded for candidate `ab08b76` (`acceptance-evidence:ab08b76…/results.json`): 160 found, 156 passed, 4 failed. The four failures are:
- `browser: D-HIST-WASM open defect …`;
- `ci: browser suite`;
- `deployed: an engine failure after start is announced … (T33 R2-07)`;
- `ci: deployed suite`.

### 1.5 Known defect: application code bypassing the persistence state machine

**Intended mechanism.** `src/causa/Sync.fs` holds `Sync.step : State -> Input -> Result<State, Fault>`. At `6950158` this is lines 110–143: illegal inputs return `fault`, for example `Publishing _, Edited -> fault "The investigation cannot change while it is being saved."`, and `| _ -> fault "This input is not legal in this state."`.

**Bypass at `6950158`** (`src/app/App.fs`), all **[DEFECT]**, confirmed by code reading:
1. **Faults swallowed.** `create` (line 579) and `addEvent` (line 543) call `Sync.step st Sync.Edited`, then do `| Some(Ok st) -> Some st | _ -> s.Sync` (lines 570, 596). A fault from the state machine is discarded and the mutation goes ahead anyway. `create` checked only `WorkspaceLoaded`, so an investigation could be created during Publishing, Acknowledged, Confirming, Unknown or Reconciling.
2. **Sync assigned directly.** The state is set without `Sync.step` in three places:
   - `finishWorkspace`: line 514, `Sync = Some(if dirty then Sync.Pending head else Sync.Clean head)`;
   - `reload`: line 651, `Sync = None`, accepted in any state;
   - the `WorkTree` empty-tree branch: line 953.
3. **Persisted counts overwritten.** The confirmation and reconcile-Applied handlers mark **every** owned investigation `Persisted = o.Events.Length` (lines 1029, 1050). That includes one created mid-save that was never sent, which then silently disappears on the next load. `localOnly` keeps only `Persisted = 0` (lines 501–503).

Evidence and review that recorded it:
- **T33 fresh review** of `6950158`: `evidence/reviews/T33-findings-6950158.json`.
  - DCT-002-B OPEN: "Sync.step faults on illegal inputs, but App bypasses it: Sync is assigned directly in finishWorkspace/WorkTree/reload (App.fs:514,651,953)…".
  - R2-01 (blocker, App.fs:579).
  - R2-02 (major, App.fs:773: reload/disconnect/replace-token accepted in every state).
  - R2-03, R2-04, R2-05.
  - Traceability over-claim AR-006: "O2 'invalid transitions rejected before side effects' is violated at the App level".
- **R4 report**, finding 6 and lesson 3: "Treating the state-machine fix as the whole persistence fix left the application-level paths unguarded (T33 R2-01 to R2-05)". The same lesson appears as lesson 3 in `Indy-init/docs/REHEARSAL-004-RESULTS.md`.
- **Partial fix in the run.** `8e03f40` added guards before the step call. At `ab08b76` `create` refuses Publishing, Acknowledged, Confirming, Unknown, Reconciling and Conflict (App.fs:600–607).
- **R5 review** of `e7a0a8e`:
  - DCT-002-B is only PARTIAL: "reload is accepted with Sync=None and in all settled states (App.fs:714-723); disconnect in Pending, Conflict or Refused discards work … The core enumeration covers Sync.step only".
  - R5-01 (blocker) showed the same class again: unreadable remote copies were overwritten.
  - R5-06 (reload during a load).

**Residual at HEAD `78a1b15`: [ARCH?].** The fault-swallowing pattern `| _ -> s.Sync` is still at App.fs:657 and :687. Direct `Sync =` assignments are still at App.fs:594, 742 and 1094. The engine-side state machine is not the single authority: legality depends on hand-written guards placed before the step call. The readiness record still lists R2-05, R2-08 (partial) / R5-03 and R5-06 open.

### 1.6 Acceptance results and verification-coverage claims

| Candidate | Verdict | P0 PASS/FAIL/NOT_RUN | Gates | Defect classes | Source |
|---|---|---|---|---|---|
| 6931477 (run 40486246734) | FAILED | 2/9/97 | | | `evidence/acceptance/*-6931477…json` |
| 5b48de2 (run 40488432023) | INVALID | 1/9/98 | | | `evidence/acceptance/*-5b48de2…json` |
| **ab08b76** (run 40490687398) | **FAILED** (118 reasons) | **1/9/98** | G0, G3, G9 PASS; G5, G8 FAIL; G1, G2, G4, G6, G7 NOT_RUN | 6 PASS; DCT-001-A, DCT-002-A, DCT-002-B, DCT-INPUT-A NOT_RUN | ledger and verdict for ab08b76 |
| 78a1b15 (run 40576664266) | INCOMPLETE (123 reasons) | 1/0/107 | G0, G3, G8, G9 PASS | 10 NOT_RUN | `remediation/r4-acceptance` |

All nine FAILs at ab08b76 come from the same thing: each maps `ci: browser suite`, which failed only because of the D-HIST-WASM expected-failure test. The nine are PF-005, PF-011, PF-012, PF-017, VER-003, VER-006, AR-010, AR-013 and AR-022 (`R4-REMEDIATION-PLAN.md`).

The remediation ledger (`remediation-ledger-ab08b76.json`) classifies the findings:
- 115 MISSING_VERIFICATION, 85 ACCEPTANCE_INFRASTRUCTURE (37 of them stale mappings), 35 IMPLEMENTATION_DEFECT, 10 RUNTIME_DEFECT, 6 DEPENDENCY_DEFECT, 4 REQUIREMENT_GAP;
- by severity: 26 CRITICAL, 27 HIGH, 202 MEDIUM.

Coverage claims in the in-run traceability:
- T+120 checkpoint draft at bd469c8: "5 of 108 P0 fully covered, 89 partial, 14 none".
- `traceability.json` at ab08b76: coverage `full` 2, `partial` 92, `none` 14.
- At HEAD: `full` 1, `partial` 94, `none` 13.

**Tests mapped to obligations they did not prove: [DEFECT], all confirmed by the reviews.**
- **The one P0 PASS at ab08b76, EXT-DATA-002.** It rested on one test, `browser: first launch … no request to GitHub`. R5 recorded: "EXT-DATA-002-O2 no implicit demo import or automatic fallback … The single mapped test checks a fresh launch only. It does not exercise the fallback cases." At 78a1b15 the requirement passes with four checks, still mapped at requirement level only.
- **T33 `traceabilityOverclaims`** (12 entries). Examples:
  - G4: the 90-fixture check is vacuous; 88 of 90 are refused for an unsupported event type (R2-06).
  - G2: the Boundary A test edits the hidden input by script (R2-08).
  - AR-006 mapped `full` while App-level violations exist.
  - EXT-DATA-008 is circular (the reader recomputes with the same `Store.files`).
  - AR-015 rests on `document:` refs that pass by file existence.
  - DCT-001-A tests only the never-saved path.
  - G1: `ci: site build` is synthesized from any browser pass.
- **T33 `staleMappings`** (8): gaps claim missing checks that already exist, for example the DCT-004-A renewal, the EXT-DATA-013 active.json read, and the UI-001 sort.
- **R5 `traceabilityOverclaims`** (7): AR-010 O1/O2, PF-005 O1/O2, AR-008 (document refs; reviews not of the candidate SHA), EXT-DATA-013-O2, EXT-DATA-011-O1 (circular writer conformance), EXT-DATA-002-O2.
- **R5-04 (major), tests that cannot fail, used as closure evidence:**
  - the G6/R1-01 test passes with the fix removed (mutation A1);
  - the DCT-INPUT-A test passes 5/5 with `data-on` removed;
  - the EXT-DATA-013-O2 assertion passes against ab08b76, which lacks the feature;
  - the R1-14 and R1-10 fixes have no test that fails without them (A11, A2).
- **R2-12 and R5-02: the evidence builder over-credits.**
  - Before the fix, the review file's *existence* counted as defect-class evidence.
  - Before `bfc0488`, the review was not bound to the source SHA, so a review of `6950158` closed classes at later commits.

**[DEFECT] Mapping granularity in R4 traceability.** Mapping is per requirement, with free-text check titles. Obligation ids appear only inside gap strings ("gap: O5: …"). No structured obligation→test record exists, even at HEAD. The remediation commit `bfc0488` says "EXT-DATA-002-O2 maps the tests that exercise it". In `acceptance/traceability.json` this shows up only as more `checks` on the EXT-DATA-002 entry; the entry has keys `id, priority, title, coverage, checks, gap` and nothing else.

### 1.7 Milestones

| Milestone | Determinable? | Value |
|---|---|---|
| First working app (deployed) | Partly | The T+60 checkpoint records "Pages deployment with deployed-SHA verification" and "deployed-SHA check passed for 9da6be7". Commit 9da6be7 is 02:38:22 EDT = T+33.7 min by commit time. The deploy run's completion time is not recorded, and the API was not readable. |
| First successful persistence (live create/write/reload/conflict/rejected token green in CI) | Yes, as recorded | **T+36 min** (`Indy-init/docs/REHEARSAL-004-RESULTS.md`, "green in CI at T+36 minutes"). Checkpoint T+60: live write path green in CI at 9da6be7 and 6269dd3. Two-hour gate: met. The run ID for the T+36 green run is not recorded. |
| Independent acceptance | Never achieved | The R4 verdict FAILED at T+296 (11:00:31Z). Post-remediation, INCOMPLETE at 78a1b15 (about 20:33Z). |

### 1.8 Token and tool-use telemetry (Praxis/ROS execution records)

`.ros/telemetry/executions/` holds 4 records. Each record lists 138 metric capabilities.

| Execution | Work item | Status | Metrics actually recorded |
|---|---|---|---|
| EXE-20261027T062656522Z-a4c22312 | COND-MISSION-001 | `active`, never finalized. Started 06:26:56Z at `5389d65` with 919 dirty paths. | `git.baseline_dirty_files=919` only |
| EXE-20261027T195125303Z-7d99e0bd | WI-0001 | `active` (never finalized) | `git.baseline_dirty_files=9` |
| EXE-20261027T195125395Z-f1ec861b | WI-0004 | `active` (never finalized) | `git.baseline_dirty_files=9` |
| EXE-20261027T195125459Z-5ca6d0ed | WI-0005 | finalized 20:08:12Z | `time.wall_ms=1006842`, `time.blocked_ms=0`, `git.ending_dirty_files=0`. `changeSummary.available=false` (`preexisting-dirty-worktree`). |

- **Token metrics.** `tokens.input` and the other token metrics are `supported-unavailable` in the first record ("no observation has been ingested") and `unknown` in the remediation records. **No token count exists.**
- **Tool metrics.** `tool.calls`, `tool.failures` and `tool.duration_ms` are `supported-unavailable` or `unknown`. **No tool-call count exists.**
- **The WI-0005 wall time is not the review's duration.** WI-0005 is the R5 review. Its execution started at 19:51:25Z, about 11 min after the review commit (`c5d9941`, 19:40Z). So the 1,006,842 ms (16.8 min) measures post-hoc bookkeeping, not the review. **[DEFECT]**, in telemetry fidelity.
- **Checkpoints.** They say "Not measurable precisely" or "Not measurable". The harness budget figures (14.31M, 14.02M, 14.28M remaining) are explicitly "not a consumption measure".
- **R4 report.** "Total tokens: Not measured."
- **`telemetry/metrics.json`** is only a metric catalog.

**[INEFFICIENCY]** The Praxis/ROS telemetry was installed (ROS 3.21.0) but did not capture any token or tool use. The remediation work items were opened after the work was done ("post-hoc reconciliation"). Token and tool use for R4 are therefore **unavailable**. No estimate is given.

---

## Part 2: Indy-init requirements compiler inventory (Workstream C prep)

Indy-init HEAD: `459f65a`. `dotnet` is not installed in this session, so the F# compiler was **not built or run**. Only the Python baseline generator was run (section 1.1).

### 2.1 What is where

| Path | Language | Contents |
|---|---|---|
| `tools/requirements-compiler/src/` | F# (net10.0, SDK 10.0.402 pinned by `global.json`, TreatWarningsAsErrors, Deterministic) | 14 files, about 3,500 lines: Json, Git, Text, Baseline (F# port of the baseline generator), Model, Inventory, Normalize, Graph, Artifacts, Validate (V01–V17), VerificationPlan (Phase Two), PlanVerification (Phase Eight), Preflight, Program. Assembly `requirements-compiler`. |
| `tools/requirements-compiler/tests/` | F# | `Tests.fs` (393 lines): offline mode, full mode with 19 injected defects, verification-plan tests. `Replay.fs` (241 lines): R4 replay with negative controls. |
| `tools/*.py` | Python 3.12 | `build-requirements-baseline.py` (the baseline generator, `praxis.requirements-baseline/1`); `compute-acceptance-verdict.py` (the **authoritative** verifier); `validate-acceptance-ledger.py`; `compile-agent-context.py`; `discover-echelon-stack.py`; tests `test-preflight.py`, `test-acceptance-ledger.py`, `test-acceptance-verdict.py`, `test-requirements-baseline.py`. |
| `tools/persistence-verification`, `tools/praxis-preservation-verification` | F# | T05 persistence verifier (built against the rehearsal-002 adapter); Praxis preservation verification. |
| `requirements/` | JSON + MD | `causa-requirements-baseline.json` (461 requirements, 2,293 obligations); README with the mechanical rules. |
| `requirements-compiled/v1/` | JSON + MD | The compiled package (listed in 2.3), `curation/` (hand-written inputs), `readiness/` (reports and evidence). |
| `docs/` | MD | About 80 docs. Normative ones are pinned in the baseline (19 docs). Process: `REHEARSAL-DERIVATION-POLICY.md`, `VERIFICATION-PLAN-CONTRACT.md`, `REHEARSAL-004-RESULTS.md`, `REHEARSAL-ACCEPTANCE-LEDGER.md`, `DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md`. |
| `kickoff/` | JSON | The kickoff manifest plus a schema, and `rehearsal-004/` and `rehearsal-005/` operator records. |

**Entry points** (`src/Program.fs:606-624`):
- `requirements-compiler [--source --committed-baseline --curation --out --repo NAME=DIR… --check]`: full compile. Exit 0 PASS, 3 FAIL, 2 bad input, 4 `--check` difference.
- `requirements-compiler preflight … --evidence FILE`: exit 0 READY, 3 otherwise.
- `requirements-compiler baseline ROOT COMMIT`
- `requirements-compiler verify-plan --plan --baseline --ledger --evidence-manifest --evidence-manifest-sha256 --evidence-root --trusted-source-sha --approvals --trusted-operator-id [--out]`: lines 552–604. Exit 0 only for ACCEPTED.
- Test project: `dotnet run --project tools/requirements-compiler/tests -- --offline --source .`, the full mode, or `--replay`.

**Build:** `dotnet build tools/requirements-compiler/tests -c Release`.

**CI** (`.github/workflows/preflight-tooling.yml`):
- Job `offline-contract-tests`: the four Python tests and `compileall`.
- Job `fsharp-tooling`:
  - build;
  - offline tests;
  - a byte-for-byte `cmp` of the Python and F# baselines at `$GITHUB_SHA`;
  - build of the T05 verifier against `indy-init-rehearsal-002@b053d1da`.
- The full compile and the full test mode need 14 sibling checkouts and are **not run in CI** (v1 README "Limits").
- `offline-factory-rehearsal.yml` is a separate Conditor offline-bundle workflow.

### 2.2 How baselines are frozen and derived

- **Generation.** `python3 -I tools/build-requirements-baseline.py . <commit>` reads documents via `git show` and is deterministic. The F# port is `Baseline.fs`; CI asserts the two outputs are identical.
- **Obligations.** Each verbatim sentence or table row is one obligation, with ids `<REQ>-O<n>`. These are the ids seen in rehearsal commits, for example `UI-015-O3` (UI-015 has O1–O4) and `PF-011-O5` (PF-011 has O1–O6). Each obligation has a kind (`acceptance` / `value` / `constraint`) and exact value `tokens` (`requirements/README.md`, "Mechanical rules").
- **Freeze.** The baseline is re-pinned at each kickoff (T01, OD-INDY-6). The preflight record pins the baseline id, source commit and sha256, plus the compiled-package manifest digest (`kickoff/rehearsal-00N/preflight-record.json`).
- **Derivation policy.** RDP-001..005 are in `docs/REHEARSAL-DERIVATION-POLICY.md`:
  - derive from the pinned baseline;
  - give every requirement a disposition (`praxis.requirements-disposition/1`);
  - the operator writes approvals;
  - the preservation gate (`praxis requirements preservation verify`) passes before implementation and again at acceptance;
  - missing verification is never PASS.
- **R4 derivation.** Branch `preflight/t07-derivation` at `3a5248d`, made with `tools/derive-requirements` (F#), produced 461 requirements: 456 preserved, 5 exclusion requests, 750 review requests. The record is `research/requirements-preservation/derivation-record.json`. The gate PASSed (exit 0: 461 requirements, 2,293 obligations, 5 exclusions, 0 fail/drift/needs-review), recorded in `evidence/requirements-preservation/gate-pre-implementation.json` with its command. The gate was re-run at `4fd267a`.
- **Not run in CI. [INEFFICIENCY]** The preservation gate ran locally, never as a CI step (R4 report and RESULTS doc).
- **"R1 commit".** The term does not occur in Indy-init or R4 `*.md`/`*.json` files; nothing was found for it.

### 2.3 What the compiler outputs today (`requirements-compiled/v1`)

The schemas are named in-file. The P0 verification plan is the artifact relevant to Workstream C.

- `requirements-inventory.json`: 466 requirements (the baseline plus 6 causa-data catalog sections), verbatim, with modality, flags, dependencies and status.
- `normalized-requirements.json` and `requirements-source-map.json`.
- `decision-registry.json`, `contract-bindings.json` (18), `dependency-graph.json` (35 work items).
- `verification-manifest.json`: one entry per requirement, with gates, rules (`method`, `evidence`, `freshness`, `invalidatedBy`, `tooling`), acceptance links and validity dependencies. The rules come from `curation/verification-rules.json` (14 rules, VR-LEDGER … VR-CATALOG, each tied to a gate G0–G10).
- **`verification-plan.json` (`indy-init.verification-plan/1`, schema `verification-plan.schema.json`, contract `docs/VERIFICATION-PLAN-CONTRACT.md`).** It covers 108 P0 requirements and 326 obligations. Each obligation carries `id` (`VPO-<16 hex>`), `sourceObligation` (for example `AR-001-O1`), `index`, `kind`, verbatim `text`, `textSha256` and `testIdentity {status, tests[{id, kind}]}`. Each requirement carries `priority`, `title`, `document`, `sectionSha256`, `acceptance`, `verificationMethod {gates, rules[]}`, a requirement-level `testIdentity` (bound/partial/missing counts), `evidenceContract {subjectType: requirement, requiredEvidence[] (free text)}`, `dependencies {requirements, contracts}`, `freshness {policy, boundTo, invalidatedBy}` and `exclusion {status, approval}`. The plan also has `planIdentity` (a content digest), `statusVocabulary` (PASS, FAIL, BLOCKED, NOT_RUN, MISSING, UNBOUND, STALE, INVALID, EXCLUDED) and 3 freshness policies.
- **Current state of the plan:** `boundObligations: 0`, `missingTestObligations: 326`. `curation/verification-plan.json` has `"testBindings": []` and `"exclusions": []`.
- Others: `execution-plan-time.json`, `execution-plan-resource.json`, `preflight-manifest.json`, `timed-execution-manifest.json`, `requirements-validation-report.json` (V01–V18), `package-manifest.json`, `percepta-experiment.json`, `readiness/`.
- `readiness/evidence/r4-verification-plan-replay.json`: R4's ab08b76 evidence replayed through both verifiers, plus 6 negative controls. All were as expected. The plan assessment gives FAILED, with EXT-DATA-002 **UNBOUND**, not PASS (`PlanVerification.fs:154`).

Answers to the questions about the current compiler:
- **Obligations:** yes, baseline obligation ids are carried through, plus stable `VPO-` ids.
- **Verification method:** yes, at requirement level (gates and rules); it is not chosen per obligation.
- **Evidence categories:** only three test *kinds* (`live`, `deterministic`, `review`), plus free-text `requiredEvidence`.
- **Test mappings:** the field exists; it is empty for all 326 obligations.
- **Freshness:** yes; 3 policies, and the strictest one wins.
- **Status:** yes, as a vocabulary, with per-obligation status in the assessment.
- **Missing coverage:** yes, as `testIdentity.status=missing` plus counts.

### 2.4 Gaps against the Workstream C target

The table compares each target per-obligation field with the current state.

| Target field / check | State | Where (file:line) |
|---|---|---|
| Requirement id | EXISTS | `VerificationPlan.fs:157` (`requirement`) |
| Obligation id | EXISTS: `VPO-` stable id from (requirement id, source obligation id, text), plus `sourceObligation` | `VerificationPlan.fs:39-41, 139-140` |
| Priority | Requirement level only (P0, since the plan is P0-only); not repeated on the obligation | `VerificationPlan.fs:158`; P0 filter `:46-47` |
| Verification method | **Requirement level only** (gates and rules from the manifest). No per-obligation method. | `VerificationPlan.fs:164-176` |
| Required evidence categories (unit, integration, browser, live-provider, security, accessibility, independent acceptance; possibly several) | **MISSING.** Only `testKinds = ["live"; "deterministic"; "review"]`, which applies to bound tests and drives freshness. `requiredEvidence` is requirement-level free text, merged from the rules. There is no enumerated category set, no per-obligation required-category list, and no check that the bound tests' categories cover the required ones. | `VerificationPlan.fs:43, 182-186`; `Model.fs` `VerificationRule.Evidence` |
| Test identity | EXISTS structurally (`testIdentity.tests[{id, kind}]`); **0 of 326 bound** | `VerificationPlan.fs:145-148`; `curation/verification-plan.json` `testBindings: []` |
| Dependencies | **Requirement level only** (`requirements`, `contracts`); no obligation-to-obligation or obligation-to-test dependencies | `VerificationPlan.fs:187` |
| Freshness requirements | EXISTS (policy per requirement, `boundTo`, `invalidatedBy`, 3 policies). Not per obligation. Reuse under FP-DETERMINISTIC is always treated as STALE, because no Dokimos attestation carrier exists. | `VerificationPlan.fs:107-119, 188-192`; `PlanVerification.fs:70-76` |
| Verification status | EXISTS in the assessment (requirement status, derived obligation status) | `PlanVerification.fs:138-171` |
| Missing coverage | EXISTS (`missing`/`partial`, `missingTestObligations`) | `VerificationPlan.fs:150-151, 177-181, 254` |
| **Checker rejects missing obligations** | EXISTS for the plan: the obligation list must equal the baseline's, even when re-sealed. Tested. | `VerificationPlan.fs:306-308`; `Tests.fs` preservation block |
| **Checker rejects incorrect test mappings** | **MISSING.** (a) Bindings are checked only for an unknown obligation, an empty list, a duplicate or a bad kind (`VerificationPlan.fs:95-102`). Nothing establishes that a bound test exercises its obligation; no mutation or negative-control link exists. (b) `PlanVerification.assess` **never reads the ledger row's `tests`**; it reads only `evidence_paths` (`:152`). A ledger whose tests differ from the plan's bindings is not rejected. (c) The authoritative Python verifier has no obligation concept: `compute-acceptance-verdict.py` has no "obligation" occurrences, and PASS needs only non-empty `tests` and `evidence_paths`. R4's EXT-DATA-002 PASS (a one-test mapping that did not prove O2) is the in-the-wild case. | `VerificationPlan.fs:95-102`; `PlanVerification.fs:144-158`; `tools/compute-acceptance-verdict.py` |
| **Checker rejects altered requirement values** | PARTIAL. Priority change (`:305`), obligation text change (`:308`, and `textSha256` at `:312`) and ledger priority ≠ P0 (`PlanVerification.fs:146`) are detected. **Not compared against the baseline:** `title`, `sectionSha256`, `document`, obligation `kind`, and obligation `tokens`. Tokens are not carried into the plan at all. | `VerificationPlan.fs:293-319` |
| **Checker rejects swapped requirement identities** | IMPLICITLY yes in the plan: obligations are compared as (id, text) per requirement (`:306-308`), and `VPO` ids embed the requirement id (`:311`). At evidence level, `subject_id` must equal the requirement (`PlanVerification.fs:68`). **No dedicated test** swaps two requirements' content; ledger rows are keyed by id only. | `VerificationPlan.fs:306-311`; `PlanVerification.fs:68` |
| **Checker rejects stale evidence** | EXISTS: `source_sha` and `run_id` must match the candidate (`:70`); manifest run and source are checked (`:114-115`). Tested in the replay (cases stale-evidence-repinned and stale-manifest). Evidence is per requirement, not per obligation or per test. | `PlanVerification.fs:52-81, 105-121` |
| **Checker rejects unauthorized exclusions** | EXISTS in the plan (V18, `VerificationPlan.fs:91-94`) and the ledger (`PlanVerification.fs:116-121`). Tested (`Tests.fs` exclusions block; replay case unauthorized-exclusion). | as cited |
| Obligation-level evidence | **MISSING.** `evidenceContract.subjectType` is always `"requirement"`; obligation status is copied from the requirement (`PlanVerification.fs:161-164`). | `VerificationPlan.fs:182-185` |

### 2.5 Findings on the compiler and process

1. **[DEFECT] The authoritative acceptance path does not check obligation coverage.** `compute-acceptance-verdict.py` and the R4 `traceability.json` work at requirement level, with free-text check titles. That allowed R4's single P0 PASS (EXT-DATA-002) on a test that did not prove O2. Reviews found 12 + 7 traceability over-claims and 8 stale mappings (section 1.6).
2. **[DEFECT] The verification plan carries no test bindings.** `testBindings: []`, so 326/326 obligations are `missing`. Under `PlanVerification`, no candidate can reach PASS: every evidenced requirement becomes UNBOUND. This blocks false PASSes, but it also means the plan has never been exercised with real bindings.
3. **[DEFECT] The compiled package changed in place after the R4 freeze, and no `v2/` was cut.**
   - The README rule is "Publish the frozen kickoff state as `v2/`" (v1 README step 4).
   - The R4 freeze pinned `package-manifest.json` at `0cd4c3f5…`. HEAD's is `b771cd40…`.
   - It changed in `5d69cca`, `f9219d1` and `4b38213` (adding `verification-plan.json` and re-binding the approvals to `kickoff/rehearsal-005`, commit `295bc64`).
   - `docs/REHEARSAL-004-RESULTS.md` (`26d7d20`, written before these changes) still says v1 "is unchanged since the kickoff" and "No `v2/` was published; the next package change starts one". Both statements are now false at HEAD. The R4-frozen package can be recovered only from git history (`ad2f1af`).
4. **[INEFFICIENCY] The R4 replay evidence is bound to a superseded plan identity.** `readiness/evidence/r4-verification-plan-replay.json` records plan `VP-d1efaf695ef4`. The committed plan is now `VP-e77a2134e1b0`; its digest changed when the approvals pin moved to R5. The replay was not regenerated.
5. **[ARCH?] Evidence categories exist only as free-text rule prose.** There is no machine-checkable category vocabulary, and a requirement's gate set cannot be shown to cover browser, security or accessibility obligations individually. The plan also stores no `tokens`, so the RDP-002 "exact values" rule cannot be checked at verification time.
6. **[INEFFICIENCY] Most of the compiler is not exercised in CI.** The full compile, V01–V18 and the 19 injected defects need 14 local checkouts, and CI runs only the offline subset. `dotnet` was unavailable in this session, so nothing in the F# code was executed or re-verified here.
7. **[ARCH?] Two verifiers with diverging semantics.** The Python verifier is authoritative and requirement-level. The F# `verify-plan` complements it at obligation level ("never replaces", `PlanVerification.fs:4`). A Workstream C checker needs to state which one decides.

## Not determined (stated, not estimated)

- Per-workflow CI conclusion counts beyond Build and test, the exact run IDs for the first green live write and the first Pages deploy, and CI failure logs. The Actions API was not accessible (section 1.4).
- Token use, tool-call counts and cost, for both the rehearsal and the remediation. None was recorded (section 1.8).
- Whether the F# compiler builds and its tests pass at HEAD. Not run; no dotnet.
