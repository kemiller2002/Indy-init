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

- **checked** means a compiler check or CI step verifies the stated property on every commit, and a negative control (in `V41Tests.fs` or CI) shows it rejects a broken input;
- **estimated** means the package author's planning estimate;
- **hypothesis** means only the event build can show it.

Nothing here is measured. No build has run against v4.1. A check verifies only what its title says. For example, X12 checks that a pair with several transitions has exactly the reviewed guards; it cannot decide whether two prose guards overlap.

## What v4.1 changes in v4, and what it does not

v4.1 changes no requirement text, obligation or test binding (X10). It does ask the owner to set aside three groups of clauses for November 7 (ODR-V41-06):

- REHEARSAL-AGENT-INTERFACE-FREEZE says F# signatures are authored at kickoff;
- KICKOFF-EXECUTION-PACKET §4 says parallel ownership starts after the kernel stabilizes;
- HACKATHON-BOUNDARIES quotes the published event rules, "the repository starts empty" and "code starts at kickoff". It also says to confirm with the organizers before producing scaffolding and to keep application source out until then. The T10 bootstrap makes the application repository non-empty before kickoff.

The owner stated in session on 2026-10-30 that the organizers allow pre-kickoff scaffolding. The owner's own record makes that authoritative.

## Where v4 left time on the table

| Gap in v4 | Effect during a timed build | v4.1 answer |
|---|---|---|
| Interfaces described as 12 boundaries, with no types | Producers and consumers negotiate types at T14, and consumers wait for implementations | Typechecked F# contracts for 8 interfaces, each with operations, success and failure. Each state and input union equals its v4 machine (X05). The contracts drive every multi-request flow:<br>• save, read-back and stale-head reads;<br>• automatic reconciliation reads (`ReconcileDue`);<br>• load reads (`InterpretLoad`) and conflict reads (`InterpretConflict`).<br>Every request carries the connection context (repository and opaque credential) and a tab-unique request id. CI refuses a forged `Confirmation` and a read of the credential's token |
| No file-level ownership; project files edited by whoever adds a file | Project-file and shared-file merge conflicts | 266 files, one owner each, including every contract, state-machine and unit test file. Integration writes every project file once, at bootstrap. Shared test support (stateful fake, recordings) is its own project |
| No generated starting point | The first hour is spent creating projects | `v41 --bootstrap` writes the .NET skeleton of the application repository and CI builds it from an empty directory. It covers:<br>• the solution, every project, the references and the full compile lists;<br>• the contracts in their producers' files;<br>• the layout-assertion module.<br>It does not write package references (ODR-V41-07), integration's other shared files (`package.json`, `Directory.Packages.props`, `build.fsx`, CI, `web/`) or test-framework wiring; integration adds those at T10 |
| State machines given as transition lists | Each agent works out which pairs are illegal and how to fail | Every (state, event) pair is decided (84 legal, 270 fail closed). The 11 multi-transition pairs match reviewed guard partitions. Unused events are listed for the owner (ODR-V41-08) |
| GitHub failure behaviour spread across PERSIST-008..016 | Errors diagnosed during the build | 15 error classes, each with:<br>• evidence that distinguishes it;<br>• a precedence rule;<br>• a typed error and a transition;<br>• UI text;<br>• a retry bound of its own kind;<br>• plan tests for its requirements |
| Forma named, not inventoried, layout untested | Rehearsal 7 used Forma tokens that do not exist; layout bugs found by eye | The layout contract is checked against the published 0.12.0 tarball. Every rule (FL-01..FL-10) has executable browser assertions for each invariant it states (23 assertions, axe-core 4.14.0 among them). CI runs them in Chromium and WebKit against:<br>• Forma's own pattern pages;<br>• pages composed from several patterns;<br>• conforming and broken fixtures.<br>The bootstrap puts the same module in the application repository for the plan's browser tests |
| 655 tests bound, with no run order; contract and machine tests unnamed | Setup decided ad hoc; conformance tests never written | 11 suites by environment. Each plan binding and each of the 139 contract, state-machine and unit tests is in exactly one suite, budgeted in a named task |

## v4 versus v4.1

| Measure | v4 | v4.1 | Kind |
|---|---|---|---|
| Requirements / P0 / obligations / P0 obligations | 520 / 161 / 2599 / 613 | same | checked (X10) |
| Verification plan | `VP-9817d9814482` | same file, same SHA-256 | checked (X10) |
| Plan tests / bindings | 655 / 779 | same; each binding in exactly one suite | checked (X09) |
| Interfaces with typechecked contracts | 0 | 8, plus domain stubs | checked (CI; X04, X05) |
| Empty application repository (.NET skeleton) builds | not defined | 173 generated files, written into an empty or git-initialised directory, build | checked (CI bootstrap step) |
| Files with a single owner | not defined | 266 | checked (X01–X03, X14) |
| State-machine pairs decided | 96 transitions listed | 354 pairs; 11 reviewed partitions; 1 unused event raised | checked (X12) |
| GitHub error classes | not enumerated | 15 | checked (X06) |
| Layout rules with executable assertions for each stated invariant | 0 | 10 of 10 rules, 23 listed invariants, 23 assertions, 97 broken pages | checked (X15 maps every invariant to its assertions; CI runs them) |
| Dependency critical path | 340 min | 300 min | estimated |
| B-single-lead (one lead agent) total, with 65 min reserves | 765 (does not fit 420) | 745 (does not fit) | estimated |
| A-role-lanes (one UI agent) total | 465 (does not fit) | 455 (does not fit) | estimated |
| C-role-lanes-two-ui (two UI agents) total | 405 (fits) | 385 (fits, 35 min slack); makespan 320 | estimated |
| Test setup | — | not claimed: v4 has no run plan to compare with | — |

The experimental target, a critical path under 300 minutes, is **not met**: the lane-free dependency critical path is 300 minutes, and topology C's makespan, which includes waiting for busy lanes, is 320. Earlier drafts reached 270–290 only with dependency drops and estimate cuts that review rejected. The target is not an acceptance condition.

### What each change contributes (schedule.json scenarios; estimated minutes)

| Scenario | Critical path | A | B | C |
|---|---|---|---|---|
| v4 | 340 | 465 | 765 | 405 |
| v4.1 orderings only (ODR-V41-03) | 305 | 465 | 765 | 395 |
| v4.1 estimates only (ODR-V41-04) | 330 | 455 | 745 | 395 |
| v4.1 | 300 | 455 | 745 | 385 |

## What changed in the schedule, and why

Each change and its rationale is in [analysis/task-adjustments.json](analysis/task-adjustments.json). X13 checks the following:

- every ordering v4 implied (its transitive dependencies) is kept, unless a waiver names it with a reason and an existing owner decision request;
- no estimate falls below half of v4's;
- every change is labelled an estimate.

**Orderings kept explicitly.** These held in v4 through T14 → T13:

- T12 (invariant tests) before T19 (first write code), as R4-01 and AR-022 require;
- T12 before review checkpoint T35;
- T13 (kernel) before T17 (checkpoint 1);
- T13 (kernel) before T21 (live integration).

**Orderings waived (ODR-V41-03, which depends on ODR-V41-06):**

| Ordering dropped | Why |
|---|---|
| T12 before T14, and T13 before T14 | The contracts are typechecked before kickoff, so the freeze needs neither the invariant tests nor the kernel |
| T13 before T19, and T13 before T20 | The save machine and the GitHub provider build on the frozen types and the stateful fake. They meet the real kernel at T21 |
| T13 before T35 | T35 reviews contracts and invariant tests; the kernel is reviewed at T30 and T36 |

**Estimates (ODR-V41-04).** Reductions:

| Task | Minutes (v4 → v4.1) | Reason |
|---|---|---|
| T12 | 25 → 15 | Invariants and machine tables are already compiled |
| T18 | 45 → 35 | The probe confirms a prepared classification |

Unchanged, because each now carries named test work:

| Task | Minutes | Work it carries |
|---|---|---|
| T14 | 20 | Producer and consumer contract tests |
| T16 | 35 | SM-CONNECTION and SM-NETWORK tests |
| T19 | 60 | SM-SAVE and SM-CONFLICT tests, 75 transitions |
| T20 | 40 | SM-LOAD tests |
| T22 | 70 | Restored after review RV41-3-9: the layout assertions do not shorten the views' work by a known amount |
| T31 | 45 | — |

Preflight T10 gets 25 minutes outside the window.

**Binding chains (estimated).** These are the riskiest dependencies, and any overrun on them moves the deadline. Each topology's chain is in `schedule.json` `topologies[].bindingChain`.

| Topology | Chain | Note |
|---|---|---|
| C | T14 → T12 → T13 → T17 → T23 → T26 → T29 → T28 → T31 → T32 → T33 → T34 | The ui-2 lane, with T23, T26, T29 and T28 back to back, sets the makespan |
| A | Same start, with the whole UI queue on one lane | 455, which does not fit |

The lane-free dependency critical path is T14 → T13 → T17 → T22 → T28 → T31 → T32 → T33 → T34.

## Checked improvements

These are verified on every commit, each with negative controls.

**Contracts:**

- The interface contracts compile together (CI).
- A forged `Confirmation` and a read of the credential's token in another file do not compile (CI, FS1093).
- Each machine's state and input unions equal its states and events (X05).
- Each interface (X04):
  - has operations with success and failure stated, and real concurrency and versioning rules;
  - is implemented by one file its producer owns;
  - has contract-test identities: producer tests in the producer's files, and a test for every consumer in that consumer's files.

**Project structure:**

- The predefined project structure bootstraps into an empty repository and builds (CI). This covers the .NET skeleton only: package references and integration's other shared files come at T10.
- Every file has exactly one owner (X01), and no file holds two owners' tests.
- Integration owns (X02, a fixed rule in the compiler):
  - project and solution files;
  - `Directory.*` build files and NuGet configuration;
  - package manifests, CI and deployment;
  - shared test fixtures;
  - composition (`View.fs`, `Program.fs`, the host and `web/boundary.js`).

  ui owns `web/causa.css` and `web/index.html` (T16).
- Source and test project references are acyclic, and compile order and helper order are fixed (X03).
- Every test identity names an owned file (X14).

**State machines (X12):** every pair is decided; multi-transition pairs match reviewed guard partitions; every state is reachable; unused events are listed with a decision.

**Error matrix (X06):**

- it covers every required class and timeouts;
- each class sharing a status names evidence the others lack, and a status or reason phrase does not count;
- each class's bound is of its own kind;
- each test verifies one of the class's requirements.

**Forma (X07, X15, CI):**

- The contract names only published custom properties, classes, patterns and breakpoints, and a colour only when it appears in the requirement document cited as its source (X07).
- The inventory equals what CI extracts from the tarball.
- Every layout rule is critical and has executable assertions for each invariant it states (X15), and the event-build copy of the module is an integration-owned file the bootstrap writes. CI runs them in Chromium and WebKit:
  - each passes on the listed Forma patterns at the listed viewports and colour schemes;
  - each fails on its broken fixture.
- Nine Forma gaps are recorded, all with one remedy that uses only Forma tokens (below).
- Every reuse pattern is also run for touch targets, horizontal scroll, scroll regions, clipping, contrast (light and dark), axe-core (light and dark) and tokens, at 320 px and desktop.

**Preservation:** nothing in v4 was removed or weakened (X10), and every binding is in exactly one suite (X09).

**Package integrity:** the package manifest digests every authored input and document, including AGENT-RULES.md and the layout kit. `--check` fails on a file in the package that the compiler neither generates nor reads.

## Found by the executable layout tests

**Forma 0.12.0 links are shorter than `--ef-layout-target-min` (44 px) in nine reuse patterns:** `navigation-shell` (26 px tall, at 320 px, both iPad sizes and desktop), and `cluster`, `diagram`, `landmark-region`, `record-header`, `relationship-index`, `sidebar`, `validation-summary` and `workspace-shell`. FL-01 has no exception for links in running text.

- **Remedy (in `formaGaps`):** `web/causa.css` gives every link `display: inline-flex` with `min-block-size` and `min-inline-size` set to `var(--ef-layout-target-min)`, using only the Forma token.
- **CI behaviour:** CI checks that each raw pattern still fails, so a Forma fix is noticed, and that each remedied pattern passes.
- **Contrast and axe-core:** all 44 reuse patterns pass in light and dark.
- **Next step:** report the gap to Forma's owner.

**Forma 0.12.0's `fault-banner` copy says "New diagnostics are queued locally".** PERSIST-032 forbids "queued" for Causa, so that page is not a positive page for `faultStatesPreserved`; PERSIST-031's copy for each fault state is (conforming fixtures).

## Estimated improvements

These are planning estimates:

- the critical path and the topology minutes above;
- each suite's fixture set-up count (`counts.fixtureSetups`), which is a planning count, not a saving.

## Hypotheses (only the event build can test them)

- **H41-01:** Bootstrapped project files and integration as the single writer of shared files reduce textual merge conflicts. Measured by `merge-conflicts`, `overlapping-files` and `ownership-violations`.
- **H41-02:** Frozen typechecked contracts, with producer and consumer contract tests, reduce semantic integration failures. Measured by `contract-test-failures`, `integration-test-failures` and `reverted-integrations`.
- **H41-03:** The error matrix shortens diagnosis of GitHub failures, and the executable layout assertions avoid layout rework. Measured by the defect log and CI timings.
- **H41-04:** Waiting time between agents falls because consumers wait for contracts, not implementations. Measured by `waiting-time`.

The telemetry spec ([analysis/conflict-telemetry.json](analysis/conflict-telemetry.json)) records each metric, or records it as null with a reason. It never infers a value.

## Risks

| Id | Risk | Mitigation |
|---|---|---|
| R41-01 | Task estimates are wrong. Topology C has 35 minutes of slack by the estimates | Reserves (RES-INTEGRATION 20 min, RES-FINAL 45 min) are unchanged. Topology A does not fit in any scenario |
| R41-02 | A contract frozen before the kernel exists needs to change | Change control through the coordinator. Every change to a union is breaking, and consumers' contract tests change in the same pull request |
| R41-03 | Replay integration problems surface at T21 instead of T19 | T21 depends on T13; checkpoint T36 follows T21 |
| R41-04 | The error matrix comes from documentation and Rehearsal 7 evidence, not a fresh live probe | T18 records live answers against the matrix. A mismatch is a matrix defect, fixed before T19 builds on it |
| R41-05 | Pre-kickoff scaffolding rests on the organizers' interpretation | ODR-V41-06 records the owner's statement; the owner's record names who confirmed it |
| R41-06 | Integration owns 67 test files, on a lane that also does every merge. These are tests of requirements only integration tasks cover: 19 PF, 18 PERSIST (including P0 PERSIST-044, -045, -047 and -054), 11 EXT-DATA, 8 VER, 4 AR, 2 WDL, 1 UI, and 4 contract tests | The owner may re-split before T0 by recompiling. Integration's timed estimates are unchanged |
| R41-07 | The compiler changed, so v4's compiler test record and freeze manifest were regenerated (only `compilerTreeSha256` and the summary changed) | v4's requirement artifacts and plan are unchanged. An owner record naming the old manifest SHA must be rewritten (FREEZE.md) |
| R41-08 | UI-016's amber `#E9A23B` is not a Forma value | ODR-V41-05 |
| R41-09 | No test framework or Playwright version is locked, so the bootstrap builds test projects without one | ODR-V41-07 |
| R41-10 | `Confirmation.ofReadBack` can still be called with the same bytes twice | The plan's INV-01 tests (Mutation.INV01.ReadBackSkipped, Property.INV01) guard that path |
| R41-11 | The layout assertions are DOM checks in a real browser, not proofs. Known limits, each a possible miss:<br>• `textContrast` skips text over images and gradients, and axe-core leaves those for manual review;<br>• `focusOrder` compares Tab order with DOM order, not with visual order (a `flex-direction: row-reverse` row is missed), and requires every control to be reached only on pages with at most 25;<br>• `touchTargets` cannot see a click handler added by script to an element with no role, tabindex, `onclick` attribute or pointer cursor, and counts a `gridcell` only when it is focusable;<br>• `faultStatesPreserved` reads the fault's own message (not its actions, diffs or code) against a fixed vocabulary built from PERSIST-031's copy: loss is a past or present statement about the tab's own work (your, some, all or the last changes, edits, work, draft, attachments or notes), not a condition (if, will, when it closes) and not another writer's change; PERSIST-032's forbidden claims (queued, saved or kept locally or on the device, saved or synced later) fail. Wording outside this vocabulary may be misjudged either way (a missed loss, or a correct sentence reported as loss), so the plan tests check the exact PERSIST-031 copy;<br>• an operation status headed Unknown commit outcome (or under a lozenge saying so) that says the save may or may not have reached GitHub is not a fault, so the fault assertions do not judge it; on a page with no other fault they return only "no fault on the page", and a plan test does not run them on that state;<br>• `dialogFocus` finds invokers that declare their dialog (`commandfor`, `popovertarget`, `aria-controls` or a handler attribute naming it); AGENT-RULES requires that, or the plan test names the invoker;<br>• `navigationCollapses` accepts any navigation inside a Forma pattern that carries navigation (site header, workspace shell, landmark region, breadcrumb, sidebar and the like);<br>• `statusVisible` requires the indicator's first 48 px to be shown, so a tall operation status may continue below the fold;<br>• `indicatorComposed` reads the state from the lozenge's text, which must be exactly one of PERSIST-030's seven state names;<br>• `faultActionable` asks no action of an Offline fault (PERSIST-031 offers Save only after the connection returns);<br>• `reducedMotion` samples the page between 1.5 s and 1.9 s after load;<br>• `amberLabelled` accepts visible text in the amber element's parent as its label.<br>**Measured catch rate**, reproducible with `run.mjs --catch-rate` over [layout-tests/catch-rate.mjs](layout-tests/catch-rate.mjs) (every defect and false-failure page from review rounds 5 to 10, each classified). A run counts as caught only when the defect's own assertion reports something other than a missing page element. Chromium: 280 of 287 violation runs caught. The 7 others are the disclosed misses: `row-reverse` (3 runs), text on a gradient (1), a script-only click handler (2) and a loss worded outside the vocabulary (1). Another 30 runs are not judged violations, each with its reason in the file: 29 do not violate the rule, and one, a screen at 200% text whose operation status starts at the fold, depends on the installed fonts (it passes here and fails on CI's fonts), and all 216 conforming runs pass (6 of them, pages whose only status is Unknown commit outcome while checking, return only "no fault on the page"), among them PERSIST-031's copy for every fault state, every save state's header, script-opened dialogs and a full investigation screen through every screen assertion. (Round 6 recorded a rule-level count, 114 of 127, which counted failures of unrelated assertions; it is superseded) | Every page the review classified as a violation is either caught or names its R41-11 entry, and every conforming page passes; CI runs the measurement and fails otherwise. Every defect page found in review is checked in CI: as a broken page that must fail (97), or through `--catch-rate` (caught, or a disclosed miss) |

## Finding F41-01 (resolved at the owner's direction)

`docs/FSHARP-TYPE-SHAPES.md` contradicted ADR-R7-001 and PERSIST-003 in two places:

- §42 defined a store port with `ProviderConflict`;
- §46 listed `LocalStore.fs`.

At the owner's direction (2026-10-30), both sections now carry ADR-R7-001 supersession notes pointing to the v4.1 contracts and project structure, and `docs/rehearsal-007/traceability.json` lists them. The R7 persistence validator passes. ODR-V41-02 asks the owner to confirm.

## Review

| Round | Verdict | Findings |
|---|---|---|
| 1 | FAIL | 1 critical, 7 major, 8 minor |
| 2 | FAIL | 5 major, 6 minor |
| 3 | FAIL | 3 major, 6 minor |
| 4 | FAIL | 1 major, 6 minor |
| 5 | FAIL | 1 major, 1 minor |
| 6 | FAIL | 4 major, 1 minor |
| 7 | FAIL | 7 major, 2 minor |
| 8 | FAIL | 5 major, 4 minor |
| 9 | FAIL | 1 major, 4 minor |
| 10 | FAIL | 1 major, 4 minor |

This revision addresses all ten rounds; see [independent-review.md](independent-review.md).
