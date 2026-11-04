# Compiled package v4.1.4: suite runners that execute

**Status: CANDIDATE.** Not approved, not frozen and not pinned by any rehearsal lock.

v4.1.4 is the successor amendment of v4.1.2, which the owner approved as APR-V412-OWNER-3. v4.1.2 is not edited.

It corrects the verification execution plan defects that Rehearsal 009's release gate hit (`docs/release-gate-repair/FINAL-REPORT.md`):
- SU-OWN-DOTNET and SU-DOTNET-LIVE now run one `dotnet test Causa.slnx` with a filter. Under v4.1.2 they named several projects, which fails with MSB1008. This is ODR-V414-01.
- The bootstrap now writes `playwright.config.ts`, which defines every project the runners name (chromium and webkit), and every Playwright runner selects it. This is ODR-V414-02.
- New validation check X21: every runner can execute.

No requirement, obligation, test binding, contract, state machine or schedule changes. The plan is byte-identical to v4.1.2's except for the six runners.

Version 4.1.3 is not used, because it belongs to the withdrawn v4.1.3 proposal (superseded by OD-INDY-19).

Compile it:

```
requirements-compiler v41 --source . --package requirements-compiled/v4.1.4 --base requirements-compiled/v4
```

Freezing it is in [FREEZE.md](FREEZE.md); the change is described in [AMENDMENT-4.1.4.md](AMENDMENT-4.1.4.md).
