# Readiness report: package v4.1.1

**Date:** 2026-11-01. **Branch:** `claude/v411-amendment` (indy-init#67). **Package:** [requirements-compiled/v4.1.1](../requirements-compiled/v4.1.1/README.md), status CANDIDATE until the owner records ODR-V411-01.

This report answers the seven questions of the Final Execution Optimization and Readiness Amendment. The detail is in [AMENDMENT-4.1.1.md](../requirements-compiled/v4.1.1/AMENDMENT-4.1.1.md).

## A. Is the functional scope unchanged?

**Yes.** v4.1.1 uses v4's verification plan byte for byte. X10 and `preservation-report.json` read PRESERVED: 520 requirements, 161 of them P0, and 2599 obligations, 613 of them P0. The following are v4.1's, unchanged:

- the state machines (`state-machine-contracts.json`);
- the contracts and layout tests (CI diffs them against v4.1's);
- the verification execution plan.

The amendment changes only these:

- execution: when T10 runs, what follows it, and one dropped preference on T06;
- ownership of two shared files;
- documentation;
- the token-economics report.

## B. Can the application be generated from an empty repository at T0?

**Yes, for the mechanical part, on this host.** The v4.1.1 sequence ran on three fresh empty repositories. Each run did the following:

1. `git init`;
2. `conditor init --preset indy-init`;
3. the generator, layered onto Conditor's repository;
4. a build of both `Causa.slnx` and `App.slnx`;
5. `conditor ci-policy check`, `conditor verify`, and a commit.

Every step passed in every run. The runs took 72 s and 76 s with a cold cache, and 32 s with a warm one.

The test found and fixed one real defect. Copying the generator's output over Conditor's repository broke Conditor's `App.slnx`. The generator now layers onto the repository instead, and central package management applies only to the Causa projects.

**Not verified:** `conditor repo create`, the push, and the first GitHub Actions run. These need a real GitHub repository.

## C. Are the F# contracts ready to materialize without further design decisions?

**Yes.** The contracts typecheck, and the negative controls fail to compile, as they must (CI). Each state machine's states and events are exactly one interface's unions (X05).

`contract-classification.json` classifies every contract file:

- **Seven files** are types and signatures only.
- **Two files carry reviewed boundary code**, seven members in all: the credential's `ToString`, `create` and `authorization`, and the confirmation's three accessors and `ofReadBack`. Each enforces a requirement at the type boundary (PF-015, INV-21, PERSIST-023). The behavior around them is timed work: the save workflow, the HTTP calls and the credential's use.

X17 rejects any other implementation member. The bootstrap writes each contract verbatim into its producer's file, deterministically, and never writes the compile control.

## D. Is file ownership unambiguous?

**Yes.** Every planned file has exactly one owner (X01). `web/index.html` and `web/causa.css` are now integration's (X02, X18). UI roles change them only through an integration-owned change request. AGENT-RULES' roles table and the manifest agree, and X18 checks this. The negative tests reject three cases:

- giving either file back to ui or ui-2;
- a UI contributor on either file;
- prose that gives the files to ui.

## E. Is Praxis token accounting operational for the timed run?

**The tooling is ready. It depends on the run executing the Praxis ingestion steps.** `requirements-compiler token-economics` produces `evidence/telemetry/token-economics.{json,md}` from Praxis 3.24.0's execution records. It has been verified on a recorded fixture.

Rehearsals 5 and 6 recorded no tokens, because nothing ran Praxis's transcript adapter. The runbook in [TOKEN-ECONOMICS.md](TOKEN-ECONOMICS.md) makes these steps explicit:

- `praxis telemetry ingest` for each session, before its `work complete`;
- the report, at completion.

A run that skips ingestion produces a report that says the tokens are unavailable, with Praxis's reason. Under the amendment, that is a process defect, not an application failure.

The historical comparison is limited:

- R5 and R6 have no token data;
- R7's numbers come from its own telemetry script, not from Praxis.

No cross-rehearsal token comparison is therefore possible yet.

## F. Does the updated schedule fit within 420 minutes?

**Topology C fits, at 414 minutes, with 6 minutes of margin.** Topologies A (484) and B (774) do not fit. This is unchanged in kind from v4.1, where A was 455 and B was 745.

The reserves are kept at 20 and 45 minutes, and no estimate was shortened. The bootstrap adds its full 29 minutes to the front of every chain.

The margin is thin. It is gone if the bootstrap overruns by more than 6 minutes, or if every integration task overruns by about 5%. The sensitivity scenarios are in `schedule.json`.

One lever is not applied, because it changes v4's T10 completion criterion. T12, T14 and T18 could start at the skeleton commit, which takes about 2 minutes. They would no longer wait for the first green CI. That would recover up to 25 minutes, and it needs an owner decision.

## G. What remains unverified?

- **First GitHub Actions run.** It, `conditor repo create`, and integration's shared-file authoring are 27 of T10's 29 minutes, and all are estimated.
- **Other estimates.** No estimate other than T10's mechanical part has been measured in an event build.
- **Token telemetry.** It has not been collected on a real timed run. The Praxis ingestion commands come from the Praxis 3.24.0 documentation and source; they were not run here.
- **Organizers' reading.** How the organizers read the starting state remains open; see the residual risk in [STARTING-STATE.md](../requirements-compiled/v4.1.1/STARTING-STATE.md).
- **Owner approval.** ODR-V411-01, the owner's approval of v4.1.1, is pending. Until it is recorded, the event build follows v4.1 as approved.

## Independent review

See [independent-review.md](../requirements-compiled/v4.1.1/independent-review.md).
