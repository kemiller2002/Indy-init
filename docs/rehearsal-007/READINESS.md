# Rehearsal 7 readiness

**Verdict: NOT READY.** Gates: PASS 3, FAIL 0, BLOCKED 2, NOT_RUN 9. Machine-readable: [`readiness-gates.json`](readiness-gates.json).

**Rule.** PASS only on executed evidence for what Rehearsal 7 will consume; planned or unmerged work is never PASS. Both critical Rehearsal 6 defects are closed in the requirements by the owner-approved ADR-R7-001; they can be verified only by Rehearsal 7's PAT tests, so their gates are NOT_RUN. The remediation pull requests were merged on the owner's instruction on 2026-10-29, and the components were released and selected in echelon-current 1.33.0 (owner-approved, echelon-registry#104). This assessment complements the persistence-requirements [READINESS-ASSESSMENT.md](READINESS-ASSESSMENT.md) (K1-K7), which it does not repeat. Rehearsal 7 has not been started.

| Gate | Requirement | State | Why |
|---|---|---|---|
| R7-PF-01 | R6 critical defects corrected | **NOT_RUN** | Closed in the requirements only. Correction is verified when Rehearsal 7's candidate passes PAT-01, 02, 07, 08, 11, 13 and 15; nothing can show it before the application exists. |
| R7-PF-02 | Persistence concurrency verified | **NOT_RUN** | Run during Rehearsal 7. The Arca multi-tab browser evidence (arca#47) verifies the library, which Causa no longer uses for investigations. |
| R7-PF-03 | Offline retry behavior verified | **NOT_RUN** | Run during Rehearsal 7. Arca's SyncSchedule (arca#47) bounds retries for Arca's own queue; Causa has no queue. |
| R7-PF-04 | GitHub credential separation verified | **PASS** | Real requests against GitHub show CAUSA_GITHUB_TOKEN reads causa-data and cannot write it, and CAUSA_WRITE_TOKEN writes only causa-write-test (refused on causa-data). Rehearsal 6's D-09 (a read token that could write) does not recur. The probe ran from a temporary branch so 008's main keeps its starting state. |
| R7-PF-05 | CI starvation regression test passed | **PASS** | The static regression test passes and is in released components that Rehearsal 7 consumes. Separately, the live load test (Experiment B, VM-019) was not run and stays NOT_RUN in the verification matrix; the MSB3030 parallel-build race was assessed, not reproduced. |
| R7-PF-06 | Unattended PR lifecycle verified | **BLOCKED** | A real merge without routine owner intervention was demonstrated, but the merge gate is advisory: the agent holds the signing key and the evaluate job is not required by a ruleset (UAE-R7-12). It must be re-run with the kickoff authorization (UAE-R7-06). |
| R7-PF-07 | Evidence-generation pipeline verified | **NOT_RUN** | Experiment D (a representative workflow producing its evidence automatically) was not run. The producers exist (verify-defect-closures, assertion-ratchet, release-comparison, conditor candidate-verification) but are not yet wired into a rehearsal pipeline (R7-AMD-04). |
| R7-PF-08 | Candidate verification gates passed | **NOT_RUN** | The candidate-acceptance template has not run on GitHub (Experiment E not run). |
| R7-PF-09 | Defect classes closed correctly | **NOT_RUN** | The verifier is merged, reviewed, and correctly refuses Rehearsal 6's closures. Rehearsal 7's own defect classes can only be closed, and this gate passed, against fix commits in Rehearsal 7's history before its freeze. |
| R7-PF-10 | Forma contrast verified | **PASS** | Verified and released in the components Rehearsal 7 consumes. The Rehearsal 7 application must still pass its own UI-016 contrast obligations (DEC-R7-002 applies to text-paired amber). |
| R7-PF-11 | Release comparison recorded | **NOT_RUN** | The record must be made at preflight against the kickoff set, immediately before the lock. The demonstration predates the releases this remediation needs (Arca, Forma, Conditor, Praxis), so it is not the record. |
| R7-PF-12 | Independent adversarial review passed | **NOT_RUN** | The Indy-init tools' final review is PASS_WITH_FINDINGS with every finding fixed, Arca's PASS_WITH_FINDINGS (low, fixed) and Forma's PASS. The credential probe passed (R7-PF-04). Conditor's F1-F8 fixes were not re-reviewed, so this gate stays NOT_RUN. |
| R7-PF-13 | No unresolved critical defects | **NOT_RUN** | No Rehearsal 7 candidate exists. The defect-closure verifier must record each closure against a fix commit in Rehearsal 7's history. |
| R7-PF-14 | Frozen requirements and verification plan ready | **BLOCKED** | The compiled package v4 is not cut (K3: the R7 compile with the v3 curation fails V09, V11, V17, V18), the four-case causa-data catalog that DEC-R7-006 requires is not published (K7), and R7-AMD-03..05 are undecided. Approvals are owner records (RDP-003). |

## Evidence per gate

### R7-PF-01: R6 critical defects corrected (NOT_RUN)

- ADR-R7-001 (owner-approved, Indy-init #41): R6-D-002 and R6-D-003 removed by design: no shared browser store, no offline queue, no cross-tab locking (DEFECT-TRACEABILITY.md)
- Baseline re-pinned as BL-CAUSA-INDY-INIT-8445ca11986f after the owner decisions DEC-R7-001..007; tools/validate-r7-persistence-requirements.fsx PASS 10/10 with 15/15 planted defects caught (re-run on this branch: PASS)
- Library fixes in arca#47 (CI green at ac585ba) for Arca's other consumers; not used by Causa

Needs:
- Rehearsal 7 PAT results

### R7-PF-02: Persistence concurrency verified (NOT_RUN)

- Requirements: PERSIST-002..004, 006, 013 (tabs are independent GitHub writers resolved by expected-head commits)
- PAT-01, PAT-02 (live, PERSIST-025), PAT-11 defined

### R7-PF-03: Offline retry behavior verified (NOT_RUN)

- Requirements: PERSIST-010, 016, 018 (no offline queue; nothing sent automatically on reconnect; bounded mutations and reconciliation reads)
- PAT-07, PAT-08 defined

### R7-PF-04: GitHub credential separation verified (PASS)

- Credential probe (SECURITY-REVIEW.md section 1) run against the secrets on kemiller2002/indy-init-rehearsal-008: run 41185232453 (2026-10-31), artifact credential-probe
- readGet 200, readWrite 403, writeTest 201, writeCleanup 204, writeElsewhere 403

### R7-PF-05: CI starvation regression test passed (PASS)

- Released in Conditor 0.10.18 (v0.10.18, 2d6ae48e; conditor#122 merged as 9241290): `conditor ci-policy check` rules CIP-000..008 reproduce the Rehearsal 6 starvation on its workflows and pass the corrected scaffold; CI green
- Released in Praxis 3.24.0 (v3.24.0, dccf8400; praxis#300 merged as 4e9993e): the starter workflow runs once per ref, cancels superseded runs, 15-minute timeout
- Both in echelon-current 1.33.0, approved by the owner (echelon-registry#104, 7a2c27a)
- evidence/r6-workflow-runs.tsv: the 22:22-23:54 history

Needs:
- Owner: a disposable repository for Experiment B (VM-019)

### R7-PF-06: Unattended PR lifecycle verified (BLOCKED)

- praxis docs/autonomous-execution/evidence/2026-10-29/readiness-receipt.json: conditor unattended-readiness READY, probe PR #20 merged as 9c3aaf6 (cited; this session could not re-observe the repository)

Needs:
- Owner: UAE-R7-12 ruleset on main and trust store in an Actions secret

### R7-PF-07: Evidence-generation pipeline verified (NOT_RUN)

- None.

Needs:
- Owner: decide R7-AMD-04 and R7-AMD-05

### R7-PF-08: Candidate verification gates passed (NOT_RUN)

- candidate-acceptance.yml template released in Conditor 0.10.18 (pinned SHA, ancestry check); reviewed
- Rehearsal 6 deployed-smoke proved served SHA = candidate

### R7-PF-09: Defect classes closed correctly (NOT_RUN)

- indy-init main (merged with #42): verify-defect-closures DCL-001..014; offline tests pass in CI
- evidence/defect-closures-r6.json: Rehearsal 6 REFUSED, 8/8 closures
- Independent review: five rounds; final PASS_WITH_FINDINGS with every finding fixed at 2e4b561; the heuristic-lexer limit is stated (reviewAttention files must be read by the independent review)

Needs:
- Rehearsal 7: verify-defect-closures VERIFIED before the freeze

### R7-PF-10: Forma contrast verified (PASS)

- Released in Forma 0.12.0 (v0.12.0, 38ba6701; forma#173 merged as 6c344f6): A11Y-NONTEXT-CONTRAST (WCAG 1.4.11, 3:1), status tokens, Chromium spec light/dark/forced colors on renderer output; CI green
- Independent review PASS after fixes (fef95fc, af8903b)
- Rehearsal 6 outline 1.95:1 now reported by the rule
- npm consumers: @echelon-foundry/design-system 0.12.0 and @echelon-foundry/forma-workflow 1.2.0 (forma#175)
- Forma 0.12.0 is in echelon-current 1.33.0, approved by the owner (echelon-registry#104, 7a2c27a)

### R7-PF-11: Release comparison recorded (NOT_RUN)

- evidence/release-comparison-demo.json: live demonstration 2026-10-29T11:08Z, praxis 3.23.0, forma 0.11.0, arca 0.6.0 CURRENT, PASS

### R7-PF-12: Independent adversarial review passed (NOT_RUN)

- Arca: three rounds, final PASS_WITH_FINDINGS (low), fixed
- Conditor: PASS_WITH_FINDINGS, F1-F8 fixed (not re-reviewed)
- Forma: PASS after fixes
- Indy-init tools: first review BLOCKING (F1-F5), fixed; re-review BLOCKING (N1-N3, F3 partial, N5), fixed at eb978e2; third review BLOCKING (D1-D4), fixed at c08c23e; fourth BLOCKING (E1), fixed; fifth PASS_WITH_FINDINGS, fixed at 2e4b561; N4 documented as an inherent limit
- Security: SECURITY-REVIEW.md; credential probe PASS (indy-init-rehearsal-008 run 41185232453)

Needs:
- Independent re-review of Conditor's F1-F8 fixes (conditor#122)

### R7-PF-13: No unresolved critical defects (NOT_RUN)

- R6-D-002 and R6-D-003: closed in requirements by ADR-R7-001; open until PAT tests pass
- defect-closures-r6.json: Rehearsal 6's own closures REFUSED 8/8

Needs:
- Rehearsal 7 PAT results and verify-defect-closures VERIFIED

### R7-PF-14: Frozen requirements and verification plan ready (BLOCKED)

- Baseline BL-CAUSA-INDY-INIT-8445ca11986f pinned after DEC-R7-001..007 (K1, K2, K4 done; READINESS-ASSESSMENT.md)
- VERIFICATION-MATRIX.md, EXECUTION-PLAN.md
- Requested amendments R7-AMD-03..05 (R7-AMD-01/02 withdrawn as superseded by ADR-R7-001)

Needs:
- Owner: approve or decline R7-AMD-03..05
- Agent then owner: K3 v4 cut
- K7: publish the four-case catalog in causa-data (DEC-R7-006)

## Owner actions that unblock the gates

1. Done on 2026-10-29 on the owner's instruction: the remediation PRs are merged; Arca 0.7.0, Forma 0.12.0 (and forma-workflow 1.2.0), Praxis 3.24.0 and Conditor 0.10.18 are released; echelon-current 1.33.0 selects them, approved by the owner (echelon-registry#104).
2. Approve or decline R7-AMD-03..05, approve the v4 cut (K3), and publish the four-case causa-data catalog (K7, DEC-R7-006); K1, K2 and K4 are done (R7-PF-07, 14).
3. Done: both tokens exist on kemiller2002/indy-init-rehearsal-008 and the credential probe passed (run 41185232453; R7-PF-04, K6).
4. UAE-R7-12: a ruleset on `main` requiring CI, `independent-review` and the Praxis evaluate job, and the trust store in an Actions secret (R7-PF-06).
5. A disposable repository for Experiment B (R7-PF-05).

After these, the agent runs the preflight in EXECUTION-PLAN.md §1 (release comparison before the lock, the credential probe, `ci-policy check`, `unattended-readiness` with the kickoff authorization, Experiments B, D and E) and re-assesses every gate. R7-PF-01, 02, 03, 11 and 13 are NOT_RUN by design until the preflight and the timed run.
