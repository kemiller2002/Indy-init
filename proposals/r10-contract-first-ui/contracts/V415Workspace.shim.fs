// STAND-IN, proposal build only. Copied verbatim from requirements-compiled/v4.1.5/contracts/02-IF41-STORAGE.fs (PR #135,
// revision 3, 730e2d6) because main still holds only v4.1.4. NOT part of the folded IF41-VIEW: in v4.1.5 these types
// come from 02-IF41-STORAGE.fs itself. Delete this file when the contracts project links v4.1.5.
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

/// v4.1.5 W1: a workspace of the connected repository, with its role. The role is derived, never asserted: the
/// representation is private, a catalog address comes only from WorkspaceAddress.catalog and a working copy only from
/// WorkspaceAddress.workingCopy (EXT-DATA-013; EXT-DATA-006).
[<RequireQualifiedAccess>]
module WorkspaceAddress =
    type T =
        private
            { Dir: Workspace
              Kind: WorkspaceRole }
        member a.Workspace = a.Dir
        member a.Role = a.Kind

    let catalog (workspace: Workspace) : T = { Dir = workspace; Kind = WorkspaceRole.Catalog }

    /// The working copy `workspaces/demo/causa-app-<run>` (CONTRACT section 3, runId ^[a-z0-9][a-z0-9-]{0,63}$) for a
    /// run matching ^[a-z0-9][a-z0-9-]{0,53}$. The reserved `causa-app-` namespace keeps every working copy apart from
    /// the catalog workspaces (demonstration-v1, historical-v1, ...), and any workspace in `catalogs` is refused as well.
    let workingCopy (catalogs: Workspace list) (run: string) : T option =
        let workspace = Workspace("workspaces/demo/causa-app-" + run)
        if System.Text.RegularExpressions.Regex.IsMatch(run, "^[a-z0-9][a-z0-9-]{0,53}$") && not (List.contains workspace catalogs) then
            Some { Dir = workspace; Kind = WorkspaceRole.WorkingCopy }
        else
            None

/// v4.1.5 W1: an investigation is identified by where it is and its id: the same case can be stored in two
/// workspaces (causa-data holds identical cases in historical-v1 and demonstration-v1).
type InvestigationRef = { Address: WorkspaceAddress.T; Id: InvestigationId }

/// v4.1.5 W1: an address that may be written. Its only constructor refuses a Catalog, so no catalog investigation can
/// reach a save session (EXT-DATA-013; EXT-DATA-006).
[<RequireQualifiedAccess>]
module Writable =
    type T =
        private
        | Writable of WorkspaceAddress.T

    let ofAddress (address: WorkspaceAddress.T) : T option =
        match address.Role with
        | WorkspaceRole.WorkingCopy -> Some(Writable address)
        | WorkspaceRole.Catalog -> None

    let address (Writable address) : WorkspaceAddress.T = address
