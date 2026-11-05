namespace Causa.Ui.Contracts

open System
open Causa.Domain
open Causa.Persistence

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
    { /// Where the investigation is and its id (v4.1.5 W1); opening it dispatches with this, never with the id alone.
      Ref: InvestigationRef
      Title: string
      /// The domain lifecycle (UI-022 "actual domain lifecycle/status"), never an "expected" one.
      Status: Shown<string> option
      LastActivity: DateTimeOffset option
      Counts: Shown<SummaryCounts> option
      Copy: CopyKind
      /// The canonical workspace route (INV-040 activate); also the row key the open action carries.
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
