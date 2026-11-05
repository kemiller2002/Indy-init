# Rehearsal 010 startup instructions

**These take effect only when LAUNCH-CHECKLIST.md is complete.** Every owner action in OWNER-DECISIONS-REQUIRED.md must be recorded, and the lock `kickoff/rehearsal-010/toolchain.lock.json` must be pinned by the owner. **Do not launch from this document alone.** In what follows, `SRC` is the Indy-init checkout at the commit the lock is governed from.

## 0. The launch prompt

The prompt says this and nothing narrower:

> **Objective:** maximize correctly implemented, independently verified and integrated Causa capabilities before the original deadline (T0 + 420 minutes).
> Build the approved scope in `SRC/docs/rehearsal-010-readiness/scope-manifest.json` (scopeSha256 as pinned in the lock), in dependency-aware vertical slices (`SRC/docs/rehearsal-010-readiness/SLICE-EXECUTION-PLAN.md`), following this document.

**Compute** (ODR-R10-13). No compute limit is approved: the whole window is expected to be used. A stated budget is never a reason to stop (§6).

**Do not add** a narrower target, a compute-saving instruction, or "the approved scope is X" to the prompt. Only the owner narrows scope, by approving a new scope manifest (§8).

## 1. What you are building

**The whole approved scope:**
- 205 requirements (200 mandatory) and 890 obligations;
- 161 P0 requirements of the frozen plan (613 obligations);
- the 44 requirements of the ratified catalog CAT-CAUSA-INVESTIGATION, its successor included (INV-039..044; 277 obligations);
- nothing excluded.

**What is not the scope:**
- **The content catalog is part of the scope, not the scope.** A passing catalog run is not completion.
- **A slice PASS is not a release PASS.** Only the full-scope completion gate decides completion.
- **Builder-run verification is supporting evidence.** It is never governed acceptance. Label any verdict from a workflow you wrote "implementer-run".

**What judges you.** The governed acceptance assets in Indy-init are run from the governed checkout; you cannot edit them:
- `verification/journeys` (J1–J3, Chromium and WebKit, real GitHub);
- `verification/design-contract` (Forma shell, save status, tokens, theme boundary, FL-01..10, screenshots);
- the catalog verification contract.

Build to what they check. The names they look for are listed in `SRC/verification/journeys/bindings.json` (ODR-R10-07).

## 2. T0 (the bootstrap)

1. **Bootstrap** exactly as `SRC/requirements-compiled/v4.1.4/AGENT-RULES.md` duty 1 says. Then:
   ```
   dotnet fsi SRC/tools/r9/r9.fsx -- govern --indy-init SRC --target . --lock kickoff/rehearsal-010/toolchain.lock.json
   ```
2. **Write the T0 record** `evidence/t0/t0.json` in the bootstrap commit:
   ```
   {"schema": "indy-init.t0/1", "t0": "<T0 UTC>", "deadline": "<T0 + 420 min>", "windowMinutes": 420}
   ```
   It is never changed. Every recovery and every replacement agent uses it. A different T0 or window is refused as DEADLINE_RESET.
3. Commit and push.

## 3. Before any application code

Implementation is prohibited until steps 1–5 pass. They need no implementation and no verification evidence.

1. **Suite preflight.** Compile the obligations, then run the preflight:
   ```
   dokimos verification compile --indy-execution-plan SRC/requirements-compiled/v4.1.4/verification-execution-plan.json \
     --indy-verification-plan SRC/requirements-compiled/v4/verification-plan.json --out .echelon/obligations.json
   dotnet fsi SRC/tools/r9/r9.fsx -- suite-preflight --obligations .echelon/obligations.json --root . --out evidence/t0/suite-preflight.json
   ```
   It must PASS: every executable suite command runs (chromium and webkit defined; one `dotnet test` target each). A refusal is a T10 defect; fix it in T10.
2. **Generate the work plan, and register every work item in Praxis.** Do not write the work plan by hand.
   ```
   requirements-compiler scope work-plan --manifest SRC/docs/rehearsal-010-readiness/scope-manifest.json --out .echelon/r9-work-plan.json
   ```
   - It lists every work item of the approved scope: 36 today, 37 if the catalog successor is ratified. That covers T10–T36, `WI-GEN-*` (including `WI-GEN-RECORDS`, the record-only requirements) and `WI-INV-*`.
   - It equals `SRC/docs/rehearsal-010-readiness/praxis-work-inventory.json`.
   - Create each work item with `./praxis work begin --id ID …` or `./praxis add "[ID] title"`.
   - `requirements-compiler scope preflight --praxis-queue .ros/work/queue.json --praxis-context .ros/context/current.json` prints each missing one.
3. **Scope checkpoint:**
   ```
   requirements-compiler scope checkpoint --source SRC --package requirements-compiled/v4.1.4 \
     --manifest SRC/docs/rehearsal-010-readiness/scope-manifest.json --praxis-queue .ros/work/queue.json \
     --praxis-context .ros/context/current.json --at NOW --commit SRC-COMMIT --out .echelon/scope-checkpoint.json
   ```
   - It writes the checkpoint only when the manifest equals what the frozen sources give and every work item is in Praxis.
   - Commit it. It is immutable.
4. **Initialization checkpoint (authorization).** With the work plan of step 2 committed:
   ```
   dotnet fsi SRC/tools/r9/r9.fsx -- authorize-implementation --target . --catalog SRC/requirements/catalogs/causa-investigation.requirement-catalog.json \
     --obligations .echelon/obligations.json --suite-preflight evidence/t0/suite-preflight.json \
     --spec-preflight evidence/t0/spec-preflight.json --acceptance-preflight evidence/t0/acceptance.log \
     --scope-sha256 <lock scope.scopeSha256>
   ```
   `--scope-sha256` is required: a scope checkpoint of any other scope does not authorize.
   Commit `.echelon/r9-initialization-checkpoint.json`. It is immutable.
5. **Sequencing.** `r9.fsx check-sequencing --scope-manifest` and `requirements-compiler scope order` must PASS on every commit. The governed gate runs `check-sequencing --scope-manifest` first. It refuses:
   - application code committed before the later of the two checkpoints, on any branch, merged or not;
   - a scope hash that differs from the approved manifest;
   - an edited checkpoint;
   - incomplete Praxis coverage at the scope-checkpoint commit.

## 4. The slice loop (continue to the next ready slice after every merge)

Repeat until the deadline or a permitted stop:

1. **Select** the highest-value ready slice: integrated dependencies, by the order in SLICE-EXECUTION-PLAN §3 (J1, then J2, then the design contract, then the rest of P0, then P1).
2. **Confirm** its requirements, obligations and dependencies in `slice-execution-manifest.json`. **Start** its Praxis work item.
3. **Implement** on the branch `slice/SL-<work item>`. Keep `main` working. A pull request that changes implementation from a branch not named for its slice is refused by the governed gate.
4. **Run** the slice's acceptance tests and the regression set.
5. **Open the pull request.** The governed Verification gate runs on the **merge result** of your branch onto the current `main`. It runs, in order:
   - the plan gate for the slice's work item;
   - governed catalog acceptance;
   - J1–J3 and the design contract (Chromium and WebKit, real GitHub);
   - build and start;
   - base-versus-head test integrity;
   - `slice-gate`.

   The pull request is mergeable only when that job is green. To check locally first (the same command, with your own evidence files):
   ```
   requirements-compiler slice-gate --manifest SRC/docs/rehearsal-010-readiness/scope-manifest.json --events evidence/progress/events.json \
     --slices SRC/docs/rehearsal-010-readiness/slice-execution-manifest.json --slice SL-<work item> --candidate <merged SHA> \
     --target-base <main SHA it was built on> --target-head <main SHA now> --gate <gate.json> --journeys <journey-results.json> \
     --design <design-contract.json> --regression <regression.json> --build <build.json> --live <live.json> --test-integrity <test-integrity.json> --out evidence/slices/SL-<id>.json
   ```
6. **Merge only on PASS.** A REFUSED slice stays unmerged: repair it, or isolate it and record `attempt-failed`.
7. **After the merge,** verify the merged commit and its deployment.
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
requirements-compiler stop-decision --manifest SRC/docs/rehearsal-010-readiness/scope-manifest.json --checkpoint .echelon/scope-checkpoint.json \
  --events evidence/progress/events.json --request evidence/progress/stop-request.json \
  --t0-record evidence/t0/t0.json --deadline-policy SRC/kickoff/rehearsal-010/deadline-policy.json --lock SRC/kickoff/rehearsal-010/toolchain.lock.json --repo . \
  [--completion <gate/completion.json from the release-mode Verification gate artifact of main's HEAD>] --approvals SRC/kickoff/acceptance/approvals.causa.json --trusted-operator kemiller2002 --out evidence/progress/stop-decision.json
```

Stop only on `STOP_PERMITTED`. Anything else is `CONTINUE_REQUIRED`: go back to §4.

| Reason | Permitted only when |
|---|---|
| `complete` | the governed full-scope completion gate says COMPLETE for the current head of main (`--completion`: its `gate/completion.json`; HEAD must be `main` and equal the verdict's `candidate`) |
| `owner-authorized` | an operator record `{action: authorize-stop}` named by the request exists |
| `hard-blocker` | every blocker has evidence and no open work item can proceed (a verification blocker never counts) |
| `compute-limit` | an operator record `{action: approve-compute-limit, limit}` exists and the request shows the limit reached. None is approved for Rehearsal 010 |
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
requirements-compiler resume --manifest SRC/docs/rehearsal-010-readiness/scope-manifest.json --checkpoint .echelon/scope-checkpoint.json \
  --events evidence/progress/events.json --t0-record evidence/t0/t0.json --deadline-policy SRC/kickoff/rehearsal-010/deadline-policy.json \
  --lock SRC/kickoff/rehearsal-010/toolchain.lock.json --repo . --at NOW [--stop-decision evidence/progress/stop-decision.json] --out evidence/progress/resume.json
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

The observer derives the deadline itself, from the T0 record first committed in BUILDER and the governed deadline policy. Every stop decision whose recorded deadline differs is DEADLINE_RESET, even if the builder's own `--lock` and policy agreed with it.
```
requirements-compiler observe --manifest SRC/docs/rehearsal-010-readiness/scope-manifest.json --events BUILDER/evidence/progress/events.json \
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
