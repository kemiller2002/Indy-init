# Final recommendation: v4.1.5 cost and strategy

**Status:** analysis only. Nothing was compiled, regenerated or frozen. v4.1.4 is unchanged.

## Why "several days"?

The earlier estimate was too coarse. Measured evidence (`COMPILATION-BOTTLENECKS.md` §1) narrows it:

- **Mechanical work is negligible.**
  - Compiling and validating a whole package (X01–X21) takes about 1 s.
  - The contract typecheck takes 9 s.
  - The offline suite takes 147 s.
  - CI takes about 22 min per push.
- **Even small successors took 2–19 hours elapsed.** v4.1.4 changed six suite runners. That time went to 3–5 independent review rounds and to owner waits (v4.1.4: 10 h 44 min before decisions were recorded).
- **v4.1.5 is far larger than any previous successor:**
  - It needs **new design**: workspace addressing, listing, and creation. None of the three exists anywhere.
  - That design has to fit **frozen compiler rules** that refuse part of the combined scope: new contract files (`V41Package.fs:1790`), new state-machine states or events (X05/X12) and new DAG tasks (`:778`).
  - It also needs **its own owner interface-change requests** before the compile (`:1774-1787`), which is a round trip the earlier summary did not count.

**Revised estimate**

| Package | Agent time | Critical path |
|---|---|---|
| Complete combined v4.1.5 | about 10–26 h | about 14–30 h, plus owner waits |
| Minimum successor | about 8.5–20 h | about 12–26 h, plus owner waits |

That is roughly 1–2 days elapsed with prompt owner turn-around, not "several days".

**Where the agent time goes**

| Kind of work | Share |
|---|---|
| Genuine design (workspace, listing, creation, and the IF41-APP / UI-contract and ownership shaping) | about **55–60%** |
| Independent review and rework | about **20%** |
| Mechanical compilation, verification and recording | about **20%** |

Owner round trips add elapsed time, not agent time.

## Fastest defensible way to a frozen, working contract set before November 7

**Recommendation: stage the successor (OD-V415-01 A) and use Scenario C (OD-V415-02 A).**

1. **Now:** I write one design note covering four parts:
   - the event-union transcription plan (already verified);
   - workspace, derived and not a form field (OD-V415-05 A);
   - listing, as a request/interpret pair beside `Connector`, with no new state;
   - creation within existing SM-SAVE states (OD-V415-04 A), via `SaveMachine.InitialNew` starting in `Unsaved`.

   An independent verifier checks the note against causa-data, EXT-DATA-003/-008/-013, PERSIST-005/-006/-007 and X05/X12. *Estimate: 4–9 h.*
2. **O1:** you record the v4.1.5 interface-change and state-machine-amendment requests with `owner-freeze.fsx`.
3. **One compilation owner (me)** transcribes, compiles and gets X01–X21, the offline suite and CI green. The historical-compatibility checks C11/C15 re-run against the transcribed contract. *Estimate: 3–6 h.*
4. **Independent review rounds.** *Estimate: 4–10 h.* Then **O2** (approve) and **O3** (tag), and FREEZE step 5 with the lock re-pin.
5. **v4.1.6**, after the competition or in parallel when safe: ownership FO-1..FO-3 (ODR-R10-20), the T19/T22 route (ODR-R10-21), and the UI contracts (CF-09) with the compiler extension they need.

**Expected finish:** around Nov 6 10:00–Nov 7 00:00 UTC.

**Fallback:** if the review has not passed by **Nov 6 22:00 UTC (17:00 Eastern)**, launch on v4.1.4 with ODR-R10-25 option B's declared expected failures (OD-V415-03).

## Owner decisions needed

| ID | Decision | Recommended option |
|---|---|---|
| OD-V415-01 | stage the successor | A |
| OD-V415-02 | Scenario C | A |
| OD-V415-03 | fallback trigger | A |
| OD-V415-04 | creation in existing states only | A |
| OD-V415-05 | derived workspace | A |
| OD-V415-06 | record the v4.1.5 requests when the design note is verified | — |

Details are in `OWNER-DECISIONS.md`.

## Evidence that would change the recommendation

| Evidence | Change |
|---|---|
| The creation path **cannot** be expressed with existing SM-SAVE/SM-LOAD states and events | Creation leaves v4.1.5 and is deferred. v4.1.5 becomes read-only (event union, workspace, listing), and EXT-DATA-008 is declared an expected failure. Relaxing X05/X12 stays off the table. |
| The first review round of the design note **passes clean** and owner turn-around is under an hour | The complete combined package (ODR-R10-32 A as recorded) becomes feasible before T0. Keep it combined. |
| The workspace **must** be a Connect-form field (journey J1 requires it) | Owner changes to `verification/journeys` bindings are needed, adding a round trip. Expect the fallback to become likelier. |
| A measured review round on v4.1.5 takes **over 2 h**, or the review needs more than 5 rounds | Trigger the fallback at checkpoint C2 (Nov 6 14:00 UTC) instead of waiting for 22:00. |
| The contract-first UI interfaces are judged essential for parallel UI work in the competition window | Include them by **folding** into `06-IF41-APP.fs`/`07-IF41-VIEW.fs` (no compiler change). That adds about 2–4 h and review volume. |
