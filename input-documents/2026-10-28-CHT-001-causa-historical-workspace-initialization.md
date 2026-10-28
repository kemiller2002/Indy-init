# CHT-001: Causa historical templates and rehearsal workspace initialization

Priority: P0 before the next Causa rehearsal starts.
Owner systems: Causa Data (template/workspace implementation), Conditor (ordered preflight), Indy Init (requirements and evidence contract).
Target repositories: `kemiller2002/causa-data`, `kemiller2002/conditor`, and `kemiller2002/Indy-init`.
Reference only: `kemiller2002/indy-init-rehearsal-005` (frozen; no files or acceptance evidence may change).
Intake: Praxis document ingestion through Indy Init's legacy `input-documents/` path. Indy Init is not Praxis-initialized; this file is source material, not a canonical requirement or an approval.

## Objective

Every rehearsal begins from a newly created, verified and isolated Causa workspace containing byte-identical starting histories for Apollo 1, Apollo 11 and Space Shuttle Challenger. Causa owns the reusable operations. Conditor invokes them and refuses readiness unless local creation, GitHub publication, remote readback and application loading all have complete evidence.

## Requirements

| ID | Owner | Requirement | Acceptance evidence |
|---|---|---|---|
| CHT-001-01 | Causa Data | The repository contains a versioned immutable `historical-v1` template that copies the three canonical investigation file sets byte-for-byte without changing ids, events, timestamps, manifests, snapshots or the active historical catalog. | Raw-byte comparison for all nine storage files; exact three identifiers; canonical historical source unchanged. |
| CHT-001-02 | Causa Data | A canonical template manifest records template/version, storage-contract version, source repository/commit/directory, investigation ids, relative paths, each raw-file SHA-256 and an explicitly documented deterministic overall digest. The pinned F# engine validates every history and snapshot. | Manifest schema/identity tests, stable digest test, engine replay, invalid-template negative control. |
| CHT-001-03 | Causa F# tooling | Direct callers can create a uniquely named workspace from a verified template. Creation rejects empty/invalid/traversing names, symlink escapes, paths outside `workspaces/demo`, and any existing destination; it stages, verifies, atomically publishes, and cleans only its own failed staging artifacts. | Creation, invalid-name, traversal, symlink, existing-destination, partial-cleanup and safe-retry tests. |
| CHT-001-04 | Causa F# tooling | Initialized verification requires exactly the template investigations/files and byte identity. Modified verification accepts valid appended events only when the original event stream remains an exact byte prefix and the complete current history/snapshot replays. | Initialized and modified positive tests; unexpected-file, rewritten/replaced historical-event and snapshot/history negative controls. |
| CHT-001-05 | Causa F# tooling / GitHub | Publication confirms repository and branch, refuses an existing remote workspace, creates one non-forced commit limited to the workspace prefix, detects concurrent movement, reads the commit back, verifies every path/byte/history, reports the commit SHA and never exposes credentials. | Disposable-repository success, existing/concurrent negative controls, changed-path boundary, remote readback and credential-redaction tests. |
| CHT-001-06 | Conditor | The existing Conditor manifest format can declare one Causa rehearsal workspace. `conditor rehearsal init` invokes the Causa F# interface in this order: resolve configuration/tooling, verify template, prove local and remote absence, create, locally verify, publish, independently remote-verify, application-verify, write evidence, decide readiness. Conditor must not copy Causa storage logic. | Ordered invocation test and a real disposable-branch end-to-end receipt. |
| CHT-001-07 | Conditor | Readiness is fail-closed for missing/invalid templates, either existing destination, creation/validation/publication/readback/application failures, mismatched repository/branch/workspace/commit, malformed or incomplete evidence, and unavailable tools/authentication. It never falls back to a previous workspace or creates an empty replacement. | One negative-control test per stage, identity-mismatch and missing-evidence tests, and proof that every attempted workspace argument remains the configured name. |
| CHT-001-08 | Conditor / Indy Init | A schema-validated receipt records rehearsal/template/source/destination/workspace/published commit/investigation identity, local/remote/application results, timestamp and verdict. Evidence-backed states are `LOCAL_CREATED`, `LOCAL_VERIFIED`, `PUBLISHED`, `REMOTE_VERIFIED`, `APPLICATION_VERIFIED`, `READY` and `BLOCKED`; partial progress is never READY. | READY and every BLOCKED receipt validate against the committed schema; transitions cite their producing stage. |
| CHT-001-09 | Causa application | The generated layout matches the actual provider resolver used by the frozen Rehearsal 5 application. A selected published workspace loads Apollo 1, Apollo 11 and Challenger. Writes to one generated workspace do not alter the template, canonical historical workspace or another generated workspace. | Resolver/load compatibility check plus a two-workspace persistence test with byte digests of every unaffected copy. |
| CHT-001-10 | Indy Init / Conditor | Rehearsal preflight requires a unique explicit rehearsal id and complete READY receipt before the timed start can be authorized. Initialization is preparation only and cannot activate the mission, launch the implementation agent or record the timed start; timed execution remains an explicit later transition. | Runbook ordering, receipt review, and absence of any start/supervise/Praxis activation in the initialization command. |
| CHT-001-11 | All owners | The implementation remains reusable outside the event: independent Causa commands work without Conditor; Conditor is an orchestrator, not a storage owner; no new orchestration framework, database, Kubernetes, Terraform or credential store is introduced. | Direct workspace command tests, documented component boundaries and dependency review. |
| CHT-001-12 | Indy Init | Rehearsal 5 remains frozen. These requirements apply prospectively and do not amend its verdict, acceptance ledger, timestamps or evidence. | Git diff for `kemiller2002/indy-init-rehearsal-005` is empty; no Rehearsal 5 record is rewritten here. |

## Preflight gate

The next rehearsal is BLOCKED until all of the following are true:

1. The Causa and Conditor implementation commits are reviewed, merged and released/available to the rehearsal host.
2. Indy Init has ingested this document through Praxis, producing derived records that name this input in `derived_from`.
3. The operator selects a new rehearsal identifier and supplies the matching Conditor configuration; no agent invents or reuses an id.
4. `conditor rehearsal init --json` returns exit 0 and its receipt validates as READY.
5. The receipt's remote commit is the head of the configured Causa repository/branch and application verification names all three expected ids.
6. The operator then makes the separate explicit transition into timed execution under the event rules.

## Current implementation evidence (not merge or release evidence)

On 2026-10-28 the feature implementation produced:

- Causa Data branch `codex/immutable-historical-rehearsal-workspaces`, culminating in commit `cfd761d` (historical implementation commits begin at `d213361`).
- Conditor branch `codex/causa-rehearsal-preflight-v2`, culminating in commit `822f0ca` with Praxis work item `WI-0062` complete.
- Disposable Causa branch `codex/causa-workspace-e2e-20261028`, workspace `rehearsal-900001`, published commit `c3101ec32521482b5a939fd43dde0e8676fb4d93`, with a Conditor READY receipt.

These prove the implementation path on disposable infrastructure. They do not claim that the branches are merged, a Conditor release has been cut, the next event workspace has been chosen, or timed execution has begun.

## Acceptance for this intake

- Each CHT-001 item has a derived Praxis record naming this document in `derived_from`.
- No GitHub Issue substitutes for ingestion.
- No agent authors an operator approval or silently changes the rehearsal identifier.
- The published receipt, its remote commit and the final preflight evidence are retained by the next rehearsal.
