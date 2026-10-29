# CI performance: Rehearsal 6 starvation and the Rehearsal 7 controls

This document covers R7-REM-004 and Experiment B. Measurements come from the GitHub Actions API for `kemiller2002/indy-init-rehearsal-006`, 2026-10-28 20:00 to 2026-10-29 03:00 UTC. Raw runs: [`evidence/r6-workflow-runs.tsv`](evidence/r6-workflow-runs.tsv) (427 runs).

## 1. What happened (measured)

| Measure | Value |
|---|---|
| Workflow runs started | 427 in total: 132 Build and test, 144 Echelon foundations, 144 Praxis validation, 7 other |
| Run conclusions | 278 success, 116 cancelled, 33 failure |
| Main Build-and-test runs | 123: **111 cancelled**, 11 failed, **1 succeeded** (the kickoff, 20:09) |
| Main runs open at once | 2 before 22:20; then 6 (22:30), 26 (22:50), 33 (23:00), **40 (23:30)**, 38 (23:50); 1 after the force-cancel at 23:53 |
| Jobs waiting for a runner | About 104–126 from 22:40 to 23:30, while 4–19 ran (the Free plan caps concurrent jobs at 20) |
| Runner wait, jobs created 22:22–23:54 | Median 5 min, p90 35 min, maximum 41 min |
| Time to first main result | **None between 22:31 and 00:35 (124 min).** Before 22:22, results came 5–36 min after creation. |
| Waste | Jobs in Build-and-test runs that ended cancelled (22:00–01:00) used about **1,297 runner-minutes** (≈21.6 h) |

## 2. Causes, with locations in Rehearsal 6's workflows

1. **A per-commit group on main.** It was introduced at 5d02836 (22:22:41Z) and **is still present** at `build-and-test.yml:39`:
   - `group: …${{ github.ref == 'refs/heads/main' && format('-{0}', github.sha) || '' }}` with `cancel-in-progress: false` on main;
   - every push to main therefore ran a complete pipeline in parallel.
   - WI-R6-A4 (`!cancelled()`) and WI-R6-A5 (force-cancel) treated the symptoms.
2. **Job-level shared groups on top of per-commit runs** (`build-and-test.yml:1026-1028`, `:1272-1274`):
   - each newer run's pending job cancelled the older run's pending job;
   - this cancelled provider conformance 38 times and deploy 14 times;
   - so older candidates failed `PROVIDER=success` for reasons unrelated to their own code.
3. **Job-level `if: always()`.** It is fixed in build-and-test but **still present** at `echelon-foundations.yml:60`.
4. **Validation on push to every branch and on `pull_request`, with no group.** It ran each pull-request push twice and added 288 runs.
5. **The source of the pipeline.** It was not Conditor's template, which was safe. An R5 pipeline was imported at T+6 and edited by agents, and nothing checked the result.

## 3. Controls for Rehearsal 7 (implemented)

| Control | Where | Status |
|---|---|---|
| `conditor ci-policy check`: CIP-000 (fail closed), CIP-001 per-commit groups, CIP-002 missing group, CIP-003 uncancelled PR runs, CIP-004 job-level `always()`/`cancelled()`, CIP-005 missing timeout, CIP-006 duplicate push and PR runs, CIP-007 cancellable candidate, CIP-008 a group shared across refs | conditor#122 (WI-0063) | Implemented. **Rehearsal 6's workflows fail it** (CIP-001 at `build-and-test.yml:39`, among others). Independent review PASS_WITH_FINDINGS; the 8 findings are fixed |
| Tier 1: `build-and-test.yml` | Conditor template | One group per ref, superseded runs cancelled, 20-minute timeout |
| Tier 3: `candidate-acceptance.yml` | Conditor template (new) | Dispatch-only, one group per candidate SHA, never cancelled, SHA-on-main guard, verification record |
| Foundations workflow | Conditor template | Push to `main` plus pull requests, one group per ref, superseded runs cancelled |
| Praxis validation workflow | praxis#300 (WI-0106) | The same treatment, plus a 15-minute timeout |
| Library build and release jobs | Conditor template | Timeouts |

**Tier 2 (integration).** Integration checks that are too slow for tier 1 run inside the candidate workflow, or in a further workflow keyed on the candidate in the same way, never in a per-commit group.

**Proposed requirement R7-AMD-03.** The policy must pass on the rehearsal repository at kickoff and on every candidate.

## 4. Experiment B (live commit bursts): NOT_RUN

The script asks for 5 and 15 rapid commits, concurrent development streams, a candidate run during continued development, and a failed job followed by a correction. It also asks for queue depth, time to first result, cancellation latency, completed versus superseded runs, candidate completion and workflow minutes.

**Not run.** It needs a disposable repository carrying these workflows. This session can push only to the repositories it was given. Its attempt to attach `indy-init-rehearsal-007` was refused, and creating a repository is outside what it was authorized to do. **No number in this section is claimed.**

**The procedure, for the owner or a session with a disposable repository:**
1. Scaffold `fsharp-limen-web` with Conditor at this PR.
2. Push 5, then 15, empty-diff commits 10 s apart to `main`, and separately to one pull-request branch.
3. Dispatch `candidate-acceptance.yml` for the first commit while the bursts run.
4. From the Actions API, read run and job timestamps.
5. Pass criteria:
   - at most 1 running and 1 pending run per ref and workflow;
   - the candidate run completes;
   - time to first result on `main` stays within the tier-1 duration plus runner wait.

## 5. MSB3030 build race (assessment only)

**The fixes:**
- **8a13e6a:** removed `AdditionalProperties="CausaTestBuild=…"`, which created a second global-property set sharing `obj/Release`.
- **daad1e5:** gave the Wasm project its own `bin`/`obj` per configuration.

**Assessment from the source (not run):**
- **Now isolated:** App, Persistence, Persistence.Tool and Wasm each use `$(Configuration)-test` output and intermediate paths when `CausaTestBuild=true`. App.Engine is referenced with `GlobalPropertiesToRemove`.
- **Still shared:** only `BaseIntermediateOutputPath` (`project.assets.json`). That is harmless, because `CausaTestBuild` does not change package references.
- **Unguarded:** nothing stops a future `AdditionalProperties` that has no output split.

**Not run:** no parallel-build reproduction. Rehearsal 7 starts from Conditor's scaffold, which has neither pattern.
