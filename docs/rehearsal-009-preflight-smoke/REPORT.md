# Rehearsal 009: final preflight smoke test (2026-11-03)

**Decision: READY WITH RISKS.** All eight checks pass on released binaries. Every verification handshake, the approval preflight and version resolution were executed, and none failed. The risks (§Risks) are gaps in how things are pinned or gated, not failed checks. Rehearsal 009 was **not** started, and nothing in Rehearsal 008 was touched. The smoke test awaits authorization.

- Window: 07:19–07:30 UTC (about 11 minutes, inside the 30-minute box).
- Isolation: everything ran in a disposable scratch directory with a sacrificial `--home`. Two inputs were used read-only: the Indy-init checkout at `98bad5f` (main, the same tree as this branch) and the echelon-registry checkout at `ae03cff`. Check 6 edited only a disposable clone. The host's `~/.local`, `~/.profile` and PATH were not modified.
- Evidence: `evidence/` (raw logs, each beginning with `# cmd:` and ending with `# exit:`), `inputs/` (the synthetic requirement, test variants, Percepta inputs and the synthetic approval record) and `drivers/` (the harness, stored as `.txt`; it is not tooling).
- Runner: the .NET SDK 10.0.402 (the `global.json` pin) was installed into the scratch directory with `dotnet-install.sh`, because the container had no SDK. Conditor does not install the SDK; its `fsharp-limen-wasm` profile reports it as external-only.

## Versions Rehearsal 009 consumes

| System | Version | Source of the pin | Identity reported by the installed binary |
|---|---|---|---|
| Praxis | 3.24.0 (`v3.24.0`, `dccf840`) | echelon-current 1.35.0 `linux-x64.json` (sha256 `bc0f9515…aab`, matching `channel.json`) | `praxis 3.24.0` |
| Dokimos | 0.10.0 (`dokimos-v0.10.0`, `1147762`) | same | `ReleaseVersion 0.10.0`, `SourceCommit 11477629…` |
| Percepta (semantic) | 0.4.0 (`percepta-semantic-v0.4.0`, `4a53688`) | same | `percepta 0.4.0+4a536882…` |
| Percepta (repo lifecycle) | 0.1.0 (`percepta-repo-v0.1.0`) | same | installed; the receipt matched |
| Conditor | 0.10.19 (`v0.10.19`) | **not in the resolved set.** It is the latest release tag and the version R9-READINESS-HANDOFF.md §F.1 names | `conditor-linux-x64` sha256 `cd6b87d3…41cc`, equal to the release `checksums.txt` |
| Ordo | 1.20.0 | echelon-current 1.35.0 | `1.20.0` |

## Results

| # | Check | Command (exact commands are in each log) | Tool version | Exit | Result |
|---|---|---|---|---|---|
| 1 | Versions resolve with no source build | `conditor workstation plan --resolved-set …/linux-x64.json --resolved-set-sha256 bc0f9515… --home <scratch>`, then `workstation apply --authorize sha256:d39022c4…` | Conditor 0.10.19 | 0 / 0 | **PASS** |
| 2 | Synthetic requirement compiled by the real Dokimos compiler | `dokimos verification compile --obligations obligations.json --out compiled.json`; `dokimos verification scaffold … --write true` | Dokimos 0.10.0 | 0 / 0 | **PASS** |
| 3 | Passing test is verified, and Praxis permits completion | `dotnet test` → `dokimos verification coverage … --candidate 0a4922c4… --praxis-matrix …` → `praxis work complete --id WI-SMK-PASS … --evidence verification-matrix=…` | .NET 10.0.402, Dokimos 0.10.0, Praxis 3.24.0 | 0 / 0 / **0** | **PASS** |
| 4 | Skipped test and missing test: Praxis refuses | same pipeline at `7c02d632…` (skip) and `db1fffaf…` (missing) | same | 0 / 4 / **3**, and 0 / 4 / **3** | **PASS** |
| 5 | Failing test: Praxis refuses | same pipeline at `1f87bfb4…` | same | 1 / 4 / **3** | **PASS** |
| 6 | Invalid approval rejected, with owner actions | `conditor acceptance preflight --target <disposable clone>` | Conditor 0.10.19 + `tools/compute-acceptance-verdict.py` (Python 3.13.18) | control 0; invalid **3**; unpinned **3** | **PASS** |
| 7 | Contradictory invariant and incomplete carrier detected | `percepta spec-preflight --machines … --invariants … --interfaces … --catalog …` | Percepta 0.4.0 | control (v4.1.2) 0; synthetic **3** | **PASS** |
| 8 | Requirements package, trust inputs and versions are consistent | `bash drivers/c8.sh.txt` (digests, tag trees, manifest) | git, sha256sum | 0 | **PASS** |

### 1. Version resolution (`evidence/c1-*.log`)
The plan resolved every tool to a release-asset URL with a pinned `sha256` (trust class `IntegrityVerified`). Apply ran all 19 steps with `receipt matched`, ending in "Workstation ready." No component was built from source.

Installation *registration* reported `invalid-target: an environment target needs an explicit logical id`. That is expected when apply runs without `--target-id`; it does not affect installation.

### 2–5. Verification handshake (`evidence/c2-*`, `c3-*`, `c4c5-*`, `evidence/case-*/`)
The synthetic requirement is SMK-001, with one obligation, SMK-001-O1 ("appending 3 then 4 to an empty ledger yields a total of 7"). Its bound identity is `dotnet:Smoke.Tests.SMK001Tests.SMK-001-O1` in suite SU-SMOKE-UNIT. Each case is its own commit and its own Praxis work item. Each work item is declared `persistence`, `medium`, `--state-impact`, so Praxis requires the six-dimension verification matrix.

| Case | `dotnet test` | Dokimos obligation status | Matrix | `praxis work complete` |
|---|---|---|---|---|
| pass | Passed 1 (exit 0) | `verified` | all six dimensions `met` | **exit 0**, `verificationMatrixSatisfied: satisfied`, item `complete` |
| skip (`[<Fact(Skip=…)>]`) | Skipped 1 (**exit 0**) | `invalid` | `not-met`, identity `invalid` | **exit 3**, refused: "verification-matrix-satisfied not satisfied" |
| missing (identity absent; another test passes) | Passed 1 (exit 0) | `not-generated` | `not-met` | **exit 3**, refused |
| fail (`Assert.Equal(8, …)`) | Failed 1 (exit 1) | `failed` | `not-met` | **exit 3**, refused |

What Praxis received in the passing case (`evidence/case-pass/praxis-matrix.json`, `praxis-complete.out`):
- **Obligation identity:** `dotnet:Smoke.Tests.SMK001Tests.SMK-001-O1`, `passed`.
- **Execution evidence:** `dokimos verification coverage --candidate 0a4922c4483bb4c10c7d927add48113247f968af (plan VP-R9-SMOKE)`, `verified`.
- **Candidate SHA:** `0a4922c4483bb4c10c7d927add48113247f968af`, which equals the commit tested (`case-pass/candidate.txt`, and the `sourceSha` in `case-pass/results.json`).
- Praxis recorded the matrix in `consumedEvidence` with sha256 `7031e8bb…7af9`, the same digest as the file.

### 6. Acceptance preflight (`evidence/c6-*.log`, `inputs/conditor/SYNTHETIC-invalid-approvals.json`)
- **Control,** on the real checkout (read-only): READY, `COND-ACC-READY`, exit 0.
- **Synthetic invalid record,** in a disposable clone with the pin moved in that clone only, so the verifier judges content: the record has an untrusted operator, no `source`, and a change keyed only by `obligation_id`. Result: INVALID, exit 3, three `COND-ACC-TRUST` problems, each with the matching owner action:
  - "authority must be {kind: operator, id: <trusted operator>} …"
  - "re-record this approval with source.repository, source.commit (40 hex) and source.path …"
  - "every change must name requirement_id and action; the verifier ignores obligation_id or decision_id alone"
- **Same record, pin left alone:** additionally `COND-ACC-INPUT-DIGEST`, with "re-pin only through the owner decision process; never re-pin to match an edited file". Exit 3.

### 7. Specification preflight (`evidence/c7-*`, `inputs/percepta/`)
- **Control,** on frozen v4.1.2 (5 machines, 101 transitions, 25 invariants, 85 types, 134 commands): nothing blocking, exit 0.
- **Synthetic input:** exit 3, `blocked: true`, with two findings:
  - `rate-bound-vs-unbounded-loop` (likely contradiction): INV-SMK-01 versus T-SMK-3. The counterexample has four strokes, which exceeds 3 in 30 s.
  - `carrier-cannot-discriminate` (missing interface): `SmokeEnvelope` holds only identities and versions; it is consumed by `IF-SMK-KERNEL.Decide` and carried in `Outbox.Pending`.
  - Each finding carries an owner action.

### 8. Indy Init consistency (`evidence/c8-package-consistency.log`)

| Input | Pinned / approved | Observed | Match |
|---|---|---|---|
| v4.1.2 package manifest | APR-V412-OWNER-3 `manifest_sha256 1977780f…683a` | `1977780f…683a` | yes |
| v4.1.2 independent review | APR-V412-OWNER-3 `review_sha256 b0a819fb…164e` | `analysis/independent-review.json` `b0a819fb…164e` | yes |
| v4.1.2 verification plan | `conditor.acceptance.json` `e31dc204…a3ca`; v4.1.2 manifest `files` | `e31dc204…a3ca` | yes |
| v4.1.2 tree versus tag `indy-init-requirements-v4.1.2` (`485b8a9`) | identical | tree `afa2c766…` on both; `git diff` rc 0 | yes |
| v4.1.1 tree versus tag `indy-init-requirements-v4.1.1` (`f85081f`) | identical | tree `0636cc79…` on both; rc 0 | yes |
| v4.1.2 manifest `amends` | v4.1.1 manifest `77d15f55…`, APR-V411-OWNER-2 | v4.1.1 manifest `77d15f55…fa0` | yes |
| Requirements baseline | `f86a3539…8f1a` (contract and approvals file) | `f86a3539…8f1a` | yes |
| Acceptance approvals | `87eaf064…f2ed` | `87eaf064…f2ed` | yes |

The acceptance contract and the plan digest name v4.1.2. v4.1.1 appears only as v4.1.2's declared base and in its own frozen-guard and CI steps. No substitution was found in either direction. Both manifests say `status: CANDIDATE`; that is the frozen convention for both packages, and approval is recorded separately.

## Risks (no check failed; these are the reasons for "with risks")

1. **Conditor's version is not pinned in a machine-readable form.** echelon-current 1.35.0 pins Praxis, Dokimos and Percepta but not Conditor. 0.10.19 was confirmed as the latest release (checksum verified) and is the version the R9 handoff names. However, most Indy-init documents and the CI bootstrap fixture (`conditor-0.10.18-scaffold`) still cite 0.10.18. The owner should state 0.10.19 as the R9 Conditor before T0.
2. **The Praxis verification gate depends on risk metadata.** A freshly initialised repository has `qualityEvidence` off. The matrix is required only because each item declared `persistence` / `--state-impact`. An item with no risk metadata and no `requiredFacets` would complete on `implementation` and `tests` attestations alone. Rehearsal 009 should either declare risk on every item or set `requiredFacets: ["verification-matrix-satisfied"]`.
3. **`dotnet test` exits 0 on a skipped test, and on a suite missing the obligation's test.** Only Dokimos coverage catches these cases. CI must gate on `dokimos verification coverage --require-verified true` (exit 4), not on the test runner's exit code. This repeats handoff §F item 4.
4. **`percepta spec-preflight` with no inputs exits 0** with every count at zero. An invocation with a wrong path or a missing flag reads as "nothing blocking". The invocation should assert non-zero `analysed` counts.
5. **Not exercised in this smoke test:** stale-SHA rejection, which the R9 handoff demonstrated earlier. Here the SHA binding was observed only as matching. Also not exercised: Praxis cross-checking the candidate SHA. Praxis consumes the matrix and does not compare SHAs itself.
6. **Workstation registration** needs `--target-id` at T0. Without it, apply succeeds but registers nothing.

Nothing was repaired. Rehearsal 009 was not launched.
