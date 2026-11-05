# EXP-INDY-002 final report: contract-first F#, run as contract-triggered parallel development

**Verdict: ADOPT WITH CONDITIONS** (conditions in section 10).

This was one short run (n = 1) on a 39-requirement slice. It used Limen Storage, not GitHub, as the persistence boundary, and the verification is the experiment's own, not the governed rehearsal gates. Treat the numbers as a demonstrated mechanism with directional magnitudes, not as a precise prediction.

Experimental application code lives only in `experiments/exp-indy-002/`. It copies nothing from `preview/causa` or earlier rehearsals, and it must not be merged into any competition implementation.

## 1. Timeline (UTC, measured)

| T+ | Time | Event |
|---|---|---|
| 0:00 | 21:15:04 | Timed run starts. Phase A: Contracts.fs and the registry are written. |
| 2:36 | 21:17:40 | **Owner's protocol change** (asynchronous contract-driven). The clock is not reset (DEV-1). |
| 3:08 | 21:18:12 | contract-check catches a registry defect (INV-040) before release; fixed 17 s later. |
| ~3:45 | 21:18:50 | **CG-1 released.** Domain and UI agents dispatched to isolated worktrees. |
| ~5:35 | 21:20:40 | CG-2 to CG-5 validated and released, one after another. |
| 5:49 / 6:02 | 21:20:53 / 21:21:06 | First domain code / first UI work, **2.1 min** after the first release. |
| 7:28 | 21:22:32 | Domain CG-1 complete with 14 unit tests. |
| ~9:00 | ~21:24 | CR-1 (control accessible names), raised by the verification agent. |
| ~11:00 | ~21:26 | CR-2 (relationship picker labels). |
| 14:36 | 21:29:40 | Domain wave 1 complete (58 tests). It files a contract-change request on SM-SAVE. |
| 15:52 | 21:30:56 | CR-3, an F# type change (`ReadBackMatched of version * at`). |
| 16:30 | 21:31:34 | UI wave 1 complete: 3 screens, 16 tests, static binding check PASS. |
| 17:43 | 21:32:47 | Both agents finish the CR-3 rework (50 s and 60 s). |
| 18:26 | 21:33:30 | **Integration INT-1 starts** as soon as both sides are staged (DEV-2: not held to T+60). |
| 20:05 | 21:35:09 | **First working UI** in a browser on the real engine. |
| 20:16 | 21:35:20 | The independent browser test finds **D-3**, a real UI/contract mismatch. |
| 21:37 | 21:36:41 | D-3 repaired by its owner (UI agent, 60 s). |
| **23:01** | **21:38:05** | **First accepted end-to-end workflow.** 37 requirements; all local suites green. |
| 24:45 | 21:39:49 | **Wave 2: CG-6 (conclusions) released** into existing code. |
| 25:09 | 21:40:13 | Both agents start CG-6, 24 s after release. |
| 28:17 | 21:43:21 | **INT-2 accepted.** 39 requirements, 0 integration defects. |
| 28:45 | 21:43:49 | Feature development stops. Final candidate (docs-only successor of `76d88cd`; identical application code) is pushed for required CI. |
| — | — | Required CI on the final candidate: see section 5. |

The directive's T+55 and T+75 reporting points fell after development had already finished. Both are reported here, against the shifted schedule.

## 2. Slice, contracts and lifecycle

**Slice.** 39 ratified `CAT-CAUSA-INVESTIGATION` requirements, all listed in REQUIREMENT-MATRIX.json:
- start, edit and cancel an investigation, with validation INV-019 to INV-021;
- observations, hypotheses and evidence;
- evidence–hypothesis relationships, including the Current → Revised / Withdrawn transition;
- conclusions;
- save with read-back, the seven-state indicator, reopen and restore;
- the list screen and opening an investigation.

**Excluded:** INV-003, INV-037 (needs real GitHub; not authorized), and INV-041 to INV-043 (judged only by owner journeys).

**Contract registry.** Every user-editable field has a typed binding (`Record.Property : type`). Every action maps to a `Command` or `UiAction` case. `contract-check.fsx` verifies this mechanically, using reflection over the compiled `Exp002.Contracts.dll`:
- C1: catalog coverage;
- C2: binding existence and type;
- C3: command and UI-action existence, and every Command is bound;
- C4: test IDs present;
- C5: no untyped editable field.

**Lifecycle.** Six release groups (CG-1 to CG-6). Every transition is in CONTRACT-LIFECYCLE.jsonl, with a version and a SHA-256 hash over Contracts.fs plus the group's registry entries.

**VERIFIED** is recorded only after required CI passes on the final candidate.

## 3. Answers to the ten questions

**1. How quickly did released contracts produce code?**
- Wave 1: domain code started 2.1 min after the first release, and the first domain group was unit-tested 3.7 min after release.
- Wave 2: both agents started 24 s after release. Both staged within 2.3 min. The integrated build was accepted 3.5 min after release.

**2. How much idle time was eliminated?**
- Neither worker was ever blocked waiting for a release or for the other agent (measured idle: 0).
- The UI agent built all three screens without any domain code, against `EnginePorts` and an isolated stub.
- In the sequential model, the UI agent would wait for the whole domain implementation, about 9.7 min of measured domain work.
- Between assignments the agents were finished and consumed no compute: domain about 9.7 min, UI about 6.2 min.

**3. How many contracts were revised?**
- Three revisions:
  - CR-1: accessible names for secondary controls.
  - CR-2: picker and option labels.
  - CR-3: an F# type change to SM-SAVE.
- Together they touched 10 of the 39 bound contracts.
- The UI agent worked around 5 more mismatches without a revision, recorded in `agents/ui-timeline.jsonl`:
  - picker field IDs;
  - withdraw reason source;
  - no "opened from storage" save event;
  - no index codec;
  - no randomness source for the UUID prefill.
- **Contract accuracy: 29 of 39 bindings (74%) needed no revision.** Counting the unrevised workarounds, 27 of 39 (69%) needed neither. All 2 wave-2 bindings survived unchanged.
- Every revision was found before integration: 2 by the verification agent while it wrote the independent test, 1 by the domain agent.
- None was a domain-model rewrite. Each could have been caught in Phase A by a stricter registry schema (labels for every control) or by a state-machine test specification.

**4. How much rework did integration cost?**
- Integration defects: D-1 (test-ID convention between the agents), D-2 (my Playwright import), D-3 (the UI auto-saved on Start, against INV-004's contract).
- 3 min of repair in a 4.6 min integration.
- Contract-revision rework: about 2.7 agent-minutes (domain 0.8, UI 2.0). The additive wave-2 contract caused about 1.5 agent-minutes of compile repair (47 s and 44 s), all of it non-exhaustive matches and a missing record field. The compiler pinpointed every site.
- **Integration repair ratio: 3.0 min / 28.3 min to the final accepted candidate = 10.6%** (3.3% of the 90-minute box). No merge conflicts: file ownership held.

**5. How many requirements and field IDs were verified?**
- **39 of 39 selected requirements**, including all 17 field contracts (15 field entries, among them INV-031 and INV-038, plus the 2 INV-030 picker bindings), were verified by unit tests and by the independent browser acceptance test.
- The verification agent wrote that test from the registry alone (labels and requirement IDs), never from the implementers' markup.
- This is experiment-level, independent-of-the-implementer verification. It is **not** governed `catalog-acceptance` or journey acceptance (section 7).

**6. Was there a working end-to-end workflow?**
- **Yes.** In a real browser on the integrated WebAssembly build: start → observe → hypothesize → admit evidence (unsafe URL refused) → link with a required kind and rationale (duplicate refused) → save with read-back → edit details → reassess (history kept) → conclusion → reload and reopen from the list, with every entered value restored.
- 22 acceptance checks pass, including two 375×812 mobile checks and no console errors.

**7. Did delayed full CI hide defects too long?**
- No defect in this run was CI-detectable that the local checks had missed. The only CI "failure" was a superseded run cancelled by my own pushes (CI-F1).
- The latency risk is real, but it sits elsewhere:
  - D-3 was invisible to every check the implementing agent ran (unit tests and the static binding check). Only the browser test caught it.
  - That favors running the independent browser test at each integration, not only at the end.
- Required CI on the final candidate is reported in section 5.

**8. How long would the sequential strategy take on the same task durations? (Modeled, not measured.)**
- Contracts to full release: 5.6 min. Domain wave 1 plus CR-3: 9.7. UI wave 1 plus revisions: 11.5. Integration and repair: 4.6. First accepted workflow at about **T+31.4**, against the observed **T+23.0**.
- Wave 2: about 5.1 min sequential against the observed 3.5.
- Modeled saving: **about 8.4 min (27%) to the first accepted workflow.** It is larger than the measured integration rework (3.0 min) and the contract-revision rework (2.7 agent-min) combined.
- The model is generous to sequential on one point: done sequentially, CR-3 would have been found before the UI began, saving the UI 1 min.
- It is unfair to sequential on another: a sequential UI agent could build against the real domain and skip the stub. The stub is part of UI time and costs about 1–2 min.
- The milestone-first comparison (schedule C) is in section 6.

**9. What is the four-hour competition forecast?** See section 8.

**10. Should this strategy replace the current execution plan?** Adopt it with conditions (section 10). A controlled comparison against milestone-first on the same slice is still needed before it fully replaces the plan.

## 4. Primary metrics

| Metric | Value | Basis |
|---|---|---|
| Verified implementation throughput | 39 req / 28.3 min = **1.38 req/min** (0.43 req/min over the 90-min box) | measured; experiment-level verification |
| F# implementation throughput | 1,603 app F# lines / 22.5 wall-min = **71 lines/min**. 2,508 lines with unit tests (111/min). Plus 331 lines of F# tooling. | measured (line counts include blanks and comments) |
| Functions | about 86 top-level/nested `let` functions in the engine and 75 top-level in the UI. All engine functions are exercised by 67 unit tests; none are incomplete. | measured by grep, approximate |
| Contract accuracy | 29/39 = **74%** without revision (69% counting workarounds) | measured |
| Integration repair ratio | 3.0 / 28.3 = **10.6%** | measured |
| Compute efficiency | 812 CPU-s (T+12..T+28) / 39 = **20.8 CPU-s per requirement**. Worker tokens 383,729 / 39 = **9,839 per requirement**. | measured; coordinator tokens unavailable |
| Time to first working UI | **T+20.1** (browser, real engine) | measured |
| Time to first accepted E2E workflow | **T+23.0** | measured |
| Compile failures | domain 2 (11 s and 47 s to repair), UI 1 (44 s); the Wasm build waiting on the engine was expected | measured |
| Merge conflicts | 0 across 6 agent-branch merges | measured |

## 5. Required CI

PR [kemiller2002/Indy-init#140](https://github.com/kemiller2002/Indy-init/pull/140) (draft) runs **Preflight tooling** and the **offline factory rehearsal** on the candidate. Every run is listed in CI-RUNS.json.

- On earlier heads, the offline factory passed in about 1.5 min.
- Preflight runs on superseded heads were cancelled by newer pushes (CI-F1, CI-F2).

**Final candidate (docs-only successor of `76d88cd`; identical application code):** CI_RESULT_PLACEHOLDER

## 6. Comparison with milestone-first (schedule C)

Historical evidence, from the cited documents:
- R5: first live write at T+24m11s, 77 of 108 P0 passing in about 394 min, 7 conflicted merges.
- R6: first deployed slice at T+128, and no main CI result for 124 min.
- R8: 266 of 613 P0 obligations verified. Its INV-001/002/007/008 failed because the event payload had no body.
- R10's milestone-first plan: modeled only (M3 "create" at T+489 with 10-min CI).

None of these is the same scope (full application, real GitHub, 420 minutes, 5–7 streams). So **no controlled comparison is possible from history.**

Qualitatively, this run avoided the two failure modes those reports name:
- **Merge conflicts in shared adapter, engine-port and view files.** EnginePorts as the only seam and strict file ownership gave 0 conflicts.
- **Contract bodies missing from events (DEV-R8-09).** The registry forced full event bodies, and the save–reload test proves them.

| Dimension | EXP-002 (measured) | Milestone-first (historical) |
|---|---|---|
| Early F# throughput | first tested domain group at T+7.5 | not recorded |
| Contract stability | 3 revisions, all before integration, ≤1 min rework each | R8: frozen interface defect found after the build |
| UI assembly speed | 3 screens, 37 requirements, built in 10.5 min without domain code | not comparable |
| Integration defect rate | 3 in wave 1, 0 in wave 2 | R5: 7 conflicted merges; R6: 4 critical defects |
| Verified at cutoff | 39/39 selected (experiment-level) | different scopes |
| CI waiting | about 1 min queue; superseded runs wasted | R6: median 5 min, p90 35 min runner wait |
| Coordination overhead | coordinator only: releases, 3 CRs, 3 integration repairs | not recorded |

**Shift or reduction?** Both, but the reduction is the smaller part:
- Most of the gain is the critical path running UI work in parallel (about 8 min modeled).
- The work genuinely removed is integration conflict and late interface repair. Integration here took 4.6 min with 0 conflicts.
- Contract definition work moved earlier (about 4–6 min of Phase A and revisions), and is not removed.

## 7. Limitations

- **DEV-0:** the run used branch `claude/exp-indy-002-contract-first-hhlktz`, not `experiment/exp-indy-002`; the session is limited to its assigned branch.
- **DEV-1:** the protocol changed at T+2.6, after every wave-1 contract had already been *defined* in one piece. Wave-1 releases came in about 2 minutes, not interleaved across 50, so the incremental-release mechanism is evidenced mainly by wave 2 (one group).
- **DEV-2:** integration began at T+18 when both sides were staged, not at the T+60 checkpoint. Holding it would only have added idle time.
- The persistence boundary is Limen Storage (localStorage). There is no GitHub write, conflict, or unknown-outcome path. Offline and Conflict exist in the domain and are unit-tested, but were never driven in a browser.
- Verification is experiment-level. The browser test was written by the coordinator, who also wrote the contracts; it is independent of the implementing agents, not of the contract author. The governed gates were not run: Dokimos manifest, `catalog-acceptance`, journeys J1–J3, `slice-gate`, `completion-gate`.
- Chromium only.
- The agents were cloud sub-agents on one 4-vCPU container. Throughput may not transfer to other machines or larger teams.
- This slice is about 39 of the catalog's 44 requirements and a small fraction of the full approved scope (37 slices, 1,387 modeled serial minutes in R10's plan). Extrapolating carries a large error.

## 8. Four-hour forecast (hours 0–3 F# and unit/contract tests, hour 3+ UI and integration)

**Basis.** This run's measured rates, discounted for:
- real GitHub persistence, which in R4 cost hours of CI round trips;
- a far larger and more interdependent scope;
- governed gates;
- multi-hour fatigue and review effects.

**Late-integration risk.** The proposed plan defers all UI to hour 3. That gives up this run's main gain, UI concurrency. It also delays defects like D-3, which only a browser test catches. The forecast therefore assumes the *parallel* variant, with UI agents starting at the first release and integration running per wave.

| At T+4h | Conservative | Expected | Optimistic |
|---|---|---|---|
| F# functions implemented | 250 | 450 | 700 |
| Net F# lines (app + unit tests) | 6,000 | 11,000 | 18,000 |
| Field contracts verified | 60 | 110 | 160 |
| UI screens assembled | 4 | 7 | 10 |
| E2E workflows accepted | 2 | 4 | 6 |
| Integration repair time | 60 min | 35 min | 20 min |
| Requirements independently verified | 70 | 130 | 200 |
| CI runner-minutes / elapsed | 400 / 60 min | 250 / 35 min | 150 / 20 min |

The expected case uses about 0.55 req/min, roughly 40% of this run's rate. Under the hour-3 UI start as literally proposed, I expect the end-of-hour-4 figures to be **20–35% lower** than this table, and almost all integration repair to land in hour 4.

## 9. Acceptance criteria (directive section 10)

| Criterion | Result |
|---|---|
| Released contracts complete and traceable | **Met.** contract-check C1–C5 PASS; 39/39 mapped to tests. |
| Domain and UI agents work independently against the same contract | **Met.** Separate worktrees, a stub seam, 0 conflicts. |
| Staged implementations compile | **Met.** Both, at every staging. |
| Integration tests detect real mismatches | **Met.** D-3 was caught only by the independent browser test. |
| Mandatory acceptance checks remain enforced | **Met.** Nothing was disabled or weakened. D-1 changed my own test-ID matcher to accept an ID at the start of a test name; it still requires a named test per ID. |
| At least one meaningful E2E workflow independently verified | **Met** (experiment-level). |
| Time savings exceed measured integration rework | **Met, modeled.** About 8.4 min saved against 3.0 min of integration and 2.7 agent-min of contract rework. |
| No provenance or isolation rule violated | **Met.** Only `experiments/` and `docs/experiments/` changed, and nothing was copied. |

## 10. Recommendation: ADOPT WITH CONDITIONS

**Mechanism demonstrated:** a reflection-checked F# contract plus an `EnginePorts` seam let a UI agent build complete screens with zero domain code. The domain and UI then integrated with zero conflicts. An additive contract release then went from release to accepted, in production code, in 3.5 minutes.

Conditions:
1. Extend the registry schema and contract-check before the competition:
   - an accessible name for every control, not only fields;
   - labels for nested fields;
   - a test-spec entry for every `transition`.

   Each of CR-1, CR-2, CR-3 and D-3 would have been caught in Phase A.
2. Keep UI agents parallel from the first release. Do not defer UI to hour 3.
3. Run the independent, registry-driven browser test at **every** integration, not only at the end. Use static binding checks as well, but not instead.
4. Push once per integration candidate. Avoid PR pushes that cancel required runs (CI-F1, CI-F2).
5. Before relying on the forecast, repeat the experiment on a GitHub-backed persistence slice with the governed gates, and run a controlled milestone-first comparison on the same slice.
