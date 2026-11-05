// Browser mechanism only (Limen host pattern): load the .NET WebAssembly runtime, move serialized Limen messages
// across the boundary, and report the browser clock as a capability fact. No application vocabulary lives here.
import { BrowserKernel } from "@echelon-foundry/limen";

const clockFact = () => ({ kind: "CapabilityFact", capability: "causa.clock", version: 1, fact: { now: new Date().toISOString() } });

class WasmTransport {
  #dispatch = null;
  async start() {
    const { dotnet } = await import("./_framework/dotnet.js");
    const runtime = await dotnet.withDiagnosticTracing(false).create();
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    const fn = exports?.CausaPreviewWasm?.Exports?.Dispatch;
    if (typeof fn !== "function") throw new Error("CausaPreviewWasm.Exports.Dispatch was not exported.");
    this.#dispatch = (m) => JSON.parse(String(fn(JSON.stringify(m))));
  }
  async dispatch(message) {
    if (this.#dispatch === null) throw new Error("dispatch() before start()");
    if (message.kind === "Event" || message.kind === "Initialize") this.#dispatch(clockFact());
    return this.#dispatch(message);
  }
}

try {
  await new BrowserKernel(new WasmTransport(), document).start();
  document.documentElement.dataset.causaReady = "true";
} catch (error) {
  document.documentElement.dataset.causaReady = "failed";
  const p = document.createElement("p");
  p.setAttribute("role", "alert");
  p.textContent = "Causa could not start: " + (error instanceof Error ? error.message : String(error));
  document.body.prepend(p);
}
