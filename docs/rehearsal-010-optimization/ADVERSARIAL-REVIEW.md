# Rehearsal 010 optimization: independent adversarial review

Two independent reviewer agents worked from the code and their own clones, not from the implementer's conclusions. Neither modified the repository. The implementing agent certified nothing itself: every blocking finding below was fixed and then re-reviewed.

## A. Launch fixes (PR #118): three rounds, final verdict PASS

| Round | Head | Verdict | Blocking findings | Disposition |
|---|---|---|---|---|
| 1 | 1539cfc | FAIL | Major: a concurrency group keeps one *pending* run and cancels it for the next, so main's verification could still be cancelled while waiting | Fixed: every non-PR run gets its own group (`github.run_id`) |
| 2 | 5041eea | FAIL | Major: Conditor's scaffold protects main (pull requests required), so the setup records couldn't be committed directly; through a PR the gate treated them as implementation | Fixed: the gate excludes these records, as `isImplementationPath` already did, and STARTUP routes them through `setup/*` PRs merged with a merge commit |
| 3 | 485655f | **PASS** | none | Notes: `check-sequencing` can't tell a real initialization checkpoint from a hand-written one (this predates the PR, and is left to the observer); `.ros` coverage files go in or before the scope-checkpoint commit (added to STARTUP); LP-09 needs a new tooling run after merge |

Round 3 verified, among other things:
- a setup PR runs governance, `check-sequencing` and the base suites, and skips only the slice gate;
- the checkpoint-parent rule holds under a merge-commit merge (shown in a scratch repository);
- a clean GitHub merge commit lists no implementation paths;
- the excluded files are never read by the gate from the candidate.

## B. Optimization (PR #119)

### Round 1 (39d83e0): FAIL, 2 majors

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| RVO-1-1 | major | `applySplit` validated against the pre-split items. Duplicate ids across parents, dependencies on replaced parents, parts skipping T17 and arbitrary owners all compiled, and preflight passed them | **Fixed:** judged against the post-split set; four new refusal codes; the reviewer's mutants are offline controls |
| RVO-1-2 | minor | `scope preflight` never compared work items | **Fixed:** WORK_ITEM_ALTERED, _MISSING, _UNSOURCED and _DUPLICATED, with forged-successor controls |
| RVO-1-3 | minor | Cross-item requirement dependencies are not ordered, and the docs overclaimed | **Narrowed:** the claim now covers dependencies within one parent; the gap predates the split and is documented |
| RVO-1-4 | major | ODR-R10-22 cited a speculative-verification spec and controls that did not exist; the obvious implementation would relax `STALE_TARGET` | **Fixed:** VERIFICATION-OPTIMIZATION §7. The verified tree must equal the merged tree, which strengthens `STALE_TARGET`; controls S1–S6; ODR-R10-22 cannot be decided before they exist |
| RVO-1-5 | minor | The model tests did not catch merges out of queue order, or repairs costing no time | **Fixed:** two invariants added. The out-of-order mutation is now caught (4 cases fail on it) |
| RVO-1-6 | minor | Optimistic assumptions: no per-slice overhead; V5 assumes ownership that doesn't exist; merges cost no agent time; a false monotonicity claim | **Fixed or labelled:** `--slice-overhead`; V5 marked hypothetical; claim corrected; merge cost stated as a limitation |
| RVO-1-7 | note | The split does not shorten completion time: the queue and the set-up do | **Published:** separation rows in SCHEDULE-COMPARISON and ODR-R10-19 |
| RVO-1-8 | note | Smaller test gaps (some refusal codes, recompile-and-compare) | Partly covered by the new controls; the rest are open notes |
| RVO-1-9 | note | Scope, approvals, clock, observer and gate are intact | – |

The reviewer also confirmed that:
- the published V1, V3 and V5 numbers reproduce exactly;
- the approved scope recompiles byte-identically;
- every requirement of the successor matches the approved scope field by field.

### Round 2

Pending at the time of writing; see FINAL-REPORT.md.

## The ten mission questions (from round 1 unless noted)

| # | Question | Answer |
|---|---|---|
| 1 | Did a requirement or obligation disappear? | No. All 205 requirements are identical field by field; only work-item counts differ |
| 2 | Did splitting change semantics? | Not the requirements. Ordering holes were found and fixed (RVO-1-1); cross-item ordering is unchanged from before (RVO-1-3) |
| 3 | Can test reuse hide failures? | No gate code was changed. The reuse proposals are design only, and the Dokimos omitted-test gap is documented (VERIFICATION-OPTIMIZATION §2) |
| 4 | Can parallel verification accept stale results? | Not under §7: the tree identity check plus voiding of stacked verdicts. Not built |
| 5 | Can integration bypass current-main verification? | Not on this branch. #118 makes main's post-merge runs uncancellable |
| 6 | Do gains depend on unrealistic assumptions? | Partly: merge cost, per-slice overhead, V5's ownership. All now labelled, with sensitivity runs |
| 7 | Was the scope or approval digest changed without authorization? | No. Pinned files are unchanged; the successor is PROPOSED only |
| 8 | Is the 420-minute clock immutable? | Yes. `deadline-policy.json` and T0 handling are untouched |
| 9 | Can the observer still detect shortcuts? | Yes. Observer pins and Observer.fs are unchanged, and its scope checks apply to a successor |
| 10 | Is recovery from interrupted sessions preserved? | Yes, for adoption before T0. A mid-run adoption would orphan Praxis records (now stated in ODR-R10-19) |
