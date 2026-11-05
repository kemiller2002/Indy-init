# Rehearsal 010: M0 implementation plan (for inside the timed run)

**Status:** a plan, not code.
- **Not committed in advance.** Under OD-INDY-22 the contest application is written inside its own timed window, from the requirements, the frozen packages and the released Echelon tools. Any M0 code committed to Indy-init or a competition repository before T0 would be pre-built contest code.
- **Measurements only.** What was built for this investigation is a throwaway read probe in the agent's scratchpad (MINIMUM-DATA-BROWSER.md §1). It is not committed and must not be copied.
- **Needs ODR-R10-30.** The plan can be executed only after ODR-R10-30 is approved.

## Slice SL-M0, "Data visible (raw stored data, not replayed)"

| | |
|---|---|
| Depends on | SL-T10 (bootstrap). It optionally waits for SL-T11 and SL-T16 (variants in TIME-TO-FIRST-DATA.md) |
| Owner lanes | **ui:** `Connection.fs`, `Update.fs`, `Engine.fs`, `Views/Dashboard.fs`, `Views/Investigation.fs`, `Views/Shell.fs`. **persistence**, by contribution PR: `GitHubRequests.fs`, `Classify.fs`, `Reader.fs`. **integration:** `Program.fs`, `web/main.js`, `Directory.Packages.props` (Limen and Forma pins) |
| Frozen contracts used, unchanged | `Repository`, `Credential.T` and `Execute` (03-IF41-WIRE); `ConnectResult` (04-IF41-ERRORS); SM-CONNECTION (06-IF41-APP). No change to IF41-DOMAIN, IF41-STORAGE or IF41-SAVE |
| Contracts **not** used | `CoreEvent`, `Replay`, `Verify`, `SaveMachine` |
| Estimate | 45–55 lane-minutes (assumption). It has two bases. Rehearsal 009 committed its whole first application, about 2,200 lines, 19 minutes after its bootstrap (measured). The equivalent read path there is about 60 lines of F# and 28 of JavaScript |

## Steps

1. **Configuration.** Connect form fields: owner, repository, branch and token, using the label patterns of ODR-R10-07. The catalog pointer is read from `catalog/active.json`, never hard-coded, and the workspace comes from it.
2. **Read adapter.** These requests go through `Execute`, with `Authorization` from `Credential.header`:
   - `GET /repos/{o}/{r}/contents/catalog/active.json?ref={b}`;
   - then the manifest it names;
   - then `{path}/manifest.json` and `{path}/events.ndjson` on demand.

   Answers are classified as `Ok`, `Unauthorized` (401/403), `Missing` (404) or `Network`. Contents over 1 MB are not served by the contents API. The current files are at most 368 KB, so a larger file is reported as `Malformed` rather than fetched another way.
3. **Document reader.** Generic JSON and NDJSON parsing (`System.Text.Json`), line-numbered errors, and no typed event decoding.
4. **List view.** Title, case key, recorded lifecycle and expected version, from the catalog manifest. It has an empty state.
5. **Detail view.**
   - The per-case manifest (event count, dates, digests).
   - The events in `aggregateVersion` order: event type, time, actor and pretty-printed payload.
   - A permanent banner: **"Stored events, shown as recorded. Not replayed or validated by the Causa engine."**
6. **States.** Loading, empty catalog, unauthorized, missing file, malformed JSON and network error, each with a recovery action (retry, or back to Connect).
7. **Secrets.**
   - The token exists only in memory for the session.
   - The CI journey reads it from an Actions secret into the browser context at run time.
   - It never appears in a built asset, a log or an artifact, and the existing secret scans apply.

## What M1 (authoritative) later replaces, and what it keeps

**Kept:**
- the read adapter;
- the read adapter's bytes, which are the input to `Verify` (the generic document reader stays display-only);
- the Connect form and the states.

**Replaced:** the raw-event view, by the projection views, once T13, T15 and T25 exist **and** ODR-R10-25 makes the union able to represent the catalog.
