# Test deploy key: the exception ODR-COMP-P13 requests (ODR-R10-36 for Rehearsal 010)

**Status: PROPOSED. Not recorded.** Only the owner records it, with `tools/owner-freeze.fsx -- decide competition`
(ODR-COMP-P13) and `-- decide rehearsal-010` (ODR-R10-36). Agents never record (RDP-003). Both requests pin this file:
the decision records its sha256 at origin/main, so the wording below is what is decided.

## 1. Why

The owner added four GitHub Actions secrets to the competition and rehearsal repositories so the deployed app
(`deploy-pages.yml`) reaches the live test data repository on its own: `REPO_OWNER`, `DATA_SOURCE`, `CAUSA_BRANCH`,
`DATA_ACCESS_KEY_CAUSA`. The owner states that this is a test, that the repository holds no secret data, and that the
access is acceptable, and chose to **amend the requirement** rather than avoid bundling. Today the frozen requirements
forbid it:

| Requirement | Where | Text today |
|---|---|---|
| **EXT-DATA-004-O1** (P0) | `requirements-compiled/v4` acceptance obligations (AO-EXT-DATA-004-O1), unchanged through v4.1.6; source `docs/REHEARSAL-EXTERNAL-DATA-BOUNDARY.md` | Credentials MUST never be committed, bundled, logged, embedded in URLs, exposed in error text, stored in localStorage, or written into domain event files. |
| **DF-CON-2026-A002**, bootstrap criterion | Conditor decision record; carried in the `indy-init` preset (`conditor.json` `requirementsImport.acceptance[slice=bootstrap].criteria[0]`) | Baseline security from the first commit: no credential, token or provider key is committed, bundled into the client, or written to Praxis state, reports or logs (P35.3, R11.3, K-STOP-11). A CI check fails the build when one appears. |

Its neighbours are touched by the same change, so they are named too (section 4).

## 2. The exception, and nothing wider

1. **One key.** Only the value of the secret `DATA_ACCESS_KEY_CAUSA`. No other credential is ever bundled.
2. **One repository.** Only for the designated test data repository `REPO_OWNER`/`DATA_SOURCE` on branch `CAUSA_BRANCH`.
   The app sends the key only to `https://api.github.com/repos/REPO_OWNER/DATA_SOURCE...` and `https://api.github.com/graphql`
   (EXT-DATA-004-O2 unchanged).
3. **One build.** Only the deployed test build: the build step of `.github/workflows/deploy-pages.yml`, which alone
   receives the four secrets and sets `CAUSA_DEPLOY_CONNECTION=1`. Local builds, Build and test, Candidate acceptance and
   the Verification gate never read the secrets and bundle nothing: the app shows the Connect form exactly as today.
4. **One file.** Only `dist/deploy-connection.json` of the deploy artifact (`{"schema":"causa.deploy-connection/1",
   "connection":{"owner","repository","branch","key"}}`), read by the app at load (same origin, no credential sent).
   Never in `index.html`, a script, an assembly, `build-info.json` or any other file of the artifact.
5. **Still never:** committed to any repository; logged (build log, console, Praxis telemetry); in events, snapshots,
   manifests or any investigation file; in reports or Praxis state; in a URL; in error text or an announcement; in
   localStorage, sessionStorage or any browser store; in screenshots, traces or test artifacts. In the tab it is the
   opaque `Credential.T`, used only as the Authorization header (IF41-WIRE, unchanged).
6. **Least privilege.** A fine-grained personal access token, resource owner `REPO_OWNER`, repository access **only
   `DATA_SOURCE`**, repository permissions **Contents: Read and write** (Metadata: Read-only is added by GitHub and is
   required to read the repository's permissions); no other permission, no account permission. Expiry no later than
   7 days after the competition. The app needs exactly this: it reads files and the repository's permissions, and
   writes with `createCommitOnBranch` (EXT-DATA-004-O2's test asserts it). EXT-DATA-004-O4 (no unrestricted token by
   default) still holds.
7. **Revocable.** The owner revokes the token (Settings, Developer settings, Fine-grained tokens) and deletes the secret
   when the test ends or on any suspicion; the deployed app then fails closed (SM-CONNECTION AuthFailed, the Connect
   form). Rotation is: new token, update the secret, re-run the deploy.
8. **Accepted risk, stated plainly.** GitHub Pages sites are public. **Anyone who opens the deployed site can read the
   key from `deploy-connection.json` and, until it is revoked, read and write `REPO_OWNER/DATA_SOURCE` with it.** The
   owner accepts this for a test repository with no secret data. No other repository and no other permission is exposed.

## 3. Options

- **A (recommended, per the owner's direction): approve this exception** for the competition (ODR-COMP-P13) and, by its
  counterpart, Rehearsal 010 (ODR-R10-36), with the amended wording, obligations, tests and CI check below.
- **B: keep the prohibition.** The key is entered per session in the Connect form; the four secrets stay unused (the
  owner should then delete `DATA_ACCESS_KEY_CAUSA`). Nothing below is applied.

## 4. What option A amends (applied after the decision, never in a frozen package)

Frozen packages are not edited. The amended text is applied by an agent, in a reviewed pull request, to the v4.1.6
package while it is still a candidate (or to its successor), and to the competition's carried criteria. **Dependency:**
`claude/v4.1.6-review-fixes` is in flight on v4.1.6 (`verification-execution-plan.json`, `agent-file-ownership.json`,
`AMENDMENT-4.1.6.md`); P13's application is made after it merges, not on it.

| Id | Amended text (option A) |
|---|---|
| EXT-DATA-004-O1 | Credentials MUST never be committed, bundled, logged, embedded in URLs, exposed in error text, stored in localStorage, or written into domain event files. **Sole exception (ODR-COMP-P13):** the deployed test build bundles the one key of the secret `DATA_ACCESS_KEY_CAUSA`, for the designated test data repository `REPO_OWNER`/`DATA_SOURCE` on `CAUSA_BRANCH` only, in `deploy-connection.json` of the deploy artifact only (EXT-DATA-004-O5). |
| EXT-DATA-004-O3 | In-memory-only credentials require reauthorization after reload. **In the deployed test build, the reauthorization is the injected connection read again at load (EXT-DATA-004-O5); every other build is unchanged.** |
| EXT-DATA-004-O5 (**added**) | The deploy build, and only it, writes `deploy-connection.json` from the four secrets `REPO_OWNER`, `DATA_SOURCE`, `CAUSA_BRANCH`, `DATA_ACCESS_KEY_CAUSA`; with all four set it holds that connection, with none set it holds no connection, with only some set the build fails naming the missing secrets (never a value). The app reads it after its build record and, when it names a connection and the tab is disconnected, connects with it without the user typing the key; otherwise the Connect form is shown as before. The key is held as the opaque credential and appears nowhere else. |
| EXT-DATA-002-O1 | A fresh launch MUST display an accessible empty/disconnected workspace and an explicit Connect data repository action, **except the deployed test build with an injected connection (EXT-DATA-004-O5), which connects to that one repository at load; a failed connection shows the Connect action.** |
| PF-015-O4 | Never embed or print credentials, put them in URLs, or include them in screenshots, traces, logs, or artifacts, **except the one deploy key in the deploy artifact's `deploy-connection.json` (EXT-DATA-004-O5).** |
| PERSIST-003-O3, OD-INDY-10 | Unchanged in wording. The key is never in a browser store; in the tab it is memory-only. Recorded here because the key now also exists in the public deploy artifact. |
| INV-21 (AO-INV-21-2) | Unchanged for every test run (no test build carries the key). The deploy artifact is covered by the CI check below instead. |
| DF-CON-2026-A002, bootstrap criterion | Baseline security from the first commit: no credential, token or provider key is committed, bundled into the client, or written to Praxis state, reports or logs (P35.3, R11.3, K-STOP-11), **except the one test deploy key ODR-COMP-P13 allows, in the deploy artifact's `deploy-connection.json` only.** A CI check fails the build when **any other** credential appears, **or that key appears anywhere else.** |
| DF-CON-2026-A002, persistence authentication criterion | ... a fine-grained personal access token scoped to one investigation data repository, contents read and write only, with a short expiry. It is entered at runtime, **or, in the deployed test build only, injected from `DATA_ACCESS_KEY_CAUSA` (ODR-COMP-P13)**, held in memory for the session only and never stored in localStorage. ... |

### Tests the amended obligations bind (the competing build implements them)

Reference implementation: `kemiller2002/indy-init-rehearsal-010` branch `setup/test-deploy-key` (held, not merged).

| Obligation | Test |
|---|---|
| EXT-DATA-004-O1 | `dotnet:Causa.Acceptance.Live.EXTDATA004Tests.EXT-DATA-004-O1`, **unchanged**: no token literal in the source or a non-deploy build. |
| EXT-DATA-004-O5 (generation) | `dotnet:Causa.App.Tests.DeployConnectionTests` "no secret set: no connection, nothing bundled", "all four secrets: the one connection, rendered for the application", "some secrets: the build fails naming the missing secrets, never a value", "a malformed secret fails naming the secret, never the value", "the settings never print the key" |
| EXT-DATA-004-O5 (scan) | same class: "scan passes: a build without secrets ships no credential", "scan passes: the deploy build ships the one key, in deploy-connection.json only", "scan fails: any other token literal, in any build", "scan fails: the injected key outside deploy-connection.json", "scan fails: a deploy-connection.json not at the artifact root ...", "scan fails: a connection in a build that has no deploy secrets", "scan fails: another token or another repository in deploy-connection.json", "scan findings never carry a credential value" |
| EXT-DATA-004-O5, EXT-DATA-002-O1, -004-O3 (app) | same class: "the deploy connection is read once, after the build record", "without a connection in the file the app is exactly as before", "with the injected connection the app connects to that repository with that key, untyped", "a connection the user already made is not replaced", "the read is a same-origin GET without credentials, and its answer is decoded" |

The existing tests asserting that a page load reads only its build record (EXT-DATA-001/002/004-O3) stay as they are:
the deploy connection is read after the build record, not at load.

## 5. The CI check (DF-CON-2026-A002) after option A

- **Source:** unchanged. EXT-DATA-004-O1's scan of `src/`, `web/` and `dist/` fails on any token literal; the key is never
  in the repository.
- **Every build:** the build scans its own output (`build.fsx`, `DeployConnection.scan`) and fails on any GitHub token
  literal, and on any connection in `deploy-connection.json` when the build has no deploy secrets.
- **Deploy artifact:** the build scan, and an independent workflow step before upload ("Scan the site for credentials"),
  fail on any credential except exactly the injected `DATA_ACCESS_KEY_CAUSA` value, and on that value anywhere but
  `dist/deploy-connection.json`, or more than once there, or for another repository. Findings name the file, never the value.

## 6. Where the deploy workflow comes from

`.github/workflows/deploy-pages.yml` is generated by **Conditor** (`webDeployWorkflow` in
`src/Conditor.Core/Scaffolding.fs`, `conditor init --preset indy-init`); the lock pins Conditor 0.10.20 by binary digest
and the preset by `manifestSha256`. It is **not** governed: `r9.fsx govern` copies only `verification-gate.yml`, and
AGENT-RULES gives "CI and deployment" to the integration role. Two routes:

1. **For 7 November (recommended):** the competing build's integration lane applies the change to its generated
   `deploy-pages.yml` (the build step's `env:` and the scan step, exactly as in the reference implementation), as part
   of EXT-DATA-004-O5. No Conditor release and no lock change.
2. **Afterwards:** the same change in the Conditor template, prepared on `kemiller2002/conditor` branch
   `claude/test-deploy-key-template` (draft PR, gated on P13). It reaches a competition only through a Conditor release
   and an owner re-pin of the lock (ODR-RG-04).

## 7. Owner steps before T0 (with P-7)

With the App secrets, when pre-creating the competition repository (ODR-COMP-P07 A), and only if P13 is A: add the four
repository secrets `CAUSA_BRANCH`, `DATA_ACCESS_KEY_CAUSA`, `DATA_SOURCE`, `REPO_OWNER` (the token per section 2.6), set
`DEPLOY_TARGET=github-pages` and Pages source "GitHub Actions". With P13 B, leave `DATA_ACCESS_KEY_CAUSA` unset.
