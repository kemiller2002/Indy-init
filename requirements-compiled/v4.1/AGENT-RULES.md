# Agent roles and coordination rules (v4.1)

These rules apply during the timed build. The authority on who owns which file is [agent-file-ownership.json](agent-file-ownership.json), generated from [analysis/project-structure.json](analysis/project-structure.json). Where this page summarises, the manifest wins. The compiler checks the manifest (X01–X05, X14).

## Roles

| Role | Owns | Does not touch |
|---|---|---|
| domain | `src/Causa.Domain/*` (kernel); the invariant tests it writes first (v4 T12) except live ones; domain unit and contract tests | persistence, UI, shared files |
| persistence | `src/Causa.Persistence/*`; the stateful fake and recordings (`tests/Causa.TestSupport`, referenced by every test project); state-machine tests for SM-SAVE, SM-CONFLICT and SM-LOAD; live invariants; acceptance tests of requirements a persistence task covers first | UI, shared files |
| ui | Limen engine, routes, projections, shell, investigation views, save indicator, conflict review; `web/index.html` and `web/causa.css` (loading shell and layout, T16); SM-CONNECTION and SM-NETWORK tests; acceptance tests of requirements a UI task covers first | ui-2's files, shared files |
| ui-2 (topology C) | Causal graph, report, history and administration views, and their tests. In topologies without a ui-2 lane, these belong to ui (`ownerByTopology`) | `Views/Shell.fs` (read-only), ui's files |
| integration | Every project, solution and test-project file; `Directory.*` build files, NuGet configuration, `global.json`; package manifests; CI and deployment; `build.fsx`; `README.md`; `web/boundary.js`; `.echelon/`; `tests/fixtures/` and `tests/browser/support/`; the host shim; `View.fs`, `Program.fs`; acceptance tests of requirements only integration tasks cover | other roles' source files |
| coordinator | Interface change control, requirements ledger, kickoff record, `evidence/` | product source |
| reviewer | Independent review checkpoints; verifier tests | product source |

## Integration agent duties

1. **At bootstrap (T10, before T0), generate the empty repository with `requirements-compiler v41 ... --bootstrap DIR`** (DIR may already hold `.git`). It writes every project and test-project file once and completely (Compile lists from `agent-file-ownership.json`), every planned file as an empty namespace, and each contract in its producer's file; CI proves it builds. It also writes the test tooling the owner locked (ODR-V41-07): `xunit` 2.9.2, `xunit.runner.visualstudio` 2.8.2 and `Microsoft.NET.Test.Sdk` 17.11.1 in the six test projects, and `@playwright/test` 1.63.0 in `package.json`. No other package is added without an owner decision; axe-core waits for ODR-V41-09. No agent adds a file to a project later. This relies on ODR-V41-06.
2. **At the interface freeze (T14), build.** The contracts are already in their producers' files from the bootstrap. Each producer reviews its block and writes its producer contract tests (the domain's kernel-dependent ones complete in T13 and T15); each consumer writes its consumer contract tests in its first task. Integration builds the solution; a contract that does not compile stops the freeze. Integration does not write other roles' files.
3. **Merge qualifying pull requests in order.** Run the contract tests (`Causa.Contract.Tests`) and the state-machine tests on each merge.
4. **Be the only writer of shared files.** Another role that needs a shared change asks integration, which makes it in one pull request.
5. **Record coordination telemetry as it happens** (see [analysis/conflict-telemetry.json](analysis/conflict-telemetry.json)): every conflicting merge, its resolution time, and every reverted integration.

## Rules

- **One owner per file.** A contributor changes a file only through a pull request that its owner approves. Two agents never edit the same file at the same time.
- **Interfaces are frozen at T14.** Any change to a union or record, including an added case, is breaking. It goes through the coordinator as a new version, with every consumer's contract tests changed and passing in the same pull request.
- **Implement the state-machine tables.** Every (state, event) pair in [state-machine-contracts.json](state-machine-contracts.json) is either legal, with its listed transitions, or fails closed with `SaveFault`. Each machine's state and input unions are exactly its states and events. No state is added.
- **Classify GitHub answers with the matrix.** Use [analysis/github-error-matrix.json](analysis/github-error-matrix.json) and its precedence rule. Never classify by HTTP status alone where one status has several meanings, and never retry beyond the class's bound.
- **Use only published Forma.** `web/causa.css` reads `--ef-*` properties listed in the inventory and declares none of its own; it applies the recorded Forma gap remedy (every link at least `--ef-layout-target-min`; nine Forma 0.12.0 patterns have shorter links). Layout comes from the Forma patterns named in the layout contract, and the plan's browser tests run the executable assertions in `layout-tests/assertions.mjs` against Causa's screens. There is no new CSS framework. Amber (UI-016's `#E9A23B`) is drawn with Forma's `--ef-color-status-warning` and always carries a text label (ODR-V41-05).
- **Declare every dialog's invoker.** A control that opens a dialog names it with `commandfor`, `popovertarget` or `aria-controls`, so the layout kit's `dialogFocus` finds it (a WASM handler attached by script is otherwise invisible to the page). A plan test may instead pass `{ invokers: [{ invoker, dialog }] }`.
- **Run the suites in the verification execution plan's order.** Each suite shares an environment, but every test keeps its own identity and assertions.
- **Never write an owner decision.** Only the operator writes approval records (RDP-003).
