# Integration of #101 (release-gate repair) and #102 (process corrections)

**Status: combined tests pass locally; awaiting the combined CI run and the integration review. Not merged.**

#102's branch (`claude/rehearsal-009-early-stop-hcy044`) now contains #101's branch (`claude/causa-investigation-r9-6l5isz` at `0d816e3`), merged with a merge commit. Nothing on #101's branch was rewritten. Merging #102 therefore merges both, or #101 can merge first and #102 then adds only its own changes.

## Owner decisions applied

| # | Decision | Applied as |
|---|---|---|
| 1 | Use #101's v4.1.4; port #102's zero-tests detection and fixture; drop v4.1.3 | `requirements-compiled/v4.1.3/` is removed. v4.1.4, #101's X21, `suiteRunners`, `suite-preflight` and the bootstrap `playwright.config.ts` are kept unchanged: `V41Package.fs`, `owner-freeze.fsx` and the v4.1.4 package are #101's versions. The ports into `tools/r9/r9.fsx` are listed below the table. |
| 2 | #102's `scope-manifest.json` is the authoritative full-release scope; #101's reconciliation is an independent check | The manifest is recompiled on v4.1.4: 199 requirements, 863 obligations, 36 work items, frozen hashes, `scopeSha256`. A new offline test (`ScopeTests.reconciliation`) requires the manifest and `docs/release-gate-repair/obligation-inventory.json` to agree on all 613 plan obligations, their requirements and their evidence kind (72 record-only, 541 executable or mixed), with nothing excluded. Praxis coverage is kept. |
| 3 | `check-sequencing` stays CI-facing and requires the scope checkpoint | `r9.fsx check-sequencing --scope-manifest F` additionally requires the committed, immutable `.echelon/scope-checkpoint.json`, with the manifest's `scopeSha256` and every work item in Praxis at that commit. Praxis means backlog items `[ID] …` or by id, and work items begun with `praxis work begin`. Implementation counts as authorized only from the later of the two checkpoints. The gate passes `--scope-manifest` whenever the pinned lock names a scope. Without it, the command behaves exactly as #101 wrote it. #102's own `scope order` is removed. |
| 4 | Close dokimos#68 if nothing unique | Compared with merged #67: the same `componentMatches` case (`input` of type url, text or empty). Its extra negative test values and doc note change no outcome. Closed with a comment. |

**Decision 1 ports:**
- `--require-tests true`: refuses a suite that executes but selects no test, and reports `listedTests`;
- `suite-fixture`: writes `playwright.config.ts`, the file v4.1.4's runners name;
- `--plan` for `suite-preflight` and `run-suites`;
- the CI job `suite-commands`, retargeted to v4.1.4.

### Integration fixes found while combining

1. **#101's gate compiled the v4.1.2 plan with the Rehearsal 009 lock hardcoded.** A Rehearsal 010 gate would have run the commands that cannot execute. Ported from #102: `govern --lock` records the lock in `.echelon/r9-governance.json`, and the gate reads the lock and its `requirementsPackage.path` from there. The key is `lock` (#101 independently added the same selection, with a path check; its name and check are kept). Rehearsal 009's record has no `lock`, so it reads its own lock unchanged.
2. **Two hand-kept work lists.** #101's `authorize-implementation` reads `.echelon/r9-work-plan.json`, while #102 checked the Praxis backlog against the manifest. `requirements-compiler scope work-plan` now generates the work plan from the manifest. Checked: it links exactly the 863 obligation IDs that `authorize-implementation` compares against (613 from Dokimos's compiled v4.1.2 plan, 250 from Praxis's catalog manifest), with 0 unknown and 0 unlinked.
3. **16 record-only P0 requirements had no work item.** #101's step 4 requires every in-scope obligation to be linked to one. The manifest now gives them the evidence item `WI-GEN-RECORDS` (owner: reviewer), which makes 36 work items.
4. **`docs/release-gate-repair/FINAL-REPORT.md`, which #101's description cites, is not on its branch.** It was not recreated here. #101's owner should add it, or the description should be corrected.

## Shared, integration-owned files: resolution

| File | Resolution |
|---|---|
| `tools/requirements-compiler/src/V41Package.fs` | #101's version, unchanged |
| `tools/owner-freeze.fsx` | #101's version, unchanged (v4.1.4) |
| `tools/r9/r9.fsx` | #101's version, plus: `suiteSource` (`--plan`), `listedTests`/`--require-tests`, `suite-fixture`, `check-sequencing --scope-manifest`, `govern --lock` |
| `tools/r9/verification-gate.yml` | #101's version, plus: the lock from the governance record, the package from the lock, `--scope-manifest` from the lock |
| `.github/workflows/preflight-tooling.yml` | both jobs kept: #101's `release-gate-repair` (lists the commands, reproduces v4.1.4) and #102's `suite-commands` (executes every v4.1.4 suite on the fixture, WebKit included) |
| `AGENTS.md` | #101's lines plus #102's full-scope rule. The v4.1.3 line is removed. |
| `docs/release-gate-repair/NEXT-REHEARSAL-STARTUP.md` | step 3 now generates the work plan from the scope manifest and writes the scope checkpoint |
| `tools/release-gate-repair/tests.fsx` | 14 cases added (below) |

## Combined test results (local, this container, on the merged tree)

| Suite | Command | Result |
|---|---|---|
| Offline suite | `dotnet run -c Release --project tools/requirements-compiler/tests -- --offline --source .` | **all passed**, including #102's adversarial, legitimate-stop, recovery, real-scope, Rehearsal 009 replay and reconciliation cases |
| #101 harness | `dotnet fsi tools/release-gate-repair/tests.fsx -- …` (Praxis 3.25.0 and Dokimos 0.11.0 from the echelon-current 1.36.0 workstation) | **45 cases, 0 failed**: #101's 31, plus 14 added (6 suite/fixture, 8 sequencing) |
| r9 readiness harness | `dotnet fsi tools/r9/tests.fsx -- …` | **76 cases, 0 failed** |
| v4.1.4 reproduction | `requirements-compiler v41 --package requirements-compiled/v4.1.4 … --check` | PASS (X01–X21) |
| v4.1.2 frozen | the same, for v4.1.2 | regenerates unchanged (harness case `v41-v4.1.2-regenerates-unchanged`) |
| Rehearsal 009 replay | `stop-decision` / `resume` on the v4.1.4 scope | `CONTINUE_REQUIRED` at 115 min (306 min left; SCOPE_INCOMPLETE, CATALOG_IS_NOT_SCOPE). Resume returns `CONTINUE`. At the deadline, `STOP_PERMITTED` with the acceptance claim refused. |
| WebKit execution | CI job `suite-commands` | runs on the PR (this container cannot install WebKit's host libraries) |

**The 14 added harness cases:**
- `suite-preflight-require-tests-refuses-an-empty-selection`
- `suite-fixture-writes-the-plan-layout`
- `suite-fixture-without-webkit`
- `suite-preflight-fixture-refuses-v4.1.2`
- `suite-preflight-fixture-passes-v4.1.4-with-tests`
- `suite-preflight-fixture-refuses-a-missing-webkit-project`
- `check-sequencing-without-scope-manifest-is-unchanged`
- `check-sequencing-refuses-implementation-without-a-scope-checkpoint`
- `check-sequencing-passes-with-the-scope-checkpoint`
- `check-sequencing-refuses-implementation-before-the-scope-checkpoint`
- `check-sequencing-refuses-a-scope-hash-mismatch`
- `check-sequencing-refuses-incomplete-praxis-coverage`
- `check-sequencing-counts-work-items-begun-in-praxis`
- `check-sequencing-refuses-an-edited-scope-checkpoint`

## Execution policy: incremental merges versus the full-scope gate

**Separation in the combined tree.** No change in either PR makes completion of all 199 requirements a condition for merging a slice.
- **A slice merges on the development-mode gate:**
  - the applicable acceptance tests of the requirements it adds to `.echelon/verification-scope.json`, which only grows;
  - regression of every requirement already there;
  - governance and `check-sequencing`.
- **The full scope is required only by:**
  - the release gate (`mode=release`, unchanged);
  - `stop-decision` with reason `complete` (final completion);
  - `acceptance-status`'s fullPlan level.

  None of these was weakened.

**Gap, proposed as a follow-up (not implemented: beyond the approved integration scope).** Dependency satisfaction is not checked when a slice merges. PROCESS-CORRECTIONS.md §9 F1 proposes `requirements-compiler slice-check --work-item ID`. It would refuse a slice whose `dependsOn` work items or requirements are not implemented and verified (or recorded blocked), and would run in the development-mode gate.

## Remaining before merge

1. The combined CI run on this PR is green.
2. The integration review of the combined diff, by both agents' owners.
3. Correct #101's description (`FINAL-REPORT.md`), or add the file.

Owner actions before Rehearsal 010 are unchanged: PROCESS-CORRECTIONS.md §8 (v4.1.4 decisions and tag, a Dokimos release, the Rehearsal 010 lock, the launch prompt).
