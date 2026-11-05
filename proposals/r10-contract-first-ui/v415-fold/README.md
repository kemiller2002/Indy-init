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

## What the compilation owner still needs

These are not done here, because they live in the shared v4.1.5 package:

1. **An `interface-change` request for IF41-VIEW** in v4.1.5's own `proposals/owner-decision-requests.json`, recorded by the owner before the compile (`V41Package.fs:1774-1787`). Suggested wording: "IF41-VIEW gains the presentation contracts `Causa.Ui.Contracts` (CF-09 A): additive only; no existing IF41 type changes." The amendment's `contractChanges` declares the same change.
2. **`analysis/interface-specifications.json`:** IF41-VIEW's description and consumers gain the UI roles.
3. **`analysis/project-structure.json`:**
   - `Projections.fs` (implements IF41-VIEW) becomes integration-owned, frozen types only, and compiles before `Connection.fs` (`../FILE-OWNERSHIP.json` `compileOrder`, review R1);
   - the UI ownership split follows CF-10 A and ODR-R10-20 (`../FILE-OWNERSHIP.json`).
4. **The bootstrap** writes the fixture backend (`../fixtures/`) to `tests/Causa.TestSupport/UiFixtures/` (CF-09 A, OD-INDY-22: fixtures enter a run only as frozen-package content).
5. **STARTUP-INSTRUCTIONS §4:** a UI slice may start once bootstrap is integrated and its UI predecessors are built. Merging is unchanged (CF-09 A extends ODR-R10-31).

## Other recorded decisions this fold already carries

| Decision | Where it shows |
|---|---|
| CF-03 A | `ContentForms.hypothesisOnRecord` |
| CF-05 A | `Navigation.format`/`parse`: `#/administration` and `?tab=` |
| CF-12 A | the contracts' own `Rule`/`Validation` |

CF-06 A (Reduce motion, device scope) is the fixture's preference. CF-11 A is a theme token and CF-08 A a markup layout; both belong to the package's ownership and bootstrap, not to contract types. CF-02 A needs successor catalog entries for "Record evidence", "Reassess" and the two link choosers (catalog ratification).
