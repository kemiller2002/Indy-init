# Competition UI ownership (workstream B)

**Basis:**
- The owner's Parallel Acceleration Directive (2026-11-05, 19:00 UTC), workstream B, coordinated by the lead/integration session.
- CF-10 A: three UI roles.
- This revises FILE-OWNERSHIP.json's ui-a, ui-b and ui-c to the directive's three areas.
- Roles (DTD-R10-004): UI-1 is held by `ui`, UI-2 by `ui-2` and UI-3 by the new `ui-3`. The v4.1.x role names stay because the compiler hard-codes `ui`.

The machine-readable form is [COMPETITION-UI-OWNERSHIP.json](COMPETITION-UI-OWNERSHIP.json): every file has exactly one owner, checked when the file was generated.

**OD-INDY-22:** only contracts and stubs reach Indy-init `main`. Implementation is written in the competition build.

| Area | Owns (exclusive) | Buildable from | Earliest integration point |
|---|---|---|---|
| **UI-1 Application shell** | `Connection.fs` (SM-CONNECTION, SM-NETWORK); `Views/Shell`, `Connection`, `InvestigationList`, `States`, `Language`, `Administration`, `Settings`; screen fragments for list, administration, settings, not-found; their tests | bootstrap | **M1 connect**: UiAdapter Connect mapping, T20, tokens (ODR-R10-28). Then **M2 browse**: v4.1.5 listing, T25, event union (OD-HC-01) |
| **UI-2 Investigation workspace** | `Views/Workspace/Frame`, `StoredRecord`, `Details`; `Views/Start`, `SaveIndicator`, `ConflictReview`, `History`; fragments for frame, start, save, details; their tests | bootstrap | **M2 open** (as UI-1). Then **M3 create**: v4.1.5 creation path, T19. Then **M4 save/reload**: T20, T21, T22 |
| **UI-3 Hypotheses and evidence** | `Reporting.fs`; `Views/Workspace/Overview`, `Hypotheses`, `Evidence`, `Links`, `Conclusions`, `Timeline`, `Inspector`; `Views/Graph`, `Report`; fragments for overview, hypotheses, evidence, causal analysis, timeline; their tests | bootstrap (the graph also needs T24) | **M5 analysis**: kernel projections over the v4.1.5 event union (T13), T21, ODR-R10-26 wire fields |
| **Integration (lead)** | `Engine.fs`, `Routes.fs`, `Projections.fs` (IF41-VIEW with the UI contracts), `Ui/UiAdapter.fs`, `Ui/Events.fs`, `Update.fs`, `View.fs`, `Program.fs`, project files, `web/index.html`, `causa.css`, theme, `build.fsx`, the shared UiFixtures | bootstrap | every milestone: swap the fixture backend for UiAdapter. Screen modules do not change |

**Rules:**
- Screens read `AppView` and emit `UiIntent` only.
- No UI area edits shared wiring or another area's files.
- A contract change goes through the lead as a versioned change.
- If only two UI agents are available, UI-3 starts after M1 and M2 are stable (directive §4).

**Verification:**

| Layer | When | What runs |
|---|---|---|
| A | per change | `dotnet test` on the area's fixture-backed tests |
| B | per screen | its Playwright spec: governed journey locators, axe-core, four viewports |
| C | at integration | `slice-gate`, with journeys on Chromium and WebKit against api.github.com, judged per ODR-R10-33 A |
