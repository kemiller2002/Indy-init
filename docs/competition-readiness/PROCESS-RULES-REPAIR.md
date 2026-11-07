# Competition owner decisions ODR-COMP-P17, -P18 and -P19: the process-evidence rules repair, the P10 extension and the INV-023 storage

Status: **PROPOSED.** Nothing here is recorded. The owner records ODR-COMP-P17, ODR-COMP-P18, ODR-COMP-P19 and ODR-COMP-LOCK in one session with `tools/owner-freeze.fsx decide competition`, commits the lock, and records the re-govern approval (section 4). Agents never write approval records, never commit the lock and never pin rules (RDP-003, ODR-RG-04).

Decision state, 2026-11-07:
- The owner decided P17 = A and P18 = A in chat, around 17:22–17:25Z. A chat answer is not a record: both await formal recording with `owner-freeze.fsx decide competition`.
- P19 is still open.

## 1. The defect (P17)

The competition lock `kickoff/competition/toolchain.lock.json` (owner pin, APR-COMP-OWNER-2) names Rehearsal 010's process-evidence rules: `processEvidence.rules = kickoff/rehearsal-010/process-evidence-rules.json`, `rulesSha256 36172c57…`. Those rules say `"repository": "indy-init-rehearsal-010"`.

The competition repository, kemiller2002/causa, records its Praxis events under the project id `causa-t0`. `conditor init` took that id from the bootstrap directory name, and it is committed at T0 in `ros.json` (`"name": "causa-t0"`, `repository.id: "causa-t0"`) and in `.ros/events/events.jsonl`. Every one of the 11 events on causa main (1c67842) says `"repository":"causa-t0"`.

`requirements-compiler verify-process` (`tools/requirements-compiler/src/ProcessVerification.fs`, `recordDiagnostics`) checks every record against the rules' `repository`. Any record that names a different repository gets the diagnostic **`INCORRECT-REPOSITORY`** ("recorded in repository Some "causa-t0", not indy-init-rehearsal-010") and cannot count as evidence. As a result, no `praxis:*` obligation can pass the governed slice gate.

T0 records are never rewritten. The repair is therefore on the rules side: `kickoff/competition/process-evidence-rules.json` is Rehearsal 010's rules with exactly one change, `"repository": "causa-t0"`, plus a description that says what it is. The owner, agents, reviewers, automation, review roles and owner decisions are unchanged.

| File | sha256 |
| --- | --- |
| `kickoff/rehearsal-010/process-evidence-rules.json` (current) | `36172c57721419714a414334cd65a1548da5c0bc6318f55f73b77a207546344d` |
| `kickoff/competition/process-evidence-rules.json` (proposed) | `88cda0b04d1125cf49b8bf03aa664437d5c4f5ebb0584c2e57b155f731acedf3` |

### Evidence

`verify-process` was run against a clone of kemiller2002/causa at 1c67842 (`--repo`, no attestations), with the lock's plan (`requirements-compiled/v4/verification-plan.json`, planDigest `1e3c66bc…`):

| Rules | Records qualifying | `INCORRECT-REPOSITORY` | Category checks verified |
| --- | --- | --- | --- |
| Rehearsal 010 (`36172c57…`) | 0 of 10 | 60 | 0 of 133 |
| Competition (`88cda0b0…`) | 10 of 10 | 0 | 5 of 133 |

The 5 checks that now verify are the `praxis:execution-start` process records of AR-017-O1, AR-017-O2, UI-021-O3, UI-040-O2 and VER-010-O2. The remaining checks are waiting for evidence that has not been produced yet: reviews, checkpoints and approvals.

```
dotnet tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll verify-process \
  --plan requirements-compiled/v4/verification-plan.json \
  --plan-digest 1e3c66bcc958a60415811ffbe2795b5e7147360340749c16e1a0f43fc19c751d \
  --rules kickoff/competition/process-evidence-rules.json \
  --rules-sha256 88cda0b04d1125cf49b8bf03aa664437d5c4f5ebb0584c2e57b155f731acedf3 \
  --candidate 1c67842be59e… --repo <clone of kemiller2002/causa> --out vp.json
```

### What this pull request changes

- `kickoff/competition/process-evidence-rules.json`: new, PROPOSED.
- `docs/v416-competition-decisions/proposals/owner-decision-requests.json`:
  - ODR-COMP-P17 is added. It pins the rules file and offers option A (adopt).
  - ODR-COMP-P18 is added (section 2).
  - ODR-COMP-P19 is added (section 3).
  - ODR-COMP-LOCK's question now names P17.
- `kickoff/competition/toolchain.lock.proposed.json` is the proposed lock. Compared with the committed lock, it changes only:
  - `processEvidence`: `rules`, `rulesSha256`, `proposedSha256`, `rulesStatus` and `description`.
  - `ownerDecisionRequests.sha256` and its `rule` text.
  - the top-level `description`.

  Launch-preflight LP-11 requires the committed lock to equal this proposal except for `status` and `indyInit.commit`. ODR-COMP-LOCK pins this file, so `decide competition` asks ODR-COMP-LOCK again: a recorded decision whose pinned file changed is shown again, with what changed.

The committed lock `kickoff/competition/toolchain.lock.json` and every frozen package are unchanged.

## 2. ODR-COMP-P18: extension of ODR-COMP-P10

Governed acceptance of `praxis:independent-review` cannot be reached on a kemiller2002/causa pull request:

1. A review record qualifies only if CI observes, in the same run, an approving GitHub review by the reviewer's frozen login. The run is `verification-gate.yml:247`, `review-attestations`. The login is `kemiller2002` in the rules. The review must approve exactly the candidate, or verify-process reports `STALE-REVIEW` (`ProcessVerification.fs:292-308`).
2. The candidate is `github.sha` (`verification-gate.yml:61-67`). On a pull request that is the merge commit `refs/pull/N/merge`, and GitHub reviews attach to the pull request head, never to that merge commit.
3. Every causa pull request is authored by kemiller2002, the account the builder works through. GitHub does not let an author approve their own pull request (cf. OD-INDY-17).

15 of the 31 slices in `docs/competition-readiness/slice-execution-manifest.json` have `praxis:*` acceptance tests. They are SL-T10, T11, T12, T13, T18, T20, T21, T22, T24, T30, T31, T33, T34, T36 and SL-WI-GEN-VER. Every other slice depends on SL-T10, directly or transitively. So without an extension, no slice can reach governed PASS.

**Option A (recommended):**
- A slice may merge when the governed Verification gate ran to a verdict and the only refused obligations are `praxis:*` process-record obligations with these verify-process codes: `INCORRECT-REPOSITORY`, `MISSING-INDEPENDENT-REVIEW`, `STALE-REVIEW`, `UNATTESTED-REVIEW`, `MISSING-CHECKPOINT`, `MISSING-PROCESS-RECORD` or `MISSING-ASSIGNMENT`.
- Every executable, journey, design-contract, build, test-integrity and regression obligation of the slice must PASS in that governed run.
- Each such merge is labelled "governed on executable obligations; process records unverifiable (ODR-COMP-P18)". It is not counted as governed acceptance in the ledger, and completion remains `completion-gate` COMPLETE only.

**Option B:** no extension. The competition then merges nothing, or merges only under P10 A if the gate cannot run at all.

**Caveat, as worded.** A refusal with any other code still blocks the merge. At causa 1c67842, verify-process also reports two codes outside the list:
- `MISSING-APPROVAL` for AR-008-O2, AR-024-O1 and AR-024-O2. The rules' `ownerDecisions` name `DEC-R10-KICKOFF` for these. They belong to slices SL-T30, SL-T33 and SL-T34.
- `MISSING-RELEASE-SELECTION` for UI-030-O3, in slice SL-T22.

P18 pins no file. The lock binds the request text through `ownerDecisionRequests.sha256`, which launch-preflight checks in LP-03.

## 3. ODR-COMP-P19: where the J1 "Primary investigative question" (INV-023) is stored

Evidence:

- J1 (`verification/journeys/bindings.json`) binds INV-023 in J1-03. It checks the entered question in GitHub itself in J1-05 (`providerHolds([v.problem, v.title, v.question])`, `verification/journeys/j1-create-persist.spec.mjs`) and again after a reload in J1-06. All three assertions are P0.
- The pre-competition field map maps INV-023 to `Payload.InvestigationDetailsRevised.question` under CMD-INV-CREATE, and marks the binding blocked by OD-CR-01 (`docs/contract-readiness/causa-precompetition/UI-FIELD-MAP.json`, UB-023).
- causa-storage 1.1.0 leaves `InvestigationDetailsRevised` to a later 1.2.0 (causa-data `contract/v1.1.0/CONTRACT.md` section 1; `requirements-compiled/v4.1.6/proposals/STORAGE-1.1.md` section 3).
- `COMPETITION-CONSUMPTION.md:56` says a blocked binding stays unimplemented until the owner decides, and that an agent must not choose the meaning of a blocked field itself.
- The frozen `InvestigationStarted` payload is exactly `{title, observationId, original}` (causa-data `contract/v1.0.0/CONTRACT.md`, unchanged in 1.1.0). No field of the start holds the question. The only wire field named `question` is in `UnknownIdentified {unknownId, question, materiality}`.

**Option A (recommended):**
- For the competition, store INV-023 as one `UnknownIdentified` with materiality `informational`, written in the same command group as the start.
- On reload, the earliest-created unknown is the primary question.
- Side effects: the question also appears in the unknowns list, and it needs a migration once causa-storage 1.2.0 has `InvestigationDetailsRevised`.

**Option B:** do not store it. J1-05 and J1-06 then fail on the question value and stay failed for the competition. J1-03's start is unaffected.

P19 pins no file. Like P18, its text is bound by the lock's `ownerDecisionRequests.sha256`.

## 4. Owner steps

Run these in a GitHub Codespace from `/workspaces`, with the clone at `/workspaces/Indy-init`, signed in to git and gh as kemiller2002. Run owner-freeze from `/workspaces`, not from inside the clone: the clone's `global.json` pins an SDK. Steps 2 and 3 need `jq` (present in the default Codespaces image) and a clean working tree.

**0. Merge this pull request** (it holds proposals only):

```
gh pr merge <this PR> --repo kemiller2002/Indy-init --merge
```

**1. Decide.** This asks only the open requests: ODR-COMP-P17, ODR-COMP-P18 and ODR-COMP-P19, which are new, and ODR-COMP-LOCK, which is asked again because the proposed lock changed. Every other request is already recorded and is not asked. Answer `A`, `A`, `A`, `A`, then type `record`. The command commits as you, pushes, and opens the pull request; the second line merges it.

```
cd /workspaces && git -C Indy-init fetch -q origin && git -C Indy-init switch -q --detach origin/main && dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide competition --repo Indy-init
gh pr merge "$(git -C /workspaces/Indy-init branch --show-current)" --repo kemiller2002/Indy-init --merge
```

**2. Commit the lock** (as ODR-RG-04 and ODR-COMP-LOCK). This writes the approved proposal with only `status` and `indyInit.commit` filled in. `indyInit.commit` is the merge commit of step 1; the same pattern as 4b75cd5 for APR-COMP-OWNER-2. `--indent 1` reproduces the file's own layout byte for byte.

```
cd /workspaces/Indy-init && git fetch -q origin && git switch -q -C main origin/main && jq --indent 1 --arg c "$(git rev-parse HEAD)" '.status = "OWNER PIN: competition lock committed by kemiller2002 under ODR-COMP-LOCK, ODR-COMP-P17, -P18 and -P19 (APR-COMP-OWNER-3), 2026-11-07" | .indyInit.commit = $c' kickoff/competition/toolchain.lock.proposed.json > kickoff/competition/toolchain.lock.json && git commit -qm "Competition toolchain lock: process-evidence rules for causa-t0 (ODR-COMP-P17, ODR-COMP-LOCK, APR-COMP-OWNER-3)" kickoff/competition/toolchain.lock.json && git push -q origin main
```

APR-COMP-OWNER-3 is the id `decide` assigns: it is the next free APR-COMP-OWNER number. Use the id step 1 printed if it differs.

**3. Approve the re-govern.** owner-freeze.fsx cannot write this record. `decide` writes only `{decision_id, action: "decide", option, pins}`, while `r9.fsx reGovernCauses` accepts only an operator change `{action: "re-govern", indyInitCommit: X}`. You therefore write it yourself with jq.

The command does the following:
- Names the commit of step 2 as `L`: the last commit on origin/main that changed the lock.
- Prints what that commit changed, which should be only `kickoff/competition/toolchain.lock.json`.
- Appends one operator approval with the next free APR-COMP-OWNER id. The rest of the file is unchanged: `jq .` reproduces it byte for byte.
- Commits it as you on a branch, opens the pull request and merges it.

```
cd /workspaces/Indy-init && git fetch -q origin && L=$(git log -1 --format=%H origin/main -- kickoff/competition/toolchain.lock.json) && git diff --name-only "$L^" "$L" && git switch -q -C owner/competition-re-govern origin/main && jq --arg c "$L" --arg d "$(date -u +%F)" '.approvals += [{id: ("APR-COMP-OWNER-" + ([.approvals[].id | capture("^APR-COMP-OWNER-(?<n>[0-9]+)$").n | tonumber] | max + 1 | tostring)), authority: {kind: "operator", id: "kemiller2002"}, date: $d, changes: [{action: "re-govern", indyInitCommit: $c}]}]' kickoff/rehearsal-007/approvals.causa.json > /tmp/approvals.json && mv /tmp/approvals.json kickoff/rehearsal-007/approvals.causa.json && git commit -qm "Owner re-govern of kemiller2002/causa from Indy-init ${L:0:12} (competition lock, ODR-COMP-P17)" kickoff/rehearsal-007/approvals.causa.json && git push -q -u origin owner/competition-re-govern && gh pr create --repo kemiller2002/Indy-init --base main --head owner/competition-re-govern --fill && gh pr merge owner/competition-re-govern --repo kemiller2002/Indy-init --merge
```

The record has the same shape the re-govern cases of `tools/release-gate-repair/tests.fsx` exercise: `{"id": "APR-COMP-OWNER-N", "authority": {"kind": "operator", "id": "kemiller2002"}, "changes": [{"action": "re-govern", "indyInitCommit": "<L>"}]}`. `r9.fsx check-governance` (`reGovernCauses`) reads it from Indy-init's owner-merged main. It accepts a later `.echelon/r9-governance.json` only if:
- its `indyInitCommit` is that commit, or a descendant that changes only `kickoff/rehearsal-007/approvals.causa.json`;
- it keeps the T0 record's `toolchainLock` and `completedBeforeGovernance`.

## 5. Builder steps in kemiller2002/causa (after step 3 is merged)

The governed gate checks out Indy-init at `.echelon/r9-governance.json` `indyInitCommit`, which is currently dae2347. It reads the lock, and through the lock the rules, from that commit, so it sees the new lock only after a re-govern. `L` below is the commit named in step 3's record.

```
git -C Indy-init fetch -q origin && git -C Indy-init checkout -q --detach L
cd causa && git fetch -q origin && git switch -c setup/re-govern-competition origin/main
dotnet fsi ../Indy-init/tools/r9/r9.fsx -- govern --indy-init ../Indy-init --target . --lock kickoff/competition/toolchain.lock.json
git diff --stat      # expected: only .echelon/r9-governance.json, indyInitCommit dae2347… -> L
git commit -am "Re-govern from Indy-init L (owner re-govern approval APR-COMP-OWNER-N)"
git clone -q --filter=blob:none --branch main https://github.com/kemiller2002/Indy-init ../indy-init-main
dotnet fsi ../Indy-init/tools/r9/r9.fsx -- check-governance --indy-init ../Indy-init --target . \
  --trusted-operator kemiller2002 --trusted-indy-init ../indy-init-main --approvals kickoff/rehearsal-007/approvals.causa.json
git push -u origin setup/re-govern-competition   # open the PR and merge it with a merge commit
```

- `govern` keeps the T0 record's lock and `completedBeforeGovernance`, and it refuses a different `--lock`. It also rewrites `ros.json` qualityEvidence and the gate workflow from Indy-init at L. Both are unchanged since dae2347, so they show no diff unless main changed them.
- `check-governance` must report PASS before the push.
- The gate's slice step (`verification-gate.yml:110`) treats `.echelon/r9-governance.json` as implementation, because it is not in that step's list of records. A Verification gate run on this `setup/*` pull request therefore refuses with "changes implementation but its branch does not name a slice". causa main has no required status checks (its branch protection requires a pull request with 0 approvals), so the pull request can still merge. The gate verdicts that count are the slice pull requests after it.
- After the merge, re-run the Verification gate on slice/SL-T10 (PR #2). Its candidate `refs/pull/2/merge` then contains the new record, so the gate uses L's lock and `kickoff/competition/process-evidence-rules.json`.
