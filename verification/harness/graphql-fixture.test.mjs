// The J3 provider-refusal and lost-answer injections reach an application that saves the way the persistence contract
// says (GraphQL createCommitOnBranch), not only one using the Contents API (review F3):
//   positive: the GraphQL fixture (fixture-app-graphql over fixture-app/web) passes J3 against the harness double;
//   negative: the same application with a save that never reaches GitHub and always shows Saved fails J3, at J3-06.
//
//   node --test verification/harness/graphql-fixture.test.mjs
//
// Needs verification/journeys/node_modules (@playwright/test 1.63.0) and a Chromium Playwright can launch.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { listen } from './github-double.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const journeys = path.join(here, '..', 'journeys');
const work = fs.mkdtempSync(path.join(os.tmpdir(), 'graphql-fixture-'));

// fixture-app/web with the GraphQL app.js; the negative replaces the save with one that reports Saved and sends nothing.
const assemble = (name, edit) => {
  const dir = path.join(work, name);
  fs.cpSync(path.join(here, 'fixture-app', 'web'), dir, { recursive: true });
  const app = fs.readFileSync(path.join(here, 'fixture-app-graphql', 'web', 'app.js'), 'utf8');
  fs.writeFileSync(path.join(dir, 'app.js'), edit(app));
  return dir;
};
const positive = assemble('graphql', (s) => s);
const negative = assemble('graphql-always-saved', (s) => {
  const marker = 'const save = async () => {';
  assert.ok(s.includes(marker), 'the GraphQL fixture has no save function to replace');
  return s.replace(marker, `${marker}\n  return set((s) => ({ ...s, dirty: false, saveState: 'Saved', message: '' }));`);
});

const serve = (dir) => new Promise((ok) => {
  const p = spawn(process.execPath, [path.join(here, 'serve.mjs'), dir, '0'], { stdio: ['ignore', 'pipe', 'inherit'] });
  p.stdout.on('data', (d) => { const m = String(d).match(/on (\d+)/); if (m) ok({ p, url: `http://127.0.0.1:${m[1]}/` }); });
});

const j3 = async (dir, label) => {
  const double = await listen(0);
  const app = await serve(dir);
  const results = path.join(work, `${label}.json`);
  try {
    // Asynchronous: the double answers from this process, so the event loop must keep running while Playwright does.
    const r = await new Promise((ok) => {
      const p = spawn('npx', ['playwright', 'test', '--project=chromium', 'j3-structure-recovery.spec.mjs'], {
        cwd: journeys,
        env: { ...process.env, CAUSA_PROVIDER: 'harness-double', CAUSA_APP_URL: app.url, CAUSA_DOUBLE_URL: `http://127.0.0.1:${double.address().port}`, CAUSA_CANDIDATE: label, CAUSA_JOURNEY_RESULTS: results },
      });
      let stdout = '', stderr = '';
      p.stdout.on('data', (d) => (stdout += d));
      p.stderr.on('data', (d) => (stderr += d));
      const timer = setTimeout(() => p.kill(), 300000);
      p.on('close', (status) => { clearTimeout(timer); ok({ status, stdout, stderr }); });
    });
    const assertions = fs.existsSync(results)
      ? JSON.parse(fs.readFileSync(results, 'utf8')).results.flatMap((x) => x.assertions || [])
      : [];
    return { status: r.status, output: (r.stdout || '') + (r.stderr || ''), failed: assertions.filter((a) => a.status !== 'passed').map((a) => a.id) };
  } finally {
    app.p.kill();
    double.close();
  }
};

test('J3 passes the GraphQL-saving fixture (the injections reach createCommitOnBranch)', async () => {
  const r = await j3(positive, 'graphql');
  assert.equal(r.status, 0, r.output.slice(-3000));
  assert.deepEqual(r.failed, []);
});

test('J3 refuses the GraphQL fixture whose save always shows Saved', async () => {
  const r = await j3(negative, 'graphql-always-saved');
  assert.notEqual(r.status, 0, 'J3 passed an application whose save never reaches GitHub');
  assert.ok(r.failed.includes('J3-06'), `J3-06 did not fail (failed: ${r.failed.join(', ')})`);
});
