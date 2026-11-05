namespace Causa.Ui.Contracts

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
