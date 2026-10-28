# R5A-001: Rehearsal 5 implementation audit — follow-up requirements

Priority: P0 before Rehearsal 6 starts; no change to Causa product requirements except where an owner decision is requested.
Owner system: Praxis (intake, work items). Decisions belong to the owner.
Intake: Praxis document ingestion through Indy-init's legacy `input-documents/` path (Indy-init is not Praxis-initialized; see EII-001). This document is source material, not a canonical requirement.

Evidence: [`docs/rehearsal-005-implementation-audit/`](../docs/rehearsal-005-implementation-audit/README.md).

## Constraints

- Rehearsal 5 (`indy-init-rehearsal-005`) is not modified, and its verdict stays INCOMPLETE / NOT ACCEPTED.
- F# for new executable implementation; no new Python; no GitHub Issues; no new frameworks where an existing component can be extended.
- Items that overlap EII-001 or EII-C are named against them, not duplicated.

## Requirements

| ID | Target | Requirement | Audit finding |
|---|---|---|---|
| R5A-001-01 | Limen app template / Conditor scaffold | The mocked browser suite's network guard admits exactly the configured deployed origin when `CAUSA_BASE_URL` is set, so the production suite can run against the deployed exact SHA. A negative control shows any other host still fails. | The deployed mode is blocked by the guard; UI-015-O3, WDL-014-O1, PF-014-O2 and UI-016-O15 were never run on the deployed SHA. |
| R5A-001-02 | Conditor scaffold, Dokimos | Fixture repositories (causa-data) are checked out at a pinned commit in every CI job that reads them, browser and live jobs included. | The browser and live jobs used `ref: main`; the .NET jobs pinned 3d861bc. |
| R5A-001-03 | Conditor scaffold, Dokimos | The application repository pins the .NET SDK (global.json, `rollForward: disable`), and CI installs exactly that SDK and workload. A re-run comparison reports artifact differences as product change or toolchain drift (AR-011-O2). | 198 of 263 artifacts differ when 1b9fb56 is rebuilt with SDK 10.0.402. |
| R5A-001-04 | Dokimos traceability; Indy-init plan checker (extends EII-C-04) | Obligations whose tags a review removed are listed as unbound-after-review until a reviewer re-binds them to a later fix's tests or records why not. | EXT-DATA-006-O2, EXT-DATA-005-O1 and PF-009-O3 stayed unbound after 5336ec4 although their fixes' tests passed. |
| R5A-001-05 | Indy-init requirements compiler | Aggregate obligations that restate other obligations (UI-015-O2) are compiled as derived from their constituents, not bound to tests directly. | UI-015-O2 lists about 12 behaviours, each proven under another VPO. |
| R5A-001-06 | Indy-init preflight; Dokimos evidence bundle | Preflight results and pre-run records (PRE-TOOL-02, PRE-DEP-01, registry pin and checked time, Praxis inbox package, approved-exclusion record) are bindable producers in the run's evidence bundle. | AR-023-O3, VER-002-O1, VER-004-O3 and UI-040-O2 had evidence at the deadline that the bundle could not carry. |
| R5A-001-07 | Praxis, Ordo | The last independent review runs on the release candidate SHA, and the reviewer's identity and role are recorded by Praxis (consumed by `verify-process`). | Both R5 reviews predate 1b9fb56; independence is self-asserted. |
| R5A-001-08 | Owner, then Causa application if needed | Reconcile owner decision DEC-025 (OD-INDY-10: no anonymous mode; permission fields advisory) with PF-015-O1 (catalog tests without a PAT), PF-015-O2 (a least-privilege, read-only test secret) and EXT-DATA-005-O1 (check repository and branch permissions). Either amend those obligations, or reverse the decision and implement the behaviour. | `Credential.create` refuses a blank token (F-3); `RepositoryInfo.CanPush` is parsed but never used. |
| R5A-001-09 | Causa application | Focus is placed deliberately after a back navigation or route transition (UI-011-O4). | Post-rehearsal probe: scroll is restored, focus falls to body. |
| R5A-001-10 | Causa application, owner | Amber non-text indicators meet 3:1 against their surface, or the owner records that text-paired indicators are outside UI-016-O11. | #E9A23B borders measure 1.95:1 on ivory and 2.17:1 on white. |
| R5A-001-11 | Owner | Decide: whether UI-031-O5 role/access control applies to a single-user browser app; whether AR-019-O3 is met by INVALID/FAIL for Rehearsal 3; and DEC-025 versus PF-015 and EXT-DATA-005 (R5A-001-08). | Three plan or scope ambiguities that no implementation can resolve. |

## Acceptance for this intake

- Each item has a derived record that names this input in `derived_from`.
- Owner decisions (R5A-001-11) are recorded by the owner, not transcribed by an agent (RDP-003).
- Items for other repositories are routed through each owner's documented Praxis input path.
