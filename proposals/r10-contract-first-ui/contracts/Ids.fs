namespace Causa.Ui.Contracts

/// Stable identifiers. Catalog entries keep their CAT-CAUSA-INVESTIGATION id (INV-0xx) as their field, action or region id,
/// so a UI element, its manifest entry and its Dokimos/catalog check share one name. Entries no catalog item names (the
/// connection form, journey-named actions) get a UIF-/UIA-/UIR- id and cite the source that requires them
/// (SCREEN-MANIFEST.json). The id is also the Limen binding name (data-text, data-event) of the element.
type ScreenId = ScreenId of string
type FieldId = FieldId of string
type ActionId = ActionId of string
type RegionId = RegionId of string

[<RequireQualifiedAccess>]
module ScreenId =
    let value (ScreenId s) = s

[<RequireQualifiedAccess>]
module FieldId =
    let value (FieldId s) = s

[<RequireQualifiedAccess>]
module ActionId =
    let value (ActionId s) = s

[<RequireQualifiedAccess>]
module RegionId =
    let value (RegionId s) = s

/// Screens: the catalog's three (investigation-list, investigation-start, investigation-workspace) plus the two
/// primary destinations UI-018/UI-019/UI-020 require and the URL contract's not-found route.
[<RequireQualifiedAccess>]
module Screens =
    let list = ScreenId "investigation-list"
    let start = ScreenId "investigation-start"
    let workspace = ScreenId "investigation-workspace"
    let administration = ScreenId "administration"
    let settings = ScreenId "settings"
    let notFound = ScreenId "not-found"
    let all = [ list; start; workspace; administration; settings; notFound ]

[<RequireQualifiedAccess>]
module Fields =
    // investigation-list, connection region (EXT-DATA-002/003/004; labels: ODR-R10-07 patterns)
    let repositoryOwner = FieldId "UIF-CONN-OWNER"
    let repositoryName = FieldId "UIF-CONN-REPOSITORY"
    let branch = FieldId "UIF-CONN-BRANCH"
    let token = FieldId "UIF-CONN-TOKEN"
    // investigation-start (S01)
    let problemStatement = FieldId "INV-001"
    let title = FieldId "INV-002"
    let identifier = FieldId "INV-006"
    let question = FieldId "INV-023"
    // investigation-workspace content editors
    let hypothesisExplanation = FieldId "INV-011"
    let evidenceContent = FieldId "INV-012"
    let observationText = FieldId "INV-024"
    let observationSource = FieldId "INV-025"
    let hypothesisStatement = FieldId "INV-026"
    let evidenceLabel = FieldId "INV-027"
    let evidenceSource = FieldId "INV-028"
    let evidenceUrl = FieldId "INV-029"
    let relationshipKind = FieldId "INV-031"
    let conclusionStatement = FieldId "INV-033"
    let relationshipRationale = FieldId "INV-038"
    /// The link editor's evidence and hypothesis choosers (J2-04 picks them by combobox name; INV-030 names the action).
    let linkEvidence = FieldId "UIF-LINK-EVIDENCE"
    let linkHypothesis = FieldId "UIF-LINK-HYPOTHESIS"
    // investigation-workspace details editor (INV-034 edits INV-001, INV-002, INV-023; the identifier is unchanged)
    let detailsTitle = FieldId "UIF-DETAILS-TITLE"
    let detailsProblem = FieldId "UIF-DETAILS-PROBLEM"
    let detailsQuestion = FieldId "UIF-DETAILS-QUESTION"

[<RequireQualifiedAccess>]
module Actions =
    let connect = ActionId "UIA-CONNECT"
    let disconnect = ActionId "UIA-DISCONNECT"
    let newInvestigation = ActionId "UIA-NEW-INVESTIGATION"
    let openInvestigation = ActionId "INV-040"
    let startInvestigation = ActionId "INV-004"
    let cancelStart = ActionId "INV-022"
    let recordHypothesis = ActionId "INV-009"
    let recordObservation = ActionId "INV-014"
    let save = ActionId "INV-015"
    let linkEvidence = ActionId "INV-030"
    let recordConclusion = ActionId "INV-032"
    let editDetails = ActionId "INV-034"
    let cancelEditor = ActionId "INV-036"
    /// J2-03 activates /record evidence|add evidence/; no catalog entry names it (OWNER-DECISIONS CF-02).
    let recordEvidence = ActionId "UIA-RECORD-EVIDENCE"
    /// J2-06 activates "Reassess"; INV-030's note requires reassessment; no catalog entry names the action (CF-02).
    let reassess = ActionId "UIA-REASSESS"
    let saveDetails = ActionId "UIA-SAVE-DETAILS"
    let retryLoad = ActionId "UIA-RETRY-LOAD"
    /// The seven-state indicator's own actions (frozen IF41-VIEW SaveAction).
    let saveIndicator = ActionId "UIA-SAVE-INDICATOR"

[<RequireQualifiedAccess>]
module Regions =
    let investigations = RegionId "INV-039"
    let primaryNavigation = RegionId "INV-041"
    let sections = RegionId "INV-042"
    let creationTimestamp = RegionId "INV-007"
    let originalProblem = RegionId "INV-008"
    let hypotheses = RegionId "INV-010"
    let evidence = RegionId "INV-013"
    let saveState = RegionId "INV-016"
    let investigationState = RegionId "INV-018"
    let saveOutcomes = RegionId "INV-043"
    let profileIndicator = RegionId "INV-003"
    let destinationIndicator = RegionId "INV-005"
    let connection = RegionId "UIR-CONNECTION"
    let header = RegionId "UIR-WORKSPACE-HEADER"
    let timeline = RegionId "UIR-TIMELINE"
    let causalAnalysis = RegionId "UIR-CAUSAL-ANALYSIS"
    let conclusions = RegionId "UIR-CONCLUSIONS"
    let observations = RegionId "UIR-OBSERVATIONS"
    let overview = RegionId "UIR-OVERVIEW"
    let nextSteps = RegionId "UIR-NEXT-STEPS"
    let conflictReview = RegionId "UIR-CONFLICT-REVIEW"
    let storedRecord = RegionId "UIR-STORED-RECORD"
    let diagnostics = RegionId "UIR-DIAGNOSTICS"
    let preferences = RegionId "UIR-PREFERENCES"
