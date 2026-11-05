# Parallel execution options

**The constraint all three scenarios share:** `requirements-compiled/v4.1.5/` is one compiled output. Two of its files carry every change: `analysis/amendment.json` holds the digests of every changed contract, and `package-manifest.json` digests everything. So **only one agent can write the package at a time.** Any other agent's work has to be an *input* that this one author transcribes, or a *review*.

## Scenario comparison

| | A: one agent designs and compiles everything | B: separate agents produce reviewed input contracts; one compilation owner | C: one compilation owner; others independently verify |
|---|---|---|---|
| Critical-path elapsed (est.) | about 16–34 h. Everything is serial, including design of 2, 4, 5 and 7. | about 12–24 h. Designs 1, 2–4, 5 and 7 are produced in parallel and reviewed, then 8–11. | about 13–26 h. Design is serial (2 → 4) but review overlaps authoring. |
| Coordination overhead | lowest | **highest**: four input contracts, each with its own review, then reconciliation by the compiler owner (FO-2 changes IF41-APP, which item 5's UI contracts also change) | medium: verifiers read and report, but never write the package |
| Integration risk | low for consistency, high for blind spots (the author reviews their own design until item 10) | **highest**: inputs designed separately can disagree. IF41-APP is touched by FO-2, the UI contracts and workspace/listing at once. | low-medium |
| Shared-file conflicts | none | conflicts move into input files: `06-IF41-APP.fs` is changed by three inputs | none (verifiers do not edit) |
| Review requirement | one large review (item 10), several rounds likely | one review per input, **plus** the package review | one package review, with verification findings arriving early |
| Probability of rework (qualitative, no measured basis) | **high**. Measured: earlier single-author successors needed 3–5 rounds even when small. | medium-high, at reconciliation | **medium**. Early independent checks catch shape errors before round 1. |

## What can safely run in parallel without anyone editing the compiled package

| Work | Who | Output (outside `requirements-compiled/`) |
|---|---|---|
| Event-union transcription check against the 1,315 events | historical-compatibility agent (this one) | `proposals/r10-historical-data-compatibility/`: already done (C11, C15). Re-run against the transcribed contract as a verifier. |
| Workspace, listing and creation semantics against causa-data and EXT-DATA/PERSIST requirements | an independent verifier | a review note, with a check that the design reads `catalog/active.json`, writes only under `workspaces/demo/<runId>/` (CONTRACT §9.4.4), and adds no state or event (X05/X12) |
| UI contract reconciliation with IF41-APP/IF41-VIEW (fold versus extend) | the contract-first agent | an input proposal in `proposals/r10-contract-first-ui/`; only needed for the complete package |
| Ownership (FO-1..FO-3) and the T19/T22 route | the optimization agent | an input proposal in `docs/rehearsal-010-optimization/`; only needed for the complete package |
| A Praxis work-item split of T19/T22, if chosen instead of a compiler change | the optimization agent | scope-successor input (ODR-R10-19 machinery) |

**Recommended: Scenario C**, with the minimum successor first (`FINAL-RECOMMENDATION.md`).
