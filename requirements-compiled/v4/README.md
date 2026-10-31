# Compiled requirements package v4 (Indy Init, November 7)

Package v4 holds the requirements for the November 7 Causa build, for Praxis to consume. It is cut from the Rehearsal 7 baseline plus the owner-approved amendments R7-AMD-03..05 (K3 in [`docs/rehearsal-007/READINESS-ASSESSMENT.md`](../../docs/rehearsal-007/READINESS-ASSESSMENT.md)).

**Status:** see [`freeze-manifest.json`](freeze-manifest.json) `status` and [`FREEZE.md`](FREEZE.md). The package stays a CANDIDATE until every freeze-gate condition is recorded. v1, v2 and v3 are unchanged.

|   |   |
|---|---|
| Source | kemiller2002/indy-init `62df28a559c054226dd4022e9744cf1db06e954f`, baseline `BL-CAUSA-INDY-INIT-62df28a559c0` ([`requirements/causa-requirements-baseline-v4.json`](../../requirements/causa-requirements-baseline-v4.json)) |
| Scope | 520 requirements, 161 P0, 2599 obligations, of which 613 are P0 obligations in the plan |
| Release set | echelon-current 1.33.0 (DEC-R7-011), resolved from echelon-registry `7a2c27a` into [`dependency-lock.json`](dependency-lock.json) |
| Catalog | causa-data `demonstration-v1` at `e8259df`: four investigations (DEC-R7-006) |
| Approvals | [`kickoff/rehearsal-007/approvals.causa.json`](../../kickoff/rehearsal-007/approvals.causa.json) at `cbc752b`. It holds the owner's operator approvals: APR-V41-OWNER-1 (ODR-V41-06) and APR-V4-OWNER-1 (ODR-V4-02..11, applied through `curation/verification-plan.json` `obligationAmendments`). Only the operator writes records (RDP-003); ODR-V4-01 is pending |

## What to read

| If you are | Read |
|---|---|
| The implementing agent | `timed-execution-manifest.json` (the order of work and the clock rules), `verification-plan.json` (what must be proven), `acceptance/`, `state-machines.json`, `invariants.json`, `operational-definitions.json`, `interfaces.json`, `dependency-lock.json` |
| The independent verifier | `acceptance/acceptance-gate.json`, `acceptance/acceptance-obligations.json`, `verification-plan.json`, `freeze-manifest.json` (digests to check) |
| The owner | [`FREEZE.md`](FREEZE.md), [`proposals/owner-decision-requests.json`](proposals/owner-decision-requests.json), [`optimization-report.md`](optimization-report.md), [`independent-review.md`](independent-review.md), `risk-register.json` |

## Files

| File | Producer | Content |
|---|---|---|
| `curation/` | authored | Inputs to the v3 pipeline. They are v3's curation re-pinned to the v4 baseline and the 1.33.0 release commits, with rehearsal repositories removed. They add a PERSIST verification rule and evidence categories, v4 work items with estimates, F-030, and the `timedRun` clock |
| `analysis/` | authored | Inputs to the v4 stage: operational definitions, state machines, invariants, Rehearsal 7 lessons, interfaces, execution policy, risks, dependency-lock source and v4 source amendments. Also the compiler test record, the independent-review record and the freeze input |
| `proposals/` | authored or `propose-test-bindings` | Owner decision requests ODR-V4-01..11 (02..11 decided by the owner; 01 pending); the r7 test-binding proposal (from `propose-test-bindings`) and the v4 strengthened-binding proposal (from the v4 stage), both applied to the curation |
| `normalized-requirements.json`, `requirements-inventory.json`, `requirements-source-map.json`, `dependency-graph.json`, `decision-registry.json`, `contract-bindings.json`, `verification-manifest.json`, `verification-plan.json`, `execution-plan-time.json`, `execution-plan-resource.json`, `preflight-manifest.json`, `timed-execution-manifest.json`, `percepta-experiment.json`, `requirements-validation-report.json`, `package-manifest.json` | v3 pipeline (`requirements-compiler --source …`) | As in v3. The timed manifest adds minutes for both topologies and the absolute-deadline clock |
| `baseline-reconciliation.json`, `dependency-lock.json`, `execution-plan.json`, `invariants.json`, `state-machines.json`, `interfaces.json`, `operational-definitions.json`, `r7-lessons-mapping.json`, `risk-register.json`, `acceptance/`, `package-validation-report.json`, `freeze-manifest.json` | v4 stage (`requirements-compiler v4`) | The v4 artifacts, checks W01–W16 and the freeze gate FG-01–FG-10 |

`execution-plan-time.json` and `execution-plan-resource.json` are v3's step-only plans, kept so the pipeline stays unchanged. `execution-plan.json` is the minute-based plan for the event.

## Rebuild

```bash
# 1. v3 pipeline: needs checkouts of the supporting repositories at the commits in curation/sources.json
requirements-compiler --source . --committed-baseline requirements/causa-requirements-baseline-v4.json \
  --curation requirements-compiled/v4/curation --out requirements-compiled/v4 \
  --repo causa-data=DIR --repo praxis=DIR --repo ordo=DIR --repo dokimos=DIR --repo percepta=DIR \
  --repo forma=DIR --repo limen=DIR --repo folio=DIR --repo echelon-registry=DIR --repo conditor=DIR [--check]
# 2. v4 stage: needs echelon-registry; --r7-evidence re-verifies the Rehearsal 7 quotes
requirements-compiler v4 --source . --package requirements-compiled/v4 --registry DIR \
  [--r7-evidence DIR-WITH-evidence/] [--check]
# 3. tests: v4 scope test and controls are part of the offline suite; --record writes analysis/compiler-test-record.json
dotnet run -c Release --project tools/requirements-compiler/tests -- --offline --source . [--record FILE]
```

CI (`preflight-tooling.yml`) runs the offline suite and `requirements-compiler v4 --check` against echelon-registry at the pinned commit. The output is deterministic: two builds are byte-identical, and the controls in `V4Tests.fs` check this.

## How v4 freezes

The package is frozen only when every condition in `freeze-manifest.json` `freezeGate` passes:
- the owner has written records for ODR-V4-01..11 in the pinned approvals file (each amendment exactly as proposed, the package approval naming the plan, review-record and test-record digests, and the selected topology), in a commit of their own on main;
- the compiler test record matches the compiler tree;
- the independent review passes;
- the freeze commit is recorded.

Then the package is recompiled, and `indy-init-requirements-v4.0.0` is tagged on the merged commit. An approved amendment is applied through `curation/verification-plan.json` `obligationAmendments` and checked by `verify-plan` (UNAUTHORIZED-AMENDMENT otherwise).

**Merge with a merge commit, not a squash.** The baseline pins `62df28a` and the approvals file pins `cbc752b`, and both must stay reachable from `main`.
