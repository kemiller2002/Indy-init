# Independent review of package v4.1

| | |
|---|---|
| Reviewer | A fresh, read-only Claude Code subagent. It received the sources, the brief and the compiler, but not the author's conclusions |
| Record | [analysis/independent-review.json](analysis/independent-review.json): every finding, its remediation and its status |
| Caveat | The reviewer and the author of this record are both agents. The record is not an authenticated attestation; the owner's GitHub review of the pull request is the authentication |

## Rounds

| Round | Reviewed | Verdict | Findings |
|---|---|---|---|
| 1 | `88d09ea` | FAIL | 1 critical, 7 major, 8 minor |
| 2 | `7ec98d9` | FAIL | 0 critical, 5 major, 6 minor. All round-1 findings fixed or partly fixed; the partly fixed ones are completed by the round-2 remediation |
| 3 | `7e189f9` | FAIL | 0 critical, 3 major, 6 minor. Round-2 findings fixed except 2-1, 2-2 and 2-6 (partly), completed by the round-3 remediation. Owner adjustments: bootstrap largely achieved; layout tests and type-shape reconciliation partly |
| 4 | `5c6adda` | FAIL | 0 critical, 1 major, 6 minor. Round-3 findings fixed except 3-1 (partly), completed by the round-4 remediation. Owner adjustments: type-shape reconciliation achieved; bootstrap achieved; layout tests partly |

## Round 1 findings and remediation

| Id | Severity | Finding | Remediation |
|---|---|---|---|
| RV41-1-1 | critical | The new DAG dropped the invariant-tests-first order for the save machine (T12 before T19) | T19 depends on T12 again; X13 now requires every v4 transitive ordering unless an owner-decided waiver names it; the scope test asserts T12 before T19 |
| RV41-1-2 | major | Integration checkpoint 1 (T17) could run before the kernel; topology A fitted only because of it | T17 depends on T13 again; A no longer fits (440) and the documents say so |
| RV41-1-3 | major | ODR-V41-03 and the report mixed ordering and estimate effects and omitted dropped orderings | schedule.json scenarios separate them; ODR-V41-03 (orderings, with the six waived orderings listed) and ODR-V41-04 (estimates) are separate requests |
| RV41-1-4 | major | IF41-SAVE did not match SM-SAVE and SM-CONFLICT (input union, conflict states, 'inert' wording) | SaveInput, ConflictView/ConflictInput, LoadState/LoadInput, ConnectionInput and NetworkInput now equal the machines' events and states; SaveMachine.Interpret produces reply events; X05 checks every machine against one interface's unions; the concurrency text follows the table |
| RV41-1-5 | major | Contract and state-machine tests had no owner, compile entry, suite or task; producer tests owned by non-producers | Every contract, state-machine and unit test is generated into an owned file (producer side by the producer, consumer side by each consumer, machine tests by the machine owner) and a suite; X04 checks producer and consumer ownership; T14 (15 min) and T19 (60 min) budget the tests |
| RV41-1-6 | major | Ownership incoherent across topologies; test files assigned by first task id gave integration most PERSIST tests; AGENT-RULES disagreed | ui-2 files carry ownerByTopology (ui in A and B); test files go to the first covering implementation role (domain, persistence, ui), integration only when no such task covers them; AGENT-RULES defers to the manifest; residual integration load recorded as R41-06 |
| RV41-1-7 | major | X02, X06, X07, X08, X11, X12 and X13 passed inputs they claimed to reject | Each tightened (per-class bounds, status-only evidence rejected, all-pairs evidence comparison, test-requirement match, fixed integration-exclusive list, foreign properties/classes/colours rejected, structured unavailable policy, broader provenance pattern over documents too, reviewed guard partitions, precedence and estimate-floor rules); every reported bypass is a negative control in V41Tests.fs |
| RV41-1-8 | major | The 163-to-9 setup saving contradicted the suites' own fixtures and justified the T31 cut | Saving claim withdrawn; suites report fixture set-ups by scope; T31 back to 45; runner filters select by namespace, the same rule as membership |
| RV41-1-9 | minor | The review record was referenced but missing | This record and analysis/independent-review.json |
| RV41-1-10 | minor | The manifest did not digest the documents; --check ignored stray files | Documents and the decision requests are digested; --check fails on files the compiler neither generates nor reads |
| RV41-1-11 | minor | Error-matrix gaps: truncated-list budget, primary/secondary precedence, PERSIST-016 online reads, timeout not required | Paging counts against the three reads; precedence rule added; NETWORK-AFTER states the online-change read and 4-in-60-s cap; X06 requires TimedOut detection |
| RV41-1-12 | minor | Weak types and a wrong versioning promise; raw credential in Msg | HttpMethod, Politeness, ProviderAnswer and conflict-input unions replace strings; every union change is declared breaking; the credential is created once as the opaque Credential type |
| RV41-1-13 | minor | FL-06 hard-codes #E9A23B, not a Forma value | The literal is cited with its UI-016 source (X07 accepts a colour only with a source); ODR-V41-05 asks the owner |
| RV41-1-14 | minor | Who performs T14 was contradictory | Each producer commits its own contract block; integration only builds (AGENT-RULES duty 2) |
| RV41-1-15 | minor | Test projects declared no references; Causa.Domain.Tests empty | testProjects declare references (X03 checks them, acyclic); required unit tests generate their files |
| RV41-1-16 | minor | The v4 manifest rewrite invalidates owner records naming its old SHA | Stated in FREEZE.md |

## Round 2 findings and remediation

| Id | Severity | Finding | Remediation |
|---|---|---|---|
| RV41-2-1 | major | Pre-written contracts and the kernel waivers contradict REHEARSAL-AGENT-INTERFACE-FREEZE (author at kickoff), KICKOFF-EXECUTION-PACKET §4 and HACKATHON-BOUNDARIES (confirm organizers); the owner was not asked | The owner was asked in session and stated that the organizers allow pre-kickoff scaffolding; ODR-V41-06 records it for the owner's own record and names the clauses set aside; ODR-V41-03 and the waivers cite it; the report no longer says v4.1 changes nothing but states the procedural clauses |
| RV41-2-2 | major | The frozen save machine could not run multi-request save flows; MutationSent illegal everywhere | The session records what it awaits (Awaiting: mutation answer, read-back, fresh head, file comparison, reconciliation head/pages/files); Interpret returns MachineEvent or NextRequest, so intermediate reads need no new state; X12 requires every unused event to be listed with a decision, and MutationSent goes to the owner (ODR-V41-08) |
| RV41-2-3 | major | Interface types dropped commands, refusal reasons, per-investigation load state and load failure causes | Unsaved changes and reapply carry CommandEnvelope; RefusedCommand carries the engine's reason; Model.Loads is keyed by investigation; LoadFailure distinguishes unverifiable files from provider failures |
| RV41-2-4 | major | Test projects could not reach the stateful fake and recordings; helper order wrong; X03 ignored helpers | New Causa.TestSupport project (Recordings before StatefulFake) referenced by every test project that needs it; X03 checks helper dependencies; the bootstrap builds it |
| RV41-2-5 | major | X02, X06, X07 and X13 still passed inputs their titles reject | X02 covers Directory.* build files, NuGet configuration, runsettings, tests/fixtures/ and tests/browser/support/; X06 rejects reason phrases as evidence, requires distinguishing evidence per class and bounds of the class's own kind; X07 rejects rgb/hsl colours, unhyphenated classes and foreign property families, and accepts a literal only if the cited document contains it; X13 requires waivers to name an existing decision request; each probe is a negative control |
| RV41-2-6 | minor | Contract and machine test work not budgeted; transition count wrong | T14 and T16 keep v4's estimates (contract tests; SM-CONNECTION/NETWORK tests); T19 (75 transitions) and T20 (SM-LOAD) carry the machine tests; the counts are corrected |
| RV41-2-7 | minor | Only a lane-free critical path was reported | Every topology reports its binding chain; the report distinguishes the dependency critical path (290) from topology C's makespan (320) |
| RV41-2-8 | minor | Confirmation constructible from any file in the persistence namespace | Confirmation's representation is private to its module, with a byte-comparing constructor; CI compiles a forging file and requires FS1093 |
| RV41-2-9 | minor | web/ funnelled to integration; R41-06 understated the integration test load | web/index.html and web/causa.css are ui's (T16); R41-06 states the actual split |
| RV41-2-10 | minor | The T35 waiver reason contradicted the task | T35 depends on T12 again (invariant review kept); only T13 before T35 is waived, with a corrected reason |
| RV41-2-11 | minor | Interface specs had no explicit operations, success or failure | Each interface lists operations with signature, success and failure; X04 requires them, checks each name is in the contract, and rejects empty concurrency or versioning rules |

## Round 3 findings and remediation

| Id | Severity | Finding | Remediation |
|---|---|---|---|
| RV41-3-1 | major | Layout assertions tested less than the FL rules state and were not wired into the event build | New assertions for each stated invariant: scroll regions accessible, line length, focus order, skip link first, dialog focus (open, trap, Escape, return), non-text contrast, forced colours, amber with a label, indicator composition, fault states what was preserved, navigation collapse; focusVisible requires a non-transparent ring of the focus-ring width that the blurred element lacks; textNotClipped detects vertical clipping and line-clamp; onlyFormaTokens reads document.styleSheets and style attributes. The reviewer's 12 defective pages are all detected except text over a gradient, which FL-06 assigns to the plan's axe run. The bootstrap writes the module to tests/browser/support/layout-assertions.mjs (integration). X15 requires every rule critical and executable, checks fixtures and composed patterns and the event-build copy, and has negative controls |
| RV41-3-2 | major | IF41-SAVE could not drive automatic reconciliation reads, load reads or conflict reads; request bookkeeping unspecified | ReconcileDue sends automatic reconciliation reads on the ScheduleReconcileRead timer (TimerKey); InterpretLoad and InterpretConflict with awaited-read state (LoadAwaiting, ReadingRemote of RequestId); RequestContext carries the tab-unique NextId (the engine advances it, at most one request per operation) and MutationInFlightElsewhere for T-S04/T-S53; operations record Outstanding; producer contract tests named for each |
| RV41-3-3 | major | No frozen type kept the repository and credential after Connect | IF41-WIRE ConnectionContext (repository, branch, opaque credential) kept in Model.Context and passed in RequestContext; persistence adds the Authorization header with Credential.authorization; producer test CredentialOnlyInAuthorizationHeader |
| RV41-3-4 | minor | The §46 note asserted a dependency direction v4.1 inverts | The note and project-structure.json state App -> Persistence -> Domain and mark §46's direction superseded; the R7 validator passes |
| RV41-3-5 | minor | ODR-V41-06 omitted the published organizer rules | ODR-V41-06 quotes 'the repository starts empty', 'code starts at kickoff' and 'keep competition application source code out of it', and says the T10 bootstrap makes the repository non-empty before kickoff |
| RV41-3-6 | minor | Credential readable from any Causa.App file; Saved after a load needed a forged Confirmation | Credential is a module type with private representation (CI negative ReadCredential.fs refused with FS1093); Saved carries SavedBasis (Confirmed of Confirmation.T, or VerifiedAt for a load or confirmed discard, subject to ODR-V4-08) |
| RV41-3-7 | minor | X02 accepted contributors on integration files and a dropped boundary script; X04 trivial outcomes; X15 without controls; CI step name | X02 rejects contributors on integration-exclusive files and requires the essential shared files in the manifest; X04 requires stated outcomes; X15 controls added; the step names X01-X15 |
| RV41-3-8 | minor | The bootstrap's scope was described more broadly than it is | README, the report and AGENT-RULES state it is the .NET skeleton (plus the layout module); package references, integration's other shared files and test-framework wiring come at T10 (ODR-V41-07) |
| RV41-3-9 | minor | T19 and T22 estimate basis | T22 restored to 70 (dependency critical path now 300; the experimental target is reported as not met); T19's rationale states why 60 minutes holds and that it is off the critical path |

## Round 4 findings and remediation

| Id | Severity | Finding | Remediation |
|---|---|---|---|
| RV41-4-1 | major | The layout assertions missed common violations of their invariants | Overflow checks include html and body and any content past the viewport; clipping compares each text range with its clipping ancestors; line length measures running text anywhere; dialogFocus fails when the invoker does not open the dialog (only a declared show-modal command is carried out in a browser without invoker commands); indicatorComposed is scoped to the header carrying the lozenge and checks order; faultStatesPreserved rejects statements that work was lost; contrast accounts for ancestor opacity and waits for entry animations; also skip link visible on focus, ring contrast, hidden scroll bars, late animations, empty navs, clickable non-controls, colour-only lozenges, unreadable linked stylesheets. 46 broken pages; the reviewer's 18 defect pages are all caught except two that do not violate the rule at that viewport or with Forma's ring |
| RV41-4-2 | minor | X15 did not enforce per-invariant coverage | Each rule lists its invariants and the assertions that test each; X15 fails if an invariant's assertion is not run or a run assertion tests no invariant; negative controls added |
| RV41-4-3 | minor | No frozen contract drove SM-CONNECTION | IF41-ERRORS Connector (ConnectRequest, InterpretConnect -> ConnectResult; write access only from stated permissions, EXT-DATA-005); ConnectionState Connecting of RequestId; producer test WriteAccessOnlyFromPermissions |
| RV41-4-4 | minor | --bootstrap refused a git-initialised repository | A .git directory is allowed; CI now runs git init before the bootstrap |
| RV41-4-5 | minor | The report summarised ODR-V41-06 as two clauses | The report names all three groups, including the published event rules |
| RV41-4-6 | minor | Duplicate union-case names caused type errors | The machines' input unions and NetworkState require qualified access; contracts/99-Usage.fs is a positive control CI compiles |
| RV41-4-7 | minor | .gitignore unowned and not bootstrapped; onlyFormaTokens exempted any forma*.css | `.gitignore` is integration's (X02) and written by the bootstrap; only Forma's own stylesheet (default /dist/all.css, configurable) is exempt |
