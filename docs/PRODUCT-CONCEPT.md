# Product Concept: Root-Cause Investigator Platform

## Problem

Root-cause analysis often collapses too quickly into a plausible story. An investigator gathers a few facts, an AI suggests a likely explanation, and the explanation is treated as the answer.

The application should instead preserve competing hypotheses, explicit evidence, contradictions, negative knowledge, predictions, experiments, and unresolved uncertainty until the evidence supports a conclusion.

## User experience

The entry point should be deliberately simple:

> Something went wrong. What did you observe?

The user describes the initial failure or unexpected result in natural language.

The investigator then builds and maintains an explicit investigation state rather than simply continuing a chat.

## Core investigation loop

1. Observation
2. Candidate hypotheses
3. Predictions for each hypothesis
4. Known evidence
5. Missing or discriminating evidence
6. Proposed experiments or checks
7. Findings
8. Hypotheses strengthened, weakened, or falsified
9. Contradictions and unresolved unknowns
10. Root-cause conclusion when justified
11. Corrective actions
12. Verification that the corrective action worked
13. Final report

## Hypothesis lifecycle

A hypothesis may move through states such as:

- Proposed
- Plausible
- Supported
- Weakened
- Falsified
- Confirmed

A transition must have a reason and supporting evidence.

The system must retain the history of earlier states. It must not rewrite the investigation as though the final explanation was obvious from the start.

## Evidence relationships

Evidence can:

- support a hypothesis
- contradict a hypothesis
- discriminate between multiple hypotheses
- be relevant but insufficient
- be superseded
- remain unverified

The investigator should prefer evidence that efficiently distinguishes between competing explanations.

## Negative knowledge

The system must explicitly record what has been ruled out.

Examples:

- Known false: the input dataset changed.
- Unsupported: parser regression.
- Eliminated by experiment: network timing was causal.
- Unknown: denominator semantics include unresolved effects.

Negative knowledge should prevent repeated investigation of already falsified explanations unless new evidence justifies reopening them.

## Unknowns

Unknown must be a first-class state, not a synonym for false or missing.

An unknown may create an obligation:

- acquire evidence
- perform an experiment
- ask a question
- verify an external effect
- accept the uncertainty explicitly before proceeding

## Corrective action

The investigator should distinguish:

- root cause
- contributing factors
- corrective action
- mitigation
- verification

A corrective action is not considered successful merely because it was performed. The system must define and then test verification criteria.

## Product direction

The hackathon application is the first constrained release of a reusable investigation platform, not a disposable demo.

The product must preserve a domain-neutral investigation kernel. Competition-specific provenance and timing rules belong to a release profile around the kernel rather than inside it.

Long-term use should support:

- multiple investigations and workspaces
- pluggable domain specializations
- multiple persistence providers
- multiple AI providers
- collaboration and review
- cross-investigation search and learning
- import/export and schema migration
- service-backed, self-hosted, and static-capable deployment models where appropriate

## Generic first, EDF second

The core must model investigation primitives rather than EDF-specific concepts.

After the hackathon, EDF becomes the first domain pack. EDF-specific concepts such as experiment regressions, unknown effects, assumptions, metrics, and evidence dependencies should extend the generic model through explicit versioned contracts instead of creating an EDF fork.
