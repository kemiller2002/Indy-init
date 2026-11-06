# CTP-001 integration policy

## 1. Event-driven, not checkpoint-driven
- **Start condition.** Integrate a group the moment both implementing sides have recorded STAGED on the **same version and hash**. Do not wait for a fixed checkpoint.
- **Evidence.** In EXP-002, integration started at T+18.4 instead of the planned T+60 (DEV-2). Holding it would only have added idle time.

## 2. The integration procedure (per group; in the competition, one slice per candidate)
1. **Verify versions.** Each side's staged hash must equal the group's latest RELEASED hash (`ctp-check integration`, CTP-I-VERSION). A mismatch is an agent-coordination defect: re-stage on the released version.
2. **Assemble.** Merge or assemble the isolated implementations. File ownership makes this conflict-free (EXP-002: 0 conflicts across 6 agent-branch merges). Any conflict is a defect against the ownership map.
3. **Compile the integrated application**, with real ports swapped in for stubs (EXP-002: one line).
4. **Run the relevant unit and contract tests.** That means the groups' UT-/UI- obligations, `ctp-check registry`, and the contract-to-code check where one exists.
5. **Run the independent browser test** (VERIFICATION-POLICY.md), on the integrated build, at **every** integration.
6. **Classify and repair defects.** Classes:
   - incorrect-contract;
   - domain-defect;
   - ui-binding-defect;
   - persistence-defect;
   - environment-tooling;
   - missing-mapping;
   - agent-coordination.

   Each defect is assigned to the owner of the files at fault, and that owner repairs it. An incorrect-contract defect becomes a CR.
7. **Record acceptance evidence** in the integration record (`CONTRACT-SCHEMA.json#/$defs/integrationRecord`): groups and hashes, acceptance run reference and author, defects with owners, and the persistence mode. Then record INTEGRATION_READY.

## 3. Full CI
- **When it runs.** Full required CI runs at each **candidate checkpoint**, meaning a state the integration coordinator intends to verify or merge, and before every protected merge.
- **What VERIFIED needs:** the candidate SHA, the CI run, and the acceptance run (CTP-L-VERIFIED). A green local or focused run never stands in for full CI.
- **Readiness for merge.** A group is not VERIFIED, and a candidate is not merge-ready, while any required check is pending, failed or cancelled. Infrastructure failures before any test ran (for example an apt mirror stall) are re-run **once**. Nobody merges past a red gate.

## 4. Push once per candidate (no self-cancelled CI)
- **What went wrong in EXP-002.** Four pushes in 4 minutes cancelled three Preflight runs before they finished (CI-F1). The session's stop hook forced a further push that cancelled a near-complete run (CI-F2, CI-F3).
- **The procedure that avoids it.** It works with the repository's per-PR concurrency group (`cancel-in-progress` for pull_request events):
  1. Agents commit and push to their **work branches**, which have no pull request, so the pushes trigger no PR workflow. That still satisfies a "push before waiting" stop hook.
  2. Only the integration coordinator updates the **PR branch**, and only with a candidate, once per candidate. In the competition repository the PR branch is the slice's own `slice/SL-<id>`, because the governed gate refuses any other (STARTUP-INSTRUCTIONS §4). So a candidate is one slice: groups that became ready together are still pushed one slice branch each.
  3. Once a candidate's required CI is running, nobody updates the PR branch until that run finishes, unless the candidate is abandoned.
- **Enforcement status.**
  - `ctp-check ci` reports cancelled and superseded runs from the run log, so the waste is **measured**.
  - Steps 1–3 are a **procedure and review gate**, not an automated block. GitHub cannot refuse a push for cancelling a run. This is listed as an enforcement gap in ADOPTION-DECISIONS.md.

## 5. Real-provider persistence
- **The rule.** A group whose persistence is `durable-github` is not VERIFIED on a REST double. It needs a real write to the authorized test repository and an independent read-back (CTP-I-REAL-PROVIDER).
- **The governed source of that evidence** is `.github/workflows/live-provider-preflight.yml` (run 42077412742 passed on main with the proposed lock). A double proves component behaviour only.
