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

### Agent 2's requested comparisons (SCHEDULE-INPUTS.md §6)

**How the runs were set up.**
- **Inputs.** They come from PR #121 at b89465a and are rebuilt on demand by `tools/r10/contract-first-inputs.fsx prepare`, so nothing of PR #121 is committed.
- **Settings.** The successor plan, 45-min set-up, 3-min merges, chain-first priority, and the journey merge dependencies with every cyclic edge deferred. Agent 2 confirms this deferral is the faithful reading of CF-07 option A.
- **Contract-first.** Every UI-boundary slice is built after SL-T10, and merges after its declared `dependsOn` (`mergeDependsOn`) and its journey merge dependencies. This is an approximation: PR #121 lets a UI slice start once its UI predecessors are *built*, and this model lets it start once SL-T10 is integrated.
- **Capability times** are the last integration among PR #121's `capabilities` slice sets (`contract-first-inputs.fsx table`).
- **Results:** `results/K-*.json`.

**Primary: option A (every cyclic edge deferred), this model.** Minutes after T0 at 0 / 5 / 10-min CI:

| Plan | Queue depth | Data Visible | Data Understood | Data Editable | All integrated |
|---|---|---|---|---|---|
| Current plan | 1 | 640/674/620 | 660/699/754 | 696/740/806 | 696/740/806 |
| Current plan | 3 | 640/666/690 | 660/691/720 | 696/732/766 | 696/732/766 |
| Contract-first, existing UI agents | 1 | 593/603/661 | 599/619/713 | 616/635/726 | 616/643/805 |
| Contract-first, existing UI agents | 3 | 593/603/613 | 599/614/629 | 616/626/639 | 616/626/642 |
| Contract-first, 1 UI agent | 1 | 746/756/766 | 752/772/792 | 769/788/818 | 769/796/831 |
| Contract-first, 1 UI agent | 3 | 746/756/766 | 752/767/782 | 769/779/792 | 769/779/795 |
| Contract-first, 2 UI agents | 1 | 384/429/544 | 445/485/687 | 465/517/726 | 465/585/838 |
| Contract-first, 2 UI agents | 3 | 384/395/404 | 445/460/477 | 465/475/493 | 465/475/507 |
| Contract-first, 3 UI agents | 1 | 278/437/583 | 340/453/635 | 328/493/726 | 435/622/838 |
| Contract-first, **3 UI agents** | **3** | **278/290/313** | **340/351/367** | **328/348/380** | **435/471/518** |

**Sensitivity bound: minimal deferral (Agent 2's `contract-first-bound.fsx`, its own serial queue).** Data Visible / Understood / Editable / all, in minutes after T0:

| Plan | 0-min CI | 5-min CI | 10-min CI |
|---|---|---|---|
| Current plan | 758/764/800/800 | 859/875/916/916 | 967/993/1042/1042 |
| Contract-first, existing UI agents | 673/682/691/787 | 776/808/832/868 | 894/946/998/998 |
| Contract-first, 1 UI agent | 826/835/844/940 | 929/961/985/1021 | 1047/1099/1151/1151 |
| Contract-first, 2 UI agents | 468/477/486/582 | 571/603/627/663 | 760/812/864/864 |
| Contract-first, 3 UI agents | 364/373/382/475 | 502/534/558/594 | 708/760/812/812 |

**Reading the two together:**
1. **Contract-first helps only with more UI agents.** With one UI agent it is *later* than the current plan under both deferral rules: the single lane builds everything and then waits at the queue.
2. **Three UI agents with the speculative queue** is the only configuration that puts all three capabilities inside 420 minutes at every CI value, under option A: Data Visible T+278–313, everything integrated T+435–518. Under minimal deferral, the same plan misses 420 at every CI value.
3. **The queue depth matters more as CI slows.** For contract-first with three UI agents at 10-min CI, depth 1 gives Data Visible T+583 and depth 3 gives T+313.
4. **Non-monotonic cells appear here too** (the current plan, depth 1: Data Visible 674 at 5-min CI, 620 at 10-min CI). Read each row as a range.
5. **Under the narrower deferral, nothing fits in 420 minutes.** So the CF-07 decision is worth more than any agent-count or ordering change.
6. **Data Visible here is PR #121's capability**, including T25 and the authoritative list. It is not this branch's M0 raw browser (MINIMUM-DATA-BROWSER.md), which arrives earlier and needs no T25.

### Other Agent 2 material

- **Role files** (`roles.contract-first-{1,2,3}ui.json`): run in this model above (`results/K-*.json`). They depend on Agent 2's ownership proposal, which is not adopted, so these are sensitivity results.
- **OD-INDY-22 interaction:** Agent 2's fixture implementations must enter a run only as specification or frozen-package content (INTEGRATION-ARCHITECTURE.md).

## Agent 3: historical data compatibility

No PR or branch had been published at the time of writing.
