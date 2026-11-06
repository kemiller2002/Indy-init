/// Adversarial process tests for the scope manifest, Praxis work-item coverage, the progress model and the stop
/// decision (docs/rehearsal-010-readiness/PROCESS-CORRECTIONS.md, phase 7). Each case is a deterministic fixture;
/// none needs a rehearsal. Every inappropriate completion or early stop must be refused with its code.
module ScopeTests

open System
open System.IO
open Json
open Scope
open Progress
open Continuation

// ---- a small deterministic scope: three P0 requirements, two catalog requirements -------------------------------

let private plan =
    let req id (obligations: string list) =
        JObj [ "requirement", JStr id; "priority", JStr "P0"; "title", JStr id; "dependencies", JObj [ "requirements", JArr []; "contracts", JArr [] ]; "obligations", JArr(obligations |> List.map (fun o -> JObj [ "sourceObligation", JStr o ])) ]
    JObj [ "requirements", JArr [ req "P-001" [ "P-001-O1"; "P-001-O2" ]; req "P-002" [ "P-002-O1" ]; req "P-003" [ "P-003-O1" ] ] ]

let private executionPlan =
    let test id (obligations: string list) = JObj [ "id", JStr id; "obligations", jstrs obligations ]
    let suite id runner (tests: Json list) = JObj [ "id", JStr id; "runner", JStr runner; "environment", JStr "ci"; "planTests", JBool true; "tests", JArr tests ]
    JObj
        [ "suites",
          JArr
              [ suite "SU-X" "dotnet test Causa.slnx --filter FullyQualifiedName~X." [ test "dotnet:X.P001.O1" [ "P-001-O1" ]; test "dotnet:X.P001.O2" [ "P-001-O2" ] ]
                suite "SU-W" "npx playwright test --project=webkit tests/browser/acceptance" [ test "playwright:acceptance/P-002.spec.js > P-002-O1 [webkit]" [ "P-002-O1" ] ]
                suite "SU-REVIEW" "Praxis work evidence --kind independent-review" [ test "praxis:independent-review" [ "P-003-O1"; "P-001-O2" ] ] ] ]

let private dag =
    let task id (covers: string list) (deps: string list) = JObj [ "id", JStr id; "title", JStr id; "owner", JStr "domain"; "covers", jstrs covers; "dependsOn", jstrs deps ]
    JObj [ "tasks", JArr [ task "T1" [ "P-001" ] []; task "T2" [ "regex:^P-002$" ] [ "T1" ]; task "T17" [] [ "T1" ]; task "T3" [ "P-003" ] [ "T2" ] ] ]

let private catalog =
    let req id required (checks: string list) = JObj [ "id", JStr id; "screen", JStr "investigation-start"; "name", JStr id; "required", JBool required; "verification", jstrs checks ]
    JObj [ "status", JStr "ratified"; "requirements", JArr [ req "C-001" true [ "exists"; "save" ]; req "C-002" false [ "exists" ] ] ]

let private sources: Sources =
    { Plan = plan
      PlanSha256 = "plan"
      ExecutionPlan = executionPlan
      ExecutionPlanSha256 = "exec"
      Dag = dag
      PackageManifest = JObj [ "packageVersion", JStr "test"; "status", JStr "CANDIDATE" ]
      PackageManifestSha256 = "pkg"
      PackagePath = "fixture"
      Catalog = catalog
      CatalogSha256 = "cat"
      Baseline = JObj [ "requirements", JArr([ "P-001"; "P-002"; "P-003"; "U-001" ] |> List.map (fun id -> JObj [ "id", JStr id ])) ]
      BaselineSha256 = "base"
      Repository = "fixture"
      Commit = "0000000000000000000000000000000000000000" }

let private manifest = compile sources
let private scope = str "scopeSha256" manifest

let private t0 = DateTimeOffset.Parse "2026-11-07T14:30:00Z"
let private clock = { T0 = t0; Deadline = t0.AddMinutes 420.0 }

let private ev (kind: string) (fields: (string * Json) list) = JObj(("kind", JStr kind) :: fields)
let private started wi (rs: string list) = ev "implementation-started" [ "workItem", JStr wi; "requirements", jstrs rs ]
let private completed (rs: string list) = ev "implementation-completed" [ "requirements", jstrs rs; "commit", JStr "c" ]
let private passed o = ev "verification-recorded" [ "obligation", JStr o; "result", JStr "pass" ]
let private blocked suite cause = ev "suite-blocked" [ "suite", JStr suite; "cause", JStr cause ]

let private catalogDone =
    [ started "WI-INV-START" [ "C-001"; "C-002" ]; completed [ "C-001"; "C-002" ]; passed "C-001/V1/exists"; passed "C-001/V5/save"; passed "C-002/V1/exists" ]

let private allDone =
    catalogDone
    @ [ started "T1" [ "P-001" ]; completed [ "P-001" ]; started "T2" [ "P-002" ]; completed [ "P-002" ]; started "T3" [ "P-003" ]; completed [ "P-003" ]
        passed "P-001-O1"; passed "P-001-O2"; passed "P-002-O1"; passed "P-003-O1" ]

let private stateOf (events: Json list) =
    match replay manifest events with
    | Ok s -> s
    | Error e -> failwithf "fixture events do not replay: %s" e

let private request (minutesIn: float) (reason: string) =
    { At = t0.AddMinutes minutesIn
      Reason = reason
      ClaimsAcceptance = false
      ClaimsComplete = false
      Approval = None
      Blockers = []
      EnvironmentKind = ""
      EnvironmentEvidence = []
      EnvironmentAttempts = 0
      ScopeSha256 = scope
      ComputeUsed = None
      Facts = Map.empty }

let private codes (d: Decision) = d.Refusals |> List.map (fun r -> r.Code)

/// A fixture approval record (tests only; agents never write the real approvals file).
let private fixtureApprovals (changes: Json list) =
    JObj [ "approvals", JArr [ JObj [ "id", JStr "APR-FIXTURE-1"; "authority", JObj [ "kind", JStr "operator"; "id", JStr "owner" ]; "changes", JArr changes ] ] ]
    |> fun j -> approvalChanges j (set [ "owner" ])

let private expectRefused (name: string) (code: string) (d: Decision) =
    [ if d.Permitted && not (List.contains code (codes d)) then yield sprintf "%s: permitted, expected refusal %s" name code
      if not (List.contains code (codes d)) then yield sprintf "%s: refusals %A do not include %s" name (codes d) code ]

let private expectStop (name: string) (d: Decision) =
    [ if not d.Permitted || not d.Refusals.IsEmpty then yield sprintf "%s: expected STOP_PERMITTED with no refusal, got permitted=%b %A" name d.Permitted (codes d) ]

// ---- the eight adversarial cases --------------------------------------------------------------------------------

let adversarial () : string list =
    let decide' events q = decide (stateOf events) clock scope [] q
    [ // 1. The content catalog passes while broader requirements remain.
      let d1 = decide' catalogDone (request 115.0 "complete")
      yield! expectRefused "1 catalog passes, P0 remain" "SCOPE_INCOMPLETE" d1
      yield! expectRefused "1 catalog passes, P0 remain" "CATALOG_IS_NOT_SCOPE" d1
      if d1.OpenMandatory <> [ "P-001"; "P-002"; "P-003" ] then yield sprintf "1: open mandatory %A" d1.OpenMandatory

      // 2. The release gate fails because of an invalid command (MSB1008): verification of SU-X is blocked, nothing else.
      let gateFailed = catalogDone @ [ started "T1" [ "P-001" ]; blocked "SU-X" "MSB1008: Only one project can be specified" ]
      let s2 = stateOf gateFailed
      if s2.Requirements["P-001"].Implementation <> InProgress then yield "2: a blocked suite changed the implementation state"
      if status s2 "P-002" <> NotStartedStatus then yield "2: a blocked suite changed an unrelated requirement"
      yield! expectRefused "2 release gate failed" "NOT_A_STOP_CONDITION" (decide' gateFailed (request 115.0 "release-gate-failed"))
      let viaBlocker = decide' gateFailed { request 115.0 "hard-blocker" with Blockers = [ { Id = "B-MSB1008"; Kind = "verification"; WorkItems = [ "T1" ]; Evidence = [ "evidence/release-gate/run.log" ] } ] }
      yield! expectRefused "2 verification blocker as hard blocker" "INDEPENDENT_WORK_REMAINS" viaBlocker
      if not (List.contains "T2" viaBlocker.IndependentWorkItems && List.contains "T1" viaBlocker.IndependentWorkItems) then
          yield sprintf "2: a verification blocker removed implementation work: %A" viaBlocker.IndependentWorkItems

      // 3. A required work item has not been created in Praxis.
      let queue (ids: string list) = JObj [ "items", JArr(ids |> List.map (fun id -> JObj [ "title", JStr(sprintf "[%s] work" id) ])) ]
      let all = arr "workItems" manifest |> List.map (str "id")
      let missing = coverage manifest [ queue (all |> List.filter ((<>) "T2")) ]
      if missing |> List.map (fun r -> r.Code) <> [ "WORK_ITEM_MISSING" ] || not (missing.Head.Detail.StartsWith "T2") then yield sprintf "3: a missing work item was not refused: %A" missing
      if not (coverage manifest [ queue all ]).IsEmpty then yield "3: a complete Praxis backlog was refused"
      // Work items begun with `praxis work begin` (.ros/context/current.json) count as well as backlog items.
      let context = JObj [ "workItems", JArr(all |> List.map (fun id -> JObj [ "id", JStr id ])) ]
      if not (coverage manifest [ context ]).IsEmpty then yield "3: work items begun in Praxis were not counted"
      // The generated work plan links every obligation of the scope to a work item (authorize-implementation step 4).
      let planned = workPlan manifest |> arr "workItems" |> List.collect (strs "obligations") |> Set.ofList
      let everyObligation = arr "requirements" manifest |> List.collect (arr "obligations") |> List.map (str "id") |> Set.ofList
      if planned <> everyObligation then yield sprintf "3: the work plan leaves %d obligation(s) unlinked" (Set.difference everyObligation planned).Count
      let r9Queue = coverage manifest [ JObj [ "items", JArr [ JObj [ "id", JStr "COND-MISSION-001"; "title", JStr "Build the platform from the kickoff contract" ] ] ] ]
      if r9Queue.Length <> all.Length then
          yield "3: Rehearsal 009's single mission item was accepted as coverage"

      // 4. Completion is declared with requirements missing.
      let d4 = decide' (catalogDone @ [ started "T1" [ "P-001" ]; completed [ "P-001" ] ]) { request 200.0 "complete" with ClaimsComplete = true }
      yield! expectRefused "4 completion with missing requirements" "SCOPE_INCOMPLETE" d4
      yield! expectRefused "4 completion with missing requirements" "COMPLETION_CLAIM_REFUSED" d4

      // 5. A stop with five hours remaining.
      let d5 = decide' catalogDone (request 120.0 "conserve-compute")
      yield! expectRefused "5 five hours remaining" "NOT_A_STOP_CONDITION" d5
      if d5.MinutesRemaining <> 300 then yield sprintf "5: %d minutes remaining, expected 300" d5.MinutesRemaining
      yield! expectRefused "5 deadline claimed early" "DEADLINE_NOT_REACHED" (decide' catalogDone (request 120.0 "deadline"))

      // 6. Implemented, but its verification is blocked.
      let s6 = stateOf (allDone |> List.filter (fun e -> strOpt "obligation" e <> Some "P-002-O1") |> fun es -> es @ [ blocked "SU-W" "webkit project not defined" ])
      if status s6 "P-002" <> VerificationBlocked then yield sprintf "6: implemented + blocked reads %s" (statusName (status s6 "P-002"))
      if s6.Requirements["P-002"].Implementation <> Implemented then yield "6: blocked verification changed the implementation state"
      yield! expectRefused "6 implemented but verification blocked" "SCOPE_INCOMPLETE" (decide (s6) clock scope [] (request 300.0 "complete"))
      match apply s6 (AcceptanceRecorded("P-002", "operator", "APR-FIXTURE-1")) with
      | Ok _ -> yield "6: acceptance of a requirement whose verification is blocked was accepted"
      | Error _ -> ()

      // 7. A scope manifest silently omits a mandatory requirement (hash recomputed, so only the content betrays it).
      let without (id: string) (m: Json) =
          match m with
          | JObj ps -> JObj(ps |> List.map (fun (k, v) -> if k = "requirements" then k, JArr(arr "requirements" m |> List.filter (fun r -> str "id" r <> id)) else k, v)) |> withScopeHash
          | j -> j
      let silent = match without "P-002" manifest with JObj ps -> JObj(ps |> List.filter (fst >> (<>) "scopeSha256")) |> withScopeHash | j -> j
      let r7 = preflight manifest silent |> List.map (fun r -> r.Code)
      if not (List.contains "MANDATORY_REQUIREMENT_MISSING" r7) then yield sprintf "7: the silent omission was not refused: %A" r7
      if not (preflight manifest manifest).IsEmpty then yield sprintf "7: the faithful manifest was refused: %A" (preflight manifest manifest)
      let tampered = match manifest with JObj ps -> JObj(ps |> List.map (fun (k, v) -> if k = "requirements" then k, JArr(arr "requirements" manifest |> List.filter (fun r -> str "id" r <> "C-001")) else k, v)) | j -> j
      if not (preflight manifest tampered |> List.exists (fun r -> r.Code = "SCOPE_HASH_MISMATCH")) then yield "7: an edit without rehashing was not refused"

      // 8. The frozen scope is changed without the owner's approval.
      let reduced = str "scopeSha256" silent
      yield! expectRefused "8 scope change without approval" "SCOPE_CHANGE_UNAPPROVED" (decide' allDone { request 300.0 "complete" with ScopeSha256 = reduced })
      let approved = decide (stateOf allDone) clock scope (fixtureApprovals [ JObj [ "action", JStr "approve-scope"; "scope_sha256", JStr reduced ] ]) { request 300.0 "complete" with ScopeSha256 = reduced }
      if List.contains "SCOPE_CHANGE_UNAPPROVED" (codes approved) then yield "8: an owner-approved scope change was refused" ]

// ---- the legitimate stops, and the guards around them ----------------------------------------------------------

let legitimate () : string list =
    [ yield! expectStop "complete: every applicable mandatory requirement verified" (decide (stateOf allDone) clock scope [] (request 300.0 "complete"))
      // At the deadline the run stops whatever remains; a false acceptance claim is still refused.
      let atDeadline = decide (stateOf catalogDone) clock scope [] (request 420.0 "deadline")
      yield! expectStop "deadline" atDeadline
      let claim = decide (stateOf catalogDone) clock scope [] { request 420.0 "deadline" with ClaimsAcceptance = true }
      if not claim.Permitted then yield "deadline with a claim: the stop itself must be permitted"
      yield! expectRefused "deadline with an invented acceptance" "ACCEPTANCE_NOT_ESTABLISHED" claim
      // Owner authorization: only a trusted operator's record.
      let auth = fixtureApprovals [ JObj [ "decision_id", JStr "OD-STOP-1"; "action", JStr "authorize-stop" ] ]
      yield! expectStop "owner-authorized" (decide (stateOf catalogDone) clock scope auth { request 120.0 "owner-authorized" with Approval = Some "OD-STOP-1" })
      yield! expectRefused "owner-authorized without a record" "OWNER_AUTHORIZATION_MISSING" (decide (stateOf catalogDone) clock scope [] { request 120.0 "owner-authorized" with Approval = Some "OD-STOP-1" })
      let forged = approvalChanges (JObj [ "approvals", JArr [ JObj [ "id", JStr "APR-AGENT"; "authority", JObj [ "kind", JStr "agent"; "id", JStr "claude" ]; "changes", JArr [ JObj [ "decision_id", JStr "OD-STOP-1"; "action", JStr "authorize-stop" ] ] ] ] ]) (set [ "owner" ])
      if not forged.IsEmpty then yield "owner-authorized: an agent-authored record was read as an approval"
      // Environment failure: documented and repeated, and of an environment kind.
      yield! expectStop "environment-failure" (decide (stateOf catalogDone) clock scope [] { request 120.0 "environment-failure" with EnvironmentKind = "container-lost"; EnvironmentEvidence = [ "evidence/env.log" ]; EnvironmentAttempts = 2 })
      yield! expectRefused "a broken gate is not an environment failure" "NOT_AN_ENVIRONMENT_FAILURE" (decide (stateOf catalogDone) clock scope [] { request 120.0 "environment-failure" with EnvironmentKind = "release-gate-failed"; EnvironmentEvidence = [ "x" ]; EnvironmentAttempts = 3 })
      // A documented implementation blocker that leaves no independent work.
      let everyOpen = [ "T1"; "T2"; "T3"; "T17" ]
      yield! expectStop "hard-blocker, nothing independent" (decide (stateOf catalogDone) clock scope [] { request 120.0 "hard-blocker" with Blockers = [ { Id = "B-1"; Kind = "implementation"; WorkItems = everyOpen; Evidence = [ "evidence/b1.md" ] } ] })
      yield! expectRefused "hard-blocker without evidence" "BLOCKER_UNDOCUMENTED" (decide (stateOf catalogDone) clock scope [] { request 120.0 "hard-blocker" with Blockers = [ { Id = "B-1"; Kind = "implementation"; WorkItems = everyOpen; Evidence = [] } ] })
      // The retry budget parks a work item that keeps failing the same way; its dependants wait, others proceed.
      let failing = catalogDone @ [ started "T2" [ "P-002" ] ] @ List.replicate retryBudget (ev "attempt-failed" [ "workItem", JStr "T2"; "cause", JStr "same" ])
      let _, independent = independentWork (stateOf failing) []
      if List.contains "T2" independent || List.contains "T3" independent || not (List.contains "T1" independent) then yield sprintf "retry budget: independent work %A" independent
      // Acceptance comes from the owner or the independent verifier, never from the implementing agent.
      match apply (stateOf allDone) (AcceptanceRecorded("P-001", "agent", "self")) with
      | Ok _ -> yield "acceptance: the implementing agent accepted its own requirement"
      | Error _ -> ()
      match apply (stateOf allDone) (AcceptanceRecorded("P-001", "independent-verifier", "verifier-run-1")) with
      | Ok s when status s "P-001" = Accepted -> ()
      | _ -> yield "acceptance: the independent verifier's record was refused"
      // Events that invent progress are refused.
      match replay manifest [ completed [ "P-001" ] ] with
      | Ok _ -> yield "progress: completion without a start was applied"
      | Error _ -> ()
      match replay manifest [ started "T9" [ "Z-999" ] ] with
      | Ok _ -> yield "progress: a requirement outside the scope was applied"
      | Error _ -> ()
      // Implementation may not precede the scope checkpoint (scope order, kept beside check-sequencing).
      let isApp (p: string) = p.StartsWith "src/"
      if (implementationBeforeCheckpoint ".echelon/scope-checkpoint.json" isApp [ "a", [ "README.md" ]; "b", [ "src/A.fs" ]; "c", [ ".echelon/scope-checkpoint.json" ] ]).IsEmpty then
          yield "order: application code before the checkpoint was accepted"
      if not (implementationBeforeCheckpoint ".echelon/scope-checkpoint.json" isApp [ "a", [ "README.md" ]; "c", [ ".echelon/scope-checkpoint.json" ]; "b", [ "src/A.fs" ] ]).IsEmpty then
          yield "order: application code after the checkpoint was refused" ]

// ---- recovery: a successor continues from repository files alone ----------------------------------------------

let recovery () : string list =
    let midRun = catalogDone @ [ started "T1" [ "P-001" ]; blocked "SU-X" "MSB1008" ]
    // The successor reads the events back from their committed serialization, not from the lost executor's memory.
    let persisted = match parseBytes (Text.Encoding.UTF8.GetBytes(serialize (JArr midRun))) with JArr xs -> xs | _ -> []
    let original, successor = stateOf midRun, stateOf persisted
    let at = t0.AddMinutes 150.0
    let plan = resumePlan successor clock scope at false
    [ if plan.Action <> "CONTINUE" then yield sprintf "recovery: a lost executor at 150 min gives %s, expected CONTINUE" plan.Action
      if plan.NextWork <> snd (independentWork original []) then yield "recovery: the successor's next work differs from the lost executor's"
      if plan.MinutesRemaining <> 270 then yield sprintf "recovery: %d minutes remaining, expected 270" plan.MinutesRemaining
      if plan.BlockedSuites <> [ "SU-X" ] then yield "recovery: the blocked suite was not carried over"
      if successor.Requirements["P-001"].Implementation <> InProgress then yield "recovery: in-progress implementation was lost"
      // An environment-failure stop ends an executor, not the run: the successor continues.
      let afterEnv = decide successor clock scope [] { request 150.0 "environment-failure" with EnvironmentKind = "container-lost"; EnvironmentEvidence = [ "e" ]; EnvironmentAttempts = 2 }
      if not afterEnv.Permitted then yield "recovery: the documented environment failure was not permitted"
      if (resumePlan successor clock scope (t0.AddMinutes 160.0) false).Action <> "CONTINUE" then yield "recovery: the successor did not continue after the environment failure"
      if (resumePlan successor clock scope (t0.AddMinutes 420.0) false).Action <> "REPORT_AND_STOP" then yield "recovery: the successor did not stop at the deadline"
      if (resumePlan successor clock "" at false).Action <> "REPAIR_CHECKPOINT" then yield "recovery: a missing checkpoint was not refused"
      if (resumePlan successor clock "other-scope" at false).Refusals |> List.exists (fun r -> r.Code = "SCOPE_CHANGE_UNAPPROVED") |> not then yield "recovery: a changed scope was not refused"
      if (resumePlan (stateOf allDone) clock scope at false).Action <> "REQUEST_STOP_COMPLETE" then yield "recovery: a complete run was not sent to stop-decision"
      if (resumePlan (stateOf catalogDone) clock scope at true).Action <> "STOPPED" then yield "recovery: a recorded permitted stop was not respected" ]

// ---- the real scope, and Rehearsal 009 replayed against it ------------------------------------------------------

let private manifestPath (source: string) = Path.Combine(source, "docs", "rehearsal-010-readiness", "scope-manifest.json")

/// The package the Rehearsal 010 scope is compiled from: the requirements package the lock pins, so the scope checks
/// follow the lock rather than a second hard-coded copy. A lock without requirementsPackage.path is an error, not a
/// default: a silent fallback would compile the scope from a package the lock does not name.
let private scopePackage (source: string) =
    let lock = Path.Combine(source, "kickoff", "rehearsal-010", "toolchain.lock.proposed.json")
    parseFile lock
    |> field "requirementsPackage"
    |> Option.bind (strOpt "path")
    |> Option.defaultWith (fun () -> failwithf "%s names no requirementsPackage.path" lock)

let real (source: string) : string list =
    let s = ScopeCommands.sourcesOf source (scopePackage source)
    let committed = parseFile (manifestPath source)
    // The approved Rehearsal 010 scope is a historical record of the Legacy selector semantics (its v4.1.5 package).
    let semantics = recordedSemantics committed
    let m = compileWith semantics s
    let reqs = arr "requirements" m
    let ids = reqs |> List.map (str "id") |> Set.ofList
    let p0 = arr "requirements" s.Plan |> List.map (str "requirement")
    let inv = arr "requirements" s.Catalog |> List.map (str "id")
    [ if semantics <> Legacy then yield sprintf "real: the committed scope manifest selects %A selectors, not Legacy" semantics
      if str "scopeSha256" m <> str "scopeSha256" committed then yield sprintf "real: compiles to %s, the committed manifest has %s" (str "scopeSha256" m) (str "scopeSha256" committed)
      if not (preflight m committed).IsEmpty then yield sprintf "real: the committed scope manifest is stale or altered: %A" (preflight m committed |> List.truncate 5)
      // The ratified CAT-CAUSA-INVESTIGATION successor (ODR-R10-02 = A, APR-R10-OWNER-1) has 44 requirements: Rehearsal 009's
      // 38, unchanged, and INV-039..INV-044.
      if p0.Length <> 161 || inv.Length <> 44 then yield sprintf "real: %d P0 and %d catalog requirements (expected 161, 44)" p0.Length inv.Length
      for id in p0 @ inv do
          if not (ids.Contains id) then yield sprintf "real: approved requirement %s is not in the manifest" id
      for r in reqs do
          if (strs "workItems" r).IsEmpty then yield sprintf "real: %s has no work item" (str "id" r)
          for o in arr "obligations" r do
              if (strs "producers" o).IsEmpty then yield sprintf "real: %s has no producer" (str "id" o)
      let count k = field "counts" m |> Option.bind (intOpt k) |> Option.defaultValue -1L
      if count "planObligations" <> 613L || count "planTests" <> 660L || count "obligations" <> 890L || count "mandatoryRequirements" <> 200L then
          yield sprintf "real: counts %s" (field "counts" m |> Option.map serialize |> Option.defaultValue "")

      // Rehearsal 009 as it ended (evidence/FINAL-REPORT.md): every catalog requirement implemented and all but
      // INV-029 verified, INV-003 not applicable, no P0 requirement started, the release gate unable to run three
      // suites. It asked to stop at 02:25Z, 115 minutes after T0, for the reasons it gave.
      // Rehearsal 009 had the catalog's first 38 requirements; the successor's six were added after it ended (ODR-R10-02).
      let successorOnly = set [ "INV-039"; "INV-040"; "INV-041"; "INV-042"; "INV-043"; "INV-044" ]
      let catalogIds = inv |> List.filter (successorOnly.Contains >> not)
      let catalogObligations = reqs |> List.filter (fun r -> str "origin" r = "catalog" && List.contains (str "id" r) catalogIds && str "id" r <> "INV-029" && str "id" r <> "INV-003") |> List.collect (arr "obligations") |> List.map (str "id")
      let events =
          [ started "WI-INV-START" (catalogIds |> List.filter (fun i -> reqs |> List.exists (fun r -> str "id" r = i && List.contains "WI-INV-START" (strs "workItems" r))))
            started "WI-INV-WORKSPACE" (catalogIds |> List.filter (fun i -> reqs |> List.exists (fun r -> str "id" r = i && List.contains "WI-INV-WORKSPACE" (strs "workItems" r))))
            completed catalogIds ]
          @ (catalogObligations |> List.map passed)
          @ [ blocked "SU-OWN-DOTNET" "MSB1008"; blocked "SU-DOTNET-LIVE" "MSB1008"; blocked "SU-BROWSER-WEBKIT" "Project(s) webkit not found" ]
      match replay m events with
      | Error e -> yield sprintf "r9 replay: %s" e
      | Ok st ->
          let r9T0 = DateTimeOffset.Parse "2026-11-04T00:30:01Z"
          let r9Clock = { T0 = r9T0; Deadline = r9T0.AddMinutes 420.0 }
          let at = DateTimeOffset.Parse "2026-11-04T02:25:00Z"
          let q reason = { request 0.0 reason with At = at; ScopeSha256 = str "scopeSha256" m }
          let asked = [ decide st r9Clock (str "scopeSha256" m) [] (q "complete"); decide st r9Clock (str "scopeSha256" m) [] (q "release-gate-failed"); decide st r9Clock (str "scopeSha256" m) [] (q "conserve-compute") ]
          for d in asked do
              if d.Permitted then yield sprintf "r9 replay: the Rehearsal 009 stop was permitted (%A)" (codes d)
          // Against the ratified scope Rehearsal 009 did not even finish the catalog (INV-039..INV-044 are new), so its
          // 'complete' is refused as incomplete scope.
          if not (List.contains "SCOPE_INCOMPLETE" (codes asked.Head)) then yield sprintf "r9 replay: 'complete' not refused as incomplete scope: %A" (codes asked.Head)
          // The same claim with the whole ratified catalog done and no P0 requirement started is refused as catalog-only.
          let wholeCatalog = reqs |> List.filter (fun r -> str "origin" r = "catalog" && str "id" r <> "INV-029" && str "id" r <> "INV-003") |> List.collect (arr "obligations") |> List.map (str "id")
          let byItem (wi: string) = inv |> List.filter (fun i -> reqs |> List.exists (fun r -> str "id" r = i && List.contains wi (strs "workItems" r)))
          let catalogItems = reqs |> List.filter (fun r -> str "origin" r = "catalog") |> List.collect (strs "workItems") |> List.distinct
          match replay m ((catalogItems |> List.map (fun wi -> started wi (byItem wi))) @ [ completed inv ] @ (wholeCatalog |> List.map passed)) with
          | Error e -> yield sprintf "r9 replay (whole catalog): %s" e
          | Ok st2 ->
              let d = decide st2 r9Clock (str "scopeSha256" m) [] (q "complete")
              if d.Permitted then yield "r9 replay (whole catalog): 'complete' was permitted with no P0 requirement started"
              if not (List.contains "CATALOG_IS_NOT_SCOPE" (codes d)) then yield sprintf "r9 replay (whole catalog): 'complete' not refused as catalog-only: %A" (codes d)
          if asked.Head.MinutesRemaining <> 306 then yield sprintf "r9 replay: %d minutes remaining, expected 306" asked.Head.MinutesRemaining
          if asked.Head.OpenMandatory.Length < 161 then yield sprintf "r9 replay: only %d open mandatory requirements" asked.Head.OpenMandatory.Length
          if asked.Head.IndependentWorkItems.IsEmpty then yield "r9 replay: no independent work offered while the gate is blocked" ]

// ---- independent consistency check against #101's obligation inventory --------------------------------------------

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps when ps |> List.exists (fst >> (=) key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | JObj ps -> JObj(ps @ [ key, value ])
    | j -> j

let suiteRunners (source: string) : string list =
    let inputs v = Program.v41Inputs source (Path.Combine(source, "requirements-compiled", v)) (Path.Combine(source, "requirements-compiled", "v4")) None
    let x21 (i: V41Package.Inputs) = (V41Package.build i).Checks |> List.filter (fun c -> c.Id = "X21")
    let v414 = inputs "v4.1.4"
    let unrepaired = { v414 with Amendment = v414.Amendment |> Option.map (setIn "suiteRunners" (JArr [])) }
    let wrongSuite = { v414 with Amendment = v414.Amendment |> Option.map (setIn "suiteRunners" (JArr [ JObj [ "suite", JStr "SU-NOPE"; "runner", JStr "dotnet test Causa.slnx"; "decision", JStr "ODR-V414-01" ] ])) }
    let undecided = { v414 with Amendment = v414.Amendment |> Option.map (setIn "suiteRunners" (JArr [ JObj [ "suite", JStr "SU-OWN-DOTNET"; "runner", JStr "dotnet test Causa.slnx" ] ])) }
    [ match x21 v414 with
      | [ c ] when c.Problems.IsEmpty -> ()
      | cs -> yield sprintf "X21: v4.1.4 does not pass X21: %A" cs
      match x21 unrepaired with
      | [ c ] when c.Problems |> List.exists (fun p -> p.Contains "SU-OWN-DOTNET") && c.Problems |> List.exists (fun p -> p.Contains "SU-DOTNET-LIVE") -> ()
      | cs -> yield sprintf "X21 control: v4.1.2's runners were not refused: %A" cs
      if (x21 wrongSuite |> List.collect (fun c -> c.Problems)).IsEmpty then yield "X21 control: a runner for an unknown suite was accepted"
      if (x21 undecided |> List.collect (fun c -> c.Problems)).IsEmpty then yield "X21 control: a runner without an owner decision was accepted"
      // v4.1.2 is frozen: it compiles without X21, byte for byte as approved (the committed-package check covers the bytes).
      if not (x21 (inputs "v4.1.2")).IsEmpty then yield "X21: the frozen v4.1.2 gained a check" ]

/// Rehearsal 010 (Fix 2): central package management drops the F# SDK's implicit FSharp.Core reference, so no F#
/// assembly ships FSharp.Core.dll and xunit discovers no test. A successor package that pins bootstrap.fsharpCore gets
/// the central version and the conditional reference; the frozen packages bootstrap exactly as before.
let bootstrapFSharpCore (source: string) : string list =
    let inputs v = Program.v41Inputs source (Path.Combine(source, "requirements-compiled", v)) (Path.Combine(source, "requirements-compiled", "v4")) None
    let v415 = inputs "v4.1.5"
    let withCore (fc: Json) =
        { v415 with Amendment = v415.Amendment |> Option.map (fun a -> setIn "bootstrap" (setIn "fsharpCore" fc (field "bootstrap" a |> Option.defaultValue (JObj []))) a) }
    let pinned = withCore (JObj [ "version", JStr "10.1.402"; "decision", JStr "ODR-V41-07"; "approval", JStr "APR-TEST" ])
    let undecided = withCore (JObj [ "version", JStr "10.*"; "decision", JStr "ODR-NOPE" ])
    let boot (i: V41Package.Inputs) = V41Package.bootstrapFiles i (V41Package.ownedFiles i) "{}" |> Map.ofList
    let x19 (i: V41Package.Inputs) = (V41Package.build i).Checks |> List.filter (fun c -> c.Id = "X19") |> List.collect (fun c -> c.Problems)
    let reference = "<PackageReference Include=\"FSharp.Core\" />"
    let conditorProps = "<Project>\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n    <Nullable>enable</Nullable>\n    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n  </PropertyGroup>\n</Project>\n"
    [ for v in [ "v4.1.4"; "v4.1.5" ] do
          if boot (inputs v) |> Map.exists (fun _ text -> text.Contains "FSharp.Core") then yield sprintf "FSharp.Core: the frozen %s bootstrap changed (it names FSharp.Core)" v
      let b = boot pinned
      let get n = b.TryFind n |> Option.defaultValue ""
      if not ((get "Directory.Packages.props").Contains "<PackageVersion Include=\"FSharp.Core\" Version=\"10.1.402\" />") then yield "FSharp.Core: a pinned package's Directory.Packages.props has no central FSharp.Core version"
      if not ((get "Directory.Build.props").Contains reference && (get "Directory.Build.props").Contains "'$(MSBuildProjectExtension)' == '.fsproj' And '$(ManagePackageVersionsCentrally)' == 'true'") then
          yield "FSharp.Core: a pinned package's Directory.Build.props does not reference FSharp.Core for centrally managed F# projects"
      if b |> Map.exists (fun n text -> n.EndsWith ".fsproj" && text.Contains "FSharp.Core") then yield "FSharp.Core: a project file references FSharp.Core itself"
      if x19 pinned |> List.exists (fun p -> p.Contains "fsharpCore") then yield sprintf "FSharp.Core: X19 refuses a decided pin: %A" (x19 pinned)
      let refused = x19 undecided |> List.filter (fun p -> p.Contains "fsharpCore")
      if refused.Length < 3 then yield sprintf "FSharp.Core control: X19 does not refuse an inexact, undecided, unapproved pin: %A" refused
      // Layering onto Conditor's Directory.Build.props adds the reference once and keeps Conditor's properties.
      match V41Package.layerFile "Directory.Build.props" conditorProps (get "Directory.Build.props") with
      | Ok(Some merged) ->
          if not (merged.Contains reference && merged.Contains "<TargetFramework>net10.0</TargetFramework>" && merged.TrimEnd().EndsWith "</Project>") then yield sprintf "FSharp.Core: layering gave %s" merged
          if V41Package.layerFile "Directory.Build.props" merged (get "Directory.Build.props") <> Ok None then yield "FSharp.Core: layering twice adds the reference again"
      | other -> yield sprintf "FSharp.Core: layering onto Conditor's Directory.Build.props gave %A" other
      if V41Package.layerFile "Directory.Build.props" conditorProps ((boot v415).["Directory.Build.props"]) <> Ok None then yield "FSharp.Core: layering a frozen package's props is no longer a no-op" ]

/// Rehearsal 010 (Fix 10): Conditor's build-and-test.yml builds and tests App.slnx only. A successor package that sets
/// bootstrap.ciWorkflow gets an integration-owned workflow that builds and tests Causa.slnx; frozen packages do not.
let bootstrapCiWorkflow (source: string) : string list =
    let inputs v = Program.v41Inputs source (Path.Combine(source, "requirements-compiled", v)) (Path.Combine(source, "requirements-compiled", "v4")) None
    let v415 = inputs "v4.1.5"
    let withCi (path: string) = { v415 with Amendment = v415.Amendment |> Option.map (fun a -> setIn "bootstrap" (setIn "ciWorkflow" (JStr path) (field "bootstrap" a |> Option.defaultValue (JObj []))) a) }
    let boot (i: V41Package.Inputs) = V41Package.bootstrapFiles i (V41Package.ownedFiles i) "{}" |> Map.ofList
    let x19 (i: V41Package.Inputs) = (V41Package.build i).Checks |> List.filter (fun c -> c.Id = "X19") |> List.collect (fun c -> c.Problems)
    [ for v in [ "v4.1.4"; "v4.1.5" ] do
          if boot (inputs v) |> Map.exists (fun n _ -> n.StartsWith ".github/") then yield sprintf "CI: the frozen %s bootstrap writes a workflow" v
      let pinned = withCi ".github/workflows/ci.yml"
      match (boot pinned).TryFind ".github/workflows/ci.yml" with
      | None -> yield "CI: a package with bootstrap.ciWorkflow writes no workflow"
      | Some text ->
          for needed in [ "dotnet build Causa.slnx -c Release"; "dotnet test Causa.slnx -c Release --no-build"; "could not find dependent assembly"; "Skipped:"; "Owner: integration" ] do
              if not (text.Contains needed) then yield sprintf "CI: the bootstrapped workflow lacks '%s'" needed
          if text.Contains "build App.slnx" || text.Contains "test App.slnx" then yield "CI: the bootstrapped workflow builds or tests App.slnx"
      if x19 pinned |> List.exists (fun p -> p.Contains "ciWorkflow") then yield sprintf "CI: X19 refuses the integration-owned ci.yml: %A" (x19 pinned)
      if not (x19 (withCi ".github/workflows/elsewhere.yml") |> List.exists (fun p -> p.Contains "ciWorkflow")) then yield "CI control: X19 accepts a workflow that is not an integration-owned manifest file" ]

/// The owner's record against v4.1.4's corrected runners (RDP-003, FREEZE.md step 2), as a pure check over the records:
/// each runner names its decision and the operator approval whose latest record of that decision selects option A; its
/// content (every field but the approval) equals the runner of that suite in the amendment the owner decided on, so a
/// runner weakened or shadowed after the decision is refused; no suite has two runners; each decision applies to
/// exactly the runners that cite it (counted, not as sets); and the bootstrap a decision applies is unchanged.
/// `decided` gives the amendment at the commit that recorded an approval id; `proposed` is the amendment and the
/// requests as the commit that introduced the v4.1.4 requests wrote them, which is what the owner was asked about: the
/// requests must still equal it, and the runners and bootstrap the owner decided on must equal its own.
let v414OwnerRecordsWith (approvals: Json list) (requests: Json list) (amendment: Json) (proposed: Json * Json list) (decided: string -> Result<Json, string>) : string list =
    let proposedAmendment, proposedRequests = proposed
    let operator (a: Json) = (field "authority" a |> Option.bind (strOpt "kind")) = Some "operator"
    let changeOf (decision: string) (a: Json) = arr "changes" a |> List.tryFind (fun c -> strOpt "decision_id" c = Some decision)
    let latest (decision: string) = approvals |> List.filter operator |> List.filter (changeOf decision >> Option.isSome) |> List.tryLast
    let get k j = strOpt k j |> Option.defaultValue ""
    let content (j: Json) = match j with JObj ps -> ps |> List.filter (fun (k, _) -> k <> "approval") |> List.sortBy fst | _ -> []
    let runners = arr "suiteRunners" amendment
    let applies (r: Json) = field "applies" r |> Option.defaultValue (JObj [])
    let runnerOf (suite: string) (a: Json) = arr "suiteRunners" a |> List.filter (fun x -> get "suite" x = suite)
    [ if requests <> proposedRequests then yield "v4.1.4: the decision requests differ from those the owner was asked about (the commit that introduced them)"
      for suite, n in runners |> List.countBy (get "suite") do
          if n > 1 then yield sprintf "v4.1.4: %s has %d runners; a suite has one" suite n
      for r in runners do
          let suite, decision, approval = get "suite" r, get "decision" r, get "approval" r
          match latest decision with
          | None -> yield sprintf "v4.1.4: %s's runner applies %s, which the owner has not recorded" suite decision
          | Some a when get "id" a <> approval -> yield sprintf "v4.1.4: %s's runner names %s, but the owner's latest record of %s is %s" suite (if approval = "" then "no approval" else approval) decision (get "id" a)
          | Some a ->
              match changeOf decision a |> Option.bind (strOpt "option") with
              | Some "A" ->
                  match decided approval with
                  | Error e -> yield sprintf "v4.1.4: the amendment %s was recorded against cannot be read: %s" approval e
                  | Ok before ->
                      match runnerOf suite before with
                      | [ x ] when content x = content r && (runnerOf suite proposedAmendment |> List.map content) = [ content x ] -> ()
                      | [ x ] when content x = content r -> yield sprintf "v4.1.4: %s's runner as decided in %s is not the one the request proposed" suite approval
                      | [ _ ] -> yield sprintf "v4.1.4: %s's runner differs from the one the owner decided in %s" suite approval
                      | xs -> yield sprintf "v4.1.4: the amendment %s was recorded against has %d runners of %s" approval xs.Length suite
              | other -> yield sprintf "v4.1.4: %s records option %A of %s; the runners are option A's" approval other decision
      for q in requests do
          let id = get "id" q
          let suites = strs "suiteRunners" (applies q) |> List.sort
          let cited = runners |> List.filter (fun x -> get "decision" x = id) |> List.map (get "suite") |> List.sort
          if suites <> cited then yield sprintf "v4.1.4: %s applies %A, but the runners citing it are %A" id suites cited
          match strOpt "bootstrap" (applies q) with
          | None -> ()
          | Some config ->
              let current = field "bootstrap" amendment
              if (current |> Option.bind (strOpt "playwrightConfig")) <> Some config then
                  yield sprintf "v4.1.4: %s applies the bootstrap configuration %s, which the amendment does not write" id config
              match latest id with
              | Some a when changeOf id a |> Option.bind (strOpt "option") = Some "A" ->
                  match decided (get "id" a) with
                  | Ok before when field "bootstrap" before = current && field "bootstrap" proposedAmendment = current -> ()
                  | Ok _ -> yield sprintf "v4.1.4: the bootstrap differs from the one the owner decided in %s" (get "id" a)
                  | Error e -> yield sprintf "v4.1.4: the amendment %s was recorded against cannot be read: %s" (get "id" a) e
              | _ -> () ]

/// The real record: the committed approvals, requests and amendment; the amendment as it stood at the commit that
/// first recorded each approval id; and no commit since the v4.1.4 requests were introduced that changed the approvals
/// file unless the owner authored and committed it with no agent trailer. Any git error, and a shallow history, refuse.
let v414OwnerRecords (source: string) : string list =
    let approvalsPath = "kickoff/rehearsal-007/approvals.causa.json"
    let amendmentPath = "requirements-compiled/v4.1.4/analysis/amendment.json"
    let requestsPath = "requirements-compiled/v4.1.4/proposals/owner-decision-requests.json"
    let owner = "kemiller2002@gmail.com"
    let text (b: byte[]) = System.Text.Encoding.UTF8.GetString b
    let git args = Git.run source args
    let lines args = git args |> Result.map (fun b -> (text b).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray)
    let decided (approval: string) =
        lines [ "log"; "--format=%H"; "-S"; sprintf "\"%s\"" approval; "--"; approvalsPath ]
        |> Result.bind (function [] -> Error "no commit records it" | cs -> Ok (List.last cs))
        |> Result.bind (fun c -> git [ "show"; c + ":" + amendmentPath ] |> Result.map parseBytes)
    let introduced =
        match git [ "rev-parse"; "--is-shallow-repository" ] |> Result.map (text >> fun t -> t.Trim()) with
        | Ok "false" ->
            match lines [ "log"; "--diff-filter=A"; "--format=%H"; "--"; requestsPath ] with
            | Ok [] -> Error "the commit that introduced the v4.1.4 requests is not found"
            | Ok cs -> Ok (List.last cs)
            | Error e -> Error e
        | Ok _ -> Error "the history is shallow"
        | Error e -> Error e
    // A missing or unreadable blob is never equal to anything: a lookup error refuses (RV414-3-2).
    let blob (c: string) = git [ "rev-parse"; "--verify"; "--quiet"; c + ":" + approvalsPath ] |> Result.map (text >> fun t -> t.Trim()) |> Result.toOption |> Option.filter ((<>) "")
    let recordsAt (c: string) = git [ "show"; c + ":" + approvalsPath ] |> Result.map (parseBytes >> arr "approvals")
    // Every commit since the requests that changed the approvals file, merges included. A merge whose approvals file
    // equals one parent's added nothing to it (GitHub's own merges); any other must be the owner's, like a commit.
    let agentCommits =
        match introduced with
        | Error e -> [ e ]
        | Ok since ->
            match git [ "log"; "--full-history"; "--format=%H%n%P%n%ae%n%ce%n%B%x00"; since + "^..HEAD"; "--"; approvalsPath ] with
            | Error e -> [ e ]
            | Ok b ->
                (text b).Split('\000') |> List.ofArray |> List.map (fun m -> m.Trim()) |> List.filter ((<>) "")
                |> List.choose (fun m ->
                    let ls = m.Split('\n')
                    let item i = Array.tryItem i ls |> Option.defaultValue "?"
                    let parents = item 1 |> fun p -> p.Split(' ', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
                    let body = m.ToLowerInvariant()
                    let passThrough = parents.Length > 1 && parents |> List.exists (fun p -> blob p |> Option.isSome && blob p = blob ls[0])
                    let owners = ls.Length >= 4 && ls[2] = owner && ls[3] = owner && not (body.Contains "claude-session:" || body.Contains "co-authored-by: claude" || body.Contains "noreply@anthropic.com")
                    if passThrough || owners then None
                    else Some (sprintf "commit %s (author %s, committer %s)" ls[0] (item 2) (item 3)))
    // The approvals file is append-only (RV414-3-1): every record in any version of it since the requests (each commit
    // that changed it, each merge parent, and the version the requests were introduced on) is in HEAD's file
    // unchanged. A merge resolved to one side, or any rewrite, that drops or edits a record is refused whoever made it.
    let dropped =
        match introduced with
        | Error e -> [ e ]
        | Ok since ->
            match lines [ "log"; "--full-history"; "--format=%H %P"; since + "^..HEAD"; "--"; approvalsPath ] with
            | Error e -> [ e ]
            | Ok ls ->
                let versions = since :: (ls |> List.collect (fun l -> l.Split(' ', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray)) |> List.distinct
                match recordsAt "HEAD" with
                | Error e -> [ e ]
                | Ok head ->
                    versions
                    |> List.collect (fun c ->
                        match recordsAt c with
                        | Error _ when blob c |> Option.isNone -> []   // the file did not exist at c
                        | Error e -> [ sprintf "%s: %s" c e ]
                        | Ok rs -> rs |> List.filter (fun r -> not (List.contains r head)) |> List.map (fun r -> sprintf "%s (in %s)" (strOpt "id" r |> Option.defaultValue "?") c))
                    |> List.distinct
    let proposed =
        introduced
        |> Result.bind (fun c ->
            git [ "show"; c + ":" + amendmentPath ] |> Result.map parseBytes
            |> Result.bind (fun a -> git [ "show"; c + ":" + requestsPath ] |> Result.map (fun r -> a, parseBytes r |> arr "requests")))
    [ for c in agentCommits do yield sprintf "v4.1.4: the approvals file since the v4.1.4 requests is not the owner's alone: %s" c
      for d in dropped do yield sprintf "v4.1.4: the approvals file is append-only, but a record is missing or changed at HEAD: %s" d
      match proposed with
      | Error e -> yield sprintf "v4.1.4: the proposal the owner was asked about cannot be read: %s" e
      | Ok p ->
          yield! v414OwnerRecordsWith
                     (parseFile (Path.Combine(source, approvalsPath)) |> arr "approvals")
                     (parseFile (Path.Combine(source, requestsPath)) |> arr "requests")
                     (parseFile (Path.Combine(source, amendmentPath)))
                     p decided ]

/// The guard refuses what it exists for. Starting from the committed records, each mutation (a wrong or missing
/// approval, option B, a later record selecting B, a runner citing the wrong decision, a runner weakened after the
/// decision, a duplicate runner shadowing the approved one, an extra runner, a changed bootstrap) must be refused,
/// and the committed records must pass.
let v414OwnerRecordControls (source: string) : string list =
    let approvals = parseFile (Path.Combine(source, "kickoff/rehearsal-007/approvals.causa.json")) |> arr "approvals"
    let requests = parseFile (Path.Combine(source, "requirements-compiled/v4.1.4/proposals/owner-decision-requests.json")) |> arr "requests"
    let amendment = parseFile (Path.Combine(source, "requirements-compiled/v4.1.4/analysis/amendment.json"))
    let set (k: string) (v: Json) (j: Json) =
        match j with
        | JObj ps when ps |> List.exists (fst >> (=) k) -> JObj (ps |> List.map (fun (k', x) -> if k' = k then k', v else k', x))
        | JObj ps -> JObj (ps @ [ k, v ])
        | x -> x
    let unset (k: string) (j: Json) = match j with JObj ps -> JObj (ps |> List.filter (fst >> (<>) k)) | x -> x
    let runners = arr "suiteRunners" amendment
    let withRunners rs = set "suiteRunners" (JArr rs) amendment
    let onRunner (suite: string) (f: Json -> Json) = withRunners (runners |> List.map (fun r -> if strOpt "suite" r = Some suite then f r else r))
    let webkit = runners |> List.find (fun r -> strOpt "suite" r = Some "SU-BROWSER-WEBKIT")
    let decidedFixed (a: Json) (_: string) : Result<Json, string> = Ok a
    let record (option: string) = JObj [ "id", JStr "APR-V414-TEST"; "authority", JObj [ "kind", JStr "operator"; "id", JStr "kemiller2002" ]
                                         "changes", JArr [ JObj [ "decision_id", JStr "ODR-V414-02"; "action", JStr "select"; "option", JStr option ] ] ]
    let swapOption (a: Json) =
        if strOpt "id" a = Some "APR-V414-OWNER-1" then
            set "changes" (JArr (arr "changes" a |> List.map (fun c -> if strOpt "decision_id" c = Some "ODR-V414-02" then set "option" (JStr "B") c else c))) a
        else a
    let weakened = (strOpt "runner" webkit |> Option.defaultValue "").Replace(" tests/browser/invariants", "")
    let withoutBootstrap = requests |> List.map (fun q -> match field "applies" q with Some ap when strOpt "id" q = Some "ODR-V414-02" -> set "applies" (unset "bootstrap" ap) q | _ -> q)
    let cases =
        [ "wrong approval id", approvals, onRunner "SU-OWN-DOTNET" (set "approval" (JStr "APR-V414-OWNER-9")), amendment
          "missing approval", approvals, onRunner "SU-OWN-DOTNET" (unset "approval"), amendment
          "option B recorded", approvals |> List.map swapOption, amendment, amendment
          "a later record selects B", approvals @ [ record "B" ], amendment, amendment
          "runner cites the wrong decision", approvals, onRunner "SU-OWN-DOTNET" (set "decision" (JStr "ODR-V414-02")), amendment
          "runner weakened after the decision", approvals, onRunner "SU-BROWSER-WEBKIT" (set "runner" (JStr weakened)), amendment
          "duplicate runner shadows the approved one", approvals, withRunners (set "runner" (JStr weakened) webkit :: runners), amendment
          "extra runner", approvals, withRunners (runners @ [ set "suite" (JStr "SU-DOTNET-DETERMINISTIC") webkit ]), amendment
          "bootstrap changed after the decision", approvals, set "bootstrap" (JObj [ "playwrightConfig", JStr "playwright.config.ts" ]) amendment, amendment ]
    // Changes before the decision: the decided tree carries a runner or bootstrap other than the one proposed.
    let beforeCases =
        [ "runner changed between the request and the decision", onRunner "SU-BROWSER-WEBKIT" (set "runner" (JStr weakened))
          "bootstrap changed between the request and the decision", set "bootstrap" (JObj [ "playwrightConfig", JStr "playwright.config.ts" ]) amendment ]
    let check aps qs current before = v414OwnerRecordsWith aps qs current (amendment, requests) (decidedFixed before)
    [ match check approvals requests amendment amendment with
      | [] -> ()
      | ps -> yield sprintf "v4.1.4 owner-record control: the committed records fail: %A" ps
      if weakened = (strOpt "runner" webkit |> Option.defaultValue "") then yield "v4.1.4 owner-record control: the WebKit runner no longer runs tests/browser/invariants; update the mutation"
      for name, aps, current, before in cases do
          if check aps requests current before |> List.isEmpty then
              yield sprintf "v4.1.4 owner-record control: '%s' was not refused" name
      for name, decidedAndCurrent in beforeCases do
          if check approvals requests decidedAndCurrent decidedAndCurrent |> List.isEmpty then
              yield sprintf "v4.1.4 owner-record control: '%s' was not refused" name
      if check approvals withoutBootstrap amendment amendment |> List.isEmpty then
          yield "v4.1.4 owner-record control: 'a request edited after it was proposed (bootstrap dropped)' was not refused" ]

/// v4.1.5's owner records (FREEZE.md step 2), as a pure check. The owner's records carry no digests ("select A"), so what
/// the owner selected is the request as it stood when the record landed: `pinsAt approval` gives the requests file at the
/// parent of the commit that first recorded that approval id (independent review of #137). For each contract-bearing
/// decision (ODR-V415-01, ODR-V415-IF-VIEW) the owner's latest record must select A, and every pin of that decision's
/// `applies.A` as it stood then must hold now: each pinned contract's file digest and its contractChanges digest equal
/// the pin and the change names that approval; T-L03 and the SM-SAVE note equal their content digests and name that
/// approval; the project structure equals its pin. Every changed contract must be covered by some decision's pin, and
/// the scope decisions (ODR-V415-03, ODR-V415-04) must be recorded with option A.
let private v415Canonical (j: Json) : string =
    let quote (s: string) =
        let escape (c: char) =
            match c with
            | '"' -> "\\\""
            | '\\' -> "\\\\"
            | '\n' -> "\\n"
            | '\r' -> "\\r"
            | '\t' -> "\\t"
            | '\b' -> "\\b"
            | '\012' -> "\\f"
            | c when c < ' ' -> sprintf "\\u%04x" (int c)
            | c -> string c
        "\"" + String.Join("", s |> Seq.map escape) + "\""
    let rec go (j: Json) =
        match j with
        | JNull -> "null"
        | JBool b -> if b then "true" else "false"
        | JInt n -> string n
        | JNum d -> string d
        | JStr s -> quote s
        | JArr xs -> "[" + String.Join(",", xs |> List.map go) + "]"
        | JObj ps -> "{" + String.Join(",", ps |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b)) |> List.map (fun (k, v) -> quote k + ":" + go v)) + "}"
    go j

let v415EntryDigest (entry: Json) : string =
    let withoutApproval = match entry with JObj ps -> JObj (ps |> List.filter (fst >> (<>) "approval")) | x -> x
    Git.sha256Hex (Text.Encoding.UTF8.GetBytes(v415Canonical withoutApproval))

let v415OwnerRecordsWith (approvals: Json list) (amendment: Json) (machines: Json) (pinsAt: string -> Result<Json list, string>) (fileSha: string -> string) : string list =
    let get k j = strOpt k j |> Option.defaultValue ""
    // Only the owner's own operator record counts (review L2 on #143): kind operator and id kemiller2002.
    let operator (a: Json) =
        let authority = field "authority" a
        (authority |> Option.bind (strOpt "kind")) = Some "operator" && (authority |> Option.bind (strOpt "id")) = Some "kemiller2002"
    let changeOf (decision: string) (a: Json) = arr "changes" a |> List.tryFind (fun c -> strOpt "decision_id" c = Some decision)
    let latest (decision: string) = approvals |> List.filter operator |> List.filter (changeOf decision >> Option.isSome) |> List.tryLast
    let changes = arr "contractChanges" amendment
    let applied (decision: string) (approval: string) =
        pinsAt approval
        |> Result.bind (fun rs ->
            match rs |> List.tryFind (fun r -> get "id" r = decision) |> Option.bind (field "applies") |> Option.bind (field "A") with
            | Some a -> Ok a
            | None -> Error (sprintf "%s had no option A when %s was recorded" decision approval))
    let pinned =
        [ for decision in [ "ODR-V415-01"; "ODR-V415-IF-VIEW" ] do
              match latest decision with
              | None -> yield Error (sprintf "v4.1.5: %s is not recorded by the owner" decision)
              | Some a when changeOf decision a |> Option.bind (strOpt "option") <> Some "A" ->
                  yield Error (sprintf "v4.1.5: %s records %s other than option A" (get "id" a) decision)
              | Some a ->
                  match applied decision (get "id" a) with
                  | Error e -> yield Error (sprintf "v4.1.5: what %s selected cannot be read: %s" (get "id" a) e)
                  | Ok pins -> yield Ok (decision, get "id" a, pins) ]
    [ for p in pinned do
          match p with
          | Error e -> yield e
          | Ok (decision, approval, pins) ->
              for c in arr "contracts" pins do
                  let file, pin = get "file" c, get "sha256" c
                  if fileSha file <> pin then yield sprintf "v4.1.5: %s selected %s at %s, but the file is %s" approval file pin (fileSha file)
                  match changes |> List.filter (fun x -> get "file" x = file) with
                  | [ x ] when get "sha256" x <> pin -> yield sprintf "v4.1.5: %s selected %s at %s, but contractChanges declares %s" approval file pin (get "sha256" x)
                  | [ x ] when get "decision" x <> decision -> yield sprintf "v4.1.5: the change of %s names decision %s, but %s pins it" file (get "decision" x) decision
                  | [ x ] when get "approval" x <> approval -> yield sprintf "v4.1.5: the change of %s names %s, not the owner's record %s" file (if get "approval" x = "" then "no approval" else get "approval" x) approval
                  | [ _ ] -> ()
                  | xs -> yield sprintf "v4.1.5: contractChanges declares %s %d times" file xs.Length
              let check (kind: string) (entries: Json list) (matches: Json -> Json -> bool) =
                  [ for pinEntry in arr kind pins do
                        match entries |> List.filter (matches pinEntry) with
                        | [ e ] when v415EntryDigest e <> get "sha256" pinEntry -> yield sprintf "v4.1.5: the %s entry %s differs from what %s selected" kind (v415Canonical pinEntry) approval
                        | [ e ] when get "approval" e <> approval -> yield sprintf "v4.1.5: the %s entry %s names %s, not the owner's record %s" kind (get "machine" e) (if get "approval" e = "" then "no approval" else get "approval" e) approval
                        | [ _ ] -> ()
                        | xs -> yield sprintf "v4.1.5: %d %s entries match %s" xs.Length kind (v415Canonical pinEntry) ]
              yield! check "transitions" (arr "transitions" machines) (fun pin e -> get "machine" e = get "machine" pin && get "id" e = get "id" pin)
              yield! check "accepted" (arr "accepted" machines) (fun pin e -> get "machine" e = get "machine" pin && get "decision" e = get "decision" pin)
              match field "projectStructure" pins with
              | Some ps when fileSha (get "file" ps) <> get "sha256" ps -> yield sprintf "v4.1.5: %s selected %s at %s, but the file is %s" approval (get "file" ps) (get "sha256" ps) (fileSha (get "file" ps))
              | _ -> ()
      let covered = pinned |> List.collect (function Ok (_, _, pins) -> arr "contracts" pins |> List.map (get "file") | Error _ -> [])
      for x in changes do
          if not (List.contains (get "file" x) covered) then yield sprintf "v4.1.5: contractChanges declares %s, which no recorded selection pins" (get "file" x)
      for decision in [ "ODR-V415-03"; "ODR-V415-04" ] do
          match latest decision with
          | Some a when changeOf decision a |> Option.bind (strOpt "option") = Some "A" -> ()
          | Some a -> yield sprintf "v4.1.5: %s records %s other than option A" (get "id" a) decision
          | None -> yield sprintf "v4.1.5: %s is not recorded by the owner" decision ]

/// The requests the owner selected with `approval`: the requests file at the parent of the commit that first recorded
/// that approval id (review caveat on #137: the owner's records carry no digests).
let v415DecidedRequests (source: string) (approval: string) : Result<Json list, string> =
    let approvalsPath = "kickoff/rehearsal-007/approvals.causa.json"
    let text (b: byte[]) = System.Text.Encoding.UTF8.GetString b
    let git args = Git.run source args
    git [ "log"; "--format=%H"; "-S"; sprintf "\"%s\"" approval; "--"; approvalsPath ]
    |> Result.map (fun b -> (text b).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray)
    |> Result.bind (function [] -> Error "no commit records it" | cs -> Ok (List.last cs))
    |> Result.bind (fun c -> git [ "show"; c + "^:requirements-compiled/v4.1.5/proposals/owner-decision-requests.json" ] |> Result.map (parseBytes >> arr "requests"))

/// Every commit since the v4.1.5 requests were introduced that changed the approvals file is the owner's (author and
/// committer kemiller2002@gmail.com, no agent trailer), except a merge whose approvals file equals a parent's; and the
/// file is append-only since then (review L2/B2 on #143; the same rules as the v4.1.4 guard, for this package's span).
let v415ApprovalsHistory (source: string) : string list =
    let approvalsPath = "kickoff/rehearsal-007/approvals.causa.json"
    let requestsPath = "requirements-compiled/v4.1.5/proposals/owner-decision-requests.json"
    let owner = "kemiller2002@gmail.com"
    let text (b: byte[]) = System.Text.Encoding.UTF8.GetString b
    let git args = Git.run source args
    let lines args = git args |> Result.map (fun b -> (text b).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray)
    let blob (c: string) = git [ "rev-parse"; "--verify"; "--quiet"; c + ":" + approvalsPath ] |> Result.map (text >> fun t -> t.Trim()) |> Result.toOption |> Option.filter ((<>) "")
    let recordsAt (c: string) = git [ "show"; c + ":" + approvalsPath ] |> Result.map (parseBytes >> arr "approvals")
    let introduced =
        match git [ "rev-parse"; "--is-shallow-repository" ] |> Result.map (text >> fun t -> t.Trim()) with
        | Ok "false" ->
            match lines [ "log"; "--diff-filter=A"; "--format=%H"; "--"; requestsPath ] with
            | Ok [] -> Error "the commit that introduced the v4.1.5 requests is not found"
            | Ok cs -> Ok (List.last cs)
            | Error e -> Error e
        | Ok _ -> Error "the history is shallow"
        | Error e -> Error e
    match introduced with
    | Error e -> [ sprintf "v4.1.5: %s" e ]
    | Ok since ->
        let agentCommits =
            match git [ "log"; "--full-history"; "--format=%H%n%P%n%ae%n%ce%n%B%x00"; since + "^..HEAD"; "--"; approvalsPath ] with
            | Error e -> [ e ]
            | Ok b ->
                (text b).Split('\000') |> List.ofArray |> List.map (fun m -> m.Trim()) |> List.filter ((<>) "")
                |> List.choose (fun m ->
                    let ls = m.Split('\n')
                    let item i = Array.tryItem i ls |> Option.defaultValue "?"
                    let parents = item 1 |> fun p -> p.Split(' ', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
                    let body = m.ToLowerInvariant()
                    let passThrough = parents.Length > 1 && parents |> List.exists (fun p -> blob p |> Option.isSome && blob p = blob ls[0])
                    let owners = ls.Length >= 4 && ls[2] = owner && ls[3] = owner && not (body.Contains "claude-session:" || body.Contains "co-authored-by: claude" || body.Contains "noreply@anthropic.com")
                    if passThrough || owners then None else Some (sprintf "commit %s (author %s, committer %s)" ls[0] (item 2) (item 3)))
        let dropped =
            match lines [ "log"; "--full-history"; "--format=%H %P"; since + "^..HEAD"; "--"; approvalsPath ], recordsAt "HEAD" with
            | Error e, _ | _, Error e -> [ e ]
            | Ok ls, Ok head ->
                since :: (ls |> List.collect (fun l -> l.Split(' ', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray)) |> List.distinct
                |> List.collect (fun c ->
                    match recordsAt c with
                    | Error _ when blob c |> Option.isNone -> []
                    | Error e -> [ sprintf "%s: %s" c e ]
                    | Ok rs -> rs |> List.filter (fun r -> not (List.contains r head)) |> List.map (fun r -> sprintf "%s (in %s)" (strOpt "id" r |> Option.defaultValue "?") c))
                |> List.distinct
        [ for c in agentCommits do yield sprintf "v4.1.5: the approvals file since the v4.1.5 requests is not the owner's alone: %s" c
          for d in dropped do yield sprintf "v4.1.5: the approvals file is append-only, but a record is missing or changed at HEAD: %s" d ]

/// The real v4.1.5 record: the committed approvals, amendment and state-machine amendments; the requests each record
/// selected (v415DecidedRequests); the package files' digests on disk; and the approvals file's history.
let v415OwnerRecords (source: string) : string list =
    let approvalsPath = "kickoff/rehearsal-007/approvals.causa.json"
    let package = "requirements-compiled/v4.1.5"
    let fileSha (path: string) =
        let full = Path.Combine(source, package, path)
        if File.Exists full then Git.sha256Hex (File.ReadAllBytes full) else "<missing>"
    match v415ApprovalsHistory source with
    | [] ->
        v415OwnerRecordsWith
            (parseFile (Path.Combine(source, approvalsPath)) |> arr "approvals")
            (parseFile (Path.Combine(source, package, "analysis/amendment.json")))
            (parseFile (Path.Combine(source, package, "analysis/state-machine-amendments.json")))
            (v415DecidedRequests source) fileSha
    | problems -> problems

/// The v4.1.5 guard refuses what it exists for. Starting from the committed records, each mutation must be refused:
/// a pin edited after the decision (the requests now differ from what was selected, but the package follows the edit),
/// a contract changed after the decision, a wrong approval on a change, T-L03 or the note changed, option B, an
/// unrecorded decision, and an unpinned contract change. The committed records must pass.
let v415OwnerRecordControls (source: string) : string list =
    let package = "requirements-compiled/v4.1.5"
    let approvals = parseFile (Path.Combine(source, "kickoff/rehearsal-007/approvals.causa.json")) |> arr "approvals"
    let amendment = parseFile (Path.Combine(source, package, "analysis/amendment.json"))
    let machines = parseFile (Path.Combine(source, package, "analysis/state-machine-amendments.json"))
    let requests = parseFile (Path.Combine(source, package, "proposals/owner-decision-requests.json")) |> arr "requests"
    let fileSha (path: string) =
        let full = Path.Combine(source, package, path)
        if File.Exists full then Git.sha256Hex (File.ReadAllBytes full) else "<missing>"
    let set (k: string) (v: Json) (j: Json) =
        match j with
        | JObj ps when ps |> List.exists (fst >> (=) k) -> JObj (ps |> List.map (fun (k', x) -> if k' = k then k', v else k', x))
        | JObj ps -> JObj (ps @ [ k, v ])
        | x -> x
    let onList (k: string) (pick: Json -> bool) (f: Json -> Json) (j: Json) = set k (JArr (arr k j |> List.map (fun x -> if pick x then f x else x))) j
    let file07 = "contracts/07-IF41-VIEW.fs"
    let isFile f x = strOpt "file" x = Some f
    let fakeSha = String('0', 64)
    // As decided: what each record selected, read from git at the parent of its commit (review L1 on #143), so every
    // case below is judged against the older requests, never HEAD's.
    let asDecided = v415DecidedRequests source
    // A pin edited after the decision: the package and the current requests follow the edit, the decided requests do not.
    let editedPin = amendment |> onList "contractChanges" (isFile file07) (set "sha256" (JStr fakeSha))
    let editedFile f = if f = file07 then fakeSha else fileSha f
    let swap (decision: string) (option: string) (a: Json) =
        set "changes" (JArr (arr "changes" a |> List.map (fun c -> if strOpt "decision_id" c = Some decision then set "option" (JStr option) c else c))) a
    let withoutRecord (decision: string) = approvals |> List.filter (fun a -> not (arr "changes" a |> List.exists (fun c -> strOpt "decision_id" c = Some decision)))
    let cases =
        [ "a pin edited after the decision (contract and contractChanges follow it)", approvals, editedPin, machines, editedFile
          "a contract changed after the decision", approvals, amendment, machines, editedFile
          "a change naming the wrong approval", approvals, amendment |> onList "contractChanges" (isFile "contracts/00-DOMAIN-STUBS.fs") (set "approval" (JStr "APR-V415-OWNER-9")), machines, fileSha
          "T-L03 changed after the decision", approvals, amendment, machines |> onList "transitions" (fun t -> strOpt "id" t = Some "T-L03") (set "guard" (JStr "always")), fileSha
          "the SM-SAVE note changed after the decision", approvals, amendment, machines |> onList "accepted" (fun a -> strOpt "decision" a = Some "ODR-V415-01") (set "note" (JStr "changed")), fileSha
          "ODR-V415-01 recorded as option B", approvals |> List.map (swap "ODR-V415-01" "B"), amendment, machines, fileSha
          "ODR-V415-IF-VIEW not recorded", withoutRecord "ODR-V415-IF-VIEW", amendment, machines, fileSha
          "ODR-V415-03 not recorded", withoutRecord "ODR-V415-03", amendment, machines, fileSha
          "07 relabelled to ODR-V415-01 (decision mismatch)", approvals, amendment |> onList "contractChanges" (isFile file07) (set "decision" (JStr "ODR-V415-01")), machines, fileSha
          "the record's authority is not the owner", approvals |> List.map (fun a -> if strOpt "id" a = Some "APR-V415-OWNER-2" then set "authority" (JObj [ "kind", JStr "operator"; "id", JStr "mallory" ]) a else a), amendment, machines, fileSha
          "an unpinned contract change", approvals, set "contractChanges" (JArr (arr "contractChanges" amendment @ [ JObj [ "file", JStr "contracts/08-IF41-REPORT.fs"; "sha256", JStr fakeSha; "decision", JStr "ODR-V415-01"; "approval", JStr "APR-V415-OWNER-1" ] ])) amendment, machines, fileSha ]
    [ match v415OwnerRecordsWith approvals amendment machines asDecided fileSha with
      | [] -> ()
      | ps -> yield sprintf "v4.1.5 owner-record control: the committed records fail: %A" ps
      for name, aps, a, m, sha in cases do
          if v415OwnerRecordsWith aps a m asDecided sha |> List.isEmpty then yield sprintf "v4.1.5 owner-record control: '%s' was not refused" name
      // The lookup reads the older requests: ODR-V415-IF-VIEW did not exist when APR-V415-OWNER-1 was recorded, but does
      // at HEAD and when APR-V415-OWNER-2 was recorded. A lookup that read HEAD would fail this.
      let has (decision: string) (rs: Json list) = rs |> List.exists (fun r -> strOpt "id" r = Some decision)
      match asDecided "APR-V415-OWNER-1", asDecided "APR-V415-OWNER-2" with
      | Ok first, Ok second when not (has "ODR-V415-IF-VIEW" first) && has "ODR-V415-01" first && has "ODR-V415-IF-VIEW" second && has "ODR-V415-IF-VIEW" requests -> ()
      | Ok _, Ok _ -> yield "v4.1.5 owner-record control: the decided requests are not read at the parent of each record's commit"
      | Error e, _ | _, Error e -> yield sprintf "v4.1.5 owner-record control: the decided requests cannot be read: %s" e
      // HEAD's requests, the contract and contractChanges re-pinned together after the decision: the check judges against
      // the decided requests, so it refuses although HEAD's requests agree with the edit.
      let headRepinned = requests |> List.map (fun q -> if strOpt "id" q = Some "ODR-V415-IF-VIEW" then (match field "applies" q |> Option.bind (field "A") with Some a -> set "applies" (JObj [ "A", onList "contracts" (isFile file07) (set "sha256" (JStr fakeSha)) a ]) q | None -> q) else q)
      if v415OwnerRecordsWith approvals editedPin machines (fun _ -> Ok headRepinned) editedFile |> List.isEmpty |> not then
          yield "v4.1.5 owner-record control: a re-pin consistent with HEAD's requests is refused even when judged against them (the fixture is wrong)"
      if v415OwnerRecordsWith approvals editedPin machines asDecided editedFile |> List.isEmpty then
          yield "v4.1.5 owner-record control: 'HEAD requests, contract and contractChanges re-pinned together after the decision' was not refused" ]

/// The committed v4.1.4 package is what the compiler produces from its inputs (`v41 --check`).
let committedPackage (source: string) : string list =
    let code =
        Program.v41Command [ "--source"; source; "--package"; Path.Combine(source, "requirements-compiled", "v4.1.4"); "--base"; Path.Combine(source, "requirements-compiled", "v4"); "--check" ]
    if code = 0 then [] else [ sprintf "v4.1.4: v41 --check exited %d (the committed package differs from its compilation)" code ]

/// docs/release-gate-repair/obligation-inventory.json reconciles the 613 plan obligations with Rehearsal 009's gate.
/// The scope manifest is derived independently; both must agree on every obligation, its requirement and its
/// evidence kind (record-only, or executable/mixed), and the inventory must exclude nothing.
let reconciliation (source: string) : string list =
    let inventory = parseFile (Path.Combine(source, "docs", "release-gate-repair", "obligation-inventory.json"))
    let manifest = compile (ScopeCommands.sourcesOf source (scopePackage source))
    let fromManifest =
        arr "requirements" manifest
        |> List.filter (fun r -> str "origin" r = "v4-plan")
        |> List.collect (fun r -> arr "obligations" r |> List.map (fun o -> str "id" o, (str "id" r, str "kind" o)))
        |> Map.ofList
    let fromInventory =
        arr "obligations" inventory
        |> List.map (fun o -> str "obligation" o, (str "requirement" o, (if str "evidenceType" o = "record" then "record" else "executable")))
        |> Map.ofList
    let keys (m: Map<string, _>) = m |> Map.toList |> List.map fst |> Set.ofList
    let summary k = field "summary" inventory |> Option.bind (intOpt k) |> Option.defaultValue -1L
    let count kind = fromManifest |> Map.filter (fun _ (_, k) -> k = kind) |> Map.count |> int64
    [ for o in Set.difference (keys fromManifest) (keys fromInventory) do yield sprintf "reconciliation: %s is in the scope manifest, not in the inventory" o
      for o in Set.difference (keys fromInventory) (keys fromManifest) do yield sprintf "reconciliation: %s is in the inventory, not in the scope manifest" o
      for KeyValue(o, v) in fromManifest do
          match fromInventory.TryFind o with
          | Some w when w <> v -> yield sprintf "reconciliation: %s is %A in the scope manifest and %A in the inventory" o v w
          | _ -> ()
      if summary "recordOnly" <> count "record" then yield sprintf "reconciliation: %d record-only obligations in the inventory, %d in the manifest" (summary "recordOnly") (count "record")
      if summary "executable" <> count "executable" then yield sprintf "reconciliation: %d executable obligations in the inventory, %d in the manifest" (summary "executable") (count "executable")
      if summary "approvedExclusions" <> 0L then yield "reconciliation: the inventory records an exclusion the scope manifest does not"
      if arr "obligations" inventory |> List.exists (fun o -> strOpt "scopeStatus" o <> Some "in-scope") then yield "reconciliation: the inventory has an obligation out of scope" ]

// ---- work-item split: a governed successor scope ---------------------------------------------------------------------

/// The proposed split plan compiles to a successor scope that keeps every requirement, obligation, priority and
/// mandatory flag of the approved scope, the approved scope still compiles unchanged, and every mutation that would
/// drop, duplicate, move or reorder work is refused with its code.
let splitScope (source: string) : string list =
    let planPath = "docs/rehearsal-010-optimization/inputs/work-item-split.proposed.json"
    let plan = parseFile (Path.Combine(source, planPath))
    let sources = ScopeCommands.sourcesOf source (scopePackage source)
    // The plan splits generated work items of the approved Rehearsal 010 scope, a Legacy-semantics record: it and its
    // controls are judged with the semantics that scope records (the committed successor records the same package).
    let semantics = recordedSemantics (parseFile (manifestPath source))
    let successorSemantics = recordedSemantics (parseFile (Path.Combine(source, "docs/rehearsal-010-optimization/proposals/scope-manifest.successor.json")))
    let compile = compileWith semantics
    let compileWithSplit = compileWithSplitWith semantics
    let approved = compile sources
    let content (m: Json) = arr "requirements" m |> List.map (fun r -> str "id" r, serialize (JObj [ "o", (field "obligations" r |> Option.defaultValue (JArr [])); "p", JStr(strOpt "priority" r |> Option.defaultValue ""); "m", (field "mandatory" r |> Option.defaultValue JNull) ])) |> Map.ofList
    let mapSplits (f: Json -> Json) (j: Json) =
        match j with
        | JObj ps -> JObj(ps |> List.map (fun (k, v) -> if k = "splits" then k, (match v with JArr xs -> JArr(List.map f xs) | x -> x) else k, v))
        | x -> x
    let setField (k: string) (v: Json) (j: Json) = match j with JObj ps -> JObj(ps |> List.map (fun (k', x) -> if k' = k then k', v else k', x)) | x -> x
    let onParts (parent: string) (f: Json list -> Json list) =
        plan |> mapSplits (fun sp -> if str "workItem" sp = parent then setField "parts" (JArr(f (arr "parts" sp))) sp else sp)
    let onPart (parent: string) (part: string) (f: Json -> Json) = onParts parent (List.map (fun p -> if str "id" p = part then f p else p))
    let reqs (xs: string list) = JArr(xs |> List.map JStr)
    let refusedWith (code: string) (mutant: Json) =
        match compileWithSplit sources mutant planPath "test" with
        | Error ps when ps |> List.exists (fun p -> p.StartsWith code) -> None
        | Error ps -> Some(sprintf "split control: expected %s, got %A" code (List.truncate 3 ps))
        | Ok _ -> Some(sprintf "split control: a plan that should be refused with %s compiled" code)
    let mutants =
        [ "SPLIT_REQUIREMENT_DROPPED", onPart "WI-INV-WORKSPACE" "WI-INV-WORKSPACE-ROUNDTRIP" (setField "requirements" (reqs [ "INV-017" ]))
          "SPLIT_REQUIREMENT_DUPLICATED", onPart "WI-INV-WORKSPACE" "WI-INV-WORKSPACE-SAVE" (setField "requirements" (reqs [ "INV-015"; "INV-016"; "INV-043"; "INV-017" ]))
          "SPLIT_FOREIGN_REQUIREMENT", onPart "WI-INV-START" "WI-INV-START-ACTIONS" (setField "requirements" (reqs [ "INV-004"; "INV-022"; "INV-039" ]))
          "SPLIT_FROZEN_TASK", plan |> mapSplits (fun sp -> if str "workItem" sp = "WI-GEN-PF" then setField "workItem" (JStr "T19") sp else sp)
          "SPLIT_CYCLE", onPart "WI-INV-WORKSPACE" "WI-INV-WORKSPACE-FRAME" (setField "dependsOn" (reqs [ "WI-INV-WORKSPACE-ROUNDTRIP" ]))
          "SPLIT_BREAKS_REQUIREMENT_DEPENDENCY", onPart "WI-INV-WORKSPACE" "WI-INV-WORKSPACE-DETAILS" (setField "dependsOn" (reqs [ "WI-INV-WORKSPACE-FRAME" ]))
          "SPLIT_UNKNOWN_DEPENDENCY", onPart "WI-GEN-PF" "WI-GEN-PF-SECURITY" (setField "dependsOn" (reqs [ "WI-NOPE" ]))
          "SPLIT_ID_TAKEN", onPart "WI-GEN-UI" "WI-GEN-UI-VERIFY" (setField "id" (JStr "T31"))
          "SPLIT_EMPTY_PART", onParts "WI-GEN-PF" (fun ps -> ps @ [ JObj [ "id", JStr "WI-GEN-PF-EMPTY"; "dependsOn", JArr []; "requirements", JArr [] ] ])
          // review RVO-1-1: judged against the work items that exist after the split
          "SPLIT_DROPS_PARENT_DEPENDENCY", onPart "WI-GEN-PF" "WI-GEN-PF-BOUNDARY" (setField "dependsOn" (JArr []))
          "SPLIT_DEPENDS_ON_REPLACED", onPart "WI-GEN-PF" "WI-GEN-PF-SECURITY" (setField "dependsOn" (reqs [ "WI-GEN-UI" ]))
          "SPLIT_OWNER_CHANGED", onPart "WI-GEN-PF" "WI-GEN-PF-SECURITY" (fun p -> match p with JObj ps -> JObj(ps @ [ "owner", JStr "nobody" ]) | x -> x)
          "SPLIT_DUPLICATE_PART_ID", onPart "WI-GEN-PF" "WI-GEN-PF-SECURITY" (setField "id" (JStr "WI-GEN-UI-VERIFY"))
          // review RVO-2-1: a cycle across two splits (LANDING -> PF-SECURITY -> UI-VERIFY -> ... -> LANDING)
          "SPLIT_CYCLE", (onPart "WI-GEN-PF" "WI-GEN-PF-SECURITY" (setField "dependsOn" (reqs [ "WI-GEN-PF-BOUNDARY"; "WI-GEN-UI-VERIFY" ]))
                          |> fun pl -> pl |> mapSplits (fun sp -> if str "workItem" sp = "WI-GEN-UI" then setField "parts" (JArr(arr "parts" sp |> List.map (fun p -> if str "id" p = "WI-GEN-UI-LANDING" then setField "dependsOn" (reqs [ "T17"; "WI-GEN-PF-SECURITY" ]) p else p))) sp else sp)) ]
    [ if semantics <> Legacy || successorSemantics <> Legacy then yield sprintf "split: the approved scope and its successor select %A and %A selectors, not Legacy" semantics successorSemantics
      match compileWithSplit sources plan planPath "test" with
      | Error ps -> yield sprintf "split: the proposed plan is refused: %A" (List.truncate 3 ps)
      | Ok successor ->
          if content successor <> content approved then yield "split: the successor's requirements, obligations, priorities or mandatory flags differ from the approved scope"
          let count k (m: Json) = field "counts" m |> Option.bind (intOpt k) |> Option.defaultValue -1L
          for k in [ "requirements"; "obligations"; "mandatoryObligations"; "mandatoryRequirements"; "planTests" ] do
              if count k successor <> count k approved then yield sprintf "split: count %s differs (%d, approved %d)" k (count k successor) (count k approved)
          if str "scopeSha256" successor = str "scopeSha256" approved then yield "split: the successor has the approved scopeSha256; a successor must be approved on its own digest"
          let wis = arr "workItems" successor |> List.map (str "id") |> Set.ofList
          for r in arr "requirements" successor do
              for w in strs "workItems" r do
                  if not (wis.Contains w) then yield sprintf "split: %s names work item %s, which the successor does not have" (str "id" r) w
      if str "scopeSha256" (compile sources) <> str "scopeSha256" (parseFile (Path.Combine(source, "docs", "rehearsal-010-readiness", "scope-manifest.json"))) then
          yield "split: the approved scope no longer compiles to the committed manifest's scopeSha256"
      for code, mutant in mutants do
          match refusedWith code mutant with Some p -> yield p | None -> ()
      // review RVO-1-2: preflight compares the work items, so a successor edited after compiling is refused
      match compileWithSplit sources plan planPath "test" with
      | Ok successor ->
          let editItem (f: Json -> Json) (m: Json) =
              match m with
              | JObj ps -> JObj(ps |> List.map (fun (k, v) -> if k = "workItems" then k, (match v with JArr xs -> JArr(xs |> List.map (fun w -> if str "id" w = "WI-GEN-PF-SECURITY" then f w else w)) | x -> x) else k, v)) |> withScopeHash
              | x -> x
          let strip (m: Json) = match m with JObj ps -> JObj(ps |> List.filter (fun (k, _) -> k <> "scopeSha256")) | x -> x
          for name, forged in [ "requirements emptied", editItem (setField "requirements" (JArr [])) (strip successor)
                                "dependency dropped", editItem (setField "dependsOn" (JArr [])) (strip successor)
                                "owner changed", editItem (setField "owner" (JStr "nobody")) (strip successor) ] do
              if not (preflight successor forged |> List.exists (fun r -> r.Code = "WORK_ITEM_ALTERED")) then
                  yield sprintf "split control: a successor with a work item's %s passes preflight" name
          if not (preflight successor successor).IsEmpty then yield sprintf "split: the successor fails its own preflight: %A" (preflight successor successor |> List.truncate 3)
      | Error _ -> () ]

/// The milestone-aligned refinement (TIME-TO-WORKING-APPLICATION.md, R5) compiles, keeps the approved requirements,
/// obligations, priorities and mandatory flags, and reproduces the committed proposal's digest.
let milestoneAlignedSplit (source: string) : string list =
    let planPath = "docs/rehearsal-010-optimization/inputs/work-item-split.milestone-aligned.proposed.json"
    let sources = ScopeCommands.sourcesOf source (scopePackage source)
    let content (m: Json) = arr "requirements" m |> List.map (fun r -> str "id" r, serialize (JObj [ "o", (field "obligations" r |> Option.defaultValue (JArr [])); "p", JStr(strOpt "priority" r |> Option.defaultValue ""); "m", (field "mandatory" r |> Option.defaultValue JNull) ])) |> Map.ofList
    let committed = parseFile (Path.Combine(source, "docs/rehearsal-010-optimization/proposals/scope-manifest.milestone-aligned.json"))
    let planSha = Git.sha256Hex (File.ReadAllBytes(Path.Combine(source, planPath)))
    // A historical Rehearsal 010 proposal: judged with the selector semantics it records (Legacy, v4.1.5).
    let semantics = recordedSemantics committed
    let compile = compileWith semantics
    let compileWithSplit = compileWithSplitWith semantics
    [ if semantics <> Legacy then yield sprintf "milestone-aligned split: the committed proposal selects %A selectors, not Legacy" semantics
      match compileWithSplit sources (parseFile (Path.Combine(source, planPath))) planPath planSha with
      | Error ps -> yield sprintf "milestone-aligned split: refused: %A" (List.truncate 3 ps)
      | Ok m ->
          if content m <> content (compile sources) then yield "milestone-aligned split: requirements, obligations, priorities or mandatory flags differ from the approved scope"
          if str "scopeSha256" m <> str "scopeSha256" committed then yield sprintf "milestone-aligned split: compiles to %s, the committed proposal has %s" (str "scopeSha256" m) (str "scopeSha256" committed)
          if Option.map (str "planSha256") (field "workItemSplit" committed) <> Some planSha then yield "milestone-aligned split: the committed proposal records a different plan digest"
          if not (preflight m committed).IsEmpty then yield sprintf "milestone-aligned split: the committed proposal fails preflight: %A" (preflight m committed |> List.truncate 3) ]

/// CF-07 option A (owner decision ODR-R10-33 A): the slice gate does not judge a bound assertion while a slice it needs
/// is not integrated; with no needs map every bound assertion is judged, as before. Built on the approved manifest.
let cf07Deferral (source: string) : string list =
    let manifest = parseFile (Path.Combine(source, "docs/rehearsal-010-readiness/scope-manifest.json"))
    let s = match Ledger.replay manifest [] with Ok s -> s | Error why -> failwithf "replay: %s" why
    let slice (assertions: string list) = JObj [ "id", JStr "SL-WI-INV-START"; "dependsOn", JArr []; "governedAssertions", JArr(assertions |> List.map JStr) ]
    // WI-INV-LIST is integrated by its judged obligations alone (none record-only, none live-only in a non-live slice)
    let listObligations =
        let rs = arr "workItems" manifest |> List.find (fun w -> str "id" w = "WI-INV-LIST") |> strs "requirements" |> Set.ofList
        arr "requirements" manifest |> List.filter (fun r -> rs.Contains(str "id" r))
        |> List.collect (fun r -> arr "obligations" r |> List.filter (fun o -> boolOr "mandatory" true o && strOpt "kind" o <> Some "record" && not (strs "producers" o |> List.exists (fun p -> Ledger.liveProducers.Contains p || p = "SU-BROWSER-WEBKIT"))) |> List.map (str "id"))
    let evidence (needs: (string * string list) list) (regression: string list) (assertions: string list) : Ledger.SliceEvidence =
        { Slice = slice assertions; Candidate = "c"; TargetBase = "b"; TargetHead = "b"; Gate = None; Journeys = None; DesignContract = None
          Regression = Some(JObj [ "candidate", JStr "c"; "passed", JArr(regression |> List.map JStr) ]); Build = None; LiveProvider = None
          // a governed catalog acceptance at the candidate with no binding refusal, so INV obligations count (CF07-R2-1)
          TestIntegrity = None; CatalogAcceptance = Some(JObj [ "candidate", JStr "c"; "refusals", JArr [] ]); AssertionNeeds = Map.ofList needs }
    let journeyCodes (e: Ledger.SliceEvidence) =
        Ledger.sliceRules |> List.filter (fun r -> r.Id = "journeys") |> List.collect (fun r -> r.Check s e) |> List.map (fun r -> r.Code)
    [ // a needed slice not integrated: the assertion is deferred, no journey evidence is demanded at this merge
      let deferredCase = evidence [ "J1-03", [ "SL-WI-INV-LIST"; "SL-WI-INV-START" ] ] [] [ "J1-03" ]
      if Ledger.deferredAssertions s deferredCase <> [ "J1-03" ] then yield sprintf "cf07: J1-03 not deferred: %A" (Ledger.deferredAssertions s deferredCase)
      if not (journeyCodes deferredCase).IsEmpty then yield sprintf "cf07: a deferred assertion still demanded journeys: %A" (journeyCodes deferredCase)
      // control: no needs map -> every bound assertion is judged (the rule before ODR-R10-33)
      if journeyCodes (evidence [] [] [ "J1-03" ]) <> [ "JOURNEYS_MISSING" ] then yield "cf07 control: without a needs map the assertion was not judged"
      // needs only the slice itself: judged
      if journeyCodes (evidence [ "J1-03", [ "SL-WI-INV-START" ] ] [] [ "J1-03" ]) <> [ "JOURNEYS_MISSING" ] then yield "cf07: an assertion needing only its own slice was deferred"
      // a needed slice outside the scope is ignored: judged
      if journeyCodes (evidence [ "J1-03", [ "SL-NOT-IN-SCOPE" ] ] [] [ "J1-03" ]) <> [ "JOURNEYS_MISSING" ] then yield "cf07: an out-of-scope needed slice deferred the assertion"
      // the needed slice integrated (SL-T10's judged obligations passed at the merged result): judged
      if journeyCodes (evidence [ "J1-03", [ "SL-WI-INV-LIST" ] ] listObligations [ "J1-03" ]) <> [ "JOURNEYS_MISSING" ] then yield "cf07: an assertion whose needed slice is integrated was deferred"
      // and with one of those obligations missing, the needed slice is not integrated: deferred again
      if Ledger.deferredAssertions s (evidence [ "J1-03", [ "SL-WI-INV-LIST" ] ] (List.tail listObligations) [ "J1-03" ]) <> [ "J1-03" ] then yield "cf07: a partly verified needed slice counted as integrated"
      // partial: one assertion deferred, the other judged -> the judged one still demands its journey
      let mixed = evidence [ "J1-03", [ "SL-WI-INV-LIST" ] ] [] [ "J1-03"; "J1-06" ]
      if Ledger.deferredAssertions s mixed <> [ "J1-03" ] || journeyCodes mixed <> [ "JOURNEYS_MISSING" ] then yield sprintf "cf07: mixed case %A %A" (Ledger.deferredAssertions s mixed) (journeyCodes mixed)
      // review CF07-1: a needed item proved only by the builder's own Praxis records (WI-GEN-EXT-DATA: every requirement
      // record-only or live) counts as present, so withholding its completion event cannot defer the assertion
      if journeyCodes (evidence [ "J1-03", [ "SL-WI-GEN-EXT-DATA" ] ] [] [ "J1-03" ]) <> [ "JOURNEYS_MISSING" ] then yield "cf07: a missing Praxis completion event deferred the assertion"
      // review CF07-2: in a live slice, a needed item's live or WebKit obligations do not count, so breaking them cannot defer
      //   (review CF07-R2-2: on needed items that really have live (T21) and WebKit (T22) obligations, all of them unverified)
      let provableOf (wid: string) =
        let rs = arr "workItems" manifest |> List.find (fun w -> str "id" w = wid) |> strs "requirements" |> Set.ofList
        arr "requirements" manifest |> List.filter (fun r -> rs.Contains(str "id" r))
        |> List.collect (fun r -> arr "obligations" r |> List.filter (fun o -> boolOr "mandatory" true o && strOpt "kind" o <> Some "record" && not (strs "producers" o |> List.exists (fun p -> Ledger.liveProducers.Contains p || p = "SU-BROWSER-WEBKIT"))) |> List.map (str "id"))
      let liveSlice (assertions: string list) = JObj [ "id", JStr "SL-LIVE-CASE"; "dependsOn", JArr []; "realProvider", JBool true; "governedAssertions", JArr(assertions |> List.map JStr) ]
      // (a slice id no needed item depends on: T21 depends on T19, so SL-T19 would defer by CF07-R3-1)
      for wid in [ "T21"; "T22" ] do
          let liveCase = { evidence [ "J1-04", [ "SL-" + wid ] ] (provableOf wid) [ "J1-04" ] with Slice = liveSlice [ "J1-04" ] }
          if Ledger.deferredAssertions s liveCase <> [] then yield sprintf "cf07: %s's live or WebKit obligations deferred the assertion in a live slice" wid
          // and with its provable obligations unverified it is not present: deferred (the case can bite)
          let notYet = { evidence [ "J1-04", [ "SL-" + wid ] ] [] [ "J1-04" ] with Slice = liveSlice [ "J1-04" ] }
          if Ledger.deferredAssertions s notYet <> [ "J1-04" ] then yield sprintf "cf07: %s with nothing verified counted as present" wid
      // record-only obligations never count: T10 (AR-023 is record-only) with every provable obligation verified is present
      if Ledger.deferredAssertions s (evidence [ "J1-03", [ "SL-T10" ] ] (provableOf "T10") [ "J1-03" ]) <> [] then yield "cf07: T10's record-only obligations deferred the assertion"
      // a needed item whose provable obligations all passed is judged, whatever its other requirements (WI-INV-START)
      if Ledger.deferredAssertions s (evidence [ "J1-03", [ "SL-WI-INV-START" ] ] (provableOf "WI-INV-START") [ "J1-03" ]) <> [] then yield "cf07: WI-INV-START with every provable obligation verified was not present"
      // review CF07-R2-1: without a trusted catalog acceptance, catalog obligations do not count: the item is judged
      let noCatalog = { evidence [ "J1-03", [ "SL-WI-INV-LIST" ] ] [] [ "J1-03" ] with CatalogAcceptance = None }
      if Ledger.deferredAssertions s noCatalog <> [] then yield "cf07: a withheld catalog acceptance deferred the assertion"
      let refusedCatalog = { evidence [ "J1-03", [ "SL-WI-INV-LIST" ] ] [] [ "J1-03" ] with CatalogAcceptance = Some(JObj [ "candidate", JStr "c"; "refusals", JArr [ JObj [ "code", JStr "CATALOG_DIGEST_MISMATCH"; "detail", JStr "x" ] ] ]) }
      if Ledger.deferredAssertions s refusedCatalog <> [] then yield "cf07: a catalog acceptance with a binding refusal still let catalog obligations defer"
      let otherCandidate = { evidence [ "J1-03", [ "SL-WI-INV-LIST" ] ] [] [ "J1-03" ] with CatalogAcceptance = Some(JObj [ "candidate", JStr "other"; "refusals", JArr [] ]) }
      if Ledger.deferredAssertions s otherCandidate <> [] then yield "cf07: a catalog acceptance of another candidate let catalog obligations defer"
      // review CF07-3: the governed needs file carries PR #121's rules unchanged, and every assertion bound in the approved
      // slice manifest is named in it or deliberately judged at every merge (J1-01 needs nothing but its own slice)
      let governed = parseFile (Path.Combine(source, "docs/rehearsal-010-readiness/assertion-merge-needs.json"))
      let original = parseFile (Path.Combine(source, "proposals/r10-contract-first-ui/schedule-inputs/hidden-merge-dependencies.json"))
      if serialize (field "rules" governed |> Option.defaultValue JNull) <> serialize (field "rules" original |> Option.defaultValue JNull) then yield "cf07: the governed needs file's rules differ from PR #121's"
      let named = Ledger.assertionNeedsOf governed |> Map.toList |> List.map fst |> Set.ofList
      let bound = arr "slices" (parseFile (Path.Combine(source, "docs/rehearsal-010-readiness/slice-execution-manifest.json"))) |> List.collect (strs "governedAssertions") |> Set.ofList
      let unnamed = Set.difference bound named
      if unnamed <> set [ "J1-01" ] then yield sprintf "cf07: bound assertions missing from the needs file: %A (only J1-01 is judged at every merge by design)" unnamed
      // review CF07-4: the governed gate passes the file, and through the parser SL-T16's J2-08 is deferred on an empty ledger
      let gateYml = File.ReadAllText(Path.Combine(source, "tools/r9/verification-gate.yml"))
      if not (gateYml.Contains "--assertion-needs \"$src/docs/rehearsal-010-readiness/assertion-merge-needs.json\"") then yield "cf07: the governed gate does not pass --assertion-needs"
      let t16 = { evidence [] [] [ "J2-08" ] with Slice = JObj [ "id", JStr "SL-T16"; "dependsOn", JArr []; "governedAssertions", JArr [ JStr "J2-08" ] ]; AssertionNeeds = Ledger.assertionNeedsOf governed }
      if Ledger.deferredAssertions s t16 <> [ "J2-08" ] then yield "cf07: SL-T16's J2-08 is not deferred with the governed needs file"
      // review CF07-R3-1: a needed slice that depends on this one (transitively) cannot be on main yet, so the assertion is
      // deferred even when that slice has nothing a merge can prove. SL-T16's J3-01/J3-02 need SL-WI-GEN-EXT-DATA.
      let approvedSlices = arr "slices" (parseFile (Path.Combine(source, "docs/rehearsal-010-readiness/slice-execution-manifest.json")))
      let sliceAt (id: string) = approvedSlices |> List.find (fun x -> str "id" x = id)
      let t16Real = { evidence [] [] [] with Slice = sliceAt "SL-T16"; AssertionNeeds = Ledger.assertionNeedsOf governed }
      for a in [ "J3-01"; "J3-02" ] do
          if strs "governedAssertions" (sliceAt "SL-T16") |> List.contains a && not (Ledger.deferredAssertions s t16Real |> List.contains a) then
              yield sprintf "cf07: SL-T16 judges %s, which needs SL-WI-GEN-EXT-DATA (a slice that depends on SL-T16)" a
      // invariant over the approved manifest: with every provable obligation verified (so only the dependency rule can
      // defer) and with nothing verified, no judged assertion needs a slice that transitively depends on the merging slice
      let deps = arr "workItems" manifest |> List.map (fun w -> "SL-" + str "id" w, strs "dependsOn" w |> List.map ((+) "SL-")) |> Map.ofList
      let rec reach (seen: Set<string>) = function
          | [] -> seen
          | x :: rest when seen.Contains x -> reach seen rest
          | x :: rest -> reach (seen.Add x) ((Map.tryFind x deps |> Option.defaultValue []) @ rest)
      let everything = arr "requirements" manifest |> List.collect (fun r -> arr "obligations" r |> List.map (str "id"))
      let needs = Ledger.assertionNeedsOf governed
      for sl in approvedSlices do
          let id = str "id" sl
          for verified in [ []; everything ] do
              let e = { evidence [] verified [] with Slice = sl; AssertionNeeds = needs }
              let deferred = Ledger.deferredAssertions s e |> Set.ofList
              for a in strs "governedAssertions" sl |> List.filter (deferred.Contains >> not) do
                  for n in Map.tryFind a needs |> Option.defaultValue [] do
                      if n <> id && deps.ContainsKey n && (reach Set.empty (Map.tryFind n deps |> Option.defaultValue [])).Contains id then
                          yield sprintf "cf07: %s judges %s, which needs %s, a slice that depends on %s" id a n id
      // control: the rule can bite (a needed slice that does not depend on this one, fully verified, is judged)
      if Ledger.deferredAssertions s { evidence [ "J1-03", [ "SL-WI-INV-START" ] ] (provableOf "WI-INV-START") [ "J1-03" ] with Slice = JObj [ "id", JStr "SL-WI-INV-LIST"; "governedAssertions", JArr [ JStr "J1-03" ] ] } <> [] then
          yield "cf07 control: an independent, verified needed slice was deferred"
      // the verdict records what was deferred, so release knows what it still owes
      let verdict = Ledger.sliceVerdictJson (Ledger.deferredAssertions s deferredCase) deferredCase []
      if strs "deferredToRelease" verdict <> [ "J1-03" ] then yield "cf07: the slice verdict does not record the deferred assertion" ]

/// `covers` judges every form of the frozen selector grammar (Model.matches) the execution DAG uses: T18 `prefix:PF-`,
/// T20 `prefix:EXT-DATA-`, T23 `doc:docs/WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md`, T25 `origin:external`. Each form
/// has a requirement it must cover and one it must not, both directly and through a compiled scope.
let selectorGrammar () : string list =
    let wdl = selectorSubject "WDL-013" "docs/WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md" "baseline"
    let pf = selectorSubject "PF-001" "docs/PROVIDER-FIXTURE-AND-INTEGRATION-REQUIREMENTS.md" "baseline"
    let ext = selectorSubject "HISTORICAL-CASE-CATALOG§catalog-policy" "docs/HISTORICAL-CASE-CATALOG.md" "external"
    let task (selector: string) = JObj [ "id", JStr "T"; "covers", jstrs [ selector ] ]
    let cases =
        [ "all", wdl, true
          "id:WDL-013", wdl, true
          "id:WDL-013", pf, false
          "id:WDL-01", wdl, false
          "prefix:PF-", pf, true
          "prefix:PF-", wdl, false
          "prefix:F-", pf, false
          "regex:^WDL-0\\d+$", wdl, true
          "regex:^WDL-0\\d+$", pf, false
          "doc:docs/WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md", wdl, true
          "doc:docs/WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md", pf, false
          "doc:WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md", wdl, false
          "origin:external", ext, true
          "origin:external", wdl, false
          "origin:baseline", wdl, true
          "origin:baseline", ext, false
          "WDL-013", wdl, true
          "WDL-013", pf, false
          "WDL", wdl, false ]
    let direct =
        [ for selector, r, expected in cases do
              if covers (task selector) r <> expected then
                  yield sprintf "selector grammar: '%s' %s %s" selector (if expected then "does not cover" else "covers") r.Id ]
    // Through compile: plan requirements carry their document, gate-level ones take it from the baseline.
    let planReq id doc = JObj [ "requirement", JStr id; "priority", JStr "P0"; "title", JStr id; "document", JStr doc; "dependencies", JObj [ "requirements", JArr []; "contracts", JArr [] ]; "obligations", JArr [ JObj [ "sourceObligation", JStr(id + "-O1") ] ] ]
    let dagTask id (covers: string list) = JObj [ "id", JStr id; "title", JStr id; "owner", JStr "domain"; "covers", jstrs covers; "dependsOn", JArr [] ]
    let s =
        { sources with
            Plan = JObj [ "requirements", JArr [ planReq "PF-001" "docs/PF.md"; planReq "EXT-DATA-001" "docs/EXT.md"; planReq "WDL-001" "docs/WDL.md"; planReq "R1.1" "docs/REQUIREMENTS.md"; planReq "Z-001" "docs/Z.md" ] ]
            ExecutionPlan = JObj [ "suites", JArr [] ]
            Dag =
              JObj
                  [ "tasks",
                    JArr
                        [ dagTask "T17" []
                          dagTask "T18" [ "prefix:PF-" ]
                          dagTask "T20" [ "prefix:EXT-DATA-" ]
                          dagTask "T23" [ "doc:docs/WDL.md" ]
                          dagTask "T25" [ "origin:external" ]
                          dagTask "T26" [ "id:R1.1" ] ] ]
            Baseline = JObj [ "requirements", JArr [ JObj [ "id", JStr "WDL-013"; "document", JStr "docs/WDL.md" ]; JObj [ "id", JStr "PF-099"; "document", JStr "docs/PF.md" ]; JObj [ "id", JStr "U-001"; "document", JStr "docs/U.md" ] ] ] }
    let m = compile s
    let itemsOf id = arr "requirements" m |> List.tryFind (fun r -> str "id" r = id) |> Option.map (strs "workItems") |> Option.defaultValue []
    let gateOf wi = arr "workItems" m |> List.tryFind (fun w -> str "id" w = wi) |> Option.map (strs "gateRequirements") |> Option.defaultValue []
    let compiled =
        [ for id, expected in [ "PF-001", [ "T18" ]; "EXT-DATA-001", [ "T20" ]; "WDL-001", [ "T23" ]; "R1.1", [ "T26" ]; "Z-001", [ "WI-GEN-RECORDS" ] ] do
              if itemsOf id <> expected then yield sprintf "selector grammar: compiled %s maps to %A, expected %A" id (itemsOf id) expected
          for wi, expected in [ "T23", [ "WDL-013" ]; "T18", [ "PF-099" ]; "T20", []; "T25", []; "T26", [] ] do
              if gateOf wi <> expected then yield sprintf "selector grammar: compiled %s has gate requirements %A, expected %A" wi (gateOf wi) expected
          for gone in [ "WI-GEN-PF"; "WI-GEN-EXT-DATA"; "WI-GEN-WDL" ] do
              if arr "workItems" m |> List.exists (fun w -> str "id" w = gone) then yield sprintf "selector grammar: %s is generated although a DAG task's selector covers its requirements" gone ]
    direct @ compiled

/// The full grammar (V2) is the default; Legacy is selected only by a recorded package version up to v4.1.5. Compiling
/// the Rehearsal 010 sources without Legacy gives a different scope than the approved one, which preflight refuses,
/// while `scope preflight` (selecting from the manifest) still passes the committed record.
let selectorSemantics (source: string) : string list =
    let versions =
        [ "4.1.5", Legacy; "4.1.4", Legacy; "4.0.0", Legacy; "4.1", Legacy; "4.1.6", V2; "4.1.10", V2; "4.2", V2; "5", V2; "", V2; "4.1.x", V2; "v4.1.5", V2 ]
    let pf = selectorSubject "PF-001" "docs/PF.md" "baseline"
    let task (selector: string) = JObj [ "id", JStr "T"; "covers", jstrs [ selector ] ]
    let legacyCases = [ "PF-001", true; "regex:^PF-0\\d+$", true; "PF", false; "prefix:PF-", false; "id:PF-001", false; "doc:docs/PF.md", false; "origin:baseline", false; "all", false ]
    let fixedDigest = "6a0e23b0db2aa15cde7d2dff235e7cd09b133d0d3260beaa61a7195f8d70604c"
    let s = ScopeCommands.sourcesOf source (scopePackage source)
    let committed = parseFile (manifestPath source)
    let byDefault = compile s
    let itemIds (m: Json) = arr "workItems" m |> List.map (str "id") |> Set.ofList
    [ for v, expected in versions do
          if semanticsForPackageVersion v <> expected then yield sprintf "selector semantics: package version '%s' selects %A, expected %A" v (semanticsForPackageVersion v) expected
      for selector, expected in legacyCases do
          if coversWith Legacy (task selector) pf <> expected then yield sprintf "selector semantics: Legacy '%s' %s PF-001" selector (if expected then "does not cover" else "covers")
      if recordedSemantics (JObj []) <> V2 then yield "selector semantics: a manifest recording no package version selects Legacy"
      if semanticsOfSources s <> Legacy then yield "selector semantics: the v4.1.5 sources do not select Legacy for scope compile"
      // The default is the fixed grammar: the Rehearsal 010 sources compile to a different, non-approved scope.
      if str "scopeSha256" byDefault <> fixedDigest then yield sprintf "selector semantics: the default compile of the Rehearsal 010 sources gives %s, expected %s" (str "scopeSha256" byDefault) fixedDigest
      if str "scopeSha256" byDefault = str "scopeSha256" committed then yield "selector semantics: the default compile reproduces the approved Legacy scope"
      if (preflight byDefault committed).IsEmpty then yield "selector semantics: the approved Legacy scope passes preflight against the default compile"
      for gone in [ "WI-GEN-PF"; "WI-GEN-UI"; "WI-GEN-EXT-DATA" ] do
          if (itemIds byDefault).Contains gone then yield sprintf "selector semantics: the default compile still generates %s" gone
          if not ((itemIds committed).Contains gone) then yield sprintf "selector semantics: the approved Legacy scope has no %s" gone
      // The command layer selects from the manifest's record: the approved scope passes `scope preflight`.
      let code = ScopeCommands.scopeCommand [ "preflight"; "--source"; source; "--package"; scopePackage source; "--manifest"; manifestPath source ]
      if code <> 0 then yield sprintf "selector semantics: scope preflight of the approved Rehearsal 010 manifest exited %d" code ]

let all (source: string) : string list =
    let guard name f = try f () with ex -> [ sprintf "%s threw: %s" name ex.Message ]
    guard "adversarial" adversarial @ guard "selector grammar" selectorGrammar @ guard "selector semantics" (fun () -> selectorSemantics source) @ guard "legitimate" legitimate @ guard "recovery" recovery @ guard "real" (fun () -> real source) @ guard "suite runners" (fun () -> suiteRunners source) @ guard "bootstrap FSharp.Core" (fun () -> bootstrapFSharpCore source) @ guard "bootstrap CI workflow" (fun () -> bootstrapCiWorkflow source) @ guard "v4.1.4 owner records" (fun () -> v414OwnerRecords source) @ guard "v4.1.4 owner-record controls" (fun () -> v414OwnerRecordControls source) @ guard "v4.1.4 package" (fun () -> committedPackage source) @ guard "v4.1.5 owner records" (fun () -> v415OwnerRecords source) @ guard "v4.1.5 owner-record controls" (fun () -> v415OwnerRecordControls source) @ guard "reconciliation" (fun () -> reconciliation source) @ guard "work-item split" (fun () -> splitScope source) @ guard "milestone-aligned split" (fun () -> milestoneAlignedSplit source) @ guard "CF-07 deferral" (fun () -> cf07Deferral source)
