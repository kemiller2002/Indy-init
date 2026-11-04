# Rehearsal 010 owner-approval execution

**This is the consolidated owner-decision transaction.**
- Every step marked **owner** is yours, at your own terminal. The recording tools refuse to write when an agent runs them (`CLAUDECODE` set) or when no terminal is attached (RDP-003).
- Agents prepared everything else, and check each step afterwards.
- **Nothing here starts Rehearsal 010 or its clock.**

**Run order.** Run the steps in order. A step is done only when its **check** passes.
- A digest shown by a tool that differs from the one this document expects **stops the transaction**. Tell the agent; do not approve.
- Run every `owner-freeze.fsx` command from the directory that holds a clean clone named `Indy-init` on an up-to-date `main`, with `git` and `gh` signed in as you.

## 0. What changed since the last readiness report (read first)

**1. #102 was merged** (`cae4102`). It carried #101's release-gate repair as of `0d816e3`, without #101's later review fixes (`7fefd7b`). #104 now merges `main`:
- It keeps #102's scope rule: authorization from the later of the two checkpoints, with `--scope-manifest` coverage.
- It keeps #101's review fixes:
  - every commit is judged by ancestry;
  - merges are judged with `--cc`;
  - the suite-report root binding;
  - strict spec, acceptance and empty-scope checks.
- It keeps all of #104's audit work.

**2. The scope is not the one the mission expected.** The approval transaction for it stops here, as the mission requires (§3, step 10).

| | expected | `main` (#102) | #104 now (successor deferred) | #104 if ODR-R10-02 = A |
|---|---|---|---|---|
| requirements | 199 | 199 | 199 | **205** |
| obligations | 863 | 863 | 863 | **890** |
| work items | 35 | 36 | 36 | **37** |
| scopeSha256 | `b407b5cc…` | `fefde8a1…` | `5a8aef29…` | computed after ratification |

**Why the numbers differ:**
- `main` added `WI-GEN-RECORDS`, which gives the 16 record-only AR requirements a work item. That makes 36 work items.
- The scope digest embeds the v4.1.4 package digest, which changes when v4.1.4 is recompiled with your approval IDs (FREEZE.md step 2). It also embeds the catalog digest, which changes on ratification.
- So **no digest can be approved before v4.1.4 is frozen and ODR-R10-02 is applied.** The tool will not ask ODR-R10-01 until then.

**3. v4.1.4 FREEZE.md.** #104 keeps its step-2 text: approval IDs go beside `suiteRunners` in `analysis/amendment.json`, which X21 reads. `main`'s text pointed at `state-machine-amendments.json`, which the compiler does not read for these decisions. The package is a candidate, not frozen, so this is allowed. It is why #104's package digest (`af0132f6…`) differs from `main`'s (`dd81a4b3…`).

**4. The full scope does not fit 420 minutes under Topology A, at any CI latency.**
- The evidence is in `evidence/schedule/`.
- Launch remains possible with the full scope and an accurate deadline report. That is ODR-R10-18.

**5. The live credential changed** (ODR-R10-09).
- An isolated private test repository, with a single-repository GitHub App token minted per job.
- The Verification gate's own token is `contents: read`.
- See LIVE-PROVIDER-PREFLIGHT.md.

## 1. Tools

| Tool | Owner commands | Agent use |
|---|---|---|
| `tools/owner-freeze.fsx` | `decide v4.1.4`, `approve v4.1.4`, `tag v4.1.4 --commit SHA`, `decide rehearsal-010`, `ratify rehearsal-010` | `status`, and any command with `--dry-run` |
| `tools/r10/launch-preflight.fsx` | — (read-only) | LP-00..10; READY only when everything is done |
| `.github/workflows/live-provider-preflight.yml` | dispatch | read its artifact |

**How `decide rehearsal-010` records decisions:**
- It shows each request in `kickoff/rehearsal-010/proposals/owner-decision-requests.json` and records exactly what you type.
- For a request that approves a file, it prints the file's sha256 at `origin/main` before you answer, and records that digest in the decision.
- It writes one approval `APR-R10-OWNER-n` to `kickoff/rehearsal-007/approvals.causa.json`, commits it as you, and opens a PR.
- `skip` leaves a request pending.

## 2. Expected digests (at #104's head; re-check after each merge)

| File | sha256 |
|---|---|
| `kickoff/rehearsal-010/catalog-verification-contract.json` | `808de8a9fe43a4b1f8bfe7fb9f789a6de8f5e23ab27d878dfb675c74ada02a9a` |
| `kickoff/rehearsal-010/design-contract.json` | `366ed579446636d31894e77ab63877d5dc5f4ef0e935ae14f089b4360780482a` |
| `verification/design-contract/forma-0.13.0-inventory.json` | `d71e964d94a4e189fb22f633238ba21fc3943712ecb60c5a5876611a2d110a2d` |
| `kickoff/rehearsal-010/deadline-policy.json` | `9f6ac357b09190074efcc3bdba851ddce0ebd8cca56d3c0fe69fe7ba8499a4e0` |
| `verification/journeys/bindings.json` | `cd68237205ff8f56503c46bdb6dc925085a3b07c9306ab6f9979f2b17b9754dd` |
| `kickoff/rehearsal-010/process-evidence-rules.json` | `36172c57721419714a414334cd65a1548da5c0bc6318f55f73b77a207546344d` |
| `docs/rehearsal-010-readiness/proposals/causa-investigation.requirement-catalog.successor.proposed.json` | `b4bd432fdcbc673718e3df752a403ca2605a72bb0497a06a5aa05086b605d6e9` |
| `requirements-compiled/v4.1.4/package-manifest.json` (before step 3 recompiles it) | `af0132f62039bf814a98cf3a3c2b60d939eea1ce3791e6ee45a202bb9210540f` |

`git show origin/main:<path> | sha256sum` reproduces each digest.

## 3. The transaction

| # | Who | Action | Check |
|---|---|---|---|
| 0 | owner | **Turn on commit signing** with a key registered on your GitHub account, for example SSH signing: `git config --global gpg.format ssh`, `git config --global user.signingkey ~/.ssh/<key>.pub`, `git config --global commit.gpgsign true`, with the key added as a *signing* key on GitHub. launch-preflight counts an owner record or a lock change only if GitHub verifies its commit as **committed and signed by you** (an agent can write any author name, and signs with its own key; GitHub's own commits, from web edits or squash merges, are not yours). So merge every owner-freeze pull request with **Create a merge commit**, never squash or rebase. `owner-freeze` refuses to record while the approvals file holds a change you did not sign. launch-preflight also verifies each signature itself against the signing keys registered on your GitHub account (SSH signing keys or GPG keys), so register the key as a **signing** key. Keep the token this agent environment uses without `write:ssh_signing_key` and `write:gpg_key`, so no agent can register a key as yours. On an owner-freeze pull request, never click "Update branch": rebase locally and sign instead (GitHub's merge commit would be unsigned, and the preflight refuses it) | `git log --show-signature -1` on a test commit; GitHub shows it "Verified" |
| 1 | owner | Review and merge **#104** (ODR-R10-16 is recorded in step 7). Then close **#101** as superseded: #104 carries its required changes, including the review fixes #102 lacked | `main` = #104's merge commit; CI on it green |
| 2 | owner | `dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1.4 --repo Indy-init` (ODR-V414-01, -02: **A**, **A**). Merge the PR it opens | `owner-freeze.fsx status`: ODR-V414-01, -02 recorded |
| 3 | agent | FREEZE.md step 2: write the approval IDs beside `suiteRunners`; recompile v4.1.4 (X01–X21 PASS); run `percepta spec-preflight`; record `analysis/independent-review.json` for **this** commit; add v4.1.4's CI steps. Then recompile the scope, the slice manifest and the Praxis inventory. Open a PR | `requirements-compiler v41 --package requirements-compiled/v4.1.4 --check` PASS; the review names the compiled package digest |
| 4 | owner | Merge step 3's PR. Then `owner-freeze.fsx -- approve v4.1.4 --repo Indy-init` (ODR-V414-03) and merge its PR. Then `owner-freeze.fsx -- tag v4.1.4 --repo Indy-init --commit <that merge commit>` | `git rev-parse indy-init-requirements-v4.1.4^{commit}` = the ODR-V414-03 merge; the package manifest at the tag equals the reviewed one |
| 5 | agent | Add v4.1.4 to the frozen-package guard (CI and AGENTS.md). Fill `requirementsPackage.tag` and `tagCommit` in the proposed lock | CI `frozen-v4` PASS |
| 6 | owner | **Dokimos (ODR-RG-03).** Merge kemiller2002/dokimos#69. Then `git tag dokimos-v0.11.1 <merge commit> && git push origin dokimos-v0.11.1`, or dispatch `release.yml` with version `0.11.1` from `main`. The Release workflow publishes, with checksums and provenance attestations | `gh release view dokimos-v0.11.1 --repo kemiller2002/dokimos` lists the tool, the native CLIs and `dokimos-checksums.txt`; `gh attestation verify` passes on the tool |
| 6a | agent | Verify the published artifacts against `dokimos-checksums.txt`. Prepare the echelon-registry PR for a new `echelon-current` set carrying Dokimos 0.11.1. Run Conditor qualification against that set | Conditor qualification PASS on the actual set |
| 6b | owner | Approve and merge the echelon-registry PR | The set's registry commit and per-platform digests are fixed |
| 7 | owner | **Test repository and App** (LIVE-PROVIDER-PREFLIGHT.md §2): create `kemiller2002/causa-acceptance-r10` and its App; add the two secrets to the Indy-init **environment** `live-provider-preflight` (deployment branch: `main` only). Create the empty competition repository `kemiller2002/indy-init-rehearsal-010` and add the two secrets there | §2's checks |
| 8 | owner | `owner-freeze.fsx -- decide rehearsal-010 --repo Indy-init`. Answer as you selected. ODR-R10-01 is **not asked yet**: the tool says why. Merge the PR | `status`: everything recorded except ODR-R10-01 |
| 9 | owner | If ODR-R10-02 = A: `owner-freeze.fsx -- ratify rehearsal-010 --repo Indy-init`. Merge the PR | The ratified catalog has 44 requirements; the 38 earlier ones are byte-for-byte unchanged |
| 10 | agent | Recompile the scope manifest from the frozen v4.1.4 and the ratified catalog. Regenerate the slice manifest, the Praxis inventory and the schedule evidence. Regenerate the proposed lock (all digests, the new set, `processEvidence` with the recorded rules digest). Open a PR; you merge it | `scope preflight` PASS; the counts match §0's last column (or the middle one if ODR-R10-02 = B) |
| 11 | owner | `owner-freeze.fsx -- decide rehearsal-010 --repo Indy-init` again. **ODR-R10-01** is now asked and shows the counts and `scopeSha256` from step 10. Approve only if they equal the agent's report. Merge the PR | `status`: ODR-R10-01 recorded |
| 12 | owner | Dispatch the live preflight (LIVE-PROVIDER-PREFLIGHT.md §3). Download its artifact | J1–J3 × Chromium and WebKit all `passed` against `api.github.com`; the token reaches exactly the test repository. Note the run id |
| 13 | owner | **ODR-RG-04.** Commit `kickoff/rehearsal-010/toolchain.lock.json`: the proposed lock from step 10, with `status` set to your pin. This is your own commit; an agent never writes it | `dotnet fsi tools/r10/launch-preflight.fsx -- --indy-init Indy-init --live-run <run id of step 12> --adversarial-run <the Preflight tooling run id of main> --competition https://github.com/kemiller2002/indy-init-rehearsal-010` prints **READY** (from a clean clone at `origin/main`) |
| 14 | owner | Merge kemiller2002/specula#11 (ODR-R10-17). Point Specula at the observer repository. Schedule the observer (STARTUP-INSTRUCTIONS §9) | Specula shows the observer's replay findings |

Only after step 13 prints READY: FINAL-READINESS §9's launch command.

## 4. Decisions recorded in step 8 or 11, as you selected

| ID | Selected | Pins |
|---|---|---|
| ODR-RG-03 | A: Dokimos 0.11.1 and a new set | — |
| ODR-RG-05 | A: Topology A (five lanes and an independent reviewer) | — |
| ODR-R10-02 | A: ratify the successor (or B: defer) | the successor file |
| ODR-R10-03 | A | the catalog-verification contract |
| ODR-R10-04 | A: `domain-profile-selected = false` | the catalog-verification contract |
| ODR-R10-05 | A | the design contract; the Forma 0.13.0 inventory |
| ODR-R10-06 | A: 420 min, one immutable T0, no reset, no early stop, no compute limit | the deadline policy |
| ODR-R10-07 | A | the journey bindings |
| ODR-R10-08 | A: WASM-LOAD-001 and -005 are P0 under UI-014-O2 (an interpretation; no normative text changes, so no successor package) | — |
| ODR-R10-09 | A: isolated repository with a single-repository App token | the lock's `testRepository` |
| ODR-R10-10..12 | A, A, A | — |
| ODR-R10-13 | A: no compute-stop limit | — |
| ODR-R10-14 | A: pin the Rehearsal 010 process-evidence rules | the rules file |
| ODR-R10-15 | A, for the reviewed commit in FINAL-READINESS §5 | — |
| ODR-R10-16 | A: merge #104; close #101 | — |
| ODR-R10-17 | A: approve Specula #11 | — |
| ODR-R10-18 | **your choice:** A launch with the full scope (partial integration expected), B reduce scope, C extend the window | — |
| ODR-R10-01 | asked at step 11 | the scope manifest's `scopeSha256` |

## 5. If something does not match

- **A digest shown by a tool differs from §2 or from the agent's report:** answer `skip`, and tell the agent. Nothing is recorded.
- **A step's check fails:** stop. The agent fixes it in a pull request, and the step is repeated. Recorded decisions stay.
- **A material change after the review** (FINAL-READINESS §5): ODR-R10-15 requires a renewed review before step 1.
