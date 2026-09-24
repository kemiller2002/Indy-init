# Indy Init

Planning and experiment repository for the Indy Init hackathon.

## Objective

Use the hackathon as an externally constrained test of the Echelon Foundry software factory.

The competition application will be the first implementation of a reusable **Root-Cause Investigator platform**. It will begin from zero application source code at kickoff and will use the prepared engineering framework, requirements, architecture, and design work to produce a working application during the event.

The product is intentionally generic. After the hackathon, EDF will become the first domain specialization of the same investigation kernel so that EDF can investigate its own experiment results, regressions, assumptions, and unknown effects without forking the core product.

## Core hypothesis

AI code generation is not the scarce capability. The hard problem is producing software and conclusions that can be defended with explicit state, evidence, tests, constraints, provenance, and unresolved uncertainty.

The hackathon tests two nested systems:

1. The Root-Cause Investigator governs an investigation using explicit state.
2. The Echelon software factory governs the creation of the Root-Cause Investigator using explicit engineering state.

## Planned stack

- **Ordo** - explicit engineering and domain state, legal transitions, obligations, evidence
- **ROS** - engineering execution and orchestration
- **Aegis** - faults, invalid transitions, diagnostics, explicit failure
- **Praxis** - code and process metrics, thresholds, change history
- **Limen** - browser/WASM boundary and application runtime pattern
- **Forma** - accessible, mobile-first web components
- **Folio** - professional printable/shareable investigation reports
- **GitHub** - source control plus application persistence/event history

## Device target

The primary demonstration target is **iPad Safari**.

The application must be usable without a desktop development environment. GitHub will act as the remote persistence layer. Active investigation state should remain usable during transient connectivity loss, with remote writes represented as pending rather than falsely reported as complete.

## Repository intent before kickoff

This repository may contain:

- requirements
- architecture
- state models
- acceptance criteria
- threat model
- UX specifications
- test strategy
- experiment protocol
- demo plan
- engineering-framework integration requirements

It should not contain the implemented competition application before kickoff.

See [Hackathon Boundaries](docs/HACKATHON-BOUNDARIES.md).

## Documents

- [Product Concept](docs/PRODUCT-CONCEPT.md)
- [Long-term Product Requirements](docs/PRODUCT-REQUIREMENTS.md)
- [Investigation Semantics](docs/INVESTIGATION-SEMANTICS.md)
- [Semantic Evolution and Research Safety](docs/SEMANTIC-EVOLUTION.md)
- [Minimal Core Domain Model](docs/CORE-DOMAIN-MODEL.md)
- [Domain Pack Contract](docs/DOMAIN-PACK-CONTRACT.md)
- [Policy and Transition Engine](docs/POLICY-ENGINE.md)
- [Command and Event Catalog](docs/COMMAND-EVENT-CATALOG.md)
- [State Transition Tables](docs/STATE-TRANSITION-TABLES.md)
- [F# Implementation Type Shapes](docs/FSHARP-TYPE-SHAPES.md)
- [Domain Test Matrix](docs/DOMAIN-TEST-MATRIX.md)
- [Semantic Compiler and Generated Contracts](docs/SEMANTIC-COMPILER.md)
- [Formal and State-Space Validation](docs/FORMAL-VALIDATION.md)
- [Engineering Proof Bundle](docs/ENGINEERING-PROOF-BUNDLE.md)
- [Screen Contracts](docs/SCREEN-CONTRACTS.md)
- [Persistence, Portability, and Recovery](docs/PERSISTENCE-CONTRACT.md)
- [AI Investigation Orchestration](docs/AI-ORCHESTRATION.md)
- [Proof-Carrying AI Proposals](docs/PROOF-CARRYING-PROPOSALS.md)
- [Investigation Planner](docs/INVESTIGATION-PLANNER.md)
- [Cross-Investigation Intelligence](docs/CROSS-INVESTIGATION-INTELLIGENCE.md)
- [Research Harness](docs/RESEARCH-HARNESS.md)
- [Investigation Workspace](docs/INVESTIGATION-WORKSPACE.md)
- [Competition Ambition Profile](docs/COMPETITION-AMBITION.md)
- [Multi-Agent Adversarial Investigation](docs/MULTI-AGENT-INVESTIGATION.md)
- [Counterfactual Branching and Sensitivity](docs/COUNTERFACTUAL-BRANCHING.md)
- [Advanced Investigation Visualizations](docs/ADVANCED-VISUALIZATIONS.md)
- [Competition Demo Choreography](docs/COMPETITION-DEMO-CHOREOGRAPHY.md)
- [Security, Privacy, and Threat Model](docs/SECURITY-THREAT-MODEL.md)
- [Implementation Slices](docs/IMPLEMENTATION-SLICES.md)
- [Open Questions and Research Register](docs/OPEN-QUESTIONS.md)
- [Design Decision Boundaries](docs/DESIGN-DECISION-BOUNDARIES.md)
- [Investigation Acceptance Scenarios](docs/INVESTIGATION-ACCEPTANCE-SCENARIOS.md)
- [Requirements](docs/REQUIREMENTS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Hackathon Boundaries](docs/HACKATHON-BOUNDARIES.md)
- [Experiment Plan](docs/EXPERIMENT-PLAN.md)
- [Demo Plan](docs/DEMO-PLAN.md)
- [EDF Follow-on](docs/EDF-FOLLOW-ON.md)
- [UI Contract Exploration](docs/UI-CONTRACT-EXPLORATION.md)
