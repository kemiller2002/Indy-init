# Parallel Acceleration Directive: measured timeline and idle time by cause

T0 = 2026-11-05 19:08:39 UTC (directive received). Every time below is observed from session logs, git or the Actions API. Model figures are labelled **model**.

## Timeline (lead session)
| UTC | T+min | Event |
|---|---|---|
| 19:08:39 | 0 | directive received |
| ~19:10 | 2 | assignments sent to the UI session (B) and the v4.1.5 session (D) |
| ~19:10 | 2 | review round 3 of #132 returned FAIL (CF07-R3-1 deadlock at SL-T16) |
| ~19:12 | 3 | the UI session merged #128 (`d21fe96`) under the standing authorization |
| ~19:14 | 5 | DA-R10-1 checker: 31/31 cases pass; committed (governance timebox used: about 6 of 45 min) |
| ~19:20 | 11 | CF07-R3-1 fixed (`7ea3d4c`); suite exit 0; the mutation without the fix fails 10 checks |
| ~19:21 | 12 | practice-app builder started, fenced on `practice/r10-acceleration-app` |
| ~19:25 | 16 | CI sharding committed (C) |
| ~19:25 | 16 | review round 4 PASS; plan note added to #132 (`69c225a`) |
| ~19:26 | 18 | ownership records, classification and owner batch written |

The UI session (B) opened PR #134 (ownership map). The v4.1.5 session (D) opened PR #135 (the staged v4.1.5) and reports it needs owner records ODR-V415-01..03.

## Idle time by cause (lead, T0 to T+18)
| Cause | Minutes | Note |
|---|---|---|
| Owner-decision waiting | 0 | the standing authorization removed it; owner-only items are batched in [OWNER-BATCH.md](OWNER-BATCH.md) |
| CI waiting | 0 idle | #132 CI ran while other work proceeded; the lead never blocked on it |
| Independent-review waiting | 0 idle | rounds 3 and 4 ran in the background (about 5 and about 4 min) |
| Failed verification and repair | about 11 | CF07-R3-1 fix plus one test repair (T21 depends on T19); about 2 min for a DA script compile error |
| Integration conflict repair | 0 | nonoverlapping ownership; no conflicts |

Other sessions, before T0: the v4.1.5 session sat **blocked on an owner decision** ("merge PR #128, record ODR-V415-IF-VIEW, say go"). The directive and the #128 merge unblocked it within about 3 min of T0. Its pre-T0 blocked span is in its own transcript and is not measured here.

## Application milestones (practice app, fenced branch `practice/r10-acceleration-app` at `a5bbdd6`, never merged)
Observed by the practice builder. Persistence ran against the harness REST double: this is **component behaviour, not real persistence acceptance**.

| Milestone | UTC | T+min | Evidence |
|---|---|---|---|
| First successful F# build | 19:28:24 | 19.8 | `practice/causa-app/evidence/timeline.md` |
| First browser render | 19:32:44 | 24.1 | Playwright run |
| Connect, Browse, Open, Create, Save/Reload (double) | 19:33:18 | 24.7 | in-journey offsets +1.25 s, +3.1, +3.2, +3.3, +3.4, reload +5.9 s |
| A real defect found and fixed (listing counted a folder per file) | 19:34:34 | 26.0 | regression test added |
| Save/Reload against real GitHub | **not executed** | – | blocked: owner decision 3 (credential use) |

**Tests.**
- **Engine: 16/16 pass against real causa-data `e8259df`.**
  - Covers 4 investigations, with the raw, decoded, replayed and authoritative layers distinct.
  - Event counts are 432, 431 and 452.
  - A tampered snapshot is not authoritative, and a tampered event file is refused.
  - Negative cases: a read-back mismatch, no token, a refused write, and the token never appearing in the view.
- **Playwright: 6/6 pass** (30.2 s), on desktop Chromium, Pixel 7, and an iPhone 13 viewport emulated in Chromium.

**Not executed:**
- real WebKit;
- a real write and read-back;
- an admissibility oracle.

**Interop.** One C# file, `Host.cs`, exists only because `[JSExport]` needs the C# source generator; it holds no logic. There is also a small JS bootstrap.

**Builder waiting by cause:**
- downloads, about 8 s;
- WebAssembly publish, 52 s;
- site build, 10 s;
- CI and owner, 0;
- repair, about 2.5 min (compile errors in a test file, plus the defect above).

## Real persistence: the v4.1.5 session's preview app (reported, not verified by the lead)
- **What it is.** The preview app is `preview/causa/` on `main` (PR #126; record `cdcc52f`). It is an F# engine compiled to .NET 10 browser WebAssembly, using Limen 0.9.1 and Forma CSS. It is built on causa-storage v1.0.0 through `Causa.HistoricalCompatibility`.
- **Real-write evidence** (as that session reports it): Actions run 42067851789 in `causa-testing-repo`, using that workflow's own `GITHUB_TOKEN` (contents: write on that repository only).
  - 11/11 real-provider checks: create → byte-identical read-back → edit/save → fresh-session reload → stale-head conflict refused.
  - 31/31 engine tests.
  - Architecture checks A1–A7.
  - 25/25 browser checks against the deployed GitHub Pages site.
- **What this lead did not do.** This lead session cannot reach `causa-testing-repo` and has **not** verified that run independently. It is recorded here as the other session's evidence until someone with access checks it.
- **Fence.** It is application code on `main`; see owner batch item 6.

## Governance timebox
The directive allowed 45 min for the delegated-authority change.
- **First version:** about 6 min (19:12–19:18).
- **Independent review:** found three blockers.
  - B1: my own entry DTD-R10-002 decided the owner's OD-V415-01. It was withdrawn before merge.
  - B2: the authority record certified itself.
  - B3: protected paths were self-declared.
- **Rework:** about 19:40–20:00. **Total about 30 min, within the 45.**
- **Blocked:** no other work waited on it.
