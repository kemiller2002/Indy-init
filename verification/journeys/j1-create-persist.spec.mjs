// J1: investigation creation and persistence (bindings.json). Governed; the builder cannot edit it.
import { test, expect } from '@playwright/test';
import { env, assertion, contextFor, connect, startInvestigation, saveState, button, providerHolds } from './support.mjs';

test('J1 investigation creation and persistence', async ({ browser }) => {
  const a = await contextFor(browser);
  const page = await a.newPage();

  await assertion('J1', 'J1-01', async () => {
    await page.goto(env.app);
    await expect(page.getByRole('button', { name: /connect data repository|connect/i }).first()).toBeVisible({ timeout: 20000 });
  });
  await assertion('J1', 'J1-02', async () => {
    await connect(page);
    await expect(page.getByRole('link', { name: /new investigation/i }).or(page.getByRole('button', { name: /new investigation/i })).first()).toBeVisible({ timeout: 20000 });
  });
  let v;
  await assertion('J1', 'J1-03', async () => {
    v = await startInvestigation(page, 'J1');
    await expect(page.getByText(v.problem).first()).toBeVisible();
  });
  await assertion('J1', 'J1-04', async () => {
    // Saved must not appear before the commit: GitHub is checked as soon as Saved shows.
    await expect(saveState(page)).not.toHaveText(/^\s*Saved\s*$/);
    await button(page, 'Save').click();
    await expect(saveState(page)).toHaveText(/^\s*Saved\s*$/, { timeout: 20000 });
    await providerHolds([v.problem]);
  });
  await assertion('J1', 'J1-05', async () => { await providerHolds([v.problem, v.title, v.question]); });
  await assertion('J1', 'J1-06', async () => {
    const b = await contextFor(browser);
    const fresh = await b.newPage();
    await connect(fresh);
    await fresh.getByRole('link', { name: v.title }).click();
    for (const value of [v.problem, v.title, v.question]) await expect(fresh.getByText(value).first()).toBeVisible({ timeout: 20000 });
    if (v.id) expect(fresh.url()).toContain(v.id);
    await b.close();
  });
  await a.close();
});
