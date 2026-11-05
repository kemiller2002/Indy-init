// schedule-model.fsx: the Rehearsal 010 schedule model, extended to the constraints schedule.fsx leaves out.
//
// schedule.fsx (unchanged; its committed evidence stays reproducible) simulates slices on lanes with one serialized
// integration queue. This model is the same simulation, minute by minute, with these additions, each off by default
// so that the defaults reproduce schedule.fsx exactly (schedule-model-tests.fsx checks that):
//
//   --roles FILE         slice -> owning role (indy-init.slice-roles/1). The frozen file ownership of v4.1.4
//                        (agent-file-ownership.json) gives ui and ui-2 disjoint files, so a ui-2 lane cannot take a ui
//                        slice. Without --roles a slice's role is its implementationBoundary (schedule.fsx's view).
//   --queue-depth N      speculative verification: up to N candidates are verified at once, each on main plus the
//                        candidates ahead of it in merge order; merges stay strictly one at a time and in order, and a
//                        verification slot is released only when its candidate merges. N = 1 is schedule.fsx's queue.
//   --fail-every K       deterministic rework: every K-th slice (in first-verification order) fails its first verification once; it is repaired on its own lane
//                        (--repair-fraction of its estimate) and verified again, and every speculative run stacked on
//                        it is restarted. 0 (default) = no failures.
//   --scale F            multiply every implementation estimate by F (sensitivity), default 1.
//   --slice-overhead M   add M minutes to every slice (branch, pull request, context switching), default 0.
//   --milestones FILE    functional milestones (indy-init.r10-milestones/1); reports when each is delivered (every slice
//                        carrying each of its requirements integrated) and how many are usable at T+120/240/360/420.
//   --priority P         tail (default, schedule.fsx: longest chain first) or milestones (the earliest milestone a
//                        slice serves, directly or as an ancestor, first; ties by longest chain).
//   --setup, --ci, --merge, --window, --lanes, --default-minutes: as in schedule.fsx.
//
//   dotnet fsi schedule-model.fsx -- --slices slice-execution-manifest.json --setup 45 --ci 0,2,5,10 --merge 3 \
//     [--roles roles.json] [--queue-depth 3] [--fail-every 4 --repair-fraction 0.25] [--scale 1.25] --out result.json
//
// Every number it prints is a model result from estimates, not an observation.

#load "schedule-core.fsx"
open ScheduleCore

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let opt (name: string) = args |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let req name = opt name |> Option.defaultWith (fun () -> failwithf "missing %s" name)
let num name dflt = opt name |> Option.map float |> Option.defaultValue dflt

let config =
    { Setup = num "--setup" 60.0
      Merge = num "--merge" 3.0
      Window = num "--window" 420.0
      QueueDepth = opt "--queue-depth" |> Option.map int |> Option.defaultValue 1
      FailEvery = opt "--fail-every" |> Option.map int |> Option.defaultValue 0
      RepairFraction = num "--repair-fraction" 0.25
      Scale = num "--scale" 1.0
      SliceOverhead = num "--slice-overhead" 0.0
      Milestones = opt "--milestones" |> Option.map loadMilestones |> Option.defaultValue []
      Priority = opt "--priority" |> Option.defaultValue "tail"
      DefaultMinutes = num "--default-minutes" 20.0
      Lanes = parseLanes (opt "--lanes" |> Option.defaultValue defaultLanes) }
let slices = loadSlices (req "--slices") (opt "--roles") config
let cis = (opt "--ci" |> Option.defaultValue "0,2,5,10,15,20").Split(',') |> Array.map float |> List.ofArray
let report = run config slices cis
printReport report
opt "--out" |> Option.iter (fun out -> writeReport out report)
