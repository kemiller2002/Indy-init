# Freezing package v4.1.3

Only the owner records decisions, approves and tags (RDP-003). `tools/owner-freeze.fsx` refuses to write when an agent runs it.

| Step | Who | What |
|---|---|---|
| 1 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1.3 --repo Indy-init` records ODR-V413-04 and ODR-V413-05 (`proposals/owner-decision-requests.json`). Merge the pull request it opens. |
| 2 | agent | Recompiles v4.1.3 from the recorded options (X01–X21 must pass), records an independent review in `analysis/independent-review.json` and `independent-review.md`, and adds the v4.1.3 CI steps (`v41 --check`, contract typecheck, negative controls, bootstrap). |
| 3 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4.1.3 --repo Indy-init` records ODR-V413-06 for the package as compiled. The script refuses while ODR-V413-04 or -05 is unrecorded. |
| 4 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4.1.3 --repo Indy-init --commit <merge commit of the ODR-V413-06 pull request>` creates `indy-init-requirements-v4.1.3`. |
| 5 | agent | Adds v4.1.3 to the frozen-package guard in CI and to AGENTS.md, and the owner moves `kickoff/rehearsal-010/toolchain.lock.json` to it. |

If the owner selects an option other than A for ODR-V413-04 or -05, step 2 changes `analysis/amendment.json` to that option before compiling.
