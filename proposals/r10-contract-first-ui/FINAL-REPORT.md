# Final report: contract-first parallel UI development

**Every duration here is either measured on this sandbox (compile and test times) or a model result from estimates (schedule).** Each is labelled. Rehearsal 010 was not started, and nothing frozen, approved or owner-controlled was changed: `git diff --stat origin/main...HEAD` touches only `proposals/r10-contract-first-ui/`.

## The question

> How early can UI agents begin building real screens when all required interfaces, fields, actions and ownership are predefined, and what prevents them from beginning immediately after bootstrap?

### Answer

**UI agents can begin building real screens immediately after bootstrap: T+29 on the DAG estimate for T10, T+45 with the plan's 45-minute set-up.**

This is verified:
- `evidence/bootstrap-compile.log` bootstraps an empty repository with v4.1.4 exactly as CI does.
- It drops the 12 contract files and the two prototype screens unchanged into `src/Causa.App/`, and `Causa.slnx` builds with 0 warnings and no domain, persistence or GitHub implementation.
- At T10 the bootstrap already writes the frozen IF41 types into `Engine.fs` and `Projections.fs`, and the view files are empty stubs.
- So nothing in the *compiler* stops a screen at T10.

Today the first non-shell UI slice starts at **T+189 (0-min CI) or T+252 (10-min CI)** (model). Five things hold it back, each verified:

| # | What prevents it today | Evidence | Removed by |
|---|---|---|---|
| 1 | **The start rule.** Every UI slice except T16/T24 depends on SL-T17, which needs the T13 kernel, and STARTUP-INSTRUCTIONS §4 starts only slices whose dependencies are *integrated* | `execution-dag.json` T22 ← T17 ← T13; `slice-execution-manifest.json` | CF-09: start on built UI predecessors after bootstrap; merge unchanged |
| 2 | **Nothing to build against.** IF41-VIEW has three records (save indicator, header, screen); every content payload is an opaque `class end` stub | `requirements-compiled/v4.1.4/contracts/07-IF41-VIEW.fs`, `00-DOMAIN-STUBS.fs` | `contracts/`: screen view models, capability boundaries, validation, intents, plus the fixture backend |
| 3 | **Ownership.** All investigation views are one `ui` file (`Views/Investigation.fs`); all markup is one integration-owned `web/index.html`; `Update.fs` is the only route from a view to the engine | `analysis/project-structure.json` | FILE-OWNERSHIP.json (101 files, one owner each) and CF-08 (per-screen fragments) |
| 4 | **Merging needs the live path, not T17.** The slice gate requires every bound journey assertion on both engines against api.github.com. A UI slice's assertions connect and start an investigation, and most also save and reload, so they need SL-WI-GEN-EXT-DATA, the start form, and often SL-T21, SL-T22 and SL-T25. Under v4.1.4 they also need ODR-R10-25, because the frozen types cannot represent causa-data | `Ledger.fs` lines 391–410; `schedule-inputs/hidden-merge-dependencies.json`; `real-adapter-sketch` `listGap`, `contentGap` | Not removable by UI work. ODR-R10-25 (PR #119) |
| 5 | **Cycles in the approved bindings.** 22 merge-dependency edges form cycles: SL-T16 (shell) and SL-T19 are bound to assertions that need slices depending on them, and slices bound to the same assertion need each other (EXT-DATA and INV-START both carry J1-03). As specified, the run deadlocks | `tools/contract-first-bound.fsx` cycle detection; OWNER-DECISIONS CF-07 | CF-07 |

- Points 1–3 are what contract-first removes.
- Points 4–5 decide when the early-built screens can **integrate**.
- With them modelled, contract-first is earlier in every same-roles comparison, but the gain at realistic CI is bounded by the serial integration queue. At 10-min CI with two UI agents, full completion is T+1064 → T+864, still far beyond 420 minutes.

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
- Validation is close to `Limen.Forms` (differences in CF-12). Bindings mirror Limen's `ViewValue` exactly. Navigation is the catalog's routes.
- A screen is a pure `AppView -> Bindings` function plus an HTML fragment.

**Faster: only partly, and the model says exactly where.**

| Same UI roles, current rule → contract-first (model, successor slices) | 0-min CI | 10-min CI |
|---|---|---|
| First non-shell UI slice start | T+189 → T+45 | T+252 → T+45 |
| Full completion, frozen ownership | T+800 → T+787 | T+1042 → T+998 |
| Full completion, 2 UI agents | T+840 → T+582 | T+1064 → T+864 |
| Full completion, 3 UI agents | T+691 → T+475 | T+1008 → T+812 |
| Data Editable, 2 / 3 UI agents | T+830 → T+486 / T+681 → T+382 | T+1064 → T+864 / T+1008 → T+812 |
| Approved 37-slice plan, frozen ownership | T+598 → T+462 | T+770 → T+709 |

Caveats:
- The first version of these figures, before the independent review corrected the merge dependencies, showed the current rule *ahead* at 10-min CI (2 UI agents: T+791 against T+846). The conclusion is sensitive to those rules.
- The model has no rework or per-slice overhead.
- The optimization agent has reproduced these comparisons in its own model (SCHEDULE-INPUTS.md §4b, claude/r10-optimization 4947573). The two models agree on direction. Absolute times differ with the CF-07 deferral policy.

**Recommendation:**
1. **Adopt the contract package and fixture backend** (CF-09) in the next successor package. It costs about 5 minutes at bootstrap (estimate), it is verified to compile in the real structure, and it makes early, parallel, reviewable UI work possible.
2. **Fix CF-07 first.** The cyclic journey bindings deadlock a run under any plan.
3. **Split UI ownership** (FILE-OWNERSHIP.json). The split, not contract-first alone, carries most of the gain: with the frozen ownership, contract-first saves only 13–44 minutes in this model.
   - The optimization agent reran the comparisons in its own model (SCHEDULE-INPUTS.md §4b). **Three UI agents plus speculative verification (ODR-R10-22, depth 3)** is then the only configuration that puts Data Visible, Understood and Editable inside 420 minutes at every CI value: T+278–380, with all slices integrated at T+435–518.
   - Without the speculative queue, use two UI agents.
   - Before relying on the gain, confirm it with `--slice-overhead` and `--fail-every`.
4. **Do not expect any of this to fit 420 minutes at realistic CI.** The best modelled case is T+812. Converting built-but-unmerged UI work (about four times as many slice-minutes) into integrated work needs ODR-R10-22.
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

## Correction: what the package carries into the competition repository (2026-11-05)

Found by the v4.1.5 session when it folded this file.

**The rule.** The v4.1.x compiler writes interface text **only into each interface's implementing file**. Every other planned file is bootstrapped as an empty owner stub. Under OD-INDY-22 ("code starts at kickoff"), code written beforehand cannot be carried in.

| Arrives at bootstrap | Must be written during the timed window |
|---|---|
| The IF41-VIEW presentation types, in `src/Causa.App/Projections.fs`: every view model, `UiIntent`, `UiBackend`, `RouteTarget`, the capability boundaries, and the 16 boundary members | `src/Causa.App/Ui/Presentation.fs`: validation, navigation, presentation language, form specs, action contracts, binding helpers, ids. About 474 non-blank lines in this proposal |
| | the UiFixtures helpers: about 600 non-blank lines (backend, scenarios and the causa-data extract) |
| | the adapter, about 110 lines in the sketch |
| | every screen and every test |

**What changes in the claims:**
1. **UI agents can still start right after bootstrap**, but only against the types. A screen module compiles against `AppView`, `UiIntent` and the field and action ids as types. The ids' *values* (`Fields.*`, `Actions.*`) and the catalog rules (`StartForm.fields` …) live in `Presentation.fs`, so they also arrive only when integration writes that file.
2. **Fixture-backed rendering needs a fixture first.**
   - Recommended: integration writes `Presentation.fs` as its first post-bootstrap task.
   - Each UI role writes a minimal fixture `UiBackend` for its own screens in its own `Scenarios.<role>.fs`, rather than waiting for a shared backend.
   - Not measured. Model it as a per-role set-up cost before the first fixture-backed screen.
3. **The Layer A and B evidence in this proposal** (45 tests, 9 mutants, 7 browser checks) shows that the contract design works and that the screens can be checked this way. **It is not competition evidence and does not carry over.** In the run, the same tests have to be written again against the code written at kickoff, and only Layer C judges acceptance.
4. **A route to carrying `Presentation.fs` text** would be either a compiler change, letting a successor carry producer text, or an owner decision that such reviewed helper code counts as package content under OD-INDY-22. Both are the owner's to decide. Neither is assumed here.
