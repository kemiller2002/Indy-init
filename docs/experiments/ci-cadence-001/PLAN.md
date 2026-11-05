# CI-CADENCE-001: plan

**Question.** Does running full CI about once an hour, with continuous local compile and test, produce more independently verified Causa functionality per unit of elapsed time and compute than the previous CI strategy?

**Status of this document.** Written during preparation, before T+0. It is not changed after T+0. Any deviation is recorded in [TIMELINE.jsonl](TIMELINE.jsonl) and [FINAL-REPORT.md](FINAL-REPORT.md).

## Boundaries (from the directive)
- **Isolation.**
  - The experiment runs on `experiment/ci-cadence-001` and its per-agent branches `experiment/ci-cadence-001-{domain,ui,controls}`.
  - Each agent works in its own worktree under `/home/user/exp/`.
  - Nothing merges into `main`.
- **Rehearsal 010.** It is not started. No owner decision is taken, and no approval record is written.
- **Frozen material.** Frozen requirements, the catalog, `verification/` and the workflows are not edited.
- **Application code.**
  - The experiment extends `preview/causa/` (precompetition code, OD-INDY-22) on experiment branches only.
  - None of it may enter the competition build.
- **Persistence claims.** The proxy in this container blocks GitHub GraphQL writes (preview D-14). Every save in this experiment therefore runs against the in-process `FakeGitHub` double or a Playwright-routed double. Such a save is **component behaviour, never real-provider persistence**.

## Starting state
- **Starting commit:** `48823515234042b6c8010f4f1993a634fd14e7d8` (Indy-init `main`, PR #138 merge). `origin/main` later moved to `d4fdef9` (#134, proposal files only); the experiment does not include it.
- **Dependencies:**

| Dependency | Version |
|---|---|
| .NET SDK | 10.0.402 (`global.json`, rollForward disabled) |
| Node | 22.23.4 |
| Playwright | 1.63.0 (preinstalled Chromium) |
| Limen | 0.9.1 |
| Forma (design-system) | 0.10.0 |
| causa-data | `e8259df74add723765c57a5b1a5888d9c3f707af` |
| Causa.HistoricalCompatibility | `proposals/r10-historical-data-compatibility` at the starting commit |

## Work slice: investigation-start and content validation (catalog `CAT-CAUSA-INVESTIGATION`, ratified)

### Targeted for acceptance
A requirement counts as accepted only if **every** verification obligation the catalog lists for it passes, and an independent verifier confirms it.

| ID | Type | Expected behaviour | Obligations | Acceptance test |
|---|---|---|---|---|
| INV-019 | validation-rule | An empty or whitespace-only problem statement is refused with a field message. No investigation is created and nothing is written. | mapped, validation, rejects | engine: blank, whitespace-only and `" \t"` statements produce a field error on INV-001 and no write (head unchanged). Browser: the message is shown on the field (`aria-invalid`, `aria-describedby`). |
| INV-020 | validation-rule | A title longer than 200 characters is refused (`maxLength` 200). | mapped, validation, rejects | engine: lengths 200 (accepted) and 201 (refused, no write). Browser: the field message is shown. |
| INV-021 | validation-rule | A problem statement longer than 10,000 characters is refused (`maxLength` 10000). | mapped, validation, rejects | engine: lengths 10,000 (accepted) and 10,001 (refused, no write). |
| INV-022 | action | **Cancel investigation start.** It leaves the start screen; no investigation is created and nothing is written. | mapped, exists, visible, accessible, activate, outcome | browser: a button with accessible name "Cancel" and `data-requirement-id="INV-022"`; activating it after typing returns to the list. Engine: no commit request is issued and the head is unchanged. |
| INV-005 | ui-control | **Persistence destination indicator.** It is shown on the start screen before a remote write. | mapped, exists, visible, accessible | browser: an element with `data-requirement-id="INV-005"`, visible, with an accessible name, naming the write repository, branch and workspace path. |
| INV-016 | ui-control | **Save state.** It is shown on the investigation workspace. | mapped, exists, visible, accessible | browser: a `role=status` element with `data-requirement-id="INV-016"` that states unsaved/saved. |

### Partially targeted (not claimable as accepted)
These are validation sub-obligations only. Their remaining obligations (save, reload, update against the real provider) are out of reach here.

| ID | Sub-obligations targeted | Not targeted |
|---|---|---|
| INV-024 (observation text, 1..10000) | validation, rejects, mapped, exists, visible, editable, enter | accessible (partly), save/reload/update against the real provider |
| INV-026 (hypothesis statement, 1..10000) | validation, rejects, mapped, exists, visible, editable, enter | accessible (partly), save/reload/update against the real provider |
| INV-001, INV-002 | field IDs mapped (`data-requirement-id`), exists, visible, accessible, editable, enter | save against the real provider. INV-002 is `required: false`, but the storage contract (`EventDecoder` `Field.text "title"`) requires a non-blank title. The existing preview refuses a blank title. Resolving that is an owner/contract question, **not taken here**. |

### Field IDs to verify
INV-001, INV-002, INV-005, INV-016, INV-019, INV-020, INV-021, INV-022, INV-024 and INV-026: each must appear on a rendered element as `data-requirement-id` (the convention in `verification/`). A missing ID fails traceability.

### Length semantics (routine technical decision)
Length is counted in UTF-16 code units, which is what the HTML `maxlength` attribute counts. A value is blank when it has no non-whitespace character (`Text.tryCreate`, the existing rule).

## Agents and file ownership (nonoverlapping)

| Agent | Worktree, branch | Owns |
|---|---|---|
| lead (this session) | `/home/user/exp/integration`, `experiment/ci-cadence-001` | the kickoff interface stub, `tests/Engine.Tests/Program.fs` (the runner line only), integration merges, negative controls (`/home/user/exp/controls`), evidence |
| domain | `/home/user/exp/domain`, `experiment/ci-cadence-001-domain` | `src/Causa.Preview.Engine/Validation.fs`, `Writer.fs`, `Causa.Preview.Engine.fsproj`, `tests/Engine.Tests/ValidationTests.fs`, `Engine.Tests.fsproj` |
| ui | `/home/user/exp/ui`, `experiment/ci-cadence-001-ui` | `Model.fs`, `Update.fs`, `View.fs`, `web/index.html`, `web/app.css`, `tests/Engine.Tests/StartScreenTests.fs`, `tests/start-form.mjs` |
| verifier (fresh agent at T+45) | read-only on the integration candidate | none: it runs the tests and checks each obligation independently |

**Frozen interface, committed at T+0 by the lead.**

```fsharp
module Causa.Preview.Validation
type FieldError = { FieldId: string; RuleId: string; Message: string }
val length : string -> int                                     // UTF-16 code units
val startForm : title: string -> problemStatement: string -> FieldError list   // INV-019/020/021 (+ existing non-blank title)
val observationText : string -> FieldError list                // INV-024
val hypothesisStatement : string -> FieldError list            // INV-026
```

## Cadence under test
- **Level 1 (continuous, local).** After each meaningful change, the agent:
  - compiles the affected projects (`dotnet build tests/Engine.Tests`);
  - runs the engine tests;
  - runs `tests/ArchitectureCheck.fsx` (A1–A7) when web or JS files change;
  - runs `tools/r10/change-impact.fsx` (advisory selection);
  - commits locally. Pushing an experiment branch triggers no CI: Preflight tooling runs on `push` only for `main`.
- **Full CI (once, at T+45).**
  - `workflow_dispatch` of **Preflight tooling** (all required jobs) on `experiment/ci-cadence-001`.
  - The **complete preview application suite** in a clean worktree: build, A1–A7, engine tests, `read-path.mjs`, `start-form.mjs`.
  - It is run in both places because the required GitHub CI does not build or test `preview/causa` at all (no workflow references it). This is a finding in itself.
- **No frequent-CI control run.** A matched frequent-CI development run is optional and is not performed (time). The comparison uses the measured CI data below and labelled models.

## Timeline
| Time | Action |
|---|---|
| T+0 | Kickoff commit; launch the domain and ui agents in the background. The lead runs the negative controls. |
| T+0 to T+42 | Agents develop with Level 1 checks and commit locally. |
| T+42 to T+45 | Agents stop and report. |
| T+45 | The lead merges both branches, then runs the integration and acceptance checks. Full CI is dispatched and the verifier agent is launched. |
| T+60 | Development cutoff, with state recorded. Verification already started may finish; its completion time is reported separately. |

## Negative controls (disposable worktree `experiment/ci-cadence-001-controls`, never merged)
- **NC-1.** A known failing unit test: the engine test runner must exit non-zero.
- **NC-2.** An interface incompatibility (rename `Validation.startForm`): the build of Engine.Tests must fail.
- **NC-3.** A failed mandatory CI check cannot be reported as passing:
  - the r10-adversarial merge check (its jq logic) is fed a failed shard result;
  - [CI-RUNS.json](CI-RUNS.json) records only API-observed conclusions.
- **NC-4.** An incomplete requirement cannot be marked accepted: the acceptance computation over [REQUIREMENTS.json](REQUIREMENTS.json) must refuse a requirement with a failing or missing obligation.
- **NC-5.** A missing field ID cannot satisfy traceability: delete one `data-requirement-id`, and the browser trace check must fail.
