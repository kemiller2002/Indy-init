# Rehearsal 4 results (index for agents)

**Outcome: NOT ACCEPTED.** The authoritative verifier (`tools/compute-acceptance-verdict.py`) computed **FAILED** for the deployed candidate `ab08b76`. The core write path worked live; most P0 requirements were not evidenced, and one runtime defect stayed open.
**Repository:** [kemiller2002/indy-init-rehearsal-004](https://github.com/kemiller2002/indy-init-rehearsal-004). The links below are pinned to the run's final commit `b6947b8`, except the remediation links, which are pinned to `1681080` on `remediation/r4`.
**Next:** the remediation in that repository ([plan](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/1681080cc224dceac7c85e426fcd4f3122381ba0/docs/rehearsal/R4-REMEDIATION-PLAN.md)). It is in progress and is not recorded here.

## Machine-readable records

- [Verdict](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/evidence/acceptance/verdict-ab08b764daddde9e978a78fef957d9ae01505c6a.json): FAILED, with every reason.
- [Ledger](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/evidence/acceptance/ledger-ab08b764daddde9e978a78fef957d9ae01505c6a.json) (`causa.acceptance-ledger/2`, CI run 40490687398): per-requirement, gate and defect-class status, with the gap behind each one that does not pass.
- [Traceability](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/acceptance/traceability.json): each P0 requirement mapped to named checks.
- [Clock](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/evidence/rehearsal/clock.json) and [checkpoints](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/evidence/rehearsal/checkpoints.json).
- Reviews: [T30](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/evidence/reviews/T30-findings-ca52aae.json) and [T33](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/evidence/reviews/T33-findings-6950158.json).

## Key facts

| Item | Value |
|---|---|
| Requirements baseline | `BL-CAUSA-INDY-INIT-aab8f80c7743` (this repository at `aab8f80`, sha256 `621e2f96…57b9`); preflight frozen in [`kickoff/rehearsal-004/preflight-record.json`](../kickoff/rehearsal-004/preflight-record.json) |
| Compiled package | [`requirements-compiled/v1`](../requirements-compiled/v1/README.md), frozen by digest in the preflight record (`package-manifest.json` sha256 `0cd4c3f5…ace7`). It is unchanged since the kickoff, so its readiness report is the pre-run state. No `v2/` was published; the next package change starts one (v1 README, "For a new run", step 4) |
| Kickoff set | echelon-current 1.28.0; dependency lock sha256 `719be140…3337` |
| Clock | Start 2026-10-27 06:04:39 UTC (first green bootstrap CI), verdict 11:00:31 UTC: 296 of 420 minutes. The owner stopped the run at the report. |
| Candidate | `ab08b764daddde9e978a78fef957d9ae01505c6a`, deployed to GitHub Pages; the site reports the same commit |
| P0 requirements | 108: 1 PASS (EXT-DATA-002), 9 FAIL, 98 NOT_RUN. No P0 excluded; T27 and T28 excluded under APR-R4-001 (OD-INDY-11) |
| P0 FAILs | PF-005, PF-011, PF-012, PF-017, VER-003, VER-006, AR-010, AR-013, AR-022. Apart from PF-005, which failed in the browser suite, each FAIL is a named gap with no check: the provider-contract layer, the fixture mode, no upgrade gate for the newest release, no test against the deployed Pages URL, no dependency-based test selection, no injected-defect baseline run |
| Gates | G0, G3, G9 PASS; G5 (UI), G8 (candidate) FAIL; G1, G2, G4, G6, G7 NOT_RUN |
| Defect classes | 6 PASS; DCT-001-A, DCT-002-A, DCT-002-B, DCT-INPUT-A NOT_RUN (T33 found them open at `6950158`) |
| Checks in the run | 160 found, 156 passed, 4 failed |
| Open runtime defect | D-HIST-WASM: each historical case loads and replays alone, but loading all three in one page fails (EXT-DATA-013-O2). Mitigated, not fixed |
| Preservation gate | PASS before implementation and at the candidate, run locally rather than as a CI step |

## Against the plan

[REHEARSAL-004-WRITE-PATH-PLAN.md](REHEARSAL-004-WRITE-PATH-PLAN.md) §6 maps the result to a consequence. The run was not ACCEPTED. The live write path (create, write, reload, conflict, rejected token) was green in CI at T+36 minutes and held at the two-hour gate, so the result is the second row: "Not ACCEPTED, but write milestones within 300 minutes".

The plan's consequence for that row is "Cut P1 UI scope for the event. Keep P0". The event scope is the owner's decision. For the remediation, the owner has answered OD-8 ("demonstration-ready core first"; the unaddressed P0 requirements stay open, none is downgraded): [R4-REM-OWNER-DECISIONS.md](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/1681080cc224dceac7c85e426fcd4f3122381ba0/docs/decisions/R4-REM-OWNER-DECISIONS.md).

## Reuse

| Design | Where (indy-init-rehearsal-004 at `b6947b8`) |
|---|---|
| Publication state machine (request book): DC-001 to DC-003 as rules, each with a failing-first test | [`src/causa/Sync.fs`](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/src/causa/Sync.fs) |
| Guarded save: `createCommitOnBranch` with `expectedHeadOid`; `STALE_DATA` mapped to a conflict | [`src/causa/vendor/GitHub.fs`](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/src/causa/vendor/GitHub.fs) |
| Storage contract v1.0.0 in and out, with integrity proof on read | [`src/causa/Store.fs`](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/src/causa/Store.fs) |
| Full replay of all 31 event types to the projected state | [`src/causa/Replay.fs`](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/src/causa/Replay.fs) |
| T07 identity derivation from the pinned baseline (F#) | [`tools/derive-requirements/Program.fs`](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/tools/derive-requirements/Program.fs) |
| Acceptance pipeline: CI reports to evidence bundle to ledger; the verifier computes the verdict | [`tools/acceptance/build-evidence.mjs`](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/tools/acceptance/build-evidence.mjs) and [`verify.sh`](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/tools/acceptance/verify.sh) |

## Deviations

All are recorded in the report.

- **RDP-003, twice.** The coding agent wrote both approvals files on the owner's instruction: APR-R4-001 ([`kickoff/rehearsal-004/approvals.json`](../kickoff/rehearsal-004/approvals.json)) and the verifier-format [`approvals.causa.json`](../kickoff/rehearsal-004/approvals.causa.json). Ratification is still open (remediation OD-3).
- **R4-DEC-001.** `@echelon-foundry/forma-workflow` 1.0.0 is owner-approved but not in the Registry (remediation OD-6, open).
- **Conditor 0.10.13**, released during preflight, is not in the 1.28.0 qualification record, which names 0.10.12.
- **The Python verifier** ran locally over the CI-published evidence.
- **Owner settings** were changed during the run: the Pages source, the default branch, and the github-pages environment.
- **Token exposure.** The live token appeared in Playwright traces of a failed run. The owner rotated it, the traces were deleted, and the cause was fixed in `64ae19b`.

## Lessons

1. **Prove the acceptance formats in the first hour.** The approvals file was in the Praxis format (`praxis.requirements-approvals/1`), and the verifier needs `causa.requirements-approvals/1`. That was found at T+60. The kickoff package should carry the verifier-format file.
2. **Map each P0 requirement to a precise test before implementing it.** Eight of the nine FAILs and most NOT_RUNs are gaps in evidence, not defects found by a test.
3. **Persistence fixes must reach the application level.** Fixing the state machine left the application paths unguarded, so DCT-001-A and DCT-002-B came back (T33 R2-01 to R2-05).
4. **Debug the WebAssembly build locally.** nuget.org was blocked in the sandbox, so D-HIST-WASM took about 2 hours of CI round trips.
5. **No traces for suites that handle secrets**, and gate deployed evidence on the write path only, so one failing live scenario does not block all deployed evidence.

## Reports

- [Rehearsal 4 report](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/docs/rehearsal/REHEARSAL-004-REPORT.md) (computed verdict line, metrics, comparison, findings, deviations)
- [R4-DEC-001](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/b6947b8f69ee16e9e8e10f706b15bd1c4d22f9b1/docs/decisions/R4-DEC-001-forma-workflow-dependency.md)
- [Remediation plan](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/1681080cc224dceac7c85e426fcd4f3122381ba0/docs/rehearsal/R4-REMEDIATION-PLAN.md), [remediation ledger](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/1681080cc224dceac7c85e426fcd4f3122381ba0/evidence/remediation/remediation-ledger-ab08b76.json) and [owner decisions](https://github.com/kemiller2002/indy-init-rehearsal-004/blob/1681080cc224dceac7c85e426fcd4f3122381ba0/docs/decisions/R4-REM-OWNER-DECISIONS.md) (OD-1, -2, -7, -8 answered; OD-3 to OD-6 open)
- [Plan](REHEARSAL-004-WRITE-PATH-PLAN.md) and [readiness report](../requirements-compiled/v1/readiness/READINESS-REPORT.md) (pre-run records)
- [Rehearsal 3 results](REHEARSAL-003-RESULTS.md)
