/// The one authoritative scope of a rehearsal (docs/rehearsal-010-readiness/PROCESS-CORRECTIONS.md).
///
/// Rehearsal 009 implemented the 38-requirement investigation-content catalog and stopped, although the approved
/// release scope is the frozen package's 161 P0 requirements (613 obligations, 660 plan tests) plus that catalog.
/// Nothing compiled the two into one list, so nothing showed what was left. This module does:
///
/// - `compile` reads the frozen sources (the v4 verification plan, a v4.1.x package's execution plan and DAG, the
///   ratified catalog) and writes `indy-init.approved-scope/1`: every requirement with its priority, obligations,
///   evidence producers, implementation work items and dependencies, the gate-level scope and the exclusions, pinned
///   by the source hashes and closed by a scope hash.
/// - `preflight` re-derives the scope from the sources and refuses a manifest that omits or alters anything.
/// - `coverage` refuses a Praxis backlog that lacks a work item of the manifest.
///
/// Every function is pure over its inputs; reading files happens in the command layer (ScopeCommands).
module Scope

open System
open System.Text.RegularExpressions
open Json

// ---- the domain --------------------------------------------------------------------------------------------------

type ObligationKind =
    /// Judged from test results of an executable suite (dotnet, Playwright).
    | Executable
    /// Judged from records (Praxis review, process, owner records; the independent verifier).
    | Record

type Obligation =
    { Id: string
      Mandatory: bool
      Kind: ObligationKind
      /// The suites (verification or evidence producers) that judge it.
      Producers: string list
      Tests: string list }

type Requirement =
    { Id: string
      Origin: string
      Title: string
      Priority: string
      Mandatory: bool
      /// "always", or the catalog fact that must hold for it to apply.
      Applicability: string
      Obligations: Obligation list
      WorkItems: string list
      DependsOn: string list }

type WorkItem =
    { Id: string
      Title: string
      Origin: string
      Owner: string
      DependsOn: string list
      Requirements: string list
      /// Requirements without priority P0 (gate-level scope, ODR-V4-10 A) the task also covers.
      GateRequirements: string list }

type Sources =
    { /// requirements-compiled/v4/verification-plan.json (the P0 plan, unchanged by every v4.1.x package).
      Plan: Json
      PlanSha256: string
      /// The package's verification-execution-plan.json and execution-dag.json, and its manifest.
      ExecutionPlan: Json
      ExecutionPlanSha256: string
      Dag: Json
      PackageManifest: Json
      PackageManifestSha256: string
      PackagePath: string
      /// The ratified catalog (praxis.requirement-catalog/1) and its digest.
      Catalog: Json
      CatalogSha256: string
      /// The v4 requirements baseline, for the requirements without a stated priority (gate-level scope).
      Baseline: Json
      BaselineSha256: string
      Repository: string
      Commit: string }

// ---- reading the frozen sources -------------------------------------------------------------------------------------

/// The executable runners; every other suite of the plan produces record evidence.
let private executableSuite (runner: string) = runner.StartsWith("dotnet test ", StringComparison.Ordinal) || runner.StartsWith("npx playwright test", StringComparison.Ordinal)

/// obligation id -> (suite id, test id, executable) for every binding of the package's execution plan.
let private bindings (executionPlan: Json) =
    arr "suites" executionPlan
    |> List.collect (fun s ->
        let exe = executableSuite (str "runner" s)
        arr "tests" s |> List.collect (fun t -> strs "obligations" t |> List.map (fun o -> o, (str "id" s, str "id" t, exe))))
    |> List.groupBy fst
    |> List.map (fun (o, xs) -> o, xs |> List.map snd)
    |> Map.ofList

/// Does a DAG task cover a requirement id (`covers`: exact ids, `regex:` patterns)?
let covers (task: Json) (requirement: string) =
    strs "covers" task
    |> List.exists (fun c -> if c.StartsWith("regex:", StringComparison.Ordinal) then Regex.IsMatch(requirement, c.Substring 6) else c = requirement)

/// The requirement family of an id ("UI-001" -> "UI", "EXT-DATA-001" -> "EXT-DATA"): generated work items group
/// uncovered requirements of one family, so overlapping obligations become one implementation unit, not 660 tasks.
let family (id: string) = Regex.Replace(id, "-[0-9]+$", "")

/// Praxis 3.25.0's verification levels by check (praxis.requirement-catalog/1): obligation ids are REQ/Vn/check.
let private levelOf =
    Map.ofList
        [ "mapped", "V1"; "exists", "V1"; "visible", "V2"; "accessible", "V2"; "editable", "V3"; "enter", "V3"; "activate", "V3"; "select", "V3"
          "outcome", "V4"; "validation", "V4"; "rejects", "V4"; "save", "V5"; "reload", "V5"; "update", "V5"; "isolation", "V5" ]

let private planRequirements (s: Sources) (dagTasks: Json list) : Requirement list =
    let bound = bindings s.ExecutionPlan
    arr "requirements" s.Plan
    |> List.map (fun r ->
        let id = str "requirement" r
        let obligations =
            arr "obligations" r
            |> List.map (fun o ->
                let oid = str "sourceObligation" o
                let bs = bound.TryFind oid |> Option.defaultValue []
                { Id = oid
                  Mandatory = true
                  Kind = (if bs |> List.exists (fun (_, _, exe) -> exe) then Executable else Record)
                  Producers = bs |> List.map (fun (suite, _, _) -> suite) |> List.distinct |> List.sort
                  Tests = bs |> List.map (fun (_, t, _) -> t) |> List.distinct |> List.sort })
        let tasks = dagTasks |> List.filter (fun t -> covers t id) |> List.map (str "id")
        let executable = obligations |> List.exists (fun o -> o.Kind = Executable)
        { Id = id
          Origin = "v4-plan"
          Title = strOpt "title" r |> Option.defaultValue id
          Priority = strOpt "priority" r |> Option.defaultValue "P0"
          Mandatory = true
          Applicability = "always"
          Obligations = obligations
          WorkItems = (if tasks.IsEmpty && executable then [ "WI-GEN-" + family id ] else tasks)
          DependsOn =
            field "dependencies" r
            |> Option.map (strs "requirements")
            |> Option.defaultValue []
            |> List.filter (fun d -> d <> id)
            |> List.distinct
            |> List.sort })

let private catalogRequirements (s: Sources) : Requirement list =
    arr "requirements" s.Catalog
    |> List.map (fun r ->
        let id = str "id" r
        let mandatory = boolOr "required" false r
        let applicability =
            match arr "appliesWhen" r with
            | [] -> "always"
            | conditions -> conditions |> List.map (fun c -> sprintf "%s=%s" (str "fact" c) (match field "equals" c with Some(JBool b) -> (if b then "true" else "false") | Some(JStr v) -> v | _ -> "?")) |> String.concat " and "
        let screen = strOpt "screen" r |> Option.defaultValue "screen"
        { Id = id
          Origin = "catalog"
          Title = strOpt "name" r |> Option.defaultValue id
          Priority = "catalog-ratified"
          Mandatory = mandatory
          Applicability = applicability
          Obligations =
            strs "verification" r
            |> List.map (fun check ->
                { Id = sprintf "%s/%s/%s" id (levelOf.TryFind check |> Option.defaultValue "V?") check
                  Mandatory = mandatory
                  Kind = Executable
                  Producers = [ "DOKIMOS-REQUIREMENTS" ]
                  Tests = [] })
          WorkItems = [ "WI-INV-" + (screen.ToUpperInvariant().Replace("INVESTIGATION-", "")) ]
          DependsOn = strs "dependsOn" r |> List.sort })

/// The work items: every DAG task that covers a requirement or is a timed task, plus one generated item per
/// requirement family no task covers and one per catalog screen.
let private workItems (dagTasks: Json list) (requirements: Requirement list) (gateLevel: string list) : WorkItem list =
    let reqsOf (wi: string) = requirements |> List.filter (fun r -> List.contains wi r.WorkItems) |> List.map (fun r -> r.Id)
    let fromDag =
        dagTasks
        |> List.map (fun t ->
            { Id = str "id" t
              Title = str "title" t
              Origin = "execution-dag"
              Owner = strOpt "owner" t |> Option.defaultValue ""
              DependsOn = strs "dependsOn" t
              Requirements = reqsOf (str "id" t)
              GateRequirements = gateLevel |> List.filter (covers t) })
    let generated =
        requirements
        |> List.collect (fun r -> r.WorkItems)
        |> List.distinct
        |> List.filter (fun wi -> not (fromDag |> List.exists (fun d -> d.Id = wi)))
        |> List.sort
        |> List.map (fun wi ->
            let catalog = wi.StartsWith("WI-INV-", StringComparison.Ordinal)
            { Id = wi
              Title =
                if catalog then sprintf "Implement the CAT-CAUSA-INVESTIGATION requirements of screen %s" (wi.Substring 7)
                else sprintf "Implement the %s requirements no execution-DAG task covers" (wi.Substring 7)
              Origin = "generated"
              Owner = (if catalog || wi.StartsWith("WI-GEN-UI", StringComparison.Ordinal) || wi.StartsWith("WI-GEN-WDL", StringComparison.Ordinal) then "ui" else "integration")
              // After the first legal command runs end to end (T17), so a generated item never precedes the kernel.
              DependsOn = [ "T17" ]
              Requirements = reqsOf wi
              GateRequirements = [] })
    fromDag @ generated

// ---- serialization -------------------------------------------------------------------------------------------------

let private kindName = function Executable -> "executable" | Record -> "record"

let private obligationJson (o: Obligation) =
    JObj [ "id", JStr o.Id; "mandatory", JBool o.Mandatory; "kind", JStr(kindName o.Kind); "producers", jstrs o.Producers; "tests", jstrs o.Tests ]

let private requirementJson (r: Requirement) =
    JObj
        [ "id", JStr r.Id
          "origin", JStr r.Origin
          "title", JStr r.Title
          "priority", JStr r.Priority
          "mandatory", JBool r.Mandatory
          "applicability", JStr r.Applicability
          "executable", JBool(r.Obligations |> List.exists (fun o -> o.Kind = Executable))
          "workItems", jstrs r.WorkItems
          "dependsOn", jstrs r.DependsOn
          "obligations", JArr(r.Obligations |> List.map obligationJson) ]

let private workItemJson (w: WorkItem) =
    JObj [ "id", JStr w.Id; "title", JStr w.Title; "origin", JStr w.Origin; "owner", JStr w.Owner; "dependsOn", jstrs w.DependsOn; "requirements", jstrs w.Requirements; "gateRequirements", jstrs w.GateRequirements ]

/// The scope hash: SHA-256 of the manifest serialized without its `scopeSha256` field and without `source` (the
/// Indy-init commit it was compiled at is informational: the frozen hashes pin the content).
let scopeHash (manifest: Json) =
    match manifest with
    | JObj ps -> ps |> List.filter (fun (k, _) -> k <> "scopeSha256" && k <> "source") |> JObj |> serialize |> Text.Encoding.UTF8.GetBytes |> Git.sha256Hex
    | j -> j |> serialize |> Text.Encoding.UTF8.GetBytes |> Git.sha256Hex

let withScopeHash (manifest: Json) =
    match manifest with
    | JObj ps -> JObj(ps @ [ "scopeSha256", JStr(scopeHash manifest) ])
    | j -> j

// ---- compile -------------------------------------------------------------------------------------------------------

let compile (s: Sources) : Json =
    let dagTasks = arr "tasks" s.Dag
    let planReqs = planRequirements s dagTasks
    let catalogReqs = catalogRequirements s
    let requirements = planReqs @ catalogReqs
    let planIds = planReqs |> List.map (fun r -> r.Id) |> Set.ofList
    let gateLevel =
        arr "requirements" s.Baseline
        |> List.filter (fun r -> not (planIds.Contains(str "id" r)))
        |> List.map (fun r -> str "id" r)
        |> List.sort
    let items = workItems dagTasks requirements gateLevel
    let producers =
        arr "suites" s.ExecutionPlan
        |> List.filter (fun su -> boolOr "planTests" false su)
        |> List.map (fun su -> JObj [ "id", JStr(str "id" su); "kind", JStr(if executableSuite (str "runner" su) then "verification" else "evidence"); "environment", JStr(str "environment" su); "runner", JStr(str "runner" su) ])
        |> fun xs -> xs @ [ JObj [ "id", JStr "DOKIMOS-REQUIREMENTS"; "kind", JStr "verification"; "environment", JStr "ci-browser"; "runner", JStr "dokimos verification requirements (the catalog's compiled manifest, observations recorded by the application's requirement probes)" ] ]
    let obligations = requirements |> List.collect (fun r -> r.Obligations)
    let planObligations = planReqs |> List.collect (fun r -> r.Obligations)
    let count p = obligations |> List.filter p |> List.length
    JObj
        [ "schema", JStr "indy-init.approved-scope/1"
          "description", JStr "The full approved release scope of the rehearsal: every P0 requirement of the frozen plan and every requirement of the ratified catalog, each with its obligations, evidence producers, implementation work items and dependencies. A passing catalog is not this scope. Generated by `requirements-compiler scope compile`; `scope preflight` refuses any manifest that differs from what the sources give."
          "source", JObj [ "repository", JStr s.Repository; "commit", JStr s.Commit ]
          "frozen",
          JObj
              [ "package", JStr s.PackagePath
                "packageVersion", JStr(strOpt "packageVersion" s.PackageManifest |> Option.defaultValue "")
                "packageStatus", JStr(strOpt "status" s.PackageManifest |> Option.defaultValue "")
                "packageManifestSha256", JStr s.PackageManifestSha256
                "verificationPlan", JStr "requirements-compiled/v4/verification-plan.json"
                "verificationPlanSha256", JStr s.PlanSha256
                "executionPlanSha256", JStr s.ExecutionPlanSha256
                "catalog", JStr "requirements/catalogs/causa-investigation.requirement-catalog.json"
                "catalogStatus", JStr(strOpt "status" s.Catalog |> Option.defaultValue "")
                "catalogSha256", JStr s.CatalogSha256
                "baseline", JStr "requirements/causa-requirements-baseline-v4.json"
                "baselineSha256", JStr s.BaselineSha256 ]
          "approvals",
          JArr
              [ JObj [ "scope", JStr "P0 requirements of the v4 plan, verified per obligation"; "decision", JStr "ODR-V4-10 option A"; "record", JStr "APR-V4-OWNER-1 (kickoff/rehearsal-007/approvals.causa.json)" ]
                JObj [ "scope", JStr "CAT-CAUSA-INVESTIGATION INV-001..018"; "decision", JStr "ratified by the owner"; "record", JStr "Indy-init commit 0ab8ffd" ]
                JObj [ "scope", JStr "CAT-CAUSA-INVESTIGATION INV-019..038"; "decision", JStr "OD-INDY-19..21, OD-INDY-23"; "record", JStr "Indy-init commit 7cefdc6 (PR #98)" ] ]
          "counts",
          JObj
              [ "requirements", jint requirements.Length
                "planRequirements", jint planReqs.Length
                "catalogRequirements", jint catalogReqs.Length
                "mandatoryRequirements", jint (requirements |> List.filter (fun r -> r.Mandatory) |> List.length)
                "obligations", jint obligations.Length
                "mandatoryObligations", jint (count (fun o -> o.Mandatory))
                "planObligations", jint planObligations.Length
                "planTests", jint (planObligations |> List.collect (fun o -> o.Tests) |> List.distinct |> List.length)
                "executableObligations", jint (count (fun o -> o.Kind = Executable))
                "recordOnlyObligations", jint (count (fun o -> o.Kind = Record))
                "workItems", jint items.Length
                "generatedWorkItems", jint (items |> List.filter (fun w -> w.Origin = "generated") |> List.length)
                "gateLevelRequirements", jint gateLevel.Length ]
          "reconciliation",
          JStr "Obligations are not tasks. Each requirement is one implementation unit, linked to the work items that build it; its obligations are linked to the suites that judge them. Record-only obligations (reviews, process records, owner records, the independent verifier) need an evidence producer, not implementation. An obligation bound in several suites (for example an executable test and a review record) is judged by each and implemented once."
          "requirements", JArr(requirements |> List.map requirementJson)
          "workItems", JArr(items |> List.map workItemJson)
          "evidenceProducers", JArr producers
          "gateLevelScope",
          JObj
              [ "rule", JStr "ODR-V4-10 option A (APR-V4-OWNER-1): requirements without priority P0 stay in scope and are verified by their manifest rules and the acceptance gates (AG-11, AG-12), not per obligation. They are not excluded."
                "requirements", jstrs gateLevel ]
          "exclusions",
          JObj
              [ "rule", JStr "A P0 requirement leaves scope only through an exclusion whose approval is an authenticated operator change {requirement_id, action: exclude} in the pinned approvals file (v4 exclusionPolicy). None is recorded. A catalog requirement whose applicability fact is false is NOT_APPLICABLE, not excluded."
                "requirements", JArr [] ] ]
    |> withScopeHash

// ---- preflight -------------------------------------------------------------------------------------------------------

type Refusal = { Code: string; Detail: string }

let private refusal code detail = { Code = code; Detail = detail }

let refusalJson (r: Refusal) = JObj [ "code", JStr r.Code; "detail", JStr r.Detail ]

/// Re-derives the scope from the sources and compares: a manifest that omits a mandatory requirement, changes an
/// obligation, drops a work-item link or a producer, or carries a stale hash is refused. Implementation may not begin
/// until this list is empty.
let preflight (expected: Json) (manifest: Json) : Refusal list =
    let byId key j = arr key j |> List.map (fun x -> str "id" x, x) |> Map.ofList
    let want, have = byId "requirements" expected, byId "requirements" manifest
    let haveItems = byId "workItems" manifest |> Map.toList |> List.map fst |> Set.ofList
    let haveProducers = byId "evidenceProducers" manifest |> Map.toList |> List.map fst |> Set.ofList
    let frozen (j: Json) = field "frozen" j |> Option.map serialize |> Option.defaultValue ""
    [ if strOpt "schema" manifest <> Some "indy-init.approved-scope/1" then yield refusal "SCHEMA" "the manifest is not indy-init.approved-scope/1"
      if strOpt "scopeSha256" manifest <> Some(scopeHash manifest) then yield refusal "SCOPE_HASH_MISMATCH" "scopeSha256 is not the hash of the manifest's content"
      if frozen manifest <> frozen expected then yield refusal "FROZEN_SOURCES_CHANGED" "the frozen package, plan, catalog or baseline hashes differ from the sources"
      for KeyValue(id, w) in want do
          match have.TryFind id with
          | None when boolOr "mandatory" false w -> yield refusal "MANDATORY_REQUIREMENT_MISSING" (sprintf "%s (%s) is an approved mandatory requirement and is not in the manifest" id (str "origin" w))
          | None -> yield refusal "REQUIREMENT_MISSING" (sprintf "%s is approved scope and is not in the manifest" id)
          | Some h ->
              if boolOr "mandatory" false w && not (boolOr "mandatory" false h) then yield refusal "MANDATORY_WEAKENED" (sprintf "%s is mandatory in the sources and not in the manifest" id)
              let ids key j = arr key j |> List.map (str "id") |> Set.ofList
              let missing = Set.difference (ids "obligations" w) (ids "obligations" h)
              if not missing.IsEmpty then yield refusal "OBLIGATION_MISSING" (sprintf "%s: %s" id (missing |> String.concat ", "))
              if serialize w <> serialize h && missing.IsEmpty then yield refusal "REQUIREMENT_ALTERED" (sprintf "%s differs from what the sources give" id)
      for KeyValue(id, _) in have do
          if not (want.ContainsKey id) then yield refusal "REQUIREMENT_UNSOURCED" (sprintf "%s is in the manifest but in no approved source" id)
      // Coverage inside the manifest: every executable requirement is built by a declared work item, every
      // obligation is judged by a declared producer.
      for KeyValue(id, h) in have do
          let wis = strs "workItems" h
          if boolOr "executable" false h && wis.IsEmpty then yield refusal "REQUIREMENT_ORPHANED" (sprintf "%s has executable obligations and no implementation work item" id)
          for wi in wis do
              if not (haveItems.Contains wi) then yield refusal "WORK_ITEM_UNDECLARED" (sprintf "%s names work item %s, which the manifest does not declare" id wi)
          for o in arr "obligations" h do
              let ps = strs "producers" o
              if ps.IsEmpty then yield refusal "OBLIGATION_WITHOUT_PRODUCER" (sprintf "%s has no verification or evidence producer" (str "id" o))
              for p in ps do
                  if not (haveProducers.Contains p) then yield refusal "PRODUCER_UNDECLARED" (sprintf "%s names producer %s, which the manifest does not declare" (str "id" o) p) ]

// ---- Praxis work-item coverage -----------------------------------------------------------------------------------

/// The manifest work-item id a Praxis backlog item carries: its title starts with "[ID]" (`praxis add "[T13] ..."`).
let praxisItemId (item: Json) =
    let m = Regex.Match(strOpt "title" item |> Option.defaultValue "", @"^\[([A-Za-z0-9-]+)\]")
    if m.Success then Some m.Groups[1].Value else None

/// Every work item of the manifest must exist in the Praxis backlog (.ros/work/queue.json) before implementation, so
/// the agent's own queue shows all remaining work, not one mission item.
let coverage (manifest: Json) (queue: Json) : Refusal list =
    let present = arr "items" queue |> List.choose praxisItemId |> Set.ofList
    arr "workItems" manifest
    |> List.filter (fun w -> not (present.Contains(str "id" w)))
    |> List.map (fun w -> refusal "WORK_ITEM_MISSING" (sprintf "%s (%d requirement(s)) has no Praxis work item: praxis add \"[%s] %s\"" (str "id" w) (strs "requirements" w).Length (str "id" w) (str "title" w)))

/// The durable pre-implementation checkpoint, written only when preflight and coverage pass.
let checkpoint (manifest: Json) (at: string) (commit: string) : Json =
    JObj
        [ "schema", JStr "indy-init.scope-checkpoint/1"
          "scopeSha256", JStr(str "scopeSha256" manifest)
          "recordedAt", JStr at
          "sourceCommit", JStr commit
          "requirements", jint (arr "requirements" manifest).Length
          "workItems", jint (arr "workItems" manifest).Length
          "preflight", JStr "PASS"
          "coverage", JStr "PASS"
          "rule", JStr "No application source or test may be committed before this checkpoint (scope order); the scope it pins changes only with an owner approval {action: approve-scope, scope_sha256} in the pinned approvals file." ]

/// Commits (oldest first) as (sha, paths): the first commit that touches application code must not precede the
/// commit that adds the checkpoint. Bootstrap files (T10) are not application code.
let implementationBeforeCheckpoint (checkpointPath: string) (isApplication: string -> bool) (commits: (string * string list) list) : Refusal list =
    let indexOf p = commits |> List.tryFindIndex (fun (_, files) -> files |> List.exists p)
    match indexOf ((=) checkpointPath), indexOf isApplication with
    | None, Some i -> [ refusal "IMPLEMENTATION_BEFORE_CHECKPOINT" (sprintf "commit %s changes application code and no scope checkpoint exists" (fst commits[i])) ]
    | Some c, Some i when i < c -> [ refusal "IMPLEMENTATION_BEFORE_CHECKPOINT" (sprintf "commit %s changes application code before the scope checkpoint (commit %s)" (fst commits[i]) (fst commits[c])) ]
    | _ -> []
