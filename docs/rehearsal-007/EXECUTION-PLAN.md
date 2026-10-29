# Rehearsal 7 execution plan

**Status: proposed. This plan does not start Rehearsal 7.** The 420-minute limit stands. The checkpoints are targets, not permission to weaken the frozen acceptance criteria. A late checkpoint is recorded as a deviation and the remaining work is reassessed; an early one means the next phase starts.

## 1. Preflight, before the clock (owner and agent)

| Step | Who | Tool | Blocks the start if |
|---|---|---|---|
| Requirements baseline re-pinned; derived package, disposition and approvals pass `praxis requirements preservation verify` | Agent; owner for approvals | Praxis (RDP-001..004) | It does not pass |
| Owner decisions K2 and K4 ([READINESS-ASSESSMENT.md](READINESS-ASSESSMENT.md)), R7-AMD-03..05, and the v4 package cut (K3) | Owner; agent for K3 | Approval records | Undecided items stay BLOCKED |
| **Release comparison recorded before the stack lock** | Agent | `requirements-compiler release-comparison observe --selected <set>` then `assess --observations <file> --locked-at <lock, with offset>` (observations at most 6 h old by default, 24 h maximum; deviations need an owner `approvedBy`) | The verdict is not PASS (VER-001-O1, VER-002-O2) |
| Credential probe | Owner creates the secrets; the agent dispatches | `SECURITY-REVIEW.md` §1 | Any probe expectation fails (D-09) |
| CI policy on the scaffolded repository | Agent | `conditor ci-policy check` | Any finding (R7-AMD-03) |
| Unattended readiness with the kickoff authorization | Agent | `conditor unattended-readiness` | Not READY (UAE-R7-06) |
| Enforced merge gate | Owner | Ruleset and secret (UAE-R7-12) | If absent, the run may start, but the report must say the gate was advisory |

## 2. The 420-minute model

| Time | Objective | Exit evidence |
|---|---|---|
| T+0 | Authorized kickoff (signed run authorization, frozen policy digest) | Kickoff record |
| T+30 | Environment and baseline verified: CI green on the scaffold, ci-policy PASS, plan-readiness READY | CI run, reports |
| T+90 | First deployed vertical slice under ADR-R7-001: one investigation saved by an expected-head commit, confirmed by read-back, and shown with the seven save states | Deployed SHA = source SHA |
| T+180 | Core workflows implemented | Bound tests green in CI |
| T+240 | Integration and adversarial verification complete (§3) | Reviewer reports |
| T+300 | Candidate selected **from a green core run of its exact SHA**; `candidate-acceptance.yml` dispatched | `conditor.candidate-verification/1` |
| T+360 | Independent review; defect-class closures and the assertion ratchet run **before the freeze** | `verify-defect-closures --base <kickoff SHA> --candidate <SHA> --records <SHA> --curation <file> --freeze-at <time>` VERIFIED; `assertion-ratchet --base <kickoff SHA> --candidate <SHA> --kickoff <kickoff record>` PASS |
| T+420 | Final deadline: computed verdict | Frozen verifiers |

## 3. Early adversarial verification (risk-based)

Each risk is tested when its capability first becomes executable, not at final acceptance. It is run by a reviewer that implemented nothing (DCC-002).

| Priority | Risk | First executable at | Test |
|---|---|---|---|
| 1 | Data loss across tabs | First persisted save (≈T+90) | PAT-01, PAT-02 (live), PAT-11: independent tabs as GitHub writers, without Web Locks; no other writer's commit overwritten |
| 2 | False remote-save confirmation | First remote save | PAT-06, PAT-12, PAT-15: Saved only after read-back; lost answers become Unknown commit outcome and are reconciled |
| 3 | Authorization failures | First remote save | Revoked or read-only credential: one attempt, Save failed with the stated cause (PERSIST-024) |
| 4 | Unsafe concurrency | Second writer (another tab, device or commit) | A conflicting GitHub update gives a conflict, never a silent overwrite; a network failure is never shown as a conflict (RF-F-3; PAT-03, PAT-05, PAT-07) |
| 5 | Test-only code in production | First deploy | Production artifact scan (AR-030) |
| 6 | Persistence corruption | First reload | PAT-10: reopening loads from GitHub; nothing is restored from browser storage |
| 7 | Missing deployment identity | First deploy | deployed-smoke: served SHA = candidate |
| 8 | Critical accessibility failures | First graph render | Forma `A11Y-NONTEXT-CONTRAST` on the app's diagram documents; computed contrast in light and dark |

**Experiment (early versus late), to be measured and not presumed.** For each defect, record when it was found (T+), the time to repair, tokens (`praxis telemetry`), CI minutes and its acceptance impact. Compare with Rehearsal 6, where the critical defects were found at final acceptance. With one uncontrolled run, report observed differences and make no causal claim (DCC-011).

## 4. Streams

| Stream | Work | Shared files |
|---|---|---|
| A, application | Persistence under ADR-R7-001 (PERSIST-001..055), save states, error classification | `src/persistence`, owned by A |
| B, infrastructure | CI tiers, candidate and deploy gates | `.github/workflows`, owned by B; checked by ci-policy |
| C, governance | Praxis authorization, receipts, attestation pull requests | `.praxis/`, `evidence/` |
| D, UI and provenance | Forma diagrams, Percepta contracts, the release record | `src/app` views |

Streams coordinate through Praxis work items. Each shared file has one owning stream, and others propose changes through it. Whether parallelism shortened the run is measured per stream, not assumed.

## 5. Carried from Rehearsal 6's review

These items are carried forward without code in this initiative:
- **RF-F-4:** the core CI step must print its test output.
- **RF-F-5, RF-F-7, RF-F-9:** UI-010's `judge` checks the stated cause, not just the absence of "Saving".
- **R1-F-3/4/5:** see REMEDIATION-BASELINE.
- **Fixtures:** PF-013-O2's recorded GitHub answer is re-recorded at preflight (fixture drift).
- **Copy:** UI-018-O2's copy (`Connected`) is fixed in the requirement or the UI with an owner decision, not by loosening the test.
