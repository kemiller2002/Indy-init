# Provider Fixtures, Fast UI Verification, and Integration Requirements

Status: normative, reusable requirements for Causa rehearsals and future implementations.
Authority: Indy-init. Applicability: all rehearsals and implementations with external data providers, subject to explicitly approved scope decisions.
Purpose: make UI/domain development fast and deterministic while independently verifying real provider communication. These requirements supplement, not replace, the storage, security, UI, and acceptance contracts.

## Architectural contract

**PF-001 [P0] Provider-neutral boundary.** Define a typed F# investigation-data port shared by fixture and GitHub adapters. The application/UI must consume the same result, error, cancellation, freshness, and provenance semantics regardless of provider. Provider selection must not alter domain meaning or authorization.

**PF-002 [P0] One domain authority.** Fixture adapters supply contract-compliant inputs; they must not implement parallel domain policy, state transitions, event reconstruction, causal inference, or confidence calculations. The existing F# engine validates and reconstructs both fixture and real-provider data.

**PF-003 [P0] Explicit provider selection.** Support an explicit local fixture mode and real GitHub mode, using a documented, repeatable build/run/test command (FAKE or equivalent). Fixture mode must require no network access, GitHub credentials, database, or external service. It must not silently activate in a production deployment. Display the active data mode conspicuously and never imply fixture data is live.

**PF-004 [P0] Contract fidelity.** Fixture data must follow the frozen versioned Causa storage contract and be loaded through the same parsing/validation/reconstruction path as real data. Maintain a machine-readable fixture manifest with IDs, schema versions, expected domain versions/digests, and expected error outcomes. Do not mutate the frozen historical dataset.

**PF-005 [P0] Provider isolation.** UI and domain tests must not directly depend on GitHub, GitHub Actions, network availability, or credentials. Real-provider tests must not silently fall back to fixtures or report mocked results as live success.

## Fixture catalog

**PF-006 [P0] Populated investigation.** Supply a compact complete investigation with observations, competing hypotheses, supporting and contradicting evidence, provenance, unknowns, causal relationships, corrective actions, verification records, timeline events, and populated semantic color states. Use stable IDs, timestamps, and deterministic ordering.

**PF-007 [P0] Edge-case fixtures.** Supply an empty investigation; a completed/verified investigation; missing or incomplete provenance; unresolved and contradictory evidence; invalid event history; invalid schema; malformed or missing catalog; and missing investigation. Expected outcomes must be explicit, including fail-closed behavior.

**PF-008 [P0] Provider-failure fixtures.** Model authorization failure, forbidden access, not found, missing branch (including GitHub HTTP 422 where observed), rate limit, timeout, offline failure, malformed response, retry, and partial transport failure. Distinguish provider errors from domain validation errors.

**PF-009 [P0] Concurrency fixtures.** Deterministically schedule delayed, reordered, superseded, cancelled, and retried requests. A stale response must never overwrite the state of a newer request, and cached success must never mask a new authentication failure. Request identity, active provider, credential generation, and freshness must be honored.

**PF-010 [P1] Fixture maintainability.** Provide a small, representative default dataset for rapid development plus opt-in extended scenarios. Avoid large duplicated JSON fixtures and ensure fixture edits are reviewed as contract changes.

## Verification layers

**PF-011 [P0] Four independent verification levels.**
1. Domain: deterministic F# validation, reconstruction, state-transition tests; no external dependencies.
2. UI: browser tests using local fixtures for Forma screens, responsive layouts, keyboard/focus, accessibility, semantic palette, loading and failure states; no external dependencies.
3. Provider conformance: shared contract tests against fixture and GitHub adapters, with sanitized recorded real responses where appropriate.
4. Live integration: real GitHub requests against the authorized Causa data repository, validating catalog discovery, data transfer, reconstruction, expected digests, and errors.

Each level must have an independently executable command, CI outcome, and evidence. A higher-level pass must not conceal a lower-level failure.

**PF-012 [P0] Fast local feedback.** Document a single command that builds and starts a populated fixture-backed UI and a separate command for fast offline domain/UI/provider-contract checks. CI must run offline deterministic checks without secrets. Record execution time and failures by layer; set measurable performance budgets after baseline measurement rather than inventing targets.

**PF-013 [P0] Real-provider conformance.** Maintain sanitized, versioned observations of actual GitHub status codes, response bodies, relevant headers, branch behavior, pagination, cache behavior, and retry semantics. Tests must detect fake/real divergence. Do not capture secrets, private payloads, or sensitive headers. Refresh observations when the provider contract changes.

**PF-014 [P0] Live integration acceptance.** At defined integration checkpoints and before release acceptance, run a real GitHub provider test against the approved repository and pinned catalog. Confirm actual network communication, correct request-to-response association, three historical investigation reconstructions, expected versions/digests, and deployed application behavior. Record repository ref, source SHA, run ID, and provider mode. Never claim live success from mocks.

**PF-015 [P0] Credentials and security.** Public read-only catalog tests should not require a PAT when anonymous access is sufficient. Authenticated integration tests use an approved secret such as `CAUSA_GITHUB_TOKEN` when explicitly required, with least-privilege read-only permissions. Never embed or print credentials, put them in URLs, or include them in screenshots, traces, logs, or artifacts. A missing secret yields BLOCKED, not PASS.

**PF-016 [P0] Visual verification.** Fixture scenarios must exercise navy, teal, amber, coral, and ivory per UI-016, with textual state labels and contrast checks. Capture deployed exact-SHA screenshots at required desktop, iPad, and mobile sizes for final acceptance; local fixture screenshots alone do not satisfy deployed acceptance.

**PF-017 [P0] Failure isolation.** Report failures by domain, UI, provider contract, or live integration layer. A provider outage must not make offline UI/domain checks fail; an offline pass must not waive a failing integration gate.

**PF-018 [P0] Traceability.** Map each fixture and test to stable requirements and authoritative source commit. Preserve obligations during rehearsal derivation; missing or weakened P0 obligations must fail requirements reconciliation unless explicitly approved as out of scope.

## Execution and release gates

**PF-019 [P0] Local-first workflow.** During development, agents should run the smallest relevant deterministic tests first, batch integration checks at meaningful checkpoints, and run the complete live/deployed suite before release. Never skip a test required by an impacted risk or dependency.

**PF-020 [P0] Deployment gate.** Release acceptance requires green domain, fixture UI, provider conformance, and applicable live integration checks, plus exact-SHA deployed browser verification. If the live provider or required credential is unavailable, report BLOCKED and preserve offline test evidence.

**PF-021 [P1] Reusable implementation.** Implement provider-neutral contracts and test harnesses in the narrowest appropriate Echelon components, avoiding a Causa-only parallel framework and minimizing dependencies. Keep fixture definitions and application-specific scenarios with the application.

## Acceptance scenarios

- Offline startup: one documented command starts a populated Causa UI with no network or token; screen state and active fixture mode are visible.
- Contract parity: identical valid fixture data yields identical domain reconstruction whether read from fixture or GitHub adapter.
- Failure fidelity: malformed events fail closed; missing branch, 401/403, 404, 422, rate-limit, and timeout are classified correctly.
- Stale-response regression: request B supersedes A; B completes first; late A cannot replace B. Reverse completion order also tested.
- Cache/auth regression: changing credentials invalidates relevant cached responses; an unauthorized request cannot be satisfied by stale cached success.
- Visual checks: populated fixtures expose the complete UI-016 palette and labels; contrast and responsive checks pass.
- Real integration: approved GitHub repository and pinned catalog are fetched over the network; all three historical investigation digests match.
- Gate honesty: disconnected integration or missing secret reports BLOCKED, not PASS, while deterministic offline checks remain independently green.
- Traceability: each PF requirement is mapped to an implementation test and evidence record; no future rehearsal may silently omit an applicable P0 requirement.

## Rehearsal intake

Before the next rehearsal, reconcile PF-001 through PF-021 into the rehearsal requirements package through the configured Praxis input workflow. Preserve this document as the authoritative source, and record explicit approved exclusions for any genuinely out-of-scope obligation. No GitHub Issues are to be used for requirements intake.
