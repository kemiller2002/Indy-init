# Final report: rehearsal retrospective and implementation conventions

**Status:**
- The analysis is complete.
- The low-risk tooling is implemented and tested.
- **No convention is adopted.** Every adoption is the owner's decision.
- EXP-INDY-002 was not read, changed or depended on.

## 1. What was examined

There are 87 evidence-cited findings (HF-001..HF-087, [FAILURE-DATA.json](FAILURE-DATA.json)), drawn from:
- **Rehearsals 2–9:** hub documents, plus read-only clones of rehearsals 008 and 009. Rehearsal 007's repository could not be read, so its findings rest on the hub documents that quote it.
- **Rehearsal 010 readiness:** **R10 itself has not been run.** Its repository holds one README commit, and every R10 duration in the hub is modeled.
- **The post-R10 optimization and preview work.**
- **Forma's history.**

Existing Echelon capabilities were evaluated before anything was proposed:
- Conditor `ci-policy`, its scaffold and its `TreatWarningsAsErrors` default;
- Indy-init's own tools: `r9.fsx` (check-sequencing, suite-preflight, stop-decision), `launch-preflight.fsx`, `owner-freeze.fsx`, the slice and completion gates, `AssertionRatchet`, `ContractCheck` and the negative contracts;
- the contract-first UI proposal: `ManifestTests` and `OwnershipTests`;
- the historical-compatibility proposal (replay coverage);
- the v4.1.5 candidate (E1/W1);
- Forma 0.13.0 containment;
- the Praxis work-item model.

Where one of those already enforces a rule, the convention points to it rather than duplicating it.

## 2. Findings

1. **Measured critical-path loss is concentrated and mostly unrecorded.** Six findings state 652.9 minutes in total:
   - R9 stopped with 305 minutes unused;
   - R4 lost about 120 minutes and R5 lost 90 minutes to WASM start failures;
   - about 90 minutes passed without a CI result;
   - a duplicated port cost 27.9 minutes;
   - a candidate re-cut cost 20 minutes.

   The most recurrent families (persistence durability, tests that prove less than claimed, evidence not bound to the candidate) have no recorded minutes at all.
2. **Persistence durability is the most recurrent failure family.** It appears in seven rehearsals or experiments. Each fix closed one variant. No single rule defined "durable", and no rule made provider doubles carry real provider behaviour.
3. **Several fixes were made per occurrence instead of by default.**
   - Shell `pipefail` was fixed in R6, R7 and R8, yet CK-03 still finds 23 non-pipefail run steps in R8 and 39 in R9.
   - Forma containment was fixed upstream in 0.13.0, yet R9 and the preview app worked around it locally, and the frozen packages and the Conditor scaffold still reference 0.12.0.
4. **Generated artifacts shipped without being executed.** Suite commands (MSB1008), SDK setup and deploy preconditions failed first inside timed runs (R8, R9).
5. **Contracts were frozen without being checked against real data.** 487 of 1,315 causa-data events cannot be represented, and there is no workspace and no command body. The v4.1.5 candidate addresses the data gap; the rule that would have caught it before freeze does not exist yet.
6. **File granularity is not a measured problem.** Single ownership removed source conflicts between R5 and R8, and moved the cost into change requests and into conflicts over shared tool state. The data cannot settle whether smaller files would help; EXP-INDY-002 should measure it.
7. **Many proposed rules do not pay for themselves.**
   - A falsifying control for every bound test (CV-13) has negative modeled net at central.
   - A standard per-feature file split (CV-22) has no supporting evidence, and would change frozen structure.

   Both are deferred.

## 3. Top 10 time savings

Details, assumptions and evidence for each convention: [TOP-10-TIME-SAVINGS.md](TOP-10-TIME-SAVINGS.md). The figures are modeled expectations per seven-hour build, net of overhead and of overlap with EXP-INDY-002.

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

**How to read it:**
- **Secured value.** Half of the central value is what existing checks already secure: stop-decision, check-sequencing, suite-preflight, ci-policy, owner-only approvals and the completion gate. Keeping them on is the single most valuable decision.
- **New value.** The new value is the "Incremental" line. It is positive at central and optimistic, and slightly negative at conservative. These conventions are a bet that failure families seen in 2–7 earlier rehearsals recur.
- **No vital few.** About 11 conventions are needed to reach 80% of the central peak.

**Rules enforced regardless of time value:** CV-09, CV-10, CV-11, CV-15, CV-16 and CV-19.

## 4. Decision package

### Tier 1: adopt before the competition

| Convention | Exact enforcement mechanism | Status | Owner approval |
|---|---|---|---|
| CV-02 Stop only on STOP_PERMITTED; clock from T0 | `requirements-compiler stop-decision`, `--t0-record`/`--deadline-policy` | enforced (existing) | none; keep it on |
| CV-03 One scope source, itemised before code | `r9.fsx check-sequencing`; launch-preflight LP-04 | enforced (existing) | none |
| CV-01 Execute every generated command before freeze | `r9.fsx suite-preflight --require-tests`; extended to deploy preconditions by PRC-01 | enforced for suites; extension proposed | ODR-IC-01 |
| CV-11 Contracts proven against real data | replay coverage (historical-compatibility proposal) as a release condition; v4.1.5 E1/W1 | proposed | ODR-V415-01/-02; ODR-IC-01 |
| CV-09 Durable means read back | SM-SAVE tests; live preflight J3-05; recorded-header doubles; review obligation | partly enforced | ODR-IC-04 |
| CV-06 Workflows pass ci-policy | `conditor ci-policy check` | enforced (existing) | none |
| CV-04 WASM boot smoke on the first CI | first-CI boot smoke; review obligation for view size | proposed | ODR-IC-05 |
| CV-17 Forma first, Forma ≥ 0.13.0 | **CK-04** (new) + design contract + layout tests | check implemented, not adopted | **ODR-IC-02 (breaking pin)** |
| CV-10 Typed errors, one state machine | **CK-01** (new) + state-machine tests + review obligation | check implemented, not adopted | ODR-IC-03 |
| CV-16 Owner-only approvals | RDP-003; `owner-freeze.fsx`; launch-preflight unsigned-change checks | enforced (existing) | none |
| CV-15 Evidence bound to the candidate SHA | completion gate | enforced (existing) | none |
| CV-07 Workflow shell is pipefail | **CK-03** (new) | check implemented, not adopted | ODR-IC-03 |
| CV-05 One source for port literals | **CK-02** (new) | check implemented, not adopted | ODR-IC-03 |
| CV-14 Reconcile requirements with owner decisions | package review obligation (PRC-01 §3) | proposed | ODR-IC-01 |
| CV-21 Everything mandated is in the lock | package review obligation; a skipped test assembly fails | proposed | with ODR-IC-01 |
| CV-18 Editable fields bind on input | `ManifestTests` with a negative control (contract-first UI) | proposed | with CF-09 A |
| CV-19 Test-only code never ships (safety) | candidate review obligation | proposed | ODR-IC-06 |

### Tier 2: adopt if EXP-INDY-002 validates it

- **CV-08:** append-only shared tool state and change-request flow.
- **CV-12 and PA-04:** per-PR contract version, refused on mismatch.
- **CV-20:** the schedule and gate checks already exist; their marginal value depends on the topology EXP-INDY-002 uses.
- **CV-23:** stable IDs via `Ids.fs`.

The measurements needed are listed in [PARALLEL-AGENT-PATTERNS.md](PARALLEL-AGENT-PATTERNS.md).

### Tier 3: defer

- **CV-13:** a falsifying control for every bound test. Its modeled net is negative at central.
- **CV-22:** a standard per-feature file split. There is no evidence for it, and it would change frozen structure.

## 5. Implementation status (this change)

| Item | State |
|---|---|
| The 14 deliverables in this directory, plus [FORECAST.json](FORECAST.json) | written. The forecast blocks are generated. |
| `tools/conventions/conventions.fsx` (F#, functional, no dependencies) | implemented. `validate`, `forecast`, `render`, `check`, `test`. |
| Self-tests | **19 of 19 pass.** They cover workspace checks with negative controls, the overlap math, and catalog-mutation controls. |
| Catalog validation | **PASS.** All evidence resolves, duplicates carry no cost, MANDATORY conventions have enforcement and tests, and the generated blocks are current. |
| Retrospective runs of `check` | R8, R9, the preview app and the Conditor 0.10.18 scaffold; results are in HISTORICAL-FINDINGS.md |
| CI, protected checks, frozen packages, v4.1.5, Forma, Conditor, EXP-INDY-002 | **unchanged** |
| Merge | not merged. Draft PR only. |

Reproduce:

```bash
dotnet fsi tools/conventions/conventions.fsx -- test
```

```bash
dotnet fsi tools/conventions/conventions.fsx -- validate
```

```bash
dotnet fsi tools/conventions/conventions.fsx -- check --workspace <competition-repo>
```

## 6. Remaining decisions (owner)

1. **ODR-IC-01:** package release conditions (PRC-01).
2. **ODR-IC-02:** pin Forma 0.13.0 (PRC-02). This is breaking, and it also needs the Conditor preset to change.
3. **ODR-IC-03:** run `conventions.fsx check` at bootstrap (PRC-03).
4. **ODR-IC-04, ODR-IC-05, ODR-IC-06:** durability wording, WASM boot smoke, and the test-only code review (PRC-04..06).
5. **Whether to file the two upstream Forma proposals** (FORMA-PATTERNS.md): validation-state styling, and container-query reflow for three patterns.
6. **After EXP-INDY-002:** the Tier 2 adoption decisions.

## 7. Limits

- **Modeled, not measured.** Every forecast figure is a model output. Its inputs (probability, impact, prevention and overhead per scenario) are recorded per failure mode and per convention in CONVENTION-CATALOG.json, so a reviewer can challenge each one.
- **Historical minutes are a floor.** They are only those a source states. Most recurring failures cost unrecorded time.
- **R10 has no observed outcome.** No result here depends on one.
- **Shallow history.** The hub history is shallow (from 2026-11-02), so hub commit counts cover only that range.
