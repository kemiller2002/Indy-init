# R7-REM: Rehearsal 6 remediation and Rehearsal 7 readiness

Priority: P0
Owner systems: Arca (GitHub adapter and offline library), Forma (accessibility), Conditor (CI policy, templates), Praxis (starter workflow, autonomy), Indy-init (verification tooling, readiness).
Intake: Praxis document ingestion. Indy-init has no Praxis installation (`.praxis/`), so this file uses the legacy `input-documents/` path, as UAE-001 and ESO-001 do. It is source material, not a canonical requirement. No Praxis state is written or implied by this file.

**Status: proposed.** Rehearsal 7 is not started by this document; starting it stays the owner's decision.

## Source

The owner's script "Rehearsal 6 remediation and Rehearsal 7 readiness" (2026-10-29), and the evidence in [`docs/rehearsal-007/REMEDIATION-BASELINE.md`](../docs/rehearsal-007/REMEDIATION-BASELINE.md).

## Work packages

The work packages are tracked in [`docs/rehearsal-007/work-packages.json`](../docs/rehearsal-007/work-packages.json) and [`REMEDIATION-PLAN.md`](../docs/rehearsal-007/REMEDIATION-PLAN.md). Each has a canonical work item in the repository that owns the behaviour:

| Package | Canonical work items |
|---|---|
| R7-REM-001 cross-tab persistence | arca WI-0038 |
| R7-REM-002 offline retry and save state | arca WI-0037 |
| R7-REM-003 credential separation | owner action (no work item: credentials are owner-held) |
| R7-REM-004 CI concurrency | conditor WI-0063, praxis WI-0106 |
| R7-REM-005 unattended PR execution | praxis WI-0104, conditor WI-0062 (built before this initiative) |
| R7-REM-009 Forma contrast | forma WI-0041 |
| R7-REM-006, -007, -008, -010, -011, -012 | this intake (Indy-init has no work-item queue) |

## Requirement amendments requested for Rehearsal 7 (owner decisions)

The changes below alter what Rehearsal 7 must satisfy, so under RDP-003 they are **requests**. They become requirements only through the owner's approval records, as part of the v4 package cut from baseline `BL-CAUSA-INDY-INIT-7d44f5546bcd` ([READINESS-ASSESSMENT.md](../docs/rehearsal-007/READINESS-ASSESSMENT.md) K3).

**R7-AMD-01 and R7-AMD-02 are withdrawn.** The owner approved [ADR-R7-001](../docs/rehearsal-007/PERSISTENCE-ARCHITECTURE-DECISION.md) (merged in #41 while this intake was in progress): each tab keeps session-local state, GitHub is the only durable store, and there is no browser storage for investigations, no offline queue and no cross-tab locking. That removes the shared store and the queue these two requests were about. They are kept below, struck through, so the record shows what was asked.

| ID | Request | Why |
|---|---|---|
| ~~R7-AMD-01~~ (withdrawn: ADR-R7-001) | Causa's device persistence MUST use Arca's queue (`LimenQueue` with `Order = [IndexedDb]`, or a declared memory-only mode that the page shows), `OfflineSync.attempt`, `SyncSchedule` and `SaveState`, at the Arca release that contains arca#47. No application-level cross-tab store. | R6-D-002 and R6-D-003 lived in an application store and retry loop that bypassed Arca (D-07). |
| ~~R7-AMD-02~~ (withdrawn: ADR-R7-001) | AR-035-O4's no-Web-Locks half is read as: without Web Locks no acknowledged change is lost, and tabs that cannot own the queue are refused durable writes and told so. | As written it expects every tab to keep offline changes durably without exclusion. Nothing in the browser offers that safely (localStorage has no compare-and-swap; both R6 lease designs failed). The corrected design fails closed. |
| R7-AMD-03 | `conditor ci-policy check` passes on the rehearsal repository at kickoff and on every candidate. | The R6 starvation came from a pipeline that nothing checked. |
| R7-AMD-04 | Acceptance runs three producers before the candidate freeze: `requirements-compiler verify-defect-closures`, `assertion-ratchet` (base = kickoff) and the preflight's `release-comparison` record. | G7, RF-F-8 and VER-001/002 failed for lack of these checks. |
| R7-AMD-05 | Every CI job that runs bound tests sets `CAUSA_PLAN`. Test fixtures that look like storage records are declared in a fixture manifest, which the credential scan honours, without weakening the scan. | Four FAIL requirements (UI-015, UI-021, UI-040, WDL-015) failed only because the browser shards could not find the plan; EXT-DATA-004-O1 and PF-015-O3 failed on fixtures. |

## Owner actions that no agent can take

The agent cannot take these actions:
1. **Credentials.** Create the read-only `CAUSA_GITHUB_TOKEN` and the scoped `CAUSA_WRITE_TOKEN`. See `docs/rehearsal-007/SECURITY-REVIEW.md`.
2. **Enforced gate (UAE-R7-12).** Put the trust store in an Actions secret, and add a ruleset on the rehearsal repository's `main`.
3. **Requirement amendments.** Approve or decline R7-AMD-03 to R7-AMD-05.
4. **Merges.** Merge the remediation pull requests, or authorize their merge under an approved policy.
