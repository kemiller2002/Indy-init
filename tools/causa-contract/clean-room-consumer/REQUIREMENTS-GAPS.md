# Requirements gaps found while building the clean-room consumer

Scope: `requirements/causa/data/v1.0.0/` was the only rule source. Second iteration (2026-10-23): GAP-01 was
re-implemented from the corrected §10.4.1–§10.4.2. Result: all 102 acceptance rows pass. **No row
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

## Resolution status (third iteration, after CLARIFICATIONS.md and the SEMANTICS §0/§3.3 changes)

I re-read only CLARIFICATIONS.md, acceptance/ACCEPTANCE.md and SEMANTICS §0, §3.3 and §17 E-5. The consumer now
conforms to them.

| gap | resolved by | consumer change |
|---|---|---|
| GAP-01 URL verdict | SEMANTICS §10.4.1–§10.4.2. CLAR-03: `CAUSA.UNREPRESENTABLE` wins over another `AdmitEvidence` refusal, which settles my residual item 2. | New `url-verdict` command. The IPv6-text corner (residual item 1) is still open. |
| GAP-02 lowercase | SEMANTICS §3.3 pins Unicode 15.1.0 simple lowercase; §17 E-5 resolved | Python 3.13 `unicodedata` is 15.1.0. I checked all code points: U+0130 is the only one whose `str.lower()` is not one-to-one, so per-code-point `lower()` with U+0130 kept equals the pinned mapping. A runtime guard returns "not evaluated" for the repetition rule if the Unicode version differs. |
| GAP-03 `admissibility` | CLAR-02 | none; it matches what I already chose |
| GAP-04 command refusal code | CLAR-03: `CAUSA.COMMAND_REFUSED`, `domainCode` = first inner rule code | changed (I previously used `HISTORY_INADMISSIBLE`/`HISTORY.INADMISSIBLE_EVENT`) |
| GAP-05 local atomicity | CLAR-04 | Replaced per-file renames with the `.next-<n>`/`.prev-<n>` sibling-directory publish, confirm-then-remove, and start-up recovery. |
| GAP-06 frozen tree | CLAR-03: `CAUSA.PATH_FROZEN`, which also applies to any target outside `workspaces/demo/<runId>/investigations/` | changed (I previously used `PATH_INVALID`, frozen trees only) |
| GAP-07 append evaluation | CLAR-05 | none; it matches |
| GAP-08 successor | CLAR-06 | none. `directoryId` was already only data, never a path. |
| GAP-09 digests | CLAR-07 | Stage-1 failures now report digests of the files that are present (previously all null). |
| GAP-10 stage-5 scope | CLAR-08 | none |
| GAP-11 command timestamps | CLAR-09 | Parsing is now strict RFC 3339. Years outside 0001–9999 after UTC normalization are refused; this used to crash on overflow. |
| GAP-12 calendar validity | CLAR-09 | none; I already attached the check to the timestamp definition |
| GAP-13 E-2 | CLAR-11 | none |
| GAP-14 dataset codes | CLAR-10 | none. There is no protocol command for dataset verification (see NEW-09). |
| GAP-15 oracle as tie-breaker | SEMANTICS §0.3 (this document is complete; disagreements are defects in it) and CLAR-14 | none |
| GAP-16 golden refusal inputs | still informative and out of scope (ACCEPTANCE §6) | none |

CLAR-01 (de-duplicating id lists) does not apply: this consumer's only command path, `RecordObservation`, takes no
identifier list or causal members. CLAR-12 and CLAR-13 do not apply either: the consumer reads bytes and never writes
URLs.

## GAP-01: artifact URL well-formedness, E-1 (was critical; resolved by a requirements fix, residual minor)

### History: original text and finding (first iteration)

**Original text (SEMANTICS §10.4.1, before the fix):** "(b) `u` is accepted by **.NET 10
`System.Uri.IsWellFormedUriString(u, UriKind.Absolute)`** … Behaviour for non-ASCII characters, `[`/`]` outside an
IPv6 host, leading/trailing whitespace, and other edge cases is **platform-defined** (§17 E-1)". It also contained a
prose list of cases that "implementations MUST reject" and a form that is "true". §17 E-1 called the rest "**NOT
specifiable from text alone**".

**What I reported:** A Python consumer cannot call .NET, so the requirements had no complete decision procedure. I
implemented the prose list and returned `Unresolved` for everything else. I rated it critical for inputs outside the
list.

**How it was found to be wrong:** I reported this as GAP-01. The coordinator then tested the old prose list against
the .NET 10 predicate and found it was wrong in places. .NET *accepts* trailing whitespace, U+0085, and `{|}` when
non-ASCII is present, and it *rejects* empty DNS labels such as `a..b`. My first implementation followed the old list,
so it inherited those errors. For example, `{`, `|` and `}` in a path were "certainly ill-formed" in my code but are
platform-defined in reality. No acceptance row exercised those inputs, so 102/102 passed with a wrong rule. The
matrix alone could not have caught it.

### Fix (second iteration)

The requirements now give a normative four-step procedure in §10.4.1:

1. Prefix.
2. Interior ASCII whitespace means ill-formed.
3. The strict artifact-URL profile of §10.4.2 decides the verdict.
4. Anything else is platform-defined.

They also add writer rule CAUSA-URL-W, update §17 E-1 to "resolved for conforming data", and add the normative
vectors `catalog/artifact-url-vectors.jsonl`. I re-read only §10.4.1–§10.4.2 and §17 E-1, not
`tools/causa-contract/differential/url_profile.py`.

`semantics.artifact_url_verdict` now implements those four steps literally:

* The profile regex is the §10.4.2 pattern, with ECMA `$` rendered as `\Z`.
* A bracketed host must parse as IPv6 with Python `ipaddress`; `%` and zone ids are excluded by the character class.
* The path, query and fragment split is: first `#`, then first `?` before it.
* The port must be ≤ 65535, the fragment must contain no second `#`, and path, query and fragment characters must
  be in the stated sets with `%HH` escapes.
* Step 4 returns `Unresolved`. At stage 15 that becomes `"admissibility": "not-evaluated"` with `primaryCode: null`,
  and the history is never reported admissible. This applies when the case occurs at or before the first error. If
  an earlier group is already inadmissible, that earlier error is reported, and the verdict stays "inadmissible".

`semantics.writer_url_check` implements CAUSA-URL-W:

* Outside the profile → `CAUSA.UNREPRESENTABLE`.
* Prefix failure, interior whitespace, or an ill-formed URL inside the profile → domain refusal `EVIDENCE.URL`.

The only write command in the consumer protocol is `append-observation`. It never carries an artifact URL, so the
check is present but no CLI path calls it.

### Outcome

* `python3 -I selftest_urls.py`: **3596/3596 vectors agree** (wellFormed 1542/1542, illFormed 1522/1522,
  platformDefined 532/532). The script reads only `url` and `expected`, never `dotnetAccepted`.
* A side check of the data itself: every decided vector agrees with `dotnetAccepted`. Of the platformDefined
  vectors, 264 are accepted by .NET and 268 are rejected, which confirms that no text rule could decide them.
* Acceptance is still 102/102, and 3000 fuzz mutations still produce no exceptions.

### Remaining ambiguity in the new text (minor)

1. **"Valid RFC 4291 IPv6 address text."** RFC 4291 §2.2 does not settle every lexical corner. Examples are an
   embedded IPv4 octet with leading zeros (`::1.2.3.04`) and more than four hex digits in a group. I use Python
   `ipaddress.IPv6Address`, which rejects both. A rejected bracket host falls out of the profile to step 4
   (not-evaluated), which fails closed, so a difference here can only turn a decided case into "not evaluated",
   never into a wrong verdict. The vectors do not distinguish the readings.
2. **CAUSA-URL-W ordering and output code.** The text does not say whether the profile check
   (`CAUSA.UNREPRESENTABLE`) comes before or after the other `AdmitEvidence` checks, for example `DUPLICATE_ID`,
   when both apply. How a domain refusal (`EVIDENCE.URL`) is reported as a CAUSA code is still open (see GAP-04).
   This does not affect this consumer, which has no AdmitEvidence write path.
3. **Deliberate narrowing (info, not a defect).** CAUSA-URL-W refuses out-of-profile URLs that .NET accepts, such
   as the 264 vectors above. A history written directly by the oracle engine with such a URL is therefore
   admissible to the oracle but "not evaluated" to a text-only reader. §17 E-1 states this ("readers fail closed on
   them"), so I record it only so that the trade-off stays visible.

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

---

# Third iteration: new gaps in CLARIFICATIONS.md, ACCEPTANCE.md and the suite (strict reading)

## NEW-01: the runner cannot evaluate AC-URL-01 (critical for the suite verdict; not a consumer defect)

**Text (ACCEPTANCE §3):** "`url-verdict` → `{"verdicts": [...]}`, one of … per input line, in order."

**Problem:** `catalog/artifact-url-vectors.jsonl` has 3596 LF-terminated lines. One `url` value contains a raw
U+0085 (NEL), which JSON allows unescaped. The runner reads the file with Python `str.splitlines()`, which also
breaks at U+0085, U+2028, VT, FF and similar characters. It therefore gets 3597 pieces and crashes with
`JSONDecodeError` inside `url_outcome`. I saw only the traceback, not the runner source.

This aborts the whole `run`, so no full-suite verdict can currently be produced. The requirement text never defines
"line". JSONL and CONTRACT §2.2 imply LF only, which is what my consumer uses.

**Fix needed (requirements or runner):**
1. State "lines are LF-delimited".
2. Make the runner split on `\n`.
3. Optionally, escape C1 and line-separator characters in the vectors file.

My CLI output equals the `expected` column in order for 3596/3596 vectors.

## NEW-02: AC-PARITY-01 fails because release-pin.json is stale (process; not a consumer defect)

`parity_gate.py check` reports 10 `REQUIREMENT_UNPINNED_CHANGE` items. They cover README.md, SEMANTICS.md,
INVARIANTS.md, CLARIFICATIONS.md, compatibility/RULES.md, acceptance/ACCEPTANCE.md, TEST-MATRIX.json, both
catalog/*-semantics.json files and artifact-url-vectors.jsonl. The new normative text has not been pinned, so the
suite cannot PASS until `release-pin.json` is updated in a reviewed change.

## NEW-03: CLAR-04 sibling directories conflict with CONTRACT §3.2 (minor; a real text conflict)

**Text:**

* CLAR-04: "a reader enumerating investigations ignores names that are not canonical UUIDs (they are not
  `CAUSA.PATH_INVALID` investigations …)".
* CONTRACT §3.2: "A reader MUST reject any other directory name (`CAUSA.PATH_INVALID`)".
* CLARIFICATIONS preamble: "Where a clarification and CONTRACT.md appear to conflict, CONTRACT.md wins and the
  clarification is defective."

**Problem:** By the preamble's own rule, a reader that enumerates `workspaces/demo/<runId>/investigations/` while a
`.next-<n>`, `.prev-<n>` or (my) `.invalid-<n>` sibling exists must reject it. CLAR-04 says the opposite. My consumer
never enumerates directories, so its behaviour is unaffected.

**Fix needed:** CONTRACT §3.2 should scope "reject" to a directory that is *given* as an investigation, or
CLAR-04 should put the siblings outside `investigations/`.

## NEW-04: CLAR-04 leaves several recovery details open (minor)

**Text (CLAR-04):** "On start, if `<investigationId>` is missing or invalid, and a `.prev-<n>` or `.next-<n>` sibling
verifies fully, the writer restores the verified sibling … Recovery is a writer action that only renames whole,
verified file sets."

**What is missing, and what I chose:**

| open point | my choice |
|---|---|
| (a) How `<n>` is chosen. | The smallest `n ≥ 1` not used by any `.next`, `.prev` or `.invalid` sibling. |
| (b) What happens to an existing but *invalid* `<id>`. Renaming it is not "renaming a verified set", but restoring requires the name. | Move it aside, unchanged, to `<id>.invalid-<n>`. It is never deleted or repaired. A stricter reading would refuse instead. |
| (c) Which sibling to use when several `n` verify. | The highest `n` of each kind. Then `.next` is used only if its events start with `.prev`'s, else `.prev`. |
| (d) What "on start" means. | The start of every `append-observation`. |
| (e) What happens to a non-chosen `.next`. | Left in place. |
| (f) After `CAUSA.WRITE_UNCONFIRMED` the new set is already published as `<id>`; the table says "written: see CAUSA-CLAR-04", but CLAR-04 does not say whether to roll back. | Keep it published, keep `.prev-<n>`, and report `CAUSA.WRITE_UNCONFIRMED`. |

## NEW-05: CLAR-03 precedence omits a failing base investigation (minor)

**Text (CLAR-03):** "Precedence when several apply: path, then version, then representability of the inputs, then
`decide` …"

**What is missing:** The version check needs the current version, which comes from the verified base (CLAR-05 §1
says the base must verify, but CLAR-05 is about append fixtures). Where a base that fails verification sits in the
order is not stated.

**What I chose:** path, then recovery (CLAR-04), then base verification (its own stage code), then version,
representability, decide, verification of the new set, confirmation.

## NEW-06: boundary between "representability" and "decide" (minor)

**Text (CLAR-03):** "a value has no v1 wire form … → `CAUSA.UNREPRESENTABLE`"; "`decide` refuses … →
`CAUSA.COMMAND_REFUSED`"; representability comes before decide.

**Problem:** Blank `--original` has no wire form (the schema `text` definition), and decide also refuses it
(`VALIDATION.EMPTY`). The precedence makes the classification matter.

**What I chose:** If SEMANTICS has a rule for the value, `decide` reports it: blank `original` →
`COMMAND_REFUSED` / `VALIDATION.EMPTY`. If decide never sees the value, it is `UNREPRESENTABLE`. That covers blank or
U+FEFF `actorId`, an actor kind other than human/system, non-canonical ids, and U+FEFF or lone surrogates in any text.

## NEW-07: the `CAUSA.PATH_FROZEN` condition is under-specified (minor)

**Text (CLAR-03):** "the target directory is under `fixtures/v1.0.0/`, `contract/v1.0.0/` or
`compatibility/v1.0.0/`, or is not under `workspaces/demo/<runId>/investigations/`".

**What is missing:**

* Whether "under" means directly (`…/investigations/<id>`) or at any depth.
* Whether `<runId>` must match the CONTRACT §3.2 pattern.
* Whether the test applies to the given path or the resolved one (symlinks).
* A single code covers two different situations: frozen, and not a workspace.

**What I chose:** The real path, with symlinks resolved, must end with
`workspaces/demo/<runId>/investigations/<dir>`, where `<runId>` matches `^[a-z0-9][a-z0-9-]{0,63}$`. No path
component pair may be `fixtures|contract|compatibility` followed by `v1.0.0`.

## NEW-08: CLAR-09 year check, before or after normalization (minor)

**Text (CLAR-09):** "refuses … a year outside 0001–9999".

**What is missing:** Whether the year is the input's local year or the UTC year. For example,
`0001-01-01T00:30:00+01:00` is year 0000 in UTC.

**What I chose:** The input must be calendar-valid, *and* the normalized UTC instant must lie in 0001–9999.
Related RFC 3339 corners:

* Lowercase `t`/`z` are accepted.
* A leap second `:60` is refused as calendar-invalid (CONTRACT §2.9).
* `-00:00` is treated as UTC, although RFC 3339 gives it an "unknown local offset" meaning.

## NEW-09: CLAR-10 dataset verification has no protocol command or acceptance row (minor)

**Text (CLAR-10):** "Dataset verification is a separate operation …".

**Problem:** ACCEPTANCE §3 has no command for it and no row exercises it, so a consumer cannot show that it
conforms. I did not add a command; the root manifest is checked only when it is used to locate a scenario.

## NEW-10: CLAR-02 and stages 16–17 after a not-evaluated stage 15 (minor)

**Text:**

* CLAR-02: "`primaryCode` MUST be `null` only if every stage before 15 passed".
* CONTRACT §11.3: "For inputs whose expected code comes after stage 15, such a consumer's primary code is
  conditional on admissibility, and it MUST say so."

**What is missing:** Whether a consumer whose stage 15 is not evaluated should still run stages 16–17 and report
their code as conditional, or stop with `null`.

**What I chose:** Stop with `null`, and say so in `detail`. This happens only for out-of-profile URLs.

## NEW-11: supplementary acceptance artefacts are referenced but absent (info)

ACCEPTANCE ACC-10 and §4 make the verdict depend on `acceptance/supplementary/MATRIX.json` and
`supplementary/README.md`. Neither exists yet, so no AC-SUP row ran.

## NEW-12: digests reported for append and successor negatives (minor)

**Text (CLAR-07):** "Digests describe the files as read".

**What is missing:** For an `append` fixture that fails before step (3), the "files as read" are ambiguous: the
base, the appended chunk, or the combined set.

**What I chose:** The base investigation's digests until the combined set is built, then the combined set's.

---

# Fourth iteration: NEW-01…12 rechecked against the revised CLARIFICATIONS.md and ACCEPTANCE.md §3

I re-read CLARIFICATIONS.md and ACCEPTANCE.md §3, plus `acceptance/supplementary/`, which now exists.

**Single full run:** 170/171 PASS. The only failure is AC-PARITY-01, which is NEW-02 and set aside on instruction.
The run includes:

* AC-STEP0-DATASET and AC-STEP0-MATRIX;
* AC-URL-01 (3596/3596);
* AC-DATASET-01;
* all 64 supplementary rows (61 negative, 3 valid).

**Stricter evidence:** the supplementary matrix publishes the oracle's *complete* ordered `History.validate` list,
with history code and inner rule code, for each negative. My full lists equal it for **61/61** rows. The runner
compares only the first code.

| gap | status | resolved by / note |
|---|---|---|
| NEW-01 | resolved | ACCEPTANCE §3 now says lines are LF only and U+0085/U+2028/U+2029 are not breaks, and the runner was fixed. My NDJSON readers already split on `\n` only. The vector readers in `url-verdict` and `selftest_urls.py` now read bytes, because text-mode `open()` would treat a raw CR as a line end. |
| NEW-02 | open (coordinator) | `release-pin.json` is not updated; `parity_gate` now reports 12 differences. |
| NEW-03 | resolved | CLAR-04 moves staging to `workspaces/demo/<runId>/.staging/`, so `investigations/` holds only investigations and CONTRACT §3.2 applies unchanged. Implemented. |
| NEW-04 | resolved, with residual NEW-14 and NEW-15 | CLAR-04 fixes `<n>` (1 + the largest present), the ranking (highest `<k>`; `.next` over `.prev` at equal `k` only if it extends it), `.invalid-<n>` kept, recovery before first access, and no rollback after `WRITE_UNCONFIRMED`. Implemented. |
| NEW-05 | resolved | CLAR-03 precedence now includes base verification at step 2. It matches what I implemented. |
| NEW-06 | resolved | CLAR-03 "Representability vs decide" matches my choice. Blank `--original` gives `COMMAND_REFUSED` with `VALIDATION.EMPTY`. |
| NEW-07 | resolved, with residual NEW-13 | CLAR-03 "Paths" splits the cases: `PATH_FROZEN` at any depth below `fixtures/contract/compatibility/<version>/`, otherwise `PATH_INVALID` unless the target is exactly `workspaces/demo/<runId>/investigations/<uuid>`, evaluated after resolving symlinks and `.`/`..`. Implementing it exposed a bug of mine, now fixed: I checked the resolved path but recovered and wrote through the unresolved one, which could move a symlink. The resolved path is now used for every step. |
| NEW-08 | resolved | CLAR-09: `t`/`z` accepted, `±00:00` is UTC, leap second 60 refused, UTC-year bound. Matches. |
| NEW-09 | resolved | CLAR-10 §4 adds `verify-dataset` and AC-DATASET-01. Implemented in `dataset.py`. It passes on the frozen dataset and reports the right code for each mutation I tried: entry drift, golden state, scenarios.json, root framing, an extra negative file, unordered entries, a schema byte change. |
| NEW-10 | resolved | CLAR-02: when stage 15 is not evaluated, I now run stages 16–17 and report any code only in `detail`, marked conditional; `primaryCode` stays null. |
| NEW-11 | resolved | `acceptance/supplementary/` exists; 64/64 pass. |
| NEW-12 | resolved | CLAR-07 matches what I already did. |

## NEW-13: what counts as a `<version>` directory in CLAR-03 (minor)

**Text:** "`CAUSA.PATH_FROZEN` applies when the resolved path is at any depth below a `fixtures/<version>/`,
`contract/<version>/` or `compatibility/<version>/` directory."

**What is missing:** `<version>` is not defined. Is it any child of `fixtures/`, or only a version-shaped name?

**What I chose:** A segment matching `v?N.N.N` (for example `v1.0.0` or `v2.0.0`). A target such as
`…/fixtures/scratch/…` is therefore not frozen; it is `PATH_INVALID` instead. The code differs, but nothing is
written either way.

## NEW-14: CLAR-04 recovery contradicts itself about `.invalid-<n>` (minor; a real contradiction)

**Text:**

* Recovery step 1: "If `investigations/<investigationId>` exists and verifies fully, it stays. Every staging sibling
  for that id is deleted."
* Step 2: "`.staging/<investigationId>.invalid-<n>` … kept for inspection, never deleted automatically."

**Problem:** An `.invalid-<n>` directory is a staging sibling for that id, so step 1 deletes what step 2 says is never
deleted automatically.

**What I chose:** Step 1 deletes only `.next-*` and `.prev-*`. `.invalid-*` is never deleted.

## NEW-15: CLAR-04 gives no code for a missing investigation (minor)

**Text (recovery step 5):** "If no candidate verifies, the investigation is reported missing. Nothing is invented."

**What is missing:** The code for "reported missing", and the `<n>` rule for `.invalid-<n>` in step 2. The `<n>` rule
is stated only for `.next` in publish step 2.

**What I chose:** `CAUSA.PATH_INVALID` ("investigation directory does not exist"), with the recovery note in
`detail`. `.invalid-<n>` uses the same rule: 1 + the largest `<n>` present for that id.

## NEW-16: recovery after `WRITE_UNCONFIRMED` can keep bytes that are not the writer's (minor)

**Text (CLAR-04 publish step 4):** "otherwise report `CAUSA.WRITE_UNCONFIRMED`, perform no rollback, and run
recovery". Recovery step 1: a fully verifying `investigations/<id>` "stays", and every staging sibling is deleted.

**Problem:** If the re-read differs from the written bytes but still verifies, for example because another writer
replaced it, step 1 keeps it and deletes `.prev-<n>`. The pre-write history is then gone, without a §9.3 prefix
check against it.

**What I chose:** Followed the text literally. I am recording the risk; no acceptance row covers it.

## NEW-17: CLAR-09 does not give a code for a calendar-invalid date (minor)

**Text (CLAR-09):** it lists refusals for sub-millisecond precision, a missing offset, a leap second and the UTC year.

**What is missing:** A date such as `2026-02-30` (which RFC 3339 §5.7 makes invalid), or an offset hour above 23, is
not in that list.

**What I chose:** `CAUSA.UNREPRESENTABLE`, as for the listed cases.

## NEW-18: CLAR-10 entry checks are still partly open (minor)

**Text (CLAR-10 §1–§2):** "then the entry checks, with `CAUSA.FIXTURE_DRIFT`", "a negative fixture listed as a valid
investigation".

**What is missing:**

* Which entry checks exist beyond the digests, version, lifecycle and title (CONTRACT §11.1).
* What "listed as a valid investigation" means operationally.
* Whether dataset verification must also *run* every negative fixture and check its expected code.
* Which code applies when an investigation itself fails a reader stage inside dataset verification.

**What I chose:**

* Run the root-manifest entry comparisons, plus:
  * the bundle's schema identity, equal to the root manifest's and to the requirements' identity;
  * the bundle's file list and digests;
  * investigation directories exactly equal to the entries;
  * `negative/` files exactly equal to the listing, with `name` and `expectedErrorCode` matching.
* An investigation-kind negative counts as "listed as valid" if its decoded files equal a listed investigation's
  files under the same id.
* Running the negatives is left to the acceptance rows.
* An investigation that fails a reader stage reports that stage's code.

## NEW-19: the append-observation output example omits `domainCode` (info)

**Text:**

* ACCEPTANCE §3: "It prints `{"primaryCode": null, "version": N+1}`, or the refusal code".
* CLAR-03 requires `domainCode` for `CAUSA.COMMAND_REFUSED`.

**Problem:** The §3 output shape for `append-observation` never mentions `domainCode`.

**What I chose:** Always print `primaryCode`, `domainCode`, `version` and `detail`.
