# Adversarial review

The work was challenged twice:

1. **An independent reviewer.** A separate agent was given only the code, CONTRACT.md, the schemas and the data, not this proposal's conclusions. It probed the library with its own throwaway programs. It modified no repository file.
2. **The author's own pass,** against each hazard the mission lists.

Each confirmed defect was fixed. No test was weakened, and every fix comes with a check that fails without it. C18 was verified to fail with the fix reverted (19/20) and to pass with it (20/20).

## 1. Findings and dispositions

| ID | Severity | Finding | Verified how | Disposition |
|---|---|---|---|---|
| **AR-01** | high | `snapshot.json` `state` was accepted as any object, with no stage-7 schema check. An undeclared state field, a missing array, a mistyped title or a bad enumeration value was reported as `SNAPSHOT_MISMATCH` (stage 17). When combined with a manifest defect, it was reported as `MANIFEST_MISMATCH` (stage 8) instead of `SCHEMA_VIOLATION` (stage 7). That breaks "conforming readers MUST agree on the primary code" (CONTRACT §11.1). No negative fixture covers it. | Reproduced by the reviewer with a recomputed `snapshotSha256` | **Fixed.** New `src/StateSchema.fs` is a declarative closed-key table of every §8.3 entity, the hypothesis-change history, causal members and provenance. It is called from `StorageReader.decodeSnapshot`. New check **C18** has 7 mutations of real Apollo 1 snapshot bytes, including the reviewer's stage-8 combination. |
| AR-02 | medium | C06 mutated only payload fields, and only for the 26 types in the history. Envelope, actor and causal-member fields and the 5 fixture-only types were untested. | Code reading | **Fixed.** C06 now applies 275 mutations: 245 payload mutations across all 31 types, 27 envelope and actor mutations, and 3 causal-member mutations. Its title states exactly that scope. |
| AR-03 | medium | C07 "passes" 31 engine-refused fixtures because the loader reports `NotEvaluated`. A loader that ignored admissibility would score the same. | Code reading | **Accepted as stated, not hidden.** C07 is a measurement of the gap, not a proof of rejection. `REPLAY-VERIFICATION.md` §5–§6 and the README report 31/35 as **not** handled, and Level C is not claimed (check LV). C07 becomes the kernel's acceptance test once the tolerated verdict is changed to a failure (OD-HC-03, evidence E2). |
| AR-04 | medium | The successor half of C11 is close to a tautology: most `CoreEventV415` cases wrap the same payload records, so `toWire ∘ ofWire` is the identity by construction. | Code reading | **Accepted, with scope extended.** The non-trivial part is the 3 split families (lifecycle, hypothesis, corrective action) and the widened cases, which C11 does exercise. C11 now also covers the 5 synthetic fixtures, so all 31 types and every split variant present in the corpus pass through the successor. The independent losslessness proof is the direct encoder: rebuilt `events.ndjson` = catalog `eventsSha256`. |
| AR-05 | low | C03's title said "decoded from real data", but 5 types come only from synthetic fixtures. | Evidence field | **Fixed:** the title now says "26 from the historical cases, the other 5 only from the frozen synthetic fixtures". |
| AR-06 | low | C04 used `List.zip`, so if a line failed to decode the runner would crash instead of reporting a failure. | Code reading | **Fixed:** a length mismatch is now a reported failure. |
| AR-07 | low | C09 compares two reads in one process. | Code reading | **Mitigated.** Verify mode (no `--write`) compares the regenerated `HISTORICAL-COVERAGE.json`, which contains the C09 fingerprints and state digests, with the copy committed from an earlier process. That is a cross-process determinism check. Repeated runs produced identical reports. |
| AR-08 | low | In the test file-system source, a subdirectory named like one of the three files was read as an empty file (`ENCODING`) instead of `PATH_INVALID`. | Code reading | **Fixed:** directories are listed with a trailing `/`, so stage 1 refuses them. |

## 2. Areas the reviewer probed with no defect found, by running code

**JSON layer:**

- the depth limit, 128 accepted and 129 refused;
- duplicate keys detected after escape decoding;
- lone and reversed surrogates;
- U+FEFF raw, escaped and as a BOM;
- `-0`, leading zeros, floats, exponents and the ±(2^53−1) range;
- no stack overflow on 1M-element arrays, 1M escapes or 100k members;
- the JCS key order by UTF-16 code units (astral versus U+E000), and JCS escaping.

**Primitives:** the whitespace set, `httpUrl`, and the `\A…\z` anchors.

**Decoders:** all 31 payload schemas compared field by field, including exact keys, nullability, `*Target` subsets, `uniqueItems`, `minItems` and every if/then invariant. The envelope `allOf` rules are enforced, and the manifest decoder matches its schema.

**Stage order:** correct apart from AR-01.

**Projection:** matches every §8.3 row, including paths the history never exercises: proposals, supersession, reopened cause, `awaitingSinceVersion`, and the version bump on no-op updates.

## 3. The author's pass against the mission's list

| Hazard | Result | Evidence |
|---|---|---|
| Events dropped silently | none | C02: lines = outcomes = decoded = manifest = catalog. C11: byte-identical rebuild. |
| Incorrect default values | none invented | The only defaults are the §8.3 initial states (for example evidence `unverified`, cause `proposed`, `awaitingSinceVersion` 0), and the engine snapshot confirms them byte for byte. The successor removes `HypothesisFalsified`'s `Scope` rather than default it. |
| Unknown fields discarded | refused everywhere | `Field.exactKeys` on the envelope, actor, payloads, causal members, manifest and snapshot, and now every state entity (C06, C18) |
| Incorrect event ordering | none | C04. Order comes only from `aggregateVersion`; `occurredAt` is not unique (`SCHEMA-DIFF.md` §4). |
| Loss of historical identifiers | none | C05: every `eventId` re-derived. C10: every provenance claim id, source id and URL found in the stored text. |
| Invented replay semantics | none | The fold is the §8.3 table only. The adapter's "inadmissible" anomalies are reported as necessary conditions, never as admissibility. The reviewer notes they could in theory refuse a history the engine admits; none fired on the 9 valid histories (3 historical, lightbulb, 5 synthetic). |
| Non-deterministic results | none | C09 and AR-07 |
| Wrong assumptions about the source schema | checked against the schemas, not the report | The reviewer compared the schemas field by field. The prior report's 432/431/452 counts were confirmed (C02). Its event totals, such as `EvidenceRelated` 195, were for four cases including the lightbulb. The three historical cases have 186. |
| Tests that only test the decoder's own fixtures | avoided | Positive inputs are causa-data files. Mutations start from real lines. Negative expectations are causa-data's 90 fixtures. State expectations are engine snapshots, catalog digests and golden files. |
| Compatibility claims based only on parsing | avoided | Level B (typed decode plus byte round trip) and Level C-projection (independent snapshots and digests) are separate checks. Level C is reported as not achieved. |

## 4. Residual limitations

These are known and are not defects of the claims made:

- **Admissibility is not evaluated** (AR-03): OD-HC-03.
- **Append-side behaviour (`VERSION_STALE`) is out of scope.** The adapter is read-only, and the one append fixture is reported as not applicable.
- **The adapter's anomaly checks are stricter than nothing and weaker than the engine.** They are labelled that way in every result.
