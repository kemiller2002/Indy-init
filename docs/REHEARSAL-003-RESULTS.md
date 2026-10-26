# Rehearsal 3 results (index for agents)

**Outcome: NOT ACCEPTED.** That applies to the original run and to its remediation. [OWNER-DECISIONS-2026-10-24.md](OWNER-DECISIONS-2026-10-24.md) records the decision (OD-INDY-1..8: writes are P0, explicit Connect, GUID routes, a released Percepta CLI).
**Repository:** [kemiller2002/indy-init-rehearsal-003](https://github.com/kemiller2002/indy-init-rehearsal-003). The links below are pinned to commit `bc6eb5b`.
**Next:** [REHEARSAL-004-WRITE-PATH-PLAN.md](REHEARSAL-004-WRITE-PATH-PLAN.md).

## Machine-readable summary

[`evidence/REHEARSAL-003-RESULTS.json`](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/evidence/REHEARSAL-003-RESULTS.json) (schema `causa.rehearsal-results/1`) holds:
- the verdict;
- the final deployed candidate;
- ledger counts and every criterion that does not pass, with its reason;
- the preservation-gate result and its known bypasses;
- designs reusable in Rehearsal 4, with file paths;
- lessons learned;
- open upstream dependencies;
- a document index.

## Key facts

| Item | Value |
|---|---|
| Final deployed candidate | `d54785c`: CI 4/4 green, deployed acceptance 9/9, Test A PASS on the named secret `CAUSA_GITHUB_TOKEN` |
| Ledger | 116 criteria: 67 PASS, 11 PARTIAL, 12 FAIL, 1 BLOCKED, 25 NOT_RUN. The FAILs are writes, explicit Connect and offline/conflict states, which OD-INDY put in scope. The BLOCKED item needs a released Percepta CLI |
| Requirements preservation | DC-008, the lost UI-016, confirmed and restored. The gate fails the original package (UI-016 palette tokens missing). On the remediated package it reports P0 38/42 and exit 3, with exclusions and conflicts left for the owner |
| Gate weaknesses (independent review) | self-written approvals and reviews pass; an edited baseline passes; a role swap or negation of palette tokens passes; no CI job runs the gate |

## Reuse in Rehearsal 4

| Design | Where (indy-init-rehearsal-003) |
|---|---|
| Typed request book: a response applies only to its own request (DC-002) | [`src/engine/Requests.fs`](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/src/engine/Requests.fs) |
| Freshness nonce for mutable reads (DC-005); D-07 reproduced deterministically | [`research/rehearsal-003-engineering-hardening.md`](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/research/rehearsal-003-engineering-hardening.md) |
| Provider conformance: real-GitHub fixture, shared model, drift job, HTTPS replay with the real browser cache (R4-02) | [`research/rehearsal-003-provider-conformance.md`](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/research/rehearsal-003-provider-conformance.md) |
| UI-016 Causa palette through Forma tokens, domain-derived tones, colour tests, deployed screenshots at four sizes | [`research/ui-016-restoration-evidence.md`](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/research/ui-016-restoration-evidence.md) |
| Security review and SEC-T1..T12 | [`research/rehearsal-003-security-review.md`](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/research/rehearsal-003-security-review.md) |

## Lessons

1. **Derive from the pinned baseline and gate it before implementing.** Rehearsal 3 derived its requirements from the mission text and lost UI-016 ([root cause](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/research/requirements-preservation-root-cause.md)).
2. **Agents must not interpret blanket approvals or write approval files** (OD-INDY-7). The remediation agent did both on 2026-10-25, and the transcription was withdrawn ([report §10](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/docs/REHEARSAL-003-REMEDIATION-REPORT.md)).
3. **Bind editable inputs on `input`.** A bound field without `data-on="input"` lost typed text to the next projection. This happened twice.

## Reports

- [Remediation report](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/docs/REHEARSAL-003-REMEDIATION-REPORT.md)
- [Remediation results](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/research/rehearsal-003-remediation-results.md)
- [Independent review](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/research/rehearsal-003-independent-review.md)
- [Requirements reconciliation](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/research/indy-init-requirements-reconciliation.md)
- [Original report](https://github.com/kemiller2002/indy-init-rehearsal-003/blob/bc6eb5b4e3d4ccc11e9f1c39b57276f408caa3b8/docs/REHEARSAL-003-REPORT.md) (unchanged)
