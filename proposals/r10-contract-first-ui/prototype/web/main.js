// The Limen wiring layer: the real BrowserKernel (0.9.1) and a transport that forwards each message to the F# engine
// host (prototype/host). The transport decides nothing; it carries messages.
import { BrowserKernel } from "/node_modules/@echelon-foundry/limen/dist/index.js";

const session = crypto.randomUUID();
const scenario = new URLSearchParams(location.search).get("scenario") ?? "connected-catalog";
const transport = {
  async start() {},
  async dispatch(message) {
    const r = await fetch(`/dispatch?session=${session}&scenario=${encodeURIComponent(scenario)}`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(message),
    });
    return r.json();
  },
};
await new BrowserKernel(transport, document).start();
