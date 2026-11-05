/// The coverage ledger, the derived slice execution manifest and the two independent gates (adversarial audit R6;
/// correctness-first execution).
///
/// - The ledger folds the progress events (Progress) with the integration events this module adds, and gives each
///   requirement exactly one of eight states. There is no "partial" state: a requirement is integrated or it is not.
/// - The slice manifest is derived from the approved scope; every requirement is assigned to a slice, classified as a
///   full-release (record) obligation, or excluded by an owner decision. Anything else is a refusal.
/// - The slice integration gate decides whether one slice may merge. The full-scope completion gate decides whether
///   the release is complete. A slice PASS is never a release PASS: the completion gate reads only whole-plan,
///   release-mode, governed evidence.
module Ledger

open System
open System.Text.RegularExpressions
open Json
open Scope
open Progress

// ---- ledger state ------------------------------------------------------------------------------------------------

type LedgerState =
    | NotStarted
    | InProgress
    | ImplementedUnverified
    | VerifiedUnmerged
    | Integrated
    | Blocked
    | Failed
    | Excluded

let ledgerStateName =
    function
    | NotStarted -> "not-started"
    | InProgress -> "in-progress"
    | ImplementedUnverified -> "implemented-unverified"
    | VerifiedUnmerged -> "verified-unmerged"
    | Integrated -> "integrated"
    | Blocked -> "blocked"
    | Failed -> "failed"
    | Excluded -> "excluded"

let allLedgerStates = [ NotStarted; InProgress; ImplementedUnverified; VerifiedUnmerged; Integrated; Blocked; Failed; Excluded ]

type State =
    { Progress: Progress.State
      /// requirement -> the main commit that integrated it
      IntegratedAt: Map<string, string>
      /// requirement -> the owner decision that excluded it
      ExcludedBy: Map<string, string>
      /// Some (commit, cause) while main is unhealthy (deployment verification failed after a merge): no merges.
      Unhealthy: (string * string) option }

let ledgerKinds = set [ "integrated"; "excluded"; "main-unhealthy"; "main-healthy" ]

let ledgerState (s: State) (id: string) : LedgerState =
    if s.ExcludedBy.ContainsKey id then Excluded
    else
        match status s.Progress id with
        | NotStartedStatus -> NotStarted
        | ImplementationInProgress -> InProgress
        | VerificationPending -> ImplementedUnverified
        | VerificationBlocked -> Blocked
        | VerificationFailed -> Failed
        | Verified
        | Accepted -> if s.IntegratedAt.ContainsKey id then Integrated else VerifiedUnmerged

/// approvedExclusions: (requirement, decision) pairs from trusted operator records {requirement_id, action: exclude} in
/// the pinned approvals file. An `excluded` event that no such record backs is refused, whatever authority it claims.
let private applyLedger (approvedExclusions: Set<string * string>) (s: State) (j: Json) : Result<State, string> =
    let known id = s.Progress.Requirements.ContainsKey id
    match strOpt "kind" j with
    | Some "integrated" ->
        let rs, commit = strs "requirements" j, strOpt "commit" j |> Option.defaultValue ""
        let unknown = rs |> List.filter (known >> not)
        let unverified = rs |> List.filter (fun id -> known id && not (List.contains (status s.Progress id) [ Verified; Accepted ]))
        if commit = "" then Error "integrated names no main commit"
        elif not unknown.IsEmpty then Error(sprintf "integrated names requirements outside the scope: %s" (String.concat ", " unknown))
        elif not unverified.IsEmpty then Error(sprintf "integration of requirements that are not verified is refused: %s" (String.concat ", " unverified))
        elif s.Unhealthy.IsSome then Error(sprintf "integration while main is unhealthy (%s) is refused: repair or revert first" (snd s.Unhealthy.Value))
        else Ok { s with IntegratedAt = rs |> List.fold (fun m id -> m.Add(id, commit)) s.IntegratedAt }
    | Some "excluded" ->
        let id, decision = strOpt "requirement" j |> Option.defaultValue "", strOpt "decision" j |> Option.defaultValue ""
        let authority = field "authority" j |> Option.bind (strOpt "kind") |> Option.defaultValue ""
        if not (known id) then Error(sprintf "excluded names a requirement outside the scope: %s" id)
        elif authority <> "operator" then Error(sprintf "exclusion of %s by %s is refused: only the owner (operator) excludes scope" id (if authority = "" then "an unnamed authority" else authority))
        elif decision = "" then Error(sprintf "exclusion of %s names no owner decision" id)
        elif not (approvedExclusions.Contains(id, decision)) then Error(sprintf "exclusion of %s (%s) is refused: no trusted operator record {requirement_id: %s, action: exclude} in the pinned approvals backs it" id decision id)
        else Ok { s with ExcludedBy = s.ExcludedBy.Add(id, decision) }
    | Some "main-unhealthy" -> Ok { s with Unhealthy = Some(strOpt "commit" j |> Option.defaultValue "", strOpt "cause" j |> Option.defaultValue "deployment verification failed") }
    | Some "main-healthy" -> Ok { s with Unhealthy = None }
    | k -> Error(sprintf "not a ledger event: %A" k)

/// Folds progress and ledger events in order; the first refused event stops the fold.
let replayWith (approvedExclusions: Set<string * string>) (manifest: Json) (events: Json list) : Result<State, string> =
    let start = { Progress = Progress.initial manifest; IntegratedAt = Map.empty; ExcludedBy = Map.empty; Unhealthy = None }
    events
    |> List.indexed
    |> List.fold
        (fun acc (n, j) ->
            acc
            |> Result.bind (fun s ->
                (if ledgerKinds.Contains(strOpt "kind" j |> Option.defaultValue "") then applyLedger approvedExclusions s j
                 else eventOf j |> Result.bind (Progress.apply s.Progress) |> Result.map (fun p -> { s with Progress = p }))
                |> Result.mapError (sprintf "event %d: %s" (n + 1))))
        (Ok start)

/// Replays with no approved exclusion: any `excluded` event is refused.
let replay (manifest: Json) (events: Json list) = replayWith Set.empty manifest events

// ---- applicability ------------------------------------------------------------------------------------------------

/// A requirement applies always, or when every `fact=value` of its rule holds.
let applies (facts: Map<string, string>) (req: Json) =
    match strOpt "applicability" req |> Option.defaultValue "always" with
    | "always" -> true
    | rule -> rule.Split([| " and " |], StringSplitOptions.None) |> Array.forall (fun c -> match c.Split('=') with [| k; v |] -> facts.TryFind k = Some v | _ -> false)

let private requirements (s: State) = arr "requirements" s.Progress.Manifest

/// The requirement group: the id without its trailing number (PERSIST-001 -> PERSIST, EXT-DATA-013 -> EXT-DATA).
let groupOf (id: string) =
    let m = Regex.Match(id, @"^(.*?)-?[0-9]+(\.[0-9]+)?$")
    if m.Success && m.Groups[1].Value <> "" then m.Groups[1].Value else id

// ---- the machine-readable scope report -------------------------------------------------------------------------------

let scopeReport (s: State) (facts: Map<string, string>) : Json =
    let reqs = requirements s |> List.filter (applies facts)
    let obligationState (req: Json) (o: Json) =
        let id = str "id" req
        let rs = s.Progress.Requirements[id]
        match ledgerState s id, obligationVerification s.Progress rs o with
        | Excluded, _ -> "excluded"
        | NotStarted, _ -> "missing"
        | _, OBlocked _ -> "blocked"
        | _, OFail _ -> "failed"
        | Integrated, OPass -> "integrated"
        | _, OPass -> "verified"
        | (InProgress | ImplementedUnverified | VerifiedUnmerged | Blocked | Failed | Integrated), OPending -> if rs.Implementation = Implemented then "implemented" else "in-progress"
    let group (g: string, members: Json list) =
        let states = members |> List.collect (fun r -> arr "obligations" r |> List.filter (boolOr "mandatory" false) |> List.map (obligationState r))
        let count st = states |> List.filter ((=) st) |> List.length
        // Implemented counts every obligation whose implementation is done, whatever its verification; verified
        // counts every passing one; integrated only those merged to main.
        let implemented = states |> List.filter (fun x -> List.contains x [ "implemented"; "verified"; "integrated"; "failed"; "blocked" ]) |> List.length
        JObj
            [ "group", JStr g
              "requirements", jint members.Length
              "mandatoryRequirements", jint (members |> List.filter (boolOr "mandatory" false) |> List.length)
              "applicableObligations", jint states.Length
              "implemented", jint implemented
              "verified", jint (count "verified" + count "integrated")
              "integrated", jint (count "integrated")
              "missing", jint (count "missing")
              "inProgress", jint (count "in-progress")
              "blocked", jint (count "blocked")
              "failed", jint (count "failed")
              "excluded", jint (count "excluded")
              "states", JObj(allLedgerStates |> List.map (fun st -> ledgerStateName st, jint (members |> List.filter (fun r -> ledgerState s (str "id" r) = st) |> List.length)))
              "workItems", jstrs (members |> List.collect (strs "workItems") |> List.distinct |> List.sort)
              "supportingEvidence", jstrs (members |> List.choose (fun r -> s.IntegratedAt.TryFind(str "id" r)) |> List.distinct)
              "exclusions", JArr(members |> List.choose (fun r -> s.ExcludedBy.TryFind(str "id" r) |> Option.map (fun d -> JObj [ "requirement", JStr(str "id" r); "decision", JStr d ]))) ]
    let groups = reqs |> List.groupBy (str "id" >> groupOf) |> List.sortBy fst
    let mandatoryOpen = reqs |> List.filter (fun r -> boolOr "mandatory" false r && not (List.contains (ledgerState s (str "id" r)) [ Integrated; Excluded ]))
    JObj
        [ "schema", JStr "indy-init.scope-report/1"
          "scopeSha256", JStr(strOpt "scopeSha256" s.Progress.Manifest |> Option.defaultValue "")
          "mainHealthy", JBool s.Unhealthy.IsNone
          "states", JObj(allLedgerStates |> List.map (fun st -> ledgerStateName st, jint (reqs |> List.filter (fun r -> ledgerState s (str "id" r) = st) |> List.length)))
          "mandatoryApplicable", jint (reqs |> List.filter (boolOr "mandatory" false) |> List.length)
          "mandatoryOpen", jint mandatoryOpen.Length
          "completion", JStr(if mandatoryOpen.IsEmpty then "SCOPE-INTEGRATED" else "CONTINUE_REQUIRED")
          "groups", JArr(groups |> List.map group)
          "requirements",
          JArr(
              reqs
              |> List.map (fun r ->
                  let id = str "id" r
                  JObj
                      [ "id", JStr id
                        "mandatory", JBool(boolOr "mandatory" false r)
                        "state", JStr(ledgerStateName (ledgerState s id))
                        "workItems", jstrs (strs "workItems" r)
                        "integratedAt", jopt JStr (s.IntegratedAt.TryFind id)
                        "excludedBy", jopt JStr (s.ExcludedBy.TryFind id) ])
          ) ]

// ---- the derived slice execution manifest ---------------------------------------------------------------------------

/// Producers that need the real provider (GitHub) rather than a deterministic double.
let liveProducers = set [ "SU-DOTNET-LIVE"; "SU-BROWSER-LIVE" ]

/// Requirement groups whose slices carry Forma, design-contract and accessibility obligations.
let uiGroups = set [ "UI"; "INV"; "WDL"; "WASM-LOAD"; "EXT-DATA" ]

/// The governed journeys and the requirements each one binds (verification/journeys/bindings.json is the source; the
/// slice manifest copies it so a slice names the journeys that judge it).
type Journey = { Id: string; Requirements: string list; Assertions: (string * string list) list }

let journeysOf (bindings: Json) : Journey list =
    arr "journeys" bindings
    |> List.map (fun j ->
        let assertions = arr "assertions" j |> List.filter (fun a -> strOpt "priority" a = Some "P0") |> List.map (fun a -> str "id" a, strs "requirements" a)
        { Id = str "id" j; Requirements = assertions |> List.collect snd |> List.distinct; Assertions = assertions })

let mergeConditions =
    [ "every mandatory applicable obligation of the slice passes in the governed gate at the merged result"
      "every dependency slice is integrated"
      "every governed journey assertion bound to the slice passes in Chromium and WebKit, except one whose needed slices are not all present yet (their merge-provable obligations verified at the merged result; docs/rehearsal-010-readiness/assertion-merge-needs.json, ODR-R10-33 A): the completion gate judges that one at release"
      "every previously integrated obligation still passes (regression)"
      "the application builds and starts at the merged result"
      "required real-provider checks pass against api.github.com"
      "the merged result is the candidate merged onto the current target branch"
      "no mandatory security, accessibility or design-contract check fails"
      "no test was removed, skipped or weakened" ]

/// Derives the slice manifest. `estimates` maps execution-DAG task ids to their estimated minutes.
let sliceManifest (manifest: Json) (estimates: Map<string, int64>) (journeys: Journey list) (s: State option) : Json * Refusal list =
    let reqs = arr "requirements" manifest
    let items = arr "workItems" manifest
    let reqById = reqs |> List.map (fun r -> str "id" r, r) |> Map.ofList
    let excluded = s |> Option.map (fun st -> st.ExcludedBy) |> Option.defaultValue Map.empty
    let classify (r: Json) =
        let id = str "id" r
        if excluded.ContainsKey id then "excluded"
        elif not (strs "workItems" r).IsEmpty then "slice"
        elif arr "obligations" r |> List.forall (fun o -> strOpt "kind" o = Some "record") then "full-release"
        else "unassigned"
    let unassigned = reqs |> List.filter (classify >> (=) "unassigned") |> List.map (str "id")
    let danglingItems = reqs |> List.collect (fun r -> strs "workItems" r |> List.filter (fun w -> not (items |> List.exists (fun x -> str "id" x = w))) |> List.map (fun w -> str "id" r, w))
    let refusals =
        [ for id in unassigned do yield { Code = "REQUIREMENT_UNASSIGNED"; Detail = sprintf "%s has executable obligations but no slice, no full-release classification and no owner exclusion" id }
          for id, w in danglingItems do yield { Code = "WORK_ITEM_UNKNOWN"; Detail = sprintf "%s names work item %s, which the scope does not register" id w } ]
    let slice (w: Json) =
        let wid = str "id" w
        let rs = strs "requirements" w |> List.choose reqById.TryFind
        let obligations = rs |> List.collect (arr "obligations")
        let producers = obligations |> List.collect (strs "producers") |> List.distinct |> List.sort
        let rids = rs |> List.map (str "id")
        let status =
            match s with
            | None -> "not-started"
            | Some st ->
                let states = rids |> List.map (ledgerState st)
                if rids.IsEmpty then (if st.Progress.CompletedItems.Contains wid then "integrated" else "not-started")
                elif states |> List.forall (fun x -> x = Integrated || x = Excluded) then "integrated"
                elif states |> List.contains Failed then "failed"
                elif states |> List.contains Blocked then "blocked"
                elif states |> List.forall (fun x -> List.contains x [ VerifiedUnmerged; Integrated; Excluded ]) then "verified-unmerged"
                elif states |> List.forall ((=) NotStarted) then "not-started"
                elif states |> List.exists (fun x -> x = InProgress || x = NotStarted) then "in-progress"
                else "implemented-unverified"
        JObj
            [ "id", JStr("SL-" + wid)
              "workItem", JStr wid
              "title", JStr(strOpt "title" w |> Option.defaultValue "")
              "requirements", jstrs rids
              "gateRequirements", jstrs (strs "gateRequirements" w)
              "obligations", jstrs (obligations |> List.map (str "id"))
              "mandatoryObligations", jint (obligations |> List.filter (boolOr "mandatory" false) |> List.length)
              "dependsOn", jstrs (strs "dependsOn" w |> List.map ((+) "SL-"))
              "implementationBoundary", JStr(strOpt "owner" w |> Option.defaultValue "integration")
              "acceptanceTests", jstrs (obligations |> List.collect (strs "tests") |> List.distinct |> List.sort)
              "producers", jstrs producers
              "governedJourneys", jstrs (journeys |> List.filter (fun j -> j.Requirements |> List.exists (fun x -> List.contains x rids)) |> List.map (fun j -> j.Id))
              // Only the P0 assertions bound to this slice's requirements judge it: another slice's assertion failing in the
              // same journey never blocks this one.
              "governedAssertions", jstrs (journeys |> List.collect (fun j -> j.Assertions) |> List.filter (fun (_, rs) -> rs |> List.exists (fun x -> List.contains x rids)) |> List.map fst)
              "regressionTests", JStr "every obligation integrated before this slice (computed at the gate from the ledger)"
              "realProvider", JBool(producers |> List.exists liveProducers.Contains)
              "formaAndAccessibility", JBool(rids |> List.exists (groupOf >> uiGroups.Contains))
              "mergeConditions", jstrs mergeConditions
              "estimateMinutes", (match estimates.TryFind wid with Some m -> JInt m | None -> JNull)
              "estimateSource", JStr(if estimates.ContainsKey wid then "execution-dag" else "unestimated: generated work item (no DAG estimate)")
              "status", JStr status ]
    let classified = reqs |> List.map (fun r -> str "id" r, classify r)
    let count c = classified |> List.filter (snd >> (=) c) |> List.length
    JObj
        [ "schema", JStr "indy-init.slice-execution-manifest/1"
          "scopeSha256", JStr(strOpt "scopeSha256" manifest |> Option.defaultValue "")
          "derivedFrom", JStr "the approved scope manifest; requirements are not rewritten"
          "coverage",
          JObj
              [ "requirements", jint reqs.Length
                "inSlices", jint (count "slice")
                "fullRelease", jint (count "full-release")
                "excluded", jint (count "excluded")
                "unassigned", jint (count "unassigned")
                "complete", JBool(refusals.IsEmpty) ]
          "fullReleaseObligations", jstrs (classified |> List.filter (snd >> (=) "full-release") |> List.map fst)
          "crossCutting",
          JObj
              [ "designContract", JStr "verification/design-contract (every UI-bearing slice and the release)"
                "journeys", jstrs (journeys |> List.map (fun j -> j.Id))
                "regression", JStr "every integrated obligation, at every merge" ]
          "slices", JArr(items |> List.map slice) ],
    refusals

// ---- governed verification: the only authority for integration, regression and completion ------------------------

/// The obligations governed evidence shows verified: the plan coverage's `verified` obligations (Dokimos, computed by
/// the governed gate), the governed catalog verification's PASS obligations (and NOT_APPLICABLE within a requirement
/// that is not applicable as a whole), and, when the release gate judged record evidence and passed, the plan's
/// record-only obligations. Progress events written by the builder never add to it.
let governedVerified (manifest: Json) (coverage: Json option) (catalogVerification: Json option) (recordsPassed: bool) : Set<string> =
    let plan = coverage |> Option.map (arr "obligations" >> List.filter (fun o -> strOpt "status" o = Some "verified") >> List.map (str "id")) |> Option.defaultValue []
    let catalog =
        catalogVerification
        |> Option.map (arr "requirements" >> List.collect (fun r -> arr "obligations" r |> List.filter (fun o -> strOpt "status" o = Some "PASS" || (strOpt "status" o = Some "NOT_APPLICABLE" && strOpt "status" r = Some "NOT_APPLICABLE")) |> List.map (str "id")))
        |> Option.defaultValue []
    let records =
        if not recordsPassed then []
        else arr "requirements" manifest |> List.collect (arr "obligations") |> List.filter (fun o -> strOpt "kind" o = Some "record") |> List.map (str "id")
    Set.ofList (plan @ catalog @ records)

/// A requirement is integrated when every mandatory obligation is governed-verified at the judged commit, or the owner
/// excluded it (an approved exclusion record).
let governedIntegrated (s: State) (verified: Set<string>) (req: Json) =
    s.ExcludedBy.ContainsKey(str "id" req)
    || arr "obligations" req |> List.filter (boolOr "mandatory" true) |> List.forall (fun o -> verified.Contains(str "id" o))

// ---- the slice integration gate --------------------------------------------------------------------------------------

/// What a slice merge is judged on. Every artifact names the candidate it judged; the gate compares it with the
/// merged result, never with a SHA the builder asserts.
type SliceEvidence =
    { Slice: Json
      /// The merged result: the candidate merged onto the current target.
      Candidate: string
      /// The target branch head the merged result was built on, and the target's head now.
      TargetBase: string
      TargetHead: string
      Gate: Json option
      Journeys: Json option
      DesignContract: Json option
      Regression: Json option
      Build: Json option
      LiveProvider: Json option
      TestIntegrity: Json option
      /// The governed catalog acceptance at the merged result (catalog slices).
      CatalogAcceptance: Json option
      /// CF-07 option A (owner decision ODR-R10-33 A): for each bound assertion, the slices its journey steps need on
      /// main. An assertion whose needed slices (other than this one, and in the scope) are not all present for judging
      /// (deferredAssertions) is not judged at this merge; the completion gate judges every assertion at release. Empty = every bound
      /// assertion is judged at every merge (the rule before ODR-R10-33).
      AssertionNeeds: Map<string, string list> }

type SliceRule = { Id: string; Check: State -> SliceEvidence -> Refusal list }

let private ref' code detail = { Code = code; Detail = detail }
let private candidateOf (j: Json) = strOpt "candidate" j |> Option.defaultValue ""

let private atCandidate (name: string) (e: SliceEvidence) (j: Json option) (ok: Json -> Refusal list) =
    match j with
    | None -> [ ref' (name + "_MISSING") (sprintf "no %s evidence for the merged result %s" (name.ToLowerInvariant().Replace('_', ' ')) e.Candidate) ]
    | Some x when candidateOf x <> e.Candidate -> [ ref' (name + "_STALE") (sprintf "the %s evidence is for %s, not the merged result %s" (name.ToLowerInvariant().Replace('_', ' ')) (candidateOf x) e.Candidate) ]
    | Some x -> ok x

/// Whether the work item behind slice SL-<d> is integrated: every requirement's judged mandatory obligations passed at
/// the merged result's regression run (or the requirement is excluded), or, for a work item with nothing a pull request
/// can judge, its Praxis record completed it.
let private workItemIntegrated (s: State) (e: SliceEvidence) (d: string) : bool =
    let items = arr "workItems" s.Progress.Manifest |> List.map (fun w -> str "id" w, strs "requirements" w) |> Map.ofList
    let rs = items.TryFind d |> Option.defaultValue []
    let headVerified = e.Regression |> Option.map (strs "passed" >> Set.ofList) |> Option.defaultValue Set.empty
    let reqs = arr "requirements" s.Progress.Manifest |> List.map (fun r -> str "id" r, r) |> Map.ofList
    // A dependency without requirements is a timed task; it completes by its Praxis record.
    let runsLive = boolOr "realProvider" false e.Slice
    let judgedHere (o: Json) =
        strOpt "kind" o <> Some "record"
        && (runsLive || not (strs "producers" o |> List.exists (fun p -> liveProducers.Contains p || p = "SU-BROWSER-WEBKIT")))
    // A requirement with nothing a pull request can judge (all record, or live in a non-live slice) is not
    // integrated vacuously: its work item must be completed by its Praxis record.
    let integratedReq (r: Json) =
        let judged = arr "obligations" r |> List.filter (fun o -> boolOr "mandatory" true o && judgedHere o)
        s.ExcludedBy.ContainsKey(str "id" r)
        || (if judged.IsEmpty then s.Progress.CompletedItems.Contains d else judged |> List.forall (fun o -> headVerified.Contains(str "id" o)))
    if rs.IsEmpty then s.Progress.CompletedItems.Contains d else rs |> List.forall (fun r -> reqs.TryFind r |> Option.exists integratedReq)

/// An assertion -> needed-slices file (indy-init.r10-contract-first-ui.merge-dependencies/1: { "rules": [ { "assertions":
/// [..], "needs": [..] } ] }) as a map; an assertion named by several rules needs the union.
let assertionNeedsOf (j: Json) : Map<string, string list> =
    arr "rules" j
    |> List.collect (fun r -> strs "assertions" r |> List.map (fun a -> a, strs "needs" r))
    |> List.groupBy fst |> List.map (fun (a, xs) -> a, xs |> List.collect snd |> List.distinct) |> Map.ofList

/// Whether a needed work item counts as present for CF-07 deferral. Unlike workItemIntegrated, this leans toward
/// JUDGING (review CF07-1, CF07-2): only obligations that any merge's regression proves count (mandatory, not
/// record-only, not live-only, not WebKit-only), the builder's own Praxis records are ignored, and a requirement or
/// work item with no such obligation counts as present. So a builder cannot defer an assertion by withholding a
/// completion event or by breaking a needed slice's live or WebKit obligations.
let private presentForJudging (s: State) (e: SliceEvidence) (d: string) : bool =
    let items = arr "workItems" s.Progress.Manifest |> List.map (fun w -> str "id" w, strs "requirements" w) |> Map.ofList
    let headVerified = e.Regression |> Option.map (strs "passed" >> Set.ofList) |> Option.defaultValue Set.empty
    let reqs = arr "requirements" s.Progress.Manifest |> List.map (fun r -> str "id" r, r) |> Map.ofList
    // Catalog (INV-*) obligations reach the regression's passed set only through the governed catalog acceptance at the
    // candidate. They count only when that acceptance is for this candidate and has no binding refusal (wrong commit,
    // digest, producer, contract, facts, probe spec); otherwise a builder could withhold the catalog to defer
    // (review CF07-R2-1), so they are treated as unprovable and the assertion is judged.
    let catalogTrusted =
        match e.CatalogAcceptance with
        | Some c when candidateOf c = e.Candidate ->
            arr "refusals" c |> List.forall (fun r -> List.contains (str "code" r) [ "CATALOG_OBLIGATION_FAILED"; "FAULT_INSENSITIVE"; "CATALOG_NOT_ACCEPTED" ])
        | _ -> false
    let provable (o: Json) =
        boolOr "mandatory" true o && strOpt "kind" o <> Some "record"
        && not (strs "producers" o |> List.exists (fun p -> liveProducers.Contains p || p = "SU-BROWSER-WEBKIT"))
        && (catalogTrusted || not ((str "id" o).StartsWith "INV-"))
    items.TryFind d |> Option.defaultValue []
    |> List.forall (fun rid ->
        s.ExcludedBy.ContainsKey rid
        || (reqs.TryFind rid |> Option.map (fun r -> arr "obligations" r |> List.filter provable |> List.forall (fun o -> headVerified.Contains(str "id" o))) |> Option.defaultValue true))

/// CF-07 option A: the bound assertions this merge does not judge, because a slice they need is not present yet.
/// A needed slice outside the scope's work items is ignored (the needs file names approved and successor ids alike).
let deferredAssertions (s: State) (e: SliceEvidence) : string list =
    let self = str "id" e.Slice
    let inScope = arr "workItems" s.Progress.Manifest |> List.map (fun w -> "SL-" + str "id" w) |> Set.ofList
    strs "governedAssertions" e.Slice
    |> List.filter (fun a ->
        Map.tryFind a e.AssertionNeeds |> Option.defaultValue []
        |> List.filter (fun n -> n <> self && inScope.Contains n)
        |> List.exists (fun n -> not (presentForJudging s e (n.Substring 3))))

let sliceRules : SliceRule list =
    [ { Id = "main-healthy"
        Check = fun s _ -> match s.Unhealthy with Some(c, why) -> [ ref' "MAIN_UNHEALTHY" (sprintf "main is unhealthy at %s (%s); no merge until it is repaired or reverted" c why) ] | None -> [] }
      { Id = "dependencies-integrated"
        Check =
          fun s e ->
              let deps = strs "dependsOn" e.Slice |> List.map (fun d -> d.Substring 3)
              [ for d in deps do
                    if not (workItemIntegrated s e d) then yield ref' "DEPENDENCY_NOT_INTEGRATED" (sprintf "dependency SL-%s is not integrated" d) ] }
      { Id = "current-target"
        Check = fun _ e -> if e.TargetBase <> e.TargetHead || e.TargetHead = "" then [ ref' "STALE_TARGET" (sprintf "the merged result was built on %s; the target is now %s: re-merge and re-verify" e.TargetBase e.TargetHead) ] else [] }
      { Id = "governed-gate"
        Check =
          fun _ e ->
              atCandidate "GATE" e e.Gate (fun g ->
                  let scope = strs "scope" g |> Set.ofList
                  let missing = strs "requirements" e.Slice |> List.filter (scope.Contains >> not)
                  [ if strOpt "workItem" g <> strOpt "workItem" e.Slice then yield ref' "GATE_OTHER_WORK_ITEM" (sprintf "the gate judged work item %s, not %s" (strOpt "workItem" g |> Option.defaultValue "(none)") (str "workItem" e.Slice))
                    if not missing.IsEmpty then yield ref' "GATE_SCOPE_INCOMPLETE" (sprintf "the gate's scope omits %s" (String.concat ", " missing))
                    for c in strs "causes" g do yield ref' "MANDATORY_OBLIGATION_UNVERIFIED" c
                    if strOpt "verdict" g <> Some "PASS" && (strs "causes" g).IsEmpty then yield ref' "GATE_NOT_PASS" (sprintf "the gate verdict is %s, not PASS" (strOpt "verdict" g |> Option.defaultValue "(none)")) ]) }
      { Id = "journeys"
        Check =
          fun s e ->
              let deferred = deferredAssertions s e |> Set.ofList
              let bound = strs "governedAssertions" e.Slice |> List.filter (deferred.Contains >> not)
              if bound.IsEmpty then []
              else
                  atCandidate "JOURNEYS" e e.Journeys (fun j ->
                      let results = arr "results" j
                      [ if strOpt "baseUrl" (field "provider" j |> Option.defaultValue (JObj [])) <> Some "https://api.github.com" then
                            yield ref' "LIVE_PROVIDER_SUBSTITUTED" "the journeys did not run against api.github.com"
                        for id in bound do
                            let journey = id.Substring(0, id.IndexOf '-')
                            for engine in [ "chromium"; "webkit" ] do
                                match results |> List.tryFind (fun r -> strOpt "journey" r = Some journey && strOpt "engine" r = Some engine) with
                                | None -> yield ref' "JOURNEY_NOT_RUN" (sprintf "%s was not run on %s" journey engine)
                                | Some r ->
                                    match arr "assertions" r |> List.tryFind (fun a -> strOpt "id" a = Some id) with
                                    | Some a when strOpt "status" a = Some "passed" -> ()
                                    | Some a -> yield ref' "JOURNEY_FAILED" (sprintf "%s failed on %s: %s" id engine (strOpt "detail" a |> Option.defaultValue ""))
                                    | None -> yield ref' "JOURNEY_NOT_RUN" (sprintf "%s did not run on %s (an earlier step of %s failed)" id engine journey) ]) }
      { Id = "regression"
        Check =
          fun s e ->
              atCandidate "REGRESSION" e e.Regression (fun r ->
                  let passed = strs "passed" r |> Set.ofList
                  match field "required" r with
                  | None -> [ ref' "REGRESSION_BASE_MISSING" "the regression evidence has no governed verification of the target base (required)" ]
                  | Some _ ->
                      [ for o in strs "required" r do
                            if not (passed.Contains o) then yield ref' "REGRESSION" (sprintf "obligation %s is verified at the target base and not at the merged result" o) ]) }
      { Id = "build-and-start"
        Check =
          fun _ e ->
              atCandidate "BUILD" e e.Build (fun b ->
                  [ if not (boolOr "built" false b) then yield ref' "BUILD_FAILED" "the application does not build"
                    if not (boolOr "started" false b) then yield ref' "START_FAILED" "the application does not start" ]) }
      { Id = "real-provider"
        Check =
          fun _ e ->
              if not (boolOr "realProvider" false e.Slice) then []
              else
                  atCandidate "LIVE_PROVIDER" e e.LiveProvider (fun l ->
                      [ if strOpt "baseUrl" l <> Some "https://api.github.com" then yield ref' "LIVE_PROVIDER_SUBSTITUTED" (sprintf "the real-provider check ran against %s, not api.github.com; a double is not real-provider verification" (strOpt "baseUrl" l |> Option.defaultValue "(unknown)"))
                        if strOpt "status" l <> Some "passed" then yield ref' "LIVE_PROVIDER_FAILED" (sprintf "the real-provider check is %s" (strOpt "status" l |> Option.defaultValue "missing")) ]) }
      { Id = "design-contract"
        Check =
          fun _ e ->
              if not (boolOr "formaAndAccessibility" false e.Slice) then []
              else atCandidate "DESIGN_CONTRACT" e e.DesignContract (fun d -> [ for c in strs "causes" d do yield ref' "DESIGN_CONTRACT" c ] @ (if strOpt "verdict" d = Some "PASS" then [] else [ ref' "DESIGN_CONTRACT" "the design contract did not pass" ])) |> List.distinctBy (fun r -> r.Detail) }
      { Id = "catalog"
        Check =
          fun _ e ->
              // A slice carrying catalog requirements needs the governed catalog acceptance at the merged result: every binding
              // refusal counts (wrong commit, digest, producer, contract, facts, probe spec), and an obligation refusal counts
              // when it names one of this slice's obligations; other slices' unfinished catalog work does not block it.
              let mine = strs "obligations" e.Slice |> List.filter (fun o -> o.StartsWith "INV-")
              if mine.IsEmpty then []
              else
                  atCandidate "CATALOG" e e.CatalogAcceptance (fun c ->
                      let perObligation = set [ "CATALOG_OBLIGATION_FAILED"; "FAULT_INSENSITIVE" ]
                      [ for r in arr "refusals" c do
                            let code, detail = str "code" r, str "detail" r
                            if not (perObligation.Contains code) && code <> "CATALOG_NOT_ACCEPTED" then yield ref' code detail
                            elif perObligation.Contains code && mine |> List.exists (fun o -> detail.StartsWith(o + " ") || detail.StartsWith(o + ":")) then yield ref' code detail ]) }
      { Id = "test-integrity"
        Check = fun _ e -> atCandidate "TEST_INTEGRITY" e e.TestIntegrity (fun t -> arr "refusals" t |> List.map (fun r -> ref' (str "code" r) (str "detail" r))) } ]

let sliceGateWith (rules: SliceRule list) (s: State) (e: SliceEvidence) : Refusal list = rules |> List.collect (fun r -> r.Check s e)

let sliceGate s e = sliceGateWith sliceRules s e

let sliceVerdictJson (deferred: string list) (e: SliceEvidence) (refusals: Refusal list) =
    JObj
        [ "schema", JStr "indy-init.slice-gate/1"
          "slice", JStr(str "id" e.Slice)
          "candidate", JStr e.Candidate
          "verdict", JStr(if refusals.IsEmpty then "PASS" else "REFUSED")
          "claimsRelease", JBool false
          // CF-07 option A: assertions not judged at this merge; the completion gate judges them at release
          "deferredToRelease", JArr(deferred |> List.map JStr)
          "refusals", JArr(refusals |> List.map refusalJson) ]

// ---- the full-scope completion gate ----------------------------------------------------------------------------------

type ReleaseEvidence =
    { Candidate: string
      ReleaseGate: Json option
      CatalogAcceptance: Json option
      Journeys: Json option
      DesignContract: Json option
      Facts: Map<string, string>
      /// The release gate's governed coverage (whole plan) and the governed catalog verification at the candidate.
      Coverage: Json option
      CatalogVerification: Json option
      /// The approved plan's frozenPlanSha256 the release gate must have judged, when pinned.
      PlanSha256: string option
      /// The journeys and their P0 assertions (verification/journeys/bindings.json).
      JourneyBindings: Journey list }

type CompletionRule = { Id: string; Check: State -> ReleaseEvidence -> Refusal list }

let completionRules : CompletionRule list =
    [ { Id = "release-gate"
        Check =
          fun _ e ->
              match e.ReleaseGate with
              | None -> [ ref' "RELEASE_GATE_MISSING" "no release-mode gate verdict for the candidate; the full-release gate has not executed" ]
              | Some g ->
                  [ if candidateOf g <> e.Candidate then yield ref' "RELEASE_GATE_STALE" (sprintf "the gate verdict is for %s, not %s" (candidateOf g) e.Candidate)
                    if strOpt "mode" g <> Some "release" then yield ref' "SLICE_VERDICT_IS_NOT_RELEASE" (sprintf "the gate ran in %s mode; only a release-mode verdict over the whole plan completes the release" (strOpt "mode" g |> Option.defaultValue "(unknown)"))
                    if not (strs "scope" g).IsEmpty || (strOpt "workItem" g |> Option.defaultValue "") <> "" then yield ref' "SLICE_VERDICT_IS_NOT_RELEASE" "the gate verdict is scoped to a slice or work item; a slice PASS is never a release PASS"
                    for c in strs "causes" g do yield ref' "RELEASE_GATE_REFUSED" c
                    if strOpt "verdict" g <> Some "PASS" then yield ref' "RELEASE_GATE_NOT_PASS" (sprintf "the release gate verdict is %s, not PASS" (strOpt "verdict" g |> Option.defaultValue "(none)"))
                    match e.PlanSha256 with
                    | Some pinned when strOpt "frozenPlanSha256" g <> Some pinned -> yield ref' "RELEASE_GATE_OTHER_PLAN" (sprintf "the release gate judged plan %s, not the approved %s" (strOpt "frozenPlanSha256" g |> Option.defaultValue "(none)") pinned)
                    | _ -> () ] }
      { Id = "catalog"
        Check =
          fun _ e ->
              match e.CatalogAcceptance with
              | None -> [ ref' "CATALOG_ACCEPTANCE_MISSING" "no governed catalog acceptance for the candidate" ]
              | Some c ->
                  [ if candidateOf c <> e.Candidate then yield ref' "CATALOG_ACCEPTANCE_STALE" (sprintf "the catalog acceptance is for %s" (candidateOf c))
                    if strOpt "level" c <> Some "GOVERNED-ACCEPTED" then yield ref' "CATALOG_NOT_GOVERNED_ACCEPTED" (sprintf "the catalog level is %s" (strOpt "level" c |> Option.defaultValue "(none)")) ] }
      { Id = "journeys"
        Check =
          fun _ e ->
              match e.Journeys with
              | None -> [ ref' "JOURNEYS_MISSING" "the governed journeys did not run for the candidate" ]
              | Some j ->
                  let results = arr "results" j
                  [ if candidateOf j <> e.Candidate then yield ref' "JOURNEYS_STALE" (sprintf "the journey results are for %s" (candidateOf j))
                    if strOpt "baseUrl" (field "provider" j |> Option.defaultValue (JObj [])) <> Some "https://api.github.com" then yield ref' "LIVE_PROVIDER_SUBSTITUTED" "the journeys did not run against api.github.com"
                    if e.JourneyBindings.IsEmpty then yield ref' "JOURNEY_BINDINGS_MISSING" "the completion gate was given no journey bindings"
                    for journey in e.JourneyBindings do
                        for engine in [ "chromium"; "webkit" ] do
                            match results |> List.tryFind (fun r -> strOpt "journey" r = Some journey.Id && strOpt "engine" r = Some engine) with
                            | None -> yield ref' "JOURNEY_NOT_RUN" (sprintf "%s did not run on %s" journey.Id engine)
                            | Some r ->
                                if strOpt "status" r <> Some "passed" then yield ref' "JOURNEY_FAILED" (sprintf "%s on %s: %s" journey.Id engine (strOpt "status" r |> Option.defaultValue ""))
                                for (aid, _) in journey.Assertions do
                                    match arr "assertions" r |> List.tryFind (fun a -> strOpt "id" a = Some aid) with
                                    | Some a when strOpt "status" a = Some "passed" -> ()
                                    | Some _ -> yield ref' "JOURNEY_FAILED" (sprintf "%s failed on %s" aid engine)
                                    | None -> yield ref' "JOURNEY_NOT_RUN" (sprintf "%s has no recorded result on %s" aid engine) ] }
      { Id = "design-contract"
        Check =
          fun _ e ->
              match e.DesignContract with
              | None -> [ ref' "DESIGN_CONTRACT_MISSING" "the design contract did not run for the candidate" ]
              | Some d -> [ if candidateOf d <> e.Candidate then yield ref' "DESIGN_CONTRACT_STALE" "the design-contract verdict is for another commit"
                            if strOpt "verdict" d <> Some "PASS" then yield ref' "DESIGN_CONTRACT_FAILED" (String.concat "; " (strs "causes" d |> List.truncate 5)) ] }
      { Id = "scope-integrated"
        Check =
          fun s e ->
              let recordsPassed = e.ReleaseGate |> Option.exists (fun g -> strOpt "verdict" g = Some "PASS" && (strs "causes" g).IsEmpty && strOpt "mode" g = Some "release")
              let verified = governedVerified s.Progress.Manifest e.Coverage e.CatalogVerification recordsPassed
              let open' = arr "requirements" s.Progress.Manifest |> List.filter (fun r -> boolOr "mandatory" false r && applies e.Facts r && not (governedIntegrated s verified r))
              [ for name, j in [ "coverage", e.Coverage; "catalog verification", e.CatalogVerification ] do
                    match j with
                    | Some x when strOpt "candidate" x <> Some e.Candidate -> yield ref' "EVIDENCE_STALE" (sprintf "the %s is for %s, not %s" name (strOpt "candidate" x |> Option.defaultValue "(none)") e.Candidate)
                    | _ -> ()
                if e.Coverage.IsNone then yield ref' "COVERAGE_MISSING" "no governed coverage of the whole plan at the candidate; integration is judged only from governed evidence, never from progress events"
                if e.CatalogVerification.IsNone then yield ref' "CATALOG_VERIFICATION_MISSING" "no governed catalog verification at the candidate"
                if not open'.IsEmpty then
                    yield ref' "SCOPE_NOT_INTEGRATED" (sprintf "%d applicable mandatory requirement(s) are not governed-verified at the candidate, for example %s" open'.Length (open' |> List.truncate 6 |> List.map (str "id") |> String.concat ", ")) ] } ]

let completionGateWith (rules: CompletionRule list) (s: State) (e: ReleaseEvidence) = rules |> List.collect (fun r -> r.Check s e)

let completionGate s e = completionGateWith completionRules s e

let completionJson (e: ReleaseEvidence) (refusals: Refusal list) =
    JObj
        [ "schema", JStr "indy-init.completion-gate/1"
          "candidate", JStr e.Candidate
          "verdict", JStr(if refusals.IsEmpty then "COMPLETE" else "INCOMPLETE")
          "refusals", JArr(refusals |> List.map refusalJson) ]

// ---- test integrity: no test removed, skipped or weakened to get green ------------------------------------------------

type TestDiffLine = { File: string; Added: bool; Text: string }

let private isTestFile (f: string) =
    let name = f.Substring(f.LastIndexOf('/') + 1)
    [ "tests/"; "test/"; "__tests__/"; "spec/" ] |> List.exists (fun d -> f.StartsWith(d, StringComparison.Ordinal) || f.Contains("/" + d))
    || [ ".spec."; ".test."; "_test."; "-test." ] |> List.exists name.Contains
    || [ "Tests.fs"; "Test.fs"; "Tests.cs"; "Test.cs" ] |> List.exists (fun x -> name.EndsWith(x, StringComparison.Ordinal))

let private skipPattern = Regex(@"\b(test|it|describe)\.(skip|fixme|only)\s*\(|Skip\s*=\s*""|\[<\s*Fact\s*\(\s*Skip|\[<\s*Ignore|\bxit\s*\(|\bxdescribe\s*\(|test\.fail\s*\(")
let private assertionPattern = Regex(@"\bexpect\s*\(|\bAssert\.|\bshould\b|\btoBe|\btoHave|\btoEqual|\bassert\b")

/// Compares the test identities recorded at the base (what was integrated) with those at the head, and the changed
/// lines of test files. A removed identity, an added skip/only marker or a net loss of assertions in a test file is a
/// refusal. Governed acceptance assets (verification/ in Indy-init) are never in the candidate, so the builder cannot
/// edit them at all.
let testIntegrity (baseTests: Set<string>) (headTests: Set<string>) (diff: TestDiffLine list) : Refusal list =
    let removed = Set.difference baseTests headTests
    let testDiff = diff |> List.filter (fun d -> isTestFile d.File)
    let skips = testDiff |> List.filter (fun d -> d.Added && skipPattern.IsMatch d.Text)
    let assertionLoss =
        testDiff
        |> List.groupBy (fun d -> d.File)
        |> List.choose (fun (f, ls) ->
            let n added = ls |> List.filter (fun d -> d.Added = added && assertionPattern.IsMatch d.Text) |> List.length
            if n false > n true then Some(f, n false - n true) else None)
    [ for t in removed do yield ref' "TEST_REMOVED" (sprintf "test %s passed at the base and is absent at the head" t)
      for d in skips do yield ref' "TEST_SKIPPED" (sprintf "%s adds a skip/only marker: %s" d.File (d.Text.Trim()))
      for f, n in assertionLoss do yield ref' "TEST_WEAKENED" (sprintf "%s loses %d assertion(s) net" f n) ]

let testIntegrityJson (candidate: string) (refusals: Refusal list) =
    JObj [ "schema", JStr "indy-init.test-integrity/1"; "candidate", JStr candidate; "verdict", JStr(if refusals.IsEmpty then "PASS" else "REFUSED"); "refusals", JArr(refusals |> List.map refusalJson) ]
