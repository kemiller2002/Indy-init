# Persistence and compatibility patterns

Authoritative sources, which this file does not restate:
- `docs/CAUSA-DATA-CONTRACT-v1.md` and `docs/CAUSA-STORAGE-RELEASE-v1.0.0.md` (the wire format);
- `requirements-compiled/v4.1.5` (E1 events, W1 workspace, C1 create; `state-machine-contracts.json` SM-SAVE; `analysis/github-error-matrix.json`);
- `proposals/r10-historical-data-compatibility` (`EVENT-COMPATIBILITY-MATRIX.json`, `HISTORICAL-COVERAGE.json`, `REPLAY-VERIFICATION.md`);
- `docs/rehearsal-007/PERSISTENCE-ARCHITECTURE-DECISION.md`.

Persistence is the most recurrent failure family in the record. It was found in seven rehearsals or experiments (FM-09: HF-006, HF-021, HF-026, HF-039, HF-040, HF-074; FM-10: HF-008, HF-041; FM-11: HF-068, HF-069). Each fix closed one variant. What was missing is one definition that every variant is checked against.

## Rules

| ID | Rule | Level | Belongs in | Evidence |
|---|---|---|---|---|
| P-01 | **Durable = read back.** A change is Saved only when a provider read-back returns the blob SHA that was written. Nothing restored from browser storage is ever shown as "Saved to GitHub". | MANDATORY (CV-09) | Causa contract (SM-SAVE) | HF-040, HF-008 |
| P-02 | **Read-back bypasses HTTP caches.** `cache: no-store`, or a conditional request with a fresh ETag. Without that, GitHub's `Cache-Control: private, max-age=60` serves stale reads. | MANDATORY (CV-09) | Reusable infrastructure (Limen transport) | HF-074 (`540d7ff`, PR #129) |
| P-03 | **Doubles carry recorded provider behaviour.** A double replays recorded real responses, including Cache-Control, ETag, pagination (`compare` past 250 commits) and the 1 MB contents limit. A hand-written double is a unit-test aid only. | MANDATORY (CV-09) | Reusable infrastructure (provider fixtures) | HF-074, HF-039 |
| P-04 | **Mock success is never durability evidence.** An obligation about durability is verified only by a real-provider run on the candidate SHA. | MANDATORY (CV-09, CV-15) | Dokimos evidence categories | HF-005, HF-074 |
| P-05 | **Every stored event decodes or is preserved.** A known kind decodes to its case. An unknown kind is preserved opaquely and round-trips; it is never defaulted to another kind or dropped. | MANDATORY (CV-11, CV-10) | Causa contract (v4.1.5 E1) | HF-068, HF-041 |
| P-06 | **Versioned decoding.** Every envelope carries the schema version. Decoders are total over the supported versions and refuse a newer major version with a typed error. | MANDATORY | Causa contract | causa-storage v1.0.0 |
| P-07 | **Optional fields are `option` in F#.** A missing optional field decodes to `None`. A missing required field is a typed decode error, never a default value. | PREFERRED | Causa contract | HF-022 (two codecs with different defaults) |
| P-08 | **Identifiers.** Investigation and event ids are generated once, client-side, in the domain function that creates them. They are stable across retries and never derived from counters local to a page. | MANDATORY | Causa contract | HF-021 (per-page counter write marker) |
| P-09 | **Persistence errors are typed.** Provider failures go through the `Classifier` into `ProviderError`. Offline waits with backoff and shows "Waiting for the connection", never "Saving". | MANDATORY | Causa contract + infrastructure | HF-026 (151 POSTs in 60 s) |
| P-10 | **In-memory vs durable is visible in the type.** Device-only state and provider-confirmed state are distinct types, so the UI cannot render one as the other. | PREFERRED | Causa contract (IF41-VIEW `SaveIndicatorView`) | HF-040 |
| P-11 | **Real-provider acceptance runs before the candidate.** The live-provider preflight (J3) runs on the practice repository before T0 and on the candidate. | MANDATORY (exists) | Governed verification | `tools/requirements-compiler/tests/live-provider-preflight.yml` |

## What belongs where

- **Causa contracts (domain-specific):** P-01, P-05, P-06, P-08, P-09 (the cases) and P-10. v4.1.5 already carries P-05 (E1) and the workspace address (W1). P-01 and P-09 are already SM-SAVE behaviour. The convention adds the rule that the indicator is driven only by a confirmed read-back.
- **Reusable infrastructure (Limen transport and provider fixtures):** P-02, P-03 and the backoff part of P-09. These should not be re-implemented per application. Proposed owner: Limen (transport) and Causa testing (recorded fixtures).
- **Verification (Dokimos):** P-04. Real-provider and double evidence must be distinct categories.

## Enforcement

| Rule | Earliest inexpensive check | Status |
|---|---|---|
| P-01, P-09 | SM-SAVE state-machine tests (every pair) | existing |
| P-02 | J3-05 live preflight; a double that replays `Cache-Control: max-age=60` makes a non-bypassing read fail in CI | live: existing; recorded-header double: proposed (CV-09 required test) |
| P-03 | Fixture provenance: each double response file names the real run it was recorded from | proposed, review obligation |
| P-04 | Evidence category in the obligation record; reviewer confirms real-provider runs for durability obligations | review obligation |
| P-05, P-06 | Replay coverage over the pinned causa-data histories: 0 unrepresentable events; a negative control removes a case | existing in the historical-compatibility proposal; becomes a package-release condition (CV-11) |
| P-08 | Domain unit test: two creates in the same tick give distinct ids; a retry keeps its id | proposed |
