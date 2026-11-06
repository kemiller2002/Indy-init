# Historical compatibility

Every figure here is **observed**: the validator computes it (`dotnet run --project tools/contract-readiness -- check --causa-data <causa-data>`), and it is recorded in `CONTRACT-READINESS.json` (CR-HIST-*, CR-REP-*) and `HISTORICAL-DATA-INVENTORY.json`. Pinned data: causa-data `e8259df74add723765c57a5b1a5888d9c3f707af`.

## 1. What was examined

| Scope | Histories | Events |
|---|---|---|
| Historical cases (`workspaces/demo/historical-v1`: Apollo 1 432, Apollo 11 431, Challenger STS-51-L 452) | 3 | **1,315** |
| Active catalog `demonstration-v1` (the three historical cases + the burned-out lightbulb, 48), the catalog EXT-DATA-013 names | 4 | **1,363** |
| Every distinct history in the repository (also 5 valid fixtures, 184 events, and 7 rehearsal working copies, 36 events) | 16 | **1,583** |
| Every investigation directory, counting identical copies (templates, two catalogs, `everyday-v1`) | 28 | 5,672 |

The historical cases use 26 of the 31 registered event types (37 command/state variants). All 31 types occur somewhere in the repository; `ProposalRecorded`, `ProposalWasAccepted`, `ProposalWasRejected` and others occur only in fixtures.

## 2. How "represented" is decided

Being accepted by a permissive decoder is not counted. For an event to count as lossless under a contract version, all of the following must hold:

1. **Strict decode against the registry** (CR-HIST-01). Every key must be a registered field of its object type, with no unknown or missing key. Every value must have its registered kind: canonical lowercase non-nil UUID, a calendar-valid millisecond UTC timestamp, non-blank text over the §2.6 whitespace set, a closed enum value, or an integer in range. Every schema rule must hold (for example, `command = markSupported` requires non-empty `evidenceIds`, and `actor.kind = agent` requires `eventType = ProposalRecorded`).
2. **Byte-for-byte re-encode from typed values.** UUIDs are re-rendered from `Guid` and timestamps from `DateTime`. The RFC 8785 text must equal the stored line.
3. **Exactly one wire variant** (CR-HIST-02). A variant is the event type, refined by `command` or `state` where the domain union splits it.
4. **A case in the contract's parsed `CoreEvent`** whose arguments carry every payload field (CR-REP-*). A sole opaque payload argument carries the whole record. An id-typed argument carries the entity id. A labelled argument carries the field of that name. An unlabelled `NonEmptyText` carries `reason`. Anything else, such as v4.1.4's `Scope`, has no wire source, and is counted as a defect.
5. **An `EventEnvelope` record whose fields carry every envelope field.** This is checked against the parsed F# record through `envelopeBinding`.

Further checks over the whole history:

- Envelope integrity: contiguous versions, the UUIDv5 `eventId` derivation recomputed, a correlation id never reused across groups, and `InvestigationStarted` first (CR-HIST-03).
- Manifests and snapshots decode and round-trip, and each manifest pins the events bytes (CR-HIST-04).
- Catalog digests and expected versions match (CR-HIST-05).
- Every state transition is legal under `docs/STATE-TRANSITION-TABLES.md`, with the stored `from` equal to the replayed state (CR-HIST-06: 273 transitions).
- Historical provenance records correspond one to one with admitted evidence, and each source text carries its URL (CR-HIST-07: 376 records).

## 3. Results

| Measure | Frozen v4.1.4, historical cases | Candidate v4.1.5, historical cases | Candidate v4.1.5, active catalog | Candidate v4.1.5, all distinct histories |
|---|---|---|---|---|
| Events examined | 1,315 | 1,315 | 1,363 | 1,583 |
| Represented losslessly (payload and envelope) | **0** | **1,315** | **1,363** | **1,583** |
| No `CoreEvent` case | **487** | 0 | 0 | 0 |
| Case drops payload fields | **63** | 0 | 0 | 0 |
| Envelope fields lost | `formatVersion`, `occurredAt`, `actor`, `semanticVersion`, `correlationId`, from **all 1,315** | none | none | none |
| Events requiring migration | — | 0 | 0 | 0 |
| Events still unsupported | — | 0 | 0 | 0 |
| Fields lost by round-trip serialization | — | 0 | 0 | 0 |
| Unknown values found (preserved in the problem record, never dropped) | — | 0 | 0 | 0 |

**PR #141 is reproduced exactly** (CR-REP-02). Against frozen v4.1.4:

- **487 events have no case:**
  - `EvidenceVerificationAssessed` ×376;
  - `ContradictionTransitioned` ×45;
  - `UnknownTransitioned` ×9;
  - `ActionTransitioned` ×9;
  - `CorrectiveActionTransitioned` non-implemented states ×25;
  - `HypothesisTransitioned/markWeakened` ×7;
  - `NegativeKnowledgeEstablished` ×6;
  - `ConclusionRecorded` ×3;
  - `AssumptionTransitioned` ×3;
  - lifecycle `markAwaitingVerification` ×2, `markAwaitingEvidence` ×1 and `verificationFailed` ×1.
- **63 events are lossy:**
  - `markPlausible` ×29 and `markSupported` ×22 lose `from`, `reason` and `evidenceIds`;
  - `falsify` ×6 loses `from` and `evidenceIds`, and needs an invented `Scope`;
  - `activateInvestigation` ×3 loses `from`;
  - `CausalAssertionConfirmed` ×3 loses `policy`.
- **Every event loses its provenance.**

**Does the successor resolve it?**
- **E1 resolves every unsupported event.** v4.1.5 represents 1,315/1,315 historical events, 1,363/1,363 catalog events and 1,583/1,583 stored events losslessly.
- **W1 resolves the missing workspace representation.** CR-ST-02 checks the `Workspace`, `WorkspaceAddress` and `InvestigationRef` types, plus `ListingTarget` and the keyed state in IF41-APP.

## 4. Remaining incompatibilities: none in the data; three in the contracts

No stored event is incompatible. Three contract-level issues remain:

1. **`ExtensionAttached` / `AttachExtension`** (v4.1.5) has no causa-storage v1.0.0 form, because v1.0.0 has no extension namespace (CONTRACT §13.5). The registry marks it `no-wire-form`, and the command boundary must refuse it. It never occurs in data.
2. **Written content the v1.0.0 wire cannot hold.** This concerns the primary question, observation source, evidence label and URL, the new relationship kinds, and revisions. ODR-R10-26 A commissions causa-storage 1.1.0 for it. The proposed content is `DOMAIN-CONTRACT.json` `proposedStorageExtension` (OD-CR-01). It is purely additive, so the historical results above are unaffected (CR-DOM-02).
3. **Admissibility** (CONTRACT §11.3) is still a kernel obligation (OD-HC-03 A), and this contract validator does not claim it. Of the 90 causa-data negative fixtures, 32 are refused by registry-level decoding alone. The rest need the later reader stages or the admissibility kernel (`HISTORICAL-DATA-INVENTORY.json` `negativeFixtures`).

## 5. Data consistency findings

The historical data is internally consistent at every level checked:

- no integrity, canonical-form, transition, provenance or digest problem was found;
- every copy of a case is byte-identical across `historical-v1`, `demonstration-v1` and both template trees;
- `occurredAt` is non-decreasing in every history (CONTRACT §5.6 does not require this);
- 9 historical relationships use `insufficient`, which has no INV-031 option. They are displayed verbatim and read-only (OD-HC-07 B).
