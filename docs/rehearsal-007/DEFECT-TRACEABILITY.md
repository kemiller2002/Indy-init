# Rehearsal 7 persistence: defect-to-requirement traceability

The machine-readable record is [`traceability.json`](traceability.json); this page is generated from it and from the Rehearsal 7 baseline. `tools/validate-r7-persistence-requirements.fsx` checks both (R7-06, R7-08, R7-09). Rehearsal 6's own results are not changed; this page only links to them.

## Historical defects

| Defect | Severity | State at Rehearsal 6 | What happened | Root cause | Resolved by | Requirements | Tests |
|---|---|---|---|---|---|---|---|
| R6-D-002 Cross-tab lost updates | critical | open | Without navigator.locks, several tabs read, merged and wrote one shared device store without exclusion; the changes of one or two tabs were lost on every run (AR-035-O4, no-locks half). | A device-local store shared by every tab of the origin needs cross-tab exclusion that the browser does not guarantee. | Removed by design: no shared browser storage for investigations, no cross-tab locking, and tabs are independent GitHub writers resolved by expected-head commits. | PERSIST-002, PERSIST-003, PERSIST-004, PERSIST-006, PERSIST-013, AR-035 | PAT-01, PAT-02, PAT-11, PAT-13 |
| R6-D-003 Offline retry loop | critical | open | A save that could not be sent was retried with no backoff, 151 POSTs in 60 s for one change, while the page said 'Saving to GitHub'. | An offline synchronization queue retried unsendable writes automatically, and its state was projected as Saving. | No offline queue; a save requested offline sends nothing and shows Offline; automatic mutations and reconciliation reads are capped per save request. | PERSIST-010, PERSIST-016, PERSIST-018, PERSIST-024, PERSIST-031, PERSIST-032 | PAT-07, PAT-08, PAT-15 |
| R1-F-2 Spurious conflict for a single writer | major | fixed in the run (a72bc6a) | After saving investigation B, a change to investigation A in the same tab was refused as a conflict and left unsaved, because the branch head had moved for an unrelated investigation. | A stale head was treated as a conflict without checking whether the investigation's own files had changed. | A stale head over unrelated changes rebases automatically, within a limit. | PERSIST-008, PERSIST-009, PERSIST-010 | PAT-03, PAT-04 |
| RF-F-3 Network failure on the rebase path reported as a conflict | major | open | When the write could not be sent on the rebase path, the page said another writer had changed the repository and offered Discard; no other writer existed. | Failure classification did not separate a network failure from a stale head, and the conflict path offered a destructive recovery. | Provider answers are classified from typed content; a network failure is never a conflict; Discard is offered only in a genuine conflict and after confirmation. | PERSIST-008, PERSIST-012, PERSIST-015, PERSIST-024 | PAT-03, PAT-05, PAT-07 |
| RF-F-9 Acceptance judge accepts a false cause | minor | open | UI-010-O2's test judge checked only that a message was long and mentioned preservation and a next step, so the false conflict text of RF-F-3 passed. | The test did not compare the stated cause with the determined cause. | Messages must state the determined cause, and PAT-07 and PAT-14 check the cause, not only the message shape. | PERSIST-024, PERSIST-053 | PAT-07, PAT-14 |
| R6-D-001 Restore claimed Saved for device-only changes | critical | fixed in the run (f91d2c5, a72bc6a, 23ac222) | After all tabs closed, a restore adopted only the page's own writer record and showed 'Saved to GitHub (confirmed …)' for other writers' changes that existed only on the device. | Save state was restored from browser storage rather than derived from a GitHub confirmation. | Nothing is restored from browser storage; Saved is constructible only from a read-back confirmation; reopening loads from GitHub. | PERSIST-003, PERSIST-007, PERSIST-021, PERSIST-023 | PAT-10, PAT-12 |
| R1-F-1 Another investigation's save state shown | critical | fixed in the run (23ac222) | An investigation's page showed another investigation's save state, so a change that never reached GitHub read 'Saved to GitHub'. | One workspace-level sync state was rendered on every investigation page. | Each investigation has its own save state, and the workspace summary may not contradict it. | PERSIST-022, PERSIST-033 | PAT-02, PAT-05, PAT-15 |
| RF-F-7 Header contradicts the workspace | minor | open | The header said the workspace had no investigation while it held investigations saved to GitHub. | The header's connection label was not derived from the investigations it summarized. | The workspace summary is derived from the investigations' save states. | PERSIST-033 | PAT-15 |
| R6-DEVIATION-D-07 Web Locks used instead of a cross-tab library (deviation D-07) | deviation | recorded deviation | AR-035-O4 was met with a Web Locks exclusive section and a fallback that did not hold without Web Locks; Arca 0.6.0 stayed unused. | The requirement demanded a shared durable device store, which needs cross-tab exclusion. | The requirement for a shared device store is superseded, so no cross-tab exclusion mechanism, Web Locks or library, is needed. | PERSIST-003, PERSIST-004, AR-035 | PAT-11, PAT-13 |

Evidence for each defect:

- **R6-D-002:** [REHEARSAL-006-RESULTS.md](../../docs/REHEARSAL-006-RESULTS.md); [FINAL-REVIEW.md (RF-F-1)](https://github.com/kemiller2002/indy-init-rehearsal-006/blob/main/docs/r6/reviews/FINAL-REVIEW.md); [DEVIATIONS.md (D-07)](https://github.com/kemiller2002/indy-init-rehearsal-006/blob/main/docs/r6/DEVIATIONS.md)
- **R6-D-003:** [REHEARSAL-006-RESULTS.md](../../docs/REHEARSAL-006-RESULTS.md); [FINAL-REVIEW.md (RF-F-2)](https://github.com/kemiller2002/indy-init-rehearsal-006/blob/main/docs/r6/reviews/FINAL-REVIEW.md)
- **R1-F-2:** [REVIEW-1.md (R1-F-2)](https://github.com/kemiller2002/indy-init-rehearsal-006/blob/main/docs/r6/reviews/REVIEW-1.md)
- **RF-F-3:** [FINAL-REVIEW.md (RF-F-3)](https://github.com/kemiller2002/indy-init-rehearsal-006/blob/main/docs/r6/reviews/FINAL-REVIEW.md)
- **RF-F-9:** [FINAL-REVIEW.md (RF-F-9)](https://github.com/kemiller2002/indy-init-rehearsal-006/blob/main/docs/r6/reviews/FINAL-REVIEW.md)
- **R6-D-001:** [REHEARSAL-006-RESULTS.md](../../docs/REHEARSAL-006-RESULTS.md)
- **R1-F-1:** [REHEARSAL-006-RESULTS.md](../../docs/REHEARSAL-006-RESULTS.md); [REVIEW-1.md (R1-F-1)](https://github.com/kemiller2002/indy-init-rehearsal-006/blob/main/docs/r6/reviews/REVIEW-1.md)
- **RF-F-7:** [FINAL-REVIEW.md (RF-F-7)](https://github.com/kemiller2002/indy-init-rehearsal-006/blob/main/docs/r6/reviews/FINAL-REVIEW.md)
- **R6-DEVIATION-D-07:** [DEVIATIONS.md (D-07)](https://github.com/kemiller2002/indy-init-rehearsal-006/blob/main/docs/r6/DEVIATIONS.md)

The defect groups named in the Rehearsal 7 preparation map as follows:

- **R6-D-002, cross-tab lost updates:** R6-D-002 and deviation D-07.
- **R6-D-003, offline retry loop:** R6-D-003.
- **Rehearsal 6 GitHub conflict and rebase findings:** R1-F-2, RF-F-3, RF-F-9, R6-D-001, R1-F-1 and RF-F-7.

## Acceptance tests

Each test is a requirement of its own (PERSIST-040 to PERSIST-054) that the Rehearsal 7 agent must implement and run during the timed build. PERSIST-055 makes its result a ledger entry.

| Test | Requirement | Name | Covers |
|---|---|---|---|
| PAT-01 | PERSIST-040 | Independent browser-tab state | PERSIST-002, PERSIST-003 |
| PAT-02 | PERSIST-041 | Concurrent GitHub commits | PERSIST-006, PERSIST-007, PERSIST-013, PERSIST-014, PERSIST-022, PERSIST-025 |
| PAT-03 | PERSIST-042 | Stale HEAD rejection | PERSIST-008 |
| PAT-04 | PERSIST-043 | Automatic rebase of unrelated changes | PERSIST-009, PERSIST-013 |
| PAT-05 | PERSIST-044 | Same-investigation conflict preservation | PERSIST-011, PERSIST-012, PERSIST-013, PERSIST-022 |
| PAT-06 | PERSIST-045 | Unknown commit reconciliation | PERSIST-015 |
| PAT-07 | PERSIST-046 | Offline save failure | PERSIST-017, PERSIST-018, PERSIST-024 |
| PAT-08 | PERSIST-047 | No uncontrolled retry loops | PERSIST-010, PERSIST-016 |
| PAT-09 | PERSIST-048 | Navigation with unsaved changes | PERSIST-019, PERSIST-020 |
| PAT-10 | PERSIST-049 | Successful reopening from GitHub | PERSIST-005, PERSIST-007, PERSIST-020, PERSIST-021 |
| PAT-11 | PERSIST-050 | Operation without Web Locks | PERSIST-004 |
| PAT-12 | PERSIST-051 | Save-state machine model test | PERSIST-007, PERSIST-023, PERSIST-031 |
| PAT-13 | PERSIST-052 | Production artifact and provenance scan | PERSIST-003, PERSIST-004, PERSIST-026 |
| PAT-14 | PERSIST-053 | Requirements and copy consistency | PERSIST-001, PERSIST-024, PERSIST-032 |
| PAT-15 | PERSIST-054 | Save-state indicator acceptance | PERSIST-030, PERSIST-031, PERSIST-032, PERSIST-033 |

## Requirement coverage

Every functional, nonfunctional and interface requirement names at least one test in its own acceptance obligation and is covered by at least one test requirement.

| Requirement | Title | Acceptance obligations | Covered by |
|---|---|---|---|
| PERSIST-001 | Authority and precedence | 1 | PAT-14 |
| PERSIST-002 | Each browser tab holds its own investigation state | 1 | PAT-01 |
| PERSIST-003 | No shared browser storage for investigations | 1 | PAT-01, PAT-13 |
| PERSIST-004 | No cross-tab locking or coordination | 1 | PAT-11, PAT-13 |
| PERSIST-005 | GitHub is the authoritative durable store | 1 | PAT-10 |
| PERSIST-006 | One atomic commit per save, with an expected head | 1 | PAT-02 |
| PERSIST-007 | A save is confirmed only after GitHub confirms it | 1 | PAT-02, PAT-10, PAT-12 |
| PERSIST-008 | Detect a stale head | 1 | PAT-03 |
| PERSIST-009 | Rebase automatically over unrelated changes | 2 | PAT-04 |
| PERSIST-010 | Limit automatic rebases | 1 | PAT-08 |
| PERSIST-011 | Preserve unsaved changes when the same investigation changed remotely | 1 | PAT-05 |
| PERSIST-012 | Resolve a genuine conflict only by explicit user choice | 2 | PAT-05 |
| PERSIST-013 | Never silently overwrite another commit | 1 | PAT-02, PAT-04, PAT-05 |
| PERSIST-014 | Unique write markers | 1 | PAT-02 |
| PERSIST-015 | Reconcile an unknown commit outcome before retrying | 1 | PAT-06 |
| PERSIST-016 | Bounded retries and no tight loops | 1 | PAT-08 |
| PERSIST-017 | An already loaded investigation stays visible offline | 1 | PAT-07 |
| PERSIST-018 | Editing while disconnected stays in session memory | 1 | PAT-07 |
| PERSIST-019 | Warn before leaving with unsaved changes | 1 | PAT-09 |
| PERSIST-020 | Unsaved work does not survive browser termination | 1 | PAT-09, PAT-10 |
| PERSIST-021 | Reopen from GitHub | 2 | PAT-10 |
| PERSIST-022 | One save state per investigation | 1 | PAT-02, PAT-05 |
| PERSIST-023 | Truthful save state is structural | 2 | PAT-12 |
| PERSIST-024 | Failure causes are reported truthfully | 1 | PAT-07, PAT-14 |
| PERSIST-025 | Testable provider boundary | 3 | PAT-02 |
| PERSIST-026 | Built from these requirements | 2 | PAT-13 |
| PERSIST-030 | Forma save-state indicator | 1 | PAT-15 |
| PERSIST-031 | Meaning of each save state | 1 | PAT-12, PAT-15 |
| PERSIST-032 | No false persistence claims | 1 | PAT-14, PAT-15 |
| PERSIST-033 | Workspace connection and unsaved summary | 1 | PAT-15 |

## Amended Rehearsal 6 requirements

No Rehearsal 6 requirement identifier is removed. These 22 requirements keep their identifiers and are amended.

| Requirement | Document | Priority | Why | Replaced by |
|---|---|---|---|---|
| UI-006 | CAUSA-UI-EXPERIENCE-REQUIREMENTS.md | P0 | The nine-state local/remote model (saving locally, saved locally, queued offline, syncing remotely) assumed a device-local durable store. Replaced by the seven session-local save states. | PERSIST-007, PERSIST-030, PERSIST-031, PERSIST-011, PERSIST-012 |
| UI-010 | CAUSA-UI-EXPERIENCE-REQUIREMENTS.md | P0 | States where unsaved changes are preserved: only in the tab memory. | PERSIST-020 |
| ADRP-011 | AGENT-DECISION-REDUCTION-PROFILE.md | unstated | The offline default no longer preserves changes durably; they stay unsaved in the tab. | PERSIST-018 |
| ADRP-012 | AGENT-DECISION-REDUCTION-PROFILE.md | unstated | Build order step renamed from local persistence truth to session-local save-state truth. | PERSIST-023 |
| R9.7 | REQUIREMENTS.md | unstated | Offline deep links open only investigations already loaded in the tab; no locally held store exists. | PERSIST-017 |
| R10.5 | REQUIREMENTS.md | unstated | Offline tolerance narrowed to visibility of loaded investigations; offline edits are session-only. | PERSIST-017, PERSIST-018 |
| R10.6 | REQUIREMENTS.md | unstated | Pending remote state restated as unconfirmed, never successful. | PERSIST-007, PERSIST-031 |
| R10.7 | REQUIREMENTS.md | unstated | Restart recovery of pending writes removed: no local crash recovery. | PERSIST-019, PERSIST-020, PERSIST-021 |
| S23 | SCREEN-CONTRACTS.md | unstated | Conflict recovery screen shows the tab version, the GitHub version and unsaved commands held in the tab. | PERSIST-011, PERSIST-012 |
| D-204 | DESIGN-DECISION-BOUNDARIES.md | unstated | Reversible choice of a browser durability store closed: none. | PERSIST-003, PERSIST-005 |
| KICKOFF-EXECUTION-PACKET§preamble | KICKOFF-EXECUTION-PACKET.md | unstated | Local durable store replaced by session-local state; the 12:00 checkpoint no longer allows a fallback. | PERSIST-002, PERSIST-006, PERSIST-007 |
| KICKOFF-EXECUTION-PACKET§3-work-order | KICKOFF-EXECUTION-PACKET.md | unstated | Local durability step replaced by session-local state and truthful save state. | PERSIST-002, PERSIST-031 |
| KICKOFF-EXECUTION-PACKET§4-parallel-ownership-after-kernel-stabilizes | KICKOFF-EXECUTION-PACKET.md | unstated | Persistence agent owns session-local state, not a local durable store. | PERSIST-002 |
| KICKOFF-EXECUTION-PACKET§persistence-agent | KICKOFF-EXECUTION-PACKET.md | unstated | Persistence agent owns session-local state, not a local durable store. | PERSIST-002 |
| KICKOFF-EXECUTION-PACKET§7-checkpoints | KICKOFF-EXECUTION-PACKET.md | unstated | 12:00 checkpoint is the confirmed GitHub save path; no durable-local fallback. | PERSIST-006, PERSIST-007 |
| KICKOFF-EXECUTION-PACKET§12-00 | KICKOFF-EXECUTION-PACKET.md | unstated | 12:00 checkpoint is the confirmed GitHub save path; no durable-local fallback. | PERSIST-006, PERSIST-007 |
| AR-035 | AGENT-RELIABILITY-REQUIREMENTS.md | P0 | Device-local durable state shared by several tabs is the design that produced R6-D-002; tabs now share only GitHub. | PERSIST-002, PERSIST-003, PERSIST-004 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble | REHEARSAL-AGENT-INTERFACE-FREEZE.md | unstated | Boundary D restated for expected-head commits, reconciliation and rebase; the local queue and local-durable status removed; interfaces authored from requirements, not extracted from R001 source. | PERSIST-006, PERSIST-009, PERSIST-015, PERSIST-026 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§ownership | REHEARSAL-AGENT-INTERFACE-FREEZE.md | unstated | Persistence owner holds session-local unsaved changes, not a local queue. | PERSIST-002, PERSIST-006 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence | REHEARSAL-AGENT-INTERFACE-FREEZE.md | unstated | Boundary D restated; no local-durable status. | PERSIST-005, PERSIST-006, PERSIST-007, PERSIST-009, PERSIST-012, PERSIST-015 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure | REHEARSAL-AGENT-INTERFACE-FREEZE.md | unstated | Interfaces are authored at kickoff from requirements; earlier rehearsal source is not an input. | PERSIST-026 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§mandatory-negative-cases | REHEARSAL-AGENT-INTERFACE-FREEZE.md | unstated | Offline-queue negative case replaced by stale head over unrelated changes, unknown commit outcome and save requested offline. | PERSIST-009, PERSIST-015, PERSIST-018 |

## Superseded Rehearsal 6 obligations

Every Rehearsal 6 obligation whose text no longer appears in its requirement, with its Rehearsal 6 text and what replaces it. Obligation ids are those of each baseline: Rehearsal 6 `BL-CAUSA-INDY-INIT-856157a78d47`, and the Rehearsal 7 baseline for the replacements.

| Rehearsal 6 obligation | Rehearsal 6 text | Superseded by |
|---|---|---|
| UI-006-O1 | Expose distinct states: clean, unsaved, saving locally, saved locally, queued offline, syncing remotely, remotely synced, conflict, failed. | UI-006-O1, UI-006-O2, PERSIST-007, PERSIST-030, PERSIST-031, PERSIST-011, PERSIST-012 |
| UI-006-O2 | Only claim remote synchronization after confirmed provider acknowledgement. | UI-006-O3, PERSIST-007, PERSIST-030, PERSIST-031, PERSIST-011, PERSIST-012 |
| UI-006-O3 | Show last successful sync time when known. | UI-006-O4, PERSIST-007, PERSIST-030, PERSIST-031, PERSIST-011, PERSIST-012 |
| UI-006-O4 | On expected-version conflict, preserve local edits and provide a recoverable compare/retry flow; do not overwrite remote history. | UI-006-O5, PERSIST-007, PERSIST-030, PERSIST-031, PERSIST-011, PERSIST-012 |
| UI-010-O2 | Every error MUST describe what happened, what data was preserved, and a safe next step. | UI-010-O2, PERSIST-020 |
| ADRP-011-O1 | No data: explicit empty-state action; loading: HTML/CSS shell before WASM; failure: visible recoverable error; offline: preserve local changes and label unsynced; version conflict: no overwrite, preserve edits and show reconciliation; unauthorized action: disabled/explained by F# capability; historical mode: visibly read-only; unknown semantics: preserve and block unsafe writes; reduced motion: disable nonessential animation. | ADRP-011-O1, PERSIST-018 |
| ADRP-012-O1 | Order: freeze contract and interface pins; F# kernel and invariants; frozen fixture replay; shell/loading/navigation; end-to-end legal action; local persistence truth; GitHub adapter; timeline/evidence/hypotheses; causal graph/comparison/report; proof and deployed acceptance. | ADRP-012-O1, PERSIST-023 |
| R9.7-O4 | A deep link to state that is already held locally opens offline, and its persistence state is shown truthfully. | R9.7-O4, PERSIST-017 |
| R10.5-O1 | The active investigation must remain usable during temporary loss of network access. | R10.5-O1, PERSIST-017, PERSIST-018 |
| R10.6-O1 | A remote write that has not completed must be represented as pending, not successful. | R10.6-O1, PERSIST-007, PERSIST-031 |
| R10.7-O1 | Pending writes must be recoverable after application restart when practical. | R10.7-O1, PERSIST-019, PERSIST-020, PERSIST-021 |
| S23-O2 | local version | S23-O2, PERSIST-011, PERSIST-012 |
| S23-O3 | remote version | S23-O3, PERSIST-011, PERSIST-012 |
| S23-O4 | pending commands | S23-O4, PERSIST-011, PERSIST-012 |
| D-204-O1 | IndexedDB or another browser store may be used. | D-204-O1, D-204-O2, PERSIST-003, PERSIST-005 |
| D-204-O2 | The domain model must not depend on its API. | D-204-O3, PERSIST-003, PERSIST-005 |
| KICKOFF-EXECUTION-PACKET§preamble-O25 | local durability; | KICKOFF-EXECUTION-PACKET§preamble-O25, PERSIST-002, PERSIST-006, PERSIST-007 |
| KICKOFF-EXECUTION-PACKET§preamble-O49 | local durable store; | KICKOFF-EXECUTION-PACKET§preamble-O49, PERSIST-002, PERSIST-006, PERSIST-007 |
| KICKOFF-EXECUTION-PACKET§preamble-O98 | Durable local + GitHub persistence path established or explicit fallback active. | KICKOFF-EXECUTION-PACKET§preamble-O98, PERSIST-002, PERSIST-006, PERSIST-007 |
| KICKOFF-EXECUTION-PACKET§3-work-order-O5 | local durability; | KICKOFF-EXECUTION-PACKET§3-work-order-O5, PERSIST-002, PERSIST-031 |
| KICKOFF-EXECUTION-PACKET§4-parallel-ownership-after-kernel-stabilizes-O18 | local durable store; | KICKOFF-EXECUTION-PACKET§4-parallel-ownership-after-kernel-stabilizes-O18, PERSIST-002 |
| KICKOFF-EXECUTION-PACKET§persistence-agent-O2 | local durable store; | KICKOFF-EXECUTION-PACKET§persistence-agent-O2, PERSIST-002 |
| KICKOFF-EXECUTION-PACKET§7-checkpoints-O3 | Durable local + GitHub persistence path established or explicit fallback active. | KICKOFF-EXECUTION-PACKET§7-checkpoints-O3, PERSIST-006, PERSIST-007 |
| KICKOFF-EXECUTION-PACKET§12-00-O1 | Durable local + GitHub persistence path established or explicit fallback active. | KICKOFF-EXECUTION-PACKET§12-00-O1, PERSIST-006, PERSIST-007 |
| AR-035-O4 | Device-local durable state MUST survive several tabs sharing one store. | AR-035-O4, PERSIST-002, PERSIST-003, PERSIST-004 |
| AR-035-O5 | Rehearsal 5 found and fixed all four during the run (review findings F-1, F-2, R2-F1, R2-F2). | AR-035-O5, PERSIST-002, PERSIST-003, PERSIST-004 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O3 | Persistence owner: provider-neutral adapter, frozen contract mapping, local queue, optimistic concurrency and remote acknowledgment. | REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O3, PERSIST-006, PERSIST-009, PERSIST-015, PERSIST-026 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O16 | Provider-neutral operations: load, append with expected version, acknowledge remote persistence, surface conflict, and recover local edits. | REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O16, PERSIST-006, PERSIST-009, PERSIST-015, PERSIST-026 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O17 | Separate local-durable from remotely-synced status. | REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O17, PERSIST-006, PERSIST-009, PERSIST-015, PERSIST-026 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O22 | Extract actual types and exported interfaces from accepted R001 F# source and Limen adapter. | REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O22, REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O23, PERSIST-006, PERSIST-009, PERSIST-015, PERSIST-026 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O29 | Stale aggregate version; forged/illegal history; unknown required semantic extension; unauthorized command; duplicate submission; conflicting remote write; offline queue; mismatched frozen schema; incomplete verification; incorrect deployed SHA. | REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O30, PERSIST-006, PERSIST-009, PERSIST-015, PERSIST-026 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§ownership-O3 | Persistence owner: provider-neutral adapter, frozen contract mapping, local queue, optimistic concurrency and remote acknowledgment. | REHEARSAL-AGENT-INTERFACE-FREEZE§ownership-O3, PERSIST-002, PERSIST-006 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence-O1 | Provider-neutral operations: load, append with expected version, acknowledge remote persistence, surface conflict, and recover local edits. | REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence-O1, PERSIST-005, PERSIST-006, PERSIST-007, PERSIST-009, PERSIST-012, PERSIST-015 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence-O2 | Separate local-durable from remotely-synced status. | REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence-O2, PERSIST-005, PERSIST-006, PERSIST-007, PERSIST-009, PERSIST-012, PERSIST-015 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O1 | Extract actual types and exported interfaces from accepted R001 F# source and Limen adapter. | REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O1, REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O2, PERSIST-026 |
| REHEARSAL-AGENT-INTERFACE-FREEZE§mandatory-negative-cases-O1 | Stale aggregate version; forged/illegal history; unknown required semantic extension; unauthorized command; duplicate submission; conflicting remote write; offline queue; mismatched frozen schema; incomplete verification; incorrect deployed SHA. | REHEARSAL-AGENT-INTERFACE-FREEZE§mandatory-negative-cases-O1, PERSIST-009, PERSIST-015, PERSIST-018 |

## Long-term documents marked superseded for Causa

These documents are outside the baseline: long-term platform direction or competition plans. Their sections that describe offline durability keep their text for the platform and carry a "Superseded for Causa (ADR-R7-001)" note. PERSIST-001 gives this document precedence over them. Validator check R7-10 fails any such statement that is neither marked nor negated.

| Document | Sections |
|---|---|
| docs/PERSISTENCE-CONTRACT.md | §19 Offline local store; §20 Local truth distinction; §21 Restart recovery; §22 Queue durability; §46 IndexedDB/local browser persistence; §47 offline work survives restart |
| docs/POLICY-ENGINE.md | §31 Offline command queue |
| docs/PRODUCT-REQUIREMENTS.md | P32.8 Local durability; P44.3 Offline analytical capability |
| docs/ARCHITECTURE.md | Local-first behavior |
| docs/IMPLEMENTATION-SLICES.md | Slice 4: Durable local-first persistence |
| docs/BUILD-DAY-EXECUTION.md | 11:15–12:00 Local durability + GitHub provider |
| docs/INVESTIGATION-ACCEPTANCE-SCENARIOS.md | A70 Offline restart preserves pending work; A185 Auth expiry preserves local queue |
| docs/SECURITY-THREAT-MODEL.md | §21 Local durable data |
| docs/OPEN-QUESTIONS.md | OQ-008 Local browser storage (answered) |
| docs/REHEARSAL-INTERFACE-MANIFEST-R001.md | whole document: not an input to Rehearsal 7 |
| kickoff/indy-init.kickoff.json | prioritySlices order 5: local-persistence renamed session-state |
