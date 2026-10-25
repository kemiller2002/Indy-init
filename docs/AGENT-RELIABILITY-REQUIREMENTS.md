# Autonomous Engineering Reliability Requirements

Status: normative for future Indy-init rehearsals and candidate reusable Echelon engineering capabilities. Applies to Praxis, Ordo, Dokimos, Percepta, and their integration points as appropriate. These are proposed requirements, not claims that current tooling implements them.

## Reliability controls

**AR-001 [P0] Source-of-truth baseline.** Every run must pin authoritative requirements and their source repository, commit, and content digest. A modified baseline must be detected and require an authorized change process.

**AR-002 [P0] Lossless requirement derivation.** Each source obligation must have a machine-verifiable mapping to an implementation requirement, or a recorded scope disposition. Detect omissions, weakening, negation, and role/value swaps, not only literal missing strings.

**AR-003 [P0] Authenticated decisions.** Agents must not approve their own exclusions, waivers, acceptance, or architectural exceptions. Require verifiable operator or delegated-authority approval, bound to the exact requirement and baseline digest.

**AR-004 [P0] Mandatory CI preservation gate.** A required, non-optional job must verify baseline integrity, requirement coverage, approvals, and evidence before merge or acceptance. Failing, missing, skipped, or bypassed checks cannot be reported PASS.

**AR-005 [P0] Capability registry and reuse.** Before designing a component, discover existing Echelon capabilities, owners, supported versions, and contracts. Reuse verified implementations; require an explicit approved justification for duplication.

**AR-006 [P0] Typed state and transition contracts.** Represent valid states, permissions, and transitions with typed F# contracts where practical. Invalid or unauthorized transitions must be rejected before side effects.

**AR-007 [P0] Evidence-backed acceptance.** Every PASS must cite the actual test, assertion, artifact, environment, and source SHA. Distinguish PASS, FAIL, PARTIAL, BLOCKED, and NOT_RUN; never infer PASS from code presence or a mock result.

**AR-008 [P0] Adversarial independent review.** A reviewer distinct from the implementer must attempt to falsify high-risk claims, inspect requirements and evidence, and record gaps. Reviewers cannot self-ratify operator decisions.

**AR-009 [P0] Negative-control testing.** Critical tests must include a deliberately invalid case that fails for the intended reason. Detect tests that pass regardless of implementation or verify mere existence rather than behavior.

**AR-010 [P0] Layered provider verification.** Run fast deterministic domain/fixture tests frequently, provider contract tests at integration boundaries, and real provider and deployed browser tests at acceptance. Live tests must never silently substitute mocks.

**AR-011 [P0] Reproducibility and provenance.** Record tool versions, pinned dependency SHAs, fixture seeds and digests, build inputs, CI run IDs, and deployed SHA. Re-run must distinguish reproducible differences from environment drift.

**AR-012 [P0] Controlled work scope.** Agent changes must be bounded to named requirements and paths; commits should stage intended files only. Detect unrelated changes and require explicit review before inclusion.

**AR-013 [P0] Risk-based verification.** Select tests based on changed contracts and dependency impact, preserving mandatory security, integrity, and release gates. Batching tests for efficiency cannot bypass required checks.

**AR-014 [P0] Secure capability isolation.** Agents receive only the credentials, repository access, context, and mutation permissions required for current work. Never echo secrets in logs, prompts, screenshots, or test artifacts.

**AR-015 [P0] Long-running agent continuity.** Maintain durable, compact, machine-readable checkpoints of objectives, decisions, completed work, open defects, evidence, and next actions so agent restarts do not re-decide settled questions.

**AR-016 [P1] Reliability experiment.** Compare existing agent workflow with constrained workflow on matched tasks, recording correctness, requirement retention, false PASS rate, rework, elapsed time, and token usage. Preserve raw evidence, define acceptance metrics before the run, and do not treat unobserved gains as measured.

**AR-017 [P0] Ownership and intake.** Route implementation work to the actual owning repository through its documented Praxis input folder and accepted grammar, never GitHub Issues. Do not assume component ownership from its name.

**AR-018 [P0] Fail-closed acceptance.** Missing credentials, unavailable external verification, unsigned approvals, stale evidence, failed gates, or unverified deployed artifacts must produce BLOCKED or FAIL as appropriate. An agent cannot override these outcomes with narrative.

## Required adversarial acceptance cases

1. Remove or weaken a mandatory UI requirement: preservation gate fails.
2. Modify baseline content while retaining a prior digest: integrity gate fails.
3. Swap semantic roles or negate a requirement without removing its keywords: semantic preservation review detects it.
4. Forge an agent-authored operator approval: approval gate fails.
5. Skip or disable the CI gate: acceptance remains blocked.
6. Use mock data in place of a required live test: acceptance remains blocked.
7. Present source code without behavior evidence: no PASS is granted.
8. Re-run a task with the same pinned inputs: evidence and decisions remain traceable.
9. Modify an unrelated file: scope guard reports it.
10. Compare matched constrained/unconstrained agent runs and publish correctness and cost without invented measurements.

## Adoption

First inventory current Praxis, Ordo, Dokimos, Percepta and related contracts. For each AR requirement, record existing implementation, gap, owning repository, test plan, and dependency. Place resulting work in each owner's documented Praxis input path. Do not mark requirements implemented merely because this document exists.
