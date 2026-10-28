# Requirement matrix: the 31 unverified P0 requirements

Generated from [requirement-matrix.json](requirement-matrix.json), which also holds full obligation text, source, test and evidence references, causes and confidence. Classifications are audit judgements made after the independent review; they are not acceptance results. Rehearsal 5 remains **NOT ACCEPTED** (verdict INCOMPLETE).

Legend: **A** implemented, verification missing; **B** partially implemented; **C** not implemented; **D** indeterminate; **E** process evidence missing (sub-status in brackets). Primary cause codes: 1 no implementation, 2 no test, 3 test not executed, 4 insufficient assertions, 5 evidence not collected, 6 evidence not bound, 7 wrong candidate, 9 process evidence never generated, 10 owner/reviewer action missing, 11 verification-plan defect, 12 other. Work sizes are relative (S/M/L); `-` means none. They are not hour estimates.

| Requirement | Class | Implemented | Verified | Missing evidence (open obligations, primary cause) | Primary cause | Remaining work (code / tests / evidence / plan / owner / review) |
|---|---|---|---|---|---|---|
| AR-005 — Capability registry and reuse | E (unknown) | n/a (process) | no | AR-005-O1 (9), AR-005-O2 (9) | 9 | - / - / M / S / - / S |
| AR-008 — Adversarial independent review | E (unknown) | n/a (process) | no | AR-008-O1 (11), AR-008-O2 (11) | 11 | - / - / S / S / - / M |
| AR-011 — Reproducibility and provenance | B | partial | partial (O1 PASS) | AR-011-O2 (1) | 1 | S / S / S / - / - / - |
| AR-012 — Controlled work scope | E (unknown) | n/a (process) | no | AR-012-O1 (9), AR-012-O2 (9) | 9 | - / - / M / S / - / - |
| AR-015 — Long-running agent continuity | E (not performed) | n/a (process) | no | AR-015-O1 (9) | 9 | - / - / M / S / - / - |
| AR-017 — Ownership and intake | E (unknown) | n/a (process) | no | AR-017-O1 (9), AR-017-O2 (9) | 9 | - / - / S / S / - / - |
| AR-019 — Computed verdict, not agent assertion | B | partial (R2 clause met; R3 clause not met) | partial | AR-019-O3 (1) | 1 | S / S / S / S / S / - |
| AR-020 — Selective enforcement | E (unknown) | n/a (process) | no | AR-020-O1 (11), AR-020-O2 (11), AR-020-O3 (11) | 11 | - / - / S / S / - / S |
| AR-021 — Continuous implementation agent | E (not performed) | n/a (process) | no | AR-021-O1 (12), AR-021-O2 (11), AR-021-O3 (9) | 12 | - / - / M / S / S / S |
| AR-023 — Release-set integrity | E (performed evidence inadequate) | n/a (process) | partial (O1, O2, O4 PASS) | AR-023-O3 (5) | 5 | - / - / S / S / - / - |
| AR-024 — Explicit kickoff decisions | E (unknown) | n/a (process) | no | AR-024-O1 (10), AR-024-O2 (10) | 10 | - / - / S / - / S / - |
| EXT-DATA-005  | B | partial (no repository/branch permission check, by owner decision DEC-025) | O2 PASS; O1 path/manifest/admissibility checks verified post-rehearsal, permission clause absent | EXT-DATA-005-O1 (11) | 11 | S / S / S / S / S / - |
| EXT-DATA-006  | A | yes | tests ran and passed at the candidate; unbound | EXT-DATA-006-O2 (6) | 6 | - / - / S / - / - / - |
| PF-005 — Provider isolation | A | yes | post-rehearsal only (O1); O2 PASS | PF-005-O1 (2) | 2 | - / S / S / - / - / - |
| PF-009 — Concurrency fixtures | A | yes | post-rehearsal only (O3); O1, O2 PASS | PF-009-O3 (4) | 4 | - / - / S / - / - / - |
| PF-014 — Live integration acceptance | D | harness yes; deployed live run not executed | partial (O1, O3, O4 PASS) | PF-014-O2 (3) | 3 | - / S / S / - / - / - |
| PF-015 — Credentials and security | B | partial | partial (O3, O4 PASS) | PF-015-O1 (11), PF-015-O2 (2) | 11 | M / S / S / S / S / - |
| PF-019 — Local-first workflow | E (unknown) | n/a (process) | partial (O2 PASS) | PF-019-O1 (9) | 9 | - / - / S / S / - / - |
| UI-011 — Keyboard, accessibility and iPad usability | B | partial (O4) | O1-O3, O5 PASS | UI-011-O4 (1) | 1 | S / S / S / - / - / - |
| UI-015 — Acceptance and traceability | D | elements implemented; deployed exact-SHA run missing | O1, O4 PASS | UI-015-O2 (11), UI-015-O3 (3) | 11 | - / S / S / S / - / - |
| UI-016 — Causa semantic color system | B | partial (non-text indicator contrast) | 15 of 17 obligations PASS | UI-016-O11 (1), UI-016-O15 (10) | 10 | S / S / S / - / S / S |
| UI-021 — Content and route verification | E (performed evidence inadequate) | yes (O1, O2 PASS) | application behaviour PASS | UI-021-O3 (11) | 11 | - / - / S / S / - / - |
| UI-030 — Presentation semantics verification and adversarial tests | E (performed evidence inadequate) | yes (O1, O2, O4 PASS) | application behaviour PASS | UI-030-O3 (11) | 11 | - / - / S / S / - / S |
| UI-031 — Preserve system detail without exposing it in the workflow | B | partial (no role/access control) | O1-O4 PASS | UI-031-O5 (1) | 1 | M / S / S / - / S / - |
| UI-040 — End-to-end acceptance | E (performed evidence inadequate) | yes (O1 PASS) | application behaviour PASS | UI-040-O2 (11) | 11 | - / - / S / S / - / - |
| VER-001 — Latest compatible verified release | E (unknown) | n/a (process) | no | VER-001-O1 (11), VER-001-O2 (11), VER-001-O3 (11) | 11 | - / - / S / S / - / - |
| VER-002 — Verify release freshness | E (performed evidence inadequate) | n/a (process) | no | VER-002-O1 (5), VER-002-O2 (9), VER-002-O3 (11) | 11 | - / - / S / S / - / - |
| VER-003 — Compatibility over blind upgrades | E (unknown) | n/a (process) | partial (O3 PASS) | VER-003-O1 (11), VER-003-O2 (11) | 11 | - / - / S / S / S / - |
| VER-004 — Reproducible lock | E (performed evidence inadequate) | n/a (process) | partial (O1, O2 PASS) | VER-004-O3 (5) | 5 | - / - / S / S / - / - |
| VER-010 — Requirements traceability | E (unknown) | n/a (process) | no | VER-010-O1 (11), VER-010-O2 (9) | 11 | - / - / S / S / - / - |
| WDL-014 — Integration gate | D | yes at the local candidate build; deployed exact-SHA run missing | local only; deployed post-rehearsal at b972cb4 | WDL-014-O1 (3) | 3 | - / S / S / - / - / - |

Totals: 77 verified during the rehearsal, 3 A, 7 B, 0 C, 3 D, 18 E = 108. Code is product code, or acceptance-tooling code for AR-011 and AR-019.
