<!-- Investigation note written 2026-10-28 by a read-only investigation agent for the Echelon Engineering Improvement Initiative (Phase One). Local paths refer to that session's clones. Numbers marked as recomputed in ../BASELINE-2026-10-28.md were re-derived independently. -->

# Causa persistence state-machine safety: Rehearsal 4 vs Rehearsal 5

Read-only investigation. Paths: R4 = `/home/user/w/indy-init-rehearsal-004` (HEAD 78a1b15), R5 = `/home/user/w/indy-init-rehearsal-005` (HEAD e086147, `main`; `r5/persist` exists only as merged history on main, e.g. d23060f, 5321c43; no local branch). No `.fsi` files exist in either rehearsal.

## 1. Rehearsal 4

### State machine — `src/causa/Sync.fs`
- States (`State`, 25-43): `Clean head | Pending head | Publishing Attempt | Acknowledged(Attempt, commit) | Confirming(Attempt, commit, readId) | Unknown Attempt | Reconciling(Attempt, readId) | Conflict(localBase, remoteHead) | Refused(head, Refused)`.
- Inputs (45-60): Edited, SaveRequested, Saved, SaveRefused, SaveOutcomeUnknown, ReconcileStarted, HeadRead, ReconcileFailed, ConfirmStarted, Confirmed, ConfirmFailed, Discarded.
- `step` (111-145): Clean/Pending/Refused+Edited→Pending; Pending/Refused+SaveRequested→Publishing (empty file list faults, 116); Publishing+Saved(id match)→Acknowledged; Acknowledged+ConfirmStarted→Confirming; Confirming+Confirmed→Clean only if `classify = Applied`, else Unknown (122-124); ConfirmFailed→Unknown; Publishing+SaveRefused Stale→Conflict, other→Refused; SaveOutcomeUnknown→Unknown; Unknown+ReconcileStarted→Reconciling; Reconciling+HeadRead→Clean/Pending/Conflict by content (`classify` 99-109); ReconcileFailed→Unknown; Conflict+Discarded→Clean; everything else faults (145).
- Enforcement: none structural. `State` is a public DU in a public module; every case constructor is usable anywhere. The "persisted" fact is not in the machine at all: it is `Owned.Persisted: int` (`src/app/App.fs:54`), a plain public record field.

### The defect (application bypass)
Recorded by the T33 reviewer at snapshot 6950158: `evidence/reviews/T33-findings-6950158.json:108-111`, DCT-002-B OPEN:
> "Sync.step faults on illegal inputs, but App bypasses it: Sync is assigned directly in finishWorkspace/WorkTree/reload (App.fs:514,651,953) …"

Concrete bypasses at 6950158 (`git show 6950158:src/app/App.fs`):
- `App.fs:514` (finishWorkspace): `Sync = Some(if dirty then Sync.Pending head else Sync.Clean head)`. Constructs Clean without going through any transition.
- `App.fs:651` (reload): `Sync = None`. Wipes the machine, including Unknown or Publishing.
- `App.fs:953` (WorkTree, empty tree): again constructs `Sync.Clean head` directly.
- `App.fs:1029` and `1050` (ConfirmTree and ReconcileTree): `Owned = s.Owned |> List.map (fun o -> { o with Persisted = o.Events.Length })`. Every investigation is marked persisted, including ones created mid-save that were never sent. That is the blocker T33 R2-01 (`T33-findings-6950158.json:150-152`). `create` was accepted during Publishing/Unknown/Reconciling because only `WorkspaceLoaded` was checked.
- Earlier T30 review (`evidence/reviews/T30-findings-ca52aae.json:12, 34, 45`): the reconcile path "marks every Owned investigation Persisted" (App.fs:871-875), and success was claimed on the GraphQL acknowledgement alone (App.fs:814-838).

Remediation record:
- `docs/rehearsal/REHEARSAL-004-REPORT.md:43`: "DCT-001-A … and DCT-002-B (illegal inputs) came back at the application level after the state machine was fixed (T33 R2-01 to R2-05)".
- `docs/rehearsal/R4-REMEDIATION-PLAN.md:31, 59, 85` (F-R2-09, WP5).
- `evidence/remediation/remediation-ledger-ab08b76.json:314` (DCT-002-B NOT_RUN), `:5066`.
- `evidence/remediation/corrections.json:54, 212`.
- Re-review `evidence/reviews/R5-findings-e7a0a8e.json:191-193` (DCT-002-B PARTIAL: "the core enumeration covers Sync.step only"), plus `:121, :178, :231` (R5-01: unreadable remote kept with `Persisted=0` and saved over another writer).
- Commits: 8e03f40 ("only what a save carried is marked saved"), 153274a, 559d9c2, 4134418.

Status at R4 HEAD: the guards were patched at runtime only. `Sync.Pending/Clean` are still assigned directly (`App.fs:594`, `1094`). `Sync = None` and restore of `BeforeReload` are at `App.fs:742, 941, 977`. `Persisted` is still mutated freely (`App.fs:457, 495, 539, 557, 693`; `markSent` 451, used at 1175 and 1196).

## 2. Rehearsal 5 design

### Documents
- `docs/PERSISTENCE-CONTRACT.md` is generic and not code-specific. It covers §12 concurrency (185-197), §19 offline store (265-277), §20 local-truth states RemoteConfirmed/LocalAppliedPending/Conflict/SyncFailed/RevalidationRequired (279-289), §21 restart recovery (291-303), §22 queue durability (305-311), §23 conflict classes (313-323) and §42 provider failure (555-568).
- The binding rule is causa-data `contract/v1.0.0/CONTRACT.md` §9.4 (`/home/user/causa-data/contract/v1.0.0/CONTRACT.md:924-939`). §9.4.3 requires: "Success is reported only after the provider confirms … **and** a re-read at the new head returns the written bytes."
- `docs/STATE-TRANSITION-TABLES.md` covers domain lifecycles only. It has no persistence states. Its §17 (336-345) says "one domain decision path; do not duplicate transition tables".
- `docs/architecture/BOUNDARIES.md:23, 41-64` define Boundary D. **Line 52: "do not build `Sync.State` values by hand; that bypasses the invariants"** is a convention only, not enforced.
- `docs/FSHARP-TYPE-SHAPES.md` §42 (1109-1130) specifies `type ProviderVersion = private ProviderVersion of string`. The implementation instead uses public `Oid = Oid of string`.

### Two-layer machine
**Core request book**, `src/engine/Sync.fs` (module `IndyInitRootCauseInvestigator.Engine.Sync`):
- `Phase` (88-100): `Disconnected | Connecting | ConnectRefused | Idle(head, pending: Change option) | Writing | Confirming(head, Attempt, commit, read) | Unknown(head, Attempt) | Reconciling | Held(head, Change, Conflict remoteHead | NotWritten(Refused | NeverSent))`.
- `State = { Phase; Issued }` (102).
- `step` (235-323) rejects any answer whose RequestId is not `inFlight` (248-250). It is stage-aware on failure (Preparing→Held NotWritten, Sent→Unknown; 300-303). Confirming→Idle(commit, None) happens only on `ReadBack(_, true)` (306). Reconciliation is classified by ancestry and marker (`classify` 191-199, 315-321).
- Status projection is at 332-357.
- Durable hooks: `restoreUnknown` (374), `restore` (378-390: in-flight→Unknown), `attach` (394-397: Disconnected→`Idle(head, None)`), `encode`/`decode` (443-623).

**Outer machine**, `src/persistence/Sync.fs` (namespace `Causa.Persistence`):
- `Connection` has 8 states (12-24). `Durability` is NotSaved / SavingLocally / SavedLocally / LocalSaveFailed (26-30). `RemoteConflict` (34-37), `ReloadJob` (46-48).
- `Document` (50-64) holds `Confirmed: Verified option`, `ConfirmedHead`, `Local`, `Book: Book.State`, `LastSyncedAt`.
- Single entry point `Sync.update` (882-996). It guards Connect, Disconnect, Save, Reconcile, ReloadRemote and DiscardLocal against unsettled, unknown and held states.
- Projection: `documentStatus` (1001-1020): conflict / queuedOffline / failed / unknown / syncingRemotely / savingLocally / unsaved / savedLocally / remotelySynced / clean.

Mapping of the requested states:

| Requested state | R5 implementation |
|---|---|
| persisted | Book `Idle(_, None)` + `Document.Confirmed = Some` + `LastSyncedAt` → "remotelySynced" |
| pending | `Idle(_, Some change)` |
| publishing | `Writing`, `Confirming` |
| unknown outcome | `Unknown` |
| reconciliation | `Reconciling` |
| conflict | `Held(Conflict)` or `Document.RemoteConflict` |
| failed | `Held(NotWritten Refused)`, or `LocalSaveFailed` |
| reload recovery | `Restore` msg → `parseStored` (340-391) → `Book.decode` + `Book.restore` (363-364); then `finishLoad` auto-reconciles Unknown (558-566). Per-document `ReloadRemote` → `startReload`/`integrate` (451-508) |

### How transitions are enforced
- Enforcement is runtime only. The book is pure and total, and the outer program is the only caller in production code.
- There are no `.fsi` files and no `private` union cases anywhere in `src`. Searching for `= private [A-Z]|internal` finds only `RecordedGitHub.fs:103` (`type internal Auth`) and `AiLoop.fs:9`.
- All the types involved are public: `Book.State`, `Phase`, `Oid`, `RequestId`, `Document`, `Model`, `Verified` (`src/persistence/Store.fs:12-24`). `Credential` has a public primary constructor (`Provider.fs:55`) that bypasses `Credential.create`.

**Can application code construct "persisted" or mutate authoritative state?** Yes, by type. Nothing in the compiler prevents `{ Phase = Book.Idle(Book.Oid h, None); Issued = 0L }`, `{ d with Confirmed = Some d.Local; LastSyncedAt = Some now }`, or `{ s with Model = { m with Documents = … } }` in `src/app`.

In practice, R5 app code does **not** do this today:
- `src/app/PersistencePort.fs` only calls `Sync.update` (191-196) and reads fields (99-115, 211).
- `EnginePort.Persistence.memory` (99-115) fabricates projection-level `A.SyncStatus` values. `A.RemotelySynced` is a public case (`Adapter.fs:159-171`), so any adapter can claim "Saved to GitHub" in the UI.

The R4 failure mode is therefore closed by discipline and placement, not made unrepresentable.

### Every place an authoritative/persisted state is constructed (R5 at e086147)
Book `Idle(_, None)` / `Confirmed = Some`:
1. `src/engine/Sync.fs:257`: `Connecting + Connected → Idle(head, None)`.
2. `:283`: `Idle(_, None) + Reloaded → Idle(observed, None)`.
3. `:277`, `:278`, `:280`: Discard → `Idle(_, None)` (local drop; not a remote claim).
4. `:306`: **Confirming + ReadBack(true) → Idle(commit, None)** (the real confirmation).
5. `:318-319`: **Reconciling + Applied → Idle(ours, None)**. This is by ancestry and marker only; no byte re-read, unlike §9.4.3.
6. `:396`: `attach` Disconnected → `Idle(head, None)` with no transition or event (public hook).
7. `:526-623`: `decode` builds any phase from JSON, including Idle (used on local-store restore).
8. `src/persistence/Sync.fs:197`: `bookAt` = `attach (Oid head) initial`.
9. `:209`: `observe` uses `attach`.
10. `:436-445`: `stepBook` sets `Confirmed = Some d.Local; ConfirmedHead; LastSyncedAt` on any `(Confirming | Reconciling) → Idle(commit, None)` edge. Note it copies `d.Local`, **not** `attempt.Change.Files`. Equality relies on the runtime refusals of `changeLocal` (859-860).
11. `:467-477`: `integrate.fresh`: `Confirmed = Some v` from a validated remote read at `head`.
12. `:489`: `rebased` re-stamps `ConfirmedHead = Some head`.
13. `:498`: fast-forward adoption of a remote successor.
14. `:376-385`: `parseStored` restores `Confirmed`, `ConfirmedHead` and `LastSyncedAt` from browser storage, unverified until the next load.
15. `:979`: DiscardLocal adopts `RemoteConflict.Remote` or the old Confirmed as `Confirmed`.
16. `src/app/PersistencePort.fs:110` maps to `A.RemotelySynced d.LastSyncedAt`. `Adapter.fs:163` is public, so any code can construct `RemotelySynced`.

### Other runtime-only weak points found
- `ReadBack(r, sentBytesFound: bool)` (`engine/Sync.fs:121`): the book trusts a caller-computed bool. It is computed at `persistence/Sync.fs:802-803`.
- `stepOr` (`persistence/Sync.fs:199-202`) silently swallows book faults in `observe` and `offer` (206-252).
- A book that fails to decode falls back to `Book.initial` (`persistence/Sync.fs:365`). This silently downgrades a stored `Unknown` to Disconnected and then attach→Idle. The expected-head guard still prevents a duplicate commit, but "may have saved" is lost.
- Tests depend on hand-built states. The negative controls in `tests/App.Engine.Tests/WriteInvariantTests.fs:299, 388` build `{ state with Phase = … }`. Hiding constructors needs `InternalsVisibleTo` or a test-only module.

## 3. Test coverage (R5)
U = `tests/Persistence.Tests/SyncAppTests.fs`, D = `tests/Persistence.Tests/DcTableTests.fs`, W = `tests/App.Engine.Tests/WriteInvariantTests.fs`, P = `tests/Persistence.Tests/DctProviderTests.fs`. The harness fault injection is `Harness.fs:7-17` (Deliver / NotSent / ApplyThenLose / LoseWithoutApplying / Hold).

| Scenario | Test(s) |
|---|---|
| Duplicate writes | Second Save refused while sending: D:128 `DCT-002-B … every user event … legal source states`. No second POST after lost answer: U:256 (`writesBefore` = after) and D:160 (`Assert.Single(posts fake)`). Idempotent duplicate-append detection (contract §15): **absent** |
| Concurrent writes | Other writer simulated via fake `seed`/`CommitFiles`: U:205 (same investigation → conflict), D:54, D:69, P:65. Live: `tests/browser/live-persistence.spec.js:108` (DCT-LIVE-A). Two real program instances racing: **absent** |
| Timeouts | Book level W:395 (`sentFailuresAreUnknown` over NetworkError/TimedOut/Cancelled, 344-366), W:212 (TimedOut before send). Persistence level has no TimedOut transport (`Provider.fs:86`); timeouts are the kernel's. A program-level timeout firing test: **absent** |
| Unknown outcomes | U:189 (re-read fails→unknown), U:256 (applied), U:275 (not applied→pending), D:160, D:54, D:69; P:94 (untyped GraphQL error→unknown) |
| Credential changes | U:294 (revoked→accessDenied, replacement saves), P:41 (write-denied, renewed credential), U:318 (credential not persisted across reload). Weak assertion: U:305 `fake.Head branch <> fake.Head branch` is always false, so `Assert.Empty` is vacuous |
| Reload during publishing | U:343 `a reload during a send restores it as unknown…`; W:439 (`SYNC-DURABLE restore …`), W:473 (`restoreUnknown`) |
| Remote deletion | U:498 (deleted with a pending local change → conflict "gone"). Deletion of a fully synced, unchanged investigation (`integrate` 499-505 path): **absent** |
| Conflicting changes | U:205; `tests/Persistence.Tests/FixtureTests.fs:114`; `tests/browser/fixture-mode.spec.js:131` |
| Non-conflicting changes | U:236 (unrelated file → reload rebases → saves) |
| Stale acks | D:85 (late ack for replaced write refused, model unchanged); W:212, W:219 (negative control) |
| Out-of-order responses | D:113 and U:390 (answer from an earlier connection generation); D:85 via `Hold`. Out-of-order `LocalStored` acks (handled at `persistence/Sync.fs:983-985`, `PersistencePort.fs:193`): **absent**. Reordered load/blob answers: **absent** (implicit only) |

Missing entirely: any test that application code *cannot* construct a persisted state, which is a compile-time property. The interface-manifest test (`tests/App.Engine.Tests/InterfaceManifestTests.fs`) freezes the public surface but does not forbid public constructors.

## 4. Where a structural persistence contract belongs
- R5 dependencies: `src/engine/App.Engine.fsproj:14` uses `EchelonFoundry.Aegis.Core 1.0.0`; `src/app/Causa.App.fsproj:10-11` uses `Limen.Contract`/`Limen.Guest 0.9.0`. `conditor.json` lists praxis, ordo, percepta, aegis, limen, forma and folio. There is **no Arca**.
- R4 vendored its GitHub write adapter from rehearsal 2 (`src/causa/VENDOR.json`). R5 re-implemented the book and adapter. So persistence is app code copied or rewritten per rehearsal.
- Registry (`/home/user/w/echelon-registry/spec/ecosystem-distribution-readiness.md:45, 78`; `releases/arca/0.5.0.release.json`): **Arca** 0.5.0 is a ".NET/NuGet library family (pure core + GitHub adapter + Limen IndexedDB bridge)". It has `EchelonFoundry.Arca.Core`, `.GitHub` and `.Limen`, with namespace-scoped change tokens, an offline queue, a read cache, and pinned GitHub reads. It is an optional project binding via Conditor's local NuGet feed, and Conditor's README:29 scaffolds it as an F# NuGet library.
- **Fides** (line 46/79) is SSO with a `Fides.Arca` bridge. **Aegis** is the structured-faults library (contract §42 says provider failures map to Aegis faults). **Limen F#** includes `Limen.Store`.
- Tutela (security lifecycle tooling), Tekmerion (research publishing) and Summa.Contracts (billing) are not relevant. **Vitium** is not in the registry (`echelon-engineering-system/.../DETAILED-WEBSITE-AND-SOURCE-INVENTORY-v0.2.md:18`: "require canonical-repository resolution").
- The pre-kickoff rule allows "generic reusable libraries that are independently legitimate frameworks" (`docs/HACKATHON-BOUNDARIES.md:16`). Requirement DEP-CURRENT-001-O2 lists "Arca when used".

Conclusion: the smallest existing home is **`EchelonFoundry.Arca.Core`** (pure F# core). Two pieces would go there:
- a generic, opaque "confirmed write" contract: a publication-state type with private cases and a confirmation token minted only by its provider adapter (`Arca.GitHub`);
- the stage-aware Unknown and reconcile rules.

Causa-specific parts stay in the app: event-file contract, `Verified`, markers.

**Caveat:** Arca's source was not readable from this session (GitHub access to `kemiller2002/arca` was denied), so its current API was not checked. Whether it already models change tokens as opaque is unverified. No new framework is warranted. If Arca turns out to be a poor fit, the fallback is a sealed module inside R5's existing `Causa.Persistence` project.

## 5. F# techniques to make "persisted without authoritative confirmation" unrepresentable

1. **Private union cases on the book state.** `type Phase = private | Idle of … | …` plus public active patterns (`(|Idle|Writing|…|)`) for reading. Only `Sync.step`, `restore` and `decode` in the same module can build states. This kills hand-built `Idle(_, None)` everywhere outside `engine/Sync.fs`. Apply the same to `State = private { … }`.
2. **Signature file `Sync.fsi`.** It exposes `step`, `initial`, `status`, `encode`/`decode`, `restore`, `restoreUnknown` and abstract `type State`. It hides `attach`, or replaces it with a step event. It is cheaper than (1) for records, but the F# signature file must be maintained.
3. **Opaque confirmation token minted only by the provider adapter.** For example `type Confirmed = private { Commit: Oid; Files: FileWrite list }`, constructible only in `GitHub.fs`'s read-back or compare parser. Then `ReadBack of RequestId * Confirmed` replaces `ReadBack of RequestId * bool`, and `Document.Confirmed` holds a token, not a `Verified` the caller chose. This also forces `Confirmed` to be derived from the attempt's files rather than `d.Local` (fixes 10 above).
4. **Private `Oid` / `RequestId` / `Verified` constructors.** Do as FSHARP-TYPE-SHAPES §42 already requires for `ProviderVersion`, so tokens cannot be forged from strings. Make `Credential` a private constructor too.
5. **Phantom types** (`Document<'S>` with `Pending`/`Confirmed` tags) are possible but awkward here, because state changes come from asynchronous answers held in a `Map`. They add little over (1)+(3) and would be invasive. Not recommended.
6. **Projection.** Make `A.RemotelySynced` constructible only from a `Confirmed` token (e.g. `RemotelySynced of ConfirmationView` with a private ctor), so no adapter (e.g. `EnginePort.memory`) can claim "Saved to GitHub".
7. **Tests.** Negative-control steppers (W:299, 388) need `InternalsVisibleTo("App.Engine.Tests")` with `internal` rather than `private` cases, or a test-only factory.

**What remains runtime-only regardless:**
- The provider truly applying the write: the expected-head guard (`GitHub.createCommit` with `expectedHeadOid`).
- Correct byte comparison in the read-back.
- Marker forgery by another writer.
- Request-id matching of answers (`inFlight`).
- Generation checks.
- Refusal of edits while in flight or Unknown (`changeLocal` 859-860).
- Restore from untrusted local storage (decode is a legitimate constructor; it must at minimum map to Unknown, never to confirmed).
- Ordering of `LocalStored` acks.
- The adapter honestly minting tokens. Types limit who can mint; they cannot prove the minter is correct.
