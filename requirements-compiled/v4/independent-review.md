# Independent review of package v4

| | |
|---|---|
| Reviewer | A fresh, read-only Claude Code subagent. It received the sources, the artifacts and the compiler, but not the package author's conclusions. It wrote only to its own scratch directory |
| Rounds | 7. Rounds 1 to 5 were FAIL, round 6 was PASS, and round 7 (the owner's amendments applied) was PASS. Each verdict is reported as the reviewer gave it |
| Record | [`analysis/independent-review.json`](analysis/independent-review.json): every finding with its severity, the author's remediation, and the reviewer's later status. FG-09 computes its result from this record. The owner's package approval names its SHA-256 |
| Caveat | The reviewer is an agent, and so is the author of this record. The record is not an authenticated attestation (residual RV3-2). The owner's GitHub review of the freeze pull request is the authentication |

## Rounds

| Round | Reviewed | Verdict | Findings | Most important |
|---|---|---|---|---|
| 1 | `fd68ab1` | FAIL | 5 critical, 6 major, 3 minor | The freeze gate could reach FROZEN from files an agent can write. Invariant tests and WebKit runs were not bound. S14 and S15 were scheduled at 0 minutes against DEC-R7-005. FG-07 accepted any topology that fits |
| 2 | `7cd70c6` | FAIL | 2 critical, 3 major, 3 minor | A forged set of inputs still reached FROZEN. verify-plan counted a category covered if any one of its tests passed. Two state-machine transitions broke PERSIST-018 and PERSIST-024 |
| 3 | `67886ee` | FAIL | 0 critical, 2 major, 4 minor | The package approval could never match its own plan digest. The review and approval states rested on records that cannot be authenticated |
| 4 | `3dda420` | FAIL | 0 critical, 1 major, 2 minor | Approvals counted as complete while the plan did not apply the amendments |
| 5 | `8b80c69` | FAIL | 0 critical, 2 major | FG-10 read the old approvals schema. Approved invariant tests were not bound in the plan |
| 6 | `dbc9a5f` | **PASS** | none open | RV5-1 and RV5-2 confirmed end to end. Residuals are recorded and none is critical |
| 7 | `cc434fc` | **PASS** | 0 critical, 0 major, 5 minor | The owner's APR-V4-OWNER-1 applied exactly (plan content digest `1d2a6043…`); the changed test controls each still catch their defect. Minor: stale status documents (RV7-1), proposal wording in generated texts (RV7-2), FG-09 not bound to the reviewed plan (RV7-3), unchecked decision label (RV7-4), stale test comment (RV7-5); all remediated after the round |

## What the review changed

- **The compiler no longer declares a package frozen.** The best status it can report is READY-FOR-OWNER-FREEZE. The freeze itself is the owner's approving GitHub review of the freeze pull request, followed by the tag. The kickoff (T01) and acceptance gate AG-13 check both.
- **Approvals.** The gate reads the approvals schema verify-plan authorizes. It counts a decision only when:
  - a trusted operator authority wrote it;
  - the commit has no agent attribution trailers and is on origin/main;
  - the record matches the proposal exactly;
  - for amendments, the compiled plan applies it with that criterion.

  The package approval names a plan content digest that leaves out the approvals pin.
- **verify-plan.** A category is covered only when every bound ledger test declaring it passed. This change only narrows coverage, so v3's verdicts and the R5/R6 replays are unaffected.
- **Bindings.** The plan now binds:
  - every normative invariant and lesson test;
  - 72 WebKit twins of the Chromium tests;
  - verifier-run PAT-13 tests;
  - the clauses the owner approves (two-pass workflow).
- **State machines.** New transitions cover a waiting save, a waiting save dropped when the tab goes offline, a failed read on the rebase path while online (to Unknown outcome) or offline (to Offline), a failed read-back, and reconciliation after the rebase limit.
- **Contradictions routed to the owner.** Saved after a verified load (ODR-V4-08), the reading of "in flight" (ODR-V4-11), the AI-loop scope (ODR-V4-09), and acceptance of non-P0 requirements (ODR-V4-10).
- **Schedule.** The critical path T19 now follows T18. Review checkpoints T35 and T36 were added. T27 and T28 are estimated. With those corrections only the split-UI topology C fits (405 of 420 minutes).

## Residuals the reviewer accepted (none critical)

| Id | Residual | Mitigation |
|---|---|---|
| RV3-2 | Approval and review records are repository files that cannot be authenticated; a local actor can forge them | The compiler never computes frozen. Manifest states and gate rows are labelled unauthenticated. The owner's GitHub review and the tag are checked by T01 and AG-13 (RISK-13) |
| RV-10 / RV2-2 | `verifier:` rows are ordinary ledger rows | They must pass, and their independence rests on AG-07 and AG-10 |
| RV2-8 | Test-record freshness is checked only in CI | CI re-runs the suite and compares the record (`--verify-test-record`) |
| RV3-5 | After a known stale head, T-S59's "Unknown commit outcome" wording overstates the uncertainty | PAT-07 allows only Offline or Unknown outcome there. Recorded on T-S59 for the owner |
| RV6-1 | Process-record invariant tests (INV-15..19, INV-23) are not plan-bound | Their producers (the compiler suite and CI policy) run them |

## Round 7 remediation (after the round, not re-reviewed by it)

- **RV7-1:** FREEZE.md and README.md now state the state after APR-V4-OWNER-1.
- **RV7-2:** FREEZE.md lists the decided requests. A clause marked proposed whose request is listed there is normative. The generated texts keep their proposal wording (residual).
- **RV7-3:** each round verdict names the plan content digest it reviewed. FG-09 passes only when the last round is PASS and names the digest of the plan being frozen.
- **RV7-4:** W13 checks that an applied amendment's decision is the request that proposes it.
- **RV7-5:** the V4Tests header and control labels are corrected. A control with an approvals file holding no record is added.
