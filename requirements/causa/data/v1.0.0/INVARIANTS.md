# Causa v1.0.0 persistence invariants

Normative. Requirement ids `CAUSA-INV-01` … `CAUSA-INV-15`. RFC 2119 keywords. Each invariant names the contract
clause that defines it (CONTRACT.md is the frozen `causa-storage-v1.0.0` text, imported byte for byte), how it is
enforced, the error code a conforming reader or writer reports, and the frozen fixtures that prove enforcement. Where an
invariant is enforced by semantic re-decision, the rules are in [SEMANTICS.md](SEMANTICS.md).

A conforming implementation satisfies all fifteen. An implementation that cannot evaluate one MUST NOT report the input
valid (CONTRACT §11.3).

| id | invariant | defined by | enforced by | code | fixture evidence |
|---|---|---|---|---|---|
| CAUSA-INV-01 | Events are authoritative | §1.2, §8.1 | snapshot and manifest rebuilt from events and compared | `CAUSA.SNAPSHOT_MISMATCH`, `CAUSA.MANIFEST_MISMATCH` | `forged-snapshot` (SNAPSHOT_MISMATCH); `manifest-created-at-mismatch`, `manifest-event-count-mismatch`, `manifest-updated-at-mismatch`, `manifest-version-mismatch`, `manifest-wrong-investigation-id` (MANIFEST_MISMATCH) |
| CAUSA-INV-02 | Snapshots are derived | §1.2, §8.1–§8.4 | byte-identical projection check | `CAUSA.SNAPSHOT_HASH_MISMATCH`, `CAUSA.SNAPSHOT_MISMATCH`, `CAUSA.NON_CANONICAL` | `broken-snapshot-hash` (SNAPSHOT_HASH_MISMATCH), `forged-snapshot` (SNAPSHOT_MISMATCH), `snapshot-non-canonical` (NON_CANONICAL) |
| CAUSA-INV-03 | Event histories are immutable | §5.5, §9.3 | successor prefix check; events digest | `CAUSA.HISTORY_REWRITTEN`, `CAUSA.EVENTS_HASH_MISMATCH` | `rewritten-history` (HISTORY_REWRITTEN), `altered-event-without-rehash` (EVENTS_HASH_MISMATCH) |
| CAUSA-INV-04 | Aggregate versions are contiguous | §5, §5.1 | stage 12 | `CAUSA.SEQUENCE_INVALID` | `sequence-gap`, `duplicate-aggregate-version` |
| CAUSA-INV-05 | Replay rejects invalid histories | §11.1–§11.3 | 17 ordered reader stages, fail closed | stage codes | all 90 negative fixtures |
| CAUSA-INV-06 | Event ids are never reused | §5.2 | derivation and uniqueness | `CAUSA.EVENT_ID_DUPLICATE`, `CAUSA.EVENT_ID_MISMATCH` | `duplicate-event-id`, `forged-event-id` |
| CAUSA-INV-07 | Cross-entity references are valid | §8.3, SEMANTICS.md | re-decision (stage 15) | `CAUSA.HISTORY_INADMISSIBLE` / `HISTORY.INADMISSIBLE_EVENT` | `engine-dangling-reference`, `engine-duplicate-entity-id` |
| CAUSA-INV-08 | Domain transitions are approved by the F# policy engine | §1.3, §11.3, SEMANTICS.md | re-decision of every command group | `CAUSA.HISTORY_INADMISSIBLE` + domain code | 34 stage-15 fixtures |
| CAUSA-INV-09 | GitHub metadata never enters domain structures | §1.4, §2.4, §9.1, §9.4 | closed schemas; provider rules | `CAUSA.SCHEMA_VIOLATION` | `unexpected-payload-field` (any added key, provider metadata included, is refused by the same closed-object rule); provider rows AC-PROV-* in [PROVIDER-GITHUB.md](PROVIDER-GITHUB.md) |
| CAUSA-INV-10 | Canonical serialization and hashing are deterministic | §2.1–§2.3, §2.11, §8.4, §10 | byte form and digest checks | `CAUSA.NON_CANONICAL`, `CAUSA.ENCODING`, hash codes | `non-canonical-manifest`, `event-line-non-canonical`, `negative-zero`, `manifest-interior-newline`, `events-crlf` |
| CAUSA-INV-11 | Unsupported event types fail closed | §7.1, §13.5 | stage 6 | `CAUSA.UNKNOWN_EVENT_TYPE` | `unknown-event-type` |
| CAUSA-INV-12 | Schema changes require explicit versioning | §13, §14 | version fields and parity gate | `CAUSA.UNSUPPORTED_VERSION` | `unsupported-format-version`, parity self-test |
| CAUSA-INV-13 | Frozen fixtures are never rewritten | §3.4, §14 | release tag anchor, parity gate, acceptance step 0, writer path rule | `CAUSA.FIXTURE_DRIFT`, `CAUSA.PATH_FROZEN`, gate failure | parity self-test mutations; acceptance step 0 (frozen-file digests); AC-PROV rows (working copy leaves frozen tree byte-identical) |
| CAUSA-INV-14 | Storage writes are atomic and concurrency-correct | §9.2, §9.4 | version check, atomic commit, non-forced ref update | `CAUSA.VERSION_STALE`, sync state `conflicted` | `stale-expected-version`; AC-APPEND-02; AC-PROV rows in [PROVIDER-GITHUB.md](PROVIDER-GITHUB.md); local atomicity CAUSA-CLAR-04 |
| CAUSA-INV-15 | A new build consumes the frozen dataset without migration | §13.2, §13.3 | acceptance suite against the pinned dataset | golden digest equality | 5 investigations, 5 golden states |

---

## CAUSA-INV-01 — Events are authoritative

1. The ordered event history in `events.ndjson` is the only source of domain truth for an investigation.
2. Snapshots, manifests, golden projections, root-manifest entries, indexes, caches and UI state are derived. They MUST be
   reproducible from the history alone, and MUST NOT carry a fact that is absent from it.
3. When the history is available, a reader MUST NOT read the snapshot as a source of truth. v1.0.0 has no events-only or
   snapshot-only profile: all three files are always required (§3.3, `CAUSA.PATH_INVALID`).
4. A disagreement between a derived file and the history is an error in the derived file (`CAUSA.MANIFEST_MISMATCH`,
   `CAUSA.SNAPSHOT_HASH_MISMATCH`, `CAUSA.SNAPSHOT_MISMATCH`). It is never resolved in favour of the derived file.

## CAUSA-INV-02 — Snapshots are derived

1. `snapshot.json` MUST equal, byte for byte, the RFC 8785 canonical text (plus one LF) of the projection defined in
   CONTRACT §8.3, applied to the full history. `generatedAt` is the `occurredAt` of the last event and never a wall
   clock. `projectionVersion` is `1`.
2. `manifest.snapshotSha256` MUST equal the SHA-256 of the canonical snapshot text without the trailing LF.
3. Writers MUST recompute the snapshot from the history after every append. They MUST NOT patch it incrementally unless
   the result is byte-identical to a full replay.
4. Derived UI postures ("leading", "next best action") MUST NOT be persisted (§8.2).

## CAUSA-INV-03 — Event histories are immutable

1. An appended event MUST NOT be edited, reordered or deleted. Corrections are new events (for example
   `EvidenceSuperseded`, `CausalAssertionReopened`, `HypothesisTransitioned`).
2. A newer file set for an investigation is acceptable only if its `events.ndjson` begins with the previously accepted
   `events.ndjson` bytes. This holds even when every hash and the snapshot are self-consistent
   (`CAUSA.HISTORY_REWRITTEN`, §9.3). The candidate is first verified on its own (§11.1), and the prefix check follows. A
   byte-identical successor is acceptable and changes nothing.
3. Changing an event without updating `eventsSha256` is `CAUSA.EVENTS_HASH_MISMATCH` (stage 9).

## CAUSA-INV-04 — Aggregate versions are contiguous

1. The `aggregateVersion` values of an investigation MUST be exactly `1, 2, …, N` in file order, with no gaps or
   duplicates (`CAUSA.SEQUENCE_INVALID`, stage 12).
2. `InvestigationStarted` MUST be version 1 and the first line (schema rule; `HISTORY.NO_START` at stage 15 when absent).
3. Order is defined by `aggregateVersion` only. Clock values (`occurredAt`, `capturedAt`, `observedAt`) never establish
   order, and a reader MUST NOT reject or reorder a history because of them (§5.6).
4. The manifest's `aggregateVersion` and `eventCount` MUST both equal the number of lines (stage 8). The snapshot's
   `aggregateVersion` and `state.version` MUST equal N.

## CAUSA-INV-05 — Replay rejects invalid histories

1. A reader MUST validate an investigation in the 17-stage order of CONTRACT §11.1 and report the first failing stage as
   the primary code. Every conforming reader MUST report the same primary code for every negative fixture.
2. Readers fail closed. An invalid investigation is not partially loaded, skipped past, repaired, re-sorted, re-hashed
   or rewritten (§11.2).
3. Errors carry the primary code, a human-readable detail and, at stage 15, the domain code (`HISTORY.*`).
4. A consumer that cannot evaluate stage 15 MUST report "structurally valid, admissibility not evaluated", never valid.
   For inputs whose expected code comes after stage 15, it MUST mark its primary code conditional (§11.3).

## CAUSA-INV-06 — Event ids are never reused

1. `eventId` MUST equal `UUIDv5(241fefd4-c810-5a3a-9cfa-4bbd968ae1cb, "<investigationId>/<aggregateVersion>/<correlationId>")`
   with lowercase UUID text, a decimal version without leading zeros, and the name encoded as UTF-8
   (`CAUSA.EVENT_ID_MISMATCH`, stage 13). The namespace is `UUIDv5(NAMESPACE_URL,
   "https://github.com/kemiller2002/causa-data/contract/v1/event-id")`.
2. `eventId` MUST be unique within the history (`CAUSA.EVENT_ID_DUPLICATE`, stage 11). Writers MUST NOT choose ids
   freely: the derivation binds an id to its position and its command, so divergent histories get different ids.
3. A `correlationId` names exactly one command group. Reusing it for a later, non-adjacent group is
   `CAUSA.CORRELATION_REUSED` (stage 14). Writers MUST mint a fresh command id for every command. This rule is stricter
   than the engine.
4. Entity ids (`observationId`, `hypothesisId`, …) MUST be unique within their kind. A second creation with an existing
   id is inadmissible (`HISTORY.INADMISSIBLE_EVENT`, fixture `engine-duplicate-entity-id`).

## CAUSA-INV-07 — Cross-entity references are valid

1. Every id that refers to an existing entity MUST name an entity of the right kind that exists in the state replayed
   before the event: `hypothesisId`, `evidenceId`, `replacementEvidenceId`, `evidenceIds[]`, `basisEvidenceIds[]`,
   `targetHypothesisIds[]`, `targetCauseIds[]`, `members[].hypothesisId`, `actionId`, `causeId`, `causeIds[]`,
   `criterionId`, `correctiveActionId`, `proposalId`, and every transition target id. Otherwise the history is
   inadmissible at stage 15. The per-event reference table is in [EVENT-CATALOG.md](EVENT-CATALOG.md), and the exact
   rule and its evaluation order are in [SEMANTICS.md](SEMANTICS.md).
2. `ProposalWasAccepted.targetId` MUST be the id of the entity created by the preceding event of the same command group.
3. Identifier arrays MUST NOT contain duplicates (schema `uniqueItems`). The command boundary MUST de-duplicate before
   deciding, because a duplicate has no v1 wire form (`CAUSA.UNREPRESENTABLE`, §2.7).

## CAUSA-INV-08 — Domain transitions are approved by the F# policy engine

1. Every persisted command group MUST be exactly the event list that the F# domain engine's decision function returns
   for the reconstructed command at the replayed prior state, using the recorded actor and `occurredAt`. That covers
   the transition tables, open-investigation rules, the cause-confirmation and resolution gates, verification
   freshness, prediction timing, agent authority and repetition refusal. A group that the engine would refuse or decide
   differently is inadmissible (`CAUSA.HISTORY_INADMISSIBLE` with domain code `HISTORY.NO_START`, `HISTORY.SEQUENCE`,
   `HISTORY.INADMISSIBLE_EVENT`, `HISTORY.UNGATED_CONFIRMATION` or `HISTORY.UNGATED_RESOLUTION`).
2. "F# policy engine" here means any implementation of [SEMANTICS.md](SEMANTICS.md) that conforms by SEMANTICS §0.2
   (CAUSA-CLAR-14). The decision rules are stated normatively in [SEMANTICS.md](SEMANTICS.md). The F# engine at
   `kemiller2002/indy-init-rehearsal-001@55f49f61c939a3a780cee592f36f732a1328f981` (pinned in
   `compatibility/engine.lock.json` and by assembly digests in `compatibility/lock.json`) is the **reference oracle**
   used to generate and verify the frozen dataset. An implementation of SEMANTICS.md conforms when it reproduces the
   oracle's verdict and first domain code on every history (SEMANTICS §0.2); passing every frozen and supplementary
   fixture is necessary evidence, not sufficient. SEMANTICS.md §"Engine-dependence register" records
   where the text depends on platform behaviour.
3. The application MUST route every domain mutation through the policy engine's decide step. UI code, storage adapters
   and AI agents MUST NOT construct domain events directly.
4. Agents (actor kind `agent`) MAY author only `ProposalRecorded` (D-004). The envelope schema enforces this at stage 7
   (`agent-authored-hypothesis`), and the engine enforces it again at stage 15.

## CAUSA-INV-09 — GitHub metadata never enters domain structures

1. Provider metadata MUST NOT appear in any file defined by the contract. That includes Git commit SHAs, blob SHAs,
   tree SHAs, branch names, ref names, repository paths, ETags, API URLs, installation ids, tokens and rate-limit data
   (§1.4, §9.1). It also covers events, payloads, manifests, snapshots and fixtures.
2. Every object is closed (`additionalProperties: false`), and v1.0.0 has no extension namespace (§2.4, §13.5). An
   added field is therefore `CAUSA.SCHEMA_VIOLATION` (`unexpected-payload-field`). The parity gate fails on any
   unapproved field (`FIELD_ADDED`).
3. Provider state lives only in the adapter. The concurrency token is the branch head commit the writer read, and
   commit messages are audit metadata, not semantic data (§9.4.2, §9.4.5).
4. Credentials are sent only in the `Authorization` header and are never written to any file, message, log or event.

## CAUSA-INV-10 — Canonical serialization and hashing are deterministic

1. Every JSON text is its exact RFC 8785 (JCS) serialization. Each JSON file is one canonical text plus exactly one LF.
   `events.ndjson` is one canonical text per LF-terminated line with no blank lines. Encoding is UTF-8 without BOM,
   with LF only (§2.1–§2.2).
2. Integers lie in `[-(2^53-1), 2^53-1]`. There are no floats, exponents or NaN, and `-0` is non-canonical. Nesting depth
   is at most 128. Keys are unique, U+FEFF is forbidden everywhere, and unpaired surrogates are invalid (§2.3).
3. Digests are lowercase SHA-256 hex. JSON documents are hashed over their canonical text without the trailing LF.
   `events.ndjson`, the schema files and `bundle.json` are hashed over their exact file bytes (§2.11).
4. Projection is deterministic. Entity arrays are sorted by ordinal UUID text, and identifier arrays keep citation
   order (§2.7, §8.3). `generatedAt` and manifest times are copied from events, never from a clock.
5. Text is stored verbatim: no trimming, case folding or Unicode normalization (§2.6). Timestamps are millisecond UTC
   `Z`, and writers refuse sub-millisecond instants instead of truncating them (`CAUSA.UNREPRESENTABLE`, §2.9).
6. The schema identity, dataset identity and release identity (§10.4) are SHA-256 digests over exact bytes. Their v1.0.0
   values are pinned in `compatibility/release-pin.json`.

## CAUSA-INV-11 — Unsupported event types fail closed

1. The registry is exactly the 31 discriminators in [EVENT-CATALOG.md](EVENT-CATALOG.md). Any other string `eventType`
   invalidates the whole history (`CAUSA.UNKNOWN_EVENT_TYPE`, stage 6). A non-string one is `CAUSA.SCHEMA_VIOLATION`.
2. Unknown events MUST NOT be skipped, preserved-and-ignored, down-cast to a generic event, or partially projected.
3. A v1.0.0 reader MUST reject every `formatVersion`, `schemaVersion` or `semanticVersion` string other than `"1.0.0"`
   (`CAUSA.UNSUPPORTED_VERSION`, stage 5), and never reads newer data "best effort" (§13.2).

## CAUSA-INV-12 — Schema changes require explicit versioning

1. Any change to a schema file, an enumeration value, the event registry, requiredness, nullability, a field name or
   type, the id derivation, the canonical form or the projection rules is a contract change. It MUST be classified under
   [compatibility/RULES.md](compatibility/RULES.md) and released as a new contract version with its own directory tree.
2. A changed schema byte changes the schema identity. The parity gate fails until the change is approved and versioned.
   There is no unversioned "minor fix".
3. Requirements lead: the change is first approved in these Indy-init requirements (CAUSA-DATA-001), and only then
   implemented in `causa-data` and the application.

## CAUSA-INV-13 — Frozen fixtures are never rewritten

1. After the `causa-storage-v1.0.0` tag, nothing under `contract/v1.0.0/`, `fixtures/v1.0.0/` or
   `compatibility/v1.0.0/` may be created, modified or deleted, and neither may `engine.lock.json` or `global.json`, in
   `causa-data` (§3.4, §14).
2. The imported copies under `requirements/causa/data/v1.0.0/` in Indy-init are equally immutable. Their bytes MUST equal
   the tag (`tools/causa-contract/parity_gate.py`).
3. The release tag MUST continue to resolve to tag object `fd2991a392e913b741f0ed2bc4daa4bc4be57f6c` and commit
   `c947c7e354be80766573a782546c8186336774cc` (`TAG_MOVED` otherwise).
4. Demonstration writes go only to `workspaces/demo/<runId>/investigations/<investigationId>/`. Before publishing, the
   adapter MUST check that the new tree differs from its base only under that prefix (§9.4.4).
5. Migration tooling never rewrites a source. It writes new files in a new namespace (§13.1, §13.5).

## CAUSA-INV-14 — Storage writes are atomic and concurrency-correct

1. **Optimistic version.** An append carries the aggregate version that the writer observed. If it differs from the
   current version, the append is refused with `CAUSA.VERSION_STALE` and nothing is written. The writer MUST keep the
   pending command so that it can reload and re-decide (§9.2).
2. **Evaluation order of an append:** (1) the version check; (2) the appended lines through stages 2–7, numbered from
   current + 1; (3) the combined history through every reader stage, with manifest and snapshot rebuilt.
3. **Verify before success.** After appending, a writer recomputes the snapshot and manifest and verifies the result
   exactly as a reader would before it reports success.
4. **Atomic publication.** The events append, manifest and snapshot of one command become visible together. On GitHub
   that means one commit whose tree contains all three files, published by a non-forced ref update (`PATCH /git/refs`,
   `force: false`). Sequential per-file Contents API writes do not conform (§9.4.1).
5. **Conflicts** (full procedure and sync-state machine: [PROVIDER-GITHUB.md](PROVIDER-GITHUB.md)). A non-fast-forward ref update is a conflict. The writer reloads and re-verifies. If the
   investigation's files did not change, it rebases onto the new head. Otherwise it reports `conflict`, keeps its
   pending commands, and re-decides them through the engine. It MUST NOT text-merge, overwrite or force-push.
6. **Truthful status.** Success is reported only after the provider confirms the ref update **and** a re-read at the new
   head returns the written bytes. Pending, sending, conflicted and unknown remain distinct states. A network failure is
   never reported as success (§9.4.3).

## CAUSA-INV-15 — A new build consumes the frozen dataset without migration

1. Every application build MUST read all five frozen investigations of `causa-demo-v1` unchanged, with no conversion,
   upgrade, re-encoding or re-seeding. For each one it MUST reproduce the golden `expectedStateSha256`, `eventsSha256`,
   `manifestSha256` and `snapshotSha256` recorded in `fixtures/manifest.json`.
2. Every build MUST reject every negative fixture with its expected primary code and, at stage 15, its expected domain
   code.
3. New events appended by a build MUST use exactly the v1.0.0 wire form. An existing investigation MUST stay
   readable by every v1.0.0 reader after the append.
4. The build gate is [acceptance/ACCEPTANCE.md](acceptance/ACCEPTANCE.md). A build that fails any row of
   `acceptance/TEST-MATRIX.json` MUST NOT be released (CAUSA-DATA-001).
