# v4 freeze declaration

**Status: CANDIDATE.** Package v4 is compiled and validated, the owner's decisions ODR-V4-02..11 are recorded and applied, and its independent review record passes for the plan as now compiled (rounds 7 and 8). It is not approved (ODR-V4-01 is pending) and not frozen.

This file explains the status. The authoritative values are in [`freeze-manifest.json`](freeze-manifest.json), which the compiler writes and CI reproduces byte for byte (`requirements-compiler v4 --check`).

## The five states

Each state is established differently. Only the first two are computed by the compiler.

| State | Now | How it is established |
|---|---|---|
| Compiled | **yes** | The v3 pipeline returns compilation PASS (V01–V18), and the v4 stage generates every artifact |
| Validated | **yes** | Checks W01–W16 pass (`package-validation-report.json`), and plan-readiness is READY with 1211 of 1211 category checks and no permits |
| Independently reviewed | **record: yes** | Eight review rounds were run (`independent-review.md`): rounds 1 to 5 FAIL, round 6 PASS, round 7 PASS for the plan with the owner's amendments applied and round 8 PASS for its remediation, with no critical or major finding open and residuals recorded. FG-09 computes PASS from the record, and only when the last round names the content digest of the plan being frozen. The record is written by the package author from the reviewer's report, so it is a record, not an authenticated attestation |
| Approved | **no** | The owner recorded ODR-V4-02..11 in operator approval APR-V4-OWNER-1 (`kickoff/rehearsal-007/approvals.causa.json`, commit `cbc752b`), and the plan applies them. The package approval ODR-V4-01 has no record yet. Under RDP-003 only the operator writes them. The compiler checks that each record has a trusted approver, sits in a commit without agent trailers, is on origin/main and matches the proposal exactly. It cannot authenticate the person behind the record |
| Frozen | **no** | Never computed. The package is frozen only when the owner approves the freeze pull request on GitHub, and then the tag `indy-init-requirements-v4.0.0` is created on its merge commit. The kickoff (T01) and acceptance gate AG-13 check the tag, its manifest digests and the owner's review |

## Freeze gate (from `freeze-manifest.json`)

| Gate | Condition | Status |
|---|---|---|
| FG-01 | Authoritative baseline reconciled | PASS |
| FG-02 | No mandatory requirement disappeared | PASS |
| FG-03 | Every P0 requirement has acceptance coverage | PASS |
| FG-04 | No unresolved critical contradiction | PASS |
| FG-05 | All required owner approvals exist | **FAIL**: pending ODR-V4-01 only |
| FG-06 | Dependency lock valid | PASS |
| FG-07 | Execution plan complete, and the selected topology fits | PASS: the owner selected C (role lanes with two UI agents), which fits at 405 of 420 minutes |
| FG-08 | Compiler tests pass | PASS (the record matches the compiler tree; CI re-runs the suite and compares) |
| FG-09 | Independent review passes | PASS (an unauthenticated record; round 8 PASS, naming the current plan content digest) |
| FG-10 | Freeze commit recorded | **FAIL**: needs the owner's approval on main |

## Identity

| | |
|---|---|
| Package version | 4.0.0 (candidate) |
| Source commit | `62df28a559c054226dd4022e9744cf1db06e954f` |
| Baseline | `BL-CAUSA-INDY-INIT-62df28a559c0`, sha256 `f86a3539e48ee8dda7d99d5f4f5546a18e2397a82d308a277c8f346edb2a8f1a` |
| Counts | 520 requirements, 161 P0, 2599 obligations (613 P0 obligations), 655 acceptance obligations (655 normative: the owner's approvals made the proposed ones normative), 25 invariants, 5 state machines, 20 Rehearsal 7 lesson classes, 11 owner decision requests |
| Digests | `freeze-manifest.json`: `verificationPlan` (digest and contentDigest), `dependencyLockSha256`, `executionManifestSha256`, `executionPlanSha256`, `acceptanceSha256`, and every file under `files` |
| Freeze timestamp | none: the package is not frozen (`freezeTimestamp` is the compilation time) |
| Tag | not created |

## What the owner does to freeze v4

Steps 1 to 3 are done: the owner's APR-V4-OWNER-1 (#51), and the curation, recompile and review rounds 7 and 8 (#52).

1. Decide ODR-V4-02..11 in `proposals/owner-decision-requests.json`.
   - Write each decision as a change in one operator approval in `kickoff/rehearsal-007/approvals.causa.json`: `{"id", "authority": {"kind": "operator", "id": "kemiller2002"}, "date", "changes": [...]}`. This is the schema verify-plan authorizes (see `recordFormat`).
   - Make the commit yourself and merge it through a pull request.
   - Write amendments exactly as proposed. Where two approved requests amend PERSIST-051-O2, write its `combinedCriterion` once.
   - Scope decisions name an option letter.
   - ODR-V4-07 names a topology. Only `C-role-lanes-two-ui` fits.
   - A declined request is `{"decision_id", "action": "decline"}`.
2. Re-pin the approvals commit in `curation/verification-plan.json`. Add each approved amendment to `obligationAmendments` as `{obligation, approval, decision}`, then recompile both stages. The agent can do this step, because it applies the operator's records and writes none. FG-05 counts an amendment only once the compiled plan applies it, with exactly the recorded criterion.
3. If approved amendments changed the plan, run a fresh independent review of the recompiled package and record it. FG-09 needs a last round verdict of PASS.
4. Write the ODR-V4-01 record. It names `verificationPlan.contentDigest` and the SHA-256 of `analysis/independent-review.json` and `analysis/compiler-test-record.json`. Approve the freeze pull request on GitHub.
5. After merge, set `analysis/freeze-input.json` `freezeCommit` to the merge commit and recompile. When the status reads READY-FOR-OWNER-FREEZE, tag that commit `indy-init-requirements-v4.0.0`.

Until then, the November 7 build may use the candidate. It must report the package as unfrozen, and it must treat every clause marked proposed as non-normative unless its request is decided below.

## Decided requests (APR-V4-OWNER-1)

Some generated texts still use the wording of the proposals (`state-machines.json` `proposedTransitions`, the "in flight" reading in `operational-definitions.json`, the gap-proposed classes in `r7-lessons-mapping.json`, and the topology note in `timed-execution-manifest.json`; review residual RV7-2, accepted in round 8). A clause marked proposed whose request is listed here is decided and normative:

| Request | Decision |
|---|---|
| ODR-V4-02 | Approved: the copy ban and the provenance scan extend to Rehearsal 7's repository (PERSIST-026-O2, PERSIST-052-O3) |
| ODR-V4-03 | Approved: replies to superseded requests are discarded (PERSIST-051-O2, combined with ODR-V4-08) |
| ODR-V4-04 | Approved: re-authentication keeps the tab's unsaved work (PERSIST-031-O22) |
| ODR-V4-05 | Approved: a way out of a conflict whose GitHub version is deleted or unverifiable (PERSIST-012-O2) |
| ODR-V4-06 | Approved: forged provider replies cannot reach a confirmation (PERSIST-051-O3) |
| ODR-V4-07 | Topology C, role lanes with two UI agents |
| ODR-V4-08 | Approved: a verified load is a confirmation that may show Saved (PERSIST-023-O2, PERSIST-007-O2, PERSIST-007-O4, PERSIST-051-O2) |
| ODR-V4-09 | Option A: the AI proposal loop stays in scope (T27 stays scheduled) |
| ODR-V4-10 | Option A: the P0-only plan; non-P0 requirements are verified by their manifest rules and gates |
| ODR-V4-11 | Option A: a lost answer settles the mutation for PERSIST-006; other investigations may save while one is in Unknown commit outcome |

The authoritative record is the approvals file itself; this table only names what it holds.
