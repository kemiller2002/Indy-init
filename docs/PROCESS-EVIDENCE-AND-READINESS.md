# Process evidence and the implementation-start gate

Echelon development speed optimization, Priorities One and Two. Intake:
[`input-documents/2026-10-28-ESO-001-development-speed-optimization.md`](../input-documents/2026-10-28-ESO-001-development-speed-optimization.md).

Rehearsal 5 ended INCOMPLETE with 49 of 326 P0 obligations unproven. About 35 of them could only be proven by
evidence about how the rehearsal was run (reviewer conduct, agent scope and checkpoints, owner decisions, release
selection), and that evidence was assembled late or not at all. Its plan also began implementation with no test
identity for any obligation, so bindings were curated at the end (Indy-init PR #32, closed unmerged). Two changes
address this:

1. **Process evidence is recorded as the work happens**, in the rehearsal's own Praxis event log, and verified per
   candidate by `requirements-compiler verify-process`.
2. **Implementation cannot start with unresolved mandatory mappings**: `requirements-compiler plan-readiness` is
   BLOCKED until every mandatory obligation names how each of its evidence categories will be produced.

## Evidence classes

`requirements-compiled/v2/curation/evidence-categories.json` gives every category an evidence class and a producer.
The table only adds categories; no existing rule changed.

| Class | Categories | Proven by |
|---|---|---|
| application-test | unit, integration, browser, live-provider, security, accessibility | a test run of the application, through the ledger and evidence bundle (`verify-plan`) |
| reviewer | independent-acceptance | an independent reviewer's record or an acceptance job the implementer does not author |
| process | process-record (25 obligation-categories) | records in the run's Praxis log: `work start`, `work checkpoint`, `praxis work evidence` |
| owner | owner-decision (3) | an owner-approval record by the owner, with an attestation CI observed in the trusted run |

`process-record` is assigned by seven rules that name exact obligations (AR-005, AR-012, AR-015, AR-017, AR-021,
UI-021-O3, UI-030-O3, UI-040-O2, VER-001..VER-004, VER-010, PF-019-O1); `owner-decision` by one rule (AR-024-O1,
AR-024-O2, AR-008-O2). Each rule quotes the obligations it rests on. Every one of these obligations also keeps its
existing categories, usually independent-acceptance, so a process record never proves an obligation alone: it shows
the step was recorded when it happened, and a reviewer still judges it.

## Binding a producer

A process or owner category is bound like a test, with a producer id `praxis:<record kind>`:

```json
{ "obligation": "AR-012-O2", "tests": [ { "id": "praxis:scope-change", "kind": "process", "categories": ["process-record"] } ] }
```

Record kinds: `execution-start`, `checkpoint` (from `work.started` and `work.checkpointed`), and the
`praxis work evidence` kinds `assignment`, `dependency-decision`, `scope-change`, `owner-approval`,
`independent-review`, `release-selection`, `test-result`, `acceptance-reference`, `final-disposition`. Compilation
refuses a process or owner binding without a `praxis:` id, an unknown kind, owner evidence from anything but
`praxis:owner-approval`, and any category a binding's kind cannot provide. When several producers are bound to one
category, each of them must deliver.

`requirements-compiled/v2/proposals/process-bindings.proposed.json` holds **proposed** bindings for all 28 process and
owner obligation-categories (and AR-008-O1's independent review). It is an agent proposal and is not approved. Bindings
take effect only when the owner copies them into `curation/verification-plan.json` `testBindings` and the plan is
recompiled (RDP-003).

## `plan-readiness`: before implementation

```bash
$RC plan-readiness --plan PLAN --plan-digest FROZEN-DIGEST [--rules READINESS-RULES.json --rules-sha256 FROZEN-SHA256] [--out FILE]
```

Exit 0 READY, 3 BLOCKED, 2 unusable input. The plan must recompute to the digest frozen at kickoff and the rules to
their frozen hash; an edited plan or self-supplied rules are BLOCKED. For every mandatory (P0, not excluded) obligation and each required
category, the gate needs a bound application test (application-test and reviewer classes) or a valid `praxis:<kind>`
producer (process and owner classes). It reports every gap with its class, producer and reason (`no-bound-test`,
`no-bound-producer`, `invalid-producer`, `unclassified`).

A rehearsal may begin with gaps only if its **frozen** readiness rules (`indy-init.plan-readiness-rules/1`) permit
each one: `{obligation, category, reason, decision}`. A permit without a reason or a decision, or one that matches no
gap of this plan, is a problem and the gate stays BLOCKED. A permitted gap is listed as such and is still unverified at
acceptance; the gate never marks anything verified.

## `verify-process`: per candidate

```bash
$RC verify-process --plan PLAN --plan-digest FROZEN-DIGEST --rules PROCESS-RULES.json --rules-sha256 FROZEN-SHA256 \
  --candidate SHA (--repo REHEARSAL-CHECKOUT | --events events.jsonl [--executions DIR] --ancestry rev-list.txt) \
  [--attestations ATTESTATIONS.json --trusted-run-id RUN] [--out FILE]
```

Exit 0 VERIFIED, 3 not verified, 2 unusable input. It reads the Praxis log and the execution envelopes, keeps every
record's provenance class, and decides each process, owner and Praxis-review category bound in the plan.

Praxis identities are self-reported (`--actor-kind`, `ROS_ACTOR_KIND`, even `GITHUB_ACTIONS=true` can be set by any
process), so the verifier trusts nothing a record says about itself:

- **Frozen rules** (`indy-init.process-evidence-rules/1`, pinned by `--rules-sha256`): the owners (`login`, the Praxis
  `actorIds` they record under), the implementing agents, the **independent reviewers** (a roster frozen in advance,
  `{actorId, login}`: the Praxis identity and the GitHub login whose review anchors its findings; a reviewer may not
  also be an implementing agent), the automation identities, the review roles, the rehearsal repository, and
  `ownerDecisions`: for each owner obligation, the decision ids that prove it (an owner category is verified only by
  those decisions, never by an approval of something else).
- **Attestations** (`indy-init.owner-attestations/1`): what the acceptance job observed from the GitHub API in its
  own run: approving pull-request reviews by the owner or a frozen reviewer, each with the commit it approved
  (`commit`) and, for owner decisions, the decisions the pull request carries, each bound to the paths and
  requirements it covers (`decisions: [{id, paths}]`; a bare id covers no path). They count
  only when their `runId` equals the `--trusted-run-id` the job passes and they name this candidate, and only the
  latest review state of each login on each pull request counts: a later CHANGES_REQUESTED or DISMISSED review voids
  the earlier approval, and a same-second tie goes to the non-approving state. An attestation
  proves only what it names. An owner approval names one decision id (`--decision`) and what it covers
  (`--subject`), all of which its attestation must name; a scope change stands only on the standing decision it cites,
  and only for the paths and requirements the attestation binds to that decision; a scope change must name what it
  adds. A path named by any withdrawal is withdrawn from every decision.
- **Reviews** are anchored the same way: a review record must cite the reviewer's own approving review of the exact
  candidate commit, observed by CI. A review written into the log, under any identity, without it is unattested.
- **Final disposition** is the owner's: a human owner record attested by CI for the candidate commit as the final
  disposition (its attestation names `final-disposition`).
- **CI records**: a test result or acceptance reference counts only when it is tool-observed **and** names the trusted
  run (`observed.identity.runId`); a CI claim from any other run is treated as an agent assertion. A test result must
  pass.
- **CI-observed withdrawals**: a non-approving latest review state that CI observed for an owner or frozen reviewer,
  on any pull request, withdraws what it names (an owner's decisions, `final-disposition`; a reviewer's approval of the
  candidate), whether or not the log records it.
- **Withdrawals always count, wherever they sit in the log**: any owner rejection of a decision, any frozen reviewer's
  non-approving review of the candidate, any owner disposition other than accepted, any failing trusted test result,
  and any selection of another candidate after this one was selected withdraws the approval, review, acceptance, pass
  or selection, attested or not. A withdrawal can only make the verdict stricter, so a replayed approval line cannot
  outrank it; a decision the owner rejected needs a new decision id.

A record qualifies only if all of these hold, otherwise it carries the diagnostic shown:

| Check | Diagnostic |
|---|---|
| recorded in the frozen rehearsal repository | `INCORRECT-REPOSITORY` |
| actor is in the frozen roster and is the actor of the execution envelope it was recorded under | `INCORRECT-AGENT-IDENTITY` |
| Praxis observed a commit, and it is the candidate or one of its ancestors | `INCORRECT-COMMIT-IDENTITY` |
| a candidate-bound statement names this candidate | `EVIDENCE-OTHER-CANDIDATE`, `STALE-REVIEW` for reviews |
| a checkpoint was verified by Praxis against the remote | `UNVERIFIED-CHECKPOINT` |
| a test result or acceptance reference was recorded by CI in the trusted run; a test result passed | `UNTRUSTED-TEST-RESULT`, `UNTRUSTED-ACCEPTANCE-REFERENCE`, `TEST-NOT-PASSED` |
| a review is a reviewer finding by a frozen independent reviewer who checkpointed nothing and started no non-review work in the run, in a review role (read from the envelope Praxis wrote), anchored by that reviewer's own CI-observed review of the candidate commit | `SELF-ACCEPTANCE`, `UNATTESTED-REVIEW`, `STALE-REVIEW` |
| a final disposition is the owner's, attested by CI for the candidate commit | `TRANSCRIBED-DECISION`, `UNATTESTED-DISPOSITION` |
| an owner approval is the owner's own, attested by CI, for exactly what it decides | `TRANSCRIBED-DECISION`, `UNATTESTED-OWNER-DECISION`, `MISSING-APPROVAL` |

Run-level checks: every implementing work item has a verified checkpoint (`MISSING-CHECKPOINT`), every scope change
is covered by a standing attested owner decision (`UNAPPROVED-SCOPE-CHANGE`), the latest review approves
(`REVIEW-NOT-APPROVED`), the latest trusted test result passes (`TEST-NOT-PASSED`), the latest release selection names
this candidate (`RELEASE-SUPERSEDED`), a binding's kind can provide its category (otherwise
`UNBOUND-PROCESS-EVIDENCE`), and the bound kind exists for the candidate (`MISSING-RELEASE-SELECTION`, `MISSING-APPROVAL`,
`MISSING-INDEPENDENT-REVIEW`, `MISSING-PROCESS-RECORD`). A log line that claims `verified-acceptance` is demoted to an
agent assertion: only the verifier issues that class. A plan or rules file that does not match its frozen pin makes the
assessment INVALID.

Agent assertions still qualify as process records for the steps an agent itself performs (assignment, dependency
decision, scope change, release selection): what they prove is that the agent recorded the step,
under its own identity and execution, when it happened. They never qualify as test results, reviews or owner
decisions, and every obligation they serve also requires a reviewer category.

`verify-plan` accepts the same inputs with a `--process-` prefix (`--process-plan-digest`, `--process-rules`,
`--process-rules-sha256`, `--process-repo` or `--process-events`/`--process-executions`/`--process-ancestry`,
`--process-attestations`, `--process-trusted-run-id`), computes the process assessment itself for its own plan and
trusted candidate, and counts the verified process, owner and review categories as covered, only for categories a
`praxis:` producer is bound to. A category with a bound producer is covered only by the producer, never by a passing
ledger test, and compilation refuses a binding that mixes producers and ledger tests for one category. It never reads
an assessment file. Application-test categories still need the evidence bundle. `praxis:` producers are not ledger tests and are
not expected in ledger rows.

## Evidence

| File | What it shows |
|---|---|
| `requirements-compiled/v2/readiness/evidence/process-evidence-simulation.json` | A simulated rehearsal recorded by the real Praxis CLI (`tools/process-evidence/simulate-rehearsal.sh`, fixture `tools/fixtures/process-evidence/simulated-rehearsal/`): implementer, reviewer, owner and CI each act under their own identity. With the proposed bindings, all 30 process, owner and review categories are VERIFIED from 12 records captured as the work happened. 47 negative controls, each a change to the recorded log, the attestations, the frozen inputs or the plan, fail closed with the named diagnostic, and 3 record-level controls refuse the record: the nine required ones (missing approval, incorrect agent identity, missing checkpoint, unapproved scope change, self-acceptance, stale review, incorrect commit identity, missing release selection, evidence from another candidate) and 41 more found by five independent reviews (among them: a transcribed or unattested owner decision, an attestation reused for another decision or a broader scope change, an owner rejection with or without attestation, failed, untrusted or later-failed test results, the implementer reviewing under a review role or a reviewer identity, a review without its attestation or attested for an older commit, a later review rejection (attested or not) or release selection, a replayed approval after a rejection, a later CHANGES_REQUESTED review, an owner approval of an unrelated decision, a disposition citing another attestation, non-approving reviews CI observed on other pull requests, a scope change citing an unrelated or rejected decision, naming nothing, or claiming a path its decision does not bind or that was later withdrawn, a same-second approval and withdrawal, an unverified checkpoint, a missing commit, another repository, a mis-kinded binding, the implementer's own final disposition, an edited plan, self-supplied rules). |
| `requirements-compiled/v2/readiness/evidence/r5-readiness-replay.json` | Rehearsal 5's binding problem replayed through the gate (read only; R5's verdict is unchanged). Its kickoff plan `VP-e77a2134e1b0` is BLOCKED on all 326 obligations. Its 277 final bindings declare no evidence categories. Even granting each of them every application-test category its test kind can provide, 140 obligations stay unresolved before implementation (many R5 bindings name a test kind that cannot provide the category, for example a deterministic test for live-provider evidence), among them all 49 R5 left unproven; 37 of those 49 need process, owner or reviewer evidence. |

Both are regenerated by the compiler tests (`--process-simulation`, `--readiness-replay`); the offline suite (CI) runs
the simulation and the producer-binding controls on every change.

## Limits

- Praxis identities are self-reported, and so is `observed.identity.runId`: a local process that knows or guesses the
  trusted run id can write a record that looks like that run's. Reviews and owner decisions do not rest on this (they
  need a CI-observed attestation), but test results and acceptance references do. The
  run id of the trusted acceptance job is known only when it runs, so the strongest arrangement is for that job itself
  to record the CI evidence it checks. Owner decisions rest on the CI-observed attestation, not on the Praxis actor; the
  attestation file is trusted only inside the acceptance job that fetched it.
- A checkpoint's `verification.status` and an execution envelope's role are written by Praxis but live in the same
  writable repository; `verify-process` checks the checkpoint's commit against the candidate's real ancestry and takes
  implementers from the frozen agent roster, so neither is relied on alone.
- Praxis event ids are content hashes but are not signed; `verify-process` does not recompute them. Editing the log is
  detectable through git history, not cryptographically.
- A process record shows that a step was recorded at the time, with what Praxis observed. Whether it was the right
  step remains a reviewer's judgement, which is why these obligations keep their independent-acceptance category.
- Cross-repository routing (AR-017, VER-010-O2) is only partly observable from one repository's log.
