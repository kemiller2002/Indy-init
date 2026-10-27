# Rehearsal 5 operator records

| File | SHA-256 |
|---|---|
| `approvals.json` (`praxis.requirements-approvals/1`, no approvals) | `711859d610ee6af1174aa33ebc6455ec77a7d107af02aa3fe80da0003616c011` |
| `approvals.causa.json` (`causa.requirements-approvals/1`, bound to baseline `621e2f96…57b9`, no approvals) | `3e70dcd2e87811c4f3744ee97585855639741c3ba7a63d98f4b842c7e86bace7` |
| `equivalence-reviews.json` (`indy-init.r4-reviews/1`, 775 obligations) | `7dba9c655e3c39b69862cb6da08d33e8b2152e9b0f391a893a0747cdc1f0f80a` |

Trusted approver and reviewer id for the preservation gate and the acceptance verifier: `kemiller2002`.

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

## Equivalence reviews (second RDP-003 deviation, 2026-10-27)

`equivalence-reviews.json` reviews the preservation gate's derivation in `kemiller2002/indy-init-rehearsal-005` (T07 on branch `preflight/t07-derivation`, commit `2a15e6b`). That derivation requests 775 equivalence reviews (`research/requirements-preservation/review-requests.json`): token-less obligations under five words, which Praxis cannot decide by verbatim carriage. The Praxis 3.22.0 dry-run gate reports exactly those 775 as `needs-review` and nothing else.

On 2026-10-27 the owner was asked whether to record approval of verbatim carriage for these obligations, and answered in chat: "Yes, record it". The coding agent transcribed that answer into `equivalence-reviews.json` and committed it. RDP-003 says the operator records reviews and an agent must not; this is a deviation, recorded so it stays visible:

- The decision is the owner's: verbatim carriage is acceptable. The file and its digest were produced by the agent.
- The record covers only obligations whose derived criterion is byte-identical to the obligation's text. This was checked programmatically for every one of the 775 requests, twice: by the derivation tool (re-parsing the rendered markdown) and by an independent script that also found each text verbatim in its source document at `aab8f80`. All 775 passed; none was left out. The derivation tool refuses to apply a review that covers anything else.
- Like Rehearsal 4's, these reviews approve verbatim carriage in bulk. They are an owner decision on the derivation method, not a per-obligation reading.

The Rehearsal 5 report must list this deviation. If the owner rejects the transcription, revert `equivalence-reviews.json` and this section, and PRE-REQ-04 returns to NOT_RUN.
