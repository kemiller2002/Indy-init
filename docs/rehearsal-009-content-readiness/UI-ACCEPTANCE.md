# Rehearsal 009 content readiness: mobile-first UI acceptance

**Status:** PROPOSED. It is the user-visible half of CONTENT-001 … CONTENT-012 (AMENDMENT-DECISION.md).

**Platform.** It is built with Forma components and the Echelon Foundry design system, through Limen. It adds no custom component library, no JavaScript application framework and no Bolero.

**Navigation.** The existing navigation model is kept: the secondary navigation of UI-029-O2 (Overview, Causal Analysis, Evidence, Hypotheses, …).

**Viewports.** Layouts are checked at the viewports v4.1.2 already defines in `requirements-compiled/v4.1.2/analysis/forma-layout-contract.json`:
- VP-320 (320×640, "narrow phone");
- the iPad 820×1180 and 1180×820 viewports;
- desktop 1440×900;
- 200% text.

One more phone width, 390×844 (iPhone), is added for the content forms.

## Screens

### Create investigation
- **Fields:**
  - **Investigation name** (required);
  - **Description** (multi-line, optional);
  - **Primary question** (optional).
- **Primary action:** "Create investigation". **Secondary:** "Cancel".
- **Validation:**
  - an empty name shows a field-level message ("Enter a name") and disables nothing else;
  - the error is announced to assistive technology.
- **After Create:**
  - the investigation opens with its save indicator at **Unsaved**, then **Saving**;
  - it reads **Saved** only after the save protocol confirms the read-back (PERSIST-007-O1, UI-024-O4);
  - a local draft is never described as saved.
- **The identifier** is shown only as secondary, discoverable information (UI-022-O5), never as the heading.

### Investigation workspace
- **Header:** the investigation name as the page heading, the description and the primary question, an **Edit details** action, and the save indicator.
- **Secondary navigation** (UI-029-O2), each with a count:
  - Overview;
  - Observations;
  - Hypotheses;
  - Evidence;
  - Causal analysis;
  - Conclusions;
  - Timeline/History.

  On a phone it is a horizontally scrollable tab strip or a single menu button. It never wraps controls out of their container.
- **Overview** shows investigation information first: name, description, question, the latest observations, the hypotheses with their linked evidence, and the conclusion if one exists. Internal command names, event kinds and engineering diagnostics are never the primary content (UI-022-O4).

### Content lists and entry
Each of Observations, Hypotheses, Evidence and Conclusions has:
- an obvious **Add** action;
- list items that show the entity's **own content**:
  - the first lines of its text or statement;
  - for evidence, the label and source;
  - never "Observation 2 (version 3)".

**Hypotheses** are visually distinct from observations and conclusions. They carry a "Hypothesis" label and a distinct Forma tone, and are never shown as established fact (UI-025-O4).

Each editor has:
- **Observation:** Text (required, multi-line), Notes, Source reference.
- **Hypothesis:** Statement (required), Explanation.
- **Evidence:**
  - Label (required);
  - Description (required, multi-line);
  - Source reference;
  - Source URL (validated; an invalid URL shows a field error and is never rendered as a link);
  - Date/context.
- **Relationship:** opened from evidence or from a hypothesis. It has:
  - a chooser of existing hypotheses or evidence (no free-typed IDs);
  - the kind: **Supports**, **Contradicts**, **Relevant (no direction)**;
  - an optional rationale.
- **Conclusion:** Statement (required) and linked evidence. A notice explains that recording a conclusion does not resolve the investigation, and resolution still shows its existing gate.

**Every editor:**
- has **Save** and **Cancel**;
- can be reopened from its list item to read the full content and choose **Edit**;
- keeps the same entity identity on edit;
- shows the edit in the Timeline as a revision, with the earlier text available.

### Timeline/History
- Shows events in human language with actor and time (UI-003-O1). For example: "Observation recorded by kemiller2002 at 14:02". A revision links to the before and after text.

## Layout and accessibility requirements (checked at 320 and 390 px)

These apply UI-011, UI-016 and UI-004-O5 to the content forms:
- every field has a visible label tied to its control, and required fields are marked in text, not only by colour;
- the focus state is visible on every control; tab order follows the visual order; editors can be operated by keyboard alone;
- touch targets are at least 44×44 px (UI-004-O5);
- long text wraps inside its container; no control overflows its rounded container or the viewport; there is no horizontal page scroll;
- error messages are associated with their field (`aria-describedby`), and a save failure or conflict is announced;
- contrast is WCAG AA (UI-016-O11).

## What must not happen
- A screen that renders, or a command that is available, does not count as the capability.
- No list shows only counts, sequence numbers or event kinds where content exists.
- "Saved" is never shown before verified persistence.
- No content is placed in a URL (R9.9-O1, SCREEN-CONTRACTS§url-contract-O6).
