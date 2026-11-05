// EXP-INDY-002 independent acceptance test (verification agent). Written from CONTRACT-REGISTRY.json only: it finds
// controls by their registry labels and requirement IDs (data-requirement-id), never by the UI agent's markup.
// Browser automation only; contains no application logic.
//   NODE_PATH=$(npm root -g) node tests/acceptance/e2e.mjs http://localhost:4174/ [results.json]
import { createRequire } from "node:module";
const { chromium } = createRequire(import.meta.url)("playwright");
import { writeFileSync } from "node:fs";

const base = process.argv[2] ?? "http://localhost:4174/";
const out = process.argv[3];
const results = [];
const errors = [];
const record = (id, reqs, status, detail = "") => { results.push({ id, requirements: reqs, status, detail }); console.log(`${status} ${id} [${reqs.join(" ")}] ${detail}`); };
const step = async (id, reqs, fn) => { try { await fn(); record(id, reqs, "PASS"); return true; } catch (e) { record(id, reqs, "FAIL", String(e.message ?? e).split("\n")[0]); return false; } };
const expect = (cond, msg) => { if (!cond) throw new Error(msg); };
const req = (page, id) => page.locator(`[data-requirement-id="${id}"]`).first();
const hash = (page) => page.evaluate(() => location.hash);
const settle = (page) => page.waitForTimeout(250);
const uuid = () => crypto.randomUUID();

const browser = await chromium.launch();
const context = await browser.newContext();
const page = await context.newPage();
page.on("pageerror", (e) => errors.push(String(e)));
page.on("console", (m) => { if (m.type() === "error") errors.push(m.text()); });

const id = uuid();
const problem = `Pump P-7 tripped three times on ${id.slice(0, 8)}`;
const title = "Pump trips";
const question = "Why does the pump trip under load?";
const obsText = "Trip log shows overcurrent at 14:02";
const hypText = "Bearing wear raises motor load";
const hypExplains = "Explains the overcurrent and the noise";
const evLabel = "Maintenance log";
const evContent = "Bearing replaced 18 months ago; due at 12";
const rationale = "The overdue bearing fits the rising load";

const confirm = (scope) => scope.getByRole("button", { name: "Confirm", exact: true });

await page.goto(base);
await page.waitForSelector('[data-requirement-id="INV-039"]', { timeout: 30000 });

await step("AT-01 list screen", ["INV-039"], async () => { expect(await req(page, "INV-039").isVisible(), "INV-039 region not visible"); });

await step("AT-02 open start screen", ["INV-005"], async () => {
  await page.getByRole("button", { name: "New investigation" }).or(page.getByRole("link", { name: "New investigation" })).first().click();
  await settle(page);
  expect((await hash(page)) === "#/investigations/new", `hash is ${await hash(page)}`);
  expect((await req(page, "INV-005").innerText()).trim().length > 0, "INV-005 destination indicator empty");
});

await step("AT-03 cancel start writes nothing", ["INV-022"], async () => {
  await page.getByLabel("Problem statement", { exact: true }).fill("should not be created");
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await settle(page);
  expect((await hash(page)) === "#/" || (await hash(page)) === "", `hash is ${await hash(page)}`);
  expect(!(await page.getByText("should not be created").isVisible().catch(() => false)), "cancelled investigation is listed");
  await page.getByRole("button", { name: "New investigation" }).or(page.getByRole("link", { name: "New investigation" })).first().click();
  await settle(page);
});

const startButton = () => page.getByRole("button", { name: "Start investigation", exact: true });
const refused = async (fieldLabel, reqId) => {
  await startButton().click();
  await settle(page);
  expect((await hash(page)) === "#/investigations/new", `start was not refused (hash ${await hash(page)})`);
  const invalid = await page.getByLabel(fieldLabel, { exact: true }).getAttribute("aria-invalid");
  const message = await req(page, reqId).isVisible().catch(() => false);
  expect(invalid === "true" || message, `no field message for ${fieldLabel}`);
};

await step("AT-04 identifier prefilled and validated", ["INV-006"], async () => {
  const idField = page.getByLabel("Investigation identifier", { exact: true });
  expect(/^[0-9a-f-]{36}$/i.test(await idField.inputValue()), "identifier not prefilled with a UUID");
  await idField.fill("not-a-uuid");
  await page.getByLabel("Problem statement", { exact: true }).fill(problem);
  await refused("Investigation identifier", "INV-006");
  await idField.fill(id);
});
await step("AT-05 empty problem statement refused", ["INV-019", "INV-001"], async () => {
  await page.getByLabel("Problem statement", { exact: true }).fill("   ");
  await refused("Problem statement", "INV-019");
});
await step("AT-06 title over 200 refused", ["INV-020", "INV-002"], async () => {
  await page.getByLabel("Problem statement", { exact: true }).fill(problem);
  await page.getByLabel("Title", { exact: true }).fill("t".repeat(201));
  await refused("Title", "INV-020");
});
await step("AT-07 problem statement over 10000 refused", ["INV-021"], async () => {
  await page.getByLabel("Title", { exact: true }).fill(title);
  await page.getByLabel("Problem statement", { exact: true }).fill("p".repeat(10001));
  await refused("Problem statement", "INV-021");
});

const started = await step("AT-08 start investigation", ["INV-004", "INV-001", "INV-002", "INV-023", "INV-007", "INV-008", "INV-018", "INV-016"], async () => {
  await page.getByLabel("Problem statement", { exact: true }).fill(problem);
  await page.getByLabel("Primary investigative question", { exact: true }).fill(question);
  await startButton().click();
  await settle(page);
  expect((await hash(page)) === `#/investigations/${id}`, `hash is ${await hash(page)}`);
  expect((await req(page, "INV-008").innerText()).includes(problem), "INV-008 does not show the problem statement");
  expect((await req(page, "INV-007").innerText()).trim().length > 0, "INV-007 creation timestamp empty");
  expect(/open/i.test(await req(page, "INV-018").innerText()), "INV-018 state not Open");
  expect(/unsaved/i.test(await req(page, "INV-016").innerText()), "INV-016 not Unsaved after start");
  expect(await page.getByText(question).first().isVisible(), "question not shown");
  expect(await page.getByText(title).first().isVisible(), "title not shown");
});

const reachedWorkspace = started || (await hash(page)) === `#/investigations/${id}`;
if (reachedWorkspace) {
  await step("AT-09 record observation", ["INV-014", "INV-024", "INV-025"], async () => {
    await page.getByRole("button", { name: "Record an observation" }).click();
    await page.getByLabel("Observation", { exact: true }).fill("");
    await confirm(page).click(); await settle(page);
    expect(await confirm(page).isVisible(), "empty observation was accepted (editor closed)");
    await page.getByLabel("Observation", { exact: true }).fill(obsText);
    await page.getByLabel("Source reference", { exact: true }).fill("trip-log.csv");
    await confirm(page).click(); await settle(page);
    expect(await page.getByText(obsText).first().isVisible(), "observation text not listed");
  });
  await step("AT-10 cancel editor records nothing", ["INV-036"], async () => {
    await page.getByRole("button", { name: "Record a hypothesis" }).click();
    await page.getByLabel("Hypothesis", { exact: true }).fill("cancelled hypothesis text");
    await page.getByRole("button", { name: "Cancel", exact: true }).click(); await settle(page);
    expect(!(await page.getByText("cancelled hypothesis text").isVisible().catch(() => false)), "cancelled hypothesis recorded");
  });
  await step("AT-11 record hypothesis", ["INV-009", "INV-026", "INV-011", "INV-010"], async () => {
    await page.getByRole("button", { name: "Record a hypothesis" }).click();
    await page.getByLabel("Hypothesis", { exact: true }).fill(hypText);
    await page.getByLabel("What it explains", { exact: true }).fill(hypExplains);
    await confirm(page).click(); await settle(page);
    expect((await req(page, "INV-010").innerText()).includes(hypText), "hypothesis not in INV-010 list");
  });
  await step("AT-12 admit evidence; unsafe URL refused", ["INV-013", "INV-027", "INV-012", "INV-028", "INV-029"], async () => {
    await page.getByRole("button", { name: "Add evidence" }).click();
    await page.getByLabel("Evidence label", { exact: true }).fill(evLabel);
    await page.getByLabel("Original content", { exact: true }).fill(evContent);
    await page.getByLabel("Source reference", { exact: true }).fill("CMMS WO-1182");
    await page.getByLabel("Source URL", { exact: true }).fill("javascript:alert(1)");
    await confirm(page).click(); await settle(page);
    expect(await confirm(page).isVisible(), "javascript: URL accepted");
    await page.getByLabel("Source URL", { exact: true }).fill("https://example.com/wo-1182");
    await confirm(page).click(); await settle(page);
    expect((await req(page, "INV-013").innerText()).includes(evLabel), "evidence not in INV-013 list");
    expect((await page.locator('a[href="https://example.com/wo-1182"]').count()) > 0, "source URL not rendered as a link");
    expect((await page.locator('a[href^="javascript:"]').count()) === 0, "javascript: link rendered");
  });
  await step("AT-13 link evidence to hypothesis", ["INV-030", "INV-031", "INV-038", "INV-044"], async () => {
    await page.getByRole("button", { name: "Link evidence to a hypothesis" }).click();
    const ev = page.getByLabel("Evidence item", { exact: true });
    const hy = page.getByLabel("Hypothesis to assess", { exact: true });
    await ev.selectOption({ index: 1 }); await hy.selectOption({ index: 1 });
    const checked = await page.locator('input[type="radio"]:checked').count();
    expect(checked === 0, "a relationship kind is preselected");
    await page.getByLabel("Rationale", { exact: true }).fill(rationale);
    await confirm(page).click(); await settle(page);
    expect(await confirm(page).isVisible(), "link without a kind was accepted");
    await page.getByLabel("Supports", { exact: true }).check();
    await confirm(page).click(); await settle(page);
    expect(/support/i.test(await req(page, "INV-013").innerText()), "link not shown on evidence");
    expect(/support/i.test(await req(page, "INV-010").innerText()), "link not shown on hypothesis");
  });
  await step("AT-14 duplicate link refused", ["INV-030"], async () => {
    await page.getByRole("button", { name: "Link evidence to a hypothesis" }).click();
    await page.getByLabel("Evidence item", { exact: true }).selectOption({ index: 1 });
    await page.getByLabel("Hypothesis to assess", { exact: true }).selectOption({ index: 1 });
    await page.getByLabel("Contradicts", { exact: true }).check();
    await page.getByLabel("Rationale", { exact: true }).fill("second assessment");
    await confirm(page).click(); await settle(page);
    const shownAlert = await page.getByRole("alert").first().isVisible().catch(() => false);
    expect(shownAlert || (await confirm(page).isVisible()), "duplicate link was not refused visibly");
    expect(!/contradict/i.test(await req(page, "INV-010").innerText()), "duplicate link recorded");
    if (await page.getByRole("button", { name: "Cancel", exact: true }).isVisible()) await page.getByRole("button", { name: "Cancel", exact: true }).click();
  });
  await step("AT-15 save confirmed by read-back", ["INV-015", "INV-016"], async () => {
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await page.waitForFunction(() => /\bsaved\b/i.test(document.querySelector('[data-requirement-id="INV-016"]')?.textContent ?? "") && !/unsaved/i.test(document.querySelector('[data-requirement-id="INV-016"]')?.textContent ?? ""), null, { timeout: 10000 });
  });
  await step("AT-16 edit details keeps identity and history", ["INV-034", "INV-035"], async () => {
    await page.getByRole("button", { name: "Edit details" }).click();
    await page.getByLabel("Title", { exact: true }).fill("Pump trips (revised)");
    await confirm(page).click(); await settle(page);
    expect((await hash(page)) === `#/investigations/${id}`, "identity changed");
    expect(await page.getByText("Pump trips (revised)").first().isVisible(), "edited title not shown");
    expect(/unsaved/i.test(await req(page, "INV-016").innerText()), "INV-016 not Unsaved after edit");
    expect((await req(page, "INV-035").innerText()).length > 0, "no history shown");
  });
  await step("AT-17 reassess keeps the earlier assessment in history", ["INV-030", "INV-035"], async () => {
    await page.getByRole("button", { name: "Reassess" }).first().click();
    await page.getByLabel("Inconclusive", { exact: true }).check();
    await page.getByLabel("Rationale", { exact: true }).fill("Bearing inspection pending");
    await confirm(page).click(); await settle(page);
    expect(/inconclusive/i.test(await req(page, "INV-010").innerText()), "reassessment not shown");
    expect(/support/i.test(await req(page, "INV-035").innerText()), "earlier assessment not in history");
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await page.waitForFunction(() => !/unsaved|saving/i.test(document.querySelector('[data-requirement-id="INV-016"]')?.textContent ?? "x"), null, { timeout: 10000 });
  });
  await step("AT-18 reopen restores the saved investigation", ["INV-017", "INV-039", "INV-040", "INV-008"], async () => {
    await page.goto(base + "#/");
    await page.reload();
    await page.waitForSelector('[data-requirement-id="INV-039"]', { timeout: 30000 });
    await page.getByRole("link", { name: "Pump trips (revised)" }).click();
    await page.waitForFunction((p) => document.body.innerText.includes(p), problem, { timeout: 10000 });
    expect((await hash(page)) === `#/investigations/${id}`, `hash is ${await hash(page)}`);
    for (const t of [problem, question, obsText, hypText, hypExplains, evLabel, evContent]) expect(await page.getByText(t).first().isVisible(), `not restored: ${t}`);
    expect(/inconclusive/i.test(await req(page, "INV-010").innerText()), "relationship not restored");
    expect(/\bsaved\b/i.test(await req(page, "INV-016").innerText()) && !/unsaved/i.test(await req(page, "INV-016").innerText()), "reopened investigation not Saved");
  });
}

// Wave 2 (CG-6): a conclusion is a snapshot; it never resolves the investigation and survives a reload.
if (reachedWorkspace) {
  const conclusion = "Bearing wear is the leading explanation; inspection still pending";
  await step("AT-19 record a conclusion; reopen restores it", ["INV-032", "INV-033", "INV-018"], async () => {
    await page.getByRole("button", { name: "Record a conclusion" }).click();
    await page.getByLabel("Conclusion", { exact: true }).fill("  ");
    await confirm(page).click(); await settle(page);
    expect(await confirm(page).isVisible(), "blank conclusion accepted");
    await page.getByLabel("Conclusion", { exact: true }).fill(conclusion);
    await confirm(page).click(); await settle(page);
    expect((await req(page, "INV-033").innerText()).includes(conclusion), "conclusion not listed in INV-033 region");
    expect(/open/i.test(await req(page, "INV-018").innerText()), "recording a conclusion changed the investigation state");
    expect(/unsaved/i.test(await req(page, "INV-016").innerText()), "not Unsaved after recording a conclusion");
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await page.waitForFunction(() => { const t = document.querySelector('[data-requirement-id="INV-016"]')?.textContent ?? ""; return /\bsaved\b/i.test(t) && !/unsaved/i.test(t); }, null, { timeout: 10000 });
    await page.goto(base + "#/"); await page.reload();
    await page.waitForSelector('[data-requirement-id="INV-039"]', { timeout: 30000 });
    await page.getByRole("link", { name: "Pump trips (revised)" }).click();
    await page.waitForFunction((c) => document.body.innerText.includes(c), conclusion, { timeout: 10000 });
  });
}

// Mobile viewport: every screen fits 375 px without horizontal scrolling, and primary actions stay on screen.
const mobile = await browser.newContext({ viewport: { width: 375, height: 812 }, isMobile: true, hasTouch: true });
const m = await mobile.newPage();
m.on("pageerror", (e) => errors.push("mobile: " + String(e)));
const fits = async (name) => { const w = await m.evaluate(() => document.documentElement.scrollWidth); expect(w <= 376, `${name} scrollWidth ${w} > 375`); };
await step("AT-M1 mobile list and start screens fit 375 px", ["INV-039", "INV-004"], async () => {
  await m.goto(base); await m.waitForSelector('[data-requirement-id="INV-039"]', { timeout: 30000 }); await fits("list");
  await m.getByRole("button", { name: "New investigation" }).or(m.getByRole("link", { name: "New investigation" })).first().click(); await m.waitForTimeout(250); await fits("start");
  const box = await m.getByRole("button", { name: "Start investigation", exact: true }).boundingBox();
  expect(box && box.x >= 0 && box.x + box.width <= 376, "Start button off screen");
});
await step("AT-M2 mobile workspace fits 375 px", ["INV-008", "INV-015"], async () => {
  await m.getByLabel("Problem statement", { exact: true }).fill("Mobile check problem");
  await m.getByRole("button", { name: "Start investigation", exact: true }).click(); await m.waitForTimeout(250);
  await m.getByRole("button", { name: "Add evidence" }).click(); await m.waitForTimeout(250);
  await fits("workspace with editor");
  const box = await m.getByRole("button", { name: "Save", exact: true }).boundingBox();
  expect(box && box.x + box.width <= 376, "Save button off screen");
});

await step("AT-00 no page or console errors", [], async () => { expect(errors.length === 0, errors.slice(0, 3).join(" | ")); });
await browser.close();

const summary = { base, at: new Date().toISOString(), passed: results.filter((r) => r.status === "PASS").length, failed: results.filter((r) => r.status === "FAIL").length, results };
if (out) writeFileSync(out, JSON.stringify(summary, null, 2) + "\n");
console.log(`acceptance: ${summary.passed} passed, ${summary.failed} failed`);
process.exit(summary.failed === 0 ? 0 : 1);
