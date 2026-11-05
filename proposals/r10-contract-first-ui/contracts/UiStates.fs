namespace Causa.Ui.Contracts

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
