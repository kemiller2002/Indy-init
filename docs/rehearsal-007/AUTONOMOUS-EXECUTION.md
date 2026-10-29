# Autonomous execution for Rehearsal 7 (R7-REM-005, Experiment A)

The requirements are UAE-001 ([`input-documents/2026-10-28-UAE-001-unattended-autonomous-execution.md`](../../input-documents/2026-10-28-UAE-001-unattended-autonomous-execution.md), UAE-R7-01..12, approved with the owner-delegated-key amendment). This document does not restate them. It records what exists, what was demonstrated, and what still belongs to the owner. No competing implementation was built.

## What exists

| Capability | Owner | Evidence |
|---|---|---|
| Signed run authorization (`praxis.run-authorization/1`), a frozen policy, a fail-closed merge evaluator, SHA-bound receipts, revocation | Praxis WI-0104 | praxis `docs/autonomous-execution/` (ARCHITECTURE, EXECUTION-POLICY, INTEGRATION-TEST-RESULTS: negative cases against real GitHub) |
| `conditor unattended-readiness`, a real branch → commit → PR → CI → independent review → merge → verified receipt | Conditor WI-0062 | praxis `docs/autonomous-execution/evidence/2026-10-29/readiness-receipt.json` |
| Dependency continuation (`praxis autonomy observations` → `praxis plan analyze --observations`) | Praxis | UAE-R7-07 |

## Experiment A: PASS (advisory gate), recorded by an earlier session

**The run.** `conditor unattended-readiness` reported **READY** against `kemiller2002/indy-init-rehearsal-007` `main` on 2026-10-29, run `RUN-R7-READINESS-20261029-01`. Every check passed:
- UR-01: Praxis 3.23.0 available.
- UR-02: push permission.
- UR-03: no required human review.
- UR-04: policy valid, `sha256:e0ad0877…`.
- UR-05: `build-and-test` required.
- UR-06: authorization valid until 2026-10-30T01:01:20Z.
- UR-07: revocation refused a revoked run.
- UR-08: probe PR #20 at `ac2f954`.
- UR-09: CI, independent review, Praxis authorization and merge passed; the PR merged as `9c3aaf6`.
- UR-10: merge verified.

That was a **real merge without routine owner interaction**.

**Not re-observed here.** This session could not read `indy-init-rehearsal-007`: its request to attach the repository was refused. So the result is cited from the receipt Praxis keeps, not observed again. The receipt must be **re-run at kickoff with the kickoff authorization** (UAE-R7-06).

## Why the gate is still advisory (open, owner-accepted)

These come from praxis SECURITY-REVIEW F1 and the diagnosis D-2/D-7:
- the agent holds the signing key (the owner decision of 2026-10-29);
- the executor chooses the trust store;
- the agent's credential is admin.

Together, these mean Praxis binds a cooperating agent only. **UAE-R7-12** closes this:
- the Praxis evaluate job runs in GitHub Actions with the trust store in an Actions secret;
- a ruleset on `main` requires that job, CI and `independent-review`, with no human review and no force pushes.

The ruleset and the secret are **owner actions**. Until they exist, the Rehearsal 7 report must say the gate was advisory and that the agent held the key.

## How the remediation work in this initiative was merged

The remediation pull requests were merged by this session on the owner's instruction in chat ("Merge please", 2026-10-29), through the API, with merge commits, after CI was green on each head and each head SHA was pinned:
- arca#47 as 916d0ca
- conditor#122 as 9241290
- praxis#300 as 4e9993e
- forma#173 as 6c344f6
- the Indy-init pull request (#42)

These repositories have no Praxis run authorization for this initiative and no ruleset, so the policy-controlled merge path did not apply. This was an owner-approved merge, not the unattended, policy-controlled path, and it is not evidence for Experiment A.

For comparison, Indy-init #41 (ADR-R7-001, from another session) was merged by an agent through the API after the owner approved it in chat. GitHub attributes that merge to the owner's account. That is an owner-approved merge, not the unattended, policy-controlled path. It is not evidence for Experiment A.

## Measurements Rehearsal 7 must record

Record them as defined in UAE-001 ("Measurement definitions"): owner interventions, approval requests, pull requests created and merged, cycle time, time waiting for approval, CI retries, merge retries, prevented operations, policy violations, tokens, elapsed time, and requirements passed.
