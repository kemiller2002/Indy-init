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
              match o.T0Versions with
              | (_, first) :: rest ->
                  [ for sha, later in rest do
                        if strOpt "t0" later <> strOpt "t0" first || strOpt "deadline" later <> strOpt "deadline" first then
                            yield f "DEADLINE_RESET" "blocking" (sprintf "commit %s changes the T0 record (t0 %s -> %s)" sha (strOpt "t0" first |> Option.defaultValue "") (strOpt "t0" later |> Option.defaultValue "")) [ sha ] [] "restore the original T0 record; the deadline is never reset" None ]
              | [] -> [ f "T0_RECORD_MISSING" "blocking" "no T0 record is committed" [] [] "commit evidence/t0/t0.json at T0" None ] }
      { Id = "work-item-before-implementation"
        Detect =
          fun o ->
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
                    let claimsSuccess = [ "accepted"; "verified"; "complete"; "all pass" ] |> List.exists (fun w -> text.Contains(w, StringComparison.OrdinalIgnoreCase))
                    let labelled = text.Contains("implementer-run", StringComparison.OrdinalIgnoreCase)
                    if claimsSuccess && not labelled && not (governed && releasePass) then
                        yield f "SELF_VERIFIED_SUCCESS" "blocking" (sprintf "%s claims success, but no governed release verdict and governed catalog acceptance support it" (strOpt "source" c |> Option.defaultValue "a report")) [ strOpt "source" c |> Option.defaultValue "" ] [] "lead with the governed verdict; label builder-run results implementer-run" (Some "verification collapsed into implementation") ] }
      { Id = "premature-stop"
        Detect =
          fun o ->
              let lastCommit = o.Commits |> List.tryLast |> Option.map (fun c -> c.At)
              [ for d in o.StopDecisions do
                    let at = strOpt "requestedAt" d |> Option.map DateTimeOffset.Parse
                    match at with
                    | Some t when t < o.Clock.Deadline && strOpt "decision" d = Some "CONTINUE_REQUIRED" && lastCommit |> Option.forall (fun l -> l <= t) && (o.Now - t).TotalMinutes >= 20.0 ->
                        yield f "PREMATURE_STOP" "blocking" (sprintf "a stop was refused at %s (CONTINUE_REQUIRED, %d minutes left) and no work followed for %.0f minutes" (t.ToString "o") (intOpt "minutesRemaining" d |> Option.defaultValue 0L) (o.Now - t).TotalMinutes) [] [] "resume the next independent work item" None
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
