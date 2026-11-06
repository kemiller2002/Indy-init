# WDL-013 obligations: the minimum owner-controlled change for v4.1.6

Status: PROPOSAL for the owner (kemiller2002). Nothing here is approved, recorded or verified.
Evidence base: Indy-init origin/main 89f5840.

## 1. What the frozen package already defines (no derivation needed)

The frozen v4 package already derives three obligations for WDL-013, verbatim from the requirement text
(`docs/WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md:36`):

| Id | Criterion (verbatim, frozen) | Frozen source |
|---|---|---|
| WDL-013-O1 | Compare the existing fixed-coordinate SVG approach against the reusable workflow layout system on identical causal graphs. | `requirements-compiled/v4/requirements-inventory.json:41806-41811` |
| WDL-013-O2 | Record layout correctness, overlap, clipping, edge crossings, responsiveness, implementation effort, test effort, and agent token consumption. | `requirements-inventory.json:41813-41818` |
| WDL-013-O3 | Hold graph data and acceptance criteria constant; preserve original measurements and distinguish observed results from estimates. | `requirements-inventory.json:41820-41825` |

- Acceptance: own obligations O1..O3, linked section `WORKFLOW-DIAGRAM-INTEGRATION§acceptance-scenarios` (scenario 8, `WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md:51`: "Run the controlled comparison and publish measured results, including token and implementation costs.") (`requirements-inventory.json:41832-41840`).
- Dependencies: no referenced requirements; work item T23 (`requirements-inventory.json:41842-41847`; `requirements-compiled/v4/dependency-graph.json:4970-4975`). Contract dependency CT-002 (`verification-manifest.json:18288-18291`).
- Verification method: VM-WDL-013, rules VR-LEDGER, VR-PRESERVATION, VR-UI; gates G0, G5; `currentStatus: NOT_RUN` (`requirements-compiled/v4/verification-manifest.json:18261-18289`).
- Status: `implementationStatus.status: not-verified` (`requirements-inventory.json:41828-41831`).

What the frozen package does NOT give them: a test identity. `requirements-compiled/v4/verification-plan.json` is P0-only (`scope.priority: "P0"`, lines 24-25) by owner decision ODR-V4-10 option A (`requirements-compiled/v4/FREEZE.md:85`; APR-V4-OWNER-1). All 18 P1 requirements of the inventory (AR-016, AR-025, AR-026, AR-032, AR-033, PF-010, PF-021, UI-002, UI-007, UI-008, UI-009, UI-012, UI-027, UI-028, UI-035, UI-036, VER-009, WDL-013) are handled identically: in scope at gate level (`docs/rehearsal-010-readiness/scope-manifest.json:13859-14221`, rule at 13860), not excluded (`exclusions.requirements: []`, 14223-14226), with no per-obligation test binding.

Giving O1..O3 test identities therefore changes the frozen verification contract. That is the owner-controlled change below.

## 2. The minimum change (one owner decision, carried in v4.1.6)

**ODR-V416-WDL013 (proposed):** For WDL-013 only, supersede ODR-V4-10 A's gate-level treatment: WDL-013 is verified per obligation. Its obligations are the frozen WDL-013-O1..O3, carried verbatim (no new obligation, no rewording, no weakening). Priority stays P1. All other non-P0 requirements stay under ODR-V4-10 A.

Package effect (v4.1.6; v4 and v4.1.5 not edited):
1. A supplementary per-obligation plan entry for WDL-013 (or a successor plan whose only difference from `v4/verification-plan.json` is this entry), with each obligation's `acceptanceContract` derived by the existing rules exactly as for any plan requirement.
2. An execution-plan binding of the three test identities below to suite SU-BROWSER-CHROMIUM (the suite of WDL-001..015).
3. Recompiled approved scope: WDL-013 moves from `gateLevelScope` to `requirements` with three obligations (new `scopeSha256`; the competition needs an owner `approve-scope` for it).

| Obligation | Verification method (mechanical, from VM-WDL-013) | Required categories (derived as for WDL-012, which has the same rules) | Intended test identity | Implementation reference | Status |
|---|---|---|---|---|---|
| WDL-013-O1 | VR-LEDGER (G0), VR-PRESERVATION (G0), VR-UI (G5) | browser (EC-UI-BROWSER), accessibility (EC-UI-ACCESSIBILITY) | `playwright:acceptance/WDL-013.spec.js > WDL-013-O1 [chromium]` | T23 (frozen DAG; see D2) | not verified |
| WDL-013-O2 | same | same | `playwright:acceptance/WDL-013.spec.js > WDL-013-O2 [chromium]` | T23 | not verified |
| WDL-013-O3 | same | same | `playwright:acceptance/WDL-013.spec.js > WDL-013-O3 [chromium]` | T23 | not verified |

## 3. Owner questions this change cannot settle mechanically

- **Q1 (records).** O2's implementation effort, test effort and agent token consumption, and O3's "distinguish observed results from estimates", are records that a browser test cannot observe. The mechanical derivation (VR-UI) gives browser and accessibility only. Option: also bind `praxis:independent-review` (kind review, category independent-acceptance, producer SU-REVIEW-RECORDS) to O2 and O3, as AR-001-O1 does. This adds a category and is your call. It is not part of the minimum.
- **Q2 (implementation unit).** The frozen DAG gives T23. The rehearsal-010 scope links WDL-013 to no work item (D2 below), and the P0 WDLs are in `WI-GEN-WDL`. Choose T23 (frozen) or WI-GEN-WDL (scope as approved).

## 4. Related defects (not fixed tonight, reported)

- **D1: WDL-015-O2's lane test reads the wrong record.** `tests/browser/acceptance/WDL-015.spec.js:27-31` (competition, origin/slice/SL-WI-GEN-WDL) looks for WDL-013 in `.echelon/scope-checkpoint.json`. That file holds only the scope hash and counts. Gate-level membership is in the pinned scope manifest's `gateLevelScope`, which is not an exclusion. After v4.1.6, the test passes only once `.echelon/obligations.json` is recompiled from the v4.1.6 plan.
- **D2: scope compiler selector defect.** `tools/requirements-compiler/src/Scope.fs:95-98` (`covers`) handles bare ids and `regex:` only. The frozen selector grammar, `Model.fs:119-129` and used by v4.1.5 DAG and VR-UI, also has `doc:`, `prefix:`, `origin:`, `id:`, `all`. So T23's `doc:docs/WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md` (`v4.1.5/execution-dag.json:413`) links nothing: WDL-013 is in no work item's `gateRequirements`. The same applies to T18 `prefix:PF-`, T20 `prefix:EXT-DATA-`, T25 `origin:external` and every `doc:` cover. This is why WI-GEN-* items exist for families the DAG covers. Repairing it re-maps P0 work items and changes the approved scope hash (`ScopeTests.fs:880` requires the committed manifest to equal compile output). That is an owner-controlled re-scope, not a minimum repair.
