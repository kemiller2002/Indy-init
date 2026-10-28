<!-- Investigation note written 2026-10-28 by a read-only investigation agent for the Echelon Engineering Improvement Initiative, Priority Seven (preserve UI and persistence improvements). Local paths refer to that session's clones. No code was changed in forma, percepta, arca, the R5 app or causa-write-test. -->

# Regression preservation: UI and persistence (Priority Seven)

## Scope and revisions

This note checks whether the UI and persistence improvements are protected by tests, and whether that protection would carry into Rehearsal 6.

| Repo | Local path | Revision | Notes |
|---|---|---|---|
| forma | `/home/user/kemiller2002/forma` | `1cf9482` (merge of forma#171), `package.json` 0.11.0 | `git ls-remote` shows tags only up to **v0.10.0**. npm `@echelon-foundry/design-system` latest is **0.10.0**. So 0.11.0, with the type roles, is merged but **not released**. |
| percepta | `/home/user/kemiller2002/percepta` | `c9747c2` | |
| R5 app | `/home/user/kemiller2002/indy-init-rehearsal-005` | `b972cb4` | Historical. It depends on `@echelon-foundry/design-system` 0.9.0 and `@echelon-foundry/limen` 0.9.0, and has **no Arca** dependency. |
| arca | `/home/user/kemiller2002/arca` | `d0ce9da` (merge of arca#46), tag `v0.6.0` | |
| limen npm 0.9.0 | unpacked to a scratch directory | | Only to read the browser kernel's Storage effect. |
| causa-write-test | `/home/user/causa-write-test` | `fe3b074` (shallow) | |

**causa-write-test is confirmed as the live persistence test DATA repository:**
- It holds only `README.md` and `workspaces/demo/{rec-*, live-*, r4-*}`. That is 115 workspaces: 57 `rec-`, 48 `live-` and 10 `r4-`.
- The `rec-<runId>` naming is written by the R5 recorder (`src/persistence/Tool/Program.fs:125, 205, 395`).
- `rec-40620099844` matches the R5 final CI run 40620099844.
- `live-*-other-writer/note.txt` is written by `tests/browser/live-persistence.spec.js:176`.
- The secret `CAUSA_TEST_REPO`, read at `.github/workflows/build-and-test.yml:570`, cannot be read here. The match is by content, not by the secret's value.
- It needed no change, and none was made.

## 1. UI protection

All Forma `tests/browser/*.spec.mjs` run in CI through `.github/workflows/conformance.yml:56-58` (`npm run test:browser` → `playwright test`):
- Projects: chromium, firefox and webkit (`playwright.config.mjs:13-16`).
- Triggers: every pull request and every push to main.
- Note the 600 s `sleep` at `conformance.yml:20-22`.

They also run in `design-system-pilot-validation.yml:76` and `validate-forma-feature.yml:60`.

| Concern | Forma test (reusable) | Runs in CI? | Percepta (reusable) | R5 consumer test (historical only) |
|---|---|---|---|---|
| Text overflow | `tests/conformance/catalog-text-scale.spec.mjs:43` (ULI-L05, every catalog example at 320/390px and 100%/200% text). `tests/browser/composition-stress.spec.mjs:31,49`. `operational-mobile.spec.mjs:53`. Helper `packages/conformance/src/evaluate.mjs:203` `evaluateOverflow` and `src/index.mjs:344,381` | `conformance/`: only `forma-site-validation.yml` → `ui:conformance` (`playwright.conformance.config.mjs:10-16`, **chromium only**, **path-filtered** PRs to main and `work/**` pushes). `tests/browser/*`: yes (3 engines) | Document-level horizontal overflow per breakpoint only, `src/Percepta.Adapter.Playwright/BrowserVerification.fs:535-539`. Adversarial case `tests/Percepta.Adversarial.Tests/Program.fs:186`, run by `percepta-semantic-build.yml:133` | `tests/browser/shell.spec.js:214`, `ux.spec.js:224` (page scrollWidth) |
| Responsive layout | `tests/browser/mobile.spec.mjs:118,140`. `operational-mobile.spec.mjs:53-105`. `theme-layout-matrix.spec.mjs:28,36`. `type-roles.spec.mjs:199,268` (320–1280px). `tests/site-browser/mobile.spec.mjs`, `responsive-navigation.spec.mjs` | `tests/browser`: yes. `site-browser`: `site:mobile` (chromium) in `forma-site-validation.yml:82` and `deploy-forma-pages.yml:67`, path-filtered | Required regions and observations visible per breakpoint, `BrowserVerification.fs:511-531` | `identity.spec.js:143` (`@responsive`) |
| Component containment | `tests/browser/corner-containment.spec.mjs:13` (LCE-001 radius versus padding). `flyout-containment.spec.mjs:13,49,81`. `composition-stress.spec.mjs:49`. `type-roles.spec.mjs:100-134` (line-box and sibling overlap, clipping). Unit test of the corner evaluator `tests/conformance-package.test.mjs:49-61`. Internal F# `tests/layout/Forma.Layout.Probe` (`boundary.corner`, `contain.*`) | `tests/browser`: yes. `conformance-package.test.mjs`: only via `test:package` inside `ui:conformance` (path-filtered). **`Forma.Layout.Probe` (`npm run test:layout`) is not run by any workflow**: grep of `.github/workflows` finds no match | **None** at element level. No containment, clipping or corner check (confirmed at `c9747c2`) | `graph-layout.spec.js:46`, `live-graph.spec.js:97` (node overflow and clipping) |
| Typography roles | `tests/browser/type-roles.spec.mjs:199` (R5 heading scale), `:215`, `:230`, `:245`, `:256`. CSS `src/styles/foundations.css:300-371` (`data-ef-type` application, dense, accessible) | Yes (3 engines) | None | `identity.spec.js:143-154`, which asserts R5's own override `src/kernel/causa.css:342-353` |
| Accessibility | `tests/browser/accessibility.spec.mjs:5-102` (axe WCAG A/AA, `@axe-core/playwright`). `operational-mobile.spec.mjs:105`. `type-roles.spec.mjs:281` (focus and keyboard at 320px and 200% text). `composition-stress.spec.mjs:39,58`. `theme-layout-matrix.spec.mjs:28,41`. `forced-colors-projection.spec.mjs` | Yes (3 engines) | Keyboard reachability and a visible focus indicator per capability (`BrowserVerification.fs:372-433`). Positive tabindex (`:474`). Target type (`:190-208` per env-and-forma.md). Run per app by the R5 `percepta` CI job (`build-and-test.yml:764`) | `identity.spec.js:114` (keyboard, no drag or hover). There is **no axe in R5** (no `axe` match in `tests/browser` or `package.json`) |

### UI carry-over risks for Rehearsal 6
1. **The protections are in Forma source but not in a release.**
   - The type roles and the `forma-conformance` corner helpers (`measureCornerContainment`, `expectTextInsideCorners`, `packages/conformance/src/index.mjs:422,452`) are in Forma 0.11.0. That version is not tagged and not on npm.
   - The npm `forma-conformance` 0.2.0 tarball has **no** `CornerContainment` symbol. The CHANGELOG at line 17 says "Released with the package's next version".
   - R5 pins design-system 0.9.0, which predates both the LCE-001 radius bounds (0.10.0) and the roles.
2. **The R5 consumer assertions are application tests.**
   - They are `identity.spec.js:143`, `shell.spec.js:214` and the graph-layout specs.
   - They will not exist in Rehearsal 6 unless a requirement or a template carries them.
3. **Percepta covers only page-level overflow and focus.** An app that passes Percepta can still clip text in a pill (the `.causa-tag` pattern, `causa.css:175`).
4. **The Layout probe is not in CI.** The `site-browser` and `conformance/` suites are path-filtered and run on chromium only.

## 2. Persistence protection

Legend: **R5** means the test lives in the historical app and does not carry over. **Arca** means the test lives in the reusable library and carries over only if Rehearsal 6 adopts Arca.

R5 CI runs every .NET test via `dotnet test App.slnx` (`build-and-test.yml:166`, job `core`, on PRs and pushes to main and `r5/**`). Playwright non-live specs run at `:481` and live specs at `:586` (main only).

Arca CI:
- `build-and-test.yml:32` runs `dotnet test Arca.slnx` and fails on zero passes or any skips.
- `browser-verification.yml` runs `verification/browser/tests/*.spec.mjs` in Chromium **and WebKit** (`playwright.config.mjs:26-28`, "Desktop Safari" profile, not a real iPad).

| Concern | R5 app tests (historical) | Arca 0.6.0 tests (reusable) |
|---|---|---|
| Unknown write outcomes | `tests/Persistence.Tests/SyncAppTests.fs:187,253,272,340`. `DcTableTests.fs:154,172,288,305,327,356,381` (F-2, R2-F2: unrecognised GraphQL or HTTP after send → unknown). `DctProviderTests.fs:90`. `App.Engine.Tests/WriteInvariantTests.fs:394,400,407,438,472` | `PersistenceSafetyTests.fs:214,233,248`. `OfflineQueueTests.fs:224,235,250,269`. Browser: `verification/browser/tests/arca.spec.mjs:153` |
| Multi-tab changes | `DcTableTests.fs:235` (F-1, a unique marker per page load). `:403,427,457` (R2-F1, R3-F1 merge). Live two-tab: `tests/browser/live-persistence.spec.js:195-228`. **The local-store tests use `Harness.fs:83`, which merges in one atomic step, so they cannot exhibit R3-F2.** | `MultiTabQueueTests.fs:79,105,149,165,256,267,291,304,381`. `LimenQueueTests.fs:76,88,106,134,200`. Browser (Chromium and WebKit): `queue.spec.mjs:30,71`, `limen-queue.spec.mjs:53,93` |
| Conflict recovery | `SyncAppTests.fs:202,233,492`. `DcTableTests.fs:39,268`. `FixtureTests.fs:114`. `tests/browser/fixture-mode.spec.js:131` | `PersistenceSafetyTests.fs:193,326,345,366`. `OfflineQueueTests.fs:279,322`. `ConcurrencyTests.fs:42,154,165,209` |
| Reconciliation | `DcTableTests.fs:52,66,207,288,305`. `WriteInvariantTests.fs:152,159`. Confirmation needs a byte read-back (`DcTableTests.fs:172`, causa contract §9.4.3) | `PersistenceSafetyTests.fs:233,248`. `OfflineQueueTests.fs:224,250`. **See the gap below:** Arca reconciles by ancestry or idempotency key with no byte re-read |
| Stale acknowledgments | `DcTableTests.fs:81,108`. `SyncAppTests.fs:386`. `WriteInvariantTests.fs:212,219,243,249` | `PersistenceSafetyTests.fs:381,401,426`. Compile-failure fixtures `PersistenceSafetyTests.fs:97,105,121` (`tests/Arca.CompileFailure`: forged `Synchronized` and receipts do not compile) |

### Where protection lives

**Every F-1, F-2/R2-F2 and R2-F1/R3-F1 fix and its tests live only in the R5 app:**
- `src/engine/Sync.fs`;
- `src/persistence/Sync.fs`;
- `src/app/PersistencePort.fs`;
- the test projects above.

R4 vendored its adapter and R5 re-implemented it (persistence.md §4). Without a requirement, Rehearsal 6 inherits none of this automatically.

**Arca 0.6.0 is the reusable home.** It has equivalent scenario coverage and adds things R5 lacks:
- compile-time unforgeability;
- Web Lock ownership across tabs, checked in WebKit.

**Arca does not cover these R5 protections:**
- **No byte read-back on confirmation or reconciliation.**
  - `src/Arca.GitHub/GitHubStorage.fs:497-558` treats an operation as `Landed` when:
    - the candidate commit is the head or an ancestor of it (compare `identical` or `ahead`), or
    - a commit before the base carries the operation's idempotency key.
  - That is the R5 pre-fix shape of F-1.
  - Keys are application-supplied strings (`IdempotencyKey.create`, `src/Arca.Core/Concurrency.fs:90`). A colliding key, which is what F-1's per-page counter produced, would be reconciled as landed.
  - R5's test "a commit carrying our marker but other bytes is a conflict" (`DcTableTests.fs:268`) has no Arca equivalent. This was found by reading the code; it was not executed.
- **Causa's domain merge** of two tabs' records (`mergeRecords`, preserved entries) is application code. Arca chooses single-owner instead.

## 3. R3-F2: the non-atomic local read-merge-write (open; not resolved)

### Code
- **Model** (`src/persistence/Sync.fs`):
  - `persistLocal` at `:323-333` emits `StoreLocal(writeId, storageKey, record m)`. The key is `causa.persistence/2` (`:160`).
  - The record carries `writer = m.Nonce` and `basedOn` (`:315-320`).
- **Port, step 1** (`src/app/PersistencePort.fs:96`): `StoreLocal` becomes `Storage.Get(persist-merge-<id>, key)`. The pending value is kept in `State.Writes` (`:233`).
- **Port, step 2** (`PersistencePort.fs:257-266`): when the Get result arrives (a later engine message), the port computes `Sync.mergeRecords value stored` and emits `Storage.Set(persist-store-<id>, key, merged)`.
- **Port, step 3** (`PersistencePort.fs:268-270` → `Sync.fs:1122-1134`): `LocalStored(writeId, true)` marks the documents `SavedLocally`.
- **The known limit is documented in the code**, at `PersistencePort.fs:21-24`: "the read and the write are two storage calls, not one atomic step … There is no `storage`-event merge or lock yet."
- **Kernel**: Limen 0.9.0 `dist/kernel/browser-kernel.js:894-904` performs each Storage effect as one `localStorage.getItem` or `setItem`. Each call is atomic; the pair is not.
- **Merge rule** (`Sync.fs:441-490`):
  - If the stored record's `writer` equals this page's nonce, the stored documents are dropped wholesale (`own` at `:460-468`), and only `preserved` entries survive.
  - Otherwise, the other tab's documents this page does not hold are kept, and its precious documents are preserved (`:414`).
- **Fixture mode skips the store**: `PersistencePort.fs:212` answers `StoreLocal` with `LocalStored(true)` without touching storage, so fixture-mode browser specs never exercise this path.

### Concrete race
Tabs A (nonce `a`) and B (nonce `b`) share one origin. The store holds R0 with `writer = a`.

1. B makes an offline change to investigation X. It emits Get, which returns R0. The result is queued back to B's .NET engine.
2. A changes investigation Y. It emits Get, which also returns R0.
3. B computes `mergeRecords(mineB, R0)`. R0 is not B's, so A's documents are kept. B Sets RB, which holds X and Y0. B receives `LocalStored ok`, so X shows **"saved locally"**.
4. A computes `mergeRecords(mineA, R0)`. R0's writer is `a`, so `own = true` and R0's documents are superseded. A's record does not contain X, and R0 had no preserved entry for it. A Sets SA, which **overwrites RB**. X is no longer in storage.
5. B is not told: there is no `storage` listener. B's model still says `SavedLocally`.

**Consequence:**
- If B closes, crashes, or is discarded by the OS (common for background iPad tabs) before its next local write, X's unsaved change is lost.
- The UI claimed it was saved locally. That violates "saved locally" honesty.
- If B writes again first, its next merge re-adds X. That self-heal is why the defect is intermittent.

**The window is wide.** It spans at least one kernel round trip into WASM between Get and Set. In addition, cross-process localStorage coherence between tabs is implementation-specific. This was not verified here.

### Browser primitives (iPad Safari availability)
Availability is from `@mdn/browser-compat-data` 8.1.7. All iPadOS browsers use WebKit.

| Primitive | Makes it atomic? | Safari iOS/iPadOS | Already available to the app? |
|---|---|---|---|
| Web Locks (`navigator.locks.request`) | Yes, if every writer takes the same lock around Get-merge-Set (or, as in Arca, one tab owns the store for its lifetime). A context that dies releases its lock. | **15.4+** (also `Navigator.locks`) | Limen 0.9.0 coordination pack (`dist/capabilities/coordination/index.js:151-174`, `acquire`/`release`, `ifAvailable`/`steal`). Not registered by R5 (`src/kernel/main.ts` imports only `BrowserKernel` and `DotnetWasmTransport`). Arca already uses it: `src/Arca.GitHub/LocalStorageQueue.fs:136-142, 194-202`; `src/Arca.Limen/Locks.fs`. |
| IndexedDB readwrite transaction | Yes. Read and write inside one transaction is atomic, and overlapping readwrite transactions are serialized. | IDBTransaction 8+. `indexedDB.open` is reliable from 15. Some iOS 14 releases hang on the first open (WebKit bug 226547). | Limen 0.9.0 store pack (`dist/capabilities/store/index.js:242`; "never partly applied"). Not used by R5. Arca.Limen `IndexedDbQueue.fs` uses it with fencing epochs. |
| BroadcastChannel | No. It notifies only, and can't exclude a concurrent write. It is useful to tell other tabs to re-read. | 15.4+ | Limen coordination pack (`broadcast`). |
| `storage` event | No. It fires after the fact in other documents. It could trigger a re-merge but leaves the window open. | 4+. Delivery to a suspended background iPad tab is **unknown** (not established). | Not exposed by R5 code. |
| A compare-and-retry fence (re-Get before Set, retry on change) | No. It narrows the window but doesn't close it: localStorage has no compare-and-set. Arca says so at `LocalStorageQueue.fs:139-142`. | n/a | n/a |

**Not established:**
- whether localStorage writes are immediately visible to another tab that acquires a Web Lock just after (cross-process cache coherence) in WebKit or Chromium;
- Web Locks behaviour across iPad tab discard;
- whether the Playwright WebKit project in Arca CI (Linux "Desktop Safari") matches iPad Safari.

Arca avoids the coherence question by making one tab the sole owner and fencing saves on the exact text it last observed. **Recommended direction for Rehearsal 6:** use a Web Lock, or IndexedDB through Limen packs, or adopt Arca's single-owner queue. The status of R3-F2 remains **open**.

### Deterministic reproduction design (for Rehearsal 6; not implemented)

**Unit level (F#, no browser):**
- Add a store harness whose `StoreLocal` is split into two queued steps, `Get(writeId)` and `Set(writeId)`, the way the real port performs them. Today's `Harness.fs:83` does the read-merge-write in one step.
- Script this interleaving: B.Edit(X) → A.Edit(Y) → run B.Get → run A.Get → run B.Set (+ `LocalStored`) → run A.Set.
- Assert:
  - the final stored text, read through `parseStored` or `restoreRecord`, contains X's pending change (as a document or a preserved entry);
  - **or** B's status for X is not `savedLocally`.
- Negative control: the same script with the steps in serial order passes on the current code.
- Expected on R5 code: FAIL. That is the reproduction.
- Property variant: generate every interleaving of {A,B} × {Get, Set} for 2–3 writes, as Arca's `MultiTabQueueTests.fs:105` does for its queue. Assert that no precious change acknowledged as saved locally is absent from the final store.

**Browser level (Playwright, Chromium and WebKit projects):**
- Use two pages in one context.
- Add a test-build-only hook in the kernel composition root that holds Storage results whose correlation id starts with `persist-merge-` until released (a hold or release gate). Do not patch `localStorage`, because `setItem` is synchronous and can't be awaited.
- Script the interleaving:
  1. Hold A.
  2. Edit offline in B and wait for "saved locally".
  3. Edit in A.
  4. Release A.
  5. Close B.
  6. Open a new tab C.
- Assert that C restores X's change, or names it as preserved.
- The test must run in the WebKit project. Real iPad Safari remains a manual check.

**Pass criterion for a fix:** both tests pass with the lock, IndexedDB or single-owner design, and fail when that mechanism is disabled (mutation control).

## 4. Gaps that need Rehearsal 6 requirements

1. **Forma release.**
   - Rehearsal 6 must consume a released Forma that is 0.11.0 or later, with `data-ef-type="application"`, and a released `forma-conformance` that includes `expectTextInsideCorners`.
   - Today neither is tagged or published.
   - Replace R5's `causa.css:342-353` override and `.causa-tag` (`causa.css:175`).
2. **Consumer UI checks as requirements.**
   - Required: a heading-scale check, page and element overflow at 320px and 200% text, corner containment, and axe WCAG A/AA in the app (R5 had no axe).
   - Percepta alone checks only page overflow and focus.
3. **Forma CI coverage.**
   - `Forma.Layout.Probe` is not run in CI.
   - The `conformance/` and `site-browser/` suites run on chromium only and are path-filtered.
4. **Persistence library decision.**
   - Either adopt Arca 0.6.0 or require the R5 scenarios: F-1, F-2/R2-F2, R2-F1/R3-F1, stale and out-of-order acks, reload during send.
   - If Arca is adopted, require a byte read-back confirmation (causa contract §9.4.3) and globally unique idempotency keys per page load. Arca does not do the read-back today, and an F-1-style key collision would be reconciled as landed.
5. **R3-F2.**
   - Require atomic or exclusive cross-tab local persistence: Web Locks, IndexedDB transactions or single-owner.
   - Require the deterministic split-step reproduction above, run in Chromium and WebKit.
   - Require an explicit fallback when `navigator.locks` is missing (Safari < 15.4), never a silent unfenced writer. Arca's `MultiTabQueueTests.fs:291` is the model.
6. **Fixture mode should exercise the real local store**, or a separate spec must. Today `PersistencePort.fs:212` short-circuits it.
