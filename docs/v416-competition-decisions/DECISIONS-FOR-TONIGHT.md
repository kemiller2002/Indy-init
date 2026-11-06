# Decisions to record tonight (so 7 Nov has none)

Only the owner records decisions (RDP-003). Agents draft the requests; `tools/owner-freeze.fsx -- decide <set>` records
them. Each line: id — question — **recommended answer** — why. Reply "accept all" or list exceptions.

## A. Contract (v4.1.6 + causa-storage 1.1.0) — closes J2-03/04/06/07
| id | Question | Recommended | Why |
|---|---|---|---|
| RC-1 | One package or staged? | **One v4.1.6** = RecordConclusion + the minimal storage 1.1.0 below (the other OD-CR-01 A content later as 1.2.0 / v4.1.7) | One pin for tomorrow; smallest change that closes every J2 gap |
| S-1 | Storage 1.1.0 content | **B (minimal):** EvidenceRevised, ObservationRevised, kinds consistentWith + inconclusive | Fits tonight; rest of OD-CR-01 A as 1.2.0 (narrows it → recorded here) |
| S-8 | J2-06 "Reassess" | **B:** a new EvidenceRelated for the same pair is the reassessment; latest is current; drop the second-link refusal | Representable in v1.0.0 already; what the app does today |
| RC-2 / S-10 | Approve + tag v4.1.6 and causa-storage-v1.1.0, re-approve scope, re-pin lock | **Approve** after X01–X21 PASS, independent review, ST-01..06 PASS | Required for the competition pin |
| RC-3 | J2-03/J2-07 don't fill new required category (INV-045) / strength (INV-046) | **A:** amend J2-03/J2-07 to choose them (owner-controlled journeys) | Never store a value the user didn't choose |
| RC-4 | Residual-uncertainty input | **A:** optional catalog field "Residual uncertainty" | Lets a conclusion be recorded with an open blocking unknown |
| RC-5 | Decide-time rules V7/V8 | **A:** adopt | Conclusion can't claim a cause it doesn't cite |
| RC-6 | Show strength (IF41-VIEW change) | **A:** in v4.1.6 | Package is recompiled anyway |
| RC-7 | Position of the new command case | **A:** after RecordVerification | Catalog order |
| S-2 | Admissibility authority for 1.1.0 | **B:** rules stated normatively in CONTRACT 1.1.0; app kernel + negative fixtures are the oracle | No new reference-engine release needed tonight |
| S-3 | Creation byte-equality rule | **A:** adopt | One meaning per creation |
| S-4 | Length limits | **A:** UI/command boundary only | v1.0.0 practice |
| S-5 | projectionVersion | **A:** keep 1 | 1.0.0 projection unchanged |
| S-6 | 1.0.0 working copies under a 1.1.0 app | **A:** refuse 1.1.0-only commands with the reason; no migration | Fresh working copy tomorrow holds none |
| S-7 | Label on the wire | **A:** required in EvidenceRevised | INV-027 is required |
| S-9 | Tree name | **contract/v1.1.0/** | Matches v1.0.0 |
| CAT | Apply already-decided catalog edits (INV-028 required, INV-045, INV-046) | **Apply with v4.1.6** | Decided (OD-CR-02/03/04 A), not yet applied |

## B. Gate and process (Indy-init PR #174 + launch) — removes tonight's blockers
| id | Question | Recommended | Why |
|---|---|---|---|
| P-1 | Merge PR #174 process fixes 1, 2, 5, 6, 7, 8, 10 | **Merge each that the independent review approves**; changes-required ones fixed first | Each fixes a defect that blocked this run |
| P-2 | FSharp.Core 10.1.402 as a central package (fix 2) | **Approve** (dependency decision) | Without it 0 tests run |
| P-3 | @echelon-foundry/forma-workflow 1.2.0 (PolyForm-NC, your product) | **Approve** (you said yes; record it) | Causal graph (T23/T24/WDL) |
| P-4 | Gate: bounded journeys, always-run verdict, longer job (fix 5) | **Approve** | Removes the 40-min deadlock |
| P-5 | Owner-approved re-govern mechanism (fix 5) | **Approve** (owner record required) | Lets a fixed template be adopted without bypass |
| P-6 | Read-only + write tokens; "live" judged per slice (fix 6, ODR-R10-28 A) | **Approve**; same App, two scoped tokens | PF-015-O2/O3; one failing live test no longer fails every slice |
| P-7 | Competition repo: owner pre-creates it empty with both App secrets + branch protection (require PR) before T0; bootstrap pushes to it (Conditor `repo create` refuses an existing repo) | **Pre-create** (+ amend STARTUP §2 step 4 wording) | Today: secrets missing, protection unsettable from the session |
| P-8 | Agent actor identity in Praxis | **`claude`** (roster), never a lane name | verify-process would flag lane actors |
| P-9 | Design-contract populate step records evidence with a label only | **Amend it to fill content + source** (governed asset) | INV-028 makes source required; otherwise it fails honestly |
| P-10 | Fallback merge rule if the governed gate cannot run | **Rule B as a recorded contingency only**, labelled "implementer-verified, not governed-accepted" | So no one has to ask tomorrow |
| P-11 | WDL-013 | **Derive obligations** (your direction); if that needs a frozen-plan change, include it in v4.1.6 | Removes WDL-015-O2's failure |
| P-12 | Competition lock | **Re-pin** to the Indy-init commit with P-1 merged + v4.1.6 + new scope digest; rerun live preflight + launch-preflight READY tonight | Launch with zero open items |

## Order of operations tonight (owner steps in bold)
1. Agents: finish v4.1.6 + causa-storage 1.1.0 drafts (PRs), independent review of PR #174 and of v4.1.6.
2. **Record A and B** (`owner-freeze.fsx -- decide v4.1.6`, `decide competition` — the requests are added by the drafting agent).
3. **Merge** causa-data 1.1.0 PR → **tag** `causa-storage-v1.1.0`; **merge** PR #174 (approved fixes) and the v4.1.6 PR.
4. **`approve v4.1.6`**, **`tag v4.1.6`**.
5. Agent: frozen guard, scope recompile, proposed lock.
6. **Re-record ODR-R10-01 (scope), commit the lock (ODR-RG-04)**, dispatch the live-provider preflight.
7. Agent: launch-preflight → READY.

## C. Added after "accept all" (scope-selector fix, Indy-init PR #176) — not yet accepted
| id | Question | Recommended | Why |
|---|---|---|---|
| SS-1 | The fixed compiler yields a different R10 scope (scopeSha256 26e6ff3c… → 6a0e23b0…; 37 → 31 items; WDL-013 now gate-covered by T23). Re-approve R10's scope, or approve only the competition scope? | **Competition only:** compile the v4.1.6 competition scope with the fix and approve it on its own digest; leave R10's approved scope as history | R10 is already running; there's no reason to re-approve it |
| SS-2 | Approved split plans (work-item-split, milestone-aligned) name WI-GEN-UI/PF/EXT-DATA, which no longer exist | **Retire them for the competition**; do not redo them unless the v4.1.6 scope shows a slice too large for a lane | The fix puts those requirements on T18/T20/T22, which is what the splits approximated |
| SS-3 | PR #176 makes the `real`/`split` offline tests fail until SS-1 lands | **Merge #176 together with the v4.1.6 scope recompile** (same owner step 5), with the tests updated to the new approved digest | So main is never red |
