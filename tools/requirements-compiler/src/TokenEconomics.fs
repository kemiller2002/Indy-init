/// Token and cost economics of a rehearsal (Amendment D). Reads the Praxis execution records of the rehearsal repository
/// (`.ros/telemetry/executions/EXE-*.json`, Praxis telemetry schema 1.0.0, Praxis 3.24.0) and reports input, output,
/// cache-read, cache-write and total tokens; provider-reported and price-table-derived cost, never summed together;
/// attribution by work item, agent, role, provider, model and step; which executions have token data and which do not
/// and why; failed, abandoned, unfinalized and repeated work; and the timed versus pre-event split.
///
/// It integrates Praxis telemetry and measures nothing itself. A token figure comes only from a `tokens.*` measurement
/// in a record (quality observed or derived), never from elapsed time, commits, source lines or transcript size. A value
/// the records do not carry is reported unavailable with the record's own capability status and reason, never zero and
/// never estimated. Everything here is pure; reading the records and writing the report is Program.tokenEconomicsCommand.
module TokenEconomics

open System
open System.Globalization
open Json

type Window = { Start: DateTimeOffset; End: DateTimeOffset }

/// The token buckets reported, with their Praxis metric ids (telemetry/metrics.json). Cache read and cache write are
/// their own buckets and are never folded into input.
let buckets = [ "input", "tokens.input"; "output", "tokens.output"; "cacheRead", "tokens.cache_read"; "cacheWrite", "tokens.cache_write" ]

let private bucketOfMetric = buckets |> List.map (fun (b, m) -> m, b) |> Map.ofList

/// The price-table rate that prices each bucket (the Praxis legacy price table, `telemetry usage --price-table`).
let private rateOf = Map [ "input", "input"; "output", "output"; "cacheRead", "cacheRead"; "cacheWrite", "cacheWrite" ]

let outsideAnyStep = "(outside any step)"
let notRecorded = "(not recorded)"

type Measurement =
    { ExecutionId: string
      MeasurementId: string
      MetricId: string
      Value: decimal
      Unit: string
      Currency: string option
      Quality: string
      CollectedAt: DateTimeOffset option
      Snapshot: string option
      Step: string option
      LowerBound: bool
      Source: string }

type Capability = { MetricId: string; Status: string; Reason: string }

type Execution =
    { Id: string
      File: string
      Sha256: string
      WorkItem: string
      Status: string
      StartedAt: DateTimeOffset option
      FinalizedAt: DateTimeOffset option
      StartedText: string option
      FinalizedText: string option
      Provider: string
      Model: string
      ServedModels: string list
      /// Transcript snapshots: id, session start, session end (rawTelemetry payload startedAt/endedAt).
      Sessions: (string * DateTimeOffset option * DateTimeOffset option) list
      Runtime: string
      Agent: string
      Role: string
      ActorKind: string
      ParentExecutionId: string option
      Abandoned: string option
      FailedSteps: (string * string) list
      Measurements: Measurement list
      Capabilities: Capability list }

type PriceTable =
    { Source: string
      Version: string
      EffectiveDate: DateTimeOffset
      EffectiveText: string
      Currency: string
      Unit: string
      Models: Map<string, Map<string, decimal>> }

// ---- parsing -------------------------------------------------------------------------------------------------------

let parseTime (s: string) : DateTimeOffset option =
    match DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal ||| DateTimeStyles.AdjustToUniversal) with
    | true, t -> Some t
    | _ -> None

let private number (j: Json option) : decimal option =
    match j with
    | Some(JInt i) -> Some(decimal i)
    | Some(JNum d) -> Some d
    | _ -> None

let private obj (key: string) (j: Json) = field key j |> Option.defaultValue JNull

let private textOr (dflt: string) (key: string) (j: Json) =
    match strOpt key j with
    | Some s when s.Trim() <> "" -> s
    | _ -> dflt

let private sourceText (j: Json) =
    let s = obj "source" j
    [ "type"; "name"; "mechanism" ] |> List.map (fun k -> textOr "?" k s) |> String.concat "/"

let private measurement (executionId: string) (j: Json) : Measurement option =
    match strOpt "id" j, number (field "value" j) with
    | Some id, Some value ->
        let dims = obj "dimensions" j
        Some
            { ExecutionId = executionId
              MeasurementId = textOr "" "measurementId" j
              MetricId = id
              Value = value
              Unit = textOr "" "unit" j
              Currency = strOpt "currency" j
              Quality = textOr "" "quality" j
              CollectedAt = strOpt "collectedAt" j |> Option.bind parseTime
              Snapshot = strOpt "snapshot" dims
              Step = (match strOpt "step" dims with Some s -> Some s | None -> strOpt "stepId" j)
              LowerBound = strOpt "bound" dims = Some "lower"
              Source = sourceText j }
    | _ -> None

/// The served models a session-transcript snapshot names (rawTelemetry payload `models`), as the provider reported them.
let private servedModels (record: Json) =
    arr "rawTelemetry" record
    |> List.collect (fun snap -> strs "models" (obj "payload" snap))
    |> List.distinct
    |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))

/// One Praxis execution record. `file` and `sha256` identify the record the figures come from.
let parseExecution (file: string) (sha256: string) (record: Json) : Execution =
    let id = textOr file "executionId" record
    let identity = obj "identity" record
    let events = arr "events" record
    let eventsOf (t: string) = events |> List.filter (fun e -> strOpt "type" e = Some t)
    let agent =
        match strOpt "agentId" identity, strOpt "subagentId" identity with
        | Some a, Some s -> sprintf "%s/%s" a s
        | Some a, None -> a
        | None, Some s -> s
        | None, None -> notRecorded
    { Id = id
      File = file
      Sha256 = sha256
      WorkItem = textOr notRecorded "workItemId" record
      Status = textOr notRecorded "status" record
      StartedAt = strOpt "startedAt" record |> Option.bind parseTime
      FinalizedAt = strOpt "finalizedAt" record |> Option.bind parseTime
      StartedText = strOpt "startedAt" record
      FinalizedText = strOpt "finalizedAt" record
      Provider = textOr notRecorded "provider" identity
      Model = textOr notRecorded "model" identity
      ServedModels = servedModels record
      Sessions =
        arr "rawTelemetry" record
        |> List.choose (fun snap ->
            let payload = obj "payload" snap
            strOpt "snapshotId" snap
            |> Option.map (fun s -> s, strOpt "startedAt" payload |> Option.bind parseTime, strOpt "endedAt" payload |> Option.bind parseTime))
      Runtime = textOr notRecorded "runtime" identity
      Agent = agent
      // Praxis 3.24.0 writes identity.orchestration empty, so the work classification is the role the records allow.
      Role =
        match strOpt "role" (obj "orchestration" identity), strs "types" (obj "classification" record) with
        | Some r, _ when r.Trim() <> "" -> r
        | _, (_ :: _ as types) -> String.concat "+" types
        | _ -> notRecorded
      ActorKind = textOr notRecorded "actorKind" identity
      ParentExecutionId = strOpt "parentExecutionId" identity
      Abandoned =
        eventsOf "work.abandoned"
        |> List.tryHead
        |> Option.map (textOr "work abandoned (no reason recorded)" "reason")
      FailedSteps =
        eventsOf "step.failed"
        |> List.map (fun e -> textOr (textOr "?" "stepId" e) "step" e, textOr "no reason recorded" "reason" e)
      Measurements = arr "metrics" record |> List.choose (measurement id)
      Capabilities =
        arr "capabilities" record
        |> List.choose (fun c ->
            strOpt "metricId" c
            |> Option.map (fun m -> { MetricId = m; Status = textOr "unknown" "status" c; Reason = textOr "no reason recorded" "reason" c })) }

/// The Praxis legacy price table (`docs/development-telemetry.md`, "Cumulative usage and cost attribution"): an
/// operator-supplied table with its source, version and effective (as-of) date. Praxis ships no prices, nor does this.
let parsePriceTable (j: Json) : Result<PriceTable, string> =
    let need k = strOpt k j |> Option.filter (fun s -> s.Trim() <> "")
    match need "source", need "version", need "effectiveDate", need "currency", need "unit" with
    | Some source, Some version, Some effective, Some currency, Some unit ->
        match parseTime effective with
        | None -> Error(sprintf "price table effectiveDate '%s' is not a date" effective)
        | Some _ when unit <> "per-million-tokens" -> Error(sprintf "price table unit '%s' is not per-million-tokens" unit)
        | Some at ->
            match field "models" j with
            | Some(JObj models) when not models.IsEmpty ->
                let rates =
                    models
                    |> List.map (fun (model, r) ->
                        model,
                        (match r with
                         | JObj ps -> ps |> List.choose (fun (k, v) -> number (Some v) |> Option.map (fun d -> k, d)) |> Map.ofList
                         | _ -> Map.empty))
                if rates |> List.exists (fun (_, r) -> r |> Map.exists (fun _ d -> d < 0M)) then Error "price table has a negative rate"
                else Ok { Source = source; Version = version; EffectiveDate = at; EffectiveText = effective; Currency = currency; Unit = unit; Models = Map.ofList rates }
            | _ -> Error "price table has no models"
    | _ -> Error "price table needs source, version, effectiveDate, currency and unit"

// ---- selection -----------------------------------------------------------------------------------------------------

let phaseOf (w: Window) (t: DateTimeOffset option) =
    match t with
    | None -> "unknown"
    | Some t when t < w.Start -> "pre-event"
    | Some t when t > w.End -> "post-event"
    | Some _ -> "timed"

/// The phase bucket of usage whose span crosses the window start or end. It is assigned to no phase: the records carry
/// sums without per-request times, so such usage is never split by proportion or duration.
let unsplit = "unsplit (crosses the window boundary)"

let private phaseRank = Map [ "pre-event", 0; "timed", 1; "post-event", 2; unsplit, 3; "unknown", 4 ]

/// The window boundaries a span from `s` to `f` crosses: those its two ends fall on either side of, by `phaseOf`.
let crossings (w: Window) (s: DateTimeOffset) (f: DateTimeOffset) =
    [ if s < w.Start && f >= w.Start then yield "window start"
      if s <= w.End && f > w.End then yield "window end" ]

/// The phase of usage known to lie between `s` and `f`: the phase of both ends, or `unsplit` when they differ.
let spanPhase (w: Window) (s: DateTimeOffset, f: DateTimeOffset) =
    if (crossings w s f).IsEmpty then phaseOf w (Some s) else unsplit

/// The transcript sessions the record dates (rawTelemetry payload startedAt and endedAt both present).
let private datedSessions (e: Execution) =
    e.Sessions |> List.choose (fun (snap, s, f) -> match s, f with Some s, Some f -> Some(snap, (s, f)) | _ -> None)

/// The execution's span: startedAt to finalizedAt, widened to every session its record dates, since a measurement of
/// the whole execution includes those sessions. None when the record has no startedAt or no finalizedAt.
let executionSpan (e: Execution) =
    match e.StartedAt, e.FinalizedAt with
    | Some s, Some f ->
        let spans = (s, f) :: (datedSessions e |> List.map snd)
        Some(spans |> List.map fst |> List.min, spans |> List.map snd |> List.max)
    | _ -> None

/// A measurement's span: its session's, when the record dates that session; otherwise its execution's.
let measurementSpan (e: Execution) (m: Measurement) =
    match m.Snapshot |> Option.bind (fun snap -> datedSessions e |> List.tryFind (fst >> (=) snap)) with
    | Some(_, span) -> Some span
    | None -> executionSpan e

/// A measurement's phase: by its span when known; otherwise by its collectedAt, else the execution's startedAt.
let measurementPhase (w: Window) (e: Execution) (m: Measurement) =
    match measurementSpan e m with
    | Some span -> spanPhase w span
    | None -> phaseOf w (match m.CollectedAt with Some t -> Some t | None -> e.StartedAt)

type Excluded = { Measurement: Measurement; Reason: string }

/// Whether a measurement counts. A token measurement counts when it is an observed or derived whole number of tokens;
/// a provider-reported cost when it is an observed `cost.execution_total` (Praxis `costBasis: provider-reported`).
/// Anything estimated is excluded with its reason and never enters a total.
let private admit (m: Measurement) : Result<Measurement, string> option =
    if bucketOfMetric.ContainsKey m.MetricId then
        Some(
            if m.Quality <> "observed" && m.Quality <> "derived" then Error(sprintf "token value of quality '%s' is never counted" m.Quality)
            elif m.Unit <> "tokens" then Error(sprintf "unit '%s' is not tokens" m.Unit)
            elif m.Value < 0M || m.Value <> Math.Round m.Value then Error "not a whole, non-negative number of tokens"
            else Ok m)
    elif m.MetricId = "cost.execution_total" then
        Some(
            if m.Quality <> "observed" then Error(sprintf "cost of quality '%s' is not provider-reported" m.Quality)
            elif m.Currency.IsNone then Error "cost without a currency"
            elif m.Value < 0M then Error "negative cost"
            else Ok m)
    else None

/// One observation is counted once: a transcript snapshot (`dimensions.snapshot`, `claude-session-SESSION`) ingested into
/// several executions is counted once, at its largest value (a session's sums only grow, so a later ingest of the same
/// session contains the earlier one), in the execution holding it (ties: the ordinally first), and excluded, with the
/// reason, from the others.
let private dedupe (ms: Measurement list) : Measurement list * Excluded list =
    let key (m: Measurement) = m.MetricId, (match m.Snapshot with Some s -> "snapshot:" + s | None -> "measurement:" + m.MeasurementId + ":" + m.ExecutionId)
    ms
    |> List.groupBy key
    |> List.map (fun (_, group) ->
        let sorted =
            group
            |> List.sortWith (fun a b ->
                match compare b.Value a.Value with
                | 0 -> String.CompareOrdinal(a.ExecutionId, b.ExecutionId)
                | c -> c)
        let kept = List.head sorted
        kept,
        sorted
        |> List.tail
        |> List.map (fun m ->
            { Measurement = m
              Reason = sprintf "snapshot %s is counted once, in %s (its largest value)" (defaultArg m.Snapshot "?") kept.ExecutionId }))
    |> fun pairs -> pairs |> List.map fst, pairs |> List.collect snd

// ---- rows ----------------------------------------------------------------------------------------------------------

/// The counted measurements of one execution in one phase and step.
type Row =
    { Exec: Execution
      Phase: string
      Step: string
      Tokens: Map<string, decimal * bool>
      ProviderCost: (decimal * string) list
      Earliest: DateTimeOffset option }

let private rowsOf (w: Window) (e: Execution) (counted: Measurement list) : Row list =
    match counted with
    | [] ->
        let phase = match executionSpan e with Some span -> spanPhase w span | None -> phaseOf w e.StartedAt
        [ { Exec = e; Phase = phase; Step = outsideAnyStep; Tokens = Map.empty; ProviderCost = []; Earliest = None } ]
    | ms ->
        ms
        |> List.groupBy (fun m -> measurementPhase w e m, defaultArg m.Step outsideAnyStep)
        |> List.map (fun ((phase, step), group) ->
            let tokens =
                group
                |> List.choose (fun m -> bucketOfMetric.TryFind m.MetricId |> Option.map (fun b -> b, m))
                |> List.groupBy fst
                |> List.map (fun (b, xs) -> b, (xs |> List.sumBy (fun (_, m) -> m.Value), xs |> List.exists (fun (_, m) -> m.LowerBound)))
                |> Map.ofList
            { Exec = e
              Phase = phase
              Step = step
              Tokens = tokens
              ProviderCost = group |> List.filter (fun m -> m.MetricId = "cost.execution_total") |> List.map (fun m -> m.Value, defaultArg m.Currency "")
              Earliest =
                match group |> List.choose (fun m -> m.CollectedAt) with
                | [] -> e.StartedAt
                | ts -> Some(List.min ts) })

/// The model a price applies to: the single served model of the execution's transcript snapshots, else the record's
/// identity model. Several served models cannot be priced per execution.
let effectiveModel (e: Execution) =
    match e.ServedModels with
    | [ one ] -> one
    | [] -> e.Model
    | _ -> "(several)"

let private priceRow (table: PriceTable) (row: Row) : Result<decimal * bool, string> =
    let model = effectiveModel row.Exec
    if model = "(several)" then Error "the execution's sessions served several models; price them per request with `praxis telemetry usage --price-table`"
    elif model = notRecorded then Error "the record names no model"
    else
        match table.Models.TryFind model with
        | None -> Error(sprintf "model %s is not in the price table" model)
        | Some rates ->
            match row.Earliest with
            | Some t when t < table.EffectiveDate -> Error(sprintf "measurements predate the price table's effective date %s" table.EffectiveText)
            | _ ->
                buckets
                |> List.fold
                    (fun acc (b, metric) ->
                        match acc with
                        | Error e -> Error e
                        | Ok(sum, lower) ->
                            match row.Tokens.TryFind b, rates.TryFind rateOf.[b] with
                            | None, _ -> Error(sprintf "no %s measurement to price" metric)
                            | _, None -> Error(sprintf "the price table has no %s rate for %s" rateOf.[b] model)
                            | Some(tokens, lb), Some rate -> Ok(sum + tokens * rate / 1000000M, lower || lb))
                    (Ok(0M, false))

// ---- report --------------------------------------------------------------------------------------------------------

let private ordinal (xs: string list) = xs |> List.distinct |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))
let private ids (es: Execution list) = es |> List.map _.Id |> ordinal |> jstrs
let private money (d: decimal) = JStr(Math.Round(d, 6, MidpointRounding.ToEven).ToString("0.000000", CultureInfo.InvariantCulture))

let private availability (hasValue: bool) (missing: bool) =
    if not hasValue && missing then "unavailable" elif missing then "partial" else "available"

/// Per-execution availability of a bucket across all of its rows.
let private hasBucket (rows: Row list) (e: Execution) (b: string) =
    rows |> List.exists (fun r -> r.Exec.Id = e.Id && r.Tokens.ContainsKey b)

let private hasProviderCost (rows: Row list) (e: Execution) =
    rows |> List.exists (fun r -> r.Exec.Id = e.Id && not r.ProviderCost.IsEmpty)

/// A group's figures. A bucket's value is the sum of its measurements in the group; an execution of the group with no
/// measurement of that bucket anywhere is listed in `unavailableExecutions`, and the value is then `partial` (a lower
/// bound) or `unavailable` (null), never zero. `total` is input + output + cache read + cache write, derived only from
/// those four buckets.
let private groupFigures (allRows: Row list) (table: PriceTable option) (rows: Row list) : Json =
    let execs = rows |> List.map _.Exec |> List.distinctBy _.Id
    let bucketJson (b: string) =
        let values = rows |> List.choose (fun r -> r.Tokens.TryFind b)
        let missing = execs |> List.filter (fun e -> not (hasBucket allRows e b))
        let avail = availability (not values.IsEmpty) (not missing.IsEmpty)
        let sum = values |> List.sumBy fst
        avail,
        (if values.IsEmpty then None else Some sum),
        (values |> List.exists snd) || avail = "partial",
        missing,
        JObj
            [ "availability", JStr avail
              "value", (if values.IsEmpty then JNull else JInt(int64 sum))
              "lowerBound", JBool((values |> List.exists snd) || avail = "partial")
              "unavailableExecutions", ids missing ]
    let parts = buckets |> List.map (fun (b, _) -> b, bucketJson b)
    let total =
        let avails = parts |> List.map (fun (_, (a, _, _, _, _)) -> a)
        let values = parts |> List.choose (fun (_, (_, v, _, _, _)) -> v)
        let missing = parts |> List.collect (fun (_, (_, _, _, m, _)) -> m) |> List.distinctBy _.Id
        let avail =
            if avails |> List.forall ((=) "available") then "available"
            elif values.IsEmpty then "unavailable"
            else "partial"
        JObj
            [ "availability", JStr avail
              "value", (if values.IsEmpty then JNull else JInt(int64 (List.sum values)))
              "lowerBound", JBool(parts |> List.exists (fun (_, (_, _, lb, _, _)) -> lb) || avail = "partial")
              "unavailableExecutions", ids missing
              "derivation", JStr "input + output + cacheRead + cacheWrite" ]
    let provider =
        let values = rows |> List.collect _.ProviderCost
        let currencies = values |> List.map snd |> ordinal
        let missing = execs |> List.filter (fun e -> not (hasProviderCost allRows e))
        match currencies with
        | _ :: _ :: _ ->
            JObj
                [ "availability", JStr "unavailable"; "amount", JNull; "currency", JNull
                  "reason", JStr(sprintf "mixed currencies: %s" (String.concat ", " currencies))
                  "unavailableExecutions", ids missing ]
        | _ ->
            let avail = availability (not values.IsEmpty) (not missing.IsEmpty)
            JObj
                [ "availability", JStr avail
                  "amount", (if values.IsEmpty then JNull else money (values |> List.sumBy fst))
                  "currency", (match currencies with [ c ] -> JStr c | _ -> JNull)
                  "lowerBound", JBool(avail = "partial")
                  "unavailableExecutions", ids missing
                  "basis", JStr "provider-reported: observed cost.execution_total" ]
    let derived =
        match table with
        | None ->
            JObj
                [ "availability", JStr "unavailable"; "amount", JNull; "currency", JNull
                  "reason", JStr "no operator-supplied price table (--price-table)"
                  "unavailableExecutions", ids execs ]
        | Some t ->
            let priced = rows |> List.map (fun r -> r, priceRow t r)
            let ok = priced |> List.choose (fun (_, p) -> match p with Ok v -> Some v | Error _ -> None)
            let missing = priced |> List.choose (fun (r, p) -> match p with Error _ -> Some r.Exec | Ok _ -> None) |> List.distinctBy _.Id
            let avail = availability (not ok.IsEmpty) (not missing.IsEmpty)
            JObj
                [ "availability", JStr avail
                  "amount", (if ok.IsEmpty then JNull else money (ok |> List.sumBy fst))
                  "currency", JStr t.Currency
                  "lowerBound", JBool((ok |> List.exists snd) || avail = "partial")
                  "unavailableExecutions", ids missing
                  "basis", JStr(sprintf "estimated:%s@%s/%s" t.Source t.Version t.EffectiveText) ]
    JObj
        [ "executions", ids execs
          "tokens", JObj((parts |> List.map (fun (b, (_, _, _, _, j)) -> b, j)) @ [ "total", total ])
          "cost", JObj [ "providerReported", provider; "priceTableDerived", derived ] ]

let private grouped (allRows: Row list) (table: PriceTable option) (keyOf: Row -> string) (sortKey: string -> string) (rows: Row list) =
    rows
    |> List.groupBy keyOf
    |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(sortKey a, sortKey b))
    |> List.map (fun (k, rs) ->
        match groupFigures allRows table rs with
        | JObj ps -> JObj(("key", JStr k) :: ps)
        | j -> j)
    |> JArr

type Outcome =
    | Abandoned of string
    | Failed of (string * string) list
    | Unfinalized
    | Finalized

let outcomeOf (e: Execution) =
    match e.Abandoned, e.FailedSteps with
    | Some reason, _ -> Abandoned reason
    | None, (_ :: _ as steps) -> Failed steps
    | None, [] when e.Status <> "finalized" -> Unfinalized
    | _ -> Finalized

let private outcomeText =
    function
    | Abandoned _ -> "abandoned"
    | Failed _ -> "failed"
    | Unfinalized -> "unfinalized"
    | Finalized -> "finalized"

/// Executions of a work item in start order; every one after the first repeats the work item.
let private attempts (execs: Execution list) =
    execs
    |> List.groupBy _.WorkItem
    |> List.collect (fun (_, es) ->
        es
        |> List.sortWith (fun a b ->
            match compare a.StartedAt b.StartedAt with
            | 0 -> String.CompareOrdinal(a.Id, b.Id)
            | c -> c)
        |> List.mapi (fun i e -> e.Id, (i + 1, es.Length)))
    |> Map.ofList

let private unavailableReason (e: Execution) (metric: string) =
    match e.Capabilities |> List.tryFind (fun c -> c.MetricId = metric) with
    | Some c -> c.Status, c.Reason
    | None -> "not-recorded", "the record has neither a measurement nor a capability state for this metric"

let rules =
    [ "Token figures come only from tokens.* measurements of quality observed or derived in the Praxis execution records; never from elapsed time, commits, source lines or transcript size."
      "A value the records do not carry is unavailable, with the record's capability status and reason; it is never zero and never estimated."
      "A group value with some executions unavailable is partial: the sum of the others, a lower bound."
      "tokens.output from a Claude Code transcript is a lower bound (dimensions.bound = lower); any figure including it is marked lowerBound."
      "total = input + output + cacheRead + cacheWrite; cache read and cache write are never folded into input."
      "A transcript snapshot ingested into two executions is counted once, in the ordinally first execution."
      "Provider-reported cost is the sum of observed cost.execution_total; price-table-derived cost prices the token buckets with an operator-supplied table. They are separate figures and are never summed."
      "The runtime's own cost estimate (cost.session_cumulative, quality estimated) is listed per execution and enters neither cost figure."
      "Phase: a measurement is pre-event, timed or post-event by its span against the competition window, both ends inclusive. A measurement of a transcript session the record dates (start and end) spans that session; any other measurement spans its execution, startedAt to finalizedAt, widened to every session the record dates. A measurement with no known span (no finalizedAt) is placed by its collectedAt, else the execution's startedAt; an execution with no measurements is placed by its span, else its startedAt."
      "A measurement whose span crosses the window start or end is in the phase bucket 'unsplit (crosses the window boundary)', which is assigned to no phase, and its span is listed under phaseCaveats: the record's sums carry no per-request times, so it is never split by proportion or duration. Split a session with praxis telemetry reliability-report --transcripts DIR."
      "Repeated work: every execution of a work item after the first (by startedAt) repeats it." ]

/// The dated sessions, and the executions' own startedAt to finalizedAt, that cross a window boundary: the measurements
/// they span are in the `unsplit` bucket and cannot be split by phase from the record.
let private phaseCaveats (w: Window) (execs: Execution list) =
    let stamp (t: DateTimeOffset) = t.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
    execs
    |> List.collect (fun e ->
        let own =
            match e.StartedAt, e.FinalizedAt with
            | Some s, Some f -> [ "execution", None, (s, f) ]
            | _ -> []
        own @ (datedSessions e |> List.map (fun (snap, span) -> "session", Some snap, span))
        |> List.choose (fun (kind, snap, (s, f)) ->
            match crossings w s f with
            | [] -> None
            | boundary ->
                Some(
                    JObj
                        [ "executionId", JStr e.Id
                          "span", JStr kind
                          "snapshot", jopt JStr snap
                          "startedAt", JStr(stamp s)
                          "endedAt", JStr(stamp f)
                          "spans", jstrs boundary
                          "bucket", JStr unsplit ]
                )))

/// The report over all records. `records` are (file name, SHA-256, parsed record).
let build (window: Window) (table: PriceTable option) (records: (string * string * Json) list) : Json =
    let execs =
        records
        |> List.map (fun (f, sha, j) -> parseExecution f sha j)
        |> List.sortWith (fun a b -> String.CompareOrdinal(a.Id, b.Id))
    let admitted = execs |> List.collect (fun e -> e.Measurements |> List.choose (fun m -> admit m |> Option.map (fun r -> m, r)))
    let rejected = admitted |> List.choose (fun (m, r) -> match r with Error reason -> Some { Measurement = m; Reason = reason } | Ok _ -> None)
    let counted, duplicates = admitted |> List.choose (fun (_, r) -> match r with Ok m -> Some m | Error _ -> None) |> dedupe
    let rows = execs |> List.collect (fun e -> rowsOf window e (counted |> List.filter (fun m -> m.ExecutionId = e.Id)))
    let attempt = attempts execs
    let figures = groupFigures rows table
    let by keyOf = grouped rows table keyOf id rows
    let excludedJson (x: Excluded) =
        JObj
            [ "executionId", JStr x.Measurement.ExecutionId
              "measurementId", JStr x.Measurement.MeasurementId
              "metric", JStr x.Measurement.MetricId
              "quality", JStr x.Measurement.Quality
              "reason", JStr x.Reason ]
    let execJson (e: Execution) =
        let own = rows |> List.filter (fun r -> r.Exec.Id = e.Id)
        let n, of' = attempt.[e.Id]
        let runtimeEstimates =
            e.Measurements
            |> List.filter (fun m -> m.MetricId = "cost.session_cumulative")
            |> List.map (fun m ->
                JObj
                    [ "amount", money m.Value
                      "currency", jopt JStr m.Currency
                      "quality", JStr m.Quality
                      "source", JStr m.Source
                      "note", JStr "the runtime's own estimate; not provider-reported and not price-table-derived" ])
        let tokenJson (b: string, metric: string) =
            let values = own |> List.choose (fun r -> r.Tokens.TryFind b)
            let ms = counted |> List.filter (fun m -> m.ExecutionId = e.Id && m.MetricId = metric)
            if values.IsEmpty then
                let status, reason = unavailableReason e metric
                b, JObj [ "availability", JStr "unavailable"; "value", JNull; "capabilityStatus", JStr status; "reason", JStr reason ]
            else
                b,
                JObj
                    [ "availability", JStr "available"
                      "value", JInt(int64 (values |> List.sumBy fst))
                      "lowerBound", JBool(values |> List.exists snd)
                      "quality", jstrs (ms |> List.map _.Quality |> ordinal)
                      "source", jstrs (ms |> List.map _.Source |> ordinal) ]
        let price =
            match table with
            | None -> JObj [ "availability", JStr "unavailable"; "reason", JStr "no operator-supplied price table (--price-table)" ]
            | Some t ->
                match own |> List.map (priceRow t) |> List.choose (function Error r -> Some r | Ok _ -> None) with
                | [] ->
                    let ok = own |> List.map (priceRow t) |> List.choose (function Ok v -> Some v | Error _ -> None)
                    JObj
                        [ "availability", JStr "available"
                          "amount", money (ok |> List.sumBy fst)
                          "currency", JStr t.Currency
                          "lowerBound", JBool(ok |> List.exists snd) ]
                | reasons -> JObj [ "availability", JStr "unavailable"; "reason", JStr(reasons |> ordinal |> String.concat "; ") ]
        let provider =
            match own |> List.collect _.ProviderCost with
            | [] ->
                let status, reason = unavailableReason e "cost.execution_total"
                JObj [ "availability", JStr "unavailable"; "capabilityStatus", JStr status; "reason", JStr reason ]
            | vs when (vs |> List.map snd |> ordinal |> List.length) = 1 ->
                JObj [ "availability", JStr "available"; "amount", money (vs |> List.sumBy fst); "currency", JStr(snd vs.Head) ]
            | _ -> JObj [ "availability", JStr "unavailable"; "reason", JStr "mixed currencies" ]
        JObj
            [ "executionId", JStr e.Id
              "record", JObj [ "file", JStr e.File; "sha256", JStr e.Sha256 ]
              "workItemId", JStr e.WorkItem
              "attempt", jint n
              "attemptsOfWorkItem", jint of'
              "status", JStr e.Status
              "outcome", JStr(outcomeText (outcomeOf e))
              "startedAt", jopt JStr e.StartedText
              "finalizedAt", jopt JStr e.FinalizedText
              "phases", own |> List.map _.Phase |> List.distinct |> List.sortBy (fun p -> phaseRank.[p]) |> jstrs
              "provider", JStr e.Provider
              "model", JStr e.Model
              "servedModels", jstrs e.ServedModels
              "runtime", JStr e.Runtime
              "agent", JStr e.Agent
              "role", JStr e.Role
              "actorKind", JStr e.ActorKind
              "parentExecutionId", jopt JStr e.ParentExecutionId
              "tokens", JObj(buckets |> List.map tokenJson)
              "cost",
              JObj
                  [ "providerReported", provider
                    "priceTableDerived", price
                    "runtimeEstimates", JArr runtimeEstimates ] ]
    let missingOf (e: Execution) =
        buckets
        |> List.filter (fun (b, _) -> not (hasBucket rows e b))
        |> List.map (fun (_, metric) ->
            let status, reason = unavailableReason e metric
            JObj [ "metric", JStr metric; "capabilityStatus", JStr status; "reason", JStr reason ])
    let tokenState (e: Execution) =
        match buckets |> List.filter (fun (b, _) -> hasBucket rows e b) |> List.length with
        | n when n = buckets.Length -> "complete"
        | 0 -> "none"
        | _ -> "partial"
    let withState s = execs |> List.filter (fun e -> tokenState e = s)
    let repeatedItems =
        execs
        |> List.groupBy _.WorkItem
        |> List.filter (fun (_, es) -> es.Length > 1)
        |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b))
    let isRework (e: Execution) = outcomeOf e <> Finalized || fst attempt.[e.Id] > 1
    let reworkRows = rows |> List.filter (fun r -> isRework r.Exec)
    JObj
        [ "schema", JStr "indy-init.token-economics/1"
          "source",
          JObj
              [ "telemetry", JStr "Praxis execution records (.ros/telemetry/executions, telemetry schema 1.0.0)"
                "records", JArr(execs |> List.map (fun e -> JObj [ "file", JStr e.File; "sha256", JStr e.Sha256 ])) ]
          "window", JObj [ "start", JStr(window.Start.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)); "end", JStr(window.End.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)) ]
          "priceTable",
          (match table with
           | None -> JNull
           | Some t ->
               JObj
                   [ "source", JStr t.Source
                     "version", JStr t.Version
                     "effectiveDate", JStr t.EffectiveText
                     "currency", JStr t.Currency
                     "unit", JStr t.Unit
                     "models", jstrs (t.Models |> Map.toList |> List.map fst |> ordinal) ])
          "rules", jstrs rules
          "totals", figures rows
          "byPhase", grouped rows table _.Phase (fun p -> string phaseRank.[p]) rows
          "phaseCaveats", JArr(phaseCaveats window execs)
          "byWorkItem", by _.Exec.WorkItem
          "byAgent", by _.Exec.Agent
          "byRole", by _.Exec.Role
          "byProvider", by _.Exec.Provider
          "byModel", by (fun r -> effectiveModel r.Exec)
          "byStep", by _.Step
          "completeness",
          JObj
              [ "executions", jint execs.Length
                "tokensComplete", ids (withState "complete")
                "tokensPartial", JArr(withState "partial" |> List.map (fun e -> JObj [ "executionId", JStr e.Id; "missing", JArr(missingOf e) ]))
                "tokensUnavailable", JArr(withState "none" |> List.map (fun e -> JObj [ "executionId", JStr e.Id; "missing", JArr(missingOf e) ]))
                "providerCostReported", ids (execs |> List.filter (hasProviderCost rows)) ]
          "failedOrRepeated",
          JObj
              [ "abandoned",
                JArr(
                    execs
                    |> List.choose (fun e ->
                        match outcomeOf e with
                        | Abandoned r -> Some(JObj [ "executionId", JStr e.Id; "workItemId", JStr e.WorkItem; "reason", JStr r ])
                        | _ -> None)
                )
                "failed",
                JArr(
                    execs
                    |> List.choose (fun e ->
                        match outcomeOf e with
                        | Failed steps ->
                            Some(
                                JObj
                                    [ "executionId", JStr e.Id
                                      "workItemId", JStr e.WorkItem
                                      "failedSteps", JArr(steps |> List.map (fun (s, r) -> JObj [ "step", JStr s; "reason", JStr r ])) ]
                            )
                        | _ -> None)
                )
                "unfinalized",
                JArr(
                    execs
                    |> List.filter (fun e -> outcomeOf e = Unfinalized)
                    |> List.map (fun e -> JObj [ "executionId", JStr e.Id; "workItemId", JStr e.WorkItem; "status", JStr e.Status ])
                )
                "repeatedWorkItems",
                JArr(
                    repeatedItems
                    |> List.map (fun (wi, es) ->
                        let ordered = es |> List.sortBy (fun e -> fst attempt.[e.Id])
                        JObj
                            [ "workItemId", JStr wi
                              "executions", jstrs (ordered |> List.map _.Id)
                              "repeats", jstrs (ordered |> List.tail |> List.map _.Id) ])
                )
                "figures", (if reworkRows.IsEmpty then JNull else figures reworkRows) ]
          "executions", JArr(execs |> List.map execJson)
          "excludedMeasurements", JArr((rejected @ duplicates) |> List.sortWith (fun a b -> String.CompareOrdinal(a.Measurement.ExecutionId + a.Measurement.MeasurementId, b.Measurement.ExecutionId + b.Measurement.MeasurementId)) |> List.map excludedJson) ]

// ---- Markdown ------------------------------------------------------------------------------------------------------

let private cell (j: Json option) =
    let lb (o: Json) = if boolOr "lowerBound" false o then " (lower bound)" else ""
    match j with
    | Some o ->
        match strOpt "availability" o, field "value" o, field "amount" o with
        | Some "unavailable", _, _ -> "unavailable"
        | Some a, Some(JInt v), _ -> sprintf "%s%s%s" (v.ToString("N0", CultureInfo.InvariantCulture)) (lb o) (if a = "partial" then ", partial" else "")
        | Some a, _, Some(JStr amount) ->
            sprintf "%s %s%s%s" amount (defaultArg (strOpt "currency" o) "") (lb o) (if a = "partial" then ", partial" else "")
        | _ -> "unavailable"
    | None -> ""

let private figureRow (label: string) (g: Json) =
    let t = obj "tokens" g
    let c = obj "cost" g
    let n = arr "executions" g |> List.length
    sprintf "| %s | %d | %s | %s | %s | %s | %s | %s | %s |" label n
        (cell (field "input" t)) (cell (field "output" t)) (cell (field "cacheRead" t)) (cell (field "cacheWrite" t)) (cell (field "total" t))
        (cell (field "providerReported" c)) (cell (field "priceTableDerived" c))

let private header =
    [ "| Group | Executions | Input | Output | Cache read | Cache write | Total | Provider-reported cost | Price-table cost |"
      "|---|---|---|---|---|---|---|---|---|" ]

let private table (title: string) (groups: Json list) =
    [ sprintf "## %s" title; "" ] @ header @ (groups |> List.map (fun g -> figureRow (textOr "" "key" g) g)) @ [ "" ]

/// The Markdown companion of the JSON report: the same figures, rendered from it.
let markdown (report: Json) : string =
    let w = obj "window" report
    let completeness = obj "completeness" report
    let fr = obj "failedOrRepeated" report
    let pt = obj "priceTable" report
    let missingLine (x: Json) =
        let parts =
            arr "missing" x
            |> List.map (fun m -> sprintf "%s %s (%s)" (str "metric" m) (str "capabilityStatus" m) (str "reason" m))
        sprintf "- %s: %s" (str "executionId" x) (String.concat "; " parts)
    let lines =
        [ "# Token economics"
          ""
          "Generated by `requirements-compiler token-economics` from the Praxis execution records; regenerate, do not edit."
          ""
          sprintf "- Competition window: %s to %s" (str "start" w) (str "end" w)
          sprintf "- Execution records: %d" (arr "records" (obj "source" report) |> List.length)
          (match pt with
           | JNull -> "- Price table: none supplied; price-table-derived cost is unavailable"
           | t -> sprintf "- Price table: %s, version %s, effective %s, %s %s (operator-supplied)" (str "source" t) (str "version" t) (str "effectiveDate" t) (str "currency" t) (str "unit" t))
          ""
          "## Totals"
          "" ]
        @ header
        @ [ figureRow "all executions" (obj "totals" report); "" ]
        @ table "Timed versus pre-event" (arr "byPhase" report)
        @ (match arr "phaseCaveats" report with
           | [] -> []
           | xs ->
               [ sprintf "Spans that cross a window boundary. What they span is in the bucket `%s`, which is assigned to no phase and never split by proportion or duration; `praxis telemetry reliability-report --transcripts DIR` splits a session by its requests' own times:" unsplit; "" ]
               @ (xs
                  |> List.map (fun x ->
                      let what = match strOpt "snapshot" x with Some snap -> "session " + snap | None -> "execution"
                      sprintf "- %s, %s: %s to %s, spans the %s; in `%s`" (str "executionId" x) what
                          (str "startedAt" x) (str "endedAt" x) (String.concat " and " (strs "spans" x)) (str "bucket" x)))
               @ [ "" ])
        @ table "By work item" (arr "byWorkItem" report)
        @ table "By agent" (arr "byAgent" report)
        @ table "By role" (arr "byRole" report)
        @ table "By provider" (arr "byProvider" report)
        @ table "By model" (arr "byModel" report)
        @ table "By step" (arr "byStep" report)
        @ [ "## Completeness"
            ""
            sprintf "- Executions: %d" (match field "executions" completeness with Some(JInt n) -> n | _ -> 0L)
            sprintf "- Token data complete (all four buckets): %s" (match strs "tokensComplete" completeness with [] -> "none" | xs -> String.concat ", " xs)
            sprintf "- Provider-reported cost recorded: %s" (match strs "providerCostReported" completeness with [] -> "none" | xs -> String.concat ", " xs)
            ""
            "Partial token data:"
            "" ]
        @ (match arr "tokensPartial" completeness with [] -> [ "- none" ] | xs -> xs |> List.map missingLine)
        @ [ ""; "No token data:"; "" ]
        @ (match arr "tokensUnavailable" completeness with [] -> [ "- none" ] | xs -> xs |> List.map missingLine)
        @ [ ""; "## Failed, abandoned, unfinalized and repeated work"; "" ]
        @ (arr "abandoned" fr |> List.map (fun x -> sprintf "- Abandoned: %s (%s): %s" (str "executionId" x) (str "workItemId" x) (str "reason" x)))
        @ (arr "failed" fr
           |> List.map (fun x ->
               sprintf "- Failed: %s (%s): %s" (str "executionId" x) (str "workItemId" x)
                   (arr "failedSteps" x |> List.map (fun s -> sprintf "step %s, %s" (str "step" s) (str "reason" s)) |> String.concat "; ")))
        @ (arr "unfinalized" fr |> List.map (fun x -> sprintf "- Unfinalized: %s (%s), status %s" (str "executionId" x) (str "workItemId" x) (str "status" x)))
        @ (arr "repeatedWorkItems" fr |> List.map (fun x -> sprintf "- Repeated: %s, executions %s; repeats %s" (str "workItemId" x) (String.concat ", " (strs "executions" x)) (String.concat ", " (strs "repeats" x))))
        @ (match field "figures" fr with
           | Some(JObj _ as g) -> [ "" ] @ header @ [ figureRow "failed, abandoned, unfinalized or repeated" g ]
           | _ -> [ "- none" ])
        @ [ ""; "## Rules"; "" ]
        @ (strs "rules" report |> List.map (sprintf "- %s"))
        @ (match arr "excludedMeasurements" report with
           | [] -> []
           | xs ->
               [ ""; "## Excluded measurements"; "" ]
               @ (xs |> List.map (fun x -> sprintf "- %s %s (%s): %s" (str "executionId" x) (str "metric" x) (str "quality" x) (str "reason" x))))
    (lines |> String.concat "\n") + "\n"
