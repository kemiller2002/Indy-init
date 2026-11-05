# Rehearsal 010: bootstrap preflight (what to verify before T0)

Each item is a **check**, not a pre-built artifact. Nothing of the application is written before T0 (OD-INDY-22; STARTING-STATE.md).

| # | Check | Command or evidence | Status today |
|---|---|---|---|
| P1 | Launch preflight passes | `dotnet fsi tools/r10/launch-preflight.fsx` (LP-00..10) | LP-09 needs a new Preflight-tooling run on main after #118 (triggered by the push) |
| P2 | Pinned SDK present | `dotnet --version` = 10.0.402 (global.json, rollForward disabled) | verified in this session |
| P3 | Conditor and the echelon-current set | `conditor acceptance preflight --target SRC` → READY (STARTUP §3) | verified in R9; CI workstation step passes |
| P4 | NuGet cache holds the locked packages | `dotnet restore` against an offline source succeeds; compare package hashes with the lock | **not verified**: no bundle exists (ODR-R10-24) |
| P5 | Playwright and browsers of the locked version | `npx playwright --version`; browsers installed | CI installs in 65 s |
| P6 | Repository access | the App installed on `causa-acceptance-r10` and readable on `causa-data`; secrets present | **not done**: owner action (ODR-R10-28) |
| P7 | Public fetches will not send a placeholder token | `GH_TOKEN` and `GITHUB_TOKEN` unset in the bootstrap shell | R9 failure mode; STARTUP to note it |
| P8 | Data repository reachable | `curl -sI https://raw.githubusercontent.com/kemiller2002/causa-data/main/catalog/active.json` → 200 | verified (MINIMUM-DATA-BROWSER.md) |

**Repeated unnecessarily today.** Every CI job installs Conditor and the workstation (19–23 s) and Playwright (65 s) again. That is seconds per run, not a critical-path item. Caching them in CI is a future improvement, not a pre-competition change.
