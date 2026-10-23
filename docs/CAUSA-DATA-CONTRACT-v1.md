# Causa Data Contract v1.0.0 — superseded candidate (historical)

Status: **SUPERSEDED CANDIDATE.** The validated, frozen v1.0.0 contract is [requirements/causa/data/v1.0.0/CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md) (byte copy of `causa-data/contract/v1.0.0/CONTRACT.md` at tag `causa-storage-v1.0.0`); see [CAUSA-STORAGE-RELEASE-v1.0.0.md](CAUSA-STORAGE-RELEASE-v1.0.0.md). This document is kept as the candidate the release was reconciled against.

> **Normative status.** **Explanatory (historical).** This candidate is not normative. Persisted data is governed only by the frozen Causa storage contract v1.0.0 ([README.md](../requirements/causa/data/v1.0.0/README.md), [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md), [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md)). Where this document states a persisted field, event name, enum value, layout, hash or concurrency rule differently, the contract governs; such statements are marked *Superseded by frozen contract v1.0.0* in place. The conflicts between this candidate and the frozen contract, and how each was resolved, are recorded in [CAUSA-CONTRACT-RECONCILIATION.md](CAUSA-CONTRACT-RECONCILIATION.md).
Owner: Indy Init / Causa. Data repository: `kemiller2002/causa-data`.
~~This is the authoritative proposed on-disk wire contract.~~ ***Superseded by frozen contract v1.0.0:** historical candidate only; every persisted byte is governed by [CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md).* It is deliberately independent of F# record serialization, UI, GitHub blob SHA and provider implementation. ~~Once verified, record the exact schema/content hashes, tag `causa-storage-v1.0.0`, and treat this document as immutable for R002 and competition.~~ *Done: the verified release identities are in [CAUSA-STORAGE-RELEASE-v1.0.0.md](CAUSA-STORAGE-RELEASE-v1.0.0.md).* Incompatibilities require a new version and approved migration. The existing Indy-init FSHARP-TYPE-SHAPES document is *not* itself a wire schema.

## 1. Fixed physical layout (historical; superseded by CONTRACT §3)

```text
contract/
  v1.0.0/CONTRACT.md
  v1.0.0/schemas/*.schema.json
  v1.0.0/golden/*.json
fixtures/
  v1.0.0/manifest.json
  v1.0.0/investigations/<investigationId>/
    manifest.json
    events.ndjson
    snapshot.json
  v1.0.0/negative/*.json
workspaces/
  demo/<runId>/investigations/<investigationId>/
    manifest.json
    events.ndjson
    snapshot.json
```

All paths are lowercase ASCII, slash-delimited, case-sensitive. `investigationId` is a lowercase canonical UUID string. `runId` matches `[a-z0-9][a-z0-9-]{0,63}`. No user-controlled path traversal. No extra per-investigation authoritative files. `fixtures/` is immutable after freeze. Never write demonstration changes into `fixtures/`. Each run creates an isolated `workspaces/demo/<runId>/` namespace. GitHub branch/commit metadata is provider-only and must not appear in semantic event payloads.

> **Superseded by frozen contract v1.0.0:** (1) schemas are nested (`schemas/entities/`, `schemas/payloads/`) and identified by SHA-256 of `schemas/bundle.json`; `golden/scenarios.json` and `compatibility/v1.0.0/lock.json` are frozen files (CONTRACT §3, §10.3). (2) Only data paths (`fixtures/`, `workspaces/`) are lowercase; payload schema file names are PascalCase (§3.1). (3) An investigation directory contains **exactly** `manifest.json`, `events.ndjson`, `snapshot.json`; any other entry, hidden files included, is `CAUSA.PATH_INVALID` (§3.3). (4) Provider metadata must not appear in **any** contract file, not only event payloads (§1.4). See [CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md).

## 2. JSON wire conventions (all files) (historical; superseded by CONTRACT §2)

UTF-8 without BOM; LF newlines; no comments, NaN or Infinity. JSON object keys are case-sensitive lowerCamelCase, unique, and explicitly listed in schemas. Unknown **core** fields are rejected on write; unknown future version fields are preserved by migration tooling but not silently accepted as v1. UUIDs lowercase RFC 4122 textual form, 36 chars; dates RFC 3339 UTC with exactly 3 fractional digits and `Z`, e.g. `2026-10-22T12:00:00.000Z`. Integers are JSON integers, never floating-point. Aggregate versions are safe integers 0..9007199254740991; sequences are contiguous and start at 1. Strings Unicode NFC, nonempty where noted, with no trimming or normalization of verbatim observation/evidence text. `null` is allowed **only** on fields explicitly marked nullable below. Optional fields are omitted, not set to null. Arrays are ordered; sets encoded as lexicographically sorted, unique arrays. Canonical hashing uses RFC 8785 JSON Canonicalization Scheme for JSON documents; NDJSON digest is SHA-256 over the exact UTF-8 bytes including terminal LF. Hash representation: lowercase 64-character hex. No secrets, credentials, real personal data, or embedded binaries.

> **Superseded by frozen contract v1.0.0:** the following statements in this paragraph are replaced (CONTRACT §2):
> - "Unknown **core** fields are rejected on write" → every object is closed; unknown fields are rejected on read **and** write; v1.0.0 has no extension namespace; data a reader cannot interpret is refused, never dropped (§2.4, §13.5).
> - "UUIDs lowercase RFC 4122 textual form" → also: the nil UUID is invalid; any UUID version is accepted (§2.8).
> - "Aggregate versions are safe integers 0..9007199254740991" → `aggregateVersion` is **1**..9007199254740991; 0 appears only as `correctiveAction.awaitingSinceVersion` (never) (`common.schema.json`).
> - "Strings Unicode NFC" → domain text is stored **verbatim**: never trimmed, case-folded or Unicode-normalized; non-blank per the engine whitespace set (§2.6).
> - "Optional fields are omitted, not set to null" → v1.0.0 has **no optional keys**: every declared key is present; an absent domain `option` is `null` (§2.5).
> - "sets encoded as lexicographically sorted, unique arrays" → identifier arrays are duplicate-free and keep **citation order** (not sorted); snapshot entity arrays are sorted by ordinal UUID text (§2.7, §8.3).
> - "Canonical hashing uses RFC 8785" → every JSON file **is** its RFC 8785 canonical text plus exactly one LF (other bytes: `CAUSA.NON_CANONICAL`); JSON digests are SHA-256 of the canonical text **without** the trailing LF; `events.ndjson`, schema files and `bundle.json` are hashed over exact bytes (§2.2, §2.11).
> - Timestamps must also be calendar-valid; writers refuse sub-millisecond instants (`CAUSA.UNREPRESENTABLE`) (§2.9).
> - Field-by-field types: [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md).

## 3. Root fixture manifest: `fixtures/v1.0.0/manifest.json` (historical; superseded by CONTRACT §4)

| field | JSON type | rule |
|---|---|---|
| `formatVersion` | string | constant `"1.0.0"` |
| `datasetId` | string | constant `"causa-demo-v1"` |
| `createdAt` | string | UTC timestamp |
| `schemaSha256` | string | digest of pinned schema bundle manifest |
| `investigations` | array<object> | ordered by investigationId |
| `investigations[].investigationId` | string | UUID |
| `investigations[].scenarioKey` | string | unique slug |
| `investigations[].title` | string | *Added by frozen contract v1.0.0:* required, non-blank, equals the investigation title (`root-manifest.schema.json`) |
| `investigations[].expectedVersion` | integer | >=1 |
| `investigations[].expectedLifecycle` | string | lifecycle enum below |
| `investigations[].expectedStateSha256` | string | canonical projected semantic state digest |
| `investigations[].eventsSha256` | string | byte digest |
| `investigations[].manifestSha256` | string | canonical JSON digest |
| `investigations[].snapshotSha256` | string | canonical JSON digest |
| `negativeFixtures` | array<object> | each `{path:string,expectedErrorCode:string}`; never listed as valid investigation |

Do not put a digest of the root manifest inside itself.

## 4. Investigation manifest: `manifest.json` (historical; superseded by CONTRACT §9.1)

| field | JSON type | rule |
|---|---|---|
| `formatVersion` | string | `"1.0.0"` |
| `investigationId` | string | UUID equals directory name |
| `schemaVersion` | string | `"1.0.0"` |
| `semanticVersion` | string | `"1.0.0"` initially; change only via approved semantic evolution |
| `aggregateVersion` | integer | number of applied events, >=1 |
| `eventCount` | integer | equals aggregateVersion |
| `eventsSha256` | string | SHA-256 events bytes |
| `snapshotVersion` | integer | projection version, constant 1 |
| `snapshotSha256` | string | SHA-256 canonical snapshot |
| `domainPacks` | array | ~~sorted unique identifiers, initially []~~ *Superseded by frozen contract v1.0.0:* always `[]` (`maxItems: 0`); a domain pack requires a new contract version |
| `createdAt` | string | UTC timestamp of first event |
| `updatedAt` | string | UTC timestamp of latest recorded event |

No GitHub SHA, access token, branch name, or repo path in this manifest. A manifest is updated atomically with its event history and snapshot, never file-by-file with partially successful commits.

## 5. Authoritative event envelope: one JSON object per line in `events.ndjson` (historical; superseded by CONTRACT §5)

| field | JSON type | rule |
|---|---|---|
| `formatVersion` | string | `"1.0.0"` |
| `eventId` | string | ~~UUID, globally unique~~ *Superseded by frozen contract v1.0.0:* derived `UUIDv5(241fefd4-c810-5a3a-9cfa-4bbd968ae1cb, "<investigationId>/<aggregateVersion>/<correlationId>")`, verified by readers (CONTRACT §5.2) |
| `investigationId` | string | UUID, matches manifest |
| `aggregateVersion` | integer | 1,2,...,N without gaps or duplicates |
| `eventType` | string | registered stable PascalCase event discriminator |
| `occurredAt` | string | UTC timestamp |
| ~~`recordedAt`~~ | — | *Superseded by frozen contract v1.0.0:* removed; the engine records one instant, `occurredAt` (CD-ENV-01). Ordering is by aggregateVersion, not clock |
| `actor` | object | Actor below |
| ~~`policyRefs`~~ | — | *Superseded by frozen contract v1.0.0:* removed; the cause gate's policy id is `CausalAssertionConfirmed.policy` |
| `semanticVersion` | string | `"1.0.0"` |
| `correlationId` | string | UUID, required; *frozen contract v1.0.0:* equals the producing command id and names exactly one command group (`CAUSA.CORRELATION_REUSED`, §5.4) |
| ~~`causationId`~~ | — | *Superseded by frozen contract v1.0.0:* removed (the engine has no causation chain) |
| `payload` | object | strict eventType-specific shape, see section 7 |
| ~~`provenance`~~ | — | *Superseded by frozen contract v1.0.0:* removed from the envelope; entity provenance `{actor, recordedAt}` is derived from `actor` and `occurredAt` (§8.3) |

Actor: `{actorId:string, kind:"human"|"agent"|"system"|"external"|"imported", displayName:string|null}`. actorId is nonempty, stable, non-secret; for agents use a non-personal opaque ID. Provenance: `{source:string, sourceRef:string|null, recordedBy:string}`, all non-null strings nonempty. Event identity and provenance are immutable. Never persist UI-derived `leading` or `nextBestAction` fields as authoritative facts.

> **Superseded by frozen contract v1.0.0:** Actor is `{actorId, kind:"human"|"system"}` or `{actorId, kind:"agent", provider, model}`; kinds `external`/`imported`, `displayName` and the envelope Provenance object do not exist; an `agent` actor may appear only on `ProposalRecorded` (CONTRACT §5.3, `actor.schema.json`).

## 6. Snapshot envelope: `snapshot.json` (derived, never authority) (historical; superseded by CONTRACT §8)

| field | JSON type | rule |
|---|---|---|
| `formatVersion` | string | `"1.0.0"` |
| `investigationId` | string | UUID |
| `aggregateVersion` | integer | replayed through this version |
| `projectionVersion` | integer | 1 |
| `semanticVersion` | string | `"1.0.0"` |
| `generatedAt` | string | UTC timestamp; *frozen contract v1.0.0:* equals `occurredAt` of the last applied event, so snapshots are byte-reproducible (§8.1) |
| `state` | object | state fields below |

`state` MUST be a deterministic projection from replay, not a second source of truth. Its fixed fields: `investigationId:string`, `title:string`, `lifecycle:string`, `version:integer`, `createdAt:string`, `observations:array<object>`, `hypotheses:array<object>`, `evidence:array<object>`, `relationships:array<object>`, `unknowns:array<object>`, `assumptions:array<object>`, `contradictions:array<object>`, `predictions:array<object>`, `actions:array<object>`, `findings:array<object>`, `negativeKnowledge:array<object>`, `causes:array<object>`, `correctiveActions:array<object>`, `criteria:array<object>`, `verifications:array<object>`, `conclusions:array<object>`, `proposals:array<object>`. Each array sorted by `id` ascending. **Do not generate snapshot entity schemas from F# auto-serialization**: they must be explicitly authored and pinned before freeze. Until every nested shape is specified and validated, this contract is NOT frozen. For the initial competition adapter, snapshots MAY be omitted only if the manifest explicitly supports a separately versioned events-only profile; do not claim this v1 contract while omitting them.

> **Superseded by frozen contract v1.0.0:** nested shapes are frozen in [schemas/entities/](../requirements/causa/data/v1.0.0/schemas/entities/), so the "NOT frozen" sentence no longer applies. v1.0.0 has **no events-only or snapshot-only profile**: `snapshot.json` is always present and must be byte-identical to the replayed projection (§3.3, §8.1). Arrays are sorted by ordinal UUID text (§8.3).

Lifecycle enum: `"draft"|"active"|"awaitingEvidence"|"awaitingVerification"|"resolved"|"closed"|"archived"`. Never persist derived lifecycle or recommendation labels.

## 7. Event payload registry: required typed fields (historical; superseded by CONTRACT §7 and EVENT-CATALOG.md)

Every event type must have a strict JSON Schema with `additionalProperties:false`, required fields, enumerations and semantic invariants. The **minimum demo-compatible registry** is:

| eventType | required payload fields (name:type) | v1.0.0 wire event (frozen; see EVENT-CATALOG.md) |
|---|---|---|
| `InvestigationStarted` | `title:string`, `originalProblem:string` | `InvestigationStarted{title, observationId, original}` (no `originalProblem`) |
| `InvestigationRenamed` | `title:string` | not registered (deferred; additive v1.x) |
| `InvestigationActivated` | `reason:string` | `LifecycleChanged` command `activateInvestigation`, `reason: null` |
| `InvestigationMarkedAwaitingEvidence` | `reason:string` | `LifecycleChanged` command `markAwaitingEvidence`, `reason` text |
| `InvestigationMarkedAwaitingVerification` | `reason:string` | `LifecycleChanged` command `markAwaitingVerification`, `reason: null` |
| `InvestigationResolved` | `reason:string`, `conclusionId:uuid` | `LifecycleChanged` command `resolveInvestigation`, `reason: null`; no `conclusionId` (current conclusion = highest `recordedAtVersion`) |
| `InvestigationClosed` | `reason:string` | `LifecycleChanged` command `closeInvestigation`, `reason: null` |
| `InvestigationReopened` | `reason:string` | `LifecycleChanged` command `reopenInvestigation`, `reason` text (engine follow-on `verificationFailed` also returns to active) |
| `InvestigationArchived` | `reason:string` | not registered (no v1 command reaches `archived`) |
| `ObservationRecorded` | `observationId:uuid`, `original:string`, `observedAt:timestamp\|null` | `ObservationRecorded` (same) |
| `HypothesisProposed` | `hypothesisId:uuid`, `statement:string`, `mechanism:string\|null` | `HypothesisRecorded` (same fields) |
| `HypothesisStateChanged` | `hypothesisId:uuid`, `from:string`, `to:string`, `reason:string`, `evidenceIds:uuid[]` | `HypothesisTransitioned{hypothesisId, from, to, command, reason, evidenceIds}` |
| `EvidenceAdmitted` | `evidenceId:uuid`, `contentKind:"text"\|"artifact"`, `content:string`, `category:string`, `source:string`, `capturedAt:timestamp\|null`, `verification:string` | `EvidenceAdmitted` without `verification` (starts `unverified`) |
| `EvidenceVerificationChanged` | `evidenceId:uuid`, `from:string`, `to:string`, `reason:string` | `EvidenceVerificationAssessed{evidenceId, verification}` |
| `EvidenceRelatedToHypothesis` | `relationshipId:uuid`, `evidenceId:uuid`, `hypothesisId:uuid`, `kind:string`, `rationale:string\|null` | `EvidenceRelated` (same fields) |
| `UnknownIdentified` | `unknownId:uuid`, `question:string`, `materiality:string` | `UnknownIdentified` (same) |
| `UnknownStateChanged` | `unknownId:uuid`, `from:string`, `to:string`, `basis:string` | `UnknownTransitioned{unknownId, state, basis}` |
| `AssumptionRecorded` | `assumptionId:uuid`, `proposition:string`, `materiality:string` | `AssumptionRecorded` (same); transitions are `AssumptionTransitioned{assumptionId, state}` |
| `ContradictionDetected` | `contradictionId:uuid`, `statement:string`, `evidenceIds:uuid[]`, `materiality:string` | `ContradictionRecorded` (same fields) |
| `ContradictionStateChanged` | `contradictionId:uuid`, `from:string`, `to:string`, `basis:string` | `ContradictionTransitioned{contradictionId, state, basis}` |
| `PredictionRecorded` | `predictionId:uuid`, `hypothesisId:uuid`, `statement:string`, `strength:string`, `timing:string` | `PredictionRecorded` (same; timing decided by the engine) |
| `InvestigationActionProposed` | `actionId:uuid`, `kind:string`, `purpose:string`, `targetHypothesisIds:uuid[]` | `ActionRecorded` (same fields) |
| `InvestigationActionStateChanged` | `actionId:uuid`, `from:string`, `to:string`, `reason:string` | `ActionTransitioned{actionId, state}` |
| `FindingRecorded` | `findingId:uuid`, `actionId:uuid`, `originalResult:string` | `FindingRecorded` plus required-nullable `interpretation` |
| `CausalAssertionProposed` | `causeId:uuid`, `outcome:string`, `members:array<{hypothesisId:uuid,role:string}>`, `mechanism:string\|null` | `CausalAssertionRecorded` (same fields) |
| `CausalAssertionStateChanged` | `causeId:uuid`, `from:string`, `to:string`, `reason:string` | `CausalAssertionConfirmed{causeId, policy}` / `CausalAssertionReopened{causeId, reason}` |
| `CorrectiveActionProposed` | `correctiveActionId:uuid`, `kind:string`, `description:string`, `targetCauseIds:uuid[]` | `CorrectiveActionRecorded` (same fields) |
| `CorrectiveActionStateChanged` | `correctiveActionId:uuid`, `from:string`, `to:string`, `reason:string` | `CorrectiveActionTransitioned{correctiveActionId, state}` |
| `VerificationCriterionRecorded` | `criterionId:uuid`, `correctiveActionId:uuid`, `expectedOutcome:string`, `method:string\|null` | `CriterionRecorded` (same fields) |
| `VerificationRecorded` | `verificationId:uuid`, `criterionId:uuid`, `outcome:string`, `evidenceIds:uuid[]` | `VerificationRecorded` (same; satisfied ⇒ evidence cited) |
| `ConclusionRecorded` | `conclusionId:uuid`, `statement:string`, `strength:string`, `causeIds:uuid[]`, `residualUncertainty:string\|null` | `ConclusionRecorded` (same) |

Registry above is a **candidate subset**, not proof that the current rehearsal emits these exact names. The agent MUST reconcile against the actual F# Command/Event union and existing docs/COMMAND-EVENT-CATALOG.md before freezing. Every additional supported command/event (including supersession, negative knowledge, proposal lifecycle, verification failure and resolution) MUST be exhaustively registered. Unknown eventType must fail closed, not be skipped. Avoid collapsing semantically distinct events to generic `StateChanged` if the canonical domain catalog requires distinct events.

> **Superseded by frozen contract v1.0.0:** reconciled. The v1.0.0 registry is exactly the 31 events in [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md) (including `EvidenceSuperseded`, `NegativeKnowledgeEstablished`, `AssumptionTransitioned`, `ProposalRecorded`, `ProposalWasAccepted`, `ProposalWasRejected`). Lifecycle and hypothesis transitions carry the causing `command`; per-entity `*Transitioned` events carry the target state. Lifecycle `reason` is `null` except for markAwaitingEvidence, reopenInvestigation and verificationFailed.

Enum wire values MUST be exhaustively enumerated in schemas and mapped to F# discriminated unions with bidirectional tests. At minimum: hypothesis states `proposed|plausible|supported|weakened|falsified|superseded`; evidence categories `directObservation|measurement|log|document|experimentResult|testimony|externalReference`; evidence verification `unverified|partiallyVerified|verified|invalidated`; relationship kinds `supports|contradicts|discriminates|insufficient`; materiality `blocking|material|nonMaterial|informational`; verification outcomes `satisfied|failed|inconclusive`. All other enum sets must be extracted from the canonical domain model and pinned explicitly, with no fallback `other` unless domain semantics require it.

> All 31 v1.0.0 enumerations: CONTRACT §6 and [FIELD-CATALOG.md § Closed enumerations](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md).

## 8. Provider concurrency, integrity and safety (historical; superseded by CONTRACT §9.2–§9.4, §10, §11)

A semantic command validates expected aggregate version and GitHub provider version token (*frozen contract v1.0.0:* the branch head commit; publish by a non-forced ref update; success only after a re-read at the new head, §9.4). The GitHub adapter commits event append + manifest + snapshot **atomically in one Git tree/commit**. A stale base must return `conflict` and retain pending commands, never overwrite or falsely report synced. Detect event gaps, duplicates, changed historical events, hash mismatches, manifest/version mismatch, schema mismatch, invalid foreign references, and invalid transitions. Verify that a forged history is rejected even when snapshot looks plausible. Use SHA-256 for integrity, not authentication. Reject arbitrary external artifact URLs in untrusted UI contexts unless validated.

## 9. Required deterministic seed scenarios (historical; superseded by CONTRACT §12)

Five synthetic scenarios, stable UUIDs, timestamps, event sequences and expected projections: `payment-failure` (competing hypotheses); `intermittent-outage` (evidence timeline); `conflicting-metrics` (blocking contradiction); `api-latency` (awaiting verification); `authentication-outage` (resolved with verification). Negative fixtures: duplicate version, missing version, duplicate eventId, wrong investigationId, altered prior event, manifest hash mismatch, unknown event type, invalid payload field, illegal transition, forged resolution, stale provider version. Negative cases never appear in the valid index.

## 10. Freeze/acceptance gate (historical; superseded by CONTRACT §14 and acceptance/ACCEPTANCE.md)

1. Inspect actual rehearsal F# domain, serializer, and canonical event catalog; resolve all name/type conflicts with explicit ADR. No guessed names accepted as final.
2. Produce JSON Schema 2020-12 for root manifest, investigation manifest, event envelope, **each** event payload, snapshot envelope and **each** nested state entity. Record schema bundle digest and schema-to-F# mapping.
3. Generate seed fixtures by issuing real domain commands through the F# engine, not hand-writing states. Ensure fixed clock/UUID seed.
4. Replay all event streams; compare expected state and digest; validate snapshot; test export/import/roundtrip and immutable history.
5. Test actual GitHub API read, atomic append, optimistic concurrency, conflict/reload and offline pending state in disposable workspaces; never mutate frozen fixtures.
6. Compare baseline and rebuilt application against **the same frozen fixture SHA and contract digest**. Any drift, partial write, invalid fixture accepted, or unapproved migration = FAIL. Missing live GitHub test = BLOCKED.
7. Publish evidence, golden projections, schema hashes, acceptance report, and immutable tag. Preserve backward readers for v1 across all later application runs.
8. Before competition, verify organizer permission for pre-created dataset and app-specific executable schemas. If not permitted, seed at kickoff from approved specifications; do not violate empty-app-repo rule.

> Steps 1–7 are complete: evidence in `research-documents/research/experiments/EX-CAUSA-STORAGE-2026-001/` and [acceptance/ACCEPTANCE.md](../requirements/causa/data/v1.0.0/acceptance/ACCEPTANCE.md). Step 8 remains open (COMPATIBILITY-DECISIONS U-01; FROZEN-DEMO-STORAGE-REQUIREMENTS STORAGE-008).

~~**Current status:** candidate requirements only. No evidence yet that the nested snapshot shapes or full event registry have been reconciled.~~ **Current status:** reconciled and superseded by release `causa-storage-v1.0.0` (see [CAUSA-STORAGE-RELEASE-v1.0.0.md](CAUSA-STORAGE-RELEASE-v1.0.0.md) and [CAUSA-CONTRACT-RECONCILIATION.md](CAUSA-CONTRACT-RECONCILIATION.md)). The original rationale stands: declaring a guessed schema frozen would have been worse than marking a concrete specification incomplete.
