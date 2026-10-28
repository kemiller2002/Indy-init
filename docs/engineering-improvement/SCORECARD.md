# Engineering improvement scorecard (2026-10-28)

Outcome of the Echelon Engineering Improvement Initiative. Evidence:
[`BASELINE-2026-10-28.md`](BASELINE-2026-10-28.md); protocol:
[`EXPERIMENT-PROTOCOL.md`](EXPERIMENT-PROTOCOL.md).

Status words:

- **Implemented** means code is on a pull request branch. Nothing here is merged
  yet, and no merge has happened without the owner.
- **Verified** means the change's own tests, negative controls and the
  repository's full CI pass on the PR head.
- **Measured benefit** gives only what was measured. "Not measured" is not a
  zero. All comparisons with rehearsal history are retrospective and non-causal;
  no controlled experiment has been run yet.

Rehearsal 5 was read only, at cutoff `e086147`, and was not modified. Its
deadline (2026-10-28T03:08:38Z) passed during the initiative; its final state is
outside this scorecard.

| Improvement | Implemented | Verified | Measured benefit |
|---|---|---|---|
| WASM compatibility (A) | Yes: `limen wasm-check` ([limen#113](https://github.com/kemiller2002/limen/pull/113)) | Yes: 166/166 CLI tests on SDK 10 and 8; CI green | 12 controlled examples matched the real Mono interpreter byte for byte: 0 false positives (7) and 0 false negatives (6). On R5 it errors on the failing commit `ec6407b` (70,720 B in `View.project`) in 0.41 s and passes the fixed commits. The CI time it replaces is not available; the app was broken about 90 min by commit timestamps. |
| Integration contracts (B) | Yes: contract `praxis.integration-contract/1` and `praxis streams check` ([praxis#295](https://github.com/kemiller2002/praxis/pull/295)) | Yes: 5 negative controls; full suite 2434/2434; CI green | Retrospective R5 replay: 7 of 7 real conflicts flagged before merge (near-guaranteed by construction), 50% precision (7 flags on clean merges). Ownership rule as reconstructed is noisy (164 findings). 3 of 19 reconcile commits attributable from the contract alone. **No reduction in integration work demonstrated.** |
| Acceptance planning (C) | Yes: per-obligation contracts, compiled package v2 ([Indy-init#31](https://github.com/kemiller2002/Indy-init/pull/31)) | Yes: full compiler suite; all 6 required rejections plus forged contracts, each with its own diagnostic; a broken checker fails 15 controls; CI green | All 108 P0 requirements / 326 obligations represented. R4 and R5 baselines byte-identical (verified independently). The UI-011 unit-test-only case stays UNVERIFIED. Re-scoring R4: INVALID (590 unbound test claims) instead of FAILED, i.e. stricter. |
| Persistence safety (D) | Yes: Arca 0.6.0, breaking, owner-approved ([arca#46](https://github.com/kemiller2002/arca/pull/46)) | Yes: compile-failure fixtures prove forged synchronized state, forged receipts and state copy-updates do not compile; 11 scenario tests; 380/380; CI green | The R4 defect class (application code declaring persisted state) is unrepresentable outside Arca. Runtime-only risks remain and are documented (deliberate wiring to another provider, dishonest adapter, stored-state decoding). |
| Browser environment (E) | Yes: test-environment contract and `conditor test-env preflight` ([conditor#117](https://github.com/kemiller2002/conditor/pull/117)) | Yes: all 7 required failure types plus controls; 1278/1278; CI green | Each fault type is detected in 7–175 ms (a non-HTTP port holder takes ~2 s). The R5 CI time-to-failure it replaces is not available; commit-to-fix for the port fault was 27 min 55 s. |
| Forma layout (F) | Yes: typography roles, Forma 0.11.0 ([forma#171](https://github.com/kemiller2002/forma/pull/171)) | Yes: 21 tests failing on main now pass (31/31); LCE-001 regression proven by revert; Forma Studio unchanged (113/113, 74/74); CI green | The R5 override (`causa.css:342-353`) becomes unnecessary for an adopting app. Chromium only locally; Firefox and WebKit through CI. |
| Incremental verification (G) | Yes. Ordo: policy-aware freshness ([ordo#108](https://github.com/kemiller2002/ordo/pull/108)). Dokimos: passing and failing evidence in the existing store; failures block reuse ([dokimos#58](https://github.com/kemiller2002/dokimos/pull/58)) | Ordo: 947 passed, 1 skipped; 8 negative controls; CI green. Dokimos: 535/535 locally (CI pending at writing); every incorrect reuse refused (660 attempts per sequence, 0 accepted) | Dokimos replay of the last 20 commits (one repository, 3 test projects, shared machine): dependency-aware selection ran 3,461 tests instead of 8,617 (−60%), wall-clock 1,107 s vs 3,444 s. Selection **alone** kept a seeded failure visible on only 4 of 9 later defect commits; with failure-aware reuse, 9 of 9. Reuse saved no further runs on clean history (63.3% hit rate, same set). Ordo: **not measurable yet**, because the R5 plan binds no test to any obligation. Tokens not measured. |
| Experimental validity (H) | Yes: baseline and pre-registered protocol ([Indy-init#30](https://github.com/kemiller2002/Indy-init/pull/30)) | Yes: headline baseline numbers recomputed from git | Protocol defines a control group and four treatments on equivalent task packets. **No controlled run has been performed**, so no treatment is "improved". |

## What worked, failed, is mixed or is unverified

- **Worked** (defect prevented, proven by reproduce-then-prevent):
  - A (WASM frame check)
  - C (mapping, swap and alteration rejections)
  - D (forged persistence state does not compile)
  - E (environment faults caught in preflight)
  - F (type roles, corner containment)
- **Mixed:** B. It gives earlier detection with 50% precision; the ownership rule needs tuning before it is useful.
- **Measured (retrospective, one repository):** G. Selection saves about 60% of test execution. Without failure-aware reuse it also hides failures, which is why the two must ship together.
- **Unverified as an efficiency gain:** B, C, G and H. No controlled experiment has been run, and token or tool-use telemetry was never collected in either rehearsal.
- **Not implemented here:**
  - a Dokimos startup oracle for WASM apps (the static check shipped; a runtime probe is application-specific);
  - Ordo reading C's per-obligation contract (it still applies requirement-level evidence to every obligation);
  - Rehearsal adoption, which happens through a future rehearsal's pinned stack.

## Owner decisions and follow-ups

1. **Merges that publish:** merging arca#46 publishes Arca 0.6.0, and merging forma#171 publishes Forma 0.11.0.
2. **Security rule in C:** the security evidence-category rule matches requirement titles and covers only AR-014 and PF-015. It needs review.
3. **Test bindings:** future rehearsals are INVALID under C until tests are bound to obligations in `testBindings`.
4. **Stack release:** adoption needs a new echelon-current set. Rehearsal 5 pins 1.29.0.
5. **Dokimos records:** provenance records are unsigned (an edited outcome alone is not detected), and `Category` is declared by the caller.
6. **Praxis versions:** several repositories run Praxis versions without `inbox claim`. Their intake documents are attached to work items but not claimed.
7. **Telemetry:** token and tool-use telemetry must be ingested before any efficiency claim is possible.
