# Optimization analysis: Rehearsals 1–3 against the compiled requirements

This document compares what went wrong in earlier rehearsals with the compiled package. It reports only measured values, each with its source.

- **No improvements are claimed.** Nothing here has been measured against this package. Every "would have helped" statement is a hypothesis, to be tested by `percepta-experiment.json`.
- **Rehearsal numbering.** The next run is the repository's **Rehearsal 4** (write-path dress rehearsal). The mission brief calls it "Rehearsal 3". Rehearsal 3 has already run and is NOT ACCEPTED, so its evidence is included here (finding F-020).

## 1. Evidence used

Each source was read at the revision recorded in `curation/sources.json`.

| Run | Source | Status of the record |
|---|---|---|
| R1 | indy-init `docs/DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md` (DCC-011); `indy-init-rehearsal-001` `evidence/` | The primary R001 report is in a repository this session could not read. Only secondary figures are used. |
| R2 | `indy-init-rehearsal-002` `docs/REHEARSAL-002-REPORT.md`, `docs/decisions/DC-00N.md`, `evidence/stack/compatibility-matrix.json` | Complete report. The run declared itself accepted. Under indy-init's ledger contract its ledger is **INVALID** (OWNER-DECISIONS consequences). |
| R3 | `indy-init-rehearsal-003` `docs/REHEARSAL-003-REPORT.md`, `docs/REHEARSAL-003-REMEDIATION-REPORT.md`, `research/requirements-preservation-root-cause.md`, `evidence/telemetry/` | Complete report. Outcome: **NOT ACCEPTED (blocked)**. |
| Percepta | `kemiller2002/percepta` | Holds no rehearsal telemetry. It supplies only the metric vocabulary. |

## 2. Observed telemetry

The table gives values as recorded. "—" means the source does not record the value. Missing values are not estimated.

| Metric | R1 | R2 | R3 (timed phase) |
|---|---|---|---|
| First green CI | T+88.3 min (DCC-011) | 104.3 min | T+79.1 min |
| First verified remote write | — | 176.3–177.5 min | not attempted: read-only scope, later rejected by OD-INDY-1 |
| First deployed, verified candidate | — | 296.8 min | T+118.9 min |
| Accepted | T+261.8 min (DCC-011) | 721.2 min, self-declared; ledger INVALID | not accepted |
| Release candidates | — | 7 on main | 1 |
| Defect classes recurring after a claimed fix | recurring forged-history failures (DCC-011) | 5 (DC-002…DC-006) | DC-002 → M1; DC-005 → D-07 |
| Review rounds | 4 adversarial | 1 security + 11 closure | 2 rounds + re-verification |
| CI runs | — | 128 workflow executions, scope of count not recorded (R3 report) | 59 created, 51 executed, 6 failed, 71.5 min (to experiment end) |
| Tokens | — | unavailable to the session | 116.8 M (351.7 k output; 14.0 M by subagents) |

Sources:
- R2: report §metrics table, lines 77–91.
- R3: report §1 and §6, and the final figures from `evidence/telemetry/tokens-final.json` and `ci-timed.json`.

These runs had different scopes: R3 was read-only, and R2 had writes. They are not comparable as efficiency baselines (DCC-011, ADRP-015).

## 3. Failures and what could plausibly have prevented them

Columns:
- **Cause:** **D** = demonstrated in the cited record; **H** = hypothesis.
- **Category:** one of the six levers named in the mission brief.
- **Package element:** where this package addresses the failure. Every "prevented" claim is a hypothesis until it is measured.

| # | Failure | Run | Cause (D/H, source) | Category | Package element |
|---|---|---|---|---|---|
| 1 | P0 UI-016 (semantic color) silently dropped. 29 P0 PF/EXT-DATA requirements had no disposition. | R3 | **D**: incomplete source discovery. The package was derived from a checklist, not the documents (`requirements-preservation-root-cause.md`). | Requirement definition / evidence planning | `requirements-inventory.json` (466 requirements, 2292 obligations, verbatim); `normalized-requirements.json`. V01, V02 and V09 fail compilation when any requirement or obligation is missing from the inventory, from a work item or from the plans. |
| 2 | REST ref update cannot compare-and-swap. The save path was redesigned after review 6, late in the run. | R2 | **D** (report lines 120–121; lesson 2: "check the provider's concurrency primitives before designing the write path") | Contract validation | **F-009 / CT-007**: the frozen contract still requires the unsafe primitive. It is a readiness blocker, decided before kickoff (T04). |
| 3 | `permissions.push` treated as authority (DC-004). Stale cached mutable reads (DC-005 → R3 D-07). GitHub returns 422 for a missing branch, but the fake returned 404 (R3 H1). | R2, R3 | **D**: provider behaviours the fakes did not model (R2 report lesson 1; DC-004/005; R3 H1) | Integration-testing timing / contract validation | T18 records real provider responses before the adapter is built. T21 is the live checkpoint at the first persistence milestone. CT-017 constraint. |
| 4 | Request-identity class recurred in every run: R1 M4, R2 DC-002, R3 M1 and D-04. "Prevented by design" was claimed and was false. | R1–R3 | **D**: the recurrence is recorded (R3 report line 120). **H**: the cause is that the earlier fix was not reused. | Capability reuse | T12 writes failing-first tests for DC-001…006 and for the identity class before any write code (R4-01). DEC-030 reuses R3's typed request book. |
| 5 | "Not written" reported after a send (DC-003, variants over 7 rounds) | R2 | **D**: "may have applied" state missing from the design (report lesson 3) | Requirement definition | CT-018 states this explicitly. Storage contract §9.4.3 already requires `unknown` to be a distinct state. |
| 6 | Kickoff gates blocked: no `CAUSA_GITHUB_TOKEN`, no released Percepta | R3 | **D** (report §3) | Evidence planning / preconditions | T05 (secret plus a no-op authenticated read before kickoff) and T03 (registered Percepta). Both are on the plan before the timer starts. |
| 7 | Conditor SD-1 (Folio pinned from npm, 404) recurred | R2, R3 | **D** (DEF-STACK-001; R3 defect analysis) | Environment / capability reuse | T03 completion requires a Conditor release with SD-1 fixed. T10 forbids the hand workaround (R4 P3). |
| 8 | Workflow named a missing `global.json` (D-02). The fix pinned an SDK the Dokimos workflow did not install (D-06, fix-induced). | R3 | **D** (R3 report §6) | Environment / dependency ordering | DEC-027 and the T10 completion condition: `global.json` exists before any workflow names it, and every workflow installs that SDK. |
| 9 | Phase-0 stack verification took about the first hour. First green CI came 16 min later than R1. | R2 | **D** (report lines 98–99) | Dependency ordering | T03 and T11 move set qualification upstream of kickoff (R4 P1, P5: preflight untimed). |
| 10 | Self-declared acceptance overturned: R2 ledger INVALID; R3 first draft claimed ACCEPTED | R2, R3 | **D** (OWNER-DECISIONS; R3 report line 9) | Evidence planning | DEC-010 (computed verdict). Every verification entry starts `NOT_RUN`. V10 fails compilation if any requirement is marked complete. |
| 11 | Live-test token could reach uploaded artifacts (N1, high, fix-induced) | R3 | **D** (R3 report §9) | Security boundary / evidence planning | DEC-019 constraint: uploaded artifacts must not contain tokens. VR-PERSISTENCE evidence rule. |
| 12 | Forged event histories accepted, recurring | R1 | **D** recurrence (DCC-011). **H**: validation bypassed the canonical decide/evolve path. | Contract validation | CT-003 (Boundary C: replay validates every event through the engine). T12 and T13 adversarial variants. |

What this mapping shows:
- **Six of the twelve rows are contract-validation or integration-timing failures** (rows 2, 3, 4, 5, 8, 12).
- **Three are precondition or evidence failures that were visible before kickoff** (rows 6, 7, 9).
- **The remaining three** are about the integrity of requirements and acceptance (rows 1, 10, 11).

None of the twelve was caused by a missing application requirement. Each was caused by a contract, precondition or verification step that was discovered late.

## 4. What compilation found that no rehearsal had found yet

These problems were found from the sources alone. Each would have cost a rehearsal slice to discover during a run. All of them are in `curation/findings.json`.

- **F-001 (critical):** UI-001 and UI-015 require the five frozen synthetic investigations on launch. EXT-DATA-001/002, OD-INDY-3 and the active-catalog policy (exactly three historical cases) forbid exactly that. No reconciliation section covers it.
- **F-002 (critical):** two operator records answer DF-CAUSA-2026-0003 differently.
  - Indy-init (2026-10-24): explicit Connect, GUID routes.
  - Rehearsal 3 remediation (2026-10-25): auto-load exception, case-key URL exception.
  - A third record says these answers must not be inferred.
- **F-003 (critical):** the committed baseline (002d8ba, 450 requirements) is behind its own source. AR-019…AR-026 are missing. The Praxis gate would report drift on day one.
- **F-004 (critical):** the mandatory preservation gate exists only on a draft Praxis branch (praxis#292). VER-005 forbids using it without an exception, so RDP-004 cannot currently be met.
- **F-005 (critical):** the Percepta semantic CLI is released (v0.2.1) but not registered. echelon-current 1.27.0 says it is not the complete R4 set.
- **F-009 (critical):** the frozen storage contract §9.4 still mandates the write primitive that Rehearsal 2 proved unsafe.
- **F-008 (critical):** it is undecided whether the AI proposal loop and time travel (kickoff packet checkpoints 12:45 and 2:45) are in scope for R4 and the event.

## 5. Critical path and parallelism (from `dependency-graph.json`)

**Critical path (16 tasks):** T00 → T01 → T07 → T09 → T08 → T12 → T13 → T14 → T19 → T20 → T21 → T30 → T31 → T32 → T33 → T34.

- **The first five are pre-implementation.** They are owner decisions, the baseline re-pin, derivation, operator approvals and the preservation gate.
- **No application code can start before the gate passes.** RDP-004 forbids it, and that rule is a genuine edge in the graph.
- **The R4 timer starts later.** P5 starts it at the first green bootstrap CI (T10), so T00–T09 can be finished before the timer starts.
- **Consequence:** the precondition tasks are where untimed preparation removes the most timed risk.

**Safe parallelism.** These are genuine dependencies only; no task waits on an ordering preference.
- **Before kickoff:** T00, T02, T05 and T06 are independent. T03 waits only for T02. T04 waits only for T00.
- **After the gate:**
  - T16 (shell and loading) and T18 (provider recording) run alongside the kernel (T12 → T13).
  - T15 (frozen replay) and T27 (AI loop, if in scope) run alongside the interface freeze (T14).
- **After checkpoint 1 (T17):** T22, T23, T26 and T29 are mutually independent.
- **Integration checkpoints:**
  - T17 (a legal command end to end) is at depth 9.
  - T21 (live write) is at depth 11.
  - Both come before review (T30) and the first candidate (T31), so incompatible contracts surface before final assembly.

**Plan comparison (structural counts, not measurements):**

| | Plan A (time) | Plan B (resource) |
|---|---|---|
| Unit steps | 18 | 28 |
| Cross-lane handoffs | 43 | 20 |
| Planned CI runs, upper bound | 25 (one per merged task) | 9 (checkpoints and verification only) |
| Agent topology | 6 owner lanes; needs owner approval (F-007) | 1 lead + 1 fresh reviewer (matches R4-03) |

## 6. Estimated optimization opportunities, with assumptions

These are hypotheses, not savings. Each states the assumption it depends on and the measurement that would test it.

1. **Untimed resolution of the 19 readiness blockers** (`requirements-validation-report.json` → `rehearsalReadiness.blockers`).
   - Assumption: each blocker otherwise stops a slice mid-run under the ADRP-001 conflict stop or RDP-005.
   - Historical anchor: R3 ended BLOCKED on two of these classes of condition (rows 6 and 7 of §3).
   - Test: count `agent.human_escalations` and the blocked slices per arm.
2. **Write-path risk retired before the write code is written** (T04, T18 and T21 at the first persistence checkpoint).
   - Assumption: the late classes in R2 (rows 2, 3 and 5) arise from provider behaviour and from the contract.
   - Historical anchor: R2's first verified write came at 176.3 min, and acceptance at 721.2 min after five recurring classes.
   - Test: time from the first verified write to acceptance, and the number of defect classes found after first green CI.
3. **Batched CI under Plan B.**
   - Structural upper bound: 9 runs against 25 for Plan A.
   - Assumption: batching does not hide a failure that per-task CI would catch earlier. AR-022 forbids suppressing mandatory checks, so the bound applies only to optional runs.
   - Historical anchor: R3 created 59 CI runs in its timed phase.
   - Test: CI runs created, executed and failed per arm.
4. **Less rediscovery.**
   - Assumption: agents read the package instead of the 72 documents in `docs/`. The package carries 31 decisions (15 fixed, 7 unresolved), 18 contracts and 35 work items, and every one of their citations is verified.
   - Test: `tokens.total`, `tool.calls` and `tool.file_reads` per arm.
   - Historical anchor: R3 used 116.8 M tokens in its timed phase. No R1 or R2 token figure exists.

No percentage or minute figure is given, because none can be derived from three runs with different scope and no controls (DCC-011).

## 7. Limits

- The R1 primary report was not readable, and the R1 and R2 token counts do not exist.
- The rehearsal clones are shallow. PR and commit totals were therefore not counted.
- Plan step counts are unit steps. No task duration has been measured, so steps order the work but are not time estimates.
- Findings F-001…F-029 are analyst judgements with verbatim citations. The compiler verifies the citations, not the judgement. A person should review them (`flagged-for-review`).
