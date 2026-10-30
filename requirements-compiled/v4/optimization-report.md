# v4 optimization report

How the v4 package was optimized for the November 7 build, and what each change costs. No requirement was removed, merged or downgraded. Counts in this report are computed by the compiler; the files named hold the evidence.

## Summary

| | Result |
|---|---|
| Authoritative baseline | `BL-CAUSA-INDY-INIT-62df28a559c0`: the R7 baseline plus DCC-013..015 (owner-approved R7-AMD-03..05, DEC-R7-008..010) |
| Requirements | 517 (R7) → 520 (v4); 471 in the R6 baseline that v3 compiled |
| P0 requirements | 161 (R7) → 161 (v4); 115 in v3 |
| P0 obligations in the plan | 613, all classified and bound; plan-readiness READY, 1211 of 1211 category checks, no permits |
| Ambiguities resolved | 14 status terms given operational definitions (`operational-definitions.json`) |
| Contradictions found | 5. Three were resolved by an existing authority. C1 (DEC-R7-006 against causa-data's catalog text) is resolved for Causa but needs a causa-data fix. C5 (PERSIST-021 against PERSIST-007/023 on Saved after a verified load), found by the independent review, needs the owner (ODR-V4-08) |
| Requirements consolidated | 0. Twelve overlap groups are consolidated at the verification level only (one test proves both); see §G |
| Requirements amended | 3 written into the source (DCC-013..015, owner-approved). 6 obligation amendments are proposed and not applied (ODR-V4-02..06, ODR-V4-08) |
| Acceptance obligations | 655: 613 from the plan, 39 invariant tests, 3 lesson tests. 650 are normative; 5 wait for owner decisions. The plan also binds 100 strengthening tests (invariant, lesson, verifier and 72 WebKit twins) on 94 obligations, so verify-plan enforces them (W16) |
| Invariants | 25, in seven groups including accessibility (`invariants.json`) |
| Critical path | 340 minutes over 11 timed tasks (10 dependencies): T12 → T13 → T14 → T19 → T20 → T21 → T30 → T31 → T32 → T33 → T34 |
| Schedule fit | Role lanes with two UI agents (C): 340 + 65 reserved = 405 minutes, 15 minutes of slack. Role lanes (A): 465. One lead agent (B): 765. Only C fits. The owner chooses (ODR-V4-07), and FG-07 checks the chosen topology. No scope is cut |

## A. Contradiction analysis

| # | Contradiction | Resolution | Authority |
|---|---|---|---|
| C1 | causa-data `research/HISTORICAL-CASE-CATALOG.md` says the active catalog lists "exactly these three case identifiers and no others". EXT-DATA-013, UI-001, UI-015 and PF-014 require four, and causa-data's README and `catalog/active.json` (demonstration-v1 at `e8259df`) have four | For Causa, four. The stale causa-data text is a documentation defect to fix in causa-data | DEC-R7-006 (finding F-030) |
| C2 | v3's work item T19 "Local durability and persistence truth" and contract CT-004 ("recover local edits; local-durable separate from remotely-synced") require a local durable store. ADR-R7-001 and PERSIST-003 forbid one | T19 and CT-004 were rewritten to session memory plus GitHub | PERSIST-001 precedence (ADR-R7-001) |
| C3 | DEC-023 and F-013 allowed interfaces extracted from R001 source. The amended interface-freeze procedure and PERSIST-026 forbid earlier rehearsal source as an input | DEC-023 and F-013 were restated: interfaces are authored at kickoff | PERSIST-026, the interface-freeze procedure |
| C5 | PERSIST-021 requires an investigation opened from GitHub to "show Saved at that commit"; PERSIST-007 allows only a confirmed save to move it to Saved, and PERSIST-023 makes Saved constructible only from the PERSIST-007 read-back | Not resolved by the agent: the state machines mark Saved after a verified load or discard as proposed | Owner decision request ODR-V4-08 |
| C4 | UI-016 changed between the R6 and R7 baselines (DEC-R7-002's amber sentence), but `docs/rehearsal-007/traceability.json` does not list it | Reported as a traceability gap. The change is authorized: DEC-R7-002 names UI-016 | DEC-R7-002 (`baseline-reconciliation.json` `summary.traceabilityGaps`) |

Areas checked with no contradiction found: state (PERSIST-002..004 against UI-013), persistence (PERSIST against R10.x and the storage contract; PERSIST-001 resolves the rest), GitHub authentication (PF-015 and EXT-DATA-005 after DEC-R7-001 and DEC-R7-007), concurrency (PERSIST-006/013 against AR-035), validation, accessibility (UI-016 and PERSIST-030), deployment (DCC-008 and PF-020), security, and acceptance (AR-019 and PERSIST-055). The legacy check V03 also passes.

## B. Ambiguity analysis

Rehearsal 7 showed that these words admit materially different implementations: Save requested, Saving, Saved/confirmed, Confirmed (UI claim), Stale head, Conflict, Unknown commit outcome, Recovered, Loaded/opened, Verified, Complete, Accepted, Offline, and "the Rehearsal 7 agent". Each now has an operational definition with the evidence that decides it (`operational-definitions.json`; check W04). Examples:

- **Saved:** GitHub returned the commit for that exact mutation and a re-read at that commit returned exactly the written bytes, or reconciliation found the save's marker on a commit holding those bytes. Nothing else counts.
- **Stale head:** includes GitHub's `FORBIDDEN … is at <oid> but expected <oid>` form, which Rehearsal 7 misclassified as a permission refusal.
- **"Rehearsal 7 agent":** PERSIST-040..055 say "The Rehearsal 7 agent MUST…". The document's status line makes it normative "for Rehearsal 7 and every later Causa build, including the event", so in the event build it means the event's implementing agent.

The legacy normalizer also flags 56 requirements for vague terms and 149 non-P0 requirements without an acceptance criterion (`normalized-requirements.json`). Every P0 requirement has an acceptance contract.

## C. Dependency analysis

- **Requirement graph:** reference extraction now recognizes `PERSIST-` ids (`Inventory.fs`), so PERSIST edges appear in `baseline-reconciliation.json` `dependencies` and in the dependency graph. Without this, the graph had no PERSIST edges at all.
- **Work-item DAG:** 35 nodes and 56 justified edges, acyclic (V05).
- **Foundational contracts first:** invariant inventory T12, kernel T13, interface freeze T14.
- **Independent work:** UI shell T16 and stack lock T11 start at T0.
- **Integration boundaries:** T17 and T21.
- **Verifiable before the UI:** T12 and T19 (PAT-12 and the fake-backed PATs).
- **Rework minimized:** the live GitHub probe T18 (45 minutes) is scheduled at T0, in the persistence lane. The save process T19 depends on it, because the fake must be built from the recorded answers. Review checkpoints T35 (after T14) and T36 (after T21) put independent review inside the run, not only at the end. Rehearsal 7 found two classification defects only against real GitHub, late.
- **Critical path versus integration risk:** the shortest graph would put T20 before the probe. The plan accepts 30 minutes on T18 first because it removes the largest late-integration risk.

## D. State-machine analysis

`state-machines.json` defines five machines: SM-SAVE (7 states, 58 transitions), SM-CONFLICT, SM-CONNECTION, SM-LOAD and SM-NETWORK. For each state they give events, guards, results, effects, failure behaviour and invariants. The illegal-input policy comes from PERSIST-023: any pair not listed fails closed and changes nothing.

Check W05 verifies that:
- states and events are declared;
- every transition is sourced;
- every state is reachable;
- guards are deterministic;
- Saved is entered only by `ReadBackMatched`, `ReconcileMarkerBytesMatch` or a verified discard-load.

The analysis covers the cases Rehearsal 7 got wrong:
- **Stale and reordered replies:** every reply carries its request identity; a reply that is not current is inert. These transitions are marked as proposed by ODR-V4-03.
- **Concurrent operations:** one mutation in flight per tab; a waiting save is not Saving.
- **Uncertain outcomes:** `AnswerLost` always leads to Unknown commit outcome.

Network events are explicit no-ops in every state, so connectivity changes never raise false typed faults.

## E. Interface analysis

`interfaces.json` lists 12 boundaries, each with pinned systems from `dependency-lock.json` and contracts from `curation/contracts.json` (check W09).

- **Limen:** one C# `[JSExport]` shim and one browser-boundary script, both documented interoperability boundaries. They are recorded as Rehearsal 7 findings, not code.
- **GitHub:** typed contract only for GraphQL/REST shapes. Answer forms are verified live in T18.
- **Percepta:** CT-012's identity predates percepta-semantic 0.3.0 and must be re-checked in T11.
- **No new adapter or framework.** The lock forbids Bolero, React, Angular, Vue and Blazor.

## F. Testability analysis

Every P0 obligation has an acceptance obligation (`acceptance/acceptance-obligations.json`) with required tests, producer, consumer, method, freshness, and pass and failure conditions. The consumer is always `verify-plan` run by a verifier independent of the implementing agent at the exact final SHA.

- **PERSIST classification:** the 255 PERSIST obligations are classified by seven rules that quote their text: integration for all (PERSIST-025's stateful fake), plus live-provider, browser, unit, accessibility, security and independent-acceptance where the text names them.
- **Bindings:** regenerated by `propose-test-bindings` (K3e) from the plan built with v3's bindings. The scope test checks that the bindings are exactly v3's plus that proposal.
- **Acceptance gate:** `acceptance/acceptance-gate.json` lists 12 conditions, each checked by an existing tool. Any NOT_RUN fails it.

## G. Complexity analysis

| Simplification | Benefit | Trade-off |
|---|---|---|
| One durable store (GitHub) and session memory only; no local store, queue or cross-tab coordination (already ADR-R7-001; v4 removed the remaining v3 work items and contracts that still assumed local durability) | Removes the defect classes R6-D-002/003 and a whole synchronization layer | Unsaved work dies with the tab; PERSIST-020 makes the indicator the safeguard |
| Immutable records and a pure save-state machine whose projection is the only UI source (PERSIST-023) | Truthful state is structural and model-testable | Requires effects to be returned as data at the boundary |
| Side effects at explicit boundaries: transport decoding inside the persistence module (proposed ODR-V4-06) | Forged replies cannot reach a confirmation | Small refactor of the adapter |
| Verification-level consolidation of 12 overlap groups (3 exact-duplicate obligation groups among PAT acceptance lines; 9 near-duplicates such as PERSIST-030/UI-006 and the Praxis-intake sentences). One suite proves each group; requirements stay verbatim | Fewer tests to write and run | None to scope; preservation forbids merging the text |
| Reuse of verified component contracts (Forma status lozenge, operation status, conflict review, freshness; causa-data `template-verify`; registry digests) instead of new code | Less to build and verify | Pins to 1.33.0; a Forma token check is added (IF-FORMA) |
| Pipeline reuse: v4 is compiled by the existing compiler and a new pure stage (`V4Package.fs`, `TimedSchedule.fs`); no Python, no new framework | Backward compatible: v1–v3 untouched (scope test) | The v3 pipeline still needs the supporting checkouts to recompile the full package |

## Rehearsal 7 findings

There are 20 lesson classes (`r7-lessons-mapping.json`):
- **covered (10):** the requirements already prevent the class; the binding is strengthened by invariant or lesson tests that are bound in the plan.
- **gap-proposed (7):** false save confirmation (forged replies and Saved after a verified load), stale refresh, conflict re-read race, re-authentication, unresolvable conflict, forged replies, and the copy ban for Rehearsal 7's code. Each is an exact operator approval record in `proposals/owner-decision-requests.json`.
- **process (3):** incomplete deployed acceptance, deadline and merge orchestration, and undelivered P0 scope; enforced through the execution plan and acceptance gate.

Every quote is checked verbatim against the five permitted evidence files when `--r7-evidence` is supplied (W07).

## What was not optimized, and why

- **The 420-minute fit with one lead agent or one UI agent:** it cannot be reached without removing P0 scope. ODR-V4-07 asks the owner to choose.
- **The proposed amendments (ODR-V4-02..06, ODR-V4-08) and scope questions (ODR-V4-09, ODR-V4-10):** they wait for operator records (RDP-003). Until then they are labelled non-normative in every artifact.
- **Non-P0 requirements:** 359 requirements state no priority and are verified by manifest rules and gates, not by per-obligation contracts. This is the v3 design; ODR-V4-10 asks whether S14, S15 and DCC-013..015 should become P0.
