# Recommendations

**How the recommendations were chosen**
- Each one modifies an existing requirement, gate or harness: `tools/r9/r9.fsx`, `tools/r9/verification-gate.yml`, `tools/r9/tests.fsx`, CAT-CAUSA-INVESTIGATION, or a successor package.
- None adds a framework, an orchestration layer or a dependency.
- All tooling is F# (OD-INDY-14).
- None edits a frozen package. Where the plan must change, the proposal is a successor package through the owner's freeze process.
- None is implemented here.

**Scoring** (1 = low, 5 = high; for effort, compute and new-failure risk, higher means *cheaper/safer*)

| Rank | ID | Recommendation | Findings | Shortcut-risk reduction | Demo importance | Effort (cheap = 5) | Compute (cheap = 5) | New-failure risk (safe = 5) |
|---|---|---|---|---|---|---|---|---|
| 1 | R1 | Make the governed gate executable: runner-only successor package plus a bootstrap-tree harness case | SA-01, SA-10 | 5 | 5 | 4 | 5 | 4 |
| 2 | R2 | The governed gate runs the catalog; one combined verdict; fail closed on unknown sensitivity categories | SA-02, SA-07, SA-08, SA-13 | 5 | 4 | 4 | 5 | 4 |
| 3 | R3 | Three owner-authored black-box journeys, run live in Chromium and WebKit by the governed gate | SA-06, SA-10, SA-11, SA-12, SA-14 | 5 | 5 | 3 | 3 | 3 |
| 4 | R5 | Governed design-contract check (Forma shell elements, no bare colours, palette tokens, FL-01..10, screenshots) | SA-05 | 4 | 5 | 3 | 4 | 3 |
| 5 | R4 | Catalog structure: list and workspace routes, navigation destinations, investigation sections, save-state outcomes | SA-04, SA-12 | 4 | 4 | 4 | 5 | 4 |
| 6 | R6 | Computed scope reconciliation and a completion rule | SA-03, SA-09 | 4 | 4 | 4 | 5 | 4 |
| 7 | R7 | Launch-packet clauses: priority order; gate defects do not release scope; report format | SA-02, SA-03, SA-09 | 3 | 3 | 5 | 5 | 5 |
| 8 | R8 | Fix requirement conflicts and stale status lines | SA-15 | 2 | 2 | 5 | 5 | 5 |

**Why R4 ranks below R5.** R5 checks the design system on whatever routes exist. R4 changes which routes must exist. R4 needs owner ratification of new catalog entries, so it is slower to land, though cheap.

## R1: Make the governed gate executable (highest priority)

**Problem.** The release gate refuses every possible candidate before judging anything (SA-01). Until this is fixed, no other gate improvement changes agent behaviour, because the gate cannot pass.

**Change**
- A successor package (proposed `v4.1.3`; a runner-only scope, with an owner decision and freeze) that changes only `suites[].runner` for SU-OWN-DOTNET and SU-DOTNET-LIVE to one of:
  - one `dotnet test <project>` per project, with `r9.fsx run-suites` taught to accept a runner list; or
  - a solution filter file committed by the bootstrap (`tests/Causa.Plan.slnf`).
- SU-BROWSER-LIVE gets the token mechanism 009 proved works: the workflow `github.token` on the competition repository's data branch.

**Verification**
- New `tools/r9/tests.fsx` case `run-suites-on-bootstrap-tree`. It runs the real bootstrap (`requirements-compiler v41 --bootstrap`) into a scratch repo, runs `run-suites` for every suite, and requires a result file per suite. Failing tests are fine; MSB1008 is not.
- This is the case whose absence let the defect through two readiness verdicts.

**Cost**
- Owner and engineering time: about 2–3 h, plus the freeze ceremony.
- Compute: none added per rehearsal. The harness case adds about 2 min to the readiness suite.

**Risk.** Changing a frozen artefact's successor. Limit the diff to runner strings and verify it with `git diff --stat` in the package's review.

## R2: One governed verdict that includes the catalog

**Change**
- In `tools/r9/verification-gate.yml`, add a step before `gate`. It:
  - checks out the pinned Indy-init commit;
  - runs `praxis requirements manifest compile` on `requirements/catalogs/causa-investigation.requirement-catalog.json` with the candidate's `requirements/requirement-implementation-map.json`;
  - runs `dokimos verification requirements` on the candidate's observations.
- `r9.fsx gate` takes `--catalog-verification <file>` and refuses in release mode unless `acceptance.accepted` is true at the same candidate SHA.
- Gate `--scope` accepts INV IDs from the governed manifest (SA-13).
- `--require-fault-sensitive` refuses when its category matches zero obligations, or when release mode has no probe spec (SA-07).
- Applicability facts come from an owner-pinned governance file, not from probe output (SA-08).
- Final reports lead with the governed verdict. Any implementer-run verdict is labelled "implementer-run" (wording in PROPOSED-REQUIREMENT-CHANGES.md §4).

**Verification.** Harness cases:
- catalog-accepted / plan-refused → REFUSED;
- catalog-rejected / plan-passed → REFUSED;
- unknown sensitivity category → REFUSED;
- probe-supplied fact contradicting governed fact → REFUSED;
- INV-scoped work item with passing catalog → completes.

**Cost.** About 4–6 h. CI: about 2 min per release run. Tokens: none.

## R3: Owner-authored black-box journeys

**Purpose.** These are the representative user journeys the brief asks for. They are written once, by the owner side, from the requirements (OD-INDY-22 does not prohibit this: it governs what a *rehearsal* copies, and these are requirements-side verification assets in Indy-init like `layout-tests/assertions.mjs`).
- They drive the UI only by role and accessible name taken from the catalog.
- They read GitHub directly through the REST API.
- The implementer cannot edit them.

**The three journeys**

| Journey | Steps | Kills |
|---|---|---|
| J1 Investigate | List → New → statement, title, question → 2 observations, 2 hypotheses, 2 evidence → 4 links of each kind with rationale → conclusion → Save (indicator must reach "Saved" only after the governed proxy sees the commit) → new context → open from list → every value visible, links shown on **both** evidence and hypothesis → edit, reassess → reload → history shows earlier values | inert inputs; session-state persistence; one-sided links; constant save label |
| J2 Demonstration | Connect → list shows exactly the four EXT-DATA-013 titles → open Apollo 1 → at least one observation, hypothesis and evidence text from `causa-data` visible → Causal Analysis destination opens and shows a text relationship list → Report destination renders a printable page (`@media print` emulation) | dropped demo catalog; missing analysis and report; navigation stubs |
| J3 Failure | Two contexts edit the same investigation → second Save shows Conflict and keeps unsaved values → resolve → `context.setOffline(true)` shows Offline with values kept → proxy drops one content field on read → reload is refused or shows the missing value, never silently empty | double-only failure testing; missing Offline state; self-chosen fault probes |

**How they run**
- Each journey runs live in `chromium` and `webkit` (iPad 820×1180) inside the governed gate.
- The gate refuses in release mode on any failure.
- In development mode they run non-blocking and publish results, so agents see the target from minute one.

**Verification**
- Run the journeys against the 009 deployed site. Expected result: J1 passes except "links on both ends" (unverified), J2 fails, J3 fails at Offline.
- Run them against a fixture app built for the harness. Expected result: pass.

**Cost**
- Owner and engineering: about 1–1.5 days for the journeys and a small Playwright route-based proxy.
- CI: about 6–8 min per release run (two engines).
- Implementer tokens: no new tests to write. Building what the journeys need is work the intended application already requires, estimated at +150–250 k tokens over 009's run, inside the 7 h window 009 left two-thirds unused.

**Risk.** Accessible names become a contract. Mitigation: take the names from the catalog's `name` fields and add a catalog `accessibleName` field only where they differ (PROPOSED-REQUIREMENT-CHANGES.md §2).

## R5: Governed design-contract check

**Change.** A step in the governed gate, written in F# or as Playwright in the governed checkout, run against the candidate's `dist/` and the journey pages. Each item is a refusal cause:
1. `ef-site-header` and `ef-navigation-shell` (or the Forma 0.13 names in the pinned inventory) are present on the list and workspace routes.
2. The save state renders through `ef-status-lozenge`.
3. Application CSS contains no bare hex, `rgb(` or `hsl(` outside one theme file, and every colour is `var(--ef-…)` or a Causa token defined in that theme file.
4. The five UI-016 palette values each appear as a token value in the theme file and as a computed colour in at least one populated J1/J2 state.
5. The pre-written `layout-assertions.mjs` FL-01..10 runs from the governed checkout, not from implementer code.
6. Screenshots at 390, 820×1180, 1180×820 and 1440 are uploaded as an artefact and listed in the final report for the owner's visual review. This makes UI-016-O16's "independent visual review" a real owner action, not an implementer spec.

**Verification**
- The 009 `dist/` is refused on causes 1, 2, 3 and 4.
- The 008 `dist/` is refused on 4 only.
- A fixture passes.

**Cost.** About 1 day. CI: about 3 min. Tokens: the implementer must build the shell (estimated +60–120 k).

**Unnecessary restriction to relax at the same time.** UI-016's "exactly these hex values" can become "these five starting colours, with derived tokens that meet contrast". The document already says that; make the check accept derived shades for foreground use.

## R4: Catalog structure (proposed entries, owner ratification)

Add screens and entries so that application structure becomes verifiable with Dokimos's existing check kinds:
- screens `investigation-list` (`#/`) and `investigation-workspace` (`#/investigations/{id}`);
- INV-039..INV-044: list, open, primary destinations, investigation sections, the seven save-state outcomes, and links shown on both ends.

Draft JSON is in PROPOSED-REQUIREMENT-CHANGES.md §2.

**Cost.** About 3 h plus ratification. CI: under 1 min. Tokens: +40–80 k per rehearsal.

**Note.** Where Dokimos 0.11.0 lacks a check (for example route assertion), J1 and J2 (R3) cover it instead. Do not add Dokimos features for this.

## R6: Scope reconciliation and completion rule

**Change**
- `r9.fsx scope-report` reads the governed plan, the catalog manifest and the candidate's results. For each P0 requirement group it emits:
  - identities written;
  - identities passing;
  - catalog obligations passing;
  - journeys passing;
  - a disposition from {done, partial, blocked-by:<owner-decision-request id>, excluded-by:<owner decision>}.
- Release refuses on any P0 group with nothing written and no disposition.
- The final report embeds this table verbatim.
- **Completion rule** (launch packet): before T+390 the agent may stop only if the governed gate passes, or every P0 group is done/partial or blocked by a filed request. A broken suite blocks only that suite's obligations.

**Verification.** `scope-report` on the 009 tree lists UI-018, UI-019, UI-026, UI-029, UI-009 (P1, informational) and EXT-DATA-013 with nothing written, and exits 4.

**Cost.** About 4 h. CI: under 1 min.

## R7: Launch-packet clauses (prose, but checkable)

Keep these short and make each one checkable by R2, R3, R5 or R6:
1. **Priority order:** J1 > J2 > design contract > catalog negatives > plan P0 > P1.
2. **Gate defects:** "A defect in a governed gate is reported and worked around only for the obligations it blocks. It never narrows scope." This is checked by R6.
3. **Report format:** the governed verdict comes first, then the scope table. "Verified" without qualification means governed-verified. This is checked by review.
4. **Time:** work continues until the completion rule holds. The compute budget is stated as a number (tokens or CI minutes), not as "constrained".

**Cost.** About 1 h.

## R8: Conflicts and stale lines

Fix in non-frozen documents only; see PROPOSED-REQUIREMENT-CHANGES.md §5.
- Cut order versus UI-016-O18.
- UI-026 versus UI-002.
- UI-014-O2 versus WASM-LOAD.
- The "CANDIDATE", "(proposed)" and "not yet available" status lines.

## Requirements that are unnecessarily restrictive or costly and could be simplified safely

| Item | Why it costs more than it protects | Safe simplification |
|---|---|---|
| ~540 plan obligations each bound to one exact test identity (`…PERSIST001Tests.PERSIST-001-O1`) | It rewards writing hundreds of named test shells. 009 cited this as the reason not to start | Let one journey or test identity cover several obligations (the v4.1.2 execution plan already holds 784 bindings over 660 tests for 613 obligations, so binding one test to several obligations is representable; confirm Dokimos coverage counts it before relying on it); make R3's journeys the bound identity for the UI P0 obligations they exercise. A successor-package decision |
| 105 obligations satisfied only by `praxis:independent-review` | Makes "independent review" a single record that is easy to under-specify | Keep it, but require the review record to cite the R6 scope table and the R5 screenshots |
| UI-001 "exactly four" from an external repository | Brittle if the data repository changes | "the active catalog's investigations (four in EXT-DATA-013)", checked against `catalog/active.json` |
| UI-016 exact hex values for all uses | Forces failing contrast combinations | Already allowed by the prose; align the check (R5) |
| OD-INDY-22, read as forbidding owner-side verification assets | It would block R3 and R5 | Clarify: it applies to code from earlier *rehearsal* repositories, not to Indy-init verification assets authored from requirements |

## Not recommended

| Option | Reason |
|---|---|
| A new orchestration layer or an extra agent role | The failures are in gate executability and scope, which existing tools can fix |
| More prose exhortation ("implement the full application") | 009's report shows the agent read the requirements carefully and still followed the measurable path. Phase 4 reproduced this |
| Making every P1 item mandatory | Raises cost without addressing the demonstration-critical gaps. R3's J2 already pulls in the analysis and report views at the depth the demonstration needs |
