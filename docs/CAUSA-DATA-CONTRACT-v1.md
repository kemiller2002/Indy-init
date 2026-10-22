# Causa Data Contract v1.0.0 (proposed freeze)

Status: **NORMATIVE CANDIDATE, NOT YET VALIDATED/FROZEN**. Do not call this implemented until golden replay and live GitHub tests pass.
Owner: Indy Init / Causa. Data repository: `kemiller2002/causa-data`.
This is the authoritative proposed on-disk wire contract. It is deliberately independent of F# record serialization, UI, GitHub blob SHA and provider implementation. Once verified, record the exact schema/content hashes, tag `causa-storage-v1.0.0`, and treat this document as immutable for R002 and competition. Incompatibilities require a new version and approved migration. The existing Indy-init FSHARP-TYPE-SHAPES document is *not* itself a wire schema.

## 1. Fixed physical layout

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

## 2. JSON wire conventions (all files)

UTF-8 without BOM; LF newlines; no comments, NaN or Infinity. JSON object keys are case-sensitive lowerCamelCase, unique, and explicitly listed in schemas. Unknown **core** fields are rejected on write; unknown future version fields are preserved by migration tooling but not silently accepted as v1. UUIDs lowercase RFC 4122 textual form, 36 chars; dates RFC 3339 UTC with exactly 3 fractional digits and `Z`, e.g. `2026-10-22T12:00:00.000Z`. Integers are JSON integers, never floating-point. Aggregate versions are safe integers 0..9007199254740991; sequences are contiguous and start at 1. Strings Unicode NFC, nonempty where noted, with no trimming or normalization of verbatim observation/evidence text. `null` is allowed **only** on fields explicitly marked nullable below. Optional fields are omitted, not set to null. Arrays are ordered; sets encoded as lexicographically sorted, unique arrays. Canonical hashing uses RFC 8785 JSON Canonicalization Scheme for JSON documents; NDJSON digest is SHA-256 over the exact UTF-8 bytes including terminal LF. Hash representation: lowercase 64-character hex. No secrets, credentials, real personal data, or embedded binaries.

## 3. Root fixture manifest: `fixtures/v1.0.0/manifest.json`

| field | JSON type | rule |
|---|---|---|
| `formatVersion` | string | constant `"1.0.0"` |
| `datasetId` | string | constant `"causa-demo-v1"` |
| `createdAt` | string | UTC timestamp |
| `schemaSha256` | string | digest of pinned schema bundle manifest |
| `investigations` | array<object> | ordered by investigationId |
| `investigations[].investigationId` | string | UUID |
| `investigations[].scenarioKey` | string | unique slug |
| `investigations[].expectedVersion` | integer | >=1 |
| `investigations[].expectedLifecycle` | string | lifecycle enum below |
| `investigations[].expectedStateSha256` | string | canonical projected semantic state digest |
| `investigations[].eventsSha256` | string | byte digest |
| `investigations[].manifestSha256` | string | canonical JSON digest |
| `investigations[].snapshotSha256` | string | canonical JSON digest |
| `negativeFixtures` | array<object> | each `{path:string,expectedErrorCode:string}`; never listed as valid investigation |

Do not put a digest of the root manifest inside itself.

## 4. Investigation manifest: `manifest.json`

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
| `domainPacks` | array<string> | sorted unique identifiers, initially [] |
| `createdAt` | string | UTC timestamp of first event |
| `updatedAt` | string | UTC timestamp of latest recorded event |

No GitHub SHA, access token, branch name, or repo path in this manifest. A manifest is updated atomically with its event history and snapshot, never file-by-file with partially successful commits.

## 5. Authoritative event envelope: one JSON object per line in `events.ndjson`

| field | JSON type | rule |
|---|---|---|
| `formatVersion` | string | `"1.0.0"` |
| `eventId` | string | UUID, globally unique |
| `investigationId` | string | UUID, matches manifest |
| `aggregateVersion` | integer | 1,2,...,N without gaps or duplicates |
| `eventType` | string | registered stable PascalCase event discriminator |
| `occurredAt` | string | UTC timestamp |
| `recordedAt` | string | UTC timestamp; ordering is by aggregateVersion, not clock |
| `actor` | object | Actor below |
| `policyRefs` | array<string> | sorted unique rule identifiers, may be [] |
| `semanticVersion` | string | `"1.0.0"` |
| `correlationId` | string | UUID, required |
| `causationId` | string or null | UUID or null |
| `payload` | object | strict eventType-specific shape, see section 7 |
| `provenance` | object | Provenance below |

Actor: `{actorId:string, kind:"human"|"agent"|"system"|"external"|"imported", displayName:string|null}`. actorId is nonempty, stable, non-secret; for agents use a non-personal opaque ID. Provenance: `{source:string, sourceRef:string|null, recordedBy:string}`, all non-null strings nonempty. Event identity and provenance are immutable. Never persist UI-derived `leading` or `nextBestAction` fields as authoritative facts.

## 6. Snapshot envelope: `snapshot.json` (derived, never authority)

| field | JSON type | rule |
|---|---|---|
| `formatVersion` | string | `"1.0.0"` |
| `investigationId` | string | UUID |
| `aggregateVersion` | integer | replayed through this version |
| `projectionVersion` | integer | 1 |
| `semanticVersion` | string | `"1.0.0"` |
| `generatedAt` | string | UTC timestamp |
| `state` | object | state fields below |

`state` MUST be a deterministic projection from replay, not a second source of truth. Its fixed fields: `investigationId:string`, `title:string`, `lifecycle:string`, `version:integer`, `createdAt:string`, `observations:array<object>`, `hypotheses:array<object>`, `evidence:array<object>`, `relationships:array<object>`, `unknowns:array<object>`, `assumptions:array<object>`, `contradictions:array<object>`, `predictions:array<object>`, `actions:array<object>`, `findings:array<object>`, `negativeKnowledge:array<object>`, `causes:array<object>`, `correctiveActions:array<object>`, `criteria:array<object>`, `verifications:array<object>`, `conclusions:array<object>`, `proposals:array<object>`. Each array sorted by `id` ascending. **Do not generate snapshot entity schemas from F# auto-serialization**: they must be explicitly authored and pinned before freeze. Until every nested shape is specified and validated, this contract is NOT frozen. For the initial competition adapter, snapshots MAY be omitted only if the manifest explicitly supports a separately versioned events-only profile; do not claim this v1 contract while omitting them.

Lifecycle enum: `"draft"|"active"|"awaitingEvidence"|"awaitingVerification"|"resolved"|"closed"|"archived"`. Never persist derived lifecycle or recommendation labels.

## 7. Event payload registry: required typed fields

Every event type must have a strict JSON Schema with `additionalProperties:false`, required fields, enumerations and semantic invariants. The **minimum demo-compatible registry** is:

| eventType | required payload fields (name:type) |
|---|---|
| `InvestigationStarted` | `title:string`, `originalProblem:string` |
| `InvestigationRenamed` | `title:string` |
| `InvestigationActivated` | `reason:string` |
| `InvestigationMarkedAwaitingEvidence` | `reason:string` |
| `InvestigationMarkedAwaitingVerification` | `reason:string` |
| `InvestigationResolved` | `reason:string`, `conclusionId:uuid` |
| `InvestigationClosed` | `reason:string` |
| `InvestigationReopened` | `reason:string` |
| `InvestigationArchived` | `reason:string` |
| `ObservationRecorded` | `observationId:uuid`, `original:string`, `observedAt:timestamp|null` |
| `HypothesisProposed` | `hypothesisId:uuid`, `statement:string`, `mechanism:string|null` |
| `HypothesisStateChanged` | `hypothesisId:uuid`, `from:string`, `to:string`, `reason:string`, `evidenceIds:uuid[]` |
| `EvidenceAdmitted` | `evidenceId:uuid`, `contentKind:"text"|"artifact"`, `content:string`, `category:string`, `source:string`, `capturedAt:timestamp|null`, `verification:string` |
| `EvidenceVerificationChanged` | `evidenceId:uuid`, `from:string`, `to:string`, `reason:string` |
| `EvidenceRelatedToHypothesis` | `relationshipId:uuid`, `evidenceId:uuid`, `hypothesisId:uuid`, `kind:string`, `rationale:string|null` |
| `UnknownIdentified` | `unknownId:uuid`, `question:string`, `materiality:string` |
| `UnknownStateChanged` | `unknownId:uuid`, `from:string`, `to:string`, `basis:string` |
| `AssumptionRecorded` | `assumptionId:uuid`, `proposition:string`, `materiality:string` |
| `ContradictionDetected` | `contradictionId:uuid`, `statement:string`, `evidenceIds:uuid[]`, `materiality:string` |
| `ContradictionStateChanged` | `contradictionId:uuid`, `from:string`, `to:string`, `basis:string` |
| `PredictionRecorded` | `predictionId:uuid`, `hypothesisId:uuid`, `statement:string`, `strength:string`, `timing:string` |
| `InvestigationActionProposed` | `actionId:uuid`, `kind:string`, `purpose:string`, `targetHypothesisIds:uuid[]` |
| `InvestigationActionStateChanged` | `actionId:uuid`, `from:string`, `to:string`, `reason:string` |
| `FindingRecorded` | `findingId:uuid`, `actionId:uuid`, `originalResult:string` |
| `CausalAssertionProposed` | `causeId:uuid`, `outcome:string`, `members:array<{hypothesisId:uuid,role:string}>`, `mechanism:string|null` |
| `CausalAssertionStateChanged` | `causeId:uuid`, `from:string`, `to:string`, `reason:string` |
| `CorrectiveActionProposed` | `correctiveActionId:uuid`, `kind:string`, `description:string`, `targetCauseIds:uuid[]` |
| `CorrectiveActionStateChanged` | `correctiveActionId:uuid`, `from:string`, `to:string`, `reason:string` |
| `VerificationCriterionRecorded` | `criterionId:uuid`, `correctiveActionId:uuid`, `expectedOutcome:string`, `method:string|null` |
| `VerificationRecorded` | `verificationId:uuid`, `criterionId:uuid`, `outcome:string`, `evidenceIds:uuid[]` |
| `ConclusionRecorded` | `conclusionId:uuid`, `statement:string`, `strength:string`, `causeIds:uuid[]`, `residualUncertainty:string|null` |

Registry above is a **candidate subset**, not proof that the current rehearsal emits these exact names. The agent MUST reconcile against the actual F# Command/Event union and existing docs/COMMAND-EVENT-CATALOG.md before freezing. Every additional supported command/event (including supersession, negative knowledge, proposal lifecycle, verification failure and resolution) MUST be exhaustively registered. Unknown eventType must fail closed, not be skipped. Avoid collapsing semantically distinct events to generic `StateChanged` if the canonical domain catalog requires distinct events.

Enum wire values MUST be exhaustively enumerated in schemas and mapped to F# discriminated unions with bidirectional tests. At minimum: hypothesis states `proposed|plausible|supported|weakened|falsified|superseded`; evidence categories `directObservation|measurement|log|document|experimentResult|testimony|externalReference`; evidence verification `unverified|partiallyVerified|verified|invalidated`; relationship kinds `supports|contradicts|discriminates|insufficient`; materiality `blocking|material|nonMaterial|informational`; verification outcomes `satisfied|failed|inconclusive`. All other enum sets must be extracted from the canonical domain model and pinned explicitly, with no fallback `other` unless domain semantics require it.

## 8. Provider concurrency, integrity and safety

A semantic command validates expected aggregate version and GitHub provider version token. The GitHub adapter commits event append + manifest + snapshot **atomically in one Git tree/commit**. A stale base must return `conflict` and retain pending commands, never overwrite or falsely report synced. Detect event gaps, duplicates, changed historical events, hash mismatches, manifest/version mismatch, schema mismatch, invalid foreign references, and invalid transitions. Verify that a forged history is rejected even when snapshot looks plausible. Use SHA-256 for integrity, not authentication. Reject arbitrary external artifact URLs in untrusted UI contexts unless validated.

## 9. Required deterministic seed scenarios

Five synthetic scenarios, stable UUIDs, timestamps, event sequences and expected projections: `payment-failure` (competing hypotheses); `intermittent-outage` (evidence timeline); `conflicting-metrics` (blocking contradiction); `api-latency` (awaiting verification); `authentication-outage` (resolved with verification). Negative fixtures: duplicate version, missing version, duplicate eventId, wrong investigationId, altered prior event, manifest hash mismatch, unknown event type, invalid payload field, illegal transition, forged resolution, stale provider version. Negative cases never appear in the valid index.

## 10. Freeze/acceptance gate

1. Inspect actual rehearsal F# domain, serializer, and canonical event catalog; resolve all name/type conflicts with explicit ADR. No guessed names accepted as final.
2. Produce JSON Schema 2020-12 for root manifest, investigation manifest, event envelope, **each** event payload, snapshot envelope and **each** nested state entity. Record schema bundle digest and schema-to-F# mapping.
3. Generate seed fixtures by issuing real domain commands through the F# engine, not hand-writing states. Ensure fixed clock/UUID seed.
4. Replay all event streams; compare expected state and digest; validate snapshot; test export/import/roundtrip and immutable history.
5. Test actual GitHub API read, atomic append, optimistic concurrency, conflict/reload and offline pending state in disposable workspaces; never mutate frozen fixtures.
6. Compare baseline and rebuilt application against **the same frozen fixture SHA and contract digest**. Any drift, partial write, invalid fixture accepted, or unapproved migration = FAIL. Missing live GitHub test = BLOCKED.
7. Publish evidence, golden projections, schema hashes, acceptance report, and immutable tag. Preserve backward readers for v1 across all later application runs.
8. Before competition, verify organizer permission for pre-created dataset and app-specific executable schemas. If not permitted, seed at kickoff from approved specifications; do not violate empty-app-repo rule.

**Current status:** candidate requirements only. No evidence yet that the nested snapshot shapes or full event registry have been reconciled. This is intentional: declaring a guessed schema frozen would be worse than marking a concrete specification incomplete.
