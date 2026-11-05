/// Writes causa-storage v1.0.0 file sets for new and edited investigations. Every produced file set is read back
/// through the same fail-closed reader (stages 1-14, 16, 17) before it may be sent; a set the reader refuses is
/// never written.
module Causa.Preview.Writer

open System
open System.Text
open Causa.HistoricalCompatibility

/// The commands the preview supports: start an investigation, record an observation, record a hypothesis.
type Command =
    | Start of title: string * observation: string
    | RecordObservation of text: string
    | RecordHypothesis of statement: string

let private previewNamespace = Uuid.v5 Uuid.namespaceUrl "https://github.com/kemiller2002/Indy-init/preview/causa/ids"

let actor = JObj [ "actorId", JStr "causa-preview-user"; "kind", JStr "human" ]

/// The working-copy path of an investigation (CONTRACT §3, §9.4.4: writes only under workspaces/demo/<runId>/).
let directory (workspace: string) (investigationId: string) = $"workspaces/demo/{workspace}/investigations/{investigationId}"

let validWorkspace (w: string) = Text.RegularExpressions.Regex.IsMatch(w, @"\A[a-z0-9][a-z0-9-]{0,63}\z")

let newInvestigationId (workspace: string) (title: string) (now: Timestamp) =
    (Uuid.v5 previewNamespace $"investigation/{workspace}/{now.Text}/{title}").Text

let private line (investigationId: Uuid) (version: int64) (now: Timestamp) (correlation: Uuid) (eventType: string) (payload: JValue) =
    let eventId = Uuid.deriveEventId investigationId version correlation
    JObj
        [ "formatVersion", JStr "1.0.0"; "eventId", JStr eventId.Text; "investigationId", JStr investigationId.Text
          "aggregateVersion", JInt version; "eventType", JStr eventType; "occurredAt", JStr now.Text; "actor", actor
          "semanticVersion", JStr "1.0.0"; "correlationId", JStr correlation.Text; "payload", payload ]
    |> Json.canonical

/// The canonical event lines one command emits, numbered from `nextVersion`. Text is kept verbatim (CONTRACT §2.6).
let eventsOf (investigationId: Uuid) (nextVersion: int64) (now: Timestamp) (command: Command) : Result<string list, string> =
    let correlation = Uuid.v5 previewNamespace $"command/{investigationId.Text}/{nextVersion}/{now.Text}"
    let id kind = (Uuid.v5 previewNamespace $"{kind}/{investigationId.Text}/{nextVersion}").Text
    let validated (errors: Validation.FieldError list) value =
        match errors with
        | [] -> Ok value
        | es -> Error(es |> List.map (fun e -> e.Message) |> String.concat " ")
    match command with
    | Start(title, observation) ->
        validated (Validation.startForm title observation) (title, observation)
        |> Result.map (fun (t, o) ->
            [ line investigationId nextVersion now correlation "InvestigationStarted"
                  (JObj [ "title", JStr t; "observationId", JStr(id "observation"); "original", JStr o ]) ])
    | RecordObservation text ->
        validated (Validation.observationText text) text
        |> Result.map (fun o ->
            [ line investigationId nextVersion now correlation "ObservationRecorded"
                  (JObj [ "observationId", JStr(id "observation"); "original", JStr o; "observedAt", JNull ]) ])
    | RecordHypothesis statement ->
        validated (Validation.hypothesisStatement statement) statement
        |> Result.map (fun s ->
            [ line investigationId nextVersion now correlation "HypothesisRecorded"
                  (JObj [ "hypothesisId", JStr(id "hypothesis"); "statement", JStr s; "mechanism", JNull ]) ])

/// The three files for a complete history (existing lines followed by appended ones), with manifest and snapshot
/// derived from the replayed history (CONTRACT §8.1, §9.1). Verified by the reader before it is returned.
let fileSet (directoryName: string) (createdLines: string list) : Result<InvestigationFiles, string> =
    let eventsText = String.Join("\n", createdLines) + "\n"
    let eventsBytes = Encoding.UTF8.GetBytes eventsText
    let parsedLines = createdLines |> List.mapi (fun i l -> i + 1, l)
    let events =
        parsedLines
        |> List.map (fun (n, l) ->
            match Json.parse l with
            | Ok(JObj o) ->
                Envelope.decode n l o
                |> Result.bind (fun env -> EventDecoder.decodePayload env.EventType env.Payload |> Result.map (fun e -> { Envelope = env; Event = e }))
            | _ -> Error "an event line is not an object")
    match events |> List.tryPick (function Error e -> Some e | Ok _ -> None) with
    | Some e -> Error e
    | None ->
        let ds = events |> List.choose (function Ok d -> Some d | Error _ -> None)
        let replay = ReplayAdapter.replay ds
        match replay.State with
        | None -> Error "the history does not begin with InvestigationStarted"
        | Some state ->
            let first = (List.head ds).Envelope
            let last = (List.last ds).Envelope
            let snapshotText = Json.canonical (ReplayAdapter.snapshotJson last state)
            let manifest =
                JObj
                    [ "aggregateVersion", JInt last.AggregateVersion; "createdAt", JStr first.OccurredAt.Text; "domainPacks", JArr []
                      "eventCount", JInt(int64 (List.length ds)); "eventsSha256", JStr(Sha256.ofBytes eventsBytes); "formatVersion", JStr "1.0.0"
                      "investigationId", JStr directoryName; "schemaVersion", JStr "1.0.0"; "semanticVersion", JStr "1.0.0"
                      "snapshotSha256", JStr(Sha256.ofText snapshotText); "snapshotVersion", JInt 1L; "updatedAt", JStr last.OccurredAt.Text ]
            let files =
                { DirectoryName = directoryName
                  Entries =
                    [ "manifest.json", Encoding.UTF8.GetBytes(Json.canonical manifest + "\n")
                      "events.ndjson", eventsBytes
                      "snapshot.json", Encoding.UTF8.GetBytes(snapshotText + "\n") ] }
            // Fail closed: the set must pass the same reader a loader applies.
            match HistoricalLoader.load files with
            | StructurallyVerified _ -> Ok files
            | LoadRejected(e, _) -> Error("the produced file set fails its own verification: " + ReaderError.describe e)

/// The existing event lines of a loaded file set.
let linesOf (files: InvestigationFiles) : string list =
    match files.Entries |> List.tryFind (fst >> (=) "events.ndjson") with
    | Some(_, b) -> (Encoding.UTF8.GetString b).TrimEnd('\n').Split('\n') |> Array.toList
    | None -> []
