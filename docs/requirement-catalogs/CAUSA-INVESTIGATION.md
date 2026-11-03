# Causa investigation requirement catalog (proposed)

[`requirements/catalogs/causa-investigation.requirement-catalog.json`](../../requirements/catalogs/causa-investigation.requirement-catalog.json)
(`CAT-CAUSA-INVESTIGATION`, schema `praxis.requirement-catalog/1`) gives the
investigation creation and workspace requirements stable, field-level IDs:
`INV-001` to `INV-018`. Praxis compiles it into the verification manifest that
Dokimos verifies Causa against. See kemiller2002/praxis
`docs/requirement-verification-manifest.md` and kemiller2002/dokimos
`docs/requirement-verification.md`.

**Status: proposed.** An agent wrote it. It is not ratified, and it is not a
frozen package. It adds no obligation the cited sources do not state. Each
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

Implementing and verifying the catalog against Causa showed that the frozen
IF41-DOMAIN `EventPayload` carries only the event kind. The problem statement,
title and creation timestamp therefore have nowhere to be saved. INV-001,
INV-002 and INV-008 fail their lifecycle obligations, and INV-007 fails
structurally. This needs an owner decision on the investigation data
contract. Editing the catalog would only hide it.
