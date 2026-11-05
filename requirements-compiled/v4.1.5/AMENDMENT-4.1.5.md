# Amendment 4.1.5: real causa-data with the minimum contract change

v4.1.5 is the successor of the approved package v4.1.4 (APR-V414-OWNER-2, tag `indy-init-requirements-v4.1.4`). v4.1.4 is not edited.

| Decision | Defect | Change |
|---|---|---|
| ODR-V415-01 (E1) | The frozen 28-case `CoreEvent` cannot represent 487 of the 1,315 catalog events (`proposals/r10-historical-data-compatibility/SCHEMA-DIFF.md` §2). EXT-DATA-013 requires all four catalog cases to load. | `00-DOMAIN-STUBS.fs`: 50 cases. Every v4.1.4 name is kept, 9 are widened with the wire's fields, and 22 are added. `HypothesisFalsified` has no `Scope`, because the wire has none. `EventEnvelope.Provenance` is added. Payload types stay opaque and the domain owner's, with exactly the wire fields. `02-IF41-STORAGE.fs`: `VerificationFailure.ReaderStage` (CONTRACT §11.1). |
| ODR-V415-02 (W1, L1) | No contract can address a workspace or list one (`SCHEMA-DIFF.md` §5). EXT-DATA-003 needs an explicit selection, and EXT-DATA-013 needs the catalog's cases. | `Workspace` (02), `WorkspaceRole`/`WorkspaceAddress` (03), `WorkspaceLister`/`Listing` (05), `Msg.ListRequested`/`CreateRequested` and `Model.Listings`/`ListingsInFlight` (06). `PathOf`, `Verify`, `VerifiedInvestigation`, `Initial` and `LoadInput.OpenRequested` carry the workspace. |
| ODR-V415-03 (C1) | EXT-DATA-008 needs a command-created, round-trip-verified new investigation. SM-SAVE starts only from a verified stored history. | `SaveMachine.InitialNew` starts in `Unsaved`. `StorageCodec.EncodeNew` writes the first file set. There is one `accepted` note on SM-SAVE, and no state, event or transition is added. |
| ODR-V415-04 | — | Approval of this package |

## How the change is checked

- **X01–X21** pass (`v41 --check`).
- **X05 and X12:** no machine gains a state or an event. `LoadInput.OpenRequested` keeps its name and only its payload widens.
- **X17 baseline:** the five changed contracts are declared with their digests and requests. The other contracts equal v4.1.4's byte for byte.
- **The contracts typecheck** (`contracts/ContractCheck.fsproj`, `TreatWarningsAsErrors`). Both negative controls are still refused with FS1093.
- **The skeleton bootstraps** into an empty repository, and `Causa.slnx` builds.
- **Historical compatibility:** the `CoreEvent` case names equal those of `proposals/r10-historical-data-compatibility/src/SuccessorContract.fs` `CoreEventV415`. Checks C11/C15 prove that union lossless on all 1,315 catalog events.

Every other part of v4.1.4 is carried unchanged.
