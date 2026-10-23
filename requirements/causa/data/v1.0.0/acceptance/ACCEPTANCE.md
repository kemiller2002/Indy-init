# Causa v1.0.0 storage acceptance criteria

Normative. This is the release gate required by CAUSA-DATA-001 and CAUSA-INV-15. A build (application, storage adapter
or consumer) is releasable against Causa storage v1.0.0 only when **every** row of
[TEST-MATRIX.json](TEST-MATRIX.json) is `PASS`. `NOT_EVALUATED` is not `PASS`.

## 1. Inputs

| input | value |
|---|---|
| requirements | this directory (`requirements/causa/data/v1.0.0/`) |
| frozen contract version | `1.0.0`, pinned in [../compatibility/release-pin.json](../compatibility/release-pin.json) |
| published dataset | `https://github.com/kemiller2002/causa-data`, tag `causa-storage-v1.0.0` (commit `c947c7e354be80766573a782546c8186336774cc`), paths `fixtures/v1.0.0/**` and `contract/v1.0.0/golden/**` |

The dataset repository is a test-data and conformance resource only. A build MUST take its rules from these
requirements, not from code in `causa-data/tools/` or `indy-init-rehearsal-001`.

## 2. Criteria

| id | criterion | matrix rows |
|---|---|---|
| ACC-01 | Parse all 60 imported schemas and the bundle, with every `$ref` resolved, and compute the schema identity `0a68af8c…9152` | `AC-SCHEMA-01` |
| ACC-02 | Recognize exactly the 31 registered discriminators, in registry order | `AC-REG-01` |
| ACC-03 | Deserialize and fully verify the five frozen investigations through all 17 reader stages, **including stage 15 admissibility per [SEMANTICS.md](../SEMANTICS.md)** | `AC-INV-*` (5) |
| ACC-04 | Reconstruct state and match every golden digest: `stateSha256`, `eventsSha256`, `manifestSha256`, `snapshotSha256`, final version and lifecycle | `AC-INV-*` (5) |
| ACC-05 | Reject every one of the 90 negative fixtures with the expected primary code and, for `CAUSA.HISTORY_INADMISSIBLE`, the expected domain code | `AC-NEG-*` (90) |
| ACC-06 | Preserve existing data when writing. Appending a decided event at the observed version keeps the old `events.ndjson` bytes as a prefix, verifies, and is accepted by an independent structural reader. A stale append is refused with nothing written | `AC-APPEND-01`, `AC-APPEND-02` |
| ACC-07 | Imported requirements equal the frozen release (bidirectional parity), and the gate detects every mutation class | `AC-PARITY-01`, `AC-PARITY-02` |
| ACC-08 | No undocumented persisted field: catalogs equal regeneration, and every JSON path in the dataset is catalogued | `AC-CATALOG-01` |
| ACC-09 | Decide the artifact-URL verdict of every published vector exactly as SEMANTICS §10.4.1–§10.4.2 states (`wellFormed`, `illFormed`, `platformDefined`) | `AC-URL-01` |
| ACC-10 | Reject every supplementary negative fixture with its expected codes and accept every supplementary valid history with its expected state digest ([supplementary/README.md](supplementary/README.md)) | `AC-SUP-*` (from `supplementary/MATRIX.json`) |
| ACC-00 | **Step 0, trusted inputs.** Before any row runs, every frozen file in the dataset checkout equals `compatibility/lock.json`, and TEST-MATRIX agrees with `lock.expectedStates` and `lock.negatives`. Otherwise the suite stops and FAILs | `AC-STEP0-DATASET`, `AC-STEP0-MATRIX` |
| ACC-11 | **Provider gate.** For every build that has a GitHub adapter: the AC-PROV rows of [../PROVIDER-GITHUB.md](../PROVIDER-GITHUB.md). The consumer runner does not execute them; the application's own acceptance suite does, and a build without live or fake-server evidence for them is BLOCKED, not PASS | `AC-PROV-*` |

## 3. Consumer protocol

The runner (`tools/causa-contract/acceptance.py`) drives a consumer through the command line. Every command prints one
JSON object as its last stdout line.

```text
<consumer> info               --dataset <causa-data root>
<consumer> verify             --dataset <root> --investigation <investigation directory>
<consumer> negative           --dataset <root> --fixture <negative fixture file>
<consumer> url-verdict        --vectors <artifact-url-vectors.jsonl>
<consumer> append-observation --dir <investigation directory> --expected-version <N>
                              --observation-id <uuid> --original <text> --occurred-at <timestamp>
                              --actor-id <text> --actor-kind human|system --correlation-id <uuid>
```

* `info` → `{"schemasParsed": <int>, "schemaIdentity": "<sha256>", "eventTypes": [...]}`.
* `verify` and `negative` → `{"primaryCode": <CAUSA code or null>, "domainCode": <HISTORY code or null>,
  "admissibility": "evaluated" | "not-evaluated", "stateSha256", "eventsSha256", "manifestSha256", "snapshotSha256",
  "version", "lifecycle", "detail"}`. Digests describe the files as read, and `stateSha256` describes the replayed state.
  For a valid investigation `primaryCode` is `null`.
* `negative` applies the fixture as CONTRACT §12.2 defines its kind:
  - `investigation`: validate the decoded file set under the directory name `directoryId`.
  - `append`: append the decoded lines to the frozen scenario `baseScenarioKey` at `expectedAggregateVersion` (§9.2
    order).
  - `successor`: verify the decoded file set as a successor of the frozen scenario (§9.3).
* `url-verdict` → `{"verdicts": [...]}`, one of `"wellFormed"`, `"illFormed"`, `"platformDefined"` per input line, in order.
* Protocol details are fixed by [../CLARIFICATIONS.md](../CLARIFICATIONS.md): the meaning of `"admissibility"` (CAUSA-CLAR-02), the codes on
  the command/write path (CAUSA-CLAR-03, e.g. `CAUSA.COMMAND_REFUSED` with `domainCode` = the inner rule code), how
  append and successor fixtures are evaluated (CAUSA-CLAR-05, -06; never use `directoryId` as a path), and the
  digests reported for invalid inputs (CAUSA-CLAR-07).
* `append-observation` decides `RecordObservation` through the consumer's implementation of [SEMANTICS.md](../SEMANTICS.md)
  (observedAt `null`) and writes events, manifest and snapshot as one unit. It prints `{"primaryCode": null, "version": N+1}`,
  or the refusal code (`CAUSA.VERSION_STALE` when `--expected-version` is not current).

## 4. Running

```sh
git clone https://github.com/kemiller2002/causa-data && git -C causa-data checkout causa-storage-v1.0.0
python3 -I tools/causa-contract/acceptance.py run --consumer "<consumer command>" --causa-data causa-data
```

The suite verdict is `PASS` only when step 0 and every row (TEST-MATRIX plus `supplementary/MATRIX.json`) pass. Its output and the commit SHAs it ran on are the release
evidence.

## 5. Error behaviour required of every consumer

* Fail closed. Never partially load, repair, re-sort or rewrite an invalid input (CONTRACT §11.2).
* Report the **first** failing stage in CONTRACT §11.1 order. At stage 15, report the first `History.validate` error as
  SEMANTICS.md defines it.
* A consumer without a stage-15 implementation MUST answer `"admissibility": "not-evaluated"` and never claim a valid
  history. The suite then reports `NOT_EVALUATED` and the verdict is not `PASS`.
* Writers refuse values with no v1 wire form (`CAUSA.UNREPRESENTABLE`). Examples: sub-millisecond instants, text
  containing U+FEFF, an out-of-profile artifact URL (CAUSA-URL-W), and engine `HypothesisChange.At/Sequence` that
  differ from the envelope. Duplicate ids in a *command's* id list are **not** refused: the command boundary
  de-duplicates them, keeping the first occurrence (CAUSA-CLAR-01).
* Writers refuse a target under a frozen tree with `CAUSA.PATH_FROZEN`, a domain refusal with `CAUSA.COMMAND_REFUSED`,
  and an unconfirmed write with `CAUSA.WRITE_UNCONFIRMED` (CAUSA-CLAR-03, -04).

## 6. Out of scope for v1.0.0 acceptance

`contract/v1.0.0/golden/scenarios.json` refusals are informative generator records (CONTRACT §12.1). They identify
refused *commands* by generator step and are not replayable from the dataset alone. A build MAY test them through its
own command API, but they are not acceptance rows. SEMANTICS.md maps each refusal to its rule for traceability.
