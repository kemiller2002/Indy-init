# Rehearsal 010 readiness: integration baseline

**Recorded:** 2026-11-04, 06:52–09:05 UTC, before any file was changed, and refreshed at the end of the integration.

**Rule:** earlier reports were not assumed to describe the repository. Every row below was read from the repositories or from GitHub at the time stated.

## 1. Repositories and commits

| Repository | Ref | Commit | Read |
|---|---|---|---|
| kemiller2002/Indy-init | `main` | `8672bc1f0302a0fb96e0d227056d2338e460a7ab` (merge of #103) | 06:52 |
| kemiller2002/Indy-init | #101 head `claude/causa-investigation-r9-6l5isz` | `73638962064f44b150e6ebc254934f58d5191575` | 06:52 |
| kemiller2002/Indy-init | #102 head `claude/rehearsal-009-early-stop-hcy044` | `c98968b1062e56c4e828789a33e73afa679266a3` | 06:52 |
| kemiller2002/Indy-init | integration branch `claude/rehearsal-010-integration` (#104) | see FINAL-READINESS.md §1 | — |
| kemiller2002/indy-init-rehearsal-009 | `main` | `0e760501b5585d11009a2fd2da8d146a1d47e025` | 07:05 |
| kemiller2002/dokimos | `main` | `9370202` (merge of #67) | 07:03 |
| kemiller2002/specula | `main` | `3094b7a7924785e5d0ad3ce2b6b3bc195dc6e338` | 07:05 |
| kemiller2002/forma | `main` | `dba08a005ee574ca134a7e3ae3354755826ebee3` (package 0.13.0) | 07:05 |
| kemiller2002/praxis | `main` | `12bf9951253fc15e6973c2d3f5b88076f4556245` | 07:05 |
| kemiller2002/indy-init-rehearsal-008 | — | not cloned; the audit's reading (`fd8fba9`) is used as reported | — |
| kemiller2002/conditor | — | used only through the pinned release 0.10.20 | — |

## 2. Pull requests

| PR | State at 06:52 | Determination |
|---|---|---|
| Indy-init #103 (adversarial audit) | **merged** 06:43 (`8672bc1`) | Its four files are on `main` and unchanged since the audit. |
| Indy-init #101 (release-gate repair, v4.1.4) | open, draft; last push 06:45 | Not integrated. Integrated on #104 (§4). |
| Indy-init #102 (process corrections, v4.1.3) | open, draft; last push 06:12 | Not integrated. Integrated on #104 (§4); its v4.1.3 is superseded by v4.1.4. |
| Indy-init #99 (Rehearsal 009 readiness) | open, stale | Superseded by #100 (merged). Not touched. |
| Indy-init #97 (owner-r9-ratify praxis hint) | open | Unrelated to this mission. Not touched. |
| Dokimos #67 (url-input) | **merged** 06:07 (`9370202`) | The fix. |
| Dokimos #68 (url-input) | **closed unmerged** 06:38 | **A duplicate.** Identical semantics (`url-input` = `<input>` of type `url`, `text` or none). #67 has the same four tests (positive, negative, INV-029 V1). No unique change remains, so no separate change is needed. |

**Concurrent writers.** #101 and #102 were pushed by other sessions until 06:45. No other session pushed to either branch after this integration began.

To prevent conflicting writes:
- all integration happens on one branch, `claude/rehearsal-010-integration` (#104);
- nothing was pushed to #101 or #102;
- the shared files are taken from #101, per the agreed ownership (`docs/release-gate-repair/INTEGRATION-RESPONSE.md`):
  - `tools/r9/r9.fsx`
  - `tools/r9/verification-gate.yml`
  - `tools/requirements-compiler/src/V41Package.fs`
  - `tools/owner-freeze.fsx`
  - `AGENTS.md`

**Recommendation:** once #104 merges, #101 and #102 are closed as superseded. That is an owner action.

## 3. Requirements package, scope and owner decisions

| Item | State |
|---|---|
| Frozen packages | v4 (`indy-init-requirements-v4.0.0`), v4.1, v4.1.1 and v4.1.2 (`indy-init-requirements-v4.1.2`, APR-V412-OWNER-3). The offline suite and CI job `frozen-v4` check each against its tag; none was modified. |
| v4.1.2 suite runners | Cannot execute: MSB1008 ×2 and an undefined `webkit` project. Re-confirmed on the Rehearsal 009 tree: `evidence/r9-replay/RP-15.log`. |
| Successor | v4.1.4 (#101), **CANDIDATE**. Its plan is byte-identical to v4.1.2's except for six runners (ODR-V414-01..03, pending). v4.1.3 (#102) is dropped; the number belongs to a withdrawn proposal. |
| Ratified catalog | `CAT-CAUSA-INVESTIGATION`, INV-001..038. sha256 `19a036682876b496a1785890ab22203931eb7afe7bf519f4c15526ac36f2d5af`; ratified at 0ab8ffd and 7cefdc6. |
| Pending owner decisions inherited | ODR-RG-01..05 (#101); ODR-V414-01..03 (#101). ODR-V413-04..06 (#102) are superseded by the V414 requests. |
| Owner decision records | `docs/OWNER-DECISIONS-2026-10-24.md` … `docs/OWNER-DECISIONS-2026-11-03.md` (OD-INDY-14 … OD-INDY-23). Unchanged. |

**Scope reconciliation** (the scope manifest recompiled from v4.1.4 at the integration commit):

| Count | Expected (integration proposal) | Compiled | Cross-check |
|---|---|---|---|
| Requirements | 199 | **199** (161 v4-plan + 38 catalog) | 161 equal #101's inventory requirement set |
| Obligations | 863 | **863** (613 plan + 250 catalog) | 613 plan obligation IDs equal #101's `obligation-inventory.json`, with no difference in either direction |
| Work items | 35 | **35** (27 DAG tasks + 8 generated) | every executable requirement names at least one |
| Mandatory requirements | — | 194 | — |
| scopeSha256 | `2b99dc01…` (v4.1.3) | `b407b5cc5f200e3651088440c0db630f18e84357c933020970f236f18f9352d6` | The identities are the same as the v4.1.3 compilation. Only the package source digest differs. |

Any mismatch would have failed closed: `scope preflight` and the offline `real` case compare the committed manifest with a fresh compilation.

## 4. Toolchain and dependency versions

| Component | Version | Pin |
|---|---|---|
| .NET SDK | 10.0.402 | `global.json` (rollForward disable) |
| Conditor | 0.10.20 (`a4c53cee`) | `kickoff/rehearsal-009/toolchain.lock.json` |
| echelon-current | 1.36.0 (Praxis 3.25.0, Dokimos 0.11.0, percepta-semantic 0.4.0, Ordo 1.20.0) | the same lock |
| Dokimos with `url-input` | merged on `main`. **Not released**; prepared as 0.11.1 (FINAL-READINESS §6) | — |
| Playwright | 1.63.0 | ODR-V41-07 |
| Forma | 0.12.0 inventory pinned in v4.1.4; Rehearsal 009 used 0.13.0 (same surface size: 352 properties, 524 classes) | `verification/design-contract/forma-0.13.0-inventory.json` (tarball sha256 `2f9ceeb6…`) |

## 5. Checks that existed at the baseline

CI workflow `preflight-tooling.yml` on `main@8672bc1`: run 41787394155, **success**. Jobs:
- `offline-contract-tests`
- `frozen-v4`
- `fsharp-tooling`
- `r9-readiness-gates`

Plus the `offline-factory` workflow.

On the merged tree:
- #101 added `release-gate-repair`;
- #102 added `suite-commands`;
- this integration adds `r10-adversarial`.

## 6. Integration actions taken

1. **Merged #101 into the integration branch,** then #102, resolving conflicts by the agreed ownership.
   - #102's `requirements-compiled/v4.1.3/` is removed; its `V41Package.fs`, `r9.fsx`, `verification-gate.yml` and `owner-freeze.fsx` changes are dropped.
   - Its `Scope.fs`, `Progress.fs`, `Continuation.fs`, `ScopeCommands.fs`, tests and readiness documents are kept and retargeted to v4.1.4.
2. **Recompiled the scope manifest** from v4.1.4 and verified the counts (§3).
3. **Ported from #102:**
   - the zero-selected-tests refusal (`suite-preflight --require-tests`, `run-suites --require-tests`);
   - the deterministic suite fixture (`r9.fsx suite-fixture`);
   - CI job `suite-commands`, retargeted to v4.1.4, which runs WebKit.
4. **Fixed X21:** a corrected runner with no suite or decision now reports a problem instead of throwing.
