# Rehearsal 010 readiness: process corrections

**Verdict: NOT READY.**
- The engineering corrections are built, tested and demonstrated.
- Rehearsal 010 still needs owner actions that agents may not take (§8):
  - the decisions and approval of the successor package v4.1.3;
  - a Dokimos release with the `url-input` fix, registered and pinned;
  - the Rehearsal 010 toolchain lock.

Rehearsal 010 was not launched.

The root-cause analysis is [ROOT-CAUSE.md](ROOT-CAUSE.md). The new startup instructions are [STARTUP-INSTRUCTIONS.md](STARTUP-INSTRUCTIONS.md).

## 1. What failed in Rehearsal 009, in one table

| Failure | Correction | Where |
|---|---|---|
| The catalog was treated as the whole target | One authoritative scope manifest: 199 requirements (161 P0 + 38 catalog), 863 obligations, 35 work items, pinned by source hashes and closed by `scopeSha256` | `scope-manifest.json`, `tools/requirements-compiler/src/Scope.fs` |
| No work-item inventory; Praxis showed one mission item | `scope preflight` refuses an omitted, weakened, altered or unsourced requirement, an orphaned requirement and an obligation without a producer. `coverage` refuses a Praxis backlog missing any work item. `scope checkpoint` is written only when both pass. `scope order` refuses application code before it. | `Scope.fs`, `ScopeCommands.fs` |
| Implementation and verification were one status | Two independent axes per requirement, plus acceptance from the owner or the independent verifier only. `suite-blocked` changes verification only. | `src/Progress.fs` |
| No stop rule; compute saving became a stop reason | `stop-decision` permits only complete, owner-authorized, hard-blocker, environment-failure and the deadline. Every other request gets a machine-readable `CONTINUE_REQUIRED` with the remaining work and the next independent items. A retry budget parks repeated failures. | `src/Continuation.fs` |
| The plan's commands could not run (MSB1008 ×2, no webkit project, no browser install) | Successor package **v4.1.3** (CANDIDATE; ODR-V413-04/05/06 pending). New compiler check X21. `r9.fsx suite-preflight` and `suite-fixture`. The gate installs Chromium and WebKit and reads the package from the pinned lock. | `requirements-compiled/v4.1.3/`, `V41Package.fs`, `tools/r9/` |
| Dokimos `url-input` mismatch (INV-029 V1–V3 failed on a correct element) | `componentMatches` gains `url-input` (input of type url, text or none), with tests | kemiller2002/dokimos#67 |

## 2. The authoritative scope (deliverable 2)

`docs/rehearsal-010-readiness/scope-manifest.json`, schema `indy-init.approved-scope/1`, compiled by:

```
requirements-compiler scope compile --source . --package requirements-compiled/v4.1.3 --out docs/rehearsal-010-readiness/scope-manifest.json
```

| | |
|---|---|
| Requirements | 199: 161 P0 of the v4 plan, 38 of `CAT-CAUSA-INVESTIGATION` |
| Mandatory | 194: every P0, and the 33 catalog requirements with `required: true` |
| Obligations | 863: 613 of the plan, 250 of the catalog (815 mandatory) |
| Executable / record-only | 791 / 72 |
| Plan tests | 660 identities, linked to their obligations |
| Work items | 35: 27 DAG tasks (T10–T36), 6 generated `WI-GEN-*` for the P0 requirements no DAG task covers (AR, EXT-DATA, PF, UI, VER, WDL), 2 `WI-INV-*` for the catalog screens |
| Producers | the 11 plan suites, plus `DOKIMOS-REQUIREMENTS` for the catalog |
| Gate-level scope | 359 requirements without priority P0: in scope by ODR-V4-10 A (APR-V4-OWNER-1), judged by the acceptance gates, not excluded |
| Exclusions | none. INV-003 is conditional (`domain-profile-selected=true`). |
| Frozen pins | v4.1.3 package manifest, v4 verification plan `10ce16eb…`, v4.1.3 execution plan, catalog `19a03668…`, baseline `f86a3539…` |

**Reconciling the "660 unverified obligations".** They are not 660 tasks:
- There are 613 obligations and 660 test identities, because some obligations bind several tests.
- Each requirement is built once, in its work items.
- 72 obligations are record-only (reviews, process records, owner records, the independent verifier). They need an evidence producer, not code.
- Obligations bound in two suites (for example a test and a review record) are judged by both and implemented once.

The manifest records each obligation's kind and producers, and each requirement's work items.

## 3. Praxis work-item coverage (deliverable 3)

Before implementation, `scope checkpoint` must pass:
1. The manifest is re-derived from the frozen sources and compared. Refusal codes:
   - `MANDATORY_REQUIREMENT_MISSING`, `REQUIREMENT_MISSING`, `MANDATORY_WEAKENED`, `OBLIGATION_MISSING`, `REQUIREMENT_ALTERED`, `REQUIREMENT_UNSOURCED`;
   - `FROZEN_SOURCES_CHANGED`, `SCOPE_HASH_MISMATCH`;
   - `REQUIREMENT_ORPHANED`, `WORK_ITEM_UNDECLARED`, `OBLIGATION_WITHOUT_PRODUCER`, `PRODUCER_UNDECLARED`.
2. Every work item exists in the Praxis backlog (`[ID] …` title). Otherwise `WORK_ITEM_MISSING` is refused, with the exact `praxis add` command.
3. The checkpoint `indy-init.scope-checkpoint/1` records the scope hash. `scope order` refuses application code committed before it.

Demonstrated:
- Rehearsal 009's own queue gives 35 × `WORK_ITEM_MISSING` (`evidence/coverage/r9-queue-verdict.json`).
- A complete queue gives the checkpoint (`evidence/coverage/example-checkpoint.json`).

## 4. Progress model (deliverable 4) and early-stop validation (deliverable 5)

Rehearsal 009 replayed against the full scope (`evidence/stop-decision/`). The events record:
- every catalog requirement implemented;
- every catalog obligation passing, except INV-029 V1–V3 (failing) and INV-003 (not applicable);
- `SU-OWN-DOTNET`, `SU-DOTNET-LIVE` and `SU-BROWSER-WEBKIT` blocked;
- no P0 requirement started.

| Request at 02:25Z (115 min in) | Decision | Refusals |
|---|---|---|
| `complete`, claiming completion | CONTINUE_REQUIRED, 306 min remaining | SCOPE_INCOMPLETE (161 open), CATALOG_IS_NOT_SCOPE, COMPLETION_CLAIM_REFUSED |
| `hard-blocker`: the release gate (a verification blocker) | CONTINUE_REQUIRED | INDEPENDENT_WORK_REMAINS (T10, T11, T12, …; "a verification blocker never blocks implementation") |
| `conserve-compute` | CONTINUE_REQUIRED | NOT_A_STOP_CONDITION |
| `deadline` at 07:30:01Z | STOP_PERMITTED | none |
| `deadline`, claiming acceptance | STOP_PERMITTED, `acceptanceClaimPermitted: false` | ACCEPTANCE_NOT_ESTABLISHED |

Progress (`r9-progress.json`):
- 161 not-started;
- 36 verified;
- 1 verification-failed (INV-029);
- 1 verification-pending (INV-003, not applicable);
- 3 blocked suites.

No implementation state changed when the suites were blocked.

## 5. Release-gate repairs (deliverable 1)

**v4.1.3** is a successor of v4.1.2 (APR-V412-OWNER-3, tag `indy-init-requirements-v4.1.2`). v4.1.2 is not edited; CI still checks it against its tag. See `requirements-compiled/v4.1.3/AMENDMENT-4.1.3.md`.

| Suite | v4.1.2 | v4.1.3 (ODR-V413-04, option A, pending) |
|---|---|---|
| SU-OWN-DOTNET | `dotnet test tests/Causa.Contract.Tests tests/Causa.Domain.Tests tests/Causa.Persistence.Tests tests/Causa.App.Tests` (MSB1008) | `dotnet test Causa.slnx --filter "FullyQualifiedName~Causa.Contract.Tests.\|…Domain.Tests.\|…Persistence.Tests.\|…App.Tests."` |
| SU-DOTNET-LIVE | `dotnet test tests/Causa.Acceptance tests/Causa.Invariants --filter …` (MSB1008) | `dotnet test Causa.slnx --filter "FullyQualifiedName~Causa.Acceptance.Live.\|FullyQualifiedName~Causa.Invariants.Live."` |
| Browser suites | the `webkit` project unchecked | `suitePrerequisites` (ODR-V413-05): `r9.fsx suite-preflight` at T10 and before T0 |

Further changes:
- The compiler gains X21: every `dotnet test` runner names exactly one project or solution, and a replaced runner names its decision. It applies from 4.1.3 on, so v4.1.1 and v4.1.2 compile byte for byte as before.
- No requirement, obligation, binding, state machine or contract changes (X10, X17).
- v4.1.3 validation: X01–X21 PASS.

**The gate workflow** (`tools/r9/verification-gate.yml`):
- installs Chromium and WebKit (`npx playwright install --with-deps chromium webkit`);
- reads the package from the lock named in the governance record (`r9.fsx govern --lock …`), so Rehearsal 010 runs the v4.1.3 plan.

Rehearsal 009's record has no `toolchainLock`, so it reads v4.1.2 exactly as before.

**Executed before any timed run** (`evidence/suite-commands/`), on a deterministic fixture tree from `r9.fsx suite-fixture`: the plan's project, namespace and folder layout, one trivial test per suite, no application code.

| Check | Result |
|---|---|
| `suite-preflight`, v4.1.2 plan | REFUSED: SU-OWN-DOTNET and SU-DOTNET-LIVE (MSB1008). This reproduces Rehearsal 009. |
| `suite-preflight`, v4.1.3 plan, fixture without webkit | REFUSED: SU-BROWSER-WEBKIT (`Project(s) "webkit" not found`) |
| `suite-preflight`, v4.1.3 plan | PASS: all 8 executable suites list (4, 1, 1, 1, 2, 2, 2, 1 tests) |
| `run-suites`, v4.1.3 plan (this container) | .NET: 8/8 passed in 4 suites. Chromium: 4/4 passed in 3 suites. WebKit: not run here, because the container cannot install WebKit's host libraries (`browserType.launch: Executable doesn't exist`). |
| CI job `suite-commands` (`.github/workflows/preflight-tooling.yml`) | Repeats all of the above with `--with-deps chromium webkit` and fails unless every suite, WebKit included, passes. Recorded on the pull request that delivers this file. |

## 6. Regression tests (deliverable 6)

`tools/requirements-compiler/tests/ScopeTests.fs` runs in the offline suite (`dotnet run --project tools/requirements-compiler/tests -- --offline --source .`). Every case uses deterministic fixtures; none consumes a rehearsal.

| # | Adversarial case | Must be refused with |
|---|---|---|
| 1 | The catalog passes while P0 requirements remain | SCOPE_INCOMPLETE, CATALOG_IS_NOT_SCOPE |
| 2 | The release gate fails on an invalid command | implementation unchanged; NOT_A_STOP_CONDITION; INDEPENDENT_WORK_REMAINS |
| 3 | A required work item was not created in Praxis | WORK_ITEM_MISSING; R9's single mission item is refused for every item |
| 4 | Completion is declared with requirements missing | SCOPE_INCOMPLETE, COMPLETION_CLAIM_REFUSED |
| 5 | A stop with five hours remaining | NOT_A_STOP_CONDITION (300 min), DEADLINE_NOT_REACHED |
| 6 | Implemented, but verification blocked | status `verification-blocked`, not verified; completion and acceptance refused |
| 7 | The manifest silently omits a mandatory requirement (rehashed) | MANDATORY_REQUIREMENT_MISSING; an edit without rehashing: SCOPE_HASH_MISMATCH |
| 8 | The scope changes without owner approval | SCOPE_CHANGE_UNAPPROVED (and accepted with a fixture owner record) |

**Also covered:**
- The legitimate stops: complete, the deadline (with an invented acceptance claim refused), owner-authorized (an agent-authored "approval" is ignored), environment failure (a broken gate is not one), and a documented hard blocker.
- The retry budget, the agent's own acceptance (refused), progress events that would invent progress (refused), and scope order.
- The real scope: the committed manifest equals its compilation; 161 + 38 requirements are present; no orphan; every obligation has a producer; the counts.
- The Rehearsal 009 replay of §4.
- X21: v4.1.3 passes; v4.1.2's runners, an unknown suite and an undecided runner fail; frozen v4.1.2 gains no check.
- `v41 --check` on v4.1.3.

## 7. Phase 8 validation

| Claim | Evidence |
|---|---|
| Every mandatory requirement is represented | `real` test: all 161 P0 and 38 catalog ids are in the manifest; `scope preflight` PASS |
| Every implementation work item is traceable | each requirement names its work items; each work item names its requirements (gate-level ones too); `REQUIREMENT_ORPHANED` never fires on the real scope |
| The release gate executes | `suite-preflight` PASS and `run-suites` on v4.1.3. WebKit runs in CI job `suite-commands`. |
| Implementation and verification states are independent | cases 2 and 6; `r9-progress.json` |
| Completion cannot be claimed after only the catalog | case 1; R9 replay `r9-decision-complete.json` |
| A blocked release gate does not prevent unrelated implementation | case 2; `r9-decision-gate-blocker.json` (`nextIndependentWork`) |
| An early stop with unfinished work gets a machine-readable refusal | `indy-init.stop-decision/1` files in `evidence/stop-decision/` |

## 8. Remaining blockers (owner actions)

| # | Blocker | Who | How |
|---|---|---|---|
| 1 | ODR-V413-04 and ODR-V413-05 are not recorded | owner | `owner-freeze.fsx decide v4.1.3`; then an agent recompiles v4.1.3 and records its independent review (FREEZE.md step 2) |
| 2 | v4.1.3 is not approved or tagged (ODR-V413-06) | owner | `owner-freeze.fsx approve v4.1.3`, then `tag v4.1.3`; an agent adds it to the frozen guard |
| 3 | Dokimos `url-input` fix is not released | Dokimos owners, then owner | Merge kemiller2002/dokimos#67. Release Dokimos. Register it in a new echelon-current set (owner approval, Conditor qualification). Without it, INV-029 (non-mandatory) still fails V1–V3. |
| 4 | No Rehearsal 010 lock | owner | Adopt `kickoff/rehearsal-010/toolchain.lock.proposed.json` as `toolchain.lock.json` after 1–3 (package path, manifest digest, resolved set). The scope manifest's `scopeSha256` is recompiled if v4.1.3 changes. |
| 5 | The launch prompt | owner | Use the prompt in STARTUP-INSTRUCTIONS.md, with no narrower target and no compute-saving instruction (ROOT-CAUSE H1, H2) |

**Not blockers, but known limits:**
- Progress events are written by the implementing agent. `stop-decision` refuses inconsistent events and agent acceptance, but it cannot detect an event that claims a passing verification which never ran. The release gate and the independent verifier remain the authority on verification.
- The generated `WI-GEN-*` items group requirements by family. They depend on T17 only, so their finer order is the agent's to plan.
- Ordo was not changed. No Ordo capability was needed for these checks.
- Praxis was not changed. Coverage reads Praxis's own backlog file; work items are created with `praxis add`.
