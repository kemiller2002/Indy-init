# Causa persistence requirements — storage contract v1.0.0

This directory is the **complete normative persistence specification** for Causa (the Root-Cause Investigator) at
storage contract version `1.0.0` (requirement CAUSA-DATA-001 in [docs/REQUIREMENTS.md](../../../../docs/REQUIREMENTS.md)).
An implementer needs only this directory to build the storage adapter, validate every persisted structure, replay
histories with the domain admissibility rules, and consume the frozen dataset `causa-demo-v1`.

The frozen dataset lives in `kemiller2002/causa-data` at tag `causa-storage-v1.0.0`. That repository is a test-data and
conformance resource. It conforms to this contract and holds no normative requirement that is not also here.

## Documents and authority

| path | status | origin | purpose |
|---|---|---|---|
| [CONTRACT.md](CONTRACT.md) | **normative, frozen** | byte copy of `causa-data/contract/v1.0.0/CONTRACT.md` | wire conventions, layout, envelope, enums, payloads, projection, manifest, appends, provider rules, error stages, compatibility |
| [schemas/](schemas/) (60 `*.schema.json` + `bundle.json`) | **normative, frozen** | byte copies of `contract/v1.0.0/schemas/**`; relative `$ref` layout preserved | machine-readable structure; schema identity = SHA-256 of `schemas/bundle.json` |
| [FIELD-MAPPING.md](FIELD-MAPPING.md) | **normative, frozen** | byte copy | F# engine ↔ wire mapping for every payload field and enum |
| [golden/](golden/) | **normative, frozen** | byte copies of `contract/v1.0.0/golden/**` | expected projections of the five scenarios; informative scenario summary |
| [compatibility/lock.json](compatibility/lock.json), `RELEASE`, `engine.lock.json`, `global.json` | **normative, frozen** | byte copies | release lock: frozen file digests, expected state digests, negative codes, engine pin |
| [fixtures/manifest.json](fixtures/manifest.json) | **normative, frozen** | byte copy of `fixtures/v1.0.0/manifest.json` | dataset identity: digests of every investigation file and state |
| [compatibility/release-pin.json](compatibility/release-pin.json) | normative | written here | tag, commit and identity digests that the parity gate enforces |
| [SEMANTICS.md](SEMANTICS.md) | **normative** | written here from the pinned engine | domain admissibility: commands, decision rules, transition tables, gates, `History.validate`; closes CONTRACT §11.3's delegation to the engine |
| [INVARIANTS.md](INVARIANTS.md) | **normative** | written here | the 15 persistence invariants `CAUSA-INV-01…15` with enforcement and evidence |
| [compatibility/RULES.md](compatibility/RULES.md) | **normative** | consolidated here | change classes, reader/writer rules, accepted compatibility decisions, change procedure, parity gate |
| [FIELD-CATALOG.md](FIELD-CATALOG.md) | **normative, generated** | generated from `schemas/` + `catalog/field-semantics.json` + FIELD-MAPPING.md | every persisted field in every document: type, F# type, required, nullable, values, format, bounds, meaning, rule, default, version |
| [EVENT-CATALOG.md](EVENT-CATALOG.md) | **normative, generated** | generated from `schemas/` + `catalog/event-semantics.json` + CONTRACT.md §8.3 | every event discriminator with payload, references, projection effect and invariants |
| [catalog/](catalog/) | normative source | written here | semantic text the catalog generator joins to the schemas |
| [acceptance/ACCEPTANCE.md](acceptance/ACCEPTANCE.md), [acceptance/TEST-MATRIX.json](acceptance/TEST-MATRIX.json) | **normative** | written here | release gate (step 0 + TEST-MATRIX + supplementary rows) and consumer protocol |
| [acceptance/supplementary/](acceptance/supplementary/README.md) | **normative test data** | generated here against the reference oracle, cross-checked with the clean-room consumer | additional negative and valid histories covering decision rules the frozen negatives do not isolate |
| [CLARIFICATIONS.md](CLARIFICATIONS.md) | **normative** | written here | CAUSA-CLAR-01…14: command-boundary de-duplication, protocol meanings, write-path codes, local atomicity, dataset codes, byte-exact storage |
| [PROVIDER-GITHUB.md](PROVIDER-GITHUB.md) | **normative** | written here | GitHub provider: configuration, atomic append request sequence, status mapping, sync-state machine, conflict re-decision, provider gate AC-PROV-* |
| [catalog/artifact-url-vectors.jsonl](catalog/artifact-url-vectors.jsonl) | **normative test data** | generated with the .NET 10 engine predicate | expected verdicts for SEMANTICS §10.4.1–§10.4.2 (AC-URL-01) |

"Frozen" files MUST NOT be edited. Their bytes are verified against the tag by `tools/causa-contract/parity_gate.py`.
CONTRACT.md was written for the `causa-data` layout. Read its paths with this mapping:

| CONTRACT.md path | here |
|---|---|
| `contract/v1.0.0/CONTRACT.md`, `FIELD-MAPPING.md` | `CONTRACT.md`, `FIELD-MAPPING.md` |
| `contract/v1.0.0/schemas/…` | `schemas/…` |
| `contract/v1.0.0/golden/…` | `golden/…` |
| `compatibility/v1.0.0/lock.json`, `RELEASE` | `compatibility/lock.json`, `compatibility/RELEASE` |
| `engine.lock.json`, `global.json` | `compatibility/engine.lock.json`, `compatibility/global.json` |
| `fixtures/v1.0.0/manifest.json` | `fixtures/manifest.json` |
| `fixtures/v1.0.0/investigations/…`, `negative/…` | not imported (data); read from the tagged `causa-data` checkout |

## Precedence

1. CONTRACT.md and the schemas (frozen) define structure, encoding, layout and error stages. A disagreement between them
   is a contract defect, and readers treat the input as invalid (CONTRACT preamble).
2. SEMANTICS.md defines domain admissibility (stage 15) completely for `semanticVersion` 1.0.0 and is how these
   requirements satisfy CONTRACT §11.3. The pinned engine at `indy-init-rehearsal-001@55f49f6` is the reference
   oracle from which SEMANTICS.md was written and against which it is tested. It is evidence for defect reports, not a
   runtime or build-time dependency. A disagreement is a defect resolved by a reviewed change to SEMANTICS.md
   (SEMANTICS §0.3), never by consulting engine code.
3. INVARIANTS.md, compatibility/RULES.md and ACCEPTANCE.md consolidate and operationalize 1–2. They add no wire
   structure.
4. The catalogs are generated views of 1. If a catalog disagrees with a schema, regenerate it. A catalog never
   overrides a schema.
5. Documents under `docs/` that describe persistence are explanatory unless they point here. See the normative-status
   table in [docs/PERSISTENCE-CONTRACT.md](../../../../docs/PERSISTENCE-CONTRACT.md) and the reconciliation record.

## Tooling (non-normative)

```sh
python3 -I tools/causa-contract/generate_catalogs.py check [--dataset <causa-data checkout>]
python3 -I tools/causa-contract/parity_gate.py check    --causa-data <causa-data checkout>
python3 -I tools/causa-contract/parity_gate.py selftest --causa-data <causa-data checkout>
python3 -I tools/causa-contract/acceptance.py run --consumer "<cmd>" --causa-data <causa-data checkout at the tag>
```

The tools need Python 3.11+, `jsonschema` 4.x and `git`.
