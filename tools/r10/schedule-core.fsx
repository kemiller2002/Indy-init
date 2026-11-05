// schedule-core.fsx: the pure Rehearsal 010 schedule model behind schedule-model.fsx and schedule-model-tests.fsx.
//
// A minute-by-minute simulation over immutable state. Lanes implement slices of the roles they hold; a slice starts
// when every slice it depends on is integrated; an implemented slice enters one integration queue, is verified on
// main plus the candidates ahead of it (at most QueueDepth at once) and merges strictly in queue order. With the
// defaults (QueueDepth 1, no failures, scale 1, roles = boundaries) it is the simulation of schedule.fsx.

module ScheduleCore

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes

type Slice =
    { Id: string
      Role: string
      Deps: string list
      Minutes: float
      Source: string
      MandatoryObligations: int
      Requirements: string list }

type Config =
    { Setup: float
      Merge: float
      Window: float
      QueueDepth: int
      FailEvery: int
      RepairFraction: float
      Scale: float
      SliceOverhead: float
      /// Functional milestones, in delivery order: (id, name, requirement ids). Empty = none.
      Milestones: (string * string * string list) list
      /// "tail" (schedule.fsx: the longest chain first) or "milestones" (the earliest milestone a slice serves first).
      Priority: string
      DefaultMinutes: float
      Lanes: (string * string list) list }

let defaultLanes = "integration:integration,domain:domain,persistence:persistence,ui-1:ui,ui-2:ui,reviewer:reviewer"

let parseLanes (spec: string) : (string * string list) list =
    spec.Split(',') |> Array.map (fun l -> let p = l.Split(':') in p[0], p[1].Split('|') |> List.ofArray) |> List.ofArray

let private str (n: JsonNode) (k: string) = match n[k] with null -> "" | v -> v.ToString()

/// The slices of a slice-execution manifest, with schedule.fsx's estimate rule: the DAG estimate, else the DAG's
/// minutes per mandatory obligation applied to the slice's mandatory obligations (derived), else DefaultMinutes; then
/// scaled. A roles file (indy-init.slice-roles/1: { "roles": { slice: { "role": r, "basis": text } } }) overrides
/// a slice's role; every other slice keeps its implementationBoundary.
let loadSlices (path: string) (rolesPath: string option) (c: Config) : Slice list =
    let manifest = JsonNode.Parse(File.ReadAllText path)
    let roles =
        rolesPath
        |> Option.map (fun p ->
            let r = (JsonNode.Parse(File.ReadAllText p)).["roles"].AsObject()
            r |> Seq.map (fun kv -> kv.Key, str kv.Value "role") |> Map.ofSeq)
        |> Option.defaultValue Map.empty
    let raw =
        manifest["slices"].AsArray()
        |> Seq.map (fun s ->
            let est = match s["estimateMinutes"] with null -> None | v -> Some(v.GetValue<float>())
            let reqs = match s["requirements"] with null -> [] | v -> v.AsArray() |> Seq.map (fun r -> match r with :? JsonObject as o -> str o "id" | _ -> r.ToString()) |> List.ofSeq
            str s "id", str s "implementationBoundary", (s["dependsOn"].AsArray() |> Seq.map string |> List.ofSeq), est, s["mandatoryObligations"].GetValue<int>(), reqs)
        |> List.ofSeq
    let unknown = roles |> Map.toList |> List.map fst |> List.filter (fun id -> not (raw |> List.exists (fun (i, _, _, _, _, _) -> i = id)))
    if not unknown.IsEmpty then failwithf "the roles file names slices the manifest does not have: %A" unknown
    let carrying = raw |> List.choose (fun (_, _, _, e, o, _) -> match e with Some m when o > 0 -> Some(m, float o) | _ -> None)
    let rate = if carrying.IsEmpty then nan else (carrying |> List.sumBy fst) / (carrying |> List.sumBy snd)
    raw
    |> List.map (fun (id, boundary, deps, est, o, reqs) ->
        let minutes, source =
            match est with
            | Some m -> m, "execution-dag"
            | None when o > 0 && not (Double.IsNaN rate) -> Math.Ceiling(rate * float o), "derived"
            | None -> c.DefaultMinutes, "default"
        { Id = id
          Role = roles |> Map.tryFind id |> Option.defaultValue boundary
          Deps = deps
          // per-slice overhead (branch, pull request, context) is charged to every slice, so finer slices are not free
          Minutes = (if c.Scale = 1.0 then minutes else Math.Ceiling(minutes * c.Scale)) + c.SliceOverhead
          Source = source
          MandatoryObligations = o
          Requirements = reqs })

/// Milestones (indy-init.r10-milestones/1: { "milestones": [ { "id", "name", "requirements": [..] } ] }), in order.
let loadMilestones (path: string) : (string * string * string list) list =
    (JsonNode.Parse(File.ReadAllText path)).["milestones"].AsArray()
    |> Seq.map (fun m -> str m "id", str m "name", (m["requirements"].AsArray() |> Seq.map string |> List.ofSeq))
    |> List.ofSeq

/// The earliest milestone (1-based) each slice serves: one that carries a requirement of it, or any slice that such
/// a slice depends on (transitively). Slices no milestone needs rank after every milestone.
let milestoneRank (milestones: (string * string * string list) list) (slices: Slice list) : Map<string, int> =
    let byId = slices |> List.map (fun s -> s.Id, s) |> Map.ofList
    let rec ancestors (id: string) (acc: Set<string>) =
        if acc.Contains id then acc else byId[id].Deps |> List.fold (fun a d -> ancestors d a) (acc.Add id)
    let needed =
        milestones |> List.mapi (fun i (_, _, reqs) ->
            let carriers = slices |> List.filter (fun s -> s.Requirements |> List.exists (fun r -> List.contains r reqs)) |> List.map (fun s -> s.Id)
            i + 1, carriers |> List.fold (fun a c -> ancestors c a) Set.empty)
    slices |> List.map (fun s -> s.Id, (needed |> List.tryFind (fun (_, set) -> set.Contains s.Id) |> Option.map fst |> Option.defaultValue (milestones.Length + 1))) |> Map.ofList

/// When each milestone is delivered: every slice carrying each of its requirements is integrated. A requirement no
/// slice carries is an error (a milestone must not name work that is not in the plan).
let milestoneTimes (milestones: (string * string * string list) list) (slices: Slice list) (recs: Map<string, 'r>) (integratedOf: 'r -> float) : (string * string * float) list =
    milestones |> List.map (fun (id, name, reqs) ->
        let t =
            reqs |> List.map (fun r ->
                match slices |> List.filter (fun s -> List.contains r s.Requirements) with
                | [] -> failwithf "milestone %s names %s, which no slice carries" id r
                | carriers -> carriers |> List.map (fun s -> integratedOf recs[s.Id]) |> List.max)
            |> List.fold max 0.0
        id, name, t)

/// How many milestones are usable at minute w: a milestone is usable only once it and every earlier milestone are
/// delivered (nothing can be browsed before Connect works), so this counts the delivered prefix (review RVO-3-2).
let usableAt (times: (string * string * float) list) (w: float) : int =
    times |> List.takeWhile (fun (_, _, t) -> t <= w) |> List.length

/// The minutes per mandatory obligation the derived estimates use (for the report).
let rateOf (slices: Slice list) =
    let carrying = slices |> List.filter (fun s -> s.Source = "execution-dag" && s.MandatoryObligations > 0)
    if carrying.IsEmpty then nan else (carrying |> List.sumBy (fun s -> s.Minutes)) / float (carrying |> List.sumBy (fun s -> s.MandatoryObligations))

// ---- critical path (no lane contention: the dependency structure alone) --------------------------------------------

type CpmRow = { Slice: string; Es: float; Ef: float; Ls: float; Slack: float; Critical: bool }

/// Slice ids in dependency order; fails on a cycle or an unknown dependency.
let topo (slices: Slice list) : string list =
    let ids = slices |> List.map (fun s -> s.Id) |> Set.ofList
    slices |> List.iter (fun s -> s.Deps |> List.iter (fun d -> if not (ids.Contains d) then failwithf "%s depends on unknown %s" s.Id d))
    let rec go (doneSet: Set<string>) (acc: string list) (rest: Slice list) =
        match rest with
        | [] -> List.rev acc
        | _ ->
            match rest |> List.partition (fun s -> s.Deps |> List.forall doneSet.Contains) with
            | [], _ -> failwithf "dependency cycle among %A" (rest |> List.map (fun s -> s.Id))
            | ready, blocked -> go (ready |> List.fold (fun d s -> d.Add s.Id) doneSet) (List.rev (ready |> List.map (fun s -> s.Id)) @ acc) blocked
    go Set.empty [] slices

/// Earliest/latest start with unlimited lanes; each slice costs its implementation plus one verification and merge.
let cpm (setup: float) (ci: float) (merge: float) (slices: Slice list) : CpmRow list * float =
    let byId = slices |> List.map (fun s -> s.Id, s) |> Map.ofList
    let dur (s: Slice) = s.Minutes + ci + merge
    let order = topo slices
    let ef = order |> List.fold (fun (m: Map<string, float>) id ->
                let s = byId[id]
                let es = s.Deps |> List.map (fun d -> m[d]) |> List.fold max setup
                m.Add(id, es + dur s)) Map.empty
    let finish = ef |> Map.toList |> List.map snd |> List.fold max setup
    let dependents = slices |> List.collect (fun s -> s.Deps |> List.map (fun d -> d, s.Id)) |> List.groupBy fst |> List.map (fun (k, v) -> k, List.map snd v) |> Map.ofList
    let lf = order |> List.rev |> List.fold (fun (m: Map<string, float>) id ->
                let succ = dependents |> Map.tryFind id |> Option.defaultValue []
                let l = succ |> List.map (fun d -> m[d] - dur byId[d]) |> List.fold min finish
                m.Add(id, l)) Map.empty
    let rows =
        order |> List.map (fun id ->
            let s = byId[id]
            let e = ef[id] - dur s
            let ls = lf[id] - dur s
            { Slice = id; Es = e; Ef = ef[id]; Ls = ls; Slack = ls - e; Critical = abs (ls - e) < 1e-9 })
    rows, finish

// ---- the simulation ------------------------------------------------------------------------------------------------

type Phase =
    | Waiting
    | Implementing of lane: string * start: float * until: float
    | Repairing of lane: string * until: float
    | Queued of implemented: float
    | Verifying of since: float * until: float * fails: bool
    | Verified of at: float
    | Merging of until: float
    | Integrated of at: float

type Record =
    { Lane: string; Start: float; Implemented: float; FirstVerification: float; Integrated: float
      Runs: int; Repairs: int; RepairMinutes: float; QueueWait: float; Defective: bool }

type State =
    { T: float
      Phases: Map<string, Phase>
      LaneFree: Map<string, float>
      Repairs: (string * string) list   // (lane, slice) awaiting repair, in order
      Queue: string list                 // merge order of candidates in the integration queue
      Runs: int                          // slices whose first verification has started (for FailEvery)
      Rec: Map<string, Record> }

type Event = { Slice: string; Lane: string; Start: float; Implemented: float; Integrated: float; Runs: int; Repairs: int }

/// The longest chain of implementation + one verification from a slice to the end: schedule.fsx's priority.
let priorities (ci: float) (slices: Slice list) =
    let byId = slices |> List.map (fun s -> s.Id, s) |> Map.ofList
    let dependents = slices |> List.collect (fun s -> s.Deps |> List.map (fun d -> d, s.Id)) |> List.groupBy fst |> List.map (fun (k, v) -> k, List.map snd v) |> Map.ofList
    let rec go (id: string) : float =
        byId[id].Minutes + ci + (dependents.TryFind id |> Option.defaultValue [] |> List.map go |> List.fold max 0.0)
    slices |> List.map (fun s -> s.Id, go s.Id) |> Map.ofList

let private integratedAt (st: State) (id: string) = match st.Phases[id] with Integrated at -> Some at | _ -> None

/// One fixed-point pass at minute st.T; returns the state and whether anything changed.
let private pass (c: Config) (ci: float) (slices: Slice list) (prio: Map<string, float>) (rank: Map<string, int>) (st: State) : State * bool =
    let t = st.T
    let byId = slices |> List.map (fun s -> s.Id, s) |> Map.ofList
    let set id p (s: State) = { s with Phases = s.Phases.Add(id, p) }
    let upd id f (s: State) = { s with Rec = s.Rec.Add(id, f s.Rec[id]) }
    // a. merges completing now
    let st1 =
        st.Phases |> Map.toList |> List.filter (fun (_, p) -> match p with Merging u -> u <= t | _ -> false) |> List.map fst
        |> List.fold (fun s id -> s |> set id (Integrated t) |> upd id (fun r -> { r with Integrated = t }) |> fun s -> { s with Queue = s.Queue |> List.filter ((<>) id) }) st
    // b. implementations and repairs completing now enter the queue (schedule.fsx: the latest started first on a tie)
    let finished =
        st1.Phases |> Map.toList
        |> List.choose (fun (id, p) -> match p with Implementing(l, s0, u) when u <= t -> Some(id, l, s0) | Repairing(l, u) when u <= t -> Some(id, l, u) | _ -> None)
        |> List.sortBy (fun (_, l, s0) -> -s0, -(c.Lanes |> List.findIndex (fst >> (=) l)))
    let st2 =
        finished |> List.fold (fun s (id, l, _) ->
            { (set id (Queued t) s) with Queue = s.Queue @ [ id ]; LaneFree = s.LaneFree.Add(l, t) }
            |> upd id (fun r -> if r.Implemented < 0.0 then { r with Implemented = t } else r)) st1
    // c. verifications completing now: pass, or fail (every FailEvery-th run) and go back to the lane for repair;
    //    a failure restarts every speculative verification stacked behind it
    let done' = st2.Queue |> List.filter (fun id -> match st2.Phases[id] with Verifying(_, u, _) -> u <= t | _ -> false)
    let st3 =
        done' |> List.fold (fun (s: State) id ->
            match s.Phases[id] with
            | Verifying(_, _, true) ->
                let behind = s.Queue |> List.skipWhile ((<>) id) |> List.tail
                let restarted =
                    behind |> List.fold (fun (s': State) b ->
                        match s'.Phases[b] with
                        | Verifying _ | Verified _ -> set b (Queued t) s'
                        | _ -> s') s
                let lane = restarted.Rec[id].Lane
                { restarted with Queue = restarted.Queue |> List.filter ((<>) id); Repairs = restarted.Repairs @ [ lane, id ] }
                |> set id Waiting
                |> upd id (fun r -> { r with Repairs = r.Repairs + 1 })
            | Verifying _ -> set id (Verified t) s
            | _ -> s) st2
    // d. the queue head merges when verified and no merge is running
    let merging = st3.Phases |> Map.exists (fun _ p -> match p with Merging _ -> true | _ -> false)
    let st4 =
        match st3.Queue with
        | head :: _ when not merging && (match st3.Phases[head] with Verified _ -> true | _ -> false) ->
            if c.Merge <= 0.0 then st3 |> set head (Integrated t) |> upd head (fun r -> { r with Integrated = t }) |> fun s -> { s with Queue = List.tail s.Queue }
            else set head (Merging(t + c.Merge)) st3
        | _ -> st3
    // e. free lanes start work: a pending repair of their own first, else the highest-priority ready slice they own
    let st5 =
        c.Lanes |> List.fold (fun (s: State) (lane, roles) ->
            if s.LaneFree[lane] > t then s
            else
                match s.Repairs |> List.tryFind (fun (l, _) -> l = lane) with
                | Some(_, id) ->
                    let m = Math.Ceiling(byId[id].Minutes * c.RepairFraction)
                    { s with Repairs = s.Repairs |> List.filter (fun (l, x) -> not (l = lane && x = id)); LaneFree = s.LaneFree.Add(lane, t + m) }
                    |> set id (Repairing(lane, t + m))
                    |> upd id (fun r -> { r with RepairMinutes = r.RepairMinutes + m })
                | None ->
                    let ready =
                        slices
                        |> List.filter (fun x -> s.Phases[x.Id] = Waiting && not (s.Rec[x.Id].Start >= 0.0) && List.contains x.Role roles
                                                 && x.Deps |> List.forall (fun d -> integratedAt s d |> Option.exists (fun a -> a <= t)))
                        |> List.sortBy (fun x -> rank[x.Id], -prio[x.Id])
                    match ready with
                    | x :: _ ->
                        { s with LaneFree = s.LaneFree.Add(lane, t + x.Minutes) }
                        |> set x.Id (Implementing(lane, t, t + x.Minutes))
                        |> upd x.Id (fun r -> { r with Lane = lane; Start = t })
                    | [] -> s) st4
    // f. verification starts: queued candidates, in merge order, while a slot is free
    let busy (s: State) = s.Queue |> List.filter (fun id -> match s.Phases[id] with Verifying _ | Verified _ | Merging _ -> true | _ -> false) |> List.length
    let st6 =
        st5.Queue |> List.fold (fun (s: State) id ->
            match s.Phases[id] with
            | Queued at when busy s < c.QueueDepth ->
                // every FailEvery-th slice (in the order slices are first verified) fails its first verification once
                // a defect belongs to the slice, not to a run: decided at its first verification, it fails every
                // verification of the slice until the slice has been repaired (a restart does not hide it)
                let first = s.Rec[id].Runs = 0
                let ordinal = if first then s.Runs + 1 else s.Runs
                let defective = if first then c.FailEvery > 0 && ordinal % c.FailEvery = 0 else s.Rec[id].Defective
                let fails = defective && s.Rec[id].Repairs = 0
                { s with Runs = ordinal }
                |> set id (Verifying(t, t + ci, fails))
                |> upd id (fun r -> { r with Defective = defective; Runs = r.Runs + 1; FirstVerification = (if r.FirstVerification < 0.0 then t else r.FirstVerification); QueueWait = r.QueueWait + (t - at) })
            | _ -> s) st5
    st6, st6 <> st

/// Runs the simulation to completion; returns the per-slice records.
let simulate (c: Config) (ci: float) (slices: Slice list) : Map<string, Record> =
    topo slices |> ignore   // refuses a cycle or an unknown dependency before anything recurses over the graph
    let prio = priorities ci slices
    let rank = if c.Priority = "milestones" then milestoneRank c.Milestones slices else slices |> List.map (fun s -> s.Id, 0) |> Map.ofList
    let empty = { Lane = ""; Start = -1.0; Implemented = -1.0; FirstVerification = -1.0; Integrated = -1.0; Runs = 0; Repairs = 0; RepairMinutes = 0.0; QueueWait = 0.0; Defective = false }
    let rec settle st = match pass c ci slices prio rank st with s, true -> settle s | s, false -> s
    let rec tick (st: State) =
        let st = settle st
        if st.Phases |> Map.forall (fun _ p -> match p with Integrated _ -> true | _ -> false) then st.Rec
        elif st.T > 100000.0 then failwithf "deadlock at T+%.0f: check dependsOn and that every role has a lane" st.T
        else tick { st with T = st.T + 1.0 }
    tick { T = 0.0
           Phases = slices |> List.map (fun s -> s.Id, Waiting) |> Map.ofList
           LaneFree = c.Lanes |> List.map (fun (l, _) -> l, c.Setup) |> Map.ofList
           Repairs = []; Queue = []; Runs = 0
           Rec = slices |> List.map (fun s -> s.Id, empty) |> Map.ofList }

// ---- reporting -----------------------------------------------------------------------------------------------------

type Scenario =
    { Ci: float
      Finish: float
      SlicesByDeadline: int
      RequirementsByDeadline: int
      ObligationsByDeadline: int
      CriticalPath: float
      CriticalSlices: string list
      Cpm: CpmRow list
      ImplementationMinutes: float
      RepairMinutes: float
      VerificationMinutes: float
      MergeMinutes: float
      QueueWaitMinutes: float
      LaneIdleMinutes: float
      VerificationRuns: int
      MilestoneTimes: (string * string * float) list
      Records: Map<string, Record> }

type Report = { Config: Config; Slices: Slice list; Rate: float; Scenarios: Scenario list }

let scenario (c: Config) (slices: Slice list) (ci: float) : Scenario =
    let recs = simulate c ci slices
    let finish = recs |> Map.toList |> List.map (fun (_, r) -> r.Integrated) |> List.max
    let inWindow = slices |> List.filter (fun s -> recs[s.Id].Integrated <= c.Window)
    let rows, cp = cpm c.Setup ci c.Merge slices
    let impl = slices |> List.sumBy (fun s -> s.Minutes)
    let repair = recs |> Map.toList |> List.sumBy (fun (_, r) -> r.RepairMinutes)
    // idle lane-minutes inside the window: lanes that hold work, minus what they spent implementing or repairing
    let worked lane = slices |> List.filter (fun s -> recs[s.Id].Lane = lane) |> List.sumBy (fun s -> min s.Minutes (max 0.0 (c.Window - recs[s.Id].Start)))
    let idle = c.Lanes |> List.sumBy (fun (l, _) -> max 0.0 (c.Window - c.Setup - worked l))
    { Ci = ci
      Finish = finish
      SlicesByDeadline = inWindow.Length
      RequirementsByDeadline = inWindow |> List.collect (fun s -> s.Requirements) |> List.distinct |> List.length
      ObligationsByDeadline = inWindow |> List.sumBy (fun s -> s.MandatoryObligations)
      CriticalPath = cp
      CriticalSlices = rows |> List.filter (fun r -> r.Critical) |> List.sortBy (fun r -> r.Es) |> List.map (fun r -> r.Slice)
      Cpm = rows
      ImplementationMinutes = impl
      RepairMinutes = repair
      VerificationMinutes = recs |> Map.toList |> List.sumBy (fun (_, r) -> float r.Runs * ci)
      MergeMinutes = float slices.Length * c.Merge
      QueueWaitMinutes = recs |> Map.toList |> List.sumBy (fun (_, r) -> r.QueueWait)
      LaneIdleMinutes = idle
      VerificationRuns = recs |> Map.toList |> List.sumBy (fun (_, r) -> r.Runs)
      MilestoneTimes = milestoneTimes c.Milestones slices recs (fun r -> r.Integrated)
      Records = recs }

let run (c: Config) (slices: Slice list) (cis: float list) : Report =
    { Config = c; Slices = slices; Rate = rateOf slices; Scenarios = cis |> List.map (scenario c slices) }

let printReport (r: Report) =
    let c = r.Config
    printfn "slices %d (%d requirements, %d mandatory obligations); implementation %.0f lane-minutes; lanes %s; queue depth %d; fail every %s; scale %.2f"
        r.Slices.Length (r.Slices |> List.collect (fun s -> s.Requirements) |> List.distinct |> List.length) (r.Slices |> List.sumBy (fun s -> s.MandatoryObligations))
        (r.Slices |> List.sumBy (fun s -> s.Minutes)) (c.Lanes |> List.map (fun (l, rs) -> l + ":" + String.Join("|", rs)) |> String.concat ",")
        c.QueueDepth (if c.FailEvery = 0 then "never" else string c.FailEvery) c.Scale
    for s in r.Scenarios do
        printfn "ci %3.0f min: all integrated at T+%.0f (%s %.0f); by the deadline %d slices, %d requirements, %d mandatory obligations; critical path %.0f; queue wait %.0f; verification %.0f (%d runs); repair %.0f"
            s.Ci s.Finish (if s.Finish <= c.Window then "within" else "OVER") c.Window s.SlicesByDeadline s.RequirementsByDeadline s.ObligationsByDeadline
            s.CriticalPath s.QueueWaitMinutes s.VerificationMinutes s.VerificationRuns s.RepairMinutes
        if not s.MilestoneTimes.IsEmpty then
            printfn "         milestones: %s; usable at T+120/240/360/420: %s"
                (s.MilestoneTimes |> List.map (fun (id, _, t) -> sprintf "%s T+%.0f" id t) |> String.concat ", ")
                ([ 120.0; 240.0; 360.0; 420.0 ] |> List.map (fun w -> usableAt s.MilestoneTimes w |> string) |> String.concat "/")

let private jv (x: obj) : JsonNode =
    match x with
    | :? string as s -> JsonValue.Create s :> JsonNode
    | :? float as f -> JsonValue.Create f :> JsonNode
    | :? int as i -> JsonValue.Create i :> JsonNode
    | :? bool as b -> JsonValue.Create b :> JsonNode
    | :? JsonNode as n -> n
    | _ -> failwith "unsupported"
let private obj (kvs: (string * obj) list) = JsonObject(kvs |> List.map (fun (k, v) -> Collections.Generic.KeyValuePair(k, jv v))) :> JsonNode
let private arrOf (xs: JsonNode list) = JsonArray(Array.ofList xs) :> JsonNode

let toJson (r: Report) : JsonNode =
    let c = r.Config
    obj [ "schema", box "indy-init.schedule-model/1"
          "note", box "model results from estimates, not observations"
          "lanes", box (c.Lanes |> List.map (fun (l, rs) -> l + ":" + String.Join("|", rs)) |> String.concat ",")
          "setupMinutes", box c.Setup; "mergeMinutes", box c.Merge; "windowMinutes", box c.Window
          "priority", box c.Priority; "queueDepth", box c.QueueDepth; "failEvery", box c.FailEvery; "repairFraction", box c.RepairFraction; "scale", box c.Scale
          "minutesPerObligation", box r.Rate
          "slices", box (arrOf (r.Slices |> List.map (fun s -> obj [ "slice", box s.Id; "role", box s.Role; "minutes", box s.Minutes; "source", box s.Source; "requirements", box s.Requirements.Length; "mandatoryObligations", box s.MandatoryObligations ])))
          "scenarios", box (arrOf (r.Scenarios |> List.map (fun s ->
              obj [ "ciMinutes", box s.Ci; "allIntegratedAt", box s.Finish; "fits", box (s.Finish <= c.Window)
                    "slicesByDeadline", box s.SlicesByDeadline; "requirementsByDeadline", box s.RequirementsByDeadline; "mandatoryObligationsByDeadline", box s.ObligationsByDeadline
                    "criticalPathMinutes", box s.CriticalPath; "criticalSlices", box (arrOf (s.CriticalSlices |> List.map (fun x -> jv x)))
                    "implementationMinutes", box s.ImplementationMinutes; "repairMinutes", box s.RepairMinutes; "verificationMinutes", box s.VerificationMinutes
                    "verificationRuns", box s.VerificationRuns; "mergeMinutes", box s.MergeMinutes; "queueWaitMinutes", box s.QueueWaitMinutes; "laneIdleMinutesInWindow", box s.LaneIdleMinutes
                    "milestones", box (arrOf (s.MilestoneTimes |> List.map (fun (id, name, t) -> obj [ "id", box id; "name", box name; "deliveredAt", box t; "byDeadline", box (t <= c.Window) ])))
                    "usableMilestonesAt", box (obj ([ 120.0; 240.0; 360.0; 420.0 ] |> List.map (fun w -> sprintf "T+%.0f" w, box (usableAt s.MilestoneTimes w))))
                    "cpm", box (arrOf (s.Cpm |> List.map (fun x -> obj [ "slice", box x.Slice; "earliestStart", box x.Es; "earliestFinish", box x.Ef; "latestStart", box x.Ls; "slack", box x.Slack; "critical", box x.Critical ])))
                    "timeline", box (arrOf (r.Slices |> List.map (fun sl -> let x = s.Records[sl.Id] in obj [ "slice", box sl.Id; "lane", box x.Lane; "start", box x.Start; "implemented", box x.Implemented; "firstVerification", box x.FirstVerification; "integrated", box x.Integrated; "runs", box x.Runs; "repairs", box x.Repairs ]))) ]))) ]

let writeReport (path: string) (r: Report) =
    File.WriteAllText(path, (toJson r).ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
