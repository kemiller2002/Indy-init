namespace Causa.HistoricalCompatibility

/// How one decoded wire event relates to the frozen v4.1.4 `CoreEvent` union
/// (Indy-init requirements-compiled/v4.1.4/contracts/00-DOMAIN-STUBS.fs:92-120).
type FrozenRepresentation =
    /// A frozen case exists and its declared arguments do not discard any wire field. Payload records that the stub
    /// leaves opaque (`class end`) are the domain owner's to define, so field fidelity there is not established.
    | FrozenCase of caseName: string
    /// A frozen case exists but its fixed arguments cannot hold these wire fields.
    | FrozenCaseLossy of caseName: string * droppedFields: string list
    /// No frozen case can represent the event.
    | NoFrozenCase of reason: string

/// The proposed successor (SUCCESSOR-CONTRACT.md) representation.
type SuccessorRepresentation =
    | KeepCase of caseName: string
    | WidenCase of caseName: string * change: string
    | AddCase of caseName: string

type CompatibilityEntry =
    { Variant: string
      Frozen: FrozenRepresentation
      Successor: SuccessorRepresentation }

module EventCompatibility =
    /// Fields of the causa-storage envelope that the frozen `EventEnvelope = {EventId; Investigation; Version; Payload}`
    /// (00-DOMAIN-STUBS.fs:126) has no place for. They are lost for every event, whatever its case.
    let frozenEnvelopeDroppedFields = [ "occurredAt"; "actor"; "correlationId"; "formatVersion"; "semanticVersion" ]

    /// The mapping key: the discriminator, refined by the command or target state where the frozen union splits a
    /// single wire type into several cases.
    let variant (e: WireEvent) : string =
        match e with
        | LifecycleChanged p -> "LifecycleChanged/" + WireEnum.toWire Enums.lifecycleCommand p.Command
        | HypothesisTransitioned p -> "HypothesisTransitioned/" + WireEnum.toWire Enums.hypothesisCommand p.Command
        | CorrectiveActionTransitioned p -> "CorrectiveActionTransitioned/" + WireEnum.toWire Enums.correctiveActionState p.State
        | other -> EventDecoder.discriminator other

    let private noCase = NoFrozenCase

    /// Every variant the v1.0.0 registry can produce, with its frozen and successor representation.
    let table : CompatibilityEntry list =
        let e v f s = { Variant = v; Frozen = f; Successor = s }
        let lifecycle cmd f s = e ("LifecycleChanged/" + cmd) f s
        let hyp cmd f s = e ("HypothesisTransitioned/" + cmd) f s
        let ca st f s = e ("CorrectiveActionTransitioned/" + st) f s
        [ e "InvestigationStarted" (FrozenCase "InvestigationStarted") (KeepCase "InvestigationStarted")
          lifecycle "activateInvestigation" (FrozenCaseLossy("InvestigationActivated", [ "from" ])) (WidenCase("InvestigationActivated", "add from"))
          lifecycle "markAwaitingEvidence" (noCase "no awaiting-evidence case") (AddCase "InvestigationMarkedAwaitingEvidence")
          lifecycle "markAwaitingVerification" (noCase "no awaiting-verification case") (AddCase "InvestigationMarkedAwaitingVerification")
          lifecycle "resolveInvestigation" (FrozenCaseLossy("InvestigationResolved", [ "from" ])) (WidenCase("InvestigationResolved", "add from"))
          lifecycle "closeInvestigation" (noCase "no close case") (AddCase "InvestigationClosed")
          lifecycle "reopenInvestigation" (FrozenCaseLossy("InvestigationReopened", [ "from" ])) (WidenCase("InvestigationReopened", "add from"))
          lifecycle "verificationFailed" (noCase "no verification-failed lifecycle case") (AddCase "InvestigationVerificationFailed")
          e "ObservationRecorded" (FrozenCase "ObservationRecorded") (KeepCase "ObservationRecorded")
          e "HypothesisRecorded" (FrozenCase "HypothesisRecorded") (KeepCase "HypothesisRecorded")
          hyp "markPlausible" (FrozenCaseLossy("HypothesisMarkedPlausible", [ "from"; "reason"; "evidenceIds" ])) (WidenCase("HypothesisMarkedPlausible", "HypothesisTransition {from; reason; evidenceIds}"))
          hyp "markSupported" (FrozenCaseLossy("HypothesisMarkedSupported", [ "from"; "reason"; "evidenceIds" ])) (WidenCase("HypothesisMarkedSupported", "HypothesisTransition {from; reason; evidenceIds}"))
          hyp "markWeakened" (noCase "no weakened case") (AddCase "HypothesisWeakened")
          hyp "falsify" (FrozenCaseLossy("HypothesisFalsified", [ "from"; "evidenceIds" ])) (WidenCase("HypothesisFalsified", "HypothesisTransition {from; reason; evidenceIds}; the frozen Scope argument has no wire source and is removed"))
          hyp "supersede" (noCase "no superseded case") (AddCase "HypothesisSuperseded")
          hyp "reopen" (FrozenCaseLossy("HypothesisReopened", [ "from"; "evidenceIds" ])) (WidenCase("HypothesisReopened", "HypothesisTransition {from; reason; evidenceIds}"))
          e "EvidenceAdmitted" (FrozenCase "EvidenceAdmitted") (KeepCase "EvidenceAdmitted")
          e "EvidenceSuperseded" (noCase "no supersession case") (AddCase "EvidenceSuperseded")
          e "EvidenceVerificationAssessed" (noCase "no verification-assessment case") (AddCase "EvidenceVerificationAssessed")
          e "EvidenceRelated" (FrozenCase "EvidenceRelationshipRecorded") (KeepCase "EvidenceRelationshipRecorded")
          e "UnknownIdentified" (FrozenCase "UnknownIdentified") (KeepCase "UnknownIdentified")
          e "UnknownTransitioned" (noCase "no unknown transition case") (AddCase "UnknownTransitioned")
          e "AssumptionRecorded" (FrozenCase "AssumptionRecorded") (KeepCase "AssumptionRecorded")
          e "AssumptionTransitioned" (noCase "no assumption transition case") (AddCase "AssumptionTransitioned")
          e "ContradictionRecorded" (FrozenCase "ContradictionRecorded") (KeepCase "ContradictionRecorded")
          e "ContradictionTransitioned" (noCase "no contradiction transition case") (AddCase "ContradictionTransitioned")
          e "PredictionRecorded" (FrozenCase "PredictionRecorded") (KeepCase "PredictionRecorded")
          e "ActionRecorded" (FrozenCase "InvestigationActionRecorded") (KeepCase "InvestigationActionRecorded")
          e "ActionTransitioned" (noCase "no investigation-action transition case") (AddCase "InvestigationActionTransitioned")
          e "FindingRecorded" (FrozenCase "FindingRecorded") (KeepCase "FindingRecorded")
          e "NegativeKnowledgeEstablished" (noCase "only the EstablishNegativeKnowledge command exists; no event case") (AddCase "NegativeKnowledgeEstablished")
          e "CausalAssertionRecorded" (FrozenCase "CausalAssertionRecorded") (KeepCase "CausalAssertionRecorded")
          e "CausalAssertionConfirmed" (FrozenCaseLossy("CauseConfirmed", [ "policy" ])) (WidenCase("CauseConfirmed", "add the applied policy id"))
          e "CausalAssertionReopened" (FrozenCase "ConfirmedCauseReopened") (KeepCase "ConfirmedCauseReopened")
          e "CorrectiveActionRecorded" (FrozenCase "CorrectiveActionRecorded") (KeepCase "CorrectiveActionRecorded")
          ca "approved" (noCase "only CorrectiveActionImplemented exists") (AddCase "CorrectiveActionApproved")
          ca "cancelled" (noCase "only CorrectiveActionImplemented exists") (AddCase "CorrectiveActionCancelled")
          ca "inProgress" (noCase "only CorrectiveActionImplemented exists") (AddCase "CorrectiveActionStarted")
          ca "implemented" (FrozenCase "CorrectiveActionImplemented") (KeepCase "CorrectiveActionImplemented")
          ca "awaitingVerificationOfEffect" (noCase "only CorrectiveActionImplemented exists") (AddCase "CorrectiveActionAwaitingVerificationOfEffect")
          ca "verificationFailed" (noCase "only CorrectiveActionImplemented exists") (AddCase "CorrectiveActionVerificationFailed")
          ca "verifiedEffective" (noCase "only CorrectiveActionImplemented exists") (AddCase "CorrectiveActionVerifiedEffective")
          ca "reworkRequired" (noCase "only CorrectiveActionImplemented exists") (AddCase "CorrectiveActionReworkRequired")
          e "CriterionRecorded" (FrozenCase "VerificationCriterionRecorded") (KeepCase "VerificationCriterionRecorded")
          e "VerificationRecorded" (FrozenCase "VerificationRecorded") (KeepCase "VerificationRecorded")
          e "ConclusionRecorded" (noCase "a Conclusion record exists (FSHARP-TYPE-SHAPES §27) but no event case") (AddCase "ConclusionRecorded")
          e "ProposalRecorded" (FrozenCase "ProposalRecorded") (KeepCase "ProposalRecorded")
          e "ProposalWasAccepted" (noCase "only the AcceptProposal command exists; no event case") (AddCase "ProposalAccepted")
          e "ProposalWasRejected" (FrozenCaseLossy("ProposalRejected", [ "reason" ])) (WidenCase("ProposalRejected", "add the rejection reason")) ]

    let entry (e: WireEvent) : CompatibilityEntry =
        let v = variant e
        table |> List.find (fun x -> x.Variant = v)

    /// The event fits the frozen union with no field discarded by a case signature (envelope loss is separate).
    let isFrozenLossless (e: WireEvent) =
        match (entry e).Frozen with
        | FrozenCase _ -> true
        | _ -> false

    let frozenLabel =
        function
        | FrozenCase c -> "frozen-case:" + c
        | FrozenCaseLossy(c, _) -> "frozen-case-lossy:" + c
        | NoFrozenCase _ -> "no-frozen-case"

    let successorLabel =
        function
        | KeepCase c -> "keep:" + c
        | WidenCase(c, _) -> "widen:" + c
        | AddCase c -> "add:" + c
