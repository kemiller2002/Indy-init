# Version-drift preflight: results and negative controls

**Tool:** `tools/stack-drift/stack-drift.fsx`. It is F# only and has no package dependencies. Its checks are pure functions over observations that the IO layer gathers explicitly.

**It compares the dependency lock with:**

1. echelon-registry at the lock's commit: the set digest and status, each release manifest's digest and stage, and each version against the set's version.
2. The toolchain lock: profile version, registry commit, expected tools, and the Conditor version.
3. The pinned SDK (`global.json` version and `rollForward`) and the installed SDK (`dotnet --version`).
4. The installed Echelon executables (`--version` of praxis, ordo, dokimos, percepta, percepta-repo).
5. The Conditor binary's digest against the toolchain lock artifact for this platform.
6. The published artifacts, downloaded from their declared distribution and hashed.
7. Every workspace:
   - `package.json` declared versions; floating ranges are DRIFT.
   - Installed `node_modules` versions.
   - `conditor.json` components.
   - `EchelonFoundry.*` PackageReferences; an Echelon package outside the lock is BLOCKED.
   - Workflow download pins.
   - `setup-dotnet` steps against `global.json`.

| Status | Meaning |
|---|---|
| PASS | Matches the lock |
| DRIFT | A version differs from the lock, or floats |
| BLOCKED | Missing, incompatible, an Echelon package outside the lock, or an artifact whose digest differs |
| UNVERIFIED | The version or provenance cannot be established: no registry checkout, no version output, or an artifact that could not be obtained |

A required system that is not PASS gives **NOT READY** and exit code 3. Optional systems are reported but do not block. The tool never substitutes a version. Each finding names the system, the expected value, the actual value and the correction.

## Self-test (executed)

`dotnet fsi tools/stack-drift/stack-drift.fsx -- test --registry <echelon-registry>` → **30/30 PASS** in 5 s ([evidence/stack-drift-test.log](evidence/stack-drift-test.log)). The tests include:

- **Equivalence control:** the lock generator reproduces the frozen `requirements-compiled/v4/dependency-lock.json` from its source at registry `7a2c27a` (deep JSON equality, no problems). It is therefore the same algorithm as `V4Package.dependencyLock`, and the frozen compiler is not edited.
- **Negative controls:**
  - floating specifiers
  - invented versions
  - praxis 3.24.0 against 3.25.0
  - a missing tool
  - an executable that reports no version
  - SDK 10.0.403, no `dotnet`, and a rolling-forward `global.json`
  - Forma 0.12.0, Forma `^0.13.0` and node_modules 0.13.1
  - an unlisted `@echelon-foundry` package, and Arca outside the lock
  - a workflow downloading Forma 0.12.0
  - `setup-dotnet 10.0.x` with `rollForward: disable`
  - a Conditor manifest naming 0.12.0
  - a tampered artifact and an unobtainable artifact
  - a lock version that is not the set's version
  - a toolchain lock on 1.36.0 or Conditor 0.10.18, a Conditor binary of another digest, and a toolchain-lock tool outside the lock
  - a prerelease, an unrecorded digest or a forbidden framework in lock generation

## Real runs (executed, 2026-11-05)

Logs and JSON reports are in [evidence/](evidence/). Paths there are anonymized as `$SCRATCH` and `$HOME`.

| Run | Inputs | Result | Time |
|---|---|---|---|
| DP-01 | Proposed lock; registry `5752402`; R10 toolchain lock; the real Conditor 0.10.20 scaffold with v4.1.5 layered; installed binaries; Conditor binary; artifacts **downloaded** | PASS 44, **DRIFT 2** (CA-01: `setup-dotnet 10.0.x` in `build-and-test.yml:29`, `candidate-acceptance.yml:66`). NOT READY, exit 3 | 8 s |
| DP-02 | As DP-01 with CA-01 corrected (`global-json-file: global.json`) | **PASS 46. READY**, exit 0 | 5 s |
| DP-03 | Proposed lock against the Indy-init repository itself | PASS 50, DRIFT 7 (classified below). Exit 3 | 5 s |
| DP-04 | **Frozen** v4 lock (1.33.0) against what the R10 toolchain lock installs | PASS 21, **DRIFT 11**. Exit 3 | 4 s |
| DP-05 | Proposed lock against the recorded Conditor 0.10.18 scaffold fixture | DRIFT 4 (Forma 0.12.0 ×2, praxis 3.24.0, ordo 1.19.0). Exit 3 | 5 s |
| DP-06 | **Negative:** scaffold with design-system `0.99.0` and limen `^0.9.1`; `global.json` 10.0.403 | DRIFT 3 (`global.json`, 0.99.0, floating range) and BLOCKED 1 (the SDK host refuses 10.0.403: "exit 155"). Exit 3 | 5 s |
| DP-07 | **Negative:** lock with Forma's artifact digest replaced by zeros | BLOCKED 1 (the downloaded artifact hashes to `2f9ceeb6…`). Exit 3 | 5 s |
| DP-08 | **Negative:** no installed tools | BLOCKED 5 (praxis, dokimos, ordo, percepta, percepta-repo). Exit 3 | 4 s |
| DP-09 | **Negative:** no registry checkout | UNVERIFIED 10 (provenance cannot be established). Exit 3 | 5 s |

Exit codes reach the caller unchanged. The runs were driven by a shell that recorded `$?` (see each line's `exit=` in this session's transcript; the JSON report's `ready` field matches).

### DP-03 findings classified

| Finding | Classification | Action |
|---|---|---|
| `.github/workflows/preflight-tooling.yml:120` downloads Forma 0.12.0 | **Correct as is.** It reproduces the frozen v4.1 package byte for byte (`--forma-tarball … --check`) | None. A successor package adds a 0.13.0 step for itself (REQ-ESU-03) |
| `tools/requirements-compiler/tests/fixtures/conditor-0.10.18-scaffold` (Forma 0.12.0) | Stale fixture (CA-03) | REQ-ESU-05 |
| `proposals/r10-contract-first-ui/prototype` (Forma 0.12.0) | Proposal prototype, not competition code | None |
| `preview/causa/web` (Forma 0.10.0, HF-085) | Preview, not competition code (OD-INDY-22) | Optional. Executed on 0.13.0 in scratch: build, 25/25 read-path, 31/31 engine tests |
| `.github/workflows/offline-factory-rehearsal.yml` (Forma and Folio 0.3.0, `setup-dotnet 10.0.x`) | Tests the historical `indy-init-0.1.0` registry freeze on purpose | None |

### DP-04: today's split between the two governed locks

The frozen app lock pins Praxis 3.24.0, Dokimos 0.9.0, percepta-semantic 0.3.0, Ordo 1.19.0, Forma 0.12.0 and Conditor 0.10.18. The R10 toolchain lock installs 3.25.0, 0.11.1, 0.4.0, 1.20.0, 0.13.0 (scaffold) and 0.10.20. A competition agent therefore gets Forma 0.13.0 from Conditor, but the package's layout contract inventories Forma 0.12.0. This is the drift ODR-ESU-01 resolves.

The frozen lock carries no `executable` fields, so its installed-tool rows are judged through the toolchain lock rows above.
