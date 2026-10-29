# Rehearsal 6 remediation: results

**Rehearsal 7 is NOT READY and was not started.** Gates R7-PF-01..14: 0 PASS, 6 BLOCKED, 8 NOT_RUN ([READINESS.md](READINESS.md)). The initiative is **not complete** against its completion criteria (below). Every code change is on a draft pull request, none is merged, and the readiness gates wait on owner actions or on Rehearsal 7 itself.

This report states results only. Plans are in [REMEDIATION-PLAN.md](REMEDIATION-PLAN.md), and verification states are in [VERIFICATION-MATRIX.md](VERIFICATION-MATRIX.md).

## Implementation results

| Package | Repository | Work item | Commit | Pull request | Verification evidence | Independent review | Final status |
|---|---|---|---|---|---|---|---|
| R7-REM-001 Cross-tab persistence (Arca library) | kemiller2002/arca | WI-0038 | ac585ba | arca#47 (draft) | Browser specs 14/14 on Chromium and WebKit in CI; seeded three-tab interleavings, no Web Locks, restart; 0 acknowledged saves lost | No loss found, including a concurrent save-versus-steal experiment (6/6) | IMPLEMENTED_IN_REVIEW; for Causa superseded by ADR-R7-001 |
| R7-REM-002 Offline retry and save state (Arca library) | kemiller2002/arca | WI-0037 | ac585ba | arca#47 (draft) | 415/415 tests; CI green. Storm reproduction: a naive host sends more than 50 POSTs in 60 s, the schedule at most 5. Adapter tests over FakeGitHub | Three rounds: BLOCKING, then findings, then PASS_WITH_FINDINGS (low); all fixed; the ac585ba fixes had no fourth review | IMPLEMENTED_IN_REVIEW; for Causa superseded by ADR-R7-001 |
| R7-REM-003 Credential separation | owner | none | none | none | Probe workflow written (SECURITY-REVIEW §1), not run | none | BLOCKED (owner credentials) |
| R7-REM-004 CI concurrency (starvation guard) | kemiller2002/conditor | WI-0063 | 9916fa9 | conditor#122 (draft) | `ci-policy check` CIP-000..008 flags Rehearsal 6's workflows and passes the corrected scaffold; 1398 checks; CI green (24 checks) | PASS_WITH_FINDINGS; F1–F8 fixed, not re-reviewed | PARTIAL: Experiment B not run |
| R7-REM-004 Starter workflow | kemiller2002/praxis | WI-0106 | 0b1ee23 | praxis#300 (draft) | CI green (26 checks) | Covered by the CIP rules | IMPLEMENTED |
| R7-REM-005 Unattended PR execution | praxis, conditor | WI-0104, WI-0062 | earlier work | earlier work | Readiness receipt: READY; PR #20 merged as 9c3aaf6 (cited) | Praxis SECURITY-REVIEW F1 accepted by the owner | PASS_ADVISORY: no enforced gate (UAE-R7-12) |
| R7-REM-006 Automated evidence | rehearsal repository | none | none | none | Experiment D not run | none | PLANNED |
| R7-REM-007 Candidate and deployment gates | kemiller2002/conditor | WI-0063 | 9916fa9 | conditor#122 (draft) | Template only; not run on GitHub | Pinning and ancestry check judged sound | PARTIAL |
| R7-REM-008 Defect-class closure | kemiller2002/indy-init | intake | see #42 | indy-init#42 (draft) | Offline tests; Rehearsal 6: REFUSED 8/8 | Round 1 BLOCKING, fixed; round 2 BLOCKING (N1), fixed in 6d05613; round 3 BLOCKING (D1-D4), fixes in progress | IMPLEMENTED_IN_REVIEW |
| R7-REM-009 Forma non-text contrast | kemiller2002/forma | WI-0041 | ac34a55 | forma#173 (draft) | Rule plus Chromium spec (light, dark, forced colours); the Rehearsal 6 outline (1.95:1) is reported | PASS after fixes | IMPLEMENTED_IN_REVIEW |
| R7-REM-010 Release comparison | kemiller2002/indy-init | intake | see #42 | indy-init#42 (draft) | Offline tests; live demonstration PASS (praxis 3.23.0, forma 0.11.0, arca 0.6.0) | Round 1 BLOCKING, fixed; round 2: N5 fixed in 07be4f2, N4 inherent and stated in the report; round 3 BLOCKING (D1-D4), fixes in progress | IMPLEMENTED_IN_REVIEW |
| R7-REM-011 Assertion ratchet (RF-F-8) | kemiller2002/indy-init | intake | see #42 | indy-init#42 (draft) | Offline tests; Rehearsal 6: FAIL, 45 unrecorded removals including both UI-034 weakenings (base read from the real kickoff record) | Round 1 BLOCKING, fixed; round 2 BLOCKING (N2, N3, F3 partial), fixed in 865d443; round 3 BLOCKING (D1-D4), fixes in progress | IMPLEMENTED_IN_REVIEW |
| R7-REM-012 Readiness package | kemiller2002/indy-init | intake | see #42 | indy-init#42 (draft) | This directory | none | NOT READY |

## Verification results

**Passed.** Each of these ran in this session; the CI results are on the pull requests:
- Arca's unit, property and adapter tests, and its browser specs.
- Conditor's CI-policy suite, including the Rehearsal 6 starvation reproduction.
- Praxis's CI.
- Forma's contrast rule, token and browser tests.
- Indy-init's offline suite, and `validate-r7-persistence-requirements.fsx` on the merged tree.

**Failed, as designed.** Rehearsal 6's own results under the new verifiers:
- `verify-defect-closures`: REFUSED.
- `assertion-ratchet`: FAIL.

**Not run:**
- the credential probe;
- Experiment B (CI load), which needs a disposable repository;
- Experiment D (evidence completeness);
- Experiment E (candidate acceptance);
- the MSB3030 parallel-build reproduction;
- `unattended-readiness` with a kickoff authorization;
- the pre-lock release comparison for the kickoff set;
- PAT-01..15.

## Correctness

**Both critical persistence defects are closed in the requirements, not yet in an application.**
- The owner-approved ADR-R7-001 removes the shared browser store (R6-D-002) and the offline queue (R6-D-003) for Causa.
- They are eliminated only when Rehearsal 7's candidate passes PAT-01, 02, 07, 08, 11, 13 and 15.

Separately, arca#47 corrects the same failure modes in Arca as a library: a cross-tab store fenced by Web Locks that refuses durable ownership without them, and bounded, classified retries. Causa no longer uses that path.

## CI

**Reproduced statically and corrected in templates. Not load-tested.**
- `conditor ci-policy check` identifies Rehearsal 6's starvation pattern from its workflows, as recorded in [CI-PERFORMANCE.md](CI-PERFORMANCE.md) and `evidence/r6-workflow-runs.tsv` (22:22 to 23:54): per-SHA concurrency groups, a single pending slot, and gated jobs that never ran.
- The scaffold templates and the Praxis starter now pass it.
- Experiment B, a live burst, was not run.

## Autonomous development

A real pull request merged without routine owner intervention:
- **Run:** probe PR #20, merged as 9c3aaf6 (Experiment A, recorded by an earlier session; cited, not re-observed).
- **Advisory, not enforced:** the agent holds the signing key, and no ruleset requires the evaluate job (UAE-R7-12).
- **Remediation PRs:** none was merged by this session (see [AUTONOMOUS-EXECUTION.md](AUTONOMOUS-EXECUTION.md)).

## Evidence

**Producers exist; automation is not demonstrated.**
- The three verifiers write machine-readable records, as do `conditor candidate-verification` and `ci-policy`.
- Wiring them into the rehearsal's acceptance before the freeze is R7-AMD-04, which is undecided.
- Experiment D was not run.

## Security

**Not passed.**
- **Arca:** final round PASS_WITH_FINDINGS (low), fixed.
- **Forma:** PASS.
- **Conditor:** PASS_WITH_FINDINGS, fixed, not re-reviewed.
- **Indy-init verifiers:** the second review was BLOCKING; fixed at eb978e2; the third review was BLOCKING (D1 lexer desync, D2 caller-chosen kickoff path, D3 edited copy as review evidence, D4 free-text tag-change reviews); fixes in progress.
- **Credentials:** the separation is unverified until the owner creates the tokens and the probe runs.
- **Release observations:** documented limit N4. They are caller-supplied; proof needs `observe` running in trusted CI with signed output.

## Readiness

**NOT READY.** See [READINESS.md](READINESS.md) and, for the persistence requirements, [READINESS-ASSESSMENT.md](READINESS-ASSESSMENT.md).

## Completion criteria

| # | Criterion | State |
|---|---|---|
| 1 | R6-D-002 independently verified as corrected | Not met: closed in requirements; PAT tests pending |
| 2 | R6-D-003 independently verified as corrected | Not met: same |
| 3 | Remaining critical application findings resolved | Not met: in requirements (ADR-R7-001); verified only in Rehearsal 7 |
| 4 | Credential permissions verified | Not met: owner credentials |
| 5 | CI starvation regression tests pass | Met in PR CI; not merged; live load not run |
| 6 | Unattended PR merging demonstrated | Met, advisory |
| 7 | Evidence generation automated | Not met |
| 8 | Candidate and deployment gates work | Not met: template not exercised |
| 9 | Defect closure tied to actual fix commits | Verifier built; third review BLOCKING, fixes in progress |
| 10 | Forma contrast failures corrected | On forma#173; not merged or released |
| 11 | Release-selection evidence before freeze | Producer built; kickoff record not made |
| 12 | Independent security review passes | Not met |
| 13 | Rehearsal 7 preflight gates pass | Not met |
| 14 | No unresolved critical defects | Not met |
| 15 | Results committed and traceable | Met for this work: commits and pull requests above |

## Remaining owner decisions

1. **Merges:**
   - forma#173, conditor#122, praxis#300, indy-init#42;
   - arca#47, a library fix.
   - Then releases of Forma, Conditor and Praxis, so that Rehearsal 7's release set contains them.
2. **Credentials:**
   - `CAUSA_GITHUB_TOKEN`, read-only on the data repository;
   - `CAUSA_WRITE_TOKEN`, write to `causa-write-test` only;
   - these unblock the probe and K6.
3. **UAE-R7-12:**
   - a ruleset on the rehearsal repository's `main` requiring CI, `independent-review` and the Praxis evaluate job;
   - the trust store in an Actions secret.
4. **Requirements:**
   - K2 and K4 of READINESS-ASSESSMENT.md;
   - approve or decline R7-AMD-03..05;
   - approve the v4 package cut (K3).
5. **Experiment B:** a disposable repository.
6. **Rehearsal 6 cleanup:** unused branches in `indy-init-rehearsal-006`. This is optional and does not affect the evidence.
