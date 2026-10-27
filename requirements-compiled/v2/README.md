# Compiled requirements package v2

The compiled requirements for the baseline that follows Rehearsal 4. It is produced by the same F# compiler and rules as [`v1`](../v1/README.md), which remains the frozen Rehearsal 4 package and is not modified.

| | |
|---|---|
| Source | kemiller2002/indy-init `78277924cc2bfceb7c8bcfaeb55b6ad3804490db` (adds `docs/WASM-DIAGNOSTICS-REQUIREMENTS.md`, WASM-DIAG-001) |
| Baseline | `BL-CAUSA-INDY-INIT-78277924cc2b`, sha256 `acbb0996c7c54f6ce4f98a0834a3164ba5d2fc1e72ccb580e300e1955092bce3` (463 requirements) |
| Other repositories | the exact commits in `curation/sources.json`, the same as v1 |
| Compilation verdict | as recorded in `requirements-validation-report.json` (PASS when this package was committed) |
| Change record | [`docs/decisions/2026-10-27-WASM-DIAG-001-baseline.md`](../../docs/decisions/2026-10-27-WASM-DIAG-001-baseline.md) |

## Differences from v1's curation

Only two files differ from `v1/curation`:

- `sources.json`: the indy-init commit is `78277924…`, and `"package": "requirements-compiled/v2"` labels the manifest. v1's curation has no `package` field, so its label stays `requirements-compiled/v1`.
- `tasks.json`: one added work item, **T35**, "WebAssembly runtime diagnostics configuration and reproducible diagnostic evidence (WASM-DIAG-001)". It covers the new document, depends on T16 and T11, and is gated by G1. It is a planning entry for the next rehearsal. The agent session that compiled this package added it, and the owner can revise it.

Nothing in this package marks a requirement implemented or verified: every requirement is `not-verified` and every verification entry is `NOT_RUN`.

## Regenerate

Use the commands in [`v1/README.md`](../v1/README.md#regenerate), with `--curation requirements-compiled/v2/curation --out requirements-compiled/v2`. The compiler reads indy-init at the commit in `curation/sources.json`. The package is deterministic: two compilations produced byte-identical directories, and `--check` exits 0.
