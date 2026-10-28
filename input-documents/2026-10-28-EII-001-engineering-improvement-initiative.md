# EII-001: Echelon Engineering Improvement Initiative — engineering requirements

Priority: P0 for the next rehearsal's engineering stack; no change to Causa product requirements.
Owner system: Praxis (intake, work items). Decisions belong to the operator (Ordo).
Target repositories: limen, dokimos (A); praxis (B); indy-init (C); arca (D); conditor (E); forma (F); ordo, dokimos (G); indy-init, ordo (H).
Intake: Praxis document ingestion. This document is source material, not a canonical requirement.

Indy-init is not Praxis-initialized (no `.praxis/`), so this file is in the
legacy `input-documents/` path that Praxis still reads (praxis
`docs/fallback-reconciliation.md`, "Input documents"). Each target repository
that is Praxis-initialized receives its own workstream requirement in
`.praxis/inbox/documents/` and processes it with its own work protocol.

Evidence: [`docs/engineering-improvement/BASELINE-2026-10-28.md`](../docs/engineering-improvement/BASELINE-2026-10-28.md).
Experiment protocol: [`docs/engineering-improvement/EXPERIMENT-PROTOCOL.md`](../docs/engineering-improvement/EXPERIMENT-PROTOCOL.md).

## Constraints that apply to every item

- Rehearsal 5 (`indy-init-rehearsal-005`) is an active experiment and is not
  modified: no code, requirements, acceptance criteria, CI or agent changes.
  Adoption by a rehearsal happens only through a future rehearsal's pinned stack.
- F# for new executable implementation; no new Python; no new JavaScript
  application logic; no new orchestration framework; no GitHub Issues.
- No weakening of acceptance requirements; no fabricated evidence; no
  unauthorized dependency upgrades.

## Owner decisions given in the initiative session (2026-10-28)

These were answered by the repository owner in the session that ran the
initiative. If an authenticated form is required (AR-003), they must be
confirmed in it; agents do not write approvals.

| ID | Decision |
|---|---|
| EII-DEC-001 | A breaking Arca minor release is approved to make synchronized state and commit receipts unforgeable outside Arca (Workstream D). |
| EII-DEC-002 | Ordo freshness bindings become policy-aware: deterministic-input-digest evidence binds to integrity, input digest and plan/rehearsal identity, not run id; live and review evidence keep run id and source commit (Workstream G). |
| EII-DEC-003 | Experiments are limited to retrospective replays (reported as non-causal) plus a pre-registered protocol for future controlled runs (Workstream H). |

## Requirements

| ID | Workstream | Requirement | Baseline finding |
|---|---|---|---|
| EII-001-A1 | A | Detect, before browser deployment, compiled methods that exceed the .NET WebAssembly interpreter's per-method limit, or provide a fast runtime start probe that reproduces it. Report false positives. No source-line heuristics. | 1, 2 |
| EII-001-B1 | B | A machine-readable stream contract (identity, owned paths, shared interfaces, dependencies, required tests, merge readiness, expected outputs) and a pre-merge check for ownership, cross-stream overlap and dependency order. | 3, 15 |
| EII-001-C1 | C | The compiled verification plan carries, per obligation, priority, method, required evidence categories, test identity, dependencies, freshness, status and missing coverage; the checker rejects missing obligations, wrong test mappings, altered values, swapped identities, stale evidence and unauthorized exclusions. | 4, 5, 6 |
| EII-001-D1 | D | Application code cannot declare data persisted or synchronized without an authoritative provider result; invalid transitions are unrepresentable or rejected; remaining runtime-only risks are documented. | 7, 8 |
| EII-001-E1 | E | One authoritative test-environment contract and a preflight that fails in seconds on port, endpoint, fixture, build configuration, browser, stale artifact and undecodable-path faults. | 9, 10 |
| EII-001-F1 | F | Forma typography roles for marketing, application, dense administrative and accessible large-text interfaces using existing tokens; layout failures reproduced before and prevented after. | 11 |
| EII-001-G1 | G | Policy-aware evidence freshness; passing and failing outcomes recorded independently in the existing evidence store; no invalid reuse. | 12, 13, 14 |
| EII-001-H1 | H | Frozen baselines and requirement identities are preserved; comparisons follow the pre-registered protocol; missing measurements are reported as unavailable. | 6, 16 |
