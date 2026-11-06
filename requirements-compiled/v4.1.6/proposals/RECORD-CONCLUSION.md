# Contract gap 1: recording a conclusion (Rehearsal 010)

**Status: unverified analysis and proposal.** Nothing here was compiled, run or reviewed. Nothing here is an owner
decision, and no approval record, lock or frozen package was changed. Agents never record decisions (RDP-003,
`AGENTS.md`).

## Sources and citation keys

| key | repository and revision | read as |
|---|---|---|
| `Indy-init:` | `kemiller2002/Indy-init` `origin/main` at `89f5840` | worktree `/home/user/wt/contract` |
| `app:` | `kemiller2002/indy-init-rehearsal-010` `origin/main` at `0bd6e0c` | worktree `/home/user/wt/contract-app` |
| `causa-data:` | `kemiller2002/causa-data` tag `causa-storage-v1.0.0` (commit `c947c7e`); `contract/v1.0.0`, `fixtures/v1.0.0` and `compatibility/v1.0.0` are byte-identical at `main` `e8259df` | clone `/home/user/wt/causa-data` |

A citation is `key:path:line` or `key:path:first-last`.

## 1. Question

INV-032 "Record a conclusion" is a required action, verified by `activate`, `outcome`, `save` and `reload`. Its note is
"Records a snapshot; it does not resolve the investigation, whose existing gates are unchanged"
(`Indy-init:requirements/catalogs/causa-investigation.requirement-catalog.json:1042-1079`, note at `:1078`). INV-033 is
its statement: required, 1 to 10,000 characters (`:1081-1123`). Journey J2-07 fills "Conclusion statement", presses
"Record a conclusion" and expects the statement to be visible (`Indy-init:verification/journeys/j2-analysis.spec.mjs:79-84`).
J2-09 expects it to be held by the provider and visible after a reload (`:95`).

The question is whether the frozen v4.1.5 `CoreCommand` (`Indy-init:requirements-compiled/v4.1.5/contracts/00-DOMAIN-STUBS.fs:62-91`)
can record a conclusion without information loss.

## 2. The target: the stored `ConclusionRecorded` event

causa-storage v1.0.0 defines `ConclusionRecorded` (`causa-data:contract/v1.0.0/CONTRACT.md:315-325`):

| wire field | type | nullable | meaning |
|---|---|---|---|
| `conclusionId` | uuid | no | conclusion id |
| `statement` | text, non-blank, verbatim | no | the conclusion |
| `strength` | enum `conclusionStrength` (`plausibleExplanation`, `leadingExplanation`, `supportedCause`, `confirmedCause`, `acceptedWithResidualUncertainty`) | no | strength |
| `causeIds` | ordered, distinct uuid array | no | the causes it rests on |
| `residualUncertainty` | text | yes | residual uncertainty (R6.4) |

The projection creates a conclusion entity with these fields plus `recordedAtVersion`
(`causa-data:contract/v1.0.0/CONTRACT.md:637`). The current conclusion is the one with the highest `recordedAtVersion`
(`:694`). v1.0.0 has no reassess or supersede event for conclusions. A later conclusion is a new `ConclusionRecorded`.

The domain already holds it field for field. v4.1.5 `CoreEvent.ConclusionRecorded of Conclusion` is in
`Indy-init:requirements-compiled/v4.1.5/contracts/00-DOMAIN-STUBS.fs:157`. The app's record is
`app:src/Causa.Domain/Entities.fs:176-182`. The wire encoding is `app:src/Causa.Persistence/Reader.fs:187-196`, the
decoding `:800-814`, and the projection `:350-354`. Replay admits the event at `app:src/Causa.Domain/Evolve.fs:155-160`
and `:335-338`.

**The reference engine has a command that emits it alone.** The frozen fixtures were generated "by issuing real
commands through `Decide.execute` of the pinned engine" (`causa-data:contract/v1.0.0/CONTRACT.md:1013`). In
`fixtures/v1.0.0/investigations/a6747bd3-…/events.ndjson`, `ConclusionRecorded` at version 55 has its own correlation
id (`d54ca756-…`). The events before it (`f949283c-…`) and after it (`16873376-…`) belong to other commands, so a
conclusion is a one-event command group. The catalogs contain 3 `ConclusionRecorded` in `workspaces/demo/historical-v1`,
4 in `demonstration-v1` and 1 in `everyday-v1`.

## 3. Verdict: not representable

**No case of the frozen `CoreCommand` can produce `ConclusionRecorded`.** Recording a conclusion through another
command either drops wire fields or records a different fact.

| candidate | what it emits | why it fails |
|---|---|---|
| `ResolveInvestigation` (no payload: `00-DOMAIN-STUBS.fs:86`) | `InvestigationResolved` → `LifecycleChanged{from,to:"resolved",command:"resolveInvestigation",reason:null}` (`causa-data:…/CONTRACT.md:471-482`, where `reason` must be null; `app:…/Reader.fs:65`) | It carries none of the five fields. It changes the lifecycle, which INV-032's note forbids (`catalog.json:1078`). It cannot even be issued: the resolution gate refuses it while no conclusion exists (`resolution.no-conclusion`, `app:src/Causa.Domain/Policies.fs:237`; negative fixture `engine-resolve-no-conclusion`), so the requirement would depend on itself. |
| `RecordFinding` | `FindingRecorded{findingId, actionId, originalResult, interpretation}` (`CONTRACT.md:425-434`) | It loses `strength`, `causeIds` and `residualUncertainty`. It needs an existing investigative action (`app:src/Causa.Domain/Decide.fs:118-123`). A finding is "the actual result of an action", a different fact, and the catalog keeps findings and conclusions apart (`Indy-init:docs/INVESTIGATION-SEMANTICS.md:205`). `s.Conclusions` stays empty, so resolution stays blocked. |
| `AttachExtension` / `Command.Extension` | `ExtensionAttached` / `ExtensionEvent` | Both are refused as `UnsupportedSemantics` (`Decide.fs:185`, `:193-194`, `:207`). They cannot be encoded (`Reader.fs:57`, `:201`: `CAUSA.UNREPRESENTABLE`), and replay refuses them (`app:src/Causa.Domain/Kernel.fs:39`). v1.0.0 "defines no extension namespace" (`CONTRACT.md:55-56`, `:1070-1074`). |
| Proposal flow (`RecordProposal`, then `AcceptProposal`) | `ProposalRecorded{kind ∈ hypothesis\|unknown\|action}` (`CONTRACT.md:202`, `:517-526`) | No proposal kind is a conclusion. A proposal is "never evidence or authoritative state" (`CONTRACT.md:519`). Acceptance must emit the target command's events and then `ProposalWasAccepted` (`CONTRACT.md:530`), but no command emits `ConclusionRecorded`. `AcceptProposal` is always refused (`Decide.fs:174-177`). |
| `RecordObservation`, `RecordHypothesis`, `EstablishNegativeKnowledge`, `RecordCausalAssertion` + `ConfirmCause` | their own events | Each records a different kind of fact, which D-001 forbids conflating (`Indy-init:docs/DESIGN-DECISION-BOUNDARIES.md:22`). `strength` and `residualUncertainty` have nowhere to go. |

The domain owner's own test records the gap: "causa-storage records conclusions (ConclusionRecorded) but the frozen
CoreCommand has no RecordConclusion, so a conclusion can come only from history"
(`app:tests/Causa.Domain.Tests/Identity.fs:319-321`).

**Where the gap comes from.** The command catalog defines `RecordConclusion | ConclusionRecorded`
(`Indy-init:docs/COMMAND-EVENT-CATALOG.md:403`) and lists it as item 25 of the first implementation's minimum command
set (`:620`). The type-shapes `CoreCommand` (`Indy-init:docs/FSHARP-TYPE-SHAPES.md:835-864`) left it out, together with
items 24 (`MarkCorrectiveActionVerified`) and 32 (`PinPolicyVersion`). v4.1.2 froze that union, and v4.1.5 deferred
"commands for the 22 added event kinds" (`Indy-init:requirements-compiled/v4.1.5/README.md:32`).

**Consequence under v4.1.5.**
- INV-032, INV-033, J2-07 and the J2-09 conclusion check cannot pass.
- `ResolveInvestigation` cannot succeed for any investigation created in the app, because the resolution gate needs a
  conclusion. Every "resolved" outcome is therefore unreachable except in a history that was loaded.

## 4. Proposal: `RecordConclusion`

The owner already chose the direction. **OD-CR-01 A is recorded** (APR-CR-OWNER-1 and -2, 2026-11-06,
`Indy-init:kickoff/rehearsal-007/approvals.causa.json:956-1043`). Its question asks for a v4.1.6 successor "with the
matching CoreEvent cases and 7 CoreCommand cases including RecordConclusion"
(`Indy-init:docs/contract-readiness/causa-precompetition/OWNER-DECISIONS.proposed.json:49`; registry entry
`CMD-CONCL-RECORD` at `…/COMMAND-EVENT-REGISTRY.json:416-437`). This section gives that command an exact
specification. **Unlike the other six commands, `RecordConclusion` needs no storage change.** Every event it emits
already exists in causa-storage v1.0.0.

### 4.1 Interface change (successor package v4.1.6)

In `contracts/00-DOMAIN-STUBS.fs` of v4.1.6 (a copy of v4.1.5; v4.1.5 itself is never edited, `Indy-init:AGENTS.md:7`):

```fsharp
type RecordConclusion = class end          // opaque, like every command payload; the domain owner defines it
type CoreCommand =
    | ...
    | RecordVerification of RecordVerification
    | RecordConclusion of RecordConclusion  // new: placed as in COMMAND-EVENT-CATALOG §31 (item 25), before ResolveInvestigation
    | ResolveInvestigation
    | ...
```

The domain owner's definition (`app:src/Causa.Domain/Commands.fs`) follows the v4.1.5 pattern. A command payload is
exactly the record of the event it emits (`Commands.fs:9-36`; stub comment
`00-DOMAIN-STUBS.fs:59-61`, `:95-99`):

```fsharp
type RecordConclusion = Conclusion
// = { ConclusionId: ConclusionId; Statement: NonEmptyText; Strength: ConclusionStrength;
//     CauseIds: CausalAssertionId list; ResidualUncertainty: NonEmptyText option }
```

### 4.2 Payload: 1:1 with the wire

| command field | type | wire field | source in the UI |
|---|---|---|---|
| `ConclusionId` | `ConclusionId` (Guid) | `conclusionId` | new UUID from the command-creation layer (`FSHARP-TYPE-SHAPES.md` §43). Decide never invents ids. |
| `Statement` | `NonEmptyText` | `statement` | INV-033, verbatim: never trimmed or normalized (`CONTRACT.md:59-63`) |
| `Strength` | `ConclusionStrength` (5 cases, `app:src/Causa.Domain/Semantics.fs:192-197`) | `strength` | **No catalog field today.** OD-CR-04 A (recorded) adds a required field INV-046 "Conclusion strength" (`OWNER-DECISIONS.proposed.json:122`), which is not yet in the catalog (no INV-046 in `causa-investigation.requirement-catalog.json`). See decision RC-3. |
| `CauseIds` | `CausalAssertionId list`, ordered, distinct | `causeIds` | INV-032 asks for none, so `[]` (registry `COMMAND-EVENT-REGISTRY.json:416-437`). Kept in the payload because the wire has it and the reference fixtures use it. |
| `ResidualUncertainty` | `NonEmptyText option` | `residualUncertainty` | No catalog field, so `None` from the UI. See RC-4. |

The command carries no field the wire lacks. `FSHARP-TYPE-SHAPES.md` §27 lists `Evidence`, `ResidualUnknowns`,
`UnresolvedContradictions`, `AcceptedAssumptions`, `PolicyRefs` and `Extensions` (`Indy-init:docs/FSHARP-TYPE-SHAPES.md:716-728`).
They are **not** added: the wire cannot store them, and v4.1.5 E1 pins payloads to the wire
(`Indy-init:requirements-compiled/v4.1.5/AMENDMENT-4.1.5.md:7`). The state at `recordedAtVersion` can be derived by
replaying to that version.

### 4.3 Validation (Decide)

The order follows STATE-TRANSITION-TABLES §16, as `Decide.decide` already does (`app:src/Causa.Domain/Decide.fs:190-219`):
semantics, then duplicate command, then expected version, then actor, then the structural checks below, then the
policy blockers. Every failing check is returned, not only the first (`whenAll`, `Decide.fs:11-14`).

| # | rule | blocker code | source |
|---|---|---|---|
| V1 | The investigation is open for recording: `Draft`, `Active`, `AwaitingEvidence` or `AwaitingVerification`. | `investigation.read-only` | `Policies.fs:41-49`; replay rule `INVESTIGATION.NOT_OPEN` (`Evolve.fs:118-119`) |
| V2 | `ConclusionId` is new. | `conclusion.exists` | `Evolve.fs:337` (`create`) |
| V3 | Every `CauseIds` element exists. | `cause.missing` | `Evolve.fs:336` |
| V4 | `CauseIds` has no duplicates. The command boundary removes them before deciding (`CONTRACT.md:64-68`); Decide refuses any that remain. | `conclusion.duplicate-cause` | `CONTRACT.md:64-68` |
| V5 | If a **blocking** unknown is `identified` or `investigating`, `ResidualUncertainty` is `Some`. | `CONCLUSION.RESIDUAL_REQUIRED` | `Evolve.fs:155-156`; negative fixture `engine-conclusion-without-residual`; R6.4 |
| V6 | If `Strength = confirmedCause`, every cited cause is `confirmed`. | `CONCLUSION.UNSUPPORTED` | `Evolve.fs:157-160`; fixture `engine-confirmed-conclusion-unconfirmed-cause`; R6.1; catalog §21 "cannot grant a stronger causal state than policy permits" (`COMMAND-EVENT-CATALOG.md:407`) |
| V7 (policy, proposed) | `supportedCause` and `confirmedCause` cite at least one cause. | `conclusion.no-cause` | R6.1. Today V6 accepts `confirmedCause` with `causeIds = []`, because `List.forall` over an empty list is true (`Evolve.fs:159`). |
| V8 (policy, proposed) | `acceptedWithResidualUncertainty` states `ResidualUncertainty`. | `conclusion.residual-not-stated` | R6.4; `INVESTIGATION-SEMANTICS.md:1033` |

V1–V6 are what the replay already enforces, so a decided event always evolves (the firewall at `Evolve.fs:410-416`).
V7 and V8 are **Decide-time policy rules only**, in the `causa.core/1` component (`Policies.fs`). They are not
admissibility rules: a stored history the reference engine accepts must still replay, and the reference engine's
`History.validate` is the oracle (`CONTRACT.md:997-1007`). Adopting V7 and V8 is decision RC-5.

UI-level validation is not the domain's. INV-033 requires at least 1 and at most 10,000 characters
(`catalog.json:1089-1092`). The view's field rules and the command-creation layer enforce the maximum. The domain's
`NonEmptyText` has no maximum, matching the wire, which has none.

### 4.4 Authorization and capabilities

- **Actor.** Human or system. An agent actor may appear only on `ProposalRecorded` (`CONTRACT.md:157-159`, enforced by
  the schema and by the app's reader at `Reader.fs:1008-1011`). No proposal kind is a conclusion, so an agent cannot
  record a conclusion, directly or by acceptance. `ActorContext` is `{ActorId}` only
  (`00-DOMAIN-STUBS.fs:179`), so the actor kind is supplied by the save layer. OD-CR-07 A (recorded) gives a fixed
  configured `causa-operator` of kind `human`.
- **Policy.** Decided under the composed `PolicySet` like every command. An unknown component is
  `UnsupportedSemantics` (`Policies.fs`, `Policy.rules`).
- **Lifecycle.** Allowed in `Draft`, `Active`, `AwaitingEvidence` and `AwaitingVerification`. Refused in `Resolved`,
  `Closed` and `Archived` until the investigation is reopened (V1). The lifecycle is never changed.
- **Capabilities.** Add one global probe to `Capabilities.probes` (`app:src/Causa.Domain/Capabilities.fs:37-111`):
  `"RecordConclusion", None, RecordConclusion { ConclusionId = probeId; Statement = placeholder; Strength = <the INV-046 value the UI submits when none is chosen, else PlausibleExplanation>; CauseIds = []; ResidualUncertainty = None }`.
  The probe sends exactly what the UI can send. While a blocking unknown is open, the capability is
  `Unavailable [CONCLUSION.RESIDUAL_REQUIRED]` with the engine's reason, and INV-032's button shows it as unavailable
  with that reason (UI-004). `Capabilities.available`, and therefore `InvestigationProjection.Capabilities`
  (`Kernel.fs:53-56`), gains the string `"RecordConclusion"`. The UI maps that capability to INV-032's `ActionId`.

### 4.5 Events emitted

Exactly one event: `CoreEvent.ConclusionRecorded` with the payload copied field for field. On the wire it is one
`ConclusionRecorded` line whose `correlationId` is the command's `CommandId` (`CONTRACT.md:145`, `:160-165`). It is a
one-event command group, like the reference engine's (§2). There is no `LifecycleChanged`. Registry test
`T-IMPL-CMD-CONCL-RECORD-01` checks that the lifecycle is unchanged.

### 4.6 Replay, Evolve and admissibility

- **No change to Evolve, Replay, the wire or the projection.** `ConclusionRecorded` is already decoded, admitted and
  projected (§2). The new command produces only events the v1.0.0 reader already accepts, so a history the app writes
  stays causa-storage v1.0.0. Its `formatVersion` and `semanticVersion` stay `"1.0.0"`, and no `causa-storage` release
  is needed for this command.
- **Existing stored histories that contain `ConclusionRecorded` are unaffected.** They contain no command, only events.
  Adding a `CoreCommand` case changes no event case, wire byte or projection rule. Replaying the 5 frozen fixtures and
  the 1,363 + 1,315 + 48 catalog events (`app:tests/Causa.Acceptance/Live/EXTDATA011Tests.fs`) must give the same
  states and digests as before. Test RC-T6 checks this.
- **Admissibility of new histories.** A one-event group with the actor and time of its command. It must satisfy V5 and
  V6, the reference engine's rules that negative fixtures pin. It must use a fresh correlation id
  (`CAUSA.CORRELATION_REUSED`, `CONTRACT.md:160-165`). The resolution gate then sees `s.Conclusions` non-empty
  (`Policies.fs:236-237`), and `resolution.residual-not-stated` can be met by a conclusion that states residual
  uncertainty (`:254-255`).

### 4.7 Consequences outside the domain

These are not part of the interface change, but they follow from it.

- **View.** `ConclusionCard` (`Indy-init:requirements-compiled/v4.1.5/contracts/07-IF41-VIEW.fs:543`) has no
  `Strength`. INVESTIGATION-SEMANTICS asks for "conclusion strength and residual uncertainty … visible together"
  (`Indy-init:docs/INVESTIGATION-SEMANTICS.md:1033`). Showing the strength is a further breaking change to IF41-VIEW
  (RC-6). J2-07 does not need it.
- **J2-07 against OD-CR-04 A.** J2-07 fills only the statement (`j2-analysis.spec.mjs:81-82`). If INV-046 is a
  required choice group with no initial selection (the Forma pattern INV-031 uses, `catalog.json:1039`), J2-07 fails
  at validation. The same applies to J2-03 and OD-CR-02 A's INV-045 "Evidence category": J2-03 fills no category
  (`:48-51`). See RC-3.

## 5. Contract-change mechanics

1. **A new successor package.** v4.1.5 is never edited (`Indy-init:AGENTS.md:7`). v4.1.6 copies v4.1.5 and changes
   `contracts/00-DOMAIN-STUBS.fs` as in §4.1. Under OD-CR-01 A it also carries the other six command cases and the
   matching `CoreEvent` cases. Those depend on causa-storage 1.1.0 (`STORAGE-1.1.md`); this one does not.
2. **The change is breaking.** "Any change to a union or record, including an added case, is breaking. It goes through
   the coordinator as a new version, with every consumer's contract tests changed and passing in the same pull
   request" (`Indy-init:requirements-compiled/v4.1.5/AGENT-RULES.md:37`; `docs/REHEARSAL-AGENT-INTERFACE-FREEZE.md:32`).
   It is declared in `analysis/amendment.json` `contractChanges` with the new `00-DOMAIN-STUBS.fs` sha256. X17 sees one
   changed contract, X12 needs its approval id, and X05/X12 add no machine state or event. Then an independent review
   and FREEZE steps 1–5 as for v4.1.5 (`Indy-init:requirements-compiled/v4.1.5/FREEZE.md`).
3. **Compile-time effect in the competition repository.** `TreatWarningsAsErrors` turns every incomplete match on
   `CoreCommand` into a build error. Each one has to be handled: `Commands.name` (`app:src/Causa.Domain/Commands.fs:85-115`)
   and `Decide.onExisting` (`app:src/Causa.Domain/Decide.fs:60-185`). `Policy` rules end in `| _ -> []` and still
   compile.
4. **Tests changed in the same pull request.** Files and owners are from `requirements-compiled/v4.1.5/agent-file-ownership.json`.

| file | owner | change |
|---|---|---|
| `src/Causa.Domain/Commands.fs` | domain | `type RecordConclusion = Conclusion`; the case; `Commands.name` |
| `src/Causa.Domain/Decide.fs` | domain | the `RecordConclusion` branch (V1–V6) |
| `src/Causa.Domain/Policies.fs` | domain | V7 and V8 in `causa.core/1`, if RC-5 is A |
| `src/Causa.Domain/Capabilities.fs` | domain | the probe (§4.4) |
| `src/Causa.Domain/Kernel.fs` | domain | `InterfaceManifest.version` = `"4.1.6"` (`Kernel.fs:87`) |
| `tests/Causa.Domain.Tests/Identity.fs` | domain | the frozen case list (`:116-123`); replace the "no RecordConclusion" assertion (`:319-321`) with RC-T3 |
| `tests/Causa.Contract.Tests/Domain/Producer.fs` | domain | add a representative to `everyCommand` (`:536`); the coverage check (`:592-594`) fails without it. The index `everyCommand.[29]` (`:631`) moves to 30. |
| `tests/Causa.Contract.Tests/Domain/ConsumerIntegration.fs`, `ConsumerPersistence.fs`, `ConsumerUi.fs`, `ConsumerUi2.fs` | integration, persistence, ui, ui-2 | each consumer's contract test for the new case: integration maps the INV-032 intent to it; persistence round-trips its event; ui/ui-2 see the capability |
| `src/Causa.App/Engine.fs`, `Update.fs`, `Ui/Presentation.fs`, `Ui/UiAdapter.fs` | integration | INV-032 intent → `RecordConclusion`; the capability → INV-032's `ActionId` availability |
| `src/Causa.App/Views/Workspace/Overview.fs`, `Conclusions.fs` | ui-3 (ui in topologies A/B) | the editor (INV-033, and INV-046 once added) and the conclusion list |

5. **Re-pinning Rehearsal 010.** Its lock and scope pin v4.1.4 or v4.1.5 (`Indy-init:AGENTS.md:9`). Moving to v4.1.6
   needs the owner to re-approve the scope and the agent to re-pin `kickoff/rehearsal-010/toolchain.lock.proposed.json`
   (FREEZE step 5).

## 6. Test plan (nothing has been run)

| id | case | expected |
|---|---|---|
| RC-T1 | Active investigation, `RecordConclusion{statement "S", plausibleExplanation, [], None}` | `Allowed [ConclusionRecorded …]` with exactly those values. The lifecycle is unchanged. `Capabilities` contains `RecordConclusion` before and after. |
| RC-T2 | RC-T1 encoded through `FileSets.append`, then verified by the v1.0.0 reader | All 17 stages pass. The line's payload keys are `causeIds, conclusionId, residualUncertainty, statement, strength`, JCS-sorted. `formatVersion`/`semanticVersion` are `"1.0.0"`. Decoding and re-encoding give the same bytes. |
| RC-T3 | Correction, criterion and satisfied verification, then `RecordConclusion`, then `ResolveInvestigation` | Before the conclusion: `[resolution.no-conclusion]`. After it: `Allowed`. (This replaces `Identity.fs:319-321`.) |
| RC-T4 | Blocking unknown open, residual `None` | `Blocked [CONCLUSION.RESIDUAL_REQUIRED]`, and the capability shows that blocker. With `Some "…"`: `Allowed`. |
| RC-T5 | `confirmedCause` citing a proposed cause; `confirmedCause` with `[]`; `acceptedWithResidualUncertainty` with `None` | `CONCLUSION.UNSUPPORTED`; `conclusion.no-cause` (RC-5 A); `conclusion.residual-not-stated` (RC-5 A) |
| RC-T6 | Replay of every frozen fixture and catalog history before and after the change | Identical `Investigation` values, snapshot bytes and `expectedStateSha256`, including the 2 frozen fixtures and the catalog histories that hold the 8 catalog `ConclusionRecorded` events |
| RC-T7 | `Resolved` investigation | `Blocked [investigation.read-only]`. After `ReopenInvestigation`: `Allowed`. |
| RC-T8 | Same `CommandId` twice | `Duplicate`. A stale `ExpectedVersion` gives `Conflict`. |
| RC-T9 | Statement with leading and trailing spaces, U+200B, NFD text, 10,000 characters | Stored verbatim. After reload the statement compares ordinally equal. A 10,001-character statement is refused by the view's validation and never reaches Decide. |
| RC-T10 | J2-07 and J2-09 conclusion parts, from the governed checkout | Pass once RC-3 is settled |
| RC-T11 | The resulting history checked by causa-data's `causa gate`/reference consumer with the pinned engine (`History.validate`) | Admissible. This is the oracle check that the app's command emits what the reference engine would. |

## 7. Owner decisions required

Recorded decisions are not asked again: OD-CR-01 A (a v4.1.6 with RecordConclusion), OD-CR-04 A (INV-046), OD-CR-07 A
(the actor).

| id | decision | options | recommendation |
|---|---|---|---|
| **RC-1** | **Staging.** OD-CR-01 A bundles RecordConclusion with six commands that need causa-storage 1.1.0, which has not been released (`causa-data` holds only `contract/v1.0.0`). | **A:** v4.1.6 carries RecordConclusion alone now, because it needs no storage change, and v4.1.7 carries the six once 1.1.0 is released. This departs from OD-CR-01 A's single package, so it needs a record. **B:** wait and ship all seven in one v4.1.6. | **A.** It unblocks INV-032/033, J2-07 and every resolution path with a change that is known safe. OD-CR-01 alternative B already describes this (`OWNER-DECISIONS.proposed.json:50`). |
| **RC-2** | **Approve the v4.1.6 package** as compiled (the ODR-V416-02-equivalent approval, FREEZE step 3) and tag it. Re-approve Rehearsal 010's scope against it and re-pin the lock. | approve / reject | approve after X01–X21 and an independent review pass |
| **RC-3** | **J2-07 and J2-03 against OD-CR-04 A and OD-CR-02 A.** The new required catalog fields INV-046 and INV-045 are not filled by the governed journeys. | **A:** amend J2-03 and J2-07 to choose a category and a strength. They are owner-controlled assets. **B:** INV-045 and INV-046 start preselected, for example `plausibleExplanation` and `document`. That is a stored default, which OD-CR-04 A's "A" was meant to avoid. **C:** both fields are optional with that fallback. | **A.** No stored value is chosen for the user. |
| **RC-4** | **Residual uncertainty input.** The catalog has no field, so the UI cannot record a conclusion while a blocking unknown is open, and cannot meet `resolution.residual-not-stated`. | **A:** add an optional catalog field "Residual uncertainty" (≤10,000) mapped to `residualUncertainty`. **B:** accept that the capability is unavailable with the engine's reason in that state. | **A** for completeness. **B** is acceptable for Rehearsal 010, because J2 opens no unknowns. |
| **RC-5** | **Stronger Decide-time rules V7 and V8** (policy blockers, not admissibility) | A: adopt. B: only the engine's V5 and V6. | **A.** R6.1 and R6.4, and catalog §21 "cannot grant a stronger causal state". |
| **RC-6** | **Show the strength** (`ConclusionCard.Strength`): a breaking IF41-VIEW change | A: in v4.1.6 too. B: later. | **A** if v4.1.6 is recompiled anyway, otherwise **B**. |
| **RC-7** | **Position of the case** in `CoreCommand` | A: after `RecordVerification` (catalog §31 order). B: before `AttachExtension`. | **A**. The position matters only to reflection-ordered tests, which change in the same pull request either way. |
