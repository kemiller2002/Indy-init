# Rehearsal 006 preflight

First run on 2026-10-28 against Indy-init `856157a`, echelon-current **1.32.0** and `kemiller2002/indy-init-rehearsal-006` at `65e346e`. Re-evaluated the same day after the owner decisions (DEC-R6-001..010) and the independent scope review.

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
Baseline freeze:                   BLOCKED
Independent scope review:          BLOCKED (performed, no blocking findings; not attested)
Overall readiness:                 BLOCKED
```

## Owner decisions

Recorded in [`kickoff/rehearsal-006/OWNER-DECISIONS.md`](../../kickoff/rehearsal-006/OWNER-DECISIONS.md) and `owner-decisions.json`.

- **Provenance:** an agent transcribed them on the owner's instruction. The owner did not edit those files.
- **When they take effect:** when the pull request that adds them merges.

## What still blocks readiness

1. **Operator approval records for DEC-R6-003 and DEC-R6-004.** The owner decided to amend UI-031-O5 (no role model in a single-user application) and AR-019-O3 (any authoritative non-accepting verdict). Under RDP-003 only the operator may write the approval records that apply an amendment.
   - An agent-written file did exist, but it violated RDP-003 (review finding SR-01) and has been withdrawn.
   - Until the owner writes the records, the plan verifies both obligations as written. The current verifier cannot meet AR-019-O3's literal "BLOCKED".
   - Once the records exist, the plan is recompiled and the kickoff pins move.
2. **The owner's attestation of the independent scope review.**
   - The reviewer was a fresh-context agent that did not author the work. It found no blocking issue: 4 major and 11 minor findings, each dispositioned in `preflight.json` stage 18.
   - It counts as the independent review only when `kemiller2002` attests it with a GitHub review of the pull request.

Stage 17, the freeze, waits on both.

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
| Plan | `VP-8ef1c8dca656`. It changes once the operator approval records are applied |
| Process rules | sha256 `6f164353…e17c` |
| Stack | echelon-current 1.32.0 |
| Rehearsal repository | initial `65e346e` |
| Data | `historical-v1` at causa-data `dcb8cb9` (DEC-R6-007) |
| Owner decisions | DEC-R6-001..010 |
| Acceptance job pin | the Indy-init merge commit of this pull request |
| Deployment target | GitHub Pages, configured at kickoff |

The earlier stage evidence, for stages 3 and 6–14 (unchanged), is in `preflight.json`.
