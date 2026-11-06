# Conditor audit (the competition entry point)

Everything below was executed with the **released** Conditor 0.10.20 binary in an isolated scratch directory (Linux x64). The binary was installed by `tools/r9/r9.fsx install-conditor --lock kickoff/rehearsal-010/toolchain.lock.proposed.json`.

## Identity and installation

| Item | Finding |
|---|---|
| Published version | 0.10.20, tag `v0.10.20`, commit `a4c53cee6f72966ed737362be5daa5d8ddea12d8`, published 2026-11-03T21:19:20Z. This is the latest release, and Conditor `main` has no unreleased version change |
| Installation | `r9.fsx install-conditor`: downloaded `conditor-linux-x64`, SHA-256 `75849672…ff326bd`, equal to the lock. PASS in 9 s |
| `conditor --version` | Not a command: it prints usage and exits 1. Identity therefore comes from the installer's digest check, not from the binary itself |
| Workstation | `r9.fsx workstation` → `conditor workstation apply` for echelon-current 1.37.0. It installed praxis 3.25.0, dokimos 0.11.1, percepta-semantic 0.4.0, ordo 1.20.0, percepta-repo 0.1.0 and strata 0.1.1 (optional) in 20 s. Each cached archive's digest equals the registry's |

## Bootstrap (`conditor init --preset indy-init`)

110 actions, exit 0, 82 s. It generates:

- `conditor.json` and the lock `.conditor/lock.json`. Components: praxis 3.25.0, ordo 1.20.0, percepta 0.1.0, aegis 1.0.0, limen 0.9.1, forma 0.13.0, folio 0.4.1.
- `package.json` with `@echelon-foundry/design-system` 0.13.0, `@echelon-foundry/limen` 0.9.1, Folio as the `v0.4.1` release-asset URL, `@playwright/test` 1.63.0 and TypeScript 5.9.3.
- `Directory.Build.props`: `net10.0`, `TreatWarningsAsErrors` true.
- `App.slnx`, `src/engine` (`EchelonFoundry.Aegis.Core` 1.0.0) and `tests/App.Engine.Tests`.
- Five workflows: `build-and-test`, `candidate-acceptance`, `deploy-pages`, `echelon-foundations` (praxis reusable workflow at `aa7c7ca`) and `praxis-validation`.
- Praxis is initialized, verified (`--strict`) and given mission COND-MISSION-001.
- No `global.json`. The v4.1.5 bootstrap writes one (10.0.402, `rollForward: disable`).

**Where the versions come from:** Echelon versions are not hardcoded in the scaffold. They come from the Registry resolved set, either the preset's components or `examples/indy-init.conditor.json`. The test `tests/Conditor.Tests/EchelonCurrent.fs` pins Forma 0.13.0, and `components/forma.component.json` qualifies 0.10.0–0.13.0. Some non-Echelon versions **are** hardcoded in `src/Conditor.Core/Scaffolding.fs`:

- Playwright 1.63.0
- Limen routing 0.9.0
- Microsoft.NET.Test.Sdk 17.11.1, xunit 2.9.2 and xunit.runner.visualstudio 2.8.2
- `dotnet-version: "10.0.x"` (lines 981, 1134, 1224, 1656, 1750)

## Layering v4.1.5 onto the real 0.10.20 scaffold (executed)

The package's CI layers v4.1.5 onto a **recorded Conditor 0.10.18 scaffold** (`tools/requirements-compiler/tests/fixtures/conditor-0.10.18-scaffold`). That fixture pins Forma 0.12.0, praxis 3.24.0 and ordo 1.19.0 (stack-drift DP-05).

This audit layered v4.1.5 onto the scaffold that 0.10.20 actually generates. The steps were the same ones CI runs:

1. `requirements-compiler v41 --package requirements-compiled/v4.1.5 --check --bootstrap <scaffold>`: 201 of 202 files written, with `.gitignore` and `package.json` merged. The only failures are the 7 pending-approval lines that CI's `pending415` accepts until FREEZE step 2.
2. `dotnet build Causa.slnx -c Release`: 0 warnings, 0 errors (26 s).
3. `dotnet build App.slnx -c Release`: 0 warnings, 0 errors (7 s).
4. `dotnet test Causa.slnx`: exit 0, but **no test cases exist yet**. The skeleton's test projects are written at kickoff, so this proves only that the runner starts.

## Findings

| ID | Finding | Source of the version | Correction | Owner of the fix |
|---|---|---|---|---|
| CA-01 | Generated workflows install `dotnet-version: "10.0.x"`. With the v4.1.5 `global.json` (10.0.402, `rollForward: disable`), a runner with a newer 10.0.x SDK fails to build (HF-029, R8 and R9). It is still present in 0.10.20 | `src/Conditor.Core/Scaffolding.fs` (5 templates) | Emit `global-json-file: global.json`, which needs the scaffold to own a pinned `global.json` (the SDK version's source is a decision), and add a ci-policy rule. For the competition: correct the two workflows in the first kickoff commit, as R9 did (`ececa70`, `5ef2908`) | Conditor (release 0.10.21), ODR-ESU-03 |
| CA-02 | 19 generated `run` steps have no `pipefail` (HF-030, CK-03) | `Scaffolding.fs` templates | `defaults: run: shell: bash` in each workflow | Conditor; already proposed by PR #141 (not adopted) |
| CA-03 | Indy-init's layering test uses the recorded 0.10.18 scaffold, not the 0.10.20 one the toolchain lock installs | `tools/requirements-compiler/tests/fixtures/conditor-0.10.18-scaffold` | Record a 0.10.20 fixture with the same rules (README), and point CI's `layered415` step and the offline suite at it | Indy-init successor change (REQ-ESU-05) |
| CA-04 | `conditor --version` does not exist | Conditor CLI | Report version, commit and RID | Conditor (nonblocking) |
| CA-05 | Conditor sends `GH_TOKEN`/`GITHUB_TOKEN` as a basic credential for **every** source fetch. A token that cannot read a public repository then fails a fetch that would succeed anonymously. Seen in this sandbox with a session-scoped token; unsetting the variables fixed it | `src/Conditor.Core/SourceCache.fs` `gitFetchEnvironment` | Retry anonymously on 401/403 for public sources, or document it in `docs/indy-day-of.md` | Conditor (nonblocking for the event: the owner's token can read the sources) |

## No Conditor release is required for versions

0.10.20 already installs and scaffolds echelon-current 1.37.0, with Forma 0.13.0. A Conditor release is needed only for CA-01, CA-02 and CA-05. CA-01 has a no-release workaround for the event (the first kickoff commit). No change was made in the Conditor repository: choosing the SDK pin's source is a decision for the owner (ODR-ESU-03), and the released artifact was not touched.
