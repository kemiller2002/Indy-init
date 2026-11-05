namespace Causa.HistoricalCompatibility

open System
open System.Text

/// Raw document retrieval output: one investigation directory as named entries and exact bytes. Provider-neutral:
/// it carries no Git SHA, branch, URL or token (CONTRACT §1.4, §9.4).
type InvestigationFiles =
    { DirectoryName: string
      Entries: (string * byte[]) list }

type InvestigationManifest =
    { InvestigationId: Uuid
      AggregateVersion: int64
      EventCount: int64
      EventsSha256: string
      SnapshotSha256: string
      SnapshotVersion: int64
      CreatedAt: Timestamp
      UpdatedAt: Timestamp
      FormatVersion: string
      SchemaVersion: string
      SemanticVersion: string }

type SnapshotDocument =
    { InvestigationId: Uuid
      AggregateVersion: int64
      ProjectionVersion: int64
      GeneratedAt: Timestamp
      State: JValue }

/// Stages 1-14 passed: the history is structurally valid and fully, typedly decoded. Admissibility (stage 15) is not
/// part of this type; see ReplayAdapter / HistoricalLoader.
type StructuralRead =
    { DirectoryName: string
      Manifest: InvestigationManifest
      ManifestText: string
      EventsBytes: byte[]
      Events: DecodedEvent list
      Snapshot: SnapshotDocument
      SnapshotText: string }

module StorageReader =
    let fileNames = [ "manifest.json"; "events.ndjson"; "snapshot.json" ]

    let private err code file line detail : Result<'T, ReaderError> = Error(ReaderError.create code file line detail)

    let private strictUtf8 = UTF8Encoding(false, true)

    /// Stage 1 (CONTRACT §3.2-3.3).
    let checkPath (files: InvestigationFiles) : Result<unit, ReaderError> =
        let names = files.Entries |> List.map fst
        if (Uuid.tryParse files.DirectoryName).IsNone then err PathInvalid files.DirectoryName None "directory name is not a canonical lowercase UUID"
        elif List.length (List.distinct names) <> List.length names then err PathInvalid files.DirectoryName None "duplicate directory entry"
        elif Set.ofList names <> Set.ofList fileNames then
            err PathInvalid files.DirectoryName None $"""directory must hold exactly {String.Join(", ", fileNames)}; found {String.Join(", ", names |> List.sort)}"""
        else Ok()

    let private bytesOf (files: InvestigationFiles) name = files.Entries |> List.find (fst >> (=) name) |> snd

    /// Stage 2 framing (CONTRACT §2.1-2.2). Returns the decoded text of each file.
    let decodeText (name: string) (bytes: byte[]) : Result<string, ReaderError> =
        if bytes.Length = 0 then err ReaderCode.Encoding name None "empty file"
        elif bytes.Length >= 3 && bytes[0] = 0xEFuy && bytes[1] = 0xBBuy && bytes[2] = 0xBFuy then err ReaderCode.Encoding name None "byte order mark"
        else
            match (try Ok(strictUtf8.GetString bytes) with :? DecoderFallbackException -> Error()) with
            | Error() -> err ReaderCode.Encoding name None "invalid UTF-8"
            | Ok text ->
                if text.Contains '\r' then err ReaderCode.Encoding name None "CR in file"
                elif not (text.EndsWith '\n') then err ReaderCode.Encoding name None "missing final LF"
                elif name = "events.ndjson" && (text.StartsWith '\n' || text.Contains "\n\n") then err ReaderCode.Encoding name None "blank line"
                else Ok text

    /// Splits events.ndjson (already framed) into 1-based numbered lines.
    let lines (text: string) : (int * string) list =
        text.Substring(0, text.Length - 1).Split('\n') |> Array.toList |> List.mapi (fun i l -> i + 1, l)

    /// Stage 3 + 4 for one JSON text.
    let parseCanonical (name: string) (line: int option) (text: string) : Result<JValue, ReaderError> * (unit -> Result<unit, ReaderError>) =
        match Json.parse text with
        | Error e -> err JsonMalformed name line $"offset {e.Offset}: {e.Detail}", (fun () -> Ok())
        | Ok v -> Ok v, (fun () -> if Json.canonical v = text then Ok() else err NonCanonical name line "not the RFC 8785 canonical form")

    type private Parsed =
        { Manifest: JValue
          ManifestText: string
          Lines: (int * string * JValue) list
          Snapshot: JValue
          SnapshotText: string }

    let private firstError (checks: (unit -> Result<unit, ReaderError>) list) =
        checks |> List.fold (fun acc c -> acc |> Result.bind (fun () -> c ())) (Ok())

    /// Stages 2-4 over all three files, each stage completed for every file before the next begins.
    let private parse (files: InvestigationFiles) : Result<Parsed, ReaderError> =
        result {
            let! manifestText = decodeText "manifest.json" (bytesOf files "manifest.json")
            let! eventsText = decodeText "events.ndjson" (bytesOf files "events.ndjson")
            let! snapshotText = decodeText "snapshot.json" (bytesOf files "snapshot.json")
            let strip (s: string) = s.Substring(0, s.Length - 1)
            let m, mCanon = parseCanonical "manifest.json" None (strip manifestText)
            let evs = lines eventsText |> List.map (fun (n, l) -> n, l, parseCanonical "events.ndjson" (Some n) l)
            let s, sCanon = parseCanonical "snapshot.json" None (strip snapshotText)
            let! manifest = m
            let! parsedLines = evs |> traverse (fun (n, l, (r, _)) -> r |> Result.map (fun v -> n, l, v))
            let! snapshot = s
            do! firstError ([ mCanon ] @ (evs |> List.map (fun (_, _, (_, c)) -> c)) @ [ sCanon ])
            return { Manifest = manifest; ManifestText = strip manifestText; Lines = parsedLines; Snapshot = snapshot; SnapshotText = strip snapshotText }
        }

    /// Stage 5: a string version field that is not "1.0.0".
    let private checkVersions (p: Parsed) =
        let bad file line (v: JValue) =
            match v with
            | JObj o ->
                [ "formatVersion"; "schemaVersion"; "semanticVersion" ]
                |> List.tryPick (fun k ->
                    match Json.tryField k o with
                    | Some(JStr s) when s <> "1.0.0" -> Some(ReaderError.create UnsupportedVersion file line $"{k} '{s}' is not 1.0.0")
                    | _ -> None)
            | _ -> None
        [ bad "manifest.json" None p.Manifest ]
        @ (p.Lines |> List.map (fun (n, _, v) -> bad "events.ndjson" (Some n) v))
        @ [ bad "snapshot.json" None p.Snapshot ]
        |> List.tryPick id
        |> Option.map Error
        |> Option.defaultValue (Ok())

    /// Stage 6 support: the typed outcome of every line. Never drops a line.
    let private classify (n: int, raw: string, v: JValue) : LineOutcome =
        match v with
        | JObj o ->
            match Json.tryField "eventType" o with
            | Some(JStr t) when not (EventDecoder.isRegistered t) -> Unregistered { Line = n; RawLine = raw; EventType = t }
            | _ ->
                Envelope.decode n raw o
                |> Result.bind (fun env -> EventDecoder.decodePayload env.EventType env.Payload |> Result.map (fun e -> { Envelope = env; Event = e }))
                |> function
                    | Ok d -> Decoded d
                    | Error detail -> Rejected(ReaderError.create SchemaViolation "events.ndjson" (Some n) detail)
        | _ -> Rejected(ReaderError.create SchemaViolation "events.ndjson" (Some n) "an event line must be a JSON object")

    let private decodeManifest (v: JValue) : Result<InvestigationManifest, string> =
        match v with
        | JObj o ->
            result {
                do!
                    Field.exactKeys "manifest"
                        [ "formatVersion"; "investigationId"; "schemaVersion"; "semanticVersion"; "aggregateVersion"; "eventCount"; "eventsSha256"
                          "snapshotVersion"; "snapshotSha256"; "domainPacks"; "createdAt"; "updatedAt" ] o
                let! fv = Field.str o "formatVersion"
                let! sv = Field.str o "schemaVersion"
                let! smv = Field.str o "semanticVersion"
                let! id = Field.uuid o "investigationId"
                let! av = Field.positive o "aggregateVersion"
                let! ec = Field.positive o "eventCount"
                let! es = Field.str o "eventsSha256"
                let! snv = Field.int o "snapshotVersion"
                let! ss = Field.str o "snapshotSha256"
                let! packs = Field.arr o "domainPacks"
                let! created = Field.timestamp o "createdAt"
                let! updated = Field.timestamp o "updatedAt"
                let isSha (s: string) = s.Length = 64 && s |> Seq.forall (fun c -> (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))
                if fv <> "1.0.0" || sv <> "1.0.0" || smv <> "1.0.0" then return! Error "manifest version fields must be the string 1.0.0"
                elif snv <> 1L then return! Error "snapshotVersion must be 1"
                elif not packs.IsEmpty then return! Error "domainPacks must be empty in v1.0.0"
                elif not (isSha es && isSha ss) then return! Error "digests must be lowercase SHA-256 hex"
                else
                    return
                        { InvestigationId = id; AggregateVersion = av; EventCount = ec; EventsSha256 = es; SnapshotSha256 = ss; SnapshotVersion = snv
                          CreatedAt = created; UpdatedAt = updated; FormatVersion = fv; SchemaVersion = sv; SemanticVersion = smv }
            }
        | _ -> Error "manifest must be a JSON object"

    let private decodeSnapshot (v: JValue) : Result<SnapshotDocument, string> =
        match v with
        | JObj o ->
            result {
                do! Field.exactKeys "snapshot" [ "formatVersion"; "investigationId"; "aggregateVersion"; "projectionVersion"; "semanticVersion"; "generatedAt"; "state" ] o
                let! fv = Field.str o "formatVersion"
                let! smv = Field.str o "semanticVersion"
                let! id = Field.uuid o "investigationId"
                let! av = Field.positive o "aggregateVersion"
                let! pv = Field.int o "projectionVersion"
                let! g = Field.timestamp o "generatedAt"
                let! state = Field.obj o "state"
                if fv <> "1.0.0" || smv <> "1.0.0" then return! Error "snapshot version fields must be the string 1.0.0"
                elif pv <> 1L then return! Error "projectionVersion must be 1"
                else return { InvestigationId = id; AggregateVersion = av; ProjectionVersion = pv; GeneratedAt = g; State = JObj state }
            }
        | _ -> Error "snapshot must be a JSON object"

    /// Lines grouped into maximal runs of one correlationId (CONTRACT §5.4).
    let commandGroups (events: DecodedEvent list) : DecodedEvent list list =
        events
        |> List.fold
            (fun groups e ->
                match groups with
                | (head :: _ as g) :: rest when head.Envelope.CorrelationId = e.Envelope.CorrelationId -> (e :: g) :: rest
                | _ -> [ e ] :: groups)
            []
        |> List.map List.rev
        |> List.rev

    let private stage8to14 (dir: string) (m: InvestigationManifest) (eventsBytes: byte[]) (events: DecodedEvent list) =
        let n = int64 (List.length events)
        let file = "events.ndjson"
        let first = List.head events
        let last = List.last events
        let check cond code f line detail = fun () -> if cond () then Ok() else err code f line detail
        let byEnvelope (pred: DecodedEvent -> bool) code detail =
            fun () ->
                match events |> List.tryFind (pred >> not) with
                | None -> Ok()
                | Some e -> err code file (Some e.Envelope.Line) (detail e)
        let duplicateId () =
            events
            |> List.fold
                (fun (seen: Set<string>, found) e ->
                    match found with
                    | Some _ -> seen, found
                    | None -> if seen.Contains e.Envelope.EventId.Text then seen, Some e else seen.Add e.Envelope.EventId.Text, None)
                (Set.empty, None)
            |> snd
            |> function
                | None -> Ok()
                | Some e -> err EventIdDuplicate file (Some e.Envelope.Line) $"eventId {e.Envelope.EventId} appears twice"
        let correlationReuse () =
            commandGroups events
            |> List.fold
                (fun (seen: Set<string>, found) g ->
                    let c = (List.head g).Envelope.CorrelationId.Text
                    match found with
                    | Some _ -> seen, found
                    | None -> if seen.Contains c then seen, Some(List.head g) else seen.Add c, None)
                (Set.empty, None)
            |> snd
            |> function
                | None -> Ok()
                | Some e -> err CorrelationReused file (Some e.Envelope.Line) $"correlationId {e.Envelope.CorrelationId} names two non-adjacent command groups"
        firstError
            [ check (fun () -> m.InvestigationId.Text = dir) ManifestMismatch "manifest.json" None "manifest investigationId differs from the directory"
              check (fun () -> m.EventCount = n && m.AggregateVersion = n) ManifestMismatch "manifest.json" None $"manifest counts ({m.AggregateVersion}, {m.EventCount}) differ from the {n} lines"
              check (fun () -> m.CreatedAt.Text = first.Envelope.OccurredAt.Text) ManifestMismatch "manifest.json" None "createdAt is not the first event's occurredAt"
              check (fun () -> m.UpdatedAt.Text = last.Envelope.OccurredAt.Text) ManifestMismatch "manifest.json" None "updatedAt is not the last event's occurredAt"
              check (fun () -> Sha256.ofBytes eventsBytes = m.EventsSha256) EventsHashMismatch "manifest.json" None "eventsSha256 does not match events.ndjson"
              byEnvelope (fun e -> e.Envelope.InvestigationId = m.InvestigationId) InvestigationMismatch (fun e -> $"event names investigation {e.Envelope.InvestigationId}")
              duplicateId
              byEnvelope (fun e -> e.Envelope.AggregateVersion = int64 e.Envelope.Line) SequenceInvalid (fun e -> $"line {e.Envelope.Line} carries aggregateVersion {e.Envelope.AggregateVersion}")
              byEnvelope
                  (fun e -> Uuid.deriveEventId e.Envelope.InvestigationId e.Envelope.AggregateVersion e.Envelope.CorrelationId = e.Envelope.EventId)
                  EventIdMismatch
                  (fun e -> $"eventId {e.Envelope.EventId} is not the §5.2 derivation")
              correlationReuse ]

    /// Every line's typed outcome, independent of stage order: used for Level B accounting and diagnostics.
    let outcomes (files: InvestigationFiles) : Result<LineOutcome list, ReaderError> =
        result {
            do! checkPath files
            let! p = parse files
            return p.Lines |> List.map classify
        }

    /// Stages 1-14 (CONTRACT §11.1). Fail closed: the first failing stage is returned, nothing is partially loaded.
    let read (files: InvestigationFiles) : Result<StructuralRead, ReaderError> =
        result {
            do! checkPath files
            let! p = parse files
            do! checkVersions p
            let classified = p.Lines |> List.map classify
            do!
                classified
                |> List.tryPick (function
                    | Unregistered u -> Some(ReaderError.create UnknownEventType "events.ndjson" (Some u.Line) $"eventType '{u.EventType}' is not registered (line kept, history refused)")
                    | _ -> None)
                |> Option.map Error
                |> Option.defaultValue (Ok())
            let! manifest = decodeManifest p.Manifest |> Result.mapError (ReaderError.create SchemaViolation "manifest.json" None)
            let! events =
                classified
                |> traverse (function
                    | Decoded d -> Ok d
                    | Rejected e -> Error e
                    | Unregistered u -> Error(ReaderError.create UnknownEventType "events.ndjson" (Some u.Line) u.EventType))
            let! snapshot = decodeSnapshot p.Snapshot |> Result.mapError (ReaderError.create SchemaViolation "snapshot.json" None)
            let eventsBytes = bytesOf files "events.ndjson"
            do! stage8to14 files.DirectoryName manifest eventsBytes events
            return
                { DirectoryName = files.DirectoryName
                  Manifest = manifest
                  ManifestText = p.ManifestText
                  EventsBytes = eventsBytes
                  Events = events
                  Snapshot = snapshot
                  SnapshotText = p.SnapshotText }
        }

    /// §9.3: a successor file set must start with the previously accepted events.ndjson bytes.
    let checkSuccessor (accepted: byte[]) (candidate: StructuralRead) : Result<unit, ReaderError> =
        let c = candidate.EventsBytes
        if c.Length >= accepted.Length && Array.sub c 0 accepted.Length = accepted then Ok()
        else err HistoryRewritten "events.ndjson" None "the candidate does not extend the accepted events.ndjson bytes"
