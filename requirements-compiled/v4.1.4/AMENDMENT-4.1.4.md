# Amendment 4.1.4: suite runners that execute

v4.1.4 is the successor of the approved package v4.1.2 (APR-V412-OWNER-3, tag `indy-init-requirements-v4.1.2`). v4.1.2 is not edited, and v4.1.3 is not used (the withdrawn proposal).

| Decision | Defect (Rehearsal 009 release gate) | Change |
|---|---|---|
| ODR-V414-01 | SU-OWN-DOTNET (`dotnet test tests/Causa.Contract.Tests tests/Causa.Domain.Tests tests/Causa.Persistence.Tests tests/Causa.App.Tests`) and SU-DOTNET-LIVE (`dotnet test tests/Causa.Acceptance tests/Causa.Invariants --filter ...`) exit 1 before any test runs on SDK 10.0.402: `MSB1008: Only one project can be specified`. The generator (`V41Package.suites`) has carried both since v4.1, and no check executed them. | One `dotnet test Causa.slnx` with `--filter "FullyQualifiedName~Causa.Contract.Tests.\|…Domain.Tests.\|…Persistence.Tests.\|…App.Tests."` (SU-OWN-DOTNET) or `"…Acceptance.Live.\|…Invariants.Live."` (SU-DOTNET-LIVE). These are the same namespaces each suite's matcher assigns, so tests, bindings, fixtures and environments are unchanged. |
| ODR-V414-02 | SU-BROWSER-WEBKIT (`npx playwright test --project=webkit …`) exits 1: `Project(s) "webkit" not found. Available projects: "chromium"`. The ownership manifest plans the integration-owned `playwright.config.ts`, but the bootstrap never wrote it, and Conditor's scaffold `playwright.config.js` defines only chromium. | The bootstrap writes `playwright.config.ts` with one project per engine the runners name (chromium on Desktop Chrome, webkit on Desktop Safari). The four Playwright runners select it with `--config playwright.config.ts`. WebKit stays mandatory. |
| ODR-V414-03 | — | Approval of this package. |

## How the change is checked

- **X21** runs for every package whose amendment declares `suiteRunners` or a bootstrapped Playwright configuration. It requires:
  - a `dotnet test` runner names exactly one target, either `Causa.slnx` or a declared test project;
  - a Playwright runner selects the bootstrapped configuration and names only projects it defines;
  - every corrected runner names its suite and its authorizing decision.
- **`r9.fsx suite-preflight`** executes every runner of the compiled plan in list mode against a repository. It runs `--list-tests` for .NET and `--list --pass-with-no-tests` for Playwright, and refuses readiness when one cannot execute.
  - On the Rehearsal 009 application, v4.1.2 is REFUSED with exactly the three release-gate causes.
  - v4.1.4 is PASS, with all 8 executable suites listed.
- **v4.1.1 and v4.1.2** still regenerate byte for byte (`v41 --check`). Packages that declare no `suiteRunners` keep v4.1's runners.

Every other part of v4.1.2 is carried unchanged.
