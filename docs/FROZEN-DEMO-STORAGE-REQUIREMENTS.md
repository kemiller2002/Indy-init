# Frozen Demo Data and Storage Compatibility Requirements

~~Canonical candidate wire fields, types, paths, event payloads and freeze gates: [CAUSA-DATA-CONTRACT-v1.md](CAUSA-DATA-CONTRACT-v1.md).~~ ***Superseded by frozen contract v1.0.0:** normative wire fields, types, paths and event payloads are in [requirements/causa/data/v1.0.0/CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md) (imported from tag `causa-storage-v1.0.0`); CAUSA-DATA-CONTRACT-v1.md is the superseded candidate.* The v1.0.0 contract is **frozen**: release identity, digests and tag status are in [CAUSA-STORAGE-RELEASE-v1.0.0.md](CAUSA-STORAGE-RELEASE-v1.0.0.md). The normative text is [requirements/causa/data/v1.0.0/CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md) (byte copy of `causa-data/contract/v1.0.0/CONTRACT.md`).

Status: normative planning requirement for Indy Init R002 and competition.
Scope: GitHub-backed Root-Cause Investigator storage and synthetic demonstration data.

> **Normative status.** **Normative** for the storage requirements STORAGE-001…009 (what must be true); **not** a wire specification. Persisted data is governed only by the frozen Causa storage contract v1.0.0 ([README.md](../requirements/causa/data/v1.0.0/README.md), [FIELD-CATALOG.md](../requirements/causa/data/v1.0.0/FIELD-CATALOG.md), [EVENT-CATALOG.md](../requirements/causa/data/v1.0.0/EVENT-CATALOG.md)). Where this document states a persisted field, event name, enum value, layout, hash or concurrency rule differently, the contract governs; such statements are marked *Superseded by frozen contract v1.0.0* in place. STORAGE-001…009 are satisfied for v1.0.0 by release `causa-storage-v1.0.0`; acceptance is [acceptance/ACCEPTANCE.md](../requirements/causa/data/v1.0.0/acceptance/ACCEPTANCE.md) and [acceptance/TEST-MATRIX.json](../requirements/causa/data/v1.0.0/acceptance/TEST-MATRIX.json).

## STORAGE-001: Immutable versioned storage contract
Before seeding demo data, select and publish a canonical versioned storage contract including repository paths, manifest, event envelope, event ordering, identifier formats, field names/types, null semantics, timestamps, snapshot rules, and serialization. Freeze version 1.0 for all rehearsal and competition runs. Application builds MUST NOT silently change it. *(Satisfied by `causa-storage-v1.0.0`, contract version `1.0.0`.)*

## STORAGE-002: Cross-run compatibility
Every new application build MUST read the same frozen dataset and reconstruct equivalent domain state by replaying authoritative events. Tests MUST compare canonical state and semantic digests, not merely successful JSON parsing. A fresh build MUST read/write/reload against the same contract without rewriting seed data. *(Criterion: identical `expectedStateSha256` for every frozen scenario, CONTRACT §8.4 and `compatibility/lock.json` `expectedStates`.)*

## STORAGE-003: Schema evolution
Changes to the frozen contract require an explicit new schema version, documented rationale, reviewed migration, forward/backward compatibility matrix, and test fixtures for both versions. No in-place migration or destructive rewrite of the frozen baseline. Incompatible change requires explicit authorization. An agent MUST fail the acceptance gate on unapproved schema drift. *(Rules: [compatibility/RULES.md](../requirements/causa/data/v1.0.0/compatibility/RULES.md).)*

## STORAGE-004: Repository isolation
Proposed personal-account datastore: kemiller2002/causa-data (private initially). Keep immutable seed fixtures in a tagged commit and mutable demo writes in a separate branch/path. Never write secrets or personal data to fixtures. Do not store GitHub tokens. *(Mutable writes go to `workspaces/demo/<runId>/`, CONTRACT §3. **Decision U-02 (2026-10-23): `causa-data` stays public**; every file is synthetic and the no-secrets/no-personal-data rule is binding. See [CAUSA-CONTRACT-RECONCILIATION.md](CAUSA-CONTRACT-RECONCILIATION.md#owner-decisions).)*

## STORAGE-005: Seed scenarios
Prepare deterministic synthetic investigations for: payment failure (competing hypotheses), intermittent outage (timeline), conflicting metrics (blocked contradiction), API latency (verification), authentication outage (resolved report). Include one deliberately invalid fixture excluded from the live index to demonstrate refusal.

## STORAGE-006: Integrity and concurrency
Record fixture manifest and checksums. Verify append-only semantic history, aggregate-version ordering, duplicate/gap detection, expected-version conflict refusal, and atomic semantic transition persistence. Snapshots are derived; provider metadata is separate from domain meaning.

## STORAGE-007: Release acceptance
For every R002 and competition candidate: verify schema version/hash; load all valid frozen fixtures; replay histories through F# engine; compare expected projections; perform create/update/reload and conflict tests against disposable demo workspace; confirm seed hash unchanged. Any unexpected schema change, silent rewrite, failed replay, or invalid fixture accepted as valid = FAIL. Missing live API test = BLOCKED/UNKNOWN, never PASS. *(Executable form, normative: the storage acceptance gate [acceptance/ACCEPTANCE.md](../requirements/causa/data/v1.0.0/acceptance/ACCEPTANCE.md) (consumer protocol, frozen and supplementary fixtures, URL vectors) **and** the provider gate AC-PROV-* in [PROVIDER-GITHUB.md](../requirements/causa/data/v1.0.0/PROVIDER-GITHUB.md) for any build with a GitHub adapter. "Replay histories through F# engine" means any implementation of SEMANTICS.md conforming by SEMANTICS §0.2 (CAUSA-CLAR-14). In causa-data, `causa gate . --anchor causa-storage-v1.0.0` remains the dataset-side gate.)*

## STORAGE-008: Competition boundary
Confirm organizer permission for pre-created datastore and test data. If disallowed, retain specification and a reproducible seeding procedure for execution after kickoff; do not import application-specific implementation code into the blank competition repo. *(U-01, 2026-10-23: pending the organizers; recommended path recorded in [CAUSA-CONTRACT-RECONCILIATION.md](CAUSA-CONTRACT-RECONCILIATION.md#owner-decisions). The deterministic generator reproduces dataset identity `f650a2e509a420872b46177b21f57ef6ebd7625ae4c69dd92198d3ca472c2b86`.)*

## STORAGE-009: Ownership
The application storage adapter owns GitHub paths and provider tokens; the F# domain model owns semantic validity. The frozen wire contract is independent of Forma, Limen, and UI release versions. *(Semantic oracle: pinned engine `indy-init-rehearsal-001@55f49f6`, specified in [SEMANTICS.md](../requirements/causa/data/v1.0.0/SEMANTICS.md).)*

## Implementation work
~~Agent must inspect docs/PERSISTENCE-CONTRACT.md and actual rehearsal serialization before freezing version 1.0. Existing example physical layouts in that document are reversible proposals, not yet a committed frozen schema. Resolve that explicitly and record the decision.~~ ***Superseded by frozen contract v1.0.0:** resolved: the v1.0.0 layout is CONTRACT §3 (decision CD-LAY-01); the layouts in PERSISTENCE-CONTRACT.md §2 and §11 are non-normative for v1.0.0 data.* Place executable implementation work into each target repository's documented Praxis input folder, never GitHub Issues.

## Required evidence
- ~~STORAGE-CONTRACT-v1.0.md and machine-readable schema~~ [CONTRACT.md](../requirements/causa/data/v1.0.0/CONTRACT.md) and [schemas/](../requirements/causa/data/v1.0.0/schemas/) (identity: SHA-256 of `schemas/bundle.json`)
- Fixture manifest and checksums ([fixtures/manifest.json](../requirements/causa/data/v1.0.0/fixtures/manifest.json))
- Schema compatibility test matrix ([acceptance/TEST-MATRIX.json](../requirements/causa/data/v1.0.0/acceptance/TEST-MATRIX.json), [compatibility/lock.json](../requirements/causa/data/v1.0.0/compatibility/lock.json))
- Replay/projection golden results ([golden/](../requirements/causa/data/v1.0.0/golden/))
- GitHub read/write/conflict/reload evidence
- Explicit release gate result for each run
