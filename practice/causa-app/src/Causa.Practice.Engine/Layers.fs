// Practice only (FENCE.md). The four state layers of a loaded investigation, kept as distinct types:
//   Raw           - the exact bytes the provider returned, per file (nothing interpreted)
//   Decoded       - stages 1-14 of causa-storage v1.0.0: typed events, manifest and snapshot document
//   Replayed      - the state this application folds from the decoded events (never read from a file)
//   Authoritative - the snapshot.json the repository stores, and whether it equals the replayed projection
namespace Causa.Practice

open Causa.HistoricalCompatibility

type RawLayer =
    { Directory: string
      Files: (string * byte[]) list }

type DecodedLayer = { Read: StructuralRead }

type ReplayedLayer =
    { State: InvestigationState
      StateSha256: string
      Anomalies: ReplayAnomaly list }

type AuthoritativeLayer =
    { StoredStateSha256: string
      /// The stored snapshot.json is byte-for-byte the projection of the replayed history, and its digest matches the
      /// manifest (HistoricalLoader.verify). Admissibility is NOT evaluated (no engine oracle; CONTRACT §11.3).
      Agrees: Result<unit, string> }

type Layers =
    { Raw: RawLayer
      Decoded: Result<DecodedLayer, ReaderError>
      Replayed: Result<ReplayedLayer, string>
      Authoritative: Result<AuthoritativeLayer, string> }

module Layers =
    let private storedSha (r: StructuralRead) = Sha256.ofText (Json.canonical r.Snapshot.State)

    /// Pure: raw bytes in, every layer out. A failed layer stops the layers after it (fail closed), and each failure is
    /// reported at the layer where it happened.
    let load (raw: RawLayer) : Layers =
        let decoded =
            StorageReader.read { DirectoryName = raw.Directory; Entries = raw.Files }
            |> Result.map (fun r -> { Read = r })
        let replayed =
            decoded
            |> Result.mapError ReaderError.describe
            |> Result.bind (fun d ->
                let r = ReplayAdapter.replay d.Read.Events
                match r.State with
                | None -> Error "the history does not begin with InvestigationStarted"
                | Some s -> Ok { State = s; StateSha256 = ReplayAdapter.stateSha256 s; Anomalies = r.Anomalies })
        let authoritative =
            match decoded, replayed with
            | Ok d, Ok _ ->
                let agrees =
                    match HistoricalLoader.verify d.Read with
                    | StructurallyVerified _ -> Ok()
                    | LoadRejected(e, _) -> Error(ReaderError.describe e)
                Ok { StoredStateSha256 = storedSha d.Read; Agrees = agrees }
            | Error e, _ -> Error("not decoded: " + ReaderError.describe e)
            | _, Error e -> Error("not replayed: " + e)
        { Raw = raw; Decoded = decoded; Replayed = replayed; Authoritative = authoritative }

    let isVerified (l: Layers) =
        match l.Replayed, l.Authoritative with
        | Ok r, Ok a when r.Anomalies.IsEmpty -> Result.isOk a.Agrees
        | _ -> false
