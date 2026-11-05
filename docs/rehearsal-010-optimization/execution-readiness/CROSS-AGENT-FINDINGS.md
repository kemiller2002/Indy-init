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

**Counts, and what they mean** (corrected after review RVO-8-2). Deadlock is a property of **slices**: a slice can never merge, as frozen, if it lies on a cycle of the combined wait-for graph (`dependsOn` plus the journeys' merge dependencies). Three edge sets matter:

| Plan | Merge-dependency edges | On any cycle | Closing a cycle through `dependsOn` alone (impossible whatever else is deferred) | Greedy cycle-breaking set (`--defer-cyclic minimal`) | Agent 2's deferred set |
|---|---|---|---|---|---|
| Approved | 42 | 42 | 9 (7 of them SL-T16's) | 21 | 22 |
| Successor | 126 | 93 | 18 | 36 | 35 |

- Counts "on any cycle" are the 42 and 93 this document first reported.
- "Impossible" counts and Agent 2's 35 are from the round-8 reviewer.
- Agent 2's 35 edges are a subset of the 93, and deferring them alone leaves the successor graph acyclic.

**The conclusion does not depend on the definition.** As specified, T16, the third slice on the critical path, can never merge, so **the approved plan cannot complete**. It needs the owner's decision on how such assertions are judged (Agent 2's CF-07).

### What it does to the published schedule figures

**The model before `--merge-deps` represented no merge-time journey dependency at all.** That matches the literal reading of CF-07 option A: "judge a slice's bound assertion only once the slices it needs are integrated, and judge it at release otherwise". Under that reading, merge dependencies never delay a merge; they move judgment of those assertions to the release gate.

`tools/r10/schedule-core.fsx` now models merge dependencies:
- a slice is **built** when its `dependsOn` are integrated, and **enters the merge queue** only once its merge dependencies are on main;
- cycles are refused unless `--defer-cyclic all` (every edge on a cycle) or `--defer-cyclic minimal` (a **greedy** cycle-breaking set: cyclic edges deferred one at a time in sorted order until no cycle remains. It is not a minimum set: 36 edges in the successor against Agent 2's 35, and 21 against 22 in the approved plan; review RVO-9-3);
- the input file, its sha256 and the policy are recorded in each result (`mergeDependencies`).

Three policies, at 10-min CI (minutes after T0):

| Plan | Policy | M0 | M1 | M2 | M3 | M4 | All |
|---|---|---|---|---|---|---|---|
| Approved | none, as frozen | – | **never** (deadlock) | never | never | never | never |
| Approved | literal option A (published R0) | – | 681 | 794 | 739 | 566 | 794 |
| Approved | all cyclic deferred (J0) | – | 681 | 794 | 739 | 566 | 794 |
| Approved | greedy cycle-breaking deferral (J0-minimal) | – | 931 | 983 | 983 | 983 | 1,153 |
| Optimized | literal option A (published R2) | – | 283 | 309 | 489 | 667 | 769 |
| Optimized | all cyclic deferred (J2) | – | 283 | 309 | 489 | 700 | 769 |
| Optimized | greedy cycle-breaking deferral (J2-minimal) | – | 721 | 747 | 495 | 808 | 834 |
| Combined | literal option A (published E8) | 120 | 270 | 239 | 387 | 496 | 565 |
| Combined | all cyclic deferred (J8) | 120 | 270 | 239 | 387 | 509 | 563 |
| Combined | greedy cycle-breaking deferral (J8-minimal) | 120 | 505 | 545 | 402 | 594 | 617 |

Results: `results/J0-*`, `J2-*`, `J8-*`, each with and without `-minimal`.

**What this means.**
1. **The published figures are not a bound** (corrected; review RVO-8-1). They are the result under the literal option A. The greedy model is not monotone in added constraints: some times come out *earlier* with more merge dependencies. Examples: J2 M5 690 against published 700; J8 completion 563 against 565.
2. **Under greedy cycle-breaking deferral, the early milestones that depend on journeys move by hours.** The combined plan's authoritative list (M2) goes from T+239 to T+545.
3. **The raw data browser M0 is unaffected (T+101–120).** It carries no journey binding (ODR-R10-30).
4. **The CF-07 decision decides which row applies.** Literal option A keeps the published schedule but moves those assertions' judgment to the release gate. A stricter rule pushes Connect and Browse past 420 minutes in every plan.
5. **Agent 2's own tool** (setup 45, its role files, a serial queue) gives all-slices T+709 to T+1,164 at 10-min CI across its configurations (`bound-results.txt` at b89465a). Those are different configurations, so they are not a range for these rows.
6. **Reproduction.** The merge-dependency input lives only in PR #121 (`schedule-inputs/hidden-merge-dependencies.json` at b89465a, sha256 recorded in each J result). The model tests therefore do not reproduce the J runs; the R0–R6 runs, which need no external input, are reproduced.

### Agent 2's requested comparisons (SCHEDULE-INPUTS.md §6)

**How the runs were set up.**
- **Inputs.** They come from PR #121 at b89465a and are rebuilt on demand by `tools/r10/contract-first-inputs.fsx prepare`, so nothing of PR #121 is committed.
- **Settings.** The successor plan, 45-min set-up, 3-min merges, chain-first priority, and the journey merge dependencies with every cyclic edge deferred. These were run with `--defer-cyclic all`, before `minimal` existed, so their results carry no `mergeDependencies` record.
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

**Sensitivity: greedy cycle-breaking deferral (Agent 2's `contract-first-bound.fsx`, its own serial queue).** Data Visible / Understood / Editable / all, in minutes after T0:

| Plan | 0-min CI | 5-min CI | 10-min CI |
|---|---|---|---|
| Current plan | 758/764/800/800 | 859/875/916/916 | 967/993/1042/1042 |
| Contract-first, existing UI agents | 673/682/691/787 | 776/808/832/868 | 894/946/998/998 |
| Contract-first, 1 UI agent | 826/835/844/940 | 929/961/985/1021 | 1047/1099/1151/1151 |
| Contract-first, 2 UI agents | 468/477/486/582 | 571/603/627/663 | 760/812/864/864 |
| Contract-first, 3 UI agents | 364/373/382/475 | 502/534/558/594 | 708/760/812/812 |

**Sensitivity: greedy cycle-breaking deferral in this model** (`--defer-cyclic minimal`, same settings as the primary table; `results/K-*-minimal.json`, with provenance). Data Visible / Understood / Editable / all, in minutes after T0 at 0 / 5 / 10-min CI:

| Plan | Queue depth | Data Visible | Data Understood | Data Editable | All integrated |
|---|---|---|---|---|---|
| Current plan | 1 | 755/851/962 | 764/875/1001 | 774/885/1014 | 797/908/1037 |
| Current plan | 3 | 755/838/915 | 764/857/944 | 774/872/964 | 797/895/987 |
| Contract-first, existing UI agents | 1 | 620/718/824 | 632/750/876 | 635/758/902 | 740/822/928 |
| Contract-first, existing UI agents | 3 | 620/659/712 | 632/678/741 | 635/683/751 | 740/787/833 |
| Contract-first, 1 UI agent | 1 | 773/871/977 | 785/903/1029 | 788/911/1055 | 893/975/1081 |
| Contract-first, 1 UI agent | 3 | 773/812/865 | 785/831/894 | 788/836/904 | 893/940/986 |
| Contract-first, 2 UI agents | 1 | 475/554/728 | 481/578/767 | 490/610/819 | 562/658/832 |
| Contract-first, 2 UI agents | 3 | 475/499/539 | 481/515/565 | 490/526/581 | 562/614/660 |
| Contract-first, 3 UI agents | 1 | 359/504/701 | 371/536/753 | 374/544/779 | 479/608/805 |
| Contract-first, **3 UI agents** | **3** | **359/401/454** | **371/420/483** | **374/425/493** | 479/529/575 |

**Reading them together:**
1. **Contract-first helps only with more UI agents.** With one UI agent it is *later* than the current plan under both deferral rules: the single lane builds everything and then waits at the queue.
2. **Three UI agents with the speculative queue** is the only configuration whose three capabilities are all inside 420 minutes at every CI value under literal-A-style deferral (`all`): Data Visible T+278–313, everything integrated T+435–518. Under greedy cycle-breaking deferral, the capabilities fit only at 0-min CI: T+359–374 in this model, T+364–382 in Agent 2's tool. At 5-min CI only Data Visible (T+401) and Data Understood (T+420) fit, in this model only. At 10-min CI nothing fits. Full integration is past 420 in every greedy run (corrected in review RVO-9-1).
3. **The queue depth matters more as CI slows.** For contract-first with three UI agents at 10-min CI, depth 1 gives Data Visible T+583 and depth 3 gives T+313.
4. **Non-monotonic cells appear here too** (the current plan, depth 1: Data Visible 674 at 5-min CI, 620 at 10-min CI). Read each row as a range.
5. **The CF-07 policy moves the capabilities by 80–140 minutes** for the best plan (three UI agents, depth 3), at 0, 5 and 10-min CI. That is the same order as the gain from adding UI agents (two → three: about 85–115 minutes under the greedy policy). Neither alone brings full integration inside 420 minutes under the greedy policy.
6. **Data Visible here is PR #121's capability**, including T25 and the authoritative list. It is not this branch's M0 raw browser (MINIMUM-DATA-BROWSER.md), which arrives earlier and needs no T25.

### Other Agent 2 material

- **Role files** (`roles.contract-first-{1,2,3}ui.json`): run in this model above (`results/K-*.json`). They depend on Agent 2's ownership proposal, which is not adopted, so these are sensitivity results.
- **OD-INDY-22 interaction:** Agent 2's fixture implementations must enter a run only as specification or frozen-package content (INTEGRATION-ARCHITECTURE.md).

## Agent 3: historical data compatibility

No PR or branch had been published at the time of writing.
