/// Duration-weighted scheduling of the timed work items (compiled package v4): estimates come from each task's
/// estimateMinutes, lanes from a named topology. Pure and deterministic; used by the timed-execution manifest and the
/// v4 execution plan, so both report the same minutes.
module TimedSchedule

open Json
open Model

let estimate (t: WorkTask) = intOpt "estimateMinutes" t.Raw |> Option.map int

let private implementationLanes = set [ "domain"; "ui"; "persistence"; "integration"; "coordinator" ]

/// The lane of a task under a topology: "B-single-lead" puts every implementation role on one lead agent; any other
/// topology (A-role-lanes) gives each owner role its own lane.
let laneOf (topology: string) (t: WorkTask) =
    match topology with
    | "B-single-lead" -> if implementationLanes.Contains t.Owner then "lead" else t.Owner
    | _ -> t.Owner

type Slot = { Task: string; Lane: string; Start: int; Finish: int }


/// Duration-weighted list scheduling of the timed tasks: a task starts when its timed prerequisites have finished and
/// its lane is free; ready tasks are taken longest-remaining-duration first, then by id. Preflight prerequisites are
/// complete at T0. Pure and deterministic.
let schedule (laneOf: WorkTask -> string) (tasks: WorkTask list) : Slot list =
    let timed = tasks |> List.filter (fun t -> t.Phase = "timed")
    let byId = timed |> List.map (fun t -> t.Id, t) |> Map.ofList
    let dur id = estimate byId.[id] |> Option.defaultValue 0
    let deps (t: WorkTask) = t.DependsOn |> List.map (fun d -> d.Task) |> List.filter byId.ContainsKey
    let dependents = timed |> List.collect (fun t -> deps t |> List.map (fun d -> d, t.Id)) |> List.groupBy fst |> List.map (fun (k, xs) -> k, List.map snd xs) |> Map.ofList
    let rec tail (memo: Map<string, int>) id =
        match memo.TryFind id with
        | Some v -> memo, v
        | None ->
            let memo', best =
                dependents.TryFind id |> Option.defaultValue [] |> List.fold (fun (m, b) d -> let m2, v = tail m d in m2, max b v) (memo, 0)
            let v = dur id + best
            memo'.Add(id, v), v
    let tails = timed |> List.fold (fun m t -> fst (tail m t.Id)) Map.empty
    let rec go (done': Map<string, Slot>) (laneFree: Map<string, int>) =
        if done'.Count = timed.Length then done' |> Map.toList |> List.map snd
        else
            let ready = timed |> List.filter (fun t -> not (done'.ContainsKey t.Id) && deps t |> List.forall done'.ContainsKey)
            // Non-delay: the ready task that can start soonest goes first, longest remaining duration breaking ties.
            let startOf (t: WorkTask) =
                let earliest = deps t |> List.map (fun d -> done'.[d].Finish) |> List.fold max 0
                max earliest (laneFree.TryFind(laneOf t) |> Option.defaultValue 0)
            match ready |> List.sortBy (fun t -> startOf t, -tails.[t.Id], t.Id) with
            | [] -> done' |> Map.toList |> List.map snd
            | t :: _ ->
                let lane = laneOf t
                let start = startOf t
                let slot = { Task = t.Id; Lane = lane; Start = start; Finish = start + dur t.Id }
                go (done'.Add(t.Id, slot)) (laneFree.Add(lane, slot.Finish))
    go Map.empty Map.empty

/// The duration-weighted longest dependency chain among timed tasks (independent of lanes).
let criticalPath (tasks: WorkTask list) : string list * int =
    let timedIds = tasks |> List.filter (fun t -> t.Phase = "timed") |> List.map (fun t -> t.Id) |> Set.ofList
    // Preflight prerequisites are complete at T0, so only timed edges count.
    let timed = tasks |> List.filter (fun t -> timedIds.Contains t.Id) |> List.map (fun t -> { t with DependsOn = t.DependsOn |> List.filter (fun d -> timedIds.Contains d.Task) })
    let byId = timed |> List.map (fun t -> t.Id, t) |> Map.ofList
    match Graph.topoOrder timed with
    | Error _ -> [], 0
    | Ok order ->
        let finish =
            order
            |> List.fold
                (fun (acc: Map<string, int * string option>) id ->
                    let t = byId.[id]
                    let preds = t.DependsOn |> List.map (fun d -> d.Task) |> List.filter byId.ContainsKey
                    let bestPred = preds |> List.sortBy (fun p -> -(fst acc.[p]), p) |> List.tryHead
                    let start = bestPred |> Option.map (fun p -> fst acc.[p]) |> Option.defaultValue 0
                    acc.Add(id, (start + (estimate t |> Option.defaultValue 0), bestPred)))
                Map.empty
        match finish |> Map.toList |> List.sortBy (fun (id, (f, _)) -> -f, id) with
        | [] -> [], 0
        | (last, (len, _)) :: _ ->
            let rec back id acc =
                match snd finish.[id] with
                | Some p -> back p (id :: acc)
                | None -> id :: acc
            back last [], len

