# v4.1 optimization report

v4.1 aims to cut the time an implementing team spends during the timed build on:

- architecture decisions;
- negotiating interfaces;
- merge conflicts;
- repeated integration;
- CSS and layout fixes;
- diagnosing GitHub errors;
- duplicated test setup;
- recovering from known failures.

It must do this without removing a requirement or weakening acceptance.

Every claim below is labelled:

- **checked** means a compiler check or CI step verifies the stated property on every commit, and a negative control in `V41Tests.fs` shows that the check rejects a broken input;
- **estimated** means the package author's planning estimate;
- **hypothesis** means only the event build can show it.

Nothing here is measured. No build has run against v4.1. A check verifies only what its title says. For example, X12 checks that a pair with several transitions has exactly the reviewed guards; it cannot decide whether two prose guards overlap.

## Where v4 left time on the table

| Gap in v4 | Effect during a timed build | v4.1 answer |
|---|---|---|
| Interfaces described as 12 boundaries, with no types | Producers and consumers negotiate types at T14, and consumers wait for implementations | Typechecked F# contracts for 8 interfaces. Their state and input unions are exactly the v4 machines' states and events (X05) |
| No file-level ownership; project files edited by whoever adds a file | Project-file and shared-file merge conflicts | 263 files, one owner each, including every contract, state-machine and unit test file. Integration writes every project file once, at bootstrap |
| State machines given as transition lists | Each agent works out which pairs are illegal and how to fail | Every (state, event) pair is decided (84 legal, 270 fail closed). The 11 pairs with several transitions match a reviewed guard partition |
| GitHub failure behaviour spread across PERSIST-008..016 | Errors diagnosed during the build | 15 error classes. Each has detection evidence, a precedence rule, a typed error, a transition, UI text, a per-class retry bound and plan tests |
| Forma named, not inventoried | Rehearsal 7 used Forma tokens that do not exist | Layout contract checked against the published 0.12.0 tarball: tokens, classes, patterns, breakpoints and colour literals |
| 655 tests bound, with no run order; contract and machine tests unnamed | Setup decided ad hoc; conformance tests never written | 11 suites by environment. Each plan binding and each of the 134 contract, state-machine and unit tests is in exactly one suite |

## v4 versus v4.1

| Measure | v4 | v4.1 | Kind |
|---|---|---|---|
| Requirements / P0 / obligations / P0 obligations | 520 / 161 / 2599 / 613 | same | checked (X10) |
| Verification plan | `VP-9817d9814482` | same file, same SHA-256 | checked (X10) |
| Plan tests / bindings | 655 / 779 | same; each binding in exactly one suite | checked (X09) |
| Interfaces with typechecked contracts | 0 | 8, plus domain stubs | checked (CI build of `contracts/`; X04, X05) |
| Files with a single owner | not defined | 263 | checked (X01–X03, X14) |
| State-machine pairs decided | 96 transitions listed | 354 pairs; 11 multi-transition pairs match reviewed partitions | checked (X12) |
| GitHub error classes | not enumerated | 15 | checked (X06) |
| Forma names or colours outside the published release | not checked | 0 (one requirement colour cited with its source, ODR-V41-05) | checked (X07) |
| Critical path | 340 min | 285 min | estimated |
| B-single-lead (one lead agent), total with 65 min reserves | 765 (does not fit 420) | 725 (does not fit) | estimated |
| A-role-lanes (one UI agent) | 465 (does not fit) | 440 (does not fit) | estimated |
| C-role-lanes-two-ui (two UI agents) | 405 (fits) | 380 (fits, 40 min slack) | estimated |
| Test setup | — | not claimed: v4 has no run plan to compare with | — |

The experimental target, a critical path under 300 minutes, is met by the estimates (285). It is not an acceptance condition.

### What each change contributes (schedule.json scenarios; estimated minutes)

| Scenario | Critical path | A | B | C |
|---|---|---|---|---|
| v4 | 340 | 465 | 765 | 405 |
| v4.1 orderings only (ODR-V41-03) | 305 | 465 | 765 | 395 |
| v4.1 estimates only (ODR-V41-04) | 325 | 440 | 725 | 390 |
| v4.1 | 285 | 440 | 725 | 380 |

## What changed in the schedule, and why

Each change and its rationale is in [analysis/task-adjustments.json](analysis/task-adjustments.json). Check X13 verifies four things:

- every ordering v4 implied (its transitive dependencies) is kept, unless a waiver names it with a reason and an owner decision;
- no estimate falls below half of v4's;
- every change is labelled an estimate.

**Orderings kept explicitly.** These held in v4 through T14 → T13:

- T12 (invariant tests) before T19 (first write code), as R4-01 and AR-022 require;
- T13 (kernel) before T17 (integration checkpoint 1);
- T13 (kernel) before T21 (live integration).

**Orderings waived (ODR-V41-03).** Each has its reason in the adjustments file:

| Ordering dropped | Why |
|---|---|
| T12 before T14, and T13 before T14 | The contracts are typechecked before kickoff, so the freeze needs neither the invariant tests nor the kernel |
| T13 before T19, and T13 before T20 | The save machine and the GitHub provider build on the frozen types and the stateful fake. They meet the real kernel at T21 |
| T12 before T35, and T13 before T35 | Checkpoint T35 reviews the interface freeze only |

**Estimates (ODR-V41-04):**

| Task | Minutes (v4 → v4.1) | Reason |
|---|---|---|
| T12 | 25 → 15 | Invariants and machine tables are already compiled |
| T14 | 20 → 15 | The types exist; producers commit them and write the contract tests |
| T16 | 35 → 30 | Forma patterns are named |
| T18 | 45 → 35 | The probe confirms a prepared classification |
| T22 | 70 → 60 | Forma patterns are named |
| T19 | 60 (unchanged) | Its transition tests are written there |
| T31 | 45 (unchanged) | An earlier cut rested on a setup saving the fixtures do not support (RV41-1-8) |
| T10 (preflight) | 25, outside the window | It writes every project file with its full Compile list |

**Topology C schedule (estimated minutes from T0):** see `schedule.json` `topologies`. The critical path is T12 → T13 → T17 → T22 → T28 → T31 → T32 → T33 → T34, which runs through the kernel, checkpoint 1 and the UI lane. These zero-slack edges are the riskiest dependencies. Any overrun on them moves the deadline minute for minute.

## Checked improvements

These are verified on every commit, and each check has negative controls:

- **The interface contracts compile together** (CI). Each state and input union equals its machine's states and events (X05). Each interface is implemented by one file its producer owns. Its producer tests are in files the producer owns, and every consumer has a contract test in a file it owns (X04).
- **Every file of the planned build has exactly one owner, and no file holds two owners' tests** (X01).
  - Integration owns:
    - project files and manifests;
    - CI and deployment;
    - composition (`View.fs`, `Program.fs`, the host);
    - `web/`.

    This is a fixed rule in the compiler, not an input (X02).
  - Source and test project references are acyclic, and compile order is fixed (X03).
  - Every test identity names an owned file (X14).
- **Every state machine pair is decided.** Each pair with several transitions matches a reviewed guard partition, and every state is reachable (X12).
- **The error matrix covers every required class and timeouts (X06):**
  - where a status is shared, each class names non-status evidence that differs from every other class sharing that status;
  - retries stay within each class's own bound;
  - each test verifies one of the class's requirements.
- **The Forma contract names only published custom properties, classes, patterns and breakpoints**, and no colour without a requirement source (X07). The inventory equals what CI extracts from the tarball.
- **Nothing in v4 was removed or weakened (X10).** Every binding is in exactly one suite (X09).
- **The package manifest digests every authored input and document**, including AGENT-RULES.md. `--check` fails on a file in the package that the compiler neither generates nor reads.

## Estimated improvements

These are planning estimates:

- the critical path and the topology minutes above;
- each suite's fixture set-up count (`counts.fixtureSetups`), which is a planning count, not a saving.

The estimates depend on the task estimates in `task-adjustments.json`. These are the author's judgement, informed by Rehearsal 7's evidence files but not derived from measurements.

## Hypotheses (only the event build can test them)

- **H41-01:** Writing project files once at bootstrap, and having integration as the single writer of shared files, reduces textual merge conflicts. Measured by `merge-conflicts`, `overlapping-files` and `ownership-violations`.
- **H41-02:** Frozen typechecked contracts, with producer and consumer contract tests, reduce semantic integration failures. Measured by `contract-test-failures`, `integration-test-failures` and `reverted-integrations`.
- **H41-03:** The error matrix shortens diagnosis of GitHub failures, and the Forma contract avoids layout rework. Measured by the defect log and the CI timings of the event build.
- **H41-04:** Waiting time between agents falls because consumers wait for contracts, not implementations. Measured by `waiting-time`.

The telemetry spec ([analysis/conflict-telemetry.json](analysis/conflict-telemetry.json)) records each metric, or records it as null with a reason. It never infers a value.

## Risks

| Id | Risk | Mitigation |
|---|---|---|
| R41-01 | Task estimates are wrong, and the critical path has no slack | Reserves (RES-INTEGRATION 20 min, RES-FINAL 45 min) are unchanged. Topology C keeps 40 min of slack by the estimates. Topology A does not fit in any scenario |
| R41-02 | A contract frozen before the kernel exists needs to change | Change control through the coordinator. Every change to a union is treated as breaking, and consumers' contract tests change in the same pull request (AGENT-RULES.md) |
| R41-03 | Replay integration problems surface at T21 instead of T19 | T21 depends on T13. Reviewer checkpoint T36 follows T21 |
| R41-04 | The error matrix comes from documentation and Rehearsal 7 evidence, not a fresh live probe | T18 records live answers against the matrix. A mismatch is a matrix defect, fixed before T19 builds on it |
| R41-05 | FSHARP-TYPE-SHAPES §42 and §46 contradict the contracts (finding F41-01) | ODR-V41-02 asks the owner. The document was not edited |
| R41-06 | Test files go to the implementation role of the first covering task. Requirements covered only by integration tasks (mostly PF, EXT-DATA, AR) give integration 67 test files, on a lane that also does every merge | The owner may re-split before T0 by recompiling. Integration's timed estimates are unchanged |
| R41-07 | The compiler changed, so v4's compiler test record and freeze manifest were regenerated (only `compilerTreeSha256` and the summary changed) | v4's requirement artifacts and plan are unchanged. An owner record naming the old manifest SHA must be rewritten (FREEZE.md) |
| R41-08 | UI-016's amber #E9A23B is not a Forma value | ODR-V41-05 |

## Finding F41-01

`docs/FSHARP-TYPE-SHAPES.md` contradicts ADR-R7-001 and PERSIST-003 in two places:

- §42 defines `IInvestigationStore` with a `ProviderConflict`;
- §46 lists `LocalStore.fs`.

`docs/rehearsal-007/traceability.json` does not list either section as superseded. PERSIST-001 gives the persistence requirements precedence, so v4.1's IF41-STORAGE and IF41-SAVE are the Causa boundary. The document is unchanged, and ODR-V41-02 asks the owner to decide.

## Review

Independent review round 1 returned FAIL, with 1 critical, 7 major and 8 minor findings. This revision addresses them; see [independent-review.md](independent-review.md).
