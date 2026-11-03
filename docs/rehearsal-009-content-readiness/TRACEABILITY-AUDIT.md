# Rehearsal 009 investigation-content traceability audit

Status: initial source-and-requirements audit, 2026-11-03. **Not an approved amendment or proof of live behavior.**

## Sources inspected
- `requirements/causa-requirements-baseline-v4.json` (520 baseline entries, including sections and non-P0 requirements)
- `requirements-compiled/v4.1.2/verification-execution-plan.json`
- `requirements-compiled/v4.1.2/AGENT-RULES.md`
- Rehearsal 008: `src/Causa.Domain/{Commands,Entities}.fs`, `src/Causa.App/{Update,Views/Investigation}.fs`.

## Existing requirement coverage
| Existing ID | Existing requirement | Assessment |
|---|---|---|
| R1.1 | User begins with a free-form description of an observed failure | Already required; substantive text-entry verification missing from inspected R8 UI |
| R1.2 | Preserve original source statement in structured investigation | Already required; end-to-end preservation needs explicit test |
| R1.3 | Stable investigation ID and creation timestamp | Already required; editable name distinct from ID is not explicit here |
| R2.1 | Multiple plausible hypotheses | Already required; substantive hypothesis entry/reload needs explicit test |
| R2.3 | Record reasons and evidence for hypothesis state changes | Already required; relationship authoring UI not established |
| UI-001 | Investigation dashboard shows title and counts | Already required; title entry/edit not established |
| UI-005 | Evidence inspector shows original content/source and links | Already required; creation of evidence items not established in R8 |
| UI-015 | Tagged UI acceptance tests and traceability; deployed exact SHA | Already required; add explicit content round-trip assertions |
| UI-025 | Overview shows what happened, findings and evidence | Already required; rendering substantive saved text needs assertion |
| UI-027 | Contextual domain-authorized actions and draft clarity | Already required; editable form workflow not established |
| UI-040 | End-to-end acceptance | Existing requirement; inspect precise obligations before mapping additional test |

## Rehearsal 008 implementation evidence
- `Commands.fs` includes StartInvestigation, RenameInvestigation, RecordObservation, RecordHypothesis, RecordConclusion.
- `Entities.fs` includes observation/hypothesis counters; inspected state does not establish content fields for investigation name, description, primary question or evidence.
- `Views/Investigation.fs` renders history-derived observation/hypothesis/conclusion entries and explicitly says: 'evidence items are not part of this version of Causa.'
- `Update.fs` has a GitHub create workflow, but this is not proof that meaningful text can be entered, committed and reconstructed.

## Findings
1. **Implementation/verification gap (supported):** Workflow commands and event labels do not demonstrate substantive content entry or round-trip persistence. Evidence entry is explicitly unavailable in the inspected R8 view.
2. **Specification precision gap (provisional):** Required editable investigation name, description, primary question, entity-specific editors, and full content round-trip are not established by the requirement excerpts inspected here. Search the complete canonical documents before proposing new IDs.
3. **Acceptance gap (supported):** The inspected plan includes live GitHub and browser suites, but this audit has not established a single user-facing scenario that enters all content, saves, reloads and asserts exact values and links.

## Recommended correction
Preserve existing IDs and strengthen their bound acceptance tests where requirements already cover behavior. For genuinely missing user-visible behavior, prepare an owner-approved successor amendment. Use `CONTENT-ACCEPTANCE-PROPOSAL.md` as proposed text, not a frozen requirement.

**Mandatory acceptance:** Mobile UI creates a named investigation with description/question, observation, hypothesis, sourced evidence and relationship; verified GitHub save; fresh reload; assert exact text and stable IDs; edit; save and reload; assert edit and provenance. Fail on missing/skipped tests and false Saved states.

## Required next validation before promotion
1. Inspect complete source requirements and all UI-040 obligations, not only baseline excerpts.
2. Map each proposed field and interaction to exact requirement IDs or identify truly absent behaviors.
3. Generate an explicit successor amendment only for absent obligations; preserve v4.1.2 frozen files and hashes.
4. Obtain owner approval, compile/validate the successor, bind Dokimos tests, and confirm Praxis acceptance coverage.
5. Do not launch rehearsal 009 or create application code before T0.
