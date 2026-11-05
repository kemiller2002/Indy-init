# Compiled package v4.1.5: real causa-data, minimum contract change

**Status: CANDIDATE.** It is not approved, not frozen and not pinned by any rehearsal lock. It validates except for the owner approvals it still needs, which are pending ODR-V415-01 (X12, X17: "names no operator approval").

v4.1.5 is the successor amendment of v4.1.4, which the owner approved as APR-V414-OWNER-2. v4.1.4 is not edited.

It is the smallest contract change that lets the governed build work on the real catalog: read it, list it, create an investigation, save it and reload it. Its authority is ODR-R10-25 A, OD-HC-01 A, and workstream D of the owner's Parallel Acceleration Directive of 2026-11-05.

**One decision, ODR-V415-01.** Its parts depend on each other, so they cannot be chosen separately:

| Part | What it does |
|---|---|
| E1 | `CoreEvent` represents every causa-storage v1.0.0 event without loss: 50 cases, with every v4.1.4 name kept. The envelope carries provenance, and `VerificationFailure` carries the §11.1 reader stage. The commands whose events gained fields carry them too. |
| W1 | `Workspace`/`WorkspaceAddress` in the connected repository; a catalog is read-only, a working copy is writable. `InvestigationRef` keys investigations by workspace and id. `Writable` is the only way to a save session, and it refuses a catalog. |
| L1 | `WorkspaceLister` lists a workspace as a read, step by step, at one commit. |
| C1 | `SaveMachine.InitialNew` and `StorageCodec.EncodeNew` create an investigation through existing SM-SAVE transitions. T-L03 is replaced so a catalog load creates no save session. |

ODR-V415-02 approves the package.

No state, event, DAG task or contract file is added. No requirement, obligation or test binding is removed. Four contract files change (`analysis/amendment.json` `contractChanges`), and there is one SM-LOAD replacement and one SM-SAVE accepted note. Eight producer contract tests are added for the new surface.

**Known requirement conflict, stated rather than hidden.** Acceptance scenario A4 says "the falsifying evidence and scope are recorded" (`docs/INVESTIGATION-ACCEPTANCE-SCENARIOS.md:47`). causa-storage v1.0.0 stores no scope on a hypothesis transition, so `HypothesisFalsified` cannot carry one. Scope can be recorded with `NegativeKnowledgeEstablished` (whose wire form has `scope`). A scope *on the falsification itself* needs causa-storage 1.1.0 (ODR-R10-26 A, OD-HC-06 B). It stays incomplete in the deferred record (`docs/rehearsal-010-dependency-reduction/CLASSIFICATION.md` §3).

**Not in v4.1.5; deferred with incomplete status:**
- ownership FO-1..FO-3;
- the T19/T22 split;
- causa-storage 1.1.0 content;
- commands for the 22 added event kinds. Reading needs only the events; deciding them is domain work.

The contract-first UI fold (CF-09 A) is added as a separate declared change once its role mapping compiles. See `docs/rehearsal-010-dependency-reduction/V415-DESIGN-NOTE.md`.

Compile it:

```
requirements-compiler v41 --source . --package requirements-compiled/v4.1.5 --base requirements-compiled/v4
```

Freezing it is in [FREEZE.md](FREEZE.md); the change is described in [AMENDMENT-4.1.5.md](AMENDMENT-4.1.5.md).
