/// Process evidence (Priority One) and the implementation-start gate (Priority Two), tested against a simulated
/// rehearsal that the real Praxis CLI recorded (tools/process-evidence/simulate-rehearsal.sh, committed under
/// tools/fixtures/process-evidence/simulated-rehearsal). Every negative control is one change to that recorded log,
/// so a control fails for the reason it names and not because the fixture is artificial.
module ProcessTests

open System
open System.IO
open Json

type Fixture =
    { Events: Json list
      Executions: Map<string, Json>
      Rules: Json
      RulesSha256: string
      Attestations: Json
      Candidate: string
      Ancestry: Set<string>
      /// The CI run that observed the attestations, as the acceptance job passes it; never read from a mutated file.
      TrustedRunId: string
      /// The frozen plan digest when a control edits the plan; None pins the plan under test as it is.
      PlanPin: string option
      /// The frozen rules hash.
      RulesPin: string }

let fixtureDir (source: string) = Path.Combine(source, "tools", "fixtures", "process-evidence", "simulated-rehearsal")

let load (dir: string) : Fixture =
    let lines (f: string) = File.ReadAllLines(Path.Combine(dir, f)) |> Array.map (fun l -> l.Trim()) |> Array.filter ((<>) "")
    let rules = File.ReadAllBytes(Path.Combine(dir, "rules.json"))
    { Events = lines "events.jsonl" |> Array.map (Text.Encoding.UTF8.GetBytes >> parseBytes) |> List.ofArray
      Executions =
        Directory.GetFiles(Path.Combine(dir, "executions"), "*.json")
        |> Array.map (fun f -> Path.GetFileNameWithoutExtension f, parseFile f)
        |> Map.ofArray
      Rules = parseBytes rules
      RulesSha256 = Git.sha256Hex rules
      Attestations = parseFile (Path.Combine(dir, "attestations.json"))
      Candidate = (lines "candidate.txt").[0]
      Ancestry = lines "ancestry.txt" |> Set.ofArray
      TrustedRunId = parseFile (Path.Combine(dir, "attestations.json")) |> str "runId"
      PlanPin = None
      RulesPin = Git.sha256Hex rules }

let inputs (plan: Json) (f: Fixture) : ProcessVerification.Inputs =
    { Plan = plan
      // The frozen pins are the committed plan's own digest and the fixture rules' hash; a control that edits the
      // plan or the rules therefore fails the pin, exactly as an edited copy would at acceptance.
      ExpectedPlanDigest = f.PlanPin |> Option.defaultValue (VerificationPlan.digestOf plan)
      ExpectedRulesSha256 = f.RulesPin
      Events = f.Events
      Executions = f.Executions
      Rules = f.Rules
      RulesSha256 = f.RulesSha256
      Attestations = Some f.Attestations
      TrustedRunId = Some f.TrustedRunId
      Candidate = f.Candidate
      IsAncestor = fun commit candidate -> commit = candidate || (candidate = f.Candidate && f.Ancestry.Contains commit) }

// ---- mutations of the recorded log ---------------------------------------------------------------------------------

let rec private setPath (path: string list) (value: Json) (j: Json) =
    match path, j with
    | [ key ], JObj ps when ps |> List.exists (fst >> (=) key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | [ key ], JObj ps -> JObj(ps @ [ key, value ])
    | key :: rest, JObj ps -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, setPath rest value v else k, v))
    | _ -> j

let private isKind (k: string) (e: Json) = strOpt "type" e = Some "work.evidence.recorded" && strOpt "kind" e = Some k
let private isType (t: string) (e: Json) = strOpt "type" e = Some t
let private drop (p: Json -> bool) (f: Fixture) = { f with Events = f.Events |> List.filter (p >> not) }
let private mapWhere (p: Json -> bool) (g: Json -> Json) (f: Fixture) = { f with Events = f.Events |> List.map (fun e -> if p e then g e else e) }

let private otherSha = String.replicate 40 "e"

let private executionOfKind (k: string) (f: Fixture) =
    f.Events |> List.find (isKind k) |> field "observed" |> Option.bind (strOpt "execution") |> Option.defaultValue ""

let private append (p: Json -> bool) (g: Json -> Json) (f: Fixture) = { f with Events = f.Events @ (f.Events |> List.filter p |> List.map g) }

/// Re-seals an edited plan with its own correct identity: the plan a careless curator might freeze.
let private reseal (plan: Json) =
    let digest = VerificationPlan.digestOf plan
    match plan with
    | JObj ps -> JObj(ps |> List.map (fun (k, v) -> if k = "planIdentity" then k, setPath [ "digest" ] (JStr digest) v else k, v))
    | p -> p

/// The plan with one obligation's bound tests replaced.
let private rebind (obligation: string) (tests: Json list) (plan: Json) =
    let mapList key f j = match field key j with
                          | Some(JArr xs) -> setPath [ key ] (JArr(List.map f xs)) j
                          | _ -> j
    plan
    |> mapList "requirements" (mapList "obligations" (fun o ->
        if strOpt "sourceObligation" o = Some obligation then setPath [ "acceptanceContract"; "testIdentity"; "tests" ] (JArr tests) o else o))
    |> reseal

let private addAttestation (reference: string) (login: string) (state: string) (decisions: string list) (commit: string option) (f: Fixture) =
    let entry =
        JObj(
            [ "type", JStr "github-review"; "reference", JStr reference; "login", JStr login; "state", JStr state; "decisions", jstrs decisions ]
            @ (commit |> Option.map (fun c -> [ "commit", JStr c ]) |> Option.defaultValue [])
            @ [ "submittedAt", JStr "9999-12-31T00:00:00Z" ]
        )
    { f with Attestations = setPath [ "attestations" ] (JArr(arr "attestations" f.Attestations @ [ entry ])) f.Attestations }

let private addAttestationJson (reference: string) (login: string) (state: string) (decisions: Json) (commit: string option) (f: Fixture) =
    let entry =
        JObj(
            [ "type", JStr "github-review"; "reference", JStr reference; "login", JStr login; "state", JStr state; "decisions", decisions ]
            @ (commit |> Option.map (fun c -> [ "commit", JStr c ]) |> Option.defaultValue [])
            @ [ "submittedAt", JStr "9999-12-31T00:00:00Z" ]
        )
    { f with Attestations = setPath [ "attestations" ] (JArr(arr "attestations" f.Attestations @ [ entry ])) f.Attestations }

let private mapAttestation (reference: string) (g: Json -> Json) (f: Fixture) =
    let mapped = arr "attestations" f.Attestations |> List.map (fun a -> if strOpt "reference" a = Some reference then g a else a)
    { f with Attestations = setPath [ "attestations" ] (JArr mapped) f.Attestations }

/// (name, the diagnostic it must produce, the mutated fixture, the change to the plan under test)
let controls (plan: Json) (f: Fixture) : (string * string * Fixture * Json) list =
    let implementer = "example/implementing-agent"
    let parent = f.Ancestry |> Set.remove f.Candidate |> Set.toList |> List.head
    let reviewExecution = f.Events |> List.find (isKind "independent-review") |> field "observed" |> Option.bind (strOpt "execution") |> Option.defaultValue ""
    let one name code fixture = name, code, fixture, plan
    [ // The nine required controls.
      one "missing approval" "MISSING-APPROVAL" (drop (isKind "owner-approval") f)
      one "incorrect agent identity" "INCORRECT-AGENT-IDENTITY" (mapWhere (isKind "release-selection") (setPath [ "actor"; "id" ] (JStr "example/impostor")) f)
      one "missing checkpoint" "MISSING-CHECKPOINT" (drop (isType "work.checkpointed") f)
      one "unapproved scope change" "UNAPPROVED-SCOPE-CHANGE" (mapWhere (isKind "scope-change") (setPath [ "statement"; "decision" ] (JStr "DEC-NEVER-APPROVED")) f)
      one "self-acceptance" "SELF-ACCEPTANCE" (mapWhere (isKind "independent-review") (setPath [ "actor"; "id" ] (JStr implementer) >> setPath [ "observed"; "execution" ] (JStr(executionOfKind "assignment" f))) f)
      one "stale review" "STALE-REVIEW" (mapWhere (isKind "independent-review") (setPath [ "statement"; "candidate" ] (JStr parent)) f)
      one "incorrect commit identity" "INCORRECT-COMMIT-IDENTITY" (mapWhere (isKind "release-selection") (setPath [ "observed"; "commit" ] (JStr otherSha)) f)
      one "missing release selection" "MISSING-RELEASE-SELECTION" (drop (isKind "release-selection") f)
      one "evidence reused from another candidate" "EVIDENCE-OTHER-CANDIDATE" (mapWhere (isKind "release-selection") (setPath [ "statement"; "candidate" ] (JStr otherSha)) f)
      // Owner decisions count only when they are the owner's, attested by CI in the trusted run, for what they decide.
      one "owner decision transcribed by an agent" "TRANSCRIBED-DECISION" (mapWhere (isKind "owner-approval") (setPath [ "provenance"; "class" ] (JStr "agent-assertion") >> setPath [ "actor"; "id" ] (JStr implementer)) f)
      one "owner attestation not observed by CI" "UNATTESTED-OWNER-DECISION" { f with Attestations = setPath [ "runId" ] (JStr "another-run") f.Attestations }
      one "a log line claiming verified-acceptance" "TRANSCRIBED-DECISION" (mapWhere (isKind "owner-approval") (setPath [ "provenance"; "class" ] (JStr "verified-acceptance")) f)
      one
          "a real attestation reused for another decision"
          "UNAPPROVED-SCOPE-CHANGE"
          (append (isKind "owner-approval") (setPath [ "statement"; "subjects" ] (jstrs [ "DEC-EVIL" ])) f
           |> mapWhere (isKind "scope-change") (setPath [ "statement"; "decision" ] (JStr "DEC-EVIL")))
      one "a later owner rejection withdraws the approval" "UNAPPROVED-SCOPE-CHANGE" (append (isKind "owner-approval") (setPath [ "statement"; "outcome" ] (JStr "rejected")) f)
      // CI records count only from the trusted run, and a failed test never proves anything.
      one "a failed test result" "TEST-NOT-PASSED" (mapWhere (isKind "test-result") (setPath [ "statement"; "outcome" ] (JStr "fail")) f)
      one "a test result from another CI run" "UNTRUSTED-TEST-RESULT" (mapWhere (isKind "test-result") (setPath [ "observed"; "identity"; "runId" ] (JStr "1")) f)
      one
          "a test result asserted by the implementing agent"
          "UNTRUSTED-TEST-RESULT"
          (mapWhere (isKind "test-result") (setPath [ "provenance"; "class" ] (JStr "agent-assertion") >> setPath [ "actor"; "id" ] (JStr implementer)) f)
      // A reviewer comes from the frozen roster and implemented nothing, whatever role it chose for itself.
      one
          "the implementer reviewing under a review role"
          "SELF-ACCEPTANCE"
          ({ mapWhere (isKind "independent-review") (setPath [ "actor"; "id" ] (JStr implementer)) f with
               Executions = f.Executions |> Map.map (fun k v -> if k = reviewExecution then setPath [ "actor"; "id" ] (JStr implementer) v else v) })
      one "a later review rejects the candidate" "REVIEW-NOT-APPROVED" (append (isKind "independent-review") (setPath [ "statement"; "outcome" ] (JStr "rejected")) f)
      one "a checkpoint Praxis did not verify" "UNVERIFIED-CHECKPOINT" (mapWhere (isType "work.checkpointed") (setPath [ "checkpoint"; "verification"; "status" ] (JStr "unverified")) f)
      one "a record without an observed commit" "INCORRECT-COMMIT-IDENTITY" (mapWhere (isKind "release-selection") (setPath [ "observed"; "commit" ] JNull) f)
      one "a record from another repository" "INCORRECT-REPOSITORY" (mapWhere (isKind "release-selection") (setPath [ "repository" ] (JStr "another-repository")) f)
      // The plan and the rules are the frozen ones.
      "a plan edited after freezing (producers rebound)",
      "INVALID",
      { f with PlanPin = Some(VerificationPlan.digestOf plan) },
      (match plan with
       | JObj ps -> JObj(ps @ [ "edited", JBool true ])
       | p -> p)
      one "self-supplied process rules" "INVALID" { f with Rules = setPath [ "agents" ] (jstrs [ implementer; "example/review-agent" ]) f.Rules; RulesSha256 = "0" }
      // Second independent review: a review is anchored outside the log, decisions cover exactly what they name, and
      // the latest word wins for rejections, test results and release selections.
      one "a review written into the log without its attestation" "UNATTESTED-REVIEW" (mapWhere (isKind "independent-review") (setPath [ "statement"; "attestation" ] JNull) f)
      one "a review citing someone else's attestation" "SELF-ACCEPTANCE" (mapWhere (isKind "independent-review") (setPath [ "statement"; "attestation" ] (JStr "github-review:kemiller2002/simulated-rehearsal#1@1001")) f)
      one "a review attested for an older commit" "STALE-REVIEW" (mapAttestation "kemiller2002/simulated-rehearsal#2@2001" (setPath [ "commit" ] (JStr parent)) f)
      one
          "one owner approval cited for a broader scope change"
          "UNAPPROVED-SCOPE-CHANGE"
          (append (isKind "scope-change") (setPath [ "statement"; "subjects" ] (jstrs [ "**/*" ])) f)
      one
          "an owner rejection without an attestation"
          "UNAPPROVED-SCOPE-CHANGE"
          (append (isKind "owner-approval") (setPath [ "statement"; "outcome" ] (JStr "rejected") >> setPath [ "statement"; "attestation" ] JNull) f)
      one "a later failed test from the trusted run" "TEST-NOT-PASSED" (append (isKind "test-result") (setPath [ "statement"; "outcome" ] (JStr "fail")) f)
      one "a later release selection of another candidate" "RELEASE-SUPERSEDED" (append (isKind "release-selection") (setPath [ "statement"; "candidate" ] (JStr otherSha)) f)
      // Third independent review: rejections always withdraw, CI's latest review state decides, owner obligations
      // need the decisions the frozen rules name for them, and dispositions are attested as dispositions.
      one
          "a reviewer's later rejection, written without an attestation"
          "REVIEW-NOT-APPROVED"
          (append (isKind "independent-review") (setPath [ "statement"; "outcome" ] (JStr "rejected") >> setPath [ "statement"; "attestation" ] JNull) f)
      one
          "an owner rejection followed by a replayed approval line"
          "UNAPPROVED-SCOPE-CHANGE"
          (f
           |> append (isKind "owner-approval") (setPath [ "statement"; "outcome" ] (JStr "rejected") >> setPath [ "statement"; "attestation" ] JNull)
           |> append (fun e -> isKind "owner-approval" e && strOpt "outcome" (field "statement" e |> Option.defaultValue JNull) = Some "approved") id)
      one
          "the reviewer's CI review later changed to changes requested"
          "UNATTESTED-REVIEW"
          { f with
              Attestations =
                setPath
                    [ "attestations" ]
                    (JArr(
                        arr "attestations" f.Attestations
                        @ [ JObj [ "type", JStr "github-review"; "reference", JStr "kemiller2002/simulated-rehearsal#2@2002"; "login", JStr "example-reviewer"; "state", JStr "CHANGES_REQUESTED"; "commit", JStr f.Candidate; "submittedAt", JStr "9999-12-31T00:00:00Z" ] ]
                    ))
                    f.Attestations }
      one
          "the owner approved only an unrelated decision"
          "MISSING-APPROVAL"
          (drop (fun e -> isKind "owner-approval" e && (field "statement" e |> Option.bind (strOpt "decision")) = Some "DEC-SIM-KICKOFF") f)
      one
          "a trusted test failure followed by a replayed pass"
          "TEST-NOT-PASSED"
          (f |> append (isKind "test-result") (setPath [ "statement"; "outcome" ] (JStr "fail")) |> append (fun e -> isKind "test-result" e && strOpt "outcome" (field "statement" e |> Option.defaultValue JNull) = Some "pass") id)
      one
          "the candidate re-selected after another was selected"
          "RELEASE-SUPERSEDED"
          (f |> append (isKind "release-selection") (setPath [ "statement"; "candidate" ] (JStr otherSha)) |> append (fun e -> isKind "release-selection" e && strOpt "candidate" (field "statement" e |> Option.defaultValue JNull) = Some f.Candidate) id)
      "the owner's later rejection of the final disposition",
      "DISPOSITION-NOT-ACCEPTED",
      append (isKind "final-disposition") (setPath [ "statement"; "outcome" ] (JStr "not-accepted") >> setPath [ "statement"; "attestation" ] JNull) f,
      rebind "UI-030-O3" [ for k in [ "acceptance-reference"; "release-selection"; "final-disposition" ] -> JObj [ "id", JStr("praxis:" + k); "kind", JStr "process"; "categories", jstrs [ "process-record" ] ] ] plan
      // Fourth independent review: non-approving reviews CI observed withdraw on any pull request, and a scope change
      // stands only on the decision that approved its paths.
      one "the reviewer requested changes on another pull request" "REVIEW-NOT-APPROVED" (addAttestation "kemiller2002/simulated-rehearsal#5@5001" "example-reviewer" "CHANGES_REQUESTED" [] (Some f.Candidate) f)
      one "the owner requested changes on the kickoff decision elsewhere" "MISSING-APPROVAL" (addAttestation "kemiller2002/simulated-rehearsal#6@6001" "kemiller2002" "CHANGES_REQUESTED" [ "DEC-SIM-KICKOFF" ] None f)
      one
          "a scope change citing an unrelated standing decision"
          "UNAPPROVED-SCOPE-CHANGE"
          (mapWhere (isKind "scope-change") (setPath [ "statement"; "decision" ] (JStr "DEC-SIM-KICKOFF")) f)
      one
          "a scope change resting on a rejected decision's path"
          "UNAPPROVED-SCOPE-CHANGE"
          (f
           |> mapWhere (isKind "scope-change") (setPath [ "statement"; "decision" ] (JStr "DEC-SIM-KICKOFF"))
           |> append (fun e -> isKind "owner-approval" e && (field "statement" e |> Option.bind (strOpt "decision")) = Some "DEC-SIM-001") (setPath [ "statement"; "outcome" ] (JStr "rejected")))
      // Fifth independent review: a path is covered only by the decision it is bound to and is withdrawn wherever a
      // withdrawal names it; a scope change must name what it adds; a same-second tie goes to the withdrawal.
      one "a scope change that names nothing" "UNAPPROVED-SCOPE-CHANGE" (mapWhere (isKind "scope-change") (setPath [ "statement"; "subjects" ] (jstrs [])) f)
      one
          "a path the owner later withdrew under another decision"
          "UNAPPROVED-SCOPE-CHANGE"
          (f
           |> mapAttestation "kemiller2002/simulated-rehearsal#1@1001" (setPath [ "decisions" ] (JArr [ JObj [ "id", JStr "DEC-SIM-001"; "paths", jstrs [ "src/engine/Replay.fs"; "src/payments/" ] ] ]))
           |> append (fun e -> isKind "owner-approval" e && (field "statement" e |> Option.bind (strOpt "decision")) = Some "DEC-SIM-001") (setPath [ "statement"; "subjects" ] (jstrs [ "src/payments/" ]))
           |> append (isKind "scope-change") (setPath [ "statement"; "subjects" ] (jstrs [ "src/payments/" ]))
           |> addAttestationJson "kemiller2002/simulated-rehearsal#9@9001" "kemiller2002" "CHANGES_REQUESTED" (JArr [ JObj [ "id", JStr "DEC-SIM-002"; "paths", jstrs [ "src/payments/" ] ] ]) None)
      one
          "a path claimed under a decision the attestation does not bind it to"
          "UNAPPROVED-SCOPE-CHANGE"
          (f
           |> mapAttestation "kemiller2002/simulated-rehearsal#1@1001" (setPath [ "decisions" ] (JArr [ JObj [ "id", JStr "DEC-SIM-001"; "paths", jstrs [ "src/engine/Replay.fs" ] ]; JObj [ "id", JStr "DEC-SIM-002"; "paths", jstrs [ "src/payments/" ] ] ]))
           |> append (fun e -> isKind "owner-approval" e && (field "statement" e |> Option.bind (strOpt "decision")) = Some "DEC-SIM-001") (setPath [ "statement"; "subjects" ] (jstrs [ "src/payments/" ]))
           |> mapWhere (isKind "scope-change") (setPath [ "statement"; "subjects" ] (jstrs [ "src/payments/" ])))
      one
          "an approval and a withdrawal submitted in the same second"
          "MISSING-APPROVAL"
          (let stamp = arr "attestations" f.Attestations |> List.find (fun a -> strOpt "reference" a = Some "kemiller2002/simulated-rehearsal#4@4001") |> str "submittedAt"
           { f with
               Attestations =
                 setPath
                     [ "attestations" ]
                     (JArr(arr "attestations" f.Attestations @ [ JObj [ "type", JStr "github-review"; "reference", JStr "kemiller2002/simulated-rehearsal#4@4000"; "login", JStr "kemiller2002"; "state", JStr "CHANGES_REQUESTED"; "decisions", jstrs [ "DEC-SIM-KICKOFF" ]; "submittedAt", JStr stamp ] ]))
                     f.Attestations })
      "an owner-decision category bound to a record kind that cannot provide it",
      "UNBOUND-PROCESS-EVIDENCE",
      drop (isKind "owner-approval") f,
      rebind "AR-024-O1" [ JObj [ "id", JStr "praxis:checkpoint"; "kind", JStr "process"; "categories", jstrs [ "owner-decision" ] ] ] plan ]

/// Record-level controls: the record never qualifies, whatever obligation it would serve.
let recordControls (f: Fixture) : (string * string * Fixture) list =
    let implementer = "example/implementing-agent"
    [ "the implementer recording the final disposition",
      "TRANSCRIBED-DECISION",
      mapWhere (isKind "final-disposition") (setPath [ "actor"; "id" ] (JStr implementer) >> setPath [ "provenance"; "class" ] (JStr "agent-assertion")) f
      "a final disposition attested for another commit",
      "UNATTESTED-DISPOSITION",
      mapAttestation "kemiller2002/simulated-rehearsal#3@3001" (setPath [ "commit" ] (JStr otherSha)) f
      "a final disposition citing the owner's scope-change approval",
      "UNATTESTED-DISPOSITION",
      mapWhere (isKind "final-disposition") (setPath [ "statement"; "attestation" ] (JStr "github-review:kemiller2002/simulated-rehearsal#1@1001")) f ]

let private codesOf (assessment: Json) =
    (if str "verdict" assessment = "INVALID" then [ "INVALID" ] else [])
    @ (arr "obligations" assessment
    |> List.collect (arr "categories")
    |> List.filter (fun c -> strOpt "status" c <> Some "verified")
    |> List.collect (arr "diagnostics")
    |> List.map (str "code"))
    |> List.distinct

let private unverified (assessment: Json) =
    arr "obligations" assessment
    |> List.collect (fun o -> arr "categories" o |> List.filter (fun c -> strOpt "status" c <> Some "verified") |> List.map (fun c -> str "obligation" o + " " + str "category" c))

/// The simulation's evidence and its failures. `buildPlan` compiles the plan with the given testBindings.
let simulation (source: string) (buildPlan: Json -> Json) : Json * string list =
    let f = load (fixtureDir source)
    let proposal = parseFile (Path.Combine(source, "requirements-compiled", "v2", "proposals", "process-bindings.proposed.json"))
    let committedPlan = parseFile (Path.Combine(source, "requirements-compiled", "v2", "verification-plan.json"))
    let proposedPlan = buildPlan (JArr(arr "testBindings" proposal))
    let positive = ProcessVerification.assess (inputs proposedPlan f)
    let noBindings = ProcessVerification.assess (inputs (buildPlan (JArr [])) f)
    let results =
        controls proposedPlan f
        |> List.map (fun (name, code, mutated, plan) ->
            let a = ProcessVerification.assess (inputs plan mutated)
            name, code, a)
    let recordResults =
        recordControls f
        |> List.map (fun (name, code, mutated) ->
            let a = ProcessVerification.assess (inputs proposedPlan mutated)
            let codes = arr "records" a |> List.filter (fun r -> boolOr "qualifies" true r |> not) |> List.collect (arr "diagnostics") |> List.map (str "code")
            name, code, List.contains code codes)
    let readiness plan = PlanReadiness.assess plan (VerificationPlan.digestOf plan) None
    let withRules plan rules = PlanReadiness.assess plan (VerificationPlan.digestOf plan) (Some(rules, "pinned", "pinned"))
    let summaryOf (a: Json) = field "summary" a |> Option.defaultValue JNull

    let failures =
        [ if str "verdict" positive <> "VERIFIED" then
              yield sprintf "process simulation positive: verdict %s, unverified %A, codes %A" (str "verdict" positive) (unverified positive) (codesOf positive)
          if (arr "obligations" positive).Length <> (arr "testBindings" proposal).Length then
              yield sprintf "process simulation positive: %d obligations decided, the proposal binds %d" (arr "obligations" positive).Length (arr "testBindings" proposal).Length
          if str "verdict" noBindings = "VERIFIED" then yield "process simulation: a plan with no producer bindings verified process evidence"
          if VerificationPlan.digestOf committedPlan <> VerificationPlan.digestOf proposedPlan then yield "process simulation: the committed plan's bindings differ from the approved proposal"
          if VerificationPlan.processRecordKinds <> ProcessVerification.recordKinds then yield "process record kinds differ between VerificationPlan and ProcessVerification"
          for (name, code, caught) in recordResults do
              if not caught then yield sprintf "process record control '%s': no record carries %s" name code
          // The gate repeats compilation's kind rules: an application test cannot resolve an owner-decision category.
          let misbound = rebind "AR-024-O1" [ JObj [ "id", JStr "dotnet:Some.Test"; "kind", JStr "deterministic"; "categories", jstrs [ "owner-decision"; "independent-acceptance" ] ] ] proposedPlan
          let misboundGate = PlanReadiness.assess misbound (VerificationPlan.digestOf misbound) None
          if arr "unresolved" misboundGate |> List.exists (fun g -> strOpt "obligation" g = Some "AR-024-O1" && strOpt "category" g = Some "owner-decision" && strOpt "reason" g = Some "invalid-producer") |> not then
              yield "readiness: an application test was accepted as an owner-decision producer"
          for (name, code, a) in results do
              if str "verdict" a = "VERIFIED" then yield sprintf "process control '%s': verdict VERIFIED" name
              if not (List.contains code (codesOf a)) then yield sprintf "process control '%s': expected %s, got %A" name code (codesOf a)
          // A permitted gap stays reported; a stale or reasonless permit is a problem.
          let gate = readiness proposedPlan
          let firstGap = arr "unresolved" gate |> List.tryHead
          match firstGap with
          | None -> yield "readiness: the proposed plan has no unresolved application-test gap to permit"
          | Some g ->
              let permit reason decision obligation =
                  JObj [ "schema", JStr PlanReadiness.rulesSchemaId
                         "permitUnresolved", JArr [ JObj [ "obligation", JStr obligation; "category", JStr(str "category" g); "reason", JStr reason; "decision", JStr decision ] ] ]
              let permitted = withRules proposedPlan (permit "deferred to the release checkpoint" "DF-TEST-1" (str "obligation" g))
              if intOpt "permitted" (summaryOf permitted) <> Some 1L then yield "readiness: a valid permit was not applied"
              if intOpt "unresolved" (summaryOf permitted) <> Some(int64 ((arr "unresolved" gate).Length - 1)) then yield "readiness: a permit removed more than its own gap"
              let stale = withRules proposedPlan (permit "x" "DF-TEST-1" "NO-SUCH-O1")
              if strs "problems" stale |> List.exists (fun p -> p.Contains "matches no unresolved") |> not then yield "readiness: a stale permit was accepted"
              let reasonless = withRules proposedPlan (permit "" "" (str "obligation" g))
              if (strs "problems" reasonless).Length < 2 || str "verdict" reasonless <> "BLOCKED" then yield "readiness: a permit without reason and decision was accepted"
              let unpinned = PlanReadiness.assess proposedPlan (VerificationPlan.digestOf proposedPlan) (Some(permit "x" "DF-TEST-1" (str "obligation" g), "self-supplied", "frozen"))
              if str "verdict" unpinned <> "BLOCKED" then yield "readiness: rules that do not hash to the frozen value were accepted"
          // A plan edited after freezing, even one that binds everything, is BLOCKED.
          let edited =
              match proposedPlan with
              | JObj ps -> JObj(ps @ [ "edited", JBool true ])
              | p -> p
          if str "verdict" (PlanReadiness.assess edited (VerificationPlan.digestOf proposedPlan) None) <> "BLOCKED" then yield "readiness: an edited plan was not BLOCKED"
          if str "verdict" (readiness committedPlan) <> "BLOCKED" then yield "readiness: the committed plan, which binds nothing, is not BLOCKED" ]

    let evidence =
        JObj
            [ "schema", JStr "indy-init.process-evidence-simulation/1"
              "description",
              JStr
                  "Priority One acceptance: a simulated rehearsal recorded by the real Praxis CLI (tools/process-evidence/simulate-rehearsal.sh) is assessed by verify-process against the v2 plan compiled with the proposed (not approved) process bindings. The positive control must be VERIFIED; each negative control is one change to the recorded log and must leave the affected obligations unverified with the named diagnostic. Generated by the compiler tests (--process-simulation); never edited by hand."
              "fixture",
              JObj
                  [ "candidate", JStr f.Candidate
                    "events", jint f.Events.Length
                    "executions", jint f.Executions.Count
                    "rulesSha256", JStr f.RulesSha256 ]
              "plans",
              JObj
                  [ "committed", field "planIdentity" committedPlan |> Option.defaultValue JNull
                    "withProposedBindings", field "planIdentity" proposedPlan |> Option.defaultValue JNull ]
              "readiness",
              JObj
                  [ "committedPlan", JObj [ "verdict", JStr(str "verdict" (readiness committedPlan)); "summary", summaryOf (readiness committedPlan) ]
                    "withProposedProcessBindings", JObj [ "verdict", JStr(str "verdict" (readiness proposedPlan)); "summary", summaryOf (readiness proposedPlan) ] ]
              "positive", JObj [ "verdict", JStr(str "verdict" positive); "summary", summaryOf positive ]
              "recordControls", recordResults |> List.map (fun (name, code, caught) -> JObj [ "control", JStr name; "expected", JStr code; "pass", JBool caught ]) |> JArr
              "withoutBindings", JObj [ "verdict", JStr(str "verdict" noBindings); "summary", summaryOf noBindings ]
              "controls",
              results
              |> List.map (fun (name, code, a) ->
                  JObj
                      [ "control", JStr name
                        "expected", JStr code
                        "verdict", JStr(str "verdict" a)
                        "diagnostics", jstrs (codesOf a)
                        "unverified", jstrs (unverified a)
                        "pass", JBool(str "verdict" a <> "VERIFIED" && List.contains code (codesOf a)) ])
              |> JArr
              "failures", jstrs failures ]

    evidence, failures

/// Priority Two acceptance: Rehearsal 5's binding problem, replayed through the implementation-start gate. Read-only
/// over the Rehearsal 5 clone; the historical verdict is not recomputed or changed.
let readinessReplay (source: string) (r5: string) (buildPlan: Json -> Json) : Json * string list =
    let kickoffPlan =
        match Git.showFile source "459f65a" "requirements-compiled/v1/verification-plan.json" with
        | Ok b -> parseBytes b
        | Error e -> failwithf "R5's pinned plan (Indy-init 459f65a) unreadable: %s" e
    let run = Path.Combine(r5, "evidence", "acceptance", "run-40620099844-1b9fb56")
    let r5Bindings = parseFile (Path.Combine(run, "curation-test-bindings.json")) |> arr "testBindings"
    let traceability = parseFile (Path.Combine(run, "bundle", "traceability.json")) |> arr "obligations"
    let missingAtEnd = traceability |> List.filter (fun o -> strOpt "binding" o = Some "missing") |> List.map (str "sourceObligation") |> Set.ofList
    let proposal = parseFile (Path.Combine(source, "requirements-compiled", "v2", "proposals", "process-bindings.proposed.json")) |> arr "testBindings"
    let committedPlan = parseFile (Path.Combine(source, "requirements-compiled", "v2", "verification-plan.json"))

    let applicationCategories (o: Json) =
        field "acceptanceContract" o
        |> Option.map (arr "evidenceProducers")
        |> Option.defaultValue []
        |> List.filter (fun p -> strOpt "evidenceClass" p = Some "application-test")
        |> List.map (str "category")
    let contractOf =
        arr "requirements" committedPlan |> List.collect (arr "obligations") |> List.map (fun o -> str "sourceObligation" o, o) |> Map.ofList

    // Upper bound, stated as such: every R5 binding is assumed to prove all of its obligation's application-test
    // categories. R5's bindings declare no categories, so this is the most they could have resolved.
    let generous =
        r5Bindings
        |> List.choose (fun b ->
            let o = str "obligation" b
            match contractOf.TryFind o |> Option.map applicationCategories with
            | Some(_ :: _ as cats) ->
                Some(JObj [ "obligation", JStr o; "tests", arr "tests" b |> List.map (fun t -> JObj [ "id", JStr(str "id" t); "kind", JStr(str "kind" t); "categories", jstrs cats ]) |> JArr ])
            | _ -> None)
    let merged =
        let producers = proposal |> List.map (fun b -> str "obligation" b, arr "tests" b) |> Map.ofList
        let app = generous |> List.map (fun b -> str "obligation" b, arr "tests" b) |> Map.ofList
        Set.union (Set.ofSeq (Map.keys producers)) (Set.ofSeq (Map.keys app))
        |> Set.toList
        |> List.map (fun o ->
            JObj [ "obligation", JStr o; "tests", JArr((app.TryFind o |> Option.defaultValue []) @ (producers.TryFind o |> Option.defaultValue [])) ])

    // Each plan is pinned as it is: the replay asks what the gate says about it, not whether it was frozen.
    let gate plan = PlanReadiness.assess plan (VerificationPlan.digestOf plan) None
    let atKickoff = gate kickoffPlan
    let asBound = gate (buildPlan (JArr r5Bindings))
    let upperBound = gate (buildPlan (JArr generous))
    let withProcess = gate (buildPlan (JArr merged))
    let gapObligations (g: Json) = arr "unresolved" g |> List.map (str "obligation") |> Set.ofList
    let flaggedOfMissing (g: Json) = Set.intersect missingAtEnd (gapObligations g)
    let processAtStart (g: Json) =
        arr "unresolved" g |> List.filter (fun x -> strOpt "evidenceClass" x <> Some "application-test") |> List.map (str "obligation") |> Set.ofList
    let summaryOf (g: Json) = field "summary" g |> Option.defaultValue JNull
    let row label note (g: Json) =
        JObj
            [ "plan", JStr label
              "note", JStr note
              "verdict", JStr(str "verdict" g)
              "summary", summaryOf g
              "missingAtEndFlaggedBeforeImplementation", jint (flaggedOfMissing g).Count ]

    let failures =
        [ if str "verdict" atKickoff <> "BLOCKED" then yield "readiness replay: R5's kickoff plan would not have been BLOCKED"
          if (flaggedOfMissing upperBound).Count <> missingAtEnd.Count then
              yield sprintf "readiness replay: only %d of the %d obligations R5 left unproven are flagged before implementation" (flaggedOfMissing upperBound).Count missingAtEnd.Count
          if missingAtEnd.Count <> 49 then yield sprintf "readiness replay: R5's final traceability lists %d unproven obligations, expected 49" missingAtEnd.Count ]

    JObj
        [ "schema", JStr "indy-init.readiness-replay/1"
          "description",
          JStr
              "Priority Two acceptance: the implementation-start gate (requirements-compiler plan-readiness) applied to Rehearsal 5's plans. Read-only; Rehearsal 5's historical verdict (INCOMPLETE, NOT ACCEPTED) is not recomputed or changed. Generated by the compiler tests (--readiness-replay); never edited by hand."
          "rehearsal5",
          JObj
              [ "candidate", JStr "1b9fb56873fe7a98dffd5172066d909449c17eae"
                "ciRun", JStr "40620099844"
                "kickoffPlan", field "planIdentity" kickoffPlan |> Option.defaultValue JNull
                "finalBindings", jint r5Bindings.Length
                "finalBindingsDeclaringCategories", jint (r5Bindings |> List.filter (fun b -> arr "tests" b |> List.exists (fun t -> not (strs "categories" t).IsEmpty)) |> List.length)
                "unprovenAtEnd", jint missingAtEnd.Count ]
          "gate",
          JArr
              [ row "R5 kickoff plan VP-e77a2134e1b0 (Indy-init 459f65a)" "the plan R5 began implementation with: no acceptance contracts and no test identity" atKickoff
                row "v2 plan with R5's final test bindings, as R5 wrote them" "R5's bindings declare no evidence categories, so under v2 none resolves anything" asBound
                row "v2 plan with R5's final bindings, upper bound" "every R5 binding assumed to prove all application-test categories of its obligation (hypothetical; R5 never stated this)" upperBound
                row "upper bound plus the proposed process bindings" "the proposed (not approved) praxis:<kind> producers added" withProcess ]
          "processEvidenceNeededAtStart",
          JObj
              [ "obligations", jstrs (processAtStart upperBound |> Set.toList)
                "ofWhichUnprovenAtEnd", jint (Set.intersect (processAtStart upperBound) missingAtEnd).Count
                "ofWhichR5ClaimedByTests", jstrs (Set.difference (processAtStart upperBound) missingAtEnd |> Set.toList) ]
          "failures", jstrs failures ],
    failures

/// A producer binding the process verifier could not check is refused when the plan is compiled.
let producerBindingControls (problemsWith: Json -> string list) : string list =
    let binding o (id: string) kind (cats: string list) = JObj [ "obligation", JStr o; "tests", JArr [ JObj [ "id", JStr id; "kind", JStr kind; "categories", jstrs cats ] ] ]
    let refused name (needle: string) b =
        if problemsWith (JArr [ b ]) |> List.exists (fun p -> p.Contains(needle, StringComparison.Ordinal)) then [] else [ sprintf "producer binding control '%s': accepted" name ]
    [ yield! refused "process kind without a praxis producer" "must name a producer" (binding "AR-012-O1" "dotnet:Some.Test" "process" [ "process-record" ])
      yield! refused "unknown record kind" "unknown Praxis record kind" (binding "AR-012-O1" "praxis:telepathy" "process" [ "process-record" ])
      yield! refused "owner evidence from a non-approval record" "only from praxis:owner-approval" (binding "AR-024-O1" "praxis:release-selection" "owner" [ "owner-decision" ])
      yield! refused "an application test kind claiming process evidence" "cannot provide process-record" (binding "AR-012-O1" "praxis:assignment" "deterministic" [ "process-record" ])
      yield! refused "a process record claiming application evidence" "cannot provide integration" (binding "VER-001-O1" "praxis:dependency-decision" "process" [ "integration" ])
      yield!
          refused
              "a Praxis producer and a ledger test bound to the same category"
              "binds both a Praxis producer and a ledger test"
              (JObj [ "obligation", JStr "AR-008-O1"
                      "tests", JArr [ JObj [ "id", JStr "praxis:independent-review"; "kind", JStr "review"; "categories", jstrs [ "independent-acceptance" ] ]
                                      JObj [ "id", JStr "review:T33"; "kind", JStr "review"; "categories", jstrs [ "independent-acceptance" ] ] ] ])
      if not (problemsWith (JArr [ binding "AR-012-O1" "praxis:assignment" "process" [ "process-record" ] ])).IsEmpty then
          yield sprintf "producer binding control: a valid producer binding is refused: %A" (problemsWith (JArr [ binding "AR-012-O1" "praxis:assignment" "process" [ "process-record" ] ])) ]
