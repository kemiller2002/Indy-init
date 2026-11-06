# Contract gap 2: content that causa-storage v1.0.0 cannot store (Rehearsal 010)

**Status: unverified analysis and proposal.** Nothing here was compiled, run or reviewed. Nothing here is an owner
decision. No file under `contract/v1.0.0`, `fixtures/v1.0.0` or `compatibility/v1.0.0` is changed, and no field is
added to v1.0.0.

Citation keys are as in `RECORD-CONCLUSION.md`:

| key | repository |
|---|---|
| `Indy-init:` | `origin/main` `89f5840` |
| `app:` | `indy-init-rehearsal-010` `origin/main` `0bd6e0c` |
| `causa-data:` | tag `causa-storage-v1.0.0`; frozen paths identical at `main` `e8259df` |

## 1. Question

Can these four values be stored **losslessly** under causa-storage v1.0.0?

| value | requirement | journeys |
|---|---|---|
| Evidence label | INV-027: required, 1–200 characters (`Indy-init:requirements/catalogs/causa-investigation.requirement-catalog.json:809-852`) | J2-03, J2-05, J2-09 (`Indy-init:verification/journeys/j2-analysis.spec.mjs:48`, `:53`, `:95`, `:100`) |
| Observation source reference | INV-025: optional, ≤10,000 (`catalog.json:720-757`) | J2-01, J2-09 (`j2-analysis.spec.mjs:36`, `:95`) |
| Evidence source URL | INV-029: optional, ≤2,000, `^https?://[^\s]+$` (`catalog.json:897-944`) | J2-03 (catalog verification `save`/`reload`) |
| Relationship kinds `consistent-with` and `inconclusive` | INV-031 (`catalog.json:994-1040`, options `:1003-1008`); OD-INDY-23 (1) (`Indy-init:docs/OWNER-DECISIONS-2026-11-03.md:30`) | J2-06 uses `inconclusive` (`j2-analysis.spec.mjs:73-77`) |

## 2. Verdict: not lossless for any of the four

The whole v1.0.0 wire was checked: the envelope (`causa-data:contract/v1.0.0/CONTRACT.md:131-169`), all 31 payloads
(`:246-578`), the enumerations (`:180-214`), the state and snapshot (`:580-890`) and the manifest (`:897-904`).

**Structural rules that close every workaround:**

- Every object is closed (`additionalProperties: false`), and unknown fields are rejected, never ignored. "v1.0.0
  defines **no extension namespace**" (`CONTRACT.md:55-56`). An extra key is `CAUSA.SCHEMA_VIOLATION` at stage 7
  (`:972`). The app's reader re-encodes each payload and refuses any key difference (`app:src/Causa.Persistence/Reader.fs:1016-1020`).
  An extension field is therefore not merely ignored, it is refused. The contract defines none, so none is lossless.
- `domainPacks` is `[]`, and no domain packs exist in v1 (`CONTRACT.md:903`). `AttachExtension`/`ExtensionAttached`
  cannot be encoded (`Reader.fs:57`, `:201`). §13.5: "Extensibility is achieved only through the versioned change
  classes" (`:1070-1074`).
- Enumerations are closed with "no fallback `other` value" (`:81`).
- Packing a value into another field's text, such as "label: … source: …" in `source` or `content`, is not lossless.
  The contract defines no structure inside verbatim text (`:59-63`), so a reader cannot recover the value.
  `content` is the admitted evidence itself (R3), and `source` is provenance.

| value | nearest v1.0.0 field | verdict | decisive evidence |
|---|---|---|---|
| Evidence label | none. `EvidenceAdmitted` = `evidenceId, contentKind, content, category, source, capturedAt` (`CONTRACT.md:379-392`); evidence entity (`:746-761`) | **No** | No label key exists. `content` and `source` carry other facts. The UI contract already models it as `Label: string option`, "because causa-data v1.0.0 has no label field (C5)" (`app:src/Causa.App/Projections.fs:526`; frozen `07-IF41-VIEW.fs:523-533`). |
| Observation source reference | none. `ObservationRecorded` = `observationId, original, observedAt` (`:495-503`); `InvestigationStarted` = `title, observationId, original` (`:461-469`) | **No** | No source key exists, and `original` must be verbatim (R1.2). |
| Evidence source URL | `EvidenceAdmitted.content` only when `contentKind = artifact` (`:392`) | **No** | With `artifact`, the URL *replaces* the content: the user's text content (INV-012) is then lost, so evidence cannot have both. `source` is free text with no defined URL slot (also OD-HC-06's finding, `Indy-init:proposals/r10-historical-data-compatibility/SCHEMA-DIFF.md:184`). |
| `consistent-with`, `inconclusive` | `relationshipKind` = `supports, contradicts, discriminates, insufficient` (`CONTRACT.md:197`; `app:src/Causa.Domain/Semantics.fs:167-171`, `:310-313`) | **No** | Neither value exists. Mapping `inconclusive` to `insufficient`, or `consistent-with` to anything, assigns a meaning no source states. OD-HC-07 B (recorded, `Indy-init:kickoff/rehearsal-007/approvals.causa.json:574-578`) chose "show the wire kind verbatim", explicitly not `insufficient` = `inconclusive`. |

Related values the issue did not ask about, for completeness:

- **INV-028** evidence source reference maps losslessly to `EvidenceAdmitted.source`. The wire requires it non-blank,
  however, while the catalog says `required: false` (`catalog.json:859`). OD-CR-03 A (recorded) makes INV-028
  required, but the catalog is not yet amended.
- **J2-06's "Reassess"** keeps the earlier assessment and must leave one current assessment per pair (INV-030 note,
  `catalog.json:991`; OD-INDY-23 (3)). In v1.0.0 it can only be a second `EvidenceRelated` for the same pair. The app
  already treats the latest as current (`app:src/Causa.Domain/Policies.fs:99-126`). The registry, however, refuses a
  second link (`Indy-init:docs/contract-readiness/causa-precompetition/COMMAND-EVENT-REGISTRY.json:304-328`), and OD-CR-01
  A provides `EvidenceRelationshipRevised` for it. See decision S-8.

## 3. Relation to decisions already recorded

- **ODR-R10-26 A** (`approvals.causa.json:645`): "causa-data storage contract v1.1 adds them".
- **OD-HC-06 B** (`:564`): nothing is fabricated for historical cases.
- **OD-HC-07 B** (`:575`): `insufficient` and `discriminates` are shown verbatim.
- **OD-CR-01 A** (APR-CR-OWNER-1 and -2, `:956-1043`): release causa-storage 1.1.0 "exactly as DOMAIN-CONTRACT.json
  proposedStorageExtension". The pinned sha256 `8f4deaec…` equals the file at `89f5840`. That content is 6 events
  and 2 enum values (`Indy-init:docs/contract-readiness/causa-precompetition/DOMAIN-CONTRACT.json:34-70`).

**The minimum extension for the four values is a strict subset of that recorded content:**

| | in the minimum | also in OD-CR-01 A (for INV-023/-034/-035/-030 update, outside this issue) |
|---|---|---|
| events | `EvidenceRevised` (`DOMAIN-CONTRACT.json:57-61`), `ObservationRevised` (`:49-52`) | `InvestigationDetailsRevised`, `HypothesisRevised`, `EvidenceRelationshipRevised`, `EvidenceRelationshipWithdrawn` |
| enum values | `relationshipKind` + `consistentWith`, `inconclusive` (`:42`) | — |

**Alternative considered and not recommended: widen the existing payloads.** This would add `label` and `sourceUrl`
to `EvidenceAdmitted` and `source` to `ObservationRecorded`: 3 fields and no new event. It needs two schemas for the
same `eventType`, selected by `formatVersion`. It contradicts OD-CR-01 A's rule "no v1.0.0 event changes"
(`DOMAIN-CONTRACT.json:38`). It also still needs revision events for INV-035. Adopting it would need a new owner
decision that replaces OD-CR-01 A.

The recorded content names its fields but leaves the release rules unspecified: versions, mixed histories, the
projection, admissibility of the new events, the engine authority, fixtures and the gate. §4 to §7 specify them. Each
point that goes beyond the recorded text is listed in §9 as a decision.

## 4. The extension: causa-storage 1.1.0, minimum content

**Change class:** "compatible additive extension": a new event type and a new enum value give a new minor version
`1.x.0` with its own tree (`CONTRACT.md:1045`). The tree is `contract/v1.1.0/`, following `§3`'s `contract/v1.0.0/`
layout (`:93-104`). §13.1 writes `contract/1.x.0/`, without the `v`; see S-9.

### 4.1 New event `EvidenceRevised`

Descriptive metadata of evidence. The admitted `content` is never revisable here (OD-CR-06 A).

| field | type | nullable | constraint |
|---|---|---|---|
| `evidenceId` | uuid | no | lowercase canonical, non-nil |
| `label` | text | no | non-blank, verbatim (INV-027) |
| `source` | text | no | non-blank, verbatim (INV-028) |
| `sourceUrl` | httpUrl | yes | absolute http(s) URL, verbatim (INV-029) |

`sourceUrl` uses `common.schema.json` `httpUrl` (`causa-data:contract/v1.0.0/schemas/common.schema.json:64-68`). Full
well-formedness is an admissibility rule, as for artifact content (`CONTRACT.md:243-244`; `app:src/Causa.Domain/Evolve.fs:132-135`).

### 4.2 New event `ObservationRevised`

| field | type | nullable | constraint |
|---|---|---|---|
| `observationId` | uuid | no | lowercase canonical, non-nil |
| `original` | text | no | non-blank, verbatim (INV-024) |
| `source` | text | yes | non-blank, verbatim (INV-025) |

### 4.3 Enumeration

`relationshipKind` = `supports`, `contradicts`, `discriminates`, `insufficient`, **`consistentWith`**, **`inconclusive`**.
The UI maps `consistent-with` ↔ `consistentWith` and `inconclusive` ↔ `inconclusive`. `insufficient` and
`discriminates` keep their own meaning and are never written for an INV-031 option (OD-HC-07 B).

### 4.4 Registry

There are 33 event types: the 31 of `CONTRACT.md:221-230`, then `EvidenceRevised` and `ObservationRevised`. With the
rest of OD-CR-01 A there are 37. Unregistered types still fail closed (`:232-233`).

### 4.5 Writing at creation (one command group)

There is no new field on `EvidenceAdmitted` or `ObservationRecorded`. The entered values travel in a revision event
**in the same command group**, which shares `correlationId`, `occurredAt` and `actor` (`CONTRACT.md:160-165`). This
follows the registry (`COMMAND-EVENT-REGISTRY.json:159-180`, `:252-276`).

- `AdmitEvidence` (INV-012/027/028/029) emits `EvidenceAdmitted{…, source: S}` and then **always**
  `EvidenceRevised{evidenceId, label: L, source: S, sourceUrl: U|null}`, because INV-027 is required.
- `RecordObservation` (INV-024/025) emits `ObservationRecorded{…, original: O}` and then
  `ObservationRevised{observationId, original: O, source: R}` **only when INV-025 is entered**. Without INV-025 no
  revision is written, which is lossless because nothing was entered.
- **Creation equality rule (new):** in such a group, `EvidenceRevised.source` equals `EvidenceAdmitted.source`, and
  `ObservationRevised.original` equals `ObservationRecorded.original`, byte for byte. This mirrors §7.2's
  "no redundant copies … a writer MUST refuse where they differ" (`CONTRACT.md:234-236`). A writer refuses a
  difference (`CAUSA.UNREPRESENTABLE`); a reader refuses it at stage 15 (domain code `EVIDENCE.REVISION_MISMATCH` /
  `OBSERVATION.REVISION_MISMATCH`). See S-3.

### 4.6 Admissibility of the new events (1.1.0 semantics)

| event | admissible iff | domain code on refusal |
|---|---|---|
| `EvidenceRevised` | the investigation is open for recording | `INVESTIGATION.NOT_OPEN` (as `Evolve.fs:118-119`) |
| | the evidence exists | `HISTORY.INADMISSIBLE_EVENT` (update of a missing id) |
| | the evidence is not superseded (`COMMAND-EVENT-REGISTRY.json` CMD-EVD-EDIT precondition) | `EVIDENCE.SUPERSEDED` |
| | `sourceUrl` is null or a well-formed absolute http(s) URL whose `OriginalString` equals the value | `EVIDENCE.URL` |
| | the creation equality rule holds | `EVIDENCE.REVISION_MISMATCH` |
| `ObservationRevised` | the investigation is open; the observation exists (including the `InvestigationStarted` observation); the creation equality rule holds | `INVESTIGATION.NOT_OPEN` / `HISTORY.INADMISSIBLE_EVENT` / `OBSERVATION.REVISION_MISMATCH` |
| `EvidenceRelated` with `consistentWith` or `inconclusive` | as for the existing kinds | — |

Neither new kind is support. The cause-confirmation gate counts only `supports`
(`app:src/Causa.Domain/Policies.fs:203-205`, `:272`), and that stays true: `consistentWith` "fits but does not favour
it" (`catalog.json:1039`). No transition, gate or projection other than the relationship entity reads the new kinds.

### 4.7 Projection (state) for 1.1.0 investigations

These rules are added to §8.3 (`CONTRACT.md:600-640`) and apply only when the investigation's version is 1.1.0
(§5.1):

| change | rule |
|---|---|
| evidence entity | gains `label` (text \| null) and `sourceUrl` (httpUrl \| null). `EvidenceAdmitted` creates them as `null`. |
| observation entity | gains `source` (text \| null). `InvestigationStarted` and `ObservationRecorded` create it as `null`. |
| `EvidenceRevised` | evidence `label ← label`, `source ← source`, `sourceUrl ← sourceUrl` |
| `ObservationRevised` | observation `original ← original`, `source ← source` |
| `relationship.kind` | the extended enum |

Provenance never changes (`:604-605`). Earlier values stay in `events.ndjson`, as INV-035 requires ("the earlier text
and its provenance remain in the history", `catalog.json:1200`). No new snapshot array is added.

## 5. Versions, readers and writers

### 5.1 Version fields

| file | 1.0.0 data (unchanged) | 1.1.0 data |
|---|---|---|
| event line `formatVersion`, `semanticVersion` | `"1.0.0"` | `"1.1.0"` |
| manifest `formatVersion`, `schemaVersion`, `semanticVersion` | `"1.0.0"` | `"1.1.0"` |
| snapshot `formatVersion`, `semanticVersion` | `"1.0.0"` | `"1.1.0"` |
| manifest `snapshotVersion`, snapshot `projectionVersion` | `1` | `1`, see S-5 |

**Homogeneity (new rule):** every line and file of one investigation carries the same version. Its version is fixed by
`InvestigationStarted`. A file set whose version strings differ is `CAUSA.UNSUPPORTED_VERSION` at stage 5 (new
negative fixture `mixed-version`). This applies the existing rule that writers "never upgrade a v1.0.0 investigation in
place" (`CONTRACT.md:1054-1055`).

`semanticVersion` moves to 1.1.0 because new events gain meaning. No v1.0.0 event or history changes meaning (§13.1,
"breaking semantic change", does not apply). Event-id derivation, including the `…/contract/v1/event-id` namespace
(`:150-156`), RFC 8785 canonical form, the timestamp, UUID and text rules, and the error stages 1–17 are unchanged.

### 5.2 Reader behaviour

| reader | 1.0.0 data | 1.1.0 data |
|---|---|---|
| **1.0.0 reader** (frozen; the app's current `StorageReader`) | as today | **Refused whole** at stage 5, `CAUSA.UNSUPPORTED_VERSION`: "rejects every other formatVersion/schemaVersion/semanticVersion … never best-effort" (`CONTRACT.md:970`, `:1051-1052`; `app:src/Causa.Persistence/Reader.fs:929-934`). No partial load (`:993`). This meets EXT-DATA-011-O2 "fail closed on incompatible data". |
| **1.1.0 reader** | **exactly the v1.0.0 rules**: the v1.0.0 schemas, the 31-type registry, the v1.0.0 enums and the v1.0.0 projection. A `"1.0.0"` line with `EvidenceRevised` is `CAUSA.UNKNOWN_EVENT_TYPE`; with kind `consistentWith`, `CAUSA.SCHEMA_VIOLATION`. Every digest is unchanged (§13.3, `:1059-1062`). | the 1.1.0 schemas, the 33-type registry and the §4.7 projection |
| either reader, any other version string (for example `"1.2.0"`, `"2.0.0"`) | `CAUSA.UNSUPPORTED_VERSION` | — |

Admissibility (stage 15): the reference engine pinned for 1.0.0 (`causa-data:engine.lock.json`, commit `55f49f61…`)
knows no 1.1.0 event. A consumer that cannot evaluate admissibility must not report a history valid
(`CONTRACT.md:1005-1007`). 1.1.0 therefore needs an admissibility authority for the §4.6 rules (S-2).

### 5.3 Writer behaviour

- **New investigations** are written in 1.1.0 (§13.2: "Writers always write the newest version they implement, but
  only into new workspaces"). Under W1 these are working-copy investigations only
  (`Indy-init:requirements-compiled/v4.1.5/README.md:14`). Catalogs are read-only.
- **Appends to a 1.0.0 investigation** stay 1.0.0. A command whose events need 1.1.0 is **refused with
  `CAUSA.UNREPRESENTABLE` and nothing is written**: an evidence label, URL or observation source, or a new kind. The
  pending command is kept (§9.2, `:908-912`). **A value the user entered is never stripped to make a write succeed.**
  Since INV-027 is required, the UI cannot add evidence to a 1.0.0 investigation (S-6).
- **Canonical encoding.** Unchanged JCS. Payload keys sort as `evidenceId, label, source, sourceUrl` and
  `observationId, original, source`. Example (illustrative; the event id is derived by §5.2 for investigation
  `11111111-1111-4111-8111-111111111111`, version 8, correlation `22222222-2222-4222-8222-222222222222`, computed with
  UUIDv5 and not checked against a reference consumer):

```text
{"actor":{"actorId":"causa-operator","kind":"human"},"aggregateVersion":8,"correlationId":"22222222-2222-4222-8222-222222222222","eventId":"b7913566-2780-5c7d-8204-74558fb33ee8","eventType":"EvidenceRevised","formatVersion":"1.1.0","investigationId":"11111111-1111-4111-8111-111111111111","occurredAt":"2026-11-06T12:00:00.000Z","payload":{"evidenceId":"33333333-3333-4333-8333-333333333333","label":"Gateway 502 log","source":"ops runbook capture","sourceUrl":"https://example.invalid/logs/502"},"semanticVersion":"1.1.0"}
```

- Lengths are not storage rules. The UI and the command boundary enforce INV-027 ≤200, INV-025/028 ≤10,000 and
  INV-029 ≤2,000 with its pattern, as v1.0.0 has no length limits (S-4).

## 6. Release mechanics in causa-data (owner: causa-data)

1. `contract/v1.1.0/`: `CONTRACT.md` (v1.0.0 plus §4–§5), schemas (2 payloads; extended enum; envelope `oneOf` +2;
   evidence, observation and relationship entity schemas; version consts `"1.1.0"`), `bundle.json` (a new schema
   identity), `FIELD-MAPPING.md`, and golden files.
2. `fixtures/v1.1.0/`: at least one valid investigation that exercises both events, both kinds, a URL, a null URL, an
   observation with and without a source, and a revision after creation. Negative fixtures as in §7.
3. `compatibility/v1.1.0/lock.json` with its `RELEASE`, anchored to a new tag `causa-storage-v1.1.0`. The v1.0.0 gate,
   `causa gate . --anchor causa-storage-v1.0.0` (`CONTRACT.md:1107-1114`), must still pass unchanged.
4. The engine pin or admissibility authority for 1.1.0 (S-2), and the reference consumers in `tools/consumers`.

## 7. Compatibility and migration test plan (nothing has been run)

| id | case | expected |
|---|---|---|
| ST-01 | 1.1.0 reader on all 5 `fixtures/v1.0.0` investigations and every catalog history (`demonstration-v1` 1,363, `historical-v1` 1,315, `everyday-v1` 48 events) | all stages pass; replayed `state` digest = `expectedStateSha256`; snapshot and manifest bytes as stored; `encode(decode(line)) == line` for every line |
| ST-02 | 1.1.0 reader on all 90 `fixtures/v1.0.0/negative` fixtures | same primary and domain codes as the 1.0.0 reader. None contains `"1.1.0"`, `consistentWith` or the new types (checked by a byte scan of the base64 payloads, not by a run) |
| ST-03 | `causa gate . --anchor causa-storage-v1.0.0` after the release | PASS; frozen 1.0.0 paths and lock unchanged |
| ST-04 | 1.1.0 J2 history: start; observation + `ObservationRevised` (source); 2 hypotheses; 2 × evidence + `EvidenceRevised` (label, source, URL / null URL); `supports`, `contradicts`; `inconclusive`; `consistentWith`; conclusion | the writer's file set verifies. Decode then encode reproduces `events.ndjson`, `snapshot.json` and `manifest.json` **byte for byte**. A second, independent consumer projects the same `state` digest. |
| ST-05 | no value dropped: every string entered in ST-04 (label, observation source, evidence source, URL, rationale, kind) | appears verbatim, JSON-escaped per JCS, in `events.ndjson`, and in the projected entity after reload. The J2-09 `providerHolds` analogue (`j2-analysis.spec.mjs:95`). |
| ST-06 | 1.0.0 reader (the app's current `StorageReader.verify`, and causa-data at the v1.0.0 tag) on the ST-04 set | `CAUSA.UNSUPPORTED_VERSION`, stage 5, `manifest.json`; nothing loaded |
| ST-07 | mixed version: a 1.1.0 line appended to a 1.0.0 history, or a 1.1.0 manifest over 1.0.0 lines | `CAUSA.UNSUPPORTED_VERSION` (new fixture `mixed-version`). As a successor, the §9.3 prefix check is never reached. |
| ST-08 | `"1.0.0"` line with `eventType: EvidenceRevised` / with `kind: consistentWith` | `CAUSA.UNKNOWN_EVENT_TYPE` / `CAUSA.SCHEMA_VIOLATION` |
| ST-09 | writer: evidence with a label appended to a 1.0.0 working-copy investigation | `CAUSA.UNREPRESENTABLE` before any write; pending command kept; remote unchanged |
| ST-10 | creation-equality violations; `EvidenceRevised` on missing or superseded evidence; `ObservationRevised` on a missing observation; `sourceUrl` = `javascript:alert(1)`, `https://a b`, `http:/x` | `CAUSA.HISTORY_INADMISSIBLE` with the §4.6 domain code, or `CAUSA.SCHEMA_VIOLATION` for a schema-pattern failure |
| ST-11 | verbatim edge cases: label with leading or trailing spaces, U+200B, NFD vs NFC, exactly 200 characters; URL `HTTPS://Example.invalid/%7Ex` (case and percent-encoding kept); observation source with `\n` | stored and re-read ordinally equal, with no normalization |
| ST-12 | relationship kinds: all 6 values round-trip; historical `insufficient` shown verbatim, never written as `inconclusive` (OD-HC-07 B); cause gate with only `consistentWith` evidence | round-trip exact; `cause.no-supporting-evidence` |
| ST-13 | the app's `EXT-DATA-011-O2` test (`app:tests/Causa.Acceptance/Live/EXTDATA011Tests.fs:546-566`) once the app reads 1.1.0 | its `"semanticVersion 1.1.0"` refusal case is replaced by an unsupported `"1.2.0"` case, and ST-06 and ST-07 are added. The test still proves fail-closed behaviour. |
| ST-14 | migration | **none**: v1.0.0 files are never rewritten (§13.1, §13.5). No 1.0.0 investigation gains a label, source or new kind (OD-HC-06 B). |

## 8. What changes in the competition repository once approved

Owners are from `Indy-init:requirements-compiled/v4.1.5/agent-file-ownership.json`. The frozen interface also changes:
`CoreEvent` gains `EvidenceRevised` and `ObservationRevised`, and OD-CR-01 A's `ReviseEvidence` and `ReviseObservation`
commands are added in the successor package (v4.1.6, or v4.1.7 if RC-1 A stages it). This is breaking
(`AGENT-RULES.md:37`), with consumer contract tests in the same pull request. The UI contract needs **no** change: it
already has `EvidenceCard.Label`/`SourceUrl`, `ObservationCard.SourceReference` and
`RelationshipKind.ConsistentWith`/`Inconclusive` (`07-IF41-VIEW.fs:489-533`).

| file | owner | change |
|---|---|---|
| `src/Causa.Domain/Semantics.fs` | domain | `RelationshipKind` + `ConsistentWith`, `Inconclusive` (`:167-171`); wire map (`:310-313`) |
| `src/Causa.Domain/Entities.fs` | domain | records `EvidenceRevision{EvidenceId; Label; Source; SourceUrl}` and `ObservationRevision{ObservationId; Original; Source}`; `EvidenceEntry` gains `Label` and `SourceUrl`, `Observation` state gains `Source` (state only; the `Observation`/`Evidence` event records stay exactly the v1.0.0 wire) |
| `src/Causa.Domain/Events.fs` | domain | two `CoreEvent` cases; `caseName` |
| `src/Causa.Domain/Commands.fs` | domain | `AdmitEvidence` carries `Label` and `SourceUrl`; `RecordObservation` carries `Source option` (opaque payload types; `Commands.fs:10`, `:21` currently alias the event records); `ReviseEvidence` and `ReviseObservation` cases |
| `src/Causa.Domain/Decide.fs` | domain | emit the creation groups (§4.5); §4.6 checks |
| `src/Causa.Domain/Evolve.fs` | domain | §4.6 admissibility; `step` updates; version-dependent: refuse new events in a 1.0.0 investigation |
| `src/Causa.Domain/Capabilities.fs`, `Kernel.fs` | domain | probes for the revise commands; `InterfaceManifest.storageContract` (`Kernel.fs:89`) |
| `src/Causa.Persistence/Reader.fs` | persistence | encode and decode the two events and the enum (`:81-104`, `:595-630`); `registered` per version (`:888-896`); `versionsOk` accepting `{1.0.0, 1.1.0}` plus homogeneity (`:929-934`); the §4.7 projection by version (`:295-306`); snapshot and manifest versions instead of the hard-coded `"1.0.0"` (`:401-413`); writer refusal ST-09 (`FileSets`, `:415+`) |
| `src/Causa.Persistence/Canonical.fs`, `Wire.fs`, `Storage.fs` | persistence | no JCS change expected; version constants and doc comments (`Storage.fs:6`) |
| `tests/Causa.TestSupport/StatefulFake.fs`, `Recordings.fs` | persistence | 1.1.0 file sets |
| `tests/Causa.Contract.Tests/Storage/*`, `Wire/Producer.fs` | persistence | ST-01, ST-04 to ST-11 |
| `tests/Causa.Contract.Tests/Domain/Producer.fs`, `tests/Causa.Domain.Tests/Identity.fs` | domain | case lists and representatives |
| `tests/Causa.Contract.Tests/Domain/Consumer*.fs`, `Wire/Consumer*.fs` | integration, persistence, ui, ui-2 | consumer tests for the new cases |
| `tests/Causa.Acceptance/Live/EXTDATA011Tests.fs` | domain | ST-13 |
| `src/Causa.App/Engine.fs`, `Ui/Presentation.fs`, `Ui/UiAdapter.fs`, `Update.fs` | integration | map INV-025/027/029/031 values to commands; project `Label`, `SourceUrl` (through `SafeUrl.tryCreate`), `SourceReference` and the kinds (`Projections.fs:496-499`); the `CatalogKind`/`UninterpretedKind` split stays |
| `src/Causa.App/Views/Workspace/Evidence.fs`, `Links.fs`, `Overview.fs`, `Timeline.fs` | ui-3 (ui in topologies A/B) | fields and options become writable rather than "No label recorded" |
| `tests/fixtures/causa-data` | integration | 1.1.0 fixtures once causa-data tags them |

Nothing in `Indy-init` is edited for this. Indy-init's part is the successor package and the re-pin of the
Rehearsal 010 lock (FREEZE step 5 pattern).

## 9. Owner decisions required

Already recorded and not asked again: ODR-R10-26 A, OD-HC-06 B, OD-HC-07 B, OD-CR-01 A (the content), OD-CR-03 A and
OD-CR-06 A. The decisions below fill in what the recorded content leaves open. The causa-data owner (S-1, S-2, S-5,
S-9) and the Indy-init owner (the rest) are the same person, `kemiller2002`.

| id | decision | options | recommendation |
|---|---|---|---|
| **S-1** | **Release causa-storage 1.1.0** with the §4–§6 rules, as the concrete form of OD-CR-01 A. All six recorded events, or the minimum two now. | **A:** all of OD-CR-01 A (6 events, 2 values) in one release. **B:** the minimum (§4: 2 events, 2 values) as 1.1.0, and the other four as 1.2.0. | **A** if it can be released before Rehearsal 010's re-pin, because it is one cycle and matches the record. **B** otherwise, which needs a record because it narrows OD-CR-01 A. |
| **S-2** | **Admissibility authority for 1.1.0.** The pinned engine (`55f49f61`) has no new events. | **A:** pin a new reference engine commit that implements §4.6. **B:** state §4.6 normatively in CONTRACT 1.1.0 and accept the app kernel plus negative fixtures as the oracle for the new rules (extending OD-HC-03 A). **C:** release with readers reporting "structurally valid, admissibility not evaluated" for 1.1.0 (§11.3). | **A**; **B** if no engine change is available. **C** blocks EXT-DATA-005-style "verified" claims. |
| **S-3** | **Creation equality rule** (§4.5) | A: adopt (writer refuses, reader stage 15). B: no rule: the revision simply wins. | **A.** It keeps one meaning per creation, in the spirit of §7.2. |
| **S-4** | **Length limits** for label, source and URL | A: UI and command boundary only (v1.0.0 practice). B: also in the 1.1.0 schemas (`maxLength`). | **A** |
| **S-5** | **`projectionVersion`/`snapshotVersion`** for 1.1.0. §13.4 calls a projection change a migration, while §13.1 lists additive extensions as minor. | A: keep `1`, because the 1.0.0 projection is unchanged and 1.1.0 is told apart by `formatVersion`. B: `2` for 1.1.0 snapshots. | **A**, recording that §13.4 means changing the projection of existing data |
| **S-6** | **1.0.0 working-copy investigations** under a 1.1.0 app | **A:** they accept only commands representable in 1.0.0, and the rest are refused (`UNREPRESENTABLE`) with the reason shown. **B:** a deterministic migration to a new 1.1.0 investigation in a new namespace (§13.1 migration class). | **A.** A fresh Rehearsal 010 working copy holds no 1.0.0 investigations, and catalogs are read-only. |
| **S-7** | **Label requiredness on the wire** | A: `EvidenceRevised.label` non-null (as §4.1), and evidence state `label` nullable (null between admission and revision, and for 1.0.0). B: nullable on the wire too. | **A** (INV-027 is required) |
| **S-8** | **J2-06 "Reassess"** under the minimum only (S-1 B) | A: wait for `EvidenceRelationshipRevised`. B: ratify "a new `EvidenceRelated` for the same pair is the reassessment; the latest is current" (representable in v1.0.0; it is what the app does at `Policies.fs:99-126`), and drop CMD-REL-LINK's second-link refusal. | **A** with S-1 A. **B** only if S-1 B and J2-06 is needed before 1.2.0. |
| **S-9** | **Tree name** | `contract/v1.1.0/` (as §3) or `contract/1.1.0/` (as §13.1's text) | `contract/v1.1.0/` |
| **S-10** | **Successor package and re-pin**: v4.1.6 (or v4.1.7 after RC-1 A) with the two `CoreEvent` and two `CoreCommand` cases; owner approval, tag and Rehearsal 010 scope re-approval | approve / reject | approve after X01–X21, an independent review, and ST-01 to ST-06 passing against the tagged causa-storage 1.1.0 |
| (related) | **Catalog amendments already decided but not applied**: INV-028 `required: true` (OD-CR-03 A), INV-045 category (OD-CR-02 A) and INV-046 strength (OD-CR-04 A). J2-03 fills no category, so see RC-3. | apply via the catalog's change process | apply together with S-10 |
