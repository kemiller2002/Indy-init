# Workstream C: verification levels and CI measurement

## Levels

| Level | When | What runs | Command |
|---|---|---|---|
| 1, local | after each meaningful change | compile the affected F# projects; focused unit tests; the relevant contract and architecture checks. Selection is advisory | `dotnet fsi tools/r10/change-impact.fsx -- --indy-init . select <files>` (22 adversarial cases, [VERIFICATION-NEGATIVE-CONTROLS](../execution-readiness/VERIFICATION-NEGATIVE-CONTROLS.md)) |
| 2, slice | a functional slice is ready | its integration tests, acceptance criteria, requirement and field IDs, evidence, independent review. Independent slices verify concurrently in separate work dirs | the slice gate (`tools/r9/verification-gate.yml`) |
| 3, integration | every merge | **full required CI**; cross-component tests; no merge on a failed required check; independent review kept | branch protection plus the required checks |

A green Level 1 or Level 2 never substitutes for Level 3. Unknown files make Level 1 select everything (fail-safe).

## Measured CI before the change
Source: run 42087387548 (Preflight tooling, PR #132 at `e141f86`, created 18:45:59 UTC), from the Actions API.

| Job | Wall (min) | Dominant step |
|---|---|---|
| **r10-adversarial** | **21.5** (18:47:27–19:09:00) | adversarial cases, serial: **15.9** |
| fsharp-tooling | 13.0 | Forma layout assertions: 9.9 |
| release-gate-repair | 9.7 | repair cases: 8.75 |
| r9-readiness-gates | 7.3 | readiness gates: 6.4 |
| suite-commands | 3.7 | – |
| frozen-v4 | 1.2 | – |
| offline-contract-tests | 0.3 | – |
| **Run total** | **23.0** | r10-adversarial is the critical path |

The "Indy Init offline factory" workflow takes 1.2–8.0 min per run (40 runs sampled), so it is never the critical path.

**Waiting observed** (40 most recent `pull_request` runs, 14:17–19:21 UTC):
- **Runner allocation inside a run.** Job start lagged run creation by 0.4–3.8 min.
- **Run queue.**
  - Two runs show 32.9 and 66.0 min from creation to start, but both are attempt-2 re-runs, so that span includes the first attempt. They are excluded; this is not queue time.
  - At 19:21 UTC, 6 runs across 3 PRs sat `queued` at once (runner contention among the three sessions).
- **Cancelled work.** One 56.4-min run was cancelled by timeout (slow apt mirror; see #129 history). Several runs show 0.2 min: superseded by a newer push.
- **Duplicate work.** The Conditor and echelon workstation download (~21–27 s) runs in 3 jobs, and the Playwright browser install (~42 s) in 2. Together that is under 2 min per run and not the bottleneck. No unverified cache was introduced.

## The change (this PR)
- **Sharding.** The adversarial cases now run in three parallel shards: `r10-adversarial-cases`, with `adversarial.fsx --shard K/3`.
- **The required check keeps its name.** `r10-adversarial` still exists and:
  - runs with `if: always()`, so a failed or cancelled shard cannot leave it *skipped*, which would read as passing;
  - fails unless every shard succeeded;
  - proves with `jq` that the shards' case ids equal the full case list exactly once, and that none failed;
  - then runs its own steps unchanged.
- **Controls for the merge check** (run locally): it accepts a complete set and refuses each of these: a missing case, a failed case, a duplicate in place of a case, and a case run twice. Since review L1 it also refuses NOT-RUN.
- **Evidence layout (review L2).** Each shard's evidence sits under `adv-evidence/adversarial-shard-N/`; there is no merged `adversarial-results.json` (no tool reads one). When a shard fails, its evidence is in that shard's artifact.
- **Shard selection**, checked locally: `--only C09,C10,C11,C12 --shard 1/3` ran exactly C11 (index 10 mod 3 = 1). It reported VALIDATED and recorded `allCases` = 36.
- **Expected effect (model, not observed).** The critical path drops from about 21.5 min to about 1.7 (set-up) + 15.9/3 + 1.7 (set-up) + 4.5 (the remaining steps), roughly 13 min. This depends on free runners. The **measured** after-timing will come from this PR's own CI run and will be recorded in [MEASUREMENTS.md](MEASUREMENTS.md).

## Not changed, deliberately
- Required checks are not path-filtered: that would make a required check absent on some PRs.
- No cache of build outputs between runs.
