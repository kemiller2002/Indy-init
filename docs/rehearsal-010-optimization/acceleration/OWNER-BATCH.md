# Owner-only decisions, one batch (Parallel Acceleration Directive)

Agents cannot do these. They are reserved by the directive or need the owner's own signed record (RDP-003). Everything else proceeds under DA-R10-1.

| # | Decision | Why it is yours | Blocks | How |
|---|---|---|---|---|
| 1 | **ODR-V415-01..03** (and any other v4.1.5 owner records `FREEZE.md` step 2 names): approve the staged minimum v4.1.5 in PR #135 | frozen-package rule: the owner's own record | v4.1.5 freeze; then ODR-R10-19/-20 adoption | the command in #135's `FREEZE.md` step 2, at your terminal (the v4.1.5 session will post it) |
| 2 | **Create the `causa-catalog-read` GitHub App** (Contents read-only, `causa-data` only) and its secrets `CAUSA_CATALOG_APP_CLIENT_ID`, `CAUSA_CATALOG_APP_KEY` in the `live-provider-preflight` environment | creating credentials (reserved) | ODR-R10-28 A wiring (task ready) | the steps given earlier; tell the lead when done |
| 3 | **Real write for the practice app**: may a practice workflow on branch `practice/r10-acceleration-app` use the `causa-acceptance-r10` App credential against its single test repository? | using a credential from a new place is credential-access expansion (reserved) | directive §8 items 5–6 (real write and read-back) for the practice app; the competition build's live slice is unaffected | say yes or no; if yes, allow that branch in the environment's deployment-branch rule yourself |
| 4 | **Accept the CF-07 residual (CF07-R4-2)**: failing per-obligation INV checks of an already merged slice can delay judging an assertion until release; release still judges all | accepting a gate limitation | nothing (documented; #132 merges without it) | a one-line note, or ask for the catalog regression base after Nov 7 |
| 5 | **Start Rehearsal 010 / the official timed build** | reserved | – | only you; nothing here starts it |
