# Verification plan: consumer contract

The verification plan is the per-requirement statement of what acceptance must prove for every baseline P0 requirement. It is written for Ordo, Praxis and Dokimos to consume. The requirements compiler generates it; nobody edits it by hand.

| | |
|---|---|
| Artifact | `requirements-compiled/v2/verification-plan.json` (v1, frozen as committed at `459f65a`, holds `VP-e77a2134e1b0`, the plan Rehearsal 5 uses) |
| Schema | `requirements-compiled/v2/verification-plan.schema.json` (JSON Schema 2020-12, `schema: "indy-init.verification-plan/1"`, revision 2) |
| Generator | `tools/requirements-compiler` (F#), in the same run as the rest of the package. It is listed in `package-manifest.json` under `generated`. |
| Curated input | `requirements-compiled/v2/curation/verification-plan.json`: freshness policies, the gate-to-policy map, test bindings (with evidence categories), exclusions and the pinned approvals source. `requirements-compiled/v2/curation/evidence-categories.json`: the declared evidence-category rules |
| Current identity | `planId` `VP-1b2fed568a3b`, digest `1b2fed568a3b6837ad923b7e81a237292f0677342665bbeed248dd042becda92` (baseline `BL-CAUSA-INDY-INIT-aab8f80c7743`: 108 P0 requirements, 326 obligations, 29 bound to Praxis producers, no application test bound, 0 unclassified). Process and owner evidence classes: [PROCESS-EVIDENCE-AND-READINESS.md](PROCESS-EVIDENCE-AND-READINESS.md) |

**Revision 2 is additive.** Every revision-1 member keeps its name, type and meaning; the only change to an existing member is the value `UNVERIFIED` appended to `statusVocabulary.verification`. Ordo's reader (`VerificationPlanWire.readPlan`, which ignores unknown members) reads the v2 plan: 108 requirements, 326 obligations. The v1 schema document is closed (`additionalProperties: false`), so a strict validator of that document rejects a revision-2 plan; validate against the v2 schema.

## What a consumer may rely on

1. **Identity.**
   - `planIdentity.digest` is the SHA-256 of the plan with `planIdentity` removed, serialized by the compiler's canonical writer: ASCII JSON, two-space indent, keys in the generated order, trailing newline.
   - `planId` is `VP-` followed by the first 12 hex digits of the digest.
   - Pin the digest, not the file path. Any content change produces a new digest.
2. **Completeness.**
   - Every baseline requirement with priority `P0` appears exactly once in `requirements`, sorted by id.
   - The requirement id, priority, title, document and `sectionSha256` are copied unchanged from the baseline.
   - An excluded requirement stays in the plan, marked `exclusion.status: "excluded"` with its approval id.
   - Compilation check V18 fails if any of this is violated.
3. **Obligations.**
   - Each obligation keeps its baseline id (`sourceObligation`), its verbatim `text` and a `textSha256`.
   - The obligation `id` is `VPO-` followed by the first 16 hex digits of `sha256(requirementId + "\n" + sourceObligation + "\n" + text)`.
   - The id is therefore stable across compilations, test bindings and reordering. It changes only when the requirement, the obligation id or the verbatim text changes. Use it as the obligation key in Ordo and Praxis records.
4. **Test identity.**
   - An obligation's `testIdentity.status` is `bound` only when the curation names an exact test id for that obligation (`testBindings`). Otherwise it is `missing` (in the acceptance contract, `unbound`). The generator never infers a test.
   - A binding is `{obligation, tests: [{id, kind, categories}]}`. `categories` is the non-empty set of evidence categories that test fully proves for that obligation, and each one must be possible for the test's kind (for example, `live-provider` needs a `live` test). Compilation fails (V18) otherwise.
   - The requirement-level status is `bound`, `partial` or `missing`.
   - At baseline `aab8f80` every obligation is `missing`.
5. **Verification method.** `verificationMethod` gives the gates, plus each rule's id, gate, owner, method and tooling, copied from `verification-manifest.json`. Acceptance criteria and linked acceptance sections are in `acceptance`.
6. **Evidence contract.**
   - The top level sets the rules for every requirement:
     - evidence files use `causa.acceptance-evidence/1`;
     - the manifest uses `causa.acceptance-evidence-manifest/1`;
     - `run_id`, `source_sha`, `subject_type`, `subject_id`, `status` and `exit_code` must match;
     - the file's SHA-256 must equal its manifest entry.
   - Each requirement also gives its subject, and the required evidence as the union of its rules' evidence.
7. **Freshness.** Each requirement has one `freshness.policy`: the strictest of the default policy, its gates' policies (`gatePolicies`) and its bound tests' kinds.
   - `FP-LIVE-PER-COMMIT` (gates G6 and G9, or a `live` test): the evidence must come from the candidate commit's own run. It is never reused.
   - `FP-REVIEW-PER-COMMIT` (gate G7, or a `review` test): the evidence is bound to the reviewed commit. Any reuse needs a new reviewer attestation.
   - `FP-PROCESS-THIS-RUN` (a `process` or `owner` producer): the record must be in the run's own Praxis log (or, for an owner attestation, observed by the run's CI) and bound to the exact candidate. It is never reused.
   - `FP-DETERMINISTIC-INPUT-DIGEST` (the default, or a `deterministic` test): the evidence is reusable only when the test identity and every input digest are unchanged and Dokimos reports `Reusable:true`. It is never reused from an earlier rehearsal.
   - `boundTo` and `invalidatedBy` are the manifest defaults plus the rules' own entries.
8. **Dependencies.** `dependencies.requirements` and `dependencies.contracts` are the requirements and contracts whose change invalidates the evidence.
9. **Exclusions.**
   - An exclusion is valid only when the pinned approvals file (`source.approvals`, `causa.requirements-approvals/1`, checked by SHA-256) contains an approval whose `authority.kind` is `operator`, whose `authority.id` is in `trustedOperators`, and which has a change `{requirement_id, action: "exclude"}` for that requirement.
   - Any other exclusion fails compilation, and the requirement stays in scope.

10. **Acceptance contract (revision 2).**
   - Each obligation carries `tokens` (its baseline value tokens, verbatim) and an `acceptanceContract` with these members:
     - `requirement`, `obligation`, `obligationId`, `priority` and `verificationMethod` (gates and rule ids);
     - `requiredEvidenceCategories` and `categoryDerivation`;
     - `evidenceProducers`: each required category's evidence class (`application-test`, `reviewer`, `process`, `owner`) and expected producer;
     - `testIdentity` (`bound` or `unbound`, with each test's categories);
     - `dependencies`;
     - `freshness` (the strictest of the requirement policy and the category policies);
     - `verificationStatus` (`unverified`, `unclassified` or `excluded`, never `verified` in the plan);
     - `missingCoverage` (`no-bound-test`, `no-evidence` or `unclassified`).
   - The contract is a pure function of the plan entry, the obligation, its bound tests and the embedded `evidenceCategories` table. A consumer can re-derive it, and the checker does.
11. **Evidence categories.**
   - `evidenceCategories` embeds `curation/evidence-categories.json`. It contains:
     - the seven categories, each with a definition, the test kinds that can provide it, and an optional freshness policy;
     - rules `{id, category, when, source}`. The `when` conditions are `rules`, `gates`, `documents`, `requirements`, `titlePattern`, `obligationKinds`, `obligations` and `freshnessPolicies`. Every stated condition must hold, and any listed value matches.
   - An obligation requires the union of the categories of the rules that apply to it.
   - With no applicable rule, an obligation is `unclassified`: it is reported as missing coverage, never given a default.
   - Changing the table changes the plan digest.

## Checker rejections

`requirements-compiler verify-plan` and compilation check V18 run the same preservation checks. Each problem is a diagnostic `{code, subject, detail, action}`, and every rejection below has a negative-control test (`tools/requirements-compiler/tests`, `--offline`).

| Code | Rejects | Example detail |
|---|---|---|
| `MISSING-REQUIREMENT` / `MISSING-OBLIGATION` | A baseline P0 requirement or obligation is absent from the plan. | `P0 requirement PF-005 obligation PF-005-O1 is missing from the plan` |
| `TEST-MAPPING` | The ledger names a test that the plan binds to none of the requirement's obligations. A bound test is absent from a ledger row that claims PASS or names tests. An evidence file reports a test under an obligation it is not bound to, or reports an obligation the requirement does not have. | `ledger test 'X' claims VER-001, but the plan binds it to none of its obligations` |
| `ALTERED-VALUE` | A changed title, document, section hash, priority, obligation kind, value tokens (including swapped tokens), text, order or index. A text with the same words in a different order is named a role/value swap. | `obligation UI-016-O11 text: role/value swap (the same words in a different order)` |
| `SWAPPED-REQUIREMENT-IDENTITY` | A plan entry carries another requirement's content (matched by section hash, or by title and obligations). | `plan entry AR-001 carries the content of baseline requirement AR-002` |
| `SWAPPED-OBLIGATION-IDENTITY` | An obligation carries another obligation's verbatim text or VPO id: the R5 AR-002-O2 gap. | `obligation AR-002-O1 carries the VPO id … of obligation AR-002-O2` |
| `STALE-EVIDENCE` | Evidence or its manifest comes from another run or commit, even when the digests have been re-pinned. | `evidence … is bound to source … not the candidate …` |
| `UNAUTHORIZED-EXCLUSION` | An exclusion without an authenticated operator approval in the pinned approvals file and in the plan. | `unauthorized exclusion of PF-005 (approval 'APR-INVENTED' …)` |
| `CONTRACT-INCONSISTENT` | An acceptance contract that differs from what the embedded rules derive, for example removed categories or a forged `verified`. | `acceptance contract differs from what the embedded rules derive (requiredEvidenceCategories, …)` |
| `PLAN-IDENTITY`, `EXTRA-REQUIREMENT`, `EXTRA-OBLIGATION`, `DUPLICATE`, `EVIDENCE-INTEGRITY` | A digest mismatch, invented entries, duplicates, or a manifest, ledger or approvals pin mismatch. | |

Any of these makes the verdict INVALID.

## Verifying a candidate against the plan

```bash
dotnet tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll verify-plan \
  --plan requirements-compiled/v2/verification-plan.json --baseline requirements/causa-requirements-baseline.json \
  --ledger LEDGER.json --evidence-manifest MANIFEST.json --evidence-manifest-sha256 HEX --evidence-root DIR \
  --trusted-source-sha SHA --approvals APPROVALS.json --trusted-operator-id kemiller2002 [--out FILE]
```

The command writes `indy-init.verification-plan-assessment/1` and exits 0 only for ACCEPTED (3 otherwise, 2 for unusable input). It complements `tools/compute-acceptance-verdict.py`, which remains the authority for gates, defect classes and the ledger. Acceptance requires both to report ACCEPTED.

**Per-obligation verification.** An obligation is `verified` only when every required category is covered by a test that meets all of these conditions:
- the test is bound to that obligation and declares that category;
- it is reported `passed` under that obligation's VPO id;
- the report is inside evidence that is in the pinned manifest, digest-identical, bound to the requirement and fresh, on a ledger row that claims PASS.

A requirement-level PASS, or a passing test bound to another obligation, verifies nothing. The assessment lists `verificationStatus`, the covered categories and `missingCoverage` for every obligation. A missing category has one of these reasons: `no-bound-test`, `no-evidence`, `evidence-not-fresh`, `evidence-invalid`, `evidence-not-pass` or `unclassified`.

**Per-requirement statuses:**
- `PASS`: fresh, digest-matched evidence, and every obligation is verified.
- `UNBOUND`: the evidence passes, but the plan has no test identity, so the obligations are not shown to be verified.
- `UNVERIFIED`: the evidence passes and tests are bound, but some obligation lacks coverage for a required category (for example, a unit test bound to a browser and accessibility obligation).
- `FAIL` and `BLOCKED`: copied from the ledger. They are never upgraded.
- `NOT_RUN`: copied from the ledger.
- `MISSING`: there is no row, a PASS has no evidence, or the evidence file is absent.
- `STALE`: the evidence comes from a different run or commit than its freshness policy allows.
- `INVALID`: a digest mismatch, a wrong subject, a wrong priority, an unauthorized exclusion, or an incorrect test mapping on a PASS row.
- `EXCLUDED`: an authorized exclusion that the plan records.

**Verdict precedence:** INVALID (also forced by any integrity problem or any `TEST-MAPPING` diagnostic), then FAILED, then BLOCKED, then INCOMPLETE (including UNBOUND and UNVERIFIED), then ACCEPTED.

The Rehearsal 4 replay against revision 2 is recorded in `requirements-compiled/v2/readiness/evidence/r4-verification-plan-replay.json`; the v1 replay stays in `requirements-compiled/v1/readiness/evidence/`. Validation against the R4 and R5 baselines is in `requirements-compiled/v2/readiness/evidence/r4-r5-baseline-validation.json`.

## Process, reviewer and owner evidence

`praxis:<record kind>` bindings (kinds `process`, `owner`, and `review` for `praxis:independent-review`) are producers,
not ledger tests: `verify-plan` never expects them in a ledger row. They are decided by
`requirements-compiler verify-process` from the rehearsal's Praxis log. `verify-plan` takes the same inputs with a
`--process-` prefix, computes that assessment itself for its own plan and trusted candidate, and counts the categories
it verified; it never reads an assessment file. See
[PROCESS-EVIDENCE-AND-READINESS.md](PROCESS-EVIDENCE-AND-READINESS.md).

## Consuming the plan before implementation

See `requirements-compiled/v2/README.md`, "How agents consume the plan before implementation". In short:
1. Pin the digest, and read each obligation's `requiredEvidenceCategories` and `freshness.policy` before writing code.
2. Have the owner bind each test to the obligations it fully proves, with exactly the categories it proves.
3. Report results per VPO id.
4. Treat any obligation that is not `verified` as open work.
