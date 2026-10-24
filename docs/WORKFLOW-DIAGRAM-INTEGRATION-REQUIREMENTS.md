# Workflow Layout Integration and Causal Diagram Requirements

Status: normative for future Causa rehearsals and implementations that display causal relationships.
Authority: Indy-init. These requirements supplement the Causa UI, screen, provider, and acceptance contracts.

## Design rule

Causa must compose the **existing Echelon workflow layout capability** rather than implement a new hardcoded graph layout engine. First discover and verify the actual owning repository, public API, supported rendering model, version, and availability. Do not assume the layout engine lives in Forma Studio, and do not invent an API. If the capability is unavailable or lacks a required contract, record the gap and propose work in its owning repository through the canonical Praxis input process.

## Requirements

**WDL-001 [P0] Existing capability discovery.** Identify the workflow layout engine's owning repository, supported package or source integration, API, version, license, and constraints before implementation. Document evidence of a working example and identify any required upstream changes.

**WDL-002 [P0] No duplicate engine.** Causa must not introduce a second general-purpose graph layout implementation or maintain fixed SVG coordinates for domain nodes and edges when the existing workflow engine supports them. An explicitly documented temporary fallback requires approval and cannot be represented as satisfying this requirement.

**WDL-003 [P0] Typed domain-to-graph projection.** Define a typed F# adapter that projects authoritative causal assertions, outcomes, hypotheses, cause roles (root cause, trigger, contributing factor, enabling condition), and evidence counts into graph nodes and relationships. The projection must not invent causal claims, change domain states, or perform domain inference.

**WDL-004 [P0] Separation of concerns.** Causa owns graph semantics; the reusable workflow system owns placement, routing, spacing, and supported layout constraints; Forma owns accessible visual components and semantic styling. Keep these responsibilities independently testable.

**WDL-005 [P0] Data-driven layout.** Node placement and edge geometry must be computed from graph input and supported constraints, not from hardcoded coordinates for specific investigations. Layout must remain valid for zero, one, many, disconnected, and cyclic graphs where the domain permits them.

**WDL-006 [P0] Visual correctness.** Layout must avoid unintended node overlaps, clipped labels, inaccessible connectors, and hidden evidence counts. Edge crossings and visual ambiguity should be minimized using documented layout capabilities; do not promise zero crossings for arbitrary graphs.

**WDL-007 [P0] Responsive and accessible rendering.** Support desktop, iPad portrait/landscape, and mobile with accessible text equivalents, meaningful labels, keyboard navigation/focus, adequate contrast, zoom/reflow or intentional navigable overflow, and reduced-motion preferences. Diagram information must not depend on color alone.

**WDL-008 [P0] Forma design-system alignment.** Render graph nodes and statuses using Forma-compatible components/tokens and the canonical Causa UI-016 semantic palette. Colors must reflect domain-authoritative states; layout cannot assign causal or evidentiary meaning.

**WDL-009 [P0] Stable and deterministic output.** Given identical graph data, layout engine version, viewport constraints, and configuration, output must be deterministic within documented tolerances. Stable node IDs and ordering must survive refreshes; avoid needless visual movement.

**WDL-010 [P0] Interaction contract.** Where the workflow component supports interaction, node selection and navigation must resolve to authoritative investigation records. Read-only rehearsal scope must not expose editing, drag-to-modify-domain, or persistence capabilities without explicit authorization.

**WDL-011 [P0] Fixture-backed development.** Include deterministic graph fixtures: empty, single-node, four cause roles, dense evidence, long labels, disconnected graph, and permitted cycles. The fixture provider must use the same domain-to-graph projection as the real GitHub provider (PF-001..PF-021).

**WDL-012 [P0] Layered tests.** Independently test graph projection, layout invariants, rendering/accessibility, and live integration. Verify expected nodes, roles, relationships, evidence counts, no unintended overlap or clipping, stable output, keyboard/text-equivalent access, and real historical investigation rendering.

**WDL-013 [P1] Comparative experiment.** Compare the existing fixed-coordinate SVG approach against the reusable workflow layout system on identical causal graphs. Record layout correctness, overlap, clipping, edge crossings, responsiveness, implementation effort, test effort, and agent token consumption. Hold graph data and acceptance criteria constant; preserve original measurements and distinguish observed results from estimates.

**WDL-014 [P0] Integration gate.** Before claiming completion, prove the application actually invokes the identified workflow layout capability, records the pinned dependency version/SHA, and passes fixture, accessibility, and deployed-browser tests. A visually similar hand-coded diagram is not equivalent.

**WDL-015 [P0] Requirements preservation.** Every future rehearsal requirements package must trace WDL-001..WDL-015 to implementation and verification or record an explicitly approved scope exclusion. Do not silently omit these obligations during derivation.

## Acceptance scenarios

1. Load a populated causal investigation offline; render through the actual Echelon workflow layout system without GitHub access.
2. Change graph membership or long-label content; placement adjusts without Causa-specific coordinate edits.
3. Render the same graph twice with the same engine/version/viewport; positions are stable within documented tolerances.
4. Verify that every causal assertion and evidence count in the graph matches the F# domain projection.
5. Exercise desktop, iPad portrait/landscape, mobile, keyboard, zoom, and text equivalent.
6. Verify that real GitHub-backed investigations use the same graph projection and layout integration.
7. Inspect the dependency and runtime evidence to confirm the existing engine was used, not reimplemented.
8. Run the controlled comparison and publish measured results, including token and implementation costs.

## Rehearsal preparation

Before the next rehearsal, resolve the workflow layout engine's true owner and public contract. Add any upstream implementation requirements to the owning repository's Praxis input directory, using its documented canonical path, not GitHub Issues. Reconcile this document with the rehearsal baseline and approved scope before implementation.
