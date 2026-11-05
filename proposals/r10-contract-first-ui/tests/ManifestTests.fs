/// SCREEN-MANIFEST.json against its approved sources: nothing dropped, nothing invented, ids equal the F# contracts.
module Causa.Ui.Tests.ManifestTests

open System.Text.Json
open Xunit
open Causa.Ui.Contracts
open Causa.Ui.Tests.Support

let manifest = proposal "SCREEN-MANIFEST.json"
let catalog = json "requirements/catalogs/causa-investigation.requirement-catalog.json"
let scope = json "docs/rehearsal-010-readiness/scope-manifest.json"
let bindings = json "verification/journeys/bindings.json"
let screens = arr manifest "screens"
let catalogEntries = screens |> List.collect (fun s -> arr s "catalogEntries" |> List.map (fun e -> str s "id" |> Option.get, e))
let additional = screens |> List.collect (fun s -> arr s "additionalEntries")
let allManifestIds = (catalogEntries |> List.map (snd >> fun e -> str e "id" |> Option.get)) @ (additional |> List.map (fun e -> str e "id" |> Option.get)) @ (screens |> List.collect (fun s -> arr s "regions" |> List.map (fun r -> str r "id" |> Option.get)))

[<Fact>]
let ``every catalog entry is on exactly one screen, the catalog's own`` () =
    let reqs = arr catalog "requirements"
    let placed = catalogEntries |> List.map (fun (screen, e) -> str e "id" |> Option.get, screen)
    for r in reqs do
        let id = str r "id" |> Option.get
        let onScreens = placed |> List.filter (fst >> (=) id) |> List.map snd
        Assert.True((onScreens = [ str r "screen" |> Option.get ]), sprintf "%s is on %A, the catalog says %A" id onScreens (str r "screen"))
    Assert.Equal(reqs.Length, catalogEntries.Length)

[<Fact>]
let ``catalog fields are copied, not rewritten: name, component, required, validation`` () =
    let byId = arr catalog "requirements" |> List.map (fun r -> str r "id" |> Option.get, r) |> Map.ofList
    for (_, e) in catalogEntries do
        let c = byId[str e "id" |> Option.get]
        for k in [ "name"; "component" ] do Assert.Equal(str c k, str e k)
        Assert.Equal(c.GetProperty("required").GetBoolean(), e.GetProperty("required").GetBoolean())
        match c.TryGetProperty "validation", e.TryGetProperty "validation" with
        | (true, cv), (true, ev) -> Assert.Equal(JsonSerializer.Serialize(cv), JsonSerializer.Serialize(ev))
        | (false, _), (false, _) -> ()
        | _ -> Assert.Fail(sprintf "%s validation differs" (str e "id" |> Option.get))

[<Fact>]
let ``every in-scope UI requirement is placed on a screen or as cross-cutting`` () =
    let inScope = arr scope "requirements" |> List.choose (fun r -> str r "id") |> List.filter (fun i -> i.StartsWith "UI-")
    let placed = (screens |> List.collect (fun s -> strs s "requirements")) @ (arr manifest "crossCutting" |> List.choose (fun c -> str c "id")) |> set
    let missing = inScope |> List.filter (placed.Contains >> not)
    Assert.True(missing.IsEmpty, sprintf "unplaced: %A" missing)

[<Fact>]
let ``out-of-scope UI requirements are exactly those the scope manifest omits`` () =
    let inScope = arr scope "requirements" |> List.choose (fun r -> str r "id") |> set
    for o in arr manifest "outOfScopeUiRequirements" do
        Assert.False(inScope.Contains(str o "id" |> Option.get), sprintf "%A is in scope" (str o "id"))

[<Fact>]
let ``every P0 journey assertion that names a UI or catalog requirement is reachable from a screen`` () =
    let placed = (screens @ arr manifest "crossCutting") |> List.collect (fun s -> strs s "journeyAssertions") |> set
    let p0 =
        arr bindings "journeys" |> List.collect (fun j -> arr j "assertions")
        |> List.filter (fun a -> str a "priority" = Some "P0" && strs a "requirements" |> List.exists (fun r -> r.StartsWith "UI-" || r.StartsWith "INV-"))
        |> List.choose (fun a -> str a "id")
    let missing = p0 |> List.filter (placed.Contains >> not)
    Assert.True(missing.IsEmpty, sprintf "P0 assertions on no screen: %A" missing)

[<Fact>]
let ``every non-catalog id cites a source`` () =
    for e in additional do
        Assert.False(System.String.IsNullOrWhiteSpace(str e "source" |> Option.defaultValue ""), sprintf "%A has no source" (str e "id"))

let private fieldIds = Connection.fields @ StartForm.fields @ DetailsForm.fields @ ContentForms.all |> List.map (fun f -> FieldId.value f.Field)
let private actionIds = ActionContracts.all |> List.map (fun a -> ActionId.value a.Action)

[<Fact>]
let ``every F# field and action id is in the manifest, and every manifest UIF/UIA id is in F#`` () =
    let ids = set allManifestIds
    for i in fieldIds @ actionIds do Assert.True(ids.Contains i, sprintf "%s is not in SCREEN-MANIFEST.json" i)
    let fsharp = set (fieldIds @ actionIds)
    for i in allManifestIds |> List.filter (fun i -> i.StartsWith "UIF-" || i.StartsWith "UIA-") do
        Assert.True(fsharp.Contains i, sprintf "%s is in the manifest but in no F# contract" i)

[<Fact>]
let ``screen ids and routes equal the catalog's and the F# navigation`` () =
    let manifestScreens = screens |> List.map (fun s -> str s "id" |> Option.get) |> set
    Assert.Equal<Set<string>>(Screens.all |> List.map ScreenId.value |> set, manifestScreens)
    for c in arr catalog "screens" do
        let s = screens |> List.find (fun s -> str s "id" = str c "id")
        Assert.Equal(str c "route", str s "route")
    Assert.Equal("#/", Navigation.format InvestigationsRoute)
    Assert.Equal("#/investigations/new", Navigation.format StartRoute)

[<Fact>]
let ``F# validation rules equal the catalog validation blocks`` () =
    let byName = arr catalog "requirements" |> List.map (fun r -> str r "id" |> Option.get, r) |> Map.ofList
    for spec in StartForm.fields @ ContentForms.all do
        let id = FieldId.value spec.Field
        match byName |> Map.tryFind id with
        | None -> ()
        | Some c ->
            Assert.Equal(str c "name", Some spec.Label)
            match c.TryGetProperty "validation" with
            | true, v ->
                let intOf (k: string) = match v.TryGetProperty k with | true, x -> Some(x.GetInt32()) | _ -> None
                Assert.Equal(intOf "maxLength", spec.Rules |> List.tryPick (function MaxLength n -> Some n | _ -> None))
                Assert.Equal(intOf "minLength", spec.Rules |> List.tryPick (function MinLength n -> Some n | _ -> None))
                Assert.Equal(str v "pattern", spec.Rules |> List.tryPick (function Pattern p -> Some p | _ -> None))
                let options = match v.TryGetProperty "options" with | true, o -> Some(o.EnumerateArray() |> Seq.map (fun x -> x.GetString() |> string) |> List.ofSeq) | _ -> None
                Assert.Equal(options, spec.Rules |> List.tryPick (function OneOf o -> Some o | _ -> None))
            | _ -> ()
    // The catalog's validation rules on the start fields (INV-019, INV-020, INV-021) are carried by INV-001/INV-002.
    let problem = StartForm.fields |> List.find (fun f -> f.Field = Fields.problemStatement)
    Assert.Contains(MinLength 1, problem.Rules)
    Assert.Contains(MaxLength 10000, problem.Rules)

[<Fact>]
let ``every name the governed journeys look up exists as a field label or action label`` () =
    // Copied from verification/journeys/*.spec.mjs and support.mjs (fieldNamed, button, section).
    let journeyNames =
        [ "Problem statement"; "Title"; "Primary investigative question"; "Investigation identifier"; "Start investigation"
          "Observation text"; "Observation source reference"; "Record an observation"; "Hypothesis statement"; "Record a hypothesis"
          "Evidence label"; "Evidence source reference"; "Evidence original content"; "Evidence relationship rationale"
          "Evidence relationship kind"; "Link evidence to a hypothesis"; "Reassess"; "Conclusion statement"; "Record a conclusion"; "Save" ]
    let labels = set ((Connection.fields @ StartForm.fields @ ContentForms.all |> List.map (fun f -> f.Label)) @ (ActionContracts.all |> List.map (fun a -> a.Label)))
    for n in journeyNames do Assert.True(labels.Contains n, sprintf "no field or action is labelled %s" n)
    for s in [ "Overview"; "Causal Analysis"; "Evidence"; "Hypotheses"; "Timeline" ] do
        Assert.Contains(s, Navigation.sections |> List.map Navigation.sectionLabel)
    Assert.Equal<string list>([ "Investigations"; "Administration"; "Settings" ], Navigation.destinations |> List.map Navigation.destinationLabel)
