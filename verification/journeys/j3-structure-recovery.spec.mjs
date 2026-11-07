// J3: application structure and recovery (bindings.json). Governed; the builder cannot edit it.
// Conflict is produced by a real second writer; a provider refusal and a lost answer are injected at the browser's
// network boundary (the request still targets the real provider path), never by replacing persistence.
import { test, expect } from '@playwright/test';
import { env, assertion, contextFor, connect, startInvestigation, fieldNamed, button, section, saveState, saveAndConfirm, marker } from './support.mjs';
import { finish } from './support.mjs';

// The next save request, in whichever form the application sends it: a REST Contents API PUT (the positive-control
// fixture) or the persistence contract's GraphQL createCommitOnBranch (PERSISTENCE-CONTRACT, OD-INDY-9; the error
// matrix's operation graphql:createCommitOnBranch). Only the first save request is intercepted; reads and later
// requests pass. Returns a function that removes the interception.
const onNextSave = async (page, onRest, onGraphql) => {
  let fired = false;
  const once = (matches, handle) => async (route) => {
    if (fired || !matches(route.request())) return route.fallback();
    fired = true;
    return handle(route);
  };
  const rest = once((r) => r.method() === 'PUT', onRest);
  const graphql = once((r) => r.method() === 'POST' && (r.postData() || '').includes('createCommitOnBranch'), onGraphql);
  await page.route(/\/contents\//, rest);
  await page.route(/\/graphql(\?.*)?$/, graphql);
  return async () => { await page.unroute(/\/contents\//, rest); await page.unroute(/\/graphql(\?.*)?$/, graphql); };
};
// The provider a request goes to: GitHub, or the harness double in harness-double mode (as support.mjs contextFor).
const providerUrl = (url) => { const u = new URL(url); return env.provider === 'harness-double' ? env.double + u.pathname + u.search : u.href; };

const primary = (page) => page.getByRole('navigation', { name: /primary|main/i }).first();
const recordObservation = async (page, text) => {
  await section(page, 'Overview');
  await fieldNamed(page, 'Observation text').fill(text);
  await button(page, 'Record an observation').click();
};

test('J3 application structure and recovery', async ({ browser }) => {
  const ctx = await contextFor(browser);
  const page = await ctx.newPage();

  await assertion('J3', 'J3-10', async () => {
    const slow = await contextFor(browser);
    const p = await slow.newPage();
    await p.route(/\.(m?js|wasm|dll|dat|blat)(\?.*)?$/, async (route) => { await new Promise((r) => setTimeout(r, 1500)); await route.continue(); });
    await p.goto(env.app, { waitUntil: 'commit' });
    await expect(p.getByText(/loading/i).first()).toBeVisible({ timeout: 1400 });
    await slow.close();
  });
  await connect(page);
  await assertion('J3', 'J3-01', async () => {
    for (const name of [ 'Administration', 'Settings', 'Investigations' ]) {
      await primary(page).getByRole('link', { name, exact: true }).click();
      await expect(primary(page).getByRole('link', { name, exact: true })).toHaveAttribute('aria-current', /page|true|location/);
      await expect(page.getByRole('heading', { name: new RegExp(name, 'i') }).first()).toBeVisible();
    }
  });
  await assertion('J3', 'J3-02', async () => {
    await primary(page).getByRole('link', { name: 'Administration', exact: true }).click();
    await expect(page.getByText(env.repo).first()).toBeVisible();
    await expect(page.getByText(/github/i).first()).toBeVisible();
    await primary(page).getByRole('link', { name: 'Settings', exact: true }).click();
    await expect(page.locator('main').getByRole('radio').or(page.locator('main').getByRole('checkbox')).or(page.locator('main').getByRole('combobox')).first()).toBeVisible();
    await primary(page).getByRole('link', { name: 'Investigations', exact: true }).click();
  });
  const v = await startInvestigation(page, 'J3');
  await saveAndConfirm(page);
  const kept = marker('kept');
  await assertion('J3', 'J3-03', async () => {
    await recordObservation(page, kept);
    await expect(saveState(page)).toHaveText(/^\s*Unsaved\s*$/);
  });
  await assertion('J3', 'J3-04', async () => {
    await ctx.setOffline(true);
    await recordObservation(page, marker('offline'));
    await expect(saveState(page)).toHaveText(/^\s*Offline\s*$/);
    await expect(page.getByText(kept).first()).toBeVisible();
    await ctx.setOffline(false);
    await expect(saveState(page)).toHaveText(/^\s*Unsaved\s*$/, { timeout: 10000 });
  });
  await assertion('J3', 'J3-06', async () => {
    // A provider refusal (GH-VALIDATION): REST answers 422; GraphQL answers 200 with an UNPROCESSABLE error.
    const restore = await onNextSave(page,
      (route) => route.fulfill({ status: 422, contentType: 'application/json', body: '{"message":"refused"}' }),
      (route) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data: { createCommitOnBranch: null }, errors: [{ type: 'UNPROCESSABLE', path: ['createCommitOnBranch'], locations: [{ line: 1, column: 1 }], message: 'refused' }] }) }));
    try {
      await button(page, 'Save').click();
      await expect(saveState(page)).toHaveText(/^\s*Save failed\s*$/, { timeout: 10000 });
      await expect(page.getByText(kept).first()).toBeVisible();
    } finally { await restore(); }
  });
  await assertion('J3', 'J3-07', async () => {
    // A lost answer (GH-NETWORK-AFTER). The GraphQL mutation reaches the provider, then its answer is lost.
    const restore = await onNextSave(page,
      (route) => route.abort('connectionreset'),
      async (route) => { await route.fetch({ url: providerUrl(route.request().url()) }).catch(() => {}); await route.abort('connectionreset'); });
    try {
      await button(page, 'Save').click();
      await expect(saveState(page)).toHaveText(/unknown commit outcome/i, { timeout: 10000 });
      await expect(saveState(page)).not.toHaveText(/^\s*Saved\s*$/);
    } finally { await restore(); }
  });
  await assertion('J3', 'J3-05', async () => {
    // Reach Saved first (the lost answer may or may not have written), then two writers edit the same investigation.
    const fresh = await (await contextFor(browser)).newPage();
    await connect(fresh);
    await fresh.getByRole('link', { name: v.title }).click();
    await recordObservation(fresh, marker('writer-b'));
    await saveAndConfirm(fresh);
    const mine = marker('writer-a');
    await recordObservation(page, mine);
    await button(page, 'Save').click();
    await expect(saveState(page)).toHaveText(/^\s*Conflict\s*$/, { timeout: 20000 });
    await expect(page.getByText(mine).first()).toBeVisible();
  });
  await assertion('J3', 'J3-08', async () => {
    const k = await (await contextFor(browser)).newPage();
    await k.goto(env.app);
    let reached = false;
    for (let i = 0; i < 25 && !reached; i++) {
      await k.keyboard.press('Tab');
      reached = await k.evaluate(() => document.activeElement?.closest('nav') !== null && /administration/i.test(document.activeElement?.textContent || ''));
    }
    expect(reached, 'Administration is not reachable by Tab').toBe(true);
    const outline = await k.evaluate(() => { const s = getComputedStyle(document.activeElement); return s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0 || s.boxShadow !== 'none'; });
    expect(outline, 'the focused navigation link shows no visible focus').toBe(true);
    await k.keyboard.press('Enter');
    await expect(k.getByRole('heading', { name: /administration/i }).first()).toBeVisible();
  });
  await assertion('J3', 'J3-09', async () => {
    const m = await (await contextFor(browser)).newPage();
    await m.setViewportSize({ width: 390, height: 844 });
    await connect(m);
    await m.getByRole('link', { name: v.title }).click();
    await expect(m.getByText(v.problem).first()).toBeVisible({ timeout: 20000 });
    const overflow = await m.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, 'horizontal overflow at 390 px').toBeLessThanOrEqual(0);
  });
  await ctx.close();
  finish();
});
