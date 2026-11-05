namespace Causa.Ui.Contracts

open System
open Causa.Domain
open Causa.Persistence

/// One stored event as read (Data Visible): the wire record, uninterpreted. Shown, for example, when the kernel cannot
/// yet project a history; never presented as the investigation's state (UI-010 "fail closed", UI-023).
type StoredEvent =
    { Version: int64
      EventType: string
      OccurredAt: DateTimeOffset
      ActorId: string
      /// The payload JSON exactly as stored. Rendered as text only (UI-005 sanitising).
      PayloadJson: string }

/// The read-only record of an investigation as stored (Data Visible). No replay is claimed.
type StoredRecordView =
    { Ref: InvestigationRef
      Title: string
      CreatedAt: DateTimeOffset option
      UpdatedAt: DateTimeOffset option
      EventCount: int
      /// A page of stored events, oldest first.
      Events: StoredEvent list }

/// The workspace frame every section shares (INV-007, INV-008, INV-018, INV-042, UI-013, UI-014).
type WorkspaceFrame =
    { /// The identity every section dispatches with (v4.1.5 W1).
      Ref: InvestigationRef
      /// The investigation identifier as text (INV-006 read-only after Start; INV-034 keeps it).
      Identifier: string
      Title: string
      /// INV-008: the original problem statement, as entered.
      ProblemStatement: Shown<string>
      /// INV-023: optional; shown in the header.
      Question: Shown<string> option
      /// INV-007.
      CreatedAt: Shown<DateTimeOffset>
      /// INV-018: the domain state, from the domain only.
      InvestigationState: Shown<string>
      Copy: CopyKind
      Sections: NavLink<Section> list
      /// Which aggregate version every section of this frame refers to (UI-013); None for stored-only data.
      Version: AggregateVersion option }

[<RequireQualifiedAccess>]
module WorkspaceFrame =
    let currentSection (frame: WorkspaceFrame) =
        frame.Sections |> List.tryFind (fun l -> l.Current) |> Option.map (fun l -> l.Destination)
