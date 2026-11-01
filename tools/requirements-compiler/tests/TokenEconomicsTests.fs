/// Token economics (Amendment D): the recorded fixture (tests/fixtures/token-economics, Praxis-shaped execution records
/// labelled as a fixture) produces the committed token-economics.json and .md byte for byte; totals add up across every
/// grouping; missing data is unavailable with the record's reason, never zero; no token figure comes from a non-token
/// input; estimated values, duplicate snapshots and price tables are handled as the rules state.
module TokenEconomicsTests

open System
open System.IO
open Json

let private fixtureDir (source: string) = Path.Combine(source, "tools", "requirements-compiler", "tests", "fixtures", "token-economics")

let private window: TokenEconomics.Window =
    { Start = DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero); End = DateTimeOffset(2026, 1, 10, 19, 0, 0, TimeSpan.Zero) }

let private records (source: string) =
    Directory.GetFiles(Path.Combine(fixtureDir source, "repo", ".ros", "telemetry", "executions"), "*.json")
    |> Array.sortWith (fun a b -> String.CompareOrdinal(a, b))
    |> Array.map (fun f -> let b = File.ReadAllBytes f in Path.GetFileName f, Git.sha256Hex b, parseBytes b)
    |> List.ofArray

let private table (source: string) =
    match TokenEconomics.parsePriceTable (parseFile (Path.Combine(fixtureDir source, "price-table.json"))) with
    | Ok t -> t
    | Error e -> failwithf "fixture price table refused: %s" e

let rec private at (path: string list) (j: Json) =
    match path with
    | [] -> j
    | k :: rest -> at rest (field k j |> Option.defaultValue JNull)

let private intAt path j = match at path j with JInt i -> Some i | _ -> None

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps when ps |> List.exists (fst >> (=) key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | JObj ps -> JObj(ps @ [ key, value ])
    | j -> j

let private mapRecord (id: string) (f: Json -> Json) (rs: (string * string * Json) list) =
    rs |> List.map (fun (file, sha, j) -> if strOpt "executionId" j = Some id then file, sha, f j else file, sha, j)

let private f02 = "EXE-20260110T120500000Z-f0000002"
let private f03 = "EXE-20260110T121000000Z-f0000003"
let private f05 = "EXE-20260110T150000000Z-f0000005"

let private metric (id: string) (value: Json) (unit: string) (quality: string) (sourceType: string) =
    JObj
        [ "measurementId", JStr("MEAS-test-" + id); "id", JStr id; "value", value; "unit", JStr unit; "quality", JStr quality
          "dimensions", JObj []; "source", JObj [ "type", JStr sourceType; "name", JStr "test"; "mechanism", JStr "test" ]
          "collectedAt", JStr "2026-01-10T13:00:00.000Z" ]

let private bucketNames = [ "input"; "output"; "cacheRead"; "cacheWrite" ]

/// Every grouping adds up to the totals, the per-execution figures add up to the totals, and total is the sum of the
/// four buckets.
let private addsUp (label: string) (report: Json) : string list =
    let totals = field "totals" report |> Option.defaultValue JNull
    let sumOf (groups: Json list) b = groups |> List.choose (intAt [ "tokens"; b; "value" ]) |> List.sum
    [ for g in [ "byPhase"; "byWorkItem"; "byAgent"; "byRole"; "byProvider"; "byModel"; "byStep" ] do
          for b in bucketNames @ [ "total" ] do
              let expected = intAt [ "tokens"; b; "value" ] totals |> Option.defaultValue 0L
              if sumOf (arr g report) b <> expected then yield sprintf "%s: %s %s does not add up to the total %d" label g b expected
      for b in bucketNames do
          let perExecution = arr "executions" report |> List.choose (intAt [ "tokens"; b; "value" ]) |> List.sum
          if perExecution <> (intAt [ "tokens"; b; "value" ] totals |> Option.defaultValue 0L) then
              yield sprintf "%s: per-execution %s (%d) does not add up to the total" label b perExecution
      let four = bucketNames |> List.choose (fun b -> intAt [ "tokens"; b; "value" ] totals) |> List.sum
      if intAt [ "tokens"; "total"; "value" ] totals <> Some four && four <> 0L then yield sprintf "%s: total is not input + output + cacheRead + cacheWrite" label ]

let all (source: string) : string list =
    let rs = records source
    let t = table source
    let build = TokenEconomics.build window
    let report = build (Some t) rs
    let execution (r: Json) id = arr "executions" r |> List.tryFind (fun e -> strOpt "executionId" e = Some id) |> Option.defaultValue JNull
    let totalsOf (r: Json) b = intAt [ "totals"; "tokens"; b; "value" ] r

    let golden =
        match Program.tokenEconomicsReport (Path.Combine(fixtureDir source, "repo")) window (Some(Path.Combine(fixtureDir source, "price-table.json"))) with
        | Error e -> [ "fixture: " + e ]
        | Ok(json, md) ->
            [ if json <> File.ReadAllText(Path.Combine(fixtureDir source, "expected", "token-economics.json")) then
                  yield "fixture: token-economics.json differs from expected/token-economics.json"
              if md <> File.ReadAllText(Path.Combine(fixtureDir source, "expected", "token-economics.md")) then
                  yield "fixture: token-economics.md differs from expected/token-economics.md" ]

    let figures =
        [ if totalsOf report "input" <> Some 610L then yield "the fixture's input total is not 610 (the estimated 999 must not count)"
          if totalsOf report "cacheWrite" <> Some 34000L then yield "the fixture's cache-write total is not 34000"
          if at [ "totals"; "tokens"; "cacheWrite"; "availability" ] report <> JStr "partial" then yield "cache write with an unavailable execution is not partial"
          if at [ "totals"; "cost"; "providerReported"; "amount" ] report <> JStr "1.250000" then yield "provider-reported cost is not the observed cost.execution_total"
          if at [ "totals"; "cost"; "priceTableDerived"; "amount" ] report <> JStr "0.341040" then yield "price-table cost is not the priced token buckets"
          let phases = arr "byPhase" report |> List.map (str "key")
          if phases <> [ "pre-event"; "timed"; TokenEconomics.unsplit ] then yield sprintf "phases are %A, not pre-event, timed and unsplit" phases
          if strs "repeats" (arr "repeatedWorkItems" (at [ "failedOrRepeated" ] report) |> List.head) <> [ f05 ] then yield "the repeat of WI-F2 is not reported"
          if arr "failed" (at [ "failedOrRepeated" ] report) |> List.length <> 1 then yield "the failed execution is not reported"
          if arr "abandoned" (at [ "failedOrRepeated" ] report) |> List.length <> 1 then yield "the abandoned execution is not reported"
          if arr "unfinalized" (at [ "failedOrRepeated" ] report) |> List.length <> 1 then yield "the unfinalized execution is not reported" ]

    let unavailable =
        let e3 = execution report f03
        [ for b in bucketNames do
              if at [ "tokens"; b; "value" ] e3 <> JNull then yield sprintf "missing %s is not null (zero or estimated)" b
              if at [ "tokens"; b; "availability" ] e3 <> JStr "unavailable" then yield sprintf "missing %s is not unavailable" b
              if at [ "tokens"; b; "capabilityStatus" ] e3 <> JStr "supported-unavailable" then yield sprintf "missing %s does not carry the record's capability status" b
          let wi = arr "byWorkItem" report |> List.find (fun g -> strOpt "key" g = Some "WI-F3")
          if at [ "tokens"; "total"; "value" ] wi <> JNull then yield "a work item with no token data has a total that is not null"
          let none = build (Some t) (rs |> List.map (fun (f, s, j) -> f, s, setIn "metrics" (JArr(arr "metrics" j |> List.filter (fun m -> not ((defaultArg (strOpt "id" m) "").StartsWith("tokens.", StringComparison.Ordinal))))) j))
          for b in bucketNames @ [ "total" ] do
              if at [ "totals"; "tokens"; b; "value" ] none <> JNull then yield sprintf "with no tokens.* measurement anywhere, total %s is not null" b
          if at [ "totals"; "cost"; "priceTableDerived"; "amount" ] none <> JNull then yield "with no tokens, price-table cost is not null"
          let noTable = build None rs
          if at [ "totals"; "cost"; "priceTableDerived"; "availability" ] noTable <> JStr "unavailable" then yield "without a price table, price-table cost is not unavailable"
          if at [ "totals"; "cost"; "providerReported"; "amount" ] noTable <> JStr "1.250000" then yield "provider-reported cost depends on the price table" ]
        |> List.map (sprintf "unavailable: %s")

    // No token figure is derived from non-token inputs: wall time, commits, lines, raw transcript bytes and an
    // estimated cost added to the execution with no token data leave every token figure unavailable and unchanged.
    let nonToken =
        let noisy =
            rs
            |> mapRecord f03 (fun j ->
                j
                |> setIn "metrics" (JArr(arr "metrics" j @ [ metric "time.active_ms" (JInt 9000000L) "milliseconds" "derived" "calculated";
                                                            metric "git.lines_deleted" (JInt 4000L) "lines" "derived" "ros-git";
                                                            metric "tool.calls" (JInt 300L) "count" "observed" "runtime-output";
                                                            metric "cost.execution_total" (JNum 3.5M) "currency" "estimated" "calculated" ]))
                |> setIn "rawTelemetry" (JArr [ JObj [ "snapshotId", JStr "test"; "payloadBytes", JInt 5000000L; "payload", JObj [ "transcriptCharacters", JInt 5000000L ] ] ]))
        let r = build (Some t) noisy
        [ for b in bucketNames do
              if at [ "tokens"; b; "value" ] (execution r f03) <> JNull then yield sprintf "%s became a value from non-token inputs" b
              if totalsOf r b <> totalsOf report b then yield sprintf "total %s changed when only non-token inputs changed" b
          if at [ "totals"; "cost"; "providerReported"; "amount" ] r <> JStr "1.250000" then yield "an estimated cost entered the provider-reported cost" ]
        |> List.map (sprintf "non-token inputs: %s")

    // A transcript snapshot ingested into two executions counts once.
    let duplicates =
        let copy = arr "metrics" (rs |> List.pick (fun (_, _, j) -> if strOpt "executionId" j = Some f02 then Some j else None)) |> List.find (fun m -> strOpt "id" m = Some "tokens.input")
        let r = build (Some t) (rs |> mapRecord f05 (fun j -> setIn "metrics" (JArr(arr "metrics" j @ [ copy ])) j))
        // The same session re-ingested later, with grown sums, counts once at its largest value, in the later execution.
        let grown = setIn "value" (JInt 450L) (setIn "measurementId" (JStr "MEAS-test-regrown") copy)
        let r2 = build (Some t) (rs |> mapRecord f05 (fun j -> setIn "metrics" (JArr(arr "metrics" j @ [ grown ])) j))
        [ if totalsOf r "input" <> totalsOf report "input" then yield "a snapshot ingested twice was counted twice"
          if List.length (arr "excludedMeasurements" r) <> List.length (arr "excludedMeasurements" report) + 1 then yield "the duplicate is not listed as excluded"
          if totalsOf r2 "input" <> Some 760L then yield "a re-ingested cumulative snapshot is not counted once at its largest value (610 - 300 + 450)"
          if at [ "tokens"; "input"; "value" ] (execution r2 f02) <> JNull then yield "the superseded smaller ingest still counts in its execution" ]
        |> List.map (sprintf "duplicates: %s")

    // A session that spans the window start is flagged, never split.
    let caveats =
        [ match arr "phaseCaveats" report with
          | [ c ] when strOpt "executionId" c = Some f02 && strs "spans" c = [ "window start" ] && strOpt "bucket" c = Some TokenEconomics.unsplit -> ()
          | cs -> yield sprintf "the session of %s spanning the window start is not the one caveat naming the unsplit bucket (found %d)" f02 cs.Length ]
        |> List.map (sprintf "phase caveats: %s")

    // A session whose span crosses a window boundary is in the unsplit bucket, never in timed; a session wholly inside
    // the window is timed whenever it was ingested.
    let unsplitPhase =
        let phase (r: Json) key = arr "byPhase" r |> List.tryFind (fun g -> strOpt "key" g = Some key) |> Option.defaultValue JNull
        let session (startedAt: string) (endedAt: string) (collectedAt: string) (finalizedAt: string) =
            rs
            |> mapRecord f02 (fun j ->
                j
                |> setIn "finalizedAt" (JStr finalizedAt)
                |> setIn "metrics" (JArr(arr "metrics" j |> List.map (setIn "collectedAt" (JStr collectedAt))))
                |> setIn "rawTelemetry" (JArr(arr "rawTelemetry" j |> List.map (fun snap -> setIn "payload" (field "payload" snap |> Option.defaultValue JNull |> setIn "startedAt" (JStr startedAt) |> setIn "endedAt" (JStr endedAt)) snap))))
            |> build (Some t)
        let u = phase report TokenEconomics.unsplit
        let timed = phase report "timed"
        let coordinator = session "2026-01-10T11:50:00.000Z" "2026-01-10T19:02:00.000Z" "2026-01-10T19:02:30.000Z" "2026-01-10T19:03:00.000Z"
        let inside = session "2026-01-10T12:06:00.000Z" "2026-01-10T13:58:30.000Z" "2026-01-10T19:30:00.000Z" "2026-01-10T13:59:30.000Z"
        [ if strs "executions" u <> [ f02 ] then yield sprintf "the unsplit bucket holds %A, not %s" (strs "executions" u) f02
          if intAt [ "tokens"; "input"; "value" ] u <> Some 300L || intAt [ "tokens"; "cacheRead"; "value" ] u <> Some 400000L then yield "the crossing session's tokens are not in the unsplit bucket"
          if strs "executions" timed |> List.contains f02 then yield "the crossing session's execution is in timed"
          if intAt [ "tokens"; "input"; "value" ] timed <> Some 210L || intAt [ "tokens"; "cacheRead"; "value" ] timed <> Some 190000L then yield "timed contains the crossing session's tokens"
          if strs "phases" (execution report f02) <> [ TokenEconomics.unsplit ] then yield "the crossing execution's phases are not unsplit alone"
          if intAt [ "tokens"; "input"; "value" ] (phase coordinator TokenEconomics.unsplit) <> Some 300L then yield "a session spanning the deadline, ingested after it, is not unsplit"
          if phase coordinator "post-event" <> JNull then yield "a session spanning the deadline, ingested after it, is post-event"
          if (arr "phaseCaveats" coordinator |> List.filter (fun c -> strOpt "span" c = Some "session") |> List.map (strs "spans")) <> [ [ "window start"; "window end" ] ] then yield "the session spanning both boundaries is not one caveat naming both"
          if intAt [ "tokens"; "input"; "value" ] (phase inside "timed") <> Some 510L || phase inside TokenEconomics.unsplit <> JNull then yield "a session wholly inside the window, ingested after it, is not timed"
          if TokenEconomics.spanPhase window (window.Start, window.End) <> "timed" then yield "the whole window is not timed"
          if TokenEconomics.spanPhase window (window.Start.AddSeconds -1.0, window.Start) <> TokenEconomics.unsplit then yield "a span ending at the window start is not unsplit"
          if TokenEconomics.spanPhase window (window.End, window.End.AddSeconds 1.0) <> TokenEconomics.unsplit then yield "a span starting at the window end is not unsplit" ]
        |> List.map (sprintf "unsplit: %s")

    let rules =
        [ if TokenEconomics.phaseOf window (Some(DateTimeOffset(2026, 1, 10, 19, 0, 1, TimeSpan.Zero))) <> "post-event" then yield "after the window is not post-event"
          if TokenEconomics.phaseOf window (Some window.Start) <> "timed" then yield "the window start is not timed"
          if TokenEconomics.phaseOf window (Some(window.Start.AddSeconds -1.0)) <> "pre-event" then yield "before the window is not pre-event"
          if build (Some t) (List.rev rs) <> report then yield "the report depends on the order of the records"
          match TokenEconomics.parsePriceTable (JObj [ "version", JStr "1"; "currency", JStr "USD"; "unit", JStr "per-million-tokens"; "models", JObj [ "m", JObj [ "input", JInt 1L ] ] ]) with
          | Ok _ -> yield "a price table without its source and effective date was accepted"
          | Error _ -> () ]
        |> List.map (sprintf "rules: %s")

    (golden @ figures @ addsUp "fixture" report @ addsUp "no price table" (build None rs) @ unavailable @ nonToken @ duplicates @ caveats @ unsplitPhase @ rules)
    |> List.map (sprintf "token economics: %s")
