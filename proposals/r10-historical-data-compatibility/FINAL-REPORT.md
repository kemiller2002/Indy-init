# Final report: Causa historical data contract compatibility

## Answer to the final question

**What is the smallest verified change that lets Causa correctly load and interpret its actual historical data?**

The smallest verified change is a **provider-neutral, read-only decoding and projection layer that implements causa-storage v1.0.0 exactly as written**. Its functions are `StorageReader.read` and `HistoricalLoader.verify` (`src/`, about 2,600 lines of F# including the proposed successor union). It uses the storage contract's own 31-event registry and its normative §8.3 fold. It does not use the frozen v4.1.4 `CoreEvent`. A small proposed successor contract carries the decoded events into the domain kernel's types without loss (`SUCCESSOR-CONTRACT.md`):

- keep all 28 frozen case names;
- widen 9 of them;
- add 22 cases;
- add `Provenance` to `EventEnvelope`;
- pin the payload records to the wire tables.

**Verified against the actual data** (causa-data `e8259df7`, 20/20 checks, `HISTORICAL-COVERAGE.json`):

- **Every event is accounted for.** Apollo 1 has 432 events, Apollo 11 has 431 and Challenger has 452, which confirms the reported counts. All 1,315 decode into typed events with no catch-all case. Every one re-encodes to its stored line byte for byte, both directly and through the proposed successor union (C02, C11).
- **Replay matches the engine.** For all three cases, the replay reproduces the engine-written `snapshot.json` byte for byte, and its state digest equals the catalog's (C12). The same holds for the full four-case active catalog that EXT-DATA-013 names (C16).
- **Malformed data is caught.** Of the causa-data negative fixtures (C07):
  - all 55 that do not need the engine are refused with the contract's primary code;
  - 3 of the 35 that need the engine are refused by the adapter's own checks;
  - the other 32 fixtures (31 engine-only plus one append-side fixture) are never reported valid.
- **The historical data needs no correction.** It is consistent, version-uniform and byte-identical across all four copies (C13; `SCHEMA-DIFF.md` §4).

**What prevents integration before the competition?** Three things, in order of severity:

1. **The frozen contract cannot hold the data, and only the owner can change it.**
   - v4.1.4 `CoreEvent` has no case for 487 of the 1,315 events: `EvidenceVerificationAssessed` ×376, `ContradictionTransitioned` ×45, and others.
   - Its case arguments drop fields from 63 more.
   - Its `EventEnvelope` drops `occurredAt`, `actor` and `correlationId` from all 1,315 events.
   - Changing it requires a successor package: compiled with X01–X21, independently reviewed and owner-frozen. The optimization agent already judged that "its freeze cycle … cannot complete before a launch this morning" (its ODR-R10-20). Decision: **OD-HC-01, OD-HC-02.**
2. **Admissibility has no requirements-side implementation.** CONTRACT §11.3 and EXT-DATA-005-O1 require history admissibility. Its only implementation is the private reference engine, which OD-INDY-22 forbids a rehearsal to copy. 31 of the 35 engine-refused fixtures pass every check this adapter can run. Until a requirements-derived kernel refuses them, no reader may call a history "valid". Decision: **OD-HC-03, OD-HC-04.**
3. **The adapter cannot be copied into a rehearsal.** OD-INDY-22 forbids copying code from earlier work. It must become a released package, or a frozen verification asset that the rehearsal implements against, or remain a reference. Decision: **OD-HC-05.**

None of these is a technical unknown. The decoder exists and is verified. What remains are governance steps and one body of domain work, the admissibility kernel, which the rehearsal already has to build (T13/T14).

## Timebox report (mission §12)

| Question | Answer |
|---|---|
| Event types found | **26** distinct discriminators in the historical data. The registry has **31**, and the other 5 appear only in the synthetic fixtures. There are 37 command and state variants in the history, and 49 that the registry can produce. |
| Currently supported by the frozen v4.1.4 union | **15 of 26** discriminators have a frozen case whose arguments drop no field: 765 events. **0 of 1,315** events keep their provenance, because the frozen envelope drops it. |
| Requiring a successor contract | **11 of 26** historical discriminators (14 of 31 overall): 487 events with no case and 63 with a lossy case |
| Full typed decoding achievable before the competition? | **Already achieved** (Level B, this branch). Whether it can be *used* before the competition depends on OD-HC-05. |
| Authoritative replay achievable? | **Projection: achieved** (Level C-projection). **Authoritative: not before the admissibility kernel exists** (OD-HC-03). The adapter provides the acceptance tests (C07, C12, C16). |
| Estimated effort | See the table below. |
| Owner decisions | OD-HC-01 … OD-HC-09 (`OWNER-DECISIONS-REQUIRED.md`) |

Effort estimates are rough. They are extrapolated from this mission and from the freeze steps in `requirements-compiled/v4.1.4/FREEZE.md`. They have not been measured.

| Work | Who | Estimate |
|---|---|---|
| Compile the successor package from `SUCCESSOR-CONTRACT.md`, run X01–X21, update the contract typecheck and negative controls | agent | 0.5–1 day, plus review rounds |
| Independent review, owner approval and tag | reviewer, owner | owner-paced; it cannot finish before a same-morning launch |
| Requirements-derived admissibility, so the 35 fixtures are refused with their domain codes | domain implementer | 1–3 agent-days; this is the bulk of T13/T14's decide logic |
| Wire the adapter to a GitHub `DocumentSource` | integration | about 0.5 day |
| Route R2: freeze the checks as verification assets | agent, owner | about 0.5 day plus a freeze |

## Failure analysis (mission §4)

| # | Hypothesis | Confirmed? | Evidence | Kind |
|---|---|---|---|---|
| 1 | Missing F# union cases | **Yes.** 16 historical variants of 10 discriminators have no case: 487 events. Another 5 variants have only a lossy case. | C14; `SCHEMA-DIFF.md` §2; `00-DOMAIN-STUBS.fs:92-120` | domain representation (replay) |
| 2 | Incorrect discriminator names | **Yes, but mappable.** 6 renamed cases (`EvidenceRelated`→`EvidenceRelationshipRecorded`, `ActionRecorded`→`InvestigationActionRecorded`, `CausalAssertionConfirmed`→`CauseConfirmed`, `CausalAssertionReopened`→`ConfirmedCauseReopened`, `CriterionRecorded`→`VerificationCriterionRecorded`, `ProposalWasRejected`→`ProposalRejected`), and 3 wire types split by command or state. | `EventCompatibility.table` | serialization; fixed by the decoder |
| 3 | Incorrect field names or types | **Yes.** Case arguments drop `reason`, `evidenceIds`, `from`, `policy` and the rejection `reason` (63 events). The indicative payload shapes need `Scope`, which has no wire source, and lack `source`/`capturedAt`. | `SCHEMA-DIFF.md` §2–§3 | domain representation |
| 4 | Version mismatches | **No.** Every file is 1.0.0, and CT-006 binds 1.0.0. | `SCHEMA-DIFF.md` §4 | — |
| 5 | Unsupported envelopes | **Yes.** `occurredAt`, `actor`, `correlationId` and both versions are lost for all 1,315 events. | `SCHEMA-DIFF.md` §1; `00-DOMAIN-STUBS.fs:126` | serialization and provenance |
| 6 | Event-ordering assumptions | **No failure, one hazard.** `aggregateVersion` is contiguous, but `occurredAt` is not unique. | C04; `SCHEMA-DIFF.md` §4 | — |
| 7 | Missing relationship representations | **Not for reading.** All 186 relationships decode and replay. **Yes for the UI contract:** `insufficient` (9) has no INV-031 option, `consistent-with`/`inconclusive` have no wire value, and there is no revise or withdraw event. | `SCHEMA-DIFF.md` §6 | UI contract and domain |
| 8 | Incorrect storage layout assumptions | **Yes, at contract level.** There is no workspace in `Repository` or `PathOf`, and no list operation. The adapter works around it by making the workspace a `DocumentSource` argument. | `SCHEMA-DIFF.md` §5; `03-IF41-WIRE.fs:45` | persistence/wire contract (owned elsewhere, ODR-R10-25) |
| 9 | Published model vs. frozen Indy-init contract | **Yes. This is the root cause.** The frozen union follows `FSHARP-TYPE-SHAPES.md` §33. causa-storage v1.0.0 follows the reference engine and records the reconciliation in its §16. Indy-init never adopted that reconciliation. `kemiller2002/causa` itself was not accessible (OD-HC-09), so this is verified against causa-data, whose contract names the engine as its domain authority. | `SCHEMA-DIFF.md` §0 | contract governance |

**Serialization versus replay.** No serialization failure exists in the data. Every line parses, is canonical and decodes strictly (Level B). Every failure above is a failure of *representation* in the frozen domain contract, or a missing *admissibility* implementation. A decoder that accepts everything was explicitly not taken as proof of replay: replay is checked against independent engine snapshots and catalog digests, and Level C is withheld.

## Related data-model gaps (mission §9)

See `SCHEMA-DIFF.md` §6 for the full table with consequences.

| Gap | Requirement IDs | Classification |
|---|---|---|
| Primary question | INV-023 | serialization, domain and persistence command |
| Observation source | INV-025 | serialization, domain and persistence command |
| Evidence label | INV-027 | serialization and UI contract |
| Evidence source URL | INV-028, INV-029 | UI contract |
| Relationship kinds | INV-031, OD-INDY-23 | serialization, domain and UI contract |
| Relationship rationale | INV-038 | writer-side serialization |
| Relationship revise/withdraw | INV-030, INV-035 | domain and persistence command |
| Investigation rename/edit | INV-034, INV-035 | domain and persistence command |
| Title optional | INV-002 | none |

None blocks loading the historical cases. All of them block write or round-trip journeys (J1-03, J2-01, J2-03, J2-04, J2-05, J2-06, J2-09). They are recorded for the UI-contract agent in `INTEGRATION-HANDOFF.md` §5, and the owner choice is OD-HC-06 and OD-HC-07.

## Deliverables

| # | File |
|---|---|
| 1 | `README.md` |
| 2 | `EVENT-COMPATIBILITY-MATRIX.json`, generated from the data |
| 3 | `SCHEMA-DIFF.md` |
| 4 | `SUCCESSOR-CONTRACT.md` and `src/SuccessorContract.fs` |
| 5 | F# adapter: `src/` (net10.0, no packages, warnings as errors) |
| 6 | F# tests: `tests/`, 20 checks |
| 7 | `HISTORICAL-COVERAGE.json`, generated |
| 8 | `REPLAY-VERIFICATION.md` |
| 9 | `INTEGRATION-HANDOFF.md` |
| 10 | `ADVERSARIAL-REVIEW.md` |
| 11 | `OWNER-DECISIONS-REQUIRED.md` |
| 12 | `FINAL-REPORT.md` |

**Process notes**

- **Branch:** the work is on `claude/causa-historical-compatibility-09j9rw`, as this session's platform instructions require, rather than on the brief's `claude/r10-historical-data-compatibility` (OD-HC-08).
- **Scope of changes:** nothing outside `proposals/r10-historical-data-compatibility/` changed. Rehearsal 010, its T0 clock, frozen packages, locks and other agents' files were not touched.
- **No PR in causa:** no change to `kemiller2002/causa` was needed or possible (no access), so no causa PR exists.
- **Nothing merged:** nothing was merged.
