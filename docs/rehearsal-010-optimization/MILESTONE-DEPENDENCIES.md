# Rehearsal 010: milestone dependencies

**Status:** read-only analysis; nothing was changed in any repository. Machine-readable form, consumed by the schedule model: `inputs/milestones.json`.

**Sources** (all in this repository):
- **Scope:** the approved scope, `docs/rehearsal-010-readiness/scope-manifest.json` (scopeSha256 `59bfef5a…`). It has 205 requirements and 37 work items.
- **Slices:** `docs/rehearsal-010-readiness/slice-execution-manifest.json`.
- **DAG:** `requirements-compiled/v4.1.4/execution-dag.json`, with dependency reasons from `analysis/task-adjustments.json` and `requirements-compiled/v4/dependency-graph.json` (edges).
- **Journeys:** `verification/journeys/bindings.json`.
- **Contracts and ownership:** `requirements-compiled/v4.1.4/contracts/*.fs`, `analysis/interface-specifications.json` and `agent-file-ownership.json`.
- **Data repository:** the companion draft `DATA-REPOSITORY-CONTRACT.md`, cited as **DRC §n**.

## 0. Method and conventions

- **Exact IDs only.** Every requirement ID listed is present in the scope manifest. `milestones.json` assigns each ID to exactly one milestone: the earliest one that needs it.
- **Additions.** A milestone contains:
  - the owner-named requirements;
  - the requirement IDs of the named journeys;
  - the **direct** `dependsOn` of those (scope manifest);
  - requirements a named requirement's own text makes necessary. These are marked *text* and justified.
- **Forward dependencies.** A direct dependency that is the owner's later milestone is **not** pulled forward. It is reported as a *forward dependency* instead, because pulling it would merge the milestones.
- **Out-of-scope dependencies.** Dependencies outside the scope manifest are reported, never added. They are `STORAGE-005`, `STORAGE-007`, `UI-012`, `WDL-013`, `PF-010`, `PF-021` and `OD-INDY-23`; the scope manifest has no requirement with these IDs.
- **Line numbers.** Lines cited as "scope:N" are lines of `scope-manifest.json`, at the `"id"` of the requirement or work item.

**Structural fact that shapes every milestone.**
- Work items are monolithic, and a slice merges only when "every mandatory applicable obligation of the slice passes" and "every governed journey bound to the slice's requirements passes" (`slice-execution-manifest.json`:101, `mergeConditions`).
- The milestones cut across work items:
  - `WI-INV-WORKSPACE` holds 30 requirements and 170 mandatory obligations spanning M2 to M5.
  - `WI-GEN-UI` holds 21 requirements spanning M1, M2 and M5.
  - `WI-GEN-EXT-DATA` spans M1 and M3.
  - `T19` spans M2 and M4.
- So, under the approved 37-item scope, **no milestone below M5 can merge as a slice on its own**. The finer split exists only as the unapproved successor scope `docs/rehearsal-010-optimization/proposals/scope-manifest.successor.json` (ODR-R10-19, `docs/rehearsal-010-optimization/OWNER-DECISIONS-REQUIRED.md`:7).

---

## M1: connect to the data repository with the real GitHub provider

### 1. Requirements

| ID | Why | Evidence |
|---|---|---|
| EXT-DATA-002 | named; J1-01 | baseline:2635; bindings.json:11 |
| EXT-DATA-003 | named; J1-02 | baseline:2655; bindings.json:12 |
| UI-001 | named; J1-02 | baseline:238; bindings.json:12 |
| EXT-DATA-013 | **dependsOn** of UI-001 (scope:6632). UI-001-O1 itself says that after Connect the dashboard shows the four catalog cases (baseline:246). | scope:6632, :1943 |
| EXT-DATA-004 | *text*: EXT-DATA-003-O1 requires "a scoped credential through a secure connection flow". EXT-DATA-004 is the credential rule, and the frozen `Credential.T` cites it (03-IF41-WIRE.fs:32-43; IF41-APP requirements include EXT-DATA-004, interface-specifications.json:328-) | baseline:2675 |
| EXT-DATA-005 | *text*: Connect must check repository and branch permissions. `ConnectResult` cites EXT-DATA-005 (04-IF41-ERRORS.fs:26-27), and so does SM-CONNECTION T-N02 (`state-machine-contracts.json`:3008) | baseline:2705 |
| EXT-DATA-006 | *text*: O1 "No data or repository mutation occurs on connect or read" | baseline:2733 |
| EXT-DATA-007 | *text*: the connection states, and "Never imply a successful connection merely because a token was supplied" | baseline:2745 |
| EXT-DATA-012 | *text*: the data repository is configurable and isolated, and its content is untrusted even when authenticated | baseline:2850 |
| EXT-DATA-001 | *text*: the counterpart of EXT-DATA-002-O2 ("no automatic fallback to bundled fixtures"); nothing may be bundled | baseline:2612 |
| PF-003 | *text*: "real GitHub provider". O3/O4: fixture mode must not activate silently, and the active mode must be displayed | baseline:2012 |
| PF-015 | *text*: every catalog read needs an authenticated credential, and there is no anonymous mode. T-N02 cites PF-015 | baseline:2323; state-machine-contracts.json:3008 |

- **Journeys:** J1-01 and J1-02.
- **Not added:** EXT-DATA-013's own dependencies STORAGE-005 and STORAGE-007 are outside the scope manifest (scope:1943).
- **Owner choice.** Through EXT-DATA-013, M1 as named already includes **loading and replaying the four catalog cases**. To keep M1 "connect only", the owner would have to move the second half of UI-001-O1 and EXT-DATA-013 to M2. That is a scope decision, not a dependency fact.
- **Note on J1-02.** It is checked by a visible "New investigation" control after Connect (`j1-create-persist.spec.mjs`:14-17), against the private acceptance repository (`LIVE-PROVIDER-PREFLIGHT.md`:22), not against causa-data (DRC §6 S8, C4).

### 2. Work items and slices implementing them

| Requirement | Work item / slice | Owner (scope) |
|---|---|---|
| EXT-DATA-001..008, EXT-DATA-012 | WI-GEN-EXT-DATA / SL-WI-GEN-EXT-DATA (scope:13570; slice:2294; governed assertions J1-01, J1-02, J1-03; 21 mandatory obligations; real provider) | integration |
| UI-001 | WI-GEN-UI / SL-WI-GEN-UI (scope:13647; slice:2645; J1-02, J1-06, J2-05, J2-08, J3-02; 85 obligations) | ui |
| EXT-DATA-013 | T25 / SL-T25 (scope:13343; slice:1512; 3 obligations; real provider) | persistence |
| PF-003, PF-015 | WI-GEN-PF / SL-WI-GEN-PF (scope:13591; slice:2390) | integration |

- **T20 is empty.** In the DAG, T20 ("Connect flow and GitHub provider") `covers` `prefix:EXT-DATA-` (execution-dag.json:315). But in the scope manifest T20 has `requirements: []` (scope:13238), and SL-T20 has 0 mandatory obligations. The EXT-DATA requirements were assigned to WI-GEN-EXT-DATA as "no execution-DAG task covers" them (scope:13570).
- **Consequence.** The connect *requirements* wait on T17, while the connect *implementation the DAG names* (T20) waits on T19.

### 3. DAG chain to integrate first

```
T10 → T14 ─┐
T10 → T12 ─┴→ T13 ─┬→ T17 ← T16 ← T11 ← T10      (WI-GEN-EXT-DATA, WI-GEN-UI, WI-GEN-PF wait on T17)
                   └→ T15 ────────────────┐
T10 → T18 → T19 (also T12, T14) → T20 ────┴→ T25 (EXT-DATA-013)
```

- **Dependency lists.** T17 dependsOn T13, T14 and T16 (execution-dag.json:228). T19 dependsOn T12, T14 and T18 (:272). T20 dependsOn T18 and T19 (:315). T25 dependsOn T20 and T15 (:439).
- **Timing.** By the DAG's own earliest starts, T25 starts at T+164 and ends at T+184. T17 ends at T+114.

### 4. Genuine versus artificial dependencies

**Case: a read-only connect waiting on T19 through T20.**
- **The edge reason.** The T19→T20 edge is justified as "the connect flow drives the session-memory save process (ADR-R7-001)" (`requirements-compiled/v4/dependency-graph.json`:1402). No v4.1.4 adjustment restates it; T20 has only an estimate note (`task-adjustments.json`:133-134).
- **What a read-only connect and load actually use:**
  - `Connector.ConnectRequest` and `InterpretConnect` (04-IF41-ERRORS.fs:34-36);
  - SM-CONNECTION (06-IF41-APP.fs:10-25);
  - SM-LOAD `InterpretLoad`/`StepLoad` (05-IF41-SAVE.fs:130-146, :223-224);
  - `StorageCodec.Verify` (02-IF41-STORAGE.fs:31);
  - `DomainKernel.Replay` (01-IF41-DOMAIN.fs:15-19).
- **Its only contact with SM-SAVE.** T-L03's effect is "SM-SAVE initial Saved at that commit" (state-machine-contracts.json:3357), which is `SaveMachine.Initial: VerifiedInvestigation -> InvestigationSession` (05-IF41-SAVE.fs:211). The interface specification calls it "total" (interface-specifications.json IF41-SAVE `operations[0]`). The other contact is T-L07's refresh guard, which reads whether SM-SAVE is Saved (:3477). No `SaveMachine.Step`, `Interpret`, reconciliation, rebase or conflict logic is on the read path.
- **EXT-DATA-006-O1 is a check, not a need.** "No mutation on connect or read" (baseline:2733) is a property the read path must have. It does not require the write path to exist first.
- **Verdict:** the T19 → T20 → T25 chain is **artificial for connect, list and read-only open**.
- **What stays genuine:**
  - **T18 → (connect/read).** "built and tested against the recorded provider behaviour" (dependency-graph.json:1397). Connect's 403/404 forms are classified from `analysis/github-error-matrix.json`:60-74.
  - **T13 → (load).** EXT-DATA-005-O1 and EXT-DATA-013-O2 require "the F# history-admissibility pipeline" and "F# replay", and `Verify` needs `Replay`. The DAG does not express this edge for T20 or T25 directly; it waives T13 before T20 (execution-dag.json waivedPrecedences, `task-adjustments.json`:172-177) and relies on T15.
  - **T15 → T25.** "catalog records are v1.0.0 file sets replayed through the engine" (dependency-graph.json:1447).
  - **The SM-LOAD types.** They live in IF41-SAVE, whose producer file is `SaveMachine.fs`. That is not a T19 dependency, because the generator writes contract declarations into producers' files at bootstrap (`STARTING-STATE.md`:49).
- **What would be needed.** A read-only connect/load task depending on T18, T13 and T15, which T20's write half then extends. The DAG has no such task: T20 bundles "read, create, write, conflict" (execution-dag.json:315 title).
- **Blocked regardless of ordering.** EXT-DATA-013 cannot pass until DRC §6 C1 (the event union cannot represent the catalog events), C3 (no workspace in `Repository`) and S1 (no list operation) are resolved.

**Case: WI-GEN-* waiting on T17.**
- **What T17 delivers.** "a typed command crosses Limen, returns events or a typed fault, and every dependent view refreshes from one projection version (ADRP-004)" (dependency-graph.json T17 `completion`). Its reasons are a real decide (T13), the frozen contracts (T14) and the shell (T16) (`task-adjustments.json`:60-68).
- **The rule behind the edge.** "The generated `WI-GEN-*` items … depend on T17 only" (`PROCESS-CORRECTIONS.md`:162). The owner's rule "a generated item never precedes the kernel, whose purpose is correctness" was explicitly **not** relaxed (`rehearsal-010-optimization/OWNER-DECISIONS-REQUIRED.md`:16).
- **For M1:**
  - **Genuine.** EXT-DATA-002 needs the shell (T16). EXT-DATA-013/UI-001 need the kernel (T13) for replay.
  - **Artificial in data/code-flow terms.** The integration checkpoint's command round trip does nothing for Connect: Connect issues no domain command, and the frozen `Msg.Connection` (06-IF41-APP.fs:60) is separate from `Msg.DomainCommand` (:65).
  - **Conservative call.** Keep T13 and T16 and keep the owner's kernel-first rule. The T17 edge itself is a governance rule, not a data dependency.
- **Slice coupling.** SL-WI-GEN-EXT-DATA is bound to J1-03, through EXT-DATA-008. It therefore cannot merge until the start form of WI-INV-START works (slice:2294 governedAssertions; bindings.json:13). This **journey coupling ties M1 to M3** under the current slices.

### 5. Concurrency: see §7 (streams A, B, C).

---

## M2: list and open historical investigations read-only

### 1. Requirements

| ID | Why | Evidence |
|---|---|---|
| INV-039 | *text*: the list region of the connected repository's investigations | catalog INV-039; scope:WI-INV-LIST 13717 |
| INV-040 | *text*: "Open investigation". `dependsOn` INV-039 | catalog INV-040 |
| INV-041 | *text*: primary navigation on every screen | catalog INV-041 |
| INV-042 | J2-08 (applicable part): the investigation sections nav | catalog INV-042 |
| INV-008 | J1-06: the original problem statement shown on opening | bindings.json:16; scope:10190 |
| UI-029 | J2-08 | bindings.json:31; scope:8297 |
| UI-020 | **dependsOn** of UI-029 | scope:8297 |
| UI-025 | *text*: "Opening an investigation MUST lead to an Overview" | baseline:1269 |
| UI-014 | *text*: O3 "Navigation MUST distinguish frozen demonstrations from writable copies" | baseline:728 |
| EXT-DATA-011 | *text*: reading against the frozen storage contract, failing closed | baseline:2830 |
| PERSIST-005 | *text*: "An investigation is opened by reading it from GitHub at a branch head and verifying it". It is SM-LOAD T-L01's and T-L03's source | baseline:16959; state-machine-contracts.json:3286, :3357 |
| PERSIST-017 | *text*: SM-LOAD's offline transitions T-L02, T-L05 and T-L06 cite it | state-machine-contracts.json (SM-LOAD :3251-) |
| UI-010 | *text*: SM-LOAD T-L04 (LoadFailed) cites it. O4: invalid histories fail closed | baseline:572; state-machine-contracts.json:3378 |

- **Journeys:** J1-06 and J2-08, only where applicable.
  - J1-06 (bindings.json:16) reopens the investigation the journey itself **created and saved** (`j1-create-persist.spec.mjs`:31-39). Its INV-017 and INV-037 are save-dependent, so they go to M4. INV-006 is a start-screen field (WI-INV-START; support.mjs:113), so it goes to M3. What remains for M2 is list → open → identity and content display: UI-001 (already M1) and INV-008.
  - J2-08 (bindings.json:31) needs UI-029, UI-026 and UI-003. Only the section navigation (UI-029, INV-042) is read-only infrastructure. UI-026 (causal analysis, with WDL-001..015 dependencies) and UI-003 (timeline) are the analysis views the owner lists under M5, so they are assigned there.
- **Forward dependencies, not pulled:**
  - INV-008 → INV-001 (M3). For historical cases the problem statement already exists as `InvestigationStarted.original` (CONTRACT.md:461-469), so the dependency is on the data, not on the start form.
  - UI-010 → PERSIST-020 (M4). In a read-only session there is no unsaved work to describe.

### 2. Work items and slices

| Requirement | Work item / slice |
|---|---|
| INV-039, INV-040, INV-041 | WI-INV-LIST / SL-WI-INV-LIST (scope:13717; slice:3044; 16 obligations; no governed assertion) |
| INV-008, INV-042 | WI-INV-WORKSPACE / SL-WI-INV-WORKSPACE (scope:13755; slice:3216; 170 obligations) |
| UI-025, UI-010 | WI-GEN-UI / SL-WI-GEN-UI |
| UI-014, UI-020, UI-029 | T16 / SL-T16 (scope:13134) |
| EXT-DATA-011 | T15 / SL-T15 (scope:13119) |
| PERSIST-005, PERSIST-017 | T19 / SL-T19 (scope:13198; 115 obligations) |

The SM-LOAD transition tests belong to T20 (`task-adjustments.json`:133-134), whose slice has 0 obligations.

### 3. DAG chain
`T10 → {T11 → T16, T12, T14} → T13 → {T15, T17}`; `T10 → T18 → T19` (because PERSIST-005 and PERSIST-017 sit in T19). WI-INV-LIST, WI-INV-WORKSPACE and WI-GEN-UI wait on T17.

### 4. Genuine versus artificial
- **Genuine:**
  - T16, the shell and routes (dependency-graph.json:1362).
  - T13 and T15, which verification and replay of the catalog need (DRC §4.2, §6 C2).
  - T17, for the views: "views consume the verified projection path" (dependency-graph.json:1422) is a real code-flow dependency for anything rendering `InvestigationProjection`.
- **Artificial:**
  - **PERSIST-005 and PERSIST-017 in T19.** These are read-side rules implemented by SM-LOAD (`LoadMachine.fs`, persistence) and `GitHubRequests.fs`. Being in T19 makes the read path wait for T19's 60-minute, 115-obligation write-machine slice (execution-dag.json:272), with no data or code flow from SM-SAVE beyond `Initial` (see M1 §4).
- **Gaps blocking M2 regardless of ordering:**
  - No listing operation in the frozen contracts (DRC §6 S1).
  - No workspace in the connection contract (C3).
  - No contract representation of a per-investigation read-only state (DRC §5). UI-014-O3 and ADRP-004-O6 require one.

---

## M3: create an investigation

### 1. Requirements

| ID | Why | Evidence |
|---|---|---|
| INV-001, INV-002, INV-004, INV-023, EXT-DATA-008 | named; J1-03 | bindings.json:13 |
| INV-019, INV-020, INV-021 | *text*: validation of the named fields. Each `dependsOn` INV-001 or INV-002 (OD-INDY-21) | scope:10834, :10878, :10922 |
| INV-022 | *text*: "Cancel investigation start: no investigation is created and nothing is written". This is the explicit-action half of EXT-DATA-006-O2 | catalog INV-022 |
| INV-005 | *text*: the start screen's "Persistence destination indicator" (C4 makes this non-trivial) | catalog INV-005 |
| INV-006 | *text*: the start screen's investigation identifier. The journey reads it (support.mjs:113), and J1-06 checks the URL contains it (`j1-create-persist.spec.mjs`:37) | catalog:153 |
| INV-003 | *text*: on the start screen, applicable only when `domain-profile-selected=true` (scope applicability) | catalog INV-003 |
| INV-007, INV-018 | *text*: creation timestamp, and investigation state after Start. INV-018 `dependsOn` INV-004 | catalog INV-007, INV-018 |

None of the named five has a scope `dependsOn` (scope:9743, :9821, :9941, :11035, :1732). Journey: J1-03.

### 2. Work items and slices
- WI-INV-START / SL-WI-INV-START (scope:13732; slice:3101; 43 obligations; J1-03, J1-06).
- EXT-DATA-008 is in WI-GEN-EXT-DATA.
- INV-007 and INV-018 are in WI-INV-WORKSPACE.

### 3. DAG chain
`T10 → T11 → T16`; `T10 → T12, T14 → T13`; `T13, T14, T16 → T17` → WI-INV-START, WI-GEN-EXT-DATA, WI-INV-WORKSPACE.

### 4. Genuine versus artificial
- **Genuine:**
  - T13: `StartInvestigation` is decided by the kernel (00-DOMAIN-STUBS.fs:60, `Decide` 01-IF41-DOMAIN.fs:16). EXT-DATA-008 requires the file set to be "command-created … replayable".
  - T17: the start action is a domain command through Limen (`Msg.DomainCommand`, 06-IF41-APP.fs:65). That is exactly T17's checkpoint.
- **Genuine but undeclared.** EXT-DATA-008-O2 "round-trip verified" needs a write and a read-back, which is M4's save path. **The DAG does not declare this edge**, because WI-GEN-EXT-DATA depends only on T17 (scope:13570).
- **Gap.** The frozen contracts have no creation path for a session or a first file set (DRC §6 S2). The content gap for INV-023 (no wire field for the question; DRC §6 C5) blocks J1-03, J1-05 and J1-06 as written.

---

## M4: edit and persist

### 1. Requirements

**Named:** INV-015, INV-016, INV-037, PERSIST-007, PERSIST-021, PERSIST-030, PERSIST-031.

**Added through `dependsOn`:**

| ID | Dependency of | Evidence |
|---|---|---|
| UI-016 | PERSIST-030 (Forma semantic tokens) | scope:3919 |
| PERSIST-011, PERSIST-012, PERSIST-015 | PERSIST-031 | scope:4042 |
| PERSIST-010 | PERSIST-015 (`dependsOn` PERSIST-010) | scope:3013 |

**Added from journeys:**

| ID | Journey | Evidence |
|---|---|---|
| INV-017 | J1-06 | bindings.json:16 |
| PERSIST-032 | J3-04 | bindings.json:44 |
| PERSIST-011 | J3-05 | bindings.json:45 |
| PERSIST-015 | J3-07 | bindings.json:47 |

**Added from requirement text:**

| ID | Basis in the requirement text |
|---|---|
| PERSIST-006 | PERSIST-007-O1 confirms "that mutation", which PERSIST-006 defines |
| PERSIST-008 | PERSIST-031's Conflict and Save failed entry conditions distinguish a stale head |
| PERSIST-009 | PERSIST-031-O8: Saving includes "an automatic rebase" |
| PERSIST-013 | Conflict resolution must not overwrite (PERSIST-012-O7) |
| PERSIST-014 | PERSIST-015-O5 relies on "this save's marker" |
| PERSIST-016 | PERSIST-031-O19: "after the automatic reads are spent" |
| PERSIST-022 | One indicator per investigation (PERSIST-030-O1) |
| PERSIST-023 | INV-016 and PERSIST-007 rely on the structural `Confirmation` (05-IF41-SAVE.fs:8-24) |
| PERSIST-024 | PERSIST-031-O21: "names the cause" |
| PERSIST-033 | The workspace header half of the indicator |
| UI-006 | Source of INV-016 (catalog) |
| INV-043 | `dependsOn` INV-016 |
| PERSIST-002, PERSIST-003, PERSIST-004, PERSIST-018, PERSIST-019, PERSIST-020 | The session-memory model that PERSIST-031's Unsaved and Offline wording describes: "the changes exist only in this tab" (O6, O12) |
| PERSIST-025 | PERSIST-007-O4's PAT tests run against the stateful fake |
| PERSIST-041 (PAT-02), PERSIST-049 (PAT-10), PERSIST-051 (PAT-12) | Named as the acceptance of PERSIST-007-O4 and PERSIST-021-O4 (baseline:17046, :17582) |

- **Not added:** PERSIST-054 (PAT-15). PERSIST-030-O8 and PERSIST-031-O24 name it, but it is a T31 release-candidate requirement (scope T31), and adding it would pull in the whole release chain (T30, T23, T26, T27, T28, T29).
- **Journeys:** J1-04, J1-05, J1-06, J3-03, J3-04, J3-05, J3-06 and J3-07.
- **Forward dependencies, not pulled:**
  - INV-037 `dependsOn` INV-012, INV-024, INV-026, INV-027, INV-028, INV-030, INV-031 and INV-033 (scope:12443), all M5 content.
  - INV-017 `dependsOn` INV-014 (M5).
  - **So M4 can satisfy INV-037 and INV-017 only for the creation fields.** That is what J1-05 and J1-06 check: `providerHolds([v.problem, v.title, v.question])` (`j1-create-persist.spec.mjs`:30, :36). Full INV-037 acceptance (J2-09) belongs to M5.
- **Dependencies outside M4:** PERSIST-003 → UI-012 (outside the scope manifest); PERSIST-025 → PF-020 and UI-015 (M6).

### 2. Work items and slices

| Work item | Requirements | Slice evidence |
|---|---|---|
| T19 | PERSIST-002..007, PERSIST-009..016, PERSIST-018..022, PERSIST-024, UI-006 | SL-T19 (slice:727; 115 obligations; J1-04, J1-05, J3-05, J3-07) |
| T22 | PERSIST-030..033 | SL-T22 (slice:1263; 40 obligations; J1-04, J3-03..07) |
| T18 | PERSIST-008, PERSIST-025 | |
| T12 | PERSIST-023, PERSIST-051 | |
| T21 | PERSIST-041, PERSIST-049 | |
| T16 | UI-016 | |
| WI-INV-WORKSPACE | INV-015, INV-016, INV-017, INV-037, INV-043 | |

### 3. DAG chain
`T10 → T18 → T19 (T12, T14) → T20 → T21 (T17, T13)`; `T17 → T22`. The save UI (INV-015/016 in WI-INV-WORKSPACE) waits on T17.

### 4. Genuine versus artificial
**All of the following are kept:**
- T12 → T19: invariant tests before the first write code, R4-01 and AR-022 (`task-adjustments.json`:87).
- T18 → T19: the fake built from live recordings, PERSIST-025 and R7L-10 (dependency-graph.json:1392).
- T14 → T19: implements IF41-SAVE (:1387).
- T19 → T20 for the **write** half: create, write and conflict really drive the save process.
- T20 → T21 and T17 → T21: the live write is "issued as a legal command through the browser path" (:1412, :1417).
- T17 → T22.

These are real persistence and correctness dependencies.

### 5. Concurrency: see §7 (stream E).

---

## M5: analysis

### 1. Requirements
- **Content:** INV-009..014 and INV-024..033. These are observations, hypotheses, evidence, links, kinds and conclusions.
- **Editing:** INV-034 (direct `dependsOn` of INV-035), INV-035, INV-036, INV-038 and INV-044.
- **Views:** UI-003 (timeline, J2-08), UI-005 and UI-033 (J2-05) and UI-026 (causal analysis, J2-08).
- **Graph dependencies:** WDL-001..012, WDL-014 and WDL-015. These are the direct `dependsOn` of UI-026 (scope:8222).
- **Journeys:** J2-01..J2-09 (bindings.json:24-32). J2-10 is P1, and its UI-009 is not in the scope manifest.

**Not pulled:**
- WDL-011's direct dependencies PF-001..PF-021 (scope WDL-011). They are fixture and provider-verification requirements, not analysis content, and include the live checkpoints PF-013, PF-014 and PF-020. Taking them would move WI-GEN-PF and T21/T31 work into M5. They stay in M6.
- WDL-013, PF-010, PF-021 and OD-INDY-23 are not scope IDs.

### 2. Work items and slices
- WI-INV-WORKSPACE: all the INV IDs.
- WI-GEN-UI: UI-003, UI-005, UI-026 and UI-033.
- T24: WDL-001 and WDL-002.
- WI-GEN-WDL: the other WDL IDs.
- T23 (the causal graph, owner ui-2, `Views/Graph.fs`) carries no scope requirements (scope T23), but it is the graph implementation.

### 3. DAG chain
`T10 → … → T13 → T17` → WI-INV-WORKSPACE and WI-GEN-UI; `T11 → T24 → T23 (and T17)`; WI-GEN-WDL waits on T17.

### 4. Genuine versus artificial
- **Genuine:**
  - T13: every analysis command is decided by the kernel.
  - T17 → T22/T23: "views consume the verified projection path" and "graph edges come only from projected relationships" (dependency-graph.json:1422, :1432).
  - T24 → T23: WDL-001 discovery.
  - Persistence for J2-09, via M4.
- **Artificial.** None that can be shown from data or code flow, because all of this content is created through the kernel and rendered from projections.
- **Blocking regardless of ordering.** DRC §6 C5:
  - no wire field for observation source (J2-01) or evidence label (J2-03);
  - no wire value for `inconclusive` or `consistent-with` (J2-06);
  - no relationship reassessment or withdrawal event, and no edit events (J2-06, J2-09; INV-034, INV-035).

---

## M6: everything else

Every scope requirement not listed above: 91 IDs.
- AR-*, the live PATs (PERSIST-040, -042..048, -050, -052..055), PF-*, the remaining UI-*, VER-*, EXT-DATA-009 and EXT-DATA-010, PERSIST-001 and PERSIST-026.
- `milestones.json` omits M6, as the task permits.

---

## 5. Summary of the two named cases

| Case | Edge | Stated reason | Verdict |
|---|---|---|---|
| Read-only connect via T20 → T19 | T19 → T20 | "the connect flow drives the session-memory save process (ADR-R7-001)" (dependency-graph.json:1402) | **Artificial for connect, list and read-only load.** These use Connector, SM-CONNECTION, SM-LOAD, Verify and Replay. Their only SM-SAVE touch is the total `SaveMachine.Initial` (05-IF41-SAVE.fs:211; T-L03 state-machine-contracts.json:3357). **Genuine for create, write and conflict.** |
| | T18 → T20 | "built and tested against the recorded provider behaviour" (:1397) | Genuine (error classification) |
| | T15 → T25, and T13 via Verify | "catalog records are v1.0.0 file sets replayed through the engine" (:1447) | Genuine |
| Every WI-GEN-*/WI-INV-* item on T17 | WI-* → T17 | Generated items depend on T17 only (PROCESS-CORRECTIONS.md:162). The owner keeps "a generated item never precedes the kernel" (rehearsal-010-optimization/OWNER-DECISIONS-REQUIRED.md:16) | **Kernel (T13) and shell (T16): genuine** for any item that renders projections or issues commands. **The T17 checkpoint itself is a governance edge.** Not a data or code-flow dependency for WI-GEN-RECORDS (review records), WI-GEN-VER (VER-010 traceability) or the Connect half of WI-GEN-EXT-DATA (`Msg.Connection` is not `Msg.DomainCommand`, 06-IF41-APP.fs:60, :65). Changing it is the owner's call (V7, not proposed). |

---

## 7. Concurrent work streams: contracts and files by owner

File owners are from `requirements-compiled/v4.1.4/agent-file-ownership.json` (line of each `"path"` in brackets).
- "A file has exactly one primary owner; contributors may edit only through a pull request the owner approves, never concurrently" (`rules[0]`).
- "Integration alone writes" project, solution and shared files, `View.fs` and `Program.fs` (`rules[4]`).
- Compile order within `Causa.Persistence` is Storage, Canonical, Reader, Wire, GitHubRequests, Classify, SaveMachine, Reconcile, Conflict, LoadMachine, Session (`compileLists`).

| Stream | Frozen contract coded against | Files touched (owner) | Genuine prerequisites |
|---|---|---|---|
| **A. Connection** | IF41-WIRE (`Repository`, `ConnectionContext`, `Credential`, `Execute`; 03-IF41-WIRE.fs:5-59); IF41-ERRORS `Connector`, `ConnectResult` (04:28-36); IF41-APP SM-CONNECTION (06:10-25) | persistence: `Wire.fs` [343], `GitHubRequests.fs` [352], `Classify.fs` [358]. ui: `Connection.fs` [420], `Engine.fs` [405], `Update.fs` [444]. integration: `tests/Causa.Acceptance/Live/EXTDATA00{1..10,12}Tests.fs` | T10 (declarations at bootstrap), T18 (recorded answer forms), T16 (shell, for the UI half) |
| **B. Data contract / deserialization** | IF41-STORAGE `Verify`, `PathOf` (02:28-31); consumes IF41-DOMAIN `Replay` (01:15-19) and the frozen event union (00:92-125) | persistence: `Storage.fs` [322], `Canonical.fs` [331], `Reader.fs` [337]. domain: `Events.fs` [278], `Evolve.fs` [296], `Kernel.fs` [308], `tests/Causa.Acceptance/Live/EXTDATA011Tests.fs` (domain). persistence: `tests/Causa.Contract.Tests/Storage/*` | T13 for replay; T15 fixture replay. **Blocked by DRC §6 C1** (no union cases for the catalog's events) |
| **C. List and read-only views** | IF41-SAVE SM-LOAD (`LoadState`, `LoadInput`, `InterpretLoad`, `StepLoad`; 05:118-146, :223-224) and `Initial` (:211); IF41-VIEW (07-IF41-VIEW.fs); IF41-DOMAIN `Project` | persistence: `LoadMachine.fs` [388], `SaveMachine.fs` [367] (holds the IF41-SAVE declarations), `GitHubRequests.fs`, `Session.fs` [394]. ui: `Projections.fs` [426], `Views/Dashboard.fs` [468], `Views/Investigation.fs` [474], `Routes.fs` [414], `Views/Shell.fs` [450], `Update.fs`, `Engine.fs`. persistence: `tests/Causa.Acceptance/Live/EXTDATA013Tests.fs`, `tests/Causa.Persistence.Tests/StateMachines/SMLOAD.fs` | Streams A and B; T16. **No list operation exists (S1)** |
| **D. Creation domain logic** | IF41-DOMAIN `Decide` (01:16), `CoreCommand.StartInvestigation` (00:60); IF41-STORAGE `Encode` (02:30) | domain: `Commands.fs` [272], `Decide.fs` [290], `Events.fs`, `Entities.fs` [266]. persistence: `Storage.fs` (Encode). ui: the start screen in `Views/Dashboard.fs` or `Views/Investigation.fs` (no start-screen file exists in the ownership list), `Update.fs` | T13. **S2:** no creation path in `SaveMachine.Initial`/`Encode` |
| **E. Persistence (save)** | IF41-SAVE `Step`, `Interpret`, `ReconcileDue`, `StepConflict`, `Confirmation` (05:13-224); IF41-ERRORS `Classifier` | persistence: `SaveMachine.fs`, `Reconcile.fs` [376], `Conflict.fs` [382], `Session.fs`, `GitHubRequests.fs`, `Classify.fs`, `tests/Causa.TestSupport/StatefulFake.fs` [679], `Recordings.fs` [673], `tests/Causa.Persistence.Tests/StateMachines/SMSAVE.fs`, `SMCONFLICT.fs`. ui: `Views/SaveIndicator.fs` [456], `Views/ConflictReview.fs`, `Projections.fs` (`SaveIndicator`, `Header`) | T12, T14, T18 (all genuine) |
| **F. Analysis** | IF41-DOMAIN `Decide`, `Evolve`, `Project` (content payloads opaque, 00:11-13); IF41-VIEW; IF41-REPORT | domain: `Commands.fs`, `Decide.fs`, `Evolve.fs`, `Entities.fs`, `Capabilities.fs`. ui: `Views/Investigation.fs`, `Projections.fs`, `Update.fs`. ui-2: `Views/Graph.fs` [480], `Views/History.fs` [500] | T13, T17, T24 (graph). **Blocked in part by C5** |

**Where two agents would edit the same file.** These are same-owner collisions; ownership forbids concurrent edits by different owners.
- **ui:**
  - `Update.fs` and `Engine.fs`: streams A, C, D, E (UI wiring) and F all add `Msg` handling there, since one `Update: Msg -> Model -> …` is frozen (06-IF41-APP.fs:77-79).
  - `Views/Investigation.fs`: streams C, D (possibly), E (indicator placement) and F.
  - `Projections.fs`: C, E and F.
  - ODR-R10-20 proposes splitting `Views/Investigation.fs` for exactly this reason (rehearsal-010-optimization/OWNER-DECISIONS-REQUIRED.md:8). It is not adopted.
- **persistence:**
  - `GitHubRequests.fs`: A (connect request), C (head, file and tree reads) and E (`createCommitOnBranch`, reconciliation reads).
  - `Classify.fs`: A, C and E.
  - `SaveMachine.fs`: C (the SM-LOAD declarations live there) and E.
  - `Storage.fs`: B (Verify) and D (Encode).
  - `Session.fs`: C and E.
- **domain:**
  - `Events.fs` and `Evolve.fs`: B (the wire mapping must match the union) and F.
  - `Commands.fs` and `Decide.fs`: D and F.
- **No cross-owner file overlap.**
  - Persistence streams A/B/C/E never edit ui files; they meet only through the frozen IF41-SAVE, IF41-WIRE and IF41-ERRORS types.
  - Domain streams B/D/F meet persistence only through IF41-DOMAIN and IF41-STORAGE.
  - Integration's EXT-DATA test files are its own. But WI-GEN-EXT-DATA (owner integration, scope:13570) implements behaviour whose code files are ui- and persistence-owned. Its implementation therefore goes through those owners' pull requests (`rules[0]`).

**What can therefore run concurrently, by owner lane, after T10 and T14:**
- **persistence:** A (persistence half) and E in parallel, one file-set each, except that `GitHubRequests.fs` and `Classify.fs` are shared. C's `LoadMachine.fs` can proceed alongside E.
- **domain:** B's event union/Evolve and D's Commands/Decide are separate files. They converge in `Kernel.fs`.
- **ui:** each stream's view file can proceed separately. `Update.fs`/`Engine.fs` serialize all UI streams.
- **ui-2:** F's graph and history views (`Graph.fs`, `History.fs`) are independent of the ui lane.
