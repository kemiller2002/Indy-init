// Practice only (FENCE.md). Connect -> Browse -> Open -> Create -> Save -> Reload, through the real F# WebAssembly app,
// against the harness REST double seeded with the real causa-data files. Component behavior (double), NOT real
// persistence acceptance.
import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const DOUBLE = 'http://127.0.0.1:4632';
const DATA = process.env.CAUSA_DATA;
const WS = 'workspaces/demo/demonstration-v1/investigations';

test.beforeAll(async ({ request }) => {
  if (!DATA) throw new Error('set CAUSA_DATA to a causa-data clone');
  await request.post(`${DOUBLE}/__control`, { data: { reset: true } });
  for (const dir of fs.readdirSync(path.join(DATA, WS))) {
    for (const name of ['manifest.json', 'events.ndjson', 'snapshot.json']) {
      const content = fs.readFileSync(path.join(DATA, WS, dir, name)).toString('base64');
      const r = await request.put(`${DOUBLE}/repos/kemiller2002/causa-data/contents/${WS}/${dir}/${name}`, { data: { message: 'seed', content, branch: 'main' } });
      expect(r.ok()).toBeTruthy();
    }
  }
});

const connect = async (page, token) => {
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-ready', /.+/, { timeout: 60000 });
  await page.fill('#c-api', DOUBLE);
  if (token) await page.fill('#c-token', token);
  await page.getByTestId('connect').click();
  await expect(page.getByTestId('connection-status')).toContainText('Connected to kemiller2002/causa-data@main');
};

test('Connect -> Browse -> Open -> Create -> Save -> Reload', async ({ page }, info) => {
  const marks = {};
  const t0 = Date.now();
  const mark = (k) => { marks[k] = Date.now() - t0; };
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => m.type() === 'error' && errors.push(m.text()));

  await connect(page, 'practice-test-token');
  mark('connect');
  await expect(page.getByTestId('double-warning')).toBeVisible();

  // Browse: the four real investigations, each decoded, replayed and verified
  const rows = page.getByTestId('open-investigation');
  await expect(rows).toHaveCount(4);
  await expect(page.getByTestId('notice')).toHaveText('4 investigation(s) found.');
  await expect(page.getByText('verified: decoded, replayed, snapshot agrees')).toHaveCount(4, { timeout: 60000 });
  await expect(page.getByText('Apollo 1 (AS-204) Command Module fire, 27 January 1967')).toBeVisible();
  mark('browse');

  // Open: four distinct layers
  await page.getByText('Apollo 1 (AS-204) Command Module fire, 27 January 1967').click();
  await expect(page.getByTestId('open-title')).toHaveText('Apollo 1 (AS-204) Command Module fire, 27 January 1967');
  await expect(page.getByTestId('layer-raw')).toContainText('Raw:');
  await expect(page.getByTestId('layer-decoded')).toContainText('Decoded: 432 events');
  await expect(page.getByTestId('layer-replayed')).toContainText('Replayed: this app folded the events to version 432');
  await expect(page.getByTestId('layer-authoritative')).toContainText('equals the replayed projection');
  mark('open');

  // Create -> Save -> read-back
  const title = `Practice ${info.project.name} ${Date.now()}`;
  await page.getByRole('button', { name: 'New investigation' }).click();
  await page.getByRole('button', { name: 'Create and save' }).click();
  await expect(page.getByText('Enter a title.')).toBeVisible();
  await page.fill('#n-title', title);
  await page.fill('#n-original', 'Pump P-101 tripped during the practice run.');
  await page.getByRole('button', { name: 'Create and save' }).click();
  mark('create');
  await expect(page.getByTestId('save-status')).toHaveText('Saved: all 3 files written to the repository.');
  await expect(page.getByTestId('readback-status')).toContainText('Read-back verified');
  mark('saveReadBack');

  // Reload: a new page load (fresh engine, nothing in memory), reconnect read-only, browse, open the saved investigation
  await page.reload();
  await connect(page, '');
  await expect(rows).toHaveCount(5);
  await expect(page.getByTestId('notice')).toHaveText('5 investigation(s) found.');
  await page.getByText(title).click();
  await expect(page.getByTestId('open-title')).toHaveText(title);
  await expect(page.getByTestId('layer-decoded')).toContainText('Decoded: 1 events');
  await expect(page.getByTestId('layer-authoritative')).toContainText('equals the replayed projection');
  mark('reload');

  // the token never reaches the DOM
  expect(await page.content()).not.toContain('practice-test-token');
  // no horizontal overflow on this viewport
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(0);
  expect(errors).toEqual([]);
  await page.screenshot({ path: `evidence/${info.project.name}-reload.png`, fullPage: false });
  fs.writeFileSync(`evidence/${info.project.name}-timings.json`, JSON.stringify(marks, null, 2));
});

test('a refused write is shown as a failed save, never as saved', async ({ page, request }) => {
  await connect(page, 'practice-test-token');
  await request.post(`${DOUBLE}/__control`, { data: { failNext: 403 } });
  await page.getByRole('button', { name: 'New investigation' }).click();
  await page.fill('#n-title', 'Refused write');
  await page.fill('#n-original', 'This save must fail.');
  await page.getByRole('button', { name: 'Create and save' }).click();
  await expect(page.getByTestId('save-status')).toContainText('Save FAILED');
  await expect(page.getByTestId('readback-status')).not.toContainText('verified');
});
