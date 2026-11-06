// J2: complete investigation analysis (bindings.json). Governed; the builder cannot edit it.
import { test, expect } from '@playwright/test';
import { assertion, contextFor, connect, startInvestigation, fieldNamed, button, section, saveAndConfirm, providerHolds, marker } from './support.mjs';
import { finish } from './support.mjs';

const kind = async (page, value) => {
  const group = page.getByRole('group', { name: /evidence relationship kind/i }).or(page.getByRole('radiogroup', { name: /evidence relationship kind/i })).first();
  await group.getByLabel(value, { exact: true }).check();
};
// ODR-V416-RC3 A: the required choice groups INV-045 (evidence category) and INV-046 (conclusion strength) start
// with nothing selected, so the journey chooses a value; the label may be the wire value or its words.
const choose = async (page, groupName, label) => {
  const group = page.getByRole('group', { name: groupName }).or(page.getByRole('radiogroup', { name: groupName })).first();
  await group.getByLabel(label).check();
};
const pick = async (page, pattern, label) => {
  const select = page.getByRole('combobox', { name: pattern }).first();
  await select.selectOption({ label });
};
const linkWith = async (page, evidence, hypothesis, k, rationale, action) => {
  await pick(page, /evidence/i, evidence);
  await pick(page, /hypothes/i, hypothesis);
  await kind(page, k);
  await fieldNamed(page, 'Evidence relationship rationale').fill(rationale);
  await button(page, action).click();
};

test('J2 complete investigation analysis', async ({ browser }) => {
  const ctx = await contextFor(browser);
  const page = await ctx.newPage();
  await connect(page);
  const v = await startInvestigation(page, 'J2');
  const obs = { text: marker('obs'), source: marker('obs-src') };
  const h1 = marker('hyp-a'), h2 = marker('hyp-b');
  const e1 = { label: marker('ev-a'), source: marker('ev-a-src'), content: marker('ev-a-content') };
  const e2 = { label: marker('ev-b'), source: marker('ev-b-src'), content: marker('ev-b-content') };
  const r1 = marker('why-supports'), r2 = marker('why-contradicts'), r3 = marker('why-reassessed'), conclusion = marker('conclusion');

  await assertion('J2', 'J2-01', async () => {
    await section(page, 'Overview');
    await fieldNamed(page, 'Observation text').fill(obs.text);
    await fieldNamed(page, 'Observation source reference').fill(obs.source);
    await button(page, 'Record an observation').click();
    await expect(page.getByText(obs.text).first()).toBeVisible();
  });
  await assertion('J2', 'J2-02', async () => {
    await section(page, 'Hypotheses');
    for (const h of [h1, h2]) { await fieldNamed(page, 'Hypothesis statement').fill(h); await button(page, 'Record a hypothesis').click(); }
    for (const h of [h1, h2]) await expect(page.getByText(h).first()).toBeVisible();
  });
  await assertion('J2', 'J2-03', async () => {
    await section(page, 'Evidence');
    for (const e of [e1, e2]) {
      await fieldNamed(page, 'Evidence label').fill(e.label);
      await fieldNamed(page, 'Evidence source reference').fill(e.source);
      await fieldNamed(page, 'Evidence original content').fill(e.content);
      await choose(page, /evidence category/i, /^(document)$/i);
      await page.getByRole('button', { name: /record evidence|add evidence/i }).first().click();
    }
    for (const e of [e1, e2]) await expect(page.getByText(e.label).first()).toBeVisible();
  });
  await assertion('J2', 'J2-04', async () => {
    await linkWith(page, e1.label, h1, 'supports', r1, 'Link evidence to a hypothesis');
    await linkWith(page, e2.label, h1, 'contradicts', r2, 'Link evidence to a hypothesis');
    await expect(page.getByText(r1).first()).toBeVisible();
    await expect(page.getByText(r2).first()).toBeVisible();
  });
  await assertion('J2', 'J2-05', async () => {
    const evidenceCard = page.getByRole('article').filter({ hasText: e1.label });
    await expect(evidenceCard.getByText(h1).first()).toBeVisible();
    await expect(evidenceCard.getByText(/supports/).first()).toBeVisible();
    await section(page, 'Hypotheses');
    const hypothesisCard = page.getByRole('article').filter({ hasText: h1 });
    await expect(hypothesisCard.getByText(e1.label).first()).toBeVisible();
    await expect(hypothesisCard.getByText(e2.label).first()).toBeVisible();
    await expect(hypothesisCard.getByText(/contradicts/).first()).toBeVisible();
  });
  await assertion('J2', 'J2-06', async () => {
    await section(page, 'Evidence');
    await linkWith(page, e1.label, h1, 'inconclusive', r3, 'Reassess');
    await expect(page.getByRole('article').filter({ hasText: e1.label }).getByText(/inconclusive/).first()).toBeVisible();
    await section(page, 'Timeline');
    await expect(page.getByText(/supports/).first()).toBeVisible();
    await expect(page.getByText(/inconclusive/).first()).toBeVisible();
  });
  await assertion('J2', 'J2-07', async () => {
    await section(page, 'Overview');
    await fieldNamed(page, 'Conclusion statement').fill(conclusion);
    await choose(page, /conclusion strength/i, /^(plausible ?explanation)$/i);
    await button(page, 'Record a conclusion').click();
    await expect(page.getByText(conclusion).first()).toBeVisible();
  });
  await assertion('J2', 'J2-08', async () => {
    const expectations = { Overview: v.problem, 'Causal Analysis': r2, Evidence: e2.content, Hypotheses: h2, Timeline: obs.text };
    for (const [name, text] of Object.entries(expectations)) {
      await section(page, name);
      await expect(page.getByRole('navigation', { name: /sections/i }).getByRole('link', { name, exact: true })).toHaveAttribute('aria-current', /page|true|location/);
      await expect(page.getByText(text).first()).toBeVisible();
    }
  });
  await assertion('J2', 'J2-09', async () => {
    await saveAndConfirm(page);
    await providerHolds([obs.text, obs.source, h1, h2, e1.label, e1.content, e2.label, r1, r2, r3, conclusion]);
    const fresh = await (await contextFor(browser)).newPage();
    await connect(fresh);
    await fresh.getByRole('link', { name: v.title }).click();
    await section(fresh, 'Evidence');
    for (const t of [e1.label, e2.content, r2, r3]) await expect(fresh.getByText(t).first()).toBeVisible({ timeout: 20000 });
    await section(fresh, 'Timeline');
    await expect(fresh.getByText(/supports/).first()).toBeVisible();
  });
  await assertion('J2', 'J2-10', async () => {
    await page.getByRole('navigation', { name: /sections/i }).getByRole('link', { name: /report/i }).click();
    await expect(page.getByRole('article', { name: /report/i }).or(page.getByRole('region', { name: /report/i })).first()).toContainText(v.title);
    await page.emulateMedia({ media: 'print' });
    await expect(page.getByText(v.title).first()).toBeVisible();
    await page.emulateMedia({ media: 'screen' });
  });
  await ctx.close();
  finish();
});
