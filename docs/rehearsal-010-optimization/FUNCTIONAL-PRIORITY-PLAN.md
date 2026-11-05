# Rehearsal 010: functional priority plan

**Goal (addendum):** a working Causa application early, built up through capabilities that each run against the real GitHub provider and the real `kemiller2002/causa-data` repository:
- M1: connect;
- M2: browse historical investigations, read-only;
- M3: create;
- M4: edit and persist;
- M5: analysis;
- M6: everything else.

Nothing in this plan drops a requirement, weakens a gate, mocks a real-provider check, or relabels incomplete work. M6 is still the full approved scope. All 205 requirements and 890 obligations remain, and Rehearsal 010 is judged against them.

## 1. Milestones

The derivation, by requirement, journey, work item and DAG edge, is in MILESTONE-DEPENDENCIES.md. The machine-readable form, consumed by the schedule model, is `inputs/milestones.json`.

| Milestone | Requirements | Journeys | Owner-named anchors |
|---|---|---|---|
| M1 connect | 12 | J1-01, J1-02 | EXT-DATA-002, EXT-DATA-003, UI-001 |
| M2 browse read-only | 13 | J1-06, J2-08 | (list and open, historical) |
| M3 create | 14 | J1-03 | INV-001, 002, 004, 023, EXT-DATA-008 |
| M4 edit and persist | 36 | J1-04, J1-05, J1-06, J3-03..07 | INV-015, 016, 037, PERSIST-007, 021, 030, 031 |
| M5 analysis | 39 | J2-01..09 | |
| M6 full scope | 205 | all | |

Each requirement is assigned to the **earliest** milestone that needs it. A dependency that belongs to a later milestone is reported as a forward dependency, not pulled forward.

## 2. The data repository: what it is, and what blocks it

DATA-REPOSITORY-CONTRACT.md is a read-only investigation of the real repository; no format is invented.
- **Verified present:** Apollo 1, Apollo 11 and Challenger, in the active catalog `demonstration-v1`, with 432, 431 and 452 events. The catalog also holds the everyday lightbulb case.
- **Blocker C1.** The frozen v4.1.4 contracts **cannot represent these histories**. Most of their events have no case in the frozen `CoreEvent` union, for example `HypothesisTransitioned`, `LifecycleChanged` and `EvidenceVerificationAssessed` (382 events).
- **Further gaps:**
  - no workspace in `Repository` (C3);
  - one destination, where the data needs a read-only catalog and a writable copy (C4);
  - content fields with no wire form (C5);
  - no list operation (S1);
  - no creation path in the save contract (S2).
- **Consequence:** M2 cannot be delivered under v4.1.4 at any time, whatever the schedule. That is ODR-R10-25, and it is critical. No agent may resolve it by inventing event shapes (ADRP-005-O4).
- **Not verifiable here:** the `kemiller2002/causa` repository was not readable from this session (HTTP 403). Nothing in this plan is taken from it.

## 3. Ordering rule (implemented)

`tools/r10/schedule-core.fsx` `--priority milestones`. A lane chooses, among its ready slices:
1. the one needed by the **earliest** milestone, where a slice is needed by a milestone if it carries one of the milestone's requirements or is an ancestor of such a slice;
2. then the slice with the longest remaining chain, as before.

Dependencies, ownership, one agent per role, serialized merges and the T17 rule are unchanged. The rule only reorders work that is ready anyway.

The model tests cover:
- delivery time = the integration of the last carrier;
- an empty milestone list = the old order;
- milestone-first is never later for M1 on the real plan, at 0, 5 and 10-min CI;
- a milestone naming a requirement no slice carries is refused.

## 4. Splitting around milestones (implemented as a proposal)

The first successor (`a847f98c…`, 53 work items) split the four largest items by capability. Several of its parts still straddle milestones:

| Successor slice | Milestones it carries |
|---|---|
| `SL-WI-GEN-UI-LANDING` | M1, M2, M5, M6 |
| `SL-WI-GEN-EXT-DATA` | M1, M3 |
| `SL-WI-GEN-UI-STATES` | M2, M5 |
| `SL-WI-INV-WORKSPACE-FRAME` | M2, M3 |

`inputs/work-item-split.milestone-aligned.proposed.json` splits these again:
- `WI-GEN-UI-CONNECT` (UI-001, UI-025) before the rest of LANDING;
- `WI-GEN-UI-RECOVERY` (UI-010);
- `WI-INV-WORKSPACE-READ` (INV-008, INV-042) before FRAME;
- `WI-GEN-EXT-DATA-CONNECT` (EXT-DATA-001..007, 012) before `-WRITE` (EXT-DATA-008).

It compiles through the governed `scope compile --split` with no refusal: 205 requirements, 57 work items, scopeSha256 `723c9eb335861aa32eead93c23a5923d8a841ca9e2cd8a5d472c2afdda0efdc9`. Its `scope preflight` passes. Only the `workItems` of 14 requirements differ from the first successor.

The offline suite (`milestone-aligned split`) checks that it keeps every requirement, obligation, priority and mandatory flag, and reproduces the committed digest. As a negative control, a dependency edit was refused with `SPLIT_DROPS_PARENT_DEPENDENCY`.

Files:
- `proposals/scope-manifest.milestone-aligned.json`
- `proposals/slice-execution-manifest.milestone-aligned.json`
- `proposals/praxis-work-inventory.milestone-aligned.json`

These are not adopted. ODR-R10-19 decides which successor, if any, ODR-R10-01 approves.

**Not split, because they are frozen DAG tasks:**
- `SL-T16`: M2, M4, M6;
- `SL-T19`: M2, M4;
- `SL-T12`, `SL-T18`, `SL-T21`: M4, M6;
- `SL-T24`: M5, M6.

They need the v4.1.5 successor (ODR-R10-21).

## 5. Dependencies re-evaluated (MILESTONE-DEPENDENCIES.md §4, §5)

| Item | Finding |
|---|---|
| `SL-WI-INV-WORKSPACE` | Spans M2–M5 as one item: no milestone below M5 can merge while it is whole. Split (§4) |
| `SL-WI-GEN-UI` | Spans M1, M2 and M5. Split (§4) |
| `SL-WI-INV-START` | M3 only, in three parts |
| `SL-WI-GEN-EXT-DATA` | M1 and M3. Split into connect and write (§4) |
| T16 shell | Genuine for every UI milestone |
| T17 checkpoint | A governance edge, not a data edge, for the Connect half of EXT-DATA, review records and VER-010. **Kept**: "a generated item never precedes the kernel" is the owner's rule (V7 not proposed) |
| T18 recorded provider behaviour | Genuine (error classification) |
| T19 → T20 | **Artificial for connect, list and read-only load**, which touch only the total `SaveMachine.Initial`. Genuine for create, write and conflict. Splitting T20 (R3) gains about 6 minutes, so it is not worth a successor on its own |
| T22 workspace views | Genuine for M2 onwards |

## 6. Parallel lanes with stable F# contracts

MILESTONE-DEPENDENCIES.md §7 lists six streams:
- A: connection;
- B: data contract;
- C: list and read-only views;
- D: creation;
- E: save;
- F: analysis.

For each it gives the frozen contract the stream codes against, and the files it touches, by owner. Different owners meet only through the frozen IF41 types, so their streams can run in parallel. Within one owner, the shared files serialize the work:
- `ui`: `Update.fs`, `Engine.fs`, `Views/Investigation.fs`, `Projections.fs`;
- `persistence`: `GitHubRequests.fs`, `Classify.fs`, `SaveMachine.fs`.

That serialization is why `ui` binds M1 and M2 in every run. ODR-R10-20 (v4.1.5 ownership) addresses it.

## 7. Result (model; TIME-TO-WORKING-APPLICATION.md)

| | Approved plan (R0) | Split + queue + 25-min set-up, milestone-first (R2) | Milestone-aligned split (R5) |
|---|---|---|---|
| M1 / M2 / M3 at 0-min CI | 607 / 720 / 665 | 236 / 259 / 439 | 236 / 233 / 418 |
| M1 / M2 / M3 at 10-min CI | 681 / 794 / 739 | 283 / 309 / 489 | 293 / 284 / 468 |
| Usable at T+120/240/360/420, 0-min CI | 0/0/0/0 | 0/1/2/2 | 0/2/2/3 |
| Usable at T+120/240/360/420, 10-min CI | 0/0/0/0 | 0/0/2/2 | 0/0/2/2 |
| Obligations by T+420, 10-min CI | 435 | 527 | 520 |

All M2 figures are conditional on ODR-R10-25. R2 and R5 also need ODR-R10-19, -22, -23 and -27. ODR-R10-22 needs gate code and controls S1–S7 that do not exist yet. **Without them, the run launches on the approved plan, the R0 row applies, and no milestone is usable by the deadline.**
