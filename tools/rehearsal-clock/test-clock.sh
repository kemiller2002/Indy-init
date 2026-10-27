#!/usr/bin/env bash
# Replays the Rehearsal 4 timeline through clock.fsx: T+60 and T+120 on time, T+240 recorded 8 minutes late.
# The tool must report T+240 OVERDUE before it is recorded, compute its lateness, refuse duplicates and unknown
# names, and count P0 statuses from a ledger instead of a narrative.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
dir="$(mktemp -d)"; trap 'rm -rf "$dir"' EXIT
clock() { dotnet fsi "$here/clock.fsx" "$@"; }
expect() { local want="$1"; shift; local rc=0; "$@" >"$dir/out" 2>&1 || rc=$?; [ "$rc" = "$want" ] || { echo "FAIL: expected exit $want, got $rc: $*"; cat "$dir/out"; exit 1; }; }
echo '{"completedWork":["narrative"],"verifiedRequirements":"typed by the agent; ignored for counts"}' > "$dir/n.json"
printf '{"schema":"causa.acceptance-ledger/2","requirements":[{"id":"A","priority":"P0","status":"PASS"},{"id":"B","priority":"P0","status":"NOT_RUN"},{"id":"C","priority":"P1","status":"FAIL"}]}' > "$dir/ledger.json"
ev="$dir/evidence"
expect 3 clock status --dir "$ev"
expect 0 clock start --dir "$ev" --start-utc 2026-10-27T06:04:39Z --basis "replay of Rehearsal 4"
expect 2 clock start --dir "$ev" --start-utc 2026-10-27T06:04:39Z --basis again
expect 0 clock status --dir "$ev" --now 2026-10-27T06:30:00Z
expect 0 clock checkpoint --dir "$ev" --name T+60 --head bcb78d9 --input "$dir/n.json" --now 2026-10-27T07:01:30Z
expect 2 clock checkpoint --dir "$ev" --name T+60 --head bcb78d9 --input "$dir/n.json" --now 2026-10-27T07:02:00Z
expect 2 clock checkpoint --dir "$ev" --name T+61 --head bcb78d9 --input "$dir/n.json" --now 2026-10-27T07:02:00Z
expect 0 clock checkpoint --dir "$ev" --name T+120 --head 9a0886a --input "$dir/n.json" --ledger "$dir/ledger.json" --now 2026-10-27T08:06:00Z
expect 2 clock status --dir "$ev" --now 2026-10-27T10:12:00Z
grep -q "OVERDUE T+240: due 2026-10-27T10:04:39Z, 7 minutes late" "$dir/out" || { echo "FAIL: overdue report"; cat "$dir/out"; exit 1; }
expect 0 clock checkpoint --dir "$ev" --name T+240 --head 6931477 --input "$dir/n.json" --now 2026-10-27T10:12:00Z
jq -e '.checkpoints[2] | .elapsedMinutes == 247 and .lateMinutes == 7 and .dueAtUtc == "2026-10-27T10:04:39Z"' "$ev/checkpoints.json" >/dev/null || { echo "FAIL: computed fields"; jq '.checkpoints[2]' "$ev/checkpoints.json"; exit 1; }
jq -e '.checkpoints[1].p0FromLedger.counts == {"NOT_RUN":1,"PASS":1}' "$ev/checkpoints.json" >/dev/null || { echo "FAIL: ledger counts"; exit 1; }
jq -e '.checkpoints[0].elapsedMinutes == 56 and .checkpoints[0].lateMinutes == 0' "$ev/checkpoints.json" >/dev/null || { echo "FAIL: T+60 fields"; jq '.checkpoints[0]' "$ev/checkpoints.json"; exit 1; }
expect 0 clock checkpoint --dir "$ev" --name END --head ab08b76 --input "$dir/n.json" --now 2026-10-27T11:00:31Z
expect 0 clock status --dir "$ev" --now 2026-10-27T13:30:00Z
echo "clock: all scenarios passed"
