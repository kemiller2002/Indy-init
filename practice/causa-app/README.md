# Causa practice app (practice only: see FENCE.md)

**practice only, never merged, never copied into the competition build.**

A working F# WebAssembly Causa application for the directive's acceptance target, built from the Indy-init
requirements and contracts, and the historical-compatibility decoder (referenced as a project, not copied):
**Connect → Browse → Open → Create → Save → Reload**.

| Layer | Where |
|---|---|
| Raw / Decoded / Replayed / Authoritative state, as separate types | `src/Causa.Practice.Engine/Layers.fs` |
| New investigation file set (causa-storage v1.0.0) | `Create.fs` |
| GitHub contents API as pure request descriptions | `GitHub.fs` |
| Pure update over an immutable model | `Engine.fs` |
| Pure projection to the Limen view (never the token) | `View.fs` |
| Limen protocol codec; the single state cell | `Protocol.fs` |
| WASM export shim (C#: `[JSExport]` needs the C# source generator; no logic) | `src/Causa.Practice.Host/Host.cs` |
| Markup (Limen bindings, Forma classes), bootstrap | `web/` |

## What was verified, and what was not

| Claim | Status | Evidence |
|---|---|---|
| Real causa-data (e8259df) loads: 4 investigations, every layer, snapshot agrees with replay | PASS (F# tests, local clone) | `evidence/engine-tests.log` |
| Tampered snapshot → not authoritative; tampered events → refused at decoding | PASS | same |
| Create → files decode, replay, verify | PASS | same |
| Whole engine flow incl. read-back and reload, in-memory provider | PASS (component behavior) | same |
| Browser journey Connect → Browse → Open → Create → Save → Reload, desktop Chromium, Pixel 7, iPhone 13 viewport (emulated in Chromium; WebKit not installed) | PASS **against the harness REST double seeded with the real causa-data files: component behavior (double), NOT real persistence acceptance** | `evidence/playwright-results.json`, `evidence/*-timings.json`, `evidence/pixel-7-reload.png` |
| Refused write shown as failed save | PASS (double) | same |
| Real authorized GitHub write + independent read-back | **NOT EXECUTED** | blocker below |
| Reading api.github.com from a browser in this container | **NOT EXECUTED** | the session proxy answers 403 for repositories not attached to the session; attaching one would expand access |
| Real WebKit / Safari | **NOT EXECUTED** | WebKit is not installed here |
| Admissibility (engine `History.validate`) | **NOT EVALUATED** | no requirements-derived oracle (CONTRACT §11.3); the UI says so |

## Blockers (owner decisions)

1. **Real write.** No credential is available to this practice work, and none was obtained, created or moved. A real
   write and read-back needs the owner to authorize use of the `causa-acceptance-r10` App credential (its single test
   repository) from a practice workflow, or to run the app themselves with a token: open `dist/`, connect to
   `https://api.github.com`, the test repository, a branch, and a fine-grained token with Contents read/write on that
   repository only.
2. **Real read in this container.** Needs causa-data attached to the session (an access expansion: owner only). In an
   ordinary browser the app reads the public causa-data anonymously.

## Reproduce

```sh
git clone https://github.com/kemiller2002/causa-data /tmp/causa-data      # read-only, anonymous
cd practice/causa-app
npm install                                                              # Limen 0.9.1, Forma 0.13.0, @playwright/test 1.63.0
dotnet run --project tests/Causa.Practice.Tests -- --data /tmp/causa-data  # 16 engine cases
dotnet fsi build.fsx                                                     # dist/: WASM engine + page + Limen + Forma
CAUSA_DATA=/tmp/causa-data npx playwright test                           # 6 browser journeys against the REST double
node ../../verification/harness/serve.mjs dist 4631                      # then open http://127.0.0.1:4631/
```

Timeline and waiting by cause: `evidence/timeline.md`.
