# Floor Judging Architecture Defense

Status: preparation guide for live technical defense.

Judges are expected to ask why decisions were made. This document records the intended answer and evidence, not a memorized script.

## 1. Why this app?

Answer structure:

- Root-cause investigation is a natural stress test for AI because plausible narratives are easy and defensible conclusions are hard.
- The product therefore demonstrates whether explicit state/evidence/policy can improve AI-assisted reasoning.
- It remains reusable beyond software incidents and becomes an EDF research instrument later.

Evidence:

- generic semantic model
- domain pack contract
- EDF follow-on
- multiple scenario types

## 2. Why F#?

Answer structure:

- domain semantics are the hard part
- discriminated unions/typed IDs make illegal states/transitions easier to prevent
- pure decide/evolve functions enable deterministic replay/property testing
- extension boundary avoids closed-world lock-in

Evidence:

- type shapes
- compile-time state distinctions
- domain project dependency isolation
- generated state tests

## 3. Why event/history architecture?

Answer structure:

- investigation meaning changes over time
- time travel and "what changed my mind?" require historical state
- stale/offline clients need semantic revalidation
- policy/version history must remain explainable
- append/supersede is safer than overwriting narrative

Evidence:

- time travel
- semantic diff
- replay equivalence
- correction events

## 4. Why GitHub as data store?

Answer structure:

- event constraint and iPad-first deployment favor an auditable remote provider already available
- GitHub gives provenance/history and deployment simplicity
- domain does not depend on GitHub
- provider abstraction enables future service/database storage

Evidence:

- persistence contract
- provider version token separate from AggregateVersion
- cross-provider equivalence test

## 5. Why not a normal database first?

Answer structure:

- initial deployment does not require one
- GitHub reduces build-day infrastructure while retaining auditability
- domain/persistence abstraction prevents lock-in
- a hosted database can be added after event without changing semantics

Risk:
GitHub is not ideal for high-frequency large-scale data.

Evidence:
provider capability contract and scale constraints.

## 6. Why WebAssembly/Limen?

Answer structure:

- keep deterministic domain logic local
- support offline/time travel/diff/hash/graph work
- reduce JS surface
- minimize AI disclosure/cost
- reuse same typed logic where practical

Evidence:

- network-off demo
- local hash/diff
- local semantic projection

## 7. Why not let the AI control state directly?

Answer structure:

- models are probabilistic and may hallucinate or overstate certainty
- AI generates proposals
- deterministic domain rules decide legality
- tools are scoped by host-issued capabilities

Evidence:

- chat says "root cause confirmed" while command remains blocked
- malformed proposal rejected
- tool escalation denied

## 8. Why multiple agents?

Answer structure:

- not for voting
- different roles expose different reasoning failures
- blind first pass reduces artificial consensus
- dependencies are recorded so copied reasoning is not treated independent
- research harness can measure whether added cost is worthwhile

Evidence:

- blind panel
- disagreement
- discriminating action
- cost/latency

## 9. Why so much provenance?

Answer structure:

- AI-assisted reasoning is only useful if users can inspect what changed and why
- provenance enables revalidation, stale proposal detection, time travel, reproduction, and audit
- same principle applies to software factory evidence

Evidence:

- evidence drill-down
- proposal source
- proof bundle

## 10. How do you avoid overengineering?

Answer structure:

- CoreStable is deliberately small
- experimental concepts stay extensions
- many advanced features are projections over the same event/state model
- design decision register classifies reversible/experimental/deferred choices

Evidence:

- semantic evolution doc
- open questions
- domain pack extensions
- derived Leading posture

## 11. What is actually generated?

Potential generated artifacts:

- structural transition tests
- capability metadata
- AI schemas
- event codec registry
- state diagrams
- contract manifest/diff
- Aegis metadata

Important:
Only claim what is implemented by judging time.

## 12. How do you know the tests are meaningful?

Answer structure:

- not just example tests
- state-space enumeration
- property tests
- mutation testing
- offline/concurrency chaos
- prompt-injection corpus
- accessibility state matrix

Strong proof:
show a deliberate semantic mutation being killed.

## 13. What if the model provider is down?

Answer structure:

- domain/persistence/manual capture still work
- local WASM analysis still works
- pending work remains local
- optional provider fallback
- AI outage is a degraded capability, not loss of truth

Evidence:
manual/offline demo or test.

## 14. What if GitHub is down?

Answer structure:

- local durable commands/state remain
- remote status becomes Pending/Offline
- reconnect triggers semantic revalidation
- no fake synced state

Evidence:
disconnect/reconnect.

## 15. What if two users conflict?

Answer structure:

- optimistic versioning
- commands carry expected aggregate version
- stale commands revalidated
- semantic conflicts are not JSON merges
- independent additions may commute where proven safe

Evidence:
concurrency simulation.

## 16. How is this secure?

Answer structure:

- least privilege
- scoped tool capability
- secrets isolated
- evidence untrusted
- prompt-injection tests
- classification-aware AI disclosure
- no arbitrary tool/write access
- CSP/sanitization

Evidence:
tool denial + injection test + secrets scan.

## 17. How is this observable?

Answer structure:

- Aegis structured faults
- Praxis code/process metrics
- ROS work state
- AI/tool cost/latency
- event/proposal provenance
- Factory View

Evidence:
click from runtime fault to diagnostic/proof.

## 18. How would this scale?

Answer structure:

- bounded AI context
- snapshots + incremental replay
- local filtering
- derived indexes
- provider abstraction
- graph views are projections

Be explicit:
GitHub storage is first-release provider, not the scale endpoint.

## 19. How do you control AI cost?

Answer structure:

- local deterministic analytics
- bounded context
- proposal cache
- role-specific models
- autonomous budgets
- planner budgets
- track cost per run/task

Evidence:
multi-agent cost view.

## 20. What is production-grade versus hackathon shortcut?

Answer structure:

Use proof bundle.

Show:

- verified requirements
- implemented-unverified
- deviations
- known limitations
- replacement paths

Never claim everything is production-ready if it is not.

## 21. Can this factory build something else?

Strongest answer:

- Conditor bootstraps the same components
- generic semantic compiler/testing/proof patterns are reusable
- domain-specific investigation code is separate
- EDF becomes a domain pack
- next month another application can reuse the engineering factory even if its business domain differs

If possible by judging time:
show a tiny second generated sample or a non-investigator test repo created with Conditor.

## 22. Why should we believe it started blank?

Evidence:

- kickoff baseline
- first commit
- build timeline
- proof bundle
- Git history
- explicit pre-event artifacts separated from implementation repo

## 23. What did AI actually do?

Show:

- agent work items
- generated code commits
- prompts/roles at safe level
- tests/evidence
- human decisions/reviews

Avoid claiming "AI built everything." Show where governance mattered.

## 24. What failed today?

Have an answer.

Strong response:

- show actual failed experiment/test/architecture decision
- show Aegis/ROS/Praxis evidence
- show how process caught/recovered

A credible failure story may be stronger than pretending there were none.

## 25. What would you build next?

Prioritized:

- hosted persistence provider
- EDF pack/research mode
- collaboration/review
- richer observability integrations
- cross-investigation intelligence
- production auth/security hardening

Tie to existing extension boundaries.
