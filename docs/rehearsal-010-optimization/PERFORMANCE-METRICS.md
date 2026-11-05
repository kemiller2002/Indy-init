> **Provenance.** Written by an independent read-only research agent on 2026-11-05 against Indy-init main 76e50ce, and placed here unedited except for this note. Each figure is labelled *measured* (a cited CI run or file) or *estimated*. Its recommendations are proposals: what has been implemented is listed in FINAL-REPORT.md.

# Rehearsal 010 performance instrumentation (draft)

Sources: Indy-init main 76e50ce (paths relative to it unless prefixed), Specula `claude/observer-findings-adapter`
(322210a, PR #11), Praxis `.ros/` in echelon-registry and conditor, GitHub REST (`gh api`, checked live against
Indy-init runs/jobs/pulls). `RC/` = `tools/requirements-compiler/src/`. Read-only; nothing was changed.

## 0. Findings that shape the design

1. **Progress events carry no time.** The schema table lists no time field (docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md:124-133);
   `eventOf` reads none (RC/Progress.fs:127-142). The R9 example has `at` on `implementation-started` only
   (evidence/stop-decision/r9-events.json:18,52), never on completion or verification (:56-99). Any builder time is builder-authored.
2. **Gate outputs carry no run identity or time.** `gate.json` (`indy-init.r9-gate-verdict/2`, tools/r9/r9.fsx:579-603)
   and the results manifest `dokimos.verification-results/1` (r9.fsx:676-680) name the candidate, never the run id,
   attempt or timestamps. The run id appears only in `producer.json` (tools/r9/verification-gate.yml:237) and
   attestations (:199). The artifact is keyed by candidate, `verification-<sha>` (:357), and for `pull_request` the
   candidate is the merge ref `github.sha` (:49-55). That is not the run's `head_sha`, so the join has to go through
   `GET /actions/runs/{id}/artifacts`.
3. **No outcome or failure signature survives.** `testIdentities` keeps names of Passed|Failed results and drops the
   outcome and message (RC/GovernedCommands.fs:142-161). Coverage keeps obligation-level `reasons` only (r9.fsx:465-475).
   The raw TRX and Playwright JSON are still in the artifact (`results/<suite>/`, r9.fsx:656-665; yml:360).
4. **The selection varies.** Development mode runs a narrowed suite set (yml:177-178). A test outside the selection
   is *not run*, never *passed*. `cancel-in-progress: true` (yml:38-40) cancels runs, and they leave no verdict.
5. **Specula counts raw CI failures.** `Metrics.recoveries` pairs a failed run with the next passing run of the same
   workflow name (specula src/Specula.Engine/Metrics.fs:107-126). That is the CI-failure-count conflation this design
   replaces. The PR #11 adapter only re-counts and displays observer findings (ObserverFindings.fs:68-93).
6. **Praxis times come from the builder's clock.** `occurredAt` source is `ros-clock` (echelon-registry
   .ros/telemetry/executions/EXE-20261105T013437445Z-4971d0ae.json:2081-2083). `links.requirements` is empty in all 70
   conditor executions. No Praxis event records a test or verification outcome: conditor events.jsonl has only work.* and
   artifact.contributed.

Authority labels used below: **GH** = GitHub server timestamp or record. **CI** = produced inside the governed
Verification gate job (runner clock, governed tools). Candidate code runs in that job (yml:27-35), so CI results are
recomputable, not tamper-proof. **DK** = Dokimos coverage verdict. **PX** = Praxis CLI on the builder's machine.
**B** = builder-authored (progress events, commit author/committer dates, commit messages, PR text). Commit *SHAs and
diffs* are content-addressed and verifiable. Their *dates* are B.

## 1. Required events: existing record, authority, gap

| Event | Existing record (evidence) | Authority | Gap / fix |
|---|---|---|---|
| Work item started | Praxis `work.started` {workItem, occurredAt} (echelon .ros/events/events.jsonl:2); progress `implementation-started` {workItem, requirements} (RC/Progress.fs:130), optional unparsed `at` | PX/B | No server-side start. Proxy: first GH record on branch `slice/SL-<wi>` (yml:81-100), which exists only once a PR opens (`pull_request`; push runs only main, yml:12-15). Label B. |
| Implementation completed | progress `implementation-completed` {requirements, commit} (Progress.fs:131); Praxis `work.completed` (events.jsonl:1); execution `links.commits` | B/PX | No time field. Add `at` (labelled B). Authoritative proxy: GH `created_at` of the first gate run whose `head_sha` = that commit. |
| First test execution | TRX `UnitTestResult startTime/outcome`, Playwright `results[].startTime/status` in artifact `results/<suite>/` (r9.fsx:656-665); job step "Run the plan's suites" `started_at` (jobs REST) | CI + GH | No extractor keeps the outcome. Builder-local runs: only `verification-recorded` (Progress.fs:132), no time, B. |
| First test failure | Same files: TRX `ErrorInfo/Message`, Playwright `errors[0].message` | CI | Signature is not extracted. Obligation-level only in coverage `reasons` (r9.fsx:475). |
| Corrective commit | No record. Derivable: `git log h0..h1` on the PR head chain between a failing and a passing run; diff classified by `isTestFile` (RC/Ledger.fs:571-575) | SHA/diff verifiable, date B | Derive in `metrics`. Never trust commit-message claims ("fix"). |
| Test passed | CI outcome Passed at candidate; DK obligation/identity `verified` (r9.fsx:546,588); `gate.json` verdict | CI/DK | Builder `verification-recorded pass` is B and never counts. Gate only "informational" (STARTUP:120). |
| Verification requested | GH run `created_at`, `event`, `head_sha`, `pull_requests[]` (checked: run 41963160840) | GH | None. |
| Verification started | GH `run_started_at`, job `started_at`, step times (jobs REST) | GH | None. Re-runs: use `run_attempt` and `/attempts/{n}/jobs`. |
| Verification completed | GH job `completed_at`, `conclusion`; `gate.json`, `slice-gate.json` (yml:339), `test-integrity.json` (:332), `regression.json` (:322) | GH + CI/DK | gate.json has no run id or time (finding 2). Join via artifacts REST. Cancelled runs give time but no verdict. |
| Merge requested | GH PR `created_at`; `ready_for_review`/`auto_merge_enabled` in `GET /issues/{n}/timeline` | GH | None (REST only). |
| Merge completed | GH PR `merged_at`, `merge_commit_sha` (checked: PR #117); ledger `integrated` {requirements, commit} (RC/Ledger.fs:73-80), no time | GH; ledger B | None for GH. `integrated` is refused unless verified, but is still B. |
| Deployment verified | No positive record. Failure only: `main-unhealthy`/`main-healthy` (Ledger.fs:53,95-96; STARTUP:115-116), B, no time. Candidate: push-to-main gate run on `merge_commit_sha` (yml:14-15) with `gate.json` PASS + `journeys/build.json`; GitHub deployments if the run repo has them (specula GitHub.fs:142-178) | GH + CI if defined | Gap. Define "deployment verified" = the push-to-main gate run at `merge_commit_sha` concluded success with gate PASS and build PASS (or a GH deployment status `success` for that SHA). |

**Correlation keys:**
- **Work item:** Praxis `workItem`, progress `workItem`, branch `slice/SL-<wi>`, gate.json `workItem`, slice manifest `workItem` (yml:98).
- **Requirement:** scope manifest; slice `requirements` (yml:99); coverage `obligation.requirement` (r9.fsx:475); obligation id `REQ/Vn/x`.
- **Test identity:** TRX `testName`, or Playwright `suite › … › spec` (GovernedCommands.fs:146-158), mapped to obligations via coverage `identities`.
- **Commit SHA:** run `head_sha`, `candidate`/`sourceSha` (merge ref), `merge_commit_sha`, progress `commit`.
- **CI run:** (`run.id`, `run_attempt`), from `producer.json runId` (yml:237) and the artifacts list.

## 2. Metrics: exact computations

The base relation is the governed observation
`O = (runId, attempt, prNumber?, event, headSha, baseSha, candidate, identity, outcome ∈ {passed, failed, absent}, signature?, retried, ranAt[CI], runCompletedAt[GH])`.

- `O` is built only from runs of the Verification gate workflow whose `.github/workflows/verification-gate.yml` passed check-governance (yml:104-110).
- `absent` = the identity is expected and has no result. Expected identities are the plan identities of the suites selected in that run's mode (obligations.json).
- A cancelled or skipped run produces no rows.
- `signature` = the first line of the failure message with digits, GUIDs, paths, timings and ports normalised, prefixed by the exception type or Playwright matcher.
- Code state `S = (headSha, baseSha)` for PR runs; `S = (sha, ⊥)` for push and dispatch runs.

**Episode classification**, per identity, ordered by `runCompletedAt`:
- **flake:** both failed and passed on the *same* S (rerun attempt), or Playwright `status: flaky`.
- **resolved-by-base:** a failing streak ends in a pass where only `baseSha` changed.
- **defect:** a maximal failing streak on one PR chain, or on main, that ends in a pass at S′ with a different `headSha`, where `git log h0..h1` is non-empty (the corrective commits).
- **open:** the streak never ends in a pass.
- **open-removed:** the streak ends in `absent`, or the identity is dropped from the plan selection.

A defect keys on (identity, first signature) and lists every signature it showed. This is better than one defect per (identity, signature) pair: a fix that changes the failure message is not a second defect. The output reports both counts.

| Metric | Computation | Authoritative sources |
|---|---|---|
| First-pass test success rate | \|{i : outcome of i's earliest O (min runCompletedAt; attempt 1; no retry credit) = passed}\| / \|{i observed}\|. Also per slice PR: first gate run concluded success / slice PRs | CI + GH |
| Unique failing tests | \|{i : ∃ O failed}\| over all identities ever failing, flake-only identities included; `flakeOnly` and `uniqueFailurePairs` (distinct (i, sig)) reported alongside | CI |
| Total failure occurrences | \|{(runId, attempt, i) : failed}\|. Playwright retries in one attempt count once (`retried` flag) | CI |
| Repair cycles per defect | Per defect: number of distinct failing code states before S′ (≥1); report mean, median, max | CI + git |
| Median time to repair | median over repaired defects of `runCompletedAt(first pass at S′) − runCompletedAt(first failure)`. GH times only; open and open-removed excluded and counted | GH |
| Regression rate | \|{i : i passed at a main state M (push run, or `gate-all-base` coverage, yml:316) and later failed at a main state, or at a PR merged result whose base passed it}\| / \|{i ever passed at a main state}\|. Split into `caught` (PR only; REGRESSION refusal, Ledger.fs:420) and `escaped` (failed on main) | CI/DK + GH |
| Test correction rate | Repaired defects whose corrective range touches only test files (Ledger.fs:571) / repaired defects; `mixed` (test + source) reported separately; any TEST_WEAKENED/SKIPPED/REMOVED (Ledger.fs:594-596) on those PRs is attached | git + CI |
| Implementation to verified merge | Per slice PR: `t1 = completed_at` of the push-to-main gate run on `merge_commit_sha` with gate PASS ("verified merge"), `t0 = created_at` of the PR's first gate run (GH). Builder variant `t0 = implementation-completed.at`, labelled B. Merges whose main run failed are `unverifiedMerges` | GH + CI |
| Verification share of elapsed | \|⋃ [job.started_at, job.completed_at] of gate jobs (cancelled included), clipped to W\| / \|W\|, where W = [T0, min(deadline, permitted stop, now)]. T0 and the deadline are bound the observer's way (RC/Observer.fs:16-30; STARTUP:234). Also `ciMinutes` = Σ durations, not unioned | GH |

## 3. How Specula can recompute or challenge

| Figure | What Specula can do today | Missing |
|---|---|---|
| Verification share, time to verified merge, gate timings | Parses runs, check-runs, pulls and deployments (GitHub.fs:18-19,65,106-178) | Jobs REST timings. Full pagination: `per_page=50` and `maxCommitPages=5` (GitHub.fs:10-18) truncate a 420-minute run (Metrics.fs:51 only warns). `run_attempt` handling for re-runs |
| Deployment verified | Deployment status parser (GitHub.fs:142-178) | An agreed definition (section 1) and the push-to-main run join |
| Test-level metrics (first pass, unique, occurrences, defects, flakes, regression) | Nothing: it reads no artifacts, TRX or Playwright JSON | Artifact download (`/actions/runs/{id}/artifacts`, zip); a TRX/Playwright parser; the plan's expected identities; the signature normaliser (shared spec) |
| Corrective commits, test correction | Commit list only (GitHub.fs:52-62) | Per-commit file lists (`/compare/{h0}...{h1}`), and the isTestFile rule as a shared spec |
| Any figure in the metrics file | Pattern from PR #11: refuse a file whose summary disagrees with its own rows (ObserverFindings.fs:84-86) | An adapter for `indy-init.r10-performance-metrics/1` that re-derives every aggregate from the per-observation and per-episode rows. Spot-checks rows against GH by `runId` and by the artifact `sha256`. Shows any value whose authority is B with a "builder-reported" badge. Replaces `failureToRecovery` with the defect episodes |

Challenge rule: Specula recomputes the GH-only metrics (verification share, time to verified merge, first gate PASS)
independently and marks the file `DISPUTED` on any mismatch beyond 60 s. For test-level metrics, it re-aggregates from
the rows and re-hashes sampled artifacts.

## 4. `metrics` command design

New pure module `RC/Performance.fs`, placed after `Ledger.fs` and `Observer.fs` in the fsproj, in the
TokenEconomics/Observer style: records and pure functions over parsed `Json`. IO lives only in
`Program.metricsCommand` (dispatch next to `token-economics`, Program.fs:1342). Exit codes follow GovernedCommands.fs:17
(0 written, 4 refused inputs, 2 usage).

```
requirements-compiler metrics --manifest scope-manifest.json --events BUILDER/evidence/progress/events.json
  --github DIR      # gh api REST dumps: runs.json, jobs/<run>-<attempt>.json, pulls.json, timeline/<pr>.json, artifacts/<run>.json
  --artifacts DIR   # unzipped verification-<sha>/ per run id (results/, gate/, slice-evidence/, obligations.json)
  --repo BUILDER    # for git log h0..h1 and diff --name-only
  --t0-record PATH --deadline-policy F --now ISO [--praxis BUILDER/.ros] --out metrics.json
```

```fsharp
type Authority = GitHub | GovernedCi | Dokimos | Praxis | Builder
type Stamp = { At: DateTimeOffset; Authority: Authority; Source: string }        // e.g. "run 123 job.completed_at"
type Outcome = Passed | Failed of signature: string | Absent
type Obs = { Run: int64; Attempt: int; State: string * string option; Identity: string; Outcome: Outcome; Retried: bool; At: Stamp }
type Episode = Defect of DefectRec | Flake of FlakeRec | ResolvedByBase of ... | Open of ... | OpenRemoved of ...
val observations : expected:Set<string> -> run:Run -> resultFiles:(string * string) list -> Obs list   // TRX/PW text in, rows out
val episodes     : corrective:(string -> string -> string list) -> Obs list -> Episode list           // git range as a function arg
val metrics      : Window -> Run list -> Pull list -> Episode list -> Obs list -> progress:Json list -> Json
```

Output `indy-init.r10-performance-metrics/1`:
```json
{ "schema": "indy-init.r10-performance-metrics/1", "readOnly": true, "observedAt": "…", "window": {"t0": "…", "end": "…", "endReason": "deadline|stop|now"},
  "inputs": [{"kind": "github-runs", "sha256": "…"}, {"kind": "artifact", "runId": 1, "attempt": 1, "name": "verification-<sha>", "sha256": "…"}],
  "metrics": [{"id": "median-time-to-repair", "value": 742, "unit": "s", "authority": "github", "inputs": ["defect:D-3", …],
               "unavailable": false, "reason": null}],
  "builderReported": [{"id": "impl-to-verified-merge-builder", "value": 1800, "authority": "builder", "timeSource": "progress-event at"}],
  "observations": [{"runId": 1, "attempt": 1, "state": ["h", "b"], "identity": "…", "outcome": "failed", "signature": "…", "retried": false, "at": {"at": "…", "authority": "governed-ci"}}],
  "episodes": [{"id": "D-3", "kind": "defect|flake|resolved-by-base|open|open-removed", "identity": "…", "signatures": ["…"],
                "failingStates": 2, "firstFailure": {…}, "firstPass": {…}, "correctiveCommits": ["sha"], "correction": "source|test|mixed",
                "integrityRefusals": ["TEST_WEAKENED …"]}],
  "summary": {"defects": 3, "flakes": 1, "open": 0, "openRemoved": 1, "uniqueFailingTests": 5, "failureOccurrences": 9} }
```

Labelling rules:
- Every timestamp is a `Stamp` with its authority.
- A metric's authority is the weakest of its inputs.
- Any metric that needs a B or Praxis time goes only in `builderReported`.
- Its headline twin is `unavailable: true` with a reason. It is never estimated: the policy of
  requirements-compiled/v4.1.4/analysis/conflict-telemetry.json `unavailablePolicy`.
- Progress events serve only to name work items and requirements. They never supply outcomes.

**Test cases** (pure; fixtures as JSON; each negative control asserts the *absence* of a count):
1. **Builder pass without CI (neg.):** `verification-recorded pass` for obligation X with no governed run. Expect test X absent from `observations`, `verifiedMerges = 0`, and the requirement not counted as passed.
2. **Rerun flake (neg.):** same S, attempt 1 Failed, attempt 2 Passed. Expect `flakes = 1`, `defects = 0`, occurrences 1, and no entry in median time to repair.
3. **Playwright retry:** one attempt with `status: flaky`. Expect a flake, occurrences 1, and first-pass counted false.
4. **Removed from selection (neg.):** fails at h0; at h1 the suite is deselected, or the test is deleted. Expect `Absent`, then `open-removed`, `uniqueFailingTests` including it, and `defects = 0`.
5. **Real repair:** fails at h0 (sig A); corrective commit touches `src/` only; passes at h1. Expect 1 defect, cycles 1, correction `source`, and repair time = GH completed_at difference (the commit dates in the fixture are deliberately wrong and must be ignored).
6. **Test correction:** corrective range touches only `tests/…`. Expect test-correction rate 1.0, and a TEST_WEAKENED refusal from test-integrity attached.
7. **Base-only change (neg.):** head unchanged, base moved, then pass. Expect `resolved-by-base` and no repaired defect.
8. **Signature drift:** a streak of sig A then sig B, then a pass. Expect 1 defect with 2 signatures, `uniqueFailurePairs = 2`, and occurrences 2.
9. **Cancelled and overlapping runs:** job 10:00-10:20 cancelled, job 10:10-10:30 passed. Expect verification time 30 min (union), `ciMinutes = 40`, and no observations from the cancelled run.
10. **Builder-only timing (neg.):** `implementation-completed` with an `at` and the merge's push-to-main run failed. Expect the headline impl-to-verified-merge `unavailable` with reason "no verified merge"; the builder value appears only in `builderReported`; `unverifiedMerges = 1`.
11. **Window:** a run created before T0, or after the deadline, is excluded from verification share and from the rates.
