/// The integration adapter's shape: UiBackend over the frozen Engine. Connect, navigation and the seven save states map
/// completely; the investigation list and the workspace content cannot, and the reason is a value, not a guess.
module Causa.Ui.RealAdapter.Sketch

open Causa.Domain
open Causa.Persistence
open Causa.App
open Causa.Ui.Contracts

/// UI-local text the frozen Model does not hold (form fields before submit). The credential is the frozen opaque type.
type Local = { Form: ConnectForm; Credential: Credential.T option; Errors: ValidationError list; Route: UiRoute; Drafts: Map<FieldId, string> }

type State = { Model: Model; Local: Local }

/// Total over the frozen SM-SAVE states: an eighth state, or a missing one, fails to compile (PERSIST-031 "exactly seven").
let saveKind (s: SaveState) : SaveStateKind =
    match s with
    | SaveState.Saved _ -> SavedKind
    | SaveState.Unsaved -> UnsavedKind
    | SaveState.Saving -> SavingKind
    | SaveState.Offline -> OfflineKind
    | SaveState.Conflict _ -> ConflictKind
    | SaveState.UnknownOutcome _ -> UnknownOutcomeKind
    | SaveState.SaveFailed _ -> SaveFailedKind

/// The gaps, as values the views render (DATA-REPOSITORY-CONTRACT.md §6 on claude/r10-optimization; ODR-R10-25/-26).
let listGap = CapabilityAbsent(DataVisible, "S1: the frozen Model, Msg, LoadInput and Connector have no investigation listing")
let contentGap = CapabilityAbsent(DataUnderstood, "C1/C5: the frozen Investigation stub carries Id and Version only; content payloads are opaque")

let private connect (engine: Engine) (s: State) =
    match Connection.validate s.Local.Form, s.Local.Credential with
    | [], Some credential ->
        // C3: the frozen Repository has no workspace; causa-data addresses investigations by workspace.
        let repository = { Owner = s.Local.Form.Owner; Name = s.Local.Form.Repository; Branch = s.Local.Form.Branch }
        let m, fx = engine.Update (Msg.Connection(ConnectionInput.ConnectSubmitted(repository, credential))) s.Model
        { s with Model = m; Local = { s.Local with Errors = [] } }, fx
    | errors, _ -> { s with Local = { s.Local with Errors = errors } }, []

let private currentId s = match s.Local.Route with WorkspaceRoute(id, _) -> Some id | _ -> None

let private dispatch (engine: Engine) (intent: UiIntent) (s: State) : State * HostEffect list =
    match intent with
    | Navigate r ->
        let m, fx = engine.Update (Msg.Navigate(Route(Navigation.format r))) s.Model
        { Model = m; Local = { s.Local with Route = r } }, fx
    | Input(f, v, _) when Connection.fields |> List.exists (fun x -> x.Field = f) ->
        { s with Local = { s.Local with Form = Connection.withField f v s.Local.Form } }, []
    // Start and content fields wait for the domain owner's command payloads (C1, C5); kept, not dropped.
    | Input(f, v, _) -> { s with Local = { s.Local with Drafts = s.Local.Drafts |> Map.add f v } }, []
    | Secret(_, c) -> { s with Local = { s.Local with Credential = Some c; Form = { s.Local.Form with CredentialEntered = true } } }, []
    | Activate(a, _) when a = Actions.connect -> connect engine s
    | Activate(a, _) when a = Actions.disconnect ->
        let m, fx = engine.Update (Msg.Connection ConnectionInput.DisconnectConfirmed) s.Model
        { s with Model = m }, fx
    | Activate(a, _) when a = Actions.save ->
        match currentId s with
        | Some id ->
            let m, fx = engine.Update (Msg.SessionInput(id.Id, SaveInput.SaveRequested)) s.Model
            { s with Model = m }, fx
        | None -> s, []
    // Content commands need a CommandEnvelope whose Command payloads are opaque in the frozen stubs (C1, C5): the
    // adapter cannot build RecordObservation etc. until the domain owner defines them.
    | Activate _ -> s, []

let private connectionView (s: State) : ConnectionView =
    { State = s.Model.Connection
      Form = s.Local.Form
      Errors = s.Local.Errors
      Connect = (match s.Model.Connection with Connected _ | Connecting _ -> NotOffered | _ -> Available)
      Disconnect = (match s.Model.Connection with Connected _ -> Available | _ -> NotOffered)
      Connected = s.Model.Context |> Option.map (fun c -> sprintf "%s/%s on %s" c.Repository.Owner c.Repository.Name c.Repository.Branch)
      WriteScope = (match s.Model.Connection with Connected true -> Writable | Connected false -> ReadOnlyConnection | _ -> WriteScopeUnknown) }

let private loadFailure (id: InvestigationRef) (l: LoadFailure) =
    match l with
    | Unverifiable fs -> InvalidData(sprintf "%d verification failure(s)" fs.Length)
    | ProviderFailed AuthenticationFailed -> CredentialRefused
    | ProviderFailed(PermissionDenied _) -> NotAuthorized
    | ProviderFailed(NotFound _) -> InvestigationNotFound id
    | ProviderFailed(ProviderError.RateLimited _) -> ProviderRateLimited
    | ProviderFailed(ProviderError.NetworkUnavailable _) -> ProviderUnreachable
    | ProviderFailed e -> UiFailure.ProviderError(sprintf "%A" e)

let private project (s: State) : AppView =
    let screen =
        match s.Local.Route with
        | WorkspaceRoute(id, section) ->
            let failedWith f =
                { Frame = Failed f; Stored = Failed f; Section = OverviewSection(Failed f)
                  Drafts = { Values = Map.empty; Errors = []; OpenEditor = None }
                  Details = { Open = false; Form = { Title = ""; ProblemStatement = ""; Question = "" }; Errors = []; Edit = NotOffered; SaveDetails = NotOffered; Cancel = NotOffered }
                  Save = None; State = ScreenFailed f }
            // The frozen v4.1.4 Model is keyed by InvestigationId; v4.1.5 keys it by InvestigationRef (drop `.Id` then).
            let load = s.Model.Loads |> Map.tryFind id.Id |> Option.defaultValue NotLoaded
            let view =
                match load with
                | LoadFailed f -> failedWith (loadFailure id f)
                | LoadState.Loading _ -> { failedWith contentGap with Frame = Waiting "Opening investigation"; State = ScreenLoading }
                // Loaded: the history is verified, but nothing in the frozen types turns it into a title, problem statement
                // or hypotheses for the frame or the sections.
                | LoadState.Loaded _ | NotLoaded | UnavailableOffline -> failedWith contentGap
            let save =
                s.Model.Sessions |> Map.tryFind id.Id |> Option.map (fun session ->
                    let kind = saveKind session.State
                    { Kind = kind
                      // The frozen machine's own projection (IF41-VIEW Projections.SaveIndicator) supplies Text, Glyph,
                      // Politeness and the commit time; the sketch takes the label and the choices from the contracts.
                      Indicator =
                        { InvestigationId = id.Id; Label = PresentationLanguage.saveState kind; Text = ""; Glyph = ""
                          Actions = SaveChoices.forKind kind
                          Politeness = (match kind with ConflictKind | SaveFailedKind | UnknownOutcomeKind -> Assertive | _ -> Polite)
                          LastSavedCommitTime = None }
                      Save = (match kind with UnsavedKind | SaveFailedKind -> Available | _ -> NotOffered)
                      Conflict = None
                      NotSaved = [] })
            ignore section
            WorkspaceScreen { view with Save = save }
        | _ ->
            ListScreen
                { Connection = connectionView s
                  Investigations = (match s.Model.Connection with Connected _ -> Failed listGap | _ -> NotRequested)
                  Sort = ByUpdated
                  NewInvestigation = (match s.Model.Connection with Connected true -> Available | _ -> NotOffered)
                  State = (match s.Model.Connection with Connected _ -> ScreenFailed listGap | _ -> ScreenReady) }
    { Route = s.Local.Route; Primary = Navigation.primary s.Local.Route; Screen = screen; Announcements = []; Origin = IntegratedBackend }

/// The frozen Engine as a UiBackend. Effects are the frozen HostEffect, performed by the Limen host.
let backend (engine: Engine) : UiBackend<State, HostEffect> =
    { Provides = [ DataVisible ]
      Init =
        fun route ->
            let m, fx = engine.Init(Route(Navigation.format route))
            { Model = m; Local = { Form = Connection.emptyForm; Credential = None; Errors = []; Route = route; Drafts = Map.empty } }, fx
      Dispatch = dispatch engine
      Project = project }
