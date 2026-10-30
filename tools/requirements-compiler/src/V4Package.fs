/// Compiled package v4 (Indy Init event, November 7): the artifacts the v3 pipeline does not produce, the checks that
/// bind them to the baseline and the verification plan, and the freeze gate.
///
/// Pure. Every function maps already-read inputs to JSON or to problems; Program does the reading and writing. The
/// v3 pipeline (Program.compile) must already have written the package's verification plan, verification manifest,
/// dependency graph and timed manifest; this stage reads them back, so it checks what is actually committed.
///
/// Nothing here approves, verifies or freezes by itself: the freeze gate reports each condition from recorded inputs
/// (owner approval records, the compiler test record, the independent review record) and declares FROZEN only when
/// every condition holds. An agent cannot satisfy a condition by writing to this module's inputs, because approvals
/// must be operator records in the pinned approvals file (RDP-003) and the review record must name a reviewer who is
/// not the compiler's author.
module V4Package

open System
open System.Text.RegularExpressions
open Json
open Model

let schemaPrefix = "indy-init.v4-"

type Inputs =
    { SourceRepository: string
      SourceCommit: string
      Baseline: Json
      BaselineSha256: string
      /// Earlier baselines, oldest first: (label, document).
      Previous: (string * Json) list
      /// docs/rehearsal-007/traceability.json
      R7Trace: Json
      /// kickoff/rehearsal-00N/owner-decisions.json decisions, all rehearsals.
      OwnerDecisions: Json list
      /// analysis/amendments.json: requirement changes made in the v4 source cut.
      Amendments: Json
      Plan: Json
      Tasks: WorkTask list
      Contracts: Json list
      Definitions: Json
      StateMachines: Json
      Invariants: Json
      Lessons: Json
      Interfaces: Json
      Policy: Json
      Risks: Json
      LockSource: Json
      Proposals: Json
      Approvals: Json
      /// Reads a file of echelon-registry at the pinned commit; None when no checkout was supplied.
      Registry: (string -> byte[] option) option
      RegistryCommit: string
      /// Reads one of Rehearsal 7's five evidence files; None when no checkout was supplied.
      R7Evidence: (string -> string option) option
      LegacyReport: Json
      CompilerTests: Json
      CompilerTreeSha256: string
      Review: Json
      FreezeInput: Json
      /// Paths of every file under the package directory, relative to it.
      PackageFiles: string list }

type Check = { Id: string; Title: string; Critical: bool; Problems: string list }

let private check id title critical problems = { Id = id; Title = title; Critical = critical; Problems = problems }

let checkJson (c: Check) =
    JObj
        [ "id", JStr c.Id
          "title", JStr c.Title
          "critical", JBool c.Critical
          "status", JStr(if c.Problems.IsEmpty then "PASS" else "FAIL")
          "problems", jstrs c.Problems ]

// ---- baseline helpers -----------------------------------------------------------------------------

/// A string field, or "" when the record does not carry it (optional records: test record, review, freeze input).
let private sOr (key: string) (j: Json) = strOpt key j |> Option.defaultValue ""

let private reqs (b: Json) = arr "requirements" b
let private reqMap (b: Json) = reqs b |> List.map (fun r -> str "id" r, r) |> Map.ofList
let private oblTexts (r: Json) = arr "obligations" r |> List.map (fun o -> str "id" o, str "text" o)
let private p0Ids (b: Json) = reqs b |> List.filter (fun r -> str "priority" r = "P0") |> List.map (str "id")
let private known (i: Inputs) = reqMap i.Baseline
let private obligationText (i: Inputs) = reqs i.Baseline |> List.collect oblTexts |> Map.ofList

let private proposalIds (i: Inputs) = arr "requests" i.Proposals |> List.map (str "id") |> Set.ofList

/// The selector-free requirement id check used by every analysis file: an id must be in the v4 baseline.
let private unknownIds (i: Inputs) (where: string) (ids: string list) =
    let k = known i
    ids |> List.filter (k.ContainsKey >> not) |> List.map (sprintf "%s: '%s' is not a requirement of the v4 baseline" where)

let private unknownProposals (i: Inputs) (where: string) (ids: string list) =
    let p = proposalIds i
    ids |> List.filter (p.Contains >> not) |> List.map (sprintf "%s: '%s' is not an owner decision request" where)

// ---- baseline reconciliation ---------------------------------------------------------------------

/// Owner decisions that name a requirement as affected, by requirement id.
let private decisionsByRequirement (i: Inputs) =
    i.OwnerDecisions
    |> List.collect (fun d -> strs "affectedRequirements" d |> List.map (fun r -> r, str "id" d))
    |> List.groupBy fst
    |> List.map (fun (r, xs) -> r, xs |> List.map snd |> List.distinct |> List.sort)
    |> Map.ofList

let private r7Changed (i: Inputs) =
    arr "changedRequirements" i.R7Trace |> List.map (fun c -> str "requirement" c, c) |> Map.ofList

let private v4Amended (i: Inputs) =
    arr "requirements" i.Amendments |> List.map (fun a -> str "requirement" a, a) |> Map.ofList

type private Step = { Label: string; Before: Map<string, Json>; After: Map<string, Json> }

let private steps (i: Inputs) =
    let chain = i.Previous @ [ "v4", i.Baseline ]
    chain |> List.pairwise |> List.map (fun ((la, a), (lb, b)) -> { Label = sprintf "%s->%s" la lb; Before = reqMap a; After = reqMap b })

/// What happened to one requirement in one step, and the authority for it (None = unauthorized).
let private stepChange (i: Inputs) (s: Step) (rid: string) : (string * string list * string) option =
    let decisions = decisionsByRequirement i
    let toR7 = s.Label.EndsWith("->r7", StringComparison.Ordinal)
    let authority () =
        if toR7 then
            let trace = (r7Changed i).TryFind rid |> Option.map (fun c -> strs "decision" c) |> Option.defaultValue []
            let owner = decisions.TryFind rid |> Option.defaultValue []
            let introduced =
                match s.After.TryFind rid with
                | Some r when str "document" r = "docs/CAUSA-PERSISTENCE-REQUIREMENTS.md" -> [ "ADR-R7-001" ]
                | _ -> []
            trace @ owner @ introduced |> List.distinct
        else
            (v4Amended i).TryFind rid |> Option.map (strs "decisions") |> Option.defaultValue []
    let reason () =
        if toR7 then (r7Changed i).TryFind rid |> Option.map (str "reason") |> Option.defaultValue ""
        else (v4Amended i).TryFind rid |> Option.map (str "reason") |> Option.defaultValue ""
    match s.Before.TryFind rid, s.After.TryFind rid with
    | None, None -> None
    | None, Some _ -> Some("added", authority (), reason ())
    | Some _, None -> Some("removed", authority (), reason ())
    | Some a, Some b when str "sectionSha256" a <> str "sectionSha256" b || str "priority" a <> str "priority" b -> Some("amended", authority (), reason ())
    | Some _, Some _ -> None

let reconciliationProblems (i: Inputs) : string list =
    let all = i.Previous |> List.collect (snd >> reqs) |> List.map (str "id") |> List.append (reqs i.Baseline |> List.map (str "id")) |> List.distinct
    let changeProblems =
        steps i
        |> List.collect (fun s ->
            all
            |> List.choose (fun rid ->
                match stepChange i s rid with
                | Some(kind, [], _) -> Some(sprintf "%s: %s was %s without a recorded authority" s.Label rid kind)
                | Some("removed", _, _) -> Some(sprintf "%s: %s disappeared from the baseline" s.Label rid)
                | _ -> None))
    let priorityProblems =
        steps i
        |> List.collect (fun s ->
            s.Before
            |> Map.toList
            |> List.choose (fun (rid, a) ->
                match s.After.TryFind rid with
                | Some b when str "priority" a = "P0" && str "priority" b <> "P0" -> Some(sprintf "%s: P0 %s was downgraded to %s" s.Label rid (str "priority" b))
                | _ -> None))
    let amendmentProblems =
        arr "requirements" i.Amendments
        |> List.collect (fun a ->
            let rid = str "requirement" a
            let recorded = i.OwnerDecisions |> List.map (str "id") |> Set.ofList
            [ if not ((known i).ContainsKey rid) then yield sprintf "amendments.json: %s is not in the v4 baseline" rid
              for d in strs "decisions" a do
                  if not (recorded.Contains d) then yield sprintf "amendments.json: %s cites %s, which no owner-decisions.json records" rid d ])
    changeProblems @ priorityProblems @ amendmentProblems

/// Requirements changed between the R6 and R7 baselines whose authority is an owner decision but which
/// docs/rehearsal-007/traceability.json does not list (a documentation gap, reported, not a failure).
let traceabilityGaps (i: Inputs) : string list =
    steps i
    |> List.filter (fun s -> s.Label.EndsWith("->r7", StringComparison.Ordinal))
    |> List.collect (fun s ->
        s.After
        |> Map.toList
        |> List.choose (fun (rid, _) ->
            match stepChange i s rid with
            | Some("amended", _, _) when not ((r7Changed i).ContainsKey rid) -> Some rid
            | _ -> None))

let reconciliation (i: Inputs) : Json =
    let all = i.Previous |> List.collect (snd >> reqs) |> List.map (str "id") |> List.append (reqs i.Baseline |> List.map (str "id")) |> List.distinct |> List.sort
    let maps = (i.Previous @ [ "v4", i.Baseline ]) |> List.map (fun (l, b) -> l, reqMap b)
    let k = known i
    let ids = k |> Map.keys |> Set.ofSeq
    let reqOf (r: Json) : Requirement =
        { Id = str "id" r; Title = str "title" r; Priority = str "priority" r; Document = str "document" r; Heading = str "heading" r
          SectionSha256 = str "sectionSha256" r; Repository = ""; Commit = ""; Origin = "baseline"
          Obligations = arr "obligations" r |> List.map (fun o -> { Id = str "id" o; Kind = str "kind" o; Text = str "text" o; Tokens = strs "tokens" o }) }
    let superseded =
        arr "supersededObligations" i.R7Trace
        |> List.map (fun o -> str "previous" o, o)
        |> List.groupBy (fun (p, _) -> p.Substring(0, p.LastIndexOf("-O", StringComparison.Ordinal)))
        |> Map.ofList
    let entry rid =
        let current = k.TryFind rid
        let history =
            steps i
            |> List.choose (fun s ->
                stepChange i s rid
                |> Option.map (fun (kind, auth, reason) ->
                    JObj [ "step", JStr s.Label; "change", JStr kind; "authority", jstrs auth; "reason", JStr reason ]))
        let presence = maps |> List.map (fun (l, m) -> l, m.TryFind rid)
        let disposition =
            match current, history with
            | None, _ -> "removed"
            | Some _, [] -> "preserved"
            | Some _, h when h |> List.exists (fun e -> str "change" e = "amended") -> "amended-with-authority"
            | Some _, _ -> "added-with-authority"
        let last = current |> Option.orElse (presence |> List.rev |> List.tryPick snd)
        JObj
            [ "id", JStr rid
              "title", jopt JStr (last |> Option.map (str "title"))
              "document", jopt JStr (last |> Option.map (str "document"))
              "source", JObj [ "repository", JStr i.SourceRepository; "commit", JStr i.SourceCommit; "baselineSha256", JStr i.BaselineSha256 ]
              "priority", JObj(presence |> List.map (fun (l, r) -> l, jopt JStr (r |> Option.map (str "priority"))))
              "sectionSha256", JObj(presence |> List.map (fun (l, r) -> l, jopt JStr (r |> Option.map (str "sectionSha256"))))
              "heading", jopt JStr (last |> Option.map (str "heading"))
              "obligations",
              (last |> Option.map (fun r -> arr "obligations" r |> List.map (fun o -> JObj [ "id", JStr(str "id" o); "kind", JStr(str "kind" o); "text", JStr(str "text" o) ])) |> Option.defaultValue [] |> JArr)
              "acceptanceObligations", (last |> Option.map (fun r -> arr "obligations" r |> List.filter (fun o -> str "kind" o = "acceptance") |> List.map (str "id")) |> Option.defaultValue [] |> jstrs)
              "dependencies", (current |> Option.map (reqOf >> Inventory.referencesOf ids) |> Option.defaultValue [] |> jstrs)
              "history", JArr history
              "supersededObligations",
              (superseded.TryFind rid |> Option.defaultValue [] |> List.map (fun (_, o) -> JObj [ "previous", JStr(str "previous" o); "previousText", JStr(str "previousText" o); "supersededBy", jstrs (strs "supersededBy" o) ]) |> JArr)
              "disposition", JStr disposition ]
    let entries = all |> List.map entry
    let count d = entries |> List.filter (fun e -> str "disposition" e = d) |> List.length
    let stepCount label kind =
        steps i |> List.filter (fun s -> s.Label = label) |> List.collect (fun s -> all |> List.choose (stepChange i s)) |> List.filter (fun (k, _, _) -> k = kind) |> List.length
    JObj
        [ "schema", JStr(schemaPrefix + "baseline-reconciliation/1")
          "description", JStr "Every requirement of the R6 (v3), R7 and v4 baselines with its source, priority in each, obligations, dependencies (requirement ids its own text names), each change and the authority for it. No requirement disappears without a disposition; a change without a recorded owner decision or ADR fails check W01."
          "baselines", JArr((i.Previous @ [ "v4", i.Baseline ]) |> List.map (fun (l, b) -> JObj [ "label", JStr l; "id", JStr(str "id" b); "commit", JStr(field "source" b |> Option.map (str "commit") |> Option.defaultValue ""); "requirements", jint (reqs b).Length; "p0", jint (p0Ids b).Length ]))
          "authoritative", JObj [ "id", JStr(str "id" i.Baseline); "commit", JStr i.SourceCommit; "sha256", JStr i.BaselineSha256; "why", JStr "The v4 baseline is the R7 baseline (BL-CAUSA-INDY-INIT-8445ca11986f, used read-only by Rehearsal 7) plus the owner-approved R7-AMD-03..05 written as DCC-013..015 (DEC-R7-008..010). It is the newest pinned baseline and the only one carrying every merged owner decision." ]
          "summary",
          JObj
              [ "requirements", jint entries.Length
                "preserved", jint (count "preserved")
                "amendedWithAuthority", jint (count "amended-with-authority")
                "addedWithAuthority", jint (count "added-with-authority")
                "removed", jint (count "removed")
                "r6ToR7", JObj [ "added", jint (stepCount "r6->r7" "added"); "amended", jint (stepCount "r6->r7" "amended"); "removed", jint (stepCount "r6->r7" "removed") ]
                "r7ToV4", JObj [ "added", jint (stepCount "r7->v4" "added"); "amended", jint (stepCount "r7->v4" "amended"); "removed", jint (stepCount "r7->v4" "removed") ]
                "traceabilityGaps", jstrs (traceabilityGaps i) ]
          "requirements", JArr entries ]

// ---- dependency lock ------------------------------------------------------------------------------

let private registryJson (i: Inputs) (path: string) = i.Registry |> Option.bind (fun read -> read path) |> Option.map parseBytes

let dependencyLock (i: Inputs) : Json * string list =
    let set = field "releaseSet" i.LockSource |> Option.defaultValue JNull
    let setPath = str "path" set
    match i.Registry with
    | None -> JObj [ "schema", JStr(schemaPrefix + "dependency-lock/1"); "error", JStr "no echelon-registry checkout supplied" ], [ "dependency lock: no echelon-registry checkout supplied (--registry)" ]
    | Some read ->
        let setBytes = read setPath
        let setDoc = setBytes |> Option.map parseBytes |> Option.defaultValue JNull
        let components = arr "components" setDoc |> List.map (fun c -> str "systemId" c, c) |> Map.ofList
        let entry (s: Json) =
            let id = str "id" s
            match str "kind" s with
            | "registry-component" ->
                let sid = str "systemId" s
                match components.TryFind sid with
                | None -> JObj [ "id", JStr id; "status", JStr "not-in-release-set" ], [ sprintf "dependency lock: %s is not a component of %s" sid setPath ]
                | Some c ->
                    let version = str "version" c
                    let manifestPath = sprintf "releases/%s/%s.release.json" sid version
                    match read manifestPath with
                    | None -> JObj [ "id", JStr id; "status", JStr "release-manifest-missing" ], [ sprintf "dependency lock: %s is unreadable at %s" manifestPath i.RegistryCommit ]
                    | Some bytes ->
                        let m = parseBytes bytes
                        let sha = Git.sha256Hex bytes
                        let problems =
                            [ if sha <> str "releaseManifestSha256" c then yield sprintf "dependency lock: %s hashes to %s, the set records %s" manifestPath sha (str "releaseManifestSha256" c)
                              if str "version" m <> version then yield sprintf "dependency lock: %s declares version %s" manifestPath (str "version" m)
                              if str "releaseStage" m <> "stable" then yield sprintf "dependency lock: %s %s is %s, not stable (VER-005)" sid version (str "releaseStage" m) ]
                        let package = arr "artifacts" m |> List.tryFind (fun a -> str "purpose" a = "package")
                        JObj
                            [ "id", JStr id
                              "systemId", JStr sid
                              "version", JStr version
                              "kind", JStr "registry-component"
                              "use", JStr(str "use" s)
                              "repository", JStr(str "repository" m)
                              "tag", JStr(str "tag" m)
                              "commit", JStr(str "commit" m)
                              "releaseStage", JStr(str "releaseStage" m)
                              "distributionClass", JStr(str "distributionClass" m)
                              "distributions", JArr(arr "distributions" m)
                              "packageDigest", (package |> Option.map (fun a -> JObj [ "artifact", JStr(str "name" a); "sha256", JStr(str "sha256" a) ]) |> Option.defaultValue JNull)
                              "releaseManifest", JObj [ "path", JStr manifestPath; "sha256", JStr sha ]
                              "runtimeCompatibility", JStr(str "runtimeCompatibility" s)
                              "availability", JStr(if problems.IsEmpty then "released-stable-in-approved-set" else "unverified") ],
                        problems
            | kind ->
                JObj(
                    [ "id", JStr id; "kind", JStr kind; "version", JStr(str "version" s); "use", JStr(str "use" s); "runtimeCompatibility", JStr(str "runtimeCompatibility" s) ]
                    @ ([ "source"; "repo"; "tag"; "commit"; "path"; "sha256" ] |> List.choose (fun k -> strOpt k s |> Option.map (fun v -> k, JStr v)))
                    @ [ "availability", JStr(match kind with
                                             | "sdk" -> "pinned by global.json"
                                             | "external-api" -> "external service; verified live by the provider-conformance task"
                                             | _ -> "pinned by tag or commit; identity checked where a checkout is supplied") ]),
                []
        let built = arr "systems" i.LockSource |> List.map entry
        let setProblems =
            [ if setBytes.IsNone then yield sprintf "dependency lock: %s is unreadable at %s" setPath i.RegistryCommit
              if setBytes.IsSome && str "status" setDoc <> "approved" then yield sprintf "dependency lock: %s is %s, not approved" setPath (str "status" setDoc) ]
        let forbidden = strs "forbiddenFrameworks" i.LockSource |> List.map (fun s -> s.ToLowerInvariant()) |> Set.ofList
        let frameworkProblems =
            arr "systems" i.LockSource |> List.map (str "id") |> List.filter (fun s -> forbidden.Contains(s.ToLowerInvariant())) |> List.map (sprintf "dependency lock: %s is a forbidden framework")
        JObj
            [ "schema", JStr(schemaPrefix + "dependency-lock/1")
              "description", JStr(str "description" i.LockSource)
              "releaseSet",
              JObj
                  [ "channel", JStr(str "channel" set)
                    "version", JStr(str "version" set)
                    "registry", JStr "kemiller2002/echelon-registry"
                    "registryCommit", JStr i.RegistryCommit
                    "path", JStr setPath
                    "sha256", (setBytes |> Option.map (Git.sha256Hex >> JStr) |> Option.defaultValue JNull)
                    "status", JStr(str "status" setDoc)
                    "decision", JStr(str "decision" set) ]
              "approvedStack", JArr(arr "approvedStack" i.LockSource)
              "forbiddenFrameworks", JArr(arr "forbiddenFrameworks" i.LockSource)
              "interopBoundaries", JArr(arr "interopBoundaries" i.LockSource)
              "systems", JArr(built |> List.map fst) ],
        setProblems @ frameworkProblems @ (built |> List.collect snd)

// ---- schedule (TimedSchedule) ----------------------------------------------------------------------------------------


let executionPlan (i: Inputs) : Json * string list =
    let window = field "clock" i.Policy |> Option.bind (intOpt "timedMinutes") |> Option.map int |> Option.defaultValue 420
    let reserves = arr "reserves" i.Policy |> List.sumBy (fun r -> intOpt "minutes" r |> Option.map int |> Option.defaultValue 0)
    let timed = i.Tasks |> List.filter (fun t -> t.Phase = "timed")
    let missingEstimates = timed |> List.filter (fun t -> (TimedSchedule.estimate t).IsNone) |> List.map (fun t -> sprintf "execution plan: timed task %s has no estimateMinutes" t.Id)
    let path, pathMinutes = TimedSchedule.criticalPath i.Tasks
    let topology (id: string) =
        let slots = TimedSchedule.schedule (TimedSchedule.laneOf id) i.Tasks
        let makespan = slots |> List.map (fun s -> s.Finish) |> List.fold max 0
        let lanes = slots |> List.groupBy (fun s -> s.Lane) |> List.sortBy fst
        let fits = makespan + reserves <= window
        id, makespan, fits,
        JObj
            [ "id", JStr id
              "description", JStr(arr "topologies" i.Policy |> List.tryFind (fun t -> str "id" t = id) |> Option.map (str "description") |> Option.defaultValue "")
              "makespanMinutes", jint makespan
              "reservedMinutes", jint reserves
              "totalMinutes", jint (makespan + reserves)
              "windowMinutes", jint window
              "fits", JBool fits
              "overrunMinutes", jint (max 0 (makespan + reserves - window))
              "lanes", JArr(lanes |> List.map (fun (l, ss) -> JObj [ "lane", JStr l; "busyMinutes", jint (ss |> List.sumBy (fun s -> s.Finish - s.Start)); "tasks", jstrs (ss |> List.sortBy (fun s -> s.Start, s.Task) |> List.map (fun s -> s.Task)) ]))
              "schedule", JArr(slots |> List.sortBy (fun s -> s.Start, s.Task) |> List.map (fun s -> JObj [ "task", JStr s.Task; "lane", JStr s.Lane; "startMinute", jint s.Start; "finishMinute", jint s.Finish ])) ]
    let topologies = arr "topologies" i.Policy |> List.map (str "id") |> List.map topology
    let anyFits = topologies |> List.exists (fun (_, _, f, _) -> f)
    let byId = i.Tasks |> List.map (fun t -> t.Id, t) |> Map.ofList
    let problems =
        missingEstimates
        @ [ if not anyFits then yield sprintf "execution plan: no topology fits %d minutes with %d reserved" window reserves ]
    JObj
        [ "schema", JStr(schemaPrefix + "execution-plan/1")
          "description", JStr "The timed plan for November 7, computed from curation/tasks.json (dependencies, estimateMinutes) and analysis/execution-policy.json. Every topology keeps every timed task and every requirement; a topology that does not fit is reported, never trimmed."
          "event", (field "event" i.Policy |> Option.defaultValue JNull)
          "clock", (field "clock" i.Policy |> Option.defaultValue JNull)
          "reserves", JArr(arr "reserves" i.Policy)
          "independentReviewCheckpoints", JArr(arr "independentReviewCheckpoints" i.Policy)
          "ciRules", JArr(arr "ciRules" i.Policy)
          "principles",
          jstrs
              [ "Start with verified dependencies: preflight tasks T00-T09 and the release-set lock finish before T0."
                "Foundational contracts first: invariant inventory (T12), kernel (T13) and interface freeze (T14) precede dependent features."
                "Real-provider tests early: the live GitHub probe (T18) runs in the first hour, before the save process is built on assumptions."
                "Commit frequently and integrate continuously: integration checkpoints T17 and T21; merge every green pull request immediately."
                "Independent verification throughout: review checkpoints after T14, T21 and T31, not only at the end."
                "Preserve the final verification and delivery window (RES-FINAL); nothing after the deadline alters the timed result." ]
          "criticalPath", JObj [ "tasks", jstrs path; "minutes", jint pathMinutes; "dependencies", jint (max 0 (path.Length - 1)); "basis", JStr "duration-weighted longest chain of timed tasks with unlimited lanes (a lower bound on the makespan)" ]
          "tasks",
          JArr(
              timed
              |> List.sortBy (fun t -> t.Id)
              |> List.map (fun t ->
                  JObj
                      [ "id", JStr t.Id
                        "title", JStr t.Title
                        "owner", JStr t.Owner
                        "estimateMinutes", jopt jint (TimedSchedule.estimate t)
                        "estimateBasis", JStr(strOpt "estimateBasis" t.Raw |> Option.defaultValue "")
                        "dependsOn", jstrs (t.DependsOn |> List.map (fun d -> d.Task) |> List.filter (fun d -> byId.ContainsKey d && byId.[d].Phase = "timed"))
                        "conditionalOn", jopt JStr t.ConditionalOn ]))
          "topologies", JArr(topologies |> List.map (fun (_, _, _, j) -> j))
          "fit",
          JObj
              [ "anyTopologyFits", JBool anyFits
                "fitting", jstrs (topologies |> List.filter (fun (_, _, f, _) -> f) |> List.map (fun (id, _, _, _) -> id))
                "notFitting", jstrs (topologies |> List.filter (fun (_, _, f, _) -> not f) |> List.map (fun (id, _, _, _) -> id))
                "conflict",
                JStr(
                    match topologies |> List.filter (fun (_, _, f, _) -> not f) with
                    | [] -> "none"
                    | xs -> sprintf "%s exceed the %d-minute window; no P0 requirement is removed to make them fit (ODR-V4-07)" (xs |> List.map (fun (id, m, _, _) -> sprintf "%s (%d + %d reserved)" id m reserves) |> String.concat ", ") window) ] ],
        problems

// ---- acceptance ---------------------------------------------------------------------------------------

let private consumer = "requirements-compiler verify-plan in the acceptance job, run by a verifier independent of the implementing agent, against the exact final SHA (AR-008, AR-019)"

let private passCondition (categories: string list) (policy: string) =
    sprintf "Every listed test has a PASS result in the evidence bundle produced from the exact final SHA, the bundle covers every required category (%s) within freshness policy %s, and verify-plan reports the obligation verified." (String.concat ", " categories) policy

let private failureConditions =
    jstrs
        [ "any listed test is FAIL, NOT_RUN, skipped, quarantined or missing"
          "evidence was produced from another SHA, an earlier rehearsal, or a fake where live evidence is required"
          "a required evidence category is not covered, or its freshness policy is violated"
          "the obligation's binding, amendment or exclusion differs from the frozen plan without an operator approval record" ]

let acceptanceObligations (i: Inputs) : Json =
    let planOblig =
        arr "requirements" i.Plan
        |> List.collect (fun e ->
            arr "obligations" e
            |> List.map (fun o ->
                let c = field "acceptanceContract" o |> Option.defaultValue (JObj [])
                let categories = strs "requiredEvidenceCategories" c
                let tests = field "testIdentity" c |> Option.map (arr "tests") |> Option.defaultValue []
                let policy = field "freshness" c |> Option.map (str "policy") |> Option.defaultValue ""
                JObj
                    [ "id", JStr("AO-" + str "sourceObligation" o)
                      "origin", JStr "verification-plan"
                      "requirement", JStr(str "requirement" e)
                      "obligation", JStr(str "sourceObligation" o)
                      "planObligation", JStr(str "id" o)
                      "text", JStr(str "text" o)
                      "requiredTests", JArr tests
                      "requiredEvidenceCategories", jstrs categories
                      "evidenceProducers", JArr(arr "evidenceProducers" c)
                      "evidenceConsumer", JStr consumer
                      "verificationMethod", (field "verificationMethod" c |> Option.defaultValue JNull)
                      "freshnessPolicy", JStr policy
                      "passCondition", JStr(passCondition categories policy)
                      "failureConditions", failureConditions
                      "normative", JBool true ]))
    let extra (origin: string) (id: string) (sources: string list) (proposals: string list) (t: Json) =
        JObj
            [ "id", JStr id
              "origin", JStr origin
              "requirements", jstrs sources
              "proposals", jstrs proposals
              "requiredTests", JArr [ JObj [ "id", JStr(str "id" t); "kind", JStr(str "kind" t); "categories", jstrs [ str "category" t ] ] ]
              "checks", JStr(strOpt "checks" t |> Option.defaultValue "")
              "evidenceConsumer", JStr consumer
              "passCondition", JStr(sprintf "The test has a PASS result in the evidence bundle for the exact final SHA (category %s)." (str "category" t))
              "failureConditions", failureConditions
              "normative", JBool proposals.IsEmpty
              "status", JStr(if proposals.IsEmpty then "binding-strengthened (enforces existing obligations of the cited requirements)" else "proposed: normative only after the owner approves " + String.concat ", " proposals) ]
    let invariantOblig =
        arr "invariants" i.Invariants
        |> List.collect (fun inv -> arr "tests" inv |> List.mapi (fun n t -> extra "invariant" (sprintf "AO-%s-%d" (str "id" inv) (n + 1)) (strs "sources" inv) (strs "proposals" inv) t))
    let lessonOblig =
        arr "lessons" i.Lessons
        |> List.collect (fun l -> arr "tests" l |> List.mapi (fun n t -> extra "r7-lesson" (sprintf "AO-%s-%d" (str "id" l) (n + 1)) [ str "obligation" t ] (strs "proposals" l) t))
    let all = planOblig @ invariantOblig @ lessonOblig
    JObj
        [ "schema", JStr(schemaPrefix + "acceptance-obligations/1")
          "description", JStr "Every P0 obligation of the verification plan as an acceptance obligation (required tests, producer, consumer, method, pass and failure conditions), plus the invariant and Rehearsal 7 lesson tests that strengthen the binding of existing obligations. The implementing agent cannot change this file: it is regenerated from the frozen plan and analysis, and its digest is in freeze-manifest.json."
          "plan", JObj [ "planId", JStr(field "planIdentity" i.Plan |> Option.map (str "planId") |> Option.defaultValue ""); "digest", JStr(field "planIdentity" i.Plan |> Option.map (str "digest") |> Option.defaultValue "") ]
          "summary",
          JObj
              [ "total", jint all.Length
                "fromPlan", jint planOblig.Length
                "fromInvariants", jint invariantOblig.Length
                "fromLessons", jint lessonOblig.Length
                "normative", jint (all |> List.filter (boolOr "normative" false) |> List.length)
                "proposed", jint (all |> List.filter (boolOr "normative" false >> not) |> List.length)
                "p0RequirementsCovered", jint (planOblig |> List.map (str "requirement") |> List.distinct |> List.length) ]
          "obligations", JArr all ]

let acceptanceGate (i: Inputs) : Json =
    let g id condition tool =
        JObj [ "id", JStr id; "acceptanceFailsWhen", JStr condition; "checkedBy", JStr tool ]
    JObj
        [ "schema", JStr(schemaPrefix + "acceptance-gate/1")
          "description", JStr "Conditions under which the event build's acceptance fails, whatever any agent reports. Each is checked by an existing tool run by the acceptance job at the exact final SHA; none is checked by the implementing agent's own assertion."
          "finalSha", JStr "The SHA of main at the deadline instant (execution-plan clock). Every condition is evaluated against that SHA and the deployment of that SHA."
          "conditions",
          JArr
              [ g "AG-01" "any P0 obligation of the frozen plan is unverified" "requirements-compiler verify-plan (verdict and missing coverage)"
                g "AG-02" "a critical test (any test bound to a P0 obligation, any PAT test, any invariant test of a normative invariant) fails" "verify-plan over the evidence bundle; acceptance ledger"
                g "AG-03" "required evidence is missing, or is for another SHA, or from an earlier rehearsal" "verify-plan --trusted-source-sha; Dokimos completion verify"
                g "AG-04" "the deployed artifact's SHA differs from the verified SHA, or the deployed acceptance job for that SHA did not conclude successfully" "deploy smoke (build.json equals the SHA) and the deployed-acceptance job conclusion (DCC-008)"
                g "AG-05" "any bound test is NOT_RUN, skipped or quarantined (including browser suites that stopped after a failure)" "acceptance ledger and verify-plan (PERSIST-055)"
                g "AG-06" "a required security check fails: credential scan, PAT-13 artifact scan, CSP and credential-destination checks" "credential scan with the fixture manifest (DCC-015); PAT-13; INV-21 tests"
                g "AG-07" "provenance is violated: a candidate file matches a rehearsal file (PAT-13 comparison by the independent verifier) or commits lack work-item trailers" "PAT-13 provenance comparison; Praxis work records"
                g "AG-08" "a required pull request remains unmerged at the deadline, or main differs from the verified SHA" "git: main at the deadline instant equals the verified SHA"
                g "AG-09" "an acceptance obligation, binding or amendment differs from the frozen package without an operator approval record" "verify-plan (UNAUTHORIZED-AMENDMENT); requirements-compiler v4 --check against freeze-manifest.json"
                g "AG-10" "the independent review of the final SHA is missing, or authored by the implementing agent, or reports an unresolved critical finding" "praxis:independent-review record; verify-plan review attestations"
                g "AG-11" "an in-scope critical defect class is open or lacks SHA-bound closure evidence" "requirements-compiler verify-defect-closures and assertion-ratchet (base = kickoff) before the candidate freeze (DCC-014)"
                g "AG-12" "conditor ci-policy check fails on the final SHA, or a CI job running bound tests lacks CAUSA_PLAN" "conditor ci-policy check (DCC-013); CI configuration (DCC-015)" ] ]

// ---- analysis validation -----------------------------------------------------------------------------

let definitionsProblems (i: Inputs) =
    arr "terms" i.Definitions
    |> List.collect (fun t ->
        let where = sprintf "operational-definitions %s" (str "term" t)
        [ if (str "definition" t).Trim() = "" then yield where + ": empty definition"
          if (str "decidedBy" t).Trim() = "" then yield where + ": no deciding evidence"
          if (strs "sources" t).IsEmpty then yield where + ": no source requirement" ]
        @ unknownIds i where (strs "sources" t)
        @ unknownProposals i where (strs "proposals" t))

let stateMachineProblems (i: Inputs) =
    let invariantIds = arr "invariants" i.Invariants |> List.map (str "id") |> Set.ofList
    arr "machines" i.StateMachines
    |> List.collect (fun m ->
        let mid = str "id" m
        let states = arr "states" m |> List.map (str "id")
        let stateSet = Set.ofList states
        let events = arr "events" m |> List.map (str "id") |> Set.ofList
        let ts = arr "transitions" m
        let where (t: Json) = sprintf "%s %s" mid (str "id" t)
        let reachable =
            let rec go (seen: Set<string>) frontier =
                match frontier with
                | [] -> seen
                | s :: rest ->
                    let next = ts |> List.filter (fun t -> str "from" t = s) |> List.map (str "to") |> List.filter (seen.Contains >> not) |> List.distinct
                    go (Set.union seen (Set.ofList next)) (rest @ next)
            go (Set.singleton (str "initial" m)) [ str "initial" m ]
        let terminal = strs "terminal" m |> Set.ofList
        let duplicates =
            ts |> List.countBy (fun t -> str "from" t, str "event" t, str "guard" t) |> List.filter (fun (_, n) -> n > 1)
            |> List.map (fun ((f, e, g), _) -> sprintf "%s: (%s, %s) has two transitions with guard '%s'" mid f e g)
        let savedEntry =
            if mid = "SM-SAVE" then
                let allowed = strs "entersSavedOnlyBy" m |> Set.ofList
                ts |> List.filter (fun t -> str "to" t = "Saved" && str "from" t <> "Saved" && not (allowed.Contains(str "event" t)))
                |> List.map (fun t -> sprintf "%s: Saved is entered by %s, which is not a confirmation (PERSIST-007, PERSIST-023)" (where t) (str "event" t))
            else []
        [ if not (stateSet.Contains(str "initial" m)) then yield sprintf "%s: initial state %s is not declared" mid (str "initial" m)
          for t in ts do
              if not (stateSet.Contains(str "from" t)) then yield sprintf "%s: from-state %s is not declared" (where t) (str "from" t)
              if not (stateSet.Contains(str "to" t)) then yield sprintf "%s: to-state %s is not declared" (where t) (str "to" t)
              if not (events.Contains(str "event" t)) then yield sprintf "%s: event %s is not declared" (where t) (str "event" t)
              if (strs "sources" t).IsEmpty then yield sprintf "%s: no source requirement" (where t)
          for s in states do
              if not (reachable.Contains s) then yield sprintf "%s: state %s is unreachable from %s" mid s (str "initial" m)
              if not (terminal.Contains s) && not (ts |> List.exists (fun t -> str "from" t = s && str "to" t <> s)) && s <> str "initial" m && not (ts |> List.exists (fun t -> str "from" t = s)) then
                  yield sprintf "%s: state %s has no outgoing transition and is not terminal" mid s
          for st in arr "states" m do
              for inv in strs "invariants" st do
                  if not (invariantIds.Contains inv) then yield sprintf "%s state %s: invariant %s is not in invariants.json" mid (str "id" st) inv ]
        @ duplicates
        @ savedEntry
        @ (ts |> List.collect (fun t -> unknownIds i (where t) (strs "sources" t) @ unknownProposals i (where t) (strs "proposals" t)))
        @ (arr "states" m |> List.collect (fun st -> unknownIds i (sprintf "%s state %s" mid (str "id" st)) (strs "sources" st) @ unknownProposals i (sprintf "%s state %s" mid (str "id" st)) (strs "proposals" st))))

let invariantProblems (i: Inputs) =
    let machines = arr "machines" i.StateMachines |> List.map (str "id") |> Set.ofList
    let categories = field "evidenceCategories" i.Plan |> Option.map (arr "categories") |> Option.defaultValue [] |> List.map (str "id") |> Set.ofList
    let invs = arr "invariants" i.Invariants
    let dupIds = invs |> List.countBy (str "id") |> List.filter (fun (_, n) -> n > 1) |> List.map (fst >> sprintf "invariants: %s is defined twice")
    let required = [ "persistence-correctness"; "data-preservation"; "concurrency"; "domain-integrity"; "verification-integrity"; "security" ]
    let groups = invs |> List.map (str "group") |> Set.ofList
    dupIds
    @ (required |> List.filter (groups.Contains >> not) |> List.map (sprintf "invariants: no invariant in group %s"))
    @ (invs
       |> List.collect (fun inv ->
           let where = "invariant " + str "id" inv
           [ if (strs "sources" inv).IsEmpty then yield where + ": no source requirement"
             if (arr "tests" inv).IsEmpty then yield where + ": no test"
             for m in strs "machines" inv do
                 if not (machines.Contains m) then yield sprintf "%s: machine %s is not in state-machines.json" where m
             for t in arr "tests" inv do
                 if not (categories.Contains(str "category" t)) then yield sprintf "%s: test %s has category %s, which is not an evidence category" where (str "id" t) (str "category" t)
                 if not ([ "property"; "model"; "negative"; "mutation"; "live"; "deterministic"; "review" ] |> List.contains (str "kind" t)) then yield sprintf "%s: test %s has unknown kind %s" where (str "id" t) (str "kind" t) ]
           @ unknownIds i where (strs "sources" inv)
           @ unknownProposals i where (strs "proposals" inv)))

/// The quote check of a Rehearsal 7 lesson: whitespace-insensitive substring of the named evidence file.
let private quoted (text: string) (quote: string) =
    let norm (s: string) = Regex.Replace(s, @"\s+", " ")
    (norm text).Contains(norm quote, StringComparison.Ordinal)

let lessonsProblems (i: Inputs) =
    let invariantIds = arr "invariants" i.Invariants |> List.map (str "id") |> Set.ofList
    let allowedFiles = field "evidence" i.Lessons |> Option.map (strs "files") |> Option.defaultValue [] |> Set.ofList
    let required =
        [ "False save confirmation"; "Lost responses after successful commits"; "Concurrent GitHub writes"; "Stale refreshes"; "Conflict-resolution races"
          "Reauthentication without data loss"; "Unresolvable conflicts"; "Offline recovery"; "GitHub rate-limit classification"; "GitHub moved-head responses"
          "Contract and snapshot integrity"; "Forged persistence responses"; "Missing regression coverage"; "Incomplete deployed-browser acceptance"
          "Deadline and merge orchestration failures" ]
    let classes = arr "lessons" i.Lessons |> List.map (str "class") |> Set.ofList
    let texts = obligationText i
    (required |> List.filter (classes.Contains >> not) |> List.map (sprintf "r7 lessons: required defect class '%s' is not mapped"))
    @ (arr "lessons" i.Lessons
       |> List.collect (fun l ->
           let where = "r7 lesson " + str "id" l
           let disposition = str "disposition" l
           [ if not ([ "covered"; "gap-proposed"; "process" ] |> List.contains disposition) then yield sprintf "%s: unknown disposition %s" where disposition
             if disposition = "gap-proposed" && (strs "proposals" l).IsEmpty then yield where + ": a gap without an owner decision request"
             if disposition = "covered" && (strs "invariants" l).IsEmpty && (arr "tests" l).IsEmpty then yield where + ": covered but no strengthened binding (invariant or test)"
             if (arr "evidence" l).IsEmpty then yield where + ": no evidence quote"
             for inv in strs "invariants" l do
                 if not (invariantIds.Contains inv) then yield sprintf "%s: invariant %s is not in invariants.json" where inv
             for t in arr "tests" l do
                 if not (texts.ContainsKey(str "obligation" t)) then yield sprintf "%s: test %s binds to %s, which is not a baseline obligation" where (str "id" t) (str "obligation" t)
             for e in arr "evidence" l do
                 if not (allowedFiles.Contains(str "file" e)) then yield sprintf "%s: evidence file %s is not one of the permitted evidence files" where (str "file" e)
                 match i.R7Evidence with
                 | Some read ->
                     match read (str "file" e) with
                     | Some text when quoted text (str "quote" e) -> ()
                     | Some _ -> yield sprintf "%s: quote not found in %s: %s" where (str "file" e) (str "quote" e)
                     | None -> yield sprintf "%s: %s is unreadable" where (str "file" e)
                 | None -> () ]
           @ unknownIds i where (strs "requirements" l)
           @ unknownProposals i where (strs "proposals" l)))

let lessonsMapping (i: Inputs) : Json =
    let lessons = arr "lessons" i.Lessons
    let count d = lessons |> List.filter (fun l -> str "disposition" l = d) |> List.length
    JObj
        [ "schema", JStr(schemaPrefix + "r7-lessons-mapping/1")
          "description", JStr(str "description" i.Lessons)
          "evidence", (field "evidence" i.Lessons |> Option.defaultValue JNull)
          "summary", JObj [ "classes", jint lessons.Length; "covered", jint (count "covered"); "gapProposed", jint (count "gap-proposed"); "process", jint (count "process") ]
          "lessons", JArr lessons ]

let interfaceProblems (i: Inputs) (lock: Json) =
    let contracts = i.Contracts |> List.map (str "id") |> Set.ofList
    let systems = arr "systems" lock |> List.map (str "id") |> Set.ofList
    let lockAvailable = not systems.IsEmpty
    arr "interfaces" i.Interfaces
    |> List.collect (fun f ->
        let where = "interface " + str "id" f
        [ for c in strs "contracts" f do
              if not (contracts.Contains c) then yield sprintf "%s: contract %s is not in curation/contracts.json" where c
          if lockAvailable then
              for s in strs "systems" f do
                  if not (systems.Contains s) then yield sprintf "%s: system %s is not in the dependency lock" where s
          if (strs "operations" f).IsEmpty then yield where + ": no operations"
          if (str "verification" f).Trim() = "" then yield where + ": no verification method" ]
        @ unknownIds i where (strs "requirements" f))

let proposalProblems (i: Inputs) =
    let texts = obligationText i
    arr "requests" i.Proposals
    |> List.collect (fun p ->
        let where = "owner decision request " + str "id" p
        let records = match field "approvalRecord" p with Some(JArr xs) -> xs | _ -> []
        (records
         |> List.filter (fun r -> str "action" r = "amend")
         |> List.collect (fun r ->
             let oid = str "obligation_id" r
             match texts.TryFind oid with
             | None -> [ sprintf "%s: %s is not a baseline obligation" where oid ]
             | Some text ->
                 // An amendment may only add: the criterion must carry the baseline text (without its full stop) verbatim.
                 let core = text.TrimEnd('.')
                 [ if not ((str "criterion" r).Contains(core, StringComparison.Ordinal)) then yield sprintf "%s: the criterion for %s does not carry the baseline text verbatim, so it could weaken it" where oid
                   if (str "rationale" r).Trim() = "" then yield sprintf "%s: the record for %s has no rationale" where oid ]))
        @ unknownIds i where (strs "affects" p))

let riskProblems (i: Inputs) =
    let ids = Set.unionMany [ (known i) |> Map.keys |> Set.ofSeq; proposalIds i; i.Tasks |> List.map (fun t -> t.Id) |> Set.ofList; arr "invariants" i.Invariants |> List.map (str "id") |> Set.ofList; arr "lessons" i.Lessons |> List.map (str "id") |> Set.ofList ]
    arr "risks" i.Risks
    |> List.collect (fun r ->
        [ for k in [ "likelihood"; "impact" ] do
              match intOpt k r with
              | Some v when v >= 1L && v <= 3L -> ()
              | _ -> yield sprintf "risk %s: %s must be 1..3" (str "id" r) k
          for l in strs "links" r do
              if not (ids.Contains l) then yield sprintf "risk %s: link %s names nothing in the package" (str "id" r) l ])

let riskRegister (i: Inputs) : Json =
    let exposure r = (intOpt "likelihood" r |> Option.defaultValue 0L) * (intOpt "impact" r |> Option.defaultValue 0L)
    JObj
        [ "schema", JStr(schemaPrefix + "risk-register/1")
          "description", JStr(str "description" i.Risks)
          "risks",
          JArr(
              arr "risks" i.Risks
              |> List.sortBy (fun r -> -(exposure r), str "id" r)
              |> List.map (fun r -> match r with
                                    | JObj ps -> JObj(ps @ [ "exposure", JInt(exposure r) ])
                                    | x -> x)) ]

let private withHeader (schema: string) (j: Json) =
    match j with
    | JObj ps -> JObj(("schema", JStr(schemaPrefix + schema)) :: (ps |> List.filter (fun (k, _) -> k <> "schema")))
    | x -> x

let private stateMachinesOut (i: Inputs) =
    match withHeader "state-machines/1" i.StateMachines with
    | JObj ps ->
        let summary =
            arr "machines" i.StateMachines
            |> List.map (fun m ->
                let ts = arr "transitions" m
                JObj
                    [ "machine", JStr(str "id" m)
                      "states", jint (arr "states" m).Length
                      "events", jint (arr "events" m).Length
                      "transitions", jint ts.Length
                      "proposedTransitions", jstrs (ts |> List.filter (fun t -> not (strs "proposals" t).IsEmpty) |> List.map (str "id")) ])
        JObj(ps @ [ "summary", JArr summary ])
    | x -> x

let private applicationExtensions = [ ".fs"; ".fsx"; ".fsi"; ".cs"; ".js"; ".mjs"; ".ts"; ".tsx"; ".jsx"; ".html"; ".css"; ".py"; ".sh" ]

/// Order-independent digest of a source tree: SHA-256 over the sorted lines "<path> <sha256 of bytes>".
let treeSha256 (files: (string * byte[]) list) =
    files
    |> List.map (fun (p, b) -> sprintf "%s %s" (p.Replace('\\', '/')) (Git.sha256Hex b))
    |> List.sort
    |> String.concat "\n"
    |> Text.Encoding.UTF8.GetBytes
    |> Git.sha256Hex

// ---- the package ---------------------------------------------------------------------------------------

type Built = { Files: (string * Json) list; Checks: Check list; Lock: Json }

/// Every generated v4 file except freeze-manifest.json, plus the checks that bind them.
let build (i: Inputs) : Built =
    let lock, lockProblems = dependencyLock i
    let plan, planProblems = executionPlan i
    let readiness = PlanReadiness.assess i.Plan (VerificationPlan.digestOf i.Plan) None
    let legacyPass = str "compilationVerdict" i.LegacyReport = "PASS"
    let legacyReady = field "rehearsalReadiness" i.LegacyReport |> Option.map (str "verdict") = Some "READY"
    let p0 = p0Ids i.Baseline |> Set.ofList
    let planP0 = arr "requirements" i.Plan |> List.map (str "requirement") |> Set.ofList
    let acceptance = acceptanceObligations i
    let aoP0 = arr "obligations" acceptance |> List.choose (fun o -> strOpt "requirement" o) |> Set.ofList
    let timedCovered =
        let timed = i.Tasks |> List.filter (fun t -> t.Phase = "timed") |> List.map (fun t -> t.Id) |> Set.ofList
        let scheduled = field "topologies" plan |> Option.map (fun _ -> ()) |> Option.isSome
        timed, scheduled
    let checks =
        [ check "W01" "The authoritative baseline is reconciled: every requirement of the R6, R7 and v4 baselines has a disposition, every change has a recorded authority, and no requirement or P0 priority disappears" true (reconciliationProblems i)
          check "W02" "The legacy compilation (V01-V18) passes and its readiness is READY: no unresolved contradiction, valid DAG, complete coverage" true
              [ if not legacyPass then yield "requirements-validation-report.json: compilation verdict is not PASS"
                if not legacyReady then yield "requirements-validation-report.json: rehearsal readiness is not READY" ]
          check "W03" "Every P0 requirement of the baseline is in the verification plan and has acceptance obligations; plan-readiness is READY with no permit" true
              [ for r in Set.difference p0 planP0 do yield sprintf "P0 %s is not in the verification plan" r
                for r in Set.difference p0 aoP0 do yield sprintf "P0 %s has no acceptance obligation" r
                if str "verdict" readiness <> "READY" then yield sprintf "plan-readiness is %s" (str "verdict" readiness)
                if (field "summary" readiness |> Option.bind (intOpt "permitted")) <> Some 0L then yield "plan-readiness relies on a permitted gap" ]
          check "W04" "Operational definitions cite real requirements and name their deciding evidence" true (definitionsProblems i)
          check "W05" "State machines are well formed: declared states and events, sourced transitions, reachable states, deterministic guards, Saved entered only by a confirmation" true (stateMachineProblems i)
          check "W06" "Invariants cover every required group, cite real requirements and name tests of known kinds and evidence categories" true (invariantProblems i)
          check "W07" "Every required Rehearsal 7 defect class is mapped; gaps carry an owner decision request; quotes are verbatim in the permitted evidence files" true (lessonsProblems i)
          check "W08" "The dependency lock is the approved release set: every component is a stable release of echelon-current at the recorded version and manifest digest; no forbidden framework" true lockProblems
          check "W09" "Interfaces name existing contracts and locked systems, with operations and a verification method" true (interfaceProblems i lock)
          check "W10" "The execution plan schedules every timed task with an estimate and reports whether each topology fits the window" true
              (planProblems @ [ if not (snd timedCovered) then yield "execution plan: no topology was scheduled" ])
          check "W11" "Owner decision requests are well formed: every proposed amendment only adds to its obligation's baseline text" true (proposalProblems i)
          check "W12" "The risk register's ratings are in range and every link names something in the package" true (riskProblems i)
          check "W13" "No approval record was written by an agent: the pinned approvals file holds only operator records (RDP-003)" true
              [ for a in arr "approvals" i.Approvals do
                    if (strOpt "approved_by" a |> Option.orElse (strOpt "approver" a)) |> Option.exists (fun s -> s.Contains("agent", StringComparison.OrdinalIgnoreCase) || s.Contains("claude", StringComparison.OrdinalIgnoreCase)) then
                        yield "approvals: a record names an agent as its approver" ]
          check "W14" "The package contains no application implementation: no source file of an application language under requirements-compiled/v4" true
              (i.PackageFiles |> List.filter (fun f -> applicationExtensions |> List.exists (fun e -> f.EndsWith(e, StringComparison.OrdinalIgnoreCase))) |> List.map (sprintf "application source file in the package: %s"))
          check "W15" "The verification plan and the reconciliation are compiled from the committed v4 baseline" true
              [ let planSha = field "source" i.Plan |> Option.map (str "baselineSha256") |> Option.defaultValue ""
                if planSha <> i.BaselineSha256 then yield sprintf "verification-plan.json was compiled from baseline sha256 %s, the committed v4 baseline is %s" planSha i.BaselineSha256 ] ]
    let files =
        [ "baseline-reconciliation.json", reconciliation i
          "dependency-lock.json", lock
          "execution-plan.json", plan
          "invariants.json", withHeader "invariants/1" i.Invariants
          "state-machines.json", stateMachinesOut i
          "interfaces.json", withHeader "interfaces/1" i.Interfaces
          "operational-definitions.json", withHeader "operational-definitions/1" i.Definitions
          "r7-lessons-mapping.json", lessonsMapping i
          "risk-register.json", riskRegister i
          "acceptance/acceptance-obligations.json", acceptance
          "acceptance/acceptance-gate.json", acceptanceGate i ]
    { Files = files; Checks = checks; Lock = lock }

let validationReport (i: Inputs) (checks: Check list) : Json =
    JObj
        [ "schema", JStr(schemaPrefix + "package-validation-report/1")
          "source", JObj [ "repository", JStr i.SourceRepository; "commit", JStr i.SourceCommit; "baselineSha256", JStr i.BaselineSha256 ]
          "verdict", JStr(if checks |> List.forall (fun c -> not c.Critical || c.Problems.IsEmpty) then "PASS" else "FAIL")
          "rule", JStr "FAIL when any critical check reports a problem. The legacy checks V01-V18 are in requirements-validation-report.json and are summarised by W02."
          "checks", JArr(checks |> List.map checkJson) ]

// ---- freeze ----------------------------------------------------------------------------------------------

type Gate = { Id: string; Condition: string; Pass: bool; Detail: string }

/// Operator approval of the package itself (ODR-V4-01): a record in the pinned approvals file naming the plan digest.
let private packageApproved (i: Inputs) (planDigest: string) =
    arr "approvals" i.Approvals
    |> List.exists (fun a -> sOr "decision_id" a = "ODR-V4-01" && sOr "action" a = "approve-package" && sOr "plan_digest" a = planDigest)

/// Owner decision requests still without an operator record (amendments) or decision.
let pendingDecisions (i: Inputs) (planDigest: string) =
    let recorded = arr "approvals" i.Approvals |> List.map (fun a -> sOr "obligation_id" a, sOr "action" a) |> Set.ofList
    let decided = arr "approvals" i.Approvals |> List.map (sOr "decision_id") |> Set.ofList
    arr "requests" i.Proposals
    |> List.filter (fun p ->
        match str "kind" p with
        | "package-approval" -> not (packageApproved i planDigest)
        | "obligation-amendment" ->
            let records = match field "approvalRecord" p with Some(JArr xs) -> xs | _ -> []
            not (decided.Contains(str "id" p)) && not (records |> List.forall (fun r -> recorded.Contains(sOr "obligation_id" r, "amend")))
        | _ -> not (decided.Contains(str "id" p)))
    |> List.map (str "id")

let gates (i: Inputs) (b: Built) (planDigest: string) : Gate list =
    let byId id = b.Checks |> List.find (fun c -> c.Id = id)
    let ok id = (byId id).Problems.IsEmpty
    let pending = pendingDecisions i planDigest
    let testsPass = sOr "result" i.CompilerTests = "PASS" && sOr "compilerTreeSha256" i.CompilerTests = i.CompilerTreeSha256
    let reviewPass = sOr "verdict" i.Review = "PASS" && (arr "findings" i.Review |> List.forall (fun f -> not (sOr "severity" f = "critical" && sOr "status" f <> "resolved")))
    let planFits = b.Files |> List.tryFind (fst >> (=) "execution-plan.json") |> Option.bind (snd >> field "fit") |> Option.map (boolOr "anyTopologyFits" false) |> Option.defaultValue false
    let freezeCommit = strOpt "freezeCommit" i.FreezeInput |> Option.defaultValue ""
    [ { Id = "FG-01"; Condition = "The authoritative baseline is reconciled"; Pass = ok "W01"; Detail = sprintf "W01 %d problem(s)" (byId "W01").Problems.Length }
      { Id = "FG-02"; Condition = "No mandatory requirement has disappeared"; Pass = ok "W01" && ok "W03"; Detail = "W01 (no removal, no P0 downgrade) and W03 (every P0 in the plan)" }
      { Id = "FG-03"; Condition = "Every P0 requirement has acceptance coverage"; Pass = ok "W03"; Detail = "W03: plan-readiness READY, no permit, acceptance obligations for every P0" }
      { Id = "FG-04"; Condition = "No unresolved critical contradiction remains"; Pass = ok "W02"; Detail = "W02: legacy V03 and readiness" }
      { Id = "FG-05"; Condition = "All required owner approvals exist"; Pass = pending.IsEmpty; Detail = (if pending.IsEmpty then "none pending" else "pending: " + String.concat ", " pending) }
      { Id = "FG-06"; Condition = "The dependency lock is valid"; Pass = ok "W08"; Detail = sprintf "W08 %d problem(s)" (byId "W08").Problems.Length }
      { Id = "FG-07"; Condition = "The execution plan is complete"; Pass = ok "W10" && planFits; Detail = (if planFits then "complete; at least one topology fits (see execution-plan.json fit)" else "no topology fits the window") }
      { Id = "FG-08"; Condition = "Compiler tests pass"; Pass = testsPass; Detail = sprintf "record: %s, tree %s (current tree %s)" (sOr "result" i.CompilerTests) (sOr "compilerTreeSha256" i.CompilerTests) i.CompilerTreeSha256 }
      { Id = "FG-09"; Condition = "Independent review passes"; Pass = reviewPass; Detail = sprintf "review verdict %s (%s)" (sOr "verdict" i.Review) (sOr "reviewer" i.Review) }
      { Id = "FG-10"; Condition = "The freeze commit is recorded"; Pass = freezeCommit <> ""; Detail = (if freezeCommit = "" then "no freeze commit: recorded only when every other condition passes" else freezeCommit) } ]

let freezeStatus (b: Built) (gs: Gate list) (legacyPass: bool) =
    let pass id = gs |> List.exists (fun g -> g.Id = id && g.Pass)
    let validated = legacyPass && b.Checks |> List.forall (fun c -> not c.Critical || c.Problems.IsEmpty)
    let reviewed = pass "FG-09"
    let approved = pass "FG-05"
    let frozen = gs |> List.forall (fun g -> g.Pass)
    [ "compiled", legacyPass; "validated", validated; "independentlyReviewed", reviewed; "approved", approved; "frozen", frozen ],
    (if frozen then "FROZEN" else "CANDIDATE")

let freezeManifest (i: Inputs) (b: Built) (digests: (string * string) list) (legacyPass: bool) : Json =
    let planIdentity = field "planIdentity" i.Plan |> Option.defaultValue JNull
    let planDigest = str "digest" planIdentity
    let gs = gates i b planDigest
    let states, status = freezeStatus b gs legacyPass
    let digest name = digests |> List.tryFind (fst >> (=) name) |> Option.map snd |> Option.defaultValue ""
    let acceptance = b.Files |> List.find (fst >> (=) "acceptance/acceptance-obligations.json") |> snd
    let ao = field "summary" acceptance |> Option.defaultValue JNull
    let unresolved =
        (b.Checks |> List.filter (fun c -> not c.Problems.IsEmpty) |> List.map (fun c -> sprintf "%s: %d problem(s)" c.Id c.Problems.Length))
        @ (arr "findings" i.Review |> List.filter (fun f -> sOr "status" f <> "resolved") |> List.map (fun f -> sprintf "review %s (%s): %s" (sOr "id" f) (sOr "severity" f) (sOr "status" f)))
        @ (pendingDecisions i planDigest |> List.map (sprintf "owner decision %s pending"))
    JObj
        [ "schema", JStr(schemaPrefix + "freeze-manifest/1")
          "packageVersion", JStr "4.0.0"
          "status", JStr status
          "states", JObj(states |> List.map (fun (k, v) -> k, JBool v))
          "sourceCommit", JStr i.SourceCommit
          "baseline", JObj [ "id", JStr(str "id" i.Baseline); "path", JStr "requirements/causa-requirements-baseline-v4.json"; "sha256", JStr i.BaselineSha256 ]
          "counts",
          JObj
              [ "requirements", jint (reqs i.Baseline).Length
                "p0Requirements", jint (p0Ids i.Baseline).Length
                "obligations", jint (reqs i.Baseline |> List.sumBy (fun r -> (arr "obligations" r).Length))
                "p0Obligations", (field "scope" i.Plan |> Option.bind (intOpt "obligations") |> Option.map JInt |> Option.defaultValue JNull)
                "acceptanceObligations", (ao |> intOpt "total" |> Option.map JInt |> Option.defaultValue JNull)
                "normativeAcceptanceObligations", (ao |> intOpt "normative" |> Option.map JInt |> Option.defaultValue JNull)
                "invariants", jint (arr "invariants" i.Invariants).Length
                "stateMachines", jint (arr "machines" i.StateMachines).Length
                "r7Lessons", jint (arr "lessons" i.Lessons).Length
                "ownerDecisionRequests", jint (arr "requests" i.Proposals).Length ]
          "verificationPlan", JObj [ "planId", JStr(str "planId" planIdentity); "digest", JStr planDigest; "fileSha256", JStr(digest "verification-plan.json") ]
          "dependencyLockSha256", JStr(digest "dependency-lock.json")
          "executionManifestSha256", JStr(digest "timed-execution-manifest.json")
          "executionPlanSha256", JStr(digest "execution-plan.json")
          "acceptanceSha256", JObj [ "acceptance-obligations.json", JStr(digest "acceptance/acceptance-obligations.json"); "acceptance-gate.json", JStr(digest "acceptance/acceptance-gate.json") ]
          "files", JArr(digests |> List.sortBy fst |> List.map (fun (n, d) -> JObj [ "path", JStr n; "sha256", JStr d ]))
          "compiler", JObj [ "name", JStr "requirements-compiler"; "treeSha256", JStr i.CompilerTreeSha256; "sdk", JStr(sOr "sdk" i.FreezeInput) ]
          "compilerTests", i.CompilerTests
          "independentReview", JObj [ "verdict", JStr(sOr "verdict" i.Review); "reviewer", JStr(sOr "reviewer" i.Review); "rounds", (intOpt "rounds" i.Review |> Option.map JInt |> Option.defaultValue JNull); "record", JStr "independent-review.md" ]
          "freezeGate", JArr(gs |> List.map (fun g -> JObj [ "id", JStr g.Id; "condition", JStr g.Condition; "status", JStr(if g.Pass then "PASS" else "FAIL"); "detail", JStr g.Detail ]))
          "unresolvedFindings", jstrs unresolved
          "freezeTimestamp", JStr(sOr "compiledAt" i.FreezeInput)
          "freezeTimestampMeaning", JStr(if status = "FROZEN" then "the time the package was frozen" else "the time this candidate was compiled; the package is not frozen")
          "tag", JStr(if status = "FROZEN" then "indy-init-requirements-v4.0.0" else "none: the tag is created only for a FROZEN package") ]
