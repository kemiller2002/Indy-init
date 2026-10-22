# Frozen Demo Data and Storage Compatibility Requirements

Status: normative planning requirement for Indy Init R002 and competition.
Scope: GitHub-backed Root-Cause Investigator storage and synthetic demonstration data.

## STORAGE-001: Immutable versioned storage contract
Before seeding demo data, select and publish a canonical versioned storage contract including repository paths, manifest, event envelope, event ordering, identifier formats, field names/types, null semantics, timestamps, snapshot rules, and serialization. Freeze version 1.0 for all rehearsal and competition runs. Application builds MUST NOT silently change it.

## STORAGE-002: Cross-run compatibility
Every new application build MUST read the same frozen dataset and reconstruct equivalent domain state by replaying authoritative events. Tests MUST compare canonical state and semantic digests, not merely successful JSON parsing. A fresh build MUST read/write/reload against the same contract without rewriting seed data.

## STORAGE-003: Schema evolution
Changes to the frozen contract require an explicit new schema version, documented rationale, reviewed migration, forward/backward compatibility matrix, and test fixtures for both versions. No in-place migration or destructive rewrite of the frozen baseline. Incompatible change requires explicit authorization. An agent MUST fail the acceptance gate on unapproved schema drift.

## STORAGE-004: Repository isolation
Proposed personal-account datastore: kemiller2002/indy-init-investigation-data (private initially). Keep immutable seed fixtures in a tagged commit and mutable demo writes in a separate branch/path. Never write secrets or personal data to fixtures. Do not store GitHub tokens.

## STORAGE-005: Seed scenarios
Prepare deterministic synthetic investigations for: payment failure (competing hypotheses), intermittent outage (timeline), conflicting metrics (blocked contradiction), API latency (verification), authentication outage (resolved report). Include one deliberately invalid fixture excluded from the live index to demonstrate refusal.

## STORAGE-006: Integrity and concurrency
Record fixture manifest and checksums. Verify append-only semantic history, aggregate-version ordering, duplicate/gap detection, expected-version conflict refusal, and atomic semantic transition persistence. Snapshots are derived; provider metadata is separate from domain meaning.

## STORAGE-007: Release acceptance
For every R002 and competition candidate: verify schema version/hash; load all valid frozen fixtures; replay histories through F# engine; compare expected projections; perform create/update/reload and conflict tests against disposable demo workspace; confirm seed hash unchanged. Any unexpected schema change, silent rewrite, failed replay, or invalid fixture accepted as valid = FAIL. Missing live API test = BLOCKED/UNKNOWN, never PASS.

## STORAGE-008: Competition boundary
Confirm organizer permission for pre-created datastore and test data. If disallowed, retain specification and a reproducible seeding procedure for execution after kickoff; do not import application-specific implementation code into the blank competition repo.

## STORAGE-009: Ownership
The application storage adapter owns GitHub paths and provider tokens; the F# domain model owns semantic validity. The frozen wire contract is independent of Forma, Limen, and UI release versions.

## Implementation work
Agent must inspect docs/PERSISTENCE-CONTRACT.md and actual rehearsal serialization before freezing version 1.0. Existing example physical layouts in that document are reversible proposals, not yet a committed frozen schema. Resolve that explicitly and record the decision. Place executable implementation work into each target repository's documented Praxis input folder, never GitHub Issues.

## Required evidence
- STORAGE-CONTRACT-v1.0.md and machine-readable schema
- Fixture manifest and checksums
- Schema compatibility test matrix
- Replay/projection golden results
- GitHub read/write/conflict/reload evidence
- Explicit release gate result for each run
