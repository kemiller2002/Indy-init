# Rehearsal 010 launch checklist

**Use this checklist in order.**
- Nothing here starts the rehearsal clock; T0 is the bootstrap of §D.
- Each box needs its evidence before the next section begins.
- An unchecked box means the state is BLOCKED or READY FOR OWNER APPROVAL, never READY TO LAUNCH.

## A. Engineering (done on #104; re-run on its final commit)

- [ ] CI on the #104 head is green:
  - `offline-contract-tests`
  - `frozen-v4`
  - `fsharp-tooling`
  - `release-gate-repair`
  - `r9-readiness-gates`
  - `suite-commands` (WebKit runs here)
  - `r10-adversarial` (Chromium and WebKit, the 18 cases, the journeys and design contract on the fixture, and the Rehearsal 009 replay)
- [ ] Independent review: no open blocking finding (FINAL-READINESS §5).
- [ ] Frozen packages unchanged (CI `frozen-v4` and the offline suite).

## B. Owner decisions and releases (OWNER-DECISIONS-REQUIRED.md)

- [ ] ODR-V414-01, -02: `dotnet fsi tools/owner-freeze.fsx -- decide v4.1.4`.
- [ ] Agent step 2 of `requirements-compiled/v4.1.4/FREEZE.md`:
  - write the approval ids into `analysis/amendment.json`;
  - recompile (X01–X21 PASS);
  - run percepta spec-preflight;
  - record the package's independent review;
  - add the CI steps.
- [ ] ODR-V414-03: `owner-freeze.fsx approve v4.1.4`, then `owner-freeze.fsx tag v4.1.4 --commit <freeze commit>`. An agent then adds v4.1.4 to the frozen guard.
- [ ] ODR-RG-03:
  - merge kemiller2002/dokimos#69;
  - tag and publish `dokimos-v0.11.1`;
  - register a new echelon-current set carrying it.
- [ ] ODR-R10-01: approvals record `{action: approve-scope, scope_sha256: b407b5cc5f200e3651088440c0db630f18e84357c933020970f236f18f9352d6}`.
- [ ] ODR-R10-03..09: decide and review each, then pin its digest in the lock:
  - the catalog contract;
  - the governed fact;
  - the design contract;
  - the deadline policy;
  - the journey bindings;
  - the loading-screen priority;
  - the test repository.
- [ ] ODR-R10-10..12: the R8 conflicts, recorded (cut order, UI-026, OD-INDY-22 clarification). Any normative text change goes through the successor process before the lock.
- [ ] ODR-R10-13: compute budget stated (proposed: no limit; the full window is expected).
- [ ] ODR-R10-14: process-evidence rules for the Rehearsal 010 repository, pinned.
- [ ] ODR-RG-05: topology chosen.
- [ ] ODR-R10-16:
  - #104 merged;
  - #101 and #102 closed as superseded;
  - the lock's governing Indy-init commit is the merge commit.
- [ ] ODR-R10-17: kemiller2002/specula#11 merged, and an observer repository created.
- [ ] **ODR-RG-04: the owner commits `kickoff/rehearsal-010/toolchain.lock.json`.** It is the proposed lock with:
  - the v4.1.4 tag commit;
  - the new echelon-current resolved set;
  - every contract digest;
  - the test repository.

## C. Pre-launch preflight (owner side, before T0; no clock)

- [ ] `r9.fsx spec-preflight --lock kickoff/rehearsal-010/toolchain.lock.json` PASS.
- [ ] `conditor acceptance preflight` reads READY.
- [ ] `requirements-compiler v41 --package requirements-compiled/v4.1.4 --check` PASS.
- [ ] `requirements-compiler slice-manifest … --work-plan …` reproduces `slice-execution-manifest.json` and `praxis-work-inventory.json` byte for byte.
- [ ] `requirements-compiler scope preflight --source . --package requirements-compiled/v4.1.4 --manifest docs/rehearsal-010-readiness/scope-manifest.json` PASS.
- [ ] Empty competition repository created.
  - Its `causa-acceptance` branch exists for the live journeys (ODR-R10-09).
  - Repository settings let the Verification gate's `github.token` write to that branch.
- [ ] Observer scheduled (STARTUP-INSTRUCTIONS §9). Specula is pointed at the observer repository.

## D. Launch (starts the clock)

- [ ] The builder receives exactly the §0 prompt of STARTUP-INSTRUCTIONS.md, with SRC = the lock's governing Indy-init commit.
- [ ] The builder's first commit is the T0 bootstrap with `evidence/t0/t0.json`.

**The exact launch command** is in FINAL-READINESS.md §9. It is run only after A–C are checked.
