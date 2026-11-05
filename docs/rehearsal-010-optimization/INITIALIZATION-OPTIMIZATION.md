> **Provenance.** Written by an independent read-only research agent on 2026-11-05 against Indy-init main 76e50ce, and placed here unedited except for this note. Each figure is labelled *measured* (a cited CI run or file) or *estimated*. Its recommendations are proposals: what has been implemented is listed in FINAL-REPORT.md.

# Rehearsal 010 initialization overhead

Sources: Indy-init `main` 76e50ce (read only), Conditor `main` 92bd55e, the Rehearsal 009 clone (`/tmp/claude-0/review-r9`), and GitHub REST job timings. Labels used throughout:
- **M**: measured, with the source named.
- **E**: estimated, with the basis given.
- **U**: unknown (nothing measured, no basis for an estimate).

## 0. Evidence used

| Id | What | Value |
|---|---|---|
| M1 | `analysis/bootstrap-measurement.json`: git init, then `conditor init`, then the v41 bootstrap, Causa/App build, ci-policy, verify and commit (Conditor 0.10.18, warm NuGet) | 77 / 31.8 / 76.4 s, and 83 s in the reproduction. `conditor init` takes 45.8–48.4 s with a cold Conditor cache and 5.2 s with a warm one. The v41 bootstrap takes 0.8–1.3 s, the Causa.slnx build 17.8–22.7 s, App.slnx 6.1–8.5 s, ci-policy 0.13–0.19 s and verify 1.2–1.9 s |
| M2 | Rehearsal 009, the real event (`evidence/t0/T0.md`, git log, `.ros/work/queue.json`) | T0 00:30:01Z. Praxis `COND-MISSION-001` created 00:31:28Z (+87 s, after the `conditor init` retry for GH_TOKEN). Bootstrap commit 8001e7c at 00:32:11Z (**+2 min 10 s**). Workstation, spec-preflight and acceptance preflight were done **before T0**. R9 had no suite preflight, work plan, scope checkpoint or authorization, so its first app commit (edde38c, +21 min) is implementation time, not set-up. Its CI runs are not readable from this session (REST 403), so its first green CI is **U** |
| M3 | Indy-init "Preflight tooling": `r9.fsx install-conditor` + `workstation` (pinned Conditor 0.10.20, **R9 lock, echelon-current 1.36.0**) | 17–42 s over 18 jobs in runs 41963048303, 41961419895, 41960643456, 41959113588, 41957826956 and 41957812548. Run 41963048303 alone: 23, 27 and 21 s |
| M4 | Same runs: `npm install @playwright/test@1.63.0` alone | 2–5 s (n=6) |
| M5 | Same runs: npm install of Playwright and axe, `npx playwright install --with-deps chromium webkit`, Forma pack | 37–123 s (n=6; 101 s in 41963048303) |
| M6 | `suite-commands` "Fixture trees": suite-fixture ×2, npm install, browsers with deps | 48–123 s (n=6) |
| M7 | Run 41963048303 suite-commands: two `suite-preflight` runs on the fixture | 50 s. suite-preflight plus run-suites: 67 s |
| M8 | Run 41963048303: build of the requirements compiler | 11 s and 16 s |
| M9 | Run 41963048303 step 21: v4.1.4 bootstrap into an empty tree and onto the Conditor scaffold, three solution builds, cold CI NuGet | 34 s |
| M10 | Conditor CI runs 41949085452 and 41946329182, `echelon-current-1-37-0-kickoff-set-qualification`: `workstation plan`+`apply` from the published releases | **1.37.0: 12 s and 10 s.** Every set from 1.27 to 1.37: 8–14 s (n=20) |
| M11 | Conditor `indy-init-offline-freeze` (run 41949085452, an older set: praxis 3.6.0) | `bundle create`+`verify` 4 s. Offline `workstation apply`, with network fallback forbidden, 3 s |
| M12 | CI-only overhead | `setup-dotnet` 1–11 s and `setup-node` 0–6 s. These do not apply on a prepared host |

Limits of the evidence:
- No run has ever executed STARTUP-INSTRUCTIONS §3 end to end on a real skeleton.
- Indy-init CI has not yet installed the 1.37.0 workstation through `r9.fsx` (it uses the R9 lock); only Conditor CI has (M10).

## 1. Launch to first slice: ordered steps

T0 is the moment the builder records in `t0.json`, which goes in the bootstrap commit. R9 recorded T0 before `conditor init` (M2). "T0 is the builder's bootstrap commit" (LAUNCH-CHECKLIST) reads as "the commit that carries T0", not "the commit timestamp is T0". This should be stated (see owner approvals).

| # | Step (governing text) | Duration | Label |
|---|---|---|---|
| P1 | Owner side, no clock: `launch-preflight` LP-00..10, `r9.fsx spec-preflight`, `conditor acceptance preflight`, `scope preflight`, slice-manifest/work-plan reproduction (LAUNCH-CHECKLIST §C) | outside the window | — |
| P2 | Host tools: Conditor 0.10.20 and the echelon-current 1.37.0 workstation (praxis 3.25.0, dokimos 0.11.1, percepta-semantic 0.4.0, ordo 1.20.0) | 10–12 s apply (M10), 17–42 s via r9.fsx (M3). **Before T0** in R9 (M2), as STARTING-STATE allows | M |
| P3 | Agent start: read the §0 prompt, STARTUP-INSTRUCTIONS, AGENT-RULES duty 1 | 2–5 min | E: agent reading time; pre-T0 if T0 is recorded after it |
| 1 | Record T0; `git init -b main` / clone of the empty repository | 0.01 s | M1 |
| 2 | `conditor init --preset indy-init`: Echelon scaffold, **Praxis init**, Ordo/Percepta init, requirements import from GitHub | 45.8–48.4 s cold, 5.2 s warm cache | M1 (R9: ≤87 s including a retry, M2) |
| 3 | Build `requirements-compiler` from SRC (STARTUP calls it as a command) | 11–16 s | M8 |
| 4 | `requirements-compiler v41 … --bootstrap .` (**scaffolding**: 178 files) | 0.8–1.3 s | M1 |
| 5 | `dotnet restore/build Causa.slnx` (+App.slnx) (**dependency restore**) | 24–31 s warm NuGet (M1); 34 s for three builds on cold CI NuGet (M9) | M |
| 6 | `conditor ci-policy check`, `conditor verify` | 1.4–2.1 s | M1 |
| 7 | `r9.fsx govern --lock …` (STARTUP §2.1) | <1 min | E: dotnet fsi start + file writes; R9 ran it (M2), not timed |
| 8 | Write `evidence/t0/t0.json`; commit; `conditor repo create`; push | commit 0.11 s (M1); repo create/protect and push about 2 min | M / E (bootstrap-measurement `notMeasured`) |
| — | **Steps 1–8 as executed in R9** | **2 min 10 s** | **M2** |
| 9 | npm install of `@playwright/test` 1.63.0 and axe-core 4.14.0 | 2–5 s | M4 |
| 10 | **Browser install**: `npx playwright install --with-deps chromium webkit` | ≤37–123 s, including M4 and Forma | M5/M6 |
| 11 | **Dokimos setup**: `dokimos verification compile … --out .echelon/obligations.json` (STARTUP §3.1) | seconds to 1 min | U/E: deterministic compile of committed inputs |
| 12 | **Suite preflight**: `r9.fsx suite-preflight` (dotnet `--list-tests -m:1` for each target, which builds every test project; Playwright `--list`, chromium and webkit) | about 25 s per run on the fixture (M7). 1–3 min on the 6-test-project skeleton (E) | M/E |
| 13 | **Requirements compile** of the catalog: `requirements/verification/verification-manifest.json` (praxis `requirements manifest compile`), which authorize step 2 needs but **STARTUP never says to produce** | 1–5 min, including discovery | E (R9 produced it at +29 min, in an implementation commit) |
| 14 | Generate the work plan: `requirements-compiler scope work-plan` (STARTUP §3.2) | seconds | E |
| 15 | **Work-item registration**: 36 `praxis work begin/add`, then `scope preflight` | 1–2 min scripted; 4–8 min one agent turn per item | E |
| 16 | Commit the work plan and Praxis state | <1 min | E |
| 17 | **Scope checkpoint**: `scope checkpoint … --at NOW`; commit it as its own commit | about 1 min | E |
| 18 | `evidence/t0/spec-preflight.json` and `evidence/t0/acceptance.log`, which authorize needs and **STARTUP gives no command for** | 1–5 min, including discovery | E |
| 19 | **Authorization gate**: `r9.fsx authorize-implementation … --scope-sha256`; commit the initialization checkpoint | about 1 min | E |
| 20 | **Authorization gate**: `check-sequencing --scope-manifest` and `scope order` PASS; push | <1 min | E |
| 21 | First slice SL-T10 (the T10 remainder): integration's shared files (15 min E), first green CI (10 min E), then the governed gate and merge. Every other slice depends on SL-T10 | 29 min in the DAG, 2 of which (steps 1–8) are already spent | E (execution-dag T10) |

Totals for steps 1–20:
- **Measured machine time:** about 3–6 min (2.2 min from R9, plus M4/M5/M7/M8).
- **With estimated agent orchestration:** about **14–28 min**.

A trap on the critical path:
- `check-sequencing` exempts only "the T0 bootstrap (the commit that adds .echelon/r9-governance.json)" (r9.fsx:983). Every commit before the checkpoints that touches `tests/`, `*.fsproj`, `package.json`, `package-lock.json` or `playwright.config.ts` is refused (r9.fsx:835–840).
- STARTUP §3.1 says "A refusal is a T10 defect; fix it in T10", but a T10 fix made before authorization is itself a sequencing violation.
- `package-lock.json`, which npm install writes, is in the same position. If it is not in the bootstrap commit, it cannot be committed until after authorization.
- So the bootstrap commit must already pass suite preflight. In practice, run steps 9–12 **before** the bootstrap commit is pushed.

## 2. What must happen after T0, and what can be frozen before it

**Must happen after T0, in the competition repository:**

| Step | Governing rule (quoted) |
|---|---|
| Any write to the repository: steps 1–8 | Published rule: "the repository starts empty"; "code starts at kickoff" (STARTING-STATE). AGENT-RULES duty 1: "nothing is written to it before T0". STARTING-STATE: "Before T0, nothing is written to the competition repository." |
| `conditor init`, bootstrap, build, ci-policy, commit, repo create | AGENT-RULES duty 1: "At T0, bootstrap (T10, the first timed task) … `conditor init` …; then generate the skeleton …; build `Causa.slnx`; run `conditor ci-policy check`; commit; publish with `conditor repo create`; first green CI." |
| `t0.json` | STARTUP §2.2: "Write the T0 record `evidence/t0/t0.json` in the bootstrap commit … It is never changed." |
| `govern` | STARTUP §2.1. authorize step 1: "run r9.fsx govern at T0" |
| Obligations compile, suite preflight, work plan, Praxis registration, scope checkpoint, initialization checkpoint, sequencing | STARTUP §3: "Implementation is prohibited until steps 1–5 pass." §3.2: "Do not write the work plan by hand." §3.3: `--at NOW --commit SRC-COMMIT` … "Commit it. It is immutable." They write `.echelon/`, `.ros/` and `evidence/t0/` in the repository |
| First green CI, SL-T10 gate | It needs the published repository (AGENT-RULES duty 1; STARTUP §4.5) |

**Can be prepared and frozen before T0, outside the repository, without changing what is built:**

| Item | Governing rule (quoted) |
|---|---|
| Conditor + workstation tools (P2) | STARTING-STATE "Before T0 … these exist before T0, all outside the competition repository: … the Echelon tools (Conditor, Praxis, Ordo, Percepta, Limen, Forma, Folio) installed on the host." R9 did this (M2) |
| A built `requirements-compiler` and the bootstrap generator | STARTING-STATE: "the deterministic bootstrap generator (`requirements-compiler v41`), tested in CI against disposable empty directories only" |
| Pinned versions (NuGet, npm, browsers) as caches | STARTING-STATE: "the pinned dependency versions and compile order". Caches of third-party tools are not "code". **Owner should confirm.** |
| spec-preflight, acceptance preflight, scope preflight, work-plan reproduction | LAUNCH-CHECKLIST §C (owner side, "no clock"). The same results must still be **written** at T0 (step 18) |
| Obligations compile, work plan, registration script | Their inputs are only SRC files at the lock commit. Precompute and compare digests at T0. The repository copies must still be generated at T0 (§3.2) |
| Planning | Published rule: "teams may plan beforehand, including the problem, stack, and sketch" |

Never before T0: anything in the competition repository; "No source is imported from an earlier rehearsal (X11)"; any Praxis state or checkpoint (they bind `--at NOW` and the repository's commits).

## 3. Design: a checksum-verified toolchain bundle

**Basis.** Conditor 0.10.20 (the pinned commit a4c53cee) already ships `conditor bundle create --resolved-set … --resolved-set-sha256 … --output DIR`, `bundle verify --path DIR`, and `workstation apply --artifact-mirror DIR --offline`. These are tested in Conditor CI `indy-init-offline-freeze`: offline apply in 3 s (M11), and CON-INDY-060..064 and -093 require exactly this. It has not been exercised with echelon-current 1.37.0.

**Contents** (`toolchain-bundle/`, built owner-side from the lock, one platform, linux-x64):
1. `conditor-linux-x64`: sha256 = lock `conditor.artifacts.linux-x64.sha256` (75849672…26bd), carrying the identity `0.10.20+a4c53cee…`, as `install-conditor` checks.
2. `resolved-set.json`: registry 5752402, `channels/echelon-current/linux-x64.json`, sha256 = lock `resolvedSet.platforms.linux-x64.sha256` (bba3594a…a91).
3. The `conditor bundle create` output for that set: the native release artifacts (praxis 3.25.0, dokimos 0.11.1, percepta-semantic 0.4.0, ordo 1.20.0, …), each with the sha256 the resolved set pins, plus the bundle's own manifest and provenance.
4. Optional, owner-approved third-party caches, each pinned by digest: the npm tarballs of `@playwright/test` 1.63.0 and `axe-core` 4.14.0 (npm `integrity` sha512); the Playwright 1.63.0 chromium/webkit builds; the NuGet packages xunit 2.9.2, xunit.runner.visualstudio 2.8.2 and Microsoft.NET.Test.Sdk 17.11.1; Conditor's source cache for the `indy-init` preset (`CONDITOR_CACHE_DIR`).
5. `bundle.json`: every file with its path, sha256, size and the lock field it answers to. The bundle is closed-world: a file not listed is a refusal.

**Verification at T0** (before step 2, written into the bootstrap commit as `evidence/t0/toolchain-bundle.json`):
- recompute every sha256 and compare it with the lock and `bundle.json`; require the lock's own digest to equal the owner-committed `toolchain.lock.json`;
- `conditor bundle verify --path`;
- `workstation plan` then `apply --artifact-mirror … --offline` (no network fallback);
- read back `expectedTools` versions as `r9.fsx workstation` does.
Any mismatch is a refusal; the fallback is the online install path, which is slower but has the same identity (CON-INDY-064).

**Must never contain:**
- application code, or any output of `requirements-compiler v41 --bootstrap`;
- `.fsproj`/`package.json`/`package-lock.json`/`playwright.config.ts` for Causa;
- anything from an earlier rehearsal (X11);
- `.ros`/Praxis state, work plans, checkpoints, `t0.json` or evidence;
- credentials or tokens (GH_TOKEN, `CAUSA_ACCEPTANCE_APP_*`);
- `node_modules` laid out inside a repository path;
- any version not in the lock.

**What it saves:**

| Item | Saving | Label |
|---|---|---|
| Workstation | online 10–12 s → offline about 3 s (M10, M11), or 17–42 s via r9.fsx (M3) | M |
| `conditor init`, warm cache | 45.8–48.4 s → 5.2 s (M1) | M |
| Browsers, npm, Forma | 37–123 s (M5) | M |
| Compiler build | 11–16 s (M8) | M |
| NuGet | small: 34 s cold (M9) against 24–31 s warm (M1); the runs are not like for like | M |
| **Total** | **about 2–4 min**, nearly all of it already avoided if the host is prepared as in R9 | M |

Its main value is **risk**, not minutes:
- R9's `conditor init` failed on the network until it was rerun anonymously (M2);
- CON-INDY-093 requires that competition-day success not depend on GitHub, package registries or remote source repositories once the offline bundle has been prepared.

## 4. Is 45 minutes justified?

**Measured.**
- Steps 1–8 took 2 min 10 s in R9 (M2), and 1.3–1.4 min in the lab (M1).
- Tool work for steps 9–20 is about 1–4 min (M4–M8).
- Measured machine time overall: **about 3–6 min.**

**Estimated.**
- Agent orchestration of about 40–60 commands, including 36 registrations: 6–15 min.
- Discovering the undocumented inputs (steps 13 and 18): 2–8 min.
- Three or four checkpoint commits and pushes: 1–3 min.
- Repository create and push: about 2 min.

**Recommendation: 15–30 min of set-up (planning value 25 min). 45 min is not justified by measurement.**
- It is about 7–15× the measured machine time, and nothing exercised in a rehearsal supports it.
- The upper end, 30 min, covers one suite-preflight refusal only if it is repaired before the bootstrap push (§1 trap). After the push, the cost is **U** and possibly unrecoverable without an owner ruling.

**Model double count.**
- `schedule.fsx` starts SL-T10 (29 min) *after* `--setup 45` (topology-a.json: SL-T10 start 45, integrated 77 at 0 CI).
- SL-T10's first 2 measured minutes (the bootstrap) are inside the set-up, so they are counted twice.
- Modelled time before any other lane can start: **T+77 at 0 CI, T+87 at 10 min CI.**
- The recommendation (set-up 25, SL-T10 27) gives T+55 / T+65 instead: about 22 min earlier, with the same scope, gates and estimates.
- If the generator also wrote the remaining T10 shared files into the bootstrap commit, SL-T10 would drop to about 10–12 min (first green CI plus gate).

## Changes that need owner approval

1. Change `--setup 45` to 25, with the range 15–30 as a sensitivity. Model SL-T10 at 27 min, not 29. This means regenerating `evidence/schedule/topology-a.json` (CI diffs it) and SLICE-EXECUTION-PLAN §3.
2. State the T0 definition: T0 is recorded before `git init` and carried in the bootstrap commit (R9 practice). It is not the commit timestamp.
3. Allow, under STARTING-STATE "Before T0", pre-T0 host caches: the toolchain bundle (§3), Playwright browsers, npm/NuGet caches for the locked versions, and Conditor's source cache. Rehearsal 010 should use Conditor's offline bundle path with online fallback.
4. Amend STARTUP §3:
   - list the commands that produce `requirements/verification/verification-manifest.json`, `evidence/t0/spec-preflight.json` and `evidence/t0/acceptance.log`;
   - require npm install (`package-lock.json`) and suite preflight to pass before the bootstrap commit is pushed;
   - say how a T10 defect found before authorization is repaired without a sequencing violation (amend the unpushed bootstrap commit, or an explicit exemption).
5. If adopted, a successor package (v4.1.5, a new tag and freeze) in which the generator writes `package-lock.json`, `build.fsx`, CI and `web/boundary.js` into the bootstrap commit. This changes scaffolding, not scope.

## Changes that need code

1. `r9.fsx verify-bundle --lock --bundle`: closed-world sha256 check against the lock and `bundle.json`, then `conditor bundle verify`, offline `workstation apply`, version read-back, and `evidence/t0/toolchain-bundle.json`. `r9.fsx workstation` would gain `--artifact-mirror/--offline` pass-through.
2. Qualify `conditor bundle create/verify` and offline apply for echelon-current 1.37.0 in Conditor CI (today only the older indy-init 0.1.0 set is covered, M11). Measure it with the network disabled (CON-INDY-062).
3. Switch Indy-init CI's install jobs from the R9 lock (1.36.0) to the Rehearsal 010 proposed lock (1.37.0), so M3 measures the real set.
4. A `tools/measure-t0-bootstrap.fsx` extension, or a new measurement, that times STARTUP §2–§3 end to end on a disposable skeleton, from obligations compile to authorize-implementation and check-sequencing. That would replace the estimated rows of §1 with measurements.
5. A scripted, idempotent Praxis registration from `.echelon/r9-work-plan.json` (one command, then `scope preflight`), replacing 36 agent turns.
6. Optionally, `check-sequencing` would accept a declared T10-repair commit before authorization, limited to the files suite preflight names. This needs owner approval first (item 4 above).
