# CF-09 A: the UI start rule

**Where:** `docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md` §4 "The slice loop", step 1, or its v4.1.5 successor. It extends ODR-R10-31 A, under which generated UI items start after T16.

**Current step 1:**

> 1. **Select** the highest-value ready slice: integrated dependencies, by the order in SLICE-EXECUTION-PLAN §3 (J1, then J2, then the design contract, then the rest of P0, then P1).

**Sentence to add as the second sentence of step 1:**

> A UI slice (implementation boundary ui-a, ui-b or ui-c) is ready to **start** once the bootstrap (SL-T10) is integrated and every UI slice it depends on is **built** on its own branch, because it builds against IF41-VIEW and the bootstrapped fixture backend; it is ready to **merge** only as every slice is, on `slice-gate` PASS with its declared dependencies integrated and its bound journey assertions judged as ODR-R10-33 decides.

**What it does not change:**
- the slice gate and the completion gate;
- the merge order;
- the rule that a fixture-backed result is never evidence: every fixture view carries `BackendOrigin.FixtureBackend`, and Layer C is unchanged (`../VERIFICATION-STRATEGY.md`).

The schedule model needs the build/merge split this sentence states (`buildDependsOn`/`mergeDependsOn`, `../SCHEDULE-INPUTS.md` §6).
