# ADR-R7-001: Session-local investigation state, GitHub as the only durable store

| | |
|---|---|
| Decision | ADR-R7-001 |
| Date | 2026-10-29 |
| Status | Approved by the owner; effective for Rehearsal 7 when the owner merges the pull request that adds it (RDP-003) |
| Applies to | Causa: Rehearsal 7 and every later Causa build, including the event |
| Normative requirements | [`docs/CAUSA-PERSISTENCE-REQUIREMENTS.md`](../CAUSA-PERSISTENCE-REQUIREMENTS.md) (PERSIST-001 to PERSIST-055) |
| Traceability | [`DEFECT-TRACEABILITY.md`](DEFECT-TRACEABILITY.md), machine-readable [`traceability.json`](traceability.json) |

## Provenance

- **Who wrote this:** an agent (Claude Code) wrote this record on 2026-10-29 from the owner's task instruction "Indy Init: Rehearsal 7 Requirements Preparation". That instruction names the architecture as "the approved Causa persistence architecture" and states each of its parts. The owner did not edit this file.
- **What the agent decided:** only the wording and the concrete limits the instruction left open. They are listed under "Choices made in writing the requirements" so the owner can change them before kickoff.
- **What this record does not do:** it changes requirements. It verifies nothing and starts nothing. Rehearsal 6's results are unchanged.

## Context

Rehearsal 6 was NOT ACCEPTED. Its two open critical defects were both in the persistence architecture the requirements asked for, not in a single bug:

- **R6-D-002, cross-tab lost updates.** AR-035-O4 required "device-local durable state" to "survive several tabs sharing one store". Every tab of the origin read, merged and wrote one store. Without `navigator.locks` there was no exclusion, and one or two tabs' changes were lost on every run. Two fallback designs were tried during the run: a localStorage lease, and a lease with a hold. Neither was correct, and neither shipped (deviation D-07). The Rehearsal 6 report suggested a cross-tab guard such as Arca for Rehearsal 7.
- **R6-D-003, offline retry loop.** R10.5 and R10.7 asked for offline work that survives a restart, and UI-006 for a "queued offline" state. The resulting queue retried a save that could not be sent 151 times in 60 seconds, while the page said "Saving to GitHub".

The GitHub write path had its own findings. R1-F-2 was a spurious conflict when the branch moved for an unrelated investigation; it was fixed by a rebase in the run. RF-F-3 was a network failure on that rebase path, reported as another writer's conflict with Discard offered. R6-D-001 and R1-F-1 were false "Saved to GitHub" claims, restored from browser storage or taken from another investigation.

The common cause was shared, durable browser state. It needs cross-tab exclusion the platform does not guarantee on every target, including the iPad Safari target, and it creates a second, local truth that the interface then has to reconcile with GitHub.

## Decision

1. **Session-local state.** Each browser tab holds its own, independent investigation state in F# WebAssembly memory. No browser storage holds investigation documents or unsaved changes. No cross-tab locking is required or used.
2. **GitHub is the authoritative durable store.** Every save is one atomic `createCommitOnBranch` commit with an expected branch head, as OD-INDY-9 already defines. The interface says Saved only after GitHub confirms the commit and a read-back returns the written bytes.
3. **Conflict handling.**
   - A stale head is detected and classified.
   - A branch that moved only for unrelated changes is rebased automatically, a limited number of times.
   - When the same investigation changed remotely, the unsaved changes are kept and the user must choose an explicit resolution.
   - An uncertain commit outcome is reconciled before any retry.
   - No other writer's commit is ever overwritten.
4. **Offline.** Durable offline editing is removed. An investigation already loaded stays visible without connectivity. Edits made while disconnected stay in the tab's memory only. There is no offline synchronization queue. Save state is reported truthfully.
5. **Browser lifecycle.** The application warns before the user leaves a tab that has unsaved changes, where the browser supports it. Closing or terminating the browser may destroy unsaved work, and the application says so. There is no local crash recovery.
6. **Save-state interface.** A Forma-based, accessible indicator shows exactly seven states: Saved, Unsaved, Saving, Offline, Conflict, Unknown commit outcome and Save failed. It makes no false persistence claim.
7. **From requirements.** Rehearsal 7 builds this from the requirements during its timed run. Earlier rehearsal code is evidence, not a source.

## Why this, and not a cross-tab guard

| Option | Why not chosen |
|---|---|
| Keep the shared device store; guard it with Web Locks (Rehearsal 6) | The correctness of every write depends on an API that a target browser may lack. Rehearsal 6's no-locks half failed on every run |
| Keep the shared store; guard it with a localStorage lease | Tried twice in Rehearsal 6; neither design was correct. A lease needs clocks and expiry that tabs do not share reliably |
| Keep the shared store; adopt Arca's cross-tab support | Integration was too large for the clock in Rehearsal 6 (D-07). It still keeps a second, local truth that must be reconciled with GitHub, which is the root of R6-D-001 |
| Keep durable offline editing with a backoff queue | It fixes R6-D-003's symptom but keeps queued writes that run without the user, after a restart, against heads the tab never read |
| **Session-local state, GitHub only (chosen)** | Two tabs are two GitHub writers. The expected-head commit, which must be correct anyway for two devices, is the only concurrency mechanism. Nothing local can claim to be saved |

## Consequences

- **Lost capability:** an edit made offline, or an unsaved edit in a tab that is closed or terminated, is lost. The interface must say this (PERSIST-020), warn before leaving (PERSIST-019), and never call such an edit saved (PERSIST-032).
- **More visible conflicts:** the same investigation edited in two tabs is now a genuine conflict, resolved explicitly (PERSIST-011, PERSIST-012). Before, the shared store merged it silently, or lost it.
- **Less to build:** there is no local store, no restore, no queue, no cross-tab guard and no local/remote truth distinction.
- **Arca:** Arca stays in the release set. Rehearsal 7 may use a published library for GitHub access, but no library may store investigation content in the browser (PERSIST-026), so Arca's offline queue and IndexedDB bridge are out of scope.
- **Storage contract:** causa-data v1.0.0 and OD-INDY-9 are unchanged. Paths, file sets, serialization, atomicity, read-back and the rebase-or-conflict rule (§9.4.2) all stand. PERSIST-009 and PERSIST-011 make §9.4.2's "rebase when the investigation's files did not change" precise, and PERSIST-010 bounds it.

## Superseded requirements

The change amends 22 requirements of the Rehearsal 6 baseline `BL-CAUSA-INDY-INIT-856157a78d47` and supersedes 36 of their obligations. No requirement identifier is removed. Each superseded obligation is listed, with its Rehearsal 6 text and what replaces it, in [`DEFECT-TRACEABILITY.md`](DEFECT-TRACEABILITY.md#superseded-rehearsal-6-obligations). The main ones are:

- UI-006: the nine local/remote states are replaced by the seven save states.
- AR-035-O4: device-local durable state shared by several tabs is replaced by tabs sharing nothing but GitHub.
- R10.5, R10.6 and R10.7: offline tolerance, pending writes and restart recovery.
- D-204: IndexedDB or another browser store may be used.
- ADRP-011: the offline default.
- The kickoff packet's local durability step and 12:00 checkpoint.
- The interface freeze's Boundary D, local queue and extraction of interfaces from R001 source.

Long-term platform documents outside the baseline keep their direction, marked "Superseded for Causa (ADR-R7-001)" where they describe offline durability.

## Choices made in writing the requirements

The instruction left these open. The values below are in the requirements, and the owner can change any of them before the baseline is re-pinned.

| Choice | Value | Where |
|---|---|---|
| Automatic rebase attempts per save request | three, so at most four commit mutations | PERSIST-010 |
| Automatic reconciliation reads for one unknown outcome | three, at least 2, 4 and 8 seconds apart, plus one per offline-to-online transition | PERSIST-016 |
| Conflict resolutions offered | review, reapply my changes through the engine, discard with confirmation; never "overwrite GitHub" | PERSIST-012 |
| Save after reconnecting | only on a new save request; nothing is sent automatically | PERSIST-018 |
| Write marker | random per-tab session identifier and sequence, as a commit-message trailer | PERSIST-014 |
| Saves in flight per tab | one; others wait in memory | PERSIST-006 |
| Copy check | no F#, C#, JavaScript, TypeScript, CSS, HTML or shell file over 20 lines byte-identical to a file of Rehearsals 1 to 6 | PERSIST-052 |
| Interfaces | authored at kickoff from requirements; the R001 interface manifest is not an input | REHEARSAL-AGENT-INTERFACE-FREEZE.md |
