# Owner decisions, 2026-10-26

**Status:** in force from the merge of indy-init pull request #20, which adds this file to `main`, on the owner's approval of this exact text.

**Decided by:** kemiller2002 (Kevin Miller), the repository owner, on 2026-10-26, in two steps:
1. The owner answered the recommendations in `requirements-compiled/v1/readiness/OWNER-DECISION-REQUESTS.md` with "Do what you recommend".
2. The owner reviewed pull request #20, which states each decision below in full. Asked how to approve OD-INDY-9 to OD-INDY-13, the owner chose "Approve; you merge it" in the Claude Code session https://claude.ai/code/session_01TG1BA3aoV14fyzeJCzeHDL.

**Recorded by:** an agent session (Claude Code). The agent wrote this record and merged #20 on the owner's explicit instruction. It made none of the decisions.

**Why the reply alone was not enough:**
- AR-003 forbids an agent to approve its own architectural exceptions or exclusions. It requires verifiable approval bound to the exact requirement.
- [OPERATOR-KICKOFF-DECISIONS.md](OPERATOR-KICKOFF-DECISIONS.md) reads the same phrase as authorizing recommended engineering work.
- So each decision below names the exact alternative chosen, and **the owner approved this exact text** after reviewing it. #18 was approved the same way: by an explicit instruction to merge it.

**What they answer:** the open decisions DEC-006, DEC-022, DEC-023 and DEC-025, and the confirmation that F-002 asked for, from `requirements-compiled/v1` (findings F-009, F-008, F-013, F-019 and F-012, and F-002).

**What they do not do.**
- Scope approvals that the requirements-preservation gate or the acceptance verifier consume still need the operator's `praxis.requirements-approvals/1` file. That is task T09. The verifier is given the file pinned by SHA-256, together with `--trusted-operator-id` (REHEARSAL-ACCEPTANCE-LEDGER.md).
- This record does not write or replace that file.

## Decisions

| ID | Decision | Answers |
|---|---|---|
| OD-INDY-9 | **GitHub publication: exception to causa-storage v1.0.0 §9.4.1.** The contract stays at v1.0.0: paths, file sets, schemas, fixtures, the three identities, and §9.4.2 to §9.4.5 are unchanged. For GitHub, one semantic command is published as one GraphQL `createCommitOnBranch` mutation. It carries the events append, manifest and snapshot, targets the writer's branch, and sets `expectedHeadOid` to the commit the deciding state was read at; that commit is the provider version token of §9.4.2. When GitHub refuses the mutation because the head moved, the writer treats it as a non-fast-forward: reload, re-verify, then rebase when the investigation's files did not change, or report `conflict`. It never forces. Success is reported only after a re-read at the new head returns the written bytes, and a failure after sending is `unknown`. The non-forced REST ref update is no longer conforming. The exception holds until causa-storage 1.1.0 states the same rule (STORAGE-003), planned after the event | DEC-006, F-009 |
| OD-INDY-10 | **Authentication for Connect: a fine-grained personal access token.** The user creates it for the selected data repository only: Contents read/write, Metadata read, short expiry. It is entered in the Connect form and held in memory only. It is never written to localStorage, sessionStorage, IndexedDB, URLs, logs, error text, events or files, and must be entered again after a reload. It is sent only in the `Authorization` header. The application refuses a classic token whose `X-OAuth-Scopes` grant `repo` or `admin:*`. Every connection needs a credential; there is no anonymous mode, because Rehearsal 3 exhausted GitHub's anonymous quota. Permission fields are advisory (DC-004). The CI live job uses the separate Actions secret `CAUSA_GITHUB_TOKEN`, with contents write on the test repository only | DEC-025, F-019, F-012 |
| OD-INDY-11 | **Rehearsal 4 scope: the structured AI proposal loop and time travel / semantic diff are excluded.** That means tasks T27 and T28: kickoff packet checkpoints 12:45 and 2:45, the AI/Tool agent section, and S14 and S15. The proposal-authority invariant (D-004) and prompt/data boundaries (R11.4) stay in scope. The event's scope for these features is decided from the Rehearsal 4 result (REHEARSAL-004-WRITE-PATH-PLAN §6). The operator records the exclusion in the T09 approvals file | DEC-022, F-008 |
| OD-INDY-12 | **Interfaces from earlier rehearsals.** Rehearsal 4 may extract interfaces from existing rehearsal source (T14). The event may carry only the documented interface manifest, never source, and only after the organizer confirms (PRE-ORG-01). Otherwise the event derives interfaces at kickoff from the frozen storage contract and the command/event catalog | DEC-023, F-013 |
| OD-INDY-13 | **Confirmation of OD-INDY-3 and OD-INDY-4.** The first launch shows an explicit Connect action with no auto-load, and routes use GUIDs, with case keys only as redirecting aliases. They apply to Rehearsal 4 and the event. The Rehearsal 3 transcription of 2026-10-25 stays withdrawn | F-002 (residual) |

## Consequences

- CT-007 (the write primitive) and CT-017 (authentication) become defined. They stay **unverified** until T18 and T21 exercise them against the real GitHub test repository.
- T27 and T28 are marked excluded by OD-INDY-11. They leave the run once the operator's T09 approvals cover them.
- Still open after this record. These are not owner decisions:
  - a Praxis release carrying the requirements-preservation gate (F-004);
  - echelon-current 1.28.0 containing percepta-semantic 0.2.1, Conditor-qualified and approved (F-005);
  - the write-capable test repository and the `CAUSA_GITHUB_TOKEN` secret (T05).
