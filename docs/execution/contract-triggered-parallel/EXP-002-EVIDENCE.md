# EXP-INDY-002 evidence for CTP-001

**Sources** (PR #140, branch `claude/exp-indy-002-contract-first-hhlktz`, final candidate `5151cdf`):
- `docs/experiments/exp-indy-002/FINAL-REPORT.md`
- `CONTRACT-REGISTRY.json`, with history at `d544ead`, `95fb820`, `428ae47` and `99824ee`
- `CONTRACT-LIFECYCLE.jsonl`
- `CONTRACT-CHANGES.json`
- `DEFECTS.json`
- `INTEGRATION-RESULTS.json`
- `CI-RUNS.json`
- the agents' timelines

Every figure below is labelled either **observed** (measured in the run) or **modelled** (computed, never run).

## 1. Preserved findings

| Finding | Value | Kind | Source |
|---|---|---|---|
| First accepted end-to-end workflow | **T+23.0** (21:38:05Z) | observed | FINAL-REPORT §1, INTEGRATION-RESULTS INT-1 |
| Sequential equivalent | **T+31.4** | **modelled** (task durations re-sequenced, not run) | FINAL-REPORT §3 Q8 |
| Contract revisions | **3**: CR-1 (accessible names for secondary controls), CR-2 (picker and option labels), CR-3 (F# type change to SM-SAVE) | observed | CONTRACT-CHANGES.json |
| Merge conflicts | **0** across 6 agent-branch merges | observed | FINAL-REPORT §4 |
| Integration repair | **3.0 min**, within a 4.6 min INT-1 | observed | INTEGRATION-RESULTS |
| Static checks missed a behaviour that browser tests found | D-3: the UI auto-saved on Start, against INV-004. Unit tests and the static binding check passed; only independent acceptance test AT-08 caught it | observed | DEFECTS.json |
| Wave 2, release to acceptance | 3.5 min, 0 defects | observed | INTEGRATION-RESULTS INT-2 |

## 2. Limits stated by the experiment (do not over-read)
- **Small sample:** n = 1, on a 39-requirement slice.
- **Persistence:** it used Limen Storage (localStorage), with **no GitHub write**.
- **Verification:** at experiment level only. The browser test's author also wrote the contracts. The governed gates were not run.
- **Incremental release:** the protocol changed at T+2.6 (DEV-1), so incremental release is evidenced mainly by wave 2.
- **Browser and agents:** Chromium only. The agents ran in one 4-vCPU container.

## 3. Final CI on the candidate: **NOT VERIFIED**
- **Where the report stands.** The report's §5 still reads `CI_RESULT_PLACEHOLDER`.
- **What was checked.** PR #140's checks were read at 22:14Z and at about 22:35Z, on head `5151cdf`, the docs-only successor of `76d88cd`.
- **Results so far.**
  - Passed: offline-factory, offline-contract-tests, frozen-v4, suite-commands, release-gate-repair, r9-readiness-gates, fsharp-tooling, and r10-adversarial-cases shards 1 and 2.
  - **`r10-adversarial-cases (0)` was still in progress, more than 45 minutes after it started at 21:47:42Z.** That is consistent with the apt-mirror stall seen on PR #139 the same evening.
  - The aggregate `r10-adversarial` therefore has no result.
- **Consequence.** This document does **not** treat EXP-002's final candidate as CI-verified. No group in its lifecycle log reached VERIFIED (confirmed by `ctp-check` replay).

## 4. CI waste, observed
- **What happened.** Three Preflight runs were cancelled by newer pushes: on `2f7ebf7` and `c7e97c4` (cancelled), and on `99824ee` (superseded).
- **Causes:**
  - CI-F1: four pushes in 4 minutes;
  - CI-F2: the stop hook forced pushes;
  - CI-F3: a forced push cancelled a near-complete run on `76d88cd`.
- **Reproduce:** `ctp-check ci --runs <(git show 5151cdf:docs/experiments/exp-indy-002/CI-RUNS.json)` reports all three.

## 5. Protocol validation against this evidence
- **Reproduce with:** `dotnet fsi tools/execution/ctp-check.fsx -- test --indy-init .`.
- **Inputs.** The five EXP-002 files are committed as fixtures in `evidence/exp-002/`, with their source commits in the file names and `SHA256SUMS`. When PR #140's commits are in the clone (`git fetch origin claude/exp-indy-002-contract-first-hhlktz`), the test also checks that each fixture equals `git show <commit>:<path>` byte for byte. Without those commits, that check reports NOT-RUN.
- **How the replay works.** It reads EXP-002's real files at their historical commits. An adapter maps EXP-002's registry onto the CTP schema field for field and **invents nothing**:
  - a UI action listed without its own name stays unnamed;
  - a free-text transition stays unstructured;
  - EXP-002 registered no acceptance obligations, so none are added.
- **Results: 57 of 57 cases pass**, including 5 provenance checks and 21 replay cases. Each claim below is backed by a named case.

| Question | Result | Mechanism | Strength |
|---|---|---|---|
| Would the protocol have caught the three contract revisions earlier? | **CR-1: 5 of 7 contracts caught at VALIDATED (v1, `d544ead`)**: INV-009, -013, -014, -030, -034 each list several UI actions under one label. **Not caught: INV-039 and INV-040.** Their controls were missing from the registry altogether (a missing "New investigation" action; link text unspecified), and a schema check cannot find a control that was never listed. **CR-2: caught at v1**: INV-030's nested fields have no labels; INV-031's "one of …" values have no option labels. **CR-3: the underspecified save transition (INV-015) is refused at v1** as unstructured. The rule forces from/event/to/carries per transition, which is where the missing `ReadBackMatched` payload had to be stated. | automated: CTP-C-CONTROL-NAME, -NESTED-LABEL, -OPTION-LABEL, -TRANSITION-STRUCTURE | CR-1 partial (5 of 7), CR-2 full. CR-3 is **forced to be specified, not proven**: the check refuses the free text, and a human still has to write the right payload |
| Would it have caught the auto-save mismatch (D-3)? | INV-004's transition at v1 has only a domain unit test; the rule refuses it without an AT- (and UI-) obligation. The defect itself is caught only by **executing** that obligation, as AT-08 did. VERIFICATION-POLICY requires the independent browser test at every integration, and `ctp-check integration` refuses an integration record without an acceptance run | automated obligation (CTP-C-TRANSITION-TEST) plus integration evidence gate (CTP-I-ACCEPTANCE) | detection, not prevention |
| Would it have preserved zero shared-file merge conflicts? | `ctp-check ownership` over EXP-002's 37 experiment files with the role map it used: 0 errors (no unowned and no shared file). EXP-002's 6 domain-labelled commits touched only domain-owned files and the domain agent's own timeline. The UI agent's commits carry no role prefix, so this cannot be attributed per commit for the UI | automated: CTP-O-SHARED, CTP-O-UNOWNED | preserves the precondition. It cannot prove absence of conflicts in future runs |
| Would it have avoided redundant CI cancellations? | The three cancellations are **reported** by `ctp-check ci`. **Avoidance is procedural:** work branches without PRs, and the integration coordinator pushes the PR branch once per candidate (INTEGRATION-POLICY §4). No automated check can refuse such a push | review gate plus measurement | **not demonstrated by an automated check**; listed as a gap |
| Would it have required real-provider evidence for durable GitHub persistence? | Yes, for any contract or integration that claims `durable-github`. CTP-C-PERSISTENCE refuses the contract without `realProviderEvidence`; CTP-I-REAL-PROVIDER refuses a REST double (case "durable persistence on a REST double"). EXP-002 never claimed durable persistence (localStorage; INV-037 excluded for that reason), so there is nothing to replay | automated | full for claims made in the registry |

**Lifecycle findings in EXP-002's own log** (replay):
- CG-1-START v2 and the other re-hashed groups were RELEASED without DEFINED or VALIDATED for the new version (CTP-L-ORDER).
- Revised versions cite no change request (CTP-L-CHANGE-REQUEST).
- IMPLEMENTING was recorded after code had already started.
- INTEGRATION_READY was recorded by the "verification" actor, under combined roles.
- No group reached VERIFIED.

None of these broke the run, but under CTP-001 each would be refused or corrected.

**Mutation check.** Removing each of ten rules makes its named cases fail:
- control name;
- nested label;
- option label;
- transition structure;
- transition acceptance;
- lifecycle order;
- change request;
- real provider;
- independence;
- shared file.
