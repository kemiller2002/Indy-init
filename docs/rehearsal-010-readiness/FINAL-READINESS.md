# Rehearsal 010 final readiness

> **Superseded in part (2026-11-06).** Rehearsal 010 is now pinned to the frozen v4.1.5 (tag `indy-init-requirements-v4.1.5` on b2b3022; APR-V415-OWNER-1..3). The scope is `26e6ff3c…` (#155), and ODR-R10-01 and ODR-R10-19 are re-asked for it (#156). The v4.1.4 package and scope rows below are kept as they were recorded.

## Decision: READY FOR OWNER ACTION

Engineering is complete and verified:
- the integration with `main` (#102);
- the governed gates;
- the owner tooling;
- the isolated live credential;
- the launch preflight and its negative controls;
- the schedule analysis.

The remaining conditions are owner actions, set out as one ordered transaction in [OWNER-APPROVAL-EXECUTION.md](OWNER-APPROVAL-EXECUTION.md). They are:
- the package freeze and tag;
- the Dokimos release and the registry set;
- the test repository and its App;
- the decisions;
- the catalog ratification;
- the scope approval;
- the lock.

**It is not READY TO LAUNCH.** Owner actions are outstanding, and the required real-provider test has not run. Rehearsal 010 has not been started; no T0 exists.

**The launch preflight** (`tools/r10/launch-preflight.fsx`) decides launch. Today it refuses, and only for owner actions: [`evidence/launch-preflight/launch-preflight-now.log`](evidence/launch-preflight/launch-preflight-now.log). Its 49 controls pass.

## 1. Integrated result

| Item | Value |
|---|---|
| Branch and PR | kemiller2002/Indy-init `claude/rehearsal-010-integration`, **#104** (draft) |
| Integrated head | **`7e6dca7`** for §3–§5. The v4.1.4 package and scope rows below are FREEZE step 2's, compiled after the owner recorded APR-V414-OWNER-1 |
| Base | `main` `cae4102`, the merge of #102. #104 merges it (`cadf4e4`) |
| Successor package | `requirements-compiled/v4.1.4` (CANDIDATE; X01–X21 PASS); package-manifest `24c68f7a…`, compiled with the owner's APR-V414-OWNER-1 (FREEZE step 2) |
| Scope (as compiled now) | `59bfef5a62a3ab43ef19c08b5915ffaf3d408bd55d8add874c5f1071e7cd087c`: the frozen v4.1.4 and the ratified catalog (205 requirements, 890 obligations, 37 work items). ODR-R10-01 approves this digest |
| Proposed lock | `kickoff/rehearsal-010/toolchain.lock.proposed.json`; every digest matches its file |

**What the merge with `main` changed** (details in OWNER-APPROVAL-EXECUTION §0):
- **Taken from `main`:** #102's work item `WI-GEN-RECORDS`, which gives the 16 record-only requirements a work item (36 work items); `scope work-plan`; `--plan` suite sources; the obligation-inventory reconciliation; INTEGRATION-REPORT.md.
- **`check-sequencing` combines both sides.** #102's rule: authorization runs from the later of the two checkpoints, with `--scope-manifest` coverage. #101's review fixes, which `main` lacked: every commit is judged by ancestry, and merges with `--cc`.
- **#104's FREEZE.md step 2 is kept.** Approval IDs go beside `suiteRunners`, which X21 reads.

**Other pull requests**

| PR | State | Note |
|---|---|---|
| Indy-init #102 | **merged** (`cae4102`) | carried #101 as of `0d816e3` |
| Indy-init #101 | open | superseded: #104 carries its review fixes (`7fefd7b`). Close after #104 merges |
| kemiller2002/dokimos#69 | draft, CI green | Dokimos 0.11.1; only a version bump and Praxis records, on #67's merge `9370202`; release by tag `dokimos-v0.11.1` (with checksums and provenance attestations) |
| kemiller2002/specula#11 | draft, CI green | read-only `ObserverFindings` adapter (CAP-016); 6 of 6 tests pass on the current observer output |

## 2. Scope counts

| | Now (successor deferred) | If ODR-R10-02 = A (successor ratified) | Mission expectation |
|---|---|---|---|
| Requirements | 199 (194 mandatory) | 205 (200 mandatory) | 199 |
| Obligations | 863 (791 executable, 72 record) | 890 | 863 |
| Work items | 36 | 37 | 35 |
| Exclusions | 0 | 0 | 0 |

- The mission's 35 work items predate `main`'s `WI-GEN-RECORDS`.
- The expected digest `b407b5cc…` was #104's scope before the merge.
- Per the mission, **the scope-approval transaction stops on this difference.** `owner-freeze.fsx decide rehearsal-010` does not ask ODR-R10-01 until the scope is compiled from the frozen v4.1.4 and the ratified catalog.
- Slices: 37, all 205 requirements placed, 0 unassigned (SLICE-EXECUTION-PLAN §1).

## 3. Verification at `7e6dca7`

| Check | Result |
|---|---|
| Build: requirements-compiler, tests (Debug) and src (Release) | PASS |
| Offline suite | PASS (all groups) |
| `tools/release-gate-repair/tests.fsx` | 74 cases, 0 failed (includes the side-branch checkpoint case) |
| `tools/r9/tests.fsx` | 80 cases, 0 failed |
| `tools/r10/adversarial.fsx` | **36 VALIDATED**, 0 not run, 0 failed |
| `tools/r10/r9-replay.fsx` | 15 of 15 as expected |
| `tools/r10/launch-preflight-tests.fsx` | 49 of 49 (positive controls in the unsigned and signed modes, an owner PR merged with a merge commit, and the accepted-risk case read READY; the other 45 are refused, each by its target check) |
| `tools/r10/launch-preflight.fsx` on this repository | REFUSED, for owner actions only (LP-00..09); LP-10 (the published `indy-init-rehearsal-010` holds one initial commit) PASS |
| Specula adapter on the current observer output | 6 of 6 |
| CI on `7e6dca7` | every job green on `b288b75`, `dfa42a2` and `53c2136`; `7e6dca7` was still running when this was written (FINAL-LAUNCH-VERIFICATION §3) |

FINAL-LAUNCH-VERIFICATION.md lists the preflight item by item.

## 4. What was implemented

Earlier rounds (R1–R8, slices, observer) are unchanged; see INTEGRATION-BASELINE.md and ADVERSARIAL-VALIDATION.md. Added in this round:

| Item | Where |
|---|---|
| Owner-decision transaction | `owner-freeze.fsx decide rehearsal-010` and `ratify rehearsal-010`; the requests in `kickoff/rehearsal-010/proposals/owner-decision-requests.json`. Each decision records the digest of what it approves; both commands refuse to write under an agent |
| Isolated live credential (ODR-R10-09) | The Verification gate is `contents: read`. A single-repository GitHub App token is minted per job, revoked at the end, and fails closed without secrets |
| Live-provider preflight | `.github/workflows/live-provider-preflight.yml`: owner-dispatched J1–J3 × Chromium and WebKit against `api.github.com`, with a token-scope check |
| Launch preflight | `tools/r10/launch-preflight.fsx` (LP-00..10) and its 49 controls |
| Topology A schedule | `tools/r10/schedule.fsx`; `evidence/schedule/` |
| Rehearsal 010 process-evidence rules | `kickoff/rehearsal-010/process-evidence-rules.json` (proposed; ODR-R10-14) |

## 5. Independent review

Each round was run by a reviewer agent spawned by this session. Each saw the code, not this document's conclusions.

| Round | On | Blocking | Major | Fixed in |
|---|---|---|---|---|
| 1 | the first integrated head | 16 findings | — | `b65d2f7` |
| 2 | `b65d2f7` | integration judged from builder events; the live clock | — | `d841ac2` |
| 3 | `d841ac2` | 2 | 3 | `9dd597f` (C28–C32) |
| 4 | `9dd597f` | 0 | 3 | `fad97aa` (C33–C35) |
| 5 | `fad97aa` | 0 | 2 | `6e43b7a` (C36) |
| 6 | the merge with `main` and this round's tooling (`7521966..d73b22c`) | 0 | 7: dropped checkpoint ancestry check; stale ODR-R10-01; LP-05 trusted free-text authority; LP-08 hand-written evidence; impossible scope check; App key on agent-pushable workflows; LP-10 bad path | `b288b75` |
| 7 | `975cd98..b288b75` | 2: LP-05 provenance (first commit only); the requests file was agent-writable | 5 | `dfa42a2` |
| 8 | `b288b75..dfa42a2` | 2: author, not committer, was trusted; the marker window could be skipped | 2 | `53c2136` |
| 9 | `dfa42a2..53c2136` | 1: a merge hid a rollback from `git log -- file` | 1 (local signature verification) | `316a13a` |
| 10 | `53c2136..316a13a` | 0 | 3: x509 verifier; grafts and shallow clones; who merged | `0c80154` |
| 11 | `316a13a..0c80154` | 1 (a false refusal): `merged_by` is absent from the abbreviated pull-request list | 0 | `7e6dca7` |

The fix of round 11 was checked against live GitHub (PR #103: `[103]`, then `[true,"kemiller2002"]`) and by the 47 controls; it has not had a round of its own. ODR-R10-15 asks the owner to accept these rounds for `7e6dca7`. Any material change after this requires a new round.

**Minor items accepted and documented:**
- `CompletedItems` (Praxis completion records) is builder-written. A record-only dependency's slice-order check rests on it. Record obligations are judged at release.
- The observer accepts any governed COMPLETE verdict for a commit on `main`.
- In the builder's own commands, `--t0` (replay only) and `resume --at` are read in local time when they lack an offset.
- The App token reaches candidate code in the live steps. That is inherent: the application needs a credential to persist. It is bounded to one synthetic repository for one job (LIVE-PROVIDER-PREFLIGHT §1).

## 6. Known limits

- **No real-provider run yet.** The journeys have run only against the REST double. LP-08 refuses launch until the live preflight passes.
- **The full scope does not fit 420 minutes** under Topology A at any CI latency, even zero (SLICE-EXECUTION-PLAN §3). The expected outcome is partial, verified integration reported at the deadline (ODR-R10-18).
- **Builder-authored fault probes; self-reported compute.**
- **Browsers in this container.** The local browser runs are Chromium only. WebKit runs in CI.
- **The App private key** is a secret of the competition repository. A builder workflow change could read it before the governance check refuses that run. What it grants is only the synthetic test repository. Rotate the key after the rehearsal. Moving it out of reach needs a separate owner-controlled runner repository, which is not done.
- **`fsharp-tooling`** has a known layout-kit timing flake.

- **No commit signing (the owner's decision, 2026-11-04).** Owner records and the lock count by GitHub's record that the owner committed and authored them; merges count only when the owner merged them.
  - The accepted risk: agents in this environment act on GitHub as the owner. An agent that writes the owner's email as its committer is recorded as the owner, and launch-preflight cannot tell it apart (case `accepted-risk-agent-commit-under-the-owners-name` reads READY).
  - Signing closes this: `--require-signatures true` on launch-preflight and owner-freeze restores the signed check, which is still tested.

## 7. Releases (owner merges and tags)

- **Dokimos 0.11.1** (dokimos#69). Merge it. Then tag `dokimos-v0.11.1` on the merge commit, or dispatch `release.yml` with version `0.11.1`. Then verify the release assets against `dokimos-checksums.txt` and the attestations.
- **echelon-current.** Then an agent prepares the echelon-registry pull request for a new set carrying 0.11.1, and runs Conditor qualification on that set. The owner approves and merges it.
- **Specula** (specula#11): merge after its CI (ODR-R10-17).

## 8. Owner actions outstanding

They are in order in OWNER-APPROVAL-EXECUTION.md §3:
1. merge #104 and close #101;
2. v4.1.4: decide, then the agent recompiles, then approve, then tag;
3. Dokimos 0.11.1 and the registry set;
4. the test repository, the App and its secrets. The competition repository `kemiller2002/indy-init-rehearsal-010` already exists, with one initial commit (`616ee17`, README only) and no T0 record. The test repository `causa-acceptance-r10` is not visible to this session, so it is not confirmed;
5. `decide rehearsal-010`;
6. `ratify rehearsal-010`;
7. the scope recompile, then ODR-R10-01;
8. the live preflight;
9. commit the lock;
10. Specula and the observer.

The decisions are ODR-V414-01..03, ODR-RG-03, -04, -05 and ODR-R10-01..18. ODR-R10-18 is new: accept the expected partial outcome, or reduce scope, or extend the window.

## 9. The exact next launch command

**Run only after LAUNCH-CHECKLIST §A–C are all checked**, including `launch-preflight` printing READY. Until then this command must not run.

It runs in a clone of the empty competition repository `kemiller2002/indy-init-rehearsal-010`, with `SRC` = the Indy-init checkout at the lock's governing commit:

```
cd <clone of kemiller2002/indy-init-rehearsal-010> && \
SRC=<Indy-init checkout at the lock's governing commit> && \
dotnet fsi "$SRC/tools/r10/launch-preflight.fsx" -- --indy-init "$SRC" --live-run <live-provider preflight run id> --adversarial-run <Preflight tooling run id of main> --competition https://github.com/kemiller2002/indy-init-rehearsal-010 && \
claude -p "$(sed -n '/^> \*\*Objective/,/^$/p' "$SRC/docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md" | sed 's/^> //') SRC is $SRC. Follow $SRC/docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md."
```

- The launch preflight runs first, and the `&&` stops the launch unless it prints READY.
- The prompt is exactly STARTUP-INSTRUCTIONS §0, with nothing narrower added.
- T0 is the builder's first commit, the bootstrap with `evidence/t0/t0.json`.
- The owner schedules the observer at T0 (STARTUP-INSTRUCTIONS §9).
