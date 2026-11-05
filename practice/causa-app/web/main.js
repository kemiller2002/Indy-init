// Practice only (FENCE.md). Browser bootstrap: starts the .NET WebAssembly engine and hands it to the Limen kernel.
// It relays protocol JSON and nothing else; no token or application decision passes through here.
import { BrowserKernel } from "@echelon-foundry/limen";
import { dotnet } from "./_framework/dotnet.js";

const transport = (() => {
  let exports;
  return {
    async start() {
      const runtime = await dotnet.create();
      exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    },
    async dispatch(message) {
      return JSON.parse(exports.Causa.Practice.Host.HostShim.Dispatch(JSON.stringify(message)));
    },
  };
})();

const kernel = new BrowserKernel(transport, document, { report: (e) => e.kind === "BridgeError" && console.error(`bridge ${e.phase}: ${e.detail}`) });
await kernel.start();
document.getElementById("app-loading")?.remove();
document.documentElement.dataset.ready = kernel.status;
