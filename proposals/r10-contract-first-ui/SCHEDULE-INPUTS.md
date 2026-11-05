# Schedule inputs for the optimization agent

**Handoff to:** the Rehearsal 010 optimization agent (branch `claude/r10-optimization`, PR #119).
- This proposal does not edit `tools/r10/schedule-model.fsx`, `docs/rehearsal-010-optimization/`, or any work-item split or schedule proposal.
- Every figure below is a **model result from estimates**. Nothing was measured in a timed run.

## 1. Current UI prerequisite chain (v4.1.4, approved)

```
T10 bootstrap (29) ─┬─ T11 stack lock (15) ── T16 shell (35, ui) ─────────────────┐
                    ├─ T12 invariants (15) ─┐                                       ├─ T17 checkpoint 1 (20) ── every other UI slice
                    └─ T14 interface freeze (20) ─┴─ T13 kernel (45, domain) ────────┘
```

- Every UI slice except T16 and T24 depends on **SL-T17** (`slice-execution-manifest.json`). STARTUP-INSTRUCTIONS §4 step 1 starts only slices whose dependencies are integrated.
- T17's earliest start is T+94 (`execution-dag.json`). With CI and merge time, the first non-shell UI slice starts at T+189 (0-min CI) or T+252 (10-min CI) in this model.
- What a UI agent would build against before T17:
  - IF41-VIEW has three view records only (save indicator, header, screen);
  - every content payload is an opaque `class end` stub (`00-DOMAIN-STUBS.fs`);
  - every view is one `ui`-owned file (`Views/Investigation.fs`).

## 2. Proposed contract-first prerequisite chain

```
T10 bootstrap, now including src/Causa.App/Ui/Contracts/*.fs and the fixture backend
  └─ every UI slice may START: built and tested against UiBackend + fixtures (Layers A, B)
        MERGE still needs: declared dependsOn integrated  AND  the live path its bound journey assertions use
```

**Start condition for a UI agent.**
- SL-T10 is integrated, and the slice's UI predecessors are *built*, not merged.
- `evidence/bootstrap-compile.log` shows the contracts and screens compile in the real v4.1.4 bootstrap with no domain implementation.

**Merge condition.** Unchanged: `slice-gate` PASS. Its journeys rule (`Ledger.fs` lines 391–410) makes these hidden merge dependencies real. They are listed per assertion in `schedule-inputs/hidden-merge-dependencies.json`, corrected after the independent review (R3):

| Bound assertions | Also need integrated (successor ids) |
|---|---|
| J1-02, J3-01, J3-02 | EXT-DATA (Connect) |
| J1-03 | EXT-DATA, START-ACTIONS (Start does not save) |
| J2-01..J2-08 | EXT-DATA, START-ACTIONS, WORKSPACE-FRAME and that assertion's editors (EVIDENCE, HYPOTHESES, LINKS, CONCLUSION; J2-06 and J2-08 also UI-STATES and LANDING) |
| J1-04, J1-05, J3-03, J3-04, J3-06, J3-07, J3-08 | the above plus WORKSPACE-SAVE, T22 and T21: these run after a save. J3-08 is included because the unasserted `saveAndConfirm` before it aborts the test if it fails |
| J1-06, J2-09, J3-05, J3-09 | the above plus T25 and INV-LIST: a fresh context reloads |

The rules are applied to **both** policies, so the current plan is not credited with merges its gate would refuse.

They expose CF-07: the bindings create **cyclic merge dependencies**.
- 22 edges in the approved manifest and 36 in the successor.
- Some cycles run through declared dependencies:
  - SL-T16 needs T21, which needs T17, which needs T16;
  - SL-T19 needs T21, which needs T20, which needs T19.
- Others are mutual journey couplings: SL-WI-GEN-EXT-DATA (bound to J1-03) needs SL-WI-INV-START, and SL-WI-INV-START (bound to J1-03 too) needs EXT-DATA.
- The model defers these edges greedily, in a fixed order and identically in both policies, only until the graph is acyclic: the **minimal** deferral, and the latest times. It prints them.
- CF-07 option A as worded defers every edge that closes a cycle through any merge dependency. The optimization agent implemented that in schedule-model.fsx (`--merge-deps`, `--defer-cyclic`): 42/42 approved and 93/126 successor edges. It gives the earliest times.
- These are not monotone bounds. The optimization agent identifies three distinct policies:
  - **literal option A**: assertions whose needs are not on main are judged at release, so merge dependencies never delay a merge. This equals its published pre-merge-dependency figures;
  - **all cyclic edges deferred**;
  - **minimal deferral**.
  
  Which one applies is the owner's CF-07 decision. §4b gives its results under the all-cyclic policy.

Two further assumptions:
- Under contract-first a UI slice *builds* without waiting for non-UI dependencies such as SL-T17. That is the CF-09 rule itself, and it is the source of the T+45 start.
- Not modelled: ODR-R10-25/-26. Under v4.1.4 no ordering lets the historical list or content pass against real data.

## 3. File ownership changes

See `FILE-OWNERSHIP.json` (101 files, one owner each; checked by `OwnershipTests`). Changes against v4.1.4:
- `Views/Investigation.fs` (ui) splits into `Views/Workspace/{Frame,Overview,Hypotheses,Evidence,Links,Conclusions,Timeline,Inspector,Details,StoredRecord}.fs`.
- New `Views/{Connection,InvestigationList,States,Language,Start,Settings}.fs`.
- `Graph.fs`, `Report.fs`, `History.fs` and `Administration.fs` move from ui-2 to ui-c, ui-c, ui-b and ui-a.
- `Engine.fs`, `Routes.fs`, `Update.fs` and the frozen view types in `Projections.fs` move from ui to integration, so no UI-owned file compiles before the contracts (review R1); the save-indicator projection function moves to `Views/SaveIndicator.fs` (ui-b). Screens emit `UiIntent` only, and `Ui/UiAdapter.fs` (integration) maps it to `Msg`.
- New coordinator-controlled `Ui/Contracts/*.fs`, copied at bootstrap.
- New per-screen markup and styles `web/screens/*.html|css` (CF-08).
- New role-local fixture scenario files, so no role edits the shared fixtures.

## 4. Results (model; `schedule-inputs/bound-results.txt`)

Inputs and settings:
- Slices: the successor manifest of PR #119 (53 slices, e64b4f3).
- Setup 45 min, merge 3 min, one serial integration queue.
- Lanes: integration, domain, persistence, reviewer, plus the UI lanes of each role file.
- Capability groups:
  - Data Visible = shell, connect, catalog load, list, frame, landing, states;
  - Data Understood = kernel, fixture replay, hypotheses, evidence, links, conclusion, inspector, WDL, graph;
  - Data Editable = live write path, save states, start, save, details, round-trip.

**Same roles, both rules.** This isolates the effect of contract-first from the effect of re-splitting ownership (review R2):

| UI roles | CI | Rule | Data Visible | Data Understood | Data Editable | All slices | First non-shell UI start |
|---|---|---|---|---|---|---|---|
| Frozen v4.1.4 ownership (2 lanes) | 0 | current | T+758 | T+764 | T+800 | T+800 | T+189 |
| | 0 | contract-first | T+673 | T+682 | T+691 | T+787 | T+45 |
| | 10 | current | T+967 | T+993 | T+1042 | T+1042 | T+252 |
| | 10 | contract-first | T+894 | T+946 | T+998 | T+998 | T+45 |
| 1 UI agent | 0 | current / contract-first | T+908 / T+826 | T+914 / T+835 | T+950 / T+844 | T+950 / T+940 | T+189 / T+80 |
| | 10 | current / contract-first | T+1089 / T+1047 | T+1115 / T+1099 | T+1164 / T+1151 | T+1164 / T+1151 | T+252 / T+87 |
| 2 UI agents (FILE-OWNERSHIP two-agent variant) | 0 | current / contract-first | T+808 / T+468 | T+814 / T+477 | T+830 / T+486 | T+840 / T+582 | T+189 / T+45 |
| | 10 | current / contract-first | T+989 / T+760 | T+1015 / T+812 | T+1064 / T+864 | T+1064 / T+864 | T+252 / T+45 |
| 3 UI agents | 0 | current / contract-first | T+659 / T+364 | T+665 / T+373 | T+681 / T+382 | T+691 / T+475 | T+189 / T+45 |
| | 10 | current / contract-first | T+933 / T+708 | T+959 / T+760 | T+1008 / T+812 | T+1008 / T+812 | T+252 / T+45 |

On the approved 37-slice manifest (frozen ownership; all slices):

| Rule | 0-min CI | 10-min CI |
|---|---|---|
| Current | T+598 | T+770 |
| Contract-first | T+462 | T+709 |

What the model says, and does not say:
- **Contract-first is earlier in every same-roles pair, at both CI settings.** The first non-shell UI slice starts at T+45 instead of T+189/252.
  - With the frozen 2-lane ownership the gain is small: full completion T+1042 → T+998 at 10-min CI.
  - The two-agent split gains far more: T+1064 → T+864.
- **The review was right about the first version of these figures.** Before the dependency correction, the current rule beat contract-first at 10-min CI with the same roles (2 UI: T+791 against T+846), and the earlier tables compared against the frozen baseline only. With the dependencies the gate actually imposes, that reversal disappears. Treat this sensitivity as a warning: the result depends on the merge-dependency rules, which the optimization agent should re-derive independently.
- **Built-but-unmerged UI time rises sharply** (for example 2,036 → 8,298 slice-minutes, two agents, 10-min CI). Contract-first moves waiting from "idle" to "built, waiting for the gate". The serial integration queue then dominates, and ODR-R10-22 (speculative verification) is what would convert built work into integrated work.
- **A third UI agent** brings full completion from T+864 to T+812 at 10-min CI, and Data Editable from T+486 to T+382 at 0-min CI.
- **One UI agent is the slowest variant** under either rule.
- **Nothing fits 420 minutes at 10-min CI.** The best case is T+812.
- This model differs from schedule-model.fsx:
  - it adds the hidden merge dependencies;
  - it has no rework, no speculative queue and no per-slice overhead.
  
  Its absolute times are therefore not comparable with PR #119's. Compare only within this table.

## 4b. The same comparisons in the optimization agent's model

Results from `schedule-model.fsx` at claude/r10-optimization 4947573 (`docs/rehearsal-010-optimization/execution-readiness/CROSS-AGENT-FINDINGS.md`):
- These inputs were rebuilt from this PR; nothing of it is committed there.
- Settings: setup 45, merge 3, chain-first priority, journey merge dependencies, every cyclic edge deferred.
- Approximation: contract-first is modelled as "build after SL-T10", a little more generous than this proposal's "after UI predecessors are built".

Minutes after T0 at 0 / 5 / 10-min CI:

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

What the two models agree on:
- Contract-first pays off only with the ownership split and more UI agents. With one UI agent it is later than the current plan.
- **Three UI agents plus speculative verification (ODR-R10-22, depth 3) is the only configuration that puts Data Visible, Understood and Editable inside 420 minutes at every CI value.** This holds under the all-cyclic deferral. Full integration still does not fit: T+435–518.
- Under this proposal's minimal deferral, the same plan misses 420 at 5 and 10-min CI. **The CF-07 decision is worth more than any agent-count or ordering change.**
- Some cells are non-monotonic in CI. Read each row as a range.

## 5. Overheads (estimates, not measured)

| Item | Estimate | Basis |
|---|---|---|
| Contract preparation | Done before T0: this package | `contracts/` is 12 files. It compiles in 8.6 s from clean (`evidence/layer-a.log`) |
| Added bootstrap work at T10 | ≈5 min | copy 12 contract files, 3 fixture files and the extract, and add their Compile entries; the generator writes the Compile list once. Not measured |
| Real adapter `Ui/UiAdapter.fs` | ≈20–30 min of the integration lane, inside T17 | the sketch (`real-adapter-sketch/RealAdapter.fs`, about 140 lines) already maps Connect, navigation, Save and the seven save states. List and content wait for ODR-R10-25 |
| Contract change requests during the run | unknown | none was needed to build the two prototype screens. Each change is coordinator-versioned and notifies the consumers |
| Integration merges | +16 slices (37 → 53) if the successor split is adopted | each merge costs a gate run plus 3 minutes of merging. This is the term that dominates at 10-min CI |

## 6. Requested comparisons (optimization agent)

Please run your model (`schedule-model.fsx`) for these four plans, against both milestone sets, reporting time to Data Visible, Data Understood, Data Editable and full release acceptance, at CI 0, 5 and 10, with and without `--queue-depth 3`:

1. the current execution plan;
2. contract-first with the existing UI agent count (`schedule-inputs/roles.current-2ui.json`);
3. contract-first with two UI agents (`roles.contract-first-2ui.json`);
4. contract-first with three UI agents (`roles.contract-first-3ui.json`).

Each roles file is `indy-init.slice-roles/1`, keyed by the successor slice ids, with an extra `capabilities` block. Your loader can ignore that block.

**Model feature needed.** Your model starts a slice only when its dependencies are integrated, so it cannot express contract-first directly. It needs two per-slice dependency sets:
- `buildDependsOn`: for UI slices, `SL-T10` plus the UI predecessors, *built*;
- `mergeDependsOn`: the declared `dependsOn` plus `hidden-merge-dependencies.json`.

`tools/contract-first-bound.fsx` (about 100 lines, F#) implements exactly this rule and nothing else, as a reference. Reproduce:

```
git show origin/claude/r10-optimization:docs/rehearsal-010-optimization/proposals/slice-execution-manifest.successor.json > /tmp/succ.json
dotnet fsi proposals/r10-contract-first-ui/tools/contract-first-bound.fsx -- /tmp/succ.json \
  proposals/r10-contract-first-ui/schedule-inputs/roles.contract-first-3ui.json \
  proposals/r10-contract-first-ui/schedule-inputs/hidden-merge-dependencies.json contract-first 10
```

## 7. Risks of additional parallelism

- **Queue saturation.** More early-built UI slices compete for the one integration authority. Built-but-unmerged time grows about fourfold (§4). Without ODR-R10-22 much of the extra building waits.
- **Stale-target churn.** Every merge moves main, so each queued UI slice must re-merge and re-verify (`STALE_TARGET`). More parallel UI branches mean more of this. It is not modelled.
- **Fixture drift.**
  - Fixture behaviour, for example "Save → Saved", is not provider behaviour.
  - Layer C must still run. A UI built only against fixtures will meet real failures late: the save path, causa-data's 31 event types, the missing workspace.
  - This is limited by:
    - copying fixture data verbatim from causa-data at a pinned commit;
    - keeping fixture provider outcomes within the frozen `ProviderError`/`ConnectionState` cases.
- **Contract churn.** A mid-run contract change hits every UI agent at once. Mitigation: coordinator versioning, and no change without a consumer notice.
- **Integration-lane load.** Moving `Engine.fs`, `Routes.fs`, `Update.fs` and the adapter to integration adds work to a lane that already carries T10, T11, T17, T21, T24 and T31–T34 plus the generated items.
- **The cyclic journey bindings (CF-07)**, 22 edges in the approved plan, stall the run whatever the agent count unless the owner decides how such assertions are judged.
