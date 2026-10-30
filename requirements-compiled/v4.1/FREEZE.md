# v4.1 freeze procedure

**Status: CANDIDATE.** v4.1 is compiled and validated, with checks X01–X14 passing. Its independent review record is in [independent-review.md](independent-review.md). It is not approved and not frozen. The compiler never reports a frozen state: [package-manifest.json](package-manifest.json) always reads `CANDIDATE`.

## Dependency on v4

The v4.1 change regenerated v4's `analysis/compiler-test-record.json` and `freeze-manifest.json` for the new compiler tree. Only `compilerTreeSha256` and the test summary changed. Any v4 owner record that names the SHA-256 of either file must name the current one.

v4.1 adds no requirement and uses v4's verification plan unchanged. It cannot be frozen before v4. The owner first completes [v4's freeze procedure](../v4/FREEZE.md). That covers ODR-V4-01..11, including the topology choice ODR-V4-07.

## What the owner does to freeze v4.1

1. **Freeze v4** ([../v4/FREEZE.md](../v4/FREEZE.md)).
2. **Decide ODR-V41-02 to ODR-V41-05** in [proposals/owner-decision-requests.json](proposals/owner-decision-requests.json). Write each decision as a change in an operator approval in `kickoff/rehearsal-007/approvals.causa.json`, in v4's record format, and commit it yourself.
   - **If ODR-V41-03 is declined:** the agent removes the dependency changes and waivers from `analysis/task-adjustments.json` and recompiles.
   - **If ODR-V41-04 is declined:** the agent restores v4's estimates and recompiles.
   - **If ODR-V41-02 is declined:** the interface freeze waits until FSHARP-TYPE-SHAPES sections 42 and 46 are reconciled.
3. **If v4's freeze changed anything v4.1 reads,** recompile v4.1 (`requirements-compiler v41 ...`). That includes an amendment in the plan, the state machines, the tasks or the policy. Then run a fresh independent review and record it.
4. **Write the ODR-V41-01 record.** It names the SHA-256 of `package-manifest.json` and of `analysis/independent-review.json`. Approve the pull request that carries it on GitHub.
5. **After the merge, tag the merge commit `indy-init-requirements-v4.1.0`.**

Until then, the November 7 build may follow v4.1 as guidance, but it must report v4.1 as unapproved.
