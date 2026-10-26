# RCP-001: Compiled requirements package v1 and requirement changes it proposes

Priority: P0. The next rehearsal and the event depend on it.
Owner system: Praxis (intake, requirements). Decisions belong to the operator (Ordo).
Target repositories: indy-init (requirements), causa-data (storage contract), echelon-registry and praxis (releases).
Intake: Praxis document ingestion. This document is source material, not a canonical requirement.

Every item below is **proposed**. Nothing here is approved, and no requirement is changed by this document. The authoritative requirements are unmodified (`docs/`, `requirements/causa-requirements-baseline.json`).

Source package: `requirements-compiled/v1/`, compiled from indy-init `598a759b597e05d59e60bfbeb608c66d3945118b`:
- compilation PASS;
- rehearsal readiness BLOCKED.

The finding ids below (F-…) refer to `requirements-compiled/v1/curation/findings.json`. Each finding cites its sources verbatim at the recorded commits.

## Intake handling

- Claim this file with `praxis inbox claim`.
- Derive one record per RCP item with `praxis inbox derive --kind requirement|decision|question`.
- An RCP that needs an owner decision becomes a decision request. It never becomes an approval: agents do not write approvals (RDP-003).
- Indy-init is not yet Praxis-initialized (no `.praxis/`). This file is in the legacy `input-documents/` path, which Praxis still reads (praxis `docs/fallback-reconciliation.md`, "Input documents").

## Proposed items

| ID | Kind | Proposal | Source |
|---|---|---|---|
| RCP-001-01 | decision | Re-pin `requirements/causa-requirements-baseline.json` at the kickoff commit (RDP-001), or record an owner decision to stay on 002d8ba. The committed baseline lacks AR-019..AR-026 and two changed ledger sections. | F-003 |
| RCP-001-02 | decision | Resolve the launch-content conflict. UI-001 and UI-015 require "all five frozen demonstration investigations" on launch. EXT-DATA-001/002, OD-INDY-3 and the active-catalog policy require an empty first launch and three historical cases. | F-001 |
| RCP-001-03 | decision | State which DF-CAUSA-2026-0003 answers apply to Rehearsal 4 and the event: OD-INDY-3/4 of 2026-10-24, or the Rehearsal 3 approvals of 2026-10-25. Confirm that the 2026-10-25 approvals are Rehearsal-3-scoped. | F-002 |
| RCP-001-04 | requirement | Version the causa storage contract (STORAGE-003) so that the conforming GitHub publication is an atomic, head-guarded commit (GraphQL `createCommitOnBranch` with `expectedHeadOid`, per the Rehearsal 2 owner decision), or record an exception. Section 9.4.1 currently mandates the REST ref update that Rehearsal 2 proved cannot compare-and-swap. | F-009 |
| RCP-001-05 | requirement | Add a Causa requirement: after Connect, the active catalog `historical-v1` (Apollo 1, Apollo 11, Challenger STS-51-L) loads externally and replays to its manifest states. Alternatively, record it as demonstration content outside acceptance. Today it exists only in causa-data and is not preservation-gated. | F-006 |
| RCP-001-06 | decision | Decide whether the AI proposal loop and time travel (kickoff packet checkpoints 12:45 and 2:45; "AI proposal authority boundary" never cut) are in scope for Rehearsal 4 and the event. | F-008 |
| RCP-001-07 | decision | Choose the browser application's authentication mechanism for Connect (for example, a fine-grained token held in memory, or a GitHub App user token). Decide whether reading a public catalog needs a credential. | F-019, F-012 |
| RCP-001-08 | decision | Decide whether R001-derived interface manifests may be carried into the blank event repository, with organizer confirmation. | F-013 |
| RCP-001-09 | requirement | Add the Rehearsal 4 plan, and the R3-DV rules it inherits, to the baseline scope for Rehearsal 4, so that R4-01..R4-07 are preservation-gated. | F-018 |
| RCP-001-10 | requirement | Give a P0/P1 priority to every requirement that has none: REQUIREMENTS R0.1..R16.5, STORAGE, DCC, ADRP, DEP-CURRENT, WASM-LOAD and SCREEN-CONTRACTS. Make the baseline generator parse qualified markers such as UI-037's "[P0, competition profile]". | F-010, F-011 |
| RCP-001-11 | upstream | Praxis: release the hardened preservation gate (REQ-PRES-001). Registry: register percepta-semantic, assess folio 0.4.1, and cut and qualify echelon-current 1.28.0. | F-004, F-005, F-015 |
| RCP-001-12 | requirement | Every rehearsal recompiles this package at its pinned commit, and does not start the timer until the compilation passes and readiness is READY or every blocker carries an owner decision. | package README |
| RCP-001-13 | maintenance | Update the stale status lines of REHEARSAL-003-DEFERRED-VERIFICATION-EXPERIMENT.md and NEXT-REHEARSAL-PREFLIGHT-STATUS.md. | F-014 |

## Acceptance for this intake

- Each RCP item has a derived record that names this input in `derived_from`.
- Every item of kind `decision` has an owner decision recorded outside agent authorship.
- After any requirement edit, the baseline is regenerated and the package recompiles with compilation PASS.
