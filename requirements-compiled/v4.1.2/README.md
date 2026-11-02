# Compiled package v4.1.2: the frozen-package defects resolved

**Status: INPUTS ONLY, pending owner decisions.**

- v4.1.2 is the successor amendment of v4.1.1, which the owner approved (APR-V411-OWNER-2) and which is not edited.
- This directory holds the authored inputs and the owner decision requests ODR-V412-01..04.
- It is compiled only after the owner records ODR-V412-01..03, because each state-machine amendment must cite the operator approval that selects it. See [FREEZE.md](FREEZE.md) and [AMENDMENT-4.1.2.md](AMENDMENT-4.1.2.md).

v4.1.2 changes three things and nothing else; v4.1.1's other content is carried unchanged:
- Every SM-SAVE reconciliation read, automatic or not, is sent only within INV-04's own bound of four in 60 s.
- The domain contract gets the command and event bodies FSHARP-TYPE-SHAPES specifies.
- The failed final automatic read gets a transition.

Compile it, after step 2 of FREEZE.md:

```
requirements-compiler v41 --source . --package requirements-compiled/v4.1.2 --base requirements-compiled/v4
```

The rest of the package layout, the deliverables and the regeneration steps are those of [v4.1.1](../v4.1.1/README.md) and [v4.1](../v4.1/README.md).
