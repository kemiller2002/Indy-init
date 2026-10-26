/// Phases 5 and 7: the work dependency graph and the two derived execution schedules.
/// Only `dependsOn` edges are dependencies; `preferenceAfter` edges are recorded but never scheduled.
module Graph

open Model

type Cycle = string list

/// Kahn's algorithm with ordinal tie-breaking. Returns the order, or the tasks left on a cycle.
let topoOrder (tasks: WorkTask list) : Result<string list, Cycle> =
    let deps = tasks |> List.map (fun t -> t.Id, t.DependsOn |> List.map (fun d -> d.Task) |> Set.ofList) |> Map.ofList

    let rec go (remaining: Map<string, Set<string>>) (done': string list) =
        if remaining.IsEmpty then
            Ok(List.rev done')
        else
            let doneSet = Set.ofList done'
            let ready = remaining |> Map.filter (fun _ ds -> Set.isSubset ds doneSet) |> Map.keys |> Seq.sort |> List.ofSeq

            match ready with
            | [] -> Error(remaining |> Map.keys |> Seq.sort |> List.ofSeq)
            | r :: _ -> go (remaining.Remove r) (r :: done')

    go deps []

/// Length (in tasks) of the longest dependency chain ending at each task.
let depthFrom (order: string list) (tasks: WorkTask list) =
    let byId = tasks |> List.map (fun t -> t.Id, t) |> Map.ofList

    order
    |> List.fold
        (fun (acc: Map<string, int>) id ->
            let d = byId.[id].DependsOn |> List.map (fun x -> acc.[x.Task]) |> List.fold max 0
            acc.Add(id, d + 1))
        Map.empty

/// Length of the longest chain starting at each task (used as scheduling priority).
let remainingFrom (order: string list) (tasks: WorkTask list) =
    let dependents =
        tasks
        |> List.collect (fun t -> t.DependsOn |> List.map (fun d -> d.Task, t.Id))
        |> List.groupBy fst
        |> List.map (fun (k, xs) -> k, xs |> List.map snd)
        |> Map.ofList

    order
    |> List.rev
    |> List.fold
        (fun (acc: Map<string, int>) id ->
            let r =
                dependents.TryFind id |> Option.defaultValue [] |> List.map (fun x -> acc.[x]) |> List.fold max 0

            acc.Add(id, r + 1))
        Map.empty

/// One longest chain, chosen deterministically (deepest end, then ordinal ids).
let criticalPath (order: string list) (tasks: WorkTask list) =
    let byId = tasks |> List.map (fun t -> t.Id, t) |> Map.ofList
    let depth = depthFrom order tasks

    let rec back id acc =
        let preds = byId.[id].DependsOn |> List.map (fun d -> d.Task)

        match preds |> List.sortBy (fun p -> -depth.[p], p) with
        | [] -> id :: acc
        | p :: _ -> back p (id :: acc)

    match order |> List.sortBy (fun id -> -depth.[id], id) with
    | [] -> []
    | last :: _ -> back last []

type Slot = { Task: string; Lane: string; Step: int }

/// Unit-step list scheduling. A task starts at the first step after all its dependencies finished
/// and its lane is free. Lanes in `unbounded` (operator, upstream) never constrain concurrency.
let schedule (laneOf: WorkTask -> string) (unbounded: Set<string>) (order: string list) (tasks: WorkTask list) =
    let priority = remainingFrom order tasks

    let rec step (n: int) (finished: Map<string, int>) (slots: Slot list) =
        if finished.Count = tasks.Length then
            List.rev slots
        else
            let ready =
                tasks
                |> List.filter (fun t -> not (finished.ContainsKey t.Id))
                |> List.filter (fun t -> t.DependsOn |> List.forall (fun d -> finished.ContainsKey d.Task && finished.[d.Task] < n))
                |> List.sortBy (fun t -> -priority.[t.Id], t.Id)

            let chosen =
                ready
                |> List.fold
                    (fun (busy: Set<string>, picked: WorkTask list) t ->
                        let lane = laneOf t
                        if unbounded.Contains lane || not (busy.Contains lane) then (busy.Add lane, t :: picked)
                        else (busy, picked))
                    (Set.empty, [])
                |> snd
                |> List.rev

            let finished' = chosen |> List.fold (fun (m: Map<string, int>) t -> m.Add(t.Id, n)) finished
            let slots' = (chosen |> List.map (fun t -> { Task = t.Id; Lane = laneOf t; Step = n }) |> List.rev) @ slots
            step (n + 1) finished' slots'

    step 1 Map.empty []
