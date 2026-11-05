# Echelon stack upgrade audit (P0, competition readiness)

Start with [FINAL-REPORT.md](FINAL-REPORT.md).

**Nothing is adopted.** No frozen package, the v4.1.5 candidate, `verification/`, any CI workflow, Conditor or EXP-INDY-002 is changed. Adoption needs the owner decisions in [OWNER-DECISIONS.md](OWNER-DECISIONS.md).

| File | Content |
|---|---|
| [CURRENT-VERSIONS.md](CURRENT-VERSIONS.md) | Installed, latest-stable and proposed versions, with package identities and digests |
| [DEPENDENCY-GRAPH.md](DEPENDENCY-GRAPH.md) | Direct and transitive dependencies, classification, invoked vs. mentioned |
| [RELEASE-EVIDENCE.json](RELEASE-EVIDENCE.json) | Release URLs, tags, commits, dates, and artifact digests (observed) |
| [COMPATIBILITY-MATRIX.md](COMPATIBILITY-MATRIX.md) | Boundary checks between systems |
| [UPGRADE-PLAN.md](UPGRADE-PLAN.md) | Ordered steps, risk classes, fallback |
| [CONDITOR-AUDIT.md](CONDITOR-AUDIT.md) | What Conditor 0.10.20 installs and generates; findings CA-01..05 |
| [VERSION-DRIFT-RESULTS.md](VERSION-DRIFT-RESULTS.md) | The stack-drift preflight, its real runs and negative controls |
| [TEST-RESULTS.md](TEST-RESULTS.md) | Every executed check, its outcome and time; what was NOT RUN |
| [REQUIREMENTS-CHANGES.md](REQUIREMENTS-CHANGES.md) | Exact successor-package changes REQ-ESU-01..09 |
| [OWNER-DECISIONS.md](OWNER-DECISIONS.md) | ODR-ESU-01..05 |
| [lock/](lock/) | `dependency-lock.source.json` (extends `indy-init.v4-dependency-lock-source/1`) and the generated `dependency-lock.json` (`indy-init.v4-dependency-lock/1`) |
| [evidence/](evidence/) | stack-drift logs and JSON reports (DP-01..09), self-test, offline suite, convention checks, Praxis in the scaffold |

**Tool:** `tools/stack-drift/stack-drift.fsx`, with the `lock`, `check` and `test` commands. To reproduce the lock:

```bash
dotnet fsi tools/stack-drift/stack-drift.fsx -- lock --source docs/optimization/echelon-stack-upgrade/lock/dependency-lock.source.json --registry ../echelon-registry --registry-commit 575240233d1fc32b57cfa827ea1031b68609fc58 --out docs/optimization/echelon-stack-upgrade/lock/dependency-lock.json
```
