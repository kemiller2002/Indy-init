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
  await page.waitForFunction(() => document.documentElement.dataset.causaReady !== undefined, null, { timeout: 120000 });
  check("engine starts in the browser (WebAssembly)", await page.evaluate(() => document.documentElement.dataset.causaReady) === "true");
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
