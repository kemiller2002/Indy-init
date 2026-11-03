# Rehearsal 009: final readiness corrections (2026-11-03)

This corrects the six risks in [REPORT.md](REPORT.md), the historical smoke-test record. REPORT.md is preserved unchanged.

**Decision: BLOCKED, on one owner action. Everything else is READY.**
- All six corrections and both owner decisions of 2026-11-03 are implemented and verified: 76 of 76 cases pass on the released binaries.
- The one remaining gate is a trust input only the owner may set. The Rehearsal 009 process-evidence rules (`kickoff/rehearsal-009/process-evidence-rules.json`) are drafted, but their hash is not yet pinned (§Owner action).
  - Until they are pinned, record-bound obligations cannot be verified: the record gate fails closed.
  - Those are 119 obligations (72 record-only and 47 mixed), covering 26 record-only requirements.
  - Once pinned, the verdict is READY WITH RISKS. The remaining risks are in §Remaining limitations.
- Workstation registration is UNAVAILABLE. It is accepted as a limitation and is not counted as a registration (§6).
- Rehearsal 009 was not started.

## What changed

All of it is in Indy-init. No framework release was needed, and nothing in Praxis, Dokimos, Conditor or Percepta was modified.

| File | Purpose |
|---|---|
| `kickoff/rehearsal-009/toolchain.lock.json` | The one toolchain every agent starts from. It pins Conditor 0.10.19 (tag, commit `31725a78…`, per-platform SHA-256), the echelon-current 1.35.0 resolved set (registry commit `ae03cff6…`, per-platform SHA-256), package v4.1.2 (manifest digest, tag, approval) and the workstation target id. |
| `tools/r9/governance.json` | The authoritative Praxis completion policy: `requiredFacets: ["verification-matrix-satisfied"]`, with no `workTypes`. |
| `tools/r9/verification-gate.yml` | The gate workflow that `govern` writes into the competition repository. |
| `tools/r9/r9.fsx` | The commands `install-conditor`, `workstation`, `govern`, `check-governance`, `run-suites`, `gate` and `spec-preflight` (F#, per OD-INDY-14). |
| `tools/r9/tests.fsx` | 76 end-to-end positive and negative cases on the released binaries (the last three cover the content fault-sensitivity rule of docs/rehearsal-009-content-readiness). |
| `kickoff/rehearsal-009/process-evidence-rules.json` | PROPOSED rules for `verify-process`: owner, implementing agents, independent reviewer roster, repository and owner-decision ids, drafted from Rehearsal 6's. The lock references it with `rulesSha256: null` until the owner pins it. |
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
- **Record evidence:** Dokimos never judges record identities; it reports them as `blocked`, by design. The gate therefore runs Dokimos on the executable view of the frozen plan, requires exit 0 there, and judges record evidence with the existing record verifier (§7).
- **On the real plan:** 613 obligations: 541 bind executable identities and 119 bind record identities, so 47 are mixed (`gate-release-real-v412-plan-blank-refused`: 0/541 and 0/119 verified, exit 4; nothing deferred, nothing counted).
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

### 6. Workstation registration (P1): installation CORRECTED; registration UNAVAILABLE, accepted (owner decision 1)
- **Fixed:**
  - `r9.fsx workstation` always passes `--target-id` (default `indy-init-r9-workstation`, a logical id, not a host identity), which removes `invalid-target`.
  - It reads registration back from Conditor's ledger and checks every tool identity. A repeat run reuses every receipt (`workstation-fresh-home`, `workstation-repeat-idempotent`).
- **Owner decision 1 (2026-11-03): registration is UNAVAILABLE for Rehearsal 009.**
  - Project Administration is not built from source, its repository is not modified, and no rehearsal record is written to the production inventory.
  - `workstation` reports `installation: VERIFIED` and, separately, `registration: UNAVAILABLE (6 of 6 components not registered: no Project Administration integration; accepted limitation for Rehearsal 009, not a registration)`.
  - Installation, the pinned resolved set, the checksums and the tool identities remain mandatory and fail the command if wrong.
  - `invalid`, `refused` or `misconfigured` registration still fails it.

### 7. Record-only and mixed obligations: CORRECTED with the existing contracts (owner decision 2)
- **Evidence type comes only from the authoritative plan:** the runner of the suite each identity is bound in.
  - `dotnet-test` and `playwright` identities are **executable**, judged by Dokimos.
  - `evidence-record` identities named `praxis:<kind>` are **record**, judged by `requirements-compiler verify-process`.
  - `evidence-record` identities named `verifier:*` are **independent-verifier**, judged only by the acceptance verifier.

  Any other runner, or an obligation with no bound identity, is refused; its type is never inferred (`gate-unclassified-evidence-type-refused`). A missing test never makes an obligation record-only (`gate-executable-missing-test-never-record`).
- **Rules:**
  - Executable obligations need Dokimos `verified` at HEAD.
  - Record obligations need every category `verify-process` decides for them to be `verified` at the same HEAD.
  - Mixed obligations need both.
  - A work item is record-only only when the plan classifies every in-scope obligation as record. It then completes on the record verdict alone.
  - Work items keep their own identities and requirement scopes: nothing is merged to fill a matrix.
- **Record verifier, existing and unchanged:** `verify-process` (docs/PROCESS-EVIDENCE-AND-READINESS.md) judges:
  - **provenance:** the Praxis provenance class, and the execution envelope's actor;
  - **authority:** the frozen roster of owners, agents and reviewers, and owner decisions only for the decisions they name;
  - **independence:** a frozen reviewer who implemented nothing, anchored by a CI-observed review;
  - **candidate binding:** records name this candidate; commits are this candidate or its ancestors.

  Attestations count only from the CI run that observed them (`review-attestations`, `--trusted-run-id`). The pinned plan is the frozen v4 plan (digest `1e3c66bc…`); the rules are pinned by hash. On the real plan it decides exactly the 119 record-bound obligations (`gate-release-real-v412-plan-blank-refused`).
- **Where records are judged:**
  - At work-item completion and at release, by the gate workflow's `workflow_dispatch` (inputs `work_item`, `scope`, `mode`). It observes reviews in its own run, then runs `review-attestations`, then `gate` with the record verifier.
  - A development merge re-checks executable obligations only, and says so. A review of an earlier commit is correctly stale at a later HEAD, so re-judging records on every merge would always fail (`gate-development-merge-does-not-rejudge-records`).
- **Matrix for Praxis:**
  - **Mixed item:** Dokimos's rows also carry the record verdict (command evidence naming `verify-process --candidate HEAD`). A row is `met` only when both hold.
  - **Record-only item:** the gate writes the rows from the record verdict, with command and location evidence. Praxis 3.24.0 accepts it (`praxis-record-only-item-completes`).
  - `check-governance` still ties every completion to a PASS gate verdict.
- **The acceptance verifier is not weakened:** `compute-acceptance-verdict.py`, `verify-plan` and `verify-process` are unchanged.
- **Tests** (each against the frozen v4 plan, on the Praxis log recorded by `tools/process-evidence/simulate-rehearsal.sh`, relabelled to the synthetic candidate; mutations touch only the agent-recorded log, never the rules or the CI attestations):

| Case | Expected | Actual |
|---|---|---|
| Record-only item, valid records (`gate-record-only-valid-records`) | PASS; Praxis completes; governance holds | exit 0, `record: 1/1`; `praxis-record-only-item-completes` 0; `check-governance-after-record-only-completion` 0 |
| Missing review record (`gate-record-missing-review-refused`) | refused | exit 4, `MISSING-INDEPENDENT-REVIEW` |
| Invalid authority: reviewer not on the roster (`gate-record-invalid-authority-refused`) | refused | exit 4, `INCORRECT-AGENT-IDENTITY`, `SELF-ACCEPTANCE` |
| Rules not matching the pin (`gate-record-unpinned-rules-refused`) | refused | exit 4, verify-process INVALID |
| Wrong candidate SHA: records of another commit (`gate-record-wrong-candidate-refused`) | refused | exit 4, `STALE-REVIEW` |
| No pinned record-verifier inputs (`gate-record-without-verifier-inputs-refused`) | refused | exit 4 |
| Mixed item, records valid, executable test missing (`gate-mixed-missing-executable-refused`) | refused | exit 4, `not-generated` |
| Mixed item, executable passed, review missing (`gate-mixed-missing-record-refused`) | refused | exit 4, `MISSING-INDEPENDENT-REVIEW` |
| Mixed item, both present (`gate-mixed-both-verified`) | PASS | exit 0, executable 1/1, record 1/1 |
| Executable-only obligation whose test is missing (`gate-executable-missing-test-never-record`) | refused, never treated as record | exit 4, `not-generated` |
| Identity with an unclassified runner (`gate-unclassified-evidence-type-refused`) | refused | exit 4 |

- **Independent-verifier evidence:** two mixed obligations, PERSIST-052-O3 and PERSIST-026-O2, also bind `verifier:*` evidence, which the post-deployment independent verifier job produces.
  - The gate refuses to claim them at work-item time.
  - At release it lists them for the acceptance verifier (`verify-plan`). They are never counted as verified.

## Owner action required before T0

**Confirm the Rehearsal 009 process-evidence rules and pin them.**
- `kickoff/rehearsal-009/process-evidence-rules.json` is an agent's draft from the Rehearsal 6 rules. Its current sha256 is `b8adffc4122226cbe568dae1b0173ee85aab08b8435ded7390f9d5205ad0d115`.
- It names:
  - the owner, `kemiller2002`;
  - the implementing agents;
  - the independent reviewer roster (Rehearsal 6's: `anthropic/claude-code-reviewer`, login `kemiller2002`);
  - the repository, `indy-init-rehearsal-009`;
  - the decision proving AR-024-O1/O2 and AR-008-O2, `DEC-R9-KICKOFF`.
- If it is right, the owner sets `processEvidence.rulesSha256` in `kickoff/rehearsal-009/toolchain.lock.json` to that hash, in the owner's own commit. Otherwise the owner corrects the file first.
- Agents never pin it. While the hash is `null`, the gate workflow refuses every work-item or release check that has record-bound obligations, naming the missing pin.

## Owner decisions applied (2026-11-03)
1. **Registration:** UNAVAILABLE is accepted for Rehearsal 009, as reported in §6. Nothing was built from source, and no production inventory was written.
2. **Record-only requirements:** obligation-type-aware verification is applied with the existing Praxis and acceptance contracts (§7). No new framework was needed; the only new input is the rules pin above.

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
| Workstation registration | installed; registration UNAVAILABLE, accepted | installation VERIFIED, receipts idempotent; `registration: UNAVAILABLE` reported, not counted as registered (`workstation-fresh-home`) |
| Record-only item | completes on valid records only | PASS (`gate-record-only-valid-records`, `praxis-record-only-item-completes`; 5 record negatives refused) |
| Mixed item, executable evidence missing | refused | PASS (`gate-mixed-missing-executable-refused`) |

- **Local run:** `dotnet fsi tools/r9/tests.fsx -- --indy-init . --bin $HOME_DIR/.local/bin --conditor $CONDITOR --work $WORK --evidence $EVIDENCE` gave 76 cases, 0 failed (`corrections/cases.tsv`).
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

# Completing a work item (every item, executable, record-only or mixed): run the gate in CI, where record evidence is
# observed in the run itself
gh workflow run "Verification gate" -f mode=development -f work_item=WI-ID -f scope=REQ-A,REQ-B
# download its artifact verification-<SHA>/gate into .echelon/verification/WI-ID, then:
praxis work complete --id WI-ID --occurred-at NOW --evidence implementation=… --evidence tests=… \
  --evidence verification-matrix=.echelon/verification/WI-ID/praxis-matrix.json
# commit .echelon/verification/WI-ID and the REQ ids added to .echelon/verification-scope.json
# (executable-only items may also run the same gate locally: r9.fsx run-suites, then r9.fsx gate --work-item ...)

# Release acceptance (T34): workflow_dispatch of "Verification gate" with mode=release at the candidate (executable
# and record evidence), then the authoritative acceptance verifier (verify-plan) for the two independent-verifier
# obligations and the final verdict.
```

`OBLIGATIONS` is the output of `dokimos verification compile --indy-execution-plan $INDY/requirements-compiled/v4.1.2/verification-execution-plan.json --indy-verification-plan $INDY/requirements-compiled/v4/verification-plan.json --out OBLIGATIONS`.

## Remaining limitations
- **Registration is UNAVAILABLE** for Rehearsal 009 (owner decision 1). Installation is verified; the inventory records nothing.
- **The process-evidence rules pin** is the owner action above. Until it is set, record-bound obligations cannot be verified.
- **Record-gate CI not run against a live repository.** The work-item and release record path in the gate workflow (observe reviews, `review-attestations`, `verify-process`) reuses the Rehearsal 6 acceptance job's steps. It has not run against a live competition repository here. Its logic was proven with the recorded Praxis log, and the workflow passes Conditor's CI policy.
- **Two `verifier:*` obligations** (PERSIST-052-O3, PERSIST-026-O2) can complete only at acceptance, not at their work items.
- **Live suites at release** need their provider secrets in the gate job. Without them, their obligations are refused as having no execution evidence; they never pass.
- **The T0 sequence was not run end to end here.** `conditor init --preset indy-init` needs authenticated GitHub fetches this sandbox lacks. `govern`, `check-governance` and `ci-policy check` were proven on a copy of Rehearsal 008's Conditor-initialized repository instead.
- **Local completion is not blocked:** a forged or locally edited completion is stopped at CI (it cannot merge), not at `praxis work complete`.
