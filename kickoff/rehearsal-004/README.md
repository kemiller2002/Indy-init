# Rehearsal 4 operator records (T09)

| File | SHA-256 |
|---|---|
| `approvals.json` (`praxis.requirements-approvals/1`, decision `APR-R4-001`) | `f893adea3d38b19ee310a31a91624fccd17ea7140337a9bec7b33b1a869ddf56` |
| `equivalence-reviews.json` (750 obligations) | `164fc216e05c9a64347f55e634b4939fec8393790fbd15c711303af9e43bc448` |

Trusted approver and reviewer id for the preservation gate and the acceptance verifier: `kemiller2002`.

## Provenance: deviation from RDP-003

The owner approved both records in chat on 2026-10-26 at 20:55 EDT ("I approve all"), in reply to the requests in rehearsal-004 `research/requirements-preservation/OPERATOR-DECISIONS-REQUESTED.md`, after instructing the agent to do what was needed to start the run.

The coding agent **transcribed** that approval into these files and committed them. RDP-003 says the operator writes the approvals file and an agent must not; this is a deviation, recorded so that it stays visible:

- The decision is the owner's; the files and the published digest were produced by the agent.
- The gate's trust (approvals pin, trusted ids) therefore rests on the owner's chat instruction, not on a file the owner wrote.
- The 750 reviews approve verbatim carriage in bulk. They are an owner decision on the derivation method, not a per-obligation reading.

The Rehearsal 4 report must list this deviation. If the owner rejects the transcription, revert this directory and PRE-REQ-04 returns to NOT_RUN.

## Verifier-format approvals (second RDP-003 deviation, 2026-10-27)

`tools/compute-acceptance-verdict.py` accepts only `causa.requirements-approvals/1`; `approvals.json` above is `praxis.requirements-approvals/1`, so the verifier would return INVALID. APR-R4-001's exclusions (T27, T28) touch only requirements whose baseline priority is `unstated`, which need no ledger scope change, so the verifier-format file approves nothing:

| File | SHA-256 |
|---|---|
| `approvals.causa.json` (`causa.requirements-approvals/1`, bound to baseline `621e2f96…57b9`, no approvals) | `3e70dcd2e87811c4f3744ee97585855639741c3ba7a63d98f4b842c7e86bace7` |

The owner chose to have the agent commit exactly this content (chat, 2026-10-27 03:02 EDT, answer "B" to option B). Written by the coding agent; the Rehearsal 4 report lists it as a deviation from RDP-003.
