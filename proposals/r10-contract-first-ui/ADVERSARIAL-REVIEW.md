# Adversarial review

Two passes:
- **Self-review** while building: the mutation controls, and the defects the checks themselves exposed.
- **Independent review** by a fresh agent. It was given the code, the sources and the mission's §11 questions, but not this proposal's conclusions, and it edited nothing.

Every finding is listed with its disposition. "Fixed" means a test now covers it unless stated otherwise.

## Mission §11 questions

| Question | Verdict | Evidence |
|---|---|---|
| Does the contract library create unnecessary abstraction? | **Mostly no, with known overlap.** 12 files of records and unions; no runtime, framework or engine. One substitutable interface (`UiBackend`). `Bindings` equals Limen `ViewValue` exactly. Overlap: `Rule`/`FieldSpec`, `EditorDrafts` and `Submission` overlap `Limen.Forms` state and do not share its semantics (R5) | CF-12 |
| Do fixtures drift from real provider behaviour? | **They can, by design.** The fixture's Save goes straight to Saved, with no Saving step and no provider round trip. Its Connect answers at once. Data is not invented: re-running `extract-fixtures.fsx` at causa-data e8259df reproduces `CausaDataExtract.g.fs` exactly (independent check). Containment: `FixtureBackend` origin, Layer C unchanged | VERIFICATION-STRATEGY.md |
| Are required fields missing? | **No.** All 44 catalog entries compared field by field (screen, name, component, required, validation, verification): no differences (independent script and `ManifestTests`). Gaps the journeys need but the catalog lacks are listed, not invented (CF-02) | `ManifestTests`, M1 |
| Do UI modules depend on undeclared domain types? | **No.** The screens reference only the contracts (assembly metadata and project file); the contract surface is allowlisted | `BoundaryTests`, M3, M4, M4b |
| Do shared files still cause conflicts? | **Reduced, not zero.** Remaining hot spots: the integration lane (Engine.fs, Update.fs, View.fs, the adapter) and the coordinator-controlled contracts. R1 fixed the compile-order coupling | FILE-OWNERSHIP.json, `OwnershipTests` |
| Do parallel agents add more merge overhead than they save? | **The queue is the limit.** Built-but-unmerged UI time grows about fourfold. Contract-first is still earlier in every same-roles pair after the dependency correction, but the result is sensitive to the merge-dependency rules (R2, R3) | SCHEDULE-INPUTS.md §4 |
| Do tests falsely report integrated functionality? | **No finding** (independent). Every fixture view carries `FixtureBackend`; Layer B says "not a journey run". Weakness: `IntegratedBackend` and `Understood.projected` are public constructors, so the guarantee rests on review and tests, not the type system (R9) | M8 |
| Does the proposal violate frozen requirements? | **No.** `git diff --name-only origin/main...HEAD` lists only `proposals/r10-contract-first-ui/`. The frozen files are linked, never copied or edited. The offline suite passes on main and on this branch | `evidence/offline-suite.log` |
| Do the new contracts duplicate Forma or Limen? | Forma: no; the contracts name Forma patterns and render with Forma classes. Limen: the validation overlap above (R5) | CF-12 |

## Findings

| # | Source | Severity | Finding | Disposition |
|---|---|---|---|---|
| S1 | self (Layer B) | major | Ivory on UI-016 teal is 4.27:1 (axe, first render) | Fixed in the prototype with a derived token. Ownership is CF-11 |
| S2 | self (Layer B) | major | The Limen kernel calls `reportValidity()`, so HTML `required` silently blocked every submit, and validation is the engine's | Fixed: `aria-required` |
| S3 | self (Layer B) | major | Echoing input values through `data-bind-value` raced with in-flight responses and erased typed text | Fixed: inputs are not echoed. This is a lesson for every UI agent; recorded in PARALLEL-EXECUTION-PLAN.md step 2 via the prototype |
| S4 | self (compile) | minor | Contract cases `Loading`, `Loaded`, `RateLimited` and `NetworkUnavailable` shadowed frozen cases (`LoadState`, `ProviderError`) | Fixed: renamed `Waiting`, `Ready`, `ProviderRateLimited`, `ProviderUnreachable` |
| S5 | self (mutation) | minor | An unused ProjectReference survived the assembly-reference check (M4) | Fixed: the project file is also checked |
| S6 | self (model) | major | The slice gate's journeys rule plus the bindings yield **cyclic merge dependencies** (SL-T16, SL-T19 and mutual couplings). The independent review confirmed it is real | Owner decision CF-07 |
| R1 | independent | major | In FILE-OWNERSHIP.json the contracts compiled after `Projections.fs` (ui-b) and `Connection.fs` (ui-a), so lane edits sat upstream of the frozen contracts | Fixed: `Projections.fs` (frozen types) becomes integration-owned and compiles before `Connection.fs`; the contracts follow it. Verified in the real bootstrap (`evidence/bootstrap-compile.log`); new `OwnershipTests` check |
| R2 | independent | major | Schedule gains were misattributed: comparisons used the frozen-ownership baseline only; with the same roles, the current rule beat contract-first at 10-min CI; CF-10's "every variant T+846" was false | Fixed: same-roles rows for every variant (SCHEDULE-INPUTS.md §4). CF-10 and the final report restated. With the corrected dependencies (R3) the reversal disappears; the sensitivity is stated |
| R3 | independent | major | The hidden merge dependencies were wrong: J1-03 and J2-01..08 do not save, and UI-to-UI needs (start form, editors, T22) were missing. The model's `Some 0.0` for a UI slice's non-UI dependencies and its greedy cycle deferral were not disclosed | Fixed: rules re-derived per assertion; all results regenerated. Both assumptions now stated in SCHEDULE-INPUTS.md §2 |
| R4 | independent | major | Fixture bugs: Save details was unhandled (INV-034 never exercised); Disconnect discarded copies and reused identifiers; a link to a missing target "succeeded" silently | Fixed, with regression tests R4a–R4c. Save → Saved with no Saving step is documented as deliberate fixture behaviour |
| R5 | independent | minor | "Mirrors Limen.Forms field for field" was false (no `Pattern` there; different counting, trimming and error reporting) | Wording corrected in `Types.fs`, CF-12 and the final report |
| R6 | independent | minor | INV-011: `ContentForms.hypothesis` requires the explanation while the record action does not | `ContentForms.hypothesisOnRecord` makes the record-time rule explicit pending CF-03; the catalog rule is unchanged |
| R7 | independent | minor | `Navigation` omits the URL contract's `repo` and `at` parameters; `format (NotFoundRoute "investigations/new")` parses as StartRoute | **Open.** `repo` is needed once investigations live in more than one repository (C4, ODR-R10-25), and `at` belongs to S14 (T28). Recorded for the owner of `Routes.fs` (integration) |
| R8 | independent | minor | The Apollo 11 analysis fixture is a slice (1 of 12 hypotheses, 3 evidence items, 6 of 431 events) of the stored snapshot, shown as a ready projection with no "partial" marker; `mechanism` is mapped to INV-011 | **Open, disclosed.** It is labelled a fixture and is not historical completeness evidence. A `Partial` marker is a candidate contract addition |
| R9 | independent | minor | Adapter sketch: provider NotFound became `ProviderError`; save choices were dropped; start and content inputs were discarded | Fixed (NotFound → `InvestigationNotFound`, a shared `SaveChoices.forKind`, inputs kept); regression test R9. Public `IntegratedBackend`/`Understood.projected` stay public: F# has no cross-assembly friend scope here, so the guarantee is the M8 test plus review |
| R10 | independent | note | No repo-rule violation; the JavaScript is only the prototype page and its Playwright spec, outside `tools/` | none |
| R11 | independent | note | The reviewer did not reproduce `evidence/bootstrap-compile.log` | The log now gives the exact steps and states that the copied files are byte-identical to this commit (`cmp`) |

## Would I recommend against adopting it?

The mission asks to recommend against the architecture if it is not demonstrably simpler or faster.

- **It is demonstrably earlier for UI work.** This is verified by compiling in the real bootstrap, not modelled.
- **It is simpler for screen authors.**
- **It is not demonstrated to make release faster.**
  - The model shows gains, but they depend on merge-dependency rules that changed the sign of the result once already.
  - It omits rework and overhead.
  - Nothing fits 420 minutes at realistic CI.

The recommendation (FINAL-REPORT.md) is therefore:
- adopt the contracts and the ownership split for earlier, parallel building;
- fix CF-07;
- have the optimization agent's model confirm the integration-time effect before any schedule commitment depends on it.
