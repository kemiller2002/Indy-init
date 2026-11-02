# Independent review of v4.1.2, round 3

**Verdict: PASS** (commit f24b167). No blocking or major finding stands.

## What holds
- **Liveness.**
  - T-S65 now schedules the next automatic read at once.
  - T-S58 and T-S59 now schedule read 1, as T-S19 does.
  - No reachable (UnknownOutcome, event) stalls. Every path ends in a resolution, or in T-S64 or T-S30 with Check again offered. This holds whether or not the engine runs ReconcileDue while offline.
- **INV-04 window.**
  - Only ReconcileDue, T-S31 and T-S32 send a reconciliation read.
  - Each one sends only while fewer than four reads fall in the 60 s window, and records the read.
  - So at most four reads fall in any 60 s, per session.
- **Bootstrap.** AGENT-RULES and the bootstrap order now generate v4.1.2. The bootstrapped code carries `CommandEnvelope.Command`, `RequestContext.Now` and `ReconcileReadsSentAt`.
- **Provenance.**
  - The package applies exactly option A's `applies` for ODR-V412-01..03 (APR-V412-OWNER-1) and ODR-V412-05 (APR-V412-OWNER-2). -05 supersedes -01's T-S65.
  - Both approvals are owner commits, and each is the latest record of its decision.
  - No agent commit has touched the approvals file since 2dc5a05.
  - The requests are unchanged since each record. f24b167 changed only the `status` text.
  - v4, v4.1 and v4.1.1 equal their tags.
- **Evidence re-run.**
  - `--check` passes (X01–X20), with and without the Forma tarball.
  - The offline suite passes, including the real owner-record check.
  - The contracts typecheck, and both negative controls fail with FS1093.
  - Both bootstraps build.
  - Percepta exits 0 on v4.1.2 and 3 on v4.1.1.
  - 22 mutations were run. Each one is caught by X12, X17 or ownerRecords, except the two listed under RV412-3-2.

## Findings
| id | severity | one line |
|---|---|---|
| RV412-3-1 | minor | T-S65's "at once, replacing any pending one" has no contract mechanism. Timers have no replace or cancel, and ReconcileDue ignores a read already in flight. So a stale timer or a flap can cut short a read in flight and use up an automatic read. The at-once read also departs from INV-04's 2/4/8 s spacing, unless it counts as the "plus one per offline-to-online change". The four-in-60 s bound holds either way, and nothing stalls. |
| RV412-3-2 | minor | The owner's select records hold no digest of `applies`, and owner-freeze never shows `applies`. Editing a request's `applies` and the package the same way passes every check. Git history shows this has not happened, and ODR-V412-04 will digest the requests file. |
| RV412-3-3 | minor | Stale docs: ODR-V412-04's question omits -05, FREEZE step 1 doesn't say -05 is recorded, STARTING-STATE is titled v4.1.1, and the optimization-report banner covers v4.1.1 only. AMENDMENT says "INV-04 bounds reads" and counts "three" defects. X20's text says "succeeds 4.1.0". |
| RV412-3-4 | note | Still unstated: whether the 60 s boundary is open or closed, what happens if the clock is not monotonic, and how the T-S47 "pause" works. Both readings of the pause are now live. |
| RV412-3-5 | note | CI has no Percepta step, and its contract exclusion list is hardcoded. The owner-record check runs only in the offline suite. |
| RV412-3-6 | note | The "no agent commit" test reads the author email and trailers only. I checked by hand that both records are owner commits. |

## Round-2 dispositions
| id | was | now |
|---|---|---|
| RV412-2-1 | major | fixed: T-S65 schedules the read; residual in RV412-3-1 |
| RV412-2-2 | major | fixed: T-S58 and T-S59 schedule read 1 |
| RV412-2-3 | major | fixed: the bootstrap is v4.1.2; STARTING-STATE title residual |
| RV412-2-4 | minor | fixed: latest record, manifest digest, author email; residual in RV412-3-2 and -3-6 |
| RV412-2-5 | minor | (1) and (3) fixed or disclosed; (2) open: T-S19 and T-S29 still state no window |
| RV412-2-6 | minor | mostly fixed; optimization-report banner open |
| RV412-2-7 | note | partly disclosed; boundary and monotonic clock still open |
| RV412-2-8 | note | fixed: disclosed as a known limit |

Round 1 items still open: RV412-1-9 (the schedule margin; the owner decides) and RV412-1-10 (no Percepta step in CI, hardcoded exclusion list).

The full record is `independent-review.json`. The mutation script and its log are in `/tmp/claude-0/review412-r3/mut/`.

## Author dispositions (after the review)

- **RV412-3-1** (disclosed): AMENDMENT-4.1.2.md states that 'replacing any pending one' is an engine requirement (a superseded timer is ignored), that this read is INV-04's 'plus one per offline-to-online change', and that it is counted in the 60 s window. No transition or contract changed; a contract mechanism would need an owner decision.
- **RV412-3-2** (open for the owner): The select records hold no digest of 'applies'. The ODR-V412-04 package approval binds the manifest, which digests the requests file. Changing the record format is the owner's (owner-freeze.fsx) and is not part of this package.
- **RV412-3-3** (fixed (docs only)): ODR-V412-04's question names -05; FREEZE step 1 names APR-V412-OWNER-2; STARTING-STATE.md titled v4.1.2; the optimization-report banner covers v4.1.2; AMENDMENT's title and the INV-04 mutation sentence corrected. X20's description ('succeeds 4.1.0') is compiler text, left as is: X20 checks the amended version, 4.1.1.
- **RV412-3-4** (open (note)): The window boundary, clock steps and T-S47's pause are left as stated; neither reading of the pause stalls or exceeds four reads in 60 s (this review).
- **RV412-3-5** (open (note)): No Percepta CI step; the owner-record check runs in the offline suite (CI job offline-factory / fsharp-tooling), not in --check.
- **RV412-3-6** (open (note)): Commits under the owner's own identity cannot be told apart by the test; both records were checked by hand in this review.

The reviewer's text above is unchanged. After f24b167 only the RV412-3-3 docs fixes and these review files changed; the package was recompiled and re-checked.
