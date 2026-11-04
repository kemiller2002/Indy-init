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

let real (source: string) : string list =
    let s = ScopeCommands.sourcesOf source "requirements-compiled/v4.1.4"
    let m = compile s
    let committed = parseFile (manifestPath source)
    let reqs = arr "requirements" m
    let ids = reqs |> List.map (str "id") |> Set.ofList
    let p0 = arr "requirements" s.Plan |> List.map (str "requirement")
    let inv = arr "requirements" s.Catalog |> List.map (str "id")
    [ if not (preflight m committed).IsEmpty then yield sprintf "real: the committed scope manifest is stale or altered: %A" (preflight m committed |> List.truncate 5)
      if p0.Length <> 161 || inv.Length <> 38 then yield sprintf "real: %d P0 and %d catalog requirements (expected 161, 38)" p0.Length inv.Length
      for id in p0 @ inv do
          if not (ids.Contains id) then yield sprintf "real: approved requirement %s is not in the manifest" id
      for r in reqs do
          if (strs "workItems" r).IsEmpty then yield sprintf "real: %s has no work item" (str "id" r)
          for o in arr "obligations" r do
              if (strs "producers" o).IsEmpty then yield sprintf "real: %s has no producer" (str "id" o)
      let count k = field "counts" m |> Option.bind (intOpt k) |> Option.defaultValue -1L
      if count "planObligations" <> 613L || count "planTests" <> 660L || count "obligations" <> 863L || count "mandatoryRequirements" <> 194L then
          yield sprintf "real: counts %s" (field "counts" m |> Option.map serialize |> Option.defaultValue "")

      // Rehearsal 009 as it ended (evidence/FINAL-REPORT.md): every catalog requirement implemented and all but
      // INV-029 verified, INV-003 not applicable, no P0 requirement started, the release gate unable to run three
      // suites. It asked to stop at 02:25Z, 115 minutes after T0, for the reasons it gave.
      let catalogIds = inv
      let catalogObligations = reqs |> List.filter (fun r -> str "origin" r = "catalog" && str "id" r <> "INV-029" && str "id" r <> "INV-003") |> List.collect (arr "obligations") |> List.map (str "id")
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
          if not (List.contains "CATALOG_IS_NOT_SCOPE" (codes asked.Head)) then yield sprintf "r9 replay: 'complete' not refused as catalog-only: %A" (codes asked.Head)
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
    let manifest = compile (ScopeCommands.sourcesOf source "requirements-compiled/v4.1.4")
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

let all (source: string) : string list =
    let guard name f = try f () with ex -> [ sprintf "%s threw: %s" name ex.Message ]
    guard "adversarial" adversarial @ guard "legitimate" legitimate @ guard "recovery" recovery @ guard "real" (fun () -> real source) @ guard "suite runners" (fun () -> suiteRunners source) @ guard "v4.1.4 owner records" (fun () -> v414OwnerRecords source) @ guard "v4.1.4 owner-record controls" (fun () -> v414OwnerRecordControls source) @ guard "v4.1.4 package" (fun () -> committedPackage source) @ guard "reconciliation" (fun () -> reconciliation source)
