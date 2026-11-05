# EXP-INDY-001 (CI-CADENCE-001): partial report

## Status: **PAUSED, NOT COMPLETED**

- **Why it stopped.** The owner's instruction, received during execution: "Stop EXP-INDY-001 at the next safe checkpoint … Release agent and runner capacity for EXP-INDY-002." The experiment did not fail. It was stopped before the integration checkpoint.
- **Restart.** It must not restart without explicit owner authorization. The planned FINAL-REPORT.md is **not produced**: no verdict (ADOPT / ADOPT WITH CONDITIONS / REJECT / INCONCLUSIVE) can be given from this run.

## Times and commits

| Item | Value |
|---|---|
| Preparation | 2026-11-05T21:02:02Z to 21:08:51Z (6m49s, outside the timebox) |
| T+0 | **2026-11-05T21:08:51Z** |
| Stop (safe checkpoint: every agent's work committed, no build running) | **2026-11-05T21:14:03Z** |
| Elapsed execution | **5 min 12 s of the 60-min timebox** (8.7 %) |
| Starting commit | `48823515234042b6c8010f4f1993a634fd14e7d8` (main) |
| `experiment/ci-cadence-001` | lead stub `365dba0`, fix `dd078b2`, merge of domain `a0834b7`, plus this documentation commit |
| `experiment/ci-cadence-001-domain` | `a0834b7` |
| `experiment/ci-cadence-001-ui` | `2168342`, which merges domain `a0834b7`. **Not integrated** into `experiment/ci-cadence-001`: integration was stopped by the owner instruction. |
| `experiment/ci-cadence-001-controls` | `dd078b2`; the controls left no committed changes |

None of this application code is merged to `main` or to any competition branch.

## What was measured (observed)
- **Baseline CI** ([BASELINE.md](BASELINE.md), [CI-RUNS.json](CI-RUNS.json)).
  - The first clean post-sharding Preflight tooling run (42105890806) took **18.3 min wall** and **60.5 runner-min**. Its r10-adversarial merge job waited **4.1 min** for a runner.
  - The critical path is now **fsharp-tooling (Forma layout assertions, 13.2 min)**, not the adversarial suite. The projected ~13 min is not confirmed.
- **Coverage finding.** No workflow builds or tests `preview/causa`. The required CI gives **no** coverage of the application code this experiment changed; that code is covered only by local suites.
- **Development, T+0 to T+5:12.**
  - 6 local commits across 3 branches and 2 integration events (ui merged domain at 21:13:25Z; the lead merged domain at 21:13:42Z), both with **no conflicts**.
  - New F# lines, excluding blanks and comments, tests included: lead 9, domain 116, ui 82, total **207**. Removed or rewritten: 15. Net **+192**.
  - F# integrated into `experiment/ci-cadence-001`: 125 (lead and domain).
- **Local verification.**
  - Engine tests went 31 → 67 (domain) → 81 (ui on top of domain), all passing.
  - A mutation check by the domain agent (`>` changed to `>=`) was caught by 8 tests.
- **Defects** ([DEFECTS.json](DEFECTS.json)): 2 compile defects (D-01 lead, D-02 ui).
  - Both were caught by the Level 1 local build.
  - Detection latency was about 8 s and about 90 s; repair took 31 s and 18 s.
  - None was found only by full CI, because no full CI ran.
- **Negative controls.** All four that were run detected their defect ([evidence/](evidence/)):

| Control | Defect introduced | Result |
|---|---|---|
| NC-1 | A failing unit test | The runner exits 1. |
| NC-3 | Required-check merge logic (`jq`) fed a FAILED or missing case, or a failure, cancelled or skipped shard | Refused (exit 1). |
| NC-4 | `catalog-acceptance` with an incomplete obligation, or one without a status | REFUSED, `CATALOG_OBLIGATION_FAILED`. |
| NC-5a | `catalog-acceptance` with a requirement left unjudged | REFUSED, `CATALOG_REQUIREMENT_UNJUDGED`. |

  Even a fully passing local verdict only reaches `SUPPORTING-ONLY` (`PRODUCER_NOT_GOVERNED`), never governed acceptance.
- **Compute** ([COMPUTE.json](COMPUTE.json)):
  - domain agent: 83,092 tokens, 12 tool uses, 162 s;
  - ui agent: 121,829 tokens, 23 tool uses, 229 s;
  - lead: tokens **unavailable**;
  - CI runner-minutes during the experiment: **0**.

## Acceptance status at stop
**0 of 6 targeted requirements accepted, and 0 independently verified.**

| Requirement | Status |
|---|---|
| INV-019, INV-020, INV-021, INV-022, INV-005, INV-016 | Implemented, with engine-level tests passing against the in-process FakeGitHub double. **Not accepted.** |
| INV-024, INV-026, INV-001, INV-002 | Partial. INV-002 has an open contract gap: the catalog makes the title optional, but storage requires a non-blank title. |

The six targeted requirements are not accepted because:
- the WebAssembly build, the architecture check A1–A7 and the browser test were never run on the candidate (`tests/start-form.mjs` was not written);
- no rendered `data-requirement-id` was verified;
- the independent verifier never ran.

No persistence result is real-provider evidence. See [REQUIREMENTS.json](REQUIREMENTS.json).

## Incomplete
- The T+45 integration checkpoint was not reached: the ui branch was not merged into the candidate.
- The full-CI dispatch was not made. Its wall time, runner-minutes and queue time for the experiment are unmeasured.
- Browser verification, A1–A7 and field-ID traceability in the rendered page are missing.
- NC-2 (interface incompatibility) and NC-5b (a missing `data-requirement-id` in the browser) were not run.
- Independent verification, throughput metrics, the four-hour forecast and the decision verdict are missing.
- **These figures must not be extrapolated.**
  - A 5-minute window says nothing about integration-defect rates at an hourly CI interval.
  - The early speed (domain scope done at T+4) reflects a deliberately small slice.

## Other findings
- **P-01.** `change-impact.fsx` does not recognise `preview/causa` paths and selects EVERYTHING, so it gives application work no focused Level 1 guidance.
- **P-02.** `read-path.mjs` is flaky through the container proxy, which is pre-existing.

## Capacity released
- Both agents have stopped, and the local servers and .NET build servers are shut down.
- No GitHub Actions run belongs to this experiment, so none was cancelled and no other work's checks were touched.
- No pull request is opened for the experiment branches, because a PR would trigger `pull_request` CI, which the stop instruction forbids.
