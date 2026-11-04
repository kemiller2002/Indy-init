# Rehearsal 009: why the run built only the catalog and stopped at 115 minutes

**Summary.**
- Rehearsal 009 built and verified the 38-requirement investigation-content catalog. It then stopped at 02:25Z, 115 minutes into a 420-minute window.
- At that point, 161 P0 requirements of the frozen v4.1.2 plan had not been started. Neither had their 613 obligations or 660 plan tests.
- Four causes combined:
  1. Every document the agent worked from named the catalog as the build target. The full plan appeared only as one trailing line of the readiness checklist.
  2. Nothing compiled the full scope into work items. Praxis showed the agent one mission item.
  3. Three defects in the frozen plan's own commands made the release gate fail before it judged anything.
  4. No rule said when a run may stop. So the agent treated "the gate cannot pass" plus "save compute" as a reason to stop.

Each finding below is marked:
- **Confirmed** when a file or record shows it.
- **Hypothesis** when the evidence is indirect. The Rehearsal 009 launch prompt is the main gap: it is in no repository this investigation could read, and API access to the rehearsal repository was not granted.

Sources:
- `kemiller2002/indy-init-rehearsal-009` at `main` (shallow clone; `evidence/FINAL-REPORT.md`, `.ros/`, `.echelon/`, `.claude/settings.json`);
- Indy-init at `bc3c381` (the Rehearsal 009 source commit) and this branch.

## 1. Which instructions made the 38-requirement catalog the implementation target

| # | Source | What it says | Status |
|---|---|---|---|
| 1.1 | `docs/rehearsal-009-content-readiness/FINAL-READINESS.md`, "Is Rehearsal 009 ready?" and "Acceptance scenarios that must pass" | It lists the readiness verdict and four acceptance scenarios: INVESTIGATION-CONTENT-E2E, -NEGATIVE, -MOBILE and -CONCLUSION. All four are catalog scenarios. The P0 plan is item 5: "**Unchanged:** every v4.1.2 P0 obligation, the record obligations through `verify-process`, and the stale-SHA refusals of the gate." | Confirmed |
| 1.2 | `docs/requirement-catalogs/CAUSA-INVESTIGATION.md`, "Using it for the November 7 build" | It gives four steps, all of them about the catalog pipeline (data-requirement-id, probes, workflow, `requirement-verification.json`, command bodies). It does not mention the DAG or the P0 plan. | Confirmed |
| 1.3 | Rehearsal 009 `README.md` | "Requirements: `CAT-CAUSA-INVESTIGATION` (INV-001..038)". No other requirement set is named. | Confirmed (what the agent took as its target) |
| 1.4 | Rehearsal 009 `evidence/FINAL-REPORT.md` §16 | "The approved scope for this run was the content catalog and the INVESTIGATION-CONTENT scenarios." | Confirmed (the agent's belief) |
| 1.5 | The Rehearsal 009 launch prompt | Not available. §1.4 and the README suggest it named the catalog as the target and did not state the full P0 scope. | **Hypothesis H1** |

**Conclusion.** The full v4.1.2 scope was in force, but it was never stated as the target. Every catalog item had its own section, scenario and tool. The P0 plan, about four times larger, was one "unchanged" line.

## 2. Where the full frozen v4.1.2 requirements were supposed to be included

| Where | What it carried | Did it reach the agent's work list? |
|---|---|---|
| `kickoff/rehearsal-009/toolchain.lock.json` `requirementsPackage` | v4.1.2, manifest `1977780f…`, approval APR-V412-OWNER-3 | No. It is a pin, not a work list. |
| `requirements-compiled/v4.1.2/execution-dag.json` (T10–T36) and `AGENT-RULES.md` | 27 timed tasks with `covers` | No. Nothing created Praxis items from it. Separately, **78 of the 161 P0 requirements are covered by no DAG task id**, and 62 of those have executable obligations (computed: `scope compile`, the `WI-GEN-*` items). |
| `requirements-compiled/v4/verification-plan.json` | 161 P0 requirements, 613 obligations, all bound | Only through the release gate, which judges results. It does not assign work. |
| `kickoff/indy-init.kickoff.json` | 12 priority slices (8 "never-cut") and screens S01–S21 | Conditor turned it into one Praxis item, `COND-MISSION-001` ("Build … from the Conditor execution contract"). |
| `.echelon/verification-scope.json` (written by `r9.fsx govern`) | `"requirements": []` | Empty for the whole run. The development gate therefore judged nothing. |

**Conclusion (confirmed).** The full scope existed in three frozen files. No step turned them into one list of requirements and work items that the agent's own tools (Praxis, the gate scope) showed.

## 3. Why the agent treated the failed release gate as grounds to stop

**Confirmed (FINAL-REPORT §1, §11, §12, §16):**
1. **The gate could never pass.** Its `run-suites` step ran the frozen plan's own commands, and three of them cannot execute:
   - `SU-OWN-DOTNET` and `SU-DOTNET-LIVE` pass several projects to one `dotnet test`. SDK 10.0.402 refuses this (`MSB1008`).
   - `SU-BROWSER-WEBKIT` names a `webkit` Playwright project the application did not define.
   - Separately, the gate workflow installs no browser.

   This was reproduced on a deterministic fixture tree (#101's harness cases `suite-preflight-fixture-*`) and on the Rehearsal 009 application itself (`docs/release-gate-repair/evidence/`).
2. **A failed verification was read as a reason to stop implementing.** The report says: "the remaining release-gate refusals cannot be cleared by more application work inside the rules", and "Writing about 540 plan-bound tests would not change the release-gate verdict while the plan's commands cannot run. Under the compute constraint, I stopped." Implementation status and verification status were one variable.
3. **Completion was impossible to record.** The governance policy (`tools/r9/governance.json`: `requiredFacets: [verification-matrix-satisfied]`) needs a passing gate matrix to complete any work item. Its one work item, `R9-CONTENT-001`, therefore stayed `active`. With no recordable progress, more work looked worthless.
4. **No stop rule existed.** No document listed the permitted stop conditions, so "save compute" filled the gap.

**Hypothesis H2.** The launch prompt asked the agent to minimize compute or tokens, without saying that this never justifies leaving approved work. "Under the compute constraint" points to such a sentence.

## 4. Did the launch prompt conflict with the readiness documentation?

- **Confirmed conflict between what the agent did and the readiness documentation.** FINAL-READINESS item 5 requires every v4.1.2 P0 obligation to pass at the release candidate. The agent's stated scope (§1.4) excluded them.
- **Whether the prompt itself conflicted** (H1) cannot be confirmed without the prompt. Either way, the readiness document was weak enough to allow that reading: item 5 is phrased as "unchanged", not as work.
- A second conflict is confirmed inside the readiness material. FINAL-READINESS says "READY", but the plan's own commands could not run on the pinned SDK. The readiness harness (76 cases) never ran a suite command of the plan, only gate logic on synthetic results.

## 5. Did Praxis expose all required work items before implementation?

**No (confirmed).**
- `.ros/work/queue.json` holds one item, `COND-MISSION-001`.
- `.ros/context/current.json` holds `ROS-INSTALL-3-25-0` (complete) and `R9-CONTENT-001` (active). The agent created `R9-CONTENT-001` *after* the first application commits (FINAL-REPORT §12).
- No check refused implementation before a complete work-item inventory existed.
- Replaying that queue through the new coverage check gives `WORK_ITEM_MISSING` for all 36 work items of the approved scope (`evidence/coverage/r9-queue-verdict.json`).

## 6. Was the agent authorized to continue implementing independent requirements?

**Yes (confirmed).**
- Implementing P0 requirements and writing tests with the plan's identities needed no owner decision. Both are in-scope and reversible (AGENTS.md "Core Rules").
- `.claude/settings.json` in the rehearsal repository allowed opening, pushing and merging pull requests "once every required check is green". The required checks were Build and test, Echelon foundations and Praxis validation, and all were green. The verification gate was not a required check (FINAL-REPORT §11).
- Only two changes needed the owner: the plan's commands (a frozen-package change) and the gate workflow (governance-checked).
- The agent identified both correctly. But neither blocked implementation. They blocked only the *verification* of three suites.

## Causes, ranked

| Cause | Type | Corrected by |
|---|---|---|
| C1. The catalog was presented as the target; the P0 plan was one "unchanged" line | instruction defect | One scope manifest of 199 requirements (`scope-manifest.json`); STARTUP-INSTRUCTIONS §1 |
| C2. No work-item inventory; Praxis showed one mission item; 78 P0 requirements had no DAG task | process defect | `scope preflight` and `coverage`, generated `WI-GEN-*` and `WI-INV-*` items, the scope checkpoint required by `r9.fsx check-sequencing` |
| C3. Plan commands that cannot run (MSB1008, no webkit project, no browser install) | frozen-package and integration defect | v4.1.4 candidate (#101), `r9.fsx suite-preflight` (with `--require-tests`), the gate installs browsers and reads the package from the pinned lock |
| C4. Implementation and verification were one status; a blocked runner looked like no progress | model defect | `Progress`: separate axes, `suite-blocked` touches verification only |
| C5. No stop conditions; compute saving filled the gap | rule defect | `Continuation.decide` and `stop-decision`: four permitted stops plus the deadline; every other request refused with machine-readable remaining work |
| C6. Dokimos had no `url-input` component (INV-029) | tool defect | kemiller2002/dokimos#67, merged (a release is needed) |
