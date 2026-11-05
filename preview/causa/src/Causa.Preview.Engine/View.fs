/// Projection: model -> Limen ViewState (flat scalars and lists of flat items). The view states what each level of
/// evidence actually is: raw bytes read, typed events decoded, replayed state, and admissibility (never claimed).
module Causa.Preview.View

open System.Text
open Causa.HistoricalCompatibility
open Causa.Preview.Model

let private s (v: string) = JStr v
let private b (v: bool) = JBool v
let private i (v: int) = JInt(int64 v)
let private items (xs: (string * JValue) list list) = JArr(xs |> List.map JObj)

let private lineCount (files: InvestigationFiles) =
    match files.Entries |> List.tryFind (fst >> (=) "events.ndjson") with
    | Some(_, bytes) -> (Encoding.UTF8.GetString bytes).TrimEnd('\n').Split('\n').Length
    | None -> 0

let private detail (m: Model) (o: Opened) : (string * JValue) list =
    let expected = m.Entries |> List.tryFind (fun e -> e.Source = o.Source && e.DirName = o.DirName) |> Option.bind (fun e -> e.ExpectedState)
    let raw = $"{lineCount o.Files} event lines in events.ndjson; 3 files read at commit {o.ReadAt}"
    let common =
        [ "dWritable", b (o.Source = WorkingCopy)
          "dSource", s (match o.Source with Catalog -> "Historical catalog · read-only" | WorkingCopy -> "Working copy · writable")
          "dReadAt", s o.ReadAt
          "dRaw", s raw
          "unsavedCount", i o.Unsaved.Length
          "canSave", b (o.Source = WorkingCopy && not o.Unsaved.IsEmpty && m.Pending.IsEmpty)
          "unsaved",
          items (
              o.Unsaved
              |> List.mapi (fun n c ->
                  [ "key", s (string n)
                    "text",
                    s (
                        match c with
                        | Causa.Preview.Writer.RecordObservation t -> "Observation: " + t
                        | Causa.Preview.Writer.RecordHypothesis t -> "Hypothesis: " + t
                        | Causa.Preview.Writer.Start(t, _) -> "Start: " + t
                    ) ])
          ) ]
    match o.Result with
    | LoadRejected(e, conditional) ->
        common
        @ [ "dLoaded", b false
            "dTitle", s o.DirName
            "dRefused", s (ReaderError.describe e + (if conditional then " (conditional on admissibility)" else ""))
            "dDecoded", s "Not decoded: the reader refused the history (fail closed, nothing partially shown)."
            "dReplayed", s "—"; "dCatalogMatch", s "—"; "dAdmissibility", s "—"; "dLifecycle", s "—"; "dVersion", i 0
            "observations", JArr []; "hypotheses", JArr []; "events", JArr []; "dEvidence", i 0; "dRelationships", i 0 ]
    | StructurallyVerified h ->
        let st = h.State
        let catalogMatch =
            match expected with
            | Some x when x = h.StateSha256 -> "Replayed state equals the catalog manifest's expectedStateSha256."
            | Some x -> $"MISMATCH: replayed state {h.StateSha256} differs from the catalog's {x}."
            | None -> "No independent catalog expectation for this investigation."
        common
        @ [ "dLoaded", b true
            "dTitle", s st.Title.Value
            "dRefused", s ""
            "dDecoded", s $"{h.Read.Events.Length} of {lineCount o.Files} lines decoded to typed events (none dropped)."
            "dReplayed", s "Replayed with the causa-storage §8.3 fold; the projection equals snapshot.json byte for byte."
            "dCatalogMatch", s catalogMatch
            "dAdmissibility", s "Admissibility not evaluated: this is not an authoritative, verified state (CONTRACT §11.3)."
            "dLifecycle", s (WireEnum.toWire Enums.lifecycle st.Lifecycle)
            "dVersion", JInt st.Version
            "dEvidence", i st.Evidence.Count
            "dRelationships", i st.Relationships.Count
            "observations", items (st.Observations |> Map.toList |> List.map (fun (k, v) -> [ "key", s k.Text; "text", s v.Original.Value ]))
            "hypotheses",
            items (st.Hypotheses |> Map.toList |> List.map (fun (k, v) -> [ "key", s k.Text; "text", s v.Statement.Value; "state", s (WireEnum.toWire Enums.hypothesisState v.State) ]))
            "events",
            items (
                h.Read.Events
                |> List.map (fun d ->
                    [ "key", s (string d.Envelope.AggregateVersion)
                      "version", JInt d.Envelope.AggregateVersion
                      "type", s (EventDecoder.discriminator d.Event)
                      "at", s d.Envelope.OccurredAt.Text
                      "actor", s d.Envelope.Actor.ActorId.Value ])
            ) ]

let project (m: Model) : JValue =
    let entry (e: Entry) = [ "key", s ((if e.Source = Catalog then "c:" else "w:") + e.DirName); "title", s e.Title; "detail", s e.Detail; "id", s e.DirName ]
    let base' =
        [ "screenConnect", b (m.Screen = ConnectScreen)
          "screenList", b (m.Screen = ListScreen)
          "screenDetail", b (m.Screen = DetailScreen && m.Opened.IsSome)
          "screenCreate", b (m.Screen = CreateScreen)
          "connected", b m.Catalog.IsSome
          "busy", b (not m.Pending.IsEmpty)
          "idle", b m.Pending.IsEmpty
          "notIdle", b (not m.Pending.IsEmpty)
          "status", s m.Status
          "hasProblem", b m.Problem.IsSome
          "problem", s (m.Problem |> Option.defaultValue "")
          "hasConfirmed", b m.Confirmed.IsSome
          "confirmed", s (m.Confirmed |> Option.defaultValue "")
          "catalogRepo", s m.Form.CatalogRepo
          "catalogBranch", s m.Form.CatalogBranch
          "writeRepo", s m.Form.WriteRepo
          "writeBranch", s m.Form.WriteBranch
          "workspace", s m.Form.Workspace
          "newTitle", s m.Form.NewTitle
          "newObservation", s m.Form.NewObservation
          "editObservation", s m.Form.EditObservation
          "editHypothesis", s m.Form.EditHypothesis
          "catalogLabel", s (m.Catalog |> Option.map Causa.Preview.GitHub.RepoRef.text |> Option.defaultValue "")
          "writeLabel", s (m.Write |> Option.map Causa.Preview.GitHub.RepoRef.text |> Option.defaultValue "no writable repository connected")
          "canWrite", b m.Write.IsSome
          "cannotWrite", b m.Write.IsNone
          "catalogItems", items (m.Entries |> List.filter (fun e -> e.Source = Catalog) |> List.map entry)
          "workingItems", items (m.Entries |> List.filter (fun e -> e.Source = WorkingCopy) |> List.map entry)
          "workingEmpty", b (m.Entries |> List.exists (fun e -> e.Source = WorkingCopy) |> not) ]
    let d = match m.Opened with Some o -> detail m o | None -> []
    JObj(base' @ d)
