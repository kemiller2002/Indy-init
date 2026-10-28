ECHELON FOUNDRY

INDY INIT REHEARSAL 6

Owner Decisions Before Kickoff

Owner: kemiller2002
Rehearsal: 006
Primary repository: kemiller2002/Indy-init
Preflight report: Indy-init/docs/rehearsal-006/PREFLIGHT.md (PR #37)

⸻

1. Mission

Collect, record and apply the owner decisions that the Rehearsal 6 preflight found open.

Each decision changes how Rehearsal 6 is verified. No implementation can settle any of them.

The preflight cannot report READY until every decision below is recorded.

⸻

2. Operating Rules

Ask the owner each question exactly as written in section 4, with the options given.

Do not choose for the owner. Do not infer an answer from silence or from an earlier conversation.

Record only what the owner states, in the owner's own words, with the date.

Owner decisions are recorded by the owner (RDP-003). If the owner instructs the agent to transcribe an answer, record that the owner instructed it, and quote the instruction.

Do not modify Rehearsal 5 evidence.

Do not change the frozen baseline, BL-CAUSA-INDY-INIT-aab8f80c7743, or the frozen plan, VP-0eb9c791dd0b, unless an owner decision requires it. If one does, produce a new plan version. Do not edit the frozen one.

Do not start the timed rehearsal.

Do not create GitHub Issues.

Use F# for any new tooling.

⸻

3. Already decided (2026-10-28)

Record these as context. Do not ask them again.

* The causa-data four-case requirement is waived. Rehearsal 6 loads the published historical-v1 workspace (Apollo 1, Apollo 11, Challenger) and writes only to isolated workspaces/demo/<runId>/ paths.
* CI and deployment for kemiller2002/indy-init-rehearsal-006 are kickoff work, not preflight prerequisites. Preflight stages 1 and 15 become kickoff obligations.
* The stack is echelon-current 1.32.0. Arca 0.6.0 is adopted with its license change. Chrona stays on 1.31.0.

⸻

4. Questions for the Owner

Ask them in this order. One answer per question.

⸻

Question 1: DEC-025 against PF-015 and EXT-DATA-005 (R5A-001-08)

The owner decided earlier (DEC-025, OD-INDY-10) that the application has no anonymous mode and that GitHub permission fields are advisory.

These obligations require the opposite:

* PF-015-O1: "Public read-only catalog tests should not require a PAT when anonymous access is sufficient."
* PF-015-O2: "Authenticated integration tests use an approved secret such as CAUSA_GITHUB_TOKEN when explicitly required, with least-privilege read-only permissions."
* EXT-DATA-005-O1: "The GitHub provider MUST check repository and branch permissions, validate file paths and manifests, and load only schema-compatible data through the full contract validation and F# history-admissibility pipeline."

Rehearsal 5 followed DEC-025. It refused a blank token and never used RepositoryInfo.CanPush, so these obligations could not pass.

Options:

A. Keep DEC-025. Record that it supersedes PF-015-O1, PF-015-O2 and the permission-check clause of EXT-DATA-005-O1. They are not applicable in Rehearsal 6. The rest of EXT-DATA-005-O1 (path, manifest and contract validation) still applies.

B. Reverse DEC-025. Rehearsal 6 implements anonymous read-only catalog access and a real repository and branch permission check.

⸻

Question 2: Amber indicators (R5A-001-10, UI-016-O11)

UI-016-O11: "Ensure WCAG AA contrast for normal text (4.5:1), large text (3:1), and relevant non-text indicators (3:1)."

Rehearsal 5's amber indicator borders (#E9A23B) measured 1.95:1 on ivory and 2.17:1 on white.

Options:

A. The 3:1 requirement applies. Rehearsal 6 uses an amber, or an indicator treatment, that meets 3:1 against its surface.

B. Amber indicators that always appear beside text are outside "relevant non-text indicators". They are not held to 3:1.

⸻

Question 3: Role and access control (R5A-001-11, UI-031-O5)

UI-031-O5: "Apply role/access controls to sensitive operational information and never expose credentials."

Causa is a single-user browser application with no roles.

Options:

A. Not applicable to a single-user application. The obligation reduces to "never expose credentials", which still applies.

B. It applies. Rehearsal 6 implements a role model for sensitive operational information.

⸻

Question 4: Rehearsal 3 verdict wording (R5A-001-11, AR-019-O3)

AR-019-O3: "Original Rehearsal 3 must yield BLOCKED; Rehearsal 2 candidate 45644a7 should yield ACCEPTED only if its actual ledger and evidence meet the frozen contract."

The verifier yields INVALID or FAIL for Rehearsal 3, not the literal BLOCKED.

Options:

A. Any non-accepting verdict (INVALID, FAIL or BLOCKED) meets the obligation.

B. The verdict must be exactly BLOCKED. Rehearsal 6 changes the verifier accordingly.

⸻

Question 5: Corrective actions AR-027 through AR-035

These nine requirements, in Indy-init/docs/AGENT-RELIABILITY-REQUIREMENTS.md, were written after Rehearsal 5. They were added after the requirements baseline was frozen, so today's verifier does not score them.

* AR-027 [P0] Independent derivation check, byte for byte against the source text.
* AR-028 [P0] Isolated local verification: own ports, no reused servers.
* AR-029 [P0] Start smoke on every primary route, with no WebAssembly interpreter fault.
* AR-030 [P0] No test controls in production, proven in CI.
* AR-031 [P0] Owner scope decisions for generic screen contracts before kickoff.
* AR-032 [P1] The release set covers every runtime Echelon dependency.
* AR-033 [P1] Secrets and variables ready. Decided as kickoff work.
* AR-034 [P0] Early adversarial review and tag audit at the first release candidate.
* AR-035 [P0] Multi-writer persistence semantics, with read-back confirmation.

Options:

A. Do not gate on them. They are engineering practice the agents follow during Rehearsal 6. Acceptance does not score them. AR-031 is still decided before kickoff (Question 6).

B. Gate on them. Produce a new baseline and plan version that adds AR-027 to AR-035 with bindings. Re-run plan-readiness and the offline suite. Re-freeze. This replaces VP-0eb9c791dd0b.

C. Gate on a subset. The owner names which ones.

⸻

Question 6: Screen-contract scope (AR-031)

Rehearsal 6 starts from the generic Indy-Init Percepta screen contracts.

Before asking, list every screen contract the bootstrap copies, from the R5 repository's Percepta contracts and the Indy-init kickoff material, with a one-line description of each.

For each contract that Causa does not implement, ask the owner:

A. In scope. Rehearsal 6 must implement and verify it.

B. NOT-APPLICABLE. Record a Decision Record with the reason.

Rehearsal 5 scoped only S16 as not applicable. Do not assume Rehearsal 5's scoping carries over.

⸻

5. Recording

Record each answer as a Decision Record:

* Decision id: DEC-R6-001 onward.
* The question.
* The owner's exact words.
* The date.
* The obligations affected.
* The resulting status of each affected obligation.

Write them to:

Indy-init/kickoff/rehearsal-006/owner-decisions.json

And a readable copy to:

Indy-init/kickoff/rehearsal-006/OWNER-DECISIONS.md

Update the Rehearsal 5 audit follow-ups (R5A-001-08, -10, -11) and AR-031 to cite their Decision Records.

⸻

6. Applying the Decisions

| Answer | Action |
|---|---|
| Q1 A | Record PF-015-O1 and PF-015-O2 as not applicable under DEC-025, and narrow the permission-check clause of EXT-DATA-005-O1. Do this in the readiness rules (permitUnresolved does not apply, because these are bound) or as an approved exclusion record, whichever the verifier accepts for a superseded obligation. Prove the verifier reports them as decided, not failed. |
| Q1 B | Add the implementation work to the Rehearsal 6 kickoff requirements. |
| Q2, Q3, Q4 | Same pattern: a recorded scope decision the verifier honors, or a kickoff implementation requirement. |
| Q5 B or C | Produce the new baseline and plan version with propose-test-bindings, apply the bindings, run plan-readiness (it must be READY), and run the offline and full suites. Update the kickoff pins (README, acceptance-process.yml) to the new plan digest. |
| Q6 | Record the scope list. NOT-APPLICABLE contracts are excluded from Percepta verification only through the recorded decision. |

Do not mark any obligation verified. A decision changes scope. It never proves an obligation.

⸻

7. Update the Preflight

Update Indy-init/docs/rehearsal-006/PREFLIGHT.md and preflight.json:

* Stage 1 and stage 15: kickoff obligations, by the owner's decision.
* Stage 4: re-evaluate with the recorded decisions. It passes only if every item that R5A-001 marks "P0 before Rehearsal 6 starts" is either implemented or decided by a Decision Record.
* Stage 17: freeze the values if every mandatory stage passes.
* Overall readiness: READY or BLOCKED.

R5A-001-01 to -05 are implementation items, not owner decisions. If they are still open, list them as remaining blockers. Recommend whether each is kickoff work or preflight work, and ask the owner.

⸻

8. Deliverables

* owner-decisions.json and OWNER-DECISIONS.md.
* The updated preflight report.
* A new plan version, only if Question 5 requires it.
* One pull request in kemiller2002/Indy-init, merged only on the owner's instruction.
* A short summary: each decision, its effect, and the preflight verdict.

⸻

9. Final Directive

Ask the six questions. Record the owner's answers exactly. Apply them. Re-run the preflight checks they affect. Report READY or BLOCKED.

Stop at the start gate. Do not begin the timed rehearsal until the owner explicitly authorizes it.

No assumed decisions.

No transcribed approvals without the owner's instruction.

No unverified acceptance.
