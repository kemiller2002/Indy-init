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

Every number below is labelled:

- **proven** means a compiler check or CI step verifies it on every commit;
- **estimated** means the package author's planning estimate;
- **hypothesis** means only the event build can show it.

Nothing here is measured. No build has run against v4.1.

## Where v4 left time on the table

Package v4 fixed what must be built and how it is accepted. It left these open:

| Gap in v4 | Effect during a timed build | v4.1 answer |
|---|---|---|
| Interfaces described as boundaries (12 in `v4/interfaces.json`), with no types | Producers and consumers negotiate types at T14; consumers wait for implementations | Typechecked F# contracts for 8 interfaces (IF41-*), frozen before kickoff |
| No file-level ownership; project files edited by whoever adds a file | Project-file and shared-file merge conflicts | 242 files, one owner each; integration writes every project file once, at bootstrap |
| State machines given as transition lists | Each agent works out which pairs are illegal and how to fail | Every (state, event) pair decided: 84 legal, 270 illegal, all fail closed |
| GitHub failure behaviour spread across PERSIST-008..016 | Errors diagnosed during the build. Rehearsal 7 misread stale-head answers (R7 lessons) | 15 error classes with detection evidence, typed error, transition, UI, retry limit and tests |
| Forma named, not inventoried | Rehearsal 7 used Forma tokens that do not exist | Layout contract checked against the published 0.12.0 tarball: 352 properties, 168 patterns, 6 breakpoints |
| 655 tests bound, with no run order | Each test file sets up its own environment | 9 suites by environment, each binding in exactly one |
| Consumers waited for producers' implementations (T14 after T13) | Long critical path: 340 minutes | Consumers wait for contracts: 270 minutes (estimated) |

## v4 versus v4.1

| Measure | v4 | v4.1 | Kind |
|---|---|---|---|
| Requirements / P0 / obligations / P0 obligations | 520 / 161 / 2599 / 613 | same | proven (X10) |
| Verification plan | `VP-9817d9814482` | same file, same SHA-256 | proven (X10) |
| Bound tests / bindings | 655 / 779 | same; each in exactly one suite | proven (X09) |
| Interfaces with typechecked contracts | 0 | 8, plus domain stubs | proven (CI build of `contracts/`) |
| Files with a single owner | not defined | 242 | proven (X01–X03) |
| State-machine pairs decided | 96 transitions listed | 354 pairs (84 legal, 270 fail closed) | proven (X12) |
| GitHub error classes | not enumerated | 15 | proven (X06) |
| Forma tokens or patterns not in the published release | not checked | 0 | proven (X07, against the tarball in CI) |
| Critical path | 340 min | 270 min | estimated |
| B-single-lead (one lead agent), total with 65 min reserves | 765 (does not fit 420) | 700 (does not fit) | estimated |
| A-role-lanes (one UI agent) | 465 (does not fit) | 405 (fits) | estimated |
| C-role-lanes-two-ui (two UI agents) | 405 (fits) | 345 (fits, 75 min slack) | estimated |
| Environment setups for the bound tests | 163 (one per test file) | 9 (one per suite) | estimated (a count of setups, not minutes) |

The experimental target of a critical path under 300 minutes is met by the estimates (270). It is not an acceptance condition.

## What changed in the schedule, and why

Each change and its rationale is in [analysis/task-adjustments.json](analysis/task-adjustments.json). The compiler checks that each change is labelled an estimate (X13).

- **T14, the interface freeze: 20 → 5 minutes. It no longer waits for the kernel (T13).** The contracts are written and typechecked before kickoff, so the freeze is a copy and a build.
- **T13, the kernel, now implements the frozen IF41-DOMAIN contract.** It depends on T12 and T14.
- **T19, the save machine: 60 → 50 minutes. It no longer waits for the kernel.** In v4 it waited through T14. The decision table replaces reading prose.
- **T21, live integration, now also depends on T13.** Replay verification against the real kernel happens here.
- **Other estimate reductions, each for the stated reason:**

  | Task | Change (minutes) | Reason |
  |---|---|---|
  | T12 | 25 → 15 | Invariants and tables are already compiled |
  | T18 | 45 → 35 | The probe confirms a prepared classification |
  | T16 | 35 → 30 | Forma patterns are named |
  | T22 | 70 → 60 | Forma patterns are named |
  | T31 | 45 → 40 | Shared suite setup |

- **T10, bootstrap (preflight, before T0), gets an estimate of 25 minutes outside the window.** It writes every project file with its full Compile list.

Topology C schedule (estimated minutes from T0):

| Lane | Tasks |
|---|---|
| domain | T14 0–5, T12 5–20, T13 20–65, T15 65–85, T27 85–115 |
| persistence | T18 0–35, T19 35–85, T20 85–125, T25 125–145 |
| ui | T16 15–45, T22 60–120 |
| ui-2 | T23 60–90, T26 90–115, T29 115–135, T28 135–180 |
| integration | T11 0–15, T24 15–25, T17 45–60, T21 125–150, T31 180–220, T32 220–240, T34 260–280 |
| reviewer | T35 5–20, T30 150–170, T36 170–185, T33 240–260 |

The riskiest dependencies are the zero-slack edges of the critical path: T18 → T19 → T20 → T21 → T30 → T31 → T32 → T33 → T34. The persistence lane drives the build. Any overrun in the live GitHub probe (T18) or the save machine (T19) moves the deadline minute for minute.

## Proven improvements

Each of these is verified on every commit by the compiler or CI:

- **The interface contracts compile together**, in project compile order (CI builds `contracts/`).
- **Ownership is complete:**
  - every file of the planned build has exactly one owner;
  - project files, manifests, CI and deployment belong to integration;
  - compile order is fixed and acyclic (X01–X03);
  - every bound test names an owned file (X14).
- **The save-state interface has exactly SM-SAVE's seven states (X05).** Each interface is implemented by one file its producer owns (X04).
- **Every state machine is deterministic and complete (X12).**
- **The error matrix covers every required class (X06):**
  - it names non-status evidence wherever an HTTP status is shared;
  - it retries within the PERSIST-010/016 bounds;
  - it cites only plan-bound tests.
- **The Forma contract uses only published tokens, patterns and breakpoints (X07).** The inventory equals what CI extracts from the tarball.
- **Nothing in v4 was removed or weakened (X10, X09).**

## Estimated improvements

These are planning estimates:

- the critical path and topology minutes above;
- the setup count of the verification execution plan.

They depend on the task estimates in `task-adjustments.json`. These are the author's judgement, informed by Rehearsal 7's evidence files but not derived from measurements.

## Hypotheses (only the event build can test them)

- **H41-01:** Writing project files once at bootstrap, and having integration as the single writer of shared files, reduces textual merge conflicts. Measured by `merge-conflicts`, `overlapping-files` and `ownership-violations`.
- **H41-02:** Frozen typechecked contracts reduce semantic integration failures. Measured by `contract-test-failures`, `integration-test-failures` and `reverted-integrations`.
- **H41-03:** The error matrix shortens diagnosis of GitHub failures, and the Forma contract avoids layout rework. Measured by the defect log and the CI timings of the event build.
- **H41-04:** Waiting time between agents falls because consumers wait for contracts, not implementations. Measured by `waiting-time`.

The telemetry spec ([analysis/conflict-telemetry.json](analysis/conflict-telemetry.json)) records each metric, or records it as unavailable with the reason. It never estimates a missing value.

## Risks

| Id | Risk | Mitigation |
|---|---|---|
| R41-01 | Task estimates are wrong. The schedule gain is an estimate, and the persistence lane has no slack | Reserves (RES-INTEGRATION 20 min, RES-FINAL 45 min) are unchanged. Topology C keeps 75 min of slack by the estimates |
| R41-02 | A contract frozen before the kernel exists needs to change | Change control through the coordinator. Consumers' contract tests run in the same pull request (AGENT-RULES.md) |
| R41-03 | Replay integration problems surface at T21 instead of T19 | T21 depends on T13; reviewer checkpoint T36 follows T21 |
| R41-04 | The error matrix describes GitHub behaviour from documentation and Rehearsal 7 evidence, not from a fresh live probe | T18 records live answers against the matrix. A mismatch is a matrix defect, fixed before T19 builds on it |
| R41-05 | FSHARP-TYPE-SHAPES sections 42 and 46 contradict the contracts (finding F41-01) | ODR-V41-02 asks the owner. The document was not edited |
| R41-06 | `agent-file-ownership.json` assigns acceptance test files by the first covering task, which may not match how the team splits work | A file's owner can be changed before T0 by recompiling. The compiler checks uniqueness, not the split itself |
| R41-07 | The compiler tree changed, so v4's compiler test record and freeze manifest were regenerated. Only `compilerTreeSha256` and the summary changed | v4's requirement artifacts and plan are unchanged; `v4 --check` passes |

## Finding F41-01

`docs/FSHARP-TYPE-SHAPES.md` contradicts ADR-R7-001 and PERSIST-003 in two places:

- §42 defines `IInvestigationStore` with a `ProviderConflict`;
- §46 lists `LocalStore.fs`.

`docs/rehearsal-007/traceability.json` does not list either section as superseded. PERSIST-001 gives the persistence requirements precedence, so v4.1's IF41-STORAGE and IF41-SAVE are the Causa boundary. The document is unchanged, and ODR-V41-02 asks the owner to decide.
