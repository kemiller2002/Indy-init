# Compiled package v4.1.5: real causa-data, minimum contract change

**Status: CANDIDATE.** It is not approved, not frozen and not pinned by any rehearsal lock. The owner has recorded ODR-V415-01, -03 and -04 (APR-V415-OWNER-1) and ODR-V415-IF-VIEW (APR-V415-OWNER-2), all option A; their approval ids are written in (FREEZE step 2) and it validates fully (X01-X21 PASS). It awaits its approval, ODR-V415-02.

v4.1.5 is the successor amendment of v4.1.4, which the owner approved as APR-V414-OWNER-2. v4.1.4 is not edited.

It is the smallest contract change that lets the governed build work on the real catalog: read it, list it, create an investigation, save it and reload it. Its authority is ODR-R10-25 A, OD-HC-01 A, and workstream D of the owner's Parallel Acceleration Directive of 2026-11-05.

**One decision, ODR-V415-01.** Its parts depend on each other, so they cannot be chosen separately:

| Part | What it does |
|---|---|
| E1 | `CoreEvent` represents every causa-storage v1.0.0 event without loss: 50 cases, with every v4.1.4 name kept. The envelope carries provenance, and `VerificationFailure` carries the §11.1 reader stage. The commands whose events gained fields carry them too. |
| W1 | `Workspace` and a private `WorkspaceAddress` in the connected repository, whose role is derived, never asserted. A catalog comes from `catalog/active.json`. A working copy can only be `workspaces/demo/causa-app-<run>`, and never a workspace a catalog names. `InvestigationRef` keys investigations by workspace and id. `Writable` is the only way to a save session, and it refuses a catalog. |
| L1 | `WorkspaceLister` lists a workspace as a read, step by step, at one commit. |
| C1 | `SaveMachine.InitialNew` and `StorageCodec.EncodeNew` create an investigation through existing SM-SAVE transitions. T-L03 is replaced so a catalog load creates no save session. |

**Two further owner decisions come with it, and approval does not imply either one.**
- **ODR-V415-03, staging.** ODR-R10-32 A (recorded) commissions ONE combined v4.1.5 that also carries ownership FO-1..FO-3 and the T19/T22 split. This package does not carry them, so freezing it departs from that record unless the owner chooses to stage.
- **ODR-V415-04, the fallback trigger.**

ODR-V415-02 approves the package. `owner-freeze.fsx approve v4.1.5` refuses until ODR-V415-01, -03 and -04 are all recorded; ODR-V415-IF-VIEW must be recorded too, or the fold taken out.

No state, event, DAG task or contract file is added. No requirement, obligation or test binding is removed. **One acceptance condition is weakened**, see below. Four contract files change under ODR-V415-01 and a fifth, `07-IF41-VIEW.fs`, under ODR-V415-IF-VIEW (`analysis/amendment.json` `contractChanges`), and there is one SM-LOAD replacement and one SM-SAVE accepted note. Eight producer contract tests are added for the new surface.

**Weakened: scenario A4's falsification scope.** Acceptance scenario A4 says "the falsifying evidence and scope are recorded" (`docs/INVESTIGATION-ACCEPTANCE-SCENARIOS.md:47`). causa-storage v1.0.0 stores no scope on a hypothesis transition, so `HypothesisFalsified` cannot carry one. Scope can be recorded with `NegativeKnowledgeEstablished` (whose wire form has `scope`). A scope *on the falsification itself* needs causa-storage 1.1.0 (ODR-R10-26 A, OD-HC-06 B). It stays incomplete in the deferred record (`docs/rehearsal-010-dependency-reduction/CLASSIFICATION.md` §3).

**Not in v4.1.5; deferred with incomplete status:**
- ownership FO-1..FO-3;
- the T19/T22 split;
- causa-storage 1.1.0 content;
- commands for the 22 added event kinds. Reading needs only the events; deciding them is domain work.

**A separate decision, ODR-V415-IF-VIEW: the contract-first UI fold (CF-09 A).** `contracts/07-IF41-VIEW.fs` is the frozen IF41-VIEW followed by `namespace Causa.Ui.Contracts` (from `proposals/r10-contract-first-ui/v415-fold` at 21cc2b0): types and signatures plus 16 boundary members beside private representations, allowlisted by digest. It is declared as its own interface-change request and `contractChanges` entry, so ODR-V415-01 can be decided without it. With it come role ui-3 (fallback ui), the split Causa.App views and their owners, integration as producer of IF41-APP and IF41-VIEW (`Projections.fs`, compiled before `Connection.fs`) and the producer file `Ui/Presentation.fs`, ui-3 as producer of IF41-REPORT, the UI fixture helpers (`Causa.TestSupport` now references `Causa.App`) and the screen-fragment rules (`analysis/project-structure.json`). The bootstrap writes `Ui/Presentation.fs` as an empty namespace, like every non-contract file; its text is integration's work during the run. See [AMENDMENT-4.1.5.md](AMENDMENT-4.1.5.md).

**For builders.** The compiled `state-machine-contracts.json` still says SM-SAVE `initial: Saved`. That is the start of a loaded investigation. A *new* investigation's session starts in Unsaved (or Offline). That is stated in the `InitialNew` comment of `contracts/05-IF41-SAVE.fs`, in the SM-SAVE `accepted` note of `analysis/state-machine-amendments.json`, and by the test `Save.Producer.NewInvestigationSavesThroughExistingTransitions`.

Compile it:

```
requirements-compiler v41 --source . --package requirements-compiled/v4.1.5 --base requirements-compiled/v4
```

Freezing it is in [FREEZE.md](FREEZE.md); the change is described in [AMENDMENT-4.1.5.md](AMENDMENT-4.1.5.md).
