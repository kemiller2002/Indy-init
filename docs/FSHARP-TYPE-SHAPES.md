# F# Implementation Type Shapes

Status: normative first implementation profile, intentionally not final wire schema.

> **Normative status.** **Normative** typed-domain profile; **not** a wire schema. Persisted data is governed only by the frozen Causa storage contract v1.0.0 ([README.md](../requirements/causa/data/v1.0.0/README.md), [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md), [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md)). Where this document states a persisted field, event name, enum value, layout, hash or concurrency rule differently, the contract governs; such statements are marked *Superseded by frozen contract v1.0.0* in place. The engine↔wire mapping is [FIELD-MAPPING.md](../requirements/causa/data/v1.0.0/FIELD-MAPPING.md). Any domain value below that has no v1.0.0 wire form MUST NOT be written to a v1.0.0 workspace (`CAUSA.UNREPRESENTABLE`); such values are marked deferred.

These types show how to begin implementation while preserving the semantic evolution boundaries. They are guidance for the domain project, not permission to prematurely close research-sensitive taxonomies.

## 1. Design principles

Use F# types for:

- stable identity
- lifecycle state
- command intent
- event meaning
- domain decisions
- invariant-safe construction

Use versioned semantic identifiers/envelopes for:

- EDF-specific concepts
- research constructs
- extensible classifications
- future domain semantics

Avoid:

- giant stringly typed dictionaries
- one universal JSON record
- generic SetState commands
- GitHub types in the domain
- AI-provider types in the domain
- EDF-specific types in the generic core

## 2. Primitive wrappers

```fsharp
namespace Investigator.Domain

open System

[<Struct>]
type InvestigationId = private InvestigationId of Guid

[<Struct>]
type CommandId = private CommandId of Guid

[<Struct>]
type EventId = private EventId of Guid

[<Struct>]
type ObservationId = private ObservationId of Guid

[<Struct>]
type ClaimId = private ClaimId of Guid

[<Struct>]
type InterpretationId = private InterpretationId of Guid

[<Struct>]
type HypothesisId = private HypothesisId of Guid

[<Struct>]
type AssumptionId = private AssumptionId of Guid

[<Struct>]
type EvidenceId = private EvidenceId of Guid

[<Struct>]
type PredictionId = private PredictionId of Guid

[<Struct>]
type InvestigationActionId = private InvestigationActionId of Guid

[<Struct>]
type FindingId = private FindingId of Guid

[<Struct>]
type UnknownId = private UnknownId of Guid

[<Struct>]
type NegativeKnowledgeId = private NegativeKnowledgeId of Guid

[<Struct>]
type ContradictionId = private ContradictionId of Guid

[<Struct>]
type ObligationId = private ObligationId of Guid

[<Struct>]
type CausalAssertionId = private CausalAssertionId of Guid

[<Struct>]
type CorrectiveActionId = private CorrectiveActionId of Guid

[<Struct>]
type VerificationCriterionId = private VerificationCriterionId of Guid

[<Struct>]
type VerificationId = private VerificationId of Guid

[<Struct>]
type ConclusionId = private ConclusionId of Guid

[<Struct>]
type ProposalId = private ProposalId of Guid

[<Struct>]
type AggregateVersion = private AggregateVersion of int64
```

Constructors should live in modules with validation/creation functions rather than exposing raw DU constructors where inappropriate.

> **Superseded by frozen contract v1.0.0:** wire field names differ from these type names (e.g. `InvestigationActionId` → `actionId`, `VerificationCriterionId` → `criterionId`, `CausalAssertionId` → `causeId`); ids are lowercase canonical non-nil UUIDs; `EventId` is never minted at random: `eventId` is derived from `investigationId/aggregateVersion/correlationId` (CONTRACT §5.2). See [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md).

## 3. Non-empty semantic text

```fsharp
type NonEmptyText = private NonEmptyText of string

module NonEmptyText =
    val create : string -> Result<NonEmptyText, string>
    val value : NonEmptyText -> string
```

Use domain-specific wrappers later if validation semantics differ materially.

## 4. Semantic identity

```fsharp
type SemanticId = {
    Namespace : NonEmptyText
    Name : NonEmptyText
    Version : NonEmptyText
}

type StabilityClass =
    | CoreStable
    | DomainStable
    | Experimental
    | Deprecated
    | Retired

type Requiredness =
    | Required
    | Optional

type SemanticDependency = {
    Id : SemanticId
    Requiredness : Requiredness
}
```

## 5. Actor and provenance

Keep authentication claims outside the pure core where possible.

```fsharp
type ActorId = private ActorId of string

type AgentIdentity = {
    Provider : string
    Model : string
    Runtime : string option
    PromptVersion : string option
}

type ActorKind =
    | Human
    | Agent of AgentIdentity
    | System
    | ExternalSystem of string
    | ImportedHistorical

type Actor = {
    Id : ActorId
    Kind : ActorKind
    DisplayName : string option
}

type Provenance = {
    Actor : Actor
    RecordedAt : DateTimeOffset
    SourceRef : string option
    ImportRef : string option
    TransformationRefs : string list
}
```

Do not put OAuth tokens/permissions into Actor.

> **Superseded by frozen contract v1.0.0:** persisted actor kinds are only `human`, `system` and `agent{provider, model}`; `ExternalSystem`, `ImportedHistorical`, `DisplayName`, `Runtime` and `PromptVersion` have no wire form (deferred). Persisted provenance is `{actor, recordedAt}` only, derived from the creating event; `SourceRef`, `ImportRef` and `TransformationRefs` are deferred (`actor.schema.json`).

## 6. Scope

```fsharp
type TimeScope = {
    Start : DateTimeOffset option
    EndExclusive : DateTimeOffset option
}

type ExtensionEnvelope = {
    SemanticId : SemanticId
    SchemaId : string
    Stability : StabilityClass
    Requiredness : Requiredness
    PayloadJson : string
    Provenance : Provenance
}

type Scope = {
    Time : TimeScope option
    Systems : string list
    Versions : string list
    Environments : string list
    Subjects : string list
    ExtensionScopes : ExtensionEnvelope list
}
```

PayloadJson is a boundary representation only. A loaded domain pack should decode it to a typed domain-specific value before using its semantics.

> **Superseded by frozen contract v1.0.0:** v1.0.0 has no extension namespace and no scope fields: `Scope` and `ExtensionEnvelope` values must be empty for v1.0.0 persistence (CONTRACT §2.4, §13.5).

## 7. Entity references

```fsharp
type CoreEntityKind =
    | ObservationEntity
    | ClaimEntity
    | InterpretationEntity
    | HypothesisEntity
    | AssumptionEntity
    | EvidenceEntity
    | PredictionEntity
    | InvestigationActionEntity
    | FindingEntity
    | UnknownEntity
    | NegativeKnowledgeEntity
    | ContradictionEntity
    | ObligationEntity
    | CausalAssertionEntity
    | CorrectiveActionEntity
    | VerificationCriterionEntity
    | VerificationEntity
    | ConclusionEntity

type EntityKind =
    | CoreEntity of CoreEntityKind
    | ExtensionEntity of SemanticId

type EntityRef = {
    Kind : EntityKind
    Id : string
}
```

Typed IDs are preferred inside entity-specific code; EntityRef is for heterogeneous graphs.

> **Superseded by frozen contract v1.0.0:** v1.0.0 persists typed id fields only; `EntityRef` has no wire form (deferred).

## 8. Investigation lifecycle

```fsharp
type InvestigationLifecycle =
    | Draft
    | Active
    | AwaitingEvidence
    | AwaitingVerification
    | Resolved
    | Closed
    | Archived
```

Do not add Reopened as a durable lifecycle case initially. Reopening transitions back to Active and remains visible through events.

## 9. Hypothesis lifecycle

```fsharp
type HypothesisState =
    | Proposed
    | Plausible
    | Supported
    | Weakened
    | Falsified
    | Superseded
```

Do not include Leading initially. Compute:

```fsharp
type HypothesisComparisonPosture =
    | NotCompared
    | CandidateLeader
    | Leading
    | Tied
```

as a projection that can evolve.

## 10. Assumption lifecycle

```fsharp
type AssumptionState =
    | ExplicitUnverified
    | ProvisionallyAccepted
    | Verified
    | Rejected
    | Expired
```

## 11. Unknown lifecycle

```fsharp
type UnknownState =
    | Identified
    | Investigating
    | Resolved
    | AcceptedResidual
    | Superseded
```

Materiality should remain separate:

```fsharp
type Materiality =
    | Blocking
    | Material
    | NonMaterial
    | Informational
```

If research later shows materiality belongs entirely in policy, the lifecycle event history remains usable.

## 12. Contradiction lifecycle

```fsharp
type ContradictionState =
    | Detected
    | Triaged
    | Investigating
    | Resolved
    | AcceptedResidual
    | Superseded
```

## 13. Investigation action lifecycle

```fsharp
type InvestigationActionState =
    | Proposed
    | Approved
    | InProgress
    | Blocked   // Superseded by frozen contract v1.0.0: no wire value (deferred)
    | Completed
    | Interpreted
    | Rejected
    | Cancelled
```

## 14. Corrective action lifecycle

```fsharp
type CorrectiveActionState =
    | Proposed
    | Approved
    | InProgress
    | Implemented
    | AwaitingVerification   // Superseded by frozen contract v1.0.0: the case is AwaitingVerificationOfEffect, wire `awaitingVerificationOfEffect`
    | VerifiedEffective
    | VerificationFailed
    | ReworkRequired
    | Cancelled

type CoreCorrectiveActionKind =
    | Mitigation
    | Workaround
    | Correction
    | Prevention

type CorrectiveActionKind =
    | CoreCorrectiveAction of CoreCorrectiveActionKind
    | ExtensionCorrectiveAction of SemanticId
```

> **Superseded by frozen contract v1.0.0:** `correctiveActionKind` is closed (`mitigation|workaround|correction|prevention`); extension kinds have no wire form.

## 15. Evidence relationship kind

Keep the currently plausible core set isolated so it can still be reduced before final stabilization.

```fsharp
type CoreEvidenceRelationshipKind =
    | Supports
    | Contradicts
    | Discriminates
    | Qualifies
    | Corroborates
    | FailsToSupport
    | Irrelevant
    | Supersedes
    | DependsOn
    | DerivedFrom

type EvidenceRelationshipKind =
    | CoreRelationship of CoreEvidenceRelationshipKind
    | ExtensionRelationship of SemanticId
```

OQ-012 remains open. Do not treat the current core relationship set as empirically final merely because it is typed.

> **Superseded by frozen contract v1.0.0:** the v1.0.0 wire set is exactly `supports|contradicts|discriminates|insufficient`. `Insufficient` is required and missing above; `Qualifies`, `Corroborates`, `FailsToSupport`, `Irrelevant`, `Supersedes`, `DependsOn`, `DerivedFrom` and extension kinds are deferred (OQ-012 stays open for later versions).

## 16. Core action kind

```fsharp
type CoreInvestigationActionKind =
    | Question
    | Inspection
    | Query
    | Measurement
    | Comparison
    | Experiment
    | Simulation
    | Reproduction
    | ExternalVerification
    | CodeReview
    | Interview

type InvestigationActionKind =
    | CoreAction of CoreInvestigationActionKind
    | ExtensionAction of SemanticId
```

> **Superseded by frozen contract v1.0.0:** the v1.0.0 wire set is exactly `test|inspection|query|comparison|experiment`. `Test` is missing above; `Question`, `Measurement`, `Simulation`, `Reproduction`, `ExternalVerification`, `CodeReview`, `Interview` and extension kinds are deferred.

## 17. Evidence category

```fsharp
type CoreEvidenceCategory =
    | DirectObservation
    | Measurement
    | Log
    | Document
    | ExperimentResult
    | Testimony
    | ExternalReference
    | DerivedCalculation

type EvidenceCategory =
    | CoreEvidence of CoreEvidenceCategory
    | ExtensionEvidence of SemanticId
```

AI interpretation is intentionally not a direct evidence category. Agent-generated interpretation remains Interpretation unless admitted evidence from an external source exists.

> **Superseded by frozen contract v1.0.0:** the v1.0.0 `evidenceCategory` set has 7 values; `DerivedCalculation` and extension categories are deferred.

## 18. Verification state

```fsharp
type VerificationStatus =
    | Unverified
    | PartiallyVerified
    | Verified
    | Invalidated
```

Avoid a universal numeric quality score.

## 19. Core entity records

These are indicative shapes, not final serialization. v1.0.0 persisted shapes: [schemas/entities/](../requirements/causa/data/v1.0.0/schemas/entities/) and [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md).

```fsharp
type Observation = {
    Id : ObservationId
    Original : NonEmptyText
    Normalized : NonEmptyText option
    Scope : Scope
    ObservedAt : DateTimeOffset option
    Provenance : Provenance
    Verification : VerificationStatus
    Extensions : ExtensionEnvelope list
}

type Claim = {
    Id : ClaimId
    Proposition : NonEmptyText
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}

type Interpretation = {
    Id : InterpretationId
    Statement : NonEmptyText
    Sources : EntityRef list
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}

type Hypothesis = {
    Id : HypothesisId
    Statement : NonEmptyText
    Explains : EntityRef list
    State : HypothesisState
    Mechanism : NonEmptyText option
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}

type Assumption = {
    Id : AssumptionId
    Proposition : NonEmptyText
    State : AssumptionState
    Materiality : Materiality
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}
```

Relationships/dependencies should generally live as separate records/events rather than duplicated lists that can drift.

> **Superseded by frozen contract v1.0.0:** persisted `observation` = `{id, original, observedAt, provenance}` (no Normalized, Scope, Verification, Extensions); persisted `hypothesis` = `{id, statement, mechanism, state, history[], provenance}` (no Explains/Scope/Extensions; `history` is persisted); persisted `assumption` = `{id, proposition, materiality, state, provenance}`. Claim and Interpretation have no v1.0.0 entity (deferred).

## 20. Evidence record

```fsharp
type ArtifactRef = {
    ArtifactId : string
    Uri : string
    MediaType : string option
    Digest : string option
}

type EvidenceContent =
    | TextEvidence of NonEmptyText
    | ArtifactEvidence of ArtifactRef

type Evidence = {
    Id : EvidenceId
    Content : EvidenceContent
    Category : EvidenceCategory
    Scope : Scope
    Verification : VerificationStatus
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}
```

> **Superseded by frozen contract v1.0.0:** persisted `evidence` = `{id, contentKind, content, category, source, capturedAt, verification, supersededBy, admittedAtVersion, provenance}`; an artifact is only its absolute http(s) URL in `content` (`ArtifactId`, `MediaType`, `Digest` deferred).

## 21. Evidence relationship record

```fsharp
type EvidenceRelationshipId = private EvidenceRelationshipId of Guid

type EvidenceRelationship = {
    Id : EvidenceRelationshipId
    Evidence : EvidenceId
    Target : EntityRef
    Kind : EvidenceRelationshipKind
    Rationale : NonEmptyText option
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}
```

## 22. Prediction

> **Superseded by frozen contract v1.0.0:** persisted `relationship` targets a hypothesis: `{id, evidenceId, hypothesisId, kind, rationale, provenance}`.

```fsharp
type PredictionStrength =
    | Necessary
    | Expected
    | Discriminating
    | NonDiscriminating

type PredictionTiming =
    | Prospective
    | Retrospective

type Prediction = {
    Id : PredictionId
    Hypotheses : HypothesisId list
    Statement : NonEmptyText
    Strength : PredictionStrength
    Timing : PredictionTiming
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}
```

> **Superseded by frozen contract v1.0.0:** a persisted prediction has a single `hypothesisId` (not a list).

## 23. Unknown and contradiction

```fsharp
type Unknown = {
    Id : UnknownId
    Question : NonEmptyText
    State : UnknownState
    Materiality : Materiality
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}

type Contradiction = {
    Id : ContradictionId
    Statement : NonEmptyText
    Participants : EntityRef list
    State : ContradictionState
    Materiality : Materiality
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}
```

> **Superseded by frozen contract v1.0.0:** persisted `unknown` adds `basis` (latest transition basis); persisted `contradiction` = `{id, statement, evidenceIds, materiality, state, basis, provenance}` (evidence ids, not EntityRef participants).

## 24. Investigation action and finding

```fsharp
type InvestigationAction = {
    Id : InvestigationActionId
    Kind : InvestigationActionKind
    Purpose : NonEmptyText
    Targets : EntityRef list
    State : InvestigationActionState
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}

type Finding = {
    Id : FindingId
    Action : InvestigationActionId
    OriginalResult : NonEmptyText
    NormalizedResult : NonEmptyText option
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}
```

> **Superseded by frozen contract v1.0.0:** persisted `action` uses `targetHypothesisIds`; persisted `finding` has `interpretation` (nullable), not NormalizedResult.

## 25. Causal assertion

Avoid prematurely imposing a DAG.

```fsharp
type CoreCauseRole =
    | RootCause
    | ContributingFactor
    | Trigger
    | EnablingCondition

type CauseRole =
    | CoreCauseRole of CoreCauseRole
    | ExtensionCauseRole of SemanticId

type CausalMember = {
    Entity : EntityRef
    Role : CauseRole
}

type CausalAssertionState =
    | Proposed
    | Confirmed
    | Reopened
    | Superseded

type CausalAssertion = {
    Id : CausalAssertionId
    Outcome : EntityRef
    Members : CausalMember list
    Mechanism : NonEmptyText option
    Scope : Scope
    State : CausalAssertionState
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}
```

Causal confirmation belongs to CausalAssertion rather than HypothesisState. This supports causal sets and avoids assuming one hypothesis maps one-to-one to one confirmed cause.

> **Superseded by frozen contract v1.0.0:** persisted `cause` = `{id, outcome (text), members[{hypothesisId, role}], mechanism, state, provenance}`; members reference hypotheses only; `causeRole` is closed (no extension roles).

## 26. Corrective action and verification

```fsharp
type CorrectiveAction = {
    Id : CorrectiveActionId
    Kind : CorrectiveActionKind
    Description : NonEmptyText
    Targets : CausalAssertionId list
    State : CorrectiveActionState
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}

type VerificationCriterion = {
    Id : VerificationCriterionId
    CorrectiveAction : CorrectiveActionId
    ExpectedOutcome : NonEmptyText
    Method : NonEmptyText option
    Scope : Scope
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}

type VerificationOutcome =
    | Satisfied
    | Failed
    | Inconclusive

type Verification = {
    Id : VerificationId
    Criterion : VerificationCriterionId
    Outcome : VerificationOutcome
    Evidence : EvidenceId list
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}
```

## 27. Conclusion

> **Superseded by frozen contract v1.0.0:** persisted `correctiveAction` adds `awaitingSinceVersion`; persisted `verification` adds `recordedAtVersion`; no Scope/Extensions.

```fsharp
type ConclusionStrength =
    | PlausibleExplanation
    | LeadingExplanation
    | SupportedCause
    | ConfirmedCause
    | AcceptedWithResidualUncertainty

type Conclusion = {
    Id : ConclusionId
    Statement : NonEmptyText
    Strength : ConclusionStrength
    Causes : CausalAssertionId list
    Evidence : EvidenceId list
    ResidualUnknowns : UnknownId list
    UnresolvedContradictions : ContradictionId list
    AcceptedAssumptions : AssumptionId list
    PolicyRefs : string list
    Provenance : Provenance
    Extensions : ExtensionEnvelope list
}
```

ConclusionStrength is a report/current-posture representation. It does not bypass hypothesis/cause transitions.

> **Superseded by frozen contract v1.0.0:** persisted `conclusion` = `{id, statement, strength, causeIds, residualUncertainty (text or null), recordedAtVersion, provenance}`; the evidence/unknown/contradiction/assumption reference lists and PolicyRefs are deferred.

## 28. Proposal

```fsharp
type ProposalState =
    | Active
    | Revised    // Superseded by frozen contract v1.0.0: no wire value (deferred)
    | Accepted
    | Rejected
    | Deferred   // no wire value (deferred)
    | Stale      // no wire value (deferred)

type ProposalKind =
    | ProposedObservation
    | ProposedInterpretation
    | ProposedHypothesis
    | ProposedEvidenceRelationship
    | ProposedUnknown
    | ProposedAssumption
    | ProposedContradiction
    | ProposedPrediction
    | ProposedInvestigationAction
    | ProposedCausalAssertion
    | ProposedCorrectiveAction
    | ProposedVerificationCriterion
    | ExtensionProposal of SemanticId

type Proposal = {
    Id : ProposalId
    Kind : ProposalKind
    PayloadJson : string
    State : ProposalState
    Rationale : NonEmptyText option
    SourceRefs : EntityRef list
    SemanticManifest : InvestigationSemanticManifest
    Provenance : Provenance
}
```

A domain-specific proposal decoder validates PayloadJson before producing a target command.

> **Superseded by frozen contract v1.0.0:** persisted `proposalKind` is `hypothesis|unknown|action` (the other 9 cases and extensions are deferred); persisted `proposal` = `{id, kind, text, rationale, state, decision, provenance}` (no PayloadJson, SourceRefs or SemanticManifest).

## 29. Semantic manifest

```fsharp
type DomainPackRef = {
    Id : string
    Version : string
}

type PolicyRef = {
    Id : string
    Version : string
}

type InvestigationSemanticManifest = {
    CoreSchemaVersion : string
    CoreSemanticVersion : string
    DomainPacks : DomainPackRef list
    PolicyRefs : PolicyRef list
    SemanticDependencies : SemanticDependency list
}
```

> **Superseded by frozen contract v1.0.0:** v1.0.0 persists only `schemaVersion`/`semanticVersion` = `"1.0.0"` and `domainPacks: []` in `manifest.json`, and `semanticVersion` on each event; no policy refs or dependencies.

## 30. Investigation aggregate projection

Use maps for current entity projections.

```fsharp
type Investigation = {
    Id : InvestigationId
    Title : NonEmptyText
    Lifecycle : InvestigationLifecycle
    Version : AggregateVersion
    SemanticManifest : InvestigationSemanticManifest
    Observations : Map<ObservationId, Observation>
    Claims : Map<ClaimId, Claim>
    Interpretations : Map<InterpretationId, Interpretation>
    Hypotheses : Map<HypothesisId, Hypothesis>
    Assumptions : Map<AssumptionId, Assumption>
    Evidence : Map<EvidenceId, Evidence>
    EvidenceRelationships : Map<EvidenceRelationshipId, EvidenceRelationship>
    Predictions : Map<PredictionId, Prediction>
    Actions : Map<InvestigationActionId, InvestigationAction>
    Findings : Map<FindingId, Finding>
    Unknowns : Map<UnknownId, Unknown>
    Contradictions : Map<ContradictionId, Contradiction>
    CausalAssertions : Map<CausalAssertionId, CausalAssertion>
    CorrectiveActions : Map<CorrectiveActionId, CorrectiveAction>
    VerificationCriteria : Map<VerificationCriterionId, VerificationCriterion>
    Verifications : Map<VerificationId, Verification>
    Conclusions : Map<ConclusionId, Conclusion>
    Proposals : Map<ProposalId, Proposal>
    Extensions : ExtensionEnvelope list
}
```

NegativeKnowledge and durable Obligations should be added to the aggregate in implementation even if omitted from this abbreviated listing.

> **Superseded by frozen contract v1.0.0:** the persisted projection is `state.schema.json`: 5 scalars and 17 arrays (incl. `negativeKnowledge`; no claims, interpretations, manifest or extensions), each sorted by ordinal UUID text, not `Map`/Guid order.

## 31. Commands

Prefer one command DU for core commands plus an extension command envelope.

```fsharp
type CoreCommand =
    | StartInvestigation of StartInvestigation
    | ActivateInvestigation
    | RecordObservation of RecordObservation
    | RecordHypothesis of RecordHypothesis
    | MarkHypothesisPlausible of HypothesisId
    | MarkHypothesisSupported of HypothesisId
    | FalsifyHypothesis of FalsifyHypothesis
    | ReopenHypothesis of ReopenHypothesis
    | AdmitEvidence of AdmitEvidence
    | RelateEvidence of RelateEvidence
    | IdentifyUnknown of IdentifyUnknown
    | RecordAssumption of RecordAssumption
    | RecordContradiction of RecordContradiction
    | RecordPrediction of RecordPrediction
    | RecordInvestigationAction of RecordInvestigationAction
    | RecordFinding of RecordFinding
    | EstablishNegativeKnowledge of EstablishNegativeKnowledge
    | RecordCausalAssertion of RecordCausalAssertion
    | ConfirmCause of ConfirmCause
    | RecordCorrectiveAction of RecordCorrectiveAction
    | RecordVerificationCriterion of RecordVerificationCriterion
    | MarkCorrectiveActionImplemented of CorrectiveActionId
    | RecordVerification of RecordVerification
    | ResolveInvestigation
    | ReopenInvestigation of ReopenInvestigation
    | RecordProposal of RecordProposal
    | AcceptProposal of ProposalId
    | RejectProposal of ProposalId
    | AttachExtension of ExtensionEnvelope

type Command =
    | Core of CoreCommand
    | Extension of SemanticId * payloadJson:string
```

Do not put every future EDF command into CoreCommand.

## 32. Command envelope

```fsharp
type CommandEnvelope = {
    CommandId : CommandId
    InvestigationId : InvestigationId
    ExpectedVersion : AggregateVersion option
    Actor : Actor
    RequestedAt : DateTimeOffset
    SemanticManifest : InvestigationSemanticManifest
    IdempotencyKey : string option
    CorrelationId : string option
    CausationId : string option
    Command : Command
}
```

StartInvestigation may have ExpectedVersion=None.

> **Superseded by frozen contract v1.0.0:** the persisted `correlationId` is `CommandId`; the optional CorrelationId/CausationId/IdempotencyKey are not persisted. `RequestedAt` becomes `occurredAt` and MUST be millisecond-truncated UTC (CONTRACT §2.9).

## 33. Events

Do not require a forever-closed wire DU. ***Superseded by frozen contract v1.0.0:** each contract version has a closed wire registry (31 events in v1.0.0, [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md)); new events arrive only with a new minor contract version (CONTRACT §13.1).*

Inside a given core assembly version, typed core events are useful:

```fsharp
type CoreEvent =
    | InvestigationStarted of InvestigationStarted
    | InvestigationActivated
    | ObservationRecorded of Observation
    | HypothesisRecorded of Hypothesis
    | HypothesisMarkedPlausible of HypothesisId
    | HypothesisMarkedSupported of HypothesisId
    | HypothesisFalsified of HypothesisId * reason:NonEmptyText * Scope
    | HypothesisReopened of HypothesisId * reason:NonEmptyText
    | EvidenceAdmitted of Evidence
    | EvidenceRelationshipRecorded of EvidenceRelationship
    | UnknownIdentified of Unknown
    | AssumptionRecorded of Assumption
    | ContradictionRecorded of Contradiction
    | PredictionRecorded of Prediction
    | InvestigationActionRecorded of InvestigationAction
    | FindingRecorded of Finding
    | CausalAssertionRecorded of CausalAssertion
    | CauseConfirmed of CausalAssertionId
    | ConfirmedCauseReopened of CausalAssertionId * reason:NonEmptyText
    | CorrectiveActionRecorded of CorrectiveAction
    | VerificationCriterionRecorded of VerificationCriterion
    | CorrectiveActionImplemented of CorrectiveActionId
    | VerificationRecorded of Verification
    | InvestigationResolved
    | InvestigationReopened of NonEmptyText
    | ProposalRecorded of Proposal
    | ProposalRejected of ProposalId
    | ExtensionAttached of ExtensionEnvelope

type EventPayload =
    | CoreEvent of CoreEvent
    | ExtensionEvent of SemanticId * payloadJson:string
```

Wire serialization should carry an EventType SemanticId/version so older/newer clients can reason about compatibility. ***Superseded by frozen contract v1.0.0:** the wire carries `eventType` (closed PascalCase discriminator), `formatVersion` and `semanticVersion`. Wire names for the cases above: InvestigationActivated/Resolved/Reopened → `LifecycleChanged`; HypothesisMarkedPlausible/Supported, HypothesisFalsified, HypothesisReopened → `HypothesisTransitioned`; EvidenceRelationshipRecorded → `EvidenceRelated`; InvestigationActionRecorded → `ActionRecorded`; CauseConfirmed → `CausalAssertionConfirmed`; ConfirmedCauseReopened → `CausalAssertionReopened`; VerificationCriterionRecorded → `CriterionRecorded`; CorrectiveActionImplemented → `CorrectiveActionTransitioned`; ProposalRejected → `ProposalWasRejected`; ExtensionAttached → deferred.*

## 34. Event envelope

```fsharp
type EventEnvelope = {
    EventId : EventId
    InvestigationId : InvestigationId
    Version : AggregateVersion
    OccurredAt : DateTimeOffset
    Actor : Actor
    SemanticManifest : InvestigationSemanticManifest
    PolicyRefs : PolicyRef list
    CommandId : CommandId
    CorrelationId : string option
    CausationId : string option
    Payload : EventPayload
}
```

> **Superseded by frozen contract v1.0.0:** the v1.0.0 wire envelope has exactly `formatVersion, eventId, investigationId, aggregateVersion, eventType, occurredAt, actor, semanticVersion, correlationId (= CommandId), payload`; no SemanticManifest, PolicyRefs or CausationId (CONTRACT §5).

## 35. Blockers

```fsharp
type BlockerClass =
    | InvariantViolation
    | MissingRequiredEvidence
    | BlockingUnknown
    | UnresolvedContradiction
    | UnverifiedAssumption
    | MissingVerification
    | MissingPermission
    | MissingReview
    | UnsupportedSemantics
    | StaleVersion
    | InvalidLifecycle
    | PolicyPrerequisite
    | PersistenceConflict
    | ExtensionUnavailable

type Blocker = {
    Code : string
    Class : BlockerClass
    Message : NonEmptyText
    SourcePolicy : PolicyRef option
    Related : EntityRef list
    SuggestedCapability : string option
}
```

## 36. Decision

```fsharp
type VersionConflict = {
    Expected : AggregateVersion
    Actual : AggregateVersion
}

type ReviewRequirement = {
    Id : string
    Message : NonEmptyText
    Policy : PolicyRef
}

type Decision =
    | Allowed of EventPayload list
    | Blocked of Blocker list
    | RequiresReview of ReviewRequirement list
    | UnsupportedSemantics of SemanticDependency list
    | Conflict of VersionConflict
    | Duplicate of EventId list
```

Persistence assigns final aggregate versions/EventIds if the architecture chooses to separate proposed events from committed event envelopes. ***Superseded by frozen contract v1.0.0:** EventIds are derived per CONTRACT §5.2, never random.*

## 37. Pure decision function

Target shape:

```fsharp
val decide :
    policies:PolicySet ->
    actor:ActorContext ->
    state:Investigation option ->
    command:CommandEnvelope ->
    Decision
```

PolicySet is composed outside the core from core + domain + experimental + organization policy.

## 38. Pure evolution function

```fsharp
val evolve :
    state:Investigation option ->
    event:EventEnvelope ->
    Result<Investigation, ProjectionError>
```

evolve must not call:

- network
- GitHub
- AI
- clock
- random generator

Those values already exist in the event envelope.

## 39. Capability projection

```fsharp
type CapabilityState =
    | Available
    | Unavailable of Blocker list
    | ReviewRequired of ReviewRequirement list
    | SemanticsUnavailable of SemanticDependency list

type Capability = {
    Id : string
    State : CapabilityState
}

val capabilities :
    policies:PolicySet ->
    actor:ActorContext ->
    state:Investigation ->
    Capability list
```

The UI consumes these results.

## 40. Policy interface

First implementation can remain typed F#.

```fsharp
type RuleContext = {
    Investigation : Investigation option
    Actor : ActorContext
    Command : CommandEnvelope
}

type RuleEffect =
    | NoOpinion
    | Block of Blocker
    | RequireReview of ReviewRequirement
    | Warn of string

type PolicyRule = RuleContext -> RuleEffect list

type PolicySet = {
    Refs : PolicyRef list
    Rules : PolicyRule list
}
```

Do not introduce a general DSL yet.

## 41. Domain pack interface

Logical shape:

```fsharp
type DomainPack = {
    Reference : DomainPackRef
    SemanticDependencies : SemanticDependency list
    Policies : PolicyRule list
    DecodeExtension : ExtensionEnvelope -> Result<obj, string>
    ValidateExtension : ExtensionEnvelope -> Result<unit, string list>
}
```

The final interface should avoid raw obj, likely using an internal registry/codec abstraction. This is intentionally marked as implementation exploration rather than CoreStable API.

## 42. Persistence port

```fsharp
type ProviderVersion = private ProviderVersion of string

type LoadedInvestigation = {
    State : Investigation
    ProviderVersion : ProviderVersion
}

type AppendResult =
    | Appended of ProviderVersion * EventEnvelope list
    | ProviderConflict of ProviderVersion
    | ProviderFailure of string

type IInvestigationStore =
    abstract Load : InvestigationId -> Async<Result<LoadedInvestigation option, string>>
    abstract Append :
        InvestigationId *
        ProviderVersion option *
        EventPayload list ->
            Async<AppendResult>
```

This interface belongs outside the pure domain assembly.

GitHub implementation maps ProviderVersion to SHA/commit metadata. *(v1.0.0: the branch head commit, CONTRACT §9.4.2.)*

## 43. Time/randomness

Domain commands requiring IDs/time should receive them from application services or validated command factories.

Do not call DateTimeOffset.UtcNow or Guid.NewGuid deep inside pure domain decisions if deterministic replay/testing depends on them.

A command creation layer can safely supply them.

## 44. Serialization

Keep serialization DTOs separate from domain records when needed for:

- backward compatibility
- unknown extension preservation *(Superseded by frozen contract v1.0.0: closed structures reject unknown data, CONTRACT §2.4, §13.5)*
- wire versioning
- optional fields *(Superseded by frozen contract v1.0.0: no optional keys; `option` encodes as required `null`, §2.5)*
- provider storage

Do not decorate the pure domain model until serialization concerns dictate the type design.

## 45. Test shape

Each command handler should have table-driven tests:

```text
Given state S
And policy P
When command C
Then decision D
And emitted event kinds E
And evolved state S'
And capabilities K
```

Property tests are useful for:

- aggregate version monotonicity
- no event rewrite
- superseded records remain in history
- unsupported required semantics never allow unsafe write
- unknown optional extensions survive projection/serialization round-trip *(Superseded by frozen contract v1.0.0: unknown data is refused with its CONTRACT §11.1 code, never dropped)*
- no ConfirmCause when required blockers exist
- no Resolved when verification gate fails

## 46. Module boundary recommendation

Initial solution structure:

```text
Investigator.Domain
  Identity.fs
  Semantics.fs
  Entities.fs
  Commands.fs
  Events.fs
  Evolution.fs
  Policies.fs
  Capabilities.fs
  Validation.fs

Investigator.Application
  CommandHandler.fs
  ContextBuilder.fs
  Synchronization.fs
  Proposals.fs
  Reporting.fs

Investigator.Persistence
  Contracts.fs
  GitHub.fs
  LocalStore.fs
  Serialization.fs

Investigator.AI
  Contracts.fs
  OpenAI.fs
  Anthropic.fs
  PromptRegistry.fs

Investigator.Web
  Limen boundary
  Forma components/adapters
  UI projections
```

Exact project split is ReversibleChoice. Dependency direction is more important:

```text
Web / AI / Persistence
        ↓
Application
        ↓
Domain
```

Domain depends on none of them.

## 47. First implementation warning

These shapes should guide kickoff coding, not become a reason to avoid learning.

If tests reveal that:

- a state is better derived,
- a core taxonomy is too large,
- a relationship belongs in an extension,
- a command combines two independent intents,

change the implementation profile explicitly and update the relevant decision/open-question record.

The purpose of strong types is to make semantics explicit, not to fossilize the first model.
