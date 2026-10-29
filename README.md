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

The application must be usable without a desktop development environment. GitHub is the only durable store: every save is an atomic, expected-head commit that is confirmed before the interface says Saved. Each browser tab keeps its own investigation state in memory; an investigation already loaded stays visible during transient connectivity loss, and changes made then remain unsaved in that tab, never falsely reported as saved ([Causa Persistence Requirements](docs/CAUSA-PERSISTENCE-REQUIREMENTS.md)).

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
- [Causa Persistence Requirements (Rehearsal 7 onward)](docs/CAUSA-PERSISTENCE-REQUIREMENTS.md)
- [AI Investigation Orchestration](docs/AI-ORCHESTRATION.md)
- [Proof-Carrying AI Proposals](docs/PROOF-CARRYING-PROPOSALS.md)
- [Investigation Planner](docs/INVESTIGATION-PLANNER.md)
- [Tool Execution Gateway](docs/TOOL-EXECUTION-GATEWAY.md)
- [Bounded Autonomous Investigation](docs/BOUNDED-AUTONOMOUS-INVESTIGATION.md)
- [Local WASM Investigation Analytics](docs/LOCAL-WASM-ANALYTICS.md)
- [Evidence Ingestion Pipeline](docs/EVIDENCE-INGESTION-PIPELINE.md)
- [Cross-Investigation Intelligence](docs/CROSS-INVESTIGATION-INTELLIGENCE.md)
- [Research Harness](docs/RESEARCH-HARNESS.md)
- [Investigation Workspace](docs/INVESTIGATION-WORKSPACE.md)
- [Competition Ambition Profile](docs/COMPETITION-AMBITION.md)
- [Multi-Agent Adversarial Investigation](docs/MULTI-AGENT-INVESTIGATION.md)
- [Counterfactual Branching and Sensitivity](docs/COUNTERFACTUAL-BRANCHING.md)
- [Advanced Investigation Visualizations](docs/ADVANCED-VISUALIZATIONS.md)
- [Competition Demo Choreography](docs/COMPETITION-DEMO-CHOREOGRAPHY.md)
- [Competition Alignment](docs/COMPETITION-ALIGNMENT.md)
- [Floor Judging Architecture Defense](docs/FLOOR-JUDGING-DEFENSE.md)
- [Build-Day Execution Plan](docs/BUILD-DAY-EXECUTION.md)
- [Canonical Kickoff Manifest](kickoff/indy-init.kickoff.json)
- [Kickoff Execution Packet](docs/KICKOFF-EXECUTION-PACKET.md)
- [Factory Rehearsal Protocol](docs/FACTORY-REHEARSAL.md)
- [Fresh-Agent Factory Acceptance](docs/FRESH-AGENT-FACTORY-ACCEPTANCE.md)
- [Security, Privacy, and Threat Model](docs/SECURITY-THREAT-MODEL.md)
- [Implementation Slices](docs/IMPLEMENTATION-SLICES.md)
- [Open Questions and Research Register](docs/OPEN-QUESTIONS.md)
- [Design Decision Boundaries](docs/DESIGN-DECISION-BOUNDARIES.md)
- [Investigation Acceptance Scenarios](docs/INVESTIGATION-ACCEPTANCE-SCENARIOS.md)
- [Requirements](docs/REQUIREMENTS.md)
- [Rehearsal 3 Results](docs/REHEARSAL-003-RESULTS.md) (outcome, reusable designs, lessons; machine-readable summary linked)
- [Rehearsal 4 Results](docs/REHEARSAL-004-RESULTS.md) (computed NOT ACCEPTED; key facts, reusable designs, lessons; verdict and ledger linked)
- [Rehearsal 5 Results](docs/REHEARSAL-005-RESULTS.md) (computed INCOMPLETE, NOT ACCEPTED: G0–G7 pass, 0 FAIL, 49 obligations without proving tests; findings AR-027..AR-035)
- [Causa Rehearsal Workspace Preflight](docs/CAUSA-REHEARSAL-WORKSPACE-PREFLIGHT.md) (prospective immutable-template, isolated-workspace, publication/readback and explicit timed-start workflow; requirement intake CHT-001)
- [Everyday Lightbulb Demonstration Intake](input-documents/2026-10-28-ELB-001-everyday-lightbulb-demonstration.md) (engine-authored everyday investigation, negative control and four-case combined template; prospective requirement intake ELB-001)
- [Process Evidence and the Implementation-Start Gate](docs/PROCESS-EVIDENCE-AND-READINESS.md) (`verify-process`, `plan-readiness`: process and owner evidence recorded in Praxis as work happens, verified per candidate; implementation blocked while mandatory mappings are unresolved)
- [Development Speed Optimization Report](docs/engineering-improvement/SPEED-OPTIMIZATION-REPORT.md) (Rehearsal 5 baseline, changes, measurements and Rehearsal 6 preparation)
- [Verification Plan Consumer Contract](docs/VERIFICATION-PLAN-CONTRACT.md) (`requirements-compiled/v2/verification-plan.json`, schema `indy-init.verification-plan/1` revision 2: per-obligation executable acceptance contracts; [package v2](requirements-compiled/v2/README.md))
- [Rehearsal Derivation Policy](docs/REHEARSAL-DERIVATION-POLICY.md) and the [Causa requirements baseline](requirements/README.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Hackathon Boundaries](docs/HACKATHON-BOUNDARIES.md)
- [Experiment Plan](docs/EXPERIMENT-PLAN.md)
- [Demo Plan](docs/DEMO-PLAN.md)
- [EDF Follow-on](docs/EDF-FOLLOW-ON.md)
- [UI Contract Exploration](docs/UI-CONTRACT-EXPLORATION.md)
