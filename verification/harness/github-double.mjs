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
const send = (res, status, body) => {
  res.writeHead(status, { 'content-type': 'application/json', 'access-control-allow-origin': '*', 'access-control-allow-headers': '*', 'access-control-allow-methods': 'GET,PUT,POST,DELETE,OPTIONS' });
  res.end(body === undefined ? '' : JSON.stringify(body));
};
const readBody = (req) => new Promise((ok) => { let b = ''; req.on('data', (c) => (b += c)); req.on('end', () => ok(b ? JSON.parse(b) : {})); });

const server = http.createServer(async (req, res) => {
  if (req.method === 'OPTIONS') return send(res, 204);
  const url = new URL(req.url, 'http://double');
  if (url.pathname === '/__control') { const b = await readBody(req); control = b.reset ? {} : { ...control, ...b }; if (b.reset) files.clear(); return send(res, 200, control); }
  let m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)$/);
  if (m) return send(res, 200, { full_name: `${m[1]}/${m[2]}`, default_branch: 'main', permissions: { pull: true, push: true } });
  m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)\/branches\/([^/]+)$/);
  if (m) return send(res, 200, { name: m[3] });
  m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)\/git\/trees\/([^/]+)$/);
  if (m) {
    const prefix = `${m[1]}/${m[2]}/${m[3]}/`;
    const tree = [...files.entries()].filter(([k]) => k.startsWith(prefix)).map(([k, v]) => ({ path: k.slice(prefix.length), type: 'blob', sha: v.sha }));
    return send(res, 200, { tree, truncated: false });
  }
  m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)\/git\/blobs\/([^/]+)$/);
  if (m) { const f = [...files.values()].find((v) => v.sha === m[3]); return f ? send(res, 200, { content: f.content, encoding: 'base64' }) : send(res, 404, { message: 'Not Found' }); }
  m = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)\/contents\/(.*)$/);
  if (!m) return send(res, 404, { message: 'Not Found' });
  const branch = url.searchParams.get('ref') || 'main';
  const path = decodeURIComponent(m[3]);
  if (req.method === 'GET') {
    const prefix = `${m[1]}/${m[2]}/${branch}/${path}`;
    const f = files.get(prefix);
    if (f) return send(res, 200, { path, sha: f.sha, content: f.content, encoding: 'base64' });
    const dir = [...files.entries()].filter(([k]) => k.startsWith(prefix + '/')).map(([k, v]) => ({ name: k.slice(prefix.length + 1).split('/')[0], path: k.slice(`${m[1]}/${m[2]}/${branch}/`.length), sha: v.sha, type: 'file' }));
    return dir.length ? send(res, 200, dir) : send(res, 404, { message: 'Not Found' });
  }
  if (req.method === 'PUT') {
    const b = await readBody(req);
    const key = `${m[1]}/${m[2]}/${b.branch || 'main'}/${path}`;
    if (control.failNext) { const s = control.failNext; control.failNext = undefined; return send(res, s, { message: 'injected failure' }); }
    const existing = files.get(key);
    if (control.conflictNext === path && existing) { control.conflictNext = undefined; existing.sha = sha(existing.sha + 'remote'); }
    if ((existing && b.sha !== existing.sha) || (!existing && b.sha)) return send(res, 409, { message: 'is at ' + (existing ? existing.sha : 'nothing') + ' but expected ' + b.sha });
    const next = { content: b.content, sha: sha(b.content + Date.now() + Math.random()) };
    files.set(key, next);
    return send(res, existing ? 200 : 201, { content: { path, sha: next.sha }, commit: { sha: sha(next.sha + 'c') } });
  }
  return send(res, 405, { message: 'method' });
});
server.listen(Number(process.argv[2] || 4610), '127.0.0.1', () => console.log(`github-double on ${server.address().port}`));
