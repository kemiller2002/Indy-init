# Architecture: an F# application on Limen and Forma

**The application is F#.** That covers state, transitions, routing, the GitHub integration, validation, serialization, event decoding and replay, persistence orchestration, and every value the page displays. All of it lives in `src/Causa.Preview.Engine/` and in the shared library `proposals/r10-historical-data-compatibility/src/`, and all of it is compiled to WebAssembly.

```text
browser ──DOM events──▶ Limen kernel (framework) ──SemanticEvent──▶ F# engine (WebAssembly)
   ▲                         │  ▲                                   │ state · routes · decisions
   └──textContent/attrs──────┘  └──EffectResult (HTTP, navigation)──┘ view · effect requests
```

- **Limen 0.9.1** is the framework. It is Echelon's TypeScript browser kernel (DOM bindings, `fetch`, history), and it knows no application vocabulary.
- **Forma 0.10.0** supplies the components, as semantic HTML and CSS. It has zero runtime.

The F# engine never calls a browser API. HTTP and navigation leave the engine as Limen effect requests, and their outcomes come back to it as data.

## The documented non-F# exceptions

The check `tests/ArchitectureCheck.fsx` enforces the list and fails the build on anything else.

| ID | File | What it does | Why F# cannot do it |
|---|---|---|---|
| **JS-1** | `web/main.js` (2 code lines) | Constructs Limen's `BrowserKernel` and Limen's own `DotnetWasmTransport`, which loads `dotnet.js` and names the export. | A browser starts a page from an HTML-declared JavaScript module. There is no way to start a .NET WebAssembly runtime without calling `dotnet.js` from JavaScript, and Limen's kernel is constructed in JavaScript by design. Everything after start-up runs in F#. A2 enforces: at most 4 code lines; Limen and `dotnet.js` imports only; no `fetch`, storage, `Date`, DOM queries, conditionals or functions beyond the runtime-loading arrow. |
| **CS-1** | `src/Causa.Preview.Wasm/Program.cs` (one method) | `[JSExport] Dispatch(json) => Causa.Preview.Dispatch.handle(json)`. | .NET's `[JSExport]` marshalling is produced by a Roslyn source generator that runs only on C#. F# has no equivalent generator, and without an export JavaScript cannot call into the module. Limen's own F# site uses the same shim (`limen/site/fsharp/README.md`: "a pure marshalling shim"). A3 enforces: exactly that one expression-bodied forward, with no branching, construction or I/O. |
| — | `web/index.html`, `web/app.css` | Forma markup with Limen `data-*` bindings; layout glue | This is markup and style, not code. A4 enforces: no inline scripts or handlers. A6 enforces that every bound key is projected by F#. |

**Superseded:** the earlier preview read the clock in JavaScript (DECISIONS.md D-03). The clock is now read in F# at the dispatch boundary (`Dispatch.handle`, `DateTimeOffset.UtcNow`, millisecond UTC), and `Dispatch.step` stays pure by taking `now` as a parameter.

**Not used:** Bolero, Blazor and any other UI framework. No verified limitation required one.

**Not applicable yet:** Folio (printable output). No printable report exists in this slice.

## Tooling, not application code

| What | Language |
|---|---|
| `build.fsx`, `serve.fsx`, `tests/ArchitectureCheck.fsx`, `tests/Engine.Tests/` | F# |
| `tests/read-path.mjs`, `tests/real-provider.mjs` | Node 22 browser automation, which the constraint permits; these drive the application and contain no application logic |
| `web/package.json` | npm pins Limen and Forma |
