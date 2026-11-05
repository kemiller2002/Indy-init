# Practice timeline (UTC; T0 = 2026-11-05T19:08:39Z, the directive)

| UTC | T+ min | Event |
|---|---|---|
| 19:22:00 | 13.4 | fork started; worktree `practice/r10-acceleration-app` created at 19:23:00 |
| 19:23:21 | 14.7 | Limen 0.9.1 and Forma (design-system 0.13.0) downloaded from npm (3 s) |
| 19:23:40 | 15.0 | causa-data cloned at e8259df (1 s) |
| 19:28:24 | 19.8 | **first successful F# build** (engine + historical decoder) |
| 19:29:26–19:29:48 | 20.8–21.2 | repair: 3 F# compile errors in the test file (indexer/argument parsing), ~1 min |
| 19:30:04 | 21.4 | engine tests 15/15 PASS (real causa-data through every layer; in-memory provider) |
| 19:31:05 | 22.4 | first .NET WebAssembly publish (52 s) |
| 19:32:13 | 23.6 | first static site build (`dotnet fsi build.fsx`, 10 s) |
| 19:32:44–19:32:51 | 24.1–24.2 | **first browser render** and first journey in Chromium (run started 19:32:44; Connect at +1.25 s, Browse +3.1 s, Open +3.2 s, Create +3.3 s, Save + read-back +3.4 s, Reload +5.9 s within the journey) |
| 19:33:18 | 24.7 | **Connect → Browse → Open → Create → Save → Reload passed against the REST double** on desktop Chromium, Pixel 7 and an iPhone 13 viewport emulated in Chromium (6/6) |
| 19:33:30 | 24.9 | defect found by inspecting the screenshot: the double lists a directory once per file, the notice said "15 investigation(s)" |
| 19:34:34 | 26.0 | repaired (`List.distinct`), engine test + Playwright assertion added; 16/16 and 6/6 PASS |

Waiting by cause (measured): toolchain/package download 4 s (npm) + 3 s (Limen/Forma pack) + 1 s (causa-data clone);
.NET WebAssembly publish 52 s; static build 10 s; F# builds ~8 s each; CI 0 (no CI run: the branch is never merged);
owner-decision waiting 0; integration conflicts 0; failed verification and repair: ~1.5 min (compile errors) + ~1 min
(listing defect).
