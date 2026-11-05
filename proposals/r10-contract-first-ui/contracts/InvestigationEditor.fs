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
/// Qualified access: Refused would otherwise collide with IF41-SAVE SaveFailure.Refused.
and [<RequireQualifiedAccess>] Submission =
    | NotSubmitted
    | Submitting
    | Refused of reasons: string list
    | Accepted

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

// ---- Save-state presentation (INV-015, INV-016, INV-043, UI-006, PERSIST-030..033) ------------------------------------

/// SM-CONFLICT review as the user sees it (S23): this tab's version, GitHub's version, the unsaved changes kept, and
/// the safe choices the frozen machine offers (IF41-VIEW SaveAction).
type ConflictPresentation =
    { ThisTabVersion: string
      GitHubVersion: string
      UnsavedChanges: int
      Explanation: string
      Choices: SaveAction list }

type SavePresentation =
    { Kind: SaveStateKind
      /// The frozen IF41-VIEW projection; Label equals PresentationLanguage.saveState Kind (contract test).
      Indicator: SaveIndicatorView
      Save: Availability
      Conflict: ConflictPresentation option
      /// PERSIST-033: investigations in this tab with unsaved changes.
      NotSaved: string list }
