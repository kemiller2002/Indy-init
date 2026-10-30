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
