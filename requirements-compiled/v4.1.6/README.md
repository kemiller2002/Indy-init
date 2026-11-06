# Compiled package v4.1.6: RecordConclusion and causa-storage 1.1.0

**Status: CANDIDATE.** It is not approved, not frozen and not pinned by any rehearsal lock. The owner has recorded every ODR-V416 decision except the package approval, each with its recommended option, as APR-V416-OWNER-1 (PR #179). FREEZE step 2 wrote that approval id in (the three contract changes, `bootstrap.fsharpCore`, the WDL-013 supplementary plan entry) and it validates fully (X01-X21 PASS). It awaits its independent review record and its approval, ODR-V416-02.

v4.1.6 is the successor amendment of v4.1.5, which the owner approved as APR-V415-OWNER-3 (tag `indy-init-requirements-v4.1.5`). v4.1.5 is not edited.

It carries what OD-CR-01 A (APR-CR-OWNER-1/-2) commissions, as one package (ODR-V416-RC1 A) narrowed to the minimal causa-storage 1.1.0 (ODR-V416-S1 B):

| Part | What it does |
|---|---|
| RecordConclusion | `CoreCommand.RecordConclusion of RecordConclusion` after `RecordVerification`. It emits exactly one `ConclusionRecorded`, a causa-storage v1.0.0 event, and is checked by rules V1-V8. INV-032/INV-033 and J2-07/J2-09 become reachable, and so does every resolution of an investigation created in the app. |
| causa-storage 1.1.0 | `CoreEvent.ObservationRevised` and `EvidenceRevised` and their commands `ReviseObservation` and `ReviseEvidence`. Relationship kinds `consistentWith` and `inconclusive`. IF41-STORAGE is pinned to 1.1.0 and reads 1.0.0 unchanged (INV-025/027/029/031). |
| ConclusionCard.Strength | The view shows the conclusion strength (ODR-V416-RC6 A). |
| Catalog | INV-028 required, INV-045 category, INV-046 strength and INV-047 residual uncertainty, in the successor catalog ODR-V416-CAT pins. |

**Not in v4.1.6.** The four other OD-CR-01 A events and their commands (InvestigationDetailsRevised, HypothesisRevised, EvidenceRelationshipRevised, EvidenceRelationshipWithdrawn) wait for causa-storage 1.2.0 and a later package.

Compile it:

```
requirements-compiler v41 --source . --package requirements-compiled/v4.1.6 --base requirements-compiled/v4
```

The change is described in [AMENDMENT-4.1.6.md](AMENDMENT-4.1.6.md), and freezing it in [FREEZE.md](FREEZE.md).
