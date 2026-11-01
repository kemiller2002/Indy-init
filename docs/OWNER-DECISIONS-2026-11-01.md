# Owner decisions, 2026-11-01

**Status:** OD-INDY-14 to OD-INDY-16 are in force from the merge of indy-init#65; OD-INDY-17 from the merge of the pull request that adds it.

**Decided by:** kemiller2002 (Kevin Miller), the repository owner, on 2026-11-01 in the Claude Code session https://claude.ai/code/session_01TG1BA3aoV14fyzeJCzeHDL. The owner's words were "do 1 and 2.. We'll get back to 3 later. From now on only F#", answering three options for the repository's Python tooling (OD-INDY-14 to OD-INDY-16). For OD-INDY-17 the owner chose "AG-13: approval record counts" when asked how AG-13's review condition is to be met.

**Recorded by:** an agent session (Claude Code). The agent wrote this record; it made none of the decisions.

## Decisions

| ID | Decision | Source |
|---|---|---|
| OD-INDY-14 | **Tooling is F# only.** New tooling, checks, generators and scripts in this repository are written in F# (`.fs`, `.fsx`, `.fsproj`). No new Python, shell or other scripting language is added. The F# offline suite enforces this: a tracked script in another language under `tools/` or `.github/` fails CI, unless it is one of the tools still to be ported (below). | "From now on only F#" |
| OD-INDY-15 | **The F# baseline generator is authoritative.** `requirements-compiler baseline ROOT COMMIT` (`tools/requirements-compiler/src/Baseline.fs`) replaces `tools/build-requirements-baseline.py`, which RDP-001 and OD-INDY-6 named. CI had shown the two produce byte-identical output; the Python generator and its tests are removed. Where a frozen package names the Python script, its F# replacement fulfils it: v4's preflight task T01 ("Regenerate and pin the baseline at the kickoff commit") runs `requirements-compiler baseline`, which is also the generator v4's own package manifest names. | Option 2: "do 1 and 2" |
| OD-INDY-16 | **Retired tools are removed:** `tools/discover-echelon-stack.py` and `tools/echelon-stack-sources.json` (superseded by the F# registry check PRE-DEP-01), `tools/compile-agent-context.py` (superseded by the ownership and lanes `requirements-compiler v41` generates), `tools/test-preflight.py` (their tests), and two committed Python cache files in `tools/__pycache__/`. | Option 1: "do 1 and 2" |
| OD-INDY-17 | **The owner's package approval record satisfies AG-13's "owner's approving review of the freeze pull request".** GitHub does not let the owner approve a pull request opened under the owner's own account, so that review cannot exist. For each frozen package, AG-13's review condition is met by the owner's package approval instead: an operator approval in `kickoff/rehearsal-007/approvals.causa.json`, committed by the owner without agent trailers, merged into `main`, whose digests match the package at the freeze tag. For v4 that is APR-V4-OWNER-3 (ODR-V4-01, #58; the tagged manifest's FG-05 verifies its digests); for v4.1 it is APR-V41-OWNER-4 (ODR-V41-01, #64; `owner-freeze.fsx tag v4.1` verifies its digests). AG-13's other conditions are unchanged: the tag exists, is on `main`, and names a freeze manifest whose plan digest equals the plan `verify-plan` used. The kickoff (T01) and acceptance apply AG-13 this way. The same holds for the "approve the pull request on GitHub" step in v4's and v4.1's FREEZE.md, whose texts stay as frozen and approved. | "AG-13: approval record counts" |

## Still to be ported to F# (option 3, deferred by the owner)

Until each is ported, these are the only non-F# tools, and the offline suite's allowlist names them:

- `tools/compute-acceptance-verdict.py` and `tools/test-acceptance-verdict.py`: the rehearsal acceptance verdict (OD-INDY-7);
- `tools/validate-acceptance-ledger.py` and `tools/test-acceptance-ledger.py`: the acceptance ledger's structural validation;
- `tools/process-evidence/simulate-rehearsal.sh`: the process-evidence simulation the F# process tests run.

Not tooling, and not affected: the executable layout assertions in `requirements-compiled/v4.1/layout-tests/` are JavaScript because they run inside the browser under Playwright.

## What does not change

Frozen package v4 (tag `indy-init-requirements-v4.0.0`) and the approved v4.1 package are unchanged; their texts that name the removed scripts stay as they were frozen and approved. Earlier packages (v1 to v3) and dated reports are history and are not edited.
