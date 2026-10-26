# Dependency compatibility report

Observed 2026-10-26 from `git ls-remote` and the Registry's main branch (`2d6338e`). The machine-checked parts are in `evidence/preflight-run.json`: PRE-DEP-01, PRE-REQ-03, PRE-TOOL-01 and PRE-TOOL-02.

**Discovery and Registry membership are not verification.** Compatibility is exercised in T11 (lock and compatibility gate) and in the tasks that consume each system (VER-006, DEP-CURRENT-004).

| System | In echelon-current 1.27.0 (approved) | Newest tag observed | Status for Rehearsal 4 |
|---|---|---|---|
| Praxis | 3.20.0 | **v3.21.0** | **3.21.0 carries the preservation gate** (PRE-REQ-03 PASS). It is released and verified, but in no approved set yet; include it in 1.28.0. |
| Ordo | 1.17.0 | v1.17.0 | Current |
| Dokimos | 0.7.0 | dokimos-v0.7.0 | Current. It is required for attested completion with Praxis ≥ 3.17 (OD-INDY-8). |
| Percepta (repo lifecycle) | 0.1.0 | percepta-repo-v0.1.0 | Current |
| **percepta-semantic** | **absent** | percepta-semantic-v0.2.1 | **Release recorded in the Registry (PR #96/#97); in no approved set** (PRE-DEP-01 BLOCKED; F-005) |
| Limen / limen-fsharp | 0.9.0 / 0.9.0 | v0.9.0 (0.9.1 in package.json, untagged) | Current release; 0.9.1 is pending and unregistered |
| Forma | 0.9.0 | v0.9.0 (0.9.1 in package.json, untagged) | Current release; 0.9.1 is pending and unregistered |
| Folio | 0.4.0 | **v0.4.1** | The set pins 0.4.0. v0.4.1 is tagged but not assessed (F-015, VER-001/002). Assess it in 1.28.0. |
| Aegis | 1.0.0 | — | 2.0.0 is pending and outside the profile range |
| Conditor | qualified the set as 0.10.11 | v0.10.11 | **The SD-1 fix (CON-367) is released** (PRE-TOOL-02 PASS). Bootstrap without the workaround is still to be shown (PRE-TOOL-03, T10). |
| causa storage contract | (not in the Registry, F-016) | causa-storage-v1.0.0 | Identities match (PRE-DEP-03 PASS). For GitHub, the §9.4.1 mechanism is replaced by the owner exception OD-INDY-9 (F-009). |
| causa historical catalog | (not in the Registry) | historical-v1 (`22260f4`) | Manifest identity matches (PRE-DEP-03 PASS) |
| .NET SDK | — | 10.0.402 | Pinned in the root `global.json`, and installed locally (PRE-TOOL-01 PASS). The CI job `fsharp-tooling` installs it from `global.json`. |

## What the kickoff set (echelon-current 1.28.0) must contain

- percepta-semantic 0.2.1 (OD-INDY-2)
- Praxis 3.21.0, the release that carries the preservation gate (RDP-004 without breaking VER-005)
- Dokimos 0.7.0
- Conditor ≥ 0.10.11 (SD-1)
- folio 0.4.1 assessed (selected or refused with a reason, VER-003)
- the rest of 1.27.0 unless a newer compatible release is verified

Once the set is cut, PRE-DEP-01 turns PASS by itself when the preflight is re-run: it reads the newest set on Registry main.

## Not done here, by scope

- No release was cut.
- No Registry entry was written.
- No upstream repository was modified.
