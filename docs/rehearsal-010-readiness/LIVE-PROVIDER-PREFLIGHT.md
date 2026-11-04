# Rehearsal 010 live-provider preflight

**Status: NOT RUN.** It needs:
- the isolated test repository;
- its GitHub App;
- the App's secrets.

All three are owner actions (§2). No real-provider result exists yet. None is claimed from the REST double.

The live preflight is the real-provider positive control before launch. It runs the governed journeys J1–J3:
- on Chromium and WebKit;
- against the real GitHub API;
- persisting to an isolated test repository;
- with a credential that reaches only that repository and only for one job.

`tools/r10/launch-preflight.fsx` check LP-08 refuses launch until the evidence exists and passes.

## 1. The boundary (ODR-R10-09)

| What | Value |
|---|---|
| Test repository | `kemiller2002/causa-acceptance-r10`, **private**, branch `causa-acceptance`. It holds synthetic data only and no production secrets. It is resettable: authoritative evidence lives in the governed CI artifacts, never in this repository |
| Credential | A GitHub App installed on that one repository with **Contents: read and write** and nothing else. There is no organization-wide or all-repositories installation |
| Token | An installation token minted per job by `actions/create-github-app-token` (pinned to v3.3.0, `64ab729a…`). It is scoped with `repositories: causa-acceptance-r10` and `permission-contents: write`, and revoked when the job ends |
| Secrets | `CAUSA_ACCEPTANCE_APP_CLIENT_ID` and `CAUSA_ACCEPTANCE_APP_KEY`. In **Indy-init** they are held in the environment `live-provider-preflight`, restricted to `main`, so a workflow an agent pushes to a branch cannot read them. In the **competition repository** they are repository secrets for the Verification gate (residual risk below) |
| The gate's own token | `contents: read` (it was `contents: write`). It never writes |

**Who receives the token.**
- Only the steps that run:
  - the live suites;
  - the governed journeys;
  - the slice gate (its base-versus-head live runs).
- It reaches them as `CAUSA_GITHUB_TOKEN`.
- It is never written to the checkout (`persist-credentials: false`), a file, an artifact, a log, or the served application's files.
- The preflight workflow fails if the token string appears in its evidence.

**What candidate code can still reach, stated plainly.**
- The candidate application needs a credential to persist to GitHub. That is what J1–J3 verify.
- So candidate code running in those steps can read that token. This cannot be removed without removing real-provider verification.
- The design bounds it instead. The exposed token can write only synthetic data in one repository, for at most one job, and is revoked at job end.
- It cannot reach the competition repository, Indy-init, or any other repository. The preflight checks this: the installation lists exactly one repository.
- The long-lived App private key is a secret of the competition repository.
- **Residual risk:**
  - Who can obtain the key: a builder who pushes a workflow change to a branch could obtain it, because GitHub runs a branch's own workflow with the repository's secrets.
  - Why that is bounded: the governance check refuses such a run, but only after it has run. What the key grants is still only that synthetic repository.
  - The mitigation: rotate the key after the rehearsal (§4).
- Moving the key out of the builder's reach entirely needs a separate owner-controlled runner repository. That is a larger contract change, offered in OWNER-APPROVAL-EXECUTION.md as an option, not done here.

## 2. Owner set-up (once, before the preflight)

1. Create the private repository `kemiller2002/causa-acceptance-r10` with a README on `main`. Create the branch `causa-acceptance` from it.
2. Create a GitHub App `causa-acceptance-r10`.
   - Permissions: **Repository → Contents: Read and write**, and nothing else. Metadata read is implied.
   - No webhook.
   - Install it on **only** `kemiller2002/causa-acceptance-r10`.
3. Generate a private key. Record the App's client ID.
4. In **kemiller2002/Indy-init**, create the environment `live-provider-preflight`, with deployment branches set to `main` only, and add the two secrets to it. In the **competition repository** (`indy-init-rehearsal-010`), add the same two secrets as repository secrets:
   - `CAUSA_ACCEPTANCE_APP_CLIENT_ID` = the client ID;
   - `CAUSA_ACCEPTANCE_APP_KEY` = the private key.
5. Delete the downloaded key file.

## 3. Run (owner, before launch; no rehearsal clock)

The lock must name the repository:

```json
"testRepository": { "owner": "kemiller2002", "repo": "causa-acceptance-r10", "branch": "causa-acceptance", "isolated": true }
```

This is already so in `kickoff/rehearsal-010/toolchain.lock.proposed.json`. To run against it before the lock is committed:

```
gh workflow run live-provider-preflight.yml --repo kemiller2002/Indy-init --ref main \
  -f lock=kickoff/rehearsal-010/toolchain.lock.proposed.json
gh run watch --repo kemiller2002/Indy-init $(gh run list --repo kemiller2002/Indy-init --workflow live-provider-preflight.yml --limit 1 --json databaseId --jq '.[0].databaseId')
gh run download --repo kemiller2002/Indy-init --name live-provider-preflight-<sha> --dir evidence-live
```

**What the run proves:**

| Check | How |
|---|---|
| The credential works and is narrow | `GET /installation/repositories` lists exactly the test repository; its branch exists |
| Real investigation creation, and durable persistence | J1: GitHub is read directly; the investigation reopens in a fresh browser context |
| Hypothesis and evidence relationships | J2 on the real provider, re-read in a fresh context |
| Navigation and application structure | J3-01, J3-02 |
| Recovery | J3-04 offline, J3-06 save failure, J3-07 unknown commit outcome |
| Conflict | J3-05: two writers edit the same investigation on the real provider; the second sees Conflict, and nothing is lost |
| Both engines | Chromium and WebKit, each journey on each |
| It is real | `provider.baseUrl` = `https://api.github.com`, `mode` = `github`; the double is not started |

**After the run:** keep the artifact as governed evidence and note the run id. LP-08 fetches the run itself with `gh`. It checks that the run is this workflow, dispatched on `main` and successful, of a commit in the history whose journeys and workflow are unchanged since, bound to that commit, and against the lock's test repository (`run-info.json`). Then it reads the artifact:

```
dotnet fsi tools/r10/launch-preflight.fsx -- --indy-init . --live-run <run id> --adversarial-run <Preflight tooling run id of main> --competition https://github.com/kemiller2002/indy-init-rehearsal-010
```

## 4. After the rehearsal

- Rotate or delete the App's private key.
- Uninstall the App, or keep it only for the next rehearsal's preflight.
- Reset `causa-acceptance` if wanted. The evidence is in the artifacts.
