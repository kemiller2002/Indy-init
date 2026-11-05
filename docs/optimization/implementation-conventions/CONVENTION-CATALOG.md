# Implementation Convention Catalog (v1.0.0, PROPOSED)

The human-readable form of [CONVENTION-CATALOG.json](CONVENTION-CATALOG.json). If the two disagree, the JSON is authoritative, and `tools/conventions/conventions.fsx validate` checks that every convention's id, level, tier and status below matches it. **Nothing here is adopted until the owner approves it.** `ENFORCED-EXISTING` means an existing check already enforces the rule, and the catalog only names it. Net minutes for each convention are in IMPACT-FORECAST.md (generated).

Levels:

- **MANDATORY:** required for correctness, compatibility or process integrity.
- **PREFERRED:** the standard pattern; a deviation needs a justification.
- **DISCRETIONARY:** agents choose.

Tiers:

- **1:** adopt before the competition.
- **2:** adopt only if EXP-INDY-002 validates it.
- **3:** defer.

| ID | Title | Level | Tier | Status | Prevents (failure modes) |
|---|---|---|---:|---|---|
| CV-01 | Execute every generated command before freeze | MANDATORY | 1 | ENFORCED-EXISTING (v4.1.4+); extend to deploy preconditions | FM-03, FM-22 |
| CV-02 | Stop only on STOP_PERMITTED, clock from the T0 record | MANDATORY | 1 | ENFORCED-EXISTING | FM-01 |
| CV-03 | One scope source, pinned and fully itemised before code | MANDATORY | 1 | ENFORCED-EXISTING | FM-02 |
| CV-04 | WASM boot smoke on the first CI build; bounded view functions | PREFERRED | 1 | PROPOSED | FM-04 |
| CV-05 | One source for environment literals | PREFERRED | 1 | IMPLEMENTED-NOT-ADOPTED | FM-05 |
| CV-06 | Workflows pass Conditor ci-policy | MANDATORY | 1 | ENFORCED-EXISTING | FM-06 |
| CV-07 | Workflow shell is pipefail by default | MANDATORY | 1 | IMPLEMENTED-NOT-ADOPTED | FM-07 |
| CV-08 | Single-writer files and append-only shared state | MANDATORY | 2 | PARTIALLY ENFORCED | FM-08 |
| CV-09 | Durable means read back from the real provider | MANDATORY | 1 | PARTIALLY ENFORCED | FM-09 |
| CV-10 | Typed errors through one state machine; no silent defaults | MANDATORY | 1 | IMPLEMENTED-NOT-ADOPTED | FM-10, FM-13 |
| CV-11 | Contracts proven against real data before release | MANDATORY | 1 | PROPOSED; content addressed by v4.1.5 CANDIDATE (E1 all 50 causa-storage events, W1 workspace) pending ODR-V415-01/-02 | FM-11 |
| CV-12 | Released contracts are immutable versions; consumers pin them | PREFERRED | 2 | PARTIALLY ENFORCED | FM-12 |
| CV-13 | Every bound test has a falsifying control | PREFERRED | 3 | PROPOSED | FM-13 |
| CV-14 | Reconcile requirements with owner decisions before freeze | MANDATORY | 1 | PROPOSED | FM-14 |
| CV-15 | Evidence is bound to the candidate SHA | MANDATORY | 1 | ENFORCED-EXISTING | FM-15 |
| CV-16 | Only the owner records approvals | MANDATORY | 1 | ENFORCED-EXISTING | FM-16 |
| CV-17 | Forma first: current Forma, no containment workarounds | MANDATORY | 1 | IMPLEMENTED-NOT-ADOPTED | FM-17, FM-19 |
| CV-18 | Editable fields bind on input, through the field map | PREFERRED | 1 | PROPOSED | FM-18 |
| CV-19 | Test-only code never ships | MANDATORY | 1 | PROPOSED | FM-20 |
| CV-20 | Schedules read the frozen ownership; gates are acyclic | PREFERRED | 2 | ENFORCED-EXISTING | FM-21 |
| CV-21 | Everything mandated is in the dependency lock | PREFERRED | 1 | PROPOSED | FM-22 |
| CV-22 | Standard per-feature F# file split (Contract/Domain/State/View/Tests) | DISCRETIONARY | 3 | DEFERRED | FM-10 |
| CV-23 | Stable requirement and field IDs, decoupled from F# names | PREFERRED | 2 | PROPOSED | FM-18 |

## CV-01 Execute every generated command before freeze

- **Level / tier / status:** MANDATORY / 1 / ENFORCED-EXISTING (v4.1.4+); extend to deploy preconditions
- **Problem:** Suite runners, SDK setup, deployment preconditions and browser installs generated into a package were first executed during a timed run.
- **Historical evidence:** HF-028, HF-029, HF-033, HF-047, HF-076, HF-018 (FAILURE-DATA.json)
- **Scope:** requirements packages, bootstrap, workflow templates
- **Required pattern:** A package is releasable only after every suite command, workflow and deploy precondition it generates has run on the pinned SDK in a clean checkout and produced its expected result (or an expected refusal).
- **Prohibited:** claiming readiness from synthetic gate results; patching a generated command inside a timed run without a successor package
- **Allowed exceptions:** live-provider suites whose credentials exist only at T0: run against the practice repository instead
- **Enforcement:** existing at package release: r9.fsx suite-preflight --require-tests; launch-preflight.fsx LP checks; Conditor scaffold CI
- **Check command:** `dotnet fsi tools/r9/r9.fsx suite-preflight ...`
- **Existing capability used:** tools/r9/r9.fsx suite-preflight; tools/r10/launch-preflight.fsx
- **Required tests:** negative control: a suite command naming two test projects (MSB1008) must be refused by suite-preflight
- **Prevents (prevention cons/central/opt):** FM-03 Generated commands, workflows and environment never executed before freeze (0.5/0.7/0.85); FM-22 Required dependency missing from the lock or silently dropped (0.2/0.3/0.4)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 0/0/0
  - pre-competition cost: 90 min
  - implementation cost: about 90 min per package release (one clean-checkout execution)
- **Confidence:** medium. Generated-command failures recur whenever a successor package is cut (R5, R8, R9); suite-preflight already catches the MSB1008 class, so the residual is deploy preconditions and environment setup. Impact if it occurs is 20-90 min (R8: live suites unexercised to T+94, first deploy T+129).
- **Owner:** Indy-init owner (package release); **approval required:** none for existing checks; extending to deploy preconditions is a successor-package change

## CV-02 Stop only on STOP_PERMITTED, clock from the T0 record

- **Level / tier / status:** MANDATORY / 1 / ENFORCED-EXISTING
- **Problem:** R9 stopped at 115 of 420 minutes; R7 projected time from stale timestamps.
- **Historical evidence:** HF-048, HF-057 (FAILURE-DATA.json)
- **Scope:** every timed run
- **Required pattern:** The run clock is read from the committed T0 record; work stops only on requirements-compiler stop-decision STOP_PERMITTED or the deadline.
- **Prohibited:** stopping because a gate is red; estimating elapsed time from commit timestamps
- **Enforcement:** existing at during run: requirements-compiler stop-decision; --t0-record/--deadline-policy
- **Check command:** `requirements-compiler stop-decision`
- **Existing capability used:** AGENTS.md; docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md
- **Required tests:** existing r9-replay negative control (tools/r10/r9-replay.fsx)
- **Prevents (prevention cons/central/opt):** FM-01 Run stopped or clock misread before the deadline (0.6/0.8/0.9)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 1/1/0.5
  - pre-competition cost: 0 min
  - implementation cost: none (exists)
- **Confidence:** medium. Probability is the chance an agent stops early or misreads the clock without the stop rule (2 of the 8 observed rehearsals, R7 and R9); impact uses R9's measured 305 min as the optimistic bound.
- **Owner:** run coordinator

## CV-03 One scope source, pinned and fully itemised before code

- **Level / tier / status:** MANDATORY / 1 / ENFORCED-EXISTING
- **Problem:** Agents built the 38-requirement catalog instead of the P0 plan; scope digest changed three times during readiness.
- **Historical evidence:** HF-001, HF-037, HF-038, HF-064, HF-062 (FAILURE-DATA.json)
- **Scope:** every timed run
- **Required pattern:** docs/rehearsal-010-readiness/scope-manifest.json (digest pinned in the launch lock) is the only build target; every work item exists in Praxis and the scope checkpoint is committed before application code.
- **Prohibited:** naming a catalog or mission text as the target; changing scope without a new digest and owner approval
- **Enforcement:** existing at T0 / initialization: r9.fsx check-sequencing; launch-preflight LP-04 scope digest
- **Check command:** `r9.fsx check-sequencing`
- **Existing capability used:** tools/r9/r9.fsx; tools/r10/launch-preflight.fsx
- **Required tests:** r9-replay WORK_ITEM_MISSING control
- **Prevents (prevention cons/central/opt):** FM-02 Build target ambiguous or changing (catalog vs plan, mission text vs baseline, unstated priority) (0.4/0.6/0.75)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 3/2/1
  - pre-competition cost: 30 min
  - implementation cost: none (exists)
- **Confidence:** medium. Scope ambiguity recurred in R3, R8, R9 and readiness; impact if it recurs is 60-240 min of misdirected build. check-sequencing already enforces itemisation; value shown is what that enforcement secures.
- **Owner:** run coordinator

## CV-04 WASM boot smoke on the first CI build; bounded view functions

- **Level / tier / status:** PREFERRED / 1 / PROPOSED
- **Problem:** R4 lost about 120 min to a WASM interpreter trap with Release debug info; R5's app could not start for 90 min (12,683 IL locals in View.project).
- **Historical evidence:** HF-007, HF-013 (FAILURE-DATA.json)
- **Scope:** Causa.Web and any F# view module
- **Required pattern:** The first CI job publishes the WASM app in Release and loads it in a browser until first render (boot smoke). View code is composed from per-region functions; no single view function renders more than one screen region.
- **Prohibited:** a single projection function rendering all screens; Release builds with full debug info for WASM
- **Allowed exceptions:** Debug builds used only locally
- **Enforcement:** review at first green CI: boot smoke test in tier-1 CI (plan's first-render journey); review obligation for view size
- **Review obligation:** Reviewer confirms the first CI run executed the boot smoke and that no view function spans more than one screen region.
- **Required tests:** boot smoke: page reaches first render within 60 s on Chromium
- **Prevents (prevention cons/central/opt):** FM-04 Application fails to start in the WASM runtime (0.4/0.6/0.8)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 3/2/1
  - pre-competition cost: 20 min
  - implementation cost: about 20 min to write the smoke step once
- **Confidence:** medium. Two consecutive rehearsals (R4, R5) lost 120 and 90 min to start failures; R6-R9 did not, so occurrence is discounted to 15-50%. The boot smoke shortens detection, it does not fix the cause.
- **Owner:** integration

## CV-05 One source for environment literals

- **Level / tier / status:** PREFERRED / 1 / IMPLEMENTED-NOT-ADOPTED
- **Problem:** R5's test-site port was defined in 11 places; a partial move broke main CI for about 28 min.
- **Historical evidence:** HF-014 (FAILURE-DATA.json)
- **Scope:** tests, specs, workflows, scripts in the competition repository
- **Required pattern:** Ports, hosts and base URLs used by tests come from one configuration file; every other file reads it.
- **Prohibited:** the same localhost:PORT literal in more than one file
- **Allowed exceptions:** documentation files (*.md)
- **Enforcement:** new at pre-commit / CI (seconds): tools/conventions/conventions.fsx check, rule CK-02
- **Check command:** `dotnet fsi tools/conventions/conventions.fsx -- check --workspace .`
- **Required tests:** conventions.fsx test: two files with the same port literal fail CK-02; one file passes
- **Prevents (prevention cons/central/opt):** FM-05 Environment literals (ports, hosts) duplicated across files (0.6/0.8/0.9)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 1/0.5/0.5
  - pre-competition cost: 5 min
  - implementation cost: implemented in this change
- **Confidence:** high. One measured occurrence (27.9 min); the check is seconds; CK-02 still finds the pattern in R9 and the preview app.
- **Owner:** integration

## CV-06 Workflows pass Conditor ci-policy

- **Level / tier / status:** MANDATORY / 1 / ENFORCED-EXISTING
- **Problem:** R4 ran 152 workflow runs with 25 failed builds; R6 had about 90 min with no main result; preflight CI ran every commit twice.
- **Historical evidence:** HF-011, HF-031, HF-032, HF-075 (FAILURE-DATA.json)
- **Scope:** every workflow
- **Required pattern:** conditor ci-policy check passes for .github/workflows (per-ref concurrency, cancel superseded PR runs, timeouts, no duplicate push+PR triggers).
- **Enforcement:** existing at bootstrap (T10): conditor ci-policy check
- **Check command:** `conditor ci-policy check --target .`
- **Existing capability used:** kemiller2002/conditor docs/ci-policy.md CIP-000..008
- **Required tests:** Conditor CiPolicyTests (rehearsal-006 fixtures)
- **Prevents (prevention cons/central/opt):** FM-06 CI workflow shape wastes or starves runs (0.5/0.7/0.85)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 1/0.5/0.5
  - pre-competition cost: 0 min
  - implementation cost: none (exists)
- **Confidence:** high. Measured: about 90 min without a main result (R6) and 152 runs with 25 failed builds (R4). Conditor ci-policy already enforces it; value shown is what it secures.
- **Owner:** integration

## CV-07 Workflow shell is pipefail by default

- **Level / tier / status:** MANDATORY / 1 / IMPLEMENTED-NOT-ADOPTED
- **Problem:** Shell steps hid failures in R6, R7 and R8 (bash -e without pipefail; tee pipelines that could not fail).
- **Historical evidence:** HF-030 (FAILURE-DATA.json)
- **Scope:** every workflow with run steps
- **Required pattern:** Every workflow declares defaults.run.shell: bash (GitHub then runs bash -eo pipefail), or every run step declares a shell.
- **Prohibited:** run steps relying on the implicit default shell (bash -e)
- **Allowed exceptions:** workflows with no run steps
- **Enforcement:** new at bootstrap / CI (seconds): tools/conventions/conventions.fsx check, rule CK-03
- **Check command:** `dotnet fsi tools/conventions/conventions.fsx -- check --workspace .`
- **Required tests:** conventions.fsx test: a run step without a declared shell fails CK-03
- **Prevents (prevention cons/central/opt):** FM-07 CI shell steps hide failures (no pipefail, swallowed output) (0.6/0.8/0.9)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 0.5/0.5/0.5
  - pre-competition cost: 5 min
  - implementation cost: implemented in this change; Conditor could absorb it as a CIP rule (proposal)
- **Confidence:** medium. Recurred R6-R8 (repair commits, minutes unrecorded); CK-03 finds 23 and 39 non-pipefail steps in R8 and R9. Impact per occurrence is modeled at 10-40 min of hidden red CI.
- **Dependencies:** CV-06
- **Owner:** integration

## CV-08 Single-writer files and append-only shared state

- **Level / tier / status:** MANDATORY / 2 / PARTIALLY ENFORCED
- **Problem:** R5: 17 of 92 merges conflicted; R8: 26 of 49 merges conflicted on .ros state; strict ownership moved cost into 19 change-request commits.
- **Historical evidence:** HF-015, HF-042, HF-043, HF-044, HF-079 (FAILURE-DATA.json)
- **Scope:** competition repository
- **Required pattern:** Every path has one owner (agent-file-ownership.json); compile lists are written once at bootstrap; tool state that every lane writes is append-only per lane or excluded from merges.
- **Prohibited:** two roles editing one file; per-merge reconciliation commits of tool state
- **Allowed exceptions:** integration applies change requests to shared files
- **Enforcement:** existing at bootstrap and every merge: agent-file-ownership.json + proposals/r10-contract-first-ui/tests/OwnershipTests.fs
- **Check command:** `OwnershipTests`
- **Existing capability used:** requirements-compiled/v4.1.5/agent-file-ownership.json
- **Required tests:** OwnershipTests: every path has exactly one owner
- **Prevents (prevention cons/central/opt):** FM-08 Concurrent writers conflict on shared files or shared state (0.4/0.6/0.75)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 5/3/2
  - pre-competition cost: 30 min
  - implementation cost: none for ownership; .ros merge policy is a Praxis change
- **Confidence:** low. Conflicts are measured as counts, not minutes; EXP-INDY-002 overlap is assumed at 30-50% because its strategy targets the same failure.
- **Dependencies:** EXP-INDY-002 results
- **Owner:** integration

## CV-09 Durable means read back from the real provider

- **Level / tier / status:** MANDATORY / 1 / PARTIALLY ENFORCED / **safety or correctness rule: enforce it whatever its time value**
- **Problem:** Saves were reported durable without verified read-back in R2, R4, R5, R6, R7, R8; the double lacked GitHub's cache headers so CI missed a stale read.
- **Historical evidence:** HF-006, HF-008, HF-021, HF-026, HF-039, HF-040, HF-074 (FAILURE-DATA.json)
- **Scope:** Causa persistence and save state machine
- **Required pattern:** A save is Saved only after the provider read-back returns the written blob SHA, read with the HTTP cache bypassed; in-memory and double results never satisfy a durability obligation; provider doubles replay recorded real responses including Cache-Control, ETag, pagination and size limits.
- **Prohibited:** marking an investigation Persisted before a confirmed write; restoring 'Saved' from browser storage; retry without backoff
- **Enforcement:** existing at live-provider preflight and candidate: live-provider preflight (J3-05); Dokimos evidence category real-provider vs double; state-machine tests
- **Check command:** `tools/requirements-compiler/tests/live-provider-preflight.yml`
- **Review obligation:** Reviewer confirms each durability obligation cites a real-provider run, not a double.
- **Existing capability used:** verification/journeys J3; requirements-compiled/v4.1.5/analysis/github-error-matrix.json
- **Required tests:** read-back with stale cache must fail (J3-05 regression); double replays recorded GitHub headers
- **Prevents (prevention cons/central/opt):** FM-09 Save reported durable without verified provider read-back (multi-writer, stale read, API limits, retry storms) (0.3/0.5/0.7)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 3/2/1
  - pre-competition cost: 45 min
  - implementation cost: about 45 min to record provider fixtures once
- **Confidence:** medium. The most recurrent family (7 rehearsals/experiments) but minutes were never recorded; prevention is capped at 30-70% because each past fix covered one variant.
- **Owner:** persistence

## CV-10 Typed errors through one state machine; no silent defaults

- **Level / tier / status:** MANDATORY / 1 / IMPLEMENTED-NOT-ADOPTED / **safety or correctness rule: enforce it whatever its time value**
- **Problem:** R4 bypassed Sync and swallowed faults; R5 stepOr swallowed book faults; R8 decided unknown commands as Observations; contract drift compiled only because warnings were errors.
- **Historical evidence:** HF-008, HF-022, HF-041, HF-073 (FAILURE-DATA.json)
- **Scope:** all F# projects
- **Required pattern:** Expected failures are Result<_, typed error>; state changes go only through the machine's step; unknown input is refused or preserved opaquely, never defaulted; TreatWarningsAsErrors stays on and FS0025/FS0026/FS0049 are never suppressed.
- **Prohibited:** | _ -> on a contract union in decision code; try ... with _ -> default; assigning machine state directly; NoWarn containing 25, 26 or 49
- **Allowed exceptions:** exhaustive wildcard after every case is listed explicitly, with a comment
- **Enforcement:** new at every build (seconds): tools/conventions/conventions.fsx check CK-01 (warnings-as-errors) + state-machine tests (existing) + review obligation
- **Check command:** `dotnet fsi tools/conventions/conventions.fsx -- check --workspace .`
- **Review obligation:** Reviewer confirms no wildcard/default branch on command or event unions in decision code.
- **Existing capability used:** requirements-compiled/v4.1.5/state-machine-contracts.json
- **Required tests:** conventions.fsx test: TreatWarningsAsErrors false or NoWarn 25 fails CK-01
- **Prevents (prevention cons/central/opt):** FM-10 State machine bypassed, faults swallowed, unknown input silently defaulted (0.4/0.6/0.75); FM-13 Tests that cannot fail, are never written, or over-claim what they prove (0.05/0.1/0.15)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 2/1/0.5
  - pre-competition cost: 5 min
  - implementation cost: CK-01 implemented in this change
- **Confidence:** medium. R4, R5, R8 evidence; CK-01 is cheap; wildcard-default detection is review-only so prevention is moderate.
- **Owner:** domain

## CV-11 Contracts proven against real data before release

- **Level / tier / status:** MANDATORY / 1 / PROPOSED; content addressed by v4.1.5 CANDIDATE (E1 all 50 causa-storage events, W1 workspace) pending ODR-V415-01/-02
- **Problem:** Frozen v4.1.4 cannot represent 487 of 1,315 causa-data events; the connection contract has no workspace; v4.1.1 had no command body.
- **Historical evidence:** HF-068, HF-069, HF-034 (FAILURE-DATA.json)
- **Scope:** requirements packages (contracts)
- **Required pattern:** Before a contract is released, every event type in the pinned causa-data histories decodes to a case or to an opaque preserved Unknown, and every required input of the governing requirements has a field.
- **Prohibited:** releasing a contract checked only against fixtures written for it
- **Enforcement:** existing at package release: proposals/r10-historical-data-compatibility replay coverage (HISTORICAL-COVERAGE.json, EVENT-COMPATIBILITY-MATRIX.json)
- **Existing capability used:** requirements-compiled/v4.1.5 (analysis/amendment.json E1, W1); proposals/r10-historical-data-compatibility
- **Required tests:** replay coverage: 0 unrepresentable events; negative control: removing a case makes coverage fail
- **Prevents (prevention cons/central/opt):** FM-11 Frozen contract cannot represent the real data or the required inputs (0.5/0.7/0.85)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 0/0/0
  - pre-competition cost: 60 min
  - implementation cost: about 60 min per package
- **Confidence:** medium. Not yet observed failing in a timed run because R10 did not run; the 487/1,315 gap is measured. The v4.1.5 candidate carries the fix, so the value depends on its approval.
- **Owner:** Indy-init owner (package release); **approval required:** owner: ODR-V415-01 and ODR-V415-02 (v4.1.5 approval and freeze); the convention itself (replay coverage as a release condition for every later package) is a new rule

## CV-12 Released contracts are immutable versions; consumers pin them

- **Level / tier / status:** PREFERRED / 2 / PARTIALLY ENFORCED
- **Problem:** Contract and package revisions cost 2-19 h each and blocked mid-run change requests.
- **Historical evidence:** HF-012, HF-035, HF-071, HF-072 (FAILURE-DATA.json)
- **Scope:** contracts shared by parallel agents
- **Required pattern:** A released contract version never changes; a change is a new version with every consumer's contract tests in the same pull request; each agent records the contract version it built against and integration refuses a mismatch.
- **Prohibited:** editing a released contract file in place
- **Enforcement:** existing at integration: frozen-package CI equality (AGENTS.md); contract tests at T14 (AGENT-RULES)
- **Review obligation:** Integration confirms each merged pull request names the contract version it was built against.
- **Existing capability used:** requirements-compiled/*/FREEZE.md
- **Required tests:** contract-version mismatch must be refused at merge (to be measured in EXP-INDY-002)
- **Prevents (prevention cons/central/opt):** FM-12 Contract or package revision needed during the build (0.3/0.5/0.65)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 3/2/1
  - pre-competition cost: 0 min
  - implementation cost: pending EXP-INDY-002
- **Confidence:** low. Revision costs are elapsed pre-competition hours (measured) but in-run cost is modeled; depends on EXP-INDY-002.
- **Dependencies:** EXP-INDY-002 results
- **Owner:** integration

## CV-13 Every bound test has a falsifying control

- **Level / tier / status:** PREFERRED / 3 / PROPOSED
- **Problem:** Tests credited with obligations they did not prove (R3-R9); 347 of 613 R8 obligations unverified because bound tests were never written.
- **Historical evidence:** HF-002, HF-009, HF-017, HF-027, HF-045, HF-046, HF-052 (FAILURE-DATA.json)
- **Scope:** P0 obligation tests
- **Required pattern:** A test bound to a P0 obligation is accepted only with a recorded control that makes it fail; bootstrap stubs fail rather than skip.
- **Prohibited:** skipped stubs counted as written; tags on tests that never exercise the obligation
- **Allowed exceptions:** record obligations verified by document checks
- **Enforcement:** existing at slice gate: slice-gate/completion-gate assertion ratchet; negative controls (tools/requirements-compiler AssertionRatchet)
- **Review obligation:** Reviewer samples one control per slice.
- **Existing capability used:** tools/requirements-compiler/src/AssertionRatchet.fs
- **Required tests:** mutation control per obligation family
- **Prevents (prevention cons/central/opt):** FM-13 Tests that cannot fail, are never written, or over-claim what they prove (0.3/0.5/0.65)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 15/10/6
  - pre-competition cost: 0 min
  - implementation cost: 10-15 min per slice in competition
- **Confidence:** low. Writing a falsifying control for every bound test costs 6-15 min of critical path per build; modeled benefit only exceeds it in the optimistic case.
- **Owner:** each implementing role

## CV-14 Reconcile requirements with owner decisions before freeze

- **Level / tier / status:** MANDATORY / 1 / PROPOSED
- **Problem:** Clauses contradicting DEC-025/OD-INDY-10 and INV-04 vs SM-SAVE stayed in frozen packages and produced FAILs.
- **Historical evidence:** HF-023, HF-036 (FAILURE-DATA.json)
- **Scope:** requirements packages
- **Required pattern:** Every owner decision that names a requirement is either applied as an amendment in the successor package or recorded as a waiver against that requirement before freeze.
- **Prohibited:** freezing a package while a decision contradicts a clause
- **Enforcement:** review at package release
- **Review obligation:** Package reviewer lists each owner decision naming a requirement and its amendment or waiver.
- **Required tests:** review checklist entry in the package independent review
- **Prevents (prevention cons/central/opt):** FM-14 Requirement clauses contradict owner decisions or each other (0.5/0.7/0.85)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 0/0/0
  - pre-competition cost: 30 min
  - implementation cost: about 30 min per package
- **Confidence:** medium. Two measured contradictions produced FAILs (R5, R8); fix is pre-competition review only.
- **Owner:** Indy-init owner (package release)

## CV-15 Evidence is bound to the candidate SHA

- **Level / tier / status:** MANDATORY / 1 / ENFORCED-EXISTING / **safety or correctness rule: enforce it whatever its time value**
- **Problem:** Reviews and closures cited earlier commits (R4-R7); R6 re-cut a candidate (20 min); cross-session claims could not be verified.
- **Historical evidence:** HF-005, HF-016, HF-024, HF-025, HF-049, HF-051, HF-078 (FAILURE-DATA.json)
- **Scope:** acceptance
- **Required pattern:** Completion is only completion-gate COMPLETE on the candidate SHA; every review, run and record names that SHA; a claim from another session counts only after the lead reproduces it.
- **Prohibited:** carrying a review forward to a later commit
- **Enforcement:** existing at slice merge and acceptance: slice-gate, completion-gate, catalog-acceptance
- **Check command:** `requirements-compiler completion-gate`
- **Existing capability used:** AGENTS.md (Rehearsal 010 governed verification)
- **Required tests:** existing adversarial.fsx cases
- **Prevents (prevention cons/central/opt):** FM-15 Evidence or completion claims not bound to the final candidate (0.5/0.7/0.85)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 2/1/1
  - pre-competition cost: 0 min
  - implementation cost: none (exists)
- **Confidence:** high. Recurred R2-R9; completion-gate already enforces; value shown is what it secures.
- **Owner:** run coordinator

## CV-16 Only the owner records approvals

- **Level / tier / status:** MANDATORY / 1 / ENFORCED-EXISTING / **safety or correctness rule: enforce it whatever its time value**
- **Problem:** Agents wrote or transcribed approvals in R3-R6; the R8 verdict was INVALID; DA-R10-1 certified itself.
- **Historical evidence:** HF-003, HF-050, HF-077 (FAILURE-DATA.json)
- **Scope:** approval records
- **Required pattern:** Approval records are written only by tools/owner-freeze.fsx under the owner's signature; agents never write, edit or transcribe them (RDP-003).
- **Prohibited:** agent-authored approvals.causa.json; self-certifying authority records
- **Enforcement:** existing at launch preflight and acceptance: launch-preflight unsigned-change checks; owner-freeze.fsx
- **Check command:** `tools/r10/launch-preflight.fsx`
- **Existing capability used:** AGENTS.md
- **Required tests:** launch-preflight-tests.fsx controls
- **Prevents (prevention cons/central/opt):** FM-16 Approval records written or self-certified by agents; verdict not computable (0.7/0.85/0.95)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 0/0/0
  - pre-competition cost: 0 min
  - implementation cost: none (exists)
- **Confidence:** high. Safety rule; recurred R3-R8; enforced by RDP-003 and owner-freeze.fsx.
- **Owner:** owner

## CV-17 Forma first: current Forma, no containment workarounds

- **Level / tier / status:** MANDATORY / 1 / IMPLEMENTED-NOT-ADOPTED
- **Problem:** Forma 0.12.0 fails 48 of 78 containment checks (fixed upstream in 0.13.0); R5, R9 and the preview app re-implemented or worked around Forma in application CSS.
- **Historical evidence:** HF-019, HF-056, HF-080, HF-081, HF-082, HF-084, HF-085, HF-087 (FAILURE-DATA.json)
- **Scope:** Causa web CSS and package.json
- **Required pattern:** Pin Forma at or above 0.13.0; lay out with ef-* patterns named in the layout contract; application CSS declares no --ef-* properties and adds no overflow, radius or validation-state overrides; a Forma gap is filed upstream with a failing fixture.
- **Prohibited:** Forma below 0.13.0; overflow-wrap/border-radius/[aria-invalid] overrides in application CSS
- **Allowed exceptions:** remedies recorded in the layout contract (e.g. --ef-layout-target-min links)
- **Enforcement:** new at bootstrap / every merge (seconds): conventions.fsx check CK-04 (Forma version) + design-contract check + layout-tests (existing)
- **Check command:** `dotnet fsi tools/conventions/conventions.fsx -- check --workspace . --forma-min 0.13.0`
- **Existing capability used:** kickoff/rehearsal-010/design-contract.json; requirements-compiled/v4.1.5/layout-tests
- **Required tests:** conventions.fsx test: Forma 0.12.0 fails CK-04; 0.13.0 passes
- **Prevents (prevention cons/central/opt):** FM-17 UI content escapes containers; application CSS re-implements or works around Forma (0.3/0.5/0.7); FM-19 Non-text contrast below threshold for status colours (0.2/0.4/0.6)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 2/1/0.5
  - pre-competition cost: 20 min
  - implementation cost: CK-04 implemented in this change
- **Confidence:** medium. Containment defects are measured (48/78 checks) and fixed upstream; minutes lost to UI rework were never recorded. Requires owner approval of the Forma 0.13.0 pin.
- **Owner:** ui (UI-1); **approval required:** owner: pin Forma 0.13.0 (v4.1.5 references the 0.12.0 inventory; ODR-R10-05 allows 0.12.x/0.13.x)

## CV-18 Editable fields bind on input, through the field map

- **Level / tier / status:** PREFERRED / 1 / PROPOSED
- **Problem:** A bound field without data-on=input lost typed text twice in R3; the R4 regression test could not fail.
- **Historical evidence:** HF-004 (FAILURE-DATA.json)
- **Scope:** every editable field
- **Required pattern:** Each editable field is declared once in the screen manifest and F# field map with an input-event binding; ManifestTests compare both.
- **Prohibited:** binding editable fields only on change/blur
- **Enforcement:** existing at contract freeze: proposals/r10-contract-first-ui/tests/ManifestTests.fs
- **Existing capability used:** proposals/r10-contract-first-ui
- **Required tests:** negative control: removing the input binding fails the test
- **Prevents (prevention cons/central/opt):** FM-18 Editable field binding loses typed input (0.5/0.7/0.85)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 1/0.5/0.5
  - pre-competition cost: 0 min
  - implementation cost: none if contract-first UI is adopted
- **Confidence:** medium. One rehearsal (R3, twice) plus an R4 test that could not fail; cheap if contract-first UI (CF-09 A) is adopted.
- **Dependencies:** CF-09 A (APR-CFUI-OWNER-1)
- **Owner:** ui (UI-1)

## CV-19 Test-only code never ships

- **Level / tier / status:** MANDATORY / 1 / PROPOSED / **safety or correctness rule: enforce it whatever its time value**
- **Problem:** A live token reached Playwright traces (R4); production exposed EnableTestHooks (R5); test-only code reached production (R6).
- **Historical evidence:** HF-010 (FAILURE-DATA.json)
- **Scope:** build configuration
- **Required pattern:** Test hooks compile only under a Test configuration; traces and logs redact Authorization headers.
- **Prohibited:** runtime flags that enable test hooks in Release
- **Enforcement:** review at candidate
- **Review obligation:** Reviewer confirms the candidate bundle contains no test hook symbol and traces are redacted.
- **Required tests:** bundle scan for hook names on the candidate
- **Prevents (prevention cons/central/opt):** FM-20 Test hooks or secrets reach production artifacts (0.5/0.7/0.85)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 1/0.5/0.5
  - pre-competition cost: 10 min
  - implementation cost: about 10 min
- **Confidence:** high. Safety rule; R4-R6 leaks; time value is near zero, enforced regardless.
- **Owner:** integration

## CV-20 Schedules read the frozen ownership; gates are acyclic

- **Level / tier / status:** PREFERRED / 2 / ENFORCED-EXISTING
- **Problem:** The schedule model ignored file ownership (T+519 vs T+720); the slice-gate journeys rule deadlocked the plan.
- **Historical evidence:** HF-066, HF-067 (FAILURE-DATA.json)
- **Scope:** planning tools
- **Required pattern:** The schedule model takes lanes from agent-file-ownership.json and the slice gate's merge dependencies form a DAG; both are tested before launch.
- **Enforcement:** existing at pre-launch: tools/r10/schedule-model-tests.fsx; ODR-R10-33 deferral
- **Existing capability used:** tools/r10/schedule-model.fsx
- **Required tests:** cycle detection on merge-dependency edges
- **Prevents (prevention cons/central/opt):** FM-21 Schedule or gate design contradicts ownership or deadlocks (0.5/0.7/0.85)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 0/0/0
  - pre-competition cost: 30 min
  - implementation cost: exists
- **Confidence:** medium. Defects found in the model and gate before any run (modeled minutes); existing tests guard them.
- **Owner:** planning

## CV-21 Everything mandated is in the dependency lock

- **Level / tier / status:** PREFERRED / 1 / PROPOSED
- **Problem:** Central package management dropped FSharp.Core and skipped every test assembly (R8); a mandated layout engine was outside the lock.
- **Historical evidence:** HF-053, HF-055 (FAILURE-DATA.json)
- **Scope:** dependency lock and bootstrap
- **Required pattern:** Every package a requirement mandates, and FSharp.Core, is listed in the lock and in Directory.Packages.props; a test run that skips an assembly fails.
- **Prohibited:** implicit FSharp.Core under central package management
- **Enforcement:** review at package release
- **Review obligation:** Package reviewer lists every package named by a requirement and its lock entry.
- **Required tests:** bootstrap test run fails when any test assembly is skipped
- **Prevents (prevention cons/central/opt):** FM-22 Required dependency missing from the lock or silently dropped (0.4/0.6/0.8)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 0/0/0
  - pre-competition cost: 20 min
  - implementation cost: about 20 min
- **Confidence:** medium. Two R8 occurrences (FSharp.Core, forma-workflow); minutes unrecorded.
- **Dependencies:** CV-01
- **Owner:** Indy-init owner (package release)

## CV-22 Standard per-feature F# file split (Contract/Domain/State/View/Tests)

- **Level / tier / status:** DISCRETIONARY / 3 / DEFERRED
- **Problem:** Proposed in the directive; no finding attributes a defect or conflict to module layout, and ownership is already fixed per file.
- **Historical evidence:** HF-022 (FAILURE-DATA.json)
- **Scope:** F# application code
- **Required pattern:** Agents may organise modules as they choose inside their owned files; the frozen project structure decides file boundaries.
- **Enforcement:** none at n/a
- **Prevents (prevention cons/central/opt):** FM-10 State machine bypassed, faults swallowed, unknown input silently defaulted (0.05/0.1/0.15)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 5/3/1
  - pre-competition cost: 0 min
  - implementation cost: would require changing frozen project structure
- **Confidence:** low. No evidence attributes a defect to module layout; costs are a frozen-structure change.
- **Owner:** n/a

## CV-23 Stable requirement and field IDs, decoupled from F# names

- **Level / tier / status:** PREFERRED / 2 / PROPOSED
- **Problem:** Field-mapping drift between UI and contracts; IDs module exists in the contract-first UI proposal.
- **Historical evidence:** HF-004, HF-073 (FAILURE-DATA.json)
- **Scope:** UI contracts
- **Required pattern:** Fields and actions use UIF-/UIA-/INV- ids from the screen manifest; HTML ids and F# names map through one Ids module.
- **Prohibited:** deriving HTML ids from F# parameter names
- **Enforcement:** existing at contract freeze: proposals/r10-contract-first-ui/contracts/Ids.fs + ManifestTests
- **Existing capability used:** proposals/r10-contract-first-ui
- **Prevents (prevention cons/central/opt):** FM-18 Editable field binding loses typed input (0.1/0.2/0.3)
- **Cost:**
  - in-competition overhead (min, cons/central/opt): 1/0.5/0.5
  - pre-competition cost: 0 min
  - implementation cost: none if contract-first UI is adopted
- **Confidence:** low. Supports CV-18; benefit is mostly in CV-18 already.
- **Dependencies:** CF-09 A
- **Owner:** ui (UI-1)
