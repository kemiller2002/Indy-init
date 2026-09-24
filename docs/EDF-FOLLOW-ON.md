# EDF Follow-on

After the hackathon, adapt the generic investigation kernel into an EDF-specific investigator.

## Purpose

Use the Root-Cause Investigator to diagnose unexpected EDF experiment outcomes, regressions, metric shifts, model disagreements, requirements failures, and framework changes.

This should not fork the generic reasoning model unnecessarily. EDF should supply domain knowledge and domain-specific projections around the shared investigation kernel.

## EDF-specific concepts

Potential specialist concepts include:

- CDR
- MDR
- MaDR
- ContextCoverage
- EvidenceClosure
- UnknownEffect
- requirements
- acceptance criteria
- experiment runs
- agent/model outputs
- cost
- version differences
- state transitions
- evidence dependencies
- negative knowledge

## Example investigation

Observation:

> CDR decreased after a change intended to improve evidence handling.

Candidate hypotheses:

- ContextCoverage semantics changed.
- Evidence closure rejects previously accepted paths.
- Unknown-effect handling changed the denominator.
- Experiment inputs differ from the baseline.
- Measurement instrumentation is wrong.

The investigator should identify evidence that discriminates among these explanations rather than simply listing them.

For example:

> Compare evidence closure before and after the change only for cases whose ContextCoverage is unchanged.

If ContextCoverage remains unchanged but previously valid evidence paths disappear, one hypothesis weakens while another gains support.

## Research questions

The EDF implementation can test additional hypotheses about the methodology itself:

1. Does explicit negative knowledge reduce repeated diagnostic work?
2. Does recording predictions before experiments reduce hindsight bias?
3. Does explicit unknown-effect handling prevent premature conclusions?
4. Does preserving falsified hypotheses improve later audits?
5. Can the system identify weaknesses in EDF experiments that agents miss during ordinary review?
6. Does the investigator improve consistency across different AI models?

## Recursive value

This creates a useful recursive test:

EDF helped motivate the investigation model.

The resulting investigator then becomes an instrument for challenging EDF.

The goal is not to prove EDF correct. The goal is to create stronger mechanisms for finding where EDF is wrong, incomplete, or insufficiently evidenced.


## Semantic integration

EDF must consume the canonical investigation semantics rather than define a parallel investigation model.

The EDF domain pack should map:

- experiment observations and metric changes to Observation/Finding/Evidence
- regression explanations to Hypothesis
- model premises to Assumption
- unknown effects to Unknown
- disproven explanations to NegativeKnowledge
- evidence dependency and cycle information to Evidence dependency relationships
- proposed experiments to InvestigationAction
- expected outcomes to Prediction
- accepted causal explanations to Cause/Conclusion
- follow-up checks to InvestigationAction or Verification according to intent

EDF policy may strengthen confirmation requirements, for example by requiring reproduced experimental effects, explicit evidence closure, scoped context coverage, or reconciled unknown effects.

It must not weaken the core guarantees around provenance, evidence/inference separation, unresolved contradictions, proposal authority, negative knowledge, or historical integrity.

The intended result is one investigation engine with EDF as a versioned specialization, not an EDF-specific fork.
