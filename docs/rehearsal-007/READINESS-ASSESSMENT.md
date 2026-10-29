# Rehearsal 7 readiness assessment (persistence requirements)

**Requirements: READY.** The persistence requirements are written, validated and committed, and the baseline is pinned ([validation report](REQUIREMENTS-VALIDATION-REPORT.md)).

**Kickoff: NOT READY.** The open steps below must happen before Rehearsal 7 starts. None of them is application work, and Rehearsal 7 has not started. The main blockers are the four-case causa-data catalog (K7), the v4 package (K3) and the two tokens (K6).

| | |
|---|---|
| Decision | [ADR-R7-001](PERSISTENCE-ARCHITECTURE-DECISION.md), effective on the owner's merge (RDP-003) |
| Requirements | [`docs/CAUSA-PERSISTENCE-REQUIREMENTS.md`](../CAUSA-PERSISTENCE-REQUIREMENTS.md), PERSIST-001 to PERSIST-055, plus 30 amended Rehearsal 6 requirements |
| Owner decisions | [`kickoff/rehearsal-007/OWNER-DECISIONS.md`](../../kickoff/rehearsal-007/OWNER-DECISIONS.md), DEC-R7-001 to DEC-R7-007 |
| Baseline | `BL-CAUSA-INDY-INIT-8445ca11986f`, re-pinned after the owner decisions ([`requirements/causa-requirements-baseline-r7.json`](../../requirements/causa-requirements-baseline-r7.json)) |
| Traceability | [`DEFECT-TRACEABILITY.md`](DEFECT-TRACEABILITY.md), [`traceability.json`](traceability.json) |
| Validator | `tools/validate-r7-persistence-requirements.fsx`, run in CI by `preflight-tooling.yml` together with its negative controls |

## Still a from-requirements build

- **Nothing to copy:** no application file was created, copied or migrated. Causa and every rehearsal repository are unchanged, and Rehearsal 6's results are unchanged.
- **Copying is forbidden:** PERSIST-026 forbids copying, vendoring, translating or adapting application code, tests or scripts from Causa or Rehearsals 1 to 6. PAT-13 checks the candidate for byte-identical files.
- **No extracted interfaces:** the interface freeze no longer extracts interfaces from R001 source. The R001 interface manifest is marked "not an input to Rehearsal 7".
- **Behaviour only:** the requirements state behaviour, limits and tests, never an implementation.
- **One existing link to old code, outside the build:** Indy-init's own preflight tooling still builds `tools/persistence-verification` against the Rehearsal 2 adapter, a Rehearsal 4 verifier (T05). It is not a Rehearsal 7 input, and no requirement points the agent at it. The owner may retire it with the v4 cut.

## Before kickoff

| # | Step | Who | State |
|---|---|---|---|
| K1 | Merge ADR-R7-001 and the persistence requirements | Owner | **Done.** Approved ("I approve") and merged as pull request #41 (`8d01613`) |
| K2 | Confirm the values chosen in writing the requirements ([ADR table](PERSISTENCE-ARCHITECTURE-DECISION.md#choices-made-in-writing-the-requirements)) | Owner | **Done**, with the approval of #41 |
| K4 | Record the Rehearsal 7 owner decisions for the scope carried from Rehearsal 6 | Owner | **Done.** DEC-R7-001 to DEC-R7-007 are recorded, and those that change a requirement are applied to its text. They take effect on the owner's merge of the pull request that adds them (RDP-003). DEC-R7-005 keeps Rehearsal 6's screen scope: S16 NOT-APPLICABLE and the other 24 IN SCOPE |
| K7 | **Publish the four-case catalog in causa-data (DEC-R7-006).** causa-data `main` holds only `historical-v1` (Apollo 1, Apollo 11, Challenger). Before kickoff it needs four things, verified by causa-data's own gates: the burned-out lightbulb investigation, a catalog with all four investigations, `catalog/active.json` naming that catalog, and its workspace initialization. Rehearsal 6's preflight stages 7 to 10 are no longer waived | Owner, or an agent in causa-data | **Blocker.** EXT-DATA-013, UI-001, UI-015 and PF-014 require four |
| K3 | Cut compiled package **v4** from `BL-CAUSA-INDY-INIT-8445ca11986f`. The steps are K3a to K3f below, and the compiler must report PASS with plan-readiness READY and no permits | Agent, then owner | Open. The R7 compile with the v3 curation fails V09, V11, V17 and V18 for curation reasons only |
| K5 | Close the Rehearsal 6 report's other recommendations, in preflight: G7 defect classes re-closed with fix SHAs in R7's own history; deploy gated on a green core job with visible test output; owner attestation through approving GitHub reviews; a pre-lock newest-release comparison (VER-001/002); the tooling gaps D-03 and attestation types | Agent and owner | Open. The amber outline item is settled by DEC-R7-002, and the read-only token by DEC-R7-007 and K6 |
| K6 | Create `CAUSA_GITHUB_TOKEN` (read-only) and `CAUSA_WRITE_TOKEN` (contents write on the test repository only), and confirm the live test repository exists. PAT-02, PAT-04 and PAT-10 must run live (PERSIST-025) | Owner | Open. The owner said both tokens will be created (DEC-R7-007) |

The v4 cut (K3) has six parts:

- **K3a:** a verification rule and evidence-category rules for `docs/CAUSA-PERSISTENCE-REQUIREMENTS.md`, so the 255 PERSIST obligations are classified. They should be integration, browser and live-provider for PAT-02 to PAT-11, unit for PAT-12, and accessibility for PAT-15.
- **K3b:** a work item covering PERSIST-001 to PERSIST-055 (V09, V17).
- **K3c:** the four v3 citations of replaced text re-cited, and UI-015-O2's derived-obligation map rewritten for its four-investigation wording (V11, V18).
- **K3d:** a `kickoff/rehearsal-007/approvals.causa.json` bound to the R7 baseline, with no records (V18, RDP-003).
- **K3e:** test bindings regenerated by `propose-test-bindings`, including re-bindings for every superseded P0 obligation.
- **K3f:** an R7 scope test in the compiler suite, as `R6ScopeTests` is for v3. Its negative controls cover DEC-R7-001 to DEC-R7-007.

## Risks the timed build will meet

| Risk | Where the requirements handle it |
|---|---|
| Safari on iPad may not show a `beforeunload` warning | PERSIST-020 makes the save-state indicator the primary safeguard and requires the termination statement; PAT-09 checks the warning only where supported |
| A failed `fetch` cannot always tell "not sent" from "sent and lost" | PERSIST-015: anything after sending is Unknown commit outcome and reconciled by marker and head; PERSIST-016: no automatic retry of a send that failed before sending |
| The stale-head answer from `createCommitOnBranch` has to be recognised by its typed content | PERSIST-008 requires typed classification and a test of every class (PAT-03), live as well as fake |
| Conflicts become visible when one investigation is open in two tabs | The intended consequence (ADR-R7-001). PERSIST-012's reapply re-decides through the engine, so the user keeps the work |
| GitHub secondary rate limits during PAT-02 and PAT-08 | Rate limit is its own classification (PERSIST-008), and retries are bounded (PERSIST-016) |
| Agents reintroduce browser storage through a library | PERSIST-003 and PERSIST-026 forbid it whatever the library; PAT-01 and PAT-13 inspect stores and the artifact |
| Forma components | Status lozenge, operation status, conflict review and freshness exist at the Forma commit the current package pins (`28d1101`). The approved release set for R7 must still be resolved in preflight (DEP-CURRENT) |

## Defects this change addresses

| Defect | Closed by | Proven by |
|---|---|---|
| R6-D-002 cross-tab lost updates | No shared store and no cross-tab locking (PERSIST-002 to PERSIST-004); expected-head commits (PERSIST-006, PERSIST-013) | PAT-01, PAT-02, PAT-11, PAT-13 |
| R6-D-003 offline retry loop | No offline queue (PERSIST-018); bounded mutations and reads (PERSIST-010, PERSIST-016); truthful Offline (PERSIST-031, PERSIST-032) | PAT-07, PAT-08, PAT-15 |
| R6 GitHub conflict and rebase findings (R1-F-2, RF-F-3, RF-F-9, R6-D-001, R1-F-1, RF-F-7) | PERSIST-007 to PERSIST-012, PERSIST-015, PERSIST-021 to PERSIST-024, PERSIST-033 | PAT-03 to PAT-07, PAT-10, PAT-12, PAT-14, PAT-15 |

These defects are closed in the requirements only. They are verified only when Rehearsal 7's candidate passes its PAT tests.
