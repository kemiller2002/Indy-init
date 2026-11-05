module Causa.Preview.Model

open Causa.HistoricalCompatibility
open Causa.Preview.GitHub

type Screen =
    | ConnectScreen
    | ListScreen
    | DetailScreen
    | CreateScreen

/// Where an investigation lives: the read-only historical catalog, or the writable working copy (UI-014-O3).
type Source =
    | Catalog
    | WorkingCopy

type Entry =
    { Source: Source
      DirName: string
      Title: string
      Detail: string
      /// The catalog manifest's expectedStateSha256: an independent expectation, never computed here.
      ExpectedState: string option }

/// One opened investigation, exactly as read from the remote at `ReadAt`.
type Opened =
    { Source: Source
      DirName: string
      ReadAt: string
      Files: InvestigationFiles
      Result: LoadResult
      /// Commands recorded since the last confirmed save, oldest first (never written until Save).
      Unsaved: Writer.Command list }

type Form =
    { CatalogRepo: string
      CatalogBranch: string
      WriteRepo: string
      WriteBranch: string
      Workspace: string
      Token: string
      NewTitle: string
      NewObservation: string
      EditObservation: string
      EditHypothesis: string }

/// Why a request was sent, so its answer can be interpreted.
type Purpose =
    | CatalogHead
    | ActiveCatalog of head: string
    | CatalogManifest of head: string * workspacePath: string
    | WriteHead
    | WriteListing of head: string
    | ReadFile of source: Source * dir: string * commit: string * file: string
    | Commit of dir: string * files: InvestigationFiles * reopen: bool
    | ReadBack of dir: string * commit: string * file: string * expected: InvestigationFiles

type Model =
    { Screen: Screen
      Form: Form
      Catalog: RepoRef option
      Write: RepoRef option
      CatalogHead: string option
      CatalogWorkspace: string option
      WriteHead: string option
      Entries: Entry list
      Opened: Opened option
      /// Partial file reads of an open or read-back in progress: (dir, commit) -> files read so far.
      Reads: Map<string * string, (string * byte[]) list>
      Pending: Map<string, Purpose>
      NextId: int
      Now: Timestamp option
      Busy: bool
      Status: string
      Problem: string option
      Confirmed: string option
      /// Field-level validation errors of the last refused submit (INV-019..021, INV-024, INV-026); cleared per field on edit.
      FieldErrors: Causa.Preview.Validation.FieldError list
      /// A route the browser asked for before the data it names was available (a deep link before Connect).
      PendingRoute: string option }

let initial =
    { Screen = ConnectScreen
      Form =
        { CatalogRepo = "kemiller2002/causa-data"; CatalogBranch = "main"; WriteRepo = ""; WriteBranch = "main"; Workspace = "preview"; Token = ""
          NewTitle = ""; NewObservation = ""; EditObservation = ""; EditHypothesis = "" }
      Catalog = None
      Write = None
      CatalogHead = None
      CatalogWorkspace = None
      WriteHead = None
      Entries = []
      Opened = None
      Reads = Map.empty
      Pending = Map.empty
      NextId = 1
      Now = None
      Busy = false
      Status = "Not connected."
      Problem = None
      Confirmed = None
      FieldErrors = []
      PendingRoute = None }
