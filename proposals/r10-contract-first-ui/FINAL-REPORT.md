# Final report: contract-first parallel UI development

**Every duration here is either measured on this sandbox (compile and test times) or a model result from estimates (schedule).** Each is labelled. Rehearsal 010 was not started, and nothing frozen, approved or owner-controlled was changed: `git diff --stat main...HEAD` touches only `proposals/r10-contract-first-ui/`.

## The question

> How early can UI agents begin building real screens when all required interfaces, fields, actions and ownership are predefined, and what prevents them from beginning immediately after bootstrap?

### Answer

**UI agents can begin building real screens immediately after bootstrap: T+29 on the DAG estimate for T10, T+45 with the plan's 45-minute set-up.**

This is verified:
- `evidence/bootstrap-compile.log` bootstraps an empty repository with v4.1.4 exactly as CI does.
- It drops the 12 contract files and the two prototype screens unchanged into `src/Causa.App/`, and `Causa.slnx` builds with 0 warnings and no domain, persistence or GitHub implementation.
- At T10 the bootstrap already writes the frozen IF41 types into `Engine.fs` and `Projections.fs`, and the view files are empty stubs.
- So nothing in the *compiler* stops a screen at T10.

Today the first non-shell UI slice starts at **T+189 (0-min CI) or T+253 (10-min CI)** (model). Five things hold it back, each verified:

| # | What prevents it today | Evidence | Removed by |
|---|---|---|---|
| 1 | **The start rule.** Every UI slice except T16/T24 depends on SL-T17, which needs the T13 kernel, and STARTUP-INSTRUCTIONS §4 starts only slices whose dependencies are *integrated* | `execution-dag.json` T22 ← T17 ← T13; `slice-execution-manifest.json` | CF-09: start on built UI predecessors after bootstrap; merge unchanged |
| 2 | **Nothing to build against.** IF41-VIEW has three records (save indicator, header, screen); every content payload is an opaque `class end` stub | `requirements-compiled/v4.1.4/contracts/07-IF41-VIEW.fs`, `00-DOMAIN-STUBS.fs` | `contracts/`: screen view models, capability boundaries, validation, intents, plus the fixture backend |
| 3 | **Ownership.** All investigation views are one `ui` file (`Views/Investigation.fs`); all markup is one integration-owned `web/index.html`; `Update.fs` is the only route from a view to the engine | `analysis/project-structure.json` | FILE-OWNERSHIP.json (101 files, one owner each) and CF-08 (per-screen fragments) |
| 4 | **Merging needs the live path, not T17.** The slice gate requires every bound journey assertion on both engines against api.github.com. Almost every UI slice's assertions connect, start and save, so they need SL-WI-GEN-EXT-DATA, SL-T21 and often SL-T25. Under v4.1.4 they also need ODR-R10-25, because the frozen types cannot represent causa-data | `Ledger.fs` lines 391–410; `schedule-inputs/hidden-merge-dependencies.json`; `real-adapter-sketch` `listGap`, `contentGap` | Not removable by UI work. ODR-R10-25 (PR #119) |
| 5 | **A cycle in the approved bindings.** SL-T16 (shell) and SL-T19 are bound to assertions that need slices depending on them, so as specified neither can merge | `tools/contract-first-bound.fsx` cycle detection; OWNER-DECISIONS CF-07 | CF-07 |

Points 1–3 are what contract-first removes. Points 4–5 decide when the early-built screens can **integrate**. They are why, at 10-minute CI, full release does not come earlier in any variant (model: T+846 throughout). The serial integration queue, not UI building, is then the constraint.

## Acceptance criteria

| Criterion | Met? | Evidence |
|---|---|---|
| Every required screen and field is accounted for | **Yes** | `ManifestTests`: all 44 catalog entries on their catalog screen with name, component, required flag and validation copied; all 30 in-scope UI-0xx requirements and every P0 journey assertion placed; F# ids equal manifest ids. Mutant M1 killed |
| The UI contracts compile | **Yes** | `evidence/layer-a.log` (8.6 s clean); also inside the real bootstrap (`evidence/bootstrap-compile.log`) |
| At least one fixture-backed screen path is demonstrated | **Yes** | investigation-list with its connection region: Layer A scenarios; Layer B on real Limen 0.9.1 and Forma 0.12.0, 7/7 including axe-core and the governed journey locators (`evidence/layer-b.log`) |
| UI implementation can begin without waiting for the domain engine | **Yes, for building** | the screens project references only the contracts (`BoundaryTests`, M4/M4b); it compiles in the bootstrap with domain stubs only. **Not for merging** (see the answer, points 4–5) |
| Independent UI file ownership is defined | **Yes** | FILE-OWNERSHIP.json; `OwnershipTests` (unique owner, no integration-reserved path owned by UI, every proposed UI slice implemented by its role's files). M7 killed |
| Required final verification remains intact | **Yes** | VERIFICATION-STRATEGY.md: Layer C unchanged. Fixture views carry `FixtureBackend` (M8 killed). No gate, journey, test binding or acceptance asset touched |
| Proposed schedule changes are explicit and measurable | **Yes, as model inputs** | SCHEDULE-INPUTS.md: chains, start and merge conditions, ownership diff, overhead estimates, risks; `schedule-inputs/*.json` in the optimizer's `slice-roles/1` format; `contract-first-bound.fsx` reproduces every figure |
| The optimization agent can incorporate the results without conflicting edits | **Yes** | nothing under `docs/rehearsal-010-optimization/` or `tools/r10/` is edited; inputs are keyed by its successor slice ids; the needed model feature (`buildDependsOn`/`mergeDependsOn`) is specified |

## Is it simpler or faster? (mission §11)

**Simpler for UI agents: yes.**
- The contract is 12 small files of records and unions with no runtime and no framework.
- Validation mirrors `Limen.Forms`. Bindings mirror Limen's `ViewValue`. Navigation is the catalog's routes.
- A screen is a pure `AppView -> Bindings` function plus an HTML fragment.

**Faster: only partly, and the model says exactly where.**

| | Effect (model, successor slices) |
|---|---|
| First UI slice start | **T+189/253 → T+45** in every variant |
| Full completion, 10-min CI | **no change** (T+846) for 2 or 3 UI agents; the integration queue decides |
| Full completion, 0-min CI | T+693 → T+437 (2 UI agents) |
| Data Editable, 0-min CI | T+693 → T+433 (2 UI) / T+343 (3 UI) |
| Data Visible, 10-min CI | T+736 → T+591 |
| Approved 37-slice plan, 0 / 10-min CI | T+595 → T+453 / T+690 → T+696 |

**Recommendation:**
1. **Adopt the contract package and fixture backend** (CF-09) in the next successor package. It costs about 5 minutes at bootstrap (estimate), it is verified to compile in the real structure, and it makes early, parallel, reviewable UI work possible.
2. **Fix CF-07 first.** The cyclic journey bindings stall a run under any plan.
3. **Use two UI agents**, not three. Three pay off only with speculative verification (ODR-R10-22), and the optimization agent's model should confirm that with rework and overhead before anyone relies on it.
4. **Do not expect contract-first alone to shorten release at realistic CI.** Its value is earlier capability (Data Visible and Data Editable) and reduced UI idle time. Turning that into earlier release needs ODR-R10-22.
5. **None of this delivers historical browsing under v4.1.4.** ODR-R10-25 remains the critical blocker.

## Integration handoff (concrete)

For the integration lane in a successor package:
1. At T10 bootstrap:
   - copy `contracts/{Ids,Types,UiStates,Navigation,Connection,InvestigationList,InvestigationDetail,InvestigationEditor,InvestigationAnalysis,System,UiActions,Bindings}.fs` to `src/Causa.App/Ui/Contracts/`, after `Projections.fs` in `Causa.App.fsproj` (as in `evidence/bootstrap-compile.log`);
   - copy `fixtures/*` to `tests/Causa.TestSupport/UiFixtures/`;
   - alias `BindingValue`/`BindingPrimitive` to `Limen.Contract.Core.ViewValue`/`ViewPrimitive` once EchelonFoundry.Limen.Contract is referenced.
2. Write `View.fs` once, with every `ScreenView` case wired to its role's view module, and `web/index.html` with one include per `web/screens/<screen>.html` (CF-08).
3. At T17, write `Ui/UiAdapter.fs` from `real-adapter-sketch/RealAdapter.fs`. Connect, navigation, Save and the seven save states map already. List and content become implementable when ODR-R10-25 lands (`ListInvestigations`, content payloads).
4. Swap the composition from `FixtureBackend.backend` to the adapter. The screen modules do not change.
5. Merge UI slices only on `slice-gate` PASS.

For the optimization agent: SCHEDULE-INPUTS.md §6 lists the four comparisons and the model feature they need.

## Time spent (this mission)

Agent execution, approximate:
- reading sources: about 45 minutes;
- prototype and initial validation (contracts, fixtures, screens, host, Layer A/B): about 90 minutes, at the upper end of the 60–90-minute timebox;
- schedule model, ownership, documents and review: the remainder.

The timebox was not exceeded to build more of the application. Only the two prototype screens exist.
