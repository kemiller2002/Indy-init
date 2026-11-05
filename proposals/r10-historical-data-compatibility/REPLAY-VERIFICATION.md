# Replay verification

This document covers what replaying the three historical investigations establishes, what it does not, and the exact evidence still missing. Decoding (Level B) and replay (Level C) are verified by separate code and separate oracles. A decoder that accepts every event does not by that fact establish replay.

## 1. What runs

| Step | Code | Specification |
|---|---|---|
| Decode | `src/StorageReader.fs` `read`: stages 1–14, typed by `src/EventDecoder.fs` | CONTRACT §2, §5, §7, §11.1 |
| Fold | `src/ReplayAdapter.fs` `replay`: the normative fold, one function per row of the §8.3 table, applied in `aggregateVersion` order | CONTRACT §8.3 |
| Project | `ReplayAdapter.stateJson` and `snapshotJson`: the §8.2 state object and the §8.1 snapshot document, canonicalized by RFC 8785 | CONTRACT §8.1, §8.2, §8.4 |
| Compare | `src/HistoricalLoader.fs` `verify`: stage 16 (`snapshotSha256`) and stage 17 (projection = `snapshot.json` bytes) | CONTRACT §11.1 |

The fold was written from the CONTRACT §8.3 text. It shares no code with the producer: causa-data's builder issued commands to the private reference engine (`Decide.execute`; causa-data README "Construction and validation"), and its projection is `tools/src/Causa.Contract/Projection.fs`, which compiles against that engine.

## 2. Oracles, all independent of this adapter

| Oracle | Produced by | Used in |
|---|---|---|
| `snapshot.json` of each case | causa-data `historical-build` (engine pipeline) | stage 17, byte equality |
| `catalog/historical-v1/manifest.json` `expectedStateSha256`, `expectedLifecycle`, `expectedVersion` | causa-data catalog builder | C12 |
| `catalog/demonstration-v1/manifest.json` (four cases, including the lightbulb) | causa-data catalog builder | C16 |
| `contract/v1.0.0/golden/<scenario>.state.json` and the root fixture manifest `expectedStateSha256` | the frozen storage release | C12, for the five types the history never uses |
| `fixtures/v1.0.0/negative/*.json` (90 fixtures with expected codes) | the frozen storage release | C07 |

## 3. Results (`HISTORICAL-COVERAGE.json`)

| Case | Events | Projection = `snapshot.json` | State SHA-256 = catalog | Lifecycle | Replay anomalies |
|---|---|---|---|---|---|
| Apollo 1 | 432 | byte-identical | `67af9c9f…` = `67af9c9f…` | `awaitingVerification` | 0 |
| Apollo 11 | 431 | byte-identical | `b82ac9ce…` = `b82ac9ce…` | `awaitingEvidence` | 0 |
| Challenger STS-51-L | 452 | byte-identical | `296bdf27…` = `296bdf27…` | `active` | 0 |
| Burned-out lightbulb (`demonstration-v1`) | 48 | byte-identical | equals catalog | `resolved` | 0 |
| 5 frozen synthetic fixtures | 26–59 | byte-identical, and equal to the golden state file | equals root manifest | as manifest | 0 |

The other checks:

- **Determinism (C09):** two independent reads from disk give identical decoded-line digests and state digests.
- **Tamper detection (C17), on real Apollo 1 bytes:**

  | Mutation | Detected as |
  |---|---|
  | edit an event | `EVENTS_HASH_MISMATCH` |
  | edit an event and recompute the manifest digests | `SNAPSHOT_MISMATCH` (replay catches what hashing no longer can) |
  | swap two events | `SEQUENCE_INVALID` |
  | drop the last event and recompute | `SNAPSHOT_MISMATCH` |

- **Anomaly checks.** These are necessary conditions, not admissibility. They report and never "repair":
  - an update to a missing entity;
  - a duplicate entity id;
  - a dangling reference;
  - a transition `from` that differs from the replayed state;
  - a repeated or missing `InvestigationStarted`.

  They catch 3 of the 35 inadmissible negative fixtures: `engine-dangling-reference`, `engine-duplicate-entity-id` and `missing-start`.

## 4. What this establishes

**Level C-projection: achieved for all three historical cases and the full four-case catalog.** The adapter rebuilds, from the events alone, exactly the investigation state the authoritative engine recorded. That covers:

- every entity, state, history entry and provenance actor;
- `recordedAt`, `admittedAtVersion` and `awaitingSinceVersion`;
- sort order and canonical bytes.

This meets EXT-DATA-013's "to the states recorded in the catalog manifest" for the read path.

## 5. What it does not establish

**Level C (authoritative): not achieved, and not claimed** (check LV fails if it ever is). There are two reasons.

1. **Admissibility is not evaluated.** CONTRACT §11.3 defines a valid history as one for which the pinned engine's `History.validate` returns no errors. It also says: "A consumer that cannot evaluate admissibility MUST NOT report a history as valid." EXT-DATA-005-O1 requires "the full contract validation and F# history-admissibility pipeline". This adapter has no such pipeline.
   - The engine is private: `kemiller2002/indy-init-rehearsal-001@55f49f61` (`engine.lock.json`).
   - OD-INDY-22 forbids a rehearsal to copy it.
   - C07 measures the gap: **31 of 35** engine-refused negative fixtures pass every check this adapter can run. They are reported as "structurally valid, admissibility not evaluated", never as valid. Each fixture's file name names the rule a requirements-derived kernel must enforce: transition tables, cause and resolution gates, verification freshness, prediction timing, agent authority and repetition, and content after resolution.
2. **No approved domain representation exists.** The frozen v4.1.4 `CoreEvent` cannot hold 487 of the 1,315 events, and it drops provenance from all of them (`SCHEMA-DIFF.md` §2). The state reproduced here is the causa-storage §8.2 state, not a v4.1.4 `Investigation`. Mapping it into the domain kernel's `Investigation` needs the successor contract (`SUCCESSOR-CONTRACT.md`).

## 6. Exact evidence required for Level C

| # | Evidence | Who | Note |
|---|---|---|---|
| E1 | An approved successor contract that can represent every event (OD-HC-01) | owner | `SUCCESSOR-CONTRACT.md` is the proposal |
| E2 | A requirements-derived `History.validate` equivalent that refuses all 35 `CAUSA.HISTORY_INADMISSIBLE` negative fixtures with the expected domain code, and accepts the 5 synthetic fixtures and the 4 catalog cases | domain implementer, inside the rehearsal window (OD-INDY-22) | C07 is the ready-made acceptance test: change `not-claimed-valid-admissibility-not-evaluated` from tolerated to failure |
| E3 | The same kernel's replay producing `expectedStateSha256` for the four catalog cases | domain implementer | C12 and C16 are the ready-made acceptance tests |
| E4 | An owner ruling on whether "projection equals the engine snapshot and the catalog digest, admissibility not evaluated" is acceptable for **read-only** historical browsing until E2 exists (OD-HC-04) | owner | this is the gap between Level C-projection and Level C |

No expected authoritative result was manufactured. Every expectation used above comes from a causa-data file that predates this work.
