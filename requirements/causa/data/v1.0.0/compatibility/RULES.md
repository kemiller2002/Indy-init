# Causa storage compatibility and migration rules (v1.0.0)

Normative. Requirement ids `CAUSA-COMPAT-*`. This file consolidates CONTRACT.md §13–§14, the accepted compatibility
decisions of experiment EX-CAUSA-STORAGE-2026-001 (`research-documents/research/experiments/EX-CAUSA-STORAGE-2026-001/COMPATIBILITY-DECISIONS.md`)
and the requirements-first process of CAUSA-DATA-001. Where this file and CONTRACT.md differ, CONTRACT.md wins and the
difference is a defect in this file.

## 1. Pinned release identity

| item | value |
|---|---|
| contract version | `1.0.0` (`formatVersion` = `schemaVersion` = `semanticVersion` = `"1.0.0"`; `snapshotVersion` = `projectionVersion` = `1`) |
| release tag | `causa-storage-v1.0.0` in `kemiller2002/causa-data` |
| tag object / commit | `fd2991a392e913b741f0ed2bc4daa4bc4be57f6c` / `c947c7e354be80766573a782546c8186336774cc` |
| schema identity (SHA-256 of `schemas/bundle.json` bytes) | `0a68af8c93190d2e4d6c27beb2f8cb67c3aebdda586d01beb7122b71af179152` |
| dataset identity (SHA-256 of `fixtures/manifest.json` bytes) | `f650a2e509a420872b46177b21f57ef6ebd7625ae4c69dd92198d3ca472c2b86` |
| release identity (SHA-256 of `compatibility/lock.json` bytes) | `6a7e7802ff2a3ff500ab6065662514a96e09e6c15eb30212ba1377fcc5bf8fc5` |
| semantic oracle | `kemiller2002/indy-init-rehearsal-001@55f49f61c939a3a780cee592f36f732a1328f981`, assemblies pinned in `lock.json.engineAssemblies`, .NET SDK `10.0.402` (`global.json`, `rollForward: disable`) |

These values are machine-readable in [release-pin.json](release-pin.json). Changing any of them is a contract change.

## 2. Change classes (CAUSA-COMPAT-01)

Every proposed change MUST be classified into exactly one class before it is implemented. If the classification is in
doubt, the stricter class applies.

| class | examples | version effect | required gate |
|---|---|---|---|
| **A. Compatible implementation change** | a new consumer, a refactored adapter, a faster validator, new tests, new tooling | none | schema identity, dataset identity and release identity unchanged; all golden digests unchanged; every negative fixture rejected with its expected code |
| **B. Compatible additive extension** | a new event type, a new optional field, a new enum value, a new snapshot array | new minor version `1.x.0` with its own `contract/1.x.0/` tree in causa-data and `requirements/causa/data/v1.x.0/` here | v1.0.0 readers MUST reject the new data (fail closed); v1.x readers MUST read all v1.0.0 data unchanged with identical digests |
| **C. Explicitly versioned migration** | a renamed field, a changed id derivation, a projection change, a changed canonical form, the candidate catalog's narrow event names (CD-EVT-02) | new major version `2.0.0` plus a migration | the migration reads v1, writes **new** files in a new namespace, and is deterministic, repeatable and independently tested; v1 files are never rewritten |
| **D. Breaking semantic change** | changing what an existing event means; the engine accepting a history it used to refuse, or the reverse; changing a gate | new `semanticVersion` and explicit owner authorization | every frozen fixture is replayed under the new engine and reviewed; any changed digest or verdict is a FAIL until authorized |

Notes:

1. "A new optional field" under class B means a new *nullable required key* in a new minor contract (v1 has no optional
   keys, CD-WIRE-02). It is still invisible to v1.0.0 readers, because they reject the newer `formatVersion`.
2. A change to the engine that alters any decision for any frozen fixture or golden refusal is class D, whatever its
   size.
3. Editorial changes to CONTRACT.md or any schema file under `contract/v1.0.0` are forbidden. They change frozen bytes.
   Clarifications go in these requirements as explanatory text, or in a new contract version.

### 2.1 Version-field effect of each class (CAUSA-COMPAT-01a, review F-14)

| class | `formatVersion` | `schemaVersion` | `semanticVersion` | `snapshotVersion` / `projectionVersion` | schema identity | contract tree / tag |
|---|---|---|---|---|---|---|
| A implementation | unchanged | unchanged | unchanged | unchanged | unchanged | none |
| B additive extension | `1.x.0` | `1.x.0` (always equal to `formatVersion`) | unchanged unless the extension changes meaning (then also D) | unchanged unless the projection changes (then C) | new | `contract/1.x.0/`, tag `causa-storage-v1.x.0` |
| C versioned migration | `2.0.0` | `2.0.0` | unchanged unless meaning changes (then also D) | incremented if the §8.3 rules change | new | `contract/2.0.0/` + migration |
| D semantic change | unchanged if wire shapes are unchanged | unchanged | new value (minor for a strictly narrower admissible set, major otherwise) | unchanged unless the projection changes | unchanged if no schema byte changes | new SEMANTICS version under `requirements/causa/data/<version>/` |

Further rules:
1. A history never mixes versions. Every line, the manifest and the snapshot of one investigation carry the same
   `formatVersion`/`schemaVersion`/`semanticVersion` (CONTRACT §13.2: writers never append newer events to an older
   history).
2. `schemaVersion` names the schema set. The schema identity (bundle digest) pins its bytes. Two releases with the same
   `schemaVersion` MUST have the same schema identity.

### 2.2 Negative-fixture coverage (summary of the external CONTRACT §14 coverage list, review F-20)

CONTRACT §14 points to `ADVERSARIAL-REVIEW.md` in research-documents for the list of decision rules covered by the
frozen negatives. That list is now reproduced as the coverage table in SEMANTICS.md §18.1, together with the
supplementary fixtures in [../acceptance/supplementary/README.md](../acceptance/supplementary/README.md). Read
together, the two tables are the authoritative coverage statement for v1.0.0. The CD-* decisions cited in CONTRACT §16
are reproduced in §5 below.

## 3. Reader and writer rules (CAUSA-COMPAT-02)

1. A v1.0.0 reader reads exactly v1.0.0. It rejects every other `formatVersion`/`schemaVersion`/`semanticVersion` string
   with `CAUSA.UNSUPPORTED_VERSION` and never reads newer data "best effort".
2. A v1.x writer MUST NOT emit data that a v1.0.0 reader would accept with a different meaning.
3. Writers always write the newest version they implement, and only into new workspaces. They never upgrade a v1.0.0
   investigation in place, and never append newer-version events to a v1.0.0 history.
4. Unknown data is refused, never dropped (§13.5). Migration tooling preserves it byte for byte by not rewriting the
   source.
5. v1.0.0 has no extension namespace. Extensibility exists only through classes B–D.

## 4. Invariants every future release MUST keep (CAUSA-COMPAT-03)

Machine-checked by `causa gate . --anchor causa-storage-v1.0.0` in causa-data and by
`tools/causa-contract/parity_gate.py` plus the acceptance suite here:

1. The same schema identity, or an approved compatibility path (class B/C/D) with its own version.
2. The same frozen fixture hashes: every `frozenFiles` digest in `lock.json`.
3. The same replay semantics: an identical `expectedStateSha256` for every frozen scenario.
4. No unapproved field changes: no added, removed or renamed field, and no type, enum, requiredness or nullability change.
5. No silently ignored unknown events.
6. No historical mutation (successor prefix rule, CONTRACT §9.3).
7. No invalid state accepted: all 90 negative fixtures rejected with their expected primary and domain codes.
8. The same engine assembly bytes, when the pinned engine is the oracle (`engine.loaded`, CONTRACT §14).

## 5. Accepted compatibility decisions carried into v1.0.0 (CAUSA-COMPAT-04)

These decisions resolved conflicts between the engine, the canonical catalog (`docs/COMMAND-EVENT-CATALOG.md`) and the
candidate contract (`docs/CAUSA-DATA-CONTRACT-v1.md`). They are frozen. Reversing any of them is a class C or D change.

| id | decision | class to reverse |
|---|---|---|
| CD-EVT-01 | Registry = the engine's 31 `EventBody` cases with engine names | C |
| CD-EVT-02 | `LifecycleChanged{from,to,command,reason}` and `HypothesisTransitioned{…,command,…}` instead of per-transition or generic `*StateChanged` events | C |
| CD-EVT-03 | Per-entity `*Transitioned` events carry only the target `state` (+ `basis` where the engine has it) | C |
| CD-EVT-04 | `CausalAssertionConfirmed{policy}` and `CausalAssertionReopened{reason}` are distinct | C |
| CD-EVT-05 | `InvestigationRenamed`, `InvestigationArchived` and 19 other catalog events are not registered | B (adding one) |
| CD-EVT-06 | Resolution names no conclusion; the current conclusion is the highest `recordedAtVersion` | D |
| CD-EVT-07 | `EvidenceAdmitted` has no `verification` field; evidence starts `unverified` | C |
| CD-EVT-09 | `HypothesisChange.At`/`.Sequence` are not persisted; reconstructed from the envelope | C |
| CD-ENV-01 | `recordedAt`, `policyRefs`, `causationId`, the envelope `provenance` and `actor.displayName` are removed | C |
| CD-ENV-02 | `eventId` derived by UUIDv5 over `investigationId/aggregateVersion/correlationId` | C |
| CD-ENV-03 | `correlationId` = command id; one group per id (`CAUSA.CORRELATION_REUSED`) | C |
| CD-ENV-04 | Actor kinds `human`, `system`, `agent{provider,model}` | B (adding a kind) |
| CD-ENV-05 | Agents author only `ProposalRecorded` | D |
| CD-WIRE-01 | Exact RFC 8785 bytes + LF | C |
| CD-WIRE-02 | No optional keys; `option` = required nullable | C |
| CD-WIRE-03 | Millisecond UTC timestamps; refuse sub-millisecond instants | C |
| CD-WIRE-04 | Verbatim text, no NFC; engine whitespace set for non-blank | C |
| CD-WIRE-05 | Identifier arrays ordered, distinct, not sorted | C |
| CD-WIRE-06 | lowerCamelCase enums with F# prefixes dropped | C |
| CD-WIRE-07 | ECMA-262 regex semantics | C |
| CD-WIRE-08 | Framing errors vs non-canonical classification | C |
| CD-SNAP-01 | `generatedAt` = last event's `occurredAt` | C |
| CD-SNAP-02 | Entity arrays sorted by ordinal UUID text | C |
| CD-SNAP-03 | `domainPacks` = `[]` | B |
| CD-PROV-01 | One commit + non-forced ref update; head-commit concurrency token | A (provider-side) unless the files change |
| CD-PROV-02 | Pending events re-decided after conflict, never text-merged | D |
| CD-PROV-03 | Lost confirmation: remote = synced + pending ⇒ mark synced without rewriting | A |

## 6. Legacy data (CAUSA-COMPAT-05)

The rehearsal's legacy `rci.events/v1` histories carry 7-digit fractional timestamps. Migrating them to v1.0.0 would
change instants (CD-WIRE-03), so they MUST NOT be migrated silently. Any migration of them is a class C migration with
its own tests. Until then they are not Causa v1.0.0 data.

## 7. Change procedure (CAUSA-COMPAT-06, implements CAUSA-DATA-001)

1. **Requirements first.** A contract change starts as an approved requirement in Indy-init, as a new versioned
   directory `requirements/causa/data/v<new>/`. It names its class (§2), the fields, events and enums affected,
   migration rules and new fixtures. It never starts as an edit in `causa-data` or in application code.
2. **Review.** The owner approves it. Class D needs explicit written authorization.
3. **Implement in causa-data** under a new `contract/<new>/`, `fixtures/<new>/` and `compatibility/<new>/` tree, then
   freeze and tag it (`causa-storage-v<new>`). Never edit the v1.0.0 trees.
4. **Import** the frozen bytes back into `requirements/causa/data/v<new>/`, add a `release-pin.json`, and regenerate the
   catalogs. The parity gate MUST pass against the new tag.
5. **Approved differences.** `release-pin.json.approvedDifferences` lists `{code, subject, rationale, approvedBy}`
   entries for a difference that is intentional *within the same version*. For v1.0.0 the list is empty and MUST
   stay empty. No difference between the frozen tag and the imported copy is acceptable.

## 8. Parity gate (CAUSA-COMPAT-07)

`python3 -I tools/causa-contract/parity_gate.py check --causa-data <checkout>` reads the frozen side from the git object
store at `refs/tags/causa-storage-v1.0.0` and the requirements side from this directory. It never writes to either side.
It compares:

- the file inventory, contents and SHA-256 digests of every imported file (60 schemas, `bundle.json`, CONTRACT.md,
  FIELD-MAPPING.md, 6 golden files, `lock.json`, `RELEASE`, `engine.lock.json`, `global.json`, the root fixture
  manifest);
- the schema bundle digest, against the pin, `lock.schemaBundleSha256` and `manifest.schemaSha256` on both sides;
- the contract version in the lock, bundle, root manifest and common schema constants;
- the release lock identity and `releaseTag`, plus every `frozenFiles` digest on both sides;
- the event registry (envelope enum, `lock.eventTypes`, payload schema files);
- every enumeration, every property (added, removed or renamed), type, requiredness and nullability;
- `$ref` resolution on both sides;
- the tag object and the commit the tag resolves to.

It fails on any difference not listed in `approvedDifferences`. `selftest` applies 17 mutations to each side and requires
each one to be detected with the right finding code.
