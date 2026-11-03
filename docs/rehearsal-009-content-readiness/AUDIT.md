# Rehearsal 009 content readiness: audit

**Question.** Why could Rehearsal 008 offer investigation workflow commands without letting a person enter, save, reload and read the substance of an investigation?

**Answer.** All three layers failed together, and each failure hid the others:
1. **The specification never required it at a verifiable priority.** The content requirements exist in the frozen baseline, but none is P0, so the verification plan carries no criterion and no test for any of them.
2. **The frozen interface contract made it impossible.** v4.1.1's `EventPayload` was a kind string, and its `CommandEnvelope` had no command body.
3. **The tests measured the state machine, not the content.** They asserted counters and labels; no test ever typed investigation content.

**Sources.** All evidence below is from:
- the frozen Indy-init packages (`requirements/causa-requirements-baseline-v4.json`, `requirements-compiled/v4`, `v4.1.1`, `v4.1.2`);
- the read-only Rehearsal 008 repository `kemiller2002/indy-init-rehearsal-008` at `fd8fba9` (cited as `r008:`).

Nothing in either repository was modified.

## 1. Rehearsal 008: what the code actually does

Rehearsal 008's plan of record was `indy-init-requirements-v4.1.1` (`r008:evidence/FINAL-REPORT.md:3`).

| Layer | Finding | Evidence |
|---|---|---|
| Commands | Twelve payload-less union cases. `RenameInvestigation` carries no name. `RecordObservation`, `RecordHypothesis` and `RecordConclusion` carry no text. | `r008:src/Causa.Domain/Commands.fs:6-18` |
| Events | Twelve payload-less cases, one per command. | `r008:src/Causa.Domain/Events.fs:7-20` |
| Wire payload | `type EventPayload = EventPayload of kind: string`. What is persisted is the event's name. `DomainEvent.toPayload` writes `EventPayload(kind event)`. | `r008:src/Causa.Domain/Identity.fs:14`; `Events.fs` `toPayload` |
| State | `InvestigationState = { Id; Version; Lifecycle; Observations: int; Hypotheses: int; Conclusions: int; EventIds }`: counters only. There is no name, description, question or text. | `r008:src/Causa.Domain/Entities.fs:5-13` |
| Kernel | "the frozen EventPayload carries only the event kind, so the v1 fold below takes the…" | `r008:src/Causa.Domain/Kernel.fs:130` |
| Page commands | The page offers fixed buttons `StartInvestigation`, `RecordObservation`, `ActivateInvestigation`, `RecordConclusion` and `ResolveInvestigation`, each sent as the Limen event `submit.<CommandName>`. There is no input field. | `r008:src/Causa.App/View.fs:57-66` |
| Start | `StartInvestigation` is decided with no payload. | `r008:src/Causa.App/Update.fs:614-618` |
| Workspace lists | Entries are `"<Noun> n (version v)"`, built from event kinds. | `r008:src/Causa.App/Views/Investigation.fs:64-65, 190-195` |
| Evidence | Hard-coded empty state: "No evidence recorded yet. Evidence items are not part of this version of Causa." | `r008:src/Causa.App/Views/Investigation.fs:196` |
| Title | The projection's `Title` is the route title, not an investigation name. | `r008:src/Causa.App/Projections.fs:36, 230-249` |
| Deviation | DEV-R8-09: "the frozen `CommandEnvelope` has no command body, `EventPayload` is only a kind string, and `Investigation` holds only an id and a version". The request to add a body (CR-T13-01) was declined because the contract was frozen. The workaround `Kernel.decideOn` recovers the command's identity, never content. | `r008:evidence/DEVIATIONS.md:38-42` |
| Tests | They assert counters and labels: `"1 observation recorded. Recorded: Observation recorded at version 2."`, timeline `["Investigation started", "Observation recorded", …]`, graph nodes `"Observation 1"`, `"Observation 2"`. | `r008:tests/browser/contract/report-ui-2.spec.js:103, 109`; `view-ui-2.spec.js:45` |
| Tests | The only `fill(` calls in the whole browser suite are the connection form's Owner, Repository and Token fields. No test types investigation content. | `r008:tests/browser/contract/connect-save-flow.spec.js:200-202` (`grep -rn "fill(" tests/browser`) |

### Classification of each capability

The six classes the assignment defines are: (1) absent from the application; (2) in the domain but not exposed; (3) in the UI but not persisted; (4) persisted but not reconstructed; (5) implemented but unusable on mobile; (6) implemented but not verified.

| Capability | Class | Evidence |
|---|---|---|
| Investigation name entry | 1 | There is no field. `StartInvestigation` has no payload. The title is the route title (`Projections.fs:230-249`). |
| Rename | 2, then 1 | `RenameInvestigation` exists in the domain without a name, so even the domain cannot rename. It is not offered on the page (`View.fs:57-63`). |
| Description, primary question | 1 | Absent from the state, commands and UI (`Entities.fs`, `Commands.fs`). |
| Observation text | 1 | `RecordObservation` carries nothing. The UI shows only "Observation n (version v)". |
| Hypothesis text | 1 for content, 2 for the command | `RecordHypothesis` exists without content. It is in the workspace list (`Investigation.fs:94`) but not in `View.offered`. |
| Evidence items | 1 | Explicitly "not part of this version" (`Investigation.fs:196`). |
| Evidence–hypothesis relationships | 1 | No command, event or view. |
| Conclusion text | 1 | `RecordConclusion` carries nothing. |
| Editing content | 1 | There is nothing to edit. |
| Persistence of content | 3 and 4 do not apply | No content exists to persist. Event kinds persist and reconstruct correctly through the verified save protocol; only kinds survive. |
| Mobile | 5 for workflow, 6 for content | Layout assertions exist at the narrow viewports (`r008:tests/browser/acceptance/WDL-001.spec.js`, `UI-029.spec.js`), but there is no content form to lay out. |
| Verification | 6 for lifecycle and save | The lifecycle, save indicator and persistence protocol are tested. No test enters or reads content. |

**Note on the demonstration catalog.** The read-only demonstration catalog (Challenger STS-51-L and others in `causa-data`, `r008:evidence/T34/T34-matrix.json:2857`) was loaded as event files. The loaded events carry only kinds into this kernel, so their content could not be shown either.

## 2. The frozen requirements

These findings come from the full audit in [TRACEABILITY.md](TRACEABILITY.md). Every ID there was checked against the baseline.

### 2.1 The content requirements exist, but at a priority the plan does not verify
- The baseline states the content capabilities. Examples:
  - R1.1-O1: "A user must be able to begin with a free-form description of an observed failure…"
  - S01-O6/O7/O16: original problem statement field, optional title, original text preserved.
  - R3.1–R3.5: evidence as first-class records, with source and URL.
  - R3.3-O1: evidence linkable to hypotheses with support or contradiction.
  - S05-O4/O6/O11/O14: evidence capture of text, URL, source and original content.
- Every one of them has priority `unstated`.
- The owner's ODR-V4-10 option A made the verification plan P0-only (`requirements-compiled/v4/FREEZE.md`). As a result:
  - The plan's scope is 161 requirements and 613 obligations, all P0, with `excludedRequirements: 0`.
  - R1–R6, S01–S13 and the state-transition tables are absent from the plan. In `verification-manifest.json` their entries have empty criteria and `NOT_RUN`.
  - DEC-R7-005 put S01–S15 and S17–S25 "IN SCOPE", but scope without a P0 obligation creates no acceptance test.
- The P0 requirements that touch content verify other things:
  - UI-005-O1 verifies **viewing** evidence, against deterministic fixtures.
  - PERSIST-049-O2 verifies that a reload "contains every confirmed change", where "change" is never defined as content.
  - EXT-DATA-008-O2 verifies the round trip of a **new empty** investigation.
- **No obligation anywhere requires a user to enter substantive content through the UI and see it again after a reload.**

### 2.2 The interface contract
- **v4.1.1** (Rehearsal 008) froze `EventPayload = EventPayload of kind: string` and a `CommandEnvelope` without a command (`requirements-compiled/v4.1.1/contracts/00-DOMAIN-STUBS.fs:11-12`). Content was impossible by construction.
- **v4.1.2** (ODR-V412-02) restored `CommandEnvelope.Command` and the `CoreEvent` bodies (`v4.1.2/contracts/00-DOMAIN-STUBS.fs:56-125`), which corrects the structural cause.
- **But v4.1.2 leaves every payload opaque:** `type RecordObservation = class end` and so on, "each payload type is the domain owner's to define".
- **And v4.1.2 still omits several capabilities:**
  - no `RecordConclusion` command and no conclusion event;
  - no rename or revision command;
  - no description or primary question.
- The concrete shapes exist only in the background `docs/FSHARP-TYPE-SHAPES.md` (sections 19–21 and 27, "indicative shapes, not final serialization"), which the requirements README lists as non-normative.

### 2.3 What the frozen requirements do not contain at all
- an investigative-question field;
- editing an investigation's name or description;
- revising observation, hypothesis or conclusion text;
- user entry of conclusion text;
- an evidence title or description field;
- empty-field and invalid-URL validation rules (only a generic "validated forms", UI-004-O3, and URL sanitising, UI-005-O3);
- a tested UI round trip of content.

## 3. Root cause

| Gap class | Applies | Why |
|---|---|---|
| SPECIFICATION GAP | yes, the primary cause | Content requirements are unprioritised, so they are not in the P0 plan and have no criterion or test. Several behaviours are unspecified (§2.3). The v4.1.1 contract forbade content, and v4.1.2 leaves it opaque. |
| VERIFICATION GAP | yes | The plan binds no content round trip. Rehearsal 008's tests asserted counters and labels, and no test typed content. Dokimos could not catch it, because no obligation required it. |
| IMPLEMENTATION GAP | yes, but consistent with the contract | Rehearsal 008 implemented exactly what the frozen v4.1.1 boundary allowed. It saw the defect and reported it (DEV-R8-09), but could not change the frozen contract inside the run. |
| INTEGRATION GAP | yes | A workaround (`decideOn`) carried the command's identity end to end, but nothing carried content between the domain, the wire and the view. |
| USABILITY GAP | yes | The workspace shows engineering labels ("Observation 2 (version 3)") instead of investigation information. |

**Why the engineering system did not stop it.** Every gate in place at the time checked what the plan required, and the plan did not require content. Dokimos verifies bound identities, Praxis completes against the matrix, and the acceptance verifier counts P0 obligations. A rehearsal could therefore be "verified" while missing the product's core purpose. The correction (AMENDMENT-DECISION.md) puts content into the P0 plan, so every existing gate starts enforcing it, and adds two content-specific checks that are proven in this change:
- a suite boundary that rejects command-only tests;
- a fault-sensitivity requirement that rejects tests passing whatever the content is.

## 4. Rehearsal 009 exposure if nothing changes

Rehearsal 009 builds on v4.1.2. The structural blocker of the contract is gone, so a team *could* now implement content. But no P0 obligation *requires* it, and no gate would notice its absence. Rehearsal 009 could therefore repeat Rehearsal 008's outcome with every gate green. That is why the readiness verdict depends on the owner approving the successor amendment (FINAL-READINESS.md).
