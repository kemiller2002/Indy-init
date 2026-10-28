/// Phase Two: the verification plan (`verification-plan.json`, schema indy-init.verification-plan/1).
///
/// Pure functions only. The plan is derived from four authorities and nothing else:
///   - the requirements baseline (praxis.requirements-baseline/1): ids, priorities, titles, section hashes and
///     verbatim obligations with their kinds and value tokens;
///   - the generated verification manifest: gates, rules, acceptance links and dependencies;
///   - the curation file `curation/verification-plan.json`: freshness policies, test bindings, exclusions
///     and the pinned approvals source that authorizes exclusions;
///   - the curation file `curation/evidence-categories.json`: the declared rules that give each obligation its
///     required evidence categories.
/// A test identity is never invented: an obligation without a curated binding is reported `missing` (and, in its
/// acceptance contract, `unbound`). The plan never reports an obligation `verified`; only an assessment of
/// fresh, integrity-checked evidence can (PlanVerification).
///
/// Revision 2 of the /1 wire format is additive: every member of revision 1 keeps its name, type and meaning.
module VerificationPlan

open System
open System.Text
open Json

let schemaId = "indy-init.verification-plan/1"

type TestRef = { Id: string; Kind: string; Categories: string list }

type Approvals =
    { Repository: string
      Commit: string
      Path: string
      Sha256: string
      TrustedOperators: string list
      Document: Json }

type Categories = { Path: string; Sha256: string; Table: Json }

type Inputs =
    { Repository: string
      Commit: string
      BaselineId: string
      BaselineSha256: string
      Baseline: Json
      Manifest: Json
      Spec: Json
      Approvals: Approvals
      Categories: Categories }

/// One checker finding. Codes are stable; consumers branch on them, people read detail and action.
type Diagnostic = { Code: string; Subject: string; Detail: string; Action: string }

let diagnosticCodes =
    [ "MISSING-REQUIREMENT"
      "MISSING-OBLIGATION"
      "EXTRA-REQUIREMENT"
      "EXTRA-OBLIGATION"
      "DUPLICATE"
      "ALTERED-VALUE"
      "SWAPPED-REQUIREMENT-IDENTITY"
      "SWAPPED-OBLIGATION-IDENTITY"
      "TEST-MAPPING"
      "STALE-EVIDENCE"
      "UNAUTHORIZED-EXCLUSION"
      "EVIDENCE-INTEGRITY"
      "PLAN-IDENTITY"
      "CONTRACT-INCONSISTENT"
      "INPUT-BINDING" ]

let diag code subject detail action = { Code = code; Subject = subject; Detail = detail; Action = action }
let render (d: Diagnostic) = sprintf "[%s] %s: %s (action: %s)" d.Code d.Subject d.Detail d.Action

let diagnosticJson (d: Diagnostic) =
    JObj [ "code", JStr d.Code; "subject", JStr d.Subject; "detail", JStr d.Detail; "action", JStr d.Action ]

let private sha256Text (s: string) = Encoding.UTF8.GetBytes s |> Git.sha256Hex

/// Stable obligation id: depends only on the requirement id, the baseline obligation id and its verbatim text.
let obligationId (requirementId: string) (sourceObligation: string) (text: string) =
    "VPO-" + (sha256Text (requirementId + "\n" + sourceObligation + "\n" + text)).Substring(0, 16)

let testKinds = EvidenceCategories.testKinds

/// Record kinds of the Praxis work log a `praxis:<kind>` binding may name (kept equal to ProcessVerification.recordKinds,
/// which a test asserts; that module compiles after this one).
let processRecordKinds =
    [ "execution-start"; "checkpoint"; "assignment"; "dependency-decision"; "scope-change"; "owner-approval"
      "independent-review"; "release-selection"; "test-result"; "acceptance-reference"; "final-disposition" ]

/// Baseline P0 requirements, in id order. These are the plan's mandatory scope.
let p0Of (baseline: Json) =
    arr "requirements" baseline |> List.filter (fun r -> strOpt "priority" r = Some "P0") |> List.sortBy (str "id")

let private policies (spec: Json) = arr "freshnessPolicies" spec
let private policyIds spec = policies spec |> List.map (str "id")

let testOf (t: Json) =
    { Id = strOpt "id" t |> Option.defaultValue ""; Kind = strOpt "kind" t |> Option.defaultValue ""; Categories = strs "categories" t }

let private bindingsOf (spec: Json) =
    arr "testBindings" spec |> List.map (fun b -> str "obligation" b, arr "tests" b |> List.map testOf)

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
    let ruleIds = arr "rules" i.Manifest |> List.map (str "id") |> Set.ofList
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
          // A Praxis producer names a record kind, not a test: one record kind may serve an obligation once per
          // evidence kind (praxis:independent-review as both a process record and the independent review). An
          // application test id is unique.
          let identity (t: TestRef) = if t.Id.StartsWith("praxis:", StringComparison.Ordinal) then t.Id + " (" + t.Kind + ")" else t.Id
          for (dup, n) in tests |> List.countBy identity do
              if n > 1 then yield sprintf "test binding for %s names test '%s' more than once" o dup
          for t in tests do
              if String.IsNullOrWhiteSpace t.Id then yield sprintf "test binding for %s has an empty test id" o
              if not (List.contains t.Kind testKinds) then yield sprintf "test binding for %s has unknown kind '%s'" o t.Kind
              // A process, owner or Praxis review producer names one record kind of the run's Praxis log; anything else
              // could not be checked by verify-process and must not be bound as if it could.
              let producer = t.Id.StartsWith("praxis:", StringComparison.Ordinal)
              if (t.Kind = "process" || t.Kind = "owner") && not producer then
                  yield sprintf "test binding for %s: a %s binding must name a producer 'praxis:<record kind>', not '%s'" o t.Kind t.Id
              if producer then
                  let kind = t.Id.Substring 7
                  if not (List.contains kind processRecordKinds) then yield sprintf "test binding for %s names unknown Praxis record kind '%s'" o kind
                  if t.Kind = "owner" && kind <> "owner-approval" then yield sprintf "test binding for %s: owner evidence comes only from praxis:owner-approval" o
                  if t.Kind = "review" && kind <> "independent-review" then yield sprintf "test binding for %s: review evidence from Praxis comes only from praxis:independent-review" o
                  if t.Kind <> "process" && t.Kind <> "owner" && t.Kind <> "review" then
                      yield sprintf "test binding for %s: producer '%s' cannot have kind '%s'" o t.Id t.Kind
              if t.Categories.IsEmpty then
                  yield sprintf "test binding for %s: test '%s' declares no evidence category (state what it proves; categories are never inferred)" o t.Id
              for c in t.Categories do
                  if not (List.contains c EvidenceCategories.vocabulary) then
                      yield sprintf "test binding for %s: test '%s' declares unknown evidence category '%s'" o t.Id c
                  elif not (List.contains t.Kind (EvidenceCategories.kindsFor i.Categories.Table c)) then
                      yield sprintf "test binding for %s: test '%s' of kind '%s' cannot provide %s evidence" o t.Id t.Kind c
      // A category is proven either by Praxis producers or by ledger tests, never by a mix: otherwise one could stand
      // in for the other.
      for (o, tests) in bindings do
          let producerCats = tests |> List.filter (fun t -> t.Id.StartsWith("praxis:", StringComparison.Ordinal)) |> List.collect (fun t -> t.Categories) |> Set.ofList
          let testCats = tests |> List.filter (fun t -> not (t.Id.StartsWith("praxis:", StringComparison.Ordinal))) |> List.collect (fun t -> t.Categories) |> Set.ofList
          for c in Set.intersect producerCats testCats do
              yield sprintf "test binding for %s binds both a Praxis producer and a ledger test to %s; bind one kind of evidence per category" o c
      for (g, p) in (match field "gatePolicies" i.Spec with Some(JObj ps) -> ps |> List.map (fun (k, v) -> k, asStr v) | _ -> []) do
          if not (known.Contains p) then yield sprintf "gate %s maps to unknown freshness policy %s" g p
      if not (known.Contains(str "defaultPolicy" i.Spec)) then yield "defaultPolicy is not a declared freshness policy"
      yield! EvidenceCategories.problems i.Categories.Table ruleIds known |> List.map (sprintf "evidence categories: %s") ]

/// The strictest of the given policies; `order` lists policies strictest first.
let strictest (order: string list) (ps: string list) =
    ps
    |> List.distinct
    |> List.sortBy (fun p -> List.tryFindIndex ((=) p) order |> Option.defaultValue Int32.MaxValue)
    |> List.tryHead
    |> Option.defaultValue ""

/// The strictest policy wins; policies are declared strictest first.
let private policyFor (spec: Json) (gates: string list) (kinds: string list) =
    let gatePolicy =
        match field "gatePolicies" spec with
        | Some(JObj ps) -> gates |> List.choose (fun g -> ps |> List.tryFind (fun (k, _) -> k = g) |> Option.map (snd >> asStr))
        | _ -> []
    let kindPolicy =
        kinds |> List.choose (fun k -> policies spec |> List.tryFind (fun p -> List.contains k (strs "testKinds" p)) |> Option.map (str "id"))
    strictest (policyIds spec) (str "defaultPolicy" spec :: gatePolicy @ kindPolicy)

let private union (xs: string list list) = xs |> List.concat |> List.distinct

let testJson (t: TestRef) =
    JObj
        [ "id", JStr t.Id
          "kind", JStr t.Kind
          "categories", jstrs (EvidenceCategories.vocabulary |> List.filter (fun c -> List.contains c t.Categories)) ]

/// The acceptance contract of one obligation. It is derived only from the plan entry (as written), the obligation,
/// its bound tests, the embedded categories table and the policy order, so a checker can re-derive it from the
/// plan alone and detect any edit. At compilation no evidence exists: verificationStatus is never `verified`.
let contractFor (categories: Json) (policyOrder: string list) (entry: Json) (o: Json) (tests: TestRef list) : Json =
    let rid = str "requirement" entry
    let vm = field "verificationMethod" entry |> Option.defaultValue JNull
    let gates = strs "gates" vm
    let rules = arr "rules" vm |> List.map (str "id")
    let policy = field "freshness" entry |> Option.bind (strOpt "policy") |> Option.defaultValue ""
    let excluded = field "exclusion" entry |> Option.bind (strOpt "status") = Some "excluded"

    let subject: EvidenceCategories.Subject =
        { Requirement = rid
          Title = str "title" entry
          Document = str "document" entry
          Gates = gates
          Rules = rules
          Policy = policy
          Obligation = str "sourceObligation" o
          ObligationKind = str "kind" o }

    let required = EvidenceCategories.classify categories subject
    let requiredIds = required |> List.map fst
    let provided = tests |> List.collect (fun t -> t.Categories) |> Set.ofList
    let categoryPolicies = requiredIds |> List.choose (fun c -> EvidenceCategories.policyFor categories c |> Option.map (fun p -> c, p))

    let missing =
        if requiredIds.IsEmpty then
            [ JObj [ "category", JStr EvidenceCategories.unclassified; "reason", JStr "unclassified"; "detail", JStr "no declared category rule applies to this obligation" ] ]
        else
            requiredIds
            |> List.map (fun c ->
                if provided.Contains c then
                    JObj [ "category", JStr c; "reason", JStr "no-evidence"; "detail", JStr "a bound test declares this category; only an assessed evidence bundle can verify it" ]
                else
                    JObj [ "category", JStr c; "reason", JStr "no-bound-test"; "detail", JStr "no test bound to this obligation declares this category" ])

    let deps = field "dependencies" entry |> Option.defaultValue (JObj [])

    JObj
        [ "requirement", JStr rid
          "obligation", JStr(str "sourceObligation" o)
          "obligationId", JStr(str "id" o)
          "priority", JStr(str "priority" entry)
          "verificationMethod", JObj [ "gates", jstrs gates; "rules", jstrs rules ]
          "requiredEvidenceCategories", jstrs requiredIds
          "categoryDerivation", required |> List.map (fun (c, ids) -> JObj [ "category", JStr c; "rules", jstrs ids ]) |> JArr
          "evidenceProducers",
          requiredIds
          |> List.map (fun c ->
              JObj
                  [ "category", JStr c
                    "evidenceClass", JStr(EvidenceCategories.classFor categories c)
                    "producer", JStr(EvidenceCategories.producerFor categories c) ])
          |> JArr
          "testIdentity",
          JObj
              [ "status", JStr(if tests.IsEmpty then "unbound" else "bound")
                "tests", tests |> List.sortBy (fun t -> t.Id, t.Kind) |> List.map testJson |> JArr ]
          "dependencies", JObj [ "requirements", jstrs (strs "requirements" deps); "contracts", jstrs (strs "contracts" deps) ]
          "freshness",
          JObj
              [ "policy", JStr(strictest policyOrder (policy :: (categoryPolicies |> List.map snd)))
                "requirementPolicy", JStr policy
                "categoryPolicies", categoryPolicies |> List.map (fun (c, p) -> JObj [ "category", JStr c; "policy", JStr p ]) |> JArr ]
          "verificationStatus", JStr(if excluded then "excluded" elif requiredIds.IsEmpty then "unclassified" else "unverified")
          "missingCoverage", JArr(if excluded then [] else missing) ]

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
                        "tests", tests |> List.map (fun t -> JObj [ "id", JStr t.Id; "kind", JStr t.Kind ]) |> JArr ]
                  "tokens", jstrs (strs "tokens" o) ])

    let bound = obligations |> List.filter (fun (ts, _) -> not ts.IsEmpty) |> List.length
    let missing = obligations.Length - bound
    let kinds = obligations |> List.collect (fun (ts, _) -> ts |> List.map (fun t -> t.Kind)) |> List.distinct
    let acceptance = field "acceptanceTest" m |> Option.defaultValue (JObj [])
    let deps = field "dependenciesAffectingValidity" m |> Option.defaultValue (JObj [])

    let withoutContracts =
        JObj
            [ "requirement", JStr rid
              "priority", JStr(str "priority" r)
              "title", JStr(str "title" r)
              "document", JStr(str "document" r)
              "sectionSha256", JStr(str "sectionSha256" r)
              "obligations", obligations |> List.map snd |> JArr
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

    let withContract (tests: TestRef list, o: Json) =
        match o with
        | JObj ps -> JObj(ps @ [ "acceptanceContract", contractFor i.Categories.Table (policyIds i.Spec) withoutContracts o tests ])
        | j -> j

    match withoutContracts with
    | JObj ps -> JObj(ps |> List.map (fun (k, v) -> if k = "obligations" then k, JArr(obligations |> List.map withContract) else k, v))
    | j -> j

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

let obligationVerificationVocabulary = [ "verified"; "unverified"; "unclassified"; "excluded" ]

let missingCoverageReasons =
    [ "unclassified"; "no-bound-test"; "no-evidence"; "evidence-not-fresh"; "evidence-invalid"; "evidence-not-pass" ]

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
    let contracts = obligations |> List.choose (field "acceptanceContract")
    let countStatus s = contracts |> List.filter (fun c -> strOpt "verificationStatus" c = Some s) |> List.length

    let fullyBound =
        contracts
        |> List.filter (fun c ->
            not (strs "requiredEvidenceCategories" c).IsEmpty
            && arr "missingCoverage" c |> List.forall (fun m -> strOpt "reason" m = Some "no-evidence"))
        |> List.length

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
                "excludedRequirements", jint excluded.Count
                "verifiedObligations", jint 0
                "unverifiedObligations", jint (countStatus "unverified")
                "unclassifiedObligations", jint (countStatus "unclassified")
                "excludedObligations", jint (countStatus "excluded")
                "obligationsWithEveryCategoryBound", jint fullyBound
                "obligationsByCategory",
                EvidenceCategories.vocabulary
                |> List.map (fun c -> c, jint (contracts |> List.filter (fun x -> List.contains c (strs "requiredEvidenceCategories" x)) |> List.length))
                |> JObj ]
          "statusVocabulary",
          JObj
              [ "testIdentity", jstrs [ "bound"; "partial"; "missing" ]
                "verification", jstrs [ "PASS"; "FAIL"; "BLOCKED"; "NOT_RUN"; "MISSING"; "UNBOUND"; "UNVERIFIED"; "STALE"; "INVALID"; "EXCLUDED" ]
                "contractTestIdentity", jstrs [ "bound"; "unbound" ]
                "obligationVerification", jstrs obligationVerificationVocabulary
                "missingCoverage", jstrs missingCoverageReasons
                "evidenceCategories", jstrs EvidenceCategories.vocabulary
                "diagnostics", jstrs diagnosticCodes ]
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
                "reuse", JStr(field "defaults" i.Manifest |> Option.map (str "evidenceReuse") |> Option.defaultValue "")
                "obligationEvidence",
                JStr
                    "An obligation counts as verified only from an evidence file that passes every check above for its requirement and lists, under `obligations`, an entry {id: <the obligation's VPO id>, verification: PASS, tests: [{testId, outcome: passed}]} whose tests are bound to that obligation in this plan; the categories of those tests must cover every required category. A passing requirement-level result, or a test bound to a different obligation, verifies nothing." ]
          "exclusionPolicy",
          JObj
              [ "rule",
                JStr "A P0 requirement leaves scope only through an exclusion listed in curation/verification-plan.json whose approval is an authenticated operator change {requirement_id, action: exclude} in the pinned approvals file. It stays in the plan, marked excluded with its approval; any other exclusion fails compilation (V18)."
                "approvalSchema", JStr "causa.requirements-approvals/1" ]
          "evidenceCategories", EvidenceCategories.embedded i.Categories.Path i.Categories.Sha256 i.Categories.Table
          "requirements", JArr entries ]

// ---- preservation: what a checker re-derives from the baseline and the plan alone --------------------------------

let private words (s: string) =
    s.Split([| ' '; '\t'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
    |> Array.map (fun w -> w.Trim([| ','; '.'; ';'; ':' |]).ToLowerInvariant())
    |> Array.sort
    |> List.ofArray

let private policyOrderOf (plan: Json) = arr "freshnessPolicies" plan |> List.map (str "id")

let private contractTests (o: Json) =
    field "acceptanceContract" o |> Option.bind (field "testIdentity") |> Option.map (arr "tests" >> List.map testOf) |> Option.defaultValue []

/// Contract problems for one obligation of a plan entry: the contract must be exactly what the embedded rules derive.
let private contractDiagnostics (plan: Json) (entry: Json) (o: Json) : Diagnostic list =
    let oid = strOpt "sourceObligation" o |> Option.defaultValue "?"
    let rid = strOpt "requirement" entry |> Option.defaultValue "?"
    let action = "recompile the plan; never edit verification-plan.json by hand"
    match field "acceptanceContract" o, field "evidenceCategories" plan with
    | None, _ -> [ diag "CONTRACT-INCONSISTENT" oid (sprintf "obligation %s of %s has no acceptance contract" oid rid) action ]
    | _, None -> [ diag "CONTRACT-INCONSISTENT" "plan" "the plan does not embed its evidence-categories table" action ]
    | Some actual, Some categories ->
        let tests = contractTests o
        let legacy = field "testIdentity" o |> Option.map (arr "tests" >> List.map (fun t -> strOpt "id" t, strOpt "kind" t)) |> Option.defaultValue []
        let expected = contractFor categories (policyOrderOf plan) entry o tests
        [ if List.sort legacy <> (tests |> List.map (fun t -> Some t.Id, Some t.Kind) |> List.sort) then
              yield diag "CONTRACT-INCONSISTENT" oid (sprintf "obligation %s: the contract's tests differ from its testIdentity tests" oid) action
          for t in tests do
              for c in t.Categories do
                  if not (List.contains c EvidenceCategories.vocabulary) then
                      yield diag "CONTRACT-INCONSISTENT" oid (sprintf "obligation %s: test '%s' declares unknown category '%s'" oid t.Id c) action
                  elif not (List.contains t.Kind (EvidenceCategories.kindsFor categories c)) then
                      yield diag "CONTRACT-INCONSISTENT" oid (sprintf "obligation %s: test '%s' of kind %s cannot provide %s evidence" oid t.Id t.Kind c) action
          if serialize actual <> serialize expected then
              let differing =
                  match actual, expected with
                  | JObj a, JObj e ->
                      e
                      |> List.filter (fun (k, v) -> a |> List.tryFind (fst >> (=) k) |> Option.map (snd >> serialize) <> Some(serialize v))
                      |> List.map fst
                  | _ -> [ "acceptanceContract" ]
              yield
                  diag
                      "CONTRACT-INCONSISTENT"
                      oid
                      (sprintf "obligation %s: acceptance contract differs from what the embedded rules derive (%s)" oid (String.concat ", " differing))
                      action ]

/// Requirements preservation, as diagnostics: the plan read back must carry every baseline P0 requirement exactly
/// once, with its id, priority, title, document, section hash and verbatim obligations (id, kind, text, value
/// tokens, order), the derived obligation ids, self-consistent acceptance contracts and identity. Exchanged
/// requirement or obligation identities are named as swaps, not only as differences.
let preservationDiagnostics (baseline: Json) (plan: Json) : Diagnostic list =
    let entries = arr "requirements" plan
    let byId = entries |> List.groupBy (fun e -> strOpt "requirement" e |> Option.defaultValue "") |> Map.ofList
    let p0 = p0Of baseline
    let p0Ids = p0 |> List.map (str "id") |> Set.ofList
    let identity = field "planIdentity" plan |> Option.defaultValue JNull
    let recompile = "recompile the plan from the pinned baseline; never edit verification-plan.json by hand"
    let bySection = p0 |> List.map (fun r -> str "sectionSha256" r, str "id" r) |> Map.ofList
    let byTitle = p0 |> List.groupBy (str "title") |> Map.ofList

    let textOwners =
        p0
        |> List.collect (fun r -> arr "obligations" r |> List.map (fun o -> str "text" o, (str "id" r, str "id" o)))
        |> List.groupBy fst
        |> Map.ofList

    let vpoOwners =
        p0
        |> List.collect (fun r -> arr "obligations" r |> List.map (fun o -> obligationId (str "id" r) (str "id" o) (str "text" o), str "id" o))
        |> Map.ofList

    let obligationTexts (r: Json) = arr "obligations" r |> List.map (fun o -> strOpt "text" o |> Option.defaultValue "")

    /// The baseline requirement whose content a plan entry actually carries, when it is not the entry's own.
    let contentOwner (rid: string) (e: Json) =
        let bySectionHit = strOpt "sectionSha256" e |> Option.bind bySection.TryFind
        let byContentHit =
            strOpt "title" e
            |> Option.bind byTitle.TryFind
            |> Option.bind (List.tryFind (fun r -> obligationTexts r = obligationTexts e))
            |> Option.map (str "id")
        [ bySectionHit; byContentHit ] |> List.choose id |> List.tryFind (fun x -> x <> rid)

    let obligationDiagnostics (rid: string) (r: Json) (e: Json) =
        let expected = arr "obligations" r
        let actual = arr "obligations" e
        let actualById = actual |> List.groupBy (fun o -> strOpt "sourceObligation" o |> Option.defaultValue "") |> Map.ofList
        let expectedIds = expected |> List.map (str "id")
        let tokens (o: Json) = strs "tokens" o

        [ for o in expected do
              let oid = str "id" o
              match actualById.TryFind oid with
              | None -> yield diag "MISSING-OBLIGATION" oid (sprintf "P0 requirement %s obligation %s is missing from the plan" rid oid) recompile
              | Some(_ :: _ :: _) -> yield diag "DUPLICATE" oid (sprintf "obligation %s appears more than once in %s" oid rid) recompile
              | Some [ a ] ->
                  let text = str "text" o
                  let atext = strOpt "text" a |> Option.defaultValue ""
                  if atext <> text then
                      match textOwners.TryFind atext |> Option.map (List.map snd) |> Option.bind (List.tryFind (fun owner -> owner <> (rid, oid))) with
                      | Some(r2, o2) ->
                          yield
                              diag
                                  "SWAPPED-OBLIGATION-IDENTITY"
                                  oid
                                  (sprintf "obligation %s of %s carries the verbatim text of %s (%s); obligation identities are exchanged" oid rid o2 r2)
                                  recompile
                      | None when words atext = words text ->
                          yield
                              diag
                                  "ALTERED-VALUE"
                                  oid
                                  (sprintf "obligation %s text: role/value swap (the same words in a different order); baseline \"%s\", plan \"%s\"" oid text atext)
                                  recompile
                      | None ->
                          yield
                              diag
                                  "ALTERED-VALUE"
                                  oid
                                  (sprintf "obligation %s text differs from the baseline (weakened, negated or rewritten); baseline \"%s\", plan \"%s\"" oid text atext)
                                  recompile
                  if strOpt "kind" a <> Some(str "kind" o) then
                      yield diag "ALTERED-VALUE" oid (sprintf "obligation %s kind changed from %s to %A" oid (str "kind" o) (strOpt "kind" a)) recompile
                  if tokens a <> tokens o then
                      let detail =
                          if List.sort (tokens a) = List.sort (tokens o) then "value tokens swapped (same values, different order)"
                          else "value tokens changed"
                      yield
                          diag
                              "ALTERED-VALUE"
                              oid
                              (sprintf "obligation %s %s: baseline [%s], plan [%s]" oid detail (String.concat ", " (tokens o)) (String.concat ", " (tokens a)))
                              recompile
                  let expectedVpo = obligationId rid oid text
                  match strOpt "id" a with
                  | Some v when v = expectedVpo -> ()
                  | Some v when vpoOwners.ContainsKey v ->
                      yield
                          diag
                              "SWAPPED-OBLIGATION-IDENTITY"
                              oid
                              (sprintf "obligation %s carries the VPO id %s of obligation %s; its own id is %s" oid v vpoOwners.[v] expectedVpo)
                              recompile
                  | v -> yield diag "ALTERED-VALUE" oid (sprintf "obligation %s has an unstable id %A (expected %s)" oid v expectedVpo) recompile
                  if strOpt "textSha256" a <> Some(sha256Text atext) then
                      yield diag "ALTERED-VALUE" oid (sprintf "obligation %s text hash differs from its text" oid) recompile
                  let ti = field "testIdentity" a |> Option.defaultValue JNull
                  match strOpt "status" ti, arr "tests" ti with
                  | Some "missing", []
                  | Some "bound", _ :: _ -> ()
                  | s, _ ->
                      yield
                          diag "CONTRACT-INCONSISTENT" oid (sprintf "obligation %s test identity status %A is inconsistent with its tests" oid s) recompile
                  yield! contractDiagnostics plan e a
              | Some [] -> ()
          for a in actual do
              let aid = strOpt "sourceObligation" a |> Option.defaultValue ""
              if not (List.contains aid expectedIds) then
                  match textOwners.TryFind(strOpt "text" a |> Option.defaultValue "") |> Option.map (List.map snd) |> Option.bind List.tryHead with
                  | Some(r2, o2) ->
                      yield
                          diag
                              "SWAPPED-OBLIGATION-IDENTITY"
                              aid
                              (sprintf "%s carries obligation %s (%s) under the id %s, which is not a baseline obligation of %s" rid o2 r2 aid rid)
                              recompile
                  | None -> yield diag "EXTRA-OBLIGATION" aid (sprintf "%s carries obligation %s, which is not a baseline obligation" rid aid) recompile
          let actualIds = actual |> List.map (fun o -> strOpt "sourceObligation" o |> Option.defaultValue "")
          if List.sort actualIds = List.sort expectedIds && actualIds <> expectedIds then
              yield diag "ALTERED-VALUE" rid (sprintf "obligations of %s are reordered (baseline order %s)" rid (String.concat ", " expectedIds)) recompile
          if actual |> List.mapi (fun k o -> intOpt "index" o = Some(int64 (k + 1))) |> List.contains false then
              yield diag "ALTERED-VALUE" rid (sprintf "obligation indexes of %s are not 1..n in order" rid) recompile ]

    [ if strOpt "schema" plan <> Some schemaId then yield diag "PLAN-IDENTITY" "plan" "plan has an unsupported schema" recompile
      if strOpt "digest" identity <> Some(digestOf plan) then
          yield diag "PLAN-IDENTITY" "plan" "plan identity digest does not match its content" "reject this plan; obtain the plan whose digest was admitted"
      if strOpt "planId" identity <> (strOpt "digest" identity |> Option.map (fun d -> "VP-" + d.Substring(0, min 12 d.Length))) then
          yield diag "PLAN-IDENTITY" "plan" "plan id does not derive from its digest" recompile
      let sorted = entries |> List.map (fun e -> strOpt "requirement" e |> Option.defaultValue "")
      if sorted <> List.sort sorted then yield diag "ALTERED-VALUE" "plan" "plan requirements are not in id order" recompile
      for r in p0 do
          let rid = str "id" r
          match byId.TryFind rid with
          | None -> yield diag "MISSING-REQUIREMENT" rid (sprintf "missing P0 requirement: %s" rid) recompile
          | Some(_ :: _ :: _) -> yield diag "DUPLICATE" rid (sprintf "P0 requirement %s appears more than once" rid) recompile
          | Some [ e ] ->
              match contentOwner rid e with
              | Some other ->
                  yield
                      diag
                          "SWAPPED-REQUIREMENT-IDENTITY"
                          rid
                          (sprintf
                              "plan entry %s carries the content of baseline requirement %s (title %A, sectionSha256 %A); requirement identities are exchanged"
                              rid
                              other
                              (strOpt "title" e)
                              (strOpt "sectionSha256" e))
                          recompile
              | None ->
                  if strOpt "priority" e <> Some "P0" then
                      yield diag "ALTERED-VALUE" rid (sprintf "P0 requirement %s priority changed to %A" rid (strOpt "priority" e)) recompile
                  for key in [ "title"; "document"; "sectionSha256" ] do
                      if strOpt key e <> Some(str key r) then
                          yield diag "ALTERED-VALUE" rid (sprintf "P0 requirement %s %s changed from \"%s\" to %A" rid key (str key r) (strOpt key e)) recompile
                  yield! obligationDiagnostics rid r e
          | Some [] -> ()
      for e in entries do
          let rid = strOpt "requirement" e |> Option.defaultValue ""
          if not (p0Ids.Contains rid) then
              yield diag "EXTRA-REQUIREMENT" rid (sprintf "plan carries %s, which is not a baseline P0 requirement" rid) recompile ]

/// The preservation diagnostics rendered as text (compilation check V18 and the plan assessment).
let preservationProblems (baseline: Json) (plan: Json) : string list =
    preservationDiagnostics baseline plan |> List.map render

/// The embedded categories table must be the curated one (compile time, where the file is available).
let categoriesTableProblems (i: Inputs) (plan: Json) : string list =
    match field "evidenceCategories" plan with
    | None -> [ "plan does not embed the evidence-categories table" ]
    | Some e ->
        [ if (field "table" e |> Option.bind (strOpt "sha256")) <> Some i.Categories.Sha256 then
              yield "plan's embedded evidence-categories table is not the curated file"
          if serialize e <> serialize (EvidenceCategories.embedded i.Categories.Path i.Categories.Sha256 i.Categories.Table) then
              yield "plan's embedded evidence-categories rules differ from the curated table" ]
