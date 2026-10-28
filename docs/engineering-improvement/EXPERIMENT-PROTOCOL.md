# Engineering improvement experiment protocol (pre-registered)

Workstream H of the Echelon Engineering Improvement Initiative. This protocol is
written **before** any treatment result is known, so that later comparisons
cannot be fitted to the data. Changing it after a run starts requires a new
version of this file and a recorded reason.

Version: 1 (2026-10-28). Baseline: [`BASELINE-2026-10-28.md`](BASELINE-2026-10-28.md).

## Question

Do the initiative's engineering changes increase correct, independently verified
completion of an equivalent workload, and do they reduce avoidable integration
and verification work, without reducing defect detection?

## What is preserved

These are frozen inputs. A run that changes any of them is not comparable and is
reported separately.

- Requirements baseline `BL-CAUSA-INDY-INIT-aab8f80c7743` (sha256
  `621e2f96…57b9`) and its 108 P0 requirements.
- The compiled requirements package version the run declares at kickoff
  (finding 6 in the baseline: a frozen package must not change after freeze).
- Requirement identities, obligation identities and test contracts.
- The acceptance standard: independent verdict computed from a revision-bound
  ledger; no self-declared acceptance.
- Evidence provenance: every result is bound to source commit, run and evidence
  integrity as the active freshness policy requires.
- Rehearsal 4 and Rehearsal 5 repositories are historical records and are not
  edited.

## Groups

| Group | Engineering stack | Changes enabled |
|---|---|---|
| Control | the stack pinned by Rehearsal 5 (echelon-current 1.29.0) | none |
| A | control + Praxis integration contracts (Workstream B) | stream contract, pre-merge overlap, dependency-ordered integration |
| B | control + executable acceptance planning (Workstream C) | obligation-level plan with evidence categories and mapping checks |
| C | control + incremental verification (Workstream G) | policy-aware freshness, failing-outcome provenance, safe reuse |
| D | all initiative changes (A–G) | everything above plus WASM preflight, persistence safety, environment preflight, Forma roles |

Workstreams A, D, E and F are defect preventions. They are evaluated by their own
reproduce-then-prevent tests and only collectively in group D.

## Equivalent workloads

Rehearsal-scale runs are expensive, so the unit of comparison is a **task
packet**: a bounded slice of the Causa requirements (for example: one
persistence scenario end to end, one screen with its obligations, one
verification gate) with a fixed starting repository state, fixed requirements
subset, fixed time ceiling and fixed acceptance check.

- Each packet is defined once, before any group runs it, with its starting
  commit, requirement ids, obligation ids, and acceptance command.
- Every group runs the **same** packets from the **same** starting state.
- Packet order is randomized per group (recorded seed), so learning effects in
  the agent prompt or cache do not favor one group.
- Agents receive the same model, the same prompt template and the same context
  budget; only the engineering stack differs.
- A full rehearsal is a separate observation and is never pooled with packet
  results.

## Metrics

Primary:

1. Correctness: acceptance check verdict per packet (pass / fail / incomplete).
2. Mandatory acceptance coverage: P0 obligations with fresh, bound evidence.
3. Time to independently verified completion (wall clock from packet start to
   independent verdict).

Secondary: total execution time, agent tokens, tool calls, CI runs, merge
conflicts, reconciliation commits, rework (commits reverting or rewriting
same-run work), runtime failures, human interventions.

Rules:

- A metric that was not collected is reported as **unavailable**, never zero
  and never estimated. Tokens and tool calls count only when provider telemetry
  is ingested into the Praxis execution record (both rehearsals so far recorded
  none).
- Commit counts and CI durations are never used as a proxy for tokens.
- Defect detection is checked by seeded defects: each packet carries at least
  one known defect injected into a copy; a group that misses it fails that
  packet for detection, regardless of efficiency.

## Analysis

- Report each metric per group as the per-packet values, the median and the
  range. With fewer than about ten packets per group, report descriptive results
  only. Do not report a p-value or claim significance.
- Paired comparison per packet (treatment − control) is the main view, because
  packets differ in difficulty.
- A treatment is called **improved** on a metric only if it is better on a
  majority of packets **and** no packet lost correctness or defect detection.
  Otherwise it is **mixed**, **not improved**, or **unverified** (not run).
- A single uncontrolled comparison, including any comparison of Rehearsal 4
  with Rehearsal 5, is never reported as causal.

## Retrospective replays (already possible, non-causal)

Before controlled runs exist, the initiative reports retrospective replays
against recorded history, labelled **non-causal**:

- Workstream B: replay of Rehearsal 5's merges against a reconstructed stream
  contract (bias: the contract is derived after the fact).
- Workstream C: compiling the Rehearsal 4 and Rehearsal 5 baselines and running
  negative controls.
- Workstream G: replay of a real commit series under full, dependency-aware and
  reuse strategies.

## Limitations known in advance

- Packets are smaller than a rehearsal; integration effects (Workstream B) are
  weaker in small packets and may be under-measured.
- Agent behavior is stochastic; with few packets, variation between runs can
  exceed treatment effects.
- Telemetry availability depends on the runtime; if tokens remain unavailable,
  efficiency claims are limited to wall-clock time, CI runs and merges.
