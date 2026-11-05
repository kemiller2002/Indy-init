# Integration handoff

This document defines the smallest interface the Causa application needs to load historical investigations, and how it connects to the existing contracts. No file owned by another agent is edited. Consumers read this branch; they do not need to change it.

## 1. Four separate layers

```text
 data-access adapter         decoder (pure)                projection (pure)               UI / report
 ───────────────────         ──────────────                ─────────────────               ───────────
 DocumentSource       ──▶    StorageReader.read      ──▶   HistoricalLoader.verify   ──▶   InvestigationState
 (bytes only)                Result<StructuralRead,        LoadResult                      (provider-neutral)
                                     ReaderError>          (StructurallyVerified |
                                                            LoadRejected)
```

| Layer | Type / function | Knows about | Does not know about |
|---|---|---|---|
| **Raw document retrieval** | `DocumentSource = { ListInvestigations; ReadInvestigation }` → `InvestigationFiles = { DirectoryName; Entries: (name * bytes) list }` (`src/HistoricalLoader.fs`, `src/StorageReader.fs`) | GitHub or the file system, the workspace path, credentials | events, schemas, replay. **It never implements domain replay.** |
| **Event decoding** | `StorageReader.read : InvestigationFiles -> Result<StructuralRead, ReaderError>`; per-line `StorageReader.outcomes : … -> LineOutcome list` | CONTRACT §2–§7, §11.1 stages 1–14 | domain rules, providers |
| **Domain projection** | `HistoricalLoader.verify : StructuralRead -> LoadResult`; `ReplayAdapter.replay : DecodedEvent list -> ReplayResult` | CONTRACT §8 fold, stages 16–17, adapter-checkable anomalies | providers, the UI |
| **Replay errors** | `ReaderError { Code: ReaderCode; File; Line; Detail }`; `ReplayAnomaly { Line; Kind; Detail }`; `Admissibility = NotEvaluated reason \| AdapterDetectedInadmissible` | — | — |

**The UI never sees a GitHub-specific envelope.** It receives `InvestigationState`: title, lifecycle, version, `createdAt`, and 17 entity maps whose entities each carry `Provenance { Actor; RecordedAt }`. `InvestigationState` contains no SHA, path, branch or token, and neither do `StructuralRead` or `HistoricalEnvelope` (CONTRACT §1.4). The commit the read happened at belongs to the caller, as `VerifiedInvestigation.ReadAt` does today.

## 2. Usage

```fsharp
open Causa.HistoricalCompatibility

// The integration agent supplies this; a file-system version is in tests/Source.fs (fileSystemSource).
let source : DocumentSource = githubSource repo "workspaces/demo/demonstration-v1/investigations" token

match HistoricalLoader.loadWorkspace source with
| Error e -> showConnectionError e                                     // listing failed
| Ok results ->
    for dir, result in results do
        match result with
        | StructurallyVerified h ->
            // h.State : InvestigationState  - render it
            // h.Admissibility = NotEvaluated _ - label the case "read-only, admissibility not evaluated" (OD-HC-04)
            show h.State
        | LoadRejected (err, conditional) ->
            // fail closed (CONTRACT §11.2): nothing partial is shown
            showSafeDiagnostic (ReaderCode.code err.Code) err.File err.Line err.Detail conditional
```

The steps can stop early: a caller can stop after `StorageReader.read`, for example to show a raw timeline. `HistoricalLoader.acceptance` reports Levels A, B and C separately for diagnostics.

## 3. Connection to the frozen v4.1.4 contracts

| Frozen element | Connection | Blocked by |
|---|---|---|
| `IF41-STORAGE.FileSet { Manifest; Events; Snapshot }` (`02-IF41-STORAGE.fs:9-12`) | maps one to one to `InvestigationFiles` (directory name = investigation id) | — |
| `StorageCodec.Verify : CommitOid -> FileSet -> Result<VerifiedInvestigation, VerificationFailure list>` (`:28-31`) | `StorageReader.read` followed by `HistoricalLoader.verify` **is** the Verify body, except for building the result | `VerifiedInvestigation.History : EventEnvelope list` cannot hold 487 of the 1,315 events or any event's provenance (`SCHEMA-DIFF.md` §1–§2), and `Investigation` comes from the domain kernel. Needs OD-HC-01. |
| `IF41-DOMAIN.Replay : EventEnvelope list -> Result<Investigation, ReplayError>` (`01-IF41-DOMAIN.fs:18`) | `ReplayAdapter` is a reference for the projection part. It can serve as the kernel's projection oracle: equal digests are the STORAGE-002 criterion (CONTRACT §8.4) | the kernel owns `Investigation` and admissibility (OD-HC-03) |
| `VerificationFailure` (`02-IF41-STORAGE.fs:14-20`) | see the mapping table below | the frozen set has no stage code; the proposal adds `ReaderStage` |
| `Repository { Owner; Name; Branch }` (`03-IF41-WIRE.fs:45`) | the workspace is a constructor argument of `DocumentSource`, not a contract field | writes still need a workspace in the contract (ODR-R10-25) |

**Mapping `ReaderError` to the frozen `VerificationFailure`**, usable today with no contract change:

| ReaderCode | Stage | Frozen `VerificationFailure` |
|---|---|---|
| `PathInvalid`, `Encoding`, `JsonMalformed`, `NonCanonical`, `UnsupportedVersion`, `UnknownEventType`, `SchemaViolation` | 1–7 | `SchemaInvalid(file, ReaderError.describe e)` |
| `ManifestMismatch`, `InvestigationMismatch` | 8, 10 | `ContractValueMismatch(field, expected, actual)` (details in `Detail`) |
| `EventsHashMismatch`, `SnapshotHashMismatch` | 9, 16 | `DigestMismatch "eventsSha256"` / `DigestMismatch "snapshotSha256"` |
| `EventIdDuplicate`, `SequenceInvalid`, `EventIdMismatch`, `CorrelationReused` | 11–14 | `ReplayRejected(NonContiguousVersion …)` for stage 12; `SchemaInvalid("events.ndjson", …)` otherwise |
| `HistoryInadmissible` | 15 | `ReplayRejected(IllegalEvent …)` |
| `SnapshotMismatch` | 17 | `SnapshotDiffersFromReplay [ "state" ]` |

## 4. How the adapter reaches the application

The adapter lives under `proposals/`. **OD-INDY-22** ("No rehearsal copies application code, tests, probes, tools or workflows from an earlier rehearsal's repository … What a rehearsal needs is written inside its own timed window from the requirements, the frozen packages and the released Echelon tools") means a rehearsal may **not** copy it. Three routes are open, each needing an owner decision (OD-HC-05):

| Route | What happens | Lead time before a launch |
|---|---|---|
| **R1. Released tool** | Publish the library as a versioned, released package, the way the Echelon tools are. The rehearsal references the package instead of copying source. | packaging, release, lock pin |
| **R2. Requirement asset** | Freeze `CONTRACT.md` §8.3 plus this suite's checks as verification assets in the successor package, like `verification/`. The rehearsal writes its own decoder and must pass them. | successor freeze cycle |
| **R3. Reference only** | Nothing is promoted. The rehearsal rebuilds the decoder from CONTRACT.md inside its window. These documents only shorten that work. | none, but costs rehearsal time |

## 5. For the UI-contract agent

These items are consumable without editing this branch:

- **Render from `InvestigationState` only** (§1). The fields available per entity are the CONTRACT §8.3 entity tables.
- **Requirement gaps** to fold into the UI contracts: `SCHEMA-DIFF.md` §6, covering INV-023, INV-025, INV-027, INV-028, INV-029, INV-030, INV-031, INV-034, INV-035, INV-038 and INV-002, with journeys J1-03, J2-01, J2-03, J2-04, J2-05, J2-06 and J2-09.
- **Historical cases are read-only.** None of the missing fields exists in them. A historical view must not offer edit affordances that cannot persist (UI-006-O6, ADRP-004-O6).
- **The `insufficient` relationship kind** occurs 9 times and has no INV-031 option. Its rendering is OD-HC-07.
- **Order by `aggregateVersion`, never by `occurredAt`.** In each case several events share one `occurredAt` (`SCHEMA-DIFF.md` §4).

## 6. For the optimization / minimum-data-browser agent

A minimum data browser needs only:

1. a `DocumentSource` over the GitHub tree and blob reads of `workspaces/demo/demonstration-v1/investigations`;
2. `HistoricalLoader.loadWorkspace`;
3. a view over `InvestigationState`.

It does not need `IF41-DOMAIN`. C16 measures the result: four cases, 1,363 events, loaded to the catalog digests. The full 20-check suite, C16 included, runs in about 9 s wall-clock on this container.
