// Shared steps for the governed journeys. Black-box: the application is driven by role and accessible name only, and
// persistence is checked by reading GitHub directly, never through the application.
//
// Environment:
//   CAUSA_APP_URL         the candidate's served application (deployed exact SHA, or a local build of it)
//   CAUSA_CANDIDATE       the candidate commit SHA the results are bound to
//   CAUSA_PROVIDER        github (default: real provider) | harness-double (synthetic fixtures only; never evidence)
//   CAUSA_GITHUB_TOKEN    a scoped token for the designated test repository (github mode)
//   CAUSA_TEST_OWNER, CAUSA_TEST_REPO, CAUSA_TEST_BRANCH   the designated test repository
//   CAUSA_DOUBLE_URL      the harness double's base URL (harness-double mode)
import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const bindings = JSON.parse(fs.readFileSync(new URL('./bindings.json', import.meta.url), 'utf8'));
export const env = {
  app: process.env.CAUSA_APP_URL || 'http://127.0.0.1:4620/',
  provider: process.env.CAUSA_PROVIDER || 'github',
  token: process.env.CAUSA_GITHUB_TOKEN || 'harness-token',
  owner: process.env.CAUSA_TEST_OWNER || 'harness',
  repo: process.env.CAUSA_TEST_REPO || 'causa-test',
  branch: process.env.CAUSA_TEST_BRANCH || 'main',
  double: process.env.CAUSA_DOUBLE_URL || 'http://127.0.0.1:4610',
};
export const providerBase = env.provider === 'harness-double' ? env.double : 'https://api.github.com';
export const marker = (label) => `${label}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;

/// One bound assertion. Every assertion runs and is recorded (so the slice gate can judge exactly the assertions bound
/// to a slice's requirements); finish() then fails the journey if any P0 assertion failed. P1 and ODR-pending failures
/// are recorded and reported, never failing the journey.
export const assertion = async (journey, id, fn) => {
  const spec = bindings.journeys.find((j) => j.id === journey).assertions.find((a) => a.id === id);
  if (!spec) throw new Error(`assertion ${id} is not bound in bindings.json`);
  const record = (status, detail) => test.info().annotations.push({ type: 'assertion', description: JSON.stringify({ id, requirements: spec.requirements, priority: spec.priority, status, detail }) });
  try {
    await test.step(`${id} ${spec.check}`, fn);
    record('passed', '');
  } catch (e) {
    record('failed', String(e.message || e).split('\n')[0]);
  }
};
export const finish = () => {
  const failed = test.info().annotations.filter((a) => a.type === 'assertion').map((a) => JSON.parse(a.description)).filter((a) => a.priority === 'P0' && a.status !== 'passed');
  if (failed.length) throw new Error(`P0 assertion(s) failed: ${failed.map((a) => `${a.id} (${a.detail})`).join('; ')}`);
};

/// In harness-double mode the browser's requests to api.github.com are answered by the double, so a fixture app that
/// talks to GitHub runs unchanged. In github mode nothing is intercepted.
export const contextFor = async (browser) => {
  const context = await browser.newContext();
  if (env.provider === 'harness-double') {
    await context.route('https://api.github.com/**', async (route) => {
      const u = new URL(route.request().url());
      // Read the double's whole answer before fulfilling: a streamed response can be disposed if the page moves on
      // (Playwright "Fetch response has been disposed"). A request the browser already abandoned stays abandoned.
      let answer;
      try {
        const response = await route.fetch({ url: env.double + u.pathname + u.search });
        answer = { status: response.status(), headers: response.headers(), body: await response.body() };
      } catch (error) {
        await route.abort().catch(() => {});
        return;
      }
      await route.fulfill(answer).catch(() => {});
    });
  }
  return context;
};

/// Reads every blob of the test branch from the provider itself and returns their decoded text.
export const providerTexts = async () => {
  const h = { authorization: `Bearer ${env.token}`, accept: 'application/vnd.github+json' };
  const tree = await (await fetch(`${providerBase}/repos/${env.owner}/${env.repo}/git/trees/${env.branch}?recursive=1`, { headers: h })).json();
  const blobs = (tree.tree || []).filter((t) => t.type === 'blob');
  return Promise.all(blobs.map(async (b) => {
    const j = await (await fetch(`${providerBase}/repos/${env.owner}/${env.repo}/git/blobs/${b.sha}`, { headers: h })).json();
    return Buffer.from(j.content || '', 'base64').toString('utf8');
  }));
};
export const providerHolds = async (values) => {
  const texts = (await providerTexts()).join('\n');
  const missing = values.filter((v) => !texts.includes(v));
  expect(missing, `values missing from GitHub (read directly): ${missing.join(', ')}`).toEqual([]);
};

const firstVisible = async (candidates) => {
  for (const l of candidates) if (await l.count() && await l.first().isVisible()) return l.first();
  return candidates[0].first();
};
export const connect = async (page) => {
  await page.goto(env.app);
  const owner = await firstVisible([page.getByLabel('Repository owner', { exact: true }), page.getByLabel(/owner/i)]);
  if (!(await owner.isVisible().catch(() => false))) await page.getByRole('button', { name: /connect/i }).first().click();
  await (await firstVisible([page.getByLabel('Repository owner', { exact: true }), page.getByLabel(/owner/i)])).fill(env.owner);
  await (await firstVisible([page.getByLabel('Repository', { exact: true }), page.getByLabel(/repository name|^repo/i)])).fill(env.repo);
  await (await firstVisible([page.getByLabel(/branch/i)])).fill(env.branch);
  await (await firstVisible([page.getByLabel(/token|credential/i)])).fill(env.token);
  await page.getByRole('button', { name: /connect/i }).first().click();
};

export const saveState = (page) => page.getByRole('status', { name: /save state/i }).or(page.locator('[data-requirement-id="INV-016"]')).first();
export const fieldNamed = (page, name) => page.getByLabel(name, { exact: true });
export const button = (page, name) => page.getByRole('button', { name, exact: true });
export const section = async (page, name) => { await page.getByRole('navigation', { name: /investigation sections|sections/i }).getByRole('link', { name, exact: true }).click(); };

/// Starts and saves a new investigation; returns its values and the URL of its workspace.
export const startInvestigation = async (page, label) => {
  const v = { problem: marker(`${label}-problem`), title: marker(`${label}-title`), question: marker(`${label}-question`) };
  await page.getByRole('link', { name: /new investigation/i }).or(page.getByRole('button', { name: /new investigation/i })).first().click();
  await fieldNamed(page, 'Problem statement').fill(v.problem);
  await fieldNamed(page, 'Title').fill(v.title);
  await fieldNamed(page, 'Primary investigative question').fill(v.question);
  v.id = await fieldNamed(page, 'Investigation identifier').inputValue().catch(() => '');
  await button(page, 'Start investigation').click();
  return v;
};
export const saveAndConfirm = async (page) => {
  await button(page, 'Save').click();
  await expect(saveState(page)).toHaveText(/^\s*Saved\s*$/, { timeout: 20000 });
};

export const resultsPath = () => path.resolve(process.env.CAUSA_JOURNEY_RESULTS || 'journey-results.json');
