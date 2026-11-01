# Independent review of v4.1.1

The reviews are independent: each was run by an agent that did not write the package, read-only, with instructions to challenge it. A failed review is recorded as failed. [analysis/independent-review.json](analysis/independent-review.json) holds the latest round, with each finding's disposition.

## Round 2: FAIL

Reviewed commit 6db97ae. The reviewer verified 11 of the 14 round-1 dispositions as real and complete. One major finding, seven minor and one note remain; none is blocking. Every one has a disposition below, and round 3 reviews the fixes.

| Finding | Severity | Disposition |
|---|---|---|
| RV411-2-1 | major | Fixed: R0.1-O1 and the kickoff checks are no longer declared met. Like ADRP-016-O2, they are stated, not claimed, and the owner decides them (ODR-V411-02). Also dispositioned under ODR-V411-02: RISK-12, CAUSA-DATA-CONTRACT-v1 §10.8, DOMAIN-TEST-MATRIX (introduction and §29) and PRODUCT-REQUIREMENTS line 1056, all now in ODR-V411-02's question. The scan covers v4's risk register and those documents. STARTING-STATE, AMENDMENT and READINESS name exactly what is scanned and call the scan a heuristic. |
| RV411-2-2 | minor | Fixed: wider gate words (permit, sign-off, wait, until, hold, obtain, ask). v4.1.1's own AGENT-RULES, README and FREEZE are scanned from the package inputs. An offline control adds 'Do not start until an organizer has signed off' to duty 1 and X19 fails. |
| RV411-2-3 | minor | Fixed: the record names the quoted published rules, and says the section's other lines were edited under OD-INDY-18. Each historical record carries a source and an exact quote, and X19 checks the source still contains it. The external constraints are verbatim quotes, which X19 finds in HACKATHON-BOUNDARIES. |
| RV411-2-4 | minor | Fixed: AGENT-RULES may name the entrypoints only in four reviewed lines, each pinned by SHA-256. Any other line that names them fails X18, whatever its wording; the review's three rewrites are offline controls. The documents describe exactly that. |
| RV411-2-5 | minor | Fixed: the contract baseline must equal the amended package and its approval (offline control). The stripper follows the F# lexer: strings, characters, verbatim and triple-quoted strings, then comments, nested. Record expressions are code. The five new forms are offline controls. READINESS and V411-B now name byte equality as the guarantee and the scan as a heuristic. |
| RV411-2-6 | minor | Fixed: the X19 title, amendment.json, the optimization report and ODR-V411-01 say 'partly measured (2 of 29 minutes)'; ODR-V411-01 names the warm NuGet cache. The script's step 7 now times git add and commit together without --allow-empty. Its committed run (83.0 s, cold Conditor cache) is recorded under reproduction, with provenance stating that R1-R3 predate it. |
| RV411-2-7 | minor | Fixed: both v4.1 sentences in the carried optimization report are marked superseded in v4.1.1 and point to STARTING-STATE. |
| RV411-2-8 | minor | Fixed: (1) 'not sought' reads 'the package as proposed (option A) does not seek it'. (2) The impact sentence is corrected. (3) owner-freeze.fsx approve v4.1.1 refuses while a requirement-conflict request of v4.1.1 is pending. (4) The decide body names the package's own directory. |
| RV411-2-9 | note | Fixed: the README duplicate is removed; the ui verification lag is listed under 'What remains unverified'; ODR-V411-02 names the REHEARSAL-AGENT-INTERFACE-FREEZE clause. |

### The reviewer's report (round 2)


- **Package:** `requirements-compiled/v4.1.1`
- **Commit reviewed:** `6db97ae` (branch `claude/v411-amendment`)
- **Verdict:** **FAIL**. One major finding remains, and any blocking or major finding fails the round.

One major finding and seven minor findings remain, plus one note. Of the 14 round-1 findings, 11 have real and complete fixes and 3 are only partly fixed (RV411-1-1, -8 and -9). The checks pass: v41 --check passes on v4.1.1 (X01-X20) and on v4.1 (X01-X16), and the offline suite passes in full with exit 0. v4, v4.1 and kickoff/ are byte-unchanged. No approval was fabricated.

#### What was run

- dotnet build tools/requirements-compiler/src -c Release: 0 warnings, 0 errors
- v41 --check on v4.1.1 (X01-X20) and on v4.1 (X01-X16): both PASS
- offline suite (--offline --source .): all passed, exit 0 (the round-1 W16 failure did not reproduce)
- git diff 3f5d062..HEAD for requirements-compiled/v4, requirements-compiled/v4.1 and kickoff: empty. v4 equals tag indy-init-requirements-v4.0.0. The v4.1 manifest sha256 is 779916ab...31736a, the digest APR-V41-OWNER-4 approved
- v4.1.1's contracts, layout-tests, state machines, verification execution plan, preservation report and analysis inputs are byte-identical to v4.1's. All 45 manifest digests match the files
- Independent recomputation of the lane-free critical path: 334 minutes (T10 29, T14 20, T13 45, T17 20, T22 70, T28 45, T31 45, T32 20, T33 20, T34 20)
- /tmp/claude-0/review411-r2/scripts/x17r2.fsx probes V41Package.contractImplementations with the round-1 forms and new ones
- Scratch mutations under /tmp/claude-0/review411-r2: mutX17, mutX18, mutX19 and mutR1x18

#### Round-1 dispositions

| Finding | Verified | Note |
|---|---|---|
| RV411-1-1 | **no** | Only partly fixed. The items round 1 listed now have dispositions, and ODR-V411-02 covers ADRP-016-O2, STORAGE-008-O1, PRE-ORG-01/T06, OD-INDY-12 and the four waivers. However, R0.1-O1 is declared 'met' under OD-INDY-18 rather than put to the owner. The completeness claim is false, and X19 is still bypassable for v4.1.1's own rules. See RV411-2-1 and RV411-2-2. |
| RV411-1-2 | yes | The fix is real. X17 compares every contract byte for byte with v4.1 and pins each of the 7 members by SHA-256. All 18 round-1 probe forms are now detected (x17r2.fsx), and the contracts are clean. Residual problems: the line scan misses lexer-level forms, contractBaseline.package is not bound to the approved package, and READINESS and amendment.json V411-B still overclaim. See RV411-2-5 (minor). |
| RV411-1-3 | yes | T10 now reads evidence 'measured+estimated', 2 + 27 = 29, and X13/X19 enforce the split. ODR-V411-01, AGENT-RULES, README and the sensitivity texts say 2 measured and 27 estimated, and the script is committed. Unqualified 'measured' labels remain elsewhere. See RV411-2-6 (minor). |
| RV411-1-4 | yes | T17 is 20 minutes (15 + 5 for change requests), and SENS-CR-200 gives C = 424. C is 419, with 1 minute of margin, and this is stated plainly. |
| RV411-1-5 | yes | 'permitted' is removed. Step 3 cites KICKOFF-EXECUTION-PACKET section 1 as an internal plan. The residual risk now names the Conditor scaffold and the 68-document import. |
| RV411-1-6 | yes | Disclosed: the import comes from 2eee2d5. Note that T01 pins the baseline in the planning repository. It does not replace the stale import inside the competition repository, and the package does not state whether the two conflict. |
| RV411-1-7 | yes | A rule naming a task now replaces the owner rule (V41Package.fs sensitivity). The combined scenario gives C = 475, which is consistent with round 1's 469 plus the T17 change. |
| RV411-1-8 | **no** | Items (1)-(4) and (6) are fixed. Item (5) is not: the carried optimization-report.md still says 'The T10 bootstrap makes the application repository non-empty before kickoff' and 'the organizers allow pre-kickoff scaffolding ... The owner's own record makes that authoritative', with no superseded marker beyond the general header. See RV411-2-7. |
| RV411-1-9 | **no** | The round-1 rewrite is now rejected (mutR1x18 FAIL). New rewrites pass X18 and validation (mutX18): 'ui modifies ... itself and skips the change request', 'ui edits web/causa.css directly too; integration should never edit it' and 'The UI lane may also change the loading shell in index.html without any change request'. The claim that X18 'rejects any rule line that lets a UI role edit the files directly' is false. See RV411-2-4. |
| RV411-1-10 | yes | The recorded Conditor 0.10.18 fixture is in place, and CI layers onto it and builds both solutions. The package.json 'dependencies' case is handled. The offline layering passes (177 of 178 written, 2 merged). |
| RV411-1-11 | yes | Spans that cross the window go to the unsplit bucket. In the fixture, byPhase (pre-event + timed + unsplit) sums exactly to the totals for every bucket. A measurement with no finalizedAt still falls back to collectedAt; this is disclosed in the rules. |
| RV411-1-12 | yes | The offline suite passed in full in this round (exit 0). |
| RV411-1-13 | yes | AMENDMENT B states the Bearer scheme and exact read-back equality as the two fixed choices. |
| RV411-1-14 | yes | OD-INDY-18 now separates the owner's quoted words from the agent's application. The 9001890 subject stays, as stated. The optimization-report sentence from the remediation is still unmarked (RV411-2-7). |

#### Findings

| ID | Severity | Area |
|---|---|---|
| RV411-2-1 | major | C: organizer obligations and ODR-V411-02 coverage |
| RV411-2-2 | minor | C: X19 robustness |
| RV411-2-3 | minor | C: historical records |
| RV411-2-4 | minor | E: X18 prose check |
| RV411-2-5 | minor | B: X17 line scan and baseline binding |
| RV411-2-6 | minor | A: residual 'measured' labels and measurement provenance |
| RV411-2-7 | minor | Docs: carried organizer statements |
| RV411-2-8 | minor | Owner decisions: neutrality and process around ODR-V411-02 |
| RV411-2-9 | note | Docs and schedule details |

##### RV411-2-1 (major): C: organizer obligations and ODR-V411-02 coverage

Amendment C still settles part of the organizer question itself and overstates how complete its scan is. (1) amendment.json declares R0.1-O1 ('must: The competition application's source implementation must begin at hackathon kickoff from a blank application repository or the organizer-approved equivalent') and the kickoff packet's 'empty/organizer-compliant' checks 'Met by its first branch', under OD-INDY-18. AMENDMENT C repeats this ('Some are met without any organizer'). Whether code authored before kickoff and written at T0 satisfies 'source implementation must begin at kickoff' is exactly the question STARTING-STATE leaves open. That code includes 7 implementation members, plus the generator output, the Conditor scaffold and the requirements import: 'An organizer could read the materialized contract declarations ... as material prepared before kickoff ... This policy does not claim that the F# contracts are exempt under every reading'. OD-INDY-18's quoted owner words do not decide R0.1-O1, and ODR-V411-02 does not ask about it. READINESS A meanwhile says 'No requirement is weakened or claimed met'. (2) STARTING-STATE says 'Every organizer reference in the frozen v4 inputs and the internal rule documents has a recorded disposition ... X19 scans for them and fails on any without one', and ODR-V411-02's impact says 'X19 lists every organizer reference'. Both are false. CAUSA-DATA-CONTRACT-v1 section 10 step 8 is a v4 source that the inventory cites 6 times, and it reads 'Before competition, verify organizer permission for pre-created dataset and app-specific executable schemas'. v4 risk-register RISK-12's mitigation reads 'Owner obtains it before the event (ADRP-016)'. DOMAIN-TEST-MATRIX section 29 ('Before kickoff, verify event rules regarding pre-written executable tests') and PRODUCT-REQUIREMENTS:1056 bear on the pre-written layout assertions that v4.1.1 writes at T0. None of these has a disposition, and ODR-V411-02 does not mention them. As a result, the owner's decision on the organizer conflict is posed over an incomplete list, and one 'must' obligation is resolved by the agent.

**Evidence.** requirements-compiled/v4.1.1/analysis/amendment.json startingState.organizerReferences (R0.1-O1, KICKOFF-EXECUTION-PACKET section 1 / preamble entries: 'Met by its first branch'); AMENDMENT-4.1.1.md section C; docs/READINESS-V4.1.1.md section A; STARTING-STATE.md:22 and :63; proposals/owner-decision-requests.json ODR-V411-02 question/impact; docs/CAUSA-DATA-CONTRACT-v1.md:170; requirements-compiled/v4/risk-register.json RISK-12; docs/DOMAIN-TEST-MATRIX.md:5,517-523; docs/PRODUCT-REQUIREMENTS.md:1056; requirements-compiled/v4/requirements-inventory.json R0.1-O1 (modality must)

**Remediation.** Do not declare R0.1-O1 or the kickoff-packet checks met. Give them the same disposition as ADRP-016-O2 (stated, not claimed, decided by the owner) and add them to ODR-V411-02's question. Add dispositions for CAUSA-DATA-CONTRACT-v1 section 10.8, RISK-12 and DOMAIN-TEST-MATRIX section 29 / PRODUCT-REQUIREMENTS:1056, and list them in ODR-V411-02. Either scan every v4 source document and the v4 risk register, or reword the completeness claim to name exactly what is scanned. Make READINESS A consistent with whatever is claimed.

##### RV411-2-2 (minor): C: X19 robustness

X19 still verifies 'no task waits on organizer permission' only for the timed DAG and one literal phrase in v4.1.1's own rule documents. Its gate regex (confirm|approv|verif|permission|interpretation) also misses ordinary wording. In mutX19, 'Do not start until an organizer has signed off on the prepared material in writing.' was added to AGENT-RULES duty 1 (T10's own instruction), and 'The integration agent holds T10 until the organizers sign off on the generator.' was added to HACKATHON-BOUNDARIES. Both regenerate and check with X19 PASS and validation PASS. A disposition quote also covers any later line that contains it.

**Evidence.** tools/requirements-compiler/src/V41Package.fs bootstrapCheck (organizerDocuments = 3 docs; AGENT-RULES only checked for 'relies on ODR-V41-06'); scratch /tmp/claude-0/review411-r2/mutX19

**Remediation.** Add v4.1.1's AGENT-RULES.md, STARTING-STATE.md and README.md to the scanned documents. Widen the pattern, for example to organi[sz]er near wait|until|before|hold|sign|ask|obtain. Add a negative test for an organizer wait in AGENT-RULES duty 1.

##### RV411-2-3 (minor): C: historical records

startingState.historicalRecords records 'docs/HACKATHON-BOUNDARIES.md published-rule section (last reviewed 2026-09-24)' as 'kept unchanged'. This branch edited two lines inside that section: 'Continue to verify ambiguity around:' became 'Open interpretation questions (no task waits on them, OD-INDY-18 ...)', and 'Until clarified, keep ...' became 'Keep ...'. The section still carries 'Last reviewed: 2026-09-24'. The published quotes themselves are intact. X19 checks only that the disposition string equals 'kept unchanged' and never compares the records' content.

**Evidence.** git diff 3f5d062..HEAD -- docs/HACKATHON-BOUNDARIES.md (lines 98 and 106 fall under '## Published rule verification', which starts at line 75); V41Package.fs bootstrapCheck historicalRecords loop

**Remediation.** Narrow the record to the quoted published statements, or note the 2026-11-01 edit in that section. Pin each historical record by digest (the ODR-V41-06 JSON, APR-V41-OWNER-1 and the quote block) so that X19 checks the content and not a label.

##### RV411-2-4 (minor): E: X18 prose check

The roles-table check is now positive, and the round-1 rewrite fails. The whole-document line scan is still a keyword heuristic. Its verb list is 'directly|edits?|writes?|owns' and its role list is 'ui|ui-2|UI role(s)|UI agent(s)'. Any line containing 'never edit', 'does not edit' or 'No two agents', or starting with '| integration', is exempt. In mutX18, three added lines pass X18 and validation: 'ui modifies `web/index.html` and `web/causa.css` itself and skips the change request', 'ui edits `web/causa.css` directly too; integration should never edit it while ui works', and 'The UI lane may also change the loading shell in index.html without any change request'. READINESS D and AMENDMENT E say X18 'rejects any rule line that lets a UI role edit the files directly'.

**Evidence.** V41Package.fs entrypointCheck; scratch /tmp/claude-0/review411-r2/mutX18 (X18 PASS, validation PASS); mutR1x18 (round-1 rewrite now FAIL); docs/READINESS-V4.1.1.md section D; AMENDMENT-4.1.1.md section E

**Remediation.** Reword both documents to say what the check does: the UI rows must match the fixed sentence, and a keyword scan covers other lines. Alternatively, require that the entrypoint paths appear in AGENT-RULES only in the integration row, the fixed ui sentence and duty 4.

##### RV411-2-5 (minor): B: X17 line scan and baseline binding

The line scan's comment and string stripping does not follow the F# lexer. Five of the new probe forms go undetected (x17r2.fsx). A string containing '(*' blanks every line up to the next '*)'. A char literal '"' or a verbatim @"\" swallows the keyword that follows on the same line. A triple-quoted '(*' and a module-level record expression are also missed. The byte-equality guarantee reads contractBaseline.package, a field of amendment.json that nothing binds to amends.package (which X20 pins to requirements-compiled/v4.1) or to APR-V41-OWNER-4. In mutX17, setting it to requirements-compiled/v4.1.1 and adding a hidden 'module Hidden = let run ...' to IF41-APP behind '[<System.Obsolete("(*")>]' gives these results: ContractCheck builds with 0 warnings, X17 PASS and validation PASS on the second generation and on --check, and contract-classification labels 06-IF41-APP.fs 'specification: types and signatures only'. CI's 'diff -r v4.1/contracts v4.1.1/contracts' would still catch it, and the committed contracts are clean. Two overclaims remain: READINESS C ('X17's line scan is a heuristic that rejects any other code') and amendment.json V411-B ('X17 rejects any implementation member that is not on the reviewed allowlist').

**Evidence.** /tmp/claude-0/review411-r2/scripts/x17r2.fsx (R2a-R2e NOT DETECTED); /tmp/claude-0/review411-r2/mutX17; V41Package.fs:1569 (baseline = contractBaseline.package, unchecked); analysis/amendment.json changes V411-B; docs/READINESS-V4.1.1.md:41

**Remediation.** In X17, require contractBaseline.package = amends.package and contractBaseline.approval = amends.approval, or drop the field and use amends.package. Optionally verify the baseline files against the approved manifest's per-file digests. Strip strings and char literals before block comments, and handle verbatim and triple-quoted strings. Reword READINESS C and V411-B.

##### RV411-2-6 (minor): A: residual 'measured' labels and measurement provenance

Unqualified 'measured' remains in owner-facing text. The X19 title (validation-report.json and the --check output) reads 'with a measured duration'. amendment.json's description reads 'at T0 with a measured duration'. optimization-report.md lines 5 and 126 say 'with a measured duration'. ODR-V411-01's recommendation says 'into the timed window, measured'. ODR-V411-01 says '2 are measured on fresh empty repositories' without the condition that every run had a warm NuGet cache. tools/measure-t0-bootstrap.fsx was committed (8d04bba) after the measurement (71b69b1) and does not match the recorded steps: 7-commit uses --allow-empty, and its git add -A is untimed. No raw logs are committed, so 'reproduces the measurement' is unverified.

**Evidence.** requirements-compiled/v4.1.1/validation-report.json:115; analysis/amendment.json description; optimization-report.md:5,126; proposals/owner-decision-requests.json ODR-V411-01; analysis/bootstrap-measurement.json environment.nugetCache and steps[7]; tools/measure-t0-bootstrap.fsx

**Remediation.** Change the X19 title and the remaining texts to 'partly measured (2 of 29 minutes)'. Add 'warm NuGet cache' to ODR-V411-01. Run the committed script once and commit its output, or say that the record predates the script.

##### RV411-2-7 (minor): Docs: carried organizer statements

The v4.1.1 package's optimization-report.md still says 'The T10 bootstrap makes the application repository non-empty before kickoff' and 'The owner stated in session on 2026-10-30 that the organizers allow pre-kickoff scaffolding. The owner's own record makes that authoritative.' The owner's record (APR-V41-OWNER-1 organizer_confirmation) reads 'I approved it.' It does not say what the organizers allowed. Both sentences contradict STARTING-STATE ('does not restate or reinterpret what the organizers said') and v4.1.1's T0 plan. Round 1 asked for them to be marked superseded (RV411-1-8(5), RV411-1-14), and neither disposition did so.

**Evidence.** requirements-compiled/v4.1.1/optimization-report.md:39-41; kickoff/rehearsal-007/approvals.causa.json APR-V41-OWNER-1

**Remediation.** Put an explicit 'superseded in v4.1.1: see STARTING-STATE.md' marker on those two sentences. The file is authored, so v4.1 itself is not touched.

##### RV411-2-8 (minor): Owner decisions: neutrality and process around ODR-V411-02

Four problems. (1) STARTING-STATE ('Organizer approval of the prepared material (ADRP-016-O2) is not sought') and the STORAGE-008-O1 disposition ('is not sought') state option A of the pending ODR-V411-02 as fact. Options B and C both involve an organizer record. (2) ODR-V411-02's impact says 'X19 lists every organizer reference with ODR-V411-02 as the decision', but 7 of the 20 references name OD-INDY-18. (3) It also says 'Until decided, v4.1.1 cannot be approved', but owner-freeze.fsx approve only prints 'Note: still pending' and records the approval. (4) owner-freeze.fsx decide's pull-request body for v4.1.1 tells the agent to 'apply these decisions in requirements-compiled/v4.1 (FREEZE.md step 2), recompile v4.1'. That is the approved package, which must stay byte-identical. The question itself is otherwise neutral ('The owner's call'), and nothing records ODR-V411-02 or ODR-V411-01 as decided.

**Evidence.** requirements-compiled/v4.1.1/STARTING-STATE.md:63; analysis/amendment.json STORAGE-008-O1 disposition; proposals/owner-decision-requests.json ODR-V411-02 impact; tools/owner-freeze.fsx approve (pending only noted) and decide (body text for p.Name <> v4)

**Remediation.** Write 'the package as proposed (option A) does not seek it'. Correct the impact sentence. Make approve refuse while a requirement-conflict request of the same package is pending, or soften the sentence. Make the decide body name p.Dir.

##### RV411-2-9 (note): Docs and schedule details

Three details. README line 3 says twice that ODR-V411-01 is pending. ui cannot verify its loading-shell and visual-identity work in the real entrypoints until integration applies its change requests in T17, after T16. The 5 minutes model integration's work, not the ui round-trip; AMENDMENT's lever 2 and the 20-minute reserve partly cover it. ODR-V411-02 reaches ODR-V41-06's set-aside of REHEARSAL-AGENT-INTERFACE-FREEZE ('F# signatures MUST be authored from the requirements at kickoff') only through the waiver reasons. It does not name that clause, which Amendment B's pre-authored contracts rest on.

**Evidence.** requirements-compiled/v4.1.1/README.md:3; execution-dag.json T16/T17; proposals/owner-decision-requests.json ODR-V41-06 and ODR-V411-02

**Remediation.** Remove the duplicate sentence. Mention the ui verification lag under 'What remains unverified'. Name the REHEARSAL-AGENT-INTERFACE-FREEZE clause in ODR-V411-02's question.

#### Challenges

**1. Does the app start empty?**  
*YES, WITH AN OVERREACHING CLAIM.* Nothing is written to the competition repository before T0. CI and the measurement use throwaway directories. At T0 the order is git init, then conditor init (Echelon scaffold plus 68 requirement documents, 42 of them from Indy-init@2eee2d5), then the generator. All of this is disclosed in STARTING-STATE. However, amendment.json then declares R0.1-O1 'met' on that basis, although the package's own residual risk says an organizer could read the T0 materialization as material prepared before kickoff (RV411-2-1).

**2. Is there hidden implementation in the contracts?**  
*NO (CONTRACTS CLEAN); X17'S LINE SCAN STILL BYPASSABLE.* The contracts are byte-identical to v4.1's. X17 enforces this, and so does CI's diff -r. Only the 7 allowlisted members are code, and each is pinned by SHA-256. Every round-1 bypass form is now detected. The new lexer-level forms are not detected, and the byte-equality path is an unbound amendment field. An end-to-end mutation reached validation PASS, but only with that field edited, and CI diff -r would still catch it (RV411-2-5).

**3. Does the schedule fit 420 (now C = 419)?**  
*YES, ON PAPER, WITH 1 MINUTE OF MARGIN.* C = 354 + 65 = 419. The reserves are 20 + 45. The independently recomputed critical path is 334. Compared with v4.1, only T10 (new in the window, 29) and T17 (15 to 20) changed, and no estimate is shorter. The sensitivity rules are now correct (a named rule replaces the owner rule). Every modelled overrun breaks the window: 424, 434, 448, 454, 475 and 487. The documents state this plainly.

**4. Is the bootstrap time realistic, and is it honestly labelled?**  
*MOSTLY.* The 2 measured minutes are credible: 77 s at most, cold Conditor cache, warm NuGet cache, release binaries. The 27 estimated minutes are plausible but unmeasured. The DAG, ODR-V411-01, AGENT-RULES and README now say '2 measured, 27 estimated'. The X19 title, amendment.json's description, the optimization report and one ODR sentence still say 'measured'. The script post-dates the record (RV411-2-6).

**5. Are the telemetry totals complete and non-duplicated, with the unsplit bucket?**  
*YES.* In the fixture report, pre-event + timed + unsplit sum exactly to the totals for input, output, cacheRead, cacheWrite and total. The session crossing the window start (f0000002) is in 'unsplit (crosses the window boundary)' and is listed in phaseCaveats. Only observed or derived tokens.* values count, missing data is null with its reason, and the two cost kinds are never summed. When there is no finalizedAt, phase still falls back to collectedAt. This is disclosed, and the runbook says to ingest before the window ends.

**6. Does the owner manifest match the files?**  
*YES (MANIFEST); PROSE CHECK WEAK.* package-manifest.json lists 45 files, and every sha256 matches. agent-file-ownership.json gives web/index.html and web/causa.css to integration (kind 'shared') with no contributors, and X02/X18 reject other owners. The AGENT-RULES prose check can be bypassed with ordinary wording (RV411-2-4). Nothing in the generated repository enforces ownership, and this is disclosed.

**7. Is any P0 weakened?**  
*NO.* X10 reads PRESERVED: 520 requirements, 161 P0; 2599 obligations, 613 P0. v4 is unchanged against its tag. The v4.1.1 verification execution plan, state machines and contracts are byte-identical to v4.1's. The issue in RV411-2-1 concerns a 'must' obligation of unstated priority (R0.1-O1) that is claimed met. No text was weakened.

**8. Do the docs contradict the plan?**  
*YES (ONE MAJOR, SEVERAL MINOR).* READINESS A ('no requirement ... claimed met') contradicts amendment.json and AMENDMENT C (R0.1-O1 'met'). READINESS C and D, and amendment.json V411-B, overclaim X17 and X18. optimization-report.md keeps 'organizers allow pre-kickoff scaffolding' and 'non-empty before kickoff'. HACKATHON-BOUNDARIES' published-rule section is recorded as unchanged but was edited. owner-freeze decide tells the agent to edit v4.1.

**9. Is v4.1 unchanged, and is the layering safe?**  
*YES.* git diff 3f5d062..HEAD is empty for v4, v4.1 and kickoff/. The v4.1 manifest sha256 is 779916ab...31736a, equal to APR-V41-OWNER-4, and v4.1 --check passes. X20 binds the amended package and its digest. layerFile pre-checks every overlap and refuses anything except the three merged files. CI and the offline suite layer onto a recorded Conditor 0.10.18 scaffold and build both solutions, and the real measurement layered 177 of 178 files. contractBaseline is not bound to the amended package (RV411-2-5).

**10. Are any approvals fabricated?**  
*NO.* approvals.causa.json holds no ODR-V411 record. The package is CANDIDATE, and README, FREEZE, AMENDMENT, READINESS and the ODR status line all say that ODR-V411-02 and ODR-V411-01 are pending. OD-INDY-18 separates the owner's quoted words from the agent's application and decides nothing about the obligations. The package text does presume option A ('not sought'), and the approve script does not block on the pending ODR-V411-02 (RV411-2-8).


## Round 1: FAIL

Reviewed commit 71b69b175a6923b50f01caed9c5f16f0f012c732. Five major findings, six minor, three notes; none blocking. Every one has a disposition below; round 2 reviews the fixes.

| Finding | Severity | Disposition |
|---|---|---|
| RV411-1-1 | major | Fixed in data and check: X19 scans v4's preflight checks, tasks and requirement obligations, the DAG waivers and the internal rule documents for organizer gates; each of the 20 found has a disposition (amendment.json startingState.organizerReferences). The conflicting ones (ADRP-016-O2, its checklist form, STORAGE-008-O1, PRE-ORG-01/T06, OD-INDY-12's condition, the four waivers) go to the new owner decision ODR-V411-02; no requirement is weakened or claimed met. HACKATHON-BOUNDARIES' 'Continue to verify'/'Until clarified' lines rewritten. |
| RV411-1-2 | major | Fixed: X17 now (1) requires every contract to equal the reviewed v4.1 contract byte for byte, (2) pins each allowed member's text by SHA-256, and (3) scans every line, which must begin as a declaration and carry no code keyword. The offline suite tests the review's bypass forms (13). Documented as a heuristic over lines, with the guarantee resting on (1). |
| RV411-1-3 | major | Fixed: T10 is labelled measured+estimated with measuredMinutes 2 and estimatedMinutes 27; X13/X19 require the split to add up and to match the measurement record. ODR-V411-01, AGENT-RULES, README and the sensitivity texts say 2 measured, 27 estimated. The measurement script is tools/measure-t0-bootstrap.fsx. |
| RV411-1-4 | major | Fixed: T17 (integration, follows T16) carries 5 more minutes for applying T16's entrypoint change requests; later requests fall in RES-INTEGRATION; a sensitivity scenario doubles it. Topology C is now 419 of 420 (margin 1), stated as such. |
| RV411-1-5 | major | Fixed: 'permitted' removed; the source cited is v4's KICKOFF-EXECUTION-PACKET §1, an internal plan. The residual risk names the Conditor scaffold and the requirements import. |
| RV411-1-6 | minor | Disclosed: STARTING-STATE and the residual risk record that the import comes from Indy-init@2eee2d5 and that T01 still pins the authoritative baseline at the kickoff commit. The preset's source commit is Conditor's, not changed here. |
| RV411-1-7 | minor | Fixed: a rule naming a task replaces the owner rule for it; the combined scenario is now 475 for C. |
| RV411-1-8 | minor | Fixed: proposals status, README carried section, schedule description and scenario names updated; the review record is now in the package. |
| RV411-1-9 | minor | Fixed: X18 requires the UI rows to name the entrypoints only in the fixed change-request sentence and rejects any line that lets a UI role edit them directly; the generated repository's lack of a CODEOWNERS check is disclosed. |
| RV411-1-10 | minor | Fixed: a recorded Conditor 0.10.18 scaffold fixture; CI and the offline suite layer onto it, and CI builds both solutions. The package.json merge handles dependencies. |
| RV411-1-11 | minor | Fixed: usage whose session or execution span crosses the window goes to an unsplit bucket; the runbook says to ingest before the window end. |
| RV411-1-12 | note | Not this change: fails on origin/main too; not reproduced in this session's runs. |
| RV411-1-13 | note | Disclosed in AMENDMENT section B. |
| RV411-1-14 | note | OD-INDY-18 now separates the owner's words from the agent's application. The commit subject 9001890 stays (pushed history is not rewritten); it adds script support, records no approval. |

### The reviewer's report (round 1)


- **Package:** `requirements-compiled/v4.1.1`
- **Commit reviewed:** `71b69b175a6923b50f01caed9c5f16f0f012c732` (branch `claude/v411-amendment`). The package content is unchanged since `68828a1`, except the bootstrap measurement, which was re-run with the Registry-pinned release binaries (77.0, 31.8 and 76.4 s), plus the T10 rationale, the AMENDMENT table and the manifest. `v41 --check` passes at `71b69b1`. Amendment D is at `0d068cc`, and `docs/READINESS-V4.1.1.md` at `71b69b1`.
- **Verdict:** **FAIL**. 5 major findings remain, and the rule is FAIL on any blocking or major finding.

#### What was run

- `v41 --check` on v4.1.1 passed (X01–X20 PASS), and on v4.1 also passed (X01–X16). The v4.1 manifest sha256 is `779916ab…31736a`.
- `git diff 3f5d062..HEAD` is empty for `requirements-compiled/v4`, `requirements-compiled/v4.1` and `kickoff/`. The v4.1.1 contracts, layout tests, state machines, verification execution plan and preservation report are byte-identical to v4.1's.
- The offline suite had 1 failure, v4 stage W16, which also fails on origin/main `3f5d062`, so it predates this branch. The v4.1.1 and token-economics groups pass.
- Scratch mutations under `/tmp/claude-0/review411/`:
  - `mut1`: an X17 bypass that compiles, with validation PASS.
  - `mut2`: an X18 prose bypass, with validation PASS.
  - `mut3`: the combined sensitivity scenario recomputed with T10 at exactly 200%.
  - `scripts/x17.fsx`: 11 of 20 implementation forms go undetected.

#### Findings

| ID | Severity | Area | Finding |
|---|---|---|---|
| RV411-1-1 | major | C: organizer-permission gates | Amendment C is only partly done. |
| RV411-1-2 | major | B: X17 hidden-implementation detector | X17 can be bypassed. |
| RV411-1-3 | major | A: bootstrap 'measured' label | The T10 estimate is labelled measured, but 27 of its 29 minutes (93%) are estimates: repo create and push, 15 minutes of agent authoring of shared files including npm install, and the first GitHub Actions run. |
| RV411-1-4 | major | E/schedule: entrypoint work moved without time | Ownership of web/index.html and web/causa.css moved to integration, but the work and the minutes did not move. |
| RV411-1-5 | major | A/C: starting state and residual risk | STARTING-STATE calls `conditor init` 'the permitted event setup'. |
| RV411-1-6 | minor | A: stale requirements import | The measured Conditor 0.10.18 indy-init preset imports Indy-init requirements from commit 2eee2d5, an early commit (PR #7). |
| RV411-1-7 | minor | Schedule: sensitivity computation | SENS-BOOT-200-INT-125 scales T10 twice. |
| RV411-1-8 | minor | Docs: stale v4.1 text in v4.1.1 | Several v4.1 statements were carried into v4.1.1 and now contradict it. |
| RV411-1-9 | minor | E: X18 prose check and enforcement | The manifest side of X18 is sound. |
| RV411-1-10 | minor | A: layered bootstrap coverage | The layering behaves as claimed. |
| RV411-1-11 | minor | D: phase attribution of token figures | Tokens are assigned to a phase by the measurement's collectedAt, which is when the snapshot was ingested. |
| RV411-1-12 | note | Verification: offline suite | The offline suite fails once, at both the reviewed branch (0d068cc tools) and origin/main 3f5d062: 'v4 stage: W16 fails on the committed package (curation testBindings are not exactly the base bindings extended by the v4 strengthened proposal)'. |
| RV411-1-13 | note | B: boundary code | The seven allowlisted members are genuine type-boundary code: a private-representation constructor, accessors, a redacting ToString and one equality test. |
| RV411-1-14 | note | Records: approvals and OD-INDY-18 | No approval was fabricated: kickoff/ is unchanged, ODR-V411-01 is pending, and the manifest reads CANDIDATE. |

##### RV411-1-1 (major): C: organizer-permission gates

Amendment C is only partly done. v4.1.1 removes one edge (T10's preference after T06) and one AGENT-RULES sentence. Several rules that still make the event wait on organizer confirmation stay in force, and nothing in the package or in OD-INDY-18 sets them aside or records how they are handled. (1) OD-INDY-12, an owner decision still in force, says the event may carry only the documented interface manifest, 'never source, and only after the organizer confirms (PRE-ORG-01)'. v4.1.1 writes F# contract source at T0 without that confirmation. (2) v4's preflight check PRE-ORG-01 (task T06, gate G0) says it 'needs an owner or organizer record; never passes automatically'. (3) ADRP-016-O2 is a 'must' obligation: 'Prepared requirements, ... and scripts are subject to organizer approval'. It is verified at gates G0 and G10. (4) The ADRP readiness checklist says 'Obtain organizer rule confirmation'. (5) HACKATHON-BOUNDARIES lines 98-106 still say 'Continue to verify ...' and 'Until clarified ...'. (6) The DAG's waived precedences still rest on ODR-V41-06, whose option A reads 'the organizers allow it'. X19 checks only the T06 preference and the literal phrase 'relies on ODR-V41-06'. The claim 'No task in the execution plan waits on an organizer response' is therefore checked only for the timed DAG.

**Evidence.** docs/OWNER-DECISIONS-2026-10-26.md:29 (OD-INDY-12); requirements-compiled/v4/preflight-manifest.json:5,163 (PRE-ORG-01, 'competitionUse'); requirements-compiled/v4/curation/tasks.json T06 gates [G0]; docs/AGENT-DECISION-REDUCTION-PROFILE.md:53-54,69; requirements-compiled/v4/requirements-inventory.json ADRP-016-O2 modality must; docs/HACKATHON-BOUNDARIES.md:98-106; requirements-compiled/v4.1.1/execution-dag.json:23 (waiver reasons cite ODR-V41-06); tools/requirements-compiler/src/V41Package.fs bootstrapCheck (only checks removedGates T06 and the string 'relies on ODR-V41-06')

**Remediation.** Have the owner record, in OD-INDY-18 or in a new ODR-V411 request, how each of these is handled for November 7. That means OD-INDY-12's organizer condition and its 'never source', PRE-ORG-01/T06/G0, ADRP-016-O2 (state that it stays NOT_RUN at G0/G10 and why), and the HACKATHON-BOUNDARIES 'Continue to verify'/'Until clarified' lines. Then either rebase the DAG waivers on that decision or state that they still rest on ODR-V41-06. Extend X19 to fail while any of them is unaccounted for.

##### RV411-1-2 (major): B: X17 hidden-implementation detector

X17 can be bypassed. It is a line-based regular expression and misses the following, each probed directly against V41Package.contractImplementations: active patterns (let (|A|B|) ...), static let, a do block whose 'do' stands alone on its line, bare module-level expressions, use bindings, an attribute and a let on the same line, backtick-quoted names, a type and its member on one line (type T() = member _.Run x = x * 2), a block comment before let, and a let whose name is on the next line. It also never inspects the body of an allowlisted member, and it ignores 'and'-chained bindings after an allowlisted let, so any amount of code can sit under create or ofReadBack. End-to-end test: an active pattern, a do block and a one-line type with a member were added to IF41-APP and the package was regenerated. ContractCheck.fsproj built with 0 warnings, X17 passed, validation was PASS, and contract-classification.json labelled 06-IF41-APP.fs 'specification: types and signatures only'. Today's contracts are clean: a manual line-by-line inspection found only the 7 allowlisted members, and CI's diff -r against v4.1/contracts pins them. So no hidden implementation exists now. But AMENDMENT-4.1.1.md and READINESS state 'X17 rejects any other implementation member', and that is false.

**Evidence.** tools/requirements-compiler/src/V41Package.fs:1481-1505 (contractImplementations, implRx; scope match takes precedence over implementation match; body skipped by indentation); scratch mutation /tmp/claude-0/review411/mut1 (validation PASS, X17 PASS, ContractCheck 'Build succeeded. 0 Warning(s)'); probe script /tmp/claude-0/review411/scripts/x17.fsx (11 of 20 forms NOT DETECTED); requirements-compiled/v4.1.1/AMENDMENT-4.1.1.md:49; docs/READINESS-V4.1.1.md section C

**Remediation.** Classify from the F# syntax tree (FSharp.Compiler.Service untyped AST: SynModuleDecl.Let/Do/Expr, SynMemberDefn.*, active patterns, operators) or from the compiled assembly, instead of from lines. Pin each allowlisted member's body by digest. Add negative controls for each form above. Until then, say that X17 is a heuristic and that the guarantee rests on byte equality with the reviewed v4.1 contracts.

##### RV411-1-3 (major): A: bootstrap 'measured' label

The T10 estimate is labelled measured, but 27 of its 29 minutes (93%) are estimates: repo create and push, 15 minutes of agent authoring of shared files including npm install, and the first GitHub Actions run. The rationale text gives the split honestly. The machine label (evidence: measured) and the owner-facing texts do not. ODR-V411-01, which is what the owner approves, says 'the bootstrap (T10) at T0 with a measured duration'. AGENT-RULES says 'The duration is measured', README says 'with a measured duration', and the sensitivity scenarios say 'as long as measured'. X13 and X19 require the label 'measured' for the whole adjustment. The re-measurement at 71b69b1 is better evidence: it used the Registry-pinned, SHA-256-checked release binaries and took 77.0, 31.8 and 76.4 s. It still ran in one 4-CPU cloud container, with the NuGet cache warm in every run, no npm or Playwright browser install and no GitHub. No raw logs or measurement script are committed, so no one can reproduce it from the repository.

**Evidence.** requirements-compiled/v4.1.1/analysis/task-adjustments.json:9-11; analysis/bootstrap-measurement.json environment.nugetCache 'warm in every run', notMeasured, t10; proposals/owner-decision-requests.json:157; AGENT-RULES.md:19; README.md:7; analysis/amendment.json:74-78; V41Package.fs bootstrapCheck ('is not labelled measured')

**Remediation.** Label it partly measured. For example, use evidence 'measured+estimated' with measuredMinutes 2 and estimatedMinutes 27, and have X13/X19 require that split. Reword ODR-V411-01, AGENT-RULES duty 1, README and the sensitivity descriptions to say '2 minutes measured, 27 estimated'. Commit the measurement script (F#) and the raw step logs.

##### RV411-1-4 (major): E/schedule: entrypoint work moved without time

Ownership of web/index.html and web/causa.css moved to integration, but the work and the minutes did not move. T16 (ui, 35 min) still covers the WASM loading experience (index.html's loading shell), UI-016's visual identity and the responsive contract. ui can now deliver these only through CR round trips with integration. The DAG and schedule have no estimate, edge or lane time for integration applying those CRs. T17 (critical, slack 0) depends on T16, and topology C has 6 minutes of margin. T10's 15-minute 'remaining shared files' estimate also now claims the 'web entrypoint shells'. The 414-minute figure therefore leaves out a coordination cost that this amendment itself creates.

**Evidence.** requirements-compiled/v4.1.1/execution-dag.json T16 (owner ui, covers doc:docs/WASM-LOADING-EXPERIENCE-REQUIREMENTS.md, UI-016, SCREEN-CONTRACTS§responsive-contract; slack 15) and T17 (dependsOn T16, slack 0); analysis/task-adjustments.json (T16 estimate unchanged at 35); analysis/bootstrap-measurement.json notMeasured[1]; schedule.json C-role-lanes-two-ui slackMinutes 6

**Remediation.** Do one of two things. Either move the entrypoint parts of T16 to an integration task with its own estimate and lane time, and recompute the schedule. Or add CR handling time to T16/T17 and integration's lane, plus a sensitivity scenario for it. At minimum, disclose it as unmodelled in AMENDMENT 'What remains unverified'.

##### RV411-1-5 (major): A/C: starting state and residual risk

STARTING-STATE calls `conditor init` 'the permitted event setup'. No published rule, organizer statement or owner decision is cited for that permission, and the phrase appears nowhere else in the repository. Conditor init does more than scaffold. At T0 it imports 68 requirement documents into the competition repository, 42 of them from Indy-init. HACKATHON-BOUNDARIES itself lists 'whether a separate planning repository may be consumed directly by the blank implementation repo' as unresolved. The residual-risk section names only the contracts, the generator and the layout assertions. It leaves out the Conditor scaffold and the requirements import. Before T0 nothing is written to the competition repository: CI and the measurements use throwaway directories. That part of the claim holds.

**Evidence.** requirements-compiled/v4.1.1/STARTING-STATE.md:43, :55-62; AMENDMENT-4.1.1.md:29; docs/HACKATHON-BOUNDARIES.md:104; analysis/amendment.json startingState.residualRisk

**Remediation.** Drop 'permitted', or cite its source (for example v4's KICKOFF-EXECUTION-PACKET §1, which is an internal plan and not an organizer permission). Add the Conditor scaffold and the requirement import from the planning repository to the residual-risk text in STARTING-STATE and amendment.json.

##### RV411-1-6 (minor): A: stale requirements import

The measured Conditor 0.10.18 indy-init preset imports Indy-init requirements from commit 2eee2d5, an early commit (PR #7). It does not import from the v4 baseline commit. AMENDMENT reports this as a success, not a risk. The competition repository's Praxis requirements at T0 may therefore differ from the frozen v4 baseline.

**Evidence.** requirements-compiled/v4.1.1/AMENDMENT-4.1.1.md:29; git log -1 2eee2d5 = 'Merge pull request #7: require URL-addressable state ...'

**Remediation.** Record which baseline the imported documents represent and whether they conflict with v4 (T01 pins the baseline at the kickoff commit). Pin the preset's source commit, or add a step that replaces the import.

##### RV411-1-7 (minor): Schedule: sensitivity computation

SENS-BOOT-200-INT-125 scales T10 twice. Both rules match it, so it gets 200% x 125% = 250% (73 minutes, not 58). The description says 'every other integration task overruns by a quarter', and AMENDMENT's table labels the row 'Bootstrap x2 and integration x1.25 = 484'. With T10 at exactly 200% the scheduler gives C = 469 (margin -49), A = 539 and B = 843. The error runs in the conservative direction. The other four scenarios check out: 429, 443, 448, 480. The reserves are 20 + 45 = 65 in every topology, and no estimate was shortened. Every task except T10 has the same estimate as in v4.1.

**Evidence.** tools/requirements-compiler/src/V41Package.fs:694 (fold multiplies every matching rule); analysis/amendment.json:78; AMENDMENT-4.1.1.md:90; scratch recomputation /tmp/claude-0/review411/mut3 (T10 160% x 125% = 200%: C 469, A 539, B 843)

**Remediation.** Apply task-specific rules in place of owner rules, not on top of them, or exclude the named tasks from the owner rule. Regenerate the schedule and correct the table.

##### RV411-1-8 (minor): Docs: stale v4.1 text in v4.1.1

Several v4.1 statements were carried into v4.1.1 and now contradict it. (1) proposals/owner-decision-requests.json status reads 'Pending: ODR-V41-01, the package approval', but v4.1 is approved and ODR-V411-01 is the one pending. (2) README's carried half says 'only ODR-V41-01 is pending', 'checks X01–X16', 'ODR-V41-01..09' and 'Validation X01–X16'. (3) README CI step 5 says the bootstrap runs 'onto the repository conditor init created ... into a git-initialised empty directory', which contradicts itself. CI uses an empty directory, so the Conditor layering path never runs in CI. (4) schedule.json's description says 'The v4.1 timed schedule ... All minutes are estimates', while the DAG labels T10 measured. Its scenarios are still named 'v4.1', and the 'v4' and 'v4.1 estimates only' scenarios leave T10 out entirely, because the phase move applies only with the dependency changes. They cannot be compared with the 414 figure. (5) optimization-report keeps 'The T10 bootstrap makes the application repository non-empty before kickoff' and 'the organizers allow pre-kickoff scaffolding' under a general disclaimer. (6) FREEZE.md, README and READINESS point to independent-review.md and analysis/independent-review.json, which do not exist yet. ODR-V411-01's record needs the review JSON's digest.

**Evidence.** requirements-compiled/v4.1.1/proposals/owner-decision-requests.json:3; README.md:22,47,69,75; schedule.json:3 and scenarios; optimization-report.md:39-41; FREEZE.md:3,11-12; docs/READINESS-V4.1.1.md (last line)

**Remediation.** Update the ODR status line, README's carried section, the schedule description and the scenario names for v4.1.1. Add a scenario that includes T10 for v4-estimates comparability. Mark or remove the superseded organizer sentences. Add the review record before asking for ODR-V411-01.

##### RV411-1-9 (minor): E: X18 prose check and enforcement

The manifest side of X18 is sound. It rejects ui or ui-2 as owner or contributor, and X02 rejects them too; the negative tests cover this. The prose side is a substring heuristic. A roles-table row is accepted if it mentions 'change request' anywhere, and text outside the table is never read. In a scratch copy, the ui row was rewritten to 'It owns and edits `web/index.html` and `web/causa.css` directly; no change request is needed', and duty 4 to 'ui edits `web/index.html` directly'. X18 and X02 both passed, and validation was PASS. AMENDMENT and READINESS say the negative tests 'reject ... prose that gives the files to ui'. That holds only for the exact phrasing tested. Nothing in the generated application repository stops a UI agent from editing the entrypoints either: there is no CODEOWNERS file and no CI ownership check. Only the agent rules do.

**Evidence.** tools/requirements-compiler/src/V41Package.fs:1590; scratch mutation /tmp/claude-0/review411/mut2 (X18 PASS); AMENDMENT-4.1.1.md:67; docs/READINESS-V4.1.1.md section D

**Remediation.** Make the prose check positive: the ui and ui-2 rows must not name the entrypoint paths at all, or must match one fixed sentence. Scan the whole of AGENT-RULES. Optionally have the bootstrap write a CI check that compares changed paths with the manifest owner of the PR's lane.

##### RV411-1-10 (minor): A: layered bootstrap coverage

The layering behaves as claimed. Every overlap is pre-checked before anything is written, any file other than package.json, Directory.Build.props and .gitignore that differs is refused, and the run then aborts with nothing written. v4.1 output is untouched: without an amendment the old empty-directory path is unchanged, v4.1 --check passes, and its manifest digest is 779916ab...31736a. Three gaps remain. CI never exercises the layering on a real Conditor repository; the v4.1.1 CI step bootstraps into an empty git directory. Detecting a Conditor repository relies only on the presence of conditor.json. The package.json merge ignores the non-dev 'dependencies' section, so a locked package that Conditor lists there ends up in both sections.

**Evidence.** .github/workflows/preflight-tooling.yml (v4.1.1 bootstrap step uses git init into $RUNNER_TEMP/app411); tools/requirements-compiler/src/Program.fs:1262-1280; V41Package.fs layerFile; tools/requirements-compiler/tests/V411Tests.fs layering (synthetic inputs only)

**Remediation.** Add a CI or offline fixture holding a recorded Conditor scaffold (conditor.json, App.slnx, Directory.Build.props, package.json) and bootstrap onto it. Check 'dependencies' in the package.json merge.

##### RV411-1-11 (minor): D: phase attribution of token figures

Tokens are assigned to a phase by the measurement's collectedAt, which is when the snapshot was ingested. A session that crosses T0 is counted entirely in the phase of its ingest. In the fixture, f0000002's session started at 11:50, before the 12:00 window start, and all of its tokens are in 'timed'. phaseCaveats lists the crossing, but the byPhase figure carries no flag. The runbook says to ingest the coordinator's main session 'once, at completion'. If that ingest happens after the deadline (R7's main agent ran 2 minutes past it), the coordinator's entire timed usage is reported as post-event. In every other respect D is sound. Tokens come only from observed or derived tokens.* metrics in the four buckets. The TTL split metrics are separate and are not added. Missing buckets are null and unavailable with Praxis's reason, and groups are partial and a lower bound. Estimated values are excluded and listed. Re-ingested snapshots are counted once. Provider-reported cost and price-table cost are never summed. Wall time, commits and lines never become tokens, and the offline suite checks this.

**Evidence.** tools/requirements-compiler/src/TokenEconomics.fs:273-294 (rowsOf phase by collectedAt), :483-500 (phaseCaveats); docs/TOKEN-ECONOMICS.md:41,122; tests/fixtures/token-economics/expected/token-economics.json byPhase.timed includes EXE-...f0000002

**Remediation.** Put a session whose span crosses a window boundary into an 'unsplit' phase bucket, or mark the byPhase figures that contain one. Tell operators to ingest before window end and to use reliability-report --transcripts for the split.

##### RV411-1-12 (note): Verification: offline suite

The offline suite fails once, at both the reviewed branch (0d068cc tools) and origin/main 3f5d062: 'v4 stage: W16 fails on the committed package (curation testBindings are not exactly the base bindings extended by the v4 strengthened proposal)'. The failure is pre-existing and not caused by this branch. Every v4.1, v4.1.1 and token-economics group passes. Readers should still know that 'the offline suite passes' is not literally true on main or on this branch.

**Evidence.** dotnet run --project tools/requirements-compiler/tests -c Release -- --offline --source . : '1 failure(s)' in scratch clones at the branch and at 3f5d062

**Remediation.** Track W16 separately. Do not cite the offline suite as fully green in the approval material.

##### RV411-1-13 (note): B: boundary code

The seven allowlisted members are genuine type-boundary code: a private-representation constructor, accessors, a redacting ToString and one equality test. They still fix two behaviour decisions before kickoff, and both are disclosed. authorization hard-codes the 'Bearer' scheme. ofReadBack defines a read-back match as exact structural equality of FileSet, with no line-ending or ordering normalisation. 99-Usage.fs is never materialised; the offline suite checks this.

**Evidence.** requirements-compiled/v4.1.1/contracts/03-IF41-WIRE.fs:39-43; contracts/05-IF41-SAVE.fs:19-24

**Remediation.** State both decisions explicitly in contract-classification rationale.

##### RV411-1-14 (note): Records: approvals and OD-INDY-18

No approval was fabricated: kickoff/ is unchanged, ODR-V411-01 is pending, and the manifest reads CANDIDATE. OD-INDY-18 attributes three quotations to the owner's assignment and makes no claim about what organizers said. It says it takes effect only with ODR-V411-01. Three wording problems remain. Commit 9001890's subject, 'owner-freeze: approve and tag v4.1.1 (ODR-V411-01)', reads like an approval but only extends the script. OD-INDY-18's 'This covers prewritten specifications, reusable development tools and generated scaffolding' interprets the owner's words; it does not quote them. 'the bootstrap generator [is] materialized at T0' is imprecise, because the generator is run at T0, not materialised. The owner's own APR-V41-OWNER-1 organizer_confirmation reads 'I approved it.', yet v4.1.1's carried optimization report still says the owner stated that the organizers allow pre-kickoff scaffolding. D's documents and code sit outside the v4.1.1 package manifest, so ODR-V411-01 does not pin them.

**Evidence.** git show 9001890 (only tools/owner-freeze.fsx); docs/OWNER-DECISIONS-2026-11-01.md OD-INDY-18 row; kickoff/rehearsal-007/approvals.causa.json APR-V41-OWNER-1; requirements-compiled/v4.1.1/optimization-report.md:41; package-manifest.json files (no docs/TOKEN-ECONOMICS.md)

**Remediation.** Reword the OD-INDY-18 sentence as the recorder's reading, or confirm it with the owner. Mark the optimization-report sentence as superseded. Consider adding the D spec's digest to the amendment record.

#### Challenges

**1. Does the application truly start empty, and is Conditor's init at T0 'permitted event setup'?**  
*PARTLY.* Before T0 nothing is written to the competition repository: CI and the measurements use throwaway directories, and the bootstrap runs only at T0. Calling conditor init 'permitted' is unsourced. At T0 it imports 42 Indy-init requirement documents from an old commit, and the residual-risk statement leaves that out (RV411-1-5, RV411-1-6).

**2. Do the prewritten contracts hide implementation; can X17 be bypassed; is the allowlisted code boundary code?**  
*CONTRACTS CLEAN; X17 BYPASSABLE.* Manual inspection finds only the 7 allowlisted members, and CI pins the contracts byte-equal to v4.1. X17 misses active patterns, static let, standalone do, bare expressions, use, attribute+let, backticks, one-line type+member and multi-line let headers, and it never inspects allowlisted bodies. This was shown end to end with validation PASS (RV411-1-2). The boundary code is genuine but fixes two small behaviour decisions (RV411-1-13).

**3. Does the schedule fit 420 (C 414), reserves preserved, no estimate shortened, sensitivity correct?**  
*FITS ON PAPER; ONE SCENARIO MISCOMPUTED; CR COST UNMODELLED.* C = 349 + 65 = 414, and the reserves are 20 + 45. Only T10 changed estimate (29), and no other estimate is shorter than in v4.1. Four scenarios are correct. The combined scenario scales T10 by 250%, not 200% (correct value 469, not 484; RV411-1-7). The 6-minute margin leaves out the change-request handling that E creates (RV411-1-4).

**4. Is 2 min measured + 27 min estimated realistic, and is 'measured' honest?**  
*LABEL NOT HONEST ENOUGH.* The mechanical part (72-76 s cold) is credibly measured, with a warm NuGet cache. The 15 minutes of agent authoring and the 10-minute first CI are plausible but unmeasured guesses. Labelling the whole 29 minutes 'measured', including in the owner's approval question, overstates the evidence (RV411-1-3).

**5. Amendment D: totals complete and non-duplicated, missing data unavailable, nothing inferred, timed vs pre-event, provider vs price-table cost?**  
*PASS WITH A MINOR ISSUE.* Present at 0d068cc and reviewed. Tokens come only from tokens.* observed or derived measurements. Missing data is null and unavailable with its reason, and partial groups are lower bounds. Estimated values are excluded, and snapshots are counted once. The two cost kinds are never summed, and there is no inference from time, commits, lines or characters. The fixture and the tests pass. The phase split uses ingest time, so a session crossing T0, or ingested after the deadline, is put wholly in one phase (RV411-1-11).

**6. Does the ownership manifest match the files; can a UI role still edit the entrypoints; do negative tests reject unauthorized ownership changes?**  
*MANIFEST PASS; PROSE CHECK WEAK; NO RUNTIME ENFORCEMENT.* The manifest gives both files to integration with no contributors, and X02 and X18 negative tests reject ui or ui-2 owners and contributors. The prose check is defeated by a row that mentions 'change request'. Nothing in the generated repository stops a UI agent's edit (RV411-1-9).

**7. Is any P0 requirement, obligation, test binding, state machine or contract weakened?**  
*NO TEXT CHANGE; ONE OBLIGATION LEFT UNADDRESSED.* Byte comparison: contracts/*, layout-tests/*, state-machine-contracts.json, verification-execution-plan.json and preservation-report.json are identical to v4.1, and X10 passes (520/161/2599/613). ADRP-016-O2 (a 'must' obligation of unstated priority: organizer approval of prepared material) is not reworded, but the plan now proceeds without it and states no disposition for it (RV411-1-1).

**8. Does any documentation contradict the execution plan?**  
*YES (MINOR).* The ODR status still says ODR-V41-01 is pending. README's carried v4.1 half (X01–X16, 'only ODR-V41-01 pending', CI step 5) contradicts v4.1.1. The schedule description says 'v4.1' and 'all estimates'. Its scenarios leave T10 out. HACKATHON-BOUNDARIES still says 'Continue to verify'/'Until clarified'. The optimization report carries 'non-empty before kickoff' and 'organizers allow' under a disclaimer. Review files are referenced but missing (RV411-1-8, RV411-1-1). No v4.1.1 text still places T10 before T0 as a v4.1.1 instruction, or gives web/index.html to ui.

**9. Is v4.1 unchanged; does the layered bootstrap refuse to overwrite?**  
*PASS.* git diff 3f5d062..HEAD -- requirements-compiled/v4 requirements-compiled/v4.1 kickoff is empty. v4.1 --check passes on the new compiler, and its manifest sha256 is 779916ab...31736a (= APR-V41-OWNER-4). The layering pre-validates every file and refuses any differing file except the three merged ones, writing nothing. It is not exercised by CI on a real Conditor scaffold (RV411-1-10).

**10. Is OD-INDY-18 faithful; is anything recorded as an approval the owner did not give?**  
*PASS WITH NOTES.* No approval record was written, and the package is CANDIDATE with ODR-V411-01 pending. OD-INDY-18 quotes the owner's assignment and claims nothing about organizers. One sentence interprets scope, and commit 9001890's subject reads like an approval (RV411-1-14). OD-INDY-18 does not supersede OD-INDY-12's organizer condition, which conflicts with it (RV411-1-1).
