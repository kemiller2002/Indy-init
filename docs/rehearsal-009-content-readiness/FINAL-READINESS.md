# Rehearsal 009 content readiness: final readiness

**Verdict: BLOCKED** (updated 2026-11-03, after the owner's decisions OD-INDY-19..22).
- Content is now specified, as proposed catalog additions INV-019 … INV-037. Merged with the ratified catalog, they compile with exit 0 under Praxis's catalog compiler.
- Three things remain outstanding:
  1. **Ratification.** The owner must ratify the content additions.
  2. **Tool releases.** The tools that compile and verify a requirement catalog (`praxis requirements manifest`, `dokimos verification requirements`) are merged on `main` but **not released**. The Rehearsal 9 toolchain pins Praxis 3.24.0 and Dokimos 0.10.0, which lack them. So the approved toolchain cannot enforce the catalog.
  3. **Process-evidence rules.** The Rehearsal 9 process-evidence rules are not pinned.
- Rehearsal 009 was not launched.

**Superseded.** The earlier route, a v4.1.3 package with ODR-V413-01..03, is superseded by OD-INDY-19 and withdrawn. Its analysis is kept below and in AMENDMENT-DECISION.md.

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
| Owner decisions OD-INDY-19..22 recorded (decided by the owner; written by an agent) | `docs/OWNER-DECISIONS-2026-11-03.md` |
| Proposed catalog additions INV-019 … INV-037 and their reconciliation | `requirements/catalogs/causa-investigation-content.proposed.requirement-catalog.json`, `docs/requirement-catalogs/CAUSA-INVESTIGATION-CONTENT.md` |
| Catalog build guidance corrected: no copying from Rehearsal 8 (OD-INDY-22); the catalog tools are unreleased | `docs/requirement-catalogs/CAUSA-INVESTIGATION.md` |

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
| 13 | The proposed catalog compiles once merged | `praxis requirements manifest compile` on ratified + proposed, and again with `--previous` the ratified manifest (`evidence/praxis-catalog-compile-*.log`) | Praxis main `ddfcbbe`, built from source for validation only (unreleased) | 0 / 0 | 37 requirements, 239 obligations; nothing dropped |
| 14 | Catalog sources resolve | every non-owner-decision `sources[].requirement` of INV-019 … INV-037 checked against the baseline's requirement IDs | baseline v4 | 0 | all resolve |
| 12 | Repository CI | the pull request that delivers this file | | | recorded on the pull request |

## Unresolved owner actions

| # | Action | Who | How |
|---|---|---|---|
| 1 | Ratify the content requirements INV-019 … INV-037 (OD-INDY-19..21) | owner | Merge the entries of `requirements/catalogs/causa-investigation-content.proposed.requirement-catalog.json` into `requirements/catalogs/causa-investigation.requirement-catalog.json` in your own commit, then delete the proposed file. Also correct the ratified catalog's `description`, which still says "not ratified". |
| 2 | Decide how Rehearsal 9 gets the catalog tools | owner decision | Option A: release Praxis (with `requirements manifest`) and Dokimos (with `verification requirements`), register them in a new echelon-current set, and move `kickoff/rehearsal-009/toolchain.lock.json` to it. Option B: Rehearsal 9 runs without catalog verification, so INV-* is guidance only and not enforced. Option C: decide otherwise. Recommended: A. An agent can prepare the releases through each repository's release process, but it does not publish them without your go-ahead. |
| 3 | Pin the Rehearsal 9 process-evidence rules | owner | Set `processEvidence.rulesSha256` in the toolchain lock (`docs/rehearsal-009-preflight-smoke/FINAL-CORRECTIONS.md`, §Owner action). |

**Withdrawn:** ODR-V413-01..03 (superseded by OD-INDY-19).

## Is Rehearsal 009 ready?

**No, it is BLOCKED** on the three actions above. After them:
- the catalog is ratified;
- the released Praxis and Dokimos are pinned;
- the record rules are pinned.

Each INV requirement is then compiled into obligations and verified against the running application, with the mutations the released Dokimos runs. The INVESTIGATION-CONTENT-E2E scenario is INV-037's live round trip.

## Acceptance scenarios that must pass during the seven-hour run

These apply once INV-019 … INV-037 are ratified and the catalog tools are released and pinned. Each must pass at the release candidate through `Verification gate` in `mode=release`:
1. **INVESTIGATION-CONTENT-E2E**, live, in SU-CONTENT-LIVE: INV-037, with INV-001/002/023–035 entered and reloaded. Each must be fault-sensitive (P-CONTENT-WIRE, -PROJECTION, -VIEW and -RELATION detected).
2. **INVESTIGATION-CONTENT-NEGATIVE** (the `validation`/`rejects` obligations of INV-019–021 and INV-023–033, and the failure states):
   - missing name, empty observation, empty hypothesis;
   - invalid URL, invalid relationship target;
   - duplicate submission;
   - save failure, authentication failure, conflict;
   - malformed reload.
3. **INVESTIGATION-CONTENT-MOBILE**, at 320 and 390 px (the `accessible` obligations of every INV entry).
4. **INVESTIGATION-CONTENT-CONCLUSION** (INV-032/033).
5. **Unchanged:** every v4.1.2 P0 obligation, the record obligations through `verify-process`, and the stale-SHA refusals of the gate.
