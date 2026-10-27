# Change record: WASM-DIAG-001 and the baseline after Rehearsal 4

**Date:** 2026-10-27

**Branch:** `requirements/wasm-diag-001` (not merged; review it before it reaches `main`).

**Recorded by:** an agent session (Claude Code), carrying out Phase 5 of the D-HIST-WASM remediation brief. The brief told it to add this requirement. This record documents a change to the requirements. It is not an approval of any kind (RDP-003): it approves no exclusion, supersession or not-applicable disposition, and no requirement is marked implemented or verified.

## What changed, and why

The remediation brief for defect D-HIST-WASM, found after Rehearsal 4, calls for a requirement REQ-WASM-DIAG-001 on runtime diagnostics for the .NET WebAssembly engine. This repository does not describe the defect itself; the brief is the authority for it.

1. **New requirement.** [`docs/WASM-DIAGNOSTICS-REQUIREMENTS.md`](../WASM-DIAGNOSTICS-REQUIREMENTS.md) adds **WASM-DIAG-001: Runtime diagnostics for the .NET WebAssembly engine**.
   - Identifier. The repository uses document-specific ids, so the brief's REQ-WASM-DIAG-001 is WASM-DIAG-001; the document says so.
   - Requirement pattern: `^## (WASM-DIAG-\d{3}):`.
   - Not an extension of an existing requirement. No existing requirement covers runtime diagnostics tooling. A search for EventPipe, heap dumps, EnableDiagnostics and runtime diagnostics found none. UI-018 (Administration page) shows operational information to users; it is not engine diagnostics.
2. **Generator document lists.** The document is added, after `AGENT-RELIABILITY-REQUIREMENTS.md`, to `DOCUMENTS` in `tools/build-requirements-baseline.py` and to the F# port in `tools/requirements-compiler/src/Baseline.fs`. It is also added to the document table in [`requirements/README.md`](../../requirements/README.md). No rule, exclusion or token pattern changed.
3. **Baseline re-pin (RDP-001).** `requirements/causa-requirements-baseline.json` was regenerated with the documented command. It was not edited by hand.

## Baseline identities

| | Previous (Rehearsal 4, frozen) | New |
|---|---|---|
| Baseline id | `BL-CAUSA-INDY-INIT-aab8f80c7743` | `BL-CAUSA-INDY-INIT-78277924cc2b` |
| Pinned commit | `aab8f80c7743392e794b3b631542903ed4ed6f64` | `78277924cc2bfceb7c8bcfaeb55b6ad3804490db` |
| SHA-256 | `621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9` | `acbb0996c7c54f6ce4f98a0834a3164ba5d2fc1e72ccb580e300e1955092bce3` |
| Documents | 19 | 20 |
| Requirements | 461 | 463 |
| Obligations | 2293: 378 acceptance, 55 value, 1860 constraint | 2305: 379 acceptance, 55 value, 1871 constraint |

**Commands.** The new baseline was produced by:

```bash
python3 -I tools/build-requirements-baseline.py . 78277924cc2bfceb7c8bcfaeb55b6ad3804490db > requirements/causa-requirements-baseline.json
```

**Determinism and equivalence.**
- Two runs were byte-identical.
- The F# port (`requirements-compiler baseline . <commit>`) produced the same bytes.
- The compiler's offline tests pass. They rebuild the baseline at the pinned commit from git and compare it with the committed file.

**What changed in the baseline.** Comparing the two baselines:
- No requirement was removed.
- No existing requirement or obligation changed.
- No existing document hash changed.
- The only additions are the new document and its two requirements, WASM-DIAG-001 and `WASM-DIAGNOSTICS§implementation`. The second is the document's "Implementation" section, which the generator inventories exactly as it does `WASM-LOADING-EXPERIENCE§implementation`.

## Rehearsal 4 is preserved and unaffected

- **Frozen copy of the baseline.** The Rehearsal 4 baseline is kept byte-identical at [`requirements/frozen/BL-CAUSA-INDY-INIT-aab8f80c7743.json`](../../requirements/frozen/BL-CAUSA-INDY-INIT-aab8f80c7743.json). Its SHA-256 was checked: `621e2f96…0857b9`, equal to the file at `1ede27f`.
- **v1 package untouched.** `requirements-compiled/v1/` is not modified on this branch.
- **Status of the document.** The new document says it does not apply to Rehearsal 4 and does not change Rehearsal 4's acceptance. Rehearsal 4's evidence names its baseline by id and SHA-256. Both still resolve to the same bytes.
- **Reproducing the historical artifacts.** Use the tools at the historical commit. The generator's document list is in code, so the tools on this branch expect `docs/WASM-DIAGNOSTICS-REQUIREMENTS.md`, which does not exist at `aab8f80`. Run on this branch, the compiler's v1 `--check` exits 2 (input error: the document is unreadable at `aab8f80`). With the tools at `1ede27f`, both artifacts reproduce:
  - `python3 -I tools/build-requirements-baseline.py . aab8f80c7743392e794b3b631542903ed4ed6f64`, run in a worktree at `1ede27f`, gives SHA-256 `621e2f96…0857b9`.
  - The requirements-compiler, built at `1ede27f`, run with `--check` against `requirements-compiled/v1` (same options as its README), exits 0.

  A lasting fix would make the document list depend on the source commit, for example by reading it from the tool at that commit. That is a compiler design change; it is not made here.

## Effect on acceptance

- **Scope.** One new normative requirement, WASM-DIAG-001, plus its document's Implementation section. Any baseline-derived rehearsal from this pin on must give both a disposition (RDP-002).
- **Priority.** The generator classifies it as `unstated`, because the heading carries no `[P#]` marker. It is therefore not counted as P0 (V17).
- **Obligations.** The generator split WASM-DIAG-001 into ten obligations:
  - O1 to O9 are `constraint`.
  - O10 is `acceptance`. The keyword rule matched "acceptance pipeline"; this is mechanical and was not adjusted by hand.
  - No obligation carries a value token. Each one needs a recorded human equivalence review, or an operator-approved disposition, before the preservation gate accepts it.
- **Status.** Nothing is implemented or verified. In any future acceptance verdict, WASM-DIAG-001 is NOT_RUN until evidence for it exists. A preservation PASS would not count as that evidence (RDP-005).

## Compiled package v2: not produced (blocked)

The new package was to be compiled by the F# requirements-compiler into `requirements-compiled/v2/`, pinned to `78277924cc2b`. The compiler supports this with `--out`.

**Curation.** The curation for the attempt was v1's, copied, with only `sources.json` → `indy-init.commit` changed to `78277924cc2bfceb7c8bcfaeb55b6ad3804490db`. The command was the v1 README's step 2, with `--curation requirements-compiled/v2/curation`.

**Result.** The compiler reports compilation **FAIL** (exit 3); rehearsal readiness is READY. One check fails:
- V09 FAIL: "WASM-DIAG-001 is in no work item" and "WASM-DIAGNOSTICS§implementation is in no work item".
- Every other check passes: V01–V08, V10–V13, V15–V17, and V14 (no baseline drift).

**Determinism.** Two compilations were byte-identical, and two `--check` runs found no differing file. Both `--check` runs exit 3 because the verdict is FAIL.

**Why it was not committed.** Passing V09 needs the curation's `tasks.json` to place the new requirement in a work item, which is a planning judgement. v1's tasks are the Rehearsal 4 plan. For example, T16 covers `doc:docs/WASM-LOADING-EXPERIENCE-REQUIREMENTS.md`, but WASM-DIAG-001 explicitly does not apply to Rehearsal 4. Adding it to a task only to make the check pass would be forcing the check, so no v2 directory was committed.

**Two decisions needed before v2 can be published:**
1. Which work item covers WASM-DIAG-001 and `WASM-DIAGNOSTICS§implementation`. It could be a new task, or an existing one with its own completion conditions and gates. This is a v2 `tasks.json` change.
2. The package label. The compiler writes `"package": "requirements-compiled/v1"` into `package-manifest.json` whatever `--out` is (`tools/requirements-compiler/src/Program.fs`). A v2 package would mislabel itself until that string comes from an input. One option is an optional `package` field in `curation/sources.json` that defaults to v1, so v1's bytes stay the same.
