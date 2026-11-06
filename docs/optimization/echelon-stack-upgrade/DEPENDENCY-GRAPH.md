# Echelon dependency graph of Indy Init

**Sources inspected:**

- `requirements-compiled/v4/dependency-lock.json`, its source, and the v4.1.5 candidate (`analysis/project-structure.json`, `contracts/*.fs`).
- `kickoff/rehearsal-010/toolchain.lock.proposed.json` and `.github/workflows/*.yml`.
- The scaffold that the released Conditor 0.10.20 actually generated (`conditor init --preset indy-init`, executed), with v4.1.5 layered onto it (executed).
- echelon-registry `5752402`.

"Invoked" means a command, build or test in this session called the system. "Mentioned" means it appears only in documents.

## Graph

```
Conditor 0.10.20 (installer; pinned by the toolchain lock, not in the set it installs)
 ├─ workstation (echelon-current 1.37.0, linux-x64) ── installs ─► praxis 3.25.0, ordo 1.20.0, dokimos 0.11.1,
 │                                                               percepta (semantic) 0.4.0, percepta-repo 0.1.0, strata 0.1.1
 ├─ init --preset indy-init ── writes ─► conditor.json (praxis, ordo, percepta, aegis, limen, forma, folio)
 │                                       package.json (design-system 0.13.0, limen 0.9.1, print-components 0.4.1 asset)
 │                                       src/engine (EchelonFoundry.Aegis.Core 1.0.0), .github/workflows (5)
 │                           ── runs ─► praxis init, praxis verify --strict, praxis mission COND-MISSION-001
 │                           ── fetches ─► Indy-init kickoff docs @2eee2d5, Percepta contracts @9e63077
 └─ ci-policy check / acceptance preflight (used by r9.fsx, launch-preflight)

requirements-compiler v41 --bootstrap (v4.1.5) ── layers onto the scaffold ─► global.json (SDK 10.0.402), Causa.slnx
 └─ contracts reference EchelonFoundry.Limen.Contract (limen-fsharp 0.9.0); layout contract ─► Forma inventory

Causa application (written at kickoff)
 ├─ runtime: Limen 0.9.1 (browser kernel) ─► .NET 10 WASM ─► F# engine
 ├─ runtime: Forma (CSS, zero JS runtime), Folio (print CSS)
 ├─ runtime: causa-storage 1.0.0 wire contract; causa-data demonstration-v1 (read); GitHub REST/GraphQL (write)
 └─ engine scaffold: Aegis.Core 1.0.0

Verification: Dokimos (gates), Praxis (work items, traceability), percepta-semantic (semantic checks), Playwright 1.63.0
```

## Classification

| System | Class | Direct or transitive | Invoked in this audit | Required |
|---|---|---|---|---|
| Conditor | Build (bootstrap, CI policy) | direct | yes: install, `init`, workstation apply | mandatory |
| .NET SDK 10.0.402 | Build | direct | yes: every build | mandatory |
| Limen (npm) | Runtime | direct (scaffold `package.json`) | yes: preview WASM start | mandatory |
| Limen F# (`Limen.Contract`) | Runtime | direct (v4.1.5 contracts) | yes: Causa.slnx build | mandatory |
| Forma | Runtime | direct | yes: LRP-001 suite, layout contract, token validator, preview | mandatory |
| Folio | Runtime | direct (scaffold `package.json`) | download and digest only | mandatory (scaffolded) |
| Aegis | Runtime | direct (scaffold engine) | yes: App.slnx build | mandatory (scaffolded) |
| Praxis | Development tool | direct | yes: Conditor init ran it; `--version` | mandatory |
| Ordo | Development tool | direct (toolchain lock) | `--version` only | mandatory |
| Dokimos | Verification tool | direct (toolchain lock) | `--version` only | mandatory |
| percepta-semantic | Verification tool | direct (toolchain lock) | `--version` only | mandatory |
| Percepta repo tool | Verification tool | transitive (installed by the workstation profile) | `--version`; contracts materialized | mandatory in the set |
| Strata | Development tool | transitive (workstation profile) | installed, never invoked | optional, excluded |
| Arca, Fides, summa-contracts | Runtime libraries | none | no | unused |
| visual-engineering, communication-engineering, tutela | Repository lifecycle | none (in the set; not installed by the preset) | no | unused |
| Vigila | — | none (mentioned in scaffold docs) | no | unused |
| Iter, Vitium | — | none (no releases) | no | unused |
| Tutors | — | none (no repository) | no | unused |
| GitHub API | Optional integration (persistence provider) | direct | read path executed; write path not run (needs the owner's token) | mandatory for the real-provider workflow |

## Mentioned only in documentation

- **Vigila:** appears in the scaffold's `docs/agent-provenance.md`.
- **Arca:** appears in input documents and R7 decisions (R7-AMD-01, withdrawn).
- **Strata:** appears in older preflight records.

None of them is referenced by code, the bootstrap, the package or CI.
