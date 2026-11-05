// Real-provider acceptance of Create -> Save -> Reload against GitHub, run by the owner with an approved credential.
//   CAUSA_TOKEN=<fine-grained token: contents read on the catalog, read/write on CAUSA_WRITE_REPO only>
//   CAUSA_WRITE_REPO=<owner/name> [CAUSA_WRITE_BRANCH=main] [CAUSA_WORKSPACE=preview-<date>]
//   node tests/real-provider.mjs <baseUrl>
// The token is read from the environment, typed into the password field, and never printed or written.
import { createRequire } from "node:module";
const { chromium, devices } = createRequire(import.meta.url)("playwright");

const base = process.argv[2] ?? "http://localhost:4173/";
const token = process.env.CAUSA_TOKEN, repo = process.env.CAUSA_WRITE_REPO;
const branch = process.env.CAUSA_WRITE_BRANCH ?? "main";
const workspace = process.env.CAUSA_WORKSPACE ?? `preview-${new Date().toISOString().slice(0, 10)}`;
if (!token || !repo) { console.error("set CAUSA_TOKEN and CAUSA_WRITE_REPO"); process.exit(2); }
const results = [];
const check = (name, ok, detail = "") => { results.push(ok); console.log(`${ok ? "PASS" : "FAIL"} ${name}${detail ? " — " + detail : ""}`); };
const title = `Preview acceptance ${new Date().toISOString()}`;

const proxy = process.env.HTTPS_PROXY ? { server: process.env.HTTPS_PROXY, bypass: "localhost,127.0.0.1" } : undefined;
const browser = await chromium.launch(proxy ? { proxy } : {});
const page = await (await browser.newContext({ ...devices["iPhone 13"], ignoreHTTPSErrors: !!proxy })).newPage();
const connect = async () => {
  await page.goto(base);
  await page.getByText("Not connected.").waitFor({ timeout: 120000 }); // rendered by the F# engine
  await page.fill("#write-repo", repo); await page.fill("#write-branch", branch); await page.fill("#workspace", workspace);
  await page.fill("#token", token);
  await page.getByRole("button", { name: "Connect" }).click();
  await page.getByRole("heading", { name: "Investigations" }).waitFor({ timeout: 60000 });
};
try {
  await connect();
  await page.getByRole("button", { name: "New investigation" }).click();
  await page.fill("#new-title", title);
  await page.fill("#new-observation", "Real-provider acceptance: created by tests/real-provider.mjs.");
  await page.getByRole("button", { name: "Create and save" }).click();
  await page.getByText(/read back byte-identical/).waitFor({ timeout: 60000 });
  check("create is saved and read back byte-identical", true);
  await page.fill("#edit-observation", "A second observation, saved separately.");
  await page.getByRole("button", { name: "Add observation" }).click();
  await page.getByRole("button", { name: "Save to GitHub" }).click();
  await page.getByText(/read back byte-identical/).waitFor({ timeout: 60000 });
  check("edit is saved and read back byte-identical", true);
  // Independent reload: a new page (no engine memory) connects and opens it from GitHub.
  await connect();
  const row = page.locator(".causa-row", { hasText: "Working copy" });
  check("a new session lists the stored investigation", (await row.count()) >= 1);
  await row.last().click();
  await page.getByText(/lines decoded to typed events/).waitFor({ timeout: 60000 });
  const text = await page.locator("main").innerText();
  check("reloaded title matches", text.includes(title));
  check("reloaded history has both observations", text.includes("A second observation, saved separately."));
  // Independent of the application: fetch the stored manifest straight from GitHub.
  const headers = { Authorization: `Bearer ${token}`, Accept: "application/vnd.github+json" };
  const listing = await (await fetch(`https://api.github.com/repos/${repo}/contents/workspaces/demo/${workspace}/investigations?ref=${branch}`, { headers })).json();
  check("GitHub holds the working-copy investigation directory", Array.isArray(listing) && listing.length >= 1, `${Array.isArray(listing) ? listing.length : "?"} entries`);
} catch (e) { check("run completed", false, String(e)); }
finally { await browser.close(); }
console.log(`${results.filter(Boolean).length}/${results.length} passed`);
process.exit(results.every(Boolean) ? 0 : 1);
