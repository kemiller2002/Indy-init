# Verification plan: consumer contract

The verification plan is the per-requirement statement of what acceptance must prove for every baseline P0 requirement. It is written for Ordo, Praxis and Dokimos to consume. The requirements compiler generates it; nobody edits it by hand.

| | |
|---|---|
| Artifact | `requirements-compiled/v1/verification-plan.json` |
| Schema | `requirements-compiled/v1/verification-plan.schema.json` (JSON Schema 2020-12, `schema: "indy-init.verification-plan/1"`) |
| Generator | `tools/requirements-compiler` (F#), in the same run as the rest of the package. It is listed in `package-manifest.json` under `generated`. |
| Curated input | `requirements-compiled/v1/curation/verification-plan.json`: freshness policies, the gate-to-policy map, test bindings, exclusions and the pinned approvals source |
| Current identity | `planId` `VP-d1efaf695ef4`, digest `d1efaf695ef4e4e43f1c5d3b33c3f7e87e36d5980afdbf2472c316d89915634f` (baseline `BL-CAUSA-INDY-INIT-aab8f80c7743`: 108 P0 requirements, 326 obligations, 0 bound test identities) |

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
   - An obligation's `testIdentity.status` is `bound` only when the curation names an exact test id for that obligation (`testBindings`). Otherwise it is `missing`. The generator never infers a test.
   - The requirement-level status is `bound`, `partial` or `missing`.
   - At baseline `aab8f80` every obligation is `missing`. Binding tests is a Rehearsal 5 task.
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
   - `FP-DETERMINISTIC-INPUT-DIGEST` (the default, or a `deterministic` test): the evidence is reusable only when the test identity and every input digest are unchanged and Dokimos reports `Reusable:true`. It is never reused from an earlier rehearsal.
   - `boundTo` and `invalidatedBy` are the manifest defaults plus the rules' own entries.
8. **Dependencies.** `dependencies.requirements` and `dependencies.contracts` are the requirements and contracts whose change invalidates the evidence.
9. **Exclusions.**
   - An exclusion is valid only when the pinned approvals file (`source.approvals`, `causa.requirements-approvals/1`, checked by SHA-256) contains an approval whose `authority.kind` is `operator`, whose `authority.id` is in `trustedOperators`, and which has a change `{requirement_id, action: "exclude"}` for that requirement.
   - Any other exclusion fails compilation, and the requirement stays in scope.

## Verifying a candidate against the plan

```bash
dotnet tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll verify-plan \
  --plan requirements-compiled/v1/verification-plan.json --baseline requirements/causa-requirements-baseline.json \
  --ledger LEDGER.json --evidence-manifest MANIFEST.json --evidence-manifest-sha256 HEX --evidence-root DIR \
  --trusted-source-sha SHA --approvals APPROVALS.json --trusted-operator-id kemiller2002 [--out FILE]
```

The command writes `indy-init.verification-plan-assessment/1` and exits 0 only for ACCEPTED (3 otherwise, 2 for unusable input). It complements `tools/compute-acceptance-verdict.py`, which remains the authority for gates, defect classes and the ledger. Acceptance requires both to report ACCEPTED.

**Per-requirement statuses:**
- `PASS`: fresh, digest-matched evidence, and every obligation has a bound test.
- `UNBOUND`: the evidence passes, but the plan has no test identity, so the obligations are not shown to be verified.
- `FAIL` and `BLOCKED`: copied from the ledger. They are never upgraded.
- `NOT_RUN`: copied from the ledger.
- `MISSING`: there is no row, a PASS has no evidence, or the evidence file is absent.
- `STALE`: the evidence comes from a different run or commit than its freshness policy allows.
- `INVALID`: a digest mismatch, a wrong subject, a wrong priority, or an unauthorized exclusion.
- `EXCLUDED`: an authorized exclusion that the plan records.

**Verdict precedence:** INVALID (also forced by any integrity problem), then FAILED, then BLOCKED, then INCOMPLETE, then ACCEPTED.

The Rehearsal 4 replay and its negative controls are recorded in `requirements-compiled/v1/readiness/evidence/r4-verification-plan-replay.json`.
