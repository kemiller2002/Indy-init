/// TEMPORARY: never counts toward integration or acceptance.
/// Minimal fake EnginePorts so the UI can compile, run and be tested before the domain agent's Exp002.Engine exists.
/// Integration replaces Stub.ports with Exp002.Engine.Engine.ports in src/Exp002.Wasm/Program.cs (one line).
module Exp002.Ui.Stub

open Exp002.Contracts

let private blank (s: string) = System.String.IsNullOrWhiteSpace s
let private opt (s: string) = if blank s then None else Some(s.Trim())
let private err f m = { FieldId = f; Message = m }

let private validateStart (i: StartInvestigationInput) =
    let errors =
        [ if blank i.InvestigationId then err "INV-006" "Enter an identifier."
          if blank i.ProblemStatement then err "INV-001" "Enter a problem statement."
          elif i.ProblemStatement.Length > 10000 then err "INV-001" "Use 10000 characters or fewer."
          if i.Title.Length > 200 then err "INV-002" "Use 200 characters or fewer." ]
    if errors.IsEmpty then
        Ok(InvestigationId(i.InvestigationId.Trim()),
           { ProblemStatement = i.ProblemStatement.Trim(); Title = opt i.Title; PrimaryQuestion = opt i.PrimaryQuestion })
    else Error errors

let private evolve (inv: Investigation option) (e: Event) : Investigation option =
    let add (i: Investigation) = { i with History = i.History @ [ { Sequence = i.History.Length + 1; Event = e } ] }
    let replace pick id x xs = xs |> List.map (fun y -> if pick y = id then x else y)
    match inv, e with
    | None, InvestigationStarted(id, d, at) ->
        Some(add { Id = id; CreatedAt = at; State = Open; Details = d; Observations = []; Hypotheses = []
                   Evidence = []; Relationships = []; History = [] })
    | Some i, InvestigationDetailsEdited(d, _) -> Some(add { i with Details = d })
    | Some i, ObservationRecorded(o, _) -> Some(add { i with Observations = i.Observations @ [ o ] })
    | Some i, ObservationEdited(o, _) -> Some(add { i with Observations = replace (fun (x: Observation) -> x.Id) o.Id o i.Observations })
    | Some i, HypothesisRecorded(h, _) -> Some(add { i with Hypotheses = i.Hypotheses @ [ h ] })
    | Some i, HypothesisEdited(h, _) -> Some(add { i with Hypotheses = replace (fun (x: Hypothesis) -> x.Id) h.Id h i.Hypotheses })
    | Some i, EvidenceAdmitted(v, _) -> Some(add { i with Evidence = i.Evidence @ [ v ] })
    | Some i, EvidenceEdited(v, _) -> Some(add { i with Evidence = replace (fun (x: Evidence) -> x.Id) v.Id v i.Evidence })
    | Some i, RelationshipRecorded(r, _) -> Some(add { i with Relationships = i.Relationships @ [ r ] })
    | Some i, RelationshipRevised(prev, next, _) ->
        let rs = i.Relationships |> List.map (fun r -> if r.Id = prev then { r with Status = Revised next.Id } else r)
        Some(add { i with Relationships = rs @ [ next ] })
    | Some i, RelationshipWithdrawn(rid, reason, _) ->
        Some(add { i with Relationships = i.Relationships |> List.map (fun r -> if r.Id = rid then { r with Status = Withdrawn reason } else r) })
    | other, _ -> other

let private replay (events: Event list) = events |> List.fold evolve None

let private decide (inv: Investigation option) (at: Timestamp) (cmd: Command) : Result<Event list, DomainError> =
    let need f m = if blank f then [ err m "Required." ] else []
    let ok (es: Event list) = Ok es
    match inv, cmd with
    | None, StartInvestigation i ->
        match validateStart i with
        | Ok(id, d) -> ok [ InvestigationStarted(id, d, at) ]
        | Error e -> Error(Invalid e)
    | Some _, StartInvestigation _ -> Error AlreadyStarted
    | None, _ -> Error NotStarted
    | Some i, cmd ->
        match cmd with
        | StartInvestigation _ -> Error AlreadyStarted
        | EditInvestigationDetails d ->
            match need d.ProblemStatement "INV-001" with
            | [] -> ok [ InvestigationDetailsEdited({ ProblemStatement = d.ProblemStatement.Trim(); Title = opt d.Title; PrimaryQuestion = opt d.PrimaryQuestion }, at) ]
            | e -> Error(Invalid e)
        | RecordObservation o ->
            match need o.ObservationText "INV-024" with
            | [] -> ok [ ObservationRecorded({ Id = ObservationId(sprintf "obs-%d" (i.Observations.Length + 1)); Text = o.ObservationText; SourceReference = opt o.SourceReference }, at) ]
            | e -> Error(Invalid e)
        | EditObservation(id, o) ->
            if i.Observations |> List.exists (fun x -> x.Id = id) |> not then Error(UnknownObservation id)
            else ok [ ObservationEdited({ Id = id; Text = o.ObservationText; SourceReference = opt o.SourceReference }, at) ]
        | RecordHypothesis h ->
            match need h.Statement "INV-026" with
            | [] -> ok [ HypothesisRecorded({ Id = HypothesisId(sprintf "hyp-%d" (i.Hypotheses.Length + 1)); Statement = h.Statement; Explanation = h.Explanation }, at) ]
            | e -> Error(Invalid e)
        | EditHypothesis(id, h) -> ok [ HypothesisEdited({ Id = id; Statement = h.Statement; Explanation = h.Explanation }, at) ]
        | AdmitEvidence v ->
            match need v.Label "INV-027" @ need v.OriginalContent "INV-012" with
            | [] -> ok [ EvidenceAdmitted({ Id = EvidenceId(sprintf "ev-%d" (i.Evidence.Length + 1)); Label = v.Label; OriginalContent = v.OriginalContent; SourceReference = opt v.SourceReference; SourceUrl = opt v.SourceUrl }, at) ]
            | e -> Error(Invalid e)
        | EditEvidence(id, v) -> ok [ EvidenceEdited({ Id = id; Label = v.Label; OriginalContent = v.OriginalContent; SourceReference = opt v.SourceReference; SourceUrl = opt v.SourceUrl }, at) ]
        | LinkEvidence r ->
            match r.Kind with
            | None -> Error(Invalid [ err "INV-031" "Choose a relationship." ])
            | Some k ->
                ok [ RelationshipRecorded({ Id = RelationshipId(sprintf "rel-%d" (i.Relationships.Length + 1)); EvidenceId = EvidenceId r.EvidenceId
                                            HypothesisId = HypothesisId r.HypothesisId; Kind = k; Rationale = r.Rationale; Status = Current }, at) ]
        | ReviseRelationship(rid, r) ->
            match i.Relationships |> List.tryFind (fun x -> x.Id = rid), r.Kind with
            | None, _ -> Error(UnknownRelationship rid)
            | _, None -> Error(Invalid [ err "INV-031" "Choose a relationship." ])
            | Some old, Some k ->
                ok [ RelationshipRevised(rid, { old with Id = RelationshipId(sprintf "rel-%d" (i.Relationships.Length + 1)); Kind = k; Rationale = r.Rationale; Status = Current }, at) ]
        | WithdrawRelationship(rid, reason) -> ok [ RelationshipWithdrawn(rid, reason, at) ]

/// The stub codec keeps documents in a process-local table keyed by a deterministic token (fake, session-only).
let private table = System.Collections.Concurrent.ConcurrentDictionary<string, StoredInvestigation>()

let private encode (s: StoredInvestigation) =
    let (InvestigationId id) = s.Id
    let token = sprintf "stub:%s:%d:%d" id s.Version s.Events.Length
    table[token] <- s
    token

let private decode (text: string) =
    match table.TryGetValue text with
    | true, s -> Ok s
    | _ -> Error(Corrupt "stub codec: unknown document")

let private saveTransition (state: SaveState) (e: SaveEvent) =
    match e with
    | Edited -> Unsaved
    | SaveRequested -> Saving
    | WriteConfirmed(v, at) -> Saved(v, at)
    | ReadBackMatched -> state
    | ReadBackMismatched -> UnknownOutcome
    | WriteRejected(VersionConflict(_, actual)) -> Conflict actual
    | WriteRejected e -> SaveFailed(sprintf "%A" e)
    | WriteOutcomeUnknown -> UnknownOutcome

let ports: EnginePorts =
    { ValidateStart = validateStart
      Decide = decide
      Replay = replay
      Encode = encode
      Decode = decode
      SaveTransition = saveTransition }
