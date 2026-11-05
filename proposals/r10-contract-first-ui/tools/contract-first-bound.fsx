// A deliberately small list-scheduling MODEL of when UI slices can be integrated, under the current start rule and under
// contract-first. It is not tools/r10/schedule-model.fsx (owned by the optimization agent, not edited) and it measures
// nothing: every figure is arithmetic on estimates. Its purpose is to state exactly what contract-first changes, so the
// optimization agent can reproduce the comparison with its own model (SCHEDULE-INPUTS.md section 6).
//
//   dotnet fsi tools/contract-first-bound.fsx -- <slice-execution-manifest.json> <roles.json> <hidden-merge-dependencies.json> <policy> <ci> [setup] [merge]
//   policy: current | contract-first
//
// Rules (both policies):
//   - estimates: estimateMinutes, else ceil(rate * mandatoryObligations) with schedule.fsx's rate (DAG minutes per DAG
//     mandatory obligation), else 20;
//   - one lane per role (roles.json maps slice -> role; unmapped slices use implementationBoundary);
//   - one serial integration queue: each merge holds it for ci + merge minutes, in readiness order;
//   - a slice MERGES only when its declared dependsOn AND its hidden merge dependencies are integrated.
// current:        a slice STARTS only when its declared dependsOn are integrated (STARTUP-INSTRUCTIONS section 4).
// contract-first: a UI slice STARTS when bootstrap (SL-T10) is integrated and its UI predecessors are BUILT (not merged),
//                 against the contract package and fixtures; non-UI slices keep the current rule.
open System
open System.IO
open System.Text.Json.Nodes

let argv = fsi.CommandLineArgs |> Array.skip 1 |> Array.skipWhile ((=) "--")
let manifestPath, rolesPath, hiddenPath, policy = argv.[0], argv.[1], argv.[2], argv.[3]
let ci = float argv.[4]
let setup = if argv.Length > 5 then float argv.[5] else 45.0
let merge = if argv.Length > 6 then float argv.[6] else 3.0

let node (p: string) = JsonNode.Parse(File.ReadAllText p) |> nonNull
let manifest = node manifestPath
let roles = (node rolesPath).["roles"].AsObject() |> Seq.map (fun kv -> kv.Key, (kv.Value |> nonNull).GetValue<string>()) |> Map.ofSeq
let hidden =
    (node hiddenPath).["rules"].AsArray()
    |> Seq.map (fun r ->
        let r = nonNull r
        let assertions = r.["assertions"].AsArray() |> Seq.map string |> set
        let needs = r.["needs"].AsArray() |> Seq.map string |> List.ofSeq
        assertions, needs)
    |> List.ofSeq

type Slice = { Id: string; Boundary: string; Deps: string list; Est: float option; Obligations: int; Assertions: string list }
let slices =
    manifest.["slices"].AsArray()
    |> Seq.map (fun s ->
        let s = nonNull s
        { Id = string s.["id"]; Boundary = string s.["implementationBoundary"]
          Deps = s.["dependsOn"].AsArray() |> Seq.map string |> List.ofSeq
          Est = (match s.["estimateMinutes"] with null -> None | v -> Some(v.GetValue<float>()))
          Obligations = s.["mandatoryObligations"].GetValue<int>()
          Assertions = s.["governedAssertions"].AsArray() |> Seq.map string |> List.ofSeq })
    |> List.ofSeq
let rate =
    let dag = slices |> List.filter (fun s -> s.Est.IsSome && s.Obligations > 0)
    (dag |> List.sumBy (fun s -> s.Est.Value)) / float (dag |> List.sumBy (fun s -> s.Obligations))
let minutes s = match s.Est with Some m -> m | None when s.Obligations > 0 -> Math.Ceiling(rate * float s.Obligations) | None -> 20.0
let roleOf s = roles |> Map.tryFind s.Id |> Option.defaultValue s.Boundary
let isUi s = s.Boundary = "ui"
let byId = slices |> List.map (fun s -> s.Id, s) |> Map.ofList
/// Hidden merge dependencies that would make a slice's gate wait, directly or through other gates, for work that waits
/// for the slice. Found over the whole merge graph (declared + hidden edges), deferred one at a time in a fixed order
/// until the graph is acyclic, and reported (cyclicMergeDependencies). Deferring means the slice merges without them:
/// exactly the owner decision CF-07 asks for. Applied identically to both policies.
let hiddenOf s = hidden |> List.collect (fun (a, needs) -> if s.Assertions |> List.exists a.Contains then needs else []) |> List.distinct |> List.filter (fun d -> d <> s.Id && byId.ContainsKey d)
let allHidden = slices |> List.collect (fun s -> hiddenOf s |> List.map (fun d -> s.Id, d))
let reaches (edges: (string * string) list) (src: string) (target: string) =
    let next = edges |> List.groupBy fst |> List.map (fun (k, v) -> k, v |> List.map snd) |> Map.ofList
    let rec go (seen: Set<string>) frontier =
        match frontier with
        | [] -> false
        | x :: rest when x = target -> true
        | x :: rest when seen.Contains x -> go seen rest
        | x :: rest -> go (seen.Add x) ((next |> Map.tryFind x |> Option.defaultValue []) @ rest)
    go Set.empty [ src ]
let declared = slices |> List.collect (fun s -> s.Deps |> List.map (fun d -> s.Id, d))
let cyclic =
    allHidden |> List.fold (fun deferred (a, b) ->
        let kept = allHidden |> List.filter (fun e -> not (List.contains e deferred))
        if reaches (declared @ kept) b a then deferred @ [ (a, b) ] else deferred) []
let mergeDeps s =
    (s.Deps @ (hiddenOf s |> List.filter (fun d -> not (List.contains (s.Id, d) cyclic)))) |> List.distinct
let rec tail =
    let memo = Collections.Generic.Dictionary<string, float>()
    fun (id: string) ->
        match memo.TryGetValue id with
        | true, v -> v
        | _ ->
            let v = minutes byId.[id] + (slices |> List.filter (fun x -> List.contains id x.Deps) |> List.map (fun x -> tail x.Id) |> List.fold max 0.0)
            memo.[id] <- v
            v

/// Discrete-event simulation over immutable maps (lane free times, built and integrated times).
type St = { Built: Map<string, float>; Integrated: Map<string, float>; LaneFree: Map<string, float>; QueueFree: float; Started: Set<string> }

let startTime (st: St) (s: Slice) : float option =
    let integ d = st.Integrated |> Map.tryFind d
    let built d = st.Built |> Map.tryFind d
    let need =
        if policy = "contract-first" && isUi s then
            (integ "SL-T10" |> Option.toList |> List.map Some)
            @ (s.Deps |> List.map (fun d -> if isUi byId.[d] then built d else if d = "SL-T10" then integ d else Some 0.0))
        else s.Deps |> List.map integ
    if need |> List.forall Option.isSome then Some(need |> List.choose id |> List.fold max setup) else None

let rec run (st: St) =
    let pending = slices |> List.filter (fun s -> not (st.Started.Contains s.Id))
    let startable =
        pending |> List.choose (fun s -> startTime st s |> Option.map (fun t -> s, max t (st.LaneFree |> Map.tryFind (roleOf s) |> Option.defaultValue setup)))
    let mergeable =
        slices |> List.filter (fun s -> st.Built.ContainsKey s.Id && not (st.Integrated.ContainsKey s.Id))
        |> List.choose (fun s ->
            let deps = mergeDeps s |> List.map (fun d -> st.Integrated |> Map.tryFind d)
            if deps |> List.forall Option.isSome then Some(s, deps |> List.choose id |> List.fold max st.Built.[s.Id]) else None)
    match startable |> List.sortBy (fun (s, t) -> t, -(tail s.Id)) |> List.tryHead, mergeable |> List.sortBy snd |> List.tryHead with
    | None, None -> st
    | Some(s, t), m when (match m with Some(_, mt) -> t <= max mt st.QueueFree | None -> true) ->
        let done' = t + minutes s
        run { st with Started = st.Started.Add s.Id; Built = st.Built.Add(s.Id, done'); LaneFree = st.LaneFree.Add(roleOf s, done') }
    | _, Some(s, ready) ->
        let finish = max ready st.QueueFree + ci + merge
        run { st with Integrated = st.Integrated.Add(s.Id, finish); QueueFree = finish }
    | Some(s, t), None -> run { st with Started = st.Started.Add s.Id; Built = st.Built.Add(s.Id, t + minutes s); LaneFree = st.LaneFree.Add(roleOf s, t + minutes s) }

let result = run { Built = Map.empty; Integrated = Map.empty; LaneFree = Map.empty; QueueFree = setup; Started = Set.empty }
let unfinished = slices |> List.filter (fun s -> not (result.Integrated.ContainsKey s.Id)) |> List.map (fun s -> s.Id, result.Built.ContainsKey s.Id, mergeDeps s |> List.filter (result.Integrated.ContainsKey >> not))
if not unfinished.IsEmpty then failwithf "not integrated (deadlock in the inputs): %A" unfinished
let capability = (node rolesPath).["capabilities"].AsObject() |> Seq.map (fun kv -> kv.Key, (nonNull kv.Value).AsArray() |> Seq.map string |> List.ofSeq) |> List.ofSeq
let at ids = ids |> List.map (fun i -> result.Integrated |> Map.tryFind i) |> fun xs -> if xs |> List.forall Option.isSome then Some(xs |> List.choose id |> List.max) else None
let uiSlices = slices |> List.filter isUi
let firstUiStart = uiSlices |> List.filter (fun s -> s.Id <> "SL-T16") |> List.map (fun s -> result.Built.[s.Id] - minutes s) |> List.min
let uiIdle =
    // UI lane-minutes inside [setup, last UI integration] not spent building.
    let lanes = uiSlices |> List.map roleOf |> List.distinct
    let horizon = uiSlices |> List.map (fun s -> result.Integrated.[s.Id]) |> List.max
    lanes |> List.sumBy (fun l -> (horizon - setup) - (uiSlices |> List.filter (fun s -> roleOf s = l) |> List.sumBy minutes))
let waitingToMerge = uiSlices |> List.sumBy (fun s -> result.Integrated.[s.Id] - result.Built.[s.Id])
let fmt = function Some (v: float) -> sprintf "T+%.0f" v | None -> "never"
printfn "policy=%s roles=%s ci=%.0f setup=%.0f merge=%.0f rate=%.3f" policy (Path.GetFileName rolesPath) ci setup merge rate
for (name, ids) in capability do printfn "  %-16s %s" name (fmt (at ids))
printfn "  %-16s %s" "all-slices" (fmt (Some(result.Integrated |> Map.toList |> List.map snd |> List.max)))
printfn "  cyclic merge dependencies (deferred, CF-07): %s" (cyclic |> List.map (fun (a, b) -> a + "->" + b) |> String.concat ", ")
printfn "  first non-shell UI slice starts: T+%.0f; UI lane idle: %.0f lane-min; UI built-but-unmerged: %.0f slice-min" firstUiStart uiIdle waitingToMerge
