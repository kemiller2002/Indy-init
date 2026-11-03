# Causa investigation requirement catalog (proposed)

[`requirements/catalogs/causa-investigation.requirement-catalog.json`](../../requirements/catalogs/causa-investigation.requirement-catalog.json)
(`CAT-CAUSA-INVESTIGATION`, schema `praxis.requirement-catalog/1`) gives the
investigation creation and workspace requirements stable, field-level IDs:
`INV-001` to `INV-018`. Praxis compiles it into the verification manifest that
Dokimos verifies Causa against. See kemiller2002/praxis
`docs/requirement-verification-manifest.md` and kemiller2002/dokimos
`docs/requirement-verification.md`.

**Status: ratified** by the owner: INV-001 to INV-018 in commit `0ab8ffd`, and
INV-019 to INV-038 in commit `7cefdc6` (PR #98, OD-INDY-19..21 and OD-INDY-23).
An agent wrote the catalog. It is not a frozen package. It adds no obligation the cited sources do not state. Each
requirement names its authoritative sources (`sources`). Ratifying it, or
changing a requirement, goes through the owner's requirements governance
process (OD-INDY-15, RDP-003), never through an agent's edit made to turn a
build green.

## Reconciliation with the authoritative requirements

| ID | Requirement | Type | Authoritative source |
|---|---|---|---|
| INV-001 | Problem statement | ui-field | S01 "original problem statement field"; R1.1 |
| INV-002 | Title (optional) | ui-field | S01 "optional title" |
| INV-003 | Domain or profile indicator | ui-control | S01 "domain/profile indicator if selected". Applies only when `domain-profile-selected` is true |
| INV-004 | Start investigation | action | S01 "Start Investigation action" |
| INV-005 | Persistence destination indicator | ui-control | S01 "persistence destination indicator before remote write" |
| INV-006 | Investigation identifier | ui-field | R1.3. The UUID pattern is the identifier format the application already enforces |
| INV-007 | Creation timestamp | ui-control | R1.3 "stable identifier and creation timestamp" |
| INV-008 | Original problem statement | ui-control | S01 completion evidence "original text preserved"; R1.2; A96 |
| INV-009 | Record a hypothesis | action | R2.1; S02 hypotheses |
| INV-010 | Hypotheses | ui-control | S02 |
| INV-011 | Hypothesis explanation | ui-field | S04 "what it explains"; R2.1 |
| INV-012 | Evidence original content | ui-field | S05 "original content"; R3.1 |
| INV-013 | Evidence | ui-control | S02; UI-005 |
| INV-014 | Record an observation | action | S02; R1.1 |
| INV-015 | Save | action | PERSIST-007; R10.1 |
| INV-016 | Save state | ui-control | UI-006; PERSIST-030 |
| INV-017 | Reopening restores the saved investigation | workflow-outcome | R10.7; PERSIST-021 |
| INV-018 | Investigation state | ui-control | S02; R2.2 |

## Open questions for the owner

These are deliberately **not** in the catalog. No authoritative requirement
states them, and the agent contract forbids inventing them.

1. **Is a problem statement required to start an investigation?** S01 lists
   the field. R1.1 says a user "must be able to begin" with one. Neither says
   an empty statement must be refused. INV-001 therefore has no `minLength`.
2. **Length limits** for the problem statement and the title. None are
   stated, so the catalog defines no `maxLength`.
3. **A Cancel action** on investigation start ("exit without unintended
   changes"). No authoritative requirement defines one, so none is in the
   catalog.
4. **Hypothesis editing and evidence association** (R2.3, R3.3, S04, S06). The
   catalog covers the entry fields (INV-011, INV-012). The relationship
   controls (support, contradiction, discrimination, insufficiency) need their
   own requirements once their presentation is decided.

## What verification found in Causa (indy-init-rehearsal-008)

Implementing and verifying the catalog against Causa showed that Rehearsal 8's
IF41-DOMAIN `EventPayload` carries only the event kind. The problem statement,
title and creation timestamp therefore have nowhere to be saved there. INV-001,
INV-002 and INV-008 fail their lifecycle obligations, and INV-007 fails
structurally.

This is not a new decision. ODR-V412-02 (package v4.1.2, approved and tagged
`indy-init-requirements-v4.1.2`) already restores the command and event bodies
of FSHARP-TYPE-SHAPES sections 31–33, for example `InvestigationStarted of
InvestigationStarted`. Rehearsal 8 was built before v4.1.2. An implementation
on v4.1.2 can save these values, and the verifier will tell whether it does.
Editing the catalog would only hide the gap.

## Using it for the November 7 build

**Every rehearsal starts from the requirements alone (OD-INDY-22).** Nothing
is copied from an earlier rehearsal: no application code, tests, probes, tools
or workflows from Rehearsal 8 (kemiller2002/indy-init-rehearsal-008) or its
reference PR #57. The steps below are written inside the timed window from
this catalog, the frozen packages and the released Echelon tools. An earlier
version of this section said to copy Rehearsal 8's probes, mutation tool and
workflow. That conflicts with the contest rules and is withdrawn.

**Tool releases: not yet available.** The pipeline needs
`praxis requirements manifest` and `dokimos verification requirements`. Both
are merged on `main` (kemiller2002/praxis #301, kemiller2002/dokimos #65) but
**not released**. The Rehearsal 9 toolchain pins Praxis 3.24.0 and Dokimos
0.10.0 (echelon-current 1.35.0; kickoff/rehearsal-009/toolchain.lock.json),
and neither carries these commands. Until both are released, registered in a
new echelon-current set and pinned in the lock, a rehearsal cannot compile or
verify this catalog with the approved toolchain. That is an owner decision
(docs/rehearsal-009-content-readiness/FINAL-READINESS.md).

Once the tools are released and pinned, the application built at kickoff
adopts the pipeline as follows:

1. Put `data-requirement-id="INV-0nn"` on the element of each implemented
   requirement, and list the files in
   `requirements/requirement-implementation-map.json`.
2. Write the requirement probes, the mutations and the verification workflow
   in the rehearsal's own repository, from the released tools' documentation
   (kemiller2002/praxis `docs/requirement-verification-manifest.md`,
   kemiller2002/dokimos `docs/requirement-verification.md`). The workflow
   compiles the manifest from this catalog at a pinned Indy-init commit, so the
   application cannot change its own obligations.
3. Commit the run's `requirement-verification.json` to
   `evidence/requirement-verification/requirement-verification.json` on the
   application's `main`. Specula reads that file, not the CI artifact.
4. Implement v4.1.2's command and event bodies (ODR-V412-02), so that the
   problem statement, title, creation time and every content field are saved.

The content entries (INV-019 to INV-038, OD-INDY-19..21 and OD-INDY-23) are described in
[CAUSA-INVESTIGATION-CONTENT.md](CAUSA-INVESTIGATION-CONTENT.md).
