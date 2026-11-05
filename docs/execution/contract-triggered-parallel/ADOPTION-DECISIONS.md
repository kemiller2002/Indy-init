# CTP-001 adoption decisions

**Status.** Nothing here changes a frozen requirement package, the competition lock, launch-preflight, a required CI check, Forma or Conditor. Everything needing the owner is listed in section 2, in one batch.

**Basis.** The agent-side adoption in section 1 falls under the owner's standing technical authorization (Parallel Acceleration Directive: "updating preparation scripts, development workflows, and non-security governance procedures"). It is **to be recorded** as DTD-R10-007 in `kickoff/rehearsal-010/delegated/delegated-decisions.json`, in a separate PR after this one merges. A delegated decision may not ride with this PR's workflow change (the CTP-check step), which is a protected path. Until that entry is on `main`, nothing in section 1 is in force.

## 1. Ready for immediate adoption (agents; no frozen change)
| Rule | Enforced by |
|---|---|
| Lifecycle DEFINED…VERIFIED; immutable versions; change requests for revisions | `ctp-check lifecycle` (CTP-L-*) |
| Contract completeness: requirement IDs, stable IDs, typed F# boundary, a named control per UI action, nested and option labels, structured transitions, error behaviour, persistence mode, test obligations | `ctp-check registry` (CTP-C-*) |
| Roles and single-writer ownership; independent verifier | `ctp-check ownership` (CTP-O-*), `ctp-check integration` (CTP-I-INDEPENDENT) |
| Event-driven integration with version check, browser acceptance and defect ownership | `ctp-check integration` (CTP-I-*) |
| Independent browser test at **every** integration | integration record gate (CTP-I-ACCEPTANCE); reviewer duty |
| Durable GitHub persistence verified only by a real write and read-back | CTP-C-PERSISTENCE, CTP-I-REAL-PROVIDER; `.github/workflows/live-provider-preflight.yml` |
| Push once per candidate: work branches without PRs; the integration coordinator updates PR branches | **procedure** (INTEGRATION-POLICY §4); measured by `ctp-check ci` |
| Never merge past a red check; re-run an infrastructure failure once | review gate (already given to all agents, 2026-11-05) |

Once DTD-R10-007 is recorded, these apply to preparation work and rehearsals on Indy-init. Until then they are proposals.

## 2. Requiring owner approval

| # | Decision | Why it is the owner's | Recommendation |
|---|---|---|---|
| CTP-OD-1 | **Make CTP-001 the competition execution method:**<br>• add COMPETITION-ENTRYPOINT.md to the launch reading list;<br>• replace the "UI after hour 3" variant with UI from the first release (EXP-002 condition 2). | changes the competition execution plan | **A: adopt** for Rehearsal 010 and Nov 7 |
| CTP-OD-2 | **Make the `ctp-check` lifecycle and integration modes a required check** in the competition repository's slice gate or launch-preflight, pinned through the lock's Indy-init source commit. | changes governed gates and the lock | A: adopt after one rehearsal uses it. Until then it is advisory |
| CTP-OD-3 | **Allow the contract registry to be prepared before T0** as frozen-package content in a successor package, instead of being written in the window. | frozen specification, and OD-INDY-22 (what may exist before the start) | B: write it in the window for Nov 7. No new package this close to the competition |
| CTP-OD-4 | **CODEOWNERS** with "Require review from Code Owners" over: `tools/execution/`, `tools/r10/delegated-authority.fsx`, `.github/workflows/`, `kickoff/rehearsal-010/delegated/`. | branch protection is a security control | A |
| CTP-OD-5 | **PR #141's conventions** CV-05 (one source for environment literals) and CV-07 (pipefail by default). Their checks exist in #141's `tools/conventions/conventions.fsx` (CK-02, CK-03), which is unmerged. | #141 states that every adoption needs an owner decision | A: merge #141's tooling and run CK-02 and CK-03 as advisory |
| CTP-OD-6 | **PR #141 CV-17: pin Forma 0.13.0.** v4.1.4 and v4.1.5 reference 0.12.0, which fails 48 of 78 containment checks. | **breaking change to a frozen specification** (ODR-IC series) | owner's call. Not before Nov 7 unless the 0.12.0 failures block the build |
| CTP-OD-7 | **EXP-INDY-002 (PR #140):** merge the docs only, or the docs plus the isolated code, or nothing. | experimental code on main is an OD-INDY-22 question | docs only |
| CTP-OD-8 | **Repeat EXP-002 on GitHub-backed persistence with the governed gates**, and a controlled milestone-first comparison (EXP-002 condition 5). | competition time budget | A, if a slot exists before Nov 7; otherwise accept the forecast as modelled |

## 3. PR #141 conventions, prioritized for CTP-001
Reviewed against the 23 conventions in `docs/optimization/implementation-conventions/CONVENTION-CATALOG.json` (PR #141; catalog unchanged between `46a463c` and head `46af352`). Nothing in #141 is adopted by this PR. "Covered" below means a CTP-001 rule, itself proposed, enforces the same idea. #141's own decisions (ODR-IC-01..06, including ODR-IC-04 for CV-09 and ODR-IC-06 for CV-19) remain the owner's.

| Area | Convention | Status under CTP-001 |
|---|---|---|
| Contract stability | CV-12, released contracts are immutable versions | covered (proposed) by CTP-L-IMMUTABLE and CTP-L-CHANGE-REQUEST |
| Contract stability | CV-23, stable requirement and field IDs | covered (proposed) by CTP-C-ID and CTP-C-REQ |
| Contract stability | CV-11, contracts proven against real data | existing: the historical-compatibility suite (C11/C15/C16) |
| F# domain/UI boundary | CV-10, typed errors through one state machine | partially covered (proposed) by CTP-C-ERROR and CTP-C-TRANSITION-STRUCTURE. #141's code-level check is not adopted (owner, via #141) |
| F# domain/UI boundary | CV-22, standard per-feature file split | not covered (DISCRETIONARY; package ownership decides files) |
| Forma layout | CV-17, Forma 0.13.0 or later | owner: CTP-OD-6 (breaking) |
| Forma layout | CV-18, editable fields bind on input through the field map | existing (the contract-first UI field map) |
| Persistence | CV-09, durable means read back from the real provider | covered (proposed) by CTP-C-PERSISTENCE and CTP-I-REAL-PROVIDER; existing live-provider preflight |
| Agent file ownership | CV-08, single-writer files, append-only shared state | covered (proposed) by CTP-O-*; existing package OwnershipTests |
| Independent acceptance | CV-15, evidence bound to the candidate SHA | covered (proposed) by CTP-L-VERIFIED (`candidateSha`); existing slice gate |
| Independent acceptance | CV-13, every bound test has a falsifying control | existing slice and completion gates; CTP tests carry negative controls |
| Independent acceptance | CV-19, test-only code never ships | **review gate**: UI stubs are removed at integration (AGENT-OWNERSHIP) |
| Low-risk checks | CV-05, CV-07 | owner: CTP-OD-5 |
| Out of scope for CTP-001 | the other 9: CV-01…04, CV-06, CV-14, CV-16, CV-20, CV-21 | unchanged; #141's own decisions |

## 4. Existing systems that enforce parts of CTP-001
| System | Enforces |
|---|---|
| `tools/r9/r9.fsx` (`check-sequencing`, `authorize-implementation`) | no code before the checkpoints; scope pinned |
| Slice gate and completion gate (`tools/requirements-compiler/src/Ledger.fs`) | per-slice journeys, regression, design contract, evidence bound to the candidate |
| Package OwnershipTests and `agent-file-ownership.json` | single-writer files in the competition build |
| `live-provider-preflight.yml` | real write and read-back on the authorized test repository |
| Preflight tooling concurrency (merged in #136) | one run per PR head, with older runs cancelled. It reduces waste, but cannot stop a self-inflicted cancel |
| `owner-freeze.fsx`, launch-preflight LP-05 | only the owner records approvals |
| `tools/r10/delegated-authority.fsx` | agent decisions stay in scope |

## 5. Remaining enforcement gaps (honest list)
1. **Missing controls are not detectable.**
   - A schema check finds unnamed or unlabelled controls, but not controls the registry never lists. In CR-1 that is INV-039 (its control was absent) and the three per-item Edit controls (never listed as UI actions). INV-040's control was listed under a wrong name, and name correctness is not checkable.
   - Only the independent acceptance test, written from requirements, finds them.
2. **Correct content is not checkable.**
   - CTP-C-TRANSITION-STRUCTURE forces a transition to state what its target state carries. It cannot check that the payload is right.
   - CR-3's fix still needs a human or a test.
3. **Push discipline is procedural.** No automated check prevents a push that cancels a required run. `ctp-check ci` only measures it.
4. **The class field is self-declared.** As with DA-R10-1, an agent picks a contract's kind and persistence mode. A contract that wrongly declares `local` instead of `durable-github` passes, and only review catches it.
5. **The checker runs from the PR's own copy** until CTP-OD-2 pins it through the lock. Until then it is advisory and review-gated.
6. **Combined roles weaken independence.** When the contract author also writes the acceptance test (as in EXP-002), the test is independent of the implementers but not of the contract.
7. **CI shard installs** can stall on a slow apt mirror: PR #139, and PR #140's final candidate, shard 0. That delays VERIFIED. A cached or retried browser install is a separate CI fix. (The CTP check itself runs in the `fsharp-tooling` job, before any browser install.)
8. **CTP-001 is strict by design.** EXP-002's own registry would not reach VALIDATED under it: 27 of 37 contracts fail at v1, and 28 of 39 at final, mostly for missing error text, acceptance obligations and structured transitions. A competition registry has to be written to this schema from the start, and that costs time early in the window.
9. **Payload truth and cross-contract names are not checked.**
   - `carries: ["nothing"]` passes on any transition, even CR-3's save transition.
   - Control names must be distinct within a contract, but not across contracts on one screen. Two lists each with an "Edit" control both pass, and only the acceptance test, through the accessible name and its region, tells them apart.

## 6. Expected impact on competition execution (modelled, not observed)
- **First accepted workflow.** EXP-002's modelled saving was about 8.4 minutes (27%) at a 39-requirement slice. At competition scale, with real GitHub, governed gates and 5–7 streams, EXP-002 itself discounts throughput to about 40% (FINAL-REPORT §8). Treat the saving as directional only.
- **Earlier detection, rule by rule.** The control-name, nested-label and option-label rules flag CR-1's five multi-action contracts (8 of the 13 names CR-1 changed) and all of CR-2 at v1, before any UI code. They do not make a registry valid sooner: CTP-001 also demands content EXP-002 never wrote. In EXP-002 each revision cost the coordinator about 2 minutes plus UI rework, and the competition scale has more contracts.
- **CI waste.** EXP-002 lost three Preflight runs to self-cancellation. The push-once procedure removes that source if it is followed.
- **What is not expected:** a change to the governed gates' results. CTP-001 feeds them; it does not replace them.
