// Runs every executable layout assertion the Forma layout contract names:
//   positive: against each listed Forma pattern page, at each listed viewport and colour scheme; every run must pass;
//   negative: against the assertion's broken fixture (fixtures.mjs); every run must fail;
//   Forma gaps (contract formaGaps): the unmodified pattern must still fail (so a fixed Forma release is noticed) and
//   the pattern with the gap's remedy CSS, which may use only Forma tokens, must pass.
// Usage: node run.mjs --forma <extracted Forma package dir> --contract <forma-layout-contract.json>
//                     --inventory <forma inventory json> [--browsers chromium,webkit] [--out results.json]
// Exit 0 when every positive run passes and every negative run fails; 1 otherwise; 2 for unusable input.

import { readFileSync, writeFileSync, mkdtempSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';
import { assertions } from './assertions.mjs';
import { brokenFixtures } from './fixtures.mjs';

const require = createRequire(import.meta.url);
const playwright = require('playwright');

const args = Object.fromEntries(
  process.argv.slice(2).reduce((acc, a, k, all) => (a.startsWith('--') ? [...acc, [a.slice(2), all[k + 1]]] : acc), []),
);
if (!args.forma || !args.contract || !args.inventory) {
  console.error('usage: node run.mjs --forma DIR --contract FILE --inventory FILE [--browsers chromium,webkit] [--out FILE]');
  process.exit(2);
}

const contract = JSON.parse(readFileSync(args.contract, 'utf8'));
const inventory = JSON.parse(readFileSync(args.inventory, 'utf8')).customProperties;
const css = pathToFileURL(resolve(args.forma, 'dist/all.css')).href;
const scratch = mkdtempSync(join(tmpdir(), 'forma-layout-'));
const viewports = Object.fromEntries(contract.viewports.map(v => [v.id, v]));

const page = (name, body, zoom) => {
  const file = join(scratch, `${name.replace(/[^a-z0-9-]/gi, '_')}-${zoom}.html`);
  writeFileSync(file, `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${name}</title><link rel="stylesheet" href="${css}"><style>html{font-size:${zoom * 100}%}</style></head><body>${body}</body></html>`);
  return pathToFileURL(file).href;
};
const pattern = id => readFileSync(resolve(args.forma, 'patterns', `${id}.html`), 'utf8');

// Every (rule, assertion, pattern-or-fixture, viewport, scheme) run, positive and negative.
const gaps = contract.formaGaps || [];
const gapOf = (assertion, p, vp) => gaps.find(g => g.assertion === assertion && g.pattern === p && (g.viewports || []).includes(vp));
const runsOf = rule =>
  (rule.executable || []).flatMap(x => {
    const vps = x.viewports === 'all' || !x.viewports ? contract.viewports.map(v => v.id) : x.viewports;
    const schemes = x.colorSchemes || ['light'];
    const cell = (kind, target, body) => vps.flatMap(vp => schemes.map(scheme => ({ rule: rule.id, assertion: x.assertion, kind, target, body, vp, scheme })));
    return [
      ...x.patterns.flatMap(p =>
        vps.flatMap(vp => {
          const gap = gapOf(x.assertion, p, vp);
          const one = (kind, target, body) => schemes.map(scheme => ({ rule: rule.id, assertion: x.assertion, kind, target, body, vp, scheme }));
          return gap
            ? [...one('gap', p, pattern(p)), ...one('positive', `${p}+remedy`, `<style>${gap.remedyCss}</style>${pattern(p)}`)]
            : one('positive', p, pattern(p));
        }),
      ),
      ...cell('negative', `broken:${x.assertion}`, brokenFixtures[x.assertion]),
    ];
  });

const execute = browser => async run => {
  const vp = viewports[run.vp];
  const context = await browser.newContext({
    viewport: { width: vp.width, height: vp.height },
    colorScheme: run.scheme,
    reducedMotion: run.assertion === 'reducedMotion' ? 'reduce' : 'no-preference',
  });
  const tab = await context.newPage();
  try {
    await tab.goto(page(run.target, run.body, vp.textZoom || 1));
    const violations = await assertions[run.assertion](tab, { inventory });
    const ok = run.kind === 'positive' ? violations.length === 0 : violations.length > 0;
    // A gap run that passes means Forma fixed it: fail, so the gap entry is removed.
    if (run.kind === 'gap' && violations.length === 0) violations.push('the Forma gap is closed in this release; remove it from formaGaps');
    return { ...run, body: undefined, violations, ok };
  } catch (e) {
    return { ...run, body: undefined, violations: [`error: ${e.message}`], ok: false };
  } finally {
    await context.close();
  }
};

const sequence = (xs, f) => xs.reduce((acc, x) => acc.then(rs => f(x).then(r => [...rs, r])), Promise.resolve([]));

const main = async () => {
  const unknown = contract.rules.flatMap(r => (r.executable || []).filter(x => !assertions[x.assertion] || !brokenFixtures[x.assertion]).map(x => `${r.id}: ${x.assertion}`));
  if (unknown.length > 0) {
    console.error(`unknown assertions or missing fixtures: ${unknown.join(', ')}`);
    process.exit(2);
  }
  const runs = contract.rules.flatMap(runsOf);
  const names = (args.browsers || 'chromium').split(',');
  const results = await sequence(names, async name => {
    const browser = await playwright[name].launch();
    const rs = await sequence(runs, execute(browser));
    await browser.close();
    return { browser: name, version: browser.version(), runs: rs };
  });
  const summary = results.map(b => ({
    browser: b.browser,
    version: b.version,
    positive: b.runs.filter(r => r.kind === 'positive').length,
    negative: b.runs.filter(r => r.kind === 'negative').length,
    gaps: b.runs.filter(r => r.kind === 'gap').length,
    failed: b.runs.filter(r => !r.ok).map(r => ({ rule: r.rule, assertion: r.assertion, kind: r.kind, target: r.target, viewport: r.vp, scheme: r.scheme, violations: r.violations.slice(0, 5) })),
  }));
  if (args.out) writeFileSync(args.out, JSON.stringify({ summary, results }, null, 2) + '\n');
  summary.forEach(s => {
    console.log(`${s.browser} ${s.version}: ${s.positive} positive, ${s.negative} negative and ${s.gaps} Forma-gap runs, ${s.failed.length} failed`);
    s.failed.slice(0, 40).forEach(f => console.log(`  FAIL ${f.rule} ${f.assertion} ${f.kind} ${f.target} ${f.viewport} ${f.scheme}: ${f.violations.join('; ')}`));
  });
  process.exit(summary.every(s => s.failed.length === 0) ? 0 : 1);
};

main().catch(e => { console.error(e); process.exit(2); });
