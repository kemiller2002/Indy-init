// Browser test of Connect -> Browse -> Open against the real kemiller2002/causa-data (anonymous read; public repo).
// Usage: node tests/read-path.mjs <baseUrl> <screenshotDir>. Exit code 0 only if every assertion holds.
import { createRequire } from "node:module";
// The container ships Playwright globally (NODE_PATH); a project install would pin its own version.
const { chromium, devices } = createRequire(import.meta.url)("playwright");

const base = process.argv[2] ?? "http://localhost:4173/";
const shots = process.argv[3] ?? ".";
const proxy = process.env.HTTPS_PROXY ? { server: process.env.HTTPS_PROXY, bypass: "localhost,127.0.0.1" } : undefined;
const results = [];
const check = (name, ok, detail = "") => { results.push({ name, ok, detail }); console.log(`${ok ? "PASS" : "FAIL"} ${name}${detail ? " — " + detail : ""}`); };

const browser = await chromium.launch(proxy ? { proxy } : {});
// Test-only: the container's egress proxy re-signs TLS; the deployed site never uses this setting.
const context = await browser.newContext({ ...devices["iPhone 13"], ignoreHTTPSErrors: true });
const page = await context.newPage();
const consoleErrors = [];
page.on("console", (m) => { if (m.type() === "error") consoleErrors.push(m.text()); });
try {
  await page.goto(base);
  // "Not connected." exists only in the F# engine's projection: seeing it proves the F# application rendered the page.
  await page.getByText("Not connected.").waitFor({ timeout: 120000 });
  check("the F# engine renders the page (WebAssembly)", true);
  await page.screenshot({ path: `${shots}/1-connect-mobile.png`, fullPage: true });
  await page.getByRole("button", { name: "Connect" }).click();
  await page.getByRole("heading", { name: "Investigations" }).waitFor({ timeout: 60000 });
  await page.locator(".causa-row").first().waitFor({ timeout: 60000 });
  const titles = await page.locator(".causa-row__title").allTextContents();
  check("catalog lists the four active cases", titles.length === 4, titles.join(" | "));
  for (const t of ["Apollo 1 (AS-204)", "Apollo 11", "Challenger"]) check(`catalog contains ${t}`, titles.some((x) => x.includes(t)));
  await page.screenshot({ path: `${shots}/2-list-mobile.png`, fullPage: true });
  const expectations = [["Apollo 1 (AS-204)", 432, "awaitingVerification"], ["Apollo 11", 431, "awaitingEvidence"], ["Challenger", 452, "active"]];
  for (const [title, events, lifecycle] of expectations) {
    await page.locator(".causa-row", { hasText: title }).first().click();
    await page.locator("#detail-title").waitFor({ timeout: 60000 });
    await page.getByText(/lines decoded to typed events/).waitFor({ timeout: 60000 });
    const text = await page.locator("main").innerText();
    check(`${title}: ${events} of ${events} lines decoded`, text.includes(`${events} of ${events} lines decoded`));
    check(`${title}: replayed state equals the catalog digest`, text.includes("equals the catalog manifest's expectedStateSha256"));
    check(`${title}: lifecycle ${lifecycle}`, text.includes(lifecycle));
    check(`${title}: shown as not authoritative`, text.includes("Admissibility not evaluated"));
    check(`${title}: read-only (no edit form)`, (await page.locator("#edit-title").count()) === 0);
    if (title === "Apollo 1 (AS-204)") await page.screenshot({ path: `${shots}/3-detail-apollo1-mobile.png`, fullPage: false });
    await page.getByRole("button", { name: "← Investigations" }).click();
    await page.getByRole("heading", { name: "Investigations" }).waitFor();
  }
  // Routing is F#: the engine pushes hash routes through Limen and interprets Back and reloaded deep links.
  const apollo = page.locator(".causa-row", { hasText: "Apollo 1 (AS-204)" }).first();
  await apollo.click();
  await page.getByText(/lines decoded to typed events/).waitFor({ timeout: 60000 });
  const detailUrl = page.url();
  check("opening a case sets its hash route", /#\/investigations\/catalog\/[0-9a-f-]{36}$/.test(detailUrl), detailUrl);
  await page.goBack();
  await page.getByRole("heading", { name: "Investigations" }).waitFor({ timeout: 30000 });
  check("browser Back returns to the list", page.url().endsWith("#/investigations"), page.url());
  await page.goto(detailUrl);
  await page.getByText("Not connected.").or(page.getByText(/lines decoded/)).first().waitFor({ timeout: 120000 });
  if (await page.getByRole("button", { name: "Connect" }).count()) await page.getByRole("button", { name: "Connect" }).click();
  await page.getByText(/432 of 432 lines decoded/).waitFor({ timeout: 60000 });
  check("a reloaded deep link opens the case after Connect", true);
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  check("no horizontal overflow at 390 px", overflow <= 0, `overflow ${overflow}px`);
  check("no console errors", consoleErrors.length === 0, consoleErrors.slice(0, 3).join(" / "));
} catch (e) {
  check("run completed", false, String(e));
} finally {
  await browser.close();
}
const failed = results.filter((r) => !r.ok).length;
console.log(`${results.length - failed}/${results.length} passed`);
process.exit(failed === 0 ? 0 : 1);
