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

## Application milestones
These are filled from the practice builder's report, which runs on a REST double for persistence: component behaviour, **not** real persistence acceptance.

| Milestone | Observed | Evidence |
|---|---|---|
| First successful F# build | pending | |
| First browser render | pending | |
| Connect | pending | |
| Browse | pending | |
| Create | pending | |
| Save/Reload (double) | pending | |
| Save/Reload (real) | **blocked**: owner decision 3 | |
