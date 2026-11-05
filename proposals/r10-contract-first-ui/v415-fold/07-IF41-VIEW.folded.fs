namespace Causa.App

open Causa.Domain
open Causa.Persistence

/// IF41-VIEW: projections the views render (ui and ui-2). Views read only these; they never decide legality.
type SaveAction =
    | Save
    | Retry
    | Reconnect
    | CheckAgainAction
    | Review
    | ReapplyMine
    | DiscardMine
    | ExportMine

type SaveIndicatorView =
    { InvestigationId: InvestigationId
      Label: string
      Text: string
      Glyph: string
      Actions: SaveAction list
      Politeness: Politeness
      LastSavedCommitTime: System.DateTimeOffset option }

type WorkspaceHeaderView =
    { Connection: ConnectionState
      Offline: bool
      NotSaved: (InvestigationId * string) list }

type ScreenView =
    { Route: Route
      Title: string
      Header: WorkspaceHeaderView
      Indicator: SaveIndicatorView option }

type Projections =
    { SaveIndicator: InvestigationSession -> SaveIndicatorView
      Header: Model -> WorkspaceHeaderView
      Screen: Model -> ScreenView }

// ---- IF-UI (CF-09 A, APR-CFUI-OWNER-1): presentation contracts for screens, folded into IF41-VIEW so no new
// contract file is added (V41Package.fs:1790). Source: proposals/r10-contract-first-ui/contracts. Investigation
// identities are InvestigationRef (v4.1.5 W1, 02-IF41-STORAGE.fs). ------------------------------------------------

// -- Ids.fs --
namespace Causa.Ui.Contracts

/// Stable identifiers. Catalog entries keep their CAT-CAUSA-INVESTIGATION id (INV-0xx) as their field, action or region id,
/// so a UI element, its manifest entry and its Dokimos/catalog check share one name. Entries no catalog item names (the
/// connection form, journey-named actions) get a UIF-/UIA-/UIR- id and cite the source that requires them
/// (SCREEN-MANIFEST.json). The id is also the Limen binding name (data-text, data-event) of the element.
type ScreenId = ScreenId of string
type FieldId = FieldId of string
type ActionId = ActionId of string
type RegionId = RegionId of string

[<RequireQualifiedAccess>]
module ScreenId =
    let value (ScreenId s) = s

[<RequireQualifiedAccess>]
module FieldId =
    let value (FieldId s) = s

[<RequireQualifiedAccess>]
module ActionId =
    let value (ActionId s) = s

[<RequireQualifiedAccess>]
module RegionId =
    let value (RegionId s) = s

/// Screens: the catalog's three (investigation-list, investigation-start, investigation-workspace) plus the two
/// primary destinations UI-018/UI-019/UI-020 require and the URL contract's not-found route.
[<RequireQualifiedAccess>]
module Screens =
    let list = ScreenId "investigation-list"
    let start = ScreenId "investigation-start"
    let workspace = ScreenId "investigation-workspace"
    let administration = ScreenId "administration"
    let settings = ScreenId "settings"
    let notFound = ScreenId "not-found"
    let all = [ list; start; workspace; administration; settings; notFound ]

[<RequireQualifiedAccess>]
module Fields =
    // investigation-list, connection region (EXT-DATA-002/003/004; labels: ODR-R10-07 patterns)
    let repositoryOwner = FieldId "UIF-CONN-OWNER"
    let repositoryName = FieldId "UIF-CONN-REPOSITORY"
    let branch = FieldId "UIF-CONN-BRANCH"
    let token = FieldId "UIF-CONN-TOKEN"
    // investigation-start (S01)
    let problemStatement = FieldId "INV-001"
    let title = FieldId "INV-002"
    let identifier = FieldId "INV-006"
    let question = FieldId "INV-023"
    // investigation-workspace content editors
    let hypothesisExplanation = FieldId "INV-011"
    let evidenceContent = FieldId "INV-012"
    let observationText = FieldId "INV-024"
    let observationSource = FieldId "INV-025"
    let hypothesisStatement = FieldId "INV-026"
    let evidenceLabel = FieldId "INV-027"
    let evidenceSource = FieldId "INV-028"
    let evidenceUrl = FieldId "INV-029"
    let relationshipKind = FieldId "INV-031"
    let conclusionStatement = FieldId "INV-033"
    let relationshipRationale = FieldId "INV-038"
    /// The link editor's evidence and hypothesis choosers (J2-04 picks them by combobox name; INV-030 names the action).
    let linkEvidence = FieldId "UIF-LINK-EVIDENCE"
    let linkHypothesis = FieldId "UIF-LINK-HYPOTHESIS"
    // investigation-workspace details editor (INV-034 edits INV-001, INV-002, INV-023; the identifier is unchanged)
    let detailsTitle = FieldId "UIF-DETAILS-TITLE"
    let detailsProblem = FieldId "UIF-DETAILS-PROBLEM"
    let detailsQuestion = FieldId "UIF-DETAILS-QUESTION"

[<RequireQualifiedAccess>]
module Actions =
    let connect = ActionId "UIA-CONNECT"
    let disconnect = ActionId "UIA-DISCONNECT"
    let newInvestigation = ActionId "UIA-NEW-INVESTIGATION"
    let openInvestigation = ActionId "INV-040"
    let startInvestigation = ActionId "INV-004"
    let cancelStart = ActionId "INV-022"
    let recordHypothesis = ActionId "INV-009"
    let recordObservation = ActionId "INV-014"
    let save = ActionId "INV-015"
    let linkEvidence = ActionId "INV-030"
    let recordConclusion = ActionId "INV-032"
    let editDetails = ActionId "INV-034"
    let cancelEditor = ActionId "INV-036"
    /// J2-03 activates /record evidence|add evidence/; no catalog entry names it (OWNER-DECISIONS CF-02).
    let recordEvidence = ActionId "UIA-RECORD-EVIDENCE"
    /// J2-06 activates "Reassess"; INV-030's note requires reassessment; no catalog entry names the action (CF-02).
    let reassess = ActionId "UIA-REASSESS"
    let saveDetails = ActionId "UIA-SAVE-DETAILS"
    let retryLoad = ActionId "UIA-RETRY-LOAD"
    /// The seven-state indicator's own actions (frozen IF41-VIEW SaveAction).
    let saveIndicator = ActionId "UIA-SAVE-INDICATOR"

[<RequireQualifiedAccess>]
module Regions =
    let investigations = RegionId "INV-039"
    let primaryNavigation = RegionId "INV-041"
    let sections = RegionId "INV-042"
    let creationTimestamp = RegionId "INV-007"
    let originalProblem = RegionId "INV-008"
    let hypotheses = RegionId "INV-010"
    let evidence = RegionId "INV-013"
    let saveState = RegionId "INV-016"
    let investigationState = RegionId "INV-018"
    let saveOutcomes = RegionId "INV-043"
    let profileIndicator = RegionId "INV-003"
    let destinationIndicator = RegionId "INV-005"
    let connection = RegionId "UIR-CONNECTION"
    let header = RegionId "UIR-WORKSPACE-HEADER"
    let timeline = RegionId "UIR-TIMELINE"
    let causalAnalysis = RegionId "UIR-CAUSAL-ANALYSIS"
    let conclusions = RegionId "UIR-CONCLUSIONS"
    let observations = RegionId "UIR-OBSERVATIONS"
    let overview = RegionId "UIR-OVERVIEW"
    let nextSteps = RegionId "UIR-NEXT-STEPS"
    let conflictReview = RegionId "UIR-CONFLICT-REVIEW"
    let storedRecord = RegionId "UIR-STORED-RECORD"
    let diagnostics = RegionId "UIR-DIAGNOSTICS"
    let preferences = RegionId "UIR-PREFERENCES"

// -- Types.fs --

open System
open System.Text.RegularExpressions
open Causa.Domain
open Causa.Persistence

/// A fixture's label. Every fixture value carries one, so a fixture-backed screen can never be reported as an
/// integrated one (VERIFICATION-STRATEGY.md, Layer A/B versus Layer C). Construct only through FixtureLabel.create.
type FixtureLabel = private FixtureLabel of string

[<RequireQualifiedAccess>]
module FixtureLabel =
    let create (name: string) : FixtureLabel = FixtureLabel("FIXTURE: " + name)
    let value (FixtureLabel s) = s

/// Where a stored record was read: the repository commit when the provider supplied one.
type StoredRef = { Path: string; Commit: string option }

// ---- The three capability boundaries (README.md section 3) ----------------------------------------------------------

/// What the UI is entitled to claim about a value.
type Capability =
    /// Repository status, investigation summaries, metadata and stored records exactly as read. No replay is claimed.
    | DataVisible
    /// Validated projections the domain engine supplied (hypotheses, evidence, relationships, timeline, conclusions,
    /// state). The UI never replays events itself.
    | DataUnderstood
    /// Inputs, commands, validation results and save progress. Persistence and the domain engine decide correctness.
    | DataEditable

/// A value shown as stored (Data Visible). It can never be turned into an Understood value: there is no conversion,
/// so stored data cannot be presented as the kernel's interpretation.
type Visible<'a> =
    private
    | StoredValue of 'a * StoredRef
    | VisibleFixture of 'a * FixtureLabel

/// A value the domain engine projected at a version (Data Understood).
type Understood<'a> =
    private
    | Projected of 'a * AggregateVersion
    | UnderstoodFixture of 'a * FixtureLabel

[<RequireQualifiedAccess>]
module Visible =
    let stored (at: StoredRef) (value: 'a) = StoredValue(value, at)
    let fixture (label: FixtureLabel) (value: 'a) = VisibleFixture(value, label)
    let value v = match v with StoredValue(x, _) | VisibleFixture(x, _) -> x
    let fixtureLabel v = match v with VisibleFixture(_, l) -> Some l | StoredValue _ -> None
    let map f v = match v with StoredValue(x, s) -> StoredValue(f x, s) | VisibleFixture(x, l) -> VisibleFixture(f x, l)

[<RequireQualifiedAccess>]
module Understood =
    /// Only an integration adapter holding a domain projection calls this.
    let projected (version: AggregateVersion) (value: 'a) = Projected(value, version)
    let fixture (label: FixtureLabel) (value: 'a) = UnderstoodFixture(value, label)
    let value u = match u with Projected(x, _) | UnderstoodFixture(x, _) -> x
    let version u = match u with Projected(_, v) -> Some v | UnderstoodFixture _ -> None
    let fixtureLabel u = match u with UnderstoodFixture(_, l) -> Some l | Projected _ -> None
    let map f u = match u with Projected(x, v) -> Projected(f x, v) | UnderstoodFixture(x, l) -> UnderstoodFixture(f x, l)

/// A displayed fact, either as stored or as projected; the boundary stays visible to the view.
type Shown<'a> =
    | AsStored of Visible<'a>
    | AsProjected of Understood<'a>

[<RequireQualifiedAccess>]
module Shown =
    let value s = match s with AsStored v -> Visible.value v | AsProjected u -> Understood.value u
    let capability s = match s with AsStored _ -> DataVisible | AsProjected _ -> DataUnderstood
    let isFixture s =
        match s with
        | AsStored v -> (Visible.fixtureLabel v).IsSome
        | AsProjected u -> (Understood.fixtureLabel u).IsSome

/// A count that may be unknown. Unknown is never rendered as zero (UI-022, UI-001 "where supported").
type Count =
    | Known of int
    | UnknownCount
    | NotSupported

/// Whether an action is offered. The domain engine decides (UI-004); the UI renders only what it is given.
type Availability =
    | Available
    /// Shown disabled with the engine's reason, only when the engine supplies one.
    | Unavailable of reason: string
    /// Not rendered at all.
    | NotOffered

// ---- Validation (catalog validation blocks) ----------------------------------------------------------------------
// Close to Limen.Forms' FieldSpec, but NOT identical: Limen.Forms has Required: bool and no Pattern, counts runes, does
// not trim and reports one error per field; this checks UTF-16 length, trims for MinLength and lists every failed rule.
// Which semantics the product uses is CF-12.

type Rule =
    | Required
    | MinLength of int
    | MaxLength of int
    | Pattern of string
    | OneOf of string list

type ComponentKind =
    | TextInput
    | TextArea
    | UrlInput
    | PasswordInput
    | RadioGroup
    | Select
    | Display
    | Button
    | Link
    | Region

type FieldSpec =
    { Field: FieldId
      /// The accessible name the governed journeys look the field up by (catalog `name`).
      Label: string
      Component: ComponentKind
      Rules: Rule list }

type ValidationError = { Field: FieldId; Rule: Rule; Message: string }

[<RequireQualifiedAccess>]
module Validation =
    let private message (label: string) rule =
        match rule with
        | Required -> sprintf "%s is required." label
        | MinLength n -> sprintf "%s must have at least %d character%s." label n (if n = 1 then "" else "s")
        | MaxLength n -> sprintf "%s must have at most %d characters." label n
        | Pattern _ -> sprintf "%s is not in the expected form." label
        | OneOf options -> sprintf "Choose one of: %s." (String.Join(", ", options))

    let private holds (value: string) rule =
        match rule with
        | Required -> not (String.IsNullOrWhiteSpace value)
        // Catalog INV-019 (OD-INDY-21): whitespace-only counts as empty for a minimum length.
        | MinLength n -> value.Trim().Length >= n
        | MaxLength n -> value.Length <= n
        // An optional field that is empty is not checked against its pattern; Required/MinLength judge emptiness.
        | Pattern p -> value = "" || Regex.IsMatch(value, p)
        | OneOf options -> value = "" || List.contains value options

    /// Pure: the field's errors for this value, in rule order.
    let check (spec: FieldSpec) (value: string) : ValidationError list =
        spec.Rules
        |> List.filter (holds value >> not)
        |> List.map (fun r -> { Field = spec.Field; Rule = r; Message = message spec.Label r })

    let checkAll (specs: FieldSpec list) (values: Map<FieldId, string>) : ValidationError list =
        specs |> List.collect (fun s -> check s (values |> Map.tryFind s.Field |> Option.defaultValue ""))

/// An absolute http(s) URL, the only form INV-029 allows to be rendered as a link (UI-005 sanitising).
type SafeUrl = private SafeUrl of string

[<RequireQualifiedAccess>]
module SafeUrl =
    let pattern = @"^https?://[^\s]+$"
    let tryCreate (s: string) =
        let schemeOk = s.StartsWith("http://", StringComparison.Ordinal) || s.StartsWith("https://", StringComparison.Ordinal)
        if schemeOk && Uri.IsWellFormedUriString(s, UriKind.Absolute) && Regex.IsMatch(s, pattern) && s.Length <= 2000 then Some(SafeUrl s)
        else None
    let value (SafeUrl s) = s

// ---- Failure and loading ---------------------------------------------------------------------------------------------

/// Why data is not shown. Each case has its own plain-language copy (UiStates.describe, UI-010, UI-028).
type UiFailure =
    /// Keyed by workspace and id (v4.1.5 W1): the same case can be stored in two workspaces.
    | InvestigationNotFound of InvestigationRef
    /// The stored files did not verify (schema, digest, replay refusal). Fails closed: nothing partial is shown.
    | InvalidData of detail: string
    | NotAuthorized
    | CredentialRefused
    | ProviderUnreachable
    | ProviderRateLimited
    /// The provider answered something no case classifies.
    | ProviderError of detail: string
    /// The capability the screen needs is not provided by the current backend or by the frozen contracts.
    | CapabilityAbsent of needed: Capability * because: string

/// Data a screen is waiting for.
type Remote<'a> =
    | NotRequested
    /// The copy describes the user's task (UI-024 "Opening investigation"), never a fabricated progress figure.
    | Waiting of task: string
    | Ready of 'a
    | Failed of UiFailure

[<RequireQualifiedAccess>]
module Remote =
    let map f r =
        match r with
        | Ready x -> Ready(f x)
        | NotRequested -> NotRequested
        | Waiting t -> Waiting t
        | Failed e -> Failed e

// -- UiStates.fs --

/// The UI states every screen declares (SCREEN-MANIFEST.json `states`). A screen view is in exactly one.
type ScreenState =
    | ScreenLoading
    | ScreenReady
    | ScreenEmpty
    | ScreenFailed of UiFailure

/// The seven save states (PERSIST-031, UI-006). Presentation-only: the frozen SaveState (IF41-SAVE) decides which one
/// applies, and an adapter maps it with a total match (real-adapter-sketch/RealAdapter.fs), so an eighth state or a
/// missing one does not compile.
type SaveStateKind =
    | SavedKind
    | UnsavedKind
    | SavingKind
    | OfflineKind
    | ConflictKind
    | UnknownOutcomeKind
    | SaveFailedKind

/// What a failure tells the user (UI-010: what happened, what was preserved, a safe next step).
type FailureCopy =
    { What: string
      Preserved: string
      NextStep: string
      /// Offered only when it performs a real action (UI-010).
      Retry: bool }

/// The centralized presentation-language mapping (UI-024). Views take their words from here; no view template invents
/// a label, a meaning or a success claim. Technical detail goes to Administration (UI-017, UI-028).
[<RequireQualifiedAccess>]
module PresentationLanguage =
    /// The exact words the indicator shows (PERSIST-031; the journeys match them exactly: J1-04, J3-03..07).
    let saveState kind =
        match kind with
        | SavedKind -> "Saved"
        | UnsavedKind -> "Unsaved"
        | SavingKind -> "Saving"
        | OfflineKind -> "Offline"
        | ConflictKind -> "Conflict"
        | UnknownOutcomeKind -> "Unknown commit outcome"
        | SaveFailedKind -> "Save failed"

    let allSaveStates = [ SavedKind; UnsavedKind; SavingKind; OfflineKind; ConflictKind; UnknownOutcomeKind; SaveFailedKind ]

    /// UI-024's default labels, applied wherever the concept appears.
    let terms =
        [ "Historical investigations", "Investigations"
          "Fetching and validating the investigation", "Opening investigation"
          "Expected lifecycle", "Status"
          "Replayed state", "Investigation history"
          "Open a sandbox copy", "Explore changes"
          "Ask the engine", "Apply change to draft"
          "Engine decisions", "Change history"
          "Discard the sandbox", "Discard draft" ]

    let private unsavedNote = "Changes you have not saved are kept only in this tab until you save or close it."

    let failure (f: UiFailure) : FailureCopy =
        match f with
        | InvestigationNotFound _ ->
            { What = "This investigation could not be found in the connected repository."
              Preserved = "Nothing was changed."
              NextStep = "Return to Investigations."
              Retry = false }
        | InvalidData _ ->
            { What = "This investigation's stored history could not be read safely, so it is not shown."
              Preserved = "Nothing was changed in the repository."
              NextStep = "See Administration for details."
              Retry = false }
        | NotAuthorized ->
            { What = "You do not have access to this repository."
              Preserved = unsavedNote
              NextStep = "Check the repository name, or connect with a credential that can read it."
              Retry = false }
        | CredentialRefused ->
            { What = "The repository did not accept the credential."
              Preserved = unsavedNote
              NextStep = "Reconnect with a valid credential."
              Retry = false }
        | ProviderUnreachable ->
            { What = "The repository cannot be reached right now."
              Preserved = unsavedNote
              NextStep = "Try again when you are back online."
              Retry = true }
        | ProviderRateLimited ->
            { What = "The repository is limiting requests for now."
              Preserved = unsavedNote
              NextStep = "Try again in a few minutes."
              Retry = true }
        | ProviderError _ ->
            { What = "The repository returned an answer Causa does not recognise."
              Preserved = unsavedNote
              NextStep = "See Administration for details."
              Retry = true }
        | CapabilityAbsent _ ->
            { What = "This information is not available yet."
              Preserved = "Nothing was changed."
              NextStep = "Other sections of the investigation remain available."
              Retry = false }

    /// Words that must never appear in routine investigation copy (UI-017, UI-023). The contract tests check every
    /// string this module produces against them.
    let forbiddenInInvestigationCopy = [ "Verified"; "replay"; "schema"; "commit SHA"; "catalog identifier"; "sha256" ]

// -- Navigation.fs --

open System
open Causa.Domain
open Causa.Persistence

/// The investigation sections (INV-042, UI-029). Corrective Actions only "where supported".
type Section =
    | Overview
    | CausalAnalysis
    | EvidenceSection
    | HypothesesSection
    | TimelineSection
    | CorrectiveActions

/// Every route the in-scope requirements define. Hash routes (SCREEN-CONTRACTS URL contract, D-203).
type UiRoute =
    /// `#/` investigation-list (catalog screens[0]); before Connect it holds the connection region (UI-001, EXT-DATA-002).
    | InvestigationsRoute
    /// `#/investigations/new` (catalog screens[1]).
    | StartRoute
    /// `#/investigations/{id}?ws=<workspace>&role=<catalog|working-copy>[&tab=<section>]`. The workspace and its role
    /// are part of the identity (v4.1.5 W1: the same case can be stored in two workspaces); `tab` (S02, default
    /// overview) selects the section.
    | WorkspaceRoute of InvestigationRef * Section
    /// `#/administration`: UI-018 requires the page; no document fixes its path (OWNER-DECISIONS CF-05).
    | AdministrationRoute
    /// `#/settings` (S24).
    | SettingsRoute
    | NotFoundRoute of requested: string

type PrimaryDestination =
    | Investigations
    | Administration
    | Settings

/// One navigation link as the view renders it; `Current` becomes aria-current="page" (J2-08, J3-01).
type NavLink<'d> = { Destination: 'd; Label: string; Href: string; Current: bool }

[<RequireQualifiedAccess>]
module Navigation =
    let sectionSlug s =
        match s with
        | Overview -> "overview"
        | CausalAnalysis -> "causal-analysis"
        | EvidenceSection -> "evidence"
        | HypothesesSection -> "hypotheses"
        | TimelineSection -> "timeline"
        | CorrectiveActions -> "corrective-actions"

    /// The accessible names J2-08 and the design contract (investigationSections) look the links up by.
    let sectionLabel s =
        match s with
        | Overview -> "Overview"
        | CausalAnalysis -> "Causal Analysis"
        | EvidenceSection -> "Evidence"
        | HypothesesSection -> "Hypotheses"
        | TimelineSection -> "Timeline"
        | CorrectiveActions -> "Corrective Actions"

    let sections = [ Overview; CausalAnalysis; EvidenceSection; HypothesesSection; TimelineSection; CorrectiveActions ]

    let destinationLabel d =
        match d with
        | Investigations -> "Investigations"
        | Administration -> "Administration"
        | Settings -> "Settings"

    let destinations = [ Investigations; Administration; Settings ]

    let private guidText (InvestigationId g) = g.ToString("D")

    let roleSlug r =
        match r with
        | WorkspaceRole.Catalog -> "catalog"
        | WorkspaceRole.WorkingCopy -> "working-copy"

    let private refQuery (r: InvestigationRef) =
        let (Workspace ws) = r.Address.Workspace
        sprintf "ws=%s&role=%s" (Uri.EscapeDataString ws) (roleSlug r.Address.Role)

    /// Canonical hash route. Defaults are omitted (URL contract "canonical form"): the overview tab has no `tab`.
    let format route =
        match route with
        | InvestigationsRoute -> "#/"
        | StartRoute -> "#/investigations/new"
        | WorkspaceRoute(r, Overview) -> sprintf "#/investigations/%s?%s" (guidText r.Id) (refQuery r)
        | WorkspaceRoute(r, s) -> sprintf "#/investigations/%s?%s&tab=%s" (guidText r.Id) (refQuery r) (sectionSlug s)
        | AdministrationRoute -> "#/administration"
        | SettingsRoute -> "#/settings"
        | NotFoundRoute r -> "#/" + r.TrimStart('#', '/')

    let parse (hash: string) : UiRoute =
        let raw = hash.TrimStart('#')
        let path, query =
            match raw.IndexOf('?') with
            | -1 -> raw, ""
            | i -> raw.Substring(0, i), raw.Substring(i + 1)
        let param name =
            query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            |> Array.tryPick (fun kv -> match kv.Split('=') with [| k; v |] when k = name -> Some(Uri.UnescapeDataString v) | _ -> None)
        let tab = param "tab"
        let address =
            match param "ws", param "role" with
            | Some ws, Some "catalog" when ws <> "" -> Some { Workspace = Workspace ws; Role = WorkspaceRole.Catalog }
            | Some ws, Some "working-copy" when ws <> "" -> Some { Workspace = Workspace ws; Role = WorkspaceRole.WorkingCopy }
            | _ -> None
        let section =
            match tab with
            | None -> Some Overview
            | Some t -> sections |> List.tryFind (fun s -> sectionSlug s = t)
        match path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray with
        | [] -> InvestigationsRoute
        | [ "investigations"; "new" ] -> StartRoute
        | [ "investigations"; id ] ->
            match Guid.TryParse id, address, section with
            | (true, g), Some a, Some s -> WorkspaceRoute({ Address = a; Id = InvestigationId g }, s)
            | _ -> NotFoundRoute raw
        | [ "administration" ] -> AdministrationRoute
        | [ "settings" ] -> SettingsRoute
        | _ -> NotFoundRoute raw

    let destinationOf route =
        match route with
        | InvestigationsRoute | StartRoute | WorkspaceRoute _ | NotFoundRoute _ -> Investigations
        | AdministrationRoute -> Administration
        | SettingsRoute -> Settings

    /// The primary navigation for a route: present on every screen (INV-041), exactly one current.
    let primary route : NavLink<PrimaryDestination> list =
        let hrefOf d =
            match d with
            | Investigations -> format InvestigationsRoute
            | Administration -> format AdministrationRoute
            | Settings -> format SettingsRoute
        destinations
        |> List.map (fun d -> { Destination = d; Label = destinationLabel d; Href = hrefOf d; Current = destinationOf route = d })

    /// The section navigation of a workspace; Corrective Actions appears only when the projection supports it.
    let sectionLinks (id: InvestigationRef) (current: Section) (correctiveActionsSupported: bool) : NavLink<Section> list =
        sections
        |> List.filter (fun s -> s <> CorrectiveActions || correctiveActionsSupported)
        |> List.map (fun s -> { Destination = s; Label = sectionLabel s; Href = format (WorkspaceRoute(id, s)); Current = s = current })

// -- Connection.fs --

open Causa.App

/// The Connect form's text fields as entered. The credential is not here: it reaches the backend as an opaque frozen
/// Credential.T (UiIntent.Secret) and is never part of any view model, binding or log (PF-015, INV-21, UI-019).
type ConnectForm = { Owner: string; Repository: string; Branch: string; CredentialEntered: bool }

/// What the connected repository allows, as the provider stated it (EXT-DATA-005). Frozen: ConnectResult.ConnectAccepted.
type WriteScope =
    | ReadOnlyConnection
    | Writable
    /// Not yet known (before Connect succeeds).
    | WriteScopeUnknown

/// The connection region of investigation-list (UI-001 before Connect; EXT-DATA-002/003/007).
type ConnectionView =
    { /// SM-CONNECTION's state, frozen (IF41-APP ConnectionState); the view never invents a sixth.
      State: ConnectionState
      Form: ConnectForm
      Errors: ValidationError list
      Connect: Availability
      Disconnect: Availability
      /// "owner/repository on branch", shown after Connect (UI-022: not in the global header as a commit hash).
      Connected: string option
      WriteScope: WriteScope }

[<RequireQualifiedAccess>]
module Connection =
    /// Labels satisfy the governed journey's patterns (support.mjs connect: owner, repository, branch, token|credential;
    /// ODR-R10-07). No requirement fixes the exact words.
    let fields : FieldSpec list =
        [ { Field = Fields.repositoryOwner; Label = "Repository owner"; Component = TextInput; Rules = [ Required ] }
          { Field = Fields.repositoryName; Label = "Repository"; Component = TextInput; Rules = [ Required ] }
          { Field = Fields.branch; Label = "Branch"; Component = TextInput; Rules = [ Required ] }
          { Field = Fields.token; Label = "Access token"; Component = PasswordInput; Rules = [ Required ] } ]

    let emptyForm = { Owner = ""; Repository = ""; Branch = ""; CredentialEntered = false }

    let validate (form: ConnectForm) : ValidationError list =
        let values =
            Map.ofList
                [ Fields.repositoryOwner, form.Owner
                  Fields.repositoryName, form.Repository
                  Fields.branch, form.Branch
                  Fields.token, (if form.CredentialEntered then "entered" else "") ]
        Validation.checkAll fields values

    let withField (field: FieldId) (value: string) (form: ConnectForm) =
        if field = Fields.repositoryOwner then { form with Owner = value }
        elif field = Fields.repositoryName then { form with Repository = value }
        elif field = Fields.branch then { form with Branch = value }
        else form

    /// The state word shown to the user (UI-024; never a raw state code).
    let describe (state: ConnectionState) =
        match state with
        | Disconnected -> "Not connected"
        | Connecting _ -> "Connecting"
        | Connected true -> "Connected"
        | Connected false -> "Connected (read only)"
        | ConnectionState.CredentialRefused -> "Credential not accepted"
        | Reauthenticating -> "Reconnecting"

// -- InvestigationList.fs --

open System
open Causa.Domain
open Causa.Persistence

/// UI-014-O3 and UI-001: frozen demonstrations are told apart from the user's writable copies.
type CopyKind =
    | FrozenDemonstration
    | WorkingCopy
    | CopyKindUnknown

/// The counts a card shows "where supported" (UI-001). Each may be unknown; none defaults to zero.
type SummaryCounts = { Hypotheses: Count; Evidence: Count; UnresolvedContradictions: Count }

/// One investigation card (UI-001, UI-022). Title and last activity are Data Visible; lifecycle and counts are shown
/// with their boundary, so a stored value is never presented as the kernel's interpretation (UI-023).
type InvestigationSummary =
    { /// Where the investigation is and its id (v4.1.5 W1); opening it dispatches with this, never with the id alone.
      Ref: InvestigationRef
      Title: string
      /// The domain lifecycle (UI-022 "actual domain lifecycle/status"), never an "expected" one.
      Status: Shown<string> option
      LastActivity: DateTimeOffset option
      Counts: Shown<SummaryCounts> option
      Copy: CopyKind
      /// The canonical workspace route (INV-040 activate); also the row key the open action carries.
      Href: string }

type ListSort =
    | ByUpdated
    | ByTitle

/// investigation-list: before Connect the connection region; after Connect the Investigations region (INV-039).
type InvestigationListView =
    { Connection: ConnectionView
      /// NotRequested before Connect; Ready [] is the empty catalog (UI-010 "no investigations"), distinct from Failed.
      Investigations: Remote<Visible<InvestigationSummary list>>
      Sort: ListSort
      /// Opens investigation-start; offered only on a writable connection (J1-02 S8 is open: OWNER-DECISIONS CF-04).
      NewInvestigation: Availability
      State: ScreenState }

[<RequireQualifiedAccess>]
module InvestigationList =
    let sort (by: ListSort) (items: InvestigationSummary list) =
        match by with
        | ByTitle -> items |> List.sortBy (fun s -> s.Title.ToLowerInvariant())
        | ByUpdated -> items |> List.sortByDescending (fun s -> s.LastActivity |> Option.defaultValue DateTimeOffset.MinValue)

    let screenState (investigations: Remote<Visible<InvestigationSummary list>>) =
        match investigations with
        | NotRequested -> ScreenReady
        | Waiting _ -> ScreenLoading
        | Ready v when List.isEmpty (Visible.value v) -> ScreenEmpty
        | Ready _ -> ScreenReady
        | Failed f -> ScreenFailed f

// -- InvestigationDetail.fs --

open System
open Causa.Domain
open Causa.Persistence

/// One stored event as read (Data Visible): the wire record, uninterpreted. Shown, for example, when the kernel cannot
/// yet project a history; never presented as the investigation's state (UI-010 "fail closed", UI-023).
type StoredEvent =
    { Version: int64
      EventType: string
      OccurredAt: DateTimeOffset
      ActorId: string
      /// The payload JSON exactly as stored. Rendered as text only (UI-005 sanitising).
      PayloadJson: string }

/// The read-only record of an investigation as stored (Data Visible). No replay is claimed.
type StoredRecordView =
    { Ref: InvestigationRef
      Title: string
      CreatedAt: DateTimeOffset option
      UpdatedAt: DateTimeOffset option
      EventCount: int
      /// A page of stored events, oldest first.
      Events: StoredEvent list }

/// The workspace frame every section shares (INV-007, INV-008, INV-018, INV-042, UI-013, UI-014).
type WorkspaceFrame =
    { /// The identity every section dispatches with (v4.1.5 W1).
      Ref: InvestigationRef
      /// The investigation identifier as text (INV-006 read-only after Start; INV-034 keeps it).
      Identifier: string
      Title: string
      /// INV-008: the original problem statement, as entered.
      ProblemStatement: Shown<string>
      /// INV-023: optional; shown in the header.
      Question: Shown<string> option
      /// INV-007.
      CreatedAt: Shown<DateTimeOffset>
      /// INV-018: the domain state, from the domain only.
      InvestigationState: Shown<string>
      Copy: CopyKind
      Sections: NavLink<Section> list
      /// Which aggregate version every section of this frame refers to (UI-013); None for stored-only data.
      Version: AggregateVersion option }

[<RequireQualifiedAccess>]
module WorkspaceFrame =
    let currentSection (frame: WorkspaceFrame) =
        frame.Sections |> List.tryFind (fun l -> l.Current) |> Option.map (fun l -> l.Destination)

// -- InvestigationEditor.fs --

open Causa.App

// ---- investigation-start (S01; INV-001..006, INV-019..023) -----------------------------------------------------------

/// Where a Start will be written (INV-005), shown before any remote write.
type Destination = { Repository: string; Branch: string; Writable: bool }

type StartForm = { ProblemStatement: string; Title: string; Question: string; Identifier: string }

type StartView =
    { Form: StartForm
      Errors: ValidationError list
      Destination: Remote<Destination>
      /// INV-003 applies only when a domain profile is selected (catalog fact domain-profile-selected).
      Profile: string option
      Start: Availability
      Cancel: Availability
      /// Data Editable progress of the Start command (submit, refusal, accepted).
      Submission: Submission }

/// The progress of one submitted command. Correctness is the engine's and persistence's; the UI shows the outcome.
and Submission =
    | NotSubmitted
    | Submitting
    | Refused of reasons: string list
    | Accepted

[<RequireQualifiedAccess>]
module StartForm =
    /// The catalog's names and validation blocks, verbatim (INV-001, INV-002, INV-006, INV-023; rules INV-019/020/021).
    let fields : FieldSpec list =
        [ { Field = Fields.problemStatement; Label = "Problem statement"; Component = TextArea
            Rules = [ Required; MinLength 1; MaxLength 10000 ] }
          { Field = Fields.title; Label = "Title"; Component = TextInput; Rules = [ MaxLength 200 ] }
          { Field = Fields.question; Label = "Primary investigative question"; Component = TextArea; Rules = [ MaxLength 10000 ] }
          { Field = Fields.identifier; Label = "Investigation identifier"; Component = TextInput
            Rules = [ Required; MinLength 1; Pattern "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$" ] } ]

    let empty identifier = { ProblemStatement = ""; Title = ""; Question = ""; Identifier = identifier }

    let values (f: StartForm) =
        Map.ofList
            [ Fields.problemStatement, f.ProblemStatement
              Fields.title, f.Title
              Fields.question, f.Question
              Fields.identifier, f.Identifier ]

    let validate (f: StartForm) = Validation.checkAll fields (values f)

    let withField (field: FieldId) (value: string) (f: StartForm) =
        if field = Fields.problemStatement then { f with ProblemStatement = value }
        elif field = Fields.title then { f with Title = value }
        elif field = Fields.question then { f with Question = value }
        elif field = Fields.identifier then { f with Identifier = value }
        else f

// ---- Edit investigation details (INV-034, INV-035) --------------------------------------------------------------------

type DetailsForm = { Title: string; ProblemStatement: string; Question: string }

type DetailsEditorView =
    { Open: bool
      Form: DetailsForm
      Errors: ValidationError list
      /// INV-034 is offered only where the engine authorizes it (UI-004); under causa-data v1.0.0 there is no rename
      /// event (DATA-REPOSITORY-CONTRACT C5), so a real backend reports Unavailable until ODR-R10-26 is decided.
      Edit: Availability
      SaveDetails: Availability
      Cancel: Availability }

[<RequireQualifiedAccess>]
module DetailsForm =
    let fields : FieldSpec list =
        [ { Field = Fields.detailsTitle; Label = "Title"; Component = TextInput; Rules = [ MaxLength 200 ] }
          { Field = Fields.detailsProblem; Label = "Problem statement"; Component = TextArea; Rules = [ Required; MinLength 1; MaxLength 10000 ] }
          { Field = Fields.detailsQuestion; Label = "Primary investigative question"; Component = TextArea; Rules = [ MaxLength 10000 ] } ]

    let validate (f: DetailsForm) =
        Validation.checkAll fields (Map.ofList [ Fields.detailsTitle, f.Title; Fields.detailsProblem, f.ProblemStatement; Fields.detailsQuestion, f.Question ])

// ---- Save-state presentation (INV-015, INV-016, INV-043, UI-006, PERSIST-030..033) ------------------------------------

/// SM-CONFLICT review as the user sees it (S23): this tab's version, GitHub's version, the unsaved changes kept, and
/// the safe choices the frozen machine offers (IF41-VIEW SaveAction).
type ConflictPresentation =
    { ThisTabVersion: string
      GitHubVersion: string
      UnsavedChanges: int
      Explanation: string
      Choices: SaveAction list }

/// The frozen SaveAction choices each state offers (PERSIST-031..033), one mapping for fixture and adapter alike.
[<RequireQualifiedAccess>]
module SaveChoices =
    let forKind kind =
        match kind with
        | SavedKind | SavingKind | OfflineKind -> []
        | UnsavedKind -> [ Save ]
        | ConflictKind -> [ Review; ReapplyMine; DiscardMine; ExportMine ]
        | UnknownOutcomeKind -> [ CheckAgainAction ]
        | SaveFailedKind -> [ Retry ]

type SavePresentation =
    { Kind: SaveStateKind
      /// The frozen IF41-VIEW projection; Label equals PresentationLanguage.saveState Kind (contract test).
      Indicator: SaveIndicatorView
      Save: Availability
      Conflict: ConflictPresentation option
      /// PERSIST-033: investigations in this tab with unsaved changes.
      NotSaved: string list }

// -- InvestigationAnalysis.fs --

open System
open Causa.Domain

/// A record the UI can link to (UI-033): its kind and its stable id as text. Not a domain type.
type EntityKind =
    | ObservationEntity
    | HypothesisEntity
    | EvidenceEntity
    | RelationshipEntity
    | ConclusionEntity
    | ContradictionEntity
    | CorrectiveActionEntity
    | OtherEntity of string

type EntityRef = { Kind: EntityKind; Id: string }

/// INV-031's options (OD-INDY-23). "Not assessed" is the absence of a relationship, never an option.
type RelationshipKind =
    | Supports
    | Contradicts
    | ConsistentWith
    | Inconclusive

/// A kind as the projection supplies it: one of INV-031's options, or a stored kind no option names (causa-data's
/// `discriminates`, `insufficient`: DATA-REPOSITORY-CONTRACT C5), shown verbatim and never mapped by the UI.
type KindShown =
    | CatalogKind of RelationshipKind
    | UninterpretedKind of string

type ObservationCard = { Id: string; Text: string; SourceReference: string option; ObservedAt: DateTimeOffset option }

/// One evidence-hypothesis assessment, shown on both the evidence and the hypothesis (INV-044).
type LinkCard =
    { Id: string
      Evidence: EntityRef
      EvidenceLabel: string
      Hypothesis: EntityRef
      HypothesisStatement: string
      Kind: KindShown
      Rationale: string
      /// At most one current assessment per pair; earlier ones stay in the history (INV-030, INV-035).
      Current: bool }

type HypothesisCard =
    { Id: string
      Statement: string
      /// The hypothesis's lifecycle word as the domain states it.
      State: string
      Explanation: string option
      Links: LinkCard list }

type EvidenceCard =
    { Id: string
      /// INV-027. Option because causa-data v1.0.0 has no label field (C5); None renders as "No label recorded".
      Label: string option
      Content: string
      SourceReference: string option
      /// Only a SafeUrl is rendered as a link (INV-029, UI-005).
      SourceUrl: SafeUrl option
      Category: string option
      CapturedAt: DateTimeOffset option
      Links: LinkCard list }

type TimelineItem =
    { Version: int64
      EventType: string
      Summary: string
      OccurredAt: DateTimeOffset
      Actor: string option
      Affects: EntityRef list }

type ConclusionCard = { Id: string; Statement: string; ResidualUncertainty: string option; RecordedAtVersion: int64 option }

/// UI-032: a domain-derived next step linked to its record; never invented.
type NextStep = { Text: string; Target: EntityRef; Action: ActionId option }

/// The projection the analysis sections render (Data Understood). One value, one version, for every section (UI-013).
type AnalysisProjection =
    { Observations: ObservationCard list
      Hypotheses: HypothesisCard list
      Evidence: EvidenceCard list
      Relationships: LinkCard list
      Timeline: TimelineItem list
      Conclusions: ConclusionCard list
      NextSteps: NextStep list
      UnresolvedContradictions: Count
      CorrectiveActionsSupported: bool
      /// Which content actions the engine authorizes now (UI-004). Keys are ActionIds.
      Actions: Map<ActionId, Availability> }

// ---- Content editors (Data Editable) -----------------------------------------------------------------------------------

[<RequireQualifiedAccess>]
module ContentForms =
    let observation : FieldSpec list =
        [ { Field = Fields.observationText; Label = "Observation text"; Component = TextArea; Rules = [ Required; MinLength 1; MaxLength 10000 ] }
          { Field = Fields.observationSource; Label = "Observation source reference"; Component = TextInput; Rules = [ MaxLength 10000 ] } ]

    let hypothesis : FieldSpec list =
        [ { Field = Fields.hypothesisStatement; Label = "Hypothesis statement"; Component = TextArea; Rules = [ Required; MinLength 1; MaxLength 10000 ] }
          // INV-011 has no validation block in the catalog; required: true.
          { Field = Fields.hypothesisExplanation; Label = "Hypothesis explanation"; Component = TextArea; Rules = [ Required ] } ]

    /// The fields checked when a hypothesis is first recorded. J2-02 records with the statement only, while the catalog
    /// marks INV-011 required: until CF-03 is decided this list checks only the statement, and `hypothesis` keeps the
    /// catalog rule unchanged for the detail editor (S04). No requirement is weakened in the manifest.
    let hypothesisOnRecord : FieldSpec list = [ hypothesis.Head ]

    let evidence : FieldSpec list =
        [ { Field = Fields.evidenceLabel; Label = "Evidence label"; Component = TextInput; Rules = [ Required; MinLength 1; MaxLength 200 ] }
          { Field = Fields.evidenceSource; Label = "Evidence source reference"; Component = TextInput; Rules = [ MaxLength 10000 ] }
          { Field = Fields.evidenceUrl; Label = "Evidence source URL"; Component = UrlInput; Rules = [ MaxLength 2000; Pattern SafeUrl.pattern ] }
          // INV-012 has no validation block; required: true.
          { Field = Fields.evidenceContent; Label = "Evidence original content"; Component = TextArea; Rules = [ Required ] } ]

    let link : FieldSpec list =
        [ { Field = Fields.linkEvidence; Label = "Evidence"; Component = Select; Rules = [ Required ] }
          { Field = Fields.linkHypothesis; Label = "Hypothesis"; Component = Select; Rules = [ Required ] }
          { Field = Fields.relationshipKind; Label = "Evidence relationship kind"; Component = RadioGroup
            Rules = [ Required; OneOf [ "supports"; "contradicts"; "consistent-with"; "inconclusive" ] ] }
          { Field = Fields.relationshipRationale; Label = "Evidence relationship rationale"; Component = TextArea; Rules = [ Required; MinLength 1; MaxLength 10000 ] } ]

    let conclusion : FieldSpec list =
        [ { Field = Fields.conclusionStatement; Label = "Conclusion statement"; Component = TextArea; Rules = [ Required; MinLength 1; MaxLength 10000 ] } ]

    let all = observation @ hypothesis @ evidence @ link @ conclusion

    let kindValue k =
        match k with
        | Supports -> "supports"
        | Contradicts -> "contradicts"
        | ConsistentWith -> "consistent-with"
        | Inconclusive -> "inconclusive"

/// The text of the open content editors and their validation (one map: the editor never holds a second store).
type EditorDrafts = { Values: Map<FieldId, string>; Errors: ValidationError list; OpenEditor: ActionId option }

// ---- investigation-workspace -------------------------------------------------------------------------------------------

/// The content of the current section. Analysis sections need Data Understood; without it they say so (CapabilityAbsent),
/// never "not supported" as a substitute for content (J2-08) and never a partial replay.
type SectionView =
    | OverviewSection of Remote<Understood<AnalysisProjection>>
    | CausalAnalysisView of Remote<Understood<AnalysisProjection>>
    | EvidenceView of Remote<Understood<AnalysisProjection>>
    | HypothesesView of Remote<Understood<AnalysisProjection>>
    | TimelineView of Remote<Understood<AnalysisProjection>>
    | CorrectiveActionsView of Remote<Understood<AnalysisProjection>>

type WorkspaceView =
    { Frame: Remote<WorkspaceFrame>
      /// The stored record (Data Visible), available even where no projection is.
      Stored: Remote<Visible<StoredRecordView>>
      Section: SectionView
      Drafts: EditorDrafts
      Details: DetailsEditorView
      Save: SavePresentation option
      State: ScreenState }

// -- System.fs --

/// A system fact on Administration (UI-018): unknown, unavailable, failed and verified are distinct, and nothing
/// unverified is shown as passed.
type SystemFactStatus =
    | FactKnown of string
    | FactUnknown
    | FactUnavailable
    | FactFailed of string

type SystemFact = { Label: string; Status: SystemFactStatus }

/// Administration (UI-018, UI-031): read-only; system detail moves here from investigation screens (UI-017, UI-022).
type AdministrationView =
    { Provider: SystemFact
      Repository: SystemFact
      Connection: SystemFact
      Catalog: SystemFact
      StorageContract: SystemFact
      ApplicationVersion: SystemFact
      DeployedCommit: SystemFact
      DataValidation: SystemFact
      Diagnostics: string list }

/// Where a preference lives (UI-019 "define persistence and scope of each setting").
type PreferenceScope =
    | SessionScope
    | DeviceScope

/// One supported preference. Which preferences exist is an owner decision (OWNER-DECISIONS CF-06); fixtures label theirs.
type Preference =
    { Id: string
      Label: string
      Scope: PreferenceScope
      Options: string list
      Effective: string }

type SettingsView =
    { Preferences: Preference list
      /// "Distinguish a preference from a verified external connection" (UI-019).
      ConnectionPreference: SystemFact }

type NotFoundView = { Requested: string; Copy: FailureCopy }

// -- UiActions.fs --

open Causa.Domain
open Causa.Persistence

/// The screen a route shows.
type ScreenView =
    | ListScreen of InvestigationListView
    | StartScreen of StartView
    | WorkspaceScreen of WorkspaceView
    | AdministrationScreen of AdministrationView
    | SettingsScreen of SettingsView
    | NotFoundScreen of NotFoundView

/// Who produced a view. A fixture-backed view can never be reported as an integrated feature: acceptance evidence
/// requires Integrated, and only an integration adapter over the real engine says so (VERIFICATION-STRATEGY.md).
type BackendOrigin =
    | FixtureBackend of FixtureLabel
    | IntegratedBackend

type AppView =
    { Route: UiRoute
      Primary: NavLink<PrimaryDestination> list
      Screen: ScreenView
      /// Polite/assertive announcements for the live region (frozen Politeness).
      Announcements: (Politeness * string) list
      Origin: BackendOrigin }

/// What the user did, in Limen's SemanticEvent shape: an element id, an optional row key, an optional value. Views emit
/// only these; they never call a backend function, decide legality or build a domain command.
type UiIntent =
    | Navigate of UiRoute
    /// A field's value changed (data-on="input").
    | Input of field: FieldId * value: string * key: string option
    /// The credential field: turned into the opaque frozen Credential.T before it reaches any backend.
    | Secret of field: FieldId * Credential.T
    /// A button or link was activated.
    | Activate of action: ActionId * key: string option

/// The one substitutable interface. The fixture backend (fixtures/) and the integration adapter (real-adapter-sketch/)
/// both implement it; screens depend only on AppView and UiIntent. Pure: effects are data the host performs.
type UiBackend<'state, 'effect> =
    { /// The boundaries this backend actually provides; a screen needing more shows CapabilityAbsent.
      Provides: Capability list
      Init: UiRoute -> 'state * 'effect list
      Dispatch: UiIntent -> 'state -> 'state * 'effect list
      Project: 'state -> AppView }

/// An action's contract, as data (SCREEN-MANIFEST.json `actions` is checked against these).
type ActionContract =
    { Action: ActionId
      Screen: ScreenId
      Label: string
      Component: ComponentKind
      Needs: Capability
      /// The fields validated before the action is sent.
      Validates: FieldId list
      Outcome: string }

[<RequireQualifiedAccess>]
module ActionContracts =
    let private a action screen label comp needs validates outcome =
        { Action = action; Screen = screen; Label = label; Component = comp; Needs = needs; Validates = validates; Outcome = outcome }

    let all : ActionContract list =
        [ a Actions.connect Screens.list "Connect data repository" Button DataVisible
              [ Fields.repositoryOwner; Fields.repositoryName; Fields.branch; Fields.token ] "SM-CONNECTION Connecting, then Connected or a stated refusal"
          a Actions.disconnect Screens.list "Disconnect" Button DataVisible [] "SM-CONNECTION DisconnectConfirmed"
          a Actions.openInvestigation Screens.list "Open investigation" Link DataVisible [] "route #/investigations/{id}; the workspace shows its problem statement"
          a Actions.newInvestigation Screens.list "New investigation" Link DataEditable [] "route #/investigations/new"
          a Actions.retryLoad Screens.list "Try again" Button DataVisible [] "the failed read is sent again (offered only when it performs a real action)"
          a Actions.startInvestigation Screens.start "Start investigation" Button DataEditable
              [ Fields.problemStatement; Fields.title; Fields.question; Fields.identifier ] "StartInvestigation command; the workspace opens"
          a Actions.cancelStart Screens.start "Cancel" Button DataEditable [] "leaves the start screen; nothing is created or written"
          a Actions.recordObservation Screens.workspace "Record an observation" Button DataEditable [ Fields.observationText; Fields.observationSource ] "RecordObservation; the text is listed"
          a Actions.recordHypothesis Screens.workspace "Record a hypothesis" Button DataEditable [ Fields.hypothesisStatement ] "RecordHypothesis; it is listed"
          a Actions.recordEvidence Screens.workspace "Record evidence" Button DataEditable
              [ Fields.evidenceLabel; Fields.evidenceSource; Fields.evidenceUrl; Fields.evidenceContent ] "AdmitEvidence; it is listed"
          a Actions.linkEvidence Screens.workspace "Link evidence to a hypothesis" Button DataEditable
              [ Fields.linkEvidence; Fields.linkHypothesis; Fields.relationshipKind; Fields.relationshipRationale ] "RelateEvidence; shown on the evidence and the hypothesis"
          a Actions.reassess Screens.workspace "Reassess" Button DataEditable
              [ Fields.linkEvidence; Fields.linkHypothesis; Fields.relationshipKind; Fields.relationshipRationale ] "a new current assessment; the earlier one stays in the history"
          a Actions.recordConclusion Screens.workspace "Record a conclusion" Button DataEditable [ Fields.conclusionStatement ] "a conclusion snapshot; the investigation is not resolved"
          a Actions.editDetails Screens.workspace "Edit investigation details" Button DataEditable [] "opens the details editor"
          a Actions.saveDetails Screens.workspace "Save details" Button DataEditable [ Fields.detailsTitle; Fields.detailsProblem; Fields.detailsQuestion ] "details changed; the identifier is unchanged"
          a Actions.cancelEditor Screens.workspace "Cancel" Button DataEditable [] "closes the editor; nothing entered is recorded or written"
          a Actions.save Screens.workspace "Save" Button DataEditable [] "SM-SAVE SaveRequested; Saved only after read-back confirmation"
          a Actions.saveIndicator Screens.workspace "Save-state action" Button DataEditable [] "the frozen SaveAction (Retry, Reconnect, Check again, Review, Reapply, Discard, Export)" ]

// -- Bindings.fs --

/// Limen 0.9.1's view-state shape (limen.core contract, guests/fsharp/Limen.Contract Core.fs ViewValue/ViewPrimitive),
/// mirrored case for case so this library compiles before the integration lane adds EchelonFoundry.Limen.Contract at
/// T10. Integration replaces these two types with aliases of Limen.Contract.Core.ViewValue/ViewPrimitive; no screen
/// code changes. A screen module's whole output is a Bindings map.
[<RequireQualifiedAccess>]
type BindingPrimitive =
    | Text of string
    | Number of float
    | Flag of bool

[<RequireQualifiedAccess>]
type BindingValue =
    | Text of string
    | Number of float
    | Flag of bool
    | Items of Map<string, BindingPrimitive> list

type Bindings = Map<string, BindingValue>

[<RequireQualifiedAccess>]
module Bindings =
    let text (k: string) v : string * BindingValue = k, BindingValue.Text v
    let flag (k: string) v : string * BindingValue = k, BindingValue.Flag v
    let number (k: string) (v: float) : string * BindingValue = k, BindingValue.Number v
    let items (k: string) (rows: (string * BindingPrimitive) list list) : string * BindingValue =
        k, BindingValue.Items(rows |> List.map Map.ofList)

    /// Every key of a binding map (top level and row fields), for the contract test that no binding names a credential.
    let keys (b: Bindings) =
        b
        |> Map.toList
        |> List.collect (fun (k, v) ->
            match v with
            | BindingValue.Items rows -> k :: (rows |> List.collect (Map.toList >> List.map fst))
            | _ -> [ k ])
        |> List.distinct

    /// Every text value, for the copy checks (UI-017, UI-023, credential never rendered).
    let texts (b: Bindings) =
        b
        |> Map.toList
        |> List.collect (fun (_, v) ->
            match v with
            | BindingValue.Text s -> [ s ]
            | BindingValue.Items rows ->
                rows |> List.collect (Map.toList >> List.choose (fun (_, p) -> match p with BindingPrimitive.Text s -> Some s | _ -> None))
            | _ -> [])
