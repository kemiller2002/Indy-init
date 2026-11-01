# v4.1.1 freeze procedure

**Status: CANDIDATE.** v4.1.1 is the successor amendment of v4.1 ([AMENDMENT-4.1.1.md](AMENDMENT-4.1.1.md), [analysis/amendment.json](analysis/amendment.json)). It is compiled and validated, with checks X01–X20 passing. It is not approved and not frozen; [package-manifest.json](package-manifest.json) always reads `CANDIDATE`. Its independent review record is in [independent-review.md](independent-review.md).

## Dependency on v4 and v4.1

v4 is frozen at tag `indy-init-requirements-v4.0.0`. v4.1 is approved (ODR-V41-01, APR-V41-OWNER-4, #64) and is not edited by this amendment: X20 checks that v4.1.1 names v4.1's approved manifest digest (`779916ab…31736a`), and CI checks that v4.1 still compiles to exactly the approved files. The owner's decisions ODR-V41-02..09 carry over unchanged; v4.1.1 asks for one new decision, ODR-V411-01.

## What the owner does to freeze v4.1.1

1. **Read** [AMENDMENT-4.1.1.md](AMENDMENT-4.1.1.md), [STARTING-STATE.md](STARTING-STATE.md) and [independent-review.md](independent-review.md).
2. **Record ODR-V411-01** with `dotnet fsi tools/owner-freeze.fsx -- approve v4.1.1`. The record names the SHA-256 of `package-manifest.json` and of `analysis/independent-review.json`. The script commits it and opens the pull request; merge it.
3. **Tag the merge commit** with `dotnet fsi tools/owner-freeze.fsx -- tag v4.1.1 --commit <merge commit>`, which creates `indy-init-requirements-v4.1.1` after checking that the record binds that commit's files.

Until then, the November 7 build follows v4.1 as approved. It may use v4.1.1 as guidance only if it reports v4.1.1 as unapproved.
