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


## Evidence-driven refinement (October 2026)

The reported ARCH-002 trial found 29 architecture violations in 68 unguided trials versus 0 in 71 trials with the four-tier architecture brief. Additional Ordo enforcement showed no measured improvement and reportedly increased cost by 7%. Continuous agents reportedly reduced cost per accepted requirement by 40.9%. These are experiment-specific observations, not universal guarantees; retain raw trial evidence and study context-compaction effects separately.

**AR-019 [P0] Computed verdict, not agent assertion.** The authoritative acceptance outcome MUST be computed from the evidence ledger, frozen baseline, required gates, requirements coverage, approved decisions, defect closures, and deployed SHA. Agent-authored accepted fields are advisory at most. Original Rehearsal 3 must yield BLOCKED; Rehearsal 2 candidate 45644a7 should yield ACCEPTED only if its actual ledger and evidence meet the frozen contract. Include tests for false accepted flags, missing CI, baseline tampering, and unauthorized waivers.

**AR-020 [P0] Selective enforcement.** Retain the demonstrated four-tier architecture brief as default guidance. Do not add per-step Ordo architecture enforcement, typed agent records, or executable decision gates without a measured incremental correctness benefit that justifies cost and false rejections. Typed domain models and security/acceptance boundaries remain mandatory where independently justified.

**AR-021 [P0] Continuous implementation agent.** Prefer one long-running implementation agent with compact durable checkpoints. Do not require structured handoffs by default. Measure correctness and requirement retention after context compaction; use independent reviewers at release candidates.

**AR-022 [P0] Testing discipline.** Dependency-based test selection may prioritize but must not omit mandatory regression/security/integration coverage. Do not restrict CI to infrequent checkpoints when intermediate changes can hide failures. Require baseline tests that demonstrably fail on injected defects before trusting an experiment.

**AR-023 [P0] Release-set integrity.** Select the latest *verified compatible release set*, not unrelated latest versions. Confirm whether Dokimos 0.7.0 and Percepta have published artifacts; do not claim attestation from a set lacking its required components. Resolve and verify Conditor scaffold defect SD-1. Pin the exact set and require evidence for waivers.

**AR-024 [P0] Explicit kickoff decisions.** An authorized human must resolve the write scope, Percepta release/waiver, DF-CAUSA-2026-0003 operator decisions, and frozen requirements/stack before a timed full-write rehearsal. Agents must not self-ratify these decisions.

**AR-025 [P1] Research controls.** Compare constrained versus baseline workflows on matched tasks with measured accepted requirements, false acceptance, escaped defects, token cost, elapsed time, and rework. Do not claim 'prevented by design' without a falsifiable test or generalize a single reviewer observation as proven efficacy.

**AR-026 [P1] Shared HTTP cache.** Investigate a provider-neutral, opt-in cache policy at the Limen HTTP boundary with explicit freshness, invalidation, security, and stale-data semantics; do not silently use stale results for authoritative decisions. Place implementation in Limen's documented Praxis input path after owner/contract verification.

### Reconciliation
AR-019..AR-026 refine AR-001..AR-018 when there is tension. In particular AR-003 protects genuine approval authority, not agent-maintained pseudo-authority; AR-006 concerns domain correctness, not universal typed agent messages; AR-013 allows test prioritization, not skipping mandatory CI. These requirements do not establish that any release or implementation is complete.
