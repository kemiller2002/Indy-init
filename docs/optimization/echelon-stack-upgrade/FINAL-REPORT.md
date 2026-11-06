# Echelon stack upgrade: final report

**Audit date:** 2026-11-05. **Branch:** `claude/admiring-keller-ln2qta`.

**Scope:**

- Indy-init `main` at `d80fda3`, which includes PR #141.
- echelon-registry `5752402`.
- The released Conditor 0.10.20.
- Every Echelon repository the directive names.

## Summary

| Measure | Value |
|---|---|
| Echelon systems audited | **22**. The 15 the directive names, plus percepta-semantic, limen-fsharp, strata, summa-contracts, visual-engineering, communication-engineering and tutela |
| Already current in what an agent actually installs (R10 toolchain lock and Conditor 0.10.20 scaffold) | **11 of 11** required: Conditor, Praxis, Ordo, Dokimos, percepta-semantic, Percepta, Limen, Limen F#, Forma, Folio, Aegis |
| Behind in the governed **app lock** (frozen v4, echelon-current 1.33.0) | **6**: Forma 0.12.0, Praxis 3.24.0, Dokimos 0.9.0, Ordo 1.19.0, percepta-semantic 0.3.0, Conditor 0.10.18. Percepta is missing from it |
| Upgrades proposed | **6 version moves and 1 addition**, all in one successor lock (echelon-current 1.37.0) |
| Upgrades implemented on a governed package | **0**. Frozen packages and the v4.1.5 candidate are untouched. The successor lock is generated on this branch as a proposal |
| Blocked | **0** mandatory. **1 generated-CI defect** (CA-01, HF-029) is repairable at kickoff; a Conditor release is optional |
| Excluded as unused | **11**: Strata (installed, never invoked), Arca, Fides, summa-contracts, visual-engineering, communication-engineering, tutela, Vigila, Iter, Vitium, Tutors |

## Selected versions

| System | Version |
|---|---|
| echelon-current | 1.37.0 (set `bc7d163d…`) |
| Conditor | 0.10.20 |
| Praxis | 3.25.0 |
| Ordo | 1.20.0 |
| Dokimos | 0.11.1 |
| percepta-semantic | 0.4.0 |
| Percepta | 0.1.0 |
| Limen | 0.9.1 |
| Limen F# | 0.9.0 |
| **Forma** | **0.13.0** |
| Folio | 0.4.1 |
| Aegis | 1.0.0 |
| .NET SDK | 10.0.402 |

Each one is the latest stable published release; see [RELEASE-EVIDENCE.json](RELEASE-EVIDENCE.json).

## Results

| Item | Result |
|---|---|
| Conditor changes required | No version change. CA-01 (SDK in generated workflows) is still open in 0.10.20. CA-02 (pipefail), CA-04 (`--version`) and CA-05 (token on public fetches) are nonblocking. No Conditor commit was made; ODR-ESU-03 needs the owner's choice of the SDK pin's source |
| Requirements package | Successor (v4.1.6) changes are specified exactly (REQ-ESU-01..09) and the lock is generated. **Not compiled or frozen:** it waits on ODR-ESU-01. v4.1.5 is mid-freeze and was not edited |
| Compilation | PASS (executed): v4.1.5 layered onto the real Conditor 0.10.20 scaffold; `Causa.slnx` and `App.slnx` have 0 warnings and 0 errors with warnings as errors; requirements compiler 0 errors; offline suite all passed |
| WASM startup | PASS (executed, Chromium): the F# engine on Limen 0.9.1 with Forma 0.13.0 (scratch preview) starts, renders, and has no console errors. The competition app itself does not exist before kickoff |
| Forma layout | PASS (executed, Chromium): LRP-001 is 16/26 → 26/26 from 0.12.0 to 0.13.0; Causa-representative 10/10; v4.1.5 layout contract 0 failed on 0.13.0. Firefox and WebKit were NOT RUN. Three components remain open upstream at iPad portrait 200%; the alternative is documented |
| Real-provider integration | Read path: PASS (executed) against the real causa-data, 1,315 events, digests equal. Write path: PASS against the in-process double (31/31). **Real GitHub write: NOT RUN** (owner token) |
| Version-drift preflight | Implemented (`tools/stack-drift/stack-drift.fsx`, F#). 30/30 self-tests. Real runs: DP-01 NOT READY (CA-01 only); DP-02 READY after correction; 4 negative controls fail closed |
| Full CI | Pending on this branch's pull request. This change adds no workflow step |
| Remaining owner decisions | ODR-ESU-01 (lock to 1.37.0), -02 (Forma 0.13.0 inventory), -05 (0.10.20 scaffold fixture). ODR-ESU-03 A and -04 A were applied on the owner instruction of 2026-11-06 (STARTUP §2 step 3; `fsharp-tooling` CI step) |

## PR #141 claim check

| Claim in PR #141 | Finding |
|---|---|
| "Forma 0.12.0 failed 48 of 78 containment checks; 0.13.0 passes 78/78" | Reproduced for Chromium (one of the three browsers): **16 of 26 fail on 0.12.0, 26 of 26 pass on 0.13.0**, using Forma v0.13.0's own suite against the published tarballs |
| "No newer stable Forma" | Confirmed: npm `latest` is 0.13.0, and 0.13.1 is an unreleased candidate |
| "v4.1.4 and v4.1.5 still reference Forma 0.12.0" | Confirmed |
| "Conditor 0.10.18 scaffold pins Forma 0.12.0" | Confirmed (DP-05), but **0.10.20, the pinned Conditor, scaffolds 0.13.0** |

## Status of each upgrade

| Upgrade | Proposed | On branch | Tested | Approved |
|---|---|---|---|---|
| App lock → echelon-current 1.37.0 (Forma, Praxis, Dokimos, Ordo, percepta-semantic, Conditor; add Percepta) | yes | lock generated (proposal) | yes (DP-01/02, builds, browser) | no |
| Layout contract → Forma 0.13.0 inventory | yes | no (successor not compiled) | yes (contract run on 0.13.0) | no |
| Generated-CI SDK correction | yes | yes (STARTUP §2 step 3) | yes (DP-02, DP-10) | owner instruction 2026-11-06 (ODR-ESU-03 A) |
| Version-drift preflight | yes | yes (tool, CI self-test, T0 step) | yes | owner instruction 2026-11-06 (ODR-ESU-04 A) |
| 0.10.20 scaffold fixture | yes | no | yes (layering executed) | no |

## Readiness

The stack an agent installs today, through the R10 toolchain lock and Conditor 0.10.20, is the latest stable everywhere. That was established by execution. The governed requirements still describe an older stack, and the generated CI does not install the pinned SDK. With ODR-ESU-01..03 decided and REQ-ESU-06 applied at kickoff, the competition agent starts with a current stack whose consistency is checked by execution (stack-drift READY).

**Recommendation:** decide ODR-ESU-03 A and ODR-ESU-04 A now; they need no package change. Then decide ODR-ESU-01 A and ODR-ESU-02 A, with the documented fallback if the successor cannot be frozen before kickoff.
