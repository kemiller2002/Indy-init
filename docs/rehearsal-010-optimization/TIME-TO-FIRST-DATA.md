# Rehearsal 010: time to first data

**Model results from estimates, not observations**, except the rows marked *measured*.

All runs:
- `--merge 3 --queue-depth 3 --setup 25 --roles slice-roles.v414.json` with the frozen lanes, except where named;
- milestone-first ordering (`--milestones inputs/milestones-with-m0.json --priority milestones`) for the M0 runs.

| Plan | Run |
|---|---|
| Approved plan | `results/R0-baseline.json`: 45-min set-up, one candidate at a time, chain-first |
| Current optimized plan | `results/R2-V4-milestones.json` |
| M0 plans | `results/R6-M0-after-t10.json`, `-after-t11.json`, `-after-t16.json`: R2 plus one slice SL-M0 that depends only on the named task (`results/sensitivity-m0-*.slices.json`) |

## Minutes after T0, at 0 / 5 / 10-minute CI

| Metric (the slice that delivers it) | Approved plan | Optimized (R2) | M0 plan, after T10 |
|---|---|---|---|
| Connect screen (`WI-GEN-EXT-DATA`; M0: SL-M0) | 307 / 346 / 406 | 220 / 246 / 270 | **116 / 125 / 135** |
| Successful GitHub read (T20; M0: SL-M0) | 225 / 255 / 282 | 208 / 225 / 244 | **116 / 125 / 135** |
| Visible investigation list (milestone M2; M0: SL-M0) | 720 / 759 / 794 | 259 / 285 / 309 | **116 / 125 / 135** (raw) |
| First case detail (M2; M0: SL-M0) | 720 / 759 / 794 | 259 / 285 / 309 | **116 / 125 / 135** (raw events) |
| Authoritative replay (T25) | 250 / 283 / 315 | 236 / 256 / 283 | 227 / 256 / 283 |
| Create (M3) | 665 / 704 / 739 | 439 / 465 / 489 | 443 / 465 / 489 |
| Edit and persist (M4) | 492 / 531 / 566 | 617 / 643 / 667 | 621 / 643 / 667 |

**Validity of the columns:**
- **Approved and optimized, rows 1–4:** schedule times only. Under v4.1.4 they cannot pass against real data without ODR-R10-25, -28 and -29 (TIME-TO-WORKING-APPLICATION.md, limit 1).
- **Authoritative replay:** the slice integrates, but **cannot represent the catalog** under v4.1.4 (C1).
- **M0 column:** needs ODR-R10-30, and ODR-R10-28 for the real-provider read. It needs nothing from -25, because it does not decode events.

## The three M0 variants

| SL-M0 waits for | Estimate (assumption) | M0 at 0 / 5 / 10-min CI | Full completion at 10-min CI | Obligations by T+420 at 10-min CI |
|---|---|---|---|---|
| T16 (uses the shell) | 30 | 149 / 166 / 189 | 769 | 527 |
| T11 (own minimal shell) | 45 | 123 / 138 / 153 | 781 | 487 |
| **T10 only** (own shell and pins) | 55 | **116 / 125 / 135** | **769** | **527** |
| *(no M0: R2)* | – | – | 769 | 527 |

The best variant costs nothing in the model: completion and obligations at 10-min CI equal R2's. M0 fills `ui` lane time that would otherwise wait for T17. The *after-t11* variant does cost: it delays T16, and through it the UI chain.

## Critical path to M0, and what blocks 60–120 minutes

The T10-only variant:

| Step | Minutes | Basis |
|---|---|---|
| set-up | 25 | ODR-R10-23 planning value. Measured machine time is 3–6 min (INITIALIZATION-OPTIMIZATION.md) |
| T10 bootstrap | 29 | DAG estimate. The v4.1.4 bootstrap itself ran in 2 s here (*measured*) |
| T10 integration | 3 + CI | merge estimate + CI latency |
| SL-M0 | 55 | assumption. Basis: Rehearsal 009 committed a full first application 19 min after its bootstrap (*measured*, 2026-11-04 00:32 → 00:51 UTC) |
| SL-M0 integration | 3 + CI | – |
| **Total** | **115 + 2 × CI** (the model gives 116 at 0 CI, by its minute ticks) | – |

- **The 60–120 minute target holds only at near-zero CI with today's estimates:** 116, 125 and 135 at 0, 5 and 10-min CI.
- **The set-up and the bootstrap estimate are the largest remaining assumptions.** Planned at their measured values (5-min set-up, about 10-min bootstrap), the same chain gives roughly 75 + 2 × CI. That is an estimate, not modelled.
- **Rehearsal 009, measured:**
  - first working application commit at **T+21**;
  - accepted candidate (36/37 requirements verified) at **T+78**;
  - one agent, a single integrated gate at the end.

  That is the evidence that the work fits in 60–120 minutes. The governed one-gate-per-slice process is what adds the rest.

## Measured against assumed

| Measured | Assumed |
|---|---|
| Rehearsal 009 timeline: T0 00:30:01Z, app 00:51, accepted 01:48 | every DAG estimate |
| Probe: catalog + 4 histories read and parsed in 1.6 s | the M0 slice estimate (30–55) |
| v4.1.4 bootstrap generation: 2 s | CI latency (0 / 5 / 10 are sensitivity values) |
| Set-up machine time: 3–6 min | 3-min merges; no merge or supervision agent time |
