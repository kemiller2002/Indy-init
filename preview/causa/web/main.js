// Composition root: the only JavaScript in the application (documented exception JS-1, ARCHITECTURE.md).
// Browsers start applications from a JavaScript module; this one only constructs Limen's kernel and Limen's .NET
// WebAssembly transport. Every state, route, decision, request and view value is computed by the F# engine.
import { BrowserKernel } from "@echelon-foundry/limen";
import { DotnetWasmTransport } from "@echelon-foundry/limen/hosts/dotnet-wasm";

const transport = new DotnetWasmTransport({ loadRuntime: () => import("./_framework/dotnet.js"), exportPath: ["CausaPreviewWasm", "Exports", "Dispatch"] });
await new BrowserKernel(transport, document).start();
