// Practice only (FENCE.md). Pure projection of the model to the Limen view (flat keys; lists of flat items). Never
// projects the token.
namespace Causa.Practice

open System.Text.Json.Nodes
open Causa.HistoricalCompatibility

module View =
    let private s (v: string) : JsonNode = JsonValue.Create v
    let private b (v: bool) : JsonNode = JsonValue.Create v
    let private items (rows: (string * string) list list) : JsonNode =
        let a = JsonArray()
        rows |> List.iter (fun r -> let o = JsonObject() in r |> List.iter (fun (k, v) -> o[k] <- s v); a.Add o)
        a

    let private short (n: int) (t: string) = if t.Length <= n then t else t.Substring(0, n) + "…"
    let private sha (x: string) = x.Substring(0, 12) + "…"

    let titleOf (l: Layers) = match l.Replayed with Ok r -> r.State.Title.Value | Error _ -> "(not replayed)"

    let private layerTexts (l: Layers) =
        let raw = "Raw: " + (l.Raw.Files |> List.map (fun (n, x) -> $"{n} {x.Length:N0} bytes") |> String.concat "; ") + " — exact bytes from the provider."
        let decoded =
            match l.Decoded with
            | Ok d -> $"Decoded: {d.Read.Events.Length} events, manifest and snapshot document passed causa-storage v1.0.0 stages 1-14."
            | Error e -> "Decoded: REFUSED — " + ReaderError.describe e
        let replayed =
            match l.Replayed with
            | Ok r -> $"Replayed: this app folded the events to version {r.State.Version}; state sha256 {sha r.StateSha256}; {r.Anomalies.Length} adapter-detected anomalies."
            | Error e -> "Replayed: NOT AVAILABLE — " + e
        let authoritative =
            match l.Authoritative with
            | Ok a ->
                match a.Agrees with
                | Ok() -> $"Authoritative: the stored snapshot.json (state sha256 {sha a.StoredStateSha256}) equals the replayed projection and its manifest digest. Admissibility not evaluated (no engine oracle)."
                | Error e -> $"Authoritative: the stored snapshot.json (state sha256 {sha a.StoredStateSha256}) DISAGREES — {e}"
            | Error e -> "Authoritative: NOT COMPARED — " + e
        raw, decoded, replayed, authoritative

    let private counts (st: InvestigationState) =
        [ "observations", st.Observations.Count; "hypotheses", st.Hypotheses.Count; "evidence", st.Evidence.Count
          "relationships", st.Relationships.Count; "unknowns", st.Unknowns.Count; "assumptions", st.Assumptions.Count
          "contradictions", st.Contradictions.Count; "predictions", st.Predictions.Count; "actions", st.Actions.Count
          "findings", st.Findings.Count; "negative knowledge", st.NegativeKnowledge.Count; "causes", st.Causes.Count
          "corrective actions", st.CorrectiveActions.Count; "criteria", st.Criteria.Count; "verifications", st.Verifications.Count
          "conclusions", st.Conclusions.Count; "proposals", st.Proposals.Count ]
        |> List.map (fun (k, n) -> [ "kind", k; "count", string n ])

    let project (m: Model) : JsonObject =
        let v = JsonObject()
        let set (k: string) (n: JsonNode) = v[k] <- n
        let draft k = m.Draft.TryFind k |> Option.defaultValue ""
        let isDouble = m.Conn |> Option.exists (fun c -> c.Api <> GitHub.realApi)
        set "notice" (s m.Notice)
        set "connected" (b m.Connected)
        set "connectionStatus" (s (match m.Conn with
                                   | Some c when m.Connected -> $"Connected to {c.Owner}/{c.Repo}@{c.Branch} via {c.Api}" + (if c.Token.IsSome then " (token held in memory)" else " (read-only, no token)")
                                   | Some _ -> "Connecting…"
                                   | None -> "Not connected"))
        set "providerIsDouble" (b (m.Connected && isDouble))
        set "providerWarning" (s "This provider is NOT api.github.com: persistence shown here is component behavior against a test double, not real persistence.")
        set "showConnect" (b (m.Route = ConnectScreen))
        set "onBrowse" (b (m.Route = Browse))
        set "onCreate" (b (m.Route = CreateScreen))
        for k in [ "api"; "owner"; "repo"; "branch"; "path"; "title"; "original" ] do set ("draft_" + k) (s (draft k))
        set "connectError" (s m.ConnectError)
        set "hasConnectError" (b (m.ConnectError <> ""))
        for k in [ "api"; "owner"; "repo"; "branch"; "path"; "title"; "original" ] do
            let e = m.FieldErrors.TryFind k |> Option.defaultValue ""
            set ("error_" + k) (s e)
            set ("hasError_" + k) (b (e <> ""))
            set ("invalid_" + k) (s (if e = "" then "false" else "true"))
        // browse
        let listed = match m.Listing with Listed ds -> ds | _ -> []
        set "listMessage" (s (match m.Listing with
                             | NotListed -> ""
                             | Listing -> "Loading the investigation list…"
                             | Listed [] -> "No investigations here yet."
                             | Listed ds -> $"{ds.Length} investigation(s)."
                             | ListFailed e -> "Could not list investigations: " + e))
        set "investigations" (items (listed |> List.map (fun d ->
            let title, status =
                match m.Loads.TryFind d with
                | Some(Loaded l) -> titleOf l, (if Layers.isVerified l then "verified: decoded, replayed, snapshot agrees" else "NOT verified")
                | Some(LoadFailed e) -> "(failed to load)", e
                | Some(Fetching _) -> "Loading…", "loading"
                | None -> d, "not loaded"
            [ "id", d; "title", title; "status", status ])))
        // open
        let opened = match m.Route with Open d -> Some d | _ -> None
        set "onOpen" (b opened.IsSome)
        let load = opened |> Option.bind m.Loads.TryFind
        let layers = match load with Some(Loaded l) -> Some l | _ -> None
        set "open_id" (s (defaultArg opened ""))
        set "open_loading" (b (match load with Some(Fetching _) -> true | _ -> false))
        set "open_failed" (s (match load with Some(LoadFailed e) -> e | _ -> ""))
        set "open_hasFailed" (b (match load with Some(LoadFailed _) -> true | _ -> false))
        set "open_loaded" (b layers.IsSome)
        let r, d, p, a = layers |> Option.map layerTexts |> Option.defaultValue ("", "", "", "")
        set "layer_raw" (s r); set "layer_decoded" (s d); set "layer_replayed" (s p); set "layer_authoritative" (s a)
        let st = layers |> Option.bind (fun l -> match l.Replayed with Ok r -> Some r.State | _ -> None)
        set "open_title" (s (st |> Option.map (fun x -> x.Title.Value) |> Option.defaultValue (defaultArg opened "")))
        set "open_lifecycle" (s (st |> Option.map (fun x -> WireEnum.toWire Enums.lifecycle x.Lifecycle) |> Option.defaultValue ""))
        set "open_version" (s (st |> Option.map (fun x -> string x.Version) |> Option.defaultValue ""))
        set "open_original" (s (st |> Option.bind (fun x -> x.Observations |> Map.toList |> List.tryHead |> Option.map (fun (_, o) -> o.Original.Value)) |> Option.defaultValue ""))
        set "counts" (items (st |> Option.map counts |> Option.defaultValue [] |> List.filter (fun r -> r[1] <> ("count", "0"))))
        set "hypotheses" (items (st |> Option.map (fun x -> x.Hypotheses |> Map.toList |> List.map (fun (k, h) -> [ "id", k.Text; "statement", short 240 h.Statement.Value; "state", WireEnum.toWire Enums.hypothesisState h.State ])) |> Option.defaultValue []))
        set "evidence" (items (st |> Option.map (fun x -> x.Evidence |> Map.toList |> List.truncate 50 |> List.map (fun (k, e) -> [ "id", k.Text; "content", short 240 e.Content.Value; "source", short 120 e.Source.Value ])) |> Option.defaultValue []))
        set "hasHypotheses" (b (st |> Option.exists (fun x -> not x.Hypotheses.IsEmpty)))
        set "hasEvidence" (b (st |> Option.exists (fun x -> not x.Evidence.IsEmpty)))
        // created by this session: save and read-back
        let created = opened |> Option.bind m.Creates.TryFind
        set "open_isCreated" (b created.IsSome)
        set "saveStatus" (s (match created with
                            | Some { Status = Saving; Written = w; Files = f } -> $"Saving… {w.Length}/{f.Length} files written."
                            | Some { Status = Saved } -> "Saved: all 3 files written to the repository."
                            | Some { Status = SaveFailed e } -> "Save FAILED: " + e
                            | None -> ""))
        set "readBackStatus" (s (match created with
                                | Some { ReadBack = ReadBackVerified h } -> $"Read-back verified: re-read from the provider, bytes identical to those written, decoded, replayed and the snapshot agrees (state sha256 {sha h})."
                                | Some { ReadBack = ReadBackFailed e } -> "Read-back FAILED: " + e
                                | Some { ReadBack = Reading _ } -> "Reading back from the provider…"
                                | _ -> ""))
        set "readBackVerified" (b (match created with Some { ReadBack = ReadBackVerified _ } -> true | _ -> false))
        set "canReadBack" (b (match created with Some { Status = Saved } -> true | _ -> false))
        set "canSave" (b (m.Conn |> Option.exists (fun c -> c.Token.IsSome)))
        v
