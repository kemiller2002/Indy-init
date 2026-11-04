// schedule.fsx: does a slice plan fit the rehearsal window under a lane topology?
//
// A discrete-event simulation (no division of serial minutes by lane count):
//   - each development lane owns an implementation boundary (file ownership); a slice runs only on a lane that owns
//     its boundary, one slice per lane at a time;
//   - a slice starts only when every slice it depends on is INTEGRATED (merged on main);
//   - after implementation a slice enters the single integration queue (one integration authority, no concurrent
//     merges). The slice gate requires the merged result to be built on the current target, so each integration is
//     a governed gate run on current main (ciMinutes) plus the merge (mergeMinutes), strictly one at a time;
//   - the independent reviewer lane runs the reviewer-boundary slices; it never implements another lane's slice;
//   - a slice without a DAG estimate gets a derived estimate: the DAG's minutes per mandatory obligation, applied to
//     its mandatory obligations (labelled derived), or --default-minutes when it carries none.
//
//   dotnet fsi schedule.fsx -- --slices slice-execution-manifest.json --window 420 --setup 60 \
//     --ci 10,15,20,25 --merge 3 --out schedule.json
//
// Lanes (Topology A): integration, domain, persistence, ui-1, ui-2 (both own ui), and reviewer.

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let opt (name: string) = args |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let req name = opt name |> Option.defaultWith (fun () -> failwithf "missing %s" name)

type Slice = { Id: string; Boundary: string; Deps: string list; Minutes: float; Source: string; Obligations: int }

let str (n: JsonNode) (k: string) = match n[k] with null -> "" | v -> v.ToString()
let manifest = JsonNode.Parse(File.ReadAllText(req "--slices"))
let raw =
    manifest["slices"].AsArray()
    |> Seq.map (fun s ->
        let est = match s["estimateMinutes"] with null -> None | v -> Some(v.GetValue<float>())
        str s "id", str s "implementationBoundary", (s["dependsOn"].AsArray() |> Seq.map string |> List.ofSeq), est, s["mandatoryObligations"].GetValue<int>())
    |> List.ofSeq

/// Minutes per mandatory obligation over the DAG-estimated slices that carry obligations.
let rate =
    let carrying = raw |> List.choose (fun (_, _, _, e, o) -> match e with Some m when o > 0 -> Some(m, float o) | _ -> None)
    // No DAG-estimated slice that carries obligations: no rate can be derived (every unestimated slice gets the default).
    if carrying.IsEmpty then nan else (carrying |> List.sumBy fst) / (carrying |> List.sumBy snd)
let defaultMinutes = opt "--default-minutes" |> Option.map float |> Option.defaultValue 20.0
let slices =
    raw |> List.map (fun (id, b, d, e, o) ->
        match e with
        | Some m -> { Id = id; Boundary = b; Deps = d; Minutes = m; Source = "execution-dag"; Obligations = o }
        | None when o > 0 && not (Double.IsNaN rate) -> { Id = id; Boundary = b; Deps = d; Minutes = Math.Ceiling(rate * float o); Source = "derived"; Obligations = o }
        | None -> { Id = id; Boundary = b; Deps = d; Minutes = defaultMinutes; Source = "default"; Obligations = o })
let byId = slices |> List.map (fun s -> s.Id, s) |> Map.ofList

/// --lanes name:boundary|boundary,... (default Topology A: one lane per boundary, two ui lanes, one reviewer).
let lanes =
    (opt "--lanes" |> Option.defaultValue "integration:integration,domain:domain,persistence:persistence,ui-1:ui,ui-2:ui,reviewer:reviewer").Split(',')
    |> Array.map (fun l -> let p = l.Split(':') in p[0], p[1])
    |> List.ofArray
let owns (laneBoundaries: string) (b: string) = laneBoundaries.Split('|') |> Array.contains b

/// The longest chain of work (implementation + one integration each) from a slice to the end: the priority.
let tail (ci: float) =
    let dependents = slices |> List.collect (fun s -> s.Deps |> List.map (fun d -> d, s.Id)) |> List.groupBy fst |> List.map (fun (k, v) -> k, v |> List.map snd) |> Map.ofList
    let rec go (id: string) : float =
        let s = byId[id]
        s.Minutes + ci + (dependents.TryFind id |> Option.defaultValue [] |> List.map go |> List.fold max 0.0)
    slices |> List.map (fun s -> s.Id, go s.Id) |> Map.ofList

type Event = { Slice: string; Lane: string; Start: float; Implemented: float; QueueStart: float; Integrated: float }

/// One run of the simulation; returns the events in integration order.
let simulate (setup: float) (ci: float) (merge: float) : Event list =
    let priority = tail ci
    // state: lane free time, integrated map (slice -> time), implemented-waiting list, queue free time
    let rec step (laneFree: Map<string, float>) (integrated: Map<string, float>) (started: Set<string>) (pending: (string * string * float * float) list) (queueFree: float) (events: Event list) =
        if integrated.Count = slices.Length then List.rev events
        else
            // the earliest moment a lane can start a ready slice
            let ready (t: float) =
                slices
                |> List.filter (fun s -> not (started.Contains s.Id) && s.Deps |> List.forall (fun d -> integrated |> Map.tryFind d |> Option.exists (fun i -> i <= t)))
                |> List.sortByDescending (fun s -> priority[s.Id])
            let starts =
                lanes |> List.choose (fun (lane, boundary) ->
                    let t = laneFree[lane]
                    // a lane waits for the next integration if nothing is ready now
                    let times = t :: (integrated |> Map.toList |> List.map snd |> List.filter (fun x -> x > t)) |> List.distinct |> List.sort
                    times |> List.tryPick (fun at -> ready at |> List.tryFind (fun s -> owns boundary s.Boundary) |> Option.map (fun s -> at, lane, s)))
            let nextStart = starts |> List.sortBy (fun (at, _, _) -> at) |> List.tryHead
            let nextIntegration = pending |> List.sortBy (fun (_, _, _, impl) -> impl) |> List.tryHead |> Option.map (fun (id, lane, st, impl) -> max impl queueFree, (id, lane, st, impl))
            match nextStart, nextIntegration with
            | Some(at, lane, s), Some(qt, _) when at < qt || (at = qt) ->
                step (laneFree.Add(lane, at + s.Minutes)) integrated (started.Add s.Id) ((s.Id, lane, at, at + s.Minutes) :: pending) queueFree events
            | Some(at, lane, s), None ->
                step (laneFree.Add(lane, at + s.Minutes)) integrated (started.Add s.Id) ((s.Id, lane, at, at + s.Minutes) :: pending) queueFree events
            | _, Some(qt, (id, lane, st, impl)) ->
                let done' = qt + ci + merge
                step laneFree (integrated.Add(id, done')) started (pending |> List.filter (fun (p, _, _, _) -> p <> id)) done'
                    ({ Slice = id; Lane = lane; Start = st; Implemented = impl; QueueStart = qt; Integrated = done' } :: events)
            | None, None -> failwithf "deadlock: %d of %d integrated; check dependsOn" integrated.Count slices.Length
    step (lanes |> List.map (fun (l, _) -> l, setup) |> Map.ofList) Map.empty Set.empty [] setup []

let window = opt "--window" |> Option.map float |> Option.defaultValue 420.0
let setup = opt "--setup" |> Option.map float |> Option.defaultValue 60.0
let merge = opt "--merge" |> Option.map float |> Option.defaultValue 3.0
let cis = (opt "--ci" |> Option.defaultValue "10,15,20,25").Split(',') |> Array.map float |> List.ofArray

let serialMinutes = slices |> List.sumBy (fun s -> s.Minutes)
let criticalPath ci = (tail ci) |> Map.toList |> List.filter (fun (id, _) -> byId[id].Deps.IsEmpty) |> List.map snd |> List.fold max 0.0
printfn "slices %d; implementation minutes %.0f serial (%d DAG-estimated, %d derived at %.2f min/obligation, %d default)"
    slices.Length serialMinutes (slices |> List.filter (fun s -> s.Source = "execution-dag") |> List.length)
    (slices |> List.filter (fun s -> s.Source = "derived") |> List.length) rate (slices |> List.filter (fun s -> s.Source = "default") |> List.length)
let results =
    cis |> List.map (fun ci ->
        let ev = simulate setup ci merge
        let finish = ev |> List.map (fun e -> e.Integrated) |> List.max
        let inWindow = ev |> List.filter (fun e -> e.Integrated <= window)
        let queueBusy = float slices.Length * (ci + merge)
        printfn "ci %4.0f min: all %d slices integrated at T+%.0f (%s the %.0f-minute window); %d integrated by the deadline; serialized integration alone needs %.0f min; critical path %.0f min"
            ci slices.Length finish (if finish <= window then "within" else "OVER") window inWindow.Length queueBusy (setup + criticalPath ci)
        ci, finish, inWindow.Length, ev)
let maxCi =
    // the largest CI latency (whole minutes) at which every slice integrates within the window
    [ 1.0 .. 60.0 ] |> List.filter (fun ci -> (simulate setup ci merge |> List.map (fun e -> e.Integrated) |> List.max) <= window) |> List.tryLast
printfn "largest CI latency that fits the window: %s" (match maxCi with Some c -> sprintf "%.0f min" c | None -> "none")
opt "--out" |> Option.iter (fun out ->
    let j =
        JsonObject(
            [ Collections.Generic.KeyValuePair("schema", JsonValue.Create "indy-init.topology-schedule/1" :> JsonNode)
              Collections.Generic.KeyValuePair("lanes", JsonValue.Create (String.Join(",", lanes |> List.map (fun (n, b) -> n + ":" + b))) :> JsonNode)
              Collections.Generic.KeyValuePair("windowMinutes", JsonValue.Create window :> JsonNode)
              Collections.Generic.KeyValuePair("setupMinutes", JsonValue.Create setup :> JsonNode)
              Collections.Generic.KeyValuePair("mergeMinutes", JsonValue.Create merge :> JsonNode)
              Collections.Generic.KeyValuePair("minutesPerObligation", JsonValue.Create rate :> JsonNode)
              Collections.Generic.KeyValuePair("largestCiMinutesThatFits", (match maxCi with Some c -> JsonValue.Create c :> JsonNode | None -> null))
              Collections.Generic.KeyValuePair("estimates", JsonArray(slices |> List.map (fun s -> JsonObject([ Collections.Generic.KeyValuePair("slice", JsonValue.Create s.Id :> JsonNode); Collections.Generic.KeyValuePair("minutes", JsonValue.Create s.Minutes :> JsonNode); Collections.Generic.KeyValuePair("source", JsonValue.Create s.Source :> JsonNode) ]) :> JsonNode) |> Array.ofList) :> JsonNode)
              Collections.Generic.KeyValuePair("scenarios", JsonArray(results |> List.map (fun (ci, finish, n, ev) ->
                    JsonObject([ Collections.Generic.KeyValuePair("ciMinutes", JsonValue.Create ci :> JsonNode)
                                 Collections.Generic.KeyValuePair("allIntegratedAt", JsonValue.Create finish :> JsonNode)
                                 Collections.Generic.KeyValuePair("integratedByDeadline", JsonValue.Create n :> JsonNode)
                                 Collections.Generic.KeyValuePair("fits", JsonValue.Create (finish <= window) :> JsonNode)
                                 Collections.Generic.KeyValuePair("timeline", JsonArray(ev |> List.map (fun e -> JsonObject([ Collections.Generic.KeyValuePair("slice", JsonValue.Create e.Slice :> JsonNode); Collections.Generic.KeyValuePair("lane", JsonValue.Create e.Lane :> JsonNode); Collections.Generic.KeyValuePair("start", JsonValue.Create e.Start :> JsonNode); Collections.Generic.KeyValuePair("implemented", JsonValue.Create e.Implemented :> JsonNode); Collections.Generic.KeyValuePair("integrated", JsonValue.Create e.Integrated :> JsonNode) ]) :> JsonNode) |> Array.ofList) :> JsonNode) ]) :> JsonNode) |> Array.ofList) :> JsonNode) ])
    File.WriteAllText(out, j.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n"))
