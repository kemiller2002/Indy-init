# Owner decisions required (not self-approved)

These are requests only. No agent records, approves or applies them (RDP-003). Each states its options, a recommendation, and the evidence behind it. They are not yet in `owner-freeze.fsx`'s machine-readable request format. Recording them that way is part of REQ-ESU-01, once the owner picks the vehicle in ODR-ESU-01.

The deadline context: v4.1.5's fallback trigger is Nov 6 22:00 UTC (ODR-V415-04), and kickoff is Nov 7.

## ODR-ESU-01: the application's dependency lock moves to echelon-current 1.37.0

**Today:** the frozen app lock (v4, the base of every v4.1.x package) pins echelon-current 1.33.0. The Rehearsal 010 toolchain lock installs 1.37.0. Five systems differ: Praxis 3.24→3.25, Dokimos 0.9.0→0.11.1, Ordo 1.19→1.20, percepta-semantic 0.3→0.4 and **Forma 0.12→0.13**. Conditor differs too (0.10.18→0.10.20). See [VERSION-DRIFT-RESULTS.md](VERSION-DRIFT-RESULTS.md), DP-04.

**Risk:** behaviour change. Forma 0.13.0 breaks long unbroken tokens at container edges, and `.ef-cluster` and `.ef-actions` children are capped at the row's width. No class, token or pattern was removed: the inventory is identical, 352/524/168.

| Option | Effect |
|---|---|
| **A (recommended)** | Adopt `docs/optimization/echelon-stack-upgrade/lock/dependency-lock.json` as the dependency lock of the next package after v4.1.5 (call it v4.1.6), with the changes in REQUIREMENTS-CHANGES.md. v4.1.5's freeze path is untouched. If v4.1.6 cannot be frozen before kickoff, the event runs on v4.1.5 with the R10 toolchain lock as it is: the tools are already 1.37.0, and Conditor already scaffolds Forma 0.13.0. The only remaining gap is that the layout-contract inventory names 0.12.0. That gap is harmless: the v4.1.5 layout contract passes on 0.13.0 (executed, 0 failed) |
| B | Fold the lock into v4.1.5 before its freeze. This reopens v4.1.5's independent review and recompilation, and puts the Nov 6 22:00 fallback at risk |
| C | Keep 1.33.0. The split between the app lock and the toolchain lock remains, and PR #141's ODR-IC Forma pin stays unmet |

## ODR-ESU-02: Forma 0.13.0 is the competition's Forma (the layout contract inventory)

Today, `analysis/forma-layout-contract.json` and `analysis/forma-0.12.0-inventory.json` in v4.1.1–v4.1.5 name 0.12.0. The R10 design contract (`kickoff/rehearsal-010/design-contract.json`) already accepts both inventories (ODR-R10-05), and Conditor 0.10.20 scaffolds 0.13.0.

**Evidence (executed):**

- Forma's LRP-001 suite, Chromium: 0.12.0 fails 16 of 26 checks, 0.13.0 passes 26 of 26.
- The v4.1.5 contract passes on 0.13.0 with either inventory.

| Option | Effect |
|---|---|
| **A (recommended)** | The successor package's layout contract names `forma-0.13.0-inventory.json` (byte-equal to `verification/design-contract/forma-0.13.0-inventory.json`), and its CI step downloads the 0.13.0 tarball. This is the PR #141 ODR-IC Forma pin |
| B | Keep both versions allowed (status quo, ODR-R10-05). Agents may still install 0.12.0 |

**Known open in 0.13.0** (Forma's spec asserts these still fail): `.ef-dashboard-grid`, `.ef-split-pane` and `.ef-operation-status` overflow at iPad portrait (820 px) with page-level 200% text.

- **Supported alternative (Forma `docs/reliability/indy-init-compatibility.md`):** place these regions in a single column, or in a container that collapses them, at that breakpoint.
- **Not acceptable:** application CSS that re-adds `overflow-wrap` (HF-085). 0.13.0 does that upstream.
- **Forma 0.13.1 (unreleased):** fixes marketing containment only. It does not fix these three, and it is not a published release.

## ODR-ESU-03: the generated CI installs the pinned SDK (Conditor HF-029)

Conditor 0.10.20 still scaffolds `setup-dotnet dotnet-version: "10.0.x"`. The v4.1.5 bootstrap writes `global.json` 10.0.402 with `rollForward: disable`. R8 and R9 both lost CI runs to this.

| Option | Effect |
|---|---|
| **A (recommended for the event)** | The kickoff startup instructions make the first commit after T0 replace `dotnet-version: "10.0.x"` with `global-json-file: global.json` in the generated workflows. Run `stack-drift.fsx check` (DP-02 shows READY after the correction). No release is needed |
| B | Conditor 0.10.21: scaffold templates use `global-json-file`, and Conditor writes or requires `global.json`. The owner decides the SDK pin's source (Conditor constant, Registry, or the requirements package). This is a Conditor release plus a toolchain-lock re-pin before kickoff, and has the highest schedule risk |
| C | The v41 bootstrap rewrites the workflows. This is a compiler change, which changes the compiler tree digest that frozen packages record |

## ODR-ESU-04: adopt the version-drift preflight

`tools/stack-drift/stack-drift.fsx` is added on this branch. It is not wired into any gate.

| Option | Effect |
|---|---|
| **A (recommended)** | Add `stack-drift.fsx test --registry` to the `fsharp-tooling` CI job. At T0, require `stack-drift.fsx check --lock <adopted lock> --registry … --toolchain-lock … --workspace <competition repo> --bin ~/.local/bin --conditor … --fetch …` to be READY before the scope checkpoint |
| B | Keep it as an operator tool only |

## ODR-ESU-05: the Conditor scaffold fixture used by the layering tests

`tools/requirements-compiler/tests/fixtures/conditor-0.10.18-scaffold` predates the pinned Conditor.

| Option | Effect |
|---|---|
| **A (recommended)** | Record a 0.10.20 fixture under the same README rules, and point the `layered415` CI step and the offline suite at it in the successor package. Layering onto the real 0.10.20 scaffold was executed in this audit and builds |
| B | Keep 0.10.18 |

## Not requested

- **Downgrades:** none are proposed.
- **Optional systems:** none (Arca, Fides, Strata and the rest) are adopted.
- **Bolero:** no exception is needed.
