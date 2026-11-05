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
- **Observed:** two Preflight runs were cancelled by newer pushes, on `2f7ebf7` and `c7e97c4`.
- **Not observed:**
  - The log's third entry, `99824ee`, says only "superseded by 43d1b28 (expected cancel)", which is a prediction.
  - The cancellation on `76d88cd` (CI-F3) is described in the findings text, but it is not in the `runs` array.
- **Causes:**
  - CI-F1: four pushes in 4 minutes;
  - CI-F2: the stop hook forced pushes;
  - CI-F3: a forced push cancelled a near-complete run.
- **Reproduce:** `ctp-check ci --runs evidence/exp-002/5151cdf.CI-RUNS.json` reports exactly the 2 observed cancellations.

## 5. Protocol validation against this evidence
**Reproduce with** `dotnet fsi tools/execution/ctp-check.fsx -- test --indy-init .`. The result is **91 of 91 cases** with PR #140's commits in the clone. In a shallow clone, such as CI's, it is **86 of 86, with 5 NOT-RUN**.

**Fixtures.** The EXP-002 inputs are committed in `evidence/exp-002/`:
- the registry at `d544ead` (v1) and at `99824ee` (final);
- the lifecycle log, the integration results and the CI log at `5151cdf`;
- the experiment's 37 file paths and its role map from PLAN.md, transcribed literally.

**How tampering is caught.**
- Each fixture's SHA-256 is **pinned in the checker's source**. Editing a fixture without editing `ctp-check.fsx` fails in every clone, shallow ones included. A one-byte change is a test case of its own.
- When PR #140's commits are in the clone, each fixture is also compared byte for byte with `git show <commit>:<path>`. Without those commits, that comparison is reported **NOT-RUN** and is never counted as a pass.

**The adapter** maps EXP-002's registry onto the CTP schema. It adds exactly three stated defaults, for fields EXP-002's format lacks:
- `requirements = [its own ID]`;
- `group = "CG-?"`;
- `persistence = "local"` when the contract mentions persistence, otherwise `"none"`.

Everything else is mapped as written. A contract's `label` names only its first UI action, and only on action contracts. Every other UI action needs its own name key, as CR-1 later added.

| Question | What the replay shows (named cases) | Strength |
|---|---|---|
| Would the protocol have caught **CR-1** earlier? | **Contract level:** at v1 the control-name rule flags **exactly** INV-009, -013, -014, -030 and -034, the five contracts listing several UI actions under one label. It flags no other contract (precision 5/5) and none at final. **Control level:** it reports 8 unnamed controls, while CR-1 changed 13 names. **Missed:** <ul><li>INV-039: its "New investigation" control was absent from the registry.</li><li>INV-040: its control *was* listed, under a wrong name, and name correctness is not checkable.</li><li>The three per-item Edit controls: never listed as UI actions.</li></ul> Duplicate names within a contract are now refused too (synthetic case). | automated, partial: 5 of 7 contracts, 8 of 13 names |
| **CR-2** earlier? | At v1, INV-030's nested fields have no labels and INV-031's "one of …" values have no option labels. Both pass at final. | automated, full |
| **CR-3** earlier? | **Not demonstrable from EXP-002's data.** Its registry format has no structured transitions, so CTP-C-TRANSITION-STRUCTURE and -CARRIES refuse **every** transition, before and after the fix (format-level case). CTP-001 requires each transition to state what its target state carries (CTP-C-TRANSITION-CARRIES), which is where `Saved` needed version and time. That rule refuses an omitted payload, as a synthetic case shows. It cannot check that a stated payload is right. | rule exists; no EXP-002 replay evidence |
| The **auto-save mismatch (D-3)**? | **Not demonstrable from EXP-002's data**, which registered no acceptance obligations. CTP-C-TRANSITION-TEST refuses a transition without both an implementer test and an AT- test (synthetic case). The defect itself is caught only by **executing** that test, which AT-08 did. CTP-I-ACCEPTANCE refuses an integration without a non-empty, fully passing independent acceptance run. | rule plus integration gate; detection by execution, not prevention |
| **Zero shared-file merge conflicts** preserved? | Ownership over EXP-002's 37 files, using PLAN.md's literal role map: **none shared**. PLAN names **no owner for 4 files**: `.gitignore`, `Directory.Build.props`, `Exp002.Contracts.fsproj` and `tests/acceptance/e2e.mjs` (named cases). CTP-001 would have refused those 4 until they had an owner. The 6 domain-labelled commits touched only domain files and the domain agent's own timeline. UI commits carry no role prefix and cannot be attributed. | automated precondition; cannot prove future absence |
| **Redundant CI cancellations** avoided? | 2 observed cancellations are **reported**. Avoidance is procedural (INTEGRATION-POLICY §4). No automated check refuses such a push. | **not demonstrated**; a gap |
| **Real-provider evidence** for durable GitHub persistence required? | CTP-C-PERSISTENCE refuses `durable-github` without `realProviderEvidence`. CTP-I-REAL-PROVIDER refuses a REST double. With `--registry`, it also refuses an integration that records a weaker mode than the registry declares (synthetic cases). EXP-002 never claimed durable persistence. | automated, for what the registry and record declare |

**Contract-level result.** Under CTP-001, EXP-002's registry would **not reach VALIDATED at all**. 27 of 37 contracts fail at v1, and 28 of 39 still fail at final, almost all on rules that EXP-002's format never had:
- no error text on field validations (CTP-C-ERROR: 28 and 30);
- no acceptance obligations (CTP-C-TESTS: 26 and 28);
- unstructured transitions (18 and 20).

CTP-001 is stricter by design. The CR-1 and CR-2 claims above hold **rule by rule**, not as "these contracts would have validated sooner".

**Lifecycle findings in EXP-002's own log** (named cases):
- CG-1-START v2 was RELEASED without DEFINED or VALIDATED for the new version (CTP-L-ORDER).
- Revisions cite no CR-n id (CTP-L-CHANGE-REQUEST).
- IMPLEMENTING and INTEGRATION_READY were recorded by "verification", reflecting combined roles (CTP-L-ROLE).
- No group reached VERIFIED.

**Mutation check.** This was run manually, not as a committed harness. For each rule, its code was changed so that it could no longer fire, and the suite was run again. Every case named for that rule then failed. The rules mutated:
- control name, and distinct names;
- nested label;
- option label;
- transition structure, and carries;
- transition acceptance;
- lifecycle order;
- staged sides;
- change request, and its CR-n format;
- the VERIFIED evidence format;
- real provider, and durable versus local;
- independence, and case-insensitive independence;
- acceptance with zero tests;
- the integration staged check;
- shared file;
- the fixture pin.

The two mutations that first survived (the evidence SHA format and the fixture pin) now have their own cases.
