# Parallel execution plan (proposal)

**Valid only through a successor package** that adopts FILE-OWNERSHIP.json and CF-09. Under v4.1.4 the frozen ownership and start rule stand, and nothing here changes Rehearsal 010.

## Roles

| Role | Owns (FILE-OWNERSHIP.json) | Successor slices | Can start | Can merge |
|---|---|---|---|---|
| **UI-A: connection and browsing** | Views/Shell, Connection, InvestigationList, States, Language, Workspace/Frame, StoredRecord, Timeline, Administration, Settings; `SM-CONNECTION`/`SM-NETWORK` (`Connection.fs`) | SL-T16, SL-T29, SL-WI-INV-LIST, SL-WI-INV-WORKSPACE-FRAME, SL-WI-GEN-UI-LANDING, -STATES, -LANGUAGE, -VERIFY | T16 after T11; the others at bootstrap | T16 per CF-07; the others after T17, EXT-DATA, T25 (and T21 for J1/J2-bound slices) |
| **UI-B: investigation editing** | Views/Start, Workspace/Details, SaveIndicator (the IF41-VIEW projection functions), ConflictReview, History | SL-WI-INV-START-FORM/-RULES/-ACTIONS, SL-WI-INV-WORKSPACE-SAVE/-DETAILS/-ROUNDTRIP, SL-T22, SL-T28 | bootstrap | after T17 and T21 (every bound assertion saves) |
| **UI-C: investigation analysis** | Views/Workspace/Overview, Hypotheses, Evidence, Links, Conclusions, Inspector, Graph, Report; `Reporting.fs` | SL-WI-INV-WORKSPACE-HYPOTHESES/-EVIDENCE/-LINKS/-CONCLUSION, SL-WI-GEN-UI-INSPECT, SL-WI-GEN-WDL, SL-T23, SL-T26 | bootstrap (T23 also needs T24's discovery output) | after T17, T21; historical content also needs ODR-R10-25 |
| **Data** (persistence) | unchanged: `src/Causa.Persistence/*`, Recordings, StatefulFake | T18, T19, T20, T25 | as today | as today |
| **Domain** | unchanged: `src/Causa.Domain/*` | T12, T13, T14, T15, T27 | as today | as today |
| **Integration** | every shared and project file; `Engine.fs`, `Routes.fs`, `Projections.fs` (frozen types), `Update.fs`, `View.fs`, `Program.fs`, `Ui/UiAdapter.fs`, `Ui/Events.fs`, `web/index.html`, theme, shared fixtures; writes `Ui/Contracts/*` at bootstrap | T10, T11, T17, T21, T24, T31–T34, generated EXT-DATA/PF/AR/VER | as today | sole merger |

The ownership rules (FILE-OWNERSHIP.json `rules`, checked by `OwnershipTests`):
- no two roles share a file;
- screens emit `UiIntent` and read `AppView` only, so no UI role edits `Update.fs`, `Engine.fs` or `View.fs`;
- markup lives in one fragment per screen (CF-08).

## How a UI agent works before the domain exists

1. **Bootstrap (T10).** Integration writes:
   - `src/Causa.App/Ui/Contracts/*.fs`, copied from `proposals/r10-contract-first-ui/contracts/` (without the linked frozen files: the bootstrap defines those);
   - `tests/Causa.TestSupport/UiFixtures/*`;
   - `View.fs`, with every view wired to its `ScreenView` case.
   
   Verified to compile: `evidence/bootstrap-compile.log`.
2. **Build the screen module.** The module is a pure `AppView -> Bindings` projection plus its `web/screens/<screen>.html` fragment. Binding and event names are the SCREEN-MANIFEST ids. The shape is shown by `prototype/screens/InvestigationListScreen.fs` and `prototype/web/index.html`.

   Two Limen rules learned in the prototype (ADVERSARIAL-REVIEW S2, S3):
   - do not put HTML `required` on inputs: the kernel calls `reportValidity()` and silently blocks the submit, and validation belongs to the engine (use `aria-required`);
   - do not echo text inputs back through `data-bind-value`: a late response overwrites what the user typed.
3. **Layer A.** Run `dotnet test`: component tests drive `FixtureBackend.backend <scenario>` through `UiIntent`s and assert on `AppView`.
4. **Layer B.** Run the screen's Playwright spec on the fixture host, with the governed journey locators, axe, the four viewports and keyboard checks.
5. **Open the slice PR** as soon as Layers A and B pass. It is reviewable, but **it does not merge** until `slice-gate` PASS, which needs its bound journey assertions live (Layer C).
6. **On integration.** Integration's `UiAdapter.fs` replaces the fixture backend in the composition. The screen module does not change: it depends only on `AppView`.

## Is three UI agents justified?

The dependency graph inside the UI work (successor slices):

```
LANDING ─┬─ INSPECT ─┐                FRAME ─┬─ HYPOTHESES ─┬─ LINKS ──────┐
         ├─ STATES ──┼─ VERIFY               ├─ EVIDENCE ───┤              ├─ ROUNDTRIP
         └─ LANGUAGE ┘                       │              ├─ DETAILS ────┤
START-FORM ─ START-RULES ─ START-ACTIONS     │              └─ CONCLUSION ─┤
T22 ─ T28                                    └─ SAVE ──────────────────────┘
```

- There are three independent chains: browsing (LANDING family), editing (START family, SAVE, T22) and analysis (HYPOTHESES/EVIDENCE family).
- Their estimates are similar. In this model each role holds 6–8 slices.
- So three agents can each stay busy *building*.

Busy is not the same as integrating sooner, though (SCHEDULE-INPUTS.md §4, model, contract-first, 10-min CI):
- one UI agent finishes at T+1151;
- two at T+864;
- three at T+812.

The step from two to three is 52 minutes. Unmodelled stale-target re-merges and per-slice overhead could consume it. Built-but-unmerged time is already about four times the current rule's, so the integration queue is the next limit.

**Recommendation:**
- **two UI agents** by default, using the FILE-OWNERSHIP two-agent variant;
- **three** if the optimization agent's model, with `--slice-overhead` and `--fail-every`, still shows a gain, and preferably with ODR-R10-22;
- **never one**.

## Merge protocol

Unchanged from the approved plan, with three additions:
- **Order.** Integration merges in the order `slice-gate` passes. UI slices built early queue as candidates. They never merge on a Layer A/B result.
- **Contract changes.** A change to `Ui/Contracts/*` is a versioned change request through the coordinator, applied by integration as its own PR before any consumer uses it.
- **Fixture additions.** New scenarios go into the role's own `Scenarios.<role>.fs`. The shared `Scenarios.fs` changes only by change request.
