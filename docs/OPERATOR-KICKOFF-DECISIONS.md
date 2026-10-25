# Operator kickoff decisions

Recorded: 2026-10-24.
Source: explicit operator approval in the Indy-init project conversation, responding to the proposal to adopt the first two decisions now.

## Approved

- **DEC-WRITES-001: Writes are required for the Indy Init application.** Prioritize one complete, persistent vertical slice: create investigation, add evidence, create hypothesis, perform an authorized change, persist, reload, and verify. Do not reinterpret this as approval to drop other mandatory requirements or as evidence that a seven-hour implementation is feasible.
- **DEC-PERCEPTA-001: Percepta is required.** Prepare and verify a released Percepta artifact for the compatible kickoff stack. The operator did not approve a Percepta waiver.

## Still open

- The five decisions in DF-CAUSA-2026-0003 must be individually inspected and resolved. This approval does not ratify any agent-authored disposition.
- The frozen requirements baseline and exact compatible kickoff release set remain unapproved pending evidence and integration checks.
- No claims are made here that Percepta is released, write persistence is implemented, or the full-write rehearsal is accepted.

## Authority and provenance

This document records the operator's expressed decision; it is not a cryptographic signature or a substitute for the independently authenticated approval mechanism required by AR-003 and AR-019. A future acceptance gate must verify the decision against its authorized source and pinned baseline. Agents must not self-approve exceptions.

## Next work

Carry DEC-WRITES-001 and DEC-PERCEPTA-001 into the next requirements reconciliation and implementation planning, without modifying historical rehearsal evidence. Resolve the remaining operator decisions before freezing the stack and performing a timed write-path rehearsal.


## Additional operator authorization (2026-10-24)

The operator subsequently stated: "Do what you recommend I approve." This authorizes proceeding with the recommended engineering work: independently computed acceptance, non-bypassable requirement preservation, compatible release-set repairs (including investigation of Dokimos 0.7.0, Percepta release, and Conditor SD-1), and preparation of a timed full-write rehearsal.

This is approval of the *work plan*, not a declaration that unknown evidence passed or that specific unresolved decision alternatives were chosen. The five individual DF-CAUSA-2026-0003 decisions must be retrieved and evaluated before recording their dispositions. Do not infer their answers or mark the baseline/release set frozen without verification.

Execution order: (1) retrieve decision evidence; (2) reconcile requirements and releases; (3) verify required CI and independent acceptance; (4) lock the verified stack; (5) run and assess a timed write-path rehearsal. Report BLOCKED for any missing prerequisite.
