# Rehearsal 006 preflight

First run on 2026-10-28 against Indy-init `856157a`, echelon-current **1.32.0** and `kemiller2002/indy-init-rehearsal-006` at `65e346e`. Re-evaluated the same day after the owner decisions (DEC-R6-001..013) and the independent scope review.

Machine-readable results, with every stage's evidence, findings and blockers: [`preflight.json`](preflight.json).

**The timed rehearsal has not started.**

```
REHEARSAL 006 PREFLIGHT
Repository readiness:              PASS (CI, deployment and secrets are kickoff obligations)
Requirements baseline:             PASS (re-pinned: BL-CAUSA-INDY-INIT-856157a78d47)
Rehearsal 5 implementation audit:  PASS
Corrective actions:                PASS (every R5A-001 item implemented or decided)
Verification plan:                 PASS (v3, VP-8ef1c8dca656, READY 671/671, no permits)
Historical template:               PASS (historical-v1, used in place)
Lightbulb investigation:           NOT_RUN (waived by the owner; DEC-R6-007 keeps three cases)
Combined demonstration template:   NOT_RUN (waived by the owner)
Conditor integration:              NOT_RUN (waived by the owner)
Workspace initialization:          NOT_RUN (waived by the owner)
Workspace isolation:               PASS
Application compatibility:         PASS
Engineering toolchain:             PASS
WebAssembly diagnostics:           PASS
CI and deployment:                 NOT_RUN (kickoff obligation, by the owner's decision)
Acceptance infrastructure:         PASS
Baseline freeze:                   PASS (frozen 2026-10-28)
Independent scope review:          DEVIATION (performed, no blocking findings; not attested, DEC-R6-012)
Overall readiness:                 READY WITH DEVIATION (start authorized, DEC-R6-013)
```

## Owner decisions

Recorded in [`kickoff/rehearsal-006/OWNER-DECISIONS.md`](../../kickoff/rehearsal-006/OWNER-DECISIONS.md) and `owner-decisions.json`.

- **Provenance:** an agent transcribed them on the owner's instruction. The owner did not edit those files.
- **When they take effect:** when the pull request that adds them merges.

## How the last blockers were settled (owner, 2026-10-28)

- **DEC-R6-011, "Proceed as written":** no approval records are written, so the UI-031-O5 and AR-019-O3 amendments are not applied and both obligations stay as written.
  - The current verifier refuses Rehearsal 3 as INVALID or FAIL, not with the literal BLOCKED, so AR-019-O3 is expected not to verify.
- **DEC-R6-012, "Proceed unattested":** the independent scope review was performed and found no blocking issue, but it is not attested by the owner.
  - This is recorded as a deviation from the readiness rule, not a PASS.
- **DEC-R6-013, "Start the rehearsal":** the owner authorizes the timed rehearsal.

## Kickoff obligations

These count against the seven-hour window. Moving them to kickoff does not remove their risk.

- **CI and deployment** for `indy-init-rehearsal-006`:
  - R5's workflow set, re-pinned to 1.32.0 and Praxis 3.23.0;
  - `conditor.test-environment.json`;
  - `limen wasm-check`;
  - Pages with `DEPLOY_TARGET=github-pages`;
  - then a minimal-artifact deploy and deployed smoke.
- **Secrets, set by the owner before the clock** (AR-033, DEC-R6-010):
  - `CAUSA_GITHUB_TOKEN` (read-only);
  - `CAUSA_WRITE_TOKEN` (`contents:write` on the test repository only);
  - `CAUSA_TEST_REPO`.
- **R5A-001-01, -02, -03 and -06:**
  - the deployed-origin guard;
  - causa-data pinned in every job;
  - the SDK pin;
  - preflight and registry records as Praxis records.
- **At kickoff, also:**
  - the acceptance job passes the reviewer's `--review-tag-changes` file to `verify-plan`;
  - `INDY_INIT_COMMIT` is pinned to the merge commit;
  - S16 is bound NOT-APPLICABLE citing DEC-R6-006.

## Values to freeze once readiness passes

| What | Value |
|---|---|
| Baseline | `BL-CAUSA-INDY-INIT-856157a78d47`, sha256 `40599abe…befd` (`requirements/causa-requirements-baseline-r6.json`) |
| Plan | `VP-8ef1c8dca656` (UI-031-O5 and AR-019-O3 as written, DEC-R6-011) |
| Process rules | sha256 `6f164353…e17c` |
| Stack | echelon-current 1.32.0 |
| Rehearsal repository | initial `65e346e` |
| Data | `historical-v1` at causa-data `dcb8cb9` (DEC-R6-007) |
| Owner decisions | DEC-R6-001..013 |
| Acceptance job pin | the Indy-init merge commit of this pull request |
| Deployment target | GitHub Pages, configured at kickoff |

The earlier stage evidence, for stages 3 and 6–14 (unchanged), is in `preflight.json`.
