# Compiled package v4.1.5: real causa-data, minimum contract change

**Status: CANDIDATE.** It is not approved, not frozen and not pinned by any rehearsal lock.

v4.1.5 is the successor amendment of v4.1.4, which the owner approved as APR-V414-OWNER-2. v4.1.4 is not edited.

It is the smallest contract change that lets the governed build work on the real catalog: read it, list it, create an investigation, save it and reload it. Its authority is ODR-R10-25 A, OD-HC-01 A, and workstream D of the owner's Parallel Acceleration Directive of 2026-11-05.

| Change | Decision | What it does |
|---|---|---|
| E1 | ODR-V415-01 | `CoreEvent` represents every causa-storage v1.0.0 event without loss: 50 cases, with every v4.1.4 case name kept. The envelope carries provenance, and `VerificationFailure` carries the §11.1 reader stage. |
| W1, L1 | ODR-V415-02 | A derived, user-selected `Workspace`/`WorkspaceAddress` (a read-only catalog, or a writable working copy), and listing a workspace as a read |
| C1 | ODR-V415-03 | Creation through the existing SM-SAVE states (`SaveMachine.InitialNew` starts in `Unsaved`) |
| — | ODR-V415-04 | Approval of this package |

No state, event, DAG task or contract file is added. No requirement, obligation, test binding or acceptance condition is removed or weakened. Five contract files change (`analysis/amendment.json` `contractChanges`). Everything else is v4.1.4's.

**Not in v4.1.5; deferred with incomplete status** (`docs/rehearsal-010-dependency-reduction/CLASSIFICATION.md` §3):
- ownership FO-1..FO-3;
- the T19/T22 split;
- causa-storage 1.1.0 content.

The contract-first UI fold (CF-09 A, ODR-V415-IF-VIEW) is added to this package as a separate declared change once its role mapping compiles. See `docs/rehearsal-010-dependency-reduction/V415-DESIGN-NOTE.md`.

Compile it:

```
requirements-compiler v41 --source . --package requirements-compiled/v4.1.5 --base requirements-compiled/v4
```

Freezing it is in [FREEZE.md](FREEZE.md); the change is described in [AMENDMENT-4.1.5.md](AMENDMENT-4.1.5.md).
