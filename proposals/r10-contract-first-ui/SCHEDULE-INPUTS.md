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
- T17's earliest start is T+94 (`execution-dag.json`). With CI and merge time, the first non-shell UI slice starts at T+189 (0-min CI) or T+253 (10-min CI) in this model.
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

**Merge condition.** Unchanged: `slice-gate` PASS. Its journeys rule makes these hidden merge dependencies real (`schedule-inputs/hidden-merge-dependencies.json`):

| Bound assertions | Also need integrated |
|---|---|
| J1-02, J3-01, J3-02 | SL-WI-GEN-EXT-DATA (Connect) |
| J1-03..05, J2-01..08, J3-03, J3-04, J3-06, J3-07 | SL-WI-GEN-EXT-DATA, SL-T21 (the run starts and saves an investigation) |
| J1-06, J2-09, J3-05, J3-09 | SL-WI-GEN-EXT-DATA, SL-T21, SL-T25 (fresh context lists and reopens) |

These rules are applied to **both** policies, so the current plan is not credited with merges its gate would refuse.

They expose CF-07: SL-T16 and SL-T19 have cyclic merge dependencies. The model defers those six edges in every run and says so.

Not modelled: ODR-R10-25/-26. Under v4.1.4 no ordering lets the historical list or content pass against real data.

## 3. File ownership changes

See `FILE-OWNERSHIP.json` (101 files, one owner each; checked by `OwnershipTests`). Changes against v4.1.4:
- `Views/Investigation.fs` (ui) splits into `Views/Workspace/{Frame,Overview,Hypotheses,Evidence,Links,Conclusions,Timeline,Inspector,Details,StoredRecord}.fs`.
- New `Views/{Connection,InvestigationList,States,Language,Start,Settings}.fs`.
- `Graph.fs`, `Report.fs`, `History.fs` and `Administration.fs` move from ui-2 to ui-c, ui-c, ui-b and ui-a.
- `Engine.fs`, `Routes.fs` and `Update.fs` move from ui to integration. Screens emit `UiIntent` only, and `Ui/UiAdapter.fs` (integration) maps it to `Msg`.
- New coordinator-controlled `Ui/Contracts/*.fs`, copied at bootstrap.
- New per-screen markup and styles `web/screens/*.html|css` (CF-08).
- New role-local fixture scenario files, so no role edits the shared fixtures.

## 4. Results (model; `schedule-inputs/bound-results.txt`)

Inputs and settings:
- Slices: the successor manifest of PR #119 (`proposals/slice-execution-manifest.successor.json` at e64b4f3, 53 slices).
- Setup 45 min, merge 3 min, one serial integration queue.
- Lanes: integration, domain, persistence, reviewer, plus the UI lanes of the role file.
- Capability groups:
  - Data Visible = shell, connect, catalog load, list, frame, landing, states;
  - Data Understood = kernel, fixture replay, hypotheses, evidence, links, conclusion, inspector, WDL, graph;
  - Data Editable = live write path, save states, start, save, details, round-trip.

| Plan | CI | Data Visible | Data Understood | Data Editable | All slices | First non-shell UI start |
|---|---|---|---|---|---|---|
| Current rule, frozen ownership (2 UI lanes) | 0 | T+666 | T+589 | T+693 | T+693 | T+189 |
| | 10 | T+736 | T+762 | T+846 | T+846 | T+253 |
| Contract-first, existing UI agent count (frozen ownership) | 0 | T+557 | T+480 | T+584 | T+584 | T+45 |
| | 10 | T+669 | T+734 | T+760 | T+846 | T+45 |
| Contract-first, 1 UI agent | 0 | T+710 | T+633 | T+737 | T+737 | T+80 |
| | 10 | T+747 | T+682 | T+786 | T+806 | T+87 |
| Contract-first, 2 UI agents (FILE-OWNERSHIP two-agent variant) | 0 | T+352 | T+374 | T+433 | T+437 | T+45 |
| | 10 | T+591 | T+734 | T+760 | T+846 | T+45 |
| Contract-first, 3 UI agents | 0 | T+286 | T+331 | T+343 | T+440 | T+45 |
| | 10 | T+591 | T+734 | T+760 | T+846 | T+45 |

On the approved 37-slice manifest (all slices; frozen ownership):

| Rule | 0-min CI | 10-min CI |
|---|---|---|
| Current | T+595 | T+690 |
| Contract-first | T+453 | T+696 |

What the model says, and does not say:
- **UI idle time before the first UI slice falls by 144–208 minutes** (T+189/253 → T+45). This is the direct effect of contract-first, and it holds in every variant.
- **At 10-minute CI, full completion does not improve in any variant.**
  - 53 merges × 13 minutes ≈ 690 queue-minutes, so the serial integration queue decides.
  - UI work built early waits to merge: the built-but-unmerged UI slice-minutes rise from 1,150 to 5,192 for frozen ownership.
  - Contract-first moves the wait from "idle" to "built, waiting for the gate". It does not remove it.
- **At low CI the gain is large but conditional.** Full completion goes T+693 → T+437 (2 UI) and Data Editable T+693 → T+343 (3 UI). This needs the queue to clear fast: ODR-R10-22 speculative verification, or genuinely short CI.
- **A third UI agent helps only Data Editable at low CI** (T+433 → T+343). Full completion is unchanged (T+437 versus T+440), and at 10-min CI it gives nothing.
- The 1-UI result is slower than the frozen 2-lane baseline: one lane cannot absorb the UI work, however early it starts.
- This model differs from schedule-model.fsx:
  - it adds the hidden merge dependencies;
  - it has no rework, no speculative queue and no per-slice overhead.
  
  Its absolute times are therefore not comparable with PR #119's (for example V1 T+720 at 0-min CI). Compare only within a column.

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

- **Queue saturation.** More early-built UI slices compete for the one integration authority. Without ODR-R10-22 the extra agents mostly wait.
- **Stale-target churn.** Every merge moves main, so each queued UI slice must re-merge and re-verify (`STALE_TARGET`). More parallel UI branches mean more of this. It is not modelled.
- **Fixture drift.**
  - Fixture behaviour, for example "Save → Saved", is not provider behaviour.
  - Layer C must still run. A UI built only against fixtures will meet real failures late: the save path, causa-data's 31 event types, the missing workspace.
  - This is limited by:
    - copying fixture data verbatim from causa-data at a pinned commit;
    - keeping fixture provider outcomes within the frozen `ProviderError`/`ConnectionState` cases.
- **Contract churn.** A mid-run contract change hits every UI agent at once. Mitigation: coordinator versioning, and no change without a consumer notice.
- **Integration-lane load.** Moving `Engine.fs`, `Routes.fs`, `Update.fs` and the adapter to integration adds work to a lane that already carries T10, T11, T17, T21, T24 and T31–T34 plus the generated items.
- **The circular journey bindings (CF-07)** stall the run whatever the agent count.
