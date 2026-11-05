/// Pure decide/evolve/replay over the investigation event log.
/// New entity IDs are deterministic from state: "O-n", "H-n", "E-n", "R-n" with n = count of existing + 1.
module Exp002.Engine.Domain

open Exp002.Contracts

let private invalid (r: Result<'a, FieldError list>) : Result<'a, DomainError> = Result.mapError Invalid r

let private appendHistory (inv: Investigation) (e: Event) : Investigation =
    { inv with History = inv.History @ [ { Sequence = List.length inv.History + 1; Event = e } ] }

let private replaceWhere (matches: 'a -> bool) (value: 'a) (items: 'a list) : 'a list =
    items |> List.map (fun x -> if matches x then value else x)

let private setStatus (id: RelationshipId) (status: RelationshipStatus) (rels: Relationship list) =
    rels |> List.map (fun r -> if r.Id = id then { r with Status = status } else r)

/// Applies an event's effect on the details and entity lists (history is appended separately).
let private apply (inv: Investigation) (event: Event) : Investigation =
    match event with
    | InvestigationStarted _ -> inv
    | InvestigationDetailsEdited(d, _) -> { inv with Details = d }
    | ObservationRecorded(o, _) -> { inv with Observations = inv.Observations @ [ o ] }
    | ObservationEdited(o, _) -> { inv with Observations = replaceWhere (fun x -> x.Id = o.Id) o inv.Observations }
    | HypothesisRecorded(h, _) -> { inv with Hypotheses = inv.Hypotheses @ [ h ] }
    | HypothesisEdited(h, _) -> { inv with Hypotheses = replaceWhere (fun x -> x.Id = h.Id) h inv.Hypotheses }
    | EvidenceAdmitted(e, _) -> { inv with Evidence = inv.Evidence @ [ e ] }
    | EvidenceEdited(e, _) -> { inv with Evidence = replaceWhere (fun x -> x.Id = e.Id) e inv.Evidence }
    | RelationshipRecorded(r, _) -> { inv with Relationships = inv.Relationships @ [ r ] }
    | RelationshipRevised(previous, next, _) ->
        { inv with Relationships = (setStatus previous (Revised next.Id) inv.Relationships) @ [ next ] }
    | RelationshipWithdrawn(id, reason, _) -> { inv with Relationships = setStatus id (Withdrawn reason) inv.Relationships }

/// Folds one event. Non-start events are ignored when there is no investigation (decide never emits them);
/// a second start is ignored.
let evolve: Evolve =
    fun state event ->
        match state, event with
        | None, InvestigationStarted(id, details, at) ->
            Some
                { Id = id
                  CreatedAt = at
                  State = Open
                  Details = details
                  Observations = []
                  Hypotheses = []
                  Evidence = []
                  Relationships = []
                  History = [ { Sequence = 1; Event = event } ] }
        | Some _, InvestigationStarted _ -> state
        | None, _ -> None
        | Some inv, _ -> Some(appendHistory (apply inv event) event)

let replay: Replay = fun events -> List.fold evolve None events

let private require (found: bool) (error: DomainError) : Result<unit, DomainError> = if found then Ok() else Error error

let private observation id (text, source) : Observation = { Id = id; Text = text; SourceReference = source }

let private hypothesis id (statement, explanation) : Hypothesis =
    { Id = id; Statement = statement; Explanation = explanation }

let private evidence id input : Result<Evidence, DomainError> =
    Validation.evidenceFields input
    |> invalid
    |> Result.map (fun (l, c, r, u) ->
        { Id = id
          Label = l
          OriginalContent = c
          SourceReference = r
          SourceUrl = u })

let private nextRelationshipId (inv: Investigation) = RelationshipId(sprintf "R-%d" (inv.Relationships.Length + 1))

let private findRelationship (inv: Investigation) (id: RelationshipId) : Result<Relationship, DomainError> =
    match inv.Relationships |> List.tryFind (fun r -> r.Id = id) with
    | None -> Error(UnknownRelationship id)
    | Some r when r.Status <> Current -> Error(RelationshipNotCurrent id)
    | Some r -> Ok r

let private link (inv: Investigation) (at: Timestamp) (input: RelationshipInput) : Result<Event list, DomainError> =
    let eid = EvidenceId input.EvidenceId
    let hid = HypothesisId input.HypothesisId
    Validation.validateRelationship input
    |> invalid
    |> Result.bind (fun valid ->
        require (inv.Evidence |> List.exists (fun e -> e.Id = eid)) (UnknownEvidence eid)
        |> Result.bind (fun () -> require (inv.Hypotheses |> List.exists (fun h -> h.Id = hid)) (UnknownHypothesis hid))
        |> Result.map (fun () -> valid))
    |> Result.bind (fun (kind, rationale) ->
        match inv.Relationships |> List.tryFind (fun r -> r.EvidenceId = eid && r.HypothesisId = hid && r.Status = Current) with
        | Some existing -> Error(DuplicateRelationship existing.Id)
        | None ->
            Ok
                [ RelationshipRecorded(
                      { Id = nextRelationshipId inv
                        EvidenceId = eid
                        HypothesisId = hid
                        Kind = kind
                        Rationale = rationale
                        Status = Current },
                      at
                  ) ])

let private decideOn (inv: Investigation) (at: Timestamp) (command: Command) : Result<Event list, DomainError> =
    match command with
    | StartInvestigation _ -> Error AlreadyStarted
    | EditInvestigationDetails input ->
        Validation.validateDetails input |> invalid |> Result.map (fun d -> [ InvestigationDetailsEdited(d, at) ])
    | RecordObservation input ->
        let id = ObservationId(sprintf "O-%d" (inv.Observations.Length + 1))
        Validation.validateObservation input
        |> invalid
        |> Result.map (fun v -> [ ObservationRecorded(observation id v, at) ])
    | EditObservation(id, input) ->
        require (inv.Observations |> List.exists (fun o -> o.Id = id)) (UnknownObservation id)
        |> Result.bind (fun () -> Validation.validateObservation input |> invalid)
        |> Result.map (fun v -> [ ObservationEdited(observation id v, at) ])
    | RecordHypothesis input ->
        let id = HypothesisId(sprintf "H-%d" (inv.Hypotheses.Length + 1))
        Validation.validateHypothesis input
        |> invalid
        |> Result.map (fun v -> [ HypothesisRecorded(hypothesis id v, at) ])
    | EditHypothesis(id, input) ->
        require (inv.Hypotheses |> List.exists (fun h -> h.Id = id)) (UnknownHypothesis id)
        |> Result.bind (fun () -> Validation.validateHypothesis input |> invalid)
        |> Result.map (fun v -> [ HypothesisEdited(hypothesis id v, at) ])
    | AdmitEvidence input ->
        let id = EvidenceId(sprintf "E-%d" (inv.Evidence.Length + 1))
        evidence id input |> Result.map (fun e -> [ EvidenceAdmitted(e, at) ])
    | EditEvidence(id, input) ->
        require (inv.Evidence |> List.exists (fun e -> e.Id = id)) (UnknownEvidence id)
        |> Result.bind (fun () -> evidence id input)
        |> Result.map (fun e -> [ EvidenceEdited(e, at) ])
    | LinkEvidence input -> link inv at input
    | ReviseRelationship(id, input) ->
        findRelationship inv id
        |> Result.bind (fun old ->
            Validation.validateRevision input
            |> invalid
            |> Result.map (fun (kind, rationale) ->
                [ RelationshipRevised(
                      id,
                      { old with
                          Id = nextRelationshipId inv
                          Kind = kind
                          Rationale = rationale
                          Status = Current },
                      at
                  ) ]))
    | WithdrawRelationship(id, reason) ->
        findRelationship inv id
        |> Result.bind (fun _ -> Validation.rationale reason |> invalid)
        |> Result.map (fun r -> [ RelationshipWithdrawn(id, r, at) ])

/// Total and pure: start needs no investigation; every other command needs one.
let decide: Decide =
    fun state at command ->
        match state, command with
        | None, StartInvestigation input ->
            Validation.validateStart input
            |> invalid
            |> Result.map (fun (id, details) -> [ InvestigationStarted(id, details, at) ])
        | None, _ -> Error NotStarted
        | Some inv, _ -> decideOn inv at command
