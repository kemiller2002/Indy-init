# Rehearsal 4: write-path dress rehearsal (plan)

**Status:** PLAN, not executed. Kickoff needs both preconditions in §2. The owner starts the run.
**Authority:** [OWNER-DECISIONS-2026-10-24.md](OWNER-DECISIONS-2026-10-24.md) (OD-INDY-1, -6, -7, -8).
**Inherits:** R3-DV-001..017 from [REHEARSAL-003-DEFERRED-VERIFICATION-EXPERIMENT.md](REHEARSAL-003-DEFERRED-VERIFICATION-EXPERIMENT.md),
except where this plan says otherwise.
**Rehearsal 3 results:** [REHEARSAL-003-RESULTS.md](REHEARSAL-003-RESULTS.md). It covers the verdict, the reusable designs, the lessons and the gate weaknesses.

## 1. Question

Can the event scope, **including writes**, reach a computed verdict of ACCEPTED within the 420-minute ceiling, on the
exact kickoff release set?

- No rehearsal has tested this.
- R2 had writes and needed 721.2 minutes to declare acceptance. Under this repository's ledger contract it was not
  accepted (OD-INDY consequences).
- R3 reached a deployed read-only candidate at T+118.9 minutes and was blocked.

## 2. Preconditions (no kickoff without all of them)

| # | Precondition | Evidence |
|---|---|---|
| P1 | Kickoff release set approved in the Registry after Conditor qualification (OD-INDY-8). It includes the Percepta semantic CLI release, Dokimos 0.7.0 and a Conditor release with SD-1 | Registry profile version and approval record |
| P2 | Requirements baseline regenerated and pinned at the kickoff commit. `praxis requirements preservation verify` runs against it | `requirements/causa-requirements-baseline.json` `source.commit` equals the kickoff commit |
| P3 | Fresh empty repository; Conditor bootstrap on the set. The SD-1 workaround is **not** applied by hand | the bootstrap commit |
| P4 | Write-capable test data repository and named secret (`CAUSA_GITHUB_TOKEN` with contents write on the test repository only). The live write job runs on that secret | repository secret present (value never read) |
| P5 | Preflight untimed, as in R3. It ends at the first green bootstrap CI | timer-start record |

## 3. Scope

- **Baseline:** the full acceptance baseline (OD-INDY-5).
- **P1 handling:** P1 items may stay NOT_RUN, recorded in the ledger. They do not block the verdict.
- **Never deferred:** P0 write requirements (EXT-DATA-006, -009, the R3 reconciliation write set) and the
  never-deferred verification set of R3-DV-005.

## 4. Execution rules added to R3-DV

| ID | Rule |
|---|---|
| R4-01 | **Write invariants first.** R2's six defect classes become tests before the first write code: DC-001 publication parented by the decided-on head; DC-002 answer applies only to its request; DC-003 no "not written" after a send; DC-004 advisory provider fields never authorize; DC-005 mutable reads never cached and saves only to the decided-on head; DC-006 bounded start. R3's typed request book and session nonce are reused designs, not rediscovered ones |
| R4-02 | **Live writes at the first persistence checkpoint** (R3-DV-017). They run against the real GitHub test repository: create, write, reload, conflict (expected-version mismatch), rejected token. Fakes are served from recorded real responses (R3 provider conformance), including write and conflict responses |
| R4-03 | **Topology:** one continuous lead agent. A fresh, read-only reviewer agent runs at the first release candidate and again before the final verdict. It receives code, requirements and constraints, not the lead's conclusions |
| R4-04 | **Computed verdict:** the ledger is updated per checkpoint. CI runs `tools/compute-acceptance-verdict.py --repo . --report docs/REHEARSAL-004-REPORT.md` on every ledger or report change. The report's verdict line is the computed one |
| R4-05 | **No agent-authored ratification:** scope, exclusion or waiver decisions are drafted as `proposed` and wait for the owner (OD-INDY-7). A run that needs one stops that slice and records BLOCKED |
| R4-06 | **Preservation gate** before implementation and at acceptance (RDP-004) |
| R4-07 | **Stop rule:** stop when the computed verdict is ACCEPTED, or at 420 minutes. The final report gives the computed verdict and the time to each milestone |

## 5. Measurements

The milestones measured are:
- first green CI;
- first verified remote **write**;
- first verified conflict handling;
- first deployed candidate;
- reviewer findings per round;
- computed verdict and time to it.

Also recorded: CI runs created, executed and failed; tokens per agent (Praxis ≥ 3.17 attribution, which carries no
timestamps: PRX-D4); and defect classes, their recurrence against R2 and R3, and fix-induced defects.

Unavailable values are recorded as unavailable.

## 6. Decision it feeds

| Result | Consequence for the event |
|---|---|
| ACCEPTED within 420 minutes | The event plan is R4's plan, unchanged |
| Not ACCEPTED, but write milestones within 300 minutes | Cut P1 UI scope for the event. Keep P0 |
| Write milestones after 300 minutes | Owner decision before the event freeze (OD-INDY-6): reduce P0 scope through an owner commit to the baseline, or accept the risk |
