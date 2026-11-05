# Cross-system compatibility matrix

Each result is one of:

- **PASS (executed):** the boundary was exercised in this session, and the log is cited.
- **PASS (inspected):** files were checked, but nothing ran.
- **NOT RUN:** the check did not run; the reason is given.
- **GAP:** a concrete incompatibility was found.

Where a log is not under `evidence/`, the numbers are quoted in [TEST-RESULTS.md](TEST-RESULTS.md).

**The stack under test:**

- echelon-current 1.37.0 (registry `5752402`)
- Conditor 0.10.20 (linux-x64, digest equal to the lock)
- .NET SDK 10.0.402
- Node 22.23.4
- Playwright 1.63.0 with Chromium 1243

## Conditor → Praxis

| Check | Result | Evidence |
|---|---|---|
| Work-item initialization | PASS (executed) | `conditor init --preset indy-init` actions 108–110: `praxis init`, `praxis verify --strict`, `praxis mission COND-MISSION-001 -> ready`. Exit 0, 110 actions, 82 s |
| Praxis verifies the generated repository | PASS (executed) | `praxis verify --strict`: "verification passed: @echelon-foundry/repository-operating-system@3.25.0". `praxis doctor`: no problems ([evidence/praxis-in-scaffold.log](evidence/praxis-in-scaffold.log)) |
| Requirements ingestion | PASS (executed) | Conditor materialized the Indy-init kickoff documents at `2eee2d5` and 26 Percepta contracts at `9e63077` |
| Ownership assignment | PASS (inspected) | v4.1.5 `agent-file-ownership.json` and X17 contract pins are checked by the v41 compiler (run in this session: only the 7 expected pending-approval lines fail) |
| Execution evidence | NOT RUN | Work items are created at kickoff (STARTUP-INSTRUCTIONS). Before then, `praxis status` reports "meaningful change has no work-item attribution" for every scaffold file (117 on the bare scaffold), which is the expected pre-checkpoint state |

## Praxis → Dokimos

| Check | Result | Evidence |
|---|---|---|
| Installed versions | PASS (executed) | praxis 3.25.0. dokimos `--version` reports ReleaseVersion 0.11.1, commit `fecbe0c`, schema 1.1.0, and the contract `dokimos.completion-verification/1` |
| Verification obligations, requirement-to-test mapping, gate outcomes | PASS (inspected) | echelon-current 1.37.0 was qualified by Conditor (conditor#131) and approved by the owner (echelon-registry#109). Dokimos 0.11.1 adds only the url-input requirement component (dokimos `1b253e4`). Indy-init's gates (`r9.fsx`, `launch-preflight.fsx`, the slice and completion gates) are unchanged |
| Acceptance evidence on a candidate | NOT RUN | Needs an application candidate, which exists only after kickoff |

## Ordo → execution

| Check | Result | Evidence |
|---|---|---|
| Installed version | PASS (executed) | `ordo --version`: 1.20.0. The binary is the SDE CLI (`@echelon-foundry/sde 1.20.0`: init, status, verify, boundary assess, verification schedule) |
| Pattern selection, dependency coordination | PASS (inspected) | Ordo 1.20.0 adds decision events behind an integrity-only gate (ordo#112). Praxis 3.25.0 exposes `ordo contract check` and `work complete-verified --ordo-contract`. No contradictory instruction was found between AGENT-RULES (v4.1.5) and STARTUP-INSTRUCTIONS |
| Ordo-driven parallel run | NOT RUN | EXP-INDY-002 (PR #140) is the only parallel run. Its CI was still queued at the time of this audit, and its code is not imported (directive §1.10) |

## Limen → Forma (F# WebAssembly)

| Check | Result | Evidence |
|---|---|---|
| F# WebAssembly startup on Limen 0.9.1 with Forma 0.13.0 | PASS (executed) | A scratch copy of `preview/causa` with Forma swapped from 0.10.0 to the 0.13.0 release asset. `build.fsx` succeeded (55 s). The browser shows the F# engine's "Not connected." projection; no console errors |
| Component rendering, state and events | PASS (executed) | `tests/read-path.mjs`, 25/25: Connect, Browse, Open, hash routing, Back, deep-link reload. `Engine.Tests`, 31/31 |
| Responsive layout | PASS (executed) | read-path: no horizontal overflow at 390 px. Forma LRP-001 suite on 0.13.0: containment at 320/375/768/1280 px with default and 200% text, 26/26 (Chromium). Causa-representative suite: phone, iPad portrait, iPad landscape and desktop, 10/10 |
| Package layout contract (v4.1.5 `forma-layout-contract.json`) | PASS (executed) | Forma 0.13.0, Chromium: 1,093 positive, 411 negative and 21 Forma-gap runs, 0 failed, with both the 0.12.0 inventory and the 0.13.0 inventory |
| Accessible labels, validation messages, axe | PASS (executed) | LRP-001 "a dialog with long validation errors…" passes on 0.13.0 (fails on 0.12.0). Axe WCAG A/AA passes at 375 px and at iPad portrait |
| Design tokens | PASS (executed) | Forma 0.13.0 `TokenValidator` built and ran on SDK 10.0.402: 0 errors over the preview CSS. Negative control: an unknown token, exit 1 |
| WebKit and Firefox | NOT RUN | Only Chromium is installed in this environment. CI installs Chromium and WebKit (`preflight-tooling.yml`) |
| Optimized WASM (`wasm-tools` workload) | NOT RUN | The preview published without the workload, i.e. the interpreter. The competition build's choice is unchanged by this audit |

## Causa → persistence

| Check | Result | Evidence |
|---|---|---|
| Historical-data loading | PASS (executed) | read-path against the real `kemiller2002/causa-data`. Apollo 1: 432/432 lines. Apollo 11: 431/431. Challenger: 452/452. Each replayed state equals the catalog digest |
| Serialization compatibility | PASS (executed) | The 1,315 events above decode with no drop. The v4.1.5 `CoreEvent` names equal `CoreEventV415` (X-checks run) |
| GitHub provider operations, read-after-write | PASS (executed, against the in-process double) | `Engine.Tests`, 31/31 (create, edit, save, read-back, conflict) |
| Real GitHub write and read-back | NOT RUN | Needs the owner's token (`tests/real-provider.mjs` is owner-run) |

## Conditor → generated CI

| Check | Result | Evidence |
|---|---|---|
| Commands, package versions, test paths | PASS (executed) | Scaffold `package.json`: design-system 0.13.0, limen 0.9.1, print-components 0.4.1 asset, Playwright 1.63.0. `App.slnx` and `Causa.slnx` build Release with warnings as errors: 0 warnings, 0 errors |
| Correct SDK | **GAP** (executed) | `build-and-test.yml:29` and `candidate-acceptance.yml:66` use `setup-dotnet dotnet-version: "10.0.x"`, while the bootstrapped `global.json` pins 10.0.402 with `rollForward: disable`. This is HF-029 (R8's first CI failed on the runner's 10.0.403; R9 needed two repair commits). Detected by stack-drift DP-01; DP-02 shows the correction makes the stack READY |
| Failure propagation | **GAP** (executed) | `conventions.fsx check` CK-03: 19 generated `run` steps without `pipefail` (HF-030) |
| Workflow triggers and concurrency | PASS (inspected) | Generated by Conditor's own CIP-001..008 rules (`conditor ci-policy check`) |

## EXP-002 contract-triggered parallel execution

| Capability | Supported by | Result |
|---|---|---|
| Release a validated, versioned F# contract | v4.1.5 contracts are pinned by digest in `analysis/amendment.json`, and X17 refuses an unapproved change | PASS (executed: X17 fails only on the 5 pending approvals) |
| Dispatch independent domain and UI work | v4.1.5 `agent-file-ownership.json` (roles domain, persistence, ui, ui-2, ui-3, integration) | PASS (inspected) |
| Preserve file ownership | X-checks; `r9.fsx check-sequencing`; Praxis `execution start --scope/--allow` | PASS (inspected) |
| Track contract versions | Ordo contract digest (`praxis ordo contract check --expected-digest`); amendment digests | PASS (inspected) |
| Integrate implementations when ready | slice gate (ODR-R10-33 defers assertions until their slices are integrated) | PASS (inspected) |
| Focused tests before full CI | `tools/r10/change-impact.fsx` (owner-acknowledged, feedback only) | PASS (inspected) |
| Independent browser verification | `verification/` journeys and the design contract run from the governed checkout | PASS (inspected) |
| Evidence tied to the candidate commit | Generated `candidate-acceptance.yml` writes `conditor.candidate-verification/1` with the candidate SHA | PASS (inspected) |

No upgraded system removes any of these capabilities. Nothing in the upgrade needs an engineering-system rewrite.
