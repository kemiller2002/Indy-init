/// Phase 8: static validation. Checks read the generated artifacts back (not the compiler's
/// internal state where an artifact exists), so a serialization fault is caught too.
module Validate

open System.Text.RegularExpressions
open Json
open Model
open Artifacts

type Check =
    { Id: string
      Title: string
      Critical: bool
      Problems: string list }

let private check id title critical problems =
    { Id = id; Title = title; Critical = critical; Problems = problems }

let private idsOf key (j: Json) = arr key j |> List.map (str "id")

type Inputs =
    { Compiled: Compiled
      Baseline: Json
      Outputs: Map<string, Json>
      RepoChecks: (string * string * string) list
      SourceDocumentHashes: (string * string * string) list
      SourceTexts: Map<string, string>
      SourceFiles: string list
      CommittedBaseline: Json option }

let private inventoryComplete (i: Inputs) =
    let inv = i.Outputs.["requirements-inventory.json"]
    let invReqs = arr "requirements" inv
    let invById = invReqs |> List.map (fun r -> str "id" r, r) |> Map.ofList
    let baseReqs = arr "requirements" i.Baseline

    let missing =
        baseReqs
        |> List.collect (fun b ->
            match invById.TryFind(str "id" b) with
            | None -> [ sprintf "baseline requirement %s is not inventoried" (str "id" b) ]
            | Some r ->
                let invObl = arr "obligations" r |> List.map (fun o -> str "id" o, str "text" o) |> Map.ofList

                arr "obligations" b
                |> List.choose (fun o ->
                    match invObl.TryFind(str "id" o) with
                    | None -> Some(sprintf "obligation %s is not inventoried" (str "id" o))
                    | Some t when t <> str "text" o -> Some(sprintf "obligation %s text differs from the baseline" (str "id" o))
                    | Some _ -> None))

    let external =
        i.Compiled.Requirements
        |> List.filter (fun r -> r.Origin = "external" && not (invById.ContainsKey r.Id))
        |> List.map (fun r -> sprintf "external requirement %s is not inventoried" r.Id)

    // Every baseline obligation must occur verbatim in its document at the source commit (catches an edited baseline).
    let notVerbatim =
        baseReqs
        |> List.collect (fun b ->
            match i.SourceTexts.TryFind(str "document" b) with
            | None -> [ sprintf "%s: document %s is not readable at the source commit" (str "id" b) (str "document" b) ]
            | Some text ->
                arr "obligations" b
                |> List.filter (fun o -> not (Text.containsQuote text (str "text" o)))
                |> List.map (fun o -> sprintf "obligation %s is not verbatim in %s" (str "id" o) (str "document" b)))

    let drift =
        i.SourceDocumentHashes
        |> List.filter (fun (_, pinned, observed) -> pinned <> observed)
        |> List.map (fun (p, pinned, observed) -> sprintf "%s: baseline pins %s but the source commit has %s" p pinned observed)

    check "V01" "Every original requirement and obligation is represented verbatim; the baseline matches its source commit" true (missing @ external @ notVerbatim @ drift)

let private traceability (i: Inputs) =
    let inv = i.Outputs.["requirements-inventory.json"]
    let norm = i.Outputs.["normalized-requirements.json"]
    let smap = i.Outputs.["requirements-source-map.json"]
    let invIds = idsOf "requirements" inv |> Set.ofList
    let invObl = arr "requirements" inv |> List.collect (fun r -> idsOf "obligations" r)
    let normReqs = arr "requirements" norm
    let normObl = normReqs |> List.collect (strs "obligations")

    let problems =
        [ yield! normReqs |> List.filter (fun n -> not (invIds.Contains(str "source" n))) |> List.map (fun n -> sprintf "%s traces to unknown %s" (str "id" n) (str "source" n))
          if normReqs.Length <> invIds.Count then yield sprintf "%d normalized requirements for %d inventoried" normReqs.Length invIds.Count
          if List.sort normObl <> List.sort invObl then yield "normalized obligations are not exactly the inventoried obligations"
          if (normObl |> List.distinct).Length <> normObl.Length then yield "an obligation is assigned to more than one normalized requirement"
          let mapped = arr "entries" smap |> List.collect (strs "obligations")
          if List.sort mapped <> List.sort invObl then yield "source map does not list every obligation exactly once" ]

    check "V02" "Every normalized requirement and obligation traces to its source" true problems

let private contradictions (i: Inputs) =
    let norm = i.Outputs.["normalized-requirements.json"]
    let status = arr "requirements" norm |> List.map (fun n -> str "source" n, str "reviewStatus" n) |> Map.ofList
    let findings = i.Compiled.Curation.Findings
    let allowed = set [ "unresolved"; "resolved-by-precedence"; "resolved-by-owner"; "no-action-required" ]

    let fixedIds =
        i.Compiled.Curation.Decisions
        |> List.filter (fun d -> str "classification" d = "fixed-architectural")
        |> List.map (str "id")
        |> Set.ofList

    let problems =
        [ for f in findings do
              let fid = str "id" f
              let st = strOpt "status" f |> Option.defaultValue ""
              if not (allowed.Contains st) then yield sprintf "%s has status '%s'" fid st
              if str "type" f = "conflict" && st <> "unresolved" && (citationsOf "resolutionSources" f).IsEmpty then
                  yield sprintf "%s is marked %s without a resolution source" fid st
              if isOpenMaterial f then
                  for r in strs "requirements" f do
                      if status.TryFind r <> Some "flagged-for-review" then yield sprintf "%s names %s, which is not flagged for review" fid r
                  for d in strs "decisions" f do
                      if fixedIds.Contains d then yield sprintf "%s is unresolved but decision %s is classified fixed" fid d ]

    check "V03" "No unresolved contradiction is silently accepted" true problems

let private dependencies (i: Inputs) =
    let c = i.Compiled
    let reqIds = c.Requirements |> List.map (fun r -> r.Id) |> Set.ofList
    let taskIds = c.Curation.Tasks |> List.map (fun t -> t.Id) |> Set.ofList
    let decisionIds = c.Curation.Decisions |> List.map (str "id") |> Set.ofList
    let contractIds = c.Curation.Contracts |> List.map (str "id") |> Set.ofList
    let findingIds = c.Curation.Findings |> List.map (str "id") |> Set.ofList

    let unknown kind (known: Set<string>) owner ids =
        ids |> List.filter (fun x -> not (known.Contains x)) |> List.map (sprintf "%s names unknown %s %s" owner kind)

    let problems =
        [ for t in c.Curation.Tasks do
              yield! unknown "task" taskIds t.Id (t.DependsOn @ t.Preferences |> List.map (fun d -> d.Task))
              yield! unknown "decision" decisionIds t.Id t.Decisions
              yield! unknown "contract" contractIds t.Id t.Contracts
              let selectorHits = t.Covers |> List.filter (fun s -> not (c.Requirements |> List.exists (matches s)))
              yield! selectorHits |> List.map (sprintf "%s covers selector '%s', which matches no requirement" t.Id)
          for f in c.Curation.Findings do
              yield! unknown "requirement" reqIds (str "id" f) (strs "requirements" f)
              yield! unknown "decision" decisionIds (str "id" f) (strs "decisions" f)
          for d in c.Curation.Decisions do
              yield! unknown "requirement" reqIds (str "id" d) (strs "requirements" d)
              yield! unknown "finding" findingIds (str "id" d) (strs "findings" d)
          for k in c.Curation.Contracts do
              yield! unknown "requirement" reqIds (str "id" k) (strs "requirements" k) ]

    check "V04" "All referenced dependencies, tasks, decisions and contracts exist" true problems

let private dag (i: Inputs) =
    let g = i.Outputs.["dependency-graph.json"]

    let problems =
        [ if not (boolOr "acyclic" false g) then yield sprintf "cycle among %s" (String.concat ", " (strs "cycle" g))
          for t in i.Compiled.Curation.Tasks do
              if t.DependsOn |> List.exists (fun d -> d.Task = t.Id) then yield sprintf "%s depends on itself" t.Id
              let deps = t.DependsOn |> List.map (fun d -> d.Task)
              if (List.distinct deps).Length <> deps.Length then yield sprintf "%s repeats a dependency" t.Id
              for p in t.Preferences do
                  if List.contains p.Task deps then yield sprintf "%s lists %s as both dependency and preference" t.Id p.Task
              if t.DependsOn |> List.exists (fun d -> d.Reason.Trim() = "") then yield sprintf "%s has a dependency without a reason" t.Id ]

    check "V05" "The dependency graph is a valid DAG with justified edges" true problems

let private verificationCoverage (i: Inputs) =
    let vm = i.Outputs.["verification-manifest.json"]
    let entries = arr "entries" vm |> List.map (fun e -> str "requirement" e, e) |> Map.ofList
    let rules = arr "rules" vm |> List.map (fun r -> str "id" r, r) |> Map.ofList
    let defaults = field "defaults" vm |> Option.defaultValue (JObj [])

    let problems =
        i.Compiled.Requirements
        |> List.collect (fun r ->
            match entries.TryFind r.Id with
            | None -> [ sprintf "%s has no verification entry" r.Id ]
            | Some e ->
                let applied = strs "rules" e |> List.choose rules.TryFind
                let union key = applied |> List.collect (strs key)

                [ if applied.IsEmpty then yield sprintf "%s matches no verification rule" r.Id
                  if applied.Length <> (strs "rules" e).Length then yield sprintf "%s names an unknown rule" r.Id
                  if (union "requiredEvidence").IsEmpty then yield sprintf "%s requires no evidence" r.Id
                  if (strs "owners" e).IsEmpty then yield sprintf "%s has no verification owner" r.Id
                  if (union "invalidatedBy" @ strs "invalidatedBy" defaults).IsEmpty then yield sprintf "%s has no invalidation conditions" r.Id
                  if (union "freshness" @ strs "freshness" defaults).IsEmpty then yield sprintf "%s has no freshness requirement" r.Id ])

    check "V06" "Every requirement has verification coverage" true problems

let private contractsIdentifiable (i: Inputs) =
    let vocabulary = set [ "missing"; "unverified"; "identity-verified"; "blocked" ]

    let problems =
        [ for k in i.Compiled.Curation.Contracts do
              let id = str "id" k
              let st = str "status" k
              if not (vocabulary.Contains st) then yield sprintf "%s has status '%s' (no contract may be marked verified by this package)" id st
              for f in [ "provider"; "consumer"; "contractVersion"; "verificationMethod"; "requiredCompatibility" ] do
                  if (strOpt f k |> Option.defaultValue "").Trim() = "" then yield sprintf "%s lacks %s" id f
              if (citationsOf "citations" k).IsEmpty && (arr "identity" k).IsEmpty then yield sprintf "%s has no citation and no identity check" id
              let results = i.Compiled.IdentityResults.TryFind id |> Option.defaultValue []
              for r in results do
                  if not (boolOr "match" false r) then yield sprintf "%s identity '%s' does not match" id (str "label" r)
              if st = "identity-verified" && results.IsEmpty then yield sprintf "%s claims identity-verified without identity checks" id
              match strOpt "producedBy" k with
              | Some t when not (i.Compiled.Curation.Tasks |> List.exists (fun x -> x.Id = t && List.contains id x.Contracts)) ->
                  yield sprintf "%s is producedBy %s, which does not name it" id t
              | _ -> () ]

    check "V07" "Every contract is identifiable and no contract is over-claimed" true problems

let private fixedDecisionsSourced (i: Inputs) =
    let classes = set [ "fixed-architectural"; "verified-reusable"; "implementation"; "runtime-situational"; "unresolved" ]

    let problems =
        [ for d in i.Compiled.Curation.Decisions do
              let id = str "id" d
              let cls = str "classification" d
              if not (classes.Contains cls) then yield sprintf "%s has unknown classification '%s'" id cls
              if (cls = "fixed-architectural" || cls = "verified-reusable") && not (decisionVerified i.Compiled d) then
                  yield sprintf "%s is %s without a verified authoritative source" id cls
              if cls = "fixed-architectural" && (strs "constraints" d).IsEmpty then yield sprintf "%s records no constraints" id ]

    check "V08" "Every fixed decision has a verified authoritative source and constraints" true problems

let private plansPreserveScope (i: Inputs) =
    let tasks = i.Compiled.Curation.Tasks |> List.map (fun t -> t.Id, t) |> Map.ofList

    let planProblems name =
        let p = i.Outputs.[name]
        let pts = arr "tasks" p
        let steps = pts |> List.map (fun t -> str "id" t, intOpt "step" t |> Option.defaultValue 0L) |> Map.ofList

        [ if (pts |> List.map (str "id") |> Set.ofList) <> (tasks |> Map.keys |> Set.ofSeq) then yield sprintf "%s does not contain exactly the graph's tasks" name
          for pt in pts do
              let id = str "id" pt
              match tasks.TryFind id with
              | None -> ()
              | Some t ->
                  if strs "completion" pt <> t.Completion then yield sprintf "%s changes the completion conditions of %s" name id
                  if strs "gates" pt <> t.Gates then yield sprintf "%s changes the gates of %s" name id
                  for d in t.DependsOn do
                      if steps.TryFind d.Task |> Option.defaultValue 0L >= steps.[id] then yield sprintf "%s schedules %s before its prerequisite %s finishes" name id d.Task ]

    let uncovered =
        i.Compiled.Requirements
        |> List.filter (fun r -> not (i.Compiled.Coverage.ContainsKey r.Id))
        |> List.map (fun r -> sprintf "%s is in no work item" r.Id)

    check "V09" "Both execution plans preserve the complete scope, dependencies and completion conditions" true
        (planProblems "execution-plan-time.json" @ planProblems "execution-plan-resource.json" @ uncovered)

let private nothingMarkedComplete (i: Inputs) =
    let inv = i.Outputs.["requirements-inventory.json"]
    let vm = i.Outputs.["verification-manifest.json"]
    let appSource = Regex(@"\.(fs|fsi|fsx|fsproj|ts|tsx|js|mjs|html|css)$")

    let problems =
        [ yield!
              arr "requirements" inv
              |> List.filter (fun r -> field "implementationStatus" r |> Option.map (str "status") <> Some "not-verified")
              |> List.map (fun r -> sprintf "%s is not marked not-verified" (str "id" r))
          yield!
              arr "entries" vm
              |> List.filter (fun e -> str "currentStatus" e <> "NOT_RUN")
              |> List.map (fun e -> sprintf "%s has status %s without evidence" (str "id" e) (str "currentStatus" e))
          yield!
              i.SourceFiles
              |> List.filter (fun f -> appSource.IsMatch f && not (f.StartsWith "tools/"))
              |> List.map (sprintf "the source commit contains application-like file %s; implementation status must be re-examined") ]

    check "V10" "No requirement is marked complete; the source commit holds no application implementation" true problems

let private citations (i: Inputs) =
    i.Compiled.CitationResults
    |> Map.toList
    |> List.filter (fun (_, r) -> not r.Verified)
    |> List.map (fun (cit, r) -> sprintf "%s:%s \"%s\" - %s" cit.Repo cit.Path cit.Quote r.Detail)
    |> check "V11" "Every citation is found verbatim at the recorded commit" true

let private revisions (i: Inputs) =
    i.RepoChecks
    |> List.filter (fun (_, expected, actual) -> expected <> actual)
    |> List.map (fun (n, expected, actual) -> sprintf "%s: expected %s, checkout is %s" n expected actual)
    |> check "V12" "Every repository checkout is at the recorded commit" true

let private experiment (i: Inputs) =
    let e = i.Outputs.["percepta-experiment.json"]

    [ yield!
          arr "metricVerification" e
          |> List.filter (fun m -> not (boolOr "definedInPercepta" false m))
          |> List.map (fun m -> sprintf "metric %s is not defined in Percepta telemetry/metrics.json" (str "metric" m))
      for arm in arr "arms" e do
          match strOpt "plan" arm with
          | Some p when p <> "" && not (i.Outputs.ContainsKey p) -> yield sprintf "arm %s names missing plan %s" (str "id" arm) p
          | _ -> () ]
    |> check "V13" "The Percepta experiment references defined metrics and generated plans" true

let private baselineFreshness (i: Inputs) =
    match i.CommittedBaseline with
    | None -> check "V14" "The committed baseline is pinned at the compiled source commit" false [ "no committed baseline supplied" ]
    | Some b ->
        let committed = field "source" b |> Option.map (str "commit") |> Option.defaultValue ""
        let committedIds = idsOf "requirements" b |> Set.ofList
        let compiledIds = idsOf "requirements" i.Baseline |> Set.ofList

        let drift = Baseline.content b <> Baseline.content i.Baseline

        [ if drift then
              yield sprintf "requirements/causa-requirements-baseline.json (pinned at %s) differs from the generator's output at the source commit %s" committed i.Compiled.SourceCommit
          let added = Set.difference compiledIds committedIds
          if not added.IsEmpty then yield sprintf "%d requirements at the source commit are missing from the committed baseline: %s" added.Count (String.concat ", " (Set.toList added)) ]
        |> check "V14" "The committed baseline has no drift against the source commit (readiness, non-critical for compilation)" false

let private blockersOf (c: Compiled) : Json list =
    [ for f in c.Curation.Findings |> List.sortBy (str "id") do
          if isOpenMaterial f && str "severity" f = "critical" then yield f
      for d in c.Curation.Decisions |> List.sortBy (str "id") do
          if str "classification" d = "unresolved" then yield d
      for k in c.Curation.Contracts |> List.sortBy (str "id") do
          let st = str "status" k
          // A contract the run itself produces (producedBy) is expected to be missing before kickoff.
          if st = "blocked" || (st = "missing" && strOpt "producedBy" k |> Option.isNone) then yield k ]

let private blockersClassified (i: Inputs) =
    let classes = field "blockerClasses" i.Compiled.Curation.Sources |> Option.map (function JObj ps -> ps |> List.map fst | _ -> []) |> Option.defaultValue [] |> Set.ofList

    [ for b in blockersOf i.Compiled do
          let id = str "id" b
          match strOpt "blockerClass" b with
          | Some c when classes.Contains c -> ()
          | Some c -> yield sprintf "%s has unknown blocker class '%s'" id c
          | None -> yield sprintf "%s is a readiness blocker without a blocker class" id
      for d in i.Compiled.Curation.Decisions do
          if str "classification" d = "unresolved" then
              for key in [ "recommendation"; "approvalRequired" ] do
                  if (strOpt key d |> Option.defaultValue "").Trim() = "" then yield sprintf "%s lacks %s" (str "id" d) key
              if (strs "alternatives" d).Length < 2 then yield sprintf "%s presents fewer than two alternatives" (str "id" d) ]
    |> check "V15" "Every readiness blocker is classified and every open decision carries alternatives, a recommendation and the approval it needs" true

let private manifestsValid (i: Inputs) =
    let c = i.Compiled
    let tasks = c.Curation.Tasks |> List.map (fun t -> t.Id, t) |> Map.ofList
    let known =
        Set.unionMany
            [ c.Requirements |> List.map (fun r -> r.Id) |> Set.ofList
              c.Curation.Findings |> List.map (str "id") |> Set.ofList
              c.Curation.Decisions |> List.map (str "id") |> Set.ofList
              c.Curation.Contracts |> List.map (str "id") |> Set.ofList ]
    let decisionClass = c.Curation.Decisions |> List.map (fun d -> str "id" d, str "classification" d) |> Map.ofList
    let checkTypes = set [ "package-state"; "baseline-pinned"; "sdk-pinned"; "secret-scan"; "git-ref-contains"; "registry-set-contains"; "env-present"; "github-auth-contract"; "manual" ]
    let timed = i.Outputs.["timed-execution-manifest.json"]
    let timedSteps = arr "tasks" timed |> List.map (fun t -> str "id" t, intOpt "step" t |> Option.defaultValue 0L) |> Map.ofList
    let pre = i.Outputs.["preflight-manifest.json"]
    let checkIds = c.Curation.PreflightChecks |> List.map (str "id")

    [ for t in c.Curation.Tasks do
          if t.Phase <> "preflight" && t.Phase <> "timed" then yield sprintf "%s has phase '%s'" t.Id t.Phase
          if t.Phase = "preflight" then
              for d in t.DependsOn do
                  if tasks.ContainsKey d.Task && tasks.[d.Task].Phase = "timed" then yield sprintf "preflight task %s depends on timed task %s" t.Id d.Task
          if t.Phase = "timed" && not (timedSteps.ContainsKey t.Id) then yield sprintf "timed task %s is missing from the timed-execution manifest" t.Id
          if t.Phase = "timed" then
              for d in t.DependsOn do
                  match timedSteps.TryFind d.Task, timedSteps.TryFind t.Id with
                  | Some a, Some b when a >= b -> yield sprintf "timed manifest runs %s before its prerequisite %s" t.Id d.Task
                  | _ -> ()
          match t.ConditionalOn with
          | Some d when not (known.Contains d) -> yield sprintf "%s is conditional on unknown %s" t.Id d
          | _ -> ()
          match t.ExcludedBy with
          | Some d when t.ConditionalOn <> Some d -> yield sprintf "%s is excluded by %s but not conditional on it" t.Id d
          | Some d when decisionClass.TryFind d |> Option.forall ((=) "unresolved") -> yield sprintf "%s is excluded by %s, which is not decided" t.Id d
          | _ -> ()
      if (List.distinct checkIds).Length <> checkIds.Length then yield "duplicate preflight check id"
      if (arr "checks" pre).Length <> checkIds.Length then yield "preflight manifest does not list every check"
      for k in c.Curation.PreflightChecks do
          let id = str "id" k
          if not (checkTypes.Contains(str "type" k)) then yield sprintf "%s has unknown type %s" id (str "type" k)
          for t in strs "tasks" k do
              if not (tasks.ContainsKey t) then yield sprintf "%s names unknown task %s" id t
          for r in strs "refs" k do
              if not (known.Contains r) then yield sprintf "%s names unknown reference %s" id r
      for x in c.Curation.DefectTests do
          let id = str "id" x
          if not (tasks.ContainsKey(str "task" x)) then yield sprintf "%s names unknown task %s" id (str "task" x)
          for r in strs "requirements" x do
              if not (known.Contains r) then yield sprintf "%s names unknown requirement %s" id r
      for cls in [ "DC-001"; "DC-002"; "DC-003"; "DC-004"; "DC-005"; "DC-006" ] do
          if not (c.Curation.DefectTests |> List.exists (fun x -> str "defectClass" x = cls)) then yield sprintf "no failing-first test specified for %s" cls ]
    |> check "V16" "Preflight and timed-execution manifests are complete and consistent" true

let private p0Coverage (i: Inputs) =
    let vm = i.Outputs.["verification-manifest.json"]
    let gated = arr "entries" vm |> List.filter (fun e -> not (strs "gates" e).IsEmpty) |> List.map (str "requirement") |> Set.ofList

    i.Compiled.Requirements
    |> List.filter (fun r -> r.Priority = "P0")
    |> List.collect (fun r ->
        [ if not (i.Compiled.Coverage.ContainsKey r.Id) then yield sprintf "P0 %s is in no work item" r.Id
          if not (gated.Contains r.Id) then yield sprintf "P0 %s has no verification gate" r.Id ])
    |> check "V17" "Every P0 requirement is covered by a work item and a verification gate" true

let run (i: Inputs) : Check list =
    [ inventoryComplete i
      traceability i
      contradictions i
      dependencies i
      dag i
      verificationCoverage i
      contractsIdentifiable i
      fixedDecisionsSourced i
      plansPreserveScope i
      nothingMarkedComplete i
      citations i
      revisions i
      experiment i
      blockersClassified i
      manifestsValid i
      p0Coverage i
      baselineFreshness i ]

let report (i: Inputs) (checks: Check list) =
    let failedCritical = checks |> List.filter (fun x -> x.Critical && not x.Problems.IsEmpty)
    let c = i.Compiled

    let blockers =
        blockersOf c
        |> List.map (fun b ->
            let kind =
                if c.Curation.Findings |> List.exists (fun f -> str "id" f = str "id" b) then "finding"
                elif c.Curation.Decisions |> List.exists (fun d -> str "id" d = str "id" b) then "decision"
                else sprintf "contract-%s" (str "status" b)

            JObj
                [ "kind", JStr kind
                  "id", JStr(str "id" b)
                  "title", JStr(strOpt "title" b |> Option.orElse (strOpt "name" b) |> Option.defaultValue "")
                  "blockerClass", JStr(strOpt "blockerClass" b |> Option.defaultValue "unclassified")
                  "owner", JStr(strOpt "owner" b |> Option.defaultValue (strOpt "approvalRequired" b |> Option.defaultValue ""))
                  "recommendation", JStr(strOpt "recommendation" b |> Option.defaultValue "") ])

    JObj
        [ "schema", JStr "indy-init.requirements-validation-report/1"
          "source", JObj [ "repository", JStr c.SourceRepo; "commit", JStr c.SourceCommit; "baselineSha256", JStr c.BaselineSha256 ]
          "compilationVerdict", JStr(if failedCritical.IsEmpty then "PASS" else "FAIL")
          "compilationRule", JStr "FAIL when any critical check reports a problem; the compiler then exits 3"
          "checks",
          checks
          |> List.map (fun x ->
              JObj
                  [ "id", JStr x.Id
                    "title", JStr x.Title
                    "critical", JBool x.Critical
                    "status", JStr(if x.Problems.IsEmpty then "PASS" elif x.Critical then "FAIL" else "WARN")
                    "problems", jstrs x.Problems ])
          |> JArr
          "rehearsalReadiness",
          JObj
              [ "verdict", JStr(if blockers.IsEmpty then "READY" else "BLOCKED")
                "rule", JStr "BLOCKED while any critical material unresolved finding, unresolved decision, blocked contract, or missing contract that no planned task produces remains; a compilation PASS is not readiness"
                "blockers", JArr blockers ] ]
