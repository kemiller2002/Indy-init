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
`detail` SHOULD say whether stage 15 ran. When stage 15 is not evaluated, the consumer MAY still run stages 16–17.
Any code they produce is reported only in `detail`, marked conditional on admissibility (CONTRACT §11.3), while
`primaryCode` stays `null` (NEW-10).

## CAUSA-CLAR-03 — Codes on the command/write path (GAP-04, GAP-06, review F-13)

| situation | code | written |
|---|---|---|
| expected version ≠ current version | `CAUSA.VERSION_STALE` (CONTRACT §9.2) | nothing |
| `decide` refuses the command (any SEMANTICS rule code, e.g. `INVESTIGATION.NOT_OPEN`, `DUPLICATE_ID`, `EVIDENCE.URL`) | `CAUSA.COMMAND_REFUSED`, with `domainCode` = the first inner rule code of the refusal | nothing |
| a value has no v1 wire form (sub-millisecond instant, U+FEFF, lone surrogate, out-of-profile artifact URL per CAUSA-URL-W, engine `HypothesisChange.At/Sequence` ≠ envelope) | `CAUSA.UNREPRESENTABLE` | nothing |
| the target directory is under `fixtures/<v>/`, `contract/<v>/` or `compatibility/<v>/` | `CAUSA.PATH_FROZEN` | nothing |
| the target is not exactly `workspaces/demo/<runId>/investigations/<investigationId>` | `CAUSA.PATH_INVALID` | nothing |
| the new file set fails any reader stage when verified before writing (CONTRACT §9.2 step 3) | that stage's code | nothing |
| after writing, a re-read does not return the written bytes | `CAUSA.WRITE_UNCONFIRMED` | see CAUSA-CLAR-04 |
| a candidate successor does not extend the accepted history | `CAUSA.HISTORY_REWRITTEN` (CONTRACT §9.3) | nothing |

Precedence when several apply (NEW-05):
1. path;
2. verification of the base investigation, reported with its own reader code;
3. version;
4. representability of the inputs;
5. `decide`;
6. verification of the new file set;
7. confirmation.

When the URL profile rule and another `AdmitEvidence` refusal both apply, `CAUSA.UNREPRESENTABLE` (input
representability) wins.

**Representability vs `decide`** (NEW-06): `CAUSA.UNREPRESENTABLE` covers only values that `decide` never inspects or
cannot express:
- timestamps with precision or offset problems;
- U+FEFF and lone surrogates;
- non-canonical UUID text;
- out-of-profile URLs.

A value that `decide` has a rule for (blank text → `VALIDATION.EMPTY`, an ill-formed in-profile URL → `EVIDENCE.URL`)
is refused by `decide`, giving `CAUSA.COMMAND_REFUSED`.

**Paths** (NEW-07): the target is evaluated after resolving symbolic links and `.`/`..`.
- `CAUSA.PATH_FROZEN` applies when the resolved path is at any depth below a `fixtures/<version>/`,
  `contract/<version>/` or `compatibility/<version>/` directory. Here `<version>` is a path segment matching
  `^v?[0-9]+\.[0-9]+\.[0-9]+$` (NEW-13).
- `CAUSA.PATH_INVALID` applies when it is not exactly `workspaces/demo/<runId>/investigations/<investigationId>`
  with `<runId>` matching `^[a-z0-9][a-z0-9-]{0,63}$` and `<investigationId>` a canonical UUID (CONTRACT §3.2).

## CAUSA-CLAR-04 — Atomicity for a local filesystem writer (GAP-05, review F-13, NEW-03, NEW-04)

CONTRACT §9.4 defines atomicity for GitHub (see [PROVIDER-GITHUB.md](PROVIDER-GITHUB.md)). A local filesystem writer
works in a **staging directory** `workspaces/demo/<runId>/.staging/`, which is a sibling of `investigations/` and on the
same filesystem. `investigations/` therefore only ever contains investigation directories, so CONTRACT §3.2 applies
unchanged: readers reject any other entry there. The writer MUST:

1. build and verify the complete new file set in memory (all 17 stages);
2. write it to `.staging/<investigationId>.next-<n>`, where `<n>` is 1 + the largest `<n>` already present in
   `.staging/` for that id, or 1; then fsync each file and the directory;
3. publish:
   - rename `investigations/<investigationId>` to `.staging/<investigationId>.prev-<n>`;
   - rename `.staging/<investigationId>.next-<n>` to `investigations/<investigationId>`;
   - fsync both parent directories;
4. re-read `investigations/<investigationId>`:
   - if it returns the written bytes, delete `.prev-<n>`;
   - otherwise report `CAUSA.WRITE_UNCONFIRMED`, perform no rollback, and run recovery.

**Recovery** runs before the writer's first read or write of an investigation in a process.

1. If `investigations/<investigationId>` exists and verifies fully, it stays, with one condition (NEW-16): if a
   `.prev-<k>` sibling exists, the current `events.ndjson` MUST start with the `events.ndjson` bytes of **every**
   such sibling (CONTRACT §9.3; normally there is at most one; NEW-20).
   - If it does, the `.next-*` and `.prev-*` siblings for that id are deleted.
   - If it does not, the writer reports `CAUSA.HISTORY_REWRITTEN`, keeps every sibling, and writes nothing more
     to that investigation until a person resolves it.
   - `.invalid-*` siblings are never deleted automatically (NEW-14).
2. Otherwise, any existing `investigations/<investigationId>` is moved to `.staging/<investigationId>.invalid-<n>`
   and kept for inspection, never deleted automatically. `<n>` follows the same rule as `.next-<n>` (NEW-15).
3. Then the candidates among the `.next-<k>` and `.prev-<k>` siblings that verify fully are ranked:
   - the highest `<k>` wins;
   - at equal `<k>`, `.next` wins over `.prev` only if its `events.ndjson` starts with the `.prev` bytes (CONTRACT §9.3),
     otherwise `.prev` wins.
4. The winner is renamed into `investigations/`, and the other `.next`/`.prev` siblings are deleted.
5. If no candidate verifies, the investigation is reported missing with `CAUSA.PATH_INVALID` (a required
   directory is absent; NEW-15). Nothing is invented.

Readers never repair. Recovery is a writer action that only renames whole, verified file sets. `.staging/` is never
published by a remote provider, because CAUSA-PROV-15's path guard excludes it.

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
- for an `append` fixture that fails before the combined file set is built, the digests describe the base
  investigation (NEW-12);
- `stateSha256`, `version` and `lifecycle` describe the state replayed regardless of verdict (SEMANTICS §15.2 step 4)
  once stage 15 has run, and are `null` before that.

## CAUSA-CLAR-08 — Scope of the stage-5 version check (GAP-10)

Stage 5 inspects the top-level `formatVersion`, `schemaVersion` and `semanticVersion` members of the manifest, of each
event line and of the snapshot, in that order. Nested objects carry no version fields in v1.0.0.

## CAUSA-CLAR-09 — Timestamps at the command boundary (GAP-11, GAP-12)

A command boundary accepts RFC 3339 `date-time` values (RFC 3339 §5.6). It accepts `T`/`t` and `Z`/`z`
case-insensitively, and treats `-00:00` and `+00:00` as UTC. It normalizes them to UTC `YYYY-MM-DDTHH:MM:SS.mmmZ`.
It refuses with `CAUSA.UNREPRESENTABLE` (CONTRACT §2.9):
- an instant with sub-millisecond precision (more than three fractional digits, unless the extra digits are all zero);
- an instant without an offset;
- a leap second `60`, a calendar-invalid date, or an offset outside `-23:59`…`+23:59` (NEW-17);
- an instant whose **UTC** year falls outside 0001–9999. Calendar validity (CONTRACT §2.9) is enforced
together with the `common.schema.json#/$defs/timestamp` pattern, at stage 7, for every timestamp reached through that
definition. Implementations whose JSON Schema validator cannot express it MUST attach the check to that definition.

## CAUSA-CLAR-10 — Dataset-level codes (GAP-14, review F-18)

1. Dataset verification is a separate operation from investigation verification. It runs on the root manifest,
   `bundle.json`, the golden files and the negative fixtures:
   - framing, canonical form, versions and schema, with the stage codes;
   - then the entry checks, with `CAUSA.FIXTURE_DRIFT`;
   - then `golden/scenarios.json`, with `CAUSA.GOLDEN_MISMATCH`.
2. Unordered or duplicate `investigations` entries in the root manifest are `CAUSA.FIXTURE_DRIFT`, which the schema
   cannot express. So is a `negativeFixtures` entry whose decoded investigation id equals a listed valid investigation's id. Entry
   checks compare **every** field of an `investigations` entry with the
   investigation as verified (NEW-18). An investigation that fails a reader stage is reported with that stage's code,
   not `CAUSA.FIXTURE_DRIFT`. `verify-dataset` checks that every listed negative fixture file exists (a missing one is
   `CAUSA.PATH_INVALID`; NEW-21) and validates against `negative-fixture.schema.json`. Rejecting each negative with its code is the job of the per-fixture
   acceptance rows.
3. In the consumer protocol, `verify` reports digests and the acceptance runner compares them with the root manifest.
   The comparison failing is `CAUSA.FIXTURE_DRIFT`, and a replayed state differing from the golden file is
   `CAUSA.GOLDEN_MISMATCH`.
4. Consumers expose dataset verification as the protocol command `verify-dataset --dataset <root>`, which returns
   `{"primaryCode": null | code, "detail"}` (NEW-09). Acceptance row `AC-DATASET-01` expects `null` for the frozen
   dataset.

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
