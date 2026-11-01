# Token economics (Amendment D)

Amendment D asks the rehearsal to report what its work consumed, in tokens and in cost. It uses the token and cost telemetry Praxis already has. It adds no telemetry framework.

The rehearsal records tokens in its Praxis execution records. At completion, one F# command reads those records and writes two files in the rehearsal (application) repository:

- `evidence/telemetry/token-economics.json`
- `evidence/telemetry/token-economics.md`

**Absolute rule.** Token consumption is never inferred from elapsed time, commits, source lines or transcript character counts. A figure comes only from a `tokens.*` measurement in a Praxis record. When the records do not carry a value, the report says it is unavailable and gives the record's own reason. It is never estimated and never shown as zero.

## 1. What the rehearsal must run (Praxis 3.24.0)

The locked Praxis is 3.24.0 (commit `dccf840`). Its telemetry is unchanged since 3.21.0. The source for this section is Praxis `docs/development-telemetry.md`: "Commands", "Session transcript metrics (PRAXIS-PLAN-05)", "Cumulative usage and cost attribution (CONSTRUCT-010 Phase 6)" and "Reliability report (PRX-REL-009)".

`work begin` creates an execution record and stores the runtime's session ID (`CLAUDE_CODE_SESSION_ID`) as `identity.sessionId`. **It never reads the session transcript.** Praxis's own report (PRX-REL-009) says why Rehearsals 5 and 6 have no tokens: "The adapters work ... nothing runs them." Container transcripts disappear when the session ends, so tokens cannot be added later. The steps below must run during the rehearsal.

### Once, at start

```bash
./praxis telemetry adapters        # must list anthropic-claude-session
```

### Per execution, before its `work complete`

Each agent or subagent runs under its own execution. A subagent's execution is started with its parent and its agent identity, for example `./praxis telemetry start WI-ID --parent-execution EXE-PARENT --agent ID --subagent ID`, and with `PRAXIS_AGENT_INSTANCE_ID` set for that subagent. Before that execution's `work complete`, ingest the transcript of the session that did the work:

```bash
# Main (coordinator) session: one file per session, named by CLAUDE_CODE_SESSION_ID.
./praxis telemetry ingest EXE-ID --adapter anthropic-claude-session \
  --input ~/.claude/projects/<cwd-slug>/<SESSION>.jsonl

# A subagent: its own file under the parent session.
./praxis telemetry ingest EXE-SUBAGENT-ID --adapter anthropic-claude-session \
  --input ~/.claude/projects/<cwd-slug>/<SESSION>/subagents/agent-<AGENT>.jsonl
```

- `TARGET` may be the execution ID (`EXE-...`) or the work item. Use the execution ID when a work item has more than one active execution.
- `<cwd-slug>` is the session's working directory with `/` replaced by `-`, for example `-home-user`. `ls ~/.claude/projects/*/"$CLAUDE_CODE_SESSION_ID".jsonl` finds the file.
- The adapter writes `tokens.input`, `tokens.output`, `tokens.cache_read`, `tokens.cache_write` and the TTL split, all `observed`, with `dimensions.snapshot = claude-session-SESSION`. Output is a lower bound (`dimensions.bound = lower`). It also writes `cost.session_cumulative`, which is the runtime's own estimate (`estimated`). It never stores prompts or content.
- Ingest each session once, when its work is done. The coordinator's main session runs to the end, so ingest it once, at completion, into the coordinator's execution and before that execution's `work complete`.
- Do not ingest the same session into two executions. If it happens, the report counts the session once, at its largest value, and lists the other ingest as excluded.

### Only when the platform shows a cost

If the execution platform displays a cost for the session, record it as provider-reported. Never compute this number:

```bash
./praxis telemetry record EXE-ID --metric cost.execution_total --value <AMOUNT> --currency USD \
  --quality observed --source-type platform --source-name claude-code-remote --mechanism session-record
```

### At completion, after the last `work complete`

```bash
# The Amendment D report (from an Indy-init checkout, which has the compiler):
dotnet run --project tools/requirements-compiler/src -- token-economics <REHEARSAL-ROOT> \
  --window-start <T0, ISO-8601 UTC> --window-end <DEADLINE, ISO-8601 UTC> \
  [--price-table <OWNER-SUPPLIED-TABLE.json>]
# writes <REHEARSAL-ROOT>/evidence/telemetry/token-economics.{json,md}; --out DIR changes the location.

# Optional cross-checks from Praxis itself (read-only):
./praxis telemetry usage --by execution --format json > evidence/telemetry/praxis-usage.json
./praxis telemetry reliability-report --transcripts <COPIED-TRANSCRIPTS-DIR> --out evidence/telemetry/praxis-reliability-report.json
```

`reliability-report --transcripts` uses each request's own timestamp. It is the only way to split one session that crosses T0. Copy the transcripts next to the run before the container ends; the report identifies them by SHA-256, never by path.

## 2. The command

```
requirements-compiler token-economics ROOT --window-start ISO-8601 --window-end ISO-8601 [--price-table FILE] [--out DIR]
```

The command reads `ROOT/.ros/telemetry/executions/*.json` and writes `token-economics.json` and `token-economics.md` to `--out`, which defaults to `ROOT/evidence/telemetry`. Exit codes: 0 when the files are written, 2 for unusable input (no records directory, a bad window, or a refused price table).

The code is `tools/requirements-compiler/src/TokenEconomics.fs`. The command is in `Program.fs`. All logic is pure. The same records give byte-identical output in any file order, and the output contains no wall-clock time.

**Price table.** The price table is optional, and an operator supplies it. Praxis ships no prices, and neither does this tool. The table uses the Praxis legacy format, the one `telemetry usage --price-table` reads:

```json
{ "source": "...", "version": "...", "effectiveDate": "YYYY-MM-DD", "currency": "USD", "unit": "per-million-tokens",
  "models": { "MODEL": { "input": 0, "output": 0, "cacheRead": 0, "cacheWrite": 0 } } }
```

A table without `source`, `version`, `effectiveDate`, `currency` and `unit: per-million-tokens` is refused. Without a table, price-table-derived cost is `unavailable` ("no operator-supplied price table").

## 3. Output (`indy-init.token-economics/1`)

| Member | Content |
|---|---|
| `source.records` | Each record's file name and SHA-256 |
| `window`, `priceTable` | The inputs: the window, and the table's source, version, effective date, currency and models |
| `rules` | The accounting rules below, as stated in the output |
| `totals` | One figure group for all executions |
| `byPhase` | `pre-event`, `timed`, `post-event` (and `unknown`) |
| `phaseCaveats` | Transcript sessions whose span crosses the window start or end |
| `byWorkItem`, `byAgent`, `byRole`, `byProvider`, `byModel`, `byStep` | Attribution as far as the records allow |
| `completeness` | `tokensComplete`, `tokensPartial` and `tokensUnavailable` (each missing metric with its capability status and reason), `providerCostReported` |
| `failedOrRepeated` | `abandoned` (`work.abandoned`, with reason), `failed` (`step.failed`, with step and reason), `unfinalized` (status not `finalized`), `repeatedWorkItems`, and `figures`: one group over all such executions |
| `executions[]` | For each execution: its record, work item, attempt N of M, status, outcome, phases, provider, model, served models, runtime, agent, role, actor kind, parent, the four token buckets (with quality, source and lower bound, or with the unavailable status and reason), and cost: `providerReported`, `priceTableDerived` and `runtimeEstimates` |
| `excludedMeasurements` | Estimated token values, non-observed costs, and repeated snapshot ingests, each with its reason |

**Figure groups.** Each group has `executions`. Under `tokens` it has `input`, `output`, `cacheRead`, `cacheWrite` and `total`. Under `cost` it has `providerReported` and `priceTableDerived`. Each figure has `availability` (`available`, `partial` or `unavailable`), a `value` (or `amount` with `currency`), `lowerBound`, and `unavailableExecutions`. Amounts are decimal strings with six places.

**Attribution sources.**

- **Agent:** `identity.agentId`, followed by `/subagentId` when there is one.
- **Role:** `identity.orchestration.role` when present. Praxis 3.24.0 always writes this object empty, so in practice the role comes from the work classification (`classification.types`, set with `--classification` or `telemetry classify`). Otherwise the role is `(not recorded)`.
- **Model:** the single served model in the session snapshot when there is one, otherwise `identity.model`.
- **Step:** the measurement's `dimensions.step` or `stepId`. Measurements with neither are `(outside any step)`.

## 4. Completeness and accounting rules

1. A token figure comes only from `tokens.input`, `tokens.output`, `tokens.cache_read` or `tokens.cache_write` measurements of quality `observed` or `derived`. Wall time, active time, commits, lines, tool calls, raw payload sizes and transcript sizes never become tokens. The offline suite checks this by adding each of them to an execution that has no tokens.
2. An execution with no measurement of a bucket is unavailable for that bucket. The report gives the record's capability status (`supported-unavailable`, `unknown`, `unsupported`, or `not-recorded` when there is neither a measurement nor a capability). The value is `null`, never `0`.
3. A group in which some executions are unavailable is `partial`. Its value is the sum of the others and is a lower bound.
4. `tokens.output` from a Claude Code transcript is a lower bound (Praxis PRAXIS-REL-010). Every figure that includes it is marked `lowerBound`.
5. `total` = input + output + cache read + cache write. It is derived only from those four buckets. Cache read and cache write are never folded into input.
6. A session snapshot ingested into several executions is counted once, at its largest value, in the execution that holds it. The other ingests are listed as excluded.
7. Provider-reported cost is the sum of `observed` `cost.execution_total`. Price-table-derived cost prices each bucket with the operator's table. An execution is priced only when all four buckets are present, its model is in the table, and its measurements are on or after the table's effective date. Otherwise its price is unavailable, with the reason. The two costs are never summed. The runtime's own `cost.session_cumulative` estimate is listed per execution and enters neither.
8. **Phase.** A measurement is `pre-event`, `timed` or `post-event` by its `collectedAt` (else the execution's `startedAt`) against the window, with both ends inclusive. An execution with no measurements is placed by its `startedAt`. A session whose span crosses a boundary is listed in `phaseCaveats` and is never split by proportion or duration.
9. **Repeated work.** Every execution of a work item after the first (by `startedAt`) repeats it.

The offline suite (`dotnet run --project tools/requirements-compiler/tests -- --offline --source .`, group "token economics") runs the command on the recorded fixture `tools/requirements-compiler/tests/fixtures/token-economics/` and checks the expected JSON and Markdown byte for byte. It also checks the following:

- totals add up across every grouping and across executions;
- missing data is `null` and unavailable;
- non-token inputs change no token figure;
- estimated values are excluded;
- duplicate and re-ingested snapshots are counted once;
- without a table, price-table cost is unavailable and provider-reported cost is unchanged;
- the phase boundaries hold;
- the output does not depend on record order;
- an undated price table is refused.

The fixture is invented data, labelled as such in its README.

## 5. Rehearsals 5, 6 and 7

| Rehearsal | Praxis execution records | Token data | Cost |
|---|---|---|---|
| 5 (`indy-init-rehearsal-005` at `b972cb44`; window 2026-10-27T20:08:38Z to 2026-10-28T03:08:38Z) | 8 (1 finalized, 7 never finalized) | None. Every execution has `tokens.input/output/cache_read/cache_write` `supported-unavailable`: "runtime family can expose this metric, but no observation has been ingested for this execution". | None |
| 6 (`indy-init-rehearsal-006` at `41fb6d62`; window 2026-10-28T20:02:19Z to 2026-10-29T03:02:19Z) | 15, all finalized; WI-R6-A, WI-R6-B and WI-R6-C each have 2 executions | None. 13 executions are `supported-unavailable` (same reason). The 2 reviewer executions (runtime `claude-code-reviewer`) are `unknown`: "runtime capability not reported or mapped". | None |
| 7 (`indy-init-rehearsal-007` at `f61493fa`; T0 2026-10-29T22:49:55Z, deadline 2026-10-30T05:49:55Z) | None (no `.ros/telemetry`) | Bespoke, not Praxis: `tools/telemetry.fsx` read the Claude Code transcripts, de-duplicated by message ID, and wrote `evidence/telemetry/main-agent.json` and `reviewer-*.json` (`causa.telemetry/1`: messages, input, output, cache read, cache write, tool calls and models, per phase) | Not computed: no authoritative price list |

**This tool on the real records** (run during this change; the output was not committed):

- **Rehearsal 5:** 8 executions. 0 have complete token data, 0 partial, 8 none. All 32 missing buckets are `supported-unavailable` with the reason above. By phase: 7 timed, 1 post-event. 7 unfinalized. No repeats. No provider-reported cost.
- **Rehearsal 6:** 15 executions. 0 complete, 0 partial, 15 none: 52 missing buckets are `supported-unavailable` and 8 are `unknown`. All 15 are timed. Repeated work items: WI-R6-A, WI-R6-B and WI-R6-C. No provider-reported cost.

**Rehearsal 7's own measurements** are not Praxis records. They are not produced or checked by this tool, and they come from R7's `evidence/telemetry/README.md`.

| Agent and phase | Messages | Input | Output | Cache read | Cache write |
|---|---|---|---|---|---|
| Main agent, preflight (before T0) | 46 | 92 | 25,303 | 6,983,199 | 147,880 |
| Main agent, from T0 (runs to 05:52Z, about 2 minutes past the deadline) | 510 | 1,083 | 490,062 | 238,493,928 | 1,571,829 |
| Independent reviewers 1 to 3, timed | 146 | 294 | 117,416 | 19,646,527 | 571,918 |
| All agents, from T0 | 656 | 1,377 | 607,478 | 258,140,455 | 2,143,747 |

**What can be compared.** Only Rehearsal 7 has token figures, and they come from a different instrument:

- no per-work-item attribution;
- no completeness states;
- output tokens from transcripts, which Praxis REL-010 shows are a lower bound;
- no cost.

Rehearsals 5 and 6 had Praxis telemetry running, but no transcript was ingested, so their tokens are truthfully unavailable. Their transcripts are gone, and they cannot be backfilled. No R5/R6/R7 token comparison is possible. The next rehearsal is the first that can produce Praxis token records, provided its runbook includes section 1.
