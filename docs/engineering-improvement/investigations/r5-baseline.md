<!-- Investigation note written 2026-10-28 by a read-only investigation agent for the Echelon Engineering Improvement Initiative (Phase One). Local paths refer to that session's clones. Numbers marked as recomputed in ../BASELINE-2026-10-28.md were re-derived independently. -->

# Rehearsal 5 (indy-init-rehearsal-005): engineering evidence baseline

- Repository: `kemiller2002/indy-init-rehearsal-005`, local clone `/home/user/w/indy-init-rehearsal-005` (full history; `git fetch --unshallow` reported "complete repository").
- Observation cutoff: `main` at `e086147bc93142c1d97917f2f2fd3a3d08f66dfc` (2026-10-28T00:02:07Z). Everything below is as of that commit unless stated otherwise. Stream branches were fetched read-only (`git fetch origin <b>:refs/remotes/origin/<b>`). All their tips are after the cutoff, so only commits with a committer date at or before the cutoff are counted.
- Method: read-only. Nothing was edited, committed, pushed or commented, and no agent was contacted.
- **CI limitation:** the GitHub Actions API was **not reachable** from this session. `mcp__github__actions_list` returned "Access denied: repository ... is not configured for this session". `gh run list` returned HTTP 403. Anonymous `api.github.com` returned 403. A request to attach the repo with API credentials (`add_repo access:push`) was denied by the permission classifier. As a result, every CI number below comes from run IDs and failures quoted **inside the repository**. A full listing of runs, conclusions and logs is **not available**.

Classification legend: **[DEFECT]** confirmed defect · **[INEFF]** confirmed inefficiency · **[ARCH?]** suspected architectural weakness.

---

## 1. Rehearsal start and baseline identity

| Item | Value | Reference |
|---|---|---|
| First commit (repo) | `52e9a74c870804eb72936439fbf79c651ef348c1` "Initial commit", Kevin Miller, 2026-10-27T14:50:02Z (10:50:02-04:00) | `git rev-list --max-parents=0 e086147` |
| Preflight T07 (derivation) | `2a15e6b` 20:03:41Z (branch `preflight/t07-derivation`) | |
| Preflight T08 (reviews applied, preservation gate passes) | `6878bd7` 20:04:56Z | |
| Conditor bootstrap | `cf86744` 20:07:33Z, "conditor init --preset indy-init (Conditor 0.10.14, echelon-current 1.29.0)" | |
| Clock start record | `7334318` 20:13:38Z adds `evidence/rehearsal/clock.json` | |
| **Clock start (T+0)** | `startUtc 2026-10-27T20:08:38Z`; basis: "first green check on bootstrap commit cf86744 (Build and test, green 20:08:38Z); Echelon foundations green 20:09:02Z; Praxis validation failed on the bootstrap range for missing work-item attribution ... reconciled post hoc" | `evidence/rehearsal/clock.json` |
| Deadline / ceiling | `deadlineUtc 2026-10-28T03:08:38Z`, `ceilingMinutes 420` | same |
| Frozen preflight | "kemiller2002/indy-init kickoff/rehearsal-005/preflight-record.json @ b942ac4; reviews @ 459f65a" | same |
| Frozen dependencies | `.conditor/lock.json` sha256 `0f35e596…b751` (echelon-current 1.29.0, echelon-registry 402bec21, Conditor 0.10.14) | same |
| Requirements baseline | `BL-CAUSA-INDY-INIT-aab8f80c7743`, Indy-init commit `aab8f80c7743392e794b3b631542903ed4ed6f64`, baseline sha256 `621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9` | `research/requirements-preservation/derivation-record.json` |
| Normalized requirements sha256 | `9fea70e7…61da` | same |
| Equivalence reviews | Indy-init `0bc45d400ac049adf0946ebb6690a4dad5a75de7:kickoff/rehearsal-005/equivalence-reviews.json`, sha256 `7dba9c65…f80a`; the WI-0002 re-run used Indy-init `459f65ac1aa5569d05c0e7a4096ada277dbabc91` (byte-identical inputs) | `evidence/requirements-preservation/gate-pre-implementation-command.txt` |
| Derived package | 461 requirements (108 P0), 2,293 obligations (1,449 verbatim, 775 review, 69 tokens), 0 exclusions, 775 reviews applied; gate verdict pass (Praxis 3.22.0) | `derivation-record.json`, `gate-pre-implementation-command.txt` |
| Kickoff manifest | `kickoff/indy-init.kickoff.json` (planningBaseline `kemiller2002/Indy-init`, refPolicy `pin-exact-commit-at-kickoff`, 8 required components) | |
| docs/FACTORY-REHEARSAL.md | A generic protocol document from the bootstrap. It contains no R5-specific record. | |

## 2. Commit history, up to the cutoff

```
C=e086147bc93142c1d97917f2f2fd3a3d08f66dfc
git rev-list --count $C                         # 193
git rev-list --merges --count $C                # 55
git rev-list --no-merges --count $C             # 138
git log --format=%s $C | grep -c '^Praxis: reconcile'   # 19
git log --format=%s $C | grep -c '^Praxis:'             # 22 (19 reconcile + checkpoint/work-item commits)
git log --format=%an $C | sort | uniq -c        # 192 Claude, 1 Kevin Miller
git log --format=%B $C | grep -oE 'Claude-Session: \S+' | sort | uniq -c
                                                # 144 x session_01G1iECdHZXLR6xBfMivMsx3 (one session id only)
```

| Measure | Value |
|---|---|
| Total commits | **193** |
| Merge commits | **55** (28.5%) |
| Non-merge commits | 138 |
| "Praxis: reconcile…" commits | **19**. With checkpoint and work-item bookkeeping, 22 "Praxis:" commits (11.4% of all commits) |
| Merge commits whose message records a conflict resolution ("resolved to", "keep X's") | **5**: `826b01a` (App.slnx "resolved to main's side"), `7abed91` (build-and-test.yml "resolved to r5/acc's version"), `80a0476` ("keep shell's placement…"), `3858417` ("keep the per-area projection split"), `d23060f` ("fixture-mode.spec.js resolved to r5/persist's version") |
| Merges that actually conflicted (recomputed) | **7**, found with `git merge-tree --write-tree --name-only p1 p2` (exit 1) for every two-parent merge: `7abed91` (.github/workflows/build-and-test.yml), `826b01a` (App.slnx), `85c9d68` and `6b1239e` (src/app/Adapter.fs, src/app/EnginePort.fs), `80a0476` (Adapter.fs, EnginePort.fs, View.fs), `3858417` (src/app/View.fs), `d23060f` (tests/browser/fixture-mode.spec.js). The messages of `85c9d68` and `6b1239e` do not mention their conflicts. |
| Merge direction (from the message) | 34 sync merges from main into a stream (`into r5/persist` 11, `into r5/shell` 9, `into r5/core` 7, `into r5/acc` 7); 15 `… into HEAD` (coordinator integration on main); 3 `into main`; 3 others (`5471d21` preflight, `3858417`, `6b1239e`) |
| First commit to cutoff | 14:50:02Z → 00:02:07Z = **9 h 12 m 05 s** (most of it is pre-rehearsal idle time) |
| Clock start (T+0, 20:08:38Z) to cutoff | **3 h 53 m 29 s** (233.5 of 420 ceiling minutes, 55.6%) |
| First R5 commit (`2a15e6b`, 20:03:41Z) to cutoff | 3 h 58 m 26 s |

### Per-stream counts

Branch first-parent ancestry cannot attribute commits. Stream agents pushed merge commits that became main's history ("Merge … into HEAD", with the stream commit as first parent), and the streams cross-merged each other (`r5/ux` ⇄ `r5/core` ⇄ `r5/shell`). The authoritative attribution is Praxis/ROS `work.attribution.reconciled` events (`.ros/events/events.jsonl`, `gitEvidence.commits[].sha`). Script: parse each event's `gitEvidence.commits[].sha` and intersect with `git rev-list --no-merges e086147`.

| Stream (work item) | Non-merge commits in main attributed by Praxis |
|---|---|
| acc (WI-0006) | 32 |
| persist (WI-0005) | 29 (**+8** persist commits on main that are **unattributed** at cutoff: `440f99d 6e38f54 819ab4f 825148e 8bd0963 ac3c50c b5d68de d5a5bc2`) |
| shell (WI-0004) | 25 |
| core (WI-0003) | 16 |
| ux (WI-0007, created 21:50:56Z) | 0 merged to main by the cutoff |
| preflight (WI-0001) / bootstrap (ROS-INSTALL-3-22-0) | 3 / 1 |
| Unattributed | 32 = 22 Praxis bookkeeping + `7730f78` (WI-0002 itself) + `52e9a74` + the 8 persist commits |

- 106 distinct commits are attributed, none of them twice, and all 37 reconcile events carry `"attribution":"post-hoc"`.
- At the cutoff, **28 commits (13 non-merge, 15 merges)** with committer date ≤ cutoff were on `r5/core`, `r5/shell` and `r5/ux` but **not on main**. Of the 13 non-merge commits, 3 are `ux:` work (`46dd600`, `3d96cf0`, `f74b052`) and the rest are core/shell screen work (S03–S24 bindings). `r5/acc` and `r5/persist` were fully merged. Command: `git log --format='%H %cI %P' origin/r5/{core,shell,ux,acc,persist} ^e086147 | awk '$2<="2026-10-28T00:02:07+00:00"'`.

- **[INEFF]** 22 of 193 commits (11.4%) are Praxis bookkeeping. Every stream commit was attributed after the fact by the coordinator ("agent commits under the coordinator's work item per the R5 stream rule"), and 8 persist commits were still unattributed at the cutoff.
- **[INEFF]** 55 merges for 138 non-merge commits, and 34 of them are main→stream syncs.
- **[ARCH?]** 7 conflicted merges. 5 of them are concentrated in three shared shell files (`src/app/Adapter.fs`, `src/app/EnginePort.fs`, `src/app/View.fs`) that several streams had to edit.

## 3. CI

**Not available from the API** (see the access note at the top). Below is what the repository itself records.

Workflows at the cutoff: `.github/workflows/build-and-test.yml` (15 jobs: reuse, core, lock-gate, site, browser, live, wasm-smoke, preservation-gate, preservation-adversarial, percepta, provider-conformance, build-and-test, deploy, deployed-smoke, acceptance; triggers: `pull_request` and `push` to `main` and `r5/**`), `echelon-foundations.yml`, `praxis-validation.yml`. A separate `persistence-provider.yml` existed until `910d49b` removed it. `build-and-test.yml` changed in 17 commits; workflow files changed in 30 commits (`git log --format=%h e086147 -- .github/workflows | wc -l`).

Run IDs cited in the repo:

| Run | Outcome as recorded | Where |
|---|---|---|
| (bootstrap cf86744) | Build and test green 20:08:38Z; Echelon foundations green 20:09:02Z; **Praxis validation failed** (missing work-item attribution) | `evidence/rehearsal/clock.json` |
| 40578948822 | provider recorder, **live PASS** (first verified remote write) | `815242f`, `tests/Persistence.Tests/recordings/run-40578948822.ndjson` |
| 40579006057 | acceptance job, first computed verdict INCOMPLETE / NOT ACCEPTED at `8d15492` | `evidence/acceptance/index.json` |
| 40579165469, 40582348883, 40584769828, 40585189886 | provider recordings | `tests/Persistence.Tests/recordings/` |
| 40586028131 | main run **failed**: WebKit `page.goto` timed out in the shell boot test | `4a6aeff` |
| 40598776427 | provider-conformance **green on main** (`5d8888f`) | `910d49b` |
| 40599949953 | main run **failed**: graph-navigation and fixture-mode specs `ERR_CONNECTION_REFUSED` (hardcoded port 4322) | `2e7599e` |

Other failures recorded in commit messages (no run ID given): the acceptance tool's own curation test failed at `d08fed1` (fixed `689fc1c`), the graph test fixture broke on the new Proposals field (fixed `8020602`), and the Praxis validation failure on the bootstrap range.

Counts of successes, failures and cancellations, distinct failure causes from logs, and reruns of the same SHA: **not available**.

## 4. Specific incidents

### (a) WebAssembly start failure → per-area projection. [DEFECT] plus [ARCH?]

- `bb89d44` (22:52:10Z, shell) "WIP security: test-only export (do not merge yet)" is the first commit that mentions the limit. It adds `NoInlining` helpers with the comment "the .NET WebAssembly interpreter refuses a method whose locals exceed its limit ("locals size too big")". `src/app/View.fs` grew from 623 to 644 lines in it.
- `a0e7d4a` (23:00:10Z) "Fix the start failure: the projection is one function per area…". From the message: "View.project exceeded the .NET WebAssembly interpreter's per-method limit ("locals size too big") and the page did not start. The projection is now assembled from nine top-level functions (nav, screens, list, investigation, history, report, AI, administration, forms)". It adds `tests/browser/start-smoke.spec.js`, which fails on BridgeError, StartFailed, "locals size too big" or CAUSA.INTERNAL. View.fs: 884 lines, 615+/357−.
- `3858417` (23:03:05Z): merge conflict in View.fs, resolved as "keep the per-area projection split".
- Exact error text: only the fragment **"locals size too big"** is recorded (`src/app/View.fs:12`, `:883`; `tests/browser/start-smoke.spec.js:2,13`; `a0e7d4a` message). No full stack or console message is in the repo, evidence/ or docs. CI logs are not available.
- The commit that first made `View.project` exceed the limit cannot be identified from git alone. View.fs line counts on the predecessors: `1e8757a` 618, `6b1239e` 621, `3399016` / `2c367a5` / `0a97e28` 623. a0e7d4a also says the limit is hit "once its optimizer passes are off", which points to the separate test-build publish added in `bb89d44`.
- **[DEFECT]** `bb89d44`, explicitly marked "do not merge yet", is an ancestor of main (`git merge-base --is-ancestor bb89d44 e086147` succeeds), as is `e02d000` "WIP T27 … tests not yet run".
- **[ARCH?]** A single projection method that every stream extends. Post-cutoff work on `r5/ux` (`f74b052`) says "ux keys merged outside View.project", so the pressure continued.

### (b) Test-site port mismatch and the static server crash. [DEFECT] plus [ARCH?]

- Cause chain: `a0e7d4a` moved the local test servers to 4821/4822 because "other worktrees' servers on 4321/4322 answered with stale builds". Two specs still hardcoded 4322: `tests/browser/graph-navigation.spec.js` (core, `da6f6b0`) and `tests/browser/fixture-mode.spec.js` (persist, `6e38f54`).
- Main run **40599949953** failed them with `ERR_CONNECTION_REFUSED` (`2e7599e`).
- `2e7599e` (23:28:05Z) had three parts:
  - removed the hardcoded ports;
  - changed `scripts/serve.mjs` to answer an undecodable path with 400 instead of crashing. Before this, a `decodeURIComponent` throw took the server down, "or every later test on this port fails with connection refused";
  - made `live-graph.spec.js` stop waiting for the repository name that the shell had removed from the connection line.
- Then `d23060f` (23:28:12Z) merged r5/persist and resolved `fixture-mode.spec.js` "to r5/persist's version", which **reintroduced 4322**. `b6238ad` (23:28:18Z) changed the default to 4822, 6 seconds later.
- A related case of cross-stream text drift: `8bd0963` (persist's @live spec waits for the shell's new connection words).
- **[ARCH?]** The streams ran as concurrent worktrees on one host and shared fixed ports. The test-site port was a convention, not a single source of truth.

### (c) Tests associated with obligations they did not fully prove. [DEFECT]

- `5dcb91e` (21:15:04Z, core) "Engine tests claim only what they fully prove". Engine tests had been tagged with VPO ids for obligations that also need app, browser, provider or live evidence: UI-003, UI-004, UI-006, UI-013-O3, UI-023, UI-038, PF-002, PF-004-O1/O3, PF-007-O1, PF-009, EXT-DATA-004-O1, -005-O2, -006-O2, -008-O1, -013-O2/O3, AR-014-O2. These were re-tagged `engineEvidenceFor`, so the binder no longer counts them as proven. The commit touched 5 test files and `docs/architecture/interface-manifest.json`.
- AR-002-O2 / Praxis finding: `99eb90e` (00:00:16Z) adds `causa-acceptance derivation-check`. It records that `praxis requirements preservation verify` 3.22.0 **passes value and role swaps**: the UI-016-O11 WCAG ratios swapped, and the ADRP-001-O1 source precedence swapped. The gap is kept as characterized failing tests ``FINDING Praxis 3.22.0 passes a value swap…`` and ``…role swap…`` (`tools/acceptance/Causa.Acceptance.RunTests/PreservationTests.fs:85-95`) and documented in `acceptance/README.md` §"Derivation check (AR-002-O2) and the Praxis finding". `preservation-adversarial` is not a required job (`build-and-test.yml:672-674`). This is a confirmed defect in the Praxis gate, not in the candidate.
- No matches anywhere in the repo for "does not prove"/"overstat" tied to these tests, apart from generic docs.

### (d) Forma typography. [DEFECT]

- `f207cd4` (23:47:06Z, shell): Forma's `.ef-site` maps h1/h2 to fluid marketing display sizes ("up to 7.25rem at 14ch"), which made "Connect a data repository" huge on desktop.
- The fix sets `--ef-type-size-h1/h2/h3` to Forma's 2xl/xl/lg steps in `src/kernel/causa.css` (+13 lines). `tests/browser/identity.spec.js` (+29) asserts h1 = 2xl at 1440/1180/820/320 px, fits on one line, and checks two-level navigation.
- This is a stack default that the app had not overridden, found about 3.6 h after T+0.

### (e) Shared compiled test application across CI jobs. [INEFF], fixed

- Before: `browser`, `live` and `percepta` each ran `npm run build:test`. `c54ea92` (20:56:40Z) had already made the test build reuse dist/'s WebAssembly output.
- `979c465` (23:04:17Z): the `site` job uploads the `site-test` artifact (its own .NET publish with the test-only exports), and the three jobs download it instead of rebuilding (`.github/workflows/build-and-test.yml`, +30/−8).
- Required by `a0e7d4a`'s split of test and production builds. Saved minutes are **not available** without CI timing.

## 5. Milestones (relative to T+0 = 20:08:38Z)

| Milestone | Evidence | Time |
|---|---|---|
| First green build | bootstrap `cf86744`, Build and test green 20:08:38Z | T+0 (by definition) |
| First successful live persistence (provider-level write) | run 40578948822, recording `recordedAt 20:32:49.651Z`, `live.create` "first verified remote write in 1586 ms" (GraphQL `createCommitOnBranch` 200), plus append, conflict refusal, rejected token and reconcile checks passing; committed in `815242f` 20:33:47Z | **T+24 m 11 s** (recording) |
| First application write path through the real app (@live browser) | `3c27e6a` 21:23:59Z adds DCT-LIVE-A; its CI pass is **not verifiable** | commit at T+1 h 15 m 21 s |
| First app in browser (claimed) | `d0dcc3d` 20:37:40Z "S02 bound and passing verify-app"; `29c3cf0` 20:55:15Z "legal commands end to end in the browser" (local claim) | T+29 m / T+46 m 37 s (claims, not CI-verified) |
| App start restored after the start failure | `a0e7d4a` 23:00:10Z | T+2 h 51 m 32 s (when the failure began: not determinable) |
| Provider conformance green on main | run 40598776427 at `5d8888f` (22:58:16Z commit) | ≈T+2 h 50 m |
| First green browser suite / first fully green main | **not available** (no CI access) | |
| Independent acceptance at cutoff | The only committed verdict is run 40579006057 at `8d15492` (20:32Z): ledger INCOMPLETE, plan INCOMPLETE, **NOT ACCEPTED**, obligations bound 3 of 326, P0 1/108. `acceptance/traceability.json` is stale (last changed `8d15492`). Structurally, the verdict cannot be ACCEPTED, because plan `VP-e77a2134e1b0` binds no tests and curation belongs to the requirements owner (`acceptance/README.md` §"Plan curation"). Later verdicts exist only in CI artifacts: **not available**. | **[ARCH?]** |

## 6. Token and tool-use telemetry

What is recorded:
- `.ros/telemetry/executions/*.json` holds 6 execution records (WI-0002…WI-0007).
- `.ros/executions/<id>/{envelope.json,events.jsonl}`.
- `.ros/events/events.jsonl` holds 509 events: 463 `artifact.contributed`, 37 `work.attribution.reconciled`, 6 `work.started`, 2 `work.completed`, 1 `work.checkpointed`.
- `telemetry/metrics.json` is the metric catalog.

What each record contains:
- Every record lists `tokens.input`, `tokens.output`, `tokens.cache_read`, `tokens.cache_write`, `context.window_size`, `context.utilization`, `cost.session_cumulative`, `time.active_ms`, `time.model_ms`, `model.requests`, `model.request_failures`, `tool.calls`, `tool.failures` and `tool.duration_ms` as **`supported-unavailable`** ("no observation has been ingested for this execution").
- About 100 more metrics are `unknown`, and `rawTelemetry` is `[]`.
- **No token, cost, model-request or tool-call numbers exist in the repo.**
- Only WI-0002 (finalized, 20:14:10–20:20:45Z) has derived git metrics: `time.wall_ms` 395,605; commits_created 1; files +338/−338, 123 renamed; lines +21,266/−12,916.
- WI-0003…WI-0007 are `active`, `finalizedAt null`, with only `git.baseline_dirty_files 0`. Their model is recorded as `unknown`; WI-0002's is `claude-opus-5-5`.

**[ARCH?]** All 6 executions share one ROS `sessionId` (`cf565065-a168-5486-8bfa-956f257becfe`), and all 144 session-tagged commits carry one `Claude-Session` (`session_01G1iECdHZXLR6xBfMivMsx3`). Per-stream agent identity and cost cannot be separated from the recorded telemetry. Tokens and tool use per stream are **not available**.

## 7. Workstream structure

- Streams are defined as ROS/Praxis work items in `.ros/work/queue.json` / `queue.md` (created by `78a4361` 20:44:10Z and `21fbf5c` 21:50:56Z):
  - WI-0003 core: F# kernel, invariants, interface freeze, fixture replay (T12–T15);
  - WI-0004 shell: Limen host, WASM shell, UI, Percepta (T16, T17, T22–T24, T26, T29);
  - WI-0005 persist: provider conformance, Sync, GitHub provider, catalog (T18–T21, T25);
  - WI-0006 acc: stack lock gate and acceptance pipeline (T11);
  - WI-0007 ux: UI-001, UI-017…UI-040.
- Each stream has a branch `r5/<stream>`. The coordinator merges to main and reconciles attribution.
- Interface ownership lives in `docs/architecture/BOUNDARIES.md` (boundaries A–E, owner and consumers, "Stable APIs (decision of the coordinator, R5)"; breaking changes need a coordinator decision and a manifest version). The manifest `docs/architecture/interface-manifest.json` is extracted from the compiled kernel and checked byte for byte by `tests/App.Engine.Tests/InterfaceManifestTests.fs`.
- Role-level ownership comes from the derived requirements: `research/requirements/RQ-CAUSA-2026-0318--rehearsal-agent-interface-freeze-ownership.md` (domain, UI, persistence, integration owner, coordinator) and `RQ-CAUSA-2026-0185--kickoff-execution-packet-4-parallel-ownership-after-kernel-s.md` (Owns / May not).
- **File-level** ownership is visible only as a commit-message convention for cross-stream edits of shell files:
  - `257e0d8` "Shell.fs: wire PersistencePort (coordinator-approved, stream persist)";
  - `9af1125` shell.spec.js (persist);
  - `ca848e9` EnginePort/Adapter (persist);
  - `819ab4f` "Shell files: fixture mode wiring (coordinator-approved, stream persist)";
  - `04e03f2` "Shell files: … (coordinator-approved, stream core)";
  - after the cutoff on r5 branches: `499d952`, `0310a60`, `fc60807`.
- **[ARCH?]** No machine-checked file-ownership contract (CODEOWNERS, path globs) exists in the repo. The rule appears to live in the coordinator's instructions outside the repository. The shared shell files are where the conflicts concentrated (§2).

## Findings summary

| # | Finding | Class | Refs |
|---|---|---|---|
| 1 | WASM start failure ("locals size too big" in View.project); app did not start | DEFECT | bb89d44, a0e7d4a, 3858417 |
| 2 | WIP commits ("do not merge yet", "tests not yet run") reached main | DEFECT | bb89d44, e02d000 |
| 3 | Hardcoded test-site port 4322 after the move to 4822; main run 40599949953 failed; reintroduced by a merge resolution | DEFECT | a0e7d4a, da6f6b0, 6e38f54, 2e7599e, d23060f, b6238ad |
| 4 | Static test server crashed on undecodable paths | DEFECT | 2e7599e (scripts/serve.mjs) |
| 5 | Engine tests tagged as proving app/browser/provider/live obligations | DEFECT | 5dcb91e |
| 6 | Praxis 3.22.0 preservation gate passes value and role swaps (AR-002-O2) | DEFECT (in tool) | 99eb90e, PreservationTests.fs:85-95 |
| 7 | Forma heading scale (h1 up to 7.25rem) | DEFECT | f207cd4 |
| 8 | WebKit boot test timeout (run 40586028131); acceptance-tool bugs | DEFECT | 4a6aeff; 689fc1c, f66a3e7, e42d1c0, 8005d65, fb65515, 8020602 |
| 9 | 22 Praxis bookkeeping commits, all attribution post-hoc, 8 commits unattributed | INEFF | .ros/events/events.jsonl |
| 10 | 55 merges, 34 sync merges, 7 conflicted merges | INEFF | merge-tree recomputation |
| 11 | Test build rebuilt per CI job until 979c465 | INEFF (fixed) | c54ea92, 979c465 |
| 12 | Single growing projection method shared by all streams | ARCH? | View.fs, f74b052 (post-cutoff) |
| 13 | Concurrent worktrees share host ports and serve stale builds | ARCH? | a0e7d4a message |
| 14 | File ownership is a commit-message convention only; conflicts concentrate in shell files | ARCH? | §7 |
| 15 | Acceptance cannot reach ACCEPTED (plan binds no tests); committed evidence stale | ARCH? | acceptance/README.md, evidence/acceptance/index.json |
| 16 | Telemetry has no token, cost or tool data; one session id across all streams; stream executions never finalized | ARCH? | .ros/telemetry/executions/ |
| 17 | CI runs on every push to main and r5/** with 15 jobs (volume not measurable here) | ARCH? (unverified) | build-and-test.yml `on:` |
