# Causa rehearsal workspace preflight

Status: prospective workflow for the next rehearsal. It does not modify or reinterpret Rehearsal 5.

## Boundary

Causa Data owns the immutable historical template and all workspace creation, validation, publication and provider-resolver checks. Conditor owns their ordered execution and readiness decision. Indy Init owns the requirements and preserves the resulting evidence. GitHub stores the published workspace. The application reads and writes only the configured workspace.

Conditor never copies investigation files itself. It invokes the Causa F# tool's defined commands and fails closed when any command or evidence check fails.

## Preparation

Before selecting an event workspace:

1. Use reviewed Causa Data and Conditor revisions containing CHT-001.
2. Authenticate the host's normal Git credential mechanism for `kemiller2002/causa-data`; do not put a token in the manifest or receipt.
3. Check out Causa Data with the pinned F# engine available.
4. Add the rehearsal section to the existing `conditor.json`. The rehearsal id and name must agree (`006` and `rehearsal-006`, for example), and `create`, `publish` and `verify` must all be true.
5. Confirm the destination branch is the deliberate publication branch. The next event's branch is an operator choice; no tool silently substitutes `main` or an earlier workspace.

## Initialization gate

Run:

```bash
conditor rehearsal init --target . --manifest ./conditor.json --json
```

The command verifies the template; proves local and remote destination absence; creates and verifies the local workspace; publishes it with a non-forced commit; independently reads the published commit back; and checks the actual Causa workspace resolver while loading all three histories.

The receipt is written to `.conditor/evidence/rehearsal-<id>-readiness.json` unless the manifest declares another repository-relative path. Preserve it with the rehearsal preflight evidence. Confirm:

- `overallReadinessVerdict` is `READY`;
- the destination repository, branch and workspace are the selected values;
- `publishedCommit` is the current head of that branch;
- the template digest is the reviewed `historical-v1` digest;
- all three investigation identifiers are present;
- local, remote and application verification states are complete.

Any `BLOCKED` verdict stops the preflight. Follow only the receipt's permitted recovery action. Never overwrite or delete an existing destination to make the gate pass, never reuse a prior rehearsal, and never replace a failed workspace with an empty one.

## Explicit timed start

A READY initialization receipt is preparation evidence. It does not:

- activate a Praxis mission;
- invoke `conditor start` or `conditor supervise`;
- launch an implementation agent;
- establish the competition start timestamp;
- prove that work happened inside the allotted time.

After reviewing the receipt, the operator performs the separate timed-start transition required by the event. That transition records the real start time and points the application configuration at the exact repository, branch and workspace from the receipt.

## Isolation and recovery

Ordinary development may use `workspace-create` directly without Conditor. Every destination is a physical copy beneath `workspaces/demo/<name>/`. Modified-workspace verification replays the full history while proving the template's historical events remain an exact prefix.

If initialization stops after local creation but before publication, the receipt stays BLOCKED and the workspace remains local for diagnosis. If publication occurs but readback or application loading fails, preserve the published commit and receipt for diagnosis; do not force-push over it. A retry with the same name will intentionally refuse the existing destination, so recovery may require an operator-selected new rehearsal identifier after the defect is corrected.

## Requirement intake

The prospective requirements are in [`input-documents/2026-10-28-CHT-001-causa-historical-workspace-initialization.md`](../input-documents/2026-10-28-CHT-001-causa-historical-workspace-initialization.md). Indy Init uses the legacy `input-documents/` Praxis intake until it is Praxis-initialized. The document is not an approval and remains pending until derived records are produced by ingestion.
