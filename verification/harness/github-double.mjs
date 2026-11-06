// Harness-only GitHub double (a subset of the REST and GraphQL APIs) used to prove the governed journeys against
// synthetic fixture apps in an environment without a GitHub token. It is NEVER real-provider evidence: the journeys
// record its base URL, and the slice and completion gates refuse any live result whose provider is not
// https://api.github.com (LIVE_PROVIDER_SUBSTITUTED).
//
//   node verification/harness/github-double.mjs PORT
//
// Each repository is a small git model: commits (parents, a tree of path -> blob oid, a message), refs and blobs (oid =
// git's blob hash). Any branch the double is asked about exists, at the repository's root commit until written.
//
// REST (the persistence contract's reads and the Contents API the positive-control fixture writes with):
//   GET  /repos/{o}/{r}                       GET /repos/{o}/{r}/branches/{b}
//   GET  /repos/{o}/{r}/git/ref/heads/{b}     (also git/refs/heads/{b})
//   GET  /repos/{o}/{r}/git/trees/{branch|commit|tree}[?recursive=1]
//   GET  /repos/{o}/{r}/git/blobs/{oid}       GET /repos/{o}/{r}/commits/{branch|oid}
//   GET  /repos/{o}/{r}/compare/{base}...{head}
//   GET  /repos/{o}/{r}/contents/{path}[?ref=branch|oid]       PUT /repos/{o}/{r}/contents/{path} (sha guarded, 409)
// GraphQL (POST /graphql): mutation createCommitOnBranch(input: {branch, expectedHeadOid, message, fileChanges}) and the
// investigation-files read: repository(owner, name) { ref(qualifiedName) { target { ... on Commit { oid
// file(path) { object { ... on Blob { text } } } } } } object(oid | expression) defaultBranchRef }. A stale
// expectedHeadOid answers HTTP 200 with a STALE_DATA error ("Expected branch to point to ..."), the error matrix's
// GH-STALE-HEAD form; a missing path to delete or undecodable contents answer UNPROCESSABLE (GH-VALIDATION).
//
// POST /__control {"conflictNext": path}   -> before the next write another writer commits to the branch (rewriting
//                                            that path when it exists), so a sha- or head-guarded write is stale
// POST /__control {"failNext": status}     -> the next write (PUT or createCommitOnBranch) fails with that HTTP status
// POST /__control {"graphqlErrorNext": T}  -> the next createCommitOnBranch answers 200 with a GraphQL error of type T
// POST /__control {"reset": true}
import http from 'node:http';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const sha1 = (s) => crypto.createHash('sha1').update(s).digest('hex');
const blobOid = (bytes) => sha1(Buffer.concat([Buffer.from(`blob ${bytes.length}\0`), bytes]));

export const createDouble = () => {
  let repos = new Map(); // `${owner}/${repo}` -> { refs: Map(branch -> oid), commits: Map(oid -> commit) }
  let blobs = new Map(); // oid -> Buffer
  let trees = new Map(); // tree oid -> Map(path -> blob oid)
  let control = {};
  let clock = Date.parse('2026-01-01T00:00:00Z');

  const putBlob = (bytes) => { const oid = blobOid(bytes); blobs.set(oid, bytes); return oid; };
  const treeOid = (tree) => {
    const oid = sha1('tree\0' + [...tree.entries()].sort(([a], [b]) => (a < b ? -1 : 1)).map(([p, o]) => `${p}\0${o}`).join('\n'));
    trees.set(oid, tree);
    return oid;
  };
  const commit = (r, parents, tree, message) => {
    clock += 1000;
    const tOid = treeOid(tree);
    const oid = sha1(`commit\0${tOid}\0${parents.join(',')}\0${message}\0${clock}`);
    const c = { oid, parents, tree, treeOid: tOid, message, date: new Date(clock).toISOString() };
    r.commits.set(oid, c);
    return c;
  };
  const repoOf = (owner, name) => {
    const key = `${owner}/${name}`;
    if (!repos.has(key)) {
      const r = { owner, name, refs: new Map(), commits: new Map(), root: null };
      r.root = commit(r, [], new Map(), 'Initial commit').oid;
      repos.set(key, r);
    }
    return repos.get(key);
  };
  const head = (r, branch) => { if (!r.refs.has(branch)) r.refs.set(branch, r.root); return r.refs.get(branch); };
  const isOid = (s) => /^[0-9a-f]{40}$/.test(s);
  // A revision: a commit oid this repository holds, or a branch name (which then exists).
  const resolve = (r, rev) => (isOid(rev) ? (r.commits.has(rev) ? rev : null) : head(r, rev.replace(/^refs\/heads\//, '')));
  const ancestors = (r, oid) => { const seen = new Set(); const stack = [oid]; while (stack.length) { const o = stack.pop(); if (!seen.has(o)) { seen.add(o); stack.push(...r.commits.get(o).parents); } } return seen; };
  // Another writer: a commit on the branch that rewrites `path` (when it exists) or touches a harness file.
  const remoteWriter = (r, branch, path) => {
    const parent = r.commits.get(head(r, branch));
    const tree = new Map(parent.tree);
    const target = tree.has(path) ? path : '.harness/remote-writer';
    const before = tree.has(target) ? blobs.get(tree.get(target)) : Buffer.alloc(0);
    tree.set(target, putBlob(Buffer.concat([before, Buffer.from('\n')])));
    r.refs.set(branch, commit(r, [parent.oid], tree, 'Another writer').oid);
  };
  // One commit of additions and deletions on a branch, guarded by the expected head the caller read.
  const applyCommit = (r, branch, expectedHeadOid, message, additions, deletions) => {
    const current = head(r, branch);
    if (expectedHeadOid !== current) return { stale: current };
    const tree = new Map(r.commits.get(current).tree);
    for (const d of deletions) { if (!tree.has(d)) return { invalid: `A path was requested for deletion which does not exist as of commit oid \`${current}\`` }; tree.delete(d); }
    for (const a of additions) tree.set(a.path, putBlob(a.bytes));
    const c = commit(r, [current], tree, message);
    r.refs.set(branch, c.oid);
    return { commit: c };
  };

  // ---- REST --------------------------------------------------------------------------------------------------------
  const commitJson = (r, c) => {
    const parent = c.parents.length ? r.commits.get(c.parents[0]).tree : new Map();
    const files = [...new Set([...parent.keys(), ...c.tree.keys()])].filter((p) => parent.get(p) !== c.tree.get(p)).sort()
      .map((p) => ({ filename: p, sha: c.tree.get(p) || null, status: !parent.has(p) ? 'added' : !c.tree.has(p) ? 'removed' : 'modified' }));
    return { sha: c.oid, commit: { message: c.message, tree: { sha: c.treeOid }, committer: { date: c.date } }, parents: c.parents.map((p) => ({ sha: p })), files };
  };
  const rest = async (method, url, body) => {
    let m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)(?:\/(.*))?$/);
    if (!m) return [404, { message: 'Not Found' }];
    const r = repoOf(m[1], m[2]);
    const sub = m[3] || '';
    if (sub === '' && method === 'GET') return [200, { full_name: `${m[1]}/${m[2]}`, default_branch: 'main', permissions: { pull: true, push: true } }];
    if ((m = sub.match(/^branches\/(.+)$/)) && method === 'GET') return [200, { name: m[1], commit: { sha: head(r, m[1]) } }];
    if ((m = sub.match(/^git\/refs?\/heads\/(.+)$/)) && method === 'GET') return [200, { ref: `refs/heads/${m[1]}`, object: { sha: head(r, m[1]), type: 'commit' } }];
    if ((m = sub.match(/^git\/trees\/([^/]+)$/)) && method === 'GET') {
      const tree = trees.get(m[1]) || r.commits.get(resolve(r, m[1]))?.tree;
      if (!tree) return [404, { message: 'Not Found' }];
      return [200, { sha: treeOid(tree), tree: [...tree.entries()].sort(([a], [b]) => (a < b ? -1 : 1)).map(([path, sha]) => ({ path, mode: '100644', type: 'blob', sha, size: blobs.get(sha).length })), truncated: false }];
    }
    if ((m = sub.match(/^git\/blobs\/([^/]+)$/)) && method === 'GET') {
      const b = blobs.get(m[1]);
      return b ? [200, { sha: m[1], size: b.length, content: b.toString('base64'), encoding: 'base64' }] : [404, { message: 'Not Found' }];
    }
    if ((m = sub.match(/^commits\/([^/]+)$/)) && method === 'GET') {
      const oid = resolve(r, decodeURIComponent(m[1]));
      return oid ? [200, commitJson(r, r.commits.get(oid))] : [422, { message: `No commit found for SHA: ${m[1]}` }];
    }
    if ((m = sub.match(/^compare\/(.+?)\.\.\.(.+)$/)) && method === 'GET') {
      const base = resolve(r, decodeURIComponent(m[1])), headOid = resolve(r, decodeURIComponent(m[2]));
      if (!base || !headOid) return [404, { message: 'Not Found' }];
      const ofHead = ancestors(r, headOid), ofBase = ancestors(r, base);
      const ahead = [...ofHead].filter((o) => !ofBase.has(o)), behind = [...ofBase].filter((o) => !ofHead.has(o));
      const status = !ahead.length && !behind.length ? 'identical' : !behind.length ? 'ahead' : !ahead.length ? 'behind' : 'diverged';
      const mergeBase = [...ofHead].filter((o) => ofBase.has(o)).sort((a, b) => (r.commits.get(b).date < r.commits.get(a).date ? -1 : 1))[0];
      const commits = ahead.map((o) => r.commits.get(o)).sort((a, b) => (a.date < b.date ? -1 : 1)).map((c) => commitJson(r, c));
      return [200, { status, ahead_by: ahead.length, behind_by: behind.length, total_commits: ahead.length, merge_base_commit: { sha: mergeBase }, base_commit: { sha: base }, commits }];
    }
    if (!(m = sub.match(/^contents\/(.*)$/))) return [404, { message: 'Not Found' }];
    const path = decodeURIComponent(m[1]);
    if (method === 'GET') {
      const at = resolve(r, url.searchParams.get('ref') || 'main');
      if (!at) return [404, { message: 'No commit found for the ref' }];
      const tree = r.commits.get(at).tree;
      if (tree.has(path)) return [200, { type: 'file', path, name: path.split('/').pop(), sha: tree.get(path), size: blobs.get(tree.get(path)).length, content: blobs.get(tree.get(path)).toString('base64'), encoding: 'base64' }];
      const dir = [...new Set([...tree.keys()].filter((k) => k.startsWith(path + '/')).map((k) => k.slice(path.length + 1).split('/')[0]))]
        .map((name) => { const p = `${path}/${name}`; return tree.has(p) ? { name, path: p, sha: tree.get(p), type: 'file' } : { name, path: p, type: 'dir' }; });
      return dir.length ? [200, dir] : [404, { message: 'Not Found' }];
    }
    if (method === 'PUT') {
      const branch = body.branch || 'main';
      if (control.failNext) { const s = control.failNext; control.failNext = undefined; return [s, { message: 'injected failure' }]; }
      if (control.conflictNext !== undefined) { const p = control.conflictNext; control.conflictNext = undefined; remoteWriter(r, branch, p); }
      const current = head(r, branch);
      const existing = r.commits.get(current).tree.get(path);
      if ((existing && body.sha !== existing) || (!existing && body.sha)) return [409, { message: `${path} does not match ${body.sha || 'nothing'}` }];
      const res = applyCommit(r, branch, current, body.message || `Update ${path}`, [{ path, bytes: Buffer.from(body.content || '', 'base64') }], []);
      return [existing ? 200 : 201, { content: { path, sha: res.commit.tree.get(path) }, commit: { sha: res.commit.oid } }];
    }
    return [405, { message: 'method' }];
  };

  // ---- GraphQL: a small executor over the subset of GitHub's schema the persistence contract uses -----------------
  const tokenize = (src) => {
    const out = []; let i = 0;
    while (i < src.length) {
      const c = src[i];
      if (/[\s,﻿]/.test(c)) { i++; continue; }
      if (c === '#') { while (i < src.length && src[i] !== '\n') i++; continue; }
      if (src.startsWith('...', i)) { out.push({ k: 'p', v: '...' }); i += 3; continue; }
      if ('!$()[]{}:=@|&'.includes(c)) { out.push({ k: 'p', v: c }); i++; continue; }
      if (src.startsWith('"""', i)) { const e = src.indexOf('"""', i + 3); out.push({ k: 's', v: src.slice(i + 3, e) }); i = e + 3; continue; }
      if (c === '"') {
        let j = i + 1, s = '';
        while (src[j] !== '"') { if (src[j] === '\\') { const n = src[j + 1]; s += n === 'u' ? String.fromCharCode(parseInt(src.slice(j + 2, j + 6), 16)) : ({ n: '\n', t: '\t', r: '\r', b: '\b', f: '\f' }[n] ?? n); j += n === 'u' ? 6 : 2; } else s += src[j++]; }
        out.push({ k: 's', v: s }); i = j + 1; continue;
      }
      const num = src.slice(i).match(/^-?\d+(\.\d+)?([eE][+-]?\d+)?/);
      if (num) { out.push({ k: 'n', v: Number(num[0]) }); i += num[0].length; continue; }
      const name = src.slice(i).match(/^[_A-Za-z][_0-9A-Za-z]*/);
      if (name) { out.push({ k: 'name', v: name[0] }); i += name[0].length; continue; }
      throw new Error(`Syntax Error: unexpected character "${c}"`);
    }
    return out;
  };
  const parse = (src) => {
    const t = tokenize(src); let i = 0;
    const peek = (v) => t[i] && t[i].v === v;
    const take = (v) => { if (v !== undefined && !peek(v)) throw new Error(`Syntax Error: expected "${v}", found "${t[i] ? t[i].v : '<EOF>'}"`); return t[i++]; };
    const value = () => {
      const x = t[i];
      if (!x) throw new Error('Syntax Error: unexpected <EOF>');
      if (x.v === '$' && x.k === 'p') { i++; return { var: take().v }; }
      if (x.v === '[' && x.k === 'p') { i++; const l = []; while (!peek(']')) l.push(value()); i++; return { list: l }; }
      if (x.v === '{' && x.k === 'p') { i++; const o = {}; while (!peek('}')) { const k = take().v; take(':'); o[k] = value(); } i++; return { obj: o }; }
      i++;
      if (x.k === 's' || x.k === 'n') return { lit: x.v };
      if (x.v === 'true' || x.v === 'false') return { lit: x.v === 'true' };
      if (x.v === 'null') return { lit: null };
      return { lit: x.v }; // enum
    };
    const args = () => { const a = {}; if (peek('(')) { i++; while (!peek(')')) { const k = take().v; take(':'); a[k] = value(); } i++; } return a; };
    const directives = () => { while (peek('@')) { i++; take(); args(); } };
    const selections = () => {
      take('{'); const s = [];
      while (!peek('}')) {
        if (peek('...')) {
          i++;
          if (peek('on')) { i++; const on = take().v; directives(); s.push({ inline: on, sel: selections() }); }
          else if (peek('{') || peek('@')) { directives(); s.push({ inline: null, sel: selections() }); }
          else { s.push({ spread: take().v }); directives(); }
          continue;
        }
        let name = take().v, alias = name;
        if (peek(':')) { i++; name = take().v; }
        const a = args(); directives();
        s.push({ alias, name, args: a, sel: peek('{') ? selections() : null });
      }
      i++; return s;
    };
    const ops = [], fragments = {};
    while (i < t.length) {
      if (peek('{')) { ops.push({ kind: 'query', sel: selections() }); continue; }
      const kw = take().v;
      if (kw === 'fragment') { const name = take().v; take('on'); const on = take().v; directives(); fragments[name] = { on, sel: selections() }; continue; }
      const op = { kind: kw, name: null };
      if (t[i] && t[i].k === 'name') op.name = take().v;
      if (peek('(')) { let depth = 0; do { if (peek('(')) depth++; if (peek(')')) depth--; i++; } while (depth > 0); }
      directives();
      op.sel = selections();
      ops.push(op);
    }
    return { ops, fragments };
  };
  const evalValue = (v, vars) => (v.var !== undefined ? vars[v.var] : v.list ? v.list.map((x) => evalValue(x, vars)) : v.obj ? Object.fromEntries(Object.entries(v.obj).map(([k, x]) => [k, evalValue(x, vars)])) : v.lit);

  class GqlError extends Error { constructor(message, type) { super(message); this.type = type; } }
  const commitNode = (r, oid) => ({ __typename: 'Commit', r, c: r.commits.get(oid) });
  const blobNode = (oid) => ({ __typename: 'Blob', oid });
  const treeNode = (r, tree) => ({ __typename: 'Tree', r, tree });
  const refNode = (r, branch) => ({ __typename: 'Ref', r, branch });
  const entryNode = (path, oid) => ({ __typename: 'TreeEntry', path, oid });
  const resolvers = {
    Query: {
      repository: (_, a) => ({ __typename: 'Repository', r: repoOf(a.owner, a.name) }),
      viewer: () => ({ __typename: 'User', login: 'harness' }),
      rateLimit: () => ({ __typename: 'RateLimit', limit: 5000, remaining: 4999, cost: 1, resetAt: new Date(Date.now() + 3600e3).toISOString() }),
    },
    Mutation: {
      createCommitOnBranch: (_, a) => {
        const input = a.input || {};
        const branch = input.branch || {};
        const [owner, name] = String(branch.repositoryNameWithOwner || '').split('/');
        if (!owner || !name || !branch.branchName) throw new GqlError('Could not resolve to a Ref with the name given.', 'NOT_FOUND');
        if (!input.expectedHeadOid) throw new GqlError('Argument \'expectedHeadOid\' on InputObject \'CreateCommitOnBranchInput\' is required.', 'UNPROCESSABLE');
        const r = repoOf(owner, name);
        if (control.graphqlErrorNext) { const type = control.graphqlErrorNext; control.graphqlErrorNext = undefined; throw new GqlError('injected failure', type); }
        if (control.conflictNext !== undefined) { const p = control.conflictNext; control.conflictNext = undefined; remoteWriter(r, branch.branchName, p); }
        const changes = input.fileChanges || {};
        const additions = [];
        for (const x of changes.additions || []) {
          if (!/^[A-Za-z0-9+/]*={0,2}$/.test(x.contents || '')) throw new GqlError(`Invalid base64 contents for ${x.path}`, 'UNPROCESSABLE');
          additions.push({ path: x.path, bytes: Buffer.from(x.contents || '', 'base64') });
        }
        const message = [input.message?.headline, input.message?.body].filter(Boolean).join('\n\n');
        const res = applyCommit(r, branch.branchName, input.expectedHeadOid, message, additions, (changes.deletions || []).map((d) => d.path));
        if (res.stale) throw new GqlError(`Expected branch to point to "${input.expectedHeadOid}" but it did not. Pull and try again.`, 'STALE_DATA');
        if (res.invalid) throw new GqlError(res.invalid, 'UNPROCESSABLE');
        return { __typename: 'CreateCommitOnBranchPayload', commit: commitNode(r, res.commit.oid), ref: refNode(r, branch.branchName), clientMutationId: input.clientMutationId ?? null };
      },
    },
    Repository: {
      id: (o) => Buffer.from(`Repository:${o.r.owner}/${o.r.name}`).toString('base64'),
      name: (o) => o.r.name,
      nameWithOwner: (o) => `${o.r.owner}/${o.r.name}`,
      owner: (o) => ({ __typename: 'User', login: o.r.owner }),
      viewerPermission: () => 'WRITE',
      defaultBranchRef: (o) => refNode(o.r, 'main'),
      ref: (o, a) => (String(a.qualifiedName).startsWith('refs/tags/') ? null : refNode(o.r, String(a.qualifiedName).replace(/^refs\/heads\//, ''))),
      object: (o, a) => {
        if (a.oid) return o.r.commits.has(a.oid) ? commitNode(o.r, a.oid) : blobs.has(a.oid) ? blobNode(a.oid) : null;
        const [rev, ...rest] = String(a.expression || '').split(':');
        const oid = resolve(o.r, rev || 'main');
        if (!oid) return null;
        if (!rest.length) return commitNode(o.r, oid);
        const path = rest.join(':');
        const tree = o.r.commits.get(oid).tree;
        return tree.has(path) ? blobNode(tree.get(path)) : null;
      },
    },
    Ref: {
      id: (o) => Buffer.from(`Ref:${o.r.owner}/${o.r.name}/${o.branch}`).toString('base64'),
      name: (o) => o.branch,
      prefix: () => 'refs/heads/',
      target: (o) => commitNode(o.r, head(o.r, o.branch)),
    },
    Commit: {
      id: (o) => Buffer.from(`Commit:${o.c.oid}`).toString('base64'),
      oid: (o) => o.c.oid,
      abbreviatedOid: (o) => o.c.oid.slice(0, 7),
      message: (o) => o.c.message,
      messageHeadline: (o) => o.c.message.split('\n')[0],
      committedDate: (o) => o.c.date,
      tree: (o) => treeNode(o.r, o.c.tree),
      parents: (o) => ({ __typename: 'CommitConnection', totalCount: o.c.parents.length, nodes: o.c.parents.map((p) => commitNode(o.r, p)) }),
      file: (o, a) => (o.c.tree.has(a.path) ? entryNode(a.path, o.c.tree.get(a.path)) : null),
    },
    Tree: {
      oid: (o) => treeOid(o.tree),
      entries: (o) => [...o.tree.entries()].map(([p, oid]) => entryNode(p, oid)),
    },
    TreeEntry: {
      name: (o) => o.path.split('/').pop(),
      path: (o) => o.path,
      oid: (o) => o.oid,
      type: () => 'blob',
      mode: () => 33188,
      size: (o) => blobs.get(o.oid).length,
      object: (o) => blobNode(o.oid),
    },
    Blob: {
      oid: (o) => o.oid,
      byteSize: (o) => blobs.get(o.oid).length,
      isBinary: (o) => blobs.get(o.oid).includes(0),
      isTruncated: () => false,
      text: (o) => (blobs.get(o.oid).includes(0) ? null : blobs.get(o.oid).toString('utf8')),
    },
    CommitConnection: { totalCount: (o) => o.totalCount, nodes: (o) => o.nodes },
    CreateCommitOnBranchPayload: { commit: (o) => o.commit, ref: (o) => o.ref, clientMutationId: (o) => o.clientMutationId },
    User: { login: (o) => o.login },
    RateLimit: { limit: (o) => o.limit, remaining: (o) => o.remaining, cost: (o) => o.cost, resetAt: (o) => o.resetAt },
  };
  const interfaces = { Node: ['Repository', 'Ref', 'Commit', 'Tree', 'Blob', 'User'], GitObject: ['Commit', 'Tree', 'Blob'], RepositoryOwner: ['User'], Actor: ['User'] };
  const matches = (on, typename) => on === null || on === typename || (interfaces[on] || []).includes(typename);
  const graphql = (body) => {
    let doc;
    try { doc = parse(String(body.query || '')); } catch (e) { return [200, { errors: [{ message: e.message }] }]; }
    const op = body.operationName ? doc.ops.find((o) => o.name === body.operationName) : doc.ops.length === 1 ? doc.ops[0] : null;
    if (!op) return [200, { errors: [{ message: 'An operation name is required when a document holds several operations' }] }];
    const vars = body.variables || {};
    const errors = [];
    const complete = (obj, sel, path) => {
      if (obj === null || obj === undefined) return null;
      if (Array.isArray(obj)) return obj.map((x, n) => complete(x, sel, [...path, n]));
      if (!sel) return obj;
      const out = {};
      const walk = (s) => {
        for (const f of s) {
          if (f.spread) { const fr = doc.fragments[f.spread]; if (!fr) throw new GqlError(`Fragment ${f.spread} was used, but not defined`); if (matches(fr.on, obj.__typename)) walk(fr.sel); continue; }
          if (f.inline !== undefined) { if (matches(f.inline, obj.__typename)) walk(f.sel); continue; }
          if (f.name === '__typename') { out[f.alias] = obj.__typename; continue; }
          const r = (resolvers[obj.__typename] || {})[f.name];
          if (!r) throw new GqlError(`Field '${f.name}' doesn't exist on type '${obj.__typename}'`, 'undefinedField');
          const args = Object.fromEntries(Object.entries(f.args).map(([k, v]) => [k, evalValue(v, vars)]));
          try { out[f.alias] = complete(r(obj, args), f.sel, [...path, f.alias]); }
          catch (e) { if (!(e instanceof GqlError) || e.type === 'undefinedField') throw e; errors.push({ type: e.type, path: [...path, f.alias], locations: [{ line: 1, column: 1 }], message: e.message }); out[f.alias] = null; }
        }
      };
      walk(sel);
      return out;
    };
    try {
      const data = complete({ __typename: op.kind === 'mutation' ? 'Mutation' : 'Query' }, op.sel, []);
      return [200, errors.length ? { data, errors } : { data }];
    } catch (e) {
      return [200, { errors: [{ ...(e.type === 'undefinedField' ? { extensions: { code: 'undefinedField' } } : {}), locations: [{ line: 1, column: 1 }], message: e.message }] }];
    }
  };

  const handle = async (method, url, body) => {
    if (url.pathname === '/__control') { if (body.reset) { repos = new Map(); blobs = new Map(); trees = new Map(); control = {}; } else control = { ...control, ...body }; return [200, control]; }
    if (url.pathname === '/graphql' && method === 'POST') {
      if (control.failNext && /\bmutation\b/.test(String(body.query || ''))) { const s = control.failNext; control.failNext = undefined; return [s, { message: 'injected failure' }]; }
      return graphql(body);
    }
    return rest(method, url, body);
  };
  return { handle };
};

// Answers carry the caching headers api.github.com sends on reads (Cache-Control: private, max-age=60, s-maxage=60;
// Vary: Accept, Authorization, ...), so an app whose read-back is served from the browser's HTTP cache fails here as it
// does against the real provider (live preflight run 42068316592: J3-05 failed on Chromium only).
export const listen = (port) => {
  const double = createDouble();
  const server = http.createServer(async (req, res) => {
    const send = (status, body) => {
      res.writeHead(status, { 'content-type': 'application/json', 'access-control-allow-origin': '*', 'access-control-allow-headers': '*', 'access-control-allow-methods': 'GET,PUT,POST,PATCH,DELETE,OPTIONS',
        ...(req.method === 'GET' && status === 200 ? { 'cache-control': 'private, max-age=60, s-maxage=60', vary: 'Accept, Authorization, Cookie, Accept-Encoding' } : {}) });
      res.end(body === undefined ? '' : JSON.stringify(body));
    };
    if (req.method === 'OPTIONS') return send(204);
    let text = '';
    for await (const chunk of req) text += chunk;
    let body = {};
    try { body = text ? JSON.parse(text) : {}; } catch { return send(400, { message: 'Problems parsing JSON' }); }
    const [status, answer] = await double.handle(req.method, new URL(req.url, 'http://double'), body);
    send(status, answer);
  });
  return new Promise((ok) => server.listen(port, '127.0.0.1', () => ok(server)));
};

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const server = await listen(Number(process.argv[2] || 4610));
  console.log(`github-double on ${server.address().port}`);
}
