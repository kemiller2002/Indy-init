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
  `actorIds` they record under), the implementing agents, the **independent reviewers** (a roster frozen in advance),
  the automation identities, the review roles and the rehearsal repository.
- **Attestations** (`indy-init.owner-attestations/1`): what the acceptance job observed from the GitHub API in its
  own run, for example the owner's approving pull-request review, with the decision ids that pull request carries
  (`decisions`). They count only when their `runId` equals the `--trusted-run-id` the job passes and they name this
  candidate, and an attestation proves only the decisions it names: an owner-approval record that decides anything
  else is unattested. A decision transcribed by an agent is never an owner decision.
- **CI records**: a test result or acceptance reference counts only when it is tool-observed **and** names the trusted
  run (`observed.identity.runId`); a CI claim from any other run is treated as an agent assertion. A test result must
  pass.
- **Latest wins**: for each owner decision subject and for the candidate's review, the latest authentic record
  decides; a later rejection withdraws an earlier approval.

A record qualifies only if all of these hold, otherwise it carries the diagnostic shown:

| Check | Diagnostic |
|---|---|
| recorded in the frozen rehearsal repository | `INCORRECT-REPOSITORY` |
| actor is in the frozen roster and is the actor of the execution envelope it was recorded under | `INCORRECT-AGENT-IDENTITY` |
| Praxis observed a commit, and it is the candidate or one of its ancestors | `INCORRECT-COMMIT-IDENTITY` |
| a candidate-bound statement names this candidate | `EVIDENCE-OTHER-CANDIDATE`, `STALE-REVIEW` for reviews |
| a checkpoint was verified by Praxis against the remote | `UNVERIFIED-CHECKPOINT` |
| a test result or acceptance reference was recorded by CI in the trusted run; a test result passed | `UNTRUSTED-TEST-RESULT`, `UNTRUSTED-ACCEPTANCE-REFERENCE`, `TEST-NOT-PASSED` |
| a review is a reviewer finding by a frozen independent reviewer who checkpointed nothing and started no non-review work in the run, in a review role (read from the envelope Praxis wrote) | `SELF-ACCEPTANCE` |
| an owner approval is the owner's own, attested by CI, for exactly what it decides | `TRANSCRIBED-DECISION`, `UNATTESTED-OWNER-DECISION`, `MISSING-APPROVAL` |

Run-level checks: every implementing work item has a verified checkpoint (`MISSING-CHECKPOINT`), every scope change
names a standing attested owner decision (`UNAPPROVED-SCOPE-CHANGE`), the latest review approves
(`REVIEW-NOT-APPROVED`), and the bound kind exists for the candidate (`MISSING-RELEASE-SELECTION`, `MISSING-APPROVAL`,
`MISSING-INDEPENDENT-REVIEW`, `MISSING-PROCESS-RECORD`). A log line that claims `verified-acceptance` is demoted to an
agent assertion: only the verifier issues that class. A plan or rules file that does not match its frozen pin makes the
assessment INVALID.

Agent assertions still qualify as process records for the steps an agent itself performs (assignment, dependency
decision, scope change, release selection, final disposition): what they prove is that the agent recorded the step,
under its own identity and execution, when it happened. They never qualify as test results, reviews or owner
decisions, and every obligation they serve also requires a reviewer category.

`verify-plan` accepts the same inputs with a `--process-` prefix (`--process-plan-digest`, `--process-rules`,
`--process-rules-sha256`, `--process-repo` or `--process-events`/`--process-executions`/`--process-ancestry`,
`--process-attestations`, `--process-trusted-run-id`), computes the process assessment itself for its own plan and
trusted candidate, and counts the verified process, owner and review categories as covered, only for categories a
`praxis:` producer is bound to. It never reads an assessment file. Application-test categories still need the evidence bundle. `praxis:` producers are not ledger tests and are
not expected in ledger rows.

## Evidence

| File | What it shows |
|---|---|
| `requirements-compiled/v2/readiness/evidence/process-evidence-simulation.json` | A simulated rehearsal recorded by the real Praxis CLI (`tools/process-evidence/simulate-rehearsal.sh`, fixture `tools/fixtures/process-evidence/simulated-rehearsal/`): implementer, reviewer, owner and CI each act under their own identity. With the proposed bindings, all 30 process, owner and review categories are VERIFIED from 12 records captured as the work happened. 24 negative controls, each a single change to the recorded log, the frozen inputs or the plan, fail closed with the named diagnostic: the nine required ones (missing approval, incorrect agent identity, missing checkpoint, unapproved scope change, self-acceptance, stale review, incorrect commit identity, missing release selection, evidence from another candidate) and fifteen more found by the independent review (a transcribed owner decision, an attestation CI did not observe, a forged `verified-acceptance` line, a real attestation reused for another decision, a later owner rejection, a failed test result, a test result from another CI run, one asserted by the implementer, the implementer reviewing under a review role, a later review rejection, an unverified checkpoint, a record without an observed commit, a record from another repository, a plan edited after freezing, self-supplied rules). |
| `requirements-compiled/v2/readiness/evidence/r5-readiness-replay.json` | Rehearsal 5's binding problem replayed through the gate (read only; R5's verdict is unchanged). Its kickoff plan `VP-e77a2134e1b0` is BLOCKED on all 326 obligations. Its 277 final bindings declare no evidence categories. Even granting each of them every application-test category of its obligation, 88 obligations stay unresolved before implementation, among them all 49 R5 left unproven, and 37 of those 49 need process, owner or reviewer evidence. |

Both are regenerated by the compiler tests (`--process-simulation`, `--readiness-replay`); the offline suite (CI) runs
the simulation and the producer-binding controls on every change.

## Limits

- Praxis identities are self-reported, and so is `observed.identity.runId`: a local process can name a CI run. The
  run id of the trusted acceptance job is known only when it runs, so the strongest arrangement is for that job itself
  to record the CI evidence it checks. Owner decisions rest on the CI-observed attestation, not on the Praxis actor; the
  attestation file is trusted only inside the acceptance job that fetched it.
- Praxis event ids are content hashes but are not signed; `verify-process` does not recompute them. Editing the log is
  detectable through git history, not cryptographically.
- A process record shows that a step was recorded at the time, with what Praxis observed. Whether it was the right
  step remains a reviewer's judgement, which is why these obligations keep their independent-acceptance category.
- Cross-repository routing (AR-017, VER-010-O2) is only partly observable from one repository's log.
