# Source inventory: which Causa contract sources are authoritative

**Status:** candidate contract package `causa-precompetition 1.0.0-candidate`. Read on 2026-11-05 at Indy-init `5e29c29` (main) and causa-data `e8259df74add723765c57a5b1a5888d9c3f707af`.

This package creates no competing contract. It indexes the authoritative sources below, derives machine-readable registries from them, and proposes extensions only through the established successor process: a causa-storage minor release and a requirements successor package compiled with X01–X21, independently reviewed and frozen by the owner.

## 1. Authority, by concern

| Concern | Authoritative source | Status | How this package uses it |
|---|---|---|---|
| Latest **approved frozen** requirements package | `requirements-compiled/v4.1.4` (tag `indy-init-requirements-v4.1.4`, APR-V414-OWNER-2) | frozen; CI checks it equals its tag | Baseline for the compatibility reproduction (CR-REP-02). Never edited. |
| **Successor candidate** | `requirements-compiled/v4.1.5` | CANDIDATE. ODR-V415-01 A, -03 A, -04 A (APR-V415-OWNER-1) and ODR-V415-IF-VIEW A (APR-V415-OWNER-2) are recorded. **ODR-V415-02 (package approval) is not recorded, and no tag exists.** FREEZE step 2 is open as PR #143. | Source of every F# case name, the envelope, the persistence machines and the UI boundary types. The validator parses `contracts/*.fs` directly. |
| Storage format | causa-data `contract/v1.0.0` (causa-storage v1.0.0): `CONTRACT.md`, `FIELD-MAPPING.md`, `schemas/**` (bundle sha256 in `FIELD-REGISTRY.json`) | released; immutable after tag `causa-storage-v1.0.0` (§3.4) | Wire shapes, canonical form, the 31-event registry and the projection. `FIELD-REGISTRY.json` is generated from these schemas. |
| Domain records (F#) | `v4.1.5/contracts/00-DOMAIN-STUBS.fs` (50-case `CoreEvent`, 29-case `CoreCommand`, `EventEnvelope` with `Provenance`); payload types are opaque, "with exactly the fields of their wire records" | candidate | `COMMAND-EVENT-REGISTRY.json` binds each wire variant to its case. |
| Command and event definitions | `00-DOMAIN-STUBS.fs`; `docs/COMMAND-EVENT-CATALOG.md` (design); CONTRACT §7 (wire) | candidate / design / released | Registry with 52 command entries and 56 event variants. |
| State transitions (domain) | `docs/STATE-TRANSITION-TABLES.md` | approved design document | Transcribed into `STATE-TRANSITIONS.json` on the wire vocabulary and replayed against every stored history (CR-HIST-06). |
| State machines (persistence) | `v4.1.5/state-machine-contracts.json` (SM-SAVE, SM-CONFLICT, SM-LOAD, SM-CONNECTION, SM-NETWORK; 354 decided pairs) | candidate (compiled) | Indexed by sha256, not restated; states and inputs checked against the F# unions (CR-ST-02). |
| UI field requirements | `requirements/catalogs/causa-investigation.requirement-catalog.json` (CAT-CAUSA-INVESTIGATION, INV-001..044) | ratified (INV-001..038); INV-039..044 under ODR-R10-02 A | One binding per requirement in `UI-FIELD-MAP.json`; label, input type, required flag and validation are checked equal to the catalog (CR-UI-01). |
| Field-ID registry | The catalog's `INV-0nn` ids (UI); no wire field registry existed before this package | — | `FIELD-REGISTRY.json` introduces `F.<objectType>.<field>` ids for every wire field. UI ids stay `INV-0nn`. |
| Screen states, Forma patterns, responsive and accessibility obligations | `proposals/r10-contract-first-ui/SCREEN-MANIFEST.json`, folded into `v4.1.5/contracts/07-IF41-VIEW.fs` (ODR-V415-IF-VIEW A) | proposal (manifest) / candidate (types) | Screen states and mobile obligations are copied and checked equal (CR-UI-02). |
| Persistence requirements | `docs/CAUSA-PERSISTENCE-REQUIREMENTS.md` (PERSIST-*), `docs/PERSISTENCE-CONTRACT.md`, `v4.1.5/contracts/05-IF41-SAVE.fs` | approved / design / candidate | `PERSISTENCE-CONTRACT.md` (this package) states the durability semantics and indexes them. |
| Verification obligations | catalog `verification` kinds (Praxis compiles them to `INV-0nn/Vn/<kind>`, Dokimos judges them); `verification/journeys` J1–J3 (owner-controlled) | ratified / owner-controlled | Referenced by every binding; never copied or edited (AGENTS.md). |
| Owner decisions | `kickoff/rehearsal-007/approvals.causa.json`, `docs/OWNER-DECISIONS-*.md` | records | Read only. Requests from this package are in `OWNER-DECISIONS.proposed.json`. |

## 2. Experimental, superseded or non-authoritative

| Source | Classification | Why |
|---|---|---|
| `proposals/r10-historical-data-compatibility` (PR #126) | research, reference only (OD-HC-05 B: route R2) | Its decoder is not imported. Its findings were re-derived independently here (CR-REP-02), and its `source-pin.json` is the pin checked by CR-SRC-01. |
| PR #141 (`docs/optimization/implementation-conventions`) | merged research and convention catalog | Reports the 487/1,315 finding, which is reproduced here. |
| PR #140 (`experiments/exp-indy-002`, EXP-INDY-002) | open, experimental code | Findings only: typed read-back, nested picker labels, binding checks. No code is imported (OD-INDY-22). |
| PR #145 (CTP-001) | open proposal | Its contract-triggered protocol is noted for consumption; not depended on. |
| PR #143 | open: v4.1.5 FREEZE step 2 | Needed before ODR-V415-02 can be recorded. |
| `docs/rehearsal-009-content-readiness/proposed/*` | superseded proposal | Its `ObservationRevised`/`EvidenceRevised` naming is reused for the proposed 1.1.0 events. |
| `docs/CAUSA-DATA-CONTRACT-v1.md` | superseded by causa-storage v1.0.0 | Its actor kinds `external`/`imported` and `displayName` were removed (CONTRACT §16). |
| `docs/FSHARP-TYPE-SHAPES.md` §19–§28 shapes | design, not the wire | They need fields the data lacks (`Scope`, `Explains`) and lack `source`/`capturedAt`. |
| `requirements-compiled/v1..v4.1.2` | frozen history | Not consumed. 4.1.3 is withdrawn. |
| `kemiller2002/causa` | not accessible to this session (`add_repo`: no access) | causa-data CONTRACT v1.0.0 names the engine as its domain authority; OD-HC-09 B is the standing basis. |

## 3. Pins

The dependency and schema-version manifest, with the sha256 of every package file and validator source and the package checksum, is `PACKAGE-MANIFEST.json`. The pinned inputs are listed under `dependencies` in that file, and under `source` in every generated file.
