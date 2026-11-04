# Rehearsal 010 final launch verification

**Verdict: READY FOR OWNER ACTION.**
- Engineering is complete and verified at `7e6dca7`.
- The launch preflight refuses today, and only for owner actions that have not happened yet: [`evidence/launch-preflight/launch-preflight-now.log`](evidence/launch-preflight/launch-preflight-now.log).
- No real-provider test has run, so **READY TO LAUNCH is not reported.**
- Rehearsal 010 has not been started. The competition repository holds only its initial commit, and no T0 record exists.

## 1. The preflight, item by item

| Requirement | Status | Evidence |
|---|---|---|
| v4.1.4 frozen, tagged, verified | **owner action**: ODR-V414-01..03 pending; tag not created | `owner-freeze.fsx status`; LP-02, LP-05 |
| Dokimos 0.11.1 published | **owner action**: dokimos#69 ready (version bump only, on #67's url-input merge, CI green); not merged, tagged or published | LP-06 |
| New echelon-current set approved and Conditor-qualified | **pending**: needs the published 0.11.1 artifacts (OWNER-APPROVAL-EXECUTION step 6a) | LP-06 |
| Toolchain lock committed | **owner action**: `toolchain.lock.json` does not exist (only the proposal) | LP-01 |
| Scope digest matches | proposal consistent: manifest `5a8aef29…` equals the proposed lock. **Not final**: the digest changes when v4.1.4 is recompiled with the approval IDs, and on ratification | LP-04; `scope preflight` in the offline suite PASS |
| Contract digests match: catalog contract, design contract, deadline policy, journey bindings, owner-decision requests | PASS against the proposed lock | LP-03 on the committed lock |
| Process-evidence rules pinned | **owner action**: Rehearsal 010 rules drafted; `rulesSha256` null until ODR-R10-14 | LP-03 |
| Praxis inventory complete | PASS: 36 of 36 work items; `praxis-work-inventory.json` = `scope work-plan`; `slice-manifest` COMPLETE COVERAGE | offline suite; CI |
| All suite commands executable | PASS on the deterministic fixture (v4.1.4, every suite, WebKit included) | CI `suite-commands`; release-gate-repair |
| Zero-test refusal operational | PASS: suite-preflight `--require-tests` and run-suites zero-executed refusal | C01; release-gate-repair §2b |
| Chromium and WebKit operational | PASS in CI (`r10-adversarial`, `suite-commands`). Chromium only in this container | CI |
| Live-provider acceptance preflight | **NOT RUN**: needs the test repository, App and secrets (owner). The test repository is not visible to this session | LIVE-PROVIDER-PREFLIGHT.md; LP-08 |
| Independent review complete | 11 rounds; the last blocking item, round 11's false refusal, is fixed in `7e6dca7` | FINAL-READINESS §5 |
| Adversarial checks pass | 36 of 36 VALIDATED locally; LP-09 reads them from the Preflight tooling CI run of the launched tree | `evidence/adversarial/`; LP-09 |
| Specula observer ready | adapter (specula#11) is read-only and parses the current observer output (6/6 tests); owner merge pending | FINAL-READINESS §1 |
| Topology A validated | the execution topology is defined and simulated. **The full scope does not fit 420 minutes at any CI latency** (ODR-R10-18) | `evidence/schedule/` |
| Rehearsal repository clean | PASS: `kemiller2002/indy-init-rehearsal-010` exists with one initial commit (`616ee17`, README) | LP-10 |
| No T0 record or clock started | confirmed | LP-10 |

## 2. What the launch refuses: 47 negative controls

[`tools/r10/launch-preflight-tests.fsx`](../../tools/r10/launch-preflight-tests.fsx) builds synthetic fixtures:
- an Indy-init with its published origin;
- a published competition repository;
- real GPG keys for "the owner" and "an agent";
- a `gh` stand-in that answers GitHub's commit, pull-request, key and run endpoints as GitHub does.

**Two positive controls are READY:** the plain fixture, and an owner PR merged with a merge commit. Each of the other 45 cases is refused by the check it targets. CI runs them in `r10-adversarial`. Results: [`evidence/launch-preflight/controls/`](evidence/launch-preflight/controls/).

| Check | Refusal cases |
|---|---|
| LP-00 publication | uncommitted change; checkout is not the published `main` |
| LP-01 lock | still a proposal; changed by an agent |
| LP-02 package | v4.1.4 not tagged on origin |
| LP-03 pins | design contract altered; requests file edited to allow B |
| LP-04 scope | scope altered |
| LP-05 owner records | see the list below |
| LP-06 tools | Dokimos 0.11.0 |
| LP-07 test repository | not isolated; not the approved one |
| LP-08 live run | see the list below |
| LP-09 adversarial run | see the list below |
| LP-10 competition repository | clock already started; a second root commit; unreadable |

LP-05 (owner records) refuses:
- a missing approval;
- an approval by someone else;
- an approval committed by an agent;
- an owner approval edited in place by an agent;
- a reused approval ID;
- a duplicate ID, even when signed;
- an agent-signed commit with the owner as author;
- an agent key signing as the owner committer;
- a planted git config that fakes local verification;
- a merge that rolls back an owner decision;
- an abandoned owner branch merged by someone else;
- a graft that hides an agent commit;
- a record planted under another ID;
- a decision recorded as do-not-approve.

LP-08 (live run) refuses:
- no run (absent credential);
- a run against the double;
- a run on a branch;
- a run of another workflow file;
- a failed run;
- a run of another commit;
- a run from before the journeys changed;
- a run against another test repository;
- a failed journey;
- a duplicate row that hides a failure.

LP-09 (adversarial run) refuses:
- no run;
- a run of another workflow file;
- a run on a branch;
- a failed run;
- a case not validated;
- a missing case;
- a run of older tools.

**More refusals that need no fixture:**
- `owner-freeze.fsx` refuses every write under an agent. Shown: `decide rehearsal-010` with `CLAUDECODE` set.
- It refuses to record while the approvals file holds a change you did not sign.
- The governed workflow fails closed without the App secrets.
- The governed workflow fails when a Rehearsal 010 lock names no isolated test repository.

## 3. Verification

| Check | Result |
|---|---|
| Build: requirements-compiler, tests (Debug) and src (Release) | PASS |
| Offline suite | PASS (all groups) |
| `tools/release-gate-repair/tests.fsx` | 74 cases, 0 failed |
| `tools/r9/tests.fsx` | 80 cases, 0 failed |
| `tools/r10/adversarial.fsx` (`--browsers true --dotnet-tests true`) | 36 VALIDATED, 0 not run, 0 failed |
| `tools/r10/r9-replay.fsx` (`--suites true`) | 15 of 15 as expected |
| `tools/r10/launch-preflight-tests.fsx` | 47 of 47 |
| `owner-freeze.fsx decide rehearsal-010 --dry-run` on a stand-in origin | 19 decisions shown with their pins; ODR-R10-01 held until the tag, then until the scope is current |
| Merge attribution on live GitHub (PR #103) | `[103]`, then `[true,"kemiller2002"]` |
| CI | every job green on `b288b75`, `dfa42a2` and `53c2136`. `7e6dca7` is reported on #104 |

**Where these ran.**
- The suites from build through the replay ran at `b288b75`. Every later change is in the launch preflight, the owner tool, the workflows and the documents, and none of those suites exercise them.
- The launch-preflight suite ran at `7e6dca7`.
