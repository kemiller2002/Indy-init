// change-impact.fsx: Level 1/2 test selection from a list of changed paths (PROPOSED; never part of the governed gate).
//
//   dotnet fsi tools/r10/change-impact.fsx -- --indy-init . select PATH...     print what to run now
//   dotnet fsi tools/r10/change-impact.fsx -- --indy-init . test               adversarial tests of the selection
//
// Inputs: docs/rehearsal-010-optimization/execution-readiness/CHANGE-IMPACT-MATRIX.json and the frozen v4.1.4
// ownership and project structure. The selection is a feedback aid only: it verifies nothing, and every slice merge
// still runs the full governed gate (Level 3).

open System
open System.IO
open System.Text.Json.Nodes
open System.Text.RegularExpressions

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let root = args |> List.pairwise |> List.tryFind (fst >> (=) "--indy-init") |> Option.map snd |> Option.defaultValue "."
let rest = args |> List.filter (fun a -> a <> "--indy-init" && a <> root)
let at (p: string) = Path.Combine(root, p)
let parse (p: string) = JsonNode.Parse(File.ReadAllText(at p))
let strs (n: JsonNode) = match n with null -> [] | n -> n.AsArray() |> Seq.map string |> List.ofSeq

type Rule = { Id: string; Pattern: Regex; Selects: string list }
type Model = { FileProject: Map<string, string>; Dependents: Map<string, Set<string>>; TestProjects: Set<string>; Rules: Rule list }

let load () : Model =
    let own = parse "requirements-compiled/v4.1.4/agent-file-ownership.json"
    let ps = parse "requirements-compiled/v4.1.4/analysis/project-structure.json"
    let matrix = parse "docs/rehearsal-010-optimization/execution-readiness/CHANGE-IMPACT-MATRIX.json"
    let fileProject =
        own["files"].AsArray() |> Seq.choose (fun f -> match f["project"] with null -> None | p -> Some(string f["path"], string p)) |> Map.ofSeq
    let edges =
        [ for p in ps["projects"].AsArray() do for r in strs p["references"] -> string r, string p["id"]
          for p in ps["testProjects"].AsArray() do for r in strs p["references"] -> string r, string p["id"] ]
    let direct = edges |> List.groupBy fst |> List.map (fun (k, v) -> k, v |> List.map snd |> Set.ofList) |> Map.ofList
    let rec closure (seen: Set<string>) (p: string) =
        if seen.Contains p then seen else (Map.tryFind p direct |> Option.defaultValue Set.empty) |> Set.fold closure (seen.Add p)
    let projects = edges |> List.collect (fun (a, b) -> [ a; b ]) |> List.distinct
    { FileProject = fileProject
      Dependents = projects |> List.map (fun p -> p, closure Set.empty p) |> Map.ofList
      TestProjects = ps["testProjects"].AsArray() |> Seq.map (fun p -> string p["id"]) |> Set.ofSeq
      Rules =
        matrix["rules"].AsArray()
        |> Seq.map (fun r -> { Id = string r["id"]; Pattern = Regex(string r["pattern"]); Selects = strs r["selects"] })
        |> List.ofSeq }

type Selection = { All: bool; TestProjects: Set<string>; Suites: Set<string>; Rules: Set<string>; Unknown: string list }

/// What to run now for these changed paths. Pure: the same model and paths always give the same selection.
let select (m: Model) (paths: string list) : Selection =
    let one (path: string) =
        let p = path.Replace('\\', '/').TrimStart('.', '/')
        let rules = m.Rules |> List.filter (fun r -> r.Pattern.IsMatch p)
        let projects = Map.tryFind p m.FileProject |> Option.map (fun pr -> Map.tryFind pr m.Dependents |> Option.defaultValue (Set.singleton pr))
        let inGraph = Map.tryFind p m.FileProject |> Option.map (fun pr -> m.Dependents.ContainsKey pr) |> Option.defaultValue false
        let underSource = [ "src/"; "tests/"; "verifier/" ] |> List.exists (fun d -> p.StartsWith d)
        let selected = (projects |> Option.defaultValue Set.empty |> Set.intersect m.TestProjects |> Set.isEmpty |> not)
                       || (rules |> List.exists (fun r -> not r.Selects.IsEmpty))
        // review RVO-6-1: fail safe when a path is unknown, when it sits under a source tree but outside the project
        // graph (a new file, or a project the graph lacks), or when a known path would select nothing at all
        let known = projects.IsSome || not rules.IsEmpty
        let failSafe = (not known) || (underSource && not inGraph) || not selected
        { All = failSafe || (rules |> List.exists (fun r -> List.contains "ALL" r.Selects))
          TestProjects = projects |> Option.defaultValue Set.empty |> Set.intersect m.TestProjects
          Suites = rules |> List.collect (fun r -> r.Selects |> List.filter ((<>) "ALL")) |> Set.ofList
          Rules = rules |> List.map (fun r -> r.Id) |> Set.ofList
          Unknown = if failSafe then [ p ] else [] }
    paths |> List.map one
    |> List.fold (fun a b -> { All = a.All || b.All; TestProjects = a.TestProjects + b.TestProjects; Suites = a.Suites + b.Suites; Rules = a.Rules + b.Rules; Unknown = a.Unknown @ b.Unknown })
        { All = false; TestProjects = Set.empty; Suites = Set.empty; Rules = Set.empty; Unknown = [] }

let render (s: Selection) =
    [ if s.All then "run: EVERYTHING (Level 3 plan)" + (if s.Unknown.IsEmpty then "" else sprintf "; unrecognised paths: %s" (String.Join(", ", s.Unknown)))
      else
          sprintf "run test projects: %s" (if s.TestProjects.IsEmpty then "(none)" else String.Join(", ", s.TestProjects))
          sprintf "run suites/tags: %s" (if s.Suites.IsEmpty then "(none)" else String.Join(", ", s.Suites))
      "verified by this selection: nothing. Every slice merge still runs the full governed gate (Level 3)." ]

let tests (m: Model) =
    let mutable failed = 0
    let case (name: string) (ok: bool) (detail: unit -> string) =
        if ok then printfn "%-86s PASS" name else (failed <- failed + 1; printfn "%-86s FAIL\n    %s" name (detail ()))
    let s = select m
    let all = m.TestProjects
    let show (x: Selection) = sprintf "%A" x
    // the production closure: Domain is referenced by Persistence, App and Host, so a Domain change reaches App's tests
    case "the project graph is loaded: Domain's dependents include Causa.App and Causa.Host"
        ((Map.tryFind "Causa.Domain" m.Dependents |> Option.defaultValue Set.empty) |> fun d -> d.Contains "Causa.App" && d.Contains "Causa.Host" && d.Contains "Causa.App.Tests") (fun () -> sprintf "%A" (Map.tryFind "Causa.Domain" m.Dependents))
    let a = s [ "src/Causa.Domain/Entities.fs" ]
    case "a shared F# record (Domain/Entities.fs) selects every dependent test project" (a.TestProjects = all && a.Suites.Contains "TAG:historical-replay") (fun () -> show a)
    let b = s [ "src/Causa.Persistence/GitHubRequests.fs" ]
    case "the GitHub adapter selects the real-provider suites" (b.Suites.Contains "SU-DOTNET-LIVE" && b.Suites.Contains "SU-BROWSER-LIVE" && not (b.TestProjects.Contains "Causa.Domain.Tests")) (fun () -> show b)
    let c = s [ "src/Causa.Domain/Evolve.fs" ]
    case "event replay (Domain/Evolve.fs) selects the historical replay tests" (c.Suites.Contains "TAG:historical-replay") (fun () -> show c)
    let d = s [ "src/Causa.App/Views/Dashboard.fs" ]
    case "a UI screen (field identifiers) selects traceability and both browsers" (d.Suites.Contains "TAG:traceability" && d.Suites.Contains "SU-BROWSER-WEBKIT") (fun () -> show d)
    let e = s [ "web/causa.css" ]
    case "the shared stylesheet (Forma) selects every browser suite and the design contract" (e.Suites.Contains "TAG:design-contract" && e.Suites.Contains "SU-BROWSER-LIVE" && e.Suites.Contains "SU-BROWSER-CHROMIUM") (fun () -> show e)
    let f = s [ "src/Causa.App/Views/Report.fs" ]
    case "an unrelated screen does not select domain or persistence unit tests" (not f.All && not (f.TestProjects.Contains "Causa.Domain.Tests") && not (f.TestProjects.Contains "Causa.Persistence.Tests")) (fun () -> show f)
    let g = s [ "src/Causa.App/NewThing.fs" ]
    case "an unrecognised path selects everything (fail-safe)" (g.All && g.Unknown = [ "src/Causa.App/NewThing.fs" ]) (fun () -> show g)
    let h = s [ "requirements-compiled/v4.1.4/contracts/00-DOMAIN-STUBS.fs" ]
    case "a frozen contract selects everything" h.All (fun () -> show h)
    let i = s [ "src/Causa.App/Causa.App.fsproj" ]
    case "a project file selects everything" i.All (fun () -> show i)
    let j = s [ "src/Causa.App/Views/Report.fs"; "src/Causa.Domain/Entities.fs" ]
    case "a mixed change is the union: the shared record still selects every dependent" (j.TestProjects = all) (fun () -> show j)
    let k = s [ "./src\\Causa.Domain\\Evolve.fs" ]
    case "path spelling (./, backslashes) cannot dodge a rule" (k.Suites.Contains "TAG:historical-replay") (fun () -> show k)
    let l = s []
    case "no change selects nothing and still verifies nothing" (not l.All && l.TestProjects.IsEmpty && (render l |> List.exists (fun x -> x.StartsWith "verified by this selection: nothing"))) (fun () -> show l)
    case "the renderer never reports a requirement as verified"
        ([ a; b; c; d; e; f; g; l ] |> List.forall (fun x -> render x |> List.exists (fun r -> r.Contains "verified by this selection: nothing"))) (fun () -> "")
    // negative control on the model itself: with the replay rule removed, the replay case must fail
    let noReplay = select { m with Rules = m.Rules |> List.filter (fun r -> r.Id <> "CI-REPLAY") } [ "src/Causa.Domain/Evolve.fs" ]
    case "control: removing the replay rule makes the replay case fail (the test can bite)" (not (noReplay.Suites.Contains "TAG:historical-replay")) (fun () -> show noReplay)
    // review RVO-6-1: under-selection without failing safe
    let r1 = s [ "src/Causa.Host/Host.cs" ]
    case "the process entry (Host.cs) is never a silent no-op" (r1.All || r1.Suites.Contains "SU-BROWSER-CHROMIUM") (fun () -> show r1)
    let r2 = s [ "verifier/Causa.Acceptance.Independent/PAT13ProvenanceComparison.fs" ]
    case "a file of a project the graph lacks selects everything" r2.All (fun () -> show r2)
    let r3 = s [ "src/Causa.App/Views/NewScreen.fs" ]
    case "a new source file (not in any compile list) selects everything" r3.All (fun () -> show r3)
    let r4 = s [ "src/Causa.App/Program.fs" ]
    case "composition (Program.fs) selects the browser suites" (r4.All || (r4.Suites.Contains "SU-BROWSER-CHROMIUM" && r4.Suites.Contains "SU-BROWSER-LIVE")) (fun () -> show r4)
    case "no known path ever selects nothing (every manifest file selects a test or everything)"
        (m.FileProject |> Map.toList |> List.forall (fun (path, _) -> let x = s [ path ] in x.All || not x.TestProjects.IsEmpty || not x.Suites.IsEmpty))
        (fun () -> m.FileProject |> Map.toList |> List.map fst |> List.filter (fun path -> let x = s [ path ] in not x.All && x.TestProjects.IsEmpty && x.Suites.IsEmpty) |> List.truncate 5 |> String.concat ", ")
    printfn "\nchange-impact tests: %s" (if failed = 0 then "all passed" else sprintf "%d failed" failed)
    failed

match rest with
| "select" :: paths -> load () |> fun m -> select m paths |> render |> List.iter (printfn "%s")
| [ "test" ] -> exit (tests (load ()))
| _ -> eprintfn "usage: change-impact.fsx -- --indy-init DIR (select PATH... | test)"; exit 2
