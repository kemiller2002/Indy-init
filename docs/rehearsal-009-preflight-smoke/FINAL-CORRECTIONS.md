# Rehearsal 009: final readiness corrections (2026-11-03)

This corrects the six risks in [REPORT.md](REPORT.md), the historical smoke-test record. REPORT.md is preserved unchanged.

**Decision: READY WITH RISKS.**
- All four P0 corrections and the candidate-SHA correction are implemented and verified: 57 of 57 cases pass on the released binaries.
- Workstation registration is fixed only as far as it can go without an owner decision (§6).
- Two owner decisions remain open (§Owner decisions). Neither blocks the start of Rehearsal 009.
- Rehearsal 009 was not started.

## What changed

All of it is in Indy-init. No framework release was needed, and nothing in Praxis, Dokimos, Conditor or Percepta was modified.

| File | Purpose |
|---|---|
| `kickoff/rehearsal-009/toolchain.lock.json` | The one toolchain every agent starts from. It pins Conditor 0.10.19 (tag, commit `31725a78…`, per-platform SHA-256), the echelon-current 1.35.0 resolved set (registry commit `ae03cff6…`, per-platform SHA-256), package v4.1.2 (manifest digest, tag, approval) and the workstation target id. |
| `tools/r9/governance.json` | The authoritative Praxis completion policy: `requiredFacets: ["verification-matrix-satisfied"]`, with no `workTypes`. |
| `tools/r9/verification-gate.yml` | The gate workflow that `govern` writes into the competition repository. |
| `tools/r9/r9.fsx` | The commands `install-conditor`, `workstation`, `govern`, `check-governance`, `run-suites`, `gate` and `spec-preflight` (F#, per OD-INDY-14). |
| `tools/r9/tests.fsx` | 57 end-to-end positive and negative cases on the released binaries. |
| `.github/workflows/preflight-tooling.yml` | New job `r9-readiness-gates`. It installs the pinned toolchain into a disposable home and runs `tests.fsx` on every push. |

The frozen v4.1.2 package is untouched. T10 gains one step, `r9.fsx govern`, between the package's `--bootstrap .` and the build. It writes only into the new competition repository.

## The six risks

### 1. Praxis verification not mandatory (P0): CORRECTED by configuration
- **Already correct:** Praxis 3.24.0 supports `qualityEvidence.requiredFacets`. With `workTypes` omitted, it gates every work type, so `--type research` or `--type mechanical` cannot evade it.
- **Gap:** neither `praxis init` nor `conditor init` writes the policy. Rehearsal 008's `ros.json` has none.
- **Correction:**
  - `r9.fsx govern` writes the authoritative block into `ros.json` at T0.
  - It records the items already complete (Praxis's own install record), so history is never rewritten.
  - It writes the gate workflow and `.echelon/r9-governance.json`, which holds the Indy-init commit and the digests.
  - `r9.fsx check-governance` runs first in the gate workflow on every push and PR. It fails when:
    - the policy or the workflow differs from Indy-init;
    - an item completed after governance has no completion-readiness record, ran under a policy without the facet, or has an unsatisfied matrix;
    - an item's consumed matrix digest matches no committed PASS gate verdict;
    - the development scope shrinks;
    - a completed item's requirements are missing from the development scope.
- **Existing incomplete items, parallel agents, integration, persistence, UI, provider, security and release work:** all are gated identically, because the policy is repository-wide and is evaluated at `work complete`, whatever the item's type or risk metadata.
- **Exception, explicit and narrow:** Praxis's own lifecycle records (`praxis init` and `upgrade`) are not completed through `work complete`, so the policy does not apply to them. No work type is exempt.
- **Tests** (`corrections/cases/`):

| Case | Expected | Actual |
|---|---|---|
| Valid matrix (`praxis-valid-matrix-completes`), then `check-governance` | complete 0, governance 0 | 0, 0 |
| Missing matrix (`praxis-missing-matrix-refused`) | 3 | 3 ("no verification-matrix evidence was supplied") |
| Failed matrix (`praxis-failed-matrix-refused`) | 3 | 3 ("happy-path is not met" …) |
| Incorrect obligation identity: forged matrix (`gate-refuses-incorrect-obligation-identity`, `check-governance-blocks-forged-identity`) | gate 4; CI 4 | 4 ("not a bound identity"); 4 ("matches no PASS gate verdict") |
| Matrix of another item (`praxis-refuses-matrix-of-another-item`) | 3 | 3: Praxis binds a matrix to its work item |
| Missing risk metadata, `--type research` (`praxis-no-risk-metadata-research-refused`) | 3 | 3 |
| Facet removed and committed (`praxis-completes-after-facet-removal`, `check-governance-blocks-facet-removal`) | Praxis 0; CI 4 | 0; 4 (policy differs, and the item completed without the facet) |

- **Limitation:** Praxis cannot stop an agent from editing `ros.json` locally, and it accepts a hand-forged matrix that names the right work item. Both cases complete locally and are then refused by CI's `check-governance`, so they cannot reach `main`.
- **The authoritative guarantee** is the release gate (§2). It recomputes coverage from scratch at the release candidate and trusts no earlier matrix.

### 2. Dokimos coverage not enforced in CI (P0): CORRECTED
- **Gate workflow, in order:**
  1. The candidate is the checked-out commit: the PR head or the push SHA.
  2. `check-governance`.
  3. The pinned toolchain.
  4. `dokimos verification compile` of the frozen v4.1.2 plan.
  5. `run-suites` runs each suite's own plan command, adding only a TRX or Playwright JSON logger, and records every run at HEAD.
  6. `gate` runs `dokimos verification coverage … --candidate HEAD --require-verified true` (Dokimos 0.10.0 syntax, checked against its usage text).
  7. The coverage report, results, verdict and Praxis matrix are uploaded as an artifact.
- **Matrix handoff to Praxis:** `gate --work-item ID --scope REQ,… --out .echelon/verification/ID` writes the matrix, and `praxis work complete --evidence verification-matrix=…` consumes it.
- **Development scope:**
  - Scope is `.echelon/verification-scope.json`. It may only grow, and every completed item's requirements must be in it.
  - An empty scope (T0) passes and explicitly claims nothing.
  - Only the deterministic suites run.
- **Release scope:** the whole frozen plan, all suites. The scope is not reduced.
- **Record evidence:** Dokimos never judges record evidence (`praxis:*`, `verifier:*`; it reports such identities as `blocked`, by design). The gate therefore runs Dokimos on the executable view of the frozen plan, with record identities removed. It requires exit 0 on that view and lists the 72 record-only obligations as deferred to the acceptance verifier, never as verified.
- **On the real plan:** 613 obligations become 541 executable plus 72 deferred (`gate-release-real-v412-plan-blank-refused`: 0/541 verified, exit 4).
- **Negative cases** (all exit 4): missing test, skipped test, failing test, placeholder test, mismatched identity, stale candidate SHA, stale matrix, missing execution evidence (no run, and a missing TRX), dirty tree, and a failure inside the development scope.
- **Positive case:** a passing, correctly bound test (`gate-pass-correctly-bound`): 1/1 verified, Dokimos exit 0.
- **Conditor CI policy:** `conditor ci-policy check` on a copy of Rehearsal 008's Conditor-initialized repository passes before and after `govern` (9 workflows). Before the fix it failed CIP-002, which led me to add the workflow's concurrency group (`corrections/cases/conditor-ci-policy-*.log`).

### 3. Conditor not pinned (P0): CORRECTED
- **Pin:** Conditor is the installer and is not in the resolved set it installs, so I did not add a registry field. `kickoff/rehearsal-009/toolchain.lock.json` is the bootstrap lock.
- **Installer:** `r9.fsx install-conditor` downloads the release asset for the platform and refuses unless both hold:
  - its SHA-256 equals the lock;
  - the binary carries `0.10.19+31725a78cc74a64d51084250a1c49cbabcba3646`.

  There is no fallback and no latest-version lookup.
- **Clean-home test** (`conditor-install-pinned`, `conditor-identity-0.10.19-not-0.10.18`, `conditor-install-digest-mismatch-refused`):
  - sha256 `cd6b87d3…41cc` equals the lock and the release `checksums.txt`;
  - 0.10.18 is absent from the binary;
  - a tampered lock is refused, and no binary is written.
- **References to 0.10.18:** none in the active Rehearsal 009 path selects it. `tools/measure-t0-bootstrap.fsx` takes tools from PATH and measures v4.1.1. `tests/fixtures/conditor-0.10.18-scaffold` is a recorded historical fixture. Both are left as they are, as instructed.

### 4. Percepta passes with no input (P0): CORRECTED in an Indy-init wrapper
- **CLI left as it is:** the general Percepta CLI legitimately accepts an empty specification.
- **Wrapper:** `r9.fsx spec-preflight` resolves the package from the lock and verifies its manifest digest (`1977780f…`). It refuses when:
  - an input is missing or empty;
  - an input is valid but holds nothing;
  - Percepta exits non-zero or reports blocked;
  - any blocking finding remains;
  - any analysed count differs from the count derived independently from the pinned inputs.
- **Report:** `indy-init.r9-spec-preflight/1`, which names the package and each input's digest.
- **Test results:**

| Case | Expected | Actual |
|---|---|---|
| v4.1.2 frozen package | PASS: 5 machines, 101 transitions, 25 invariants, 26 operations, 134 commands (counted distinct, as Percepta counts them), 85 types | PASS, exit 0 |
| Missing input path / no inputs / empty files / well-formed but empty | 4 | 4 |
| Contradictory invariant | 4 | 4 (`rate-bound-vs-unbounded-loop`) |
| Incomplete command carrier | 4 | 4 (`carrier-cannot-discriminate`) |
| Sound synthetic control | 0 | 0 |

### 5. Candidate SHA not cross-checked (P1): CORRECTED at the integration boundary
- **Trusted candidate:** the repository's HEAD, read by the gate itself. CI's `--expect-candidate` must equal it, and the tracked tree must be clean.
- **What must equal the candidate:**
  - the results manifest's `candidate`;
  - every run's `sourceSha`;
  - the coverage report's `candidate`;
  - the Dokimos command evidence inside the matrix.
- **What must agree with Dokimos:** every test cited in the matrix must be a bound in-scope identity with the same status Dokimos reports at HEAD.
- **Verdict:** each gate verdict records the candidate and the digests of the coverage, results and matrix. `check-governance` ties every completion to a PASS verdict.
- **Tests, all refused (exit 4):** previous-commit SHA, malformed SHA, missing SHA, evidence from another candidate, stale results, stale matrix.
- **Not changed:** the acceptance manifest is checked by the existing authoritative verifier (`compute-acceptance-verdict.py`, its `source_commit_sha` and trusted SHA), which is unchanged.

### 6. Workstation registration (P1): PARTIALLY CORRECTED; needs an owner decision
- **Fixed:**
  - `r9.fsx workstation` always passes `--target-id` (default `indy-init-r9-workstation`, a logical id, not a host identity), which removes `invalid-target`.
  - It reads registration back from Conditor's ledger and checks every tool identity. A repeat run reuses every receipt (`workstation-fresh-home`, `workstation-repeat-idempotent`).
- **Still blocked:** registration now reports `unavailable: no Project Administration integration is configured`.
  - Praxis registers into Project Administration, which has no release, so a local store would need a source build.
  - The GitHub-workflow transport would write rehearsal workstations into the real `kemiller2002/project-administration` inventory.
  - Registration is optional in Praxis's contract (exit 0). `workstation` fails on `invalid`, `refused` or `misconfigured`, and fails on `unavailable` only with `--require-registration true`.

## Owner decisions

1. **Registration target (risk 6).** Choose:
   - the GitHub-workflow transport into `kemiller2002/project-administration`;
   - a released `administration` executable;
   - accept registration as `unavailable` for Rehearsal 009.

   This does not block T0.
2. **Record-only obligations and work items.**
   - 72 obligations (45 of 161 requirements touch record evidence, mostly `praxis:independent-review`) are verified by the acceptance verifier, not by Dokimos.
   - A work item whose scope holds only record-only requirements cannot get a non-empty matrix, so the gate refuses it.
   - Confirm that such items (review or approval records) are scoped with the executable requirements they review, or name an explicit exception.

## Erratum in REPORT.md
- REPORT.md cites the pass case's candidate as `0a4922c4…`. That SHA comes from the first run, whose evidence was deleted when the Praxis state was reset.
- The committed evidence (`evidence/case-pass/*`, `c3-case-pass.log`) is internally consistent at `3a45897a07e31aee3543625ea8f4130793cca914` (candidate, results, coverage and matrix).
- The REPORT.md text is left as the historical record; this note supersedes that one SHA.

## Integrated rerun

| Scenario | Expected | Result (case) |
|---|---|---|
| Valid Dokimos evidence | Praxis completes | PASS (`praxis-valid-matrix-completes`) |
| Missing test | refused | PASS (`gate-refuses-missing`; `praxis-missing-matrix-refused`) |
| Skipped test | refused | PASS (`gate-refuses-skip`) |
| Failed test | refused | PASS (`gate-refuses-fail`, `praxis-failed-matrix-refused`) |
| Missing risk metadata | still enforced | PASS (`praxis-no-risk-metadata-research-refused`) |
| Stale SHA | refused | PASS (`gate-refuses-stale-sha-results`, `gate-refuses-stale-matrix`) |
| Invalid approval | Conditor rejects | PASS, re-run with the pinned 0.10.19 on this tree: control READY, exit 0; the synthetic invalid record (disposable clone) INVALID, exit 3, with all four owner actions (`corrections/cases/acceptance-preflight-*.log`) |
| Empty specification | rejected | PASS (`spec-no-inputs`, `spec-empty-input-files`, `spec-well-formed-but-empty`) |
| Valid v4.1.2 specification | passes | PASS (`spec-v412-frozen-package`) |
| Pinned Conditor bootstrap | 0.10.19 installed | PASS (`conditor-install-pinned`) |
| Workstation registration | target registered | target id supplied, receipts idempotent; registration `unavailable` (owner decision 1) |

- **Local run:** `dotnet fsi tools/r9/tests.fsx -- --indy-init . --bin $HOME_DIR/.local/bin --conditor $CONDITOR --work $WORK --evidence $EVIDENCE` gave 57 cases, 0 failed (`corrections/cases.tsv`).
- **Tools:** .NET SDK 10.0.402; Praxis 3.24.0, Dokimos 0.10.0, Percepta 0.4.0 and Ordo 1.20.0 from echelon-current 1.35.0; Conditor 0.10.19.
- **Offline suite:** passes.
- **Actual CI:** the job `r9-readiness-gates` in `preflight-tooling.yml` runs the same cases on GitHub's runner. Its result is recorded in the pull request that delivers this file.

## Commands at Rehearsal 009 T0

`INDY` is an Indy-init checkout at the merge commit of this change. `LOCK=$INDY/kickoff/rehearsal-009/toolchain.lock.json`.

```bash
# Before T0 (workstation and preflight)
dotnet fsi $INDY/tools/r9/r9.fsx -- install-conditor --lock $LOCK --dir ~/.local/share/indy-init-r9/conditor
CONDITOR=~/.local/share/indy-init-r9/conditor/conditor        # always this absolute path, never a conditor on PATH
dotnet fsi $INDY/tools/r9/r9.fsx -- workstation --lock $LOCK --conditor $CONDITOR --home ~ --target-id indy-init-r9-workstation
dotnet fsi $INDY/tools/r9/r9.fsx -- spec-preflight --indy-init $INDY --lock $LOCK --percepta ~/.local/bin/percepta --out ~/r9-preflight/spec.json
$CONDITOR acceptance preflight --target $INDY                    # must read READY

# T0 (T10), in the empty competition repository
git init -b main
$CONDITOR init --preset indy-init --target .
dotnet $INDY/tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll v41 --source $INDY \
  --package $INDY/requirements-compiled/v4.1.2 --base $INDY/requirements-compiled/v4 --bootstrap .
dotnet fsi $INDY/tools/r9/r9.fsx -- govern --indy-init $INDY --target .
dotnet fsi $INDY/tools/r9/r9.fsx -- check-governance --indy-init $INDY --target .
dotnet build Causa.slnx
$CONDITOR ci-policy check --target .
git add -A && git commit -m "T0 bootstrap"
# publish with $CONDITOR repo create (as the package states); first green CI includes the Verification gate

# Completing a work item (every item)
dotnet fsi $INDY/tools/r9/r9.fsx -- run-suites --obligations OBLIGATIONS --root . --out $RUNNER_TEMP/results
dotnet fsi $INDY/tools/r9/r9.fsx -- gate --mode development --scope-file .echelon/verification-scope.json \
  --obligations OBLIGATIONS --root . --results $RUNNER_TEMP/results/results.json --dokimos $(command -v dokimos) \
  --work-item WI-ID --scope REQ-A,REQ-B --out .echelon/verification/WI-ID
praxis work complete --id WI-ID --occurred-at NOW --evidence implementation=… --evidence tests=… \
  --evidence verification-matrix=.echelon/verification/WI-ID/praxis-matrix.json
# commit .echelon/verification/WI-ID and the REQ ids added to .echelon/verification-scope.json

# Release acceptance (T34): workflow_dispatch of "Verification gate" with mode=release at the candidate,
# then the authoritative acceptance verifier for the 72 deferred record obligations.
```

`OBLIGATIONS` is the output of `dokimos verification compile --indy-execution-plan $INDY/requirements-compiled/v4.1.2/verification-execution-plan.json --indy-verification-plan $INDY/requirements-compiled/v4/verification-plan.json --out OBLIGATIONS`.

## Remaining limitations
- **Registration** (owner decision 1).
- **Record-only obligations** (owner decision 2).
- **Live suites at release** need their provider secrets in the gate job. Without them, their obligations are refused as having no execution evidence; they never pass.
- **The T0 sequence was not run end to end here.** `conditor init --preset indy-init` needs authenticated GitHub fetches this sandbox lacks. `govern`, `check-governance` and `ci-policy check` were proven on a copy of Rehearsal 008's Conditor-initialized repository instead.
- **Local completion is not blocked:** a forged or locally edited completion is stopped at CI (it cannot merge), not at `praxis work complete`.
