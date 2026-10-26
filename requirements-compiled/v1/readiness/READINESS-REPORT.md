# Rehearsal 4 readiness report

**Verdict: BLOCKED. Rehearsal 4 must not start.**

- The preflight's computed verdict is BLOCKED, with no FAIL left.
- 5 readiness blockers remain, all upstream releases. Each needs a release by the system's owner, not an owner decision. The operator's test repository and CI secret (T05) are also still needed.
- The owner's decisions so far:
  - The owner approved #17 and #18 on 2026-10-26, and they were merged on that instruction. The merge of #18 decided F-001/DEC-024 and F-006, and fixed the baseline drift (F-003).
  - OD-INDY-9 to OD-INDY-13 (`docs/OWNER-DECISIONS-2026-10-26.md`) decide DEC-006, DEC-025, DEC-022 and DEC-023, and confirm OD-INDY-3/4. The owner approved them in #20.

## Revisions and sources

| | |
|---|---|
| Source | kemiller2002/indy-init `9dc7d57`: main with #16 (acceptance verifier), #17 (readiness tooling), #18 (owner-approved requirement changes), #19, and the owner decision record of 2026-10-26 |
| Package | `requirements-compiled/v1`, compilation **PASS**: all checks V01–V17 pass, and V14 confirms there is no baseline drift |
| Requirements | 467 inventoried (461 baseline, including EXT-DATA-013, plus 6 external), 2325 obligations, 108 P0. All are preserved; none is marked verified. |
| Evidence | `evidence/preflight-run.json` (executed at 9dc7d57), `evidence/auth-contract-probe.json`, `evidence/baseline-equivalence.json`. `evidence/preflight-owner-branch-projection.json` is historical: the pre-merge projection of #18. |

## Blockers: classification and state

| Blocker | Class | Cause | Prepared here | Needs |
|---|---|---|---|---|
| ~~F-001, DEC-024~~ | Indy-init requirement conflict | UI-001/UI-015 required five demo investigations on launch | **Resolved:** owner merge of #18 (UI-001-O1, UI-015-O2, EXT-DATA-013) | — |
| ~~F-003~~ | Verification deficiency | Baseline pinned at 002d8ba lacked AR-019..AR-026 | **Resolved:** owner merge of #18; PRE-REQ-01 PASS | Re-pin at the kickoff commit (T01), as planned |
| ~~F-009, DEC-006, CT-007~~ | Contract incompatibility | Storage contract §9.4.1 mandates a REST update that cannot compare-and-swap | **Resolved:** OD-INDY-9, an exception to §9.4.1 (`CONTRACT-RESOLUTION.md` §1). CT-007 is unverified until T18/T21. | — |
| ~~F-008, DEC-022~~ | Owner decision | AI loop and time travel scope | **Resolved:** OD-INDY-11 excludes T27 and T28 from Rehearsal 4. They carry `excludedBy: DEC-022`. | The operator's approvals file (T09) before they leave the run |
| ~~DEC-025, CT-017~~ | Owner decision | Authentication mechanism undecided | **Resolved:** OD-INDY-10, a fine-grained token held in memory (`CONTRACT-RESOLUTION.md` §2). CT-017 is unverified until PRE-AUTH-02/T21. | — |
| ~~DEC-023~~ | Owner decision | Reuse of earlier rehearsal interfaces at the event | **Resolved:** OD-INDY-12 | Organizer confirmation for the event (PRE-ORG-01) |
| F-004, CT-013 | External dependency | No Praxis release carries the preservation gate (PRE-REQ-03, executed) | The minimum release is identified (`CONTRACT-RESOLUTION.md` §3) | Praxis release, or an owner VER-005 exception |
| F-005, CT-012, CT-016 | External dependency | percepta-semantic 0.2.1 is now a registered release, but in no approved set (PRE-DEP-01, executed) | The contents of 1.28.0 are specified | Registry set 1.28.0, Conditor qualification, owner approval |

**Counts:**
- 16 at the start of this work.
- 11 are resolved:
  - 3 by the owner-approved merge of #18: F-001, DEC-024 and F-003;
  - 8 by OD-INDY-9 to OD-INDY-12: F-009, DEC-006, CT-007, F-008, DEC-022, DEC-025, CT-017 and DEC-023.
- Also resolved, though they were not readiness blockers: F-006 (#18), and F-002, F-012, F-013 and F-019 (OD-INDY-10, -12 and -13).
- 5 are open, all waiting on upstream releases: F-004, CT-013, F-005, CT-012 and CT-016.
- Upstream progress since package v1: percepta-semantic 0.2.1 was registered as a release (Registry PR #96/#97, by the owner), and the Conditor SD-1 fix is confirmed released in v0.10.11.

## Preflight (executed: `evidence/preflight-run.json`)

8 PASS, 5 BLOCKED, 4 NOT_RUN, 0 FAIL.

| Check | Status | Detail |
|---|---|---|
| PRE-DEC-01 owner decisions | PASS | No unresolved decision |
| PRE-DEC-02 critical findings | BLOCKED | F-004, F-005 open |
| PRE-REQ-01 baseline drift | PASS | Pinned at c751d58; no drift at 9dc7d57 |
| PRE-REQ-02 package compiles | PASS | |
| PRE-REQ-03 Praxis gate released | BLOCKED | v3.20.0 has no `preservation` command |
| PRE-REQ-04 gate PASS on the derived package | NOT_RUN | Needs T07–T09 |
| PRE-DEP-01 release set | BLOCKED | echelon-current 1.27.0 lacks percepta-semantic |
| PRE-DEP-02 contracts | BLOCKED | CT-012, CT-013, CT-016 |
| PRE-DEP-03 identities | PASS | 5 of 5 match |
| PRE-AUTH-01 CI secret | BLOCKED | CAUSA_GITHUB_TOKEN and CAUSA_TEST_REPO are not set here (T05) |
| PRE-AUTH-02 auth contract | NOT_RUN | Needs the secret. The negative-control probe showed this sandbox rewrites credentials, so it must run in CI |
| PRE-SEC-01 committed secrets | PASS | No token patterns at 9dc7d57 |
| PRE-TOOL-01 SDK | PASS | 10.0.402 = global.json |
| PRE-TOOL-02 Conditor SD-1 | PASS | v0.10.11 contains CON-367 |
| PRE-TOOL-03 bootstrap | NOT_RUN | T10 record |
| PRE-ORG-01 organizer | NOT_RUN | Event only |
| PRE-VER-01 manifests | PASS | V16 |

## Execution manifests

- **`preflight-manifest.json`:** 17 checks across these categories:
  - owner decisions
  - requirements baseline
  - requirements preservation
  - contract compatibility
  - dependency release
  - authentication
  - toolchain
  - verification planning

  It also lists the 11 preflight tasks, T00–T10.
- **`timed-execution-manifest.json`:** Plan B, the default.
  - 24 timed tasks in 23 steps, with 8 batched CI runs.
  - The clock starts at the first green bootstrap CI and has a 420-minute ceiling. The milestones are those of R4 §5.
  - 103 of 108 P0 requirements are covered by timed tasks. VER-001/002/003/005/010 are preflight work.
  - The DC-001..DC-006 failing-first tests are attached to T12, T16, T18 and T21.
  - T27 and T28 are `conditionalOn: DEC-022` and `excludedBy: DEC-022`. They stay on the plan until the operator's approvals file (T09) covers the exclusion. V16 rejects an exclusion by a decision that is undecided, or by one the task is not conditional on.
  - Workstreams covered: application, persistence, live provider, UI, integration testing, independent review, defect correction and final acceptance.

## Acceptance verifier (indy-init PR #16)

`compute-acceptance-verdict.py` requires independently pinned trust inputs:
- the baseline and its SHA-256;
- the approvals file and its SHA-256, with trusted operator ids;
- an evidence manifest, under ledger schema v2.

CT-015, T09 and T34 are updated to match. The verifier inventories the baseline it is given, so after the kickoff re-pin its baseline inputs must be the re-pinned baseline.

The owner decision record does not replace the approvals file. The T27/T28 exclusion reaches the verifier only through the operator's pinned `praxis.requirements-approvals/1` file.

## Recurring-defect prevention (prepared)

`curation/defect-class-tests.json` has 10 failing-first test specifications: DC-001 (×2), DC-002 (×2), DC-003, DC-004, DC-005, DC-006, `data-on="input"`, and the live write scenarios. Each cites its source record and the design to reuse, such as Rehearsal 3's typed request book and freshness nonce.

V16 fails compilation if any of DC-001..DC-006 loses its test.

## Order of work to reach READY

1. **Upstream:**
   - release Praxis with the gate (F-004);
   - cut, qualify and approve echelon-current 1.28.0 (F-005).
2. **Operator:**
   - provision the test repository and `CAUSA_GITHUB_TOKEN` (T05);
   - write the approvals file (T09), including the T27/T28 exclusion.
3. **Agent:**
   - update the curation statuses that follow from steps 1–2;
   - recompile, which forces citation updates (V11);
   - re-run the preflight in the rehearsal repository's CI, so that PRE-AUTH-02 is meaningful;
   - derive and disposition (T07), then pass the gate (T08) and bootstrap (T10).

The timer starts at T10's first green CI. Do not start Rehearsal 4 before the preflight verdict is READY.

## Re-run

```bash
dotnet build tools/requirements-compiler/tests -c Release
# compile (no Python needed; the baseline is built in-process at the recorded commit)
dotnet tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll --source . \
  --committed-baseline requirements/causa-requirements-baseline.json \
  --curation requirements-compiled/v1/curation --out requirements-compiled/v1 $REPOS
# preflight (writes evidence; exit 0 READY, 3 BLOCKED or FAIL)
dotnet tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll preflight <same options> \
  --evidence requirements-compiled/v1/readiness/evidence/preflight-run.json
```

`$REPOS` passes `--repo NAME=DIR` for every repository in `curation/sources.json`. See the package README.
