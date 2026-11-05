# Forma and UI patterns

Authoritative sources, which this file does not restate:
- `kickoff/rehearsal-010/design-contract.json`: Forma roles, palette tokens, the theme boundary, and FL-01..10 at 390, 820×1180, 1180×820 and 1440;
- `requirements-compiled/v4.1.5/analysis/forma-layout-contract.json` and `layout-tests/assertions.mjs`;
- AGENT-RULES "Use only published Forma";
- Forma `docs/CONSUMING-FORMA.md` and `docs/reliability/indy-init-compatibility.md`.

## Classification of the recurring UI defects

| Defect | Evidence | Class | Where it is fixed |
|---|---|---|---|
| Long text escapes cards, panels and dialogs; flex items in `.ef-cluster`/`.ef-actions` refuse to shrink | Forma 0.12.0 failed 48 of 78 containment checks (HF-080) | **Genuine Forma defect** | Fixed upstream in Forma 0.13.0 (`0ffb077`, LRP-001) and `515c262` (marketing). Applications must upgrade, not patch. |
| Text escapes rounded pills | R5 `.causa-tag` with radius 999px, Forma 0.9.0 (HF-081) | Forma defect, fixed upstream in 0.10.0 (LCE-001, radius bounded by padding) | Upgrade; never redefine pill radius in application CSS |
| Marketing heading scale on application screens | R5 override `f207cd4` (HF-019, HF-082) | Missing Forma capability, delivered upstream | `data-ef-type` roles in 0.11.0 (`9c1d48e`) |
| Application re-implements forms, cards, nav and `[aria-invalid]` styling | R9 `web/causa.css` (HF-084) | **Genuine Forma gap** for validation-state styling (open in Forma `catalog/known-issues.json`); the rest is application bypass | Upstream proposal below; application uses `ef-field`, `ef-form-row`, `ef-navigation-shell` and `ef-status-lozenge` |
| Preview app re-adds `overflow-wrap:anywhere` on Forma 0.10.0 | HF-085 | Application workaround hiding an old pin | Upgrade to 0.13.0 and delete the overrides |
| `.ef-dashboard-grid`, `.ef-split-pane` and `.ef-operation-status` overflow at iPad portrait with 200% text | HF-083 (open in 0.13.0) | **Genuine Forma defect, still open** | Upstream; until then, avoid these three patterns on Causa screens at the 820×1180 breakpoint (FL checks catch it) |
| Amber status outline at 1.95:1 non-text contrast | HF-020 (R5, R6) | Application requirement plus a missing Forma outline token | Use `--ef-color-status-warning` with a text label (ODR-V41-05). The outline token is an upstream request. |
| Editable field loses typed text | HF-004 (R3 twice, R4 test could not fail) | Application binding error | CV-18: bind on `input` through the field map |

## Approved patterns (CV-17, MANDATORY; CV-18, PREFERRED)

1. **Pin Forma 0.13.0 or later.** CK-04 checks every `package.json` (`conventions.fsx check --forma-min 0.13.0`). This needs owner approval: v4.1.4 and v4.1.5 reference the 0.12.0 inventory, the Conditor 0.10.18 scaffold pins 0.12.0, and ODR-R10-05 allows 0.12.x or 0.13.x. See PROPOSED-REQUIREMENTS-CHANGES.md PRC-02.
2. **Layout comes only from Forma patterns named in the layout contract.**
   - **Flex and grid:** `.ef-stack`, `.ef-cluster`, `.ef-actions`, `.ef-grid` and `.ef-responsive-grid`. Since 0.13.0 they contain their children.
   - **Forms:** `.ef-form-row` and `.ef-field`, which share one control height (0.7.0) and have native date/time sizing (0.6.1).
   - **Scrolling:** `.ef-bounded-overflow` is the only sanctioned scroller.
3. **Application CSS declares no `--ef-*` property.** This is already an AGENT-RULES rule, checked by the design contract. Application CSS also adds no `overflow-wrap`, `word-break`, `border-radius`, `min-width:0` or `[aria-invalid]` rules on Forma classes; those are Forma's job.
   - **Exceptions:** remedies recorded in the layout contract, such as links of at least `--ef-layout-target-min`.
   - **Why no check is claimed:** this part is not machine-checked here, because a selector-level CSS lint would duplicate the design-contract check with a high false-positive risk. The design-contract check and FL-01..10 are the enforcement.
4. **Long labels and validation messages:** `ef-validation-message` and `ef-validation-summary`. Messages wrap; they are never truncated with ellipsis on a required field.
5. **Loading, empty and error states** use `ef-operation-status` and `ef-alert`. For `ef-operation-status` see the open defect above; give it the full row width at the 820 breakpoint.
6. **Form submission.** One primary action per form, in `.ef-actions`. Submit is disabled while a save is in flight, and the save state comes from the save machine, never from the button.
7. **Keyboard and accessibility.**
   - axe-core 4.14.0 is locked (ODR-V41-09).
   - Focus scrolling is fixed in 0.13.0.
   - Every status colour carries a text label (ODR-V41-05).

## Proposed upstream Forma change (not applied; Forma is not modified by this work)

Two items, with evidence and tests:

1. **Validation-state styling for `ef-field`.** Style `[aria-invalid="true"]` and `[aria-describedby]` messages for text-field, textarea, date-time-field and select. This is the known issue in `catalog/known-issues.json`, and R9 re-implemented it locally (HF-084).
   - **Fixture:** each control with `aria-invalid="true"` and a 120-character message at 320 px and 200% text.
   - **Assertions:** the message box stays inside the field box, the border uses the danger token, and the non-text contrast is at least 3:1.
2. **Container-query reflow for `.ef-dashboard-grid`, `.ef-split-pane` and `.ef-operation-status`.** These currently reflow only on viewport media queries (HF-083).
   - **Fixture:** each pattern in an 820 px-wide container at 200% text.
   - **Assertion:** the LRP-001 containment assertions pass.

Both belong in Forma's own reliability suite (LRP fixtures), not in Indy-init.

## UI conventions not adopted

- **A per-application CSS lint for overrides:** not adopted, for the false-positive reason above.
- **A mandatory per-screen visual review in the competition:** it already exists as the owner's screenshot review in ODR-R10-05. Adding another would be duplicate cost.
