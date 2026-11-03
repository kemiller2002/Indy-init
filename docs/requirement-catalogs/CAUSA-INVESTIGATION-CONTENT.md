# Causa investigation content: catalog additions INV-019 to INV-037

Entries INV-019 to INV-037 of [`requirements/catalogs/causa-investigation.requirement-catalog.json`](../../requirements/catalogs/causa-investigation.requirement-catalog.json) (`CAT-CAUSA-INVESTIGATION`, schema `praxis.requirement-catalog/1`).

**Status: ratified when the owner merges the pull request that adds these entries to `requirements/catalogs/causa-investigation.requirement-catalog.json`** (OD-INDY-19..21, `docs/OWNER-DECISIONS-2026-11-03.md`).
- INV-019 to INV-037 continue the ratified catalog `CAT-CAUSA-INVESTIGATION` (INV-001 to INV-018). No earlier entry changes.
- The separate proposed file is removed in that same pull request.
- An agent prepared the entries. The owner's merge is the ratification; no agent merges it.

## The ten content gaps (OD-INDY-20) and the catalog's open questions (OD-INDY-21)

| Gap or question | Requirements | Authoritative sources |
|---|---|---|
| (1) Observation text | INV-024 Observation text (required, 1–10,000); INV-025 Observation source reference | R1.1, R1.2, D-001, R3.2 |
| (2) Hypothesis statement | INV-026 Hypothesis statement (required, 1–10,000) | R2.1, S03, D-001 |
| (3) Evidence label, source reference and URL | INV-027 Evidence label (required, 1–200); INV-028 Evidence source reference; INV-029 Evidence source URL (`^https?://`, refused otherwise) | R3.1, R3.2, R3.5, S05, UI-005 |
| (4) Evidence-to-hypothesis links | INV-030 Link evidence to a hypothesis; INV-031 relationship kind (`supports`, `contradicts`, `relevant`) | R3.3, R3.4, UI-005 |
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
