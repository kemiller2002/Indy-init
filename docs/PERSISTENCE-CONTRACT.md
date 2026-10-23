# Persistence, Portability, and Recovery Contract

Status: normative persistence architecture direction.

The product must preserve investigation meaning independently of any storage provider. GitHub is the first provider and is useful because it gives auditable history, but GitHub must not become the semantic model.

> **Normative status.** **Normative** for persistence architecture: provider boundaries, local-first behaviour, conflict handling, import/export and recovery. **Explanatory** wherever it describes persisted bytes. Persisted data is governed only by the frozen Causa storage contract v1.0.0 ([README.md](../requirements/causa/data/v1.0.0/README.md), [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md), [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md)). Where this document states a persisted field, event name, enum value, layout, hash or concurrency rule differently, the contract governs; such statements are marked *Superseded by frozen contract v1.0.0* in place.

## Normative status of persistence documents

Every document below was reconciled against the frozen Causa storage contract v1.0.0 (full record: [CAUSA-CONTRACT-RECONCILIATION.md](CAUSA-CONTRACT-RECONCILIATION.md)). For any persisted field, event, enum value, layout, hash or concurrency rule, the governing file in the last column wins.

| document | status for persisted data | governed by (imported contract) |
|---|---|---|
| [requirements/causa/data/v1.0.0/](../requirements/causa/data/v1.0.0/README.md) | **normative, frozen** (the contract itself) | — |
| [REQUIREMENTS.md](REQUIREMENTS.md) R10, R10.8, CAUSA-DATA-001 | normative requirements; R10.8 delegates the format to the contract | [README.md](../requirements/causa/data/v1.0.0/README.md) |
| [FROZEN-DEMO-STORAGE-REQUIREMENTS.md](FROZEN-DEMO-STORAGE-REQUIREMENTS.md) | normative requirements STORAGE-001…009 (satisfied by v1.0.0); not a wire spec | [acceptance/ACCEPTANCE.md](../requirements/causa/data/v1.0.0/acceptance/ACCEPTANCE.md), [compatibility/RULES.md](../requirements/causa/data/v1.0.0/compatibility/RULES.md) |
| [CAUSA-STORAGE-RELEASE-v1.0.0.md](CAUSA-STORAGE-RELEASE-v1.0.0.md) | normative release identities (tag, digests, engine pin) | [compatibility/lock.json](../requirements/causa/data/v1.0.0/compatibility/lock.json), [compatibility/release-pin.json](../requirements/causa/data/v1.0.0/compatibility/release-pin.json) |
| PERSISTENCE-CONTRACT.md (this document) | normative architecture; explanatory for bytes; sections marked *non-normative for v1.0.0 storage* are future direction | [CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md) §3, §9–§11, §13; [INVARIANTS.md](../requirements/causa/data/v1.0.0/INVARIANTS.md) |
| [CAUSA-DATA-CONTRACT-v1.md](CAUSA-DATA-CONTRACT-v1.md) | explanatory (historical, superseded candidate) | [CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md), [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md), [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md) |
| [CAUSA-CONTRACT-RECONCILIATION.md](CAUSA-CONTRACT-RECONCILIATION.md) | explanatory (conflict resolution record) | [README.md](../requirements/causa/data/v1.0.0/README.md) |
| [COMMAND-EVENT-CATALOG.md](COMMAND-EVENT-CATALOG.md) | normative for commands and domain vocabulary; explanatory for persisted event names (see its §0) | [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md), [SEMANTICS.md](../requirements/causa/data/v1.0.0/SEMANTICS.md) |
| [STATE-TRANSITION-TABLES.md](STATE-TRANSITION-TABLES.md) | normative domain direction; persisted state values and implemented transitions per the contract | [SEMANTICS.md](../requirements/causa/data/v1.0.0/SEMANTICS.md), [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md) § Closed enumerations |
| [FSHARP-TYPE-SHAPES.md](FSHARP-TYPE-SHAPES.md) | normative typed-domain profile; explicitly not a wire schema | [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md), [FIELD-MAPPING.md](../requirements/causa/data/v1.0.0/FIELD-MAPPING.md) |
| [CORE-DOMAIN-MODEL.md](CORE-DOMAIN-MODEL.md) | normative conceptual model; explanatory for persisted shapes | [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md) |
| [INVESTIGATION-SEMANTICS.md](INVESTIGATION-SEMANTICS.md) | normative semantics; explanatory for persisted names and enums | [SEMANTICS.md](../requirements/causa/data/v1.0.0/SEMANTICS.md), [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md) |
| [ARCHITECTURE.md](ARCHITECTURE.md) | explanatory for layout and sync states | [CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md) §3, §9.4 |
| [POLICY-ENGINE.md](POLICY-ENGINE.md) | normative policy design; command envelope is not persisted | [CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md) §5 |
| [DOMAIN-TEST-MATRIX.md](DOMAIN-TEST-MATRIX.md) | normative test obligations; persisted names per the contract | [acceptance/TEST-MATRIX.json](../requirements/causa/data/v1.0.0/acceptance/TEST-MATRIX.json), [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md) |
| [SECURITY-THREAT-MODEL.md](SECURITY-THREAT-MODEL.md) | normative security posture; load validation per the contract | [CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md) §10–§11 |
| [OPEN-QUESTIONS.md](OPEN-QUESTIONS.md) OQ-006, OQ-012, OQ-029 | explanatory (research); answered for v1.0.0 where noted | [CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md) §3, §6, §10 |
| [DESIGN-DECISION-BOUNDARIES.md](DESIGN-DECISION-BOUNDARIES.md) D-202 | normative decision boundary; layout frozen for v1.0.0 data | [compatibility/RULES.md](../requirements/causa/data/v1.0.0/compatibility/RULES.md) |
| [INVESTIGATION-ACCEPTANCE-SCENARIOS.md](INVESTIGATION-ACCEPTANCE-SCENARIOS.md), [SEMANTIC-COMPILER.md](SEMANTIC-COMPILER.md), [UI-CONTRACT-EXPLORATION.md](UI-CONTRACT-EXPLORATION.md), [ADVANCED-VISUALIZATIONS.md](ADVANCED-VISUALIZATIONS.md) | explanatory domain vocabulary; persisted names annotated in place | [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md), [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md) |
| [`kickoff/indy-init.kickoff.json`](../kickoff/indy-init.kickoff.json) | lists the contract as a normative document | [README.md](../requirements/causa/data/v1.0.0/README.md) |

## 1. Three layers of persisted information

Separate:

1. **Authoritative semantic history**: what state changes occurred.
2. **Current projection/snapshot**: efficient representation of current state.
3. **Provider metadata**: GitHub SHA, database etag, service revision, local cache keys.

Provider metadata must not leak into generic domain identity.

## 2. Canonical portable package — *non-normative for v1.0.0 storage*

A complete portable investigation package should be able to contain:

```text
manifest.json
investigation/
  snapshot.json
  events.ndjson
extensions/
  ...
reports/
  manifest.json
artifacts/
  manifest.json
integrity/
  manifest.json
```

> **Superseded by frozen contract v1.0.0:** an export package MUST embed the v1.0.0 investigation directory (exactly `manifest.json`, `events.ndjson`, `snapshot.json`) byte-for-byte; it is not itself a v1.0.0 storage layout (CONTRACT §3.3).

The exact physical layout is ReversibleChoice. ***Superseded by frozen contract v1.0.0:** for v1.0.0 storage the layout is fixed by CONTRACT §3; only an export-package layout remains a ReversibleChoice.*

The semantic contents and round-trip guarantees are the important contract.

## 3. Package manifest — *non-normative for v1.0.0 storage*

The manifest should identify:

- package format version
- InvestigationId
- exported aggregate version
- export timestamp
- exporter identity/version
- core schema/semantic version
- domain packs
- policy references
- required/optional semantic dependencies
- included sections
- external artifact references
- integrity information
- redaction status
- encryption status if applicable

> **Superseded by frozen contract v1.0.0:** these belong to an export-package manifest, never to the per-investigation `manifest.json`, which is closed with exactly 12 fields (CONTRACT §9.1, `investigation-manifest.schema.json`).

## 4. Canonical serialization

Requirements:

- deterministic field semantics
- stable IDs
- explicit versions
- explicit null/unknown semantics where material
- ~~extension passthrough~~ *Superseded by frozen contract v1.0.0: unknown fields and unregistered events are refused (fail closed), never passed through or dropped (CONTRACT §2.4, §7.1, §13.5)*
- no provider-specific identity requirement
- documented date/time representation
- documented ordering rules where order has meaning
- ~~unknown fields preserved when possible~~ *Superseded by frozen contract v1.0.0: see the previous bullet*

Serialization convenience must not redefine domain meaning.

## 5. Event envelope

Every semantic event should serialize with:

- EventId
- InvestigationId
- AggregateVersion
- EventType semantic identifier
- occurred/recorded timestamps as applicable *(Superseded by frozen contract v1.0.0: `occurredAt` only; `recordedAt` removed)*
- Actor
- PolicyRefs *(Superseded by frozen contract v1.0.0: not in the envelope; the cause-gate policy id is `CausalAssertionConfirmed.policy`)*
- SemanticManifest *(Superseded by frozen contract v1.0.0: `formatVersion` and `semanticVersion` strings, both `"1.0.0"`)*
- CorrelationId optional *(Superseded by frozen contract v1.0.0: `correlationId` is required, equals the producing command id, one id per command group)*
- CausationId optional *(Superseded by frozen contract v1.0.0: removed)*
- Payload
- Provenance *(Superseded by frozen contract v1.0.0: removed from the envelope; entity provenance `{actor, recordedAt}` is derived)*
- Integrity metadata optional *(Superseded by frozen contract v1.0.0: none per event; integrity is per file via manifest `eventsSha256`/`snapshotSha256`)*

v1.0.0 wire names: `formatVersion`, `eventId`, `investigationId`, `aggregateVersion`, `eventType`, `occurredAt`, `actor`, `semanticVersion`, `correlationId`, `payload` ([EVENT-CATALOG.md § Envelope](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md)).

## 6. Event ordering

AggregateVersion is the authoritative logical order within one investigation.

Wall-clock timestamps must not be relied upon to establish exact semantic order.

## 7. Snapshot envelope

A snapshot should identify:

- InvestigationId
- AggregateVersion represented
- projection version
- semantic manifest *(Superseded by frozen contract v1.0.0: `formatVersion`, `semanticVersion`, `projectionVersion` = 1)*
- snapshot generated-at *(frozen contract v1.0.0: `generatedAt` = `occurredAt` of the last applied event; deterministic)*
- integrity metadata *(Superseded by frozen contract v1.0.0: the snapshot has none; its digest is manifest `snapshotSha256`)*

A snapshot is invalid if its aggregate version does not match its declared source history. *(v1.0.0: it must be byte-identical to the replayed projection, `CAUSA.SNAPSHOT_MISMATCH`, CONTRACT §8.1.)*

## 8. Snapshot authority

Snapshots are convenience, not independent truth.

When authoritative event history is available:

- a snapshot may be regenerated
- a stale snapshot may be discarded *(Superseded by frozen contract v1.0.0: a stored snapshot that differs from the replayed projection makes the investigation invalid; readers fail closed and repair nothing (CONTRACT §8.1, §11.2). A writer replaces it only within a new atomic append.)*
- a snapshot cannot introduce events that do not exist in authoritative history

A persistence provider that chooses snapshot-only storage must explicitly accept the reduced reconstruction/audit guarantees. ***Superseded by frozen contract v1.0.0:** not permitted for v1.0.0 data; there is no events-only or snapshot-only profile (CONTRACT §8.1).*

## 9. Provider contract

Logical provider capabilities:

```text
LoadCurrent(investigationId)
LoadHistory(investigationId, range)
Append(expectedVersion, events)
StoreSnapshot(expectedVersion, snapshot)   # superseded for v1.0.0: snapshot + manifest are written atomically with Append (CONTRACT §9.4.1)
ListInvestigations(workspace/query)
StoreArtifactReference(...)
Export(...)
Import(...)
Health(...)
```

Not every provider must implement every optional capability, but capability support must be discoverable.

## 10. Provider capability declaration

A provider should declare capabilities such as:

- EventHistory
- Snapshots
- OptimisticConcurrency
- AtomicAppend
- Search
- Transactions
- OfflineSync
- ArtifactStorage
- ImmutableHistory
- BackupRestore
- EncryptionAtRest
- ServerSideAuthorization

Product behavior adapts explicitly rather than assuming every backend behaves like GitHub. A v1.0.0 provider MUST support EventHistory, Snapshots, OptimisticConcurrency, AtomicAppend and ImmutableHistory (CONTRACT §9).

## 11. GitHub provider mapping — *non-normative for v1.0.0 storage*

Initial GitHub design may use:

```text
investigations/<id>/
  manifest.json
  current.json
  events.ndjson
  extensions/
  artifacts.json
  reports/
```

> **Superseded by frozen contract v1.0.0:** the v1.0.0 layout is `workspaces/demo/<runId>/investigations/<investigationId>/{manifest.json, events.ndjson, snapshot.json}` (CONTRACT §3). `current.json` is `snapshot.json`; extensions, artifacts and reports have no v1.0.0 location inside an investigation directory.

But the adapter owns these paths.

The generic domain receives:

- investigation data
- aggregate version
- provider version token

It does not receive GitHub-specific paths as domain concepts.

## 12. GitHub concurrency

~~GitHub adapter may use file/blob SHA or commit base as provider version token.~~ ***Superseded by frozen contract v1.0.0:** the provider version token is the branch head commit the writer read; a non-fast-forward ref update is a conflict; blob SHAs only decide whether the investigation's files changed (CONTRACT §9.4.2).*

A write must compare against the expected remote provider version.

If it changed:

- do not overwrite
- return provider conflict
- preserve local commands
- load remote state
- revalidate queued commands semantically

If the investigation's files are unchanged at the new head, the writer rebases and retries (CONTRACT §9.4.2(a)).

## 13. Atomicity

A provider should make a semantic transition appear atomically committed.

If one domain decision emits multiple events, partial remote persistence must not be treated as success.

For GitHub, this may require committing all related file changes in one commit/tree operation rather than sequential independent writes. ***Superseded by frozen contract v1.0.0:** MUST: one Git commit containing events, manifest and snapshot, published by a non-forced ref update (`PATCH /git/refs`, `force: false`); sequential Contents-API writes do not conform (CONTRACT §9.4.1).*

## 14. Commit message semantics

GitHub commit messages are useful audit metadata but are not the semantic event source.

The semantic event payload remains authoritative.

## 15. Append integrity

Where events are append-oriented:

- prior event sequence cannot be silently rewritten
- AggregateVersion must be monotonic
- EventId must be unique
- duplicate idempotent append is detectable
- gaps/duplicates must be detectable

## 16. Hash chaining — *non-normative for v1.0.0 storage*

Optional integrity enhancement:

Each event or event batch may include a digest referencing prior authoritative digest.

This can detect accidental or malicious rewriting in exports/providers that do not themselves guarantee immutable history.

Hash chaining is an integrity mechanism, not a substitute for signatures or access control.

> **Superseded by frozen contract v1.0.0:** not representable in v1.0.0 events (closed envelope, no digest field); any chain must live outside the investigation files or in a new contract version. v1.0.0 detects rewriting through the byte-prefix successor rule (`CAUSA.HISTORY_REWRITTEN`, CONTRACT §9.3) and manifest digests (§10).

## 17. Artifact references — *non-normative for v1.0.0 storage*

Large evidence artifacts should use typed references.

Fields may include:

- ArtifactId
- URI/location
- media type
- size
- digest
- captured-at
- provenance
- access classification
- availability state
- retention metadata

A missing external artifact does not erase the evidence record; availability becomes explicit.

> **Superseded by frozen contract v1.0.0:** v1.0.0 persists only the artifact URL: `EvidenceAdmitted.content` (absolute http(s) URL) with `contentKind: "artifact"`. The other fields require a new contract version.

## 18. Artifact integrity — *non-normative for v1.0.0 storage*

When practical, externally referenced evidence should record a cryptographic digest at admission time.

Later retrieval can distinguish:

- available and matching
- available but changed
- unavailable
- digest not recorded

Changed external content must not silently replace what was originally evaluated. *(Admission-time digests are not representable in v1.0.0; requires an additive v1.x.)*

## 19. Offline local store

The browser local store should contain:

- last confirmed authoritative snapshot
- provider version token
- local queued commands
- optimistic local projection
- synchronization state
- necessary extension payloads/manifests
- minimal metadata required to recover after restart

Secrets should not be stored there unless explicitly justified by the authentication design.

## 20. Local truth distinction

The client should distinguish:

- RemoteConfirmed
- LocalAppliedPending
- Conflict
- SyncFailed
- RevalidationRequired

A user should be able to tell whether a change exists only on the device.

> **Superseded by frozen contract v1.0.0:** pending, **sending**, conflicted and **unknown** (outcome not confirmed) remain distinct states; SyncFailed is used only when the provider confirmed the write did not happen, otherwise the state is unknown; a network failure is never reported as success (CONTRACT §9.4.3).

## 21. Restart recovery

After application restart:

1. load last durable local state
2. show last known remote confirmation state
3. recover queued commands
4. reconnect provider when possible
5. fetch remote version
6. revalidate queue
7. sync or surface conflicts

Unsynced work must not disappear merely because the browser restarted.

## 22. Queue durability

A queued command should not be removed until:

- remote append is confirmed, or
- the user explicitly discards it, or
- reconciliation replaces it with a new command while preserving history of the failed/stale intent when policy requires

## 23. Conflict classes

Conflicts should distinguish:

- ProviderVersionConflict
- SemanticConflict
- ExtensionVersionConflict
- PolicyVersionConflict
- PermissionConflict
- ArtifactConflict

Not every conflict is a text merge. v1.0.0 persisted-conflict codes: `CAUSA.VERSION_STALE`, `CAUSA.HISTORY_REWRITTEN`.

## 24. Semantic merge

The product should prefer command/state reconciliation over raw JSON merge.

Examples:

- two users independently add evidence: often commutative
- one user falsifies H1 while another edits H1 wording: requires semantic review
- one user resolves an unknown while another marks it blocking: requires policy/state reconciliation

Generic merge algorithms must not silently decide semantic conflicts.

## 25. Commutativity metadata

Commands/events may declare whether operations are known to commute safely.

This can enable automatic reconciliation for independent additions.

Example candidates:

- add separate Observation
- add unrelated Evidence
- add comment/discussion

Protected transitions should default to revalidation. *(Commutativity metadata is application-level; it is never persisted in v1.0.0 events.)*

## 26. Import

Import must validate:

- package format
- schema compatibility
- semantic dependencies
- IDs
- event sequence
- integrity metadata
- extension availability
- policy references
- collisions with existing InvestigationId

Import cannot silently assign new semantic meaning. Import of a v1.0.0 investigation is the full CONTRACT §11.1 verification.

## 27. Import identity collision

If InvestigationId already exists, options should be explicit:

- reject duplicate
- recognize same history/version
- import as related copy with a new ID and provenance
- reconcile through supported merge flow

Never silently overwrite.

## 28. Export

Export should support:

- complete authoritative package
- current snapshot only when user understands reduced history
- redacted package
- report-only output

These are different products and must be labeled accurately.

## 29. Round-trip guarantee

Complete canonical export followed by import into a compatible implementation must preserve:

- InvestigationId unless intentionally copied
- authoritative events
- entity IDs
- extension envelopes
- semantic/policy versions
- historical conclusions
- unresolved uncertainty
- provenance
- report snapshot references
- artifact metadata

## 30. Unknown field/extension round-trip — *non-normative for v1.0.0 storage*

A compatible reader that does not understand optional extension semantics must preserve them if it permits save.

If it cannot preserve them, it must be read-only.

> **Superseded by frozen contract v1.0.0:** a v1.0.0 reader refuses data it cannot interpret (fail closed); it never loads it read-only or best-effort (CONTRACT §13.2, §13.5).

## 31. Migration

Migration must operate at explicit boundaries:

- package format migration
- core schema migration
- event upcast
- domain extension migration
- policy reassessment

These are not one generic "migration." Migrations write new files in a new namespace; v1.0.0 files are never rewritten ([compatibility/RULES.md](../requirements/causa/data/v1.0.0/compatibility/RULES.md)).

## 32. Backup

Provider-specific backup strategy is required for production use.

Backups should preserve:

- authoritative history
- manifests
- extension data
- artifact metadata
- provider-independent export ability

## 33. Restore

Restore must validate integrity before replacing authoritative state.

Restoring an older version over newer authoritative state requires explicit destructive/branching semantics. ***Superseded by frozen contract v1.0.0:** restoring never overwrites newer v1.0.0 history; it creates a new investigation or workspace (CONTRACT §9.3, §9.4.2).*

## 34. Fork/branch investigation — *non-normative for v1.0.0 storage*

The platform should support creating a new investigation derived from another at a known version.

Use cases:

- research comparison
- alternate policy analysis
- what-if reasoning
- anonymized copy

The derived investigation records:

- new InvestigationId
- source InvestigationId
- source AggregateVersion
- derivation reason
- copied semantic manifest
- subsequent independent history

This is different from rewriting the original. *(No `InvestigationForked` event exists in v1.0.0; derivation metadata is recorded outside v1.0.0 files until a versioned extension exists.)*

## 35. Research snapshots

Research experiments should be able to pin an immutable evidence/investigation snapshot.

The experiment records the exact investigation version and semantic/policy versions used.

Later investigation changes do not alter the research input.

## 36. Deletion and archival

Archival is domain lifecycle.

Physical deletion is storage/governance behavior.

They must not be conflated. *(No v1.0.0 command reaches `archived`.)*

Deletion behavior must account for:

- audit requirements
- legal/privacy requirements
- shared external artifacts
- derived indexes
- backups
- reports already issued

## 37. Redaction — *non-normative for v1.0.0 storage*

Redaction may replace or hide sensitive content while preserving:

- that redaction occurred
- authority
- reason/category
- affected record
- timing
- integrity implications

Where policy requires true deletion, the system must not falsely claim complete historical reconstruction is still possible. *(No `RedactionRecorded` in v1.0.0; v1.0.0 files are immutable, so redaction requires a new version/namespace.)*

## 38. Search projections

Search indexes are derived.

They may contain:

- investigation metadata
- entity summaries
- tags
- current states
- unresolved obligations
- domain-specific indexed fields

Index corruption/loss cannot corrupt authoritative investigation state.

## 39. Report persistence

Issued report metadata should preserve:

- ReportId
- source InvestigationId
- source AggregateVersion
- report template/version
- domain pack/report projection versions
- generation timestamp
- digest/location if retained

Reports are historical outputs, not the source of domain truth.

## 40. Cross-provider migration

Moving an investigation from GitHub to another provider should be:

```text
Export canonical package
  -> validate
  -> import
  -> verify aggregate/version
  -> optionally switch provider pointer
```

Provider metadata changes.

Investigation semantics do not.

## 41. Availability degradation

If persistence is unavailable:

- active local investigation may continue within allowed offline policy
- changes remain clearly pending
- operations requiring remote-only data/capabilities are unavailable with explanation
- reports state whether they include unsynced local state when relevant

## 42. Provider failure

Provider failures should map to Aegis-compatible structured faults.

Distinguish:

- authentication
- authorization
- unavailable
- rate limited
- conflict
- integrity failure
- malformed persisted data
- unsupported provider capability

## 43. Integrity verification

On load, the adapter should validate enough structure to detect: ***Superseded by frozen contract v1.0.0:** run the full CONTRACT §11.1 check order (17 stages, including engine admissibility, §11.3) and report the first failing stage; "broken required extension payload" does not apply (no extensions).*

- impossible aggregate version
- malformed required manifest
- broken required extension payload
- duplicate event IDs
- missing required event sequence
- snapshot/event mismatch

Failure should not be papered over by AI reconstruction.

## 44. Corruption recovery

When persisted data is inconsistent:

1. do not silently "fix" authoritative history
2. identify the inconsistency
3. preserve recoverable raw data
4. reconstruct from trusted source where possible
5. produce recovery evidence
6. require explicit acceptance if recovery changes authoritative state *(**Superseded by frozen contract v1.0.0:** recovery output is written to a new workspace/investigation; v1.0.0 files are never changed in place, CONTRACT §11.2)*

## 45. Canonical data contract ownership

The core investigation package belongs to the Root-Cause Investigator platform.

GitHub adapter, EDF pack, Folio, Forma, Limen, and AI providers consume it but do not independently redefine it. Its persisted form is the frozen Causa contract v1.0.0 ([README.md](../requirements/causa/data/v1.0.0/README.md)).

## 46. First-release pragmatic subset

The hackathon implementation may choose:

- GitHub as only remote provider
- current snapshot + append event file
- IndexedDB/local browser persistence
- URL-referenced external artifacts
- simple export package

provided it preserves the boundaries required to add another provider later.

## 47. Completion criterion

Persistence architecture is healthy when:

- an investigation can move providers without changing its ID/meaning
- offline work survives restart
- stale writes never silently overwrite
- semantic conflicts are not reduced to text merge
- unknown extensions survive compatible round trips *(Superseded by frozen contract v1.0.0: unknown data is refused, never dropped, CONTRACT §13.5)*
- issued reports retain source version identity
- old investigations remain interpretable as schemas and policies evolve
