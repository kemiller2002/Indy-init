# Competition startup instructions (7 November 2026, `kemiller2002/causa`)

**These take effect only when [LAUNCH-CHECKLIST.md](LAUNCH-CHECKLIST.md) is complete.** Every owner decision the competition needs must be recorded, and the lock `kickoff/competition/toolchain.lock.json` must be committed by the owner (ODR-COMP-LOCK A, as ODR-RG-04). **Do not launch from this document alone.** In what follows, `SRC` is the Indy-init checkout at the commit the lock is governed from (the lock's `indyInit.commit`).

This is Rehearsal 010's [STARTUP-INSTRUCTIONS.md](../rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md) adapted to the competition. Every rule in it is kept unless an owner decision changed it. **What changed, and why:**

| Change | Owner decision |
|---|---|
| The frozen package is `requirements-compiled/v4.1.6` (tag `indy-init-requirements-v4.1.6`), with its WDL-013 plan supplement | APR-V416-OWNER-1 (ODR-V416-01, -WDL013, …), APR-V416-OWNER-2 (ODR-V416-02) |
| The scope is `docs/competition-readiness/scope-manifest.json` (209 requirements, 31 work items, scopeSha256 `09e38f7d…`), compiled with the fixed (V2) selectors; Rehearsal 010's approved scope stays its own | ODR-V416-SCOPE-SELECTORS A (SS-1), ODR-COMP-SCOPE |
| The lock is `kickoff/competition/toolchain.lock.json`: Conditor v0.10.21, causa-storage 1.1.0, the competition repository; everything else carried from Rehearsal 010's lock | ODR-COMP-P12 A, ODR-COMP-P14 A, ODR-COMP-LOCK |
| The catalog verification contract is `kickoff/competition/catalog-verification-contract.json` (the v4.1.6 ratified catalog) | ODR-V416-CAT A, ODR-COMP-CATALOG-CONTRACT |
| `conditor init --preset indy-init` (v0.10.21) installs `@echelon-foundry/forma-workflow` 1.2.0 | ODR-COMP-P03 A, ODR-COMP-P14 A |
| FSharp.Core 10.1.402 is a central package of the bootstrap | ODR-COMP-P02 A, ODR-V416-FSCORE A |
| The competition repository is pre-created by the owner; the bootstrap is published to it (§2 step 4) | ODR-COMP-P07 A |
| The deployed test build bundles the one test deploy key (§2 step 3, §4) | ODR-COMP-P13 A (TEST-DEPLOY-KEY.md) |
| Two scoped credentials for the builder's live tests; "live" is judged per slice | ODR-COMP-P06 A |
| Agents record Praxis work as the actor `claude`, never a lane name | ODR-COMP-P08 A |
| A recorded contingency if the governed gate cannot run (§4 step 6) | ODR-COMP-P10 A |
| The stack-drift preflight checks the successor dependency lock `docs/competition-readiness/dependency-lock.json` (Conditor v0.10.21, forma-workflow 1.2.0) (§2 step 3) | ODR-COMP-P15 A (with B, the preflight is waived) |
| The slice gate reads the competition's assertion merge needs `docs/competition-readiness/assertion-merge-needs.json` (§4) | ODR-COMP-P16 A |
| The generated `build-and-test.yml` browser job gets a limit that fits a real browser suite (§2 step 3) | the integration lane's bootstrap repair; same footing as ODR-ESU-03 A (generated CI that cannot pass is fixed in the bootstrap commit) |

## 0. The launch prompt

The prompt says this and nothing narrower:

> **Objective:** maximize correctly implemented, independently verified and integrated Causa capabilities before the original deadline (T0 + 420 minutes).
> Build the approved scope in `SRC/docs/competition-readiness/scope-manifest.json` (scopeSha256 as pinned in the lock), in dependency-aware vertical slices (`SRC/docs/competition-readiness/slice-execution-manifest.json`, ordered by `SRC/docs/rehearsal-010-readiness/SLICE-EXECUTION-PLAN.md` §3), following `SRC/docs/competition-readiness/STARTUP-INSTRUCTIONS.md`.

**Compute** (ODR-R10-13, unchanged). No compute limit is approved: the whole window is expected to be used. A stated budget is never a reason to stop (§6).

**Do not add** a narrower target, a compute-saving instruction, or "the approved scope is X" to the prompt. Only the owner narrows scope, by approving a new scope manifest (§8).

## 1. What you are building

**The whole approved scope** (`docs/competition-readiness/scope-manifest.json`, scopeSha256 `09e38f7db1888b3bbb957272cddfa050908a077a1f115d0cef2d412d7ec40666`):
- 209 requirements (204 mandatory) and 921 obligations (873 mandatory);
- 162 requirements of the frozen plan, WDL-013 among them by the v4.1.6 plan supplement (ODR-V416-WDL013 A; its three obligations are bound in SU-BROWSER-CHROMIUM on T23); 616 obligations;
- the 47 requirements of the ratified v4.1.6 catalog CAT-CAUSA-INVESTIGATION (ODR-V416-CAT; INV-045 and INV-046 added, INV-028 required); 305 obligations;
- 31 work items: T10–T36, `WI-GEN-VER`, `WI-INV-LIST`, `WI-INV-START`, `WI-INV-WORKSPACE` (the fixed selectors place UI, PF and EXT-DATA requirements on T18, T20 and T22; no `WI-GEN-UI`, `-PF`, `-EXT-DATA` or `-WDL` item remains);
- nothing excluded.

**The derived run files** (generated from the scope, never edited; LAUNCH-CHECKLIST §C reproduces them):
- `slice-execution-manifest.json`: 31 slices, one per work item, every requirement placed (`slice-manifest`: COMPLETE COVERAGE);
- `praxis-work-inventory.json`: the 31 work items `scope work-plan` gives;
- `evidence/schedule/`: the Topology A simulation (`tools/r10/schedule.fsx`) at Rehearsal 010's parameters. It does not fit 420 minutes at any CI latency (27 of 31 slices by the deadline at 0–5 min CI): a stated risk, never a reason to narrow scope or stop.

**What is not the scope:**
- **The content catalog is part of the scope, not the scope.** A passing catalog run is not completion.
- **A slice PASS is not a release PASS.** Only the full-scope completion gate decides completion.
- **Builder-run verification is supporting evidence.** It is never governed acceptance. Label any verdict from a workflow you wrote "implementer-run".

**What judges you.** The governed acceptance assets in Indy-init are run from the governed checkout; you cannot edit them:
- `verification/journeys` (J1–J3, Chromium and WebKit, real GitHub);
- `verification/design-contract` (Forma shell, save status, tokens, theme boundary, FL-01..10, screenshots);
- the catalog verification contract `kickoff/competition/catalog-verification-contract.json`.

Build to what they check. The names they look for are listed in `SRC/verification/journeys/bindings.json` (ODR-R10-07, carried by the lock). The governed gate reads the scope manifest, the slice manifest beside it and the catalog contract from the lock `govern` records, so it judges the competition's scope, not Rehearsal 010's.

## 2. T0 (the bootstrap)

1. **Install the pinned toolchain, then bootstrap** exactly as `SRC/requirements-compiled/v4.1.6/AGENT-RULES.md` duty 1 says, up to but not including its commit and its publish (`conditor repo create`): those come at step 4.
   ```
   dotnet fsi SRC/tools/r9/r9.fsx -- install-conditor --lock SRC/kickoff/competition/toolchain.lock.json --dir CONDITOR_DIR
   dotnet fsi SRC/tools/r9/r9.fsx -- workstation --lock SRC/kickoff/competition/toolchain.lock.json --conditor CONDITOR_DIR/conditor --home ECHELON_HOME
   ```
   `install-conditor` installs Conditor v0.10.21 and refuses any binary whose sha256 is not the lock's. Its `conditor init --preset indy-init` includes `@echelon-foundry/forma-workflow` 1.2.0 (ODR-COMP-P14 A; the causal graph of T23/T24 and WDL-013) and the deploy workflow's secrets step (ODR-COMP-P13 A). Never install another version. Then:
   ```
   dotnet fsi SRC/tools/r9/r9.fsx -- govern --indy-init SRC --target . --lock kickoff/competition/toolchain.lock.json
   ```
2. **Write the T0 record** `evidence/t0/t0.json` in the bootstrap commit:
   ```
   {"schema": "indy-init.t0/1", "t0": "<T0 UTC>", "deadline": "<T0 + 420 min>", "windowMinutes": 420}
   ```
   It is never changed. Every recovery and every replacement agent uses it. A different T0 or window is refused as DEADLINE_RESET (the deadline policy `kickoff/rehearsal-010/deadline-policy.json`, ODR-R10-06, is carried by the lock).
3. **Before you push, run `npm ci`, then the steps below, then §3 step 1 (the suite preflight), on this tree.** The integration lane makes every change below in the bootstrap commit.
   - **Pinned SDK in the generated CI** (owner decision ODR-ESU-03 A, 2026-11-06). Conditor generates `.github/workflows/build-and-test.yml` and `candidate-acceptance.yml` with `dotnet-version: "10.0.x"`. The bootstrap's `global.json` pins 10.0.402 with `rollForward: disable`, so a runner whose newest 10.0.x SDK is not 10.0.402 fails the build (HF-029). In every workflow, replace the line `dotnet-version: "10.0.x"` with `global-json-file: global.json`. The governed gate workflow `govern` writes already uses `global-json-file`.
   - **A browser limit that fits a real browser suite.** The real browser suites (Chromium and WebKit) take about 29 minutes; the generated `build-and-test.yml` bounds its browser job at 20 minutes, so every run is cancelled and main never goes green. Either raise that job's `timeout-minutes` to 45, or split it into two jobs (or a matrix over `chromium` and `webkit`), one engine each, each with `timeout-minutes: 30`. Change nothing else in the job: the suites, their commands and their gating stay as generated.
   - **The test deploy key** (owner decision ODR-COMP-P13 A; [TEST-DEPLOY-KEY.md](../v416-competition-decisions/TEST-DEPLOY-KEY.md) §2, §4, §5). Only the build step of `.github/workflows/deploy-pages.yml` receives the four repository secrets `REPO_OWNER`, `DATA_SOURCE`, `CAUSA_BRANCH`, `DATA_ACCESS_KEY_CAUSA`, with `CAUSA_DEPLOY_CONNECTION: "1"`, and the workflow scans the site for credentials before upload ("Scan the site for credentials"). Conditor v0.10.21's template carries this; confirm the generated file has both the build step's `env:` and the scan step exactly as the reference implementation, `kemiller2002/indy-init-rehearsal-010` branch `setup/test-deploy-key` (`.github/workflows/deploy-pages.yml`), and copy them from there if either is missing. No other workflow, build or test reads those secrets. The application side (`build.fsx` writing `dist/deploy-connection.json`, `DeployConnection.scan`, the app reading it after its build record; the reference's `build/DeployConnection.fs`, `src/Causa.App/Connection.fs` and `tests/Causa.App.Tests/DeployConnectionTests.fs`) is application code: build it in its slice (EXT-DATA-004), never in the bootstrap.
   - **Stack-drift preflight** (owner decision ODR-ESU-04 A, 2026-11-06). It must report `READY` (exit 0) before the bootstrap is pushed, and so before the scope checkpoint. `CONDITOR` is the binary `install-conditor` wrote, and `ECHELON_HOME` is the `--home` the workstation was installed into. The dependency lock is the competition's, `SRC/docs/competition-readiness/dependency-lock.json` (owner decision ODR-COMP-P15 A; the lock's `dependencyLock`, whose sha256 launch-preflight LP-03 checks): the successor of Rehearsal 010's `SRC/docs/optimization/echelon-stack-upgrade/lock/dependency-lock.json`, naming Conditor v0.10.21 and the forma-workflow 1.2.0 its preset installs. Never check against Rehearsal 010's lock: it names Conditor 0.10.20, so against the competition lock it reports DRIFT on Conditor and is never READY.
     ```
     git init --quiet ECHELON_HOME/registry
     git -C ECHELON_HOME/registry fetch --quiet --depth 1 https://github.com/kemiller2002/echelon-registry 575240233d1fc32b57cfa827ea1031b68609fc58
     dotnet fsi SRC/tools/stack-drift/stack-drift.fsx -- check --lock SRC/docs/competition-readiness/dependency-lock.json \
       --registry ECHELON_HOME/registry --toolchain-lock SRC/kickoff/competition/toolchain.lock.json --workspace . --sdk-root . \
       --bin ECHELON_HOME/.local/bin --conditor CONDITOR --fetch ECHELON_HOME/artifacts --json evidence/t0/stack-drift.json
     ```
     The registry checkout and the downloaded artifacts stay outside the repository.
     Commit `evidence/t0/stack-drift.json` with the bootstrap. A finding in this tree (DRIFT or BLOCKED on a workspace file) is fixed in the bootstrap commit. A finding in the installation (a tool, the SDK, Conditor or an artifact) is fixed by reinstalling from the lock as the finding's correction says. Never substitute another version. If it cannot be made READY, do not push: ask the owner through `stop-decision` (reason `owner-authorized`), §6.
     Only if the owner recorded ODR-COMP-P15 B instead: with ODR-COMP-P15 B the stack-drift preflight is waived for the competition (ODR-ESU-04 A does not apply to it); skip this step and commit no `evidence/t0/stack-drift.json`. launch-preflight LP-05 refuses a P15 B record while this document does not say so.

   If the suite preflight refuses, fix the bootstrap in this same commit. `r9.fsx check-sequencing` exempts only the bootstrap commit: a later commit before both checkpoints that touches an implementation path (`isImplementationPath`, r9.fsx: `src/`, `tests/`, `web/`, any project or solution file, `Directory.*.props`/`.targets`, `package.json`, `package-lock.json`, `playwright.config.*`, `global.json`, `build.fsx`) is refused as application code. If the bootstrap was already published and the preflight refuses, do not repair it in a later commit: record the defect and ask the owner through `stop-decision` (reason `owner-authorized`), §6.
4. **Commit, then publish to the pre-created repository** (owner decision ODR-COMP-P07 A; Rehearsal 010 STARTUP §2 step 4 as amended in #182). The owner created `kemiller2002/causa` before T0 with its secrets, holding only the owner's one-line `README.md` commit `7afb15755756fb5fd2a930b5c281c8794834c8dd` (the lock's `competitionRepository.initialCommit`), so `conditor repo create` refuses it as existing. Publish to it instead; the bootstrap commit goes on top of the owner's commit, never replacing it and never force-pushed:
   ```
   git remote add origin https://github.com/kemiller2002/causa.git
   git fetch origin main                      # the owner's initial commit
   git reset --soft origin/main               # the bootstrap becomes its child
   git commit                                 # the bootstrap commit, with the T0 record and stack-drift evidence
   git push origin HEAD:main                  # a fast-forward; never --force
   ```
   `git reset --soft` keeps the bootstrap tree exactly as staged; where the bootstrap writes its own `README.md` it replaces the owner's one-line file in that same commit. The README commit is not an implementation path, and `r9.fsx check-sequencing` identifies the bootstrap as the commit that adds `.echelon/r9-governance.json`, so the extra parent changes no check. **After the push, the owner turns on branch protection for `main` (require a pull request)**, as `conditor repo create` would have; from then on every commit reaches main through a pull request.

   **The repository's secrets** (set by the owner before T0; never read, printed or committed by an agent):
   - `CAUSA_ACCEPTANCE_APP_CLIENT_ID`, `CAUSA_ACCEPTANCE_APP_KEY`: the governed gate mints its per-job token for the isolated test repository `kemiller2002/causa-acceptance-r10` from them (ODR-R10-09, carried by the lock).
   - `CAUSA_GITHUB_TOKEN` (Contents: read-only) and `CAUSA_WRITE_TOKEN` (Contents: read and write), two scoped credentials (ODR-COMP-P06 A). The builder's live tests read with `CAUSA_GITHUB_TOKEN` and write only with `CAUSA_WRITE_TOKEN` (PF-015-O2/O3); a read test that needs the write token is a defect.
   - `REPO_OWNER`, `DATA_SOURCE`, `CAUSA_BRANCH`, `DATA_ACCESS_KEY_CAUSA`: read only by the deploy build (step 3; ODR-COMP-P13 A).
   - `DEPLOY_TARGET=github-pages` and Pages source "GitHub Actions" (TEST-DEPLOY-KEY.md §7).

## 3. Before any application code

Implementation is prohibited until steps 1–5 pass. They need no implementation and no verification evidence.

Main is protected. Each record below reaches main through a pull request from a `setup/*` branch, merged with a merge commit; the governed gate treats these records as non-implementation, so such a pull request names no slice. The initialization checkpoint's branch starts at the authorized HEAD and holds only the checkpoint commit: `check-sequencing` requires that commit's parent to be `authorizedAtCommit`. `SRC-COMMIT` below is the Indy-init commit `govern` recorded: `jq -r .indyInitCommit .echelon/r9-governance.json`.

**Praxis actor** (ODR-COMP-P08 A). Every Praxis record an agent writes (work items, checkpoints, completions) names the actor `claude`, never a lane name (`integration`, `ui-1`, …). Lanes are recorded in the work, not as actors.

1. **Suite preflight.** Compile the obligations, then run the preflight:
   ```
   dokimos verification compile --indy-execution-plan SRC/requirements-compiled/v4.1.6/verification-execution-plan.json \
     --indy-verification-plan SRC/requirements-compiled/v4/verification-plan.json --out .echelon/obligations.json
   dotnet fsi SRC/tools/r9/r9.fsx -- suite-preflight --obligations .echelon/obligations.json --root . --out evidence/t0/suite-preflight.json
   ```
   It must PASS: every executable suite command runs (chromium and webkit defined; one `dotnet test` target each). It runs before the bootstrap is pushed (§2 step 3); a refusal is fixed in the bootstrap commit, never after it.
2. **Generate the work plan, and register every work item in Praxis.** Do not write the work plan by hand.
   ```
   requirements-compiler scope work-plan --manifest SRC/docs/competition-readiness/scope-manifest.json --out .echelon/r9-work-plan.json
   ```
   - It lists every work item of the approved scope: 31 (T10–T36, `WI-GEN-VER`, `WI-INV-LIST`, `WI-INV-START`, `WI-INV-WORKSPACE`).
   - It equals `SRC/docs/competition-readiness/praxis-work-inventory.json`.
   - Create each work item with `./praxis work begin --id ID …` or `./praxis add "[ID] title"`.
   - `requirements-compiler scope preflight --praxis-queue .ros/work/queue.json --praxis-context .ros/context/current.json` prints each missing one.
3. **Scope checkpoint:**
   ```
   requirements-compiler scope checkpoint --source SRC --package requirements-compiled/v4.1.6 \
     --manifest SRC/docs/competition-readiness/scope-manifest.json --praxis-queue .ros/work/queue.json \
     --praxis-context .ros/context/current.json --at NOW --commit SRC-COMMIT --out .echelon/scope-checkpoint.json
   ```
   - It writes the checkpoint only when the manifest equals what the frozen sources give (v4.1.6, its plan supplement and the ratified catalog) and every work item is in Praxis.
   - Commit it. It is immutable. The Praxis files it was computed from (`.ros/work/queue.json`, `.ros/context/current.json`) must be in that commit or an earlier one: `check-sequencing` reads Praxis coverage at the commit that adds the checkpoint.
4. **Initialization checkpoint (authorization).** It reads three inputs no other step writes. Produce them first:
   ```
   praxis requirements manifest compile --catalog SRC/requirements/catalogs/causa-investigation.requirement-catalog.json \
     --source-commit SRC-COMMIT --output requirements/verification/verification-manifest.json
   dotnet fsi SRC/tools/r9/r9.fsx -- spec-preflight --indy-init SRC --lock SRC/kickoff/competition/toolchain.lock.json \
     --percepta percepta --out evidence/t0/spec-preflight.json
   conditor acceptance preflight --target SRC > evidence/t0/acceptance.log
   ```
   - `spec-preflight` must print `spec-preflight: PASS`; `evidence/t0/acceptance.log` must contain the line `acceptance preflight: READY`.
   - These are records, not application code (`isImplementationPath` matches none of them). Merge them, with the work plan, through a `setup/*` pull request before the initialization checkpoint.

   Then:
   ```
   dotnet fsi SRC/tools/r9/r9.fsx -- authorize-implementation --target . --catalog SRC/requirements/catalogs/causa-investigation.requirement-catalog.json \
     --obligations .echelon/obligations.json --suite-preflight evidence/t0/suite-preflight.json \
     --spec-preflight evidence/t0/spec-preflight.json --acceptance-preflight evidence/t0/acceptance.log \
     --scope-sha256 09e38f7db1888b3bbb957272cddfa050908a077a1f115d0cef2d412d7ec40666
   ```
   `--scope-sha256` is required and is the lock's `scope.scopeSha256`: a scope checkpoint of any other scope (Rehearsal 010's included) does not authorize.
   Commit `.echelon/r9-initialization-checkpoint.json`. It is immutable.
5. **Sequencing.** `r9.fsx check-sequencing --scope-manifest` and `requirements-compiler scope order` must PASS on every commit. The governed gate runs `check-sequencing --scope-manifest` first. It refuses:
   - application code committed before the later of the two checkpoints, on any branch, merged or not;
   - a scope hash that differs from the approved manifest;
   - an edited checkpoint;
   - incomplete Praxis coverage at the scope-checkpoint commit.

## 4. The slice loop (continue to the next ready slice after every merge)

Repeat until the deadline or a permitted stop:

1. **Select** the next ready slice (dependencies integrated) by Rehearsal 010's SLICE-EXECUTION-PLAN §3, applied to the competition's slices:
   - first by functional milestone rank (ODR-R10-27 A: connect, browse, create, edit and persist, analysis, the rest; `SRC/docs/rehearsal-010-readiness/functional-milestones.json`);
   - then by value order (J1, then J2, then the design contract, then the rest of P0, then P1).
2. **Confirm** its requirements, obligations and dependencies in `SRC/docs/competition-readiness/slice-execution-manifest.json`. **Start** its Praxis work item (actor `claude`).
3. **Implement** on the branch `slice/SL-<work item>`. Keep `main` working. A pull request that changes implementation from a branch not named for its slice is refused by the governed gate.
4. **Run** the slice's acceptance tests and the regression set.
   - *Optional, owner-acknowledged 2026-11-05:* to see quickly which test projects and suites your change affects, run `dotnet fsi SRC/tools/r10/change-impact.fsx -- --indy-init SRC select $(git diff --name-only main...HEAD)`.
     - It is feedback only. It verifies nothing; an unrecognised path selects everything.
     - It never replaces the governed gate in step 5, which runs the full plan on every slice.
5. **Open the pull request.** The governed Verification gate runs on the **merge result** of your branch onto the current `main`. It runs, in order:
   - the plan gate for the slice's work item;
   - governed catalog acceptance (the competition's catalog contract);
   - J1–J3 and the design contract (Chromium and WebKit, real GitHub);
   - build and start;
   - base-versus-head test integrity;
   - `slice-gate` (live tests judged per slice, ODR-COMP-P06 A).

   The pull request is mergeable only when that job is green. To check locally first (the same command, with your own evidence files):
   ```
   requirements-compiler slice-gate --manifest SRC/docs/competition-readiness/scope-manifest.json --events evidence/progress/events.json \
     --slices SRC/docs/competition-readiness/slice-execution-manifest.json --slice SL-<work item> --candidate <merged SHA> \
     --target-base <main SHA it was built on> --target-head <main SHA now> --gate <gate.json> --journeys <journey-results.json> \
     --design <design-contract.json> --regression <regression.json> --build <build.json> --live <live.json> --test-integrity <test-integrity.json> \
     --assertion-needs SRC/docs/competition-readiness/assertion-merge-needs.json --out evidence/slices/SL-<id>.json
   ```
6. **Merge only on PASS.** A REFUSED slice stays unmerged: repair it, or isolate it and record `attempt-failed`.
   - **Contingency, only when the governed gate cannot run** (owner decision ODR-COMP-P10 A: recorded as a contingency only). If the Verification gate cannot run at all (the workflow does not start, or fails in its own infrastructure before any verdict, twice, with the failure recorded as `suite-blocked` with the run ids), a slice may merge on its implementer-run evidence, labelled in the pull request and in a `verification-recorded` event **"implementer-verified, not governed-accepted"**. A gate that runs and REFUSES is never this case. Such a merge counts for nothing governed: it is not integrated in the ledger, and the slice is judged by the governed gate when it runs again; completion is still only `completion-gate` COMPLETE.
7. **After the merge,** verify the merged commit and its deployment (the deployed test build connects with the injected test connection, ODR-COMP-P13 A).
   - If that verification fails: record `main-unhealthy` and stop all merges. Repair or revert, then record `main-healthy`.
8. **Record** `integrated` for the slice's requirements, then run `requirements-compiler ledger`. The ledger refuses `integrated` for anything not verified.
9. **Checkpoint** Praxis (`praxis work checkpoint`) at every merge, and at least every 30 minutes. Then go to step 1.

**Progress events** (`indy-init.progress-event/1`, in `evidence/progress/events.json`) are your report of progress. They are informational: integration, regression, dependencies and completion are judged only from governed evidence (the gate's coverage and the governed catalog verification at the judged commit), so a wrong event cannot make anything pass.

The events are:

| Event | Fields |
|---|---|
| `implementation-started` | work item, requirements |
| `implementation-completed` | requirements, commit |
| `verification-recorded` | obligation, pass/fail, commit |
| `integrated` | requirements, main commit |
| `suite-blocked` / `suite-unblocked` | suite, cause |
| `attempt-failed` | work item, cause |
| `work-item-completed` | work item |
| `main-unhealthy` / `main-healthy` | commit, cause |

`acceptance-recorded` and `excluded` are owner or independent-verifier records. An `excluded` event counts only when a trusted operator record `{requirement_id, action: exclude}` in the pinned approvals backs it. An agent's version of either is refused.

**Requirement states** (one each, no "partial"):

| State | Meaning |
|---|---|
| not-started | no implementation has begun |
| in-progress | implementation has begun |
| implemented-unverified | implemented; obligations not yet all verified |
| verified-unmerged | verified, not yet on `main` |
| integrated | verified and merged to `main` |
| blocked | its producers cannot run |
| failed | an obligation failed |
| excluded | excluded by an owner decision only |

A blocked requirement keeps its mandatory status.

## 5. What never stops the run

None of these is a reason to stop; record each and continue on independent work:
- a passing content catalog;
- a failed or blocked governed gate;
- one suite that cannot run;
- an infrastructure or verification defect;
- unused time;
- "the verdict would not change".

**When verification is blocked:**
1. Record `suite-blocked`. Its obligations read as blocked; nothing else changes.
2. Keep implementing unrelated slices.
3. File the defect with its exact command and error.

**When a slice fails repeatedly:** after 3 identical failures it is parked. Take the next independent slice.

## 6. Stopping

Before stopping for any reason other than the deadline, write `evidence/progress/stop-request.json` and run:
```
requirements-compiler stop-decision --manifest SRC/docs/competition-readiness/scope-manifest.json --checkpoint .echelon/scope-checkpoint.json \
  --events evidence/progress/events.json --request evidence/progress/stop-request.json \
  --t0-record evidence/t0/t0.json --deadline-policy SRC/kickoff/rehearsal-010/deadline-policy.json --lock SRC/kickoff/competition/toolchain.lock.json --repo . \
  [--completion <gate/completion.json from the release-mode Verification gate artifact of main's HEAD>] --approvals SRC/kickoff/acceptance/approvals.causa.json --trusted-operator kemiller2002 --out evidence/progress/stop-decision.json
```

Stop only on `STOP_PERMITTED`. Anything else is `CONTINUE_REQUIRED`: go back to §4.

| Reason | Permitted only when |
|---|---|
| `complete` | the governed full-scope completion gate says COMPLETE for the current head of main (`--completion`: its `gate/completion.json`; HEAD must be `main` and equal the verdict's `candidate`) |
| `owner-authorized` | an operator record `{action: authorize-stop}` named by the request exists |
| `hard-blocker` | every blocker has evidence and no open work item can proceed (a verification blocker never counts) |
| `compute-limit` | an operator record `{action: approve-compute-limit, limit}` exists and the request shows the limit reached. None is approved for the competition |
| `environment-failure` | an evidenced environment failure seen twice. This ends the **session**, not the run: a successor resumes (§7) |
| `deadline` | T0 + 420 minutes has passed |

**At the deadline,** report the actual state:
- The report's first line is the governed verdict at the release candidate.
- Include the `ledger` scope report verbatim.
- List every mandatory requirement that is not integrated, with its reason.
- Claim completion only if `completion-gate` says COMPLETE.

## 7. Recovery (a lost executor or a replacement agent)

The session is disposable; the run is not. The successor runs:
```
requirements-compiler resume --manifest SRC/docs/competition-readiness/scope-manifest.json --checkpoint .echelon/scope-checkpoint.json \
  --events evidence/progress/events.json --t0-record evidence/t0/t0.json --deadline-policy SRC/kickoff/rehearsal-010/deadline-policy.json \
  --lock SRC/kickoff/competition/toolchain.lock.json --repo . --at NOW [--stop-decision evidence/progress/stop-decision.json] --out evidence/progress/resume.json
```

It does what the result says:

| Result | Action |
|---|---|
| `CONTINUE` | work `nextIndependentWork` |
| `REQUEST_STOP_COMPLETE` | ask `stop-decision` with `complete` |
| `REQUEST_STOP_DECISION` | ask `stop-decision` |
| `REPORT_AND_STOP` | the deadline has passed: report |
| `STOPPED` | a permitted stop is recorded |
| `REPAIR_CHECKPOINT` | repair the checkpoint first |

The deadline is the original one. It cannot be reset (DEADLINE_RESET).

## 8. Scope changes

The scope is the checkpointed scopeSha256. Changing it needs an owner record `{action: approve-scope, scope_sha256}`. Excluding a requirement needs an owner record `{requirement_id, action: exclude}`. Agents never write either (RDP-003).

## 9. The observer (owner side; read-only)

The observer runs in the owner's environment, never in the builder's repository or session. It reads:
- the approved scope;
- the builder's commits and progress events;
- the gate, catalog, design-contract and journey verdicts;
- the stop decisions.

It writes findings that Specula displays (Specula CAP-016, `evidence/observer/findings.json` in the owner's observer repository).

Every 20 minutes from T0, with `EVIDENCE` holding the latest verification artifacts downloaded from the builder's runs, and `COMPLETIONS` holding only the `gate/completion.json` files the owner downloaded from the Verification gate's release-mode artifacts (`verification-<sha>`). A completion verdict anywhere else, including the builder's repository, is never counted. One whose candidate is not on the builder's `main` is not counted either.

The observer derives the deadline itself, from the T0 record first committed in BUILDER (`kemiller2002/causa`) and the governed deadline policy. Every stop decision whose recorded deadline differs is DEADLINE_RESET, even if the builder's own `--lock` and policy agreed with it.
```
requirements-compiler observe --manifest SRC/docs/competition-readiness/scope-manifest.json --events BUILDER/evidence/progress/events.json \
  --repo BUILDER --t0-record evidence/t0/t0.json --deadline-policy SRC/kickoff/rehearsal-010/deadline-policy.json \
  --checkpoint .echelon/scope-checkpoint.json --evidence EVIDENCE --completions COMPLETIONS --pins SRC/kickoff/rehearsal-010/observer-pins.json \
  --approvals SRC/kickoff/acceptance/approvals.causa.json --trusted-operator kemiller2002 --now NOW --out OBSERVER/evidence/observer/findings.json
```

**What it detects:**
- missing scope coverage;
- plan or scope changes;
- a deadline reset;
- implementation without a registered work item;
- removed or weakened tests;
- mocks for live persistence;
- design-contract failures;
- self-verified success;
- premature stopping;
- verified but unmerged work;
- repeated failures;
- deadline risk;
- an unhealthy main.

**What it may and may not do:**
- It never merges, edits requirements or touches the builder's work.
- A finding that matches a KICKOFF-EXECUTION-PACKET §8 stop-the-line rule says so. The owner may then invoke that existing hold. The observer creates no authority of its own.
