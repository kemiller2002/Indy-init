ECHELON FOUNDRY

INDY INIT REHEARSAL 7

Starting-State Contract and Source Provenance

Owner: kemiller2002
Rehearsal: 007
Application: Causa
Execution: Preparation only
Timed rehearsal: Not authorized

⸻

1. Mission

Close the gap that let Rehearsal 6 begin from Rehearsal 5's application source.

Root cause (Rehearsal 6 import investigation, 2026-10-29):

* On 2026-10-28 at 19:58:57Z, the coordinating agent decided on its own to import Rehearsal 5's source (indy-init-rehearsal-005 b972cb44) "as the starting point".
* No owner instruction asked for the import.
* The agent had committed at 18:55:49Z to "infrastructure only … no application code".
* It read the owner script's Stage 1 check 10 at 19:57:53Z, 64 seconds before deciding.
* Commit 506cd8c imported it at 20:08:58Z, inside the clock.

Contributing gaps:

1. The clean-start rule was written as a preflight check, not as a rule that holds during the timed run.
2. "Retain proven runtime configuration" and "Preserve the Rehearsal 5 persistence fixes" could be read as permission to carry code forward.
3. No verification checked the provenance of application source after the start.
4. The Rehearsal 6 results compare Rehearsal 5 and Rehearsal 6 without disclosing that Rehearsal 6 began from Rehearsal 5's code.

Deliver the smallest set of changes that makes Rehearsal 7 a genuine requirements-driven build:

* P0: a starting-state contract, in force for the whole timed run, and the owner decision it needs.
* P1: a mechanical provenance check in the acceptance job, and source classification in results reports.
* P2: an intake document for Praxis only. Do not implement it.

Do not build anything beyond this.

⸻

2. Operating Rules

Do not start Rehearsal 7.

Do not modify kemiller2002/indy-init-rehearsal-005 or kemiller2002/indy-init-rehearsal-006.

Do not modify completed rehearsal evidence or change any rehearsal's verdict.

Do not write owner approval records. Draft them for the owner. A decision takes effect when the owner merges the pull request that records it (RDP-003).

Do not prohibit legitimate reuse of shared Echelon components: Limen, Forma, Folio, Arca, Percepta, Praxis, Ordo, Dokimos, Conditor, and released packages pinned in echelon-current.

Do not add new services, frameworks, or runtime dependencies. Use git, the .NET SDK already pinned in global.json, and existing Indy-init tooling.

Write code in a functional style: pure functions, immutable data, effects at the edges.

Every check needs a negative test that proves it rejects what it should.

Open draft pull requests. Do not merge without the owner's approval.

No silent shortcuts. No fabricated evidence. Do not report anything as verified unless it was verified.

⸻

3. Repositories

Primary:

* kemiller2002/Indy-init

Read only:

* kemiller2002/indy-init-rehearsal-005 (reference for the provenance check)
* kemiller2002/indy-init-rehearsal-006 (reference and validation input)
* kemiller2002/indy-init-rehearsal-007 (confirm it still holds only its initial commit)
* kemiller2002/conditor (scaffold output)
* kemiller2002/echelon-registry (released component set)

Intake document only:

* kemiller2002/praxis (.praxis/inbox/documents)

Read first:

* Indy-init docs/rehearsal-007/ (README, EXECUTION-PLAN, READINESS, AUTONOMOUS-EXECUTION)
* Indy-init kickoff/rehearsal-006/ (README, owner-decisions.json, acceptance-process.yml)
* Indy-init docs/rehearsal-006/PREFLIGHT.md
* Indy-init docs/REHEARSAL-006-RESULTS.md
* Indy-init docs/IMPLEMENTATION-SLICES.md ("Pre-kickoff factory rehearsal")
* Indy-init README.md, line 9, and docs/EXPERIMENT-PLAN.md

⸻

PART I: P0, REQUIRED BEFORE REHEARSAL 7

4. Starting-State Contract

Create:

* kickoff/rehearsal-007/STARTING-STATE.md
* kickoff/rehearsal-007/starting-state.json

The contract applies from the official start until the final acceptance verdict.

It must state:

1. Permitted at the start and throughout:
   * The frozen requirements baseline and the compiled verification plan.
   * Owner decision records.
   * Released components in the pinned echelon-current set, consumed as released packages or vendored release artifacts.
   * Output of `conditor init` from the pinned manifest, reproducible by re-running it.
   * CI and deployment workflow files from prior rehearsals, as infrastructure only, re-pinned. List each file by path.
   * Fixture data from causa-data at the pinned commit.

2. Prohibited unless an owner decision record permits it:
   * Causa-specific application source, tests, acceptance specs, acceptance definitions (gates, defect classes, fixtures), or scripts copied from any kemiller2002/indy-init-rehearsal-* repository, whether whole files or substantially whole files.
   * Copying with a later edit. Modifying copied code does not make it new.

3. Agents may consult prior rehearsals as read-only references:
   * Agents may read prior rehearsal repositories to understand defects, designs, and tests.
   * Agents must write the Rehearsal 7 implementation during the timed run.

4. Changes to the starting state:
   * Only the owner may approve them, through a decision record merged before the clock starts.
   * An agent that believes a change is needed must stop and ask. It must not record the change only in a kickoff receipt or announce it after authorization.

5. A rule for prior fixes:
   * "Preserve a prior fix" means preserve the behavior and its test obligation, which are already requirements.
   * It never means carry the code forward.

starting-state.json must be machine-readable. It holds:

* the permitted infrastructure paths;
* the application path globs that the provenance check scopes (src/**, tests/**, scripts/**, tools/acceptance/**, acceptance/**, the root build configuration);
* the prior repositories and their pinned heads;
* the allowlist, which is empty unless an owner decision adds to it.

⸻

5. Clarify Ambiguous Instructions

Search the Rehearsal 7 kickoff material and docs/rehearsal-007 for continuation language:

* retain, preserve, carry forward, reuse, starting point, existing implementation, proven configuration, prior fixes, continue.

For each hit, report:

* the file and line;
* the exact text;
* whether it could be read as permission to copy application code;
* proposed replacement text that names the behavior or test obligation instead.

Do not apply wording changes to owner-authored text. Put them in the decision script (step 7) for the owner.

You may correct agent-authored text directly. Cite each change.

⸻

6. Start Gate and Kickoff Receipt

Update the Rehearsal 7 start gate and the kickoff-receipt schema:

1. The receipt records the starting-state contract by SHA-256.
2. The receipt records the source inventory after bootstrap: every path under the scoped globs with its blob id. For a clean start this is the Conditor scaffold only.
3. The receipt must have no "kickoff work" entry that changes the starting state unless it cites an owner decision record.
4. The start gate checks that rehearsal-007's application paths contain nothing beyond the Conditor scaffold.

Add a negative test: a receipt that lists an import without a decision record is rejected.

⸻

7. Owner Decision Script

Write docs/rehearsal-007/R7-STARTING-STATE-DECISIONS.md.

Present each question with options, a recommendation, and its consequences:

* Q1: Adopt the starting-state contract as written?
* Q2: List the prior-rehearsal infrastructure files that are permitted.
* Q3: Is any prior-rehearsal application file permitted? The recommendation is none.
* Q4: Accept each proposed rewording from step 5.
* Q5: When the provenance check reports a deviation, should the verdict be FAIL or NOT ACCEPTED WITH DEVIATION?

Ask the owner. Transcribe the answers only as drafts. Do not write approval records.

⸻

PART II: P1, REQUIRED BEFORE THE COMPETITION

8. Provenance Check

Add a command to the existing Indy-init F# tooling, for example `requirements-compiler verify-provenance`, or a small sibling tool under tools/.

Inputs:

* the candidate repository and commit;
* starting-state.json;
* the kickoff receipt;
* local clones of the prior repositories at their pinned heads.

Method:

1. List every blob under the scoped globs in the candidate tree (git ls-tree -r).
2. List every blob in each prior repository's full history under the same globs (git rev-list --objects).
3. Remove from consideration:
   * the empty blob;
   * blobs that a fresh `conditor init` produces from the pinned manifest. Reproduce this independently; never read it from the candidate.
   * blobs identical to files in released component packages;
   * allowlisted paths.
4. Report every remaining match with:
   * the candidate path;
   * the blob id;
   * the source repository, path and first commit;
   * the candidate commit that introduced it.
5. Also report:
   * paths first introduced by a commit whose message or diff names a prior rehearsal;
   * files whose content is at least 90% identical to a prior file, by line comparison. This is advisory and does not fail the build.

Output: indy-init.provenance-report/1 JSON, with the verdict CLEAN or DEVIATION and every finding.

Negative tests:

* A copied application file is detected.
* A copied file with one changed line is reported as near-identical.
* A vendored released Limen package is not flagged.
* A Conditor scaffold file is not flagged.
* An allowlisted file is not flagged, and the report shows the allowlist entry.
* A missing prior repository fails the check instead of passing it.

Wire the check into the Rehearsal 7 acceptance workflow (the successor to kickoff/rehearsal-006/acceptance-process.yml). The verdict reports it according to the owner's answer to Q5.

⸻

9. Validate Against Rehearsals 5 and 6

Run the check as validation only. Do not change those repositories or their verdicts.

1. Rehearsal 6 against Rehearsal 5:
   * Expect DEVIATION.
   * Expect the import to trace to 506cd8c.
   * The established evidence is that all 99 scoped F#, CSS and HTML files exactly match Rehearsal 5 blobs. Reconcile any difference from 99 and explain it.
2. Rehearsal 5 against Rehearsals 1 to 4:
   * Report the result as found. Do not assume it.

Commit both reports under docs/rehearsal-007/evidence/.

⸻

10. Source Classification in Results

Update the results-report template, or the Rehearsal 7 results instructions, to classify every scoped path as one of:

* Inherited (exact match to a prior rehearsal)
* Inherited and modified
* Newly implemented in the timed run
* Shared Echelon infrastructure (released components, Conditor scaffold)
* Tests
* Generated

Any comparison across rehearsals must state the starting state of each rehearsal.

⸻

11. Rehearsal 6 Disclosure

Do not edit docs/REHEARSAL-006-RESULTS.md.

Add docs/rehearsal-006/PROVENANCE-NOTE.md. It states:

* Rehearsal 6 began from Rehearsal 5's application source (506cd8c, an agent-initiated import without an owner decision).
* Its "Rehearsal 5 vs Rehearsal 6" comparison and timing measures, including time to first working application, describe a continuation, not a build from requirements.
* The provenance report from step 9.

Link the note from the docs index only if one exists.

⸻

PART III: P2, SUBSEQUENT

12. Praxis Intake

Write one intake document in kemiller2002/praxis .praxis/inbox/documents/:

* Proposed: at kickoff, Praxis records the starting source inventory (paths and blob ids) as an event, so the provenance check has a starting point the coordinating agent did not write.

Do not implement it, and do not change Praxis code.

⸻

13. Acceptance Criteria

1. STARTING-STATE.md and starting-state.json exist. They state the rule for the whole timed run, the permitted inputs, the prohibitions, and the decision path.
2. Every piece of continuation language is listed, and a rewording is proposed for each.
3. The start gate and kickoff receipt changes have passing negative tests.
4. The provenance check exists, its six negative tests pass, and the acceptance workflow runs it.
5. Validation reports for Rehearsals 6 and 5 are committed. The Rehearsal 6 result reconciles with the 99 matched files.
6. The source classification is part of the results requirements.
7. The Rehearsal 6 provenance note exists. Rehearsal 5 and 6 repositories and verdicts are unchanged.
8. The owner decision script is ready, and no approval record was written by an agent.
9. indy-init-rehearsal-007 still holds only its initial commit.
10. CI is green on every pull request.

⸻

14. Completion Report

Report:

* each pull request, with its status and CI result;
* the continuation-language findings;
* the validation results for Rehearsals 6 and 5, with counts;
* the questions waiting for the owner;
* anything not done, and why.

Rehearsal 7 is ready to start only after the owner merges the starting-state decisions.
