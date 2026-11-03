# Rehearsal 009 content readiness: executable acceptance specification

**Status:** PROPOSED.
- The obligations, test identities and suites are in `proposed/content-obligations.proposed.json` (plan `VP-CONTENT-PROPOSED`: 12 requirements, 14 obligations, 22 identities).
- Dokimos 0.10.0 compiles the plan, and its scaffold creates the skeleton specs at these paths.
- On the skeletons, all 14 obligations are `invalid` (placeholder), and the gate refuses with 0/14 verified (FINAL-READINESS.md §Validation).
- The test **bodies** are written inside the competition window. Only the specification and the bindings are prepared now (competition policy, §9 of the assignment).

## Suites

| Suite | Runner | Fixture | `requiredBoundary` | Why |
|---|---|---|---|---|
| SU-CONTENT-LIVE | `npx playwright test --project=chromium tests/browser/live/content` | The deployed candidate at the exact SHA and the authorized GitHub test repository (the Rehearsal 8 live write target `kemiller2002/causa-write-test`, token `CAUSA_WRITE_TOKEN`, contents only); one fresh investigation per test | `fill`, `reload` | A test that never types content and never reloads cannot count. Dokimos 0.10.0 already rejects such a test as "does not reach the suite boundary" (proven: `evidence/dokimos-boundary-probe.log`). |
| SU-CONTENT-BROWSER | `npx playwright test --project=chromium tests/browser/acceptance/content` | The test build with the stateful GitHub fake | `fill` | Negative, mobile and conclusion cases must enter data through the UI. |

## INVESTIGATION-CONTENT-E2E (authoritative user-level scenario)

**File:** `tests/browser/live/content/content-e2e.spec.js`, `describe("INVESTIGATION-CONTENT-E2E")`, run against the deployed candidate and real GitHub.

**The demonstration text is historically grounded.** The test proves the application round-trips content. It does not prove the historical hypothesis itself.

| Step | Action | Assertion | Test (identity suffix) | Obligation |
|---|---|---|---|---|
| 1 | Clean application state (new browser context, storage cleared) | No investigation is open | creates Challenger STS-51L Disaster… | CONTENT-001-O1 |
| 2 | Connect to the authorized test repository | Connected | (same) | |
| 3 | Create "Challenger STS-51L Disaster" | The heading shows the name. The identifier is shown and is not derived from the name. | (same) | CONTENT-001-O1 |
| 4 | Enter the description: "Loss of Space Shuttle Challenger 73 seconds after launch on 28 January 1986." | Shown in the header | (same) | CONTENT-001-O1 |
| 5 | Enter the primary question: "Why did the right solid rocket booster field joint fail?" | Shown in the header | (same) | CONTENT-001-O1 |
| 6 | Add an observation: "Launch-morning ambient temperature was about 36 °F, far below previous launches; engineers raised O-ring temperature concerns." | The list shows that text; reopening shows the full text | records and reopens the O-ring temperature observation | CONTENT-003-O1 |
| 7 | Add a hypothesis: "Cold temperatures reduced O-ring resiliency so the field joint failed to seal." | Shown labelled as a Hypothesis, visually distinct | records the cold-temperature sealing hypothesis as a hypothesis | CONTENT-004-O1 |
| 8 | Add evidence: label "Rogers Commission finding"; description "The Commission attributed the failure to the O-ring seals of the aft field joint of the right SRB, with cold temperature a contributing factor."; source "Report of the Presidential Commission on the Space Shuttle Challenger Accident (1986), Vol. 1, Ch. IV" | Independently viewable with its source | admits textual evidence with its source reference | CONTENT-005-O1 |
| 9 | Link the evidence to the hypothesis as **Supports** | Shown on both the evidence and the hypothesis | links the evidence to the hypothesis… | CONTENT-006-O1 |
| 10–11 | Save; wait for **Saved** | Saved appears only after the read-back; it is never shown during Saving | reports Saved only after verified persistence | CONTENT-001-O2 |
| 12–14 | Reload in a **new browser context**; open the same investigation | The name, description, question, observation, hypothesis, evidence and relationship text all equal what was entered | reloads every entered field from GitHub in a new context | CONTENT-009-O1 |
| 15–17 | Edit the description; save; reload | The new description is shown; the identifier is unchanged | edits the description and keeps the identifier after reload | CONTENT-002-O1 |
| 18 | Open the Timeline | Creation and revision events with actor and time; the earlier description is available | keeps provenance and event history intact after edits | CONTENT-008-O1 |

**Evidence each run must produce:**
- the Playwright JSON report;
- the candidate SHA it ran at (the gate checks it equals HEAD);
- the commit SHAs GitHub returned for each save;
- a screenshot at 390 px after step 14.

## Negative and failure scenarios

| Scenario | Expected, visible and recoverable | Identity | Obligation |
|---|---|---|---|
| Missing investigation name | A field error; nothing created or saved | `content-negative.spec.js > … > refuses a missing investigation name` | CONTENT-010-O1 |
| Empty observation | A field error; nothing recorded | `… > refuses an empty observation` | CONTENT-010-O1 |
| Empty hypothesis | A field error | `… > refuses an empty hypothesis` | CONTENT-010-O1 |
| Invalid source URL (`javascript:alert(1)`, `notaurl`) | A field error; never rendered as a link | `… > refuses an invalid source URL` | CONTENT-010-O1 |
| Invalid relationship target (deleted or unknown ID through a crafted submission) | Refused with a blocker; no relationship recorded | `… > refuses an invalid relationship target` | CONTENT-010-O1 |
| Duplicate submission (double tap, retried request) | Recorded once | `… > records a duplicate submission once` | CONTENT-010-O2 |
| Save failure (the fake returns 500) | Failure state, content kept, Retry offered; never Saved | `… > keeps content and offers retry after a save failure` | CONTENT-011-O1 |
| GitHub authentication failure (401) | An authentication message, content kept, reconnect offered | `… > shows an authentication failure without losing content` | CONTENT-011-O1 |
| Concurrent edit conflict (another writer moves the head) | The conflict state of SM-CONFLICT, with the user's content preserved | `… > shows a concurrent edit conflict` | CONTENT-011-O1 |
| Reload with missing or malformed data | A visible error naming the problem; nothing invented | `… > reports missing or malformed data on reload` | CONTENT-011-O1 |
| Stale candidate SHA in verification evidence | The gate refuses (already enforced and tested: `gate-refuses-stale-sha-results`, `gate-refuses-stale-matrix`, `gate-refuses-previous-commit-sha` in `tools/r9/tests.fsx`) | n/a (gate) | All |

**Mobile cases:** `content-mobile.spec.js` (CONTENT-012-O1):
- forms fit and wrap at 320 and 390 px;
- touch targets are at least 44 px;
- there is no overflow;
- keyboard and screen-reader operation works.

**Conclusion case:** `content-conclusion.spec.js` (CONTENT-007-O1). A conclusion is recorded and displayed distinctly. The resolution gate still blocks resolution while its conditions are unmet.

## Why a command-only or label-only test cannot pass

Four checks apply, all existing mechanisms, proven in this change:
1. **Binding.** Each content obligation is bound to the identities above. Dokimos counts it `verified` only when every bound identity passed at the candidate.
2. **Boundary.**
   - In SU-CONTENT-LIVE, a test that never calls `fill` or `reload` is `invalid` ("does not reach the suite boundary"); proven in `evidence/dokimos-boundary-probe.log`.
   - In SU-CONTENT-BROWSER, a test that never calls `fill` is `invalid`.
3. **Fault sensitivity.** The round-trip obligations carry category `content-round-trip`. The gate runs with `--require-fault-sensitive content-round-trip`, so each needs a detected Dokimos fault probe at the candidate. A test that passes whatever the content is cannot count. This is proven by `gate-content-without-probe-refused` and `gate-content-with-detected-probe-passes` in `tools/r9/tests.fsx`.

   **The probes are written at kickoff**, once the code they mutate exists. Each one's anchor must occur exactly once:

   | Probe | Fault | Detected by |
   |---|---|---|
   | P-CONTENT-WIRE | the event serializer drops the payload body | step 12–14 reload assertions |
   | P-CONTENT-PROJECTION | the projection ignores a revision | step 15–17 |
   | P-CONTENT-VIEW | the list renders the event label instead of the text | step 6 |
   | P-CONTENT-RELATION | the relationship kind is dropped | step 9 / 12–14 |
4. **Candidate binding.** Every result, coverage report and matrix must name the checked-out HEAD (`r9.fsx gate`).

**In Praxis:**
- A work item implementing content completes only through the gate with `--scope CONTENT-00n` (TRACEABILITY.md).
- `check-governance` refuses any completion without that PASS verdict.

## Gate commands in the run

```bash
# per work item (CI, so record evidence is observed in the run; inputs work_item, scope)
gh workflow run "Verification gate" -f mode=development -f work_item=WI-ID -f scope=CONTENT-003
# the workflow runs `dokimos verification probe` on .echelon/fault-probes.json at HEAD and always passes
#   --probes <results> --require-fault-sensitive content-round-trip   (tools/r9/verification-gate.yml)
# release (T34): mode=release, the whole plan including CONTENT-*, live suites with their secrets
```

**Prerequisite.** These checks bind once the CONTENT-* obligations are part of the approved plan (ODR-V413-01). Until then, they are this specification and not a gate.
