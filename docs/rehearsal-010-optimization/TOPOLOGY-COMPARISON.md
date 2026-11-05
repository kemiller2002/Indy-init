# Rehearsal 010: topology comparison

All runs use the same scope, the same estimates, 3-minute merges and the same model: `tools/r10/schedule-model.fsx`, which is equivalent to `schedule.fsx` by test. They differ only in the assumption under test. **These are model results, not observations.** The independent reviewer lane is kept in every topology.

| Topology | Lanes | Approval needed | 0-min CI: finish / obligations by T+420 | 10-min CI: finish / obligations by T+420 |
|---|---|---|---|---|
| **A as modelled** (both UI lanes take any UI slice) | integration, domain, persistence, ui-1:ui, ui-2:ui, reviewer | (current) | 519 / 444 | 656 / 421 |
| **A as owned (V1)**: ui and ui-2 disjoint, per the frozen ownership | integration, domain, persistence, ui, ui-2, reviewer | none (this is the real constraint) | 720 / 402 | 794 / 435 |
| More balanced UI ownership (V5) | as V1; ≈220 min of UI work moves to ui-2 | ODR-R10-20 (v4.1.5 ownership) | 510 / 727 | 563 / 555 |
| Smaller vertical slices (V2) | as V1, 53 slices | ODR-R10-19 | 726 / 594 | 803 / 402 |
| Parallel verification, serialized merges (V3, depth 3) | as V2 | ODR-R10-22 | 726 / 594 | 776 / 519 |
| Dynamic assignment of ready slices (A as modelled) | any UI lane, any UI slice | would break "a file has exactly one owner" | 519 / 444 | 656 / 421 |
| Core lane plus a third UI role (V6): domain and persistence on one agent | integration, core:domain\|persistence, ui, ui-2, ui-3, reviewer | ODR-R10-20 plus a topology decision | 539 / 706 | 582 / 603 |
| Dedicated integration coordinator | merges already need no lane in the model | none | see below | see below |

## Findings

1. **Dynamic assignment is not available without changing file ownership.** It is what the current model silently assumes. Under the frozen rules a lane may take only slices of the roles it holds, and "ui and ui-2 never own the same file". Assigning ready slices dynamically *within* a role adds nothing, because each role is one agent.
2. **Balancing UI ownership is the largest single topology gain.** At 0-min CI it brings full completion from T+720 to T+510. It needs a v4.1.5 package that splits the investigation views into files, some owned by `ui-2`.
3. **Merging domain and persistence onto one agent (V6) is worse for full completion** (539 against 510). The persistence chain T18 → T19 → T20 → T21 sits beside the critical path, and sharing an agent with domain delays it. It integrates more by the deadline at 10-min CI (603 against 555), because the third UI role pulls UI work forward. This is a trade-off, not a clear win.
4. **Five agents do not give five times the throughput.** About 1,250–1,350 of the 2,250 lane-minutes inside the window are idle in every topology. The binding constraints are the dependency chain, one agent per role, and serialized integration.
5. **Dedicated integration coordinator.** The model gives merges and queue supervision no lane time: an optimistic assumption, which the adversarial review notes. In practice the integration agent also implements 293 minutes of slices. A coordinator that only supervises the queue would remove that hidden contention, but it costs an agent. It is not modelled numerically, because no measurement of the supervision cost exists.
