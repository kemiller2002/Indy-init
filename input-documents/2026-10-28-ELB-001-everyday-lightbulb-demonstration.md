# ELB-001: Everyday lightbulb root-cause demonstration

Priority: P0 before the next Causa rehearsal starts.
Owner systems: Causa Data (deterministic investigation and templates), Conditor (template-selectable preflight), Indy Init (requirements and demonstration evidence).
Target repositories: `kemiller2002/causa-data`, `kemiller2002/conditor`, and `kemiller2002/Indy-init`.
Reference only: `kemiller2002/causa` when accessible and the frozen `kemiller2002/indy-init-rehearsal-005` application (no modifications).
Intake: Praxis document ingestion through Indy Init's legacy `input-documents/` path. Indy Init is not Praxis-initialized; this file is source material, not a canonical requirement or an approval.

## Objective

Demonstrate the same evidence-governed Causa workflow on a familiar problem: a ceiling light that does not illuminate because its replaceable bulb has failed. The demonstration must preserve uncertainty, compare alternatives, separate proposed/completed/verified correction states, and include a negative control in which replacement does not solve the problem.

## Requirements

| ID | Owner | Requirement | Acceptance evidence |
|---|---|---|---|
| ELB-001-01 | Causa Data | A separate immutable `everyday-v1` template contains exactly one engine-authored investigation with case key `burned-out-lightbulb`. Its manifest, event stream and snapshot use the existing storage contract and are generated through supported F# domain commands rather than handwritten. | Deterministic rebuild comparison, template digest, engine replay and snapshot validation. |
| ELB-001-02 | Causa Data | The investigation starts from the observation that the ceiling light does not illuminate when its wall switch is turned on and retains four distinguishable hypotheses: failed bulb, defective wall switch, unavailable fixture power and defective fixture. | Replayed state names all four hypothesis identities and their separate evidence assessments. |
| ELB-001-03 | Causa Data | Evidence records the failed illumination, other room lights operating, switch operation, power-off bulb removal, installation of a known-good compatible replacement, and illumination after replacement. Observations remain distinct from interpretations and conclusions, and the procedure contains no live-electrical-work instruction. | Ordered event/state assertions and accessible scenario documentation. |
| ELB-001-04 | Causa Data | The supported cause is failure of the original bulb. The exact internal failure mechanism remains explicitly unknown. Alternative hypotheses are retained and weakened only to the extent supported by the evidence. | Cause/residual-uncertainty assertions and replayed alternative-hypothesis state. |
| ELB-001-05 | Causa Data | The correction `Replace the failed bulb with a compatible replacement` is associated with the supported cause. Proposed, completed and successfully verified states are separate legal transitions, and verification cannot precede its evidence. | Event-order assertions and invalid-transition negative control. |
| ELB-001-06 | Causa Data | A separate engine-driven negative scenario records that a replacement remains dark. It must end in failed verification and must not record a supported bulb cause or successful resolution. | Negative replay, `VerificationFailed` state and rejected cause-confirmation command. |
| ELB-001-07 | Causa Data | A new immutable `demonstration-v1` template contains byte-identical copies of Apollo 1, Apollo 11, Challenger and the lightbulb investigation. `historical-v1` and its canonical source remain unchanged. | Exact four identifiers, byte comparisons, stable digest and clean historical-source comparison. |
| ELB-001-08 | Conditor | The existing rehearsal configuration selects the template explicitly. The same ordered fail-closed workflow can initialize and verify `historical-v1` or `demonstration-v1`; evidence is bound to the selected template digest and exact investigation set at every stage. | Four-case READY orchestration test, mismatch negative controls and disposable-branch end-to-end receipt. |
| ELB-001-09 | Causa application | The ordinary workspace resolver loads the lightbulb investigation and all four combined-template investigations without special-case routes, provider behavior or UI logic. Existing navigation and evidence workflows remain generic. | Application-resolver verification, frozen-application compatibility harness and unchanged application source. |
| ELB-001-10 | Indy Init | The judge walkthrough explains observation, alternatives, evidence, supported cause, residual uncertainty, correction lifecycle and verification in accessible language. It also explains why a failed replacement does not prove the bulb was the cause. | Committed walkthrough and demonstration runbook. |
| ELB-001-11 | All owners | Automated checks cover deterministic storage, identity, replay, distinct alternatives, evidence/conclusion consistency, correction order, contradictory evidence, combined initialization, isolation and unchanged historical records. | Passing F# tests, remote readback and application-load evidence; blocked checks are named rather than implied. |
| ELB-001-12 | Indy Init / Conditor | Building and testing the reusable demonstration are preparation. Initializing the actual event workspace and entering timed execution remain separate, explicit, evidenced transitions. | Runbook ordering and no automatic start/supervise/mission transition from rehearsal initialization. |

## Preflight gate

If the next rehearsal uses the combined judge demonstration, it remains BLOCKED until:

1. The reviewed Causa Data and Conditor revisions are merged and available to the rehearsal host.
2. This document and CHT-001 are ingested through Praxis with derived records naming their source documents.
3. The operator explicitly selects `demonstration-v1`, a new rehearsal identifier, the intended repository and branch.
4. `conditor rehearsal init --json` returns a schema-valid READY receipt naming the four expected investigations and the reviewed demonstration template digest.
5. The published commit is independently read back and the ordinary application resolver loads all four investigations.
6. The operator separately authorizes the timed start under the event rules.

## Current implementation evidence (not merge or release evidence)

On 2026-10-28 the feature implementation produced:

- Causa Data branch `codex/immutable-historical-rehearsal-workspaces`, commits `2c935df`, `089f29f` and `cfd761d` for the deterministic everyday investigation, combined canonical source and immutable templates.
- Conditor branch `codex/causa-rehearsal-preflight-v2`, commits `e77bfd1` and `822f0ca`, including explicit selected-template identity and four-investigation orchestration tests.
- Causa's 32-test F# suite and 53 canonical historical-catalog validations passed before this intake was recorded.
- Disposable branch `codex/causa-demonstration-e2e-20261028` published `rehearsal-900003` at commit `8d52ec9e02200977d4621ef4bf1aa429c6c788db`; Conditor independently read it back and produced a four-investigation READY receipt. A scratch harness using the frozen Rehearsal 5 provider loaded all four cases and proved a supported write changed only the selected workspace.

These are implementation and local validation evidence only. They do not claim merge, release, a selected event workspace, a READY event receipt or the start of timed execution.

## Acceptance for this intake

- Each ELB-001 item has a derived Praxis record naming this document in `derived_from`.
- No GitHub Issue substitutes for ingestion.
- The frozen Rehearsal 5 repository and completed evidence remain unchanged.
- The actual demonstration receipt, published commit and application compatibility evidence are retained by the rehearsal that uses them.
