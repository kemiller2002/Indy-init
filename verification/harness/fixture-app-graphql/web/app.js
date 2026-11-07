// Harness positive control, GraphQL form (not Causa): the fixture app of verification/harness/fixture-app with its save
// replaced by the persistence contract's mechanism: one GraphQL createCommitOnBranch guarded by expectedHeadOid (the
// head the investigation was read at), a read-back through object(expression: "<oid>:<path>"), STALE_DATA as Conflict
// and any other GraphQL error as Save failed. Served over fixture-app/web (index.html and the styles are the same) by
// verification/harness/graphql-fixture.test.mjs, which proves J3-06/J3-07 reach a GraphQL-saving application.
const API = 'https://api.github.com';
const enc = (s) => btoa(unescape(encodeURIComponent(s)));
const dec = (s) => decodeURIComponent(escape(atob(s.replace(/\n/g, ''))));
const uid = () => crypto.randomUUID();
const now = () => new Date().toISOString();
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

let state = { conn: null, list: [], inv: null, head: null, dirty: false, saveState: 'Saved', message: '', settings: { density: 'comfortable' } };
const set = (f) => { state = f(state); render(); };

// ---- provider (GitHub contents API; credentials in memory only) ------------------------------------------------------
const gh = async (path, init = {}) => {
  // cache: 'no-store': GitHub answers reads with Cache-Control: private, max-age=60, so without it Chromium serves the
  // read-back after a save from its HTTP cache and the confirmation compares against stale content (live preflight run
  // 42068316592, J3-05 on Chromium).
  const r = await fetch(`${API}${path}`, { ...init, cache: 'no-store', headers: { authorization: `Bearer ${state.conn.token}`, accept: 'application/vnd.github+json', 'content-type': 'application/json' } });
  return { status: r.status, body: r.status === 204 ? null : await r.json().catch(() => null) };
};
const repoPath = (p) => `/repos/${state.conn.owner}/${state.conn.repo}/contents/${p}`;
const fileOf = (id) => `causa/investigations/${id}.json`;
const getFile = (p) => gh(`${repoPath(p)}?ref=${encodeURIComponent(state.conn.branch)}`);
const gql = (query, variables) => gh('/graphql', { method: 'POST', body: JSON.stringify({ query, variables }) });
const branchHead = async () => (await gh(`/repos/${state.conn.owner}/${state.conn.repo}/git/ref/heads/${state.conn.branch}`)).body.object.sha;
const commitFile = (p, text, expectedHeadOid) => gql('mutation($input: CreateCommitOnBranchInput!) { createCommitOnBranch(input: $input) { commit { oid } } }',
  { input: { branch: { repositoryNameWithOwner: `${state.conn.owner}/${state.conn.repo}`, branchName: state.conn.branch }, expectedHeadOid, message: { headline: `Causa: save ${p}` }, fileChanges: { additions: [{ path: p, contents: enc(text) }] } } });
const readAt = (oid, p) => gql('query($o: String!, $n: String!, $e: String!) { repository(owner: $o, name: $n) { object(expression: $e) { ... on Blob { text } } } }', { o: state.conn.owner, n: state.conn.repo, e: `${oid}:${p}` });
const putFile = (p, text, sha) => gh(repoPath(p), { method: 'PUT', body: JSON.stringify({ message: `Causa: save ${p}`, content: enc(text), branch: state.conn.branch, ...(sha ? { sha } : {}) }) });

// ---- pure investigation updates -----------------------------------------------------------------------------------
const stamp = (inv, what) => ({ ...inv, history: [...inv.history, { at: now(), what }] });
const newInvestigation = (f) => ({ id: f.id, createdAt: now(), title: f.title, problem: f.problem, question: f.question, state: 'Open', observations: [], hypotheses: [], evidence: [], links: [], conclusions: [], history: [{ at: now(), what: 'Investigation started' }] });
const addObservation = (inv, f) => stamp({ ...inv, observations: [...inv.observations, { id: uid(), text: f.obsText, source: f.obsSource }] }, `Observation recorded: ${f.obsText}`);
const addHypothesis = (inv, f) => stamp({ ...inv, hypotheses: [...inv.hypotheses, { id: uid(), statement: f.hypStatement, assessment: 'Proposed' }] }, `Hypothesis recorded: ${f.hypStatement}`);
const addEvidence = (inv, f) => stamp({ ...inv, evidence: [...inv.evidence, { id: uid(), label: f.evLabel, source: f.evSource, url: f.evUrl, content: f.evContent }] }, `Evidence recorded: ${f.evLabel}`);
const pairOf = (inv, e, h) => inv.links.find((l) => l.evidence === e && l.hypothesis === h);
const link = (inv, f) => pairOf(inv, f.linkEvidence, f.linkHypothesis)
  ? { error: 'This evidence is already assessed against this hypothesis; use Reassess.' }
  : { inv: stamp({ ...inv, links: [...inv.links, { id: uid(), evidence: f.linkEvidence, hypothesis: f.linkHypothesis, kind: f.kind, rationale: f.rationale, history: [] }] }, `Linked: ${f.kind}`) };
const reassess = (inv, f) => {
  const l = pairOf(inv, f.linkEvidence, f.linkHypothesis);
  if (!l) return { error: 'There is no assessment to reassess for this pair.' };
  const links = inv.links.map((x) => (x === l ? { ...x, kind: f.kind, rationale: f.rationale, history: [...x.history, { kind: x.kind, rationale: x.rationale, at: now() }] } : x));
  return { inv: stamp({ ...inv, links }, `Reassessed: ${l.kind} -> ${f.kind}`) };
};
const conclude = (inv, f) => stamp({ ...inv, conclusions: [...inv.conclusions, { id: uid(), statement: f.conclusion, at: now() }] }, `Conclusion recorded: ${f.conclusion}`);

// ---- save (PERSIST-030/031 states driven by real outcomes) ----------------------------------------------------------
const save = async () => {
  if (!navigator.onLine) return set((s) => ({ ...s, saveState: 'Offline' }));
  set((s) => ({ ...s, saveState: 'Saving' }));
  const text = JSON.stringify(state.inv, null, 2);
  let r;
  try { r = await commitFile(fileOf(state.inv.id), text, state.head || await branchHead()); } catch { return set((s) => ({ ...s, saveState: navigator.onLine ? 'Unknown commit outcome' : 'Offline' })); }
  const err = r.body && r.body.errors && r.body.errors[0];
  if (r.status === 200 && !err) {
    const oid = r.body.data.createCommitOnBranch.commit.oid;
    const back = await readAt(oid, fileOf(state.inv.id));
    const confirmed = back.status === 200 && back.body.data.repository.object && back.body.data.repository.object.text === text;
    return set((s) => ({ ...s, head: confirmed ? oid : s.head, dirty: !confirmed, saveState: confirmed ? 'Saved' : 'Save failed', message: confirmed ? '' : 'The saved content did not read back.' }));
  }
  if (err && err.type === 'STALE_DATA') return set((s) => ({ ...s, saveState: 'Conflict', message: 'Another writer changed this investigation on GitHub. Nothing of this tab\'s change was written; your changes are kept.' }));
  return set((s) => ({ ...s, saveState: 'Save failed', message: `GitHub refused the save (${r.status}). Your changes are kept in this tab.` }));
};
const edit = (f) => set((s) => ({ ...s, inv: f(s.inv), dirty: true, saveState: navigator.onLine ? 'Unsaved' : 'Offline', message: '' }));

// ---- loading the list and an investigation ----------------------------------------------------------------------
const loadList = async () => {
  const r = await getFile('causa/investigations');
  const items = r.status === 200 && Array.isArray(r.body) ? r.body.filter((x) => x.name.endsWith('.json')) : [];
  const docs = await Promise.all(items.map(async (x) => { const f = await getFile(x.path); return JSON.parse(dec(f.body.content)); }));
  set((s) => ({ ...s, list: docs.map((d) => ({ id: d.id, title: d.title || d.problem })) }));
};
const open = async (id) => {
  if (state.inv && state.inv.id === id) return;
  const r = await getFile(fileOf(id));
  if (r.status !== 200) return set((s) => ({ ...s, inv: null, message: 'This investigation could not be loaded from GitHub.' }));
  const at = await branchHead();
  set((s) => ({ ...s, inv: JSON.parse(dec(r.body.content)), head: at, dirty: false, saveState: 'Saved' }));
};

// ---- views ------------------------------------------------------------------------------------------------------
const lozenge = (word) => `<span class="ef-status-lozenge ${word === 'Conflict' || word === 'Save failed' ? 'causa-conflict' : word === 'Saved' ? '' : 'causa-attention'}" role="status" aria-live="polite" aria-label="Save state">${esc(word)}</span>`;
const field = (id, label, kind = 'input', extra = '') => kind === 'textarea'
  ? `<label for="${id}">${label}</label><textarea id="${id}" name="${id}" ${extra}></textarea>`
  : `<label for="${id}">${label}</label><input id="${id}" name="${id}" ${extra}>`;
const options = (xs, f) => xs.map((x) => `<option value="${x.id}">${esc(f(x))}</option>`).join('');
const sectionNames = [['overview', 'Overview'], ['analysis', 'Causal Analysis'], ['evidence', 'Evidence'], ['hypotheses', 'Hypotheses'], ['timeline', 'Timeline'], ['report', 'Report']];
const byId = (xs, id) => xs.find((x) => x.id === id) || {};

const views = {
  connect: () => state.conn ? '' : `<section class="causa-card" aria-labelledby="c"><h1 id="c">No repository connected</h1>
    <p>Connect a data repository to see its investigations.</p>
    <form id="connect">${field('owner', 'Repository owner')}${field('repo', 'Repository')}${field('branch', 'Branch')}${field('token', 'GitHub token', 'input', 'type="password" autocomplete="off"')}
    <button type="submit">Connect data repository</button></form></section>`,
  list: () => `<h1>Investigations</h1><p><a href="#/investigations/new">New investigation</a></p>
    <ul class="causa-list" aria-label="Investigations">${state.list.map((x) => `<li><a href="#/investigations/${x.id}">${esc(x.title)}</a></li>`).join('') || '<li>No investigations yet.</li>'}</ul>`,
  start: () => `<h1>Start an investigation</h1><p>Persistence destination: ${esc(state.conn.owner)}/${esc(state.conn.repo)} (${esc(state.conn.branch)})</p>
    <form id="start">${field('problem', 'Problem statement', 'textarea', 'required')}${field('title', 'Title')}${field('question', 'Primary investigative question', 'textarea')}${field('invId', 'Investigation identifier', 'input', `value="${uid()}"`)}
    <button type="submit">Start investigation</button> <button type="button" id="cancel">Cancel investigation start</button></form>`,
  workspace: (section) => {
    const inv = state.inv;
    if (!inv) return `<p role="alert">${esc(state.message || 'Loading the investigation…')}</p>`;
    const linksOf = (pred, f) => inv.links.filter(pred).map(f).join('') || '<li>No assessed links.</li>';
    const nav = `<nav class="causa-sections" aria-label="Investigation sections">${sectionNames.map(([k, n]) => `<a href="#/investigations/${inv.id}/${k}" ${k === section ? 'aria-current="page"' : ''}>${n}</a>`).join('')}</nav>`;
    const header = `<header class="causa-card"><h1>${esc(inv.title || inv.problem)}</h1><p>Save state: ${lozenge(state.saveState)}</p>${state.message ? `<p role="alert">${esc(state.message)}</p>` : ''}
      <button type="button" id="save" ${state.saveState === 'Saving' ? 'disabled' : ''}>Save</button>${state.saveState === 'Conflict' ? ' <button type="button" id="reload">Discard and reload</button>' : ''}</header>`;
    const body = {
      overview: `<section aria-label="Original problem statement"><h2>Original problem statement</h2><p>${esc(inv.problem)}</p></section>
        <p>Creation timestamp: <time>${esc(inv.createdAt)}</time></p><p>Investigation state: ${esc(inv.state)}</p><p>Primary investigative question: ${esc(inv.question)}</p>
        <form id="obs">${field('obsText', 'Observation text', 'textarea')}${field('obsSource', 'Observation source reference')}<button type="submit">Record an observation</button></form>
        <ul aria-label="Observations">${inv.observations.map((o) => `<li>${esc(o.text)} <small>(${esc(o.source)})</small></li>`).join('')}</ul>
        <form id="conc">${field('conclusion', 'Conclusion statement', 'textarea')}<button type="submit">Record a conclusion</button></form>
        <ul aria-label="Conclusions">${inv.conclusions.map((c) => `<li>${esc(c.statement)}</li>`).join('')}</ul>`,
      hypotheses: `<form id="hyp">${field('hypStatement', 'Hypothesis statement', 'textarea')}<button type="submit">Record a hypothesis</button></form>
        <section aria-label="Hypotheses">${inv.hypotheses.map((h) => `<article class="causa-card"><h3>${esc(h.statement)}</h3><p>Evidence:</p><ul>${linksOf((l) => l.hypothesis === h.id, (l) => `<li class="${l.kind === 'contradicts' ? 'causa-contradicts' : ''}">${esc(byId(inv.evidence, l.evidence).label)}: ${esc(l.kind)} — ${esc(l.rationale)}</li>`)}</ul></article>`).join('')}</section>`,
      evidence: `<form id="ev">${field('evLabel', 'Evidence label')}${field('evSource', 'Evidence source reference')}${field('evUrl', 'Evidence source URL', 'input', 'type="url"')}${field('evContent', 'Evidence original content', 'textarea')}<button type="submit">Record evidence</button></form>
        <section aria-label="Evidence">${inv.evidence.map((e) => `<article class="causa-card"><h3>${esc(e.label)}</h3><p>${esc(e.content)}</p><p>Hypotheses:</p><ul>${linksOf((l) => l.evidence === e.id, (l) => `<li class="${l.kind === 'contradicts' ? 'causa-contradicts' : ''}">${esc(byId(inv.hypotheses, l.hypothesis).statement)}: ${esc(l.kind)} — ${esc(l.rationale)}</li>`)}</ul></article>`).join('')}</section>
        <form id="link"><label for="linkEvidence">Evidence item</label><select id="linkEvidence" name="linkEvidence">${options(inv.evidence, (e) => e.label)}</select>
        <label for="linkHypothesis">Hypothesis to assess</label><select id="linkHypothesis" name="linkHypothesis">${options(inv.hypotheses, (h) => h.statement)}</select>
        <fieldset><legend>Evidence relationship kind</legend>${['supports', 'contradicts', 'consistent-with', 'inconclusive'].map((k) => `<label><input type="radio" name="kind" value="${k}"> ${k}</label>`).join('')}</fieldset>
        ${field('rationale', 'Evidence relationship rationale', 'textarea')}
        <button type="submit" name="op" value="link">Link evidence to a hypothesis</button> <button type="submit" name="op" value="reassess">Reassess</button></form>`,
      analysis: `<h2>Causal Analysis</h2><ul aria-label="Relationships">${inv.links.map((l) => `<li>${esc(byId(inv.evidence, l.evidence).label)} ${esc(l.kind)} ${esc(byId(inv.hypotheses, l.hypothesis).statement)} — ${esc(l.rationale)}</li>`).join('') || '<li>No relationships yet.</li>'}</ul>`,
      timeline: `<h2>Timeline</h2><ol aria-label="History">${inv.history.map((h) => `<li><time>${esc(h.at)}</time> ${esc(h.what)}</li>`).join('')}</ol>`,
      report: `<h2>Report</h2><article aria-label="Report"><h3>${esc(inv.title)}</h3><p>${esc(inv.problem)}</p><p>${inv.hypotheses.length} hypotheses, ${inv.evidence.length} evidence items, ${inv.links.length} assessed links.</p></article><button type="button" onclick="window.print()">Print report</button>`,
    }[section] || '';
    return header + nav + body;
  },
  administration: () => `<h1>Administration</h1><dl><dt>Provider</dt><dd>GitHub</dd><dt>Repository</dt><dd>${esc(state.conn ? `${state.conn.owner}/${state.conn.repo}` : 'not connected')}</dd><dt>Branch</dt><dd>${esc(state.conn?.branch || '')}</dd><dt>Build</dt><dd>harness fixture</dd></dl>`,
  settings: () => `<h1>Settings</h1><form id="settings"><fieldset><legend>Density</legend>${['comfortable', 'compact'].map((d) => `<label><input type="radio" name="density" value="${d}" ${state.settings.density === d ? 'checked' : ''}> ${d}</label>`).join('')}</fieldset><button type="submit">Save settings</button></form>`,
};

const route = () => {
  const h = location.hash || '#/';
  const m = h.match(/^#\/investigations\/([0-9a-fA-F-]{36})(?:\/(\w+))?$/);
  if (h === '#/administration') return ['administration'];
  if (h === '#/settings') return ['settings'];
  if (!state.conn) return ['connect'];
  if (h === '#/investigations/new') return ['start'];
  if (m) return ['workspace', m[1], m[2] || 'overview'];
  return ['list'];
};

function render() {
  const [name, id, section] = route();
  const current = name === 'administration' || name === 'settings' ? name : 'investigations';
  document.querySelectorAll('nav.causa-primary a').forEach((a) => (a.dataset.dest === current ? a.setAttribute('aria-current', 'page') : a.removeAttribute('aria-current')));
  if (name === 'workspace' && (!state.inv || state.inv.id !== id)) open(id);
  document.getElementById('main').innerHTML = name === 'workspace' ? views.workspace(section) : views[name]();
}

const formData = (form) => Object.fromEntries(new FormData(form).entries());
document.addEventListener('submit', async (ev) => {
  ev.preventDefault();
  const form = ev.target, f = formData(form), op = ev.submitter?.value;
  if (form.id === 'connect') { state = { ...state, conn: { owner: f.owner, repo: f.repo, branch: f.branch || 'main', token: f.token } }; await loadList(); return; }
  if (form.id === 'start') {
    if (!f.problem.trim()) return set((s) => ({ ...s, message: 'The problem statement must not be empty.' }));
    state = { ...state, inv: newInvestigation({ ...f, id: f.invId }), head: null, dirty: true, saveState: 'Unsaved' };
    location.hash = `#/investigations/${f.invId}/overview`; return;
  }
  if (form.id === 'obs') return edit((inv) => addObservation(inv, f));
  if (form.id === 'hyp') return edit((inv) => addHypothesis(inv, f));
  if (form.id === 'ev') return edit((inv) => addEvidence(inv, f));
  if (form.id === 'conc') return edit((inv) => conclude(inv, f));
  if (form.id === 'link') {
    if (!f.kind || !f.rationale?.trim()) return set((s) => ({ ...s, message: 'Choose a relationship kind and give a rationale.' }));
    const r = op === 'reassess' ? reassess(state.inv, f) : link(state.inv, f);
    return r.error ? set((s) => ({ ...s, message: r.error })) : edit(() => r.inv);
  }
  if (form.id === 'settings') return set((s) => ({ ...s, settings: { density: f.density } }));
});
document.addEventListener('click', async (ev) => {
  if (ev.target.id === 'save') await save();
  if (ev.target.id === 'cancel') location.hash = '#/';
  if (ev.target.id === 'reload') { const id = state.inv.id; state = { ...state, inv: null }; await open(id); }
});
window.addEventListener('hashchange', async () => { if (route()[0] === 'list' && state.conn) await loadList(); render(); });
window.addEventListener('offline', () => set((s) => ({ ...s, saveState: s.dirty ? 'Offline' : s.saveState })));
window.addEventListener('online', () => set((s) => ({ ...s, saveState: s.dirty ? 'Unsaved' : s.saveState })));
document.getElementById('loading').remove();
render();
