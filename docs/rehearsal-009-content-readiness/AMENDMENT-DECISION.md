# Rehearsal 009 content readiness: amendment decision

**Decision: v4.1.2 does not cover these behaviours. A successor amendment (v4.1.3) is required. It is PROPOSED, not authoritative, and it needs the owner's approval.**

## Why v4.1.2 is not enough

1. **Not verified.** The content requirements are in the baseline but are not P0. The owner's ODR-V4-10 option A made the verification plan P0-only, so R1.1, R1.2, R2.x, R3.1–R3.5, R6.x, S01, S03 and S05 have no criterion and no bound test. No obligation requires entering content through the UI and reading it back after a reload (AUDIT.md §2.1, TRACEABILITY.md).
2. **Not specified.** No document specifies:
   - a primary investigative question;
   - editing an investigation's name or description;
   - revising observation, hypothesis or conclusion text;
   - user entry of a conclusion;
   - evidence title and description fields;
   - concrete empty-field and URL validation rules (AUDIT.md §2.3).
3. **Not typed.** The v4.1.2 contract left every content payload opaque by the owner's ODR-V412-02 option A. It also has no revision or conclusion command (AUDIT.md §2.2).

So the "strengthen guidance only, leave the requirements unchanged" path does not apply. Under the existing governance, guidance creates no obligation and no gate enforces it, which is exactly how Rehearsal 008 passed its gates without content.

## The proposed amendment

It follows the v4.1.2 precedent (`requirements-compiled/v4.1.2/FREEZE.md`, commit `2dc5a05`): inputs and owner decision requests first; a compiled package only after the owner records the decisions.

| Proposed change | Artifact (this directory) | Validation already run |
|---|---|---|
| CONTENT-001 … CONTENT-012: 14 P0 obligations, each traced to the baseline obligations it makes verifiable (49 IDs, all resolved) | `proposed/content-obligations.proposed.json` (sha256 `a1a0c7f7…d541`) | Dokimos 0.10.0 compiles it (exit 0). The scaffold writes 4 spec files. All 14 obligations are `invalid` as skeletons. The gate refuses with 0/14 verified. |
| Concrete content payloads; five revision and conclusion commands and events | `proposed/00-DOMAIN-STUBS.proposed.fs` (sha256 `92831b44…1466`) | Typechecks with all other v4.1.2 contracts unchanged (warnings as errors). Percepta: nothing blocking, every count matches, 109 types. |
| The INVESTIGATION-CONTENT-E2E scenario and the negative, mobile and conclusion scenarios | E2E-VERIFICATION.md, UI-ACCEPTANCE.md | The suite boundary and the fault-sensitivity rule are proven in `tools/r9/tests.fsx` and `evidence/` |
| Owner decision requests ODR-V413-01/02/03 | `proposed/owner-decision-requests.proposed.json` | n/a |

**What is not touched:**
- the frozen v4, v4.1, v4.1.1 and v4.1.2 files, manifests and hashes, and every approval record;
- Rehearsal 008;
- the Rehearsal 009 repository, which does not exist yet.

No approval record was written.

## Decisions required from the owner

1. **ODR-V413-01 (requirement addition).** Add CONTENT-001..012 as P0 obligations of package v4.1.3. *Recommended:* A.
   - **Option A:** add them as P0, with the live browser scenario, the negative and mobile suites, and the `content-round-trip` fault-sensitivity rule.
   - **Option B:** instead, promote the existing baseline requirements to P0 in a regenerated baseline.
   - **Option C:** change nothing; content stays unverified.
2. **ODR-V413-02 (interface change).** Freeze the concrete content payloads. *Recommended:* A.
   - **Option A:** replace `contracts/00-DOMAIN-STUBS.fs` with the proposed stubs.
   - **Option B:** no contract change; use extension commands and events.
   - **Option C:** keep the payloads opaque.

   This deliberately revisits ODR-V412-02, where the owner declined to freeze about 20 payload records. The proposal freezes only the content payloads.
3. **ODR-V413-03 (package approval).** After 01 and 02 are recorded, an agent compiles `requirements-compiled/v4.1.3` with the existing generator:
   - X01–X20;
   - the contract typecheck and negative controls;
   - Percepta spec-preflight;
   - an independent review;
   - the Conditor acceptance preflight.

   Then the owner approves it and tags `indy-init-requirements-v4.1.3`.

**How the owner records the decisions:** `dotnet fsi tools/owner-freeze.fsx -- decide …` writes the operator approval. `owner-freeze.fsx` needs a v4.1.3 entry, which the compile step adds, as it did for v4.1.2. Agents never write the record (RDP-003).

## If the owner declines

Rehearsal 009 can still implement content. v4.1.2's contract no longer forbids it, and the documents in this directory are guidance the build may follow. But no gate would enforce it, and a "verified" run could again lack content. FINAL-READINESS.md records that as BLOCKED under the assignment's definition.
