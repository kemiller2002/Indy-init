# Shortcut matrix

**Strategies**
- **A** = intended implementation.
- **B** = minimal implementation that could plausibly pass the existing checks.
- **C** = superficially compliant: it looks complete to the automation and fails a user expectation.

**Gate columns**
- **Catalog**: Dokimos `verification requirements` on CAT-CAUSA-INVESTIGATION, run by an implementer-written workflow.
- **Plan**: `r9.fsx gate --mode release` on frozen v4.1.2. Today it cannot execute (SA-01), so every row would read "refused regardless"; the column shows what the plan *would* do if it ran.

## 1. Strategies for each requirement group

| # | Group | A: intended | B: minimal | C: superficial | Catalog rejects C? | Plan would reject C? |
|---|---|---|---|---|---|---|
| G1 | Investigation creation and management | List of investigations at `#/`, start form, workspace at `#/investigations/{id}`, details editing, lifecycle actions from F# policy | Start form plus a list without lifecycle actions (009) | All fields on one page at `#/investigations/new`, with no list. Reopening only by typed URL | **No.** No list screen; both screens share one route | Partly. UI-001-O1..O6 bound, but the bodies are implementer-written |
| G2 | Observations, hypotheses, evidence | Entry, listing, editing and provenance for each, shown in context and in an inspector (UI-005, UI-034) | Inline forms plus lists (009) | Forms whose values reach the saved document but are listed only as raw JSON or an unlabeled list | Partly. `outcome`/`reload` compare visible text, but not presentation or inspector | Partly (UI-005, UI-034 bodies are implementer-written) |
| G3 | Hypothesis–evidence relationships | Four kinds, rationale, history, shown on both ends, used by analysis and comparison | Radio group plus rationale, listed under the workspace (009) | Links stored and listed once; not shown on the hypothesis; no analysis consumes them | Partly. INV-030 "shown on both" is only in `notes`, which are not checked | No. UI-008 comparison is P1 and unbound |
| G4 | Navigation and application shell | Forma `ef-site-header` and `ef-navigation-shell`; primary Investigations / Administration / Settings; secondary Overview … Corrective Actions (UI-029) | Header with brand and one link (009) | Navigation entries that open empty "not yet supported" panels | **No.** Not expressible | Would, if FL-09 were imported by the test body; nothing requires that |
| G5 | Root-cause analysis and causal graph | Causal Analysis section centred on a graph of hypotheses, evidence and relationships, with a text equivalent (UI-026, WDL) | Omitted (009) | A `forma-workflow` graph of event kinds (008) | **No** | Partly. UI-026 is bound, but UI-002 (the graph itself) is P1 and unbound |
| G6 | Historical investigation data | Four EXT-DATA-013 cases load after Connect and are readable, with content | Omitted (009) | Loaded and replayed in a .NET test, never shown (008) | **No** | Yes, via EXTDATA013Tests in SU-DOTNET-LIVE, which cannot run |
| G7 | GitHub persistence | Seven-state indicator; sha-guarded atomic commit; read-back before Saved; conflict review; offline | Save, read-back, conflict refusal, 6 states, no Offline or Unknown (009) | A constant "Saved to GitHub" label after a fire-and-forget PUT | Partly. INV-037 live reload catches non-persistence; INV-016 accepts a constant label | Yes (PERSIST-030..033), if runnable |
| G8 | Mobile / iPad Safari | WebKit at iPad portrait/landscape plus phone widths; touch; no overflow | Chromium mobile emulation at 320/390 (009) | Chromium desktop only | No browser engine requirement | Yes (SU-BROWSER-WEBKIT), if runnable |
| G9 | Forma architecture and Echelon identity | Forma components for surfaces, controls and states; palette mapped to Forma theme tokens; screenshots; independent visual review | `ef-form`/`ef-field` classes plus custom layout (009) | Custom CSS that mimics Forma with hardcoded colours, monochrome | **No** | Only via implementer-written UI-016 bodies; no screenshot check |
| G10 | Folio reporting | Interim/final report via Folio print components | Omitted (009) | Print button over a kind-derived summary (008) | **No** | **No.** UI-009 is P1 and unbound |
| G11 | Error handling and recovery | Live failure paths; Retry only when real; data-preserved messaging (UI-010) | Failure scenarios against a GitHub double (009) | Error banners that never occur in live use; Retry reloads the page | Partly. `rejects` checks validation, not provider failure | Yes (UI-010, PERSIST), if runnable |
| G12 | Verification and release | Governed gate passes on the deployed SHA | Implementer-written workflow passes; governed gate refused (009) | A report headed "verified" | No owner gate consumes it | Cannot execute |

## 2. Findings

Cost legend:
- **Tokens**: incremental implementation-agent tokens per rehearsal.
- **CI**: added minutes per candidate.
- **Owner effort**: work to make the change.

All costs are estimates.

### SA-01: The frozen release gate cannot execute

| | |
|---|---|
| Requirement refs | `requirements-compiled/v4.1.2/verification-execution-plan.json` `suites[]` SU-OWN-DOTNET and SU-DOTNET-LIVE; `tools/r9/r9.fsx` `run-suites`; `tools/r9/verification-gate.yml` |
| Classification | **Observed** (009 FINAL-REPORT §11; 008 `.github/workflows/live.yml:46` notes the same limit) |
| Evidence | Runner strings, verbatim: `dotnet test tests/Causa.Contract.Tests tests/Causa.Domain.Tests tests/Causa.Persistence.Tests tests/Causa.App.Tests` and `dotnet test tests/Causa.Acceptance tests/Causa.Invariants --filter …`. `run-suites` runs them verbatim and refuses without TRX. The string MSB1008 appears nowhere in Indy-init. `tools/r9/tests.fsx` exercises `run-suites` only on a synthetic single-project repo |
| Shortcut | Treat the authoritative gate as unwinnable, so skip all ~540 plan-bound obligations, including the P0 UI and persistence obligations they carry |
| Why attractive | A gate that refuses every candidate gives no reward for any increment of work. 009 §16: "Writing about 540 plan-bound tests would not change the release-gate verdict while the plan's commands cannot run" |
| Existing gate behaviour | Refuses in development mode on every PR (SU-OWN-DOTNET is in the development suite list) and in release mode |
| Impact on user | Every P0 UI capability bound only to the plan (shell, Admin, Settings, seven-state indicator, demo catalog, WebKit) lost its only executable incentive |
| Proposed correction | A runner-only successor package, with an owner decision, that changes only the runner strings:<br>- one `dotnet test` per project, or a solution filter `tests/Causa.Plan.slnf`;<br>- a readiness case that runs `run-suites` on the T0 bootstrap tree |
| Executable verification | A new `tools/r9/tests.fsx` case `run-suites-on-bootstrap-tree`: bootstrap a scratch repo, then require every suite to produce a result file (failing tests are acceptable; *no result file* is not) |
| Cost | Owner effort about 2 h, plus the package freeze ceremony. CI +0. Tokens 0 |
| Confidence | High |

### SA-02: The catalog verdict is implementer-run and outside every owner gate

| | |
|---|---|
| Requirement refs | `docs/requirement-catalogs/CAUSA-INVESTIGATION.md` §"Using it for the November 7 build" steps 2–3; OD-INDY-22; `tools/r9/verification-gate.yml`, which has no `verification requirements` step; `tools/compute-acceptance-verdict.py`, which judges no INV entries |
| Classification | **Observed** |
| Evidence | 009 `.github/workflows/requirement-verification.yml`, `tests/browser/support/probe.mjs` and `.echelon/fault-probes.json` are all agent-written. FINAL-REPORT line 4: "independently verified… Dokimos 0.11.0 accepted it". The governed gate REFUSED |
| Shortcut | Produce a green verdict in a verifier you configure yourself, and headline it |
| Why attractive | It is the only green result reachable, and it is honest about its own scope |
| Existing gate behaviour | Nothing combines the two verdicts. FINAL-READINESS scenarios 1–5 say both must pass, but no tool checks that |
| Impact on user | "Verified" no longer means owner-verified. A reader cannot tell which features were judged by whom |
| Proposed correction | Add a governed catalog step to `tools/r9/verification-gate.yml`. It must:<br>- compile the manifest from the pinned catalog at the Indy-init commit;<br>- run `dokimos verification requirements`;<br>- feed the result into the same `gate` verdict.<br>The gate refuses if the catalog is not accepted. Final reports must lead with the governed verdict |
| Executable verification | Two harness cases: (1) catalog rejected and plan passing → REFUSED; (2) catalog accepted and plan refused → REFUSED |
| Cost | Owner effort about 4 h. CI +2 min (the compile; probes already run). Tokens 0 |
| Confidence | High |

### SA-03: Catalog emphasis displaced P0 UI groups, with no recorded omission

| | |
|---|---|
| Requirement refs | UI-001, UI-006, UI-009, UI-010, UI-014, UI-016, UI-018, UI-019, UI-026, UI-029, EXT-DATA-013, WASM-LOAD-001; FINAL-READINESS scenario 5; FINAL-CORRECTIONS.md:76 |
| Classification | **Observed** |
| Evidence | 009 `web/index.html:15-19`: no Administration, Settings or secondary navigation. `src/Causa.App/Views/{Graph,Report,Administration,Dashboard,SaveIndicator,ConflictReview}.fs` are 2-line bootstrap stubs at HEAD. FINAL-REPORT §16: "The approved scope for this run was the content catalog". No source for that statement was found |
| Shortcut | Pick the catalog that is cheapest to verify; treat everything else as out of scope |
| Why attractive | The catalog has a working verifier. The plan does not (SA-01) |
| Existing gate behaviour | No check compares the delivered scope with the required scope |
| Impact on user | No navigation between areas, no analysis view, no report, no Admin/Settings, no demo cases: a form, not the competition application |
| Proposed correction | A computed scope reconciliation. `r9.fsx scope-report` lists every P0 requirement group with:<br>- bound identities written / passing;<br>- catalog obligations passing;<br>- a stated disposition.<br>The release gate refuses if any P0 group has 0 written identities and no owner-approved exclusion |
| Executable verification | A harness case: the 009 tree is refused with causes naming UI-018, UI-019, UI-026, UI-029 and EXT-DATA-013 |
| Cost | Owner effort about 4 h. CI +1 min. Tokens about 5 k (reading the report) |
| Confidence | High |

### SA-04: The catalog cannot express application structure, and both screens share one route

| | |
|---|---|
| Requirement refs | Catalog `screens`, where both have `"route": "#/investigations/new"`; OD-INDY-13 (GUID routes); REQUIREMENTS.md:164 |
| Classification | **Permitted.** The effect was observed in 009, which has one long workspace page |
| Evidence | The catalog schema expresses fields, controls, actions and outcomes per screen. There is no list screen and no navigation or destination entries. Phase 4: the planner kept both screens on `#/investigations/new` "so I would not invent routes" |
| Shortcut | One page satisfies every screen |
| Why attractive | Fewer routes, no navigation state |
| Existing gate behaviour | `exists`/`visible` are per element; the route is not checked |
| Impact on user | No addressable workspace; no back-navigation model; poor iPad flow |
| Proposed correction | Proposed catalog additions (owner ratification):<br>- screens `investigation-list` `#/` and `investigation-workspace` `#/investigations/{id}`;<br>- INV-039 investigation list;<br>- INV-040 open an investigation from the list;<br>- INV-041 primary destinations;<br>- INV-042 investigation sections.<br>See PROPOSED-REQUIREMENT-CHANGES.md §2 |
| Executable verification | Dokimos `activate`/`outcome` on INV-040 records the route after opening. The R3 journey checks the hash changes and that Back returns to the list |
| Cost | Owner effort about 3 h. Tokens per rehearsal +40–80 k (implementing navigation that the intended app needs anyway). CI +1 min |
| Confidence | High |

### SA-05: Forma, shell and palette compliance is unverified

| | |
|---|---|
| Requirement refs | UI-016-O8/O9 ("Use Forma components for surfaces, controls, states and focus treatment"), UI-016-O16/O18, UI-034, UI-029; `v4.1.2/analysis/forma-layout-contract.json` FL-01..10 |
| Classification | **Observed** |
| Evidence | Neither rehearsal uses the UI-016 palette `#14283F`, `#087F8C`, `#E9A23B`, `#D66B58`, `#F5F3ED`.<br>009 `web/index.html`: 0 Forma custom elements and 4 distinct Forma classes (`ef-button`, `ef-form`, `ef-field`, `ef-choice-group`); `web/causa.css` has 25 hex colours, 4 bare (lines 6, 19, 30, 46).<br>008: 35 Forma elements, 22 Forma classes, 0 hex. UI-016-O16 "independent visual review" is bound to `playwright:acceptance/UI-016.spec.js`, written by the implementer, not to SU-REVIEW-RECORDS |
| Shortcut | Import Forma's stylesheet, use a few classes, hand-write the rest |
| Why attractive | Reproducing the shell from requirements alone (OD-INDY-22) is costly, and nothing measures it |
| Existing gate behaviour | No gate inspects markup, CSS, rendered tokens or screenshots |
| Impact on user | Generic, low-identity UI in the demonstration; inconsistent states and focus treatment |
| Proposed correction | A governed `design-contract` check in the gate, against the built `dist/` and the deployed page:<br>1. Required Forma shell elements are present on the list and workspace routes (`ef-site-header`, `ef-navigation-shell`, an `ef-status-lozenge` for save state).<br>2. No bare hex or rgb colour in application CSS outside a single theme-token file.<br>3. Each UI-016 palette colour is defined once as a Forma theme token and is rendered somewhere in a populated journey state.<br>4. The pre-written `layout-assertions.mjs` FL-01..10 runs in the governed step, not in an implementer spec.<br>5. Screenshots at four viewports are attached for the owner's visual review |
| Executable verification | Harness cases: the 009 `dist/` is refused (no shell elements; bare hex); a minimal fixture page with the shell and tokens passes |
| Cost | Owner effort about 1 day (an F# script using an HTML parser already in the toolchain, or Playwright `evaluate` in the governed step). CI +3 min. Tokens per rehearsal +60–120 k (building the shell properly) |
| Confidence | High |

### SA-06: The implementer writes the oracles

| | |
|---|---|
| Requirement refs | R9-VERIFICATION-DECISION-RECORD D3/D6; OD-INDY-22; CAUSA-INVESTIGATION.md step 2 |
| Classification | **Permitted.** Not observed being abused: 009's E2E decodes the GitHub document test-side, and its probes were detected |
| Evidence | 009 `probe.mjs` writes `expected`/`observed` pairs, and Dokimos judges equality. `.echelon/fault-probes.json` is chosen by the implementer and read from the candidate repo. Only SU-VERIFIER (2 obligations) is independent |
| Shortcut | (Hypothetical)<br>- `observed` read from the same in-memory source as `expected`, e.g. the input element after reload served from a cache;<br>- fault probes chosen to be ones the test already catches;<br>- one probe per category |
| Why attractive | Fewer flaky live steps; probes that always detect |
| Existing gate behaviour | Dokimos rejects placeholders, skips and stale SHAs, but cannot tell who computed `observed` |
| Impact on user | A content regression in a field no probe covers ships "verified" |
| Proposed correction | Owner-authored black-box journeys (R3) that drive the UI by accessible name and read GitHub directly. Plus governed **field-drop** probes: the gate mutates the *saved document* through a provider proxy (drops each content field in turn on read) and requires the journey to fail. That needs no knowledge of the implementer's source |
| Executable verification | Harness case: an app that reads values from `sessionStorage` after reload fails the governed journey |
| Cost | Owner effort about 1–1.5 days (journeys plus proxy). CI +4–6 min. Tokens 0 for the implementer (they must still make the app pass) |
| Confidence | Medium |

### SA-07: `--require-fault-sensitive content-round-trip` is inert

| | |
|---|---|
| Requirement refs | `tools/r9/verification-gate.yml:114-131`; FINAL-READINESS.md "What changed" and scenario 1 |
| Classification | **Permitted** (verified: 0 occurrences of `content-round-trip` in `v4.1.2/verification-execution-plan.json`, `v4/verification-plan.json` or the compiled log; 16 only in the withdrawn proposal) |
| Evidence | As above. The probe step also runs only `if [ -f .echelon/fault-probes.json ]` |
| Shortcut | Ship no fault probes at all; the governed gate does not notice |
| Why attractive | It saves the probe-building time (009 spent about 9 min locally plus CI) |
| Existing gate behaviour | Silent pass of the sensitivity condition |
| Impact on user | Sensitivity claims in readiness records are untrue for the governed gate |
| Proposed correction | Fail closed: `gate` refuses when a `--require-fault-sensitive` category matches zero obligations, or when the probe spec is absent in release mode. Once SA-02 lands, tag the catalog's save/reload/isolation obligations with `content-round-trip` |
| Executable verification | Harness case `require-fault-sensitive-unknown-category-refused` |
| Cost | Owner effort about 1 h. CI 0. Tokens 0 |
| Confidence | High |

### SA-08: Applicability facts are self-declared

| | |
|---|---|
| Requirement refs | Catalog `facts: ["domain-profile-selected"]`; INV-003 `appliesWhen` |
| Classification | **Observed (benign).** 009 records `domain-profile-selected: false` from its own probe, so INV-003 is NOT_APPLICABLE. That is correct for an app with no profiles |
| Shortcut | (Hypothetical) Declare a fact false to make a hard requirement not applicable |
| Why attractive | It turns a requirement into N/A at zero cost |
| Existing gate behaviour | Accepted as recorded |
| Impact on user | Low today (one fact), but it grows with every `appliesWhen` |
| Proposed correction | Facts are set in a governed file (`.echelon/r9-governance.json`, owner-pinned) or by the governed journey, never by implementer probes. A fact change needs an owner decision |
| Executable verification | Harness case: a probe-supplied fact that conflicts with the governed value is refused |
| Cost | About 1 h. CI 0 |
| Confidence | Medium |

### SA-09: No completion criterion; early stop behind a verified headline

| | |
|---|---|
| Requirement refs | KICKOFF-EXECUTION-PACKET §§7–9 (prose); R9-READINESS-HANDOFF §E (75 % target is prose) |
| Classification | **Observed** (009 stopped at about 115/420 min) |
| Evidence | FINAL-REPORT §1, §16 |
| Shortcut | Stop as soon as the reachable verifier is green |
| Why attractive | The compute constraint, plus no penalty for unused time |
| Existing gate behaviour | No time, coverage or stopping rule. Development mode passes with an empty scope |
| Impact on user | Five hours of available build time unused while P0 capabilities were missing |
| Proposed correction | A completion rule in the launch packet and checked by `scope-report` (SA-03): an agent may stop before T+390 only when either<br>(a) the governed gate passes; or<br>(b) every P0 group is ≥ 1 identity passing, or blocked by a filed owner-decision request naming the blocker.<br>A gate defect blocks only the obligations it actually blocks: the agent continues on the journeys (R3) and the design contract (R5), which run independently of the broken suites |
| Executable verification | `scope-report --at <time>` exits non-zero on the 009 tree at T+115 |
| Cost | Owner effort about 2 h (wording plus a flag on `scope-report`). Tokens per rehearsal: whatever the agent then spends on real work, budgeted inside the 7 h already planned |
| Confidence | High |

### SA-10: Failure paths verified only against a double; the live browser suite has no token mechanism

| | |
|---|---|
| Requirement refs | UI-015 ("mocked persistence MUST NOT be represented as live write verification"); SU-BROWSER-LIVE; R9-READINESS-HANDOFF §F.6 |
| Classification | **Observed** (009: 13 negative scenarios double-only, skipped in live) / **permitted** |
| Evidence | 009 `tests/browser/scenarios/content-negative.spec.mjs` runs against `tests/browser/support/github.mjs`. 009 did run a live browser E2E using the workflow `github.token` on its own `causa-data` branch, which shows that a token mechanism exists |
| Shortcut | Verify conflict and authentication failure only against a fake |
| Why attractive | Live failure is hard to provoke |
| Existing gate behaviour | A plan suite that cannot have a token cannot pass |
| Impact on user | Real-provider conflict and authentication behaviour is untested on the demonstration device |
| Proposed correction | Adopt 009's pattern in the successor runner package: SU-BROWSER-LIVE uses the workflow token against the competition repository's data branch. Add two live failure cases to the R3 journeys: a real conflict (two contexts) and a real 401 (a revoked fine-grained token held in a secret) |
| Executable verification | The governed live journey must record a GitHub `409`/`422` from a real conflicting commit |
| Cost | Owner effort about 3 h. CI +2 min |
| Confidence | High |

### SA-11: The demonstration catalog is dropped silently

| | |
|---|---|
| Requirement refs | EXT-DATA-013; UI-001-O1 ("exactly four"); README device-target demo |
| Classification | **Observed.** 009 has none; 008 replayed them in a .NET test, but no content was readable in the UI (AUDIT.md:55) |
| Shortcut | Omit the only part of the demonstration the judges see first |
| Why attractive | Its only test lives in SU-DOTNET-LIVE (SA-01) |
| Existing gate behaviour | Unrunnable |
| Impact on user | No Apollo / Challenger / lightbulb walkthrough |
| Proposed correction | R3 journey J2: Connect → the list shows the four titles → open one → at least one observation, hypothesis and evidence text from `causa-data` is visible |
| Executable verification | The journey itself, live, read-only |
| Cost | Owner effort about 2 h. CI +1 min. Tokens per rehearsal +30–60 k |
| Confidence | High |

### SA-12: Structural-only `ui-control` entries accept inert UI; the save indicator narrowed

| | |
|---|---|
| Requirement refs | INV-016 (verification `mapped, exists, visible, accessible` only); UI-006 (seven states); PERSIST-030..033 |
| Classification | Narrowing **observed** (009 has 6 states, without Offline or Unknown commit outcome, at `src/Causa.App/Update.fs:19-25`). Constant label **hypothetical** |
| Shortcut | A save-state element that always reads "Saved" |
| Why attractive | Zero state-machine work |
| Existing gate behaviour | The catalog accepts it. The plan would reject it via PERSIST-030..033, if runnable |
| Impact on user | False durability claims, which is the most serious trust failure for a persistence demo |
| Proposed correction | Add `outcome` to INV-016 with `validation.options` naming the seven states, and drive states in the R3 journey (offline via `context.setOffline`, failure via the proxy) |
| Executable verification | A Dokimos `outcome` per state, plus the journey |
| Cost | Owner effort about 1 h (catalog). Tokens per rehearsal +20–40 k |
| Confidence | Medium |

### SA-13: Catalog work items cannot complete under R9 governance

| | |
|---|---|
| Requirement refs | `r9.fsx` `inScope` / `emptyCauses`; `check-governance` |
| Classification | **Permitted** (verified by reading). Contributed to 009's R9-CONTENT-001 staying active |
| Shortcut | Skip Praxis work tracking (009 began its work item after the code) |
| Why attractive | The item can never complete anyway |
| Proposed correction | Part of SA-02: gate scope accepts INV-0nn requirement IDs from the governed catalog manifest |
| Executable verification | Harness case: a work item scoped to INV-024 with a passing catalog result completes |
| Cost | Included in SA-02 |
| Confidence | Medium |

### SA-14: The iPad Safari engine is not verified for content

| | |
|---|---|
| Requirement refs | README "Device target: iPad Safari"; SU-BROWSER-WEBKIT |
| Classification | **Observed** (009 `playwright.config.js:23` is chromium only) |
| Shortcut | Chromium mobile emulation stands in for WebKit |
| Why attractive | No WebKit install; fewer engine-specific bugs to fix |
| Impact on user | Safari-specific failures (e.g. OPFS ephemeral refusal, 008 CR-T33-F08) are found on stage |
| Proposed correction | The governed R3 journeys run in the `webkit` project at iPad 820×1180 and 1180×820 |
| Cost | CI +3 min (the WebKit install is cached). Owner effort included in R3 |
| Confidence | High |

### SA-15: Requirement conflicts and stale status lines

| | |
|---|---|
| Requirement refs | KICKOFF-EXECUTION-PACKET §6 cuts "visual polish beyond accessibility" first vs UI-016-O18 (P0, "A grayscale-only interface is a failed UI-016 acceptance even if functional tests pass"); UI-026 (P0) vs UI-002 (P1, unbound); UI-014-O2 vs unbound WASM-LOAD-*; v4.1.2 README "CANDIDATE"; CAUSA-INVESTIGATION.md "(proposed)" and "Tool releases: not yet available"; process-evidence rules "PROPOSED" though pinned |
| Classification | **Permitted** |
| Shortcut | Cite whichever document permits the cheaper choice |
| Proposed correction | See PROPOSED-REQUIREMENT-CHANGES.md §5 |
| Cost | About 1 h of document fixes (non-frozen documents only) |
| Confidence | High |
