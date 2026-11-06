# Freezing package v4.1.6

Only the owner records decisions, approves and tags (RDP-003). `tools/owner-freeze.fsx` refuses to write when an agent runs it.

| Step | Who | What |
|---|---|---|
| 1 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1.6 --repo Indy-init` records ODR-V416-01, -RC1, -RC3..-RC7, -S1..-S9, -CAT and -FSCORE, which are section A of the decisions sheet, plus -WDL013 and -SCOPE-SELECTORS. Each request shows its recommended option, which is the one accepted in session. ODR-V416-CAT pins the successor catalog. Merge the pull request it opens. In the same window, `decide competition` records section B (`docs/v416-competition-decisions`). |
| 1b | owner (causa-data) | Merge the causa-storage 1.1.0 pull request and tag `causa-storage-v1.1.0`. |
| 2 | agent | Apply ODR-V416-WDL013 (the WDL-013 plan entry and its SU-BROWSER-CHROMIUM binding). Write the recorded approval ids into the three `contractChanges` (00 and 02 take ODR-V416-01's, 07 takes ODR-V416-RC6's), and write `bootstrap.fsharpCore` {version 10.1.402, decision ODR-V416-FSCORE, approval} with ODR-V416-FSCORE's approval id and recompile until X01-X21 all pass. Add the v4.1.6 owner-record check to the offline suite. Another agent records an independent review in `analysis/independent-review.json`. |
| 3 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4.1.6 --repo Indy-init` records ODR-V416-02. It refuses while any ODR-V416 decision is unrecorded. |
| 4 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4.1.6 --repo Indy-init --commit <merge commit of the ODR-V416-02 pull request>`. |
| 4b | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- ratify v4.1.6-catalog --repo Indy-init` writes the successor catalog ODR-V416-CAT recorded. |
| 5 | agent | Add v4.1.6 to the frozen-package guard and AGENTS.md. Recompile the Rehearsal 010 scope against v4.1.6 and the ratified catalog. Re-pin `kickoff/rehearsal-010/toolchain.lock.proposed.json` and launch-preflight LP-02. |
| 6 | owner | Re-record ODR-R10-01 (the scope) and commit the lock (ODR-RG-04) before the 7 November kickoff. |

If ODR-V416-01 is declined, v4.1.6 is withdrawn and the competition builds on v4.1.5. If ODR-V416-RC6 is declined, the 07 change is taken out and v4.1.6 is recompiled.
