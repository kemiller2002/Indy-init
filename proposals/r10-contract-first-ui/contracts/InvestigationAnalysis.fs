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

[<RequireQualifiedAccess>]
module ContentForms =
    let observation : FieldSpec list =
        [ { Field = Fields.observationText; Label = "Observation text"; Component = TextArea; Rules = [ Required; MinLength 1; MaxLength 10000 ] }
          { Field = Fields.observationSource; Label = "Observation source reference"; Component = TextInput; Rules = [ MaxLength 10000 ] } ]

    let hypothesis : FieldSpec list =
        [ { Field = Fields.hypothesisStatement; Label = "Hypothesis statement"; Component = TextArea; Rules = [ Required; MinLength 1; MaxLength 10000 ] }
          // INV-011 has no validation block in the catalog; required: true.
          { Field = Fields.hypothesisExplanation; Label = "Hypothesis explanation"; Component = TextArea; Rules = [ Required ] } ]

    let evidence : FieldSpec list =
        [ { Field = Fields.evidenceLabel; Label = "Evidence label"; Component = TextInput; Rules = [ Required; MinLength 1; MaxLength 200 ] }
          { Field = Fields.evidenceSource; Label = "Evidence source reference"; Component = TextInput; Rules = [ MaxLength 10000 ] }
          { Field = Fields.evidenceUrl; Label = "Evidence source URL"; Component = UrlInput; Rules = [ MaxLength 2000; Pattern SafeUrl.pattern ] }
          // INV-012 has no validation block; required: true.
          { Field = Fields.evidenceContent; Label = "Evidence original content"; Component = TextArea; Rules = [ Required ] } ]

    let link : FieldSpec list =
        [ { Field = Fields.linkEvidence; Label = "Evidence"; Component = Select; Rules = [ Required ] }
          { Field = Fields.linkHypothesis; Label = "Hypothesis"; Component = Select; Rules = [ Required ] }
          { Field = Fields.relationshipKind; Label = "Evidence relationship kind"; Component = RadioGroup
            Rules = [ Required; OneOf [ "supports"; "contradicts"; "consistent-with"; "inconclusive" ] ] }
          { Field = Fields.relationshipRationale; Label = "Evidence relationship rationale"; Component = TextArea; Rules = [ Required; MinLength 1; MaxLength 10000 ] } ]

    let conclusion : FieldSpec list =
        [ { Field = Fields.conclusionStatement; Label = "Conclusion statement"; Component = TextArea; Rules = [ Required; MinLength 1; MaxLength 10000 ] } ]

    let all = observation @ hypothesis @ evidence @ link @ conclusion

    let kindValue k =
        match k with
        | Supports -> "supports"
        | Contradicts -> "contradicts"
        | ConsistentWith -> "consistent-with"
        | Inconclusive -> "inconclusive"

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
