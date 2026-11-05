namespace Causa.Ui.Contracts

open System
open Causa.Domain

/// UI-014-O3 and UI-001: frozen demonstrations are told apart from the user's writable copies.
type CopyKind =
    | FrozenDemonstration
    | WorkingCopy
    | CopyKindUnknown

/// The counts a card shows "where supported" (UI-001). Each may be unknown; none defaults to zero.
type SummaryCounts = { Hypotheses: Count; Evidence: Count; UnresolvedContradictions: Count }

/// One investigation card (UI-001, UI-022). Title and last activity are Data Visible; lifecycle and counts are shown
/// with their boundary, so a stored value is never presented as the kernel's interpretation (UI-023).
type InvestigationSummary =
    { Id: InvestigationId
      Title: string
      /// The domain lifecycle (UI-022 "actual domain lifecycle/status"), never an "expected" one.
      Status: Shown<string> option
      LastActivity: DateTimeOffset option
      Counts: Shown<SummaryCounts> option
      Copy: CopyKind
      /// `#/investigations/{id}` (INV-040 activate).
      Href: string }

type ListSort =
    | ByUpdated
    | ByTitle

/// investigation-list: before Connect the connection region; after Connect the Investigations region (INV-039).
type InvestigationListView =
    { Connection: ConnectionView
      /// NotRequested before Connect; Ready [] is the empty catalog (UI-010 "no investigations"), distinct from Failed.
      Investigations: Remote<Visible<InvestigationSummary list>>
      Sort: ListSort
      /// Opens investigation-start; offered only on a writable connection (J1-02 S8 is open: OWNER-DECISIONS CF-04).
      NewInvestigation: Availability
      State: ScreenState }

[<RequireQualifiedAccess>]
module InvestigationList =
    let sort (by: ListSort) (items: InvestigationSummary list) =
        match by with
        | ByTitle -> items |> List.sortBy (fun s -> s.Title.ToLowerInvariant())
        | ByUpdated -> items |> List.sortByDescending (fun s -> s.LastActivity |> Option.defaultValue DateTimeOffset.MinValue)

    let screenState (investigations: Remote<Visible<InvestigationSummary list>>) =
        match investigations with
        | NotRequested -> ScreenReady
        | Waiting _ -> ScreenLoading
        | Ready v when List.isEmpty (Visible.value v) -> ScreenEmpty
        | Ready _ -> ScreenReady
        | Failed f -> ScreenFailed f
