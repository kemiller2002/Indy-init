# Rehearsal 010 optimization: final report

**Every duration below is a model result from estimates** (`tools/r10/schedule-model.fsx`; commands in SCHEDULE-COMPARISON.md and TIME-TO-WORKING-APPLICATION.md). **None is an observation.** Rehearsal 010 has not started, and its 420-minute clock has not run. Nothing here has been demonstrated in a timed run.

## What was done

**Merged to main** (verified, three independent review rounds, final verdict PASS, 15 of 15 CI checks green):
- **#118, launch fixes:**
  - the builder could not produce the three authorization inputs;
  - a suite-preflight fix after the bootstrap would have been refused;
  - main's post-merge verification runs could be cancelled.

**PR #119** (not merged: it contains proposals that only the owner can adopt):

| Change | Kind | Verified by |
|---|---|---|
| Extended schedule model: file ownership by role, speculative queue depth, slice-scoped defects and repair, per-slice overhead, estimate scale, CPM, functional milestones and milestone-first ordering | F# tooling | `schedule-model-tests.fsx`: equivalence with both committed baselines, invariants, monotonicity, exact repair and overhead costs, milestone cases, refusals. Runs in Preflight tooling CI |
| Governed work-item split: `scope compile --split`, `Scope.applySplit` with 19 refusal codes judged on the post-split graph, preflight of work items and of the recorded plan | F# compiler | Offline suite: approved scope byte-identical (`59bfef5a…`), successor content identical, 14 mutants each refused with its code, forged-successor controls, milestone-aligned plan reproduces its digest |
| Successor scopes `a847f98c…` (53 items) and milestone-aligned `723c9eb3…` (57 items) with slice manifests and inventories | proposals | Compiled through the governed path; preflight PASS; all 205 requirements and 890 obligations identical apart from work-item assignment |
| Speculative verification semantics (VERIFICATION-OPTIMIZATION.md §7), controls S1–S7 | **design only** | not built |
| Initialization bundle (INITIALIZATION-OPTIMIZATION.md §3) | **design only** | not built |
| Performance metrics (PERFORMANCE-METRICS.md) | **design only** | not built |
| Causa data repository investigation, milestone derivation | analysis | read-only; independent re-check in review round 3 |

**Unchanged:**
- the approved scope, the proposed lock, every owner decision and approval;
- `kickoff/`, `docs/rehearsal-010-readiness/`, `requirements-compiled/` and the frozen v4.1.4 package;
- the deadline policy;
- the observer and the verification gate's acceptance logic.

## The ten questions

**1. What actually causes the longest delays?**
- **The one `ui` agent.** It owns 564 lane-minutes of work, almost all of it only startable after T17 (about T+150–170), in files nobody else may edit.
- **Serialized verification at realistic CI times.** At 10-minute CI, slices wait 453 slice-minutes in the queue.
- **The ten-slice critical chain.** It costs about 10 minutes per minute of CI.

Under the frozen ownership the honest baseline finishes at T+720 even with zero-minute CI, against a 409-minute critical path (CRITICAL-PATH.md).

**2. How much time is spent implementing versus waiting?**
Inside the window (V1, 10-min CI), six lanes have 2,250 lane-minutes after set-up:
- about 900 go to implementation;
- 1,337 are idle, waiting for dependencies or for a role's files;
- in addition, slices spend 453 slice-minutes queued for verification, and 370 CI-minutes in verification.

**3. Which bottlenecks were eliminated?**
In the model, under owner decisions not yet taken:
- queue waiting falls from 453 to 22 slice-minutes (speculative queue);
- planned set-up falls from 45 to 25 minutes;
- milestone-first ordering removes the delay of usable capability behind tail work.

In the delivered code:
- three launch defects that would have stopped or corrupted the run (#118);
- the absence of any governed, verified way to split work items.

**No bottleneck has been eliminated in an executed run.**

**4. Which bottlenecks remain?**
- The `ui` ownership concentration, which needs v4.1.5, ODR-R10-20.
- The critical chain's sensitivity to CI time.
- Serialized verification, until §7 and S1–S7 are built and reviewed.
- Merge and supervision time, which the model treats as free.
- Unmeasured per-slice overhead.
- **The contract gap that makes M2 impossible under v4.1.4 (ODR-R10-25).**

**5. How much faster is the optimized schedule under identical assumptions?**
At 10-min CI:

| | V1, approved plan | V4: successor + queue + 25-min set-up | V5: V4 + v4.1.5 ownership (hypothetical) |
|---|---|---|---|
| Full completion | T+794 | T+756 (−38) | T+563 (−231) |

Time to a working application (R0 → R2, milestone-first):
- **M1 connect:** T+681 → T+283, conditional on ODR-R10-28, and on -29 A (else -25);
- **M2 browse:** T+794 → T+309, conditional on ODR-R10-25;
- **M3 create:** T+739 → T+489, conditional on ODR-R10-25 and -26.

These are schedule figures. Under v4.1.4 as frozen, M1, M2 and M3 cannot pass against the real data repository (review RVO-3-1).

**6. How many more requirements and obligations are expected to be verified within 420 minutes?**
At 10-min CI:
- **V4:** +31 requirements (111 → 142) and +130 mandatory obligations (435 → 565, +30%).
- **Milestone-first (R2):** 527 obligations (+92) and, on the schedule, two usable milestones instead of none. Against real data they become usable only with ODR-R10-25, -28 and -29.
- **With a 5-minute per-slice overhead,** V4's gain falls to +40 obligations.
- **Under rework** (every 4th slice fails once), V4 gives 482 against 302.

**7. What evidence demonstrates that correctness was preserved?**
- The approved scope recompiles byte-identically.
- Both successors keep every requirement's obligations, priority and mandatory flag. This is checked field by field in the offline suite, and confirmed independently in review rounds 1–3.
- The split refuses dropped, duplicated, foreign or reordered work, cycles, ownership changes and frozen DAG tasks. 14 of its 19 refusal codes have a mutant in the offline suite.
- No test was weakened or removed, and no gate logic was relaxed. The only gate change (#118) makes main's runs **un**cancellable.
- §7 strengthens `STALE_TARGET`, and is not built.
- Review: ADVERSARIAL-REVIEW.md. The implementing agent certified nothing itself.

**8. Which owner approvals are still required?** OWNER-DECISIONS-REQUIRED.md:

| Decision | What it covers |
|---|---|
| ODR-R10-19 | which successor scope, if any |
| ODR-R10-20 / -21 | v4.1.5 ownership and DAG splits |
| ODR-R10-22 | speculative verification, after its code and controls exist |
| ODR-R10-23 | 25-min set-up |
| ODR-R10-24 | toolchain bundle |
| **ODR-R10-25** (critical) | contracts able to read causa-data |
| ODR-R10-26 | content fields with no wire form |
| ODR-R10-27 | milestone-first ordering |
| ODR-R10-28 | read and write tokens, and the App installation on `causa-acceptance-r10` and `causa-data` |
| ODR-R10-29 | M1's boundary |
| **ODR-R10-30** | the read-only data-browser slice SL-M0 |

None is recorded. Agents do not record approvals (RDP-003).

**9. Can the full scope now plausibly finish within seven hours?**
**No.**
- The critical path alone is 389–409 minutes at zero CI, and passes 420 at about 1–2 minutes of CI per integration, even with unlimited agents.
- The best modelled plan (V5, hypothetical) finishes at T+510 at zero CI and T+563 at 10-minute CI.
- A working application, M1 plus M2, plausibly can within the window: about T+260–310, but only with ODR-R10-19, -22, -23, -25, -27, -28 and -29, and the gate code for -22.
- **M3 inside the window needs v4.1.5 ownership (R4)**, or zero CI with the milestone-aligned split (R5: T+418), and in either case ODR-R10-25 and -26.

**10. What is the single highest-value remaining optimization?**
**A v4.1.5 successor package** that, in one compile, review and freeze cycle:
- rebalances UI file ownership (ODR-R10-20; −231 minutes of full completion);
- makes the contracts able to read the real catalog (ODR-R10-25).

Without the second, no amount of scheduling delivers the historical browsing capability.

## Addendum: a working application through progressive capabilities

| | Approved plan (R0) | R2 | R5 |
|---|---|---|---|
| Milestones usable at T+120 / 240 / 360 / 420, 0-min CI | 0 / 0 / 0 / 0 | 0 / 1 / 2 / 2 | 0 / 2 / 2 / 3 |
| Same, 10-min CI | 0 / 0 / 0 / 0 | 0 / 0 / 2 / 2 | 0 / 0 / 2 / 2 |

These are schedule counts. Under v4.1.4 as frozen, none of M1–M3 passes against real data without ODR-R10-25, -26, -28 and -29.

R2 is milestone-first ordering; R5 adds the milestone-aligned split.

- **Data repository.** Apollo 1, Apollo 11 and Challenger are present in `kemiller2002/causa-data`, with 432, 431 and 452 events. The frozen contracts cannot represent them (DATA-REPOSITORY-CONTRACT.md §6 C1). This is the most important finding of the addendum. It means that a Rehearsal 010 launched on v4.1.4 will fail EXT-DATA-013, T25 and the M2 journeys against real data, whatever its throughput. That must be declared before T0, not discovered after it.
- **The causa repository** could not be read from this session (HTTP 403). Nothing is taken from it.

## Addendum 2: the minimum data browser (M0)

**The smallest correct application** is MINIMUM-DATA-BROWSER.md §2:
- the frozen Connect form and credential;
- a read adapter for `catalog/active.json`, then the catalog manifest, then each case's `events.ndjson`;
- a generic JSON and NDJSON reader;
- a list view and a raw-event detail view, labelled "not replayed or validated";
- six error states.

It uses no `CoreEvent`, `Replay` or save machine.

**Evidence that it is small:**
- A 45-line F# probe read the real catalog and all four histories (1,363 events, each count equal to its manifest) in 1.6 s.
- It also classified missing-file, malformed-JSON and network errors.
- Rehearsal 009 made its first application commit, with GitHub reads and writes, at T+21 (measured commit time). Its release gate later refused the candidate (0/541 plan obligations), so this is evidence of implementation effort only.

**Why it is not delivered within 120 minutes:** packaging and governance, not technology (DATA-VISIBILITY-DEPENDENCIES.md).
- Every requirement that lists or opens a case is authoritative (EXT-DATA-013, T25: "replayed through the engine").
- Those requirements sit in items behind the kernel checkpoint (T17), the save machine (T19 → T20) and replay (T15 → T25), and each slice needs a full gate run.
- Under OD-INDY-22, no M0 code may be written before T0, so it was not built here.

**With ODR-R10-30** (one slice, no requirement claimed, depending on T10 only), the model gives **M0 at T+116 / 125 / 135** at 0, 5 and 10-min CI, at no cost to completion or obligations at 10-min CI. Inside 120 minutes only at near-zero CI on today's estimates. The remaining gap is the set-up and bootstrap estimates (25 + 29 planned; measured machine parts: 3–6 min of set-up and a 2-second bootstrap generation, plus R9's bootstrap commit at T+2).

The read-only browser is **technically achievable, and blocked by governance**.

**Review round 5** (653bfbb) passed. It confirmed:
- the causa-data facts;
- the Rehearsal 009 timeline;
- every dependency row and every TIME-TO-FIRST-DATA number;
- that no M0 code was committed;
- that nothing claims replay.

Its wording findings RVO-5-1..7 were fixed.

## Execution readiness (continuation)

**Launch blocker, found by Agent 2 (CF-07, PR #121) and confirmed here.** Under the frozen slice gate's journeys rule (Ledger.fs 391–410), the approved plan cannot complete. SL-T16, the shell, is bound to J2-08, which needs slices that depend on T16; slices sharing J1-03 or J1-06 need each other. All 42 merge-dependency edges lie on cycles. Every milestone time in these documents assumes the literal CF-07 option A (assertions whose needs are not yet merged are judged at release). It is not a bound. Under a stricter greedy cycle-breaking deferral, Browse (M2) moves past 420 minutes in every plan, and Connect (M1) does too except in the combined plan at 0-min CI (T+416). See [execution-readiness/CROSS-AGENT-FINDINGS.md](execution-readiness/CROSS-AGENT-FINDINGS.md).

Full report: [execution-readiness/FINAL-REPORT.md](execution-readiness/FINAL-REPORT.md).

**In summary:**
- **Integration structure.** The frozen v4.1.4 package already provides independent module ownership, complete compile lists and one composition owner. It was demonstrated that UI code compiles against contracts and stubs with no domain engine, and that a GitHub adapter replaces a fixture without screen changes. Contract drift fails to compile only while `TreatWarningsAsErrors` stays on.
- **Change-aware Level 1/2 selection.** Added as a builder tool with 22 adversarial cases, never part of the gate.
- **Measurements.** The CI queue is 3–9 min; builds take seconds.
- **The combined plan, E8** (needs ODR-R10-19..32 and the unbuilt §7 gate). At 10-min CI:
  - M0 T+120 (data visible);
  - M2 T+239 (authoritative list);
  - M3 T+387 (create);
  - full completion 560–630 across queue depths (not a single point: completion is non-monotonic in queue depth).
- **Without the speculative gate,** M2 and M3 slip 75 and 278 min at 10-min CI. Full completion is non-monotonic in queue depth (560–803 across depths and CI values), so no single completion gain is claimed.
- **The new dominant bottleneck,** for M2 and M3 at 5–10-min CI, is serialized verification; for full completion it is the release tail T31–T34.

## Round-3 independent review

Round 3 (on 1ead513) returned **FAIL** on one documentation finding. The code, data, tests and every reproduction held. All findings were fixed. Round 4 (03825ca) re-checked every fix independently, including the reviewer's own mutations, and returned **PASS**. Its three minor findings and one note were then fixed (ADVERSARIAL-REVIEW.md §B).

The addendum's eight checks:

| # | Check | Verdict |
|---|---|---|
| 1 | Data from GitHub | PASS: independent anonymous clone; cases, counts and missing union cases confirmed |
| 2 | No write credential for browsing | PASS |
| 3 | One canonical model | PASS |
| 4 | No requirement lost | PASS |
| 5 | No test weakened | PASS |
| 6 | Save semantics unchanged | PASS |
| 7 | Runnable after each milestone | PARTIAL, and correctly so: under v4.1.4 no milestone M1–M3 can pass against real data |
| 8 | No relabelling | PARTIAL until RVO-3-1 was fixed |

## Verdict

**PARTIALLY OPTIMIZED**

M0 (addendum 2) is specified, measured with a probe, scheduled and proposed (ODR-R10-30). It is not implemented, because the contest rules (OD-INDY-22) forbid building the application before T0.

What is delivered and verified:
- the launch fixes (merged);
- the planning tooling and the governed split.

What is not delivered:
- The largest modelled gains depend on owner decisions that are not taken, and on gate code that does not exist (§7, S1–S7).
- Under v4.1.4 as frozen, the functional milestones M1–M3 cannot pass against the real data repository. M2 needs a contract successor (ODR-R10-25); M1 and M3 depend on it and on ODR-R10-26, -28 and -29.
- No gain has been demonstrated in an executed run.
