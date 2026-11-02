# Amendment 4.1.2: the three frozen-package defects Rehearsal 8 hit

v4.1.2 is the successor of the approved, frozen package v4.1.1 (APR-V411-OWNER-2, tag `indy-init-requirements-v4.1.1`). v4.1.1 is not edited.

v4.1.2 applies three owner decisions, which the owner gave on 2026-11-02 and records with `tools/owner-freeze.fsx decide v4.1.2`. It changes no requirement, obligation or test binding of v4 (X10).

| Decision | Defect (Rehearsal 8) | Change |
|---|---|---|
| ODR-V412-01: rate limit, INV-04's own numbers, every read counted | DEV-R8-11. INV-04 allows at most four reconciliation reads in 60 s, but T-S31 (Check again) and T-S32 (back online) read without limit, and the three automatic reads were counted against no window. | Every reconciliation read, automatic or not, is sent only while fewer than four were sent in the 60 s before `RequestContext.Now`. One reconciliation counts once, whatever its commit pages and file reads, per investigation session. T-S31 and T-S32 are replaced. T-S62 and T-S63 answer the same events at four: no read, and Check again stays offered. T-S65 answers back online while automatic reads are left: no extra read, and the automatic schedule resumes. T-S47 is replaced, so going offline no longer promises a read on return. `ReconcileDue` reschedules an automatic read at the limit. Contracts: `RequestContext.Now` (IF41-WIRE); `InvestigationSession.ReconcileReadsSentAt` and the windowed `ReconcileDue` (IF41-SAVE). |
| ODR-V412-02: add the command field (and the event body) | DEV-R8-09 and CR-T17-01. `CommandEnvelope` cannot say which of the 134 catalog commands it carries; `EventPayload` holds only a kind string. | `00-DOMAIN-STUBS.fs` restores FSHARP-TYPE-SHAPES §31–33: `Command = Core of CoreCommand` (29 cases) `\| Extension`; `EventPayload = CoreEvent of CoreEvent` (28 cases) `\| ExtensionEvent`; `CommandEnvelope.Command : Command`. Each payload type is opaque in the stub, and the domain owner defines it. |
| ODR-V412-03: write the transition into the table | DEV-R8-12. With the third automatic read failed, (UnknownOutcome, ReconcileReadFailed) had no transition, and the session stalled. | T-S64: offer Check again, nothing outstanding, no further automatic reads except one per offline-to-online change. These are T-S30's effects, the fail-safe reading Rehearsal 8 ratified. |

Every other part of v4.1.1 is carried unchanged.

## How the change is checked

- **Contracts.** X17 still requires every contract to equal its approved baseline (v4.1.1) byte for byte. The exceptions are the three files `analysis/amendment.json` declares in `contractChanges`, each with the SHA-256 of its new text and the decision that authorizes it. An undeclared or different change still fails.
- **State machines.** X12 checks:
  - `replaces` names a v4 transition, and reusing a v4 id without `replaces` fails;
  - the pairs that now have several transitions match their reviewed guard partitions in `analysis/guard-partitions.json`;
  - every transition names its decision and an approval; whether that approval is a real operator record selecting the decision is the offline suite's check (next item).
- **Owner records.** Each decision request lists, under `applies`, the exact transitions and contract digests its option applies. The offline suite requires the package to apply exactly that content for the option the owner's recorded approval selects. It also requires every approval the amendment names (`amends`, `contractBaseline`, transitions, contract changes) to be a real operator record, and no agent commit to have touched the approvals file since the v4.1.2 requests were introduced.
- **Specification preflight.** `percepta spec-preflight` (the rules that found the three defects) reports nothing blocking on the compiled v4.1.2. It still exits 3 on v4.1.1.

**Left as v4.1.1 has it:** before the automatic reads are spent, `CheckAgain` is not offered and fails closed. The guard partition says so (`exhaustive: false`). In v4.1.2, no transition offers Check again while automatic reads are left: T-S62, T-S63 and T-S64 offer it only once they are spent.

**Schedule:** unchanged from v4.1.1. The changes are guard conditions and one counted list in the persistence lane, within T20's estimate. Topology C keeps its 1-minute margin (419 of 420 minutes); this is the package author's estimate (independent review RV412-1-9).
