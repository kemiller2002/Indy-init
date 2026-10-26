# Rehearsal Executable Acceptance Ledger Contract

Status: normative specification for a generated machine-readable ledger. **No current test results are asserted.**

## Single ledger format
Generate a version-controlled JSON document with:
- schema: `causa.acceptance-ledger/2`
- run_id, source_commit_sha, deployed_commit_sha (nullable until deployed), contract_version, frozen_fixture_digest, dependency_lock_sha, interface_manifest_sha
- requirements_baseline: {id, repository, commit, sha256}
- approvals_sha256 and evidence_manifest_sha256, supplied by the independent verifier
- scope_changes[]: {requirement_id, action, approval_id, to when priority changes}; inline approvals are forbidden
- requirements[]: {id, source_doc, priority, owner, implementation_paths[], tests[], evidence_paths[], status, reason}
- defect_classes[]: {id, severity, violated_invariant, original_repro, variants[], fix_sha, reviewer_evidence, evidence_paths[], status}
- gates[]: {id, name, scope, verified_sha, status, evidence_paths[], started_at, completed_at}
- metrics: {first_green_elapsed_seconds, first_candidate_elapsed_seconds, accepted_elapsed_seconds, candidate_count, repeated_defect_class_count, token_count, ci_queue_seconds, ci_test_seconds, verified_requirement_count}
- unresolved_decisions[] with owner, impact, and disposition.

Allowed statuses: NOT_RUN, PASS, FAIL, BLOCKED. Never convert missing evidence to PASS. No synthetic metrics.

## Gate matrix
| Gate | Mandatory evidence | Failure behavior |
|---|---|---|
| G0 Scope | requirement IDs, owner, profile priority, source precedence resolution | BLOCKED if mandatory conflict |
| G1 Dependencies | verified package/SHA/integrity lock, SDK, WASM/Forma/Folio smoke | BLOCKED if unknown |
| G2 Interfaces | frozen versioned handoff manifest and producer/consumer tests | BLOCKED if not extracted |
| G3 Domain | F# unit tests, illegal-transition and forged-history variants | FAIL if invalid history accepted |
| G4 Frozen data | five golden scenarios, hashes, 90 negative fixtures where applicable | FAIL on schema drift |
| G5 UI | P0 screen contracts, startup/error, iPad/keyboard/a11y, projection fidelity | FAIL for missing required behavior |
| G6 Persistence | local vs remote truth, expected-version conflict, reload, no fixture mutation | BLOCKED if live provider untested |
| G7 Defect class | independent review and SHA-bound closure for critical/recurring classes | FAIL while open |
| G8 Candidate | full applicable browser/integration/security/report tests | FAIL on missing required suite |
| G9 Deployment | deployed SHA equals verified SHA, successful acceptance job on same SHA | FAIL/BLOCKED otherwise |
| G10 Measurement | elapsed time, candidates, review recurrence, token and test cost | BLOCKED if unavailable; do not invent |

## Release acceptance predicate
Accepted is permitted only if all required gates G0–G9 are PASS for the same verified source identity, no open critical defect class remains, and deployed acceptance is PASS. G10 completeness is separately reported and cannot be silently assumed. The run may be incomplete, blocked, or failed; partial successes are reported honestly.

**The verdict is computed, never written** (owner decision OD-INDY-7, [OWNER-DECISIONS-2026-10-24.md](OWNER-DECISIONS-2026-10-24.md)). `tools/compute-acceptance-verdict.py` derives ACCEPTED, INCOMPLETE, BLOCKED, FAILED or INVALID from the ledger and independently supplied trust inputs. It requires the candidate repository and trusted source SHA; the exact requirements baseline and its independently supplied SHA-256; a source checkout at the baseline commit; an externally pinned approvals file and SHA-256 plus the trusted operator identities; and a content-addressed evidence manifest, evidence directory and manifest SHA-256. It exits 1 when the ledger's `accepted` flag or the report disagrees. The report carries exactly one `<!-- acceptance-verdict: VERDICT -->` line, and no Verdict/Outcome line may claim acceptance the computation does not give.

The verifier opens every baseline source document at the pinned commit. It compares every baseline P0 identity with the ledger, so deleting or downgrading an item cannot remove it from acceptance. A scope change is effective only when it exactly matches an operator approval in the externally pinned approvals file and that approval's operator identity is named independently with `--trusted-operator-id`. Agent-authored approvals in the ledger and untrusted operator identities are invalid.

Every PASS evidence path names a `causa.acceptance-evidence/1` JSON result in a `causa.acceptance-evidence-manifest/1` bundle. The verifier checks the bundle's run and source identities, the bytes and SHA-256 of each result, the result's subject, status, command and exit code, required CI/deployment evidence types, and independent defect review evidence. Missing, stale, altered or cross-run evidence is INVALID. Digest pinning establishes integrity; authentication of the pins remains the responsibility of the trusted CI artifact, protected reference, Dokimos attestation or independent verifier supplying them.

A rehearsal's CI runs the structural and independent-verdict regression suites on every push and pull request. This repository does not claim that the job is non-bypassable unless repository rules independently require it. There is no ledger-local exclusion path: a P0 requirement leaves the verdict only through an externally pinned operator approval bound to the exact baseline.

## Requirement coverage
Map the normative requirements in REQUIREMENTS.md, CAUSA-UI-EXPERIENCE-REQUIREMENTS.md, WASM-LOADING-EXPERIENCE-REQUIREMENTS.md, FROZEN-DEMO-STORAGE-REQUIREMENTS.md, DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md and AGENT-DECISION-REDUCTION-PROFILE.md. Map existing SCREEN-CONTRACTS.md where screens are implemented. P1 scope is tracked explicitly; deferred items remain NOT_RUN rather than disappearing.

## Release batching and evidence
Use targeted checks during development; batch compatible fixes into a release candidate. Independent architecture review before first candidate. Preserve R001 baseline and historical results unchanged. Emit one ledger per candidate and one final immutable report with exact source/deployment identities.

## Next implementation step
Implement a small validator/generator consuming requirement IDs, test results and gate artifacts. It must reject duplicate IDs, unknown statuses, missing proof, mismatched SHAs, and an Accepted flag with incomplete required gates. Do not build a new generic workflow platform merely to implement this ledger.
