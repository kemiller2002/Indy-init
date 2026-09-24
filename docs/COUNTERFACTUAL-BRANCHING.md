# Counterfactual Branching and Sensitivity

Status: ambitious reusable analysis profile.

Counterfactual reasoning should be powerful without contaminating authoritative investigation history.

## 1. Branch model

A branch is a sandbox projection from an immutable source:

```text
Branch
- BranchId
- SourceInvestigationId
- SourceAggregateVersion
- CreatedBy
- CreatedAt
- Purpose
- HypotheticalChanges
- SemanticManifest
- PolicyRefs
```

Branch state is non-authoritative relative to the source investigation.

## 2. Hypothetical operations

Examples:

- suppress evidence from analysis
- mark evidence invalid for branch only
- flip an assumption
- resolve an unknown with a hypothetical value
- add hypothetical evidence
- remove a causal member
- change policy version
- change domain-pack version
- change verification outcome
- test alternate correction

Every operation is visibly hypothetical.

## 3. Branch outputs

Derived branch results may include:

- hypothesis comparison posture
- blockers
- obligations
- causal confirmation eligibility
- recommended next action
- conclusion posture
- report preview

No branch output becomes authoritative source state automatically.

## 4. Branch promotion

If a branch reveals a real-world scenario worth investigating:

- create new InvestigationId or explicit follow-up investigation
- preserve source branch provenance
- require real evidence admission
- do not copy hypothetical evidence as fact

## 5. Evidence removal sensitivity

Question:

> If E12 were unavailable or invalid, would the conclusion change?

Process:

1. clone projection at source version
2. suppress E12 relationship contribution
3. recompute derived posture/capabilities
4. compare semantic diff

Output may classify E12 as:

- non-pivotal
- material
- pivotal

Classification must be explainable.

## 6. Assumption sensitivity

For each material assumption:

- branch with assumption rejected
- recompute affected hypotheses/causal assertions
- identify conclusions that fail
- identify new blockers

This can expose hidden fragility.

## 7. Policy sensitivity

Run one state under multiple policy versions.

Do not mutate original state.

Show:

- changed blockers
- changed required evidence
- changed residual uncertainty treatment
- changed confirmation eligibility
- changed verification requirements

## 8. Domain semantic sensitivity

For active research:

```text
Same evidence snapshot
  -> EDF semantics vN
  -> EDF semantics vN+1
```

Compare derived results.

This is especially useful for researching EDF itself.

## 9. AI sensitivity

Run same branch/context through different:

- model
- provider
- prompt version
- agent role

Compare proposals, not authoritative conclusions.

## 10. Causal-member sensitivity

For causal set {A,B,C}:

- remove A
- remove B
- remove C
- inspect mechanism/confirmation consequences

This does not prove real-world counterfactual causation by itself. It evaluates dependency of the current reasoning model.

## 11. "What would change the answer?"

Generate the smallest set of currently unknown observations/findings that would materially alter:

- leading posture
- confirmation eligibility
- corrective choice
- resolution status

This is a derived decision-support feature.

## 12. Minimal-change explanation

Compare branches to answer:

> What is the smallest hypothetical change that would unblock ConfirmCause?

or:

> What is the smallest change that would make H2 lead instead of H1?

This can be computed through constrained search over blockers/relationships rather than AI prose alone.

## 13. Safety

Do not render branch state with the same visual authority as real state.

Required indicators:

- SANDBOX / WHAT-IF
- source version
- hypothetical modifications
- no remote authoritative mutation

## 14. Competition demonstration

Strong sequence:

1. show current H1 leading
2. open "What if E7 is invalid?"
3. branch recomputes
4. H2 becomes leading
5. show semantic diff
6. return to authoritative investigation unchanged

This demonstrates both sophistication and epistemic discipline.
