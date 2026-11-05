/// Named fixture scenarios (mission §5). Every value built here is labelled a fixture (FixtureLabel, Visible.fixture,
/// Understood.fixture, BackendOrigin.FixtureBackend). Historical content comes only from CausaDataExtract.g.fs, copied
/// verbatim from kemiller2002/causa-data; nothing historical is written by hand.
module Causa.Ui.Fixtures.Scenarios

open System
open Causa.Domain
open Causa.App
open Causa.Persistence
open Causa.Ui.Contracts

type Scenario =
    | ConnectedCatalog
    | EmptyCatalog
    | UnauthorizedRepository
    | NetworkFailure
    | CatalogLoading
    | InvalidStoredJson
    | UnsavedChanges
    | SaveConflict
    | UnknownSaveOutcome

let all =
    [ ConnectedCatalog; EmptyCatalog; UnauthorizedRepository; NetworkFailure; CatalogLoading; InvalidStoredJson
      UnsavedChanges; SaveConflict; UnknownSaveOutcome ]

let name s =
    match s with
    | ConnectedCatalog -> "connected-catalog"
    | EmptyCatalog -> "empty-catalog"
    | UnauthorizedRepository -> "unauthorized-repository"
    | NetworkFailure -> "network-failure"
    | CatalogLoading -> "loading"
    | InvalidStoredJson -> "invalid-json"
    | UnsavedChanges -> "unsaved-changes"
    | SaveConflict -> "save-conflict"
    | UnknownSaveOutcome -> "unknown-save-outcome"

let tryParse (s: string) = all |> List.tryFind (fun x -> name x = s)

let label s = FixtureLabel.create (sprintf "%s (causa-data %s)" (name s) (CausaDataExtract.sourceCommit.Substring(0, 7)))

let idOf (s: string) = InvestigationId(Guid.Parse s)
let private at (s: string) = DateTimeOffset.Parse(s, Globalization.CultureInfo.InvariantCulture)

/// The catalog workspace a stored case lives in, from its stored path (".../investigations/<id>"). v4.1.5 W1: a catalog
/// address comes only from WorkspaceAddress.catalog.
let catalogAddressOf (c: CausaDataExtract.StoredCase) : WorkspaceAddress.T =
    WorkspaceAddress.catalog (Workspace(c.Path.Substring(0, c.Path.IndexOf "/investigations/")))

let catalogWorkspaces = CausaDataExtract.cases |> List.map (catalogAddressOf >> fun a -> a.Workspace) |> List.distinct

/// The fixture's working copy, workspaces/demo/causa-app-fixture (the reserved working-copy namespace). Synthetic,
/// labelled; not a causa-data path.
let workingCopyAddress : WorkspaceAddress.T = (WorkspaceAddress.workingCopy catalogWorkspaces "fixture").Value

let refOf (c: CausaDataExtract.StoredCase) : InvestigationRef = { Address = catalogAddressOf c; Id = idOf c.Id }

/// A well-formed reference that is in no fixture (the missing-investigation case).
let missingRef : InvestigationRef =
    { Address = catalogAddressOf CausaDataExtract.cases.Head; Id = InvestigationId(Guid.Parse "00000000-0000-4000-8000-000000000000") }

let missingTarget : RouteTarget = Navigation.targetOf missingRef

let apollo11 = CausaDataExtract.cases |> List.find (fun c -> c.Key = "apollo-11")
let apollo11Ref = refOf apollo11

/// A card from a stored case. Status and counts are the stored snapshot's (Data Visible, not Causa's replay);
/// unresolved contradictions need the domain's interpretation of contradiction states, so they are UnknownCount.
let summary (lbl: FixtureLabel) (c: CausaDataExtract.StoredCase) : InvestigationSummary =
    { Ref = refOf c
      Title = c.Title
      Status = Some(AsStored(Visible.fixture lbl c.SnapshotLifecycle))
      LastActivity = Some(at c.UpdatedAt)
      Counts =
        Some(AsStored(Visible.fixture lbl { Hypotheses = Known c.SnapshotHypotheses; Evidence = Known c.SnapshotEvidence; UnresolvedContradictions = UnknownCount }))
      Copy = FrozenDemonstration
      Href = Navigation.format (WorkspaceRoute(Navigation.targetOf (refOf c), Overview)) }

let storedRecord (c: CausaDataExtract.StoredCase) : StoredRecordView =
    { Ref = refOf c
      Title = c.Title
      CreatedAt = Some(at c.CreatedAt)
      UpdatedAt = Some(at c.UpdatedAt)
      EventCount = c.EventCount
      Events =
        if c.Key = "apollo-11" then
            CausaDataExtract.apollo11Events
            |> List.map (fun e -> { Version = e.Version; EventType = e.EventType; OccurredAt = at e.OccurredAt; ActorId = e.ActorId; PayloadJson = e.PayloadJson })
        else [] }

let private kindOf (wire: string) =
    match wire with
    | "supports" -> CatalogKind Supports
    | "contradicts" -> CatalogKind Contradicts
    | other -> UninterpretedKind other

let contentActions =
    [ Actions.recordObservation; Actions.recordHypothesis; Actions.recordEvidence; Actions.linkEvidence; Actions.reassess
      Actions.recordConclusion; Actions.editDetails ]

/// The analysis fixture: a connected slice of the Apollo 11 stored snapshot, shaped as a projection. It stands in for
/// the domain engine's projection and is labelled so; it is not one.
let apollo11Analysis : AnalysisProjection =
    let h = CausaDataExtract.apollo11Hypothesis
    let evidenceRef (id: string) = { Kind = EvidenceEntity; Id = id }
    let links =
        CausaDataExtract.apollo11Relationships
        |> List.map (fun r ->
            { Id = r.Id
              Evidence = evidenceRef r.EvidenceId
              // causa-data v1.0.0 has no evidence label (C5): the link names the stored category, never an invented label.
              EvidenceLabel =
                CausaDataExtract.apollo11Evidence
                |> List.tryFind (fun e -> e.Id = r.EvidenceId)
                |> Option.bind (fun e -> e.Category)
                |> Option.defaultValue "No label recorded"
              Hypothesis = { Kind = HypothesisEntity; Id = r.HypothesisId }
              HypothesisStatement = h.Statement
              Kind = kindOf r.Kind
              Rationale = r.Rationale
              Current = true })
    { Observations =
        CausaDataExtract.apollo11Observations
        |> List.map (fun o -> { Id = o.Id; Text = o.Original; SourceReference = None; ObservedAt = o.ObservedAt |> Option.map at })
      Hypotheses = [ { Id = h.Id; Statement = h.Statement; State = h.State; Explanation = h.Mechanism; Links = links } ]
      Evidence =
        CausaDataExtract.apollo11Evidence
        |> List.map (fun e ->
            { Id = e.Id
              Label = None
              Content = e.Content
              SourceReference = e.Source
              SourceUrl = None
              Category = e.Category
              CapturedAt = e.CapturedAt |> Option.map at
              Links = links |> List.filter (fun l -> l.Evidence.Id = e.Id) })
      Relationships = links
      Timeline =
        CausaDataExtract.apollo11Events
        |> List.map (fun e ->
            { Version = e.Version; EventType = e.EventType; Summary = e.EventType; OccurredAt = at e.OccurredAt; Actor = Some e.ActorId; Affects = [] })
      Conclusions =
        let c = CausaDataExtract.apollo11Conclusion
        [ { Id = c.Id; Statement = c.Statement; ResidualUncertainty = c.ResidualUncertainty; RecordedAtVersion = Some c.RecordedAtVersion } ]
      NextSteps = []
      UnresolvedContradictions = UnknownCount
      CorrectiveActionsSupported = false
      Actions =
        Map.ofList
            // A frozen demonstration is read-only (UI-006 "Frozen fixtures MUST remain read-only", UI-014-O3).
            [ for a in contentActions -> a, Unavailable "Frozen demonstrations are read only." ] }

/// An empty projection for an investigation started in the fixture.
let emptyAnalysis : AnalysisProjection =
    { apollo11Analysis with
        Observations = []
        Hypotheses = []
        Evidence = []
        Relationships = []
        Timeline = []
        Conclusions = []
        Actions = Map.ofList [ for a in contentActions -> a, Available ] }

// ---- Save presentations (the frozen IF41-VIEW SaveIndicatorView is filled for every one of the seven states) --------

let saveActions kind = SaveChoices.forKind kind

let saveText kind =
    match kind with
    | SavedKind -> "All changes are saved."
    | UnsavedKind -> "Changes are kept only in this tab until you save."
    | SavingKind -> "Saving your changes."
    | OfflineKind -> "You are offline. Changes are kept only in this tab."
    | ConflictKind -> "Someone else saved this investigation. Your changes are kept in this tab."
    | UnknownOutcomeKind -> "It is not yet known whether your last save reached the repository."
    | SaveFailedKind -> "The repository refused the save. Your changes are kept in this tab."

let savePresentation (id: InvestigationRef) (kind: SaveStateKind) (title: string) : SavePresentation =
    { Kind = kind
      Indicator =
        // The frozen IF41-VIEW record still carries the bare id (v4.1.5 owns that type).
        { InvestigationId = id.Id
          Label = PresentationLanguage.saveState kind
          Text = saveText kind
          // The glyph set is the save-indicator owner's (PERSIST-030, T22); fixtures leave it empty rather than invent one.
          Glyph = ""
          Actions = saveActions kind
          Politeness = (match kind with ConflictKind | SaveFailedKind | UnknownOutcomeKind -> Assertive | _ -> Polite)
          LastSavedCommitTime = None }
      Save = (match kind with UnsavedKind | SaveFailedKind -> Available | _ -> NotOffered)
      Conflict =
        match kind with
        | ConflictKind ->
            Some { ThisTabVersion = "Your version (this tab)"; GitHubVersion = "Newer version in the repository"; UnsavedChanges = 1
                   Explanation = "The repository changed after you opened this investigation."
                   Choices = saveActions ConflictKind }
        | _ -> None
      NotSaved = (match kind with SavedKind -> [] | _ -> [ title ]) }

let administration (connected: string option) : AdministrationView =
    let fact label status = { Label = label; Status = status }
    { Provider = fact "Provider" (FactKnown "GitHub (fixture)")
      Repository = fact "Repository" (match connected with Some r -> FactKnown r | None -> FactUnavailable)
      Connection = fact "Connection" (match connected with Some _ -> FactKnown "Connected (fixture)" | None -> FactUnavailable)
      Catalog = fact "Catalog" (FactKnown(CausaDataExtract.catalogManifest + " @ " + CausaDataExtract.sourceCommit.Substring(0, 7)))
      StorageContract = fact "Storage contract" (FactKnown "causa-data v1.0.0")
      ApplicationVersion = fact "Application version" FactUnknown
      DeployedCommit = fact "Deployed commit" FactUnknown
      DataValidation = fact "Data validation" FactUnknown
      Diagnostics = [ "Fixture backend: no request reaches GitHub, and no history is replayed." ] }

/// The fixture's one preference. Which preferences the product offers is an owner decision (CF-06).
let settings : SettingsView =
    { Preferences = [ { Id = "reduced-motion"; Label = "Reduce motion"; Scope = DeviceScope; Options = [ "system"; "on"; "off" ]; Effective = "system" } ]
      ConnectionPreference = { Label = "Repository connection"; Status = FactUnavailable } }
