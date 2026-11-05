# Rehearsal 010: M0 verification

There are two parts:
- **§1 is what was verified in this investigation.** It is measured, against the real repository.
- **§2 is the verification SL-M0 must pass inside the run**, through the governed gate.

None of it replaces or weakens a real-provider acceptance test, and none of it counts toward EXT-DATA-013, T25, J1-06 or J2-08.

## 1. Verified now (throwaway probe, scratchpad; not committed)

| Check | Result | Evidence |
|---|---|---|
| F# compilation | PASS. `dotnet fsi` compiled and ran the probe | probe run |
| Retrieval against the real repository | PASS. `catalog/active.json` → `catalog/demonstration-v1/manifest.json` → 4 cases → 4 `events.ndjson` files: 432, 431, 452 and 48 events, each equal to the manifest's `expectedVersion` | probe output (MINIMUM-DATA-BROWSER.md §1) |
| Apollo 1, Apollo 11 and Challenger present | PASS, with titles as listed | same |
| Raw events displayable without `CoreEvent` | PASS. Generic parse of all 1,363 events; 26 event types in each historical case | same |
| Malformed JSON | PASS. Line-numbered `Malformed` error | same |
| Missing investigation | PASS. 404 → `Missing path` | same |
| Network failure | PASS. `Network` error | same |
| Empty catalog | not exercised against real data. **The real catalog is never empty, and must not be edited to make it so.** It goes in the slice's deterministic tests | – |
| Unauthorized | not exercised. The repository is public, and the session proxy blocks credentialed API calls to it. It goes in the slice's live test with a revoked token | – |
| Browser CORS | `raw.githubusercontent.com`: `access-control-allow-origin: *` (checked). `api.github.com` with a token from the browser: shown by Rehearsal 009's live round trip (CI run 41754817534), not re-run here | `curl` headers; R9 evidence |
| Browser journey, mobile viewport, rendered list and detail | **not run**: no M0 application exists, by OD-INDY-22 | – |

## 2. Required for SL-M0 inside the run

Each check runs in the governed gate on the merged result.

| Check | Kind | Real provider? |
|---|---|---|
| Build of the solution and the WebAssembly site | build | – |
| Read adapter classification: 200 / 401 / 403 / 404 / network / oversized | deterministic unit tests on recorded answers (the recordings T18 already makes) | recorded |
| Document reader: valid JSON, valid NDJSON, malformed at line *n*, empty file | deterministic unit tests | – |
| Empty-catalog and missing-case states render their message and recovery action | component / static HTML check | – |
| **Connect → list → detail** against `kemiller2002/causa-data` with the CI token: four cases listed by title; Apollo 11 opened; 431 events shown in order; the banner text present | Playwright, Chromium and WebKit | **yes** |
| Unauthorized: the same journey with an invalid token shows the unauthorized state and no data | Playwright | **yes** |
| Mobile viewport (390×844) smoke test of list and detail | Playwright | yes |
| No secret exposure: the token is absent from built assets, page source, console and artifacts | the existing secret scan, plus a grep of the build output | – |
| **No false replay claim**: the page and the evidence say "not replayed or validated"; the slice's evidence records zero requirements satisfied | assertion in the journey, and a gate check that the slice claims no requirement | – |
