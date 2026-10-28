<!-- Investigation note written 2026-10-28 by a read-only measurement agent for the Echelon development speed optimization initiative. Machine-readable twin: r5-final-metrics.json. -->

# Rehearsal 5: final baseline metrics

- Repository: `kemiller2002/indy-init-rehearsal-005`. Local read-only clone: `/home/user/kemiller2002/indy-init-rehearsal-005`. `origin/main` was fetched with `--depth=1000` at `b972cb4`. All 285 commits are reachable and the root `52e9a74` is present, so the history is complete.
- Window: start `2026-10-27T20:08:38Z`, final verdict `2026-10-28T02:41:07Z`, deadline `03:08:38Z`, 392 elapsed minutes. The final run was `40620099844` and the candidate was `1b9fb56`.
- Read-only. Nothing in the rehearsal repo was modified, pushed, triggered or re-run.

## CI API limitation (affects items 1–4, 6-CI and 7)

The GitHub Actions API was **not reachable** from this session:

- `mcp__github__actions_list` returned "Access denied: repository … is not configured for this session".
- `api.github.com` through the session proxy returned 403 ("GitHub access to this repository is not enabled for this session").
- The `gh` CLI token is invalid.
- `add_repo(access:"read")` grants git reads only.
- `add_repo(access:"push")`, which would have attached API credentials, was **denied by the auto-mode permission classifier**.

As a result, no run census, job listing, `run_attempt` or `head_sha` data could be collected. Where the repository itself records CI facts, those facts are reported as **partial** with their source. Nothing below is estimated. To complete items 1–4, run this again in a session where the repo is attached for API access, or let the user grant `add_repo` for it.

## Summary

| # | Metric | Value | Status |
|---|---|---|---|
| 1 | CI runs by workflow/conclusion | 17 distinct run IDs are quoted in the repo. 5 are stated as failed (all Build and test). 2 are stated as fully green (40610187284, 40620099844). | **UNAVAILABLE** (census); partial |
| 2 | CI execution minutes | Final run only: 14 jobs, **1,467 job-s (24.45 job-min)**. Wall clock from first job start to verdict: 1,031 s (17.2 min). The `browser` job accounts for 705 s (48%). | **UNAVAILABLE** (window); partial |
| 3 | Repeated builds (`run_attempt` > 1; same head_sha) | The final run is attempt 1. Nothing else is recorded. | **UNAVAILABLE** |
| 4 | Failed browser-test runs | **≥ 5** (40586028131, 40599949953, 40613071150, 40613496969, 40615430960). All 5 come from the test harness (WebKit load wait ×1, hardcoded test-site port ×4). | Lower bound only |
| 5 | Merges in window | **92 merges** / 279 commits (187 non-merge) | MEASURED |
| 5 | Conflicted merges (recomputed) | **17** (18.5% of merges) | MEASURED |
| 5 | Merge direction | 28 into main (23 `into HEAD` + 5 `into main`), 58 into streams, 6 other | MEASURED |
| 5 | `Praxis: reconcile` commits | **26** (30 `Praxis:` in total) | MEASURED |
| 5 | Reverts / explicit integration-repair commits | **0** / **2** (`805b7a9`, `b6238ad`) | MEASURED |
| 6 | Acceptance-evidence commits (acceptance/, evidence/, tools/acceptance/) | **35** in window. First at 20:13:38Z (T+5.0), last at 02:12:36Z (T+364.0). Span 359.0 min. | MEASURED |
| 6 | CI minutes in acceptance/verdict/traceability jobs | The acceptance job is not recorded in layer-timings. No job is named verdict or traceability. | **UNAVAILABLE** |
| 7 | Time to first working application | First CI-confirmed deployed-smoke success falls between **T+38.7** and **T+287.4 min**. The final candidate's deployed-smoke completed at T+391.0. | **UNAVAILABLE** (exact); bounded |
| 8 | Agent tokens / tool calls | Every value is `supported-unavailable` or `unknown` in all 8 telemetry files | **UNAVAILABLE** |

## Method and raw data

### 1. CI runs (partial)
Method: I searched commit bodies, `acceptance/reviews/*.md`, `evidence/` and `.ros/events/events.jsonl` for 11-digit run IDs, and recorded a conclusion only where the text states one. The 17 IDs found include persistence-recorder runs from the since-removed `persistence-provider.yml`: 40578948822, 40579165469, 40582348883, 40584769828 and 40585189886.

| Run | Stated outcome | Source |
|---|---|---|
| 40586028131 | failed: WebKit boot `page.goto` timeout | `4a6aeff` body |
| 40599949953 | failed: browser specs ERR_CONNECTION_REFUSED (port 4322) | `2e7599e` body |
| 40613071150, 40613496969 | failed: 23 ux tests ERR_CONNECTION_REFUSED (4322) | `ff12cbf` body |
| 40615430960 | failure (ux.spec port); live job passed | R5-final-review.md |
| 40598776427 | provider-conformance "green on main" at `5d8888f` | `910d49b` |
| 40610187284 | success, all 16 jobs including deploy + deployed-smoke | R5-independent-review.md |
| 40620099844 | success, every job (final) | evidence/acceptance/index.json |

### 2. CI minutes: final run 40620099844 only
Source: `evidence/acceptance/run-40620099844-1b9fb56/gate-records/layer-timings.json`. The acceptance job writes this file from the Actions jobs API, and the file excludes the acceptance job itself.

| Job | s | Job | s |
|---|---|---|---|
| browser | 705 | lock-gate | 65 |
| site | 128 | core | 62 |
| percepta | 126 | provider-conformance | 58 |
| live | 105 | deployed-smoke | 49 |
| wasm-smoke | 65 | preservation-adversarial | 33 |
| reuse | 28 | preservation-gate | 26 |
| deploy | 14 | build-and-test | 3 |

Total: 1,467 s. The first job started at 02:23:56Z and the last recorded job ended at 02:39:37Z (941 s). The verdict came at 02:41:07Z. The critical path is reuse → site → browser (705 s) → build-and-test → deploy → deployed-smoke → acceptance.

### 5. Merges
Source: `git log origin/main`, filtered to committer dates inside the window (the author-date filter gives the same 279 commits). To find conflicts, I ran `git merge-tree --write-tree --name-only <p1> <p2>` on each of the 92 two-parent merges (there are no octopus merges); exit code 1 means the merge conflicted. Of the 17 conflicted merges, 15 mention the resolution in their message. `9927c91` and `85c9d68` do not.

Conflicted merges and their files:
`7abed91` build-and-test.yml · `826b01a` App.slnx · `85c9d68`, `6b1239e` Adapter.fs+EnginePort.fs · `80a0476` Adapter/EnginePort/View.fs · `9927c91` Shell.fs+causa.css · `3858417` View.fs · `d23060f` fixture-mode.spec.js · `5968ba5` View.fs · `70f58be`, `dff3a4f` fixture-mode+graph-navigation specs · `2ccb548` Adapter/EngineView/TestState.fs, index.html, GraphTests.fs · `e980ee4` causa.css + 2 specs · `4c8f204` bindings.json, Causa.App.fsproj, Shell.fs, View.fs · `5cd0f42` EngineView.fs · `c42523b` .percepta/bindings.json · `a3eead3` live-persistence.spec.js.

Conflict hotspots:

| File | Conflicted merges |
|---|---|
| Adapter.fs | 4 |
| View.fs | 4 |
| fixture-mode.spec.js | 4 |
| EnginePort.fs | 3 |
| graph-navigation.spec.js | 3 |

Merge targets: r5/persist 14, r5/core 14, r5/shell 12, r5/ux 11, r5/acc 7, HEAD 23, main 5, other 6.

Integration repairs: I searched subjects and bodies for revert, merge, conflict, resolved, restore and follow, and counted only commits that a merge message names as its follow-up. `805b7a9` restores the persistence projects to App.slnx after `826b01a`. `b6238ad` is the port fix after `d23060f` ("port fix follows"). There are no reverts. Hits on "conflict" from the save-conflict feature are excluded.

### 6. Acceptance evidence
Method: `git log --no-merges origin/main -- acceptance/ evidence/ tools/acceptance/`, filtered to the window.

| Path | Commits | Last commit |
|---|---|---|
| acceptance/ | 16 | 02:12:36Z |
| evidence/ | 3 | 20:36:45Z |
| tools/acceptance/ | 27 | 01:33:39Z |
| Combined (distinct) | 35 | 02:12:36Z |

One more commit, `14bee79` at 06:16:27Z, falls after the window. It copies the final verdict evidence into the repo.

### 7. First working application
`deployed-smoke` (in build-and-test.yml) runs after `deploy`. Deploy runs only on a push to main when all of build-and-test is green. There is no start-smoke job. The bounds come from these points:

| Point | Time | T+ | Source |
|---|---|---|---|
| Lower bound: the job first reached main | 20:47:22Z | 38.7 | merge `7abed91` |
| Earliest documented deploy | 00:47Z | 278.4 | run 40606127778 deployed e086147; its deployed-smoke result is not stated |
| Upper bound: earliest CI-confirmed deployed-smoke success | 00:56Z | 287.4 | run 40610187284 (R5-independent-review.md) |
| Final candidate's deployed-smoke | 02:39:37Z | 391.0 | final run |

### 8. Tokens and tool calls
Source: `.ros/telemetry/executions/*.json`, 8 files covering WI-0002 to WI-0009. In each file, the capability statuses are 14 `supported-unavailable`, 104 `unknown` and 20 `derived`.

- `tokens.input`, `tokens.output`, the cache metrics, `tool.calls`, `tool.failures`, `tool.duration_ms` and `time.model_ms` are all `supported-unavailable`: "no observation has been ingested".
- `rawTelemetry` is `[]` in every file, and `identity.model` is null.
- The only recorded metrics are git-derived.
- 7 of the 8 executions are still `active` (never finalized). Only WI-0002 (20:14:10Z–20:20:45Z) has wall, blocked and change metrics.
