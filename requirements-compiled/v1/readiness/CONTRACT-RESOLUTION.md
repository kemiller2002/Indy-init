# Contract resolution report

**Status:** CT-007 and CT-017 are decided by OD-INDY-9 and OD-INDY-10 (`docs/OWNER-DECISIONS-2026-10-26.md`, in force from the owner's merge). The other resolutions are proposals. No contract is marked verified; the status vocabulary is in `contract-bindings.json`. Nothing was changed in causa-data, Praxis or the Registry.

| Contract | Status | Resolution |
|---|---|---|
| CT-006 causa storage v1.0.0 | identity-verified | The three identities are re-hashed by the compiler and by preflight PRE-DEP-03. They match. |
| CT-008 historical catalog | identity-verified | The manifest hash matches `verification/release.json` (PRE-DEP-03) |
| CT-007 GitHub write primitive | unverified | §1: owner exception, recorded as OD-INDY-9. T18 and T21 verify it against the test repository. |
| CT-017 authentication | unverified | §2: decided as OD-INDY-10. PRE-AUTH-02 and T21 verify it. |
| CT-013 Praxis preservation gate | **blocked** | §3: upstream release needed |
| CT-012 Percepta semantic, CT-016 release set | **blocked** | §4: echelon-current 1.28.0 needed |
| CT-001..CT-005, CT-018 (boundaries A–E, faults) | missing (`producedBy: T14`) | §5: produced during the run, not kickoff blockers |
| CT-009 Limen, CT-010 Forma, CT-011 Folio, CT-014 Dokimos, CT-015 ledger | unverified | §6: exercised by T11, T16, T26, T34 |

## 1. CT-007 / F-009: owner exception for the GitHub publication mechanism (OD-INDY-9)

The owner records this text as OD-INDY-9 (DEC-006, alternative (a)). It is in force from the owner's merge of `docs/OWNER-DECISIONS-2026-10-26.md`.

> **Exception to causa-storage v1.0.0 §9.4.1 (GitHub publication mechanism).**
>
> The causa storage contract stays at v1.0.0. Its paths, file sets, schemas, fixtures and the identities `0a68af8c…`, `f650a2e5…` and `6a7e7802…` are unchanged, and so are §9.4.2 to §9.4.5.
>
> For GitHub, one semantic command is published as **one GraphQL `createCommitOnBranch` mutation**. That mutation:
> - carries the events append, manifest and snapshot;
> - targets the writer's branch;
> - sets `expectedHeadOid` to the commit the deciding state was read at.
>
> The provider version token of §9.4.2 is that same commit.
>
> - **Refusal.** GitHub refuses the mutation when the head has moved. The writer treats the refusal as a non-fast-forward under §9.4.2: reload, re-verify, then rebase when the investigation's files did not change, or report `conflict`. It never forces.
> - **Success.** §9.4.3 still applies: success is reported only after a re-read at the new head returns the written bytes. A failure after the mutation was sent is `unknown`, never "not written".
> - **The REST update.** The non-forced REST ref update of §9.4.1 is no longer a conforming mechanism, because Rehearsal 2 review 6 showed it cannot compare-and-swap.
>
> The exception holds until causa-storage 1.1.0 states the same rule (STORAGE-003).

**What this preserves:**
- **Optimistic concurrency:** the guarded head is the concurrency token.
- **Conflict detection:** a moved head is refused and takes the existing conflict path.
- **Truthful outcomes:** the re-read rule and the `unknown` state.
- **Atomicity:** one commit carries all three files.

**What T18 must record from the real API:**
- a successful `createCommitOnBranch`;
- its refusal when `expectedHeadOid` is stale;
- the shape of that refusal.

The fakes are built from those recordings (DCT-001-A, DCT-003-A).

**Follow-up after the event:** causa-data releases contract 1.1.0 through its own `release-tag` workflow, with a new compatibility lock and the same schema and dataset identities.

## 2. CT-017: authentication contract (OD-INDY-10)

This is the minimum mechanism that meets EXT-DATA-003/004, R11.1–R11.3 and iPad Safari without adding a service.

| Item | Contract |
|---|---|
| Connect | The user chooses the repository, branch and workspace, and enters a credential. Connect performs no mutation (EXT-DATA-006). |
| Credential | A fine-grained personal access token, created by the user for the chosen data repository only: Contents read/write and Metadata read, with a short expiry. |
| Storage | Memory only. It is never written to localStorage, sessionStorage, IndexedDB, URLs, logs, error text, events or files, and is re-entered after reload (EXT-DATA-004, R9.9). |
| Transport | Sent only in the `Authorization` header to `api.github.com`. That host answers browser origins with `Access-Control-Allow-Origin: *`, observed this session. |
| Breadth check | The app reads `X-OAuth-Scopes`, which is CORS-exposed, and refuses a classic token carrying `repo` or `admin:*`. Fine-grained and app tokens carry no classic scopes. |
| Authority | Permission fields are advisory (DC-004). Only GitHub's answer to the write decides, and a refused write is held as write-denied with a renewable credential. |
| Anonymous access | None. Every connection uses a credential, because Rehearsal 3 hit GitHub's exhausted anonymous quota (F-012). |
| Failure | Bad, expired or insufficient token: a typed, explained, recoverable state with no secret in the message (EXT-DATA-007). |
| CI | The live job uses the Actions secret `CAUSA_GITHUB_TOKEN`, with contents write on the test repository only. It never uses the browser path. |
| Verification | Preflight PRE-AUTH-01 checks the secret is present. PRE-AUTH-02 is the F# check in `tools/requirements-compiler/src/Preflight.fs`; see below. The live denial and rejected-token tests are T21. |

**How PRE-AUTH-02 works:**
- It first sends a deliberately invalid token. If that is not refused with 401, something between the process and GitHub is rewriting credentials, and the check reports NOT_RUN rather than PASS.
- In this session the proxy rewrites credentials, so the check could not run here (`evidence/auth-contract-probe.json`). It must run in the rehearsal repository's CI.
- It never writes the token anywhere.

## 3. CT-013 / F-004: Praxis preservation gate

**Evidence (PRE-REQ-03, executed):** the latest Praxis release, v3.20.0, has no `preservation` command under `src/Praxis.Cli`. The gate exists only on draft PR praxis#292. Its own intake document, REQ-PRES-001, lists known bypasses: self-written approvals, an edited baseline, role-swapped tokens.

**The smallest change that meets RDP-004 without breaking VER-005:**
1. Release praxis#292 with REQ-PRES-001 R1, which records the baseline sha256 and refuses on a mismatch.
2. Release it with R2, which refuses agent-written approvals.
3. Register that release in the Registry and include it in echelon-current 1.28.0.

R3 to R5 strengthen the gate but are not needed to stop the known bypasses that matter for an owner-approved run.

**Fallback:** a time-bounded owner exception under VER-005 for a named build, recording its provenance.

RDP-004 is not bypassed either way: implementation does not start without the gate.

## 4. CT-012 / CT-016 / F-005: release set

**Evidence (PRE-DEP-01, executed):**
- percepta-semantic 0.2.1 has a release record: `releases/percepta-semantic/0.2.1.release.json`, from registry PR #96/#97, main `2d6338e`.
- The newest approved set is still echelon-current 1.27.0, which lacks percepta-semantic.

**Remaining upstream steps:**
1. Cut echelon-current 1.28.0 with percepta-semantic 0.2.1, the Praxis release from §3, Dokimos 0.7.0 and Conditor 0.10.11, and assess folio 0.4.1.
2. Conditor-qualify the set.
3. Record the owner's approval.

**Conditor SD-1:** PRE-TOOL-02 passed. Conditor v0.10.11 carries CON-367, so the scaffold pins web packages through their Registry distribution. Bootstrap without the hand workaround is still to be shown in T10 (PRE-TOOL-03).

## 5. Contracts produced during the run

Interface boundaries A–E and the fault contract are extracted from source in T14, then frozen with producer/consumer tests (G2). Contracts that are produced during the run are not kickoff blockers (`producedBy: T14`).

The failing-first tests in `curation/defect-class-tests.json` (DCT-*) pin the behaviour those contracts must have:
- typed request identity (DC-002);
- stage-aware outcomes (DC-003);
- advisory permissions (DC-004);
- fresh mutable reads (DC-005);
- a prompt boot failure (DC-006);
- `data-on="input"`.

## 6. Unverified dependency contracts

These are pinned exactly by the kickoff lock and exercised during the run:

| Contract | Exercised by |
|---|---|
| Limen 0.9.0 / limen-fsharp 0.9.0 | T11 and T16 (startup, failure, retry) |
| Forma 0.9.0 | T16 and T22 (tokens, contrast, Percepta contracts) |
| Folio 0.4.0 | T26 |
| Dokimos 0.7.0 | T32 and T34 (completion receipts) |
| Acceptance ledger | T34 (computed verdict in CI) |

No compatibility is claimed before those tasks run.
