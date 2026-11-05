# Rehearsal 010: bootstrap critical path

**Measured values come from BOOTSTRAP-MEASUREMENTS.json.** The ordered steps are STARTUP-INSTRUCTIONS.md §2–3, as merged in #118. INITIALIZATION-OPTIMIZATION.md has the earlier detailed step list.

## Classification of each step

| Step | Before T0? | Sequential after | Concurrent with | Measured |
|---|---|---|---|---|
| Toolchain present: SDK 10.0.402, Conditor 0.10.20, the echelon-current set (Praxis, Dokimos, Ordo, Percepta), Playwright | **verify before T0** (`launch-preflight` LP-xx, `conditor acceptance preflight`) | – | – | workstation install in CI 19–23 s; Playwright + browsers 65 s |
| NuGet packages of the pinned versions | **warm cache before T0**, checksum-verified (ODR-R10-24) | – | – | cold restore 13.6 s, warm 1.4 s, 64 MB |
| Competition repository exists, empty; App installed; secrets set | **before T0**, by the owner | – | – | – (the live run failed because the App was not installed on `causa-acceptance-r10`) |
| Record T0 | T0 | – | – | – |
| `git init`, `conditor init --preset indy-init` | **after T0** (rule: nothing written before T0) | T0 | – | R9: within the 130 s from T0 to the bootstrap commit; one failure from a proxy GH_TOKEN, fixed by an anonymous re-run |
| `requirements-compiler v41 --bootstrap` | after T0 | conditor init | – | 2.1 s |
| `r9.fsx govern`, `check-governance` | after T0 | bootstrap | build | inside R9's 130 s |
| `npm ci`, suite preflight (STARTUP §2, #118) | after T0 | bootstrap | build | not measured separately |
| `dotnet build Causa.slnx` | after T0 | bootstrap | govern | 11.4 s warm, 16.2–18.6 s cold |
| Commit and push the bootstrap; `conditor repo create` | after T0 | all of the above | – | – |
| First CI run green (end of T10) | after T0 | push | Praxis work-item creation | queue 3.3–8.4 min (measured, Indy-init), run length unmeasured |
| Praxis: create every work item; scope checkpoint; three authorization inputs; initialization checkpoint (STARTUP §3) | after T0 | bootstrap commit | first CI run | not measured |

## The critical path

```
T0 → conditor init → bootstrap → build (‖ govern, npm ci, suite preflight) → commit/push → [first CI run  ‖  Praxis items + checkpoints] → first slice
```

| | Planned | Measured or bounded |
|---|---|---|
| Set-up (`--setup`) | 45 (approved) / 25 (ODR-R10-23) | machine time 3–6 min; R9 reached its bootstrap commit at **T+2.2** |
| T10 (bootstrap to first green CI) | 29 (DAG) | commit at T+2.2 in R9, plus a CI queue of 3–9 min, plus one run |

**Recommendation (planning values, not a rule change):** set-up 10 min, keeping T10's 29 as estimated. E7 models it: M0 T+101/110/120 and M1 T+218/241/268 at 0/5/10-min CI, against 25 min of set-up in E2. That is 15 minutes earlier on everything.

The 10 is an assumption built from two measured parts:
- **measured:** 2.2 min in R9 to the bootstrap commit;
- **assumed, not measured:** Praxis item creation and the checkpoints.

## Minimal startup sequence

Conditor first; Praxis governance preserved:
1. **Before T0** (verify, don't build):
   - launch preflight LP-00..10;
   - warm NuGet and Playwright caches of the locked versions (ODR-R10-24);
   - App installed on the acceptance and catalog repositories (ODR-R10-28);
   - `GH_TOKEN` and `GITHUB_TOKEN` unset in the builder's shell for public fetches (the R9 failure).
2. **T0:** `conditor init` → `v41 --bootstrap` → in parallel, `govern` and `check-governance`, `npm ci` with the suite preflight, and `dotnet build`.
3. Commit and push the bootstrap. Then, **in parallel:** wait for the first CI run, and create the Praxis items and checkpoints (records only, through `setup/*` PRs, STARTUP §3).
4. First slice, after the initialization checkpoint.

Nothing here moves implementation before T0. Pre-T0 work is verification and caches of pinned, checksum-verified artifacts only.
