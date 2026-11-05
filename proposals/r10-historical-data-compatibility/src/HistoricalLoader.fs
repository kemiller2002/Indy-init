namespace Causa.HistoricalCompatibility

/// Raw document retrieval: the only thing a data-access adapter (GitHub, file system, test double) implements.
/// It knows nothing about events, replay or domain rules, and returns provider-neutral bytes.
type DocumentSource =
    { /// Investigation directory names in one workspace (e.g. `workspaces/demo/demonstration-v1/investigations`).
      ListInvestigations: unit -> Result<string list, string>
      /// Every entry of one investigation directory, with exact bytes.
      ReadInvestigation: string -> Result<InvestigationFiles, string> }

/// CONTRACT §11.3: admissibility is the pinned engine's `History.validate`. This adapter does not have it.
type Admissibility =
    /// Necessary conditions the adapter can check failed: the history cannot be admissible.
    | AdapterDetectedInadmissible of ReplayAnomaly list
    /// The adapter found nothing wrong, but the engine-equivalent check did not run, so validity is NOT claimed.
    | NotEvaluated of reason: string

/// A loaded investigation. It is `StructurallyVerified`, never "valid": stages 1-14 and 16-17 passed, stage 15 did not
/// run (CONTRACT §11.3 "structurally valid, admissibility not evaluated").
type HistoricalInvestigation =
    { Read: StructuralRead
      State: InvestigationState
      StateSha256: string
      Admissibility: Admissibility }

type LoadResult =
    /// Fail closed: nothing is partially loaded (CONTRACT §11.2). `ConditionalOnAdmissibility` is true for a stage
    /// after 15, whose primary code depends on an admissibility verdict this adapter cannot give.
    | LoadRejected of error: ReaderError * conditionalOnAdmissibility: bool
    | StructurallyVerified of HistoricalInvestigation

/// Acceptance levels of the mission brief, reported separately and never inferred from one another.
type LevelStatus =
    | Achieved
    | NotAchieved of reasons: string list

type AcceptanceReport =
    { InvestigationId: string
      LevelA_RawReadable: LevelStatus
      LevelB_Decoded: LevelStatus
      LevelCProjection_SnapshotReproduced: LevelStatus
      LevelC_Authoritative: LevelStatus }

module HistoricalLoader =
    let notEvaluatedReason =
        "causa-storage v1.0.0 §11.3 names the pinned engine's History.validate (kemiller2002/indy-init-rehearsal-001@55f49f61, private) "
        + "as the admissibility oracle; no requirements-derived equivalent exists yet"

    /// Stages 15 (adapter-checkable part only), 16 and 17 over a structurally read history.
    let verify (read: StructuralRead) : LoadResult =
        let replay = ReplayAdapter.replay read.Events
        match replay.State with
        | None -> LoadRejected(ReaderError.create HistoryInadmissible "events.ndjson" (Some 1) "adapter-detected: the history does not begin with InvestigationStarted", false)
        | Some _ when not replay.Anomalies.IsEmpty ->
            let a = List.head replay.Anomalies
            LoadRejected(ReaderError.create HistoryInadmissible "events.ndjson" (Some a.Line) $"adapter-detected ({a.Kind}): {a.Detail}", false)
        | Some state ->
            let last = (List.last read.Events).Envelope
            let expectedSnapshot = Json.canonical (ReplayAdapter.snapshotJson last state)
            if Sha256.ofText read.SnapshotText <> read.Manifest.SnapshotSha256 then
                LoadRejected(ReaderError.create SnapshotHashMismatch "manifest.json" None "snapshotSha256 does not match snapshot.json", true)
            elif expectedSnapshot <> read.SnapshotText then
                LoadRejected(ReaderError.create SnapshotMismatch "snapshot.json" None "snapshot.json is not the projection of the replayed history", true)
            else
                StructurallyVerified
                    { Read = read
                      State = state
                      StateSha256 = ReplayAdapter.stateSha256 state
                      Admissibility = NotEvaluated notEvaluatedReason }

    /// Decode, then project. The two steps are separate functions so a caller can stop after decoding.
    let load (files: InvestigationFiles) : LoadResult =
        match StorageReader.read files with
        | Error e -> LoadRejected(e, false)
        | Ok read -> verify read

    /// Loads every investigation a source lists, in ordinal directory order. A failure of one never hides another.
    let loadWorkspace (source: DocumentSource) : Result<(string * LoadResult) list, string> =
        source.ListInvestigations()
        |> Result.map (fun names ->
            names
            |> List.sortWith (fun a b -> System.String.CompareOrdinal(a, b))
            |> List.map (fun n ->
                match source.ReadInvestigation n with
                | Ok files -> n, load files
                | Error detail -> n, LoadRejected(ReaderError.create PathInvalid n None ("retrieval failed: " + detail), false)))

    /// The three acceptance levels for one investigation directory. `expectedStateSha256` is an independent
    /// expectation (the catalog manifest), not something this adapter computed.
    let acceptance (files: InvestigationFiles) (expectedStateSha256: string option) : AcceptanceReport =
        let levelA =
            match StorageReader.outcomes files with
            | Ok _ -> Achieved
            | Error e -> NotAchieved [ ReaderError.describe e ]
        let levelB =
            match StorageReader.outcomes files with
            | Error e -> NotAchieved [ ReaderError.describe e ]
            | Ok outs ->
                let problems =
                    outs
                    |> List.choose (function
                        | Decoded d when EventEncoder.canonicalLine d <> d.Envelope.RawLine -> Some $"line {d.Envelope.Line}: re-encoding differs from the stored line"
                        | Decoded _ -> None
                        | Unregistered u -> Some $"line {u.Line}: unregistered eventType '{u.EventType}' (kept losslessly, not typed)"
                        | Rejected e -> Some(ReaderError.describe e))
                if problems.IsEmpty then Achieved else NotAchieved problems
        let loaded = load files
        let levelCProj =
            match loaded with
            | LoadRejected(e, _) -> NotAchieved [ ReaderError.describe e ]
            | StructurallyVerified h ->
                match expectedStateSha256 with
                | Some x when x <> h.StateSha256 -> NotAchieved [ $"state digest {h.StateSha256} differs from the expected {x}" ]
                | Some _ -> Achieved
                | None -> NotAchieved [ "no independent expected state digest was supplied" ]
        let levelC =
            match loaded with
            | LoadRejected(e, _) -> NotAchieved [ ReaderError.describe e ]
            | StructurallyVerified h ->
                match h.Admissibility with
                | NotEvaluated r -> NotAchieved [ "admissibility not evaluated: " + r; "the approved domain representation (frozen v4.1.4 CoreEvent) cannot represent the history; the successor is only proposed" ]
                | AdapterDetectedInadmissible a -> NotAchieved(a |> List.map (fun x -> x.Detail))
        { InvestigationId = files.DirectoryName
          LevelA_RawReadable = levelA
          LevelB_Decoded = levelB
          LevelCProjection_SnapshotReproduced = levelCProj
          LevelC_Authoritative = levelC }
