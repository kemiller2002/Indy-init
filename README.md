# Indy Init

Planning and experiment repository for the Indy Init hackathon.

## Objective

Use the hackathon as an externally constrained test of the Echelon Foundry software factory.

The competition application will be a **Root-Cause Investigator**. It will begin from zero application source code at kickoff and will use the prepared engineering framework, requirements, architecture, and design work to produce a working application during the event.

The application is intentionally generic. After the hackathon, the same investigation kernel will be adapted as an **EDF Investigator** so that EDF can use it to investigate its own experiment results, regressions, assumptions, and unknown effects.

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
- [Requirements](docs/REQUIREMENTS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Hackathon Boundaries](docs/HACKATHON-BOUNDARIES.md)
- [Experiment Plan](docs/EXPERIMENT-PLAN.md)
- [Demo Plan](docs/DEMO-PLAN.md)
- [EDF Follow-on](docs/EDF-FOLLOW-ON.md)
