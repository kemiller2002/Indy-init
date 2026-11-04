# Amendment 4.1.3: the release-gate integration defects Rehearsal 009 hit

v4.1.3 is the successor of the approved, frozen package v4.1.2 (APR-V412-OWNER-3, tag `indy-init-requirements-v4.1.2`). v4.1.2 is not edited.

It applies two decision requests, ODR-V413-04 and ODR-V413-05, **which the owner has not recorded**. The package is compiled with option A of each so that the owner can review exactly what A produces. Decision ids ODR-V413-01..03 belonged to the v4.1.3 route that OD-INDY-19 withdrew; they are not reused.

| Decision | Defect (Rehearsal 009) | Change |
|---|---|---|
| ODR-V413-04: runners that can execute | `SU-OWN-DOTNET` (`dotnet test tests/Causa.Contract.Tests tests/Causa.Domain.Tests tests/Causa.Persistence.Tests tests/Causa.App.Tests`) and `SU-DOTNET-LIVE` (`dotnet test tests/Causa.Acceptance tests/Causa.Invariants --filter …`) fail on SDK 10.0.402 with `MSB1008: Only one project can be specified`, on any tree. The release gate's `run-suites` step failed on every Rehearsal 009 run (41756889999, 41758805409). | `dotnet test Causa.slnx --filter "FullyQualifiedName~Causa.Contract.Tests.\|…Domain.Tests.\|…Persistence.Tests.\|…App.Tests."` and `dotnet test Causa.slnx --filter "FullyQualifiedName~Causa.Acceptance.Live.\|FullyQualifiedName~Causa.Invariants.Live."`. `Causa.slnx` is a required bootstrap file (X02). The filters are the suites' own assignment rules (X09), so no test moves between suites. |
| ODR-V413-05: browser projects are a startup prerequisite | `SU-BROWSER-WEBKIT` runs `--project=webkit`. The application defined no `webkit` project, and nothing checked this before the release gate. | `suitePrerequisites` names the Playwright project of each browser suite. `r9.fsx suite-preflight` lists every executable suite with its own command (no test is executed) and refuses a command the runner rejects or a project the configuration lacks. It is a T10 completion condition and a pre-T0 readiness check (docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md). |

## How the change is checked

- **X21** (new, from 4.1.3): a replaced runner names an existing suite, its decision and the same runner kind; every `dotnet test` runner names exactly one project or solution. v4.1.2's runners fail X21 (see the compiler control in the offline suite).
- **X09, X10** unchanged: every binding is in exactly one suite; no requirement, obligation or binding changes.
- **X17**: every contract equals v4.1.2's byte for byte (`contractChanges` is empty).
- **Executed:** the corrected commands ran on a deterministic fixture tree with SDK 10.0.402 and Playwright 1.63.0 (docs/rehearsal-010-readiness/evidence/suite-commands/).

## Not changed

The Dokimos `url-input` mismatch (INV-029) is a tool defect, not a package defect. It is fixed in Dokimos, not here.
