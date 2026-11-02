# Rehearsal 9 preparation: verification-driven engineering decision record

- Status: accepted by the implementing agent for execution; owner review pending
- Date: 2026-11-02
- Baseline: Rehearsal 8 final candidate `5e2717665b696a0c70175c7cacfc5702c5b2058d` (kemiller2002/indy-init-rehearsal-008, read-only)
- Scope: reusable engineering systems only. No competition application code, no Causa executable tests, no approval records, no frozen package edits.

## 1. What Rehearsal 8 actually lost

Source: `evidence/T34/T34-ACCEPTANCE-REPORT.md`, `evidence/DEVIATIONS.md` and `evidence/telemetry/merges.jsonl` of Rehearsal 8.

| Cause | Obligations | Mechanism |
|---|---|---|
| Bound test never written: spec file absent, or a bootstrap stub left unreplaced | 152 + 81 + 45 + 33 + 17 = 328 references | `requirements-compiler v41` wrote compiling stub files ("the owner replaces this content"). A stub compiles, produces no result and reads as *not run*. Nothing measured the gap during the window. |
| Bound title exists in a spec but never ran (name drift or skipped) | 38 + 16 = 54 | Exact identity is `playwright:<file> > <title> [<project>]`. A one-character difference makes the obligation unexecuted. Nothing checked titles against the plan. |
| Process records absent | 24 | `SU-PROCESS-RECORDS` has no producer scheduled in the DAG. |
| Approval trust invalid → verdict INVALID | all | `kickoff/rehearsal-007/approvals.causa.json` has no `source.repository/commit/path`, and its changes use `obligation_id`/`decision_id`. The verifier requires `requirement_id` + `action`. This was detectable before T0. |
| G0 BLOCKED | gate | `research/requirements-preservation/disposition.json` never produced. |
| Package contradiction INV-04 × SM-SAVE (DEV-R8-11) | 1 failed | INV-04 bounds reads per 60 s; T-S31/T-S32 allow user-driven reads with no clock. This was detectable from the frozen tables. |
| Frozen interface lacks a command body (DEV-R8-09, CR-T17-01) | defects #51 | `CommandEnvelope` carries no command, but `Decide` must distinguish commands. This was detectable from the contract plus the catalog. |
| `.ros` merge conflicts | 26 of 49 merges | All 26 conflicted on `.ros/context/current.json`, and 20 of them also on `.ros/events/events.jsonl` (append-only). |

The dominant loss (about 380 of 613 obligation-causes) is **tests that the plan named exactly but nobody wrote, with no signal while there was still time to write them**. Coverage, not application behaviour, decided the verdict.

## 2. Capability map (audit, 2026-11-02)

| System | Version | Relevant existing capability | Gap |
|---|---|---|---|
| Dokimos | 0.9.0 | TRX claim reader (`ConstructionEvidence.readTrxClaims`), regex skip detection (`QualitySignals.skippedTestLines`), lexer (`SourceLexing`), completion verification | No plan intake, no exact-identity scan, no Playwright results, no assertion analysis, no fault probes, no obligation coverage |
| Praxis | 3.24.0 | Completion facets incl. `VerificationMatrixSatisfied` (`praxis.verification-matrix/1`), candidate binding | No per-obligation status ledger; `.gitattributes` has no merge driver for append-only logs |
| Percepta | semantic 0.3.0 | UI screen-contract validation and browser verification | No requirements, state-machine or interface analysis |
| Conditor | 0.10.18 | `doctor`, `test-env preflight`, toolchain discovery, `indy-init` preset | No acceptance-trust check |
| Verifier | `tools/compute-acceptance-verdict.py` (indy-init) | Authoritative trust and verdict rules | Requires a ledger, manifest and source SHA, so it cannot run before T0 |

## 3. Decisions

1. **D1 – One acceptance authority.** The preflight calls the authoritative verifier's own `build_trust` through a new `--preflight` mode. Conditor runs that mode and reports its problems. It does not re-implement them. *Rejected:* a Conditor-side approval schema, which could disagree with the verifier.
2. **D2 – The Dokimos verification compiler reads the frozen plan as it is.** The input contract is `dokimos.verification-obligations/1`. A reader adapts `indy-init.v41-verification-execution-plan/1`, joined with `indy-init.verification-plan/1` for obligation text, so the frozen package is not re-authored. The compiler refuses obligations without a requirement, an identity or a criterion; it does not invent behaviour.
3. **D3 – Scaffolds fail; they never pass silently.** `dokimos verification scaffold` writes test skeletons with the exact bound identity. Each body is `failwith "DOKIMOS-UNIMPLEMENTED ..."`, which `audit` classifies as a placeholder. A stub can then never be counted as verified, and it shows up as red rather than as an invisible *not run*. Bodies are written after kickoff by agents (AI generation), and they then face the same audit.
4. **D4 – A lexer-aware structural scanner, not FSharp.Compiler.Service.** F# and Playwright test blocks are extracted on the code token stream, with comments and strings removed. Assertions, skips, trivial assertions and identity mismatches are classified on that stream. FCS would add about 40 MB to a native CLI three days before the event. This limitation is recorded.
5. **D5 – Coverage is exact.** An obligation is `verified` only when every bound identity is present, structurally valid, executed and passed in results recorded for the candidate SHA. Results from another SHA are rejected.
6. **D6 – Fault probes are opt-in per obligation.** `dokimos verification probe` applies a declared fault to a disposable copy, runs the bound tests and requires them to fail. Quality is reported as `fault-sensitive` only with that evidence.
7. **D7 – Praxis integration through the existing facet.** Dokimos emits `praxis.verification-matrix/1`, the contract Praxis already gates `VerificationMatrixSatisfied` on. No new Praxis store.
8. **D8 – `.ros` contention: the smallest compatible fix.** `merge=union` for the append-only `.ros/events/events.jsonl` (built into Git, no driver to install). `current.json` is not changed before the event; it is reported.
9. **D9 – Specification preflight lives in a new, separate Percepta module.** The owner's charter for this work assigns requirements consistency to Percepta. Percepta's current code base covers only UI contracts, so the analysis is a self-contained `Specification` module with its own command, and it does not change the screen-contract code. Findings are typed: `proven-contradiction`, `likely-contradiction`, `missing-specification`, `requires-owner-decision`. A finding whose bound was extracted from prose is never reported above `likely`.

## 4. Ranked implementation order

| Rank | Change | Rehearsal 8 obligations addressed | Cost |
|---|---|---|---|
| 1 | Verifier `--preflight` + Conditor check | verdict INVALID (all) | small |
| 2 | Dokimos plan intake, scaffold, audit, results, coverage | about 380 | medium |
| 3 | Dokimos → Praxis verification matrix; union merge for events | false-verified risk; 26 conflicts | small |
| 4 | Percepta specification preflight | 1 failed + 2 frozen-contract defects | medium |
| 5 | Fault probes | quality of the 266 passing tests | small |
| 6 | Synthetic vertical slice + two benchmarks | proof of the mechanism | medium |

Out of scope before 7 November: Praxis storage rewrite, FCS-based analysis, a universal test generator, a deployed-site real-provider browser harness (it needs a browser-held token; recorded as a risk).
