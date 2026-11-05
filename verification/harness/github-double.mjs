// Harness-only GitHub REST double (a subset of the contents, trees and blobs API) used to prove the governed journeys
// against synthetic fixture apps in an environment without a GitHub token. It is NEVER real-provider evidence: the
// journeys record its base URL, and the slice and completion gates refuse any live result whose provider is not
// https://api.github.com (LIVE_PROVIDER_SUBSTITUTED).
//
//   node verification/harness/github-double.mjs PORT
//
// POST /__control {"conflictNext": path}  -> the next PUT to that path sees a newer remote (409)
// POST /__control {"failNext": status}    -> the next PUT fails with that status
// POST /__control {"reset": true}
import http from 'node:http';
import crypto from 'node:crypto';

const files = new Map(); // `${owner}/${repo}/${branch}/${path}` -> { content (base64), sha }
let control = {};
const sha = (s) => crypto.createHash('sha1').update(s).digest('hex');
// Answers carry the caching headers api.github.com sends on reads (Cache-Control: private, max-age=60, s-maxage=60;
// Vary: Accept, Authorization, ...), so an app whose read-back is served from the browser's HTTP cache fails here as it
// does against the real provider (live preflight run 42068316592: J3-05 failed on Chromium only).
const send = (res, status, body, method) => {
  res.writeHead(status, { 'content-type': 'application/json', 'access-control-allow-origin': '*', 'access-control-allow-headers': '*', 'access-control-allow-methods': 'GET,PUT,POST,DELETE,OPTIONS',
    ...(method === 'GET' && status === 200 ? { 'cache-control': 'private, max-age=60, s-maxage=60', vary: 'Accept, Authorization, Cookie, Accept-Encoding' } : {}) });
  res.end(body === undefined ? '' : JSON.stringify(body));
};
const readBody = (req) => new Promise((ok) => { let b = ''; req.on('data', (c) => (b += c)); req.on('end', () => ok(b ? JSON.parse(b) : {})); });

const server = http.createServer(async (req, res) => {
  const reply = (status, b) => send(res, status, b, req.method);
  if (req.method === 'OPTIONS') return reply(204);
  const url = new URL(req.url, 'http://double');
  if (url.pathname === '/__control') { const b = await readBody(req); control = b.reset ? {} : { ...control, ...b }; if (b.reset) files.clear(); return reply(200, control); }
  let m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)$/);
  if (m) return reply(200, { full_name: `${m[1]}/${m[2]}`, default_branch: 'main', permissions: { pull: true, push: true } });
  m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)\/branches\/([^/]+)$/);
  if (m) return reply(200, { name: m[3] });
  m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)\/git\/trees\/([^/]+)$/);
  if (m) {
    const prefix = `${m[1]}/${m[2]}/${m[3]}/`;
    const tree = [...files.entries()].filter(([k]) => k.startsWith(prefix)).map(([k, v]) => ({ path: k.slice(prefix.length), type: 'blob', sha: v.sha }));
    return reply(200, { tree, truncated: false });
  }
  m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)\/git\/blobs\/([^/]+)$/);
  if (m) { const f = [...files.values()].find((v) => v.sha === m[3]); return f ? reply(200, { content: f.content, encoding: 'base64' }) : reply(404, { message: 'Not Found' }); }
  m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)\/contents\/(.*)$/);
  if (!m) return reply(404, { message: 'Not Found' });
  const branch = url.searchParams.get('ref') || 'main';
  const path = decodeURIComponent(m[3]);
  if (req.method === 'GET') {
    const prefix = `${m[1]}/${m[2]}/${branch}/${path}`;
    const f = files.get(prefix);
    if (f) return reply(200, { path, sha: f.sha, content: f.content, encoding: 'base64' });
    const dir = [...files.entries()].filter(([k]) => k.startsWith(prefix + '/')).map(([k, v]) => ({ name: k.slice(prefix.length + 1).split('/')[0], path: k.slice(`${m[1]}/${m[2]}/${branch}/`.length), sha: v.sha, type: 'file' }));
    return dir.length ? reply(200, dir) : reply(404, { message: 'Not Found' });
  }
  if (req.method === 'PUT') {
    const b = await readBody(req);
    const key = `${m[1]}/${m[2]}/${b.branch || 'main'}/${path}`;
    if (control.failNext) { const s = control.failNext; control.failNext = undefined; return reply(s, { message: 'injected failure' }); }
    const existing = files.get(key);
    if (control.conflictNext === path && existing) { control.conflictNext = undefined; existing.sha = sha(existing.sha + 'remote'); }
    if ((existing && b.sha !== existing.sha) || (!existing && b.sha)) return reply(409, { message: 'is at ' + (existing ? existing.sha : 'nothing') + ' but expected ' + b.sha });
    const next = { content: b.content, sha: sha(b.content + Date.now() + Math.random()) };
    files.set(key, next);
    return reply(existing ? 200 : 201, { content: { path, sha: next.sha }, commit: { sha: sha(next.sha + 'c') } });
  }
  return reply(405, { message: 'method' });
});
server.listen(Number(process.argv[2] || 4610), '127.0.0.1', () => console.log(`github-double on ${server.address().port}`));
