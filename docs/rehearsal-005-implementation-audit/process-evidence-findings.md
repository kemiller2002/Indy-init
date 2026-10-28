# Process-evidence findings

Eighteen of the 31 unverified requirements concern how the rehearsal was conducted, or need records that application code cannot supply. Each is classified PROCESS_EVIDENCE_MISSING with one sub-status:

| Sub-status | Count | Meaning |
|---|---|---|
| PERFORMED_EVIDENCE_INADEQUATE | 6 | Repository artifacts show the process happened, but the evidence is not admissible: not candidate-bound, agent-asserted, pre-run and agent-local, or reconciled after the fact |
| NOT_PERFORMED | 2 | The repository shows the process was not carried out as required |
| UNKNOWN | 10 | Neither performance nor non-performance can be established |

The audit never infers that an event occurred because a mechanism for it exists. For example, the Praxis checkpoint command existing (HANDOFF.md) is not evidence that checkpoints were kept.

## Authorship and independence

282 of the 283 commits in the history of 1b9fb56 are authored "Claude" (the other is the owner's initial commit). All 201 commits that carry a session trailer name the same session (`session_01G1iECdHZXLR6xBfMivMsx3`), and that includes the two "fresh reviewer" commits.

The repository therefore cannot show that a reviewer was distinct from the implementer (AR-008-O1). Nor can it show that any decision was the owner's rather than the agent's (AR-024, VER-003-O2). The rehearsal results say so themselves: owner decisions were "made in chat; the agent transcribed them" (RDP-003).

## Per requirement

| Requirement | Sub-status | Evidence found | Can it be established retrospectively? |
|---|---|---|---|
| AR-005 — Capability registry and reuse | unknown | Looked for a capability-discovery or reuse record in the candidate: SDE-MAP.md, acceptance/reuse.json (test reuse, not capability reuse), research/decisions, .praxis/inbox. Only the layout engine has a discovery record (WDL-001, PASS). Whether discovery happened for the other components cannot be established from the repository. | No: discovery and routing had to be recorded before or while the work happened |
| AR-008 — Adversarial independent review | unknown | Two reviews by agents described as fresh exist and found real defects (F-1 critical, F-2, F-3, F-5, R2-F1/F2, R3-F1/F2, plus 13 over-tagged obligations). Independence is self-asserted, and the last review ran at be01851 and 543ddc2, not at 1b9fb56. 282 of 283 commits up to 1b9fb56 are authored "Claude", and all 201 session trailers name one session, review commits included, so independence cannot be established. | No: the required record had to be made during the run or before the timed start |
| AR-012 — Controlled work scope | unknown | Work was split into owned streams on separate branches, and every main commit is attributed to WI-0001..WI-0008. That attribution was reconciled after the fact (58 reconciliation events), and no record states each agent's bounded requirements and paths before it worked. | No: the required record had to be made during the run or before the timed start |
| AR-015 — Long-running agent continuity | not performed | The Praxis checkpoint mechanism exists (HANDOFF.md: praxis work checkpoint), but it recorded one checkpoint (2026-10-27T20:20:18Z) in a 392-minute run. After two container restarts the streams resumed from agent transcripts. | No: the required record had to be made during the run or before the timed start |
| AR-017 — Ownership and intake | unknown | Findings for other repositories (the Praxis preservation gate, F-004) were identified during the run, and the results say they 'should be routed'. They were routed after the run through Indy-init input-documents (EII-001). The audit did not search the owners' issue trackers, so 'never GitHub Issues' is not established. | No: discovery and routing had to be recorded before or while the work happened |
| AR-020 — Selective enforcement | unknown | All three obligations can be observed in the candidate: the four-tier brief is retained, no per-step Ordo enforcement was added, and the domain model is typed with documented boundaries. The plan accepts only test identity, so none of this could bind. The independent review cited 5333387 (an executable Percepta NOT-APPLICABLE gate added during the run) as contradicting O2. It is an acceptance boundary, which O3 permits, so the evidence neither confirms nor refutes compliance. | Partly: the artifacts are in the candidate and could be reviewed against 1b9fb56 by an attested reviewer; live per-commit freshness cannot be recreated |
| AR-021 — Continuous implementation agent | not performed | R5 deliberately ran parallel streams instead of the preferred single long-running agent (O1), and nothing measured requirement retention after compaction (O3). A release-candidate review happened, but at be01851, not at the final candidate. | No: the required record had to be made during the run or before the timed start |
| AR-023 — Release-set integrity | performed evidence inadequate | SD-1 was resolved and verified before the run (PRE-TOOL-02 PASS: Conditor 0.10.14 contains CON-367), and the bootstrap commit cf86744 was produced without the hand workaround. The evidence exists but was not collected into the run bundle. | Partly: an attested re-run of the preflight for the frozen inputs could replace the agent-local record, but not the pre-run timing |
| AR-024 — Explicit kickoff decisions | unknown | The decisions were made by the owner before or during the run, but only in chat; the agent transcribed them and wrote the approvals files (a recorded RDP-003 deviation). No authenticated owner record exists. | No: the required record had to be made during the run or before the timed start |
| PF-019 — Local-first workflow | unknown | O1 is a development-workflow obligation for agents. CI layering is evidenced, but the agents' local test order was not recorded. | No: the required record had to be made during the run or before the timed start |
| UI-021 — Content and route verification | performed evidence inadequate | The application behaviour is fully verified. Only the Praxis traceability and approved-scope obligation is open; the work-item attribution and the 'no exclusions' record exist but were reconciled after the fact. | Partly: the artifacts are in the candidate and could be reviewed against 1b9fb56 by an attested reviewer; live per-commit freshness cannot be recreated |
| UI-030 — Presentation semantics verification and adversarial tests | performed evidence inadequate | Requirement ids, tests and the deployed SHA are recorded. The independent review did not cover the final candidate SHA. | No: the required record had to be made during the run or before the timed start |
| UI-040 — End-to-end acceptance | performed evidence inadequate | The end-to-end behaviour is verified. The requirements went through the Praxis inbox, and 'no approved exclusions' is recorded in the frozen preflight; neither could bind. | Partly: the artifacts are in the candidate and could be reviewed against 1b9fb56 by an attested reviewer; live per-commit freshness cannot be recreated |
| VER-001 — Latest compatible verified release | unknown | The newest approved compatible set (echelon-current 1.29.0) was selected and pinned. There is no per-system owner and channel discovery record (O3) except for forma-workflow. | Partly: an attested re-run of the preflight for the frozen inputs could replace the agent-local record, but not the pre-run timing |
| VER-002 — Verify release freshness | performed evidence inadequate | The registry was queried before the freeze (executedAt 19:51:18Z). The candidate list and selection rationale (O2) were not recorded. | Partly: an attested re-run of the preflight for the frozen inputs could replace the agent-local record, but not the pre-run timing |
| VER-003 — Compatibility over blind upgrades | unknown | The lock gate and integration tests passed on the selected set, and the one exception (forma-workflow 1.1.0) has a decision record. The record was transcribed by the agent from chat. | No: the required record had to be made during the run or before the timed start |
| VER-004 — Reproducible lock | performed evidence inadequate | R5 re-resolved the release set (1.28.0 to 1.29.0) before the run; the preflight record holds the evidence. | Partly: an attested re-run of the preflight for the frozen inputs could replace the agent-local record, but not the pre-run timing |
| VER-010 — Requirements traceability | unknown | VER-005..VER-009 PASS and VER-001..VER-004 have evidence outside the bundle. No reconciliation record and no record of Praxis-path routing during the run exist. | No: the required record had to be made during the run or before the timed start |

## What exists now that did not exist at Rehearsal 5

After R5, Indy-init added process and owner evidence classes and two gates (PR #33, PR #34; [PROCESS-EVIDENCE-AND-READINESS.md](../PROCESS-EVIDENCE-AND-READINESS.md)):

- **`plan-readiness`** blocks implementation while obligations lack an evidence producer.
- **`verify-process`** rejects:
  - transcribed owner decisions (`TRANSCRIBED-DECISION`);
  - reviews of other candidates (`STALE-REVIEW`, `EVIDENCE-OTHER-CANDIDATE`);
  - unverified checkpoints;
  - reviewers who also implemented.

Applied to R5's records, these gates would have reported the same gaps this audit found. They would not have created the missing records. For those to exist in Rehearsal 6, the agents have to record checkpoints, scope, routing and reviews in Praxis while working, and the owner has to approve through the attested channel.
