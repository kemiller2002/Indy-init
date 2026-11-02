# Amendment 4.1.2: the three frozen-package defects Rehearsal 8 hit

v4.1.2 is the successor of the approved, frozen package v4.1.1 (APR-V411-OWNER-2, tag `indy-init-requirements-v4.1.1`). v4.1.1 is not edited.

v4.1.2 applies three owner decisions, which the owner gave on 2026-11-02 and records with `tools/owner-freeze.fsx decide v4.1.2`. It changes no requirement, obligation or test binding of v4 (X10).

| Decision | Defect (Rehearsal 8) | Change |
|---|---|---|
| ODR-V412-01: rate limit, INV-04's own numbers | DEV-R8-11. INV-04 allows at most four reconciliation reads in 60 s, but T-S31 (Check again) and T-S32 (back online) read without limit. | T-S31 and T-S32 are replaced: they read only while fewer than four reconciliation reads were sent in the 60 s before `RequestContext.Now`. T-S62 and T-S63 answer the same events at four: no read, and Check again stays offered. Contracts: `RequestContext.Now` (IF41-WIRE), `InvestigationSession.ReconcileReadsSentAt` (IF41-SAVE). |
| ODR-V412-02: add the command field (and the event body) | DEV-R8-09 and CR-T17-01. `CommandEnvelope` cannot say which of the 134 catalog commands it carries; `EventPayload` holds only a kind string. | `00-DOMAIN-STUBS.fs` restores FSHARP-TYPE-SHAPES §31–33: `Command = Core of CoreCommand` (29 cases) `\| Extension`; `EventPayload = CoreEvent of CoreEvent` (28 cases) `\| ExtensionEvent`; `CommandEnvelope.Command : Command`. Each payload type is opaque in the stub, and the domain owner defines it. |
| ODR-V412-03: write the transition into the table | DEV-R8-12. With the third automatic read failed, (UnknownOutcome, ReconcileReadFailed) had no transition, and the session stalled. | T-S64: offer Check again, nothing outstanding, no further automatic reads except one per offline-to-online change. These are T-S30's effects, the fail-safe reading Rehearsal 8 ratified. |

Every other part of v4.1.1 is carried unchanged.

## How the change is checked

- **Contracts.** X17 still requires every contract to equal its approved baseline (v4.1.1) byte for byte. The exceptions are the three files `analysis/amendment.json` declares in `contractChanges`, each with the SHA-256 of its new text and the decision that authorizes it. An undeclared or different change still fails.
- **State machines.** X12 checks:
  - `replaces` names a v4 transition, and reusing a v4 id without `replaces` fails;
  - the pairs that now have several transitions match their reviewed guard partitions in `analysis/guard-partitions.json`;
  - every transition cites its decision and the operator approval that records it.
- **Owner records.** The offline suite compares each amendment with the option the owner's recorded approval selects.
- **Specification preflight.** `percepta spec-preflight` (the rules that found the three defects) reports nothing blocking on the compiled v4.1.2. It still exits 3 on v4.1.1.

**Left as v4.1.1 has it:** before the automatic reads are spent, `CheckAgain` is not offered and fails closed. The guard partition says so (`exhaustive: false`). It is not an owner decision of this amendment.
