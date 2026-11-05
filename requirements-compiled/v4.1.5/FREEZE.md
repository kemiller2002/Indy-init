# Freezing package v4.1.5

Only the owner records decisions, approves and tags (RDP-003). `tools/owner-freeze.fsx` refuses to write when an agent runs it.

| Step | Who | What |
|---|---|---|
| 1 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1.5 --repo Indy-init` records ODR-V415-01 (the contract change), ODR-V415-03 (staging, which departs from ODR-R10-32 A) and ODR-V415-04 (the fallback trigger) (`proposals/owner-decision-requests.json`). Merge the pull request it opens. |
| 2 | agent | Write the recorded approval id into every empty `approval` field: the four `contractChanges` in `analysis/amendment.json`, and T-L03 and the SM-SAVE `accepted` note in `analysis/state-machine-amendments.json`. Recompile; X01–X21 must then pass with no exception. Add the v4.1.5 owner-record check to the offline suite (`tools/requirements-compiler/tests`, as ScopeTests does for v4.1.4), and make the CI steps require a full PASS (drop `pending415`). Have another agent record an independent review in `analysis/independent-review.json`. |
| 3 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4.1.5 --repo Indy-init` records ODR-V415-02 for the package as compiled. It refuses while ODR-V415-01, -03 or -04 is unrecorded. It checks that they are recorded, not which option was chosen: each choice is the owner's own act, and step 2 applies the option recorded. |
| 4 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4.1.5 --repo Indy-init --commit <merge commit of the ODR-V415-02 pull request>` creates `indy-init-requirements-v4.1.5`. |
| 5 | agent | Add v4.1.5 to the frozen-package guard in CI and to AGENTS.md. Re-pin the Rehearsal 010 lock (`kickoff/rehearsal-010/toolchain.lock.proposed.json`) and launch-preflight LP-02. |

If the owner selects option B for ODR-V415-01, v4.1.5 is withdrawn and the fallback applies. If the owner selects B for ODR-V415-03, FO-1..FO-3 and the T19/T22 split are added before step 3.

**Fallback (requested as ODR-V415-04; not recorded):** if the independent review has not passed by Nov 6 22:00 UTC (17:00 Eastern), launch on v4.1.4 with ODR-R10-25 option B's declared expected failures.
