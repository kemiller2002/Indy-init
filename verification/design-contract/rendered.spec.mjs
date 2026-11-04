// The rendered half of the governed design contract (adversarial audit R5): at every contract viewport, on the list
// and on a populated workspace, the Forma shell and save-status roles are present in the DOM, the UI-016 palette is
// actually rendered, and the pinned Forma layout kit (FL-01..FL-10, requirements-compiled/<package>/layout-tests)
// finds no violation. Screenshots are published for the owner's independent visual review (UI-016-O16); this check is
// not that review. Writes indy-init.design-rendered/1 to CAUSA_DESIGN_RENDERED.
import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
import { env, contextFor, connect, startInvestigation, fieldNamed, button, section } from '../journeys/support.mjs';

const root = path.resolve(new URL('../..', import.meta.url).pathname);
const contract = JSON.parse(fs.readFileSync(process.env.CAUSA_DESIGN_CONTRACT || path.join(root, 'kickoff/rehearsal-010/design-contract.json'), 'utf8'));
const kit = await import(path.join(root, process.env.CAUSA_LAYOUT_KIT || 'requirements-compiled/v4.1.4/layout-tests/assertions.mjs'));
const out = process.env.CAUSA_DESIGN_RENDERED || 'design-rendered.json';
const shots = process.env.CAUSA_SCREENSHOTS || 'design-screenshots';
const failures = [];
const screenshots = [];
const fail = (check, viewport, detail) => failures.push({ check, viewport, detail: String(detail).slice(0, 400) });

const hex = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16));
const rendered = (page) => page.evaluate(() => {
  const seen = new Set();
  for (const el of document.querySelectorAll('body, body *')) {
    const s = getComputedStyle(el);
    for (const c of [s.color, s.backgroundColor, s.borderTopColor, s.borderRightColor, s.borderBottomColor, s.borderLeftColor, s.outlineColor]) seen.add(c);
  }
  return [...seen];
});

// Static layout rules run on every state; focus rules press Tab from the start of a freshly loaded page.
const layoutChecks = ['touchTargets', 'noHorizontalScroll', 'textNotClipped', 'textContrast', 'nonTextContrast'];
const focusChecks = ['skipLinkFirst', 'focusVisible', 'navigationReachable'];

const judge = async (page, viewport, where, checks = layoutChecks) => {
  for (const r of contract.roles.filter((x) => (x.screens || ['list', 'workspace']).includes(where === 'fresh' ? 'list' : where))) {
    const present = await page.evaluate((names) => names.some((n) => document.querySelector(`.${n}, ${n}`)), r.names);
    if (!present) fail(`role ${r.role} (${r.requirement})`, viewport, `${where}: no element uses ${r.names.join(' or ')}`);
  }
  const nav = page.getByRole('navigation').filter({ hasText: contract.primaryDestinations.names[0] }).first();
  for (const d of contract.primaryDestinations.names) {
    if (!(await nav.getByRole('link', { name: d, exact: true }).count())) fail(`destination ${d} (UI-020)`, viewport, `${where}: no navigation link`);
  }
  for (const name of checks) {
    try {
      const v = await kit[name](page);
      for (const x of v || []) fail(name, viewport, `${where}: ${typeof x === 'string' ? x : JSON.stringify(x)}`);
    } catch (e) { fail(name, viewport, `${where}: the check could not run: ${e.message}`); }
  }
  const file = path.join(shots, `${viewport}-${where}.png`);
  await page.screenshot({ path: file, fullPage: true });
  screenshots.push(file);
};

test('design contract (rendered)', async ({ browser }) => {
  fs.mkdirSync(shots, { recursive: true });
  const colours = new Set();
  for (const vp of contract.rendered.viewports) {
    const ctx = await contextFor(browser);
    const page = await ctx.newPage();
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await page.goto(env.app);
    await judge(page, vp.id, 'fresh', focusChecks);
    await connect(page);
    await expect(page.getByRole('link', { name: /new investigation/i }).or(page.getByRole('button', { name: /new investigation/i })).first()).toBeVisible({ timeout: 20000 });
    await judge(page, vp.id, 'list');
    (await rendered(page)).forEach((c) => colours.add(c));
    await startInvestigation(page, `design-${vp.id}`);
    // A populated workspace: an observation, a hypothesis, evidence and a contradiction (so every palette role has a
    // state whose intended use it is: UI-016 coral marks contradictions, amber unsaved/pending attention). A step the
    // candidate cannot perform is a failure, never skipped.
    const step = async (what, fn) => { try { await fn(); } catch (e) { fail('populate', vp.id, `${what}: ${String(e.message).split('\n')[0]}`); } };
    await step('record an observation', async () => { await fieldNamed(page, 'Observation text').fill('A populated workspace for the design contract'); await button(page, 'Record an observation').click(); });
    await step('record a hypothesis', async () => { await section(page, 'Hypotheses'); await fieldNamed(page, 'Hypothesis statement').fill('A hypothesis under test'); await button(page, 'Record a hypothesis').click(); });
    await step('record evidence', async () => { await section(page, 'Evidence'); await fieldNamed(page, 'Evidence label').fill('Contradicting evidence'); await page.getByRole('button', { name: /record evidence|add evidence/i }).first().click(); });
    await step('link as contradicts', async () => { await page.getByRole('group', { name: /evidence relationship kind/i }).getByLabel('contradicts', { exact: true }).check(); await fieldNamed(page, 'Evidence relationship rationale').fill('It rules the hypothesis out'); await button(page, 'Link evidence to a hypothesis').click(); });
    await judge(page, vp.id, 'workspace');
    (await rendered(page)).forEach((c) => colours.add(c));
    await ctx.close();
  }
  for (const p of contract.palette) {
    const [r, g, b] = hex(p.hex);
    if (![...colours].some((c) => c.replace(/\s/g, '').startsWith(`rgb(${r},${g},${b}`) || c.replace(/\s/g, '').startsWith(`rgba(${r},${g},${b},`)))
      fail(`palette ${p.role} ${p.hex} (UI-016)`, 'all', 'never rendered in any populated state');
  }
  fs.writeFileSync(out, JSON.stringify({ schema: 'indy-init.design-rendered/1', candidate: process.env.CAUSA_CANDIDATE || '', failures, screenshots }, null, 2) + '\n');
  expect(failures, failures.map((f) => `${f.check} @${f.viewport}: ${f.detail}`).join('\n')).toEqual([]);
});
