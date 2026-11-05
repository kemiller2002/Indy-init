# Verification strategy: three layers

> **Correction (OD-INDY-22):** this proposal's Layer A/B code and fixtures are not carried into the competition repository. Only the IF41-VIEW types are (`v415-fold/README.md`, Correction). In the run, Layers A and B are rebuilt against code written at kickoff. The results recorded here are design evidence only.

**Status:** proposal. It adds no gate and replaces none. Layer C is the existing governed verification, unchanged.

Rules that hold across all three layers:
- Layer A and Layer B results never stand in for Layer C.
- Every Layer A/B view says it is a fixture. `AppView.Origin = FixtureBackend` with a `FIXTURE:` label. The page shows a "Fixture data, not a live repository" note.
- Only an integration adapter over the real `Engine` produces `IntegratedBackend`. Mutant M8 proves the tests catch a fixture that claims otherwise.
- No new test framework is built. Layer A uses the locked xunit. Layer B uses the locked Playwright and axe-core with Limen and Forma at their locked versions. Layer C uses Dokimos, the governed journeys, the design contract and the gates.

## Layer A: immediate contract verification

Runs on every UI module change, in about 11 s from a clean tree (`evidence/layer-a.log`).

| Check | Where | What it catches |
|---|---|---|
| F# compile with warnings as errors | every project | type incompatibility between a screen and the contracts or frozen types |
| Screens reference only the contracts | `BoundaryTests` (assembly metadata and project file) | a screen reaching into domain, persistence, fixtures or the adapter (M4, M4b) |
| Contract surface allowlist | `BoundaryTests` | a presentation contract exposing a domain or persistence type (M3) |
| Manifest completeness | `ManifestTests` | a catalog entry dropped or moved off its screen (M1); an in-scope UI requirement or P0 journey assertion with no screen; an id in F# missing from the manifest, or the reverse |
| Rules equal the catalog | `ManifestTests` | a validation block rewritten (M2); a label that differs from the catalog name the journeys look up |
| Seven save states with their words | `ContractTests`, `BoundaryTests` | an eighth or a missing state (M5); a frozen `SaveState` case left unmapped (compile-time total match) |
| Fixture-backed component behaviour | `FixtureBackendTests` | the required states and actions per scenario: empty versus failed, fail-closed invalid data, the duplicate-link refusal, reassessment history, conflict choices, unknown outcome never Saved |
| Credential never bound | `BoundaryTests` | a token in any binding key or value (M6) |
| Ownership | `OwnershipTests` | a file with two owners (M7); a UI role owning an integration-reserved path; a proposed slice with no file of its role |

Command:

```
dotnet test proposals/r10-contract-first-ui/tests
```

Mutation controls: `evidence/mutations.log`. All 9 mutants are killed. M4 at first survived because an unused project reference is not emitted into assembly metadata, so the test now also reads the project file.

## Layer B: screen behaviour verification

Runs for the affected screen, in about 12 s (`evidence/layer-b.log`).

The screen is rendered by the **real Limen 0.9.1 `BrowserKernel`** with **Forma 0.12.0** CSS. The engine is the F# fixture backend behind a pass-through transport (`prototype/host`).

`prototype/web/render.spec.mjs` checks:
- the screen is findable by the governed journeys' own locators, copied from `verification/journeys/support.mjs` `connect()` and J1-01/J1-02;
- loading, empty, unauthorized, network-failure and validation states;
- axe-core 4.14.0 finds no violations;
- the 390 px layout has no horizontal overflow;
- primary navigation is reachable by keyboard;
- the credential is absent from the DOM.

What it found, before any review: ivory on teal (`#F5F3ED` on `#087F8C`) is 4.27:1, below 4.5:1 for normal text. The prototype uses a derived token (`--causa-teal-strong`), as UI-016 allows. Owner decision CF-11 asks who owns that token.

Command:

```
cd proposals/r10-contract-first-ui/prototype/web && npm ci && npx playwright test
```

Limits:
- Chromium only in this sandbox. Layer C runs Chromium and WebKit.
- The four design-contract viewports are covered at 390 px only for the prototype. A screen's Layer B spec should run all four.
- The governed design contract's rendered half (`verification/design-contract/rendered.spec.mjs`) needs the full shell and workspace. It runs at Layer C.

## Layer C: integrated acceptance

Unchanged, and still required. It runs when the real dependencies exist:
- real GitHub connectivity;
- the actual causa-data histories;
- authoritative replay by Causa's kernel;
- create and update persistence;
- conflict handling;
- journeys J1–J3 on both engines against api.github.com (`slice-gate`, `Ledger.fs` journeys rule);
- catalog acceptance;
- the design contract;
- `completion-gate` COMPLETE.

A screen that passes Layers A and B is a **fixture-backed screen**. It becomes an **integrated feature** only when:
- its slice passes `slice-gate` on the merged result;
- its bound journey assertions pass live.

`SCHEDULE-INPUTS.md` §2 shows that merging needs T21 and T25 (and ODR-R10-25 for historical data), not just T17.

## Which layer judges what

| Claim | A | B | C |
|---|---|---|---|
| Screen compiles against the interfaces | ✓ | | |
| Every required field and action exists with its catalog name and rules | ✓ | ✓ (DOM) | ✓ (Dokimos catalog acceptance) |
| Loading, empty and error states render with UI-010 copy | ✓ | ✓ | ✓ (journeys, plan tests) |
| Accessible and responsive | | ✓ (axe, 390 px, keyboard) | ✓ (design contract, J3-08, J3-09, WebKit) |
| Connects to GitHub, lists real investigations | | | ✓ only |
| Shows Causa's replay of a history | | | ✓ only |
| Saved means committed and read back | | | ✓ only (J1-04, J1-05, PERSIST-007) |
| Conflict and unknown outcome driven by their real entry conditions | | | ✓ only (J3-05, J3-07) |
