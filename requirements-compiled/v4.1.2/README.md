# Compiled package v4.1.2: the frozen-package defects resolved

**Status: CANDIDATE.**

- v4.1.2 is the successor amendment of v4.1.1, which the owner approved (APR-V411-OWNER-2) and which is not edited.
- It is compiled from the owner's decisions ODR-V412-01..03 (APR-V412-OWNER-1) and ODR-V412-05 (APR-V412-OWNER-2), option A each, and validated: X01–X20 pass.
- ODR-V412-04, the approval of this package, is pending. See [FREEZE.md](FREEZE.md) and [AMENDMENT-4.1.2.md](AMENDMENT-4.1.2.md).

v4.1.2 changes these things and nothing else; v4.1.1's other content is carried unchanged:
- Every SM-SAVE reconciliation read, automatic or not, is sent only within INV-04's own bound of four in 60 s.
- The domain contract gets the command and event bodies FSHARP-TYPE-SHAPES specifies.
- The failed final automatic read gets a transition.
- The automatic reads are scheduled on every path into reconciliation, including the return online (ODR-V412-05).

Compile it:

```
requirements-compiler v41 --source . --package requirements-compiled/v4.1.2 --base requirements-compiled/v4
```

The rest of the package layout, the deliverables and the regeneration steps are those of [v4.1.1](../v4.1.1/README.md) and [v4.1](../v4.1/README.md).
