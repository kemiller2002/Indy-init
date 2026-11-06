# Amendment 4.1.6: RecordConclusion and causa-storage 1.1.0

v4.1.6 is the successor of the approved package v4.1.5 (APR-V415-OWNER-3, tag `indy-init-requirements-v4.1.5`). v4.1.5 is not edited.

**Authority.** OD-CR-01 A (APR-CR-OWNER-1/-2, recorded) commissions a causa-storage 1.1.0 and a v4.1.6 with the matching `CoreEvent` cases and a `RecordConclusion` command. The owner accepted every recommendation of section A of the decisions sheet ([`docs/v416-competition-decisions/DECISIONS-FOR-TONIGHT.md`](../../docs/v416-competition-decisions/DECISIONS-FOR-TONIGHT.md)) in session on 2026-11-06. **That answer is not a record.** The requests in `proposals/owner-decision-requests.json` (ODR-V416-*) are how the owner records it with `owner-freeze.fsx decide v4.1.6`. Each request's recommended option is the accepted one. None is recorded by this change.

| Request | Accepted answer, built into this package |
|---|---|
| ODR-V416-01 | The contract change below (00, 02) |
| ODR-V416-RC1 | One v4.1.6: RecordConclusion + the minimal causa-storage 1.1.0 |
| ODR-V416-S1 | B: EvidenceRevised, ObservationRevised, kinds `consistentWith` and `inconclusive`. The other four OD-CR-01 A events follow as causa-storage 1.2.0 and a later package (this narrows OD-CR-01 A) |
| ODR-V416-S8 | B: a new `EvidenceRelationshipRecorded` for the same pair is the reassessment; the latest is current |
| ODR-V416-RC3 | A: J2-03 and J2-07 choose a category and a strength (`verification/journeys/j2-analysis.spec.mjs`, owner-controlled) |
| ODR-V416-RC4 | A: optional catalog field INV-047 "Residual uncertainty" |
| ODR-V416-RC5 | A: Decide-time rules V7 and V8 |
| ODR-V416-RC6 | A: `ConclusionCard.Strength` (07) |
| ODR-V416-RC7 | A: `RecordConclusion` directly after `RecordVerification` |
| ODR-V416-S2 | B: the 1.1.0 rules are normative in causa-data `contract/v1.1.0/CONTRACT.md` |
| ODR-V416-S3..S7 | A each: creation equality; lengths at the UI only; `projectionVersion` 1; a 1.0.0 investigation refuses 1.1.0-only commands; `label` required in EvidenceRevised |
| ODR-V416-S9 | A: `contract/v1.1.0/` |
| ODR-V416-CAT | A: INV-028 required, INV-045, INV-046 and INV-047 in the successor catalog `docs/rehearsal-010-readiness/proposals/causa-investigation.requirement-catalog.v416.proposed.json` |
| ODR-V416-02 | The package approval (RC-2, S-10), after X01-X21, an independent review and causa-storage 1.1.0's ST-01..ST-06 |

## Contract changes (`analysis/amendment.json` `contractChanges`)

| File | Decision | Change |
|---|---|---|
| `contracts/00-DOMAIN-STUBS.fs` | ODR-V416-01 | `CoreCommand`: `ReviseObservation of ReviseObservation` (after `RecordObservation`), `ReviseEvidence of ReviseEvidence` (after `AdmitEvidence`), `RecordConclusion of RecordConclusion` (after `RecordVerification`, RC-7 A). `CoreEvent`: `ObservationRevised of ObservationRevision` (after `ObservationRecorded`), `EvidenceRevised of EvidenceRevision` (after `EvidenceVerificationAssessed`). Payloads opaque, the domain owner's, exactly the wire records. |
| `contracts/02-IF41-STORAGE.fs` | ODR-V416-01 | Doc comment only: IF41-STORAGE is pinned to causa-storage 1.1.0. 1.0.0 file sets are read by the v1.0.0 rules unchanged. A 1.0.0 investigation is appended to only with events v1.0.0 stores, and anything else is refused as `CAUSA.UNREPRESENTABLE` before a write (S-6 A). Mixed versions are refused. No type changes. |
| `contracts/07-IF41-VIEW.fs` | ODR-V416-RC6 | `ConclusionCard.Strength: string`, the stored `conclusionStrength` (RC-6 A). The `EvidenceCard.Label` comment names 1.1.0. |

Every other contract equals v4.1.5's byte for byte. Any added union case is breaking (`AGENT-RULES.md`). Every consumer's contract tests change in the same pull request of the competition repository.

## RecordConclusion (RECORD-CONCLUSION.md section 4)

**Payload.** `RecordConclusion = Conclusion`, exactly the `ConclusionRecorded` wire record: `ConclusionId`, `Statement: NonEmptyText` (verbatim), `Strength: ConclusionStrength` (five cases), `CauseIds: CausalAssertionId list` (ordered, distinct), `ResidualUncertainty: NonEmptyText option`. The UI supplies INV-033 (statement), INV-046 (strength), INV-047 (residual uncertainty) and `CauseIds = []`. The command adds no field the wire lacks.

**Validation (Decide).** The order is as `Decide.decide`: semantics, duplicate, expected version, actor, then the checks below. Every failing check is returned.

| # | Rule | Blocker |
|---|---|---|
| V1 | The investigation is open for recording (Draft, Active, AwaitingEvidence, AwaitingVerification) | `investigation.read-only` |
| V2 | `ConclusionId` is new | `conclusion.exists` |
| V3 | Every cited cause exists | `cause.missing` |
| V4 | `CauseIds` has no duplicates (the command boundary removes them first) | `conclusion.duplicate-cause` |
| V5 | An open blocking unknown requires `ResidualUncertainty` | `CONCLUSION.RESIDUAL_REQUIRED` |
| V6 | `confirmedCause` cites only confirmed causes | `CONCLUSION.UNSUPPORTED` |
| V7 (RC-5 A) | `supportedCause` and `confirmedCause` cite at least one cause | `conclusion.no-cause` |
| V8 (RC-5 A) | `acceptedWithResidualUncertainty` states `ResidualUncertainty` | `conclusion.residual-not-stated` |

V1-V6 are the reference engine's replay rules, so a decided event always evolves. V7 and V8 are Decide-time policy (`causa.core/1`), not admissibility rules: a stored history the reference engine accepts still replays.

**Authorization.** The actor is human or system, never agent (an agent actor only on `ProposalRecorded`). The UI uses the fixed `causa-operator` human actor (OD-CR-07 A). The lifecycle is never changed. The capability `RecordConclusion` is probed with what the UI sends. While V5 blocks, it is `Unavailable [CONCLUSION.RESIDUAL_REQUIRED]` with the engine's reason.

**Events.** Exactly one `ConclusionRecorded`, a one-event command group whose `correlationId` is the `CommandId`. There is no `LifecycleChanged`.

**Replay.** Evolve, Replay, the wire and the projection are unchanged for this command, because `ConclusionRecorded` is a causa-storage v1.0.0 event. In a 1.0.0 investigation it stays 1.0.0 (S-6 A allows it). Stored histories replay to the same states and digests.

## causa-storage 1.1.0 references (STORAGE-1.1.md; causa-data `contract/v1.1.0`)

- **EvidenceRevised** `{evidenceId, label, source, sourceUrl}`: label required (S-7 A), source required, sourceUrl a nullable absolute http(s) URL. **ObservationRevised** `{observationId, original, source}`: source nullable.
- **Creation.** `AdmitEvidence` emits `EvidenceAdmitted` then always `EvidenceRevised` in the same group. `RecordObservation` emits `ObservationRevised` after `ObservationRecorded` only when INV-025 is entered. The revision repeats `source`/`original` byte for byte (S-3 A).
- **Revision.** `ReviseEvidence` and `ReviseObservation` emit one revision event each. Admitted evidence content is never revised (OD-CR-06 A).
- **Kinds.** `relationshipKind` gains `consistentWith` and `inconclusive` (INV-031). Neither is support: the cause gate counts only `supports`. `insufficient` and `discriminates` are shown verbatim (OD-HC-07 B).
- **Reassessment (S-8 B).** A new `EvidenceRelationshipRecorded` for the same pair; the latest is current (`LinkCard.Current`). CMD-REL-LINK's second-link refusal is dropped.
- **Versions.** New working-copy investigations are written as 1.1.0. A 1.0.0 investigation, which includes every catalog case, is read exactly as before and refuses 1.1.0-only commands (S-6 A). `projectionVersion` stays 1 (S-5 A). Lengths are enforced at the UI and the command boundary only (S-4 A).
- **Admissibility (S-2 B).** The 1.1.0 rules are normative in causa-data `contract/v1.1.0/CONTRACT.md` section 4.6. The pinned engine stays the oracle for every 1.0.0 rule.

## How the change is checked

- `requirements-compiler v41 --source . --package requirements-compiled/v4.1.6 --base requirements-compiled/v4`: X01-X16 and X18-X21 pass. X17 fails only with "names no operator approval" for the three contract changes, which take ODR-V416-01 and ODR-V416-RC6's approvals at FREEZE step 2.
- The contracts typecheck with `TreatWarningsAsErrors`, and both negative controls are still refused with FS1093.
- New producer tests: Domain `RecordConclusionEmitsOneConclusionRecorded` and `RecordConclusionRules` (V1-V8), and Storage `V110RevisionsAndKindsRoundTrip` and `V100InvestigationRefusesV110Events` (S-6 A).

No state, event, DAG task or contract file is added. No requirement, obligation or test binding is removed.
