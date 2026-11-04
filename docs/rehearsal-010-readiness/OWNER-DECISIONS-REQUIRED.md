# Rehearsal 010: owner decisions required

> **How these are recorded now.**
> - The decisions below are machine-readable requests in [`kickoff/rehearsal-010/proposals/owner-decision-requests.json`](../../kickoff/rehearsal-010/proposals/owner-decision-requests.json).
> - You record them with `tools/owner-freeze.fsx decide rehearsal-010`, and ratify the catalog with `ratify rehearsal-010`.
> - The package decisions use `decide`, `approve` and `tag v4.1.4`.
> - The order, the commands and the checks are in [OWNER-APPROVAL-EXECUTION.md](OWNER-APPROVAL-EXECUTION.md).
>
> **Changed since this list was written:**
> - **ODR-R10-01:** the scope is 199/863/36 now, or 205/890/37 with the successor. Its digest is final only after v4.1.4 is frozen and ODR-R10-02 is applied.
> - **ODR-R10-09:** an isolated test repository with a single-repository App token.
> - **ODR-R10-16:** #102 is merged; only #101 remains to close.
> - **ODR-R10-18 is new:** the full scope does not fit 420 minutes.

**Agents never write approval records (RDP-003).** Every item below is a request.
- The owner records each decision with the existing mechanisms:
  - `tools/owner-freeze.fsx` for packages;
  - the pinned approvals file, `kickoff/acceptance/approvals.causa.json`, for scope, stop and exclusion records;
  - an owner commit for the lock.
- Until an item is recorded, the step it gates stays blocked, and FINAL-READINESS reports it as outstanding.

**Notation.** "Proposed" is the agent's recommendation. The option letters follow the source request where one exists.

## A. Packages and toolchain (carried from #101; still required)

| ID | Decision | Proposed | How the owner records it | Blocks |
|---|---|---|---|---|
| ODR-V414-01 | SU-OWN-DOTNET and SU-DOTNET-LIVE run as one `dotnet test Causa.slnx --filter …` | A | `dotnet fsi tools/owner-freeze.fsx -- decide v4.1.4` | package freeze |
| ODR-V414-02 | The bootstrap writes `playwright.config.ts` defining every project the plan names (chromium, webkit) | A | same | package freeze |
| ODR-V414-03 (= ODR-RG-02) | Approve compiled package v4.1.4, after its independent review | A | `owner-freeze.fsx approve v4.1.4`, then `tag v4.1.4` | the lock |
| ODR-RG-03 | Release Dokimos 0.11.1 (url-input, dokimos#67) and register a new echelon-current set with it | A. The release PR is prepared (FINAL-READINESS §6) | merge the Dokimos release PR, then tag, publish and register in `kemiller2002/echelon-registry` | the lock's resolved set |
| ODR-RG-04 | Adopt the Rehearsal 010 lock: `kickoff/rehearsal-010/toolchain.lock.proposed.json` → `toolchain.lock.json` | A, after V414-03, RG-03 and R10-01..09 | owner commit; replace `resolvedSet` with the new set | launch |
| ODR-RG-05 | Topology for the build: C (five lanes and a reviewer, as scheduled), B (one lead), or another named one | **A.** The 27 estimated slices sum to 764 serial minutes against 420 (SLICE-EXECUTION-PLAN §3) | written decision in the launch packet | launch command |

## B. Scope (reissued)

| ID | Decision | Proposed | How | Blocks |
|---|---|---|---|---|
| ODR-R10-01 (replaces ODR-RG-01) | Approve the scope manifest `docs/rehearsal-010-readiness/scope-manifest.json` **as recompiled after v4.1.4 is frozen and ODR-R10-02 is applied**: 199 requirements, 863 obligations and 36 work items today (scopeSha256 `@SCOPE8@…` after FREEZE step 2, which is final unless the successor is ratified); 205, 890 and 37 if the successor is ratified. Zero exclusions | approve the recompiled digest | `owner-freeze.fsx decide rehearsal-010` (asked only once the scope is current) | the lock |

## C. Governed verification contracts (new)

| ID | Decision | Proposed | How | Blocks |
|---|---|---|---|---|
| ODR-R10-02 | Ratify the catalog successor `docs/rehearsal-010-readiness/proposals/causa-investigation.requirement-catalog.successor.proposed.json`. It adds distinct list, start and workspace routes and INV-039..044, using Dokimos-supported component kinds (region, link, display) instead of the audit draft's list, navigation and status, which Dokimos refuses. It compiles under Praxis 3.25.0: 44 requirements, 277 obligations, no findings | ratify, or decline. If declined, the journeys still judge the same behaviour; only catalog-level coverage of structure is lost | owner ratification commit that replaces the catalog file (the agent never edits the ratified catalog), then pin its sha256 in the catalog contract | none for launch (optional strengthening) |
| ODR-R10-03 | Approve `kickoff/rehearsal-010/catalog-verification-contract.json`. Governed acceptance binds the candidate, the catalog digest `19a03668…`, the contract digest, the governed-gate producer, the result and failures. Fault sensitivity `content-round-trip` = mandatory save/reload/isolation obligations, with a probe spec required | approve | pin `catalogVerification.contractSha256` in the lock | release gate |
| ODR-R10-04 | Governed fact `domain-profile-selected = false`: Rehearsal 010 offers no domain or profile selection, so INV-003 is not applicable. A candidate may not declare otherwise | approve, or set `true` and require INV-003 | edit the fact in the contract before pinning (R10-03) | release gate |
| ODR-R10-05 | Approve `kickoff/rehearsal-010/design-contract.json`, which sets: Forma roles by name (`ef-navigation-shell`, `ef-status-lozenge`) for Forma 0.12.x/0.13.x; Investigations, Administration and Settings; the five sections; the UI-016 palette as tokens; the theme boundary (literal colours only as custom-property values in a CSS file whose path contains `theme`); FL-01..10 at 390, 820×1180, 1180×820 and 1440; screenshots for the owner's visual review | approve | pin `designContract.contractSha256` | slice gate (UI slices), release gate |
| ODR-R10-06 | Approve `kickoff/rehearsal-010/deadline-policy.json`: 420 minutes, no reset, T0 record `evidence/t0/t0.json` committed once at T0, no compute limit approved | approve | pin `deadlinePolicy.sha256` | stop-decision, resume |
| ODR-R10-07 | Approve the journey bindings `verification/journeys/bindings.json` and their accessible-name contract. The catalog `name`s; the destination and section names of UI-020 and UI-029; the button "Record evidence" (or "Add evidence"), which no requirement names; the connection form matched by label pattern (owner, repository, branch, token or credential) | approve. The builder receives these names at launch | pin `journeys.bindingsSha256` | journeys |
| ODR-R10-08 | Loading-screen priority (R8): which WASM-LOAD requirements are P0 under UI-014-O2 | WASM-LOAD-001 (a loading state) and WASM-LOAD-005 (failure with Retry) are P0; the rest stay as written. Until decided, J3-10 is reported but does not fail the journey | owner decision record (OD-INDY-24) and a successor package if UI-014-O2's text changes | J3-10's priority |
| ODR-R10-09 | The live test repository and its credential | `kemiller2002/causa-acceptance-r10` (private), branch `causa-acceptance`, with a GitHub App installed on that repository alone. Its installation token is minted per job and revoked at job end; the Verification gate itself is `contents: read` (LIVE-PROVIDER-PREFLIGHT.md) | `decide rehearsal-010`; create the repository, the App and the secrets | live journeys, release |

## D. Requirement conflicts (R8; normative text changes go through owner decisions and a successor package)

The agent did not edit any normative document. These sections are compiled into the requirements baseline:
- `KICKOFF-EXECUTION-PACKET.md` §6;
- `CAUSA-UI-EXPERIENCE-REQUIREMENTS.md` UI-026 and UI-014.

| ID | Conflict | Proposed resolution | Mechanism |
|---|---|---|---|
| ODR-R10-10 | The cut order item 1, "visual polish beyond accessibility", can be read to cut UI-016-O18 and the design contract | add "and beyond the design contract (UI-016-O18 and the governed design contract are not cuttable)" | OD record + requirements baseline regeneration (OD-INDY-15) + successor package |
| ODR-R10-11 | UI-026 (P0) is "centered on the causal graph" while UI-002 (graph) is P1 | "For P0, the causal graph may be the accessible text relationship list of UI-002 plus a `forma-workflow` rendering of hypothesis–evidence links; the interactive features of UI-002 remain P1." J2-08 already judges this P0 floor | same |
| ODR-R10-12 | OD-INDY-22 ("start from the requirements alone") read as forbidding owner-side verification assets | clarification: "Requirements-side verification assets in Indy-init (journeys, design contract, layout assertions, harness fixtures) are inputs, not copies of a rehearsal's implementation." The harness fixture app is a synthetic positive control, not derived from any rehearsal | OD record (no package change) |
| (no decision) | The frozen v4.1.2 README still says "CANDIDATE" | not edited (frozen). The status is stated correctly in `AGENTS.md` and in v4.1.4's README | — |
| (done) | Stale "(proposed)" and "Tool releases: not yet available" in `docs/requirement-catalogs/CAUSA-INVESTIGATION.md` (not a baseline source) | corrected on this branch | — |

## E. Launch and process

| ID | Decision | Proposed |
|---|---|---|
| ODR-R10-13 | Compute budget (R7). A stated budget is not a stop condition. A stop on compute needs a separate `{action: approve-compute-limit, decision_id, limit}` record and evidence that the limit is reached | no compute limit: the full 420-minute window is expected to be used |
| ODR-R10-14 | Process-evidence rules for Rehearsal 010 (they name the competition repository) | regenerate the Rehearsal 009 rules for the new repository with `tools/owner-r9-ratify.fsx`'s pin step, and pin the sha256 in the lock |
| ODR-R10-15 | Independent review of the combined result. A reviewer agent spawned by this session reviewed it (FINAL-READINESS §5) | the owner decides whether that suffices or a separate reviewer is required before approving v4.1.4 and merging #104 |
| ODR-R10-16 | Merge #104; close #101 as superseded (#102 is already merged; #104 carries #101's review fixes that #102 did not) | merge after the review and green CI |
| ODR-R10-17 | Specula observer adapter: approve the Specula requirement and adapter change (FINAL-READINESS §7) | approve |
| ODR-R10-18 | The full scope does not fit 420 minutes under Topology A at any CI latency (SLICE-EXECUTION-PLAN §3, `evidence/schedule/`). Launch with the full scope and partial integration as the expected, reported outcome; or reduce the scope; or extend the window | A: launch with the full scope |

