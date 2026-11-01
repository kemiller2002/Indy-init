# Amendment 4.1.1: execution from T0

v4.1.1 is the successor amendment of v4.1. v4.1 is approved (ODR-V41-01, APR-V41-OWNER-4) and is not edited; v4.1.1 names v4.1's approved manifest digest (X20).

The record of the changes is [analysis/amendment.json](analysis/amendment.json), and the compiler checks it: X13 covers the phase move, X17 to X20 cover the rest. The functional scope is unchanged. v4.1.1 uses v4's verification plan byte for byte (X10, [preservation-report.json](preservation-report.json)). Its state machines, contracts and layout tests are v4.1's: X17 and CI compare them byte for byte.

Two owner decisions are pending:

- **ODR-V411-02:** how the organizer-approval obligations are handled (section C).
- **ODR-V411-01:** the approval of the package itself.

## A. The bootstrap at T0

T10 is now the first timed task, owned by integration. Every other timed task follows it: T11, T12, T14 and T18 depend on it directly (X19).

The order of the two generators comes from a measured conflict:

- If the v4.1 generator runs first, `conditor init` refuses, because `Directory.Build.props` and `package.json` already exist.
- If the generator's output is copied over Conditor's repository, Conditor's `App.slnx` stops building, in 3 of 3 runs. Two things cause it: unconditional central package management (NU1008), and the overwritten `Directory.Build.props` (NETSDK1013).

v4.1.1 therefore runs `conditor init` first. The generator then layers onto Conditor's repository with `--bootstrap .`:

- `package.json` takes the owner's npm locks and keeps Conditor's other fields. A locked package Conditor lists under `dependencies` takes the lock there.
- `.gitignore` gains the generator's lines.
- `Directory.Build.props` is kept, because Conditor's sets every property the generator sets.
- Central package management applies only to `Causa.*` projects.
- Any other existing file with different content is refused, and then nothing is written.

CI layers onto a recorded Conditor 0.10.18 scaffold and builds both solutions; the offline suite does the same.

[analysis/bootstrap-measurement.json](analysis/bootstrap-measurement.json) records three fresh empty repositories. The tools were the release binaries the Registry set echelon-current pins for linux-x64, each checked against its SHA-256: Conditor 0.10.18, Praxis 3.24.0, Ordo 1.19.0 and percepta-repo 0.1.0. [tools/measure-t0-bootstrap.fsx](../../tools/measure-t0-bootstrap.fsx) reproduces the measurement.

| Run | Conditor cache | git init | conditor init | generator | build Causa.slnx | build App.slnx | ci-policy | verify | commit | total |
|---|---|---|---|---|---|---|---|---|---|---|
| R1 | cold | 0.0 s | 46.8 s | 0.8 s | 21.0 s | 6.7 s | 0.2 s | 1.3 s | 0.1 s | 77.0 s |
| R2 | warm | 0.0 s | 5.2 s | 1.1 s | 17.8 s | 6.1 s | 0.2 s | 1.2 s | 0.1 s | 31.8 s |
| R3 | cold | 0.0 s | 45.8 s | 0.9 s | 20.6 s | 7.6 s | 0.1 s | 1.4 s | 0.1 s | 76.4 s |

A cross-check with the tools built from their release tags took 71.8 s, 32.3 s and 75.9 s. The runs were on one 4-CPU container with a warm NuGet cache. They used an earlier shell runner, which is not committed because this repository is F#-only. The committed script, written afterwards with the same steps, reproduced them at 83.0 s with a cold Conditor cache, every step passing.

**T10 is 29 minutes, labelled `measured+estimated`:**

| Part | Minutes | Basis |
|---|---|---|
| The mechanical sequence | 2 | Measured: the slowest run, rounded up |
| `conditor repo create` and the first push | 2 | Estimated |
| Integration's remaining shared files | 15 | Estimated |
| The first green CI on GitHub Actions | 10 | Estimated |

The three estimated parts need a real GitHub repository and were not measured. X13 and X19 require the split to add up and to match the measurement record.

## B. The F# contracts are specifications

[contract-classification.json](contract-classification.json) classifies every contract file. Seven are types and signatures only. `03-IF41-WIRE.fs` and `05-IF41-SAVE.fs` carry reviewed boundary code: seven members, each with its rationale, its requirements and the SHA-256 of its text.

| Contract | Members | Requirements |
|---|---|---|
| Credential | `ToString`, `create`, `authorization` | PF-015, INV-21 |
| Confirmation | three accessors and `ofReadBack` | PERSIST-023 |

That code fixes two choices before kickoff: the `Bearer` scheme, and exact equality of what was written with what is read back. Both are requirements. The save workflow that calls `ofReadBack` remains persistence's timed work. `99-Usage.fs` is a compile control and is never materialized into the application (offline suite).

X17 has three layers:

1. **Baseline equality.** Every contract must equal its file in the reviewed, approved v4.1 package byte for byte. This is the guarantee. The baseline must be the package the amendment amends, with its approval (X20 pins both).
2. **Pinned members.** Each allowed member's text must match its pinned digest.
3. **Line scan.** No other code may appear. The scan is a heuristic over lines, not the F# syntax tree. It first blanks strings, character literals and comments in an approximation of the F# lexer, covering strings (including strings inside comments), character literals, verbatim and triple-quoted strings, and nested comments. It then requires every line to begin as a declaration and to carry no code keyword. It catches every form the three review rounds used, 20 in all, and the offline suite tests each one. These include active patterns, `static let`, `do`, bare expressions, record expressions, `use`, one-line types with members, backtick names, chained bindings, and code hidden behind a string, character or verbatim literal.

CI proves that the contracts typecheck and that the negative controls fail to compile. X05 proves that each state machine equals one interface's unions. The bootstrap writes each contract verbatim into its producer's file, and two bootstraps are identical.

## C. No organizer-permission gate

No task waits on organizer permission (OD-INDY-18): T10 no longer prefers to follow T06, and AGENT-RULES no longer makes the bootstrap rely on ODR-V41-06. The published rules remain external constraints. ODR-V41-06 and APR-V41-OWNER-1 stay as written.

X19 scans for organizer gates: a mention of an organizer with a word of approval, confirmation, permission, waiting or asking. It reads these sources:

- v4's preflight checks, tasks, requirement obligations and risk register;
- the DAG waivers that rest on ODR-V41-06;
- the documents in `startingState.organizerDocuments`: the internal rule documents, the data contract, the domain test matrix, the product requirements, and v4.1.1's own AGENT-RULES, README and FREEZE.

The scan is a heuristic over those sources, not over every file in the repository. Each match must have a recorded disposition (`startingState.organizerReferences`, 29 entries). No obligation is claimed met. These conflict with the owner's instruction, and an agent may not set them aside:

- ADRP-016-O2 ("subject to organizer approval", a must obligation) and its readiness-checklist form;
- STORAGE-008-O1 and the data contract's §10.8;
- R0.1-O1 ("blank application repository or the organizer-approved equivalent") and the kickoff packet's "empty/organizer-compliant" checks;
- the DOMAIN-TEST-MATRIX condition on pre-written executable tests;
- PRE-ORG-01/T06 and RISK-12;
- OD-INDY-12's organizer condition;
- ODR-V41-06's set-aside of the interface-freeze clause, and the four contract-first waivers that rest on it.

PRE-REQ-04 and PRE-TOOL-03 accept an owner record. The plan provides one, and their own checks decide.

The historical records (ODR-V41-06, APR-V41-OWNER-1 and the quoted published rules) are checked by their content, and the external constraints must be quoted verbatim.

**ODR-V411-02** asks the owner to choose:

- **A:** proceed without seeking organizer approval. The approval obligations are reported as not met, with the reason; the others are reported by their own evidence.
- **B:** keep the gate.
- **C:** ask the organizers but do not wait.

[STARTING-STATE.md](STARTING-STATE.md) states the boundary and the residual risk, including the Conditor scaffold and the requirements import. It does not claim that the contracts are exempt under every reading of the rules.

## D. Token economics from Praxis

The report is produced from Praxis 3.24.0's own telemetry records ([docs/TOKEN-ECONOMICS.md](../../docs/TOKEN-ECONOMICS.md)), and the offline suite verifies it with a recorded fixture. The coordinator owns the ingestion and the report (AGENT-RULES duty 6): Rehearsals 5 and 6 recorded no tokens because no role was assigned to it. Usage from a session whose span crosses the window start or end goes into an `unsplit` bucket, never into timed or pre-event.

## E. One owner for the shared web entrypoints

`web/index.html` and `web/causa.css` now belong to integration; v4.1 gave them to ui. UI roles own their view modules. They ask for entrypoint changes through an integration-owned change request: a Praxis work item titled `CR-…`, which integration applies in its own pull request.

Applying the requests costs integration time. T17, integration's first checkpoint, which follows T16, carries 5 more minutes (20 instead of 15) for T16's requests. Later requests fall in RES-INTEGRATION.

What enforces the ownership:

- X18 and X02 reject any other owner or contributor in the manifest.
- AGENT-RULES may name the files only in four reviewed lines, each pinned by SHA-256: the integration row, the ui row with its fixed change-request sentence, duty 4, and the Forma rule. Any other line that names either file, by path or by file name, fails X18. Prose that refers to the files without naming them is not checked.
- Nothing in the generated repository enforces it (there is no CODEOWNERS file). The agent rules do.

## Schedule

The schedule is recomputed from the updated DAG by the same scheduler ([schedule.json](schedule.json), [execution-dag.json](execution-dag.json)). The reserves are unchanged: RES-INTEGRATION 20 minutes and RES-FINAL 45 minutes. No estimate was shortened. Every estimate except T10 and T17 equals v4.1's.

| Topology | v4.1 total | v4.1.1 total | Fits 420 | v4.1.1 makespan |
|---|---|---|---|---|
| C, two UI agents | 385 | **419** | yes, with **1 minute** of margin | 354 |
| A, one UI agent | 455 | 489 | no | 424 |
| B, single lead | 745 | 779 | no | 714 |

The lane-free critical path is 334 minutes: T10, T14, T13, T17, T22, T28, T31, T32, T33, T34. In topology C the binding chain is T10 → T14 → T12 → T13 → T17 → T23 → T26 → T29 → T28 → T31 → T32 → T33 → T34. The bootstrap adds its full 29 minutes to the front of every chain.

Sensitivity, topology C (a rule naming a task replaces the owner rule for it):

| Scenario | C total | Margin |
|---|---|---|
| As planned | 419 | +1 |
| Change requests take twice as long (T17 25) | 424 | −4 |
| Bootstrap ×1.5 (T10 44) | 434 | −14 |
| Bootstrap ×2 (T10 58) | 448 | −28 |
| Every integration task ×1.25 | 454 | −34 |
| Bootstrap ×2 and every other integration task ×1.25 | 475 | −55 |
| Every integration task ×1.5 | 487 | −67 |

**Topology C fits, but only just.** Any overrun on the binding chain of more than a minute takes it past 420. The 20-minute integration reserve absorbs coordination time that is not in the task estimates, and is the first buffer an overrun consumes.

Two levers would reduce actual work without removing a requirement. Neither is applied, because each changes the plan's structure:

1. **Start T12, T14 and T18 at the skeleton commit.** These tasks need only the generated skeleton, which the measurement puts at about 2 minutes. They need not wait for T10's first green CI or integration's remaining shared files. This would take up to 25 minutes off every chain, but it changes v4's T10 completion criterion ("first green CI"), so it needs an owner decision.
2. **Apply T16's change requests in integration's idle time.** Integration is idle while T13 runs (T17 waits for T13), so the 5 minutes need not be on the chain. The task-level scheduler cannot express that, so the 5 minutes stay on T17.

## What remains unverified

- **The first GitHub Actions run and the remaining T10 work.** `conditor repo create` and integration's shared-file authoring are not measured either. Together they are 27 of T10's 29 minutes.
- **Every other estimate.** None has been measured in an event build of v4.1 or v4.1.1.
- **Change-request overhead in a real run.** It is estimated at 5 minutes. ui cannot see its loading-shell and visual-identity work in the real entrypoints until integration applies the requests in T17. That round-trip is not in the estimate; the 20-minute integration reserve partly covers it.
- **Token economics on a real timed run.** It is verified on a fixture only.
- **The organizers' reading of the starting state.** The residual risk is in STARTING-STATE.md, and how the organizer-approval obligations are reported is ODR-V411-02.
