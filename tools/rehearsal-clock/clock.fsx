// Rehearsal clock and checkpoints, computed rather than hand-written. Rehearsal 4 wrote its clock and checkpoint
// records by hand and recorded T+240 eight minutes late without anything noticing. Here the elapsed time, due time
// and lateness are computed from the clock, and `status` fails while a due checkpoint is unrecorded.
//
// Usage (DIR is the rehearsal repository's evidence/rehearsal):
//   dotnet fsi clock.fsx start      --dir DIR --start-utc ISO --basis TEXT [--ceiling 420] [--checkpoints 60,120,240,330,390,420]
//   dotnet fsi clock.fsx status     --dir DIR [--now ISO]
//   dotnet fsi clock.fsx checkpoint --dir DIR --name T+60 --head SHA --input FILE [--ledger FILE] [--now ISO]
//
// `checkpoint` copies the agent's narrative fields from --input (completedWork, blockedRequirements, ciStatus,
// currentDefects, tokenConsumption, remainingCriticalPath, ...) and computes recordedAtUtc, elapsedMinutes,
// dueAtUtc and lateMinutes itself; with --ledger (a causa.acceptance-ledger/2 file, provisional or final) it also
// counts P0 requirement statuses from the ledger instead of taking them from the narrative.
//
// Exit codes: 0 ok; 2 a due checkpoint is unrecorded (status) or the name is unknown/duplicate (checkpoint);
// 3 the clock is missing or past its ceiling with no END record.

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text.Json
open System.Text.Json.Nodes

type Clock = { Start: DateTimeOffset; Ceiling: int; Checkpoints: int list; Basis: string }

let parseArgs (argv: string list) =
    argv
    |> List.chunkBySize 2
    |> List.choose (function [ k: string; v ] when k.StartsWith "--" -> Some(k.Substring 2, v) | _ -> None)
    |> Map.ofList

let iso (t: DateTimeOffset) = t.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
let parseTime (s: string) = DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
let minutesBetween (a: DateTimeOffset) (b: DateTimeOffset) = int (Math.Floor((b - a).TotalMinutes))
let checkpointName minutes = $"T+{minutes}"
let dueAt clock minutes = clock.Start.AddMinutes(float minutes)

/// The checkpoints that are due at `now` and have no record yet; none once the run has ended (an END record).
let overdue clock (recorded: Set<string>) now =
    clock.Checkpoints
    |> List.filter (fun m -> not (recorded.Contains "END") && dueAt clock m <= now && not (recorded.Contains(checkpointName m)))

let readJson path = JsonNode.Parse(File.ReadAllText path)
let writeJson path (node: JsonNode) =
    File.WriteAllText(path, node.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")

let jv (s: string) = JsonValue.Create s :> JsonNode
let ji (i: int) = JsonValue.Create i :> JsonNode
let kv (k: string) (v: JsonNode) = KeyValuePair<string, JsonNode>(k, v)
let due (startUtc: DateTimeOffset) (m: int) = iso (startUtc.AddMinutes(float m))

let clockPath dir = Path.Combine(dir, "clock.json")
let checkpointsPath dir = Path.Combine(dir, "checkpoints.json")

let loadClock dir =
    let n = readJson (clockPath dir)
    { Start = parseTime (string (n["startUtc"]))
      Ceiling = (n["ceilingMinutes"]).GetValue<int>()
      Checkpoints = (n["checkpointMinutes"]).AsArray() |> Seq.map (fun x -> x.GetValue<int>()) |> List.ofSeq
      Basis = string (n["startBasis"]) }

let loadRecords dir =
    if File.Exists(checkpointsPath dir) then
        ((readJson (checkpointsPath dir))["checkpoints"]).AsArray() |> Seq.map (fun c -> c.DeepClone()) |> List.ofSeq
    else []

let recordedNames (records: JsonNode list) = records |> List.map (fun r -> string (r["checkpoint"])) |> Set.ofList

/// P0 status counts from an acceptance ledger: the numbers come from the ledger, never from the narrative.
let p0Counts (ledger: JsonNode) =
    (ledger["requirements"]).AsArray()
    |> Seq.filter (fun r -> string (r["priority"]) = "P0")
    |> Seq.countBy (fun r -> string (r["status"]))
    |> Seq.sortBy fst
    |> Seq.fold (fun (o: JsonObject) (k, v) -> o[k] <- JsonValue.Create v; o) (JsonObject())

let start (a: Map<string, string>) =
    let dir = a["dir"]
    let ceiling = a |> Map.tryFind "ceiling" |> Option.map int |> Option.defaultValue 420
    let checkpoints =
        a |> Map.tryFind "checkpoints" |> Option.defaultValue "60,120,240,330,390,420"
        |> fun s -> s.Split ',' |> Array.map int |> List.ofArray
    let startUtc = parseTime (a["start-utc"])
    if File.Exists(clockPath dir) then
        eprintfn "clock already started: %s" (clockPath dir); 2
    else
        Directory.CreateDirectory dir |> ignore
        let entry m =
            JsonObject([ kv "checkpoint" (jv (checkpointName m)); kv "dueAtUtc" (jv (due startUtc m)) ]) :> JsonNode
        let clock =
            JsonObject(
                [ kv "schema" (jv "indy-init.rehearsal-clock/1")
                  kv "startUtc" (jv (iso startUtc))
                  kv "startBasis" (jv (a["basis"]))
                  kv "ceilingMinutes" (ji ceiling)
                  kv "deadlineUtc" (jv (due startUtc ceiling))
                  kv "checkpointMinutes" (JsonArray(checkpoints |> List.map ji |> Array.ofList) :> JsonNode)
                  kv "schedule" (JsonArray(checkpoints |> List.map entry |> Array.ofList) :> JsonNode) ])
        writeJson (clockPath dir) clock
        printfn "clock started %s; deadline %s" (iso startUtc) (iso (startUtc.AddMinutes(float ceiling)))
        0

let status (a: Map<string, string>) =
    let dir = a["dir"]
    if not (File.Exists(clockPath dir)) then eprintfn "no clock at %s" (clockPath dir); 3
    else
        let clock = loadClock dir
        let now = a |> Map.tryFind "now" |> Option.map parseTime |> Option.defaultValue DateTimeOffset.UtcNow
        let records = loadRecords dir
        let names = recordedNames records
        let late = overdue clock names now
        let elapsed = minutesBetween clock.Start now
        printfn "elapsed %d of %d minutes (deadline %s)" elapsed clock.Ceiling (iso (clock.Start.AddMinutes(float clock.Ceiling)))
        late |> List.iter (fun m -> printfn "OVERDUE %s: due %s, %d minutes late" (checkpointName m) (iso (dueAt clock m)) (minutesBetween (dueAt clock m) now))
        clock.Checkpoints
        |> List.tryFind (fun m -> dueAt clock m > now)
        |> Option.iter (fun m -> printfn "next %s at %s (in %d minutes)" (checkpointName m) (iso (dueAt clock m)) (minutesBetween now (dueAt clock m)))
        match late, elapsed > clock.Ceiling && not (names.Contains "END") with
        | _, true -> printfn "PAST CEILING with no END record"; 3
        | [], false -> 0
        | _ -> 2

let checkpoint (a: Map<string, string>) =
    let dir = a["dir"]
    if not (File.Exists(clockPath dir)) then eprintfn "no clock at %s" (clockPath dir); 3
    else
        let clock = loadClock dir
        let now = a |> Map.tryFind "now" |> Option.map parseTime |> Option.defaultValue DateTimeOffset.UtcNow
        let name = a["name"]
        let records = loadRecords dir
        let known = name = "END" || clock.Checkpoints |> List.exists (fun m -> checkpointName m = name)
        if not known then eprintfn "unknown checkpoint %s" name; 2
        elif (recordedNames records).Contains name then eprintfn "checkpoint %s already recorded (records are never rewritten)" name; 2
        else
            let dueTime = clock.Checkpoints |> List.tryFind (fun m -> checkpointName m = name) |> Option.map (dueAt clock)
            let narrative = (readJson (a["input"])).AsObject()
            let elapsed = minutesBetween clock.Start now
            let lateness (d: DateTimeOffset) = max 0 (minutesBetween d now)
            let ledgerFields (l: string) =
                [ "p0FromLedger", (JsonObject([ kv "ledger" (jv l); kv "counts" (p0Counts (readJson l) :> JsonNode) ]) :> JsonNode) ]
            let computed =
                [ "checkpoint", jv name
                  "recordedAtUtc", jv (iso now)
                  "elapsedMinutes", ji elapsed
                  "headSha", jv (a["head"]) ]
                @ (dueTime |> Option.map (fun d -> [ "dueAtUtc", jv (iso d); "lateMinutes", ji (lateness d) ]) |> Option.defaultValue [])
                @ (a |> Map.tryFind "ledger" |> Option.map ledgerFields |> Option.defaultValue [])
            let computedKeys = computed |> List.map fst |> Set.ofList
            let copied =
                narrative
                |> Seq.filter (fun p -> not (computedKeys.Contains p.Key))
                |> Seq.map (fun p -> p.Key, (Option.ofObj p.Value |> Option.map (fun v -> v.DeepClone()) |> Option.toObj))
                |> List.ofSeq
            let record = JsonObject((computed @ copied) |> List.map (fun (k, v) -> kv k v)) :> JsonNode
            let all =
                JsonObject(
                    [ kv "schema" (jv "indy-init.rehearsal-checkpoints/1")
                      kv "clock" (jv $"clock.json (start {iso clock.Start}, ceiling {clock.Ceiling} min)")
                      kv "checkpoints" (JsonArray(Array.ofList (records @ [ record ])) :> JsonNode) ])
            writeJson (checkpointsPath dir) all
            let lateText = dueTime |> Option.map (fun d -> $", {lateness d} min late") |> Option.defaultValue ""
            printfn "recorded %s at %s (elapsed %d min%s)" name (iso now) elapsed lateText
            0

let exitCode =
    match fsi.CommandLineArgs |> Array.toList |> List.tail with
    | "start" :: rest -> start (parseArgs rest)
    | "status" :: rest -> status (parseArgs rest)
    | "checkpoint" :: rest -> checkpoint (parseArgs rest)
    | _ -> eprintfn "usage: clock.fsx start|status|checkpoint ..."; 2

exit exitCode
