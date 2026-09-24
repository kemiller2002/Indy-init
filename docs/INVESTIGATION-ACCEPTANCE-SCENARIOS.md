# Investigation Acceptance Scenarios

Status: normative semantic acceptance scenarios.

These scenarios are intended to become executable domain and UI tests. They verify investigation meaning, not visual styling.

## A1. Preserve observation and interpretation separately

Given a user enters:
"Orders started failing after the 2 PM deployment."

When the agent structures the input

Then the original statement is preserved
And "orders started failing" may be represented as an Observation
And "the 2 PM deployment caused the failures" must not be represented as established Evidence
And a deployment-causality hypothesis may be proposed separately.

## A2. Multiple hypotheses remain active

Given an observation can plausibly be explained by:
- deployment regression
- upstream outage
- exhausted connection pool

When the investigation begins

Then all materially plausible hypotheses can coexist
And selecting a leading hypothesis does not remove the others.

## A3. Failure to support is not contradiction

Given H1 predicts an elevated error signature
And a log sample does not contain that signature
But the sample is incomplete

Then the evidence may FailToSupport H1
But must not automatically Contradict or Falsify H1.

## A4. Necessary prediction can falsify within scope

Given H1 states that every failing request must contain header X
And a verified failing request does not contain header X
And the record is within H1's declared scope

Then H1 may transition to Falsified
And the falsifying evidence and scope are recorded.

## A5. Reopening requires new basis

Given H1 is Falsified

When a user attempts to reopen H1 without new evidence, corrected evidence, corrected scope, or an invalidated premise

Then ReopenHypothesis is unavailable
And the blocking reason is visible.

When corrected evidence invalidates the earlier falsification

Then ReopenHypothesis becomes available
And reopening records that basis.

## A6. Two records from one source are not two confirmations

Given chart C and table T both derive from query Q
And both support H1

Then C and T may both be Evidence
But the system records their common dependency
And must not represent them as independent corroboration.

## A7. Contradictory evidence remains visible

Given E1 supports H1
And E2 materially contradicts H1
And both are verified

Then H1 may not be projected as unambiguously supported
And the contradiction is first-class
And any blocked conclusion capability explains the contradiction.

## A8. Unknown blocks transition

Given U1 is a Blocking Unknown required by the cause-confirmation policy

When the user attempts ConfirmCause

Then the transition is illegal
And U1 is identified as a blocker
And the UI offers a path to resolve or explicitly accept U1 when policy permits.

## A9. Unknown is not false

Given no evidence exists for Claim C

Then C may be Unknown or Unsupported
But it must not become Falsified solely because evidence is absent.

## A10. Assumption dependency is visible

Given H1 relies on assumption A1
And A1 is ProvisionallyAccepted

Then H1 exposes that dependency
And confirmation policy may require A1 verification.

When A1 is Rejected

Then hypotheses and conclusions materially depending on A1 require reevaluation.

## A11. Retrospective prediction is not prospective

Given an experiment result is already known

When a prediction matching that result is added afterward

Then it is marked retrospective
And cannot be presented as a prospective prediction.

## A12. Prefer discriminating action

Given H1 and H2 both predict X for Test A
And H1 predicts Y while H2 predicts Z for Test B

Then the investigator identifies Test B as more discriminating when cost/risk do not override that preference
And it can explain why.

## A13. Temporal sequence is not sufficient causation

Given deployment D occurred before failure F

Then the system may create a causality hypothesis
But must not confirm D as cause solely because D preceded F.

## A14. Counterfactual strengthens causal reasoning

Given H1 claims deployment D causes F
And a rollback returns the system to the prior version while relevant conditions remain materially comparable
And F disappears

Then the finding can provide intervention/counterfactual support for H1
But alternative explanations and confounders remain assessable.

## A15. Contributing factor is not root cause

Given condition C increases severity but cannot explain the failure without cause R

Then C may be represented as ContributingFactor
And R may be RootCause if confirmation policy is satisfied
And the two roles remain distinct.

## A16. Causal set

Given failure F requires conditions A and B together
And neither A nor B alone produces F

Then the model can represent {A,B} as a causal set
And does not force one member to be the single deepest root cause.

## A17. Negative knowledge is scoped

Given network latency is ruled out for version 2 under environment E

Then the system records scoped NegativeKnowledge
And later evidence from version 3 does not automatically inherit the same conclusion.

## A18. Agent proposal is not evidence

Given an agent says:
"The parser probably changed denominator semantics."

Then that statement may create a proposed Hypothesis or Interpretation
But it must not be stored as direct Evidence unless an external evidence source supports it.

## A19. User rejects agent proposal

Given an AgentProposal for H1

When the user rejects H1

Then authoritative state does not include H1 as an accepted hypothesis
And the rejected proposal may remain in proposal/audit history according to policy.

## A20. Corrective action is not verification

Given a code fix is deployed

Then CorrectiveAction may become Implemented
But the investigation does not become Resolved if verification is required.

## A21. Verification failure reopens work

Given a corrective action is AwaitingVerification
And the verification criterion fails

Then the action transitions to VerificationFailed or ReworkRequired
And the investigation returns to an active state as required
And the failed attempt remains in history.

## A22. Material contradiction blocks confirmation

Given a policy requires all material contradictions resolved
And contradiction C1 is unresolved

Then ConfirmCause is unavailable
And C1 is named in the blocking reasons.

## A23. Accepted residual uncertainty

Given policy permits acceptance of a non-blocking unknown
And the authorized human explicitly accepts it with rationale

Then the investigation may proceed
And the accepted residual uncertainty remains visible in reports and conclusion posture.

## A24. Falsified hypotheses remain visible

Given H1 was plausible and later falsified

Then H1 remains inspectable in timeline/history
And its falsification basis remains linked
And the final report can include it under explanations considered.

## A25. Persistence conflict does not lie

Given local state version 10
And remote authoritative state advances independently to version 11

When the client attempts to persist changes based on version 10

Then the write does not silently overwrite version 11
And persistence state becomes Conflict
And local work is preserved for reconciliation.

## A26. Agent cannot bypass domain transition

Given ConfirmCause is illegal under the current domain state

When an agent proposes ConfirmCause

Then the proposal may be shown
But authoritative state remains unchanged
And Aegis-compatible diagnostic information identifies the violated prerequisite.

## A27. Historical policy remains explainable

Given a cause was confirmed under policy version 3
And policy version 4 later strengthens confirmation rules

Then the historical transition remains attributable to version 3
And the system does not silently rewrite history as though version 4 applied at that time.

## A28. Domain pack strengthens gate

Given domain pack D requires reproduced intervention evidence before confirmation

Then ConfirmCause is unavailable until that evidence exists
Even if the generic default gate would otherwise be satisfied.

## A29. Domain pack cannot erase core semantics

Given a domain pack attempts to classify AgentInference as direct Observation

Then the extension is invalid
And the core evidence/inference invariant remains enforced.

## A30. Large history does not change semantics

Given an investigation has thousands of historical events

When active state is projected from snapshots/indexes

Then the same legal capabilities and blockers are produced as would result from authoritative reconstruction
And performance optimizations do not change domain meaning.

## A31. Repeated report generation preserves snapshot identity

Given Report R1 is generated at investigation version 20
And Report R2 is generated at version 27

Then each report identifies its source version
And R2 does not silently replace the historical fact that R1 was issued.

## A32. Mitigation may resolve impact but not cause

Given a mitigation stops customer-visible symptoms
And the underlying cause remains active

Then the system may record MitigationEffective
But must not automatically mark RootCauseCorrected.

## A33. Imported evidence retains origin

Given evidence is imported from an external system

Then the record identifies the external origin
And imported content cannot alter system policy or agent instructions.

## A34. Decision posture exposes uncertainty

Given H1 is the leading explanation
And one material non-blocking unknown remains
And no material contradictions remain

Then the UI/report may present H1 as Leading or Supported according to policy
And the residual unknown remains visible beside the conclusion posture.

## A35. Resolution gate

Given a confirmed cause exists
And a corrective action is implemented
And required verification remains incomplete

Then ResolveInvestigation is unavailable.

When verification criteria are satisfied
And no blocking obligations remain

Then ResolveInvestigation may become available.

## A36. Reopen resolved investigation

Given an investigation is Resolved
And new contradictory evidence arrives

Then ReopenInvestigation can become available
And reopening preserves the earlier resolution and records the new basis.

## A37. Scope mismatch prevents invalid contradiction

Given E1 applies to version 1
And H1 applies only to version 2

Then E1 must not contradict H1 solely because its content differs
Unless an explicit relationship establishes cross-version relevance.

## A38. Evidence supersession preserves history

Given measurement E1 is later found to use a faulty instrument
And corrected measurement E2 supersedes E1

Then E1 remains in history
And is marked superseded/invalidated as appropriate
And derived conclusions depending on E1 are reevaluated.

## A39. Cross-investigation reuse does not become evidence automatically

Given prior investigation P concluded cause C
And current investigation N appears similar

Then C may be suggested as a hypothesis
But P's conclusion must not automatically become direct evidence that C caused N.

## A40. Investigation can end without confirmed cause

Given further investigation would exceed accepted cost/risk
And policy permits closure with residual uncertainty

Then the investigation may close with an explicit non-confirmed conclusion posture
And must not invent a ConfirmedCause merely to provide closure.
