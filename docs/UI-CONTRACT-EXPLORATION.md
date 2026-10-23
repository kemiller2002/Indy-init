# UI Contract Exploration

Status: exploratory design. This document is intended to define how an agent can be prevented from building the wrong user interface even when it follows general visual guidance and uses the correct component library.

## Problem

Visual Engineering provides evidence-based design criteria. Forma provides the approved presentation vocabulary. Neither, by itself, guarantees that an agent will choose the correct information architecture, screen composition, state projection, or interaction flow for a particular product.

An agent can therefore produce a UI that:

- uses approved components
- is accessible
- is responsive
- follows visual guidance

and still be the wrong product.

The missing layer is an application-specific, machine-checkable UI contract.

## Proposed model

The contract should govern five distinct concerns:

1. **Screen intent** - what question the screen answers for the user.
2. **Information architecture** - what regions and information must be present and how important they are.
3. **State projection** - how domain state becomes visible UI state.
4. **Interaction contract** - what actions are available, unavailable, or required in each state.
5. **Verification evidence** - how the implementation proves that it satisfies the contract.

Visual styling remains largely delegated to Forma and Visual Engineering. Product meaning does not.

## Principle

> The agent may choose implementation details, but it may not redefine product meaning.

## 1. Screen contract

Every consequential screen should have a screen contract.

A contract should include at least:

- stable screen identifier
- user purpose
- primary user question
- required information regions
- required actions
- prohibited actions
- required visible state
- permitted hidden/secondary state
- component constraints
- responsive behavior
- accessibility obligations
- completion evidence

Example:

```yaml
screen: investigation-workspace
purpose: investigate an observed failure without premature convergence
primaryQuestion: what currently explains the observation, and what should be tested next?

requiredRegions:
  - observation
  - hypotheses
  - evidence
  - unknowns
  - obligations
  - legal-actions

requiredVisibleState:
  - hypothesis-state
  - evidence-relationship
  - unresolved-unknown-count
  - pending-obligations
  - persistence-state

forbidden:
  - chat-only-primary-interface
  - destructive-removal-of-falsified-hypotheses
  - hidden-critical-obligations
  - enabled-illegal-domain-actions
```

The exact serialized format is not yet decided. YAML is readable, but the canonical implementation should have a typed representation.

## 2. Typed contract model

The application should not rely only on schema-valid YAML.

A typed F# model can make contract categories explicit and support compilation into tests and documentation.

Illustrative direction:

```fsharp
type RegionId =
    | Observation
    | Hypotheses
    | Evidence
    | Unknowns
    | Obligations
    | LegalActions

type VisibilityRequirement =
    | AlwaysVisible of RegionId
    | VisibleWhen of RegionId * DomainPredicate
    | MayCollapse of RegionId
    | Secondary of RegionId

type InteractionRequirement =
    | MustExpose of Capability
    | MustDisableWhenIllegal of Capability
    | MustExplainUnavailable of Capability

type ScreenContract =
    {
        Id : ScreenId
        Purpose : string
        PrimaryQuestion : string
        Regions : RegionContract list
        Visibility : VisibilityRequirement list
        Interactions : InteractionRequirement list
        ForbiddenPatterns : ForbiddenPattern list
        Breakpoints : BreakpointContract list
        Evidence : UiEvidenceRequirement list
    }
```

This model can be compiled into deterministic checks.

## 3. State-to-UI projection matrix

The most important contract may not be the static screen definition. It is the projection from domain state to visible state.

Example:

| Domain condition | Required UI |
| --- | --- |
| hypothesis proposed | hypothesis visible with Proposed state |
| hypothesis falsified | remains visible and clearly marked Falsified |
| contradictory evidence exists | contradiction visible without opening a hidden dialog |
| blocking unknown exists | obligation visible and dependent action unavailable |
| GitHub write pending | persistence indicator says Pending |
| remote conflict detected | conflict is visible and destructive synchronization is blocked |
| root cause not sufficiently evidenced | Confirm Root Cause action unavailable with reason |
| corrective action performed but unverified | state says Awaiting Verification, not Resolved |

This should be generated from the same domain concepts that govern legal transitions whenever practical.

The goal is to prevent divergence between what the system knows and what the screen tells the user.

## 4. Information hierarchy contract

Presence alone is insufficient. An agent can technically place all required information on a screen while burying the important parts.

Contracts therefore need hierarchy.

Possible levels:

- Primary - immediately visible and visually dominant
- Secondary - visible but subordinate
- Tertiary - available through progressive disclosure
- Background - accessible for audit/history but not part of the active task

For the investigator:

### Primary
- current observation
- active hypotheses
- strongest contradictions
- current unknowns blocking progress
- recommended/discriminating next action

### Secondary
- supporting evidence
- non-blocking unknowns
- completed obligations

### Tertiary
- full history
- superseded evidence
- detailed provenance

A contract violation occurs if an agent makes a tertiary element visually dominant while hiding a primary element behind navigation.

## 5. Component mapping

Forma should remain the canonical component vocabulary, but each contract may constrain which component families are appropriate.

Example:

```yaml
region: hypotheses
allowedPatterns:
  - ef-card
  - ef-status-badge
  - ef-action-rail

disallowedPatterns:
  - modal-only-list
  - transient-toast
```

This should not over-specify CSS. The objective is to restrict semantic misuse, not prevent responsive design.

## 6. Interaction contracts

An interaction contract describes outcomes rather than click choreography.

Example:

Given:
- a hypothesis has unresolved contradictory evidence
- policy says contradiction resolution is required before confirmation

Then:
- Confirm Root Cause must not be executable
- the unavailable action remains discoverable
- the UI explains the blocking reason
- the user can navigate directly to the blocking contradiction

This is stronger than testing for a disabled button. It tests product meaning.

## 7. Anti-pattern contracts

Some failures are important enough to forbid explicitly.

For the Root-Cause Investigator:

- chat transcript as the only authoritative representation
- hiding falsified hypotheses
- deleting negative knowledge
- using color alone to distinguish hypothesis state
- hiding blocking obligations in a secondary settings/details view
- presenting an LLM-generated confidence percentage without evidentiary semantics
- presenting an optimistic persistence state before GitHub confirmation
- allowing visual controls for transitions the domain model rejects
- reducing an investigation to a linear wizard that prevents comparison among competing hypotheses

These should be machine-readable where possible.

## 8. Verification ladder

No single verification technique is sufficient.

### Layer A - Static contract validation
Verify that all required contracts are syntactically and semantically valid.

Examples:
- every screen has a purpose
- every required region maps to an implementation marker
- referenced Forma patterns exist
- every domain capability used by a UI contract exists

### Layer B - Structural DOM validation
Use stable semantic markers such as:

```html
<section data-ui-region="hypotheses">...</section>
```

Verify:
- required regions exist
- required accessible names exist
- prohibited structures are absent
- actions expose legal/illegal state correctly

This should avoid brittle CSS-selector-only tests.

### Layer C - Domain/UI projection tests
Given a known domain state, render the UI and assert the required visible projection.

Example:

```text
Given hypothesis H1 is Falsified
Then H1 remains present
And its state is exposed as Falsified
And actions that depend on H1 being active are unavailable
```

### Layer D - Task-flow tests
Verify user outcomes.

Example:

```text
A user can identify why Confirm Root Cause is unavailable
and reach the blocking evidence or obligation without guessing.
```

### Layer E - Accessibility checks
Verify semantic HTML, keyboard behavior, labels, focus, text scaling, contrast, reduced motion, and other relevant criteria.

### Layer F - Responsive evidence
Capture required viewport states:

- iPad portrait
- iPad landscape
- narrow phone
- desktop/reference viewport

Each contract identifies what may reflow and what must remain visible.

### Layer G - Visual regression
Use screenshots to detect unexpected macro-layout and styling changes.

Pixel-perfect comparison should not be the sole gate. It is too brittle for text rendering, browser differences, and intended responsive changes.

Prefer visual regression as evidence around:
- region position
- overlap
- clipping
- hierarchy
- gross spacing
- missing content
- component regression

### Layer H - Semantic visual review
A vision-capable reviewer may inspect screenshots against the contract and identify failures that structural tests miss, such as poor hierarchy.

This is useful evidence but should not be the only deterministic gate.

## 9. Completion gate

A screen should not become Complete merely because it renders.

Proposed evidence set:

```text
ScreenContract
  -> component mapping
  -> structural checks
  -> state-projection tests
  -> interaction tests
  -> accessibility checks
  -> responsive captures
  -> visual regression
  -> unresolved deviations = 0 or explicitly accepted
```

Ordo/ROS should be able to keep a screen in an incomplete state when required UI evidence is missing.

## 10. Contract drift

A major risk is the implementation changing while the contract remains stale.

Therefore:

- UI-changing work should identify affected screen contracts.
- A contract change should be explicit, reviewed, and versioned.
- A screenshot change without a corresponding contract or implementation reason should be suspicious.
- A changed domain transition should trigger review of dependent UI contracts.
- New Forma components should not silently alter existing application semantics.

## 11. Generated artifacts

A typed UI contract system could generate:

- human-readable screen specification
- agent implementation guidance
- test cases/stubs
- required screenshot matrix
- accessibility checklist
- component usage manifest
- traceability records
- judge/demo documentation

This reduces duplication and keeps the human and machine views aligned.

## 12. Ownership

The responsibilities should remain separated:

### Visual Engineering
Owns evidence-based visual/design decision criteria.

### Forma
Owns reusable presentation primitives and canonical component contracts.

### Application
Owns product-specific screen contracts and domain-to-UI projection.

### Ordo
Owns legal domain state and transition semantics.

### ROS
Owns work execution, evidence, traceability, and completion.

### Aegis
Owns explicit faults when UI/domain invariants are violated.

### Praxis
Owns relevant measurable engineering change and regression signals.

The application contract should compose these systems rather than creating another design system.

## 13. Root-Cause Investigator initial contracts

Likely screen contracts:

1. Investigation Start
2. Investigation Workspace
3. Hypothesis Detail
4. Evidence Capture/Detail
5. Experiment Definition
6. Experiment Result
7. Unknown/Obligation Detail
8. Root-Cause Review
9. Corrective Action
10. Verification
11. Investigation History
12. Report Preview
13. GitHub Persistence/Conflict Resolution

The Investigation Workspace is the critical contract and should be specified first.

## 14. Key experiment

Before the hackathon, give multiple agents the same screen contract independently.

Do not give them screenshots of each other's implementations.

Then measure:

- structural contract compliance
- state-projection compliance
- task completion
- accessibility
- responsive behavior
- visual variation
- number and severity of semantic deviations

If independently generated UIs differ visually but preserve the same product meaning, the contract is doing its job.

If they satisfy the formal checks but humans still identify materially different or incorrect products, the missing invariant should be added to the contract model.

This turns UI guidance itself into an EDF-like empirical engineering problem.


## 15. Semantic projection invariants

The UI contract must project the canonical investigation semantics rather than infer meaning independently.

At minimum, screen contracts and projection tests must verify:

- Observation remains distinguishable from Interpretation and Hypothesis.
- AgentProposal remains distinguishable from AuthoritativeState.
- FailsToSupport is not rendered as Contradicts. *(FailsToSupport is not persisted in v1.0.0; `relationshipKind` = supports|contradicts|discriminates|insufficient.)*
- Falsified hypotheses remain inspectable with falsification basis and scope.
- Evidence dependency can be surfaced so correlated records are not visually implied to be independent confirmation.
- Blocking Unknowns, Contradictions, and Obligations are visible where they affect legal actions.
- A Leading explanation is not visually presented as ConfirmedCause.
- Implemented corrective action is not presented as VerifiedEffective.
- Residual uncertainty remains visible with the conclusion.
- Pending or conflicted persistence is not rendered as successfully saved authoritative state.
- A causal set can be represented without forcing one member into a false single-root-cause hierarchy.

These projections should be derived from the domain state and capabilities defined by the canonical semantics wherever practical.


## 16. Application screen contract baseline

The initial application-specific screen inventory is defined in [SCREEN-CONTRACTS.md](SCREEN-CONTRACTS.md).

Percepta should ultimately compile or validate these contracts against implementation.

The screen contracts intentionally define:

- purpose
- primary question
- required semantic regions
- required actions
- prohibited implications
- responsive obligations
- accessibility obligations
- completion evidence

They intentionally do not freeze:

- exact grid
- pixel dimensions
- animation styling
- component placement
- desktop-only information architecture

This enables multiple agents/design approaches to implement the same product meaning and allows semantic comparison of their results.
