# Independent review of v4.1.4, round 1

**Verdict: FAIL** (commit bbaa4960d21bd07a67be91ada5881e08a4c57c9c). One major finding stands (RV414-1-1). It is inherited from v4.1.2, but it defeats what the amendment says it does. Apart from that, the amendment is sound and changes only what it should.

Reviewer: an agent with a fresh context, spawned by the implementing session. It implemented nothing. The checkout was read-only and `git status` stayed clean throughout.

## Checks run

| # | Check | Result |
|---|---|---|
| 1 | `dotnet build tools/requirements-compiler/src -c Release` (SDK 10.0.402) | 0 warnings, 0 errors |
| 1 | `v41 --source . --package requirements-compiled/v4.1.4 --base requirements-compiled/v4 --check` | exit 0, 21 × PASS (X01–X21), "v4.1.4 validation: PASS; status: CANDIDATE" |
| 1 | the same with `--package requirements-compiled/v4.1.2` and `v4.1.1` | exit 0 each, X01–X20 PASS. Exit 0 means no generated file differs (exit 4 would) |
| 1 | `git diff --stat indy-init-requirements-v4.1.2 HEAD -- requirements-compiled/v4.1.2` | empty. `sha256(v4.1.2/package-manifest.json)` = 1977780f…683a = `amends.manifestSha256` = APR-V412-OWNER-3 |
| 1 | offline suite `dotnet run --project tools/requirements-compiler/tests -- --offline --source .` | exit 0, "all passed", including the v4.1.4 owner-record check |
| 2 | `diff -rq v4.1.2 v4.1.4` | Differing files: AGENT-RULES, FREEZE, README, STARTING-STATE, amendment.json, execution-dag, schedule, validation-report, package-manifest, owner-decision-requests, verification-execution-plan. Present in one package only: the AMENDMENT file and the independent review. contracts/, layout-tests/, state-machine-contracts, agent-file-ownership, interface specs, optimization-report: identical |
| 2 | `diff` of the DAG, schedule, validation-report, AGENT-RULES and STARTING-STATE | Only the 4.1.2→4.1.4 version strings change, plus the X21 row in validation-report |
| 2 | verification plan: textual diff, and JSON equality with every `runner` removed | 6 changed lines, all `runner`. With `runner` removed the JSON is equal. Same 11 suites and 804 tests in the same order |
| 2 | owner-decision-requests: per-request JSON equality | Every carried request is equal. The only changes are the added ODR-V414-01..03 and `status` (the text diff is §/— re-escaping) |
| 3 | simulation of filter semantics (FQN contains, project targeting) on every dotnet plan test | 419 tests, each selected by exactly its own suite's runner. 0 mismatches |
| 3 | project and path selection on every playwright plan test | 373 tests, each selected by exactly one runner. 0 mismatches |
| 3 | `V41Package.suites` matchers compared with the new filters | SU-OWN-DOTNET: Contract/Domain/Persistence/App `.Tests.`. SU-DOTNET-LIVE: `Acceptance.Live.`/`Invariants.Live.`. Both match the matchers |
| 4 | approvals.causa.json APR-V414-OWNER-1 | operator kemiller2002, 2026-11-04. ODR-V414-01 select A, ODR-V414-02 select A. Commit 8b1d628 by Kevin Miller (author and committer), no agent trailer |
| 4 | `git diff 8b1d628 bbaa496 -- requirements-compiled/v4.1.4` | Only `approval` fields were added (runners unchanged), plus the manifest digest. The requests file is byte-identical (c7fb2af5…) |
| 4 | option A against `applies` | -01: SU-OWN-DOTNET and SU-DOTNET-LIVE. -02: the four browser suites and the bootstrap's playwright.config.ts. This equals `suiteRunners` and the bootstrap |
| 5 | `v41 … --bootstrap <empty dir>` | exit 0. Writes playwright.config.ts with `chromium` (Desktop Chrome) and `webkit` (Desktop Safari), testDir `./tests/browser`. `dotnet build Causa.slnx` succeeds |
| 5 | bootstrap of v4.1.2 compared with v4.1.4 (`diff -r -x bin -x obj`) | Only in v4.1.4: playwright.config.ts |
| 5 | the old v4.1.2 runner form `dotnet test tests/A tests/B` | `MSB1008`, exit 1, which reproduces the defect |
| 5 | npm install @playwright/test 1.63.0 with probe specs, then `npx playwright test --config playwright.config.ts <runner args> --list` for each of the four runners | Each exits 0 and lists exactly its folder(s) under the right project (webkit included) |
| 5 | the two corrected dotnet runners on the bootstrap, then again with one probe `[<Fact>]` added (alone, and layered onto the recorded Conditor 0.10.18 fixture) | exit 0, but **every test assembly is skipped**. See RV414-1-1 |

Not checked: I did not re-run suite-preflight against the Rehearsal 009 application, because it is not in this environment. I did not run WebKit browsers, which are not installed here; listing worked. I did not re-run the contract typecheck or the negative controls, because contracts/ is byte-identical to v4.1.2's.

## Findings

| id | severity | finding |
|---|---|---|
| RV414-1-1 | **major** | On the package's own bootstrap, every .NET runner exits 0 while running no test. The bootstrapped test projects use central package management, so the F# SDK sets `DisableImplicitFSharpCoreReference=true`. The locked tooling has no FSharp.Core, so `FSharp.Core.dll` is never deployed, and xunit prints `Skipping: Causa.Contract.Tests (could not find dependent assembly 'FSharp.Core, Version=10.1.0')` for all six projects. With `Causa.slnx` and a filter, each project says "No test matches", and the run still exits 0. Adding `FSharp.Core` 10.1.402 to Directory.Packages.props and to the project made the probe test pass. X21 is static. CI's bootstrap steps only build. The suite-commands job and `r9.fsx suite-fixture` use hand-written, non-CPM projects with implicit FSharp.Core, so nothing catches the problem. It is inherited from v4.1.2, whose bootstrap differs from v4.1.4's only by playwright.config.ts. Fixing it adds a package, and AGENT-RULES says that needs an owner decision. Rehearsal 009 evidently had FSharp.Core, since 7+7 tests ran, but I could not tell from the repository how. |
| RV414-1-2 | minor | The requests file's `status` still says "Pending: ODR-V414-01 and ODR-V414-02", although APR-V414-OWNER-1 records both. README and FREEZE do not say step 1 is done. ODR-V414-01/-02 have no `approvalRecord.how`. |
| RV414-1-3 | minor | optimization-report.md is carried byte for byte. Its banner covers "v4.1.1 and v4.1.2", it links `AMENDMENT-4.1.2.md` (broken inside v4.1.4), and it links `independent-review.md` (not present yet). Every other document refers to v4.1.4 consistently, and none claims approval or freeze. |
| RV414-1-4 | minor | X21's "names only projects it defines" is tautological: `defined` = the projects the runners name. X21 does not check the filter-to-matcher equivalence, or `applies` against the suite. The owner-record test checks the decision id, not option A. I verified all three by hand, and they hold. |
| RV414-1-5 | minor | playwright.config.ts has three risks. It hard-codes `/opt/pw-browsers/chromium`. One config with a local `python3 -m http.server` webServer and baseURL 127.0.0.1 also serves SU-BROWSER-LIVE, whose fixture is the deployed candidate. Conditor's chromium-only `playwright.config.js` stays beside it with no owner, and the recorded Conditor fixture lacks that file, so CI never tests the two configs together. |

## To pass
- RV414-1-1: an owner decision adding FSharp.Core to the locked test tooling, or equivalent (for example, the bootstrap writes `<PackageVersion Include="FSharp.Core" …/>`). Then a CI step that runs at least one probe test through the corrected runners on the bootstrapped tree, failing on zero executed tests.
- Fix the minor findings, or accept them explicitly.
