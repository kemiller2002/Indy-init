namespace Causa.HistoricalCompatibility

/// Typed payloads, one record per registered discriminator (CONTRACT §7). Field names follow the wire; every wire
/// field is represented, nullable wire fields are `option`, and arrays keep their stored order.
type InvestigationStartedP = { Title: Text; ObservationId: Uuid; Original: Text }
type LifecycleChangedP = { From: Lifecycle; To: Lifecycle; Command: LifecycleCommand; Reason: Text option }
type ObservationRecordedP = { ObservationId: Uuid; Original: Text; ObservedAt: Timestamp option }
type HypothesisRecordedP = { HypothesisId: Uuid; Statement: Text; Mechanism: Text option }
type HypothesisTransitionedP = { HypothesisId: Uuid; From: HypothesisState; To: HypothesisState; Command: HypothesisCommand; Reason: Text; EvidenceIds: Uuid list }
type EvidenceAdmittedP = { EvidenceId: Uuid; ContentKind: ContentKind; Content: Text; Category: EvidenceCategory; Source: Text; CapturedAt: Timestamp option }
type EvidenceSupersededP = { EvidenceId: Uuid; ReplacementEvidenceId: Uuid; Reason: Text }
type EvidenceVerificationAssessedP = { EvidenceId: Uuid; Verification: VerificationStatus }
type EvidenceRelatedP = { RelationshipId: Uuid; EvidenceId: Uuid; HypothesisId: Uuid; Kind: RelationshipKind; Rationale: Text option }
type UnknownIdentifiedP = { UnknownId: Uuid; Question: Text; Materiality: Materiality }
type UnknownTransitionedP = { UnknownId: Uuid; State: UnknownState; Basis: Text }
type AssumptionRecordedP = { AssumptionId: Uuid; Proposition: Text; Materiality: Materiality }
type AssumptionTransitionedP = { AssumptionId: Uuid; State: AssumptionState }
type ContradictionRecordedP = { ContradictionId: Uuid; Statement: Text; EvidenceIds: Uuid list; Materiality: Materiality }
type ContradictionTransitionedP = { ContradictionId: Uuid; State: ContradictionState; Basis: Text }
type PredictionRecordedP = { PredictionId: Uuid; HypothesisId: Uuid; Statement: Text; Strength: PredictionStrength; Timing: PredictionTiming }
type ActionRecordedP = { ActionId: Uuid; Kind: ActionKind; Purpose: Text; TargetHypothesisIds: Uuid list }
type ActionTransitionedP = { ActionId: Uuid; State: ActionState }
type FindingRecordedP = { FindingId: Uuid; ActionId: Uuid; OriginalResult: Text; Interpretation: Text option }
type NegativeKnowledgeEstablishedP = { NegativeKnowledgeId: Uuid; Statement: Text; Scope: Text; BasisEvidenceIds: Uuid list }
type CausalMember = { HypothesisId: Uuid; Role: CauseRole }
type CausalAssertionRecordedP = { CauseId: Uuid; Outcome: Text; Members: CausalMember list; Mechanism: Text option }
type CausalAssertionConfirmedP = { CauseId: Uuid; Policy: Text }
type CausalAssertionReopenedP = { CauseId: Uuid; Reason: Text }
type CorrectiveActionRecordedP = { CorrectiveActionId: Uuid; Kind: CorrectiveActionKind; Description: Text; TargetCauseIds: Uuid list }
type CorrectiveActionTransitionedP = { CorrectiveActionId: Uuid; State: CorrectiveActionState }
type CriterionRecordedP = { CriterionId: Uuid; CorrectiveActionId: Uuid; ExpectedOutcome: Text; Method: Text option }
type VerificationRecordedP = { VerificationId: Uuid; CriterionId: Uuid; Outcome: VerificationOutcome; EvidenceIds: Uuid list }
type ConclusionRecordedP = { ConclusionId: Uuid; Statement: Text; Strength: ConclusionStrength; CauseIds: Uuid list; ResidualUncertainty: Text option }
type ProposalRecordedP = { ProposalId: Uuid; Kind: ProposalKind; ProposalText: Text; Rationale: Text option }
type ProposalWasAcceptedP = { ProposalId: Uuid; TargetId: Uuid }
type ProposalWasRejectedP = { ProposalId: Uuid; Reason: Text }

/// The complete causa-storage v1.0.0 event registry: one case per registered discriminator, with no catch-all case.
type WireEvent =
    | InvestigationStarted of InvestigationStartedP
    | LifecycleChanged of LifecycleChangedP
    | ObservationRecorded of ObservationRecordedP
    | HypothesisRecorded of HypothesisRecordedP
    | HypothesisTransitioned of HypothesisTransitionedP
    | EvidenceAdmitted of EvidenceAdmittedP
    | EvidenceSuperseded of EvidenceSupersededP
    | EvidenceVerificationAssessed of EvidenceVerificationAssessedP
    | EvidenceRelated of EvidenceRelatedP
    | UnknownIdentified of UnknownIdentifiedP
    | UnknownTransitioned of UnknownTransitionedP
    | AssumptionRecorded of AssumptionRecordedP
    | AssumptionTransitioned of AssumptionTransitionedP
    | ContradictionRecorded of ContradictionRecordedP
    | ContradictionTransitioned of ContradictionTransitionedP
    | PredictionRecorded of PredictionRecordedP
    | ActionRecorded of ActionRecordedP
    | ActionTransitioned of ActionTransitionedP
    | FindingRecorded of FindingRecordedP
    | NegativeKnowledgeEstablished of NegativeKnowledgeEstablishedP
    | CausalAssertionRecorded of CausalAssertionRecordedP
    | CausalAssertionConfirmed of CausalAssertionConfirmedP
    | CausalAssertionReopened of CausalAssertionReopenedP
    | CorrectiveActionRecorded of CorrectiveActionRecordedP
    | CorrectiveActionTransitioned of CorrectiveActionTransitionedP
    | CriterionRecorded of CriterionRecordedP
    | VerificationRecorded of VerificationRecordedP
    | ConclusionRecorded of ConclusionRecordedP
    | ProposalRecorded of ProposalRecordedP
    | ProposalWasAccepted of ProposalWasAcceptedP
    | ProposalWasRejected of ProposalWasRejectedP

/// A decoded event: the full envelope (identity, order, provenance, raw line) plus the typed payload.
type DecodedEvent = { Envelope: HistoricalEnvelope; Event: WireEvent }

/// An event whose discriminator is not registered. It is never mapped to a generic event or skipped: the raw line is
/// kept whole, and a history containing it is refused (CONTRACT §7.1, CAUSA.UNKNOWN_EVENT_TYPE).
type UnsupportedEvent = { Line: int; RawLine: string; EventType: string }

/// The per-line result of typed decoding. There is no success case that drops content.
type LineOutcome =
    | Decoded of DecodedEvent
    | Unregistered of UnsupportedEvent
    | Rejected of ReaderError

module EventDecoder =
    /// Registered discriminators in engine EventBody order (CONTRACT §7).
    let eventTypes =
        [ "InvestigationStarted"; "LifecycleChanged"; "ObservationRecorded"; "HypothesisRecorded"; "HypothesisTransitioned"
          "EvidenceAdmitted"; "EvidenceSuperseded"; "EvidenceVerificationAssessed"; "EvidenceRelated"; "UnknownIdentified"
          "UnknownTransitioned"; "AssumptionRecorded"; "AssumptionTransitioned"; "ContradictionRecorded"; "ContradictionTransitioned"
          "PredictionRecorded"; "ActionRecorded"; "ActionTransitioned"; "FindingRecorded"; "NegativeKnowledgeEstablished"
          "CausalAssertionRecorded"; "CausalAssertionConfirmed"; "CausalAssertionReopened"; "CorrectiveActionRecorded"
          "CorrectiveActionTransitioned"; "CriterionRecorded"; "VerificationRecorded"; "ConclusionRecorded"; "ProposalRecorded"
          "ProposalWasAccepted"; "ProposalWasRejected" ]

    let isRegistered (t: string) = List.contains t eventTypes

    let discriminator =
        function
        | InvestigationStarted _ -> "InvestigationStarted"
        | LifecycleChanged _ -> "LifecycleChanged"
        | ObservationRecorded _ -> "ObservationRecorded"
        | HypothesisRecorded _ -> "HypothesisRecorded"
        | HypothesisTransitioned _ -> "HypothesisTransitioned"
        | EvidenceAdmitted _ -> "EvidenceAdmitted"
        | EvidenceSuperseded _ -> "EvidenceSuperseded"
        | EvidenceVerificationAssessed _ -> "EvidenceVerificationAssessed"
        | EvidenceRelated _ -> "EvidenceRelated"
        | UnknownIdentified _ -> "UnknownIdentified"
        | UnknownTransitioned _ -> "UnknownTransitioned"
        | AssumptionRecorded _ -> "AssumptionRecorded"
        | AssumptionTransitioned _ -> "AssumptionTransitioned"
        | ContradictionRecorded _ -> "ContradictionRecorded"
        | ContradictionTransitioned _ -> "ContradictionTransitioned"
        | PredictionRecorded _ -> "PredictionRecorded"
        | ActionRecorded _ -> "ActionRecorded"
        | ActionTransitioned _ -> "ActionTransitioned"
        | FindingRecorded _ -> "FindingRecorded"
        | NegativeKnowledgeEstablished _ -> "NegativeKnowledgeEstablished"
        | CausalAssertionRecorded _ -> "CausalAssertionRecorded"
        | CausalAssertionConfirmed _ -> "CausalAssertionConfirmed"
        | CausalAssertionReopened _ -> "CausalAssertionReopened"
        | CorrectiveActionRecorded _ -> "CorrectiveActionRecorded"
        | CorrectiveActionTransitioned _ -> "CorrectiveActionTransitioned"
        | CriterionRecorded _ -> "CriterionRecorded"
        | VerificationRecorded _ -> "VerificationRecorded"
        | ConclusionRecorded _ -> "ConclusionRecorded"
        | ProposalRecorded _ -> "ProposalRecorded"
        | ProposalWasAccepted _ -> "ProposalWasAccepted"
        | ProposalWasRejected _ -> "ProposalWasRejected"

    let private require cond detail = if cond then Ok() else Error detail

    let private member' (o: (string * JValue) list) : Result<CausalMember, string> =
        result {
            do! Field.exactKeys "causal member" [ "hypothesisId"; "role" ] o
            let! h = Field.uuid o "hypothesisId"
            let! r = Field.enum Enums.causeRole o "role"
            return { HypothesisId = h; Role = r }
        }

    let private members o =
        Field.arr o "members"
        |> Result.bind (traverse (function JObj m -> member' m | _ -> Error "field 'members' must contain objects"))
        |> Result.bind (fun ms ->
            if List.isEmpty ms then Error "field 'members' must contain at least one member"
            elif List.length (List.distinct ms) <> List.length ms then Error "field 'members' contains a duplicate member"
            else Ok ms)

    let private lifecycleInvariant (p: LifecycleChangedP) =
        let expectedTo, reasonRequired =
            match p.Command with
            | ActivateInvestigation -> Active, Some false
            | MarkAwaitingEvidence -> AwaitingEvidence, Some true
            | MarkAwaitingVerification -> AwaitingVerification, Some false
            | ResolveInvestigation -> Resolved, Some false
            | CloseInvestigation -> Closed, Some false
            | ReopenInvestigation -> Active, Some true
            | LifecycleVerificationFailed -> Active, Some true
        result {
            do! require (p.To = expectedTo) "LifecycleChanged 'to' does not match its command"
            do! require (reasonRequired <> Some true || p.Reason.IsSome) "LifecycleChanged command requires a reason"
            do! require (reasonRequired <> Some false || p.Reason.IsNone) "LifecycleChanged command requires reason null"
            do! require (p.Command <> LifecycleVerificationFailed || p.From = AwaitingVerification) "verificationFailed must leave awaitingVerification"
        }

    let private hypothesisInvariant (p: HypothesisTransitionedP) =
        let expectedTo, needsEvidence =
            match p.Command with
            | MarkPlausible -> HPlausible, false
            | MarkSupported -> HSupported, true
            | MarkWeakened -> HWeakened, true
            | Falsify -> HFalsified, true
            | Supersede -> HSuperseded, false
            | Reopen -> HPlausible, true
        result {
            do! require (p.To = expectedTo) "HypothesisTransitioned 'to' does not match its command"
            do! require (not needsEvidence || not p.EvidenceIds.IsEmpty) "HypothesisTransitioned command must cite evidence"
        }

    /// Decodes one payload for a registered discriminator. Unknown fields, missing fields, wrong types, values outside
    /// an enumeration and violated invariants are all explicit errors.
    let decodePayload (eventType: string) (o: (string * JValue) list) : Result<WireEvent, string> =
        let keys = Field.exactKeys (eventType + " payload")
        match eventType with
        | "InvestigationStarted" ->
            result {
                do! keys [ "title"; "observationId"; "original" ] o
                let! title = Field.text o "title"
                let! oid = Field.uuid o "observationId"
                let! original = Field.text o "original"
                return InvestigationStarted { Title = title; ObservationId = oid; Original = original }
            }
        | "LifecycleChanged" ->
            result {
                do! keys [ "from"; "to"; "command"; "reason" ] o
                let! from = Field.enum Enums.lifecycle o "from"
                let! target = Field.target Enums.lifecycle Enums.lifecycleTarget o "to"
                let! command = Field.enum Enums.lifecycleCommand o "command"
                let! reason = Field.optText o "reason"
                let p = { From = from; To = target; Command = command; Reason = reason }
                do! lifecycleInvariant p
                return LifecycleChanged p
            }
        | "ObservationRecorded" ->
            result {
                do! keys [ "observationId"; "original"; "observedAt" ] o
                let! oid = Field.uuid o "observationId"
                let! original = Field.text o "original"
                let! at = Field.optTimestamp o "observedAt"
                return ObservationRecorded { ObservationId = oid; Original = original; ObservedAt = at }
            }
        | "HypothesisRecorded" ->
            result {
                do! keys [ "hypothesisId"; "statement"; "mechanism" ] o
                let! hid = Field.uuid o "hypothesisId"
                let! statement = Field.text o "statement"
                let! mechanism = Field.optText o "mechanism"
                return HypothesisRecorded { HypothesisId = hid; Statement = statement; Mechanism = mechanism }
            }
        | "HypothesisTransitioned" ->
            result {
                do! keys [ "hypothesisId"; "from"; "to"; "command"; "reason"; "evidenceIds" ] o
                let! hid = Field.uuid o "hypothesisId"
                let! from = Field.enum Enums.hypothesisState o "from"
                let! target = Field.target Enums.hypothesisState Enums.hypothesisTarget o "to"
                let! command = Field.enum Enums.hypothesisCommand o "command"
                let! reason = Field.text o "reason"
                let! evidence = Field.uuids o "evidenceIds"
                let p = { HypothesisId = hid; From = from; To = target; Command = command; Reason = reason; EvidenceIds = evidence }
                do! hypothesisInvariant p
                return HypothesisTransitioned p
            }
        | "EvidenceAdmitted" ->
            result {
                do! keys [ "evidenceId"; "contentKind"; "content"; "category"; "source"; "capturedAt" ] o
                let! eid = Field.uuid o "evidenceId"
                let! kind = Field.enum Enums.contentKind o "contentKind"
                let! content = Field.text o "content"
                let! category = Field.enum Enums.evidenceCategory o "category"
                let! source = Field.text o "source"
                let! captured = Field.optTimestamp o "capturedAt"
                do! require (kind <> ArtifactContent || Text.isHttpUrl content.Value) "artifact evidence content must be an absolute http(s) URL"
                return EvidenceAdmitted { EvidenceId = eid; ContentKind = kind; Content = content; Category = category; Source = source; CapturedAt = captured }
            }
        | "EvidenceSuperseded" ->
            result {
                do! keys [ "evidenceId"; "replacementEvidenceId"; "reason" ] o
                let! eid = Field.uuid o "evidenceId"
                let! rid = Field.uuid o "replacementEvidenceId"
                let! reason = Field.text o "reason"
                return EvidenceSuperseded { EvidenceId = eid; ReplacementEvidenceId = rid; Reason = reason }
            }
        | "EvidenceVerificationAssessed" ->
            result {
                do! keys [ "evidenceId"; "verification" ] o
                let! eid = Field.uuid o "evidenceId"
                let! v = Field.enum Enums.verificationStatus o "verification"
                return EvidenceVerificationAssessed { EvidenceId = eid; Verification = v }
            }
        | "EvidenceRelated" ->
            result {
                do! keys [ "relationshipId"; "evidenceId"; "hypothesisId"; "kind"; "rationale" ] o
                let! rid = Field.uuid o "relationshipId"
                let! eid = Field.uuid o "evidenceId"
                let! hid = Field.uuid o "hypothesisId"
                let! kind = Field.enum Enums.relationshipKind o "kind"
                let! rationale = Field.optText o "rationale"
                return EvidenceRelated { RelationshipId = rid; EvidenceId = eid; HypothesisId = hid; Kind = kind; Rationale = rationale }
            }
        | "UnknownIdentified" ->
            result {
                do! keys [ "unknownId"; "question"; "materiality" ] o
                let! uid = Field.uuid o "unknownId"
                let! q = Field.text o "question"
                let! m = Field.enum Enums.materiality o "materiality"
                return UnknownIdentified { UnknownId = uid; Question = q; Materiality = m }
            }
        | "UnknownTransitioned" ->
            result {
                do! keys [ "unknownId"; "state"; "basis" ] o
                let! uid = Field.uuid o "unknownId"
                let! s = Field.target Enums.unknownState Enums.unknownTarget o "state"
                let! b = Field.text o "basis"
                return UnknownTransitioned { UnknownId = uid; State = s; Basis = b }
            }
        | "AssumptionRecorded" ->
            result {
                do! keys [ "assumptionId"; "proposition"; "materiality" ] o
                let! aid = Field.uuid o "assumptionId"
                let! p = Field.text o "proposition"
                let! m = Field.enum Enums.materiality o "materiality"
                return AssumptionRecorded { AssumptionId = aid; Proposition = p; Materiality = m }
            }
        | "AssumptionTransitioned" ->
            result {
                do! keys [ "assumptionId"; "state" ] o
                let! aid = Field.uuid o "assumptionId"
                let! s = Field.target Enums.assumptionState Enums.assumptionTarget o "state"
                return AssumptionTransitioned { AssumptionId = aid; State = s }
            }
        | "ContradictionRecorded" ->
            result {
                do! keys [ "contradictionId"; "statement"; "evidenceIds"; "materiality" ] o
                let! cid = Field.uuid o "contradictionId"
                let! s = Field.text o "statement"
                let! e = Field.uuids o "evidenceIds"
                let! m = Field.enum Enums.materiality o "materiality"
                return ContradictionRecorded { ContradictionId = cid; Statement = s; EvidenceIds = e; Materiality = m }
            }
        | "ContradictionTransitioned" ->
            result {
                do! keys [ "contradictionId"; "state"; "basis" ] o
                let! cid = Field.uuid o "contradictionId"
                let! s = Field.target Enums.contradictionState Enums.contradictionTarget o "state"
                let! b = Field.text o "basis"
                return ContradictionTransitioned { ContradictionId = cid; State = s; Basis = b }
            }
        | "PredictionRecorded" ->
            result {
                do! keys [ "predictionId"; "hypothesisId"; "statement"; "strength"; "timing" ] o
                let! pid = Field.uuid o "predictionId"
                let! hid = Field.uuid o "hypothesisId"
                let! s = Field.text o "statement"
                let! st = Field.enum Enums.predictionStrength o "strength"
                let! t = Field.enum Enums.predictionTiming o "timing"
                return PredictionRecorded { PredictionId = pid; HypothesisId = hid; Statement = s; Strength = st; Timing = t }
            }
        | "ActionRecorded" ->
            result {
                do! keys [ "actionId"; "kind"; "purpose"; "targetHypothesisIds" ] o
                let! aid = Field.uuid o "actionId"
                let! k = Field.enum Enums.actionKind o "kind"
                let! p = Field.text o "purpose"
                let! t = Field.uuids o "targetHypothesisIds"
                return ActionRecorded { ActionId = aid; Kind = k; Purpose = p; TargetHypothesisIds = t }
            }
        | "ActionTransitioned" ->
            result {
                do! keys [ "actionId"; "state" ] o
                let! aid = Field.uuid o "actionId"
                let! s = Field.target Enums.actionState Enums.actionTarget o "state"
                return ActionTransitioned { ActionId = aid; State = s }
            }
        | "FindingRecorded" ->
            result {
                do! keys [ "findingId"; "actionId"; "originalResult"; "interpretation" ] o
                let! fid = Field.uuid o "findingId"
                let! aid = Field.uuid o "actionId"
                let! r = Field.text o "originalResult"
                let! i = Field.optText o "interpretation"
                return FindingRecorded { FindingId = fid; ActionId = aid; OriginalResult = r; Interpretation = i }
            }
        | "NegativeKnowledgeEstablished" ->
            result {
                do! keys [ "negativeKnowledgeId"; "statement"; "scope"; "basisEvidenceIds" ] o
                let! nid = Field.uuid o "negativeKnowledgeId"
                let! s = Field.text o "statement"
                let! sc = Field.text o "scope"
                let! b = Field.nonEmptyUuids o "basisEvidenceIds"
                return NegativeKnowledgeEstablished { NegativeKnowledgeId = nid; Statement = s; Scope = sc; BasisEvidenceIds = b }
            }
        | "CausalAssertionRecorded" ->
            result {
                do! keys [ "causeId"; "outcome"; "members"; "mechanism" ] o
                let! cid = Field.uuid o "causeId"
                let! outcome = Field.text o "outcome"
                let! ms = members o
                let! mech = Field.optText o "mechanism"
                return CausalAssertionRecorded { CauseId = cid; Outcome = outcome; Members = ms; Mechanism = mech }
            }
        | "CausalAssertionConfirmed" ->
            result {
                do! keys [ "causeId"; "policy" ] o
                let! cid = Field.uuid o "causeId"
                let! p = Field.text o "policy"
                return CausalAssertionConfirmed { CauseId = cid; Policy = p }
            }
        | "CausalAssertionReopened" ->
            result {
                do! keys [ "causeId"; "reason" ] o
                let! cid = Field.uuid o "causeId"
                let! r = Field.text o "reason"
                return CausalAssertionReopened { CauseId = cid; Reason = r }
            }
        | "CorrectiveActionRecorded" ->
            result {
                do! keys [ "correctiveActionId"; "kind"; "description"; "targetCauseIds" ] o
                let! cid = Field.uuid o "correctiveActionId"
                let! k = Field.enum Enums.correctiveActionKind o "kind"
                let! d = Field.text o "description"
                let! t = Field.uuids o "targetCauseIds"
                return CorrectiveActionRecorded { CorrectiveActionId = cid; Kind = k; Description = d; TargetCauseIds = t }
            }
        | "CorrectiveActionTransitioned" ->
            result {
                do! keys [ "correctiveActionId"; "state" ] o
                let! cid = Field.uuid o "correctiveActionId"
                let! s = Field.target Enums.correctiveActionState Enums.correctiveActionTarget o "state"
                return CorrectiveActionTransitioned { CorrectiveActionId = cid; State = s }
            }
        | "CriterionRecorded" ->
            result {
                do! keys [ "criterionId"; "correctiveActionId"; "expectedOutcome"; "method" ] o
                let! crid = Field.uuid o "criterionId"
                let! caid = Field.uuid o "correctiveActionId"
                let! e = Field.text o "expectedOutcome"
                let! m = Field.optText o "method"
                return CriterionRecorded { CriterionId = crid; CorrectiveActionId = caid; ExpectedOutcome = e; Method = m }
            }
        | "VerificationRecorded" ->
            result {
                do! keys [ "verificationId"; "criterionId"; "outcome"; "evidenceIds" ] o
                let! vid = Field.uuid o "verificationId"
                let! crid = Field.uuid o "criterionId"
                let! out = Field.enum Enums.verificationOutcome o "outcome"
                let! e = Field.uuids o "evidenceIds"
                do! require (out <> Satisfied || not e.IsEmpty) "a satisfied verification must cite evidence"
                return VerificationRecorded { VerificationId = vid; CriterionId = crid; Outcome = out; EvidenceIds = e }
            }
        | "ConclusionRecorded" ->
            result {
                do! keys [ "conclusionId"; "statement"; "strength"; "causeIds"; "residualUncertainty" ] o
                let! cid = Field.uuid o "conclusionId"
                let! s = Field.text o "statement"
                let! st = Field.enum Enums.conclusionStrength o "strength"
                let! causes = Field.uuids o "causeIds"
                let! r = Field.optText o "residualUncertainty"
                return ConclusionRecorded { ConclusionId = cid; Statement = s; Strength = st; CauseIds = causes; ResidualUncertainty = r }
            }
        | "ProposalRecorded" ->
            result {
                do! keys [ "proposalId"; "kind"; "text"; "rationale" ] o
                let! pid = Field.uuid o "proposalId"
                let! k = Field.enum Enums.proposalKind o "kind"
                let! t = Field.text o "text"
                let! r = Field.optText o "rationale"
                return ProposalRecorded { ProposalId = pid; Kind = k; ProposalText = t; Rationale = r }
            }
        | "ProposalWasAccepted" ->
            result {
                do! keys [ "proposalId"; "targetId" ] o
                let! pid = Field.uuid o "proposalId"
                let! t = Field.uuid o "targetId"
                return ProposalWasAccepted { ProposalId = pid; TargetId = t }
            }
        | "ProposalWasRejected" ->
            result {
                do! keys [ "proposalId"; "reason" ] o
                let! pid = Field.uuid o "proposalId"
                let! r = Field.text o "reason"
                return ProposalWasRejected { ProposalId = pid; Reason = r }
            }
        | other -> Error $"event type '{other}' is not registered"
