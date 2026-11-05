# Consuming the contract package at the competition

**Status:** candidate. The package is usable now as a read-only reference. It is a governed asset only after the owner records OD-CR-08. Until then its gate is advisory, and nothing in Conditor, Praxis, Dokimos or Ordo enforces it.

## 1. Fetch and verify (clean checkout)

From an empty directory:

```bash
git clone https://github.com/kemiller2002/Indy-init SRC
```

```bash
git -C SRC checkout <package commit>
```

```bash
git clone https://github.com/kemiller2002/causa-data causa-data
```

```bash
git -C causa-data checkout --detach e8259df74add723765c57a5b1a5888d9c3f707af
```

```bash
dotnet --version
```

The last command must print `10.0.402`. The SDK is pinned in `SRC/global.json` with `rollForward: disable`.

```bash
dotnet build SRC/tools/contract-readiness -c Release
```

```bash
dotnet SRC/tools/contract-readiness/bin/Release/net10.0/contract-readiness.dll check --causa-data causa-data --indy-root SRC
```

`check` writes nothing, and its exit code says how the run went:

- **0:** every contract check passed or is declared UNVERIFIED, every negative control failed as expected, and every generated file plus the package checksum equals a fresh generation. It then prints the verdict.
- **1:** the suite or the package is broken.
- **2:** you passed `--require-pass` and the verdict is not PASS.

The checksum rule is in `PACKAGE-MANIFEST.json`: sha256 over `"<sha256>  <path>\n"` for every package file and every validator source. The tool needs no NuGet package, so the build runs offline.

## 2. What each competition role reads

| Role | Reads | Must not |
|---|---|---|
| **Domain** (T13/T14 kernel) | `COMMAND-EVENT-REGISTRY.json` (commands, preconditions, validation errors, idempotency, result variants); `STATE-TRANSITIONS.json` `domainMachines`; `FIELD-REGISTRY.json` (`Payload.*` fields with F# element, type and meaning); `DOMAIN-CONTRACT.json` principles | Invent a field, a default or an enum value; add a `CoreEvent` case that is not in v4.1.5. Proposed cases wait for OD-CR-01. |
| **Storage / integration** | `FIELD-REGISTRY.json` (every wire field, rule and enum); `PERSISTENCE-CONTRACT.md`; `HISTORICAL-DATA-INVENTORY.json` (what real data contains); the validator's checks as the acceptance target for its own codec: canonical round trip, UUIDv5 ids, correlation groups | Report "Saved" before PS-VERIFIED; write to a catalog workspace; drop or ignore an unknown field or event type |
| **UI** (ui, ui-2, ui-3) | `UI-FIELD-MAP.json`: label = accessible name, input type, validation, command, write and read-back field ids, screen states, mobile obligations; `07-IF41-VIEW.fs` types | Bind a `blocked` binding to storage; render historical `insufficient` as `inconclusive` (OD-HC-07 B) |
| **Verification** | `VERIFICATION-MATRIX.json`: 50 executed rows (32 checks, 18 negative controls) and implementation obligations; catalog verification kinds `INV-0nn/Vn/<kind>` | Count a PENDING-IMPLEMENTATION or BLOCKED row as passed |

A competition agent can start the domain and the Forma UI in parallel at once on the **30 covered bindings** and the **41 commands defined in v4.1.5**. The 14 blocked bindings and 7 proposed commands must stay unimplemented until the owner records OD-CR-01..07, or the owner declares them expected failures (OD-CR-01 B). An agent must not choose the meaning of a blocked field itself.

## 3. The Echelon tools: supported interfaces and missing adapters

Versions are those of `kickoff/rehearsal-010/toolchain.lock.proposed.json`.

| Tool | Supported interface (exists today) | What it consumes from this package | Missing adapter |
|---|---|---|---|
| **Conditor** 0.10.20 | `conditor repo create`; `conditor acceptance preflight --target SRC` reads `conditor.acceptance.json`. Its verifier is still `python3 tools/compute-acceptance-verdict.py`, one of the tools OD-INDY-14 lists for porting. | Nothing directly. | A T0 preflight step `contract-readiness check --causa-data <dir> --require-pass` (OD-CR-08 A). No Conditor command runs it today. |
| **Praxis** 3.25.0 | `praxis requirements manifest compile --catalog …causa-investigation.requirement-catalog.json`; `praxis work begin/checkpoint` | The INV ids. Every UI binding is keyed by the catalog id Praxis compiles. | Praxis does not read `UI-FIELD-MAP.json` or `FIELD-REGISTRY.json`. A field's domain write path is not part of the Praxis manifest. Adding it would be a Praxis schema change (`praxis.requirement-catalog/1`). |
| **Dokimos** 0.11.1 | `dokimos verification compile --indy-execution-plan … --indy-verification-plan …`; `dokimos verification requirements` (catalog acceptance through `requirements-compiler catalog-acceptance`) | The catalog verification kinds named in each binding. | Dokimos does not consume `VERIFICATION-MATRIX.json`. The read-back check ("the value shown after reload is the field the command wrote") has no Dokimos check kind; it is judged by the owner-controlled journeys J1–J3. 0.11.0 lacked `url-input` (INV-029); dokimos#67 fixes it only when a released, qualified version is pinned. |
| **Ordo** 1.20.0 | `ordo init`, `ordo status`, `ordo verify` (repository operating system; method and doctrine) | Nothing. | No Ordo mission, registry or verify rule references a contract package. Recording this package as an Ordo evidence item or decision would need an Ordo-side change. That is out of scope here, and the `kemiller2002/ordo` branch was not changed. |

## 4. Rules that still apply

- **OD-INDY-22.** The competition writes its own code from the requirements. The validator in `tools/contract-readiness` is Indy-init tooling, run from the governed checkout (SRC), like `verification/`. It is not copied into the application, and its decoder is not a library for the app.
- **Frozen packages** are never edited. A contract change is a new successor package.
- **Agents never record owner decisions** (RDP-003).
- **No implementation before the committed initialization checkpoint** (`r9.fsx check-sequencing`).
