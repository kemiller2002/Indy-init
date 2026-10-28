<!-- Investigation note written 2026-10-28 by a read-only investigation agent for the Echelon Engineering Improvement Initiative (Phase One). Local paths refer to that session's clones. Numbers marked as recomputed in ../BASELINE-2026-10-28.md were re-derived independently. -->

# Inventory: Workstream E (reproducible browser test environments) and Workstream F (Forma layout)

This was a read-only inventory. Clones are under `/home/user/w/`. R5 means `indy-init-rehearsal-005`, which I only read locally. Line numbers are taken from the current HEAD of each clone:
- conditor `fdbcb6b`
- dokimos `49a92b8`
- percepta `c9747c2`
- limen `ddfc1e8`
- forma `e0b088d` (0.10.0)
- forma-studio `904d3b4`
- R5 `e086147`

---

## Workstream E: reproducible browser test environments

### E1. What the three R5 commits fixed and why

| Commit | What broke | Root cause |
|---|---|---|
| `ec6407b` (T28) → `a0e7d4a` | Worktrees on one machine collided on 4322. `ec6407b` added `CAUSA_PORT`/`CAUSA_TEST_PORT`. `a0e7d4a` then moved the defaults to 4821/4822 in `playwright.config.js` only. | The port has no single source. The config changed and the specs did not follow. |
| `2e7599e` | `graph-navigation.spec.js` and `fixture-mode.spec.js` still hardcoded `http://127.0.0.1:4322/`. Main run 40599949953 failed with `ERR_CONNECTION_REFUSED`. Separately, `scripts/serve.mjs` crashed on an undecodable path (`decodeURIComponent` throws), so every later test on that port got "connection refused". | The test-site origin was copied per spec. The static server is a hand-rolled script with no robustness contract. |
| `b6238ad` | `fixture-mode.spec.js` env fallback and its doc comment still said 4322. | Same duplication. The first fix missed one copy. |
| `979c465` | Browser, live and percepta jobs each re-ran `npm run build:test`. After `a0e7d4a` the test build became its own .NET publish (`-p:CausaTestBuild=true`), and those jobs have no .NET SDK. The site job now uploads `obj/site-test` as artifact `site-test`, and the browser jobs download it. | Nothing declares which artifact a browser job must test. Nothing checks the served bytes against what was built. |

### E2. How R5 configures endpoints, builds, browsers and fixtures today

- **`playwright.config.js`**
  - :10-12: `CAUSA_PORT ?? 4821` and `CAUSA_TEST_PORT ?? 4822`.
  - :13: `CAUSA_BASE_URL` points the run at a deployed site instead.
  - :16-18: Chromium path. `CAUSA_CHROMIUM_PATH` is used, otherwise `/opt/pw-browsers/chromium` if it exists.
  - :39-42: two `webServer`s run `node scripts/serve.mjs dist|obj/site-test PORT`. Readiness is only `index.html` answering 200.
  - `reuseExistingServer: !process.env.CI`. Locally, a server already on the port is reused even if another worktree started it with another build. Nothing checks build identity.
- **Specs.** Each spec redefines the test-site origin: `const SITE/TEST_SITE = \`http://127.0.0.1:${process.env.CAUSA_TEST_PORT ?? 4822}/\``. The copies are in:
  - `history.spec.js:5`
  - `fixture-mode.spec.js:10` (plus the comment at :5)
  - `graph-navigation.spec.js:6`
  - `forms.spec.js:5`
  - `proposals.spec.js:6`
  - `shell.spec.js:7`
  - `start-smoke.spec.js:5`
  - `identity.spec.js:7`
  - `percepta.spec.js:13` (inside the `percepta verify-app --base-url` argv)
- **Network guard.** `tests/browser/support/fixtures.js:7` allows any `127.0.0.1|localhost` port. A test that reaches the wrong local server is not caught.
- **`scripts/serve.mjs`.** Its default port is 4321 (:7) and the `package.json:10` `"serve"` script also uses 4321. Neither matches the config's 4821. The decode guard was added in `2e7599e` (:11-18).
- **`tools/acceptance/ci/fixture-ui.sh:7`.** `port="${1:-4822}"`, a separate copy that does not read `CAUSA_TEST_PORT`.
- **`.github/workflows/build-and-test.yml`**
  - :336-345 uploads `site-test`.
  - :384-389 and :508-513 download it.
  - :399-429 handle browsers: the version comes from `playwright-core`, WebKit is chosen by grepping `playwright.config.js`, the cache is keyed by that version, and `ldd` proves the OS libraries.
  - The percepta job :755-762 serves `obj/site-test` on a hardcoded **4330**, three times. It polls with a hand-written curl loop that does not fail if the server never answers. It then runs `percepta verify-app --base-url http://127.0.0.1:4330/`.
  - Stale comments say "4322" (:337, :507).
- **Stale docs.** `src/persistence/Fixture.fs:15` and `README.md:8` still say 4322.
- **Build identity.**
  - `scripts/build-site.mjs:70-82` writes `build-info.json` with `commit`, `builtAt`, `configuration` (test or production) and component versions. It has no content digest.
  - The only byte-for-byte served-vs-built check is for the deployed site: `tools/acceptance/deployed/deployed.spec.js:20-35`.
  - Local and CI test servers have no such check.
- **Fixtures.**
  - `.percepta/bindings.json` names state fixtures in `.percepta/fixtures/*.json` (16 references).
  - Browser fixtures are in `tests/browser/graph-fixtures/` and `support/fake-github.js`.
  - Nothing checks that they exist or what their digests are before a run.
- **Diagnostics are spread across:**
  - `TestResults/playwright/browser-results.json` (`PLAYWRIGHT_JSON_OUTPUT_FILE`)
  - `test-results/` (traces, uploaded as `playwright-traces`)
  - `TestResults/percepta/{verify-app.json,verify-app.log,exit-code,percepta-version.txt}`
  - `.percepta/evidence/application/<screen>/screenshots`
- **Evidence reuse.** `tools/acceptance/ci/fingerprint.sh` hashes the tracked inputs for Dokimos reuse. Ports, server config and browser revision are not salted in unless someone passes them by hand.

**How many places duplicate the port.** At HEAD the test-site port is defined in 11 executable places across 11 files:
- the config
- 9 specs
- `fixture-ui.sh`

Another 5 places are comments or docs: fixture-mode, Fixture.fs, README, and two workflow comments, four of them stale at 4322. A third, unrelated literal (4330) appears three times in the workflow. The production port is inconsistent across three places: config 4821, serve.mjs 4321, package.json 4321.

### E3. What the platform repos already provide

**Conditor (bootstrap): the origin of R5's setup**
- `src/Conditor.Core/Scaffolding.fs:1391-1426` (`webPlaywrightConfig`) is the scaffolded `playwright.config.js` that R5 grew from. It has:
  - `const port = 4321` (:1400)
  - the `/opt/pw-browsers/chromium` fallback (:1406-1407)
  - `python3 -m http.server` (:1420)
- :1428-1455 is `webSmokeSpec`, which checks that every resource loads with no script error.
- :1512-1567 is the CI browser install with no apt hang: Playwright version → cache key → `npx playwright install chromium` → `ldd` missing-library check → bounded `install-deps`. This is the strongest browser-installation verification in the portfolio, and R5 copied it (`build-and-test.yml:399-429`).
- :1381 says scaffold files are `preserved-existing`, meaning they are written once and never upgraded. Every consumer forks the config.
- `src/Conditor.Core/Doctor.fs:66-104` (`lifecycleDoctorFindings`) runs each component's `doctorArguments` (for example `components/percepta.component.json` → `percepta-repo doctor`). It aggregates them into `COND-DOC-*` findings with remediation text, under `schemas/conditor-doctor.schema.json`.
- `conditor doctor [--json]` is at `src/Conditor.Cli/Program.fs:21,1870`.
- Requirement CON-037 (`requirements/CONDITOR-REQUIREMENTS.md:46`) already separates "installation verification" from "execution-readiness verification".
- **Missing:** a test-environment schema, port allocation, a browser launch check, artifact identity and fixture checks.

**Dokimos (verification evidence)**
- `schemas/dokimos-evidence-reuse-request.schema.json:28-38` holds per-test `Inputs` with `Configuration` (a string map) and `ToolVersion` (example `"dotnet-sdk …; playwright 1.48.2"`). `$defs/inputs` (:66-75) records them in provenance.
- `docs/evidence-reuse.md` covers the R4 motivation.
- This is the natural consumer of a test-environment digest: it would go into `Configuration` or `Dependencies`.
- `schemas/dokimos-diagnostic.schema.json` is a typed stderr diagnostic.
- **Missing:** ports, servers, browsers and a preflight. `dokimos doctor` only covers the installation (`src/Dokimos.Cli/Program.fs:460,491`).

**Percepta (UI contracts)**
- `percepta doctor` (`src/Percepta.Cli/Program.fs:870-901`) validates contracts. It also calls `BrowserVerification.doctor()` (`src/Percepta.Adapter.Playwright/BrowserVerification.fs:764-774`), which just launches headless Chromium and exits 4 on failure.
- `percepta install-browser` (:776-777) runs `playwright install --with-deps chromium`, the apt path that Conditor's scaffold avoids. R5 wraps it in `timeout 600`.
- The doctor does not check the browser revision. Evidence: `artifacts/percepta/experiment-0004/environment.json` records expected revision 1243 but Chromium 141 / revision 1194 actually used, and the doctor still reported "Browser OK".
- `verify-app --base-url` (:582-604) requires a served URL. Its report (`writeApplicationReport`, :500-520) records `baseUrl`, `complete`, `counts` and `orphanBindings`, but no digest of the served artifact.
- By contrast, `percepta verify` (evidence contract 1.0.0, `docs/PERCEPTA-SEMANTIC-CLI.md:108-110,150-153`; `docs/PERCEPTA-EVIDENCE-CONTRACT.md:11-54`) deliberately rejects HTTP targets. It hashes the target, contract and fixture bytes as `inputs`. This is the portfolio's existing artifact-identity and fixture-identity model, but it does not cover `verify-app`.
- Evidence goes to `.percepta/evidence/application/<screen>/` (Program.fs:631).
- `docs/SCREEN-BINDING.md:40-53` covers fixture binding.

**Limen (F# WASM runtime)**
- `limen doctor`/`verify` (`cli/Limen.Core/Diagnose.fs`, `cli/Limen.Cli/Help.fs:23-25,169-192`) diagnose the installation only.
- `scripts/browser-smoke.ts:26-27` hardcodes `PORT = 4179`.
- `scripts/smoke-packs.ts:13` has `LIMEN_REQUIRE_BROWSER=1`, which turns "no browser" from a skip into a failure. That is a useful precedent.
- CI (`.github/workflows/ci.yml:166,213`) uses `playwright install --with-deps`.
- **Missing:** a test-environment contract.

**Other evidence of fragmentation**
- forma `playwright.config.mjs` uses `127.0.0.1:4173` (`tools/serve-tests.mjs:6`, `PORT`) and has no executable override.
- forma-studio `playwright.config.mjs` uses ports 4380/4381/4382 with `reuseExistingServer: true` even in CI. Its Chromium override is `PLAYWRIGHT_CHROMIUM_EXECUTABLE`.
- The browser override is named three ways: R5 `CAUSA_CHROMIUM_PATH`, studio `PLAYWRIGHT_CHROMIUM_EXECUTABLE`, Percepta `PLAYWRIGHT_BROWSERS_PATH`. Conditor uses a hardcoded `/opt/pw-browsers`.
- The diagnostics directory differs too: `test-results/`, `TestResults/…`, `playwright-evidence/results.json`.

### E4. Best home and the smallest change

**Home: Conditor.** It generated the config every app forks (`Scaffolding.fs:1391`). It already owns the hang-proof browser install with `ldd` verification (:1512-1567), it already aggregates doctors (`Doctor.fs`), and it publishes schemas (`schemas/`). CON-037 already names "execution-readiness" as a separate gate. The other repos fit as follows:
- Dokimos consumes the result: the env digest goes into `Inputs.Configuration`.
- Percepta is a consumer of the served URL. Its `verify` input-hashing model is the template for artifact identity.
- Limen has nothing relevant.

**Smallest change: one declared file, one module, one preflight.**
1. **Conditor: new `schemas/conditor-test-environment.schema.json`** (`conditor.test-environment/1`), placed at `test-environment.json` in the app. It declares:
   - `servers[]`: `id`, `root`, `portEnv`, `defaultPort`, `readyPath`, `identity: "build-info.json"`
   - `browsers[]`: `engine`, `executableEnv`, `preinstalledPath`
   - `fixtures[]` (globs)
   - `diagnosticsDir`
2. **Conditor scaffold (`Scaffolding.fs`, `fsharp-limen-web`)** emits:
   - `tests/browser/support/environment.js`, the only reader of that file plus env, exporting `origin(id)`;
   - a `playwright.config.js` that imports it;
   - a Node static server with the decode guard and an identity header;
   - `tools/test-env/preflight.mjs`. Its checks:
     - each port is free, or the server on it answers with the expected build digest;
     - each server root exists and its `build-info.json` content digest matches the files;
     - each declared browser launches, the `ldd` check is clean, and its revision matches the installed Playwright;
     - each fixture exists, with digests recorded.
   - The preflight writes `diagnosticsDir/preflight.json`.
3. **Later:** `conditor doctor` gets a `COND-DOC-TESTENV` finding that runs the same checks. `Doctor.fs` needs a new `add…Findings` function next to `addStatusFindings`.

**What R5, the consumer, would change:**
- `playwright.config.js`: import origins and ports from the module.
- The 9 specs: replace the `SITE`/`TEST_SITE` constant with `import { origin } from "./support/environment.js"`.
- `scripts/serve.mjs`: the default port comes from the module.
- `package.json:10`.
- `tools/acceptance/ci/fixture-ui.sh:7`: read the port with `node -p`.
- `build-and-test.yml:755-762`: call the preflight instead of the curl loop and the 4330 literal, and run the preflight before `npx playwright test` (:439).
- `scripts/build-site.mjs:70`: add a `contentDigest` to `build-info.json`, reusing the sha256 walk from `deployed.spec.js`.
- Stale comments to fix: `Fixture.fs:15`, `README.md:8`, `build-and-test.yml:337,507`.

---

## Workstream F: Forma layout

### F1. Toolchain and tests

- **Toolchain.** Forma is mostly CSS, built with Node tooling. Tokens, brands, the marketing bundle and the CSS policy are compiled by F# tools: `tools/TokenCompiler`, `BrandCompiler`, `PresentationBundler`, `SiteCssPolicy`. Diagrams use an F# engine: `src/workflow/Forma.Workflow`, published as `@echelon-foundry/forma-workflow` 1.1.0. The answer to "CSS/JS or F#?" is both: zero-runtime CSS as the product, F# for compilers and the diagram engine.
  - `npm run build` (`package.json` scripts) runs tokens:build → brands:build → color:build → marketing:theme → runtime:build → assets:build → marketing:build.
- **Browser tests in Forma.** Playwright 1.63 runs `tests/browser/*.spec.mjs` (about 55 specs).
  - `playwright.config.mjs` runs chromium, firefox and webkit, with `node tools/serve-tests.mjs` on 4173.
  - Run them with `npm run build && npm run test:browser`, or more narrowly with `test:a11y`, `test:workflow-browser`, `test:machine`, `site:mobile` (`playwright.site.config.mjs`), or `ui:conformance` (`playwright.conformance.config.mjs`).
  - CI: `.github/workflows/conformance.yml:42-58`.
  - Layout probe: `npm run test:layout` / `npm run layout:probe` (F#, `tests/layout/Forma.Layout.Probe`, Chromium via `tests/layout/measure.mjs`).
- **Browser tests in forma-studio.** Playwright 1.63 runs `tests/browser/*.spec.mjs`: editor, html-consumer, accessibility, ui-conformance, workflow, icons.
  - Run with `npm run app:build` (dotnet publish of the F# engine to WASM plus the Limen kernel), then `npm run app:test` (`html:consumer` + `playwright test`). `ui:conformance` is `--grep @uli`.
  - The F# engine tests run with `npm run engine:test`.

### F2. Typography: tokens, roles and what R5 overrode

- **Tokens** (`tokens/echelon.tokens.json`, `primitive.font-size`):
  - Fixed rem steps: 2xs .6875, xs .75, sm .875, md 1, lg 1.25, xl 1.5, 2xl 2, 3xl 2.5.
  - `font-size-fluid`: lead, display-3 (1.5→2.4rem), display-2 (2→4.5rem), display-1 (2.5→**7.25rem**, 6vw).
  - There are no semantic typography tokens. `semantic` holds only color themes.
- **Roles.** Type roles exist only in the marketing layer, `src/marketing/roles.css:37-60`:
  - `--ef-type-size-h1: fluid-display-1`
  - `h2: fluid-display-2`
  - `h3: lg`
  - also label, small, body, lead, statement, leading, weight and tracking roles.
  - `src/marketing/foundations.css:145-146` applies them under `.ef-site` with `max-inline-size: 14ch` (h1) and `18ch` (h2).
- **Core application layer.** `src/styles/foundations.css:22-27` gives `h1, h2` the display family, weight 500 and line-height 1.02, but no size and no role variables.
- **No typography role for application, dense or accessible use exists.**
  - `data-density` (foundations.css:292-295) and `data-ef-skin="compact|comfortable|square"` (`src/styles/skins.css`) change only spacing and radius.
  - Requirement DS-TOKEN-003 (`requirements/DESIGN-SYSTEM-REQUIREMENTS.md:121-131`) asks for a "heading scale" without naming a context.
- **Why R5 is on `.ef-site`.** `src/kernel/index.html:18` has `<body class="ef-site causa">`. The application layer lacks the pieces R5 needs; these gaps are documented in `docs/COMPONENT-INVENTORY.md`:
  - `.ef-app-shell` is not implemented (:226);
  - primary and destructive button variants exist only inside `.ef-site` (:231, :282);
  - `.ef-section` rhythm is not scoped to `.ef-site` (:288).
- **R5 `f207cd4`** (`src/kernel/causa.css:342-353`):
  - It sets `--ef-type-size-h1/h2/h3` to `--ef-primitive-font-size-2xl/xl/lg` on `body.causa`.
  - It removes the 14ch/18ch measure (`max-inline-size: none`), sets line-height 1.2, and re-applies the sizes.
  - `tests/browser/identity.spec.js:143-154` asserts h1 = 2×root at 1440/1180/820/320 and at most one line (two at 320).
  - **Why:** the marketing display scale made "Connect a data repository" up to 7.25rem at 14ch on desktop. That is wrong for a tool.

### F3. Overflow, min-content, wrapping and radius versus padding

- Counts across the Forma CSS files:
  - `overflow-wrap`: 31 in `src/styles/components.css`, 6 in marketing
  - `min-inline-size: 0`: 51 in `components.css`, 10 in `foundations.css`
  - `min-content`: 4
  - `container-type`: 3
- `.ef-bounded-overflow` (`foundations.css:280`) is the sanctioned contained scroller.
- `.ef-alert` stacks on a viewport media query rather than its container (LCE-001 open limitation, `experiments/LCE-001/REPORT.md:339`).
- **Radius bounded by padding** (LCE-001, shipped in Forma **0.10.0** with forma-workflow 1.1.0, commit `6800f7b`):
  - diagram pill node: `src/styles/components.css:4644`
  - marketing button, badge and card: `src/marketing/components.css:210,273,330`. These use `min(var(--ef-shape-radius), p·2.414 − 2.414px)`, or `3.414` for the card.
  - `overflow-wrap: anywhere` on marketing buttons and badges.
  - The rule is derived from a radius × padding sweep: the safe radius is about `(2+√2)·p + border` (REPORT.md:180-198).
- Application-layer pills (`.ef-switch__track` :283, wizard step :1946, fault marker :2781) are fixed-size, so text cannot escape them.
- **R5 consequences**
  - R5 pins `@echelon-foundry/design-system` **0.9.0**, which predates the radius bounds (`package.json`, `.conditor/lock.json:1218-1221`). It does use forma-workflow 1.1.0, which includes the LCE-001 engine fixes. So R5 has the engine fixes but not the CSS fixes.
  - R5's own `.causa-tag` (`src/kernel/causa.css:175`: `padding: .1rem .5rem; border-radius: 999px; display: inline-block`) is the exact unbounded-pill pattern LCE-001 fixed in Forma. If it wraps (for example the "Fixture data: synthetic, not GitHub" tag at 320px), text crosses the curve.
- **Studio**
  - `src/engine/FormaStudio.Engine/LayoutChecks.fs` statically checks pages: `layout.text.unbreakable` → ULI-L05, alignment, names.
  - Since adopting Forma 0.10.0 it also runs `Forma.Workflow.LayoutCheck` on diagrams before export (`HtmlExport.fs:274-282`). It is advisory and never blocks.
  - Its UI conformance helpers are local copies "until Forma publishes them" (`tests/browser/ui-conformance-helpers.mjs:5-12`).

### F4. Existing checks for containment, overlap, clipping and focus

- **Percepta** (`src/Percepta.Adapter.Playwright/BrowserVerification.fs`):
  - document-level horizontal overflow only, per breakpoint (:533-539, `scrollWidth <= clientWidth + 1`);
  - required regions and observations visible per breakpoint (:511-531);
  - keyboard reachability plus a visible focus indicator per capability (:403-433);
  - a target-type check on interactive elements (:190-208).
  - **No** element-level containment, overlap, clipping or rounded-corner check.
- **Forma, consumer-facing:** `packages/conformance` (`@echelon-foundry/forma-conformance` 0.2.0, dev-only Playwright helpers for `UI-LAYOUT-INTERACTION@1.0.0`, published by `.github/workflows/publish.yml`):
  - `evaluateOverflow` (ULI-L05; text escaping the viewport or a clipping container fails even when hidden): `src/evaluate.mjs:203`
  - `evaluateLabelIntegrity` (ULI-L06, clipped or off-screen): `src/evaluate.mjs:218`
  - `measureOverflow`/`expectNoOverflow`: `src/index.mjs:343,380`
  - `scaleDefaultText` (:388)
  - `expectLabelIntact` (:412)
  - `expectDestinationClearOfHeader` (:420)
  - `recordTabSequence` (:430)
  - alignment groups (:79-144)
  - **It has no corner or radius check.**
- **Forma, internal:** `tests/layout/Forma.Layout.Probe` (F#), with constraints `contain.overflow`, `contain.clipped`, `padding.preserved`, `boundary.corner`, `boundary.clearance`, `allocation.exceeded`, `separation.nodes`, connector and label overlap, `canvas.containment`, `nest.containment`, `viewport.overflow`, `a11y.min-text`, `a11y.target-size` and `determinism` (`tests/layout/README.md:40-44`).
  - Shape maths is in `src/workflow/Forma.Workflow/LayoutGeometry.fs`. Static diagram validation is in `LayoutCheck.fs`, with codes `layout.node.too-small/too-narrow/overlap`, `connector.crosses-node`, `label.overlap` and `canvas.too-small`.
  - The rendered `boundary.corner` check is not exported to consumers.

### F5. Constraint-driven layout and physics research

- **RLP-001** (`experiments/RLP-001/REPORT.md`, EX-FORMA-2026-0001/0002). 144 agent attempts were made. Outcome: "relationships win, physics does not."
  - H1 is not supported (A−D 0.47 defects, CI [−0.19, 1.11]).
  - Physics added nothing over deterministic rules and broke the 50 ms budget (p95 105.5 ms).
  - The relational vocabulary cut layout decisions from 31.4 to 12.8 and held-out defects from 2.56 to about 0.18.
  - Blind visual review preferred plain CSS.
  - It yielded usable evidence, but it argues against physics.
- **RLP-002** (`experiments/RLP-002/REPORT.md`, EX-FORMA-2026-0003). The vocabulary compiles to zero-runtime CSS: a 460-line F# compiler producing 7.3 KB of CSS, with 0 px difference when JS is disabled.
  - Result: **REFINE**. `flow=row` cannot compile exactly, so the gate failed (8 unexpected-shift and 9 reading-order defects added).
  - Not shipped.
- **LCE-001** (`experiments/LCE-001/REPORT.md`, EX-FORMA-2026-0004). This is the usable one, and it shipped in 0.10.0.
  - An adversarial probe found 113 of 500 Forma-attributable failures at baseline and 4 after the corrections.
  - All 15 Causa diagram variants were fixed.
  - Remaining limits:
    - static checks cannot guarantee the rendered result;
    - `.ef-alert` in narrow columns;
    - only Chromium was measured;
    - Limen `measure` does not wait for `document.fonts.ready` (REPORT.md:296-306).
- The physics-motion and surface-physics specs and workflows are about motion and are unrelated to layout.

### F6. Smallest change for F

**In Forma: one application type role.** Files to touch:
- `src/marketing/roles.css`, or better a core scope in `src/styles/foundations.css`: add `[data-ef-type="application"]` (name to be decided), which maps `--ef-type-size-h1/h2/h3` to `--ef-primitive-font-size-2xl/xl/lg`.
- `src/marketing/foundations.css:145-146`: under that scope, lift the 14ch/18ch `max-inline-size` and use the snug leading.
- A browser test in `tests/browser/marketing.spec.mjs`, or a new spec, asserting the fixed steps at 320, 820, 1180 and 1440.
- `docs/CONSUMING-FORMA.md` and the `CHANGELOG`.

This promotes R5's override into Forma. It is the first step toward the missing roles (application, dense, accessible), with `.ef-app-shell` as the longer-term fix.

**Consumer, R5 (later, not touched now):**
- Add the attribute at `src/kernel/index.html:18`.
- Delete `src/kernel/causa.css:342-353`.
- Keep `identity.spec.js:143-154` as the consumer check.
- Bump `@echelon-foundry/design-system` to 0.10.0 to get the LCE-001 radius bounds.
- Replace `.causa-tag` (causa.css:175) with `.ef-badge` or the bounded-radius formula.

**Optional containment follow-up:**
- Add a `boundary.corner` evaluator to `packages/conformance/src/evaluate.mjs` and `index.mjs`, ported from `Forma.Layout.Probe` `Constraints.fs` and `LayoutGeometry.fs`.
- Consumers then call it in `@responsive` tests.
- Percepta could import the same rule later. Today it checks only page-level overflow.
