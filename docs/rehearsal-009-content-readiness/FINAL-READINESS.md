# Rehearsal 009 content readiness: final readiness

**Verdict: BLOCKED.**
- A mandatory content capability lacks an approved requirement, and the required owner approvals are outstanding (ODR-V413-01, ODR-V413-02, ODR-V413-03).
- The verification mechanism that will enforce content once it is approved is built and proven.
- Rehearsal 009 was not launched.

## Why Rehearsal 008 allowed the gap

| Question | Answer |
|---|---|
| Which capabilities were missing? | Investigation name, description and primary question; observation, hypothesis and conclusion text; evidence items; evidence–hypothesis relationships; editing; and any persisted or reloaded content. Rehearsal 008 had lifecycle commands, counters and event labels. Evidence was explicitly "not part of this version" (AUDIT.md §1). |
| Were the original requirements deficient? | **Yes.** The content requirements (R1.1, R1.2, R2.x, R3.1–R3.5, R6.x, S01, S03, S05) exist only at priority `unstated`. The P0-only plan (ODR-V4-10 A) gave them no criterion and no test. Several behaviours were never specified (question, editing, revisions, conclusion entry, evidence label and description, validation rules). The v4.1.1 contract that Rehearsal 008 built on carried only an event kind. |
| Did implementation fail? | Partly. Rehearsal 008 implemented what the frozen v4.1.1 boundary allowed. It found the defect and reported it (DEV-R8-09), but could not change a frozen contract inside the run. |
| Did verification fail? | **Yes.** No obligation required content, so every gate stayed green. Rehearsal 008's tests asserted counters and labels; the only `fill(` calls were the connection form. |

## What changed in this work

| Change | Where |
|---|---|
| Audit, traceability, content model, UI acceptance, E2E specification and amendment decision | `docs/rehearsal-009-content-readiness/*.md` |
| Proposed successor inputs: CONTENT-001..012 obligations, concrete content contract stubs, owner decision requests | `docs/rehearsal-009-content-readiness/proposed/` |
| Gate: `--require-fault-sensitive CATEGORY,…` and `--probes F,…`. An obligation in a named category counts only when a Dokimos fault probe at HEAD was detected, and a matrix is never `met` without it. This uses the existing Dokimos probe mechanism; Dokimos itself is unchanged. | `tools/r9/r9.fsx` |
| Gate workflow: runs `dokimos verification probe` on `.echelon/fault-probes.json` at the candidate, and always passes `--require-fault-sensitive content-round-trip` | `tools/r9/verification-gate.yml` |
| Three new harness cases (76 in total) | `tools/r9/tests.fsx` |

**Nothing else was touched:**
- the frozen packages and their approval records;
- Rehearsal 008;
- the Rehearsal 009 repository, which does not exist;
- the engineering systems' releases.

## Validation

| # | Check | Command | Version | Exit | Result |
|---|---|---|---|---|---|
| 1 | Every traceability reference | A Python check of every `…-O<n>` ID in this directory against `requirements/causa-requirements-baseline-v4.json`: 89 in TRACEABILITY.md; 49 cited by the proposed obligations; all documents | baseline v4 (sha256 `f86a3539…`) | 0 | all resolve |
| 2 | Percepta on the complete intended specification | `r9.fsx spec-preflight --machines v4.1.2/state-machine-contracts.json --invariants v4/invariants.json --interfaces proposed/interface-specifications.proposed.json --catalog docs/COMMAND-EVENT-CATALOG.md` (`evidence/spec-preflight-proposed.json`) | Percepta 0.4.0 | 0 | PASS: nothing blocking |
| 3 | Analysis is non-empty | (same) | | | 5 machines, 101 transitions, 25 invariants, 26 operations, 134 commands, 109 types; each count equals the count derived from the inputs |
| 4 | Conditor acceptance preflight | `conditor acceptance preflight --target .` (`evidence/conditor-acceptance-preflight.log`) | Conditor 0.10.19 (pinned) | 0 | READY |
| 5a | Dokimos: plan identities and completeness | `dokimos verification compile --obligations proposed/content-obligations.proposed.json` (`evidence/dokimos-compile-proposed.log`) | Dokimos 0.10.0 | 0 | 12 requirements, 14 obligations, 22 identities, 0 refusals |
| 5b | Dokimos: scaffold | `dokimos verification scaffold … --write true` into a scratch repository (`evidence/dokimos-scaffold-proposed.log`) | | 0 | 4 spec files at the planned paths (kept in scratch, not committed) |
| 5c | Dokimos: skeletons never count | `dokimos verification coverage` on the scaffold (`evidence/dokimos-coverage-scaffold.log`) | | 0 | 14/14 `invalid` (placeholder) |
| 5d | Dokimos: the gate refuses | `r9.fsx gate --mode release … --require-fault-sensitive content-round-trip` (`evidence/gate-proposed-content-on-skeletons.log`) | | 4 | REFUSED: 0/14 verified |
| 5e | Dokimos: a command-only test is rejected | coverage with `requiredBoundary [fill, reload]` (`evidence/dokimos-boundary-probe.log`, `boundary-probe.spec.js.txt`) | | 0 | the round-trip test is accepted; the command-only test is `invalid` ("does not reach the suite boundary") |
| 5f | Dokimos: content must be fault-sensitive | `tools/r9/tests.fsx` cases `dokimos-probe-content-dropped-detected` (0), `gate-content-without-probe-refused` (4), `gate-content-with-detected-probe-passes` (0) | | | PASS (`evidence/` copies) |
| 6 | Proposed contract typechecks | `dotnet build ContractCheck.fsproj` with the proposed stubs and every other v4.1.2 contract (`evidence/contract-typecheck-proposed.log`) | .NET SDK 10.0.402 | 0 | 0 warnings, 0 errors |
| 7 | No frozen requirement modified | `git status --short requirements-compiled requirements kickoff/rehearsal-007` | | | empty |
| 8 | No Rehearsal 008 file modified | `git -C <r008 checkout> status --short` | | | empty; HEAD `fd8fba9` |
| 9 | No Rehearsal 009 application code | The directory contains documents, JSON specifications, contract stubs (proposed, not in any package) and evidence. The skeleton specs stayed in scratch. | | | none |
| 10 | Readiness harness | `dotnet fsi tools/r9/tests.fsx …` | | 0 | 76 cases, 0 failed (`docs/rehearsal-009-preflight-smoke/corrections/cases.tsv`) |
| 11 | Offline suite | `dotnet run --project tools/requirements-compiler/tests -- --offline --source .` (`evidence/offline-suite.log`) | | 0 | all passed |
| 12 | Repository CI | the pull request that delivers this file | | | recorded on the pull request |

## Unresolved owner decisions

| ID | Decision | Recommended | Effect if not taken |
|---|---|---|---|
| ODR-V413-01 | Add CONTENT-001..012 as P0 obligations of successor v4.1.3 | A | Content stays unverified; Rehearsal 008's outcome can recur with every gate green. |
| ODR-V413-02 | Freeze the concrete content payloads, revisions and the conclusion record. This revisits ODR-V412-02 option A for the content payloads only. | A | Content fields are each lane's own choice; Percepta cannot check them. |
| ODR-V413-03 | Approve the compiled v4.1.3 package, after an agent compiles and reviews it | A, after review | v4.1.2 stays the plan of record. |
| (carried over) | Pin the Rehearsal 009 process-evidence rules (`docs/rehearsal-009-preflight-smoke/FINAL-CORRECTIONS.md`, §Owner action) | confirm and pin | The record gate fails closed. |

## Is Rehearsal 009 ready?

**No, it is BLOCKED.** Once the owner records ODR-V413-01 and ODR-V413-02, the path to READY is:
1. an agent compiles v4.1.3 with the existing generator (X01–X20, contract typecheck and negative controls, Percepta, independent review, Conditor preflight);
2. the owner approves and tags it (ODR-V413-03);
3. the toolchain lock's `requirementsPackage` moves to v4.1.3, in the owner's commit;
4. the process-evidence rules are pinned.

The content verification then applies through the gates already in place, with no further framework work.

## Acceptance scenarios that must pass during the seven-hour run

These apply once v4.1.3 is approved. Each must pass at the release candidate through `Verification gate` in `mode=release`:
1. **INVESTIGATION-CONTENT-E2E**, live, in SU-CONTENT-LIVE. The seven round-trip tests cover CONTENT-001-O1/O2, 002, 003, 004, 005, 006, 008 and 009. Each must be fault-sensitive (P-CONTENT-WIRE, -PROJECTION, -VIEW and -RELATION detected).
2. **INVESTIGATION-CONTENT-NEGATIVE** (CONTENT-010, CONTENT-011):
   - missing name, empty observation, empty hypothesis;
   - invalid URL, invalid relationship target;
   - duplicate submission;
   - save failure, authentication failure, conflict;
   - malformed reload.
3. **INVESTIGATION-CONTENT-MOBILE** (CONTENT-012), at 320 and 390 px.
4. **INVESTIGATION-CONTENT-CONCLUSION** (CONTENT-007).
5. **Unchanged:** every v4.1.2 P0 obligation, the record obligations through `verify-process`, and the stale-SHA refusals of the gate.
