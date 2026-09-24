# Architecture

## Design goals

- iPad-first operation
- no traditional application database required for the hackathon version
- explicit investigation state
- auditable persistence
- offline tolerance
- clear separation between UI, state/domain logic, AI reasoning, persistence, and reporting
- reusable generic investigation kernel
- versioned domain-pack extensibility
- storage-provider abstraction
- AI-provider abstraction
- EDF specialization after the hackathon without a core fork

## Logical architecture

```text
iPad Safari
    |
    v
Forma UI
    |
    v
Limen boundary/runtime
    |
    v
Investigation state + legal transitions
    |
    +--> AI reasoning adapter
    |
    +--> Aegis faults/invariants
    |
    +--> Folio report projection
    |
    +--> Local durable queue/cache
              |
              v
          GitHub API
              |
              v
      Investigation repository
```

## Persistence boundary

The investigation domain must not depend directly on GitHub-specific storage semantics.

Persistence should be represented through an explicit provider contract supporting operations such as load, append/change, optimistic version check, export, and recovery. GitHub is the first provider because it offers both storage and useful provenance.

Future providers may include a service API, database-backed deployment, customer-hosted storage, or file-based import/export without changing investigation semantics.

## GitHub as persistence

GitHub is not treated merely as a bucket of JSON files. The repository also provides provenance.

A possible layout:

```text
investigations/
  <investigation-id>/
    investigation.json
    events.ndjson
    evidence/
      index.json
    experiments/
      index.json
    reports/
      current.json
```

Large or binary evidence can be referenced by URL rather than committed directly.

## Current state vs history

The architecture should distinguish:

- current projected state
- append-oriented investigation events
- Git commit history

The application should not require replaying the entire Git history for normal rendering, but history should remain available for audit and reconstruction.

## Local-first behavior

The browser should maintain enough durable local state to continue an active investigation during temporary network loss.

Remote persistence states should be explicit, for example:

- Synced
- Pending
- Conflict
- Failed

A remote commit is never assumed successful before confirmation.

## Concurrency

GitHub writes should use optimistic concurrency based on the last known version/SHA.

If the remote version changed:

1. stop the destructive write
2. preserve local work
3. surface the conflict
4. reconcile explicitly
5. write only after a valid merged state exists

## Authentication

Preferred production direction:

- GitHub App or OAuth-based user authorization
- repository-scoped access
- minimum necessary permissions
- no long-lived token embedded in source
- no credentials in LocalStorage when a safer supported mechanism is practical

A hackathon shortcut, if one is necessary, must be documented as a temporary deviation and must not be presented as the production design.

## Evolution-safe semantic architecture

The system must follow [SEMANTIC-EVOLUTION.md](SEMANTIC-EVOLUTION.md).

The core architecture is intentionally layered:

```text
Generic Core
  -> stable epistemic primitives and invariants

Domain Pack
  -> versioned domain semantics and policies

Experimental Profile
  -> explicitly enabled research constructs

Projection Layers
  -> UI, AI context, reports, persistence adapters
```

The generic kernel must not need source changes merely to add an EDF research construct.

F# should use closed discriminated unions where closed-world semantics are a deliberate invariant. Concepts expected to evolve should use qualified typed semantic identities and validated extension envelopes rather than a continually expanding core union or untyped arbitrary JSON.

Clients must preserve unknown optional extension data. When required semantics are unknown, they must prevent unsafe writes instead of guessing.

Schema evolution and policy evolution are separate. Historical transitions retain the semantic and policy versions under which they occurred. Reassessment under newer semantics creates new state; it does not rewrite historical state.

## Domain packs

Domain-specific behavior must extend the generic kernel through versioned domain packs.

A domain pack may contribute:

- vocabulary and labels
- evidence and hypothesis categories
- experiment/check templates
- additional invariants and completion policy
- UI projections
- AI guidance
- report sections
- migrations and compatibility rules

The core must remain usable without any domain pack. EDF should be the first specialist pack after the hackathon.

## Semantic authority

The generic investigation kernel must conform to [INVESTIGATION-SEMANTICS.md](INVESTIGATION-SEMANTICS.md).

The canonical semantics define domain meaning and invariants. UI projections, AI proposals, persistence adapters, domain packs, and report generation are consumers of that semantic model. They may not redefine it independently.

Acceptance behavior is defined in [INVESTIGATION-ACCEPTANCE-SCENARIOS.md](INVESTIGATION-ACCEPTANCE-SCENARIOS.md) and should be promoted into executable domain tests.

## Investigation kernel

The generic domain should include at least:

- Observation
- Hypothesis
- Evidence
- EvidenceRelationship
- Prediction
- Experiment
- Finding
- Contradiction
- Unknown
- NegativeKnowledge
- Cause
- ContributingFactor
- CorrectiveAction
- VerificationCriterion
- Obligation

The kernel should not depend on EDF vocabulary.

## State integrity

Legal transitions should be governed by explicit prerequisites.

Example:

```text
ProposedHypothesis
    -> SupportedHypothesis
       requires linked supporting evidence

SupportedHypothesis
    -> ConfirmedCause
       requires:
       - required discriminating checks complete
       - contradictory evidence resolved or explicitly accepted
       - required unknowns reconciled
       - verification policy satisfied
```

## Versioning and migration

Persisted investigations must record enough version information to interpret them later, including schema version and applicable domain-policy/domain-pack version.

A client must not silently write an investigation whose persisted meaning it does not fully understand. Meaning-changing upgrades require explicit migration or compatibility behavior.

## AI boundary

The LLM may propose:

- hypotheses
- questions
- experiments
- evidence relationships
- interpretations
- corrective actions

The LLM does not unilaterally mutate authoritative state.

Its proposals must be validated against the domain model and transition rules.

## Framework integration

### Ordo
Defines explicit state, legal transitions, evidence, capabilities, obligations, unknowns, and completion semantics.

### ROS
Controls engineering work and records execution evidence.

### Aegis
Represents faults, illegal transitions, invariant violations, and user-visible diagnostic information.

### Praxis
Captures engineering metrics, thresholds, file churn, and repeatedly modified hunks/files during the build.

### Limen
Provides the browser/WASM boundary and minimizes unnecessary JavaScript.

### Forma
Provides accessible, responsive UI primitives and interaction patterns.

### Folio
Produces professional printable/shareable investigation reports.

## Deployment

The target should be compatible with static hosting such as GitHub Pages when practical.

Any required backend for authentication or AI invocation must be minimized and explicitly documented.

The architectural goal is not "zero server at any cost." The goal is the smallest defensible architecture that preserves security and the demonstration goals.


## Decision stability

Architecture decisions are governed by [DESIGN-DECISION-BOUNDARIES.md](DESIGN-DECISION-BOUNDARIES.md).

The generic aggregate direction is specified in [CORE-DOMAIN-MODEL.md](CORE-DOMAIN-MODEL.md), and domain extension behavior is specified in [DOMAIN-PACK-CONTRACT.md](DOMAIN-PACK-CONTRACT.md).

Where implementation details remain open, agents must prefer reversible choices and must not resolve active EDF research questions by hard-coding them into authoritative generic state.


## Policy execution boundary

Authoritative transition evaluation follows [POLICY-ENGINE.md](POLICY-ENGINE.md).

The UI, AI layer, and persistence adapters request commands and consume typed decisions/capabilities. They do not independently determine whether a transition is legal.

Offline operation stores intent as versioned commands. On reconnect, commands are revalidated against current state, semantics, policy, and actor capabilities before application.

## Persistence portability boundary

Persistence follows [PERSISTENCE-CONTRACT.md](PERSISTENCE-CONTRACT.md).

GitHub is the first provider, not the generic storage model. Investigation identity, semantic events, extension envelopes, policy attribution, and canonical export remain provider-independent.

Provider-specific version tokens such as GitHub blob/commit SHAs belong in the adapter boundary. Conflicts are reconciled semantically, not through blind serialized-text merges.


## AI orchestration boundary

AI behavior follows [AI-ORCHESTRATION.md](AI-ORCHESTRATION.md).

The model receives scoped semantic projections and produces validated proposals. It does not own authoritative lifecycle state, transition legality, semantic migration, or persistence truth.

The architecture should support replacing or combining AI providers without changing domain semantics.

## Primary workspace boundary

The working experience follows [INVESTIGATION-WORKSPACE.md](INVESTIGATION-WORKSPACE.md).

Chat is an input and explanation surface. Structured semantic state, capabilities, blockers, and proposal controls are the authoritative interaction surface for consequential work.


## Security boundary

Security and privacy behavior follow [SECURITY-THREAT-MODEL.md](SECURITY-THREAT-MODEL.md).

External evidence is untrusted data. AI providers, persistence providers, local browser storage, and domain packs are explicit trust boundaries.

Authorization and semantic legality remain separate. A caller must satisfy both.

The initial hackathon deployment may use documented temporary shortcuts, but those shortcuts must not leak into the generic domain or persistence contracts.
