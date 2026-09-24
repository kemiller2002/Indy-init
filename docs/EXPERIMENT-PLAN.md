# Experiment Plan

## Primary question

Can the Echelon Foundry software factory produce a defensible, working Root-Cause Investigator from zero application source code within the Indy Init hackathon build window?

## Secondary questions

1. Can the process maintain requirements traceability under severe time pressure?
2. Can explicit state and legal transitions prevent false completion?
3. Can Aegis make failures and invalid actions understandable rather than merely detectable?
4. Can Praxis detect architectural/code-quality degradation as agents move faster?
5. Can an iPad-first application remain usable with GitHub as remote persistence?
6. Can the investigator itself preserve hypotheses, falsification, negative knowledge, and unknowns without collapsing into a chatbot narrative?
7. Can Folio produce a useful investigation record from the structured state?
8. Can the same generic kernel be specialized for EDF after the event?

## Independence

The application implementation should not be built before kickoff.

Frameworks may be tested before the event using other applications and EDF work.

## Evidence to capture during the build

- start time and initial repository state
- requirements baseline
- framework versions
- agent/model used for each work unit
- work-item transitions
- files changed
- build results
- test results
- code metrics
- threshold violations
- Aegis faults
- repeated file/hunk modifications when Praxis supports them
- requirements coverage
- unresolved obligations
- deployment result
- end time
- final repository state

## Success dimensions

The experiment should not collapse to a single pass/fail result.

Evaluate at least:

### Functional
Does the end-to-end application work?

### Requirements
Which requirements and acceptance criteria have evidence?

### Integrity
Did the system prevent or surface illegal transitions and unsupported completion?

### Quality
What do tests, metrics, and architecture checks say?

### Accessibility
Can the application be operated effectively on iPad and with accessibility requirements enabled?

### Resilience
What happens when network persistence fails or conflicts?

### Explainability
Can a judge understand why the investigator reached its conclusion?

### Reusability
How much of the factory is application-independent?

### Time
Where was the build window spent?

### Cost
What agent/API costs were incurred?

## Important non-success result

If the factory cannot complete the application in the build window, that is still valuable experimental evidence.

Do not hide incomplete requirements, failed tests, unresolved unknowns, or architectural compromises to create the appearance of success.

The final record should distinguish:

- complete and evidenced
- implemented but not sufficiently evidenced
- incomplete
- intentionally deferred
- unknown


## Product capability experiments

The application itself should become an experimental platform as soon as the core is stable enough.

Use [RESEARCH-HARNESS.md](RESEARCH-HARNESS.md) for repeatable comparisons.

Candidate experiments:

1. Single strong agent vs Generator + Skeptic + Evidence Auditor.
2. Challenge mode on/off for premature-convergence rate.
3. Proof-carrying proposal UI vs narrative AI response.
4. Hypothesis Battlefield vs chat-only comparison for user understanding.
5. Planner discrimination-first heuristic vs generic AI "what next?".
6. Negative-knowledge retrieval on/off for repeated disproven work.
7. Evidence-dependency visualization on/off for false corroboration detection.
8. Time-travel/semantic-diff availability on/off for explanation/trust.
9. Different prompt versions on identical frozen cases.
10. Different model/providers on identical role/context.
11. Generic policy vs EDF current vs EDF experimental policy on identical snapshots.
12. ContextCoverage/EvidenceClosure/UnknownEffect variants as EDF research matures.

### Experimental rigor

Where practical:

- freeze source investigation version;
- preserve treatment configuration;
- blind evaluators to model/provider;
- randomize output order;
- record cost/latency;
- define outcome measures before looking at results;
- distinguish synthetic known-ground-truth cases from real uncertain cases;
- preserve failed/null results.

### Competition value

Even one small controlled comparison can strengthen the competition entry by showing that the architecture is being used to measure whether its own advanced AI techniques actually help.
