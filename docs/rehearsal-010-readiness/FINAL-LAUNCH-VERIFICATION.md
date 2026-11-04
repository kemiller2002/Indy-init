# Rehearsal 010 final launch verification

**Verdict: READY FOR OWNER ACTION.**
- Engineering is complete and verified at `{HEAD}`.
- The launch preflight refuses today, and it refuses only for owner actions that have not happened yet: [`evidence/launch-preflight/launch-preflight-now.log`](evidence/launch-preflight/launch-preflight-now.log).
- No real-provider test has run, so **READY TO LAUNCH is not reported.**
- Rehearsal 010 has not been started. No T0 record exists, and no competition repository exists yet.

## 1. The preflight, item by item

| Requirement | Status | Evidence |
|---|---|---|
| v4.1.4 frozen, tagged, verified | **owner action**: ODR-V414-01..03 pending; tag not created | `owner-freeze.fsx status`; LP-02, LP-05 |
| Dokimos 0.11.1 published | **owner action**: dokimos#69 ready (version bump only, on #67's url-input merge, CI green); not merged, tagged or published | LP-06 |
| New echelon-current set approved and Conditor-qualified | **pending**: needs the published 0.11.1 artifacts (OWNER-APPROVAL-EXECUTION step 6a) | LP-06 |
| Toolchain lock committed | **owner action**: `toolchain.lock.json` does not exist (only the proposal) | LP-01 |
| Scope digest matches | proposal consistent: manifest `5a8aef29…` equals the proposed lock. **Not final**: the digest changes when v4.1.4 is recompiled with the approval IDs, and on ratification | LP-04; `scope preflight` in the offline suite PASS |
| Catalog-contract digest matches | PASS against the proposed lock (`808de8a9…`) | LP-03 on the committed lock |
| Design-contract digest matches | PASS against the proposed lock (`366ed579…`) | LP-03 |
| Deadline-policy digest matches | PASS against the proposed lock (`9f6ac357…`) | LP-03 |
| Journey-bindings digest matches | PASS against the proposed lock (`cd682372…`) | LP-03 |
| Process-evidence rules pinned | **owner action**: Rehearsal 010 rules drafted (`36172c57…`); `rulesSha256` null until ODR-R10-14 | LP-03 |
| Praxis inventory complete | PASS: 36 of 36 work items; `praxis-work-inventory.json` = `scope work-plan`; `slice-manifest` COMPLETE COVERAGE, 199/199 placed | offline suite; CI |
| All suite commands executable | PASS on the deterministic fixture (v4.1.4, every suite, WebKit included) | CI `suite-commands`; release-gate-repair |
| Zero-test refusal operational | PASS: suite-preflight `--require-tests` and run-suites zero-executed refusal | C01; release-gate-repair §2b |
| Chromium and WebKit operational | PASS in CI (`r10-adversarial`, `suite-commands`). Chromium only in this container | CI |
| Live-provider acceptance preflight | **NOT RUN**: needs the test repository, App and secrets (owner) | LIVE-PROVIDER-PREFLIGHT.md; LP-08 |
| Independent review complete | rounds 1–5 closed on `6e43b7a`; round 6 on the merge and tooling (§3) | FINAL-READINESS §5 |
| Adversarial checks pass | {ADV} | `evidence/adversarial/`; LP-09 |
| Specula observer ready | adapter (specula#11) parses the current observer output (6/6 tests) and is read-only; owner merge pending | §2 |
| Topology A validated | the execution topology is defined and simulated; **the full scope does not fit 420 minutes at any CI latency** (ODR-R10-18) | `evidence/schedule/` |
| Rehearsal repository clean | the competition repository does not exist yet (owner creates `indy-init-rehearsal-010`); LP-10 checks it is empty | LP-10 |
| No T0 record or clock started | confirmed | — |

## 2. Negative controls: the launch refuses what it must

[`tools/r10/launch-preflight-tests.fsx`](../../tools/r10/launch-preflight-tests.fsx) builds a synthetic Indy-init and competition repository that satisfy every check. The positive control is READY. It then applies one mutation per case. Every case must be refused, and refused by the check it targets. CI runs it in `r10-adversarial`. Results: [`evidence/launch-preflight/controls/`](evidence/launch-preflight/controls/).

| Case | Refused by |
|---|---|
| positive control | READY |
| missing owner approval | LP-05 |
| approval written by someone other than the operator | LP-05 |
| contract altered after its approval | LP-03 (and LP-05) |
| scope altered after its approval | LP-04 (and LP-05) |
| lock still a proposal | LP-01 |
| v4.1.4 not tagged | LP-02 |
| resolved set with Dokimos 0.11.0 | LP-06 |
| test repository not isolated | LP-07 |
| absent credential (no live run) | LP-08 |
| live run against the REST double | LP-08 |
| a journey failed (failed gate) | LP-08 |
| an adversarial case not validated | LP-09 |
| clock already started (a T0 record) | LP-10 |

14 of 14 as expected.

**More refusals that need no fixture:**
- `owner-freeze.fsx` refuses every write under an agent. Shown: `decide rehearsal-010` with `CLAUDECODE` set prints "only the owner records or tags (RDP-003)".
- The governed workflow fails closed without the App secrets: the token step fails.
- Its own token is `contents: read`.

## 3. Verification at `{HEAD}`

| Check | Result |
|---|---|
| Build: requirements-compiler, tests (Debug) and src (Release) | {BUILD} |
| Offline suite | {OFFLINE} |
| `tools/release-gate-repair/tests.fsx` | {RG} |
| `tools/r9/tests.fsx` | {R9} |
| `tools/r10/adversarial.fsx` (`--browsers true --dotnet-tests true`) | {ADV} |
| `tools/r10/r9-replay.fsx` (`--suites true`) | {REPLAY} |
| `tools/r10/launch-preflight-tests.fsx` | 14 of 14 |
| `owner-freeze.fsx decide rehearsal-010 --dry-run` on a stand-in origin | 19 decisions shown with their pins; ODR-R10-01 held until the tag, then until the scope is current |
| CI on `{HEAD}` | {CI} |
