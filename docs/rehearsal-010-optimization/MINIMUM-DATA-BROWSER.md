# Rehearsal 010: the minimum data browser (M0)

**Question:** what is the smallest correct F# application that connects to causa-data, retrieves the real investigation JSON and displays the cases? And why is it not delivered in the first 120 minutes?

**Short answer:**
1. **It is technically small.** Measured:
   - Rehearsal 009 made its first commit of an F# WebAssembly Causa application with GitHub reads and writes at T+21. That is the commit time, not a verified release: first application commit at T+21; its CI at T+42; review fixes at T+63. Its candidate passed catalog-level Dokimos verification (36/37) at T+78, but its **release gate refused it** (0 of 541 plan obligations; R9 `evidence/FINAL-REPORT.md`:7, :93). Catalog acceptance is never release acceptance.
   - A 45-line F# probe written for this investigation read the real catalog and all four histories in 1.6 seconds.
2. **It is late because of packaging and governance, not technology.**
   - In the approved plan, "show the data" is carried only by work items that wait for the semantic kernel and its checkpoint (T13 → T17). They also wait for the save-state machine (T19 → T20) and for authoritative replay (T15 → T25).
   - Every slice merges only through a full governed gate.
3. **It cannot be pre-built.** OD-INDY-22 ("Every rehearsal has to start from just the requirements. That's the rules of the contest") forbids writing the contest application before T0. So the M0 slice is **specified and measured here, not committed**.
4. **It needs an owner decision to exist inside the run.** No approved work item carries a read-only, non-authoritative browser, so M0 needs ODR-R10-30.

## 1. The data, as it really is (verified 2026-11-05 against `kemiller2002/causa-data` `main` e8259df)

| Question | Answer | Evidence |
|---|---|---|
| How investigations are discovered | `catalog/active.json` names the active manifest and workspace. `catalog/demonstration-v1/manifest.json` lists `cases[]`, each with `caseKey`, `investigationId`, `title`, `path`, `expectedVersion`, `expectedLifecycle` and sha256s | files read in the probe; DATA-REPOSITORY-CONTRACT.md §1–3 |
| Where the JSON lives | `workspaces/demo/demonstration-v1/investigations/<uuid>/{manifest.json, events.ndjson, snapshot.json}` | same |
| Titles and metadata | In the catalog manifest (`title`, `caseKey`, `expectedVersion`, `expectedLifecycle`). The per-case `manifest.json` has `eventCount`, `createdAt`, `updatedAt` and the digests | same |
| Manifest or index | **Yes**: the catalog manifest is the index. No tree walk is needed | same |
| Event representation | **NDJSON**: one JSON object per line, with `eventType`, `aggregateVersion`, `occurredAt`, `actor` and `payload` | probe output |
| Displayable without the frozen `CoreEvent` union? | **Yes, as raw JSON.** The probe parsed all 1,363 events generically and counted 26 event types per historical case. A raw display needs no decoding into the union; an authoritative one does, and cannot be done under v4.1.4 (C1) | probe output |
| Which files need authentication | None: the repository is public. **The requirements nevertheless require authenticated reads** (PF-015-O1, DEC-R7-001: "there is no anonymous mode") | `requirements/causa-requirements-baseline-v4.json` PF-015 |
| Readable from a browser through supported APIs | **Yes.** `raw.githubusercontent.com` answers `access-control-allow-origin: *` (checked here). The authenticated REST path `api.github.com` was used from the browser by Rehearsal 009's Limen engine. An `Http` effect with an `Authorization` header, run by `BrowserKernel`, passed a live round trip in CI run 41754817534. This session could not call `api.github.com` for causa-data directly: its proxy blocks unattached repositories | `curl -D -` headers; R9 `src/Causa.App/Update.fs`:55, :115; R9 `evidence/FINAL-REPORT.md`:69, :88 |
| `kemiller2002/causa` | Not accessible to this session (`add_repo`: no access; HTTP 403). Nothing is taken from it | – |

Probe output (scratchpad, not committed):
```
catalog: 4 cases (649 ms)
  Apollo 1 (AS-204) Command Module fire, 27 January 1967    432 events (expected 432), 26 types
  Apollo 11 lunar landing, July 1969: why the descent ...     431 events (expected 431), 26 types
  Space Shuttle Challenger (STS-51-L) loss, 28 January 1986  452 events (expected 452), 26 types
  Burned-out ceiling lightbulb                               48 events (expected 48), 14 types
missing file   -> Error (Missing ".../00000000-.../events.ndjson")
malformed line -> Error (Malformed "line 2: 'n' is an invalid start of a property name ...")
network        -> Error (Network "... failed with status code '502'")
total 1647 ms
```

## 2. The smallest correct application

It follows the frozen architecture. It has no new framework, and no second data format or copy of the records.

| Part | Frozen home (owner) | What M0 needs | Size basis |
|---|---|---|---|
| Repository configuration | `Connection.fs` (ui); `Repository = {Owner; Name; Branch}` (03-IF41-WIRE.fs:45) | owner, repository and branch, plus the catalog pointer `catalog/active.json`; the workspace comes from that pointer | – |
| Credential | `Credential.T`, entered on the Connect form (EXT-DATA-004) | a user-entered token kept in memory; **never** in an asset, log or commit | R9 `Credential.header` |
| GitHub read adapter | `GitHubRequests.fs`, `Classify.fs` (persistence) | GET contents for active.json, the catalog manifest and `events.ndjson`, with 200 / 401–403 / 404 / network classified | R9: `listInvestigations`, `readInvestigation`, `classify`, `fileContent` ≈ 60 lines |
| JSON document reader | `Reader.fs` (persistence) | parse the JSON and the NDJSON lines generically; malformed line → typed error. **No** `CoreEvent` decoding | probe: ≈ 15 lines |
| List view | `Views/Dashboard.fs` (ui) | title, case key, event count and lifecycle *as recorded* | – |
| Detail / raw-event view | `Views/Investigation.fs` (ui) | per-case manifest, and the raw events in order, labelled **"Stored events, not replayed or validated"** | – |
| States | `Update.fs`, `Engine.fs` (ui) | loading, empty catalog, unauthorized, missing file, malformed JSON, network error | probe classifies all six |
| Host | `Program.fs`, `web/main.js` (integration) | the Limen `BrowserKernel` with a .NET WebAssembly transport, Forma CSS | R9 `web/main.js`: 28 lines |

**Reusable by the authoritative engine.** The read adapter and its bytes are what `Verify` later consumes (`02-IF41-STORAGE.fs` `Verify` works on file-set bytes, `PathOf`). The generic document reader is display-only: `Verify` does not take parsed JSON (review RVO-5-7). The raw-event view is superseded by the projection views.

**What M0 does not claim:**
- authoritative replay, validation against the engine, the `expectedStateSha256` check;
- any hypothesis, evidence or conclusion projection;
- editing, saving, or conflict handling.

The UI and the verification evidence label the browser **"raw stored data"**.

## 3. Why it is not delivered within 120 minutes

See DATA-VISIBILITY-DEPENDENCIES.md for every edge, and TIME-TO-FIRST-DATA.md for the numbers.

1. **No approved work item carries it.** Connect is in `WI-GEN-EXT-DATA` (after T17) and T20 (after T19). The case list is in `WI-GEN-UI`, `WI-INV-WORKSPACE` and T25 (after T15).
2. **The chain in front of those items is the kernel and the save machine:**
   - T10 → T14/T12 → T13 → T17 → generated UI;
   - T10 → T18 → T19 → T20 → T25.

   In R2 at 10-min CI, T25 integrates at T+283 and M2 at T+309.
3. **Each integration costs a full governed gate run.** About 10 minutes per minute of CI is added along the chain.
4. **The approved meaning of "list the catalog" is authoritative.** EXT-DATA-013 and T25 say the catalog is *replayed through the engine*. Under v4.1.4 that replay is impossible (C1). A raw browser therefore does not satisfy them, and must not be reported as satisfying them.

## 4. Verdict on the 60–120 minute hypothesis

| | Result |
|---|---|
| Technically achievable in 60–120 minutes? | **Yes, for the implementation.** Rehearsal 009 (measured; one agent, no slice gate): first application commit at T+21; its CI at T+42; review fixes at T+63. Its candidate passed catalog-level Dokimos verification (36/37) at T+78, but its **release gate refused it** (0 of 541 plan obligations; R9 `evidence/FINAL-REPORT.md`:7, :93). Catalog acceptance is never release acceptance. The implementation effort fits; its verification against the plan never completed there |
| Achievable in the governed Rehearsal 010 plan? | **Only with ODR-R10-30**, and then **T+116 at 0-min CI, T+125 at 5, T+135 at 10** (model, M0 slice after T10 only, 55-min estimate). That is inside 120 minutes only at 0-min CI |
| Blocker | **Governance and packaging, not technology:** no work item for a non-authoritative browser; the generated-item-after-T17 rule; the save-machine edge on Connect; one full gate per slice; and OD-INDY-22 (no pre-built code) |
