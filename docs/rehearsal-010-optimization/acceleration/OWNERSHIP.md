# Parallel Acceleration Directive: workstream ownership records

- **Directive.** Directive text: [`kickoff/rehearsal-010/delegated/DIRECTIVE-2026-11-05.md`](../../../kickoff/rehearsal-010/delegated/DIRECTIVE-2026-11-05.md).
- **Execution start (T0).** 2026-11-05 19:08:39 UTC.
- **Authority.** DA-R10-1, a delegated technical authority. It is **not** an owner approval.
- **Recorded decision.** Ownership is decision DTD-R10-001 in [`delegated-decisions.json`](../../../kickoff/rehearsal-010/delegated/delegated-decisions.json).
- **Competition rule (OD-INDY-22).** No Causa application implementation reaches `main` or the competition build. Practice code lives only on the branch `practice/r10-acceleration-app`, under a fence.

| Workstream | Owner (session) | Assigned files | Dependencies | Deliverable | Required verification | Earliest integration |
|---|---|---|---|---|---|---|
| Lead / integration | lead `session_01G1iECgS2MYoyqvc2eXNdhV` | `docs/rehearsal-010-optimization/`, `tools/r10/`, `.github/workflows/preflight-tooling.yml`, `kickoff/rehearsal-010/delegated/` | none | integration, conflict resolution, DA-R10-1, the completion report | preflight tooling CI; independent review for gate changes | continuous |
| **A** milestones | lead | `docs/rehearsal-010-readiness/functional-milestones.json` (on main via #133), `SLICE-EXECUTION-PLAN.md` §3 | ODR-R10-27, -29 (recorded) | milestone-first plan M1–M6; modeled vs measured kept apart | `schedule-model-tests.fsx`; ScopeTests | **done** (#133 merged) |
| **B** UI ownership | contract-first UI `session_01G1iECg5vsW2sk7NyS6rsQX` | `proposals/r10-contract-first-ui/` | #128 (merged as `d21fe96`); CF-10 A | ownership map: UI-1 shell, UI-2 workspace, UI-3 hypotheses/evidence; build order | its offline tests; contracts typecheck | on its PR's green checks |
| **C** verification and CI | lead | `tools/r10/adversarial.fsx` (`--shard`), `preflight-tooling.yml`, `tools/r10/change-impact.fsx` | none | Levels 1–3 ([VERIFICATION-CI.md](VERIFICATION-CI.md)); measured CI before and after | the sharded job's union check (5 jq controls); one full CI run | this PR |
| **D** nonblocking work and the minimum v4.1.5 | historical-compatibility `session_01G1iECgvwA92qPWSR19ZXxX` (v4.1.5); lead (classification) | `proposals/r10-historical-data-compatibility/`, `requirements-compiled/v4.1.5/` (new); classification in [WORK-CLASSIFICATION.md](WORK-CLASSIFICATION.md) | #128 merged; any OD-V415 owner records (see [OWNER-BATCH.md](OWNER-BATCH.md)) | staged v4.1.5: event union, derived workspace, listing, creation within SM-SAVE; FO-1..3 and T19/T22 deferred to v4.1.6 (DTD-R10-002) | the compiler's X-checks; frozen-package reproduction | after its PR's checks |
| Practice app (section 8) | lead's background builder | branch `practice/r10-acceleration-app`, `practice/causa-app/` only | causa-data (anonymous git read); the REST double | Connect → Browse → Open → Create → Save → Reload, measured | F# build; Playwright desktop and mobile; persistence **against a double only** | never merged (fenced) |

## Shared-file rule
- **Project files, `Program.fs`, `View.fs`, `Update` dispatch.** The integration owner owns these in the competition build.
- **Shared F# interfaces.** They change only through a published interface change, with a notice sent to the dependent sessions.
