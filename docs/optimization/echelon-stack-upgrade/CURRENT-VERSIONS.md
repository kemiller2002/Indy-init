# Current and proposed Echelon versions

Observed 2026-11-05 (UTC). The **Installed** column shows two governed pins that disagree today:

- **app lock:** `requirements-compiled/v4/dependency-lock.json`, frozen, echelon-current 1.33.0. Every v4.1.x package uses v4 as its base.
- **toolchain lock:** `kickoff/rehearsal-010/toolchain.lock.proposed.json`, the Rehearsal 010 lock, echelon-current 1.37.0.

**Latest stable** is the newest published, non-prerelease release. It was established from the GitHub release or tag, npm or NuGet, and echelon-registry at `5752402` (see [RELEASE-EVIDENCE.json](RELEASE-EVIDENCE.json)).

**Status words used in this table:**

- **executed:** the check ran in this session and the evidence is cited.
- **not run:** the check did not run, and the reason is given.

| System | Installed: app lock / toolchain lock | Latest stable | Proposed | Compatibility | Action |
|---|---|---|---|---|---|
| Conditor | 0.10.18 / 0.10.20 | 0.10.20 (`v0.10.20`, 2026-11-03) | **0.10.20** | executed: install (digest = lock), `init --preset indy-init` (110 actions, exit 0), v4.1.5 layered and built | Align app lock; no Conditor release needed for versions. HF-029 needs a Conditor change (ODR-ESU-03) |
| Praxis | 3.24.0 / 3.25.0 | 3.25.0 (`v3.25.0`) | **3.25.0** | executed: installed binary reports `praxis 3.25.0`; Conditor ran `praxis init`, `praxis verify --strict`, `praxis mission … -> ready` | Align app lock (ODR-ESU-01) |
| Ordo | 1.19.0 / 1.20.0 | 1.20.0 (`v1.20.0`, 2026-11-02) | **1.20.0** | executed: installed binary reports `1.20.0` | Align app lock |
| Dokimos | 0.9.0 / 0.11.1 | 0.11.1 (`dokimos-v0.11.1`, 2026-11-04) | **0.11.1** | executed: installed binary reports ReleaseVersion 0.11.1, commit `fecbe0c` | Align app lock |
| percepta-semantic | 0.3.0 / 0.4.0 | 0.4.0 (`percepta-semantic-v0.4.0`) | **0.4.0** | executed: installed `percepta` reports 0.4.0+4a53688 | Align app lock |
| Percepta (repo tool) | not in app lock / 0.1.0 | 0.1.0 (`percepta-repo-v0.1.0`) | **0.1.0** (added to the lock) | executed: installed `percepta-repo` reports 0.1.0; Conditor materialized the 26 Percepta contracts | Add to the lock |
| Limen (npm) | 0.9.1 / 0.9.1 | 0.9.1 (`v0.9.1`, npm latest) | **0.9.1** (current) | executed: F# WASM preview on Limen 0.9.1 started and loaded real data (25/25) | None |
| Limen F# (`EchelonFoundry.Limen.*`) | 0.9.0 / 0.9.0 | 0.9.0 | **0.9.0** (current) | executed: artifact digest; v4.1.5 contracts reference `Limen.Contract` and build | None |
| Forma | **0.12.0** / 0.13.0 | 0.13.0 (`v0.13.0`, npm 2026-10-30) | **0.13.0** | executed: Forma's LRP-001 suite (Chromium) failed 16/26 on 0.12.0 and passed 26/26 on 0.13.0; the v4.1.5 layout contract passes on 0.13.0 (1,093/411/21, 0 failed); public surface unchanged | **Upgrade** (ODR-ESU-01, -02) |
| Folio | 0.4.1 / 0.4.1 | 0.4.1 (`v0.4.1`) | **0.4.1** (current) | executed: artifact digest; scaffold pins the 0.4.1 release asset | None |
| Aegis | 1.0.0 / 1.0.0 | 1.0.0 (NuGet, only version) | **1.0.0** (current) | executed: artifact digest; scaffold engine references it and builds | None |
| .NET SDK | 10.0.402 / 10.0.402 | (not an Echelon release) | **10.0.402** | executed: every build in this audit used 10.0.402 | None. Generated CI does not install it (HF-029) |
| Strata | not in app lock / installed by workstation | 0.1.1 | excluded (optional, never invoked) | executed: archive digest = registry | None |
| Arca | excluded (ADR-R7-001) | 0.7.0 | excluded | not run: unused | None |
| Fides | — | 0.2.0 | excluded | not run: unused | None |
| visual-engineering, communication-engineering, tutela | — | 1.0.1, 1.0.0, 0.1.0 | excluded | not run: neither the preset nor Indy Init installs or invokes them | None |
| Vigila, Iter, Vitium | — | no tags or releases | excluded | not run: no published release | None |
| Tutors | — | no reachable repository | excluded | not run: no repository | None |

## Package identities (proposed)

| System | Package | Artifact SHA-256 (downloaded and hashed in this session) |
|---|---|---|
| Forma | npm `@echelon-foundry/design-system@0.13.0` = GitHub `v0.13.0` asset | `2f9ceeb6ea99a54f6cf4ffa03e3a69fd609dd60a89ddbd99680811cca2d43725` |
| Limen | npm `@echelon-foundry/limen@0.9.1` | `a7dd300577a145ff56bfa2d8ebae21e864c00b1409e701ebb3704cc367d4ddc8` |
| Limen F# | `EchelonFoundry.Limen.Contract.0.9.0.nupkg` (GitHub `v0.9.0`) | `1db1ec2c19f45331b31fc0d13a876259e12efa4de7509f6ee4939567984c2817` |
| Folio | `echelon-foundry-print-components-0.4.1.tgz` (GitHub `v0.4.1`) | `57c6a0eccad41e6acef7c0b91a6e072276b0ebaddda27fbb3b36abd956aeea1b` |
| Aegis | NuGet `EchelonFoundry.Aegis.Core 1.0.0` | `24255d245463f1ce819ce98f10bc5f6a062f8b3342d0bf4eee00fef9a45a835d` |
| Praxis | `praxis-linux-x64.tar.gz` (`v3.25.0`) | `98e2ecaccac3f0502d755569bf2dddef852947b43a7989c570fcfff25f82d0c8` |
| Ordo | `ordo-linux-x64.tar.gz` (`v1.20.0`) | `c5851a5bead4145d6bb5bb56bfab67927c9db06c84960136f677bc94fc84e3cf` |
| Dokimos | `dokimos-linux-x64.tar.gz` (`dokimos-v0.11.1`) | `31578ce40622823c323cd2d9699aebd08c98503c59122d61b4bc98e6f06dc337` |
| percepta-semantic | `percepta-semantic-0.4.0-linux-x64.tar.gz` | `6c498b170818bbeeb0eae22da236007440d806c645e9e7a0acc1c0e0bcb67a34` |
| Percepta | `percepta-repo-linux-x64` (`percepta-repo-v0.1.0`) | `e46113aa08c9d2a97dfb1f75ca65c986946e1601c5f2c5cdcc9e8c25841d6039` |
| Conditor | `conditor-linux-x64` (`v0.10.20`) | `75849672f326b3f58dfe9e8de3a3a274ef4137a39d4e76bbe62889042ff326bd` |

Other platforms' digests are in the registry's resolved sets (`channels/echelon-current/*.json`) and in the toolchain lock (Conditor). They were not downloaded here.

## Newer than latest stable

- **Forma** `main` carries an unreleased **0.13.1 candidate**: marketing containment (WI-0043) and `Forma.Workflow.RequirementIdentity` (FMO-019). It is not used, because it is not published.
- **Conditor** `main` is 0.10.20 plus the qualification of echelon-current 1.37.0 (conditor#131, "no Conditor release"). No unreleased version change exists.
