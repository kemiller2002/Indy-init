# v4.1.5 minimum: design note

**Status: PROPOSED.** This is input to the compile in workstream D, not a frozen package. The contract text is `v415-contracts/`. The diff against frozen v4.1.4 is `v415-contracts/v4.1.4-to-v4.1.5.diff`.

**Checked so far**
- `dotnet build v415-contracts/ContractCheck.fsproj`: 0 warnings, 0 errors. `TreatWarningsAsErrors` is on, as in the package.
- The `CoreEvent` case names equal the 50 cases of `proposals/r10-historical-data-compatibility/src/SuccessorContract.fs`.
  - That union is the one checks C11/C15 proved: all 1,315 catalog events map into it and back to their exact stored bytes.
  - Every v4.1.4 case name is kept.

## Changed contract files

Five files change. Every other file stays byte-identical, so the compiler's baseline check holds.

| File | Change | Requirement it serves |
|---|---|---|
| `00-DOMAIN-STUBS.fs` | **E1.** `CoreEvent` goes from 28 to 50 cases: 9 widened with the wire's extra fields, 22 added. `HypothesisFalsified` drops `Scope`, which the wire does not have. `EventEnvelope` gains `Provenance`. The payload types stay opaque and the domain owner's to define, as in v4.1.2, with exactly their wire fields. | EXT-DATA-011, -013; ODR-R10-25 A; OD-HC-01 A |
| `02-IF41-STORAGE.fs` | **W1:** `Workspace`; `PathOf` takes it; `VerifiedInvestigation` records it; `Verify` takes it. **E1:** `VerificationFailure.ReaderStage` carries the §11.1 primary code. **C1:** `StorageCodec.EncodeNew` writes a new investigation's first file set. | EXT-DATA-003, -005, -008; CONTRACT §9.4, §11.1 |
| `03-IF41-WIRE.fs` | **W1:** `WorkspaceRole` (`Catalog`, which is read-only, or `WorkingCopy`) and `WorkspaceAddress` (repository + workspace + role). | EXT-DATA-006, -013 |
| `05-IF41-SAVE.fs` | **L1:** `ListedInvestigation`, `Listing`, `ListingInterpretation`, and `WorkspaceLister { List; InterpretList }`. A listing is a read, not a machine. **C1:** `SaveMachine.InitialNew`; `Initial` takes the address; `InvestigationSession.Address`; `LoadInput.OpenRequested` carries the address. | EXT-DATA-008, -013; PERSIST-005..007 |
| `06-IF41-APP.fs` | `Msg.ListRequested` (the user selects a derived workspace), `Msg.CreateRequested` (creation is explicit), and `Model.Listings` / `ListingsInFlight`. | EXT-DATA-003, -006 |

## Why the change fits the frozen rules

- **No new state, event or contract file.**
  - X05 compares the state and input *case names* with v4's machines.
  - `LoadInput.OpenRequested` keeps its name; only its payload widens.
  - No SM-SAVE, SM-LOAD, SM-CONNECTION or SM-NETWORK case is added.
  - The listing types are plain reads, not a declared machine.
- **Creation reuses SM-SAVE.**
  - `InitialNew` starts in `Unsaved`, with the head just read as `BaseHead`.
  - The first `Edit` is `StartInvestigation`.
  - `SaveRequested` → `Saving` → a commit adding `EncodeNew`'s three files, conditional on `BaseHead` → `Saved (Confirmed)`, only from an equal read-back.
  - A moved head is the existing `StaleHeadUnrelated` rebase path.
  - The one rule that changes: SM-SAVE's declared `initial` is `Saved`, and a new investigation's session begins in `Unsaved`. That needs one `state-machine-amendment` request (C1-SM) with an `accepted` note. It adds no transition.
- **Writes only to a working copy.** A session's `Address.Role` is always `WorkingCopy`. Saving in a catalog is a fail-closed `SaveFault`, an illegal pair.
- **The workspace is derived (OD-V415-05 A).**
  - Derivation:
    - catalog workspace: the `workspace` field of `catalog/active.json`;
    - working copy: `workspaces/demo/<run>` in the connected repository.
  - The user selects among them (`ListRequested`), which satisfies EXT-DATA-003's "explicitly select". The J1 Connect form is unchanged.

## Requests the compile needs

All are in `requirements-compiled/v4.1.5/proposals/owner-decision-requests.json`:

| Request | Kind | Contracts |
|---|---|---|
| ODR-V415-01 (E1) | interface-change | 00, 02 |
| ODR-V415-02 (W1+L1) | interface-change | 02, 03, 05, 06 |
| ODR-V415-03 (C1-SM) | state-machine-amendment | 05 |
| ODR-V415-04 | package approval (O2) | — |
| ODR-V415-IF-VIEW (CF-09 A, Agent 2) | interface-change | 07 |

`contractChanges` declares each file once. For a file changed by several requests, it names the request that changes it most and lists the others in its summary.

## Found while checking the fold, for the lead and Agent 2

Agent 2's `project-structure.delta.json` replaces roles `ui` and `ui-2` with `ui-a`, `ui-b` and `ui-c`. **The v4.1.x compiler cannot accept that without a code change:**
- Test-file owners still come from v4's tasks and laneOverrides (`T26: "ui-2"`) and from hard-coded `"ui"` (`V41Package.fs:236, 252, 258, 297`).
- X01 then fails with "owner 'ui' is not a declared role" for 58 + 7 test files.
- X18 needs `sharedEntrypoints.changeRequests.requesters` to be declared roles.
- The `ruleLinesSha256` of `AGENT-RULES.md` changes.

The IF41-VIEW contract text itself (`07-IF41-VIEW.fs`) folds without a compiler change. Two options:
- (a) Keep the role names `ui`/`ui-2` and add `ui-3`, with ownership by file.
- (b) A reviewed compiler change mapping the v4 lanes to the new roles.

(a) avoids touching the compiler and keeps the core off that path.
