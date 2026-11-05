> **Provenance.** Written by an independent read-only research agent on 2026-11-05 against Indy-init main 76e50ce, and placed here unedited except for this note. Each figure is labelled *measured* (a cited CI run or file) or *estimated*. Its recommendations are proposals: what has been implemented is listed in FINAL-REPORT.md.

# Rehearsal 010 verification pipeline: where time goes, and what evidence can safely be reused

Sources: Indy-init `76e50ce` (main), Dokimos `1b253e4`, Ordo `db6d39e` (anonymous shallow clone). All paths below are
Indy-init paths unless they are prefixed `dokimos:` or `ordo:`. "MEASURED" means a GitHub Actions REST timing; everything
else is code reading or an estimate, and is labelled as such.

**Measurement limit.** The governed gate (`tools/r9/verification-gate.yml`) runs in the competition repository. `gh api` on
`kemiller2002/indy-init-rehearsal-010` and `-009` returned HTTP 403 ("GitHub access to this repository is not enabled for
this session"). So no governed-gate run has been timed. The only real timings come from Indy-init's own
`preflight-tooling.yml`, which runs the same `r9.fsx suite-preflight`/`run-suites` code on a fixture tree.

## 1. Which tests run at which stage, and where they repeat

Suites in the frozen plan (`requirements-compiled/v4.1.4/verification-execution-plan.json`, `.suites[]`; jq output):

| Suite | Runner (abridged) | Env | dependsOn | Tests |
|---|---|---|---|---|
| SU-OWN-DOTNET | `dotnet test Causa.slnx --filter Contract/Domain/Persistence/App.Tests` | ci | none | 141 |
| SU-OWN-BROWSER | `playwright --project=chromium tests/browser/contract` | ci-browser | OWN-DOTNET | 3 |
| SU-DOTNET-DETERMINISTIC | `dotnet test tests/Causa.Acceptance --filter ...Deterministic.` | ci | OWN-DOTNET | 116 |
| SU-DOTNET-INVARIANTS | `dotnet test tests/Causa.Invariants --filter ...!Live.` | ci | OWN-DOTNET | 21 |
| SU-BROWSER-CHROMIUM | `playwright --project=chromium acceptance invariants` | ci-browser | DETERMINISTIC | 281 |
| SU-BROWSER-WEBKIT | `playwright --project=webkit acceptance invariants` | ci-browser | DETERMINISTIC | 72 |
| SU-DOTNET-LIVE | `dotnet test Causa.slnx --filter ...Live.` | live-github | DET, INV | 141 |
| SU-BROWSER-LIVE | `playwright --project=chromium tests/browser/live` | deployed-live | DOTNET-LIVE, CHROMIUM | 17 |
| SU-VERIFIER / SU-REVIEW-RECORDS / SU-PROCESS-RECORDS | record or independent producers | | | 2/2/8 |

No test identity belongs to more than one suite (a jq `group_by` over `.suites[].tests[].id` finds 0 duplicates). Any
repetition therefore comes from running the same suite at the same commit more than once.

| Stage | What runs | Evidence |
|---|---|---|
| Local development | "Run the slice's acceptance tests and the regression set", builder-chosen, supporting only | STARTUP-INSTRUCTIONS.md:98, :27; local browsers are Chromium only (FINAL-READINESS.md:126) |
| PR verification (= governed slice gate) | One `verify` job on the merge result `refs/pull/N/merge`: suite-preflight lists all 8 executable suites; run-suites runs the 5 development suites (all 8 if the slice is `realProvider`); catalog Chromium requirement probes; J1-J3 and the design contract on Chromium and WebKit; r9 gate; then **the 5 development suites again at the target base**; test-integrity; slice-gate | verification-gate.yml:13, 133-137, 166-180, 212-246, 247-277, 278-290, 291-339 (base re-run 311-312) |
| Integration on main | The same workflow on `push: main` (development mode, scope file): the same suites, catalog and journeys again, at the new main SHA | verification-gate.yml:14-15, 177, 286; STARTUP-INSTRUCTIONS.md:115 ("verify the merged commit") |
| Release / completion | `workflow_dispatch mode=release`: all 8 executable suites with `--require-tests true`, record evidence, catalog, journeys, completion-gate | verification-gate.yml:176, 181-201, 340-352 |

Repeated executions:

- **R1. The base SHA's suites run once per PR gate run.** The slice gate re-runs SU-OWN-DOTNET, DETERMINISTIC,
  INVARIANTS, OWN-BROWSER and CHROMIUM at `target_base` (verification-gate.yml:311-312). `STALE_TARGET` requires
  `target_base == target_head` (tools/requirements-compiler/src/Ledger.fs:380), so `target_base` is always the current
  main head. The `push: main` run for that SHA has already run the same five commands (yml:177; the list at :312 is
  identical). Each main SHA is therefore verified 1 + k times for this suite set, where k is the number of PR gate runs
  built on it, including every re-run after a `STALE_TARGET`.
- **R2. The merged tree is verified twice under two SHAs.** The PR gate judges `refs/pull/N/merge`. Clicking merge then
  creates a new commit on main (a merge commit or a squash) with a different SHA, which `push: main` verifies from
  scratch (yml:14-15). The trees are identical when the base has not moved, which is exactly the state slice-gate
  requires. The r9 gate binds results to `git rev-parse HEAD` (tools/r9/r9.fsx:434-436), so nothing can carry over.
- **R3. The release run repeats the development suites at main's HEAD.** This is intended
  (dokimos:docs/evidence-reuse.md:146-164, `FINAL_VERIFICATION_REQUIRED`). Keep it.
- **R4. Within one job:**
  - Each dotnet suite is built twice: once by `--list-tests -m:1` in suite-preflight (r9.fsx:731) and again by run-suites
    (r9.fsx:666).
  - Playwright browsers are installed twice (yml:165, then yml:269 for the governed journeys).
  - requirements-compiler is built twice (yml:193, 227).
  - SU-OWN-DOTNET and SU-DOTNET-LIVE both target `Causa.slnx`, so every test project's host starts for each one. MEASURED:
    6 TRX files each on the fixture tree (run 41963048303, job 127033647363 log, lines "SU-OWN-DOTNET: exit 0, 6 result
    file(s)" and the same for SU-DOTNET-LIVE).

What each repetition costs, MEASURED where possible (run 41963048303, `preflight-tooling.yml` at 76e50ce; cross-checked
against run 41961419895):

| Item | Measured | Source |
|---|---|---|
| suite-preflight, all 8 suites, fixture tree (1-4 trivial tests per suite) | 30.1 s (03:43:28.47 to 03:43:58.54) | job 127033647363 log |
| run-suites, all 8 suites, same tree | 36.7 s (03:43:58.54 to 03:44:35.25) | same log |
| Whole suite-commands step (preflight + run) | 67 s / 62 s | step #6, both runs |
| Playwright Chromium + WebKit install (plus Forma) | 101 s / 123 s | r10-adversarial step #6 |
| Fixture trees incl. `npm install` + `playwright install --with-deps` | 123 s | suite-commands step #4 |
| Queue time (job created to started) | 2-3 s (max 12 s in 41961419895) | jobs API created_at vs started_at |

These fixture numbers are a **floor**, the fixed process, restore and build overhead of 8 serial suite commands. They are
not Causa's real suite durations, which are unmeasured (403 above). ESTIMATE: R1 costs one full development-suite pass per
PR gate run, roughly equal to the head pass. R4's second browser install costs up to about 100 s per run if the
`--with-deps` apt step repeats.

Why it matters (MODELLED, not measured): tools/r10/schedule.fsx gives 28/27/23/17/14/11 slices integrated by T+420 at
0/5/10/15/20/25 min CI latency per gate run (SLICE-EXECUTION-PLAN.md:113-120). Merges are strictly one at a time
(:105), so gate latency sits on the critical path.

## 2. What Dokimos and Ordo reuse already guarantees

**The governed gate uses neither reuse mechanism.** It contains no `dokimos evidence plan` or `evidence record`
(grep finds nothing in r9.fsx or verification-gate.yml). Every governed verdict comes from execution in the same job:
results carry `sourceSha = HEAD` (r9.fsx:666-687), and the gate refuses any run at another SHA (r9.fsx:434-436). Dokimos
`verification coverage` also admits only runs whose `SourceSha` equals the candidate
(dokimos:src/Dokimos.Core/Verification/Coverage.fs:78-93). Coverage must be for the same plan id and digest
(r9.fsx:471), and the lock pins the plan file's SHA-256 (yml:79-80).

**Dokimos `evidence plan`** (dokimos:src/Dokimos.Core/EvidenceReuse.fs, documented in docs/evidence-reuse.md):

- **Key.** `dokimos.evidence-identity/1` is a SHA-256 over a length-framed canonical form of `Sources` (committed bytes
  of mapped files plus `GlobalInputs`), `Dependencies`, `TestContract`, `Configuration` (where the runtime environment is
  declared) and `ToolVersion` (EvidenceReuse.fs:158-164; docs:67-85). `TestId` keys the store path (docs:197).
  `ProducedAtRevision` must be immutable (EvidenceReuse.fs:264-265).
  - The candidate SHA is part of the key only for `per-commit` freshness (`PER_COMMIT_REVISION_CHANGED`, :272-275).
  - No plan or contract digest is part of the key unless the host adds the contract file to `GlobalInputs`.
- **Reuse requires all of the following:**
  - a passing prior (`PRIOR_NOT_PASSED`, :270-271);
  - unchanged inputs (`INPUT_CHANGED`, :276-280);
  - an untampered identity (`IDENTITY_TAMPERED`, :262-263);
  - a test that is not `live`, `security` or `independent-acceptance` (:315-320);
  - no failing record at the same identity (`CONFLICTING_PRIOR`, :325-338).
  - `ChangedPaths` can only add tests to the run set (:340-345), and the `release` checkpoint runs everything
    (docs:146-164).
- **Failing results are persisted.** `evidence record --outcome failed|unavailable` writes immutable records
  (docs:205-217). A different record under the same `EvidenceId` is a store conflict, exit 5 (docs:210-212).
- **A failing test can still drop out of required verification.** The store is consulted only for the tests in the
  request (dokimos:src/Dokimos.Cli/EvidenceCommand.fs:47-57, `request.Tests |> List.map (fun t -> store.Provenance
  t.TestId)`), and the plan iterates only `tests` (EvidenceReuse.fs:361). If the next commit's host-built request omits a
  test, its stored failure is never read, so that test is neither run nor reported. Dokimos protects only the
  same-test, changed-selection case (docs:230: "the failing record has the current identity: PRIOR_NOT_PASSED").
- **Documented limitations** (docs:395-425):
  - records are unsigned, so an edit to `Outcome` alone goes undetected (:413-416);
  - `Category` and the checkpoint are host-declared (:417-425);
  - environment and dependencies are declared, not detected (:403-405).

**Indy's own drop-out protection is in the governed layer.**
- The development scope may only grow (`check-governance`; tools/r9/tests.fsx:357 `check-governance-scope-only-grows`).
- The slice gate requires every obligation verified at the base to be verified at the head (Ledger.fs:411-418;
  yml:320-323).
- Gap: an obligation that was *failing* at the base is not in `required` (yml:320), so a flaky base failure removes it
  from that PR's regression set.
- In development mode `--require-tests true` is overwritten (yml:176-177). Per-identity coverage still refuses in-scope
  identities with no result (Coverage.fs:14-16).

**Ordo** (ordo:src/Ordo.Core/VerificationSchedule.fs):
- **Policy bindings.** Every policy binds `integrity`, `runId` and `sourceSha`. A deterministic policy can add the
  content-reuse key `integrity, inputDigest, planDigest, rehearsalId`, but only when the plan declares it reusable
  (ordo:src/Ordo.Core/VerificationPlanWire.fs:51-82; test ordo:tests/Ordo.Tests/VerificationScheduleTests.fs:366-372).
- **Stale and failing evidence are never satisfied.** Stale beats fail beats pass, and a fail stays `Failing`
  (VerificationSchedule.fs:243-256; tests :181-191, :230-263).
- **Reuse is strict.** Every reuse key must be present and equal (:215-221).
- **Fail-open.** A freshness binding with no current value is not judged stale (:200-209). This is asserted as intended
  by `a binding without a current value is not judged stale` (tests:193-196). A caller that omits `sourceSha` gets old
  evidence classified `Satisfied`.
- **No environment or toolchain key.** The Ordo reuse key has none.
- **Waves.** Ordo emits independent waves that "may run in parallel" (:183-185). The Indy gate does not use Ordo (only a
  version check, offline-factory-rehearsal.yml:124).

## 3. Existing proofs that bad evidence is refused, and the gaps

Already proven, each with a mutation that disables the check:

| Attack | Case | Refusal |
|---|---|---|
| Self-authored acceptance (implementer-run producer) | C04 (tools/r10/adversarial.fsx:456-462) | PRODUCER_NOT_GOVERNED |
| Catalog verification for another commit | C17 (:562-568) | CATALOG_WRONG_COMMIT |
| Whole-plan coverage of another commit given to completion-gate | C32 (:676-682) | EVIDENCE_STALE |
| Completion verdict for another commit | C30 (:662-668) | COMPLETION_CANDIDATE_MISMATCH |
| Builder writes its own completion verdict | C33 (:683-689) | PREMATURE_STOP (only owner `--completions`) |
| Progress events claim integration without governed evidence | C25 (:622-628) | SCOPE_NOT_INTEGRATED |
| Builder-written exclusion claiming operator authority | C19 (:580-586) | exclusion refused |
| Zero tests executed / test removed or weakened / unreadable base | C01 (:435-441), C18 (:569-579), C23 (:608-614) | executed no test; TEST_REMOVED/SKIPPED/WEAKENED; TEST_DIFF_UNAVAILABLE |
| Regression of integrated obligation; mock replacing live provider | C11 (:518-524), C08 (:489) | REGRESSION; J1 fails |
| r9 gate: stale-SHA results, previous-commit SHA, other-candidate evidence, missing SHA, dirty tree, stale matrix, other-commit catalog, record for wrong candidate | tools/r9/tests.fsx:199, 203, 205, 208, 216, 202, 183, 298 | "stale evidence", "not HEAD", STALE-REVIEW |
| Dokimos reuse: source/dependency/contract/configuration/tool-version change, tampered identity, live, per-commit, failing prior | dokimos:tests/Dokimos.Core.Tests/EvidenceReuseTests.fs:73-166; EvidencePlanCliTests.fs:191, 254, 276 | REUSE.* codes |

Gaps. grep finds no test outside `src/` for any of the following:
- **Slice-gate freshness:** `STALE_TARGET` (Ledger.fs:380) and every `*_STALE` from `atCandidate` (GATE, JOURNEYS,
  REGRESSION, BUILD, LIVE_PROVIDER, DESIGN_CONTRACT, CATALOG, TEST_INTEGRITY; Ledger.fs:348-352, 384-456).
- **Contract and catalog digests:** `CONTRACT_UNAPPROVED`, `CATALOG_DIGEST_UNAPPROVED`, `CATALOG_MANIFEST_MISMATCH`,
  `CATALOG_UNAPPROVED`, `CATALOG_REQUIREMENT_UNJUDGED` (CatalogAcceptance.fs:99-117).
- **Plan binding in the r9 gate:** "computed for another plan" and "Dokimos rejected runs" (r9.fsx:471-472).
- **Environment change:** results.json records no SDK, Playwright, browser or runner-image identity (r9.fsx:675-687).
  Today this is safe only because results never leave their job. Any cross-run reuse needs an environment key.
- **Dependency change without a source change** (for example `package-lock.json` or `global.json` only). Today SHA
  binding covers it. Under Dokimos reuse it is covered only if declared in `GlobalInputs` (docs:403-405).
- **Test dropped from the request with a stored failure** (EvidenceCommand.fs:57). No test.
- **Ordo with a missing current `sourceSha`:** a test exists, but it asserts the fail-open behaviour (tests:193-196).
- **Outcome-only edit of a stored record** (docs:413-416). Not detectable.

## 4. Parallelism

- **The gate is serial today.** There is one `verify` job (yml:42-46, `timeout-minutes: 40`). Steps run in order.
  `run-suites` executes suites sequentially (`let ran = suites |> List.map runOne`, r9.fsx:666), and so does
  suite-preflight (`List.map probe`, r9.fsx:743; `-m:1` is deliberate, :728-731). The base re-run (yml:311) comes after
  the head run in the same job.
- **Independent groups.** By the plan's `dependsOn`, and because every suite is its own process with its own results:
  - **Lane D** (deterministic dotnet): SU-OWN-DOTNET, DETERMINISTIC, INVARIANTS.
  - **Lane C** (Chromium): OWN-BROWSER, CHROMIUM, plus the catalog requirement probes (yml:231).
  - **Lane W** (WebKit): SU-BROWSER-WEBKIT.
  - **Lane B** (base): the base suites, if they are not reused.
  - **Lane L** (live): DOTNET-LIVE, BROWSER-LIVE, journeys. These all write to one test repository and branch
    (yml:259-261, `causa-acceptance`), so they must stay serial unless each lane gets its own branch.
  - `dependsOn` describes evidence ordering, not data flow. Running the lanes concurrently changes cost on failure,
    not results.
- **MEASURED analogue.** Indy-init's own preflight already fans out across 7 jobs. Run 41963048303 took 21m58s wall
  (03:40:22 to 04:02:20) for 3,717 s of job time; run 41961419895 took 3,531 s. The critical path is r10-adversarial
  (1,313 s), whose step "Adversarial validation (18 cases)" ran 944 s / 950 s serially over 36 cases, rebuilding
  requirements-compiler for each compiler mutation (adversarial.fsx:100-111). fsharp-tooling's Forma layout step
  (Chromium and WebKit in one process) ran 803 s / 758 s.
- **Real split unmeasured.** The Chromium vs WebKit vs dotnet split of a real Causa gate cannot be measured from here.
  By test count, CHROMIUM (281) dominates WEBKIT (72), so the WebKit lane would finish first. This is an ESTIMATE.

## 5. Proposed dependency-aware strategy (one authoritative integration decision per candidate)

| Stage | Authority | Reuse allowed | Change |
|---|---|---|---|
| Development (local, builder) | none (supporting) | Dokimos `evidence plan --store` incremental, functional tests only | Use it for fast feedback; never cite it to a gate |
| Pre-integration (PR on merge result) | **the** integration decision | base evidence only (below); head is always executed | Parallel lanes D/C/W/L in separate jobs; a final `decide` job runs r9 gate + slice-gate once |
| Integration (main) | confirmation, not a second decision | same-SHA adoption of the PR decision | Merge the exact gated SHA (merge queue or fast-forward), so main HEAD = the judged candidate |
| Release | completion-gate | none (`--require-tests`, all suites, FINAL_VERIFICATION_REQUIRED) | unchanged |

Concrete changes:

1. **Make the gated SHA the main SHA.** Add a `merge_group` trigger and require the merge queue, or have a governed
   merger push the gated merge commit as a fast-forward. The slice gate then judges the exact commit that becomes main.
   The `push: main` run (R2) collapses into a same-SHA check, because the judged SHA is the one on main. No tree
   equivalence code is needed. Effect: removes one full gate run per merge (ESTIMATE: the whole gate duration, which is
   unmeasured). This keeps one decision per candidate.
2. **Reuse base evidence by SHA, not by content.** The slice gate's base results (yml:311-316) may be taken from the
   governed run's artifact `verification-<base>` (yml:357). Conditions:
   - the run's `head_sha == base`;
   - the workflow path and blob equal the governed `verification-gate.yml` (check-governance already pins it);
   - `results.json.candidate == base`, and every `runs[].sourceSha == base`;
   - `obligations.json` has the same `planId`/`planDigest`;
   - the run used the same lock digest.

   Otherwise fall back to re-running. A failed or absent base artifact means re-run, never "no regression". Effect:
   removes R1, one development-suite pass per PR gate run (ESTIMATE: about equal to the head pass).
3. **Split the `verify` job into lanes D, C, W and L plus `decide`.** Add `r9.fsx merge-results`, which concatenates
   per-lane `results.json` and refuses differing `candidate` or `sourceSha`. Effect (ESTIMATE): wall time becomes
   max(lane) plus setup instead of the sum. The live lane stays serial.
4. **Stop main verification being cancelled.** `concurrency: cancel-in-progress: true` keyed on `github.ref` (yml:38-40)
   cancels an in-progress `push: main` run when the next merge lands. Then `main-unhealthy` (STARTUP:116) may never be
   observed for that SHA. Use `cancel-in-progress: ${{ github.event_name == 'pull_request' }}`. Correctness fix, no
   speed effect.
5. **Remove in-job duplicates.**
   - Build each dotnet test project once and reuse the build for listing and running. This needs an r9 option, because
     the plan's commands are frozen.
   - Install browsers once (yml:165, 269).
   - Build requirements-compiler once (yml:193, 227).
   - MEASURED floor of what is spent on listing plus running: 30.1 s + 36.7 s on trivial tests. Browser install:
     101-123 s.
6. **Keep the base failing set visible.** Feed the base's `failed` obligations into regression evidence. A test failing
   at the base and passing at the head is reported, not silently out of `required` (yml:320).
7. **Do not use content-addressed reuse in the governed gate in Rehearsal 010.** Its key lacks environment and contract
   digests in the Indy integration, and records are unsigned (docs:413-416). Use it only in development (stage 1).

MEASURED: queue time (2-12 s) is negligible, so the opportunity lies in execution, not scheduling. MODELLED: each 5 min
saved per gate run is worth 1-6 more slices by T+420 (SLICE-EXECUTION-PLAN.md:113-120).

## 6. Negative controls and mutation tests to add

Each case should use the adversarial.fsx form: Attack, Control, Mutation, with an exact expected code.

| # | Input (control passes) | Mutation (attack) | Expected refusal |
|---|---|---|---|
| N1 | slice-gate evidence with `targetBase == targetHead` | `--target-head` = another 40-hex SHA | `STALE_TARGET`; mutation `if false then [ ref' "STALE_TARGET"` must make the attack pass |
| N2 | gate.json with `candidate` = merged SHA | set gate.json `candidate` to the base SHA | `GATE_STALE` |
| N3 | journeys, build, live, design, test-integrity, regression, catalog at candidate | one file at a time with `candidate` replaced | `JOURNEYS_STALE`, `BUILD_STALE`, `LIVE_PROVIDER_STALE`, `DESIGN_CONTRACT_STALE`, `TEST_INTEGRITY_STALE`, `REGRESSION_STALE`, `CATALOG_STALE` (one case per code; mutate `atCandidate`'s `candidateOf x <> e.Candidate` to `false`) |
| N4 | catalog-acceptance with contract sha = approved sha | append one byte to the contract file (or pass another `--approved-contract-sha256`) | `CONTRACT_UNAPPROVED` |
| N5 | manifest `source.catalogSha256` = contract catalog sha | change one requirement text in the catalog, recompile manifest | `CATALOG_DIGEST_UNAPPROVED` |
| N6 | verification `manifest.contentHash` = governed manifest | verification judged against a manifest with one requirement removed | `CATALOG_MANIFEST_MISMATCH` and `CATALOG_REQUIREMENT_UNJUDGED` |
| N7 | r9 gate with coverage for `planDigest` D | obligations.json with `planDigest` D' (same tests) | r9 REFUSED "the coverage report was computed for another plan" (r9.fsx:471) |
| N8 | base results from governed artifact for base SHA (proposal 2) | (a) `runs[0].sourceSha` = other SHA; (b) artifact from a non-governed workflow path; (c) different lock digest; (d) base run concluded `cancelled` | the reuse step refuses with `BASE_EVIDENCE_STALE` / `BASE_EVIDENCE_PRODUCER` / `BASE_EVIDENCE_TOOLCHAIN` / `BASE_EVIDENCE_INCOMPLETE` and falls back to execution (new codes) |
| N9 | `merge-results` of two lanes at SHA S (proposal 3) | lane W's results.json `candidate` = S' | `merge-results` REFUSED "lanes disagree on candidate"; gate never sees a mixed manifest |
| N10 | Dokimos store with a failing record for test T at identity I | next request omits T, its mapping still matches committed files | new `REUSE.REQUIRED_TEST_OMITTED` (plan must list stored-failing tests not in the request), exit 3 |
| N11 | Dokimos passing record with `Configuration{runtime.framework=net10.0}` | current `net10.0.1xx` SDK, or Playwright 1.63.0→1.64.0 | `REUSE.INPUT_CHANGED` `:configuration:`/`:tool-version:` (exists in core tests; add a CLI case with Playwright and browser revision as declared inputs) |
| N12 | Dokimos passing record; `package-lock.json` in `GlobalInputs` | change only `package-lock.json` | `REUSE.INPUT_CHANGED` `:source:package-lock.json` and `REUSE.PATH_AFFECTED`; a mutation dropping lock files from `GlobalInputs` must yield `REUSE.UNMAPPED_CHANGE`, never reuse |
| N13 | Dokimos record | edit only `Outcome` failed→passed in the stored file | today: undetected (docs:413-416). Required: refusal `IDENTITY_TAMPERED`-class after `Outcome` enters the signed or digested record |
| N14 | Ordo schedule with `sourceSha` in `Current` | omit `sourceSha` from `Current` | must be `Stale ["sourceSha"]` or a refusal, not `Satisfied` (inverts tests:193-196) |
| N15 | slice-gate with base where obligation O failed and head where O fails | proposal 6 | regression evidence lists O as failing at both; a mutation that drops base failures must fail the case |

## Gaps that need code

1. Same-SHA integration. Add a `merge_group` trigger (or a governed fast-forward merge) so the gated SHA is main's SHA.
   Cheapen the `push: main` run to a confirmation (verification-gate.yml:13-15).
2. Base-evidence adoption by SHA from the governed run's artifact, with provenance, plan, lock and SHA checks and an
   execute fallback (replaces yml:311-316). Needs new refusal codes and N8.
3. `r9.fsx merge-results` plus a lane split of the `verify` job (r9.fsx:646-697 is single-manifest and serial). Needs N9.
4. `concurrency.cancel-in-progress` must not cancel `push: main` verification (yml:38-40).
5. Tests for every slice-gate `*_STALE` and `STALE_TARGET` (Ledger.fs:348-456) and for the catalog digest codes
   (CatalogAcceptance.fs:99-117). Covered by N1-N7.
6. Dokimos: report stored-failing tests that are omitted from a request (EvidenceCommand.fs:57; N10). Bind `Outcome`
   into a signed or digested record (N13).
7. Ordo: make a missing current value for a required freshness binding fail closed (VerificationSchedule.fs:200-209;
   N14).
8. Record an environment identity (SDK, Playwright, browser revisions, runner image) in `dokimos.verification-results/1`
   before any cross-run reuse (r9.fsx:675-687).
9. Restore `--require-tests` in development mode (yml:176-177 overwrites it). Build once for list and run (r9.fsx:731 vs
   666).
10. Measurement. Get read access to `indy-init-rehearsal-010` (or 009) Actions and time the real gate's steps. Every
    gate-duration figure here is an estimate until then.
