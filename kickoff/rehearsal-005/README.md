# Rehearsal 5 operator records

| File | SHA-256 |
|---|---|
| `approvals.json` (`praxis.requirements-approvals/1`, no approvals) | `711859d610ee6af1174aa33ebc6455ec77a7d107af02aa3fe80da0003616c011` |
| `approvals.causa.json` (`causa.requirements-approvals/1`, bound to baseline `621e2f96…57b9`, no approvals) | `3e70dcd2e87811c4f3744ee97585855639741c3ba7a63d98f4b842c7e86bace7` |

Trusted approver id for the preservation gate and the acceptance verifier: `kemiller2002`.

## Scope: no exclusions

Rehearsal 5 approves no exclusion. Every requirement in baseline `BL-CAUSA-INDY-INIT-aab8f80c7743` (461 requirements, 108 P0) is in scope.

Rehearsal 4's APR-R4-001 (T27, T28) was scoped to Rehearsal 4 only and is not carried over. The owner did not name an exclusion for Rehearsal 5, and the coding agent may not invent one: an exclusion weakens the acceptance standard.

The baseline is not re-pinned. Rebuilt at `9af35db`, it equals the frozen `aab8f80` pin requirement for requirement: same 461 ids, same 108 P0, no requirement changed, same conflicts. `approvals.causa.json` is therefore byte-identical to Rehearsal 4's.

## Provenance: deviation from RDP-003

RDP-003 says the operator writes the approvals files and an agent must not.

On 2026-10-27, the owner was asked whether to write these files or have the agent write them. The owner instructed the coding agent: "You write the approval files". The agent wrote and committed them. This is a deviation, recorded so it stays visible:

- The decision is the owner's: no exclusions, the same trusted operator. The files and their digests were produced by the agent.
- The gate's trust (approvals pin, trusted ids) rests on the owner's chat instruction, not on a file the owner wrote.

The Rehearsal 5 report must list this deviation. If the owner rejects the transcription, revert this directory.

## Not here yet

`equivalence-reviews.json` reviews the preservation gate's derivation in the rehearsal repository (Rehearsal 4: T07 derivation, T09 reviews). That derivation is produced during Rehearsal 5's untimed setup in `kemiller2002/indy-init-rehearsal-005`, so its reviews are added then, not before.
