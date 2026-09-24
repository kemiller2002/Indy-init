# Screen Contracts

Status: application-specific semantic UI contract baseline.

These contracts define what each major screen must mean. Exact layout, visual style, and component composition remain Percepta/Forma concerns.

Each screen must have:

- ScreenId
- purpose
- primary question
- required semantic regions
- required actions
- prohibited implications
- responsive obligations
- accessibility obligations
- completion evidence

## S01 Investigation Start

Purpose:
Create a new investigation from minimal unstructured input.

Primary question:
What happened?

Required:

- original problem statement field
- optional title
- domain/profile indicator if selected
- Start Investigation action
- persistence destination indicator before remote write

Prohibited:

- asking users to understand ontology first
- silently treating the first statement as a causal conclusion

Completion evidence:

- iPad/phone keyboard use
- original text preserved
- AI unavailable fallback

## S02 Investigation Workspace

Purpose:
Operate the investigation.

Primary question:
What do we know, what explains it, and what should we do next?

Required:

- lifecycle/conclusion posture
- hypotheses
- strongest evidence/contradiction
- blocking unknowns/obligations
- recommended action
- Ask/Add input
- persistence state
- history/time-travel access

Prohibited:

- chat as only authoritative view
- hiding material blockers

## S03 Hypothesis Battlefield

Purpose:
Compare competing explanations.

Primary question:
Which explanation currently fits, and how could we prove it wrong?

Required per hypothesis:

- statement
- lifecycle
- derived posture
- support
- contradiction
- assumptions
- unknowns
- prediction
- falsifier
- negative knowledge

Actions:

- Challenge
- Add evidence
- Record prediction
- Propose test
- Open detail

## S04 Hypothesis Detail

Purpose:
Inspect one explanation deeply.

Required:

- history
- what it explains
- mechanism
- scope
- evidence relationships
- assumptions
- predictions
- contradictions
- negative knowledge
- dependencies
- legal actions

## S05 Evidence Capture

Purpose:
Capture evidence quickly without losing provenance.

Inputs may include:

- text
- measurement
- URL
- screenshot/photo
- file metadata
- result from action

Required:

- source/provenance
- scope
- category
- original content
- verification status
- AI extraction as proposal only

## S06 Evidence Detail

Required:

- original source
- provenance
- integrity
- availability
- relationships
- dependency lineage
- verification
- supersession
- interpretations

Action:
Show source lineage.

## S07 Evidence Dependency

Purpose:
Reveal correlated evidence.

Required:

- lineage graph
- accessible hierarchy/table
- shared-source clusters
- DerivedFrom/DependsOn
- supersession

## S08 Unknown / Assumption / Contradiction

Purpose:
Resolve epistemic blockers.

Required:

- entity type
- materiality
- why it matters
- what it blocks
- dependent entities
- resolution path
- policy source
- history

## S09 Action / Experiment Designer

Purpose:
Define discriminating investigation work.

Required:

- target uncertainty
- hypotheses distinguished
- prospective predictions
- procedure
- expected outcomes
- cost/time/risk
- success/falsification criteria
- pre-register action

Prohibited:
running before predictions are frozen when policy requires pre-registration.

## S10 Finding / Result Capture

Required:

- source action
- original result
- anomalies
- scope/environment
- proposal to admit as Evidence
- proposed relationship impact

Result is not automatically Evidence/Conclusion.

## S11 Cause Review

Purpose:
Make causal confirmation difficult and inspectable.

Required:

- causal assertion/set
- outcome
- member roles
- mechanism
- evidence
- alternatives
- contradictions
- unknowns
- assumptions
- policy gate
- reviews
- Confirm Cause capability/blockers

## S12 Corrective Action

Required:

- action kind
- target causal assertion/member
- owner
- status
- implementation evidence
- verification criteria

Must visually distinguish mitigation/workaround/correction/prevention.

## S13 Verification

Required:

- pre-defined criteria
- expected outcome
- method
- actual result
- evidence
- pass/fail/inconclusive
- verifier

## S14 History / Time Travel

Required:

- aggregate-version timeline
- event summaries
- policy/domain version changes
- scrubber
- state-at-version
- semantic diff
- replay

Historical mode visibly read-only.

## S15 Semantic Diff

Required:

- source versions
- entity additions/removals-by-supersession
- lifecycle changes
- blocker/capability changes
- derived posture changes
- policy/domain changes

Raw JSON optional developer view only.

## S16 Counterfactual Sandbox

Required persistent banner:

WHAT-IF / SANDBOX

Required:

- source version
- hypothetical changes
- resulting semantic diff
- source unchanged indicator
- discard
- fork/promote explicitly

## S17 Multi-Agent Panel

Required:

- frozen source version
- blind-phase status
- roles
- model/provider
- proposals
- agreements/disagreements
- dependency between agent outputs
- evidence cited
- cost/latency
- accept/reject via normal proposals

## S18 Research Tournament

Required:

- experiment definition
- frozen source
- compared model/prompt/policy/domain versions
- metrics
- raw proposal access
- no arbitrary overall winner

## S19 Replay / Spectator

Purpose:
Presentation/exploration.

Required:

- problem
- current version
- active hypotheses
- key evidence
- blocker
- next action
- event step
- pause/step
- reduced-motion mode

No writes.

## S20 Factory View

Purpose:
Show how the software itself was built.

Required:

- current/complete ROS work
- requirement coverage
- tests
- Praxis metrics
- Aegis faults
- commits
- deployment
- proof bundle

All values real.

## S21 Requirements Proof

Required:

- requirement
- scenario
- work item
- code
- test
- commit
- runtime evidence

Missing edges shown.

## S22 Report Preview

Required:

- source investigation/version
- current conclusion posture
- evidence
- alternatives
- negative knowledge
- uncertainty
- corrective/verification
- classification/redaction

## S23 Persistence / Conflict Recovery

Required:

- local version
- remote version
- pending commands
- semantic conflict explanation
- revalidation result
- safe resolution choices

No raw JSON required.

## S24 Settings / Semantic Manifest

Required inspectable:

- core semantic version
- domain pack/version
- policy version/pin
- experimental profile
- persistence provider
- AI provider configuration summary
- classification
- unsupported semantics

Dangerous changes require explicit confirmation/revalidation.

## S25 Demo Health

Required checks:

- build/deployment
- GitHub
- local store
- AI provider(s)
- report
- spectator/replay
- secrets/smoke checks
- demo sample reset

Cannot hard-code green state.

## Responsive contract

Every screen must define at least:

- iPad landscape
- iPad portrait
- narrow phone
- desktop
- high zoom

Semantic priority, not pixel layout, is normative.

## Percepta integration

Percepta should eventually represent these screen contracts as machine-checkable application UI contracts and validate independent agent implementations against them.
