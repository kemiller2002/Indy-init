# Proposed requirements changes

**Nothing here is applied.**
- **Frozen packages** (v4, v4.1, v4.1.1, v4.1.2, v4.1.4) are never edited. AGENTS.md says so, and CI checks each against its tag.
- **v4.1.5 is a CANDIDATE** awaiting ODR-V415-01..04, and this work does not touch it either.

Each change below is written so the owner can approve or reject it on its own. An approved change enters through the normal successor-package route: `requirements-compiler v41`, then independent review, then `owner-freeze.fsx`. Agents do not record the decisions.

| ID | Change | Convention | Where it would land | Breaking? | Owner decision requested |
|---|---|---|---|---|---|
| PRC-01 | Package release conditions | CV-01, CV-11, CV-14 | FREEZE.md of the next successor package | no (adds release conditions; no build change) | ODR-IC-01 |
| PRC-02 | Pin Forma 0.13.0 | CV-17 | successor AGENT-RULES; `analysis/forma-*-inventory.json`; Conditor `indy-init` preset | **yes** (dependency version) | ODR-IC-02 |
| PRC-03 | Bootstrap runs the convention checks | CV-05, CV-07, CV-10, CV-17 | successor AGENT-RULES §1 (T10) | no | ODR-IC-03 |
| PRC-04 | Durable means read back | CV-09 | successor AGENT-RULES (persistence) + verification evidence categories | no; clarifies SM-SAVE | ODR-IC-04 |
| PRC-05 | WASM boot smoke in the first CI | CV-04 | successor AGENT-RULES §1 (T10) | no | ODR-IC-05 |
| PRC-06 | Test-only code never ships | CV-19 | successor AGENT-RULES; candidate review checklist | no | ODR-IC-06 |

## PRC-01: package release conditions (CV-01, CV-11, CV-14)

Add to the successor package's `FREEZE.md`, under the conditions the freeze requires:

> **Release conditions (ODR-IC-01).** The owner does not approve the package until its independent review records all three of the following:
> 1. Every generated command has run on the pinned SDK in a clean checkout, with its outcome. This covers every suite command (`r9.fsx suite-preflight --require-tests`), every generated workflow, the SDK setup step, and every deploy precondition named in STARTING-STATE.md.
> 2. Replay coverage of the pinned causa-data histories reports 0 events without a case. An unknown kind is preserved, never defaulted. A negative control that removes one case makes it fail.
> 3. A table lists every owner decision that names a requirement of the package, with the amendment that applies it or the waiver that records why the clause stands.

**Evidence:**
- HF-028 / HF-047: MSB1008 shipped in frozen v4.1.2.
- HF-029 / HF-033: SDK setup and deploy preconditions were missing.
- HF-068 / HF-069: 487 of 1,315 events, and the missing workspace.
- HF-023 / HF-036: DEC-025 vs EXT-DATA-005, and INV-04 vs SM-SAVE.

**Cost:** about 90 + 60 + 30 min per package, before the competition. Nothing is added inside the timed run.

## PRC-02: pin Forma 0.13.0 (CV-17), breaking

The current state is inconsistent:
- v4.1.4 and v4.1.5 AGENT-RULES name "Forma 0.12.0" patterns, and carry `analysis/forma-0.12.0-inventory.json`.
- `kickoff/rehearsal-010/design-contract.json` (ODR-R10-05) allows 0.12.x or 0.13.x.
- R9 used 0.13.0 (`verification/design-contract/forma-0.13.0-inventory.json`).
- The Conditor 0.10.18 scaffold fixture pins 0.12.0 (`conventions.fsx check` CK-04).

Forma 0.12.0 fails 48 of 78 containment checks, and 0.13.0 passes 78 of 78 (HF-080, `forma` 0ffb077).

Proposed text for the successor AGENT-RULES "Use only published Forma" bullet. Replace "Forma 0.12.0" with:

> Forma **0.13.0** (`@echelon-foundry/design-system` 0.13.0, inventory `verification/design-contract/forma-0.13.0-inventory.json`, tarball sha256 as pinned there). Bootstrap fails if any `package.json` resolves an earlier Forma (`conventions.fsx check --forma-min 0.13.0`, CK-04). Application CSS adds no overflow, radius or validation-state rules on Forma classes; a Forma gap is reported upstream with a failing fixture (FORMA-PATTERNS.md).

**Consequences for the owner to weigh:**
- **Re-baselining:** the layout contract and the recorded gap remedy (nine 0.12.0 patterns with short links) must be re-checked against 0.13.0.
- **Bootstrap:** the Conditor preset must install 0.13.0. That is a Conditor change, proposed here and not made.

## PRC-03: bootstrap runs the convention checks (CV-05, CV-07, CV-10, CV-17)

Add to successor AGENT-RULES §1 (T10), after `conditor ci-policy check`:

> run `dotnet fsi <indy-init>/tools/conventions/conventions.fsx -- check --workspace .` (CK-01 warnings-as-errors, CK-02 one source per port literal, CK-03 workflow shell with pipefail, CK-04 Forma version); a finding stops the bootstrap commit like a ci-policy finding.

**Cost:** under 1 s per run, plus a one-time `defaults: run: shell: bash` in each scaffolded workflow. The existing scaffold already passes CK-01, CK-02 and CK-03, and fails only CK-04 (Forma).

**Not proposed:**
- **Running the checks on every merge.** They are cheap, but the owner may prefer to keep the per-merge gate unchanged.
- **Adding them to protected CI.** The directive excludes changing protected checks.

## PRC-04: durable means read back (CV-09)

Add to successor AGENT-RULES, persistence role:

> A change is shown as Saved only after a provider read-back, made with the HTTP cache bypassed, returns the blob SHA that was written (SM-SAVE). Device-only state is never rendered as provider-confirmed. A durability obligation is verified only by a real-provider run on the candidate SHA; a provider double is a unit-test aid, and it replays recorded real responses (Cache-Control, ETag, pagination, size limits) with the run it was recorded from named.

**Evidence:** HF-006, HF-021, HF-026, HF-039, HF-040, HF-074. Rationale: PERSISTENCE-PATTERNS.md.

## PRC-05: WASM boot smoke in the first CI (CV-04)

Add to successor AGENT-RULES §1 (T10), first green CI:

> The first CI run also publishes `Causa.Web` in Release, without full debug info, and loads it in Chromium until first render (60 s timeout). View code renders one screen region per function.

**Evidence:** HF-007 (about 120 min, R4) and HF-013 (90 min, R5).

## PRC-06: test-only code never ships (CV-19)

Add to the candidate review checklist:

> The candidate bundle contains no test-hook symbol (e.g. `EnableTestHooks`), and traces and logs redact `Authorization` headers.

**Evidence:** HF-010.

## Changes deliberately not proposed

- **A standard per-feature file split (CV-22).** There is no evidence for it, and it would change the frozen project structure.
- **A falsifying control for every bound test (CV-13).** Its modeled net is negative at central; see IMPACT-FORECAST.md.
- **Per-PR contract-version naming as a gate (CV-12, PA-04).** It waits for EXP-INDY-002's measurements.
