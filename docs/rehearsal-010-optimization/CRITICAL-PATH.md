# Rehearsal 010: the critical path

**Source:** `results/cpm-v1.json`, the `cpm` array of each scenario, from `tools/r10/schedule-core.fsx` `cpm`. Each slice costs its implementation estimate plus one verified integration (CI time and a 3-minute merge). The critical-path method (CPM) ignores lane contention; the simulation (`results/V*.json` `timeline`) includes it. **All durations are model results from estimates. None are observations.**

## The dependency critical path (unlimited lanes)

`T10 → T14 → T13 → T17 → T22 → T28 → T31 → T32 → T33 → T34`: ten slices, each of which must be *integrated* before the next can start.

| CI per integration | Critical path (45-min set-up) | With 25-min set-up |
|---|---|---|
| 0 min | 409 | 389 |
| 2 min | 429 | 409 |
| 5 min | 459 | 439 |
| 10 min | 509 | 489 |
| 20 min | 609 | 589 |

`schedule.fsx` prints 389 at zero CI because it omits merge minutes from its critical path. This model includes them: 10 × 3 = 30 minutes on the chain.

**Consequence:** each minute of CI per integration adds about 10 minutes to the floor. With the current DAG, **the full scope cannot finish within 420 minutes once integration costs more than about 1 minute of CI, even with unlimited agents.** Adding lanes or splitting slices cannot change this. Only the DAG's critical tasks, the set-up, or the cost per integration can.

## Per-slice earliest start, earliest finish, latest start, slack (V1, 0-min CI, 45-min set-up)

| Slice | ES | EF | LS | Slack | Critical |
|---|---|---|---|---|---|
| SL-T10 | 45 | 77 | 45 | 0 | yes |
| SL-T14 | 77 | 100 | 77 | 0 | yes |
| SL-T12 | 77 | 95 | 82 | 5 | |
| SL-T11 | 77 | 95 | 92 | 15 | |
| SL-T18 | 77 | 115 | 97 | 20 | |
| SL-T16 | 95 | 133 | 110 | 15 | |
| SL-T13 | 100 | 148 | 100 | 0 | yes |
| SL-T19 | 115 | 178 | 135 | 20 | |
| SL-T17 | 148 | 171 | 148 | 0 | yes |
| SL-WI-INV-WORKSPACE | 171 | 404 | 176 | **5** | near |
| SL-T22 | 171 | 244 | 171 | 0 | yes |
| SL-WI-GEN-UI | 171 | 289 | 291 | 120 | |
| SL-T20 → T21 → T30 | 178 → 249 | … | | 20 | |
| SL-T28 | 244 | 292 | 244 | 0 | yes |
| SL-T31 | 292 | 340 | 292 | 0 | yes |
| SL-T32 | 340 | 363 | 340 | 0 | yes |
| SL-T33 | 363 | 386 | 363 | 0 | yes |
| SL-T34 | 386 | 409 | 386 | 0 | yes |

The full table for all 37 slices, and for every CI latency, is in `results/cpm-v1.json`.

## Lane load: where the critical path stops being the limit

| Role (frozen ownership) | Lane-minutes | Notes |
|---|---|---|
| `ui` | **564** | T16, T22, GEN-UI, GEN-WDL, INV-LIST, INV-START, INV-WORKSPACE: one agent, files nobody else may edit |
| `integration` | 293 | T10, T11, T17, T21, T24, T31, T32, T34 and GEN-AR/EXT-DATA/PF/VER |
| `persistence` | 155 | T18 → T19 → T20 → T25 |
| `domain` | 130 | |
| `reviewer` | 127 | |
| `ui-2` | 120 | T23, T26, T28, T29 |

With 0-min CI the simulation finishes at T+720, not T+409. `ui` can't start its generated items before T17 integrates (about T+171), and it then has about 530 minutes of work.

## Where the time goes (V1, 10-minute CI; lane-minutes; model)

| Category | Minutes | Basis |
|---|---|---|
| Initialization | 45 × 6 lanes | `--setup` (estimate; INITIALIZATION-OPTIMIZATION.md measured 3–6 min of machine time) |
| Implementation | 1,387 | DAG estimates and derived estimates (1.35 min per mandatory obligation) |
| Lanes idle inside the window (waiting for dependencies or for a role's files) | 1,337 | `laneIdleMinutesInWindow` |
| Verification (CI per integration) | 370 | 37 runs × 10 min, at 1 slot |
| Integration queue waiting (implemented → verification starts) | 453 | `queueWaitMinutes`, summed over slices |
| Merge coordination | 111 | 37 × 3 min |
| Compilation, unit/integration tests, browser, live GitHub | inside the CI figure | the gate is one serial job (VERIFICATION-OPTIMIZATION.md §4); only fixture-scale timings are measured |
| Defect repair and retesting | 0 in the base runs | `--fail-every` sensitivity in SCHEDULE-COMPARISON.md |

**Implementing compared with waiting:** inside the 420-minute window, the six lanes have 6 × 375 = 2,250 lane-minutes after set-up. Only about 900 of them go to implementation. The rest is waiting: for dependencies, for the one `ui` agent, or for the integration queue.

## What truly requires serialization, and what can run concurrently

- **Serial by dependency:** the ten-slice critical chain; the persistence chain T18 → T19 → T20 → T21; within a role, every slice of that role (one owner per file).
- **Serial by rule:** merges (one integration authority) and the verification of each candidate on current main.
- **Concurrent:**
  - different roles' slices;
  - independent test groups inside one gate run (Chromium, WebKit, the deterministic dotnet suites);
  - speculative verification of several queued candidates, each on main plus the candidates ahead of it, with merges still in order (`--queue-depth`).
