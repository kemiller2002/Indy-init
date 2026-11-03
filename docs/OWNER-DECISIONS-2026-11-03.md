# Owner decisions, 2026-11-03

**Status:** in force from the merge of the pull request that adds this record.

**Decided by:** kemiller2002 (Kevin Miller), the repository owner, on 2026-11-03 in the Claude Code session https://claude.ai/code/session_01G1iECf2YhG2QY5DzW4tX4s.
- The owner was asked four questions about reconciling the Rehearsal 009 content proposal (`docs/rehearsal-009-content-readiness/`) with the ratified requirement catalog `CAT-CAUSA-INVESTIGATION`.
- The owner answered, in these words: "1 yes / Include all / 3 do what you recommend / 4 no. Every rehearsal has to start from just the requirements. That's the rules of the contest".

**Recorded by:** an agent session (Claude Code). The agent wrote this record; it made none of the decisions. For OD-INDY-21, the owner delegated the choice to the agent's recommendation, and the recommendation is stated below exactly as it is applied.

## Decisions

| ID | Decision | Owner's words |
|---|---|---|
| OD-INDY-19 | **Investigation content is specified in the requirement catalog, not in a v4.1.3 package.** The content gaps found by the Rehearsal 009 content audit are added to the Causa investigation requirement catalog as new INV requirements. They are drafted as **proposed**, and the owner ratifies them. This supersedes the proposed successor amendment ODR-V413-01..03 (`docs/rehearsal-009-content-readiness/AMENDMENT-DECISION.md`), which is withdrawn without being recorded. | "1 yes" |
| OD-INDY-20 | **All ten content gaps are in scope for Rehearsal 9:** (1) observation text; (2) hypothesis statement; (3) evidence label, source reference and URL; (4) evidence-to-hypothesis links; (5) conclusions; (6) editing that keeps identity and history; (7) a primary investigative question; (8) validation rules; (9) a Cancel action; (10) a live round trip of all content through GitHub. | "Include all" |
| OD-INDY-21 | **The catalog's open questions 1–3 are answered by the agent's recommendation** (`docs/requirement-catalogs/CAUSA-INVESTIGATION.md`): (1) the problem statement is required and must not be empty; (2) a title is at most 200 characters, and each free-text content field (problem statement, question, observation text, hypothesis statement and explanation, evidence content, conclusion statement) at most 10,000 characters; (3) investigation start and every content editor have a Cancel action that writes nothing. | "3 do what you recommend" |
| OD-INDY-22 | **Every rehearsal starts from the requirements alone.** No rehearsal copies application code, tests, probes, tools or workflows from an earlier rehearsal's repository or pull request (for example Rehearsal 8's reference PR #57). What a rehearsal needs is written inside its own timed window from the requirements, the frozen packages and the released Echelon tools. Indy-init guidance that says otherwise is corrected. | "4 no. Every rehearsal has to start from just the requirements. That's the rules of the contest" |

## Later decision, same day

**Decided by:** kemiller2002, the repository owner, on 2026-11-03 in the Claude Code session https://claude.ai/code/session_01G1iECdCTBnCgRUVNbyANBs.
- The owner was shown the four decisions requested in `docs/rehearsal-009-content-readiness/HYPOTHESIS-EVIDENCE-RELATIONSHIPS.md` §10.
- The owner answered, in these words: "Apply the amendment and merge".

**Recorded by:** an agent session (Claude Code). The agent wrote this record and applied the amendment to the proposed files; it made none of the decisions.

| ID | Decision | Owner's words |
|---|---|---|
| OD-INDY-23 | **Hypothesis–evidence relationships** (catalog open question 4), as recommended in HYPOTHESIS-EVIDENCE-RELATIONSHIPS.md:<br>(1) **Kinds.** The kinds are Supports, Contradicts, Consistent (does not distinguish) and Inconclusive. "Not assessed" is the absence of a relationship; there is no default selection. Contextual is dropped: background evidence is left unlinked. Undermines Assumption and Corroborates are deferred.<br>(2) **Rationale.** Every assessment requires a rationale of at most 10,000 characters.<br>(3) **Reassess and withdraw.** These are in scope under OD-INDY-20 (6), and both keep history. There is at most one current assessment per evidence–hypothesis pair.<br>(4) **Amendment applied to the proposed files before ratification:**<br>- INV-030 gains `update`;<br>- INV-031 becomes a radio group with options supports, consistent-with, contradicts and inconclusive;<br>- new INV-038 is the relationship rationale;<br>- `proposed/00-DOMAIN-STUBS.proposed.fs` gains the four-case `RelationshipKind`, a required rationale, and `ReviseEvidenceRelationship` / `WithdrawEvidenceRelationship`. | "Apply the amendment and merge" |
