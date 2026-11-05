namespace Causa.HistoricalCompatibility

open System

/// The reader check stages of CONTRACT §11.1, in order, plus the successor rule of §9.3.
type ReaderCode =
    | PathInvalid
    | Encoding
    | JsonMalformed
    | NonCanonical
    | UnsupportedVersion
    | UnknownEventType
    | SchemaViolation
    | ManifestMismatch
    | EventsHashMismatch
    | InvestigationMismatch
    | EventIdDuplicate
    | SequenceInvalid
    | EventIdMismatch
    | CorrelationReused
    | HistoryInadmissible
    | SnapshotHashMismatch
    | SnapshotMismatch
    | HistoryRewritten

module ReaderCode =
    let all =
        [ PathInvalid; Encoding; JsonMalformed; NonCanonical; UnsupportedVersion; UnknownEventType; SchemaViolation; ManifestMismatch
          EventsHashMismatch; InvestigationMismatch; EventIdDuplicate; SequenceInvalid; EventIdMismatch; CorrelationReused
          HistoryInadmissible; SnapshotHashMismatch; SnapshotMismatch; HistoryRewritten ]

    let code =
        function
        | PathInvalid -> "CAUSA.PATH_INVALID"
        | Encoding -> "CAUSA.ENCODING"
        | JsonMalformed -> "CAUSA.JSON_MALFORMED"
        | NonCanonical -> "CAUSA.NON_CANONICAL"
        | UnsupportedVersion -> "CAUSA.UNSUPPORTED_VERSION"
        | UnknownEventType -> "CAUSA.UNKNOWN_EVENT_TYPE"
        | SchemaViolation -> "CAUSA.SCHEMA_VIOLATION"
        | ManifestMismatch -> "CAUSA.MANIFEST_MISMATCH"
        | EventsHashMismatch -> "CAUSA.EVENTS_HASH_MISMATCH"
        | InvestigationMismatch -> "CAUSA.INVESTIGATION_MISMATCH"
        | EventIdDuplicate -> "CAUSA.EVENT_ID_DUPLICATE"
        | SequenceInvalid -> "CAUSA.SEQUENCE_INVALID"
        | EventIdMismatch -> "CAUSA.EVENT_ID_MISMATCH"
        | CorrelationReused -> "CAUSA.CORRELATION_REUSED"
        | HistoryInadmissible -> "CAUSA.HISTORY_INADMISSIBLE"
        | SnapshotHashMismatch -> "CAUSA.SNAPSHOT_HASH_MISMATCH"
        | SnapshotMismatch -> "CAUSA.SNAPSHOT_MISMATCH"
        | HistoryRewritten -> "CAUSA.HISTORY_REWRITTEN"

    /// Stage number of CONTRACT §11.1 (HistoryRewritten is the §9.3 successor rule, after every reader stage).
    let stage c = (all |> List.findIndex ((=) c)) + 1

/// A reader failure: always explicit, never a partial load (CONTRACT §11.2).
type ReaderError =
    { Code: ReaderCode
      File: string
      Line: int option
      Detail: string }

module ReaderError =
    let create code file line detail = { Code = code; File = file; Line = line; Detail = detail }

    let describe (e: ReaderError) =
        let at = match e.Line with Some l -> $"{e.File}:{l}" | None -> e.File
        $"{ReaderCode.code e.Code} at {at}: {e.Detail}"

type ActorKind =
    | HumanActor
    | SystemActor
    | AgentActor of provider: Text * model: Text

type Actor = { ActorId: Text; Kind: ActorKind }

/// One line of events.ndjson after stages 2-7 of the envelope (CONTRACT §5). Every envelope field is kept, and the
/// exact canonical line text is kept as `RawLine`, so the source record is always recoverable byte for byte.
type HistoricalEnvelope =
    { Line: int
      RawLine: string
      FormatVersion: string
      EventId: Uuid
      InvestigationId: Uuid
      AggregateVersion: int64
      EventType: string
      OccurredAt: Timestamp
      Actor: Actor
      SemanticVersion: string
      CorrelationId: Uuid
      Payload: (string * JValue) list }

/// Field readers over a closed JSON object. Each returns a plain detail message; the caller attaches file, line and
/// stage (SCHEMA_VIOLATION) so every failure names exactly where it occurred.
module Field =
    let private get (o: (string * JValue) list) k =
        match Json.tryField k o with
        | Some v -> Ok v
        | None -> Error $"missing field '{k}'"

    let str o k = get o k |> Result.bind (function JStr s -> Ok s | _ -> Error $"field '{k}' must be a string")
    let int o k = get o k |> Result.bind (function JInt n -> Ok n | _ -> Error $"field '{k}' must be an integer")
    let obj o k = get o k |> Result.bind (function JObj m -> Ok m | _ -> Error $"field '{k}' must be an object")
    let arr o k = get o k |> Result.bind (function JArr a -> Ok a | _ -> Error $"field '{k}' must be an array")

    let private uuidOf k s = Uuid.tryParse s |> Option.map Ok |> Option.defaultValue (Error $"field '{k}' must be a canonical lowercase non-nil UUID")
    let private textOf k s = Text.tryCreate s |> Option.map Ok |> Option.defaultValue (Error $"field '{k}' must contain a non-whitespace character")
    let private tsOf k s = Timestamp.tryParse s |> Option.map Ok |> Option.defaultValue (Error $"field '{k}' must be a calendar-valid UTC millisecond timestamp")

    let uuid o k = str o k |> Result.bind (uuidOf k)
    let text o k = str o k |> Result.bind (textOf k)
    let timestamp o k = str o k |> Result.bind (tsOf k)

    let private nullable o k (f: string -> string -> Result<'T, string>) =
        get o k
        |> Result.bind (function
            | JNull -> Ok None
            | JStr s -> f k s |> Result.map Some
            | _ -> Error $"field '{k}' must be a string or null")

    let optText o k = nullable o k textOf
    let optTimestamp o k = nullable o k tsOf

    let enum (e: WireEnum<'T>) o k =
        str o k
        |> Result.bind (fun s ->
            match WireEnum.tryParse e s with
            | Some v -> Ok v
            | None -> Error $"field '{k}' has value '{s}' outside the {e.Name} enumeration")

    /// An enumeration value restricted to a target subset (CONTRACT §6 `*Target`).
    let target (e: WireEnum<'T>) (allowed: 'T list) o k =
        enum e o k
        |> Result.bind (fun v -> if List.contains v allowed then Ok v else Error $"field '{k}' value '{WireEnum.toWire e v}' is not a {e.Name} transition target")

    let positive o k =
        int o k |> Result.bind (fun n -> if n >= 1L then Ok n else Error $"field '{k}' must be at least 1")

    /// Ordered, distinct UUID arrays (CONTRACT §2.7). Order is preserved exactly.
    let uuids o k =
        arr o k
        |> Result.bind (fun items ->
            items
            |> List.fold
                (fun acc v ->
                    acc
                    |> Result.bind (fun xs ->
                        match v with
                        | JStr s -> uuidOf k s |> Result.map (fun u -> u :: xs)
                        | _ -> Error $"field '{k}' must contain UUID strings"))
                (Ok [])
            |> Result.map List.rev)
        |> Result.bind (fun xs -> if List.length (List.distinct xs) = List.length xs then Ok xs else Error $"field '{k}' contains a duplicate identifier")

    let nonEmptyUuids o k =
        uuids o k |> Result.bind (fun xs -> if List.isEmpty xs then Error $"field '{k}' must contain at least one identifier" else Ok xs)

    /// Closed structures (CONTRACT §2.4): exactly these keys, no unknown field silently dropped.
    let exactKeys (what: string) (wanted: string list) (o: (string * JValue) list) : Result<unit, string> =
        let present = Json.keys o
        let wantedSet = Set.ofList wanted
        let extra = Set.difference present wantedSet
        let missing = Set.difference wantedSet present
        if not extra.IsEmpty then Error $"""{what} has undeclared field(s): {String.Join(", ", extra)}"""
        elif not missing.IsEmpty then Error $"""{what} is missing field(s): {String.Join(", ", missing)}"""
        else Ok()

/// Small Result computation expression, used to keep the decoders declarative.
type ResultBuilder() =
    member _.Bind(r: Result<'T, 'E>, f: 'T -> Result<'U, 'E>) = Result.bind f r
    member _.Return(v: 'T) : Result<'T, 'E> = Ok v
    member _.ReturnFrom(r: Result<'T, 'E>) = r
    member _.Zero() : Result<unit, 'E> = Ok()

[<AutoOpen>]
module ResultSyntax =
    let result = ResultBuilder()

    /// Applies f to every element in order and stops at the first error.
    let traverse (f: 'T -> Result<'U, 'E>) (xs: 'T list) : Result<'U list, 'E> =
        xs
        |> List.fold (fun acc x -> acc |> Result.bind (fun ys -> f x |> Result.map (fun y -> y :: ys))) (Ok [])
        |> Result.map List.rev

module Envelope =
    let keys =
        [ "formatVersion"; "eventId"; "investigationId"; "aggregateVersion"; "eventType"; "occurredAt"; "actor"; "semanticVersion"; "correlationId"; "payload" ]

    let decodeActor (o: (string * JValue) list) : Result<Actor, string> =
        result {
            let! kind = Field.str o "kind"
            match kind with
            | "human"
            | "system" ->
                do! Field.exactKeys "actor" [ "actorId"; "kind" ] o
                let! id = Field.text o "actorId"
                return { ActorId = id; Kind = (if kind = "human" then HumanActor else SystemActor) }
            | "agent" ->
                do! Field.exactKeys "actor" [ "actorId"; "kind"; "provider"; "model" ] o
                let! id = Field.text o "actorId"
                let! provider = Field.text o "provider"
                let! model = Field.text o "model"
                return { ActorId = id; Kind = AgentActor(provider, model) }
            | other -> return! Error $"actor kind '{other}' is not registered"
        }

    /// Stage 7 for the envelope (payload is decoded separately, by event type). `rawLine` is the canonical line.
    let decode (line: int) (rawLine: string) (o: (string * JValue) list) : Result<HistoricalEnvelope, string> =
        result {
            do! Field.exactKeys "event envelope" keys o
            let! formatVersion = Field.str o "formatVersion"
            let! eventId = Field.uuid o "eventId"
            let! investigationId = Field.uuid o "investigationId"
            let! aggregateVersion = Field.positive o "aggregateVersion"
            let! eventType = Field.str o "eventType"
            let! occurredAt = Field.timestamp o "occurredAt"
            let! actorObj = Field.obj o "actor"
            let! actor = decodeActor actorObj
            let! semanticVersion = Field.str o "semanticVersion"
            let! correlationId = Field.uuid o "correlationId"
            let! payload = Field.obj o "payload"
            if formatVersion <> "1.0.0" || semanticVersion <> "1.0.0" then return! Error "unsupported version"
            elif eventType = "InvestigationStarted" && aggregateVersion <> 1L then return! Error "InvestigationStarted must be aggregateVersion 1"
            else
                match actor.Kind with
                | AgentActor _ when eventType <> "ProposalRecorded" -> return! Error "an agent actor may only record ProposalRecorded (D-004)"
                | _ ->
                    return
                        { Line = line
                          RawLine = rawLine
                          FormatVersion = formatVersion
                          EventId = eventId
                          InvestigationId = investigationId
                          AggregateVersion = aggregateVersion
                          EventType = eventType
                          OccurredAt = occurredAt
                          Actor = actor
                          SemanticVersion = semanticVersion
                          CorrelationId = correlationId
                          Payload = payload }
        }
