# Causa storage contract v1.0.0 — conflict resolution record

> **Normative status.** **Explanatory.** This record summarizes how Indy-init planning documents were reconciled with the frozen Causa storage contract v1.0.0. It defines nothing. Persisted data is governed only by the imported contract ([README.md](../requirements/causa/data/v1.0.0/README.md), [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md), [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md)).

Full finding-by-finding report (344 findings, REC-001…REC-344, with file:line, frozen reference, category, classification and resolution):
`kemiller2002/research-documents` → `research/experiments/CAUSA-REQUIREMENTS-CONSOLIDATION-001/RECONCILIATION.md`.
Earlier engine-level decisions (CD-*): `research/experiments/EX-CAUSA-STORAGE-2026-001/COMPATIBILITY-DECISIONS.md`.

## Rule

The frozen contract (tag `causa-storage-v1.0.0`, imported byte-for-byte at `requirements/causa/data/v1.0.0/`) is authoritative. No conflict was resolved by changing it. Candidate statements that define a persisted shape differently were corrected or marked in place with *Superseded by frozen contract v1.0.0*, keeping the original reasoning visible. Candidate requirements the contract does not address (entities, events or transitions it does not have) were preserved and marked *deferred*: adding them is a versioned change ([compatibility/RULES.md](../requirements/causa/data/v1.0.0/compatibility/RULES.md)).

## Totals (from the full report)

| classification | count |
|---|---:|
| CONFLICT-superseded | 162 |
| CONSISTENT | 119 |
| CANDIDATE-ONLY-preserve (deferred) | 60 |
| NON-STORAGE-rationale | 3 |

## Conflict classes and their resolution

| class | conflicts | what the candidates said | resolution (frozen v1.0.0) | where annotated |
|---|---:|---|---|---|
| Event discriminators and payloads | 45 | `HypothesisProposed`, `HypothesisStateChanged`, `EvidenceVerificationChanged`, `ContradictionDetected`, `Investigation{Activated,Resolved,Reopened,…}`, `CauseConfirmed`, `ProposalAccepted`, …; open SemanticId event types | Closed registry of the engine's 31 events; lifecycle = `LifecycleChanged{from,to,command,reason}`; hypothesis = `HypothesisTransitioned{…,command,…}`; other entities = `*Transitioned{state[,basis]}`; unregistered type fails the whole history (CONTRACT §7) | CAUSA-DATA-CONTRACT-v1 §7 (wire column), COMMAND-EVENT-CATALOG §0 and per table, FSHARP-TYPE-SHAPES §33, INVESTIGATION-SEMANTICS §23, test/scenario docs |
| Field names and JSON types | 37 | envelope `recordedAt`, `policyRefs`, `provenance`, semantic manifest, per-event integrity; `actor.displayName`; NFC strings; sorted id "sets"; typed artifact refs; entity Scope/Extensions/EntityRef fields | Envelope has exactly 10 fields; entity provenance derived `{actor, recordedAt}`; text verbatim; id arrays in citation order; artifact = http(s) URL in `content`; entity shapes per `schemas/entities/` | CAUSA-DATA-CONTRACT-v1 §2/§5, PERSISTENCE-CONTRACT §5/§17, FSHARP-TYPE-SHAPES §5–§34, CORE-DOMAIN-MODEL |
| Enum values | 21 | hypothesis `Confirmed`; action `Blocked`; proposal `Revised/Deferred/Stale` and 12 kinds; 10 relationship kinds; 11 action kinds; `DerivedCalculation`; actor `external/imported`; corrective `AwaitingVerification`; extension cases | Closed lowerCamelCase sets of CONTRACT §6 (e.g. relationship `supports|contradicts|discriminates|insufficient`, action kind `test|inspection|query|comparison|experiment`, `awaitingVerificationOfEffect`); extra values deferred | COMMAND-EVENT-CATALOG §7/§10/§12/§18/§19, FSHARP-TYPE-SHAPES, STATE-TRANSITION-TABLES, INVESTIGATION-SEMANTICS, DOMAIN-TEST-MATRIX |
| Manifest, snapshot and file layout | 18 | `investigations/<id>/{current.json, extensions/, …}`; ReversibleChoice layout; events-only or snapshot-only profiles; free `generatedAt`; `domainPacks` strings; semantic manifest in state | Exactly `manifest.json`, `events.ndjson`, `snapshot.json` under `workspaces/demo/<runId>/investigations/<id>/`; snapshot always present and byte-identical to replay; `generatedAt` = last `occurredAt`; `domainPacks` = `[]` (CONTRACT §3, §8, §9.1) | CAUSA-DATA-CONTRACT-v1 §1/§4/§6, PERSISTENCE-CONTRACT §2/§7/§8/§11, ARCHITECTURE, OPEN-QUESTIONS OQ-006, DESIGN-DECISION-BOUNDARIES D-202 |
| Migration and compatibility | 15 | extension passthrough; unknown fields preserved or read-only; superseded-candidate status lines | Closed structures, no extension namespace; v1.0.0 readers refuse what they cannot interpret; changes only via new contract versions (CONTRACT §2.4, §13) | PERSISTENCE-CONTRACT §4/§30/§47, FSHARP-TYPE-SHAPES §44/§45, CORE-DOMAIN-MODEL §28, DOMAIN-TEST-MATRIX §24, kickoff |
| Concurrency and replay | 10 | blob SHA as token; atomic commit "may"; separate StoreSnapshot; discard stale snapshot; "validate enough structure"; destructive restore; sync states without *sending*/*unknown* | Token = branch head commit; one commit per command via non-forced ref update; full 17-stage check order incl. admissibility; fail closed, never repair; history never rewritten; pending/sending/conflicted/unknown distinct (CONTRACT §9, §11) | PERSISTENCE-CONTRACT §9/§12/§13/§20/§33/§43/§44, ARCHITECTURE, SECURITY-THREAT-MODEL §23 |
| Identifier formats | 5 | `eventId` "globally unique" / assigned by persistence; optional or string correlation ids; nil UUID allowed | `eventId` = UUIDv5 over `investigationId/aggregateVersion/correlationId`; `correlationId` = command id, one group each; nil UUID invalid (CONTRACT §2.8, §5.2, §5.4) | CAUSA-DATA-CONTRACT-v1 §2/§5, FSHARP-TYPE-SHAPES §32/§36, POLICY-ENGINE |
| Hash calculation | 4 | RFC 8785 used for hashing only; per-event digests / hash chaining in events; snapshot integrity field | Files must be canonical bytes + LF; JSON digests exclude the trailing LF; integrity per file via manifest digests and the byte-prefix successor rule (CONTRACT §2.2, §2.11, §9.3, §10) | CAUSA-DATA-CONTRACT-v1 §2, PERSISTENCE-CONTRACT §5/§7/§16 |
| Event ordering | 3 | separate complete-action and verified/verification-failed commands | Completion follows `FindingRecorded`, and corrective verification outcomes follow `VerificationRecorded`, in the same command group (SEMANTICS.md) | COMMAND-EVENT-CATALOG §19, STATE-TRANSITION-TABLES §5/§11 |
| Required vs optional | 2 | "optional fields are omitted"; optional `CorrelationId` | No optional keys; absent option = required `null` (CONTRACT §2.5) | CAUSA-DATA-CONTRACT-v1 §2, PERSISTENCE-CONTRACT §5 |
| Null semantics | 2 | `reason` required on every lifecycle event; `causationId` nullable | `reason` null except markAwaitingEvidence, reopenInvestigation, verificationFailed; `causationId` removed | CAUSA-DATA-CONTRACT-v1 §5/§7 |

## Not resolved here (open owner decisions)

- **U-01 organizer permission** for a pre-created dataset and app-specific schemas (FROZEN-DEMO-STORAGE-REQUIREMENTS STORAGE-008): preserved, open.
- **U-02 repository visibility**: STORAGE-004 says private; `causa-data` is public. Preserved, owner to confirm.
- **U-03 catalog first-slice commands** `AttachExtension` and `PinPolicyVersion` have no engine command or v1.0.0 event; marked deferred (would need contract v1.1.0).
- Deferred domain vocabulary (claims, interpretations, obligations, reviews, reports, extensions, the 75 unregistered catalog events, and states such as `Blocked`, proposal `Revised/Deferred/Stale`, the 7 extra relationship kinds) remains research or future scope. It is not a conflict as long as nothing writes it into a v1.0.0 workspace.
