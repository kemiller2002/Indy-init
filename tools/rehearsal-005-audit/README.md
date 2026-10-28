# Rehearsal 5 implementation audit tooling (F#)

Post-rehearsal tooling for [docs/rehearsal-005-implementation-audit](../../docs/rehearsal-005-implementation-audit/README.md). None of it modifies `indy-init-rehearsal-005`, and none of its output is historical acceptance evidence.

| Project | Purpose | Run |
|---|---|---|
| `validator/` | Audit integrity checks (section 17) against the frozen evidence | `dotnet run -c Release --project tools/rehearsal-005-audit/validator -- docs/rehearsal-005-implementation-audit <rehearsal-005 checkout at b972cb4>` |
| `post-rehearsal-tests/` | 13 focused xUnit tests (EXT-DATA-005-O1, PF-009-O3, PF-015-O1) through the candidate's own harness | `dotnet test tools/rehearsal-005-audit/post-rehearsal-tests -c Release -p:R5Dir=<isolated checkout of 1b9fb56>` |
| `post-rehearsal-ui/` | Playwright (.NET) probe for rendered contrast and scroll/focus (UI-016-O11, UI-011-O4) | serve the candidate's `obj/site-test` with its `scripts/serve.mjs`, then `dotnet run -c Release --project tools/rehearsal-005-audit/post-rehearsal-ui -- http://127.0.0.1:<port>/ <chromium>` |
| `deployed-proxy/` | Localhost forwarder to the deployed Pages site, so the candidate's mocked-spec network guard admits it | `dotnet run -c Release --project tools/rehearsal-005-audit/deployed-proxy -- https://kevinmmiller.us/indy-init-rehearsal-005/ 4931`, then the candidate's `npx playwright test` with `CAUSA_BASE_URL=http://127.0.0.1:4931/` |
