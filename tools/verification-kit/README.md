# Verification kit: visible test gaps from the first merge

Rehearsal 8 lost about 380 obligation-causes to tests the plan named exactly but nobody wrote, and nothing showed the gap while there was time to write them (R9-READINESS-HANDOFF §F item 4). This kit makes the gap visible on every merge without changing the frozen package v4.1.2 and without turning T0's first CI red. The owner chose this approach on 2026-11-03: a coverage report plus per-task skeletons, with the bootstrap unchanged.

It uses only what the approved kickoff set (echelon-current 1.35.0) installs. That is Dokimos 0.10.0, with `dokimos verification compile|scaffold|coverage`. It adds no file outside the ownership manifest: `evidence/` is the coordinator's and `.github/workflows/ci.yml` is integration's.

## Who does what

| When | Who | What |
|---|---|---|
| T0, after the bootstrap's first green CI | coordinator | Compile the frozen plan into `evidence/verification/obligations.json` and commit it (below). |
| T0, same window | integration | Paste [`ci-coverage-job.yml`](ci-coverage-job.yml) unchanged under `jobs:` in `.github/workflows/ci.yml`. From then on every push and pull request publishes the coverage table in its job summary. It never fails the build. |
| Starting a task | the task's owner | Run [`scaffold-task.fsx`](scaffold-task.fsx) on the task's own test files. Their bound tests become failing skeletons, so the PR is red until each real test replaces its skeleton. |
| Any time | anyone | Run the coverage report locally, with the same command the CI job runs. |

## Commands

From the competition repository, with an Indy-init clone at `$INDY`, checked out at tag `indy-init-requirements-v4.1.2`:

```
# coordinator, once at T0
mkdir -p evidence/verification
dokimos verification compile \
  --indy-execution-plan "$INDY/requirements-compiled/v4.1.2/verification-execution-plan.json" \
  --indy-verification-plan "$INDY/requirements-compiled/v4/verification-plan.json" \
  --out evidence/verification/obligations.json
# expect exit 0, 613 obligations, planId VP-1e3c66bcc958

# a task owner, when starting a task (names only that task's test files)
dotnet fsi "$INDY/tools/verification-kit/scaffold-task.fsx" -- \
  --files tests/Causa.Acceptance/Deterministic/AR014Tests.fs

# anyone: the coverage table, as the CI job computes it
dokimos verification coverage --obligations evidence/verification/obligations.json --root . --out coverage.json
```

`scaffold-task.fsx` runs the scaffold on a throwaway copy of the tree and copies back only the files named in `--files`. It never touches any other file, and it never touches `.git`.

A named file that is still as the bootstrap wrote it (comments plus a namespace line) is written afresh, with its namespace, `open Xunit` and module. Any other named file gets the missing tests appended. Dokimos 0.10.0's own `--append` on a namespace-only file would add tests without their module. That doesn't compile, and the bound identity's class would be missing.

Statuses, as the table shows them:
- **Not generated:** the bound test doesn't exist yet.
- **Invalid:** the test exists but can't count. Examples are a `DOKIMOS-UNIMPLEMENTED` skeleton, a skip, or a test with no assertion.
- **Generated:** a real test exists. The scan doesn't run it; `verified` needs results at the candidate SHA.
- **Blocked:** record evidence (`praxis:…`), which its own producer judges.

## Checked (2026-11-03, against Rehearsal 8's final application tree, read-only, and the released Dokimos 0.10.0 binary, sha256 `b08c2235…`)

- **compile:** v4.1.2's plan gives 613 obligations, with 0 refusals.
- **coverage, source scan:** 0.2 s. The scan shows 413 bound identities not generated: SU-BROWSER-CHROMIUM 193, SU-DOTNET-LIVE 81, SU-BROWSER-WEBKIT 49, SU-DOTNET-DETERMINISTIC 45, SU-OWN-DOTNET 28 and SU-BROWSER-LIVE 17. These are T34's gaps, plus the four tests of the new SM-SAVE transitions T-S62..T-S65.
- **The CI job's two `run` steps,** extracted from the YAML and run as written:
  - the digest check passes;
  - the summary renders, with the suite table and the largest gaps.
- **scaffold-task on `AR014Tests.fs`:**
  - only that file changed;
  - its two identities moved from `not-generated` to `invalid`;
  - `tests/Causa.Acceptance` builds with 0 warnings and 0 errors (warnings as errors).
