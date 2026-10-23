# Causa v1.0.0 normative clarifications

Normative. Requirement ids `CAUSA-CLAR-01` … `CAUSA-CLAR-14`. These clarifications close ambiguities that a
clean-room implementation (`tools/causa-contract/clean-room-consumer/REQUIREMENTS-GAPS.md`) and the independent review
of CAUSA-REQUIREMENTS-CONSOLIDATION-001 found in the frozen contract. They **add no wire structure and change no frozen
byte**: every valid v1.0.0 file stays valid, and every invalid one stays invalid with the same primary code. Where a
clarification and CONTRACT.md appear to conflict, CONTRACT.md wins and the clarification is defective.

Write-side and protocol codes introduced here (`CAUSA.COMMAND_REFUSED`, `CAUSA.PATH_FROZEN`,
`CAUSA.WRITE_UNCONFIRMED`) are **never persisted** and never reported by a reader for a file set; they exist only on
the command and write path, alongside CONTRACT §11.1's `CAUSA.VERSION_STALE`, `CAUSA.HISTORY_REWRITTEN` and
`CAUSA.UNREPRESENTABLE`.

## CAUSA-CLAR-01 — Identifier lists and causal members at the command boundary (review F-03, CONTRACT §2.7, SEMANTICS §17 E-16)

1. The command boundary MUST de-duplicate identifier-list inputs (`evidenceIds`, `causeIds`, `targetHypothesisIds`,
   `targetCauseIds`, `basisEvidenceIds`) **keeping the first occurrence of each id** and preserving the order of first
   occurrences, before calling `decide`. Example: `[E2, E1, E2]` becomes `[E2, E1]`.
2. Causal-member inputs are de-duplicated the same way, by whole member (`hypothesisId` and `role`), keeping the first
   occurrence. The same hypothesis with different roles is not a duplicate (CONTRACT §7.4).
3. `CAUSA.UNREPRESENTABLE` for duplicates applies only when a writer is handed already-decided events that contain
   duplicates. That cannot happen when rule 1 is followed. A conforming writer therefore never refuses a user command
   because the user cited an id twice.

## CAUSA-CLAR-02 — Meaning of `"admissibility"` in the consumer protocol (GAP-03)

`"admissibility": "evaluated"` means the reported verdict does not depend on an admissibility evaluation that was not
performed. It is `"evaluated"` when stage 15 ran, **and** when an earlier stage failed, because no later stage can
change the primary code then. `"not-evaluated"` is reported only when stage 15 was reached and the consumer could not
decide it. In v1.0.0 that happens only for an out-of-profile artifact URL (SEMANTICS §10.4.1 step 4). In that case
`primaryCode` MUST be `null` only if every stage before 15 passed, and the consumer MUST NOT claim validity.
`detail` SHOULD say whether stage 15 ran.

## CAUSA-CLAR-03 — Codes on the command/write path (GAP-04, GAP-06, review F-13)

| situation | code | written |
|---|---|---|
| expected version ≠ current version | `CAUSA.VERSION_STALE` (CONTRACT §9.2) | nothing |
| `decide` refuses the command (any SEMANTICS rule code, e.g. `INVESTIGATION.NOT_OPEN`, `DUPLICATE_ID`, `EVIDENCE.URL`) | `CAUSA.COMMAND_REFUSED`, with `domainCode` = the first inner rule code of the refusal | nothing |
| a value has no v1 wire form (sub-millisecond instant, U+FEFF, lone surrogate, out-of-profile artifact URL per CAUSA-URL-W, engine `HypothesisChange.At/Sequence` ≠ envelope) | `CAUSA.UNREPRESENTABLE` | nothing |
| the target directory is under `fixtures/v1.0.0/`, `contract/v1.0.0/` or `compatibility/v1.0.0/`, or is not under `workspaces/demo/<runId>/investigations/` | `CAUSA.PATH_FROZEN` | nothing |
| the new file set fails any reader stage when verified before writing (CONTRACT §9.2 step 3) | that stage's code | nothing |
| after writing, a re-read does not return the written bytes | `CAUSA.WRITE_UNCONFIRMED` | see CAUSA-CLAR-04 |
| a candidate successor does not extend the accepted history | `CAUSA.HISTORY_REWRITTEN` (CONTRACT §9.3) | nothing |

Precedence when several apply: path, then version, then representability of the inputs, then `decide`, then
verification of the new file set, then confirmation. When the URL profile rule and another `AdmitEvidence` refusal both
apply, `CAUSA.UNREPRESENTABLE` (input representability) wins.

## CAUSA-CLAR-04 — Atomicity for a local filesystem writer (GAP-05, review F-13)

CONTRACT §9.4 defines atomicity for GitHub (see [PROVIDER-GITHUB.md](PROVIDER-GITHUB.md)). A local filesystem writer
MUST:

1. build and verify the complete new file set in memory (all 17 stages);
2. write it as a **new sibling directory** `<investigationId>.next-<n>`, fsync each file and the directory;
3. publish by renaming the old directory to `<investigationId>.prev-<n>`, then the new one to `<investigationId>`, then
   fsync the parent; and remove `.prev-<n>` only after a re-read of `<investigationId>` returns the written bytes
   (otherwise `CAUSA.WRITE_UNCONFIRMED`).

**Recovery.** On start, if `<investigationId>` is missing or invalid, and a `.prev-<n>` or `.next-<n>` sibling verifies
fully, the writer restores the verified sibling, preferring `.next` only when it extends `.prev` per CONTRACT §9.3.
Sibling directories are not investigation directories: a reader enumerating investigations ignores names that are not
canonical UUIDs (they are not `CAUSA.PATH_INVALID` investigations, because they are not candidates at all). Readers
never repair. Recovery is a writer action that only renames whole, verified file sets.

## CAUSA-CLAR-05 — Append evaluation details (GAP-07)

1. The base investigation MUST verify fully before an append is considered. A base failure is reported with its own
   code and nothing is written.
2. "Current `aggregateVersion`" is the version of the replayed base, which equals the base manifest's.
3. The appended bytes are checked with the `events.ndjson` framing rules. Empty appended bytes are `CAUSA.ENCODING`.
4. Step (3) of CONTRACT §9.2 rebuilds the manifest and snapshot from the combined events by CONTRACT §8.1 and §9.1, and
   then runs every reader stage.

## CAUSA-CLAR-06 — Successor fixtures and directory names (GAP-08)

A `successor` negative fixture is verified under the directory name equal to its base scenario's `investigationId`,
which is found through the root manifest by `scenarioKey`. The previously accepted history is the frozen scenario's
`events.ndjson`. A consumer MUST NOT create a directory named by a fixture's `directoryId` on disk.
`directoryId` values such as `../../fixtures` (fixture `path-traversal-directory`) are data to validate, never paths
to use.

## CAUSA-CLAR-07 — Digests and state reported for invalid inputs (GAP-09)

In the consumer protocol:
- `eventsSha256` is the SHA-256 of the file bytes as read;
- `manifestSha256` and `snapshotSha256` are the SHA-256 of the bytes as read without one trailing LF, which equals the
  canonical digest for valid files;
- `null` is reported for a missing file;
- `stateSha256`, `version` and `lifecycle` describe the state replayed regardless of verdict (SEMANTICS §15.2 step 4)
  once stage 15 has run, and are `null` before that.

## CAUSA-CLAR-08 — Scope of the stage-5 version check (GAP-10)

Stage 5 inspects the top-level `formatVersion`, `schemaVersion` and `semanticVersion` members of the manifest, of each
event line and of the snapshot, in that order. Nested objects carry no version fields in v1.0.0.

## CAUSA-CLAR-09 — Timestamps at the command boundary (GAP-11, GAP-12)

A command boundary accepts RFC 3339 instants with `Z` or a numeric offset and normalizes them to UTC
`YYYY-MM-DDTHH:MM:SS.mmmZ`. It refuses with `CAUSA.UNREPRESENTABLE` (CONTRACT §2.9) an instant with sub-millisecond
precision, an instant without an offset, and a year outside 0001–9999. Calendar validity (CONTRACT §2.9) is enforced
together with the `common.schema.json#/$defs/timestamp` pattern, at stage 7, for every timestamp reached through that
definition. Implementations whose JSON Schema validator cannot express it MUST attach the check to that definition.

## CAUSA-CLAR-10 — Dataset-level codes (GAP-14, review F-18)

1. Dataset verification is a separate operation from investigation verification. It runs on the root manifest,
   `bundle.json`, the golden files and the negative fixtures:
   - framing, canonical form, versions and schema, with the stage codes;
   - then the entry checks, with `CAUSA.FIXTURE_DRIFT`;
   - then `golden/scenarios.json`, with `CAUSA.GOLDEN_MISMATCH`.
2. Unordered or duplicate `investigations` entries in the root manifest, and a negative fixture listed as a valid
   investigation, are `CAUSA.FIXTURE_DRIFT`. They are not expressible in the schema.
3. In the consumer protocol, `verify` reports digests and the acceptance runner compares them with the root manifest.
   The comparison failing is `CAUSA.FIXTURE_DRIFT`, and a replayed state differing from the golden file is
   `CAUSA.GOLDEN_MISMATCH`.

## CAUSA-CLAR-11 — Engine crash case (GAP-13, SEMANTICS §17 E-2)

When re-decision reaches the E-2 situation (a criterion whose corrective action does not exist), the history is
inadmissible. The reported first domain code is the first error accumulated before that group. That error always
exists, because E-2 is reachable only after an earlier inadmissible group; differential testing confirmed this on 2
of 2 occurrences.

## CAUSA-CLAR-12 — Byte-exact storage (review F-19)

Dataset and workspace repositories MUST store every file byte for byte. Each such repository MUST contain a
`.gitattributes` with `* -text` or an equivalent, so that no checkout applies end-of-line conversion. Tools MUST read
blobs as bytes. A conversion that introduces CR fails stage 2 (`CAUSA.ENCODING`). That is correct reader behaviour,
not a dataset defect.

## CAUSA-CLAR-13 — URL rewriting (review F-02)

A writer MUST NOT rewrite, percent-encode, normalize or trim an artifact URL. The URL in `EvidenceAdmitted.content` is
exactly what was decided. A user interface MAY offer an encoded alternative, for example IRI → URI, only as **new,
user-confirmed input**, which is then decided normally.

## CAUSA-CLAR-14 — "F# policy engine" means any conforming implementation of SEMANTICS.md (review F-11)

Where these requirements, INVARIANTS.md (CAUSA-INV-08) or `docs/` say "F# policy engine", "F# domain engine" or
"replay through the F# engine", they mean an implementation of SEMANTICS.md that conforms by SEMANTICS §0.2. The
application's implementation language is an architecture choice outside the persistence contract. Conformance is
SEMANTICS §0.2, evidenced by the acceptance gate. The frozen fixtures are necessary but not sufficient evidence.
