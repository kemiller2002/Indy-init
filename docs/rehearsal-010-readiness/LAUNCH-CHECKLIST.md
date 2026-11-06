# Rehearsal 010 launch checklist

**Use this checklist in order.**
- Nothing here starts the rehearsal clock. T0 is the builder's bootstrap commit (§D).
- Each box needs its evidence before the next section begins.
- Section C's preflight decides; this list only orders the work.
- While any box is unchecked, the verdict is READY FOR OWNER ACTION or BLOCKED, never READY TO LAUNCH.

## A. Engineering (#104; re-run on its final commit)

- [ ] CI on the #104 head is green:
  - `offline-contract-tests`
  - `offline-factory`
  - `frozen-v4`
  - `fsharp-tooling`
  - `release-gate-repair`
  - `r9-readiness-gates`
  - `suite-commands` (WebKit)
  - `r10-adversarial`: Chromium and WebKit; the 36 cases; the fixture journeys and design contract; the Rehearsal 009 replay; the 49 launch-preflight controls; the schedule evidence
- [ ] Independent review: no open blocking finding (FINAL-READINESS §5).
- [ ] Frozen packages unchanged (CI `frozen-v4` and the offline suite).

## B. The owner transaction (OWNER-APPROVAL-EXECUTION.md §3, steps 0–14)

- [ ] 0. Your git identity is `kemiller2002@gmail.com`. You merge owner-freeze pull requests yourself, with **Create a merge commit**. You decided not to sign commits on 2026-11-04 (OWNER-APPROVAL-EXECUTION step 0: the accepted risk).

- [ ] 1. #104 merged; #101 closed as superseded.
- [ ] 2–5. v4.1.4:
  - decided (ODR-V414-01, -02);
  - recompiled and reviewed;
  - approved (ODR-V414-03);
  - tagged;
  - added to the frozen guard.
- [x] 5a. v4.1.5 (superseded v4.1.4 for Rehearsal 010), done on 2026-11-06:
  - decided (APR-V415-OWNER-1, -2) and approved (APR-V415-OWNER-3);
  - tagged `indy-init-requirements-v4.1.5` on b2b3022;
  - added to the frozen guard (#153);
  - the lock and scopes re-pinned (#155), and ODR-R10-01 and ODR-R10-19 re-asked (#156).
- [ ] 5b. Re-record ODR-R10-01 and ODR-R10-19 for the v4.1.5 scope digests (`owner-freeze.fsx decide rehearsal-010`).
- [ ] 6. Dokimos 0.11.1:
  - merged, tagged and published;
  - artifacts verified;
  - a new echelon-current set registered and Conditor-qualified.
- [ ] 7. Test repository `kemiller2002/causa-acceptance-r10`, its App and secrets; the empty competition repository `kemiller2002/indy-init-rehearsal-010`.
- [ ] 8–11. `decide rehearsal-010` (all requests), then `ratify rehearsal-010`, then the scope recompile, then ODR-R10-01.
- [ ] 12. Live-provider preflight run: J1–J3 × Chromium and WebKit pass against `api.github.com`.
- [ ] 13. **The owner commits `kickoff/rehearsal-010/toolchain.lock.json`** (ODR-RG-04).
- [ ] 14. Specula #11 merged; observer scheduled.

## C. Launch preflight (owner side; no clock)

- [ ] This prints **READY**:
  ```
  dotnet fsi tools/r10/launch-preflight.fsx -- --indy-init Indy-init --live-run <live-provider preflight run id> --adversarial-run <Preflight tooling run id of main> --competition https://github.com/kemiller2002/indy-init-rehearsal-010
  ```
  LP-00..10 cover:
  - a clean Indy-init checkout equal to `origin/main`;
  - the lock;
  - v4.1.5 and its tag (`indy-init-requirements-v4.1.5`);
  - every contract digest;
  - the scope digest;
  - every owner decision and the digests it approved;
  - Dokimos 0.11.1;
  - the isolated test repository;
  - the live evidence;
  - the adversarial suite;
  - a clean competition repository (no T0).
- [ ] `r9.fsx spec-preflight --lock kickoff/rehearsal-010/toolchain.lock.json` PASS.
- [ ] `conditor acceptance preflight` reads READY.
- [ ] `requirements-compiler scope preflight --source . --package requirements-compiled/v4.1.5 --manifest docs/rehearsal-010-readiness/scope-manifest.json` PASS.
- [ ] `requirements-compiler slice-manifest … --work-plan …` reproduces `slice-execution-manifest.json` and `praxis-work-inventory.json` byte for byte.

## D. Launch (starts the clock)

- [ ] The builder receives exactly the §0 prompt of STARTUP-INSTRUCTIONS.md, with SRC = the lock's governing Indy-init commit.
- [ ] The builder's first commit is the T0 bootstrap with `evidence/t0/t0.json`.

**The exact launch command** is in FINAL-READINESS.md §9. Run it only after A–C are checked.
