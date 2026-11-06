# Upgrade plan

**Risk classes:**

- **CV:** compatible and verified, with the executions cited.
- **CM:** compatible with migration.
- **BR:** breaking but repairable.
- **BL:** blocked by another dependency.
- **UV:** unverified.

**Status of each step:**

- **proposed:** written down only.
- **implemented on branch:** changed on this branch.
- **tested:** executed in this audit.
- **approved:** recorded by the owner. No step is approved.

## Per system

| System | Change | Class | Migration time | Regression risk | Status |
|---|---|---|---|---|---|
| Forma | 0.12.0 → 0.13.0 in the app lock and the layout contract | **CM**: identical public surface; behaviour change in wrapping (token breaks at the container edge) | ~15 min (REQ-ESU-03, -04) plus recompilation | Low. Executed: LRP-001 26/26, Causa-representative 10/10, v4.1.5 layout contract 0 failed, preview read-path 25/25. Residual: the three components still open at iPad portrait 200% (documented alternative) | proposed; tested |
| Praxis | 3.24.0 → 3.25.0 in the app lock (already installed by R10) | CV | 0 (lock only) | Low: already used by R10 and Conditor init | proposed; tested |
| Dokimos | 0.9.0 → 0.11.1 in the app lock (already installed) | CV for installation; candidate acceptance UV (no candidate yet) | 0 | Low: qualified by conditor#131, approved in echelon-registry#109 | proposed; tested (install, version) |
| Ordo | 1.19.0 → 1.20.0 in the app lock (already installed) | CV for installation; parallel-run UV | 0 | Low | proposed; tested (install, version) |
| percepta-semantic | 0.3.0 → 0.4.0 in the app lock (already installed) | CV for installation | 0 | Low | proposed; tested (install, version) |
| Percepta repo tool | added to the lock at 0.1.0 (already installed) | CV | 0 | None | proposed; tested |
| Conditor | 0.10.18 → 0.10.20 in the app lock (the toolchain lock already pins 0.10.20) | CV for versions; **BR** for CA-01 (SDK in generated CI), repairable at kickoff | 5 min at kickoff (REQ-ESU-06) | Medium if forgotten (HF-029 cost R8 and R9 a CI cycle). Low with stack-drift at T0 | proposed; tested |
| Limen, Limen F#, Folio, Aegis, SDK | none (already current) | CV | 0 | — | tested |
| Optional or unused systems | none (excluded with reasons) | — | 0 | — | — |

**Mandatory systems blocked:** none. **Downgrades:** none.

## Ordered steps

1. **Owner:** decide ODR-ESU-01..05 ([OWNER-DECISIONS.md](OWNER-DECISIONS.md)). Nothing below proceeds before this.
2. **Agent, after ODR-ESU-01:** compile the successor package with REQ-ESU-01..05 using the established v41 process. Run X01–X21, the offline suite, the layered bootstrap onto the 0.10.20 fixture, and the 0.13.0 layout tests (Chromium and WebKit in CI).
3. **Agent:** record an independent review of the successor, then go through FREEZE steps 3–4 (owner approval and tag).
4. **Owner:** re-pin `toolchain.lock.proposed.json` to the frozen successor (REQ-ESU-07), then take it through launch-preflight LP-02.
5. **At kickoff:**
   - Conditor install and workstation (unchanged commands).
   - `conditor init --preset indy-init`.
   - v41 bootstrap.
   - **REQ-ESU-06**, the workflow SDK and pipefail correction.
   - `stack-drift.fsx check` must be READY before the scope checkpoint.
6. **After the event:** a Conditor 0.10.21 release for CA-01, CA-02 and CA-05. Re-evaluate Forma when 0.13.1 or a container-query fix for the three components is published.

**If step 3 cannot finish before kickoff:** use the fallback in ODR-ESU-01 A. Run on v4.1.5 (or v4.1.4, under ODR-V415-04) with the R10 toolchain lock as it is, plus step 5. That is still the newest fully tested stack: echelon-current 1.37.0 and Conditor 0.10.20, with Forma 0.13.0 from the scaffold.
