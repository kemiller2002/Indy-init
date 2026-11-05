# Causa data repository contract (Rehearsal 010)

**Status:** read-only investigation of the real repository. Nothing in causa-data or causa was modified, and no format below is invented: every statement cites the file and line it comes from. The mismatches in §6 are **not resolved here**; ADRP-005-O4 forbids an agent to improvise event shapes, so each needs an owner decision (OWNER-DECISIONS-REQUIRED.md, ODR-R10-25..29).

**Sources examined**
- `kemiller2002/causa-data`: anonymous clone, `main` at `e8259df74add723765c57a5b1a5888d9c3f707af` (2026-10-29, "Merge pull request #8"). Tag `causa-storage-v1.0.0` → `c947c7e354be80766573a782546c8186336774cc` (`git ls-remote --tags origin`).
- `kemiller2002/causa`: **the clone failed.** `git clone https://github.com/kemiller2002/causa` answered `fatal: could not read Username for 'https://github.com': terminal prompts disabled`. `https://github.com/kemiller2002/causa` and `https://api.github.com/repos/kemiller2002/causa` both returned HTTP 403 through the session proxy. The same proxy served causa-data anonymously, so the repository is private or does not exist. Nothing below is taken from it.
- Indy-init (this repository, at the branch base), using the files the task names.

Paths in causa-data are given relative to the clone. Paths in Indy-init are given relative to the repository root.

---

## 1. Authoritative schema, storage paths and file format

### 1.1 Authority
- The normative specification is `contract/v1.0.0/CONTRACT.md` together with the JSON Schema Draft 2020-12 files in `contract/v1.0.0/schemas/`. Schema identity is the SHA-256 of `schemas/bundle.json` (CONTRACT.md:7, :946-949).
- Where the text and a schema disagree, the input is invalid until the disagreement is resolved (CONTRACT.md:13-15).
- **Events are the only authority.** Snapshots, manifests and indexes are derived from them (CONTRACT.md:23-24).
- Domain admissibility belongs to the engine pinned in `engine.lock.json`. That is `kemiller2002/indy-init-rehearsal-001@55f49f61…`, with `Decide.decide`, `Evolve.apply` and `History.validate` (CONTRACT.md:8, :25-27; engine.lock.json:2-3).
- The engine repository is private. `tools/fetch-engine.sh:13-15` says so and fetches it with a read-only token.
- The contract is frozen: "Never migrate, overwrite or reshape frozen files" (README.md:90-92).
- `fixtures/v1.0.0/` and `contract/v1.0.0/` are immutable after the tag (CONTRACT.md:114-116).

### 1.2 Physical layout (CONTRACT.md:93-104)
```
fixtures/v1.0.0/investigations/<investigationId>/{manifest.json,events.ndjson,snapshot.json}
workspaces/demo/<runId>/investigations/<investigationId>/{manifest.json,events.ndjson,snapshot.json}
```
- **Paths.** Paths are case-sensitive. `investigationId` is a canonical lowercase UUID equal to the manifest's id. `runId` matches `^[a-z0-9][a-z0-9-]{0,63}$`. Any other name is `CAUSA.PATH_INVALID` (CONTRACT.md:106-111).
- **Files.** Each investigation directory holds exactly those three files. A missing or extra entry, hidden ones included, is `CAUSA.PATH_INVALID` (CONTRACT.md:112-113).
- **Index.** There is no per-workspace index file. Each `workspaces/demo/<w>/` holds only `investigations/` (`find workspaces -maxdepth 3 -type f` returns nothing). Listing a workspace therefore means listing the directory: a Git tree read. The one exception is the active catalog, whose manifest lists its case paths (§3).

### 1.3 Wire format (CONTRACT.md §2, :31-89)
- **Encoding.** UTF-8 with no BOM and LF line endings only.
- **Canonical form.** Every JSON text is its RFC 8785 (JCS) canonical serialization. A JSON file is one canonical text plus one LF. `events.ndjson` is one canonical event per line. Non-canonical bytes are `CAUSA.NON_CANONICAL` (:33-44).
- **Values.** No floats. Integers fall in ±(2^53−1). Objects are closed (`additionalProperties: false`), with **no extension namespace** (:55-56, §13.5 :1070-1074). There are **no optional keys**: an absent option is `null` (:57-58).
- **Timestamps.** RFC 3339 UTC with exactly three fractional digits. Writers MUST refuse sub-millisecond instants (:71-80).
- **Event envelope** (`event-envelope.schema.json`, CONTRACT.md:131-169):
  - Fields: `formatVersion`, `eventId`, `investigationId`, `aggregateVersion`, `eventType`, `occurredAt`, `actor`, `semanticVersion`, `correlationId` and `payload`.
  - `eventId` is derived: UUIDv5 over `investigationId/aggregateVersion/correlationId` with namespace `241fefd4-…` (:150-156).
  - `InvestigationStarted` is version 1 (:148-149). A correlationId names one command group (:160-165).
- **Event registry.** There are 31 event types, exactly the engine's `EventBody` cases (CONTRACT.md:221-230). An unknown type invalidates the whole history (:232-233).
- **Snapshot.** `snapshot.json` has `formatVersion`, `investigationId`, `aggregateVersion`, `projectionVersion` (1), `semanticVersion`, `generatedAt` and `state` (:584-586).
  - It must be byte-identical to the replay projection (:588).
  - `state` holds 17 entity arrays (:594-598). The fold rules are in :600-640.
- **Manifest.** `manifest.json` has `investigationId`, `aggregateVersion` and `eventCount` (both equal to the line count), `eventsSha256`, `snapshotVersion` (1), `snapshotSha256`, `domainPacks: []`, `createdAt`, `updatedAt`, `formatVersion`, `schemaVersion` and `semanticVersion` (:899-904).
  - It carries no Git SHA, branch or other provider metadata (:904, :28-29).
  - A real example is `workspaces/demo/demonstration-v1/investigations/fc3f71a9-…/manifest.json` (448 bytes, one line).
- **Reader stages.** A reader runs 17 stages, from `CAUSA.PATH_INVALID` to `CAUSA.SNAPSHOT_MISMATCH`, and reports the first one that fails (:959-982). Readers fail closed and never partially load (:993).
- **Admissibility.** A consumer that cannot evaluate admissibility MUST NOT report a history as valid (:1005-1007).

### 1.4 Payload facts the app's content depends on (CONTRACT.md §7, generated tables)
| Event | Fields (all required; "nullable" means `null` allowed) | Lines |
|---|---|---|
| `InvestigationStarted` | `title` (non-blank), `observationId`, `original` (non-blank) | :461-469 |
| `ObservationRecorded` | `observationId`, `original`, `observedAt` (nullable) | :495-503 |
| `HypothesisRecorded` | `hypothesisId`, `statement`, `mechanism` (nullable) | :436-444 |
| `EvidenceAdmitted` | `evidenceId`, `contentKind` (`text`\|`artifact`), `content`, `category` (7 values), `source`, `capturedAt` (nullable) | :379-392 |
| `EvidenceRelated` | `relationshipId`, `evidenceId`, `hypothesisId`, `kind` ∈ {`supports`,`contradicts`,`discriminates`,`insufficient`}, `rationale` (nullable) | :394-404, enum :197 |
| `EvidenceSuperseded` | `evidenceId`, `replacementEvidenceId`, `reason` | :406-414 |
| `ConclusionRecorded` | `conclusionId`, `statement`, `strength`, `causeIds`, `residualUncertainty` (nullable) | :315-325 |

FIELD-MAPPING.md:30 adds that the engine's decide "falls back to the original text when the title is blank".

---

## 2. Versioning and history

- **Version fields.**
  - `formatVersion` versions the wire shapes. `semanticVersion` versions domain meaning. Both are `"1.0.0"`, as is `schemaVersion` in manifests.
  - `snapshotVersion` (manifest) and `projectionVersion` (snapshot) are both `1` (CONTRACT.md:5, :1064-1068).
  - A v1.0.0 reader rejects any other string value with `CAUSA.UNSUPPORTED_VERSION` and never "best-effort" reads (:970, :1051-1052).
  - Writers never upgrade a v1.0.0 investigation in place (:1054-1055).
- **Change classes.** An additive extension needs a new `contract/1.x.0/` tree. A rename or projection change needs a v2.0.0 migration into a new namespace (CONTRACT.md:1040-1047).
- **Per-investigation history.**
  - History is the append-only `events.ndjson`. `aggregateVersion` runs 1..N with no gaps (:140, :148).
  - An appended event is never edited. Corrections are new events (:166-167).
  - An optimistic append carries the observed version and is refused with `CAUSA.VERSION_STALE` (:906-915).
  - A successor file set must start with the previously accepted `events.ndjson` bytes, or it is `CAUSA.HISTORY_REWRITTEN` (:917-922).
- **Release identities.** There are three: the schema identity, the dataset identity (`fixtures/v1.0.0/manifest.json`) and the release identity (`compatibility/v1.0.0/lock.json`). The compatibility gate is anchored to tag `causa-storage-v1.0.0` (CONTRACT.md:950-955, :1107-1114). Indy-init records the same three digests as verified at `c947c7e3` (`requirements-compiled/v4/contract-bindings.json`:175-, CT-006).
- **Catalog versioning.**
  - `catalog/active.json` is `{"activeCatalog":"demonstration-v1","catalogId":"causa-demonstration","catalogVersion":"1.0.0",…}` (catalog/active.json:1).
  - The catalog manifest carries `catalogVersion`, `formatVersion`, `semanticVersion`, `schemaSha256` and a `template` block (`templateId`, `templateVersion`, `overallTemplateSha256`). It also records per case `expectedVersion`, `expectedLifecycle`, `expectedStateSha256` and the three file digests (`catalog/demonstration-v1/manifest.json`).
- **Git history as provider history.**
  - Commit messages are audit metadata, not semantic data (CONTRACT.md:938).
  - Earlier live runs committed one change per version, for example `89c7902 causa: 88ce4871-… v8 -> v9` and `a76765f causa: c61a0dcd-… v0 -> v2` (`git log`).
  - The historical catalog arrived in `190cce5` (2026-10-23). The four-case demonstration catalog became active in `8954d89` (2026-10-29).

---

## 3. How historical cases are organized, and exactly which exist

**Organization** (README.md:5-39, :53-66):
- **Catalog pointer.** `catalog/active.json` names manifest `catalog/demonstration-v1/manifest.json` and workspace `workspaces/demo/demonstration-v1` (README.md:7).
- **Records.** Each case is an ordinary storage v1.0.0 file set under `workspaces/demo/<catalog>/investigations/<uuid>/` (README.md:31).
- **Research.**
  - Research per case is in `research/historical/<case>/{sources.json,claims.json,analysis.md,adversarial-review.md,REVIEW-RESOLUTION.md,CORRECTIONS.md}`.
  - Provenance per case is in `catalog/historical-v1/provenance/<case>.json` (README.md:32-39).
- **Templates.** Immutable byte-for-byte templates are in `templates/{historical-v1,everyday-v1,demonstration-v1}/`, each with a `template-manifest.json` (README.md:55; file listing).
- **How cases are built.** Cases are produced by issuing commands to the pinned engine with deterministic UUIDv5 ids and a fixed 2026 build clock. Mission times appear only in `observedAt`, `capturedAt` and text (README.md:43).
- **Dataset rule.** No record may be bundled into any application (README.md:31; EXT-DATA-001, EXT-DATA-013).

**Apollo 1, Apollo 11 and Challenger are present.** The active catalog `demonstration-v1` holds four cases (`catalog/demonstration-v1/manifest.json`). `workspaces/demo/demonstration-v1/investigations/` contains exactly these four directories, and the catalog verifier enforces that (tools/py/demonstration_catalog.py:138, :154).

| caseKey | investigationId | Title (from `InvestigationStarted`) | Lifecycle | Events (= version) | Actor kind |
|---|---|---|---|---|---|
| `apollo-1` | `c6b26614-fc43-5013-a15c-cf95942ee07b` | Apollo 1 (AS-204) Command Module fire, 27 January 1967 | `awaitingVerification` | 432 | system ×432 |
| `apollo-11` | `e3adbee5-5096-5a10-bbfb-cecda86a154b` | Apollo 11 lunar landing, July 1969: why the descent succeeded despite program alarms | `awaitingEvidence` | 431 | system ×431 |
| `challenger-sts-51l` | `1784da3c-7141-59ee-a495-1d5d44bf2284` | Space Shuttle Challenger (STS-51-L) loss, 28 January 1986 | `active` | 452 | system ×452 |
| `burned-out-lightbulb` (everyday, not historical) | `fc3f71a9-4c14-5a31-8196-7ce17a7041c1` | Burned-out ceiling lightbulb | `resolved` | 48 | human ×48 |

- **Byte identity.** The three historical cases are byte-identical in `workspaces/demo/demonstration-v1`, `workspaces/demo/historical-v1` and `templates/demonstration-v1`. The SHA-256 of each `events.ndjson`, `manifest.json` and `snapshot.json` matched across the three locations when computed for this draft, as README.md:11 states.
- **Other workspaces.** None of these is a catalog case:
  - `everyday-v1`: the lightbulb only;
  - `live-20261022202313`, `live-20261022202349`, `rest-39850690710` and `rest-39875835357`: synthetic "Live persistence test investigation X/Y", versions 2-9, kept unchanged (README.md:86);
  - `fixtures/v1.0.0/`: the five synthetic fixtures (payment-failure, intermittent-outage, conflicting-metrics, api-latency, authentication-outage) and 90 negative fixtures (README.md:81-86).
- **No other historical cases exist.** `research/historical/` has exactly `apollo-1`, `apollo-11` and `challenger-sts-51l`.

**Event types used by the four active cases** (counted for this draft from `events.ndjson`):
- **Events.** EvidenceAdmitted 382, EvidenceVerificationAssessed 382, EvidenceRelated 195, HypothesisTransitioned 72, ObservationRecorded 75, ContradictionTransitioned 45, CorrectiveActionTransitioned 37, ContradictionRecorded 35 and LifecycleChanged 10, with smaller counts of the others.
- **Hypothesis commands used.** `markPlausible` 33, `markSupported` 23, `markWeakened` 10 and `falsify` 6.
- **Lifecycle commands used.** `activateInvestigation` 4, `markAwaitingVerification` 3, `resolveInvestigation` 1, `markAwaitingEvidence` 1 and `verificationFailed` 1.
- **Relationship kinds used.** `supports` 160, `contradicts` 26 and `insufficient` 9. No evidence–hypothesis pair is related twice.

---

## 4. How the frozen v4.1.4 contracts and the requirements say the app reads and writes

### 4.1 Requirements
- **Connect.**
  - EXT-DATA-002: a fresh launch shows a disconnected workspace and an explicit Connect action, with no fallback to bundled fixtures (`requirements/causa-requirements-baseline-v4.json`:2635).
  - EXT-DATA-003: the user specifies "the repository and permitted branch/workspace" plus a scoped credential, and "a token alone is not sufficient" (baseline:2655).
- **Validation.** EXT-DATA-005: the provider checks repository and branch permissions, validates paths and manifests, and loads only through "the full contract validation and F# history-admissibility pipeline", failing closed (baseline:2705, O1 :2713).
- **Mutation.** EXT-DATA-006: "No data or repository mutation occurs on connect or read". Create is an explicit user action, and write needs "separately verified authorization, optimistic concurrency, and no overwrite of unread remote history" (baseline:2725, O1 :2733, O2 :2738).
- **Creation.** EXT-DATA-008: new file sets are created "from the authoritative schema and engine without copying a demo investigation", and are command-created, schema-validated, replayable and round-trip verified (baseline:2765).
- **Compatibility.** EXT-DATA-011: the frozen storage contract is a reader/writer specification, with versioning and fail-closed reads (baseline:2830).
- **Catalog.** EXT-DATA-013: load the catalog named by `catalog/active.json`, exactly the four cases, through the full validation and F# replay pipeline to the manifest's states, without bundling them (baseline:2875).
- **Persistence.**
  - PERSIST-005: GitHub is the only durable store. Opening means reading at a branch head, verifying "as a reader would (causa-data v1.0.0 §11)" and recording the base head. Head reads bypass caches (baseline:16959).
  - PERSIST-006: one GraphQL `createCommitOnBranch` per save with `expectedHeadOid`, events + manifest + snapshot together, and only under the investigation's prefix (§9.4.4) (baseline:16997).
  - PERSIST-007: a save is confirmed only after a re-read returns the written bytes (§9.4.3) (baseline:17046).
- **Bindings.** `requirements-compiled/v4/contract-bindings.json`:
  - CT-006 (:175) binds storage contract 1.0.0 exactly.
  - CT-007 (:274) records the owner exception to §9.4.1 (OD-INDY-9): `createCommitOnBranch` in place of `PATCH /git/refs`.
  - CT-008 (:311) binds `demonstration-v1`.

### 4.2 Frozen F# contracts (`requirements-compiled/v4.1.4/contracts/`)
- **IF41-STORAGE** (`02-IF41-STORAGE.fs`):
  - `FileSet = {Manifest; Events; Snapshot}` as bytes (:9-12).
  - `VerifiedInvestigation = {Investigation; History; ReadAt: CommitOid; Bytes}` (:22-26).
  - `StorageCodec = {PathOf: InvestigationId -> RepoPath; Encode: VerifiedInvestigation -> EventEnvelope list -> FileSet; Verify: CommitOid -> FileSet -> Result<VerifiedInvestigation, VerificationFailure list>}` (:28-31).
  - The header says "CONTRACT.md wins over docs/CAUSA-DATA-CONTRACT-v1.md" (:5).
  - The implementer is `src/Causa.Persistence/Storage.fs`, owned by persistence (`agent-file-ownership.json`:322; `analysis/interface-specifications.json`:83).
- **IF41-WIRE** (`03-IF41-WIRE.fs`):
  - `Repository = {Owner; Name; Branch}` (:45). `ConnectionContext = {Repository; Credential}` (:48).
  - The opaque `Credential.T` is used only for the Authorization header (:35-43).
- **IF41-ERRORS** (`04-IF41-ERRORS.fs`):
  - `ConnectResult = ConnectAccepted of canWrite: bool | ConnectRejected of ProviderError | ConnectSuperseded`. The comment reads "write access comes only from the permissions GitHub states for the repository, never from a successful read" (:26-31).
  - `Connector = {ConnectRequest; InterpretConnect}` (:34-36).
- **IF41-SAVE** (`05-IF41-SAVE.fs`):
  - SM-LOAD is `LoadState = NotLoaded | Loading | Loaded of VerifiedInvestigation | LoadFailed | UnavailableOffline` (:130-135), driven by `LoadInput.OpenRequested of InvestigationId` (:139-146).
  - `SaveMachine.Initial: VerifiedInvestigation -> InvestigationSession` (:211). `InterpretLoad` and `StepLoad` are fields of the same `SaveMachine` record (:223-224).
  - A save is proven only by `Confirmation.ofReadBack` (:13-24).
- **State machines.** `state-machine-contracts.json`:
  - T-L01 (:3286): "read head bypassing caches; read files at that commit" (PERSIST-005).
  - T-L03 (:3357): Loaded, "record base head", "SM-SAVE initial Saved at that commit".
  - T-N02 (:3008): "record actual permissions; write never inferred from a successful read".
- **IF41-DOMAIN** (`01-IF41-DOMAIN.fs`:15-19). `Replay: EventEnvelope list -> Result<Investigation, ReplayError>` is the admissibility path. The event and command unions are frozen in `00-DOMAIN-STUBS.fs`:59-120.

---

## 5. Read-only versus writable

| Question | Finding | Evidence |
|---|---|---|
| Is causa-data publicly readable? | Yes. Anonymous `git clone` succeeded. | This session |
| Do the requirements allow anonymous browsing? | **No.** "Every catalog read and every write requires an authenticated GitHub credential; there is no anonymous mode (owner decision DEC-R7-001)." | PF-015-O1, baseline:2331 |
| Does browsing need write permission? | **No.** Reads use "least-privilege read-only permissions"; writes use a separate write token, and "neither secret falls back to the other". | PF-015-O2/O3, baseline:2336-; DEC-R7-007 |
| Where is read-only vs writable distinguished in the contracts? | **Only at connection level.** `ConnectAccepted of canWrite: bool` (04-IF41-ERRORS.fs:29) feeds `ConnectionState.Connected of canWrite: bool` (06-IF41-APP.fs:13) and `ConnectionInput.ConnectSucceeded of canWrite` (06-IF41-APP.fs:21). T-N02 records the actual permissions. | as cited |
| Is there a per-investigation or per-workspace read-only flag? | **None.** `LoadState`, `InvestigationSession`, `SaveState` and `Model` carry none (05-IF41-SAVE.fs:130-187; 06-IF41-APP.fs:68-75). No SM-SAVE or SM-LOAD guard mentions `canWrite`: grep finds 0 matches for `canWrite`/`readOnly` in `state-machine-contracts.json`, `analysis/state-machine-amendments.json` and `analysis/guard-partitions.json`. | grep result |
| Where do the requirements distinguish them? | UI-006-O6: "Frozen fixtures MUST remain read-only" (baseline:457). UI-014-O3: navigation "MUST distinguish frozen demonstrations from writable copies" (baseline:728). UI-001-O1: "the connected repository's investigations and the user's working copies" (baseline:246). ADRP-004-O6: "Historical and frozen-fixture views are read-only" (baseline:3894). ADRP-005-O2: "working copies use separate paths" (baseline:3917). The last two are gate-level, not in the scope manifest. | as cited |
| Where does causa-data distinguish them? | Writes go only to isolated `workspaces/demo/<runId>/` paths (README.md:92; CONTRACT.md:114-116, §9.4.4 :936-937). `Paths.workspace` calls that "the only writable namespace" (tools/src/Causa.GitHub/GitHub.fs:84-86). The catalog verifier requires `workspaces/demo/demonstration-v1/investigations` to hold **exactly** the four case ids (`demonstration.workspace-exact`, tools/py/demonstration_catalog.py:138, :154). New investigations written there would fail causa-data's own catalog verification. Rehearsal workspaces are created as copies by `workspace-create`/`rehearsal-init` (README.md:57-66; docs/HISTORICAL-WORKSPACE-TEMPLATES.md:63, :124). | as cited |

---

## 6. Mismatches between causa-data's real format and what the contracts or requirements assume

None of these is resolved below. Each is a fact from the cited lines. Resolving any of them needs an owner decision, because ADRP-005-O4 says "No agent may improvise alternative field names, ordering, event shapes, or snapshot semantics".

### Critical

**C1. Frozen event union versus the 31 wire event types.**
- **The stub.** `00-DOMAIN-STUBS.fs`:92-120 freezes 27 `CoreEvent` cases plus `ExtensionAttached` (corrected from 28 in review RVO-3-5). The case names are frozen (:11-13), and any added case is a breaking change (`interface-specifications.json` IF41-DOMAIN `versioning`).
- **The data.** The four catalog cases use wire events and commands that have **no stub case**:
  - `HypothesisTransitioned` with `command: markWeakened` (10). The stub has only `HypothesisMarkedPlausible`, `HypothesisMarkedSupported`, `HypothesisFalsified` and `HypothesisReopened`.
  - `LifecycleChanged` with `markAwaitingVerification` (3), `markAwaitingEvidence` (1) and `verificationFailed` (1). The stub has only `InvestigationActivated`, `InvestigationResolved` and `InvestigationReopened`.
  - `EvidenceVerificationAssessed` (382), `ContradictionTransitioned` (45), `UnknownTransitioned` (9), `AssumptionTransitioned` (3) and `ActionTransitioned` (9). The stub has none of these.
  - `CorrectiveActionTransitioned` to `approved`, `inProgress`, `awaitingVerificationOfEffect`, `verifiedEffective` and `verificationFailed`. The stub has only `CorrectiveActionImplemented`.
- **Absent from the stub altogether.** `EvidenceSuperseded`, `ProposalWasAccepted`, `NegativeKnowledgeEstablished` (6 events in the catalog) and `ConclusionRecorded` (4) have no case. The last two were added after review RVO-3-5; the stub has only an `EstablishNegativeKnowledge` command. (`CriterionRecorded` and `CausalAssertionReopened` only differ in name, from `VerificationCriterionRecorded` and `ConfirmedCauseReopened`, so they can be mapped.)
- **Consequence.** As frozen, `Replay: EventEnvelope list -> …` cannot represent the Apollo 1, Apollo 11 or Challenger histories. The only representable path would be `EventPayload.ExtensionEvent` (00-DOMAIN-STUBS.fs:124). But causa-data v1.0.0 "deliberately has no extension namespace" and refuses unknown data (CONTRACT.md:1070-1074, :232-233). EXT-DATA-013 cannot be met against this union without a versioned contract change.

**C2. Admissibility authority.**
- causa-data defines admissibility as "the pinned engine's `History.validate` returns no errors" (CONTRACT.md:998). That engine is a private repository (fetch-engine.sh:13-15).
- The app must use its own kernel (IF41-DOMAIN, T13), written from the requirements alone (OD-INDY-22, `docs/OWNER-DECISIONS-2026-11-03.md`:18).
- CT-006 confirms that the reference engine is "never an input to the event build" (contract-bindings.json CT-006 `dependencyVersions`).
- **Consequence.** The new kernel must re-decide 431-452-event histories exactly as the pinned engine does, including gates, verification freshness and prediction timing (CONTRACT.md:998-1004). Otherwise a catalog case is refused, or a forged history is accepted. Nothing in the contracts pins that equivalence, other than T15's fixture replay and the catalog's `expectedStateSha256`.

**C3. No workspace in the connection contract.**
- causa-data addresses data by `workspaces/demo/<runId>/…` (CONTRACT.md:102, :110). The active catalog is a workspace (`catalog/active.json`), and EXT-DATA-003 requires selecting "branch/workspace".
- The frozen `Repository = {Owner; Name; Branch}` has no workspace (03-IF41-WIRE.fs:45). `ConnectionInput.ConnectSubmitted of Repository * Credential.T` has none either (06-IF41-APP.fs:20), nor does `PathOf: InvestigationId -> RepoPath` (02-IF41-STORAGE.fs:29).
- The governed journey's Connect form fills only owner, repository, branch and token (`verification/journeys/support.mjs`:90-99). The owner chose those label patterns in ODR-R10-07 (bindings.json:4).
- **Consequence.** The workspace has to come from somewhere the contracts do not name.

**C4. One destination in the contract; two needed in the data.**
- The catalog workspace `demonstration-v1` must not gain investigations (demonstration_catalog.py:154). New investigations therefore need another writable workspace (README.md:92; GitHub.fs:84-86).
- The contracts hold one `ConnectionContext` (06-IF41-APP.fs:70) with one `canWrite`. There is no notion of a read-only catalog workspace beside a writable working-copy workspace, although UI-001-O1 and UI-014-O3 require both to be shown and told apart.
- The live acceptance repository is a different repository again: `kemiller2002/causa-acceptance-r10`, private, branch `causa-acceptance`, synthetic data only. Its single-repository App token has Contents read/write (`docs/rehearsal-010-readiness/LIVE-PROVIDER-PREFLIGHT.md`:22-24).
- These sources do not establish whether that token can read causa-data.
- One App token with `contents: write` reaches the app as `CAUSA_GITHUB_TOKEN` (LIVE-PROVIDER-PREFLIGHT.md:24, :33). That differs from PF-015-O2/O3, which call for a read-only `CAUSA_GITHUB_TOKEN` and a separate `CAUSA_WRITE_TOKEN`.

**C5. Catalog content fields with no v1 wire form.**
INV and journey content has no v1 wire form. Wire objects are closed and have no optional keys or extensions (CONTRACT.md:55-58, :1070-1074).

| App requirement (catalog) | Wire reality | Journey that checks it |
|---|---|---|
| INV-023 "Primary investigative question" (catalog:633) | No field. `InvestigationStarted` = `title`, `observationId`, `original` only (CONTRACT.md:461-469) | J1-03, J1-05 (`providerHolds([... v.question])`), J1-06 (`j1-create-persist.spec.mjs`:30, :36) |
| INV-025 "Observation source reference" | No field. `ObservationRecorded` = `observationId`, `original`, `observedAt` (:495-503) | J2-01 (`j2-analysis.spec.mjs`:36) |
| INV-027 "Evidence label" | No field. `EvidenceAdmitted` = `contentKind`, `content`, `category`, `source`, `capturedAt` (:379-392) | J2-03, J2-05 (`j2-analysis.spec.mjs`:48, :53, :62) |
| INV-031 options `supports`, `contradicts`, `consistent-with`, `inconclusive` (catalog INV-031; OD-INDY-23, `docs/OWNER-DECISIONS-2026-11-03.md`:30) | `relationshipKind` = `supports`, `contradicts`, `discriminates`, `insufficient` (CONTRACT.md:197, :403). `consistent-with` and `inconclusive` have no wire value. Whether `insufficient` means `inconclusive` is not stated anywhere. | J2-06 reassesses to `inconclusive` and reloads it in J2-09 (`j2-analysis.spec.mjs`:73-77) |
| INV-030/INV-035 "At most one current assessment per evidence–hypothesis pair; reassessing or withdrawing keeps the earlier assessment" (catalog notes) | No relationship revision or withdrawal event. `EvidenceRelated` only creates (`new relationship`, CONTRACT.md:618). No "current" marker exists. The data never relates a pair twice, so nothing shows how the engine treats a second link. | J2-06, J2-09 |
| INV-034 "Edit investigation details" (title, problem statement, question) | No rename event: "`InvestigationRenamed` … not registered: the engine has no such commands" (CONTRACT.md:1153) | (catalog check) |
| INV-035 "An edited observation, hypothesis, evidence item … keeps its identifier" | No edit events for observations or hypotheses. Evidence correction is `EvidenceSuperseded` to a **new** evidence id (CONTRACT.md:406-414, :616) | J2-06, J2-09 |
| INV-002 Title `required: false` (catalog:55) | `title` is non-blank and required (CONTRACT.md:467). The engine falls back to the original text (FIELD-MAPPING.md:30). Not a conflict, provided the app's kernel does the same. | J1-03 |

The proposed stubs that would have typed this content (ODR-V413 amendment) were withdrawn by OD-INDY-19 (`docs/OWNER-DECISIONS-2026-11-03.md`:15). The frozen v4.1.4 stubs keep these payloads opaque (00-DOMAIN-STUBS.fs:11-13). No storage contract 1.x.0 exists in causa-data (`contract/` holds only `v1.0.0`).

### Significant

**S1. No listing operation.**
- No frozen contract lists investigations. `Msg`, `Model`, `LoadInput`, `ConnectResult` and `Connector` have no list or catalog case (06-IF41-APP.fs:57-75; 05-IF41-SAVE.fs:139-146; 04-IF41-ERRORS.fs:28-36).
- `Model.Loads` is keyed by an `InvestigationId` that must already be known (06-IF41-APP.fs:74). `docs/PERSISTENCE-CONTRACT.md`:135 names a `ListInvestigations(workspace/query)` port, but it is not in the frozen set.
- causa-data has no workspace index. The four ids are discoverable only from the catalog manifest or a tree read.

**S2. No creation path in the save contract.**
- `SaveMachine.Initial` needs a `VerifiedInvestigation` with `ReadAt` and `Bytes` (05-IF41-SAVE.fs:211; 02-IF41-STORAGE.fs:22-26). `Encode` needs a prior `VerifiedInvestigation` (02-IF41-STORAGE.fs:30).
- A new investigation has neither. No SM-SAVE or SM-LOAD transition covers creation: grep for "creat"/"StartInvestigation" in `state-machine-contracts.json` finds only `createCommitOnBranch`.

**S3. How `canWrite` is derived conflicts between v4 and v4.1.4.**
- v4 CT-017: "never infer authority from advisory fields such as permissions.push (R2 DC-004)" (`requirements-compiled/v4/contract-bindings.json`:814).
- Frozen v4.1.4: "write access comes only from the permissions GitHub states for the repository" (04-IF41-ERRORS.fs:26-27).

**S4. Write primitive.** causa-data §9.4.1 says a non-forced `PATCH /git/refs`, and "sequential per-file Contents-API writes do not conform" (CONTRACT.md:926-928). PERSIST-006 uses GraphQL `createCommitOnBranch`. That is a recorded owner exception (CT-007, OD-INDY-9), not a defect, but a reviewer should know it departs from §9.4.1's text.

**S5. Stale task completion text.** The v4 T25 completion still says "three cases load from kemiller2002/causa-data workspace historical-v1" (`requirements-compiled/v4/dependency-graph.json`:925). The active catalog is `demonstration-v1` with four cases (catalog/active.json:1; EXT-DATA-013; CT-008).

**S6. Error vocabulary.** causa-data readers report 17 ordered `CAUSA.*` codes, and conforming readers must agree on the primary code (CONTRACT.md:961-982). The frozen `VerificationFailure` has six cases with no stage code (02-IF41-STORAGE.fs:14-20). EXT-DATA-005 asks only for "safe diagnostics", so this is a fidelity gap, not a requirement breach.

**S7. Timestamps and actor ids.**
- Writers must refuse sub-millisecond instants (CONTRACT.md:73-79). `RequestContext.Now` is a `DateTimeOffset` (03-IF41-WIRE.fs:53-59), so the command boundary must truncate.
- `actorId` must be "stable, non-secret, non-personal" (CONTRACT.md:157-159). The contracts' `ActorContext = {ActorId: string}` (00-DOMAIN-STUBS.fs:135) does not say where it comes from.

**S8. Journey J1-02 checks a create affordance.**
- J1-02 is bound to EXT-DATA-003 and UI-001 (bindings.json:12). It asserts that a "New investigation" control is visible after Connect (`j1-create-persist.spec.mjs`:14-17), not the list of investigations.
- Against a read-only connection, whether that control should appear is undecided by the contracts. `canWrite` exists only at connection level.
