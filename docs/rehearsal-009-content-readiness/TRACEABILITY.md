# Rehearsal 009 content readiness: traceability

> **Superseded in part by the owner's decision OD-INDY-19 (2026-11-03, `docs/OWNER-DECISIONS-2026-11-03.md`).**
> Content is specified in the requirement catalog, not in a v4.1.3 package. The proposed CONTENT-* obligations and ODR-V413-01..03 are withdrawn without being recorded.
> Their substance is now the proposed catalog additions INV-019 … INV-037 (`docs/requirement-catalogs/CAUSA-INVESTIGATION-CONTENT.md`).
> The mapping is: CONTENT-001 → INV-001/002/019–022 (ratified INV-001, INV-002); CONTENT-002 → INV-034; CONTENT-003 → INV-024/025; CONTENT-004 → INV-026; CONTENT-005 → INV-027–029 (with ratified INV-012); CONTENT-006 → INV-030/031; CONTENT-007 → INV-032/033; CONTENT-008 → INV-035; CONTENT-009 → INV-037; CONTENT-010 → INV-019–021 and the field rules; CONTENT-011/012 → the failure and mobile scenarios of E2E-VERIFICATION.md.
> The analysis below is kept as the record of why.


**Requirement IDs.** These are the frozen baseline's obligation IDs (`requirements/causa-requirements-baseline-v4.json`). Every ID in this file is in the baseline, and no ID was invented.
- **"P0 plan"** means the obligation is in `requirements-compiled/v4/verification-plan.json` (P0 only, ODR-V4-10 A).
- **"BL only"** means it is in the baseline with no criterion and no bound test.
- **Code references** are to `kemiller2002/indy-init-rehearsal-008` at `fd8fba9`.
- **Proposed IDs.** The CONTENT-* column names the proposed successor requirement that makes each capability verifiable. These IDs are **proposed**, not frozen (`proposed/content-obligations.proposed.json`).
- **Checked by machine.** All 49 baseline obligation IDs cited by the proposed CONTENT-* obligations resolve in the baseline. See the check in FINAL-READINESS.md §Validation.

**Gap codes:** SPEC = specification gap, IMPL = implementation gap, VERIF = verification gap, INTEG = integration gap, USAB = usability gap.

| Capability | Requirement ID (status) | Explicitly specified? | Implemented in 008? | Executably verified? | Gap | Proposed |
|---|---|---|---|---|---|---|
| Investigation creation | R1.1-O1, S01-O2, S01-O9, S01-O10 (BL only); EXT-DATA-006-O2, EXT-DATA-008-O2 (P0 plan) | Partial. S01 names the screen and its fields; EXT-DATA-008-O2 covers only an **empty** investigation. | Partial: `StartInvestigation` without payload (`src/Causa.App/Update.fs:614-618`) | Only the empty round trip (`dotnet:…Live.EXTDATA008Tests`) | SPEC, VERIF | CONTENT-001 |
| Stable identity separate from the name | R1.3-O1 (BL only); UI-001-O6, UI-013-O1 (P0 plan) | Identity: yes. Name versus identity: no. | Identity yes; there is no name (`src/Causa.Domain/Entities.fs`) | UI-001/UI-013 deterministic fixtures | SPEC | CONTENT-001, CONTENT-002 |
| Name entry | S01-O7 "optional title" (BL only); UI-001-O2 shows the title (P0 plan) | Entry: BL only. Display: P0. | No: the title is the route title (`src/Causa.App/Projections.fs:230-249`) | Display against fixtures only | SPEC, IMPL, VERIF | CONTENT-001 |
| Description | R1.1-O1, S01-O6 "original problem statement field" (BL only) | Partial | No | No | SPEC, IMPL, VERIF | CONTENT-001 |
| Primary investigative question | none (S01-O3/O4 "Primary question" is the screen's design question) | No | No | No | SPEC, IMPL | CONTENT-001 |
| Editing name or description | none (RenameInvestigation is only in the background COMMAND-EVENT-CATALOG) | No | No: `RenameInvestigation` has no payload (`Commands.fs`) | No | SPEC, IMPL | CONTENT-002 |
| Observation entry and reading | R1.1-O1, R1.2-O1, S01-O16 "original text preserved", D-001-O1 (BL only) | Partial | No: `RecordObservation` has no payload; lists show "Observation n (version v)" (`Views/Investigation.fs:64-65, 193`) | Labels only (`tests/browser/contract/report-ui-2.spec.js:103`) | SPEC, IMPL, VERIF, USAB | CONTENT-003 |
| Hypothesis entry | R2.1-O1–R2.5-O1, S03-O6 "statement", R15.5-O1 (BL only); UI-029-O2 tab (P0 plan) | Partial | No: no payload | Tab presence only | SPEC, IMPL, VERIF | CONTENT-004 |
| Hypothesis distinct from observation and conclusion | D-001-O1 (BL only) | Yes, semantically | Only by label | No | VERIF | CONTENT-004 |
| Evidence item | R3.1-O1, R3.2-O1, S05-O2/O4/O11/O14 (BL only); UI-005-O1 viewing (P0 plan) | Capture: BL only. Viewing: P0. There is no title or description field anywhere. | No: "Evidence items are not part of this version" (`Views/Investigation.fs:196`) | Viewing against fixtures (`playwright:acceptance/UI-005.spec.js`) | SPEC, IMPL, VERIF | CONTENT-005 |
| Source reference and URL | R3.5-O1, S05-O6 (BL only); UI-005-O3 sanitise, UI-017-O5, UI-031-O3 (P0 plan) | Partial | No | Sanitising against fixtures | SPEC, IMPL | CONTENT-005, CONTENT-010 |
| Evidence–hypothesis relationship | R3.3-O1, R3.4-O1 (BL only); UI-005-O1, UI-033-O1, UI-038-O1 viewing (P0 plan) | Partial: the kinds are named (support, contradiction…), but no user-creates-link criterion | No | Viewing against fixtures | SPEC, IMPL, VERIF | CONTENT-006 |
| Conclusion entry | R6.1-O1, R6.4-O1, STATE-TRANSITION-TABLES§14-conclusion-behavior-O1 (BL only); UI-025-O4 (P0 plan) | No entry requirement; only "must not invent" | No: `RecordConclusion` has no payload | No | SPEC, IMPL | CONTENT-007 |
| Editing preserves history | R2.4-O1, R15.4-O1, STATE-TRANSITION-TABLES§4-evidence-lifecycle-O9 (BL only); UI-005-O2 (P0 plan) | Preservation: yes. Revising text: no. | No | No | SPEC, IMPL | CONTENT-008 |
| GitHub persistence of content | R10.1-O1 (BL only); PERSIST-005-O1, PERSIST-006-O2, PERSIST-007-O1 (P0 plan) | The protocol: yes. Content: never named. | Event kinds only (`src/Causa.Domain/Identity.fs:14`) | Protocol: yes (live) | SPEC, INTEG | CONTENT-009 |
| Reload and reconstruction | PERSIST-021-O1/O3, PERSIST-049-O1/O2, PERSIST-020-O5, EXT-DATA-009-O1 (P0 plan); R10.7-O2 (BL only) | "Every confirmed change", with change undefined as content | Kinds reload correctly | Yes for saves, not for content | SPEC, VERIF | CONTENT-009 |
| Mobile usability | R9.1-O1, R9.2-O1 (BL only); UI-011-O2, UI-004-O5 (P0 plan) | Yes, generically | Layouts yes; no content forms | Layout assertions at narrow widths | VERIF, USAB | CONTENT-012 |
| Data validation | UI-004-O3, UI-004-O4, UI-005-O3 (P0 plan) | Generic "validated forms" only; no rules | n/a (no forms) | Generic | SPEC | CONTENT-010 |
| Failure states | UI-024-O4, PERSIST-018-O1, PERSIST-020-O5 (P0 plan) | Yes, for saves | Yes, for saves | Yes, for saves | (content not covered) | CONTENT-011 |
| Provenance | R3.2-O1, D-002-O1 (BL only); UI-003-O1, UI-005-O1, UI-038-O2 (P0 plan) | Yes | Event actor and time; no content provenance | History view against fixtures | VERIF | CONTENT-001…009 (Provenance on every payload) |
| Accessibility | R9.3-O1 (BL only); UI-011-O1–O5, UI-016-O11, UI-004-O5 (P0 plan) | Yes | Yes, for existing controls | Yes (UI-011, INV-24) | (content forms not covered) | CONTENT-012 |
| End-to-end workflow | UI-040-O1, UI-015-O2 (P0 plan); KICKOFF-EXECUTION-PACKET§7-checkpoints-O2/O5, R16.2-O1 (BL only) | No content entry in any criterion | Lifecycle walk only | Lifecycle and fixtures | SPEC, VERIF | INVESTIGATION-CONTENT-E2E (CONTENT-001…009) |

## Proposed obligation → existing source obligations (all IDs verified)

| Proposed | Source obligations it makes verifiable |
|---|---|
| CONTENT-001-O1/O2 | R1.1-O1, R1.3-O1, S01-O6, S01-O7, S01-O9, S01-O10, S01-O16, EXT-DATA-006-O2, UI-024-O4 |
| CONTENT-002-O1 | R1.3-O1, UI-001-O6, UI-013-O1 |
| CONTENT-003-O1 | R1.1-O1, R1.2-O1, S01-O16, D-001-O1 |
| CONTENT-004-O1 | R2.1-O1, S03-O6, D-001-O1, R15.5-O1 |
| CONTENT-005-O1 | R3.1-O1, R3.2-O1, R3.5-O1, S05-O4, S05-O6, S05-O11, S05-O14, UI-005-O1, UI-005-O3 |
| CONTENT-006-O1 | R3.3-O1, R3.4-O1, UI-005-O1 |
| CONTENT-007-O1 | R6.1-O1, R6.4-O1, STATE-TRANSITION-TABLES§14-conclusion-behavior-O1, UI-025-O4 |
| CONTENT-008-O1 | R2.4-O1, R15.4-O1, UI-005-O2, STATE-TRANSITION-TABLES§4-evidence-lifecycle-O9 |
| CONTENT-009-O1 | PERSIST-006-O2, PERSIST-007-O1, PERSIST-021-O3, PERSIST-049-O2, EXT-DATA-009-O1 |
| CONTENT-010-O1/O2 | UI-004-O3, UI-004-O4, UI-005-O3 |
| CONTENT-011-O1 | UI-024-O4, PERSIST-018-O1, PERSIST-020-O5 |
| CONTENT-012-O1 | R9.1-O1, R9.2-O1, UI-011-O1, UI-011-O2, UI-004-O5 |

**Test bindings.** The bound test identities, suites and boundaries are in `proposed/content-obligations.proposed.json` and E2E-VERIFICATION.md.

**Praxis traceability.**
- A Rehearsal 009 work item that implements content completes only through the gate with `--scope CONTENT-00n`, and with `--require-fault-sensitive content-round-trip` on the round-trip obligations.
- `check-governance` ties the completion to that PASS verdict.
- The requirement IDs therefore travel from the plan, to the gate verdict, to the Praxis matrix, to the completion record.
