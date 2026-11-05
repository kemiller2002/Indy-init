Indy Init: Parallel Acceleration Directive

Repository: kemiller2002/Indy-init
Target: Rehearsal 010 and the November 7, 2026 Indy Init competition
Priority: Deliver a working, tested F# Causa application as early as possible
Authorization: Standing technical approval for the work described below

1. Executive instruction

Begin immediately. Do not wait for another owner response.

Execute four optimization workstreams in parallel:

1. Functional milestone-first delivery.
2. UI bottleneck elimination through parallel ownership.
3. Concurrent local verification and efficient CI batching.
4. Elimination of nonblocking contract, governance, and design work.

The purpose is to reduce time to a working application without weakening its correctness.

Use existing development agents and independent reviewers. Do not introduce additional orchestration systems unless demonstrably necessary.

The entire application must remain F#, using Limen for browser WebAssembly and Forma for UI. Folio is required only for printable features.

Minimal browser bootstrap interoperability is permitted where technically necessary. JavaScript or TypeScript must not become an alternative application implementation.

2. Standing owner authorization

The owner explicitly authorizes the agents to make and execute compliant technical decisions necessary to implement these four optimizations.

This includes:

* Selecting implementation approaches and work-item boundaries.
* Reordering work to prioritize functional milestones.
* Assigning nonoverlapping source ownership across agents.
* Creating and modifying technical interfaces.
* Running tests and correcting failures.
* Creating and merging PRs after required checks pass.
* Updating preparation scripts, development workflows, and non-security governance procedures.
* Deferring noncritical work while preserving its original requirements and incomplete status.
* Selecting the recommended option for routine technical decisions.
* Reusing existing approved libraries, packages, and tooling.

This authorization applies through the competition on November 7, 2026, unless revoked.

Authorization implementation

Inspect existing owner-decision and approval mechanisms.

If they require an owner-authored record, do not fabricate one or impersonate the owner.

Instead, implement a narrowly scoped delegated-authorization mechanism that:

1. Records this directive as the source of authority.
2. Identifies which decisions are covered.
3. Records the actual agent making each decision.
4. Preserves provenance and an audit trail.
5. Rejects decisions outside the authorized scope.
6. Includes positive and negative authorization tests.

Timebox this governance change to 45 minutes.

If it cannot be implemented correctly within that time, continue all work not blocked by it. Collect remaining owner-only decisions into one concise batch, rather than repeatedly stopping every workstream.

Do not bypass branch protections, security controls, mandatory independent review, or required acceptance tests.

Decisions still reserved to the owner

Do not independently authorize:

* Weakening or removing acceptance criteria.
* Claiming failed or unexecuted tests passed.
* Expanding credential or repository access.
* Publishing secrets.
* Reducing the fundamental application scope without recording the reduction.
* Altering competition eligibility rules.
* Starting the official timed competition build.
* Forging owner identity, signatures, or approval records.

These restrictions do not prevent agents from continuing independent, authorized work.

3. Parallel workstream A: Functional milestones

Owner: Existing integration or scheduling agent.

Reorganize execution around the following milestones:

* M1: Connect to the historical repository.
* M2: Browse and open actual historical investigations.
* M3: Create a new investigation.
* M4: Save, reload, and verify persistence.
* M5: Add hypotheses and evidence.
* M6: Analyze relationships and findings.

Prioritize completing M1 through M4 before optional functionality.

Use existing Praxis requirements and obligation IDs.

Preserve every requirement and its traceability.

Split work items only where this permits a functional milestone to be completed and independently verified earlier.

Do not introduce extra splits that increase overhead without improving milestone delivery.

Run the F# schedule model to compare:

* Existing approved execution order.
* Milestone-first ordering.
* Milestone-aligned work-item boundaries.
* Optimized parallel execution.

Report modeled completion times separately from measured results.

Deliverable: An executable, governed milestone-first plan that preserves requirement coverage.

4. Parallel workstream B: UI bottleneck elimination

Owner: Existing UI agents, coordinated by the integration agent.

The UI lane has repeatedly appeared on the modeled critical path.

Divide implementation into independent F# ownership areas:

UI Agent 1: Application shell

* Browser startup and Limen integration.
* Navigation and routing.
* Connect screen.
* Investigation listing.
* Loading and error states.

UI Agent 2: Investigation workspace

* Investigation detail.
* Investigation creation.
* Editing.
* Save and reload interface.
* Workspace state presentation.

UI Agent 3, if an existing agent is available: Hypotheses and evidence

* Hypothesis entry and editing.
* Evidence entry and editing.
* Hypothesis/evidence relationships.
* Analysis presentation.

Do not launch a new agent solely to satisfy this structure. If only two UI agents are available, assign the third area after the first two milestones are stable.

Parallel ownership rules

* Give each agent exclusive ownership of its implementation files.
* Share stable F# interfaces rather than frequently editing common files.
* Avoid simultaneous modifications to project files, application entry points, or shared contracts.
* Have the integration agent own shared wiring.
* Use the approved UI contracts where available.
* Do not block implementation on optional v4.1.5 enhancements.
* Preserve mobile responsiveness and Forma styling.

Each agent must compile and test its own work before submitting it for integration.

Deliverable: A revised ownership map and independently buildable UI components.

5. Parallel workstream C: Verification and CI acceleration

Owner: Existing verification agent.

Measure and reduce verification waiting without removing required checks.

Use three verification levels.

Level 1: Immediate local feedback

After each meaningful implementation change:

* Compile affected F# projects.
* Run focused unit tests.
* Run relevant contract checks.
* Run architecture checks where applicable.

Do not run the entire offline suite after every small edit unless the change affects shared infrastructure.

Level 2: Independent slice verification

When a functional slice is ready:

* Run relevant integration tests.
* Verify its acceptance criteria.
* Verify applicable requirement and field IDs.
* Record evidence.
* Submit the slice for independent review.

Allow multiple independent slices to undergo verification concurrently, subject to resource and test-environment isolation.

Level 3: Integration acceptance

At integration boundaries:

* Batch compatible changes where safe.
* Run mandatory full CI.
* Run required cross-component tests.
* Reject merges with failed required checks.
* Preserve independent review requirements.

A green focused test does not substitute for required full acceptance.

CI optimization investigation

Measure:

* CI queue time.
* Build duration.
* Test duration.
* Duplicate work.
* Cache effectiveness.
* Agent waiting time.
* Repeated checks that could safely share results.

Prefer existing pinned dependencies and verified build artifacts.

Do not introduce unverified caches or suppress failures to improve apparent speed.

Deliverable: A faster verification workflow with measured before-and-after timings.

6. Parallel workstream D: Remove nonblocking work

Owner: Existing package or requirements agent.

Review all work scheduled before the competition.

Classify it as:

* Blocking: Necessary for the working application, correctness, security, or required acceptance.
* Parallelizable: Necessary but can proceed without blocking application implementation.
* Deferrable: Valuable but unnecessary for the first working application.

Immediately prioritize blocking work.

Move parallelizable work off the application critical path.

Defer nonblocking work, including:

* Additional schedule-model refinements.
* Generalized workspace abstractions beyond demonstrated needs.
* Optional UI contract extensions.
* Nonessential styling refinements.
* Additional documentation beyond operational needs.
* New framework or tooling evaluations.
* Repeated reviews of unchanged decisions.
* Optional package restructuring.

Do not discard requirements or represent deferred work as completed.

If v4.1.5 is on the critical path, identify the smallest contract change necessary for the working application and independently verify it.

Do not make the application wait for unrelated package improvements.

Deliverable: A dependency-reduced plan with a clear record of deferred obligations.

7. Integration coordination

The lead agent owns integration and conflict resolution.

Begin all four workstreams immediately.

Before implementation, each workstream must publish a short ownership record containing:

* Assigned files.
* Dependencies.
* Expected deliverable.
* Required verification.
* Earliest integration opportunity.

Do not wait for every workstream to finish before integrating completed, independently verified functionality.

If a shared contract must change, notify dependent agents and publish the interface change promptly.

Avoid having multiple agents independently redesign the same interface.

8. Working application acceptance

The first target is:

Connect → Browse → Open → Create → Save → Reload

Acceptance requires:

1. A real F# WebAssembly application running in a browser.
2. Actual historical data loaded from kemiller2002/causa-data.
3. Correct decoding and replay, with explicit distinctions between raw, decoded, replayed, and authoritative state.
4. Investigation creation through the F# application.
5. A real authorized GitHub write.
6. Independent read-back demonstrating persistence.
7. Mobile browser interaction testing.
8. Required unit, integration, and architecture checks passing.

If repository permissions prevent a real write, record the blocker. A fake-provider test may establish component behavior but must not be reported as real persistence acceptance.

Do not copy precompetition application source into the official competition build if event rules prohibit it.

9. Timeboxes and escalation

Start the four workstreams concurrently.

Use these checkpoints relative to execution start:

T+30 minutes

* Ownership assigned.
* Workstreams running.
* Critical dependencies identified.
* No unnecessary approval waiting.

T+60 minutes

* Milestone-first plan available.
* UI ownership boundaries established.
* Local verification workflow operating.
* Nonblocking work deferred.

T+120 minutes

* Report actual implementation progress.
* Identify any remaining critical blocker.
* Show which functional milestones have passing evidence.

T+240 minutes

* Demonstrate the most complete working application available.
* Report verified Create → Save → Reload status.
* Compare actual elapsed time against the previous process.

If any workstream encounters a blocker, continue the other workstreams. Do not stop the entire operation.

The lead agent may select compliant technical alternatives under the standing authorization.

10. Required measurements

Capture:

Metric	Measurement
Time to first successful F# build	Actual elapsed minutes
Time to first browser render	Actual elapsed minutes
Time to Connect	Actual elapsed minutes
Time to Browse	Actual elapsed minutes
Time to Create	Actual elapsed minutes
Time to verified Save/Reload	Actual elapsed minutes
CI waiting	Actual minutes
Owner-decision waiting	Actual minutes
Integration conflict repair	Actual minutes
Failed verification and repair	Actual minutes

Compare against the existing R010 schedule model.

Never present model projections as observed improvements.

11. Completion report

Produce one consolidated report with:

* Changes made.
* PRs created and merged.
* Authorization records.
* Test results and evidence.
* Actual time saved where measurable.
* Remaining critical-path blockers.
* Functional milestones completed.
* Deferred requirements.
* Recommended Saturday execution configuration.

Include the precise commands required to reproduce the successful build.

12. Final instruction

Begin now.

Parallelize the four workstreams using existing agents.

Use standing technical authorization to avoid unnecessary owner interruptions.

Optimize for the earliest correct, working, independently verified F# application.

Do not sacrifice correctness, provenance, security, or truthful reporting to achieve a faster apparent completion time.

Do not stop after producing a plan. Implement the changes, run the tests, integrate passing work, and report measured results.
One additional recommendation: Have the lead agent explicitly report idle time by cause. Our models suggest that eliminating waiting could matter more than increasing coding throughput, and this script should give us the measurements needed to confirm or reject that hypothesis.