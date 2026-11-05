# Schema diff: frozen v4.1.4 contracts vs. the actual causa-data history

**Status:** proposal evidence. Nothing here changes a frozen file.

**Sources compared**

| Side | Files |
|---|---|
| Frozen Indy-init contract | `requirements-compiled/v4.1.4/contracts/00-DOMAIN-STUBS.fs`, `01-IF41-DOMAIN.fs`, `02-IF41-STORAGE.fs`, `03-IF41-WIRE.fs` (tag `indy-init-requirements-v4.1.4`) |
| Shapes the stub abbreviates | `docs/FSHARP-TYPE-SHAPES.md` §5, §6, §15–§34 |
| Wire contract | causa-data `contract/v1.0.0/CONTRACT.md`, `contract/v1.0.0/schemas/**` (tag `causa-storage-v1.0.0`) |
| Actual data | causa-data `e8259df7`, `workspaces/demo/historical-v1/investigations/{c6b26614…,e3adbee5…,1784da3c…}/` |

Every count below comes from `EVENT-COMPATIBILITY-MATRIX.json`, which the test runner regenerates from the data and checks against the committed copy.

## 0. Root cause in one paragraph

Three event vocabularies exist. Indy-init's frozen `CoreEvent` union follows the earliest one.

1. **`docs/FSHARP-TYPE-SHAPES.md` §33, the early domain design.** It has 28 cases. ODR-V412-02 froze it as the v4.1.2+ stub (`00-DOMAIN-STUBS.fs:11-13`, `:92-120`).
2. **`docs/CAUSA-DATA-CONTRACT-v1.md`, the candidate storage contract.** It uses generic `*StateChanged` events, `InvestigationRenamed` and `InvestigationArchived`.
3. **causa-storage v1.0.0, the released contract.** It has exactly the reference engine's 31 `EventBody` cases (CONTRACT §7). CONTRACT §16 records that the release replaced the candidate's names, because "the engine has no such commands". All historical data is written in this vocabulary.

The frozen union was never reconciled with (3). Every confirmed failure below follows from that gap. The data itself is internally consistent: all 1,315 events pass every reader stage this adapter implements, and the replay reproduces each stored snapshot exactly.

## 1. Event envelope

| causa-storage v1.0.0 field (CONTRACT §5) | Frozen `EventEnvelope` (`00-DOMAIN-STUBS.fs:126`) | Consequence |
|---|---|---|
| `eventId` (derived UUIDv5, §5.2) | `EventId: EventId` | kept |
| `investigationId` | `Investigation: InvestigationId` | kept |
| `aggregateVersion` | `Version: AggregateVersion` | kept |
| `eventType` + `payload` | `Payload: EventPayload` | see §2 |
| `occurredAt` | — | **lost for all 1,315 events**: timeline and entity `provenance.recordedAt` cannot be rebuilt |
| `actor` (`system` for all 1,315: `historical-catalog-builder` ×563, `historical-source-importer` ×752) | — | **lost**: provenance actor cannot be shown or replayed (§8.3 requires it) |
| `correlationId` (1,307 command groups across the three cases) | — | **lost**: command groups, which admissibility is decided over (§5.4, §11.3) |
| `formatVersion`, `semanticVersion` (all `1.0.0`) | — | lost; harmless today, needed for §13 version checks |

`FSHARP-TYPE-SHAPES.md` §34 has `OccurredAt`, `Actor`, `CorrelationId` and more. The stub dropped them when it abbreviated the shapes.

## 2. Event union, variant by variant

"Variant" means the discriminator refined by the command or target state. The frozen union splits `LifecycleChanged`, `HypothesisTransitioned` and `CorrectiveActionTransitioned` into separate cases by those values. The columns are:

- **A1, A11, CH:** event counts in Apollo 1, Apollo 11 and Challenger.
- **Fixtures:** the frozen synthetic fixtures. They are counted only for the five types the history never uses.

| Variant | A1 | A11 | CH | Total | Fixtures | Frozen v4.1.4 | Proposed v4.1.5 |
|---|---|---|---|---|---|---|---|
| `InvestigationStarted` | 1 | 1 | 1 | 3 | 5 | frozen-case: `InvestigationStarted` | keep `InvestigationStarted` |
| `LifecycleChanged/activateInvestigation` | 1 | 1 | 1 | 3 | 5 | frozen-case-lossy: `InvestigationActivated` (drops from) | widen `InvestigationActivated` |
| `LifecycleChanged/markAwaitingEvidence` | 0 | 1 | 0 | 1 | 1 | no-frozen-case | add `InvestigationMarkedAwaitingEvidence` |
| `LifecycleChanged/markAwaitingVerification` | 1 | 0 | 1 | 2 | 3 | no-frozen-case | add `InvestigationMarkedAwaitingVerification` |
| `LifecycleChanged/resolveInvestigation` | 0 | 0 | 0 | 0 | 2 | frozen-case-lossy: `InvestigationResolved` (drops from) | widen `InvestigationResolved` |
| `LifecycleChanged/closeInvestigation` | 0 | 0 | 0 | 0 | 1 | no-frozen-case | add `InvestigationClosed` |
| `LifecycleChanged/reopenInvestigation` | 0 | 0 | 0 | 0 | 1 | frozen-case-lossy: `InvestigationReopened` (drops from) | widen `InvestigationReopened` |
| `LifecycleChanged/verificationFailed` | 0 | 0 | 1 | 1 | 1 | no-frozen-case | add `InvestigationVerificationFailed` |
| `ObservationRecorded` | 15 | 36 | 24 | 75 | 4 | frozen-case: `ObservationRecorded` | keep `ObservationRecorded` |
| `HypothesisRecorded` | 12 | 12 | 11 | 35 | 14 | frozen-case: `HypothesisRecorded` | keep `HypothesisRecorded` |
| `HypothesisTransitioned/markPlausible` | 9 | 11 | 9 | 29 | 7 | frozen-case-lossy: `HypothesisMarkedPlausible` (drops from, reason, evidenceIds) | widen `HypothesisMarkedPlausible` |
| `HypothesisTransitioned/markSupported` | 6 | 8 | 8 | 22 | 4 | frozen-case-lossy: `HypothesisMarkedSupported` (drops from, reason, evidenceIds) | widen `HypothesisMarkedSupported` |
| `HypothesisTransitioned/markWeakened` | 3 | 3 | 1 | 7 | 1 | no-frozen-case | add `HypothesisWeakened` |
| `HypothesisTransitioned/falsify` | 3 | 1 | 2 | 6 | 3 | frozen-case-lossy: `HypothesisFalsified` (drops from, evidenceIds) | widen `HypothesisFalsified` |
| `HypothesisTransitioned/supersede` | 0 | 0 | 0 | 0 | 1 | no-frozen-case | add `HypothesisSuperseded` |
| `HypothesisTransitioned/reopen` | 0 | 0 | 0 | 0 | 1 | frozen-case-lossy: `HypothesisReopened` (drops from, evidenceIds) | widen `HypothesisReopened` |
| `EvidenceAdmitted` | 129 | 119 | 128 | 376 | 23 | frozen-case: `EvidenceAdmitted` | keep `EvidenceAdmitted` |
| `EvidenceSuperseded` | 0 | 0 | 0 | 0 | 1 | no-frozen-case | add `EvidenceSuperseded` |
| `EvidenceVerificationAssessed` | 129 | 119 | 128 | 376 | 4 | no-frozen-case | add `EvidenceVerificationAssessed` |
| `EvidenceRelated` | 55 | 66 | 65 | 186 | 16 | frozen-case: `EvidenceRelationshipRecorded` | keep `EvidenceRelationshipRecorded` |
| `UnknownIdentified` | 3 | 4 | 3 | 10 | 6 | frozen-case: `UnknownIdentified` | keep `UnknownIdentified` |
| `UnknownTransitioned` | 3 | 3 | 3 | 9 | 3 | no-frozen-case | add `UnknownTransitioned` |
| `AssumptionRecorded` | 1 | 1 | 1 | 3 | 4 | frozen-case: `AssumptionRecorded` | keep `AssumptionRecorded` |
| `AssumptionTransitioned` | 1 | 1 | 1 | 3 | 4 | no-frozen-case | add `AssumptionTransitioned` |
| `ContradictionRecorded` | 13 | 11 | 11 | 35 | 2 | frozen-case: `ContradictionRecorded` | keep `ContradictionRecorded` |
| `ContradictionTransitioned` | 16 | 13 | 16 | 45 | 2 | no-frozen-case | add `ContradictionTransitioned` |
| `PredictionRecorded` | 1 | 1 | 1 | 3 | 3 | frozen-case: `PredictionRecorded` | keep `PredictionRecorded` |
| `ActionRecorded` | 1 | 1 | 1 | 3 | 5 | frozen-case: `InvestigationActionRecorded` | keep `InvestigationActionRecorded` |
| `ActionTransitioned` | 3 | 3 | 3 | 9 | 11 | no-frozen-case | add `InvestigationActionTransitioned` |
| `FindingRecorded` | 1 | 1 | 1 | 3 | 2 | frozen-case: `FindingRecorded` | keep `FindingRecorded` |
| `NegativeKnowledgeEstablished` | 3 | 1 | 2 | 6 | 1 | no-frozen-case | add `NegativeKnowledgeEstablished` |
| `CausalAssertionRecorded` | 1 | 1 | 1 | 3 | 3 | frozen-case: `CausalAssertionRecorded` | keep `CausalAssertionRecorded` |
| `CausalAssertionConfirmed` | 1 | 1 | 1 | 3 | 3 | frozen-case-lossy: `CauseConfirmed` (drops policy) | widen `CauseConfirmed` |
| `CausalAssertionReopened` | 0 | 0 | 0 | 0 | 1 | frozen-case: `ConfirmedCauseReopened` | keep `ConfirmedCauseReopened` |
| `CorrectiveActionRecorded` | 3 | 2 | 4 | 9 | 4 | frozen-case: `CorrectiveActionRecorded` | keep `CorrectiveActionRecorded` |
| `CorrectiveActionTransitioned/approved` | 3 | 2 | 4 | 9 | 4 | no-frozen-case | add `CorrectiveActionApproved` |
| `CorrectiveActionTransitioned/cancelled` | 0 | 0 | 0 | 0 | 1 | no-frozen-case | add `CorrectiveActionCancelled` |
| `CorrectiveActionTransitioned/inProgress` | 3 | 1 | 3 | 7 | 4 | no-frozen-case | add `CorrectiveActionStarted` |
| `CorrectiveActionTransitioned/implemented` | 3 | 1 | 3 | 7 | 4 | frozen-case: `CorrectiveActionImplemented` | keep `CorrectiveActionImplemented` |
| `CorrectiveActionTransitioned/awaitingVerificationOfEffect` | 2 | 1 | 2 | 5 | 3 | no-frozen-case | add `CorrectiveActionAwaitingVerificationOfEffect` |
| `CorrectiveActionTransitioned/verificationFailed` | 0 | 0 | 1 | 1 | 1 | no-frozen-case | add `CorrectiveActionVerificationFailed` |
| `CorrectiveActionTransitioned/verifiedEffective` | 1 | 1 | 1 | 3 | 1 | no-frozen-case | add `CorrectiveActionVerifiedEffective` |
| `CorrectiveActionTransitioned/reworkRequired` | 0 | 0 | 0 | 0 | 1 | no-frozen-case | add `CorrectiveActionReworkRequired` |
| `CriterionRecorded` | 2 | 1 | 4 | 7 | 3 | frozen-case: `VerificationCriterionRecorded` | keep `VerificationCriterionRecorded` |
| `VerificationRecorded` | 2 | 1 | 4 | 7 | 2 | frozen-case: `VerificationRecorded` | keep `VerificationRecorded` |
| `ConclusionRecorded` | 1 | 1 | 1 | 3 | 2 | no-frozen-case | add `ConclusionRecorded` |
| `ProposalRecorded` | 0 | 0 | 0 | 0 | 3 | frozen-case: `ProposalRecorded` | keep `ProposalRecorded` |
| `ProposalWasAccepted` | 0 | 0 | 0 | 0 | 2 | no-frozen-case | add `ProposalAccepted` |
| `ProposalWasRejected` | 0 | 0 | 0 | 0 | 1 | frozen-case-lossy: `ProposalRejected` (drops reason) | widen `ProposalRejected` |

**Totals over the 1,315 historical events**

| Frozen representation | Events | Meaning |
|---|---|---|
| Frozen case, no field dropped by the case's own arguments | 765 | The case name exists. The payload type is opaque in the stub (`class end`), so field fidelity is not established (§3). |
| Frozen case that drops fields | 63 | The case's fixed arguments have no room for them: 29+22+6 hypothesis transitions, 3 `InvestigationActivated` and 3 `CauseConfirmed`. |
| No frozen case | 487 | 376 `EvidenceVerificationAssessed`, 45 `ContradictionTransitioned`, 32 corrective-action transitions other than `implemented`, 9 `UnknownTransitioned`, 9 `ActionTransitioned`, 7 `markWeakened`, 6 `NegativeKnowledgeEstablished`, 3 `AssumptionTransitioned`, 3 lifecycle and 3 `ConclusionRecorded`. |

Two consequences follow:

- **The frozen union cannot represent 487 historical events (37%).** That covers 16 variants of 10 of the 26 discriminators the history uses.
- **Not one event survives with its envelope intact.** The §1 envelope losses apply to all 1,315.

**Why `ExtensionEvent` is not a way out.** The only escape hatch is `EventPayload.ExtensionEvent of SemanticId * payloadJson` (`00-DOMAIN-STUBS.fs:124`). causa-storage v1.0.0 has no extension namespace (CONTRACT §13.5), and it refuses unknown data rather than dropping it (§7.1). Wrapping core events as extensions would invent a mapping the contract forbids, and replay would have no semantics for them.

## 3. Payload shapes

In the stub every payload type is `class end`, which has no fields and no constructor (`00-DOMAIN-STUBS.fs:11-13`, `:16-55`). The domain owner fills them in from `FSHARP-TYPE-SHAPES.md`. That document calls them "indicative shapes, not final serialization" (§19).

Taking those shapes literally would make the payloads incompatible with the data in two ways:

- **Some required fields have no wire source.** Filling them would mean inventing defaults:

  | Field | Records that require it | Wire | Reference |
  |---|---|---|---|
  | `Scope` | `Observation`, `Hypothesis`, `Evidence`, `EvidenceRelationship`, `Unknown`, `Assumption`, `Contradiction`, `Prediction`, `InvestigationAction`, `Finding`, `CausalAssertion`, `VerificationCriterion`, and the `HypothesisFalsified` argument | no scope field (CONTRACT §7) | §6, §19–§26 |
  | `Observation.Verification` | `Observation` | no observation verification | §19 |
  | `Hypothesis.Explains` | `Hypothesis` | no such field | §19 |
  | `Proposal.PayloadJson`, `SemanticManifest`, `SourceRefs` | `Proposal` | `ProposalRecorded` = `{proposalId, kind, text, rationale}` | §28 |

- **Some wire fields have no place in the indicative records.** They would be dropped:

  | Wire field | Indicative record | Reference |
  |---|---|---|
  | `EvidenceAdmitted.source` and `capturedAt` | `Evidence` has neither field | §20 |
  | `FindingRecorded.interpretation` | `Finding` has `NormalizedResult`, a different meaning | §24 |
  | `CausalAssertionRecorded.outcome` (free text on the wire) | `CausalAssertion.Outcome` is an `EntityRef` | §25 |

- **The indicative enumerations differ from the wire:**

  | Wire value | Used in the history | Indicative enumeration |
  |---|---|---|
  | `relationshipKind` `insufficient` | 9 times | not in `CoreEvidenceRelationshipKind` (§15) |
  | `actionKind` `test` | once | not in `CoreInvestigationActionKind` (§16) |
  | indicative `InvestigationActionState` `Blocked` | — | the wire `actionState` has no such value (§13) |

`SUCCESSOR-CONTRACT.md` §3 therefore pins the payload records field for field to the wire tables. It does not adopt the indicative shapes.

## 4. Fields, types and versions: what is not a failure

Each item was checked against the data:

| Check | Result |
|---|---|
| Version mismatch | none. All 1,315 envelopes carry `formatVersion` and `semanticVersion` `"1.0.0"`, and all manifests carry `schemaVersion` `"1.0.0"`. CT-006 binds 1.0.0. |
| Historical schema variation | none. Each discriminator has exactly one payload key set across all three cases (`historicalVariations.payloadKeySets` in the matrix). No event fails the strict v1.0.0 decoder. |
| Ordering | `aggregateVersion` runs 1..N in file order in every case. `occurredAt` never decreases, but it is not unique: 448/452, 430/432 and 429/431 distinct values. **A consumer that orders by time is nondeterministic.** Order must come from `aggregateVersion` (CONTRACT §5.1, §5.6). |
| Identity | every `eventId` is the §5.2 UUIDv5 derivation, and all are unique. Investigation ids equal the directory and manifest. |
| Data inconsistency | none found. The three cases are byte-identical across `workspaces/demo/historical-v1`, `workspaces/demo/demonstration-v1`, `templates/historical-v1` and `templates/demonstration-v1` (check C13). No corrective action on the historical records is needed or proposed. |

## 5. Storage layout and addressing

| causa-data | Frozen contract | Gap |
|---|---|---|
| `workspaces/demo/<runId>/investigations/<id>/` (CONTRACT §3). The active catalog is workspace `demonstration-v1` (`catalog/active.json`). | `Repository = {Owner; Name; Branch}` (`03-IF41-WIRE.fs:45`); `PathOf: InvestigationId -> RepoPath` (`02-IF41-STORAGE.fs:29`) | No workspace exists in the contract, so the path of a catalog case cannot be formed from contract inputs. |
| No index file. Listing an investigation means listing the directory or reading the catalog manifest. | No list operation in any frozen contract | The four catalog ids cannot be discovered through a contract operation. |
| 17 ordered reader codes (CONTRACT §11.1) | `VerificationFailure` has 6 cases (`02-IF41-STORAGE.fs:14-20`) | A fidelity gap, not a load blocker. `SUCCESSOR-CONTRACT.md` §4 maps the codes. |

The prior optimization report raised these as C3, S1 and S6 (`origin/claude/r10-optimization:docs/rehearsal-010-optimization/DATA-REPOSITORY-CONTRACT.md` §6). This mission re-verified them against the files cited above. It does not propose to resolve them here. They belong to the persistence and wire contracts, which other agents own.

## 6. Related data-model gaps (mission §9)

Each gap is classified as one of:

- **serialization:** the wire has no form for the value;
- **domain:** there is no domain event or command;
- **UI contract:** the catalog asks for behaviour the data cannot hold;
- **persistence command:** there is no command that writes it.

| Gap | Requirement IDs | Wire / data reality | Class | Exact consequence |
|---|---|---|---|---|
| Primary investigative question | INV-023 (J1-03); feeds INV-034, INV-037 | `InvestigationStarted` = `title`, `observationId`, `original` only. No question anywhere in the history. | serialization + domain + persistence command | The question cannot be stored or reloaded in v1.0.0. A historical case cannot show one, and J1-03 cannot round-trip it. Needs a causa-storage 1.1.0 additive field or event (CONTRACT §13.1), plus a domain command. |
| Observation source reference | INV-025 (J2-01) | `ObservationRecorded` = `observationId`, `original`, `observedAt`. No source. | serialization + domain + persistence command | Same as INV-023. Historical observations carry their sourcing only inside `original` text. |
| Evidence label | INV-027 (J2-03, J2-05); required, ≤200 chars | No label field. Historical `content` begins with `Excerpt:` in 366 of 376 items, and `source` is a 219–691-character citation. | serialization + UI contract | A label cannot be persisted. For historical cases a UI would have to derive one (for example by truncating `source`), which is presentation, not data. The owner must say whether a derived label satisfies INV-027 for read-only historical cases. |
| Evidence source reference / URL | INV-028, INV-029 (J2-03) | `source` exists and is required. The historical data embeds the URL, claim id and source id inside `source` text (check C10: every provenance record's URL, claim and source id appear in it). `contentKind: artifact` carries a URL only for artifact content (0 historical uses). | UI contract | INV-028 maps to `source`. INV-029 has no separate field. For historical cases the URL can only be displayed by parsing text, which the contract does not define. |
| Relationship kinds `consistent-with`, `inconclusive` | INV-031 (J2-04), OD-INDY-23 | Wire `relationshipKind` = `supports` ×155, `contradicts` ×22, `insufficient` ×9; `discriminates` ×0 | serialization + domain + UI contract | `consistent-with` and `inconclusive` have no wire value. Nothing defines whether `insufficient` displays as `inconclusive`, so **9 historical relationships have no INV-031 rendering** until the owner decides. `discriminates` has no INV-031 option either. |
| Relationship rationale required | INV-038 (J2-04) | `rationale` is nullable on the wire. All 186 historical relationships carry one. | serialization (writer side) | Reading the history is fine. A v1.0.0 writer can store a null the catalog forbids, so the command boundary must enforce INV-038. |
| Reassess / withdraw a relationship; one current assessment per pair | INV-030 `update`, INV-035, OD-INDY-23 (3) (J2-05, J2-06, J2-09) | `EvidenceRelated` only creates. There is no revise or withdraw event. No pair is related twice in the history. | domain + persistence command | Cannot be written or replayed in v1.0.0. OD-INDY-23's `ReviseEvidenceRelationship` / `WithdrawEvidenceRelationship` need storage 1.1.0 events. |
| Investigation rename / edit | INV-034, INV-035 (J2-06, J2-09) | No rename event. CONTRACT §16: "`InvestigationRenamed` … not registered: the engine has no such commands". Evidence correction is `EvidenceSuperseded` to a **new** id. | domain + persistence command | Editing title, problem or question cannot be persisted. Editing evidence "keeping its identifier" (INV-035) contradicts v1.0.0, where supersession creates a new id. |
| Title optional | INV-002 (`required: false`) | `title` is non-blank and required. The engine falls back to the original text (FIELD-MAPPING.md:30). | none, if the kernel does the same | All three historical titles are present. |

None of these blocks loading the historical cases, which carry none of the missing fields. They block the **write** and **round-trip** requirements. They are recorded here so the UI-contract agent can consume them without editing this branch (`INTEGRATION-HANDOFF.md` §5).
