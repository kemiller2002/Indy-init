/// Proposed test identities for every unresolved application-test and reviewer category of a plan (Rehearsal 6
/// readiness, ESO-R6-02).
///
/// Pure. A rehearsal must name, before implementation, the exact test that will prove each mandatory obligation's
/// required categories. This module derives those names from a fixed convention, so the names are predictable and
/// the tests the rehearsal writes must carry exactly these identities. It proposes; it never approves. A proposal takes
/// effect only when the owner merges it into the curation, and a bound test proves nothing by being named.
///
/// Convention, per obligation (only for categories `plan-readiness` reports unresolved):
/// - `independent-acceptance` is bound to the Praxis producer `praxis:independent-review` (kind `review`).
/// - The remaining application-test categories are proven by one test whose kind every one of them admits:
///   `live` when `live-provider` is required, else `deterministic`. A category the chosen kind cannot provide gets
///   its own deterministic test.
/// - A test that proves `browser` or `accessibility` is a Playwright test, `playwright:<suite>/<REQ>.spec.js >
///   <OBLIGATION> [chromium]`; any other is a .NET test, `dotnet:Causa.Acceptance.<Suite>.<REQ>Tests.<OBLIGATION>`.
///   The suite is `acceptance` / `Deterministic` or `live` / `Live`.
module TestBindingProposal

open Json

let schemaId = "indy-init.test-bindings-proposal/1"

type Test = { Id: string; Kind: string; Categories: string list }

/// The test kinds each category admits, as the plan embeds them.
let private kindsByCategory (plan: Json) : Map<string, string list> =
    field "evidenceCategories" plan
    |> Option.map (arr "categories")
    |> Option.defaultValue []
    |> List.map (fun c -> str "id" c, strs "testKinds" c)
    |> Map.ofList

let private browserish (categories: string list) =
    categories |> List.exists (fun c -> c = "browser" || c = "accessibility")

let private testFor (requirement: string) (obligation: string) (kind: string) (categories: string list) : Test =
    let live = kind = "live"
    let id =
        if browserish categories then
            sprintf "playwright:%s/%s.spec.js > %s [chromium]" (if live then "live" else "acceptance") requirement obligation
        else
            sprintf "dotnet:Causa.Acceptance.%s.%sTests.%s" (if live then "Live" else "Deterministic") (requirement.Replace("-", "")) obligation
    { Id = id; Kind = kind; Categories = categories }

/// The tests that resolve one obligation's unresolved categories, in the contract's category order.
let testsFor (kinds: Map<string, string list>) (requirement: string) (obligation: string) (unresolved: string list) : Test list =
    let admits kind c = kinds.TryFind c |> Option.defaultValue [] |> List.contains kind
    let review = unresolved |> List.filter ((=) "independent-acceptance")
    let application = unresolved |> List.filter ((<>) "independent-acceptance")
    let primaryKind = if List.contains "live-provider" application then "live" else "deterministic"
    let primary, rest = application |> List.partition (admits primaryKind)
    [ if not primary.IsEmpty then yield testFor requirement obligation primaryKind primary
      if not rest.IsEmpty then yield testFor requirement obligation "deterministic" rest
      if not review.IsEmpty then yield { Id = "praxis:independent-review"; Kind = "review"; Categories = review } ]

let private testJson (t: Test) =
    JObj [ "id", JStr t.Id; "kind", JStr t.Kind; "categories", jstrs t.Categories ]

/// The proposal for every obligation `plan-readiness` reports with an unresolved application-test or reviewer
/// category. Unclassified obligations and process or owner gaps are left alone: they need a rule or a Praxis producer,
/// not a test.
let propose (plan: Json) : Json =
    let kinds = kindsByCategory plan
    let bindings =
        PlanReadiness.gaps plan
        |> List.filter (fun g -> g.EvidenceClass = "application-test" || g.EvidenceClass = "reviewer")
        |> List.groupBy (fun g -> g.Requirement, g.Obligation)
        |> List.map (fun ((requirement, obligation), gs) ->
            obligation, testsFor kinds requirement obligation (gs |> List.map (fun g -> g.Category)))
        |> List.filter (snd >> List.isEmpty >> not)
    let identity = field "planIdentity" plan |> Option.defaultValue JNull
    JObj
        [ "schema", JStr schemaId
          "status", JStr "proposed"
          "approval", JStr "NOT APPROVED. An agent proposal for the owner. Bindings take effect only when they are in curation/verification-plan.json testBindings and the owner merges the recompiled plan (RDP-003)."
          "plan", JObj [ "planId", jopt JStr (strOpt "planId" identity); "digest", jopt JStr (strOpt "digest" identity) ]
          "convention", JStr "independent-acceptance: praxis:independent-review. Other categories: one test of a kind every one admits (live when live-provider is required, else deterministic); a category that kind cannot provide gets its own deterministic test. Browser or accessibility: playwright:<acceptance|live>/<REQ>.spec.js > <OBLIGATION> [chromium]. Otherwise: dotnet:Causa.Acceptance.<Deterministic|Live>.<REQ without dashes>Tests.<OBLIGATION>. The rehearsal's tests must carry exactly these identities."
          "testBindings",
          bindings
          |> List.map (fun (obligation, tests) -> JObj [ "obligation", JStr obligation; "tests", tests |> List.map testJson |> JArr ])
          |> JArr ]

/// `existing` curation bindings with the proposal's tests appended, obligation by obligation; obligations keep the
/// curation's order and new ones follow in the proposal's order.
let applyTo (existing: Json list) (proposal: Json) : Json list =
    let proposed = arr "testBindings" proposal |> List.map (fun b -> str "obligation" b, arr "tests" b)
    let byObligation = Map.ofList proposed
    let known = existing |> List.map (str "obligation") |> Set.ofList
    let extended =
        existing
        |> List.map (fun b ->
            match byObligation.TryFind(str "obligation" b) with
            | Some tests -> JObj [ "obligation", JStr(str "obligation" b); "tests", JArr(arr "tests" b @ tests) ]
            | None -> b)
    extended @ (proposed |> List.filter (fst >> known.Contains >> not) |> List.map (fun (o, tests) -> JObj [ "obligation", JStr o; "tests", JArr tests ]))
