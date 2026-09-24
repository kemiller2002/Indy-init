# Product Requirements: Root-Cause Investigator Platform

Status: canonical long-term product requirements.

The Indy Init hackathon is the first externally constrained implementation of this product. Competition-specific constraints are an implementation profile and must not leak into the reusable investigation kernel.

## P0. Product identity and longevity

### P0.1 Reusable product
The Root-Cause Investigator must be designed as a reusable product that can operate after the hackathon without requiring a rewrite of its core domain model.

### P0.2 Competition as a profile
Hackathon-specific timing, provenance, demo, and blank-repository constraints must remain separable from core product behavior.

### P0.3 Domain-neutral kernel
The core investigation kernel must not depend on EDF, software engineering, hackathon, or Echelon-specific vocabulary.

### P0.4 First specialization
EDF should be supportable as the first domain specialization after the generic product exists, without forking the core investigation engine.

### P0.5 No disposable shortcuts
A hackathon shortcut may be accepted only when it is explicitly recorded as a temporary deviation with a defined replacement path.

## P1. Investigation model

### P1.1 Stable investigation identity
Every investigation must have a stable identifier independent of storage location.

### P1.2 Human-readable title and observation
Every investigation must retain both a concise human-readable title and the original observation/problem statement.

### P1.3 Investigation lifecycle
Investigations must support explicit lifecycle states such as Draft, Active, AwaitingEvidence, AwaitingVerification, Resolved, Closed, Reopened, Superseded, and Archived.

### P1.4 Reopening
A resolved or closed investigation may be reopened only with an explicit reason and provenance.

### P1.5 Related investigations
Investigations must be linkable to predecessors, duplicates, regressions, related incidents, follow-ups, and superseding investigations.

### P1.6 Immutable historical meaning
Historical records must preserve the meaning they had when recorded. Later schema or interpretation changes must not silently rewrite prior facts.

## P2. Observations and claims

### P2.1 Observation is not inference
Direct observation, reported observation, interpretation, hypothesis, assumption, and conclusion must be distinct concepts.

### P2.2 Source preservation
The exact source statement from a user or imported artifact must be retained when the system creates a normalized or structured projection.

### P2.3 Claims
The system must represent material claims explicitly so evidence can support, contradict, qualify, or leave them unresolved.

### P2.4 Claim scope
Claims should identify the subject, applicable time or version range, and context when those dimensions materially affect meaning.

## P3. Hypotheses

### P3.1 Multiple competing hypotheses
The system must support multiple simultaneously plausible explanations.

### P3.2 Explicit lifecycle
Hypotheses must have explicit state and transition history.

### P3.3 No silent deletion
Rejected, weakened, falsified, duplicate, or superseded hypotheses must remain in history.

### P3.4 Reopening
A falsified or rejected hypothesis may be reopened only when new evidence or a corrected premise is explicitly linked.

### P3.5 Dependencies
A hypothesis may depend on other claims or hypotheses, and those dependencies must be visible.

### P3.6 Scope
The system must distinguish a proposed root cause from a contributing factor, triggering condition, enabling condition, symptom, and unrelated coincidence.

## P4. Evidence

### P4.1 First-class evidence
Evidence must be represented as first-class records rather than free-form notes only.

### P4.2 Provenance
Evidence should record source, author or origin when known, capture time, applicable system/version, and verification status.

### P4.3 Evidence type
The model should distinguish at least direct observation, measurement, log, document, experiment result, testimony/report, external reference, derived calculation, and AI-generated interpretation.

### P4.4 Evidence quality
Evidence may carry structured quality metadata such as reliability, completeness, precision, freshness, directness, and independence.

### P4.5 Evidence relationships
Evidence must be linkable to one or more claims or hypotheses with explicit relationship semantics.

### P4.6 Contradiction
Conflicting evidence must be retained and surfaced rather than resolved by overwriting one record.

### P4.7 Dependency
The system should record when multiple evidence items derive from the same underlying source so correlated evidence is not falsely treated as independent confirmation.

### P4.8 Supersession
Evidence may be superseded or corrected without disappearing from history.

### P4.9 External artifacts
Large or binary artifacts may be referenced externally while preserving metadata, integrity information where practical, and source location.

### P4.10 Chain of custody
Domains that require it must be able to record who captured, transformed, or verified evidence.

## P5. Unknowns, assumptions, and negative knowledge

### P5.1 Unknown is first-class
Unknown must be represented explicitly and must not collapse into false, missing, or unsupported.

### P5.2 Assumptions
Assumptions must be explicit and distinguishable from facts and evidence.

### P5.3 Assumption risk
An assumption may be blocking, non-blocking, accepted, rejected, or scheduled for verification.

### P5.4 Negative knowledge
Known false, ruled-out, unsupported, or eliminated explanations must be preserved.

### P5.5 Repetition prevention
Negative knowledge should prevent repeated pursuit of disproven paths unless new evidence changes the state.

### P5.6 Expiry
Unknowns, assumptions, and negative knowledge may have scope or expiry when their validity depends on version, environment, or time.

## P6. Experiments, checks, and questions

### P6.1 Unified investigation action
A test, inspection, query, measurement, interview question, comparison, simulation, or experiment must be representable as an investigation action.

### P6.2 Purpose
Every proposed action should identify what uncertainty, claim, or competing hypotheses it is intended to resolve.

### P6.3 Prediction before observation
Where meaningful, expected outcomes under competing hypotheses should be recorded before results are known.

### P6.4 Discrimination
The system should prefer actions that distinguish among plausible alternatives rather than only accumulating confirmatory evidence.

### P6.5 Cost and risk
An action may record cost, time, reversibility, operational risk, and required permissions.

### P6.6 Expected information value
The system should be able to rank or explain candidate next actions by expected discriminating value, cost, urgency, and risk without pretending to have unjustified numeric precision.

### P6.7 Result preservation
Inputs, conditions, expected outcomes, actual findings, interpretation, and anomalies must be retained.

### P6.8 Reproducibility
Where applicable, an experiment should preserve enough procedure and environment information to be repeated.

## P7. Causality and conclusion quality

### P7.1 Correlation is insufficient
The system must not treat temporal sequence, correlation, or plausibility alone as proof of causation.

### P7.2 Alternative explanations
Before confirming a root cause, materially plausible alternative explanations must either be addressed, falsified, or explicitly retained as residual uncertainty.

### P7.3 Counterfactual reasoning
Where practical, the investigation should record what would be expected if the proposed cause were absent or changed.

### P7.4 Mechanism
A confirmed root cause should include a defensible mechanism connecting the cause to the observed outcome when the domain permits one.

### P7.5 Contradictory evidence
Unresolved contradictory evidence must block confirmation when policy marks it as material.

### P7.6 Confidence semantics
Any confidence representation must be derived from explicit evidence, unresolved uncertainty, and policy. It must not be an unexplained LLM probability.

### P7.7 Residual uncertainty
A conclusion must preserve what remains unknown or weakly supported.

### P7.8 Conclusion status
The system should distinguish plausible explanation, leading explanation, supported cause, confirmed cause, and accepted-with-residual-uncertainty when domain policy requires those distinctions.

## P8. Corrective action and verification

### P8.1 Distinct concepts
Mitigation, workaround, corrective action, preventive action, and root-cause correction must be distinguishable.

### P8.2 Action ownership
Corrective actions may record owner, due date, dependencies, status, and verification responsibility.

### P8.3 Verification criteria
A corrective action must define observable success criteria before it can be considered verified.

### P8.4 Verification evidence
Completion of work is not verification. Evidence must show whether the intended outcome occurred.

### P8.5 Regression monitoring
A resolved investigation may define follow-up observation criteria or a monitoring window.

### P8.6 Failed correction
If a correction fails verification, the investigation must be able to return to an active state without losing the failed attempt.

## P9. Investigation strategy and obligations

### P9.1 Explicit obligations
Missing work that blocks progress must be represented as obligations.

### P9.2 Priority
Obligations may be prioritized by blocking impact, urgency, expected information value, cost, or risk.

### P9.3 Stop conditions
An investigation may define stopping conditions for insufficient value, unacceptable cost, safety limits, or accepted residual uncertainty.

### P9.4 No false completion
An investigation may not enter a completion state while blocking obligations remain unresolved unless the user explicitly accepts the residual uncertainty under an allowed policy.

### P9.5 Decision trace
The reason an investigation advanced, paused, accepted uncertainty, or stopped must remain inspectable.

## P10. Domain packs

### P10.1 Extensibility without fork
Domain-specific behavior must be addable without forking the generic investigation kernel.

### P10.2 Domain vocabulary
A domain pack may define terminology, labels, evidence categories, hypothesis categories, experiment templates, report sections, and help content.

### P10.3 Domain rules
A domain pack may add invariants, legal transitions, completion policies, required evidence, and verification rules.

### P10.4 Domain UI extensions
A domain pack may add domain-specific projections and fields while preserving core accessibility and state semantics.

### P10.5 Domain AI guidance
A domain pack may add domain-specific AI instructions and tools, but those instructions may not override core safety, provenance, or state-integrity rules.

### P10.6 Versioning
Every domain pack must be versioned and its version recorded with affected investigations.

### P10.7 Migration
Domain-pack upgrades that change persisted meaning must provide an explicit migration or compatibility strategy.

### P10.8 EDF pack
The EDF specialization should model experiments, assumptions, regressions, metrics, unknown effects, negative knowledge, and evidence dependencies using the generic extension mechanism.

## P11. Templates and playbooks

### P11.1 Investigation templates
Users must be able to start from reusable templates without losing the ability to deviate as evidence demands.

### P11.2 Playbooks
A playbook may suggest domain-appropriate questions, checks, evidence sources, or transitions.

### P11.3 No forced narrative
A template or playbook must not force evidence into a predetermined cause.

### P11.4 Organization templates
Organizations should be able to define their own approved templates and policies.

## P12. Workspace and portfolio

### P12.1 Multiple investigations
The product must manage many investigations, not only one active case.

### P12.2 Workspaces
Investigations should be groupable into workspaces, projects, clients, products, systems, or other configured scopes.

### P12.3 Search
Users must be able to search investigations, hypotheses, evidence, unknowns, actions, and conclusions.

### P12.4 Filtering
Users should be able to filter by lifecycle state, domain, owner, date, tag, unresolved obligation, and related attributes.

### P12.5 Cross-investigation learning
The system may surface related prior investigations, known disproven explanations, recurring causes, and reusable experiments.

### P12.6 Context boundary
Cross-investigation reuse must preserve workspace, permission, and domain boundaries so unrelated or unauthorized information is not mixed.

### P12.7 Duplicate detection
The system should be able to suggest possible duplicate or related investigations without automatically merging them.

## P13. Collaboration and review

### P13.1 Authorship
Material changes must preserve author or actor provenance when identity is available.

### P13.2 Concurrent work
The product must prevent silent overwrites when multiple users or agents modify the same investigation.

### P13.3 Review workflow
Organizations may require review or approval before a root cause, corrective action, or final report becomes authoritative.

### P13.4 Comments and discussion
Users should be able to discuss evidence, hypotheses, experiments, and conclusions without turning comments into authoritative facts automatically.

### P13.5 Assignment
Investigations, obligations, experiments, and corrective actions may be assigned.

### P13.6 Agent identity
AI-generated proposals must record provider/model/runtime identity and remain distinguishable from human-authored state.

## P14. Persistence and portability

### P14.1 Storage abstraction
The investigation domain must not depend directly on GitHub-specific storage semantics.

### P14.2 GitHub adapter
GitHub must be supported as an auditable storage provider.

### P14.3 Additional providers
The architecture must allow future file, database, service, or customer-hosted persistence providers.

### P14.4 Canonical format
A documented, versioned canonical serialization must exist for portable investigation data.

### P14.5 Import/export
Users must be able to export a complete investigation with enough metadata to re-import it.

### P14.6 Human readability
At least one supported export/storage representation should remain reasonably human-readable.

### P14.7 Version metadata
Persisted state must record schema version, domain-pack version, and relevant policy versions.

### P14.8 Migration
Schema migrations must be explicit, testable, and recoverable.

### P14.9 Backup and restore
Long-term deployments must have a documented backup and restore path appropriate to the selected storage provider.

## P15. Integration surface

### P15.1 Integration boundary
External systems must interact through explicit adapters or integration contracts rather than reaching into internal state arbitrarily.

### P15.2 Import adapters
The platform should support adapters for importing evidence or observations from external systems.

### P15.3 Event publication
Material state changes should be publishable as structured integration events.

### P15.4 Idempotency
Integration operations that may be retried must support idempotent behavior where practical.

### P15.5 Provenance
Imported data must preserve its external origin.

### P15.6 No trust escalation
Imported content is data, not authority. It must not redefine system policy, agent instructions, or transition rules.

## P16. AI architecture

### P16.1 Provider independence
The product must not hard-code its domain model to one AI provider or model.

### P16.2 AI as proposer
AI may propose hypotheses, questions, experiments, relationships, interpretations, and actions, but authoritative mutation must pass domain validation.

### P16.3 Inspectable proposal
The user must be able to inspect what an AI proposes before consequential state changes occur when policy requires review.

### P16.4 Prompt and model provenance
Material AI-generated proposals should retain model/provider and prompt-policy version metadata sufficient for audit.

### P16.5 Tool provenance
When AI uses tools or external data, the resulting proposal should retain relevant source/tool provenance.

### P16.6 Cost accounting
The platform should be able to record AI usage and cost by investigation, action, or workflow when the provider exposes it.

### P16.7 Failure tolerance
AI unavailability must not corrupt investigation state or make existing evidence inaccessible.

### P16.8 Deterministic domain rules
Legal transitions, invariants, completion rules, and persistence integrity must not depend solely on probabilistic model output.

## P17. Security, privacy, and governance

### P17.1 Least privilege
Every integration must request the minimum practical permissions.

### P17.2 Secrets
Secrets and authentication material must not be persisted in investigation records.

### P17.3 Data classification
Deployments should be able to classify investigations or evidence by sensitivity.

### P17.4 Access boundaries
Workspace and investigation access must be enforceable independently of UI visibility.

### P17.5 Audit trail
Security-sensitive and authoritative state changes must be auditable.

### P17.6 Retention
Organizations should be able to define retention and archival policies.

### P17.7 Deletion
When policy permits deletion, the system must distinguish deletion from historical falsification or ordinary archival and record the action as required by the storage model.

### P17.8 Redaction
Authorized users should be able to redact sensitive material while preserving an audit indication that redaction occurred when required.

## P18. User experience

### P18.1 Conversation is not the product
Natural language may be the easiest input method, but the authoritative investigation must remain inspectable as structured state.

### P18.2 No state-machine expertise required
Users must understand what is known, disputed, unknown, blocked, and next without learning Ordo terminology.

### P18.3 Explain unavailable actions
An unavailable consequential action must explain why it is unavailable and, where possible, what resolves the blocker.

### P18.4 Preserve comparison
The UI must make competing hypotheses and discriminating evidence easy to compare.

### P18.5 Progressive disclosure
Detailed provenance and history may be progressively disclosed, but blocking uncertainty and contradictions must not be hidden.

### P18.6 Recovery
Unsaved, pending, conflicted, or failed persistence state must be visible and recoverable.

### P18.7 Multi-device
The architecture should support use on tablet, phone, and desktop without changing investigation semantics.

## P19. Accessibility and internationalization

### P19.1 Accessibility baseline
The application must support keyboard navigation, screen readers, sufficient contrast, visible focus, text scaling, semantic structure, reduced motion, and non-color-only status representation.

### P19.2 High zoom and reflow
Critical investigation tasks must remain usable under high text zoom and narrow viewport reflow.

### P19.3 Localization readiness
User-facing text, date/time, number formatting, and domain labels should be structured so localization can be added without rewriting core logic.

### P19.4 Accessible reports
Generated reports must preserve accessible document structure where the output format supports it.

## P20. Reporting and sharing

### P20.1 Interim and final reports
The system must support both interim reports with unresolved work and final reports.

### P20.2 Snapshot semantics
A report must identify the investigation version or state snapshot from which it was produced.

### P20.3 Evidence traceability
Material conclusions in a report should be traceable back to supporting evidence and unresolved uncertainty.

### P20.4 Versioned reports
Regenerated reports must not erase the existence of earlier issued versions when audit requirements apply.

### P20.5 Domain report sections
Domain packs may add report sections without replacing required core provenance and uncertainty sections.

### P20.6 Folio
Folio should be the preferred Echelon report rendering system where appropriate.

## P21. Observability and operability

### P21.1 Health
Deployments must expose enough health information to distinguish application, AI, persistence, and integration failures.

### P21.2 Structured faults
Operational and domain faults must use structured fault representations compatible with Aegis.

### P21.3 Correlation
A user-visible failure should be traceable to diagnostic events without exposing secrets.

### P21.4 Metrics
The product should capture useful metrics such as investigation volume, time in state, unresolved obligation age, sync failures, AI usage, and verification completion.

### P21.5 Audit versus telemetry
Product telemetry must be distinct from authoritative investigation history.

### P21.6 Recovery procedures
Operational recovery for interrupted writes, corrupt local cache, failed migrations, and unavailable integrations must be documented and tested.

## P22. Performance and scale

### P22.1 Large investigation
The UI and state model must remain usable when an investigation contains hundreds or thousands of evidence and event records.

### P22.2 Incremental loading
History and secondary artifacts should support incremental loading so active work is not blocked by loading the entire archive.

### P22.3 Search scale
Portfolio search must not require opening every investigation client-side.

### P22.4 Bounded AI context
The AI layer must build scoped context from authoritative state rather than blindly sending an entire long investigation to a model.

### P22.5 Derived projections
Expensive derived views may be cached or projected, but they must remain reconstructable from authoritative data.

## P23. Versioning and compatibility

### P23.1 Schema version
Every persisted artifact must identify the schema version required to interpret it.

### P23.2 Forward incompatibility
A client must not silently write data it cannot fully understand.

### P23.3 Backward reading
Where practical, newer clients should read older investigations through explicit migrations or compatibility projections.

### P23.4 Policy versioning
Changes to confirmation rules, evidence requirements, or domain policies must be versioned.

### P23.5 Reproducible historical decision
The product should preserve enough version metadata to explain why a transition was legal under the policy in force at the time.

## P24. Extension contracts

### P24.1 Explicit extension types
Supported extensions should have explicit contracts, including domain packs, persistence providers, AI providers, evidence importers, report projections, and integration publishers.

### P24.2 Isolation
An extension must not be allowed to corrupt authoritative state when it fails.

### P24.3 Capability declaration
Extensions must declare the capabilities and permissions they require.

### P24.4 Compatibility declaration
Extensions must declare compatible core versions.

### P24.5 Diagnostics
Extension failures must be attributable to the extension and surfaced through structured faults.

## P25. Quality and completion

### P25.1 Traceability
Every implemented product requirement must be traceable to acceptance evidence.

### P25.2 Tests
Core domain transitions, invariants, persistence conflict behavior, migrations, and security boundaries require deterministic tests.

### P25.3 UI contracts
Consequential screens require application-specific UI contracts that verify product meaning, not only component presence.

### P25.4 Accessibility evidence
Accessibility requirements require automated and human-verifiable evidence appropriate to the feature.

### P25.5 Agent claims are not evidence
An AI or coding agent reporting that work is complete is not sufficient completion evidence.

### P25.6 Praxis
Praxis should track code metrics, thresholds, and repeated-change/churn evidence across product evolution.

### P25.7 Regression
A previously satisfied critical requirement that loses its evidence must be treated as a regression, not silently remain complete.

## P26. Deployment models

### P26.1 Static-capable client
The client should remain compatible with static hosting when authentication and AI architecture permit it.

### P26.2 Service-backed mode
The architecture must allow a service-backed deployment for stronger authentication, collaboration, search, persistence, or organizational controls.

### P26.3 Self-hosting
A future self-hosted deployment must be possible without changing investigation semantics.

### P26.4 Managed service
A future managed-service deployment may add operational convenience but must use the same canonical domain rules and portable data model.

## P27. Product success criteria

The long-term product is successful when:

1. a user can start with an unstructured problem statement;
2. the system creates and maintains explicit investigation state;
3. competing explanations remain visible;
4. evidence provenance and contradictions are inspectable;
5. the system asks for or proposes discriminating evidence rather than merely producing plausible prose;
6. unsupported causal conclusions are blocked or clearly qualified;
7. corrective action is separated from verification;
8. investigation history remains defensible after the fact;
9. the same core can support different domains without forks;
10. data can move between supported storage/deployment models;
11. users remain in control of consequential state changes;
12. the product remains useful after the hackathon constraints are removed.


## P28. Semantic conformance

### P28.1 Canonical semantics
The authoritative meanings of observation, claim, interpretation, hypothesis, assumption, evidence, prediction, finding, unknown, negative knowledge, contradiction, obligation, causal mechanism, cause roles, corrective action, verification, and conclusion must conform to [INVESTIGATION-SEMANTICS.md](INVESTIGATION-SEMANTICS.md).

### P28.2 Semantic invariants
Implementation must enforce the generic investigation invariants defined in the canonical semantics rather than relying on UI convention or agent prompting.

### P28.3 Transition legality
Hypothesis, unknown, assumption, contradiction, action, corrective-action, and investigation lifecycle transitions must be validated by explicit domain rules.

### P28.4 Cause confirmation gate
A cause may not enter ConfirmedCause unless the active confirmation policy's required evidence, contradiction, unknown, assumption, alternative-explanation, mechanism, provenance, and invariant conditions are satisfied.

### P28.5 Resolution gate
An investigation may not enter Resolved unless the active resolution policy is satisfied, including required verification and blocking-obligation conditions.

### P28.6 Semantic acceptance scenarios
The scenarios in [INVESTIGATION-ACCEPTANCE-SCENARIOS.md](INVESTIGATION-ACCEPTANCE-SCENARIOS.md) are normative acceptance behavior and should become executable tests where practical.

### P28.7 No narrative override
Neither a human-authored narrative nor an AI-generated narrative may override contradictory authoritative state, blocking obligations, or illegal transition rules.

### P28.8 Domain-pack compatibility
Domain packs may strengthen domain semantics and policies but may not violate core evidence/inference separation, provenance, unknown semantics, proposal/authority separation, or historical integrity.
