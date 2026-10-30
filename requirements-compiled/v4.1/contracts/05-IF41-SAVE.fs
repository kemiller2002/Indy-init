namespace Causa.Persistence

open Causa.Domain

/// IF41-SAVE: the SM-SAVE / SM-CONFLICT machine (state-machines.json). Pure; effects are data.
/// Confirmation has a private representation: only this assembly constructs it (PERSIST-023).
type Confirmation =
    private
        { Investigation: InvestigationId
          Commit: CommitOid
          CommittedAt: System.DateTimeOffset }
    member c.InvestigationId = c.Investigation
    member c.CommitOid = c.Commit
    member c.CommitTime = c.CommittedAt

type WriteMarker = { Session: System.Guid; Sequence: int64 }

type SaveFailure =
    | Refused of ProviderError
    | RepositoryKeptChanging
    | BytesDiffer of CommitOid
    | HistoryWasRewritten
    | CredentialRefused

type ConflictView =
    | ReadingRemote
    | Reviewable of remote: VerifiedInvestigation
    | Reapplying
    | ConfirmingDiscard of remote: VerifiedInvestigation
    | RemoteUnavailable
    | Unresolvable of reason: string

type SaveState =
    | Saved of Confirmation
    | Unsaved
    | Saving
    | Offline
    | Conflict of ConflictView
    | UnknownOutcome of automaticReadsLeft: int
    | SaveFailed of SaveFailure

type SaveInput =
    | Edit of EventPayload
    | SaveRequested
    | QueuedSaveStarted
    | Reply of HttpOutcome
    | WentOffline
    | WentOnline
    | CheckAgain
    | TimerElapsed of RequestId
    | ConflictAction of string

type SaveEffect =
    | Send of HttpRequest
    | StartTimer of RequestId * milliseconds: int
    | Announce of politeness: string * text: string
    | ExportCommands of EventPayload list

type SaveFault = { State: string; Input: string; Reason: string }

type InvestigationSession =
    { Id: InvestigationId
      BaseHead: CommitOid
      State: SaveState
      Unsaved: EventPayload list
      AfterInFlight: EventPayload list
      Outstanding: RequestId option
      Mutations: int
      RebaseAttempts: int }

type SaveMachine =
    { Initial: VerifiedInvestigation -> Confirmation -> InvestigationSession
      Step: InvestigationSession -> SaveInput -> Result<InvestigationSession * SaveEffect list, SaveFault> }
