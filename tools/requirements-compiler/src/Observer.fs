/// The rehearsal observer's findings (correctness-first execution, section 6).
///
/// Read-only: it reads the approved scope, the progress ledger, the candidate repository's commits, CI and gate
/// verdicts and integration evidence, and writes findings (`indy-init.observer-findings/1`) for Specula to display. It
/// never merges, edits requirements or changes the builder's work. Where a finding matches an existing stop-the-line
/// rule (KICKOFF-EXECUTION-PACKET §8) it says so and recommends that existing hold; it creates no authority of its own.
module Observer

open System
open Json
open Scope

type Commit = { Sha: string; At: DateTimeOffset; Message: string; Files: string list }

type Observation =
    { Now: DateTimeOffset
      Clock: Continuation.Clock
      /// Why the run clock could not be bound to the T0 record and the policy, if it could not.
      ClockProblem: string option
      Manifest: Json
      /// The scope hash the committed checkpoint pins, if a checkpoint is committed.
      CheckpointScope: string option
      CheckpointAt: DateTimeOffset option
      /// Every committed version of the T0 record, oldest first.
      T0Versions: (string * Json) list
      /// Governed digests pinned at T0 (path -> sha256) and the digests at the head now.
      PinnedDigests: Map<string, string>
      HeadDigests: Map<string, string>
      Approvals: Continuation.ApprovalChange list
      Events: Json list
      Commits: Commit list
      /// Governed gate verdicts (gate.json), catalog acceptance, design-contract and journey results, newest last.
      Gates: Json list
      CatalogAcceptance: Json option
      DesignContract: Json option
      LiveResults: Json list
      StopDecisions: Json list
      /// Governed completion-gate verdicts (from the Verification gate's artifacts).
      Completions: Json list
      TestIntegrity: Json list
      /// Claims the builder made (final report, PR text), as {source, text}.
      Claims: Json list
      SliceManifest: Json option
      /// Remaining estimated minutes of open slices (from the slice manifest), if known.
      RemainingEstimate: int option }

type Finding =
    { Code: string
      Severity: string // blocking | warning
      Detail: string
      Evidence: string list
      Requirements: string list
      Recommendation: string
      /// The KICKOFF-EXECUTION-PACKET §8 rule this matches, when it does.
      StopTheLine: string option }

let private f code severity detail evidence reqs recommendation stl =
    { Code = code; Severity = severity; Detail = detail; Evidence = evidence; Requirements = reqs; Recommendation = recommendation; StopTheLine = stl }

let private isImplementation (path: string) =
    [ "src/"; "web/"; "tests/" ] |> List.exists (fun p -> path.StartsWith(p, StringComparison.Ordinal))

let private ledger (o: Observation) = Ledger.replay o.Manifest o.Events

type Detector = { Id: string; Detect: Observation -> Finding list }

let detectors : Detector list =
    [ { Id = "scope-coverage"
        Detect =
          fun o ->
              match o.SliceManifest with
              | None -> [ f "SCOPE_COVERAGE_UNKNOWN" "blocking" "no slice execution manifest is published; coverage of the approved scope cannot be checked" [] [] "derive it with requirements-compiler slice-manifest" None ]
              | Some m ->
                  let cov = field "coverage" m |> Option.defaultValue (JObj [])
                  [ if not (boolOr "complete" false cov) then
                        yield f "SCOPE_COVERAGE_MISSING" "blocking" (sprintf "%d requirement(s) are in no slice and not classified" (intOpt "unassigned" cov |> Option.defaultValue 0L)) [] [] "assign every requirement or obtain an owner exclusion" (Some "hidden material blocker")
                    if strOpt "scopeSha256" m <> strOpt "scopeSha256" o.Manifest then
                        yield f "PLAN_CHANGED" "blocking" "the slice manifest was derived from another scope" [ strOpt "scopeSha256" m |> Option.defaultValue "" ] [] "re-derive from the approved scope" None ] }
      { Id = "plan-and-scope-unchanged"
        Detect =
          fun o ->
              [ for KeyValue(path, pinned) in o.PinnedDigests do
                    match o.HeadDigests.TryFind path with
                    | Some h when h = pinned -> ()
                    | h -> yield f "PLAN_CHANGED" "blocking" (sprintf "%s changed since T0 (%s -> %s)" path pinned (h |> Option.defaultValue "deleted")) [ path ] [] "revert, or obtain an owner decision for the change" None
                match o.CheckpointScope with
                | Some cs when cs <> (strOpt "scopeSha256" o.Manifest |> Option.defaultValue "") ->
                    if not (o.Approvals |> List.exists (fun a -> a.Action = "approve-scope" && a.ScopeSha256 = (strOpt "scopeSha256" o.Manifest |> Option.defaultValue ""))) then
                        yield f "SCOPE_CHANGE_UNAPPROVED" "blocking" (sprintf "the scope hash is %s; the checkpoint pins %s; no owner approval" (strOpt "scopeSha256" o.Manifest |> Option.defaultValue "") cs) [] [] "restore the approved scope" None
                | _ -> () ] }
      { Id = "deadline-reset"
        Detect =
          fun o ->
              let decisionResets =
                  match o.T0Versions with
                  | (_, first) :: _ ->
                      [ for d in o.StopDecisions do
                            match strOpt "t0" d, strOpt "t0" first with
                            | Some used, Some recorded when DateTimeOffset.Parse used <> DateTimeOffset.Parse recorded ->
                                yield f "DEADLINE_RESET" "blocking" (sprintf "a stop decision was judged with T0 %s; the run's T0 is %s" used recorded) [] [] "judge stop requests with --t0-record" None
                            | _ -> () ]
                  | [] -> []
              let replayed =
                  [ for d in o.StopDecisions do
                        match strOpt "deadline" d |> Option.map DateTimeOffset.Parse with
                        | Some dl when o.ClockProblem.IsNone && dl <> o.Clock.Deadline -> yield f "DEADLINE_RESET" "blocking" (sprintf "a stop decision was judged against deadline %s; the run's deadline (committed T0 record and governed policy) is %s" (dl.ToString "o") (o.Clock.Deadline.ToString "o")) [] [] "judge stop requests with the committed T0 record and the governed deadline policy" None
                        | None -> yield f "DEADLINE_UNRECORDED" "blocking" "a stop decision records no deadline: it cannot be checked against the run's clock" [] [] "use requirements-compiler stop-decision" None
                        | _ -> ()
                        if strOpt "clock" d = Some "replay" then yield f "DEADLINE_RESET" "blocking" "a stop decision was judged with a replay clock (--replay-clock): a live run's clock is its T0 record" [] [] "judge stop requests with --t0-record, the pinned policy and --repo" None
                        match strOpt "requestedAt" d |> Option.map DateTimeOffset.Parse with
                        | Some at when at > o.Now.AddMinutes 2.0 -> yield f "REQUEST_IN_FUTURE" "blocking" (sprintf "a stop request is dated %s, after this observation (%s)" (at.ToString "o") (o.Now.ToString "o")) [] [] "date requests when they are made" None
                        | _ -> () ]
              decisionResets @ replayed
              @ (match o.ClockProblem with
                 | Some why -> [ f "CLOCK_UNBOUND" "blocking" (sprintf "the run clock cannot be bound to the T0 record and the policy: %s; deadline and premature-stop findings are unavailable" why) [] [] "commit a valid evidence/t0/t0.json at T0" None ]
                 | None -> [])
              @
              match o.T0Versions with
              | (_, first) :: rest ->
                  [ for sha, later in rest do
                        if strOpt "t0" later <> strOpt "t0" first || strOpt "deadline" later <> strOpt "deadline" first then
                            yield f "DEADLINE_RESET" "blocking" (sprintf "commit %s changes the T0 record (t0 %s -> %s)" sha (strOpt "t0" first |> Option.defaultValue "") (strOpt "t0" later |> Option.defaultValue "")) [ sha ] [] "restore the original T0 record; the deadline is never reset" None ]
              | [] -> [ f "T0_RECORD_MISSING" "blocking" "no T0 record is committed" [] [] "commit evidence/t0/t0.json at T0" None ] }
      { Id = "work-item-before-implementation"
        Detect =
          fun o ->
              // The T0 bootstrap (the commit that installs governance) writes the generated stubs; it is not implementation.
              // Only the first such commit; a later commit touching the governance file is judged like any other.
              let bootstrap = o.Commits |> List.tryFind (fun c -> c.Files |> List.contains ".echelon/r9-governance.json") |> Option.map (fun c -> c.Sha)
              let o = { o with Commits = o.Commits |> List.filter (fun c -> Some c.Sha <> bootstrap) }
              [ match o.CheckpointAt with
                | None ->
                    for c in o.Commits do
                        if c.Files |> List.exists isImplementation then
                            yield f "IMPLEMENTATION_WITHOUT_WORK_ITEM" "blocking" (sprintf "commit %s changes implementation and no scope checkpoint is committed" c.Sha) [ c.Sha ] [] "commit the scope checkpoint (every work item registered) before implementation" (Some "verification collapsed into implementation")
                | Some at ->
                    for c in o.Commits do
                        if c.At < at && c.Files |> List.exists isImplementation then
                            yield f "IMPLEMENTATION_WITHOUT_WORK_ITEM" "blocking" (sprintf "commit %s changes implementation before the scope checkpoint (%s)" c.Sha (at.ToString "o")) [ c.Sha ] [] "record the process violation; register the work item before more implementation" None ] }
      { Id = "tests-weakened"
        Detect =
          fun o ->
              o.TestIntegrity
              |> List.collect (fun t -> arr "refusals" t |> List.map (fun r -> f (str "code" r) "blocking" (str "detail" r) [ strOpt "candidate" t |> Option.defaultValue "" ] [] "restore the test; a weakened test never makes CI green" (Some "test/proof data being fabricated or hard-coded"))) }
      { Id = "mock-substitution"
        Detect =
          fun o ->
              [ for r in o.LiveResults do
                    let baseUrl = field "provider" r |> Option.bind (strOpt "baseUrl") |> Option.defaultValue ""
                    if baseUrl <> "https://api.github.com" then
                        yield f "MOCK_SUBSTITUTED_FOR_LIVE" "blocking" (sprintf "live verification at %s ran against %s" (strOpt "candidate" r |> Option.defaultValue "") (if baseUrl = "" then "(unknown provider)" else baseUrl)) [] [ "PERSIST-021"; "INV-037" ] "run the live checks against api.github.com" (Some "false synced/persisted state")
                for c in o.Commits do
                    if c.Files |> List.exists (fun p -> p.StartsWith("tests/browser/live", StringComparison.Ordinal)) && c.Message.Contains("mock", StringComparison.OrdinalIgnoreCase) then
                        yield f "MOCK_SUBSTITUTED_FOR_LIVE" "warning" (sprintf "commit %s mentions a mock in the live suite" c.Sha) [ c.Sha ] [] "confirm the live suite reads GitHub" None ] }
      { Id = "design-contract"
        Detect =
          fun o ->
              match o.DesignContract with
              | Some d when strOpt "verdict" d = Some "PASS" -> []
              | Some d -> [ f "DESIGN_CONTRACT_FAILED" "blocking" (String.concat "; " (strs "causes" d |> List.truncate 4)) [ strOpt "candidate" d |> Option.defaultValue "" ] [ "UI-016"; "UI-029"; "UI-006" ] "use the Forma shell and the theme tokens" (Some "screen contract accessibility failure") ]
              | None -> [ f "DESIGN_CONTRACT_NOT_RUN" "warning" "no design-contract verdict yet" [] [ "UI-016"; "UI-029" ] "run the governed design contract" None ] }
      { Id = "self-verified-success"
        Detect =
          fun o ->
              let governed = o.CatalogAcceptance |> Option.exists (fun c -> strOpt "level" c = Some "GOVERNED-ACCEPTED")
              let releasePass = o.Gates |> List.exists (fun g -> strOpt "mode" g = Some "release" && (strs "causes" g).IsEmpty && (strs "scope" g).IsEmpty)
              [ for c in o.Claims do
                    let text = strOpt "text" c |> Option.defaultValue ""
                    let has (w: string) = text.Contains(w, StringComparison.OrdinalIgnoreCase)
                    // "implementer-run" honestly labels a verified/passed result; it never makes completion or acceptance true.
                    let claimsCompletion = [ "accepted"; "complete" ] |> List.exists has
                    let claimsVerified = [ "verified"; "all pass" ] |> List.exists has && not (has "implementer-run")
                    if (claimsCompletion || claimsVerified) && not (governed && releasePass) then
                        yield f "SELF_VERIFIED_SUCCESS" "blocking" (sprintf "%s claims success, but no governed release verdict and governed catalog acceptance support it" (strOpt "source" c |> Option.defaultValue "a report")) [ strOpt "source" c |> Option.defaultValue "" ] [] "lead with the governed verdict; label builder-run results implementer-run" (Some "verification collapsed into implementation") ] }
      { Id = "premature-stop"
        Detect =
          fun o ->
              // Work on scope means implementation; a report or evidence commit after a refused stop is not progress.
              let lastCommit = o.Commits |> List.filter (fun c -> c.Files |> List.exists isImplementation) |> List.tryLast |> Option.map (fun c -> c.At)
              [ for d in o.StopDecisions do
                    let at = strOpt "requestedAt" d |> Option.map DateTimeOffset.Parse
                    match at with
                    | Some t when t < o.Clock.Deadline && strOpt "decision" d = Some "CONTINUE_REQUIRED" && lastCommit |> Option.forall (fun l -> l <= t) && (o.Now - t).TotalMinutes >= 20.0 ->
                        yield f "PREMATURE_STOP" "blocking" (sprintf "a stop was refused at %s (CONTINUE_REQUIRED, %d minutes left) and no implementation work followed for %.0f minutes" (t.ToString "o") (intOpt "minutesRemaining" d |> Option.defaultValue 0L) (o.Now - t).TotalMinutes) [] [] "resume the next independent work item" None
                    | Some t when t < o.Clock.Deadline && strOpt "decision" d = Some "STOP_PERMITTED" && strOpt "reason" d = Some "complete" && not (o.Completions |> List.exists (fun c -> strOpt "verdict" c = Some "COMPLETE")) ->
                        yield f "PREMATURE_STOP" "blocking" (sprintf "a stop as complete was permitted at %s, but no governed completion-gate COMPLETE verdict exists" (t.ToString "o")) [] [] "resume: completion is decided by the governed completion gate" None
                    | Some t when t < o.Clock.Deadline && strOpt "decision" d = Some "STOP_PERMITTED" && not (List.contains (strOpt "reason" d |> Option.defaultValue "") [ "complete"; "owner-authorized" ]) ->
                        yield f "EARLY_STOP_PERMITTED" "warning" (sprintf "a stop for '%s' was permitted at %s with %d minutes left; the owner should confirm its evidence" (strOpt "reason" d |> Option.defaultValue "") (t.ToString "o") (intOpt "minutesRemaining" d |> Option.defaultValue 0L)) [] [] "review the blocker or compute evidence" None
                    | _ -> () ] }
      { Id = "unmerged-verified"
        Detect =
          fun o ->
              match ledger o with
              | Error e -> [ f "LEDGER_INVALID" "blocking" e [] [] "repair the progress events" None ]
              | Ok s ->
                  let waiting = arr "requirements" o.Manifest |> List.map (str "id") |> List.filter (fun id -> Ledger.ledgerState s id = Ledger.VerifiedUnmerged)
                  if waiting.IsEmpty then [] else [ f "UNMERGED_VERIFIED_WORK" "warning" (sprintf "%d verified requirement(s) are not integrated" waiting.Length) [] (waiting |> List.truncate 10) "run the slice gate and merge on PASS" None ] }
      { Id = "repeated-failures"
        Detect =
          fun o ->
              match ledger o with
              | Error _ -> []
              | Ok s -> [ for wi in Progress.parked s.Progress do yield f "REPEATED_FAILURE" "warning" (sprintf "%s failed %d times with the same cause and is parked" wi Progress.retryBudget) [ wi ] [] "change approach or move to the next independent slice" None ] }
      { Id = "deadline-risk"
        Detect =
          fun o ->
              let remaining = int (o.Clock.Deadline - o.Now).TotalMinutes
              match o.RemainingEstimate with
              | Some est when est > remaining && remaining > 0 -> [ f "DEADLINE_RISK" "warning" (sprintf "%d estimated minutes of open slices, %d minutes left" est remaining) [] [] "take the highest-value ready slice next" None ]
              | _ -> [] }
      { Id = "main-health"
        Detect =
          fun o ->
              match ledger o with
              | Ok { Unhealthy = Some(c, why) } -> [ f "MAIN_UNHEALTHY" "blocking" (sprintf "main is unhealthy at %s: %s" c why) [ c ] [] "repair or revert before any further merge" None ]
              | _ -> [] } ]

let observe (o: Observation) = detectors |> List.collect (fun d -> d.Detect o)

let findingsJson (o: Observation) (findings: Finding list) =
    JObj
        [ "schema", JStr "indy-init.observer-findings/1"
          "observedAt", JStr(o.Now.ToString "o")
          "deadline", JStr(o.Clock.Deadline.ToString "o")
          "readOnly", JBool true
          "blocking", jint (findings |> List.filter (fun x -> x.Severity = "blocking") |> List.length)
          "findings",
          JArr(
              findings
              |> List.map (fun x ->
                  JObj
                      [ "code", JStr x.Code
                        "severity", JStr x.Severity
                        "detail", JStr x.Detail
                        "evidence", jstrs x.Evidence
                        "requirements", jstrs x.Requirements
                        "recommendation", JStr x.Recommendation
                        "stopTheLine", (match x.StopTheLine with Some s -> JObj [ "rule", JStr s; "source", JStr "docs/KICKOFF-EXECUTION-PACKET.md §8" ] | None -> JNull) ])
          ) ]
