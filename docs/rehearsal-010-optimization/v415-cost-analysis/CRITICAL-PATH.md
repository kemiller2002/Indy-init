# v4.1.5 critical path

Item numbers refer to `WORK-BREAKDOWN.md`. The elapsed ranges are estimates, except where `COMPILATION-BOTTLENECKS.md` §1 marks a figure as measured. **O1–O3** are owner acts.

## Complete combined package

```text
 [1 event union]──────────────────────────────┐
 [2 workspace]──▶[3 listing]──────────────────┤
           └────▶[4 creation]──▶[6 SM rules]──┤
 [7 ownership/FO-2]──▶[5 UI contracts]────────┤      (5 and 7 can also need a compiler extension: +2-4 h)
                                              ▼
            O1: owner records v4.1.5 interface-change and state-machine-amendment requests
                                              ▼
                                  [8 compile]──▶[9 X01-X21, offline, CI]──▶[10 review rounds 1..n]
                                                                                       ▼
                                                            O2: owner approves v4.1.5 ──▶ O3: tag ──▶ [11 FREEZE step 5]
```

**Critical path: 2 → 4 → 6 → O1 → 8 → 9 → 10 → O2 → O3 → 11.** It runs about **14–30 h** of agent and CI time, plus owner waits.

Item 5 joins the path only when the compiler is extended, which adds about 2–4 h. Item 10 is the single largest elapsed block: 4–10 h, 3–5 rounds, measured per successor.

## Minimum successor (E1 + W1 + L1 + C1)

**The same path without 5 and 7: 2 → 4 → 6 → O1 → 8 → 9 → 10 → O2 → O3 → 11**, about **12–26 h** plus owner waits.

Dropping 5 and 7 does not shorten the path much, because they were never on it. What the minimum gains is **less review volume** (fewer contract files and no ownership changes) and **no compiler extension**. Both cut the probability of extra review rounds, which is the dominant risk.

## What shortens the path

| Lever | Effect | Basis |
|---|---|---|
| Design items 2 and 4 together, as one design note reviewed before the compile | removes the likely "shape reopened" review rounds (v4.1.1 rounds 1–3 FAILed) | measured round pattern |
| Record O1 as early as the design note's review passes, not after the compile | removes one owner wait from the path | v4.1.4 measured 10 h 44 min before decisions |
| Run an independent verifier on 1 and on 2/3/4 semantics while the author compiles (Scenario C) | reviewer findings arrive in round 1 instead of rounds 2–3 | `PARALLELIZATION-OPTIONS.md` |
| Keep 5 and 7 out of the first freeze (OD-V415-01) | no compiler extension, a smaller review | `COMPILATION-BOTTLENECKS.md` §3 |
