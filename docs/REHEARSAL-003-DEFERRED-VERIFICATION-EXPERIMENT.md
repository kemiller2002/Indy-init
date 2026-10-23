# Rehearsal 3: Frequent Commits with Time-Checkpoint Verification

Status: APPROVED EXPERIMENT DESIGN; NOT EXECUTED. Applies to Rehearsal 3 only. Rehearsal 2 execution policy remains unchanged.

## Hypothesis
A time-boxed implementation phase with frequent durable commits and deferred noncritical tests/fixes will reduce agent interruptions, CI runs, and token expenditure and improve time to verified completion versus continuous verification. This is a hypothesis, not an established result. Compare against R001/R002 with scope and stack differences explicitly controlled.

## Required policy (R3-DV-001..012)
- R3-DV-001 [P0]: Discover, resolve, compatibility-test, and freeze the newest approved stable Echelon stack before implementation, following REHEARSAL-DEPENDENCY-LOCK.md. Do not defer this preflight.
- R3-DV-002 [P0]: Divide a seven-hour example run into preflight/setup 10% (42 min), implementation 60% (252 min), integration/correction 20% (84 min), and final acceptance 10% (42 min). For another duration, scale proportionally. Use monotonic elapsed time and publish planned and actual timestamps.
- R3-DV-003 [P0]: During implementation, commit every 10–20 minutes or after each coherent slice, whichever is sooner. Explicitly label commits/checkpoints as UNVERIFIED when full verification has not run. Never call such a checkpoint passing.
- R3-DV-004 [P0]: Defer routine full-suite tests, noncritical failures, and associated fixes until the implementation checkpoint. Cheap compile/syntax/contract checks MAY run when cost-effective; log all failures without concealing them.
- R3-DV-005 [P0]: Do NOT defer credential leaks, critical security failures, destructive or irreversible data operations, authorization boundary violations, corruption/data-loss risks, or producer/consumer contract mismatches that block parallel agents. Stop the affected slice, contain the issue, and fix/coordinate immediately.
- R3-DV-006 [P0]: Maintain a timestamped, machine-readable defect and failure ledger (first observed, severity, reproduction, affected component, owner, deferred/immediate disposition, and eventual resolution). Never ignore or suppress failures.
- R3-DV-007 [P0]: At the implementation time checkpoint, freeze feature development. Run integration, compile, contract, security, UI, persistence, and browser tests against the recorded SHA; then prioritize correction by severity and dependency impact.
- R3-DV-008 [P0]: Final acceptance must use an exact committed SHA and independently verified deployed SHA, including external GitHub data access and no bundled demo data, Forma semantic color requirements, accessibility, and all applicable release gates. A nonpassing gate means FAIL/BLOCKED, not PASS.
- R3-DV-009 [P0]: If correction time expires, report unfinished defects and incomplete acceptance honestly. Do not extend the timer invisibly or claim success because feature implementation was completed.
- R3-DV-010 [P0]: Collect timestamps, tokens (if measurable), agent runs/handoffs, commit counts, CI/build/test executions, first build/first screen/first verified external read/write, defects introduced/discovered/fixed, regression recurrence, rework duration, deployment attempts, and time to fully verified completion.
- R3-DV-011 [P0]: Compare with R001 and R002 using comparable denominators, scope-adjusted observations, and disclosed changes in data architecture, UI requirements, and dependency versions. Distinguish reduced testing cost from total completion cost and correctness.
- R3-DV-012 [P0]: Feed implementation requirements through each target repo's documented Praxis input directory and format, never GitHub Issues. Preserve evidence and checkpoints in the rehearsal repository.

## Acceptance experiment
Primary metric: elapsed time to exact-SHA fully verified deployed app, with all P0 gates PASS.
Secondary: total tokens, rework, number of full test runs, total failures and severity, agent handoffs, commits, correctness and recurrence.
Success is NOT simply more code or fewer tests. If deferred defects create longer integration or lower correctness, record that outcome and consider earlier intermediate gates.

## Precedence
This policy changes R003 implementation scheduling only. It does not waive external-data, security, schema, stack compatibility, UI, or final release requirements. It must not be retroactively applied to R002.
