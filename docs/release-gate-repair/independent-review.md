# Independent review of the release-gate repair

A reviewer agent reviewed the generator change, package v4.1.4 and the new r9 commands.
- It had a fresh context and implemented none of the change.
- It was spawned by the implementing session, not run as a separate session. The owner may require a separate-session or human review before approving v4.1.4 (FREEZE.md step 2).
- For that reason, no `analysis/independent-review.json` is written into the package.

## Round 1

**Generator and package: PASS.**
- `v41 --check` passes on 4.1.4, 4.1.2 and 4.1.1.
- The v4.1.2 and v4.1.4 plans are identical apart from `runner`: 11 suites, the same tests and bindings.
  - SU-BROWSER-WEBKIT keeps its 72 tests, and SU-DOTNET-LIVE its 141.
  - Each corrected filter selects exactly its suite's tests.
- The amendment names v4.1.2 by its real manifest sha256 and APR-V412-OWNER-3, and declares no contract change.

**r9 commands: FAIL.** Three blocking defects, two reproduced:
1. The scope level was ACCEPTED with no evidence.
2. The owner verdict was not bound to the candidate.
3. Sequencing missed a pre-checkpoint branch merged later.

Five major defects:
1. The preflights were substring-matched.
2. A partial or stale suite-preflight report was accepted.
3. A scope without requirements linked nothing.
4. Catalog requirements missing from the report were ignored.
5. The gate workflow hard-coded the v4.1.2 plan.

Minor defects: X21 argument parsing, the record half of mixed obligations, the checkpoint commit carrying implementation, root build files not counted as implementation, and FREEZE.md wording.

All of them are fixed, each with a regression case.

## Round 2

**PASS: nothing blocking.** The reviewer re-ran its reproductions; each is now refused. It raised four further findings, all fixed with regression cases:
- verifier-bound obligations needed the owner verdict;
- the candidate must be a full SHA, matched as a whole token;
- a merge commit that adds implementation is inspected;
- the `govern --lock` path is validated.

The harness now has 47 cases, all passing.
