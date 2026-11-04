# Integrating #102 (process corrections) with #101 (release-gate repair)

> **Integrated (2026-11-04, #104).** This is #102's record and is kept as written. In the integrated tree, v4.1.3 is replaced by #101's v4.1.4. The scope manifest is recompiled from v4.1.4 with the same 199 requirements, 863 obligations and 35 work items; its scopeSha256 is now `b407b5cc…`. The current readiness state is [FINAL-READINESS.md](FINAL-READINESS.md).

Two agents worked on overlapping parts of the same problem at the same time:
- #102: branch `claude/rehearsal-009-early-stop-hcy044`;
- #101: branch `claude/causa-investigation-r9-6l5isz`.

Neither PR merges until both are reviewed together and the combined integration tests pass. This is a proposal for that review; nothing here changes #101.

## Ownership

| Area | Owner | #102 content | #101 content |
|---|---|---|---|
| Praxis sequencing (nothing implemented before the inventory) | process corrections (#102) | `scope checkpoint`, `scope order` | `r9.fsx authorize-implementation`, `check-sequencing` |
| Continuation, early-stop validation, recovery | process corrections (#102) | `Progress.fs`, `Continuation.fs` (`progress`, `stop-decision`, `resume`), the adversarial cases in `ScopeTests.fs` | none |
| Unified requirements scope | scope/release (#101) | `Scope.fs`, `scope-manifest.json` (199 requirements, 863 obligations, 35 work items) | `obligation-inventory.json` (613 obligations), `competition-scope.proposed.json`, `acceptance-status` |
| Test-command compatibility | scope/release (#101) | v4.1.3, X21, `suiteRunners`, `r9.fsx suite-preflight` and `suite-fixture`, CI job `suite-commands` | v4.1.4, X21, `suiteRunners`, bootstrap writes `playwright.config.ts`, `r9.fsx suite-preflight`, CI job `release-gate-repair` |
| Dokimos integration | scope/release (#101) | dokimos#67 (**merged** before coordination began) | dokimos#68 (the same fix) |

## Proposed resolution

1. **Test commands: take #101.**
   - Keep v4.1.4 and its X21 and `suiteRunners` (`V41Package.fs`), its `suite-preflight`, its bootstrap `playwright.config.ts`, and its gate workflow.
   - Drop from #102: `requirements-compiled/v4.1.3/`, the `V41Package.fs` changes, `r9.fsx suite-preflight`/`suite-fixture`/`govern --lock`, the `verification-gate.yml` changes, the CI job `suite-commands`, and the `owner-freeze.fsx` v4.1.3 entry.
   - Before dropping, check that #101 covers what #102 tested. #101 also writes `playwright.config.ts`, which #102 does not. Two things in #102 may be worth porting into #101 if the scope/release owner agrees: the `--require-tests` check (a command that exits 0 but selects no test is refused) and the deterministic fixture.
2. **Dokimos.** dokimos#67 is merged. The scope/release owner decides whether dokimos#68 closes as a duplicate or rebases its extra content onto `main`.
3. **Scope.** The scope/release owner chooses one artifact.
   - #102's `scope-manifest.json` has the full release scope: P0 plan + catalog, work items, producers, hashes and scope hash.
   - #101's `obligation-inventory.json` reconciles obligations with the Rehearsal 009 gate.

   The process-corrections code needs only the **contract** below, not a particular file.
4. **Sequencing.** Keep one mechanism. Either form must refuse application code before a complete inventory has been recorded.
   - #102 `scope checkpoint`/`order` checks the Praxis backlog against every work item in the scope.
   - #101 `authorize-implementation`/`check-sequencing` checks the catalog, obligations and suite preflight.

   Proposal: `check-sequencing` stays the CI-facing command, and requires the scope checkpoint's `scopeSha256` and Praxis coverage as inputs.

## The contract continuation needs from the scope artifact

`Progress` and `Continuation` read only these fields, so they work with whichever scope artifact the scope/release owner keeps, as long as it provides them:

```
scopeSha256
requirements[]: id, origin ("v4-plan" | "catalog"), mandatory, applicability ("always" | "fact=value and ..."),
                workItems[], obligations[]: id, mandatory, producers[]
workItems[]:    id, dependsOn[], requirements[]
```

## Combined integration test (to run on the merged tree)

1. Run the offline suite (`dotnet run --project tools/requirements-compiler/tests -- --offline --source .`). It includes the #102 cases: adversarial, legitimate stops, recovery, the real scope and the Rehearsal 009 replay.
2. Run #101's harness: `dotnet fsi tools/release-gate-repair/tests.fsx …`.
3. Run `suite-preflight` on the chosen package: v4.1.2 must be refused and the successor must pass, WebKit included, in CI.
4. Run `stop-decision` and `resume` on the Rehearsal 009 replay against the chosen scope:
   - `CONTINUE_REQUIRED` at 115 minutes, with `CATALOG_IS_NOT_SCOPE`;
   - `CONTINUE` on resume.
