# Independent review of v4.1.1

The reviews are independent: each was run by an agent that did not write the package, read-only, with instructions to challenge it. A failed review is recorded as failed. [analysis/independent-review.json](analysis/independent-review.json) holds the latest round, with each finding's disposition.

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

