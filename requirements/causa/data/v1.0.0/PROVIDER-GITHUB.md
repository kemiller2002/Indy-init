# Causa v1.0.0 GitHub storage provider

| | |
|---|---|
| Status | Normative for Indy-init. Requirement ids `CAUSA-PROV-01` … `CAUSA-PROV-36`; provider gate rows `AC-PROV-01` … `AC-PROV-24`. |
| Applies to | every Causa build that reads or writes Causa storage v1.0.0 data through GitHub |
| Implements | [CONTRACT.md](CONTRACT.md) §3, §9.2–§9.4, §11, §15; [INVARIANTS.md](INVARIANTS.md) CAUSA-INV-09, -13, -14; [compatibility/RULES.md](compatibility/RULES.md) CD-PROV-01…03; [CLARIFICATIONS.md](CLARIFICATIONS.md) CAUSA-CLAR-03, -12; docs/REQUIREMENTS.md R10.1–R10.8 |
| Closes | independent-review findings F-04, F-05, F-06, F-07, F-17 and F-19 (provider side) |

The key words MUST, MUST NOT, REQUIRED, SHALL, SHALL NOT, SHOULD, SHOULD NOT, RECOMMENDED, MAY and OPTIONAL are used as in
RFC 2119.

---

## 0. Scope, authority and conformance

1. This document specifies a **GitHub persistence adapter** for Causa storage v1.0.0: where it reads and writes, the
   exact GitHub REST requests of one atomic append, how every response maps to a sync state, how a local pending queue
   survives restarts, and how conflicts are resolved by re-decision. An implementer who reads this document, the wire
   contract and [SEMANTICS.md](SEMANTICS.md) MUST be able to build a conforming adapter without reading any existing
   adapter code.
2. **Authority.** CONTRACT.md §9.4 is frozen and wins on any conflict. This document refines §9.4; it adds **no wire
   structure** and changes no persisted byte. Everything it defines that is stored (the local queue record, the
   confirmed copy, the provider token) is **adapter-private** state. It is never written to a repository and is not a
   v1.0.0 file.
3. **Conformance.** An adapter conforms when it satisfies every MUST in §1–§9 and its build passes every `AC-PROV-*` row
   of §10. Those rows are the **provider gate** referenced by [acceptance/ACCEPTANCE.md](acceptance/ACCEPTANCE.md)
   ACC-11. The consumer-protocol runner (`tools/causa-contract/acceptance.py`) does not run them. An application
   build with a GitHub adapter and no live or fake-server evidence for every `AC-PROV-*` row is BLOCKED, not PASS.
4. **Codes.** Reader codes are CONTRACT §11.1. Write-path codes are `CAUSA.VERSION_STALE`, `CAUSA.HISTORY_REWRITTEN`,
   `CAUSA.UNREPRESENTABLE` (CONTRACT §11.1) and `CAUSA.COMMAND_REFUSED`, `CAUSA.PATH_FROZEN`, `CAUSA.WRITE_UNCONFIRMED`
   (CAUSA-CLAR-03). None of them is ever persisted. Sync states (§7) are adapter states, not codes.
5. **Scope limits.** This document covers the GitHub REST API (`https://api.github.com`, API version header
   `2022-11-28`). A non-browser tool MAY publish through the Git smart-HTTP protocol instead (one commit, non-forced
   push, the same guard and confirmation) if it meets every observable requirement here. That backend is not
   specified further.

## 1. Terms

| term | meaning |
|---|---|
| **dataset repository** | `kemiller2002/causa-data`, read-only, at tag `causa-storage-v1.0.0` (tag object `fd2991a392e913b741f0ed2bc4daa4bc4be57f6c`, commit `c947c7e354be80766573a782546c8186336774cc`) |
| **workspace repository** | the configured `owner/repo` and `branch` that receives demonstration writes; MAY be the dataset repository on a non-tag branch |
| **prefix** `P` | `workspaces/demo/<runId>/investigations/<investigationId>/` |
| **head** | the commit SHA that `refs/heads/<branch>` names at the moment of a read |
| **provider token** `T` | the pair (head commit `Tc`, its root tree `Tt`) that the writer read and built on (CONTRACT §9.4.2). Adapter-private |
| **confirmed copy** | the three file byte strings of an investigation as last confirmed remotely, plus the token at which they were read or written. `Cb` is its `events.ndjson` bytes |
| **queue record** | one pending semantic command with its decided events (§7.5) |
| **in-flight lines** `L` | the concatenated decided event lines (each followed by LF) of the records being published in one attempt |
| **publish attempt** | the request sequence of §5.2, for one commit |
| **git blob id** | lowercase hex SHA-1 of `"blob " + <decimal byte length> + NUL + <bytes>`, as Git computes it |

---

## 2. Configuration and workspace location (F-07)

### CAUSA-PROV-01 — Configuration

1. An adapter instance is configured with exactly:

   | key | rule |
   |---|---|
   | `apiBase` | `https://api.github.com`. A different value is allowed only for a test fake server (§10.1). |
   | `workspace.owner` | GitHub account name, `^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$` |
   | `workspace.repo` | `^[A-Za-z0-9._-]{1,100}$`, not `.` or `..` |
   | `workspace.branch` | a branch name that passes `git check-ref-format --branch`; SHOULD match `^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*$` and MUST NOT end in `.lock` or contain `..` |
   | `workspace.runId` | `^[a-z0-9][a-z0-9-]{0,63}$` (CONTRACT §3.2), evaluated with ECMA-262 semantics (`$` only at the very end; CONTRACT §2.13) |
   | `dataset` | fixed to the dataset repository and tag above; not user-configurable |

2. Owner, repository and every `/`-separated segment of the branch MUST be percent-encoded as URL path segments when
   they are placed in a request URL. Owner and repository comparisons are case-insensitive (GitHub semantics). Path
   comparisons inside a tree are case-sensitive (CONTRACT §3.1).
3. The user interface MUST show the workspace repository, branch and runId before the first write (threat model §9).
   Changing any of them MUST NOT move, copy or delete queued or confirmed data. Queued records stay bound to the
   workspace they were decided for (§7.5 `workspace`).
4. Configuration contains no credential. The token is supplied separately (CAUSA-PROV-36).

### CAUSA-PROV-02 — Choosing a runId

1. A **new** runId is chosen when the user starts a new demonstration run or makes a working copy of the frozen
   fixtures (CAUSA-PROV-11). An existing runId MAY be reopened to continue a run.
2. The RECOMMENDED form is `<label>-<yyyymmdd>-<hhmmss>-<hex6>`. Here `label` is a short `[a-z0-9-]` word for the purpose
   or device class (`ipad`, `demo`, `rest`). The date and time are UTC, and `hex6` is six lowercase hex digits from a
   cryptographically random source. Example: `ipad-20261023-142501-7f3a9c`. The result MUST match the CAUSA-PROV-01
   pattern; a generator MUST truncate `label` rather than exceed 64 characters.
3. A runId MUST NOT contain personal data, a GitHub login, an e-mail address or a secret.
4. A new runId MUST be absent at the head the writer builds on: there is no entry `workspaces/demo/<runId>` in that tree.
   If it exists, the adapter MUST generate another runId; it MUST NOT adopt an existing run silently.
5. A run has no directory of its own until its first investigation is created (Git stores no empty trees).

### CAUSA-PROV-03 — Path construction and validation

1. The adapter MUST construct every writable path as `P = "workspaces/demo/" + runId + "/investigations/" +
   investigationId + "/"`, followed by exactly one of `manifest.json`, `events.ndjson`, `snapshot.json`. Here `runId`
   satisfies CAUSA-PROV-01 and `investigationId` is a canonical UUID (CONTRACT §2.8). Paths are lowercase ASCII.
2. Validation happens **before any request**. A runId or investigation id that fails its pattern is
   `CAUSA.PATH_INVALID`. A target outside `workspaces/demo/<runId>/investigations/`, or under `fixtures/v1.0.0/`,
   `contract/v1.0.0/` or `compatibility/v1.0.0/`, is `CAUSA.PATH_FROZEN` (CAUSA-CLAR-03). In both cases no request is
   sent and nothing is queued.
3. Identifiers taken from data (for example a negative fixture's `directoryId`) are values to validate, never paths to
   use (CAUSA-CLAR-06).

### CAUSA-PROV-04 — Repository and branch setup

1. The workspace branch SHOULD be a dedicated data branch that carries no application code. It SHOULD be protected by a
   branch rule or ruleset that forbids force pushes and deletion. This protection is the only defence against the
   rewind window that CAUSA-PROV-16 cannot close.
2. Tags matching `causa-storage-*` in the dataset repository SHOULD be protected against update and deletion.
3. Dataset and workspace repositories MUST store every file byte for byte, and MUST contain a root `.gitattributes`
   with `* -text` or an equivalent (CAUSA-CLAR-12; review F-19). The adapter cannot create this file, because its
   path guard (CAUSA-PROV-15) refuses every path outside `P`. An operator adds it out of band. The adapter SHOULD read
   the root tree at load and warn when the file is absent. Adapter reads and writes are byte-exact through the Git
   Data API either way (CAUSA-PROV-14). The attribute protects people and tools that clone the repository.
4. The frozen tag predates this rule and has no `.gitattributes`. That cannot be changed (CAUSA-INV-13). Clients of the
   frozen dataset MUST read blobs as bytes (CAUSA-PROV-09).

---

## 3. Reading

### CAUSA-PROV-05 — Reading an investigation at one commit

The normative read is through the Git Data API, addressed by SHA, at one commit:

1. `GET /repos/{owner}/{repo}/git/ref/heads/{branch}` → `200`, `object.type = "commit"`, `object.sha = H`. This request
   MUST bypass HTTP caches (CAUSA-PROV-06 §2).
2. `GET /repos/{owner}/{repo}/git/commits/{H}` → `200`, `tree.sha = Ht`.
3. Walk from `Ht` along `workspaces`, `demo`, `<runId>`, `investigations`, `<investigationId>` with non-recursive
   `GET /repos/{owner}/{repo}/git/trees/{sha}`. At each level, look up the next segment by exact, case-sensitive name:
   - entry absent → the investigation directory is **absent** at `H`;
   - entry present but not `type = "tree"` (mode `040000`) → `CAUSA.PATH_INVALID`.

   An implementation MAY instead issue one `GET …/git/trees/{Ht}?recursive=1`. If that response has
   `truncated: true`, it MUST fall back to the walk. It MUST NOT treat a truncated listing as complete.
4. The investigation directory MUST contain exactly the entries `manifest.json`, `events.ndjson` and `snapshot.json`,
   each `type = "blob"` with mode `100644` (or `100755`). Any missing or extra entry, including hidden files,
   subdirectories, symlinks (`120000`) and submodules (`160000`), is `CAUSA.PATH_INVALID` (CONTRACT §3.3).
5. `GET /repos/{owner}/{repo}/git/blobs/{sha}` for each of the three. The response MUST have `encoding = "base64"`. The
   adapter removes the line feeds GitHub inserts into the base64 text, decodes to bytes, and MUST check that the byte
   length equals `size`. It SHOULD check that the git blob id of the bytes equals `sha`. Any other encoding, or a
   mismatch, is a provider failure (CAUSA-PROV-19), never a reader verdict about the file.
6. The three byte strings are then verified per CAUSA-PROV-07.

Objects addressed by SHA (commits, trees, blobs) are immutable. They MAY be cached indefinitely, keyed by
`owner/repo` and SHA.

### CAUSA-PROV-06 — Forbidden read paths and caching

1. The adapter MUST NOT use any of these for a confirmation re-read, unknown resolution, conflict detection or a
   concurrency decision:
   - `raw.githubusercontent.com`, which is served through a CDN and can return stale content for minutes;
   - the Contents API (`GET /repos/{o}/{r}/contents/{path}`), which addresses by branch, inlines content only for
     files up to 1 MB, and can lag the ref after a write;
   - any read addressed by branch name rather than by commit SHA, other than the single ref read of
     CAUSA-PROV-05 step 1.

   The Contents API MUST NOT be used for writes at all (CONTRACT §9.4.1).
2. Ref reads (`GET …/git/ref/heads/{branch}`) carry cache headers that permit reuse. A browser adapter MUST send them
   with `fetch(…, {cache: "no-store"})`, or with `cache: "no-cache"` so that they are revalidated. A non-browser
   adapter MUST NOT serve them from a local cache.
3. A `404` for a SHA that this client has just created or just read as a ref target is treated as replication lag
   (retry, CAUSA-PROV-20), not as absence.

### CAUSA-PROV-07 — Verification on load and the successor rule

1. Every loaded investigation MUST pass all 17 stages of CONTRACT §11.1, with stage 15 per SEMANTICS.md, before any of it
   is shown as remote state or used as a base for a write (CAUSA-CLAR-05 §1). An invalid investigation is not
   partially loaded, repaired or rewritten (CONTRACT §11.2).
2. When a confirmed copy exists, the remote `events.ndjson` MUST start with `Cb` byte for byte (CONTRACT §9.3).
   Otherwise the result is `CAUSA.HISTORY_REWRITTEN`: the investigation's queue records enter `conflicted` with that
   cause (§7.2 T11), and no automatic re-decision runs.
3. When the remote files equal the confirmed copy, nothing changes. When they verify and extend `Cb`, they are a
   successor. The adapter adopts them as the new confirmed copy only when no queue record is pending for that
   investigation. Otherwise it takes the conflict path (CAUSA-PROV-27 §4).

### CAUSA-PROV-08 — Listing runs and investigations

1. Runs are the tree entries of `workspaces/demo/` at `H`. Investigations of a run are the tree entries of
   `workspaces/demo/<runId>/investigations/`. Both are read by the walk of CAUSA-PROV-05.
2. Every entry is reported. An entry whose name fails its pattern (runId or canonical UUID), or that is not a tree, is
   listed as `CAUSA.PATH_INVALID` and is not loaded. A directory that loads but fails verification is listed with its
   first failing code (CONTRACT §11.1). Valid entries are listed normally. Invalid entries never hide valid ones, and
   valid entries never hide invalid ones.
3. Listing MUST NOT repair, rename, delete or rewrite anything. In particular, the adapter MUST NOT "fix" an invalid
   directory by writing to it. Writes to an investigation whose remote copy is invalid are refused with that code.
4. Listing MAY verify lazily, opening an investigation runs CAUSA-PROV-07. The list MUST label entries it has not
   verified yet as such.

### CAUSA-PROV-09 — Reading the frozen dataset

1. The frozen dataset is read **read-only** from the dataset repository at the release tag. The repository is public
   (decision U-02), so anonymous reads, with no `Authorization` header, are allowed and RECOMMENDED. The adapter MAY
   send the workspace token when the dataset repository is the workspace repository, or to get a higher rate limit.
2. Tag verification: `GET /repos/kemiller2002/causa-data/git/ref/tags/causa-storage-v1.0.0` MUST return `object.type =
   "tag"` and `object.sha = fd2991a392e913b741f0ed2bc4daa4bc4be57f6c`, and
   `GET …/git/tags/fd2991a392e913b741f0ed2bc4daa4bc4be57f6c` MUST name `object.sha =
   c947c7e354be80766573a782546c8186336774cc`. Otherwise the adapter refuses the dataset (`TAG_MOVED`, CAUSA-INV-13).
   It MAY skip the tag reads and address the pinned commit directly. The commit pin is what guarantees the bytes.
3. Files are read by walking the tree of the pinned commit and fetching blobs (CAUSA-PROV-05 steps 3–5). The SHA-256 of
   `fixtures/v1.0.0/manifest.json`'s bytes MUST equal the dataset identity
   `f650a2e509a420872b46177b21f57ef6ebd7625ae4c69dd92198d3ca472c2b86` ([compatibility/release-pin.json](compatibility/release-pin.json)).
   Each investigation MUST then match its root-manifest digests (CAUSA-INV-15).
4. Unauthenticated requests are limited to 60 per hour per IP address. A browser adapter SHOULD fetch the
   `fixtures/v1.0.0` subtree with one recursive tree request (falling back to the walk only if it is truncated). It
   SHOULD cache every blob by SHA, and SHOULD NOT re-download the dataset on each start.
5. The adapter never writes to the dataset tag, and never writes under `fixtures/v1.0.0/`, `contract/v1.0.0/` or
   `compatibility/v1.0.0/` on any branch (CAUSA-PROV-03, -15).

---

## 4. Creating investigations and working copies (F-07)

### CAUSA-PROV-10 — Creating a new investigation

1. The new investigation id is a fresh canonical UUID. Version 4, from a cryptographically random source, is
   RECOMMENDED. Its first command is `StartInvestigation` (SEMANTICS §6 C1), decided against the empty state with
   **expected version 0**.
2. The investigation directory `P` MUST be absent at the head the writer builds on, both when the command is decided
   (if the adapter is online) and when it is published (CAUSA-PROV-12). If it exists, the command is refused with
   `CAUSA.VERSION_STALE` and nothing is written. The queue record, if one was created offline, enters `refused`
   (§7.2 T15). This holds even if the existing directory is invalid.
3. Publication follows §5 with no base files: the tree adds the three new blobs under `P` on top of `Tt`.
4. A `StartInvestigation` record whose `commandId` already appears in the remote directory is handled by
   CAUSA-PROV-24 or CAUSA-PROV-28 §2 (a lost confirmation). It is not a `VERSION_STALE` refusal.

### CAUSA-PROV-11 — Working copies of the frozen fixtures

1. A working copy copies the three files of one or more frozen investigations **byte for byte** from the pinned dataset
   commit to `workspaces/demo/<newRunId>/investigations/<sameInvestigationId>/` in the workspace repository. The runId
   is new (CAUSA-PROV-02). The investigation id is unchanged, because the copied manifest names it and the directory
   MUST equal it (CONTRACT §3.2).
2. The copied bytes are not re-encoded, re-canonicalized, re-sequenced or re-decided. Before publishing, the adapter MUST
   check that the SHA-256 of each copied `events.ndjson` equals the root manifest's `eventsSha256`. It MUST check that
   the SHA-256 of each copied `manifest.json` and `snapshot.json`, without the trailing LF, equals `manifestSha256` and
   `snapshotSha256`.
3. Blob creation: when the workspace repository is the dataset repository, the tree MAY reference the frozen blob SHAs
   directly. Otherwise the bytes are uploaded per CAUSA-PROV-14. Either way, each blob SHA in the new tree MUST equal
   the git blob id of the frozen bytes.
4. A working copy is a provider operation, not a semantic command. It is published as **one commit** by the sequence of
   §5.2, with these differences:
   - every target directory MUST be absent at the base;
   - the guard of CAUSA-PROV-15 is applied with the set of prefixes being copied;
   - the confirmation re-read compares every copied file.
5. The frozen tree is never written. After the copy, the tree of `fixtures/v1.0.0/` (and of `contract/v1.0.0/` and
   `compatibility/v1.0.0/`, where present) at the new head MUST have the same tree SHA as at the base. This follows from
   the guard and is tested by AC-PROV-15.
6. Each copied investigation becomes a confirmed copy with an empty queue. Later appends go to the copy, never to the
   frozen original.

---

## 5. Atomic append (F-06)

### CAUSA-PROV-12 — Preconditions

Before the first request of a publish attempt:

1. Every record in the attempt has `status = sending` (CAUSA-PROV-22). Its decided lines were produced by `decide`
   (SEMANTICS §8) and verified locally: CONTRACT §9.2 steps (1)–(3) over the confirmed copy plus every earlier record,
   with the manifest and snapshot rebuilt (CAUSA-CLAR-05). The adapter holds the complete new byte strings of
   `manifest.json`, `events.ndjson` and `snapshot.json`.
2. The adapter has read the head `T` (CAUSA-PROV-05), loaded the investigation at `Tc`, and found it **equal** to the
   confirmed copy (blob SHAs equal, or both absent). If it differs, the attempt does not start: the records take the
   conflict path with cause `CAUSA.VERSION_STALE` (CAUSA-PROV-27 §4).
3. Check precedence follows CAUSA-CLAR-03: path, then version, then representability, then `decide`, then verification
   of the new file set, then confirmation.

### CAUSA-PROV-13 — Request sequence of one atomic append

All requests go to `apiBase` with the headers of CAUSA-PROV-31. `{o}/{r}` is the workspace repository and `{b}` the
encoded branch. Steps run strictly in order, and each step's response is validated before the next one is sent.

| step | request | expected response | validate |
|---|---|---|---|
| 1 | `GET /repos/{o}/{r}/git/ref/heads/{b}` (uncached) | `200` | `object.sha = Tc` (else go to CAUSA-PROV-27) |
| 2 | `GET /repos/{o}/{r}/git/commits/{Tc}` | `200` | `tree.sha = Tt` |
| 3 | `POST /repos/{o}/{r}/git/blobs` ×3, body `{"content": <base64 of the exact bytes>, "encoding": "base64"}` | `201` | `sha` = locally computed git blob id of those bytes (CAUSA-PROV-14) |
| 4 | `POST /repos/{o}/{r}/git/trees`, body `{"base_tree": Tt, "tree": [{"path": P+"events.ndjson", "mode": "100644", "type": "blob", "sha": …}, {… "manifest.json" …}, {… "snapshot.json" …}]}` | `201` | `sha = Nt` |
| 5 | path-prefix guard over `Tt` → `Nt` (CAUSA-PROV-15) | — | only `P` differs |
| 6 | `POST /repos/{o}/{r}/git/commits`, body `{"message": m, "tree": Nt, "parents": [Tc]}` | `201` | `sha = C`, `tree.sha = Nt`, `parents` = exactly `[Tc]` |
| 7 | durably record `publish = {commit: C, tree: Nt, parent: Tc, patchSentAt: <now>}` in every record of the attempt (CAUSA-PROV-25) | — | the write completed before step 9 |
| 8 | `GET /repos/{o}/{r}/git/ref/heads/{b}` (uncached) | `200` | `object.sha = Tc` (CAUSA-PROV-16; else go to CAUSA-PROV-27, without a PATCH) |
| 9 | `PATCH /repos/{o}/{r}/git/refs/heads/{b}`, body `{"sha": C, "force": false}` | `200` | `object.sha = C` |
| 10 | confirmation re-read by SHA (CAUSA-PROV-17) | — | written bytes returned |

1. `force` MUST be the JSON literal `false`. The adapter MUST NOT send `force: true`, delete a ref, create a ref other
   than the configured branch, or use `PUT …/contents/…`, at any time.
2. Steps 1–6 create only unreachable objects. A failure anywhere before step 9 leaves the branch unchanged, so the
   affected records return to `pending` or enter `failed` (CAUSA-PROV-19), never `unknown`.
3. Steps 3 and 4 MAY reuse objects created by an earlier attempt for the same bytes, because objects are
   content-addressed. On a rebase (CAUSA-PROV-27 §3), steps 1–10 are repeated with the new head. The blobs are
   unchanged.
4. The tree entries are the three files only. The adapter never sends a tree entry with `sha: null` (deletion), never
   sends `content` instead of `sha`, and never sends a path outside `P`.

### CAUSA-PROV-14 — Byte-exact blob writes

1. Blob content MUST be the exact file bytes: UTF-8 without BOM, LF only, canonical (CONTRACT §2). The RECOMMENDED
   request encoding is `base64` of the bytes. `encoding: "utf-8"` MAY be used, with `content` set to the JSON string
   whose UTF-8 encoding is exactly the file bytes. The JSON serializer of the request body MUST then escape correctly
   and MUST NOT normalize Unicode or line endings.
2. In either case, the `sha` returned by `POST …/git/blobs` MUST equal the git blob id computed locally over the intended
   bytes. A mismatch aborts the attempt before step 4, and the records enter `failed` with cause
   `byte-mismatch`. Nothing becomes visible.
3. The adapter MUST NOT apply any end-of-line conversion, trimming, re-serialization or normalization between
   verification (CAUSA-PROV-12 §1) and upload.

### CAUSA-PROV-15 — Path-prefix guard (tree comparison)

Before the PATCH (CONTRACT §9.4.4, CAUSA-INV-13 §4), the adapter MUST prove that `Nt` differs from `Tt` only under `P`.
The normative method walks both trees along the segments of `P` with non-recursive tree reads:

1. Let the levels be: root, `workspaces`, `demo`, `<runId>`, `investigations`. At each level, read the base tree and
   the new tree. The base side may be absent below the first missing segment, which is treated as an empty tree.
2. At each level, the entry lists, **excluding** the single entry named by the next segment, MUST be identical: same
   names, modes, types and SHAs, and no entry added or removed. Because tree and blob SHAs are content hashes, equal
   sibling SHAs prove equal subtrees without descending into them.
3. The entry for the next segment MUST be a tree (`040000`) in the new tree.
4. At the investigation directory `<investigationId>`, the new tree MUST contain exactly the three entries, each with
   mode `100644`, type `blob`, and the SHA from step 3 of CAUSA-PROV-13.
5. For a working copy (CAUSA-PROV-11), the `investigations` level excludes the set of copied ids, every one of which MUST
   be absent at the base.
6. Any deviation refuses the publish with `CAUSA.PATH_FROZEN`. No commit is created, or if one was, it is never
   referenced by a PATCH. The records enter `failed` (§7.2 T4).
7. An implementation MAY use another method, such as a recursive listing or `GET …/compare/{Tc}...{C}`. It MUST then
   fail closed whenever the result could be incomplete: `truncated: true`, a paginated or capped file list (the compare
   API lists at most 300 files), or a missing file list. The tree walk above never needs that fallback, because it
   never reads a truncated listing.

### CAUSA-PROV-16 — Fast-forward is not compare-and-swap

1. `PATCH …/git/refs` with `force: false` only refuses an update that is not a fast-forward. It succeeds whenever `C`
   descends from the current head. If the branch has been **rewound** to an ancestor of `Tc`, for example by a force
   push, a PATCH to `C` (parent `Tc`) still succeeds and silently re-publishes `Tc`'s history.
2. Therefore the adapter MUST re-read the ref without cache immediately before the PATCH (step 8). It sends the PATCH only
   if the head still equals `Tc`. If it does not, the adapter sends no PATCH and follows CAUSA-PROV-27 with the newly
   observed head.
3. After a `200`, the adapter MUST check that `object.sha = C` in the response, and that the re-read commit `C` has
   exactly the parent `Tc` (CAUSA-PROV-17). Any other result means the outcome is `unknown`.
4. A rewind that lands between step 8 and step 9 cannot be detected through the API. Branch protection that forbids
   force pushes (CAUSA-PROV-04 §1) closes that window. Without it, the residual risk MUST be stated in the deployment's
   documentation. The successor rule (CAUSA-PROV-07 §2) still detects any rewind that removes confirmed events from an
   investigation.

### CAUSA-PROV-17 — Confirmation re-read

1. After PATCH `200`, the adapter reads **commit `C` by SHA**: `GET …/git/commits/{C}` (tree `= Nt`, parents `= [Tc]`).
   Then it walks to `P` and fetches the three blobs (CAUSA-PROV-05 steps 3–5). It MUST NOT re-read through the branch,
   the Contents API or `raw.githubusercontent.com` (CAUSA-PROV-06). Commit `C` is "the new head" of CONTRACT §9.4.3
   even if the branch has since moved further.
2. If all three byte strings equal the written bytes, every record in the attempt enters `confirmed` (§7.2 T6). The
   confirmed copy becomes the written files, with token `(C, Nt)`.
3. If the re-read fails (network, `5xx`, `404` lag, or `401` after a token expiry), the records enter `unknown`
   (§7.2 T5). They are resolved by CAUSA-PROV-24.
4. If the re-read returns different bytes, the records enter `unknown` with cause `CAUSA.WRITE_UNCONFIRMED`. That cause
   MUST be shown to the user. Resolution (CAUSA-PROV-24) then classifies the remote state. A persistent mismatch ends in
   `conflicted`, never in `confirmed`.

### CAUSA-PROV-18 — Commit granularity and commit messages

1. One commit MUST contain the complete events append, manifest and snapshot of every command it publishes (CONTRACT
   §9.4.1). One command per commit is RECOMMENDED, because each command then has its own auditable commit (R10.3). A
   commit MAY carry several consecutive queue records of **one** investigation. They then share one attempt and one
   outcome, and every intermediate state is still verified locally.
2. Commit messages are audit metadata only (CONTRACT §9.4.5). No reader, resolution step or conflict decision may depend
   on them. A message MUST NOT contain a token, credential, personal data, or domain text (titles, observations,
   evidence content, reasons, URLs). The RECOMMENDED form is
   `causa: <runId>/<investigationId> v<from> -> v<to> (<n> events)`, plus `working copy of causa-demo-v1` for a working
   copy.
3. The commit's author and committer are whatever GitHub assigns to the token's user. That is provider metadata in the
   Git object, outside every contract file. The adapter MAY set `author` and `committer` to a non-personal identity,
   for example `{"name": "causa-adapter", "email": "causa-adapter@example.invalid"}`. It MUST NOT copy them into any
   investigation file, and MUST NOT derive `actorId` from them (CAUSA-PROV-33).

---

## 6. Responses, retries and backoff (F-06)

### CAUSA-PROV-19 — Status-code mapping

Phases: **R** covers the reads before the PATCH (steps 1–2, 8, and loading). **O** covers object creation (steps 3, 4,
6) and the guard (step 5). **U** is the PATCH (step 9). **C** is the confirmation re-read (step 10) and unknown
resolution. The table gives the resulting record state (§7) and the action taken.

| response | R / O (branch unchanged) | U (PATCH) | C |
|---|---|---|---|
| `200`/`201` with the expected, validated body | continue | `200` + `object.sha = C` → phase C | compare bytes (CAUSA-PROV-17) |
| `200`/`201` with an unreadable or unexpected body | retry (CAUSA-PROV-20); then `pending` | `unknown` | `unknown` (retry) |
| `304` to a conditional read | use the validated cached representation | — | allowed only for SHA-addressed objects |
| `401` | `failed` (authentication); halt every record of the workspace until re-authentication | `failed` (not applied) | stay `unknown`; resolve after re-authentication, or anonymously if the repository is public |
| `403` with `x-ratelimit-remaining: 0`, a `retry-after` header, or a message containing "rate limit" (primary or secondary) | `pending`; wait (CAUSA-PROV-20) | `pending` (not applied); wait | stay `unknown`; wait |
| `403`, any other (permission, SSO, blocked) | `failed` (authorization) | `failed` (not applied) | stay `unknown` |
| `404` on ref, repository or commit of the configured workspace | `failed` (not found or not permitted; GitHub hides private repositories as `404`) | `failed` (not applied) | stay `unknown` (retry: replication lag) |
| `404` on an object this client just created or just read | retry (lag); then `pending` | — | stay `unknown` (retry) |
| `409` or `422` whose `message` contains "not a fast forward" (case-insensitive; observed live as `422 Update is not a fast forward`) | — (step 8 detects a moved head without a PATCH) | non-fast-forward → CAUSA-PROV-27 | — |
| `409`, any other (for example "Git Repository is empty") | `failed` | `failed` (not applied) | stay `unknown` |
| `422` "Object does not exist" | — | `pending` (not applied; bounded retry, then `failed`) | — |
| `422`, any other (for example "Reference does not exist", "Repository rule violations found", a malformed request) | `failed` | `failed` (not applied) | stay `unknown` |
| `429` | `pending`; wait | `pending` (not applied); wait | stay `unknown`; wait |
| `500`, `502`, `503`, `504` | retry; then `pending` | **`unknown`** (the server may have applied it) | stay `unknown` (retry) |
| timeout, connection reset, DNS or TLS failure, `fetch` `TypeError` (including a CORS failure), page suspended mid-request | retry; then `pending` | **`unknown`** | stay `unknown` (retry) |
| `301`/`302`/`307` (repository renamed or moved) | `failed` (configuration); MUST NOT follow with the token to another origin | `failed` | stay `unknown` |
| guard refusal (CAUSA-PROV-15) | `failed` with `CAUSA.PATH_FROZEN` | — | — |
| blob SHA mismatch (CAUSA-PROV-14) | `failed` with cause `byte-mismatch` | — | — |
| offline before any request | `pending`; no request is sent | — | — |

1. "Not applied" is certain only for a complete `4xx` response to the PATCH. A `5xx` response, a missing response or an
   unreadable response to the PATCH MUST be treated as `unknown`.
2. A `failed` record stays in the queue with its decided events. It returns to `pending` only through §7.2 T18.
3. Every mapped failure produces a structured provider fault for the user (`authentication`, `authorization`,
   `rate-limited`, `unavailable`, `conflict`, `integrity`, `configuration`; PERSISTENCE-CONTRACT §42). The fault never
   contains the token or request headers (CAUSA-PROV-31).

### CAUSA-PROV-20 — Retry and backoff bounds

1. Each request has a finite timeout. 30 s is RECOMMENDED, and the timeout MUST NOT exceed 60 s.
2. **Idempotent requests** (every GET, and the content-addressed `POST …/git/blobs`, `…/git/trees` and `…/git/commits`)
   MAY be retried on a `5xx`, a timeout, a network error or a lag `404`: at most 3 retries (4 attempts), after delays
   of 1 s, 2 s and 4 s, each multiplied by a random factor in [0.8, 1.2]. After the last retry, the record goes to the
   state given by CAUSA-PROV-19.
3. **The PATCH is never re-sent automatically after an ambiguous outcome.** The record enters `unknown`, and a new PATCH
   is sent only after CAUSA-PROV-24 has shown that the write did not land (T10). After a complete `4xx` response, a new
   PATCH is sent only by the procedures of CAUSA-PROV-27 or §7.2 T18.
4. **Rate limits.** Wait for the `retry-after` seconds when the header is present. Otherwise, with
   `x-ratelimit-remaining: 0`, wait until `x-ratelimit-reset`. Otherwise wait at least 60 s. A wait longer than 15
   minutes leaves the record `pending`, with the next attempt scheduled. A forbidden (`403` other) request MUST NOT be
   retried automatically (threat model §44).
5. **Sync cycles.** While online, `pending` records are retried after 5 s, doubling up to a maximum of 5 minutes. The
   backoff resets after a confirmation, on a browser `online` event, and on an explicit user "retry now". `unknown`
   resolution uses the same schedule and has no attempt limit: `unknown` changes only through a successful resolution
   read.
6. **Rebase rounds.** At most 5 automatic rebase or re-decision rounds (CAUSA-PROV-27, -28) per sync cycle. When the
   bound is reached, the records return to `pending`, and the user is told the branch is busy.
7. Mutating requests MUST be serialized per workspace branch: one publish attempt in flight per client. GitHub's
   secondary limits on content creation apply. One append costs 5 content-creating requests plus the PATCH.

---

## 7. Sync-state machine and local queue (F-05)

### CAUSA-PROV-21 — Closed state set

Every queue record is in exactly one of these states. No other state exists, and a UI MUST NOT invent a softer label
for any of them.

| state | meaning | remote effect |
|---|---|---|
| `pending` | decided, verified locally, durably queued; not being sent | none |
| `sending` | a publish attempt that includes this record is in progress | none yet |
| `unknown` | a PATCH was sent (or answered `200`) but the outcome is not established | may or may not be visible |
| `confirmed` | PATCH `200` and a re-read by commit SHA returned the written bytes (or CAUSA-PROV-24 proved it) | visible |
| `conflicted` | the remote investigation changed under the record; the outcome is being, or must be, re-decided | none |
| `refused` | re-decision (or the create check) refused the command: `CAUSA.COMMAND_REFUSED` with the domain code, or `CAUSA.VERSION_STALE` for a create | none |
| `failed` | the provider definitively did not apply the write, and retrying cannot help without user or operator action | none |

`confirmed` is terminal. A confirmed record leaves the active queue and is kept in the local log (CAUSA-PROV-25).
Discarding a record (T17) is not a state: it removes the record from the queue and logs the removal.

### CAUSA-PROV-22 — Transitions

| # | from | trigger | to |
|---|---|---|---|
| T1 | — | command decided at the local optimistic state, verified (CAUSA-PROV-12 §1), and the record durably stored (CAUSA-PROV-25) | `pending` |
| T2 | `pending` | a sync cycle starts a publish attempt including this record; every earlier record of the investigation is `confirmed` or in the same attempt | `sending` |
| T3 | `sending` | a retryable failure before the PATCH (CAUSA-PROV-19, phases R/O), or a definitive `4xx` rate-limit or "Object does not exist" response to the PATCH | `pending` |
| T4 | `sending` | a definitive non-retryable refusal before or at the PATCH (`401`, other `403`, `404`, other `409`/`422`, redirect, `CAUSA.PATH_FROZEN`, byte mismatch, local verification failure) | `failed` |
| T5 | `sending` | PATCH sent without a definitive response, or PATCH `200` with a failed or mismatching confirmation re-read | `unknown` |
| T6 | `sending` | PATCH `200` and the re-read equals the written bytes | `confirmed` |
| T7 | `sending` | head moved (step 8) or PATCH non-fast-forward, and the investigation is **unchanged** at the new head | `sending` (rebase, CAUSA-PROV-27 §3; bounded) |
| T8 | `sending` or `pending` | head moved or non-fast-forward, and the investigation **changed** at the new head; or, at the start of a cycle, the remote investigation differs from the record's base | `conflicted`, then re-decision (CAUSA-PROV-28) |
| T9 | `unknown` | resolution: the remote extends `Cb + L` | `confirmed` |
| T10 | `unknown` | resolution: the remote equals `Cb` (or is absent, for a create) | `pending` |
| T11 | `unknown` or `pending` | resolution or load: any other remote, including `CAUSA.HISTORY_REWRITTEN` and an invalid remote | `conflicted` |
| T12 | `unknown` | resolution read failed | `unknown` (retry, CAUSA-PROV-20) |
| T13 | `conflicted` | re-decision gives an **equal** outcome (CAUSA-PROV-28 §4) | `pending` (automatic) |
| T14 | `conflicted` | re-decision gives a **changed** outcome | `conflicted` (awaiting the user; `redecided` stored) |
| T15 | `conflicted`, or `pending`/`sending` of a create | re-decision refused, or the create target exists | `refused` |
| T16 | `conflicted` (awaiting) | the user accepts the re-decided outcome (CAUSA-PROV-29) | `pending` |
| T17 | `conflicted`, `refused`, `failed` | the user discards the record | removed (logged) |
| T18 | `failed` | the cause is fixed (re-authentication, configuration) and the user or app retries | `pending` |
| T19 | `sending` | application restart: no `publish.patchSentAt` | `pending` |
| T20 | `sending` | application restart: `publish.patchSentAt` is set | `unknown` |

1. Ordering: records of one investigation are published strictly in `seq` order. While a record is `unknown`,
   `conflicted`, `refused` or `failed`, every later record of the same investigation stays `pending` and is **blocked**:
   it is not sent. The UI shows it as blocked by the earlier record.
2. Every transition is appended to the record's `statusLog`, with a cause, before it is shown. The `conflicted` entry
   of an automatic T8 → T13 sequence MUST stay visible in the log (CONTRACT §9.4.2 "reports `conflict`").
3. The **investigation sync status** shown to the user is derived from its records: `confirmed` when the queue is empty;
   otherwise the first applicable of `refused`, `conflicted`, `failed`, `unknown`, `sending`, `pending`. The UI MUST
   show which changes exist only on this device (PERSISTENCE-CONTRACT §20).

### CAUSA-PROV-23 — Truthful reporting

1. Success is reported **only** in `confirmed` (CONTRACT §9.4.3). `pending`, `sending`, `unknown`, `conflicted`,
   `refused` and `failed` MUST stay visually and programmatically distinct. In particular, `unknown` MUST NOT be shown as
   "saved", and a network failure MUST NOT be reported as success.
2. `failed` is used only when the write was definitively not applied (CAUSA-PROV-19 §1). Every ambiguous case is
   `unknown`.
3. A report or export that includes unconfirmed local state MUST say so (PERSISTENCE-CONTRACT §41).

### CAUSA-PROV-24 — Resolving `unknown`

1. Read the head `H` without cache. Load the investigation at `H` by SHA (CAUSA-PROV-05) and verify it
   (CAUSA-PROV-07 §1). Let `R` be the remote `events.ndjson` (empty if the directory is absent), `Cb` the confirmed
   copy's events (empty for a create), and `L` the in-flight lines of the attempt.
2. The adapter MAY first read commit `publish.commit` directly. If `H = publish.commit`, step 3(a) applies once the bytes
   are compared.
3. Classify:
   - (a) `R = Cb + L`, and the remote manifest and snapshot equal the bytes written: the write landed. The records enter
     `confirmed` (T9), and the confirmed copy becomes the files at `H`.
   - (b) `R` starts with `Cb + L` and is longer: the write landed and others appended afterwards. The records enter
     `confirmed` (T9). The confirmed copy becomes the files of commit `publish.commit` when it is readable, otherwise
     `Cb + L` with the manifest and snapshot that were written. Later `pending` records then see a changed remote and
     take T8.
   - (c) `R = Cb` (including both absent): the write did not land. The records return to `pending` (T10). A retry
     builds a new commit on `H`. This is safe even if the lost PATCH is applied later: the retry's PATCH is then not a
     fast-forward, and CAUSA-PROV-28 §2 finds the records already landed.
   - (d) anything else: the records enter `conflicted` (T11). If `R` does not start with `Cb`, the cause is
     `CAUSA.HISTORY_REWRITTEN` and there is no automatic re-decision. If the remote fails verification, the cause is
     its code. If `R` contains a `correlationId` of the attempt in a different form, the cause is
     `CAUSA.WRITE_UNCONFIRMED`. Otherwise others appended and ours did not land, so re-decision runs (CAUSA-PROV-28).
4. Resolution is idempotent. It never writes, and it never resends a PATCH. Only (c) re-enables publishing.
5. Resolution runs first in every sync cycle and after every restart, before any `pending` record is sent (CD-PROV-03).

### CAUSA-PROV-25 — Durable local queue: what, where, shape

1. **What is stored.** For each workspace and investigation, the adapter durably stores:
   - the confirmed copy: the three byte strings and the token `{commit, tree}` at which they were confirmed or read;
   - the ordered queue records;
   - an append-only local log of status transitions and of confirmed or discarded records.

   Commands have no wire form (SEMANTICS §1.1). The queue record therefore stores both the **decided events** and the
   **command inputs** needed to re-decide. Re-decision MUST use the stored inputs. It MUST NOT use
   `commandOf(G)` (SEMANTICS §15.3) as a substitute, because that reconstruction is lossy: `opt(·)` turns blank text into
   `null`, the prediction `timing` is forced to `retrospective`, and the title defaults to `original`.
2. **Where.** A browser adapter uses IndexedDB, in a database for this adapter (for example `causa-github-v1`), with
   object stores for confirmed copies, queue records and the log. The record and the local optimistic projection are
   written in **one** `readwrite` transaction. A command counts as accepted locally (T1) only after that transaction's
   `complete` event. A non-browser adapter writes each record to a temporary file, fsyncs it, renames it into place and
   fsyncs the directory (compare CAUSA-CLAR-04).
3. **Shape** of one queue record (a JSON object; adapter-private, never committed to any repository):

   | field | type | rule |
   |---|---|---|
   | `recordVersion` | integer | `1` |
   | `workspace` | object | `{owner, repo, branch, runId}`; the owner and repository in lowercase |
   | `investigationId` | uuid | |
   | `seq` | integer ≥ 1 | local order within the investigation's queue; never reused |
   | `commandId` | uuid | the command id, equal to the `correlationId` of every decided event; fresh per command (CONTRACT §5.4) |
   | `requestedAt` | timestamp | millisecond UTC (CONTRACT §2.9, CAUSA-CLAR-09); the decision time, equal to `occurredAt` |
   | `actor` | object | the actor object of CONTRACT §5.3 |
   | `command` | object | `{name, inputs}`. `name` is the engine command name (SEMANTICS §6, with the transition variant, e.g. `ResolveUnknown`, `ApproveAction`). `inputs` holds the inputs under their wire field names, after the command-boundary normalizations (CAUSA-CLAR-01 de-duplication, CAUSA-CLAR-09 timestamps) and **before** any decision-time normalization |
   | `expectedVersion` | integer ≥ 0 | the aggregate version of the local state the command was decided against (0 for a create) |
   | `base` | object | `{commit, tree, aggregateVersion, eventsSha256}`: the confirmed copy the decision built on (`commit`/`tree` `null` before the first remote read) |
   | `decided` | array of strings | the decided event lines, each one canonical envelope text without its LF, in order |
   | `status` | string | one of the seven states of CAUSA-PROV-21 |
   | `statusLog` | array | `{status, at, cause, detail}` entries. `at` is the local clock (informative). `cause` is a short code (`http-422-non-ff`, `timeout-after-patch`, `CAUSA.VERSION_STALE`, …) |
   | `publish` | object or null | `{commit, tree, parent, patchSentAt, batch}` for the attempt in flight or last attempted. `batch` lists the `commandId`s in the commit |
   | `redecided` | array of strings or null | the re-decided lines awaiting user confirmation (T14) |
   | `redecidedBase` | object or null | `{commit, aggregateVersion}`: the remote head and version the `redecided` lines were decided against |
   | `refusal` | object or null | `{code, domainCode, errors: [{code, rule, message}]}` |
   | `lastError` | object or null | `{phase, httpStatus, kind, detail}`. `detail` is redacted (CAUSA-PROV-31) |

4. The queue record, confirmed copy and log MUST NOT contain the token. They MAY contain provider metadata, because they
   are not contract files.
5. Removal: a record leaves the queue only on `confirmed`, on an explicit user discard, or when an accepted re-decision
   replaces its `decided` lines (PERSISTENCE-CONTRACT §22). The user MUST be able to export queued records before
   discarding them.

### CAUSA-PROV-26 — Restart recovery

On start, before any remote request:

1. Load the confirmed copies and queues, and rebuild each optimistic projection as confirmed copy plus the decided lines
   of every non-refused record, in `seq` order, verified (CONTRACT §11.1). A record whose lines no longer verify locally
   enters `conflicted` with that code.
2. Apply T19 and T20.
3. Show every record's state as stored. Nothing is shown as confirmed unless it was confirmed before the restart.
4. When the network and the token are available, run the sync cycle: resolve `unknown` records (CAUSA-PROV-24), load
   remote heads, apply CAUSA-PROV-07, then publish `pending` records in order.
5. Unsynced work MUST survive restart, reload, tab close and device sleep (R10.7). A browser adapter MUST call
   `navigator.storage.persist()` and show whether persistence was granted (CAUSA-PROV-35 §4).

---

## 8. Conflict handling (F-04)

### CAUSA-PROV-27 — Detection, and rebase when the investigation is unchanged

1. A conflict is detected when the head re-read before the PATCH differs from `Tc` (CAUSA-PROV-16), or when the PATCH
   returns non-fast-forward (CAUSA-PROV-19). Both lead to the same procedure. In the second case, the PATCH did not
   apply.
2. Read the new head `H` without cache, and its tree. The investigation is **unchanged** iff its directory entry
   `<investigationId>` under `workspaces/demo/<runId>/investigations/` has the same tree SHA at `Tt` and at `Ht`, or is
   absent at both. Equivalently, the three blob SHAs are equal and there are no other entries. Blob SHAs, not file
   timestamps or the commit list, decide.
3. **Unchanged** (CONTRACT §9.4.2(a), T7): the decided events, manifest and snapshot are still valid, because the base
   bytes are identical. Repeat CAUSA-PROV-13 with `T := (H, Ht)` and the same blobs. The guard is evaluated again
   against `Ht`. A rewind to an ancestor counts as a moved head here. Rebasing onto it respects the rewind of unrelated
   paths, and the successor rule still protects this investigation.
4. **Changed** (CONTRACT §9.4.2(b), T8): every record of the attempt, and every later record of the investigation,
   enters `conflicted` with cause `CAUSA.VERSION_STALE` (the append was stale, CONTRACT §9.2). Nothing is written. The
   adapter loads and verifies the remote investigation at `H` (CAUSA-PROV-07). If it is invalid or rewritten, the
   records stay `conflicted` with that code and wait for the user. Otherwise re-decision runs (CAUSA-PROV-28).

### CAUSA-PROV-28 — Re-decision

Input: the verified remote state `S` at `H`, with confirmed copy `Cb' = R`, and the `conflicted` records in `seq` order.
For each record in order:

1. Let `S` be the state replayed from the remote history plus the records already re-decided in this round.
2. **Already landed.** If the remote history contains events whose `correlationId` equals `commandId`, then:
   - if those events form one group whose lines equal `decided` byte for byte, the record landed (a lost
     confirmation) and enters `confirmed`;
   - otherwise the record stays `conflicted` with cause `CAUSA.WRITE_UNCONFIRMED`, there is no automatic action, and
     processing stops.
3. **Decide.** Call `decide(S, {investigationId, actor, at: requestedAt, command})` (SEMANTICS §8), with `command` built
   from the stored `command.name` and `command.inputs`, and with expected version `S.version`. Then:
   - **refused**: the record enters `refused` (T15) with `CAUSA.COMMAND_REFUSED` and the domain code of the first inner
     error (CAUSA-CLAR-03). Processing stops; later records stay `pending` and blocked.
   - **emits** bodies: build envelopes with `aggregateVersion = S.version + i` and `eventId` re-derived (CONTRACT §5.2).
     `correlationId = commandId`, `occurredAt = requestedAt` and the actor are unchanged.
4. **Compare.** The outcome is **equal** iff the two lists have the same length and, at every position, the canonical
   JSON (RFC 8785) of the original envelope with `aggregateVersion` and `eventId` removed equals that of the re-decided
   envelope with the same two keys removed. This is the event equality of SEMANTICS §15.6: the type and every payload
   field, including decided fields such as `LifecycleChanged.from` and `HypothesisTransitioned.from`/`to`. The implicit
   `at` of a hypothesis change is equal by construction. Its implicit version is the excluded `aggregateVersion`.
   - **equal**: replace `decided` with the re-decided lines, set `base` and `expectedVersion` to `H` and `S.version`,
     verify the combined history (CONTRACT §9.2 step 3), and enter `pending` (T13) **automatically**. Continue with the
     next record.
   - **changed**: store the re-decided lines in `redecided`, with `redecidedBase`, and keep `conflicted` (T14). Processing
     stops, and later records stay `pending` and blocked.
5. Re-decision never merges text, never edits `decided` lines by hand, and never changes `commandId`, `requestedAt`,
   the actor or the inputs (CD-PROV-02).

### CAUSA-PROV-29 — User confirmation of a changed outcome

1. A changed outcome MUST NOT be persisted without an explicit confirming action by a **human** user. No timer, default,
   policy or agent actor (CONTRACT §5.3, D-004) may confirm it.
2. The UI MUST show the original decided events, the re-decided events, and the remote change that caused the
   difference. It offers exactly these choices:
   - **accept**: `decided := redecided` and enter `pending` (T16);
   - **discard**: remove the record (T17), logged and exportable;
   - **revise**: discard, then issue a new command with a new `commandId` and `requestedAt`.
3. An acceptance applies to the specific `redecidedBase`. If the remote has moved again by the time the record is
   published, re-decision runs again (T8). If the new outcome differs from the one the user accepted, the user is asked
   again.
4. A `refused` record offers **discard** or **revise** only. It is never persisted.
5. A `conflicted` record with cause `CAUSA.HISTORY_REWRITTEN`, an invalid remote or `CAUSA.WRITE_UNCONFIRMED` offers
   discard, export, or continuing in a new working copy under a new runId. The adapter MUST NOT write to the rewritten
   or invalid investigation.

### CAUSA-PROV-30 — Prohibitions

The adapter MUST NOT, under any condition:

1. text-merge or line-merge `events.ndjson`, manifests or snapshots;
2. send `force: true`, delete or recreate a ref, or force-push;
3. overwrite, truncate, reorder or delete a remote event (CONTRACT §5.5);
4. write outside `P` (or the copy prefixes of CAUSA-PROV-11);
5. report `confirmed` without the confirmation read of CAUSA-PROV-17 or CAUSA-PROV-24;
6. drop a queue record without the user's explicit action, except on `confirmed`.

---

## 9. Security and privacy

### CAUSA-PROV-31 — Token transport

1. The token is sent **only** in the `Authorization: Bearer <token>` header, and only to the `apiBase` origin. Each
   authenticated request also carries `Accept: application/vnd.github+json` and `X-GitHub-Api-Version: 2022-11-28`.
2. The token MUST NOT appear in:
   - a URL or query string, a request body, or a commit message;
   - a file in any repository;
   - an event, manifest or snapshot;
   - the local queue, confirmed copy or log;
   - telemetry, console or server logs, error messages, faults, exports, or AI prompts (CONTRACT §9.4.5,
     CAUSA-INV-09 §4, R9.9).
3. Requests to any other origin (`raw.githubusercontent.com`, `uploads.github.com`, a redirect target, a URL taken from
   a response body) MUST NOT carry the token. Browser requests use `credentials: "omit"`, `redirect: "manual"` (a
   redirect is `failed`, configuration) and `referrerPolicy: "no-referrer"`.
4. Any text that leaves the adapter (an error detail, a log line, a fault) MUST be passed through redaction that replaces
   the token, and its `Bearer` form, with `[REDACTED]`. Redaction is a defence in depth. It does not replace rule 2.

### CAUSA-PROV-32 — Least privilege

1. The write credential MUST be limited to the single workspace repository, with the repository permission
   **Contents: read and write** (plus the mandatory **Metadata: read**) and nothing else. Acceptable credentials are a
   fine-grained personal access token or a GitHub App user or installation token. A classic token with the `repo`
   scope SHOULD NOT be used. If one is used, it is a documented temporary deviation (ARCHITECTURE §Authentication).
2. A short expiry is RECOMMENDED. For a demonstration token, the expiry SHOULD be at most 7 days.
3. Frozen-dataset reads need no credential (CAUSA-PROV-09).
4. When the workspace repository is the dataset repository, the credential can technically write frozen paths. The path
   guard (CAUSA-PROV-15) and tag and branch protection (CAUSA-PROV-04) are then mandatory controls, not options. A
   separate workspace repository is RECOMMENDED.

### CAUSA-PROV-33 — actorId policy (F-17)

1. `actorId` MUST be a stable, pseudonymous, non-secret identifier (CONTRACT §5.3). It MUST NOT be, contain, or be
   derived by a reversible or enumerable function from a GitHub login, numeric GitHub user id, display name, e-mail
   address, device identifier or token.
2. **Recommended derivation:** generate `actorId` once per person as `<role>-<hex12>`. `role` is a lowercase `[a-z]`
   word (`investigator`, `reviewer`), following the frozen fixtures' `investigator-01` style. `hex12` is 12 lowercase
   hex digits from a cryptographically random source. Example: `investigator-3f9c0a1b2d4e`. Store it in local
   settings. Let the person copy it to another device, so that the same person keeps the same `actorId`.
3. System actors use a fixed non-personal id (`system`). Agent actors use a configured id (for example `assistant-01`),
   with `provider` and `model` (CONTRACT §5.3).
4. The adapter never fills `actorId` from the authenticated GitHub user. GitHub's commit author is provider metadata
   (CAUSA-PROV-18 §3).

### CAUSA-PROV-34 — Provider metadata boundary

Commit, tree and blob SHAs, branch names, repository names, ETags, rate-limit data and request ids live only in the
adapter's local state (CAUSA-PROV-25). They never appear in an event, manifest, snapshot or any other contract file
(CONTRACT §1.4, §9.1; CAUSA-INV-09). Integrity of the contract files is their own digests, not Git SHAs (CONTRACT §10).

### CAUSA-PROV-35 — Browser (iPad Safari) considerations

1. **CORS.** `api.github.com` answers CORS preflights for authenticated `GET`, `POST` and `PATCH` with an
   `Authorization` header, and exposes the rate-limit headers to scripts. No proxy is needed for the REST API.
   GitHub's OAuth token endpoints on `github.com` do not serve CORS responses. A browser-only application therefore uses
   either a user-supplied fine-grained token (a documented temporary deviation) or a minimal token-exchange backend
   (threat model §6).
2. **Content Security Policy.** `connect-src` SHOULD list only the application origin and `https://api.github.com`.
3. **Caching.** Ref reads follow CAUSA-PROV-06 §2. SHA-addressed objects MAY be cached.
4. **Storage eviction.** Safari can evict script-writable storage (IndexedDB, `localStorage`) of a site that has not been
   used for a period, and evicts more aggressively without persistent-storage permission. The adapter MUST request
   `navigator.storage.persist()`, MUST show whether the device holds unsynced records, and MUST offer export of the
   queue (CAUSA-PROV-25 §5). An application installed on the home screen is RECOMMENDED for demonstrations.
5. **Suspension.** iPad Safari suspends background tabs, and can do so in the middle of a request. The durable `publish`
   marker written before the PATCH (CAUSA-PROV-13 step 7) makes such a suspension resolve through `unknown`
   (T5, T20), never through success.
6. A browser cannot set `User-Agent`. The adapter MUST NOT try to.

### CAUSA-PROV-36 — Token storage

1. By default the token SHOULD be held in memory only, and re-entered or re-obtained per session.
2. If the user explicitly chooses "remember on this device", the token MAY be stored in a separate store from the queue
   and confirmed copies, for example its own IndexedDB object store. The UI then shows that a token is remembered and
   offers a "forget token" control. The first release MUST NOT claim the token is stored securely (threat model §21).
3. Signing out MUST delete the stored token and stop remote access. It MUST NOT delete unsynced queue records without a
   separate, explicit confirmation (threat model §22).
4. The token MUST NOT be stored in a URL, a cookie, `localStorage`, the queue, the confirmed copy, the log, an export
   or a service-worker cache.

---

## 10. Provider gate (acceptance rows)

These rows are the provider gate for every application build that contains a GitHub adapter
([acceptance/ACCEPTANCE.md](acceptance/ACCEPTANCE.md) ACC-11). They are **not** run by the consumer-protocol runner.
The build's own acceptance suite runs them, against a fake GitHub server (§10.1) or against live GitHub where the
stimulus can be produced live. A row passes only with recorded evidence: the request log, the resulting states and the
remote tree SHAs.

### 10.1 Fake GitHub server (minimum behaviour)

A conforming fake MUST implement:

- a real object graph: content-addressed blobs (git blob id), trees with `base_tree` and nested paths, and commits;
- `GET`/`PATCH` of `refs/heads/*` with fast-forward semantics, answering a non-fast-forward with `422`
  `{"message": "Update is not a fast forward"}`;
- `GET` of refs, commits, trees (with `truncated` when the listing exceeds a configurable limit) and blobs (base64);
- per-request fault injection: a status and body; a delay longer than the client timeout; dropping the response
  **after** applying the request; and moving or rewinding the branch between two requests;
- a Contents API and a branch-addressed read that can be configured to return stale data, to prove they are not used;
- a log of every request: method, URL, headers and body.

The fake MUST NOT be used to weaken a row. Where a row was produced live in EX-CAUSA-STORAGE-2026-001, that evidence is
informative only. Each application build produces its own.

### 10.2 Rows

| id | stimulus | expected state and outcome |
|---|---|---|
| AC-PROV-01 | Create a new investigation (StartInvestigation) and publish | Exactly one new commit; its parent is the old head; its tree differs from the parent only by adding `P` with exactly the 3 files; the record is `confirmed` only after the re-read by commit SHA; request order matches CAUSA-PROV-13 |
| AC-PROV-02 | Append one command to a confirmed investigation | One commit changing exactly the 3 files under `P`; the old `events.ndjson` bytes are a prefix of the new; manifest and snapshot verify (all 17 stages); `confirmed` |
| AC-PROV-03 | Another writer appends to the **same** investigation between the base read and the PATCH; the pending command re-decides to an equal outcome | The PATCH is refused non-fast-forward (or step 8 sees the moved head and no PATCH is sent); the record passes through `conflicted` (logged), then automatically reaches `pending` → `confirmed` at the new versions with re-derived `eventId`s; no remote event is altered |
| AC-PROV-04 | As AC-PROV-03, but the other writer's change makes the re-decided events differ (for example a different `from` state) | The record is `conflicted` with `redecided` stored; nothing is written until a human accepts; on accept, `confirmed` with the re-decided events; on discard, removed and logged; never written silently |
| AC-PROV-05 | As AC-PROV-03, but the re-decision is refused (for example the investigation was closed remotely) | The record is `refused` with `CAUSA.COMMAND_REFUSED` and domain code `INVESTIGATION.NOT_OPEN`; later records are `pending` and blocked; nothing is written |
| AC-PROV-06 | Another writer commits to a **different** investigation (or another path) between the base read and the PATCH | Rebase: the second attempt builds on the new head with the same blobs; `confirmed`; 2 attempts; the other writer's change is preserved |
| AC-PROV-07 | The fake applies the PATCH and then drops the response (timeout after PATCH) | `unknown` immediately; resolution reads by SHA, finds `R = Cb + L`, and reaches `confirmed`; no second commit is created; the UI never showed success while `unknown` |
| AC-PROV-08 | The PATCH times out and the fake did **not** apply it | `unknown` → resolution finds `R = Cb` → `pending` → retried → `confirmed`; exactly one commit lands |
| AC-PROV-09 | Lost confirmation across a restart: the process or page is killed after the step-7 marker and after the fake applied the PATCH | After restart the record is `unknown` (T20), resolves to `confirmed`, and is not re-sent |
| AC-PROV-10 | Failure injected before the PATCH (after blobs, tree or commit are created) | The branch head is unchanged; the record is `pending`; created objects are unreachable; a retry confirms |
| AC-PROV-11 | The tree the adapter is asked to publish changes a path outside `P` (for example `fixtures/v1.0.0/manifest.json`), or the fake returns a tree that does | The guard refuses with `CAUSA.PATH_FROZEN`; no PATCH is sent; the record is `failed`; the head is unchanged |
| AC-PROV-12 | An invalid runId (`Run-1`, `../x`, 65 characters) or an investigation id that is not canonical | `CAUSA.PATH_INVALID` with zero requests sent |
| AC-PROV-13 | Run AC-PROV-01…11 with a known token value; scan every request URL and body, every commit message, every blob, every local store (queue, confirmed copy, log), every log line and fault text, for the token, its base64 and its URL-encoded forms | Zero occurrences outside the `Authorization` header of requests to `apiBase` |
| AC-PROV-14 | Create an investigation whose directory already exists at the head (an id collision forced by the test) | `CAUSA.VERSION_STALE`; record `refused` (or no record when decided online); nothing written; the existing directory is byte-identical |
| AC-PROV-15 | Make a working copy of all 5 frozen investigations into a new runId | One commit; every copied file's digest equals the root manifest; the `fixtures/v1.0.0`, `contract/v1.0.0` and `compatibility/v1.0.0` tree SHAs are identical at base and new head; the dataset tag still resolves to `c947c7e…` |
| AC-PROV-16 | Queue 2 commands offline; restart the app (new process, or page reload) | Both records are restored with their states and inputs; the optimistic projection is rebuilt; when online, both are published in `seq` order and `confirmed` |
| AC-PROV-17 | Responses injected at the PATCH: `401`; `403` with `x-ratelimit-remaining: 0`; `429` with `retry-after`; `422` "Reference does not exist"; `409` "Git Repository is empty"; `502` | Respectively: `failed` (nothing written; queue halted until re-authentication); `pending` (waits until reset); `pending` (waits `retry-after`); `failed`; `failed`; `unknown` → resolved. A `422` other than non-fast-forward is never treated as a conflict |
| AC-PROV-18 | Rewind the branch to an ancestor of `Tc` between step 6 and step 8 | Step 8 detects the head ≠ `Tc`; no PATCH to the rewound head is sent from the stale base; the adapter reloads; if the investigation lost confirmed events, `conflicted` with `CAUSA.HISTORY_REWRITTEN` and no write |
| AC-PROV-19 | The fake returns `truncated: true` for recursive listings | The adapter walks subtrees; loading, the guard and the "unchanged" decision are still correct |
| AC-PROV-20 | Append events whose text contains non-ASCII, U+2028, `\u0000`-escaped controls and 4-byte UTF-8; use both `base64` and `utf-8` blob encodings | The returned blob SHAs equal the local git blob ids; the re-read bytes are identical; a fake that alters one byte causes `failed` (`byte-mismatch`) before any PATCH |
| AC-PROV-21 | The fake serves stale data through the Contents API, `raw` and branch-addressed reads after a PATCH | Confirmation and resolution use SHA-addressed reads only and are correct; no request to the Contents API or `raw.githubusercontent.com` appears in the log |
| AC-PROV-22 | A run contains a valid investigation, a directory `Not-A-Uuid`, a directory with a 4th file, and a directory with a bad `eventsSha256` | The listing shows the valid one; `CAUSA.PATH_INVALID`, `CAUSA.PATH_INVALID` and `CAUSA.EVENTS_HASH_MISMATCH` for the others; zero write requests |
| AC-PROV-23 | Load the frozen dataset anonymously | The tag object and commit match the pins; the dataset identity `f650a2e5…` matches; all 5 investigations reproduce their golden digests; zero write requests; a moved tag (fake) gives `TAG_MOVED` and no data is used |
| AC-PROV-24 | Decide commands as a human user | Every persisted `actorId` matches the configured pseudonymous id; none equals or contains the GitHub login, numeric id or e-mail of the token user |

---

## 11. Known non-conforming implementations (informative)

The following code was useful evidence of GitHub behaviour. It does **not** conform to this document, and it MUST NOT be
used as the specification. Each item names the requirement it misses.

1. **`indy-init-rehearsal-001`, `src/engine/Sync.fs`.**
   - It writes one file, `investigations/<id>/events.json`, with a Contents API `PUT` (`/contents/…` with the file's
     blob `sha`). The layout is wrong, there is no manifest or snapshot, and a single-file Contents write is not the
     atomic commit of CONTRACT §9.4.1 (CAUSA-PROV-03, -13).
   - It reads through the Contents API at `?ref=<branch>`, which has the 1 MB inline limit and is branch-addressed
     (CAUSA-PROV-05, -06).
   - Its concurrency token is the file blob SHA, not the head commit (CONTRACT §9.4.2).
   - It maps every `409`/`422` to a remote conflict and every `401`/`403` to an authentication failure, so a rate-limit
     `403` is misclassified (CAUSA-PROV-19).
   - Its `SyncStatus` has `LocalOnly` and no `refused` or `failed` (CAUSA-PROV-21).
   - Its `Reconcile.plan` compares event sequences instead of re-deciding (CAUSA-PROV-28).
2. **`kemiller2002/causa-data`, `tools/src/Causa.GitHub/`** (the adapter that EX-CAUSA-STORAGE-2026-001 verified live).
   It does use the conforming Git Data API sequence and re-reads by commit SHA. It differs from this document in these
   ways:
   - All `409`/`422` responses to the PATCH become non-fast-forward, so "Reference does not exist" becomes a conflict.
     A rate-limit `403` becomes `AuthFailed` (CAUSA-PROV-19).
   - The guard uses the compare API, which fails closed at 300 files, instead of the tree walk. A truncated recursive
     listing is an error rather than a walk (CAUSA-PROV-05, -15).
   - There is no head check immediately before the PATCH (CAUSA-PROV-16).
   - There is no `unknown` state. A lost response and a failed re-read after a PATCH are reported as `Pending`. That is
     truthful, but it conflates two states, and nothing durable marks "PATCH sent" (CAUSA-PROV-21, -24, -25 step 7).
   - The local queue (`pending.ndjson`) stores decided events only, and re-decision uses `History.commandOf`, which is
     lossy (CAUSA-PROV-25 §1).
   - `Sync.rebase` replaces pending events with re-decided ones without comparing outcomes, so a changed outcome could
     be persisted without user confirmation (CAUSA-PROV-28 §4, -29).
3. Its Git smart-HTTP backend (`GitBackend.fs`) was used because the session proxy denied Git Data API writes. It is a
   tool backend outside this document's REST scope (§0.5).

---

## 12. Traceability and source decisions (informative)

| finding | requirement ids |
|---|---|
| F-04 conflict handling | CAUSA-PROV-27, -28, -29, -30 |
| F-05 sync-state machine | CAUSA-PROV-21, -22, -23, -24, -25, -26 |
| F-06 request/response | CAUSA-PROV-05, -06, -12 … -20 |
| F-07 workspace location | CAUSA-PROV-01 … -04, -08 … -11 |
| F-17 actorId | CAUSA-PROV-33 |
| F-19 byte-exact storage | CAUSA-PROV-04 §3–4, -14 |
| tokens, privilege, browser | CAUSA-PROV-31, -32, -34, -35, -36 |

Choices made between sources:

1. **Automatic re-append after a conflict.** CONTRACT §9.4.2(b) says the writer "reports `conflict` and keeps its pending
   commands". F-04 asks for automatic append when re-decision gives the same outcome. Both are satisfied:
   - the record always enters `conflicted`, which is logged and visible;
   - re-decision then runs, as CAUSA-INV-14 §5 requires;
   - only an equal outcome returns automatically to `pending`.

   CONTRACT wins wherever this reading could be doubted.
2. **`.gitattributes`.** The review asked for it as a recommendation. CAUSA-CLAR-12 makes it a MUST for repositories,
   and this document follows CAUSA-CLAR-12. The adapter cannot write the file (path guard), and the frozen tag cannot
   gain it.
3. **Non-UUID entries in a listing.** CAUSA-CLAR-04 tells local-filesystem readers to ignore `.next-<n>`/`.prev-<n>`
   siblings. CONTRACT §3.2 rejects other names. A GitHub workspace has no such siblings, so every non-conforming entry
   is reported as `CAUSA.PATH_INVALID` (CONTRACT wins).
4. **`409`.** The existing adapters treat every `409` as a conflict. Only a response whose message says "not a fast
   forward" is a conflict here. The live evidence shows GitHub answers a non-fast-forward with `422` (GITHUB-PERSISTENCE
   reject-stale-write).
5. **"Re-read at the new head".** CONTRACT §9.4.3 is read as a re-read of commit `C`, the head the update produced. The
   moving branch is not re-read, because branch-addressed reads can lag.
6. **Unknown resolution.** The review's three-way rule (`= Cb + L` → confirmed; `= Cb` → pending; else conflicted) is
   kept, with one refinement: a remote that **extends** `Cb + L` is also confirmed. This avoids a false conflict when
   another writer appends right after a lost confirmation.
7. **Several commands per commit.** CONTRACT §9.4.1 requires each command's three files to become visible together. One
   commit MAY carry several consecutive commands of one investigation, but one command per commit is RECOMMENDED for
   auditability.
