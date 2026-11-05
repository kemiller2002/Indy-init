# Workstream D: dependency-reduced plan

**Authority.** The owner's "Parallel Acceleration Directive" (2026-11-05 19:00 UTC, issued in the lead session), §6. The lead session assigned workstream D to this agent. Nothing here is an owner decision: anything that changes an owner record is listed in §4 for the lead's owner batch.

**Rule.** Nothing is discarded. Deferred work keeps its requirement IDs and its incomplete status (§3). No deferral is reported as completed.

## 1. Is v4.1.5 on the critical path?

| Target | Needs v4.1.5? | Evidence |
|---|---|---|
| The working preview application (Connect → Browse → Open → Create → Save → Reload) | **No.** It is built on the causa-storage v1.0.0 contract directly (`preview/causa/DECISIONS.md` D-09), and it already passes on real GitHub. | Run 42067851789 in `kemiller2002/causa-testing-repo`: 31/31 engine tests, 11/11 real-provider checks, A1–A7 PASS; `tests/read-path.mjs` passes 25/25 against the deployed site |
| The governed competition build, against frozen contracts | **Yes.** Frozen v4.1.4 `CoreEvent` cannot represent 487 of the 1,315 catalog events. It also has no workspace addressing, no listing and no creation path. | `proposals/r10-historical-data-compatibility/SCHEMA-DIFF.md` §2, §5; ODR-R10-25 A (recorded) |

So v4.1.5 is blocking for the competition build only. The smallest contract change that unblocks it is **E1 + W1 + L1 + C1** (`docs/rehearsal-010-optimization/v415-cost-analysis/MINIMUM-SUCCESSOR.md` §1):

| ID | Change |
|---|---|
| E1 | The lossless 31-type event union, the envelope, and `ReaderStage` |
| W1 | The derived workspace |
| L1 | Listing: a request/interpret pair beside `Connector` |
| C1 | Creation inside existing SM-SAVE states |

The core is the same under both options of OD-V415-01, staged or combined, so it starts now without waiting for that decision.

## 2. Classification

### Blocking: needed for the working application, correctness, security or required acceptance

| # | Work | Owner | State |
|---|---|---|---|
| B1 | v4.1.5 core: E1, W1, L1, C1. Interface-change and state-machine requests; compile; X01–X21; the offline suite; historical checks C11/C15/C16 against the transcribed contract | D (this agent) | started 19:15 UTC |
| B2 | Independent review of B1 by another agent. Then O2/O3 (approve, tag) and the lock re-pin | reviewer; then owner | after B1 |
| B3 | Milestone-first plan M1–M4 and its governed gates | A (lead) | lead |
| B4 | Delegated-authorization mechanism | lead | lead (45-minute timebox) |
| B5 | Real-provider write for the rehearsal repository `causa-acceptance-r10` (ODR-R10-28 A) | owner (credential) | open |
| — | Preview real write | — | **done** (D-14) |

### Parallelizable: necessary, but off the application's critical path

| # | Work | Why it does not block |
|---|---|---|
| P1 | The IF41-VIEW fold (CF-09 A, recorded): one declared change to `07-IF41-VIEW.fs`, with inputs ready on PR #128 | It is applied as the last, separable change to v4.1.5 and reviewed in parallel with B1. B1 never waits on it. |
| P2 | UI roles ui-a, ui-b, ui-c (CF-10 A, recorded), through the fold's `project-structure.delta.json` | Same route as P1 |
| P3 | Toolchain bundle and warm caches (ODR-R10-24 A) | Needs no contract |
| P4 | CI batching and timing measurements | Workstream C |
| P5 | A requirements-derived `History.validate` (OD-HC-03 A) | Domain implementation inside the competition window. Its signature already fits IF41-DOMAIN `Replay`, so it needs no contract. |
| P6 | Preview: guidance on the anonymous GitHub rate limit (60 requests/hour per address); a token is accepted at Connect | Already shown as an error; a demo risk only |

### Deferrable: valuable, but unnecessary for the first working application

| # | Work | Deferred to | Needs the owner? |
|---|---|---|---|
| F1 | Ownership rebalancing FO-1..FO-3 beyond CF-10's UI roles (ODR-R10-20 A), including the IF41-APP split FO-2 | v4.1.6 | **yes**: it departs from recorded ODR-R10-32 A (OD-V415-01) |
| F2 | The T19/T22 DAG split (ODR-R10-21 A). The compiler refuses new tasks (`V41Package.fs:778`). | a work-item split in the scope successor (ODR-R10-19 machinery), or v4.1.6 | **yes**, as F1 |
| F3 | Content with no causa-storage v1.0.0 wire form: INV-023, -025, -027, -029, -030 update, -034, -035 (ODR-R10-26 A, OD-HC-06 B) | causa-storage 1.1.0, owned outside Indy-init | no; already recorded |
| F4 | Further schedule-model refinements, and further revisions of the v4.1.5 cost analysis | after the competition | no |
| F5 | Generalized workspace abstractions. W1 is exactly one rule: the catalog workspace from `catalog/active.json`, the working copy from a fixed prefix. | after the competition | no |
| F6 | Styling beyond the Forma design contract; new framework or tooling evaluations; optional package restructuring | after the competition | no |
| F7 | Repeated reviews of unchanged decisions; stale PRs #107 (v4.1.4 step 2, superseded by ODR-V414-03) and #99 (Rehearsal 009 readiness) | none; no work scheduled | the owner may close them |

## 3. Deferred obligations record

Each item keeps its source decision and the status `incomplete`. None is reported as satisfied.

| Item | Source record | Status | Restores when |
|---|---|---|---|
| FO-1, FO-2, FO-3 | ODR-R10-20 A | incomplete, deferred to v4.1.6 (pending OD-V415-01) | v4.1.6 freeze |
| T19/T22 split | ODR-R10-21 A | incomplete, deferred (pending OD-V415-01) | work-item split, or v4.1.6 |
| INV-023, -025, -027, -029, -030 update, -034, -035 on historical views | ODR-R10-26 A, OD-HC-06 B | incomplete, descoped for historical views, **not weakened** | causa-storage 1.1.0 |
| Falsification scope on the event (scenario A4, `docs/INVESTIGATION-ACCEPTANCE-SCENARIOS.md:47`). causa-storage v1.0.0 stores none; scope can be recorded via `NegativeKnowledgeEstablished`. | ODR-R10-26 A, OD-HC-06 B | incomplete, **not weakened** | causa-storage 1.1.0 |
| Commands for the 22 added event kinds (reading needs only the events) | v4.1.5 scope (workstream D) | incomplete, deferred | domain work, or v4.1.6 |
| Admissibility (CONTRACT §11.3) | OD-HC-03 A | incomplete. Views say "admissibility not evaluated". | `History.validate` passes C07 (35 negative fixtures) |

## 4. For the lead's owner batch

| Item | Why it is the owner's |
|---|---|
| OD-V415-01 A: stage v4.1.5 as the core plus CF-09/CF-10, and defer F1/F2 to v4.1.6 | It changes recorded ODR-R10-32 A ("one combined package"). The directive reserves recording a scope change. |
| OD-V415-03 A: the fallback trigger | It declares acceptance failures (ODR-R10-25 B) |
| O2/O3: approve and tag v4.1.5 | The freeze needs the owner's own signed record (`owner-freeze.fsx` signedByYou; launch-preflight LP-05) |

These are also owner records. The lead's delegated-authority record (DA-R10-1) refuses every ODR/OD id by design:
- ODR-V415-01, the coupled contract change for E1, W1, L1 and C1. In v4.1.5, OD-V415-01 became ODR-V415-03 and OD-V415-03 became ODR-V415-04;
- ODR-V415-IF-VIEW.

OD-V415-02, -04 and -05 are technical choices, logged by the lead as DTD entries. They satisfy no request.
