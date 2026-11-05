# v4.1.5 minimum: design note

**Status.** v4.1.5 is compiled in `requirements-compiled/v4.1.5` as a CANDIDATE. This note explains its design. `v415-contracts/` holds the same contract text, and `v415-contracts/v4.1.4-to-v4.1.5.diff` is the diff against frozen v4.1.4.

**Revision 2 (2026-11-05 ~20:00 UTC).** An adversarial pre-review returned FAIL on revision 1. Every finding is addressed below. The independent review the freeze needs is still to come.

## Contract changes

All five parts belong to one decision, ODR-V415-01; the second decision, ODR-V415-02, approves the package.

| Part | Files | Content |
|---|---|---|
| E1 | 00, 02 | <ul><li>`CoreEvent` goes from 28 to 50 cases. The case names equal the C11/C15-verified `CoreEventV415`, and every v4.1.4 name is kept.</li><li>`EventEnvelope.Provenance`.</li><li>`VerificationFailure.ReaderStage`.</li><li>`MarkHypothesisPlausible`/`Supported of MarkHypothesis`, and `RejectProposal of ProposalId * reason`.</li></ul> |
| W1 | 02, 06 | <ul><li>`Workspace`; `WorkspaceRole` (`Catalog` \| `WorkingCopy`); `WorkspaceAddress { Workspace; Role }`. Both roles are in the connected repository: CONTRACT §9.4 puts the writable area under `workspaces/demo/` of the data repository, and Connect has already checked that repository's permissions.</li><li>`InvestigationRef { Address; Id }` keys `Loads`, `Sessions`, `TimerKey` and the machine messages.</li><li>`Writable.T` is private, and its only constructor refuses a catalog.</li></ul> |
| L1 | 05, 06 | <ul><li>`ListingTarget`: `ActiveCatalog` derives the catalog workspace from `catalog/active.json`; `WorkingCopyOf ws`.</li><li>`ListingStep`: head → active-at → manifest-at, or head → directory-at. Each step carries its commit, so one listing reads one commit.</li><li>`WorkspaceLister { WorkingCopyFor; List; InterpretList }`. `WorkingCopyFor` maps a run id to `workspaces/demo/<run>`.</li><li>A working copy that does not exist yet lists as empty (EXT-DATA-009).</li></ul> |
| C1 | 02, 05, 06 | <ul><li>`InitialNew`: writable address, id, base head, the StartInvestigation command, and an online flag. It starts in Unsaved with `[start]`, or Offline when known offline.</li><li>`EncodeNew` returns Ok only for a file set Verify accepts.</li><li>`Msg.CreateRequested`.</li><li>T-L03 is replaced: only a working-copy load creates a save session.</li><li>A moved head: files absent at the fresh head mean `StaleHeadUnrelated`; files present there mean `StaleHeadSameInvestigation`.</li></ul> |

## Pre-review findings and how revision 2 addresses them

| Finding | Resolution |
|---|---|
| B1: a catalog load created a writable session | T-L03 is replaced, and `Writable.T` makes a catalog session unconstructible. Test: `WritableRefusesCatalog`, `CatalogLoadCreatesNoSession`. |
| M1: an empty new session; offline creation | `InitialNew` holds the start command and takes the network state |
| M2: the listing could not be interpreted | `ListingStep` is added to `ListingNextRequest`, `InterpretList` and `ListingsInFlight` |
| M3: no derivation; the repository was not checked | The address has no repository field, so it is the connected one. `ActiveCatalog` and `WorkingCopyFor` derive the workspaces. |
| M4: identity collided across workspaces | `InvestigationRef` keys |
| M5: commands could not produce the widened events | Three commands widened (opaque payloads, as in v4.1.2) |
| M6: approvals cited before they were recorded; requests that could not be chosen separately | The approvals are blank, so the package FAILs X12/X17 only on those. CI asserts exactly that (`pending415`). There is one request, ODR-V415-01, and the coupling is stated. The owner-record test is added at FREEZE step 2. |
| M7: no tests for the new surface | 8 producer contract tests are bound (`AMENDMENT-4.1.5.md`) |
| m1: A4's falsification scope | Stated as a known conflict with causa-storage v1.0.0. Scope can be recorded via `NegativeKnowledgeEstablished`; scope on the event itself is deferred to 1.1.0 with incomplete status. |
| m2: stale head on a new investigation | Specified in the `InitialNew` comment |
| m3: refresh after creation | A created investigation is opened (`OpenRequested`) after its first confirmed save, as any working-copy case is. That is the integration owner's composition, with no contract change. |
| m4: stale text | This note and the package documents are rewritten |

## For the lead and Agent 2: the IF41-VIEW fold

Agent 2's `project-structure.delta.json` renames roles `ui` and `ui-2` to `ui-a`, `ui-b` and `ui-c`. The v4.1.x compiler cannot accept that without a code change:
- test owners come from v4's laneOverrides and from hard-coded `"ui"` (`V41Package.fs:236, 252, 258, 297`);
- X01 fails for 65 test files, and X18 needs the requesters to be declared roles.

The `07-IF41-VIEW.fs` contract text itself folds cleanly. Note also that `07` still keys `NotSaved` by `InvestigationId`. That is unambiguous only because sessions exist for one working copy at a time; the fold should use `InvestigationRef`.

## Revision 3: the independent review (FAIL at f91bd7b), addressed

| Finding | Resolution |
|---|---|
| H1: a role was asserted, so run `demonstration-v1` could write into a catalog | `WorkspaceAddress` is now a private type inside a module (`WorkspaceAddress.T`); a type private at namespace level was still constructible from other files, as a probe showed. Constructors: `WorkspaceAddress.catalog`, and `WorkspaceAddress.workingCopy catalogs run`, which accepts only `workspaces/demo/causa-app-<run>` and refuses any workspace in `catalogs`. `WorkspaceLister.WorkingCopyFor` is removed. `ListingTarget.WorkingCopyOf` and `Msg.CreateRequested` take only derived values (`WorkspaceAddress.T`, `Writable.T`). A probe that forges `{ Dir; Kind = WorkingCopy }` from another file is refused with FS1093. Test: `Storage.Producer.WorkingCopyNeverNamesACatalogWorkspace`. |
| H2: `pending415` accepted other blanked approvals | It now compares the full sorted set of FAIL headers and problem lines with exactly the expected six, so 10 printed lines also fails. A copy with T-S59's approval blanked is refused (verified). |
| M1: the departure from ODR-R10-32 A | It is now its own request, ODR-V415-03 (staging), and the fallback is ODR-V415-04. `approve v4.1.5` refuses until both are recorded. README, AMENDMENT and ODR-V415-02 state that approval implies neither. |
| M2: the A4 weakening | Stated in ODR-V415-01's option A and in README and AMENDMENT as a **weakened** acceptance condition (incomplete, not dropped) |
| L1: proposals cited as if recorded | They are now cited as proposals and requests, never as records |
| L2: the DA-R10-1 claim | Corrected: DA-R10-1 refuses every ODR/OD id |
| Info: the start state of a new session | README "For builders" note |

## Revision 4: the re-review (PASS at 730e2d6, with two new findings)

| Finding | Resolution |
|---|---|
| N1: carried, owner-approved state-machine content could change with CI still green | A new CI step requires v4.1.5's `state-machine-amendments.json`, minus T-L03 and the ODR-V415-01 note, to equal v4.1.4's. ODR-V415-01 `applies` now pins the content digests of T-L03 and of the SM-SAVE note (canonical JSON without `approval`), and the step checks them. A copy with only T-S59's effects changed is refused. |
| N2: the run regex accepted a trailing newline | It is now anchored with `\z` |
| Info: what `approve` checks | FREEZE.md says it checks that the decisions are recorded, not which option was chosen |
