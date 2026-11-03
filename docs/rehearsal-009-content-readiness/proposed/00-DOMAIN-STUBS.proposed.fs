namespace Causa.Domain

open System

/// Abbreviated from docs/FSHARP-TYPE-SHAPES.md (sections 2, 30-38) for the contract typecheck only. The domain owner
/// implements the full shapes; these stubs fix only the names the boundaries use.
type InvestigationId = InvestigationId of Guid
type EventId = EventId of Guid
type AggregateVersion = AggregateVersion of int64
type Investigation = { Id: InvestigationId; Version: AggregateVersion }
/// v4.1.2 (ODR-V412-02): the command and event bodies of FSHARP-TYPE-SHAPES sections 31 and 33. The case names are
/// frozen here so every consumer can tell commands and events apart; each payload type is the domain owner's to
/// define (opaque in this stub, as the other stubs abbreviate their shapes).
type SemanticId = class end
type ExtensionEnvelope = class end
type FalsifyHypothesis = class end
type ReopenHypothesis = class end
type IdentifyUnknown = class end
type RecordAssumption = class end
type RecordContradiction = class end
type RecordPrediction = class end
type RecordInvestigationAction = class end
type RecordFinding = class end
type EstablishNegativeKnowledge = class end
type RecordCausalAssertion = class end
type ConfirmCause = class end
type RecordCorrectiveAction = class end
type RecordVerificationCriterion = class end
type RecordVerification = class end
type ReopenInvestigation = class end
type RecordProposal = class end
type Scope = class end
type Unknown = class end
type Assumption = class end
type Contradiction = class end
type Prediction = class end
type InvestigationAction = class end
type Finding = class end
type CausalAssertion = class end
type CorrectiveAction = class end
type VerificationCriterion = class end
type Verification = class end
type Proposal = class end
type HypothesisId = HypothesisId of Guid
/// PROPOSED (Rehearsal 009 content readiness, ODR-V413-02; NOT authoritative until the owner approves a successor
/// package). The content payloads FSHARP-TYPE-SHAPES sections 3, 5, 19-21 and 27 describe as indicative shapes, made
/// concrete for the competition release: every user-entered field the INVESTIGATION-CONTENT-E2E scenario needs.
type NonEmptyText = private NonEmptyText of string
type ObservationId = ObservationId of Guid
type EvidenceId = EvidenceId of Guid
type EvidenceRelationshipId = EvidenceRelationshipId of Guid
type ConclusionId = ConclusionId of Guid
/// FSHARP-TYPE-SHAPES section 5, reduced: who recorded the content, when, and from what source.
type Provenance = { ActorId: string; ActorKind: string; RecordedAt: DateTimeOffset; SourceRef: string option }
/// An absolute http(s) URL, validated by the domain owner's smart constructor.
type SourceUrl = private SourceUrl of string
type InvestigationDetails = { Title: NonEmptyText; Description: string option; PrimaryQuestion: string option }
type StartInvestigation = { Details: InvestigationDetails; Provenance: Provenance }
type ReviseInvestigationDetails = { Details: InvestigationDetails; Provenance: Provenance }
type RecordObservation = { Id: ObservationId; Text: NonEmptyText; Notes: string option; SourceRef: string option; Provenance: Provenance }
type ReviseObservation = { Id: ObservationId; Text: NonEmptyText; Notes: string option; SourceRef: string option; Provenance: Provenance }
type RecordHypothesis = { Id: HypothesisId; Statement: NonEmptyText; Explanation: string option; Provenance: Provenance }
type ReviseHypothesis = { Id: HypothesisId; Statement: NonEmptyText; Explanation: string option; Provenance: Provenance }
type EvidenceContent = { Label: NonEmptyText; Description: NonEmptyText; SourceRef: string option; SourceUrl: SourceUrl option; ObservedAt: DateTimeOffset option }
type AdmitEvidence = { Id: EvidenceId; Content: EvidenceContent; Provenance: Provenance }
type ReviseEvidence = { Id: EvidenceId; Content: EvidenceContent; Provenance: Provenance }
/// FSHARP-TYPE-SHAPES section 15, restricted to the three kinds the competition release exposes.
type RelationshipKind =
    | Supports
    | Contradicts
    | Irrelevant
type RelateEvidence = { Id: EvidenceRelationshipId; Evidence: EvidenceId; Hypothesis: HypothesisId; Kind: RelationshipKind; Rationale: string option; Provenance: Provenance }
type RecordConclusion = { Id: ConclusionId; Statement: NonEmptyText; Evidence: EvidenceId list; Provenance: Provenance }
type InvestigationStarted = StartInvestigation
type Observation = RecordObservation
type Hypothesis = RecordHypothesis
type Evidence = AdmitEvidence
type EvidenceRelationship = RelateEvidence
type Conclusion = RecordConclusion
type CorrectiveActionId = CorrectiveActionId of Guid
type ProposalId = ProposalId of Guid
type CausalAssertionId = CausalAssertionId of Guid
type CoreCommand =
    | StartInvestigation of StartInvestigation
    | ReviseInvestigationDetails of ReviseInvestigationDetails
    | ActivateInvestigation
    | RecordObservation of RecordObservation
    | ReviseObservation of ReviseObservation
    | RecordHypothesis of RecordHypothesis
    | ReviseHypothesis of ReviseHypothesis
    | MarkHypothesisPlausible of HypothesisId
    | MarkHypothesisSupported of HypothesisId
    | FalsifyHypothesis of FalsifyHypothesis
    | ReopenHypothesis of ReopenHypothesis
    | AdmitEvidence of AdmitEvidence
    | ReviseEvidence of ReviseEvidence
    | RelateEvidence of RelateEvidence
    | RecordConclusion of RecordConclusion
    | IdentifyUnknown of IdentifyUnknown
    | RecordAssumption of RecordAssumption
    | RecordContradiction of RecordContradiction
    | RecordPrediction of RecordPrediction
    | RecordInvestigationAction of RecordInvestigationAction
    | RecordFinding of RecordFinding
    | EstablishNegativeKnowledge of EstablishNegativeKnowledge
    | RecordCausalAssertion of RecordCausalAssertion
    | ConfirmCause of ConfirmCause
    | RecordCorrectiveAction of RecordCorrectiveAction
    | RecordVerificationCriterion of RecordVerificationCriterion
    | MarkCorrectiveActionImplemented of CorrectiveActionId
    | RecordVerification of RecordVerification
    | ResolveInvestigation
    | ReopenInvestigation of ReopenInvestigation
    | RecordProposal of RecordProposal
    | AcceptProposal of ProposalId
    | RejectProposal of ProposalId
    | AttachExtension of ExtensionEnvelope
type Command =
    | Core of CoreCommand
    | Extension of SemanticId * payloadJson: string
type CoreEvent =
    | InvestigationStarted of InvestigationStarted
    | InvestigationDetailsRevised of ReviseInvestigationDetails
    | InvestigationActivated
    | ObservationRecorded of Observation
    | ObservationRevised of ReviseObservation
    | HypothesisRecorded of Hypothesis
    | HypothesisRevised of ReviseHypothesis
    | HypothesisMarkedPlausible of HypothesisId
    | HypothesisMarkedSupported of HypothesisId
    | HypothesisFalsified of HypothesisId * reason: NonEmptyText * Scope
    | HypothesisReopened of HypothesisId * reason: NonEmptyText
    | EvidenceAdmitted of Evidence
    | EvidenceRevised of ReviseEvidence
    | EvidenceRelationshipRecorded of EvidenceRelationship
    | ConclusionRecorded of Conclusion
    | UnknownIdentified of Unknown
    | AssumptionRecorded of Assumption
    | ContradictionRecorded of Contradiction
    | PredictionRecorded of Prediction
    | InvestigationActionRecorded of InvestigationAction
    | FindingRecorded of Finding
    | CausalAssertionRecorded of CausalAssertion
    | CauseConfirmed of CausalAssertionId
    | ConfirmedCauseReopened of CausalAssertionId * reason: NonEmptyText
    | CorrectiveActionRecorded of CorrectiveAction
    | VerificationCriterionRecorded of VerificationCriterion
    | CorrectiveActionImplemented of CorrectiveActionId
    | VerificationRecorded of Verification
    | InvestigationResolved
    | InvestigationReopened of NonEmptyText
    | ProposalRecorded of Proposal
    | ProposalRejected of ProposalId
    | ExtensionAttached of ExtensionEnvelope
type CommandEnvelope = { CommandId: Guid; Investigation: InvestigationId; ExpectedVersion: AggregateVersion option; Command: Command }
type EventPayload =
    | CoreEvent of CoreEvent
    | ExtensionEvent of SemanticId * payloadJson: string
type EventEnvelope = { EventId: EventId; Investigation: InvestigationId; Version: AggregateVersion; Payload: EventPayload }
type Blocker = { Code: string; Message: string }
type Decision =
    | Allowed of EventPayload list
    | Blocked of Blocker list
    | RequiresReview of string list
    | UnsupportedSemantics of string list
    | Conflict of expected: AggregateVersion * actual: AggregateVersion
    | Duplicate of EventId list
type PolicySet = { Id: string }
type ActorContext = { ActorId: string }
type ProjectionError = { At: AggregateVersion; Reason: string }
