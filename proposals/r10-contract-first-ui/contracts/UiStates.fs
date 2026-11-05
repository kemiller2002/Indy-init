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
