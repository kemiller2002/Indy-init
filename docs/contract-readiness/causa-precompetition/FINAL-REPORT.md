# Final report: Causa contract readiness before the competition

**Verdict: BLOCKED.** The verdict comes from `CONTRACT-READINESS.json` and is not a waiver or an estimate.

What is complete:

- **Historical data:** every pinned history is compatible and lossless under the successor candidate.
- **Validator:** the contract package is versioned, reproducible from a clean checkout, and checked by an F# validator whose negative controls all fail as designed.

What blocks the freeze:

- **Undefined content:** 14 of 45 UI bindings and 7 required commands have no defined storage or command form.
- **Open decisions:** 9 blocking owner decisions are open. One of them, ODR-V415-02, is the existing approval of v4.1.5 itself.

Real-provider durability is **UNVERIFIED**, because this run had no GitHub credential.

Labels used in this report:

- **observed:** measured by the validator, or by a command that was run, with the evidence file named.
- **proposed:** requires an owner decision.
- **estimate:** a judgement, not a measurement.

## 1. Package index

| # | Deliverable | Kind |
|---|---|---|
| 1 | [SOURCE-INVENTORY.md](SOURCE-INVENTORY.md) | authored: authoritative vs experimental sources |
| 2 | [HISTORICAL-DATA-INVENTORY.json](HISTORICAL-DATA-INVENTORY.json) | generated |
| 3 | [HISTORICAL-COMPATIBILITY.md](HISTORICAL-COMPATIBILITY.md) | authored from generated results |
| 4 | [DOMAIN-CONTRACT.json](DOMAIN-CONTRACT.json) | authored, validated; includes the proposed causa-storage 1.1.0 extension |
| 5 | [FIELD-REGISTRY.json](FIELD-REGISTRY.json) | generated from causa-storage v1.0.0 schemas + FIELD-MAPPING.md + the proposal |
| 6 | [COMMAND-EVENT-REGISTRY.json](COMMAND-EVENT-REGISTRY.json) | authored, validated against the parsed v4.1.5 F# |
| 7 | [STATE-TRANSITIONS.json](STATE-TRANSITIONS.json) | authored, replayed against all data; indexes the v4.1.5 persistence machines |
| 8 | [UI-FIELD-MAP.json](UI-FIELD-MAP.json) | authored, validated against the catalog and SCREEN-MANIFEST |
| 9 | [PERSISTENCE-CONTRACT.md](PERSISTENCE-CONTRACT.md) | authored, checked against compiled SM-SAVE pairs |
| 10 | [VERIFICATION-MATRIX.json](VERIFICATION-MATRIX.json) | generated |
| 11 | [CONTRACT-READINESS.json](CONTRACT-READINESS.json) | generated (machine-readable verdict) |
| 12 | [OWNER-DECISIONS.proposed.json](OWNER-DECISIONS.proposed.json) | authored requests; none recorded |
| 13 | [COMPETITION-CONSUMPTION.md](COMPETITION-CONSUMPTION.md) | authored |
| 14 | this report | authored |
| — | [PACKAGE-MANIFEST.json](PACKAGE-MANIFEST.json) | generated: dependency and schema-version manifest, per-file sha256, package checksum |
| — | [evidence/clean-checkout-9e42aba.txt](evidence/clean-checkout-9e42aba.txt) | observed clean-checkout run |
| — | `tools/contract-readiness/` | the F# validator (net10.0, no packages, `TreatWarningsAsErrors`) |

No equivalent authoritative artifact existed for the wire field registry, the variant-to-case binding, the UI write/read-back map or the readiness gate. Where an authority exists, it is indexed rather than restated, and the validator checks the copy against it:

- the v4.1.5 persistence machines;
- the catalog's labels and validation;
- SCREEN-MANIFEST's screen states.

## 2. Answers to the completion criteria

| # | Question | Answer | Basis |
|---|---|---|---|
| 1 | How many historical events were examined? | 1,315 in the three historical cases; 1,363 in the active catalog `demonstration-v1`; **1,583** distinct events in all 16 distinct histories (5,672 counting byte-identical copies in 28 directories) | observed, CR-HIST-01 |
| 2 | How many are represented losslessly? | Under the successor candidate v4.1.5: **1,583 / 1,583**. Each one decodes strictly, maps to one variant and a case that carries every field, and re-encodes byte for byte. Under frozen v4.1.4: **0 / 1,315**: 487 have no case, 63 are lossy, and all 1,315 lose envelope provenance. This reproduces PR #141 exactly. | observed, CR-REP-01, CR-REP-02 |
| 3 | How many fields are defined? | **296**: 277 causa-storage v1.0.0 wire fields over 62 object types, with 72 conditional rules and 33 enumerations, plus 19 proposed causa-storage 1.1.0 fields | observed, FIELD-REGISTRY.json |
| 4 | How many commands and events are defined? | **52 command entries**: 41 defined in v4.1.5, which cover all 29 `CoreCommand` cases plus the app and persistence inputs; 7 proposed; 2 UI-only; 1 deferred; 1 refused. **56 event variants**: 49 wire variants that partition all 31 wire event types; 6 proposed; 1 with no wire form. They cover all 50 v4.1.5 `CoreEvent` cases. | observed, CR-CER-02..04 |
| 5 | How many UI bindings are covered? | 45 bindings for the 44 catalog requirements: **30 covered** (27 with a domain write or read path, 3 UI-only); **14 blocked**; 1 not applicable under v1.0.0 (INV-003, no domain packs) | observed, CR-UI-01..04 |
| 6 | How many verification obligations exist? | **121**: 50 executed here (32 contract checks and 18 negative controls) and 71 implementation obligations for the competition (42 pending, 28 blocked, 1 not applicable) | observed, VERIFICATION-MATRIX.json |
| 7 | How many tests passed? | **31 of 32 checks PASS**, 0 fail, and 1 is UNVERIFIED (CR-LIVE-01, real-provider durability). 18 of 18 negative controls were detected. | observed, CONTRACT-READINESS.json |
| 8 | Did the negative controls fail correctly? | **Yes, all 18.** Each one makes exactly its target checks fail. The controls remove a registry field, a variant, a `CoreEvent` case, a command, a binding, the envelope `Provenance`, a `SaveState` case, a CoreCommand entry and an input path. They also: duplicate a field id; corrupt the canonical bytes; inject an unknown field and an unknown event type; claim durability without a read-back; narrow a transition; mark a blocked binding as covered; tamper with the committed registry; and plant a token. | observed |
| 9 | Are there unresolved owner decisions? | **Yes, 9 blocking:** ODR-V415-02 (existing: approve v4.1.5) and OD-CR-01..08. Each request states the conflicting evidence, alternatives, recommendation, compatibility impact and the consequence of deferral. | OWNER-DECISIONS.proposed.json |
| 10 | Is the contract package ready to freeze? | **No.** The verdict is BLOCKED; criteria 1, 3 and 5 of the gate are unmet. | observed |
| 11 | Can a competition agent consume it in a clean checkout? | **Yes**, at commit `9e42aba`: fresh clones, SDK 10.0.402, restore, build and `check` all exit 0; `check --require-pass` exits 2 as it should while BLOCKED. The package checksum reproduces and was recomputed independently with `sha256sum`. | observed, evidence/clean-checkout-9e42aba.txt |
| 12 | What remains to be implemented during the competition? | See §5 | proposed |

## 3. What was verified, and how

- **Strict, registry-driven decode.** The decoder reads the registry, not the raw schema, so removing a registry field makes replay fail (NC-01). Encoding starts from typed values, not from the parsed text. This is why the round trip proves semantic representation rather than echo.
- **Representability.** It is computed from the actual F# text of both packages: union cases, case arguments and the `EventEnvelope` record. No hand-labelled compatibility table is trusted. The labels in `proposals/r10-historical-data-compatibility/src/EventCompatibility.fs` are reproduced, not imported.
- **Domain transitions.** The 41 transitions of `docs/STATE-TRANSITION-TABLES.md`, transcribed onto the wire vocabulary, replay all 273 stored transitions with no violation. Each stored `from` equals the replayed state.
- **Envelope identity.** The UUIDv5 `eventId` derivation is recomputed for every event.
- **Persistence machines.** The states and inputs of SM-SAVE, SM-CONFLICT and SM-LOAD equal the parsed F# unions. The durability states map every `SaveState`, and only `Saved` (read-back verified) may be shown as saved.
- **UI bindings.** Each binding is checked field by field against the catalog: label, input type, required flag, screen and validation. Its write and read-back ids must exist in the registry. A binding that needs a proposed field, or a command with no required input path, cannot claim coverage (NC-11).

## 4. Findings

Ordered by consequence.

1. **The v4.1.5 candidate resolves the historical gap completely.** E1 makes every stored event representable, and W1 makes workspaces addressable (observed). It is still unapproved: ODR-V415-02 is open, and FREEZE step 2 is PR #143.
2. **The approved UI requirements ask for content causa-storage v1.0.0 cannot store:**
   - the primary question (INV-023);
   - the observation source (INV-025);
   - the evidence label and URL (INV-027, INV-029);
   - the relationship kinds `consistent-with` and `inconclusive` (INV-031);
   - reassessing or withdrawing a relationship (INV-030 update);
   - editing that keeps identity (INV-034, INV-035).

   ODR-R10-26 A, which is recorded, commissions causa-storage 1.1.0 for these, but no 1.1.0 exists in causa-data at `e8259df`. This package proposes its exact content: 6 additive events and 2 enum values, with no v1.0.0 change (CR-DOM-02). The successor commands are OD-CR-01.
3. **v4.1.5 has `ConclusionRecorded` but no `RecordConclusion` command**, although INV-032 and INV-033 require recording a conclusion (observed, CR-CER-04 and the registry).
4. **Three required wire inputs have no requirement-side input path:**
   - evidence `category` is a required enum, and no catalog field supplies it (OD-CR-02);
   - evidence `source` is required on the wire but optional in INV-028 (OD-CR-03);
   - conclusion `strength` is a required enum, and no catalog field supplies it (OD-CR-04).

   An agent would otherwise have to invent them. CR-CER-06 forces each gap to be tied to an open decision.
5. **Mappings the owner must confirm, not agents:**
   - INV-011 "explanation" = `mechanism` (OD-CR-05);
   - what an evidence edit may change while keeping its id (OD-CR-06);
   - the source of `actorId` (OD-CR-07).
6. **`ExtensionAttached` / `AttachExtension` has no wire form** (CONTRACT §13.5). The registry marks it, and the command boundary must refuse it.
7. **No Conditor, Praxis, Dokimos or Ordo interface consumes this package today.** The missing adapters are listed in COMPETITION-CONSUMPTION.md §3, and adopting the package as a governed asset is OD-CR-08.
8. **The data itself is clean.** No integrity, canonical, transition, provenance or digest defect was found. Every copy of a case is byte-identical, and all 376 historical provenance records correspond to their evidence.

## 5. What remains for the competition

- **Implement, starting at T0, against the defined contracts:**
  - the v4.1.5 domain kernel's `Decide`/`Replay` for the 41 defined commands and the 49 wire variants;
  - the storage codec, which must pass the same properties this validator checks: canonical round trip, UUIDv5 ids, correlation groups, strict refusal of unknown data;
  - the persistence adapter on SM-SAVE, SM-CONFLICT and SM-LOAD;
  - the Forma UI for the 30 covered bindings.
- **Implement admissibility** (CONTRACT §11.3). OD-HC-03 A assigns it to the kernel: it must refuse the 35 inadmissible fixtures with their domain codes. This contract package does not claim it.
- **Run the real-provider durability evidence** (CR-LIVE-01, J3-05, INV-037) against GitHub.
- **Not to be implemented until the owner decides:** the 14 blocked bindings and 7 proposed commands. If OD-CR-01 A is recorded before kickoff, causa-storage 1.1.0 has to be released and v4.1.6 compiled and frozen first. That is owner-paced, and two or more agent-hours of compile and review work is an estimate. If OD-CR-01 B is recorded, the affected journeys become declared expected failures.

## 6. Recording the decisions (owner only)

1. **Merge this pull request.** `owner-freeze.fsx` reads requests and pinned files only from `origin/main`. Merging adds candidate documents and tooling. It is not a freeze.
2. **Record OD-CR-01..08.** From the directory that holds your clone, preview first:

   ```bash
   dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide contract-readiness --repo Indy-init --dry-run
   ```

   Then record:

   ```bash
   dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide contract-readiness --repo Indy-init
   ```

   - Each request shows its question, evidence, options and recommendation. OD-CR-01 and OD-CR-08 also show the sha256 of the files they pin.
   - Answer with a letter, or `skip` to leave a request pending.
   - The script commits `APR-CR-OWNER-n` to `kickoff/rehearsal-007/approvals.causa.json` as you and opens a pull request for you to merge.
   - Agents cannot record: the script refuses when an agent runs it.
3. **Record ODR-V415-02.** After #143 merges, run `owner-freeze.fsx -- approve v4.1.5`.
4. **Agent follow-up.** `contract-readiness generate` reads the recorded answers: a recorded decision stops blocking the gate. The bindings and commands it unblocks still have to be applied, for example releasing causa-storage 1.1.0 and compiling v4.1.6 under OD-CR-01 A, before the verdict can reach PASS.

## 7. Process

- Work is on branch `claude/loving-turing-ixvzqx` in Indy-init. Nothing is merged.
- No frozen package, approval record, application code, experiment code, verification asset or causa-data file was changed.
- No change was needed in `kemiller2002/ordo`, so its branch was not pushed.
- `kemiller2002/causa` was not accessible to this session (`add_repo`: no access). causa-data CONTRACT v1.0.0, which names the engine as its domain authority, is the reference (OD-HC-09 B).
- The repository's offline suite was run on this branch; its result is recorded in the pull request.
