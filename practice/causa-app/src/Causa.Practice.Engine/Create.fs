// Practice only (FENCE.md). Builds the causa-storage v1.0.0 file set of a new investigation: one InvestigationStarted
// event, its manifest and the snapshot replayed from it. Pure: identifiers and the clock are inputs.
namespace Causa.Practice

open System
open System.Text
open Causa.HistoricalCompatibility

type NewInvestigation =
    { InvestigationId: Uuid
      CorrelationId: Uuid
      ObservationId: Uuid
      Title: Text
      Original: Text
      ActorId: Text
      OccurredAt: Timestamp }

module Create =
    let timestampOf (t: DateTimeOffset) : Timestamp option =
        Timestamp.tryParse (t.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", Globalization.CultureInfo.InvariantCulture))

    let uuidOf (g: Guid) = Uuid.tryParse (g.ToString("D").ToLowerInvariant())

    /// Validates user input; every failure names its field.
    let validate (now: DateTimeOffset) (ids: Guid * Guid * Guid) (title: string) (original: string) : Result<NewInvestigation, (string * string) list> =
        let i, c, o = ids
        let errors =
            [ if (Text.tryCreate title).IsNone then "title", "Enter a title."
              if (Text.tryCreate original).IsNone then "original", "Describe what happened." ]
        match errors, uuidOf i, uuidOf c, uuidOf o, timestampOf now, Text.tryCreate title, Text.tryCreate original, Text.tryCreate "practice-user" with
        | [], Some i, Some c, Some o, Some t, Some title, Some original, Some actor ->
            Ok { InvestigationId = i; CorrelationId = c; ObservationId = o; Title = title; Original = original; ActorId = actor; OccurredAt = t }
        | [], _, _, _, _, _, _, _ -> Error [ "form", "Could not create identifiers." ]
        | errs, _, _, _, _, _, _, _ -> Error errs

    let private eventLine (n: NewInvestigation) =
        JObj
            [ "formatVersion", JStr "1.0.0"
              "eventId", JStr (Uuid.deriveEventId n.InvestigationId 1L n.CorrelationId).Text
              "investigationId", JStr n.InvestigationId.Text
              "aggregateVersion", JInt 1L
              "eventType", JStr "InvestigationStarted"
              "occurredAt", JStr n.OccurredAt.Text
              "actor", JObj [ "actorId", JStr n.ActorId.Value; "kind", JStr "human" ]
              "semanticVersion", JStr "1.0.0"
              "correlationId", JStr n.CorrelationId.Text
              "payload", JObj [ "title", JStr n.Title.Value; "observationId", JStr n.ObservationId.Text; "original", JStr n.Original.Value ] ]
        |> Json.canonical

    /// The three files, in write order (events, snapshot, manifest last: the manifest names the other two by digest).
    let files (n: NewInvestigation) : Result<(string * byte[]) list, string> =
        let line = eventLine n
        let eventsText = line + "\n"
        let eventsBytes = Encoding.UTF8.GetBytes eventsText
        let decoded =
            match Json.parse line with
            | Ok(JObj o) ->
                Envelope.decode 1 line o
                |> Result.bind (fun env -> EventDecoder.decodePayload env.EventType env.Payload |> Result.map (fun e -> { Envelope = env; Event = e }))
            | Ok _ -> Error "event is not an object"
            | Error e -> Error e.Detail
        decoded
        |> Result.bind (fun d ->
            match (ReplayAdapter.replay [ d ]).State with
            | None -> Error "replay produced no state"
            | Some state ->
                let snapshotText = Json.canonical (ReplayAdapter.snapshotJson d.Envelope state)
                let manifest =
                    JObj
                        [ "aggregateVersion", JInt 1L
                          "createdAt", JStr n.OccurredAt.Text
                          "domainPacks", JArr []
                          "eventCount", JInt 1L
                          "eventsSha256", JStr(Sha256.ofBytes eventsBytes)
                          "formatVersion", JStr "1.0.0"
                          "investigationId", JStr n.InvestigationId.Text
                          "schemaVersion", JStr "1.0.0"
                          "semanticVersion", JStr "1.0.0"
                          "snapshotSha256", JStr(Sha256.ofText snapshotText)
                          "snapshotVersion", JInt 1L
                          "updatedAt", JStr n.OccurredAt.Text ]
                Ok
                    [ "events.ndjson", eventsBytes
                      "snapshot.json", Encoding.UTF8.GetBytes(snapshotText + "\n")
                      "manifest.json", Encoding.UTF8.GetBytes(Json.canonical manifest + "\n") ])
