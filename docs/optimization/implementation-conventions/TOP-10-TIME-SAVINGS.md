# Top 10 conventions by net critical-path time saved

Ranking is by **central standalone net critical-path minutes saved per seven-hour build**: modeled prevented minutes minus the convention's own in-competition overhead. It is not by severity, frequency or documentation value. Every figure below is generated from [CONVENTION-CATALOG.json](CONVENTION-CATALOG.json) and [FAILURE-DATA.json](FAILURE-DATA.json) by `tools/conventions/conventions.fsx render`, and `validate` fails if this file goes stale.

**Measured vs modeled.**
- **Measured:** the "Observed repair or delay minutes" lines, quoted from sources. Where a source states none, they say "unknown".
- **Modeled:** recurrence probability, minutes prevented, overhead and net. The model is in IMPACT-FORECAST.md.

**Overlap.**
- **Within the catalog:** the cumulative column combines conventions as `1 − Π(1 − prevention)` per failure mode. The combined top 10 therefore never counts one prevented defect twice, and is always below the sum of the standalone figures.
- **With EXP-INDY-002:** overlap is removed before any convention is credited.

## Ranking and cumulative forecast

<!-- GENERATED:top10 BEGIN (tools/conventions/conventions.fsx render; do not edit) -->
| Rank | Convention | Level | Measured historical critical-path minutes (findings) | Standalone net, cons / central / opt | Overhead, cons / central / opt | Cumulative net in rank order, cons / central / opt | Safety rule |
|---:|---|---|---|---|---|---|---|
| 1 | CV-02 Stop only on STOP_PERMITTED, clock from the T0 record | MANDATORY | HF-048 305.0 | 4.4 / 29.0 / 95.6 | 1.0 / 1.0 / 0.5 | 4.4 / 29.0 / 95.6 |  |
| 2 | CV-03 One scope source, pinned and fully itemised before code | MANDATORY | none stated | 3.0 / 26.8 / 98.0 | 3.0 / 2.0 / 1.0 | 7.4 / 55.8 / 193.6 |  |
| 3 | CV-01 Execute every generated command before freeze | MANDATORY | none stated | 3.3 / 17.6 / 61.7 | 0.0 / 0.0 / 0.0 | 10.7 / 73.4 / 255.2 |  |
| 4 | CV-11 Contracts proven against real data before release | MANDATORY | none stated | 3.0 / 14.7 / 51.0 | 0.0 / 0.0 / 0.0 | 13.7 / 88.1 / 306.2 |  |
| 5 | CV-09 Durable means read back from the real provider | MANDATORY | none stated | -0.3 / 13.0 / 57.8 | 3.0 / 2.0 / 1.0 | 13.4 / 101.1 / 364.0 | yes |
| 6 | CV-06 Workflows pass Conditor ci-policy | MANDATORY | HF-011 90.0 | 0.5 / 10.0 / 35.2 | 1.0 / 0.5 / 0.5 | 13.9 / 111.1 / 399.2 |  |
| 7 | CV-04 WASM boot smoke on the first CI build; bounded view functions | PREFERRED | HF-007 120.0; HF-013 90.0 | -1.2 / 8.8 / 47.0 | 3.0 / 2.0 / 1.0 | 12.7 / 119.9 / 446.2 |  |
| 8 | CV-17 Forma first: current Forma, no containment workarounds | MANDATORY | none stated | -0.5 / 7.7 / 33.7 | 2.0 / 1.0 / 0.5 | 12.3 / 127.6 / 479.9 |  |
| 9 | CV-10 Typed errors through one state machine; no silent defaults | MANDATORY | none stated | -0.5 / 7.1 / 29.2 | 2.0 / 1.0 / 0.5 | 11.8 / 134.7 / 509.1 | yes |
| 10 | CV-16 Only the owner records approvals | MANDATORY | none stated | 1.1 / 6.4 / 20.0 | 0.0 / 0.0 / 0.0 | 12.8 / 141.0 / 529.1 | yes |
<!-- GENERATED:top10 END -->

<!-- GENERATED:summary BEGIN (tools/conventions/conventions.fsx render; do not edit) -->
- Residual preventable pool (expected critical-path minutes per build without these conventions, net of EXP-INDY-002 overlap), cons / central / opt: 85.4 / 316.6 / 912.5
- Peak cumulative net along the ranked order (overlap-adjusted, overhead deducted): conservative 14.7 at rank 15; central 179.5 at rank 19; optimistic 712.4 at rank 23
- Smallest ranked prefix reaching 80% of the peak: conservative top 4 (CV-02, CV-03, CV-01, CV-11); central top 11 (CV-02, CV-03, CV-01, CV-11, CV-09, CV-06, CV-04, CV-17, CV-10, CV-16, CV-15); optimistic top 12 (CV-02, CV-03, CV-01, CV-11, CV-09, CV-06, CV-04, CV-17, CV-10, CV-16, CV-15, CV-07)
- Secured by conventions an existing check already enforces (kept only while those checks stay on), cons / central / opt: 12.3 / 100.8 / 353.8
- Incremental from not-yet-enforced conventions with positive central net, given the secured set, cons / central / opt: -5.0 / 78.5 / 335.9
<!-- GENERATED:summary END -->

**Reading the 80% line.** The evidence supports the calculation only as a model: every input except the historical minutes is an assumption. The set changes with the scenario. Conservatively, four conventions capture 80% of a small peak, and an existing check already enforces three of them. At central, the set is about the top 11. The time savings are spread thin across many failure modes; no "vital few" exists here.

## Per-convention detail

<!-- GENERATED:top10detail BEGIN (tools/conventions/conventions.fsx render; do not edit) -->
### 1. CV-02 Stop only on STOP_PERMITTED, clock from the T0 record

- Level: MANDATORY; tier 1; status ENFORCED-EXISTING
- Rehearsals where the problem occurred: R007, R008, R009
- Evidence: HF-048, HF-057 (FAILURE-DATA.json); convention evidence HF-048, HF-057
- Observed repair or delay minutes (measured): HF-048: 305.0 min (unused-window).
- Estimated recurrence probability per build, cons / central / opt (modeled): FM-01 0.15 / 0.25 / 0.35
- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): 5.4 / 30.0 / 96.1
- Enforcement and implementation overhead on the critical path, cons / central / opt: 1.0 / 1.0 / 0.5
- Net critical-path minutes saved, standalone, cons / central / opt: 4.4 / 29.0 / 95.6; cumulative in rank order: 4.4 / 29.0 / 95.6
- Confidence: medium. Key assumptions: Probability is the chance an agent stops early or misreads the clock without the stop rule (2 of the 8 observed rehearsals, R7 and R9); impact uses R9's measured 305 min as the optimistic bound.
- Enforcement mechanism: requirements-compiler stop-decision; --t0-record/--deadline-policy; requirements-compiler stop-decision
- Implementable before the competition: yes (already enforced; keep it on)

### 2. CV-03 One scope source, pinned and fully itemised before code

- Level: MANDATORY; tier 1; status ENFORCED-EXISTING
- Rehearsals where the problem occurred: R003, R008, R009, R010-readiness, post-R10
- Evidence: HF-001, HF-037, HF-038, HF-064, HF-070, HF-062 (FAILURE-DATA.json); convention evidence HF-001, HF-037, HF-038, HF-064, HF-062
- Observed repair or delay minutes (measured): none stated in any source (unknown).
- Estimated recurrence probability per build, cons / central / opt (modeled): FM-02 0.25 / 0.40 / 0.55
- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): 6.0 / 28.8 / 99.0
- Enforcement and implementation overhead on the critical path, cons / central / opt: 3.0 / 2.0 / 1.0
- Net critical-path minutes saved, standalone, cons / central / opt: 3.0 / 26.8 / 98.0; cumulative in rank order: 7.4 / 55.8 / 193.6
- Confidence: medium. Key assumptions: Scope ambiguity recurred in R3, R8, R9 and readiness; impact if it recurs is 60-240 min of misdirected build. check-sequencing already enforces itemisation; value shown is what that enforcement secures.
- Enforcement mechanism: r9.fsx check-sequencing; launch-preflight LP-04 scope digest; r9.fsx check-sequencing
- Implementable before the competition: yes (already enforced; keep it on)

### 3. CV-01 Execute every generated command before freeze

- Level: MANDATORY; tier 1; status ENFORCED-EXISTING (v4.1.4+); extend to deploy preconditions
- Rehearsals where the problem occurred: R005, R008, R009, post-R10
- Evidence: HF-028, HF-029, HF-033, HF-047, HF-018, HF-076, HF-053, HF-055 (FAILURE-DATA.json); convention evidence HF-028, HF-029, HF-033, HF-047, HF-076, HF-018
- Observed repair or delay minutes (measured): none stated in any source (unknown).
- Estimated recurrence probability per build, cons / central / opt (modeled): FM-03 0.30 / 0.50 / 0.70; FM-22 0.15 / 0.30 / 0.45
- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): 3.3 / 17.6 / 61.7
- Enforcement and implementation overhead on the critical path, cons / central / opt: 0.0 / 0.0 / 0.0
- Net critical-path minutes saved, standalone, cons / central / opt: 3.3 / 17.6 / 61.7; cumulative in rank order: 10.7 / 73.4 / 255.2
- Confidence: medium. Key assumptions: Generated-command failures recur whenever a successor package is cut (R5, R8, R9); suite-preflight already catches the MSB1008 class, so the residual is deploy preconditions and environment setup. Impact if it occurs is 20-90 min (R8: live suites unexercised to T+94, first deploy T+129).
- Enforcement mechanism: r9.fsx suite-preflight --require-tests; launch-preflight.fsx LP checks; Conditor scaffold CI; dotnet fsi tools/r9/r9.fsx suite-preflight ...
- Implementable before the competition: yes (already enforced; keep it on)

### 4. CV-11 Contracts proven against real data before release

- Level: MANDATORY; tier 1; status PROPOSED; content addressed by v4.1.5 CANDIDATE (E1 all 50 causa-storage events, W1 workspace) pending ODR-V415-01/-02
- Rehearsals where the problem occurred: R008, R009, post-R10
- Evidence: HF-068, HF-069, HF-034 (FAILURE-DATA.json); convention evidence HF-068, HF-069, HF-034
- Observed repair or delay minutes (measured): none stated in any source (unknown).
- Estimated recurrence probability per build, cons / central / opt (modeled): FM-11 0.20 / 0.35 / 0.50
- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): 3.0 / 14.7 / 51.0
- Enforcement and implementation overhead on the critical path, cons / central / opt: 0.0 / 0.0 / 0.0
- Net critical-path minutes saved, standalone, cons / central / opt: 3.0 / 14.7 / 51.0; cumulative in rank order: 13.7 / 88.1 / 306.2
- Confidence: medium. Key assumptions: Not yet observed failing in a timed run because R10 did not run; the 487/1,315 gap is measured. The v4.1.5 candidate carries the fix, so the value depends on its approval.
- Enforcement mechanism: proposals/r10-historical-data-compatibility replay coverage (HISTORICAL-COVERAGE.json, EVENT-COMPATIBILITY-MATRIX.json)
- Implementable before the competition: yes, after owner approval (owner: ODR-V415-01 and ODR-V415-02 (v4.1.5 approval and freeze); the convention itself (replay coverage as a release condition for every later package) is a new rule)

### 5. CV-09 Durable means read back from the real provider

- Level: MANDATORY; tier 1; status PARTIALLY ENFORCED; safety/correctness rule (enforced regardless of time value)
- Rehearsals where the problem occurred: R002, R004, R005, R006, R007, R008, post-R10
- Evidence: HF-006, HF-021, HF-026, HF-040, HF-039, HF-074 (FAILURE-DATA.json); convention evidence HF-006, HF-008, HF-021, HF-026, HF-039, HF-040, HF-074
- Observed repair or delay minutes (measured): none stated in any source (unknown).
- Estimated recurrence probability per build, cons / central / opt (modeled): FM-09 0.30 / 0.50 / 0.70
- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): 2.7 / 15.0 / 58.8
- Enforcement and implementation overhead on the critical path, cons / central / opt: 3.0 / 2.0 / 1.0
- Net critical-path minutes saved, standalone, cons / central / opt: -0.3 / 13.0 / 57.8; cumulative in rank order: 13.4 / 101.1 / 364.0
- Confidence: medium. Key assumptions: The most recurrent family (7 rehearsals/experiments) but minutes were never recorded; prevention is capped at 30-70% because each past fix covered one variant.
- Enforcement mechanism: live-provider preflight (J3-05); Dokimos evidence category real-provider vs double; state-machine tests; tools/requirements-compiler/tests/live-provider-preflight.yml; Reviewer confirms each durability obligation cites a real-provider run, not a double.
- Implementable before the competition: yes

### 6. CV-06 Workflows pass Conditor ci-policy

- Level: MANDATORY; tier 1; status ENFORCED-EXISTING
- Rehearsals where the problem occurred: Forma, R004, R006, R007, R008, R009, post-R10
- Evidence: HF-011, HF-031, HF-032, HF-075, HF-086 (FAILURE-DATA.json); convention evidence HF-011, HF-031, HF-032, HF-075
- Observed repair or delay minutes (measured): HF-011: 90.0 min (delay). Other measured: HF-075: 23.0 min (cycle-time, not a competition loss); HF-086: 13.1 min (cycle-time, not a competition loss).
- Estimated recurrence probability per build, cons / central / opt (modeled): FM-06 0.30 / 0.50 / 0.70
- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): 1.5 / 10.5 / 35.7
- Enforcement and implementation overhead on the critical path, cons / central / opt: 1.0 / 0.5 / 0.5
- Net critical-path minutes saved, standalone, cons / central / opt: 0.5 / 10.0 / 35.2; cumulative in rank order: 13.9 / 111.1 / 399.2
- Confidence: high. Key assumptions: Measured: about 90 min without a main result (R6) and 152 runs with 25 failed builds (R4). Conditor ci-policy already enforces it; value shown is what it secures.
- Enforcement mechanism: conditor ci-policy check; conditor ci-policy check --target .
- Implementable before the competition: yes (already enforced; keep it on)

### 7. CV-04 WASM boot smoke on the first CI build; bounded view functions

- Level: PREFERRED; tier 1; status PROPOSED
- Rehearsals where the problem occurred: R004, R005
- Evidence: HF-007, HF-013 (FAILURE-DATA.json); convention evidence HF-007, HF-013
- Observed repair or delay minutes (measured): HF-007: 120.0 min (delay); HF-013: 90.0 min (delay).
- Estimated recurrence probability per build, cons / central / opt (modeled): FM-04 0.15 / 0.30 / 0.50
- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): 1.8 / 10.8 / 48.0
- Enforcement and implementation overhead on the critical path, cons / central / opt: 3.0 / 2.0 / 1.0
- Net critical-path minutes saved, standalone, cons / central / opt: -1.2 / 8.8 / 47.0; cumulative in rank order: 12.7 / 119.9 / 446.2
- Confidence: medium. Key assumptions: Two consecutive rehearsals (R4, R5) lost 120 and 90 min to start failures; R6-R9 did not, so occurrence is discounted to 15-50%. The boot smoke shortens detection, it does not fix the cause.
- Enforcement mechanism: boot smoke test in tier-1 CI (plan's first-render journey); review obligation for view size; Reviewer confirms the first CI run executed the boot smoke and that no view function spans more than one screen region.
- Implementable before the competition: yes

### 8. CV-17 Forma first: current Forma, no containment workarounds

- Level: MANDATORY; tier 1; status IMPLEMENTED-NOT-ADOPTED
- Rehearsals where the problem occurred: Forma, R005, R006, R007, R008, R009, R010
- Evidence: HF-019, HF-056, HF-080, HF-081, HF-082, HF-083, HF-084, HF-085, HF-087, HF-020 (FAILURE-DATA.json); convention evidence HF-019, HF-056, HF-080, HF-081, HF-082, HF-084, HF-085, HF-087
- Observed repair or delay minutes (measured): none stated in any source (unknown).
- Estimated recurrence probability per build, cons / central / opt (modeled): FM-17 0.30 / 0.50 / 0.70; FM-19 0.20 / 0.30 / 0.40
- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): 1.6 / 8.7 / 34.2
- Enforcement and implementation overhead on the critical path, cons / central / opt: 2.0 / 1.0 / 0.5
- Net critical-path minutes saved, standalone, cons / central / opt: -0.5 / 7.7 / 33.7; cumulative in rank order: 12.3 / 127.6 / 479.9
- Confidence: medium. Key assumptions: Containment defects are measured (48/78 checks) and fixed upstream; minutes lost to UI rework were never recorded. Requires owner approval of the Forma 0.13.0 pin.
- Enforcement mechanism: conventions.fsx check CK-04 (Forma version) + design-contract check + layout-tests (existing); dotnet fsi tools/conventions/conventions.fsx -- check --workspace . --forma-min 0.13.0
- Implementable before the competition: yes, after owner approval (owner: pin Forma 0.13.0 (v4.1.5 references the 0.12.0 inventory; ODR-R10-05 allows 0.12.x/0.13.x))

### 9. CV-10 Typed errors through one state machine; no silent defaults

- Level: MANDATORY; tier 1; status IMPLEMENTED-NOT-ADOPTED; safety/correctness rule (enforced regardless of time value)
- Rehearsals where the problem occurred: R003, R004, R005, R006, R007, R008, R009, post-R10
- Evidence: HF-008, HF-022, HF-041, HF-002, HF-009, HF-017, HF-027, HF-045, HF-046, HF-052, HF-073 (FAILURE-DATA.json); convention evidence HF-008, HF-022, HF-041, HF-073
- Observed repair or delay minutes (measured): none stated in any source (unknown).
- Estimated recurrence probability per build, cons / central / opt (modeled): FM-10 0.20 / 0.35 / 0.50; FM-13 0.40 / 0.60 / 0.80
- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): 1.5 / 8.1 / 29.7
- Enforcement and implementation overhead on the critical path, cons / central / opt: 2.0 / 1.0 / 0.5
- Net critical-path minutes saved, standalone, cons / central / opt: -0.5 / 7.1 / 29.2; cumulative in rank order: 11.8 / 134.7 / 509.1
- Confidence: medium. Key assumptions: R4, R5, R8 evidence; CK-01 is cheap; wildcard-default detection is review-only so prevention is moderate.
- Enforcement mechanism: tools/conventions/conventions.fsx check CK-01 (warnings-as-errors) + state-machine tests (existing) + review obligation; dotnet fsi tools/conventions/conventions.fsx -- check --workspace .; Reviewer confirms no wildcard/default branch on command or event unions in decision code.
- Implementable before the competition: yes

### 10. CV-16 Only the owner records approvals

- Level: MANDATORY; tier 1; status ENFORCED-EXISTING; safety/correctness rule (enforced regardless of time value)
- Rehearsals where the problem occurred: R003, R004, R005, R006, R008, R009, post-R10
- Evidence: HF-003, HF-050, HF-077 (FAILURE-DATA.json); convention evidence HF-003, HF-050, HF-077
- Observed repair or delay minutes (measured): none stated in any source (unknown). Other measured: HF-077: 30.0 min (elapsed-pre-competition, not a competition loss).
- Estimated recurrence probability per build, cons / central / opt (modeled): FM-16 0.30 / 0.50 / 0.70
- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): 1.1 / 6.4 / 20.0
- Enforcement and implementation overhead on the critical path, cons / central / opt: 0.0 / 0.0 / 0.0
- Net critical-path minutes saved, standalone, cons / central / opt: 1.1 / 6.4 / 20.0; cumulative in rank order: 12.8 / 141.0 / 529.1
- Confidence: high. Key assumptions: Safety rule; recurred R3-R8; enforced by RDP-003 and owner-freeze.fsx.
- Enforcement mechanism: launch-preflight unsigned-change checks; owner-freeze.fsx; tools/r10/launch-preflight.fsx
- Implementable before the competition: yes (already enforced; keep it on)

<!-- GENERATED:top10detail END -->

## Mandatory rules enforced regardless of time value

These rules are correctness or safety rules. They stay mandatory even where their modeled net is small or negative. The ranking does not decide them.

| Convention | Why it is mandatory | Central standalone net (see IMPACT-FORECAST.md) |
|---|---|---|
| CV-09 Durable means read back from the real provider | Data loss: shows "saved" for data that is not saved | in the top 10 |
| CV-10 Typed errors through one state machine; no silent defaults | Silent wrong behaviour; persisted wrong command kinds (HF-041) | in the top 10 |
| CV-15 Evidence is bound to the candidate SHA | Acceptance integrity | rank 11 |
| CV-16 Only the owner records approvals | Governance integrity (RDP-003) | in the top 10 |
| CV-19 Test-only code never ships | Security: token in traces, test hooks in production | negative (−0.2): enforced regardless |
| CV-11 Contracts proven against real data before release | Compatibility: the frozen contract cannot load 487 of 1,315 real events | in the top 10 |

## What the top 10 does not claim

- **That adopting it saves these minutes in any given build.** Each figure is an expectation over the chance a failure recurs. In a build where nothing recurs, the conventions cost their overhead.
- **That ENFORCED-EXISTING conventions are new savings.** Their figures are what the existing check secures, as long as it stays on. The new value of this change is the "Incremental" line of the summary.
- **That EXP-INDY-002's effects are known.** They are modeled as overlap fractions on three failure modes, and will be replaced by its measurements.
