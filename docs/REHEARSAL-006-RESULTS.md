# Rehearsal 6 results (index for agents)

**Outcome: NOT ACCEPTED.** The pinned verifiers computed **FAILED** for the final candidate `46d5e1129a798549dcf37c2a6460c4f623298761`:
- the ledger verifier (`tools/compute-acceptance-verdict.py`): FAILED;
- the plan verifier (`requirements-compiler verify-plan`): FAILED.

The verdict comes from final-acceptance run [40803128103](https://github.com/kemiller2002/indy-init-rehearsal-006/actions/runs/40803128103), over the candidate's CI run 40798638092.

**The candidate is deployed.** Its deployed SHA equals its source SHA, and deployed-smoke proved it. Two critical defects remain open (R6-D-002, R6-D-003). The owner's final disposition is **not accepted**, stated in chat and transcribed (D-13).

**Repository:** [kemiller2002/indy-init-rehearsal-006](https://github.com/kemiller2002/indy-init-rehearsal-006). Its `main` holds the candidate and two records-only commits after it: `f64a287` and `bf190b6`.

## Key facts

| Item | Value |
|---|---|
| Requirements baseline | `BL-CAUSA-INDY-INIT-856157a78d47` (sha256 `40599abe…befd`): 115 P0 requirements, 355 obligations, no exclusions |
| Verification plan | `VP-8ef1c8dca656` (`requirements-compiled/v3`). UI-031-O5 and AR-019-O3 are verified as written (DEC-R6-011) |
| Stack | echelon-current 1.32.0. `forma-workflow` 1.1.0 is outside the set, under DF-CAUSA-2026-0601 |
| Clock | Start 2026-10-28 20:02:19 UTC (kickoff receipt). Deadline 2026-10-29 03:02:19 UTC (420 min) |
| Milestones | First deployed vertical slice at T+128 (f91d2c5). Implementation cutoff and first candidate at T+287 (02c37d1). Final candidate at T+307 (46d5e11). Computed verdict at T+353 (01:55 UTC) |
| Candidate | `46d5e11` = `02c37d1` (code frozen at the cutoff) plus one corrected unit-test expectation. 02c37d1's core job failed on that stale expectation and it was never deployed |
| Deployment | https://kevinmmiller.us/indy-init-rehearsal-006/ (GitHub Pages; the github.io URL redirects there). deployed-smoke checked the served version against the candidate |
| Gates | G0–G4 PASS. **G5–G9 FAIL** |
| Requirements (ledger) | 115 P0: **70 PASS, 19 FAIL, 26 NOT_RUN** |
| Requirements (plan verifier) | 115 P0: **58 PASS, 19 FAIL, 26 NOT_RUN, 12 UNVERIFIED**; 0 UNBOUND, 0 INVALID |
| Obligations (ledger traceability) | 355: 283 bound. **259 PASS, 24 FAIL, 72 NOT_RUN** |
| Obligations (plan verifier) | 355: **178 verified (PASS)**, 89 FAIL, 68 NOT_RUN, 20 UNVERIFIED |
| Stack lock gate (T11) | PASS: 57 PASS, 0 FAIL, 0 GAP |
| Bound tests | 283 of 283 application bindings authored under their exact identities, all discovered by `bindings-coverage` |
| Percepta | 23 of 24 in-scope screen contracts PASS at the candidate's run. S16 is NOT-APPLICABLE (DEC-R6-006) |
| Final independent review | **BLOCKING_FINDINGS** (anthropic/claude-code-reviewer): 2 critical, 4 major, 3 minor. 42 spot checks: 35 whole, 7 partial. 7 tag changes. Attested by the owner in chat (D-13), so the verifier counts it as unattested |

## What blocks ACCEPTED

The blockers are the computed ones, in the order the verifiers list them.

1. **Open critical defects (G6, and the FAIL requirements below).**
   - **R6-D-002.** Without `navigator.locks`, the shared device store is read, merged and written by several tabs at once without exclusion. One or two tabs' changes are lost on every run. AR-035-O4's no-locks half fails, and the independent reviewer reproduced it.
     - Two fallback designs were tried (a localStorage lease, and a lease with a hold); neither was correct, and neither was shipped.
   - **R6-D-003.** A save that cannot be sent at all because the device is offline is retried without backoff: 151 POSTs in 60 s for one change. The page says "Saving to GitHub" instead of "Waiting for the connection". The trigger was not found in the timebox.
2. **Other FAIL requirements (19):**
   - AR-035
   - EXT-DATA-004, EXT-DATA-005
   - PF-011, PF-013, PF-015, PF-016
   - UI-004, UI-006, UI-010, UI-015, UI-016, UI-018, UI-021, UI-031, UI-040
   - VER-001, VER-002
   - WDL-015

   They fall into these honest failures:

   | Failure | Obligations |
   |---|---|
   | The "read-only" credential can write (D-09) | PF-015-O2, EXT-DATA-004-O2, EXT-DATA-005-O1 |
   | The graph's amber outline measures 1.95:1; Forma has no outline token | UI-016-O11, PF-016-O1 |
   | No pre-lock newest-release comparison was recorded, and none can be made after the fact | VER-001-O1, VER-002-O2 |
   | No role model; verified as written under DEC-R6-011 | UI-031-O5 |
   | Offline case fails because of R6-D-003 | UI-010-O2 |
   | Tests of the run's own evidence | PF-011, PF-012, PF-018 and AR-004 (4 of 21 failed) |
   | The rest | Failing bound browser specs in shards 1 and 2 of the candidate's run |

3. **G7 defect classes: FAIL.** `acceptance/defect-classes.json` in the candidate still cites Rehearsal 5 fix commits, which are not in this repository's history. DCT-004-A no longer exists, and DCT-LIVE-A failed live. The re-review had to happen before the code freeze and did not (D-11).
4. **26 NOT_RUN requirements (G8, G9),** mostly AR-*: process conduct, independent review, owner decisions and the R3/R2 ledgers. Their evidence is review or owner records, which count only when anchored by an approving GitHub review. Under D-06, D-10 and D-13 the owner attested in chat or by merge, not by review, so those obligations stay unverified.

## Defects found during the run

| Id | Severity | Defect | State |
|---|---|---|---|
| R6-D-001 | critical | After all tabs close, a restore adopted only the page's own writer record. The page showed "Saved to GitHub (confirmed …)" for other writers' changes, which were only on the device | **Fixed** (f91d2c5, a72bc6a, 23ac222). The final review re-verified it with Web Locks |
| R1-F-1 | critical | An investigation's page showed another investigation's save state | **Fixed** (23ac222); re-verified |
| R6-D-002 | critical | No cross-tab exclusion without Web Locks; changes are lost | **Open** |
| R6-D-003 | critical | An unsendable save is retried in a tight loop and shown as "Saving to GitHub" | **Open** |
| RF-F-3 | major | A network failure on the rebase path is reported as another writer's conflict, and Discard is offered | **Open**, found by the final review |
| RF-F-8 | minor | UI-034-O2's selection assertion was removed. A weakened test, found by the final review, listed in its tag changes | **Open** |
| Build race | high (CI) | Two global-property sets of Causa.Persistence shared `obj/Release` (MSB3030) and made core red intermittently | **Fixed** (8a13e6a, daad1e5) |
| AR-030 | high | Test-only code reached the production artifact through a fail-open condition (R1-F-6) | **Fixed**: test code is compiled only when `CausaTestBuild == 'true'`, and the production artifact scan gates deploy |

## Process: what went wrong, and what was recorded

- **CI starvation, 22:22–23:54.**
  - Main used one concurrency group per commit, so superseded runs piled up about 30 deep.
  - Job-level `if: always()` made cancelled runs start their jobs anyway, so no main run completed for about 90 minutes.
  - The fixes:
    - WI-R6-A4 changed job-level conditions to `!cancelled()`;
    - WI-R6-A5 added a dispatch-only force-cancel tool;
    - the owner force-cancelled the old runs.
- **The final acceptance could not read its own records.** A commit cannot contain records that name its own SHA, so the process, owner and review obligations could never verify in the candidate's own run.
  - The fix, WI-R6-A6 and D-12: `acceptance-final.yml` reads the candidate's process records from a later records-only commit, through the frozen verifier's own `--process-events/--process-ancestry` inputs.
  - Its guard refused any change outside `.ros/`, `docs/r6/`, `evidence/` and `acceptance/reviews/final/`. The run recorded the records commit (`bf190b6`) and the 14 changed paths.
- **First candidate not deployable.** 02c37d1's core job failed on a stale copy expectation after an intentional wording change. The candidate was re-cut as 46d5e11 with only that expectation corrected, and the final review judged the change a faithful update, not a weakening.
- **Deviations D-01..D-13** are in [`docs/r6/DEVIATIONS.md`](https://github.com/kemiller2002/indy-init-rehearsal-006/blob/main/docs/r6/DEVIATIONS.md):
  - D-01: the scope review is unattested.
  - D-02: the two amendments are not applied.
  - D-03: Conditor ran without a session token.
  - D-04: the 775 equivalence reviews were transcribed.
  - D-05: the forma-workflow decision record was transcribed.
  - D-06: the kickoff was ratified by merges, not reviews.
  - D-07: Web Locks were used instead of Arca 0.6.0.
  - D-08: deploy is decoupled from the acceptance suites.
  - D-09: the read token is write-capable.
  - D-10: REVIEW-1 was attested in chat.
  - D-11: the G7 classes were not re-reviewed before the freeze.
  - D-12: records are read from a records-only descendant.
  - D-13: the final review and disposition were attested in chat.
- **Merge attribution.** The coordinating agent merged #4 through the GitHub API. GitHub attributes that merge to the owner's account (`Kevin Miller`), because the session acts on GitHub with the owner's credentials. These are agent actions under the owner's standing approval of merges, not owner clicks.
- **Unused branches.** `r6/candidate`, `r6/attest-base-review` and `r6/attest-base-final` were pushed for attestation pull requests that D-13 made unnecessary. The sandbox cannot delete refs; the owner may remove them.

## Rehearsal 5 vs Rehearsal 6

| | Rehearsal 5 | Rehearsal 6 |
|---|---|---|
| Plan | VP-e77a2134e1b0: 108 P0, 326 obligations | VP-8ef1c8dca656: 115 P0, 355 obligations (adds AR-027..AR-035) |
| Computed verdict | INCOMPLETE (no FAIL) | **FAILED** |
| P0 requirements (ledger) | 77 PASS, 0 FAIL, 31 NOT_RUN | 70 PASS, **19 FAIL**, 26 NOT_RUN |
| Obligations (ledger) | 277 PASS, 0 FAIL, 49 with no proving test | 259 PASS, 24 FAIL, 72 NOT_RUN |
| Bound obligations | 277 of 326 | 283 of 355 (every application binding authored and discovered) |
| Gates | G0–G7 PASS; G8, G9 NOT_RUN | G0–G4 PASS; G5–G9 FAIL |
| Defect classes (G7) | All closed by the independent reviewer in the run | Not closed: the closures cite R5 commits, and the re-review missed the freeze |
| Deployed | Yes; source = deployed | Yes; source = deployed |
| Verdict time | about T+394 | T+353 |
| Independent review | Two fresh reviewers (T30, T33) | REVIEW-1 (BLOCKING, 10 findings, 7 closed) and the final review (BLOCKING) |
| Owner evidence | Chat transcriptions | Chat transcriptions, now marked as such by Praxis ("transcribed", "unattested"), and kept out of the verified counts |
| Process evidence | Not machine-verified | Machine-verified by `verify-process`, from the candidate's Praxis log, verified checkpoints and CI-observed attestations |

**How to read the difference.** Rehearsal 6's FAILs are not a regression in the same measure: the bar is higher.
- **Each test had to prove the whole obligation.** R5 counted 277 obligations as passing with 0 FAIL. R6 required every binding to prove the whole obligation text under its exact identity, and two independent reviews removed or flagged partial proofs.
- **Proving more exposed more.** The stricter tests found two critical persistence defects that R5 recorded only as "known limits" (R3-F2, the non-atomic cross-tab read-merge-write, is R6-D-002), plus a write-capable credential.
- **The process is now machine-checked:** owner and review evidence counts only when it is attested by an approving GitHub review.

## Final evaluation

The owner script's own wording of these questions is not available in this session. They are reconstructed from its constraints.

| Question | Answer |
|---|---|
| Was a Causa app deployed within 420 minutes? | **Yes.** The first vertical slice was deployed at T+128. The final candidate 46d5e11 is deployed, and its served SHA equals its source SHA |
| Was it independently verified? | **Yes, and it failed.** The final independent review is BLOCKING_FINDINGS, and the pinned verifiers computed FAILED. Verification is attested in chat only (D-13), so the verifier counts the review as unattested |
| Is the verdict ACCEPTED? | **No.** It is FAILED (NOT ACCEPTED), and the owner's disposition is not accepted. Nobody overrode the verdict |
| Was evidence manufactured or a requirement weakened? | **No.** Failing tests fail honestly. For example, AR-035-O4's no-locks half and VER-001/002 have no fabricated release record. No pre-lock record was invented. One test weakening (UI-034-O2) was found by the reviewer and is in its tag changes, so it does not count |
| Were approvals or reviews fabricated? | **No.** No agent posted a GitHub review for the owner. Chat statements are recorded as transcriptions and unattested evidence (D-04, D-06, D-10, D-13). Praxis refused a non-GitHub attestation type, and none was forged |
| Did implementing agents self-certify review? | **No.** Both reviews were by `anthropic/claude-code-reviewer`, which implemented nothing, in the `review` execution role |
| Was NOT_RUN turned into PASS? | **No.** 26 requirements stay NOT_RUN and 12 UNVERIFIED |
| Was completed rehearsal evidence modified? | **No.** Rehearsal 1–5 evidence is untouched. Earlier review records are kept as history, and the final acceptance reads only the final review's file |
| Was the start backdated? | **No.** The start is the kickoff receipt, 20:02:19 UTC |
| Was Python introduced for new implementation? | **No.** New tooling is F# (causa-acceptance, the tests) and shell. The pinned Indy-init verifier `compute-acceptance-verdict.py` predates the rehearsal and is used as-is |
| What should change for Rehearsal 7? | See below |

### For Rehearsal 7

1. **Re-close the defect classes before the code freeze**, with fix SHAs in the rehearsal's own history (G7).
2. **Gate deploy and candidate selection on a green core job.** Make the CI test step print its output instead of hiding it.
3. **Keep acceptance-final's records-only mechanism.** Have the owner attest through approving GitHub reviews on bot-opened pull requests, so the 26 process requirements can verify.
4. **Use a truly read-only `CAUSA_GITHUB_TOKEN`.**
5. **Fix R6-D-002** (a cross-tab guard without Web Locks, for example adopting Arca) and **R6-D-003** (backoff for unsendable saves) before any new scope.
6. **Ask Forma for a graph outline token** that measures at least 3:1.
7. **Record the pre-lock newest-release comparison** in preflight (VER-001/002).
8. **Route the tooling gaps:**
   - Conditor sends credentials on public fetches (D-03).
   - Praxis has no attestation type for an owner's out-of-band statement.
   - GitHub merges made by the agent appear under the owner's name.
