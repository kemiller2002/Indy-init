# CI-CADENCE-001: baseline

Each figure is labelled with its kind:
- **observed:** measured in this session, or from the Actions API;
- **historical:** from a committed record, not re-measured here;
- **model:** a computed estimate.

## Preparation (separate from the 60-minute timebox)
- **Started:** 2026-11-05T21:02:02Z (observed; first command of the session).
- **Ended:** recorded in TIMELINE.jsonl as `prep.end`, the moment before T+0.
- **What preparation included:**
  - installing .NET SDK 10.0.402, which the container lacked;
  - cloning causa-data;
  - one build and test pass of the preview;
  - reading the CI records.

## Required GitHub CI (workflow "Preflight tooling", `.github/workflows/preflight-tooling.yml`)

| Run | Kind | Configuration | Wall (min) | Runner-min | Queue |
|---|---|---|---|---|---|
| 42087387548 (PR #132, `e141f86`) | historical ([VERIFICATION-CI.md](../../rehearsal-010-optimization/acceleration/VERIFICATION-CI.md)) | before sharding; r10-adversarial serial | 23.0 | not recorded | job start lagged run creation by 0.4–3.8 min across 40 runs |
| 42105890806 (`main` push, `ba9124d`) | **observed** (Actions API, job timestamps) | after sharding (#136): 3 adversarial shards and the r10-adversarial merge job | **18.3** (20:42:37–21:00:54) | **60.5** (sum of 10 job durations) | jobs started ≤ 3 s after creation; **r10-adversarial waited 4.1 min** for a runner after its last shard ended |
| 42098958019 (PR #136, `952c390`) | observed (run timestamps only) | after sharding, during 3-session contention | 43.6 (19:58:28 created, 20:42:05 last update) | not fetched | contention; jobs not broken down |
| 42104590116 (`main` push, `a293fac`) | observed (run timestamps only) | before sharding | 16.4 | not fetched | – |

**Observed critical path after sharding.**
- It is **fsharp-tooling (18.2 min)**, not the adversarial suite. Its step "Executable Forma layout assertions" took **13.2 min** (20:47:14–21:00:26).
- The 13-minute projection in VERIFICATION-CI.md assumed r10-adversarial stays critical. Sharding cut the adversarial shards to 3.5–8.5 min, but the run's wall time is now bounded by fsharp-tooling.
- So the "approximately 13 min" projection is **not confirmed**: the one clean observation is 18.3 min.

**Job durations of run 42105890806 (observed, min):**

| Job | Duration |
|---|---|
| fsharp-tooling | 18.20 |
| r10-adversarial-cases (1) | 8.52 |
| r9-readiness-gates | 7.43 |
| release-gate-repair | 7.28 |
| r10-adversarial-cases (0) | 6.55 |
| r10-adversarial | 4.20 |
| r10-adversarial-cases (2) | 3.48 |
| suite-commands | 3.17 |
| frozen-v4 | 1.43 |
| offline-contract-tests | 0.22 |

**Comparability.** None of these runs is directly comparable to the experiment's application work:
- **Preflight tooling does not build or test `preview/causa`.** No workflow references `preview/`. Its cost is pure overhead for application changes: it verifies requirements tooling, frozen packages and gates.
- Runner conditions differ: 42098958019 ran under 3-session contention, while 42105890806 ran on an idle pool.

**Trigger rules.** Observed in the workflow file at the starting commit:
- `push` runs only on `main`; `pull_request` runs on every PR commit, and a newer commit cancels the older run; `workflow_dispatch` is allowed.
- A local commit or a push to an `experiment/*` branch therefore triggers **no** CI. The experiment dispatches full CI explicitly, and no protection is disabled.

## Local verification of the preview (observed, this container, 4 vCPU)

| Step | Duration | Result |
|---|---|---|
| `dotnet build tests/Engine.Tests` (cold, includes restore) | 19 s | 0 errors |
| Engine tests (`-- causa-data`) | 2 s | all engine tests passed |
| `dotnet fsi tests/ArchitectureCheck.fsx` | 2.4 s | A1–A7 PASS |
| `npm ci` (web) | 2.5 s | ok |
| `dotnet fsi build.fsx` (WebAssembly publish, no wasm-tools) | 64 s | built dist/ |
| `read-path.mjs` (real causa-data over the proxy), run 1 | 66 s | **5/6, FAIL**: `#detail-title` wait timed out (60 s) opening the first case |
| `read-path.mjs`, run 2 (same build) | ~70 s | **25/25 PASS** |

The **read-path flake is pre-existing.** It is the network latency of reading 432-line event files through the proxy. It is recorded here so it is not counted as an experiment defect; it remains a flaky check.

## Prior development measurements (historical, [MEASUREMENTS.md](../../rehearsal-010-optimization/acceleration/MEASUREMENTS.md))
- **Practice app.**
  - First successful F# build at T+19.8 min.
  - Connect to Save/Reload at T+24.7 min.
  - One real defect was found and fixed at T+26.0.
  - Repair took about 2.5 min; CI wait was 0.
- **Lead idle.** About 11 min of failed verification and repair. CI waiting was 0 idle, because the lead never blocked on CI.
- **Not measured:** F# lines per minute and verified requirements per minute. No prior run measured either, so there is **no historical throughput baseline** to compare against directly.
