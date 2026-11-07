# Competition launch checklist (7 November 2026)

**Use this checklist in order.** It is Rehearsal 010's [LAUNCH-CHECKLIST.md](../rehearsal-010-readiness/LAUNCH-CHECKLIST.md) for the competition.
- Nothing here starts the clock. T0 is the builder's bootstrap commit (§D).
- Each box needs its evidence before the next section begins.
- Section C's preflight decides; this list only orders the work.
- While any box is unchecked, the verdict is READY FOR OWNER ACTION or BLOCKED, never READY TO LAUNCH.

## A. Engineering

- [ ] CI is green on main after the competition pull requests (#186, and this one) merge:
  - `offline-contract-tests`, `offline-factory`, `frozen-v4` (v4.1.6 equals its tag), `fsharp-tooling`, `release-gate-repair`, `r9-readiness-gates`, `suite-commands` (WebKit);
  - `r10-adversarial`: the cases of `tools/r10/adversarial.fsx`; the Rehearsal 009 replay; the launch-preflight controls (Rehearsal 010's and the competition's); the schedule evidence.
- [ ] The process fixes the owner approved (ODR-COMP-P01 A: PR #174's fixes 1, 2, 5, 6, 7, 8 and 10 that the independent review approves; P-4, P-5, P-6, P-9) are merged, so the lock is pinned to a main that has them (ODR-COMP-P12 A).
- [ ] Frozen packages unchanged (CI `frozen-v4` and the offline suite).

## B. The owner transaction

- [ ] 0. Your git identity is `kemiller2002@gmail.com`. You merge owner-freeze pull requests yourself, with **Create a merge commit**. You decided not to sign commits on 2026-11-04 (the accepted risk).
- [x] 1. v4.1.6 decided (APR-V416-OWNER-1), approved (APR-V416-OWNER-2, manifest `8d3706d9…`), tagged `indy-init-requirements-v4.1.6` on `2380b3d`; catalog ratified (ODR-V416-CAT).
- [x] 2. Competition decisions ODR-COMP-P01..P14 recorded (APR-COMP-OWNER-1).
- [x] 3. Conditor v0.10.21 released (commit `528ef45`; its preset carries forma-workflow 1.2.0 and the deploy secrets step); causa-storage-v1.1.0 tagged.
- [ ] 4. Merge #186 (scope, proposed lock, catalog contract, requests) and this pull request.
- [ ] 5. `owner-freeze.fsx -- decide competition`: **ODR-COMP-SCOPE** (scopeSha256 `09e38f7d…`) and **ODR-COMP-CATALOG-CONTRACT**.
- [ ] 6. The repository `kemiller2002/causa` (pre-created; one README commit `7afb157`, ODR-COMP-P07 A) has its secrets: `CAUSA_ACCEPTANCE_APP_CLIENT_ID`, `CAUSA_ACCEPTANCE_APP_KEY`; `CAUSA_GITHUB_TOKEN` (read-only) and `CAUSA_WRITE_TOKEN` (write) (ODR-COMP-P06 A); `CAUSA_BRANCH`, `DATA_ACCESS_KEY_CAUSA`, `DATA_SOURCE`, `REPO_OWNER` (ODR-COMP-P13 A); `DEPLOY_TARGET=github-pages`, Pages source "GitHub Actions". No other commit is pushed to it.
- [ ] 7. Live-provider preflight run on the merged main, dispatched with `lock=kickoff/competition/toolchain.lock.json` (after step 8, so the lock exists on main): J1–J3 × Chromium and WebKit pass against `api.github.com`.
- [ ] 8. `owner-freeze.fsx -- decide competition`: **ODR-COMP-LOCK**; then **the owner commits `kickoff/competition/toolchain.lock.json`**: the proposal with `status` set to the owner's pin and `indyInit.commit` filled, nothing else changed (launch-preflight LP-11 checks this).
- [ ] 9. A dependency lock that names Conditor 0.10.21. Rehearsal 010's `docs/optimization/echelon-stack-upgrade/lock/dependency-lock.json` names 0.10.20, so the bootstrap's stack-drift preflight (ODR-ESU-04 A) reports `DRIFT conditor` against the competition lock and is NOT READY. The owner adopts a successor dependency lock (generated with `stack-drift.fsx lock`) or decides otherwise, before T0.
- [ ] 10. Observer scheduled (STARTUP §9).

## C. Launch preflight (owner side; no clock)

- [ ] This prints **READY**:
  ```
  dotnet fsi tools/r10/launch-preflight.fsx -- --run competition --indy-init Indy-init \
    --live-run <live-provider preflight run id> --adversarial-run <Preflight tooling run id of main>
  ```
  (`--competition` defaults to the lock's `competitionRepository.url`.) LP-00..11 cover:
  - a clean Indy-init checkout equal to `origin/main`;
  - the lock, committed by the owner;
  - v4.1.6 and its tag;
  - every contract digest, the v4.1.6 plan supplement included;
  - the scope digest;
  - every owner decision (v4.1.6, ODR-COMP-P01..P14, -SCOPE, -CATALOG-CONTRACT, -LOCK, and the carried ODR-R10-05, -06, -07, -09, -14) and the digests it approved;
  - Dokimos 0.11.1;
  - the isolated test repository, not the competition repository;
  - the live evidence, run with the competition lock;
  - the adversarial suite;
  - `kemiller2002/causa` holding only the owner's initial commit (no T0);
  - the lock being the approved proposal, and Conditor's digests those of the v0.10.21 release.

  `--offline true` gives the structural form (no network; never READY) for a check before the owner steps are done.
- [ ] `r9.fsx spec-preflight --lock kickoff/competition/toolchain.lock.json` PASS.
- [ ] `conditor acceptance preflight` reads READY.
- [ ] `requirements-compiler scope preflight --source . --package requirements-compiled/v4.1.6 --manifest docs/competition-readiness/scope-manifest.json` PASS.
- [ ] These reproduce the committed run files byte for byte:
  ```
  requirements-compiler slice-manifest --manifest docs/competition-readiness/scope-manifest.json \
    --dag requirements-compiled/v4.1.6/execution-dag.json --journeys verification/journeys/bindings.json \
    --work-plan <tmp>/praxis-work-inventory.json --out <tmp>/slice-execution-manifest.json
  dotnet fsi tools/r10/schedule.fsx -- --slices docs/competition-readiness/slice-execution-manifest.json --window 420 --setup 45 \
    --ci 0,5,10,15,20,25 --merge 3 --lanes "integration:integration,domain:domain,persistence:persistence,ui-1:ui,ui-2:ui,reviewer:reviewer" \
    --out <tmp>/topology-a.json
  ```
  (and the three-UI variant with `--lanes "integration:integration,core:domain|persistence,ui-1:ui,ui-2:ui,ui-3:ui,reviewer:reviewer"`).

## D. Launch (starts the clock)

- [ ] The builder receives exactly the §0 prompt of [STARTUP-INSTRUCTIONS.md](STARTUP-INSTRUCTIONS.md), with SRC = the lock's `indyInit.commit`.
- [ ] The builder's first commit is the T0 bootstrap with `evidence/t0/t0.json`, published on top of `7afb157` by fast-forward (STARTUP §2 step 4); the owner then protects `main`.
