/// Whether a timed run may stop now (PROCESS-CORRECTIONS.md, phase 5).
///
/// Rehearsal 009 stopped after 115 of 420 minutes, with 161 P0 requirements unstarted, because "the release gate
/// cannot pass" and "the approved scope was the content catalog". Neither is a stop condition. A run may stop only:
/// 1. when every applicable mandatory requirement of the approved scope is verified (complete);
/// 2. when the owner's record authorizes it (owner-authorized);
/// 3. when a documented hard blocker leaves no independent work that can proceed (hard-blocker);
/// 4. when the execution environment fails (environment-failure), with evidence; a successor resumes (resume);
/// 5. when a separately owner-approved hard compute limit is reached (compute-limit);
/// and at the deadline it must stop, reporting what is true. Every other request is refused with a machine-readable
/// decision naming the remaining work and the next independent work items.
module Continuation

open System
open Json
open Scope
open Progress

type Blocker =
    { Id: string
      /// "implementation" blocks the work items it names; "verification" blocks only judging (never implementation).
      Kind: string
      WorkItems: string list
      Evidence: string list }

type Request =
    { At: DateTimeOffset
      Reason: string
      ClaimsAcceptance: bool
      ClaimsComplete: bool
      Approval: string option
      Blockers: Blocker list
      EnvironmentKind: string
      EnvironmentEvidence: string list
      EnvironmentAttempts: int
      /// The scope hash the agent states it is working to; it must be the checkpointed one.
      ScopeSha256: string
      /// Catalog facts the run established (for conditional requirements such as INV-003).
      Facts: Map<string, string>
      /// The compute the run has used, in the unit of the owner's compute-limit approval (compute-limit only).
      ComputeUsed: int64 option }

type Clock = { T0: DateTimeOffset; Deadline: DateTimeOffset }

/// An owner approval that names the decision a stop needs: {decision_id, action: "authorize-stop"} or a scope change
/// {action: "approve-scope", scope_sha256}, in a record whose authority is a trusted operator.
type ApprovalChange = { RecordId: string; Authority: string; Action: string; DecisionId: string; ScopeSha256: string; Limit: int64 option }

let approvalChanges (approvals: Json) (trusted: Set<string>) : ApprovalChange list =
    arr "approvals" approvals
    |> List.filter (fun r -> field "authority" r |> Option.exists (fun a -> strOpt "kind" a = Some "operator" && trusted.Contains(strOpt "id" a |> Option.defaultValue "")))
    |> List.collect (fun r ->
        arr "changes" r
        |> List.map (fun c ->
            { RecordId = strOpt "id" r |> Option.defaultValue ""
              Authority = field "authority" r |> Option.bind (strOpt "id") |> Option.defaultValue ""
              Action = strOpt "action" c |> Option.defaultValue ""
              DecisionId = strOpt "decision_id" c |> Option.defaultValue ""
              ScopeSha256 = strOpt "scope_sha256" c |> Option.defaultValue ""
              Limit = intOpt "limit" c }))

let environmentFailureKinds = set [ "container-lost"; "toolchain-unavailable"; "credentials-revoked"; "network-unavailable"; "provider-outage" ]

/// Reasons agents have given that are not stop conditions, each with the rule that answers it.
let private notStopConditions =
    Map.ofList
        [ "release-gate-failed", "a failed release gate is a verification result; it does not authorize stopping or reducing scope"
          "verification-blocked", "blocked verification is recorded as blocked; implementation of independent requirements continues"
          "catalog-complete", "the content catalog is part of the approved scope, not all of it"
          "conserve-compute", "unused time is not a reason to stop while approved work remains"
          "scope-satisfied", "the approved scope is the scope manifest; only the owner changes it"
          "partial", "a partial label is not completion: a requirement is integrated or it is open"
          "partial-complete", "a partial label is not completion: a requirement is integrated or it is open" ]

type Decision =
    { Permitted: bool
      Refusals: Refusal list
      MinutesRemaining: int
      OpenMandatory: string list
      OpenWorkItems: string list
      IndependentWorkItems: string list
      AcceptanceClaimPermitted: bool }

let private r code detail = { Code = code; Detail = detail }

/// A requirement counts when it applies: always, or when the run established its catalog fact.
let private applies (facts: Map<string, string>) (req: Json) =
    match str "applicability" req with
    | "always" -> true
    | rule ->
        rule.Split([| " and " |], StringSplitOptions.None)
        |> Array.forall (fun c -> match c.Split('=') with [| k; v |] -> facts.TryFind k = Some v | _ -> false)

/// The work items that can proceed now: open (some requirement not implemented), not parked by the retry budget,
/// not named by an implementation blocker, and whose dependencies are not themselves blocked. Verification blockers
/// never appear here: a broken runner does not stop implementation.
let independentWork (s: State) (blockers: Blocker list) : string list * string list =
    let items = arr "workItems" s.Manifest
    let openItem (w: Json) =
        let reqs = strs "requirements" w
        not (s.CompletedItems.Contains(str "id" w))
        && (reqs.IsEmpty || reqs |> List.exists (fun id -> s.Requirements.TryFind id |> Option.exists (fun rs -> rs.Implementation <> Implemented)))
    let openItems = items |> List.filter openItem
    let blockedDirect =
        blockers |> List.filter (fun b -> b.Kind = "implementation" && not b.Evidence.IsEmpty) |> List.collect (fun b -> b.WorkItems) |> Set.ofList
        |> Set.union (parked s)
    let deps = items |> List.map (fun w -> str "id" w, strs "dependsOn" w) |> Map.ofList
    let rec blockedVia (seen: Set<string>) (id: string) =
        blockedDirect.Contains id || (not (seen.Contains id) && (deps.TryFind id |> Option.defaultValue [] |> List.exists (blockedVia (seen.Add id))))
    let openIds = openItems |> List.map (str "id")
    openIds, openIds |> List.filter (blockedVia Set.empty >> not)

let decide (s: State) (clock: Clock) (checkpointScope: string) (approvals: ApprovalChange list) (q: Request) : Decision =
    let reqs = arr "requirements" s.Manifest
    let openMandatory =
        reqs
        |> List.filter (fun x -> boolOr "mandatory" false x && applies q.Facts x)
        |> List.filter (fun x -> not (List.contains (status s (str "id" x)) [ Verified; Accepted ]))
        |> List.map (str "id")
    let allAccepted = reqs |> List.filter (fun x -> boolOr "mandatory" false x && applies q.Facts x) |> List.forall (fun x -> status s (str "id" x) = Accepted)
    let openItems, independent = independentWork s q.Blockers
    let minutes = int (Math.Ceiling((clock.Deadline - q.At).TotalMinutes)) |> max 0
    let pastDeadline = q.At >= clock.Deadline
    let catalogOnly =
        let byOrigin o = reqs |> List.filter (fun x -> str "origin" x = o && boolOr "mandatory" false x && applies q.Facts x)
        let doneAll xs = xs |> List.forall (fun x -> List.contains (status s (str "id" x)) [ Verified; Accepted ])
        doneAll (byOrigin "catalog") && not (doneAll (byOrigin "v4-plan"))
    let remainingDetail = sprintf "%d applicable mandatory requirement(s) are not verified (for example %s); %d work item(s) are open, %d can proceed now" openMandatory.Length (openMandatory |> List.truncate 5 |> String.concat ", ") openItems.Length independent.Length
    let scopeRefusals =
        [ if q.ScopeSha256 <> checkpointScope then
              let approved = approvals |> List.exists (fun a -> a.Action = "approve-scope" && a.ScopeSha256 = q.ScopeSha256)
              if not approved then
                  yield r "SCOPE_CHANGE_UNAPPROVED" (sprintf "the request works to scope %s, the checkpoint pins %s, and no owner record {action: approve-scope, scope_sha256: %s} exists" q.ScopeSha256 checkpointScope q.ScopeSha256) ]
    let claimRefusals =
        [ if q.ClaimsAcceptance && not allAccepted then
              yield r "ACCEPTANCE_NOT_ESTABLISHED" "acceptance is claimed, but not every applicable mandatory requirement has an acceptance record from the owner or the independent verifier"
          if q.ClaimsComplete && not openMandatory.IsEmpty then
              yield r "COMPLETION_CLAIM_REFUSED" (sprintf "completion is claimed with open scope: %s" remainingDetail) ]
    let reasonRefusals =
        if pastDeadline then []
        else
            match q.Reason with
            | "complete" ->
                [ if not openMandatory.IsEmpty then yield r "SCOPE_INCOMPLETE" remainingDetail
                  if catalogOnly then yield r "CATALOG_IS_NOT_SCOPE" "every applicable mandatory catalog requirement is verified, but P0 requirements of the frozen plan remain; the catalog is part of the approved scope, not all of it" ]
            | "owner-authorized" ->
                match q.Approval with
                | Some id when approvals |> List.exists (fun a -> a.Action = "authorize-stop" && (a.RecordId = id || a.DecisionId = id)) -> []
                | Some id -> [ r "OWNER_AUTHORIZATION_MISSING" (sprintf "no owner record {action: authorize-stop} named %s exists in the pinned approvals" id) ]
                | None -> [ r "OWNER_AUTHORIZATION_MISSING" "owner-authorized names no approval record" ]
            | "hard-blocker" ->
                [ for b in q.Blockers do
                      if b.Evidence.IsEmpty then yield r "BLOCKER_UNDOCUMENTED" (sprintf "blocker %s has no evidence" b.Id)
                  if q.Blockers.IsEmpty then yield r "BLOCKER_UNDOCUMENTED" "hard-blocker names no blocker"
                  if not independent.IsEmpty then
                      yield r "INDEPENDENT_WORK_REMAINS" (sprintf "work items can proceed despite the blockers: %s%s" (independent |> List.truncate 8 |> String.concat ", ") (if q.Blockers |> List.exists (fun b -> b.Kind = "verification") then " (a verification blocker never blocks implementation)" else "")) ]
            | "environment-failure" ->
                [ if not (environmentFailureKinds.Contains q.EnvironmentKind) then
                      yield r "NOT_AN_ENVIRONMENT_FAILURE" (sprintf "'%s' is not an execution-environment failure (%s); record it as a blocker and continue" q.EnvironmentKind (environmentFailureKinds |> String.concat ", "))
                  if q.EnvironmentEvidence.IsEmpty then yield r "ENVIRONMENT_FAILURE_UNDOCUMENTED" "the environment failure has no evidence"
                  if q.EnvironmentAttempts < 2 then yield r "ENVIRONMENT_FAILURE_UNCONFIRMED" "an environment failure must be seen on at least two attempts" ]
            | "compute-limit" ->
                // Only a hard limit the owner approved separately, with its value, and evidence that it is reached.
                let limit = approvals |> List.tryFind (fun a -> a.Action = "approve-compute-limit" && (q.Approval = Some a.RecordId || q.Approval = Some a.DecisionId))
                match limit, q.ComputeUsed with
                | None, _ -> [ r "COMPUTE_LIMIT_UNAPPROVED" "no owner record {action: approve-compute-limit, limit} named by the request exists; a stated budget is not a stop condition" ]
                | Some { Limit = None }, _ -> [ r "COMPUTE_LIMIT_UNAPPROVED" "the compute-limit approval states no limit" ]
                | Some _, None -> [ r "COMPUTE_LIMIT_NOT_EVIDENCED" "the request states no compute used" ]
                | Some { Limit = Some l }, Some used when used < l -> [ r "COMPUTE_LIMIT_NOT_REACHED" (sprintf "%d of the approved %d used" used l) ]
                | Some _, Some _ -> []
            | "deadline" -> [ r "DEADLINE_NOT_REACHED" (sprintf "%d minute(s) remain before %s" minutes (clock.Deadline.ToString "o")) ]
            | other ->
                let why = notStopConditions.TryFind other |> Option.defaultValue "only complete, owner-authorized, hard-blocker, environment-failure and deadline are stop conditions"
                [ r "NOT_A_STOP_CONDITION" (sprintf "'%s': %s; %s" other why remainingDetail) ]
    let refusals = scopeRefusals @ reasonRefusals @ claimRefusals
    // At the deadline the run stops whatever remains; only false claims are refused.
    let permitted = (pastDeadline && scopeRefusals.IsEmpty) || (reasonRefusals.IsEmpty && scopeRefusals.IsEmpty)
    { Permitted = permitted
      Refusals = refusals
      MinutesRemaining = minutes
      OpenMandatory = openMandatory
      OpenWorkItems = openItems
      IndependentWorkItems = independent
      AcceptanceClaimPermitted = allAccepted }

let decisionJson (q: Request) (d: Decision) : Json =
    JObj
        [ "schema", JStr "indy-init.stop-decision/1"
          "requestedAt", JStr(q.At.ToString "o")
          "reason", JStr q.Reason
          "decision", JStr(if d.Permitted then "STOP_PERMITTED" else "CONTINUE_REQUIRED")
          "minutesRemaining", jint d.MinutesRemaining
          "acceptanceClaimPermitted", JBool d.AcceptanceClaimPermitted
          "refusals", JArr(d.Refusals |> List.map refusalJson)
          "remaining",
          JObj
              [ "openMandatoryRequirements", jint d.OpenMandatory.Length
                "openWorkItems", jstrs d.OpenWorkItems
                "nextIndependentWork", jstrs (d.IndependentWorkItems |> List.truncate 10) ] ]

let requestOf (j: Json) : Request =
    let at = strOpt "at" j |> Option.map DateTimeOffset.Parse |> Option.defaultValue DateTimeOffset.MinValue
    let env = field "environment" j |> Option.defaultValue (JObj [])
    let claims = field "claims" j |> Option.defaultValue (JObj [])
    { At = at
      Reason = strOpt "reason" j |> Option.defaultValue ""
      ClaimsAcceptance = boolOr "acceptance" false claims
      ClaimsComplete = boolOr "complete" false claims
      Approval = strOpt "approval" j
      Blockers =
        arr "blockers" j
        |> List.map (fun b -> { Id = strOpt "id" b |> Option.defaultValue ""; Kind = strOpt "kind" b |> Option.defaultValue ""; WorkItems = strs "workItems" b; Evidence = strs "evidence" b })
      EnvironmentKind = strOpt "kind" env |> Option.defaultValue ""
      EnvironmentEvidence = strs "evidence" env
      EnvironmentAttempts = intOpt "attempts" env |> Option.map int |> Option.defaultValue 0
      ScopeSha256 = strOpt "scopeSha256" j |> Option.defaultValue ""
      ComputeUsed = intOpt "computeUsed" j
      Facts =
        match field "facts" j with
        | Some(JObj ps) -> ps |> List.map (fun (k, v) -> k, (match v with JBool b -> (if b then "true" else "false") | JStr x -> x | _ -> "")) |> Map.ofList
        | _ -> Map.empty }

// ---- recovery: what a successor session does next ---------------------------------------------------------------

/// The plan a successor reads after the executor is lost (container restart, session end). It is rebuilt from
/// repository files alone: the scope manifest, the committed checkpoint and the committed progress events. A lost
/// executor is never a stop condition for the run; the successor continues unless the deadline has passed or a
/// stop was permitted and recorded.
type ResumePlan =
    { Action: string
      Refusals: Refusal list
      MinutesRemaining: int
      OpenMandatory: string list
      NextWork: string list
      BlockedSuites: string list
      Parked: string list }

let resumePlan (s: State) (clock: Clock) (checkpointScope: string) (at: DateTimeOffset) (stopPermittedRecorded: bool) : ResumePlan =
    let manifestScope = strOpt "scopeSha256" s.Manifest |> Option.defaultValue ""
    let refusals =
        [ if checkpointScope = "" then yield { Code = "CHECKPOINT_MISSING"; Detail = "no scope checkpoint is committed: run scope checkpoint before any implementation" }
          elif checkpointScope <> manifestScope then
              yield { Code = "SCOPE_CHANGE_UNAPPROVED"; Detail = sprintf "the manifest's scope %s is not the checkpointed scope %s" manifestScope checkpointScope } ]
    let openMandatory =
        arr "requirements" s.Manifest
        |> List.filter (fun x -> boolOr "mandatory" false x && str "applicability" x = "always")
        |> List.filter (fun x -> not (List.contains (status s (str "id" x)) [ Verified; Accepted ]))
        |> List.map (str "id")
    let _, independent = independentWork s []
    let minutes = int (Math.Ceiling((clock.Deadline - at).TotalMinutes)) |> max 0
    let action =
        if not refusals.IsEmpty then "REPAIR_CHECKPOINT"
        elif at >= clock.Deadline then "REPORT_AND_STOP"
        elif stopPermittedRecorded then "STOPPED"
        elif openMandatory.IsEmpty then "REQUEST_STOP_COMPLETE"
        elif independent.IsEmpty then "REQUEST_STOP_DECISION"
        else "CONTINUE"
    { Action = action
      Refusals = refusals
      MinutesRemaining = minutes
      OpenMandatory = openMandatory
      NextWork = independent
      BlockedSuites = s.BlockedSuites |> Map.toList |> List.map fst
      Parked = parked s |> Set.toList }

let resumeJson (at: DateTimeOffset) (p: ResumePlan) : Json =
    JObj
        [ "schema", JStr "indy-init.resume-plan/1"
          "at", JStr(at.ToString "o")
          "action", JStr p.Action
          "minutesRemaining", jint p.MinutesRemaining
          "refusals", JArr(p.Refusals |> List.map refusalJson)
          "openMandatoryRequirements", jint p.OpenMandatory.Length
          "nextIndependentWork", jstrs (p.NextWork |> List.truncate 10)
          "blockedSuites", jstrs p.BlockedSuites
          "parkedWorkItems", jstrs p.Parked
          "rule", JStr "A lost executor is not a stop condition. Continue with nextIndependentWork; stop only on a recorded STOP_PERMITTED decision or at the deadline." ]

// ---- the run clock: bound to the T0 record and the owner's deadline policy ------------------------------------------

/// The clock comes from the run's T0 record (committed once at T0, `indy-init.t0/1`) and the owner's deadline policy
/// (`indy-init.deadline-policy/1`), never from the caller: a recovery or a replacement agent continues against the
/// original deadline. A claimed T0 or window that differs is DEADLINE_RESET.
let boundClock (t0Record: Json) (policy: Json) (claimedT0: DateTimeOffset option) (claimedWindow: float option) : Result<Clock, Refusal> =
    let window = intOpt "windowMinutes" policy |> Option.map float
    match strOpt "t0" t0Record |> Option.map DateTimeOffset.Parse, window with
    | None, _ -> Error(r "T0_RECORD_MISSING" "the T0 record states no t0")
    | _, None -> Error(r "DEADLINE_POLICY_MISSING" "the deadline policy states no windowMinutes")
    | Some t0, Some w ->
        let deadline = t0.AddMinutes w
        let recorded = strOpt "deadline" t0Record |> Option.map DateTimeOffset.Parse
        if recorded.IsSome && recorded.Value <> deadline then Error(r "DEADLINE_RESET" (sprintf "the T0 record's deadline %s is not t0 + %g minutes (%s)" (recorded.Value.ToString "o") w (deadline.ToString "o")))
        elif claimedT0 |> Option.exists ((<>) t0) then Error(r "DEADLINE_RESET" (sprintf "the request uses T0 %s; the run's T0 is %s (the original deadline is never reset)" (claimedT0.Value.ToString "o") (t0.ToString "o")))
        elif claimedWindow |> Option.exists ((<>) w) then Error(r "DEADLINE_RESET" (sprintf "the request uses a %g-minute window; the owner's policy is %g" claimedWindow.Value w))
        else Ok { T0 = t0; Deadline = deadline }
