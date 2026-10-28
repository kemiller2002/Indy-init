<!-- Investigation note written 2026-10-28 by a read-only investigation agent for the Echelon Engineering Improvement Initiative (Phase One). Local paths refer to that session's clones and scratch directory. The transform.c:9807 citation was re-checked against dotnet/runtime release/10.0 before publication. -->

# Rehearsal 5 WebAssembly start failure — root cause and check placement

Scope: read-only on all repos. All builds/runs were done in scratch copies under
`/tmp/claude-0/-home-user-causa-data/df80365e-e3df-5568-a76a-085bbe8f71e5/scratchpad/wasm-probe/` (one dir per commit, `git archive` of the local clone).

## 1. The exact failure

Reproduced verbatim (node host and headless Chromium 153 via Playwright, same `dotnet.js`/`dotnet.native.wasm`):

```
System.InvalidProgramException: Unable to run method
'Microsoft.FSharp.Collections.FSharpMap`2<string, Limen.Contract.Core.Types/ViewValue>
 Causa.App.View:project (Causa.App.Shell/Model)': locals size too big.
```

- Commit a0e7d4a (2026-10-27T23:00:10Z, r5/shell, parent bb89d44) message: "View.project exceeded the .NET WebAssembly interpreter's per-method limit ("locals size too big") and the page did not start." It added `tests/browser/start-smoke.spec.js` (checks every primary route for BridgeError/StartFailed/"locals size too big").
- Shape before/after (IL measured with System.Reflection.Metadata over Release `Causa.App.dll`):

| commit | method | IL bytes | MaxStack | IL locals | IL instrs | branches | result (interp) |
|---|---|---|---|---|---|---|---|
| 80a0476 (r5/shell before WIP) / 5d8888f (main before fix) | View.project (single method, ~184 keys) | 113,905 | 196 | 12,683 | 29,408 | 533 | FAIL |
| bb89d44 (WIP: NoInlining helpers + keys1..keys5 closures) | View.project | 4,514 | 19 | 205 | 1,625 | 179 | OK |
|  | largest closure keys2@463::Invoke | 6,520 | 51 | 650 | 2,026 | 92 | OK |
| a0e7d4a (fix: 9 top-level per-area functions) | View.project | — | — | 0-ish (locals size 16 B) | — | — | OK |
|  | largest: View.invKeys | 6,249 | 46 | 519 | 2,070 | 156 | OK |

  The F# compiler (Release) inlined the small helpers (`text`, `flag`, `item`, Option/List lambdas) into the one big list literal, and every intermediate became its own IL local; F# does not reuse locals across the list elements, so the local count grows linearly with the number of view keys. The fix adds `[<MethodImpl(NoInlining)>]` on helpers and splits the projection into nine methods.

- Not the "optimizer passes off" story: the failure occurs identically with the default interpreter options and with Causa's `MONO_INTERPRETER_OPTIONS=-ssa,-bblocks` (the View.fs comment overstates that factor).
- Bisect (probe that calls `View.project Shell.initial` under the WASM interpreter, both option sets):

| commit (time UTC) | IL locals in project | result |
|---|---|---|
| fc6e4fd 20:29 | 1,497 | OK |
| d04c363 20:44 | 3,815 | OK |
| 950a673 21:13 | 4,121 | OK |
| e02d000 21:20 | 7,246 | OK (interp slot of last local at offset 57,976) |
| **ec6407b 21:30 (T28 history screen)** | 8,838 | **FAIL** (last local offset 70,712) |
| 5fddbca, 1e8757a, 819ab4f, 3399016, 6b1239e, 0a97e28, 80a0476, 5d8888f | 11,301 to 12,683 | FAIL |
| bb89d44, a0e7d4a | split | OK |

  So the page had been unable to start from ec6407b (~21:30Z) until the fix at 23:00Z, about 90 minutes; nothing caught it because the .NET unit tests run on CoreCLR (no such limit) and no startup smoke ran on the WASM build.
- Recorded error text elsewhere: only the commit messages above, the comments in `src/app/View.fs` (lines 11-12, 882-883), `tests/browser/start-smoke.spec.js`, and 8711b12's message ("View.project is at the interpreter's locals limit"). Nothing in evidence/, docs/ or .ros events. I could not read the CI logs: the GitHub MCP is limited to kemiller2002/causa-data in this session, and I was told not to touch the rehearsal-005 remote.

## 2. How Limen/Causa runs .NET on WASM

- Runtime: the Mono **interpreter** in `Microsoft.NETCore.App.Runtime.Mono.browser-wasm` (net10.0). Causa's main.ts comment says .NET 10.0.14; my local repro used runtime pack 10.0.13 (SDK 10.0.402). The code and check are the same in v10.0.0, release/10.0 and main.
- No AOT: there is no `RunAOTCompilation` or `WasmEnableAOT` anywhere in rehearsal-005 or limen. The **jiterpreter is disabled** at start (`--no-jiterpreter-traces-enabled`, `--no-jiterpreter-interp-entry-enabled`, `--no-jiterpreter-jit-call-enabled`), and `MONO_INTERPRETER_OPTIONS=-ssa,-bblocks` is set because of the R4 D-HIST-WASM trap (`src/kernel/main.ts` lines 72-104; also `tests/Engine.WasmSmoke/run.mjs`).
- `src/wasm/Causa.Wasm.csproj` uses `Microsoft.NET.Sdk.WebAssembly` with `InvariantGlobalization=true`, `WasmEnableWebcil=true` and `PublishTrimmed=false`. `Directory.Build.props` sets net10.0 and `TreatWarningsAsErrors`. `limen.config.json` only declares the engine/kernel boundary dirs.
- CI (`build-and-test.yml`) runs `dotnet workload install wasm-tools` before `npm run build`. Without AOT the workload only enables native relinking. **The workload is not needed** for the interpreter build: my probes built and ran with no workloads installed.
- Limen itself: its own guests (`guests/minimal/*`, `guests/csharp/*`) target net8.0 with no AOT. Limen sets no interpreter options; Causa's composition root does.

## 3. The runtime limitation (source-level)

`dotnet/runtime` `src/mono/mono/mini/interp/transform.c`, in `generate()` (release/10.0 ~line 9807; v10.0.0 line 9791; main line 9846):

```c
if (td->total_locals_size >= G_MAXUINT16) {
    if (td->disable_inlining && td->optimized) {
        ... "Unable to run method '%s': locals size too big." -> InvalidProgramException
    } else {
        // We give the method another chance to compile with inlining disabled and optimization enabled
        td->retry_compilation = TRUE; td->retry_with_inlining = FALSE;
    }
}
```

- **Limit:** the interpreter frame's locals area (`total_locals_size`) must be **< 65,535 bytes** (G_MAXUINT16). It is measured in bytes of interpreter var slots, not IL bytes. Var offsets are encoded as 16-bit operands in the compacted interpreter code.
- Each var takes `ALIGN_TO(size, MINT_STACK_SLOT_SIZE)` = 8 bytes (`sizeof(stackval)`, interp-internals.h:29) for refs and primitives, more for value types. The total is aligned to 16.
- Tier 0 (unoptimized, the first call) gives every arg and IL local its own slot with no reuse (transform.c ~9223). That makes it exactly **8 × (args + IL locals)** for ref-typed locals. Measured: project@e02d000 had 1 + 7,246 vars, ending at 57,984 B, and passed. project@ec6407b had 1 + 8,838 vars, ending at 70,720 B, and failed. project@80a0476 had 1 + 12,683 vars, ending at 101,480 B, and failed. The verbose interpreter output (`MONO_VERBOSE_METHOD`) shows exactly 8 bytes per var.
- On overflow the interpreter retries once with optimization on and inlining off. The optimized offset allocator (`interp_alloc_offsets`, transform-opt.c) reuses slots only for vars local to one basic block. Vars used in more than one basic block ("global") get a dedicated slot ("We don't track liveness yet for global vars"). Many F# locals cross branches, so the retry did not rescue any failing commit. `-ssa,-bblocks` can only make the retry worse, but it was not decisive here.
- The limit is specific to the interpreter. CoreCLR JIT (where the xUnit tests run) and Mono AOT/LLVM do not have it. A fully AOT-compiled method becomes a wasm function, which is bound instead by the browser's WebAssembly limits (JS-API: 50,000 locals per function, 7,654,321-byte function body), far beyond this case. AOT still falls back to the interpreter for some methods. The jiterpreter is irrelevant: it compiles traces only after the interpreter transform succeeds, and it is disabled here anyway.
- Also in transform.c: `g_assert(size < G_MAXUINT16)` on value-type sizes, and a 16-bit data-item index per method ("Interpreter data item index ... overflows"). These are other 16-bit limits; they did not trigger here.
- I found no dotnet/runtime issue or documentation for it: GitHub search is blocked in this session, and web search found nothing. The source above is the authority.

## 4. Can compiled IL predict it? Yes, tightly

- **Predictive metric:** the estimated tier-0 interpreter frame, `Σ align8(size(arg)) + Σ align8(size(local))`, rounded to 16. Locals come from the `LocalSignature` (`StandaloneSignature` blob: 0x07 + compressed count), and refs/primitives count as 8. It matched the runtime exactly in every measured case. Fail threshold: ≥ 65,535 B, about 8,190 ref-sized locals.
- IL byte size, MaxStack, instruction count and branch count all correlate (they grow with the method) but are not the measured quantity. MaxStack does not contribute to `total_locals_size` at tier 0, because stack vars live in a separate area. IL bytes would mispredict methods with few locals and long straight-line code.
- `RuntimeHelpers.PrepareMethod` does **not** trigger the interpreter transform: all 1,237 methods prepared without error at 80a0476, then the first call failed. A runtime-only check therefore has to execute the method. The static scan is the cheap, deterministic predictor, and a startup smoke is the confirmation.
- Webcil: the shipped `_framework/*.wasm` assemblies are Webcil-wrapped, and SRM's PEReader cannot read them directly. Scan the build output `.dll` (obj/bin) or unwrap Webcil.
- Tools (scratch): `wasm-probe/ilmetrics/ilmetrics.fsx` (SRM scan), `probe-template/` (WASM exe + node runner), `browser/run.cjs` (Playwright). Results are in `bisect.txt`, `build-status.txt` and `verbose-*.log`.

## 5. Existing tooling

- Limen: `limen init|status|verify|upgrade|doctor` (`cli/Limen.Core/Diagnose.fs`, `Inspect.fs`). These are installation and lexical boundary checks over source files (manifest, config, banned authorities). There is no assembly inspection and no runtime/interpreter awareness. `npm run smoke:site:wasm` (scripts/smoke-site-wasm.sh, CI) smoke-tests Limen's **own** F# sample site in Chrome, not a consumer app.
- Dokimos: `dokimos status|verify|doctor` are read-only installation-health checks (src/Dokimos.Cli/Program.fs:460-491, docs/operations/INTEGRATION.md). It has no WASM, assembly or startup checks.
- Causa itself has `tests/Engine.WasmSmoke` (engine only, under node) and, since a0e7d4a, `tests/browser/start-smoke.spec.js`.

## Recommendation

- **Limen owns the static check.** The limit is a property of the runtime and interpreter configuration that Limen-hosted .NET guests run on, and Limen ships the guest packages (`EchelonFoundry.Limen.Guest`) and `verify`/`doctor`. Options:
  - an MSBuild target in the Guest package (after Build), or
  - a `limen verify --guest-assemblies <dir>` / doctor finding.

  Either one scans the guest's built assemblies with System.Reflection.Metadata and reports, per method, the estimated tier-0 interpreter frame bytes against 65,535. Suggested policy:
  - **error** at ≥ 65,535 B, the point where tier 0 has already overflowed and only the optimized retry can save it; that retry rescued none of the measured cases;
  - **warning** at ≥ 32,768 B (50%) to give headroom, since project grew about 1,600 locals per feature commit;
  - report the top-N methods and name the remedy (NoInlining helpers, split per area).
- **Dokimos owns the behavioral confirmation:** a generic "the built WASM app starts on its primary routes with no engine fault / InvalidProgramException" oracle, like Causa's start-smoke. It catches what static analysis cannot: the optimized-retry outcome, value-type sizing, generic instantiations, data-item index overflow and the D-HIST class of runtime traps.
- **False-positive risk** of the static error:
  - (a) a method over 65,535 B at tier 0 that the optimized/no-inline retry fits; possible when most locals are block-local. This was not observed, but it means a failure verdict should say "will fail unless the optimizer retry fits" or be confirmed by the smoke;
  - (b) dead or never-called methods, which are flagged but harmless;
  - (c) value-type sizes estimated as 8 under-count, a false negative. Resolve struct sizes or count them conservatively.
- **False-negative risk:** methods built by generic instantiation at run time, and interpreter inlining at tier-up. The latter is handled by the retry, which disables inlining.
- Do not use source-line limits: View.fs's line count barely changed between pass and fail, while IL locals went 7,246 → 8,838.
