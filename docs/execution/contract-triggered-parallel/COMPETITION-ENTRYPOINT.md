# Competition entrypoint (read before starting)

This page applies CTP-001 inside the governed competition procedure. It does not replace that procedure:
- `docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md` is authoritative for bootstrap, gates and stopping;
- the frozen package's `AGENT-RULES.md` is authoritative for duties.

**Where they disagree, they win.**

## 0. Rules that never bend
- **No pre-written code.** Start from the frozen requirements only (OD-INDY-22). Do **not** copy any code from:
  - `experiments/` (EXP-INDY-002);
  - `practice/`;
  - `preview/causa/`;
  - any `proposals/*/prototype` folder;
  - any earlier rehearsal.
- **One clock.** It comes from `evidence/t0/t0.json`: **420 minutes** from T0. Stop only on STOP_PERMITTED (STARTUP §6).
- **Agents never record approvals.** The owner records them (RDP-003).
- **Nobody merges past a red required check.** Infrastructure failures that happened before any test ran are re-run once.

## 1. Tooling (T0)
1. **Install and verify** the pinned Conditor and workstation exactly as the frozen package's AGENT-RULES duty 1 and STARTUP §2 step 1 say (`r9.fsx govern … --lock kickoff/rehearsal-010/toolchain.lock.json`).
2. **Write the T0 record** (STARTUP §2 step 2), then run the suite preflight before the first push (STARTUP §2 step 3, §3 step 1).

## 2. Requirements and work items (before any application code)
1. **Load the frozen package** that the adopted lock names. That is v4.1.5 once it is tagged. Otherwise it is v4.1.4 with ODR-R10-25 B's declared failures, per the owner's ODR-V415-04 A fallback (Nov 6 22:00 UTC).
2. **Register every work item in Praxis** from the generated work plan, then write the scope checkpoint and initialization checkpoint (STARTUP §3 steps 2–4).
3. **No application code** until both checkpoints exist (`check-sequencing`).

## 3. Contracts (CTP-001 §2–§5)
1. **Contract coordinator (integration role):** write the run's contract registry (CONTRACT-SCHEMA.json). Write it in the window, from the frozen package's contracts, field map and requirement catalog. Group it by slice in milestone order (M1 connect, M2 browse and open, M3 create, M4 save and reload).
2. **Validate each group:**
   ```
   dotnet fsi SRC/tools/execution/ctp-check.fsx -- registry --registry contracts/registry.json
   ```
   Then compile the contract project, and record VALIDATED with the group's hash.
3. **Release incrementally.** Release the first group the moment it validates, and record RELEASED. Later groups follow as they validate.

## 4. Dispatch (concurrently)
- **Domain and persistence agents** implement released groups in their own files. Their gate is compile plus focused unit tests. They record IMPLEMENTING and STAGED.
- **UI agents** (ui, ui-2, ui-3, per the competition UI ownership map) implement screens at the same time.
  - They work against the contract's ports, with isolated typed stubs until the domain is staged.
  - Their gate is compile plus the static binding check.
  - They record IMPLEMENTING and STAGED.
- **The reviewer (verification)** writes the independent acceptance tests from the registry and requirements, at the same time as implementation.
- **Agents push to their work branches.** Only the integration coordinator pushes PR branches, once per candidate (INTEGRATION-POLICY §4).

## 5. Integrate on readiness (INTEGRATION-POLICY §2)
When both sides of a group are STAGED on the released hash, follow INTEGRATION-POLICY §2 in full:
1. check the versions (`ctp-check integration`);
2. assemble;
3. compile;
4. run the unit and contract tests;
5. run the **independent browser test, mobile viewports included**;
6. classify defects and assign each to its owner;
7. record evidence, then record INTEGRATION_READY.

Then open the slice's PR through the governed slice gate (STARTUP §4).

## 6. Acceptance
- **VERIFIED** needs all of these:
  - required full CI passed on the candidate SHA;
  - the acceptance run;
  - the governed slice gate (journeys, design contract, regression).
- **Durable persistence** is verified only by the live real-provider workflow, with a real write and an independent read-back. A REST double is never reported as persistence acceptance.
- **Run at each merge and before stopping:**
  ```
  dotnet fsi SRC/tools/execution/ctp-check.fsx -- lifecycle --log evidence/ctp/lifecycle.jsonl
  dotnet fsi SRC/tools/execution/ctp-check.fsx -- integration --integration evidence/ctp/integration.json --log evidence/ctp/lifecycle.jsonl
  ```
  Both must pass.

## 7. Provenance and evidence
Commit these under `evidence/ctp/`:
- the lifecycle log (append-only);
- the integration records;
- the change requests;
- each agent's own timeline (single-writer).

Every acceptance claim cites a run reference and a candidate SHA. Praxis is checkpointed at every merge (STARTUP §4 step 9).

## 8. Deadline
- **Priority.** Integrate and verify what is staged before starting new groups when less than **one CI cycle plus integration** remains: about 30 minutes, measured during the run.
- **Never** report unverified work as done.
