# Rehearsal 010: verification pipeline

## What happens today

`tools/r9/verification-gate.yml` is installed by `r9.fsx govern`. One job runs on every slice pull request and on every push to main. On **every** slice it runs:
- governance and sequencing checks;
- the pinned toolchain;
- the compiled plan;
- the suite preflight;
- **all of the plan's suites at the candidate**;
- fault probes;
- governed catalog acceptance;
- journeys J1–J3 and the design contract in both browsers;
- the verification gate and the slice gate.

There is no change-aware selection. A one-line view change reruns everything.

## Measured

Elapsed times, not aggregate queue-minutes. Details in BOOTSTRAP-MEASUREMENTS.json.

| Item | Measured | Source |
|---|---|---|
| CI queue, workflow created → first job | 3.3–8.4 min | Preflight run 42004659389 |
| Restore, cold / warm | 13.6 s / 1.4 s | this session |
| Build of the bootstrapped solution, cold / warm / one-file incremental | 18.6 s / 11.4 s / 3.1–3.3 s | this session |
| Contract typecheck | 6–11 s | this session; CI |
| Playwright + browsers install | 65 s | CI |
| Fixture journeys J1–J3 and design contract, both browsers | 33 s | CI |
| Forma layout assertions, both browsers | 13.1 min | CI (Indy-init tooling; not in the competition gate) |
| Rehearsal 009: candidate commit → its verification evidence commit | ≤ 16 min, an upper bound that includes queue | R9 commits 33d072d 01:32 → 296b245 01:48 |

**Not measured:** the governed gate on a real Rehearsal 010 candidate, because none exists, and its share of failed-test rework.

**So the per-merge cost is dominated by queue latency (3–9 min) plus full-suite browser time.** Compilation is seconds.

## Three levels

| Level | When | What | Replaces Level 3? |
|---|---|---|---|
| **1. Fast local** | after every edit, by the builder | `dotnet build` (incremental, seconds), the owner's unit and contract tests, a static UI check, the requirement-ID check | **no** |
| **2. Affected capability** | before opening or updating a slice PR | the test projects and suites that `tools/r10/change-impact.fsx select <paths>` names (CHANGE-IMPACT-MATRIX.json) | **no** |
| **3. Mandatory integrated acceptance** | every slice merge (frozen), checkpoint and release | the full governed gate, exactly as frozen: real GitHub provider, historical data, J1–J3, persistence and conflict, invariants, security, accessibility, design contract | – |

**Levels 1 and 2 are builder-side feedback.** They catch a defect before the PR reaches the 3–9 minute queue and the full gate, so they reduce failed gate runs: the rework loop. They do not shorten a passing gate run.

**Nothing is added to or removed from the gate.** Making the gate itself change-aware (skipping unaffected suites at merge) would be test-result reuse. It is **not proposed for Rehearsal 010**, because it would need provenance, invalidation and independent review first (VERIFICATION-OPTIMIZATION.md §7 covers the parallel-queue variant, ODR-R10-22).

## Change-impact rules (implemented as a proposal; `tools/r10/change-impact.fsx`)

1. A changed file selects its project, and every project and test project that references it, transitively. The graph comes from the frozen `project-structure.json`.
2. Path rules add suites:
   - **provider:** the GitHub adapter and the machines → `SU-DOTNET-LIVE`, `SU-BROWSER-LIVE`;
   - **replay:** events, evolve, the storage codec → historical replay;
   - **UI:** views, routes, journeys → both browsers and traceability;
   - **shared presentation:** stylesheet, shell, packages → every browser suite and the design contract;
   - **governance:** contracts, pins, project files, `.github/` → **everything**.
3. **Fail-safe:** an unrecognised path selects everything.
4. **Output always says "verified by this selection: nothing".**

Adversarial tests: VERIFICATION-NEGATIVE-CONTROLS.md. 15 cases pass, and a control removes a rule and shows the corresponding case would fail.
