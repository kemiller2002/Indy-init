# Rehearsal 9 readiness: engineering handoff (2026-11-02)

The decision record is [R9-VERIFICATION-DECISION-RECORD.md](R9-VERIFICATION-DECISION-RECORD.md). Every claim below names its evidence; nothing here was estimated.

**Verdict: NOT READY today. It becomes READY WITH RISKS once the three must-fix owner items in §F are done.**

- The engineering mechanisms are built, tested and demonstrated.
- The pinned acceptance inputs are still the ones that made Rehearsal 8 INVALID, and the frozen package still carries the three defects Rehearsal 8 hit.
- Running Rehearsal 9 before those are fixed would, with near certainty, again end INVALID at T34.

## A. Framework changes

All PRs are drafts and none is merged. No release was cut.

| Repository | PR / head | Changed | Compatibility | Tests executed |
|---|---|---|---|---|
| Indy-init | #72 `02374f6` | `compute-acceptance-verdict.py`: `build_trust` split, with rules unchanged; new `--preflight` mode. `test-acceptance-verdict.py`: +8 tests. `conditor.acceptance.json`. Decision record. | `--preflight` is additive; acceptance behaviour is unchanged (all 19 earlier tests pass) | verdict tests 27 OK; ledger tests 11 OK; offline suite all passed; CI green on `b601ba2` |
| conditor | #127 `2e66c7a` | `AcceptancePreflight` module; `conditor acceptance preflight`; `doctor` findings when `conditor.acceptance.json` exists; schema | additive and opt-in | 1471 PASS / 0 FAIL; CI 25/25 green |
| dokimos | #62 `ee2e49e` | `dokimos verification compile\|scaffold\|coverage\|probe`; Praxis matrix output; docs | additive; Dokimos 0.9.0, no new release | 566 passed (Domain 4, Core 405, CLI 157, self-ratchet included); CI green |
| dokimos | #63 `d6c84a5` (stacked on #62) | the two benchmarks and their evidence | experiments only | see §D |
| percepta | #45 `fc925f8` | `Specification` module; `percepta spec-preflight` | additive; screen contracts untouched | Core, Binding, Adversarial and Lifecycle tests all pass; CI 16/16 green |
| praxis | none | no code change. Dokimos emits the `praxis.verification-matrix/1` that Praxis 3.24.0 already enforces | — | demonstrated with the 3.24.0 release (§B) |
| ordo | none | no demonstrated dependency | — | — |

## B. Verification compiler (Dokimos)

- **Input:** the frozen `indy-init.v41-verification-execution-plan/1` + `indy-init.verification-plan/1`, read as they are, or `dokimos.verification-obligations/1`. Obligations without a requirement, criterion or bound identity are refused (`dokimos.verification-refusals/1`, exit 4). The v4.1.1 plan compiles with 0 refusals: 613 obligations, 800 identities, 11 suites.
- **Output:** `dokimos.verification-coverage/1` (per-identity and per-obligation status, causes, rejected runs); `dokimos.verification-work/1` (ranked packages, unmapped identities); `praxis.verification-matrix/1`.
- **Identities:** `dotnet:<className>.<method>` (TRX TestMethod) and `playwright:<file> > <titles> [<project>]`. Record evidence (`praxis:*`) is `blocked`, for its producer.
- **Assertion-quality checks** (lexer-aware, not FCS):
  - placeholder;
  - no assertion, or only trivially-true ones;
  - skipped (static, in the body, or at run time);
  - excluded (`.fsproj` Compile, runner paths, `--filter`, `--project`);
  - helper-only (`requiredBoundary`);
  - duplicates, and name drift.
- **Fault probes:** a disposable copy; a passing control; the faulted copy must build; a surviving fault withdraws `verified`.
- **Rehearsal 8 regression, read-only:**
  - The source scan alone reproduces T34's per-suite table exactly (Chromium 88/193, WebKit 23/49, deterministic 71/45, live 60/81, browser-live 0/17).
  - With T34's CI results: 203 verified + 63 credited only by the review record = **266**, T34's count.
  - The same results at the previous SHA verify 0.
- **Praxis 3.24.0 gate**, with real `dotnet test` TRX:
  - no matrix: refused (exit 3);
  - stub test: refused (3);
  - previous-SHA results: refused (3);
  - passing test at the candidate: complete (0).
- **Known limitations:**
  - Generated or looped tests read as not-generated.
  - Helper assertions are recognised by verb name or through a file-local definition.
  - Semantic adequacy needs probes.

## C. Acceptance preflight

```
conditor acceptance preflight --target <indy-init checkout> [--json]
# runs: python3 tools/compute-acceptance-verdict.py --preflight --baseline B --baseline-sha256 PINNED
#       --baseline-source . --approvals A --approvals-sha256 PINNED --trusted-operator-id kemiller2002
```

- **Inputs:** `conditor.acceptance.json`, holding the pinned baseline, the approvals, the verification plan and the trusted operator.
- **Outputs:** `conditor.acceptance-preflight-report/1` and the verifier's own `causa.acceptance-preflight/1`. Exit 0 READY, 3 INVALID, 2 unreadable.
- **Demonstrated failure:** against today's inputs it returns **INVALID** in 0.3 s. Problems:
  - 9 approvals lack `source.repository/commit/path`;
  - 26 changes have no `requirement_id` (they use `obligation_id`/`decision_id`).
  - Each problem carries its owner action. This is the Rehearsal 8 T34 failure, found before T0.
- **Further unit-tested cases:** tampered digest, untrusted operator, unavailable verifier, verifier contradicting its exit code, drifted pinned input.
- **Dry run:** synthetic evidence proves ACCEPTED, FAILED, BLOCKED, INCOMPLETE (NOT_RUN) and INVALID, and FAIL > BLOCKED > NOT_RUN precedence.

**Specification preflight (Percepta):**

```
percepta spec-preflight --machines …/v4.1.1/state-machine-contracts.json --invariants …/v4/invariants.json \
  --interfaces …/v4.1.1/analysis/interface-specifications.json --catalog docs/COMMAND-EVENT-CATALOG.md
```

On the frozen v4.1.1 package it runs in 0.3 s and exits 3. It finds:
- INV-04 × T-S31/T-S32, a likely contradiction with a counterexample (DEV-R8-11);
- `CommandEnvelope` cannot discriminate commands, with the loss through Save, Export, Reapply and `Msg` (DEV-R8-09, CR-T17-01);
- (UnknownOutcome, ReconcileReadFailed) has no transition at the read limit (DEV-R8-12);
- one non-blocking unstated pair.

There is no other finding, and no requirement or transition id appears in the rules.

## D. Benchmark evidence

The details are in dokimos #63: `experiments/verification-benchmarks/BENCHMARK-RESULTS.md`.

| | B1 approval workflow (browser vertical slice) | B2 evidence collection (real HTTP provider) |
|---|---|---|
| Test generation | 11 exact identities scaffolded in 0.77 s; bodies by a spec-only test agent (225 s) | 9 identities in 0.63 s; spec-only test agent (144 s) |
| Execution at `b63bb3c` | .NET 8/8 (11 s), Chromium 3/3 (8 s) | .NET 9/9 (14 s) |
| Coverage | **11/11 verified**, gate exit 0 | **9/9 verified**, gate exit 0 |
| Fault injection | 6/6 detected (86 s) | 6/6 detected (66 s); a neutral calibration probe survived |
| Fault-sensitive | 7/11 | 6/9 |
| Agent tokens (harness totals) | tests 77,856; implementation 75,860 | tests 72,597; implementation 65,568 |

- Elapsed time from written requirements to both benchmarks verified: about 10 minutes.
- Token buckets, coordinator tokens and cost: unavailable.
- **Mechanism defects found and fixed:**
  - A fault that breaks compilation counted as "detected"; probes now declare a `build`.
  - The SHA binding correctly refused to combine probe results with tests run at a different SHA.

## E. Rehearsal 9: what the evidence supports and what it does not

**Supported:**
- The missing-test class (about 380 of 613 obligation-causes in Rehearsal 8) is now visible from source at any minute, with exact identities and ranked work packages.
- Placeholders, stubs, skips, exclusions, name drift and stale-SHA results cannot count as verified.
- Praxis refuses completion without verification.
- The invalid-approval class and the three package defects are detected before T0.

**Not supported:**
- Any prediction of the Rehearsal 9 verified percentage. The benchmarks are about 50 times smaller than Causa. The 75 % target (§14 of the brief) remains a target.

**Proposed targets, measurable with these tools:**
- preflight READY before T0;
- `dokimos verification coverage` committed on every merge;
- every Chromium or WebKit spec file present as a failing skeleton within 30 minutes;
- 0 obligations counted verified by T34 that Dokimos reports as anything but `verified`.

## F. Remaining work that could change competition-day execution

**Must fix before Rehearsal 9** (owner actions; agents may not do them):
1. **Approval contract.** `tools/owner-freeze.fsx` writes changes keyed by `obligation_id`/`decision_id` without `source`, and the verifier accepts only `requirement_id` + `action` with an immutable source. The owner decides which side changes; then re-record the approvals and re-pin their digest in `conditor.acceptance.json`. Until then every acceptance run is INVALID.
2. **Frozen-package defects.** The three are INV-04 versus T-S31/T-S32, the `CommandEnvelope` body and the (UnknownOutcome, ReconcileReadFailed) transition. Each needs an owner decision and a successor package (v4.1.2, never an edit of v4.1.1). Otherwise Rehearsal 9 re-lives DEV-R8-09, -11 and -12, with at least one obligation failing by construction.
3. **Merge and make the tools available at T0.**
   - Merge #72, #127, #62 and #45.
   - Either cut releases (Dokimos is not in the Conditor `indy-init` preset; Percepta `spec-preflight` is in the semantic CLI line, not `percepta-repo` 0.1.0), or have the T0 bootstrap build Dokimos from a pinned commit.
   - Decide which, and pin it.

**Should fix if time permits:**
4. **Bootstrap skeletons.** The T0 bootstrap should write `dokimos verification scaffold --write true` skeletons instead of the `requirements-compiler v41` compiling stubs, and CI should publish `dokimos verification coverage` on every merge. This is the change that turns Rehearsal 8's silent *not run* into visible red. It is reusable workflow work, permitted before kickoff, and is not done.
5. **Producers for the record suites.** Schedule producers in the DAG for the 24 `SU-PROCESS-RECORDS` obligations, the `SU-VERIFIER` job and the requirements disposition file (Rehearsal 8 G0 BLOCKED). Dokimos reports these as `blocked`; it does not produce them.
6. **Deployed-site real-provider browser suite.** `SU-BROWSER-LIVE` (17 obligations) needs a browser-held test token; the plan has no mechanism for one.
7. **Fault probes.** Write probes for the Causa obligations behind Rehearsal 8's late defects (command body, large read-back, reconcile after 250 commits) at kickoff.

**Defer until after 7 November:**
8. **Praxis single-file merge.** `praxis work history merge` refuses when only `current.json` conflicts (`FileWorkHistoryMerge.fs`). Do not add `merge=union` for `events.jsonl`: it produces exactly that case.
9. **Assertion analysis.** FCS-based analysis; recognising multi-step cycles in the Percepta rate-bound rule; more prose forms of bounds.
