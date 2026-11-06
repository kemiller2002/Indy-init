// Composition root (documented JS exception): constructs Limen's kernel and .NET WebAssembly transport only.
// Every state, route, decision, effect and view value is computed in F# (src/Exp002.Ui).
import { BrowserKernel } from "@echelon-foundry/limen";
import { DotnetWasmTransport } from "@echelon-foundry/limen/hosts/dotnet-wasm";
const transport = new DotnetWasmTransport({ loadRuntime: () => import("./_framework/dotnet.js"), exportPath: ["Exp002Wasm", "Exports", "Dispatch"] });
await new BrowserKernel(transport, document).start();
