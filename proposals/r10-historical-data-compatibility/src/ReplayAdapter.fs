namespace Causa.HistoricalCompatibility

/// Entity provenance (CONTRACT §8.3): who created the entity, and when; never changed by later updates.
type Provenance = { Actor: Actor; RecordedAt: Timestamp }

type HypothesisChange =
    { From: HypothesisState; To: HypothesisState; Command: HypothesisCommand; Reason: Text; EvidenceIds: Uuid list; At: Timestamp; AggregateVersion: int64 }

type ObservationS = { Id: Uuid; Original: Text; ObservedAt: Timestamp option; Provenance: Provenance }
type HypothesisS = { Id: Uuid; Statement: Text; Mechanism: Text option; State: HypothesisState; History: HypothesisChange list; Provenance: Provenance }
type EvidenceS =
    { Id: Uuid; ContentKind: ContentKind; Content: Text; Category: EvidenceCategory; Source: Text; CapturedAt: Timestamp option
      Verification: VerificationStatus; SupersededBy: Uuid option; AdmittedAtVersion: int64; Provenance: Provenance }
type RelationshipS = { Id: Uuid; EvidenceId: Uuid; HypothesisId: Uuid; Kind: RelationshipKind; Rationale: Text option; Provenance: Provenance }
type UnknownS = { Id: Uuid; Question: Text; Materiality: Materiality; State: UnknownState; Basis: Text option; Provenance: Provenance }
type AssumptionS = { Id: Uuid; Proposition: Text; Materiality: Materiality; State: AssumptionState; Provenance: Provenance }
type ContradictionS = { Id: Uuid; Statement: Text; EvidenceIds: Uuid list; Materiality: Materiality; State: ContradictionState; Basis: Text option; Provenance: Provenance }
type PredictionS = { Id: Uuid; HypothesisId: Uuid; Statement: Text; Strength: PredictionStrength; Timing: PredictionTiming; Provenance: Provenance }
type ActionS = { Id: Uuid; Kind: ActionKind; Purpose: Text; TargetHypothesisIds: Uuid list; State: ActionState; Provenance: Provenance }
type FindingS = { Id: Uuid; ActionId: Uuid; OriginalResult: Text; Interpretation: Text option; Provenance: Provenance }
type NegativeKnowledgeS = { Id: Uuid; Statement: Text; Scope: Text; BasisEvidenceIds: Uuid list; Provenance: Provenance }
type CauseS = { Id: Uuid; Outcome: Text; Members: CausalMember list; Mechanism: Text option; State: CauseState; Provenance: Provenance }
type CorrectiveActionS =
    { Id: Uuid; Kind: CorrectiveActionKind; Description: Text; TargetCauseIds: Uuid list; State: CorrectiveActionState; AwaitingSinceVersion: int64; Provenance: Provenance }
type CriterionS = { Id: Uuid; CorrectiveActionId: Uuid; ExpectedOutcome: Text; Method: Text option; Provenance: Provenance }
type VerificationS = { Id: Uuid; CriterionId: Uuid; Outcome: VerificationOutcome; EvidenceIds: Uuid list; RecordedAtVersion: int64; Provenance: Provenance }
type ConclusionS =
    { Id: Uuid; Statement: Text; Strength: ConclusionStrength; CauseIds: Uuid list; ResidualUncertainty: Text option; RecordedAtVersion: int64; Provenance: Provenance }
type ProposalS = { Id: Uuid; Kind: ProposalKind; ProposalText: Text; Rationale: Text option; State: ProposalState; Decision: Text option; Provenance: Provenance }

/// The CONTRACT §8.2 state object. Maps are keyed by Uuid, whose ordering is the ordinal order of the UUID text,
/// which is exactly the §8.3 sort order of every entity array.
type InvestigationState =
    { InvestigationId: Uuid
      Title: Text
      Lifecycle: Lifecycle
      Version: int64
      CreatedAt: Timestamp
      Observations: Map<Uuid, ObservationS>
      Hypotheses: Map<Uuid, HypothesisS>
      Evidence: Map<Uuid, EvidenceS>
      Relationships: Map<Uuid, RelationshipS>
      Unknowns: Map<Uuid, UnknownS>
      Assumptions: Map<Uuid, AssumptionS>
      Contradictions: Map<Uuid, ContradictionS>
      Predictions: Map<Uuid, PredictionS>
      Actions: Map<Uuid, ActionS>
      Findings: Map<Uuid, FindingS>
      NegativeKnowledge: Map<Uuid, NegativeKnowledgeS>
      Causes: Map<Uuid, CauseS>
      CorrectiveActions: Map<Uuid, CorrectiveActionS>
      Criteria: Map<Uuid, CriterionS>
      Verifications: Map<Uuid, VerificationS>
      Conclusions: Map<Uuid, ConclusionS>
      Proposals: Map<Uuid, ProposalS> }

/// Something the fold met that cannot occur in an admissible history. The fold does what §8.3 says (leave the state
/// unchanged) and reports it, so a reconstruction is never silently presented as authoritative.
type ReplayAnomalyKind =
    | NoStartEvent
    | RepeatedStart
    | UpdateOfMissingEntity
    | DuplicateEntityId
    | DanglingReference
    | TransitionFromMismatch

type ReplayAnomaly = { Line: int; Kind: ReplayAnomalyKind; Detail: string }

/// A replay outcome. `Projected` is not an admissibility verdict; see HistoricalLoader for the acceptance levels.
type ReplayResult =
    { State: InvestigationState option
      Anomalies: ReplayAnomaly list }

module ReplayAdapter =
    let private provenance (e: HistoricalEnvelope) = { Actor = e.Actor; RecordedAt = e.OccurredAt }
    let private anomaly (e: HistoricalEnvelope) kind detail = { Line = e.Line; Kind = kind; Detail = detail }

    /// Adds a new entity, or reports a duplicate id and leaves the state unchanged (the contract defines no effect).
    let private create (env: HistoricalEnvelope) (what: string) (id: Uuid) (value: 'T) (m: Map<Uuid, 'T>) : Map<Uuid, 'T> * ReplayAnomaly list =
        if m.ContainsKey id then m, [ anomaly env DuplicateEntityId $"{what} {id} already exists" ] else m.Add(id, value), []

    /// Updates an existing entity, or reports the missing id and leaves the state unchanged (§8.3).
    let private update (env: HistoricalEnvelope) (what: string) (id: Uuid) (f: 'T -> 'T) (m: Map<Uuid, 'T>) : Map<Uuid, 'T> * ReplayAnomaly list =
        match m.TryFind id with
        | Some v -> m.Add(id, f v), []
        | None -> m, [ anomaly env UpdateOfMissingEntity $"{what} {id} does not exist" ]

    /// References that must name existing entities when the event is applied (necessary, not sufficient, for
    /// admissibility: negative fixture engine-dangling-reference).
    let private references (s: InvestigationState) (e: WireEvent) : (string * Uuid * bool) list =
        let ev id = "evidence", id, s.Evidence.ContainsKey id
        let hy id = "hypothesis", id, s.Hypotheses.ContainsKey id
        match e with
        | HypothesisTransitioned p -> p.EvidenceIds |> List.map ev
        | EvidenceSuperseded p -> [ ev p.EvidenceId; ev p.ReplacementEvidenceId ]
        | EvidenceRelated p -> [ ev p.EvidenceId; hy p.HypothesisId ]
        | ContradictionRecorded p -> p.EvidenceIds |> List.map ev
        | PredictionRecorded p -> [ hy p.HypothesisId ]
        | ActionRecorded p -> p.TargetHypothesisIds |> List.map hy
        | FindingRecorded p -> [ "action", p.ActionId, s.Actions.ContainsKey p.ActionId ]
        | NegativeKnowledgeEstablished p -> p.BasisEvidenceIds |> List.map ev
        | CausalAssertionRecorded p -> p.Members |> List.map (fun m -> hy m.HypothesisId)
        | CorrectiveActionRecorded p -> p.TargetCauseIds |> List.map (fun c -> "cause", c, s.Causes.ContainsKey c)
        | CriterionRecorded p -> [ "correctiveAction", p.CorrectiveActionId, s.CorrectiveActions.ContainsKey p.CorrectiveActionId ]
        | VerificationRecorded p -> ("criterion", p.CriterionId, s.Criteria.ContainsKey p.CriterionId) :: (p.EvidenceIds |> List.map ev)
        | ConclusionRecorded p -> p.CauseIds |> List.map (fun c -> "cause", c, s.Causes.ContainsKey c)
        | _ -> []

    let private start (env: HistoricalEnvelope) (p: InvestigationStartedP) : InvestigationState =
        { InvestigationId = env.InvestigationId
          Title = p.Title
          Lifecycle = Draft
          Version = env.AggregateVersion
          CreatedAt = env.OccurredAt
          Observations = Map.ofList [ p.ObservationId, { Id = p.ObservationId; Original = p.Original; ObservedAt = None; Provenance = provenance env } ]
          Hypotheses = Map.empty
          Evidence = Map.empty
          Relationships = Map.empty
          Unknowns = Map.empty
          Assumptions = Map.empty
          Contradictions = Map.empty
          Predictions = Map.empty
          Actions = Map.empty
          Findings = Map.empty
          NegativeKnowledge = Map.empty
          Causes = Map.empty
          CorrectiveActions = Map.empty
          Criteria = Map.empty
          Verifications = Map.empty
          Conclusions = Map.empty
          Proposals = Map.empty }

    /// One step of the normative fold (CONTRACT §8.3 table), plus anomaly reporting.
    let private step (s: InvestigationState) (d: DecodedEvent) : InvestigationState * ReplayAnomaly list =
        let env = d.Envelope
        let prov = provenance env
        let v = env.AggregateVersion
        let refAnomalies =
            references s d.Event
            |> List.filter (fun (_, _, ok) -> not ok)
            |> List.map (fun (what, id, _) -> anomaly env DanglingReference $"{EventDecoder.discriminator d.Event} cites missing {what} {id}")
        let s = { s with Version = v }
        let next, anomalies =
            match d.Event with
            | InvestigationStarted _ -> s, [ anomaly env RepeatedStart "a second InvestigationStarted" ]
            | LifecycleChanged p ->
                { s with Lifecycle = p.To },
                (if p.From <> s.Lifecycle then [ anomaly env TransitionFromMismatch $"lifecycle 'from' {WireEnum.toWire Enums.lifecycle p.From} but state is {WireEnum.toWire Enums.lifecycle s.Lifecycle}" ] else [])
            | ObservationRecorded p ->
                let m, a = create env "observation" p.ObservationId { Id = p.ObservationId; Original = p.Original; ObservedAt = p.ObservedAt; Provenance = prov } s.Observations
                { s with Observations = m }, a
            | HypothesisRecorded p ->
                let m, a = create env "hypothesis" p.HypothesisId { Id = p.HypothesisId; Statement = p.Statement; Mechanism = p.Mechanism; State = HProposed; History = []; Provenance = prov } s.Hypotheses
                { s with Hypotheses = m }, a
            | HypothesisTransitioned p ->
                let fromCheck =
                    match s.Hypotheses.TryFind p.HypothesisId with
                    | Some h when h.State <> p.From -> [ anomaly env TransitionFromMismatch $"hypothesis {p.HypothesisId} 'from' {WireEnum.toWire Enums.hypothesisState p.From} but state is {WireEnum.toWire Enums.hypothesisState h.State}" ]
                    | _ -> []
                let change = { From = p.From; To = p.To; Command = p.Command; Reason = p.Reason; EvidenceIds = p.EvidenceIds; At = env.OccurredAt; AggregateVersion = v }
                let m, a = update env "hypothesis" p.HypothesisId (fun (h: HypothesisS) -> { h with State = p.To; History = h.History @ [ change ] }) s.Hypotheses
                { s with Hypotheses = m }, fromCheck @ a
            | EvidenceAdmitted p ->
                let e =
                    { Id = p.EvidenceId; ContentKind = p.ContentKind; Content = p.Content; Category = p.Category; Source = p.Source; CapturedAt = p.CapturedAt
                      Verification = Unverified; SupersededBy = None; AdmittedAtVersion = v; Provenance = prov }
                let m, a = create env "evidence" p.EvidenceId e s.Evidence
                { s with Evidence = m }, a
            | EvidenceSuperseded p ->
                let m, a = update env "evidence" p.EvidenceId (fun (e: EvidenceS) -> { e with SupersededBy = Some p.ReplacementEvidenceId }) s.Evidence
                { s with Evidence = m }, a
            | EvidenceVerificationAssessed p ->
                let m, a = update env "evidence" p.EvidenceId (fun (e: EvidenceS) -> { e with Verification = p.Verification }) s.Evidence
                { s with Evidence = m }, a
            | EvidenceRelated p ->
                let r = { Id = p.RelationshipId; EvidenceId = p.EvidenceId; HypothesisId = p.HypothesisId; Kind = p.Kind; Rationale = p.Rationale; Provenance = prov }
                let m, a = create env "relationship" p.RelationshipId r s.Relationships
                { s with Relationships = m }, a
            | UnknownIdentified p ->
                let m, a = create env "unknown" p.UnknownId { Id = p.UnknownId; Question = p.Question; Materiality = p.Materiality; State = UIdentified; Basis = None; Provenance = prov } s.Unknowns
                { s with Unknowns = m }, a
            | UnknownTransitioned p ->
                let m, a = update env "unknown" p.UnknownId (fun (u: UnknownS) -> { u with State = p.State; Basis = Some p.Basis }) s.Unknowns
                { s with Unknowns = m }, a
            | AssumptionRecorded p ->
                let m, a = create env "assumption" p.AssumptionId { Id = p.AssumptionId; Proposition = p.Proposition; Materiality = p.Materiality; State = ExplicitUnverified; Provenance = prov } s.Assumptions
                { s with Assumptions = m }, a
            | AssumptionTransitioned p ->
                let m, a = update env "assumption" p.AssumptionId (fun (x: AssumptionS) -> { x with State = p.State }) s.Assumptions
                { s with Assumptions = m }, a
            | ContradictionRecorded p ->
                let c = { Id = p.ContradictionId; Statement = p.Statement; EvidenceIds = p.EvidenceIds; Materiality = p.Materiality; State = CDetected; Basis = None; Provenance = prov }
                let m, a = create env "contradiction" p.ContradictionId c s.Contradictions
                { s with Contradictions = m }, a
            | ContradictionTransitioned p ->
                let m, a = update env "contradiction" p.ContradictionId (fun (c: ContradictionS) -> { c with State = p.State; Basis = Some p.Basis }) s.Contradictions
                { s with Contradictions = m }, a
            | PredictionRecorded p ->
                let x = { Id = p.PredictionId; HypothesisId = p.HypothesisId; Statement = p.Statement; Strength = p.Strength; Timing = p.Timing; Provenance = prov }
                let m, a = create env "prediction" p.PredictionId x s.Predictions
                { s with Predictions = m }, a
            | ActionRecorded p ->
                let x = { Id = p.ActionId; Kind = p.Kind; Purpose = p.Purpose; TargetHypothesisIds = p.TargetHypothesisIds; State = AProposed; Provenance = prov }
                let m, a = create env "action" p.ActionId x s.Actions
                { s with Actions = m }, a
            | ActionTransitioned p ->
                let m, a = update env "action" p.ActionId (fun (x: ActionS) -> { x with State = p.State }) s.Actions
                { s with Actions = m }, a
            | FindingRecorded p ->
                let x = { Id = p.FindingId; ActionId = p.ActionId; OriginalResult = p.OriginalResult; Interpretation = p.Interpretation; Provenance = prov }
                let m, a = create env "finding" p.FindingId x s.Findings
                { s with Findings = m }, a
            | NegativeKnowledgeEstablished p ->
                let x = { Id = p.NegativeKnowledgeId; Statement = p.Statement; Scope = p.Scope; BasisEvidenceIds = p.BasisEvidenceIds; Provenance = prov }
                let m, a = create env "negativeKnowledge" p.NegativeKnowledgeId x s.NegativeKnowledge
                { s with NegativeKnowledge = m }, a
            | CausalAssertionRecorded p ->
                let x = { Id = p.CauseId; Outcome = p.Outcome; Members = p.Members; Mechanism = p.Mechanism; State = CauseProposed; Provenance = prov }
                let m, a = create env "cause" p.CauseId x s.Causes
                { s with Causes = m }, a
            | CausalAssertionConfirmed p ->
                let m, a = update env "cause" p.CauseId (fun (x: CauseS) -> { x with State = CauseConfirmed }) s.Causes
                { s with Causes = m }, a
            | CausalAssertionReopened p ->
                let m, a = update env "cause" p.CauseId (fun (x: CauseS) -> { x with State = CauseReopened }) s.Causes
                { s with Causes = m }, a
            | CorrectiveActionRecorded p ->
                let x = { Id = p.CorrectiveActionId; Kind = p.Kind; Description = p.Description; TargetCauseIds = p.TargetCauseIds; State = CaProposed; AwaitingSinceVersion = 0L; Provenance = prov }
                let m, a = create env "correctiveAction" p.CorrectiveActionId x s.CorrectiveActions
                { s with CorrectiveActions = m }, a
            | CorrectiveActionTransitioned p ->
                let f (x: CorrectiveActionS) =
                    { x with State = p.State; AwaitingSinceVersion = (if p.State = CaAwaitingVerificationOfEffect then v else x.AwaitingSinceVersion) }
                let m, a = update env "correctiveAction" p.CorrectiveActionId f s.CorrectiveActions
                { s with CorrectiveActions = m }, a
            | CriterionRecorded p ->
                let x = { Id = p.CriterionId; CorrectiveActionId = p.CorrectiveActionId; ExpectedOutcome = p.ExpectedOutcome; Method = p.Method; Provenance = prov }
                let m, a = create env "criterion" p.CriterionId x s.Criteria
                { s with Criteria = m }, a
            | VerificationRecorded p ->
                let x = { Id = p.VerificationId; CriterionId = p.CriterionId; Outcome = p.Outcome; EvidenceIds = p.EvidenceIds; RecordedAtVersion = v; Provenance = prov }
                let m, a = create env "verification" p.VerificationId x s.Verifications
                { s with Verifications = m }, a
            | ConclusionRecorded p ->
                let x = { Id = p.ConclusionId; Statement = p.Statement; Strength = p.Strength; CauseIds = p.CauseIds; ResidualUncertainty = p.ResidualUncertainty; RecordedAtVersion = v; Provenance = prov }
                let m, a = create env "conclusion" p.ConclusionId x s.Conclusions
                { s with Conclusions = m }, a
            | ProposalRecorded p ->
                let x = { Id = p.ProposalId; Kind = p.Kind; ProposalText = p.ProposalText; Rationale = p.Rationale; State = ProposalActive; Decision = None; Provenance = prov }
                let m, a = create env "proposal" p.ProposalId x s.Proposals
                { s with Proposals = m }, a
            | ProposalWasAccepted p ->
                let m, a = update env "proposal" p.ProposalId (fun (x: ProposalS) -> { x with State = ProposalAccepted }) s.Proposals
                { s with Proposals = m }, a
            | ProposalWasRejected p ->
                let m, a = update env "proposal" p.ProposalId (fun (x: ProposalS) -> { x with State = ProposalRejected; Decision = Some p.Reason }) s.Proposals
                { s with Proposals = m }, a
        next, refAnomalies @ anomalies

    /// Replays a decoded history in aggregateVersion (= file) order. Pure and deterministic.
    let replay (events: DecodedEvent list) : ReplayResult =
        events
        |> List.fold
            (fun (state: InvestigationState option, anomalies: ReplayAnomaly list) d ->
                match state, d.Event with
                | None, InvestigationStarted p -> Some(start d.Envelope p), anomalies
                | None, _ -> None, anomalies @ [ anomaly d.Envelope NoStartEvent "event before InvestigationStarted" ]
                | Some s, _ ->
                    let s', a = step s d
                    Some s', anomalies @ a)
            (None, [])
        |> fun (state, anomalies) -> { State = state; Anomalies = anomalies }

    // ---------------------------------------------------------------- serialization of the §8.2 state

    module private W =
        let uuid = EventEncoder.uuid
        let text = EventEncoder.text
        let optText = EventEncoder.optText
        let ts = EventEncoder.ts
        let optTs = EventEncoder.optTs
        let uuids = EventEncoder.uuids
        let enum e v = EventEncoder.enum e v
        let optUuid (u: Uuid option) = u |> Option.map uuid |> Option.defaultValue JNull
        let prov (p: Provenance) = JObj [ "actor", EventEncoder.actor p.Actor; "recordedAt", ts p.RecordedAt ]
        let arr (m: Map<Uuid, 'T>) (f: 'T -> JValue) = JArr(m |> Map.toList |> List.map (snd >> f))

    let stateJson (s: InvestigationState) : JValue =
        JObj
            [ "investigationId", W.uuid s.InvestigationId
              "title", W.text s.Title
              "lifecycle", W.enum Enums.lifecycle s.Lifecycle
              "version", JInt s.Version
              "createdAt", W.ts s.CreatedAt
              "observations", W.arr s.Observations (fun o -> JObj [ "id", W.uuid o.Id; "original", W.text o.Original; "observedAt", W.optTs o.ObservedAt; "provenance", W.prov o.Provenance ])
              "hypotheses",
              W.arr s.Hypotheses (fun h ->
                  JObj
                      [ "id", W.uuid h.Id; "statement", W.text h.Statement; "mechanism", W.optText h.Mechanism; "state", W.enum Enums.hypothesisState h.State
                        "history",
                        JArr(
                            h.History
                            |> List.map (fun c ->
                                JObj
                                    [ "from", W.enum Enums.hypothesisState c.From; "to", W.enum Enums.hypothesisState c.To; "command", W.enum Enums.hypothesisCommand c.Command
                                      "reason", W.text c.Reason; "evidenceIds", W.uuids c.EvidenceIds; "at", W.ts c.At; "aggregateVersion", JInt c.AggregateVersion ])
                        )
                        "provenance", W.prov h.Provenance ])
              "evidence",
              W.arr s.Evidence (fun e ->
                  JObj
                      [ "id", W.uuid e.Id; "contentKind", W.enum Enums.contentKind e.ContentKind; "content", W.text e.Content; "category", W.enum Enums.evidenceCategory e.Category
                        "source", W.text e.Source; "capturedAt", W.optTs e.CapturedAt; "verification", W.enum Enums.verificationStatus e.Verification
                        "supersededBy", W.optUuid e.SupersededBy; "admittedAtVersion", JInt e.AdmittedAtVersion; "provenance", W.prov e.Provenance ])
              "relationships",
              W.arr s.Relationships (fun r ->
                  JObj
                      [ "id", W.uuid r.Id; "evidenceId", W.uuid r.EvidenceId; "hypothesisId", W.uuid r.HypothesisId; "kind", W.enum Enums.relationshipKind r.Kind
                        "rationale", W.optText r.Rationale; "provenance", W.prov r.Provenance ])
              "unknowns",
              W.arr s.Unknowns (fun u ->
                  JObj
                      [ "id", W.uuid u.Id; "question", W.text u.Question; "materiality", W.enum Enums.materiality u.Materiality; "state", W.enum Enums.unknownState u.State
                        "basis", W.optText u.Basis; "provenance", W.prov u.Provenance ])
              "assumptions",
              W.arr s.Assumptions (fun a ->
                  JObj
                      [ "id", W.uuid a.Id; "proposition", W.text a.Proposition; "materiality", W.enum Enums.materiality a.Materiality
                        "state", W.enum Enums.assumptionState a.State; "provenance", W.prov a.Provenance ])
              "contradictions",
              W.arr s.Contradictions (fun c ->
                  JObj
                      [ "id", W.uuid c.Id; "statement", W.text c.Statement; "evidenceIds", W.uuids c.EvidenceIds; "materiality", W.enum Enums.materiality c.Materiality
                        "state", W.enum Enums.contradictionState c.State; "basis", W.optText c.Basis; "provenance", W.prov c.Provenance ])
              "predictions",
              W.arr s.Predictions (fun p ->
                  JObj
                      [ "id", W.uuid p.Id; "hypothesisId", W.uuid p.HypothesisId; "statement", W.text p.Statement; "strength", W.enum Enums.predictionStrength p.Strength
                        "timing", W.enum Enums.predictionTiming p.Timing; "provenance", W.prov p.Provenance ])
              "actions",
              W.arr s.Actions (fun a ->
                  JObj
                      [ "id", W.uuid a.Id; "kind", W.enum Enums.actionKind a.Kind; "purpose", W.text a.Purpose; "targetHypothesisIds", W.uuids a.TargetHypothesisIds
                        "state", W.enum Enums.actionState a.State; "provenance", W.prov a.Provenance ])
              "findings",
              W.arr s.Findings (fun f ->
                  JObj [ "id", W.uuid f.Id; "actionId", W.uuid f.ActionId; "originalResult", W.text f.OriginalResult; "interpretation", W.optText f.Interpretation; "provenance", W.prov f.Provenance ])
              "negativeKnowledge",
              W.arr s.NegativeKnowledge (fun n ->
                  JObj [ "id", W.uuid n.Id; "statement", W.text n.Statement; "scope", W.text n.Scope; "basisEvidenceIds", W.uuids n.BasisEvidenceIds; "provenance", W.prov n.Provenance ])
              "causes",
              W.arr s.Causes (fun c ->
                  JObj
                      [ "id", W.uuid c.Id; "outcome", W.text c.Outcome; "members", JArr(c.Members |> List.map EventEncoder.causalMember); "mechanism", W.optText c.Mechanism
                        "state", W.enum Enums.causeState c.State; "provenance", W.prov c.Provenance ])
              "correctiveActions",
              W.arr s.CorrectiveActions (fun c ->
                  JObj
                      [ "id", W.uuid c.Id; "kind", W.enum Enums.correctiveActionKind c.Kind; "description", W.text c.Description; "targetCauseIds", W.uuids c.TargetCauseIds
                        "state", W.enum Enums.correctiveActionState c.State; "awaitingSinceVersion", JInt c.AwaitingSinceVersion; "provenance", W.prov c.Provenance ])
              "criteria",
              W.arr s.Criteria (fun c ->
                  JObj [ "id", W.uuid c.Id; "correctiveActionId", W.uuid c.CorrectiveActionId; "expectedOutcome", W.text c.ExpectedOutcome; "method", W.optText c.Method; "provenance", W.prov c.Provenance ])
              "verifications",
              W.arr s.Verifications (fun x ->
                  JObj
                      [ "id", W.uuid x.Id; "criterionId", W.uuid x.CriterionId; "outcome", W.enum Enums.verificationOutcome x.Outcome; "evidenceIds", W.uuids x.EvidenceIds
                        "recordedAtVersion", JInt x.RecordedAtVersion; "provenance", W.prov x.Provenance ])
              "conclusions",
              W.arr s.Conclusions (fun c ->
                  JObj
                      [ "id", W.uuid c.Id; "statement", W.text c.Statement; "strength", W.enum Enums.conclusionStrength c.Strength; "causeIds", W.uuids c.CauseIds
                        "residualUncertainty", W.optText c.ResidualUncertainty; "recordedAtVersion", JInt c.RecordedAtVersion; "provenance", W.prov c.Provenance ])
              "proposals",
              W.arr s.Proposals (fun p ->
                  JObj
                      [ "id", W.uuid p.Id; "kind", W.enum Enums.proposalKind p.Kind; "text", W.text p.ProposalText; "rationale", W.optText p.Rationale
                        "state", W.enum Enums.proposalState p.State; "decision", W.optText p.Decision; "provenance", W.prov p.Provenance ]) ]

    /// The snapshot document §8.1 prescribes for a replayed history (generatedAt = occurredAt of the last event).
    let snapshotJson (lastEvent: HistoricalEnvelope) (s: InvestigationState) : JValue =
        JObj
            [ "formatVersion", JStr "1.0.0"
              "investigationId", W.uuid s.InvestigationId
              "aggregateVersion", JInt s.Version
              "projectionVersion", JInt 1L
              "semanticVersion", JStr "1.0.0"
              "generatedAt", W.ts lastEvent.OccurredAt
              "state", stateJson s ]

    /// Digest of the canonical state object (catalog `expectedStateSha256`, CONTRACT §8.4).
    let stateSha256 (s: InvestigationState) = Sha256.ofText (Json.canonical (stateJson s))
