// Layer B (VERIFICATION-STRATEGY.md): the fixture-backed investigation-list screen rendered by the real Limen 0.9.1
// kernel and Forma 0.12.0 CSS. The locators are copied from the governed journey's support.mjs (connect, lines 89-99)
// and J1-01/J1-02 so a screen that passes here is findable by the journeys; passing here is NOT a journey pass:
// no request reaches GitHub, and the data is a labelled fixture.
import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const axeSource = fs.readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8');
const TOKEN = 'fixture-secret-token-value-9f3c';

const firstVisible = async (candidates) => {
  for (const l of candidates) if (await l.count() && await l.first().isVisible()) return l.first();
  return candidates[0].first();
};
// support.mjs connect(), verbatim locators.
const connect = async (page) => {
  const owner = await firstVisible([page.getByLabel('Repository owner', { exact: true }), page.getByLabel(/owner/i)]);
  if (!(await owner.isVisible().catch(() => false))) await page.getByRole('button', { name: /connect/i }).first().click();
  await (await firstVisible([page.getByLabel('Repository owner', { exact: true }), page.getByLabel(/owner/i)])).fill('harness');
  await (await firstVisible([page.getByLabel('Repository', { exact: true }), page.getByLabel(/repository name|^repo/i)])).fill('causa-test');
  await (await firstVisible([page.getByLabel(/branch/i)])).fill('main');
  await (await firstVisible([page.getByLabel(/token|credential/i)])).fill(TOKEN);
  await page.getByRole('button', { name: /connect/i }).first().click();
};
const open = async (page, scenario) => { await page.goto(`/?scenario=${scenario}`); await expect(page.getByRole('button', { name: /connect data repository|connect/i }).first()).toBeVisible({ timeout: 20000 }); };
const axe = async (page) => {
  await page.addScriptTag({ content: axeSource });
  return page.evaluate(async () => (await window.axe.run(document, { resultTypes: ['violations'] })).violations.map((v) => `${v.id} (${v.impact}): ${v.nodes.map((n) => n.target.join(" ") + " " + (n.any[0]?.message || "")).join(" | ")}`));
};
const noSecret = async (page) => {
  const html = await page.evaluate(() => document.documentElement.outerHTML + [...document.querySelectorAll('input')].map((i) => i.type === 'password' ? '' : i.value).join(' '));
  expect(html.includes(TOKEN), 'the credential is rendered somewhere in the DOM').toBe(false);
};

test('connected catalog: J1-01/J1-02 locators find the screen; four stored cases listed; unknown is not zero', async ({ page }) => {
  await open(page, 'connected-catalog');
  await expect(page.getByRole('note')).toContainText('Fixture data');
  await connect(page);
  const region = page.getByRole('region', { name: 'Investigations' });
  await expect(region).toBeVisible();
  for (const t of ['Apollo 1 (AS-204)', 'Apollo 11 lunar landing', 'Space Shuttle Challenger (STS-51-L)', 'Burned-out ceiling lightbulb'])
    await expect(region.getByRole('link', { name: new RegExp(t.replace(/[()]/g, '\\$&')) })).toBeVisible();
  await expect(region.getByRole('article')).toHaveCount(4);
  await expect(region.getByRole('article').first().getByText('Not available').first()).toBeVisible();
  await expect(page.getByRole('link', { name: /new investigation/i }).or(page.getByRole('button', { name: /new investigation/i })).first()).toBeVisible();
  await expect(page.getByRole('navigation', { name: /primary|main/i }).getByRole('link', { name: 'Investigations', exact: true })).toHaveAttribute('aria-current', 'page');
  const href = await region.getByRole('link', { name: /Apollo 11/ }).getAttribute('href');
  // The route carries the workspace with the id (v4.1.5 W1: the same case can be stored in two workspaces).
  expect(href).toBe('#/investigations/e3adbee5-5096-5a10-bbfb-cecda86a154b?ws=workspaces%2Fdemo%2Fdemonstration-v1&role=catalog');
  await noSecret(page);
  expect(await axe(page)).toEqual([]);
});

test('validation: an empty Connect is refused with field messages and nothing is listed', async ({ page }) => {
  await open(page, 'connected-catalog');
  await page.getByRole('button', { name: /connect data repository/i }).click();
  await expect(page.getByRole('alert')).toContainText('Repository owner is required.');
  await expect(page.getByRole('region', { name: 'Investigations' })).toHaveCount(0);
});

test('empty catalog is shown as empty, not as an error', async ({ page }) => {
  await open(page, 'empty-catalog');
  await connect(page);
  await expect(page.getByText('There are no investigations in this repository yet.')).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('unauthorized repository: what happened, what is kept, next step; no fake retry', async ({ page }) => {
  await open(page, 'unauthorized-repository');
  await connect(page);
  const alert = page.getByRole('alert');
  await expect(alert).toContainText('You do not have access to this repository.');
  await expect(alert).toContainText('kept only in this tab');
  await expect(page.getByRole('button', { name: 'Try again' })).toHaveCount(0);
  await noSecret(page);
});

test('network failure offers a real retry', async ({ page }) => {
  await open(page, 'network-failure');
  await connect(page);
  await expect(page.getByRole('alert')).toContainText('cannot be reached');
  await expect(page.getByRole('button', { name: 'Try again' })).toBeVisible();
});

test('loading describes the task, not a progress figure', async ({ page }) => {
  await open(page, 'loading');
  await connect(page);
  await expect(page.getByRole('status').filter({ hasText: 'Opening investigations' })).toBeVisible();
});

test('390 px: no horizontal overflow; primary navigation reachable by keyboard', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await open(page, 'connected-catalog');
  await connect(page);
  await expect(page.getByRole('region', { name: 'Investigations' })).toBeVisible();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(0);
  await page.goto('/?scenario=connected-catalog');
  let reached = false;
  for (let i = 0; i < 25 && !reached; i++) {
    await page.keyboard.press('Tab');
    reached = await page.evaluate(() => document.activeElement?.closest('nav') !== null && /administration/i.test(document.activeElement?.textContent || ''));
  }
  expect(reached, 'Administration is not reachable by Tab').toBe(true);
});
