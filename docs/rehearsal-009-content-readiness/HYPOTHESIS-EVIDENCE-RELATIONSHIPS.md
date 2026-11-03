# Hypothesis–evidence relationships for Causa

**Status: PROPOSED (2026-11-03).** This is an analysis and a recommendation for owner review. It records no decision and changes no frozen package, approval record, ratified catalog entry or Causa implementation.

**What it resolves:** the one open catalog question that OD-INDY-21 left unanswered. That is question 4 of `docs/requirement-catalogs/CAUSA-INVESTIGATION.md`: which hypothesis–evidence relationships Causa offers. It answers before the content additions INV-019 … INV-037 are ratified.

**Note on "v4.1.3".** The v4.1.3 package route was withdrawn by OD-INDY-19. Content is specified in the requirement catalog, as proposed INV-019 … INV-037. `proposed/00-DOMAIN-STUBS.proposed.fs` remains guidance for the content payloads and is in no package. This document therefore compares against:

- the proposed catalog entries INV-030 and INV-031;
- the proposed stubs.

## 1. Existing relationship definitions

| Source | Status | Relationship kinds |
|---|---|---|
| `docs/REQUIREMENTS.md` R3.3 | baseline requirement (priority unstated) | "linkable to one or more hypotheses with an explicit relationship **such as** support, contradiction, discrimination, or insufficiency". These are examples, not a closed set. |
| R3.4 | baseline requirement | contradictory evidence is permitted and surfaced, not forced to resolution |
| `docs/FSHARP-TYPE-SHAPES.md` §15 `CoreEvidenceRelationshipKind` | background design, explicitly not final | Supports, Contradicts, Discriminates, Qualifies, Corroborates, FailsToSupport, Irrelevant, Supersedes, DependsOn, DerivedFrom, plus `ExtensionRelationship`. "OQ-012 remains open. Do not treat the current core relationship set as empirically final merely because it is typed." |
| §21 `EvidenceRelationship` | background design | `{ Id; Evidence; Target: EntityRef; Kind; Rationale: NonEmptyText option; Scope; Provenance; Extensions }`. The target is any entity, not only a hypothesis. |
| `docs/OPEN-QUESTIONS.md` OQ-012 | open | "Which evidence relationships truly deserve CoreStable closed cases?" Posture: under review. |
| `proposed/00-DOMAIN-STUBS.proposed.fs` | proposed guidance | `RelationshipKind = Supports \| Contradicts \| Irrelevant`. `RelateEvidence = { Id; Evidence; Hypothesis; Kind; Rationale: string option; Provenance }`. Only `RelateEvidence` / `EvidenceRelationshipRecorded` exist; there is no revision or withdrawal. |
| Proposed catalog INV-030 | proposed | Link evidence to a hypothesis; shown on both; survives reload; a link to a non-existent target is refused |
| Proposed catalog INV-031 | proposed | a `select` with options `supports`, `contradicts`, `relevant` ("relevant without establishing either direction") |

**Defect in the current proposal.** The stub's third kind is named `Irrelevant`, but INV-031 names it `relevant`, with the opposite meaning: "relevant without establishing either direction". The two documents disagree, and neither name says what the category is for.

### What the Echelon Diagnostic Framework says

This is EDF v0.3 (`kemiller2002/echelon-diagnostic-framework`, `docs/edf/specification.md`, `model.md`, `principles.md`, at `41fea6f`).

- **No relationship taxonomy.** EDF defines no hypothesis–evidence relationship taxonomy and has no "hypothesis" entity. It models an **Origin Network** of contributing origins.
- **Step 6, Evaluate Evidence:** "Separate direct observation from inference. Evaluate the quality of evidence supporting the current model and note meaningful gaps or disputes."
- **Working constraints:** "Separate evidence from inference." "Record material competing explanations." Its anti-patterns include "false-precision ranking" and "confidence as probability".
- **Confidence** is "a qualitative statement about how strongly the current evidence supports the diagnostic model … not calibrated probabilities. Confidence should reflect evidence quality, consistency, competing explanations, and unresolved uncertainty."
- **Required outputs** include Unknowns and Diagnostic Sufficiency.
- **Research only, not doctrine:** negative knowledge (HY-EDF-2026-A011) and evidence revision (HY-EDF-2026-A014) are active research hypotheses. Their results are exploratory.

So EDF constrains the model without supplying it. Anything below beyond those statements is a **proposed Causa convention, not EDF doctrine**. That includes the idea that evidence compatible with every competing hypothesis does not discriminate between them, which is standard analytic practice (for example, analysis of competing hypotheses) rather than an EDF text.

## 2. Recommended relationship types

Four assessment outcomes, plus the absence of an assessment:

| Kind | UI label | Meaning |
|---|---|---|
| `Supports` | Supports | Taken with its rationale, the evidence makes this hypothesis more credible. |
| `Contradicts` | Contradicts | The evidence makes this hypothesis less credible. It conflicts with something the hypothesis predicts or requires. |
| `ConsistentWith` | Consistent, does not distinguish | The evidence fits this hypothesis, but it does not favour it over the competing hypotheses it also fits. |
| `Inconclusive` | Inconclusive | The evidence was assessed for this hypothesis, but whether it fits cannot yet be determined. |
| *(no relationship)* | Not assessed | Nobody has assessed this evidence against this hypothesis. This is a state, not a kind, and nothing is stored. |

How each proposed relationship was resolved:

| Proposed | Decision | Why |
|---|---|---|
| Supports | **keep** | core |
| Contradicts | **keep** | core; R3.4 requires surfacing contradictions |
| Consistent With | **keep, distinct from Supports** | Merging them is the confirmation-bias failure. Evidence that every rival hypothesis also explains would count as support for each. EDF asks confidence to reflect "consistency, competing explanations". R3.3's "discrimination" is the positive form of the same distinction. It replaces the proposal's `Irrelevant`/`relevant`. |
| Inconclusive | **keep, as an assessment outcome** | It differs from Consistent With. Consistent With asserts compatibility; Inconclusive asserts that compatibility could not be judged. It is EDF's "note meaningful gaps or disputes" at the level of one link, and it matches R3.3's "insufficiency". It is stored in the same field because it is an outcome of an assessment, as the other three are. |
| Contextual | **drop as a relationship** | Background evidence has no implication for a particular hypothesis. It is evidence admitted to the investigation and left unlinked. A "contextual" link would make an unassessed item look assessed. |
| Undermines Assumption | **defer** | It relates evidence to an assumption, not to a hypothesis. The core domain has `RecordAssumption` / `AssumptionRecorded` and §15 has `DependsOn`, but assumptions are not among the ten content gaps in scope (OD-INDY-20). Until then, record it as `Contradicts` with a rationale naming the premise. |
| Corroborates | **defer; evidence-to-evidence** | It relates two evidence items (§15 places it in the same kind set with an `EntityRef` target). It is not a hypothesis assessment and is not in scope. |
| Not Evaluated | **absence of a record, not a kind** | See §4. |

## 3. Definitions and examples

Each example is a documented claim from `kemiller2002/causa-data` (`research/historical/*/claims.json`, `e8259df`), cited by claim ID.

- **Supports.** For "the fire was initiated by an electrical arc in the lower forward left-hand equipment bay": a full-scale boilerplate mock-up fire, ignited by a hot wire in the suspected area, closely reproduced the total fire time and pressure history (A1-C046).
- **Contradicts.** For "ignition by electrostatic discharge": the Board eliminated electrostatic discharge as a reasonable ignition possibility (A1-C037).
- **Consistent, does not distinguish.** Panel 5 identified two candidate arc regions: the lower-left corner of the Lower Equipment Bay (junction box C-15-1A-52 cover), and the right-hand portion of the Left-Hand Equipment Bay (A1-C042). This fits both regional hypotheses and decides between neither.
- **Inconclusive.** For "ignition at the C-15-1A-52 cover plate": Panel 5 found that this arc could not be eliminated as a primary ignition source and called for experiments (A1-C044).

## 4. Assessment status model

- **Not assessed is the default, and it is the absence of a relationship record.** The evidence screen lists the investigation's hypotheses with "Not assessed" against each until an investigator assesses one.
- **No preselected kind.** Nothing is stored until the investigator picks a kind and gives a rationale. A default kind would make "Supports" the path of least resistance, which the assignment forbids.
- **At most one current assessment per evidence–hypothesis pair.**
  - Reassessing changes the current one and keeps the earlier one in history, with its provenance (R2.4-O1, INV-035).
  - Withdrawing a mistaken link records a withdrawal with a reason and never deletes it. The pair returns to "Not assessed".
- **One evidence item may have different assessments for different hypotheses.** This is the normal case; see §8.
- **Missing evidence is not evidence of absence.**
  - A documented search that found nothing is itself an evidence item. It can be assessed, usually as `Contradicts` with a rationale describing the search. Example: "no substantial evidence that water/glycol coolant was involved in initiating the fire" (A1-C039).
  - Evidence never gathered, or destroyed, is an unknown, not a relationship. Example: no single ignition source was conclusively identified (A1-C030); "destruction precludes" positive identification (A1-C033). The core domain's `IdentifyUnknown` is the place for that, and is out of this content scope.
  - Neither case is ever entered as `Contradicts` merely because something is absent.
- **Reliability is not impact.** Whether evidence is trustworthy belongs to the evidence item: its source, provenance and verification status (R3.2). What it implies for a hypothesis belongs to the relationship. The model does not combine them, and there are no numerical confidence scores (EDF: "confidence as probability" is an anti-pattern). The UI shows an evidence item's verification status beside each assessment, so an unverified item supporting a hypothesis is visible as such. Example: A11-C026, the unconfirmed Garman alarm list.

## 5. Minimal F# contract recommendation

This changes `proposed/00-DOMAIN-STUBS.proposed.fs`, which is guidance in no package. It adds no service, store or dependency.

```fsharp
/// The investigator's assessment of what one evidence item implies for one hypothesis.
/// "Not assessed" is the absence of a relationship, never a case of this union.
type RelationshipKind =
    | Supports
    | Contradicts
    /// Fits this hypothesis but does not favour it over the competing hypotheses it also fits.
    | ConsistentWith
    /// Assessed, but whether it fits cannot yet be determined.
    | Inconclusive

type RelateEvidence =
    { Id: EvidenceRelationshipId
      Evidence: EvidenceId
      Hypothesis: HypothesisId
      Kind: RelationshipKind
      Rationale: NonEmptyText          // was string option: the rationale is the recorded inference
      Provenance: Provenance }
type EvidenceRelationship = RelateEvidence

/// Reassess an existing relationship (same Id, same evidence and hypothesis).
type ReviseEvidenceRelationship =
    { Id: EvidenceRelationshipId; Kind: RelationshipKind; Rationale: NonEmptyText; Provenance: Provenance }

/// Withdraw a mistaken relationship; the pair returns to "not assessed". Nothing is deleted.
type WithdrawEvidenceRelationship =
    { Id: EvidenceRelationshipId; Reason: NonEmptyText; Provenance: Provenance }

// CoreCommand gains:  | ReviseEvidenceRelationship of ReviseEvidenceRelationship
//                     | WithdrawEvidenceRelationship of WithdrawEvidenceRelationship
// CoreEvent gains:    | EvidenceRelationshipRevised of ReviseEvidenceRelationship
//                     | EvidenceRelationshipWithdrawn of WithdrawEvidenceRelationship
```

**Not added:**
- a separate assessment-status field (the absence of a record is "not assessed");
- reliability or strength scores;
- `Target: EntityRef`, which stays hypothesis-only for this release;
- `Scope`;
- extension relationships.

All of these remain available later through §15/§21 without breaking this shape. `Revise*` follows the stub's existing pattern (`ReviseObservation`, `ReviseHypothesis`, `ReviseEvidence`). Because the case names are additions, the existing case names are unchanged.

## 6. UI behaviour

The UI uses existing Forma patterns only. It is mobile-first, and there is no visualisation and no scoring.

- **Create, from either screen:**
  - On the evidence screen (S05/S06), choose "Assess against a hypothesis", then pick the hypothesis.
  - On the hypothesis screen (S04), choose "Assess evidence", then pick the evidence.
  - Either way, the same assessment form opens.
- **Form:**
  - **Kind** is a Forma **Choice group**: native radios, one card per kind, with its one-line definition from §2 as the card's description. No option is selected initially.
  - **Rationale** is a required text area.
  - **Save** and **Cancel**: Cancel writes nothing (OD-INDY-21 (3)).
  - Save is offered only when a kind is chosen and the rationale is non-empty.
  - Why a Choice group and not a `select`: it has no placeholder option, never shows a default value, and puts each definition next to its choice. Four options fit its "two to about seven options" use.
- **Inspect:**
  - The evidence screen lists every hypothesis with its current assessment, or "Not assessed".
  - The hypothesis screen lists every evidence item with its assessment, grouped Contradicts, Inconclusive, Supports, Consistent, then Not assessed. Contradictions are never hidden (R3.4, S02 "strongest evidence/contradiction").
  - Each row shows the evidence item's verification status and opens the assessment's history.
- **Edit:** opens the same form with the current values, and saves a revision.
- **Remove:** "Withdraw assessment" asks for a reason and records a withdrawal. The history still shows it.

## 7. Validation rules

1. The evidence and the hypothesis both exist in this investigation; otherwise refused (INV-030).
2. The kind is one of the four. Anything else is refused at the boundary (INV-031 `rejects`).
3. The rationale is non-empty after trimming and at most 10,000 characters. This matches the content-field bound of OD-INDY-21 (2). The rationale is not listed there, so the bound is part of the decision requested in §10.
4. At most one current relationship per evidence–hypothesis pair. A second `RelateEvidence` for the pair is refused with a blocker that points to the existing assessment, so the investigator revises it instead.
5. Revise and withdraw name an existing, non-withdrawn relationship. Revise keeps the evidence and the hypothesis.
6. Duplicate submissions are idempotent through the existing duplicate detection by event ID.
7. History is append-only. Every record, revision and withdrawal keeps its provenance.

## 8. Historical-case examples

All claims are `verification: verified` unless noted. The hypotheses are the explanations the sources themselves weighed; no causation is claimed beyond the sources.

**Same evidence, different implications.** Challenger, CH-C052: "STS 51-C (24 January 1985) had O-ring temperature of 53 °F, the coldest to that date; erosion occurred in both boosters and for the first time a secondary O-ring showed heat effect."

| Hypothesis | Assessment | Rationale |
|---|---|---|
| H1: cold impairs the field-joint O-ring seal (Findings, CH-C022, CH-C023) | **Supports** | the coldest previous flight shows the worst seal distress |
| H2: the joint had adequate margin to launch at lower temperature (the launch rationale: "erosion margin of three times the previous worst case and the secondary O-ring being in position", CH-C036) | **Contradicts** | at the coldest prior temperature, even the secondary seal was affected |

**Challenger, further assessments:**

| Evidence | Hypothesis | Assessment |
|---|---|---|
| CH-C004 (36 °F at launch, 15 °F colder than any previous launch) | H1 | Supports |
| CH-C004 | H3: water in the clevis froze and inhibited the secondary seal (CH-C025, Finding 13, "may have been") | Consistent, does not distinguish: cold is necessary for both H1 and H3 and favours neither |
| CH-C037 (Thiokol management at 11 p.m.: "temperature effects were a concern but data inconclusive") | H2 | This is an account of an assessment made at the time, and is recorded as evidence. The investigator assesses it for H2 with a rationale. It shows why "Inconclusive" must be the investigator's own assessment, not copied from a source. |

**Apollo 1 (ignition source):**

| Evidence | Assessment |
|---|---|
| A1-C042 (two candidate regions) | Consistent, does not distinguish, for both regional hypotheses |
| A1-C044 | Inconclusive for "arc at C-15-1A-52" |
| A1-C037 | Contradicts "electrostatic discharge" |
| A1-C046 | Supports "arc in the lower forward left-hand equipment bay" |
| A1-C039 (no substantial evidence of coolant involvement) | Contradicts "coolant initiated the fire", with the rationale that this is a documented negative finding |

A1-C030 (no single source conclusively identified) is an unknown, not a relationship.

**Apollo 11 (1202/1201 alarms):**

| Evidence | Hypothesis | Assessment |
|---|---|---|
| A11-C031 (counter interrupts from the rendezvous radar CDU identified as the cause of multiple job scheduling) | H1: Executive overflow from rendezvous-radar CDU interrupts | Supports |
| A11-C028 (guidance and control performance not affected) | H2: the alarms indicated a guidance failure requiring abort | Contradicts |
| A11-C028 | H1 | Consistent, does not distinguish: it fits any benign-overload explanation |
| A11-C026 (*unverified*: Garman's handwritten list not confirmed in a primary source) | the go/no-go basis | Relates as Inconclusive; the row shows the item's unverified status, keeping reliability separate from impact |

**Result.** Every case's reasoning is expressible with the four kinds, without forcing any item into Supports or Contradicts. The only items that did not fit are unknowns (A1-C030, A1-C033), which are correctly not relationships.

## 9. Differences from the current proposal

| Item | Current proposal | Recommended | Kind of change |
|---|---|---|---|
| Kinds | `Supports \| Contradicts \| Irrelevant` (stub); `supports, contradicts, relevant` (INV-031) | `Supports \| Contradicts \| ConsistentWith \| Inconclusive`; INV-031 options `supports, contradicts, consistent-with, inconclusive` | rename the third kind (resolves the Irrelevant/relevant conflict); add one kind |
| INV-031 component | `select` | `radio-group` (Forma Choice group) | The rehearsal's probe must report the radio values as the control's options. Dokimos's `select` check compares them exactly, and a `select` with a placeholder option would fail it. No Dokimos change is needed. |
| Rationale | `string option`; no INV | `NonEmptyText`, required; a new proposed INV (ui-field, textarea, minLength 1, maxLength 10,000) | add |
| Reassess / withdraw | not specified; INV-035's editing covers observations, hypotheses, evidence and the investigation only | `ReviseEvidenceRelationship`, `WithdrawEvidenceRelationship` and their events; INV-030 gains `update`, or a new proposed INV covers it | add |
| One assessment per pair | not stated | validation rule 4 | add to INV-030's notes and `rejects` |
| Persistence and reload | INV-030/031 `save`, `reload`; INV-037 round trip | unchanged; revisions and withdrawals are covered by the same round trip | none |
| Revision events | none for relationships | as above | add |

**Proposed amendment (for owner review; not applied to any file):**
- **INV-031:** `component: "radio-group"`; `validation.options: ["supports", "contradicts", "consistent-with", "inconclusive"]`; notes as §2.
- **New proposed INV:** "Evidence relationship rationale" (ui-field, textarea, required, validation minLength 1, maxLength 10,000). Verification: mapped, exists, visible, accessible, editable, enter, validation, rejects, save, reload. Depends on INV-030. Sources R3.3, OD-INDY-21.
- **INV-030:** add `update` to its verification; its notes add "one current assessment per evidence–hypothesis pair; reassessment and withdrawal keep history".
- **Proposed stubs:** §5.

## 10. Required owner decisions

| # | Decision | Recommended |
|---|---|---|
| 1 | The relationship kinds (catalog question 4) | Supports, Contradicts, Consistent (does not distinguish), Inconclusive. Not assessed is the absence of a record. Contextual is dropped; Undermines Assumption and Corroborates are deferred. |
| 2 | Rationale | Required for every assessment, at most 10,000 characters |
| 3 | Reassess and withdraw | In scope, under OD-INDY-20 (6), "editing that keeps identity and history" |
| 4 | Apply the amendment of §9 to the proposed INV-030/031 and the proposed stubs before ratifying INV-019 … INV-037 | yes |

## 11. Implementation complexity and principal risks

**Complexity: low.** The work is:
- one four-case union;
- two commands and two events, which mirror the existing `Revise*` pattern;
- a projection keyed by evidence–hypothesis pair;
- one Forma Choice group and one text area, reused from both screens.

It adds no new store, service or dependency, no visualisation and no scoring. Against the current proposal, the extra work is roughly:
- the revise and withdraw paths;
- the rationale field;
- one more radio option.

**Principal risks:**

1. **Category misuse.** Investigators may still pick Supports where Consistent fits. The definitions on the choice cards and the required rationale reduce this but cannot prevent it. That an investigator's use of these categories improves reasoning is not established; HY-EDF-2026-A014 is untested.
2. **Probe gap.** The rehearsal's probe (written fresh under OD-INDY-22) must report a radio group's values as its options. If it doesn't, INV-031's `select` check fails honestly as FAIL; it is never a false PASS.
3. **Scope.** Deferring Undermines Assumption and Corroborates means assumption-dependency reasoning is expressed only through rationale text in this release.
4. **The tool releases are still pending** (FINAL-READINESS.md, owner action 2). Until Praxis and Dokimos are released and pinned, none of these obligations is enforced in Rehearsal 9.

## Recommendation

**MINOR AMENDMENT.** The proposed model is close but not sufficient. Its third kind is named inconsistently and conflates "fits but does not discriminate" with "relevant". It has no "inconclusive" outcome, no required rationale, and no way to reassess or withdraw a relationship. The §9 amendment is small and adds no infrastructure. It should be applied to the proposed INV-030/031 and stubs before INV-019 … INV-037 are ratified.
