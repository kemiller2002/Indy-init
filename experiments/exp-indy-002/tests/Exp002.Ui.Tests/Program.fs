/// UI tests: App.update and View.project against the TEMPORARY Stub ports. No NuGet; a tiny pure runner.
module Exp002.Ui.Tests.Program

open Exp002.Contracts
open Exp002.Ui
open Exp002.Ui.App
open Exp002.Ui.View

type Outcome = Pass | Fail of string

let check cond msg = if cond then Pass else Fail msg
let all (xs: Outcome list) = xs |> List.tryPick (function Fail m -> Some(Fail m) | Pass -> None) |> Option.defaultValue Pass

let ports = Stub.ports
let t0 = Timestamp 1_790_000_000_000L

let run (m: Model) (msgs: Msg list) =
    msgs |> List.fold (fun (m, acc) msg -> let m1, e = update ports t0 m msg in m1, acc @ e) (m, [])

let scalar key (view: (string * ViewValue) list) =
    view |> List.tryPick (fun (k, v) -> if k = key then (match v with Scalar(S s) -> Some s | Scalar(B b) -> Some(string b) | Scalar(N n) -> Some(string n) | _ -> None) else None)

let rows key (view: (string * ViewValue) list) =
    view |> List.tryPick (fun (k, v) -> if k = key then (match v with Items r -> Some r | _ -> None) else None) |> Option.defaultValue []

let field name (row: (string * Prim) list) = row |> List.tryPick (fun (k, p) -> if k = name then (match p with S s -> Some s | B b -> Some(string b) | N n -> Some(string n)) else None)

let storageSets effects = effects |> List.choose (function StorageSet(c, k, v) -> Some(c, k, v) | _ -> None)
let storageGets effects = effects |> List.choose (function StorageGet(c, k) -> Some(c, k) | _ -> None)

/// Starts an investigation through the start screen and completes its first save (write, read-back, index).
let started () =
    let m1, _ = run init [ Init "#/investigations/new" ]
    let m2, effects =
        run m1 [ Ui(SetField("INV-001", "Pump 3 tripped twice")); Ui(SetField("INV-002", "Pump trip")); Ui SubmitStart ]
    let (c, _, _) = storageSets effects |> List.head
    let m3, e3 = run m2 [ StorageResult(c, StorageValue None) ]
    let (rc, _) = storageGets e3 |> List.head
    let (_, _, written) = storageSets effects |> List.head
    let m4, e4 = run m3 [ StorageResult(rc, StorageValue(Some written)) ]
    m4, e4

let withEditor (m: Model) (editor: Editor) (fields: (string * string) list) =
    run m ([ Ui(OpenEditor editor) ] @ (fields |> List.map (fun (f, v) -> Ui(SetField(f, v)))) @ [ Ui SubmitEditor ]) |> fst

let tests: (string * (unit -> Outcome)) list =
    [ "UT-INV-005 start screen displays where the investigation is saved",
      fun () ->
          let m, _ = run init [ Init "#/investigations/new" ]
          let v = project m
          all [ check (m.Screen = StartScreen) "not on start screen"
                check (scalar "savedTo" v = Some "Saved to: this browser (local storage)") "savedTo text" ]

      "UT-INV-022 Cancel on the start screen returns to the list without an event or a write",
      fun () ->
          let m1, _ = run init [ Init "#/investigations/new" ]
          let m2, effects = run m1 [ Ui(SetField("INV-001", "draft")); Ui CancelStart ]
          all [ check (m2.Screen = ListScreen) "screen is not ListScreen"
                check (storageSets effects).IsEmpty "a storage write happened"
                check (m2.Workspace = None) "an investigation was created"
                check (effects |> List.exists (function NavigatePush(_, "#/") -> true | _ -> false)) "no navigation to #/" ]

      "UT-INV-036 Cancel in an editor closes it, discards the draft and records no event",
      fun () ->
          let m, _ = started ()
          let before = m.Workspace.Value.Events
          let m1, effects = run m [ Ui(OpenEditor NewObservationEditor); Ui(SetField("INV-024", "half typed")); Ui CancelEditor ]
          let m2, _ = run m1 [ Ui(OpenEditor NewObservationEditor) ]
          all [ check (m1.Workspace.Value.Editor = None) "editor still open"
                check (m1.Workspace.Value.Events = before) "an event was recorded"
                check effects.IsEmpty "effects were emitted"
                check (scalar "draftINV024" (project m2) = Some "") "draft not discarded" ]

      "UT-INV-039 the list region shows saved investigations by title",
      fun () ->
          let m1, effects = run init [ Init "#/" ]
          let (c, key) = storageGets effects |> List.head
          let index = encodeIndex [ { Id = "a"; Title = "Pump trip" }; { Id = "b"; Title = "Line\tbreak" } ]
          let m2, _ = run m1 [ StorageResult(c, StorageValue(Some index)) ]
          let titles = rows "investigations" (project m2) |> List.choose (field "title")
          all [ check (key = indexKey) "index key not read"
                check (titles = [ "Pump trip"; "Line\tbreak" ]) (sprintf "titles %A" titles) ]

      "UT-INV-040 the open link reads storage, decodes and replays into the workspace",
      fun () ->
          let saved, _ = started ()
          let (InvestigationId id) = saved.Workspace.Value.Id
          let fresh, _ = run init [ Init "#/" ]
          let link = rows "investigations" (project saved) |> List.head
          let m1, effects = run fresh [ Ui(OpenInvestigation(InvestigationId id)) ]
          let (c, key) = storageGets effects |> List.head
          let doc = ports.Encode { Id = InvestigationId id; Version = 1; Events = saved.Workspace.Value.Events }
          let m2, _ = run m1 [ StorageResult(c, StorageValue(Some doc)) ]
          let v = project m2
          all [ check (field "href" link = Some(sprintf "#/investigations/%s" id)) "link href"
                check (field "title" link = Some "Pump trip") "link accessible name"
                check (key = investigationKey id) "wrong storage key"
                check (scalar "busy" (project m1) = Some "True") "no loading state while in flight"
                check (m2.Screen = WorkspaceScreen(InvestigationId id)) "not on workspace"
                check (scalar "wsProblem" v = Some "Pump 3 tripped twice") "problem statement not replayed" ]

      "UI-INV-006 the identifier field is prefilled with a UUID",
      fun () ->
          let m, _ = run init [ Init "#/investigations/new" ]
          let rx = System.Text.RegularExpressions.Regex "^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$"
          check (rx.IsMatch m.Start.InvestigationId) (sprintf "not a UUID: %s" m.Start.InvestigationId)

      "UI-INV-019 field messages are shown by FieldId on the start screen",
      fun () ->
          let m, _ = run init [ Init "#/investigations/new"; Ui SubmitStart ]
          let v = project m
          all [ check (m.Screen = StartScreen) "left the start screen"
                check (scalar "startHasErrINV001" v = Some "True") "no INV-001 error flag"
                check (scalar "startInvalidINV001" v = Some "true") "aria-invalid not set" ]

      "UI-INV-016 Save writes, reads back, compares and reaches Saved; the index is updated",
      fun () ->
          let m, effects = started ()
          let v = project m
          let indexWrite = storageSets effects |> List.tryFind (fun (_, k, _) -> k = indexKey)
          all [ check (match m.Workspace.Value.Save with Saved(1, _) -> true | _ -> false) (sprintf "save state %A" m.Workspace.Value.Save)
                check (scalar "saveStateTone" v = Some "ok") "tone"
                check (indexWrite |> Option.exists (fun (_, _, text) -> decodeIndex text |> List.exists (fun e -> e.Title = "Pump trip"))) "index not written" ]

      "UI-INV-016 the lozenge never shows Saved before the read-back (CR-3)",
      fun () ->
          let m1, _ = run init [ Init "#/investigations/new" ]
          let m2, effects = run m1 [ Ui(SetField("INV-001", "p")); Ui SubmitStart ]
          let (c, _, _) = storageSets effects |> List.head
          let m3, _ = run m2 [ StorageResult(c, StorageValue None) ]
          all [ check (m2.Workspace.Value.Save = Saving) "not Saving while the write is in flight"
                check (m3.Workspace.Value.Save = Saving) (sprintf "after write, before read-back: %A" m3.Workspace.Value.Save)
                check (scalar "saveStateText" (project m3) = Some "Saving…") "lozenge text" ]

      "UI-INV-015 a read-back mismatch is not reported as Saved",
      fun () ->
          let m, _ = started ()
          let m1, e1 = run m [ Ui(OpenEditor NewObservationEditor); Ui(SetField("INV-024", "Seal leak")); Ui SubmitEditor; Ui SaveInvestigation ]
          let (c, _, _) = storageSets e1 |> List.head
          let m2, e2 = run m1 [ StorageResult(c, StorageValue None) ]
          let (rc, _) = storageGets e2 |> List.head
          let m3, _ = run m2 [ StorageResult(rc, StorageValue(Some "something else")) ]
          all [ check (match m3.Workspace.Value.Save with Saved _ -> false | _ -> true) "reported Saved"
                check m3.Alert.IsSome "no error alert" ]

      "UI-INV-031 the relationship radio group has nothing selected initially",
      fun () ->
          let m, _ = started ()
          let m1 = withEditor m NewEvidenceEditor [ "INV-027", "Log"; "INV-012", "trip at 03:00" ]
          let m2 = withEditor m1 NewHypothesisEditor [ "INV-026", "Bearing wear"; "INV-011", "trips" ]
          let m3, _ = run m2 [ Ui(OpenEditor NewRelationshipEditor) ]
          let v = project m3
          all [ for k in [ "kindSupports"; "kindContradicts"; "kindConsistentWith"; "kindInconclusive" ] do
                    check (scalar k v = Some "False") (k + " preselected")
                check (rows "evidenceOptions" v |> List.forall (fun r -> field "selected" r = Some "False")) "evidence preselected" ]

      "UI-INV-044 a link is shown on both the evidence and the hypothesis",
      fun () ->
          let m, _ = started ()
          let m1 = withEditor m NewEvidenceEditor [ "INV-027", "Log"; "INV-012", "trip at 03:00"; "INV-029", "javascript:alert(1)" ]
          let m2 = withEditor m1 NewHypothesisEditor [ "INV-026", "Bearing wear"; "INV-011", "trips" ]
          let m3 = withEditor m2 NewRelationshipEditor [ "INV-030-evidence", "ev-1"; "INV-030-hypothesis", "hyp-1"; "INV-031", "supports"; "INV-038", "timing" ]
          let v = project m3
          let ev = rows "evidence" v |> List.head
          let hy = rows "hypotheses" v |> List.head
          all [ check (field "links" ev = Some "Supports: Bearing wear") (sprintf "evidence links %A" (field "links" ev))
                check (field "links" hy = Some "Supports: Log") "hypothesis links"
                check (field "hasLinkUrl" ev = Some "False") "unsafe URL shown as a link" ]

      "UI-INV-038 Withdraw takes its reason from the Rationale field",
      fun () ->
          let m, _ = started ()
          let m1 = withEditor m NewEvidenceEditor [ "INV-027", "Log"; "INV-012", "x" ]
          let m2 = withEditor m1 NewHypothesisEditor [ "INV-026", "H"; "INV-011", "y" ]
          let m3 = withEditor m2 NewRelationshipEditor [ "INV-030-evidence", "ev-1"; "INV-030-hypothesis", "hyp-1"; "INV-031", "contradicts"; "INV-038", "r" ]
          let rid = m3.Workspace.Value.Investigation.Relationships.Head.Id
          let m4, _ = run m3 [ Ui(OpenEditor(RelationshipEditor rid)); Ui(SetField("INV-038", "recorded in error")); Ui(WithdrawSelected rid) ]
          check (m4.Workspace.Value.Investigation.Relationships.Head.Status = Withdrawn "recorded in error") "reason not used"

      "UI-INV-035 edits keep identity and appear in history",
      fun () ->
          let m, _ = started ()
          let m1 = withEditor m NewObservationEditor [ "INV-024", "first" ]
          let oid = m1.Workspace.Value.Investigation.Observations.Head.Id
          let m2 = withEditor m1 (ObservationEditor oid) [ "INV-024", "second" ]
          let inv = m2.Workspace.Value.Investigation
          all [ check (inv.Observations.Length = 1 && inv.Observations.Head.Id = oid) "identity changed"
                check ((rows "history" (project m2)).Length = 3) "history length" ]

      "UI-ROUTE hash routes map to screens",
      fun () ->
          all [ check (parseRoute "#/" = ListRoute) "list"
                check (parseRoute "" = ListRoute) "empty"
                check (parseRoute "#/investigations/new" = StartRoute) "start"
                check (parseRoute "#/investigations/abc" = WorkspaceRoute "abc") "workspace" ]

      "UI-DISPATCH Limen messages decode to UiActions and responses carry view, effects, cancellations",
      fun () ->
          let m, json = Dispatch.step ports t0 init """{"kind":"Initialize","protocolVersion":1,"capabilities":[],"location":{"origin":"x","path":"/","query":"","hash":"#/"}}"""
          let _, json2 = Dispatch.step ports t0 m """{"kind":"Event","event":{"kind":"Event","name":"field:INV-001","value":"abc"}}"""
          all [ check (json.Contains "\"view\"" && json.Contains "\"cancellations\":[]" && json.Contains "\"operation\":\"get\"") json
                check (json2.Contains "\"effects\":[]") json2
                check (Events.decode "field:INV-031" None (Some "supports") = Some(SetField("INV-031", "supports"))) "field decode"
                check (Events.decode "withdraw-relationship" (Some "r1") None = Some(WithdrawSelected(RelationshipId "r1"))) "withdraw decode" ] ]

[<EntryPoint>]
let main _ =
    let results =
        tests |> List.map (fun (name, f) -> name, (try f () with ex -> Fail(sprintf "threw %s" ex.Message)))
    for (name, r) in results do
        match r with
        | Pass -> printfn "PASS %s" name
        | Fail m -> printfn "FAIL %s: %s" name m
    let failed = results |> List.filter (snd >> (<>) Pass) |> List.length
    printfn "%d tests, %d passed, %d failed" results.Length (results.Length - failed) failed
    if failed = 0 then 0 else 1
