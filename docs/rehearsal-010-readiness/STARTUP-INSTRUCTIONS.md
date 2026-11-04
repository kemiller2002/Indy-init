# Rehearsal 010 startup instructions

These replace the Rehearsal 009 launch instructions. They take effect only once the owner has:
- recorded the v4.1.4 decisions and approval (#101, `docs/release-gate-repair/owner-decision-requests.json`);
- tagged v4.1.4;
- pinned `kickoff/rehearsal-010/toolchain.lock.json`.

Until then, Rehearsal 010 is not ready (PROCESS-CORRECTIONS.md §Verdict). **Do not launch it from this document alone.**

The launch prompt should say no more than this:

> Build the approved scope in `docs/rehearsal-010-readiness/scope-manifest.json` (scopeSha256 as pinned in the lock), following `docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md`. You have 420 minutes from T0.

Do not add a narrower target or a compute-saving instruction to the prompt. If the owner wants a narrower scope, the owner approves a new scope manifest (§6).

## 1. What you are building

- **The approved scope is the scope manifest, all of it:**
  - 161 P0 requirements of the frozen plan (613 obligations, 660 plan tests);
  - 38 requirements of the ratified catalog `CAT-CAUSA-INVESTIGATION` (250 obligations);
  - 194 mandatory requirements in total.
- 359 requirements without priority P0 stay in scope at gate level (ODR-V4-10 A). Nothing is excluded.
- **The content catalog is part of the scope, not the scope.** A passing Dokimos catalog run is not completion.
- **Obligations are not tasks.** Build each requirement once, in the work item that the manifest links it to. Its obligations are judged by the producers the manifest names. Record-only obligations need their evidence producer (a review, a process record, an owner record or the independent verifier), not code.

## 2. Before any application code (T10, the bootstrap)

Follow the startup order in [`docs/release-gate-repair/NEXT-REHEARSAL-STARTUP.md`](../release-gate-repair/NEXT-REHEARSAL-STARTUP.md) (#101), with the package and lock the owner pinned: workstation, preflights, T0 bootstrap with v4.1.4, catalog manifest, obligations, suite preflight, initialization checkpoint. Two changes apply:

1. **`govern` names the lock**, so the gate compiles the pinned package and runs `check-sequencing` with the scope:
   ```
   dotnet fsi SRC/tools/r9/r9.fsx -- govern --indy-init SRC --target . --lock kickoff/rehearsal-010/toolchain.lock.json
   ```
2. **The work items come from the scope manifest, and the scope is checkpointed before authorization:**
   - **Generate the work plan.** `authorize-implementation` reads it; do not write it by hand.
     ```
     requirements-compiler scope work-plan --manifest SRC/docs/rehearsal-010-readiness/scope-manifest.json --out .echelon/r9-work-plan.json
     ```
   - **Create every one of its 36 work items in Praxis:** T10–T36, `WI-GEN-*` and `WI-INV-*`. Use `./praxis work begin --id ID …` or `./praxis add "[ID] title"`. `scope preflight` prints each missing one.
   - **Write the scope checkpoint, then commit and push it:**
     ```
     requirements-compiler scope checkpoint --source SRC --package requirements-compiled/v4.1.4 --manifest SRC/docs/rehearsal-010-readiness/scope-manifest.json \
       --praxis-queue .ros/work/queue.json --praxis-context .ros/context/current.json --at NOW --commit SRC-COMMIT --out .echelon/scope-checkpoint.json
     ```
     It writes the checkpoint only when the manifest equals what the frozen sources give and every work item is in Praxis.
   - **Run `authorize-implementation`** (#101) and commit the initialization checkpoint.

   `r9.fsx check-sequencing --scope-manifest` runs in the gate on every push. It refuses:
   - application code committed before the later of the two checkpoints;
   - a scope hash that differs from the approved manifest;
   - an edited scope checkpoint;
   - incomplete Praxis coverage at the checkpoint commit.

On a tree past initialization, run `suite-preflight` with `--require-tests true`. A suite command that executes but selects no test is refused.

**Implementation is prohibited until these steps pass.** None of them requires any implementation or verification to be complete.

### Incremental merges and final completion

- **A vertical slice merges** when the gate passes in development mode. That covers:
  - the slice's applicable acceptance tests (the requirements it adds to `.echelon/verification-scope.json`, which may only grow);
  - regression of every requirement already in that scope;
  - governance and sequencing.

  It does not need the other 198 requirements.
- **Before merging, check the slice's dependencies.** The `dependsOn` of the slice's work items in the manifest must be implemented and verified. The gate does not yet check this (PROCESS-CORRECTIONS.md §9, follow-up F1).
- **The full-scope manifest stays authoritative** for planning, coverage, reporting, the stop decision (§5), and the release gate (`mode=release`). Completion of the run is still every applicable mandatory requirement verified.

## 3. Tracking progress

Record progress events (`indy-init.progress-event/1`) in `evidence/progress/events.json` as work happens:
- `implementation-started` (work item, requirements);
- `implementation-completed` (requirements, commit);
- `work-item-completed` (for a timed task without P0 requirements);
- `verification-recorded` (obligation, pass or fail, commit);
- `suite-blocked` / `suite-unblocked` (suite, cause);
- `attempt-failed` (work item, cause).

Running `requirements-compiler progress --manifest … --events … --out evidence/progress/progress.json` gives each requirement two independent axes:

| Axis | States |
|---|---|
| implementation | not-started, in-progress, implemented |
| verification (per obligation) | pending, pass, fail, blocked |
| derived status | not-started, implementation-in-progress, verification-pending, verification-blocked, verification-failed, verified, accepted |

**When a runner cannot run** (the gate's `run-suites` fails, a provider is down):
1. Record `suite-blocked` for that suite.
2. Its obligations read blocked.
3. Nothing else changes.
4. Keep implementing.

Only the owner (operator) or the independent verifier records `acceptance-recorded`. An agent's acceptance is refused.

## 4. Continuing

While approved work remains and progress is possible, continue:
- **Work the next independent item.** `stop-decision` lists the open work items whose dependencies are not blocked, as `remaining.nextIndependentWork`. When verification is blocked, prefer executable work that does not depend on the blocked suite.
- **Commit, push and checkpoint regularly.** Merge each green pull request. Run `./praxis work checkpoint` at every coherent boundary, and at least every 30 minutes.
- **Do not retry a failure forever.** After 3 consecutive failures of one work item with the same cause, record them as `attempt-failed`. The item is parked (the retry budget) and drops out of the independent work. Move on, and report it.
- **These are not reasons to stop:**
  - a passing content catalog;
  - a failed or blocked release gate;
  - unused time;
  - "the gate verdict would not change".

  A failed gate never authorizes reducing the scope.

## 5. Stopping

Before stopping for any reason other than the deadline, write a stop request (`indy-init.stop-request/1`) and run:

```
requirements-compiler stop-decision --manifest SRC/docs/rehearsal-010-readiness/scope-manifest.json --checkpoint .echelon/scope-checkpoint.json \
  --events evidence/progress/events.json --request evidence/progress/stop-request.json --t0 T0 \
  --approvals SRC/kickoff/acceptance/approvals.causa.json --trusted-operator kemiller2002 --out evidence/progress/stop-decision.json
```

Stop only on `STOP_PERMITTED`. The permitted reasons are:

| Reason | Permitted when |
|---|---|
| `complete` | every applicable mandatory requirement is verified (or accepted) |
| `owner-authorized` | a trusted operator's record `{action: authorize-stop}` named by the request exists in the pinned approvals |
| `hard-blocker` | every blocker has evidence, and no open work item can proceed (verification blockers never count) |
| `environment-failure` | an environment kind (container lost, toolchain unavailable, credentials revoked, network unavailable, provider outage), with evidence, seen on at least 2 attempts |
| `deadline` | T0 + 420 minutes has passed: stop, whatever remains |

Any other request gets `CONTINUE_REQUIRED` with its refusal codes and the remaining work. Act on it: go back to §4.

At the deadline, stop and report the actual state. Claim acceptance only if `acceptanceClaimPermitted` is true; otherwise the decision refuses the claim (`ACCEPTANCE_NOT_ESTABLISHED`).

## 6. Recovery (a lost executor)

An executor session is disposable. If it is lost (a container restart, a session end, or a permitted `environment-failure` stop), the run is not over. The successor rebuilds its plan from the committed scope manifest, checkpoint and progress events:

```
requirements-compiler resume --manifest SRC/docs/rehearsal-010-readiness/scope-manifest.json --checkpoint .echelon/scope-checkpoint.json \
  --events evidence/progress/events.json --t0 T0 --at NOW [--stop-decision evidence/progress/stop-decision.json] --out evidence/progress/resume.json
```

It returns one of these actions:

| Action | Meaning |
|---|---|
| `CONTINUE` | work `nextIndependentWork` |
| `REQUEST_STOP_COMPLETE` | ask `stop-decision` with `complete` |
| `REQUEST_STOP_DECISION` | nothing independent remains; ask `stop-decision` |
| `REPORT_AND_STOP` | the deadline has passed |
| `STOPPED` | a `STOP_PERMITTED` decision is recorded |
| `REPAIR_CHECKPOINT` | the checkpoint is missing or does not match the scope |

## 7. Scope changes

The scope is the checkpointed `scopeSha256`. A different scope needs an owner record `{action: approve-scope, scope_sha256: <new>}` in the pinned approvals file. Agents never write it (RDP-003). Without it, `stop-decision` refuses every request (`SCOPE_CHANGE_UNAPPROVED`).
