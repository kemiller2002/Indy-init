# Rehearsal 010: shared-file contention

## Cross-agent contention: excluded by the frozen rules

Under v4.1.4:
- a file has one primary owner, and contributors change it only through a PR the owner approves (`agent-file-ownership.json` `rules[0]`);
- integration alone writes these files (`rules[4]`, compiler checks X02 and X18):
  - the 11 project files, the solution, `Directory.*.props`, `global.json`, `NuGet.config`, package manifests;
  - `playwright.config.ts`, `.github/`, `.echelon/`, `web/index.html`, `web/causa.css`, `web/boundary.js`;
  - `View.fs`, `Program.fs` and `src/Causa.Host/`;
- every compile list is complete at bootstrap (`project-structure.json`), so no agent edits a project file to add a source file.

Counted from the manifest: 270 files, split as follows.

| Owner | Source | Test | Shared / project |
|---|---|---|---|
| integration | 3 | 67 (+2 helpers) | 29 |
| domain | 10 | 25 | – |
| persistence | 11 | 38 (+2 helpers) | – |
| ui | 11 | 58 | – |
| ui-2 | 4 | 7 | – |
| coordinator / reviewer | – | 2 | 1 |

**Measured conflict frequency: none available.**
- Rehearsal 009 had one implementing agent: 11 commits by the agent, 4 by the owner. It cannot show multi-agent conflicts.
- No earlier rehearsal with parallel lanes has a readable history in this session.
- Indy-init's own history has no commit mentioning a conflict, which is a weak signal only.
- The integration role records conflicts as they happen (`analysis/conflict-telemetry.json`), so Rehearsal 010 will produce the first measurement.

**No frequency is assumed here.**

## Contention that remains: same owner, many streams

MILESTONE-DEPENDENCIES.md §7 maps each capability stream to its files:

| File (owner) | Streams that must edit it |
|---|---|
| `Update.fs`, `Engine.fs` (ui) | connection, list and read, creation wiring, save indicator, analysis wiring |
| `Views/Investigation.fs` (ui) | read-only view, creation, save placement, analysis |
| `Projections.fs` (ui) | list, save, analysis |
| `GitHubRequests.fs`, `Classify.fs` (persistence) | connect, read, save, reconcile |
| `SaveMachine.fs` (persistence) | load declarations, save |
| `Events.fs`, `Evolve.fs` (domain) | wire mapping (historical compatibility), analysis |

These are not merge conflicts. They are **one agent doing several streams in sequence**, which is the `ui` lane's 564 lane-minutes in the model. FILE-OWNERSHIP-RECOMMENDATIONS.json gives the file splits (FO-1 to FO-3) that would let different owners take different streams. All of them need the v4.1.5 successor, because compile lists and ownership are frozen.

## Effect (model, E6 against E2, 10-min CI)

| Plan | M1 | M2 | M3 | Full |
|---|---|---|---|---|
| E2: existing optimization (R2) | 283 | 309 | 489 | 769 |
| E6: V5 ownership (FO-1) (R4) | 290 | 296 | 443 | 684 |

The ownership split mostly helps creation, editing and full completion. Connect is bound by the persistence chain (T18 → T19 → T20 → T25), not by UI files.
