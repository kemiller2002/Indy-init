# Freezing package v4.1.5

Only the owner records decisions, approves and tags (RDP-003). `tools/owner-freeze.fsx` refuses to write when an agent runs it.

| Step | Who | What |
|---|---|---|
| 1 | owner, or a delegated-authority record if the lead's checker is accepted by the owner-record checks | Record ODR-V415-01, -02 and -03 (`proposals/owner-decision-requests.json`) with `dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1.5 --repo Indy-init`. Merge the pull request it opens. |
| 2 | agent | If the recorded approval ids differ from `APR-V415-OWNER-1`, write each beside its change in `analysis/amendment.json` (`contractChanges`) and `analysis/state-machine-amendments.json`. Then recompile v4.1.5: X01–X21 must pass, as must the offline suite's owner-record check. Have another agent record an independent review in `analysis/independent-review.json`. |
| 3 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4.1.5 --repo Indy-init` records ODR-V415-04 for the package as compiled. The script refuses while any of ODR-V415-01..03 is unrecorded. |
| 4 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4.1.5 --repo Indy-init --commit <merge commit of the ODR-V415-04 pull request>` creates `indy-init-requirements-v4.1.5`. |
| 5 | agent | Adds v4.1.5 to the frozen-package guard in CI and to AGENTS.md, and re-pins the Rehearsal 010 lock (`kickoff/rehearsal-010/toolchain.lock.proposed.json`) and launch-preflight LP-02. |

If the owner selects an option other than A for any of ODR-V415-01..03, step 2 changes the inputs to that option before compiling.

**Fallback (OD-V415-03, owner):** if the independent review has not passed by Nov 6 22:00 UTC (17:00 Eastern), launch on v4.1.4 with ODR-R10-25 option B's declared expected failures.
