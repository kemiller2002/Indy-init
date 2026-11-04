/// Requirement progress with implementation and verification kept apart (PROCESS-CORRECTIONS.md, phase 4).
///
/// Rehearsal 009 let one fact, "the release gate fails", stand for everything: no requirement beyond the catalog was
/// started, and the run stopped. Here a requirement has two independent axes:
/// - implementation: not started, in progress, implemented (from work events);
/// - verification: per obligation pass, fail, pending, or blocked (a suite that cannot run);
/// and acceptance only from a trusted authority's record. A broken runner blocks the obligations that suite judges and
/// nothing else: no implementation state changes, and unrelated work stays available.
///
/// The state is a pure fold of `indy-init.progress-event/1` events over the scope manifest.
module Progress

open System
open Json
open Scope

type Implementation =
    | NotStarted
    | InProgress
    | Implemented

type Verdict =
    | Pass
    | Fail of string

type RequirementState =
    { Implementation: Implementation
      /// obligation id -> the latest recorded verdict
      Results: Map<string, Verdict>
      Accepted: string option }

type State =
    { Manifest: Json
      Requirements: Map<string, RequirementState>
      /// suite -> cause, for the producers that cannot run now
      BlockedSuites: Map<string, string>
      /// work item -> consecutive failed attempts with the same cause
      Attempts: Map<string, string * int>
      /// work items whose implementation started (for the work-item view)
      StartedItems: Set<string>
      /// work items recorded complete (a timed task that covers no P0 requirement completes only this way)
      CompletedItems: Set<string> }

/// The derived status a person reads; the two axes stay separately available.
type Status =
    | NotStartedStatus
    | ImplementationInProgress
    | VerificationPending
    | VerificationBlocked
    | VerificationFailed
    | Verified
    | Accepted

let statusName =
    function
    | NotStartedStatus -> "not-started"
    | ImplementationInProgress -> "implementation-in-progress"
    | VerificationPending -> "verification-pending"
    | VerificationBlocked -> "verification-blocked"
    | VerificationFailed -> "verification-failed"
    | Verified -> "verified"
    | Accepted -> "accepted"

let implementationName = function NotStarted -> "not-started" | InProgress -> "in-progress" | Implemented -> "implemented"

let private requirementsOf (manifest: Json) = arr "requirements" manifest

let private obligationsOf (manifest: Json) (id: string) =
    requirementsOf manifest |> List.tryFind (fun r -> str "id" r = id) |> Option.map (arr "obligations") |> Option.defaultValue []

let initial (manifest: Json) : State =
    { Manifest = manifest
      Requirements = requirementsOf manifest |> List.map (fun r -> str "id" r, { Implementation = NotStarted; Results = Map.empty; Accepted = None }) |> Map.ofList
      BlockedSuites = Map.empty
      Attempts = Map.empty
      StartedItems = Set.empty
      CompletedItems = Set.empty }

/// The verification of one obligation: blocked when every producer that judges it is blocked and no verdict exists
/// from a producer that ran; otherwise the latest verdict, or pending.
type ObligationVerification =
    | OPass
    | OFail of string
    | OPending
    | OBlocked of string

let obligationVerification (s: State) (r: RequirementState) (o: Json) =
    let id = str "id" o
    match r.Results.TryFind id with
    | Some Pass -> OPass
    | Some(Fail why) -> OFail why
    | None ->
        let producers = strs "producers" o
        let blocked = producers |> List.choose (fun p -> s.BlockedSuites.TryFind p |> Option.map (fun c -> p, c))
        if not producers.IsEmpty && blocked.Length = producers.Length then OBlocked(blocked |> List.map (fun (p, c) -> sprintf "%s: %s" p c) |> String.concat "; ")
        else OPending

/// A requirement's status from its two axes. Only mandatory obligations decide verification of a mandatory
/// requirement; a non-mandatory requirement is decided by all of its obligations.
let status (s: State) (id: string) : Status =
    let r = s.Requirements[id]
    let req = requirementsOf s.Manifest |> List.find (fun x -> str "id" x = id)
    let deciding = arr "obligations" req |> List.filter (fun o -> boolOr "mandatory" false o || not (boolOr "mandatory" false req))
    let vs = deciding |> List.map (obligationVerification s r)
    match r.Implementation, r.Accepted with
    | NotStarted, _ -> NotStartedStatus
    | InProgress, _ -> ImplementationInProgress
    | Implemented, Some _ -> Accepted
    | Implemented, None ->
        if vs |> List.exists (function OFail _ -> true | _ -> false) then VerificationFailed
        elif vs |> List.exists (function OBlocked _ -> true | _ -> false) then VerificationBlocked
        elif vs |> List.forall ((=) OPass) then Verified
        else VerificationPending

// ---- events ------------------------------------------------------------------------------------------------------

type Event =
    | ImplementationStarted of workItem: string * requirements: string list
    | ImplementationCompleted of requirements: string list * commit: string
    | VerificationRecorded of obligation: string * verdict: Verdict * commit: string
    | SuiteBlocked of suite: string * cause: string
    | SuiteUnblocked of suite: string
    | AttemptFailed of workItem: string * cause: string
    | WorkItemCompleted of workItem: string
    | AcceptanceRecorded of requirement: string * authorityKind: string * record: string

let eventOf (j: Json) : Result<Event, string> =
    let reqs () = strs "requirements" j
    match strOpt "kind" j with
    | Some "implementation-started" -> Ok(ImplementationStarted(str "workItem" j, reqs ()))
    | Some "implementation-completed" -> Ok(ImplementationCompleted(reqs (), strOpt "commit" j |> Option.defaultValue ""))
    | Some "verification-recorded" ->
        let verdict = if strOpt "result" j = Some "pass" then Pass else Fail(strOpt "cause" j |> Option.defaultValue "failed")
        Ok(VerificationRecorded(str "obligation" j, verdict, strOpt "commit" j |> Option.defaultValue ""))
    | Some "suite-blocked" -> Ok(SuiteBlocked(str "suite" j, strOpt "cause" j |> Option.defaultValue "blocked"))
    | Some "suite-unblocked" -> Ok(SuiteUnblocked(str "suite" j))
    | Some "attempt-failed" -> Ok(AttemptFailed(str "workItem" j, strOpt "cause" j |> Option.defaultValue "failed"))
    | Some "work-item-completed" -> Ok(WorkItemCompleted(str "workItem" j))
    | Some "acceptance-recorded" ->
        let authority = field "authority" j |> Option.defaultValue (JObj [])
        Ok(AcceptanceRecorded(str "requirement" j, strOpt "kind" authority |> Option.defaultValue "", strOpt "record" j |> Option.defaultValue ""))
    | k -> Error(sprintf "unknown progress event kind %A" k)

let private obligationOwner (s: State) (obligation: string) =
    requirementsOf s.Manifest |> List.tryFind (fun r -> arr "obligations" r |> List.exists (fun o -> str "id" o = obligation)) |> Option.map (str "id")

let private update (s: State) (id: string) (f: RequirementState -> RequirementState) = { s with Requirements = s.Requirements.Add(id, f s.Requirements[id]) }

/// Applies one event. Events that would invent progress are refused, never silently applied: an unknown requirement
/// or obligation, implementation completed that never started, acceptance from anyone but an operator or the
/// independent verifier, or acceptance of a requirement that is not verified.
let apply (s: State) (e: Event) : Result<State, string> =
    let known id = s.Requirements.ContainsKey id
    match e with
    | ImplementationStarted(wi, rs) ->
        match rs |> List.filter (known >> not) with
        | [] -> Ok({ (rs |> List.fold (fun st id -> update st id (fun r -> if r.Implementation = NotStarted then { r with Implementation = InProgress } else r)) s) with StartedItems = s.StartedItems.Add wi; Attempts = s.Attempts.Remove wi })
        | unknown -> Error(sprintf "implementation-started names requirements outside the scope: %s" (String.concat ", " unknown))
    | ImplementationCompleted(rs, _) ->
        match rs |> List.filter (known >> not), rs |> List.filter (fun id -> known id && s.Requirements[id].Implementation = NotStarted) with
        | [], [] -> Ok(rs |> List.fold (fun st id -> update st id (fun r -> { r with Implementation = Implemented })) s)
        | unknown, [] -> Error(sprintf "implementation-completed names requirements outside the scope: %s" (String.concat ", " unknown))
        | _, never -> Error(sprintf "implementation-completed for requirements whose implementation never started: %s" (String.concat ", " never))
    | VerificationRecorded(o, v, _) ->
        match obligationOwner s o with
        | Some id -> Ok(update s id (fun r -> { r with Results = r.Results.Add(o, v) }))
        | None -> Error(sprintf "verification-recorded for an obligation outside the scope: %s" o)
    // A blocked producer changes no implementation state and no recorded verdict.
    | SuiteBlocked(suite, cause) -> Ok { s with BlockedSuites = s.BlockedSuites.Add(suite, cause) }
    | SuiteUnblocked suite -> Ok { s with BlockedSuites = s.BlockedSuites.Remove suite }
    | AttemptFailed(wi, cause) ->
        let n = match s.Attempts.TryFind wi with Some(c, n) when c = cause -> n + 1 | _ -> 1
        Ok { s with Attempts = s.Attempts.Add(wi, (cause, n)) }
    | WorkItemCompleted wi ->
        match arr "workItems" s.Manifest |> List.tryFind (fun w -> str "id" w = wi) with
        | None -> Error(sprintf "work-item-completed for a work item outside the scope: %s" wi)
        | Some w ->
            match strs "requirements" w |> List.filter (fun id -> s.Requirements[id].Implementation <> Implemented) with
            | [] -> Ok { s with CompletedItems = s.CompletedItems.Add wi }
            | open' -> Error(sprintf "work-item-completed for %s while its requirements are not implemented: %s" wi (String.concat ", " open'))
    | AcceptanceRecorded(id, authority, record) ->
        if not (known id) then Error(sprintf "acceptance-recorded for a requirement outside the scope: %s" id)
        elif authority <> "operator" && authority <> "independent-verifier" then Error(sprintf "acceptance of %s by a %s is refused: only the owner (operator) or the independent verifier accepts" id (if authority = "" then "unnamed authority" else authority))
        elif String.IsNullOrWhiteSpace record then Error(sprintf "acceptance of %s names no record" id)
        elif status s id <> Verified then Error(sprintf "acceptance of %s refused: its status is %s, not verified" id (statusName (status s id)))
        else Ok(update s id (fun r -> { r with Accepted = Some record }))

/// Folds the events in order; the first refused event stops the fold and is reported with its position.
let replay (manifest: Json) (events: Json list) : Result<State, string> =
    events
    |> List.indexed
    |> List.fold
        (fun acc (n, j) ->
            acc |> Result.bind (fun s -> eventOf j |> Result.bind (apply s) |> Result.mapError (sprintf "event %d: %s" (n + 1))))
        (Ok(initial manifest))

/// The retry budget: a work item that failed this many times in a row with the same cause is parked, so the agent
/// moves to other work instead of spending compute on a repeated failure.
let retryBudget = 3

let parked (s: State) = s.Attempts |> Map.filter (fun _ (_, n) -> n >= retryBudget) |> Map.toList |> List.map fst |> Set.ofList

// ---- the progress report -----------------------------------------------------------------------------------------

let report (s: State) : Json =
    let reqs = requirementsOf s.Manifest
    let statuses = reqs |> List.map (fun r -> str "id" r, status s (str "id" r))
    let countBy (st: Status) = statuses |> List.filter (snd >> (=) st) |> List.length
    let applicable (r: Json) = str "applicability" r = "always"
    JObj
        [ "schema", JStr "indy-init.progress/1"
          "scopeSha256", JStr(strOpt "scopeSha256" s.Manifest |> Option.defaultValue "")
          "counts",
          JObj(
              [ NotStartedStatus; ImplementationInProgress; VerificationPending; VerificationBlocked; VerificationFailed; Verified; Accepted ]
              |> List.map (fun st -> statusName st, jint (countBy st))
          )
          "mandatoryOpen",
          jint (reqs |> List.filter (fun r -> boolOr "mandatory" false r && applicable r) |> List.filter (fun r -> not (List.contains (status s (str "id" r)) [ Verified; Accepted ])) |> List.length)
          "blockedSuites", JObj(s.BlockedSuites |> Map.toList |> List.map (fun (k, v) -> k, JStr v))
          "parkedWorkItems", jstrs (parked s)
          "requirements",
          JArr(
              reqs
              |> List.map (fun r ->
                  let id = str "id" r
                  let rs = s.Requirements[id]
                  let vs = arr "obligations" r |> List.map (obligationVerification s rs)
                  let n p = vs |> List.filter p |> List.length
                  JObj
                      [ "id", JStr id
                        "status", JStr(statusName (status s id))
                        "implementation", JStr(implementationName rs.Implementation)
                        "verification",
                        JObj
                            [ "pass", jint (n ((=) OPass))
                              "fail", jint (n (function OFail _ -> true | _ -> false))
                              "blocked", jint (n (function OBlocked _ -> true | _ -> false))
                              "pending", jint (n ((=) OPending)) ]
                        "accepted", jopt JStr rs.Accepted ])
          ) ]
