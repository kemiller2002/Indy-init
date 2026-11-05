# Rehearsal 010 optimization: baseline

**Authoritative state on 2026-11-05.** Indy-init `main` was at `76e50ce` when this baseline was recorded. After that, #116 and #117 were merged. Neither changes the scope or the schedule inputs.

## Repository and approval state

| Item | Value |
|---|---|
| Indy-init `main` | `76e50ce` (merge of #117) |
| Frozen packages | v4, v4.1, v4.1.1 and v4.1.2 at their tags. **v4.1.4 is frozen** at `indy-init-requirements-v4.1.4` → `208ec9f`. Its package manifest is `24c68f7a…` and its review `37487d19…`. Approvals: APR-V414-OWNER-1 (ODR-V414-01, -02 = A) and APR-V414-OWNER-2 (ODR-V414-03). |
| Catalog | CAT-CAUSA-INVESTIGATION successor, ratified (#114, ODR-R10-02 = A): 44 requirements, sha256 `7e5e8a40…` |
| Rehearsal 010 decisions | APR-R10-OWNER-1 records ODR-RG-03, ODR-RG-05 and ODR-R10-02…18, all A. ODR-R10-03 and -04 are **re-asked**, because the catalog contract now names the ratified catalog (#115). **ODR-R10-01 (scope) is not recorded.** |
| Approved scope (proposed for ODR-R10-01) | `docs/rehearsal-010-readiness/scope-manifest.json`: 205 requirements (200 mandatory), 890 obligations (844 mandatory), 37 work items. `scopeSha256` `59bfef5a62a3ab43ef19c08b5915ffaf3d408bd55d8add874c5f1071e7cd087c`. |
| Toolchain lock | **Not committed.** `kickoff/rehearsal-010/toolchain.lock.proposed.json` pins v4.1.4, the scope above, and echelon-current 1.37.0 (Dokimos 0.11.1, echelon-registry `5752402`). The owner commits `toolchain.lock.json` (ODR-RG-04). |
| Competition repository | `kemiller2002/indy-init-rehearsal-010`: one initial commit, no T0 (launch-preflight LP-10 PASS) |

## The schedule model, reproduced unchanged

```
dotnet fsi tools/r10/schedule.fsx -- --slices docs/rehearsal-010-readiness/slice-execution-manifest.json \
  --window 420 --setup 45 --ci 0,5,10,15,20,25 --merge 3 \
  --lanes "integration:integration,domain:domain,persistence:persistence,ui-1:ui,ui-2:ui,reviewer:reviewer" --out base-a.json
```

The output is byte-identical to the committed `evidence/schedule/topology-a.log`, and the JSON equals `topology-a.json` once timelines are removed:

```
slices 37; implementation minutes 1387 serial (27 DAG-estimated, 10 derived at 1.35 min/obligation, 0 default)
ci    0 min: all 37 slices integrated at T+519 (OVER the 420-minute window); 28 integrated by the deadline
ci    5 min: all 37 slices integrated at T+568 (OVER ...); 27 integrated by the deadline
ci   10 min: all 37 slices integrated at T+656 (OVER ...); 23 integrated by the deadline
ci   15 min: all 37 slices integrated at T+837 (OVER ...); 17 integrated by the deadline
ci   20 min: all 37 slices integrated at T+1011 (OVER ...); 14 integrated by the deadline
ci   25 min: all 37 slices integrated at T+1185 (OVER ...); 11 integrated by the deadline
```

| Input | sha256 |
|---|---|
| `slice-execution-manifest.json` | `cbbe2cd6…` |
| `scope-manifest.json` | `fa265d0b…` (file) / `59bfef5a…` (scopeSha256) |
| `tools/r10/schedule.fsx` | `72b96204…` |

### Corrections to the mission's reference baseline

| Mission figure | Current figure | Why |
|---|---|---|
| 1,350 implementation minutes | **1,387** | The ratified successor adds one work item and 27 obligations |
| SL-WI-INV-WORKSPACE: 27 requirements, 159 mandatory obligations, 215 min | **30 requirements, 170 mandatory obligations, ≈230 min** | The successor adds INV-042..044 to the workspace |
| Completion at 0, 5 and 10 min CI: 519, 568, 656 | equal | reproduced |

## The model the baseline depends on, and what it gets wrong

`schedule.fsx` lets **either UI lane take any UI slice**. The frozen v4.1.4 file ownership says otherwise. `agent-file-ownership.json` gives `ui` and `ui-2` disjoint files: "ui and ui-2 never own the same file".
- `ui-2` owns only Graph, Report, History and Administration, so only T23, T26, T28 and T29 are its slices.
- Every catalog screen, Dashboard, Shell, Investigation and the WDL specs belong to `ui`. They come to **≈564 minutes for one agent**.

Under the real ownership, the same plan finishes at **T+720 with zero CI** (`results/V1-ownership.json`), not T+519. The baseline is optimistic. `docs/rehearsal-010-optimization/inputs/slice-roles.v414.json` records the ownership-accurate role of each slice and its basis. Every comparison in this directory uses that as the honest baseline, **V1**.

## Reproducing everything

```
# the extended model: identical to schedule.fsx with its defaults (tested), plus roles, speculative queue, rework, scale
dotnet fsi tools/r10/schedule-model.fsx -- --slices <slice manifest> --setup 45 --merge 3 --ci 0,2,5,10,15,20 \
  [--roles inputs/slice-roles.v414.json --lanes integration:integration,domain:domain,persistence:persistence,ui:ui,ui-2:ui-2,reviewer:reviewer] \
  [--queue-depth 3] [--fail-every 4 --repair-fraction 0.25] [--scale 1.25] --out results/<name>.json
dotnet fsi tools/r10/schedule-model-tests.fsx -- --indy-init .       # 34 cases: equivalence, invariants, controls
```

The exact arguments of every variant are in SCHEDULE-COMPARISON.md.
