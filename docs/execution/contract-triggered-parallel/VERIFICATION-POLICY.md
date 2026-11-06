# CTP-001 verification policy

## 1. Independence
- **Who writes the tests.** The verification agent writes acceptance tests from the frozen requirements and the released registry only: accessible names, field IDs, transitions and requirement IDs. Never from the implementers' markup, code or tests.
- **Implementer self-attestation is not evidence.** "My tests pass" proves only the implementer's own understanding (EXP-002 D-3: unit tests and the static binding check passed, yet the UI auto-saved on Start against INV-004).
- **How it is enforced:**
  - CTP-I-INDEPENDENT refuses an acceptance author listed among the implementers.
  - CTP-C-TESTS and CTP-C-TRANSITION-TEST require an AT- obligation for every field, action, workflow outcome and transition before the contract is even VALIDATED.

## 2. When
- **At every integration.** The independent browser test runs at every integration (INTEGRATION-POLICY.md step 5), not only at the end.
- **Mobile.** It includes the mobile viewport checks the package requires (EXP-002 used 375×812).
- **Static checks are additional, never a substitute.** That includes the binding check and contract-check.

## 3. What an acceptance test must find
- Every field by its accessible name, including nested fields and option labels.
- Every control by its own accessible name.
- Every transition's observable result. For example, after Start, the save state shows **Unsaved**.
- Validation errors as stated.
- Persistence round-trip: save, read back, reload, reopen, with every value restored.
- Gaps found while writing a test are reported as change requests, not worked around (EXP-002 CR-1 and CR-2 came from this).

## 4. What counts as verified
| Claim | Evidence required |
|---|---|
| Component behaviour | unit/UI tests and the static binding check |
| Integrated behaviour | an independent browser acceptance run on the integrated build, at the group's released hash |
| VERIFIED (lifecycle) | the above, plus required full CI passed on the candidate SHA |
| Durable GitHub persistence | a real-provider write and an independent read-back on the authorized test repository. A REST double is never reported as persistence acceptance |
| Competition acceptance | the governed gates (slice gate, catalog acceptance, journeys, completion gate). CTP-001 evidence feeds them; it does not replace them |

## 5. Reporting
- **Test results** are stated as run, with run references.
- **Unexecuted checks** are listed as NOT RUN.
- **Modelled figures** are labelled as models.
