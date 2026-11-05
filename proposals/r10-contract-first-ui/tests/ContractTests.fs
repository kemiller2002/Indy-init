module Causa.Ui.Tests.ContractTests

open System
open Xunit
open Causa.Domain
open Causa.Ui.Contracts

[<Fact>]
let ``exactly seven save states with the PERSIST-031 words`` () =
    Assert.Equal<string list>(
        [ "Saved"; "Unsaved"; "Saving"; "Offline"; "Conflict"; "Unknown commit outcome"; "Save failed" ],
        PresentationLanguage.allSaveStates |> List.map PresentationLanguage.saveState)

[<Fact>]
let ``validation: whitespace-only problem statement is refused (INV-019), limits hold (INV-020, INV-021)`` () =
    let form = StartForm.empty "c6b26614-fc43-5013-a15c-cf95942ee07b"
    Assert.NotEmpty(StartForm.validate { form with ProblemStatement = "   " })
    Assert.Empty(StartForm.validate { form with ProblemStatement = "x" })
    Assert.NotEmpty(StartForm.validate { form with ProblemStatement = "x"; Title = String('t', 201) })
    Assert.Empty(StartForm.validate { form with ProblemStatement = "x"; Title = String('t', 200) })
    Assert.NotEmpty(StartForm.validate { form with ProblemStatement = String('p', 10001) })
    Assert.NotEmpty(StartForm.validate { form with ProblemStatement = "x"; Identifier = "not-a-guid" })

[<Fact>]
let ``relationship kind accepts only INV-031's four options`` () =
    let kind = ContentForms.link |> List.find (fun f -> f.Field = Fields.relationshipKind)
    for ok in [ "supports"; "contradicts"; "consistent-with"; "inconclusive" ] do Assert.Empty(Validation.check kind ok)
    Assert.NotEmpty(Validation.check kind "discriminates")
    Assert.NotEmpty(Validation.check kind "")

[<Fact>]
let ``only absolute http(s) URLs become SafeUrl (INV-029, UI-005)`` () =
    Assert.True((SafeUrl.tryCreate "https://www.hq.nasa.gov/alsj/").IsSome)
    for bad in [ "javascript:alert(1)"; "ftp://x"; "/relative"; "https://a b"; "data:text/html,x" ] do
        Assert.True((SafeUrl.tryCreate bad).IsNone, bad)

[<Fact>]
let ``routes round-trip; unknown ids and paths are not-found`` () =
    let id : Causa.Persistence.InvestigationRef =
        { Address = { Workspace = Causa.Persistence.Workspace "workspaces/demo/demonstration-v1"; Role = Causa.Persistence.WorkspaceRole.Catalog }
          Id = InvestigationId(Guid.Parse "e3adbee5-5096-5a10-bbfb-cecda86a154b") }
    let routes = [ InvestigationsRoute; StartRoute; AdministrationRoute; SettingsRoute ] @ (Navigation.sections |> List.map (fun s -> WorkspaceRoute(id, s)))
    for r in routes do Assert.Equal(r, Navigation.parse (Navigation.format r))
    Assert.Equal("#/investigations/e3adbee5-5096-5a10-bbfb-cecda86a154b?ws=workspaces%2Fdemo%2Fdemonstration-v1&role=catalog", Navigation.format (WorkspaceRoute(id, Overview)))
    // The same id in another workspace is another route (v4.1.5 W1).
    let other = { id with Address = { Workspace = Causa.Persistence.Workspace "workspaces/demo/historical-v1"; Role = Causa.Persistence.WorkspaceRole.Catalog } }
    Assert.NotEqual<string>(Navigation.format (WorkspaceRoute(id, Overview)), Navigation.format (WorkspaceRoute(other, Overview)))
    // Without its workspace a route identifies nothing.
    Assert.True(match Navigation.parse "#/investigations/e3adbee5-5096-5a10-bbfb-cecda86a154b" with NotFoundRoute _ -> true | _ -> false)
    Assert.True(match Navigation.parse "#/investigations/not-a-guid" with NotFoundRoute _ -> true | _ -> false)
    Assert.True(match Navigation.parse "#/investigations/e3adbee5-5096-5a10-bbfb-cecda86a154b?ws=w&role=catalog&tab=bogus" with NotFoundRoute _ -> true | _ -> false)

[<Fact>]
let ``primary navigation has exactly one current destination on every route`` () =
    for r in [ InvestigationsRoute; StartRoute; AdministrationRoute; SettingsRoute; NotFoundRoute "x" ] do
        Assert.Equal(1, Navigation.primary r |> List.filter (fun l -> l.Current) |> List.length)

[<Fact>]
let ``corrective actions appear only where supported`` () =
    let id : Causa.Persistence.InvestigationRef = { Address = { Workspace = Causa.Persistence.Workspace "w"; Role = Causa.Persistence.WorkspaceRole.WorkingCopy }; Id = InvestigationId(Guid.NewGuid()) }
    Assert.DoesNotContain(CorrectiveActions, Navigation.sectionLinks id Overview false |> List.map (fun l -> l.Destination))
    Assert.Contains(CorrectiveActions, Navigation.sectionLinks id Overview true |> List.map (fun l -> l.Destination))

[<Fact>]
let ``failure copy says what happened, what is kept and what next, without technical words (UI-010, UI-017, UI-023)`` () =
    let failures =
        [ InvestigationNotFound { Address = { Workspace = Causa.Persistence.Workspace "w"; Role = Causa.Persistence.WorkspaceRole.Catalog }; Id = InvestigationId(Guid.NewGuid()) }; InvalidData "d"; NotAuthorized; CredentialRefused; ProviderUnreachable
          ProviderRateLimited; ProviderError "p"; CapabilityAbsent(DataUnderstood, "x") ]
    for f in failures do
        let c = PresentationLanguage.failure f
        for t in [ c.What; c.Preserved; c.NextStep ] do
            Assert.False(String.IsNullOrWhiteSpace t)
            for w in PresentationLanguage.forbiddenInInvestigationCopy do Assert.DoesNotContain(w, t)

[<Fact>]
let ``a stored value cannot be presented as projected`` () =
    // There is no Visible -> Understood conversion in the contract; Shown keeps the boundary visible to the view.
    let lbl = FixtureLabel.create "test"
    let shown = AsStored(Visible.fixture lbl "active")
    Assert.Equal(DataVisible, Shown.capability shown)
    Assert.True(Shown.isFixture shown)
    let asm = typeof<Visible<string>>.Assembly
    let visibleModule = asm.GetTypes() |> Array.find (fun t -> t.Namespace = "Causa.Ui.Contracts" && t.Name.StartsWith "Visible" && t.IsAbstract && t.IsSealed)
    let understood = typedefof<Understood<string>>
    for m in visibleModule.GetMethods() do
        Assert.False(m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() = understood, m.Name)
