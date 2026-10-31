# Rehearsal 7 security review

Scope: the security-relevant changes and open risks of the Rehearsal 6 remediation. Statuses use PASS, FAIL, BLOCKED and NOT_RUN. Nothing here is PASS because it is planned.

## 1. Credential separation (R7-REM-003): PASS

**Rehearsal 6 finding (D-09).** `CAUSA_GITHUB_TOKEN`, the "read-only" credential, could write. That failed PF-015-O2, EXT-DATA-004-O2 ("GitHub accepted a write with the read-only credential") and EXT-DATA-005-O1 ("Expected Not WriteGranted, Actual WriteGranted").

**Required configuration (owner action).** Credentials are owner-held. An agent must not create, widen or read them.

| Secret | Grant | Scope |
|---|---|---|
| `CAUSA_GITHUB_TOKEN` | Fine-grained personal access token or GitHub App installation token. Contents: **read-only**. Metadata: read-only. No other permission. | The Causa data repository only (`kemiller2002/causa-data`) |
| `CAUSA_WRITE_TOKEN` | Fine-grained token or App installation token. Contents: **read and write**. Metadata: read-only. | `kemiller2002/causa-write-test` only |

Store both secrets on the rehearsal repository (`kemiller2002/indy-init-rehearsal-007`). They must be available to the dispatch-only probe workflow below and to the jobs that need them, and to nothing else.

**Probe (what proves it).** These are real requests whose answers are recorded. Token names, labels and settings pages are not evidence. The probe runs as a dispatch-only workflow in the rehearsal repository, because the agent cannot read the secrets:

```yaml
name: Credential probe
on: workflow_dispatch
permissions:
  contents: read
concurrency:
  group: credential-probe
  cancel-in-progress: false
jobs:
  probe:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - name: Probe the read and write credentials against real GitHub
        env:
          READ: ${{ secrets.CAUSA_GITHUB_TOKEN }}
          WRITE: ${{ secrets.CAUSA_WRITE_TOKEN }}
        run: |
          set -u
          api() { curl -sS -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $1" -H 'Accept: application/vnd.github+json' "${@:2}"; }
          tip() { curl -sS -H "Authorization: Bearer $1" "https://api.github.com/repos/$2/git/ref/heads/main" | jq -r .object.sha; }
          ref="refs/heads/credential-probe-${GITHUB_RUN_ID}"
          data=kemiller2002/causa-data; test=kemiller2002/causa-write-test
          read_get=$(api "$READ" "https://api.github.com/repos/$data/contents/README.md")
          read_write=$(api "$READ" -X POST "https://api.github.com/repos/$data/git/refs" -d "{\"ref\":\"$ref\",\"sha\":\"$(tip "$READ" $data)\"}")
          write_test=$(api "$WRITE" -X POST "https://api.github.com/repos/$test/git/refs" -d "{\"ref\":\"$ref\",\"sha\":\"$(tip "$WRITE" $test)\"}")
          write_cleanup=$(api "$WRITE" -X DELETE "https://api.github.com/repos/$test/git/$ref")
          write_elsewhere=$(api "$WRITE" -X POST "https://api.github.com/repos/$data/git/refs" -d "{\"ref\":\"$ref\",\"sha\":\"$(tip "$READ" $data)\"}")
          # A read-token write that GitHub accepted is removed at once and is still a FAIL.
          [ "$read_write" = 201 ] && api "$READ" -X DELETE "https://api.github.com/repos/$data/git/$ref" > /dev/null || true
          printf '{"schema":"indy-init.credential-probe/1","run":"%s","readGet":%s,"readWrite":%s,"writeTest":%s,"writeCleanup":%s,"writeElsewhere":%s}\n' \
            "$GITHUB_RUN_ID" "$read_get" "$read_write" "$write_test" "$write_cleanup" "$write_elsewhere" | tee probe.json
          [ "$read_get" = 200 ] && [ "$read_write" != 201 ] && [ "$write_test" = 201 ] && [ "$write_cleanup" = 204 ] && [ "$write_elsewhere" != 201 ]
      - uses: actions/upload-artifact@v4
        if: ${{ !cancelled() }}
        with:
          name: credential-probe
          path: probe.json
```

**PASS requires all of these:**
- read with the read token returns 200;
- write with the read token is refused (403 or 404);
- write with the write token in the test repository returns 201;
- the cleanup returns 204;
- write with the write token in the data repository is refused.

**Status: PASS (2026-10-31).** The owner stored both secrets on `kemiller2002/indy-init-rehearsal-008`. The probe ran there as run [41185232453](https://github.com/kemiller2002/indy-init-rehearsal-008/actions/runs/41185232453) (artifact `credential-probe`): `readGet 200, readWrite 403, writeTest 201, writeCleanup 204, writeElsewhere 403`. To keep 008's `main` at its starting state, the workflow ran from a temporary `credential-probe` branch, triggered by a push to that branch instead of `workflow_dispatch`; the probe steps are unchanged.

## 2. Unattended merging (R7-REM-005): PASS (advisory), and an open, accepted risk

**What runs today.** Praxis evaluates every merge against a signed run authorization and the frozen policy, and fails closed. `conditor unattended-readiness` reported **READY** on the rehearsal repository, with probe PR #20 merged as `9c3aaf6` (praxis `docs/autonomous-execution/evidence/2026-10-29/readiness-receipt.json`). That receipt was recorded by an earlier session; this session could not re-read the rehearsal repository.

**Open risk F1 (praxis `docs/autonomous-execution/SECURITY-REVIEW.md`, accepted by the owner on 2026-10-29).**
- The agent holds the signing key, and the executor chooses the trust store. Without UAE-R7-12, the gate binds a cooperating agent only.
- UAE-R7-12 means a trust store in an Actions secret and a ruleset on `main` requiring CI, `independent-review` and the Praxis evaluate job, with no human review required.
- **The Rehearsal 7 report must say the gate was advisory** unless the owner applies UAE-R7-12.

**Merge attribution.** Merges made through the API with the owner's credential appear under the owner's login. They are agent actions and must be reported as such (Rehearsal 6 "Merge attribution").

## 3. Changes in this initiative, from a security view

| Change | Security property | Verification |
|---|---|---|
| Arca 5xx and unknown-outcome handling (arca#47) | A server error or unreadable answer to a ref update is reconciled by idempotency key, never assumed unapplied. That prevents a duplicate or self-conflicting write (RF-F-3 class). Reads stay safe to retry. | Adapter tests: a PATCH applied and then answered 502 gives exactly one commit; a lost answer gives at most two ref updates per attempt. Independent re-review experiments E1 to E4: no duplicate commit, no false Confirmed, no reconciliation loop. |
| Arca credential handling | A revoked credential is attempted once and not again until the application resolves it. Previously each edit retried it, and a permanent refusal was retried forever. | Adapter test: a 401 with edits every 2 s gives 1 attempt. |
| `conditor ci-policy check` (conditor#122) | It fails closed on what it cannot read. It refuses candidate workflows that pushes can cancel. | 31 rule checks plus 19 regression checks from the independent review. |
| `candidate-acceptance.yml` template | A full SHA on `main` only. The checkout is proven to be that SHA. The verification record is named after the SHA. | Reviewed in the conditor#122 review. |
| `release-comparison` | It queries public release endpoints. It sends `GITHUB_TOKEN` only to `api.github.com`, never to the npm registry. | Code review; offline tests. |
| `assertion-ratchet`, `verify-defect-closures` | Read-only git access at explicit commits. | Offline tests on throwaway repositories. |

## 4. Independent adversarial review

| Item | Reviewer | Verdict | Resolution |
|---|---|---|---|
| arca#47, first review | An independent agent with a separate context, which implemented nothing | BLOCKING (4 findings) | All fixed in 2c265c2 |
| arca#47, re-review | The same independent agent | BLOCKING: finding 1 partly fixed; N1 (a ref-update burst introduced by the fix), N2, N3, N4 | Fixed in 3ac5652; third verification in progress |
| conditor#122 | An independent agent | PASS_WITH_FINDINGS (F1 to F8) | All fixed in 9916fa9 |
| forma#173 | An independent agent | PASS_WITH_FINDINGS (6 findings) | Fixes in progress |
| A dedicated security review of the whole initiative | — | NOT_RUN | See READINESS R7-PF-12 |

These reviewers are agents. Their reviews are engineering evidence, not owner attestations, and they are not GitHub approving reviews. Rehearsal 6's rule still holds: owner and review evidence counts toward process obligations only through the approved attestation channel (UAE-R7-10).
