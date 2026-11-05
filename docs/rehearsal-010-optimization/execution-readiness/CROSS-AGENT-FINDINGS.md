# Rehearsal 010: findings from the parallel agents

## Agent 2: contract-first UI (PR #121, head b89465a)

The PR was inspected, not merged or cherry-picked. Its schedule inputs were read from that commit for the runs below. Nothing from it is committed on this branch.

### CF-07, confirmed independently: the approved plan deadlocks under the frozen slice gate

**The rule.** The slice gate's `journeys` check (`tools/requirements-compiler/src/Ledger.fs`, 391–410) requires:
- every assertion bound to a slice (`governedAssertions`) to pass on Chromium and WebKit against `api.github.com`;
- an assertion that never ran because an earlier step of its journey failed counts as `JOURNEY_NOT_RUN`.

So a slice can merge only after every slice its journeys' earlier steps need is already on main.

**The bindings (approved `docs/rehearsal-010-readiness/slice-execution-manifest.json`):**
- `SL-T16` (shell) is bound to `J2-08`, `J3-01`, `J3-02`, `J3-08` and `J3-09`. `J2-08` runs at line 85 of the single test "J2 complete investigation analysis" (`verification/journeys/j2-analysis.spec.mjs`:22, :85), after Connect, creation and analysis steps that need slices which depend on T16.
- `J1-03` is bound to both `SL-WI-GEN-EXT-DATA` and `SL-WI-INV-START`, and `J1-06` to three slices. Each needs the others merged first.

**Count.** With Agent 2's assertion-to-slice needs (`schedule-inputs/hidden-merge-dependencies.json`), this branch's model (`--merge-deps`) and an independent script both find:
- **42 of 42** merge-dependency edges cyclic in the approved plan;
- **93 of 126** in the successor.

Agent 2 reports 22 and 36. The difference is the definition. Here, an edge is cyclic if it closes a cycle through declared dependencies **or other merge dependencies**: mutual needs, such as two slices bound to the same assertion, count. Agent 2's tool defers a narrower set (35 edges in the successor). The conclusion is the same either way: **as specified, the approved plan cannot complete. T16, the third slice on the critical path, can never merge.**

**This is a launch blocker for Rehearsal 010 on the approved plan,** independent of every optimization. It needs the owner's decision on how such assertions are judged (Agent 2's CF-07; option A defers them to a later integration point).

### What it does to the published schedule figures

**The model before `--merge-deps`.** It had no merge-time journey dependencies. That is equivalent to deferring **every** cyclic edge, and ignoring the non-cyclic ones.

**The model now.** `tools/r10/schedule-core.fsx` supports merge dependencies: a slice is **built** when its `dependsOn` are integrated, and **enters the merge queue** only when its merge dependencies are integrated. Cyclic edges are refused unless `--defer-cyclic true`. Three new model tests cover this.

The plans with merge dependencies and every cyclic edge deferred (`results/J0-*.json`, `J2-*`, `J8-*`), at 10-min CI:

| Plan | M1 | M2 | M3 | M4 | All |
|---|---|---|---|---|---|
| Approved (J0) | 681 | 794 | 739 | 566 | 794 (unchanged: every edge is cyclic and deferred) |
| Optimized (J2) | 283 | 309 | 489 | **700** (was 667) | 769 |
| Combined (J8) | 270 | 239 | 387 | **509** (was 496) | 563 |

**With Agent 2's narrower deferral,** its reference tool (`tools/contract-first-bound.fsx`, reproduced here) gives T+700 to T+1,000 at 10-min CI for every plan and agent count. The non-cyclic merge dependencies that remain then bind hard.

**The truth for any adopted CF-07 option lies between these two bounds.** Every milestone time published in this directory and in TIME-TO-WORKING-APPLICATION.md, TIME-TO-FIRST-DATA.md and SCHEDULE-COMPARISON.md is the **optimistic bound**. It assumes merge-time journey dependencies do not bind.

### Other Agent 2 material

- **Role files** (`roles.contract-first-{1,2,3}ui.json`): keyed by this branch's successor slice ids. They were run through Agent 2's tool above. They were not added to this branch's model runs, because they depend on Agent 2's ownership proposal, which is not adopted.
- **OD-INDY-22 interaction:** Agent 2's fixture implementations must enter a run only as specification or frozen-package content (INTEGRATION-ARCHITECTURE.md).

## Agent 3: historical data compatibility

No PR or branch had been published at the time of writing.
