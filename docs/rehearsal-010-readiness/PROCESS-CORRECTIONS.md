# Rehearsal 010 readiness: process corrections

> **Integrated (2026-11-04, #104).** This is #102's record and is kept as written. In the integrated tree, v4.1.3 is replaced by #101's v4.1.4. The scope manifest is recompiled from v4.1.4 with the same 199 requirements, 863 obligations and 35 work items; its scopeSha256 is now `b407b5cc…`. The current readiness state is [FINAL-READINESS.md](FINAL-READINESS.md).
>
> **Re-pinned (2026-11-06, #155).** The scope is now compiled from the frozen v4.1.5 (tag `indy-init-requirements-v4.1.5`), with the same 205 requirements and 37 work items; its scopeSha256 is `26e6ff3c…`. The command in §2 names v4.1.5. The rest of this record describes v4.1.4 as it stood then.

**Verdict: NOT READY.**
- The engineering corrections are built, tested and demonstrated.
- Rehearsal 010 still needs owner actions that agents may not take (§8):
  - the decisions and approval of the successor package v4.1.4 (from #101);
  - a Dokimos release with the `url-input` fix, registered and pinned;
  - the Rehearsal 010 toolchain lock.

Rehearsal 010 was not launched.

The root-cause analysis is [ROOT-CAUSE.md](ROOT-CAUSE.md). The new startup instructions are [STARTUP-INSTRUCTIONS.md](STARTUP-INSTRUCTIONS.md). How #101 and #102 were combined, and the combined test results, are in [INTEGRATION-REPORT.md](INTEGRATION-REPORT.md).

## 1. What failed in Rehearsal 009, in one table

| Failure | Correction | Where |
|---|---|---|
| The catalog was treated as the whole target | One authoritative scope manifest: 199 requirements (161 P0 + 38 catalog), 863 obligations, 36 work items, pinned by source hashes and closed by `scopeSha256`; #101's obligation inventory is checked against it | `scope-manifest.json`, `tools/requirements-compiler/src/Scope.fs` |
| No work-item inventory; Praxis showed one mission item | `scope preflight` refuses an omitted, weakened, altered or unsourced requirement, an orphaned requirement and an obligation without a producer. `coverage` refuses a Praxis record missing any work item. `scope checkpoint` is written only when both pass. `scope work-plan` generates #101's work plan from the manifest. #101's `r9.fsx check-sequencing --scope-manifest` refuses application code before the scope checkpoint. | `Scope.fs`, `ScopeCommands.fs` |
| Implementation and verification were one status | Two independent axes per requirement, plus acceptance from the owner or the independent verifier only. `suite-blocked` changes verification only. | `src/Progress.fs` |
| No stop rule; compute saving became a stop reason | `stop-decision` permits only complete, owner-authorized, hard-blocker, environment-failure and the deadline. Every other request gets a machine-readable `CONTINUE_REQUIRED` with the remaining work and the next independent items. A retry budget parks repeated failures. | `src/Continuation.fs` |
| The plan's commands could not run (MSB1008 ×2, no webkit project, no browser install) | Successor package **v4.1.4** from #101 (CANDIDATE; its decisions are pending), its X21 and `suite-preflight`, and its bootstrap `playwright.config.ts`. Ported from #102: `suite-preflight --require-tests`, `suite-fixture`, `--plan`, and the gate reading the package from the lock named at govern time. | `requirements-compiled/v4.1.4/`, `V41Package.fs`, `tools/r9/` |
| Dokimos `url-input` mismatch (INV-029 V1–V3 failed on a correct element) | `componentMatches` gains `url-input` (input of type url, text or none), with tests | kemiller2002/dokimos#67 (merged); #68 closed as its duplicate |

## 2. The authoritative scope (deliverable 2)

`docs/rehearsal-010-readiness/scope-manifest.json`, schema `indy-init.approved-scope/1`, compiled by:

```
requirements-compiler scope compile --source . --package requirements-compiled/v4.1.5 --out docs/rehearsal-010-readiness/scope-manifest.json
```

| | |
|---|---|
| Requirements | 199: 161 P0 of the v4 plan, 38 of `CAT-CAUSA-INVESTIGATION` |
| Mandatory | 194: every P0, and the 33 catalog requirements with `required: true` |
| Obligations | 863: 613 of the plan, 250 of the catalog (815 mandatory) |
| Executable / record-only | 791 / 72 |
| Plan tests | 660 identities, linked to their obligations |
| Work items | 36: 27 DAG tasks (T10–T36); 6 generated `WI-GEN-*` implementation items for the executable P0 requirements no DAG task covers (AR, EXT-DATA, PF, UI, VER, WDL); `WI-GEN-RECORDS`, the evidence item for the 16 record-only ones; 2 `WI-INV-*` for the catalog screens |
| Producers | the 11 plan suites, plus `DOKIMOS-REQUIREMENTS` for the catalog |
| Gate-level scope | 359 requirements without priority P0: in scope by ODR-V4-10 A (APR-V4-OWNER-1), judged by the acceptance gates, not excluded |
| Exclusions | none. INV-003 is conditional (`domain-profile-selected=true`). |
| Frozen pins | v4.1.4 package manifest, v4 verification plan `10ce16eb…`, v4.1.4 execution plan, catalog `19a03668…`, baseline `f86a3539…` |

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
2. Every work item exists in Praxis: a backlog item titled `[ID] …` or with that id, or a work item begun with `praxis work begin`. Otherwise `WORK_ITEM_MISSING` is refused, with the exact `praxis add` command.
3. The checkpoint `indy-init.scope-checkpoint/1` records the scope hash. `r9.fsx check-sequencing --scope-manifest` (#101's CI-facing command, run by the gate whenever the lock pins a scope) refuses application code committed before it, a scope hash that differs from the approved manifest, an edited checkpoint, and incomplete Praxis coverage at the checkpoint commit.

Demonstrated:
- Rehearsal 009's own queue gives 36 × `WORK_ITEM_MISSING` (`evidence/coverage/r9-queue-verdict.json`).
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

The owner chose #101's successor package **v4.1.4** (CANDIDATE) over #102's v4.1.3, which is withdrawn. It corrects the six runners (MSB1008 and the `--config playwright.config.ts` the bootstrap now writes), adds X21, and adds `r9.fsx suite-preflight`. It was validated against the real Rehearsal 009 application. v4.1.2 is unchanged and still checked against its tag. See #101's `docs/release-gate-repair/` and `requirements-compiled/v4.1.4/`.

**Ported from #102:**
- `suite-preflight --require-tests true`: a command that executes but selects no test is refused. A filtered `dotnet test` exits 0 when nothing matches.
- `suite-fixture`: the deterministic test tree (the plan's layout, one trivial test per suite, no application code).
- `--plan`: run a package's own commands before T0 without Dokimos.
- The gate reads the package and lock named at govern time (`r9.fsx govern --lock`). #101's gate compiled the v4.1.2 plan with the Rehearsal 009 lock hardcoded.

**Executed:**

| Check | Where |
|---|---|
| v4.1.2 refused on the fixture (MSB1008 ×2); a fixture without webkit refused; v4.1.4 lists a test in every suite | #101's harness cases `suite-preflight-fixture-*`, `suite-preflight-require-tests-*` |
| every v4.1.4 suite runs on the fixture, WebKit included | CI job `suite-commands` |
| v4.1.4 reproduced byte for byte, X01–X21 PASS | CI job `release-gate-repair`; #101's harness |

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
- The retry budget, the agent's own acceptance (refused), progress events that would invent progress (refused).
- The real scope: the committed manifest equals its compilation; 161 + 38 requirements are present; no orphan; every obligation has a producer; the counts.
- The Rehearsal 009 replay of §4.
- Recovery: a successor session rebuilds its plan from committed files and continues; it stops at the deadline and refuses a missing or changed checkpoint.
- Reconciliation: the scope manifest and #101's obligation inventory agree on all 613 plan obligations, their requirements and their evidence kind (72 record-only, 541 executable or mixed), and the inventory excludes nothing.
- The generated work plan links every obligation to a work item; Praxis items begun with `praxis work begin` count toward coverage.
- Sequencing (#101's harness): `check-sequencing --scope-manifest` refuses implementation without, or before, the scope checkpoint, a scope hash mismatch, incomplete Praxis coverage and an edited checkpoint. Without `--scope-manifest`, its behaviour is unchanged.

## 7. Phase 8 validation

| Claim | Evidence |
|---|---|
| Every mandatory requirement is represented | `real` test: all 161 P0 and 38 catalog ids are in the manifest; `scope preflight` PASS |
| Every implementation work item is traceable | each requirement names its work items; each work item names its requirements (gate-level ones too); `REQUIREMENT_ORPHANED` never fires on the real scope |
| The release gate executes | v4.1.4 `suite-preflight` PASS on the fixture and on Rehearsal 009 (#101); every suite runs in CI job `suite-commands`, WebKit included |
| Implementation and verification states are independent | cases 2 and 6; `r9-progress.json` |
| Completion cannot be claimed after only the catalog | case 1; R9 replay `r9-decision-complete.json` |
| A blocked release gate does not prevent unrelated implementation | case 2; `r9-decision-gate-blocker.json` (`nextIndependentWork`) |
| An early stop with unfinished work gets a machine-readable refusal | `indy-init.stop-decision/1` files in `evidence/stop-decision/` |

## 8. Remaining blockers (owner actions)

| # | Blocker | Who | How |
|---|---|---|---|
| 1 | v4.1.4's decisions are not recorded (#101: `docs/release-gate-repair/owner-decision-requests.json`, `requirements-compiled/v4.1.4/proposals/`) | owner | `owner-freeze.fsx decide`; then an agent recompiles v4.1.4 and records its independent review |
| 2 | v4.1.4 is not approved or tagged | owner | `owner-freeze.fsx approve`, then `tag`; an agent adds it to the frozen guard |
| 3 | Dokimos `url-input` fix is not released | Dokimos owners, then owner | kemiller2002/dokimos#67 is merged. Release Dokimos. Register it in a new echelon-current set (owner approval, Conditor qualification). Without it, INV-029 (non-mandatory) still fails V1–V3. |
| 4 | No Rehearsal 010 lock | owner | Adopt `kickoff/rehearsal-010/toolchain.lock.proposed.json` as `toolchain.lock.json` after 1–3 (package path, manifest digest, resolved set). The scope manifest's `scopeSha256` is recompiled if v4.1.4 changes. |
| 5 | The launch prompt | owner | Use the prompt in STARTUP-INSTRUCTIONS.md, with no narrower target and no compute-saving instruction (ROOT-CAUSE H1, H2) |

**Not blockers, but known limits:**
- Progress events are written by the implementing agent. `stop-decision` refuses inconsistent events and agent acceptance, but it cannot detect an event that claims a passing verification which never ran. The release gate and the independent verifier remain the authority on verification.
- The generated `WI-GEN-*` items group requirements by family. They depend on T17 only, so their finer order is the agent's to plan.
- Ordo was not changed. No Ordo capability was needed for these checks.
- Praxis was not changed. Coverage reads Praxis's own backlog file; work items are created with `praxis add`.

## 9. Incremental merges versus final completion, and proposed follow-ups

**How the two gates are separated in the combined tree.** Nothing in this PR makes completion of the full scope a condition for merging a slice.
- **A slice merges on the development-mode gate:**
  - its applicable acceptance tests (the requirements it adds to `.echelon/verification-scope.json`, which only grows);
  - regression of every requirement already there;
  - governance and `check-sequencing`.
- **The full scope is required only by:**
  - the release gate (`mode=release`, unchanged);
  - `stop-decision complete` (final completion);
  - `acceptance-status` (fullPlan level).

None of these was weakened.

**Proposed follow-ups.** These are not implemented here, because they go beyond the approved integration scope.
- **F1: dependency satisfaction for a slice merge.** The gate does not yet check that the `dependsOn` of a slice's work items (manifest `workItems[].dependsOn`, `requirements[].dependsOn`) is implemented and verified. Proposal:
  - add `requirements-compiler slice-check --manifest M --events E --work-item ID`. It refuses when a dependency is not implemented, or when a dependency's obligations are not verified or recorded blocked;
  - run it in the development-mode gate for the work item a PR names.
- **F2: one startup document.** `docs/release-gate-repair/NEXT-REHEARSAL-STARTUP.md` (#101) and `STARTUP-INSTRUCTIONS.md` (#102) now point at each other. They could be merged into one document once the owner pins the lock.
- **F3: competition scope.** If the owner approves a narrower competition scope (#101 ODR-RG-01), it should become a new scope manifest with its own `scopeSha256`, under an owner `approve-scope` record. It should not be a second scope file.
