# Rehearsal 010: integration architecture

**Finding first.** The frozen v4.1.4 package already predefines most of what this optimization asks for:
- the project graph;
- every project's complete compile list, written at bootstrap and never edited later (`analysis/project-structure.json`);
- one owner per file;
- an integration role that alone writes the shell, the entry points and every project and solution file (`agent-file-ownership.json` `rules[4]`, X02/X18);
- frozen IF41 contracts between the layers.

No new architecture is needed. What remains are two serialization points: one inside a role, and one in the gate.

## Structure (frozen, unchanged)

```
Causa.Host (integration)                     ← the only process entry
  └─ Causa.App: View.fs, Program.fs (integration)   ← composition: wires the implementations together
       ├─ IF41-APP / IF41-VIEW / IF41-REPORT     (frozen contracts; coordinator change control)
       ├─ Connection.fs, Routes.fs, Shell.fs, Dashboard.fs           (ui)    connection and browser UI
       ├─ Investigation.fs, SaveIndicator.fs, ConflictReview.fs       (ui)    investigation editor UI
       ├─ Graph.fs, Report.fs, History.fs, Administration.fs          (ui-2)  analysis UI
       ├─ Engine.fs, Update.fs, Projections.fs                        (ui)    ← one frozen Update: Msg -> Model
       └─ Causa.Persistence (persistence)                             GitHub data adapter, save and load machines
            └─ Causa.Domain (domain)                                  domain engine; references nothing
```

`INTEGRATION-OWNERSHIP.json` checks each module's paths against the frozen manifest. All match. The only exception is the `.fsproj` inside each project directory, which the manifest gives to integration, as intended.

## Sequence by which independently compiled modules become one application

1. **T0 + bootstrap (T10, integration).**
   - `requirements-compiler v41 --bootstrap` writes every project, the complete compile lists and the contract stubs. Measured: 2.1 s.
   - The tree builds at once against the stubs. Measured: 18.6 s cold, 11.4 s warm.
2. **Each role replaces its own files only.** Each file compiles against the stub signatures of earlier projects (F# compile order). A UI view therefore compiles **before** the domain is implemented, as long as the stubs carry the frozen types, which they do.
3. **Interface freeze (T14).** Producers' contract tests, then consumers' contract tests (`Causa.Contract.Tests`).
4. **Composition.** Integration wires the implementations in `View.fs` and `Program.fs` through the frozen types (`Connector`, `Execute`, `Classifier`, `Update`). No implementation agent edits those files.
5. **Each slice merges in order** through the governed gate, which is the full plan (VERIFICATION-PIPELINE.md, Level 3).

## Evidence (reproducible)

| Property | Result | How |
|---|---|---|
| UI contracts compile independently | PASS | `ContractCheck.fsproj` builds the frozen IF41 files alone (6 s here; 11 s in CI step "v4.1.4 interface contracts typecheck", run 42004659389) |
| Fixture-backed screen code compiles without the domain engine | PASS | A screen function written only against `Connector` and `Execute` compiles beside the contracts, with no domain implementation (scratchpad demonstration; see INTEGRATION-CONTRACTS.md) |
| A GitHub adapter replaces a fixture without changing screen code | PASS | The same `Screen.connect` compiles with `Fixture.connector` and `GitHub.connector` |
| The shell can reference independently built modules | PASS | The bootstrapped solution builds, with `Causa.Host → App → Persistence → Domain` |
| Contract drift fails at compile time | PASS, **conditionally** | An adapter returning `bool` for `ConnectResult` fails with FS0001. Screen code matching a non-existent case fails with FS0049/FS0026 **only because `TreatWarningsAsErrors` is on**. With it off, the same code compiled with exit 0, and the bad pattern silently became a catch-all variable |

**Consequence for the plan.** `TreatWarningsAsErrors=true` in the bootstrapped `Directory.Build.props` (integration-owned) is load-bearing for contract safety, so it must never be relaxed during the run. A change to that file selects every test in the change-impact matrix (`CI-ALL-GOVERNANCE`).

## What the frozen design does not give: the two serialization points

1. **`Update.fs`, `Engine.fs` and `Projections.fs` (ui).** Every UI stream (connect, list, editor, save indicator, analysis wiring) adds `Msg` handling to the one frozen `Update`. They are one owner's files, so there are no cross-agent conflicts. But all UI work funnels through one agent, the largest lane load in every model run.
2. **The full governed gate on every slice merge.** See VERIFICATION-PIPELINE.md.

Contract-first UI (Agent 2's proposal, when published) attacks the first. It is modelled here as UI slices that start after T16 instead of T17 (E3).

## Coordination with Agents 2 and 3

At the time of writing, no PR or branch from either agent exists. These are the conditions for incorporating their work.

- **Agent 2 (contract-first UI).** Typed UI contracts and fixtures are acceptable only as part of a frozen successor package. OD-INDY-22 forbids contest application code written before T0, and that includes fixture implementations meant to be reused in the run. Contracts that are frozen package content, as the IF41 files are today, are allowed.
- **Agent 3 (historical data compatibility).** The same applies to the decoder and replay adapter: they can enter the run only as successor-package contracts and stubs, or as a specification, never as pre-written implementation. Its successor contract is exactly ODR-R10-25.
- **Potential conflict to watch.** Both agents may extend `CoreEvent` (Agent 3) and the `Msg` and `Model` of IF41-APP (Agent 2). Both live in one frozen union and one `Update`. Their successor contracts must be compiled together into **one** v4.1.5 package, not two.
