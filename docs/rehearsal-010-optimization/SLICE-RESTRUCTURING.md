# Rehearsal 010: splitting oversized work items

## What was split, and how the boundaries were derived

`inputs/work-item-split.proposed.json` splits the four largest **generated** work items along capability boundaries. Each part's requirements come from the frozen requirement identities: the catalog's `component`/`type`/`name`, and the plan requirement families. Each part's `dependsOn` follows the requirements' own `dependsOn` edges.

| Parent (requirements, mandatory obligations) | Parts |
|---|---|
| WI-INV-WORKSPACE (30, 170) | FRAME (INV-007, 008, 018, 042) → HYPOTHESES (009, 010, 011, 026), EVIDENCE (012, 013, 014, 024, 025, 027, 028, 029), SAVE (015, 016, 043) → DETAILS (034, 035, 036; needs hypotheses and evidence), LINKS (030, 031, 038, 044), CONCLUSION (032, 033) → ROUNDTRIP (017, 037) |
| WI-INV-START (11, 43) | FORM (001, 002, 003, 005, 006, 023) → RULES (019, 020, 021) → ACTIONS (004, 022) |
| WI-GEN-UI (21, 85) | LANDING (UI-001, 022, 025, 026, 033) → INSPECT (004, 005, 034), STATES (003, 010), LANGUAGE (017, 023, 024, 031, 032, 038) → VERIFY (013, 015, 021, 030, 039) |
| WI-GEN-PF (16, 45) | BOUNDARY (PF-001..005) → FIXTURES (006..009) → LEVELS (011, 012, 016..019); SECURITY (015) |

The compiler caught one real dependency while this plan was being written. INV-035 ("Editing keeps identity and history") depends on INV-024, 026 and 027, so DETAILS must follow HYPOTHESES and EVIDENCE. An earlier draft that had DETAILS after FRAME only was refused with `SPLIT_BREAKS_REQUIREMENT_DEPENDENCY`.

**Not split: SL-T19 and SL-T22.** They are execution-DAG tasks of the frozen v4.1.4 package. Splitting them means a successor package (v4.1.5) with an amended DAG, its own X01–X21 checks, an independent review and an owner freeze. The compiler refuses it (`SPLIT_FROZEN_TASK`). They are proposed in OWNER-DECISIONS-REQUIRED.md (ODR-R10-21), not done.

## What guarantees nothing changed

The guarantee is `requirements-compiler scope compile --split PLAN` (`Scope.applySplit` and `compileWithSplit`, F#). It refuses a plan that does any of the following:

| Code | Refuses |
|---|---|
| `SPLIT_REQUIREMENT_DROPPED` / `_DUPLICATED` / `SPLIT_FOREIGN_REQUIREMENT` | a parent requirement in no part, in two parts, or a requirement from elsewhere |
| `SPLIT_FROZEN_TASK` | splitting an execution-DAG task |
| `SPLIT_BREAKS_REQUIREMENT_DEPENDENCY` | a requirement whose dependency is in a part its own part does not reach |
| `SPLIT_CYCLE`, `SPLIT_UNKNOWN_DEPENDENCY`, `SPLIT_DEPENDS_ON_PARENT`, `SPLIT_PARENT_HAS_DEPENDENTS` | broken ordering |
| `SPLIT_ID_TAKEN`, `SPLIT_DUPLICATE_PART_ID`, `SPLIT_EMPTY_PART`, `SPLIT_TOO_FEW_PARTS`, `SPLIT_PARENT_TWICE`, `SPLIT_EMPTY_PLAN` | malformed plans |

**`scope preflight`** judges a successor manifest against the sources compiled with the split plan it records. It does so only if that plan file still has the recorded sha256 (`SPLIT_PLAN_CHANGED`, `SPLIT_PLAN_MISSING`). Without a recorded plan, a manifest is judged exactly as before.

**The approved scope is untouched.** `compile` without `--split` produces byte-identical output (`scopeSha256` `59bfef5a…`). The offline suite checks this.

**Offline suite, `work-item-split`.** The proposed plan compiles, and the successor has:
- the same requirements, with identical obligations, priority and mandatory flag;
- the same counts of requirements, obligations, mandatory obligations, mandatory requirements and plan tests;
- its own `scopeSha256`;
- every requirement's work item present.

The suite also applies nine mutations, and each must be refused with its code.

## The successor

| | Approved | Successor (`proposals/`) |
|---|---|---|
| Requirements / mandatory | 205 / 200 | 205 / 200 |
| Obligations / mandatory | 890 / 844 | 890 / 844 |
| Work items | 37 | 53 |
| scopeSha256 | `59bfef5a…` | `23a4dfce4b97c23cc6b93e856c1d78f61a954314bf65a6a7d3054486b69f0fea` |
| `slice-manifest` | COMPLETE COVERAGE | COMPLETE COVERAGE |
| `scope preflight` | PASS | PASS (against its recorded plan) |

Files: `proposals/scope-manifest.successor.json`, `proposals/slice-execution-manifest.successor.json`, `proposals/praxis-work-inventory.successor.json`.

**It is not adopted.** The approved scope, the readiness documents and the proposed lock all still name `59bfef5a…`. Adopting the successor is ODR-R10-19, and it changes what ODR-R10-01 approves.

## File-conflict check

The parts keep their parent's owner. Under the frozen ownership every workspace, start and GEN-UI part is `ui`'s, so no two agents edit the same file. The split adds **no parallelism** within `ui`. What it adds:
- smaller increments that each merge a working state;
- more requirements integrated by the deadline;
- a smaller blast radius when a part fails verification.

Parallelism inside the UI work needs files owned by different roles. That is the v4.1.5 ownership successor (ODR-R10-20), modelled in V5 and V6.

## Effect (model, identical assumptions; SCHEDULE-COMPARISON.md)

At 0-min CI, V1 → V2 (split only) raises the work integrated by T+420:
- slices: 25 → 38;
- requirements: 109 → 146;
- mandatory obligations: 402 → 594.

At 10-min CI the split alone *lowers* it (435 → 402 obligations): 53 slices queue behind one serialized gate. With the speculative queue (V3) it is 519. **Split without the speculative queue only when CI is fast.**
