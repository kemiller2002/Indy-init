# Rehearsal 010: orchestration overhead

## Who does what (frozen v4.1.4, AGENT-RULES.md; STARTUP-INSTRUCTIONS.md)

| Duty | Owner today |
|---|---|
| Integration of all code; sole writer of shared files; merges qualifying PRs in order; runs contract and state-machine tests on each merge | **integration** |
| Contract approval and interface change control; requirements ledger; kickoff record; `evidence/` | **coordinator** |
| Creating work items | the builder, at T0 (`praxis add`, STARTUP §3) |
| Running verification | the governed gate (CI). Builders run their own tests, which never count as acceptance |
| Handling failed builds | the slice's owner, through the slice loop (STARTUP §4) |
| Merging approved work | integration, on a gate PASS only |
| Final evidence | integration (T34) after the fresh reviewer (T33) |

There is already **one integration coordinator with independently owned lanes**. No new hierarchy is proposed.

## Redundant handoffs and repeated interpretation found

1. **Requirements are read twice per slice.** The lane interprets them to implement, and integration re-reads them to judge readiness before merging. AGENT-HANDOFF-CONTRACT.json lets the lane state exactly which requirements, obligations, commit, files and test results it hands over. Integration then checks those facts against the records instead of re-deriving them.
2. **The coordinator and integration are separate roles that can sit on one agent.** The v4.1.4 topologies put them together, and nothing measured shows a benefit in splitting them. Keep them together.
3. **Change requests for `web/index.html` and `web/causa.css`** are one Praxis item each (AGENT-RULES rule 4). This is the right control. Batch them per slice instead of per edit, so each needs one integration round trip.
4. **Unmeasured:** the agent time spent on merge supervision. The model treats it as free (RVO-1-6). Rehearsal 010's conflict telemetry (`analysis/conflict-telemetry.json`) is the first chance to measure it.

## What is not proposed

- A new agent layer: there is no measured evidence for one.
- A replacement for the Praxis or Ordo contracts: the handoff is a view over Praxis items and progress events, not a new authority.
