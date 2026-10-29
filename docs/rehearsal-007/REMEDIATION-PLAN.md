# Rehearsal 7 remediation plan

Generated from [`work-packages.json`](work-packages.json); the JSON is authoritative. The requirements themselves are the frozen baseline and UAE-001; this plan does not restate them. Evidence for every Rehearsal 6 item is in [REMEDIATION-BASELINE.md](REMEDIATION-BASELINE.md).

**Principle.** Rehearsal 7 builds Causa from zero application code. A Rehearsal 6 application defect stays fixed only where the behaviour is owned by a reusable component (Arca, Forma, Conditor, Praxis) or by a frozen requirement and test that Rehearsal 7 must pass. No Rehearsal 6 evidence was changed.

| Package | Title | Owners | Work items | Pull requests | Status |
|---|---|---|---|---|---|
| R7-REM-001 | Cross-tab persistence correctness | arca, indy-init | arca WI-0038 | arca#47 | **IMPLEMENTED_IN_REVIEW** |
| R7-REM-002 | Offline retry and save-state correctness | arca | arca WI-0037 | arca#47 | **IMPLEMENTED_IN_REVIEW** |
| R7-REM-003 | GitHub credential separation | owner (repository secrets), indy-init | — | — | **BLOCKED** |
| R7-REM-004 | CI concurrency and scheduling | conditor, praxis | conditor WI-0063, praxis WI-0106 | conditor#122, praxis#300 | **PARTIAL** |
| R7-REM-005 | Unattended PR execution | praxis, conditor | praxis WI-0104, conditor WI-0062 | — | **PASS_ADVISORY** |
| R7-REM-006 | Automated acceptance evidence | indy-init, rehearsal repository | — | — | **PLANNED** |
| R7-REM-007 | Candidate and deployment gates | conditor, rehearsal repository | conditor WI-0063 | conditor#122 | **PARTIAL** |
| R7-REM-008 | Defect-class closure | indy-init | — | indy-init (this branch) | **IMPLEMENTED** |
| R7-REM-009 | Forma accessibility corrections | forma | forma WI-0041 | forma#173 | **IMPLEMENTED_IN_REVIEW** |
| R7-REM-010 | Release provenance | indy-init | — | indy-init (this branch) | **IMPLEMENTED** |
| R7-REM-011 | Independent-review corrections | indy-init, arca | — | indy-init (this branch), arca#47 | **IMPLEMENTED** |
| R7-REM-012 | Rehearsal 7 readiness | indy-init | — | indy-init (this branch) | **IMPLEMENTED** |

## R7-REM-001: Cross-tab persistence correctness

**Addresses:** R6-D-002, AR-035-O4, AR-035-O5. **Depends on:** nothing. **Status:** IMPLEMENTED_IN_REVIEW.

**Plan.** Do not build another cross-tab store. Rehearsal 6 kept one localStorage record read-merge-written by every tab (src/persistence/Sync.fs:179, main.ts:110 skips exclusion without navigator.locks). localStorage has no compare-and-swap, so no lease on it is correct (both R6 lease trials failed). Arca.Limen already provides one owner per namespace through a Web Lock, a fencing epoch raised by compare-and-put inside an IndexedDB transaction, and refusal of a stale owner's save; without Web Locks it refuses durable ownership. Prove that in a real browser and require Rehearsal 7 to adopt it (D-07 was the deviation that bypassed it).

**Scope after ADR-R7-001.** ADR-R7-001 (owner-approved, merged in Indy-init #41) closes R6-D-002 and R6-D-003 for Causa in the requirements: no browser storage, no offline queue, no cross-tab locking, GitHub expected-head commits only. They are verified only when Rehearsal 7's candidate passes PAT-01..15. This package remains a correctness fix to Arca as a library (Arca stays in the release set); Causa may not use its offline queue or IndexedDB bridge (PERSIST-026).

**Tests.**
- arca verification/browser/tests/multi-tab-safety.spec.mjs: no Web Locks (three tabs refused durable ownership, nothing written); seeded random three-tab interleavings (enqueue, save, steal, late save from a fenced tab, owner close, reopen) losing no acknowledged key; storage restored into a new context
- existing arca limen-queue.spec.mjs: hand-over and fencing
- arca MultiTabQueueTests (F#)

**Acceptance.**
- Zero lost acknowledged saves in every interleaving run
- Unsupported coordination fails closed when MemoryOnly is not declared
- Independent reviewer reproduces the R6 scenario against Arca and finds no loss

**Evidence.**
- arca#47 CI at ac585ba: build-and-test, browser (Chromium and WebKit), validate, foundations all green
- Local Chromium: 14/14 browser specs; seeds 1-3 covered 31 acknowledged saves, 5 fenced saves, 19 steals, 10 owner closes

**Remaining.**
- Merge and release arca#47 (owner): a library fix for Arca's other consumers
- For Causa: PAT-01, PAT-02, PAT-11, PAT-13 during Rehearsal 7 (DEFECT-TRACEABILITY.md)

**Independent review.** Independent review (agent, not the implementer): R6-D-002 corrected with Web Locks; no loss found, including a concurrent save-versus-steal experiment (6/6). Finding 5: with QueueOptions.standard (MemoryOnly declared) and no Web Locks the queue is per-tab memory-only: no cross-tab loss, but unsent changes do not survive closing the tab, and SaveState does not show durability.

## R7-REM-002: Offline retry and save-state correctness

**Addresses:** R6-D-003, UI-010-O2, RF-F-3. **Depends on:** nothing. **Status:** IMPLEMENTED_IN_REVIEW.

**Plan.** Root cause (R6, verified in code): Limen reports an aborted POST as an unknown outcome while navigator.onLine stays true; reconciliation finds it not landed; the host re-runs synchronization at once (151 POSTs in 60 s, about 5,000 per tab in 5 minutes). Arca's OfflineSync returned an untyped Deferred and left timing to every host. Add to Arca's pure core: typed deferrals, a validated retry policy (exponential backoff with jitter), a schedule that only a provider-confirmed entry resets, and a save state derived from queue and schedule.

**Scope after ADR-R7-001.** ADR-R7-001 (owner-approved, merged in Indy-init #41) closes R6-D-002 and R6-D-003 for Causa in the requirements: no browser storage, no offline queue, no cross-tab locking, GitHub expected-head commits only. They are verified only when Rehearsal 7's candidate passes PAT-01..15. This package remains a correctness fix to Arca as a library (Arca stays in the release set); Causa may not use its offline queue or IndexedDB bridge (PERSIST-026). Arca.GitHub's fixes (5xx is transient, a 5xx or unreadable write answer is an unknown outcome reconciled by key, NOT_LANDED) apply if Rehearsal 7 uses Arca.GitHub for GitHub access, which ADR-R7-001 permits.

**Tests.**
- SyncScheduleTests: a naive host reproduces the storm (>50 POSTs); the schedule bounds it to <=5 in 60 s; mutation (reset on any progress) gives 76
- AdapterScheduleTests: the real GitHubStorage provider over FakeGitHub on a simulated clock: offline (0 writes), lost writes (<=25 write requests in 60 s, never confirmed), 503 and 500 retried as transient, recovery with exactly one ref update, 403 once, 401 once despite edits, repeated online events, lost write then offline
- FsCheck properties: delay bounds, monotone ceiling, AwaitApplication never due, confirmed iff synchronized, connectivity idempotence

**Acceptance.**
- No tight retry loop with the device offline or writes lost
- No retry of a permanent or credential failure without the application resolving it
- Never 'confirmed' before a provider receipt
- Recovery after connectivity returns
- Independent verification

**Evidence.**
- arca#47 CI green at ac585ba
- 415/415 Arca tests locally; 14/14 browser specs

**Remaining.**
- Merge and release arca#47 (owner)
- For Causa: PAT-03, PAT-05..08, PAT-15 during Rehearsal 7 (RF-F-3 is PERSIST-008, PERSIST-012, PERSIST-015, PERSIST-024)

**Independent review.** Three independent review rounds. Round 1 BLOCKING (5xx misclassified; repeated online events reset backoff; edits retried a revoked credential; checking-outcome hid waiting-for-connection), fixed in 2c265c2. Round 2 confirmed three fixes and found 5xx in identity resolution, a ref-update resend burst (N1), connectivity never restored, and the Aborted label, fixed in 3ac5652. Round 3 PASS_WITH_FINDINGS (low): L2 NOT_LANDED code and L3 Online only from provider answers fixed in ac585ba; L1 (up to 36 write requests in the first minute when every ref update fails, decaying; R6 sent 151) documented in the CHANGELOG. The ac585ba fixes have tests but no fourth review.

## R7-REM-003: GitHub credential separation

**Addresses:** D-09, PF-015-O2, EXT-DATA-004-O2, EXT-DATA-005-O1. **Depends on:** nothing. **Status:** BLOCKED.

**Plan.** Rehearsal 6's CAUSA_GITHUB_TOKEN could write. Credentials are owner-held: create a fine-grained read-only token (or GitHub App installation) for the data repository and a write token limited to kemiller2002/causa-write-test, store them as CAUSA_GITHUB_TOKEN and CAUSA_WRITE_TOKEN, and prove the actual permissions with real requests (see SECURITY-REVIEW.md).

**Tests.**
- Probe procedure in SECURITY-REVIEW.md: read succeeds and write is refused with the read token; write succeeds in the test repository and is refused elsewhere with the write token

**Acceptance.**
- Both probes recorded against real GitHub before kickoff

**Remaining.**
- Owner action: create and store the two credentials; then the agent runs the probe

**Independent review.** Not run.

## R7-REM-004: CI concurrency and scheduling

**Addresses:** CI starvation 22:22-23:54, MSB3030 build race. **Depends on:** nothing. **Status:** PARTIAL.

**Plan.** Measured cause: an agent-edited pipeline imported from R5 used a per-commit concurrency group on main (still at build-and-test.yml:39), job-level always() started cancelled runs' jobs, and validation ran twice per push on every branch. Conditor's own template was safe; nothing checked its replacement. Add conditor ci-policy check (CIP-000..008) that fails closed, a tier-3 candidate-acceptance template nothing can cancel, foundations and Praxis validation workflows that run once per ref, and timeouts.

**Tests.**
- CiPolicyTests: every rule, the passing shapes, the reader failing closed, every scaffolded workflow passing, Rehearsal 6's real workflows failing with CIP-001 at build-and-test.yml:39
- 19 regression checks from the independent review (flow mappings, fixed group across refs, cancelled(), sha-named candidates, branches-ignore, workflow_run, ${{ false }})

**Acceptance.**
- No recurrence of the starvation pattern in a controlled commit burst
- Candidate verification completes during continued development
- Measured queue depth, time to first result, cancellation latency, workflow minutes

**Evidence.**
- conditor#122 CI green (24 checks)
- praxis#300 CI green (26 checks)
- conditor ci-policy check reproduces the Rehearsal 6 starvation on the R6 workflows (tests/fixtures/ci-policy/rehearsal-006) and passes the corrected scaffold
- evidence/r6-workflow-runs.tsv: the 22:22-23:54 run history

**Remaining.**
- Experiment B (live commit bursts of 5 and 15, concurrent streams, a candidate during development) needs a disposable repository with these workflows: NOT_RUN
- MSB3030: analysis only (CI-PERFORMANCE.md); the R6 fix isolates IntermediateOutputPath per configuration; no parallel-build reproduction was run

**Independent review.** Independent review of conditor#122: PASS_WITH_FINDINGS; F1-F8 fixed in 9916fa9 with regression checks (not re-reviewed). praxis#300 is a three-line template change covered by CIP rules.

## R7-REM-005: Unattended PR execution

**Addresses:** D-06, UAE-R7-01..12. **Depends on:** nothing. **Status:** PASS_ADVISORY.

**Plan.** Already built before this initiative (UAE-001): Praxis policy-gated merge with signed run authorization, conditor unattended-readiness. Do not create a competing implementation.

**Tests.**
- conditor unattended-readiness against kemiller2002/indy-init-rehearsal-007 main

**Acceptance.**
- A real PR merges without routine owner interaction, with a verified receipt
- The gate is enforced by GitHub (UAE-R7-12), not advisory

**Evidence.**
- praxis docs/autonomous-execution/evidence/2026-10-29/readiness-receipt.json: READY, run RUN-R7-READINESS-20261029-01, probe PR #20 merged as 9c3aaf6 (recorded by the earlier session; this session could not re-read indy-init-rehearsal-007)

**Remaining.**
- UAE-R7-12 owner action: trust store in an Actions secret and a ruleset on main requiring CI, independent-review and the Praxis evaluate job, with no human review
- Re-run unattended-readiness with the kickoff authorization at kickoff (UAE-R7-06)

**Independent review.** Praxis SECURITY-REVIEW.md F1: accepted open risk (agent-held key, owner decision 2026-10-29).

## R7-REM-006: Automated acceptance evidence

**Addresses:** 26 NOT_RUN, 12 UNVERIFIED, CAUSA_PLAN missing in browser shards, PF-013-O2 fixture drift, EXT-DATA-004-O1 / PF-015-O3 fixtures tripping the credential scan. **Depends on:** R7-REM-005. **Status:** PLANNED.

**Plan.** Classify every NOT_RUN and UNVERIFIED item (REMEDIATION-BASELINE.md): 72 of 89 non-passing plan obligations wait for independent-review attestation and 4 for owner authorization; none is missing implementation. Generate evidence where it is produced: CI sets CAUSA_PLAN in every job that runs bound tests; the credential scan excludes declared test fixtures by manifest, never by weakening; live-fixture drift is re-recorded at preflight; independent review is a CI job (UAE-R7-04) and owner decisions are approving reviews on bot-opened PRs (UAE-R7-10).

**Tests.**
- Experiment D (a representative workflow ends with every applicable requirement in a truthful state): NOT_RUN

**Acceptance.**
- No NOT_RUN caused by sequencing
- No PASS without evidence

**Remaining.**
- Kickoff workflow changes belong to the rehearsal repository created at kickoff; they are requirements in EXECUTION-PLAN.md, not code here

**Independent review.** Not run.

## R7-REM-007: Candidate and deployment gates

**Addresses:** first candidate not deployable, D-08, DCC-008. **Depends on:** R7-REM-004. **Status:** PARTIAL.

**Plan.** A candidate is selected only from a green core run of its exact SHA; candidate-acceptance.yml (tier 3) re-verifies that SHA, refuses anything not on main and records conditor.candidate-verification/1; deploy is gated on it and deployed-smoke compares served SHA with the candidate (R6 proved deployed = source).

**Tests.**
- Experiment E: NOT_RUN

**Acceptance.**
- Served SHA equals candidate SHA, checked on the served application

**Evidence.**
- Template and policy in conditor#122

**Remaining.**
- Not yet exercised on GitHub: first exercised at Rehearsal 7 T+300

**Independent review.** Template reviewed in the conditor#122 review (pinning and ancestry check judged sound).

## R7-REM-008: Defect-class closure

**Addresses:** G7, D-11, RF-F-6. **Depends on:** nothing. **Status:** IMPLEMENTED.

**Plan.** requirements-compiler verify-defect-closures checks each claimed closure against the rehearsal's own history (DCL-001..009).

**Tests.**
- DefectClosureTests over a throwaway git history (every rule; mutation of the ancestry check fails 2)
- Run against indy-init-rehearsal-006 46d5e11 + records bf190b6: REFUSED, all 8 closures (DCL-002 R5 SHAs, DCL-007 missing review evidence, DCL-005 DCT-004-A present only in a comment)

**Acceptance.**
- Closures that cite absent commits are refused
- Run before the candidate freeze

**Evidence.**
- Offline test suite passes at 2e4b561
- evidence/defect-closures-r6.json: Rehearsal 6 (46d5e11, records bf190b6, base c80f35e bound by the kickoff record): REFUSED, 8/8 closures

**Remaining.**
- Wire into the Rehearsal 7 acceptance job before the freeze, with --base set to the kickoff commit
- Merge indy-init#42 (owner)

**Independent review.** Independent review: BLOCKING (F1: closures could be satisfied by tests outside the rehearsal, comment mentions, evidence older than the fix, closedAt before the fix, and unknown statuses). Fixed in 6bde211 (required --base, word-boundary test titles excluding skipped forms, evidence changed after the fix, closedAt >= fix time, curated classes MISSING, unknown status refused, INCOMPLETE without curation or freeze, closures source and sha256). Re-review at 41ff524: F1 FIXED; new BLOCKING N1 (a `./` path prefix let the closures record serve as its own review evidence; skips via testInfo.skip() or on the declaration line went undetected). Fixed in 6d05613 (normalised paths and blob-identity against the closures record; skip detection shared in TestBlocks). Documented limits: an unrelated commit descending from the base can be named as the fix; agent-written review evidence cannot be told from a reviewer's without identity or attestation. Third review at eb978e2: N1 FIXED; new BLOCKING D1 (a regex literal after `)` desynchronises the test lexer and hides a skip: false VERIFIED) and D3 (a lightly edited copy of the closures record passes as review evidence). Fixed in e05c776: the test lexer fails closed (UNPARSEABLE files are not read and keep the verdict at best INCOMPLETE); review evidence must be an indy-init.independent-review/1 record naming the class, the full fix SHA and verdict CLOSED. Fourth review at c08c23e: all earlier reproductions caught; Rehearsal 6's whole tests/ tree (144 files) parses; BLOCKING E1 (a `}` closing an object literal followed by division still desynchronises the lexer: false VERIFIED and false PASS), E2 (a file ending in a line comment without a newline is wrongly UNPARSEABLE), E3 (JSX/TSX), E4 (reporting nits). Fixed at c33dc29 (0f9816d, 7f1c0a2): brace-kind tracking, ambiguous cases UNPARSEABLE, end of input closes a line comment (0 of 3113 real files UNPARSEABLE), .jsx/.tsx reported as unsupported, and the base is now bound to the kickoff record like the ratchet's. Fifth review at c33dc29: PASS_WITH_FINDINGS; G1 (braces inside a template's ${...}) fixed at 1c3a7d0 with a reviewAttention list of files the independent review must read; confirmation at 1c3a7d0: PASS_WITH_FINDINGS, H1 (postfix ++/-- before /) and H2 (noisy attention list) fixed at 2e4b561. Final: the reviewer judges the residual risk of the heuristic lexer adequately documented in each report's limits, provided the review reads every reviewAttention file.

## R7-REM-009: Forma accessibility corrections

**Addresses:** UI-016-O11, PF-016-O1. **Depends on:** nothing. **Status:** IMPLEMENTED_IN_REVIEW.

**Plan.** Causa outlined a Forma diagram node with #e9a23b on #f5f3ed (1.95:1). Forma's workflow validation checked only text contrast and its diagram vocabulary had no status tokens. Add token:status-* to the vocabulary and A11Y-NONTEXT-CONTRAST to validation; prove token pairs per theme and the rendered result in a browser.

**Tests.**
- F# workflow tests 84/84 (R6 regression error at 1.95:1; corrected #9e6410 passes at 4.42:1)
- nontext-contrast token test: 30 theme/brand contexts, lowest 4.10:1
- Chromium computed-colour spec

**Acceptance.**
- Non-text indicators >= 3:1 in rendered output
- High contrast supported

**Evidence.**
- forma#173: rule, tokens, Chromium spec light/dark/forced colors; inventory regenerated in ac34a55 after CI validate failed on it

**Remaining.**
- Merge and release forma#173 (owner); CI green on ac34a55
- Percepta screen contracts verified in the Rehearsal 7 app

**Independent review.** Independent review: PASS_WITH_FINDINGS (false positive on borderless filled nodes; dark mode and legend swatches unchecked; app-CSS path undocumented; browser spec used hand-written markup). Fixed in fef95fc and af8903b; re-review PASS.

## R7-REM-010: Release provenance

**Addresses:** VER-001-O1, VER-002-O2. **Depends on:** nothing. **Status:** IMPLEMENTED.

**Plan.** requirements-compiler release-comparison observe/assess: query each selected system's release source immediately before the lock, digest the answers, compare, and require owner-approved deviations.

**Tests.**
- ReleaseComparisonTests (each status, version ordering)
- Live demonstration on the in-scope systems of R6's set: praxis 3.23.0, forma 0.11.0, arca 0.6.0 all CURRENT

**Acceptance.**
- The record exists before the baseline is frozen

**Evidence.**
- Offline test suite passes at c08c23e
- evidence/release-observation-demo.json and release-comparison-demo.json: live observation of praxis 3.23.0, forma 0.11.0, arca 0.6.0 at 2026-10-29T11:08Z, PASS (demonstration only, not the kickoff set)

**Remaining.**
- Run at Rehearsal 7 preflight against the kickoff set, before the lock
- Merge indy-init#42 (owner)

**Independent review.** Independent review: BLOCKING (F4/F5: hand-edited observations accepted, duplicates, wrong tag prefixes, npm latest-vs-highest, prerelease order, unapproved deviations, unbounded age). Fixed in afe7485 (answers stored, re-hashed and re-parsed, TAMPERED otherwise; recomputed source URL; duplicates refused; per-component tag prefixes; NuGet; semver prerelease order; deviations need decision and approvedBy; --locked-at with offset; max age <= 24 h). Re-review at 41ff524: F4 FIXED for inconsistent edits, F5 mostly FIXED; N5 fixed in 07be4f2 (AMBIGUOUS_COMPONENT, not a PASS status); N4 stated in the report's attestation field; N4 inherent: a recomputed digest over a forged answer is not proof, and observedAt, the lock record, approvedBy and deviations are caller-written. Proof needs observe to run in trusted CI with signed output. Third review at eb978e2: N5 FIXED; N4 documented; npm-versus-GitHub-releases ambiguity refinement and release paging in progress. Fixed in f29edf3: AMBIGUOUS only for the same GitHub release list and prefix; releases paged; limits stated in the report. Fourth review: N5 FIXED, N4 limit stated; no new release-comparison finding.

## R7-REM-011: Independent-review corrections

**Addresses:** RF-F-1..9, R1-F-3/4/5, 7 final tag changes, UI-034-O1 unflagged loosening. **Depends on:** R7-REM-001, R7-REM-002. **Status:** IMPLEMENTED.

**Plan.** RF-F-1/2 are R6-D-002/003. RF-F-3 is handled in Arca's classification. RF-F-8: requirements-compiler assertion-ratchet makes a removed or loosened assertion fail unless a resolved tag change covers it.

**Tests.**
- AssertionRatchetTests (duplicates, loosening, add-then-remove, F#, tag changes; set-semantics mutation fails 3)
- Run against R6 c80f35e..46d5e11 (base bound by the commit that added kickoff/indy-init.kickoff.json): both UI-034 weakenings plus 43 more unrecorded removals

**Acceptance.**
- Weakening is detected before review

**Evidence.**
- Offline test suite passes at 2e4b561
- evidence/assertion-ratchet-r6.json: Rehearsal 6 c80f35e..46d5e11, base read from the real kickoff record: FAIL, 45 unrecorded removals including UI-034-O1 and UI-034-O2

**Remaining.**
- RF-F-4..7, RF-F-9 are process items carried into EXECUTION-PLAN.md
- Merge indy-init#42 (owner)

**Independent review.** Independent review: BLOCKING (F2/F3: skip/fixme/only markers, block comments, multi-line statements and helper pseudo-tests evaded the count; the base was caller-chosen). Fixed in ddc18bb (per-statement counting, markers, comments, test.* blocks, tag-change exemption through PlanVerification rules, required --kickoff binding the base, full SHAs). Re-review at 41ff524: F2 FIXED, F3 PARTIAL (any non-empty resolution accepted); new N2 (the kickoff record was any file on disk, so the base was still self-asserted) and N3 (assertions hidden in template literals; testInfo.skip(), test.info().skip(), aliased skip). Fixed in 865d443: the base is the commit that first added the kickoff record in the candidate's first-parent history (a mismatching --base is REFUSED); assertions matched on string- and template-masked text; skip markers anywhere including aliases in the file; a tag change needs PlanVerification.tagChangeAdmissible. Third review at eb978e2: N2, N3, F3 FIXED; new BLOCKING D1 (lexer desync counts a dead assertion as live), D2 (a caller-chosen --kickoff-path gave PASS on Rehearsal 6) and D4 (tag-change reviews are free text in a file outside the repository); D5 low (over-broad base skip exclusion). Fixed in 7675e38 and 6edade9: UNPARSEABLE files block PASS; only kickoff/indy-init.kickoff.json binds the base; tag changes are read from the records commit and must point to review records there (a file from disk is at best INCOMPLETE); conditional base skips count as live and exclusions are reported. Fourth review at c08c23e: all earlier reproductions caught; Rehearsal 6's whole tests/ tree (144 files) parses; BLOCKING E1 (a `}` closing an object literal followed by division still desynchronises the lexer: false VERIFIED and false PASS), E2 (a file ending in a line comment without a newline is wrongly UNPARSEABLE), E3 (JSX/TSX), E4 (reporting nits). Fixed at c33dc29 (7f1c0a2). Fifth review at c33dc29: PASS_WITH_FINDINGS; G1 (braces inside a template's ${...}) fixed at 1c3a7d0 with a reviewAttention list of files the independent review must read; confirmation at 1c3a7d0: PASS_WITH_FINDINGS, H1 (postfix ++/-- before /) and H2 (noisy attention list) fixed at 2e4b561. Final: the reviewer judges the residual risk of the heuristic lexer adequately documented in each report's limits, provided the review reads every reviewAttention file.

## R7-REM-012: Rehearsal 7 readiness

**Addresses:** R7-PF-01..14. **Depends on:** R7-REM-001, R7-REM-002, R7-REM-003, R7-REM-004, R7-REM-005, R7-REM-006, R7-REM-007, R7-REM-008, R7-REM-009, R7-REM-010, R7-REM-011. **Status:** IMPLEMENTED.

**Plan.** This package.

**Acceptance.**
- Every preflight gate has evidence

**Evidence.**
- READINESS.md

**Remaining.**
- See READINESS.md

**Independent review.** See READINESS.md: overall NOT READY pending owner actions.
