# Dependency compatibility report

Observed 2026-10-26 from `git ls-remote` and the Registry's main branch (`e6d8a734`, with echelon-current 1.28.0 approved). The machine-checked parts are in `evidence/preflight-run.json`: PRE-DEP-01, PRE-REQ-03, PRE-TOOL-01 and PRE-TOOL-02.

**Discovery and Registry membership are not verification.** Compatibility is exercised in T11 (lock and compatibility gate) and in the tasks that consume each system (VER-006, DEP-CURRENT-004).

| System | In echelon-current 1.28.0 (approved, kickoff set) | Newest tag observed | Status for Rehearsal 4 |
|---|---|---|---|
| Praxis | **3.21.0** | v3.21.0 | Current. 3.21.0 carries the preservation gate (PRE-REQ-03 PASS); the published artifact was verified on the pinned baseline. |
| Ordo | 1.17.0 | v1.17.0 | Current |
| Dokimos | 0.7.0 | dokimos-v0.7.0 | Current. It is required for attested completion with Praxis ≥ 3.17 (OD-INDY-8). |
| Percepta (repo lifecycle) | 0.1.0 | percepta-repo-v0.1.0 | Current |
| percepta-semantic | **0.2.1** | percepta-semantic-v0.2.1 | Current. Pinned by the kickoff set as OD-INDY-2 requires (PRE-DEP-01 PASS; F-005 resolved; CT-012 identity-verified) |
| Limen / limen-fsharp | 0.9.0 / 0.9.0 | v0.9.0 (0.9.1 in package.json, untagged) | Current release; 0.9.1 is pending and unregistered |
| Forma | 0.9.0 | v0.9.0 (0.9.1 in package.json, untagged) | Current release; 0.9.1 is pending and unregistered |
| Folio | **0.4.1** | v0.4.1 | Current. Assessed and selected in 1.28.0, qualified by Conditor 0.10.12 (F-015 resolved) |
| Aegis | 1.0.0 | — | 2.0.0 is pending and outside the profile range |
| Conditor | qualified the set as 0.10.12 | v0.10.12 | Current. It carries the SD-1 fix (CON-367, PRE-TOOL-02 PASS) and qualifies 1.28.0 (REG-RU-007 PASS). Bootstrap without the workaround is still to be shown (PRE-TOOL-03, T10). |
| causa storage contract | (not in the Registry, F-016) | causa-storage-v1.0.0 | Identities match (PRE-DEP-03 PASS). For GitHub, the §9.4.1 mechanism is replaced by the owner exception OD-INDY-9 (F-009). |
| causa historical catalog | (not in the Registry) | historical-v1 (`22260f4`) | Manifest identity matches (PRE-DEP-03 PASS) |
| .NET SDK | — | 10.0.402 | Pinned in the root `global.json`, and installed locally (PRE-TOOL-01 PASS). The CI job `fsharp-tooling` installs it from `global.json`. |

## The kickoff set: echelon-current 1.28.0 (approved)

Each requirement is met:
- percepta-semantic 0.2.1 (OD-INDY-2): selected;
- Praxis 3.21.0, the release that carries the preservation gate (RDP-004 without breaking VER-005): selected;
- Dokimos 0.7.0: selected;
- Conditor ≥ 0.10.11 (SD-1): Conditor 0.10.12 qualifies the set;
- folio 0.4.1 assessed (VER-003): selected;
- the rest of 1.27.0 unless a newer compatible release is verified: kept. Ordo 1.17.0 and Dokimos 0.7.0 are still the newest tags; Forma 0.9.1 and Limen 0.9.1 are untagged; Aegis 2.0.0 is outside the profile range.

The set's release-set record (`sets/echelon-current/1.28.0.json`) carries the owner's approval (OD-INDY-8) and no VER-003 exception. PRE-DEP-01 reads it from Registry main and passes.

## Not done here, by scope

- This package cut no release and wrote no Registry entry. The releases and the set were made upstream: echelon-registry#98, conditor#111 and praxis v3.21.0.
- No upstream repository was modified from this package.
