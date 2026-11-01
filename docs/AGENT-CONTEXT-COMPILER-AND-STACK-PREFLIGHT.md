# Agent Context Compiler and Current Stack Preflight

Status: **retired 2026-11-01.** `tools/compile-agent-context.py`, `tools/discover-echelon-stack.py`, `tools/echelon-stack-sources.json` and `tools/test-preflight.py` were removed ([OWNER-DECISIONS-2026-11-01](OWNER-DECISIONS-2026-11-01.md)): stack discovery is superseded by the F# registry check PRE-DEP-01, and agent assignments by the ownership and lanes `requirements-compiler v41` generates. `tools/validate-acceptance-ledger.py` remains until it is ported to F#. The rest of this document is kept as history.

## Tools
- `tools/compile-agent-context.py`: source-pinned, bounded assignment generator. Takes JSON manifest with schema_version=1, source_sha (40-char commit), interface_manifest_sha, dependency_lock_sha, sources [{path,sha256}], and agents [{name,requirement_ids,owned_paths,required_inputs,allowed_decisions,prohibited_decisions,outputs,acceptance_tests}]. Requirement headings must have exactly one matching canonical ID in pinned sources. Rejects source drift, duplicate agent names, missing requirements, path traversal and overlapping owned paths. Emits one JSON packet per agent.
- `tools/discover-echelon-stack.py`: latest stable release discovery via npm dist-tag and GitHub releases/latest. Uses explicit source inventory. Emits machine-readable discovery status and BLOCKED records. Discovery does NOT establish integration compatibility or artifact provenance.
- `tools/echelon-stack-sources.json`: initial candidate source map, subject to validation. GitHub repositories without stable releases or missing access are BLOCKED. Must expand to every in-scope Echelon system, including foundations and toolchains, before calling inventory complete.
- `tools/validate-acceptance-ledger.py`: existing structural evidence ledger validator.
- `tools/test-preflight.py`: offline positive/negative tests. CI workflow added.

## Commands
```sh
python3 tools/test-preflight.py
python3 tools/test-acceptance-ledger.py
python3 tools/discover-echelon-stack.py tools/echelon-stack-sources.json --out artifacts/stack-discovery.json
python3 tools/compile-agent-context.py path/to/assignment-manifest.json --root . --out artifacts/agent-packets
```

## Mandatory integration steps
1. Resolve and verify every source in the discovery inventory. Some Echelon repos may publish via other channels; configure the actual authoritative channel, not a guessed GitHub latest release.
2. Add Forma foundations, F#/SDK, Folio, NuGet, and all actual in-scope integrations. Pin artifacts by digest and test compatibility as a set. No silent fallback.
3. Generate source manifest checksums and actual interface/dependency lock SHAs from verified build artifacts. Do not invent placeholders.
4. Add a parser/validator for agent output and decision escalations, compare actual work to allowed owned paths and requirements.
5. Integrate acceptance ledger with real CI job/artifact existence and exact deployed SHA checks. Current validator checks structural references, not proof authenticity.
6. Before the next experiment, execute end-to-end dry run and compare a context-compiled arm against a controlled baseline using identical tasks and independent correctness grading.

## Experimental hypothesis
Bounded, authority-pinned context reduces agent decisions, tokens, interface conflicts, rework, and defects. Measure total tokens including retries, completion correctness, integration time, defect recurrence, and context size. Do not interpret reduced context size alone as a win.

## Fail-closed principles
No implicit requirement inference; no conflicting source selection; no unpublished release assumed latest; no fabricated verification; no unapproved downgrade; no claim of a completed experiment.
