namespace Causa.Ui.Contracts

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
