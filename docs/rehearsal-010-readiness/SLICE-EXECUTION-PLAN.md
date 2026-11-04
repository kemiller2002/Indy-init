# Rehearsal 010 slice execution plan

**Status:** derived, not normative.
- It is generated from the approved scope by `requirements-compiler slice-manifest`. The machine-readable form is [`slice-execution-manifest.json`](slice-execution-manifest.json).
- **No requirement is rewritten.** A requirement's priority, mandatory status, obligations and evidence producers come from the scope manifest unchanged.
- If this plan and the scope manifest ever disagree, the scope manifest wins. `slice-manifest` must be re-derived; it fails closed.

```
requirements-compiler slice-manifest --manifest docs/rehearsal-010-readiness/scope-manifest.json \
  --dag requirements-compiled/v4.1.4/execution-dag.json --journeys verification/journeys/bindings.json \
  --out docs/rehearsal-010-readiness/slice-execution-manifest.json
```

## 1. Coverage

Validated: `slice-manifest` exits 0 only when every requirement is placed; otherwise it refuses with REQUIREMENT_UNASSIGNED.

| Requirements | 199 |
|---|---|
| In a slice | 183 |
| Full-release obligations (record-only, no implementation slice) | 16: AR-001, AR-002, AR-003, AR-004, AR-007, AR-012, AR-015, AR-017, AR-018, AR-019, AR-020, AR-021, AR-024, AR-027, AR-028, AR-031 |
| Excluded by owner decision | 0 |
| **Unassigned** | **0** |

**Cross-cutting checks** run for every UI-bearing slice and for the release:
- the design contract;
- the governed journeys, judged per assertion;
- regression of every integrated obligation.

A requirement never disappears because it lacks a DAG task:
- the 8 generated work items (`WI-GEN-*`, `WI-INV-*`) carry the 100 requirements the DAG does not cover;
- the 16 record-only requirements are full-release obligations judged at release by their record producers.

## 2. What each slice states

Each entry of `slice-execution-manifest.json` has:
- `id`, `workItem` and `title`;
- `requirements`, `obligations` and `mandatoryObligations`;
- `dependsOn`: its slices;
- `implementationBoundary`: the owning lane;
- `acceptanceTests`: the bound test identities;
- `producers`;
- `governedJourneys` and `governedAssertions`: the P0 journey assertions bound to its requirements;
- `regressionTests`: every obligation integrated before it, computed at the gate;
- `realProvider`: whether a live GitHub check is required;
- `formaAndAccessibility`: whether the design contract applies;
- `mergeConditions`;
- `estimateMinutes` and `estimateSource`;
- `status`, from the ledger.

## 3. Recommended order (highest-value ready slice first)

The value order is R7's priority:
1. journey J1;
2. journey J2;
3. the design contract;
4. the rest of the P0 plan.

The order always respects dependencies. At each step the builder takes the highest-value slice whose dependencies are integrated. This table is the order computed before any work starts; the ledger decides what is ready at run time.

| # | Slice | Reqs | Mandatory obligations | DAG estimate (min) | Depends on | Live | UI | Governed assertions |
|---|---|---|---|---|---|---|---|---|
| 1 | SL-T10 bootstrap | 5 | 15 | 29 | — | | | |
| 2 | SL-T12 | 4 | 15 | 15 | T10 | | | |
| 3 | SL-T18 | 4 | 13 | 35 | T10 | live | | |
| 4 | SL-T11 | 4 | 10 | 15 | T10 | | | |
| 5 | SL-T16 shell | 5 | 34 | 35 | T11 | | UI | J2-08 J3-01 J3-02 J3-08 J3-09 |
| 6 | SL-T24 | 3 | 6 | 10 | T11 | | UI | |
| 7 | SL-T14 | 0 | 0 | 20 | T10 | | | |
| 8 | SL-T19 persistence | 22 | 115 | 60 | T12 T14 T18 | live | UI | J1-04 J1-05 J3-05 J3-07 |
| 9 | SL-T13 | 1 | 2 | 45 | T12 T14 | | | |
| 10 | SL-T15 | 1 | 2 | 20 | T13 | live | UI | |
| 11 | SL-T17 | 0 | 0 | 20 | T13 T14 T16 | | | |
| 12 | SL-WI-INV-WORKSPACE | 27 | 159 | — | T17 | | UI | J1-04..06 J2-01..07 J2-09 J3-03 J3-05 |
| 13 | SL-WI-GEN-UI | 21 | 85 | — | T17 | | UI | J1-02 J1-06 J2-05 J2-08 J3-02 |
| 14 | SL-WI-INV-START | 11 | 43 | — | T17 | | UI | J1-03 J1-06 |
| 15 | SL-T22 save states | 4 | 40 | 70 | T17 | | | J1-04 J3-03..07 |
| 16 | SL-WI-GEN-EXT-DATA | 9 | 21 | — | T17 | live | UI | J1-01..03 |
| 17 | SL-WI-GEN-WDL | 12 | 24 | — | T17 | | UI | |
| 18 | SL-T29 | 3 | 14 | 20 | T11 T17 | | UI | |
| 19 | SL-WI-GEN-PF | 16 | 45 | — | T17 | live | | |
| 20 | SL-WI-GEN-AR | 3 | 11 | — | T17 | | | |
| 21 | SL-WI-GEN-VER | 1 | 2 | — | T17 | | | |
| 22 | SL-T20 | 0 | 0 | 40 | T18 T19 | | | |
| 23 | SL-T21 | 10 | 47 | 25 | T20 T17 T13 | live | UI | |
| 24 | SL-T25 | 1 | 3 | 20 | T20 T15 | live | UI | |
| 25 | SL-T36 | 1 | 5 | 15 | T21 | | | |
| 26 | SL-T30 | 1 | 2 | 20 | T21 T22 T25 | | | |
| 27 | SL-T23 | 0 | 0 | 30 | T24 T17 | | | |
| 28 | SL-T26 | 0 | 0 | 25 | T17 | | | |
| 29 | SL-T27 | 0 | 0 | 30 | T13 | | | |
| 30 | SL-T28 | 0 | 0 | 45 | T22 | | | |
| 31 | SL-T31 | 11 | 46 | 45 | T30 T23 T26 T27 T28 T29 | live | UI | |
| 32 | SL-T32 | 0 | 0 | 20 | T31 | | | |
| 33 | SL-T33 | 1 | 2 | 20 | T32 | | | |
| 34 | SL-T34 | 3 | 14 | 20 | T33 | | | |
| 35 | SL-T35 | 0 | 0 | 15 | T12 T14 | | | |

**Execution cost (a known risk, stated rather than hidden):**
- The 27 DAG-estimated slices sum to **764 minutes**. The DAG estimates are per lane, for topology C: five builder lanes and a reviewer, finishing in 419 of 420 minutes.
- The 8 generated slices carry 100 requirements and have no DAG estimate.
- **One agent cannot complete the whole scope serially in 420 minutes.** ODR-RG-05 (topology) decides this.
- Neither the scope nor the full-release gate is relaxed because of it. The process instead makes partial delivery honest: whatever is integrated is verified, and everything else stays open and visible.

## 4. The two gates

### Slice integration gate

`requirements-compiler slice-gate`, run on the merged result (the candidate merged onto the current target). A slice merges only on PASS. The rules, each a separate named rule, are:

| Rule | Refusal codes |
|---|---|
| main is healthy | MAIN_UNHEALTHY |
| dependency slices are integrated | DEPENDENCY_NOT_INTEGRATED |
| the merged result is built on the current target | STALE_TARGET |
| governed gate for this work item, scope covering the slice, no cause | GATE_MISSING, GATE_STALE, GATE_OTHER_WORK_ITEM, GATE_SCOPE_INCOMPLETE, MANDATORY_OBLIGATION_UNVERIFIED |
| each bound journey assertion passes on Chromium **and** WebKit, against api.github.com | JOURNEY_FAILED, JOURNEY_NOT_RUN, LIVE_PROVIDER_SUBSTITUTED |
| every previously integrated obligation still passes | REGRESSION |
| the application builds and starts | BUILD_FAILED, START_FAILED |
| live provider, where the slice needs it | LIVE_PROVIDER_SUBSTITUTED, LIVE_PROVIDER_FAILED |
| design contract, for UI-bearing slices | DESIGN_CONTRACT |
| no test removed, skipped or weakened | TEST_REMOVED, TEST_SKIPPED, TEST_WEAKENED |

The gate never requires an unrelated slice:
- it judges only the slice's own requirements, its declared dependencies, and regression of what is already integrated;
- journeys are judged per assertion, so a J3 shell assertion does not block a persistence slice.

Its verdict always carries `"claimsRelease": false`.

### Full-scope completion gate

`requirements-compiler completion-gate`, in release mode only. It is COMPLETE only when **all** of these hold:
- a release-mode, unscoped, cause-free gate verdict exists at the candidate. A slice or work-item verdict is SLICE_VERDICT_IS_NOT_RELEASE;
- governed catalog acceptance exists at the candidate;
- every journey passed on both engines against api.github.com;
- the design contract passed at the candidate;
- every applicable mandatory requirement is integrated, or excluded by an owner decision.

Anything else is INCOMPLETE. A slice PASS can never become a release PASS (adversarial case C09).

## 5. Lifecycle of a slice

1. Select the highest-value ready slice (§3, with the ledger's state).
2. Confirm its requirements and dependencies in `slice-execution-manifest.json`.
3. Start its Praxis work item (`praxis work start`); the item exists from the scope checkpoint.
4. Implement on an isolated branch.
5. Run the slice's acceptance tests and the regression set locally.
6. Run the governed Verification gate for the work item (`workflow_dispatch` with `work_item` and `scope`). It runs:
   - the plan gate;
   - catalog acceptance;
   - the journeys;
   - the design contract.
7. Run `slice-gate` on the merged result. **Merge only on PASS.**
8. After the merge, verify the merged commit and its deployment.
   - If deployment verification fails, record `main-unhealthy` and stop merging.
   - Repair or revert, then record `main-healthy`.
9. Record `integrated` (the ledger refuses it for a requirement that is not verified), and update the coverage ledger (`requirements-compiler ledger`).
10. Select the next ready slice.

A failed slice stays unmerged. It is repaired, or isolated and recorded as `attempt-failed`; after three identical failures it is parked and the next independent slice is taken.
