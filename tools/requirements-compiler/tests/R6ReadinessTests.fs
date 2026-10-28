/// Rehearsal 6 readiness (ESO-R6-02, -03, -05): the committed plan binds exactly the owner-approved proposals, the
/// implementation-start gate is READY on it, the frozen process-evidence rules are well formed and pinned, and the
/// acceptance job's review converter reads GitHub reviews as the verifier expects.
module R6ReadinessTests

open System
open System.IO
open Json

let private kickoff (source: string) = Path.Combine(source, "kickoff", "rehearsal-006")
let private v2 (source: string) = Path.Combine(source, "requirements-compiled", "v2")

/// `buildPlan bindings` compiles the committed curation with other testBindings.
let bindings (source: string) (buildPlan: Json -> Json) : string list =
    let processProposal = parseFile (Path.Combine(v2 source, "proposals", "process-bindings.proposed.json"))
    let r6Proposal = parseFile (Path.Combine(v2 source, "proposals", "r6-test-bindings.proposed.json"))
    let curation = parseFile (Path.Combine(v2 source, "curation", "verification-plan.json"))
    let committedPlan = parseFile (Path.Combine(v2 source, "verification-plan.json"))
    let processOnly = buildPlan (JArr(arr "testBindings" processProposal))
    let regenerated = TestBindingProposal.propose processOnly
    let expected = TestBindingProposal.applyTo (arr "testBindings" processProposal) r6Proposal
    let gate = PlanReadiness.assess committedPlan (VerificationPlan.digestOf committedPlan) None
    [ if serialize (JArr(arr "testBindings" curation)) <> serialize (JArr expected) then
          yield "r6 bindings: the curation's testBindings are not exactly the process proposal extended by the r6 proposal"
      if serialize regenerated <> serialize r6Proposal then
          yield "r6 bindings: the committed r6 proposal differs from the proposal regenerated from the process-bound plan"
      if str "verdict" gate <> "READY" then
          yield sprintf "r6 bindings: plan-readiness on the committed plan is %s (%A)" (str "verdict" gate) (field "summary" gate)
      // The proposal leaves nothing to an application test that its kind cannot provide, and binds review evidence
      // only to the Praxis independent-review producer.
      for b in arr "testBindings" r6Proposal do
          for t in arr "tests" b do
              let cats = strs "categories" t
              if List.contains "independent-acceptance" cats && (str "id" t <> "praxis:independent-review" || str "kind" t <> "review") then
                  yield sprintf "r6 bindings: %s binds independent acceptance to %s" (str "obligation" b) (str "id" t)
              if List.contains "live-provider" cats && str "kind" t <> "live" then
                  yield sprintf "r6 bindings: %s binds live-provider evidence to a %s test" (str "obligation" b) (str "kind" t) ]

/// A Praxis producer may serve one obligation once per evidence kind; an application test id is unique.
let duplicateIds (problemsWith: Json -> string list) : string list =
    let t id kind cats = JObj [ "id", JStr id; "kind", JStr kind; "categories", jstrs cats ]
    let bind o tests = JArr [ JObj [ "obligation", JStr o; "tests", JArr tests ] ]
    let dup (ps: string list) = ps |> List.exists (fun p -> p.Contains "more than once")
    [ if not (dup (problemsWith (bind "AR-021-O3" [ t "dotnet:X.Y" "deterministic" [ "unit" ]; t "dotnet:X.Y" "deterministic" [ "unit" ] ]))) then
          yield "duplicate ids: a repeated application test id is accepted"
      if not (dup (problemsWith (bind "AR-021-O3" [ t "praxis:independent-review" "review" [ "independent-acceptance" ]; t "praxis:independent-review" "review" [ "independent-acceptance" ] ]))) then
          yield "duplicate ids: a Praxis producer repeated with the same kind is accepted"
      if dup (problemsWith (bind "AR-021-O3" [ t "praxis:independent-review" "process" [ "process-record" ]; t "praxis:independent-review" "review" [ "independent-acceptance" ] ])) then
          yield "duplicate ids: one Praxis producer as a process record and as the independent review is refused" ]

/// The acceptance job's converter: decisions from review bodies, state-changing reviews only, exact references.
let reviewConverter () : string list =
    let review id login state body commit at =
        JObj [ "id", JInt id; "user", JObj [ "login", JStr login ]; "state", JStr state; "body", JStr body; "commit_id", JStr commit; "submitted_at", JStr at ]
    let c = String('c', 40)
    let pulls =
        JArr
            [ JObj
                  [ "number", JInt 7L
                    "reviews",
                    JArr
                        [ review 11L "kemiller2002" "APPROVED" "Kickoff approved.\r\nDecision: DEC-R6-KICKOFF\nDecision: DEC-R6-002 paths: src/a.fs, AR-024\n  decision:  final-disposition  " c "2026-11-01T10:00:00Z"
                          review 12L "kemiller2002" "COMMENTED" "Decision: DEC-IGNORED" c "2026-11-01T10:05:00Z"
                          review 13L "someone" "CHANGES_REQUESTED" "" c "2026-11-01T10:06:00Z" ] ] ]
    let a = ReviewAttestations.build "kemiller2002/indy-init-rehearsal-006" "123" c pulls
    let atts = arr "attestations" a
    let first = atts |> List.tryFind (fun x -> str "reference" x = "kemiller2002/indy-init-rehearsal-006#7@11")
    [ if str "schema" a <> "indy-init.owner-attestations/1" || str "runId" a <> "123" || str "candidate" a <> c then yield "review converter: header"
      if atts.Length <> 2 then yield sprintf "review converter: expected 2 state-changing reviews, got %d" atts.Length
      if atts |> List.exists (fun x -> str "reference" x = "kemiller2002/indy-init-rehearsal-006#7@12") then yield "review converter: a COMMENTED review was kept"
      match first with
      | None -> yield "review converter: the approving review is missing"
      | Some x ->
          let ds = arr "decisions" x
          if ds.Length <> 3 then yield sprintf "review converter: expected 3 decisions, got %d" ds.Length
          match ds with
          | [ JStr "DEC-R6-KICKOFF"; second; JStr "final-disposition" ] ->
              if str "id" second <> "DEC-R6-002" || strs "paths" second <> [ "src/a.fs"; "AR-024" ] then yield "review converter: decision paths"
          | _ -> yield sprintf "review converter: decisions %s" (serialize (JArr ds))
          if str "commit" x <> c || str "state" x <> "APPROVED" || str "login" x <> "kemiller2002" then yield "review converter: review fields" ]

/// The frozen rules are well formed against the committed plan, and every pin the kickoff names is the real hash.
let frozenRules (source: string) : string list =
    let rulesPath = Path.Combine(kickoff source, "process-evidence-rules.json")
    let bytes = File.ReadAllBytes rulesPath
    let sha = Git.sha256Hex bytes
    let plan = parseFile (Path.Combine(v2 source, "verification-plan.json"))
    let digest = VerificationPlan.digestOf plan
    let i : ProcessVerification.Inputs =
        { Plan = plan; ExpectedPlanDigest = digest; Events = []; Executions = Map.empty; Rules = parseBytes bytes
          RulesSha256 = sha; ExpectedRulesSha256 = sha; Attestations = None; TrustedRunId = None
          Candidate = String('a', 40); IsAncestor = fun a b -> a = b }
    let a = ProcessVerification.assess i
    let workflow = File.ReadAllText(Path.Combine(kickoff source, "acceptance-process.yml"))
    let readme = File.ReadAllText(Path.Combine(kickoff source, "README.md"))
    [ if str "verdict" a = "INVALID" then yield sprintf "r6 rules: the frozen rules are refused: %A" (strs "problems" a)
      if str "verdict" a = "VERIFIED" then yield "r6 rules: an empty log verified process evidence"
      for (name, text) in [ "acceptance-process.yml", workflow; "README.md", readme ] do
          if not (text.Contains sha) then yield sprintf "r6 rules: %s does not pin the rules hash %s" name sha
          if not (text.Contains digest) then yield sprintf "r6 rules: %s does not pin the plan digest %s" name digest ]

let all (source: string) (buildPlan: Json -> Json) (problemsWith: Json -> string list) : string list =
    [ yield! (try bindings source buildPlan with ex -> [ "r6 bindings: " + ex.Message ])
      yield! duplicateIds problemsWith
      yield! (try reviewConverter () with ex -> [ "review converter: " + ex.Message ])
      yield! (try frozenRules source with ex -> [ "r6 rules: " + ex.Message ]) ]
