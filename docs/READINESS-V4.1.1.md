# Readiness report: package v4.1.1

**Date:** 2026-11-01. **Branch:** `claude/v411-amendment` (indy-init#67). **Package:** [requirements-compiled/v4.1.1](../requirements-compiled/v4.1.1/README.md), status CANDIDATE until the owner records ODR-V411-02 and ODR-V411-01.

This report answers the seven questions of the Final Execution Optimization and Readiness Amendment. The detail is in [AMENDMENT-4.1.1.md](../requirements-compiled/v4.1.1/AMENDMENT-4.1.1.md).

## A. Is the functional scope unchanged?

**Yes.** v4.1.1 uses v4's verification plan byte for byte. X10 and `preservation-report.json` read PRESERVED: 520 requirements, 161 of them P0, and 2599 obligations, 613 of them P0. The following are v4.1's, unchanged, and X17 and CI compare them byte for byte:

- the state machines;
- the contracts;
- the layout tests;
- the verification execution plan.

No requirement is weakened or claimed met. The organizer-related obligations of the frozen baseline stay as they are, and the owner decides how they are reported (ODR-V411-02). They are ADRP-016-O2, STORAGE-008-O1, R0.1-O1, the kickoff checks, PRE-ORG-01 and the DOMAIN-TEST-MATRIX condition.

## B. Can the application be generated from an empty repository at T0?

**Yes, for the mechanical part, on this host.** Three fresh empty repositories ran the v4.1.1 sequence with the Registry-pinned release binaries, each checked against its SHA-256. Each run did the following:

1. `git init`;
2. `conditor init`;
3. the generator, layered onto Conditor's repository;
4. a build of both solutions;
5. `ci-policy check`, `verify`, and a commit.

Every step passed in every run. The runs took 77 s and 76 s with a cold cache, and 32 s with a warm one. CI and the offline suite repeat the layering on a recorded Conditor 0.10.18 scaffold, and `tools/measure-t0-bootstrap.fsx` reproduces the timing.

**Not verified:** `conditor repo create`, the push, integration's remaining shared files and the first GitHub Actions run. They are 27 of T10's 29 minutes, and all are estimated.

## C. Are the F# contracts ready to materialize without further design decisions?

**Yes.** Every contract equals the reviewed, approved v4.1 contract byte for byte (X17). The contracts typecheck, and the negative controls fail to compile (CI). Each state machine is exactly one interface's unions (X05).

Two files carry reviewed boundary code, seven members in all, each pinned by SHA-256:

- the credential's `ToString`, `create` and `authorization` (PF-015, INV-21);
- the confirmation's three accessors and `ofReadBack` (PERSIST-023).

They fix the `Bearer` scheme and exact read-back equality, both of which are requirements. The byte equality with v4.1's reviewed contracts is the guarantee. X17's line scan is a second, heuristic guard. It reads strings, characters and comments as the F# lexer does, and the offline suite tests it against all 18 bypass forms both review rounds found.

## D. Is file ownership unambiguous?

**Yes, in the manifest and the rules.** Every planned file has exactly one owner (X01). `web/index.html` and `web/causa.css` are integration's (X02, X18). UI roles change them only through an integration-owned change request. T17 carries 5 minutes for applying them.

X18 rejects, among other cases, these three:

- another owner or contributor for either file;
- any AGENT-RULES line naming the files that is not one of the four reviewed lines pinned by SHA-256.

Nothing in the generated repository enforces ownership technically; there is no CODEOWNERS file.

## E. Is Praxis token accounting operational for the timed run?

**The tooling is ready. It depends on the run executing the Praxis ingestion steps.** `requirements-compiler token-economics` produces `evidence/telemetry/token-economics.{json,md}` from Praxis 3.24.0's execution records. It has been verified on a recorded fixture. Usage from a session that crosses the window boundary is kept apart, in an `unsplit` bucket.

Rehearsals 5 and 6 recorded no tokens, because nothing ran Praxis's transcript adapter. The runbook in [TOKEN-ECONOMICS.md](TOKEN-ECONOMICS.md) makes these steps explicit:

- `praxis telemetry ingest` for each session, before the window ends;
- the report, at completion.

A run that skips ingestion produces a report that says the tokens are unavailable, with Praxis's reason. Under the amendment, that is a process defect, not an application failure.

R7's numbers come from its own telemetry script, not from Praxis. No cross-rehearsal token comparison is possible yet.

## F. Does the updated schedule fit within 420 minutes?

**Topology C fits, at 419 minutes, with 1 minute of margin.** Topologies A (489) and B (779) do not fit, as in v4.1, where A was 455 and B was 745.

The reserves are kept at 20 and 45 minutes, and no estimate was shortened. Two changes put minutes on the binding chain:

- The bootstrap adds its full 29 minutes.
- Integration's change-request time adds 5 minutes to T17.

**This margin will not survive a real overrun.** Doubling the change-request time gives 424 minutes, a bootstrap 50% over gives 434, and integration 25% over gives 454.

Two levers would cut actual work without removing a requirement. They are not applied, and the first needs an owner decision:

- **Start T12, T14 and T18 at the skeleton commit**, not at first green CI. This recovers up to 25 minutes, but changes v4's T10 criterion.
- **Apply change requests in integration's idle time.**

## G. What remains unverified?

- **T10's unmeasured parts:** the first GitHub Actions run, `conditor repo create`, and integration's shared-file authoring. They are 27 of its 29 minutes.
- **Every other estimate,** including the 5 minutes of change-request time.
- **Token telemetry on a real timed run.** The Praxis ingestion commands come from the Praxis 3.24.0 documentation and source; they were not run here.
- **The organizers' reading of the starting state** (STARTING-STATE.md, residual risk), and how the organizer-approval obligations are reported (ODR-V411-02).
- **The owner's approval of v4.1.1** (ODR-V411-01). Until it is recorded, the event build follows v4.1 as approved.

## Independent review

See [independent-review.md](../requirements-compiled/v4.1.1/independent-review.md). Round 1 failed with five major findings, and round 2 failed with one major finding; none was blocking, and each finding has a disposition. Round 3 reviews the fixes.
