// schedule-model-tests.fsx: regression tests and negative controls for the schedule model (schedule-core.fsx).
//
//   dotnet fsi tools/r10/schedule-model-tests.fsx -- --indy-init .
//
// 1. Equivalence: with its defaults the model reproduces schedule.fsx's committed evidence, slice by slice.
// 2. Invariants on every scenario it runs: a lane implements only slices of the roles it holds; a slice starts only
//    after every dependency is integrated; merges are strictly one at a time; every slice integrates once.
// 3. Behaviour: a deeper speculative queue is never slower; a failed verification makes the slice run again and
//    restarts what was stacked on it; failures never make the plan finish earlier; scaling estimates up never helps.
// 4. Refusals: a dependency cycle, an unknown dependency, and a role no lane holds.

#load "schedule-core.fsx"
open System
open System.IO
open System.Text.Json.Nodes
open ScheduleCore

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let src = args |> List.pairwise |> List.tryFind (fst >> (=) "--indy-init") |> Option.map snd |> Option.defaultValue "."
let at (p: string) = Path.Combine(src, p)

let mutable failures = 0
let case (name: string) (problems: string list) =
    if problems.IsEmpty then printfn "%-72s PASS" name
    else
        failures <- failures + 1
        printfn "%-72s FAIL" name
        problems |> List.truncate 5 |> List.iter (printfn "    %s")

let cfg setup lanes = { Setup = setup; Merge = 3.0; Window = 420.0; QueueDepth = 1; FailEvery = 0; RepairFraction = 0.25; Scale = 1.0; SliceOverhead = 0.0; Milestones = []; Priority = "tail"; DefaultMinutes = 20.0; Lanes = parseLanes lanes }
let manifest = at "docs/rehearsal-010-readiness/slice-execution-manifest.json"
let roles = at "docs/rehearsal-010-optimization/inputs/slice-roles.v414.json"
let ownLanes = "integration:integration,domain:domain,persistence:persistence,ui:ui,ui-2:ui-2,reviewer:reviewer"

/// The invariants every simulated scenario must hold.
let invariants (c: Config) (slices: Slice list) (recs: Map<string, Record>) : string list =
    let laneRoles = c.Lanes |> Map.ofList
    let integrated = slices |> List.map (fun s -> recs[s.Id].Integrated) |> List.sort
    [ for s in slices do
        let r = recs[s.Id]
        if r.Integrated < 0.0 then yield sprintf "%s never integrated" s.Id
        if not (List.contains s.Role laneRoles[r.Lane]) then yield sprintf "%s (role %s) ran on lane %s, which does not hold it" s.Id s.Role r.Lane
        for d in s.Deps do
            if r.Start < recs[d].Integrated then yield sprintf "%s started at %.0f before its dependency %s integrated at %.0f" s.Id r.Start d recs[d].Integrated
        if r.Start < c.Setup then yield sprintf "%s started at %.0f, inside the set-up" s.Id r.Start
        if r.Runs < 1 then yield sprintf "%s integrated with no verification run" s.Id
      if c.Merge > 0.0 then
          for a, b in List.pairwise integrated do
              if b - a < c.Merge then yield sprintf "two merges overlap: integrations at %.0f and %.0f with %.0f-minute merges" a b c.Merge
      // Merges follow queue order (no candidate overtakes one ahead of it). With one CI duration and no failures, queue
      // order is the order of first verification. Review RVO-2-2: in that configuration an out-of-order merge is hardly
      // reachable, so this is a guard, not a proof of the speculative semantics (the model has one CI duration).
      if c.FailEvery = 0 then
          let byFirst = slices |> List.sortBy (fun s -> recs[s.Id].FirstVerification, recs[s.Id].Integrated) |> List.map (fun s -> recs[s.Id].Integrated)
          for a, b in List.pairwise byFirst do
              if b < a then yield sprintf "a candidate verified later merged earlier (integrations at %.0f then %.0f): merge order is not queue order" a b
      // and a repair costs its lane time
      for s in slices do
          if recs[s.Id].Repairs > 0 && recs[s.Id].RepairMinutes <= 0.0 && c.RepairFraction > 0.0 then yield sprintf "%s was repaired in no time" s.Id ]

// ---- 1. equivalence with schedule.fsx's committed evidence ----------------------------------------------------------

for evidence, lanes in [ "docs/rehearsal-010-readiness/evidence/schedule/topology-a.json", defaultLanes
                         "docs/rehearsal-010-readiness/evidence/schedule/topology-a-three-ui.json", "integration:integration,core:domain|persistence,ui-1:ui,ui-2:ui,ui-3:ui,reviewer:reviewer" ] do
    let committed = JsonNode.Parse(File.ReadAllText(at evidence))
    let c = cfg (committed["setupMinutes"].GetValue<float>()) lanes
    let slices = loadSlices manifest None c
    let problems =
        [ for sc in committed["scenarios"].AsArray() do
            let ci = sc["ciMinutes"].GetValue<float>()
            let recs = simulate c ci slices
            let finish = recs |> Map.toList |> List.map (fun (_, r) -> r.Integrated) |> List.max
            if finish <> sc["allIntegratedAt"].GetValue<float>() then yield sprintf "ci %.0f: finish %.0f, schedule.fsx %.0f" ci finish (sc["allIntegratedAt"].GetValue<float>())
            for e in sc["timeline"].AsArray() do
                let id = e["slice"].ToString()
                let r = recs[id]
                if r.Integrated <> e["integrated"].GetValue<float>() || r.Start <> e["start"].GetValue<float>() || r.Lane <> e["lane"].ToString() then
                    yield sprintf "ci %.0f %s: model %s %.0f..%.0f, schedule.fsx %s %.0f..%.0f" ci id r.Lane r.Start r.Integrated (e["lane"].ToString()) (e["start"].GetValue<float>()) (e["integrated"].GetValue<float>()) ]
    case (sprintf "defaults reproduce schedule.fsx (%s)" (Path.GetFileName evidence)) problems

// ---- 2-3. invariants and behaviour on the real manifests -------------------------------------------------------------

let variants =
    [ "baseline", manifest, None, defaultLanes
      "ownership", manifest, Some roles, ownLanes
      "successor split", at "docs/rehearsal-010-optimization/proposals/slice-execution-manifest.successor.json", Some roles, ownLanes ]
for name, m, r, lanes in variants do
    let c = cfg 45.0 lanes
    let slices = loadSlices m r c
    for depth in [ 1; 3 ] do
        for fail in [ 0; 4 ] do
            for ci in [ 0.0; 10.0 ] do
                let c' = { c with QueueDepth = depth; FailEvery = fail }
                case (sprintf "invariants: %s, depth %d, fail every %d, ci %.0f" name depth fail ci) (invariants c' slices (simulate c' ci slices))

let finishOf (c: Config) ci slices = simulate c ci slices |> Map.toList |> List.map (fun (_, r) -> r.Integrated) |> List.max
let ownC = cfg 45.0 ownLanes
let ownSlices = loadSlices manifest (Some roles) ownC
case "a deeper speculative queue is never slower (depth 1..4, ci 0..20)"
    [ for ci in [ 0.0; 5.0; 10.0; 20.0 ] do
        let fs = [ 1 .. 4 ] |> List.map (fun d -> d, finishOf { ownC with QueueDepth = d } ci ownSlices)
        for (d1, f1), (d2, f2) in List.pairwise fs do
            if f2 > f1 then yield sprintf "ci %.0f: depth %d finishes at %.0f, depth %d at %.0f" ci d2 f2 d1 f1 ]
case "verification failures never make the plan finish earlier"
    [ for ci in [ 0.0; 10.0 ] do
        for d in [ 1; 3 ] do
            let clean = finishOf { ownC with QueueDepth = d } ci ownSlices
            for k in [ 2; 4; 8 ] do
                let f = finishOf { ownC with QueueDepth = d; FailEvery = k } ci ownSlices
                if f < clean then yield sprintf "ci %.0f depth %d fail every %d: %.0f, clean %.0f" ci d k f clean ]
case "larger estimates never make the plan finish earlier"
    [ for ci in [ 0.0; 10.0 ] do
        let fs = [ 0.75; 1.0; 1.25; 1.5 ] |> List.map (fun s -> s, finishOf ownC ci (loadSlices manifest (Some roles) { ownC with Scale = s }))
        for (s1, f1), (s2, f2) in List.pairwise fs do
            if f2 < f1 then yield sprintf "ci %.0f: scale %.2f finishes at %.0f, scale %.2f at %.0f" ci s2 f2 s1 f1 ]
case "ownership-accurate lanes: no ui slice ever runs on the ui-2 lane"
    [ let recs = simulate ownC 0.0 ownSlices
      for s in ownSlices do
        if s.Role = "ui" && recs[s.Id].Lane <> "ui" then yield sprintf "%s ran on %s" s.Id recs[s.Id].Lane ]

// synthetic: A <- B <- C on one lane each; depth 3; the first verification run fails
let synth (deps: (string * string list) list) (role: string -> string) =
    deps |> List.map (fun (id, d) -> { Id = id; Role = role id; Deps = d; Minutes = 10.0; Source = "test"; MandatoryObligations = 1; Requirements = [ id ] })
let three = synth [ "A", []; "B", []; "C", [] ] (fun id -> id)
let threeC = { cfg 0.0 "a:A,b:B,c:C" with QueueDepth = 3; FailEvery = 1 }
case "a failed verification is repaired and verified again; stacked candidates restart"
    [ // fail every 1: every slice fails its first verification once, is repaired and passes on its next run
      let recs = simulate threeC 5.0 three
      for s in three do
          if recs[s.Id].Repairs <> 1 then yield sprintf "%s repaired %d times, expected 1" s.Id recs[s.Id].Repairs
          if recs[s.Id].Runs < 2 then yield sprintf "%s verified %d times, expected at least 2" s.Id recs[s.Id].Runs
      // A's failure restarts the speculative runs of B and C stacked behind it: more runs than one retry each
      let runs = three |> List.sumBy (fun s -> recs[s.Id].Runs)
      if runs <= 6 then yield sprintf "%d verification runs: the candidates stacked on a failure were not restarted" runs
      let none = simulate { threeC with FailEvery = 0 } 5.0 three
      if three |> List.exists (fun s -> none[s.Id].Repairs > 0) then yield "a slice was repaired with no failures configured" ]

// review RVO-2-2: assertions that bite on the parameters the published results use
case "a repair costs exactly ceil(estimate x repair fraction) of its lane"
    [ let c = { cfg 0.0 "a:A,b:B,c:C" with QueueDepth = 1; FailEvery = 1; RepairFraction = 0.25 }
      let recs = simulate c 5.0 three
      for s in three do
          let want = Math.Ceiling(s.Minutes * 0.25) * float recs[s.Id].Repairs
          if recs[s.Id].RepairMinutes <> want then yield sprintf "%s repaired for %.0f lane-minutes, expected %.0f" s.Id recs[s.Id].RepairMinutes want ]
case "per-slice overhead is charged to every slice"
    [ let plain = loadSlices manifest None (cfg 45.0 defaultLanes)
      let over = loadSlices manifest None { cfg 45.0 defaultLanes with SliceOverhead = 5.0 }
      for a, b in List.zip plain over do
          if b.Minutes <> a.Minutes + 5.0 then yield sprintf "%s: %.0f minutes with overhead 5, %.0f without" a.Id b.Minutes a.Minutes ]

// ---- 4. refusals ---------------------------------------------------------------------------------------------------

let refused (name: string) (expect: string) (f: unit -> obj) =
    case name (match (try Ok(f ()) with e -> Error e.Message) with
               | Error m when m.Contains expect -> []
               | Error m -> [ sprintf "refused, but with: %s" m ]
               | Ok _ -> [ sprintf "accepted; expected a refusal containing '%s'" expect ])
refused "a dependency cycle is refused" "cycle" (fun () -> box (cpm 0.0 0.0 0.0 (synth [ "A", [ "B" ]; "B", [ "A" ] ] (fun _ -> "x"))))
refused "an unknown dependency is refused" "unknown" (fun () -> box (cpm 0.0 0.0 0.0 (synth [ "A", [ "Z" ] ] (fun _ -> "x"))))
refused "a role no lane holds is refused (no silent omission)" "deadlock" (fun () -> box (simulate (cfg 0.0 "a:A") 0.0 (synth [ "A", []; "B", [] ] id)))

printfn ""
printfn "schedule-model-tests: %s" (if failures = 0 then "all passed" else sprintf "%d failed" failures)
exit (if failures = 0 then 0 else 1)
