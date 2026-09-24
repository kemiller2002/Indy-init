# Competition Demo Choreography

Status: proposed ambitious demo profile.

The demo should feel like a real investigation that surprises the system, not a scripted sequence that always confirms its first guess.

## 1. Demo principle

The best demo outcome is:

> The system forms a plausible leading explanation, new evidence breaks it, the system visibly changes its mind, and then it verifies a correction.

That demonstrates far more than a successful first guess.

## 2. Ideal scenario properties

The scenario should contain:

- ambiguous initial observation
- at least three plausible hypotheses
- one misleading early clue
- one evidence dependency/common-source trap
- one hidden assumption
- one material contradiction
- one strong discriminating action
- one falsified leading hypothesis
- causal set or contributing factor
- corrective action
- verification
- opportunity for offline/pending persistence
- enough complexity to show state, but understandable in under a minute

## 3. Opening

Screen:

```text
What happened?
```

Enter one natural sentence.

Immediately show:

- original observation preserved
- structured observation proposal
- three materially different hypotheses
- one unknown
- one recommended discriminating question

Judge should understand the product in ~20 seconds.

## 4. First wow moment: explicit alternatives

Open Hypothesis Battlefield.

Show:

- H1 plausible
- H2 plausible
- H3 plausible
- each has different prediction

Say:

"The system isn't asking the model for an answer. It's managing competing explanations."

## 5. Second wow moment: blind multi-agent panel

Run or reveal preconfigured live agent roles over the current frozen state.

Show:

- Generator proposes H1/H2/H3
- Evidence Auditor identifies E1/E2 common source
- Skeptic identifies hidden assumption A1
- Experiment Designer recommends T1

Do not wait on every model if latency hurts presentation; support cached research-run replay clearly labeled as prior run if necessary.

## 6. Third wow moment: pre-register prediction

Before T1:

- show H1 predicts X
- H2 predicts Y
- H3 predicts X

System says:
"T1 separates H2 from H1/H3, but doesn't distinguish H1 from H3."

Then select a better T2 if available.

This visibly demonstrates discriminating-test reasoning.

## 7. Fourth wow moment: system changes its mind

Admit finding F.

Result contradicts current leader H1.

Show live:

- E admitted
- contradiction appears
- H1 weakens or falsifies
- negative knowledge recorded
- H2 becomes derived Leading
- ConfirmCause blocker changes
- recommended next action changes

This should be visually dramatic but state-driven.

## 8. Fifth wow moment: "Why?"

Click:

```text
Why did H1 stop leading?
```

Show semantic explanation:

- new E12
- failed necessary prediction P4
- contradiction C2
- H1 falsified within scope
- H2 remains supported

No hand-wavy model explanation.

## 9. Sixth wow moment: time travel

Scrub backward before E12.

H1 is leading again.

Scrub forward.

H1 falsified.

The exact same app state is reconstructed from events.

## 10. Seventh wow moment: counterfactual

Ask:

"What if E12 is invalid?"

Open sandbox branch.

Show H1 becomes viable again.

Return to authoritative state unchanged.

This demonstrates reversible reasoning.

## 11. Eighth wow moment: confirm causal set

After additional evidence:

Show causal assertion:

```text
Trigger: configuration change
+
Enabling condition: stale cache behavior
=
Observed failure
```

Confirm causal set only after blockers clear.

## 12. Ninth wow moment: correction is not done

Record corrective action.

UI says:

```text
Implemented
Awaiting verification
```

Attempt Resolve.

System blocks it.

This is a powerful trust moment.

## 13. Tenth wow moment: verification

Record verification evidence.

UI transitions:

- action Verified Effective
- Resolve becomes Available
- investigation Resolved

Generate Folio report.

## 14. Eleventh wow moment: factory reveal

Switch to Factory View.

Show:

- requirements
- 135+ acceptance scenarios baseline
- current ROS work item
- tests
- Praxis metrics
- Aegis faults handled
- commits since kickoff
- deployment
- proof bundle

Message:

"The same idea governed how we built the application."

## 15. Recursive close

Optionally show a real investigation generated from one of the app's own build/test failures.

This closes the loop:

```text
The factory built the investigator.
The investigator investigated the factory.
```

## 16. Offline proof

If reliable in event environment:

- disconnect network
- add evidence
- status becomes Local / Pending
- app remains usable
- reconnect
- command revalidates and syncs

This is more compelling than claiming offline support.

## 17. Judge exploration mode

After formal demo, leave the app in Spectator/Explorer mode with:

- sample investigation
- replay
- time travel
- challenge button
- evidence graph
- report
- factory proof

Judges can explore without risking source state.

## 18. Failure fallback

Have fallback paths:

- live AI fails -> use recorded frozen run with clear label
- GitHub fails -> local pending mode becomes demonstration
- graph fails -> list/table projection
- report fails -> existing report snapshot
- external artifact unavailable -> artifact-unavailable state

The failure itself should reinforce the product semantics where possible.

## 19. Demo anti-patterns

Avoid:

- typing a long prompt and waiting
- AI instantly guesses the correct root cause
- fake confidence percentages
- scripted hard-coded result
- hiding contradictory evidence
- showing raw JSON
- spending most time on architecture slides
- making judges infer why the state model matters

The application should demonstrate those ideas directly.

## 20. Final sentence

A strong closing line:

"AI gave us proposals. The system gave us a defensible investigation."
