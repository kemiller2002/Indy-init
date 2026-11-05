namespace Causa.Ui.Contracts

open System
open Causa.Domain

/// A record the UI can link to (UI-033): its kind and its stable id as text. Not a domain type.
type EntityKind =
    | ObservationEntity
    | HypothesisEntity
    | EvidenceEntity
    | RelationshipEntity
    | ConclusionEntity
    | ContradictionEntity
    | CorrectiveActionEntity
    | OtherEntity of string

type EntityRef = { Kind: EntityKind; Id: string }

/// INV-031's options (OD-INDY-23). "Not assessed" is the absence of a relationship, never an option.
type RelationshipKind =
    | Supports
    | Contradicts
    | ConsistentWith
    | Inconclusive

/// A kind as the projection supplies it: one of INV-031's options, or a stored kind no option names (causa-data's
/// `discriminates`, `insufficient`: DATA-REPOSITORY-CONTRACT C5), shown verbatim and never mapped by the UI.
type KindShown =
    | CatalogKind of RelationshipKind
    | UninterpretedKind of string

type ObservationCard = { Id: string; Text: string; SourceReference: string option; ObservedAt: DateTimeOffset option }

/// One evidence-hypothesis assessment, shown on both the evidence and the hypothesis (INV-044).
type LinkCard =
    { Id: string
      Evidence: EntityRef
      EvidenceLabel: string
      Hypothesis: EntityRef
      HypothesisStatement: string
      Kind: KindShown
      Rationale: string
      /// At most one current assessment per pair; earlier ones stay in the history (INV-030, INV-035).
      Current: bool }

type HypothesisCard =
    { Id: string
      Statement: string
      /// The hypothesis's lifecycle word as the domain states it.
      State: string
      Explanation: string option
      Links: LinkCard list }

type EvidenceCard =
    { Id: string
      /// INV-027. Option because causa-data v1.0.0 has no label field (C5); None renders as "No label recorded".
      Label: string option
      Content: string
      SourceReference: string option
      /// Only a SafeUrl is rendered as a link (INV-029, UI-005).
      SourceUrl: SafeUrl option
      Category: string option
      CapturedAt: DateTimeOffset option
      Links: LinkCard list }

type TimelineItem =
    { Version: int64
      EventType: string
      Summary: string
      OccurredAt: DateTimeOffset
      Actor: string option
      Affects: EntityRef list }

type ConclusionCard = { Id: string; Statement: string; ResidualUncertainty: string option; RecordedAtVersion: int64 option }

/// UI-032: a domain-derived next step linked to its record; never invented.
type NextStep = { Text: string; Target: EntityRef; Action: ActionId option }

/// The projection the analysis sections render (Data Understood). One value, one version, for every section (UI-013).
type AnalysisProjection =
    { Observations: ObservationCard list
      Hypotheses: HypothesisCard list
      Evidence: EvidenceCard list
      Relationships: LinkCard list
      Timeline: TimelineItem list
      Conclusions: ConclusionCard list
      NextSteps: NextStep list
      UnresolvedContradictions: Count
      CorrectiveActionsSupported: bool
      /// Which content actions the engine authorizes now (UI-004). Keys are ActionIds.
      Actions: Map<ActionId, Availability> }

// ---- Content editors (Data Editable) -----------------------------------------------------------------------------------

/// The text of the open content editors and their validation (one map: the editor never holds a second store).
type EditorDrafts = { Values: Map<FieldId, string>; Errors: ValidationError list; OpenEditor: ActionId option }

// ---- investigation-workspace -------------------------------------------------------------------------------------------

/// The content of the current section. Analysis sections need Data Understood; without it they say so (CapabilityAbsent),
/// never "not supported" as a substitute for content (J2-08) and never a partial replay.
type SectionView =
    | OverviewSection of Remote<Understood<AnalysisProjection>>
    | CausalAnalysisView of Remote<Understood<AnalysisProjection>>
    | EvidenceView of Remote<Understood<AnalysisProjection>>
    | HypothesesView of Remote<Understood<AnalysisProjection>>
    | TimelineView of Remote<Understood<AnalysisProjection>>
    | CorrectiveActionsView of Remote<Understood<AnalysisProjection>>

type WorkspaceView =
    { Frame: Remote<WorkspaceFrame>
      /// The stored record (Data Visible), available even where no projection is.
      Stored: Remote<Visible<StoredRecordView>>
      Section: SectionView
      Drafts: EditorDrafts
      Details: DetailsEditorView
      Save: SavePresentation option
      State: ScreenState }
