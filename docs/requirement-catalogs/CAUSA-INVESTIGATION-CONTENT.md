# Causa investigation content: catalog additions INV-019 … INV-038

Ratified into [`requirements/catalogs/causa-investigation.requirement-catalog.json`](../../requirements/catalogs/causa-investigation.requirement-catalog.json)
(`CAT-CAUSA-INVESTIGATION`, schema `praxis.requirement-catalog/1`). They were drafted as `CAT-CAUSA-INVESTIGATION-CONTENT`.

**Status: ratified** by the owner on 2026-11-03 (commit `7cefdc6`, Indy-init#98). An agent drafted it under the owner's decisions OD-INDY-19..21 and OD-INDY-23 (`docs/OWNER-DECISIONS-2026-11-03.md`).
- It added INV-019 to INV-038 to `CAT-CAUSA-INVESTIGATION` (INV-001 to INV-018), continuing its numbering, and changed no earlier entry.
- The entries now live in `requirements/catalogs/causa-investigation.requirement-catalog.json`. The proposed file they were drafted in was retired at ratification.

## The ten content gaps (OD-INDY-20) and the catalog's open questions (OD-INDY-21)

| Gap or question | Requirements | Authoritative sources |
|---|---|---|
| (1) Observation text | INV-024 Observation text (required, 1–10,000); INV-025 Observation source reference | R1.1, R1.2, D-001, R3.2 |
| (2) Hypothesis statement | INV-026 Hypothesis statement (required, 1–10,000) | R2.1, S03, D-001 |
| (3) Evidence label, source reference and URL | INV-027 Evidence label (required, 1–200); INV-028 Evidence source reference; INV-029 Evidence source URL (`^https?://`, refused otherwise) | R3.1, R3.2, R3.5, S05, UI-005 |
| (4) Evidence-to-hypothesis links | INV-030 Link evidence to a hypothesis (one current assessment per pair; reassess and withdraw keep history); INV-031 relationship kind (`supports`, `contradicts`, `consistent-with`, `inconclusive`; no default); INV-038 required rationale (OD-INDY-23) | R3.3, R3.4, UI-005, OD-INDY-23 |
| (5) Conclusions | INV-032 Record a conclusion (a snapshot; resolution gates unchanged); INV-033 Conclusion statement | R6.1, R6.4, STATE-TRANSITION-TABLES§14-conclusion-behavior, D-001 |
| (6) Editing keeps identity and history | INV-034 Edit investigation details; INV-035 Editing keeps identity and history | R1.3, R2.4, R15.4, UI-005 |
| (7) Primary investigative question | INV-023 (optional, at most 10,000) | OD-INDY-20 |
| (8) Validation rules; open questions 1–2 | INV-019 Problem statement must not be empty; INV-020 Title at most 200; INV-021 Problem statement at most 10,000; plus the `minLength`/`maxLength`/`pattern`/`options` of INV-023 to INV-033 | S01, R1.1, OD-INDY-21 |
| (9) Cancel; open question 3 | INV-022 Cancel investigation start; INV-036 Cancel a content editor (both write nothing) | OD-INDY-21 |
| (10) Live round trip | INV-037 All content survives a reload from GitHub in a new browser context (`isolation`) | R10.7, PERSIST-021, PERSIST-049 |

**What the catalog already had** (ratified, reused, not repeated):
- INV-001/INV-002: problem statement and title, entered and saved;
- INV-006/INV-007: identifier and creation time;
- INV-008/INV-017: the original statement after a reload;
- INV-009 to INV-011: hypotheses and hypothesis explanation;
- INV-012/INV-013: evidence original content;
- INV-014: record an observation;
- INV-015/INV-016: save and save state;
- INV-018: investigation state.

## Validation (Praxis `main` ddfcbbe, built from source for validation only; unreleased)

| Catalog | `praxis requirements manifest compile` | Result |
|---|---|---|
| Ratified `CAT-CAUSA-INVESTIGATION` alone | exit 0 | 18 requirements, 90 obligations (V5 lifecycle 6) |
| Ratified + proposed (merge preview) | exit 0 | 37 requirements, 239 obligations (V5 lifecycle 41) |
| Merge preview with `--previous` the ratified manifest | exit 0 | no ID dropped or renamed |
| Proposed file alone | exit 3 | rejected only for its references to INV-001 … INV-018, as expected for an addition |

The logs are in `docs/rehearsal-009-content-readiness/evidence/praxis-catalog-compile-*.log`. Every cited baseline requirement ID resolves in `requirements/causa-requirements-baseline-v4.json`.
