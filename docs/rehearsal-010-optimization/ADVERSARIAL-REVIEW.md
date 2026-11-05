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

### Round 2 (517230c): FAIL, 1 major

The reviewer re-verified the round-1 fixes. RVO-1-1 holds for the cases raised (p1–p6 refused). RVO-1-2 holds: forgeries fa–fd and ff are refused, and fe, reordered items, passes. No consumer of `Scope.preflight` breaks. The plain compile is unchanged (`59bfef5a…`), and the successor reproduces (`a847f98c…`).

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| RVO-2-1 | major | A dependency cycle **across two splits** compiled, passed preflight, and gave a stack overflow in the model. The cycle check covered siblings only | **Fixed:** one acyclicity check over the whole post-split work-item graph (`SPLIT_CYCLE`); the reviewer's p7 is an offline control; the model calls `topo` before recursing |
| RVO-2-2 | minor | The merge-order invariant cannot fire with one CI duration and no failures. **The round-1 claim that the out-of-order merge was "now caught" was wrong for the reviewer's mutation**, and M3 (restart of verified candidates), M10b and M13 were not caught | **Partly fixed:** exact repair-cost and per-slice-overhead assertions added (M10b and M13 now bite). The invariant is labelled a guard, not a proof. M2/M3 stay unprovable while the model has a single CI duration; published numbers are unaffected |
| RVO-2-3 | minor | §7 bound the merge by tree hash, while all existing evidence binds by commit SHA | **Fixed in the spec:** the verified speculative commit must itself become main by fast-forward; control S7 (same tree, different parents); MAIN_UNHEALTHY voids the queue |
| RVO-2-4 | note | SPLIT_DROPS_PARENT_DEPENDENCY followed siblings only, refusing some valid plans | **Fixed:** reach is computed over the whole post-split graph, including DAG tasks |
| RVO-2-5 | note | WORK_ITEM_ALTERED is key-order sensitive; overhead also inflates repair time and priorities | Documented (SCHEDULE-COMPARISON) |

### Round 3

### Round 3 (1ead513): FAIL, 1 major (documentation)

The reviewer worked from an independent anonymous clone of causa-data, and reproduced:
- R1, R2 and R5 exactly;
- the derived proposal files, byte-equal;
- `scope preflight` PASS;
- pinned files byte-unchanged against main.

It also ran 36 invariant cases under milestone priority (all PASS) and six mutations, four of which were caught.

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| RVO-3-1 | major | M1 and M3 times were presented without the blockers the branch's own ODRs give them; only M2 was labelled blocked | **Fixed:** TIME-TO-WORKING-APPLICATION limit 1, findings 4–5, FUNCTIONAL-PRIORITY-PLAN §2 and §7, and FINAL-REPORT now qualify M1 (ODR-R10-28, -29/-25) and M3 (ODR-R10-25, -26) |
| RVO-3-2 | minor | Milestone delivery is not cumulative, so `usableMilestonesAt` could overcount | **Fixed:** `usableAt` counts the delivered prefix; test added. Committed counts are unchanged, and reproduced by test |
| RVO-3-3 | minor | No staleness check on derived artifacts. Emptying every `dependsOn` in the aligned slice manifest, or ranking without ancestors, went undetected | **Fixed:** model tests reproduce every committed run R0–R5. The rank test covers ancestors. A CI step regenerates both successors' slice manifests and inventories and diffs them. Both reviewer mutations are now caught |
| RVO-3-4 | minor | Invariants never run under milestone priority | **Fixed:** 16 invariant cases (successor and aligned × depth 1/3 × fail 0/4 × CI 0/10) |
| RVO-3-5 | minor | C1 omitted `NegativeKnowledgeEstablished` (6) and `ConclusionRecorded` (4); the stub has 27 cases, not 28 | **Fixed** in DATA-REPOSITORY-CONTRACT.md |
| RVO-3-6 | note | ODR-R10-28 option B would browse with a write-capable token | Stands as an option; A is recommended |
| RVO-3-7 | note | Round 3 not recorded | This section |

Eight addendum checks:
- PASS: 1 (data from GitHub), 2 (no write credential), 3 (one canonical model), 4 (no requirement lost), 5 (no test weakened), 6 (save semantics);
- PARTIAL: 7 (runnable after each milestone: under v4.1.4 no milestone M1–M3 can pass against real data);
- PARTIAL: 8 (no relabelling; fixed by RVO-3-1).

### Round 4 (03825ca): PASS

The reviewer verified the RVO-3 fixes independently:
- the model tests (59 PASS) and the offline suite pass;
- pinned files are byte-unchanged against main (dbcad12);
- FINAL-REPORT's figures match the committed results;
- both round-3 mutations are now caught, as are three new mutations: `usableAt` reverted, M1 and M2 swapped, and an obligation dropped from an inventory, which only the CI step catches.

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| RVO-4-1 | minor | The reproduction test did not compare obligations or requirements, so an edited R2 headline (527 → 600) went undetected | **Fixed:** it now compares mandatory obligations, requirements and slices by the deadline; the R2 edit is caught |
| RVO-4-2 | minor | FINAL-REPORT claimed a round-4 result before one existed | **Fixed:** this section, and the report states the result |
| RVO-4-3 | minor | FINAL-REPORT Q6, the addendum table and the verdict bullet were unqualified | **Fixed** |
| RVO-4-4 | note | ODR-R10-27, SCHEDULE-COMPARISON, TIME-TO-WORKING finding 2 and CRITICAL-PATH gave M1/M2 times without their decisions | **Fixed** |

The reviewer judged the PARTIALLY OPTIMIZED verdict to be supported.

### Round 5 (653bfbb, the minimum-data-browser addendum): PASS

The reviewer confirmed these claims:
- the causa-data facts (independent parse: 1,363 events, CORS `*`);
- the Rehearsal 009 timeline and its browser-side `api.github.com` path;
- every dependency row against the DAG and the edge reasons;
- every TIME-TO-FIRST-DATA number, and R6 reproduced from the CLI;
- that no M0 code was committed, and the OD-INDY-22 reasoning;
- that nothing claims replay or counts M0 toward EXT-DATA-013, T25, J1-06 or J2-08.

Two mutations were caught.

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| RVO-5-1 | minor | Rehearsal 009's "accepted candidate" omitted that its release gate refused it (0/541) | **Fixed** everywhere it is cited |
| RVO-5-2 | minor | "Working application at T+21" is a commit time, not verification | **Fixed**: commit, CI (T+42) and review fixes (T+63) are separated |
| RVO-5-3 | minor | M0 cannot surface C1, since it does not decode events | **Fixed** in ARCHITECTURAL-LESSONS |
| RVO-5-4 | minor | "Costs nothing" ignored persistence and integration contribution work | **Fixed**: stated as a model assumption |
| RVO-5-5 | note | WI-GEN-EXT-DATA window 209 → 228 | **Fixed** |
| RVO-5-6 | note | file-name, measured-value and usable-count nits | **Fixed**. R6 `usableMilestonesAt` counts M0, and is not quoted as M1-level usability |
| RVO-5-7 | note | `Verify` consumes bytes, not parsed JSON | **Fixed**: only the adapter's bytes carry over |

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

### Round 6 (execution readiness)

Requested on the execution-readiness commit; result below when it completes.
