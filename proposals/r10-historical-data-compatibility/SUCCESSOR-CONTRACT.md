# Proposed successor contract: v4.1.5 historical-compatibility amendment

**Status: PROPOSED.** This is not frozen, compiled or approved. No agent may add union cases or field names to a frozen package (ADRP-005-O4; AGENTS.md "Frozen packages are never edited"). This document is input to an owner decision (`OWNER-DECISIONS-REQUIRED.md`, OD-HC-01).

**Compilable form:** `src/SuccessorContract.fs`. Checks C11 and C15 prove that every one of the 1,315 historical events maps into the proposed union and back to its exact stored bytes, and that every frozen case name is kept.

## 1. Version number

| Fact | Source |
|---|---|
| v4.1.4 is the latest tagged package | `indy-init-requirements-v4.1.4`; AGENTS.md |
| 4.1.3 is withdrawn and not reused | AGENTS.md; OD-INDY-19 |
| A change is always a new successor package, compiled by `requirements-compiler` with X01–X21, independently reviewed and owner-frozen | AGENTS.md; `requirements-compiled/v4.1.4/FREEZE.md` |
| v4.1.5 is the next number | — |

**Conflict:** the optimization agent's ODR-R10-20 also proposes a "v4.1.5" for UI file ownership (`origin/claude/r10-optimization:docs/rehearsal-010-optimization/OWNER-DECISIONS-REQUIRED.md:8`). Its ODR-R10-25 asks for a v4.1.5 storage-mirroring successor. Two unrelated v4.1.5 packages cannot both exist, so the owner has to choose: one combined v4.1.5, or v4.1.5 and v4.1.6 (OD-HC-02). This document names the content, not the number.

## 2. Event union: extend, do not replace

The frozen 28-case `CoreEvent` (`00-DOMAIN-STUBS.fs:92-120`) becomes 50 cases:

| Change | Count | Cases |
|---|---|---|
| **Kept unchanged** | 19 | `InvestigationStarted`, `ObservationRecorded`, `HypothesisRecorded`, `EvidenceAdmitted`, `EvidenceRelationshipRecorded`, `UnknownIdentified`, `AssumptionRecorded`, `ContradictionRecorded`, `PredictionRecorded`, `InvestigationActionRecorded`, `FindingRecorded`, `CausalAssertionRecorded`, `ConfirmedCauseReopened`, `CorrectiveActionRecorded`, `VerificationCriterionRecorded`, `CorrectiveActionImplemented`, `VerificationRecorded`, `ProposalRecorded`, `ExtensionAttached` |
| **Widened**: same name, more fields | 9 | `InvestigationActivated of from`, `InvestigationResolved of from`, `InvestigationReopened of from * reason`, `HypothesisMarkedPlausible`/`HypothesisMarkedSupported`/`HypothesisFalsified`/`HypothesisReopened of HypothesisTransition {HypothesisId; From; Reason; EvidenceIds}`, `CauseConfirmed of {causeId; policy}`, `ProposalRejected of {proposalId; reason}` |
| **Added** | 22 | `InvestigationMarkedAwaitingEvidence`, `InvestigationMarkedAwaitingVerification`, `InvestigationClosed`, `InvestigationVerificationFailed`, `HypothesisWeakened`, `HypothesisSuperseded`, `EvidenceSuperseded`, `EvidenceVerificationAssessed`, `UnknownTransitioned`, `AssumptionTransitioned`, `ContradictionTransitioned`, `InvestigationActionTransitioned`, `NegativeKnowledgeEstablished`, `CorrectiveActionApproved`, `CorrectiveActionStarted`, `CorrectiveActionAwaitingVerificationOfEffect`, `CorrectiveActionVerifiedEffective`, `CorrectiveActionVerificationFailed`, `CorrectiveActionReworkRequired`, `CorrectiveActionCancelled`, `ConclusionRecorded`, `ProposalAccepted` |

**Design rules applied**

- **Keep every frozen name.** Consumers that match on existing cases keep their meaning. Under `TreatWarningsAsErrors` an added case still breaks incomplete matches, so IF41-DOMAIN's rule applies: "any change … is breaking … goes through the coordinator as a new contract version" (`analysis/interface-specifications.json`, IF41-DOMAIN `versioning`). No alternative avoids that.
- **Follow the frozen union's existing style.** It splits transitions into one case per command (`HypothesisMarkedPlausible`, `InvestigationResolved`, `CorrectiveActionImplemented`). The successor adds the missing commands the same way. It does not replace them with generic `*Transitioned` cases, which would have meant removing frozen names.
- **Keep `from` on transitions.** CONTRACT §7.3 says the prior state is "derivable by replay". Keeping it costs one field and gains two things:
  - Encoding is lossless without replay state. C11 shows each event re-encodes to its stored line on its own.
  - The kernel can verify `from` against the replayed state (`ReplayAdapter.fs`, anomaly `TransitionFromMismatch`).
- **Remove `Scope` from `HypothesisFalsified`.** The wire has no scope (`SCHEMA-DIFF.md` §3). Keeping the field would force an invented default.
- **Exclude rename, question, observation source, evidence label and relationship revision or withdrawal.** v1.0.0 cannot store them (`SCHEMA-DIFF.md` §6). Adding cases the storage cannot persist would create a domain the data cannot back. They belong to a causa-storage 1.1.0 change, which is a separate owner decision (OD-HC-06).

## 3. Payload records: pinned to the wire

The stub's opaque payload types (`class end`, `00-DOMAIN-STUBS.fs:16-55`) become records with exactly the CONTRACT §7 fields:

- Nullable wire fields become `option`.
- Arrays stay `list`, in stored order.
- Enumerations become closed unions with exact wire tables (`src/Primitives.fs` `Enums`).

`src/EventDecoder.fs` lines 1–35 hold the proposed record definitions.

The successor deliberately does **not** adopt `FSHARP-TYPE-SHAPES.md` §19–§28. Those shapes need fields the data lacks, such as `Scope` and `Explains`, and drop fields the data has, such as `source` and `capturedAt` (`SCHEMA-DIFF.md` §3). The domain owner can still add derived or projection-only fields to the *state* types. The event payloads must stay exactly what the storage holds.

## 4. Envelope and errors

```fsharp
type EventProvenance =
    { OccurredAt: Timestamp; Actor: Actor; CorrelationId: Uuid; FormatVersion: string; SemanticVersion: string }

type EventEnvelope =            // frozen: { EventId; Investigation; Version; Payload }
    { EventId: EventId
      Investigation: InvestigationId
      Version: AggregateVersion
      Provenance: EventProvenance   // added
      Payload: EventPayload }
```

**IF41-STORAGE `VerificationFailure`** (`02-IF41-STORAGE.fs:14-20`) keeps its six cases and gains one:

```fsharp
| ReaderStage of code: string * file: string * line: int option * detail: string
```

It carries the CONTRACT §11.1 primary code. The adapter's `ReaderError` maps onto it one to one (`INTEGRATION-HANDOFF.md` §3). This lets conforming readers "agree on the primary code" (§11.1), and C07 shows agreement on all 55 non-admissibility negative fixtures.

**IF41-DOMAIN `ReplayError`** (`01-IF41-DOMAIN.fs:4-8`) is unchanged. `UnknownEventKind of string` already gives the explicit refusal that §7.1 requires.

## 5. Interfaces the successor adds

The full design is in `INTEGRATION-HANDOFF.md`. These are the signatures:

```fsharp
type DocumentSource = { ListInvestigations: unit -> Result<string list, string>
                        ReadInvestigation: string -> Result<InvestigationFiles, string> }   // persistence/integration
StorageReader.read      : InvestigationFiles -> Result<StructuralRead, ReaderError>          // stages 1-14, pure
HistoricalLoader.verify : StructuralRead -> LoadResult                                       // stages 15*, 16, 17, pure
```

\* Stage 15 runs only as far as the adapter can check (anomalies). See §6.

## 6. What the successor does not settle

**Admissibility (CONTRACT §11.3).** The adapter proves that the projection equals the engine-generated snapshot. That is not admissibility. Of the 35 negative fixtures that the engine refuses as inadmissible, 31 pass every check this adapter can run (`HISTORICAL-COVERAGE.json`, C07 `stage15NotClaimedValid`). The rules that decide them are:

- transition tables;
- cause and resolution gates;
- verification freshness;
- prediction timing;
- agent repetition.

They live in the private reference engine. Their only requirements-side form is `docs/STATE-TRANSITION-TABLES.md`, `docs/INVESTIGATION-SEMANTICS.md` and the fixtures. Writing an equivalent `History.validate` from the requirements is domain work, not contract work (OD-HC-03).

**Workspace addressing and listing** (`SCHEMA-DIFF.md` §5). These are owned by the persistence and wire contracts (ODR-R10-25 in the optimization package). The adapter's `DocumentSource` sidesteps them for reads: the workspace is a constructor argument of the source, not a contract field. Writes still need them.
