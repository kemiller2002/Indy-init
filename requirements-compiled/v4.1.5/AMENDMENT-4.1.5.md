# Amendment 4.1.5: real causa-data with the minimum contract change

v4.1.5 is the successor of the approved package v4.1.4 (APR-V414-OWNER-2, tag `indy-init-requirements-v4.1.4`). v4.1.4 is not edited.

| Part of ODR-V415-01 | Defect | Change |
|---|---|---|
| E1 | The frozen 28-case `CoreEvent` cannot represent 487 of the 1,315 catalog events (`proposals/r10-historical-data-compatibility/SCHEMA-DIFF.md` §2). EXT-DATA-013 needs all four catalog cases. | `00-DOMAIN-STUBS.fs`: <ul><li>50 cases: every v4.1.4 name kept, 9 widened with the wire's fields, 22 added.</li><li>`HypothesisFalsified` has no `Scope`, because the wire has none (see "Known conflict").</li><li>`EventEnvelope.Provenance`.</li><li>`MarkHypothesisPlausible`/`Supported of MarkHypothesis` and `RejectProposal of ProposalId * reason` carry what their events need.</li><li>Payload types stay opaque and the domain owner's, with exactly the wire fields.</li></ul>`02-IF41-STORAGE.fs`: `VerificationFailure.ReaderStage` (CONTRACT §11.1). |
| W1 | No contract can address a workspace (`SCHEMA-DIFF.md` §5). The same case is stored in two workspaces. EXT-DATA-003 needs an explicit selection; EXT-DATA-006 and -013 say a catalog is read-only. | `02`: <ul><li>`Workspace`, `WorkspaceRole`, `WorkspaceAddress`. Both roles are in the connected repository, whose permissions Connect checks.</li><li>`InvestigationRef`.</li><li>`Writable`, whose only constructor refuses a catalog.</li><li>`PathOf`, `Verify` and `VerifiedInvestigation` carry the workspace.</li></ul>`06`: loads, sessions, timers and machine messages are keyed by `InvestigationRef`. |
| L1 | No contract lists a workspace. | `05`: `ListingTarget` (`ActiveCatalog` derives the catalog workspace from `catalog/active.json`; `WorkingCopyOf`), `ListingStep`, `Listing`, `ListingInterpretation`, and `WorkspaceLister { WorkingCopyFor; List; InterpretList }`. `06`: `Msg.ListRequested`, `Model.Listings`, `ListingsInFlight`. |
| C1 | EXT-DATA-008 needs a command-created, round-trip-verified new investigation. SM-SAVE starts only from a verified stored history. A catalog load created a writable session (T-L03). | `05`: `SaveMachine.InitialNew`, holding the StartInvestigation command; it starts in Unsaved, or Offline when known offline. `02`: `StorageCodec.EncodeNew`, which returns Ok only for a file set that Verify accepts. `06`: `Msg.CreateRequested`. **T-L03 replaced:** only a working-copy load creates a save session. **SM-SAVE accepted note:** a new session starts there. No state, event or transition is added. |
| ODR-V415-02 | — | Approval of this package |

## Known conflict

Scenario A4 requires the falsification scope to be recorded. causa-storage v1.0.0 has no scope on a hypothesis transition. Scope can be recorded as `NegativeKnowledgeEstablished` (its `scope` field). Scope on the falsification event itself waits for causa-storage 1.1.0 (ODR-R10-26 A, OD-HC-06 B). It stays incomplete; it is not dropped.

## How the change is checked

- **X01–X21** pass except X12 and X17, which fail only with "names no operator approval" for the six approvals ODR-V415-01 will supply. CI asserts exactly that (`pending415`) until FREEZE step 2.
- **X05 and X12:** no machine gains a state or an event. `LoadInput.OpenRequested` keeps its name and only its payload changes.
- **X17:** four contracts are declared changed with their digests. `Writable.ofAddress` and `Writable.address` are allowlisted boundary code, pinned by digest. Every other contract equals v4.1.4's byte for byte.
- **The contracts typecheck** with `TreatWarningsAsErrors`. Both negative controls are still refused with FS1093. The skeleton bootstraps, and `Causa.slnx` builds.
- **New producer tests:**
  - IF41-STORAGE: `EveryCatalogEventRoundTrips`, `EncodeNewVerifiesAndRoundTrips`, `WritableRefusesCatalog`;
  - IF41-SAVE: `NewInvestigationSavesThroughExistingTransitions`, `CatalogLoadCreatesNoSession`, `ListingReadsAtOneCommit`, `EveryListingStepIsInterpreted`;
  - IF41-APP: `StateIsKeyedByWorkspaceAndId`.
- **Historical compatibility:** the `CoreEvent` case names equal `CoreEventV415` in `proposals/r10-historical-data-compatibility/src/SuccessorContract.fs`, which checks C11/C15 prove lossless on all 1,315 catalog events.

Every other part of v4.1.4 is carried unchanged.
