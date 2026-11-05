# EXP-INDY-002 plan: contract-first F#, run as contract-triggered parallel development

**Status:** timed run in progress. An engineering experiment, not a rehearsal. Experimental application code lives only in
`experiments/exp-indy-002/`; it is never merged into a competition implementation and copies nothing from earlier
rehearsals or `preview/causa` (OD-INDY-22).

## Starting point (preparation, measured, before T+0)

| Item | Value |
|---|---|
| Starting commit | `d4fdef91654d1dde113fbf56ea1c68b331fc81d1` (main, "Workstream B: competition UI ownership map (#134)") |
| Branch | `claude/exp-indy-002-contract-first-hhlktz`. The directive names `experiment/exp-indy-002`; the session is restricted to this branch name, so the experiment runs here (deviation DEV-0). |
| Toolchain | .NET SDK 10.0.402 (global.json, `rollForward: disable`), installed in preparation; Node 22.23.4; Limen 0.9.1 and Forma 0.10.0 pinned as in `preview/causa/web/package.json`; Chromium + Playwright preinstalled |
| Machine | 4 vCPU, 15 GB RAM, one cloud container |
| Agent capacity | 1 coordinator/verification agent (this session) + 2 worker sub-agents in isolated git worktrees (domain, UI) |
| Baseline build | cold `preview/causa` WebAssembly publish: 53 s (measured; used only as a toolchain baseline, no code reused) |
| Baseline CI | push to this branch triggers no workflow; the pull request triggers `Preflight tooling` and `Indy Init offline factory rehearsal`. Local offline-suite timing recorded in CI-RUNS.json |
| T+0 | **2026-11-05T21:15:04Z** (TIMELINE.jsonl). Deadline T+90 = 22:45:04Z |

## Protocol change (deviation DEV-1)

The owner's modification ("asynchronous contract-driven development") arrived at about T+2.5, during Phase A, after
`Contracts.fs` and the registry had been written but before anything was validated or implementation began. Per its
section 11 the clock was **not reset** and no evidence was overwritten. The run continues under the modified protocol
from that point. Consequence for interpretation: all contracts had already been *defined* in one piece before the change,
so the incremental-release element is exercised only partly (groups were validated and released one after another in
about 3 minutes, not interleaved with implementation over 50 minutes).

## Slice (37 ratified catalog requirements)

Selected from `CAT-CAUSA-INVESTIGATION` (ratified, not a frozen package; no v4.1.5 dependency):

| Group | Requirements | Content |
|---|---|---|
| CG-1-START | INV-001 002 004 005 006 019 020 021 022 023 | start an investigation: fields, validation rules, start/cancel, destination indicator |
| CG-2-HEADER | INV-007 008 018 034 035 036 | header: created, problem statement, state; edit details; identity and history; editor cancel |
| CG-3-CONTENT | INV-009 010 011 012 013 014 024 025 026 027 028 029 | observations, hypotheses, evidence: record, edit, list |
| CG-4-RELATIONSHIP | INV-030 031 038 044 | evidence–hypothesis link, kind, rationale, revise/withdraw (the state transition Current → Revised/Withdrawn) |
| CG-5-PERSISTENCE | INV-015 016 017 039 040 | save, seven-state save indicator, reopen restores, list, open |

**Excluded, with reason:** INV-003 (only when a domain profile is selected; no profile in the slice); INV-032/033
(conclusions: scope); INV-037 (needs a real GitHub provider write, not authorized in this session); INV-041/042/043
(judged by owner journeys J2/J3 that only run from the governed checkout).

**Persistence boundary:** Limen `Storage` effects (browser localStorage) carry an F#-encoded event log; Save writes,
then reads back and compares bytes before showing Saved. This is a real, small boundary but **not** GitHub: PERSIST-007's
"GitHub confirmed the commit" is not exercised, and no result here is evidence for GitHub persistence.

Acceptance conditions per requirement are the catalog's `verification` lists, copied into REQUIREMENT-MATRIX.json.

## Execution model (as modified)

`Contract release → parallel domain/UI implementation → staging → integration (T+60) → verification → repair`

- **Contract coordinator + verification agent** (this session) owns `Contracts.fs`, CONTRACT-REGISTRY.json,
  `tools/contract-check.fsx`, `tools/lifecycle.fsx` and all of `docs/experiments/exp-indy-002/` except the agent timelines.
- **Domain agent** (worktree) owns `src/Exp002.Engine/`, `tests/Exp002.Tests/`, `agents/domain-timeline.jsonl`.
- **UI agent** (worktree) owns `src/Exp002.Ui/` (with an isolated TEMPORARY `Stub.fs`), `src/Exp002.Wasm/`, `web/`,
  `build.fsx`, `serve.fsx`, `tools/ui-binding-check.fsx`, `tests/Exp002.Ui.Tests/`, `agents/ui-timeline.jsonl`.
- The seam is `EnginePorts` in `Contracts.fs`; integration swaps `Stub.ports` for `Engine.ports` in one place.
- Contracts move through DEFINED → VALIDATED → RELEASED → IMPLEMENTING → STAGED → INTEGRATION_READY → VERIFIED (or
  BLOCKED). Every transition is appended to CONTRACT-LIFECYCLE.jsonl with version and SHA-256 content hash by
  `tools/lifecycle.fsx`. RELEASED requires `contract-check` PASS (C1–C5: catalog coverage, F# binding existence and
  type by reflection, command/UI action existence, test IDs, no untyped editable field). A released contract is never
  changed silently: a change request is recorded, a new version is hashed, dependents are told, rework is measured.

## Schedule

| Window | Work |
|---|---|
| T+0–T+10 | contracts, registry, validation, group releases, agent dispatch |
| T+10–T+60 | parallel implementation; local checks only (compile, unit tests, contract-check, static UI binding check); no browser or full CI |
| T+60 | integration checkpoint on the candidate (this branch): merge both staged branches, reject version mismatches, cross-component build, all unit tests, contract-check `--tests`, browser + 375 px mobile tests, open the draft PR (required CI) |
| T+60–T+80 | classify and repair integration defects, focused tests after each |
| T+80–T+90 | acceptance run on the final candidate; development stops at T+90 (22:45:04Z) |

## Evidence files

PLAN.md, CONTRACT-REGISTRY.json, REQUIREMENT-MATRIX.json, CONTRACT-LIFECYCLE.jsonl, TIMELINE.jsonl,
AGENT-TIMELINE.jsonl (+ `agents/*.jsonl`), TEST-RESULTS.json, INTEGRATION-RESULTS.json, CI-RUNS.json,
CONTRACT-CHANGES.json, DEFECTS.json, COMPUTE.json, FINAL-REPORT.md. Every value is marked measured, estimated or
unavailable.
