# Playwright CI mitigation: final report

**Status: MERGED.** #150 merged into main as e6e7a3c after the workflow owner's independent review; its measured result is "Result" (run 42138265759 at 6579f5a). *Updated 2026-11-06:* the rows below that describe main and run states are as they stood when this report was written, and are corrected where they are now stale.

**Scope.** Indy-init's `preflight-tooling.yml` and `live-provider-preflight.yml`. The competition repository's gate, `tools/r9/verification-gate.yml`, is covered under "Remaining risks". It is governed and pinned, so it is not changed here.

## 1. Current state, measured

| Item | Value |
|---|---|
| main (when written) | 307ce17 (#148). Its own Preflight run 42133849069: the `r10-adversarial` aggregate was stuck in "Playwright, Chromium and WebKit" for more than 35 minutes, the failure this report removes. That run later finished as `cancelled`. Main now carries #150 (e6e7a3c). |
| Runner | `ubuntu-latest` = Ubuntu 26.04 (runner image ubuntu26/20261101.186), x64. `--with-deps` pulls around 100 apt packages from `azure.archive.ubuntu.com`. |
| Playwright | `@playwright/test@1.63.0` and `playwright@1.63.0`, in every job |
| Container image | `mcr.microsoft.com/playwright:v1.63.0-noble@sha256:eff16c30…` (Ubuntu 24.04; published by Microsoft on mcr.microsoft.com), **pinned by digest** in the workflow. Digest as pulled `sha256:eff16c30e6f3f4af0a03fa4b706120d5e9b0891c344a27d64559aff5900a4a27` (run 42134469740). |
| Jobs that install browsers (main) | <ul><li>`fsharp-tooling`, the Forma layout assertions on Chromium and WebKit: **no time limit** (job default 360 min).</li><li>`suite-commands`, the fixture trees: job limit 20 min.</li><li>`r10-adversarial-cases` (3 shards) and `r10-adversarial`: 45 min each.</li><li>`live-provider-preflight`, owner-dispatched: job limit 30 min.</li></ul> |
| Chromium and WebKit required | all of the above (journeys J1–J3, the layout contract, and the suite runners `--project=chromium` / `webkit`) |

| PR | Head (tested) | What it does | CI evidence |
|---|---|---|---|
| #149 (lead) | 3a1ed1f | `timeout -k 15 600` and one retry. Before the retry: `pkill apt-get/dpkg`, `dpkg --configure -a`, then the archive.ubuntu.com mirror. | Run 42133710486: shard 1's install took **664 s** (11 min) and succeeded on the first attempt, so the recovery path **never ran**. The aggregate failed with `DTD_AUDIT_REMOVED`, because the branch predates #148; that is not a Playwright failure. |
| #150 (session …epi3) | ef82102, then 8d58246 | The adversarial shards and aggregate run in the Playwright image. Runner installs were at first wrapped in `timeout 480` ×3, then restored to plain in 8d58246. | Run 42134469740 (ef82102), detailed below. Every adversarial job passed. `fsharp-tooling` failed: `timeout` killed `npx` but not its `apt-get`, which kept the dpkg lock, so both retries failed instantly (`Could not get lock … held by process 10217 (apt-get)`). 8d58246 removed the wrapper. #149's kill-and-retry (3a1ed1f) depends on the same kill, so this run is the evidence against it too. |
| #151 (owner) | 5d77c09 | Pins `ubuntu-24.04` for every job, `actions/cache` for `~/.cache/ms-playwright`, a launch probe, and a `--with-deps` fallback with retry | Run 42133033781: the aggregate failed. The cached-browser launch failed (the cache holds no OS libraries), then the fallback install exited with apt code 100 twice (`browser install failed twice`). |
| #143 (for reference) | 0585338 | — | Run 42122295340: shard 1 stalled for the whole 45 minutes in `--with-deps`, twice. The log ends mid-download from azure.archive.ubuntu.com. |

## 2. Measurements (GitHub Actions; not estimates)

| Path | Browser setup | Notes |
|---|---|---|
| Runner `--with-deps` (main, #149, #143) | 40 s to 664 s when it completes; **45 min stall** three times on 2026-11-05, and once in progress on main now | depends on the Azure apt mirror |
| Container (#150, run 42134469740) | <ul><li>image pull 20–29 s</li><li>tools step 6 s (once 139 s)</li><li>browser step 3–9 s, with nothing downloaded</li></ul> | the image is from mcr.microsoft.com; only the small tools step uses apt |
| Adversarial shards, container (#150) | shard 0: 6 min 25 s; shard 1: 6 min 00 s; shard 2: 3 min 42 s | each reported `12 case(s): 12 validated, 0 not run, 0 failed` |
| Aggregate, container (#150) | 7 min 41 s | <ul><li>journeys J1–J3: **Chromium 3 passed, WebKit 3 passed**</li><li>delegated-authority suite 79/79</li><li>the union check (every case exactly once) passed</li></ul> |
| Cache (#151) | — | no successful warm run; its cold run failed |

There is no warm-versus-cold distinction for the container: every run pulls the same pinned image (20–29 s). The cache approach has no measured warm success.

## 3. Comparison

| Criterion | A: #149 bounded retry | B: #150 container | C: #151 cache | D: hybrid (selected) |
|---|---|---|---|---|
| Cold start | 40–664 s, stall-prone | about 30–45 s | failed | B for adversarial, runner elsewhere |
| Warm start | same as cold | same | unmeasured | same as B |
| External mirror | Azure, then archive | mcr.microsoft.com, plus a small apt step | Azure (fallback) | mcr for adversarial |
| Chromium and WebKit run | yes | **yes, measured** | not reached | yes |
| F#, .NET and harness compatible | yes | **yes, measured**: setup-dotnet, dotnet fsi and git in the container | — | yes |
| Reproducibility | mirror-dependent | **image pinned by digest** | key-pinned, but OS libraries still from apt | B |
| Failure recovery | kill and retry, **unverified, and unsafe for dpkg** | fails at image pull or tools step, named | the fallback failed | step limits, fail early, named |
| Maintenance | low | low (the tag must equal Playwright's version) | medium (cache key, OS pin, probe) | low |
| Hidden test omissions | none | none: same cases, union check, 0 not run | none | none |
| Protected workflow and evidence | workflow edit | workflow edit | workflow edit, plus an OS change to **every** job | workflow edit only |

## 4. Selected: PR #150, plus a step time limit on the runner installs

- **Adversarial jobs** (3 shards and the aggregate) run in `mcr.microsoft.com/playwright:v1.63.0-noble`. This is #150 unchanged: no browser download and no browser apt install.
- **Runner installs** (fsharp-tooling layout assertions, suite-commands fixtures, live-provider journeys) keep `npx playwright install --with-deps` with a step-level `timeout-minutes`: fsharp-tooling 25 (its install plus the layout run took 803 s on run 42138265759; the limit covers that whole step, the layout assertions included, not the install alone), suite-commands 15 (50 s measured) and live journeys 15 (unverified, owner-dispatched). A stall fails that step early and by name. The runner cancels the step, with no in-script kill, no retry and no skip. This replaces fsharp-tooling's current lack of any bound. **Trade-off, stated plainly:** on a very slow mirror, a run that today is slow but green (the slowest measured runner install was 664 s; the fsharp-tooling step, install plus layout run, took 803 s) could turn red at its limit. The limits leave that margin. A red step is then re-run once, as a job. This is safe where 8d58246's wrapper was not, because there is no second attempt inside the same job, so a cancelled apt leaves no dpkg lock behind for anything to trip on.
- **The container tools step** (git, jq, curl, ICU) is bounded to 5 minutes, with `::error::phase=container-tools`. Unlike 8d58246's wrapper, its `timeout -k 15 300` around apt is safe. Each job starts a fresh container, nothing inside it retries apt after a kill, and the step fails the job at once. So a killed apt can leave a dpkg lock only in a container that is about to be discarded. The step's own `timeout-minutes: 6` backs it up.

**Rejected:**
- **#149's kill-and-retry.** The recovery path never ran in CI. Killing apt mid-install risks a broken dpkg state, which #150's own run demonstrated with the lock. A single bounded attempt plus a job re-run is simpler and safe.
- **#151's cache.** The browser binaries alone don't launch without the OS libraries, and its fallback failed. Pinning every job to ubuntu-24.04 changes the environment of all evidence-producing jobs, which is out of proportion here.
- **Adding a cache on top of the container.** The pull is already 20–29 s, so no measured benefit.

## 5. Recovery policy

| Phase | Where | Detection | Outcome |
|---|---|---|---|
| Image acquisition | "Initialize containers" | the step fails (pull error) | job fails before any case; re-run the job once; if mcr is unavailable, see below |
| Container tools | tools step, 5-min `timeout` inside the step's `timeout-minutes: 6` | `::error::phase=container-tools` | job fails before any case |
| Browser install (runner jobs) | step `timeout-minutes` | the step is cancelled at its limit, by name | job fails; re-run once |
| Browser launch / tests | the test steps | test failures | real failures, never passes |

- **Budgets:** step limits are below their job limits (15 < 20 for suite-commands, 15 < 30 for live, 25 < 360 for fsharp-tooling), and there are no nested retries.
- **If mcr.microsoft.com is unavailable:** no validated alternative execution path exists. The adversarial jobs then fail at image pull with GitHub's own diagnostics, and no readiness is claimed. Reverting to the runner install is the fallback, and it is the known stall-prone path.

## 6. Remaining risks

1. **The competition gate, `tools/r9/verification-gate.yml`.** It runs on every competition PR and merge. It installs browsers twice on `ubuntu-latest`, with no retry:
   - its browser step (line 170) runs `npx playwright install --with-deps chromium webkit`, bounded to 10 minutes (`timeout-minutes: 10`);
   - its J1–J3 step runs a second `npx playwright install --with-deps chromium webkit` for the journeys (line 274). That one has no step limit, so it is bounded only by the job's 40 minutes (line 49). A stall there costs up to the whole gate budget before the run fails.

   The gate's exposure is therefore larger than one 10-minute install. It is governed (`r9.fsx govern` writes it; `check-governance` requires it to equal Indy-init's; it is pinned in `kickoff/rehearsal-010/observer-pins.json`), so it is not changed here.
   - **Recommendation:** run its `verify` job in the same image, validated in a rehearsal repository before T0. This needs the owner's go-ahead and a re-pin; see "Owner decisions".
2. **Live-provider preflight.** It is owner-dispatched, so its new step limit is **unverified** in CI.
3. **mcr.microsoft.com availability.** This is a single external dependency, as GitHub's runner images also are.

## 7. Owner decisions

- **Whether to apply the container to the competition gate** (`tools/r9/verification-gate.yml`, governed, pinned) before T0. That covers both of its installs, including the J1–J3 one that is bounded only by the 40-minute job.
- Branch protection / CODEOWNERS for workflows is unchanged (OWNER-BATCH item 8).

## Result

**PR #150 at 6579f5a, run [42138265759](https://github.com/kemiller2002/Indy-init/actions/runs/42138265759): every job succeeded.**

| Job | Result | Duration | Evidence |
|---|---|---|---|
| r10-adversarial-cases (0) | success | 8 min 29 s | container 26 s, tools 147 s, browsers 3 s; `12 case(s): 12 validated, 0 not run, 0 failed` |
| r10-adversarial-cases (1) | success | 7 min 15 s | container 20 s, tools 9 s, browsers 4 s; `12 validated, 0 not run, 0 failed` |
| r10-adversarial-cases (2) | success | 4 min 08 s | container 32 s, tools 9 s, browsers 4 s; `12 validated, 0 not run, 0 failed` |
| r10-adversarial (aggregate) | success | 5 min 37 s | <ul><li>J1–J3: **Chromium 3 ✓, WebKit 3 ✓**</li><li>positive control: 2 passed</li><li>delegated authority: 79/79</li><li>union check: passed</li></ul> |
| fsharp-tooling | success | 19 min 33 s | layout assertions on Chromium and WebKit, runner install: 803 s, inside its limit (now 25) |
| suite-commands | success | 2 min 21 s | fixture trees with the runner install: 50 s |
| offline-contract-tests, frozen-v4, r9-readiness-gates, release-gate-repair | success | — | unchanged jobs |

- **Image:** every container job pulled `sha256:eff16c30e6f3f4af0a03fa4b706120d5e9b0891c344a27d64559aff5900a4a27`, the pinned digest.
- **Earlier runs:** two more container runs of #150's adversarial jobs were also all green. Run 42134469740 (ef82102) had shards of 6:25, 6:00 and 3:42 and an aggregate of 7:41. Run 42136695891 (8d58246) had shards of 6:20, 7:23 and 3:48 and an aggregate of 5:25.
- **Head after the run:** a later commit raises fsharp-tooling's step limit from 15 to 25 (803 s measured) and records these results. Its own run is linked from the PR.
- **Merge and readiness evidence:** merged as e6e7a3c after the workflow owner's independent review, and the workflow is confirmed on main: the Preflight runs of #153, #155 and #157, all based on it, passed their adversarial and runner-install jobs. On #157, fsharp-tooling and the aggregate were first cancelled mid-run with no failing step, and passed on one re-run. #143 has merged. Re-running #140 against the accepted workflow is still open.
