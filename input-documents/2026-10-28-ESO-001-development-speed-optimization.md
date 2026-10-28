# ESO-001: Echelon development speed optimization (Rehearsal 5 → Rehearsal 6)

Priority: P0
Owner system: Praxis (intake, requirements). Decisions belong to the operator (Ordo). Verification: Indy-init tooling and Dokimos.
Target repositories: indy-init (this intake, acceptance tooling, Rehearsal 6 preparation), praxis, dokimos, ordo, limen, conditor; forma, percepta and causa-write-test were investigated only.
Intake: Praxis document ingestion. This document is source material, not a canonical requirement.

Indy-init has no Praxis installation (no `.praxis/`). As with RCP-001 and EII-C, this file is in the legacy
`input-documents/` path, which Praxis still reads (praxis `docs/fallback-reconciliation.md`, "Input documents"). No
Praxis state is written or implied by this file. Owner-side intake documents for the same initiative are in each
target repository's own Praxis inbox (praxis, limen, conditor, dokimos, ordo).

## Evidence

Rehearsal 5 (`kemiller2002/indy-init-rehearsal-005`, read only) ended **INCOMPLETE, NOT ACCEPTED** after 392 minutes
(final candidate `1b9fb56873fe7a98dffd5172066d909449c17eae`, CI run 40620099844): 108 P0 requirements, 77 PASS,
31 NOT_RUN, 0 FAIL; 326 obligations, 277 PASS, 49 NOT_RUN; gates G0–G7 PASS, G8–G9 NOT_RUN. About 35 of the 49 needed
evidence about how the rehearsal was run. Its plan began implementation with no test identity; the curated plan
(Indy-init PR #32, VP-5d1202ddec1e) was merged after the run and does not change its pinned plan or verdict. Measurements and findings:
`docs/engineering-improvement/SPEED-OPTIMIZATION-REPORT.md`.

## Requirements (implemented; merged on 2026-10-28)

| ID | Requirement |
|---|---|
| ESO-P1-01 | Process evidence (assignment, execution start, dependency decisions, scope changes, checkpoints, owner approvals, independent reviews, release selection, test results, acceptance references, final disposition) is recorded in the rehearsal's existing Praxis event log as the work happens. |
| ESO-P1-02 | Every record keeps one provenance class: tool-observed, agent-assertion, reviewer-finding, owner-decision; only a verifier computes verified-acceptance. An agent's statement never becomes independent evidence because it was written. |
| ESO-P1-03 | Owner decisions count only with an attestation observed by CI in the trusted run; transcribed decisions are agent assertions. No historical approval is manufactured. |
| ESO-P1-04 | A per-candidate verifier rejects, each with its own diagnostic and negative test: missing approval, incorrect agent identity, missing checkpoint, unapproved scope change, self-acceptance, stale review, incorrect commit identity, missing release selection, evidence reused from another candidate. |
| ESO-P2-01 | Every plan obligation states the evidence class (application-test, reviewer, process, owner) and producer of each required category; process and owner categories are never satisfied by an application test. |
| ESO-P2-02 | Implementation cannot start while any mandatory obligation has an unresolved mapping, unless the rehearsal's frozen readiness rules permit that exact gap with a reason and a decision; a permitted gap stays unverified. |
| ESO-P2-03 | The Rehearsal 5 binding problem is reproduced through the gate without changing Rehearsal 5's verdict. |
| ESO-P3-01 | Integration uses the existing integration contract (`praxis streams check`): merge the target into a stream only when the check reports overlap or a dependency stream advanced; integrate in its dependency order. |
| ESO-P4-01 | Evidence reuse refuses every case the initiative lists (changed source, test contract, dependency, environment, live freshness, security policy, independent acceptance) and never at the final release checkpoint. |
| ESO-P5-01 | The WASM interpreter frame check runs after the Release build and before any browser test. |
| ESO-P6-01 | Browser test configuration comes from one environment contract (`conditor test-env resolve`), with preflight before browser tests. |

## Rehearsal 6 preparation requirements

The owner approved these for Rehearsal 6's frozen kickoff on 2026-10-28, in the initiative's session ("I approve
anything necessary"). That statement was transcribed by an agent, so under RDP-003 the approval takes effect through the
owner's own merge of the pull request that records it. Rehearsal 6 is not started by this document.

| ID | Requirement | Source |
|---|---|---|
| ESO-R6-01 | Pin a stack release that contains the initiative's merged changes (Praxis with `work evidence`; Indy-init compiler with `plan-readiness` and `verify-process`; Limen `wasm-check`; Conditor `test-env resolve` and preflight; Dokimos failure-aware reuse). Rehearsal 5's echelon-current 1.29.0 contains none of them. | report §Adoption |
| ESO-R6-02 | Before implementation, the owner approves test and producer bindings for every mandatory obligation (process and owner categories are bound in `curation/verification-plan.json` from the approved proposal), and `plan-readiness` is READY against the frozen plan and frozen readiness rules. | ESO-P2-02 |
| ESO-R6-03 | Freeze the process-evidence rules (`indy-init.process-evidence-rules/1`: owner logins and Praxis actor ids, agent and automation identities, review roles) at kickoff. | ESO-P1 |
| ESO-R6-04 | Agents record process evidence with `praxis work evidence` at the moment of each event; every implementing work item has a verified checkpoint; reviewers work under their own identity in a `review` execution role. | ESO-P1-01 |
| ESO-R6-05 | The acceptance job fetches the owner's approving reviews from the GitHub API into `indy-init.owner-attestations/1` for its own run, runs `verify-process`, and passes the assessment to `verify-plan --process-assessment`. | ESO-P1-03 |
| ESO-R6-06 | Ingest provider token and tool-use telemetry into the Praxis execution record (`praxis telemetry ingest`), one session id per agent. Without it no efficiency claim is possible. | report §Metrics |
| ESO-R6-07 | Declare stream ownership and shared interfaces (the R5 conflict hotspots `Adapter.fs`, `View.fs`, `EnginePort.fs`, browser specs) in the integration contract; run `praxis streams check` before every merge; merge main into a stream only on reported overlap or an advanced dependency. | `investigations/r5-merge-necessity.md` |
| ESO-R6-08 | Build order in CI: Release build → `limen wasm-check` → `conditor test-env preflight` → browser tests; Playwright reads its endpoint from `conditor test-env resolve`. | ESO-P5-01, ESO-P6-01 |
| ESO-R6-09 | Persistence: decide between adopting Arca 0.6.0 and carrying Rehearsal 5's persistence fixes forward; either way make the local read-merge-write exclusive across tabs (Web Locks where available, with an explicit fallback) and add the deterministic two-tab reproduction test. R3-F2 is open. | `investigations/regression-preservation.md` |
| ESO-R6-10 | UI: consume a released Forma with the application type roles (0.11.0 is merged but not published); require overflow, corner-containment and axe checks in the application. | `investigations/regression-preservation.md` |
| ESO-R6-11 | Record the measurements of the report's comparison table during the run (time to first working application, time to verified completion, CI minutes from the Actions API, tokens, integration repairs, mandatory requirements verified, defects escaping early tests). | report §Comparison |

## Status (2026-10-28)

| ID | Status |
|---|---|
| ESO-R6-01 | Done. echelon-current 1.32.0 (echelon-registry#103, approved by the owner on 2026-10-28): Praxis 3.23.0, Ordo 1.19.0, Dokimos 0.9.0, Limen 0.9.1 (1.31.0, echelon-registry#102) plus Forma 0.11.0 and Arca 0.6.0, qualified by Conditor 0.10.17. Chrona stays on 1.31.0 until it migrates to Arca 0.6.0's API (owner decision). |
| ESO-R6-02 | Prepared. Every category of the 326 obligations is bound (plan `VP-0eb9c791dd0b`); `plan-readiness` is READY. Takes effect on the owner's merge. |
| ESO-R6-03 | Prepared. `kickoff/rehearsal-006/process-evidence-rules.json` (SHA-256 `6f164353…e17c`). The reviewer is `anthropic/claude-code-reviewer`, attested by the owner's GitHub login (owner decision, 2026-10-28). |
| ESO-R6-04, -06 | The commands are released (`praxis work evidence`, `praxis telemetry ingest`); the agents use them during the rehearsal. |
| ESO-R6-05 | Prepared. `kickoff/rehearsal-006/acceptance-process.yml` and `requirements-compiler review-attestations`. The job runs `verify-process`; `verify-plan` computes the same assessment itself from its `--process-*` inputs rather than reading a `--process-assessment` file. |
| ESO-R6-07, -08, -11 | At kickoff, in the rehearsal repository. |
| ESO-R6-09 | Decided by the owner on 2026-10-28: **adopt Arca 0.6.0** (released at v0.6.0, d0ce9dae), in echelon-current 1.32.0 (echelon-registry#103). Arca 0.6.0 changes its license from MIT to PolyForm-Noncommercial-1.0.0, which the owner accepted. Arca's single-owner Web Lock queue closes R3-F2's cross-tab read-merge-write for data that goes through it. Two Rehearsal 5 protections Arca 0.6.0 does not provide stay requirements of the application: confirm a save only by a byte read-back of the stored record (causa contract §9.4.3), and make idempotency keys globally unique per page load (an F-1-style collision would be reconciled as landed). Keep the deterministic two-tab reproduction test (`investigations/regression-preservation.md`). |
| ESO-R6-10 | Decided by the owner on 2026-10-28: **adopt Forma 0.11.0** (typography roles, forma-conformance corner helpers). Its release run failed on a stale fixture pin; forma#172 fixed it; 0.11.0 is released (v0.11.0, 7a545813) and in echelon-current 1.32.0. |
