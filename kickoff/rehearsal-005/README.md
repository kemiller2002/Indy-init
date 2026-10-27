# Rehearsal 5 kickoff package

**Status: prepared, not started.** Rehearsal 5 starts only when the operator authorizes it. Its clock starts at the first green CI of the bootstrap commit in the Rehearsal 5 repository (as in R4 P5). This package was prepared in the readiness sprint of 2026-10-27; the sprint's GO / NO-GO recommendation and blockers are in [`readiness.md`](readiness.md).

## What is frozen

[`freeze.json`](freeze.json) records every identity, computed by [`tools/rehearsal-freeze/freeze.fsx`](../../tools/rehearsal-freeze/freeze.fsx) from its source. None is typed by hand. Regenerate it with the command in that file's header; it exits 1 if any identity does not resolve or a baseline document changed since the pin.

| Item | Where it comes from |
|---|---|
| Requirements baseline | `requirements/causa-requirements-baseline.json`: the Rehearsal 4 baseline `BL-CAUSA-INDY-INIT-aab8f80c7743`. None of its 19 source documents changed since the pin, so Rehearsal 4 and Rehearsal 5 run the same requirements. |
| Compiled package | `requirements-compiled/v1`, `package-manifest.json` digest (unchanged since the Rehearsal 4 kickoff) |
| Release set | echelon-current 1.28.0 (approved by the owner, OD-INDY-8) and its linux-x64 channel file at the recorded Registry commit |
| Conditor | 0.10.13, the release with the Indy preset on 1.28.0, now registered (echelon-registry#99) and passing REG-RU-007 |
| Repository lock | the lock a clean `conditor init --preset indy-init` produces; identical to Rehearsal 4's (`evidence/bootstrap-dryrun.json`) |
| Acceptance tooling | the authoritative verifier `tools/compute-acceptance-verdict.py` and the rehearsal pipeline files at the recorded indy-init-rehearsal-004 commit |
| CI workflows | the workflow files at the same commit, to be reused in the new repository |
| Decisions | [`decisions.json`](decisions.json): what the owner has decided and what is still pending |

## Before the clock: operator

These are the operator's. An agent must not do them.

1. **Decide the pending items** in `decisions.json`:
   - Rehearsal 5 scope: keep or drop the T27/T28 exclusion that OD-INDY-11 made for Rehearsal 4 only;
   - the forma-workflow deviation (R4-DEC-001, remediation OD-6);
   - the Rehearsal 4 approval transcriptions (remediation OD-3).
2. **Create the empty repository** `kemiller2002/indy-init-rehearsal-005`.
3. **Configure the repository:**
   - set the secret `CAUSA_GITHUB_TOKEN`: the rotated fine-grained token, contents write on `kemiller2002/causa-write-test` only;
   - optionally set the variable `CAUSA_TEST_REPO`;
   - set Pages to deploy from Actions, with the `github-pages` environment allowing `main`. Rehearsal 4 had to change all three settings during the run.
   - Note that indy-init-rehearsal-003 still holds the revoked token: its preflight run 40528892899 answered 401.
4. **Write the approvals (T09)** after the agent's T07 derivation:
   - `approvals.json` (`praxis.requirements-approvals/1`);
   - `approvals.causa.json` (`causa.requirements-approvals/1`, which the verifier needs, Rehearsal 4 lesson 1);
   - the equivalence reviews.
   Pin them by SHA-256.
5. **Authorize the start.**

## Untimed setup: agent

1. **T07:** derive from the pinned baseline with the Rehearsal 4 F# derivation (`tools/derive-requirements`).
2. **T08:** pass the Praxis preservation gate once the operator's approvals exist. That gives PRE-REQ-04.
3. **Initialize the acceptance ledger at kickoff**, with every P0 mapped to its planned check (see the acceptance tooling in `freeze.json`).
4. **T10:** run `conditor init --preset indy-init` on the empty repository and push. The first green CI gives PRE-TOOL-03 and starts the clock:

   ```
   dotnet fsi tools/rehearsal-clock/clock.fsx start --dir evidence/rehearsal --start-utc <first green check> --basis "<run ids>"
   ```

## During the run

- **Checkpoints:** record each one with `clock.fsx checkpoint`, passing `--ledger` with the provisional ledger, so the counts come from the ledger. `clock.fsx status` fails while a due checkpoint is unrecorded; run it at every work boundary.
- **Verdict:** the final verdict is computed by the authoritative verifier from CI-published evidence, in the same `causa.acceptance-ledger/2` format used at the checkpoints.

## Evidence in this directory

- `evidence/bootstrap-dryrun.json`: a clean bootstrap of the kickoff set in a scratch repository: workstation install, `conditor init`/`verify` exit 0, scaffold build and 5/5 tests, the same lock as Rehearsal 4. It does not show the first green CI, which would start the clock.
- `evidence/preflight-local.json`: the Indy-init preflight in the agent session (no secret).
- `evidence/preflight-ci.json`: the same preflight in CI with the repository secret.
