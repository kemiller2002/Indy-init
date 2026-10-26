# Independent Causa Acceptance Verification

Date: 2026-10-26  
Scope: acceptance verification only; Rehearsal 4 was not executed.  
Validator implementation commit: `139375d2520079046ef765ea7a224490df7a41be`

## Trusted baselines

- Current Indy-init starting SHA: `b785b68c5172e6bdc257e350032ec851a4637ed4`.
- Requirements manifest: `requirements/causa-requirements-baseline.json`.
- Requirements baseline ID: `BL-CAUSA-INDY-INIT-002d8ba6e986`.
- Requirements source SHA: `002d8ba6e98639a385dd771999099ba032c48086`.
- Requirements manifest SHA-256: `5a4c3c83777c5ce63f017707ff6078a79d3724ea89b931ab0250a2d4ac7a7415`.
- Requirements count: 450 total, 101 P0.
- Dokimos/Praxis evidence baseline: evidence commit `e73a0a0ff040c6dcd33de605156f2cc186530467`, Praxis-state commit `e7c8061f2f142f39dfd106ba8e8cd56c87a96043`, work item `WI-0103`.
- Echelon registry baseline: `2d6338e8286a968ec7e4f356af60dde789797e5a`.

The requirements manifest, approval digest, evidence-manifest digest, source SHA, and trusted operator identities are independent verifier inputs. The acceptance ledger is not a trust root and cannot approve a change to its own baseline.

## Historical results

### Rehearsal 3 — FAIL

- Repository: `kemiller2002/indy-init-rehearsal-003`.
- Historical results record SHA: `bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8`.
- Final remediation candidate: `d54785c3eab94972327aa0da7434fc8eded223f9`.
- Ledger SHA-256: `b611696b54beafeac1b789a30d7d0b7d69ad00a0c48cd1f330221cab0a12ed29`.
- Machine results SHA-256: `2f1571850954d3a6e381ddc22ec020f4bb2414d37fa8934d7b209181452359b1`.
- Original requirements baseline: `BL-CAUSA-INDY-INIT-bff21632a673`, source `bff21632a67390e56ddeaf9ddc8d2344a7a90996`, manifest SHA-256 `084ff71db4a4dc459a14b69596f21e0db05d20ee6939d09da1c43a54e2751455`, 374 requirements and 42 P0.
- CI run `40126993034` and deployment run `40128155698` were green, but green execution did not satisfy acceptance.
- The durable result records 67 PASS, 11 PARTIAL, 12 FAIL, 1 BLOCKED, and 25 NOT_RUN criteria.
- P0 write working copies, save/sync truthfulness, offline/conflict recovery, and provider write behavior were absent. The requirements-preservation gate exited 3 with only 38/42 P0 preserved, and the Percepta release gate was blocked at that time.

Command:

```text
python3 tools/validate-acceptance-ledger.py ../indy-init-rehearsal-003/evidence/REHEARSAL-003-RESULTS.json
```

Exit: 1. The historical `causa.rehearsal-results/1` record is not a v2 machine ledger and was structurally rejected with 16 violations. The FAIL classification above comes from the unchanged historical ledger, machine result counts, source inspection, and preservation evidence rather than from the format error.

### Rehearsal 2 — BLOCKED

- Repository: `kemiller2002/indy-init-rehearsal-002`.
- Reported application candidate: `45644a7689c28f9f46e6110a2553b8c006b5060b`.
- Final ledger commit: `b053d1da4ef533fba7d6728c369c58a29a1e2440`.
- Final ledger SHA-256: `6ab44de130b1059370bc75860f20e4ace77e5629093d5d8ac452126dd48c79bc`.
- Requirements source pinned by `conditor.json`: `b5d9d79ffbbd3966a4e0aef5bc4d6476001b1aa8`.
- The ledger claims G0-G9 PASS and matching source/deployed SHA, but P0 `ADRP-016` is NOT_RUN.
- Live provider run `40041756242` succeeded, but its recorded application commit is `2f8fb98822abfda62e69edde4b295de43846b221`, not candidate `45644a7689c28f9f46e6110a2553b8c006b5060b`.
- The ledger predates the independently pinned requirements, approvals, and content-addressed evidence contract. Its historical acceptance claim therefore cannot be independently established.

Command:

```text
python3 tools/validate-acceptance-ledger.py ../indy-init-rehearsal-002/evidence/acceptance/ledger.json
```

Exit: 1. The final ledger was rejected with 67 violations, including its v1 trust model, the NOT_RUN P0 requirement, unbound PASS evidence, and defect-state incompatibilities.

## Adversarial verification

Command:

```text
python3 tools/test-acceptance-verdict.py -v
```

Exit: 0. Nineteen process-level tests passed. Each A2-A16 mutation invoked the validator, produced a nonzero exit and a non-ACCEPTED machine verdict, named the violated condition, and left the candidate repository unchanged.

| Test | Result | Enforced condition |
|---|---|---|
| A1 valid acceptance | ACCEPTED | All independently pinned identities and evidence validate |
| A2 missing P0 | REJECTED | Missing baseline P0 identity |
| A3 unauthorized priority downgrade | REJECTED | No matching external operator approval |
| A4 blocked gate | REJECTED | Mandatory gate BLOCKED |
| A5 unexecuted gate | REJECTED | Mandatory gate NOT_RUN |
| A6 missing CI evidence | REJECTED | PASS gate lacks evidence and G8 CI evidence |
| A7 stale evidence | REJECTED | Evidence-manifest source differs from candidate |
| A8 source/deployment mismatch | REJECTED | Deployed SHA differs from trusted source SHA |
| A9 nonexistent evidence | REJECTED | Referenced bytes cannot be retrieved |
| A10 self-approved scope reduction | REJECTED | Inline approval is not trusted |
| A11 unauthenticated approval | REJECTED | Claimed operator is not an independently trusted identity |
| A12 unresolved critical defect | REJECTED | Critical defect is BLOCKED |
| A13 contradictory accepted flag | REJECTED | Claim disagrees with computed verdict |
| A14 altered evidence artifact | REJECTED | Artifact bytes differ from manifest SHA-256 |
| A15 missing write functionality | REJECTED | Baseline P0 write requirement is absent |
| A16 cross-run evidence reuse | REJECTED | Evidence run identity differs from ledger run |

Additional controls verify that an externally pinned approval from an independently named trusted operator can authorize a scope change, a report cannot overclaim ACCEPTED, and the historical Rehearsal 2 ledger is not independently acceptable.

## Repository verification

Commands and results:

```text
python3 tools/test-preflight.py                 # exit 0, 9 tests
python3 tools/test-acceptance-ledger.py         # exit 0, 11 tests
python3 tools/test-acceptance-verdict.py        # exit 0, 19 tests
python3 tools/test-requirements-baseline.py     # exit 0
python3 -m compileall -q tools                  # exit 0
git diff --check                                # exit 0
```

The existing `.github/workflows/preflight-tooling.yml` runs all four test commands and Python compilation on pushes and pull requests. GitHub reported no branch protection and no repository rulesets for `main` at verification time, so this report does not characterize that CI as non-bypassable.

## Implementation result

The v2 validator now requires:

1. An externally pinned requirements baseline and its source documents at the named commit.
2. Complete baseline P0 coverage, including write requirements.
3. Scope changes that match an externally pinned approval from an independently named trusted operator.
4. Candidate and deployment identities matching an independently supplied source SHA.
5. Content-addressed, retrievable evidence bound to the same run, source, subject, status, command, and successful exit code.
6. Specific CI, deployment, and independent defect-review evidence types.
7. PASS for all mandatory gates and P0 requirements and closure of high/critical defect classes.
8. A computed verdict that cannot be overridden by the ledger or a report.

No application functionality or supporting Echelon repository was modified. No Praxis work item was created because Indy-init exposes no local `praxis`/`ros.json`, `.ros` state, `AGENTS.md`, or documented Praxis intake directory; no intake path was invented.

## Verdict

PASS — the validator independently enforces the pinned requirements baseline, authorization boundary, evidence integrity, source/deployment identity, and mandatory gates. All positive and adversarial controls pass. Rehearsal 3 is correctly classified FAIL, and Rehearsal 2 is conservatively BLOCKED because authentic evidence is insufficient for historical acceptance.
