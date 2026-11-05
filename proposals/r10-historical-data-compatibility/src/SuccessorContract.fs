namespace Causa.HistoricalCompatibility.Successor

open Causa.HistoricalCompatibility

/// PROPOSED, not frozen. The smallest extension of the frozen v4.1.4 `CoreEvent` union
/// (requirements-compiled/v4.1.4/contracts/00-DOMAIN-STUBS.fs:92-120) that represents every causa-storage v1.0.0
/// event without loss. Every frozen case name is kept; six cases gain fields; 22 cases are added. See
/// SUCCESSOR-CONTRACT.md. Payload records reuse this adapter's wire records only so the proposal compiles and can be
/// tested; in a successor package they would be the domain owner's records with the same fields.
type HypothesisTransition =
    { HypothesisId: Uuid
      From: HypothesisState
      Reason: Text
      EvidenceIds: Uuid list }

type CoreEventV415 =
    | InvestigationStarted of InvestigationStartedP
    | InvestigationActivated of from: Lifecycle
    | InvestigationMarkedAwaitingEvidence of from: Lifecycle * reason: Text
    | InvestigationMarkedAwaitingVerification of from: Lifecycle
    | InvestigationResolved of from: Lifecycle
    | InvestigationClosed of from: Lifecycle
    | InvestigationReopened of from: Lifecycle * reason: Text
    | InvestigationVerificationFailed of reason: Text
    | ObservationRecorded of ObservationRecordedP
    | HypothesisRecorded of HypothesisRecordedP
    | HypothesisMarkedPlausible of HypothesisTransition
    | HypothesisMarkedSupported of HypothesisTransition
    | HypothesisWeakened of HypothesisTransition
    | HypothesisFalsified of HypothesisTransition
    | HypothesisSuperseded of HypothesisTransition
    | HypothesisReopened of HypothesisTransition
    | EvidenceAdmitted of EvidenceAdmittedP
    | EvidenceSuperseded of EvidenceSupersededP
    | EvidenceVerificationAssessed of EvidenceVerificationAssessedP
    | EvidenceRelationshipRecorded of EvidenceRelatedP
    | UnknownIdentified of UnknownIdentifiedP
    | UnknownTransitioned of UnknownTransitionedP
    | AssumptionRecorded of AssumptionRecordedP
    | AssumptionTransitioned of AssumptionTransitionedP
    | ContradictionRecorded of ContradictionRecordedP
    | ContradictionTransitioned of ContradictionTransitionedP
    | PredictionRecorded of PredictionRecordedP
    | InvestigationActionRecorded of ActionRecordedP
    | InvestigationActionTransitioned of ActionTransitionedP
    | FindingRecorded of FindingRecordedP
    | NegativeKnowledgeEstablished of NegativeKnowledgeEstablishedP
    | CausalAssertionRecorded of CausalAssertionRecordedP
    | CauseConfirmed of CausalAssertionConfirmedP
    | ConfirmedCauseReopened of CausalAssertionReopenedP
    | CorrectiveActionRecorded of CorrectiveActionRecordedP
    | CorrectiveActionApproved of correctiveActionId: Uuid
    | CorrectiveActionStarted of correctiveActionId: Uuid
    | CorrectiveActionImplemented of correctiveActionId: Uuid
    | CorrectiveActionAwaitingVerificationOfEffect of correctiveActionId: Uuid
    | CorrectiveActionVerifiedEffective of correctiveActionId: Uuid
    | CorrectiveActionVerificationFailed of correctiveActionId: Uuid
    | CorrectiveActionReworkRequired of correctiveActionId: Uuid
    | CorrectiveActionCancelled of correctiveActionId: Uuid
    | VerificationCriterionRecorded of CriterionRecordedP
    | VerificationRecorded of VerificationRecordedP
    | ConclusionRecorded of ConclusionRecordedP
    | ProposalRecorded of ProposalRecordedP
    | ProposalAccepted of ProposalWasAcceptedP
    | ProposalRejected of ProposalWasRejectedP
    /// Kept from the frozen union. causa-storage v1.0.0 has no extension namespace, so no v1 event produces it and it
    /// has no v1 wire form.
    | ExtensionAttached of payloadJson: string

/// The envelope fields the frozen `EventEnvelope` lacks (00-DOMAIN-STUBS.fs:126): provenance and command grouping.
type EventProvenance =
    { OccurredAt: Timestamp
      Actor: Actor
      CorrelationId: Uuid
      FormatVersion: string
      SemanticVersion: string }

type EventEnvelopeV415 =
    { EventId: Uuid
      Investigation: Uuid
      Version: int64
      Provenance: EventProvenance
      Payload: CoreEventV415 }

module SuccessorContract =
    let private ht (p: HypothesisTransitionedP) = { HypothesisId = p.HypothesisId; From = p.From; Reason = p.Reason; EvidenceIds = p.EvidenceIds }

    let private reasonOf (p: LifecycleChangedP) : Result<Text, string> =
        p.Reason |> Option.map Ok |> Option.defaultValue (Error "LifecycleChanged command requires a reason")

    /// Total over every decoded wire event.
    let ofWire (e: WireEvent) : Result<CoreEventV415, string> =
        match e with
        | WireEvent.InvestigationStarted p -> Ok(InvestigationStarted p)
        | WireEvent.LifecycleChanged p ->
            match p.Command with
            | ActivateInvestigation -> Ok(InvestigationActivated p.From)
            | MarkAwaitingEvidence -> reasonOf p |> Result.map (fun r -> InvestigationMarkedAwaitingEvidence(p.From, r))
            | MarkAwaitingVerification -> Ok(InvestigationMarkedAwaitingVerification p.From)
            | ResolveInvestigation -> Ok(InvestigationResolved p.From)
            | CloseInvestigation -> Ok(InvestigationClosed p.From)
            | ReopenInvestigation -> reasonOf p |> Result.map (fun r -> InvestigationReopened(p.From, r))
            | LifecycleVerificationFailed -> reasonOf p |> Result.map InvestigationVerificationFailed
        | WireEvent.ObservationRecorded p -> Ok(ObservationRecorded p)
        | WireEvent.HypothesisRecorded p -> Ok(HypothesisRecorded p)
        | WireEvent.HypothesisTransitioned p ->
            match p.Command with
            | MarkPlausible -> Ok(HypothesisMarkedPlausible(ht p))
            | MarkSupported -> Ok(HypothesisMarkedSupported(ht p))
            | MarkWeakened -> Ok(HypothesisWeakened(ht p))
            | Falsify -> Ok(HypothesisFalsified(ht p))
            | Supersede -> Ok(HypothesisSuperseded(ht p))
            | Reopen -> Ok(HypothesisReopened(ht p))
        | WireEvent.EvidenceAdmitted p -> Ok(EvidenceAdmitted p)
        | WireEvent.EvidenceSuperseded p -> Ok(EvidenceSuperseded p)
        | WireEvent.EvidenceVerificationAssessed p -> Ok(EvidenceVerificationAssessed p)
        | WireEvent.EvidenceRelated p -> Ok(EvidenceRelationshipRecorded p)
        | WireEvent.UnknownIdentified p -> Ok(UnknownIdentified p)
        | WireEvent.UnknownTransitioned p -> Ok(UnknownTransitioned p)
        | WireEvent.AssumptionRecorded p -> Ok(AssumptionRecorded p)
        | WireEvent.AssumptionTransitioned p -> Ok(AssumptionTransitioned p)
        | WireEvent.ContradictionRecorded p -> Ok(ContradictionRecorded p)
        | WireEvent.ContradictionTransitioned p -> Ok(ContradictionTransitioned p)
        | WireEvent.PredictionRecorded p -> Ok(PredictionRecorded p)
        | WireEvent.ActionRecorded p -> Ok(InvestigationActionRecorded p)
        | WireEvent.ActionTransitioned p -> Ok(InvestigationActionTransitioned p)
        | WireEvent.FindingRecorded p -> Ok(FindingRecorded p)
        | WireEvent.NegativeKnowledgeEstablished p -> Ok(NegativeKnowledgeEstablished p)
        | WireEvent.CausalAssertionRecorded p -> Ok(CausalAssertionRecorded p)
        | WireEvent.CausalAssertionConfirmed p -> Ok(CauseConfirmed p)
        | WireEvent.CausalAssertionReopened p -> Ok(ConfirmedCauseReopened p)
        | WireEvent.CorrectiveActionRecorded p -> Ok(CorrectiveActionRecorded p)
        | WireEvent.CorrectiveActionTransitioned p ->
            let id = p.CorrectiveActionId
            match p.State with
            | CaApproved -> Ok(CorrectiveActionApproved id)
            | CaInProgress -> Ok(CorrectiveActionStarted id)
            | CaImplemented -> Ok(CorrectiveActionImplemented id)
            | CaAwaitingVerificationOfEffect -> Ok(CorrectiveActionAwaitingVerificationOfEffect id)
            | CaVerifiedEffective -> Ok(CorrectiveActionVerifiedEffective id)
            | CaVerificationFailed -> Ok(CorrectiveActionVerificationFailed id)
            | CaReworkRequired -> Ok(CorrectiveActionReworkRequired id)
            | CaCancelled -> Ok(CorrectiveActionCancelled id)
            | CaProposed -> Error "'proposed' is not a correctiveActionTarget"
        | WireEvent.CriterionRecorded p -> Ok(VerificationCriterionRecorded p)
        | WireEvent.VerificationRecorded p -> Ok(VerificationRecorded p)
        | WireEvent.ConclusionRecorded p -> Ok(ConclusionRecorded p)
        | WireEvent.ProposalRecorded p -> Ok(ProposalRecorded p)
        | WireEvent.ProposalWasAccepted p -> Ok(ProposalAccepted p)
        | WireEvent.ProposalWasRejected p -> Ok(ProposalRejected p)

    let private wireHt command target (t: HypothesisTransition) =
        WireEvent.HypothesisTransitioned { HypothesisId = t.HypothesisId; From = t.From; To = target; Command = command; Reason = t.Reason; EvidenceIds = t.EvidenceIds }

    let private wireLc command target from reason =
        WireEvent.LifecycleChanged { From = from; To = target; Command = command; Reason = reason }

    let private wireCa id state = WireEvent.CorrectiveActionTransitioned { CorrectiveActionId = id; State = state }

    /// The inverse; `Error` only for ExtensionAttached, which has no v1 wire form.
    let toWire (e: CoreEventV415) : Result<WireEvent, string> =
        match e with
        | InvestigationStarted p -> Ok(WireEvent.InvestigationStarted p)
        | InvestigationActivated f -> Ok(wireLc ActivateInvestigation Active f None)
        | InvestigationMarkedAwaitingEvidence(f, r) -> Ok(wireLc MarkAwaitingEvidence AwaitingEvidence f (Some r))
        | InvestigationMarkedAwaitingVerification f -> Ok(wireLc MarkAwaitingVerification AwaitingVerification f None)
        | InvestigationResolved f -> Ok(wireLc ResolveInvestigation Resolved f None)
        | InvestigationClosed f -> Ok(wireLc CloseInvestigation Closed f None)
        | InvestigationReopened(f, r) -> Ok(wireLc ReopenInvestigation Active f (Some r))
        | InvestigationVerificationFailed r -> Ok(wireLc LifecycleVerificationFailed Active AwaitingVerification (Some r))
        | ObservationRecorded p -> Ok(WireEvent.ObservationRecorded p)
        | HypothesisRecorded p -> Ok(WireEvent.HypothesisRecorded p)
        | HypothesisMarkedPlausible t -> Ok(wireHt MarkPlausible HPlausible t)
        | HypothesisMarkedSupported t -> Ok(wireHt MarkSupported HSupported t)
        | HypothesisWeakened t -> Ok(wireHt MarkWeakened HWeakened t)
        | HypothesisFalsified t -> Ok(wireHt Falsify HFalsified t)
        | HypothesisSuperseded t -> Ok(wireHt Supersede HSuperseded t)
        | HypothesisReopened t -> Ok(wireHt Reopen HPlausible t)
        | EvidenceAdmitted p -> Ok(WireEvent.EvidenceAdmitted p)
        | EvidenceSuperseded p -> Ok(WireEvent.EvidenceSuperseded p)
        | EvidenceVerificationAssessed p -> Ok(WireEvent.EvidenceVerificationAssessed p)
        | EvidenceRelationshipRecorded p -> Ok(WireEvent.EvidenceRelated p)
        | UnknownIdentified p -> Ok(WireEvent.UnknownIdentified p)
        | UnknownTransitioned p -> Ok(WireEvent.UnknownTransitioned p)
        | AssumptionRecorded p -> Ok(WireEvent.AssumptionRecorded p)
        | AssumptionTransitioned p -> Ok(WireEvent.AssumptionTransitioned p)
        | ContradictionRecorded p -> Ok(WireEvent.ContradictionRecorded p)
        | ContradictionTransitioned p -> Ok(WireEvent.ContradictionTransitioned p)
        | PredictionRecorded p -> Ok(WireEvent.PredictionRecorded p)
        | InvestigationActionRecorded p -> Ok(WireEvent.ActionRecorded p)
        | InvestigationActionTransitioned p -> Ok(WireEvent.ActionTransitioned p)
        | FindingRecorded p -> Ok(WireEvent.FindingRecorded p)
        | NegativeKnowledgeEstablished p -> Ok(WireEvent.NegativeKnowledgeEstablished p)
        | CausalAssertionRecorded p -> Ok(WireEvent.CausalAssertionRecorded p)
        | CauseConfirmed p -> Ok(WireEvent.CausalAssertionConfirmed p)
        | ConfirmedCauseReopened p -> Ok(WireEvent.CausalAssertionReopened p)
        | CorrectiveActionRecorded p -> Ok(WireEvent.CorrectiveActionRecorded p)
        | CorrectiveActionApproved id -> Ok(wireCa id CaApproved)
        | CorrectiveActionStarted id -> Ok(wireCa id CaInProgress)
        | CorrectiveActionImplemented id -> Ok(wireCa id CaImplemented)
        | CorrectiveActionAwaitingVerificationOfEffect id -> Ok(wireCa id CaAwaitingVerificationOfEffect)
        | CorrectiveActionVerifiedEffective id -> Ok(wireCa id CaVerifiedEffective)
        | CorrectiveActionVerificationFailed id -> Ok(wireCa id CaVerificationFailed)
        | CorrectiveActionReworkRequired id -> Ok(wireCa id CaReworkRequired)
        | CorrectiveActionCancelled id -> Ok(wireCa id CaCancelled)
        | VerificationCriterionRecorded p -> Ok(WireEvent.CriterionRecorded p)
        | VerificationRecorded p -> Ok(WireEvent.VerificationRecorded p)
        | ConclusionRecorded p -> Ok(WireEvent.ConclusionRecorded p)
        | ProposalRecorded p -> Ok(WireEvent.ProposalRecorded p)
        | ProposalAccepted p -> Ok(WireEvent.ProposalWasAccepted p)
        | ProposalRejected p -> Ok(WireEvent.ProposalWasRejected p)
        | ExtensionAttached _ -> Error "ExtensionAttached has no causa-storage v1.0.0 wire form (no extension namespace, CONTRACT §13.5)"

    let envelope (d: DecodedEvent) : Result<EventEnvelopeV415, string> =
        let e = d.Envelope
        ofWire d.Event
        |> Result.map (fun p ->
        { EventId = e.EventId
          Investigation = e.InvestigationId
          Version = e.AggregateVersion
          Provenance = { OccurredAt = e.OccurredAt; Actor = e.Actor; CorrelationId = e.CorrelationId; FormatVersion = e.FormatVersion; SemanticVersion = e.SemanticVersion }
          Payload = p })

    /// Case name, for the compatibility matrix (reflection-free).
    let caseName (e: CoreEventV415) : string =
        match e with
        | InvestigationStarted _ -> "InvestigationStarted"
        | InvestigationActivated _ -> "InvestigationActivated"
        | InvestigationMarkedAwaitingEvidence _ -> "InvestigationMarkedAwaitingEvidence"
        | InvestigationMarkedAwaitingVerification _ -> "InvestigationMarkedAwaitingVerification"
        | InvestigationResolved _ -> "InvestigationResolved"
        | InvestigationClosed _ -> "InvestigationClosed"
        | InvestigationReopened _ -> "InvestigationReopened"
        | InvestigationVerificationFailed _ -> "InvestigationVerificationFailed"
        | ObservationRecorded _ -> "ObservationRecorded"
        | HypothesisRecorded _ -> "HypothesisRecorded"
        | HypothesisMarkedPlausible _ -> "HypothesisMarkedPlausible"
        | HypothesisMarkedSupported _ -> "HypothesisMarkedSupported"
        | HypothesisWeakened _ -> "HypothesisWeakened"
        | HypothesisFalsified _ -> "HypothesisFalsified"
        | HypothesisSuperseded _ -> "HypothesisSuperseded"
        | HypothesisReopened _ -> "HypothesisReopened"
        | EvidenceAdmitted _ -> "EvidenceAdmitted"
        | EvidenceSuperseded _ -> "EvidenceSuperseded"
        | EvidenceVerificationAssessed _ -> "EvidenceVerificationAssessed"
        | EvidenceRelationshipRecorded _ -> "EvidenceRelationshipRecorded"
        | UnknownIdentified _ -> "UnknownIdentified"
        | UnknownTransitioned _ -> "UnknownTransitioned"
        | AssumptionRecorded _ -> "AssumptionRecorded"
        | AssumptionTransitioned _ -> "AssumptionTransitioned"
        | ContradictionRecorded _ -> "ContradictionRecorded"
        | ContradictionTransitioned _ -> "ContradictionTransitioned"
        | PredictionRecorded _ -> "PredictionRecorded"
        | InvestigationActionRecorded _ -> "InvestigationActionRecorded"
        | InvestigationActionTransitioned _ -> "InvestigationActionTransitioned"
        | FindingRecorded _ -> "FindingRecorded"
        | NegativeKnowledgeEstablished _ -> "NegativeKnowledgeEstablished"
        | CausalAssertionRecorded _ -> "CausalAssertionRecorded"
        | CauseConfirmed _ -> "CauseConfirmed"
        | ConfirmedCauseReopened _ -> "ConfirmedCauseReopened"
        | CorrectiveActionRecorded _ -> "CorrectiveActionRecorded"
        | CorrectiveActionApproved _ -> "CorrectiveActionApproved"
        | CorrectiveActionStarted _ -> "CorrectiveActionStarted"
        | CorrectiveActionImplemented _ -> "CorrectiveActionImplemented"
        | CorrectiveActionAwaitingVerificationOfEffect _ -> "CorrectiveActionAwaitingVerificationOfEffect"
        | CorrectiveActionVerifiedEffective _ -> "CorrectiveActionVerifiedEffective"
        | CorrectiveActionVerificationFailed _ -> "CorrectiveActionVerificationFailed"
        | CorrectiveActionReworkRequired _ -> "CorrectiveActionReworkRequired"
        | CorrectiveActionCancelled _ -> "CorrectiveActionCancelled"
        | VerificationCriterionRecorded _ -> "VerificationCriterionRecorded"
        | VerificationRecorded _ -> "VerificationRecorded"
        | ConclusionRecorded _ -> "ConclusionRecorded"
        | ProposalRecorded _ -> "ProposalRecorded"
        | ProposalAccepted _ -> "ProposalAccepted"
        | ProposalRejected _ -> "ProposalRejected"
        | ExtensionAttached _ -> "ExtensionAttached"

    /// Frozen v4.1.4 case names (00-DOMAIN-STUBS.fs:92-120), for the "every frozen name is kept" check.
    let frozenCaseNames =
        [ "InvestigationStarted"; "InvestigationActivated"; "ObservationRecorded"; "HypothesisRecorded"; "HypothesisMarkedPlausible"
          "HypothesisMarkedSupported"; "HypothesisFalsified"; "HypothesisReopened"; "EvidenceAdmitted"; "EvidenceRelationshipRecorded"
          "UnknownIdentified"; "AssumptionRecorded"; "ContradictionRecorded"; "PredictionRecorded"; "InvestigationActionRecorded"
          "FindingRecorded"; "CausalAssertionRecorded"; "CauseConfirmed"; "ConfirmedCauseReopened"; "CorrectiveActionRecorded"
          "VerificationCriterionRecorded"; "CorrectiveActionImplemented"; "VerificationRecorded"; "InvestigationResolved"
          "InvestigationReopened"; "ProposalRecorded"; "ProposalRejected"; "ExtensionAttached" ]

    /// Frozen cases whose arguments the successor changes.
    let widenedFrozenCases =
        [ "InvestigationActivated"; "InvestigationResolved"; "InvestigationReopened"; "HypothesisMarkedPlausible"; "HypothesisMarkedSupported"
          "HypothesisFalsified"; "HypothesisReopened"; "CauseConfirmed"; "ProposalRejected" ]
