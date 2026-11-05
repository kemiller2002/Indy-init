# Rehearsal 010: architectural lessons from the data browser

**1. Why did the plan make a basic data browser depend on the domain engine?**

Every requirement that touches stored cases is written authoritatively.
- EXT-DATA-013: the catalog is replayed through the engine.
- UI-001-O1: Connect shows the catalog cases.
- T25: "catalog records are v1.0.0 file sets replayed through the engine".
- No requirement says "display what is stored".

The packaging then grouped those requirements by family, into generated items. Every one of those items waits for T17 (PROCESS-CORRECTIONS.md: "They depend on T17 only"), and T20 waits for the save machine (dependency-graph.json:1402). So the browser inherited the engine's and the save machine's dependencies.

**2. Which dependencies were technically necessary?**

For raw display, only these are technically necessary:
- the bootstrap (T10);
- package pins (T11, or carried by the slice);
- a page to render into (T16, or a minimal shell).

For authoritative display, the kernel, replay and the catalog load are necessary too: T13, T15, T25, and a contract that can represent the data (ODR-R10-25). The save machine (T19) is necessary for neither. DATA-VISIBILITY-DEPENDENCIES.md gives each edge with its evidence.

**3. Which came from requirements packaging or governance?**

| Dependency | Source |
|---|---|
| Generated items after T17 | the governance rule "a generated item never precedes the kernel" |
| T20 after T19 | a design note (ADR-R7-001): Connect drives the save process |
| No capability for raw display | requirement phrasing |
| One full gate per slice | process |

None of these is wrong for an investigation engine. Together they make a data browser wait for one.

**4. Could the same mistake affect other Echelon applications?**

Yes, wherever three things meet:
- requirements are phrased only in their authoritative form;
- generated work items follow a kernel checkpoint;
- one gate integrates one slice.

This is a structural property of the compiler's packaging, not something specific to Causa. **It has not been demonstrated outside Indy-init**, so no change to Ordo, Praxis or Conditor is proposed.

**5. Should the standard architecture support a read-only, data-first vertical slice?**

Yes, as an explicitly non-authoritative capability with three properties:
- it carries no domain requirement;
- it is labelled as raw;
- it is built from the same read adapter the engine later uses.

That gives a runnable, real-provider application early, and exercises the riskiest external boundary (credentials, permissions, CORS, the file layout) hours before the engine needs it. In Rehearsal 010 it would have surfaced the C1 contract gap at about T+120 instead of at T25.

**6. Can it be generalized without much complexity?**

Probably. It needs:
- one slice template, "Data visible", depending only on the bootstrap;
- a gate rule for a slice that claims no requirement, and must not;
- a label check that the slice never claims authoritative interpretation.

That is a small addition to the compiler's slice manifest and to the gate. It is **not** to be built until ODR-R10-30 has been tried in a rehearsal and its benefit measured. The mission's instruction: first demonstrate it in Indy-init.
