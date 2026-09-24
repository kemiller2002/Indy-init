# Demo Plan

## Goal

Make the product understandable first. Reveal the engineering system second.

Do not begin by teaching judges Ordo, ROS, state machines, or the framework taxonomy.

## Opening

Start on an iPad.

The application asks:

> Something went wrong. What did you observe?

Use a scenario with multiple plausible causes.

Example:

> A customer says an order never arrived. Our system says delivered. The carrier also says delivered, but the shipping address may have changed after the order was placed.

## Product demonstration

Show the investigator create several competing hypotheses.

Then show:

1. known evidence
2. unknown facts
3. a contradiction
4. a proposed discriminating check
5. an experiment/check result
6. one hypothesis weakened or falsified
7. negative knowledge retained
8. an action that is currently unavailable
9. the reason that action is unavailable
10. new evidence satisfying the missing obligation
11. the legal actions changing
12. a supported root-cause conclusion
13. corrective action plus verification criteria
14. Folio-generated report

## GitHub persistence reveal

Open GitHub and show:

- human-readable investigation artifacts
- commit history
- state changes over time

Explain that GitHub is not only storing the current result. It also gives provenance for how the investigation changed.

## Factory reveal

Then move from the product to the build evidence.

Show the chain:

```text
Requirement
  -> acceptance criterion
  -> work item
  -> implementation
  -> tests
  -> metrics
  -> faults
  -> evidence
  -> build
```

Show concrete output from:

- ROS
- Ordo
- Aegis
- Praxis
- Limen
- Forma
- Folio

## Central statement

The strongest claim is not that AI wrote a lot of code quickly.

It is:

> The application is governed by explicit evidence and state, and the process that built the application is governed by explicit evidence and state too.

## Optional judge-driven moment

If time and reliability permit, allow a judge to provide a small failure scenario rather than using only the prepared case.

The application should demonstrate that its investigation model is generic rather than hard-coded for one demo.

## Do not overclaim

If a requirement is incomplete, show it as incomplete.

If a remote write is pending, show it as pending.

If an unknown remains, show the unknown.

That behavior is part of the product thesis.


## Ambitious competition sequence

The detailed sequence is defined in [COMPETITION-DEMO-CHOREOGRAPHY.md](COMPETITION-DEMO-CHOREOGRAPHY.md).

Preferred live progression:

1. Natural-language observation.
2. Competing hypotheses appear as structured proposals.
3. Hypothesis Battlefield shows different predictions/falsifiers.
4. Blind multi-agent panel exposes disagreement/hidden assumption/evidence dependency.
5. Pre-register a discriminating action.
6. Ingest real finding/evidence.
7. Current leader is weakened/falsified.
8. "Why did this change?" explains the exact event/evidence/policy delta.
9. Time travel shows the prior state.
10. Counterfactual sandbox changes one assumption/evidence item without changing reality.
11. Confirm a causal set only when the gate clears.
12. Implement correction.
13. Attempt resolution and show verification blocker.
14. Record verification and resolve.
15. Generate Folio report.
16. Switch to Factory View and prove how the app itself was built.

If live-model latency threatens flow, use a previously captured frozen research run for the multi-agent comparison and label it accurately rather than pretending it ran live.

## Published competition alignment

The current Indy Init challenge explicitly emphasizes:

- production-grade app
- software factory
- machinery as well as product
- defending technical decisions
- blank implementation repo at kickoff
- reusable harness/standards/tests/security/pipeline

See [COMPETITION-ALIGNMENT.md](COMPETITION-ALIGNMENT.md) and [FLOOR-JUDGING-DEFENSE.md](FLOOR-JUDGING-DEFENSE.md).

## Floor-judging posture

Because judging is live at the table, the application should be left in an explorable state.

Judges should be able to click into:

- proposal provenance
- blocker explanation
- evidence lineage
- history/time travel
- proof graph
- tests/metrics
- known limitations

Do not make the architecture defense depend on a slide deck.
