# Historical findings: recurring implementation failures, Rehearsals 002–010

Status: analysis, not normative. Machine-readable form: [FAILURE-DATA.json](FAILURE-DATA.json) (87 findings `HF-001`..`HF-087`). Every finding cites a file and heading, a commit, a PR or a workflow run. Minutes appear only where a cited source states them.

## Scope and limits of the evidence

| Source | Read | Limit |
|---|---|---|
| Hub (`kemiller2002/Indy-init`) docs: rehearsal results R3–R6, R5 audit, R6 preflight, R7 remediation, R9 content readiness, release-gate repair, R10 readiness and optimization, engineering-improvement investigations | yes | The checkout's git history starts on 2026-11-02, so hub commit counts cover only that range |
| `indy-init-rehearsal-008`, `-009`, `-010` and `forma` (read-only clones) | yes | — |
| `indy-init-rehearsal-002`, `-003`, `-004`, `-005`, `-006` | through the hub's documents and the R2 ledger fixture | Not cloned; the hub quotes their evidence |
| `indy-init-rehearsal-007` | no | Findings labelled R007 cite hub documents that quote its evidence (`requirements-compiled/v4/analysis/r7-lessons.json`, `docs/TOKEN-ECONOMICS.md`) |
| **Rehearsal 010** | — | **It has not been run.** `indy-init-rehearsal-010` holds one README commit (`616ee17`), and no T0 record exists. Findings labelled R010 are R9 failures diagnosed during R10 readiness, or defects in the readiness work itself. Every R10 duration in the hub is a model output, not an observation (HF-058). |
| EXP-INDY-002 | not touched | It is not referenced in this repository. Its results are an input to the Tier 2 conventions. |

## Counts

| Category | Findings |
|---|---:|
| governance | 14 |
| ci-tooling | 13 |
| testing-verification | 12 |
| ui-implementation | 11 |
| requirements-ambiguity | 7 |
| persistence-serialization | 7 |
| agent-coordination | 7 |
| contract-instability | 6 |
| architecture | 5 |
| fsharp-inconsistency | 5 |

Four findings repeat an event that another research pass also recorded: HF-059 duplicates HF-037, HF-060 duplicates HF-028, HF-061 duplicates HF-048 and HF-063 duplicates HF-044. Each duplicate is marked `duplicateOf` and carries no cost, so its cost is counted only once.

## Measured critical-path losses

Only six findings state a wall-clock loss inside a timed run. Together they account for **652.9 minutes** across seven rehearsals:

| Finding | Rehearsal | Loss | Kind | Source |
|---|---|---:|---|---|
| HF-048 | R9 | 305 | window left unused (stopped at 115 of 420 min) | `indy-init-rehearsal-009` evidence/FINAL-REPORT.md §1 |
| HF-007 | R4 | about 120 | delay: WASM runtime trap, 8–10 CI cycles | `docs/engineering-improvement/investigations/r4-baseline-and-compiler.md` §1.4 |
| HF-013 | R5 | 90 | delay: app could not start (WASM locals limit), `ec6407b` to `a0e7d4a` | `docs/REHEARSAL-005-RESULTS.md` §Defects |
| HF-011 | R4/R6 | about 90 | delay: no main CI result (R6 22:22–23:54) | `docs/REHEARSAL-006-RESULTS.md` §Process |
| HF-014 | R5 | 27.9 | delay: hard-coded port broke main CI | `docs/engineering-improvement/investigations/r5-baseline.md` §4(b) |
| HF-025 | R6 | 20 | delay: first candidate failed a stale copy expectation and was re-cut | `docs/REHEARSAL-006-RESULTS.md` §Key facts (T+307 minus T+287; the subtraction is the research pass's own) |

Other measured costs are not critical-path minutes and are kept apart:
- **Elapsed time outside timed runs:** the v4.1.4 successor took 19 h 17 min from candidate to tag, 10 h 44 min of it waiting for the owner (HF-071). The DA-R10-1 timebox used about 30 min (HF-077).
- **CI cycle times:** the preflight-tooling run takes 23.0 min (HF-075), and Forma layout assertions take 13.1 min (HF-086).
- **Counts:** 17 of 92 merges conflicted (R5), 26 of 49 merges conflicted on `.ros` (R8), and 347 of 613 obligations were unverified (R8). There were 1,297 runner-minutes of cancelled jobs in R6, which is compute, not wall clock.

For most recurring failures the time they cost was never recorded. **Unknown is the honest value for most of this catalog.**

## Recurring failure modes

Findings are grouped into 22 failure modes (`FM-01`..`FM-22` in [CONVENTION-CATALOG.json](CONVENTION-CATALOG.json)). The table lists only failure modes that recurred, or that cost measured time.

| Failure mode | Observed in (with `recurredIn`) | Root cause (where the evidence supports one) | Findings |
|---|---|---|---|
| FM-09 Save reported durable without verified read-back | R2, R4, R5, R6, R7, R8, post-R10 preview | Durability was defined by the client's state, not by reading the provider back. Doubles lacked real provider behaviour: cache headers, the 1 MB limit, compare pagination. | HF-006, HF-021, HF-026, HF-039, HF-040, HF-074 |
| FM-13 Tests that cannot fail, are never written, or over-claim | R3, R4, R5, R6, R8, R9 | Binding a tag was accepted as proof. Stubs skipped instead of failing. | HF-002, HF-009, HF-017, HF-027, HF-045, HF-046, HF-052 |
| FM-15 Evidence not bound to the final candidate | R2, R4, R5, R6, R7, R9 | Reviews were carried forward to later commits. Completion was claimed at first green CI. | HF-005, HF-016, HF-024, HF-025, HF-049, HF-051 |
| FM-16 Agent-written approvals | R3, R4, R5, R6, R8, post-R10 | Owner chat was transcribed into records. An authority record certified itself. | HF-003, HF-050, HF-077 |
| FM-03 Generated commands never executed before freeze | R5, R8, R9, R10 readiness | A multi-project `dotnet test` (MSB1008) shipped in frozen v4.1.2. The SDK pinned in `global.json` was not installed by the workflows. Deploy preconditions were unset at T0. | HF-028, HF-029, HF-033, HF-047, HF-018, HF-076 |
| FM-17 UI escapes containers; application CSS works around Forma | R5, R7, R9, preview | Forma 0.12.0 failed 48 of 78 containment checks, all fixed upstream in 0.13.0 (`forma` 0ffb077). Applications overrode Forma locally instead of upgrading or filing upstream. | HF-019, HF-056, HF-080..HF-085, HF-087 |
| FM-02 Ambiguous or changing build target | R3, R8, R9, R10 readiness | The catalog was named as the target while the P0 plan appeared only as "unchanged". Content priority was unstated. The scope digest changed three times. | HF-001, HF-037, HF-038, HF-062, HF-064, HF-070 |
| FM-08 Concurrent writers on shared files or state | R5, R8, post-R9 | File ownership was a commit-message convention (R5). `.ros` tool state was written by every lane (R8). Two sessions repaired the same defects (#101/#102). | HF-015, HF-042, HF-043, HF-044, HF-079 |
| FM-06 CI workflow shape | R4, R6, R8, R9, post-R10 | Per-commit concurrency groups; duplicate push+PR triggers; workflow sprawl | HF-011, HF-031, HF-032, HF-075, HF-086 |
| FM-07 CI shell steps hide failures | R6, R7, R8 | GitHub's implicit default shell is `bash -e` without `pipefail` | HF-030 |
| FM-10 State machine bypassed, faults swallowed, unknown input defaulted | R4, R5, R8 | Application code assigned machine state directly. `stepOr` swallowed faults. A command with an unknown body defaulted to `RecordObservation`. | HF-008, HF-022, HF-041 |
| FM-04 App fails to start in WASM | R4, R5 | Release build with debug info hit a Mono interpreter trap. A single view function compiled to 12,683 IL locals. | HF-007, HF-013 |
| FM-11 Frozen contract cannot represent real data | R8, post-R10 | Contracts were checked against fixtures written for them, never against causa-data. 487 of 1,315 events have no case; there is no workspace and no command body. | HF-068, HF-069, HF-034 |
| FM-12 Contract or package revision during or between builds | R4, R8, R9, post-R10 | In-place package changes. The successor compiler refuses new contract files and states. Owner round-trips dominate elapsed time. | HF-012, HF-035, HF-071, HF-072 |
| FM-14 Requirement clauses contradict decisions or each other | R5, R6, R8 | DEC-025/OD-INDY-10 were never applied to EXT-DATA-005/PF-015. INV-04 conflicts with SM-SAVE. | HF-023, HF-036 |
| FM-01 Run stopped or clock misread | R7, R9 | No stop rule; implementation and verification shared one status; time was projected from stale timestamps | HF-048, HF-057 |

Single-occurrence failure modes still have a convention, because they are cheap to prevent:
- FM-05: duplicated port literals.
- FM-18: input binding without `data-on="input"`. It occurred twice in R3, and the R4 regression test could not fail.
- FM-19: amber contrast at 1.95:1.
- FM-20: test hooks or tokens in artifacts.
- FM-21: the schedule model ignored file ownership, and the slice gate deadlocked.
- FM-22: FSharp.Core was dropped by central package management, and a mandated package was outside the dependency lock.

Four findings have no convention, and none is needed:
- **HF-054** (Python tooling): OD-INDY-14 already rules on it.
- **HF-058** (R10 not run): this is a fact about the evidence, not a failure.
- **HF-065** (readiness review rounds): this is a pre-competition process cost. Owner batching is outside the scope of this catalog.

## What recurred despite an earlier fix

- **Persistence durability.** It was "closed" repeatedly: R2's DC-003 took 11 closure reviews, then R5 F-1, R6-D-002 and D-003, R8 T33-F-01, and J3-05 in the post-R10 preview. Each fix covered one variant. No single rule defined "durable", and no rule required that doubles carry real provider behaviour. Both are now CV-09.
- **Shell pipefail.** It was fixed per step in R6, R7 and R8, never by default. The retrospective check (CK-03, below) still finds 23 non-pipefail run steps in R8's workflows and 39 in R9's.
- **Forma containment.** It was fixed upstream (0.10.0 radius, 0.11.0 typography, 0.13.0 containment). Even so, R9 and the preview app re-implemented or pinned older Forma. The frozen v4.1.4/v4.1.5 packages reference the 0.12.0 inventory, and the Conditor 0.10.18 scaffold fixture pins 0.12.0.

## Retrospective check results (new tooling, this change)

`tools/conventions/conventions.fsx check` was run read-only against past workspaces:

| Workspace | CK-01 warnings-as-errors | CK-02 port literal in >1 file | CK-03 run step without pipefail | CK-04 Forma < 0.13.0 |
|---|---:|---:|---:|---:|
| indy-init-rehearsal-008 | 0 | 0 | 23 | 1 (0.12.0) |
| indy-init-rehearsal-009 | 0 | 1 | 39 | 0 |
| Indy-init `preview/causa` | 0 | 1 (4173 in two test files) | 0 | 1 (0.10.0) |
| Conditor 0.10.18 scaffold fixture | 0 | 0 | 0 | 1 (0.12.0) |

These are detections, not prevented minutes. Whether these specific instances cost time is recorded only for the R5 port (27.9 min) and the R6–R8 shell findings (repair commits, minutes unknown).
