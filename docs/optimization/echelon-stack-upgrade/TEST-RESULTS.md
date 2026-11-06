# Executed checks and outcomes

**Environment:**

- Linux x64 cloud container
- .NET SDK 10.0.402 (dotnet-install)
- Node 22.23.4
- Playwright 1.63.0 with Chromium 1243

Firefox and WebKit are **not installed**, so every browser result is Chromium only. A skipped or unavailable check is listed as NOT RUN, never as passed. Times are wall-clock.

## Stage A: static and package checks

| Check | Command | Outcome | Time |
|---|---|---|---|
| Dependency lock generation | `stack-drift.fsx lock --source lock/dependency-lock.source.json --registry … --registry-commit 5752402` | Written, no problems. Re-running gives byte-identical output | ~5 s |
| Lock generator equals the frozen v4 algorithm | `stack-drift.fsx test --registry …` | 30/30 PASS | 5 s |
| Provenance (registry, artifacts, Conditor, installed tools) | DP-01 | 44 PASS; 2 DRIFT = CA-01 | 8 s |
| Artifact digests (7 downloads: Forma, Limen, Limen.Contract, Folio, Aegis, Dokimos nupkg, Ordo nupkg) | DP-01 `--fetch` | All equal to the registry | in DP-01 |
| Forma 0.12.0 and 0.13.0 on GitHub and npm | `curl` + `sha256sum` | Identical tarballs per version. 0.12.0 `b824568d…`, 0.13.0 `2f9ceeb6…` (= registry) | — |
| Requirements compiler build | `dotnet build tools/requirements-compiler/tests -c Release` | 0 errors | 22 s |
| Offline suite (includes the F#-only tooling rule) | `dotnet run -c Release --project tools/requirements-compiler/tests -- --offline --source .` | "all passed", exit 0 ([evidence/offline-suite.log](evidence/offline-suite.log)) | 102 s |
| v4.1.5 compile check and bootstrap onto the Conditor 0.10.20 scaffold | `requirements-compiler v41 … --package requirements-compiled/v4.1.5 --check --bootstrap` | 201/202 files. FAIL is limited to the 7 pending owner approvals (X12 ×2, X17 ×5), the same set CI's `pending415` accepts | 1 s |
| F# compilation, warnings as errors | `dotnet build Causa.slnx -c Release`; `dotnet build App.slnx -c Release` | 0 warnings, 0 errors each | 26 s; 7 s |
| Convention checks on the scaffold | `conventions.fsx check --workspace` | CK-04 (Forma ≥ 0.13.0) passes; CK-03: 19 steps without pipefail (CA-02) | ~5 s |

## Stage B: focused tests

| Check | Outcome | Time |
|---|---|---|
| Forma LRP-001 `layout-reliability.spec.mjs` (Forma v0.13.0 tests) on the **published 0.12.0** `dist/` | 10 passed, **16 failed**, 1 skipped (`test.fixme`: the narrowed-SVG limitation Forma documents) | 20 s (with Causa spec) |
| Same on the **published 0.13.0** `dist/` | **26 passed**, 0 failed, 1 skipped | 15 s (with Causa spec) |
| Forma `causa-representative.spec.mjs` on 0.12.0 / 0.13.0 | 8/10 (command-palette overlay and narrow phone at 200% fail) / **10/10**. On 0.13.0, the iPad-portrait 200% case passes *because* `.ef-dashboard-grid`, `.ef-split-pane` and `.ef-operation-status` still fail as Forma's spec asserts | (above) |
| v4.1.5 layout contract (`layout-tests/run.mjs`) on Forma 0.12.0 with the 0.12.0 inventory | 1,093 positive, 411 negative, 21 Forma-gap runs, 0 failed | 190 s |
| Same on Forma 0.13.0 with the 0.12.0 inventory | 0 failed | 186 s |
| Same on Forma 0.13.0 with `verification/design-contract/forma-0.13.0-inventory.json` | 0 failed | 183 s |
| Inventory comparison 0.12.0 → 0.13.0 | 352/352 properties, 32/32 hooks, 524/524 classes, 168/168 patterns, 6/6 breakpoints. Nothing added or removed | — |
| Forma 0.13.0 `TokenValidator` on SDK 10.0.402 (unexecuted in Forma's own report) | Built and ran: 0 errors on the preview CSS. Negative control (`--ef-color-does-not-exist`): 1 error, exit 1 | 7 s |
| Unit and contract tests of the v4.1.5 skeleton | **NOT RUN as tests:** `dotnet test Causa.slnx` exits 0 with "No test is available". The test projects are filled at kickoff | 2 s |
| Limen startup smoke | See Stage C | — |
| Negative controls | stack-drift DP-06..DP-09, plus 25 negative test cases | all fail closed |

The PR #141 claim of "48 of 78" corresponds to Forma's suite running in 3 browsers × 26 tests. This audit reproduced the Chromium third: **16 of 26 failed on 0.12.0, 26 of 26 passed on 0.13.0**. Firefox and WebKit were not run here.

## Stage C: integration

| Check | Outcome | Time |
|---|---|---|
| Conditor install (lock digest) | PASS | 9 s |
| Workstation apply (echelon-current 1.37.0) | PASS; 6 tools installed; `--version` of each equals the lock | 20 s |
| `conditor init --preset indy-init` | 110/110 actions, exit 0 (after unsetting the sandbox's session `GH_TOKEN`, CA-05) | 82 s |
| Generated application build | Causa.slnx and App.slnx, 0/0 | see Stage A |
| Browser startup: F# WASM on Limen 0.9.1 with Forma 0.13.0 (scratch copy of `preview/causa`) | `build.fsx` exit 0; page rendered by the F# engine; no console errors | 55 s build |
| Screen rendering, domain/UI bindings, real historical data | `read-path.mjs` against the real causa-data: **25/25** (1,315 events decoded; replay equals the catalog digest; routing; 390 px no overflow) | 10 s |
| Persistence write and read-back (in-process GitHub double) | `Engine.Tests` **31/31** | 16 s |
| Architecture constraint | `ArchitectureCheck.fsx`: PASS | <5 s |
| Real GitHub write and read-back | **NOT RUN**: owner token required (`real-provider.mjs`) | — |

## Stage D: full candidate validation

| Check | Outcome |
|---|---|
| Required CI workflows on this branch | Pending: the pull request's CI is authoritative and is reported there |
| Dokimos acceptance, Praxis traceability on a candidate | **NOT RUN**: no application candidate exists before kickoff |
| Mobile and browser acceptance (`verification/` journeys) | **NOT RUN** here (WebKit is absent). Run by `preflight-tooling.yml` r10-adversarial in CI |
| Final candidate provenance | **NOT RUN**: no candidate |
| No production test hooks or secrets | PASS (inspected): this change adds documentation, a lock proposal and an F# tool; no application code, credentials or workflow changes |
