# CF-09 A in v4.1.5: the UI contracts folded into IF41-VIEW

**Decision being applied:** CF-09 = A (APR-CFUI-OWNER-1, on `main`): ship the UI contract package in the combined v4.1.5 successor (ODR-R10-32).

**Why a fold.** The v41 compiler refuses a contract file that is not in the reviewed baseline (`V41Package.fs:1790`). Adding `Causa.Ui.Contracts` as twelve new files would therefore need a compiler change. Folding them into the existing `07-IF41-VIEW.fs` needs none.

## What is here

[`07-IF41-VIEW.folded.fs`](07-IF41-VIEW.folded.fs) is the frozen v4.1.4 `07-IF41-VIEW.fs`, byte-identical in its first 40 lines, followed by the twelve files of `proposals/r10-contract-first-ui/contracts/`.
- The contract files are concatenated in their compile order.
- They sit in one `namespace Causa.Ui.Contracts` block.
- They are otherwise unchanged.

Nothing in `requirements-compiled/` is edited. The v4.1.5 compilation owner takes this file into the package.

## Verified (2026-11-05)

| Check | Result |
|---|---|
| `ContractCheck.fsproj` of v4.1.4 with this file as `07-IF41-VIEW.fs` (built outside the checkout, as preflight-tooling.yml does) | Build succeeded, 0 warnings |
| Negative controls `negative/ForgeConfirmation.fs`, `negative/ReadCredential.fs` | both still refused with FS1093 |
| The same twelve files inside the real v4.1.4 bootstrap, after `Projections.fs` | builds (`../evidence/bootstrap-compile.log`) |

## Inputs for the v4.1.5 compilation owner

Requested by the historical-compatibility session. Each file is standalone; nothing in `requirements-compiled/` is edited.

| File | What it is |
|---|---|
| [`07-IF41-VIEW.folded.fs`](07-IF41-VIEW.folded.fs) and [`.sha256.json`](07-IF41-VIEW.folded.sha256.json) | the folded contract, sha256 `6328f3ce3786e952178c22d42601d43f9833a5bdfb0b942b1d42ca54d2fd46ed` |
| [`interface-change-request.IF41-VIEW.json`](interface-change-request.IF41-VIEW.json) | one `interface-change` request (`ODR-V415-IF-VIEW`) in the owner-decision-requests format, pinning the folded file. The owner records it before the compile (`V41Package.fs:1774-1787`) |
| [`interface-specifications.IF41-VIEW.json`](interface-specifications.IF41-VIEW.json) | the IF41-VIEW object only. Its `fsharp` equals the folded file. Producer is integration; consumers are ui, ui-2, ui-3 and integration; there are four added operations and the producer and consumer tests |
| [`project-structure.delta.json`](project-structure.delta.json) | changes against v4.1.4's `project-structure.json`: the three UI roles (CF-10 A), the Causa.App compile order, 14 changed, 18 added and 2 split files, the UI fixture test helpers, the per-screen fragments (CF-08 A) and the rule changes |
| [`bootstrap-fixtures.json`](bootstrap-fixtures.json) | the fixture files, what each asserts, and the generator command at causa-data `e8259df` |
| [`start-rule.md`](start-rule.md) | the exact sentence for STARTUP-INSTRUCTIONS §4 step 1 |

**The bootstrap places the fold in `Projections.fs`.**
- The bootstrap writes each interface's `fsharp` into its implementing file, and IF41-VIEW's is `src/Causa.App/Projections.fs`. So the UI contracts need **no separate `Ui/Contracts/*.fs` files**. This supersedes those entries in `../FILE-OWNERSHIP.json`.
- `Projections.fs` becomes integration-owned and compiles before `Connection.fs`.
- Verified in the real v4.1.4 bootstrap, with `Projections.fs` holding the folded text and the prototype screens after `Connection.fs`: builds with 0 warnings (`../evidence/bootstrap-compile.log`).

**Frozen names the folded types use** (for joint review with the event-union change):
- IF41-APP `ConnectionState`;
- IF41-VIEW `SaveAction` and `SaveIndicatorView`;
- IF41-SAVE/WIRE `Politeness`, `Credential.T` and `RequestId`;
- domain stubs `InvestigationId`, `AggregateVersion` and `EventId`.

They use no `Msg`, no `Model`, and no case of the event or command unions. Only the adapter sketch, which is not part of the contract, uses `Msg`, `Model` and `Engine`.

## Other recorded decisions this fold already carries

| Decision | Where it shows |
|---|---|
| CF-03 A | `ContentForms.hypothesisOnRecord` |
| CF-05 A | `Navigation.format`/`parse`: `#/administration` and `?tab=` |
| CF-12 A | the contracts' own `Rule`/`Validation` |

CF-06 A (Reduce motion, device scope) is the fixture's preference. CF-11 A is a theme token and CF-08 A a markup layout; both belong to the package's ownership and bootstrap, not to contract types. CF-02 A needs successor catalog entries for "Record evidence", "Reassess" and the two link choosers (catalog ratification).
