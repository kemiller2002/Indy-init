# Owner decisions, 2026-10-24

**Decided by:** kemiller2002 (Kevin Miller), the repository owner, on 2026-10-24 at 21:09 local time. His words were:
"We need writes", "Release it" and "Do what you recommend".

**Recorded by:** an agent session (Claude Code). The agent wrote this record; it made none of the decisions.
OD-INDY-1 and OD-INDY-2 are the owner's own words. OD-INDY-3 to OD-INDY-8 are recommendations that the owner accepted
as a whole. Each of them cites its source.

**What they answer:**
- the operator decisions requested in kemiller2002/indy-init-rehearsal-003
  `research/decisions/DF-CAUSA-2026-0003--operator-decisions-requested.md`, items 1–4, plus the baseline-scope question
  of that rehearsal's remediation report §3, item 5;
- the readiness blockers in kemiller2002/cohort-based-engineering
  `research/consolidation/2026-10-24/indy-init-readiness.md`.

These are decisions for the **Indy Init event** (2026-11-07) and for every rehearsal before it.

## Decisions

| ID | Decision | Source |
|---|---|---|
| OD-INDY-1 | **Writes are P0 for the event.** DF-CAUSA-2026-0001 (Rehearsal 3 read-only scope) is **rejected** as a precedent. It was an agent-authored decision with no operator approval, and it does not carry into any later rehearsal or the event. EXT-DATA-006 and EXT-DATA-009 (create, read, write, reload, conflict) and the 52 write-path requirements of the R3 reconciliation (§7.2) are in scope | Owner: "We need writes" |
| OD-INDY-2 | **Percepta semantic UI verification is released, not waived.** Waiver PERCEPTA-SEMCLI-001-W1 is **not granted**. `kickoff/indy-init.kickoff.json` keeps Percepta `required: true`. UI completion requires evidence from the released semantic CLI, pinned by the kickoff release set | Owner: "Release it" |
| OD-INDY-3 | **First launch shows an explicit Connect data repository action** (EXT-DATA-002, option a). There is no auto-load of a public catalogue. A fresh launch shows an accessible, empty, disconnected workspace. Choosing the public historical catalogue is an explicit user choice inside Connect. Writes need a user-chosen, authorized repository, so auto-loading would contradict EXT-DATA-006 and EXT-DATA-009 | DF-CAUSA-2026-0003 item 2 |
| OD-INDY-4 | **GUID routes, with case keys as aliases** (option a). URLs follow SCREEN-CONTRACTS §url-contract and REQUIREMENTS R9.9. `#/investigations/<caseKey>` redirects to the GUID route, and is never the identifier that gets stored | DF-CAUSA-2026-0003 item 3 |
| OD-INDY-5 | **Acceptance baseline scope:** the Causa rehearsal documents, kept in `requirements/causa-requirements-baseline.json`. These are the 16 documents of indy-init#12, plus three documents the owner added on 2026-10-24: WORKFLOW-DIAGRAM-INTEGRATION (WDL), SYSTEM-VERSION-AND-COMPATIBILITY (VER) and AGENT-RELIABILITY (AR), which REQUIREMENTS.md makes normative for future rehearsals. The long-term platform documents (PRODUCT-REQUIREMENTS, INVESTIGATION-SEMANTICS, CORE-DOMAIN-MODEL, POLICY-ENGINE, PERSISTENCE-CONTRACT, SECURITY-THREAT-MODEL and similar) stay **normative context** that is read before coding. They are not acceptance obligations for the event | remediation report §3 item 5; readiness "freeze scope" |
| OD-INDY-6 | **Requirements freeze.** The baseline is regenerated mechanically (`tools/build-requirements-baseline.py`) whenever requirements change. Two freezes apply: (1) **Rehearsal 4 kickoff:** the baseline is pinned at the Indy-init commit current at kickoff, and from then on only owner commits change it; (2) **event freeze, 2026-11-05 23:59 America/Indiana/Indianapolis:** after this, no P0 requirement is added or changed except by an owner commit naming a Rehearsal 4 finding. The event pins the exact commit at kickoff (`kickoff/indy-init.kickoff.json` `refPolicy: pin-exact-commit-at-kickoff`) | readiness "freeze scope" |
| OD-INDY-7 | **The acceptance verdict is computed, never written.** `tools/compute-acceptance-verdict.py` derives it from the ledger, checks evidence at the SHA, and fails CI when the ledger's `accepted` flag or the report disagrees (REHEARSAL-ACCEPTANCE-LEDGER.md). Agents never set ratification fields (`status: accepted` on decisions or exclusions). Approval files are written by the owner only (REHEARSAL-DERIVATION-POLICY RDP-003; AGENT-RELIABILITY AR-003) | consolidation priorities item 1 |
| OD-INDY-8 | **Kickoff release set.** Per VER-001..004: the newest stable, compatible, verified release of each Echelon system, approved in the Registry **after** Conditor qualification. The set that is expected is the current echelon-current plus Dokimos 0.7.0 (attested completion needs it alongside Praxis ≥ 3.17), the Percepta semantic CLI release (OD-INDY-2), and a Conditor release with the SD-1 fix. **Rehearsal 4 runs on exactly that set.** If a component fails qualification, the set keeps the newest compatible release, with the exception recorded (VER-003) | readiness "close stack defects"; VER-001 |

## Consequences

- **Rehearsal 3 stays NOT ACCEPTED.** Its read-only scope is rejected, and its Percepta evidence came from an unreleased
  build. This record does not reopen it.
- **Rehearsal 2's self-declared acceptance does not meet this repository's ledger contract.** Its ledger is INVALID
  under `tools/validate-acceptance-ledger.py`: defect classes are `CLOSED`, which is not an allowed status, and 51 P0
  rows are PASS without tests. P0 ADRP-016 (competition compliance) is NOT_RUN. The computed verdict is therefore not
  ACCEPTED (`tools/test-acceptance-verdict.py`). R2 accepted itself with its own tool and predicate. Its engineering
  results stand; its acceptance does not carry over as precedent.
- **Rehearsal 4 is required.** It is the write-path dress rehearsal on the kickoff set
  ([REHEARSAL-004-WRITE-PATH-PLAN.md](REHEARSAL-004-WRITE-PATH-PLAN.md)).
- **Upstream work** this record depends on:
  - Percepta semantic release (PERCEPTA-SEMCLI-001);
  - Conditor SD-1 release (conditor#108);
  - Registry Dokimos 0.7.0 and pairing (REG-PAIR-001);
  - Praxis preservation hardening (REQ-PRES-001).
