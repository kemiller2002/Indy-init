/// Layer A, fixture-backed component behaviour: the screens' required states and actions, driven through UiBackend.
module Causa.Ui.Tests.FixtureBackendTests

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.Ui.Fixtures
open Causa.Ui.Fixtures.Scenarios

let private run scenario route intents =
    let b = FixtureBackend.backend scenario
    let s0, _ = b.Init route
    let s = intents |> List.fold (fun s i -> fst (b.Dispatch i s)) s0
    s, b.Project s

let connectIntents =
    [ Input(Fields.repositoryOwner, "harness", None)
      Input(Fields.repositoryName, "causa-test", None)
      Input(Fields.branch, "main", None)
      Secret(Fields.token, Credential.create "secret-value")
      Activate(Actions.connect, None) ]

let private listOf (v: AppView) = match v.Screen with ListScreen l -> l | _ -> failwith "not the list"
let private workspaceOf (v: AppView) = match v.Screen with WorkspaceScreen w -> w | _ -> failwith "not the workspace"
let private analysisOf (w: WorkspaceView) =
    match w.Section with
    | OverviewSection(Ready a) | EvidenceView(Ready a) | HypothesesView(Ready a) | TimelineView(Ready a) | CausalAnalysisView(Ready a) -> Understood.value a
    | _ -> failwith "no projection"

[<Fact>]
let ``connected catalog lists the four stored cases with their stored titles; unresolved contradictions unknown`` () =
    let _, v = run ConnectedCatalog InvestigationsRoute connectIntents
    let l = listOf v
    match l.Investigations with
    | Ready items ->
        let titles = Visible.value items |> List.map (fun s -> s.Title)
        Assert.Equal(4, titles.Length)
        for c in CausaDataExtract.cases do Assert.Contains(c.Title, titles)
        for s in Visible.value items do
            Assert.Equal(UnknownCount, (s.Counts |> Option.get |> Shown.value).UnresolvedContradictions)
            Assert.Equal(FrozenDemonstration, s.Copy)
        Assert.True((Visible.fixtureLabel items).IsSome)
    | other -> Assert.Fail(sprintf "%A" other)
    Assert.Equal(Available, l.NewInvestigation)

[<Fact>]
let ``empty, unauthorized, network failure and loading are distinct states`` () =
    let state sc = (listOf (snd (run sc InvestigationsRoute connectIntents))).State
    Assert.Equal(ScreenEmpty, state EmptyCatalog)
    Assert.Equal(ScreenFailed NotAuthorized, state UnauthorizedRepository)
    Assert.Equal(ScreenFailed ProviderUnreachable, state NetworkFailure)
    Assert.Equal(ScreenLoading, state CatalogLoading)

[<Fact>]
let ``Connect with empty fields is refused per field and sends nothing`` () =
    let s, v = run ConnectedCatalog InvestigationsRoute [ Activate(Actions.connect, None) ]
    Assert.Equal(4, (listOf v).Connection.Errors.Length)
    Assert.Equal(Causa.App.Disconnected, s.Connection)

[<Fact>]
let ``Apollo 11 opens read-only with its stored problem statement and stored events`` () =
    let _, v = run ConnectedCatalog InvestigationsRoute (connectIntents @ [ Activate(Actions.openInvestigation, Some(Navigation.format (WorkspaceRoute(apollo11Ref, Overview)))) ])
    let w = workspaceOf v
    match w.Frame, w.Stored with
    | Ready f, Ready stored ->
        Assert.StartsWith("Historical investigation recorded from primary sources.", Shown.value f.ProblemStatement)
        Assert.Equal(FrozenDemonstration, f.Copy)
        Assert.Equal(DataVisible, Shown.capability f.ProblemStatement)
        Assert.Equal(6, (Visible.value stored).Events.Length)
        Assert.Equal("InvestigationStarted", (Visible.value stored).Events.Head.EventType)
    | other -> Assert.Fail(sprintf "%A" other)
    let a = analysisOf w
    Assert.All(a.Actions |> Map.toList, fun (_, av) -> Assert.True(match av with Unavailable _ -> true | _ -> false))
    // A relationship kind causa-data has and INV-031 does not is shown verbatim, never mapped.
    Assert.All(a.Relationships, fun l -> Assert.True(match l.Kind with CatalogKind _ | UninterpretedKind _ -> true))

[<Fact>]
let ``missing investigation and invalid JSON fail closed`` () =
    let _, v = run ConnectedCatalog InvestigationsRoute (connectIntents @ [ Navigate(WorkspaceRoute(missingRef, Overview)) ])
    Assert.Equal(ScreenFailed(InvestigationNotFound missingRef), (workspaceOf v).State)
    let _, v2 = run InvalidStoredJson InvestigationsRoute (connectIntents @ [ Navigate(WorkspaceRoute(apollo11Ref, Overview)) ])
    let w = workspaceOf v2
    Assert.True(match w.State, w.Stored, w.Section with ScreenFailed(InvalidData _), Failed(InvalidData _), OverviewSection(Failed _) -> true | _ -> false)

let private startIntents =
    [ Activate(Actions.newInvestigation, None)
      Input(Fields.problemStatement, "fixture problem", None)
      Input(Fields.title, "fixture title", None)
      Input(Fields.question, "fixture question", None)
      Activate(Actions.startInvestigation, None) ]

[<Fact>]
let ``Start validates, Cancel writes nothing, a valid Start opens an Unsaved workspace`` () =
    let s1, v1 = run ConnectedCatalog InvestigationsRoute (connectIntents @ [ Activate(Actions.newInvestigation, None); Input(Fields.problemStatement, "  ", None); Activate(Actions.startInvestigation, None) ])
    Assert.True(match v1.Screen with StartScreen st -> not st.Errors.IsEmpty | _ -> false)
    Assert.Empty(s1.Copies)
    let s2, _ = run ConnectedCatalog InvestigationsRoute (connectIntents @ [ Activate(Actions.newInvestigation, None); Input(Fields.problemStatement, "x", None); Activate(Actions.cancelStart, None) ])
    Assert.Empty(s2.Copies)
    Assert.Equal(InvestigationsRoute, s2.Route)
    let _, v3 = run ConnectedCatalog InvestigationsRoute (connectIntents @ startIntents)
    let w = workspaceOf v3
    Assert.Equal(UnsavedKind, w.Save.Value.Kind)
    Assert.Equal("Unsaved", w.Save.Value.Indicator.Label)
    match w.Frame with
    | Ready f -> Assert.Equal("fixture problem", Shown.value f.ProblemStatement); Assert.Equal(WorkingCopy, f.Copy)
    | other -> Assert.Fail(sprintf "%A" other)

[<Fact>]
let ``content editors: record, link both ways, refuse a duplicate link, reassess keeps the earlier assessment`` () =
    let record =
        [ Input(Fields.observationText, "obs", None); Input(Fields.observationSource, "src", None); Activate(Actions.recordObservation, None)
          Input(Fields.hypothesisStatement, "h1", None); Activate(Actions.recordHypothesis, None)
          Input(Fields.evidenceLabel, "e1", None); Input(Fields.evidenceContent, "content", None); Activate(Actions.recordEvidence, None) ]
    let s, v = run ConnectedCatalog InvestigationsRoute (connectIntents @ startIntents @ record)
    let a = analysisOf (workspaceOf v)
    let e, h = a.Evidence.Head, a.Hypotheses.Head
    let link kind why action = [ Input(Fields.linkEvidence, e.Id, None); Input(Fields.linkHypothesis, h.Id, None); Input(Fields.relationshipKind, kind, None); Input(Fields.relationshipRationale, why, None); Activate(action, None) ]
    let b = FixtureBackend.backend ConnectedCatalog
    let after intents = intents |> List.fold (fun st i -> fst (b.Dispatch i st)) s
    let linked = after (link "supports" "why" Actions.linkEvidence)
    let a2 = analysisOf (workspaceOf (b.Project linked))
    Assert.Equal("supports", (match a2.Evidence.Head.Links.Head.Kind with CatalogKind k -> ContentForms.kindValue k | UninterpretedKind k -> k))
    Assert.Equal(e.Id, a2.Hypotheses.Head.Links.Head.Evidence.Id)
    let dup = (link "contradicts" "again" Actions.linkEvidence) |> List.fold (fun st i -> fst (b.Dispatch i st)) linked
    Assert.NotEmpty(dup.DraftErrors)
    let reassessed = (link "inconclusive" "reassessed" Actions.reassess) |> List.fold (fun st i -> fst (b.Dispatch i st)) linked
    let a3 = analysisOf (workspaceOf (b.Project reassessed))
    Assert.Equal(2, a3.Relationships.Length)
    Assert.Equal(1, a3.Relationships |> List.filter (fun l -> l.Current) |> List.length)
    Assert.Contains(a3.Timeline, fun t -> t.Summary.StartsWith "supports")
    Assert.Contains(a3.Timeline, fun t -> t.Summary.StartsWith "inconclusive")

[<Fact>]
let ``an invalid editor input is refused with the field's message and records nothing`` () =
    let _, v = run ConnectedCatalog InvestigationsRoute (connectIntents @ startIntents @ [ Input(Fields.evidenceUrl, "javascript:alert(1)", None); Input(Fields.evidenceLabel, "e", None); Input(Fields.evidenceContent, "c", None); Activate(Actions.recordEvidence, None) ])
    let w = workspaceOf v
    Assert.Contains(w.Drafts.Errors, fun e -> e.Field = Fields.evidenceUrl)
    Assert.Empty((analysisOf w).Evidence)

[<Fact>]
let ``save scenarios: conflict keeps the changes and offers the frozen choices; unknown outcome is never Saved`` () =
    let route = WorkspaceRoute(FixtureBackend.seededCopyId, Overview)
    let save = connectIntents @ [ Navigate route; Activate(Actions.save, None) ]
    let conflict = workspaceOf (snd (run SaveConflict InvestigationsRoute save))
    Assert.Equal(ConflictKind, conflict.Save.Value.Kind)
    Assert.Equal<Causa.App.SaveAction list>([ Causa.App.Review; Causa.App.ReapplyMine; Causa.App.DiscardMine; Causa.App.ExportMine ], conflict.Save.Value.Conflict.Value.Choices)
    Assert.NotEmpty(conflict.Save.Value.NotSaved)
    let unknown = workspaceOf (snd (run UnknownSaveOutcome InvestigationsRoute save))
    Assert.Equal("Unknown commit outcome", unknown.Save.Value.Indicator.Label)
    let unsaved = workspaceOf (snd (run UnsavedChanges InvestigationsRoute (connectIntents @ [ Navigate route ])))
    Assert.Equal(UnsavedKind, unsaved.Save.Value.Kind)

[<Fact>]
let ``every fixture view says it is a fixture`` () =
    for sc in Scenarios.all do
        let _, v = run sc InvestigationsRoute connectIntents
        Assert.True(match v.Origin with FixtureBackend _ -> true | IntegratedBackend -> false)
        match v.Origin with
        | FixtureBackend l -> Assert.StartsWith("FIXTURE: ", FixtureLabel.value l)
        | IntegratedBackend -> ()

// ---- Regressions for the independent review (ADVERSARIAL-REVIEW.md R4) ----------------------------------------------

[<Fact>]
let ``R4a Save details changes the details, keeps the identifier, and makes the copy Unsaved`` () =
    let s, v = run ConnectedCatalog InvestigationsRoute (connectIntents @ startIntents @ [ Activate(Actions.save, None); Activate(Actions.editDetails, None); Input(Fields.detailsTitle, "renamed", None); Activate(Actions.saveDetails, None) ])
    let w = workspaceOf v
    match w.Frame with
    | Ready f ->
        Assert.Equal("renamed", f.Title)
        Assert.Equal(s.Copies.Head.Form.Identifier, f.Identifier)
    | other -> Assert.Fail(sprintf "%A" other)
    Assert.Equal(UnsavedKind, w.Save.Value.Kind)
    let _, bad = run ConnectedCatalog InvestigationsRoute (connectIntents @ startIntents @ [ Activate(Actions.editDetails, None); Input(Fields.detailsProblem, "  ", None); Activate(Actions.saveDetails, None) ])
    Assert.NotEmpty((workspaceOf bad).Drafts.Errors)

[<Fact>]
let ``R4b Disconnect keeps the tab's copies, and identifiers are never reused`` () =
    let again =
        [ Activate(Actions.disconnect, None) ] @ connectIntents
        @ [ Activate(Actions.newInvestigation, None); Input(Fields.problemStatement, "second", None); Activate(Actions.startInvestigation, None) ]
    let s, _ = run ConnectedCatalog InvestigationsRoute (connectIntents @ startIntents @ again)
    Assert.Equal(2, s.Copies.Length)
    Assert.Equal(2, s.Copies |> List.map (fun c -> c.Id) |> List.distinct |> List.length)
    Assert.True(s.Copies |> List.forall (fun c -> s.Analysis.ContainsKey c.Id && s.SaveKinds.ContainsKey c.Id))

[<Fact>]
let ``R4c a link to a non-existent target is refused and records nothing (INV-030)`` () =
    let link = [ Input(Fields.linkEvidence, "nope", None); Input(Fields.linkHypothesis, "nope", None); Input(Fields.relationshipKind, "supports", None); Input(Fields.relationshipRationale, "r", None); Activate(Actions.linkEvidence, None) ]
    let s0, _ = run ConnectedCatalog InvestigationsRoute (connectIntents @ startIntents @ [ Activate(Actions.save, None) ])
    let b = FixtureBackend.backend ConnectedCatalog
    let s = link |> List.fold (fun st i -> fst (b.Dispatch i st)) s0
    Assert.Equal(2, s.DraftErrors.Length)
    Assert.Equal(s0.Counter, s.Counter)
    Assert.Equal(SavedKind, s.SaveKinds[s.Copies.Head.Id])
