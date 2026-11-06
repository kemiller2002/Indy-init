// Self-test of the harness GitHub double (node --test verification/harness/github-double.test.mjs). It checks the
// answer forms a conformant Causa adapter depends on: the REST reads of the persistence contract, the guarded GraphQL
// createCommitOnBranch with the error matrix's stale-head form (GH-STALE-HEAD, STALE_DATA), the investigation-files
// read through Commit.file(path:), and that the Contents API the positive-control fixture uses still behaves as before.
import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { listen } from './github-double.mjs';

const server = await listen(0);
const base = `http://127.0.0.1:${server.address().port}`;
test.after(() => server.close());
const call = async (method, path, body) => {
  const r = await fetch(base + path, { method, headers: { 'content-type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
  return { status: r.status, body: await r.json().catch(() => null), headers: r.headers };
};
const gql = (query, variables) => call('POST', '/graphql', { query, variables });
const b64 = (s) => Buffer.from(s).toString('base64');
const R = '/repos/o/r';
const COMMIT = `mutation Save($input: CreateCommitOnBranchInput!) { createCommitOnBranch(input: $input) { commit { oid url: oid } } }`;
const save = (expectedHeadOid, files, deletions = []) => gql(COMMIT, { input: {
  branch: { repositoryNameWithOwner: 'o/r', branchName: 'b' }, expectedHeadOid, message: { headline: 'Causa: save' },
  fileChanges: { additions: Object.entries(files).map(([path, text]) => ({ path, contents: b64(text) })), deletions: deletions.map((path) => ({ path })) } } });
const READ = `query Read($owner: String!, $name: String!, $ref: String!) {
  repository(owner: $owner, name: $name) {
    ref(qualifiedName: $ref) { target { __typename ... on Commit { oid
      manifest: file(path: "inv/manifest.json") { object { ... on Blob { text } } }
      events: file(path: "inv/events.ndjson") { object { ... on Blob { text } } }
      snapshot: file(path: "inv/snapshot.json") { oid object { ...BlobText } } } } }
  }
}
fragment BlobText on Blob { text byteSize }`;

test('the double answers the persistence contract: ref, guarded commit, file read, commit, compare, blob', async () => {
  await call('POST', '/__control', { reset: true });
  const ref = await call('GET', `${R}/git/ref/heads/b`);
  assert.equal(ref.status, 200);
  assert.equal(ref.body.ref, 'refs/heads/b');
  const h0 = ref.body.object.sha;
  assert.match(h0, /^[0-9a-f]{40}$/);

  const first = await save(h0, { 'inv/manifest.json': '{"v":1}', 'inv/events.ndjson': '{"e":1}\n', 'inv/snapshot.json': '{"s":1}' });
  assert.equal(first.status, 200);
  assert.equal(first.body.errors, undefined);
  const h1 = first.body.data.createCommitOnBranch.commit.oid;
  assert.notEqual(h1, h0);
  assert.equal((await call('GET', `${R}/git/ref/heads/b`)).body.object.sha, h1);

  // The investigation files are read in one query at the head, through Commit.file(path:).
  const read = await gql(READ, { owner: 'o', name: 'r', ref: 'refs/heads/b' });
  assert.equal(read.body.errors, undefined);
  const target = read.body.data.repository.ref.target;
  assert.equal(target.__typename, 'Commit');
  assert.equal(target.oid, h1);
  assert.equal(target.manifest.object.text, '{"v":1}');
  assert.equal(target.events.object.text, '{"e":1}\n');
  assert.equal(target.snapshot.object.text, '{"s":1}');
  assert.equal(target.snapshot.object.byteSize, 7);

  // A stale expectedHeadOid is refused with the error matrix's GH-STALE-HEAD form: HTTP 200, type STALE_DATA, no write.
  const stale = await save(h0, { 'inv/events.ndjson': '{"e":1}\n{"e":2}\n' });
  assert.equal(stale.status, 200);
  assert.equal(stale.body.data.createCommitOnBranch, null);
  assert.equal(stale.body.errors[0].type, 'STALE_DATA');
  assert.match(stale.body.errors[0].message, /Expected branch to point to/);
  assert.deepEqual(stale.body.errors[0].path, ['createCommitOnBranch']);
  assert.equal((await call('GET', `${R}/git/ref/heads/b`)).body.object.sha, h1);

  const second = await save(h1, { 'inv/events.ndjson': '{"e":1}\n{"e":2}\n' }, ['inv/snapshot.json']);
  const h2 = second.body.data.createCommitOnBranch.commit.oid;
  const commit = await call('GET', `${R}/commits/${h2}`);
  assert.equal(commit.status, 200);
  assert.deepEqual(commit.body.parents.map((p) => p.sha), [h1]);
  assert.deepEqual(commit.body.files.map((f) => [f.filename, f.status]), [['inv/events.ndjson', 'modified'], ['inv/snapshot.json', 'removed']]);
  assert.equal((await call('GET', `${R}/commits/${'0'.repeat(40)}`)).status, 422);

  const ahead = await call('GET', `${R}/compare/${h1}...${h2}`);
  assert.equal(ahead.body.status, 'ahead');
  assert.equal(ahead.body.ahead_by, 1);
  assert.deepEqual(ahead.body.commits.map((c) => c.sha), [h2]);
  assert.equal((await call('GET', `${R}/compare/${h2}...${h1}`)).body.status, 'behind');
  assert.equal((await call('GET', `${R}/compare/${h2}...b`)).body.status, 'identical');
  assert.equal((await call('GET', `${R}/compare/${'1'.repeat(40)}...b`)).status, 404);

  // Reads at a named commit (contents ?ref=oid, object(expression:)) see that commit, not the head.
  const old = await call('GET', `${R}/contents/inv/snapshot.json?ref=${h1}`);
  assert.equal(Buffer.from(old.body.content, 'base64').toString(), '{"s":1}');
  assert.equal((await call('GET', `${R}/contents/inv/snapshot.json?ref=b`)).status, 404);
  const expr = await gql(`{ repository(owner: "o", name: "r") { object(expression: "${h1}:inv/manifest.json") { ... on Blob { oid text } } } }`);
  assert.equal(expr.body.data.repository.object.text, '{"v":1}');
  const blob = await call('GET', `${R}/git/blobs/${expr.body.data.repository.object.oid}`);
  assert.equal(Buffer.from(blob.body.content, 'base64').toString(), '{"v":1}');
  // git's blob hash, so equal bytes have equal oids.
  assert.equal(expr.body.data.repository.object.oid, createHash('sha1').update('blob 7\0{"v":1}').digest('hex'));

  const tree = await call('GET', `${R}/git/trees/b?recursive=1`);
  assert.deepEqual(tree.body.tree.map((t) => t.path), ['inv/events.ndjson', 'inv/manifest.json']);
});

test('the double refuses what GitHub refuses', async () => {
  await call('POST', '/__control', { reset: true });
  const h0 = (await call('GET', `${R}/git/ref/heads/b`)).body.object.sha;
  const missing = await save(h0, {}, ['nope.json']);
  assert.equal(missing.body.errors[0].type, 'UNPROCESSABLE');
  const unknownField = await gql('{ repository(owner: "o", name: "r") { nothing } }');
  assert.equal(unknownField.body.data, undefined);
  assert.match(unknownField.body.errors[0].message, /doesn't exist on type 'Repository'/);
  // Controls: another writer moves the head before the next guarded commit; an injected type or status.
  await call('POST', '/__control', { conflictNext: 'x.json' });
  assert.equal((await save(h0, { 'x.json': '1' })).body.errors[0].type, 'STALE_DATA');
  const h1 = (await call('GET', `${R}/git/ref/heads/b`)).body.object.sha;
  await call('POST', '/__control', { graphqlErrorNext: 'FORBIDDEN' });
  assert.equal((await save(h1, { 'x.json': '1' })).body.errors[0].type, 'FORBIDDEN');
  await call('POST', '/__control', { failNext: 502 });
  assert.equal((await save(h1, { 'x.json': '1' })).status, 502);
  assert.equal((await call('GET', `${R}/git/ref/heads/b`)).body.object.sha, h1);
});

test('the Contents API the positive-control fixture writes with is unchanged', async () => {
  await call('POST', '/__control', { reset: true });
  const created = await call('PUT', `${R}/contents/causa/investigations/a.json`, { message: 'm', content: b64('one'), branch: 'b' });
  assert.equal(created.status, 201);
  const read = await call('GET', `${R}/contents/causa/investigations/a.json?ref=b`);
  assert.equal(read.status, 200);
  assert.equal(read.headers.get('cache-control'), 'private, max-age=60, s-maxage=60');
  assert.equal(read.body.sha, created.body.content.sha);
  assert.equal((await call('PUT', `${R}/contents/causa/investigations/a.json`, { message: 'm', content: b64('two'), branch: 'b' })).status, 409);
  assert.equal((await call('PUT', `${R}/contents/causa/investigations/a.json`, { message: 'm', content: b64('two'), branch: 'b', sha: read.body.sha })).status, 200);
  await call('POST', '/__control', { conflictNext: 'causa/investigations/a.json' });
  const now = (await call('GET', `${R}/contents/causa/investigations/a.json?ref=b`)).body.sha;
  assert.equal((await call('PUT', `${R}/contents/causa/investigations/a.json`, { message: 'm', content: b64('three'), branch: 'b', sha: now })).status, 409);
  await call('POST', '/__control', { failNext: 422 });
  assert.equal((await call('PUT', `${R}/contents/causa/investigations/b.json`, { message: 'm', content: b64('x'), branch: 'b' })).status, 422);
  const dir = await call('GET', `${R}/contents/causa/investigations?ref=b`);
  assert.deepEqual(dir.body.map((d) => d.name).sort(), ['a.json']);
});
