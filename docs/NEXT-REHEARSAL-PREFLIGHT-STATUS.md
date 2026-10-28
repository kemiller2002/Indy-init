# Next Rehearsal Preflight Status

As of 2026-10-23. No experiment started. Status values are evidence-scoped, not aspirational.

| Check | Status | Evidence |
|---|---|---|
| Agent decision reduction rules | DOCUMENTED | AGENT-DECISION-REDUCTION-PROFILE.md |
| Baseline dependency versions | INSPECTED | indy-init-rehearsal-001/package.json, package-lock.json, .echelon/foundations.json, .ros/installation.json |
| Forma 0.9 + foundations compatibility | BLOCKED | No executed install/build/browser compatibility gate yet |
| Folio artifact integrity and WASM startup | BLOCKED | No executed compatibility gate yet |
| Frozen storage contract identity | DOCUMENTED | CAUSA-STORAGE-RELEASE-v1.0.0.md |
| Hypothesis vs causal-confirmation contradiction | RESOLVED IN DOCS | COMMAND-EVENT-CATALOG.md matches F# Model.fs and STATE-TRANSITION-TABLES.md |
| F# API baseline extraction | INSPECTED | REHEARSAL-INTERFACE-MANIFEST-R001.md includes source blob SHAs |
| F# compile and consumer interface freeze | BLOCKED | No executed build or signed manifest |
| Acceptance ledger schema and validator | IMPLEMENTED, NOT EXECUTED | tools/validate-acceptance-ledger.py |
| Acceptance validator negative tests | AUTHORED, NOT EXECUTED | tools/test-acceptance-ledger.py |
| Praxis work item ingestion | NOT RUN | Confirm exact documented input directory/grammar before creating intake |
| Deployed exact-SHA acceptance | NOT RUN | Next rehearsal not started |

## Immediate run commands
```bash
python3 tools/test-acceptance-ledger.py
python3 tools/validate-acceptance-ledger.py path/to/ledger.json
```
The validator checks internal structural consistency and proof references; it does not independently establish that referenced evidence exists or that CI passed. Integrate with artifact existence verification and SHA-bound CI evidence before using it as a release gate.

## Additional discovered risk
R001 .echelon/foundations.json pins Forma 0.5.0, Folio 0.3.0, Limen 0.9.0, Ordo 1.5.0, Praxis 3.16.0. package.json also pins Forma 0.5.0. Any upgrade MUST update all corresponding manifests consistently, not only package.json.

## No-go conditions
Do not start the timed experiment with an unverified dependency upgrade, inconsistent schema, uncompiled interface, unvalidated golden fixtures, or a fabricated acceptance PASS.

## 2026-10-28 addendum: isolated Causa workspace initialization

No new rehearsal has started. CHT-001 is recorded in the legacy Praxis intake at `input-documents/2026-10-28-CHT-001-causa-historical-workspace-initialization.md`, and the prospective workflow is documented in `CAUSA-REHEARSAL-WORKSPACE-PREFLIGHT.md`.

| Check | Status | Evidence |
|---|---|---|
| Immutable historical, everyday and combined templates | IMPLEMENTED ON FEATURE BRANCH, NOT MERGED | `causa-data` branch `codex/immutable-historical-rehearsal-workspaces`, commit `cfd761d`; digests and source commits recorded in each manifest |
| Deterministic lightbulb reasoning and negative control | VERIFIED ON FEATURE BRANCH | Engine-authored 48-event successful history; engine-driven failed-replacement scenario; Causa 32-test suite |
| Independent workspace creation and isolation | VERIFIED ON FEATURE BRANCH | Two-workspace append isolation; template and canonical-source byte checks |
| Conditor ordered fail-closed initialization | IMPLEMENTED ON FEATURE BRANCH, NOT MERGED | `conditor` branch `codex/causa-rehearsal-preflight-v2`, commit `822f0ca`; Praxis work item `WI-0062` complete |
| GitHub publication, remote readback and application load | VERIFIED ON DISPOSABLE BRANCH | `causa-data` branch `codex/causa-workspace-e2e-20261028`, `rehearsal-900001`, commit `c3101ec32521482b5a939fd43dde0e8676fb4d93` |
| CHT-001 / ELB-001 Praxis derivation | NOT RUN | Intake documents exist; Indy Init has no Praxis installation |
| Event workspace selection | NOT RUN | Operator must select the next rehearsal id after merge/release |
| Timed execution | NOT RUN | READY initialization is preparation only; explicit start remains required |

The next rehearsal remains blocked until the feature changes are reviewed and merged, CHT-001 and ELB-001 are ingested with derived records, an operator selects a new unique id and template, and the real preflight receipt is READY. Disposable integration-test workspaces must not be reused as the event workspace.
