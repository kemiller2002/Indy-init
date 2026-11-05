# Rehearsal 010: negative controls for change-aware selection

`dotnet fsi tools/r10/change-impact.fsx -- --indy-init . test` runs the cases below. All 22 pass, and the suite runs in Preflight tooling CI.

| # | Change | Must select | Why it is adversarial |
|---|---|---|---|
| 1 | (model check) the project graph | Domain's dependents include `Causa.App`, `Causa.Host`, `Causa.App.Tests` | Catches a graph that silently failed to load. A first version keyed projects by `name` instead of `id`, so the production edges were empty. Cases 2–14 still passed, because test projects reference Domain directly. This case was added to catch exactly that |
| 2 | `src/Causa.Domain/Entities.fs` (shared F# record) | every test project, plus historical replay | A shared type must invalidate every dependent |
| 3 | `src/Causa.Persistence/GitHubRequests.fs` | `SU-DOTNET-LIVE`, `SU-BROWSER-LIVE`, and **not** domain unit tests | A provider change must reach the real-provider tests |
| 4 | `src/Causa.Domain/Evolve.fs` | historical replay | Replay changes must reach catalog replay |
| 5 | `src/Causa.App/Views/Dashboard.fs` | traceability, both browsers | Field identifiers live in views |
| 6 | `web/causa.css` | every browser suite, live browser, design contract | A shared stylesheet or Forma change reaches every screen |
| 7 | `src/Causa.App/Views/Report.fs` | **not** domain or persistence unit tests | An unrelated screen must not trigger unrelated tests |
| 8 | `src/Causa.App/NewThing.fs` (unknown) | **everything** | Fail-safe for files outside the manifest |
| 9 | a frozen contract file | everything | Contracts change what every test means |
| 10 | a project file | everything | Compile lists and references |
| 11 | a view plus a shared record | the union: every dependent | Mixing changes cannot dilute the selection |
| 12 | `./src\Causa.Domain\Evolve.fs` | historical replay | Path spelling cannot dodge a rule |
| 13 | no change | nothing, and "verified: nothing" | Not selecting is not verifying |
| 14 | every case | the rendered output never claims verification | – |
| 15 | control: the replay rule removed | case 4 would fail | Shows the test can bite |
| 16 | `src/Causa.Host/Host.cs` (process entry) | browser suites, or everything; never nothing | Review RVO-6-1: it selected nothing |
| 17 | `verifier/Causa.Acceptance.Independent/…` (a project the graph lacks) | everything | Review RVO-6-1 |
| 18 | `src/Causa.App/Views/NewScreen.fs` (a new file, in no compile list) | everything | Review RVO-6-1: it selected browsers only |
| 19 | `src/Causa.App/Program.fs` (composition) | the browser suites, including live | Review RVO-6-1 |
| 20 | **every** file in the ownership manifest | at least one test or everything | Exhaustive: no known path may select nothing |
| 21 | `.github/workflows/ci.yml` | everything, through the governance rule | Review RVO-7-4: the leading dot was stripped |
| 22 | `verification/journeys/../../src/Causa.Domain/Entities.fs` | every dependent | Review RVO-7-4: `..` was not resolved |

**What these controls do not cover.**
- Semantic coupling not visible in paths, for example a JSON fixture read by name at run time. Unknown paths select everything, but a *known* file with an unmodelled reader would be under-selected. **This is why Level 2 never replaces Level 3.**
- Generated test files: they are in the ownership manifest, so they select their own test project.
