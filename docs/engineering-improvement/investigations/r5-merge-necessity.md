# Rehearsal 5: were the merges into stream branches necessary? (Priority Three)

Retrospective and **non-causal**. Read only from a clone of `kemiller2002/indy-init-rehearsal-005` (history fetched
with `git fetch --depth=1000 origin main`); nothing was written to it.

## Method

For every two-parent merge commit committed in the rehearsal window (2026-10-27T20:08:38Z to the final verdict
2026-10-28T02:41:07Z) on any ref:

1. `base = git merge-base P1 P2`.
2. `A = git diff --name-only base P1`, `B = git diff --name-only base P2`; overlap is `A ∩ B`.
3. Conflict: `git merge-tree --write-tree --name-only P1 P2` exits 1.
4. Direction: a subject of the form `Merge remote-tracking branch 'origin/main' into r5/<stream>` is a merge of main
   *into* a stream; every other merge integrated a stream into main.

## Result

| Direction | Merges | No path overlap | Overlap, merged cleanly | Overlap, conflicted |
|---|---|---|---|---|
| main into a stream | 58 | 36 | 14 | 8 |
| a stream into main | 34 | 22 | 3 | 9 |
| **total** | **92** | **58** | **17** | **17** |

The totals agree with `r5-final-metrics.md` (92 merges in the window, 17 conflicted, 58 into stream branches).

## Reading

- 36 of the 58 merges of main into a stream (62%) brought in changes that touched none of the paths the stream had
  changed since their common ancestor. A pre-merge overlap report would have shown them as unnecessary for avoiding
  conflicts. That is an **upper bound** on avoidable merges: a stream can need main for a semantic dependency (an
  engine API it calls) without any path overlap, and this replay cannot see that.
- Conflicts concentrated on shared files (`Adapter.fs`, `View.fs`, `fixture-mode.spec.js`, `EnginePort.fs`,
  `graph-navigation.spec.js`; see `r5-final-metrics.md`). Those are exactly the paths an integration contract declares
  as shared interfaces.
- `praxis streams check` (praxis.integration-contract/1, merged in praxis#295) already reports, before a merge,
  each stream's overlap with the target and with its siblings, ownership violations, unapproved shared-interface
  changes, merge readiness and the dependency-ordered integration sequence. No new mechanism is needed to act on this
  result; what Rehearsal 5 lacked was using one. Rehearsal 6 requirement ESO-R6-07 makes it the rule.

No reduction in integration work is claimed: this replay shows where coordination work went, not that a different
rule would have saved it.
