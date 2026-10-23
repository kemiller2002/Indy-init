# Requirements gaps found while building the clean-room consumer

Scope: `requirements/causa/data/v1.0.0/` was the only rule source. Result: all 102 acceptance rows pass. **No row
needed outside information, a guess about expected values, or special-casing.** Every gap below was resolved from the
text, or deliberately left undecided, and none of them changes a matrix row. Severity:

* **critical**: could not be implemented correctly from the requirements.
* **minor**: an ambiguity resolved with reasonable confidence.
* **info**: an observation with no behavioural choice.

Additional evidence beyond the matrix, all produced with this implementation:

* All 34 stage-15 negative fixtures reproduce the SEMANTICS §18.1 table: first failing version, inner rule code, and
  error multiplicity (1 each for rows 1–30, 6 for `forged-cause-confirmation`, 4 for `forged-resolution`, 68 for
  `missing-start`).
* All 5 `golden/scenarios.json` refusals reproduce their full ordered `engineCodes` list (§18.2).
* All 5 golden state files are byte-equal to the projection.
* 3000 random byte-level and event-level mutations produced no exception.

---

## GAP-01: E-1 artifact URL well-formedness for platform-defined inputs (critical, scoped)

**Text (SEMANTICS §10.4.1):** "(b) `u` is accepted by **.NET 10 `System.Uri.IsWellFormedUriString(u, UriKind.Absolute)`**
… Behaviour for non-ASCII characters, `[`/`]` outside an IPv6 host, leading/trailing whitespace, and other edge cases
is **platform-defined** (§17 E-1): there an implementation MUST either call the .NET 10 function or treat the case as
unresolved." §17 E-1 says the same cases are "**NOT specifiable from text alone**".

**What is missing:** A complete decision procedure. A Python consumer cannot call .NET, so for those inputs the
requirements give no answer.

**What I chose:** I implemented every case that §10.4.1 lists as certain, in both directions: prefix,
backslash, empty or space-containing host, bad port, controls, `" < > \ ^ ` { | }`, bad `%` escapes, bracketed
IPv6, and the accepted DNS/unreserved form. Everything else returns `Unresolved`. That includes non-ASCII (IDN),
`[`/`]` outside IPv6, leading or trailing whitespace, `_` in a host, labels that begin or end with `-`, a second `#`,
and any other ASCII character outside unreserved, sub-delims, `:@/?`. When `Unresolved` occurs at or before the
first stage-15 error, the consumer reports `"admissibility": "not-evaluated"` and does not claim the history is
valid. That is the honest reading of "treat the case as unresolved".

Two residual choices I am not certain of:

* A host such as `999.1.1.1` matches the DNS-label shape and is accepted. .NET might treat it as a malformed IPv4
  address.
* An empty port (`http://h:/`) is accepted, because §10.4.1 says "an empty port … accepted".

No dataset input reaches an unresolved case. The two fixture data points behave as stated.

## GAP-02: E-5 `lowerInvariant` depends on the Unicode or ICU version (minor)

**Text (SEMANTICS §3.3, §17 E-5):** "maps each Unicode code point … to its **simple** (one-to-one) lowercase mapping,
except that U+0130 maps to itself … the Unicode version … depends on the installed `libicu`".

**What is missing:** The Unicode version is not pinned.

**What I chose:** Per code point, Python `str.lower()` (Unicode database of Python 3.13), keeping the result only
when it is a single code point and leaving U+0130 unchanged. Applying it per code point also avoids Python's
final-sigma context rule, which the text forbids. The result is exact for ASCII and common Latin, Greek and
Cyrillic. It can differ from the oracle only for cased letters that differ between Python's Unicode version and the
host ICU's.

## GAP-03: what `"admissibility"` means when an earlier stage fails (minor)

**Text:**

* ACCEPTANCE §3: `"admissibility": "evaluated" | "not-evaluated"`.
* ACCEPTANCE §5: "A consumer without a stage-15 implementation MUST answer `"admissibility": "not-evaluated"`".
* CONTRACT §11.3: "For inputs whose expected code comes after stage 15, such a consumer's primary code is
  conditional on admissibility".

**What is missing:** A definition of the field for inputs that fail at stages 1–14, where stage 15 is never run.
TEST-MATRIX expects `"evaluated"` for those rows, for example `AC-NEG-bom-in-events`, which fails at stage 2.

**What I chose:** `"evaluated"` means "this verdict is not conditional on an unevaluated admissibility check". This
is true when stage 15 ran, and also when an earlier stage failed, because then no later stage can change the
primary code. `"not-evaluated"` is reserved for GAP-01. To stay transparent, `detail` ends with
`[stage 15 run]` or `[stage 15 not-reached]`. I did not add an extra JSON key, because the protocol names the keys.

## GAP-04: no CAUSA code for a domain refusal of a written command (minor)

**Text (ACCEPTANCE §3):** "It prints `{"primaryCode": null, "version": N+1}`, or the refusal code
(`CAUSA.VERSION_STALE` when `--expected-version` is not current)."

**What is missing:** The code to use when `decide(RecordObservation)` refuses. Examples: the investigation is
`resolved` or `closed` (`INVESTIGATION.NOT_OPEN`), or the observation id already exists (`DUPLICATE_ID`). CONTRACT
§11.1 lists no command-refusal code.

**What I chose:** `CAUSA.HISTORY_INADMISSIBLE` with domain code `HISTORY.INADMISSIBLE_EVENT`, which is what
SEMANTICS §15.4 would assign to the group if it were persisted. The inner rule code goes in `detail`. Nothing is
written. No matrix row covers this.

## GAP-05: atomicity of a local three-file write (minor)

**Text:**

* ACCEPTANCE §3: "writes events, manifest and snapshot as one unit".
* CONTRACT §9.4.1 defines atomicity only for GitHub: "one Git commit whose tree contains all three files".

**What is missing:** Semantics for a plain filesystem. POSIX has no atomic commit of several files.

**What I chose:**

1. Decide and build the full new file set in memory, then verify it through all 17 stages (§9.2).
2. Stage three temp files beside the directory, so the directory never holds a 4th entry. `fsync` each one.
3. `os.replace` each file into place, then `fsync` the directory.
4. Re-read the files and compare them byte for byte with what was written (analogue of §9.4.3). A mismatch is
   reported as `CAUSA.WRITE_UNCONFIRMED`. That is my own code; the contract defines none.

A crash between the renames can leave a mixed set. A reader then rejects it (fail closed, with
`EVENTS_HASH_MISMATCH` or `SNAPSHOT_*`), so the data is not silently corrupted, but the write is not atomic in the
strict sense.

## GAP-06: no error code for writing into the immutable tree (minor)

**Text (CONTRACT §3.4):** "`fixtures/v1.0.0/` and `contract/v1.0.0/` are **immutable** … Writers MUST NOT create,
modify or delete anything under them."

**What is missing:** An error code, and how a writer that is given only `--dir` should recognize those trees.

**What I chose:** Refuse with `CAUSA.PATH_INVALID` when the real path contains the consecutive components
`fixtures/v1.0.0` or `contract/v1.0.0`. I did not enforce the positive `workspaces/demo/<runId>/` layout, because
§9.4.4 states it for remote providers.

## GAP-07: append evaluation, including the base and an empty append (minor)

**Text (CONTRACT §9.2):** "(1) the version check (`CAUSA.VERSION_STALE`, before the appended lines are read); (2) the
appended lines through stages 2–7, numbered from current + 1; (3) the combined history through every reader stage,
with manifest and snapshot rebuilt by the evaluator."

**What is missing:**

* Whether the base scenario is verified first.
* Where "current `aggregateVersion`" comes from.
* How stage 2, which is defined for files, applies to an appended chunk, including an empty one.

**What I chose:**

* Verify the base fully; a failure there is reported as is. The current version is the replayed state version,
  which equals the manifest's.
* Apply the `events.ndjson` framing rules to the appended bytes. Empty appended bytes are `CAUSA.ENCODING`
  ("an empty file").
* For step (3), rebuild the manifest and snapshot from the combined events by the §8.1/§9.1 rules, then run
  `verify`.

## GAP-08: successor base and directory name (minor)

**Text (CONTRACT §9.3):** "its `events.ndjson` starts with the previously accepted `events.ndjson` bytes".
ACCEPTANCE §3: "verify the decoded file set as a successor of the frozen scenario".

**What is missing:** The successor fixture schema has no `directoryId`.

**What I chose:** The base is the frozen scenario's `events.ndjson`. The candidate is verified under the directory
name of the scenario's `investigationId`, which is looked up through the root manifest's `scenarioKey`.

## GAP-09: digests and state reported for invalid inputs (minor)

**Text (ACCEPTANCE §3):** "Digests describe the files as read, and `stateSha256` describes the replayed state."

**What is missing:** `manifestSha256` and `snapshotSha256` are defined as canonical-text digests (§4, §8.4). For a
non-canonical or malformed file there is no canonical text. Missing files are not covered either, and neither is
the state to report when stage 15 fails.

**What I chose:**

* For JSON files, SHA-256 of the bytes as read, minus one trailing LF. This equals the canonical digest for valid
  files.
* `null` for a missing file.
* `stateSha256`, `version` and `lifecycle` are those of the state replayed regardless of verdict (SEMANTICS §15.2
  step 4) once stage 15 has run, and `null` before it.

## GAP-10: scope of the stage-5 version check (minor)

**Text (CONTRACT §11.1 stage 5):** "a **string** `formatVersion`/`schemaVersion`/`semanticVersion` ≠ `"1.0.0"`".

**What is missing:** Which JSON levels the check covers.

**What I chose:** The top-level members of the manifest, of each event line, and of the snapshot. Nested
documents, for example `snapshot.state`, carry no such field in v1.

## GAP-11: format of the command-boundary timestamp (minor)

**Text:**

* ACCEPTANCE §3: `--occurred-at <timestamp>`.
* CONTRACT §2.9: "The command boundary MUST supply millisecond-truncated UTC … Writers MUST refuse instants with
  sub-millisecond precision rather than truncate them (`CAUSA.UNREPRESENTABLE`). Time zone offsets are not
  preserved."

**What I chose:**

* Accept an RFC 3339 instant with `Z` or an offset, and normalize it to UTC `…sssZ`.
* Refuse sub-millisecond precision and offset-less input with `CAUSA.UNREPRESENTABLE`.
* Also refuse U+FEFF or lone surrogates in `--original` or `--actor-id`, and non-canonical ids, with
  `CAUSA.UNREPRESENTABLE` (§2.3, ACCEPTANCE §5).
* Any other invalid value, such as a blank `--actor-id`, is caught by verifying the new file set before writing
  and is reported with the reader's stage code.

## GAP-12: calendar validity is "in the schema" but not expressible there (minor)

**Text (CONTRACT §2.9):** "Calendar validity is a stage-7 rule carried by the `timestamp` definition in
`common.schema.json`, for every timestamp in every file, even though the schema pattern cannot express it."

**What is missing:** A standard JSON Schema validator cannot enforce it, and the schema has no hook for it.

**What I chose:** Extend the validator's `pattern` keyword. When the pattern is exactly
`common.schema.json#/$defs/timestamp/pattern`, it also checks for a real proleptic-Gregorian datetime. This covers
every timestamp that reaches that definition through `$ref`.

In the same area, CONTRACT §2.13 requires `$` to keep ECMA semantics. Python's `re` lets `$` match before a final
`\n`, so every unescaped `$` outside a character class is translated to `\Z`.

## GAP-13: E-2 oracle crash (minor)

**Text (SEMANTICS §17 E-2):** "the oracle throws `KeyNotFoundException` … Implementations MUST report the history
inadmissible and SHOULD report the first error accumulated before that group".

**What I chose:** A refusal with the synthetic inner code `ORACLE.EXCEPTION`. The group's `codeFor` applies, and
the earlier error remains first. This is reachable only after an earlier inadmissible group.

## GAP-14: dataset-level codes have no protocol command (minor)

**Text (CONTRACT §11.1):** dataset-level codes `CAUSA.FIXTURE_DRIFT` and `CAUSA.GOLDEN_MISMATCH`.

**What is missing:** ACCEPTANCE §3 `verify` receives `--dataset`, but nothing says that the consumer must compare
the investigation with its root-manifest entry or golden file.

**What I chose:** `verify` reports digests and lets the runner compare them. The root manifest is parsed strictly,
checked for canonical form and against its schema, and its `schemaSha256` is compared with the schema identity
(`FIXTURE_DRIFT`). This happens only when it is used to locate a scenario, that is, for `append` and `successor`
fixtures.

## GAP-15: the "tie-breaking oracle" sits outside the clean room (info)

**Text (README "Precedence" 2):** "Where it is silent or ambiguous, the pinned engine at
`indy-init-rehearsal-001@55f49f6` is the tie-breaking oracle". SEMANTICS §0.3: "Inside those places, the oracle wins
until the register entry is resolved."

**Observation:** By construction, the requirement set is closed only up to the §17 register. A clean-room
implementer cannot consult the oracle (see GAP-01, GAP-02). Apart from E-1 and E-5, I found no place where
SEMANTICS.md was silent or where its text disagreed with the frozen data.

## GAP-16: inputs of the golden refusals are not recorded (info)

**Text (SEMANTICS §18.2, ACCEPTANCE §6):** refusals are "not replayable from the dataset alone".

**Observation:** To cross-check §18.2, I supplied my own inputs: any human actor, and for step 28 a conclusion with
a non-blank `residualUncertainty`. The full `engineCodes` order matched for all five. Out of scope for acceptance.

## Not read in full

I did not need `FIELD-CATALOG.md` (344 KB), `FIELD-MAPPING.md` or the per-event sections of `EVENT-CATALOG.md`
beyond their headings and envelope rules. CONTRACT §6–§8, the schemas and SEMANTICS.md were sufficient. Catalog and
parity rows (`AC-PARITY-*`, `AC-CATALOG-01`) test the requirement tooling, not the consumer.
