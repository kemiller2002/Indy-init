# Freezing package v4.1.2

Only the owner records decisions, approves and tags (RDP-003). `tools/owner-freeze.fsx` refuses to write when an agent runs it.

| Step | Who | What |
|---|---|---|
| 1 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1.2 --repo Indy-init` records ODR-V412-01, -02 and -03 (`proposals/owner-decision-requests.json`). Merge the pull request it opens. |
| 2 | agent | Writes the approval id of each recorded decision into `analysis/state-machine-amendments.json`, compiles v4.1.2 (X01–X20 must pass; the offline suite's owner-record check must pass) and commits the generated package. Then runs `percepta spec-preflight` on it (expected: nothing blocking), records an independent review in `analysis/independent-review.json`, and adds the CI steps of v4.1.1 for v4.1.2 (`v41 --check`, contract typecheck, negative controls, bootstrap). |
| 3 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4.1.2 --repo Indy-init` records ODR-V412-04 for the package as compiled. The script refuses while any of ODR-V412-01..03 is unrecorded. |
| 4 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4.1.2 --repo Indy-init --commit <merge commit of the ODR-V412-04 pull request>` creates `indy-init-requirements-v4.1.2`. |
| 5 | agent | Adds v4.1.2 to the frozen-package guard in CI and to AGENTS.md, as for v4.1.1. |

If the owner selects an option other than A for any of ODR-V412-01..03, step 2 changes the inputs to that option before compiling.
