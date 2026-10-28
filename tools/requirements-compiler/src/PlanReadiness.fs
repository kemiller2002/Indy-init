/// The implementation-start gate (Echelon development speed optimization, Priority Two).
///
/// Pure. Before implementation begins, every mandatory obligation of the frozen verification plan must say how each
/// of its required evidence categories will be produced: an application test bound to it for application-test and
/// reviewer categories, or a `praxis:<record kind>` producer for process and owner categories. A rehearsal whose plan
/// leaves any of them unresolved is BLOCKED, unless its own frozen readiness rules permit that exact obligation and
/// category with a reason and a decision id. A permitted gap stays a gap: it is reported, and it is still unverified
/// at acceptance. Nothing here marks an obligation verified, and a bound test never proves an obligation by existing.
module PlanReadiness

open System
open Json

let schemaId = "indy-init.plan-readiness/1"
let rulesSchemaId = "indy-init.plan-readiness-rules/1"

type Gap =
    { Requirement: string
      Obligation: string
      ObligationId: string
      Category: string
      EvidenceClass: string
      Producer: string
      Reason: string }

let private gapJson (g: Gap) =
    JObj
        [ "requirement", JStr g.Requirement
          "obligation", JStr g.Obligation
          "obligationId", JStr g.ObligationId
          "category", JStr g.Category
          "evidenceClass", JStr g.EvidenceClass
          "producer", JStr g.Producer
          "reason", JStr g.Reason ]

let private contract (o: Json) = field "acceptanceContract" o |> Option.defaultValue (JObj [])

/// Every unresolved (obligation, category) of the plan's mandatory scope.
let gaps (plan: Json) : Gap list =
    arr "requirements" plan
    |> List.filter (fun e -> field "exclusion" e |> Option.bind (strOpt "status") <> Some "excluded")
    |> List.collect (fun e ->
        arr "obligations" e
        |> List.collect (fun o ->
            let c = contract o
            let tests = field "testIdentity" c |> Option.map (arr "tests") |> Option.defaultValue []
            let producers = arr "evidenceProducers" c
            let classOf cat = producers |> List.tryFind (fun p -> strOpt "category" p = Some cat) |> Option.bind (strOpt "evidenceClass") |> Option.defaultValue ""
            let producerOf cat = producers |> List.tryFind (fun p -> strOpt "category" p = Some cat) |> Option.bind (strOpt "producer") |> Option.defaultValue ""
            let gap cat reason =
                { Requirement = str "requirement" e
                  Obligation = str "sourceObligation" o
                  ObligationId = str "id" o
                  Category = cat
                  EvidenceClass = classOf cat
                  Producer = producerOf cat
                  Reason = reason }
            match strs "requiredEvidenceCategories" c with
            | [] -> [ gap EvidenceCategories.unclassified "unclassified" ]
            | required ->
                required
                |> List.choose (fun cat ->
                    let declaring = tests |> List.filter (fun t -> List.contains cat (strs "categories" t))
                    let isProcess = classOf cat = "process" || classOf cat = "owner"
                    if declaring.IsEmpty then Some(gap cat (if isProcess then "no-bound-producer" else "no-bound-test"))
                    elif declaring |> List.forall (ProcessVerification.admissible plan cat >> not) then Some(gap cat "invalid-producer")
                    else None)))

/// Problems with the readiness rules themselves. A permit that matches no gap is stale and is a problem: the
/// frozen rules must describe this plan, not an earlier one.
let ruleProblems (rules: Json option) (all: Gap list) : string list =
    match rules with
    | None -> []
    | Some r ->
        let open' = all |> List.map (fun g -> g.Obligation, g.Category) |> Set.ofList
        [ if strOpt "schema" r <> Some rulesSchemaId then yield sprintf "readiness rules have schema %A, expected %s" (strOpt "schema" r) rulesSchemaId
          for p in arr "permitUnresolved" r do
              let o, c = strOpt "obligation" p |> Option.defaultValue "", strOpt "category" p |> Option.defaultValue ""
              if String.IsNullOrWhiteSpace(strOpt "reason" p |> Option.defaultValue "") then yield sprintf "permit for %s %s states no reason" o c
              if String.IsNullOrWhiteSpace(strOpt "decision" p |> Option.defaultValue "") then yield sprintf "permit for %s %s names no decision" o c
              if not (open'.Contains(o, c)) then yield sprintf "permit for %s %s matches no unresolved obligation of this plan" o c ]

let private permitted (rules: Json option) (g: Gap) =
    rules
    |> Option.map (arr "permitUnresolved")
    |> Option.defaultValue []
    |> List.tryFind (fun p -> strOpt "obligation" p = Some g.Obligation && strOpt "category" p = Some g.Category)

/// `expectedDigest` is the plan digest frozen at kickoff, `rules` the frozen readiness rules with their actual and
/// frozen SHA-256. The plan must recompute to the frozen digest, and the rules must hash to theirs: a gate that read
/// an edited plan or self-supplied rules would not be a gate.
let assess (plan: Json) (expectedDigest: string) (rules: (Json * string * string) option) : Json =
    let all = gaps plan
    let recomputed = VerificationPlan.digestOf plan
    let rulesDocument = rules |> Option.map (fun (r, _, _) -> r)
    let rulesSha256 = rules |> Option.map (fun (_, sha, _) -> sha)
    let problems =
        [ if recomputed <> expectedDigest then yield sprintf "the plan recomputes to digest %s, not the frozen %s" recomputed expectedDigest
          if field "planIdentity" plan |> Option.bind (strOpt "digest") <> Some recomputed then yield "the plan's planIdentity does not match its content"
          match rules with
          | Some(_, actual, frozen) when actual <> frozen -> yield sprintf "the readiness rules hash to %s, not the frozen %s" actual frozen
          | _ -> ()
          yield! ruleProblems rulesDocument all ]
    let rules = rulesDocument
    let permits = all |> List.choose (fun g -> permitted rules g |> Option.map (fun p -> g, p))
    let unresolved = all |> List.filter (fun g -> (permitted rules g).IsNone)
    let checks =
        arr "requirements" plan
        |> List.filter (fun e -> field "exclusion" e |> Option.bind (strOpt "status") <> Some "excluded")
        |> List.collect (arr "obligations")
        |> List.sumBy (fun o -> max 1 (strs "requiredEvidenceCategories" (contract o)).Length)
    let byClass (gs: Gap list) = gs |> List.countBy (fun g -> if g.EvidenceClass = "" then "unclassified" else g.EvidenceClass) |> List.sort |> List.map (fun (k, n) -> k, jint n) |> JObj
    let identity = field "planIdentity" plan |> Option.defaultValue JNull

    JObj
        [ "schema", JStr schemaId
          "plan", JObj [ "planId", jopt JStr (strOpt "planId" identity); "digest", JStr recomputed ]
          "rules", JObj [ "sha256", jopt JStr rulesSha256 ]
          "verdict", JStr(if problems.IsEmpty && unresolved.IsEmpty then "READY" else "BLOCKED")
          "rule", JStr "Implementation may begin only when every mandatory obligation names, for each required evidence category, a bound application test or a praxis:<record kind> producer, or the rehearsal's frozen readiness rules permit the gap with a reason and a decision. A permitted gap is still unverified at acceptance."
          "problems", jstrs problems
          "summary",
          JObj
              [ "obligations", jint (arr "requirements" plan |> List.filter (fun e -> field "exclusion" e |> Option.bind (strOpt "status") <> Some "excluded") |> List.sumBy (arr "obligations" >> List.length))
                "categoryChecks", jint checks
                "resolved", jint (checks - all.Length)
                "unresolved", jint unresolved.Length
                "unresolvedByClass", byClass unresolved
                "unresolvedObligations", jint (unresolved |> List.map (fun g -> g.Obligation) |> List.distinct |> List.length)
                "permitted", jint permits.Length ]
          "unresolved", unresolved |> List.map gapJson |> JArr
          "permitted",
          permits
          |> List.map (fun (g, p) ->
              JObj [ "gap", gapJson g; "reason", JStr(str "reason" p); "decision", JStr(str "decision" p) ])
          |> JArr ]
