/// Command layer of the scope, progress and continuation checks: file reading and exit codes only.
///
///   requirements-compiler scope compile --source DIR --package DIR --out FILE
///   requirements-compiler scope preflight --source DIR --package DIR --manifest FILE [--praxis-queue FILE] [--out FILE]
///   requirements-compiler scope checkpoint --source DIR --package DIR --manifest FILE --praxis-queue FILE --at ISO --commit SHA --out FILE
///   requirements-compiler scope order --repo DIR [--checkpoint PATH]
///   requirements-compiler progress --manifest FILE --events FILE --out FILE
///   requirements-compiler resume --manifest FILE --checkpoint FILE --events FILE --t0 ISO --at ISO [--window-minutes 420]
///                         [--stop-decision FILE] --out FILE
///   requirements-compiler stop-decision --manifest FILE --checkpoint FILE --events FILE --request FILE --t0 ISO
///                         [--window-minutes 420] [--approvals FILE --trusted-operator ID] --out FILE
///
/// Exit codes: 0 pass (or STOP_PERMITTED with no refusal); 4 refused (CONTINUE_REQUIRED, or any refusal); 2 unusable input.
module ScopeCommands

open System
open System.IO
open Json
open Scope

let private options (args: string list) =
    args |> List.pairwise |> List.filter (fun (k, _) -> k.StartsWith("--", StringComparison.Ordinal)) |> Map.ofList

let private sha (path: string) = File.ReadAllBytes path |> Git.sha256Hex

/// The frozen sources of a package, read from an Indy-init checkout.
let sourcesOf (source: string) (package: string) : Sources =
    let p (rel: string) = Path.Combine(source, rel)
    let pkg (rel: string) = Path.Combine(source, package, rel)
    let commit = match Git.headCommit source with Ok c -> c | Error _ -> ""
    { Plan = parseFile (p "requirements-compiled/v4/verification-plan.json")
      PlanSha256 = sha (p "requirements-compiled/v4/verification-plan.json")
      ExecutionPlan = parseFile (pkg "verification-execution-plan.json")
      ExecutionPlanSha256 = sha (pkg "verification-execution-plan.json")
      Dag = parseFile (pkg "execution-dag.json")
      PackageManifest = parseFile (pkg "package-manifest.json")
      PackageManifestSha256 = sha (pkg "package-manifest.json")
      PackagePath = package
      Catalog = parseFile (p "requirements/catalogs/causa-investigation.requirement-catalog.json")
      CatalogSha256 = sha (p "requirements/catalogs/causa-investigation.requirement-catalog.json")
      Baseline = parseFile (p "requirements/causa-requirements-baseline-v4.json")
      BaselineSha256 = sha (p "requirements/causa-requirements-baseline-v4.json")
      Repository = "kemiller2002/Indy-init"
      // The source commit is informational: preflight compares content, so a later commit with unchanged sources passes.
      Commit = commit }

let private verdictJson (name: string) (refusals: Refusal list) =
    JObj [ "schema", JStr "indy-init.scope-verdict/1"; "check", JStr name; "verdict", JStr(if refusals.IsEmpty then "PASS" else "REFUSED"); "refusals", JArr(refusals |> List.map refusalJson) ]

let private finish (name: string) (refusals: Refusal list) =
    refusals |> List.iter (fun r -> printfn "  REFUSED %s %s" r.Code r.Detail)
    printfn "%s: %s" name (if refusals.IsEmpty then "PASS" else sprintf "REFUSED (%d)" refusals.Length)
    if refusals.IsEmpty then 0 else 4

let private preflightAll (o: Map<string, string>) =
    let expected = compile (sourcesOf o["--source"] o["--package"])
    let manifest = parseFile o["--manifest"]
    let refusals = preflight expected manifest
    let queueRefusals = o.TryFind "--praxis-queue" |> Option.map (fun q -> coverage manifest (parseFile q)) |> Option.defaultValue []
    manifest, refusals @ queueRefusals

let scopeCommand (args: string list) : int =
    match args with
    | "compile" :: rest ->
        let o = options rest
        match o.TryFind "--source", o.TryFind "--package", o.TryFind "--out" with
        | Some s, Some p, Some out ->
            let m = compile (sourcesOf s p)
            File.WriteAllText(out, serialize m)
            printfn "scope compile: %s (%s requirements, scopeSha256 %s)" out (field "counts" m |> Option.bind (intOpt "requirements") |> Option.map string |> Option.defaultValue "?") (str "scopeSha256" m)
            0
        | _ -> eprintfn "usage: scope compile --source DIR --package DIR --out FILE"; 2
    | "preflight" :: rest ->
        let o = options rest
        if not ([ "--source"; "--package"; "--manifest" ] |> List.forall o.ContainsKey) then eprintfn "usage: scope preflight --source DIR --package DIR --manifest FILE [--praxis-queue FILE] [--out FILE]"; 2
        else
            let _, refusals = preflightAll o
            o.TryFind "--out" |> Option.iter (fun out -> File.WriteAllText(out, serialize (verdictJson "scope-preflight" refusals)))
            finish "scope preflight" refusals
    | "checkpoint" :: rest ->
        let o = options rest
        if not ([ "--source"; "--package"; "--manifest"; "--praxis-queue"; "--at"; "--commit"; "--out" ] |> List.forall o.ContainsKey) then
            eprintfn "usage: scope checkpoint --source DIR --package DIR --manifest FILE --praxis-queue FILE --at ISO --commit SHA --out FILE"; 2
        else
            let manifest, refusals = preflightAll o
            if refusals.IsEmpty then File.WriteAllText(o["--out"], serialize (checkpoint manifest o["--at"] o["--commit"]))
            finish "scope checkpoint" refusals
    | "order" :: rest ->
        let o = options rest
        match o.TryFind "--repo" with
        | None -> eprintfn "usage: scope order --repo DIR [--checkpoint PATH]"; 2
        | Some repo ->
            let cp = o.TryFind "--checkpoint" |> Option.defaultValue ".echelon/scope-checkpoint.json"
            match Git.run repo [ "log"; "--reverse"; "--name-only"; "--format=@%H" ] with
            | Error e -> eprintfn "%s" e; 2
            | Ok bytes ->
                let commits =
                    (Text.Encoding.UTF8.GetString bytes).Split('@', StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun block -> let lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries) in lines[0].Trim(), lines |> Array.skip 1 |> Array.map (fun l -> l.Trim()) |> List.ofArray)
                    |> List.ofArray
                let isApplication (path: string) = (path.StartsWith("src/", StringComparison.Ordinal) || path.StartsWith("tests/", StringComparison.Ordinal) || path.StartsWith("web/", StringComparison.Ordinal)) && not (path.EndsWith(".fsproj", StringComparison.Ordinal))
                finish "scope order" (implementationBeforeCheckpoint cp isApplication commits)
    | _ -> eprintfn "usage: scope compile|preflight|checkpoint|order ..."; 2

let private eventsOf (path: string) =
    match parseFile path with
    | JArr xs -> xs
    | j -> arr "events" j

let progressCommand (args: string list) : int =
    let o = options args
    match o.TryFind "--manifest", o.TryFind "--events", o.TryFind "--out" with
    | Some m, Some e, Some out ->
        match Progress.replay (parseFile m) (eventsOf e) with
        | Error why -> printfn "  REFUSED %s" why; printfn "progress: REFUSED"; 4
        | Ok s ->
            File.WriteAllText(out, serialize (Progress.report s))
            printfn "progress: %s" out
            0
    | _ -> eprintfn "usage: progress --manifest FILE --events FILE --out FILE"; 2

let stopDecisionCommand (args: string list) : int =
    let o = options args
    if not ([ "--manifest"; "--checkpoint"; "--events"; "--request"; "--t0"; "--out" ] |> List.forall o.ContainsKey) then
        eprintfn "usage: stop-decision --manifest FILE --checkpoint FILE --events FILE --request FILE --t0 ISO [--window-minutes 420] [--approvals FILE --trusted-operator ID] --out FILE"; 2
    else
        let manifest = parseFile o["--manifest"]
        let t0 = DateTimeOffset.Parse o["--t0"]
        let window = o.TryFind "--window-minutes" |> Option.map float |> Option.defaultValue 420.0
        let approvals =
            match o.TryFind "--approvals", o.TryFind "--trusted-operator" with
            | Some a, Some op -> Continuation.approvalChanges (parseFile a) (set [ op ])
            | _ -> []
        let request = Continuation.requestOf (parseFile o["--request"])
        match Progress.replay manifest (eventsOf o["--events"]) with
        | Error why -> printfn "  REFUSED %s" why; printfn "stop-decision: REFUSED (the progress events do not replay)"; 4
        | Ok s ->
            let d = Continuation.decide s { T0 = t0; Deadline = t0.AddMinutes window } (str "scopeSha256" (parseFile o["--checkpoint"])) approvals request
            File.WriteAllText(o["--out"], serialize (Continuation.decisionJson request d))
            d.Refusals |> List.iter (fun r -> printfn "  REFUSED %s %s" r.Code r.Detail)
            printfn "stop-decision: %s (%d minute(s) remaining, %d open mandatory requirement(s))" (if d.Permitted then "STOP_PERMITTED" else "CONTINUE_REQUIRED") d.MinutesRemaining d.OpenMandatory.Length
            if d.Permitted && d.Refusals.IsEmpty then 0 else 4

/// Recovery: a successor session's plan, from repository files alone. Exit 0 for CONTINUE (and the other actions
/// that need no repair), 4 when the checkpoint must be repaired first.
let resumeCommand (args: string list) : int =
    let o = options args
    if not ([ "--manifest"; "--checkpoint"; "--events"; "--t0"; "--at"; "--out" ] |> List.forall o.ContainsKey) then
        eprintfn "usage: resume --manifest FILE --checkpoint FILE --events FILE --t0 ISO --at ISO [--window-minutes 420] [--stop-decision FILE] --out FILE"; 2
    else
        let manifest = parseFile o["--manifest"]
        let t0 = DateTimeOffset.Parse o["--t0"]
        let window = o.TryFind "--window-minutes" |> Option.map float |> Option.defaultValue 420.0
        let at = DateTimeOffset.Parse o["--at"]
        let checkpointScope = if File.Exists o["--checkpoint"] then strOpt "scopeSha256" (parseFile o["--checkpoint"]) |> Option.defaultValue "" else ""
        let stopped = o.TryFind "--stop-decision" |> Option.filter File.Exists |> Option.exists (fun f -> strOpt "decision" (parseFile f) = Some "STOP_PERMITTED")
        match Progress.replay manifest (eventsOf o["--events"]) with
        | Error why -> printfn "  REFUSED %s" why; printfn "resume: REFUSED (the progress events do not replay)"; 4
        | Ok s ->
            let p = Continuation.resumePlan s { T0 = t0; Deadline = t0.AddMinutes window } checkpointScope at stopped
            File.WriteAllText(o["--out"], serialize (Continuation.resumeJson at p))
            p.Refusals |> List.iter (fun r -> printfn "  REFUSED %s %s" r.Code r.Detail)
            printfn "resume: %s (%d minute(s) remaining, next: %s)" p.Action p.MinutesRemaining (p.NextWork |> List.truncate 5 |> String.concat ", ")
            if p.Refusals.IsEmpty then 0 else 4
