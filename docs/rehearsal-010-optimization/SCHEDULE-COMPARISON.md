# Rehearsal 010: schedule comparison

**Every number is a model result** from `tools/r10/schedule-model.fsx`, using the DAG estimates and the derived 1.35 min/obligation estimates. None is observed. The model equals `schedule.fsx` under its defaults (`tools/r10/schedule-model-tests.fsx`). All variants use the same scope requirements and obligations, estimates, 3-minute merges and window. Each variant changes only the assumption named.

## Variants and exact commands

`O=docs/rehearsal-010-optimization`
`LO=integration:integration,domain:domain,persistence:persistence,ui:ui,ui-2:ui-2,reviewer:reviewer`
`BASE=docs/rehearsal-010-readiness/slice-execution-manifest.json`
`SUC=$O/proposals/slice-execution-manifest.successor.json`

Every command is `dotnet fsi tools/r10/schedule-model.fsx -- --merge 3 --ci 0,2,5,10,15,20 --out $O/results/<V>.json` followed by the arguments below.

| V | Changes | Arguments | Needs |
|---|---|---|---|
| V0 | none (the committed baseline; UI lanes share all UI slices) | `--slices $BASE --setup 45` | – |
| V1 | **ownership-accurate (the honest baseline)** | `--slices $BASE --setup 45 --roles $O/inputs/slice-roles.v414.json --lanes $LO` | – |
| V2 | V1 + split work items | `--slices $SUC …as V1` | ODR-R10-19 |
| V3 | V2 + speculative verification, depth 3 | `… --queue-depth 3` | ODR-R10-22 + gate code |
| V4 | V3 + 25-min set-up | `… --setup 25` | ODR-R10-23 |
| V5 | V4 + v4.1.5 UI ownership rebalance (**hypothetical**: those files and owners do not exist; change requests to ui's `Update.fs`/`Projections.fs` and to integration's project files are not modelled) | `--roles $O/inputs/slice-roles.v415-proposed.json` | ODR-R10-20 (successor package) |
| V6 | V5 + ui-3 role, domain and persistence on one agent | `--roles …v415-ui3-proposed.json --lanes integration:integration,core:domain\|persistence,ui:ui,ui-2:ui-2,ui-3:ui-3,reviewer:reviewer` | ODR-R10-20 + topology |
| V7 | V5 + generated UI items start after T13 and T16 instead of T17 (sensitivity only) | `--slices $O/results/sensitivity-ui-after-t16.slices.json` | not proposed |

## Results

Columns, in order: variant, CI minutes, all integrated at T+, slices by T+420, requirements by T+420, mandatory obligations by T+420 (of 844), critical path, queue wait (slice-minutes).

| Variant | CI | Finish | Slices | Requirements | Mandatory obligations | Critical path | Queue wait |
|---|---|---|---|---|---|---|---|
| V0-baseline | 0 | 519 | 28 | 114 | 444 | 409 | 11 |
| V0-baseline | 2 | 532 | 28 | 114 | 444 | 429 | 33 |
| V0-baseline | 5 | 568 | 27 | 111 | 430 | 459 | 107 |
| V0-baseline | 10 | 656 | 23 | 108 | 421 | 509 | 471 |
| V0-baseline | 15 | 837 | 17 | 82 | 302 | 559 | 1363 |
| V0-baseline | 20 | 1011 | 14 | 49 | 212 | 609 | 2251 |
| V1-ownership | 0 | 720 | 25 | 109 | 402 | 409 | 19 |
| V1-ownership | 2 | 732 | 30 | 125 | 490 | 429 | 59 |
| V1-ownership | 5 | 759 | 28 | 114 | 444 | 459 | 166 |
| V1-ownership | 10 | 794 | 24 | 111 | 435 | 509 | 453 |
| V1-ownership | 15 | 853 | 18 | 66 | 257 | 559 | 945 |
| V1-ownership | 20 | 953 | 14 | 49 | 212 | 609 | 1675 |
| V2-split | 0 | 726 | 38 | 146 | 594 | 409 | 20 |
| V2-split | 2 | 738 | 37 | 146 | 592 | 429 | 60 |
| V2-split | 5 | 765 | 34 | 131 | 519 | 459 | 400 |
| V2-split | 10 | 803 | 24 | 101 | 402 | 509 | 1643 |
| V2-split | 15 | 1065 | 18 | 63 | 247 | 559 | 4086 |
| V2-split | 20 | 1328 | 14 | 49 | 212 | 609 | 6166 |
| V3-split-q3 | 0 | 726 | 38 | 146 | 594 | 409 | 0 |
| V3-split-q3 | 2 | 736 | 37 | 146 | 592 | 429 | 0 |
| V3-split-q3 | 5 | 752 | 35 | 142 | 565 | 459 | 0 |
| V3-split-q3 | 10 | 776 | 34 | 131 | 519 | 509 | 22 |
| V3-split-q3 | 15 | 801 | 33 | 123 | 482 | 559 | 59 |
| V3-split-q3 | 20 | 826 | 28 | 110 | 436 | 609 | 240 |
| V4-split-q3-setup25 | 0 | 706 | 40 | 155 | 630 | 389 | 0 |
| V4-split-q3-setup25 | 2 | 716 | 38 | 146 | 594 | 409 | 0 |
| V4-split-q3-setup25 | 5 | 732 | 37 | 146 | 592 | 439 | 0 |
| V4-split-q3-setup25 | 10 | 756 | 35 | 142 | 565 | 489 | 22 |
| V4-split-q3-setup25 | 15 | 781 | 34 | 131 | 519 | 539 | 59 |
| V4-split-q3-setup25 | 20 | 806 | 30 | 116 | 460 | 589 | 240 |
| V5-own415-q3-s25 | 0 | 510 | 45 | 174 | 727 | 389 | 0 |
| V5-own415-q3-s25 | 2 | 520 | 42 | 164 | 673 | 409 | 0 |
| V5-own415-q3-s25 | 5 | 536 | 40 | 170 | 660 | 439 | 0 |
| V5-own415-q3-s25 | 10 | 563 | 36 | 145 | 555 | 489 | 10 |
| V5-own415-q3-s25 | 15 | 597 | 35 | 139 | 533 | 539 | 98 |
| V5-own415-q3-s25 | 20 | 659 | 30 | 127 | 482 | 589 | 332 |
| V6-own415-ui3-q3-s25 | 0 | 539 | 43 | 172 | 706 | 389 | 0 |
| V6-own415-ui3-q3-s25 | 2 | 547 | 41 | 164 | 640 | 409 | 0 |
| V6-own415-ui3-q3-s25 | 5 | 559 | 40 | 156 | 603 | 439 | 5 |
| V6-own415-ui3-q3-s25 | 10 | 582 | 40 | 156 | 603 | 489 | 45 |
| V6-own415-ui3-q3-s25 | 15 | 634 | 34 | 144 | 542 | 539 | 358 |
| V6-own415-ui3-q3-s25 | 20 | 715 | 28 | 108 | 417 | 589 | 615 |
| V7-sensitivity-ui-after-t16 | 0 | 484 | 44 | 182 | 718 | 389 | 0 |
| V7-sensitivity-ui-after-t16 | 2 | 492 | 43 | 182 | 718 | 409 | 1 |
| V7-sensitivity-ui-after-t16 | 5 | 509 | 41 | 168 | 658 | 439 | 2 |
| V7-sensitivity-ui-after-t16 | 10 | 571 | 40 | 162 | 641 | 489 | 25 |
| V7-sensitivity-ui-after-t16 | 15 | 608 | 38 | 159 | 621 | 539 | 51 |
| V7-sensitivity-ui-after-t16 | 20 | 658 | 33 | 142 | 562 | 589 | 275 |

## Original against optimized, under identical assumptions (10-min CI)

The honest original is V1. The optimized plan that needs **only a successor scope plus owner decisions on process** is V4. The plan that also needs **a successor package** is V5.

| Metric | Original (V1) | Optimized (V4) | Δ | Optimized with v4.1.5 (V5) | Δ |
|---|---|---|---|---|---|
| Full completion time | T+794 | T+756 | −38 | T+563 | −231 |
| Slices integrated by T+420 | 24 / 37 | 35 / 53 | (finer slices) | 36 / 53 | |
| Requirements integrated by T+420 | 111 / 205 | 142 | **+31** | 145 | +34 |
| Mandatory obligations verified by T+420 | 435 / 844 | 565 | **+130 (+30%)** | 555 | +120 |
| Critical-path duration | 509 | 489 | −20 | 489 | −20 |
| Initialization (model) | 45 | 25 | −20 | 25 | −20 |
| Integration waiting (queue, slice-minutes) | 453 | 22 | −431 | 10 | −443 |
| Verification time (CI-minutes) | 370 | 530 | +160 (more, smaller slices) | 530 | +160 |
| Estimated rework (`--fail-every 4 --repair-fraction 0.25`): obligations by T+420 | 302 | 482 | +180 | 509 | +207 |

## What the split itself contributes (review RVO-1-7), and per-slice overhead (RVO-1-6)

`results/table-review.tsv`: V1b = V1 + queue depth 3 + 25-min set-up, with no split. `--slice-overhead 5` adds 5 minutes to every slice for branch, pull request and context switching. That figure is a placeholder, not a measurement. The overhead is added to the slice's estimate, so it also enlarges repair time (estimate × repair fraction) and the scheduling priority (review RVO-2-5).

| 10-min CI | Finish | Mandatory obligations by T+420 |
|---|---|---|
| V1b: queue + set-up, no split | T+750 | 490 |
| V4: queue + set-up + split | T+756 | 565 |
| V1d: V1b + 5 min overhead per slice | T+805 | 444 |
| V4b: V4 + 5 min overhead per slice | T+876 | 475 |

**The full-completion gain comes from the queue and the set-up, not the split.** The split only adds verified work inside the window: +75 obligations with no overhead, +31 with 5 minutes per slice. With overhead it delays full completion. The model also gives merges and verification supervision no agent time, which is optimistic for every variant.

## Sensitivity (`results/sensitivity/summary.tsv`, `*.json`)

| Factor | V1 at 10 min: finish / obligations | V4 | V5 |
|---|---|---|---|
| Estimates × 0.75 | 649 / 437 | 600 / 640 | 454 / 772 |
| Estimates × 1.00 | 794 / 435 | 756 / 565 | 563 / 555 |
| Estimates × 1.25 | 956 / 356 | 934 / 475 | 686 / 499 |
| Estimates × 1.50 | 1127 / 215 | 1095 / 402 | 799 / 402 |
| Every 8th slice fails once (25% repair) | 796 / 424 | 761 / 519 | 592 / 555 |
| Every 4th slice fails once (25% repair) | 888 / 302 | 801 / 482 | 628 / 509 |
| Every 4th slice fails once (50% repair) | 925 / 302 | 868 / 475 | 668 / 475 |

- **The estimates dominate.** Even V5 with estimates 25% too high finishes at T+454 at 10-min CI, and at T+425 at 5-min CI.
- Counts integrated by the deadline are not strictly monotonic in every factor, because greedy scheduling changes which slices finish first.
- **Completion time is not always monotonic either** (corrected after review RVO-1-6). The model tests check that it never *improves* with more failures, a deeper queue is never slower, and larger estimates never help. With failures, completion is not monotonic in CI latency: V5 with `--fail-every 4 --repair-fraction 0.5` finishes at T+620 at 2-min CI and T+587 at 5-min CI, because a slice's position in first-verification order decides whether it is the one that fails.

## Can the full scope finish within 420 minutes?

**No, under any variant at any CI latency of 2 minutes or more.** The dependency critical path alone is 409 minutes at 0-min CI (389 with a 25-min set-up), and it grows by about 10 minutes per minute of CI. The best modelled plan (V5) finishes at T+510 even at 0-min CI. Only V5 with estimates 25% too high at 5-min CI comes close, at T+425.

## Functional milestones (addendum)

The model now also reports when each functional milestone is delivered (`--milestones inputs/milestones.json`), and can order ready work by milestone (`--priority milestones`). Runs R0–R5 and their tables are in TIME-TO-WORKING-APPLICATION.md. In summary, at 10-min CI:

| | R0 approved plan | R1 V4 | R2 V4, milestone-first | R5 milestone-aligned split |
|---|---|---|---|---|
| M1 connect (needs ODR-R10-28, and -29 A or -25) | 681 | 359 | 283 | 293 |
| M2 browse (needs ODR-R10-25) | 794 | 729 | 309 | 284 |
| M3 create (needs ODR-R10-25, -26) | 739 | 746 | 489 | 468 |
| All integrated | 794 | 756 | 769 | 771 |
| Obligations by T+420 | 435 | 565 | 527 | 520 |
| Milestones usable at T+420 | 0 | 1 | 2 | 2 |

Milestone-first trades about 40 obligations by the deadline, and about 13 minutes of full completion, for a connected and browsable application 6½–8 hours earlier than the approved plan on the schedule (it passes against real data only with ODR-R10-25, -28 and -29) (R0 → R2: M1 −398 minutes, M2 −485).
