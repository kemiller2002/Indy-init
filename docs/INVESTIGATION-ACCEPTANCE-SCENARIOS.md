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


## A41. Unknown optional extension survives round trip

Given an investigation contains optional extension edf:new-construct@2
And a client understands the core investigation but not that extension

When the client edits unrelated core state and saves

Then the extension is preserved without semantic loss
And it is not silently removed, rewritten, or reclassified.

## A42. Unknown required extension protects writes

Given an investigation requires edf:critical-policy-construct@3 to interpret legal transitions
And the client does not understand that extension

Then the investigation may be shown in degraded or read-only form
And the client must not permit authoritative writes whose correctness depends on the missing semantics.

## A43. Experimental feature can be disabled safely

Given experimental construct experiment:edf-topology@0.3 was used in investigation I1
And the experiment is later disabled for new investigations

Then I1 remains readable
And its experimental records remain identifiable
And new investigations do not require the construct.

## A44. Material semantic change creates a new identity/version

Given edf:context-coverage@1 and a proposed replacement have materially different meanings

Then the replacement receives a distinct semantic version or identifier
And historical @1 records are not silently interpreted as the new meaning.

## A45. Policy change does not rewrite historical legality

Given transition T was legal under EDF policy P1
And EDF policy P2 later changes its prerequisites

Then T remains historically attributable to P1
And the system does not mark T as though P2 had governed it at the time.

## A46. Reassessment creates new state

Given conclusion C1 was reached under policy P1
And newer evidence or policy P2 warrants reevaluation

When the investigation is reassessed

Then C1 remains historical fact
And a new reassessment record links C1, P1, the new basis, and the current conclusion posture.

## A47. EDF metric replacement requires no core schema change

Given EDF retires metric M1 and introduces semantically different M2

Then M2 can be introduced through the EDF domain pack
And existing M1 observations remain interpretable
And the generic investigation kernel does not require a schema change solely because the EDF metric changed.

## A48. Retired construct has generic fallback

Given an investigation contains an EDF construct whose specialized renderer is no longer installed

Then the client can still show its semantic identifier, version, provenance, stability status, and safe generic content
And critical information does not disappear from the investigation.

## A49. Extension cannot weaken core invariant

Given an experimental EDF profile attempts to permit an AgentProposal to become direct Evidence without the required authoritative transition

Then activation or transition fails
And an Aegis-compatible fault identifies the violated core invariant.

## A50. AI proposal is revalidated after domain version change

Given an agent creates a proposal under EDF domain pack version D1
And the active domain pack changes to D2 before acceptance
And D2 changes semantics relevant to the proposal

Then the proposal is revalidated or marked stale
And it is not accepted automatically under D2.

## A51. Migration cannot invent unavailable historical data

Given a newer semantic model requires field F
And an old investigation never recorded enough information to derive F

When the old investigation is loaded or migrated

Then F remains Unknown, unavailable, or explicitly not recorded
And the migration must not synthesize a false historical value.

## A52. EDF construct does not become core by convenience

Given a new EDF construct is useful for multiple EDF experiments
But cross-domain necessity and stability have not been established

Then it remains a versioned EDF extension
And the generic kernel continues to operate without understanding the construct.


## A53. New EDF construct needs no generic aggregate change

Given EDF introduces experimental construct X
And X can be represented through a versioned extension envelope

When X is added to the EDF pack

Then the generic Investigation aggregate does not require a new core field or union case solely to support X.

## A54. Domain pack cannot create alternate history

Given an EDF pack wants specialized events

Then those events participate in the same authoritative investigation event/history contract
And the pack must not create a parallel authoritative history that can disagree silently with the core investigation.

## A55. Pack policy adds blocker without duplicating UI logic

Given EDF policy requires evidence E before ConfirmCause
And E is absent

Then the domain capability projection returns the blocker
And the UI consumes that blocker
And the UI does not need a hard-coded EDF-specific transition rule.

## A56. Optional pack removal preserves core investigation

Given an investigation contains optional EDF extensions
And the EDF pack is unavailable

Then generic core observations, evidence, hypotheses, unknowns, obligations, and conclusions remain inspectable
And optional EDF extension information is preserved through generic fallback.

## A57. Required pack removal fails safely

Given an investigation's legal interpretation depends on a required EDF extension
And the EDF pack is unavailable

Then unsafe semantic writes are disabled
And the system explains which required semantics are missing.

## A58. Reversible persistence choice

Given GitHub is the active persistence provider
And a future deployment uses a service/database provider

Then InvestigationId, domain semantics, event meaning, and canonical export remain unchanged
And GitHub-specific paths or SHAs are not required by the generic domain model.

## A59. Deferred question is not silently settled

Given universal causal DAG semantics are classified Deferred
And a feature needs to display causal relationships

Then the implementation may use a reversible projection or extension
But must not make a universal DAG representation mandatory persisted core state without an explicit promotion decision.

## A60. Experimental EDF result can be rejected cleanly

Given experimental EDF construct X was used in research investigations
And later research rejects X

Then new investigations can stop producing X
And historical X records remain readable and attributable
And no generic core migration is required solely to remove X from future EDF use.


## A61. UI consumes domain blocker

Given ConfirmCause is blocked by contradiction C7
When the workspace renders the ConfirmCause capability
Then the blocker originates from the policy/transition engine
And the UI shows the reason
And no duplicate UI-specific EDF legality rule is required.

## A62. Actor permission differs from semantic legality

Given AddEvidence is semantically legal
And actor A lacks permission
Then the capability explains the permission blocker.

Given actor B has permission
And the investigation is in a state where AddEvidence is semantically illegal under policy
Then the capability explains the domain blocker.

## A63. Policy exception is auditable

Given policy allows an authorized user to accept a non-blocking residual unknown
When the user exercises the exception
Then actor, reason, authority, scope, policy version, and residual uncertainty are recorded
And the core Unknown semantics are not changed.

## A64. Retry does not duplicate events

Given command C with idempotency key K was remotely applied
And network loss prevents the client from seeing the acknowledgement
When C with K is retried
Then the prior result is returned or recognized
And no duplicate semantic event is appended.

## A65. Stale offline command is revalidated

Given command C was queued offline against aggregate version 20
And remote state changes to version 24
When the client reconnects
Then C is not blindly replayed
And it is re-evaluated against version 24 and current policy
And any changed blockers are shown.

## A66. Policy comparison does not mutate investigation

Given immutable investigation snapshot V30
When EDF policy P1 and P2 are both evaluated against V30
Then two derived policy-evaluation results are produced
And V30 authoritative state is unchanged.

## A67. Policy pin supports reproducibility

Given research investigation I pins EDF policy P1
And EDF P2 later becomes default
Then reloading I uses or explicitly identifies P1 for its historical/continued research behavior
And P2 does not silently replace the pinned version.

## A68. Multi-event transition is atomic remotely

Given one allowed command emits events E1, E2, and E3
And the provider cannot persist the complete event set
Then the transition is not represented as remotely committed
And partial provider writes are recovered or treated as failure according to the provider contract.

## A69. GitHub SHA remains provider metadata

Given a GitHub-backed investigation moves to another provider
Then its InvestigationId and semantic AggregateVersion remain meaningful
And no core entity requires the former GitHub SHA to retain identity.

## A70. Offline restart preserves pending work

Given the user records evidence offline
And the application is closed before synchronization
When the application restarts
Then the last remote-confirmed state is identifiable
And the pending evidence command is recoverable
And the UI shows that it has not yet been remotely confirmed.

## A71. External artifact changed

Given evidence E references artifact A with digest H1
And later retrieval of A produces digest H2
Then the product marks the artifact as changed or integrity-mismatched
And does not silently treat H2 content as the originally evaluated artifact.

## A72. External artifact unavailable

Given evidence E references external artifact A
And A can no longer be retrieved
Then E remains in investigation history
And artifact availability is shown as unavailable
And the system does not delete E solely because A is unavailable.

## A73. Complete export/import round trip

Given investigation I has core events, EDF extensions, unresolved unknowns, historical conclusions, and report references
When a complete canonical export is imported into a compatible empty deployment
Then entity IDs, semantic/policy versions, extension envelopes, authoritative event order, provenance, and uncertainty are preserved.

## A74. Import collision cannot overwrite silently

Given target deployment already contains InvestigationId I
When a package with InvestigationId I is imported
Then the system requires an explicit duplicate/same-history/copy/reconciliation choice
And does not silently overwrite the existing investigation.

## A75. Cross-provider migration preserves meaning

Given investigation I is exported from GitHub provider G
And imported into service provider S
Then InvestigationId, AggregateVersion, semantic event meanings, domain-pack references, and historical conclusions remain unchanged
And only provider metadata changes.

## A76. Research experiment pins immutable source

Given experiment X consumes investigation version 44
And the investigation later advances to version 51
Then X remains attributable to version 44
And rerunning X as a reproduction can resolve the same source state.

## A77. Snapshot mismatch is detected

Given snapshot S declares aggregate version 40
And authoritative event history establishes a different state at version 40
Then S is rejected or marked invalid
And the mismatch is not silently accepted.

## A78. Duplicate event ID is corruption

Given persisted history contains two non-identical events with the same EventId
Then load/integrity validation fails safely
And the system surfaces an Aegis-compatible persistence integrity fault.

## A79. Semantic conflict is not JSON merge

Given user A falsifies H1
And user B, from stale state, attempts a transition that requires H1 to remain active
Then conflict resolution re-evaluates B's intended command against the current domain state
And does not resolve the conflict by mechanically merging object fields.

## A80. Independent additions may commute

Given user A adds Evidence E1
And user B independently adds Observation O2 from the same prior version
And neither change affects the legality or meaning of the other
Then the persistence/reconciliation layer may apply both through verified commutative handling
And both receive distinct semantic events and provenance.


## A81. Chat statement cannot confirm cause

Given the AI writes "The root cause is definitely H1"
And ConfirmCause is not legal under current policy
Then authoritative cause state does not change
And the workspace continues to show the blockers.

## A82. AI proposal cites authoritative evidence

Given AI proposes hypothesis H2 based on evidence E3 and E8
Then the proposal references E3 and E8 by authoritative identity
And the user can inspect those records from the proposal.

## A83. AI interpretation is not silently admitted as evidence

Given the AI infers "the service likely retried three times"
And no source directly establishes that fact
Then the output is a proposed Interpretation/Hypothesis
And no direct Evidence record is created from the inference alone.

## A84. Evidence containing instructions cannot override policy

Given imported evidence contains text instructing the model to ignore investigation rules
When the evidence is included in AI context
Then it remains delimited as untrusted evidence content
And core/domain instructions remain authoritative.

## A85. Stale proposal after new contradiction

Given proposal P recommends ConfirmCause for H1
And new contradiction C9 is admitted before P is accepted
Then P is marked stale or revalidated
And cannot be blindly accepted from its earlier state.

## A86. Relevant negative knowledge prevents repetition

Given hypothesis H3 was previously falsified within the same applicable scope
When AI considers proposing H3 again
Then relevant NegativeKnowledge is included
And the AI either avoids H3 or explicitly identifies new evidence justifying reopening.

## A87. Challenge mode attacks leading explanation

Given H1 is Leading
When the user invokes Challenge
Then the AI is asked to identify falsifying evidence, hidden assumptions, plausible alternatives, and evidence dependencies
And results are proposals/analysis rather than authoritative mutations.

## A88. Context omission is visible

Given the context builder cannot include all materially relevant state due to budget
Then the reasoning run records or communicates the omission
And the AI must not claim to have comprehensively reviewed all evidence.

## A89. AI outage leaves manual work usable

Given the AI provider is unavailable
Then the user can still inspect authoritative state
And perform supported manual evidence/hypothesis/action operations
And legal capabilities continue to derive deterministically.

## A90. Model disagreement is not system evidence

Given model A proposes H1
And model B proposes H2
Then both can be retained as competing proposals
And the disagreement itself is not Evidence that H1 or H2 caused the observed outcome.

## A91. Workspace understandable without chat

Given an investigation contains observations, three hypotheses, evidence, one contradiction, and two blockers
Then a user can identify those states from the workspace without reading the conversation transcript.

## A92. Blocked action remains discoverable

Given ConfirmCause is blocked
Then the workspace still exposes ConfirmCause as unavailable
And identifies the blockers
And links to actionable resolution paths when available.

## A93. Recommended action differs from legal action

Given five actions are legal
And one is materially more discriminating
Then the workspace may recommend that action
And the remaining legal actions stay available
And policy legality is not confused with AI recommendation.

## A94. Blocking contradiction survives responsive collapse

Given the workspace is displayed on a narrow phone
And a material contradiction blocks confirmation
Then the contradiction/blocker remains visible or immediately surfaced in the primary task flow
And is not hidden only in a secondary settings screen.

## A95. Local pending state is understandable

Given evidence E12 was accepted locally while offline
Then the workspace can show E12 in the local optimistic state
And clearly indicates synchronization is pending
And does not present it as remotely confirmed.

## A96. Original observation remains inspectable after normalization

Given the user entered original text O
And the AI creates normalized observation N
Then the workspace allows inspection of O and N separately
And editing N does not modify O.

## A97. Hypothesis comparison exposes assumptions

Given H1 and H2 depend on different material assumptions
Then comparison view exposes those assumption dependencies
And does not rank hypotheses solely on count of supporting evidence.

## A98. Cause review exposes alternatives

Given H1 is eligible for confirmation
And H2 remains a materially plausible but addressed alternative
Then Cause Review shows how H2 was addressed before confirmation.

## A99. Corrective implementation does not look verified

Given corrective action CA1 is Implemented
And no verification result exists
Then the workspace displays Awaiting Verification or equivalent
And does not display the investigation as fully resolved when verification is required.

## A100. Screen reader receives material state change

Given H2 transitions to Falsified
When the workspace updates
Then assistive technology receives an appropriate semantic announcement
And the change is not communicated only by color or animation.
