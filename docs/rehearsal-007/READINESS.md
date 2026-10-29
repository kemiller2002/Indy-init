# Rehearsal 7 readiness

**Verdict: NOT READY.** Gates: PASS 0, FAIL 0, BLOCKED 5, NOT_RUN 9. Machine-readable: [`readiness-gates.json`](readiness-gates.json).

**Rule.** PASS only on executed evidence for what Rehearsal 7 will consume; planned or unmerged work is never PASS. Both critical Rehearsal 6 defects are closed in the requirements by the owner-approved ADR-R7-001; they can be verified only by Rehearsal 7's PAT tests, so their gates are NOT_RUN. The remediation pull requests were merged on the owner's instruction on 2026-10-29; the components are not yet released. This assessment complements the persistence-requirements [READINESS-ASSESSMENT.md](READINESS-ASSESSMENT.md) (K1-K6), which it does not repeat. Rehearsal 7 has not been started.

| Gate | Requirement | State | Why |
|---|---|---|---|
| R7-PF-01 | R6 critical defects corrected | **NOT_RUN** | Closed in the requirements only. Correction is verified when Rehearsal 7's candidate passes PAT-01, 02, 07, 08, 11, 13 and 15; nothing can show it before the application exists. |
| R7-PF-02 | Persistence concurrency verified | **NOT_RUN** | Run during Rehearsal 7. The Arca multi-tab browser evidence (arca#47) verifies the library, which Causa no longer uses for investigations. |
| R7-PF-03 | Offline retry behavior verified | **NOT_RUN** | Run during Rehearsal 7. Arca's SyncSchedule (arca#47) bounds retries for Arca's own queue; Causa has no queue. |
| R7-PF-04 | GitHub credential separation verified | **BLOCKED** | The probe cannot run until the credentials exist. Rehearsal 6's CAUSA_GITHUB_TOKEN could write (D-09). |
| R7-PF-05 | CI starvation regression test passed | **BLOCKED** | The static regression test passes; conditor#122 and praxis#300 are merged (9241290, 4e9993e) but not yet released. The live load test (Experiment B: bursts of 5 and 15 commits, concurrent streams, a candidate during development) was not run; it needs a disposable repository. The MSB3030 parallel-build race was assessed, not reproduced. |
| R7-PF-06 | Unattended PR lifecycle verified | **BLOCKED** | A real merge without routine owner intervention was demonstrated, but the merge gate is advisory: the agent holds the signing key and the evaluate job is not required by a ruleset (UAE-R7-12). It must be re-run with the kickoff authorization (UAE-R7-06). |
| R7-PF-07 | Evidence-generation pipeline verified | **NOT_RUN** | Experiment D (a representative workflow producing its evidence automatically) was not run. The producers exist (verify-defect-closures, assertion-ratchet, release-comparison, conditor candidate-verification) but are not yet wired into a rehearsal pipeline (R7-AMD-04). |
| R7-PF-08 | Candidate verification gates passed | **NOT_RUN** | The candidate-acceptance template has not run on GitHub (Experiment E not run). |
| R7-PF-09 | Defect classes closed correctly | **NOT_RUN** | The verifier is merged, reviewed, and correctly refuses Rehearsal 6's closures. Rehearsal 7's own defect classes can only be closed, and this gate passed, against fix commits in Rehearsal 7's history before its freeze. |
| R7-PF-10 | Forma contrast verified | **BLOCKED** | Merged as 6c344f6 (forma#173); not yet in a Forma release. |
| R7-PF-11 | Release comparison recorded | **NOT_RUN** | The record must be made at preflight against the kickoff set, immediately before the lock. The demonstration predates the releases this remediation needs (Arca, Forma, Conditor, Praxis), so it is not the record. |
| R7-PF-12 | Independent adversarial review passed | **NOT_RUN** | The Indy-init tools' final review is PASS_WITH_FINDINGS with every finding fixed, Arca's PASS_WITH_FINDINGS (low, fixed) and Forma's PASS. Conditor's fixes were not re-reviewed, and the security review depends on the credential probe. |
| R7-PF-13 | No unresolved critical defects | **NOT_RUN** | No Rehearsal 7 candidate exists. The defect-closure verifier must record each closure against a fix commit in Rehearsal 7's history. |
| R7-PF-14 | Frozen requirements and verification plan ready | **BLOCKED** | The compiled package v4 is not cut (K3: the R7 compile with the v3 curation fails V09, V11, V17, V18), K2 and K4 owner decisions are open, and R7-AMD-03..05 are undecided. Approvals are owner records (RDP-003). |

## Evidence per gate

### R7-PF-01: R6 critical defects corrected (NOT_RUN)

- ADR-R7-001 (owner-approved, Indy-init #41): R6-D-002 and R6-D-003 removed by design: no shared browser store, no offline queue, no cross-tab locking (DEFECT-TRACEABILITY.md)
- Baseline BL-CAUSA-INDY-INIT-7d44f5546bcd pinned; tools/validate-r7-persistence-requirements.fsx PASS 10/10 with 15/15 planted defects caught (re-run on this branch: PASS)
- Library fixes in arca#47 (CI green at ac585ba) for Arca's other consumers; not used by Causa

Needs:
- Rehearsal 7 PAT results

### R7-PF-02: Persistence concurrency verified (NOT_RUN)

- Requirements: PERSIST-002..004, 006, 013 (tabs are independent GitHub writers resolved by expected-head commits)
- PAT-01, PAT-02 (live, PERSIST-025), PAT-11 defined

Needs:
- Owner: K6, the live test repository and CAUSA_WRITE_TOKEN

### R7-PF-03: Offline retry behavior verified (NOT_RUN)

- Requirements: PERSIST-010, 016, 018 (no offline queue; nothing sent automatically on reconnect; bounded mutations and reconciliation reads)
- PAT-07, PAT-08 defined

### R7-PF-04: GitHub credential separation verified (BLOCKED)

- SECURITY-REVIEW.md §1: probe workflow and expectations

Needs:
- Owner: create CAUSA_GITHUB_TOKEN (read-only on the data repository) and CAUSA_WRITE_TOKEN (write to kemiller2002/causa-write-test only) as repository secrets; the agent then dispatches the probe

### R7-PF-05: CI starvation regression test passed (BLOCKED)

- conditor#122 CI green (24 checks): ci-policy rules CIP-000..008 reproduce the Rehearsal 6 starvation on its workflows and pass the corrected scaffold
- praxis#300 CI green (26 checks): starter workflow one run per ref, cancel-in-progress, 15-minute timeout
- evidence/r6-workflow-runs.tsv: the 22:22-23:54 history

Needs:
- Owner: release Conditor and Praxis with these changes
- Owner: provide a disposable repository for Experiment B

### R7-PF-06: Unattended PR lifecycle verified (BLOCKED)

- praxis docs/autonomous-execution/evidence/2026-10-29/readiness-receipt.json: conditor unattended-readiness READY, probe PR #20 merged as 9c3aaf6 (cited; this session could not re-observe the repository)

Needs:
- Owner: UAE-R7-12 ruleset on main and trust store in an Actions secret

### R7-PF-07: Evidence-generation pipeline verified (NOT_RUN)

- None.

Needs:
- Owner: decide R7-AMD-04 and R7-AMD-05

### R7-PF-08: Candidate verification gates passed (NOT_RUN)

- conditor#122: candidate-acceptance.yml template (pinned SHA, ancestry check); reviewed
- Rehearsal 6 deployed-smoke proved served SHA = candidate

Needs:
- Owner: release Conditor

### R7-PF-09: Defect classes closed correctly (NOT_RUN)

- indy-init main (merged with #42): verify-defect-closures DCL-001..014; offline tests pass in CI
- evidence/defect-closures-r6.json: Rehearsal 6 REFUSED, 8/8 closures
- Independent review: five rounds; final PASS_WITH_FINDINGS with every finding fixed at 2e4b561; the heuristic-lexer limit is stated (reviewAttention files must be read by the independent review)

Needs:
- Rehearsal 7: verify-defect-closures VERIFIED before the freeze

### R7-PF-10: Forma contrast verified (BLOCKED)

- forma#173: A11Y-NONTEXT-CONTRAST (WCAG 1.4.11, 3:1), status tokens, Chromium spec light/dark/forced colors on renderer output
- Independent review PASS after fixes (fef95fc, af8903b)
- Rehearsal 6 outline 1.95:1 now reported by the rule

Needs:
- Owner: release Forma with this change

### R7-PF-11: Release comparison recorded (NOT_RUN)

- evidence/release-comparison-demo.json: live demonstration 2026-10-29T11:08Z, praxis 3.23.0, forma 0.11.0, arca 0.6.0 CURRENT, PASS

### R7-PF-12: Independent adversarial review passed (NOT_RUN)

- Arca: three rounds, final PASS_WITH_FINDINGS (low), fixed
- Conditor: PASS_WITH_FINDINGS, F1-F8 fixed (not re-reviewed)
- Forma: PASS after fixes
- Indy-init tools: first review BLOCKING (F1-F5), fixed; re-review BLOCKING (N1-N3, F3 partial, N5), fixed at eb978e2; third review BLOCKING (D1-D4), fixed at c08c23e; fourth BLOCKING (E1), fixed; fifth PASS_WITH_FINDINGS, fixed at 2e4b561; N4 documented as an inherent limit
- Security: SECURITY-REVIEW.md; the credential probe is not run

Needs:
- Independent re-review verdicts
- Owner: credentials for the probe (R7-PF-04)

### R7-PF-13: No unresolved critical defects (NOT_RUN)

- R6-D-002 and R6-D-003: closed in requirements by ADR-R7-001; open until PAT tests pass
- defect-closures-r6.json: Rehearsal 6's own closures REFUSED 8/8

Needs:
- Rehearsal 7 PAT results and verify-defect-closures VERIFIED

### R7-PF-14: Frozen requirements and verification plan ready (BLOCKED)

- Baseline BL-CAUSA-INDY-INIT-7d44f5546bcd pinned and validated (READINESS-ASSESSMENT.md: requirements READY)
- VERIFICATION-MATRIX.md, EXECUTION-PLAN.md
- Requested amendments R7-AMD-03..05 (R7-AMD-01/02 withdrawn as superseded by ADR-R7-001)

Needs:
- Owner: K2 and K4 decisions; approve or decline R7-AMD-03..05
- Agent then owner: K3 v4 cut

## Owner actions that unblock the gates

1. Done on 2026-10-29 on the owner's instruction: arca#47, conditor#122, praxis#300, forma#173 and indy-init#42 merged. Remaining: release Forma, Conditor and Praxis (and Arca) so Rehearsal 7's release set contains the changes (R7-PF-05, 08, 10).
2. Decide K2 and K4 of READINESS-ASSESSMENT.md, approve or decline R7-AMD-03..05, and approve the v4 cut (R7-PF-07, 14).
3. Create `CAUSA_GITHUB_TOKEN` (read-only) and `CAUSA_WRITE_TOKEN` (write to `causa-write-test` only) (R7-PF-02, 04, 12; K6).
4. UAE-R7-12: a ruleset on `main` requiring CI, `independent-review` and the Praxis evaluate job, and the trust store in an Actions secret (R7-PF-06).
5. A disposable repository for Experiment B (R7-PF-05).

After these, the agent runs the preflight in EXECUTION-PLAN.md §1 (release comparison before the lock, the credential probe, `ci-policy check`, `unattended-readiness` with the kickoff authorization, Experiments B, D and E) and re-assesses every gate. R7-PF-01, 02, 03, 11 and 13 are NOT_RUN by design until the preflight and the timed run.
