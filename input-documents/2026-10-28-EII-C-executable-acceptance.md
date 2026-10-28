# EII-C: Executable acceptance contracts

Priority: P0. Workstream C of the Echelon Engineering Improvement Initiative.
Owner system: Praxis (intake, requirements). Decisions belong to the operator (Ordo).
Target repository: indy-init (`tools/requirements-compiler`, `requirements-compiled/`).
Intake: Praxis document ingestion. This document is source material, not a canonical requirement.

Indy-init has no Praxis installation (no `.praxis/`). As with RCP-001, this file is in the legacy `input-documents/` path, which Praxis still reads (praxis `docs/fallback-reconciliation.md`, "Input documents"). No Praxis state is written or implied by this file.

## Problem

The compiled verification plan (`indy-init.verification-plan/1`) states test identity and freshness per requirement, not per obligation, and its checker never reads the ledger's `tests`. Two Rehearsal 5 events show the cost:

- R5 `5dcb91e` removed about 20 obligation tags from engine tests that did not fully prove the obligations (they also needed app, browser, provider or live evidence). Nothing in the plan said which kinds of evidence an obligation needs, so a related unit test could be counted as proof.
- R5 `99eb90e` shows that the Praxis preservation gate passes value and role swaps (AR-002-O2). The plan checker compares priority and obligation text only; it does not compare title, section hash, kind or value tokens, and detects swapped identities only implicitly.

A further defect: the compiled v1 package changed after the Rehearsal 4 freeze (package manifest `0cd4c3f5…` at `45c9657` to `b771cd40…` at `4b38213`) without a new package version.

## Proposed requirements

| ID | Requirement |
|---|---|
| EII-C-01 | Every obligation in the compiled plan carries: requirement id, obligation id, priority, verification method, required evidence categories (a set drawn from unit, integration, browser, live-provider, security, accessibility, independent-acceptance), test identity (bound, or explicitly unbound; never invented), dependencies, freshness requirement, verification status and missing coverage. |
| EII-C-02 | Evidence categories come from explicit, declared rules over existing requirement data (verification rules and gates, document and title, obligation kind, freshness policy), held as a reviewable data table in the repository. An obligation whose categories cannot be determined is reported as `unclassified` missing coverage, never silently assigned. |
| EII-C-03 | An obligation is verified only when every required category has fresh, integrity-checked evidence explicitly bound to that obligation id. A related passing test never verifies it. |
| EII-C-04 | The checker rejects, with distinct and actionable diagnostics, each covered by a negative-control test: a missing obligation; an incorrect test mapping (a ledger or evidence test claims an obligation it is not bound to, or a bound test is absent from the ledger); altered requirement values (title, section hash, kind, value tokens, priority, text); swapped requirement identities and swapped obligation identities; stale evidence; an unauthorized exclusion. |
| EII-C-05 | The compiler is validated against the frozen Rehearsal 4 baseline (all 108 P0 requirements and their obligations represented) and, independently, against Rehearsal 5's own committed baseline and derivation records; identity or differences are recorded with measured counts. |
| EII-C-06 | The change is a properly versioned artifact: the package is cut as `requirements-compiled/v2`; v1 stays as committed, and its post-freeze changes are recorded, not rewritten. |
| EII-C-07 | No fictional tests are generated, no existing acceptance requirement or check is weakened, and the implementation is F# in functional style (no new Python). |
| EII-C-08 | The compiler documentation explains the new plan fields, the categories table, the checker rejections and how agents consume the plan before implementation. |

## Acceptance for this intake

- Each EII-C item has a derived record that names this input in `derived_from`.
- The categories table and any test binding are owner-reviewable data; agents do not author approvals (RDP-003).
- After the change, the v2 package compiles with compilation PASS and the compiler's full test suite passes.
