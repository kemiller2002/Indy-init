# UAE-001: Unattended autonomous execution (Rehearsal 7)

Priority: P0
Owner system: Praxis (execution policy, run authorization, merge gate: praxis WI-0104). Readiness: Conditor (WI-0062). Verification evidence: Dokimos (WI-0014). Dependency continuation: the Praxis planner.
Target repositories: indy-init (this intake, Rehearsal 7 preparation), praxis, conditor, dokimos. The rehearsal repository is `kemiller2002/indy-init-rehearsal-007`.
Intake: Praxis document ingestion. This document is source material, not a canonical requirement.

Indy-init has no Praxis installation (no `.praxis/`). As with ESO-001, this file therefore uses the legacy `input-documents/` path, which Praxis still reads. No Praxis state is written or implied by this file.

**Status: approved, with an amendment.** The owner approved this intake on 2026-10-29 (PR #38). Rehearsal 7 is not started by this document; starting it stays the owner's decision.

**Amendment, owner decision of 2026-10-29: owner-delegated agent authority.** The owner chose not to hold the signing key. In UAE-R7-01, "the owner signs" and "no agent … signs the authorization or the trust store" are replaced as follows:

- The run authorization is signed by the **owner-delegated agent authority**, a session-scoped P-256 key the agent generates and holds.
- Its trust store is outside every repository.
- The private key is never committed.

Everything else in UAE-R7-01 still holds: scope, Category A operations, protected-class reservation, expiry, and policy digest.

Consequence: the signature records the delegation, not the owner's intent for each run. Praxis security finding F1 is **accepted**, not closed (praxis SECURITY-REVIEW.md). Until UAE-R7-12 is met, the rehearsal report must state that the gate was advisory *and* that the agent held the key.

UAE-R7-06 has been satisfied once in advance:

- `conditor unattended-readiness` reported **READY** against rehearsal-007 `main` on 2026-10-29.
- The run was `RUN-R7-READINESS-20261029-01`, and probe PR #20 merged as `9c3aaf6`.
- Receipt: praxis `docs/autonomous-execution/evidence/2026-10-29/`.

Re-run it with the kickoff authorization at kickoff.

## Evidence

The causes and the measured facts are in praxis `docs/autonomous-execution/GITHUB-APPROVAL-DIAGNOSIS.md`.

**What causes the owner to approve pull requests.** No GitHub rule requires a review. None of the nine repositories has branch protection, a ruleset or a CODEOWNERS file, and the agent's credential is admin. The approvals came from two places:

- **The agent's execution instructions.** A merge is an outward-facing action, so the agent waited for the owner's instruction each time.
- **Governance with no verifiable way to authorize in advance** (OD-INDY-7, AR-003). Agents must not interpret blanket approvals or write approval files.

**Token identity.** The agent acts under the owner's login. GitHub therefore forbids an owner review of an agent-opened pull request, and an "owner" review cannot be told apart from the agent acting under that login.

**What WI-0104 provides.** It replaces repeated chat approvals with one *verifiable* authorization:

- The owner signs a `praxis.run-authorization/1` with a P-256 key the agent never holds.
- Praxis evaluates every merge against that authorization and the policy, and fails closed.

**Proven on 2026-10-28** on the disposable branch `praxis-autonomy-e2e/base` of indy-init-rehearsal-007, using a disposable test authority:

- PRs #2 and #3 merged with no owner action and with SHA-bound receipts.
- `conditor unattended-readiness` reported **READY**.
- The negative cases were run against real GitHub. Results: praxis `docs/autonomous-execution/INTEGRATION-TEST-RESULTS.md`.

## Requirements (proposed for Rehearsal 7)

| ID | Requirement |
|---|---|
| UAE-R7-01 | **One owner kickoff authorization.** Before the clock starts, the owner signs exactly one `praxis.run-authorization/1` for the run. It covers: the rehearsal repository; `main` (plus any declared stream branches); the Category A operations (`create_branch`, `commit`, `create_pull_request`, `run_verification`, `request_automated_review`, `merge_eligible_pull_request`); and a reservation of every protected class. Its expiry is the rehearsal deadline plus at most 2 hours. The owner's public key is in a trust store outside every repository. No agent writes, edits or signs the authorization or the trust store (OD-INDY-7, AR-003). |
| UAE-R7-02 | **Frozen execution policy.** `.praxis/autonomy/policy.json` on the rehearsal repository's `main` is frozen at kickoff, and its digest is recorded in the kickoff record. It names the CI check(s) and the independent-review check as required, with `checkProducer: github-actions` and `maxCriticalDefects: 0`. It declares the frozen requirements, acceptance criteria, the verification plan and the process-evidence rules as protected paths. Any change to it is REQUIRES_OWNER, and any change breaks the signed authorization (`authorization-policy-mismatch`). |
| UAE-R7-03 | **Every routine merge goes through Praxis.** Each pull request is merged only by `praxis autonomy merge`, pinned to the verified candidate SHA. Direct pushes to `main` and merges by any other route are process defects. The run journal (decisions, receipts, events) is committed under `evidence/autonomy/journal/`. `praxis autonomy receipts verify` must pass at the final candidate. |
| UAE-R7-04 | **Independent review is its own CI job.** `praxis autonomy review` runs in a separate GitHub Actions job against the base branch's policy. It is never run in the implementing agent's session and is never represented as a human review. Its check run and the CI check must be `success` at the candidate SHA. |
| UAE-R7-05 | **Owner-controlled decisions stop, visibly.** A REQUIRES_OWNER, DENY or BLOCKED outcome stops that slice only and is recorded with its findings. The run continues other unblocked work. The final report lists every stopped decision. Nothing is silently overridden, re-scoped or retried around. |
| UAE-R7-06 | **Mandatory preflight gate.** Before Rehearsal 7 begins, `conditor unattended-readiness` must report **READY** against `kemiller2002/indy-init-rehearsal-007` `main`. It must run with the run's own owner-signed authorization and the repository's actual rules (branch → commit → PR → CI → independent review → merge → verified receipt), and its `conditor.unattended-readiness/1` receipt is kept in the kickoff record. BLOCKED or NOT_RUN means the rehearsal does not start. A local mock is never a substitute. |
| UAE-R7-07 | **Dependency continuation without the owner.** After each merge, `praxis autonomy observations --journal … --repository … --out …` converts the verified receipts (re-confirmed on GitHub) into planner observations. `praxis plan analyze --observations` then reports which dependents are released. No owner approval is needed for a routine dependency transition. |
| UAE-R7-08 | **Verification evidence bound to the candidate.** When Dokimos runs as a pull-request check, the quality gate receives `revision: ${{ github.event.pull_request.head.sha }}`, so its evidence names the candidate Praxis authorizes, not GitHub's test merge commit. |
| UAE-R7-09 | **Revocation.** The owner can stop the run at any time. Any one of these is enough: committing a `praxis.run-revocation/1` under `.praxis/autonomy/revocations/`, adding the run id to the trust store's `revokedRunIds`, or removing their key. Every later evaluation is DENY (`authorization-revoked`). |
| UAE-R7-10 | **Owner decisions are unchanged.** Kickoff decisions, acceptance, exclusions and waivers stay owner decisions under OD-INDY-7, with the rehearsal-006 attestation mechanism: approving reviews on pull requests opened by `github-actions[bot]`, observed by CI. The run authorization authorizes routine merges only. It is never an owner decision on anything else. |
| UAE-R7-12 | **The gate is enforced, not advisory.** Praxis's merge decision runs as a GitHub Actions job (`praxis autonomy evaluate … --trust-store-env`), with the owner's trust store in an Actions secret. A ruleset on `main` requires that job's check, the CI check and `independent-review`, with no human review required (praxis diagnosis D-2 and D-7). Without this, an agent-run executor can be pointed at a trust store the agent wrote (praxis SECURITY-REVIEW.md F1), and the rehearsal report must say the gate was advisory. |
| UAE-R7-11 | **Measurements.** Record the measurements below during the run. Report "unavailable" where a source is missing; never zero. |

## Rehearsal 7 execution expectations

- **One owner action after preparation:** the kickoff decision, the signed run authorization and the starting of the clock. Target: **zero routine owner interventions** after kickoff. This target is not met by suppressing any of UAE-R7-05's stops.
- **A stop is a correct outcome**, not a failure of autonomy. A REQUIRES_OWNER for a protected change, a DENY for failed CI, and a BLOCKED for a GitHub rule are all correct. The run is judged on whether such stops were legitimate, recorded and visible.

## Measurement definitions

| Metric | Definition | Source |
|---|---|---|
| Owner interventions | Owner actions after kickoff that the run waited on or that changed its course: chat instructions, reviews, merges, manual re-runs, configuration changes. A revocation counts. | The rehearsal transcript and GitHub events, by owner login outside `performed_via_github_app` |
| Owner approval requests | Times the run asked the owner to decide (REQUIRES_OWNER, BLOCKED needing a person, or explicit questions). | The journal's `decision` and `lifecycle-finished` events, plus the transcript |
| Pull requests created / merged | Pull requests opened against the rehearsal repository in the window / merged in the window. | GitHub API |
| Average PR cycle time | The mean, over merged PRs, of `merged_at − created_at`. | GitHub API |
| Time waiting for approval | The sum, over PRs, of time from the first REQUIRES_OWNER or BLOCKED outcome until the next ALLOW, or until the window ends. | Journal events |
| CI retries | Workflow runs with `run_attempt > 1` in the window. | Actions API |
| Merge retries | `retry` events with `call=merge`, plus reconciled receipts. | Journal events and receipts |
| Unauthorized operations prevented | Decisions with result DENY carrying a gate finding (`authorization-*`, `*-out-of-scope`, `operation-not-permitted`), plus REQUIRES_OWNER decisions. | Journal decision log |
| Policy violations | REQUIRES_OWNER decisions (protected classes), counted by class. | Journal decision log |
| Token consumption | Provider tokens per execution (ESO-R6-06). | `praxis telemetry ingest`; unavailable when not ingested |
| Elapsed execution time | From the clock start to the final verdict. | Kickoff record and final record |
| Requirements / obligations passed | As computed by the frozen plan's verifier. | Final verification |

## Owner decisions this intake needs

1. Approve or amend UAE-R7-01 to UAE-R7-12 for Rehearsal 7, through the owner's merge of the PR that records them.
2. ~~Generate the owner's P-256 key and keep its trust store outside every repository (praxis `EXECUTION-POLICY.md` §2).~~ Resolved 2026-10-29: owner-delegated agent authority (see the amendment above).
3. For UAE-R7-12: put the trust store in an Actions secret, and add a ruleset on the rehearsal repository's `main` requiring the CI and `independent-review` checks, with no human review, and blocking force pushes. This turns Praxis's gate from advisory into enforced (diagnosis D-2). It stays compatible with unattended merging.
