// Static binding check for web/index.html (EXP-INDY-002 UI).
//  B1 every data-event decodes to a UiAction through Events.decode (the decoder Dispatch uses);
//  B2 every data-text / data-bind-* / data-if / data-each key (and data-each item field) is projected by View.project;
//  B3 every CONTRACT-REGISTRY entry for the UI's three screens has an element with that data-requirement-id.
// Usage: dotnet fsi tools/ui-binding-check.fsx   (exit 1 on any failure)
#load "../src/Exp002.Contracts/Contracts.fs"
#load "../src/Exp002.Ui/Stub.fs" "../src/Exp002.Ui/App.fs" "../src/Exp002.Ui/View.fs" "../src/Exp002.Ui/Events.fs"

open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open Exp002.Contracts
open Exp002.Ui
open Exp002.Ui.App
open Exp002.Ui.View

let root = Path.Combine(__SOURCE_DIRECTORY__, "..")
let html = File.ReadAllText(Path.Combine(root, "web", "index.html"))
let registryPath = Path.Combine(root, "..", "..", "docs", "experiments", "exp-indy-002", "CONTRACT-REGISTRY.json")

let attrValues (pattern: string) =
    Regex.Matches(html, pattern) |> Seq.map (fun m -> m.Groups.[1].Value) |> Seq.distinct |> List.ofSeq

// ---- representative models covering every screen and editor ----
let t = Timestamp 1_790_000_000_000L
let run m msgs = msgs |> List.fold (fun (m, _) msg -> update Stub.ports t m msg) (m, [])
let startM, _ = run init [ Init "#/investigations/new" ]
let wsM =
    let m, e = run startM [ Ui(SetField("INV-001", "p")); Ui SubmitStart ]
    let steps editor fields = [ Ui(OpenEditor editor) ] @ (fields |> List.map (fun (f, v) -> Ui(SetField(f, v)))) @ [ Ui SubmitEditor ]
    run m (steps NewObservationEditor [ "INV-024", "o"; "INV-025", "s" ]
           @ steps NewHypothesisEditor [ "INV-026", "h"; "INV-011", "x" ]
           @ steps NewEvidenceEditor [ "INV-027", "l"; "INV-012", "c"; "INV-029", "https://example.org" ]
           @ steps NewRelationshipEditor [ "INV-030-evidence", "ev-1"; "INV-030-hypothesis", "hyp-1"; "INV-031", "supports" ]
           @ steps NewConclusionEditor [ "INV-033", "c" ]) |> fst
let editors =
    [ DetailsEditor; NewObservationEditor; NewHypothesisEditor; NewEvidenceEditor; NewRelationshipEditor
      RelationshipEditor(RelationshipId "rel-1"); NewConclusionEditor ]
let models =
    [ init; startM; { init with Index = [ { Id = "a"; Title = "t" } ] }; wsM ]
    @ (editors |> List.map (fun e -> run wsM [ Ui(OpenEditor e) ] |> fst))
let views = models |> List.map project
let topKeys = views |> List.collect (List.map fst) |> Set.ofList
let itemKeys =
    views |> List.collect (List.collect (fun (_, v) -> match v with Items rows -> rows |> List.collect (List.map fst) | _ -> [])) |> Set.ofList
let projected = Set.union topKeys itemKeys

// ---- B1 ----
let events = attrValues "data-event=\"([^\"]+)\""
let b1 = events |> List.filter (fun n -> (Events.decode n (Some "k") (Some "v")).IsNone) |> List.map (sprintf "B1 data-event \"%s\" decodes to no UiAction")

// ---- B2 ----
let boundKeys =
    attrValues "data-text=\"([^\"]+)\"" @ attrValues "data-bind-[a-z-]+=\"([^\"]+)\"" @ attrValues "data-if=\"([^\"]+)\""
    @ attrValues "data-key=\"([^\"]+)\""
let eachKeys = attrValues "data-each=\"([^\"]+)\""
let b2 =
    (boundKeys |> List.filter (projected.Contains >> not) |> List.map (sprintf "B2 key \"%s\" is not projected by View"))
    @ (eachKeys |> List.filter (topKeys.Contains >> not) |> List.map (sprintf "B2 list \"%s\" is not projected by View"))

// ---- B3 ----
let uiScreens = set [ "investigation-list"; "investigation-start"; "investigation-workspace" ]
let requirementIds = attrValues "data-requirement-id=\"([^\"]+)\"" |> Set.ofList
let registry = JsonDocument.Parse(File.ReadAllText registryPath)
let registryIds =
    [ for c in registry.RootElement.GetProperty("contracts").EnumerateArray() do
          let screen = match c.TryGetProperty "screen" with | true, s -> s.GetString() | _ -> ""
          if uiScreens.Contains screen then c.GetProperty("id").GetString() ]
let b3 = registryIds |> List.filter (requirementIds.Contains >> not) |> List.map (sprintf "B3 %s has no data-requirement-id element")

let failures = b1 @ b2 @ b3
printfn "ui-binding-check: %d events, %d bound keys, %d registry entries, %d requirement ids" events.Length (boundKeys.Length + eachKeys.Length) registryIds.Length requirementIds.Count
failures |> List.iter (printfn "FAIL %s")
if failures.IsEmpty then printfn "PASS (B1-B3)" else exit 1
