# Implementation conventions (rehearsal retrospective)

This directory is a proposal. It changes no requirement, no frozen package and no CI check. Start with [FINAL-REPORT.md](FINAL-REPORT.md).

| File | Content |
|---|---|
| [HISTORICAL-FINDINGS.md](HISTORICAL-FINDINGS.md) / [FAILURE-DATA.json](FAILURE-DATA.json) | 87 evidence-cited findings from R2–R9, R10 readiness, the post-R10 work and Forma |
| [DECISION-INVENTORY.md](DECISION-INVENTORY.md) | decisions agents kept re-making, and which to make once |
| [CONVENTION-CATALOG.md](CONVENTION-CATALOG.md) / [CONVENTION-CATALOG.json](CONVENTION-CATALOG.json) | 23 conventions and 22 failure modes; the JSON is authoritative |
| [ENFORCEMENT-MATRIX.md](ENFORCEMENT-MATRIX.md) | each convention, its earliest check, and its cost |
| [F-SHARP-PATTERNS.md](F-SHARP-PATTERNS.md), [FORMA-PATTERNS.md](FORMA-PATTERNS.md), [PERSISTENCE-PATTERNS.md](PERSISTENCE-PATTERNS.md), [PARALLEL-AGENT-PATTERNS.md](PARALLEL-AGENT-PATTERNS.md) | pattern guidance |
| [IMPACT-FORECAST.md](IMPACT-FORECAST.md) / [FORECAST.json](FORECAST.json) | modeled savings with sensitivity analysis (generated) |
| [TOP-10-TIME-SAVINGS.md](TOP-10-TIME-SAVINGS.md) | top 10 by net critical-path minutes (generated) |
| [PROPOSED-REQUIREMENTS-CHANGES.md](PROPOSED-REQUIREMENTS-CHANGES.md) | exact text for successor packages, pending owner decisions ODR-IC-01..06 |

Tooling: `tools/conventions/conventions.fsx`, which is F# only (OD-INDY-14).

To regenerate the forecast blocks after editing either JSON file:

```bash
dotnet fsi tools/conventions/conventions.fsx -- render
```

To check the result:

```bash
dotnet fsi tools/conventions/conventions.fsx -- validate
```
