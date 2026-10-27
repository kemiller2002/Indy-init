# Rehearsal 4 readiness report

**Verdict: no readiness blocker remains. The preflight is not yet READY: two of its checks are records that Rehearsal 4's own untimed setup produces. The timed run must not start before the preflight is READY.**

- The preflight in CI with the secret (2026-10-26, run 40420386699): 14 PASS, 3 NOT_RUN, 0 BLOCKED, 0 FAIL. Its rule needs every check at PASS, except PRE-ORG-01 (event only). The two remaining checks are produced in the Rehearsal 4 repository:
  - PRE-REQ-04: the preservation gate passes (T07 derive, T09 operator approvals, T08 gate);
  - PRE-TOOL-03: the Conditor bootstrap reaches first green CI (T10). The timer starts there.
- The kickoff release set is approved: echelon-current 1.28.0 (Registry `e6d8a734`, echelon-registry#98) selects percepta-semantic 0.2.1, Praxis 3.21.0, Dokimos 0.7.0 and Folio 0.4.1, qualified by released Conditor 0.10.12 and approved by the owner (OD-INDY-8). F-005, CT-012 and CT-016 are resolved.
- The Praxis preservation gate is released: Praxis 3.21.0 (2026-10-26) carries it, and the published artifact was verified against the pinned baseline; see "Praxis requirements-preservation release" below.
- T05 is verified (2026-10-26). The secret and the isolated test repository work end to end with the approved guarded write; see "T05 persistence verification" below.
- The owner's decisions so far:
  - The owner approved #17 and #18 on 2026-10-26, and they were merged on that instruction. The merge of #18 decided F-001/DEC-024 and F-006, and fixed the baseline drift (F-003).
  - OD-INDY-9 to OD-INDY-13 (`docs/OWNER-DECISIONS-2026-10-26.md`) decide DEC-006, DEC-025, DEC-022 and DEC-023, and confirm OD-INDY-3/4. The owner approved them in #20.

## Revisions and sources

| | |
|---|---|
| Source | kemiller2002/indy-init `9dc7d57`: main with #16 (acceptance verifier), #17 (readiness tooling), #18 (owner-approved requirement changes), #19, and the owner decision record of 2026-10-26 |
| Package | `requirements-compiled/v1`, compilation **PASS**: all checks V01–V17 pass, and V14 confirms there is no baseline drift |
| Requirements | 467 inventoried (461 baseline, including EXT-DATA-013, plus 6 external), 2325 obligations, 108 P0. All are preserved; none is marked verified. |
| Evidence | `evidence/preflight-run.json` (all 17 checks executed in CI with the secret: indy-init-rehearsal-003 run 40420386699, on this package with the echelon-current 1.28.0 curation, Indy-init `04ea5f2`), `evidence/preflight-run-praxis-3.21.0.json` (historical: all 17 checks, run locally after the Praxis 3.21.0 release), `evidence/praxis-preservation-verification.json` and `-candidate.json` (the Praxis gate on the real baseline), `evidence/t05-persistence-verification.json`, `evidence/auth-contract-probe.json`, `evidence/baseline-equivalence.json`. `evidence/preflight-owner-branch-projection.json` is historical: the pre-merge projection of #18. |

## Blockers: classification and state

| Blocker | Class | Cause | Prepared here | Needs |
|---|---|---|---|---|
| ~~F-001, DEC-024~~ | Indy-init requirement conflict | UI-001/UI-015 required five demo investigations on launch | **Resolved:** owner merge of #18 (UI-001-O1, UI-015-O2, EXT-DATA-013) | — |
| ~~F-003~~ | Verification deficiency | Baseline pinned at 002d8ba lacked AR-019..AR-026 | **Resolved:** owner merge of #18; PRE-REQ-01 PASS | Re-pin at the kickoff commit (T01), as planned |
| ~~F-009, DEC-006, CT-007~~ | Contract incompatibility | Storage contract §9.4.1 mandates a REST update that cannot compare-and-swap | **Resolved:** OD-INDY-9, an exception to §9.4.1 (`CONTRACT-RESOLUTION.md` §1). CT-007 is unverified until T18/T21. | — |
| ~~F-008, DEC-022~~ | Owner decision | AI loop and time travel scope | **Resolved:** OD-INDY-11 excludes T27 and T28 from Rehearsal 4. They carry `excludedBy: DEC-022`. | The operator's approvals file (T09) before they leave the run |
| ~~DEC-025, CT-017~~ | Owner decision | Authentication mechanism undecided | **Resolved:** OD-INDY-10, a fine-grained token held in memory (`CONTRACT-RESOLUTION.md` §2). CT-017 is unverified until PRE-AUTH-02/T21. | — |
| ~~DEC-023~~ | Owner decision | Reuse of earlier rehearsal interfaces at the event | **Resolved:** OD-INDY-12 | Organizer confirmation for the event (PRE-ORG-01) |
| ~~F-004, CT-013~~ | External dependency | No Praxis release carried the preservation gate | **Resolved:** Praxis 3.21.0 (`fa474c42`). PRE-REQ-03 PASS; CT-013 identity-verified (four contract schemas); published artifact verified; selected by echelon-current 1.28.0 | — |
| ~~F-005, CT-012, CT-016~~ | External dependency | percepta-semantic 0.2.1 was registered but in no approved set | **Resolved:** echelon-current 1.28.0 approved (Registry `e6d8a734`), qualified by released Conditor 0.10.12 (REG-RU-007 PASS). PRE-DEP-01 and PRE-DEP-02 PASS. CT-012 identity-verified (Percepta contract and evidence schemas at `percepta-semantic-v0.2.1`, Registry release record); CT-016 identity-verified (approved set record, channel index, linux-x64 set, Conditor 0.10.12 record) | — |

**Counts:**
- 16 at the start of this work.
- All 16 are resolved:
  - 3 by the owner-approved merge of #18: F-001, DEC-024 and F-003;
  - 8 by OD-INDY-9 to OD-INDY-12: F-009, DEC-006, CT-007, F-008, DEC-022, DEC-025, CT-017 and DEC-023.
  - 2 by the Praxis 3.21.0 release: F-004 and CT-013.
  - 3 by the approved echelon-current 1.28.0: F-005, CT-012 and CT-016.
- Also resolved, though they were not readiness blockers: F-006 (#18), F-002, F-012, F-013 and F-019 (OD-INDY-10, -12 and -13), and F-015 (Folio 0.4.1 assessed and selected in 1.28.0).
- None is open.
- Upstream: percepta-semantic 0.2.1 registered (Registry #96/#97); Praxis 3.21.0, Conditor 0.10.12 and echelon-current 1.28.0 released and approved (Registry #98, conditor#111); the Conditor SD-1 fix is in v0.10.11 and later.

## Preflight (executed)

14 PASS, 0 BLOCKED, 3 NOT_RUN, 0 FAIL: `evidence/preflight-run.json`, all 17 checks executed in CI with the secret (the T05 workflow, indy-init-rehearsal-003 run 40420386699, on Indy-init `04ea5f2`). The same run repeated T05's guarded write against the test repository: PASS.

A local run of the same package, without the secret, gives the same results except PRE-AUTH-01 BLOCKED and PRE-AUTH-02 NOT_RUN.

| Check | Status | Detail |
|---|---|---|
| PRE-DEC-01 owner decisions | PASS | No unresolved decision |
| PRE-DEC-02 critical findings | PASS | No open critical finding |
| PRE-REQ-01 baseline drift | PASS | Pinned at c751d58; no drift at 9dc7d57 |
| PRE-REQ-02 package compiles | PASS | |
| PRE-REQ-03 Praxis gate released | PASS | v3.21.0 contains `preservation` under src/Praxis.Cli |
| PRE-REQ-04 gate PASS on the derived package | NOT_RUN | Needs T07–T09 |
| PRE-DEP-01 release set | PASS | `sets/echelon-current/1.28.0.json` (approved) contains percepta-semantic, Dokimos, Praxis, Limen, Forma, Folio, Ordo and Aegis |
| PRE-DEP-02 contracts | PASS | No blocked contract |
| PRE-DEP-03 identities | PASS | 16 of 16 match, including the Praxis 3.21.0 contract schemas (CT-013), the Percepta semantic schemas (CT-012) and the 1.28.0 set (CT-016) |
| PRE-AUTH-01 CI secret | PASS | CAUSA_GITHUB_TOKEN and CAUSA_TEST_REPO set in the authorized workflow (values not read) |
| PRE-AUTH-02 auth contract | PASS | In CI: invalid token refused (401); test repository readable (200); fine-grained token (no classic scopes) |
| PRE-SEC-01 committed secrets | PASS | No token patterns at 9dc7d57 |
| PRE-TOOL-01 SDK | PASS | 10.0.402 = global.json |
| PRE-TOOL-02 Conditor SD-1 | PASS | v0.10.12, the latest release, contains CON-367 |
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

1. **Upstream:** done. echelon-current 1.28.0 is cut, qualified by released Conditor 0.10.12 and approved (F-005), with Praxis 3.21.0, the release that carries the gate.
2. **Agent:** done for step 1. The curation statuses are updated (F-005, F-015, CT-012, CT-016), the package is recompiled (V11 citations pass), and the preflight is re-run in CI with the secret (run 40420386699: 14 PASS, 3 NOT_RUN).
3. **Rehearsal 4 setup (untimed), in the rehearsal repository:**
   - derive and disposition (T07), recording the exclusions the derivation requests;
   - the operator writes the approvals file (T09), including the T27/T28 exclusion. It covers exactly the ids T07 requests, and is pinned by SHA-256 outside the agent's reach;
   - pass the gate (T08): PRE-REQ-04;
   - bootstrap (T10): PRE-TOOL-03.

The timer starts at T10's first green CI. Do not start Rehearsal 4 before the preflight verdict is READY.

## T05 persistence verification (2026-10-26, executed)

`evidence/t05-persistence-verification.json`, from indy-init-rehearsal-003 workflow run 40352698801. The run used the `CAUSA_GITHUB_TOKEN` secret against `kemiller2002/causa-write-test` (branch `main`), with no fallback to the workflow token. The tool is the F# verifier `tools/persistence-verification` (Indy-init e7a5a42), which compiles the approved adapter, indy-init-rehearsal-002 `src/engine/GitHub.fs` at b053d1d.

| Check | Result |
|---|---|
| Negative control | An invalid token gets 401 |
| Authentication | 200 for the test repository; a fine-grained token |
| Guarded write | `createCommitOnBranch` with `expectedHeadOid` 3e19eba created 6ace866, whose only parent is 3e19eba |
| Readback | 154 bytes, identical (sha256 666ffe49…) |
| Stale write | Expected head 3e19eba after 6ace866: refused by GitHub (`STALE_DATA`, adapter `NotFastForward`); branch still at 6ace866, data intact |
| Cleanup | Guarded deletion 55dd105; the branch tree equals the starting tree (acfcd2c) |
| Production | kemiller2002/causa-data main unchanged at 22260f4 |
| Credentials | Absent from the probe files, the evidence and the preflight output (in-run screen); the log shows only masked values |

Two owner follow-ups, neither of which affects the result:
- **Test repository visibility:** the test repository is public, but the T05 brief asks for a private repository where supported.
- **`CAUSA_TEST_REPO` variable:** it is not set in indy-init-rehearsal-003, so the workflow names the repository directly. The variable takes precedence once it is set.

## Praxis requirements-preservation release (2026-10-26, executed)

Praxis **3.21.0** carries `praxis requirements preservation verify`, hardened by REQ-PRES-004 with REQ-PRES-001 R1 and R2:

| | |
|---|---|
| Release | <https://github.com/kemiller2002/praxis/releases/tag/v3.21.0>, stable, tag at `fa474c42`. The release bump is `806df9ce`; PR praxis#292 merged as `956bf429`. |
| Artifact | `praxis-linux-x64.tar.gz`, sha256 `eae9c427460468cc1e01f6951684e72fc4fe882ab1982e60d90d1aeb9afd3eef` (matches `native-checksums.txt` and GitHub's digest). Installed with the documented installer `scripts/install-native.sh --version 3.21.0`. `praxis-bin` sha256 `70176412…`. |
| CI | Praxis PR CI: the full suite 2411/2411 with Dokimos 0.7.0, plus lifecycle and worker suites on Linux, macOS and Windows. Release: native-release run 40378037642 (tests and validation, then publish). |
| Not published | NuGet: publishing is disabled by repository configuration (`NUGET_PUBLISH_ENABLED`). The GitHub release is the distribution channel. |

`evidence/praxis-preservation-verification.json` is the published binary run by `tools/praxis-preservation-verification` against the pinned baseline (`c751d58`, 461 requirements, 2,293 obligations). The fixture is an identity derivation of this compiled package; each case changes it once:

| Case | Expected | Actual |
|---|---|---|
| P1 missing P0 (UI-016) | reject | fail `disposition-missing` |
| P2 P0 carried at P1; P2b acceptance obligation weakened | reject | fail `priority-downgraded`; fail `acceptance-criterion-missing` |
| P3 no approval; P3b agent approval; P3c untrusted approver | reject | fail `approval-missing`; `approval-agent-authored`; `approval-untrusted` |
| P4 baseline altered (UI-016 P0 to P1) | reject | fail `baseline-digest-mismatch` |
| P5 requirement id changed | reject | fail `disposition-unknown-requirement`, `disposition-missing` |
| P6 completion on an agent's review; P6b on a missing criterion | reject | blocked `review-untrusted`; fail `criterion-not-found` |
| P7 disposition for baseline 002d8ba; P7b approvals from another run | reject | fail `baseline-mismatch`, `baseline-digest-mismatch`; fail `approvals-digest-mismatch` |
| P8 exclusion approved by the trusted operator | accept, traceable | pass; UI-016 listed as an approved exclusion |
| P9 unchanged identity derivation | accept | pass (461 requirements, 0 findings above info) |
| P10 no baseline digest; P10b approvals unpinned; P10c baseline unreadable | block | blocked `baseline-digest-missing`; blocked `approvals-unauthenticated`; exit 2, no verdict |

Every case ran twice, byte-identically. With output paths normalized, the evidence of all 16 evidence-producing cases is byte-identical to the pre-release candidate build (`evidence/praxis-preservation-verification-candidate.json`).

**Limits.**
- Approval trust rests on a caller-supplied digest and trusted identities, not a signature.
- REQ-PRES-001 R3 to R5 are not implemented.
- PRE-REQ-04, the gate passing on the rehearsal's own derived package, remains a T08 record.

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
