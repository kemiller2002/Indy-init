/// Pure persistence: a hand-written JSON codec of StoredInvestigation, the SM-SAVE transition function, and a
/// Map-based in-memory store value for tests.
module Exp002.Engine.Persistence

open System.Text.Json
open System.Text.Json.Nodes
open Exp002.Contracts

// ---------- encode ----------

let private str (s: string) : JsonNode = JsonValue.Create s
let private num (n: int64) : JsonNode = JsonValue.Create n
let private optStr (s: string option) : JsonNode = match s with Some v -> str v | None -> null

let private obj (fields: (string * JsonNode) list) : JsonNode =
    let o = JsonObject()
    fields |> List.iter (fun (k, v) -> o.Add(k, v))
    o

let private arr (items: JsonNode list) : JsonNode =
    let a = JsonArray()
    items |> List.iter (fun v -> a.Add v)
    a

let private ts (Timestamp t) = num t

let private details (d: InvestigationDetails) =
    obj [ "problemStatement", str d.ProblemStatement; "title", optStr d.Title; "primaryQuestion", optStr d.PrimaryQuestion ]

let private observation (o: Observation) =
    let (ObservationId id) = o.Id
    obj [ "id", str id; "text", str o.Text; "sourceReference", optStr o.SourceReference ]

let private hypothesis (h: Hypothesis) =
    let (HypothesisId id) = h.Id
    obj [ "id", str id; "statement", str h.Statement; "explanation", str h.Explanation ]

let private evidence (e: Evidence) =
    let (EvidenceId id) = e.Id
    obj
        [ "id", str id
          "label", str e.Label
          "originalContent", str e.OriginalContent
          "sourceReference", optStr e.SourceReference
          "sourceUrl", optStr e.SourceUrl ]

let kindTag (k: RelationshipKind) =
    match k with
    | Supports -> "supports"
    | Contradicts -> "contradicts"
    | ConsistentWith -> "consistent-with"
    | Inconclusive -> "inconclusive"

let private status (s: RelationshipStatus) =
    match s with
    | Current -> obj [ "tag", str "current" ]
    | Revised(RelationshipId by) -> obj [ "tag", str "revised"; "supersededBy", str by ]
    | Withdrawn reason -> obj [ "tag", str "withdrawn"; "reason", str reason ]

let private relationship (r: Relationship) =
    let (RelationshipId id) = r.Id
    let (EvidenceId e) = r.EvidenceId
    let (HypothesisId h) = r.HypothesisId
    obj
        [ "id", str id
          "evidenceId", str e
          "hypothesisId", str h
          "kind", str (kindTag r.Kind)
          "rationale", str r.Rationale
          "status", status r.Status ]

let private event (e: Event) : JsonNode =
    let ev (tag: string) (fields: (string * JsonNode) list) = obj (("type", str tag) :: fields)
    match e with
    | InvestigationStarted(InvestigationId id, d, at) -> ev "InvestigationStarted" [ "id", str id; "details", details d; "at", ts at ]
    | InvestigationDetailsEdited(d, at) -> ev "InvestigationDetailsEdited" [ "details", details d; "at", ts at ]
    | ObservationRecorded(o, at) -> ev "ObservationRecorded" [ "observation", observation o; "at", ts at ]
    | ObservationEdited(o, at) -> ev "ObservationEdited" [ "observation", observation o; "at", ts at ]
    | HypothesisRecorded(h, at) -> ev "HypothesisRecorded" [ "hypothesis", hypothesis h; "at", ts at ]
    | HypothesisEdited(h, at) -> ev "HypothesisEdited" [ "hypothesis", hypothesis h; "at", ts at ]
    | EvidenceAdmitted(x, at) -> ev "EvidenceAdmitted" [ "evidence", evidence x; "at", ts at ]
    | EvidenceEdited(x, at) -> ev "EvidenceEdited" [ "evidence", evidence x; "at", ts at ]
    | RelationshipRecorded(r, at) -> ev "RelationshipRecorded" [ "relationship", relationship r; "at", ts at ]
    | RelationshipRevised(RelationshipId prev, next, at) ->
        ev "RelationshipRevised" [ "previous", str prev; "next", relationship next; "at", ts at ]
    | RelationshipWithdrawn(RelationshipId id, reason, at) ->
        ev "RelationshipWithdrawn" [ "relationshipId", str id; "reason", str reason; "at", ts at ]
    | ConclusionRecorded(c, at) ->
        let (ConclusionId id) = c.Id
        ev
            "ConclusionRecorded"
            [ "conclusion",
              obj
                  [ "id", str id
                    "statement", str c.Statement
                    "basedOn", arr (c.BasedOn |> List.map (fun (RelationshipId r) -> str r)) ]
              "at", ts at ]

let schema = "exp002.investigation/1"

let encode: Encode =
    fun doc ->
        let (InvestigationId id) = doc.Id
        (obj
            [ "schema", str schema
              "id", str id
              "version", num (int64 doc.Version)
              "events", arr (doc.Events |> List.map event) ])
            .ToJsonString()

// ---------- decode ----------

type private R<'a> = Result<'a, string>

type private ResultBuilder() =
    member _.Bind(r: R<'a>, f: 'a -> R<'b>) : R<'b> = Result.bind f r
    member _.Return(v: 'a) : R<'a> = Ok v
    member _.ReturnFrom(r: R<'a>) : R<'a> = r

let private result = ResultBuilder()

let private field (o: JsonObject) (name: string) : R<JsonNode> =
    if o.ContainsKey name then Ok o.[name] else Error(sprintf "missing field '%s'" name)

let private asObject (path: string) (n: JsonNode) : R<JsonObject> =
    match n with
    | :? JsonObject as o -> Ok o
    | _ -> Error(sprintf "'%s' is not an object" path)

let private getString (o: JsonObject) (name: string) : R<string> =
    field o name
    |> Result.bind (fun n ->
        match n with
        | :? JsonValue as v when v.GetValueKind() = JsonValueKind.String -> Ok(v.GetValue<string>())
        | _ -> Error(sprintf "'%s' is not a string" name))

let private getOptString (o: JsonObject) (name: string) : R<string option> =
    field o name
    |> Result.bind (fun n ->
        match n with
        | null -> Ok None
        | :? JsonValue as v when v.GetValueKind() = JsonValueKind.String -> Ok(Some(v.GetValue<string>()))
        | _ -> Error(sprintf "'%s' is not a string or null" name))

let private getInt64 (o: JsonObject) (name: string) : R<int64> =
    field o name
    |> Result.bind (fun n ->
        match n with
        | :? JsonValue as v when v.GetValueKind() = JsonValueKind.Number ->
            match v.TryGetValue<int64>() with
            | true, x -> Ok x
            | _ -> Error(sprintf "'%s' is not an integer" name)
        | _ -> Error(sprintf "'%s' is not a number" name))

let private getObject (o: JsonObject) (name: string) : R<JsonObject> = field o name |> Result.bind (asObject name)

let private getTs o name = getInt64 o name |> Result.map Timestamp

let rec private traverse (f: 'a -> R<'b>) (items: 'a list) : R<'b list> =
    match items with
    | [] -> Ok []
    | x :: rest -> result { let! y = f x in let! ys = traverse f rest in return y :: ys }

let private decodeDetails (o: JsonObject) : R<InvestigationDetails> =
    result {
        let! p = getString o "problemStatement"
        let! t = getOptString o "title"
        let! q = getOptString o "primaryQuestion"
        return { ProblemStatement = p; Title = t; PrimaryQuestion = q }
    }

let private decodeObservation (o: JsonObject) : R<Observation> =
    result {
        let! id = getString o "id"
        let! text = getString o "text"
        let! src = getOptString o "sourceReference"
        return { Id = ObservationId id; Text = text; SourceReference = src }
    }

let private decodeHypothesis (o: JsonObject) : R<Hypothesis> =
    result {
        let! id = getString o "id"
        let! s = getString o "statement"
        let! e = getString o "explanation"
        return { Id = HypothesisId id; Statement = s; Explanation = e }
    }

let private decodeEvidence (o: JsonObject) : R<Evidence> =
    result {
        let! id = getString o "id"
        let! l = getString o "label"
        let! c = getString o "originalContent"
        let! r = getOptString o "sourceReference"
        let! u = getOptString o "sourceUrl"
        return { Id = EvidenceId id; Label = l; OriginalContent = c; SourceReference = r; SourceUrl = u }
    }

let parseKind (tag: string) : R<RelationshipKind> =
    match tag with
    | "supports" -> Ok Supports
    | "contradicts" -> Ok Contradicts
    | "consistent-with" -> Ok ConsistentWith
    | "inconclusive" -> Ok Inconclusive
    | other -> Error(sprintf "unknown relationship kind '%s'" other)

let private decodeStatus (o: JsonObject) : R<RelationshipStatus> =
    result {
        let! tag = getString o "tag"
        match tag with
        | "current" -> return Current
        | "revised" ->
            let! by = getString o "supersededBy"
            return Revised(RelationshipId by)
        | "withdrawn" ->
            let! reason = getString o "reason"
            return Withdrawn reason
        | other -> return! Error(sprintf "unknown relationship status '%s'" other)
    }

let private decodeRelationship (o: JsonObject) : R<Relationship> =
    result {
        let! id = getString o "id"
        let! e = getString o "evidenceId"
        let! h = getString o "hypothesisId"
        let! k = getString o "kind" |> Result.bind parseKind
        let! r = getString o "rationale"
        let! s = getObject o "status" |> Result.bind decodeStatus
        return { Id = RelationshipId id; EvidenceId = EvidenceId e; HypothesisId = HypothesisId h; Kind = k; Rationale = r; Status = s }
    }

let private decodeEvent (n: JsonNode) : R<Event> =
    result {
        let! o = asObject "event" n
        let! tag = getString o "type"
        let! at = getTs o "at"
        match tag with
        | "InvestigationStarted" ->
            let! id = getString o "id"
            let! d = getObject o "details" |> Result.bind decodeDetails
            return InvestigationStarted(InvestigationId id, d, at)
        | "InvestigationDetailsEdited" ->
            let! d = getObject o "details" |> Result.bind decodeDetails
            return InvestigationDetailsEdited(d, at)
        | "ObservationRecorded" ->
            let! x = getObject o "observation" |> Result.bind decodeObservation
            return ObservationRecorded(x, at)
        | "ObservationEdited" ->
            let! x = getObject o "observation" |> Result.bind decodeObservation
            return ObservationEdited(x, at)
        | "HypothesisRecorded" ->
            let! x = getObject o "hypothesis" |> Result.bind decodeHypothesis
            return HypothesisRecorded(x, at)
        | "HypothesisEdited" ->
            let! x = getObject o "hypothesis" |> Result.bind decodeHypothesis
            return HypothesisEdited(x, at)
        | "EvidenceAdmitted" ->
            let! x = getObject o "evidence" |> Result.bind decodeEvidence
            return EvidenceAdmitted(x, at)
        | "EvidenceEdited" ->
            let! x = getObject o "evidence" |> Result.bind decodeEvidence
            return EvidenceEdited(x, at)
        | "RelationshipRecorded" ->
            let! x = getObject o "relationship" |> Result.bind decodeRelationship
            return RelationshipRecorded(x, at)
        | "RelationshipRevised" ->
            let! prev = getString o "previous"
            let! next = getObject o "next" |> Result.bind decodeRelationship
            return RelationshipRevised(RelationshipId prev, next, at)
        | "RelationshipWithdrawn" ->
            let! id = getString o "relationshipId"
            let! reason = getString o "reason"
            return RelationshipWithdrawn(RelationshipId id, reason, at)
        | "ConclusionRecorded" ->
            let! c = getObject o "conclusion"
            let! id = getString c "id"
            let! statement = getString c "statement"
            let! basedOn =
                field c "basedOn"
                |> Result.bind (function
                    | :? JsonArray as a ->
                        a
                        |> Seq.toList
                        |> traverse (function
                            | :? JsonValue as v when v.GetValueKind() = JsonValueKind.String -> Ok(RelationshipId(v.GetValue<string>()))
                            | _ -> Error "'basedOn' item is not a string")
                    | _ -> Error "'basedOn' is not an array")
            return ConclusionRecorded({ Id = ConclusionId id; Statement = statement; BasedOn = basedOn }, at)
        | other -> return! Error(sprintf "unknown event type '%s'" other)
    }

let private parse (text: string) : R<JsonNode> =
    try
        match JsonNode.Parse text with
        | null -> Error "document is null"
        | n -> Ok n
    with ex ->
        Error(sprintf "invalid JSON: %s" ex.Message)

let decode: Decode =
    fun text ->
        result {
            let! root = (if isNull text then Error "document is null" else parse text) |> Result.bind (asObject "document")
            let! s = getString root "schema"
            do! (if s = schema then Ok() else Error(sprintf "unsupported schema '%s'" s))
            let! id = getString root "id"
            let! version = getInt64 root "version"
            let! events =
                field root "events"
                |> Result.bind (function
                    | :? JsonArray as a -> traverse decodeEvent (a |> Seq.toList)
                    | _ -> Error "'events' is not an array")
            return { Id = InvestigationId id; Version = int version; Events = events }
        }
        |> Result.mapError Corrupt

// ---------- SM-SAVE ----------

let describe (e: PersistenceError) =
    match e with
    | NotFound(InvestigationId id) -> sprintf "Investigation %s was not found." id
    | VersionConflict(expected, actual) -> sprintf "Version conflict: expected %d, found %d." expected actual
    | Corrupt detail -> sprintf "Stored data is corrupt: %s" detail
    | StorageUnavailable detail -> sprintf "Storage unavailable: %s" detail

/// SM-SAVE. Total: an event that does not apply in a state leaves the state unchanged.
/// - Edited: Conflict and Offline are kept (the user must resolve them); every other state, including Saving,
///   becomes Unsaved (a write in flight no longer covers the latest edits; its later outcome is then ignored).
/// - SaveRequested: Unsaved | SaveFailed | UnknownOutcome -> Saving.
/// - WriteConfirmed(v, t) while Saving stays Saving: the read-back is still pending (CR-3).
/// - ReadBackMatched(v, t) while Saving -> Saved(v, t); Saved is never entered before the read-back.
/// - ReadBackMismatched while Saving -> SaveFailed.
/// - WriteRejected(VersionConflict(_, actual)) while Saving -> Conflict actual; other rejection -> SaveFailed reason.
/// - WriteOutcomeUnknown while Saving -> UnknownOutcome.
let saveTransition: SaveTransition =
    fun state event ->
        match state, event with
        | (Conflict _ | Offline), Edited -> state
        | _, Edited -> Unsaved
        | (Unsaved | SaveFailed _ | UnknownOutcome), SaveRequested -> Saving
        | Saving, WriteConfirmed _ -> Saving
        | Saving, ReadBackMatched(v, at) -> Saved(v, at)
        | Saving, ReadBackMismatched -> SaveFailed "The saved data did not match on read-back."
        | Saving, WriteRejected(VersionConflict(_, actual)) -> Conflict actual
        | Saving, WriteRejected e -> SaveFailed(describe e)
        | Saving, WriteOutcomeUnknown -> UnknownOutcome
        | _ -> state

// ---------- in-memory store (pure value) ----------

/// Encoded documents by investigation ID; every operation returns a new store value.
type MemoryStore = MemoryStore of Map<InvestigationId, string>

let emptyStore = MemoryStore Map.empty

let currentVersion (MemoryStore m) (id: InvestigationId) : Result<int, PersistenceError> =
    match Map.tryFind id m with
    | None -> Ok 0
    | Some text -> decode text |> Result.map (fun d -> d.Version)

/// Optimistic concurrency: the stored version (0 when absent) must equal expectedVersion; the document is written
/// at expectedVersion + 1. Returns the new store and the written version.
let save (expectedVersion: int) (doc: StoredInvestigation) (MemoryStore m as store) : Result<MemoryStore * int, PersistenceError> =
    currentVersion store doc.Id
    |> Result.bind (fun actual ->
        if actual <> expectedVersion then
            Error(VersionConflict(expectedVersion, actual))
        else
            let written = { doc with Version = expectedVersion + 1 }
            Ok(MemoryStore(Map.add doc.Id (encode written) m), written.Version))

let load (id: InvestigationId) (MemoryStore m) : Result<StoredInvestigation, PersistenceError> =
    match Map.tryFind id m with
    | None -> Error(NotFound id)
    | Some text -> decode text

/// Raw write for tests (e.g. to plant a corrupt document).
let putRaw (id: InvestigationId) (text: string) (MemoryStore m) = MemoryStore(Map.add id text m)
