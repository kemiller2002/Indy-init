# Rehearsal 009 adversarial replay

**What this is.** Rehearsal 009 used as a **read-only negative control**: what would the corrected Rehearsal 010 process decide about the run it actually produced?

**Runner:** [`tools/r10/r9-replay.fsx`](../../tools/r10/r9-replay.fsx) (F#). CI job `r10-adversarial` reruns it.

**Evidence:** [`evidence/r9-replay/`](evidence/r9-replay/):
- `replay.tsv`, `replay.json`;
- one log per check;
- every verdict file.

**Inputs**

| Input | Value |
|---|---|
| kemiller2002/indy-init-rehearsal-009 | `0e760501b5585d11009a2fd2da8d146a1d47e025` (final). Release candidate `39dc037` |
| Rehearsal 009's own evidence | `evidence/requirement-verification/*`, `evidence/release-gate/local-gate-release-39dc037.json`, `.echelon/fault-probes.json`, `evidence/FINAL-REPORT.md` |
| Its progress events | #102's reconstruction `docs/rehearsal-010-readiness/evidence/stop-decision/r9-events.json` |
| Its T0 | 00:30:01Z (`evidence/t0/T0.md`). The `indy-init.t0/1` record format is new in Rehearsal 010, so the replay synthesizes it from T0.md |

**Historical outcome preserved.**
- The replay works on a **copy** of the clone. The evidence repository is never written, repaired or re-judged.
- The copy gets one replay-only commit, adding the T0 record in the new format so the observer can bind its clock. That commit exists only in the scratch copy.
- Rehearsal 009's historical verdict (governed gate REFUSED; its catalog run accepted 36/37 by its own workflow) stands as recorded.

## Results: 15 of 15 decided as expected

| Check | Finding | Kind | Decision of the corrected process | Key evidence |
|---|---|---|---|---|
| RP-01 | Missing full application shell | **observed** | `design-contract` REFUSED: FORMA_ROLE_MISSING navigation-shell (no `ef-navigation-shell` anywhere) | RP-01.log |
| RP-02 | Missing Administration and Settings | **observed** | DESTINATION_MISSING Administration, Settings | RP-02.log |
| RP-03 | Missing causal analysis views | **observed** | SECTION_MISSING Overview, Causal Analysis, Timeline. Evidence and Hypotheses exist and are not flagged | RP-03.log |
| RP-04 | Incomplete Forma integration | **observed** | FORMA_ROLE_MISSING save-status (no `ef-status-lozenge`); LITERAL_COLOUR_OUTSIDE_THEME ×4 (the audit's 4 bare colours) | RP-04.log |
| RP-05 | Missing design tokens | **observed** | PALETTE_TOKEN_MISSING ×5 (all of UI-016); THEME_MISSING | RP-05.log |
| RP-06 | Missing independent acceptance evidence | **observed** | `catalog-acceptance` not governed. PRODUCER_NOT_GOVERNED (its own `requirement-verification.yml`); CATALOG_WRONG_COMMIT (verdict for `33d072d`, release candidate `39dc037`); FAULT_INSENSITIVE for content round-trip obligations no detected probe names | RP-06.log |
| RP-07 | Incomplete scope presented as the approved scope | **observed** | `completion-gate` INCOMPLETE. SCOPE_NOT_INTEGRATED; RELEASE_GATE_REFUSED (its own release gate: 662 causes); CATALOG_NOT_GOVERNED_ACCEPTED; JOURNEYS_MISSING; DESIGN_CONTRACT_MISSING | RP-07.log |
| RP-08 | Premature stopping (115 of 420 min, "compute constraint") | **observed** | `stop-decision` CONTINUE_REQUIRED, NOT_A_STOP_CONDITION, 306 minutes remaining, 161 open mandatory requirements | RP-08.log |
| RP-09 | Stop claimed as complete on the catalog alone | **observed** | CONTINUE_REQUIRED: SCOPE_INCOMPLETE, CATALOG_IS_NOT_SCOPE, SCOPE_NOT_INTEGRATED | RP-09.log |
| RP-10 | Stop because the release gate failed | **observed** | CONTINUE_REQUIRED: NOT_A_STOP_CONDITION ("a failed release gate is a verification result") | RP-10.log |
| RP-11 | Incorrect Praxis sequencing (scope checkpoint) | **observed** | `scope order` REFUSED: IMPLEMENTATION_BEFORE_CHECKPOINT. The first implementation commit is `edde38c`; the T0 bootstrap is exempt | RP-11.log |
| RP-12 | Incorrect Praxis sequencing (initialization checkpoint) | **observed** | `check-sequencing` REFUSED: no initialization checkpoint, plus 6 process violations (`edde38c` … `33d072d`) | RP-12.log |
| RP-13 | Incomplete scope by requirement group | **observed** | `ledger` CONTINUE_REQUIRED. 161 not started; 36 verified but unmerged; 1 failed (INV-029); 0 integrated. Groups AR, EXT-DATA, PERSIST, PF, UI, VER and WDL have every mandatory obligation missing; only INV has progress | scope-report.json |
| RP-14 | The observer, over the run | **observed** | 10 blocking findings: IMPLEMENTATION_WITHOUT_WORK_ITEM ×6, DESIGN_CONTRACT_FAILED, SELF_VERIFIED_SUCCESS (its FINAL-REPORT's verified claim), PREMATURE_STOP (a refused stop at 02:25Z, then no implementation work), SCOPE_COVERAGE_UNKNOWN. 1 warning: UNMERGED_VERIFIED_WORK | observer-findings.json |
| RP-15 | Invalid suite execution | **observed** | `suite-preflight` on the v4.1.2 plan: SU-OWN-DOTNET and SU-DOTNET-LIVE refused with MSB1008 | RP-15.log |

**Reading RP-15 correctly.**
- The four Playwright suites are also refused in that log. That is because the replay copy has no `npm install`: an environment limit of the replay, **not** a Rehearsal 009 finding.
- Rehearsal 009's own Playwright defect, the undefined `webkit` project, is evidenced by #101's run at `13e1cf8`: `docs/release-gate-repair/evidence/suite-preflight-v4.1.2-r9.log`.

## Observed failures versus hypothetical vulnerabilities

**Observed in Rehearsal 009.** Every row above is a decision about something Rehearsal 009 actually did or failed to do, read from its committed files and history. RP-15's MSB1008 refusals reproduce, on its tree, the defect its release gate hit.

**Hypothetical: not observed in Rehearsal 009; covered by ADVERSARIAL-VALIDATION.md.**

| Case | Vulnerability | Why it is hypothetical |
|---|---|---|
| C06 | An inert field | Rehearsal 009's content fields worked and round-tripped live |
| C07 | A constant "Saved" | Rehearsal 009 had 6 real states (narrowed, but not constant) |
| C08 | A mock for live persistence | Rehearsal 009 used real GitHub for its round trip; its negatives ran on a double only, a partial observation |
| C10, C11 | Merging a failed slice; a regression | Rehearsal 009 had no slice process |
| C14 | A deadline reset | No replacement agent ran |
| C15 | An unauthorized scope change | Rehearsal 009 did not change the scope hash. It narrowed scope in prose ("the approved scope was the content catalog"), which RP-09 refuses |
| C18 | A weakened test | Not observed |

**Not detected by the replay** (limits stated, not hidden):
- **Journeys J1–J3 were not run against Rehearsal 009.** That needs its WebAssembly build served and a live GitHub test repository. From its source, they would fail:
  - J3-01: no Administration or Settings;
  - J2-08: no Causal Analysis section;
  - J1-02: "New investigation" is a button, not a list of investigations after connect.

  That last point is a prediction, not a run.
- **The rendered design contract was not run against Rehearsal 009,** for the same build reason. The static contract above found the same gaps.
- **Reporting:** UI-009 (Folio report) is **P1**. Rehearsal 009 has none (`Views/Report.fs` is the 2-line stub). It is reported here, not counted as a mandatory failure; J2-10 is a P1 assertion.
