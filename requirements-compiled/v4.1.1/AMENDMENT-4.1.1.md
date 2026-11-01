# Amendment 4.1.1: execution from T0

v4.1.1 is the successor amendment of v4.1. v4.1 is approved (ODR-V41-01, APR-V41-OWNER-4) and is not edited; v4.1.1 names v4.1's approved manifest digest (X20). The record of the changes is [analysis/amendment.json](analysis/amendment.json), and the compiler checks it: X13 covers the phase move, X17 to X20 cover the rest. The functional scope is unchanged. v4.1.1 uses v4's verification plan byte for byte (X10, [preservation-report.json](preservation-report.json)), and its state machines, contracts and layout tests are v4.1's; CI diffs them.

## A. The bootstrap at T0

T10 is now the first timed task, owned by integration. Every other timed task follows it: T11, T12, T14 and T18 depend on it directly, and X19 checks this.

The order is set by a measured conflict. When the v4.1 generator runs first, `conditor init` refuses: `Directory.Build.props` and `package.json` already exist. When the generator's output is copied over Conditor's repository, Conditor's `App.slnx` stops building in 3 of 3 runs. The two causes are the unconditional central package management (NU1008) and the overwritten `Directory.Build.props` (NETSDK1013).

v4.1.1 therefore runs `conditor init` first. The generator then layers onto Conditor's repository with `--bootstrap .`:

- `package.json` takes the owner's npm locks and keeps Conditor's other fields.
- `.gitignore` gains the generator's lines.
- `Directory.Build.props` is kept, because Conditor's already sets every property the generator sets.
- Central package management applies only to `Causa.*` projects.
- Any other existing file with different content is refused, and nothing is written.

[analysis/bootstrap-measurement.json](analysis/bootstrap-measurement.json) records three fresh empty repositories. The tools are the release binaries the Registry set echelon-current pins for linux-x64: Conditor 0.10.18, Praxis 3.24.0, Ordo 1.19.0 and percepta-repo 0.1.0, each checked against its SHA-256.

| Run | Conditor cache | git init | conditor init | generator | build Causa.slnx | build App.slnx | ci-policy | verify | commit | total |
|---|---|---|---|---|---|---|---|---|---|---|
| R1 | cold | 0.0 s | 46.8 s | 0.8 s | 21.0 s | 6.7 s | 0.2 s | 1.3 s | 0.1 s | 77.0 s |
| R2 | warm | 0.0 s | 5.2 s | 1.1 s | 17.8 s | 6.1 s | 0.2 s | 1.2 s | 0.1 s | 31.8 s |
| R3 | cold | 0.0 s | 45.8 s | 0.9 s | 20.6 s | 7.6 s | 0.1 s | 1.4 s | 0.1 s | 76.4 s |

A cross-check, with tools built from their release tags instead, took 71.8 s, 32.3 s and 75.9 s.

Every step passed in every run. Both solutions built, `conditor ci-policy check` and `conditor verify` passed, and Praxis, Ordo and percepta-repo initialized. Conditor imported 68 requirement documents, 42 from Indy-init@2eee2d5 and 26 from Percepta.

T10 is estimated at 29 minutes:

- 2 minutes measured (the slowest run, rounded up);
- 2 minutes estimated for `conditor repo create` and the first push;
- 15 minutes estimated for integration's remaining shared files;
- 10 minutes estimated for the first green CI on GitHub Actions.

The last three need a real GitHub repository and were not measured. v4.1 had estimated 25 minutes, outside the window.

## B. The F# contracts are specifications

[contract-classification.json](contract-classification.json) classifies every contract file. Seven are types and signatures only. `03-IF41-WIRE.fs` and `05-IF41-SAVE.fs` carry reviewed boundary code, seven members in all, each with its rationale and requirements:

- the credential's `ToString`, `create` and `authorization` (PF-015, INV-21);
- the confirmation's three accessors and `ofReadBack` (PERSIST-023).

This code is the requirement expressed as a type. The negative controls prove that nothing else can read a credential or forge a confirmation. `ofReadBack` is one equality test; the save workflow that calls it remains persistence's timed work.

`99-Usage.fs` is a compile control and is never materialized into the application (offline suite). X17 rejects any other implementation member, and a stale allowlist entry.

What CI and the offline suite prove:

- The contracts typecheck, and the negative controls fail to compile (CI).
- Each state machine equals one interface's unions (X05).
- The bootstrap writes each contract verbatim into its producer's file, and two bootstraps are identical.

## C. No organizer-permission gate

No task waits on organizer permission (OD-INDY-18). T10 no longer prefers to follow T06, and AGENT-RULES no longer says the bootstrap "relies on ODR-V41-06"; X19 checks both. The published rules remain external constraints, and ODR-V41-06 and APR-V41-OWNER-1 stay as written. [STARTING-STATE.md](STARTING-STATE.md) states the boundary and the residual interpretation risk. It does not claim that the contracts are exempt under every reading of the rules.

## D. Token economics from Praxis

The report is produced from Praxis 3.24.0's own telemetry records. [docs/TOKEN-ECONOMICS.md](../../docs/TOKEN-ECONOMICS.md) specifies it, and the offline suite verifies it with a recorded fixture.

## E. One owner for the shared web entrypoints

`web/index.html` and `web/causa.css` now belong to integration; v4.1 gave them to ui. UI roles own their view modules and ask for entrypoint changes through an integration-owned change request: a Praxis work item titled `CR-…`, which integration applies in its own pull request. X18 and X02 reject any other owner or contributor. X18 also reads AGENT-RULES' roles table, and its negative tests reject a manifest or prose that gives the files to ui.

## Schedule

The schedule is recomputed from the updated DAG by the same scheduler ([schedule.json](schedule.json), [execution-dag.json](execution-dag.json)). The reserves are unchanged: RES-INTEGRATION 20 minutes and RES-FINAL 45 minutes. No estimate was shortened.

| Topology | v4.1 total | v4.1.1 total | Fits 420 | v4.1.1 makespan |
|---|---|---|---|---|
| C, two UI agents | 385 | **414** | yes, 6 minutes of margin | 349 |
| A, one UI agent | 455 | 484 | no | 419 |
| B, single lead | 745 | 774 | no | 709 |

The lane-free critical path is 329 minutes: T10, T14, T13, T17, T22, T28, T31, T32, T33, T34. In topology C the binding chain runs T10 → T14 → T12 → T13 → T17 and then along the UI lane to the merge freeze. The bootstrap adds its full 29 minutes to every chain, because nothing can start before it.

Sensitivity, topology C (minutes over 420 are negative margin):

| Scenario | C total | Margin |
|---|---|---|
| As planned | 414 | +6 |
| Bootstrap ×1.5 (T10 = 44) | 429 | −9 |
| Bootstrap ×2 (T10 = 58) | 443 | −23 |
| Every integration task ×1.25 | 448 | −28 |
| Every integration task ×1.5 | 480 | −60 |
| Bootstrap ×2 and integration ×1.25 | 484 | −64 |

Topology C fits, but only just. It breaks if the bootstrap overruns by more than 6 minutes (about 20%), or if every integration task overruns by about 5% (a 25% overrun adds 34 minutes).

**Proposed, not applied (it needs an owner decision):** most of T10's estimate is the unmeasured part: shared files and the first green CI. T12, T14 and T18 need only the generated skeleton commit, which the measurement puts at about 2 minutes. If integration's remaining shared files and the first green CI ran in parallel with T12, T14 and T18, each chain would gain up to 25 minutes. That would change v4's T10 completion criterion ("first green CI"), so this amendment does not do it.

## What remains unverified

- **The first GitHub Actions run** on the competition repository, `conditor repo create`, and integration's shared-file authoring. 27 of T10's 29 minutes are estimated.
- **Every estimate except T10's mechanical part.** None has been measured in an event build of v4.1 or v4.1.1.
- **Token economics on a real timed run.** It is verified on a fixture only.
- **The organizers' reading of the starting state** (STARTING-STATE.md, residual risk).
