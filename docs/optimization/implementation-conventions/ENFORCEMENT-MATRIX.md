# Enforcement matrix

Each convention mapped to the earliest inexpensive point that can catch a violation. The directive's checks and where they land:

- **A missing field mapping is caught before browser testing:** `ManifestTests` at contract freeze (CV-18, CV-23).
- **An invalid F# contract is caught before UI agents begin:** `ContractCheck.fsproj` and the negative contracts, at package release and at T14.
- **A contract-version mismatch is caught before integration:** frozen-package CI equality now; per-PR version naming is Tier 2 (CV-12, PA-04).

No new CI workflow is added, and no protected check is changed. The new checks run locally in seconds (`dotnet fsi tools/conventions/conventions.fsx -- check --workspace .`), and can be added to the bootstrap's existing CI only by the owner.

| Convention | Level | Mechanism kind | Stage (earliest) | Mechanism | Existing capability reused | Overhead in competition (central, min) | Duplicates an existing check? |
|---|---|---|---|---|---|---:|---|
| CV-01 Execute every generated command before freeze | MANDATORY | existing | package release | r9.fsx suite-preflight --require-tests; launch-preflight.fsx LP checks; Conditor scaffold CI | tools/r9/r9.fsx suite-preflight; tools/r10/launch-preflight.fsx | 0 | n/a (names the existing check) |
| CV-02 Stop only on STOP_PERMITTED, clock from the T0 record | MANDATORY | existing | during run | requirements-compiler stop-decision; --t0-record/--deadline-policy | AGENTS.md; docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md | 1 | n/a (names the existing check) |
| CV-03 One scope source, pinned and fully itemised before code | MANDATORY | existing | T0 / initialization | r9.fsx check-sequencing; launch-preflight LP-04 scope digest | tools/r9/r9.fsx; tools/r10/launch-preflight.fsx | 2 | n/a (names the existing check) |
| CV-04 WASM boot smoke on the first CI build; bounded view functions | PREFERRED | review | first green CI | boot smoke test in tier-1 CI (plan's first-render journey); review obligation for view size | - | 2 | no (review obligation) |
| CV-05 One source for environment literals | PREFERRED | new | pre-commit / CI (seconds) | tools/conventions/conventions.fsx check, rule CK-02 | - | 0.5 | no (new check) |
| CV-06 Workflows pass Conditor ci-policy | MANDATORY | existing | bootstrap (T10) | conditor ci-policy check | kemiller2002/conditor docs/ci-policy.md CIP-000..008 | 0.5 | n/a (names the existing check) |
| CV-07 Workflow shell is pipefail by default | MANDATORY | new | bootstrap / CI (seconds) | tools/conventions/conventions.fsx check, rule CK-03 | - | 0.5 | no (new check) |
| CV-08 Single-writer files and append-only shared state | MANDATORY | existing | bootstrap and every merge | agent-file-ownership.json + proposals/r10-contract-first-ui/tests/OwnershipTests.fs | requirements-compiled/v4.1.5/agent-file-ownership.json | 3 | n/a (names the existing check) |
| CV-09 Durable means read back from the real provider | MANDATORY | existing | live-provider preflight and candidate | live-provider preflight (J3-05); Dokimos evidence category real-provider vs double; state-machine tests | verification/journeys J3; requirements-compiled/v4.1.5/analysis/github-error-matrix.json | 2 | n/a (names the existing check) |
| CV-10 Typed errors through one state machine; no silent defaults | MANDATORY | new | every build (seconds) | tools/conventions/conventions.fsx check CK-01 (warnings-as-errors) + state-machine tests (existing) + review obligation | requirements-compiled/v4.1.5/state-machine-contracts.json | 1 | no (new check) |
| CV-11 Contracts proven against real data before release | MANDATORY | existing | package release | proposals/r10-historical-data-compatibility replay coverage (HISTORICAL-COVERAGE.json, EVENT-COMPATIBILITY-MATRIX.json) | requirements-compiled/v4.1.5 (analysis/amendment.json E1, W1); proposals/r10-historical-data-compatibility | 0 | n/a (names the existing check) |
| CV-12 Released contracts are immutable versions; consumers pin them | PREFERRED | existing | integration | frozen-package CI equality (AGENTS.md); contract tests at T14 (AGENT-RULES) | requirements-compiled/*/FREEZE.md | 2 | n/a (names the existing check) |
| CV-13 Every bound test has a falsifying control | PREFERRED | existing | slice gate | slice-gate/completion-gate assertion ratchet; negative controls (tools/requirements-compiler AssertionRatchet) | tools/requirements-compiler/src/AssertionRatchet.fs | 10 | n/a (names the existing check) |
| CV-14 Reconcile requirements with owner decisions before freeze | MANDATORY | review | package release | Package reviewer lists each owner decision naming a requirement and its amendment or waiver. | - | 0 | no (review obligation) |
| CV-15 Evidence is bound to the candidate SHA | MANDATORY | existing | slice merge and acceptance | slice-gate, completion-gate, catalog-acceptance | AGENTS.md (Rehearsal 010 governed verification) | 1 | n/a (names the existing check) |
| CV-16 Only the owner records approvals | MANDATORY | existing | launch preflight and acceptance | launch-preflight unsigned-change checks; owner-freeze.fsx | AGENTS.md | 0 | n/a (names the existing check) |
| CV-17 Forma first: current Forma, no containment workarounds | MANDATORY | new | bootstrap / every merge (seconds) | conventions.fsx check CK-04 (Forma version) + design-contract check + layout-tests (existing) | kickoff/rehearsal-010/design-contract.json; requirements-compiled/v4.1.5/layout-tests | 1 | no (new check) |
| CV-18 Editable fields bind on input, through the field map | PREFERRED | existing | contract freeze | proposals/r10-contract-first-ui/tests/ManifestTests.fs | proposals/r10-contract-first-ui | 0.5 | n/a (names the existing check) |
| CV-19 Test-only code never ships | MANDATORY | review | candidate | Reviewer confirms the candidate bundle contains no test hook symbol and traces are redacted. | - | 0.5 | no (review obligation) |
| CV-20 Schedules read the frozen ownership; gates are acyclic | PREFERRED | existing | pre-launch | tools/r10/schedule-model-tests.fsx; ODR-R10-33 deferral | tools/r10/schedule-model.fsx | 0 | n/a (names the existing check) |
| CV-21 Everything mandated is in the dependency lock | PREFERRED | review | package release | Package reviewer lists every package named by a requirement and its lock entry. | - | 0 | no (review obligation) |
| CV-22 Standard per-feature F# file split (Contract/Domain/State/View/Tests) | DISCRETIONARY | none | n/a | - | - | 3 | n/a |
| CV-23 Stable requirement and field IDs, decoupled from F# names | PREFERRED | existing | contract freeze | proposals/r10-contract-first-ui/contracts/Ids.fs + ManifestTests | proposals/r10-contract-first-ui | 0.5 | n/a (names the existing check) |

## New checks implemented in this change (`tools/conventions/conventions.fsx`)

| Rule | Convention | Detects | Cost | False-positive risk | Negative control (in `conventions.fsx test`) |
|---|---|---|---|---|---|
| CK-01 | CV-10 | `TreatWarningsAsErrors` false or unset; `NoWarn` of FS0025, FS0026 or FS0049 | under 1 s | low: these are the warnings for incomplete matches and drift | `TreatWarningsAsErrors` false; `NoWarn` FS0025; `NoWarn` 49; no root setting |
| CK-02 | CV-05 | the same `localhost:PORT`/`127.0.0.1:PORT` literal in more than one file | under 1 s | medium: a documented example in source code counts. Markdown is excluded. | port in two spec files |
| CK-03 | CV-07 | a workflow `run` step with no declared shell, no `defaults.run.shell` and no inline `set -o pipefail` | under 1 s | low: line-based reading of YAML. Anchors are not resolved, and a step using one is reported, which is the safe direction. | step without a shell; a step that sets `-euo pipefail` itself passes |
| CK-04 | CV-17 | `@echelon-foundry/design-system` below `--forma-min` (default 0.13.0) in any `package.json` | under 1 s | low | Forma 0.12.0 |

`conventions.fsx validate` checks the catalog itself:
- every id is unique and well formed;
- every evidence reference exists, and a finding's measured minutes sit in only one failure mode (no double counting);
- a duplicate finding carries no cost;
- scenario values are ordered (conservative ≤ central ≤ optimistic, and the reverse for overhead and overlap);
- every MANDATORY convention has an enforcement method and required tests, and a review-only method has a precise review obligation;
- no convention is marked ADOPTED by the catalog;
- the generated blocks in TOP-10-TIME-SAVINGS.md, IMPACT-FORECAST.md and FINAL-REPORT.md match the forecast.

## Rules that cannot be checked automatically

The following are review obligations. They are not presented as enforced:
- **CV-04:** Reviewer confirms the first CI run executed the boot smoke and that no view function spans more than one screen region.
- **CV-09:** Reviewer confirms each durability obligation cites a real-provider run, not a double.
- **CV-10:** Reviewer confirms no wildcard/default branch on command or event unions in decision code.
- **CV-12:** Integration confirms each merged pull request names the contract version it was built against.
- **CV-13:** Reviewer samples one control per slice.
- **CV-14:** Package reviewer lists each owner decision naming a requirement and its amendment or waiver.
- **CV-19:** Reviewer confirms the candidate bundle contains no test hook symbol and traces are redacted.
- **CV-21:** Package reviewer lists every package named by a requirement and its lock entry.

Each obligation names who checks what and at which stage, so a reviewer can record PASS or FAIL against it in the run's review record.
