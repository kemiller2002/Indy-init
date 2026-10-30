# v4 freeze declaration

**Status: CANDIDATE.** Package v4 is compiled and validated, and its independent review record passes (round 6). It is not approved and not frozen.

This file explains the status. The authoritative values are in [`freeze-manifest.json`](freeze-manifest.json), which the compiler writes and CI reproduces byte for byte (`requirements-compiler v4 --check`).

## The five states

Each state is established differently. Only the first two are computed by the compiler.

| State | Now | How it is established |
|---|---|---|
| Compiled | **yes** | The v3 pipeline returns compilation PASS (V01–V18), and the v4 stage generates every artifact |
| Validated | **yes** | Checks W01–W16 pass (`package-validation-report.json`), and plan-readiness is READY with 1211 of 1211 category checks and no permits |
| Independently reviewed | **record: yes** | Six review rounds were run (`independent-review.md`): rounds 1 to 5 FAIL, round 6 PASS, with no critical or major finding open and residuals recorded. FG-09 computes PASS from the record. The record is written by the package author from the reviewer's report, so it is a record, not an authenticated attestation |
| Approved | **no** | Owner decision requests ODR-V4-01..11 have no operator records (`proposals/owner-decision-requests.json`). Under RDP-003 only the operator writes them. The compiler checks that each record has a trusted approver, sits in a commit without agent trailers, is on origin/main and matches the proposal exactly. It cannot authenticate the person behind the record |
| Frozen | **no** | Never computed. The package is frozen only when the owner approves the freeze pull request on GitHub, and then the tag `indy-init-requirements-v4.0.0` is created on its merge commit. The kickoff (T01) and acceptance gate AG-13 check the tag, its manifest digests and the owner's review |

## Freeze gate (from `freeze-manifest.json`)

| Gate | Condition | Status |
|---|---|---|
| FG-01 | Authoritative baseline reconciled | PASS |
| FG-02 | No mandatory requirement disappeared | PASS |
| FG-03 | Every P0 requirement has acceptance coverage | PASS |
| FG-04 | No unresolved critical contradiction | PASS |
| FG-05 | All required owner approvals exist | **FAIL**: ODR-V4-01..11 pending |
| FG-06 | Dependency lock valid | PASS |
| FG-07 | Execution plan complete, and the selected topology fits | **FAIL**: no topology selected. Only C (role lanes with two UI agents, 405 of 420 minutes) fits |
| FG-08 | Compiler tests pass | PASS (the record matches the compiler tree; CI re-runs the suite and compares) |
| FG-09 | Independent review passes | PASS (an unauthenticated record; round 6 PASS) |
| FG-10 | Freeze commit recorded | **FAIL**: needs the owner's approval on main |

## Identity

| | |
|---|---|
| Package version | 4.0.0 (candidate) |
| Source commit | `62df28a559c054226dd4022e9744cf1db06e954f` |
| Baseline | `BL-CAUSA-INDY-INIT-62df28a559c0`, sha256 `f86a3539e48ee8dda7d99d5f4f5546a18e2397a82d308a277c8f346edb2a8f1a` |
| Counts | 520 requirements, 161 P0, 2599 obligations (613 P0 obligations), 655 acceptance obligations (650 normative), 25 invariants, 5 state machines, 20 Rehearsal 7 lesson classes, 11 owner decision requests |
| Digests | `freeze-manifest.json`: `verificationPlan` (digest and contentDigest), `dependencyLockSha256`, `executionManifestSha256`, `executionPlanSha256`, `acceptanceSha256`, and every file under `files` |
| Freeze timestamp | none: the package is not frozen (`freezeTimestamp` is the compilation time) |
| Tag | not created |

## What the owner does to freeze v4

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

Until then, the November 7 build may use the candidate. It must report the package as unfrozen, and it must treat every clause marked proposed as non-normative.
