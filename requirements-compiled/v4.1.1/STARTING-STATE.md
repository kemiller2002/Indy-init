# Starting-state policy (v4.1.1)

This policy says what exists before T0 on November 7, what the competition repository holds at T0, and how the plan treats the published event rules. It replaces the internal organizer-permission gate of v4.1 (change V411-C, [analysis/amendment.json](analysis/amendment.json); owner decision OD-INDY-18).

## External constraints: the published event rules

The published Indy Init hackathon page states (quoted in [docs/HACKATHON-BOUNDARIES.md](../../docs/HACKATHON-BOUNDARIES.md), "Published rule verification", last reviewed 2026-09-24):

- "the repository starts empty";
- "code starts at kickoff";
- "teams may plan beforehand, including the problem, stack, and sketch";
- the build window is 9:30 AM to 5:00 PM on November 7, 2026.

These are external constraints. The plan does not reinterpret them, and if organizer guidance on the day conflicts with this policy, organizer guidance wins (HACKATHON-BOUNDARIES, "Organizer verification").

## Internal methodology: no organizer permission gate

The engineering methodology is ours. No task in the execution plan waits on an organizer response:

- T10, the bootstrap, no longer follows T06 ("Organizer confirmation for prepared requirements, data and tooling"). The adjustment drops that preference (X13), and X19 fails if any timed task depends on, or prefers to follow, T06.
- AGENT-RULES no longer says the bootstrap "relies on ODR-V41-06" (X19 checks that too).
- X19 scans for organizer gates and fails on any without a recorded disposition in [analysis/amendment.json](analysis/amendment.json) (`startingState.organizerReferences`). It reads v4's preflight checks, tasks, requirement obligations and risk register; the DAG waivers resting on ODR-V41-06; and the documents listed in `startingState.organizerDocuments`. A gate is a mention of an organizer with a word of approval, confirmation, permission, waiting or asking. The scan is a heuristic over those sources, not over every file in the repository. No obligation it finds is claimed met. How the organizer obligations are reported is the owner's decision ODR-V411-02: ADRP-016-O2 and its checklist form, STORAGE-008-O1, R0.1-O1, the kickoff checks, PRE-ORG-01/T06, RISK-12, OD-INDY-12's condition, the DOMAIN-TEST-MATRIX condition, and the waivers resting on ODR-V41-06.

v4's T06 remains in v4's frozen task list, because v4 is frozen. No v4.1.1 task depends on it.

## Before T0

These exist before T0, all outside the competition repository:

- the approved requirements and verification plan (v4, frozen at tag `indy-init-requirements-v4.0.0`);
- the F# interface contracts, as specifications ([contract-classification.json](contract-classification.json)), and the state machines;
- the deterministic bootstrap generator (`requirements-compiler v41`), tested in CI against disposable empty directories only;
- the pinned dependency versions and compile order;
- the Echelon tools (Conditor, Praxis, Ordo, Percepta, Limen, Forma, Folio) installed on the host.

The competition repository does not exist before T0. Pre-event validation never writes to it: the bootstrap test runs in throwaway directories (CI's `$RUNNER_TEMP`, and the measurements in [analysis/bootstrap-measurement.json](analysis/bootstrap-measurement.json)).

## At T0

T10 is the first timed task. Integration:

1. records the start timestamp;
2. creates the empty repository (`git init -b main`; git metadata only);
3. runs `conditor init --preset indy-init --target .`: the Echelon scaffold, Praxis initialization and the requirements import (the plan's own event setup, from v4's KICKOFF-EXECUTION-PACKET §1, an internal plan, not an organizer permission);
4. runs the generator with `--bootstrap .`, which layers the solution, projects, compile lists and contract declarations onto Conditor's repository;
5. builds the skeleton and runs `conditor ci-policy check`;
6. commits (the initial bootstrap commit), publishes with `conditor repo create`, and waits for the first green CI.

The generator writes the contract declarations into their producers' files and an empty namespace into every other planned file. It writes no application behavior beyond the reviewed boundary code that [contract-classification.json](contract-classification.json) lists: the credential that prints as `<credential>` and the confirmation that exists only from a matching read-back. No source is imported from an earlier rehearsal (X11).

## Historical records, unchanged

- ODR-V41-06 (proposals/owner-decision-requests.json) and its record APR-V41-OWNER-1 in `kickoff/rehearsal-007/approvals.causa.json` stay as written, including the owner's `organizer_confirmation` text. This policy does not restate or reinterpret what the organizers said.
- The published-rule section of docs/HACKATHON-BOUNDARIES.md stays as reviewed on 2026-09-24.

## Residual interpretation risk

The published rules say the repository starts empty and code starts at kickoff. They do not say whether material prepared before kickoff in executable form counts as planning or as code. That material is the F# contract declarations, the deterministic generator, the layout assertions, and what `conditor init` writes at T0. It writes the Echelon scaffold and imports 68 requirement documents: 42 from this planning repository, at its older commit 2eee2d5 (T01 still regenerates and pins the authoritative baseline at the kickoff commit), and 26 from Percepta. HACKATHON-BOUNDARIES itself lists as open whether a separate planning repository may be consumed directly by the blank implementation repository. The plan's boundary is as follows:

- Before T0, nothing is written to the competition repository.
- At T0, the generator writes the skeleton and the contract declarations from the approved package.

An organizer could read the materialized contract declarations, the Conditor scaffold, the requirements import or the bootstrap itself as material prepared before kickoff. Whether organizer approval of the prepared material (ADRP-016-O2) is sought, and how that obligation and R0.1-O1 are reported, is the owner's decision ODR-V411-02; the package as proposed (its option A) does not seek it. This policy does not claim that the F# contracts are exempt under every reading of the rules. If an organizer rules otherwise on the day, the team follows the ruling.
