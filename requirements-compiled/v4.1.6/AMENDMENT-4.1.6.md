# Amendment 4.1.6: RecordConclusion and causa-storage 1.1.0

v4.1.6 is the successor of the approved package v4.1.5 (APR-V415-OWNER-3, tag `indy-init-requirements-v4.1.5`). v4.1.5 is not edited.

**Authority.** OD-CR-01 A (APR-CR-OWNER-1/-2, recorded) commissions a causa-storage 1.1.0 and a v4.1.6 with the matching `CoreEvent` cases and a `RecordConclusion` command. The owner accepted every recommendation of section A of the decisions sheet ([`docs/v416-competition-decisions/DECISIONS-FOR-TONIGHT.md`](../../docs/v416-competition-decisions/DECISIONS-FOR-TONIGHT.md)) in session on 2026-11-06. **That answer is not a record.** The requests in `proposals/owner-decision-requests.json` (ODR-V416-*) are how the owner records it with `owner-freeze.fsx decide v4.1.6`. Each request's recommended option is the accepted one. None is recorded by this change.

| Request | Accepted answer, built into this package |
|---|---|
| ODR-V416-01 | The contract change below (00, 02) |
| ODR-V416-RC1 | One v4.1.6: RecordConclusion + the minimal causa-storage 1.1.0 |
| ODR-V416-S1 | B: EvidenceRevised, ObservationRevised, kinds `consistentWith` and `inconclusive`. The other four OD-CR-01 A events follow as causa-storage 1.2.0 and a later package (this narrows OD-CR-01 A) |
| ODR-V416-S8 | B: a new `EvidenceRelationshipRecorded` for the same pair is the reassessment; the latest is current. The successor catalog's INV-030 note says so, and the contract-readiness obligation T-IMPL-CMD-REL-LINK-02 ("second link for the pair refused") is superseded (`analysis/amendment.json` `supersededObligations`, change V416-S8) |
| ODR-V416-RC3 | A: J2-03 and J2-07 choose a category and a strength (`verification/journeys/j2-analysis.spec.mjs`, owner-controlled) |
| ODR-V416-RC4 | A: optional catalog field INV-047 "Residual uncertainty" |
| ODR-V416-RC5 | A: Decide-time rules V7 and V8 |
| ODR-V416-RC6 | A: `ConclusionCard.Strength` (07) |
| ODR-V416-RC7 | A: `RecordConclusion` directly after `RecordVerification` |
| ODR-V416-S2 | B: the 1.1.0 rules are normative in causa-data `contract/v1.1.0/CONTRACT.md` |
| ODR-V416-S3..S7 | A each: creation equality; lengths at the UI only; `projectionVersion` 1; a 1.0.0 investigation refuses 1.1.0-only commands; `label` required in EvidenceRevised |
| ODR-V416-S9 | A: `contract/v1.1.0/` |
| ODR-V416-CAT | A: INV-028 required, INV-030's note amended to S-8 B, INV-045, INV-046 and INV-047 in the successor catalog `docs/rehearsal-010-readiness/proposals/causa-investigation.requirement-catalog.v416.proposed.json` |
| ODR-V416-WDL013 | A: WDL-013 verified per obligation (O1..O3 verbatim, P1, SU-BROWSER-CHROMIUM, WI-GEN-WDL); supersedes ODR-V4-10 A for WDL-013 only. **Requested only**: the plan entry and binding are added after it is recorded ([WDL-013-OBLIGATIONS.md](../../docs/v416-competition-decisions/WDL-013-OBLIGATIONS.md)). Not verified. |
| ODR-V416-SCOPE-SELECTORS | A (accepted in session, "accept c"; not yet recorded): adopt PR #176 for the v4.1.6 competition scope only, on its own digest (SS-1); retire the split plans (SS-2); merge #176 with the scope recompile in owner step 5 (SS-3) |
| ODR-V416-02 | The package approval (RC-2, S-10), after X01-X21, an independent review and causa-storage 1.1.0's ST-01..ST-06 |

## Contract changes (`analysis/amendment.json` `contractChanges`)

| File | Decision | Change |
|---|---|---|
| `contracts/00-DOMAIN-STUBS.fs` | ODR-V416-01 | `CoreCommand`: `ReviseObservation of ReviseObservation` (after `RecordObservation`), `ReviseEvidence of ReviseEvidence` (after `AdmitEvidence`), `RecordConclusion of RecordConclusion` (after `RecordVerification`, RC-7 A). `CoreEvent`: `ObservationRevised of ObservationRevision` (after `ObservationRecorded`), `EvidenceRevised of EvidenceRevision` (after `EvidenceVerificationAssessed`). Payloads opaque, the domain owner's, exactly the wire records. |
| `contracts/02-IF41-STORAGE.fs` | ODR-V416-01 | Doc comment only: IF41-STORAGE is pinned to causa-storage 1.1.0. 1.0.0 file sets are read by the v1.0.0 rules unchanged. A 1.0.0 investigation is appended to only with events v1.0.0 stores, and anything else is refused as `CAUSA.UNREPRESENTABLE` before a write (S-6 A). Mixed versions are refused. No type changes. |
| `contracts/07-IF41-VIEW.fs` | ODR-V416-RC6 | `ConclusionCard.Strength: string`, the stored `conclusionStrength` (RC-6 A). The `EvidenceCard.Label` comment names 1.1.0. |

Every other contract equals v4.1.5's byte for byte. Any added union case is breaking (`AGENT-RULES.md`). Every consumer's contract tests change in the same pull request of the competition repository.

## RecordConclusion ([proposals/RECORD-CONCLUSION.md](proposals/RECORD-CONCLUSION.md) section 4)

**Payload.** `RecordConclusion = Conclusion`, exactly the `ConclusionRecorded` wire record: `ConclusionId`, `Statement: NonEmptyText` (verbatim), `Strength: ConclusionStrength` (five cases), `CauseIds: CausalAssertionId list` (ordered, distinct), `ResidualUncertainty: NonEmptyText option`. The UI supplies INV-033 (statement), INV-046 (strength), INV-047 (residual uncertainty) and `CauseIds = []`. The command adds no field the wire lacks.

**The UI cites no cause.** The catalog has no field or action that records or selects a causal assertion (INV-001..047 name none; J1-J3 record none), so the UI always sends `CauseIds = []`. By V7, a conclusion with strength `supportedCause` or `confirmedCause` is therefore always refused from the UI (`conclusion.no-cause`), although INV-046 offers both (OD-CR-04 A: a radio group over the five wire values). No requirement needs them from the UI: R6.1-R6.4, INV-032/033/046 and J2-07/J2-09 ask for a recorded conclusion with a strength, and J2-07 chooses `plausibleExplanation`. The UI shows the refusal with V7's reason. Whether INV-046 should keep offering two strengths the UI can never record is a catalog-wording question for the owner (it changes no contract).

**Validation (Decide).** The order is as `Decide.decide`: semantics, duplicate, expected version, actor, then the checks below. Every failing check is returned.

| # | Rule | Blocker |
|---|---|---|
| V1 | The investigation is open for recording (Draft, Active, AwaitingEvidence, AwaitingVerification) | `investigation.read-only` |
| V2 | `ConclusionId` is new | `conclusion.exists` |
| V3 | Every cited cause exists | `cause.missing` |
| V4 | `CauseIds` has no duplicates. The command boundary removes them first; `RecordConclusionRules` exercises Decide's refusal with a command built past the boundary | `conclusion.duplicate-cause` |
| V5 | An open blocking unknown requires `ResidualUncertainty` | `CONCLUSION.RESIDUAL_REQUIRED` |
| V6 | `confirmedCause` cites only confirmed causes | `CONCLUSION.UNSUPPORTED` |
| V7 (RC-5 A) | `supportedCause` and `confirmedCause` cite at least one cause. The UI sends `CauseIds = []`, so from the UI these two strengths are always refused (see Payload) | `conclusion.no-cause` |
| V8 (RC-5 A) | `acceptedWithResidualUncertainty` states `ResidualUncertainty` | `conclusion.residual-not-stated` |

V1-V6 are the reference engine's replay rules, so a decided event always evolves. V7 and V8 are Decide-time policy (`causa.core/1`), not admissibility rules: a stored history the reference engine accepts still replays.

**Authorization.** The actor is human or system, never agent (an agent actor only on `ProposalRecorded`). The UI uses the fixed `causa-operator` human actor (OD-CR-07 A). The lifecycle is never changed. The capability `RecordConclusion` is probed with what the UI sends. While V5 blocks, it is `Unavailable [CONCLUSION.RESIDUAL_REQUIRED]` with the engine's reason.

**Events.** Exactly one `ConclusionRecorded`, a one-event command group whose `correlationId` is the `CommandId`. There is no `LifecycleChanged`.

**Replay.** Evolve, Replay, the wire and the projection are unchanged for this command, because `ConclusionRecorded` is a causa-storage v1.0.0 event. In a 1.0.0 investigation it stays 1.0.0 (S-6 A allows it). Stored histories replay to the same states and digests.

## causa-storage 1.1.0 references ([proposals/STORAGE-1.1.md](proposals/STORAGE-1.1.md); causa-data `contract/v1.1.0`)

- **EvidenceRevised** `{evidenceId, label, source, sourceUrl}`: label required (S-7 A), source required, sourceUrl a nullable absolute http(s) URL. **ObservationRevised** `{observationId, original, source}`: source nullable.
- **Creation.** `AdmitEvidence` emits `EvidenceAdmitted` then always `EvidenceRevised` in the same group. `RecordObservation` emits `ObservationRevised` after `ObservationRecorded` only when INV-025 is entered. The revision repeats `source`/`original` byte for byte (S-3 A).
- **Revision.** `ReviseEvidence` and `ReviseObservation` emit one revision event each. Admitted evidence content is never revised (OD-CR-06 A).
- **Kinds.** `relationshipKind` gains `consistentWith` and `inconclusive` (INV-031). Neither is support: the cause gate counts only `supports`. `insufficient` and `discriminates` are shown verbatim (OD-HC-07 B).
- **Reassessment (S-8 B).** A new `EvidenceRelationshipRecorded` for the same pair; the latest is current (`LinkCard.Current`). CMD-REL-LINK's second-link refusal is dropped.
- **Versions.** New working-copy investigations are written as 1.1.0. A 1.0.0 investigation, which includes every catalog case, is read exactly as before and refuses 1.1.0-only commands (S-6 A). `projectionVersion` stays 1 (S-5 A). Lengths are enforced at the UI and the command boundary only (S-4 A).
- **Consequence of S-6 A with S-7 A for 1.0.0 investigations.** `AdmitEvidence` always emits `EvidenceRevised` (S-3 A) with a required label (INV-027, S-7 A), so on **every** 1.0.0 investigation `AdmitEvidence` is refused before anything is written (`CAUSA.UNREPRESENTABLE`), and so are `RecordObservation` with a source (INV-025), `ReviseEvidence`, `ReviseObservation` and a link of kind `consistentWith` or `inconclusive`. The capability probe reports each as `Unavailable` with that reason (test `App.Producer.AdmitEvidenceUnavailableOnV100Investigation`). The other commands, `RecordConclusion` included, still append 1.0.0 events. Nothing in the approved v4.1.5 scope needs evidence added to a 1.0.0 investigation: catalog cases are read-only (EXT-DATA-013; v4.1.5 W1, `Writable.ofAddress` refuses a catalog, test `WritableRefusesCatalog`); EXT-DATA-008 forbids copying a demonstration investigation; a working copy is per run (`workspaces/demo/causa-app-<run>`), so in the competition it holds only investigations the v4.1.6 app created, which are 1.1.0; and J1-J3 each start a new investigation. The only 1.0.0 investigations the refusal reaches are working-copy investigations an earlier (v4.1.5) app wrote, for example a reused run id or a test repository populated before v4.1.6 (EXT-DATA-009's "separately populated test repository" is read, and written only through new investigations). No requirement or journey needs them writable, so no owner decision is requested; if the owner wants them writable, the options are a recorded per-investigation upgrade (a new 1.1.0 investigation) or keeping the refusal with its reason (CONTRACT 1.1.0 §13.2 forbids an in-place upgrade).
- **Admissibility (S-2 B).** The 1.1.0 rules are normative in causa-data `contract/v1.1.0/CONTRACT.md` section 4.6. The pinned engine stays the oracle for every 1.0.0 rule.

## How the change is checked

- `requirements-compiler v41 --source . --package requirements-compiled/v4.1.6 --base requirements-compiled/v4`: X01-X16 and X18-X21 pass. X17 fails only with "names no operator approval" for the three contract changes, which take ODR-V416-01 and ODR-V416-RC6's approvals at FREEZE step 2.
- The contracts typecheck with `TreatWarningsAsErrors`, and both negative controls are still refused with FS1093.
- New producer tests (`analysis/interface-specifications.json`, change V416-TESTS):
  - Domain: `RecordConclusionEmitsOneConclusionRecorded`, `RecordConclusionRules` (V1-V8), `ReviseEvidenceRules` and `ReviseObservationRules` (missing or superseded target, read-only investigation, label required), `AdmitEvidenceEmitsRevisionInGroup` (S-3 A: `EvidenceAdmitted` then `EvidenceRevised` in one group), `RelationshipKindsNeverSupport` (`consistentWith` and `inconclusive` never count for the cause gate), `LatestAssessmentIsCurrent` (S-8 B).
  - Storage: `V110RevisionsAndKindsRoundTrip`, `V100InvestigationRefusesV110Events` (S-6 A), `StoredHistoriesReplayUnchanged` (stored 1.0.0 histories and catalog events replay to the same states and digests).
  - App: `AdmitEvidenceUnavailableOnV100Investigation` (the S-6 A consequence above).
  - View: `ConclusionCardShowsStrength` (RC-6 A), `LinkCardLatestIsCurrent` (S-8 B).
- CI (`preflight-tooling.yml`) reproduces v4.1.6 byte for byte, requires that X17 is the only failing check and that its only problems are the three pending approvals, compares its contracts and layout tests with v4.1.5's, and typechecks the contracts with both negative controls refused. The bootstrap step is added once X17 passes.

No state, event, DAG task or contract file is added. No requirement or test binding is removed; one contract-readiness obligation, T-IMPL-CMD-REL-LINK-02, is superseded by ODR-V416-S8 B.
