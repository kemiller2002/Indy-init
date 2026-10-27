/// Phase Two: the verification plan (`verification-plan.json`, schema indy-init.verification-plan/1).
///
/// Pure functions only. The plan is derived from three authorities and nothing else:
///   - the requirements baseline (praxis.requirements-baseline/1): ids, priorities and verbatim obligations;
///   - the generated verification manifest: gates, rules, acceptance links and dependencies;
///   - the curation file `curation/verification-plan.json`: freshness policies, test bindings, exclusions
///     and the pinned approvals source that authorizes exclusions.
/// A test identity is never invented: an obligation without a curated binding is reported `missing`.
module VerificationPlan

open System
open System.Text
open Json

let schemaId = "indy-init.verification-plan/1"

type TestRef = { Id: string; Kind: string }

type Approvals =
    { Repository: string
      Commit: string
      Path: string
      Sha256: string
      TrustedOperators: string list
      Document: Json }

type Inputs =
    { Repository: string
      Commit: string
      BaselineId: string
      BaselineSha256: string
      Baseline: Json
      Manifest: Json
      Spec: Json
      Approvals: Approvals }

let private sha256Text (s: string) = Encoding.UTF8.GetBytes s |> Git.sha256Hex

/// Stable obligation id: depends only on the requirement id, the baseline obligation id and its verbatim text.
let obligationId (requirementId: string) (sourceObligation: string) (text: string) =
    "VPO-" + (sha256Text (requirementId + "\n" + sourceObligation + "\n" + text)).Substring(0, 16)

let testKinds = [ "live"; "deterministic"; "review" ]

/// Baseline P0 requirements, in id order. These are the plan's mandatory scope.
let p0Of (baseline: Json) =
    arr "requirements" baseline |> List.filter (fun r -> strOpt "priority" r = Some "P0") |> List.sortBy (str "id")

let private policies (spec: Json) = arr "freshnessPolicies" spec
let private policyIds spec = policies spec |> List.map (str "id")

let private bindingsOf (spec: Json) =
    arr "testBindings" spec
    |> List.map (fun b -> str "obligation" b, arr "tests" b |> List.map (fun t -> { Id = str "id" t; Kind = str "kind" t }))

let private exclusionsOf (spec: Json) =
    arr "exclusions" spec |> List.map (fun e -> str "requirement" e, str "approval" e)

/// (approval id, requirement id) pairs that an authenticated operator approval in the pinned file excludes.
let authorizedExclusions (a: Approvals) : Set<string * string> =
    arr "approvals" a.Document
    |> List.filter (fun ap ->
        let auth = field "authority" ap |> Option.defaultValue JNull
        strOpt "kind" auth = Some "operator" && List.contains (strOpt "id" auth |> Option.defaultValue "") a.TrustedOperators)
    |> List.collect (fun ap ->
        arr "changes" ap
        |> List.filter (fun c -> strOpt "action" c = Some "exclude")
        |> List.choose (fun c -> strOpt "requirement_id" c |> Option.map (fun r -> str "id" ap, r)))
    |> Set.ofList

/// Input problems: anything that would make the plan claim more, or less, than its authorities support.
let problems (i: Inputs) : string list =
    let p0 = p0Of i.Baseline
    let p0Ids = p0 |> List.map (str "id") |> Set.ofList
    let obligationsOf = p0 |> List.collect (fun r -> arr "obligations" r |> List.map (str "id")) |> Set.ofList
    let manifestIds = arr "entries" i.Manifest |> List.map (str "requirement") |> Set.ofList
    let authorized = authorizedExclusions i.Approvals
    let bindings = bindingsOf i.Spec
    let known = policyIds i.Spec |> Set.ofList

    let pinned = field "approvals" i.Spec |> Option.defaultValue JNull

    [ if strOpt "sha256" pinned <> Some i.Approvals.Sha256 then
          yield sprintf "approvals file hashes to %s, curation pins %A" i.Approvals.Sha256 (strOpt "sha256" pinned)
      if strOpt "schema" i.Approvals.Document <> Some "causa.requirements-approvals/1" then
          yield "approvals file has an unsupported schema"
      if strOpt "baseline_sha256" i.Approvals.Document <> Some i.BaselineSha256 then
          yield "approvals file is bound to a different requirements baseline"
      for rid in p0Ids do
          if not (manifestIds.Contains rid) then yield sprintf "P0 requirement %s has no verification-manifest entry" rid
      for (rid, approval) in exclusionsOf i.Spec do
          if not (p0Ids.Contains rid) then yield sprintf "exclusion targets unknown or non-P0 requirement %s" rid
          elif not (authorized.Contains(approval, rid)) then
              yield sprintf "unauthorized exclusion of P0 requirement %s: approval %s is not an authenticated operator exclusion in the pinned approvals file" rid approval
      for (dup, n) in bindings |> List.countBy fst do
          if n > 1 then yield sprintf "obligation %s is bound more than once" dup
      for (o, tests) in bindings do
          if not (obligationsOf.Contains o) then yield sprintf "test binding targets unknown or non-P0 obligation %s" o
          if tests.IsEmpty then yield sprintf "test binding for %s names no test (leave it unbound instead)" o
          for t in tests do
              if String.IsNullOrWhiteSpace t.Id then yield sprintf "test binding for %s has an empty test id" o
              if not (List.contains t.Kind testKinds) then yield sprintf "test binding for %s has unknown kind '%s'" o t.Kind
      for (g, p) in (match field "gatePolicies" i.Spec with Some(JObj ps) -> ps |> List.map (fun (k, v) -> k, asStr v) | _ -> []) do
          if not (known.Contains p) then yield sprintf "gate %s maps to unknown freshness policy %s" g p
      if not (known.Contains(str "defaultPolicy" i.Spec)) then yield "defaultPolicy is not a declared freshness policy" ]

/// The strictest policy wins; policies are declared strictest first.
let private policyFor (spec: Json) (gates: string list) (kinds: string list) =
    let order = policyIds spec
    let gatePolicy =
        match field "gatePolicies" spec with
        | Some(JObj ps) -> gates |> List.choose (fun g -> ps |> List.tryFind (fun (k, _) -> k = g) |> Option.map (snd >> asStr))
        | _ -> []
    let kindPolicy =
        kinds |> List.choose (fun k -> policies spec |> List.tryFind (fun p -> List.contains k (strs "testKinds" p)) |> Option.map (str "id"))
    (str "defaultPolicy" spec :: gatePolicy @ kindPolicy)
    |> List.distinct
    |> List.sortBy (fun p -> List.tryFindIndex ((=) p) order |> Option.defaultValue Int32.MaxValue)
    |> List.head

let private union (xs: string list list) = xs |> List.concat |> List.distinct

let private entry (i: Inputs) (rules: Map<string, Json>) (manifestEntries: Map<string, Json>) (bindings: Map<string, TestRef list>) (excluded: Map<string, string>) (r: Json) =
    let rid = str "id" r
    let m = manifestEntries.[rid]
    let ruleJs = strs "rules" m |> List.map (fun x -> rules.[x])
    let defaults = field "defaults" i.Manifest |> Option.defaultValue (JObj [])
    let gates = strs "gates" m

    let obligations =
        arr "obligations" r
        |> List.mapi (fun idx o ->
            let oid = str "id" o
            let text = str "text" o
            let tests = bindings.TryFind oid |> Option.defaultValue [] |> List.sortBy (fun t -> t.Id, t.Kind)
            oid,
            tests,
            JObj
                [ "id", JStr(obligationId rid oid text)
                  "sourceObligation", JStr oid
                  "index", jint (idx + 1)
                  "kind", JStr(str "kind" o)
                  "text", JStr text
                  "textSha256", JStr(sha256Text text)
                  "testIdentity",
                  JObj
                      [ "status", JStr(if tests.IsEmpty then "missing" else "bound")
                        "tests", tests |> List.map (fun t -> JObj [ "id", JStr t.Id; "kind", JStr t.Kind ]) |> JArr ] ])

    let bound = obligations |> List.filter (fun (_, ts, _) -> not ts.IsEmpty) |> List.length
    let missing = obligations.Length - bound
    let kinds = obligations |> List.collect (fun (_, ts, _) -> ts |> List.map (fun t -> t.Kind)) |> List.distinct
    let acceptance = field "acceptanceTest" m |> Option.defaultValue (JObj [])
    let deps = field "dependenciesAffectingValidity" m |> Option.defaultValue (JObj [])

    JObj
        [ "requirement", JStr rid
          "priority", JStr(str "priority" r)
          "title", JStr(str "title" r)
          "document", JStr(str "document" r)
          "sectionSha256", JStr(str "sectionSha256" r)
          "obligations", obligations |> List.map (fun (_, _, j) -> j) |> JArr
          "acceptance", JObj [ "criteria", jstrs (strs "criteria" acceptance); "linkedSections", jstrs (strs "linkedSections" acceptance) ]
          "verificationMethod",
          JObj
              [ "gates", jstrs gates
                "rules",
                ruleJs
                |> List.map (fun x ->
                    JObj
                        [ "id", JStr(str "id" x)
                          "gate", JStr(str "gate" x)
                          "owner", JStr(str "owner" x)
                          "method", JStr(str "method" x)
                          "tooling", JStr(str "tooling" x) ])
                |> JArr ]
          "testIdentity",
          JObj
              [ "status", JStr(if missing = 0 && bound > 0 then "bound" elif bound = 0 then "missing" else "partial")
                "boundObligations", jint bound
                "missingObligations", jint missing ]
          "evidenceContract",
          JObj
              [ "subjectType", JStr "requirement"
                "subjectId", JStr rid
                "requiredEvidence", jstrs (union (ruleJs |> List.map (strs "requiredEvidence"))) ]
          "dependencies", JObj [ "requirements", jstrs (strs "requirements" deps); "contracts", jstrs (strs "contracts" deps) ]
          "freshness",
          JObj
              [ "policy", JStr(policyFor i.Spec gates kinds)
                "boundTo", jstrs (union (strs "freshness" defaults :: (ruleJs |> List.map (strs "freshness"))))
                "invalidatedBy", jstrs (union (strs "invalidatedBy" defaults :: (ruleJs |> List.map (strs "invalidatedBy")))) ]
          "exclusion",
          match excluded.TryFind rid with
          | Some approval -> JObj [ "status", JStr "excluded"; "approval", JStr approval ]
          | None -> JObj [ "status", JStr "in-scope"; "approval", JNull ] ]

let private identityCovers =
    "sha256 of the plan serialized by the compiler's canonical writer (ASCII JSON, two-space indent, trailing newline) with planIdentity removed"

/// The plan identity digest: sha256 of the canonical serialization of every member except planIdentity.
let digestOf (plan: Json) =
    match plan with
    | JObj props -> props |> List.filter (fun (k, _) -> k <> "planIdentity") |> JObj |> serialize |> sha256Text
    | _ -> failwith "plan must be an object"

let private withIdentity (body: (string * Json) list) =
    let d = digestOf (JObj body)
    match body with
    | schema :: rest ->
        JObj(
            schema
            :: ("planIdentity",
                JObj [ "planId", JStr("VP-" + d.Substring(0, 12)); "algorithm", JStr "sha256"; "digest", JStr d; "covers", JStr identityCovers ])
            :: rest
        )
    | [] -> failwith "empty plan"

/// Builds the plan. Every baseline P0 requirement appears exactly once, in id order; an unauthorized
/// exclusion is never applied (the requirement stays in scope and `problems` reports it).
let build (i: Inputs) : Json =
    let rules = arr "rules" i.Manifest |> List.map (fun x -> str "id" x, x) |> Map.ofList
    let manifestEntries = arr "entries" i.Manifest |> List.map (fun e -> str "requirement" e, e) |> Map.ofList
    let bindings = bindingsOf i.Spec |> List.groupBy fst |> List.map (fun (o, xs) -> o, xs |> List.collect snd) |> Map.ofList
    let authorized = authorizedExclusions i.Approvals
    let excluded = exclusionsOf i.Spec |> List.filter (fun (r, a) -> authorized.Contains(a, r)) |> List.map (fun (r, a) -> r, a) |> Map.ofList
    let p0 = p0Of i.Baseline |> List.filter (fun r -> manifestEntries.ContainsKey(str "id" r))
    let entries = p0 |> List.map (entry i rules manifestEntries bindings excluded)
    let obligations = entries |> List.collect (arr "obligations")
    let boundCount = obligations |> List.filter (fun o -> field "testIdentity" o |> Option.map (str "status") = Some "bound") |> List.length

    withIdentity
        [ "schema", JStr schemaId
          "source",
          JObj
              [ "repository", JStr i.Repository
                "commit", JStr i.Commit
                "baselineId", JStr i.BaselineId
                "baselineSha256", JStr i.BaselineSha256
                "approvals",
                JObj
                    [ "repository", JStr i.Approvals.Repository
                      "commit", JStr i.Approvals.Commit
                      "path", JStr i.Approvals.Path
                      "sha256", JStr i.Approvals.Sha256
                      "trustedOperators", jstrs (List.sort i.Approvals.TrustedOperators) ] ]
          "scope",
          JObj
              [ "priority", JStr "P0"
                "origin", JStr "baseline"
                "requirements", jint entries.Length
                "obligations", jint obligations.Length
                "boundObligations", jint boundCount
                "missingTestObligations", jint (obligations.Length - boundCount)
                "excludedRequirements", jint excluded.Count ]
          "statusVocabulary",
          JObj
              [ "testIdentity", jstrs [ "bound"; "partial"; "missing" ]
                "verification", jstrs [ "PASS"; "FAIL"; "BLOCKED"; "NOT_RUN"; "MISSING"; "UNBOUND"; "STALE"; "INVALID"; "EXCLUDED" ] ]
          "freshnessPolicies",
          policies i.Spec
          |> List.map (fun p ->
              JObj
                  [ "id", JStr(str "id" p)
                    "testKinds", jstrs (strs "testKinds" p)
                    "rule", JStr(str "rule" p)
                    "reusable", JBool(boolOr "reusable" false p)
                    "reuseCondition", JStr(str "reuseCondition" p) ])
          |> JArr
          "evidenceContract",
          JObj
              [ "evidenceSchema", JStr "causa.acceptance-evidence/1"
                "manifestSchema", JStr "causa.acceptance-evidence-manifest/1"
                "mustMatch", jstrs [ "run_id"; "source_sha"; "subject_type"; "subject_id"; "status"; "exit_code" ]
                "digest", JStr "sha256 of the evidence file bytes equals its entry in the independently pinned evidence manifest"
                "reuse", JStr(field "defaults" i.Manifest |> Option.map (str "evidenceReuse") |> Option.defaultValue "") ]
          "exclusionPolicy",
          JObj
              [ "rule",
                JStr "A P0 requirement leaves scope only through an exclusion listed in curation/verification-plan.json whose approval is an authenticated operator change {requirement_id, action: exclude} in the pinned approvals file. It stays in the plan, marked excluded with its approval; any other exclusion fails compilation (V18)."
                "approvalSchema", JStr "causa.requirements-approvals/1" ]
          "requirements", JArr entries ]

/// Requirements preservation: the plan read back must carry every baseline P0 requirement exactly once, with
/// its id, priority and verbatim obligations, the derived obligation ids, and a self-consistent identity.
let preservationProblems (baseline: Json) (plan: Json) : string list =
    let entries = arr "requirements" plan
    let byId = entries |> List.groupBy (str "requirement") |> Map.ofList
    let p0 = p0Of baseline
    let p0Ids = p0 |> List.map (str "id") |> Set.ofList
    let identity = field "planIdentity" plan |> Option.defaultValue JNull

    [ if strOpt "schema" plan <> Some schemaId then yield "plan has an unsupported schema"
      if strOpt "digest" identity <> Some(digestOf plan) then yield "plan identity digest does not match its content"
      if strOpt "planId" identity <> (strOpt "digest" identity |> Option.map (fun d -> "VP-" + d.Substring(0, min 12 d.Length))) then
          yield "plan id does not derive from its digest"
      let sorted = entries |> List.map (str "requirement")
      if sorted <> List.sort sorted then yield "plan requirements are not in id order"
      for r in p0 do
          let rid = str "id" r
          match byId.TryFind rid with
          | None -> yield sprintf "missing P0 requirement: %s" rid
          | Some(_ :: _ :: _) -> yield sprintf "P0 requirement %s appears more than once" rid
          | Some [ e ] ->
              if strOpt "priority" e <> Some "P0" then yield sprintf "P0 requirement %s priority changed to %A" rid (strOpt "priority" e)
              let expected = arr "obligations" r |> List.map (fun o -> str "id" o, str "text" o)
              let actual = arr "obligations" e |> List.map (fun o -> str "sourceObligation" o, str "text" o)
              if expected <> actual then yield sprintf "P0 requirement %s obligations differ from the baseline" rid
              for o in arr "obligations" e do
                  let oid, text = str "sourceObligation" o, str "text" o
                  if strOpt "id" o <> Some(obligationId rid oid text) then yield sprintf "obligation %s has an unstable id" oid
                  if strOpt "textSha256" o <> Some(sha256Text text) then yield sprintf "obligation %s text hash differs" oid
                  let ti = field "testIdentity" o |> Option.defaultValue JNull
                  match strOpt "status" ti, arr "tests" ti with
                  | Some "missing", [] | Some "bound", _ :: _ -> ()
                  | s, _ -> yield sprintf "obligation %s test identity status %A is inconsistent with its tests" oid s
          | Some [] -> ()
      for e in entries do
          if not (p0Ids.Contains(str "requirement" e)) then yield sprintf "plan carries %s, which is not a baseline P0 requirement" (str "requirement" e) ]
