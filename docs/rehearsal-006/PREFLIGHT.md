# Rehearsal 006 preflight

Run on 2026-10-28 against Indy-init `856157a`, echelon-current **1.32.0** and `kemiller2002/indy-init-rehearsal-006` at
`65e346e`. Machine-readable results, with every stage's evidence, findings and blockers: [`preflight.json`](preflight.json).
**The timed rehearsal has not started.**

```
REHEARSAL 006 PREFLIGHT
Repository readiness:              BLOCKED
Requirements baseline:             PASS
Rehearsal 5 implementation audit:  PASS (its independent review is not independent; see stage 3)
Corrective actions:                FAIL
Verification plan:                 PASS
Historical template:               PASS (historical-v1, used in place)
Lightbulb investigation:           NOT_RUN (waived by the owner)
Combined demonstration template:   NOT_RUN (waived by the owner)
Conditor integration:              NOT_RUN (waived by the owner)
Workspace initialization:          NOT_RUN (waived by the owner)
Workspace isolation:               PASS
Application compatibility:         PASS
Engineering toolchain:             PASS
WebAssembly diagnostics:           PASS
CI and deployment:                 BLOCKED
Acceptance infrastructure:         PASS
Baseline freeze:                   BLOCKED
Overall readiness:                 BLOCKED
```

## Owner decisions recorded during preflight (2026-10-28)

1. The causa-data four-case requirement is waived. This covers the lightbulb investigation, `everyday-v1`,
   `demonstration-v1` and Conditor initialization of a `rehearsal-006` workspace.
2. Rehearsal 6 loads the existing published `historical-v1` workspace (Apollo 1, Apollo 11, Challenger). It writes only
   to isolated `workspaces/demo/<runId>/` paths.

## What blocks readiness

### Stage 1, repository readiness, and stage 15, CI and deployment

`indy-init-rehearsal-006` holds only the owner's `README.md`:

- It has no CI workflow and no deployment configuration. Its Pages URL returns 404.
- The secrets and variables Rehearsal 5 needed cannot be verified from this session: `CAUSA_GITHUB_TOKEN`,
  `CAUSA_TEST_REPO` and `DEPLOY_TARGET=github-pages`.
- This session has no push credential for the repository.

Because CI is absent, the minimal-artifact infrastructure smoke test could not run.

Needed:

- **Owner:**
  - Enable Pages with source "GitHub Actions".
  - Set `DEPLOY_TARGET=github-pages`.
  - Add `CAUSA_GITHUB_TOKEN` (a fine-grained token with contents write on the test repository only) and
    `CAUSA_TEST_REPO`, if the live jobs are kept.
  - Confirm whether CI and deployment are preflight prerequisites or kickoff deliverables.
- **Infrastructure only, no application code:**
  - Add Rehearsal 5's workflow set (`build-and-test.yml`, `praxis-validation.yml`, `echelon-foundations.yml`),
    re-pinned to 1.32.0 and Praxis 3.23.0.
  - Add a `conditor.test-environment.json` and a `wasm-check` step.
  - Then run the minimal-artifact deploy and deployed smoke test.

### Stage 4, corrective actions

| Item | Status |
|---|---|
| AR-027, -028, -029, -032, -033, -034, -035 | Partially implemented |
| AR-030 (production test-hook protection) | Not implemented |
| AR-031 (screen-contract scope decisions) | Not implemented |
| Persistence regression tests (cross-cutting theme) | Not implemented |

AR-027 to AR-035 are also **not in the frozen baseline**. They were added after `aab8f80`.

`input-documents/2026-10-28-R5A-001-rehearsal-5-audit-follow-ups.md` marks its items "P0 before Rehearsal 6 starts":

- **Owner decisions not recorded:**
  - R5A-001-08: DEC-025 conflicts with PF-015-O1/O2 and EXT-DATA-005-O1. Without a decision, those obligations are
    unsatisfiable in Rehearsal 6, as they were in Rehearsal 5.
  - R5A-001-10: amber indicators (UI-016-O11) and the UI-031-O5 role control.
  - R5A-001-11: the wording of AR-019-O3.
- **Not implemented:** R5A-001-01 to -05. For example, UI-015-O2 is still bound to a test directly.

## Stage results in brief

- **2, requirements baseline: PASS.**
  - Baseline `BL-CAUSA-INDY-INIT-aab8f80c7743` (sha256 `621e2f96…57b9`): 461 requirements, 108 P0, 326 P0
    obligations, 0 conflicts, no exclusions.
  - It compiles with V01–V18 PASS.
  - The offline and full suites pass: 24 cases, determinism and `--check`.
  - The compiler was last changed at `f27694d`.
- **3, Rehearsal 5 implementation audit: PASS.**
  - The audit covers exactly the 31 P0 requirements left NOT_RUN: 3 implemented but unverified, 7 partial,
    0 not implemented, 3 indeterminate and 18 with missing process evidence.
  - Its own validator passes 10 of 10. 11 spot-checks against Rehearsal 5's source and tests hold.
  - The audit concludes that Rehearsal 5's bottleneck was **verification and process evidence, not
    implementation**: 33 of the 49 unproven obligations trace to the plan or governance, and 5 to development.
  - Its "independent review" was written by an agent in the same session as the auditor, and PR #35 has no GitHub
    review. It is not independent.
- **5, verification plan: PASS.**
  - Plan `VP-0eb9c791dd0b`. `plan-readiness` is READY with 633 of 633 checks and 0 permits. A wrong digest gives
    BLOCKED.
  - The process simulation reports VERIFIED, with 47 log controls and 3 record controls. The R4 replay matches,
    7 of 7 cases.
  - The 276 application-test bindings name tests the rehearsal must write under exactly those identities.
  - PR #32 (merged, unreviewed) contributes no binding to v2.
  - Gap: `verify-plan` does not pin the plan digest for application-test bindings. Consumers must check that the
    reported plan id is `VP-0eb9c791dd0b`.
- **6, historical: PASS.**
  - `causa historical-verify` passes 53 of 53. A rebuild in scratch is byte-identical.
  - All 15 manifest digests match. The storage contract fixture verify passes 106 of 106.
- **11, isolation: PASS.**
  - The Rehearsal 5 provider writes only under `workspaces/demo/<ws>/investigations/<uuid>/` and refuses writes to
    the catalog workspace. Its tests pass, 27 of 27.
  - causa-data CI rejects branches that add a workspace, so Rehearsal 6 writes go to main through the application.
- **12, compatibility: PASS.** The Rehearsal 5 provider loaded all three investigations live from the published
  `historical-v1`, with 0 refusals. Each replays to its expected state digest.
- **13, toolchain: PASS.**
  - All 40 linux-x64 artifacts of the 1.32.0 set match their sha256, and each tool runs at its approved version.
  - F# builds and tests run, and Playwright/Chromium works.
  - The `wasm-tools` workload is not installed here. Rehearsal 6's CI installs it, as Rehearsal 5's did.
- **14, WebAssembly diagnostics: PASS.**
  - `limen wasm-check` 0.9.1 flags Rehearsal 5's oversized `View::project` (70,720 bytes) and clears its fix.
  - Rehearsal 5's head has `AnalysisView::extend` at 90% of the limit.
  - Rehearsal 5 already had startup diagnostics, version stamps and a route smoke test. Rehearsal 5's CI never ran
    `wasm-check`.
- **16, acceptance infrastructure: PASS.**
  - Missing evidence is rejected.
  - Invalid evidence cannot pass by re-binding: 24 compilation cases, 17 plan-tampering controls, 10 checker
    scenarios, 47 process and 3 record controls, and the Python verdict and ledger tests pass (19 and 11).
  - Untested: defect closure in the plan verifier, and an end-to-end run of the `acceptance-process.yml` template.

## Other findings

Three unmerged branches pushed today, with no PRs, prepare the waived workspace path. They are not needed under the
owner's waiver:

- causa-data `codex/immutable-historical-rehearsal-workspaces`: CI green, adds `templates/historical-v1` and workspace
  commands;
- causa-data `codex/causa-workspace-e2e-20261028`: CI red;
- conditor `codex/causa-rehearsal-preflight`: adds `conditor rehearsal init`.

## Values to freeze once readiness passes

- **Baseline:** `BL-CAUSA-INDY-INIT-aab8f80c7743`.
- **Plan:** `VP-0eb9c791dd0b`.
- **Process rules:** sha256 `6f164353…e17c`.
- **Stack:** echelon-current 1.32.0.
- **Rehearsal repository:** initial `65e346e`.
- **Data:** workspace `historical-v1` at causa-data `dcb8cb9`, catalog digest `8c3619…`.
- **Acceptance job pin:** Indy-init `856157a`.
- **Deployment target:** GitHub Pages, once configured.
