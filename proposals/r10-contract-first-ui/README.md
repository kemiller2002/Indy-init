# Contract-first parallel UI development (proposal)

**Status:** PROPOSAL, for the next approved execution plan.
- It is not Rehearsal 010 and does not start it.
- It changes no frozen package, lock, scope, launch instruction, requirement, journey or gate.
- Everything lives under `proposals/r10-contract-first-ui/`.
- Adoption needs the owner decisions in [OWNER-DECISIONS-REQUIRED.md](OWNER-DECISIONS-REQUIRED.md), chiefly CF-09 and CF-07.

## The idea in one paragraph

UI agents today wait for integration checkpoint T17, because every UI slice depends on it and the frozen view contract offers nothing to build against. This package supplies that missing piece:
- a small set of presentation-facing F# contracts layered on the frozen v4.1.4 boundary types;
- a fixture-backed implementation of them, built from real causa-data;
- a file layout in which UI agents never edit the same file.

With it, a UI agent can build, render and test a real screen from bootstrap onward. **Merging still waits** for the live data path the governed journeys need, so the saving is earlier and parallel *building*, bounded by the integration queue.

## What is here

| Path | What |
|---|---|
| [SCREEN-MANIFEST.json](SCREEN-MANIFEST.json) | Every screen, region, field and action (all 44 catalog entries, 25 non-catalog entries each with its source, all 30 in-scope UI-0xx requirements), with routes, states, view models, Forma patterns, accessibility, responsiveness, requirement and obligation references, verification layers and owning role |
| `contracts/` | `Causa.Ui.Contracts`: 12 F# files over the frozen IF41 types, **linked from `requirements-compiled/v4.1.4/contracts`, never copied** |
| `fixtures/` | The fixture `UiBackend`, 9 scenarios, and `CausaDataExtract.g.fs`, generated verbatim from kemiller2002/causa-data at `e8259df` by `tools/extract-fixtures.fsx` |
| `prototype/screens/` | The Connection and Investigation List screens, as pure `AppView -> Bindings` modules. They reference **only** the contracts |
| `prototype/web/`, `prototype/host/` | The screen rendered by the real Limen 0.9.1 kernel with Forma 0.12.0. A pass-through transport carries messages to the F# fixture backend |
| `real-adapter-sketch/` | The same `UiBackend` over the frozen `Engine`, with the exact gaps the frozen contracts leave stated as values |
| `tests/` | Layer A: 44 xunit tests (including regressions for the review findings), plus 9 mutation controls (`evidence/mutations.log`) |
| `evidence/` | Layer A and B logs, and the bootstrap-compile proof |
| [FILE-OWNERSHIP.json](FILE-OWNERSHIP.json), [PARALLEL-EXECUTION-PLAN.md](PARALLEL-EXECUTION-PLAN.md) | One owner per file, three UI roles mapped to PR #119's successor slices |
| [VERIFICATION-STRATEGY.md](VERIFICATION-STRATEGY.md) | Layers A, B and C, and what each may and may not claim |
| [SCHEDULE-INPUTS.md](SCHEDULE-INPUTS.md), `schedule-inputs/`, `tools/contract-first-bound.fsx` | Prerequisite chains, model results, and the machine-readable inputs for the optimization agent |
| [ADVERSARIAL-REVIEW.md](ADVERSARIAL-REVIEW.md), [OWNER-DECISIONS-REQUIRED.md](OWNER-DECISIONS-REQUIRED.md), [FINAL-REPORT.md](FINAL-REPORT.md) | Review, decisions and the answer to the mission's question |

## The three capability boundaries, in the types

| Boundary | Type | What the UI may claim |
|---|---|---|
| Data Visible | `Visible<'a>`: stored record or fixture | as stored; no replay |
| Data Understood | `Understood<'a>`: projected at an `AggregateVersion` by the domain engine, or fixture | the engine's interpretation |
| Data Editable | `UiIntent`, `FieldSpec`/`Validation`, `Submission`, `SavePresentation` | input, validation and save progress; correctness stays with the engine and persistence |

There is no conversion from `Visible` to `Understood`.

`AppView.Origin` is `FixtureBackend` or `IntegratedBackend`. Only an adapter over the real `Engine` reports the latter.

## Run it

```
export PATH=/root/.dotnet:$PATH                      # .NET SDK 10.0.402 (global.json)
dotnet test proposals/r10-contract-first-ui/tests     # Layer A, about 11 s from clean
cd proposals/r10-contract-first-ui/prototype/web
npm ci && npx playwright test                         # Layer B, Chromium, about 12 s
```

Regenerate the fixture extract and the model results:

```
dotnet fsi proposals/r10-contract-first-ui/tools/extract-fixtures.fsx -- <causa-data checkout> <commit> proposals/r10-contract-first-ui/fixtures/CausaDataExtract.g.fs
dotnet fsi proposals/r10-contract-first-ui/tools/contract-first-bound.fsx -- <slices.json> <roles.json> proposals/r10-contract-first-ui/schedule-inputs/hidden-merge-dependencies.json <current|contract-first> <ci>
```

## What this does not show

- That anything works against real GitHub.
- That Causa's kernel replays the histories.
- That a save persists.
- That a journey passes.

Those are Layer C, and they are untouched. Under v4.1.4, the historical list and content cannot be produced from real data at all until ODR-R10-25 (PR #119) is decided. `real-adapter-sketch` makes that limitation a value (`listGap`, `contentGap`), not a surprise.
