# Rehearsal 010: time to a working application

**These are model results from estimates, not observations.** They come from `tools/r10/schedule-model.fsx` with the milestone definitions in `inputs/milestones.json` (derivation: MILESTONE-DEPENDENCIES.md). Every run uses 3-minute merges and the same estimates as SCHEDULE-COMPARISON.md.

## What "delivered" means in the model

A milestone is **delivered** at the minute the last slice carrying any of its requirements is integrated into main, verified. The model test `a milestone is delivered when the last slice carrying any of its requirements integrates` checks exactly that. A milestone naming a requirement that no slice carries is refused.

"Usable at T+n" counts the milestones usable by minute n. A milestone is usable only once it **and every earlier milestone** are delivered, because nothing can be browsed before Connect works. The model counts the delivered prefix (`usableAt`, review RVO-3-2), and a test checks it. Delivery times themselves are per milestone, so a later milestone can be *delivered* first (R0 M4, R4 M5, R5 M2) without being counted as usable early. The committed counts were unchanged by this correction, and the offline model tests reproduce every committed run R0–R5.

The definition is conservative: a slice that carries one M2 requirement must be fully integrated, with all its other requirements, before M2 counts.

**Two limits on what this measures.**
1. **It is a schedule claim, not a feasibility claim, for M1, M2 and M3 alike** (review RVO-3-1). Under the frozen v4.1.4 contracts and the committed `inputs/milestones.json`, **none of M1, M2 or M3 can pass against the real data repository**:
   - **M1** includes EXT-DATA-013 and UI-001-O1 (the catalog shown after Connect), so it inherits M2's blocker unless ODR-R10-29 A regroups it. Its real-provider PASS also needs the separate read and write tokens and the App installation (ODR-R10-28).
   - **M3** has no creation path in the save contract (§6 S2, part of ODR-R10-25), and INV-023 has no wire field (ODR-R10-26).
   - **M2**: see below.

   Every delivery time in this document assumes the decisions named for that milestone are taken before T0. **M2 cannot pass against the real causa-data catalog at any time without ODR-R10-25**. The frozen `CoreEvent` union has no case for most of the events in the Apollo 1, Apollo 11 and Challenger histories (DATA-REPOSITORY-CONTRACT.md §6 C1). The connection contract also has no workspace (C3) and no list operation (S1). The M2 times below assume ODR-R10-25 is resolved before T0. Without it, M2 is never delivered, and EXT-DATA-013 and T25 fail their acceptance.
2. **Milestone priority is not free.** Putting milestone work first delays tail work, so fewer obligations are integrated by T+420 (565 → 527 at 10-min CI). See the trade-off table below.

## Runs

| Run | Plan | Priority | Command (`--merge 3 --ci 0,5,10 --milestones inputs/milestones.json`, plus) | Approval needed |
|---|---|---|---|---|
| R0 | approved 37-item scope, frozen ownership, 45-min set-up, one candidate at a time | longest chain first | V1 arguments (SCHEDULE-COMPARISON.md) | none |
| R1 | V4: successor split, queue depth 3, 25-min set-up | longest chain first | V1 arguments with `--slices $SUC --queue-depth 3`, and `--setup 25` **in place of** `--setup 45` (the first occurrence of an option wins) | ODR-R10-19, -22, -23 |
| R2 | V4 | **milestones first** | R1 + `--priority milestones` | as R1 + ODR-R10-27 |
| R3 | V4 + T20 split into read and write paths (sensitivity: `results/sensitivity-t20-read-path.slices.json`) | milestones first | R2 with that slice file | as R2 + ODR-R10-21 (T20 read/write) |
| R5 | V4 with the **milestone-aligned** split (`inputs/work-item-split.milestone-aligned.proposed.json`, 57 work items, scopeSha256 `723c9eb3…`) | milestones first | R2 with `--slices $O/proposals/slice-execution-manifest.milestone-aligned.json` | as R2, with ODR-R10-19 naming `723c9eb3…` |
| R4 | V5: as R2 plus the **hypothetical** v4.1.5 UI ownership | milestones first | R2 with `--roles $O/inputs/slice-roles.v415-proposed.json` | as R2 + ODR-R10-20 |

Results are in `results/R0-baseline.json` … `results/R5-V4-milestones-aligned.json` (`milestones`, `usableMilestonesAt`).

## Minute each milestone is delivered (T+n)

| Run | CI | M1 connect | M2 browse | M3 create | M4 edit and persist | M5 analysis | M6 all |
|---|---|---|---|---|---|---|---|
| R0 | 0 | 607 | 720 | 665 | 492 | 698 | 720 |
| R0 | 10 | 681 | 794 | 739 | 566 | 772 | 794 |
| R1 | 0 | 348 | 679 | 696 | 706 | 602 | 706 |
| R1 | 10 | 359 | 729 | 746 | 756 | 684 | 756 |
| R2 | 0 | **236** | **259** | 439 | 617 | 650 | 709 |
| R2 | 5 | 256 | 285 | 465 | 643 | 676 | 740 |
| R2 | 10 | **283** | **309** | 489 | 667 | 700 | 769 |
| R3 | 0 | 230 | 260 | 440 | 618 | 651 | as R2 + ODR-R10-21 (T20 read/write) |
| R3 | 10 | 278 | 310 | 490 | 668 | 701 | as R2 + ODR-R10-21 (T20 read/write) |
| R5 | 0 | 236 | **233** | 418 | 595 | 652 | 711 |
| R5 | 5 | 262 | 256 | 444 | 621 | 678 | 742 |
| R5 | 10 | 293 | **284** | 468 | 645 | 702 | 771 |
| R4 | 0 | 233 | 240 | **393** | 471 | 458 | 594 |
| R4 | 10 | 290 | 296 | 443 | 531 | 508 | 684 |

## Milestones usable at T+120 / T+240 / T+360 / T+420

| Run | CI 0 | CI 5 | CI 10 |
|---|---|---|---|
| R0 | 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 |
| R1 | 0 / 0 / 1 / 1 | 0 / 0 / 1 / 1 | 0 / 0 / 1 / 1 |
| R2 | 0 / 1 / 2 / 2 | 0 / 0 / 2 / 2 | 0 / 0 / 2 / 2 |
| R3 | 0 / 1 / 2 / 2 | 0 / 0 / 2 / 2 | as R2 + ODR-R10-21 (T20 read/write) |
| R5 | 0 / 2 / 2 / 3 | 0 / 0 / 2 / 2 | 0 / 0 / 2 / 2 |
| R4 | 0 / 2 / 2 / 3 | 0 / 0 / 2 / 3 | 0 / 0 / 2 / 2 |

## Trade-off: working capability against obligations by the deadline

| Run | CI | All integrated at | Mandatory obligations integrated by T+420 |
|---|---|---|---|
| R0 | 10 | 794 | 435 |
| R1 | 10 | 756 | 565 |
| R2 | 10 | 769 | 527 |
| R5 | 10 | 771 | 520 |
| R4 | 10 | 684 | 585 |

Milestone-first ordering (R1 → R2):
- brings M1 forward from T+359 to T+283 and M2 from T+729 to T+309;
- costs 13 minutes of full completion and 38 obligations by T+420.

The model test `milestone-first delivers the first milestone no later than longest-chain-first` checks the first of these at 0, 5 and 10-min CI.

## Findings

1. **Under the approved plan, nothing usable exists by the deadline.** In R0 the first milestone delivered is M4, at T+492. It is delivered before M1 and M2 because those depend on the monolithic `WI-GEN-UI` and `WI-INV-WORKSPACE` items, which `ui` finishes last. Every work item spans several milestones (MILESTONE-DEPENDENCIES.md §0), so no milestone below M6 can merge on its own.
2. **Ordering is worth more than any other single change.** R2 needs no extra agent, and adds no extra ownership. On the schedule, it brings a connected, browsable application to about T+260–310, with M3 (create) at T+440–490. Each needs the decisions in limit 1 to pass against real data. It requires the split (ODR-R10-19), because only split items can be ordered by milestone.
3. **T20 read/write split (R3) adds almost nothing** (M1 −6 min at 0 CI). The read path is not the binding constraint. The `ui` lane is, because it owns the Connect view, the list view and the shell. Not recommended for its own sake.
4. **The v4.1.5 ownership (R4) is what brings M3 inside the window on the schedule** (T+393 at 0 CI). M3 also needs ODR-R10-25 (creation path) and -26 (INV-023) to pass at all. It is hypothetical (ODR-R10-20) and cannot be ready this morning.
5. **The milestone-aligned split (R5) helps M2 and M3, not M1.** It separates the parts of `WI-GEN-UI-LANDING`, `WI-GEN-UI-STATES`, `WI-INV-WORKSPACE-FRAME` and `WI-GEN-EXT-DATA` that straddled milestones. M2 then arrives before M1 has finished (T+233 at 0 CI, against T+259), and M3 at T+418, inside the window at 0 CI, on the schedule. M3 still needs ODR-R10-25 and -26 to pass. At 10-min CI, M1 is 10 minutes later than R2, because four more slices queue for verification, and 7 fewer obligations are integrated by T+420. It is a proposal, compiled and preflighted like the first successor; the offline suite checks that it keeps every requirement and obligation and reproduces its digest.
6. **No run delivers M1 by T+120.** The kernel chain T10 → T14 → T13 → T17 ends at about T+150 with 25-min set-up. Generated UI work starts after it, under the rule "a generated item never precedes the kernel". The rule is kept; V7, which relaxes it, is not proposed.
