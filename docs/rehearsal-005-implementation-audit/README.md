# Rehearsal 5 implementation-versus-verification audit

**The historical outcome is unchanged: Rehearsal 5 is NOT ACCEPTED (verdict INCOMPLETE).** This audit is decision support. Its classifications are audit judgements, not acceptance results. Every figure below that goes beyond the frozen ledger is labelled post-rehearsal.

## Executive summary

Of the 31 P0 requirements Rehearsal 5 left NOT_RUN, **none is missing outright**:
- **3** are fully implemented and lack only admissible acceptance evidence.
- **7** are partially implemented.
- **3** cannot be established without a deployed exact-SHA run that is no longer possible.
- **18** are process or governance requirements whose evidence was never produced in admissible form.

The 49 missing obligations fail mainly on verification design and governance (33), not on development (5).

The gaps that genuinely need code are small. Focus placement after back navigation and acceptance-tooling work on AR-011 and AR-019 are needed whatever the owner decides. Four further gaps need code only if the owner keeps the requirement clause over an existing owner decision (DEC-025) or rules that the clause applies: amber indicator contrast, role/access control, a permission check and anonymous catalog read.

**Recommendation for Rehearsal 6: a combination, weighted heavily to verification and evidence automation and to engineering-process changes, with a small amount of application work.**

| Classification | Requirements |
|---|---|
| Verified during rehearsal | 77 |
| Implemented, verification missing | 3 |
| Partially implemented | 7 |
| Not implemented | 0 |
| Indeterminate | 3 |
| Process evidence missing | 18 |
| **Total** | **108** |

Application-only (75 requirements: UI, PF, EXT-DATA and WDL, excluding PF-019): 61 verified, 3 implemented with verification missing, 5 partial, 0 not implemented, 3 indeterminate, 3 process evidence missing (UI-021, UI-030, UI-040, whose application obligations all passed).

| Metric (audit classification, not acceptance) | Value |
|---|---|
| Implementation coverage: (verified + implemented) / 108 | 74.1 % (80 / 108) |
| Application implementation coverage | 85.3 % (64 / 75) |
| Application behaviour coverage, also counting UI-021, UI-030 and UI-040 | 89.3 % (67 / 75) |
| Verification gap: implemented, without acceptance evidence | 3 (EXT-DATA-006, PF-005, PF-009) |
| Functional gap: a specified behaviour missing | 7 (needs code whatever the owner decides: UI-011, plus tooling AR-011 and AR-019; code only if the owner keeps the clause: EXT-DATA-005, PF-015, UI-016, UI-031) |
| Evidence gap: remaining work is tests, binding, deployed runs or process evidence only | 24 |
| Unknown gap: implementation status cannot be established | 3 (PF-014, UI-015, WDL-014); plus 10 process requirements whose performance is UNKNOWN |

Indeterminate requirements are not counted as passes anywhere.

## Baseline (frozen; read once and indexed)

| Metric | Rehearsal 5 |
|---|---|
| P0 requirements | 108 |
| P0 PASS | 77 |
| P0 NOT_RUN | 31 |
| P0 FAIL | 0 |
| Verification obligations | 326 |
| Obligations PASS | 277 |
| Obligations NOT_RUN | 49 |
| Obligations FAIL | 0 |
| Gates PASS | 8 (G0–G7) |
| Gates NOT_RUN | 2 (G8, G9) |
| Final verdict | INCOMPLETE (NOT ACCEPTED) |

Sources:
- `kemiller2002/indy-init-rehearsal-005` at `b972cb4`:
  - `evidence/rehearsal/final.json`
  - `evidence/acceptance/run-40620099844-1b9fb56/verdict.json`
  - `bundle/traceability.json` (sha256 `bff1e56f…`)
  - `bundle/ledger.json`
- [`docs/REHEARSAL-005-RESULTS.md`](../REHEARSAL-005-RESULTS.md).
- Requirement identities, text, acceptance criteria, gates, rules, required evidence and dependencies come from the frozen plan VP-e77a2134e1b0 (`git show 4b38213:requirements-compiled/v1/verification-plan.json`, file sha256 `f79efdfa…`, which equals the preflight record). Nothing was reconstructed from summaries.
- The 31 NOT_RUN identities are the same in `traceability.json` and in `verdict.json` reasons, and they match the expected list.

**PR #32** was already merged when the audit began. It changes evidence binding and, in addition, two freshness policies (AR-010, WDL-012); see [verification-findings.md](verification-findings.md#verification-plan-curation-indy-init-pr-32).

## Methodology

1. **Index.** The 49 NOT_RUN obligations and the 53 passing obligations of the same 31 requirements were extracted with their frozen text and bound tests. Each VPO id was traced through history (`git log -S`) to find tags that a review removed.
2. **Inspect.** The F# source and tests for each requirement were read at 1b9fb56: persistence, catalog, Sync, kernel focus code, CSS tokens, the graph and the CI workflow. Exact paths and symbols are in [requirement-matrix.json](requirement-matrix.json).
3. **Execute, read-only against the candidate.** All runs used an isolated worktree, which stayed clean:
   - the candidate's .NET suite, online and offline;
   - its browser suite, locally and against the deployed bytes;
   - 13 focused new F# tests;
   - an F# Playwright probe for contrast, scroll and focus;
   - Indy-init's verifier tests at the pinned ref.

   See [implementation-findings.md](implementation-findings.md) and [post-rehearsal-evidence/](post-rehearsal-evidence/).
4. **Classify.** Each requirement got exactly one class from the brief, plus a process sub-status for class E. Each missing obligation got a primary cause and secondary causes from the brief's 12.
5. **Independent review.** One adversarial reviewer with no implementation or classification role challenged every claim. Its changes were adopted or rejected on evidence ([independent-review.md](independent-review.md)): 3 classification changes, 7 sub-status changes, 5 withdrawn "sufficient evidence" claims, 7 cause corrections, 1 rejection.
6. **Validate.** `indy-init:tools/rehearsal-005-audit/validator` is an F# tool that checks the ten integrity conditions of section 17 against the frozen evidence: count, identities, single class, sums, evidence for implemented claims, investigation for missing ones, historical and post-rehearsal separation, an unchanged verdict with sha256 of the frozen files, no silent exclusion, and review inclusion. It passes; mutated copies fail the targeted checks.

   ```
   dotnet run -c Release --project tools/rehearsal-005-audit/validator -- docs/rehearsal-005-implementation-audit <indy-init-rehearsal-005 checkout at b972cb4>
   ```

**Limits.**
- Live-provider tests were not run: no credential, and they would write to the test repository.
- Pages now serves b972cb4, so no exact-SHA deployed run is possible.
- WebKit was not run.
- The Percepta CLI was not installed, so `verify-app` was not re-run; CI's result is cited.
- The offline run redirected proxy variables rather than using a network namespace.
- Effort is given as relative sizes only; no hour estimates, because no measured basis exists.

## Answers

**1. How many of the 31 were actually implemented?** 3 fully: EXT-DATA-006, PF-005 and PF-009. 13 application or tooling requirements have their product behaviour largely in place: these 3, the 7 partial requirements, and the 3 indeterminate ones, whose code exists but whose deployed run is missing. Of the 18 process requirements, 3 (UI-021, UI-030, UI-040) have all their application obligations passing.

**2. How many were partially implemented?** 7: EXT-DATA-005, PF-015, UI-011, UI-016, UI-031, AR-011 and AR-019. The last two are acceptance tooling.

**3. How many were genuinely missing?** 0 requirements. The missing behaviours are clauses inside partially implemented requirements:
- the permission check (EXT-DATA-005);
- anonymous catalog read (PF-015);
- role/access control (UI-031);
- focus after back navigation (UI-011);
- 3:1 amber indicators (UI-016);
- drift classification (AR-011);
- R3 → BLOCKED (AR-019).

Two of them, the permission check and anonymous read, are absent by owner decision DEC-025.

**4. How many were blocked primarily by verification or process evidence?** 24: 3 A, 3 D and 18 E. By obligation, 33 of the 49 trace to plan defects (19) or governance evidence never produced (14). Testing accounts for 6, evidence collection and binding for 4, development for 5, and other for 1.

**5. How many more could have passed with evidence that existed at the deadline?** 1, EXT-DATA-006. Its fix's tests ran and passed in run 40620099844, and only the binding was missing. The five further candidates of the draft were withdrawn at review: their evidence was pre-run and agent-local, reconciled after the fact, or not candidate-bound.

**6. How much additional application development would complete acceptance?**
- Needed whatever the owner decides: one small application change (focus after back navigation) and two small tooling changes (SDK pin and drift comparison; the R3 verdict, or an amendment to O3).
- Needed only if the owner keeps the clauses: a small CSS change (amber indicators), a small permission check, a medium anonymous-read path, and a medium role/access model.
- All other remaining work is tests, a working deployed-mode harness, evidence binding, process records and owner decisions.

Complete acceptance of Rehearsal 5 itself is no longer achievable. Its exact-SHA deployed runs and its during-run process records cannot be produced retrospectively.

**7. Which engineering-system improvements would have prevented the most missing obligations?**
1. Process and owner evidence classes with `verify-process`, already built in Indy-init after R5: 33.
2. Preflight and pre-run records as bindable producers: 6.
3. A working deployed-SHA browser mode, plus pinned fixtures: 4.
4. Re-binding after review fixes: 3.
5. Candidate-bound, attested reviews: 3.

Details are in [rehearsal-006-recommendations.md](rehearsal-006-recommendations.md).

**8. What should be completed before Rehearsal 6?**
1. Owner decisions: DEC-025 versus PF-015 and EXT-DATA-005, UI-031 roles, UI-016 indicators and the AR-019-O3 wording.
2. Adopt the v2 plan with `plan-readiness` and `verify-process`, and dry-run them.
3. Fix the deployed mode of the browser guard, pin causa-data in every job, and pin the .NET SDK.
4. Re-bind after reviews.
5. Fix UI-011 focus, and whatever the owner keeps.

Follow-ups are filed in [`input-documents/2026-10-28-R5A-001-rehearsal-5-audit-follow-ups.md`](../../input-documents/2026-10-28-R5A-001-rehearsal-5-audit-follow-ups.md).

## Recommendation

**Rehearsal 6 should focus on a combination of verification and evidence automation (first) and engineering-process improvements (second), with only small, targeted application development.** Rehearsal 5 built nearly all of the software. Of the 75 application requirements, 67 have their application behaviour verified or implemented, and none is absent. What stopped the acceptance system from recognising it was:
- a plan that could bind only executed tests to process, aggregate and decision-conflicted obligations;
- process records that were never made while the agents worked;
- a deployed-SHA test mode that could not run;
- tags removed by reviews and never restored after the fixes.

More application code would not have changed the verdict.

## Files

| File | Content |
|---|---|
| [requirement-matrix.md](requirement-matrix.md) / [.json](requirement-matrix.json) | One row per unverified P0 requirement: classification, references, causes, remaining work, confidence, review record |
| [implementation-findings.md](implementation-findings.md) | Source, behaviour and post-rehearsal execution findings |
| [verification-findings.md](verification-findings.md) | Per-obligation causes, the PR #32 curation analysis, Question 5 |
| [process-evidence-findings.md](process-evidence-findings.md) | Process requirements and what can be established retrospectively |
| [independent-review.md](independent-review.md) | Reviewer findings and how each was resolved |
| [rehearsal-006-recommendations.md](rehearsal-006-recommendations.md) | Ranked actions |
| [summary.json](summary.json) | Machine-readable totals and metrics |
| [post-rehearsal-evidence/](post-rehearsal-evidence/) | Every post-rehearsal result. None is historical acceptance evidence |
