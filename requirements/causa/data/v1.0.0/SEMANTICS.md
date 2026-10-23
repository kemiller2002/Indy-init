# Causa v1.0.0 domain semantics (admissibility)

| | |
|---|---|
| Status | Normative for Indy-init. Closes the delegation in [CONTRACT.md](CONTRACT.md) §1.3 and §11.3. |
| Applies to | `semanticVersion` `"1.0.0"` event histories (CONTRACT §5, §7) |
| Reference oracle | `kemiller2002/indy-init-rehearsal-001@55f49f61c939a3a780cee592f36f732a1328f981`, `src/engine/` (`Decide.decide`, `Evolve.apply`, `History.validate`) |
| Traceability | `File.fs:N` cites a line of that commit. Citations are **non-normative**; the text is normative. |

The key words MUST, MUST NOT, REQUIRED, SHALL, SHALL NOT, SHOULD, SHOULD NOT, RECOMMENDED, MAY and OPTIONAL are used as in
RFC 2119.

---

## 0. Purpose, scope and conformance

1. This document states, without reference to the engine's source, the decision rules that make a Causa v1.0.0 event
   history **semantically admissible** (CONTRACT §11.1 stage 15, `CAUSA.HISTORY_INADMISSIBLE`). An implementer who reads
   only this document and the wire contract MUST be able to decide, for any history, whether it is admissible and, if
   it is not, which **domain code** is the first error.
2. **Conformance.** An implementation of this document conforms when, for every history that reaches stage 15, it
   produces (a) the same verdict as the reference oracle (admissible iff the oracle's `History.validate` returns an
   empty list), and (b) for an inadmissible history, the same **first domain code** (one of `HISTORY.NO_START`,
   `HISTORY.SEQUENCE`, `HISTORY.INADMISSIBLE_EVENT`, `HISTORY.UNGATED_CONFIRMATION`, `HISTORY.UNGATED_RESOLUTION`).
   An implementation SHOULD also reproduce the complete ordered error list (§15.7), including the inner *rule codes*
   (`CAUSE.NO_ROOT`, `RESOLVE.NO_CORRECTION`, …) carried by each error. Messages are informative and need not match.
3. Where this document and the oracle disagree on a history, the disagreement is a defect in this document. It MUST be
   reported and resolved by changing this document, never by silently following either side. The places where the
   text depends on platform behaviour, or where the oracle's behaviour cannot be reproduced from text alone, are listed
   in §17 (Engine-dependence register). Inside those places, the oracle wins until the register entry is resolved.
4. **Scope.** This document covers stage 15 only. Stages 1–14 (framing, JSON, canonical form, versions, schema, manifest,
   hashes, ids, sequence, event-id derivation, correlation reuse) and stages 16–17 (snapshot) are defined by CONTRACT
   §11.1. The rules here are written so that they also give a defined answer for histories that did not pass those
   stages (for example, wrong `aggregateVersion` values), because the oracle does; §15.9 states which rules can fire
   first once stages 1–14 have passed.
5. The decision function (§8) is also the **command-side** semantics: a writer that issues a command MUST persist
   exactly the events this document says the command emits, or nothing when it is refused (CONTRACT §9.2). Section 18.2
   uses this to cover the refusals recorded in `golden/scenarios.json`.

## 1. Conventions

1. **Wire names.** Entity fields, payload fields and enumeration values use the wire names of CONTRACT §6–§8
   (lowerCamelCase). Where the engine name differs, it is given in parentheses at first use, e.g. unknown state
   `resolved` (`UnknownResolved`). Command names (§6) are the engine's command names; commands have no wire form.
2. **Rule codes.** Every refusal carries a *code* (e.g. `RESOLVE.NO_CORRECTION`) and a *rule tag* naming the
   requirement it enforces (e.g. `R7.2`, `D-004`). Both are given verbatim with each rule; §16 indexes them.
3. **Error list.** A refusal is a non-empty, ordered list of errors `{code, rule, message}`. Order is significant (§1.5).
4. **Notation.**
   - `S` is a domain state (§4); `⊥` means "no state yet" (before `InvestigationStarted`).
   - `S.hypotheses[h]` is the hypothesis with id `h`, if it exists. `h ∈ S.hypotheses` means it exists.
   - `vals(S.X)` is the list of entities of collection `X` in **id order** (§3.4).
   - `[]` is the empty list, `xs ++ ys` list concatenation, `null` the absent optional value.
5. **Check combinators.** Rules are written with two combinators. Implementations MUST evaluate them exactly so,
   because they determine which errors are reported together and in what order.
   - `collect(c₁, …, cₙ)`: evaluate **every** check `cᵢ`. Each yields a (possibly empty) error list. The result is
     `errors(c₁) ++ … ++ errors(cₙ)`. If it is non-empty, the command is refused with that list. (Engine `both`,
     `Decide.fs:367-371`; nested `both` calls flatten left to right.)
   - `then`: a later step runs only if every earlier step produced no error. A step that fails refuses the command
     with its own list; later steps are not evaluated. (Engine `Result.bind`.)
6. **Generic checks** used by many commands (`Decide.fs:344-365`):

   | check | yields | code | rule tag |
   |---|---|---|---|
   | `text(s)` | `[]` if `s` is non-blank (§3.2), else one error | `VALIDATION.EMPTY` | `schema` |
   | `absent(X, id)` | `[]` if `id ∉ S.X`, else one error | `DUPLICATE_ID` | `schema` |
   | `found(X, id)` | `[]` if `id ∈ S.X`, else one error | `NOT_FOUND` | `schema` |
   | `allExist(X, ids)` | `[]` if every element of `ids` is in `S.X` (vacuously true for `[]`), else **one** error | `NOT_FOUND` | `schema` |
   | `transition(T, from, cmd)` | `[]` and the target if `(from, cmd)` is in table `T` (§7), else one error | `TRANSITION.ILLEGAL` | `STATE-TRANSITION-TABLES` |
   | `opt(s)` | `null` if `s` is `null` or blank, else `s` (not a check; a normalisation) | — | — |

## 2. Input: the decoded history

1. The input is the ordered list of events of one investigation, in `aggregateVersion` order (file order). For each
   event the rules read only these envelope fields: `investigationId`, `aggregateVersion`, `eventType`, `occurredAt`,
   `actor`, `correlationId`, and the `payload`. `eventId`, `formatVersion` and `semanticVersion` are not read.
2. Each payload is decoded to typed values exactly as CONTRACT §7 and FIELD-MAPPING.md define:
   - `LifecycleChanged.command` maps to the engine command strings `ActivateInvestigation`, `MarkAwaitingEvidence`,
     `MarkAwaitingVerification`, `ResolveInvestigation`, `CloseInvestigation`, `ReopenInvestigation`,
     `VerificationFailed`; `HypothesisTransitioned.command` maps to `MarkPlausible`, `MarkSupported`, `MarkWeakened`,
     `Falsify`, `Supersede`, `Reopen` (FIELD-MAPPING.md "Engine command strings"). This document keeps the wire
     spellings; the mapping is a bijection, so nothing depends on which spelling is used internally.
   - `HypothesisTransitioned` carries two implicit fields that are not persisted (CONTRACT §7.2): `at` is the event's
     `occurredAt` and `aggregateVersion` is the event's `aggregateVersion`. They take part in the equality of §15.6.
   - `EvidenceAdmitted` decodes to *text content* (`contentKind = "text"`) or *artifact content*
     (`contentKind = "artifact"`); the two kinds are different values even if `content` is the same string.
3. The **actor** is the triple `(actorId, kind, provider/model)`. Two actors are equal iff `actorId`, `kind` and, for
   `agent`, `provider` and `model` are equal strings. Only `kind = "agent"` is distinguished by any rule; `human` and
   `system` are treated identically everywhere (`Decide.fs:553,562,614`).
4. When stages 1–14 have passed, the input additionally satisfies: versions are exactly `1..N`; every event names the
   same investigation; text fields are non-blank; identifier arrays are duplicate-free; all enumeration values are in
   the wire sets (including the `*Target` restrictions); agents appear only on `ProposalRecorded`; the payload
   invariants of CONTRACT §7 hold (e.g. `HypothesisTransitioned` `command`/`to` agreement, non-empty `evidenceIds`
   for evidence-requiring commands, `NegativeKnowledgeEstablished.basisEvidenceIds` non-empty, `members` non-empty,
   satisfied verifications cite evidence). This document does not rely on these guarantees; §15.9 lists which rules
   they make unreachable.

## 3. Value semantics and primitive predicates

1. **Equality.**
   - Strings are equal iff they are the same sequence of UTF-16 code units (ordinal). No trimming, case folding or
     normalisation is applied except where a rule says so explicitly (§10.27).
   - UUIDs are equal iff their 128-bit values are equal. On the wire every UUID is canonical lowercase text, so text
     equality is equivalent.
   - Timestamps are equal iff they denote the same instant. (On the wire every timestamp is canonical UTC with
     milliseconds, so text equality is equivalent.)
   - Lists are equal iff they have the same length and are pairwise equal in order. `null` equals only `null`.
   - Records (event payloads, causal members) are equal iff all fields are equal.
2. **Blank text.** A string is *blank* iff it is `null`, empty, or every UTF-16 code unit is one of:
   U+0009–U+000D, U+0020, U+0085, U+00A0, U+1680, U+2000–U+200A, U+2028, U+2029, U+202F, U+205F, U+3000
   (CONTRACT §2.6; engine `String.IsNullOrWhiteSpace`, `Primitives.fs:84-86`). *Non-blank* is the negation.
3. **Trim and invariant lowercase** (used only by §10.27):
   - `trim(s)` removes every leading and trailing code unit of the whitespace set in §3.2 (engine `String.Trim()`).
   - `lowerInvariant(s)` maps each Unicode code point (surrogate pairs decoded) to its **simple** (one-to-one)
     lowercase mapping, except that U+0130 maps to itself; code points without a lowercase mapping are unchanged.
     Full/special case mappings (e.g. U+0130 → "i̇", final sigma) MUST NOT be used. (Engine `ToLowerInvariant`; see
     §17 E-5 for the platform dependence.)
   - `norm(s) = lowerInvariant(trim(s))`.
4. **Id order.** Wherever this document iterates a collection, it does so in ascending *id order*: ordinal comparison of
   the canonical lowercase UUID text, which is the same as unsigned comparison of the 16 bytes in text order (engine
   `Map` ordered by `Guid.CompareTo`, `Primitives.fs:19-23`; §17 E-7). Iteration order affects only the order and
   multiplicity of inner errors, never a verdict or a first domain code.
5. **Policy id.** The string `core-default/1` (`Decide.fs:84`). It is the `policy` value of every admissible
   `CausalAssertionConfirmed`.

## 4. Domain state model

### 4.1 Investigation

The state `S` (engine `Investigation`, `Model.fs:318-340`) is created by `InvestigationStarted` and updated by every
later event (§5). It is exactly the `state` object of CONTRACT §8.2 before the final sorting step, with entity arrays
viewed as maps keyed by id.

| field | type | set by | read by rules |
|---|---|---|---|
| `investigationId` | uuid | `InvestigationStarted` (envelope) | §8 step 3 |
| `title` | text | `InvestigationStarted` | — |
| `lifecycle` | `lifecycle` enum | `InvestigationStarted` (`draft`), `LifecycleChanged` | §8, §9, §11, §12.3 |
| `version` | integer | every event (its `aggregateVersion`) | §10.4, §12, §15.3 |
| `createdAt` | timestamp | `InvestigationStarted` | — |
| 17 entity collections | maps id → entity | §5 | see below |

### 4.2 Entities

Every entity also has `provenance = {actor, recordedAt}` (CONTRACT §8.3); no rule reads provenance. Fields marked
"—" in the last column are stored but read by no rule.

| entity (engine type) | field | type | initial value | read by |
|---|---|---|---|---|
| observation (`Observation`) | `id`, `original`, `observedAt` | uuid, text, timestamp? | from event | `absent` only |
| hypothesis (`Hypothesis`) | `id` | uuid | | §10, §11 |
| | `statement` | text | | §10.27 (repetition) |
| | `mechanism` | text? | | — |
| | `state` | `hypothesisState` | `proposed` | §7.1, §11 |
| | `history` | list of hypothesis-change `{from, to, command, reason, evidenceIds, at, aggregateVersion}` | `[]` | §10.3 (latest falsification) |
| evidence (`Evidence`) | `id`, `contentKind`, `content`, `category`, `source`, `capturedAt` | | from event | — |
| | `verification` (`Verification`) | `verificationStatus` | `unverified` | **— (no rule reads it, §17 E-10)** |
| | `supersededBy` (`SupersededBy`) | uuid? | `null` | §4.4 `active`, §10.5 |
| | `admittedAtVersion` (`AdmittedAt`) | integer | `aggregateVersion` of the admitting event | §10.3, §12 |
| relationship (`Relationship`) | `id`, `evidenceId`, `hypothesisId`, `kind`, `rationale` | | from event | §11 (`supports`, `contradicts`) |
| unknown (`Unknown`) | `id`, `question`, `materiality`, `state`, `basis` | | `state = identified`, `basis = null` | §7.2, §11, §13, §14 |
| assumption (`Assumption`) | `id`, `proposition`, `materiality`, `state` | | `state = explicitUnverified` | §7.4, §11 |
| contradiction (`Contradiction`) | `id`, `statement`, `evidenceIds`, `materiality`, `state`, `basis` | | `state = detected`, `basis = null` | §7.3, §11, §14 |
| prediction (`Prediction`) | `id`, `hypothesisId`, `statement`, `strength`, `timing` | | from event | `absent` only |
| action (`InvestigationAction`) | `id`, `kind`, `purpose`, `targetHypothesisIds` (`Targets`), `state` | | `state = proposed` | §7.5, §10.17, §10.20 |
| finding (`Finding`) | `id`, `actionId`, `originalResult`, `interpretation` | | from event | §10.17 (prediction timing) |
| negativeKnowledge (`NegativeKnowledge`) | `id`, `statement`, `scope`, `basisEvidenceIds` | | from event | §10.27 (repetition) |
| cause (`CausalAssertion`) | `id`, `outcome`, `members[] {hypothesisId, role}`, `mechanism`, `state` | | `state = proposed` | §7.6, §10, §11, §13, §14 |
| correctiveAction (`CorrectiveAction`) | `id`, `kind`, `description`, `targetCauseIds`, `state` | | `state = proposed` | §7.7, §9, §12, §14 |
| | `awaitingSinceVersion` (`AwaitingSince`) | integer | `0` | §12 (verification round) |
| criterion (`Criterion`) | `id`, `correctiveActionId`, `expectedOutcome`, `method` | | from event | §12, §14 |
| verification (`Verification`) | `id`, `criterionId`, `outcome`, `evidenceIds` | | from event | §12, §14 |
| | `recordedAtVersion` (`Sequence`) | integer | `aggregateVersion` of the recording event | §12 |
| conclusion (`Conclusion`) | `id`, `statement`, `strength`, `causeIds`, `residualUncertainty` | | from event | §14 |
| | `recordedAtVersion` (`Sequence`) | integer | `aggregateVersion` of the recording event | §14 (latest conclusion) |
| proposal (`Proposal`) | `id`, `kind`, `text`, `rationale`, `state`, `decision` | | `state = active`, `decision = null` | §7.8, §10.28–§10.30 |

### 4.3 Derivation from the wire projection

1. Every field above is persisted in the CONTRACT §8.3 projection under the same wire name, so a state can be
   reconstructed losslessly from a valid `snapshot.json` `state` object by turning each entity array into a map by
   `id`. **No engine field read by any rule is absent from §8.3.** Engine-only copies (`HypothesisChange.At`,
   `HypothesisChange.Sequence`) are persisted in the snapshot as `history[].at` and `history[].aggregateVersion`.
2. Implementations MUST nevertheless derive the state by replaying events (§5), not from a snapshot: the snapshot is
   checked only after stage 15 (CONTRACT §8.1, §11.1).

### 4.4 Derived predicates

These are the only derived notions the rules use. Each is evaluated on a given state `S`.

| predicate | definition | engine |
|---|---|---|
| `isOpen(l)` | `l ∈ {draft, active, awaitingEvidence, awaitingVerification}` | `Decide.fs:77-80` |
| `openUnknown(u)` | `u.state ∈ {identified, investigating}` | `Decide.fs:88-91` |
| `openContradiction(c)` | `c.state ∈ {detected, triaged, investigating}` | `Decide.fs:93-96` |
| `material(m)` | `m ∈ {blocking, material}` | `Decide.fs:98-101` |
| `active(e)` (evidence id) | `e ∈ S.evidence` and `S.evidence[e].supersededBy = null` | `Decide.fs:107-108` |
| `round(ca)` (corrective-action id) | `S.correctiveActions[ca].awaitingSinceVersion` if `ca` exists, else `0` | `Decide.fs:196-197` |
| `fresh(r, e)` | `active(e)` and `S.evidence[e].admittedAtVersion > r` | `Decide.fs:200-201` |
| `criteriaOf(ca)` | criteria `c` in id order with `c.correctiveActionId = ca` | `Decide.fs:192-193` |
| `satisfied(c)` (criterion) | let `r = round(c.correctiveActionId)`; there exists a verification `v` with `v.criterionId = c.id`, `v.outcome = satisfied`, `v.recordedAtVersion > r`, `v.evidenceIds ≠ []`, and `fresh(r, e)` for every `e ∈ v.evidenceIds` | `Decide.fs:205-209` |
| `allSatisfied(ca)` | `criteriaOf(ca) ≠ []` and every `c ∈ criteriaOf(ca)` is `satisfied(c)` | `Decide.fs:214-216` |
| `blockingUnknowns` | unknowns in id order with `openUnknown(u)` and `u.materiality = blocking` | `Decide.fs:111-113` |
| `unresolvedMaterialUnknowns` | unknowns with `openUnknown(u)` and `material(u.materiality)` | `Decide.fs:116-118` |
| `latestConclusion` | the conclusion with the greatest `recordedAtVersion`, or none | `Decide.fs:248` |

## 5. State application (evolve)

1. `apply(S, e)` applies one event. Before `InvestigationStarted`, `S = ⊥`.
2. `apply(⊥, e)`: if `e` is `InvestigationStarted`, the result is a new state (row 1 below); otherwise the result is
   `⊥` (the event is ignored). (`Decide.fs:278-285` and `Decide.fs:803`: the engine's `Evolve.apply` raises on a
   non-start event without state; `History.validate` never calls it in that case and keeps `⊥`.)
3. `apply(S, e)` with `S ≠ ⊥`: first set `S.version ← e.aggregateVersion` (for every event, even one that changes
   nothing else, `Decide.fs:287`), then apply the row for `e.eventType`.
4. **Creation rows** ("new X") insert the entity under its id with provenance `{actor: e.actor, recordedAt:
   e.occurredAt}`. If an entity with that id already exists, the new entity **replaces** it entirely (state, history,
   provenance included). This happens only in inadmissible histories (the creating command would have been refused
   with `DUPLICATE_ID`), but implementations MUST replicate it to reproduce the full error list (§15.7).
5. **Update rows** change the named entity. If the id does not exist, the event changes nothing except `version`.
6. Event rows (engine `Decide.fs:288-332`; CONTRACT §8.3 table, which states the same effects):

| # | event | effect on `S` | engine | §8.3 |
|---|---|---|---|---|
| 1 | `InvestigationStarted` | from `⊥`: new state `{investigationId: e.investigationId, title, lifecycle: draft, version: e.aggregateVersion, createdAt: e.occurredAt}`, `observations = {observationId: {original, observedAt: null}}`, all other collections empty. From `S ≠ ⊥`: only `version` changes. | `:278-284`, `:289` | same (the `S ≠ ⊥` case is not stated in §8.3) |
| 2 | `LifecycleChanged` | `lifecycle ← to` (the `from`, `command`, `reason` fields are not stored) | `:290` | same |
| 3 | `ObservationRecorded` | new observation `{original, observedAt}` | `:291` | same |
| 4 | `HypothesisRecorded` | new hypothesis `{statement, mechanism, state: proposed, history: []}` | `:292-293` | same |
| 5 | `HypothesisTransitioned` | hypothesis `state ← to`; append `{from, to, command, reason, evidenceIds, at: e.occurredAt, aggregateVersion: e.aggregateVersion}` to `history` | `:294-295` | same |
| 6 | `EvidenceAdmitted` | new evidence `{contentKind, content, category, source, capturedAt, verification: unverified, supersededBy: null, admittedAtVersion: e.aggregateVersion}` | `:296-297` | same |
| 7 | `EvidenceSuperseded` | evidence `evidenceId`: `supersededBy ← replacementEvidenceId` | `:298` | same |
| 8 | `EvidenceVerificationAssessed` | evidence `verification ← verification` | `:299` | same |
| 9 | `EvidenceRelated` | new relationship `{evidenceId, hypothesisId, kind, rationale}` | `:300-301` | same |
| 10 | `UnknownIdentified` | new unknown `{question, materiality, state: identified, basis: null}` | `:302` | same |
| 11 | `UnknownTransitioned` | unknown `state ← state`, `basis ← basis` | `:303` | same |
| 12 | `AssumptionRecorded` | new assumption `{proposition, materiality, state: explicitUnverified}` | `:304` | same |
| 13 | `AssumptionTransitioned` | assumption `state ← state` | `:305` | same |
| 14 | `ContradictionRecorded` | new contradiction `{statement, evidenceIds, materiality, state: detected, basis: null}` | `:306-307` | same |
| 15 | `ContradictionTransitioned` | contradiction `state ← state`, `basis ← basis` | `:308` | same |
| 16 | `PredictionRecorded` | new prediction `{hypothesisId, statement, strength, timing}` | `:309-310` | same |
| 17 | `ActionRecorded` | new action `{kind, purpose, targetHypothesisIds, state: proposed}` | `:311-312` | same |
| 18 | `ActionTransitioned` | action `state ← state` | `:313` | same |
| 19 | `FindingRecorded` | new finding `{actionId, originalResult, interpretation}` | `:314` | same |
| 20 | `NegativeKnowledgeEstablished` | new negativeKnowledge `{statement, scope, basisEvidenceIds}` | `:315-316` | same |
| 21 | `CausalAssertionRecorded` | new cause `{outcome, members, mechanism, state: proposed}` | `:317-318` | same |
| 22 | `CausalAssertionConfirmed` | cause `state ← confirmed` (`policy` is not stored) | `:319` | same |
| 23 | `CausalAssertionReopened` | cause `state ← reopened` (`reason` is not stored) | `:320` | same |
| 24 | `CorrectiveActionRecorded` | new correctiveAction `{kind, description, targetCauseIds, state: proposed, awaitingSinceVersion: 0}` | `:321-322` | same |
| 25 | `CorrectiveActionTransitioned` | correctiveAction `state ← state`; if `state = awaitingVerificationOfEffect` then `awaitingSinceVersion ← e.aggregateVersion`, otherwise `awaitingSinceVersion` is unchanged | `:323-324` | same |
| 26 | `CriterionRecorded` | new criterion `{correctiveActionId, expectedOutcome, method}` | `:325` | same |
| 27 | `VerificationRecorded` | new verification `{criterionId, outcome, evidenceIds, recordedAtVersion: e.aggregateVersion}` | `:326` | same |
| 28 | `ConclusionRecorded` | new conclusion `{statement, strength, causeIds, residualUncertainty, recordedAtVersion: e.aggregateVersion}` | `:327-328` | same |
| 29 | `ProposalRecorded` | new proposal `{kind, text, rationale, state: active, decision: null}` | `:329-330` | same |
| 30 | `ProposalWasAccepted` | proposal `state ← accepted` (`targetId` is not stored; the created entity comes from the preceding event of the group) | `:331` | same |
| 31 | `ProposalWasRejected` | proposal `state ← rejected`, `decision ← reason` | `:332` | same |

7. Differences from CONTRACT §8.3, all confined to inadmissible histories: §5.2 (events before a start are ignored),
   §5.4 (duplicate creation replaces), row 1 for `S ≠ ⊥` (a second `InvestigationStarted` changes only `version`).
   §8.3's final "sort every entity array by id" is a presentation step; it equals id order (§3.4).

## 6. Command catalog

1. A **command** is what one command group records (CONTRACT §5.4). `decide(S, envelope)` (§8) either refuses the
   command with an error list or returns the ordered list of event bodies to persist. The envelope is
   `{investigationId, actor, at, command}`; in history validation `at` is the group's `occurredAt` (§15.5).
2. The table lists every command, its inputs (wire field names of the events it emits), and its **exact** successful
   output. Event bodies are written `EventType{field: value, …}`; fields not mentioned are copied verbatim from the
   command input of the same name. `opt(·)` is §1.6. "`[A, B]`" is a group of two events in that order.

| # | command (engine) | inputs | events emitted on success |
|---|---|---|---|
| C1 | `StartInvestigation` | `investigationId`, `title`, `observationId`, `original` | `[InvestigationStarted{title: t, observationId, original}]` where `t = original` if `title` is blank, else `title` |
| C2 | `ActivateInvestigation` | — | `[LifecycleChanged{from: S.lifecycle, to: active, command: activateInvestigation, reason: null}]` |
| C3 | `MarkAwaitingEvidence` | `reason` | `[LifecycleChanged{from, to: awaitingEvidence, command: markAwaitingEvidence, reason}]` |
| C4 | `MarkAwaitingVerification` | — | `[LifecycleChanged{from, to: awaitingVerification, command: markAwaitingVerification, reason: null}]` |
| C5 | `ResolveInvestigation` | — | `[LifecycleChanged{from, to: resolved, command: resolveInvestigation, reason: null}]` |
| C6 | `CloseInvestigation` | — | `[LifecycleChanged{from, to: closed, command: closeInvestigation, reason: null}]` |
| C7 | `ReopenInvestigation` | `reason` (engine `basis`) | `[LifecycleChanged{from, to: active, command: reopenInvestigation, reason}]` |
| C8 | `RecordObservation` | `observationId`, `original`, `observedAt` | `[ObservationRecorded{…}]` |
| C9 | `RecordHypothesis` | `hypothesisId`, `statement`, `mechanism` | `[HypothesisRecorded{…, mechanism: opt(mechanism)}]` |
| C10 | `TransitionHypothesis` | `hypothesisId`, `command` ∈ hypothesisCommand, `reason`, `evidenceIds` | `[HypothesisTransitioned{hypothesisId, from: h.state, to: target (§7.1), command, reason, evidenceIds}]` with implicit `at = envelope.at`, `aggregateVersion = S.version + 1` |
| C11 | `AdmitEvidence` | `evidenceId`, content (text or artifact URL), `category`, `source`, `capturedAt` | `[EvidenceAdmitted{…}]` (content kind preserved) |
| C12 | `SupersedeEvidence` | `evidenceId` (old), `replacementEvidenceId`, `reason` | `[EvidenceSuperseded{…}]` |
| C13 | `VerifyEvidence` | `evidenceId`, `verification` | `[EvidenceVerificationAssessed{…}]` |
| C14 | `RelateEvidence` | `relationshipId`, `evidenceId`, `hypothesisId`, `kind`, `rationale` | `[EvidenceRelated{…, rationale: opt(rationale)}]` |
| C15 | `IdentifyUnknown` | `unknownId`, `question`, `materiality` | `[UnknownIdentified{…}]` |
| C16 | `TransitionUnknown` | `unknownId`, unknown command, `basis` | `[UnknownTransitioned{unknownId, state: target (§7.2), basis}]` |
| C17 | `RecordAssumption` | `assumptionId`, `proposition`, `materiality` | `[AssumptionRecorded{…}]` |
| C18 | `TransitionAssumption` | `assumptionId`, assumption command | `[AssumptionTransitioned{assumptionId, state: target (§7.4)}]` |
| C19 | `RecordContradiction` | `contradictionId`, `statement`, `evidenceIds`, `materiality` | `[ContradictionRecorded{…}]` |
| C20 | `TransitionContradiction` | `contradictionId`, contradiction command, `basis` | `[ContradictionTransitioned{contradictionId, state: target (§7.3), basis}]` |
| C21 | `RecordPrediction` | `predictionId`, `hypothesisId`, `statement`, `strength`, requested `timing` | `[PredictionRecorded{…, timing: decided timing (§10.17)}]` |
| C22 | `RecordAction` | `actionId`, `kind`, `purpose`, `targetHypothesisIds` | `[ActionRecorded{…}]` |
| C23 | `TransitionAction` | `actionId`, action command | `[ActionTransitioned{actionId, state: target (§7.5)}]` |
| C24 | `RecordFinding` | `findingId`, `actionId`, `originalResult`, `interpretation` | depends on the action's state `a` (§10.20): `inProgress` → `[FindingRecorded{…}, ActionTransitioned{actionId, state: completed}]`; `approved` → `[ActionTransitioned{actionId, state: inProgress}, FindingRecorded{…}, ActionTransitioned{actionId, state: completed}]`; `completed` → `[FindingRecorded{…}]`. `FindingRecorded.interpretation = opt(interpretation)`. |
| C25 | `EstablishNegativeKnowledge` | `negativeKnowledgeId`, `statement`, `scope`, `basisEvidenceIds` | `[NegativeKnowledgeEstablished{…}]` |
| C26 | `RecordCausalAssertion` | `causeId`, `outcome`, `members`, `mechanism` | `[CausalAssertionRecorded{…, mechanism: opt(mechanism)}]` |
| C27 | `ConfirmCause` | `causeId` | `[CausalAssertionConfirmed{causeId, policy: "core-default/1"}]` |
| C28 | `ReopenCause` | `causeId`, `reason` | `[CausalAssertionReopened{…}]` |
| C29 | `RecordCorrectiveAction` | `correctiveActionId`, `kind`, `description`, `targetCauseIds` | `[CorrectiveActionRecorded{…}]` |
| C30 | `TransitionCorrection` | `correctiveActionId`, correction command | `[CorrectiveActionTransitioned{correctiveActionId, state: target (§7.7)}]` |
| C31 | `RecordCriterion` | `criterionId`, `correctiveActionId`, `expectedOutcome`, `method` | `[CriterionRecorded{…, method: opt(method)}]` |
| C32 | `RecordVerification` | `verificationId`, `criterionId`, `outcome`, `evidenceIds` | `[VerificationRecorded{…}] ++ followOns` (§12.3): `failed` → `CorrectiveActionTransitioned{ca, state: verificationFailed}`, then, if `S.lifecycle = awaitingVerification`, `LifecycleChanged{from: awaitingVerification, to: active, command: verificationFailed, reason: "A verification failed; work reopens (A21)."}`; `satisfied` and all criteria of `ca` satisfied after recording → `CorrectiveActionTransitioned{ca, state: verifiedEffective}`; otherwise nothing |
| C33 | `RecordConclusion` | `conclusionId`, `statement`, `strength`, `causeIds`, `residualUncertainty` | `[ConclusionRecorded{…, residualUncertainty: opt(residualUncertainty)}]` |
| C34 | `RecordProposal` | `proposalId`, `kind`, `text`, `rationale` | `[ProposalRecorded{…, rationale: opt(rationale)}]` |
| C35 | `AcceptProposal` | `proposalId`, `targetId` | events of the target command (§10.29) followed by `ProposalWasAccepted{proposalId, targetId}`: kind `hypothesis` → `[HypothesisRecorded{hypothesisId: targetId, statement: p.text, mechanism: null}, ProposalWasAccepted{…}]`; `unknown` → `[UnknownIdentified{unknownId: targetId, question: p.text, materiality: material}, ProposalWasAccepted{…}]`; `action` → `[ActionRecorded{actionId: targetId, kind: test, purpose: p.text, targetHypothesisIds: []}, ProposalWasAccepted{…}]` |
| C36 | `RejectProposal` | `proposalId`, `reason` | `[ProposalWasRejected{…}]` |

3. The command name strings carried by `LifecycleChanged.command` are fixed per command (C2–C7); the only other value,
   `verificationFailed`, is emitted solely as the C32 follow-on and is not a command (FIELD-MAPPING.md).
4. Commands C2–C7 are **lifecycle commands**; C8–C36 and C1 (on an existing state) are **content commands**.
5. Engine: `Commands.fs:44-80` (command shapes), `Commands.fs:96-127` (event bodies), emitting code per command in
   `Decide.fs:407-607` (line ranges in §10).

## 7. Transition tables

A pair not listed is illegal and yields `TRANSITION.ILLEGAL` (rule tag `STATE-TRANSITION-TABLES`) where the command
uses `transition(…)`. Wire state names first; engine names in parentheses where they differ.

### 7.1 Hypothesis (`Decide.fs:9-19`)

| from \ command | `markPlausible` | `markSupported` | `markWeakened` | `falsify` | `supersede` | `reopen` |
|---|---|---|---|---|---|---|
| `proposed` | `plausible` | — | — | `falsified` | `superseded` | — |
| `plausible` | — | `supported` | `weakened` | `falsified` | `superseded` | — |
| `supported` | — | — | `weakened` | `falsified` | `superseded` | — |
| `weakened` | `plausible` | `supported` | — | `falsified` | `superseded` | — |
| `falsified` | — | — | — | — | — | `plausible` |
| `superseded` | — | — | — | — | — | — |

Evidence-requiring commands (`Decide.fs:22-25`): `markSupported`, `markWeakened`, `falsify`, `reopen`.

### 7.2 Unknown (`Decide.fs:27-32`)

| from \ command | `StartInvestigatingUnknown` | `ResolveUnknown` | `AcceptUnknownAsResidual` |
|---|---|---|---|
| `identified` | `investigating` | `resolved` (`UnknownResolved`) | `acceptedResidual` |
| `investigating` | — | `resolved` | `acceptedResidual` |
| `resolved`, `acceptedResidual`, `superseded` (`UnknownSuperseded`) | — | — | — |

### 7.3 Contradiction (`Decide.fs:34-39`)

| from \ command | `TriageContradiction` | `ResolveContradiction` | `AcceptContradictionAsResidual` |
|---|---|---|---|
| `detected` | `triaged` | `resolved` (`ContradictionResolved`) | `acceptedResidual` (`ContradictionAcceptedResidual`) |
| `triaged` | — | `resolved` | `acceptedResidual` |
| `investigating` (`ContradictionInvestigating`) | — | `resolved` | `acceptedResidual` |
| `resolved`, `acceptedResidual`, `superseded` | — | — | — |

No v1 command reaches `investigating` or `superseded`.

### 7.4 Assumption (`Decide.fs:41-46`)

| from \ command | `ProvisionallyAcceptAssumption` | `VerifyAssumption` | `RejectAssumption` |
|---|---|---|---|
| `explicitUnverified` | `provisionallyAccepted` | `verified` (`AssumptionVerified`) | `rejected` |
| `provisionallyAccepted` | — | `verified` | `rejected` |
| `verified`, `rejected`, `expired` | — | — | — |

### 7.5 Action (`Decide.fs:48-54`)

| from \ command | `ApproveAction` | `RejectAction` | `StartAction` | `CancelAction` |
|---|---|---|---|---|
| `proposed` (`ActionProposed`) | `approved` | `rejected` | — | — |
| `approved` | — | — | `inProgress` | `cancelled` |
| `inProgress` | — | — | — | `cancelled` |
| `completed`, `interpreted`, `rejected`, `cancelled` | — | — | — | — |

The transitions `approved → inProgress → completed` and `inProgress → completed` also occur as `RecordFinding`
side effects (§10.20); they are not reachable through `TransitionAction` (there is no `CompleteAction`).

### 7.6 Cause (`Decide.fs:175-187`, `:499-503`)

| from | `ConfirmCause` (gated, §11) | `ReopenCause` |
|---|---|---|
| `proposed` (`CauseProposed`) | `confirmed` | — |
| `reopened` | `confirmed` | — |
| `confirmed` | — (`CAUSE.STATE`) | `reopened` |
| `superseded` | — (`CAUSE.STATE`) | — |

`ConfirmCause` does not use `transition`; an illegal source state yields `CAUSE.STATE` (§11.1). `ReopenCause` from a
non-`confirmed` state yields `TRANSITION.ILLEGAL` with rule tag `STATE-TRANSITION-TABLES §10`.

### 7.7 Corrective action (`Decide.fs:56-64`)

| from \ command | `ApproveCorrection` | `CancelCorrection` | `StartCorrection` | `MarkImplemented` | `StartVerificationOfEffect` | `RequireRework` |
|---|---|---|---|---|---|---|
| `proposed` (`CorrectionProposed`) | `approved` | `cancelled` | — | — | — | — |
| `approved` | — | `cancelled` | `inProgress` | — | — | — |
| `inProgress` | — | — | — | `implemented` | — | — |
| `implemented` | — | — | — | — | `awaitingVerificationOfEffect` | — |
| `verificationFailed` | — | — | — | — | — | `reworkRequired` |
| `reworkRequired` | — | — | `inProgress` | — | — | — |
| `awaitingVerificationOfEffect`, `verifiedEffective`, `cancelled` | — | — | — | — | — | — |

`awaitingVerificationOfEffect → verificationFailed` and `awaitingVerificationOfEffect → verifiedEffective` occur only
as `RecordVerification` follow-ons (§12.3).

### 7.8 Proposal (`Decide.fs:557-577`)

| from | `AcceptProposal` | `RejectProposal` |
|---|---|---|
| `active` | `accepted` | `rejected` |
| `accepted`, `rejected` | — (`PROPOSAL.NOT_ACTIVE`) | — (`PROPOSAL.NOT_ACTIVE`) |

### 7.9 Investigation lifecycle (`Decide.fs:66-74`)

| from \ command | `ActivateInvestigation` | `MarkAwaitingEvidence` | `MarkAwaitingVerification` | `ResolveInvestigation` | `CloseInvestigation` | `ReopenInvestigation` |
|---|---|---|---|---|---|---|
| `draft` | `active` | — | — | — | — | — |
| `active` | — | `awaitingEvidence` | `awaitingVerification` | `resolved` | — | — |
| `awaitingEvidence` | `active` | — | — | — | — | — |
| `awaitingVerification` | `active` | — | — | `resolved` | — | — |
| `resolved` | — | — | — | — | `closed` | `active` |
| `closed` | — | — | — | — | — | `active` |
| `archived` | `active` | — | — | — | — | — |

Plus the follow-on `awaitingVerification → active` (`verificationFailed`) of §12.3. No v1 command reaches `archived`.

### 7.10 Evidence

Evidence has no state machine. `supersededBy` may be set once (§10.5); `verification` may be set to any value any
number of times (§10.6).

## 8. The decision procedure

`decide(S, env)` with `env = {investigationId, actor, at, command}` (engine `Decide.decide`, `Decide.fs:611-637`).
Steps are evaluated in this order; the first step that refuses determines the result.

1. **Agent authority (`AGENT.PROPOSAL_ONLY`, rule `D-004`).** If `env.actor.kind = agent` and the command is not
   `RecordProposal`, refuse with `[AGENT.PROPOSAL_ONLY]`. This step precedes every other step, including step 2.
   (`Decide.fs:614-615`)
2. **No state.** If `S = ⊥`:
   - command `StartInvestigation`: if its `investigationId ≠ env.investigationId`, refuse `[ENVELOPE.MISMATCH]`
     (rule `schema`). Otherwise let `t` be `original` if `title` is blank, else `title`; refuse with
     `collect(text(t), text(original))`; otherwise emit C1. (`Decide.fs:618-623`)
   - any other command: refuse `[INVESTIGATION.NOT_FOUND]` (rule `schema`). (`Decide.fs:624`)
3. **Envelope.** If `env.investigationId ≠ S.investigationId`, refuse `[ENVELOPE.MISMATCH]` (rule `schema`).
   (`Decide.fs:625-626`)
4. **Expected version.** A command MAY carry an expected version `v`; if `v ≠ S.version`, refuse `[VERSION.STALE]`
   (rule `STATE-TRANSITION-TABLES §16.3`). History validation never supplies one, so this step never fires there.
   (`Decide.fs:628-630`)
5. **Lifecycle commands** (C2–C7) are decided by §9 **without** the open check of step 6. (`Decide.fs:633-634`)
6. **Open investigation (`INVESTIGATION.NOT_OPEN`, rule `D-005`).** If `not isOpen(S.lifecycle)` (i.e. `resolved`,
   `closed` or `archived`), refuse every remaining command, including `StartInvestigation`, with
   `[INVESTIGATION.NOT_OPEN]`. (`Decide.fs:635-636`)
7. **Content commands** are decided by §10. (`Decide.fs:637`)

## 9. Lifecycle command rules

`Decide.fs:582-607`. For C2–C7:

1. `target ← transition(§7.9, S.lifecycle, command)`; if illegal, refuse `[TRANSITION.ILLEGAL]` (rule
   `STATE-TRANSITION-TABLES`). No further step runs.
2. Then, by command:
   - `MarkAwaitingEvidence`: refuse `text(reason)` if blank; else emit C3 with `reason`. (`:595-596`)
   - `ReopenInvestigation`: refuse `text(reason)` if blank; else emit C7 with `reason`. (`:597-598`)
   - `MarkAwaitingVerification`: if some corrective action has `state ∈ {implemented, awaitingVerificationOfEffect}`,
     emit C4; otherwise refuse `[LIFECYCLE.NO_VERIFICATION_WORK]` (rule `STATE-TRANSITION-TABLES §1`). (`:599-602`)
   - `ResolveInvestigation`: compute the resolution blockers (§14). If the list is empty, emit C5; otherwise refuse
     with the whole list. (`:603-606`)
   - `ActivateInvestigation`, `CloseInvestigation`: emit C2 / C6. (`:607`)
3. The emitted `from` is `S.lifecycle`, `to` is `target`. `reason` is `null` for C2, C4, C5, C6.

## 10. Content command rules

Each rule below runs at step 7 of §8, i.e. only when `isOpen(S.lifecycle)`. "Refuse `collect(…)`" means: evaluate the
collect (§1.5) and refuse if it is non-empty. Unless stated otherwise the command then emits its row of §6.

1. **`RecordObservation`** (`Decide.fs:409-411`): `collect(absent(observations, observationId), text(original))`.
2. **`RecordHypothesis`** (`:412-414`): `collect(absent(hypotheses, hypothesisId), text(statement))`.
3. **`TransitionHypothesis`** (`:375-398`, `:415`).
   1. `found(hypotheses, hypothesisId)`; refuse if missing. Let `h` be the hypothesis.
   2. Then refuse `collect(c₁, c₂, c₃, c₄)` where
      - `c₁ = transition(§7.1, h.state, command)`;
      - `c₂ = text(reason)`;
      - `c₃` = if `command` is evidence-requiring (§7.1) and `evidenceIds = []`: `[HYPOTHESIS.EVIDENCE_REQUIRED]`
        (rule `R2.3`); else `allExist(evidence, evidenceIds)`;
      - `c₄` (only for `reopen`, **reopen requires new evidence**): let `fa` be the `aggregateVersion` of the
        **last** entry of `h.history` whose `to = falsified`, or `0` if there is none. `c₄ = []` iff some
        `e ∈ evidenceIds` exists in `S.evidence` with `admittedAtVersion > fa`; otherwise
        `[HYPOTHESIS.REOPEN_NEEDS_NEW_EVIDENCE]` (rule `R2.5`). Superseded evidence counts; nonexistent ids are
        ignored by `c₄`. For other commands `c₄ = []`.
   3. Emit C10 with `from = h.state`, `to` = the `c₁` target.
4. **`AdmitEvidence`** (`:416-426`). Refuse `collect(absent(evidence, evidenceId), c, text(source))` where `c` is
   `text(content)` for text content, and for artifact content `c = []` iff `wellFormedArtifactUrl(content)` (§10.4.1),
   else `[EVIDENCE.URL]` (rule `R3.5`).
   1. **Artifact URL well-formedness.** `wellFormedArtifactUrl(u)` is true iff **both**:
      (a) `u` begins with `http://` or `https://`, letters compared case-insensitively (engine
      `StartsWith(…, OrdinalIgnoreCase)`; given the CONTRACT schema pattern this is ASCII case-insensitive); and
      (b) `u` is accepted by **.NET 10 `System.Uri.IsWellFormedUriString(u, UriKind.Absolute)`** (engine `:421`).
      Note: the engine uses `IsWellFormedUriString`, which is stricter than `Uri.TryCreate(…, Absolute)`: it first
      requires `TryCreate` to succeed and then requires `IsWellFormedOriginalString()`.
      Implementation-independent statement (normative where it is certain, see §17 E-1 for the rest). For an
      `http`/`https` string, (b) is false if any of the following holds; implementations MUST reject these:
      - the string does not parse as an absolute hierarchical URI `scheme "://" authority path-abempty ["?" query]
        ["#" fragment]`, including: an empty host; a host that is not a valid DNS-style name, dotted IPv4 address,
        bracketed IPv6 literal or IDN-convertible Unicode name (e.g. a host containing a space, as in
        `https://exa mple.invalid/x`); a port containing a character other than a decimal digit, or whose value exceeds 65535 (an empty port and
        leading zeros are accepted);
      - a backslash `\` anywhere after the scheme (`http:\\host/…`, `http://host/a\b`);
      - in userinfo, path, query or fragment: a C0 or C1 control character (U+0000–U+001F, U+007F–U+009F), a space,
        or any of `"` `<` `>` `\` `^` `` ` `` `{` `|` `}` unescaped (they would need percent-encoding), or a `%`
        that is not followed by two hexadecimal digits.
      And (b) is true for a string of the form `http(s)://` + DNS host (letters, digits, `-`, `.`) + optional
      `:port` + a path, query and fragment consisting only of RFC 3986 unreserved characters, sub-delims,
      `:` `@` `/` `?` and well-formed `%HH` escapes. Dot segments (`/./`, `/../`) and `//` in the path do **not** make
      it ill-formed (the .NET check deliberately skips them). Behaviour for non-ASCII characters, `[`/`]` outside an
      IPv6 host, leading/trailing whitespace, and other edge cases is **platform-defined** (§17 E-1): there an
      implementation MUST either call the .NET 10 function or treat the case as unresolved.
5. **`SupersedeEvidence`** (`:427-432`). Refuse `collect(found(evidence, evidenceId), found(evidence,
   replacementEvidenceId), text(reason))`. Then, in order:
   - if `S.evidence[evidenceId].supersededBy ≠ null`: refuse `[EVIDENCE.ALREADY_SUPERSEDED]` (rule
     `STATE-TRANSITION-TABLES §4`);
   - else if `evidenceId = replacementEvidenceId`: refuse `[EVIDENCE.SELF]` (rule `schema`).
   The replacement may itself be superseded and may have been admitted before or after the old evidence.
6. **`VerifyEvidence`** (`:433-434`): `found(evidence, evidenceId)`. Any `verification` value is accepted, including
   the current one.
7. **`RelateEvidence`** (`:435-437`): `collect(absent(relationships, relationshipId), found(evidence, evidenceId),
   found(hypotheses, hypothesisId))`. Superseded evidence and hypotheses in any state are accepted.
8. **`IdentifyUnknown`** (`:438-440`): `collect(absent(unknowns, unknownId), text(question))`.
9. **`TransitionUnknown`** (`:441-444`): `found(unknowns, unknownId)`; then `collect(transition(§7.2, u.state,
   command), text(basis))`.
10. **`RecordAssumption`** (`:445-447`): `collect(absent(assumptions, assumptionId), text(proposition))`.
11. **`TransitionAssumption`** (`:448-451`): `found(assumptions, assumptionId)`; then `transition(§7.4, a.state,
    command)`.
12. **`RecordContradiction`** (`:452-454`): `collect(absent(contradictions, contradictionId), text(statement),
    allExist(evidence, evidenceIds))`. `evidenceIds = []` is accepted.
13. **`TransitionContradiction`** (`:455-458`): `found(contradictions, contradictionId)`; then
    `collect(transition(§7.3, c.state, command), text(basis))`.
14. **`RecordAction`** (`:467-469`): `collect(absent(actions, actionId), text(purpose), allExist(hypotheses,
    targetHypothesisIds))`.
15. **`TransitionAction`** (`:470-473`): `found(actions, actionId)`; then `transition(§7.5, a.state, command)`.
16. **`EstablishNegativeKnowledge`** (`:485-489`): `collect(absent(negativeKnowledge, negativeKnowledgeId),
    text(statement), text(scope), allExist(evidence, basisEvidenceIds))`; then if `basisEvidenceIds = []`, refuse
    `[NEGATIVE.BASIS_REQUIRED]` (rule `R5.3`).
17. **`RecordPrediction` — prediction timing** (`:459-466`). Refuse `collect(absent(predictions, predictionId),
    found(hypotheses, hypothesisId), text(statement))`. Then the **decided timing** is `retrospective` if there
    exists a finding `f` in `S.findings` such that `f.actionId ∈ S.actions` and that action's
    `targetHypothesisIds` is empty **or** contains `hypothesisId`; otherwise it is the requested `timing`
    (rule `A11`). This rule never refuses; it rewrites. A requested `retrospective` is always kept. The finding may
    have been recorded at any earlier version, for any action state.
18. **`RecordCorrectiveAction`** (`:504-506`): `collect(absent(correctiveActions, correctiveActionId),
    text(description), allExist(causes, targetCauseIds))`. Causes in any state are accepted; `targetCauseIds = []`
    is accepted.
19. **`TransitionCorrection`** (`:507-515`): `found(correctiveActions, correctiveActionId)`; then
    `target ← transition(§7.7, ca.state, command)`; then if `target = awaitingVerificationOfEffect` and
    `criteriaOf(correctiveActionId) = []`, refuse `[CORRECTION.NO_CRITERIA]` (rule `R7.3`).
20. **`RecordFinding`** (`:474-484`). Refuse `collect(absent(findings, findingId), found(actions, actionId),
    text(originalResult))`. Then by the action's state: `inProgress`, `approved`, `completed` emit C24's group for
    that state; any other state (`proposed`, `rejected`, `cancelled`, `interpreted`) refuses
    `[ACTION.NOT_EXECUTABLE]` (rule `STATE-TRANSITION-TABLES §5`).
21. **`RecordCausalAssertion`** (`:490-494`): `collect(absent(causes, causeId), text(outcome), allExist(hypotheses,
    [m.hypothesisId for m in members]))`; then if `members = []`, refuse `[CAUSE.NO_MEMBERS]` (rule `R6.3`). The same
    hypothesis MAY appear in several members.
22. **`ConfirmCause`** (`:495-498`): refuse with `causeBlockers(causeId)` (§11) if it is non-empty; otherwise emit
    C27. The emitted `policy` is exactly `core-default/1`.
23. **`ReopenCause`** (`:499-503`): `collect(found(causes, causeId), text(reason))`; then if the cause's `state ≠
    confirmed`, refuse `[TRANSITION.ILLEGAL]` (rule `STATE-TRANSITION-TABLES §10`).
24. **`RecordCriterion`** (`:516-518`): `collect(absent(criteria, criterionId), found(correctiveActions,
    correctiveActionId), text(expectedOutcome))`. No state restriction on the corrective action.
25. **`RecordVerification`**: §12.
26. **`RecordConclusion`**: §13.
27. **`RecordProposal` — repetition refusal** (`:400-404`, `:550-556`). Refuse `collect(absent(proposals,
    proposalId), text(text))`. Then, **only if** `env.actor.kind = agent` **and** `kind = hypothesis`: let
    `s = norm(text)` (§3.3). If some hypothesis with `state = falsified` has `norm(statement) = s`, or some
    negativeKnowledge (any) has `norm(statement) = s`, refuse `[PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE]` (rule `R5.4`).
    Human and system actors, and proposal kinds `unknown`/`action`, are never refused by this rule.
28. **`RejectProposal`** (`:573-577`): `collect(found(proposals, proposalId), text(reason))`; then if the proposal's
    `state ≠ active`, refuse `[PROPOSAL.NOT_ACTIVE]` (rule `STATE-TRANSITION-TABLES §12`).
29. **`AcceptProposal`** (`:557-572`). In order:
    1. `found(proposals, proposalId)`; let `p` be the proposal.
    2. if `p.state ≠ active`: refuse `[PROPOSAL.NOT_ACTIVE]` (rule `STATE-TRANSITION-TABLES §12`).
    3. if `env.actor.kind = agent`: refuse `[PROPOSAL.AGENT_CANNOT_ACCEPT]` (rule `D-004`). (Unreachable: §8 step 1
       refuses an agent first.)
    4. Build the **target command** from the proposal's stored kind and text (not from any input): `hypothesis` →
       `RecordHypothesis(targetId, p.text, mechanism null)`; `unknown` → `IdentifyUnknown(targetId, p.text,
       materiality material)`; `action` → `RecordAction(targetId, kind test, p.text, targetHypothesisIds [])`.
    5. Decide the target command with the content rules of this section (§10.2, §10.8 or §10.14) against the same
       `S` and envelope. If it refuses, `AcceptProposal` refuses with the same list (e.g. `DUPLICATE_ID` when
       `targetId` already names an entity **of that kind**). Otherwise emit the target's events followed by
       `ProposalWasAccepted{proposalId, targetId}`.
30. **`StartInvestigation`** on an existing, open state (`:578`): refuse `[INVESTIGATION.EXISTS]` (rule `schema`).

## 11. Cause confirmation gate

`causeBlockers(cid)` (engine `Policy.causeBlockers`, `Decide.fs:175-187`) returns **every** blocker, in this order.
`ConfirmCause` succeeds iff the list is empty.

1. If `cid ∉ S.causes`: return `[CAUSE.NOT_FOUND]` (rule `I-19`) and nothing else.
2. Let `ca = S.causes[cid]`. **State:** if `ca.state ∉ {proposed, reopened}`: `CAUSE.STATE` (rule
   `STATE-TRANSITION-TABLES §10`).
3. **Lifecycle:** if `S.lifecycle ∉ {active, awaitingEvidence, awaitingVerification}`: `INVESTIGATION.NOT_OPEN`
   (rule `D-005`). (In practice only `draft` reaches this, because §8 step 6 refuses closed lifecycles first.)
4. Then the **support findings** `causeSupport(ca)` (engine `Policy.causeSupportFindings`, `Decide.fs:122-171`),
   concatenated in this order:
   1. **Root (`CAUSE.NO_ROOT`, rule `R6.3`):** one error if no member has `role = rootCause`.
   2. **Members**, for each member `m` in `members` order (duplicates included):
      - if `m.hypothesisId ∉ S.hypotheses`: one `CAUSE.MEMBER_MISSING` (rule `I-19`), and nothing else for `m`;
      - otherwise, with `h = S.hypotheses[m.hypothesisId]`, in order:
        - `CAUSE.MEMBER_NOT_SUPPORTED` (rule `§15.3`) if `h.state ≠ supported`;
        - `CAUSE.NO_SUPPORTING_EVIDENCE` (rule `§15.3`) unless some relationship has `hypothesisId = h.id`,
          `kind = supports` and `active(evidenceId)`;
        - one `CAUSE.CONTRADICTING_EVIDENCE` (rule `§15.4`) **per** relationship `r` (in id order) with
          `r.hypothesisId = h.id`, `r.kind = contradicts`, `active(r.evidenceId)`, and `r.evidenceId` **not
          addressed**. Evidence `e` is *addressed* iff some contradiction `c` has `e ∈ c.evidenceIds` and
          `c.state ∈ {resolved, acceptedResidual}`.
   3. **Open contradictions:** one `CAUSE.OPEN_CONTRADICTION` (rule `§15.4`) per contradiction (id order) with
      `openContradiction(c)` and `material(c.materiality)`.
   4. **Open unknowns:** one `CAUSE.OPEN_UNKNOWN` (rule `§15.5`) per unknown (id order) with `openUnknown(u)` and
      `material(u.materiality)`.
   5. **Unverified assumptions:** one `CAUSE.UNVERIFIED_ASSUMPTION` (rule `§15.6`) per assumption (id order) with
      `state = explicitUnverified` and `material(materiality)`. (`provisionallyAccepted` does not block.)
   6. **Alternatives:** one `CAUSE.ALTERNATIVE_NOT_ADDRESSED` (rule `§15.7`) per hypothesis (id order) that is not
      a member hypothesis of `ca` and has `state ∈ {proposed, plausible, supported}`.
   7. **Mechanism:** `CAUSE.NO_MECHANISM` (rule `§15.8`) if `ca.mechanism = null`.
5. Evidence `verification` status, prediction records, findings, negative knowledge and observations do not affect
   the gate.

## 12. Verification rules and freshness

### 12.1 Rounds

A corrective action's **verification round** begins at the version of the event that moved it to
`awaitingVerificationOfEffect` (§5 row 25); `round(ca)` is that version, or `0` if it never did. A new
`StartVerificationOfEffect` (after rework) starts a new round. Evidence is **fresh** for round `r` iff it is still
active and was admitted at a version greater than `r` (§4.4).

### 12.2 `RecordVerification` decision (`Decide.fs:519-539`)

1. Refuse `collect(absent(verifications, verificationId), found(criteria, criterionId), allExist(evidence,
   evidenceIds))`.
2. Let `crit = S.criteria[criterionId]` and `ca = S.correctiveActions[crit.correctiveActionId]`. If that corrective
   action does not exist, the oracle raises an exception instead of deciding (§17 E-2); this can only happen after an
   earlier inadmissible group.
3. If `ca.state ≠ awaitingVerificationOfEffect`: refuse `[VERIFICATION.NOT_AWAITING]` (rule `R7.4`).
4. Else if `outcome = satisfied` and `evidenceIds = []`: refuse `[VERIFICATION.EVIDENCE_REQUIRED]` (rule `R7.4`).
5. Else if `outcome = satisfied` and some `e ∈ evidenceIds` is not `fresh(ca.awaitingSinceVersion, e)`: refuse
   `[VERIFICATION.EVIDENCE_PREDATES_ROUND]` (rule `R7.4`). This covers evidence admitted at or before the round start
   and evidence superseded since.
6. Otherwise emit `VerificationRecorded` and the follow-ons of §12.3. `failed` and `inconclusive` verifications are
   not subject to steps 4–5 (they may cite no evidence or stale evidence).

### 12.3 Follow-on events (`Decide.fs:530-539`)

Let `S'` be `apply(S, VerificationRecorded…)` at version `S.version + 1` (§5 row 27).

- `outcome = failed`: emit `CorrectiveActionTransitioned{correctiveActionId: ca.id, state: verificationFailed}`;
  then, **only if** `S.lifecycle = awaitingVerification` (the state before the command), also emit
  `LifecycleChanged{from: awaitingVerification, to: active, command: verificationFailed, reason: "A verification
  failed; work reopens (A21)."}`. The reason text is exact (ASCII, 42 characters) and is compared exactly in §15.6.
- `outcome = satisfied` and `allSatisfied(ca.id)` evaluated on `S'`: emit
  `CorrectiveActionTransitioned{correctiveActionId: ca.id, state: verifiedEffective}`.
- otherwise (`inconclusive`, or `satisfied` with some other criterion of `ca` not yet satisfied in this round): no
  follow-on.

## 13. Conclusion rules (`Decide.fs:540-549`)

For `RecordConclusion`:

1. Refuse `collect(absent(conclusions, conclusionId), text(statement), allExist(causes, causeIds))`.
2. If `strength = confirmedCause` and no `c ∈ causeIds` has `S.causes[c].state = confirmed`: refuse
   `[CONCLUSION.UNSUPPORTED]` (rule `R6.1`). (One confirmed cause among several suffices.)
3. Else if `residualUncertainty` is blank (null, or blank text) **and** (`unresolvedMaterialUnknowns ≠ []` **or**
   some unknown has `state = acceptedResidual`): refuse `[CONCLUSION.RESIDUAL_REQUIRED]` (rule `R6.4`).
   Contradictions accepted as residual do **not** trigger this rule (they do trigger §14 `RESOLVE.RESIDUAL_NOT_STATED`).
4. Otherwise emit C33.

## 14. Resolution gate

`resolutionBlockers` (engine `Policy.resolutionBlockers`, `Decide.fs:219-265`) is evaluated for
`ResolveInvestigation` after its lifecycle transition is found legal (§9). Let `confirmed` be the causes (id order)
with `state = confirmed` and `C` their id set. Let `corrections` be the corrective actions (id order) with
`kind = correction` and at least one `targetCauseIds` element in `C`. The result is the concatenation, in this order:

1. `RESOLVE.NO_CONFIRMED_CAUSE` (rule `R6.1`) if `confirmed = []`.
2. For each `c ∈ confirmed`, for each error `f` of `causeSupport(c)` (§11.4, same order):
   `RESOLVE.CAUSE_NO_LONGER_HOLDS` with rule tag equal to `f`'s rule tag (one error per finding).
3. If `corrections = []`: `RESOLVE.NO_CORRECTION` (rule `R7.2`). Else if no element of `corrections` has
   `state = verifiedEffective`: `RESOLVE.CORRECTION_NOT_VERIFIED` (rule `R7.4`). (Mitigations, workarounds and
   preventions never count.)
4. For each `ca ∈ corrections` with `ca.state ≠ cancelled`, for each criterion of `criteriaOf(ca.id)` that is not
   `satisfied` (§4.4, evaluated now, so later supersession of cited evidence un-satisfies it):
   `RESOLVE.CRITERION_UNSATISFIED` (rule `R7.3`).
5. For each unknown in `blockingUnknowns`: `RESOLVE.BLOCKING_UNKNOWN` (rule `R5.2`).
6. `RESOLVE.NO_CONCLUSION` (rule `R6.4`) if there is no conclusion.
7. If `latestConclusion` exists (call it `k`), in order:
   - `RESOLVE.CONCLUSION_STALE` (rule `R6.1`) if `k.strength = confirmedCause` and (`k.causeIds = []` or some element
     of `k.causeIds` is not in `C`);
   - `RESOLVE.RESIDUAL_NOT_STATED` (rule `R6.4`) if `k.residualUncertainty = null` and some unknown has
     `state = acceptedResidual` or some contradiction has `state = acceptedResidual`.

Notes (informative): rule 1 never fires alone (with no confirmed cause, `corrections = []`, so rule 3 also fires);
rule 5 never fires without rule 2 when a cause is confirmed (a blocking open unknown is a material open unknown, so
`causeSupport` reports `CAUSE.OPEN_UNKNOWN`) and never without rule 1 otherwise. CONTRACT §14 records the same.

## 15. History admissibility (`History.validate`)

Engine `History.validate`, `Decide.fs:776-805`. The procedure folds over **command groups** in history order,
threading a state, and accumulates errors. It never stops early.

### 15.1 Grouping (`Decide.fs:760-767`)

A command group is a maximal run of **consecutive** events with equal `correlationId`. Groups are processed in history
order. (Non-adjacent reuse of a `correlationId` forms separate groups here; CONTRACT §5.4 rejects it earlier at
stage 14.)

### 15.2 Per-group procedure

For each group `G = [e₁, …, eₖ]` with prior state `S` (initially `⊥`):

1. **Sequence.** Let `x = S.version + 1` (or `1` if `S = ⊥`). For each `i` in `1..k` in order, if
   `eᵢ.aggregateVersion ≠ x + i − 1`, add one error `HISTORY.SEQUENCE` (rule `D-005`). (`:779-783`)
2. **Verdict** (exactly one branch applies, first match wins; `:789-802`):
   1. If `S = ⊥` and `e₁` is not `InvestigationStarted`: `[HISTORY.NO_START]` (rule `D-005`).
   2. Else if `commandOf(G)` (§15.3) is *none*: `[HISTORY.INADMISSIBLE_EVENT]` (rule `DEC-04`).
   3. Else if the group is **inconsistent** — some `eᵢ` differs from `e₁` in `actor` (§2.3), `occurredAt` (instant)
      or `investigationId`: `[HISTORY.INADMISSIBLE_EVENT]` (rule `DEC-04`).
   4. Else let `cmd = commandOf(G)` and `r = decide(S, {investigationId: e₁.investigationId, actor: e₁.actor,
      at: e₁.occurredAt, command: cmd})` with no expected version (§8).
      - If `r` refuses with list `[f₁, …, fₙ]`: `n` errors, the `j`-th having code `codeFor(G)` (§15.4) and rule tag
        `fⱼ.rule`; the original code `fⱼ.code` is carried in the message (engine format
        `Event {e₁.aggregateVersion} would be refused now ({fⱼ.code}): {fⱼ.message}`).
      - If `r` emits bodies `B` and `B ≠ bodies(G)` (§15.6): `[HISTORY.INADMISSIBLE_EVENT]` (rule `DEC-04`).
      - Otherwise: no error.
3. The group's errors are the sequence errors followed by the verdict errors. Append them to the history's list.
4. **Next state** (`:803`): apply every event of `G` in order with §5 — **regardless of the verdict** — where an event
   seen while the state is `⊥` is applied only if it is `InvestigationStarted` (otherwise the state stays `⊥`).

The history is **admissible iff the final error list is empty.** An empty history yields an empty list (§17 E-9).

### 15.3 Command reconstruction `commandOf(G)` (`Decide.fs:674-757`)

1. **Precedence.** If any event of `G` is `FindingRecorded`, the command is `RecordFinding` built from the **first**
   such event. Else, if any event is `VerificationRecorded`, `RecordVerification` from the first such event. Else, if
   any event is `ProposalWasAccepted`, `AcceptProposal` from the first such event. (`:676-680`)
2. Otherwise the command is determined by `e₁` alone, by this table (all 31 event types). "none" means no single
   command emits a group starting with this event.

| `e₁.eventType` | reconstructed command |
|---|---|
| `InvestigationStarted` | `StartInvestigation(e₁.investigationId, title, observationId, original)` |
| `LifecycleChanged` | by `command`: `activateInvestigation` → `ActivateInvestigation`; `markAwaitingEvidence` → `MarkAwaitingEvidence(reason)` if `reason ≠ null`, else none; `markAwaitingVerification` → `MarkAwaitingVerification`; `resolveInvestigation` → `ResolveInvestigation`; `closeInvestigation` → `CloseInvestigation`; `reopenInvestigation` → `ReopenInvestigation(reason)` if `reason ≠ null`, else none; `verificationFailed` → none. `from`, `to` (and `reason` for the reason-less commands) are not used. |
| `ObservationRecorded` | `RecordObservation(observationId, original, observedAt)` |
| `HypothesisRecorded` | `RecordHypothesis(hypothesisId, statement, mechanism)` |
| `HypothesisTransitioned` | `TransitionHypothesis(hypothesisId, command, reason, evidenceIds)`; `from`, `to` are not used |
| `EvidenceAdmitted` | `AdmitEvidence(evidenceId, text content or artifact URL per contentKind, category, source, capturedAt)` |
| `EvidenceSuperseded` | `SupersedeEvidence(evidenceId, replacementEvidenceId, reason)` |
| `EvidenceVerificationAssessed` | `VerifyEvidence(evidenceId, verification)` |
| `EvidenceRelated` | `RelateEvidence(relationshipId, evidenceId, hypothesisId, kind, rationale)` |
| `UnknownIdentified` | `IdentifyUnknown(unknownId, question, materiality)` |
| `UnknownTransitioned` | by `state`: `investigating` → `StartInvestigatingUnknown`; `resolved` → `ResolveUnknown`; `acceptedResidual` → `AcceptUnknownAsResidual`; other → none. Command `TransitionUnknown(unknownId, ·, basis)` |
| `AssumptionRecorded` | `RecordAssumption(assumptionId, proposition, materiality)` |
| `AssumptionTransitioned` | by `state`: `provisionallyAccepted` → `ProvisionallyAcceptAssumption`; `verified` → `VerifyAssumption`; `rejected` → `RejectAssumption`; other → none |
| `ContradictionRecorded` | `RecordContradiction(contradictionId, statement, evidenceIds, materiality)` |
| `ContradictionTransitioned` | by `state`: `triaged` → `TriageContradiction`; `resolved` → `ResolveContradiction`; `acceptedResidual` → `AcceptContradictionAsResidual`; other → none. Command carries `basis` |
| `PredictionRecorded` | `RecordPrediction(predictionId, hypothesisId, statement, strength, timing)` — the recorded timing is the *requested* timing |
| `ActionRecorded` | `RecordAction(actionId, kind, purpose, targetHypothesisIds)` |
| `ActionTransitioned` | by `state`: `approved` → `ApproveAction`; `rejected` → `RejectAction`; `inProgress` → `StartAction`; `cancelled` → `CancelAction`; `completed` (or other) → none |
| `FindingRecorded` | (covered by precedence) `RecordFinding(findingId, actionId, originalResult, interpretation)` |
| `NegativeKnowledgeEstablished` | `EstablishNegativeKnowledge(negativeKnowledgeId, statement, scope, basisEvidenceIds)` |
| `CausalAssertionRecorded` | `RecordCausalAssertion(causeId, outcome, members, mechanism)` |
| `CausalAssertionConfirmed` | `ConfirmCause(causeId)`; `policy` is not used |
| `CausalAssertionReopened` | `ReopenCause(causeId, reason)` |
| `CorrectiveActionRecorded` | `RecordCorrectiveAction(correctiveActionId, kind, description, targetCauseIds)` |
| `CorrectiveActionTransitioned` | by `state`: `approved` → `ApproveCorrection`; `inProgress` → `StartCorrection`; `implemented` → `MarkImplemented`; `awaitingVerificationOfEffect` → `StartVerificationOfEffect`; `reworkRequired` → `RequireRework`; `cancelled` → `CancelCorrection`; `verificationFailed`, `verifiedEffective` → none |
| `CriterionRecorded` | `RecordCriterion(criterionId, correctiveActionId, expectedOutcome, method)` |
| `VerificationRecorded` | (covered by precedence) `RecordVerification(verificationId, criterionId, outcome, evidenceIds)` |
| `ConclusionRecorded` | `RecordConclusion(conclusionId, statement, strength, causeIds, residualUncertainty)` |
| `ProposalRecorded` | `RecordProposal(proposalId, kind, text, rationale)` |
| `ProposalWasAccepted` | (covered by precedence) `AcceptProposal(proposalId, targetId)` |
| `ProposalWasRejected` | `RejectProposal(proposalId, reason)` |

3. The reconstructed command ignores all events of `G` other than the one it was built from. Extra, missing or
   reordered events are detected only by the equality of §15.6.

### 15.4 Code of a refused group `codeFor(G)` (`Decide.fs:769-773`)

Determined by the **recorded** first event `e₁` only:

- `e₁` is `CausalAssertionConfirmed` → `HISTORY.UNGATED_CONFIRMATION`;
- `e₁` is `LifecycleChanged` with `to = resolved` (whatever its `command`) → `HISTORY.UNGATED_RESOLUTION`;
- otherwise → `HISTORY.INADMISSIBLE_EVENT`.

`codeFor` applies only to **refusals** (§15.2 step 2.4, first bullet). A confirmation or resolution group that the
decision accepts but whose events differ (e.g. `policy ≠ "core-default/1"`, a wrong `from`, an extra event), or that
is inconsistent or unreconstructable, gets `HISTORY.INADMISSIBLE_EVENT`.

### 15.5 Re-decision context

The command is re-decided against the state **before** the group, with the group's recorded actor (so agent
authority and repetition refusal apply to the recorded actor) and the group's `occurredAt` as decision time. Clock
values are never compared with each other or with any other time; `occurredAt` matters only through the equality of
§15.6 (`HypothesisTransitioned.at`).

### 15.6 Exact equality of decided and recorded events (`Decide.fs:800`)

`B = bodies(G)` iff `B` and `G` have the same length and, position by position, the same `eventType` and equal
payloads under §3.1, where:

- `HypothesisTransitioned` additionally compares the implicit `at` (decided: `env.at` = `e₁.occurredAt`; recorded: the
  event's `occurredAt`) and `aggregateVersion` (decided: `S.version + 1`; recorded: the event's `aggregateVersion`);
- `EvidenceAdmitted` compares the content kind as well as the string;
- `CausalAssertionRecorded.members` compares element-wise `{hypothesisId, role}` in order.

Because every field of the reconstructed command is copied from the recorded event, a difference can arise only in a
field the decision computes. These are, exhaustively:

| event | decided fields |
|---|---|
| `LifecycleChanged` | `from` (= `S.lifecycle`), `to` (transition target), `command` (fixed per command), `reason` (`null` for C2, C4, C5, C6; the exact follow-on text for `verificationFailed`) |
| `HypothesisTransitioned` | `from` (= `h.state`), `to` (target), implicit `at`, implicit `aggregateVersion` |
| `PredictionRecorded` | `timing` (forced `retrospective`, §10.17) |
| `CausalAssertionConfirmed` | `policy` (= `core-default/1`) |
| `InvestigationStarted` | `title` (= `original` if `title` is blank) |
| text options | `mechanism`, `rationale`, `interpretation`, `method`, `residualUncertainty` (`opt`: blank → `null`) |
| group shape | number, order and types of events, and every field of follow-on events: `RecordFinding` action transitions (§10.20), `RecordVerification` follow-ons (§12.3), `AcceptProposal` target event (`statement`/`question`/`purpose` = proposal `text`, `mechanism: null`, `materiality: material`, `kind: test`, `targetHypothesisIds: []`) and the trailing `ProposalWasAccepted` |

### 15.7 The error list and the "first error"

1. The history's error list is the concatenation, over groups in history order, of each group's sequence errors
   (event order) followed by its verdict errors (§15.2 step 3). Within a refused group, verdict errors follow the
   refusal order of §8–§14; they all carry the same code `codeFor(G)`.
2. The **first error** is the first element of that list. Its code is the domain code reported with
   `CAUSA.HISTORY_INADMISSIBLE` (CONTRACT §11.3). Equivalently: find the first group that produces any error; if any
   of its events has a wrong `aggregateVersion`, the code is `HISTORY.SEQUENCE`; otherwise it is that group's verdict
   code.
3. Consequently the inner refusal order (§1.5, §11, §14) never affects the first domain code; it affects only the
   inner rule codes and the multiplicity of errors.

### 15.8 When each `HISTORY.*` code is produced

| code | produced when |
|---|---|
| `HISTORY.NO_START` | a group is processed while the state is `⊥` and its first event is not `InvestigationStarted` (§15.2 2.1). Repeats for every later group while the state stays `⊥`. |
| `HISTORY.SEQUENCE` | an event's `aggregateVersion` differs from (version of the state before its group) + 1 + (its index in the group) (§15.2 step 1). One error per such event. |
| `HISTORY.INADMISSIBLE_EVENT` | no command reconstructs the group (§15.3 "none"); the group is inconsistent (§15.2 2.3); the decision refuses a group whose first event is neither `CausalAssertionConfirmed` nor `LifecycleChanged`→`resolved`; or the decision accepts but emits different events (§15.6). |
| `HISTORY.UNGATED_CONFIRMATION` | the decision refuses a group whose first recorded event is `CausalAssertionConfirmed` — for any refusal (gate blocker, `CAUSE.STATE`, `CAUSE.NOT_FOUND`, `INVESTIGATION.NOT_OPEN`, `AGENT.PROPOSAL_ONLY`, …). |
| `HISTORY.UNGATED_RESOLUTION` | the decision refuses a group whose first recorded event is `LifecycleChanged` with `to = resolved` — for any refusal (resolution blockers, `TRANSITION.ILLEGAL`, …). |

### 15.9 Reachability once CONTRACT stages 1–14 have passed (informative analysis)

1. `HISTORY.SEQUENCE` is never the first error: stage 12 makes versions `1..N`, and once a start has been applied
   the state version equals the `aggregateVersion` of the last applied event (§5.3, §15.2 step 4); sequence errors can then appear only after a
   `HISTORY.NO_START` (as in fixture `missing-start`).
2. `HISTORY.NO_START` can be first only at group 1.
3. Unreachable as an inner rule code (a schema invariant or an earlier step prevents it): `VALIDATION.EMPTY`
   (non-blank text), `AGENT.PROPOSAL_ONLY` (agents only on `ProposalRecorded`), `PROPOSAL.AGENT_CANNOT_ACCEPT` (always
   preceded by §8 step 1), `ENVELOPE.MISMATCH` (stage 10), `VERSION.STALE` (no expected version),
   `HYPOTHESIS.EVIDENCE_REQUIRED`, `NEGATIVE.BASIS_REQUIRED`, `CAUSE.NO_MEMBERS`, `VERIFICATION.EVIDENCE_REQUIRED`
   (payload invariants), `INTERNAL` (never emitted by the decision path).
4. Reachable only after an earlier inadmissible group: `CAUSE.MEMBER_MISSING` (hypotheses are never removed), the
   exception of §17 E-2.
5. Every other code of §16 can be the first inner code of the first failing group.

## 16. Rule-code index

All codes the decision path can produce, with rule tag, where defined, and engine location. "History code" is the
domain code a persisted group refused for that reason receives (§15.4): `I` = `HISTORY.INADMISSIBLE_EVENT`,
`C` = `HISTORY.UNGATED_CONFIRMATION`, `R` = `HISTORY.UNGATED_RESOLUTION`.

| # | code | rule tag | section | history code | engine |
|---|---|---|---|---|---|
| 1 | `AGENT.PROPOSAL_ONLY` | `D-004` | §8.1 | I/C/R by first event | `Decide.fs:614-615` |
| 2 | `ENVELOPE.MISMATCH` | `schema` | §8.2, §8.3 | I | `:619`, `:625-626` |
| 3 | `INVESTIGATION.NOT_FOUND` | `schema` | §8.2 | I | `:624` |
| 4 | `VERSION.STALE` | `STATE-TRANSITION-TABLES §16.3` | §8.4 | — (never in history) | `:629-630` |
| 5 | `INVESTIGATION.NOT_OPEN` | `D-005` | §8.6, §11.3 | I/C | `:635-636`, `:186` |
| 6 | `INVESTIGATION.EXISTS` | `schema` | §10.30 | I | `:578` |
| 7 | `INTERNAL` | `decide` | — (unreachable) | — | `:580` |
| 8 | `VALIDATION.EMPTY` | `schema` | §1.6 | I/R | `:344-346` |
| 9 | `DUPLICATE_ID` | `schema` | §1.6 | I | `:355-356` |
| 10 | `NOT_FOUND` | `schema` | §1.6 | I | `:350-353`, `:358-360` |
| 11 | `TRANSITION.ILLEGAL` | `STATE-TRANSITION-TABLES` (or `… §10` for `ReopenCause`) | §7, §9, §10.23 | I/R | `:362-365`, `:503` |
| 12 | `HYPOTHESIS.EVIDENCE_REQUIRED` | `R2.3` | §10.3 | I | `:381-382` |
| 13 | `HYPOTHESIS.REOPEN_NEEDS_NEW_EVIDENCE` | `R2.5` | §10.3 | I | `:384-392` |
| 14 | `EVIDENCE.URL` | `R3.5` | §10.4 | I | `:420-424` |
| 15 | `EVIDENCE.ALREADY_SUPERSEDED` | `STATE-TRANSITION-TABLES §4` | §10.5 | I | `:430` |
| 16 | `EVIDENCE.SELF` | `schema` | §10.5 | I | `:431` |
| 17 | `ACTION.NOT_EXECUTABLE` | `STATE-TRANSITION-TABLES §5` | §10.20 | I | `:484` |
| 18 | `NEGATIVE.BASIS_REQUIRED` | `R5.3` | §10.16 | I | `:488` |
| 19 | `CAUSE.NO_MEMBERS` | `R6.3` | §10.21 | I | `:493` |
| 20 | `CORRECTION.NO_CRITERIA` | `R7.3` | §10.19 | I | `:513-514` |
| 21 | `VERIFICATION.NOT_AWAITING` | `R7.4` | §12.2 | I | `:523-524` |
| 22 | `VERIFICATION.EVIDENCE_REQUIRED` | `R7.4` | §12.2 | I | `:525-526` |
| 23 | `VERIFICATION.EVIDENCE_PREDATES_ROUND` | `R7.4` | §12.2 | I | `:527-528` |
| 24 | `CONCLUSION.UNSUPPORTED` | `R6.1` | §13 | I | `:545-546` |
| 25 | `CONCLUSION.RESIDUAL_REQUIRED` | `R6.4` | §13 | I | `:547-548` |
| 26 | `PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE` | `R5.4` | §10.27 | I | `:554-555` |
| 27 | `PROPOSAL.NOT_ACTIVE` | `STATE-TRANSITION-TABLES §12` | §10.28, §10.29 | I | `:560`, `:576` |
| 28 | `PROPOSAL.AGENT_CANNOT_ACCEPT` | `D-004` | §10.29 (unreachable) | — | `:563` |
| 29 | `LIFECYCLE.NO_VERIFICATION_WORK` | `STATE-TRANSITION-TABLES §1` | §9 | I | `:600-602` |
| 30 | `CAUSE.NOT_FOUND` | `I-19` | §11.1 | C | `:177` |
| 31 | `CAUSE.STATE` | `STATE-TRANSITION-TABLES §10` | §11.2 | C | `:180-182` |
| 32 | `CAUSE.NO_ROOT` | `R6.3` | §11.4.1 | C | `:124-125` |
| 33 | `CAUSE.MEMBER_MISSING` | `I-19` | §11.4.2 | C | `:129` |
| 34 | `CAUSE.MEMBER_NOT_SUPPORTED` | `§15.3` | §11.4.2 | C | `:132-133` |
| 35 | `CAUSE.NO_SUPPORTING_EVIDENCE` | `§15.3` | §11.4.2 | C | `:134-139` |
| 36 | `CAUSE.CONTRADICTING_EVIDENCE` | `§15.4` | §11.4.2 | C | `:141-147` |
| 37 | `CAUSE.OPEN_CONTRADICTION` | `§15.4` | §11.4.3 | C | `:149-152` |
| 38 | `CAUSE.OPEN_UNKNOWN` | `§15.5` | §11.4.4 | C | `:153-156` |
| 39 | `CAUSE.UNVERIFIED_ASSUMPTION` | `§15.6` | §11.4.5 | C | `:157-160` |
| 40 | `CAUSE.ALTERNATIVE_NOT_ADDRESSED` | `§15.7` | §11.4.6 | C | `:161-166` |
| 41 | `CAUSE.NO_MECHANISM` | `§15.8` | §11.4.7 | C | `:167-170` |
| 42 | `RESOLVE.NO_CONFIRMED_CAUSE` | `R6.1` | §14.1 | R | `:222-223` |
| 43 | `RESOLVE.CAUSE_NO_LONGER_HOLDS` | rule tag of the underlying `CAUSE.*` finding | §14.2 | R | `:243-246` |
| 44 | `RESOLVE.NO_CORRECTION` | `R7.2` | §14.3 | R | `:227-229` |
| 45 | `RESOLVE.CORRECTION_NOT_VERIFIED` | `R7.4` | §14.3 | R | `:230-231` |
| 46 | `RESOLVE.CRITERION_UNSATISFIED` | `R7.3` | §14.4 | R | `:232-237` |
| 47 | `RESOLVE.BLOCKING_UNKNOWN` | `R5.2` | §14.5 | R | `:238-240` |
| 48 | `RESOLVE.NO_CONCLUSION` | `R6.4` | §14.6 | R | `:241-242` |
| 49 | `RESOLVE.CONCLUSION_STALE` | `R6.1` | §14.7 | R | `:256-259` |
| 50 | `RESOLVE.RESIDUAL_NOT_STATED` | `R6.4` | §14.7 | R | `:260-263` |
| 51 | `HISTORY.NO_START` | `D-005` | §15.2 2.1 | (is a history code) | `:791-792` |
| 52 | `HISTORY.SEQUENCE` | `D-005` | §15.2 step 1 | (is a history code) | `:780-783` |
| 53 | `HISTORY.INADMISSIBLE_EVENT` | `DEC-04` (or the refusal's tag) | §15.2, §15.4 | (is a history code) | `:773`, `:793`, `:795`, `:801` |
| 54 | `HISTORY.UNGATED_CONFIRMATION` | the refusal's tag | §15.4 | (is a history code) | `:771` |
| 55 | `HISTORY.UNGATED_RESOLUTION` | the refusal's tag | §15.4 | (is a history code) | `:772` |

Rewrite rule without a code: **prediction timing** `A11` (§10.17). Constant: policy id `core-default/1` (§3.5).

## 17. Engine-dependence register

Each entry states where this document's text depends on the oracle's platform, or where the oracle's behaviour is
surprising, and a verdict: **fully specified here**, **specified but relies on platform behaviour X**, or **NOT
specifiable**. Where the verdict is not "fully specified", the oracle wins (§0.3).

| id | topic | engine | verdict |
|---|---|---|---|
| E-1 | Artifact URL well-formedness (`EVIDENCE.URL`) uses .NET `Uri.IsWellFormedUriString(u, UriKind.Absolute)` after an `OrdinalIgnoreCase` prefix test. | `Decide.fs:421` | **Specified but relies on platform behaviour: .NET 10 (SDK 10.0.402) `System.Uri` parsing and `IsWellFormedOriginalString`, including IRI and IDN handling (IDN uses the host ICU library).** §10.4.1 states the cases that are certain (derived from the .NET 10 source of `UriExt.InternalIsWellFormedOriginalString` and `Uri.CheckCanonical`, and checked against the two fixture data points: `https://example.invalid/synthetic/resolver-query-log-2026-09-14.txt` accepted, `https://exa mple.invalid/x` refused). Non-ASCII characters, `[`/`]` outside IPv6, trailing whitespace, unusual hosts and ports are **NOT specifiable from text alone** with confidence; no execution of the .NET function was available to the author. |
| E-2 | `RecordVerification` reads the criterion's corrective action with an unchecked lookup. If the criterion names a corrective action that does not exist, the oracle throws `KeyNotFoundException` and `History.validate` returns no list (callers such as the causa-data `Store` do not catch it). | `Decide.fs:522` | **NOT specifiable** as "same first code": the oracle produces none. Reachable only when an earlier group was already inadmissible (a dangling `CriterionRecorded`), so the verdict (inadmissible) is unaffected. Implementations MUST report the history inadmissible and SHOULD report the first error accumulated before that group; this is a deliberate, documented divergence from an oracle crash. |
| E-3 | Blank-text test (`String.IsNullOrWhiteSpace`) and `Trim`. | `Primitives.fs:85`, `Decide.fs:401` | **Fully specified here** (§3.2, §3.3): fixed 25-code-unit whitespace set, per UTF-16 code unit (CONTRACT §2.6). It follows .NET `char.IsWhiteSpace`, which is stable across .NET versions. |
| E-4 | String equality is ordinal on UTF-16 code units; no normalisation. | F# `=` on `string` | **Fully specified here** (§3.1). |
| E-5 | Repetition refusal compares `Trim().ToLowerInvariant()` of statements. | `Decide.fs:401` | **Specified but relies on platform behaviour: .NET invariant lower-casing.** On Linux with ICU (the default), non-ASCII code points are mapped with the host ICU's `u_tolower` (simple mapping) except U+0130, so the Unicode version — and the result for recently added cased letters — depends on the installed `libicu`; in globalization-invariant mode .NET uses its own simple-case tables. ASCII-only and common Latin/Greek/Cyrillic text is fully specified by §3.3. |
| E-6 | Equality of `occurredAt` / `at` is by instant (`DateTimeOffset` equality ignores the offset). | `Decide.fs:785`, `:800` | **Fully specified here** (§3.1). On v1 wire data (UTC, millisecond, `Z`) instant equality and text equality coincide. |
| E-7 | Iteration order of entity collections (F# `Map` keyed by `Guid` with `Guid.CompareTo`). | `Primitives.fs:19-23` | **Fully specified here** (§3.4): .NET 10 `Guid.CompareTo` compares the first 4-byte, then the two 2-byte fields as unsigned values and then the remaining 8 bytes in order, which equals ordinal order of the canonical text. Affects only inner error order/multiplicity, never a verdict or first domain code. |
| E-8 | List equality (`decided <> bodies`) is F# structural equality: ordered, element-wise, and includes the hidden `HypothesisChange.At`/`.Sequence`. | `Decide.fs:800` | **Fully specified here** (§15.6). |
| E-9 | An empty history: `History.validate []` returns no errors (admissible). | `Decide.fs:805` | **Fully specified here.** Unreachable through the reader, which rejects an empty `events.ndjson` at stage 2 (`CAUSA.ENCODING`), and the reference `Store` fails replay of an empty list afterwards. |
| E-10 | Evidence `verification` status is stored but read by no rule (invalidated evidence still supports a cause and still satisfies a criterion). | `Decide.fs:299` only | **Fully specified here** (§4.2, §11.5); surprising, recorded so that implementers do not add a rule. |
| E-11 | Evolve keeps applying events of refused groups; duplicate creations replace entities; a second `InvestigationStarted` only bumps `version`. | `Decide.fs:276-332`, `:803` | **Fully specified here** (§5.4–§5.7, §15.2 step 4). Affects errors after the first only. |
| E-12 | `codeFor` maps by recorded first event, only for refusals; accepted-but-different confirmations/resolutions are `HISTORY.INADMISSIBLE_EVENT`. | `Decide.fs:769-773`, `:798-801` | **Fully specified here** (§15.4). |
| E-13 | `HISTORY.SEQUENCE` precedes the verdict within a group, and sequence numbering restarts at 1 while the state is `⊥`. | `Decide.fs:779-783`, `:804` | **Fully specified here** (§15.2, §15.7). |
| E-14 | Prediction timing uses *any* finding of an action whose targets are empty or include the hypothesis, regardless of when the finding was recorded relative to anything else, and of action state. | `Decide.fs:463-465` | **Fully specified here** (§10.17). |
| E-15 | Agent authority precedes the no-state check, and makes `PROPOSAL.AGENT_CANNOT_ACCEPT` unreachable. | `Decide.fs:612-615`, `:563` | **Fully specified here** (§8.1, §10.29). |
| E-16 | The command boundary may de-duplicate identifier arrays (CONTRACT §2.7); the engine itself accepts duplicates and reports one `NOT_FOUND` for any missing element. | `Decide.fs:358-360` | **Fully specified here** for histories (wire arrays are duplicate-free after stage 7). Command-side de-duplication is a CONTRACT §2.7 obligation, not an engine rule. |
| E-17 | Error messages (`label` truncation to 57 chars + `...` for texts over 60 UTF-16 units, interpolated F# union case names). | `Decide.fs:103-105` | **Specified but relies on platform behaviour: .NET string formatting.** Messages are informative only (§0.2); no conformance depends on them. |
| E-18 | The `golden/scenarios.json` `expectedCode` was generated as "some refusal has this code", not "the first refusal has this code" (causa-data `Scenarios.fs:403`). | generator | **Fully specified here**: for all five recorded refusals the expected code is also the first code of §11/§14 order (§18.2). |

## 18. Coverage

All rows were checked against the frozen files of `causa-data@causa-storage-v1.0.0`: the expected codes were read from
the fixture JSON (`expectedErrorCode`, `expectedDomainCode`) and `golden/scenarios.json`, and a throw-away clean-room
script written from this document alone (not the engine) reproduced, for every row, the expected first domain code,
the inner rule code named in the fixture description, and admissibility of the five valid scenarios. The script is not
part of this requirement set; the oracle remains authoritative (§0.3).

### 18.1 Negative fixtures with `expectedErrorCode = CAUSA.HISTORY_INADMISSIBLE` (34)

"First failing group" gives the `aggregateVersion` of its first event and its event types. "Inner code" is the code of
the first refusal (carried in the error message), or the branch of §15.2 when there is no refusal. All 34 are kind
`investigation`.

| # | fixture | first failing group | inner code / branch | rule section(s) | expected domain code |
|---|---|---|---|---|---|
| 1 | `engine-agent-repeats-falsified` | v38 `ProposalRecorded` (agent) | `PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE` | §10.27, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 2 | `engine-awaiting-verification-without-work` | v27 `LifecycleChanged` (`markAwaitingVerification`) | `LIFECYCLE.NO_VERIFICATION_WORK` | §9, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 3 | `engine-cause-already-confirmed` | v35 `CausalAssertionConfirmed` | `CAUSE.STATE` | §11.2, §15.4 | `HISTORY.UNGATED_CONFIRMATION` |
| 4 | `engine-cause-alternative-not-addressed` | v22 `CausalAssertionConfirmed` | `CAUSE.ALTERNATIVE_NOT_ADDRESSED` | §11.4.6, §15.4 | `HISTORY.UNGATED_CONFIRMATION` |
| 5 | `engine-cause-no-root` | v22 `CausalAssertionConfirmed` | `CAUSE.NO_ROOT` | §11.4.1, §15.4 | `HISTORY.UNGATED_CONFIRMATION` |
| 6 | `engine-cause-open-contradiction` | v22 `CausalAssertionConfirmed` | `CAUSE.OPEN_CONTRADICTION` | §11.4.3, §15.4 | `HISTORY.UNGATED_CONFIRMATION` |
| 7 | `engine-cause-open-unknown` | v22 `CausalAssertionConfirmed` | `CAUSE.OPEN_UNKNOWN` | §11.4.4, §15.4 | `HISTORY.UNGATED_CONFIRMATION` |
| 8 | `engine-cause-unverified-assumption` | v22 `CausalAssertionConfirmed` | `CAUSE.UNVERIFIED_ASSUMPTION` | §11.4.5, §15.4 | `HISTORY.UNGATED_CONFIRMATION` |
| 9 | `engine-cause-without-mechanism` | v36 `CausalAssertionConfirmed` | `CAUSE.NO_MECHANISM` | §11.4.7, §15.4 | `HISTORY.UNGATED_CONFIRMATION` |
| 10 | `engine-conclusion-without-residual` | v29 `ConclusionRecorded` | `CONCLUSION.RESIDUAL_REQUIRED` | §13.3, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 11 | `engine-confirmed-conclusion-unconfirmed-cause` | v29 `ConclusionRecorded` | `CONCLUSION.UNSUPPORTED` | §13.2, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 12 | `engine-content-after-resolution` | v60 `ObservationRecorded` (lifecycle `resolved`) | `INVESTIGATION.NOT_OPEN` | §8.6, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 13 | `engine-dangling-reference` | v27 `EvidenceRelated` | `NOT_FOUND` | §10.7, §1.6, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 14 | `engine-duplicate-entity-id` | v27 `HypothesisRecorded` | `DUPLICATE_ID` | §10.2, §1.6, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 15 | `engine-evidence-already-superseded` | v38 `EvidenceSuperseded` | `EVIDENCE.ALREADY_SUPERSEDED` | §10.5, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 16 | `engine-evidence-supersedes-itself` | v27 `EvidenceSuperseded` | `EVIDENCE.SELF` | §10.5, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 17 | `engine-finding-for-rejected-action` | v38 `FindingRecorded` | `ACTION.NOT_EXECUTABLE` | §10.20, §15.3, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 18 | `engine-group-time-disagreement` | v23 `ActionTransitioned`+`FindingRecorded`+`ActionTransitioned` | inconsistent group (no refusal) | §15.2 2.3 | `HISTORY.INADMISSIBLE_EVENT` |
| 19 | `engine-malformed-artifact-url` | v27 `EvidenceAdmitted` (`https://exa mple.invalid/x`) | `EVIDENCE.URL` | §10.4.1, §15.4; §17 E-1 | `HISTORY.INADMISSIBLE_EVENT` |
| 20 | `engine-prediction-timing-forged` | v38 `PredictionRecorded` (`timing: prospective`) | accepted, decided `retrospective` ≠ recorded (no refusal) | §10.17, §15.6 | `HISTORY.INADMISSIBLE_EVENT` |
| 21 | `engine-proposal-not-active` | v27 `ProposalWasRejected` | `PROPOSAL.NOT_ACTIVE` | §10.28, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 22 | `engine-reopen-without-new-evidence` | v38 `HypothesisTransitioned` (`reopen`) | `HYPOTHESIS.REOPEN_NEEDS_NEW_EVIDENCE` | §10.3, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 23 | `engine-resolve-cause-no-longer-holds` | v57 `LifecycleChanged` → `resolved` | `RESOLVE.CAUSE_NO_LONGER_HOLDS` | §14.2, §15.4 | `HISTORY.UNGATED_RESOLUTION` |
| 24 | `engine-resolve-correction-not-verified` | v33 `LifecycleChanged` → `resolved` | `RESOLVE.CORRECTION_NOT_VERIFIED` | §14.3, §15.4 | `HISTORY.UNGATED_RESOLUTION` |
| 25 | `engine-resolve-criterion-unsatisfied` | v58 `LifecycleChanged` → `resolved` | `RESOLVE.CRITERION_UNSATISFIED` | §14.4, §15.4 | `HISTORY.UNGATED_RESOLUTION` |
| 26 | `engine-resolve-no-conclusion` | v55 `LifecycleChanged` → `resolved` | `RESOLVE.NO_CONCLUSION` | §14.6, §15.4 | `HISTORY.UNGATED_RESOLUTION` |
| 27 | `engine-resolve-no-correction` | v32 `LifecycleChanged` → `resolved` | `RESOLVE.NO_CORRECTION` | §14.3, §15.4 | `HISTORY.UNGATED_RESOLUTION` |
| 28 | `engine-resolve-residual-not-stated` | v58 `LifecycleChanged` → `resolved` | `RESOLVE.RESIDUAL_NOT_STATED` | §14.7, §15.4 | `HISTORY.UNGATED_RESOLUTION` |
| 29 | `engine-verification-evidence-predates-round` | v35 `VerificationRecorded` (`satisfied`) | `VERIFICATION.EVIDENCE_PREDATES_ROUND` | §12.2.5, §15.3, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 30 | `engine-verification-without-criteria` | v56 `CorrectiveActionTransitioned` → `awaitingVerificationOfEffect` | `CORRECTION.NO_CRITERIA` | §10.19, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 31 | `forged-cause-confirmation` | v29 `CausalAssertionConfirmed` | `CAUSE.MEMBER_NOT_SUPPORTED` (first of 6: then `CONTRADICTING_EVIDENCE`, `OPEN_CONTRADICTION`, `OPEN_UNKNOWN`, `ALTERNATIVE_NOT_ADDRESSED` ×2) | §11.4, §15.4 | `HISTORY.UNGATED_CONFIRMATION` |
| 32 | `forged-resolution` | v35 `LifecycleChanged` → `resolved` | `RESOLVE.CORRECTION_NOT_VERIFIED` (first of 4: then `CRITERION_UNSATISFIED` ×2, `NO_CONCLUSION`) | §14, §15.4 | `HISTORY.UNGATED_RESOLUTION` |
| 33 | `illegal-hypothesis-transition` | v27 `HypothesisTransitioned` (`proposed` → `supported`) | `TRANSITION.ILLEGAL` | §7.1, §10.3, §15.4 | `HISTORY.INADMISSIBLE_EVENT` |
| 34 | `missing-start` | v1 `LifecycleChanged` (state `⊥`) | — | §15.2 2.1 (then `SEQUENCE`/`NO_START` repeat for every later group, §15.9) | `HISTORY.NO_START` |

Fixtures 1–30 are each the **sole** reason for refusal (one error in the whole list), as CONTRACT §14 states; this was
confirmed for all of them. No fixture with this expected code was left unexplained.

Related fixtures rejected **before** stage 15 that would otherwise exercise an engine rule (informative):
`agent-authored-hypothesis` (stage 7; would be `AGENT.PROPOSAL_ONLY`, §8.1), `artifact-not-http` (stage 7 `httpUrl`
pattern; would be `EVIDENCE.URL`, §10.4.1 (a)), `blank-text`, `ideographic-space-text` (stage 7; would be
`VALIDATION.EMPTY`), `correlation-reused` (stage 14; §15.1 would treat the groups separately).

### 18.2 Refusals in `golden/scenarios.json` (5)

These commands were refused by the oracle and never persisted (CONTRACT §12.1). "State" is the replayed state of the
named scenario after the persisted groups preceding the step. `engineCodes` (the full ordered refusal list) is
reproduced by §8–§14 in exactly the recorded order. If such a command's events were persisted anyway, §15.4 gives the
history code in the last column.

| scenario | step | command | state | `engineCodes` (order) | `expectedCode` | rule sections | history code if persisted |
|---|---|---|---|---|---|---|---|
| `payment-failure` | 25 | `ResolveInvestigation` | all 25 persisted groups; lifecycle `active` | `RESOLVE.NO_CONFIRMED_CAUSE; RESOLVE.NO_CORRECTION; RESOLVE.NO_CONCLUSION` | `RESOLVE.NO_CONFIRMED_CAUSE` | §9, §14.1, §14.3, §14.6 | `HISTORY.UNGATED_RESOLUTION` |
| `conflicting-metrics` | 27 | `ConfirmCause` (the scenario's only cause) | first 27 groups | `CAUSE.MEMBER_NOT_SUPPORTED; CAUSE.CONTRADICTING_EVIDENCE; CAUSE.OPEN_CONTRADICTION; CAUSE.OPEN_UNKNOWN; CAUSE.ALTERNATIVE_NOT_ADDRESSED; CAUSE.ALTERNATIVE_NOT_ADDRESSED` | `CAUSE.MEMBER_NOT_SUPPORTED` | §11.4.2–§11.4.6 | `HISTORY.UNGATED_CONFIRMATION` |
| `conflicting-metrics` | 28 | `RecordConclusion` (`strength: confirmedCause`, `causeIds: [that cause]`) | first 27 groups | `CONCLUSION.UNSUPPORTED` | `CONCLUSION.UNSUPPORTED` | §13.2 | `HISTORY.INADMISSIBLE_EVENT` |
| `conflicting-metrics` | 30 | `ResolveInvestigation` | all 28 persisted groups | `RESOLVE.NO_CONFIRMED_CAUSE; RESOLVE.NO_CORRECTION; RESOLVE.BLOCKING_UNKNOWN` | `RESOLVE.NO_CONFIRMED_CAUSE` | §14.1, §14.3, §14.5 | `HISTORY.UNGATED_RESOLUTION` |
| `api-latency` | 34 | `ResolveInvestigation` | all 34 persisted groups; lifecycle `awaitingVerification` | `RESOLVE.CORRECTION_NOT_VERIFIED; RESOLVE.CRITERION_UNSATISFIED; RESOLVE.CRITERION_UNSATISFIED; RESOLVE.NO_CONCLUSION` | `RESOLVE.CORRECTION_NOT_VERIFIED` | §14.3, §14.4, §14.6 | `HISTORY.UNGATED_RESOLUTION` |

The two scenarios without refusals (`intermittent-outage`, `authentication-outage`) and the three above are
admissible under §15 (empty error list).

## Appendix A. Reference algorithm (informative)

```text
validate(events):
  S := ⊥ ; errors := []
  for G in groups(events):                         # maximal runs of equal correlationId (§15.1)
    x := (S = ⊥ ? 0 : S.version) + 1
    for i, e in enumerate(G):                       # §15.2 step 1
      if e.aggregateVersion ≠ x + i: errors += [HISTORY.SEQUENCE]
    cmd := commandOf(G)                             # §15.3
    if S = ⊥ and G[0].eventType ≠ InvestigationStarted: errors += [HISTORY.NO_START]
    elif cmd = none:                                errors += [HISTORY.INADMISSIBLE_EVENT]
    elif not consistent(G):                         errors += [HISTORY.INADMISSIBLE_EVENT]
    else:
      r := decide(S, {G[0].investigationId, G[0].actor, G[0].occurredAt, cmd})   # §8
      if r is Refuse(fs):    errors += [ codeFor(G) for each f in fs ]           # §15.4
      elif r.events ≠ G:     errors += [HISTORY.INADMISSIBLE_EVENT]             # §15.6
    for e in G:                                     # §15.2 step 4
      S := (S = ⊥ and e.eventType ≠ InvestigationStarted) ? ⊥ : apply(S, e)
  return errors                                     # admissible iff empty; first element = domain code
```
