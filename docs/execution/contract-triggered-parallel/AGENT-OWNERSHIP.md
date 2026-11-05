# CTP-001 agent ownership

- **Single-writer rule.** Each role owns its files exclusively. A file has exactly one owner, and `ctp-check ownership` refuses a file with no owner or with two.
- **Shared wiring.** Project files, the entry point and the composition root belong to the integration coordinator.
- **Agent logs.** Each agent writes only its own timeline. The lifecycle log is append-only and each role records only the states the protocol assigns to it (CTP-L-ROLE). The `actor` field uses the package's role names where they exist: `integration` (it also acts as contract coordinator), `domain`, `persistence`, `ui`, `ui-2`, `ui-3`. The verification role (`reviewer`) writes tests and integration records, never lifecycle states.

| Role | Owns | Does | Never |
|---|---|---|---|
| **Contract coordinator** | the F# contract file(s), the registry, the lifecycle log's DEFINED/VALIDATED/RELEASED rows, CR decisions | defines contracts; runs `ctp-check registry` and compiles the contract to VALIDATE; releases incrementally; resolves change requests and notifies the implementing roles | implements domain or UI code for a group it released |
| **Domain agent** | domain implementation files and their unit tests | implements released groups against the contract; runs F# compilation and focused unit tests; records IMPLEMENTING and STAGED; raises CRs | edits contracts, UI files or shared wiring |
| **UI agent** | Forma views, bindings, UI tests, web assets | implements screens against the contract's ports; may use **isolated typed stubs** of the ports until the domain is staged; runs compilation and the static binding check; records IMPLEMENTING and STAGED; raises CRs | edits domain files, contracts or shared wiring; ships a stub in the integrated build |
| **Verification agent** | the independent acceptance tests | writes acceptance tests **from the requirements and the registry only**, never from implementers' markup or code; reports missing fields, transitions, labels and behaviour as CRs or defects; rejects implementer self-attestation | implements the code it verifies |
| **Integration coordinator** | shared wiring (project files, Program/composition, port selection), INTEGRATION_READY/VERIFIED rows, integration records | integrates matching contract versions; runs cross-component, contract and browser tests; classifies each defect and assigns it to its owner; records acceptance evidence; owns candidate pushes | repairs another role's defect in that role's files |

## Combining roles
- **Allowed:** roles may be combined when capacity is limited. EXP-002 combined contract coordinator, verification and integration in one agent.
- **Must remain explicit:**
  1. **The assignment is written down** before work starts. It is the `owners` map passed to `ctp-check ownership`.
  2. **The independent verifier is not an implementer of the code it verifies.** `ctp-check integration` refuses an acceptance run whose author is listed among the implementers (CTP-I-INDEPENDENT).
  3. **Combining contract author and verifier is permitted but weaker.** EXP-002 shows why: the test was independent of the implementers but not of the contract. State it in the evidence when it happens.

## Mapping to the competition build
- **Which roles exist:** the competition build uses the frozen package's roles (integration, domain, persistence, ui, ui-2, ui-3, reviewer) and its `agent-file-ownership.json`. CTP-001 roles map onto them; they do not replace them.

  | CTP-001 role | Package role(s) |
  |---|---|
  | contract coordinator | integration (contracts are frozen package content; competition-time CRs are additive) |
  | domain agent | domain and persistence |
  | UI agent | ui, ui-2 and ui-3, per the competition UI ownership map (`proposals/r10-contract-first-ui/COMPETITION-UI-OWNERSHIP.json`, DTD-R10-004) |
  | verification agent | reviewer |
  | integration coordinator | integration |

- **Which files each owns:** the package's ownership file is authoritative. `ctp-check ownership` is a quick local cross-check, not a replacement for the package's OwnershipTests.
