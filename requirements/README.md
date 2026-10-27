# Causa requirements baseline

`causa-requirements-baseline.json` is the authoritative inventory that every
Causa rehearsal derives from. It is a `praxis.requirements-baseline/1`
document, checked by `praxis requirements preservation verify`; the format is
defined in kemiller2002/praxis `docs/requirements-preservation.md`. How a
rehearsal must use it is defined in
[`docs/REHEARSAL-DERIVATION-POLICY.md`](../docs/REHEARSAL-DERIVATION-POLICY.md).

| | |
|---|---|
| Baseline id | `BL-CAUSA-INDY-INIT-a9df7ad0a711` |
| Pinned commit | `a9df7ad0a711c72caa5e945a15924277d7aef18b`, the Rehearsal 4 kickoff re-pin (T01, OD-INDY-6). No drift from the previous pin `c751d58d66e3f105a8c67a0cb5e069314bdbd391`: every requirement, obligation and document hash is identical; only the commit identity changes. Earlier pins: `2edaea4f65ba` (F-001/F-006) and `002d8ba6e986`, which lacked AR-019..AR-026 (F-003). Re-pin at the Rehearsal 4 kickoff commit (OD-INDY-6) |
| Documents | 19, each pinned by SHA-256 |
| Requirements | 461 |
| Obligations | 2293: 378 `acceptance`, 55 `value`, 1860 `constraint` |
| Regenerate | `python3 -I tools/build-requirements-baseline.py . <commit> > requirements/causa-requirements-baseline.json` |
| Tests | `python3 tools/test-requirements-baseline.py` (run by the preflight-tooling workflow) |

The script is deterministic: run twice, it produces byte-identical output.
Against this checkout at the pinned commit, the Praxis gate reports no
`baseline-invalid` and no drift findings, and one `disposition-missing`
finding per requirement when the disposition is empty.

## Documents in the baseline

Each document is pinned by the SHA-256 of its text, with line endings
normalized to LF. Where the document has requirement ids, it also carries a
`requirementPattern`. Any requirement id the pattern finds that is not
inventoried is reported as drift (`uninventoried-requirement`).

| Document | Requirements | `requirementPattern` |
|---|---|---|
| `docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md` | UI-001..UI-040 and its normative sections | `^## (UI-\d{3}):` |
| `docs/WASM-LOADING-EXPERIENCE-REQUIREMENTS.md` | WASM-LOAD-001..008 | `^## (WASM-LOAD-\d{3}):` |
| `docs/PROVIDER-FIXTURE-AND-INTEGRATION-REQUIREMENTS.md` | PF-001..PF-021 and the acceptance scenarios | `^\*\*(PF-\d{3}) ` |
| `docs/REHEARSAL-EXTERNAL-DATA-BOUNDARY.md` | EXT-DATA-001..013, the goal, acceptance and intake | `^- (EXT-DATA-\d{3}) ` |
| `docs/FROZEN-DEMO-STORAGE-REQUIREMENTS.md` | STORAGE-001..009 and the required evidence | `^## (STORAGE-\d{3}):` |
| `docs/DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md` | DCC-001..012 and the acceptance matrix | `^## (DCC-\d{3}):` |
| `docs/AGENT-DECISION-REDUCTION-PROFILE.md` | ADRP-001..016 and the readiness checklist | `^## (ADRP-\d{3}):` |
| `docs/REHEARSAL-DEPENDENCY-LOCK.md` | DEP-CURRENT-001..008 and preflight acceptance | `^## (DEP-CURRENT-\d{3}):` |
| `docs/REQUIREMENTS.md` | R0.1..R16.5 (the hackathon release profile) | `^### (R\d+\.\d+) ` |
| `docs/SCREEN-CONTRACTS.md` | S01..S25, the URL and responsive contracts | `^## (S\d{2}) ` |
| `docs/DESIGN-DECISION-BOUNDARIES.md` | D-001.. (every classified decision) | `^### (D-\d{3}) ` |
| `docs/STATE-TRANSITION-TABLES.md` | each lifecycle table section | none (no ids) |
| `docs/KICKOFF-EXECUTION-PACKET.md` | each packet section | none (no ids) |
| `docs/CAUSA-STORAGE-RELEASE-v1.0.0.md` | release identity, contents, authority, consumption | none (no ids) |
| `docs/REHEARSAL-AGENT-INTERFACE-FREEZE.md` | each boundary and procedure section | none (no ids) |
| `docs/REHEARSAL-ACCEPTANCE-LEDGER.md` | each ledger section | none (no ids) |
| `docs/WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md` | WDL-001..015 | `^\*\*(WDL-\d{3}) ` |
| `docs/SYSTEM-VERSION-AND-COMPATIBILITY-REQUIREMENTS.md` | VER-001..010 | `^\*\*(VER-\d{3}) ` |
| `docs/AGENT-RELIABILITY-REQUIREMENTS.md` | AR-001..018 | `^\*\*(AR-\d{3}) ` |

### Why these documents

Each one meets one of three conditions:

- It is a Causa or rehearsal requirements document with normative status.
- It is the hackathon release profile, `REQUIREMENTS.md`.
- `AGENT-DECISION-REDUCTION-PROFILE.md` ("Supporting normative documents")
  declares it normative for Causa rehearsals.

A requirement without an id is identified as `<DOCUMENT-KEY>§<heading-slug>`,
for example `SCREEN-CONTRACTS§url-contract`. For documents with no id pattern,
a new section is detected through the document hash, not by id.

## Mechanical rules (no obligation is invented)

These rules are implemented in `tools/build-requirements-baseline.py`.

- **What counts as a requirement.**
  - Every line the document's pattern captures an id from: a heading, a
    bold paragraph or a list item.
  - Every other level 1-3 heading section with a body of its own.
  - Excluded by name: goal statements, open items, history and references
    (`EXCLUDED_SECTIONS`). These are DCC "Objective", ADRP "Purpose", "Still
    deliberately open" and "Supporting normative documents", DEP-CURRENT
    "Current observed historical baseline (NOT a recommended target)",
    SCREEN-CONTRACTS "Percepta integration", and CAUSA-STORAGE-RELEASE "How
    the tag was created".
- **What counts as an obligation.** Every verbatim unit of the requirement's
  section:
  - each table data row;
  - each sentence of every other line, with list markers removed.

  Document `Status:` lines are metadata and are skipped. Nothing is
  paraphrased or merged.
- **Modality is preserved, not promoted.**
  - An obligation keeps its own MUST, should or may in its text.
  - In the gate, "mandatory" means that the obligation must receive a
    disposition. It does not mean that a SHOULD has become a MUST.
  - A derivation preserves a SHOULD by carrying it as a SHOULD.
- **Tokens.** Every concrete value in an obligation becomes an exact token,
  matched case-insensitively. A derived criterion must contain each token
  literally. The values are:
  - hex colours;
  - contrast ratios (`4.5:1`), percentages, sizes and timings (`60 minutes`);
  - clock times (`9:30`);
  - versions (`v1.0.0`);
  - HTTP status codes;
  - full commit SHAs;
  - number words (`five`).
- **Kinds.**
  - `acceptance`: the unit names acceptance, tests, screenshots or
    verify/verified, or its section is an acceptance or evidence section.
  - `value`: otherwise, when the unit has tokens.
  - `constraint`: everything else.

  These keyword rules are mechanical, so a domain sentence that says "tests"
  is still classified `acceptance`. A misclassification is fixed by changing
  the rule, never by hand-editing the manifest.

## Documents not in the baseline

Leaving a document out is a scope decision, so this list is reviewable.
Adding any of these documents needs an operator decision; the baseline is
then regenerated with it.

| Document(s) | Reason |
|---|---|
| `PRODUCT-REQUIREMENTS.md`, `PRODUCT-CONCEPT.md`, `INVESTIGATION-SEMANTICS.md`, `CORE-DOMAIN-MODEL.md`, `POLICY-ENGINE.md`, `PERSISTENCE-CONTRACT.md`, `SECURITY-THREAT-MODEL.md`, `COMMAND-EVENT-CATALOG.md`, `FSHARP-TYPE-SHAPES.md`, `DOMAIN-PACK-CONTRACT.md`, `SEMANTIC-EVOLUTION.md`, `INVESTIGATION-WORKSPACE.md`, `INVESTIGATION-ACCEPTANCE-SCENARIOS.md`, `DOMAIN-TEST-MATRIX.md`, `ARCHITECTURE.md`, `AI-ORCHESTRATION.md` | Long-term product and architecture direction for the Root-Cause Investigator platform. They are not Causa rehearsal requirement documents, and Causa reaches them through the documents above, for example ADRP-001's source precedence. Rehearsal 3's audit inventoried them at section level (about 1,350 records). **Open question for the operator:** should the baseline include them? |
| `ADVANCED-VISUALIZATIONS.md`, `BOUNDED-AUTONOMOUS-INVESTIGATION.md`, `COUNTERFACTUAL-BRANCHING.md`, `CROSS-INVESTIGATION-INTELLIGENCE.md`, `EVIDENCE-INGESTION-PIPELINE.md`, `FORMAL-VALIDATION.md`, `INVESTIGATION-PLANNER.md`, `LOCAL-WASM-ANALYTICS.md`, `MULTI-AGENT-INVESTIGATION.md`, `PROOF-CARRYING-PROPOSALS.md`, `RESEARCH-HARNESS.md`, `SEMANTIC-COMPILER.md`, `TOOL-EXECUTION-GATEWAY.md`, `COMPETITION-AMBITION.md`, `COMPETITION-ALIGNMENT.md`, `COMPETITION-DEMO-CHOREOGRAPHY.md`, `UI-CONTRACT-EXPLORATION.md` | Labelled ambitious, proposed or exploratory profiles, or competition alignment. They are not normative Causa obligations. |
| `CAUSA-DATA-CONTRACT-v1.md` | Marked SUPERSEDED CANDIDATE. `CAUSA-STORAGE-RELEASE-v1.0.0.md`, which is included, names the frozen contract. |
| `REHEARSAL-003-DEFERRED-VERIFICATION-EXPERIMENT.md` | Applies to Rehearsal 3 only, by its own status line. |
| `REHEARSAL-003-RESULTS.md` | An index of Rehearsal 3's outcome and evidence. It reports results and states no obligation. |
| `REHEARSAL-INTERFACE-MANIFEST-R001.md`, `NEXT-REHEARSAL-PREFLIGHT-STATUS.md`, `AGENT-CONTEXT-COMPILER-AND-STACK-PREFLIGHT.md` | Extracted R001 symbols, status or tooling reports. They state no implementation obligation. |
| `OPEN-QUESTIONS.md` | Open questions, not obligations. |
| `BUILD-DAY-EXECUTION.md`, `DEMO-PLAN.md`, `EXPERIMENT-PLAN.md`, `FACTORY-REHEARSAL.md`, `FRESH-AGENT-FACTORY-ACCEPTANCE.md`, `ENGINEERING-PROOF-BUNDLE.md`, `FLOOR-JUDGING-DEFENSE.md`, `HACKATHON-BOUNDARIES.md`, `IMPLEMENTATION-SLICES.md`, `EDF-FOLLOW-ON.md` | Competition, experiment and factory plans. They govern the event and the factory, not the Causa application. |
| `REHEARSAL-DERIVATION-POLICY.md` | Governs how a rehearsal derives from this baseline. It is the process around the inventory, not an obligation of the application. |
| `README.md`, `kickoff/indy-init.kickoff.json` | Repository overview and machine-readable kickoff manifest. `KICKOFF-EXECUTION-PACKET.md`, which is included, is the normative packet. |

## Limits

- **Granularity.** Obligations are sentences. A sentence that packs several
  duties is one obligation, and a derived criterion that carries only part of
  it is caught only by its tokens or by review.
- **Most obligations need a review.** Obligations without tokens (1844
  `constraint` and most `acceptance`) can only be accepted through a recorded
  human equivalence review, or a disposition with an operator approval. The
  gate keeps them `needs-review` until then, by design.
- **Documents without ids.** These five documents have no requirement ids,
  so a new section in them is detected through the document hash, not by
  id:
  - `STATE-TRANSITION-TABLES.md`
  - `KICKOFF-EXECUTION-PACKET.md`
  - `CAUSA-STORAGE-RELEASE-v1.0.0.md`
  - `REHEARSAL-AGENT-INTERFACE-FREEZE.md`
  - `REHEARSAL-ACCEPTANCE-LEDGER.md`
