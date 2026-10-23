# Command and Event Catalog

Status: normative first implementation profile for commands and domain vocabulary; persisted event names per §0.

This document defines the initial authoritative command and event vocabulary for the Root-Cause Investigator. It is intentionally more concrete than the semantic specifications, but it preserves the research boundaries in OPEN-QUESTIONS.md.

> **Normative status.** **Normative** for commands and domain vocabulary. **Explanatory** for persisted event names: the event names below are domain vocabulary, and §0 gives the v1.0.0 wire event for each. Persisted data is governed only by the frozen Causa storage contract v1.0.0 ([README.md](../requirements/causa/data/v1.0.0/README.md), [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md), [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md)). Where this document states a persisted field, event name, enum value, layout, hash or concurrency rule differently, the contract governs; such statements are marked *Superseded by frozen contract v1.0.0* in place.

## 0. Persisted (v1.0.0) event for each catalog event

The persisted event vocabulary is the closed registry of 31 events in [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md). Names with no v1.0.0 wire event are deferred: adding one is a compatible additive extension (new minor contract version, CONTRACT §13.1), and a v1.0.0 reader refuses them. Line numbers below refer to this document before §0 was inserted.

### 0.1 Catalog events persisted as a registered v1.0.0 event (58 catalog names → 31 wire events)

| catalog event (line) | v1.0.0 wire event | discriminating field |
|---|---|---|
| InvestigationStarted (57) | `InvestigationStarted{title, observationId, original}` | — |
| InvestigationActivated (59) | `LifecycleChanged` | `command: activateInvestigation`, `reason: null` |
| InvestigationMarkedAwaitingEvidence (60) | `LifecycleChanged` | `command: markAwaitingEvidence`, `reason` text |
| InvestigationMarkedAwaitingVerification (61) | `LifecycleChanged` | `command: markAwaitingVerification`, `reason: null` |
| InvestigationResolved (62) | `LifecycleChanged` | `command: resolveInvestigation`, `reason: null`, no conclusionId |
| InvestigationClosed (63) | `LifecycleChanged` | `command: closeInvestigation`, `reason: null` |
| InvestigationReopened (64, 379) | `LifecycleChanged` | `command: reopenInvestigation` (user) or `verificationFailed` (follow-on of a failed verification while awaiting verification) |
| ObservationRecorded (79) | `ObservationRecorded` | — |
| HypothesisRecorded (135) | `HypothesisRecorded` | — |
| HypothesisMarkedPlausible (136) | `HypothesisTransitioned` | `command: markPlausible` |
| HypothesisMarkedSupported (137) | `HypothesisTransitioned` | `command: markSupported` |
| HypothesisMarkedWeakened (138) | `HypothesisTransitioned` | `command: markWeakened` |
| HypothesisFalsified (139) | `HypothesisTransitioned` | `command: falsify` (no NegativeKnowledge follow-on) |
| HypothesisSuperseded (140) | `HypothesisTransitioned` | `command: supersede` |
| HypothesisReopened (141) | `HypothesisTransitioned` | `command: reopen`, `to: plausible` |
| CauseConfirmed (142, 347) | `CausalAssertionConfirmed{causeId, policy}` | — |
| ConfirmedCauseReopened (348) | `CausalAssertionReopened{causeId, reason}` | — |
| AssumptionRecorded (164) | `AssumptionRecorded` | — |
| AssumptionProvisionallyAccepted / AssumptionVerified / AssumptionRejected (165-167) | `AssumptionTransitioned` | `state: provisionallyAccepted / verified / rejected` |
| EvidenceAdmitted (180, 250) | `EvidenceAdmitted` | no `verification` field |
| EvidenceVerificationChanged (181) | `EvidenceVerificationAssessed{evidenceId, verification}` | — |
| EvidenceSuperseded (183) | `EvidenceSuperseded{evidenceId, replacementEvidenceId, reason}` | — |
| EvidenceRelationshipRecorded (195) | `EvidenceRelated` | — |
| PredictionRecorded (207) | `PredictionRecorded` | timing decided by the engine |
| InvestigationActionRecorded (232) | `ActionRecorded` | — |
| InvestigationActionApproved / Rejected / Started / Completed / Cancelled (233-235, 238, 240) | `ActionTransitioned` | `state: approved / rejected / inProgress / completed / cancelled` (completed is emitted inside the RecordFinding group) |
| FindingRecorded (248) | `FindingRecorded` | `interpretation` nullable |
| UnknownIdentified (273) | `UnknownIdentified` | — |
| UnknownInvestigationStarted / UnknownResolved / UnknownAcceptedAsResidual (274-276) | `UnknownTransitioned` | `state: investigating / resolved / acceptedResidual`, `basis` |
| NegativeKnowledgeEstablished (286) | `NegativeKnowledgeEstablished` | `basisEvidenceIds` ≥ 1 |
| ContradictionRecorded (311) | `ContradictionRecorded` | — |
| ContradictionTriaged / ContradictionResolved / ContradictionAcceptedAsResidual (312, 314, 315) | `ContradictionTransitioned` | `state: triaged / resolved / acceptedResidual`, `basis` |
| CausalAssertionRecorded (342) | `CausalAssertionRecorded` | — |
| CorrectiveActionRecorded (373) | `CorrectiveActionRecorded` | — |
| CorrectiveActionApproved / Started / Implemented / AwaitingVerification / VerifiedEffective / VerificationFailed / ReworkRequired / Cancelled (374-381) | `CorrectiveActionTransitioned` | `state: approved / inProgress / implemented / awaitingVerificationOfEffect / verifiedEffective / verificationFailed / reworkRequired / cancelled` (the two verification outcomes are follow-ons of `VerificationRecorded`) |
| VerificationCriterionRecorded (387) | `CriterionRecorded` | — |
| VerificationRecorded (388) | `VerificationRecorded` | satisfied ⇒ evidence cited |
| ConclusionRecorded (400) | `ConclusionRecorded` | — |
| ProposalRecorded (412) | `ProposalRecorded` | agent actor allowed only here |
| ProposalAccepted (413) | `ProposalWasAccepted{proposalId, targetId}` | follows the target entity's event in the same command group |
| ProposalRejected (414) | `ProposalWasRejected{proposalId, reason}` | — |

### 0.2 Catalog events with no v1.0.0 event: deferred (75 names)

Adding any of these is a compatible additive extension (new minor contract version with its own tree, CONTRACT §13.1); a v1.0.0 reader refuses them.

| catalog area (lines) | event names |
|---|---|
| Investigation (58, 65-67) | InvestigationRenamed, InvestigationArchived, InvestigationRelationshipAdded, InvestigationRelationshipSuperseded |
| Observation (80-82) | ObservationNormalizationAdded, ObservationVerificationChanged, ObservationSuperseded |
| Claim (92-94) | ClaimRecorded, ClaimAssessmentAdded, ClaimSuperseded |
| Interpretation (102-103) | InterpretationRecorded, InterpretationSuperseded |
| Assumption (168-170) | AssumptionExpired, AssumptionDependencyAdded, AssumptionDependencySuperseded |
| Evidence (182, 184-187) | EvidenceMetadataCorrected, EvidenceArtifactAvailabilityChanged, EvidenceArtifactIntegrityMismatchRecorded, EvidenceDependencyAdded, EvidenceDependencySuperseded |
| Evidence relationship (196-197) | EvidenceRelationshipReassessed, EvidenceRelationshipSuperseded |
| Prediction (208-209) | PredictionMarkedRetrospective, PredictionSuperseded |
| Investigation action (236-237, 239) | InvestigationActionBlocked, InvestigationActionUnblocked, InvestigationActionInterpreted |
| Finding (249-251) | FindingNormalizationAdded, FindingAdmittedAsEvidence, FindingSuperseded |
| Unknown (277-278) | UnknownSuperseded, UnknownMaterialityChanged |
| Negative knowledge (287-288) | NegativeKnowledgeChallenged, NegativeKnowledgeSuperseded |
| Contradiction (313, 316-317) | ContradictionInvestigationStarted, ContradictionSuperseded, ContradictionMaterialityChanged |
| Obligation (327-332) | ObligationAccepted, ObligationAssigned, ObligationStarted, ObligationResolved, ObligationWaived, ObligationSuperseded |
| Causal assertion (343-346) | CausalMemberAdded, CausalMemberSuperseded, CausalMechanismRecorded, CausalSupportAssessmentRecorded |
| Verification (389-390) | VerificationCriterionSuperseded, VerificationSuperseded |
| Conclusion (401-402) | ConclusionReassessed, ConclusionSuperseded |
| Proposal (415-417) | ProposalRevised, ProposalDeferred, ProposalMarkedStale |
| Extension / semantic version (427-436) | ExtensionAttached, ExtensionSuperseded, ExtensionMigrationRecorded, DomainPackActivated, DomainPackUpgraded, DomainPackDeactivated, ExperimentalProfileActivated, ExperimentalProfileDeactivated, PolicyVersionPinned, PolicyVersionPinChanged |
| Review (444-448) | ReviewRequested, ReviewApproved, ReviewRejected, ReviewWithdrawn, ReviewExpired |
| Report (458-460) | ReportIssued, ReportSuperseded |
| Import / recovery (470-473) | InvestigationImported, InvestigationForked, InvestigationRecoveryRecorded, RedactionRecorded |

The catalog follows one rule:

> Persist what happened and what a human/system explicitly decided. Derive comparative or analytical posture where practical.

In particular, the first implementation should avoid persisting "Leading" merely because it is currently displayed as a hypothesis posture. Leading is initially a derived comparison unless later research promotes it to durable state.

## 1. Command conventions

Every command is carried inside CommandEnvelope from POLICY-ENGINE.md.

A command:

- expresses intent
- names its target explicitly
- carries ExpectedAggregateVersion
- carries Actor and provenance
- is evaluated by the policy engine
- emits zero or more semantic events when allowed
- never writes persistence directly

Commands are named as verbs.

Events are named in past tense.

## 2. Event conventions

Events record completed authoritative facts.

Events must not contain UI-only state.

Events should be narrow enough that later projections can evolve without rewriting history.

Bad event:

```text
WorkspaceUpdated
```

Good events:

```text
EvidenceAdmitted
HypothesisFalsified
UnknownAcceptedAsResidual
```

*Persisted (frozen contract v1.0.0):* these persist as `EvidenceAdmitted`, `HypothesisTransitioned{command: falsify}` and `UnknownTransitioned{state: acceptedResidual}` (§0).

## 3. Investigation commands

| Command | Primary event(s) | Core prerequisites |
| --- | --- | --- |
| StartInvestigation | InvestigationStarted | valid identity, title/source statement, compatible semantic manifest |
| RenameInvestigation | InvestigationRenamed | investigation exists, actor authorized |
| ActivateInvestigation | InvestigationActivated | minimum activation policy satisfied |
| MarkAwaitingEvidence | InvestigationMarkedAwaitingEvidence | active investigation, reason supplied |
| MarkAwaitingVerification | InvestigationMarkedAwaitingVerification | verification work exists |
| ResolveInvestigation | InvestigationResolved | resolution gate satisfied |
| CloseInvestigation | InvestigationClosed | resolution/closure policy satisfied |
| ReopenInvestigation | InvestigationReopened | new evidence, failed verification, contradiction, or explicit policy-approved basis |
| ArchiveInvestigation | InvestigationArchived | archive policy satisfied |
| RelateInvestigation | InvestigationRelationshipAdded | both references valid/authorized |
| RemoveInvestigationRelationship | InvestigationRelationshipSuperseded | relationship exists; history retained |

*Persisted (frozen contract v1.0.0):* lifecycle events are one event, `LifecycleChanged{from, to, command, reason}`, with `command` = `activateInvestigation`, `markAwaitingEvidence`, `markAwaitingVerification`, `resolveInvestigation`, `closeInvestigation` or `reopenInvestigation`; `reason` is `null` except for markAwaitingEvidence and reopen. Rename, archive and investigation relationships are deferred (§0.2).

Notes:

- Draft is represented by InvestigationStarted before activation.
- Reopened is better represented by an event and lifecycle transition back to Active than as a permanent standalone lifecycle state.
- Archived is administrative and must not erase semantic history.

## 4. Observation commands

| Command | Event(s) | Prerequisites |
| --- | --- | --- |
| RecordObservation | ObservationRecorded | original source content present |
| AddObservationNormalization | ObservationNormalizationAdded | observation exists |
| VerifyObservation | ObservationVerificationChanged | verification basis supplied |
| SupersedeObservation | ObservationSuperseded | replacement/basis supplied |

Invariant:

Observation original content is immutable after ObservationRecorded. Corrections use supersession or added normalization, not rewriting.

## 5. Claim commands

| Command | Event(s) | Prerequisites |
| --- | --- | --- |
| RecordClaim | ClaimRecorded | proposition and scope valid |
| AddClaimAssessment | ClaimAssessmentAdded | claim exists; assessment basis supplied |
| SupersedeClaim | ClaimSuperseded | replacement/reason supplied |

Claim epistemic posture should initially be derived from assessments/evidence relationships rather than changed through a generic SetClaimState command.

## 6. Interpretation commands

| Command | Event(s) | Prerequisites |
| --- | --- | --- |
| RecordInterpretation | InterpretationRecorded | source refs or explicit unsupported-proposal marker |
| SupersedeInterpretation | InterpretationSuperseded | interpretation exists; reason supplied |

Interpretations do not become Evidence merely by being recorded.

## 7. Hypothesis commands

Authoritative hypothesis lifecycle for the first implementation:

```text
Proposed
  -> Plausible
  -> Supported
  -> Confirmed   (superseded: see note below)

Proposed/Plausible/Supported
  -> Weakened
  -> Falsified

Any non-confirmed active state
  -> Superseded

Falsified
  -> Plausible via ReopenHypothesis

Confirmed
  -> Reopened/Supported or other policy-defined active posture
```

> **Superseded by frozen contract v1.0.0:** the persisted `hypothesisState` values are `proposed`, `plausible`, `supported`, `weakened`, `falsified`, `superseded`; there is **no** hypothesis `Confirmed` state. Confirmation belongs to the causal assertion (`causeState` `confirmed`, §18), consistent with STATE-TRANSITION-TABLES §2.

"Leading" is initially derived rather than persisted.

| Command | Event(s) | Prerequisites |
| --- | --- | --- |
| RecordHypothesis | HypothesisRecorded | testable explanatory proposition |
| MarkHypothesisPlausible | HypothesisMarkedPlausible | relevance/rationale, no known immediate falsifier |
| MarkHypothesisSupported | HypothesisMarkedSupported | material support, no policy blocker |
| MarkHypothesisWeakened | HypothesisMarkedWeakened | weakening basis |
| FalsifyHypothesis | HypothesisFalsified (persisted: `HypothesisTransitioned{command: falsify}`), possibly NegativeKnowledgeEstablished (v1.0.0: a separate EstablishNegativeKnowledge command) | falsifying basis + scope |
| SupersedeHypothesis | HypothesisSuperseded | reason/replacement optional |
| ReopenHypothesis | HypothesisReopened | new/corrected evidence, scope, or invalidated falsification premise |
| ConfirmCause | CauseConfirmed | causal assertion references supported hypothesis/conditions and cause-confirmation gate is satisfied |

Do not provide a generic SetHypothesisState command.

*Persisted (frozen contract v1.0.0):* `HypothesisRecorded`; every hypothesis transition is `HypothesisTransitioned{hypothesisId, from, to, command, reason, evidenceIds}` with `command` = `markPlausible|markSupported|markWeakened|falsify|supersede|reopen`; ConfirmCause persists `CausalAssertionConfirmed{causeId, policy}`.

## 8. Assumption commands

Lifecycle:

```text
ExplicitUnverified
  -> ProvisionallyAccepted
  -> Verified

ExplicitUnverified/ProvisionallyAccepted
  -> Rejected

Any active state
  -> Expired
```

| Command | Event(s) |
| --- | --- |
| RecordAssumption | AssumptionRecorded |
| ProvisionallyAcceptAssumption | AssumptionProvisionallyAccepted |
| VerifyAssumption | AssumptionVerified |
| RejectAssumption | AssumptionRejected |
| ExpireAssumption | AssumptionExpired |
| RelateAssumptionDependency | AssumptionDependencyAdded |
| SupersedeAssumptionDependency | AssumptionDependencySuperseded |

A rejected/expired material assumption may emit or derive ReevaluationRequired obligations for dependent entities.

*Persisted (frozen contract v1.0.0):* `AssumptionRecorded`; transitions are `AssumptionTransitioned{assumptionId, state}` with `state` = `provisionallyAccepted|verified|rejected`. `Expired` and the dependency events are deferred (no v1.0.0 event reaches `expired`).

## 9. Evidence commands

Evidence admission lifecycle is intentionally small.

| Command | Event(s) | Notes |
| --- | --- | --- |
| AdmitEvidence | EvidenceAdmitted | preserves source/provenance |
| VerifyEvidence | EvidenceVerificationChanged | verification basis recorded |
| CorrectEvidenceMetadata | EvidenceMetadataCorrected | does not rewrite original source |
| SupersedeEvidence | EvidenceSuperseded | history preserved |
| MarkEvidenceArtifactUnavailable | EvidenceArtifactAvailabilityChanged | evidence record remains |
| RecordEvidenceIntegrityMismatch | EvidenceArtifactIntegrityMismatchRecorded | does not substitute new bytes |
| AddEvidenceDependency | EvidenceDependencyAdded | source dependency explicit |
| SupersedeEvidenceDependency | EvidenceDependencySuperseded | history retained |

Evidence is not physically deleted through ordinary investigation commands.

*Persisted (frozen contract v1.0.0):* `EvidenceAdmitted` (no `verification` field; starts `unverified`), `EvidenceVerificationAssessed{evidenceId, verification}` (not EvidenceVerificationChanged), `EvidenceSuperseded`. Metadata correction, artifact availability/integrity and dependency events are deferred.

## 10. Evidence relationship commands

| Command | Event(s) |
| --- | --- |
| RelateEvidence | EvidenceRelationshipRecorded |
| ReassessEvidenceRelationship | EvidenceRelationshipReassessed |
| SupersedeEvidenceRelationship | EvidenceRelationshipSuperseded |

Relationship kinds use CoreRelationship or versioned extension SemanticId. ***Superseded by frozen contract v1.0.0:** the persisted event is `EvidenceRelated`; `kind` is exactly `supports|contradicts|discriminates|insufficient`; extension kinds require a new contract version. Reassess/Supersede are deferred.*

A Reassess event records the new assessment and reason rather than rewriting the old relationship event.

## 11. Prediction commands

| Command | Event(s) |
| --- | --- |
| RecordPrediction | PredictionRecorded |
| MarkPredictionRetrospective | PredictionMarkedRetrospective |
| SupersedePrediction | PredictionSuperseded |

Whether a prediction is prospective should normally be established from event ordering/time and result availability, not editable after the fact without an explicit corrective event.

## 12. Investigation action commands

Lifecycle:

```text
Proposed
  -> Approved
  -> InProgress
  -> Completed
  -> Interpreted

Proposed/Approved -> Rejected
Approved/InProgress -> Cancelled
Approved/InProgress -> Blocked
Blocked -> Approved/InProgress
```

> **Superseded by frozen contract v1.0.0:** `Blocked` has no v1.0.0 `actionState` value (deferred); `Interpreted` exists but no v1.0.0 event reaches it.

| Command | Event(s) |
| --- | --- |
| RecordInvestigationAction | InvestigationActionRecorded |
| ApproveInvestigationAction | InvestigationActionApproved |
| RejectInvestigationAction | InvestigationActionRejected |
| StartInvestigationAction | InvestigationActionStarted |
| BlockInvestigationAction | InvestigationActionBlocked |
| UnblockInvestigationAction | InvestigationActionUnblocked |
| CompleteInvestigationAction | InvestigationActionCompleted |
| InterpretInvestigationAction | InvestigationActionInterpreted |
| CancelInvestigationAction | InvestigationActionCancelled |

Completed means execution ended, not that the finding supports any conclusion.

*Persisted (frozen contract v1.0.0):* `ActionRecorded`; transitions are `ActionTransitioned{actionId, state}` with `state` = `approved|rejected|inProgress|completed|cancelled`. There is no separate complete command: RecordFinding emits `FindingRecorded` followed by `ActionTransitioned(completed)` (preceded by `ActionTransitioned(inProgress)` when the action was only approved) in one command group.

## 13. Finding commands

| Command | Event(s) |
| --- | --- |
| RecordFinding | FindingRecorded |
| NormalizeFinding | FindingNormalizationAdded |
| AdmitFindingAsEvidence | EvidenceAdmitted, FindingAdmittedAsEvidence |
| SupersedeFinding | FindingSuperseded |

Admission as evidence creates or links a distinct Evidence identity.

*Persisted (frozen contract v1.0.0):* `FindingRecorded{findingId, actionId, originalResult, interpretation|null}`; normalization, admission-as-evidence and supersession events are deferred.

## 14. Unknown commands

Lifecycle:

```text
Identified
  -> Investigating
  -> Resolved

Identified/Investigating
  -> AcceptedResidual

Identified/Investigating
  -> Superseded
```

| Command | Event(s) |
| --- | --- |
| IdentifyUnknown | UnknownIdentified |
| StartInvestigatingUnknown | UnknownInvestigationStarted |
| ResolveUnknown | UnknownResolved |
| AcceptUnknownAsResidual | UnknownAcceptedAsResidual |
| SupersedeUnknown | UnknownSuperseded |
| ChangeUnknownMateriality | UnknownMaterialityChanged |

ChangeUnknownMateriality requires policy/provenance and must never be a silent convenience transition.

*Persisted (frozen contract v1.0.0):* `UnknownIdentified`; transitions are `UnknownTransitioned{unknownId, state, basis}` with `state` = `investigating|resolved|acceptedResidual`. `Superseded` and materiality change are deferred.

## 15. Negative knowledge commands

| Command | Event(s) |
| --- | --- |
| EstablishNegativeKnowledge | NegativeKnowledgeEstablished |
| ChallengeNegativeKnowledge | NegativeKnowledgeChallenged |
| SupersedeNegativeKnowledge | NegativeKnowledgeSuperseded |

Negative knowledge remains scoped. Challenge does not erase the original basis.

## 16. Contradiction commands

Lifecycle:

```text
Detected
  -> Triaged
  -> Investigating
  -> Resolved

Detected/Triaged/Investigating
  -> AcceptedResidual

Any active state
  -> Superseded
```

| Command | Event(s) |
| --- | --- |
| RecordContradiction | ContradictionRecorded |
| TriageContradiction | ContradictionTriaged |
| StartInvestigatingContradiction | ContradictionInvestigationStarted |
| ResolveContradiction | ContradictionResolved |
| AcceptContradictionAsResidual | ContradictionAcceptedAsResidual |
| SupersedeContradiction | ContradictionSuperseded |
| ChangeContradictionMateriality | ContradictionMaterialityChanged |

Resolution must identify a resolution basis, not merely a preferred side.

*Persisted (frozen contract v1.0.0):* `ContradictionRecorded`; transitions are `ContradictionTransitioned{contradictionId, state, basis}` with `state` = `triaged|resolved|acceptedResidual`. Start-investigating, supersede and materiality change are deferred.

## 17. Obligation commands

Some obligations are derived. Durable obligations use:

| Command | Event(s) |
| --- | --- |
| AcceptObligation | ObligationAccepted |
| AssignObligation | ObligationAssigned |
| StartObligation | ObligationStarted |
| ResolveObligation | ObligationResolved |
| WaiveObligation | ObligationWaived |
| SupersedeObligation | ObligationSuperseded |

WaiveObligation exists only where originating policy explicitly permits waiver. Core invariant obligations are not waivable.

## 18. Causal assertion commands

A causal assertion has its own persisted lifecycle: Proposed, Confirmed, Reopened, Superseded. This is deliberately separate from HypothesisState.

| Command | Event(s) |
| --- | --- |
| RecordCausalAssertion | CausalAssertionRecorded |
| AddCausalMember | CausalMemberAdded |
| SupersedeCausalMember | CausalMemberSuperseded |
| RecordCausalMechanism | CausalMechanismRecorded |
| RecordCausalSupportAssessment | CausalSupportAssessmentRecorded |
| ConfirmCause | CauseConfirmed |
| ReopenConfirmedCause | ConfirmedCauseReopened |

ConfirmCause is protected by the active confirmation policy.

Causal roles use stable generic cases plus extension SemanticId. ***Superseded by frozen contract v1.0.0:** `causeRole` is exactly `rootCause|contributingFactor|trigger|enablingCondition`; persisted events are `CausalAssertionRecorded`, `CausalAssertionConfirmed{causeId, policy}` (not CauseConfirmed) and `CausalAssertionReopened{causeId, reason}` (not ConfirmedCauseReopened). Member, mechanism and support-assessment events are deferred.*

## 19. Corrective action commands

Lifecycle:

```text
Proposed
  -> Approved
  -> InProgress
  -> Implemented
  -> AwaitingVerification   (persisted: awaitingVerificationOfEffect)
  -> VerifiedEffective

AwaitingVerification
  -> VerificationFailed
  -> ReworkRequired
```

| Command | Event(s) |
| --- | --- |
| RecordCorrectiveAction | CorrectiveActionRecorded |
| ApproveCorrectiveAction | CorrectiveActionApproved |
| StartCorrectiveAction | CorrectiveActionStarted |
| MarkCorrectiveActionImplemented | CorrectiveActionImplemented |
| StartCorrectiveActionVerification | CorrectiveActionAwaitingVerification |
| MarkCorrectiveActionVerified | CorrectiveActionVerifiedEffective |
| MarkCorrectiveActionVerificationFailed | CorrectiveActionVerificationFailed, possibly InvestigationReopened |
| RequireCorrectiveActionRework | CorrectiveActionReworkRequired |
| CancelCorrectiveAction | CorrectiveActionCancelled |

*Persisted (frozen contract v1.0.0):* `CorrectiveActionRecorded`; transitions are `CorrectiveActionTransitioned{correctiveActionId, state}` (state `awaitingVerificationOfEffect`, not AwaitingVerification). There is no separate MarkCorrectiveActionVerified / VerificationFailed command: they are follow-on events of `VerificationRecorded` in the RecordVerification command group (order: `VerificationRecorded`, `CorrectiveActionTransitioned`, and, if the investigation awaited verification and the outcome failed, `LifecycleChanged{command: verificationFailed}` instead of InvestigationReopened).

## 20. Verification commands

| Command | Event(s) |
| --- | --- |
| RecordVerificationCriterion | VerificationCriterionRecorded |
| RecordVerification | VerificationRecorded |
| SupersedeVerificationCriterion | VerificationCriterionSuperseded |
| SupersedeVerification | VerificationSuperseded |

A verification result references its criterion and evidence/finding basis.

*Persisted (frozen contract v1.0.0):* `CriterionRecorded` (not VerificationCriterionRecorded) and `VerificationRecorded`; supersession events are deferred.

## 21. Conclusion commands

Conclusions are authoritative snapshots of justified posture.

| Command | Event(s) |
| --- | --- |
| RecordConclusion | ConclusionRecorded |
| ReassessConclusion | ConclusionReassessed |
| SupersedeConclusion | ConclusionSuperseded |

RecordConclusion cannot grant a stronger causal state than policy permits. It summarizes authoritative state rather than overriding it.

*Persisted (frozen contract v1.0.0):* `ConclusionRecorded`; the current conclusion is the one with the highest `recordedAtVersion`. Reassess/Supersede are deferred.

## 22. Proposal commands

AI/human proposals can be durably recorded without becoming accepted domain state.

| Command | Event(s) |
| --- | --- |
| RecordProposal | ProposalRecorded |
| AcceptProposal | ProposalAccepted plus target domain event(s) |
| RejectProposal | ProposalRejected |
| EditProposal | ProposalRevised |
| DeferProposal | ProposalDeferred |
| MarkProposalStale | ProposalMarkedStale |

AcceptProposal re-evaluates the resulting target command at acceptance time.

It does not replay the proposal's original legality judgment.

*Persisted (frozen contract v1.0.0):* `ProposalRecorded`, `ProposalWasAccepted{proposalId, targetId}` (not ProposalAccepted; it follows the target entity's event in the same command group) and `ProposalWasRejected{proposalId, reason}` (not ProposalRejected). Revise, defer and stale are deferred; `proposalState` is `active|accepted|rejected`.

## 23. Extension and semantic-version commands

| Command | Event(s) |
| --- | --- |
| AttachExtension | ExtensionAttached |
| SupersedeExtension | ExtensionSuperseded |
| MigrateExtensionSideBySide | ExtensionMigrationRecorded |
| ActivateDomainPack | DomainPackActivated |
| UpgradeDomainPack | DomainPackUpgraded |
| DeactivateOptionalDomainPack | DomainPackDeactivated |
| ActivateExperimentalProfile | ExperimentalProfileActivated |
| DeactivateExperimentalProfile | ExperimentalProfileDeactivated |
| PinPolicyVersion | PolicyVersionPinned |
| ChangePolicyPin | PolicyVersionPinChanged |

These commands must obey SEMANTIC-EVOLUTION.md and DOMAIN-PACK-CONTRACT.md.

*Persisted (frozen contract v1.0.0):* none of these events exists in v1.0.0; `domainPacks` is always `[]`. Implementing them requires contract v1.1.0 (COMPATIBILITY-DECISIONS U-03).

## 24. Review commands

| Command | Event(s) |
| --- | --- |
| RequestReview | ReviewRequested |
| ApproveReview | ReviewApproved |
| RejectReview | ReviewRejected |
| WithdrawReview | ReviewWithdrawn |
| ExpireReview | ReviewExpired |

A review decision never substitutes for domain evidence unless domain policy explicitly defines it as a governance prerequisite rather than epistemic evidence.

## 25. Report commands

Issued reports are outputs tied to a source snapshot.

| Command | Event(s) |
| --- | --- |
| IssueInterimReport | ReportIssued |
| IssueFinalReport | ReportIssued |
| SupersedeReport | ReportSuperseded |

ReportIssued records source InvestigationId/AggregateVersion and rendering/template versions.

## 26. Import/recovery commands

These may be service/application-layer orchestration commands rather than aggregate commands, but semantic effects require explicit events.

| Command | Event(s) |
| --- | --- |
| ImportInvestigation | InvestigationImported / normal reconstructed history per import contract |
| ForkInvestigation | InvestigationForked |
| RecordRecovery | InvestigationRecoveryRecorded |
| RecordRedaction | RedactionRecorded |

Import must not fabricate historical commands/events absent from the source package.

## 27. Event categories

*Domain vocabulary; persisted names per §0 and EVENT-CATALOG.md.*

Core event categories:

### Identity/lifecycle
- InvestigationStarted
- InvestigationActivated
- InvestigationResolved
- InvestigationClosed
- InvestigationReopened
- InvestigationArchived
- InvestigationRenamed

### Epistemic records
- ObservationRecorded
- ClaimRecorded
- InterpretationRecorded
- HypothesisRecorded
- AssumptionRecorded
- EvidenceAdmitted
- PredictionRecorded
- FindingRecorded
- UnknownIdentified
- NegativeKnowledgeEstablished
- ContradictionRecorded

### Relationships
- EvidenceRelationshipRecorded
- EvidenceDependencyAdded
- AssumptionDependencyAdded
- InvestigationRelationshipAdded
- CausalMemberAdded

### Work/governance
- ObligationAccepted
- ReviewRequested
- ReviewApproved
- InvestigationActionRecorded
- CorrectiveActionRecorded
- VerificationCriterionRecorded

### State transitions
- HypothesisMarkedPlausible
- HypothesisMarkedSupported
- HypothesisMarkedWeakened
- HypothesisFalsified
- HypothesisReopened
- CauseConfirmed
- UnknownResolved
- ContradictionResolved
- CorrectiveActionImplemented
- CorrectiveActionVerifiedEffective

### Semantic/versioning
- DomainPackActivated
- DomainPackUpgraded
- ExperimentalProfileActivated
- PolicyVersionPinned
- ExtensionAttached
- ExtensionMigrationRecorded

### Output/audit
- ConclusionRecorded
- ConclusionReassessed
- ReportIssued
- InvestigationRecoveryRecorded
- RedactionRecorded

~~The exact serialization EventType is a SemanticId, allowing future event types without growing a closed core wire enumeration.~~ ***Superseded by frozen contract v1.0.0:** the serialized `eventType` is one of the 31 closed v1.0.0 discriminators (CONTRACT §7); an unregistered type invalidates the whole history (`CAUSA.UNKNOWN_EVENT_TYPE`). New event types require a new minor contract version (CONTRACT §13.1). The rationale (future event types without rewriting history) is preserved by that versioning rule.*

## 28. Event correction rule

Immutable events are not edited.

If an event's content was wrong:

- append a corrective/superseding event
- identify the affected event/entity
- explain correction provenance
- rebuild current projection

If persisted bytes are malformed/corrupt, use recovery procedures rather than pretending it was an ordinary semantic correction.

## 29. Derived state

Initially derive rather than persist:

- Leading hypothesis
- claim epistemic posture where feasible
- evidence counts
- blocker counts
- recommended next action
- conclusion dashboard summary
- portfolio/search state
- UI expanded/collapsed state

This protects the model from research changes.

## 30. No generic setters

The following commands are prohibited patterns:

- SetInvestigationState
- SetHypothesisState
- SetClaimConfidence
- SetRootCause
- SetEvidenceScore
- SetEverythingFromAI
- ReplaceInvestigationJson

Specific semantic commands preserve reason, prerequisites, and auditability.

## 31. First implementation minimum command set

For the earliest durable slice, implement first:

1. StartInvestigation
2. ActivateInvestigation
3. RecordObservation
4. RecordHypothesis
5. MarkHypothesisPlausible
6. MarkHypothesisSupported
7. FalsifyHypothesis
8. ReopenHypothesis
9. AdmitEvidence
10. RelateEvidence
11. IdentifyUnknown
12. RecordAssumption
13. RecordContradiction
14. RecordPrediction
15. RecordInvestigationAction
16. RecordFinding
17. EstablishNegativeKnowledge
18. RecordCausalAssertion
19. ConfirmCause
20. RecordCorrectiveAction
21. RecordVerificationCriterion
22. MarkCorrectiveActionImplemented
23. RecordVerification
24. MarkCorrectiveActionVerified *(v1.0.0: no separate command; `verifiedEffective` follows a satisfied VerificationRecorded when all criteria are satisfied)*
25. RecordConclusion
26. ResolveInvestigation
27. ReopenInvestigation
28. RecordProposal
29. AcceptProposal
30. RejectProposal
31. AttachExtension *(deferred: not in the v1.0.0 engine or registry; requires contract v1.1.0)*
32. PinPolicyVersion *(deferred: as above)*

Everything else can be added through later slices without invalidating these semantics.
