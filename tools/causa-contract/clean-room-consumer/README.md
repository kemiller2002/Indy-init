# Clean-room Causa storage v1.0.0 consumer

A Python 3.13 consumer of Causa storage contract v1.0.0. It was written only from
`requirements/causa/data/v1.0.0/`, without the existing consumers, the engine, or the acceptance runner's source.
It implements the consumer protocol of `acceptance/ACCEPTANCE.md` §3.

```sh
python3 -I causa_clean.py info               --dataset <causa-data root>
python3 -I causa_clean.py verify             --dataset <root> --investigation <dir>
python3 -I causa_clean.py negative           --dataset <root> --fixture <negative fixture>
python3 -I causa_clean.py append-observation --dir <dir> --expected-version N --observation-id <uuid> \
    --original <text> --occurred-at <timestamp> --actor-id <text> --actor-kind human|system --correlation-id <uuid>
```

Dependencies: the stdlib plus `jsonschema`/`referencing`. Schemas are read from `requirements/causa/data/v1.0.0/schemas`.
Override that location with `CAUSA_REQUIREMENTS_DIR`.

## Modules and the requirements they implement

| module | implements |
|---|---|
| `canon.py` | CONTRACT §2.2–§2.3: a strict JSON parser. It rejects duplicate keys, non-integers, out-of-range integers, unpaired surrogates, U+FEFF and depth > 128, and returns errors as `Failure` values. §2.2: RFC 8785 serialization, with keys ordered by UTF-16 code units and JCS escaping. §2.11: hashing rules. §5.2: `eventId` UUIDv5 derivation. §2.6 / SEMANTICS §3.2: the blank-text whitespace set. §2.8: canonical UUIDs. |
| `schemas.py` | CONTRACT §10.3: the bundle, per-file digests, schema identity and `$ref` resolution (ACC-01). §2.13: ECMA-262 regex semantics, where `$` is translated to `\Z`. §2.9: calendar validity, attached to the `common.schema.json` `timestamp` pattern. Stage 7 validators. The registry order comes from the envelope `eventType` enum (ACC-02). |
| `semantics.py` | All of SEMANTICS.md for stage 15. §3: equality, `norm`, simple lowercase. §4.4: derived predicates. §5: evolve, which is also the CONTRACT §8.3 fold. §6–§10: decide, covering commands C1–C36, the §7 transition tables, §8 step order, §9 lifecycle rules, §10 content rules and the §10.4.1–§10.4.2 artifact-URL decision procedure and profile, plus the CAUSA-URL-W writer check (`writer_url_check`). §11: cause gate. §12: verification freshness and follow-ons. §13: conclusions. §14: resolution gate. §15: `History.validate` (grouping, sequence errors, verdict branches, `commandOf`, `codeFor`, exact body equality including the implicit `at`/`aggregateVersion`, state threaded regardless of verdict). §17 E-1 cases that the text cannot decide come back as `Unresolved`, never as a guess. |
| `reader.py` | CONTRACT §11.1: the 17 stages in order, first failing stage wins. §8.1: snapshot. §8.2–§8.4: state and digests. §9.1: manifest. §9.2: append evaluation order (version check, then stages 2–7 on the appended lines, then the full reader on the combined history with the manifest and snapshot rebuilt). §9.3: successor (verify alone first, then the byte-prefix check). §11.3: `admissibility`. |
| `causa_clean.py` | ACCEPTANCE §3: the CLI, where the last stdout line is one JSON object. CONTRACT §12.2: the three negative-fixture kinds. §9.2 writer: `append-observation` decides `RecordObservation` through `semantics.decide`, with `observedAt` null and the expected version passed, so stale versions are refused before anything is written. It builds the envelope and its §5.2 id, rebuilds the manifest and snapshot, and verifies the result exactly as a reader would. It then writes all three files and re-reads them to confirm. §2.9 / §5 §11.1 write-side codes: `CAUSA.UNREPRESENTABLE` for sub-millisecond instants, U+FEFF and non-canonical ids. §3.4: refuses to write under `fixtures/v1.0.0` or `contract/v1.0.0`. All I/O lives in this file. |

Style: the modules other than the CLI are pure functions over values that are never mutated. They return new dicts
or tuples and fold with `functools.reduce`. Errors are `Failure` / `Refused` / `Unresolved` values. The only `try`
blocks convert exceptions from library or OS calls into values at the boundary (`schemas.attempt`, UTF-8 decoding).

## `admissibility` in the output

* `"evaluated"`: the verdict does not depend on an unevaluated admissibility check. Either stage 15 ran, or an
  earlier stage already failed, so no later stage can change the primary code. `detail` ends with
  `[stage 15 run]` or `[stage 15 not-reached]` so that you can tell the two apart.
* `"not-evaluated"`: stage 15 reached an artifact URL that §10.4.1 step 4 calls platform-defined, meaning it is
  outside the §10.4.2 profile (SEMANTICS §17 E-1). In that case the consumer reports
  `primaryCode: null` with "structurally valid; admissibility not evaluated" and never claims the history is valid.
  None of the frozen dataset inputs reaches this case.

## Self-test

`python3 -I selftest_urls.py` checks the URL verdict against every vector in
`catalog/artifact-url-vectors.jsonl` (`expected` only). Current result: 3596/3596 agree.

See `REQUIREMENTS-GAPS.md` for every ambiguity and the choice made for it. `acceptance-report.json` is the latest run
of the acceptance suite.
