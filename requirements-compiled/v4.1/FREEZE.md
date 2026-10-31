# v4.1 freeze procedure

**Status: CANDIDATE.** v4.1 is compiled and validated, with checks X01–X16 passing. The owner's decisions ODR-V41-02..08 are recorded (APR-V41-OWNER-2, #60) and applied, and ODR-V41-09 (axe-core 4.14.0) is recorded (APR-V41-OWNER-3, #62) and applied. Only ODR-V41-01, the package approval, is pending. Its independent review record is in [independent-review.md](independent-review.md). It is not approved and not frozen. The compiler never reports a frozen state: [package-manifest.json](package-manifest.json) always reads `CANDIDATE`.

## Dependency on v4

v4 is frozen at tag `indy-init-requirements-v4.0.0` (the merge of #59). `requirements-compiled/v4` is never edited after the freeze, and it is not recompiled with a later compiler: CI checks that it equals the tagged copy, and reproduces it with the tagged compiler and test suite. v4.1 applies the owner's amendments of v4 in its own inputs: the test-tooling locks (ODR-V41-07, and ODR-V41-09's axe-core) in `analysis/project-structure.json` `testTooling` (check X16), and the state-machine amendment (ODR-V41-08) in `analysis/state-machine-amendments.json` (check X12).

v4.1 adds no requirement and uses v4's verification plan unchanged. It cannot be frozen before v4. The owner first completes [v4's freeze procedure](../v4/FREEZE.md). That covers ODR-V4-01..11, including the topology choice ODR-V4-07.

## What the owner does to freeze v4.1

1. **Freeze v4** ([../v4/FREEZE.md](../v4/FREEZE.md)). Done: tag `indy-init-requirements-v4.0.0`.
2. **Decide ODR-V41-02 to ODR-V41-09** in [proposals/owner-decision-requests.json](proposals/owner-decision-requests.json). Write each decision as a change in an operator approval in `kickoff/rehearsal-007/approvals.causa.json`, in v4's record format, and commit it yourself.
   - **If ODR-V41-03 is declined:** the agent removes the dependency changes and waivers from `analysis/task-adjustments.json` and recompiles.
   - **If ODR-V41-04 is declined:** the agent restores v4's estimates and recompiles.
   - **If ODR-V41-02 is declined:** the agent revises the supersession notes in FSHARP-TYPE-SHAPES sections 42 and 46.
   - **If ODR-V41-06 is declined:** the contracts become reference text, T14 returns to authoring at kickoff, and the waivers of T13 before T14, T19 and T20 fall away.
   - **ODR-V41-07** locks the test tooling; **ODR-V41-08** amends two v4 state-machine gaps; **ODR-V41-09** locks axe-core, which the agent then adds as its own lock in `testTooling.locks`, naming ODR-V41-09 and its approval.
   - Done for ODR-V41-02..08: APR-V41-OWNER-2 (#60) selects option A for each, with the packages `xunit@2.9.2`, `xunit.runner.visualstudio@2.8.2`, `Microsoft.NET.Test.Sdk@17.11.1` and `@playwright/test@1.63.0`. The agent applied them: FL-06 draws amber with `--ef-color-status-warning` and `amberLabelled` treats that colour as amber (05); the bootstrap writes the lock (07); SM-SAVE gains T-S61, Saving + MutationSent → Saving with guard `current`, which `Engine.Update` steps when it turns the mutation request into `HostEffect.Http` and which changes nothing, and SM-LOAD's routing of transport failures through `ProviderFailed` stands (08). ODR-V41-09: APR-V41-OWNER-3 (#62) selects option A, `axe-core@4.14.0`; the agent added it as its own lock in `testTooling.locks`, and the bootstrap writes it into `package.json`.
3. **If v4's freeze changed anything v4.1 reads,** recompile v4.1 (`requirements-compiler v41 ...`). That includes an amendment in the plan, the state machines, the tasks or the policy. Then run a fresh independent review and record it.
4. **Write the ODR-V41-01 record.** It names the SHA-256 of `package-manifest.json` and of `analysis/independent-review.json`. Approve the pull request that carries it on GitHub.
5. **After the merge, tag the merge commit `indy-init-requirements-v4.1.0`.**

Until then, the November 7 build may follow v4.1 as guidance, but it must report v4.1 as unapproved.
