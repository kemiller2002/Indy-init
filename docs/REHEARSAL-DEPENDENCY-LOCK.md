# Next Rehearsal Dependency Lock and Compatibility Gate

Status: baseline pinned facts plus **conditional candidate upgrades**. Do not claim upgraded packages are verified.

## Observed R001 dependency baseline
Source: indy-init-rehearsal-001/package.json and package-lock.json on default branch, read 2026-10-23.

| Component | Baseline | Decision for next rehearsal |
|---|---|---|
| Forma @echelon-foundry/design-system | 0.5.0 | Candidate 0.9.0; upgrade ONLY after matching foundations version, package integrity, build, visual and accessibility smoke are verified |
| Limen @echelon-foundry/limen | 0.9.0 | Retain 0.9.0 unless compatibility evidence requires change |
| Folio @echelon-foundry/print-components | GitHub release v0.3.0 tarball | Retain exact artifact; verify immutable integrity before use |
| TypeScript | 5.9.3 | Retain |
| Playwright | 1.63.0 | Retain |
| @axe-core/playwright | 4.13.0 | Retain |
| Praxis | 3.16.0 from .ros/installation.json | Retain installed baseline until compatibility validated |
| Causa storage | causa-storage-v1.0.0, commit c947c7e354be80766573a782546c8186336774cc | Immutable authoritative wire contract |
| .NET SDK | 10.0.402 per released storage contract | Require installed SDK verification and no incompatible roll-forward |

## Lock procedure (must pass before agent fan-out)
1. Resolve the Forma 0.9.0 + foundations peer/dependency compatibility from actual published manifests, not assumptions.
2. Produce a reproducible package-lock and checksum manifest for each downloaded package and GitHub tarball.
3. Run install with lockfile, .NET restore/build, F# unit tests, one Limen WASM startup smoke, one Forma rendering smoke, one Folio print smoke, and a keyboard/a11y smoke.
4. Capture command, SHA, artifact integrity, outcome, and environment for each.
5. Only after all gates PASS promote the candidate set to VERIFIED. Otherwise use baseline or mark BLOCKED; never silently mix versions.
6. Do not change the frozen storage schema or domain semantics to accommodate presentation dependency upgrades.

## Explicit unknowns
Forma 0.9.0 and foundations compatibility, Folio tarball integrity, and reproducible WASM/browser acceptance are not established by the baseline read. They require execution, not just documentation.

## Acceptance
One machine-readable lock manifest contains version, source URL, integrity digest, tested SHA, and verification status per component. CI fails on drift. No claim of VERIFIED before the compatibility gate runs.
