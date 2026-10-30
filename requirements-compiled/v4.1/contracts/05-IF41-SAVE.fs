namespace Causa.Persistence

open Causa.Domain

/// IF41-SAVE: the SM-SAVE, SM-CONFLICT and SM-LOAD machines (requirements-compiled/v4/state-machines.json). Pure;
/// effects are data. Each state union has exactly the machine's states and each input union exactly its events
/// (check X05); state-machine-contracts.json decides every (state, input) pair.
/// Confirmation (PERSIST-023): the only proof that a save succeeded. Its representation is private to this module, so
/// no other file, in this assembly or another, can build one; its only constructor requires the bytes re-read at the
/// commit to equal the bytes written. (A caller can still pass the same bytes twice: the plan's INV-01 tests guard
/// that path.)
[<RequireQualifiedAccess>]
module Confirmation =
    type T =
        private
            { Investigation: InvestigationId
              Commit: CommitOid
              CommittedAt: System.DateTimeOffset }
        member c.InvestigationId = c.Investigation
        member c.CommitOid = c.Commit
        member c.CommitTime = c.CommittedAt

    let ofReadBack (investigation: InvestigationId) (commit: CommitOid) (committedAt: System.DateTimeOffset) (written: FileSet) (readBack: FileSet) : T option =
        if written = readBack then Some { Investigation = investigation; Commit = commit; CommittedAt = committedAt } else None

type WriteMarker = { Session: System.Guid; Sequence: int64 }

type SaveFailure =
    | Refused of ProviderError
    | RepositoryKeptChanging
    | BytesDiffer of CommitOid
    | HistoryWasRewritten
    | CredentialNotAccepted

/// An unsaved command the engine refused when it was re-decided against the GitHub version, with the engine's reason
/// (T-C07, PERSIST-012).
type RefusedCommand = { Command: CommandEnvelope; Reason: string }

/// SM-CONFLICT states.
type ConflictView =
    | ReadingRemote
    | Reviewable of remote: VerifiedInvestigation
    | Reapplying of remote: VerifiedInvestigation
    | Reapplied of remote: VerifiedInvestigation * refused: RefusedCommand list
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
    | ReapplyDone of accepted: CommandEnvelope list * refused: RefusedCommand list
    | DiscardChosen
    | DiscardConfirmed
    | DiscardCancelled
    | ExportChosen
    | SupersededReply of RequestId

/// SM-SAVE states (PERSIST-031: exactly seven).
type SaveState =
    | Saved of Confirmation.T
    | Unsaved
    | Saving
    | Offline
    | Conflict of ConflictView
    | UnknownOutcome of automaticReadsLeft: int
    | SaveFailed of SaveFailure

/// SM-SAVE events. Persistence produces every reply event from an HttpOutcome with SaveMachine.Interpret; the App
/// produces only Edit, SaveRequested, CheckAgain, WentOffline, WentOnline and QueuedSaveStarted.
type SaveInput =
    | Edit of CommandEnvelope
    | SaveRequested
    | MutationSent of RequestId
    | ReadBackMatched of Confirmation.T
    | ReadBackMismatched of CommitOid
    | StaleHeadUnrelated of freshHead: CommitOid
    | StaleHeadSameInvestigation of freshHead: CommitOid
    | ProviderRefused of ProviderError
    | CredentialRefused
    | AnswerLost
    | FailedBeforeSending
    | ReconcileMarkerBytesMatch of Confirmation.T
    | ReconcileMarkerBytesDiffer of CommitOid
    | ReconcileNothingWrittenHeadUnchanged
    | ReconcileNothingWrittenHeadMoved of head: CommitOid
    | ReconcileHistoryRewritten
    | ReconcileReadFailed
    | ReconcileReadsSpent
    | CheckAgain
    | WentOffline
    | WentOnline
    | ConflictResolvedReapplied of CommandEnvelope list
    | ConflictResolvedDiscarded of Confirmation.T
    | SupersededReply of RequestId
    | QueuedSaveStarted
    | RebaseReadFailed
    | ReadBackReadFailed

/// Why a load failed: the files did not verify, or the provider refused or did not answer (IF41-ERRORS), so the
/// application can state the cause truthfully (PERSIST-024).
type LoadFailure =
    | Unverifiable of VerificationFailure list
    | ProviderFailed of ProviderError

/// SM-LOAD states.
type LoadState =
    | NotLoaded
    | Loading of RequestId
    | Loaded of VerifiedInvestigation
    | LoadFailed of LoadFailure
    | UnavailableOffline

/// SM-LOAD events.
type LoadInput =
    | OpenRequested of InvestigationId
    | LoadVerified of VerifiedInvestigation
    | LoadRejected of LoadFailure
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
    | ExportCommands of CommandEnvelope list

/// The fail-closed answer to an illegal (state, input) pair: state, base head and unsaved changes are unchanged.
type SaveFault = { Machine: string; State: string; Input: string; Reason: string }

/// The answer a session is waiting for, so Interpret knows what it means. These are steps inside Saving and
/// UnknownOutcome; no machine state is added.
type Awaiting =
    | MutationAnswer
    | ReadBackAt of CommitOid
    | FreshHead
    | FileComparisonAt of freshHead: CommitOid
    | ReconcileHead
    | ReconcileCommits of page: int
    | ReconcileFilesAt of CommitOid

/// What one answer means for a session.
type Interpretation =
    /// The answer completes a step: apply this event with SaveMachine.Step.
    | MachineEvent of SaveInput
    /// The answer leads to the next read of the same step (read-back after the mutation, file comparison after a
    /// fresh head, the next reconciliation page); the state does not change.
    | NextRequest of HttpRequest * Awaiting

type InvestigationSession =
    { Id: InvestigationId
      BaseHead: CommitOid
      State: SaveState
      Unsaved: CommandEnvelope list
      AfterInFlight: CommandEnvelope list
      Outstanding: (RequestId * Awaiting) option
      Mutations: int
      RebaseAttempts: int
      ReconcileReads: int }

type SaveMachine =
    { Initial: VerifiedInvestigation -> Confirmation.T -> InvestigationSession
      /// Classifies and verifies one answer (IF41-ERRORS, read-back, reconciliation) against what the session awaits:
      /// a machine event, or the next request of the same step. An answer to a request that is not Outstanding is
      /// MachineEvent SupersededReply.
      Interpret: InvestigationSession -> HttpOutcome -> Interpretation
      Step: InvestigationSession -> SaveInput -> Result<InvestigationSession * SaveEffect list, SaveFault>
      StepConflict: ConflictView -> ConflictInput -> Result<ConflictView * SaveEffect list, SaveFault>
      StepLoad: LoadState -> LoadInput -> Result<LoadState * SaveEffect list, SaveFault> }
