# Workstream D: pre-competition work classified

Deferred items keep their requirements and their incomplete status; nothing here is marked done. "Blocking" means the first working application, correctness, security or required acceptance depends on it.

## Blocking
| Item | Why | State |
|---|---|---|
| PR #132: CF-07 slice-gate deferral (ODR-R10-33 A) | without it SL-T16 deadlocks, so M2–M4 cannot integrate | review round 3 FAIL (CF07-R3-1) fixed at `7ea3d4c`; round 4 running |
| Minimum v4.1.5: event union, derived workspace, listing, creation (ODR-R10-19/-20 inputs) | the app cannot represent historical events or list investigations without it | Agent 3 (D) |
| Acceptance lock with the live test repository (LP-08) | real-persistence acceptance | live preflight passed on the proposed lock (run 42077412742); adoption follows v4.1.5 |
| `causa-catalog-read` App and secrets (ODR-R10-28 A) | the catalog is read with a dedicated read-only credential | **owner action** ([OWNER-BATCH.md](OWNER-BATCH.md)) |
| Owner records the frozen-package rules need for v4.1.5 (if any) | RDP-003: agents cannot write them | Agent 3 to confirm; batched |

## Competition-window cost not yet measured (UI agent, PR #134 `8b51f27`)
- The v4.1.x compiler carries only interface text into implementing files. So at bootstrap only the IF41-VIEW types reach the competition repository.
- `Presentation.fs` (validation, navigation, presentation language and identifiers; about 474 non-blank lines) and the UiFixtures (about 600 lines) arrive as empty stubs. They are written inside the timed window, as OD-INDY-22 requires.
- Integration's first post-bootstrap UI task is therefore `Presentation.fs`. Each UI role writes a minimal fixture backend before its first fixture-backed screen.
- This cost is **not in the schedule model and not measured**. Treat it as a blocking item on M1's UI path.

## Parallelizable (necessary, off the app's critical path)
| Item | Owner |
|---|---|
| UI ownership map and build order (B) | Agent 2 |
| CI sharding of the adversarial cases (C) | lead, this PR |
| ODR-R10-22 speculative gate (S1–S7) | lead, after v4.1.5 |
| ODR-R10-24 toolchain bundle | lead, after v4.1.5 |
| CF07-R3-2 liveness note: a binding-clean catalog acceptance (fault-probe spec) early on main | competition plan: the first slice adds `.echelon/fault-probes.json` |

## Deferrable (recorded, incomplete)
| Item | Deferred to |
|---|---|
| FO-1..3, T19/T22 contract work in v4.1.5 | v4.1.6 **if the owner records OD-V415-01** (staging); until then they stay in v4.1.5 scope, incomplete |
| Further schedule-model refinements (V5–V7 variants, new sensitivities) | after the competition |
| Generalized workspace abstractions beyond Connect → Reload | after M4 |
| Optional UI contract extensions; nonessential Forma refinements | after M4 |
| Documentation beyond operational need | after the competition |
| New framework or tooling evaluations | not before Nov 7 |
| Re-reviews of unchanged decisions | not scheduled |
| A catalog regression base (would close the CF-07 residual "break a merged slice's INV obligations") | after the competition; release still judges every assertion |
