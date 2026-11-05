# Rehearsal 010: dependencies in front of Connect and Browse

**Sources:**
- `requirements-compiled/v4.1.4/execution-dag.json` (tasks, estimates, `dependsOn`);
- `requirements-compiled/v4/dependency-graph.json` (edge reasons);
- the successor slice manifest;
- MILESTONE-DEPENDENCIES.md §4–5;
- the R2 timeline (`results/R2-V4-milestones.json`, 10-min CI: start–integrated).

**No frozen edge is removed here.** The last column says what a successor or an additional milestone would need.

The six questions:
- **Fetch:** needed to fetch data?
- **Display:** needed to parse and display it?
- **Interpret:** needed only for authoritative interpretation?
- **Edit:** needed only for editing or persistence?
- **Gov:** a governance edge, not a technical one?
- **Move:** could it move after M0 without changing any existing acceptance condition?

| Task (est. min; R2 at 10-min CI) | Fetch | Display | Interpret only | Edit only | Gov | Move after M0? | Evidence |
|---|---|---|---|---|---|---|---|
| T10 bootstrap (29; 25–67) | yes | yes | – | – | – | **no**: the build, CI and checkpoints start here | DAG T10; STARTUP §2 |
| T11 stack lock (15; 67–95) | partly: pins Limen, Forma, SDK | partly | – | – | partly | M0 can carry its own pins (variant *after-t10*) | DAG T11 requirements AR-011, VER-004/006/008 |
| T14 interface freeze (20; 67–100) | no | no | yes: boundaries A–E for the kernel | – | yes | **yes**: M0 codes against the already-frozen IF41 types | DAG T14; contracts are frozen in the package |
| T12 invariant inventory (15; 87–115) | no | no | yes | – | – | yes | DAG T12 |
| T13 semantic kernel (45; 115–173) | no | **no** for raw display; yes for projections | **yes** | – | – | **yes** | Raw events display without `CoreEvent` (MINIMUM-DATA-BROWSER.md probe) |
| T15 fixture replay (20; 173–206) | no | no | **yes** | – | – | yes | DAG T15 "replay against the storage contract" |
| T16 shell, routes, visual identity (35; 95–146) | no | yes: a page to render into | – | – | – | M0 can bring a minimal shell (variant *after-t11*) or wait for T16 (variant *after-t16*) | DAG T16 |
| T17 checkpoint 1, "a legal command end to end in the browser" (20; 173–209) | no | no | yes: it demonstrates a *command* | partly | **yes**: "a generated item never precedes the kernel" | **yes for a read-only slice.** The rule's purpose (correctness of commands) does not apply to code that issues none | PROCESS-CORRECTIONS.md:162; MILESTONE-DEPENDENCIES.md §5 |
| T18 provider conformance (35; 67–118) | partly: the recorded 401/403/404 answer forms classify read errors | – | – | **mostly**: stale head, moved-head `FORBIDDEN`, second writer | – | M0 needs only the read answer forms; the write probes can follow | DAG T18 title; dependency-graph.json:1397 |
| T19 save-state machine (60; 118–191) | **no** | **no** | – | **yes** | – | **yes** | dependency-graph.json:1402: "the connect flow drives the session-memory save process (ADR-R7-001)". Connect and read touch only the total `SaveMachine.Initial` (05-IF41-SAVE.fs:211) |
| T20 Connect flow and provider: read, create, write, conflict (40; 191–244) | **yes** (read half) | – | – | the create, write and conflict half | – | the read half moves *before* T19 only in a successor that splits T20 (ODR-R10-21) | DAG T20 title |
| T25 load the historical catalog after Connect (20; 244–283) | yes | yes | **yes**: "catalog records are v1.0.0 file sets replayed through the engine" | – | – | **its acceptance is authoritative** and stays as is. M0's raw list is not T25 | dependency-graph.json:1447; EXT-DATA-013 |
| `WI-GEN-EXT-DATA` (Connect half) (228–270) | yes | – | – | EXT-DATA-008 only | **yes**: waits for T17 by the generated-item rule | yes, if the owner accepts a read-only exception to the rule | MILESTONE-DEPENDENCIES.md §5 (`Msg.Connection` is not `Msg.DomainCommand`, 06-IF41-APP.fs:60, :65) |
| `WI-GEN-UI-LANDING` / `-STATES` (209–296) | – | yes | UI-026/033 analysis cards | – | **yes**: after T17 | the landing list and error states, yes; the analysis cards, no | catalog UI-001, UI-010, UI-025 |
| T22 workspace views (70; 326–409) | no | no | yes | yes | – | yes | DAG T22 |

## Which dependencies are genuine for M0

Only **T10**, plus either **T11 and T16** or a minimal shell and pins carried by the M0 slice itself.

Each other edge in front of Connect and Browse is one of three kinds:
- authoritative interpretation: T12–T15, T25;
- editing and persistence: the T18 write probes, T19, the write half of T20;
- the governance rule that generated items wait for T17.

## Why they are in front of a data browser

The approved scope has no capability "show stored data without interpreting it". Every requirement that lists or opens a case is phrased authoritatively:
- **EXT-DATA-013:** the catalog is replayed;
- **UI-001-O1:** it shows the catalog cases;
- **T25:** "replayed through the engine".

These requirements were packaged into items whose other requirements genuinely need the kernel or the save machine. A data browser therefore inherits their dependencies. This is a packaging fact, not a technical one.

## What would change it (prepared, not adopted)

**ODR-R10-30** (OWNER-DECISIONS-REQUIRED.md) adds one planning slice, SL-M0 "Data visible (raw, not replayed)":
- it depends only on T10;
- it carries no existing requirement, so no acceptance condition changes meaning;
- it is verified by its own browser journey and error-state checks against the real repository.

Its gate needs a defined applicability for a slice with zero mandatory obligations, which is part of ODR-R10-30.

Everything else in the table stays as frozen. The authoritative milestones (EXT-DATA-013, T25, J1-06, J2-08) are still judged exactly as before.
