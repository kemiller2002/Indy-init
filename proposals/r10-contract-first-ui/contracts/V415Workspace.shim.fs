// STAND-IN, proposal build only. Copied verbatim from requirements-compiled/v4.1.5/contracts/02-IF41-STORAGE.fs (PR #135,
// f91bd7b, lines 8-41) because main still holds only v4.1.4. NOT part of the folded IF41-VIEW: in v4.1.5 these types come
// from 02-IF41-STORAGE.fs itself. Delete this file when the contracts project links v4.1.5.
namespace Causa.Persistence

open Causa.Domain


/// v4.1.5 W1: a causa-storage workspace, the directory of the connected repository that holds `investigations/<id>/`
/// (for example `workspaces/demo/r10`). It is derived, never typed: the catalog workspace is the one
/// `catalog/active.json` names (EXT-DATA-013); a working copy is `workspaces/demo/<run>` (CONTRACT section 9.4).
type Workspace = Workspace of string

/// v4.1.5 W1: what a workspace may be used for. A catalog is read-only (EXT-DATA-013); only a working copy is written
/// (CONTRACT section 9.4; EXT-DATA-006). Both are in the repository the user connected to (EXT-DATA-003).
[<RequireQualifiedAccess>]
type WorkspaceRole =
    | Catalog
    | WorkingCopy

/// v4.1.5 W1: a workspace of the connected repository, with its role.
type WorkspaceAddress = { Workspace: Workspace; Role: WorkspaceRole }

/// v4.1.5 W1: an investigation is identified by where it is and its id: the same case can be stored in two
/// workspaces (causa-data holds identical cases in historical-v1 and demonstration-v1).
type InvestigationRef = { Address: WorkspaceAddress; Id: InvestigationId }

/// v4.1.5 W1: an address that may be written. Its only constructor refuses a Catalog, so no catalog investigation can
/// reach a save session (EXT-DATA-013; EXT-DATA-006).
[<RequireQualifiedAccess>]
module Writable =
    type T =
        private
        | Writable of WorkspaceAddress

    let ofAddress (address: WorkspaceAddress) : T option =
        match address.Role with
        | WorkspaceRole.WorkingCopy -> Some(Writable address)
        | WorkspaceRole.Catalog -> None

    let address (Writable address) : WorkspaceAddress = address
