# The minimum coherent successor

The smallest v4.1.5 that supports the six capabilities, within the v4.1.x successor rules (`COMPILATION-BOTTLENECKS.md` §3), without removing any requirement or weakening any acceptance condition.

## 1. Capability by capability

| # | Capability | Needs in v4.1.5 | Already exists | Gap closed by |
|---|---|---|---|---|
| 1 | Connect to causa-data | **Workspace addressing**: `Repository` + `Workspace`; the catalog workspace named by `catalog/active.json` | `Connector`, `ConnectResult` (frozen); a read-only token (ODR-R10-28 A) | W1: workspace contract |
| 2 | List historical investigations | **A listing operation**: a request and interpret pair beside `Connector`, reading `catalog/active.json` and the catalog manifest, with no new state-machine state | reference `DocumentSource.ListInvestigations`; C16 loads all four cases | L1: listing contract |
| 3 | Display actual investigation data | **Lossless decode plus the §8.3 projection, labelled "admissibility not evaluated"** (OD-HC-04 A) | decoder and projection verified byte for byte (C11, C12, C16) | E1: event union, envelope, `ReaderStage` (item 1) |
| 4 | Create a new investigation in a writable workspace | **A creation path that adds no state or event**: `SaveMachine.InitialNew`, starting in `Unsaved`; the first save is a `createCommitOnBranch` adding three files under the working-copy workspace prefix, with `expectedHeadOid`; plus transitions under `state-machine-amendment` decisions | EXT-DATA-008, PERSIST-006/-007; the save commit mechanics | C1: creation contract and transitions |
| 5 | Save and reload it | nothing new beyond C1: save and reload are the frozen SM-SAVE/SM-LOAD paths, once a created investigation has a `VerifiedInvestigation` after its first confirmed save | SM-SAVE, SM-LOAD, `Confirmation.ofReadBack` | — |
| 6 | Begin UI and domain work independently through stable F# contracts | E1, W1, L1 and C1 frozen. The UI contracts are **not** required for this, because the frozen IF41-APP/IF41-VIEW already give the UI its interface; contract-first interfaces speed it up. | IF41-APP, IF41-VIEW (frozen) | — |

**The minimum v4.1.5 = E1 + W1 + L1 + C1**, plus their requests, transitions, review and freeze.

The following are **excluded** from the minimum without weakening anything. Each stays in the complete package:

- FO-1..FO-3 ownership rebalancing (ODR-R10-20);
- the T19/T22 split (ODR-R10-21);
- the contract-first UI interfaces (CF-09, not recorded);
- the IF41-APP split (FO-2).

**One exception to "nothing weakened" needs owner confirmation.** ODR-R10-32 A records "one combined package". Shipping the minimum first and the rest as v4.1.6 departs from that record. Doing it requires the owner to re-decide ODR-R10-32 (`OWNER-DECISIONS.md` OD-V415-01). It does not lower any acceptance condition.

## 2. Raw display is not authoritative replay

| Level | What the minimum provides | What it does not |
|---|---|---|
| Raw JSON display | yes, but not needed: the minimum shows the projection | — |
| Lossless typed decode (Level B) | yes (E1) | — |
| Projection equal to the engine snapshot and the catalog digest (Level C-projection) | yes, labelled "admissibility not evaluated" (OD-HC-04 A) | — |
| **Authoritative replay and analysis (Level C)** | **no** | see §3 |

## 3. Additional contracts needed for authoritative replay and analysis

These are not in the minimum. They are domain work, which the rehearsal does inside its window under OD-INDY-22:

1. **A requirements-derived `History.validate`.** It refuses all 35 `CAUSA.HISTORY_INADMISSIBLE` negative fixtures with their domain codes (OD-HC-03 A, check C07 as its acceptance test). Its signature already fits IF41-DOMAIN `Replay` (`01-IF41-DOMAIN.fs:18`). **No new contract is required, only an implementation.**
2. **A `Decide` for every command the 31 events come from**, so that a new investigation's commands produce admissible events (IF41-DOMAIN `Decide`, frozen signature). This is also implementation only, but it needs the E1 event union.
3. **Capability projection for analysis views** (`InvestigationProjection.Capabilities`, frozen). Implementation only.
4. **Content with no v1.0.0 wire form** (INV-023, -025, -027, -029, -030 update, -034, -035): **causa-storage 1.1.0** (ODR-R10-26 A, OD-HC-06 B). This is a causa-data contract change owned outside Indy-init. Until then, these stay descoped for historical views (OD-HC-06 B). They are not weakened; they cannot be satisfied in v1.0.0.

## 4. If the minimum cannot satisfy the rules

| Risk | Rule | Governed alternative |
|---|---|---|
| Creation needs a new SM-SAVE state or event | X05/X12: "no state or event is added" (`V41Package.fs:189, 359, 2056`) | (a) Design creation in existing states (above), the preferred route. (b) A v4 successor (`requirements-compiled/v5`), which changes the frozen v4 state machines through a full freeze cycle: not achievable before Nov 7. (c) A reviewed compiler change allowing a state addition under a new decision kind. It would need its own review and an owner decision, because it relaxes X05/X12 for v4.1.x. **Not recommended:** the request said not to compromise X01–X21. |
| The UI contracts are wanted in the minimum | new contract files are refused (`V41Package.fs:1790`) | Fold them into `06-IF41-APP.fs` / `07-IF41-VIEW.fs` as declared `contractChanges`, or add a compiler extension with tests and its own review. |
| ODR-R10-21 is wanted in the minimum | task adjustments name only v4 tasks (`V41Package.fs:778`) | Split at Praxis work-item level in the scope successor (ODR-R10-19 machinery), or extend the compiler. |
