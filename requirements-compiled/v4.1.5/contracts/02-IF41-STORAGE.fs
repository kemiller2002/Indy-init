namespace Causa.Persistence

open Causa.Domain

/// IF41-STORAGE: causa-storage v1.0.0 file sets (CONTRACT.md wins over docs/CAUSA-DATA-CONTRACT-v1.md).
type CommitOid = CommitOid of string
type RepoPath = RepoPath of string

/// v4.1.5 W1: a causa-storage workspace, the repository-relative directory that holds `investigations/<id>/`
/// (for example `workspaces/demo/r10`). It is derived, never typed: a catalog workspace is the one
/// `catalog/active.json` names (EXT-DATA-013); a working copy is the connected repository's run workspace under
/// `workspaces/demo/` (CONTRACT section 9.4). The user selects among the derived workspaces (EXT-DATA-003).
type Workspace = Workspace of string

type FileSet =
    { Manifest: byte[]
      Events: byte[]
      Snapshot: byte[] }

type VerificationFailure =
    | SchemaInvalid of file: string * detail: string
    | DigestMismatch of field: string
    | ContractValueMismatch of field: string * expected: string * actual: string
    | SnapshotDiffersFromReplay of paths: string list
    | MissingRequiredField of file: string * field: string
    | ReplayRejected of ReplayError
    /// v4.1.5 E1: a CONTRACT section 11.1 reader stage refused the file set; code is the stage's primary code, so
    /// conforming readers agree on it.
    | ReaderStage of code: string * file: string * line: int option * detail: string

type VerifiedInvestigation =
    { Investigation: Investigation
      History: EventEnvelope list
      ReadAt: CommitOid
      /// v4.1.5 W1: the workspace the history was read from.
      Workspace: Workspace
      Bytes: FileSet }

type StorageCodec =
    { /// v4.1.5 W1: `<workspace>/investigations/<id>`.
      PathOf: Workspace -> InvestigationId -> RepoPath
      Encode: VerifiedInvestigation -> EventEnvelope list -> FileSet
      /// v4.1.5 C1: the first file set of a new investigation from its command-created history (EXT-DATA-008), which
      /// Verify must accept before it is written.
      EncodeNew: EventEnvelope list -> FileSet
      Verify: Workspace -> CommitOid -> FileSet -> Result<VerifiedInvestigation, VerificationFailure list> }
