# Rehearsal 010 execution readiness: final report

**Scope and limits.**
- Model results are estimates from the frozen DAG and the derived estimates. They are not observations.
- Measurements are labelled with their source: BOOTSTRAP-MEASUREMENTS.json, and the CI job data of Preflight run 42004659389.
- Agents 2 (contract-first UI) and 3 (historical data compatibility) had published **no PR or branch** when this was written. Their effects are modelled from the problem they address, not from their deliverables (§7).
- Nothing frozen was changed. No owner decision was recorded, and the timed run was not started.

## 0. Launch blocker found with Agent 2 (CF-07)

**Under the frozen slice gate, the approved plan cannot complete.**
- The gate's journeys rule requires every bound assertion to pass live at merge (Ledger.fs 391–410).
- The shell slice SL-T16 is bound to J2-08, which runs after steps that need slices depending on T16. Slices sharing an assertion (J1-03, J1-06) need each other.
- Every one of the approved plan's 42 merge-dependency edges is cyclic.

This is independent of every optimization, and needs the owner's decision on CF-07 (Agent 2, PR #121) before any launch. Details and runs: CROSS-AGENT-FINDINGS.md.

**Every milestone time in this report assumes the literal CF-07 option A.** Under that reading, journey-bound assertions whose needs are not yet on main are judged at release, so merges are never delayed. This is not a bound: the model is not monotone in added constraints (review RVO-8-1). Under the stricter greedy cycle-breaking rule, the combined plan's authoritative list moves from T+239 to T+545 at 10-min CI. The raw M0 browser is unaffected. See CROSS-AGENT-FINDINGS.md.

## 1. Current bottleneck ranking

Combined plan E8 at 10-min CI:

| Rank | Bottleneck | Evidence |
|---|---|---|
| 1 | **Serialized verification**, for the early milestones at realistic CI. One full governed gate per slice, plus a CI queue of 3.3–8.4 min (measured) | Frozen depth-1 gate (E8c) against E8, at 10-min CI: M2 T+314 against T+239, M3 T+665 against T+387. **For full completion, the effect is not robust:** at 5-min CI the depth-1 gate finishes first (T+573 against T+599). See §4 |
| 2 | **The DAG's release tail.** T17 → T22 → T28 → T31 → T32 → T33 → T34 | Critical path 474 min at 10-min CI. Seven serial integrations from T17 (T+196) to T34 (T+565) |
| 3 | **The persistence chain to the historical list.** T18 → T19 (save machine) → T20 → T25 | T25 at T+270 (E8, 10-min CI). T19 is not needed for reading (DATA-VISIBILITY-DEPENDENCIES.md) |
| 4 | **The `ui` lane.** One owner of `Update.fs`, `Engine.fs`, `Projections.fs` and `Views/Investigation.fs` | 406 implementation minutes even with V5 ownership, the most of any lane |
| 5 | Bootstrap planning value | 25 planned against 2.2 min measured to the R9 bootstrap commit |

## 2. Verified measurements

| What | Value | Source |
|---|---|---|
| Bootstrap generation | 2.1 s | this session |
| Restore, cold / warm | 13.6 s / 1.4 s (64 MB) | this session |
| Build, cold / warm / one-file | 18.6 s / 11.4 s / 3.1–3.3 s | this session |
| Contract typecheck | 6 s here / 11 s CI | this session; CI |
| CI queue (workflow created → job start) | 3.3–8.4 min | run 42004659389 |
| Playwright + browsers | 65 s | CI |
| Fixture journeys J1–J3 + design contract, both browsers | 33 s | CI |
| R9 T0 → bootstrap commit | 2 min 10 s | R9 `evidence/t0/T0.md`, commit 8001e7c |
| Historical multi-agent file conflicts | **no data**: R9 had one agent | R9 git log |

## 3. Proposed optimizations, and what was implemented

| # | Optimization | Implemented? | Evidence |
|---|---|---|---|
| A | Integration architecture | **Already frozen in v4.1.4.** Documented and checked (INTEGRATION-OWNERSHIP.json is computed against the manifest) | Substitution demonstration: fixture and GitHub connectors behind one screen compile; drift fails with FS0001 and, **only with `TreatWarningsAsErrors`**, FS0049/FS0026 |
| B | Level 1/2 change-aware selection | **Implemented as a builder tool, not in the gate:** `tools/r10/change-impact.fsx` and CHANGE-IMPACT-MATRIX.json, with 15 adversarial cases in CI | One defect was found while writing it and fixed (§8) |
| C | Shared-file contention | Analysis and recommendations FO-1..4. Cross-owner contention is already excluded by the frozen rules; same-owner serialization remains | FILE-CONTENTION-ANALYSIS.md |
| D | Bootstrap | Measured. Recommendation: plan 10 min of set-up and verify caches before T0 | BOOTSTRAP-CRITICAL-PATH.md, BOOTSTRAP-PREFLIGHT.md |
| E | Orchestration | Handoff contract, as a view over Praxis and progress records | AGENT-HANDOFF-CONTRACT.json |

## 4. Updated critical-path model

0 / 5 / 10-min CI; minutes after T0. Runs are in `results/` and `execution-readiness/results/`.

| Plan | Connect screen | GitHub read | List / detail (raw, M0) | Historical list, authoritative (M2) | Replay (T25) | Create (M3) | Save (M4) | Analysis (M5) | All integrated | Obligations by T+420 |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 approved (R0) | 307/346/406 | 225/255/282 | – | 720/759/794 | 250/283/315 | 665/704/739 | 492/531/566 | 698/737/772 | 720/759/794 | 402/444/435 |
| 2 existing optimization (R2) | 220/246/270 | 208/225/244 | – | 259/285/309 | 236/256/283 | 439/465/489 | 617/643/667 | 650/676/700 | 709/740/769 | 538/538/527 |
| 3 contract-first UI + M0 (E3) | 116/125/135 | 116/125/135 | 116/125/135 | 240/265/270 | 227/259/290 | 420/437/450 | 598/615/628 | 631/648/661 | 690/712/730 | 587/575/538 |
| 4 historical data compatibility | as plan 2 | as plan 2 | as plan 2 | **feasible only with it** (ODR-R10-25) | as plan 2 | | | | | |
| 5 incremental verification | as plan 2: the gate is unchanged. It reduces failed gate runs, which is unmodelled | | | | | | | | | |
| 6 reduced contention, V5 ownership (R4) | 220/246/270 | 205/226/249 | – | 240/266/296 | 233/259/290 | 393/419/443 | 471/502/531 | 458/484/508 | 594/642/684 | 644/624/585 |
| 7 bootstrap 10 min + M0 (E7) | 101/110/120 | 101/110/120 | 101/110/120 | 248/270/294 | 212/241/268 | 428/450/474 | 606/628/652 | 639/661/685 | 698/725/754 | 575/538/536 |
| **8 combined (E8)** | **101/110/120** | **101/110/120** | **101/110/120** | **206/223/239** | 212/246/270 | **359/376/387** | 437/459/496 | 424/441/473 | **560/599/565** | **689/689/658** |
| 8b combined, +5 min overhead per slice | 110/120/130 | 110/120/130 | 110/120/130 | 237/251/266 | 243/271/297 | 410/424/439 | 502/521/541 | 487/501/516 | 646/681/715 | 624/612/612 |
| 8c combined, frozen gate (queue depth 1) | 101/112/145 | 101/112/145 | 101/112/145 | 206/268/314 | 212/300/405 | 359/420/665 | 437/509/717 | 424/491/678 | 560/573/803 | 689/652/473 |

**What E8 combines:**
- the successor split;
- the speculative queue at depth 3 (ODR-R10-22: unbuilt);
- milestone-first ordering;
- M0 after T10 (ODR-R10-30);
- contract-first UI, meaning generated UI items start after T16 instead of T17 (ODR-R10-31);
- V5 ownership (ODR-R10-20);
- 10-min set-up.

¹ In E8, M2 (T+206/223/239) is *delivered* before M1 (216/246/270) and before T25, the authoritative catalog load (212/246/270). It is *usable* only from the later of M1 and M2, for example T+216 at 0-min CI (review RVO-6-4). The "Connect screen" column for M0 plans shows the raw M0 slice.

Contract preparation is *not* free in reality: plan 8b charges 5 min per slice for it and for integration overhead.

**Non-monotonic, and the cause is not isolated** (corrected after review RVO-6-2; an earlier draft blamed failures, but every E run has `failEvery 0`). Full completion in the E8 configuration, by queue depth, at 0 / 5 / 10-min CI:

| Queue depth | All integrated |
|---|---|
| 1 (frozen gate, E8c) | 560 / 573 / 803 |
| 2 | 560 / 599 / 565 |
| 3 (E8) | 560 / 599 / 565 |
| 4 | 560 / 599 / 630 |

A deeper queue is *not* always faster here. The existing test "a deeper speculative queue is never slower" covers only the base ownership manifests. It does not cover this configuration, and it would fail on it. The likely mechanism is greedy milestone-first assignment interacting with merge order, but this is not shown. **Treat E8's completion as a range (560–630), not a point**, and do not read the 10-min CI 565 → 803 difference as robust. The early-milestone gains of the speculative queue: none at 0-min CI; about 45 min at 5-min CI (M2 268 → 223, M3 420 → 376); and M2 −75 min, M3 −278 min at 10-min CI (corrected in review RVO-7-1).

**Exceeding 420 minutes:** no probability is given, because the evidence does not support one. Under every modelled plan, CI latency, overhead and estimate scale, **the full scope does not finish within 420 minutes**. The best is T+560. On the schedule, E8 delivers data visibility (M0), Connect (M1), the authoritative list (M2) and creation (M3) inside the window at every CI value. Analysis (M5: 424/441/473) and save (M4: 437/459/496) fall just after it. M1–M3 still need ODR-R10-25, -26, -28 and -29 to pass against real data.

## 5. The new dominant bottleneck, if UI starts immediately

The chain (E8, 10-min CI):
```
T10 [10–52] → T12 → T13 [100–158] → T17 [158–196] → T22 [ui 237–329] → T28 [ui-2 348–406]
            → T31 [406–464] → T32 [464–499] → T33 [reviewer 499–532] → T34 [532–565]
```

After the domain kernel (done by T+208) and persistence (done by T+256), the **release tail** binds. T31 (release candidate, 45 min) → T32 → T33 → T34 is 105 minutes of estimates plus four gated integrations. Ahead of it sit the workspace views (T22, 70 min) and history (T28, 45 min).

| Candidate | Binds? | Evidence |
|---|---|---|
| Domain implementation | no: idle after T+208 | E8 lane loads |
| Historical replay | no, for time; **yes, for feasibility** (C1) | T25 at T+270; ODR-R10-25 |
| GitHub persistence | binds M1 and the authoritative list, not the full scope | T18 → T19 → T20 → T25 |
| Integration | yes, in the release tail | T31, T32, T34 are integration's |
| Verification | **yes for M2 and M3 at 10-min CI** (without the speculative queue, M2 +75 min, M3 +278 min). **Not shown for completion**, which is non-monotonic in queue depth (§4) | E8c, depth sweep |
| Bootstrap | no longer, at 10 min | E7 |
| Remaining UI | partly: T22 → T28 | 406 ui-minutes |

**Recommended next optimization, from these results:** build and independently review the speculative-verification gate (VERIFICATION-OPTIMIZATION.md §7, controls S1–S7, ODR-R10-22). The evidence is the 10-min CI point for M2 and M3 (E8 against E8c). It is not the full-completion figure, which is not robust (§4), and the gain is about 45 min for M2 and M3 at 5-min CI, and none at 0-min CI. It is still the only proposed change that addresses the per-slice queue (3.3–8.4 min measured) directly. Second: split T19/T20 and T22 in v4.1.5 (ODR-R10-21).

## 6. Owner decisions

New:
- **ODR-R10-31:** contract-first UI. Generated UI items may start after T16 (the frozen IF41 contracts plus bootstrap stubs) instead of T17. This relaxes "a generated item never precedes the kernel" for UI items only.
- **ODR-R10-32:** one combined v4.1.5 successor package. It brings ODR-R10-20, -21 and -25, FO-2 and FO-3, Agent 2's UI contracts and Agent 3's event-union successor into **one** freeze cycle, never two competing ones.

Still open:
- **CF-07 (Agent 2, PR #121): how assertions with cyclic merge dependencies are judged. A launch blocker on the approved plan.**
- ODR-R10-19..30;
- the App installation (ODR-R10-28).

## 7. Integration readiness, and the other two agents

**Ready under v4.1.4:**
- compile-time module independence (demonstrated);
- the composition owner (integration);
- the contract change control (coordinator).

**Conditions for incorporating Agents 2 and 3:**
- **OD-INDY-22 forbids pre-written contest code.** Typed contracts may enter the run only as frozen successor-package content, as the IF41 files do. Fixture implementations, decoders and replay adapters may enter only as specifications or stubs.
- **Both are likely to extend shared frozen types:** `CoreEvent` (Agent 3) and IF41-APP `Msg`/`Model` (Agent 2). They must be compiled into one package (ODR-R10-32).

Their PRs have not appeared, and nothing was cherry-picked or merged.

## 8. Adversarial check of this work

| Finding | Status |
|---|---|
| The change-impact graph keyed projects by `name`, not `id`, so production edges were silently empty, and every other case still passed | **fixed**, plus a graph-load case that would have caught it |
| Screen-contract drift compiles silently without `TreatWarningsAsErrors` | documented as a load-bearing setting; changes to it select every test |
| Plans 3 and 7 include M0; they are not pure single-factor runs | labelled in the table |
| Contract preparation, merge supervision and coordination are not lane time in the model | 8b charges 5 min per slice; supervision is still unmeasured |
| Incremental verification does not shorten a passing gate run | stated: plan 5 equals plan 2 |
| Optimism: E8 needs eight owner decisions and one unbuilt gate | stated in §4 and §6 |
| Review round 6 (RVO-6-1): known paths could select nothing: Host.cs, a project outside the graph, a new source file, Program.fs | **fixed:** fail-safe on an empty selection or an out-of-graph source path; composition files added to the presentation rule; 5 new cases, including "no manifest file ever selects nothing" (20 cases) |
| RVO-6-2: completion non-monotonic in queue depth, misexplained | **fixed:** §4 depth sweep and wording; completion stated as a range |
| RVO-6-3/4/5 | queue bound corrected to 8.4 min; M2 delivery before M1 and T25 footnoted; Level 1/2 use needs the owner's acknowledgement |

Independent review: ADVERSARIAL-REVIEW.md, round 6 (FAIL, fixed) and round 7.

## 9. Recommended execution plan

**For a launch on the approved plan today:**
1. Run the bootstrap preflight P1–P8.
2. Plan 10 min of set-up.
3. Builders *may* use Level 1/2 locally (`change-impact.fsx`) before each PR, so fewer slices fail in the gate. The owner acknowledged builder use on 2026-11-05 (STARTUP-INSTRUCTIONS §4 step 4). It is not locked, and it never touches the gate (review RVO-6-5).
4. Integration uses the handoff contract.
5. Declare in advance the expected failures from ODR-R10-25 and -28.

**For the plan that delivers a working app early (E8):**
- owner decisions ODR-R10-19, -20, -22 (after the §7 gate is built), -23, -25, -27, -28, -30 and -31;
- through one v4.1.5 package (ODR-R10-32);
- before the next rehearsal, not this morning's.

## Answer to the acceptance question

**Yes, structurally, and most of the structure already exists.** The frozen v4.1.4 package gives:
- separately owned UI, data-access and domain projects, compiled in a fixed order against frozen F# contracts;
- complete compile lists from the bootstrap;
- one integration owner of composition.

Shown reproducibly here:
- UI code compiles against the contracts and stubs with no domain engine;
- a GitHub adapter replaces a fixture with no screen change;
- drift fails to compile, as long as `TreatWarningsAsErrors` stays on;
- Level 1/2 selection gives fast, fail-safe feedback that never claims verification.

**What stops it from being fast is not the structure. It is:**
1. the governance rule that UI items wait for the kernel checkpoint (ODR-R10-31);
2. one full gate per slice (ODR-R10-22);
3. the contract gap with the real catalog (ODR-R10-25).

None of these can be removed without the owner, and none requires weakening final acceptance. Final acceptance stays the full governed gate.
