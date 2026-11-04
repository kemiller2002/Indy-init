# Proposed requirement changes

**Every item here is a proposal for the owner.**
- None has been applied.
- No frozen package, catalog, gate, workflow or approval record was edited by this audit.
- Changes to frozen content are proposed only as successor packages through `tools/owner-freeze.fsx`.
- Catalog entries are proposed for ratification under the owner's requirements governance (OD-INDY-15, RDP-003).

## 1. Runner-only successor package (R1, for SA-01 and SA-10)

**Proposed decision ODR-V413-R1.** A successor to v4.1.2 that changes only `verification-execution-plan.json` `suites[].runner` for the suites below. Nothing else changes.

| Suite | v4.1.2 runner (verbatim) | Proposed runner |
|---|---|---|
| SU-OWN-DOTNET | `dotnet test tests/Causa.Contract.Tests tests/Causa.Domain.Tests tests/Causa.Persistence.Tests tests/Causa.App.Tests` | `dotnet test tests/Causa.Own.slnf` (solution filter written by the bootstrap) |
| SU-DOTNET-LIVE | `dotnet test tests/Causa.Acceptance tests/Causa.Invariants --filter "…Live.…"` | `dotnet test tests/Causa.Live.slnf --filter "…Live.…"` (same filter) |
| SU-BROWSER-LIVE | `npx playwright test --project=chromium tests/browser/live` | unchanged, plus the environment contract: `CAUSA_GITHUB_TOKEN=${{ github.token }}`, data branch `causa-data` of the competition repository |

**Acceptance of the package**
- `tools/r9/tests.fsx` gains `run-suites-on-bootstrap-tree`.
- That case must produce a result file for every executable suite on a freshly bootstrapped tree. Failing tests are acceptable; a missing result file is not.

## 2. Proposed catalog additions (R4, for SA-04 and SA-12)

**Screens**

Replace the duplicated route by distinct screens. This changes the `route` of a ratified screen, so it needs ratification:

```json
"screens": [
  { "id": "investigation-list",      "route": "#/" },
  { "id": "investigation-start",     "route": "#/investigations/new" },
  { "id": "investigation-workspace", "route": "#/investigations/{id}" }
]
```

**New entries**

These use only check kinds already in the ratified catalog. Sources are existing ratified or normative requirements; no new product behaviour is invented.

```json
[
  { "id": "INV-039", "type": "ui-control", "screen": "investigation-list", "name": "Investigations",
    "component": "list", "required": true, "visible": true, "editable": false,
    "verification": ["mapped","exists","visible","accessible","outcome","reload"],
    "sources": [{"document":"docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md","requirement":"UI-001"},
                {"document":"docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md","requirement":"UI-022"}],
    "notes": "After Connect, lists the connected repository's investigations by title. outcome: a saved investigation appears; reload: it is still listed in a new browser context." },

  { "id": "INV-040", "type": "action", "screen": "investigation-list", "name": "Open investigation",
    "component": "link", "required": true, "visible": true, "editable": false,
    "verification": ["mapped","exists","visible","accessible","activate","outcome"],
    "dependsOn": ["INV-039"],
    "sources": [{"document":"docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md","requirement":"UI-001"},
                {"document":"docs/OWNER-DECISIONS-2026-10-26.md","requirement":"OD-INDY-13"}],
    "notes": "activate: the route becomes #/investigations/{id}; outcome: the workspace shows that investigation's problem statement." },

  { "id": "INV-041", "type": "ui-control", "screen": "investigation-list", "name": "Primary navigation",
    "component": "navigation", "required": true, "visible": true, "editable": false,
    "validation": { "options": ["Investigations","Administration","Settings"] },
    "verification": ["mapped","exists","visible","accessible","validation","activate"],
    "sources": [{"document":"docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md","requirement":"UI-029"},
                {"document":"docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md","requirement":"UI-020"}],
    "notes": "Present on every screen. Each destination activates and marks itself current (aria-current)." },

  { "id": "INV-042", "type": "ui-control", "screen": "investigation-workspace", "name": "Investigation sections",
    "component": "navigation", "required": true, "visible": true, "editable": false,
    "validation": { "options": ["Overview","Causal Analysis","Evidence","Hypotheses","Timeline"] },
    "verification": ["mapped","exists","visible","accessible","validation","activate","outcome"],
    "sources": [{"document":"docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md","requirement":"UI-029"},
                {"document":"docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md","requirement":"UI-026"}],
    "notes": "outcome: each section shows content of the current investigation; Causal Analysis shows a text relationship list of the investigation's links. A section that only says it is not supported fails outcome." },

  { "id": "INV-043", "type": "ui-control", "screen": "investigation-workspace", "name": "Save state outcomes",
    "component": "status", "required": true, "visible": true, "editable": false,
    "validation": { "options": ["Saved","Unsaved","Saving","Offline","Conflict","Unknown commit outcome","Save failed"] },
    "verification": ["mapped","outcome","validation"],
    "dependsOn": ["INV-016"],
    "sources": [{"document":"docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md","requirement":"UI-006"},
                {"document":"docs/CAUSA-PERSISTENCE-REQUIREMENTS.md","requirement":"PERSIST-030"}],
    "notes": "Each state is driven (offline, provider failure, conflict) and its word observed. A constant label fails." },

  { "id": "INV-044", "type": "workflow-outcome", "screen": "investigation-workspace",
    "name": "A link is shown on both the evidence and the hypothesis",
    "required": true, "visible": true, "editable": false,
    "verification": ["mapped","outcome","reload"],
    "dependsOn": ["INV-030"],
    "sources": [{"document":"docs/OWNER-DECISIONS-2026-11-03.md","requirement":"OD-INDY-23"}],
    "notes": "Promotes the existing INV-030 note to a checked outcome." }
]
```

**Check before ratifying**
- Whether Dokimos 0.11.0 `componentMatches` accepts `list`, `link`, `navigation` and `status`. 009 found that it rejects `url-input` (FINAL-REPORT §13.1).
- If it does not, use a supported component kind. Do not add Dokimos features for this; the R3 journeys cover the behaviour either way.

**Also proposed:** add `content-round-trip` to the categories of the `save`, `reload` and `isolation` obligations, so `--require-fault-sensitive` has something to bind to (SA-07).

## 3. Governed gate changes (R2, R3, R5, R6) in `tools/r9/`

These are Indy-init tools, not frozen packages. Each change is listed with its refusal cause and the harness case that proves it.

| Change | Refusal cause (text) | Harness case |
|---|---|---|
| `gate --catalog-verification FILE` (release: required) | `catalog not accepted at candidate <sha>` | `gate-release-catalog-rejected-refused`, `gate-release-plan-refused-catalog-accepted-refused` |
| `--require-fault-sensitive` with a category that matches zero obligations | `fault-sensitivity category <c> matches no obligation` | `require-fault-sensitive-unknown-category-refused` |
| Release without `.echelon/fault-probes.json` | `release requires a fault-probe spec` | `gate-release-no-probe-spec-refused` |
| Applicability facts from the governed file only | `fact <f> recorded by a probe differs from the governed value` | `fact-from-probe-refused` |
| `--scope` accepts INV IDs from the governed manifest | — | `work-item-inv-scope-completes` |
| `journeys` step: run `tools/r9/journeys/*.spec.mjs` from the governed checkout against the candidate build, chromium + webkit | `journey <J> failed on <engine>` | `journeys-on-fixture-pass`, `journeys-on-content-only-app-refused` |
| `design-contract` step | `missing Forma shell element <e> on <route>`; `bare colour in <file>:<line>`; `palette value <hex> not defined as a theme token`; `palette value <hex> never rendered in a journey state`; `FL-<n> failed` | `design-contract-fixture-pass`, `design-contract-custom-css-refused` |
| `scope-report` (release: required) | `P0 group <g> has no written identity, passing journey or disposition` | `scope-report-content-only-refused` |

**Fixtures.** The fixtures for the `*-refused` cases are small synthetic trees written for the harness. The 009 tree is used only as a read-only manual check, never copied into Indy-init (OD-INDY-22).

## 4. Launch-packet and report wording (R7)

Proposed text to add to the Rehearsal 10 launch packet:

> **Priority.** Build in this order: journey J1, journey J2, the design contract, the catalog negative scenarios, the remaining v4.x P0 obligations, then P1.
>
> **Gate defects.** If a governed gate or suite cannot execute, record the defect with its exact command and error, file an owner-decision request, and continue on every obligation, journey and design check that does not depend on it. A gate defect never narrows the build scope.
>
> **Completion.** You may stop before T+390 minutes only when `r9.fsx scope-report` exits 0: every P0 group is done, partial, or blocked by a filed owner-decision request.
>
> **Reporting.** The report's first line is the governed gate's verdict at the release candidate. Any verdict from a workflow you wrote is labelled "implementer-run". Include the `scope-report` table verbatim, and list every P0 group not done, with its reason.
>
> **Budget.** The compute budget for this run is <N> CI minutes and <M> agent tokens. Use of the full time window is expected.

The owner chooses N and M. A stated number replaces the open "compute constraint" that 009 cited when it stopped.

## 5. Conflicts and stale lines (R8; non-frozen documents only)

| Location | Current | Proposed |
|---|---|---|
| `docs/KICKOFF-EXECUTION-PACKET.md` §6, cut order item 1 | "visual polish beyond accessibility" | "visual polish beyond accessibility **and beyond the design contract (UI-016-O18 and the governed design-contract check are not cuttable)**" |
| `docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md` UI-026 | P0 section "centered on the causal graph", while UI-002 is P1 | Add: "For P0, the causal graph may be the accessible text relationship list of UI-002 plus a `forma-workflow` rendering of hypothesis–evidence links; the interactive features of UI-002 remain P1." |
| UI-014-O2 | "MUST satisfy WASM-LOADING-EXPERIENCE-REQUIREMENTS.md" (all unbound) | Name the subset that is P0 (for example WASM-LOAD-001 loading state and WASM-LOAD-005 failure with Retry) |
| `requirements-compiled/v4.1.2/README.md:3` "Status: CANDIDATE" | Frozen package, so **do not edit** | Note the status in `AGENTS.md` (already correct), and in the successor package README |
| `docs/requirement-catalogs/CAUSA-INVESTIGATION.md` title "(proposed)" and §"Tool releases: not yet available" | Stale | "(ratified)"; replace the section with the pinned versions (Praxis 3.25.0, Dokimos 0.11.0, echelon-current 1.36.0) |
| `kickoff/rehearsal-009/process-evidence-rules.json` description "PROPOSED" | Owner-pinned | Owner action only (the digest is pinned; editing changes it) |
| OD-INDY-22 | "No rehearsal copies application code, tests, probes, tools or workflows from an earlier rehearsal" | Owner clarification: "Requirements-side verification assets in Indy-init (journeys, design-contract checks, layout assertions) are inputs, not copies." |

## 6. How to test the whole set before the next rehearsal

| Check | Expected |
|---|---|
| Readiness harness `dotnet fsi tools/r9/tests.fsx` with the new cases | All pass |
| Governed gate in release mode on a scratch fixture that implements only the catalog | REFUSED; causes name J2, the design contract and scope-report |
| Governed gate on a fixture that implements J1–J3, the shell and the tokens | Passes the journeys and the design contract (plan obligations as written) |
| Manual, read-only: journeys and design contract against the 009 deployed site | J2 and the design contract fail; J1 partly passes |
| Offline suite | Passes |

**Estimated total**
- Owner-side work: about 3–4 engineer-days.
- Release-run CI: about +12–15 minutes per candidate.
- Rehearsal agent tokens: about +250–450 k. This is spent building P0 features the intended application already requires, inside the existing 7-hour window.
