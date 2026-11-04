# Rehearsal 010 final readiness

## Decision: READY FOR OWNER APPROVAL

The engineering work is done, verified and independently reviewed:
- no blocking review finding is open;
- local verification passes;
- CI passes on the integrated head.

**Rehearsal 010 is not READY TO LAUNCH.** Owner actions are still outstanding (§8): package freeze, Dokimos release, scope approval, contract approvals, lock adoption and merges. No agent can perform them (RDP-003). Rehearsal 010 was not started and its clock was not consumed.

## 1. Integrated result

| Item | Value |
|---|---|
| Repository and branch | kemiller2002/Indy-init `claude/rehearsal-010-integration` |
| Pull request | **#104** (draft): integrates #101 and #102 on `main` `8672bc1` |
| Integrated head | **`6e43b7a`** (every result below is for this commit) |
| Successor package | `requirements-compiled/v4.1.4` (#101's; v4.1.3 removed). Validation PASS, status CANDIDATE, X01–X21 PASS |
| Scope | `scope-manifest.json`, scopeSha256 `b407b5cc5f200e3651088440c0db630f18e84357c933020970f236f18f9352d6` |
| Proposed lock | `kickoff/rehearsal-010/toolchain.lock.proposed.json`, regenerated at `6e43b7a`; its digests match the contract files |

**Other pull requests**

| PR | State | Purpose |
|---|---|---|
| kemiller2002/Indy-init#101 | open | Superseded by #104: v4.1.4, suite repair, scope checkpoint. Close after #104 merges (ODR-R10-16) |
| kemiller2002/Indy-init#102 | open | Superseded by #104: zero-test refusal, fixture, scope, Continuation. Close after #104 merges |
| kemiller2002/dokimos#69 | draft, CI green | Release 0.11.1, carrying url-input (#67). Dokimos #68 was a duplicate of #67 and is closed unmerged; nothing is lost (INTEGRATION-BASELINE) |
| kemiller2002/specula#11 | draft, CI green | Observer-findings adapter (CAP-016), read-only |

## 2. Scope counts

**Requirements**
- 199 requirements, 194 of them mandatory;
- 863 obligations: 791 executable and 72 record;
- 161 P0 requirements of the frozen plan (613 obligations);
- the 38 of catalog CAT-CAUSA-INVESTIGATION (250 obligations);
- 0 excluded.

**Slice manifest** (`slice-execution-manifest.json`)
- 35 slices;
- 183 requirements assigned to slices;
- 16 full-release requirements;
- 0 unassigned; coverage `complete: true`.

**Cross-checks**
- `praxis-work-inventory.json`: derived from the same plan.
- #101's inventory matches as a cross-check (INTEGRATION-BASELINE).

**Catalog successor (proposed):**
- 44 requirements and 277 obligations;
- adds INV-039..044;
- compiles under Praxis;
- awaits ODR-R10-02.

## 3. Verification results at `6e43b7a`

**Local run** (`.NET 10.0.402`, Chromium; WebKit is not installable in this container):

| Check | Result |
|---|---|
| Build: requirements-compiler, tests (Debug) and src (Release) | PASS |
| Offline suite, including the v4.1.4 scope, progress, stop decisions, suite runners and F#-only tooling | PASS (all groups) |
| `tools/release-gate-repair/tests.fsx` | 60 cases, 0 failed |
| `tools/r9/tests.fsx` | 80 cases, 0 failed |
| `tools/r10/adversarial.fsx`, with `--browsers true --dotnet-tests true` | **36 VALIDATED**, 0 not run, 0 failed |
| `tools/r10/r9-replay.fsx`, with `--suites true` | 15 of 15 as expected |

**CI on `6e43b7a`.** Every job passed:
- `offline-contract-tests`
- `offline-factory`
- `frozen-v4`
- `fsharp-tooling`
- `release-gate-repair`
- `r9-readiness-gates`
- `suite-commands` (WebKit)
- `r10-adversarial` (Chromium **and WebKit**; the 36 cases, the fixture journeys and design contract, and the replay)

The evidence is committed in [`evidence/adversarial/`](evidence/adversarial/) and [`evidence/r9-replay/`](evidence/r9-replay/). Local paths are normalised to `$SRC`, `$WORK`, `$EVIDENCE`, `$R9` and `$SCRATCH`.

**Nothing is labelled PASS that did not run.** The following are not run here, and are stated as limits (§6), not as passes:
- a real GitHub journey;
- the journeys against Rehearsal 009's built application.

## 4. What was implemented (R1–R8, slices, observer)

| Audit item | Where | Proved by |
|---|---|---|
| R1 Executable verification (zero-test refusal, `--require-tests`) | `r9.fsx run-suites` / `suite-preflight` | C01, release-gate-repair 2b |
| R2 Governed catalog acceptance | `CatalogAcceptance.fs`, gate `--catalog-acceptance` | C04, C17, r9 tests |
| R3 Owner-controlled journeys J1–J3 (Chromium, WebKit, real provider) | `verification/journeys` | C06–C08, C22 |
| R4 Catalog successor INV-039..044 | `proposals/…successor.proposed.json` | Praxis compile |
| R5 Forma design contract | `DesignContract.fs`, `verification/design-contract` | C05, RP-01..05 |
| R6 Scope report: 8 states, CONTINUE_REQUIRED | `Ledger.fs`, `ledger`, `completion-gate` | C02, C03, C21, C25, C32, RP-13 |
| R7 Stop decisions bound to the original deadline | `Continuation.fs`, `stop-decision`, `resume` | C12–C15, C20, C26, C30, C34 |
| R8 Requirement conflicts | OWNER-DECISIONS-REQUIRED §D | owner decision |
| Slices: derived manifest, slice gate, completion gate | `slice-manifest`, `slice-gate`, Verification gate | C09–C11, C29, C35 |
| Praxis sequencing (scope checkpoint before implementation) | `scope order`, `check-sequencing` | C16, C24, RP-11, RP-12 |
| Test integrity | `test-integrity` | C18, C23 |
| Observer (read-only; recommends only existing holds) | `Observer.fs`, `observe`, Specula #11 | C27, C28, C31, C33, C36, RP-14 |

## 5. Independent review

Five rounds of review by reviewer agents spawned by this session. Each saw the code, not this document's conclusions.

| Round | On | Blocking | Major | Fixed in |
|---|---|---|---|---|
| 1 | the first integrated head | 16 findings (blocking and major) | — | `b65d2f7` |
| 2 | `b65d2f7` | integration was judged from builder events, not only governed evidence; the live clock | — | `d841ac2` |
| 3 | `d841ac2` | 2: self-made deadline policy; record-only dependencies blocked forever | 3 | `9dd597f` (C28–C32) |
| 4 | `9dd597f` | **0** | 3: forgeable completion verdict; offset-less T0; vacuous dependency | `fad97aa` (C33–C35) |
| 5 | `fad97aa` | **0** | 2: undated request skipped checks; observer crash on builder timestamps | `6e43b7a` (C36) |

**Verdict:** no open blocking finding. Every major finding is fixed and has an adversarial case. ODR-R10-15 asks the owner whether agent review suffices.

**Minor items accepted and documented** (not fixed):
- `CompletedItems` (a Praxis completion record) is builder-written. A record-only dependency's slice-order check therefore rests on that record. Record obligations are still judged at release.
- The observer accepts any governed COMPLETE verdict for a commit on `main`; the decision does not carry its own candidate.
- In the builder's own commands, `--t0` (accepted only with `--replay-clock true`) and `resume --at` are still read with the machine's local time when they lack an offset. Only the builder's own run is affected. Request files, the T0 record and everything the observer reads require an explicit offset.
- `contents: write` reaches the steps that run candidate code with the live suites (ODR-R10-09 names a test repository the owner accepts writes to). The token is not persisted in the checkout.

## 6. Known limits

- **Builder-authored probes.** Fault probes (`.echelon/fault-probes.json`) are written by the builder. Governed acceptance requires a detected probe for content round-trip obligations, but the probes themselves are not owner-authored.
- **Self-reported compute.** No compute limit is approved (ODR-R10-13); a compute stop needs an owner record.
- **Browsers.** The local browser runs are Chromium only. WebKit ran in CI.
- **No real GitHub run.** No journey ran against real GitHub here (no designated test repository, ODR-R10-09). The harness uses a REST double, and the gates refuse a double as live evidence (C08, LIVE_PROVIDER_SUBSTITUTED).
- **Rehearsal 009 journeys.** J1–J3 and the rendered design contract were not run against Rehearsal 009's application (R9-ADVERSARIAL-REPLAY, "Not detected").
- **Flake.** `fsharp-tooling` has a known layout-kit timing flake. It was green on `6e43b7a`.
- **The observer is the deadline authority.** stop-decision runs in the builder's repository, so a builder can feed it a self-made lock. The independent check is the observer: it derives the deadline from the committed T0 record and the governed policy (C28). Specula shows its findings.

## 7. Releases prepared (owner merges and tags)

- **Dokimos 0.11.1** (kemiller2002/dokimos#69): version bump and Praxis DOK-REL-0111. url-input from #67. A new echelon-current set must carry it (ODR-RG-03).
- **Specula** (kemiller2002/specula#11): `ObserverFindings.fs`, its evidence registry entry, tests and CAP-016 (ODR-R10-17).

## 8. Owner decisions outstanding

These are all in OWNER-DECISIONS-REQUIRED.md:

| Group | Decisions |
|---|---|
| Package | ODR-V414-01, -02 (decide), -03 (approve, tag) |
| Releases and lock | ODR-RG-03 (Dokimos 0.11.1 and the echelon-current set), ODR-RG-04 (commit the lock), ODR-RG-05 (topology) |
| Scope | ODR-R10-01: approve scope `b407b5cc…` |
| Contracts | ODR-R10-02 … -09: catalog successor, catalog contract, governed fact, design contract, deadline policy, journey bindings, loading priority, test repository |
| Conflicts | ODR-R10-10 … -12 |
| Process | ODR-R10-13 (compute), -14 (process evidence), -15 (review sufficiency), -16 (merge #104; close #101 and #102), -17 (Specula) |

## 9. The exact next launch command

**Run only after LAUNCH-CHECKLIST §A–C are all checked.** Until then this command must not run.

It starts the builder in the empty competition repository, with `SRC` = the Indy-init checkout at the lock's governing commit (the #104 merge commit):

```
cd <competition repository clone> && \
SRC=<Indy-init checkout at the lock's governing commit> && \
claude -p "$(sed -n '/^> \*\*Objective/,/^$/p' "$SRC/docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md" | sed 's/^> //') SRC is $SRC. Follow $SRC/docs/rehearsal-010-readiness/STARTUP-INSTRUCTIONS.md."
```

The prompt is exactly STARTUP-INSTRUCTIONS §0, with nothing narrower added. T0 is the builder's first commit, the bootstrap with `evidence/t0/t0.json` (§2).

The owner schedules the observer (STARTUP-INSTRUCTIONS §9) at T0.
