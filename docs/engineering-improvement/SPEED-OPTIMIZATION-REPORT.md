# Echelon development speed optimization: report (2026-10-28)

Initiative: reduce the time and agent resources needed to deliver independently verified software, using Rehearsal 5
as the baseline and preparing Rehearsal 6. Owner: kemiller2002. Intake:
[`input-documents/2026-10-28-ESO-001-development-speed-optimization.md`](../../input-documents/2026-10-28-ESO-001-development-speed-optimization.md).

**Status words.**
- *Implemented*: on a draft pull request, not merged. Nothing was merged without the owner.
- *Verified*: the change's own tests, its negative controls and the repository's test suite pass locally. CI status
  is per pull request.
- *Measured*: only what was measured. "Unavailable" is not zero, and no saving is estimated.

Rehearsal 5 was read only and not modified. Rehearsal 6 was not started.

## 1. Baseline: Rehearsal 5

Sources: `docs/REHEARSAL-005-RESULTS.md`; R5 `evidence/rehearsal/final.json`; R5
`evidence/acceptance/run-40620099844-1b9fb56/`; [`investigations/r5-final-metrics.md`](investigations/r5-final-metrics.md)
(recomputed from git).

| Metric | Rehearsal 5 | Source |
|---|---|---|
| Execution time | 392 min (start 20:08:38Z, verdict 02:41:07Z; deadline 420 min) | `final.json` |
| P0 requirements | 108: 77 PASS, 31 NOT_RUN, 0 FAIL | `final.json` |
| Verification obligations | 326: 277 PASS, 49 NOT_RUN | `final.json`, `bundle/traceability.json` |
| Acceptance gates | 8 of 10 (G0–G7 PASS; G8, G9 NOT_RUN) | `final.json` |
| Final acceptance | INCOMPLETE, NOT ACCEPTED | ledger verifier and plan verifier |
| Commits in window | 279 (92 merges, 187 non-merge) | git |
| Conflicted merges | 17 of 92 | `git merge-tree` |
| Merges of main into a stream | 58, of which 36 had no path overlap | [`investigations/r5-merge-necessity.md`](investigations/r5-merge-necessity.md) |
| `Praxis: reconcile` commits | 26; explicit integration repairs 2 (`805b7a9`, `b6238ad`) | git |
| Praxis post-hoc attribution events | 58 `work.attribution.reconciled`; 1 `work.checkpointed` | R5 `.ros/events/events.jsonl` |
| Acceptance-evidence commits | 35, from T+5.0 to T+364.0 min | git |
| Failed browser-test CI runs | ≥ 5 (4 hard-coded test port, 1 WebKit load wait); lower bound | commit bodies, reviews |
| Final CI run | 14 jobs, 1,467 job-s (24.45 job-min); `browser` 705 s (48%) | `gate-records/layer-timings.json` |
| CI runs, CI minutes for the window, re-runs | **unavailable** (the Actions API for this repository was not reachable from this session) | — |
| Time to first working application | **unavailable** exactly; bounded between T+38.7 and T+287.4 min (first CI-confirmed deployed-smoke) | r5-final-metrics §7 |
| Agent tokens, tool calls | **unavailable**: every value `supported-unavailable` or `unknown` in all 8 telemetry records; 7 never finalized | R5 `.ros/telemetry/executions` |
| Time producing acceptance evidence | **unavailable** as effort; the 35 commits above span 359 min | — |

### Plan binding discrepancy (recorded, not resolved)

- Two verifiers reached the same verdict (INCOMPLETE) by different routes. The ledger verifier
  (`tools/compute-acceptance-verdict.py`) counts 77 requirements PASS. The plan verifier (`requirements-compiler
  verify-plan`) checks against the plan Rehearsal 5 pinned (`VP-e77a2134e1b0`), which binds no test, so it reports
  the same 77 as UNBOUND.
- With the curated plan `VP-5d1202ddec1e` they are PASS. That plan was proposed in **Indy-init PR #32, which was
  closed unmerged**.
- It is not approved and is not treated as approved here. Rehearsal 5's historical verdict is unchanged.

## 2. Changes

| Priority | What exists (evaluated first) | What changed | Where |
|---|---|---|---|
| 1. Automatic acceptance evidence | The Praxis event log already records `work.started`, `work.checkpointed`, `artifact.contributed`. Nothing records reviews, owner approvals, scope changes, release selection or disposition as process evidence. Nothing verifies process obligations. | `praxis work evidence` writes `work.evidence.recorded` to the **same** log. Each record keeps the statement apart from what Praxis observed, and has a provenance class an agent cannot upgrade. `requirements-compiler verify-process` decides process, owner and review obligations per candidate. Owner decisions count only with a CI-observed attestation. | praxis #296, Indy-init #33 |
| 2. Verification plan before implementation | The v2 plan (EII-C) has per-obligation contracts. Rehearsal readiness was READY with 0 bound tests, and process obligations were classified as test categories. | Evidence classes and producers per category. New `process-record` (25) and `owner-decision` (3) categories; this is additive, and no rule was relaxed. The `plan-readiness` gate is BLOCKED until every mandatory mapping is resolved or permitted by frozen rules. Proposed producer bindings are left for the owner. | Indy-init #33 |
| 3. Integration overhead | `praxis streams check` (praxis#295) already provides ownership, shared interfaces, dependencies, artifacts, required tests, readiness and the dependency order. | No new mechanism. Retrospective: 36 of 58 main→stream merges had no path overlap. Rehearsal 6 rule ESO-R6-07. | analysis only |
| 4. Incremental verification | Dokimos content-addressed identity, freshness policies and affected-test selection (0.8.0); failing outcomes block reuse (dokimos#58); Ordo policy-aware freshness (ordo#108). | Mandatory final verification at the release checkpoint; Ordo reads per-obligation freshness. See §3. | dokimos #59, ordo #109 |
| 5. WASM preflight | `limen wasm-check` (limen#113) already covers all six required cases against the real interpreter. | Documented the build → wasm-check → browser order. No test cases needed. | limen #114 |
| 6. Browser environments | Test-environment contract and preflight (conditor#117). | `conditor test-env resolve` as the one source for endpoint and ports. Added the missing end-to-end incorrect-endpoint scenarios. | conditor #118 |
| 7. UI and persistence | Forma 0.11.0 type roles (forma#171), Arca 0.6.0 (arca#46). | Investigation only; nothing modified. R3-F2 is open. | [`investigations/regression-preservation.md`](investigations/regression-preservation.md) |

## 3. Verification and measurements

### Priority One: process evidence

- **Praxis.**
  - New tests: 4 domain and 3 CLI, all passing. They cover argument refusals, the classification table (an agent's
    statement is always an assertion; only a human records an owner decision; only undeclared CI automation is
    tool-observed; nothing is ever `verified-acceptance`), refusal of another actor's execution, and `praxis
    validate` accepting the new event.
  - Full suite in CI: 2440 of 2441 on the first commit. The one failure was the CLI boundary ratchet, which was fixed
    in the next commit. 7 tests that fail locally fail identically on unmodified `main` in this environment and pass
    in CI.
- **Simulated rehearsal.** It was recorded by the real Praxis CLI, with implementer, reviewer, owner and CI each
  acting under its own identity. The 13 records were captured as the work happened: 5 tool-observed, 4 agent
  assertions, 1 reviewer finding, 3 owner decisions. The reviewer finding and the owner decisions are anchored by
  CI-observed pull-request reviews.
  - With the proposed bindings, **30 of 30** process, owner and review categories verify for the candidate.
  - **43 of 43 negative controls** fail closed, each with its named diagnostic. They cover the nine required cases:
    - missing approval;
    - incorrect agent identity;
    - missing checkpoint;
    - unapproved scope change;
    - self-acceptance;
    - stale review;
    - incorrect commit identity;
    - missing release selection;
    - evidence reused from another candidate.

    The other 34 are from the independent reviews (§5). In addition, **3 record-level controls** refuse the record.
  - Evidence: `requirements-compiled/v2/readiness/evidence/process-evidence-simulation.json`.
- **What it would have changed in Rehearsal 5 (not measured causally).**
  - R5 recorded one checkpoint and 58 post-hoc attributions.
  - Its owner decisions were agent transcriptions. Under this change they stay agent assertions, so R5's owner
    obligations would still be unverified. That is the intended fail-closed result: no historical approval is
    manufactured.

### Priority Two: plan before implementation

Evidence: `requirements-compiled/v2/readiness/evidence/r5-readiness-replay.json` (read-only replay).

| Plan | Gate | Unresolved obligations | R5's 49 unproven flagged before implementation |
|---|---|---|---|
| R5 kickoff plan `VP-e77a2134e1b0` | BLOCKED | 326 (all unclassified) | 49 |
| v2 plan + R5's final bindings as written (277; none declares a category) | BLOCKED | 326 | 49 |
| v2 + R5's bindings, upper bound (each assumed to prove every application category its test kind can provide) | BLOCKED | 140 | 49 |
| the same + proposed process bindings | BLOCKED | 138 (104 application-test, 60 reviewer category gaps) | 47 |

- Of R5's 49 unproven obligations, **37 need process, owner or reviewer evidence**. The gate names them, with their
  class and producer, before implementation.
- The replay also exposes 39 obligations that R5 counted as passed through deterministic tests alone, although the
  plan requires independent-acceptance evidence for them. This is reported, not re-scored.

### Priority Three: integration

Retrospective, non-causal (`investigations/r5-merge-necessity.md`):
- 92 merges, 17 of them conflicted.
- 58 merges of main into a stream, of which 36 had zero path overlap (an upper bound on avoidable merges) and 8
  conflicted.
- No reduction in integration work is claimed.

### Priority Four: incremental verification

- **What already existed.** All of the following were already in Dokimos 0.8.0 and dokimos#58: change-impact
  analysis, dependency-aware selection, immutable identity (sources, dependencies, contract, configuration including
  runtime, tool version), freshness validation, failure-aware reuse, and a refusal with its own negative test for each
  listed case (source, contract, dependency, environment, live, security, independent acceptance).
- **Missing, now added** ([dokimos#59](https://github.com/kemiller2002/dokimos/pull/59)): mandatory final
  verification. At a `release` checkpoint every test runs (`REUSE.FINAL_VERIFICATION_REQUIRED`), and the command line
  can never lower it.
- **Ordo** ([ordo#109](https://github.com/kemiller2002/ordo/pull/109)) now reads each obligation's
  acceptance-contract freshness, which can only narrow reuse.
- **Test suites:** Dokimos 538/538; Ordo 950 passed, 1 skipped.
- **Reuse is not enabled by default.** The checkpoint defaults to incremental, and the release workflow must declare
  `--checkpoint release`.

Replay over Dokimos's last 12 commits (c99c21b..aae5413) on a shared 4-core machine. Raw data:
[`investigations/p4-dokimos-replay-partial.json`](investigations/p4-dokimos-replay-partial.json).

| Clean sequence | A: full after every change | B: dependency-aware | C: B + valid reuse |
|---|---|---|---|
| Tests executed | 5,790 | 2,034 | 2,034 |
| Duplicate verification (tests whose identity already passed) | 3,760 | 4 (the forced final run) | 4 |
| Wall clock | 1,726 s | 535 s | 520 s |
| Final release step | n/a | 535 tests, 124 s | 535 tests, 116 s |
| Incorrect reuse accepted | n/a | n/a | 0 of 396 |
| Seeded defects detected | 8 of 8, each at introduction (109–121 s) | **partial**: 3 of 12 steps run; a defect reported at step 2 was hidden at step 3 by selection | **unavailable** (not run) |
| Escaped defects | 0 | partial | unavailable |
| CI minutes, agent tool calls, tokens | unavailable | unavailable | unavailable |

- **Reading.** On clean history, B ran 35% of A's tests in 31% of A's time, and C added nothing beyond B: the same
  test set, with a 69.4% cache hit rate.
- **The defect result is incomplete and shows the risk.** Selection alone hid a seeded failure, which is why it must
  ship with failure-aware reuse and a mandatory final run.
- **Retrospective.** The earlier, complete 20-commit replay (SCORECARD row G) found selection alone visible on 4 of 9
  later defect commits, and with failure-aware reuse on 9 of 9.
- **Not established here.** No efficiency gain is claimed for C over B, and B's defect detection is not shown complete.

### Priority Five: WASM preflight

Static check:
- coverage of the six required cases confirmed;
- 166 of 166 Limen CLI tests;
- on R5's failing commit `ec6407b` it errors in 0.41 s (limen#113), and re-checks of the 12 examples took
  256–320 ms;
- R5's app was broken for about 90 min by commit timestamps;
- the CI time it would replace is unavailable.

### Priority Six: browser environments

- 1309 of 1309 Conditor tests (previously 1278).
- Each required fault is detected in preflight in 4–182 ms. A port held by a non-HTTP process takes about 2.0 s.
- Rehearsal 5 lost at least 4 CI browser runs to the hard-coded port.

### Priority Seven: regression preservation

- **UI.** UI protections are in Forma's CI on three engines, except the layout probe and Chromium-only conformance
  specs.
- **Persistence.** The persistence fixes exist only in Rehearsal 5's application, plus equivalent scenarios in
  Arca 0.6.0. R5 does not use Arca.
- **R3-F2.** R3-F2 (a non-atomic local read-merge-write across tabs) is **open**. Its race and a deterministic
  reproduction design are documented.
- **Arca reconciliation.** Arca's reconciliation accepts ancestor commits or idempotency keys without a byte
  read-back, which is the pre-fix shape of F-1. This was found by reading the code and has not been run.

## 4. Comparison table

Rehearsal 6 has not run. The "updated system" column can only be filled by a rehearsal that uses the changed stack,
so every rehearsal-level value is unavailable. Component-level effects are in §3.

| Metric | Baseline (R5) | Updated system | Change |
|---|---|---|---|
| Time to first working application | unavailable (bounded T+38.7 to T+287.4 min) | unavailable (R6 not run) | not calculable |
| Time to verified completion | not reached in 392 min (INCOMPLETE) | unavailable | not calculable |
| CI minutes | unavailable for the window; final run 24.45 job-min | unavailable | not calculable |
| Agent tokens | unavailable (not ingested) | unavailable | not calculable |
| Integration repairs | 26 reconcile commits, 2 explicit repairs, 17 conflicted merges | unavailable | not calculable |
| Mandatory requirements verified | 77 of 108 P0 (ledger); 277 of 326 obligations | unavailable | not calculable |
| Defects escaping early tests | ≥ 2 defect classes reached CI or later: the WASM start failure (~90 min) and the test-port fault (≥ 5 failed browser runs) | both are detected in preflight in replay (§3 P5, P6); rehearsal effect unavailable | not calculable |

No savings percentage is given.

## 5. Independent review

Independent agents, given the code and the requirements but not these conclusions, reviewed the Praxis and Indy-init
changes adversarially, in rounds, each against the fixed code.

| Round | Findings | Disposition |
|---|---|---|
| 1 | 11, of which 4 critical. Examples: failed or agent-asserted test results verified; one owner attestation approved any decision; an implementer could review under a review role; `verify-plan` trusted an assessment file; `plan-readiness` accepted an edited plan or self-supplied rules | all fixed, each with a control |
| 2 | 10, of which 3 high. Examples: a review existed by being written down; one owner approval covered any scope change; an honest owner rejection was ignored; a later failure, a superseding selection, or the implementer's own disposition was not caught; binding kinds were not re-checked | all fixed, each with a control |
| 3 | 5. Examples: later rejections could be bypassed or replayed; one owner approval satisfied unrelated owner obligations; the checkpoint status and the envelope role were self-reported | fixed (withdrawals always count; CI's latest review state decides; `ownerDecisions` per obligation; implementers from the frozen roster); the self-reported checkpoint status is documented as a limit |
| 4 | 3 (medium or lower): a non-approving review CI observed on another pull request was ignored; a scope change could rest on a path approved under another or a rejected decision; in `verify-plan` a passing ledger test could stand in for a bound Praxis producer | fixed (any CI-observed non-approval withdraws; decisions map to the paths they approve; producer-bound categories are covered only by the producer, and mixed bindings are refused at compilation) |

The simulation now runs 43 log controls and 3 record controls, all failing closed, while the positive control
verifies 30/30. Every round found real defects that the author's own tests had missed. The severity fell from round to round
(4 critical, then 3 high, then 3 high, then 3 medium or lower). That supports the initiative's rule that independent
review is required, not optional. It also means the absence of findings is not claimed: a fifth round was not run.

## 6. Remaining risks

- Praxis identities are self-reported. Owner and reviewer authenticity rests on CI-observed pull-request reviews,
  trusted only inside the acceptance job that fetched them. Test results and acceptance references rest on a
  self-reported CI run id. Checkpoint verification status and event ids are written into the same repository; event
  ids are unsigned content hashes. Deleting log lines can remove records, though not CI-observed withdrawals.
- Four review rounds were run. The fourth still found medium-severity issues (now fixed), so a fifth round should
  precede any reliance on `verify-process` for an acceptance verdict.
- `verify-plan`'s process-evidence path (`--process-*`) was exercised by unit-level code and reading, not by an
  end-to-end run against a real ledger and evidence bundle.
- A process record proves that a step was recorded when it happened, not that it was the right step. Those
  obligations keep their reviewer category.
- The proposed producer bindings and all Rehearsal 6 requirements need owner approval. Until a stack release
  carries the merged changes, Rehearsal 6 cannot use them (ESO-R6-01).
- Token and tool telemetry is still not ingested, so no efficiency claim about agents is possible (ESO-R6-06).
- R3-F2 is open. Forma 0.11.0 is merged but not published.
- CI census data for Rehearsal 5 was unavailable from this session. Attaching the repository for API access would
  allow it to be collected.

## 7. Rehearsal 6 preparation

ESO-R6-01 to ESO-R6-11 in the intake document. Rehearsal 6 is not started.
