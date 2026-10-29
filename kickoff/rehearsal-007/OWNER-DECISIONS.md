# Rehearsal 7 owner decisions

The machine-readable record is [`owner-decisions.json`](owner-decisions.json). This page is its readable copy.

## Provenance

- **Who wrote this:** an agent (Claude Code) transcribed these decisions on 2026-10-29, on the owner's instruction ("These decisions are getting saved as part of the Indy init requirements"). The owner did not edit this file.
- **How they were asked:** in chat. Each Rehearsal 6 scope decision that the v3 curation applies was put to the owner, with its Rehearsal 6 choice and the agent's recommendation. Two answers were clarified by a follow-up question. The owner's words are quoted in the JSON.
- **When it takes effect (RDP-003):** only when the owner merges the pull request that adds this file.
- **What it does not do:** a decision changes scope. It never verifies an obligation.
- **Where it is applied:** each decision that changes a requirement is written into that requirement's text in this same pull request, and the Rehearsal 7 baseline is re-pinned.

## Already decided (context)

- ADR-R7-001: session-local investigation state, with GitHub as the only durable store. The owner approved it ("I approve"), and it was merged as pull request #41.

## Decisions

| Decision | Replaces | Question | Selected | Effect |
|---|---|---|---|---|
| DEC-R7-001 | DEC-R6-001 | Anonymous reads of public catalogs? | **Login for reads and writes** | PF-015 now says that every catalog read and every write needs an authenticated GitHub credential; there is no anonymous mode. EXT-DATA-005's permission checks still apply |
| DEC-R7-002 | DEC-R6-002 | Does the 3:1 non-text rule apply to amber indicators? | **Amber `#E9A23B` is fine for Rehearsal 7** | UI-016 gains one sentence: for Rehearsal 7, a text-paired amber indicator is not a relevant non-text indicator, but amber that is the only cue still needs 3:1. UI-016-O11 is unchanged |
| DEC-R7-003 | DEC-R6-003 | User roles? | **No user roles** | UI-031-O5 is rewritten to the single-user criterion. Credential protection, provider permissions, data isolation and secure handling of operational information still apply |
| DEC-R7-004 | DEC-R6-004 | Amend AR-019-O3 (the Rehearsal 3 verdict)? | **Leave it as written** | No change |
| DEC-R7-005 | DEC-R6-006 | Screen contracts in scope? | **As Rehearsal 6: S16 NOT-APPLICABLE, S01–S15 and S17–S25 IN SCOPE** | 24 contracts in scope. S16 (counterfactual branching) may be bound NOT-APPLICABLE only by citing DEC-R7-005. This meets AR-031's requirement for a recorded decision before kickoff |
| DEC-R7-006 | DEC-R6-007 | How many investigations? | **Four: Apollo 1, Apollo 11, Challenger and the burned-out lightbulb** | EXT-DATA-013, UI-001, UI-015, PF-014 and PF's acceptance scenario now say four. The four-case waiver ends. **Kickoff blocker:** causa-data `main` holds only historical-v1 (three cases); a verified four-case catalog must exist before kickoff |
| DEC-R7-007 | DEC-R6-010 | How are the test secrets split? | **Two tokens: a read token and a write token** | PF-015 names `CAUSA_GITHUB_TOKEN` (read-only) and `CAUSA_WRITE_TOKEN` (contents write on the test repository only), and neither falls back to the other. AR-033's preflight check includes both. The owner creates both before kickoff |

## Decisions recorded after the readiness audit (2026-10-29)

These answer the readiness audit's owner questions. The owner's words were "1 approve", "2 use 1.33", "3 accept", "4 merge" (item 4, merging the four-case catalog, is an action: causa-data #6, merge commit `37d6b0a`). The same RDP-003 rule applies: they take effect when the owner merges the pull request that adds them.

| Decision | Amends | Question | Selected | Effect |
|---|---|---|---|---|
| DEC-R7-008 | R7-AMD-03 | Approve R7-AMD-03 (`conditor ci-policy check` on the rehearsal repository at kickoff and on every candidate)? | **Approved** | Becomes a Rehearsal 7 requirement. Written into the requirement text and baseline in the v4 cut (K3) |
| DEC-R7-009 | R7-AMD-04 | Approve R7-AMD-04 (acceptance runs `verify-defect-closures`, `assertion-ratchet` with base = kickoff, and the `release-comparison` record before the candidate freeze)? | **Approved** | As DEC-R7-008 |
| DEC-R7-010 | R7-AMD-05 | Approve R7-AMD-05 (every CI job running bound tests sets `CAUSA_PLAN`; storage-like fixtures declared in a manifest the credential scan honours)? | **Approved** | As DEC-R7-008 |
| DEC-R7-011 | — | Kickoff release set: echelon-current 1.32.0 or 1.33.0? | **1.33.0** (Praxis 3.24.0, Forma 0.12.0, Arca 0.7.0, Conditor 0.10.18) | Registry approval: echelon-registry #104 (`7e7e170`, merged `7a2c27a`). The kickoff lock and release comparison are written against 1.33.0 |
| DEC-R7-012 | — | Starting state of `indy-init-rehearsal-007`? | **Accept the #19 Praxis setup: the tree of `df336cd`** | The #20 probe file and the 23 leftover branches are removed. Anything else present at kickoff is a deviation |
