# Decision inventory: choices agents kept making independently

Each row is a decision that agents in different rehearsals made differently, or re-made after a failure. The question for each is whether the decision should be made once, before the competition. "Decided by" names the authority that already settles it, if one exists, or the proposed convention.

| # | Decision agents kept making | Evidence of divergent or repeated choices | Make once? | Decided by |
|---|---|---|---|---|
| D-01 | What "saved" means: client state, a successful PUT, or a provider read-back | HF-006, HF-021, HF-026, HF-040, HF-074 | **Yes** | CV-09 (proposed); state-machine contracts in v4.1.5 |
| D-02 | Whether a provider double may stand in for durability evidence, and what headers it models | HF-074, HF-039 | **Yes** | CV-09 |
| D-03 | How an unknown command, event or variant is handled: default, refuse or preserve | HF-041, HF-068 | **Yes** | CV-10, CV-11; historical-compatibility successor contract |
| D-04 | Whether code may assign machine state or catch-all faults | HF-008, HF-022 | **Yes** | CV-10 |
| D-05 | Expected error vs exception policy | HF-022 (two JSON codecs, swallowed faults) | Yes (policy only) | CV-10, F-SHARP-PATTERNS.md §2 |
| D-06 | Which document is the build target | HF-001, HF-037, HF-038 | **Already decided** | AGENTS.md + scope manifest + `check-sequencing` (CV-03) |
| D-07 | When to stop | HF-048, HF-057 | **Already decided** | `stop-decision` (CV-02) |
| D-08 | Which Forma version and which patterns; whether to patch layout in application CSS | HF-019, HF-084, HF-085 | **Yes** | CV-17 (version pin needs owner approval) |
| D-09 | How editable fields bind (input vs change) and how HTML ids map to F# names | HF-004 | Yes | CV-18, CV-23 (contract-first UI proposal) |
| D-10 | Where ports, hosts and base URLs live | HF-014 | Yes | CV-05 |
| D-11 | How workflow steps handle errors (`pipefail`, output on failure) | HF-030 | Yes | CV-07 |
| D-12 | Workflow triggers and concurrency | HF-011, HF-031, HF-032 | **Already decided** | Conditor `ci-policy` (CV-06) |
| D-13 | Who may edit a shared file, and how tool state is merged | HF-015, HF-042, HF-043 | Partly decided | `agent-file-ownership.json` (CV-08); `.ros` merge policy is open → Tier 2 |
| D-14 | How a contract change is released to parallel agents | HF-012, HF-035, HF-072 | Partly decided (AGENT-RULES "interfaces frozen at T14") | CV-12; EXP-INDY-002 measures the asynchronous release model |
| D-15 | When a test counts as proving an obligation | HF-009, HF-045, HF-046 | Partly decided (slice gate, assertion ratchet) | CV-13 deferred: modeled net is negative in the conservative and central cases |
| D-16 | Which SHA evidence and reviews refer to | HF-024, HF-051 | **Already decided** | completion gate (CV-15) |
| D-17 | Whether an agent may record or interpret owner approval | HF-003, HF-050, HF-077 | **Already decided** | RDP-003, owner-freeze.fsx (CV-16) |
| D-18 | How a WASM app is built and smoke-tested before features are added | HF-007, HF-013 | Yes | CV-04 |
| D-19 | Per-feature F# file layout (Contract/Domain/State/View/Tests) | none; no finding attributes a defect to layout | **No** | DISCRETIONARY (CV-22). File boundaries are already fixed by the frozen project structure and ownership. |
| D-20 | Whether a requirement contradicted by an owner decision is still binding | HF-023, HF-036 | Yes, before freeze | CV-14 |

## Decisions deliberately left to agents

- **Module and function layout inside an owned file:** D-19. The evidence gives no reason to standardise it, and splitting files would collide with frozen ownership.
- **Test organisation within a test project.** No finding concerns it.
- **Local helper abstractions.** F-SHARP-PATTERNS.md §6 discourages abstractions added only for testing, but does not prescribe any.
