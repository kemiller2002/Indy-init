# F# implementation patterns

These patterns are proposed. They are measured against what already exists: the frozen and candidate contracts in `requirements-compiled/v4.1.4|v4.1.5/contracts`, `state-machine-contracts.json`, `agent-file-ownership.json` and `analysis/project-structure.json`, and the contract-first UI proposal (`proposals/r10-contract-first-ui`). This file does not restate those. Where a contract already decides something, it points to the contract.

## 1. Typed feature contracts: already the architecture, keep it

The v4.1.x packages already give each capability its input, output, command, error and state types, in IF41-DOMAIN, -STORAGE, -WIRE, -ERRORS, -SAVE, -APP, -VIEW and -REPORT. Each contract is written into its producer's file at bootstrap (AGENT-RULES §1), and `ContractCheck.fsproj` plus `contracts/negative` prove that they compile. The contract-first UI proposal adds the requirement IDs, field IDs and validation rules for screens (`SCREEN-MANIFEST.json`, `Ids.fs`, `Bindings.fs`), and `ManifestTests` checks them against the catalog.

**Convention.** No new contract format. A contract is usable on its own when a consumer can compile against it with the producer's implementation absent. `00-DOMAIN-STUBS.fs` and the v4.1.5 fold already demonstrate this.

The history shows two gaps that a feature contract must close before release:
- **Real data.** It must represent every input the governing requirements name, and every event in the pinned data. Missing workspace (HF-069), missing command body (HF-034) and 487 unrepresentable events (HF-068) are the evidence; this is CV-11.
- **Contradictions.** It must not contradict another clause of the same package. INV-04 vs SM-SAVE is the evidence (HF-036); this is CV-14.

## 2. Errors: one policy (CV-10, MANDATORY)

| Failure | Representation | Example in the contracts |
|---|---|---|
| Expected validation failure | `Result<'T, ValidationError>` returned by a pure function | field validation in the UI contracts |
| Provider failure (HTTP, rate limit, conflict, size) | a typed case of `ProviderError`, classified once by the `Classifier` | `04-IF41-ERRORS.fs` `ProviderError`, `analysis/github-error-matrix.json` |
| Illegal (state, event) pair | fail closed with `SaveFault` (AGENT-RULES) | `05-IF41-SAVE.fs` |
| Unknown input (event kind, command body, variant) | refused with a typed error, or preserved as an opaque case when it is data | v4.1.5 E1 |
| Unexpected exception | caught only at the outermost boundary (transport, JS interop), converted to a typed fault, and shown | — |

Prohibited:
- `try ... with _ -> <default>`;
- a wildcard `| _ ->` branch on a command or event union in decision code (HF-041);
- a "step or default" helper that drops a fault (HF-022 `stepOr`);
- assigning machine state outside `step` (HF-008).

Enforced by:
- **CK-01** (new, `tools/conventions/conventions.fsx check`): `TreatWarningsAsErrors` stays on, and FS0025, FS0026 and FS0049 are never suppressed. Screen-contract drift fails to compile only while these warnings are errors (HF-073).
- **The existing state-machine tests.**
- **A review obligation** for wildcard branches. A grep cannot tell a justified exhaustive wildcard from a silent default, so no check is claimed for that.

## 3. State transitions

These are already decided by `state-machine-contracts.json`: every (state, event) pair is legal with its listed transitions or fails closed, and no state is added (AGENT-RULES). No further convention is needed. The only addition is CV-10's "only `step` changes state".

## 4. Module structure: DISCRETIONARY (CV-22, Tier 3)

The directive asked whether to impose `Contract.fs / Domain.fs / State.fs / View.fs / Tests.fs` per feature. The evidence says no:
- **No defect is attributed to layout.** No finding in R2–R9 traces a defect or a conflict to module layout. The R5 conflicts (HF-015) came from shared files with no owner, not from file granularity.
- **Layout is already fixed.** The frozen project structure and `agent-file-ownership.json` fix every file and its owner at bootstrap; "no agent adds a file to a project later". A per-feature split would mean a successor package, changing frozen structure.
- **Splitting can cost more than it saves.** The post-R10 contention analysis (HF-079) shows same-owner serialization on `Update.fs`, `Engine.fs`, `Views/Investigation.fs` and `Projections.fs`. Its modeled ownership split (FO-1) shortens full completion from T+769 to T+684 at 10-min CI. But that was deferred out of v4.1.5, and the effect is modeled, not measured. EXP-INDY-002 is the place to measure it.

So agents organise code inside their owned files as they choose. The one structural rule with evidence is size: a single view function must not render more than one screen region (CV-04). R5's `View.project` hit the WASM interpreter's per-method locals limit (HF-013).

## 5. Naming and identifiers

- **Use the manifest's identifiers.** Requirement IDs (`INV-0xx`), field IDs (`UIF-`), action IDs (`UIA-`) and region IDs (`UIR-`) come from `SCREEN-MANIFEST.json`, and they are the Limen binding and event names. F# parameter names are free; the mapping goes through `Ids.fs` (CV-23, Tier 2, adopted with CF-09 A).
- **Do not couple names to HTML.** Do not derive HTML ids from F# names.

## 6. Testability

- Domain functions are pure and are tested without a browser or provider. The domain lane's kernel tests in T12/T13 already require this.
- No interface is introduced only to allow mocking. Provider seams already exist: `Connector`, `Classifier` and the transport.
- A test bound to an obligation should be able to fail. CV-13 records that as a preferred practice, not a gate: per-slice mutation controls cost 10–15 min of critical path, and the modeled net is negative at central and conservative estimates.
