# Rehearsal 4 readiness report

**Verdict: BLOCKED. Rehearsal 4 must not start.**

- The preflight's computed verdict is FAIL: PRE-REQ-01 is the baseline drift (F-003).
- 16 readiness blockers remain. They have 8 distinct causes, and each needs the owner or an upstream release.
- Every resolution that this repository can prepare has been prepared and is waiting for approval.

## Revisions and sources

| | |
|---|---|
| Source | kemiller2002/indy-init `9d98938` (main: package v1 plus indy-init PR #16, the independent acceptance verifier) |
| Package | `requirements-compiled/v1`, compilation **PASS**. Critical checks V01–V13 and V15–V17 pass; V14 warns about the drift. |
| Requirements | 466 inventoried (460 baseline at `9d98938` plus 6 external), 2322 obligations. PR #16 added 30 obligations to the acceptance-ledger contract. All are preserved; none is marked verified. |
| Evidence | `evidence/preflight-run.json` (executed), `evidence/preflight-owner-branch-projection.json`, `evidence/auth-contract-probe.json`, `evidence/baseline-equivalence.json` |

## Blockers: classification and state

| Blocker | Class | Cause | Prepared here | Needs |
|---|---|---|---|---|
| F-001, DEC-024 | Indy-init requirement conflict | UI-001/UI-015 require five demo investigations on launch | Requirement edit (UI-001-O1, UI-015-O2, new EXT-DATA-013) on branch `claude/r4-owner-requirement-changes` | Owner merge |
| F-003 | Verification deficiency | Baseline pinned at 002d8ba lacks AR-019..AR-026 | Regenerated baseline on the same branch; PRE-REQ-01 passes there (projection evidence) | Owner merge; re-pin at kickoff (T01) |
| F-009, DEC-006, CT-007 | Contract incompatibility | Storage contract §9.4.1 mandates a REST update that cannot compare-and-swap | Exception text (`CONTRACT-RESOLUTION.md` §1) | Owner decision |
| F-008, DEC-022 | Owner decision | AI loop and time travel scope | Recommendation: exclude from R4 with approval | Owner decision plus approval records |
| DEC-025, CT-017 | Owner decision | Authentication mechanism undecided | CT-017 defined (fine-grained token, memory only), plus the F# auth check PRE-AUTH-02 with a negative control | Owner decision |
| DEC-023 | Owner decision | Reuse of earlier rehearsal interfaces at the event | Recommendation | Owner decision; organizer for the event |
| F-004, CT-013 | External dependency | No Praxis release carries the preservation gate (PRE-REQ-03, executed) | The minimum release is identified (`CONTRACT-RESOLUTION.md` §3) | Praxis release, or an owner VER-005 exception |
| F-005, CT-012, CT-016 | External dependency | percepta-semantic 0.2.1 is now a registered release, but in no approved set (PRE-DEP-01, executed) | The contents of 1.28.0 are specified | Registry set 1.28.0, Conditor qualification, owner approval |

**Counts:**
- 16 at the start of this work, and 16 still open: no blocker can be closed without the owner or an upstream release.
- Resolution prepared and waiting for owner approval: 11. These are F-001, DEC-024, F-003, F-009, DEC-006, CT-007, F-008, DEC-022, DEC-025, CT-017 and DEC-023.
- Waiting on upstream releases: 5. These are F-004, CT-013, F-005, CT-012 and CT-016.
- Upstream progress since package v1: percepta-semantic 0.2.1 was registered as a release (Registry PR #96/#97, by the owner), and the Conditor SD-1 fix is confirmed released in v0.10.11.

## Preflight (executed: `evidence/preflight-run.json`)

| Check | Status | Detail |
|---|---|---|
| PRE-DEC-01 owner decisions | BLOCKED | DEC-006, DEC-022, DEC-023, DEC-024, DEC-025 unresolved |
| PRE-DEC-02 critical findings | BLOCKED | F-001, F-003, F-004, F-005, F-008, F-009 open |
| PRE-REQ-01 baseline drift | **FAIL** | Pinned 002d8ba differs from the generator's output at 9d98938 (passes on the owner branch) |
| PRE-REQ-02 package compiles | PASS | |
| PRE-REQ-03 Praxis gate released | BLOCKED | v3.20.0 has no `preservation` command |
| PRE-REQ-04 gate PASS on the derived package | NOT_RUN | Needs T07–T09 |
| PRE-DEP-01 release set | BLOCKED | echelon-current 1.27.0 lacks percepta-semantic |
| PRE-DEP-02 contracts | BLOCKED | CT-007, CT-012, CT-013, CT-016 |
| PRE-DEP-03 identities | PASS | 5 of 5 match |
| PRE-AUTH-01 CI secret | BLOCKED | CAUSA_GITHUB_TOKEN and CAUSA_TEST_REPO are not set here (T05) |
| PRE-AUTH-02 auth contract | NOT_RUN | Needs the secret. The negative-control probe showed this sandbox rewrites credentials, so it must run in CI |
| PRE-SEC-01 committed secrets | PASS | No token patterns at 9d98938 |
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
  - 102 of 107 P0 requirements are covered by timed tasks. VER-001/002/003/005/010 are preflight work.
  - The DC-001..DC-006 failing-first tests are attached to T12, T16, T18 and T21.
  - T27 and T28 are `conditionalOn: DEC-022`.
  - Workstreams covered: application, persistence, live provider, UI, integration testing, independent review, defect correction and final acceptance.

## Acceptance verifier (indy-init PR #16, merged by the owner during this work)

`compute-acceptance-verdict.py` now requires independently pinned trust inputs:
- the baseline and its SHA-256;
- the approvals file and its SHA-256, with trusted operator ids;
- an evidence manifest, under ledger schema v2.

CT-015, T09 and T34 are updated to match. The verifier inventories the baseline it is given. After PR #18 and the kickoff re-pin, the verifier's baseline inputs must be the re-pinned baseline.

## Recurring-defect prevention (prepared)

`curation/defect-class-tests.json` has 10 failing-first test specifications: DC-001 (×2), DC-002 (×2), DC-003, DC-004, DC-005, DC-006, `data-on="input"`, and the live write scenarios. Each cites its source record and the design to reuse, such as Rehearsal 3's typed request book and freshness nonce.

V16 fails compilation if any of DC-001..DC-006 loses its test.

## Order of work to reach READY

1. **Owner:**
   - merge the requirement-change PR (F-001, F-003);
   - decide DEC-006, DEC-025, DEC-022 and DEC-023 (`OWNER-DECISION-REQUESTS.md`).
2. **Upstream:**
   - release Praxis with the gate;
   - cut, qualify and approve echelon-current 1.28.0.
3. **Operator:** provision the test repository and `CAUSA_GITHUB_TOKEN` (T05).
4. **Agent:**
   - update the curation statuses that follow from steps 1–3;
   - recompile, which forces citation updates (V11);
   - re-run the preflight in the rehearsal repository's CI, so that PRE-AUTH-02 is meaningful;
   - derive and disposition (T07), then obtain the operator's approvals (T09);
   - pass the gate (T08), then bootstrap (T10).

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
