# Token economics fixture (test data, not a measurement)

**This directory is a recorded test fixture. Every number in it is invented. Nothing here was measured from a real provider, model, session or rehearsal.**

It is used by the "token economics" group of the offline suite (`tools/requirements-compiler/tests/TokenEconomicsTests.fs`) to check `requirements-compiler token-economics` (Amendment D, see `docs/TOKEN-ECONOMICS.md`).

- `repo/.ros/telemetry/executions/EXE-*.json`: seven execution records in the Praxis 3.24.0 telemetry record shape (schema 1.0.0). The shape is copied from real records and from the Praxis source (`FileTelemetryFinalizationRepository.fs`, transcript metrics `source.type: runtime-output`, `dimensions.snapshot`, `dimensions.bound: lower` on output). Provider `fixture-provider`, runtime `fixture-runtime`, models `fixture-model` and `fixture-model-b`, source `fixture-transcript`. Each record's `capabilities` list is cut down to the token and cost metrics; a real record lists every metric in the registry.
- `price-table.json`: an invented price table (source `fixture-prices-not-real`) in the Praxis legacy price-table format. Its rates are not real prices.
- `expected/token-economics.json` and `expected/token-economics.md`: the tool's output for these records, with the window 2026-01-10T12:00:00Z to 2026-01-10T19:00:00Z and the price table above. The suite checks that the output matches these files byte for byte.

| Record | Work item | What it exercises |
|---|---|---|
| `...f0000001` | WI-F1 | Complete token data, pre-event (collected before the window). Role `coordinator`. |
| `...f0000002` | WI-F2 | Complete and timed. Has a provider-reported `cost.execution_total` (observed), a runtime estimate `cost.session_cumulative` (estimated, never counted), and a transcript session that started before the window (a phase caveat). |
| `...f0000003` | WI-F3 | No token data: tokens `supported-unavailable`, with wall time, commits and lines that must not become tokens. Its role comes from its classification (`identity.orchestration` empty, as Praxis 3.24.0 writes it). |
| `...f0000004` | WI-F4 | Failed: `step.failed`. Partial token data: cache write `supported-unavailable` ("reported on 3 of 5 requests"). The measurements are step-scoped. |
| `...f0000005` | WI-F2 | Repeats WI-F2 (its second execution, child of `...f0000002`). |
| `...f0000006` | WI-F5 | Abandoned (`work.abandoned`). Model `fixture-model-b` has no price in the table. Has an estimated `tokens.input` that must be excluded. |
| `...f0000007` | WI-F6 | Unfinalized (`status: active`). Token capabilities `unknown`. No role recorded. |

To regenerate the expected files after an intended change to the tool, run this from the repository root, then review the diff:

```
dotnet run --project tools/requirements-compiler/src -- token-economics tools/requirements-compiler/tests/fixtures/token-economics/repo \
  --window-start 2026-01-10T12:00:00Z --window-end 2026-01-10T19:00:00Z \
  --price-table tools/requirements-compiler/tests/fixtures/token-economics/price-table.json \
  --out tools/requirements-compiler/tests/fixtures/token-economics/expected
```
