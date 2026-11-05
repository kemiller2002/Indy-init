/// The fixture-backed implementation of UiBackend (mission §5, §7). Pure: no network, no clock, no randomness, no
/// replay. It answers every intent from the scenario's fixed data, so UI agents can compile, render and exercise their
/// screens before the domain engine, persistence or GitHub adapter exist. Its views carry BackendOrigin.FixtureBackend,
/// and its Saved state means only "the fixture says so"; it is never evidence of a commit (PERSIST-007).
module Causa.Ui.Fixtures.FixtureBackend

open System
open Causa.Domain
open Causa.App
open Causa.Persistence
open Causa.Ui.Contracts
open Causa.Ui.Fixtures.Scenarios

type WorkingCopy = { Id: InvestigationRef; Form: StartForm; Details: DetailsForm }

type State =
    { Scenario: Scenario
      Route: UiRoute
      Connection: ConnectionState
      Form: ConnectForm
      /// Held, never projected. Option so the tests can prove it reached the backend.
      Credential: Credential.T option
      ConnectErrors: ValidationError list
      ConnectFailure: UiFailure option
      Start: StartForm
      StartErrors: ValidationError list
      Copies: WorkingCopy list
      Analysis: Map<InvestigationRef, AnalysisProjection>
      Drafts: Map<FieldId, string>
      DraftErrors: ValidationError list
      DetailsOpen: bool
      SaveKinds: Map<InvestigationRef, SaveStateKind>
      Counter: int
      /// Copies started so far; the next Start identifier derives from it and is never reused.
      Started: int }

/// The fixed instant fixture records use (no clock in a pure backend).
let fixtureInstant = DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero)

/// Fixture identifiers come from a counter that only grows, so no two copies share an id (review finding R4).
let private newIdentifier (n: int) = sprintf "00000000-0000-4000-8000-%012d" (n + 1)

/// The working copy the save scenarios start with. Synthetic text, labelled; not a historical case.
let private seededCopy =
    let id = "00000000-0000-4000-8000-100000000001"
    { Id = { Address = workingCopyAddress; Id = InvestigationId(Guid.Parse id) }
      Form = { ProblemStatement = "Fixture working copy: problem statement."; Title = "Fixture working copy"; Question = ""; Identifier = id }
      Details = { Title = "Fixture working copy"; ProblemStatement = "Fixture working copy: problem statement."; Question = "" } }

let seededCopyId = seededCopy.Id

let init (scenario: Scenario) (route: UiRoute) : State =
    let seeded = match scenario with UnsavedChanges | SaveConflict | UnknownSaveOutcome -> [ seededCopy ] | _ -> []
    { Scenario = scenario
      Route = route
      Connection = Disconnected
      Form = Connection.emptyForm
      Credential = None
      ConnectErrors = []
      ConnectFailure = None
      Start = StartForm.empty (newIdentifier 0)
      StartErrors = []
      Copies = seeded
      Analysis = Map.ofList [ yield apollo11Ref, apollo11Analysis; for c in seeded -> c.Id, emptyAnalysis ]
      Drafts = Map.empty
      DraftErrors = []
      DetailsOpen = false
      SaveKinds = Map.ofList [ for c in seeded -> c.Id, UnsavedKind ]
      Counter = 0
      Started = 0 }

let private isConnectionField f = Connection.fields |> List.exists (fun s -> s.Field = f)
let private isStartField f = StartForm.fields |> List.exists (fun s -> s.Field = f)

let private connected s = match s.Connection with Connected _ -> true | _ -> false

/// Every reference this backend has listed: the stored catalog cases and the tab's working copies. A route resolves
/// only against these (v4.1.5 W1: no address is built from a URL).
let private known s = (CausaDataExtract.cases |> List.map refOf) @ (s.Copies |> List.map (fun c -> c.Id))

let private currentInvestigation s =
    match s.Route with
    | WorkspaceRoute(t, _) -> Navigation.resolve (known s) t
    | _ -> None

let private isWorkingCopy s id = s.Copies |> List.exists (fun c -> c.Id = id)

let private connect s =
    match Connection.validate s.Form with
    | [] ->
        match s.Scenario with
        | UnauthorizedRepository -> { s with Connection = Disconnected; ConnectErrors = []; ConnectFailure = Some NotAuthorized }
        | NetworkFailure -> { s with Connection = Disconnected; ConnectErrors = []; ConnectFailure = Some ProviderUnreachable }
        | CatalogLoading -> { s with Connection = Connecting(RequestId 1L); ConnectErrors = []; ConnectFailure = None }
        | _ -> { s with Connection = Connected true; ConnectErrors = []; ConnectFailure = None }
    | errors -> { s with ConnectErrors = errors }

let private draft s f = s.Drafts |> Map.tryFind f |> Option.defaultValue ""

/// Applies one content editor to the fixture projection: validates its catalog fields first; on success appends the
/// entered values (never interpreting them) and the investigation becomes Unsaved.
let private record (specs: FieldSpec list) (apply: State -> AnalysisProjection -> AnalysisProjection) (s: State) =
    match currentInvestigation s with
    | Some id when isWorkingCopy s id ->
        match Validation.checkAll specs s.Drafts with
        | [] ->
            let analysis = s.Analysis |> Map.tryFind id |> Option.defaultValue emptyAnalysis
            { s with
                Analysis = s.Analysis |> Map.add id (apply s analysis)
                Drafts = specs |> List.fold (fun d spec -> Map.remove spec.Field d) s.Drafts
                DraftErrors = []
                SaveKinds = s.SaveKinds |> Map.add id UnsavedKind
                Counter = s.Counter + 1 }
        | errors -> { s with DraftErrors = errors }
    | _ -> s

let private nextEntityId s = sprintf "fixture-%04d" (s.Counter + 1)

let private kindOfValue v =
    match v with
    | "supports" -> Some Supports
    | "contradicts" -> Some Contradicts
    | "consistent-with" -> Some ConsistentWith
    | "inconclusive" -> Some Inconclusive
    | _ -> None

let private addLink (supersede: bool) s (a: AnalysisProjection) =
    let evidenceId, hypothesisId = draft s Fields.linkEvidence, draft s Fields.linkHypothesis
    match kindOfValue (draft s Fields.relationshipKind),
          a.Evidence |> List.tryFind (fun e -> e.Id = evidenceId),
          a.Hypotheses |> List.tryFind (fun h -> h.Id = hypothesisId) with
    | Some kind, Some e, Some h ->
        let link =
            { Id = nextEntityId s
              Evidence = { Kind = EvidenceEntity; Id = e.Id }
              EvidenceLabel = e.Label |> Option.defaultValue "No label recorded"
              Hypothesis = { Kind = HypothesisEntity; Id = h.Id }
              HypothesisStatement = h.Statement
              Kind = CatalogKind kind
              Rationale = draft s Fields.relationshipRationale
              Current = true }
        let samePair (l: LinkCard) = l.Evidence.Id = e.Id && l.Hypothesis.Id = h.Id
        let earlier = if supersede then a.Relationships |> List.map (fun l -> if samePair l then { l with Current = false } else l) else a.Relationships
        let relationships = earlier @ [ link ]
        let linksOf pred = relationships |> List.filter pred
        { a with
            Relationships = relationships
            Evidence = a.Evidence |> List.map (fun x -> { x with Links = linksOf (fun l -> l.Evidence.Id = x.Id) })
            Hypotheses = a.Hypotheses |> List.map (fun x -> { x with Links = linksOf (fun l -> l.Hypothesis.Id = x.Id) })
            Timeline =
                a.Timeline
                @ [ { Version = int64 (a.Timeline.Length + 1); EventType = "EvidenceRelated"
                      Summary = sprintf "%s: %s" (ContentForms.kindValue kind) link.Rationale
                      OccurredAt = fixtureInstant; Actor = None; Affects = [ link.Evidence; link.Hypothesis ] } ] }
    | _ -> a

/// INV-030: "A link to a non-existent target is refused."
let private missingTarget s =
    match currentInvestigation s |> Option.bind (fun id -> s.Analysis |> Map.tryFind id) with
    | Some a ->
        [ if not (a.Evidence |> List.exists (fun e -> e.Id = draft s Fields.linkEvidence)) then
              yield { Field = Fields.linkEvidence; Rule = Required; Message = "Choose evidence recorded in this investigation." }
          if not (a.Hypotheses |> List.exists (fun h -> h.Id = draft s Fields.linkHypothesis)) then
              yield { Field = Fields.linkHypothesis; Rule = Required; Message = "Choose a hypothesis recorded in this investigation." } ]
    | None -> []

/// INV-030: a second link for a pair is refused, pointing to the existing one; reassessment supersedes it instead.
let private linkRefusal s =
    match currentInvestigation s |> Option.bind (fun id -> s.Analysis |> Map.tryFind id) with
    | Some a when a.Relationships |> List.exists (fun l -> l.Current && l.Evidence.Id = draft s Fields.linkEvidence && l.Hypothesis.Id = draft s Fields.linkHypothesis) ->
        Some { Field = Fields.linkEvidence; Rule = Required; Message = "This evidence is already linked to this hypothesis; reassess the existing link instead." }
    | _ -> None

let private timelineEntry (a: AnalysisProjection) eventType summary =
    a.Timeline @ [ { Version = int64 (a.Timeline.Length + 1); EventType = eventType; Summary = summary; OccurredAt = fixtureInstant; Actor = None; Affects = [] } ]

let private activate (action: ActionId) (key: string option) (s: State) : State =
    if action = Actions.connect then connect s
    elif action = Actions.disconnect then
        // Disconnect forgets the connection, never the tab's copies, their content or their save states (PERSIST-020).
        let fresh = init s.Scenario InvestigationsRoute
        { s with Route = InvestigationsRoute; Connection = fresh.Connection; Form = fresh.Form; Credential = None; ConnectErrors = []; ConnectFailure = None }
    elif action = Actions.newInvestigation then (if s.Connection = Connected true then { s with Route = StartRoute } else s)
    elif action = Actions.openInvestigation then
        // The row key is the canonical workspace route, so the workspace travels with the id (v4.1.5 W1).
        match key |> Option.map Navigation.parse with
        | Some(WorkspaceRoute(r, section)) -> { s with Route = WorkspaceRoute(r, section) }
        | _ -> s
    elif action = Actions.cancelStart then { s with Route = InvestigationsRoute; Start = StartForm.empty (newIdentifier s.Started); StartErrors = [] }
    elif action = Actions.startInvestigation then
        match StartForm.validate s.Start, Guid.TryParse s.Start.Identifier with
        | [], (true, g) ->
            let id = { Address = workingCopyAddress; Id = InvestigationId g }
            let copy = { Id = id; Form = s.Start; Details = { Title = s.Start.Title; ProblemStatement = s.Start.ProblemStatement; Question = s.Start.Question } }
            { s with
                Copies = s.Copies @ [ copy ]
                Analysis = s.Analysis |> Map.add id emptyAnalysis
                SaveKinds = s.SaveKinds |> Map.add id UnsavedKind
                Route = WorkspaceRoute(Navigation.targetOf id, Overview)
                Started = s.Started + 1
                Start = StartForm.empty (newIdentifier (s.Started + 1))
                StartErrors = [] }
        | errors, _ -> { s with StartErrors = errors }
    elif action = Actions.recordObservation then
        s |> record ContentForms.observation (fun s a ->
            let text, source = draft s Fields.observationText, draft s Fields.observationSource
            { a with
                Observations = a.Observations @ [ { Id = nextEntityId s; Text = text; SourceReference = (if source = "" then None else Some source); ObservedAt = None } ]
                Timeline = timelineEntry a "ObservationRecorded" text })
    elif action = Actions.recordHypothesis then
        s |> record ContentForms.hypothesisOnRecord (fun s a ->
            let statement = draft s Fields.hypothesisStatement
            let explanation = draft s Fields.hypothesisExplanation
            { a with
                Hypotheses = a.Hypotheses @ [ { Id = nextEntityId s; Statement = statement; State = "proposed"; Explanation = (if explanation = "" then None else Some explanation); Links = [] } ]
                Timeline = timelineEntry a "HypothesisRecorded" statement })
    elif action = Actions.recordEvidence then
        s |> record ContentForms.evidence (fun s a ->
            let get = draft s
            { a with
                Evidence =
                    a.Evidence
                    @ [ { Id = nextEntityId s; Label = Some(get Fields.evidenceLabel); Content = get Fields.evidenceContent
                          SourceReference = (match get Fields.evidenceSource with "" -> None | v -> Some v)
                          SourceUrl = SafeUrl.tryCreate (get Fields.evidenceUrl); Category = None; CapturedAt = None; Links = [] } ]
                Timeline = timelineEntry a "EvidenceAdmitted" (get Fields.evidenceLabel) })
    elif action = Actions.linkEvidence then
        match missingTarget s, linkRefusal s with
        | (_ :: _) as errors, _ -> { s with DraftErrors = errors }
        | [], Some refusal -> { s with DraftErrors = [ refusal ] }
        | [], None -> s |> record ContentForms.link (addLink false)
    elif action = Actions.reassess then
        match missingTarget s with
        | [] -> s |> record ContentForms.link (addLink true)
        | errors -> { s with DraftErrors = errors }
    elif action = Actions.recordConclusion then
        s |> record ContentForms.conclusion (fun s a ->
            let statement = draft s Fields.conclusionStatement
            { a with
                Conclusions = a.Conclusions @ [ { Id = nextEntityId s; Statement = statement; ResidualUncertainty = None; RecordedAtVersion = None } ]
                Timeline = timelineEntry a "ConclusionRecorded" statement })
    elif action = Actions.cancelEditor then { s with Drafts = Map.empty; DraftErrors = []; DetailsOpen = false }
    elif action = Actions.editDetails then
        match currentInvestigation s with
        | Some id when isWorkingCopy s id -> { s with DetailsOpen = true }
        | _ -> s
    elif action = Actions.saveDetails then
        match currentInvestigation s with
        | Some id when isWorkingCopy s id && s.DetailsOpen ->
            let copy = s.Copies |> List.find (fun c -> c.Id = id)
            let edited =
                { Title = s.Drafts |> Map.tryFind Fields.detailsTitle |> Option.defaultValue copy.Details.Title
                  ProblemStatement = s.Drafts |> Map.tryFind Fields.detailsProblem |> Option.defaultValue copy.Details.ProblemStatement
                  Question = s.Drafts |> Map.tryFind Fields.detailsQuestion |> Option.defaultValue copy.Details.Question }
            match DetailsForm.validate edited with
            | [] ->
                // INV-034/INV-035: the identifier is unchanged; only the details change.
                { s with
                    Copies = s.Copies |> List.map (fun c -> if c.Id = id then { c with Details = edited } else c)
                    Drafts = s.Drafts |> Map.remove Fields.detailsTitle |> Map.remove Fields.detailsProblem |> Map.remove Fields.detailsQuestion
                    DraftErrors = []
                    DetailsOpen = false
                    SaveKinds = s.SaveKinds |> Map.add id UnsavedKind }
            | errors -> { s with DraftErrors = errors }
        | _ -> s
    elif action = Actions.save then
        match currentInvestigation s with
        | Some id when isWorkingCopy s id ->
            let outcome =
                match s.Scenario with
                | SaveConflict -> ConflictKind
                | UnknownSaveOutcome -> UnknownOutcomeKind
                | _ -> SavedKind
            { s with SaveKinds = s.SaveKinds |> Map.add id outcome }
        | _ -> s
    else s

let dispatch (intent: UiIntent) (s: State) : State =
    match intent with
    | UiIntent.Navigate r -> { s with Route = r; DraftErrors = [] }
    | UiIntent.Input(f, v, _) when isConnectionField f -> { s with Form = Connection.withField f v s.Form }
    | UiIntent.Input(f, v, _) when isStartField f -> { s with Start = StartForm.withField f v s.Start }
    | UiIntent.Input(f, v, _) -> { s with Drafts = s.Drafts |> Map.add f v }
    | UiIntent.Secret(_, c) -> { s with Credential = Some c; Form = { s.Form with CredentialEntered = true } }
    | UiIntent.Activate(a, key) -> activate a key s

// ---- projection ------------------------------------------------------------------------------------------------------

let private connectionView s : ConnectionView =
    { State = s.Connection
      Form = s.Form
      Errors = s.ConnectErrors
      Connect = (match s.Connection with Connected _ | Connecting _ -> NotOffered | _ -> Available)
      Disconnect = (match s.Connection with Connected _ -> Available | _ -> NotOffered)
      Connected = (match s.Connection with Connected _ -> Some(sprintf "%s/%s on %s" s.Form.Owner s.Form.Repository s.Form.Branch) | _ -> None)
      WriteScope = (match s.Connection with Connected true -> Writable | Connected false -> ReadOnlyConnection | _ -> WriteScopeUnknown) }

let private copySummary (lbl: FixtureLabel) (c: WorkingCopy) : InvestigationSummary =
    { Ref = c.Id
      Title = (if c.Details.Title = "" then c.Details.ProblemStatement else c.Details.Title)
      Status = Some(AsProjected(Understood.fixture lbl "active"))
      LastActivity = Some fixtureInstant
      Counts = None
      Copy = WorkingCopy
      Href = Navigation.format (WorkspaceRoute(Navigation.targetOf c.Id, Overview)) }

let private listView s : InvestigationListView =
    let lbl = label s.Scenario
    let investigations =
        match s.ConnectFailure, s.Connection, s.Scenario with
        | Some f, _, _ -> Failed f
        | None, Connecting _, _ -> Waiting "Opening investigations"
        | None, Connected _, EmptyCatalog -> Ready(Visible.fixture lbl (s.Copies |> List.map (copySummary lbl)))
        | None, Connected _, _ ->
            Ready(Visible.fixture lbl ((CausaDataExtract.cases |> List.map (summary lbl)) @ (s.Copies |> List.map (copySummary lbl))))
        | None, _, _ -> NotRequested
    { Connection = connectionView s
      Investigations = investigations |> Remote.map (Visible.map (InvestigationList.sort ByUpdated))
      Sort = ByUpdated
      NewInvestigation = (if s.Connection = Connected true then Available else NotOffered)
      State = InvestigationList.screenState investigations }

let private startView s : StartView =
    { Form = s.Start
      Errors = s.StartErrors
      Destination =
        (match s.Connection with
         | Connected w -> Ready { Repository = sprintf "%s/%s" s.Form.Owner s.Form.Repository; Branch = s.Form.Branch; Writable = w }
         | _ -> Failed(CapabilityAbsent(DataEditable, "not connected")))
      Profile = None
      Start = (if s.Connection = Connected true then Available else Unavailable "Connect a writable repository first.")
      Cancel = Available
      Submission = NotSubmitted }

let private frameOf (lbl: FixtureLabel) (id: InvestigationRef) (section: Section) (title: string) (problem: Shown<string>) question created state copy supported =
    { Ref = id
      Identifier = (let (InvestigationId g) = id.Id in g.ToString("D"))
      Title = title
      ProblemStatement = problem
      Question = question
      CreatedAt = created
      InvestigationState = state
      Copy = copy
      Sections = Navigation.sectionLinks id section supported
      Version = None }

let private sectionOf (section: Section) (analysis: Remote<Understood<AnalysisProjection>>) =
    match section with
    | Overview -> OverviewSection analysis
    | CausalAnalysis -> CausalAnalysisView analysis
    | EvidenceSection -> EvidenceView analysis
    | HypothesesSection -> HypothesesView analysis
    | TimelineSection -> TimelineView analysis
    | CorrectiveActions -> CorrectiveActionsView analysis

let private workspaceView s (id: InvestigationRef) (section: Section) : WorkspaceView =
    let lbl = label s.Scenario
    let drafts = { Values = s.Drafts; Errors = s.DraftErrors; OpenEditor = None }
    let closedDetails edit =
        { Open = false; Form = { Title = ""; ProblemStatement = ""; Question = "" }; Errors = []; Edit = edit; SaveDetails = NotOffered; Cancel = NotOffered }
    let failed f =
        { Frame = Failed f; Stored = Failed f; Section = sectionOf section (Failed f); Drafts = drafts; Details = closedDetails NotOffered; Save = None; State = ScreenFailed f }
    let stored = CausaDataExtract.cases |> List.tryFind (fun c -> refOf c = id)
    let copy = s.Copies |> List.tryFind (fun c -> c.Id = id)
    match connected s, stored, copy with
    | false, _, _ -> failed (CapabilityAbsent(DataVisible, "not connected"))
    | true, Some c, _ when s.Scenario = InvalidStoredJson && c.Key = "apollo-11" ->
        failed (InvalidData "events.ndjson: line 1 is not a JSON object (fixture)")
    | true, Some c, _ ->
        let visible x = AsStored(Visible.fixture lbl x)
        let analysis =
            match s.Analysis |> Map.tryFind id with
            | Some a -> Ready(Understood.fixture lbl a)
            | None -> Failed(CapabilityAbsent(DataUnderstood, "no projection fixture for this case"))
        let problem =
            if c.Key = "apollo-11" then
                // The stored InvestigationStarted payload's `original`, verbatim (first stored event).
                let payload = (CausaDataExtract.apollo11Events.Head).PayloadJson
                Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("original").GetString() |> string
            else c.Title
        { Frame = Ready(frameOf lbl id section c.Title (visible problem) None (visible (Scenarios.storedRecord c).CreatedAt.Value) (visible c.SnapshotLifecycle) FrozenDemonstration false)
          Stored = Ready(Visible.fixture lbl (storedRecord c))
          Section = sectionOf section analysis
          Drafts = drafts
          Details = closedDetails (Unavailable "Frozen demonstrations are read only.")
          Save = None
          State = ScreenReady }
    | true, None, Some w ->
        let projected x = AsProjected(Understood.fixture lbl x)
        let analysis = s.Analysis |> Map.tryFind id |> Option.defaultValue emptyAnalysis
        let kind = s.SaveKinds |> Map.tryFind id |> Option.defaultValue UnsavedKind
        let title = if w.Details.Title = "" then w.Details.ProblemStatement else w.Details.Title
        { Frame =
            Ready(frameOf lbl id section title (projected w.Details.ProblemStatement) (if w.Details.Question = "" then None else Some(projected w.Details.Question))
                        (projected fixtureInstant) (projected "active") WorkingCopy false)
          Stored = Failed(CapabilityAbsent(DataVisible, "not yet saved to the repository"))
          Section = sectionOf section (Ready(Understood.fixture lbl analysis))
          Drafts = drafts
          Details =
            { Open = s.DetailsOpen; Form = w.Details; Errors = DetailsForm.validate w.Details
              Edit = Available; SaveDetails = (if s.DetailsOpen then Available else NotOffered); Cancel = (if s.DetailsOpen then Available else NotOffered) }
          Save = Some(savePresentation id kind title)
          State = ScreenReady }
    | true, None, None -> failed (InvestigationNotFound(Navigation.targetOf id))

/// A route whose target no listed workspace holds (or a role that does not match): not found, fail closed.
let private notFoundView s (t: RouteTarget) (section: Section) : WorkspaceView =
    let f = InvestigationNotFound t
    { Frame = Failed f; Stored = Failed f; Section = OverviewSection(Failed f)
      Drafts = { Values = s.Drafts; Errors = s.DraftErrors; OpenEditor = None }
      Details = { Open = false; Form = { Title = ""; ProblemStatement = ""; Question = "" }; Errors = []; Edit = NotOffered; SaveDetails = NotOffered; Cancel = NotOffered }
      Save = None
      State = ScreenFailed f }

let project (s: State) : AppView =
    let screen =
        match s.Route with
        | InvestigationsRoute -> ListScreen(listView s)
        | StartRoute -> StartScreen(startView s)
        | WorkspaceRoute(t, section) ->
            match Navigation.resolve (known s) t with
            | Some r -> WorkspaceScreen(workspaceView s r section)
            | None -> WorkspaceScreen(notFoundView s t section)
        | AdministrationRoute -> AdministrationScreen(administration (connectionView s).Connected)
        | SettingsRoute -> SettingsScreen settings
        | NotFoundRoute r -> NotFoundScreen { Requested = r; Copy = PresentationLanguage.failure (CapabilityAbsent(DataVisible, "unknown route")) }
    { Route = s.Route
      Primary = Navigation.primary s.Route
      Screen = screen
      Announcements = []
      Origin = FixtureBackend(label s.Scenario) }

/// The fixture backend as the substitutable interface. Effects: none (unit list): a fixture performs nothing.
let backend (scenario: Scenario) : UiBackend<State, unit> =
    { Provides = [ DataVisible; DataUnderstood; DataEditable ]
      Init = fun route -> init scenario route, []
      Dispatch = fun intent s -> dispatch intent s, []
      Project = project }
