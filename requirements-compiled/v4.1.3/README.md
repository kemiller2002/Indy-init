# Compiled package v4.1.3: the release-gate integration defects resolved

**Status: CANDIDATE.** Not approved and not frozen.

- v4.1.3 is the successor amendment of v4.1.2, which the owner approved (APR-V412-OWNER-3) and tagged (`indy-init-requirements-v4.1.2`). v4.1.2 is not edited.
- It is compiled from two decision requests that are **not yet recorded**: ODR-V413-04 and ODR-V413-05 (`proposals/owner-decision-requests.json`), each drafted with option A. ODR-V413-06, the approval of this package, is pending. See [FREEZE.md](FREEZE.md) and [AMENDMENT-4.1.3.md](AMENDMENT-4.1.3.md).
- Validation: X01–X21 pass (`validation-report.json`). X21 is new: every `dotnet test` runner names exactly one project or solution.

v4.1.3 changes these things and nothing else; v4.1.2's other content is carried unchanged:
- SU-OWN-DOTNET and SU-DOTNET-LIVE run one `dotnet test Causa.slnx` each, filtered by the namespaces that already assign their tests. v4.1.2 passed several projects to one `dotnet test`, which SDK 10.0.402 refuses (MSB1008).
- The browser suites' Playwright projects (`chromium`, `webkit`) are a declared prerequisite (`analysis/amendment.json` `suitePrerequisites`), checked by `tools/r9/r9.fsx suite-preflight` at T10 and before T0.

No requirement, obligation, test identity, test binding, state machine or contract changes (X10, X17).

Compile it:

```
requirements-compiler v41 --source . --package requirements-compiled/v4.1.3 --base requirements-compiled/v4
```

The rest of the package layout, the deliverables and the regeneration steps are those of [v4.1.2](../v4.1.2/README.md), [v4.1.1](../v4.1.1/README.md) and [v4.1](../v4.1/README.md).
