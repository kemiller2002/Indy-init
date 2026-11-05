namespace Causa.HistoricalCompatibility

/// The inverse of EventDecoder. It exists to prove decoding is lossless: re-encoding every decoded event must give
/// back the stored canonical line byte for byte (tests: `lossless-round-trip`).
module EventEncoder =
    let str (s: string) = JStr s
    let uuid (u: Uuid) = JStr u.Text
    let text (t: Text) = JStr t.Value
    let optText (t: Text option) = t |> Option.map text |> Option.defaultValue JNull
    let ts (t: Timestamp) = JStr t.Text
    let optTs (t: Timestamp option) = t |> Option.map ts |> Option.defaultValue JNull
    let uuids (xs: Uuid list) = JArr(xs |> List.map uuid)
    let enum (e: WireEnum<'T>) (v: 'T) = JStr(WireEnum.toWire e v)

    let actor (a: Actor) =
        match a.Kind with
        | HumanActor -> JObj [ "actorId", text a.ActorId; "kind", str "human" ]
        | SystemActor -> JObj [ "actorId", text a.ActorId; "kind", str "system" ]
        | AgentActor(p, m) -> JObj [ "actorId", text a.ActorId; "kind", str "agent"; "provider", text p; "model", text m ]

    let causalMember (m: CausalMember) = JObj [ "hypothesisId", uuid m.HypothesisId; "role", enum Enums.causeRole m.Role ]

    let payload (e: WireEvent) : JValue =
        match e with
        | InvestigationStarted p -> JObj [ "title", text p.Title; "observationId", uuid p.ObservationId; "original", text p.Original ]
        | LifecycleChanged p ->
            JObj [ "from", enum Enums.lifecycle p.From; "to", enum Enums.lifecycle p.To; "command", enum Enums.lifecycleCommand p.Command; "reason", optText p.Reason ]
        | ObservationRecorded p -> JObj [ "observationId", uuid p.ObservationId; "original", text p.Original; "observedAt", optTs p.ObservedAt ]
        | HypothesisRecorded p -> JObj [ "hypothesisId", uuid p.HypothesisId; "statement", text p.Statement; "mechanism", optText p.Mechanism ]
        | HypothesisTransitioned p ->
            JObj
                [ "hypothesisId", uuid p.HypothesisId; "from", enum Enums.hypothesisState p.From; "to", enum Enums.hypothesisState p.To
                  "command", enum Enums.hypothesisCommand p.Command; "reason", text p.Reason; "evidenceIds", uuids p.EvidenceIds ]
        | EvidenceAdmitted p ->
            JObj
                [ "evidenceId", uuid p.EvidenceId; "contentKind", enum Enums.contentKind p.ContentKind; "content", text p.Content
                  "category", enum Enums.evidenceCategory p.Category; "source", text p.Source; "capturedAt", optTs p.CapturedAt ]
        | EvidenceSuperseded p -> JObj [ "evidenceId", uuid p.EvidenceId; "replacementEvidenceId", uuid p.ReplacementEvidenceId; "reason", text p.Reason ]
        | EvidenceVerificationAssessed p -> JObj [ "evidenceId", uuid p.EvidenceId; "verification", enum Enums.verificationStatus p.Verification ]
        | EvidenceRelated p ->
            JObj
                [ "relationshipId", uuid p.RelationshipId; "evidenceId", uuid p.EvidenceId; "hypothesisId", uuid p.HypothesisId
                  "kind", enum Enums.relationshipKind p.Kind; "rationale", optText p.Rationale ]
        | UnknownIdentified p -> JObj [ "unknownId", uuid p.UnknownId; "question", text p.Question; "materiality", enum Enums.materiality p.Materiality ]
        | UnknownTransitioned p -> JObj [ "unknownId", uuid p.UnknownId; "state", enum Enums.unknownState p.State; "basis", text p.Basis ]
        | AssumptionRecorded p -> JObj [ "assumptionId", uuid p.AssumptionId; "proposition", text p.Proposition; "materiality", enum Enums.materiality p.Materiality ]
        | AssumptionTransitioned p -> JObj [ "assumptionId", uuid p.AssumptionId; "state", enum Enums.assumptionState p.State ]
        | ContradictionRecorded p ->
            JObj [ "contradictionId", uuid p.ContradictionId; "statement", text p.Statement; "evidenceIds", uuids p.EvidenceIds; "materiality", enum Enums.materiality p.Materiality ]
        | ContradictionTransitioned p -> JObj [ "contradictionId", uuid p.ContradictionId; "state", enum Enums.contradictionState p.State; "basis", text p.Basis ]
        | PredictionRecorded p ->
            JObj
                [ "predictionId", uuid p.PredictionId; "hypothesisId", uuid p.HypothesisId; "statement", text p.Statement
                  "strength", enum Enums.predictionStrength p.Strength; "timing", enum Enums.predictionTiming p.Timing ]
        | ActionRecorded p ->
            JObj [ "actionId", uuid p.ActionId; "kind", enum Enums.actionKind p.Kind; "purpose", text p.Purpose; "targetHypothesisIds", uuids p.TargetHypothesisIds ]
        | ActionTransitioned p -> JObj [ "actionId", uuid p.ActionId; "state", enum Enums.actionState p.State ]
        | FindingRecorded p ->
            JObj [ "findingId", uuid p.FindingId; "actionId", uuid p.ActionId; "originalResult", text p.OriginalResult; "interpretation", optText p.Interpretation ]
        | NegativeKnowledgeEstablished p ->
            JObj [ "negativeKnowledgeId", uuid p.NegativeKnowledgeId; "statement", text p.Statement; "scope", text p.Scope; "basisEvidenceIds", uuids p.BasisEvidenceIds ]
        | CausalAssertionRecorded p ->
            JObj [ "causeId", uuid p.CauseId; "outcome", text p.Outcome; "members", JArr(p.Members |> List.map causalMember); "mechanism", optText p.Mechanism ]
        | CausalAssertionConfirmed p -> JObj [ "causeId", uuid p.CauseId; "policy", text p.Policy ]
        | CausalAssertionReopened p -> JObj [ "causeId", uuid p.CauseId; "reason", text p.Reason ]
        | CorrectiveActionRecorded p ->
            JObj
                [ "correctiveActionId", uuid p.CorrectiveActionId; "kind", enum Enums.correctiveActionKind p.Kind; "description", text p.Description
                  "targetCauseIds", uuids p.TargetCauseIds ]
        | CorrectiveActionTransitioned p -> JObj [ "correctiveActionId", uuid p.CorrectiveActionId; "state", enum Enums.correctiveActionState p.State ]
        | CriterionRecorded p ->
            JObj [ "criterionId", uuid p.CriterionId; "correctiveActionId", uuid p.CorrectiveActionId; "expectedOutcome", text p.ExpectedOutcome; "method", optText p.Method ]
        | VerificationRecorded p ->
            JObj [ "verificationId", uuid p.VerificationId; "criterionId", uuid p.CriterionId; "outcome", enum Enums.verificationOutcome p.Outcome; "evidenceIds", uuids p.EvidenceIds ]
        | ConclusionRecorded p ->
            JObj
                [ "conclusionId", uuid p.ConclusionId; "statement", text p.Statement; "strength", enum Enums.conclusionStrength p.Strength
                  "causeIds", uuids p.CauseIds; "residualUncertainty", optText p.ResidualUncertainty ]
        | ProposalRecorded p -> JObj [ "proposalId", uuid p.ProposalId; "kind", enum Enums.proposalKind p.Kind; "text", text p.ProposalText; "rationale", optText p.Rationale ]
        | ProposalWasAccepted p -> JObj [ "proposalId", uuid p.ProposalId; "targetId", uuid p.TargetId ]
        | ProposalWasRejected p -> JObj [ "proposalId", uuid p.ProposalId; "reason", text p.Reason ]

    /// The full envelope, rebuilt from the typed values only (never from RawLine).
    let event (d: DecodedEvent) : JValue =
        let e = d.Envelope
        JObj
            [ "formatVersion", str e.FormatVersion
              "eventId", uuid e.EventId
              "investigationId", uuid e.InvestigationId
              "aggregateVersion", JInt e.AggregateVersion
              "eventType", str (EventDecoder.discriminator d.Event)
              "occurredAt", ts e.OccurredAt
              "actor", actor e.Actor
              "semanticVersion", str e.SemanticVersion
              "correlationId", uuid e.CorrelationId
              "payload", payload d.Event ]

    let canonicalLine (d: DecodedEvent) = Json.canonical (event d)
