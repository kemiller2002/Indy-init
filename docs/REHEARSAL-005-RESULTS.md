# Rehearsal 5 results (index for agents)

**Outcome: NOT ACCEPTED.** The pinned verifiers, Indy-init's `tools/compute-acceptance-verdict.py` and `requirements-compiler verify-plan`, computed **INCOMPLETE** for the final candidate `1b9fb56873fe7a98dffd5172066d909449c17eae` (CI run 40620099844). No requirement and no obligation failed. The verdict is not ACCEPTED because 49 of 326 P0 obligations have no test that proves them, and about 35 of those can only be proven by reviewer or owner evidence about how the rehearsal was conducted.
**Repository:** [kemiller2002/indy-init-rehearsal-005](https://github.com/kemiller2002/indy-init-rehearsal-005), final commit `1b9fb56873fe7a98dffd5172066d909449c17eae`.
**Findings for the next rehearsal:** AR-027..AR-035 in [AGENT-RELIABILITY-REQUIREMENTS.md](AGENT-RELIABILITY-REQUIREMENTS.md). **Plan curation:** Indy-init PR #32 (VP-926d0d1a223c), for the owner to merge.

## Key facts

| Item | Value |
|---|---|
| Requirements baseline | `BL-CAUSA-INDY-INIT-aab8f80c7743` (sha256 `621e2f96…57b9`), 461 requirements, 108 P0, **no exclusions**. T27 and T28 were in scope. |
| Verification plan | VP-e77a2134e1b0: 108 P0 requirements, 326 obligations |
| Kickoff set | echelon-current 1.29.0: Praxis 3.22.0, Ordo 1.18.0, Dokimos 0.8.0, Percepta 0.3.0, Limen 0.9.0, Forma 0.9.0, Folio 0.4.1, Aegis 1.0.0. `@echelon-foundry/forma-workflow` 1.1.0 is outside the set, under owner decision DF-CAUSA-2026-0001. |
| Clock | Start 2026-10-27 20:08:38 UTC (first green bootstrap CI). Deadline 2026-10-28 03:08:38 UTC (420 min). Final verdict at about 02:42 UTC (CI run 40620099844 completed): about 394 of 420 minutes. |
| Candidate | `1b9fb56873fe7a98dffd5172066d909449c17eae`, deployed to GitHub Pages (https://kevinmmiller.us/indy-init-rehearsal-005/). deployed-smoke checked the served `version.json` and every artifact byte for byte. |
| Gates | G0–G7 PASS (G7 for the first time in any rehearsal: every defect class closed and verified in the run). G8 and G9 NOT_RUN: the traceability producer needs all 326 obligations bound and passing. |
| Requirements (ledger) | 108 P0: 77 PASS, 31 NOT_RUN, **0 FAIL**. `verify-plan` against the uncurated VP-e77a2134e1b0 reports the 77 as UNBOUND; with the curated VP-5d1202ddec1e (PR #32) they are PASS. |
| Obligations (traceability) | 326: 277 bound and PASS, 49 with no proving test, **0 FAIL** |
| Defect classes | DC-001..DC-006, R3-LESSON-3 and DCT-LIVE-A were all closed by the independent T33 reviewer, with bundled evidence the verifier accepted. Fix commits: DC-001 `5336ec4`, DC-002 `e8e32c1`, DC-003 `b07d2a5`, DC-004/DC-005 `895fb6d`, DC-006 and R3-LESSON-3 `578ebd3`, DCT-LIVE-A `b07d2a5`. |
| Percepta | 24 of 25 screen contracts bound and PASS. S16 is NOT-APPLICABLE under owner decision DF-CAUSA-2026-0002. 0 FAIL, 0 NOT-BOUND. |
| Live | The provider-conformance job (19 live checks plus a live catalog load) and the live browser specs passed against the approved test repository. Live specs: DCT-LIVE-A with the two-tab F-1 variant, EXT-DATA-013 (three historical cases after Connect) and live-graph. |
| Preservation | The Praxis gate passed before implementation and at every main run. The independent derivation check (AR-002-O2) passed on the real package and caught every planted mutation. |

## What blocks ACCEPTED

1. **G8 and G9: 49 obligations have no proving test.** Most need evidence that a test of the application cannot give:
   - independent-reviewer conduct (AR-008);
   - agent scope and checkpoints (AR-012, AR-015, AR-021);
   - owner decisions and routing (AR-017, AR-020, AR-024, VER-010);
   - release selection records (VER-001..VER-005);
   - AR-019-O3, the R3/R2 ledgers, whose original form is not machine-readable.

   The rest are partial proofs that the reviews removed so they would not over-claim: UI-011-O4, UI-015-O3, UI-016-O11, UI-031-O5, PF-014-O2, PF-015-O1, EXT-DATA-005-O1, EXT-DATA-006-O2, WDL-014-O1 and others. The full list is the run's `traceability.json`.
2. **Plan curation:** until Indy-init PR #32 is merged and pinned, `verify-plan` reports bound obligations as UNBOUND.

## Defects found during the run (all fixed and re-reviewed)

| Id | Severity | Defect | Fix |
|---|---|---|---|
| F-1 | critical | The write marker was a per-page counter, so two writers collided. Reconcile then claimed "synced", and the next save erased the other writer's events. | A unique marker per page load; confirmation only after reading back the blob ids |
| F-2, R2-F2 | low, medium | An unrecognised answer after the write was sent meant "not written, Retry" | Treated as unknown, then reconciled |
| R2-F1, R3-F1 | high | Two tabs sharing one local store lost device-only changes | Per-investigation merge; ownership by the page's own nonce only |
| F-5 | medium | The ledger never bundled evidence for closed defect classes, so the verifier rejected every closure | Bundled review evidence with SHA ancestry and DCT results |
| Start failure | critical (main) | `View.project` exceeded the .NET WASM interpreter's per-method locals limit ("locals size too big"), so the app could not start | Split into nine area functions, plus a start smoke on every route |
| Test-hook export | high | The production page exposed `EnableTestHooks` through `getDotnetRuntime` | Compiled out of production; the global is removed; a test proves both |
| Port collisions | process | Shared test servers and hard-coded ports hid failures locally and broke CI | Per-stream ports; specs take ports from the config; a pipeline test guards it |
| Praxis gap | finding | `praxis requirements preservation verify` 3.22.0 passes value and role swaps | Our independent check covers AR-002-O2; the gate fix should be routed to Praxis |

Known limits that stay open: R3-F2 (the read-merge-write of the local store is not atomic across tabs), F-3 (the public catalog is not readable without a token), F-4 (a local build shows stale traceability).

## Owner decisions and deviations (RDP-003)

The owner made these decisions in chat. The agent transcribed them, and the transcriptions are recorded as such:

- Approvals files `kickoff/rehearsal-005/approvals.json` and `approvals.causa.json`: "You write the approval files".
- Equivalence reviews for the 775 verbatim-carriage obligations: "Yes, record it".
- The echelon-current 1.29.0 registry approval: "go ahead and update it".
- DF-CAUSA-2026-0001: forma-workflow 1.1.0 outside the release set ("Approved").
- DF-CAUSA-2026-0002: Percepta S16 NOT-APPLICABLE (owner selected only S16; S17, S18, S20 and S21 stayed in scope and were built).
- The AI provider: the Anthropic Messages API; the requirements name none ("No continue with what we have currently").
- `CAUSA_TEST_REPO` (secret) and `DEPLOY_TARGET` (variable) were set by the owner during the run.

Attribution: every commit on `main` is attributed to a Praxis work item (WI-0001..WI-0008). Agent commits on stream branches were reconciled post hoc by the coordinator.

## How it ran

- **Streams:** acceptance, core (F# engine), shell (Limen and the WebAssembly UI), persist (GitHub provider and Sync), ux (presentation and inspector), and two fresh reviewers (T30, T33). Each stream worked on its own branch; the coordinator merged into `main` after local verification.
- **Rehearsal 4 lessons applied from the start:**
  - `MONO_INTERPRETER_OPTIONS=-ssa,-bblocks`: the three historical cases load in WASM; a CI smoke proves it.
  - The Sync state machine owns every write.
  - Historical data is read at runtime after Connect.
  - The token stays in memory and is sent only in the Authorization header.
  - Percepta bindings exist from the first screen.
  - The acceptance pipeline was in place from hour 1.
- **Two container restarts** stopped the stream agents. They resumed from their saved transcripts.
