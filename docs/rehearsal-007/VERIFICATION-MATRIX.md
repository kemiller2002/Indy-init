# Rehearsal 7 verification matrix

Each obligation of the remediation script, the mechanism that verifies it, and its state. Machine-readable: [`verification-matrix.json`](verification-matrix.json). States:

- **PASS**: run and passed on the system of record (GitHub)
- **PASS_LOCAL**: run and passed in this session's sandbox (and, where noted, in the PR's CI); not yet merged and released
- **NOT_RUN**: not executed
- **BLOCKED**: needs an owner action
- **IN_PROGRESS**: running

**Scope of VM-001 to VM-015.** They verify Arca as a library (arca#47). For Causa, the owner-approved [ADR-R7-001](PERSISTENCE-ARCHITECTURE-DECISION.md) removes the shared browser store and the offline queue; the corresponding obligations are verified by PAT-01..15 during Rehearsal 7 (VM-031; [DEFECT-TRACEABILITY.md](DEFECT-TRACEABILITY.md)).

| ID | Obligation | Package | Verification | Evidence | State |
|---|---|---|---|---|---|
| VM-001 | Two/three tabs writing the same investigation without silent loss | R7-REM-001 | arca multi-tab-safety.spec.mjs random interleavings (3 tabs, seeds 1-3) | arca#47; local Chromium 5/5 | **PASS_LOCAL** |
| VM-002 | Tabs writing different investigations | R7-REM-001 | Same queue per namespace: entries from all tabs in one fenced queue (interleavings) | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-003 | Web Locks unavailable / denied | R7-REM-001 | multi-tab-safety: navigator.locks removed -> ownership refused, nothing written | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-004 | Lock-holder termination | R7-REM-001 | limen-queue.spec (hand-over at higher epoch) + interleavings closing the owner | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-005 | Browser restart / storage restoration | R7-REM-001 | multi-tab-safety restart: storageState with IndexedDB into a new context | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-006 | Conflicting remote GitHub updates | R7-REM-001 | Arca ConcurrencyTests / GitHubStorageTests (conditioned commits, conflicts surfaced) | arca main (existing) | **PASS_LOCAL** |
| VM-007 | Interrupted writes | R7-REM-002 | AdapterScheduleTests: lost answer, applied-then-502; GitHubStorageTests unknown outcome | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-008 | Bounded exponential backoff with jitter; no tight loop | R7-REM-002 | SyncScheduleTests (naive host >50 POSTs vs <=5; mutation 76) + adapter tests (<=2 ref updates/attempt, decaying) | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-009 | No retry of permanent authorization failures | R7-REM-002 | Adapter tests: 401 once despite edits; 403 once | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-010 | No duplicate logical writes | R7-REM-002 | Idempotency keys + reconciliation; applied-then-502 lands exactly once | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-011 | Network restoration | R7-REM-002 | Adapter tests: recovery after outage (one ref update, confirmed); drain without online event | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-012 | Durable preservation of unsent changes | R7-REM-001/002 | IndexedDB queue fenced saves; restart spec | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-013 | UI distinguishes saved locally / waiting / sending / confirmed / conflict / permanent failure | R7-REM-002 | SaveState.ofQueue (9 states) + tests; application rendering is Rehearsal 7 work | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-014 | Never report remote persistence before confirmation | R7-REM-002 | Property: confirmed iff synchronized (real synchronized queue) | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-015 | Network failure never presented as a conflict (RF-F-3) | R7-REM-002 | Deferral classification; 5xx/unreadable write answers reconciled | arca#47 (CI green at ac585ba) | **PASS_LOCAL** |
| VM-016 | Read credential cannot write; write credential scoped | R7-REM-003 | Credential probe workflow (SECURITY-REVIEW §1) | — | **BLOCKED** |
| VM-017 | Superseded development runs cancelled; cancelled runs start no jobs | R7-REM-004 | ci-policy CIP-001..004, CIP-008; scaffold guard tests | conditor#122 (CI green) | **PASS_LOCAL** |
| VM-018 | Candidate verification not cancelled by development commits | R7-REM-004 | ci-policy CIP-007; candidate-acceptance.yml template | conditor#122 | **PASS_LOCAL** |
| VM-019 | Controlled commit bursts (5, 15, concurrent, candidate during development) measured | R7-REM-004 | Experiment B procedure (CI-PERFORMANCE §4) | — | **NOT_RUN** |
| VM-020 | Parallel build isolation (MSB3030) | R7-REM-004 | Source assessment only | CI-PERFORMANCE §5 | **NOT_RUN** |
| VM-021 | Real PR merged without routine owner intervention | R7-REM-005 | conditor unattended-readiness | praxis evidence 2026-10-29 readiness receipt (READY, PR #20 -> 9c3aaf6) | **PASS** |
| VM-022 | Merge gate enforced by GitHub, not advisory (UAE-R7-12) | R7-REM-005 | Ruleset + Actions secret | — | **BLOCKED** |
| VM-023 | Evidence generated by the operation that produces it; no avoidable NOT_RUN | R7-REM-006 | Experiment D | — | **NOT_RUN** |
| VM-024 | Candidate selected only from green core verification of its SHA | R7-REM-007 | candidate-acceptance.yml pin + verify jobs | conditor#122 (template; not exercised on GitHub) | **NOT_RUN** |
| VM-025 | Served SHA equals candidate SHA | R7-REM-007 | deployed-smoke (proven in Rehearsal 6) | R6 final acceptance | **PASS** |
| VM-026 | Defect closures bound to fix commits in the rehearsal's own history | R7-REM-008 | verify-defect-closures DCL-001..014 (--base = kickoff commit) | Offline tests at c08c23e; R6 run REFUSED 8/8 (evidence/defect-closures-r6.json); fourth review in progress | **PASS_LOCAL** |
| VM-027 | Non-text indicators >= 3:1 (rendered), high contrast supported | R7-REM-009 | A11Y-NONTEXT-CONTRAST; token test (30 contexts); Chromium spec light/dark/forced colors on renderer output | forma#173 (review PASS; CI green on ac34a55) | **PASS_LOCAL** |
| VM-028 | Pre-lock newest-release comparison recorded | R7-REM-010 | release-comparison observe/assess | Offline tests at c08c23e; live demonstration 2026-10-29T11:08Z PASS (evidence/release-comparison-demo.json); fourth review in progress | **PASS_LOCAL** |
| VM-029 | Weakened tests detected (RF-F-8) | R7-REM-011 | assertion-ratchet (base read from git: the commit that added the kickoff record) | Offline tests at c08c23e; R6 run FAIL, 45 unrecorded removals incl. both UI-034 weakenings, base c80f35e from the real kickoff record (evidence/assertion-ratchet-r6.json); fourth review in progress | **PASS_LOCAL** |
| VM-031 | Causa persistence under ADR-R7-001: independent tabs, expected-head commits, no queue, bounded retries, truthful seven save states | ADR-R7-001 | PAT-01..15 (PERSIST-040..054), PAT-02/04/10 live (PERSIST-025) | Rehearsal 7 run | **NOT_RUN** |
| VM-030 | Independent adversarial review of each work package | R7-REM-011 | Independent agent reviews (SECURITY-REVIEW §4) | See READINESS R7-PF-12 | **IN_PROGRESS** |

**PASS_LOCAL is not PASS.** It becomes PASS when the owning PR's CI is green on the same commit and the change is merged and released.
