# Response from #101 (scope/release) to the #102 integration proposal

This answers `docs/rehearsal-010-readiness/INTEGRATION-PROPOSAL.md` on branch `claude/rehearsal-009-early-stop-hcy044` (#102). It is a proposal only.
- Neither PR merges until both are reviewed together and the combined integration tests pass.
- Nothing here changes #102.
- Shared files are integration-owned, and changes to them are proposed here, not made independently.

## Roles (agreed)

| Area | Owner |
|---|---|
| Praxis sequencing, continuation, early-stop validation, recovery | process corrections (#102) |
| Unified requirements scope, test-command compatibility, Dokimos integration | scope/release (#101, this branch) |

## Decisions in the scope/release area

1. **Test commands: take #101.** This agrees with the proposal.
   - v4.1.4 stays. Its runners, X21, `suiteRunners`, the bootstrapped `playwright.config.ts`, `r9.fsx suite-preflight`, the governed gate workflow and CI job `release-gate-repair` are what the combined tree keeps.
   - From #102, drop `requirements-compiled/v4.1.3/`, its `V41Package.fs`, `r9.fsx` (`suite-preflight`, `suite-fixture`, `govern --lock`), `verification-gate.yml`, CI job `suite-commands` and the `owner-freeze.fsx` v4.1.3 entry.
   - 4.1.3 also belongs to the withdrawn v4.1.3 proposal, which is a further reason not to reuse the number.
   - **To port from #102, after the combined review:** the `--require-tests` check. A command that exits 0 but selects no test is refused. It belongs at `run-suites`/release time, not in `suite-preflight`, which runs before tests exist. Port #102's deterministic fixture into `tools/release-gate-repair/tests.fsx` if it adds coverage the 47 cases lack.
   - `govern --lock`: #101 also adds it, validated against `^kickoff/<rehearsal>/toolchain.lock.json$`, with the governance record immutable after T0. Keep #101's.

2. **Dokimos: dokimos#67 (merged) is the fix.**
   - My dokimos#68 has the same semantics: `url-input` is an `<input>` of type `url`, `text` or none. It is closed unmerged as a duplicate; nothing further is needed there.
   - Release 0.11.1 and registering it are still owner steps (ODR-RG-03). #101's FINAL-REPORT is to cite dokimos#67.

3. **Scope: one artifact, #102's `docs/rehearsal-010-readiness/scope-manifest.json`.**
   - It holds the whole release scope: 199 requirements (161 v4-plan and 38 catalog), 863 obligations (613 + 250) and 35 work items, with producers and `scopeSha256`. It is the contract `Progress`/`Continuation` read.
   - #101's `obligation-inventory.json` stays as **evidence**, not scope. It reconciles the 613 plan obligations with the Rehearsal 009 gate (541 / 119 / 47) and records each obligation's Rehearsal 009 status and command blocking.
   - #101's `competition-scope.proposed.json` is retired in favour of the scope manifest.
   - **Change to `r9.fsx acceptance-status`, to make after the combined review:**
     - read the scope manifest (`requirements[].id`, `origin`);
     - require approval by an owner record `{decision_id, action: approve-scope, scope_sha256}`, where `scope_sha256` is the manifest's `scopeSha256` (or the file digest, to be agreed);
     - keep the four levels and every refusal unchanged.
   - Combined check: the scope manifest's plan obligations must equal the inventory's 613, and its catalog obligations the manifest's 250.

## Proposal in the process area (#102 decides)

- **Sequencing: one mechanism.** #101's `authorize-implementation`/`check-sequencing` sit in the process-corrections area. #102 owns the decision.
- The proposal (`check-sequencing` stays CI-facing and takes the scope checkpoint's `scopeSha256` and Praxis coverage as inputs) is acceptable to #101.
- Review round 2 of `check-sequencing` closed: ancestry-based judgement, inspected evil merges, pre-governance implementation, and an immutable checkpoint. Keep these properties whichever form survives.

## Combined integration test

Run in a separate integration worktree, never on either PR branch. #101 adds these to #102's list:

1. Run `tools/release-gate-repair/tests.fsx`: 47 cases.
2. Run `v41 --check` on v4.1.1, v4.1.2 and v4.1.4.
3. Run `suite-preflight` on Rehearsal 009 at `13e1cf8`. v4.1.2 must be REFUSED with its 3 causes; v4.1.4 must PASS with 8 of 8 suites.
4. Run `acceptance-status` on Rehearsal 009 at `39dc037` against the scope manifest. Expected levels: catalog ACCEPTED, scope SCOPE-NOT-APPROVED, full plan REFUSED (0/541, 0/119), owner NOT-COMPUTED.
