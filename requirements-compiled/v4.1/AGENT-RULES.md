# Agent roles and coordination rules (v4.1)

These rules apply during the timed build. The roles, files and owners are defined in [analysis/project-structure.json](analysis/project-structure.json) and [agent-file-ownership.json](agent-file-ownership.json). The compiler checks both (X01–X04, X14).

## Roles

| Role | Owns | Does not touch |
|---|---|---|
| domain | `src/Causa.Domain/*` (kernel), invariant tests | persistence, UI, shared files |
| persistence | `src/Causa.Persistence/*`, stateful fake, recordings, PERSIST acceptance tests | UI, shared files |
| ui | Limen engine, routes, projections, shell, investigation views, save indicator, conflict review, UI and WDL acceptance tests | ui-2's views, shared files |
| ui-2 (topology C) | Causal graph, report, history and administration views | `Views/Shell.fs` (read-only), ui's views |
| integration | Every shared file:<br>• solution, project files, package manifests;<br>• CI, deployment;<br>• host shim, boundary script, `web/causa.css`;<br>• `View.fs`, `Program.fs` | other roles' source files |
| coordinator | interface change control, requirements ledger, kickoff record, `evidence/` | product source |
| reviewer | independent review checkpoints, verifier tests | product source |

## Integration agent duties

1. **At bootstrap (T10, before T0), write every project file once and completely.** Each project's Compile list comes from `agent-file-ownership.json` `compileLists`, and every planned module starts as an empty stub. No agent adds a file to a project later.
2. **At the interface freeze (T14), copy [contracts/](contracts/) into the owning files.** Then build. A contract that does not compile stops the freeze.
3. **Merge qualifying pull requests in order.** Run the cross-component contract tests (`Causa.Contract.Tests`) on each merge.
4. **Be the only writer of shared files.** Another role that needs a shared change asks integration, which makes it in one pull request.
5. **Record coordination telemetry as it happens** (see [analysis/conflict-telemetry.json](analysis/conflict-telemetry.json)): every conflicting merge, its resolution time, and every reverted integration.

## Rules

- **One owner per file.** A contributor changes a file only through a pull request that its owner approves. Two agents never edit the same file at the same time.
- **Interfaces are frozen at T14.** A change goes through the coordinator. It gets a new version, and consumers are told before it merges. A breaking change needs every consumer's contract tests to pass in the same pull request.
- **Implement the state-machine tables.** Every (state, event) pair in [state-machine-contracts.json](state-machine-contracts.json) is either legal, with its listed transitions, or fails closed with a typed fault. No state is added.
- **Classify GitHub answers by the matrix.** Use [analysis/github-error-matrix.json](analysis/github-error-matrix.json). Never classify by HTTP status alone where one status has several meanings, and never retry beyond the stated maximum.
- **Use only published Forma.** `web/causa.css` reads `--ef-*` properties listed in the inventory and declares none of its own. Layout comes from the Forma patterns named in the layout contract. There is no new CSS framework.
- **Run the suites in the verification execution plan's order.** Each suite shares setup, but every test keeps its own identity and assertions.
- **Never write an owner decision.** Only the operator writes approval records (RDP-003).
