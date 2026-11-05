# CTP-001: contract-triggered parallel execution protocol

**Version 1.0.0** (2026-11-05). Status: **proposed**. ADOPTION-DECISIONS.md lists which parts are adopted now and which need the owner. This document comes from EXP-INDY-002 (PR #140); the evidence is in EXP-002-EVIDENCE.md.

## 1. What it is
- **The mechanism.** A typed F# contract is released in groups. Domain and UI agents implement each released group concurrently in files they own exclusively. Integration starts as soon as both sides of a group are staged, not at a fixed checkpoint. A verification agent who implemented nothing checks every integration with an independent browser test.
- **What EXP-002 measured** for this mechanism, on a 39-requirement slice:
  - first accepted end-to-end workflow at **T+23.0**;
  - 0 merge conflicts;
  - 3 minutes of integration repair;
  - three contract revisions.
- **Modelled, not observed:** the sequential equivalent would have reached the same point at about T+31.4.

## 2. Lifecycle (mandatory)

```
DEFINED → VALIDATED → RELEASED → IMPLEMENTING → STAGED → INTEGRATION_READY → VERIFIED
```

| State | Recorded by | Meaning | Gate |
|---|---|---|---|
| DEFINED | contract coordinator (actor `coordinator`, or the package's `integration`) | The group's contracts are written: registry entries plus F# types, and a SHA-256 hash over both. | – |
| VALIDATED | contract coordinator | `ctp-check registry` passes for every contract in the group, and the F# contract compiles. | CTP-C-* rules |
| RELEASED | contract coordinator | Domain and UI may implement it. This version is now **immutable**. | the same hash as VALIDATED |
| IMPLEMENTING | domain-side (`domain`, `persistence`) and UI-side (`ui`, `ui-2`, `ui-3`) agents | Each side records its own start. | – |
| STAGED | domain-side and UI-side agents | Each side's work compiles, and its focused tests (domain) or binding checks (UI) pass against this version's hash. | Recorded once per side. |
| INTEGRATION_READY | integration coordinator (`integration`) | Every side the group declares (default: a domain side and a UI side; DEFINED may set `sides`) has STAGED this version, the integrated build compiles, and unit, contract and independent browser tests pass. | CTP-L-STAGED, CTP-I-* rules |
| VERIFIED | integration coordinator | Required full CI passed on a candidate SHA that contains this version, and acceptance evidence is recorded. | CTP-L-VERIFIED |

- **BLOCKED** is an annotation, not a state: an agent records why it waits. The group's state does not change.
- **No state may be skipped.** A revised contract is a **new version** that starts again at DEFINED, carries `changeRequest: CR-n`, and goes back through VALIDATED and RELEASED.
- **One version has exactly one hash**, and a revision's hash differs from every earlier version's (CTP-L-IMMUTABLE). Versions are integers 1..n. Rows carry an ISO time and never go back in time (CTP-L-TIME). A change request is cited as `CR-n`.
- The log is append-only JSON lines (`CONTRACT-SCHEMA.json#/$defs/lifecycleRow`), committed alongside the work.

## 3. What a contract must state
`CONTRACT-SCHEMA.json` is the format, and `ctp-check registry` enforces it. Every contract states:

| Item | Field | Rule |
|---|---|---|
| Requirement IDs | `requirements` | CTP-C-REQ |
| Stable field or contract ID | `id`, decoupled from F# names | CTP-C-ID |
| Typed F# inputs and outputs | `fsharp.{record,property,type,input,output}` | CTP-C-FSHARP |
| Commands and UI actions | `command`; one `controls[]` entry per UI action, each with its own, distinct `accessibleName` | CTP-C-ACTION, CTP-C-CONTROL-NAME |
| State transitions | `transitions[]` with `from`, `event`, `to`, `carries` (what the target state holds; `["nothing"]` is explicit), `tests` | CTP-C-TRANSITION-STRUCTURE, CTP-C-TRANSITION-CARRIES |
| Validation and error behaviour | `validation[]` with `rule` and `error` | CTP-C-VALIDATION, CTP-C-ERROR |
| Accessible names and nested labels | `label`; `fields[]` with `label`, `component`, `type`; `options[]` with `label` | CTP-C-LABEL, CTP-C-NESTED-LABEL, CTP-C-OPTION-LABEL |
| Persistence expectation | `persistence` (required): `none`, `local` or `durable-github` (the last also needs `realProviderEvidence: true`) | CTP-C-PERSISTENCE |
| Test obligations | `tests.unit` or `tests.ui`, and `tests.acceptance`. Every transition names at least one UT-/UI- test **and** one AT- test | CTP-C-TESTS, CTP-C-TRANSITION-TEST |

## 4. Release incrementally
- Release the first group as soon as it validates. Do not wait to define every group.
- Later groups are released into existing code as they validate (EXP-002 wave 2: 3.5 minutes from release to acceptance).
- A change to a released group always goes through a change request (section 5). Additive groups never change released contracts.

## 5. Change requests
1. **Raise.** Any role may raise a change request (CR-n) for a released contract, naming the contract and the defect.
2. **Decide.** The contract coordinator decides: accept, or reject with a reason.
3. **Accept.** An accepted CR produces a new version for each affected group, which goes through DEFINED, VALIDATED and RELEASED with `changeRequest` set. Every role implementing that group is notified.
4. **Workarounds.** An agent never works around a contract gap silently. It either raises a CR or records the workaround in its timeline. EXP-002 recorded 5 such workarounds.

## 6. Roles
See AGENT-OWNERSHIP.md: contract coordinator, domain agent, UI agent, verification agent and integration coordinator. Roles may be combined, but **verification is never done by an implementer of the code it verifies** (CTP-I-INDEPENDENT).

## 7. Integration and verification
- **Integration:** INTEGRATION-POLICY.md. It is event-driven per group; full CI runs at candidate checkpoints and before protected merges; push once per candidate.
- **Verification:** VERIFICATION-POLICY.md. An independent browser test runs at every integration. Durable GitHub persistence is verified only by a real write and read-back.

## 8. Enforcement
- `tools/execution/ctp-check.fsx` is F# with no dependencies. It has these modes:
  - `registry`
  - `lifecycle`
  - `integration`
  - `ownership`
  - `ci` (report only)
  - `test`: 83 cases, all passing. 21 of them work on EXP-002's real files, committed as fixtures under `evidence/exp-002/`: 8 integrity checks against SHA-256 values pinned in the checker's source, plus 13 replay cases. When PR #140's commits are in the clone, 5 more cases compare each fixture byte for byte with `git show`; without those commits they are reported as NOT-RUN, not counted as passes.
- ADOPTION-DECISIONS.md section 3 says what each mode enforces today, and what still depends on review.

## 9. Competition use
COMPETITION-ENTRYPOINT.md applies this protocol inside the frozen competition procedure. It does not replace that procedure.
