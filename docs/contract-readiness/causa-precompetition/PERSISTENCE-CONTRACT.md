# Persistence and durability contract

**Status:** candidate. This document indexes and binds the existing authorities. It adds no state, input or transition.

- **Storage:** causa-storage v1.0.0, CONTRACT §3, §8, §9 and §11 (causa-data `e8259df`).
- **Persistence boundary:** `requirements-compiled/v4.1.5/contracts/02-IF41-STORAGE.fs`, `03-IF41-WIRE.fs`, `04-IF41-ERRORS.fs` and `05-IF41-SAVE.fs`.
- **State machines:** `state-machine-contracts.json`, sha256 `d58d21f9…bc2`. SM-SAVE, SM-CONFLICT, SM-LOAD, SM-CONNECTION and SM-NETWORK together decide 354 (state, input) pairs.
- **Requirements:** PERSIST-*.

The validator checks the machine-readable parts in `STATE-TRANSITIONS.json`:

- **CR-ST-02:** each machine's states and inputs equal the parsed F# unions.
- **CR-ST-03:** the seven durability states, and that only a read-back verified state is durable.

The contract is independent of the storage implementation. GitHub is the one provider the requirements name.

## 1. Paths and identity

| Item | Contract |
|---|---|
| Workspace | `workspaces/demo/<runId>/`, with `runId` matching `^[a-z0-9][a-z0-9-]{0,63}$` (§3.2). A **catalog** workspace is derived from `catalog/active.json` and is read-only. A **working copy** is only `workspaces/demo/causa-app-<run>`, never a workspace a catalog names (v4.1.5 W1: `WorkspaceAddress.catalog` / `.workingCopy`, `Writable.ofAddress` refuses a catalog). |
| Investigation | `<workspace>/investigations/<investigationId>/` holds **exactly** `events.ndjson`, `manifest.json` and `snapshot.json` (§3.3). `InvestigationRef = { Address; Id }`: the same id can exist in two workspaces. |
| Write scope | A commit may differ from its base only under that investigation's prefix (§9.4.4). `fixtures/` and `contract/` are never written. |
| Serialization version | `formatVersion`/`semanticVersion` `1.0.0`, `snapshotVersion`/`projectionVersion` `1`. A v1.0.0 reader refuses every other version (`CAUSA.UNSUPPORTED_VERSION`, §13.2). The proposed 1.1.0 (OD-CR-01) is written only to new working copies, never into a v1.0.0 investigation in place. |
| Content identity | `manifest.eventsSha256` is the sha256 of the `events.ndjson` bytes. `snapshotSha256` and `expectedStateSha256` are canonical digests (§2.11, §8.4). The manifest holds **no** Git SHA, branch, path or token (§9.1). |
| Blob and commit identity | Kept only in the persistence layer: `CommitOid` (02), the head the writer read (`SavedBasis`), and `Confirmation.T { Investigation; Commit; CommittedAt }`. Blob SHAs are provider details and are never written into the data. |

## 2. Write protocol (GitHub)

1. **Atomic commit.** The appended events, the rebuilt manifest and the rebuilt snapshot of one command group are one commit, published by a non-forced ref update (§9.4.1). PERSIST-006's GraphQL `createCommitOnBranch` is the recorded owner exception (CT-007, OD-INDY-9). Per-file Contents-API writes do not conform.
2. **Optimistic concurrency.** The token is the branch head the writer read (§9.4.2):
   - A non-fast-forward is a conflict. `StaleHeadUnrelated` means another investigation changed: rebase, at most three attempts, then `SaveFailed RepositoryKeptChanging` (T-S rows of SM-SAVE). `StaleHeadSameInvestigation` means this investigation changed: `Conflict` and SM-CONFLICT.
   - A command carrying a stale `ExpectedVersion` is refused with `CAUSA.VERSION_STALE`. The pending command is kept.
   - Never overwrite or force-push.
3. **Successor rule.** A new file set is accepted only if its `events.ndjson` starts with the previously accepted bytes (§9.3). Otherwise `HistoryWasRewritten` / `CAUSA.HISTORY_REWRITTEN`.
4. **Write marker and idempotency.** Every write carries `WriteMarker { Session; Sequence }`. After a lost answer (`AnswerLost` → `UnknownOutcome`), reconciliation reads the marker before any retry:
   - `ReconcileMarkerBytesMatch` → Saved;
   - `…BytesDiffer` → SaveFailed `BytesDiffer`;
   - `…NothingWrittenHeadUnchanged` → retry is safe;
   - `…HeadMoved` → rebase or conflict;
   - `ReconcileReadsSpent` → stays unverified until the user chooses `CheckAgain`.
5. **Retry.** Retry only through those reconciliation inputs, and never blindly. A refusal from the provider (`ProviderRefused`, `CredentialRefused`) is not retried automatically. `Retry` is offered only when it performs a real action (UI-010).
6. **Network failure and offline.** `WentOffline` moves `Unsaved` to `Offline`, where no write is attempted and edits stay local; a save already in flight stays `Saving` until its answer or reconciliation. `FailedBeforeSending` (in `Saving`) moves to `Offline`; the edits stay local. A network failure is never reported as success (§9.4.3).
7. **Read-after-write.** Success requires the ref update **and** a re-read at the new head that returns the written bytes. `Confirmation.ofReadBack` constructs a confirmation only when `written = readBack`, and its representation is private, so nothing else can build one (PERSIST-023).
8. **Credentials.** Tokens travel only in the `Authorization` header. They are never written to any file, commit message, fixture or log (§9.4.5). CR-SEC-01 scans every package file for credential patterns.

## 3. Durability states

A change may be **displayed as durably saved only in PS-VERIFIED**: after the real provider's read-back has succeeded.

| Id | State | v4.1.5 `SaveState` | Durable | May show "Saved" | Basis |
|---|---|---|---|---|---|
| PS-LOCAL | Local changes | `Unsaved` | no | no | Edits applied to the session; nothing sent. |
| PS-PENDING | Save pending | `Saving` (before an answer) | no | no | Mutation prepared or sent. |
| PS-ACK | Provider write acknowledged | `Saving` (answer received, read-back outstanding) | no | no | An acknowledgement is not durability. |
| PS-VERIFIED | Provider read-back verified | `Saved` (`SavedBasis.Confirmed` read-back, or `VerifiedAt` loaded and verified) | **yes** | **yes** | Bytes re-read at the commit equal the bytes written. |
| PS-CONFLICT | Save conflict | `Conflict` | no | no | SM-CONFLICT: review, reapply, discard, or export the local commands. |
| PS-FAILED | Save failed | `SaveFailed` | no | no | `Refused`, `RepositoryKeptChanging`, `BytesDiffer`, `HistoryWasRewritten`, `CredentialNotAccepted`. |
| PS-OFFLINE | Offline / unverified | `Offline`, `UnknownOutcome` | no | no | No write attempted, or the outcome is unknown until reconciled. |

The seven `SaveState` cases correspond to the seven UI save states (PERSIST-031, UI-006, `SaveStateKind` in 07-IF41-VIEW). An unlisted (state, input) pair fails closed with a typed `SaveFault`, and state, base head and unsaved changes are unchanged (`state-machine-contracts.json` `invalidTransition`).

## 4. Loading, reopening and historical import

- **Open.** `LoadInput.OpenRequested of InvestigationRef` reads the head, then the three files at that commit. The files are verified as a reader would verify them (§11.1, stage codes carried by `VerificationFailure.ReaderStage`).
- **Fail closed.** Invalid data is shown as `UiFailure.InvalidData`, and nothing partial is shown.
- **Reopening a persisted investigation** restores exactly the verified history (INV-017, PERSIST-021). The real-provider read-back is the obligation CR-LIVE-01.
- **Historical import** is a read of the active catalog (`ListingTarget.ActiveCatalog`). It creates **no save session**: T-L03 was replaced by v4.1.5, and `Writable` refuses a catalog. Until the kernel refuses all 35 inadmissible fixtures (OD-HC-03 A), catalog cases are shown read-only and labelled "admissibility not evaluated" (OD-HC-04 A).
- **Copying a catalog case into a working copy** is not specified by any approved requirement and is not part of this contract.
- **Schema migration.** None is required. Every stored file is 1.0.0 (inventory `requiredMigration`). causa-storage 1.1.0 would be additive (§13.1). Readers of 1.1.0 read v1.0.0 unchanged, and writers never upgrade a v1.0.0 investigation in place (§13.2). A future 2.0.0 migration would write new files in a new namespace and never rewrite v1 files.

## 5. Provider doubles and real-provider evidence

A provider double may test protocol behaviour: every SM-SAVE pair, reconciliation, and conflict paths. It **cannot** establish durability. Durability (PS-VERIFIED against GitHub, INV-037, J3-05) is established only by the live-provider preflight (`.github/workflows/live-provider-preflight.yml`) or the governed journeys against the real repository.

This validator had no credential, so **CR-LIVE-01 is UNVERIFIED**, and no local mock is substituted for it.
