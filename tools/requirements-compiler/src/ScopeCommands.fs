/// Command layer of the scope, progress and continuation checks: file reading and exit codes only.
///
///   requirements-compiler scope compile --source DIR --package DIR --out FILE
///   requirements-compiler scope preflight --source DIR --package DIR --manifest FILE [--praxis-queue FILE] [--out FILE]
///   requirements-compiler scope checkpoint --source DIR --package DIR --manifest FILE (--praxis-queue FILE | --praxis-context FILE)... --at ISO --commit SHA --out FILE
///   requirements-compiler scope work-plan --manifest FILE --out FILE   (the indy-init.r9-work-plan/1 authorize-implementation reads)
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
    let praxis = [ "--praxis-queue"; "--praxis-context" ] |> List.choose o.TryFind |> List.filter File.Exists |> List.map parseFile
    let queueRefusals = if o.ContainsKey "--praxis-queue" || o.ContainsKey "--praxis-context" then coverage manifest praxis else []
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
        if not ([ "--source"; "--package"; "--manifest"; "--at"; "--commit"; "--out" ] |> List.forall o.ContainsKey) || not (o.ContainsKey "--praxis-queue" || o.ContainsKey "--praxis-context") then
            eprintfn "usage: scope checkpoint --source DIR --package DIR --manifest FILE --praxis-queue FILE --at ISO --commit SHA --out FILE"; 2
        else
            let manifest, refusals = preflightAll o
            if refusals.IsEmpty then File.WriteAllText(o["--out"], serialize (checkpoint manifest o["--at"] o["--commit"]))
            finish "scope checkpoint" refusals
    | "work-plan" :: rest ->
        let o = options rest
        match o.TryFind "--manifest", o.TryFind "--out" with
        | Some m, Some out ->
            File.WriteAllText(out, serialize (workPlan (parseFile m)))
            printfn "scope work-plan: %s" out
            0
        | _ -> eprintfn "usage: scope work-plan --manifest FILE --out FILE"; 2
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
                // The T0 bootstrap (the commit that installs governance, .echelon/r9-governance.json) writes the generated
                // stubs before any checkpoint can exist; it is not implementation (as r9.fsx check-sequencing judges it).
                // Only that one commit: a later commit that also touches the governance file is judged like any other.
                let bootstrap = commits |> List.tryFind (fun (_, files) -> List.contains ".echelon/r9-governance.json" files) |> Option.map fst
                let afterBootstrap = commits |> List.filter (fun (sha, _) -> Some sha <> bootstrap)
                finish "scope order" (implementationBeforeCheckpoint cp isApplication afterBootstrap)
    | _ -> eprintfn "usage: scope compile|preflight|checkpoint|work-plan|order ..."; 2

let private eventsOf (path: string) =
    match parseFile path with
    | JArr xs -> xs
    | j -> arr "events" j

let progressCommand (args: string list) : int =
    let o = options args
    match o.TryFind "--manifest", o.TryFind "--events", o.TryFind "--out" with
    | Some m, Some e, Some out ->
        match Ledger.replay (parseFile m) (eventsOf e) |> Result.map (fun l -> l.Progress) with
        | Error why -> printfn "  REFUSED %s" why; printfn "progress: REFUSED"; 4
        | Ok s ->
            File.WriteAllText(out, serialize (Progress.report s))
            printfn "progress: %s" out
            0
    | _ -> eprintfn "usage: progress --manifest FILE --events FILE --out FILE"; 2

/// The run clock. With --t0-record and --deadline-policy (Rehearsal 010 and later) it is bound to the committed T0
/// record and the owner's policy; a --t0 or --window-minutes that differs is DEADLINE_RESET. Without them (earlier
/// replays) --t0 and --window-minutes are used as given.
/// Whether this invocation judges a historical replay (--replay-clock true): only then may the caller supply T0 or the
/// current time. A live run's clock is the committed T0 record, the pinned policy and this machine's time.
let private replayMode (o: Map<string, string>) = o.TryFind "--replay-clock" = Some "true"

/// The first committed version of the T0 record in --repo, when given: a record changed after T0 is DEADLINE_RESET.
let private committedT0 (o: Map<string, string>) (recordPath: string) : Result<unit, Refusal> =
    match o.TryFind "--repo" with
    | None -> Ok()
    | Some repo ->
        let rel = IO.Path.GetRelativePath(IO.Path.GetFullPath repo, IO.Path.GetFullPath recordPath).Replace('\\', '/')
        match Git.run repo [ "log"; "--reverse"; "--diff-filter=A"; "--format=%H"; "--"; rel ] with
        | Ok b ->
            match (Text.Encoding.UTF8.GetString b).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.tryHead with
            | None -> Error { Code = "T0_RECORD_UNCOMMITTED"; Detail = sprintf "%s is not committed in %s: the T0 record is committed at T0" rel repo }
            | Some sha ->
                match Git.run repo [ "show"; sprintf "%s:%s" sha rel ] with
                | Ok first ->
                    let firstT0 = parseBytes first |> strOpt "t0"
                    let nowT0 = parseFile recordPath |> strOpt "t0"
                    if firstT0 = nowT0 then Ok()
                    else Error { Code = "DEADLINE_RESET"; Detail = sprintf "the T0 record says %A; at T0 (commit %s) it said %A" nowT0 (sha.Substring(0, 12)) firstT0 }
                | Error e -> Error { Code = "T0_RECORD_UNCOMMITTED"; Detail = e }
        | Error e -> Error { Code = "T0_RECORD_UNCOMMITTED"; Detail = e }

let private clockOf (o: Map<string, string>) : Result<Continuation.Clock, Refusal> =
    let claimedT0 = o.TryFind "--t0" |> Option.map DateTimeOffset.Parse
    let claimedWindow = o.TryFind "--window-minutes" |> Option.map float
    let sha (path: string) = Security.Cryptography.SHA256.HashData(IO.File.ReadAllBytes path) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()
    match o.TryFind "--t0-record", o.TryFind "--deadline-policy" with
    | Some t, Some p ->
        // A live run's policy must be the one the owner pinned: read from the governed lock (deadlinePolicy.sha256),
        // and its T0 record must be the one committed at T0 (--repo). The observer recomputes the same deadline
        // independently and compares it with each decision's, so a self-made lock or policy is caught there.
        let pinned = o.TryFind "--lock" |> Option.filter IO.File.Exists |> Option.bind (fun l -> parseFile l |> field "deadlinePolicy" |> Option.bind (strOpt "sha256"))
        match pinned with
        | None when not (replayMode o) -> Error { Code = "DEADLINE_POLICY_UNAPPROVED"; Detail = "a live run needs --lock (the governed toolchain lock with deadlinePolicy.sha256)" }
        | _ when not (replayMode o) && not (o.ContainsKey "--repo") -> Error { Code = "T0_RECORD_UNCOMMITTED"; Detail = "a live run needs --repo: the T0 record must be the one committed at T0" }
        | Some pinned when pinned <> sha p -> Error { Code = "DEADLINE_POLICY_UNAPPROVED"; Detail = sprintf "the deadline policy has sha256 %s; the lock pins %s" (sha p) pinned }
        | _ -> committedT0 o t |> Result.bind (fun () -> Continuation.boundClock (parseFile t) (parseFile p) claimedT0 claimedWindow)
    | Some _, None -> Error { Code = "DEADLINE_POLICY_MISSING"; Detail = "--t0-record needs --deadline-policy" }
    | None, _ ->
        // A caller-supplied T0 is accepted only for an explicit historical replay; a live run binds the clock to its record.
        match claimedT0 with
        | Some t0 when replayMode o -> Ok { T0 = t0; Deadline = t0.AddMinutes(claimedWindow |> Option.defaultValue 420.0) }
        | Some _ -> Error { Code = "T0_RECORD_MISSING"; Detail = "a live run's clock comes from --t0-record and --deadline-policy; --t0 alone is accepted only with --replay-clock true (a historical replay)" }
        | None -> Error { Code = "T0_RECORD_MISSING"; Detail = "no --t0-record" }

/// A request may not be stamped after the moment it is judged: this machine's time in a live run (--now is accepted
/// only in a historical replay). A request dated past the deadline does not make the deadline pass.
let private futureRequest (o: Map<string, string>) (at: DateTimeOffset) : Refusal option =
    let now =
        match o.TryFind "--now" with
        | Some n when replayMode o -> DateTimeOffset.Parse n
        | _ -> DateTimeOffset.UtcNow
    if at > now.AddMinutes 2.0 then Some { Code = "REQUEST_IN_FUTURE"; Detail = sprintf "the request is dated %s, after the time it is judged (%s)" (at.ToString "o") (now.ToString "o") } else None

/// Exclusions the owner approved: trusted operator records {requirement_id, action: exclude} in the pinned approvals.
let exclusionsOf (o: Map<string, string>) : Set<string * string> =
    match o.TryFind "--approvals", o.TryFind "--trusted-operator" with
    | Some a, Some op ->
        Continuation.approvalChanges (parseFile a) (set [ op ])
        |> List.filter (fun c -> c.Action = "exclude" && c.RequirementId <> "")
        |> List.collect (fun c -> [ c.RequirementId, c.RecordId; c.RequirementId, c.DecisionId ])
        |> Set.ofList
    | _ -> Set.empty

/// The decision as written, with the clock it was judged against (so an observer can compare it with the T0 record).
let private withClock (replay: bool) (clock: Continuation.Clock) (j: Json) =
    match j with
    | JObj ps -> JObj(ps @ [ "t0", JStr(clock.T0.ToString "o"); "deadline", JStr(clock.Deadline.ToString "o"); "clock", JStr(if replay then "replay" else "live") ])
    | j -> j

let stopDecisionCommand (args: string list) : int =
    let o = options args
    if not ([ "--manifest"; "--checkpoint"; "--events"; "--request"; "--out" ] |> List.forall o.ContainsKey) || not (o.ContainsKey "--t0" || o.ContainsKey "--t0-record") then
        eprintfn "usage: stop-decision --manifest FILE --checkpoint FILE --events FILE --request FILE (--t0-record FILE --deadline-policy FILE --lock FILE --repo DIR | --t0 ISO --replay-clock true [--window-minutes 420]) [--now ISO] [--approvals FILE --trusted-operator ID] --out FILE"; 2
    else
        match clockOf o with
        | Error refusal -> printfn "  REFUSED %s %s" refusal.Code refusal.Detail; printfn "stop-decision: REFUSED (the run clock)"; 4
        | Ok clock ->
        let manifest = parseFile o["--manifest"]
        let approvals =
            match o.TryFind "--approvals", o.TryFind "--trusted-operator" with
            | Some a, Some op -> Continuation.approvalChanges (parseFile a) (set [ op ])
            | _ -> []
        let requestJson = parseFile o["--request"]
        let request = Continuation.requestOf requestJson
        match Ledger.replayWith (exclusionsOf o) manifest (eventsOf o["--events"]) with
        | Error why -> printfn "  REFUSED %s" why; printfn "stop-decision: REFUSED (the progress events do not replay)"; 4
        | Ok ledger ->
            let s = ledger.Progress
            let d0 = Continuation.decide s clock (str "scopeSha256" (parseFile o["--checkpoint"])) approvals request
            // "complete" also needs every applicable mandatory requirement integrated on main, not only verified.
            let unintegrated =
                arr "requirements" manifest
                |> List.filter (fun r -> boolOr "mandatory" false r && Ledger.applies request.Facts r && not (List.contains (Ledger.ledgerState ledger (str "id" r)) [ Ledger.Integrated; Ledger.Excluded ]))
            let d =
                if request.Reason = "complete" && request.At < clock.Deadline && not unintegrated.IsEmpty then
                    { d0 with Permitted = false; Refusals = d0.Refusals @ [ { Code = "SCOPE_NOT_INTEGRATED"; Detail = sprintf "%d applicable mandatory requirement(s) are not integrated on main" unintegrated.Length } ] }
                else d0
            let d =
                match futureRequest o request.At with
                | Some r -> { d with Permitted = false; Refusals = r :: d.Refusals }
                | None -> d
            let d =
                if (strOpt "at" requestJson |> Option.bind Continuation.instant).IsNone then
                    { d with Permitted = false; Refusals = { Code = "REQUEST_UNDATED"; Detail = "the stop request's at is missing or has no explicit UTC offset" } :: d.Refusals }
                else d
            // "complete" before the deadline also needs the full-scope completion gate's COMPLETE verdict (--completion).
            let d =
                // The verdict must be the governed completion gate's, for the repository's current head.
                // HEAD must be main: a verdict dispatched on an unmerged branch is not completion of the run.
                let revOf r (ref: string) = match Git.run r [ "rev-parse"; ref ] with Ok b -> Some((Text.Encoding.UTF8.GetString b).Trim()) | Error _ -> None
                let head = o.TryFind "--repo" |> Option.bind (fun r -> match revOf r "HEAD", revOf r "refs/heads/main" with Some h, Some m when h = m -> Some h | _ -> None)
                let complete =
                    o.TryFind "--completion" |> Option.filter IO.File.Exists |> Option.map parseFile
                    |> Option.exists (fun c ->
                        strOpt "schema" c = Some "indy-init.completion-gate/1" && strOpt "verdict" c = Some "COMPLETE"
                        && (replayMode o || (head.IsSome && strOpt "candidate" c = head)))
                if request.Reason = "complete" && request.At < clock.Deadline && not complete then
                    let why =
                        match o.TryFind "--completion" |> Option.filter IO.File.Exists |> Option.map parseFile with
                        | None -> { Code = "COMPLETION_GATE_MISSING"; Detail = "stopping as complete needs the governed completion gate's COMPLETE verdict (--completion)" }
                        | Some c when strOpt "schema" c <> Some "indy-init.completion-gate/1" || strOpt "verdict" c <> Some "COMPLETE" -> { Code = "COMPLETION_GATE_MISSING"; Detail = "--completion is not a completion-gate COMPLETE verdict" }
                        | Some c -> { Code = "COMPLETION_CANDIDATE_MISMATCH"; Detail = sprintf "the completion verdict is for %s; the repository's HEAD must be main and that commit (%s)" (strOpt "candidate" c |> Option.defaultValue "(none)") (head |> Option.defaultValue "HEAD is not main") }
                    { d with Permitted = false; Refusals = d.Refusals @ [ why ] }
                else d
            File.WriteAllText(o["--out"], serialize (withClock (replayMode o) clock (Continuation.decisionJson request d)))
            d.Refusals |> List.iter (fun r -> printfn "  REFUSED %s %s" r.Code r.Detail)
            printfn "stop-decision: %s (%d minute(s) remaining, %d open mandatory requirement(s))" (if d.Permitted then "STOP_PERMITTED" else "CONTINUE_REQUIRED") d.MinutesRemaining d.OpenMandatory.Length
            if d.Permitted && d.Refusals.IsEmpty then 0 else 4

/// Recovery: a successor session's plan, from repository files alone. Exit 0 for CONTINUE (and the other actions
/// that need no repair), 4 when the checkpoint must be repaired first.
let resumeCommand (args: string list) : int =
    let o = options args
    if not ([ "--manifest"; "--checkpoint"; "--events"; "--at"; "--out" ] |> List.forall o.ContainsKey) || not (o.ContainsKey "--t0" || o.ContainsKey "--t0-record") then
        eprintfn "usage: resume --manifest FILE --checkpoint FILE --events FILE (--t0-record FILE --deadline-policy FILE --lock FILE --repo DIR | --t0 ISO --replay-clock true [--window-minutes 420]) --at ISO [--now ISO] [--stop-decision FILE] --out FILE"; 2
    else
        match clockOf o with
        | Error refusal -> printfn "  REFUSED %s %s" refusal.Code refusal.Detail; printfn "resume: REFUSED (the run clock)"; 4
        | Ok _ when (futureRequest o (DateTimeOffset.Parse o["--at"])).IsSome ->
            let r = (futureRequest o (DateTimeOffset.Parse o["--at"])).Value
            printfn "  REFUSED %s %s" r.Code r.Detail; printfn "resume: REFUSED"; 4
        | Ok clock ->
        let manifest = parseFile o["--manifest"]
        let at = DateTimeOffset.Parse o["--at"]
        let checkpointScope = if File.Exists o["--checkpoint"] then strOpt "scopeSha256" (parseFile o["--checkpoint"]) |> Option.defaultValue "" else ""
        let stopped = o.TryFind "--stop-decision" |> Option.filter File.Exists |> Option.exists (fun f -> strOpt "decision" (parseFile f) = Some "STOP_PERMITTED")
        match Ledger.replay manifest (eventsOf o["--events"]) |> Result.map (fun l -> l.Progress) with
        | Error why -> printfn "  REFUSED %s" why; printfn "resume: REFUSED (the progress events do not replay)"; 4
        | Ok s ->
            let p = Continuation.resumePlan s clock checkpointScope at stopped
            File.WriteAllText(o["--out"], serialize (Continuation.resumeJson at p))
            p.Refusals |> List.iter (fun r -> printfn "  REFUSED %s %s" r.Code r.Detail)
            printfn "resume: %s (%d minute(s) remaining, next: %s)" p.Action p.MinutesRemaining (p.NextWork |> List.truncate 5 |> String.concat ", ")
            if p.Refusals.IsEmpty then 0 else 4
