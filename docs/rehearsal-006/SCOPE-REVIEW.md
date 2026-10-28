# Rehearsal 6 scope review: 856157a..f8b1a2c

The machine-readable record is [`scope-review.json`](scope-review.json). This page is its readable summary.

- **Reviewer:** an agent (`anthropic/claude-code-reviewer`), a fresh-context subagent spawned by the implementing session. It did not author the reviewed commits.
- **Date:** 2026-10-28.
- **How it was read:** every file was read at `f8b1a2c`, not from the working tree, because the branch moved to `9297528` during the review (SR-15).
- **Verdict:** **NO_BLOCKING_FINDINGS**: 4 major and 11 minor findings.
- **Attestation:** NOT_ATTESTED. This counts as the independent review only when the owner (kemiller2002) attests it with a GitHub review of the pull request.

## Findings

### Major

- **SR-01: agent-written approval records count as "authenticated operator" approvals.**
  - `approvals.causa.json` was written by the agent and carries `authority: {kind: operator, id: kemiller2002}`. The compiler accepts that string as authentication.
  - RDP-003 says an agent MUST NOT write an approval record. The R5A-001 intake says the R5A-001-11 decisions (DEC-R6-003 and -004) are recorded by the owner, not transcribed.
  - The records restate RDP-003 as "takes effect when the owner merges". That reading has precedent on main, but it is not RDP-003's text.
  - The file is honestly labelled as a transcription.
  - **Remedy:** record it as an explicit RDP-003 deviation that the owner accepts in the PR review, or have the owner commit the file.
- **SR-02: "legal command enforcement" (UI-015-O2) is mapped to constituents that prove less than the clause.**
  - Its constituents are UI-004-O1 (render only authorized commands) and UI-004-O6 (no parallel policy engine).
  - UI-004-O4 (reject stale submissions) and AR-006-O2 (reject invalid transitions before side effects) state the enforcement, and they are missing.
  - Two weaker cases:
    - "offline/conflict recovery" leans on UI-010-O1, a designed state rather than recovery;
    - the catalog clause leans on EXT-DATA-013-O2, which needs no browser evidence.
- **SR-03: the derivation check can be beaten through curation.**
  - Any word of the text may be declared a connective, which drops whole clauses.
  - Clause-to-constituent meaning is never checked, so every residual clause can be mapped away.
  - Both edits pass compilation (V18) and plan-readiness. Reproductions are in `/tmp/claude-0/review/mut-connective` and `/tmp/claude-0/review/mut-mismap`.
- **SR-04: the v3 plan fails its own shipped schema.**
  - `v3/verification-plan.schema.json` is v2's revision-2 schema, unchanged and closed.
  - Validating the v3 plan against it gives 7 errors: `scopeDecisions`, `amendment` and `derivation` are unexpected.

### Minor

- **SR-05:** R5A-001-04 is recorded as "implemented", but removals apply only when an optional, unpinned `--review-tag-changes` file is passed. No pinned invocation passes it.
- **SR-06:**
  - A resealed plan with the derivation stripped passes preservation, derivation diagnostics and readiness.
  - `scopeDecisions` is not cross-checked against the obligations.
  - Only the digest pins catch it, and verify-plan has none (a pre-existing gap). Reproduction: `/tmp/claude-0/review/fsx/strip.fsx`.
- **SR-07:**
  - The option texts in the records differ from the committed script for Q1, Q3 and Q4.
  - The second owner script they cite is not committed.
  - The amendments are no weaker than either version.
- **SR-08:**
  - The context entries quote owner words that are not among the statements given to the reviewer.
  - Two of them, "Ok let's pass on…" and "1 counts as kickoff", first appear in `f8b1a2c`.
- **SR-09:**
  - The stated reason for the residual clauses is wrong for timeline ordering: UI-003-O1 and UI-003-O3 state it.
  - The reason is partly wrong for the fixtures (EXT-DATA-013-O3) and startup (UI-014-O2).
  - Keeping those clauses residual is still conservative.
- **SR-10:**
  - DEC-R6-001 leaves the PF-015-O2 read-only-secret question to the implementation and the reviewer.
  - It needs an owner decision before kickoff, and a second write token would be a new secret under AR-033.
- **SR-11:** the v3 rebuild depends on commit `1865da2` staying reachable. A squash merge breaks `--check` and the offline suite.
- **SR-12:** the curated `decision` field is free text. It is not checked against the approval or against `owner-decisions.json`.
- **SR-13:** `PREFLIGHT.md` and `preflight.json` still name `VP-0eb9c791dd0b` and BLOCKED. The preflight was not re-evaluated in range.
- **SR-14:** AR-031-O2, the owner's Decision Record, is bound only to reviewer acceptance, although an owner-decision category exists.
- **SR-15:**
  - HEAD moved to `9297528`. Commits `317eaf1` and `9297528` change the reviewed records and add DEC-R6-009 with new owner words.
  - Those commits are not covered by this review.

## Checks

| # | Check | Result | Key evidence |
|---|---|---|---|
| 1 | Authorization and exact quotes | PARTIAL | Exact quotes on DEC-R6-001..008. Provenance is stated, and no record implies the owner edited a file. No record answers an unasked question. SR-07, SR-08, SR-10 |
| 2 | Nothing removed or weakened | PASS | v2 → v3, obligation by obligation: 0 removed, 29 AR obligations added, the 326 common obligations identical except the 2 amendments and 1 derivation. Both amendments are faithful to option A and stricter than the committed script's option A. AR-032 and AR-033 stay P1 and out of the plan |
| 3 | Frozen artifacts | PASS | Empty diff for v1, v2, the frozen baseline, `kickoff/rehearsal-005`, the R5 audit, input documents and the AR source. v2 `--check` exits 0 |
| 4 | Baseline regenerated | PASS | `baseline` at 856157a is byte-identical (sha `40599abe…befd`). The only differences are AR-027..AR-035, the section preamble and one reconciliation sentence |
| 5 | Package consistency | PARTIAL | v3 `--check` exits 0, and its negative control gives exit 4. `plan-readiness` is READY: 671/671 checks, 0 permits. The offline suite passes, and its negative control fails 6 tests. The schema does not match (SR-04) |
| 6 | Exclusions, applicability, bypasses | PARTIAL | No exclusions and no `permitUnresolved`. All 355 obligations are unverified. The amendment mechanism holds. The derivation and review-tag mechanisms have the gaps in SR-03, SR-05 and SR-06 |
| 7 | Evidence bindings | PASS | The curation bindings equal the process proposal extended by the r6 proposal. All v2 bindings and rules are unchanged. The four new rules quote their sources verbatim and only add categories |
| 8 | No fabricated approval | PARTIAL | The approvals are labelled as transcriptions, and no agent words are presented as the owner's. The authority field conflicts with RDP-003 (SR-01) |
| 9 | UI-015-O2 derivation | PARTIAL | 11 verbatim clauses. Four mappings are sound and three under-prove (SR-02). Residual clauses are kept conservatively, with a wrong stated reason (SR-09). The checker only adds a condition |
| 10 | Kickoff pins | PASS | The README and `acceptance-process.yml` pin the v3 plan and digest `13516e0d…a66c`, with no stale v2 pin. Preflight docs are stale (SR-13) |
