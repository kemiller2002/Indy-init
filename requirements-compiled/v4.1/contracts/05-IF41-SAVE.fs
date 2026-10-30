namespace Causa.Persistence

open Causa.Domain

/// IF41-SAVE: the SM-SAVE, SM-CONFLICT and SM-LOAD machines (requirements-compiled/v4/state-machines.json). Pure;
/// effects are data. Each state union has exactly the machine's states and each input union exactly its events
/// (check X05); state-machine-contracts.json decides every (state, input) pair.
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
    | CredentialNotAccepted

/// SM-CONFLICT states.
type ConflictView =
    | ReadingRemote
    | Reviewable of remote: VerifiedInvestigation
    | Reapplying of remote: VerifiedInvestigation
    | Reapplied of remote: VerifiedInvestigation
    | ConfirmingDiscard of remote: VerifiedInvestigation
    | Discarded of remote: VerifiedInvestigation
    | RemoteUnavailable
    | Unresolvable of reason: string

/// SM-CONFLICT events.
type ConflictInput =
    | RemoteVerified of VerifiedInvestigation
    | RemoteReadFailed of TransportFailure
    | RemoteMissingOrInvalid of VerificationFailure option
    | ReadAgain
    | ReviewChosen
    | ReapplyChosen
    | ReapplyDone of EventPayload list
    | DiscardChosen
    | DiscardConfirmed
    | DiscardCancelled
    | ExportChosen
    | SupersededReply of RequestId

/// SM-SAVE states (PERSIST-031: exactly seven).
type SaveState =
    | Saved of Confirmation
    | Unsaved
    | Saving
    | Offline
    | Conflict of ConflictView
    | UnknownOutcome of automaticReadsLeft: int
    | SaveFailed of SaveFailure

/// SM-SAVE events. Persistence produces every reply event from an HttpOutcome with SaveMachine.Interpret; the App
/// produces only Edit, SaveRequested, CheckAgain, WentOffline, WentOnline and QueuedSaveStarted.
type SaveInput =
    | Edit of EventPayload
    | SaveRequested
    | MutationSent of RequestId
    | ReadBackMatched of Confirmation
    | ReadBackMismatched of CommitOid
    | StaleHeadUnrelated of freshHead: CommitOid
    | StaleHeadSameInvestigation of freshHead: CommitOid
    | ProviderRefused of ProviderError
    | CredentialRefused
    | AnswerLost
    | FailedBeforeSending
    | ReconcileMarkerBytesMatch of Confirmation
    | ReconcileMarkerBytesDiffer of CommitOid
    | ReconcileNothingWrittenHeadUnchanged
    | ReconcileNothingWrittenHeadMoved of head: CommitOid
    | ReconcileHistoryRewritten
    | ReconcileReadFailed
    | ReconcileReadsSpent
    | CheckAgain
    | WentOffline
    | WentOnline
    | ConflictResolvedReapplied of EventPayload list
    | ConflictResolvedDiscarded of Confirmation
    | SupersededReply of RequestId
    | QueuedSaveStarted
    | RebaseReadFailed
    | ReadBackReadFailed

/// SM-LOAD states.
type LoadState =
    | NotLoaded
    | Loading of RequestId
    | Loaded of VerifiedInvestigation
    | LoadFailed of VerificationFailure
    | UnavailableOffline

/// SM-LOAD events.
type LoadInput =
    | OpenRequested of InvestigationId
    | LoadVerified of VerifiedInvestigation
    | LoadRejected of VerificationFailure
    | WentOffline
    | WentOnline
    | RefreshRequested
    | SupersededReply of RequestId

type Politeness =
    | Polite
    | Assertive

type SaveEffect =
    | Send of HttpRequest
    | ScheduleReconcileRead of RequestId * afterMilliseconds: int
    | Announce of Politeness * text: string
    | ExportCommands of EventPayload list

/// The fail-closed answer to an illegal (state, input) pair: state, base head and unsaved changes are unchanged.
type SaveFault = { Machine: string; State: string; Input: string; Reason: string }

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
      /// Classifies and verifies one answer (IF41-ERRORS, reconciliation, read-back) into the event it means for this
      /// session; an answer to a request that is not Outstanding becomes SupersededReply.
      Interpret: InvestigationSession -> HttpOutcome -> SaveInput
      Step: InvestigationSession -> SaveInput -> Result<InvestigationSession * SaveEffect list, SaveFault>
      StepConflict: ConflictView -> ConflictInput -> Result<ConflictView * SaveEffect list, SaveFault>
      StepLoad: LoadState -> LoadInput -> Result<LoadState * SaveEffect list, SaveFault> }
