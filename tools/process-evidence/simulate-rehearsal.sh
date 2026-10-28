#!/usr/bin/env bash
# Simulated rehearsal for process evidence (Echelon development speed optimization, Priority One).
#
# Runs a small rehearsal through the real Praxis CLI in a scratch repository: an implementing agent, an independent
# reviewing agent, the owner and CI automation each act under their own identity, and every process record is written
# by Praxis as the work happens (work start, work checkpoint, work evidence). The script then copies the run's Praxis
# log and execution envelopes, the candidate's ancestry, simulated CI-observed owner attestations and the frozen
# rules into OUT_DIR. Nothing is written by hand into the Praxis log.
#
# usage: simulate-rehearsal.sh PRAXIS_DLL OUT_DIR
set -euo pipefail

praxis_dll=$(realpath "$1")
out=$(realpath -m "$2")
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
bare="$work/remote.git"
repo="$work/rehearsal"

# Identities come only from the variables set per call; nothing leaks in from the caller's own agent runtime.
run_as() {
  local who=$1; shift
  local identity=()
  case "$who" in
    impl) identity=(ROS_ACTOR_KIND=agent ROS_ACTOR=example/implementing-agent ROS_TELEMETRY_PROVIDER=example ROS_TELEMETRY_RUNTIME=implementer ROS_TELEMETRY_SESSION_ID=session-impl) ;;
    reviewer) identity=(ROS_ACTOR_KIND=agent ROS_ACTOR=example/review-agent ROS_TELEMETRY_PROVIDER=example ROS_TELEMETRY_RUNTIME=reviewer ROS_TELEMETRY_SESSION_ID=session-review) ;;
    owner) identity=(ROS_ACTOR_KIND=human ROS_ACTOR=kemiller2002 ROS_TELEMETRY_SESSION_ID=session-owner) ;;
    ci) identity=(GITHUB_ACTIONS=true GITHUB_RUN_ID=9000000001) ;;
    none) identity=() ;;
  esac
  env -i PATH="$PATH" HOME="$HOME" DOTNET_ROOT="${DOTNET_ROOT:-}" DOTNET_CLI_TELEMETRY_OPTOUT=1 "${identity[@]}" \
    dotnet "$praxis_dll" --root "$repo" "$@" >/dev/null
}
now() { date -u +%Y-%m-%dT%H:%M:%S.%3NZ; }
g() { git -C "$repo" "$@"; }
commit_push() { g add -A && g commit -q -m "$1" && g push -q; }

git init -q --bare -b main "$bare"
git init -q -b main "$repo"
g config user.email simulation@example.invalid
g config user.name "Simulated rehearsal"
g remote add origin "$bare"
run_as none init --project "Simulated Rehearsal"
commit_push_initial() { g add -A && g commit -q -m "install praxis" && g push -q -u origin main; }
commit_push_initial

# Implementation: assignment, a dependency decision, the work, a durable checkpoint, an approved scope change.
run_as impl work start --id WI-0001 --type feature --occurred-at "$(now)"
run_as impl work evidence --id WI-0001 --kind assignment --summary "Implementing agent owns the investigation engine" \
  --subject src/engine/ --subject AR-012 --occurred-at "$(now)"
run_as impl work evidence --id WI-0001 --kind dependency-decision --summary "Use the released Forma 0.11.0 type roles" \
  --subject "npm:@echelon-foundry/forma@0.11.0" --reference "https://www.npmjs.com/package/@echelon-foundry/forma" --occurred-at "$(now)"
mkdir -p "$repo/src/engine"
echo "let decide state command = state" > "$repo/src/engine/Engine.fs"
commit_push "engine slice"
run_as impl work checkpoint --id WI-0001 --summary "Engine slice pushed" --next-action "Select the candidate" --occurred-at "$(now)"
run_as impl work evidence --id WI-0001 --kind scope-change --summary "Add src/engine/Replay.fs to the owned paths" \
  --subject src/engine/Replay.fs --decision DEC-SIM-001 --occurred-at "$(now)"
run_as owner work evidence --id WI-0001 --kind owner-approval --summary "Owner approves the scope change" \
  --decision DEC-SIM-001 --subject src/engine/Replay.fs --outcome approved --attestation "github-review:kemiller2002/simulated-rehearsal#1@1001" --occurred-at "$(now)"
run_as owner work evidence --id WI-0001 --kind owner-approval --summary "Owner resolves the kickoff decisions (write scope, release set, frozen requirements)" \
  --decision DEC-SIM-KICKOFF --outcome approved --attestation "github-review:kemiller2002/simulated-rehearsal#4@4001" --occurred-at "$(now)"
commit_push "praxis state"
candidate=$(g rev-parse HEAD)

# Release: the candidate is selected, CI records the run, an independent reviewer reviews it, the run is disposed.
run_as impl work evidence --id WI-0001 --kind release-selection --summary "Candidate selected for acceptance" \
  --candidate "$candidate" --occurred-at "$(now)"
run_as ci work evidence --id WI-0001 --kind test-result --summary "Acceptance job passed" --candidate "$candidate" \
  --outcome pass --reference "actions/runs/9000000001" --occurred-at "$(now)"
run_as ci work evidence --id WI-0001 --kind acceptance-reference --summary "Acceptance evidence bundle" \
  --candidate "$candidate" --reference "evidence/acceptance/run-9000000001/verdict.json" --occurred-at "$(now)"
run_as reviewer work start --id WI-0002 --type review --role review --occurred-at "$(now)"
run_as reviewer work evidence --id WI-0002 --kind independent-review --summary "Adversarial review of the candidate: no blocking findings" \
  --candidate "$candidate" --outcome approved --subject WI-0001 --attestation "github-review:kemiller2002/simulated-rehearsal#2@2001" --occurred-at "$(now)"
run_as owner work evidence --id WI-0001 --kind final-disposition --summary "Owner accepts the candidate" \
  --candidate "$candidate" --outcome accepted --attestation "github-review:kemiller2002/simulated-rehearsal#3@3001" --occurred-at "$(now)"

mkdir -p "$out/executions"
cp "$repo/.ros/events/events.jsonl" "$out/events.jsonl"
for dir in "$repo"/.ros/executions/*/; do
  id=$(basename "$dir")
  cp "$dir/envelope.json" "$out/executions/$id.json"
done
echo "$candidate" > "$out/candidate.txt"
g rev-list "$candidate" > "$out/ancestry.txt"

# What CI would observe from the GitHub API in the trusted run: approving pull-request reviews by the owner (with the
# decisions and paths the reviewed pull request carries: an attestation proves only what it names) and by the frozen
# independent reviewer, each with the commit it approved.
cat > "$out/attestations.json" <<JSON
{
  "schema": "indy-init.owner-attestations/1",
  "runId": "9000000001",
  "candidate": "$candidate",
  "attestations": [
    { "type": "github-review", "reference": "kemiller2002/simulated-rehearsal#1@1001", "login": "kemiller2002", "state": "APPROVED", "decisions": ["DEC-SIM-001", "src/engine/Replay.fs"], "commit": "$candidate", "submittedAt": "$(now)" },
    { "type": "github-review", "reference": "kemiller2002/simulated-rehearsal#4@4001", "login": "kemiller2002", "state": "APPROVED", "decisions": ["DEC-SIM-KICKOFF"], "commit": "$candidate", "submittedAt": "$(now)" },
    { "type": "github-review", "reference": "kemiller2002/simulated-rehearsal#2@2001", "login": "example-reviewer", "state": "APPROVED", "commit": "$candidate", "submittedAt": "$(now)" },
    { "type": "github-review", "reference": "kemiller2002/simulated-rehearsal#3@3001", "login": "kemiller2002", "state": "APPROVED", "decisions": ["final-disposition"], "commit": "$candidate", "submittedAt": "$(now)" }
  ]
}
JSON

cat > "$out/rules.json" <<'JSON'
{
  "schema": "indy-init.process-evidence-rules/1",
  "description": "Frozen process-evidence rules of the simulated rehearsal: who may act, who the owner is, and which execution roles are review roles.",
  "owners": [ { "login": "kemiller2002", "actorIds": ["kemiller2002"] } ],
  "repository": "simulated-rehearsal",
  "agents": ["example/implementing-agent"],
  "reviewers": [ { "actorId": "example/review-agent", "login": "example-reviewer" } ],
  "automation": ["github/github-actions"],
  "reviewRoles": ["review"],
  "ownerDecisions": {
    "AR-024-O1": ["DEC-SIM-KICKOFF"],
    "AR-024-O2": ["DEC-SIM-KICKOFF"],
    "AR-008-O2": ["DEC-SIM-KICKOFF"]
  }
}
JSON

echo "candidate $candidate"
