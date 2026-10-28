# Implementation findings

This file covers the application and acceptance-tooling requirements among the 31 unverified P0 requirements. Process requirements are in [process-evidence-findings.md](process-evidence-findings.md). Every path is in `kemiller2002/indy-init-rehearsal-005` at the final candidate `1b9fb56873fe7a98dffd5172066d909449c17eae` unless it is prefixed `indy-init:`. All post-rehearsal runs used an isolated worktree of that commit, which was not modified (`git status` was clean afterwards).

## Behaviour checks run on the frozen candidate

| Run | Result | Record |
|---|---|---|
| The candidate's own .NET suite (`dotnet test App.slnx -c Release`, SDK 10.0.402, causa-data at 3d861bc as CI pins it) | 252 passed, 0 failed | [dotnet-frozen-suite.txt](post-rehearsal-evidence/dotnet-frozen-suite.txt) |
| The same suite with no credentials and every proxy variable pointed at a dead port | 252 passed, 0 failed | [dotnet-frozen-suite-offline.txt](post-rehearsal-evidence/dotnet-frozen-suite-offline.txt) |
| The candidate's non-live browser suite, all configured projects, local build | 106 passed, 1 failed (Percepta `verify-app`: the Percepta CLI is not installed in the audit environment, `spawnSync percepta ENOENT`) | [browser-frozen-suite.txt](post-rehearsal-evidence/browser-frozen-suite.txt) |
| The same suite on chromium against the deployed Pages bytes (b972cb4), through a localhost forwarder | 82 passed, 4 failed (3 evidence-screens failures caused by comparing the page with local evidence files, which differ from the deployed b972cb4 evidence by design; Percepta CLI missing) | [browser-deployed-suite.txt](post-rehearsal-evidence/browser-deployed-suite.txt) |
| 13 new F# tests written by this audit, using the candidate's own test harness by project reference | 13 passed | `indy-init:tools/rehearsal-005-audit/post-rehearsal-tests`, [post-rehearsal-tests.trx](post-rehearsal-evidence/post-rehearsal-tests.trx) |
| F# Playwright probe for rendered contrast, scroll and focus on 8 views of the candidate's test build with fixture data | see UI-011 and UI-016 below | `indy-init:tools/rehearsal-005-audit/post-rehearsal-ui`, [ui-probe.json](post-rehearsal-evidence/ui-probe.json) |
| Indy-init's own verifier tests at the ref R5 pinned (459f65a) | 19 tests OK | [indy-init-verifier-tests-459f65a.txt](post-rehearsal-evidence/indy-init-verifier-tests-459f65a.txt) |

Live-provider tests were not run. The audit has no credential for the approved test repository, and running them would also write to it.

## Implemented, verification missing (A)

### EXT-DATA-006: explicit create; guarded write; no overwrite of unread remote history

- **Implementation.** `src/persistence/Sync.fs` writes through a guarded `createCommitOnBranch` with `expectedHeadOid`. Write markers combine the page-load nonce and a counter. Reconcile reads the commit back (blob ids) before it claims "saved". `GitHub.parseCommit` maps a moved head to a held conflict and an untyped error to Unknown.
- **Behaviour.** DCT-001-A/B, the conflict test and DCT-004-A passed in run 40620099844 and again post-rehearsal. The live DCT-LIVE-A two-tab variant (a lost write is never reconciled as saved) passed in the run.
- **Why it was NOT_RUN.** F-1 falsified the original tags. The fix in 5336ec4 removed every tag for this obligation, and the reviewer who closed DC-001 with live evidence did not re-bind them.
- **Evidence at the deadline.** This is the only requirement whose open obligation already had admissible evidence (run 40620099844 at 1b9fb56).
- **Caveat (review).** "Separately verified authorization" holds under owner decision DEC-025: GitHub's answer to the write decides, and permission fields are advisory.

### PF-005: provider isolation

- **O1** (UI and domain tests independent of GitHub, network and credentials). The isolation is structural: the .NET tests drive the real Sync program against `RecordedGitHub`, and every non-live browser spec runs under the guard in `tests/browser/support/fixtures.js`, which answers GitHub from recordings and fails on any other host.
- **Post-rehearsal.** The offline .NET run passed 252/252. The offline run redirected proxy variables; no network namespace was available. The browser suite's own guard is the stronger check.
- **Gap.** No test asserted O1, so the cause is 2.

### PF-009: concurrency fixtures (O3: request identity, active provider, credential generation, freshness)

- **Implementation.** `Sync.fs` matches each answer to an `InFlight` entry and its `Generation`. Connect, Disconnect and credential rejection increment the generation (`credentialRejected`). Mutable reads carry a per-page nonce (`causa_fresh`).
- **Post-rehearsal tests.** A late answer for the previously active workspace is refused, and the model is unchanged. A late answer issued before a credential rejection is refused, and the rejection is not masked. The generation rises on every Connect and Disconnect.
- **Caveat (review).** The fixture-versus-GitHub provider switch goes through the same generation mechanism (`PersistencePort.fs`), but no test executed it.

## Partially implemented (B)

| Requirement | What exists | What is missing | Code needed? |
|---|---|---|---|
| EXT-DATA-005 | Path, manifest, workspace, schema-version and admissibility validation, all fail-closed (`Catalog.parsePointer`, `parseManifest`, `checkCase`; `Store.read`, 17 stages with `History.validate`). Verified by historical and post-rehearsal tests, including pointer traversal and manifest refusals. | No repository or branch **permission check**. `RepositoryInfo.CanPush` is parsed and never used, because DEC-025 / OD-INDY-10 makes permission fields advisory (DC-004). | Only if the owner keeps the clause over DEC-025 (small) |
| PF-015 | The token appears only in the Authorization header (O3 PASS). A missing secret is BLOCKED (O4 PASS). Broad classic scopes are refused (`GitHub.broadClassicScopes`). | O1: no anonymous read path, so catalog tests need a PAT (`Credential.create` refuses a blank token; F-3). DEC-025 decided "no anonymous mode". O2: the secret's least privilege is unverified, and DEC-025 gives it write. | Only if the owner reverses DEC-025 (medium) |
| UI-011 | Focus management after the WebAssembly handoff (`main.ts` `ready.focus()`), for selection, the inspector and confirmations. Post-rehearsal probe: after a command re-render, the control keeps focus and its exact viewport position (313.125 px before and after). After back navigation, scroll is restored (1442 px). | After back navigation, **focus falls to `body`**. There is no scroll-management code; Chromium's anchoring and history restoration keep scroll, and WebKit was not measured. | Yes, small |
| UI-016 | Rendered text meets WCAG AA on every measured view and surface, washes included: 0 failures across 8 views. Palette tokens and grayscale print pass. | Amber (#E9A23B) indicator borders measure 1.95:1 on ivory and 2.17:1 on white, below the 3:1 non-text threshold (`.causa-item--hypothesis`, `--blocker`, `.causa-attention`, `.causa-report__incomplete`). They are paired with text, so whether they are "relevant" indicators needs an accessibility decision. O15 needs an independent visual review of the existing deployed screenshots. | Small (border colour), unless the owner rules them out of scope |
| UI-031 | Operational facts sit in Administration, provenance is kept, and credentials are never exposed (O1–O4 PASS). | Role or access control for sensitive operational information: none exists in `src/app`. The only "roles" are the S17 panel's agent roles. | Medium, or an owner decision that a single-user app needs none |
| AR-011 (tooling) | Provenance per PASS (O1 PASS). Reuse-plan input digests. | A re-run comparison that separates reproducible differences from environment drift. The SDK is unpinned (CI `dotnet-version: 10.0.x`, no global.json). The rebuild with SDK 10.0.402 gave 193 framework files different fingerprints and changed the bytes of 2 same-named runtime files ([deployed-vs-local-build.tsv](post-rehearsal-evidence/deployed-vs-local-build.tsv), corrected after review). | Small (pin, plus comparison tooling) |
| AR-019 (tooling) | The verdict is computed from the pinned baseline, plan, approvals and run evidence. False accepted flags, baseline tampering and self-approved waivers are rejected (O1, O2, O4 PASS). R2's ledger is INVALID. | O3: "Original Rehearsal 3 must yield BLOCKED". The R3 record is not a machine ledger, the verifier rejects it structurally, and Indy-init classifies it FAIL. | Small, or an owner amendment to O3 |

## Indeterminate (D)

| Requirement | Why it cannot be established |
|---|---|
| PF-014 | Real network communication, request association and the three historical reconstructions with manifest digests are evidenced: provider conformance (19 live checks) and the live catalog load (`Catalog.checkCase`). "Deployed application behavior" was never run live, because the live specs ran against a local `dist/`. The audit has no credential, and Pages no longer serves 1b9fb56. |
| UI-015 | Every element of O2 passed under its own obligation. O3, the run on the deployed exact SHA, never happened, and the post-rehearsal deployed run is b972cb4. The harness could not have done it: the mocked-spec network guard aborts the deployed host. This was reproduced: `a mocked browser test tried to reach the real network`. |
| WDL-014 | The app invokes the real forma-workflow frame (`shell.spec.js`), the pinned version is checked (`graph-layout.spec.js`) and the accessibility specs pass, all at 1b9fb56 locally. The deployed-browser clause has only post-rehearsal b972cb4 evidence, and those specs load the vendored frame directly, so it is not established. |

## Not implemented (C)

None. Every one of the 31 has at least some of its required behaviour in the candidate. The four genuinely absent behaviours are partial gaps within requirements that are otherwise implemented: the permission check, anonymous read, role/access control and focus after back navigation.

## Structural findings

- **Deployed mode was impossible.** `playwright.config.js` documents `CAUSA_BASE_URL`, "runs the production suites against a deployed site", but the network guard every mocked spec imports aborts any non-local host. No deployed-SHA browser obligation could have passed in R5 as built. This blocked UI-015-O3, WDL-014-O1, PF-014-O2 and part of UI-016-O15.
- **Fixture data was unpinned in browser and live jobs.** `.github/workflows/build-and-test.yml` pins causa-data at 3d861bc for the .NET jobs but uses `ref: main` for the browser and live jobs.
- **Two JSON codecs.** `src/engine/Json.fs` and `src/persistence/Json.fs` are two JSON codecs with no recorded reuse justification (AR-005-O2).
