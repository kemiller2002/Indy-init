# Rehearsal 7 readiness

**Verdict: NOT READY.** Gates: PASS 0, FAIL 0, BLOCKED 10, NOT_RUN 4. Machine-readable: [`readiness-gates.json`](readiness-gates.json).

**Rule.** PASS only on executed evidence for what Rehearsal 7 will consume; planned or unmerged work is never PASS. The fixes for both critical Rehearsal 6 defects are implemented and verified on their pull requests, but nothing is merged: merging is an owner action in these repositories (see AUTONOMOUS-EXECUTION.md). Rehearsal 7 has not been started.

| Gate | Requirement | State | Why |
|---|---|---|---|
| R7-PF-01 | R6 critical defects corrected | **BLOCKED** | The fixes are verified on the pull request head but not merged or released, so Rehearsal 7 cannot consume them; Rehearsal 7 must also be required to use them (R7-AMD-01). |
| R7-PF-02 | Persistence concurrency verified | **BLOCKED** | Verified in real browsers on the PR head; not merged or released. AR-035-O4's no-Web-Locks half conflicts with the fail-closed design until R7-AMD-02 is decided. |
| R7-PF-03 | Offline retry behavior verified | **BLOCKED** | Verified against FakeGitHub and in the pure core; not merged or released; behaviour against live GitHub is first observed in Rehearsal 7's early adversarial verification. The ac585ba fixes were not given a fourth review. |
| R7-PF-04 | GitHub credential separation verified | **BLOCKED** | The probe cannot run until the credentials exist. Rehearsal 6's CAUSA_GITHUB_TOKEN could write (D-09). |
| R7-PF-05 | CI starvation regression test passed | **BLOCKED** | The static regression test passes on the pull requests, which are not merged or released. The live load test (Experiment B: bursts of 5 and 15 commits, concurrent streams, a candidate during development) was not run; it needs a disposable repository. The MSB3030 parallel-build race was assessed, not reproduced. |
| R7-PF-06 | Unattended PR lifecycle verified | **BLOCKED** | A real merge without routine owner intervention was demonstrated, but the merge gate is advisory: the agent holds the signing key and the evaluate job is not required by a ruleset (UAE-R7-12). It must be re-run with the kickoff authorization (UAE-R7-06). |
| R7-PF-07 | Evidence-generation pipeline verified | **NOT_RUN** | Experiment D (a representative workflow producing its evidence automatically) was not run. The producers exist (verify-defect-closures, assertion-ratchet, release-comparison, conditor candidate-verification) but are not yet wired into a rehearsal pipeline (R7-AMD-04). |
| R7-PF-08 | Candidate verification gates passed | **NOT_RUN** | The candidate-acceptance template has not run on GitHub (Experiment E not run). |
| R7-PF-09 | Defect classes closed correctly | **BLOCKED** | The verifier correctly refuses Rehearsal 6's closures. Its first independent review was BLOCKING; the re-review of the fixes found a new false VERIFIED (N1), whose fix is in progress. The Indy-init branch is not merged. |
| R7-PF-10 | Forma contrast verified | **BLOCKED** | Not merged or released. CI on ac34a55 (inventory regeneration after a validate failure) must be green. |
| R7-PF-11 | Release comparison recorded | **NOT_RUN** | The record must be made at preflight against the kickoff set, immediately before the lock. The demonstration predates the releases this remediation needs (Arca, Forma, Conditor, Praxis), so it is not the record. |
| R7-PF-12 | Independent adversarial review passed | **NOT_RUN** | Not every package has a passing final review: the tools re-review was BLOCKING (fixes in progress), Conditor's fixes were not re-reviewed, and the security review depends on the credential probe. |
| R7-PF-13 | No unresolved critical defects | **BLOCKED** | Both critical defects stay open until the fix is merged and released, and the defect-closure verifier records a closure bound to its fix commit in Rehearsal 7. |
| R7-PF-14 | Frozen requirements and verification plan ready | **BLOCKED** | The requirements baseline cannot be re-pinned and frozen until the owner decides the amendments; approvals are owner records (RDP-003). |

## Evidence per gate

### R7-PF-01: R6 critical defects corrected (BLOCKED)

- arca#47 at ac585ba: CI green (build-and-test, browser on Chromium and WebKit, validate, foundations)
- R6-D-002: multi-tab-safety.spec.mjs (no Web Locks, seeded interleavings, restart)
- R6-D-003: SyncScheduleTests storm reproduction (naive host >50 POSTs in 60 s; schedule <=5); AdapterScheduleTests over the real GitHubStorage provider

Needs:
- Owner: merge and release arca#47
- Owner: decide R7-AMD-01

### R7-PF-02: Persistence concurrency verified (BLOCKED)

- arca#47 CI browser job green at ac585ba
- Independent review: no acknowledged save lost, including a concurrent save-versus-steal experiment (6/6)
- Local Chromium: seeds 1-3, 31 acknowledged saves, 5 fenced saves, 19 steals, 10 owner closes, 0 lost

Needs:
- Owner: merge and release arca#47
- Owner: decide R7-AMD-02

### R7-PF-03: Offline retry behavior verified (BLOCKED)

- arca#47 CI green at ac585ba; 415/415 tests locally
- Offline: 0 writes; lost writes with onLine true: bounded and never confirmed; 401/403 attempted once; 5xx retried with backoff; recovery confirmed with one ref update
- Three independent review rounds; round 3 PASS_WITH_FINDINGS (low), fixed in ac585ba; L1 documented (<=36 write requests in the first minute when every ref update fails, then decaying)

Needs:
- Owner: merge and release arca#47

### R7-PF-04: GitHub credential separation verified (BLOCKED)

- SECURITY-REVIEW.md §1: probe workflow and expectations

Needs:
- Owner: create CAUSA_GITHUB_TOKEN (read-only on the data repository) and CAUSA_WRITE_TOKEN (write to kemiller2002/causa-write-test only) as repository secrets; the agent then dispatches the probe

### R7-PF-05: CI starvation regression test passed (BLOCKED)

- conditor#122 CI green (24 checks): ci-policy rules CIP-000..008 reproduce the Rehearsal 6 starvation on its workflows and pass the corrected scaffold
- praxis#300 CI green (26 checks): starter workflow one run per ref, cancel-in-progress, 15-minute timeout
- evidence/r6-workflow-runs.tsv: the 22:22-23:54 history

Needs:
- Owner: merge and release conditor#122 and praxis#300
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
- Owner: merge and release conditor#122

### R7-PF-09: Defect classes closed correctly (BLOCKED)

- indy-init claude/rehearsal-007-remediation 41ff524: verify-defect-closures DCL-001..014; offline tests pass
- evidence/defect-closures-r6.json: Rehearsal 6 REFUSED, 8/8 closures

Needs:
- Independent re-review verdict
- Owner: merge the Indy-init pull request

### R7-PF-10: Forma contrast verified (BLOCKED)

- forma#173: A11Y-NONTEXT-CONTRAST (WCAG 1.4.11, 3:1), status tokens, Chromium spec light/dark/forced colors on renderer output
- Independent review PASS after fixes (fef95fc, af8903b)
- Rehearsal 6 outline 1.95:1 now reported by the rule

Needs:
- Owner: merge and release forma#173

### R7-PF-11: Release comparison recorded (NOT_RUN)

- evidence/release-comparison-demo.json: live demonstration 2026-10-29T11:08Z, praxis 3.23.0, forma 0.11.0, arca 0.6.0 CURRENT, PASS

### R7-PF-12: Independent adversarial review passed (NOT_RUN)

- Arca: three rounds, final PASS_WITH_FINDINGS (low), fixed
- Conditor: PASS_WITH_FINDINGS, F1-F8 fixed (not re-reviewed)
- Forma: PASS after fixes
- Indy-init tools: first review BLOCKING (F1-F5), fixed; re-review at 41ff524 BLOCKING (N1-N3, F3 partial, N5), fixes in progress; N4 documented as an inherent limit
- Security: SECURITY-REVIEW.md; the credential probe is not run

Needs:
- Independent re-review verdicts
- Owner: credentials for the probe (R7-PF-04)

### R7-PF-13: No unresolved critical defects (BLOCKED)

- R6-D-002 and R6-D-003 fixed on arca#47 (see R7-PF-01)

Needs:
- Owner: merge and release arca#47

### R7-PF-14: Frozen requirements and verification plan ready (BLOCKED)

- input-documents/2026-10-29-R7-REM-rehearsal-6-remediation.md: requested amendments R7-AMD-01..05
- VERIFICATION-MATRIX.md, EXECUTION-PLAN.md

Needs:
- Owner: approve or decline R7-AMD-01..05

## Owner actions that unblock the gates

1. Merge and release arca#47, forma#173, conditor#122, praxis#300 and the Indy-init pull request (R7-PF-01, 02, 03, 05, 08, 09, 10, 13).
2. Approve or decline R7-AMD-01..05 (R7-PF-01, 02, 07, 14).
3. Create `CAUSA_GITHUB_TOKEN` (read-only) and `CAUSA_WRITE_TOKEN` (write to `causa-write-test` only) (R7-PF-04, 12).
4. UAE-R7-12: a ruleset on `main` requiring CI, `independent-review` and the Praxis evaluate job, and the trust store in an Actions secret (R7-PF-06).
5. A disposable repository for Experiment B (R7-PF-05).

After these, the agent runs the preflight in EXECUTION-PLAN.md §1 (release comparison before the lock, the credential probe, `ci-policy check`, `unattended-readiness` with the kickoff authorization, Experiments B, D and E) and re-assesses every gate. R7-PF-11 is NOT_RUN by design until that preflight.
