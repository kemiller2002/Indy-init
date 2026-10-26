# F# tooling assessment

**Policy:** all new Indy-init code is F# (.NET SDK 10.0.402, pinned in the root `global.json`).
- No Python was added.
- No Python dependency was added.
- No existing Python file was changed.

## F# tooling in the repository

**`tools/requirements-compiler/src`** is the compiled F# project. Its commands:

| Command | What it does |
|---|---|
| `requirements-compiler [options]` | Compiles `requirements-compiled/v1`. The baseline is now built in-process by the F# port, so compiling needs no Python. |
| `requirements-compiler baseline ROOT COMMIT` | Same contract as `tools/build-requirements-baseline.py`: same output bytes, exit codes and messages. |
| `requirements-compiler preflight [options] --evidence FILE` | Executes the Rehearsal 4 preflight checks and writes evidence. |

**`tools/requirements-compiler/tests`:**
- `--offline` checks that the baseline port reproduces the committed baseline. It also runs the unit cases ported from `test-requirements-baseline.py` and the preflight verdict rule. CI runs it.
- The full mode adds 18 negative and positive compiler cases, determinism, and the committed-package check.

**CI:** job `fsharp-tooling` in `.github/workflows/preflight-tooling.yml`.
1. Builds the F# projects with the pinned SDK.
2. Runs the offline tests.
3. Compares the F# and Python baselines byte for byte at the CI commit.

## Python utilities

| Utility | Purpose | F# replacement warranted? | Effort | Risks | Equivalence tests required | Needed before Rehearsal 4? |
|---|---|---|---|---|---|---|
| `build-requirements-baseline.py` | Drafts the authoritative requirements baseline | **Yes, done.** It was the only Python step in requirements compilation. | Done: 243 lines of F# (`Baseline.fs`) | Regex-dialect differences between Python and .NET | **Executed** (see below) | **No.** Python stays the authoritative command named in `requirements/README.md` and RDP-001 until the owner switches it; CI checks that both agree. |
| `compute-acceptance-verdict.py` | Computes the acceptance verdict from the ledger (OD-INDY-7, R4-04) | Not now | About 1 day: port about 175 lines, plus port the 133-line negative-control suite | It is the highest-stakes tool (it decides ACCEPTED); a semantic difference would invalidate a verdict | Byte-identical report line and the same exit codes on every `test-acceptance-verdict.py` case, plus the Rehearsal 2 ledger fixture | **No.** It works, is tested in CI, and runs on GitHub runners, which have Python. Migrate after the event. |
| `validate-acceptance-ledger.py` | Structural ledger validation | Not now; port together with the verdict tool | About 0.5 day | Low | Same violations list and exit code on `tools/fixtures/rehearsal-002-ledger.json` and the ledger tests | No |
| `discover-echelon-stack.py` (+ `echelon-stack-sources.json`) | Discovers "latest" releases from npm and GitHub | **No; retire.** It is superseded by the F# registry check PRE-DEP-01, which reads the approved Registry set. | None | Rehearsal 2 DEF-STACK-004: its source list named repositories that do not exist and treated "latest release" as approved | None | No. Recommend the owner retire it. |
| `compile-agent-context.py` | Compiles bounded agent assignments for parallel fan-out | No | — | Plan B uses one lead agent, so its main use is gone | — | No |
| `test-*.py` | Tests for the tools above | Port with the tool each one tests | — | — | — | No |

## Baseline port equivalence (executed)

Evidence: `evidence/baseline-equivalence.json`.

- **Outputs:** byte-identical at commits 002d8ba, 598a759, 252cd2e and b785b68. The proposed requirement branch (2edaea4) was also identical.
- **Committed baseline:** the F# output at 002d8ba reproduces the committed `requirements/causa-requirements-baseline.json` exactly. That is the offline CI oracle.
- **Error paths:** the same exit codes and messages for an invalid commit (2), a missing argument (2), an unreadable root (1), a duplicate requirement id (1), and a requirement without obligation text (1).

## Remaining Python utilities

Python runtime use for Rehearsal 4 is now limited to the verdict and ledger tools. The repository and its owner name those tools as authoritative.
