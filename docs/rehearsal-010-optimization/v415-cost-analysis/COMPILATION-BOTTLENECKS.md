# Where v4.1.5's cost comes from

**Status:** analysis only. Nothing was compiled, regenerated or frozen. v4.1.4 is untouched.

## 1. Measured facts

Every duration below was measured in this session or read from git commit timestamps (UTC) on `origin/main`. Nothing is estimated in this section.

| Activity | Measured | How |
|---|---|---|
| Regenerate and validate a whole package (X01–X21), v4.1.4 | **about 1 s** | `requirements-compiler v41 --source . --package requirements-compiled/v4.1.4 --base requirements-compiled/v4 --check`, timed in this container: `v4.1.4 validation: PASS` |
| Contract typecheck (`contracts/ContractCheck.fsproj`) | **9 s** | `dotnet build`, on a copy of the contracts |
| Full offline suite (`tools/requirements-compiler/tests --offline`) | **147 s** | timed in this container; the same 4 pre-existing failures as on base `dbcad12` (3 caused by the shallow clone, 1 is W16) |
| CI on a pull request (all jobs) | **about 22 min** wall clock; longest jobs are `r10-adversarial` (about 20–22 min) and `fsharp-tooling` (about 18 min) | check runs of PR #120, head `7ef040d` |
| v4.1.1 successor, first package commit to review PASS | **1 h 57 min** (13:58 → 15:55, 2026-11-01), **5 review rounds**, about 20–30 min each | `git log -- requirements-compiled/v4.1.1` |
| v4.1.1, owner decisions to tag | **about 4 min** (17:00 decisions, 17:03 approval, 17:04 tag) | commits `b882a13`, `48778c7`, tag object date |
| v4.1.2 successor, inputs to tag | **13 h 06 min** (06:46 → 19:52, 2026-11-02), **3 review rounds**, two owner decision rounds in between | `git log`, tag object date |
| v4.1.4 successor, candidate to tag | **19 h 17 min** (05:42 2026-11-04 → 00:59 2026-11-05) | `git log`, tag object date |
| v4.1.4, candidate to owner decisions recorded | **10 h 44 min** (05:42 → 16:26) | commit `8b1d628` |
| v4.1.4, owner decisions to compile | **2 h 23 min** (16:26 → 18:49) | commit `317caa5` |
| v4.1.4, compile to review PASS | **3 h 41 min**, **4 review rounds** (20:00, 20:54, 21:32, 22:30) | commits `8565842`, `62967b8`, `b8bbac5`, `2035b58` |
| This session: historical decoder, 20 checks, 12 documents, PR | **about 1 h** from start to PR #120 (first commit 11:35, PR 11:53) | commit timestamps |
| This session: owner records OD-HC-01..07 and ODR-R10-19..33 | **about 10 min each** once the request file was on `main` (PRs #123 and #125) | merge commits `d8fe315`, `2a61419` |

**What these numbers show**

- The three earlier successors changed very little. v4.1.4 corrected six suite runners. v4.1.2 narrowed guards. v4.1.1 moved the bootstrap. Each still took **2 to 19 hours elapsed**.
- In all three, the mechanical compile was seconds.
- The elapsed time went to **review rounds** (3–5 per package, 20–60 min each) and to **waiting between owner round trips**.

## 2. Ranking the candidate causes

| Candidate cause | Share of v4.1.5's cost | Evidence |
|---|---|---|
| **New domain and contract design** | **largest** | Workspace addressing, investigation listing, investigation creation and the IF41-APP split have no design anywhere (`proposals/r10-historical-data-compatibility/SCHEMA-DIFF.md` §5; `docs/rehearsal-010-optimization/execution-readiness/FILE-OWNERSHIP-RECOMMENDATIONS.json` FO-2). Each must also fit the frozen-rule constraints in §3. |
| **Compiler rules that the combined scope breaks** | **large, and the main surprise** | See §3. Three of the requested changes are refused by the v4.1.x successor rules as written: new contract files, new DAG tasks, and new state-machine states or events. Either the compiler is extended (as v4.1.1 and v4.1.2 did, in commits `c37606a` and `2dc5a05` "compiler support"), or the content is shaped to fit. |
| **Independent review and corrective rework** | **large** | 3–5 rounds per package, measured (§1). Review volume grows with scope. v4.1.5 changes contracts, state machines, ownership and tasks at once; v4.1.4 changed runners only. |
| **Owner round trips** | **large in elapsed time, small in owner effort** | Each successor needs at least three owner acts: interface-change requests (before the compile), package approval and tag. Measured owner action time is minutes. Measured waits were hours (v4.1.4: 10 h 44 min before decisions). |
| **Manual requirements interpretation** | medium | Only where the new design touches requirements: EXT-DATA-003/-008/-013, PERSIST-005/-006, and the UI rows in `SCHEMA-DIFF.md` §6. A successor amendment changes no requirement, obligation or test binding (X20 classifies every change). |
| **Traceability across 205 requirements and 890 obligations** | **small** | Already mechanical: the compiler regenerates the bindings and `scope-manifest.json` counts (205 / 890 / 660 plan tests) in seconds, and X-checks verify them. A v4.1.x successor does not add obligations. |
| **Repeated validation** | small | 1 s compile, 9 s typecheck, 147 s offline suite. Repetition is cheap. |
| **F# compilation** | negligible | 9 s |
| **CI queueing** | small | about 22 min per push. It matters only because review rounds each wait for a push. |
| **Agent coordination** | medium, if more than one agent edits the package | Two other agents' proposals feed v4.1.5 (#119, #121). A shared `requirements-compiled/v4.1.5/` edited by several agents is the shared-file conflict to avoid (`PARALLELIZATION-OPTIONS.md`). |
| **Rework from incomplete specifications** | medium-high risk | ODR-R10-25's "a creation path in SM-SAVE" and "a Workspace in Repository" are intents, not shapes. The first review round is likely to reopen shape choices. That is the pattern of v4.1.1 rounds 1–3, which were FAILs. |

## 3. The rules that make the combined scope hard (`tools/requirements-compiler/src/V41Package.fs`)

| Rule | Where | Consequence for v4.1.5 |
|---|---|---|
| A contract may differ from its baseline only as the amendment's `contractChanges` declares, and each change must name a decision of kind **`interface-change`** or **`state-machine-amendment`** in the package's own `proposals/owner-decision-requests.json` | `V41Package.fs:1774-1787` | The recorded OD-HC-01, ODR-R10-25 and ODR-R10-32 are intent decisions in other request files. They do not satisfy this rule. **v4.1.5 needs its own interface-change requests, which the owner records before the compile.** This is one more owner round trip, inherent in the process. |
| A contract file not in the reviewed baseline is refused: `"%s is not in the reviewed baseline %s"` | `V41Package.fs:1790` | The contract-first agent's 9 UI contract files (`proposals/r10-contract-first-ui/contracts/`, about 1,200 lines) cannot be added as new files without a compiler change. **Alternative:** fold them into existing contract files (`06-IF41-APP.fs`, `07-IF41-VIEW.fs`) as declared changes, or extend the compiler under review. |
| "No state or event is added" to any v4 state machine (X05, X12) | `V41Package.fs:189`, `:359`, `:1043`, `:2056` | A creation path cannot add a `Creating` state or a `CreateRequested` event to SM-SAVE or SM-LOAD. It must reuse existing states and events: for example, start a session in `Unsaved` from a new `SaveMachine` field, and add transitions under `state-machine-amendment` decisions. Otherwise it needs a v4 (major) successor. |
| A task adjustment may name only v4 tasks: `"adjustment names %s, which is not a v4 task"` | `V41Package.fs:778-780` | ODR-R10-21's split of T19 and T22 into new tasks is refused as written. **Alternatives:** split at the Praxis work-item level (the ODR-R10-19 successor scope did not split them: `scope-manifest.successor.json` still has one T19 and one T22), or extend the compiler. |

## 4. Mechanical work versus design work

| Activity | Kind | Existing tool that does it |
|---|---|---|
| Regenerate contracts, ownership, state-machine tables, plan and bindings from `analysis/*.json` | mechanical | `requirements-compiler v41` (1 s) |
| X01–X21, typecheck, negative controls, offline suite | mechanical | the compiler, `ContractCheck.fsproj`, `tools/requirements-compiler/tests` (seconds to minutes) |
| Owner decision recording, approval, tag | mechanical for the owner | `tools/owner-freeze.fsx` (minutes once requests exist) |
| Praxis work items, Conditor bootstrap, Dokimos/Ordo verification | mechanical, but **downstream of the package** | Praxis, Conditor 0.10.18, Dokimos, Ordo. None of them authors contract shapes; they consume the frozen package. They cannot shorten the design or review work. |
| Event union, envelope and errors | **design done**; transcription remains | `proposals/r10-historical-data-compatibility/src/SuccessorContract.fs`, 20 checks |
| Workspace, listing, creation, IF41-APP split | **design not done** | — |
| Folding the UI contracts into existing files, or a compiler extension | design and engineering | — |
| Independent review | judgment | a separate agent, as for every earlier package |

**Conclusion:** the mechanical part of v4.1.5 (generate, validate, typecheck, test, record) is **under an hour of agent time** including CI. The cost that turns into days is:

- design not yet done;
- shaping that design to fit the compiler rules, or extending the compiler;
- 3–5 review rounds over a much larger change than any previous successor;
- owner round trips.

## 5. Compilation automation: what exists and what is worth doing before November 7

| Optimization | Existing support | Verification it would need | Worth doing before Nov 7? |
|---|---|---|---|
| Generate typed interfaces from approved specifications | **Yes, already.** The contracts are generated from `analysis/interface-specifications.json` (each interface's `fsharp` text), and `ContractCheck.fsproj` typechecks them. | none new | Already in use. Nothing to build. |
| Reuse verified contract fragments | **Partly.** A successor reuses its baseline byte for byte. Only declared `contractChanges` differ, each pinned by digest (`V41Package.fs:1774`). `SuccessorContract.fs` is a verified fragment ready to transcribe. | digest pin plus review of the changed text, as today | **Yes**, by transcription. No tooling needed. |
| Compile only changed inputs while validating the whole output | **Not needed.** A full compile and validation take about 1 s, measured. | — | **No.** No time to gain. |
| Automate requirement-to-field traceability | **Mostly exists** for requirements → obligations → tests → owners (bindings, X-checks). There is no field-level trace, for example from `INV-027` to a contract field. `SCHEMA-DIFF.md` §6 was done by hand. | a new X-check and tests, plus review | **No.** It would itself need a review cycle, and v4.1.5 adds no requirement. |
| Run independent checks concurrently | **Yes.** CI runs about 15 jobs in parallel (PR #120). Locally, compile (1 s), typecheck (9 s) and offline (147 s) are already short. | — | **No** tooling work. Scenario C runs *reviewers* concurrently, which is the real lever. |
| Avoid redundant regeneration | not a cost (1 s) | — | No |
| Deterministic cached outputs with provenance | **Yes, in effect.** Outputs are deterministic and pinned by `package-manifest.json` digests; frozen packages are checked against their tags in CI. | — | Already in use. |
| Allow new contract files, state additions and new tasks in a successor | **No.** The rules in §3 refuse them. | compiler change, new X-check semantics, tests, independent review, owner decision (it changes the rules) | **Only if** the complete package is chosen. Not needed for the minimum. Not recommended before Nov 7 for state additions, which would relax X05/X12. |

**Bottom line:** the toolchain is already fast and deterministic. No automation would remove the real costs (design, review, owner round trips). The only tooling change worth considering is the new-contract-file extension, and only for the complete package.
