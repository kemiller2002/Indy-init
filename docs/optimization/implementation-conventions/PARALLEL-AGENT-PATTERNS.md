# Parallel agent patterns

These are written to fit EXP-INDY-002's asynchronous contract-driven model, and to be measured by it. This work does not read, change or depend on EXP-INDY-002's branches, agents or evidence.

Authoritative sources, which this file does not restate:
- `requirements-compiled/v4.1.5/AGENT-RULES.md` (roles, interface freeze at T14, merge order);
- `agent-file-ownership.json`;
- `proposals/r10-contract-first-ui/COMPETITION-UI-OWNERSHIP.json` (UI-1/UI-2/UI-3);
- `docs/rehearsal-010-optimization/execution-readiness/AGENT-HANDOFF-CONTRACT.json`, `INTEGRATION-CONTRACTS.md` and `FILE-OWNERSHIP-RECOMMENDATIONS.json`;
- `requirements-compiled/v4.1.4/analysis/conflict-telemetry.json` (how conflicts are measured).

## What the record shows

| Observation | Rehearsal | Measured | Finding |
|---|---|---|---|
| Ownership by commit-message convention only: 17 of 92 merges conflicted, with hotspots `Adapter.fs`, `View.fs`, specs, `EnginePort.fs`. 26 reconcile commits. | R5 | counts | HF-015 |
| Strict single-owner files: no source conflicts recorded, but 19 change-request commits, with integration as the bottleneck (89.9 M tokens) | R8 | counts | HF-043 |
| Shared tool state (`.ros/context/current.json`, `events.jsonl`) caused 26 of 49 merges to conflict | R8 | counts | HF-042 |
| Two sessions repaired the same defects in parallel and produced competing successor packages (v4.1.3 vs v4.1.4) | post-R9 | 6 overlapping files | HF-044 |
| Same-owner serialization on `Update.fs`, `Engine.fs`, `Projections.fs` and others | post-R10 | **modeled** (FO-1: 769 → 684 min) | HF-079 |
| The schedule model ignored ownership: T+519 vs T+720 | post-R10 | **modeled** | HF-066 |
| A claim from another session could not be verified by the lead | post-R10 | — | HF-078 |

**Conclusion on file granularity.** Single ownership removed source conflicts (R5 → R8). It moved the cost into change requests, and into tool-state conflicts, which are not source files. The remaining file-size question cannot be answered from measured data, because no multi-agent run has used the v4.1.x ownership. **"Smaller files are better" is not supported, and not refuted.** It is a Tier 2 question for EXP-INDY-002.

## Rules

| ID | Rule | Level | Tier | Enforced by |
|---|---|---|---|---|
| PA-01 | **One owner per path.** Compile lists are written once at bootstrap; no agent adds files later. | MANDATORY (CV-08) | 1 (exists) | `agent-file-ownership.json`, `OwnershipTests` |
| PA-02 | **Shared tool state is append-only per lane, or excluded from merges.** No per-merge reconcile commits of `.ros` state. | PREFERRED (CV-08) | 2 | Praxis merge policy: proposal to Praxis/Conditor. Not enforced here. |
| PA-03 | **A released contract version is immutable.** A change is a new version, with every consumer's contract tests in the same PR. | MANDATORY (exists for frozen packages) | 1 (exists) / 2 (in-run asynchronous release) | frozen-package CI equality; AGENT-RULES T14 |
| PA-04 | **Each PR names the contract version it was built against.** Integration refuses a mismatch before merge. | PREFERRED (CV-12) | 2 | review obligation; becomes a check if EXP-INDY-002 shows mismatches occur |
| PA-05 | **Explicit dependency notification.** A producer that releases a contract version posts it as a Praxis event the consumer's work item depends on. Consumers start from released versions only. | PREFERRED | 2 | Praxis work-item dependency (existing capability) |
| PA-06 | **Domain and UI work in separate working directories or branches.** Each role has its own branch, and integration owns `main`. | MANDATORY (exists) | 1 (exists) | AGENT-RULES merge order |
| PA-07 | **One session per defect.** Before a repair starts, its work item is claimed in Praxis. A second session that finds the item claimed reviews instead of repairing. | PREFERRED | 1 (pre-competition sessions) | Praxis work-item claim; review obligation |
| PA-08 | **Cross-session evidence counts only when reproducible by the lead,** at the candidate SHA. | MANDATORY (CV-15) | 1 (exists) | completion gate |
| PA-09 | **Measure contract revisions and integration rework.** Record every contract version released after T14, and every PR reworked after integration feedback, in the conflict-telemetry format. | MANDATORY for EXP-INDY-002 evaluation | 2 | `conflict-telemetry.json` metrics (an unavailable value is never estimated) |

## What EXP-INDY-002 should report, so Tier 2 can be decided

1. Merge conflicts, split into textual (git) and semantic (contract or integration test failed after a clean merge), per `conflict-telemetry.json`.
2. Contract versions released after T14, and the minutes each consumer spent adapting.
3. Minutes agents spent idle waiting for a contract or a change request.
4. Change requests to shared files: count, and minutes from request to application.
5. Any edit by two roles to one path. It should be zero; any other value falsifies PA-01's sufficiency.

The Tier 2 conventions (CV-08 PA-02, CV-12, PA-04, PA-05) are adopted only if those numbers show the failure they target occurring. Otherwise they stay as written guidance.
