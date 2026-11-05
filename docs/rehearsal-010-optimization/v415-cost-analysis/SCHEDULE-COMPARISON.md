# Schedules

## Anchors

- **Competition window:** 9:30 AM – 5:00 PM on **November 7, 2026** (`docs/HACKATHON-BOUNDARIES.md:88`). The event is in Indianapolis, so this analysis assumes US Eastern (UTC−5): **14:30–22:00 UTC**. The time zone is an assumption.
- **Now:** this analysis was written about **2026-11-05 14:00 UTC**, which leaves about **48 h** before competition T0.
- **No Rehearsal 010 is started by this analysis**, and its T0 is not touched.

**What must be done before T0, and what during it**

| Before T0: preparation | During the window: implementation |
|---|---|
| package design, compile, review, freeze, tag; lock re-pin | all application code: decoder, kernel, UI, persistence (OD-INDY-22) |

The verified adapter in `proposals/r10-historical-data-compatibility/` is **not** application code a rehearsal may copy. Under OD-HC-05 B, its checks become frozen acceptance assets instead.

## 1. Minimum viable successor (E1 + W1 + L1 + C1)

| Step | Elapsed (est.) | Earliest finish (UTC) | Latest |
|---|---|---|---|
| Design note: workspace, listing, creation within X05/X12, event-union transcription plan | 3–6 h | Nov 5 20:00 | Nov 6 02:00 |
| Independent verification of the note (Scenario C), fixes | 1–3 h | Nov 5 22:00 | Nov 6 05:00 |
| **O1:** owner records the v4.1.5 interface-change and state-machine-amendment requests | owner minutes; the wait is unknown | — | — |
| Transcribe into `analysis/*.json` and contracts, compile, X01–X21, offline suite, CI | 3–6 h | Nov 6 04:00 | Nov 6 12:00 |
| Independent review rounds and rework | 4–10 h | Nov 6 08:00 | Nov 6 22:00 |
| **O2** approve, **O3** tag, FREEZE step 5, lock and launch-preflight re-pin | 1–2 h, plus owner waits | Nov 6 10:00 | Nov 7 00:00 |

**Critical path: about 12–26 h plus owner waits. Feasible before T0 with about 14–34 h of margin**, provided owner waits stay at hours, not a day.

## 2. Complete combined v4.1.5 (ODR-R10-32 A as recorded)

The minimum, plus:

- ownership FO-1..FO-3;
- the UI contracts (fold or compiler extension);
- the T19/T22 route;
- a larger review.

**Critical path: about 14–30 h plus owner waits, with a higher chance of extra review rounds.** At the upper end it finishes around Nov 6 22:00–Nov 7 04:00 UTC. That still lands before T0, but leaves **no time for a rehearsal on the frozen package** and little margin for a missed owner round trip.

## 3. Fallback: v4.1.5 not frozen before T0

Launch on the frozen **v4.1.4**, with these items declared before T0 and never relabelled afterwards (ODR-R10-25 option B text):

- EXT-DATA-013, T25 and the M2 journeys (J1-06, J2-08) **are expected to fail against real data**, because the frozen `CoreEvent` cannot represent 487 of the 1,315 historical events;
- creation, listing and workspace gaps remain (`SCHEMA-DIFF.md` §5).

**What still helps under the fallback:**

- the M0 read-only data browser (ODR-R10-30 A), which uses no `CoreEvent`;
- the OD-HC-05 B checks, if they were frozen as acceptance assets.

**Governance note:** ODR-R10-25 is recorded as **A**. Falling back is therefore an owner re-decision (`OWNER-DECISIONS.md` OD-V415-03), not something an agent can choose.

## Latest safe decision point for the fallback

**Nov 6, 22:00 UTC (17:00 Eastern), about 16.5 h before T0.**

At that point, if the v4.1.5 independent review has not passed, the remaining work cannot finish with margin before T0 and should not be attempted. The remaining work would be the rest of the review, O2, O3, FREEZE step 5, the lock re-pin and launch preflight, and a night of buffer.

Two earlier checkpoints give warning:

| Checkpoint | Deadline (UTC) | Action if missed |
|---|---|---|
| **C1:** design note verified | Nov 6 06:00 | switch to the minimum, if the complete package was being attempted |
| **C2:** v4.1.5 compiled and green (X01–X21, offline, CI) | Nov 6 14:00 | prepare the fallback declaration in parallel |
