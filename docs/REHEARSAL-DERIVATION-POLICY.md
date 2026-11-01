# Rehearsal Derivation Policy

Status: normative process policy for every Causa rehearsal and competition run, from Rehearsal 4 onward. Adopted after defect DC-008. This policy does not retroactively change the evidence or result of any earlier rehearsal.

## Why

Rehearsal 3 derived its requirements from a short brief rather than from this repository. The mandatory P0 requirement UI-016 (Causa semantic color system) disappeared, and nothing detected it. A derivation is a lossy translation unless it is checked. This policy makes the check deterministic and mandatory.

## Authority

- **Baseline.** The authoritative source is the pinned baseline [`requirements/causa-requirements-baseline.json`](../requirements/causa-requirements-baseline.json). It is a `praxis.requirements-baseline/1` document; its scope and rules are described in [`requirements/README.md`](../requirements/README.md).
- **Source documents.** The baseline pins this repository's requirement documents by commit and SHA-256. Those documents remain the source of truth. The baseline is the inventory a rehearsal is checked against.
- **The gate.** The gate is `praxis requirements preservation verify`, from Praxis (kemiller2002/praxis, `docs/requirements-preservation.md`).
- **Ownership.**
  - Praxis owns intake, provenance, derivation and disposition.
  - The operator owns approvals (Ordo, once it backs them).
  - Dokimos owns the verification gates that run the command.
  - Percepta owns semantic UI verification.

## RDP-001: Derive from the pinned baseline

Every rehearsal MUST derive its requirements package from the pinned baseline. A brief, summary or earlier rehearsal package MAY guide prioritisation, but it never replaces the baseline as the source.

- **Recording.** The rehearsal records the baseline id, its pinned commit and its SHA-256 in its own repository.
- **Changed source.** If the source documents change after pinning, the gate reports drift. The rehearsal then either re-pins against the new commit and re-derives, or records an operator decision to stay on the earlier pin.
- **Regenerating.** Re-pinning means regenerating the baseline with the F# generator, `requirements-compiler baseline . <commit>` (authoritative since 2026-11-01, [OWNER-DECISIONS-2026-11-01](OWNER-DECISIONS-2026-11-01.md); it replaced the byte-identical `tools/build-requirements-baseline.py`). It never means editing the manifest by hand.

## RDP-002: Every requirement gets a disposition

The rehearsal MUST write a `praxis.requirements-disposition/1` file that gives every requirement in the baseline exactly one disposition:

| Disposition | When | Requires |
|---|---|---|
| `preserved` | The derived package carries the requirement. | A mapping of every obligation to the derived criteria that carry it. |
| `strengthened` | The derived package is stricter. | Mappings plus a rationale. |
| `excluded-with-approval` | Genuinely out of the approved scope. | An operator approval record (RDP-003). |
| `superseded` | Replaced by an approved replacement. | The replacement criteria plus an operator approval record. |
| `not-applicable` | The requirement cannot apply to this run. | A justification plus an operator approval record; without one, the gate reports `needs-review`. |

- **No requirement is skipped.** A requirement with no disposition fails the gate.
- **One obligation, several criteria.** An obligation MAY map to several criteria, but only when they carry all of it together.
- **Partial exclusion.** One obligation of a preserved requirement MAY be excluded on its own, again only with an operator approval record that covers that obligation.
- **Exact values.** Value tokens (palette hex values, contrast ratios, sizes, timings, counts) MUST appear literally in the mapped criteria. Rewording that drops a value fails the gate.
- **Reviews.** An obligation that no token can decide is accepted only through a recorded human equivalence review (`review: {reviewer, date, basis}`). The agent that derived the package MUST NOT record that review.

## RDP-003: Approvals are written by the operator, never by the agent

Exclusions, supersessions, not-applicable dispositions and conflict decisions MUST cite an approval record in a separate `praxis.requirements-approvals/1` file. Each record states:

- the decision id;
- the approving authority;
- the scope;
- the requirement, obligation or conflict ids it covers;
- the date.

**Who writes the file.** The operator writes the approvals file, or it is produced from an Ordo decision once Ordo backs approvals. An agent MUST NOT:

- write, edit, generate or infer an approval record;
- copy an approval from another run;
- treat its own proposed decision record as an approval.

**When no approval exists yet.** An agent that needs an exclusion records the request, for example in a decision record such as "operator decisions requested", and leaves the exclusion unapproved. The gate then fails, and that failure is the correct state.

**Conflicts.** A conflict between mandatory requirements MUST be declared in the disposition. It stays BLOCKED until an operator approval decides it.

## RDP-004: The gate passes before implementation and again at acceptance

The gate MUST pass at two points:

1. **Before implementation starts.** Implementation work items MUST NOT begin until `praxis requirements preservation verify` returns PASS (exit 0) for:
   - the rehearsal's derived package;
   - its disposition;
   - the operator's approvals;
   - this repository at the pinned commit.
2. **At acceptance.** Acceptance MUST re-run the gate against the final requirements package and the same, or a deliberately re-pinned, baseline. A package that changed after the first run without a passing re-run is not accepted.

**Evidence.** Each run's JSON evidence (`--output`) is committed with the rehearsal's acceptance evidence, together with the exact command. The evidence records the SHA-256 of every input.

## RDP-005: Missing verification is BLOCKED or UNKNOWN, never PASS

- **The gate cannot run.** This covers missing inputs, an unreadable baseline (exit 2) or an internal failure (exit 1). The requirements state is then UNKNOWN, and implementation and acceptance are BLOCKED.
- **The gate fails or blocks (exit 3).** The run is not accepted, whatever any other test reports.
- **Needs review.** A `needs-review` finding blocks until a person records the review.
- **Requirements still pending.** No report, ledger or summary may present a requirement as satisfied while its preservation is failed, blocked, unknown or unreviewed.
- **Derivation is not delivery.** A PASS from this gate proves only that obligations survived derivation. It is never evidence that the application meets them. Implementation acceptance still requires the verification each requirement names, such as deployed-SHA screenshots, contrast checks and independent review.

## Procedure for a new rehearsal

1. **Resolve the commit.** Resolve this repository's commit for the run. If the baseline's pinned commit differs and the source documents changed, regenerate the baseline (`requirements-compiler baseline . <commit>`) on a reviewed branch.
2. **Scope.** State the run's scope. Request operator approvals for every exclusion the scope implies.
3. **Derive.** Derive the requirements package in the rehearsal repository's documented Praxis requirements location, through its documented Praxis input workflow. Never use GitHub Issues.
4. **Reconcile.** Write the disposition file.
5. **Verify.** Run the gate:

   ```bash
   praxis requirements preservation verify \
     --baseline <indy-init>/requirements/causa-requirements-baseline.json \
     --source <indy-init checkout at the pinned commit> \
     --derived research/requirements \
     --disposition research/requirements-preservation/disposition.json \
     --approvals research/requirements-preservation/approvals.json \
     --output evidence/requirements-preservation/gate.json --json
   ```

6. **Fix and re-run.** Fix every failure, then re-run the gate. Implementation begins only after PASS.
7. **At acceptance,** re-run the gate and commit its evidence (RDP-004).
