# Owner decision requests for Rehearsal 4

**Status:** REQUESTED. None of these is approved. An agent wrote this document; it decides nothing (RDP-003, AR-003, OD-INDY-7).

**How to approve.** Record an approval in an owner-authored form: an owner commit to an OWNER-DECISIONS addendum, or an Ordo decision. Each request below names the record it needs.

Scope approvals, such as exclusions or P0 changes, must also reach the acceptance verifier. Since indy-init PR #16, that means an **externally pinned approvals file**: the verifier is given the file's SHA-256, and the operator is named with `--trusted-operator-id`. Approvals written inline in the ledger, or by an agent, are invalid (REHEARSAL-ACCEPTANCE-LEDGER.md).

**After approval.**
1. Update the decision's `classification` in `curation/decisions.json`, and the finding's `status`.
2. Recompile the package.
3. Re-run the preflight (`readiness/READINESS-REPORT.md`, "Re-run").

The machine-readable form of every request is in `decision-registry.json`: the fields `alternatives`, `recommendation`, `consequences` and `approvalRequired`.

## Summary

| # | Request | Recommendation | Approval record |
|---|---|---|---|
| 1 | DEC-024 / F-001: what the launch view shows | Merge PR "proposed requirement changes": UI-001 and UI-015 restated against the connected catalog, plus the new EXT-DATA-013 | Owner merge of that PR. Per OD-INDY-6, only owner commits change the baseline. |
| 2 | F-003: baseline re-pin | Same PR. It regenerates the baseline with the existing tool, now 461 requirements and AR-019..AR-026 preserved. Re-pin again at the kickoff commit. | Owner merge of that PR |
| 3 | DEC-006 / F-009: GitHub write primitive | Owner exception, with the text in `CONTRACT-RESOLUTION.md` §1. Version the causa-data contract (1.1.0) after the event. | Owner-authored decision naming DEC-006 and F-009 |
| 4 | DEC-025 / F-019 / F-012: authentication | A fine-grained personal access token, scoped to the data repository, entered in Connect and held in memory. A credential is required for every connection. | Owner decision record (security boundary) |
| 5 | DEC-022 / F-008: AI proposal loop and time travel | Exclude both from Rehearsal 4, with approval. Keep the proposal-authority invariant (D-004, R11.4). | Owner approval records covering the exclusion (`praxis.requirements-approvals/1`, owner-written) |
| 6 | DEC-023 / F-013: interfaces from earlier rehearsals | Allowed for Rehearsal 4. At the event, carry the documented manifest only, subject to the organizer. | Owner decision; organizer confirmation for the event (PRE-ORG-01) |
| 7 | F-007: agent topology | No action needed: Plan B (one lead plus a fresh reviewer) is the default. Plan A's parallel lanes need approval only if wanted. | Only if Plan A is chosen |

Not blocking, but recommended:
- **F-010 / F-011:** give a priority to the requirements that have none, and say whether UI-037's "[P0, competition profile]" is P0 for Rehearsal 4.
- **F-002 residual:** confirm OD-INDY-3/4 in an authenticated record.

## 1. DEC-024: launch content (F-001, F-006)

**Evidence:**
- UI-001 says "On launch, show all five frozen demonstration investigations". UI-015 requires verifying that they load.
- EXT-DATA-001 forbids bundling those datasets, and EXT-DATA-002 and OD-INDY-3 require an empty, disconnected first launch with an explicit Connect action.
- causa-data's catalog policy requires exactly three historical cases and no synthetic ones.

**Alternatives:**
- **(a)** Restate UI-001 and UI-015 against the connected catalog, and add EXT-DATA-013 (the active catalog: Apollo 1, Apollo 11, Challenger STS-51-L, loaded after Connect, never bundled).
- **(b)** Keep UI-001 literally, with exceptions to EXT-DATA-001/002 and to the catalog policy.

**Recommendation: (a).** The proposed edit changes two obligations, UI-001-O1 and UI-015-O2, and adds EXT-DATA-013-O1..O3. No other obligation changes, and no obligation id shifts.

**Consequences:**
- T22 and T25 accept against the three historical cases.
- The five synthetic v1.0.0 fixtures stay the contract test corpus (G4).
- After the merge, F-001 and F-006 close, DEC-024 becomes fixed, and the curation quotes the new text. Until it does, the compiler's V11 rejects the old quotes.

## 2. F-003: baseline re-pin

The committed baseline is pinned at 002d8ba. It lacks AR-019..AR-026 and two AGENT-RELIABILITY sections, and two REHEARSAL-ACCEPTANCE-LEDGER sections have changed since. The same PR regenerates it with the unmodified `tools/build-requirements-baseline.py`; the F# port produces identical bytes.

Preflight PRE-REQ-01 passes on that branch, as recorded in `evidence/preflight-owner-branch-projection.json`. R4 P2 still requires a re-pin at the kickoff commit (task T01).

## 3. DEC-006: GitHub write primitive (F-009)

**Evidence:**
- causa-data CONTRACT.md §9.4.1 requires "a non-forced ref update" (`PATCH /git/refs`).
- Rehearsal 2's review 6 proved that the REST API cannot compare-and-swap a branch. On the owner's decision, the save became GraphQL `createCommitOnBranch` guarded by `expectedHeadOid`.

**Alternatives:**
- **(a)** Owner exception: wire format, paths and §9.4.2–9.4.5 unchanged; only the 9.4.1 mechanism is replaced.
- **(b)** Contract version 1.1.0 through causa-data's release process.
- **(c)** Keep the REST update. Rejected, because it is proven unsafe.

**Recommendation: (a) now, (b) after the event.** (a) changes no schema, fixture or identity, needs no upstream release, and keeps the decision's base commit as the concurrency token. The amendment text is in `CONTRACT-RESOLUTION.md` §1.

## 4. DEC-025: authentication (F-019, F-012)

**Evidence:**
- EXT-DATA-003 and EXT-DATA-004: scoped credential, no localStorage, re-authorization after reload, no unrestricted token by default.
- D-210: the authentication mechanism is a reversible choice.
- ADRP: OAuth/Fides are still open.
- `api.github.com` answers browser origins with `Access-Control-Allow-Origin: *` and exposes `X-OAuth-Scopes`. Observed in this session.
- Rehearsal 3 hit GitHub's exhausted anonymous quota (remediation report).

**Alternatives:**
- **(a)** A fine-grained personal access token entered by the user.
- **(b)** A GitHub App user token through the device flow. This needs a token-exchange service. That GitHub's login endpoints refuse cross-origin calls could not be checked here, because this session's proxy rewrites requests to github.com. Verify it before choosing (b).
- **(c)** The OAuth web flow, which needs a client secret and therefore a backend.

**Recommendation: (a).**
- The token is limited to the selected data repository, with Contents read/write and Metadata read, and a short expiry.
- It lives in memory only, is re-entered after reload, and is sent only in the Authorization header.
- The app refuses a classic token that carries `repo` or admin scopes.
- A credential is required for every connection; there is no anonymous mode.
- Browser credentials and the CI secret `CAUSA_GITHUB_TOKEN`, used by the live job only, are separate.

The contract is CT-017; `CONTRACT-RESOLUTION.md` §2 gives it.

## 5. DEC-022: AI proposal loop and time travel (F-008)

**Evidence:**
- The kickoff packet orders AI proposals 7th and time travel 9th, with checkpoints at 12:45 and 2:45. Its do-not-cut list names the proposal *authority boundary*, which is an invariant.
- ADRP-012's build order omits both features.
- The Rehearsal 4 plan measures write milestones only.

**Alternatives:**
- **(a)** Both in scope.
- **(b)** Both excluded from Rehearsal 4 with approval, and the event scope decided from the Rehearsal 4 result (R4 §6).

**Recommendation: (b).** T27 and T28 (marked `conditionalOn: DEC-022`) leave the timed plan only after an approved exclusion. Nothing mandatory is removed without approval.

## 6. DEC-023: reuse of interfaces from earlier rehearsals (F-013)

**Recommendation:**
- Rehearsal 4 may extract interfaces from existing rehearsal source (T14).
- The event carries only the documented interface manifest, with no source, if the organizer confirms (PRE-ORG-01).
- Otherwise the event derives interfaces at kickoff from the frozen contract and the command/event catalog.

## 7. Requests that are not decisions

These are upstream releases, handled by their owners:
- **Praxis:** release the hardened preservation gate (F-004).
- **Registry:** cut and qualify echelon-current 1.28.0 with percepta-semantic 0.2.1, and assess folio 0.4.1 (F-005, F-015).

See `DEPENDENCY-COMPATIBILITY.md`.
