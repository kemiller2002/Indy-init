# Rehearsal 009: investigation content acceptance amendment proposal

Status: PROPOSED, NOT APPROVED. This document supplements the existing requirements audit; it does not change the frozen v4.1.2 package or assert that the capabilities below are absent from the baseline. Before promotion, map each item to existing requirement IDs and remove duplicates. Owner approval and recompilation are required for any genuinely new obligations.

## Motivation
Rehearsal 008 source inspection showed workflow commands such as StartInvestigation, RenameInvestigation, RecordObservation, RecordHypothesis and RecordConclusion, but did not establish substantive content entry and reload. Its investigation view explicitly reports that evidence items are not part of that version. A command's existence or event counter is not sufficient evidence of usable content capture.

## Proposed user-facing acceptance requirements
1. Investigation creation: required editable name, optional editable description and primary investigative question; stable ID independent of name; validation and accurate save state.
2. Observation: substantive required text, optional notes and source, stable ID, author/time provenance; visible on reopen.
3. Hypothesis: substantive required statement, optional explanation, stable ID and provenance; visually distinct from observed facts.
4. Evidence: user-entered title, description and source reference (URL when applicable), stable ID, provenance; no mandatory binary uploads.
5. Relationships: associate evidence to a hypothesis as supporting, contradicting or relevant/undetermined using existing EDF semantics; persist links by stable IDs.
6. Conclusion: substantive text, distinct from hypotheses; preserve existing evidence and review gates.
7. Editing: edit/reopen all applicable fields without changing entity identity or silently discarding provenance.
8. Persistence: serialize to the authorized GitHub repository, verify committed read, reconstruct projection and render the same content after a fresh reload. Never display Saved merely because a command was dispatched.
9. Mobile UX: accessible Forma-based create/add/edit controls at iPhone widths; labeled long-text inputs, validation, wrapping, usable touch targets, clear unsaved/saving/saved/conflict/error states.

## Mandatory end-to-end acceptance scenario: INVESTIGATION-CONTENT-E2E
On a clean authorized test repository, use the actual mobile UI to create a named investigation with description and primary question; enter an observation, hypothesis and sourced textual evidence; link evidence to hypothesis; enter a conclusion if review gates permit; save; wait for verified GitHub persistence; reload the application and reopen the investigation; assert every field, entity ID and relationship survived; edit description, save, reload and verify the edit and provenance.

Negative scenarios: empty name/observation/hypothesis, malformed source URL, nonexistent relationship target, unauthorized save, offline/network failure, concurrent edit conflict, duplicate submission and malformed stored data. Verify user-visible recovery and no false Saved state.

## Verification requirements
- Separate checks for UI data entry, command payload, domain validation, event serialization, GitHub commit, verified read, reconstruction, display and editing.
- A command handler or event-count assertion alone cannot satisfy a user-facing content requirement.
- Bind Dokimos obligations to the exact candidate SHA; require actual browser-to-GitHub evidence for the full vertical slice.
- Praxis completion must use applicable executable and record evidence without falsely treating record-only obligations as missing tests.

## Governance and implementation boundaries
1. Audit v4.1.2 and earlier baseline requirement IDs before adding anything. Classify each capability as existing, underspecified or absent; include code and test references from rehearsal 008.
2. If existing requirements already cover an item, strengthen execution guidance and verification without duplicating requirements.
3. If genuinely absent, prepare a successor amendment with unique IDs, independent review, Percepta analysis, Dokimos verification obligations and owner approval. Do not edit frozen v4.1.2 files, manifests, hashes or approvals.
4. Do not prebuild rehearsal 009 application code or start its seven-hour clock.
5. Preserve F# + Limen + Forma, existing GitHub persistence and established domain semantics.

## Readiness gate
Before T0, deliver a requirement-to-code-to-test gap matrix and a proposed or approved verification plan. During rehearsal, the first vertical slice must demonstrate meaningful text entry, verified save, fresh reload and correct content rendering. Report unsupported functionality as incomplete rather than verified.
