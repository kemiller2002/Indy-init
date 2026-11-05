/// EXP-INDY-002 frozen contract surface (Phase A, frozen at T+10).
/// Every UI-editable field binds to a property of one of the *Input records; every UI action maps to a Command case
/// or an App message; every Command has a defined input, output (Event list) and failure (DomainError) behaviour.
/// CONTRACT-REGISTRY.json maps catalog IDs (INV-0nn) to these names; tools/contract-check.fsx verifies the mapping
/// against this compiled assembly by reflection. Changes after the freeze are recorded in CONTRACT-CHANGES.json.
module Exp002.Contracts

// ---------- Identifiers ----------

/// INV-006: a UUID. Kept as validated text so the UI field binds to a string and the domain never sees a bad one.
type InvestigationId = InvestigationId of string
type ObservationId = ObservationId of string
type HypothesisId = HypothesisId of string
type EvidenceId = EvidenceId of string
type RelationshipId = RelationshipId of string

/// Milliseconds since the Unix epoch, UTC. Supplied by the boundary; the domain never reads a clock.
type Timestamp = Timestamp of int64

// ---------- UI-bound input records (every user-editable field) ----------

/// Screen investigation-start. INV-006, INV-001, INV-002, INV-023.
type StartInvestigationInput =
    { InvestigationId: string
      ProblemStatement: string
      Title: string
      PrimaryQuestion: string }

/// Workspace "Edit investigation details" (INV-034). The identifier is not editable here.
type InvestigationDetailsInput =
    { ProblemStatement: string
      Title: string
      PrimaryQuestion: string }

/// INV-024, INV-025.
type ObservationInput = { ObservationText: string; SourceReference: string }

/// INV-026, INV-011.
type HypothesisInput = { Statement: string; Explanation: string }

/// INV-027, INV-012, INV-028, INV-029.
type EvidenceInput =
    { Label: string
      OriginalContent: string
      SourceReference: string
      SourceUrl: string }

/// INV-031 options (OD-INDY-23). "Not assessed" is the absence of a relationship, never a case.
type RelationshipKind =
    | Supports
    | Contradicts
    | ConsistentWith
    | Inconclusive

/// INV-030, INV-031, INV-038. Kind is None until the user selects one (no default selection).
type RelationshipInput =
    { EvidenceId: string
      HypothesisId: string
      Kind: RelationshipKind option
      Rationale: string }

/// Reassessment of an existing relationship (OD-INDY-23 (3)).
type RelationshipRevisionInput = { Kind: RelationshipKind option; Rationale: string }

// ---------- Validated domain values ----------

type Observation =
    { Id: ObservationId
      Text: string
      SourceReference: string option }

type Hypothesis =
    { Id: HypothesisId
      Statement: string
      Explanation: string }

type Evidence =
    { Id: EvidenceId
      Label: string
      OriginalContent: string
      SourceReference: string option
      SourceUrl: string option }

/// A relationship's lifecycle: Current -> Revised (superseded by a new Current) | Withdrawn. History keeps all.
type RelationshipStatus =
    | Current
    | Revised of supersededBy: RelationshipId
    | Withdrawn of reason: string

type Relationship =
    { Id: RelationshipId
      EvidenceId: EvidenceId
      HypothesisId: HypothesisId
      Kind: RelationshipKind
      Rationale: string
      Status: RelationshipStatus }

/// INV-018. An investigation is Open from the moment it is started; this slice has no resolve transition.
type InvestigationState = | Open

type InvestigationDetails =
    { ProblemStatement: string
      Title: string option
      PrimaryQuestion: string option }

// ---------- Commands, events, errors ----------

type Command =
    | StartInvestigation of StartInvestigationInput
    | EditInvestigationDetails of InvestigationDetailsInput
    | RecordObservation of ObservationInput
    | EditObservation of ObservationId * ObservationInput
    | RecordHypothesis of HypothesisInput
    | EditHypothesis of HypothesisId * HypothesisInput
    | AdmitEvidence of EvidenceInput
    | EditEvidence of EvidenceId * EvidenceInput
    | LinkEvidence of RelationshipInput
    | ReviseRelationship of RelationshipId * RelationshipRevisionInput
    | WithdrawRelationship of RelationshipId * reason: string

/// Events carry full bodies (ODR-V412-02), so every entered value is persisted and replayable.
type Event =
    | InvestigationStarted of id: InvestigationId * details: InvestigationDetails * at: Timestamp
    | InvestigationDetailsEdited of InvestigationDetails * at: Timestamp
    | ObservationRecorded of Observation * at: Timestamp
    | ObservationEdited of Observation * at: Timestamp
    | HypothesisRecorded of Hypothesis * at: Timestamp
    | HypothesisEdited of Hypothesis * at: Timestamp
    | EvidenceAdmitted of Evidence * at: Timestamp
    | EvidenceEdited of Evidence * at: Timestamp
    | RelationshipRecorded of Relationship * at: Timestamp
    | RelationshipRevised of previous: RelationshipId * next: Relationship * at: Timestamp
    | RelationshipWithdrawn of RelationshipId * reason: string * at: Timestamp

/// A field-level refusal: the catalog ID of the field and a message the UI shows next to it.
type FieldError = { FieldId: string; Message: string }

type DomainError =
    | Invalid of FieldError list
    | NotStarted
    | AlreadyStarted
    | UnknownObservation of ObservationId
    | UnknownHypothesis of HypothesisId
    | UnknownEvidence of EvidenceId
    | UnknownRelationship of RelationshipId
    | DuplicateRelationship of existing: RelationshipId
    | RelationshipNotCurrent of RelationshipId

/// One entry of the history (INV-035): the event and its position in the log.
type HistoryEntry = { Sequence: int; Event: Event }

/// The replayed state of one investigation. Lists are in recording order.
type Investigation =
    { Id: InvestigationId
      CreatedAt: Timestamp
      State: InvestigationState
      Details: InvestigationDetails
      Observations: Observation list
      Hypotheses: Hypothesis list
      Evidence: Evidence list
      Relationships: Relationship list
      History: HistoryEntry list }

// ---------- Persistence boundary ----------

/// The persisted document: the ordered event log of one investigation, with the version it was saved at.
type StoredInvestigation =
    { Id: InvestigationId
      Version: int
      Events: Event list }

type PersistenceError =
    | NotFound of InvestigationId
    | VersionConflict of expected: int * actual: int
    | Corrupt of detail: string
    | StorageUnavailable of detail: string

/// SM-SAVE (PERSIST-031): exactly seven states. Offline is representable; this slice never enters it.
type SaveState =
    | Saved of version: int * at: Timestamp
    | Unsaved
    | Saving
    | Offline
    | Conflict of remoteVersion: int
    | UnknownOutcome
    | SaveFailed of reason: string

/// Inputs that move the save state machine. CR-3: WriteConfirmed keeps Saving (read-back pending); only
/// ReadBackMatched(version, at) enters Saved, so Saved is never claimed before the read-back (PERSIST-007).
type SaveEvent =
    | Edited
    | SaveRequested
    | WriteConfirmed of version: int * at: Timestamp
    | ReadBackMatched of version: int * at: Timestamp
    | ReadBackMismatched
    | WriteRejected of PersistenceError
    | WriteOutcomeUnknown

// ---------- Function signatures (implemented in Phase B) ----------

/// Validation.fs
type ValidateStart = StartInvestigationInput -> Result<InvestigationId * InvestigationDetails, FieldError list>
type ValidateDetails = InvestigationDetailsInput -> Result<InvestigationDetails, FieldError list>
type ValidateObservation = ObservationInput -> Result<string * string option, FieldError list>
type ValidateHypothesis = HypothesisInput -> Result<string * string, FieldError list>
type ValidateEvidence = EvidenceInput -> Result<EvidenceInput, FieldError list>
type ValidateRelationship = RelationshipInput -> Result<RelationshipKind * string, FieldError list>

/// Domain.fs: decide is total and pure; evolve folds one event; replay folds a log.
type Decide = Investigation option -> Timestamp -> Command -> Result<Event list, DomainError>
type Evolve = Investigation option -> Event -> Investigation option
type Replay = Event list -> Investigation option

/// Persistence.fs: a pure codec and a store port. The browser adapter uses Limen Storage effects; tests use a pure
/// in-memory store value.
type Encode = StoredInvestigation -> string
type Decode = string -> Result<StoredInvestigation, PersistenceError>
type SaveTransition = SaveState -> SaveEvent -> SaveState

// ---------- UI action contract (bound in Phase C; App.fs) ----------

/// The content editor that is open on the workspace. At most one at a time.
type Editor =
    | DetailsEditor
    | NewObservationEditor
    | ObservationEditor of ObservationId
    | NewHypothesisEditor
    | HypothesisEditor of HypothesisId
    | NewEvidenceEditor
    | EvidenceEditor of EvidenceId
    | NewRelationshipEditor
    | RelationshipEditor of RelationshipId

/// Every user action on the three screens. SetField carries the catalog field ID and the raw text.
type UiAction =
    | ShowList
    | ShowStart
    | OpenInvestigation of InvestigationId
    | CancelStart
    | SubmitStart
    | OpenEditor of Editor
    | CancelEditor
    | SubmitEditor
    | WithdrawSelected of RelationshipId
    | SaveInvestigation
    | SetField of fieldId: string * value: string

type Screen =
    | ListScreen
    | StartScreen
    | WorkspaceScreen of InvestigationId

// ---------- Engine ports (the seam between domain and UI agents) ----------

/// The UI (App.fs) calls the domain only through this record. The domain agent supplies Engine.ports; the UI agent
/// may use an isolated temporary stub (src/Exp002.Ui/Stub.fs) that never counts toward integration or acceptance.
type EnginePorts =
    { ValidateStart: ValidateStart
      Decide: Decide
      Replay: Replay
      Encode: Encode
      Decode: Decode
      SaveTransition: SaveTransition }
