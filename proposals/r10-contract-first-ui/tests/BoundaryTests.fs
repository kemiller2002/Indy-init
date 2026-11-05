/// The architecture claims, checked: screens compile against the contracts alone; the contracts expose only allowlisted
/// frozen boundary types; the credential never reaches a binding; the frozen Engine can implement the same interface.
module Causa.Ui.Tests.BoundaryTests

open System
open System.Reflection
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.App
open Causa.Ui.Contracts
open Causa.Ui.Fixtures

let private screensAssembly = typeof<Causa.Ui.Prototype.Events.Marker>.Assembly

[<Fact>]
let ``the prototype screens reference only the UI contracts (no domain, persistence, fixture or adapter assembly)`` () =
    let refs = screensAssembly.GetReferencedAssemblies() |> Array.map (fun a -> a.Name |> string) |> Array.filter (fun n -> not (n.StartsWith "System" || n = "netstandard" || n = "FSharp.Core"))
    Assert.Equal<string[]>([| "Causa.Ui.Contracts" |], refs)

/// The frozen v4.1.4 types a presentation contract may expose. Anything else from Causa.Domain/Persistence/App in the
/// contracts' public surface is a coupling to domain or persistence internals and fails this test.
let allowedFrozen =
    set [ "Causa.Domain.InvestigationId"; "Causa.Domain.AggregateVersion"; "Causa.Domain.EventId"
          "Causa.App.ConnectionState"; "Causa.App.SaveAction"; "Causa.App.SaveIndicatorView"
          "Causa.Persistence.Politeness"; "Causa.Persistence.Credential+T"; "Causa.Persistence.RequestId" ]

let rec private named (t: Type) : Type list =
    if t.IsGenericType then t.GetGenericTypeDefinition() :: (t.GetGenericArguments() |> List.ofArray |> List.collect named)
    elif t.IsArray then named (t.GetElementType())
    else [ t ]

[<Fact>]
let ``the UI contracts expose no domain or persistence type outside the allowlist`` () =
    let contracts = typeof<Causa.Ui.Contracts.InvestigationSummary>.Assembly
    let surface =
        contracts.GetExportedTypes()
        |> Array.filter (fun t -> (t.Namespace |> string).StartsWith "Causa.Ui.Contracts")
        |> Array.collect (fun t ->
            let flags = BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static
            Array.concat
                [ t.GetProperties(flags) |> Array.map (fun p -> p.PropertyType)
                  t.GetMethods(flags) |> Array.collect (fun m -> Array.append [| m.ReturnType |] (m.GetParameters() |> Array.map (fun p -> p.ParameterType))) ])
        |> Array.collect (named >> Array.ofList)
        |> Array.filter (fun t -> let ns = t.Namespace |> string in ns = "Causa.Domain" || ns = "Causa.Persistence" || ns = "Causa.App")
        |> Array.map (fun t -> (t.FullName |> string))
        |> Array.distinct
    let outside = surface |> Array.filter (allowedFrozen.Contains >> not)
    Assert.True(outside.Length = 0, sprintf "contracts expose: %A" outside)

[<Fact>]
let ``the credential is never in any binding, in any scenario`` () =
    let secret = "secret-value"
    for sc in Scenarios.all do
        let b = FixtureBackend.backend sc
        let s0, _ = b.Init InvestigationsRoute
        let s = FixtureBackendTests.connectIntents |> List.fold (fun s i -> fst (b.Dispatch i s)) s0
        Assert.True(s.Credential.IsSome)
        let view = Causa.Ui.Prototype.InvestigationListScreen.bindings (b.Project s)
        Assert.DoesNotContain(secret, String.Join("\n", Bindings.texts view))
        Assert.DoesNotContain(Bindings.keys view, fun k -> k.Contains "TOKEN")
        Assert.Equal("<credential>", string s.Credential.Value)

[<Fact>]
let ``Limen events map mechanically to intents; the token becomes an opaque credential`` () =
    Assert.Equal(Some(Activate(Actions.openInvestigation, Some "k")), Causa.Ui.Prototype.Events.toIntent "INV-040" (Some "k") None)
    Assert.Equal(Some(Input(Fields.repositoryOwner, "o", None)), Causa.Ui.Prototype.Events.toIntent "UIF-CONN-OWNER" None (Some "o"))
    match Causa.Ui.Prototype.Events.toIntent "UIF-CONN-TOKEN" None (Some "t") with
    | Some(Secret(_, c)) -> Assert.Equal("<credential>", string c)
    | other -> Assert.Fail(sprintf "%A" other)
    Assert.Equal(None, Causa.Ui.Prototype.Events.toIntent "unknown-event" None None)

// ---- The same contract over the frozen Engine (real-adapter-sketch) -----------------------------------------------------

/// A stand-in Engine for the test only: it records what the adapter sends and answers Connect. It is not an engine.
let private stubEngine (sent: ResizeArray<Msg>) : Engine =
    { Init = fun route -> { Connection = Disconnected; Context = None; NextRequestId = RequestId 1L; Network = NetworkState.Online; Route = route; Loads = Map.empty; Sessions = Map.empty }, []
      Update =
        fun msg m ->
            sent.Add msg
            match msg with
            | Msg.Connection(ConnectionInput.ConnectSubmitted(repo, cred)) -> { m with Connection = Connected true; Context = Some { Repository = repo; Credential = cred } }, []
            | _ -> m, [] }

[<Fact>]
let ``the frozen Engine implements UiBackend: Connect reaches SM-CONNECTION; the list is an explicit contract gap`` () =
    let sent = ResizeArray()
    let b = Causa.Ui.RealAdapter.Sketch.backend (stubEngine sent)
    let s0, _ = b.Init InvestigationsRoute
    let s = FixtureBackendTests.connectIntents |> List.fold (fun s i -> fst (b.Dispatch i s)) s0
    Assert.Contains(sent, fun m -> match m with Msg.Connection(ConnectionInput.ConnectSubmitted(r, _)) -> r.Owner = "harness" && r.Name = "causa-test" && r.Branch = "main" | _ -> false)
    let v = b.Project s
    Assert.Equal(IntegratedBackend, v.Origin)
    match v.Screen with
    | ListScreen l ->
        Assert.Equal(Connected true, l.Connection.State)
        Assert.Equal(Failed Causa.Ui.RealAdapter.Sketch.listGap, l.Investigations)
    | other -> Assert.Fail(sprintf "%A" other)

[<Fact>]
let ``the seven frozen save states map one to one onto the presentation states`` () =
    let cases = Reflection.FSharpType.GetUnionCases(typeof<SaveState>)
    Assert.Equal(7, cases.Length)
    Assert.Equal(7, PresentationLanguage.allSaveStates.Length)
    let mapped = [ SaveState.Unsaved; SaveState.Saving; SaveState.Offline; SaveState.UnknownOutcome 1; SaveState.SaveFailed CredentialNotAccepted; SaveState.Conflict RemoteUnavailable ] |> List.map Causa.Ui.RealAdapter.Sketch.saveKind
    Assert.Equal<SaveStateKind list>([ UnsavedKind; SavingKind; OfflineKind; UnknownOutcomeKind; SaveFailedKind; ConflictKind ], mapped)
