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
      TrustedRunId: string }

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
      TrustedRunId = parseFile (Path.Combine(dir, "attestations.json")) |> str "runId" }

let inputs (plan: Json) (f: Fixture) : ProcessVerification.Inputs =
    { Plan = plan
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

/// (name, the diagnostic it must produce, the mutated fixture)
let controls (f: Fixture) : (string * string * Fixture) list =
    let implementer = "example/implementing-agent"
    let parent = f.Ancestry |> Set.remove f.Candidate |> Set.toList |> List.head
    [ "missing approval", "MISSING-APPROVAL", drop (isKind "owner-approval") f
      "incorrect agent identity", "INCORRECT-AGENT-IDENTITY", mapWhere (isKind "release-selection") (setPath [ "actor"; "id" ] (JStr "example/impostor")) f
      "missing checkpoint", "MISSING-CHECKPOINT", drop (isType "work.checkpointed") f
      "unapproved scope change", "UNAPPROVED-SCOPE-CHANGE", mapWhere (isKind "scope-change") (setPath [ "statement"; "decision" ] (JStr "DEC-NEVER-APPROVED")) f
      "self-acceptance",
      "SELF-ACCEPTANCE",
      mapWhere
          (isKind "independent-review")
          (setPath [ "actor"; "id" ] (JStr implementer)
           >> setPath [ "observed"; "execution" ] (JStr(executionOfKind "assignment" f))
           >> setPath [ "observed"; "executionRole" ] (JStr "implementation"))
          f
      "stale review", "STALE-REVIEW", mapWhere (isKind "independent-review") (setPath [ "statement"; "candidate" ] (JStr parent)) f
      "incorrect commit identity", "INCORRECT-COMMIT-IDENTITY", mapWhere (isKind "release-selection") (setPath [ "observed"; "commit" ] (JStr otherSha)) f
      "missing release selection", "MISSING-RELEASE-SELECTION", drop (isKind "release-selection") f
      "evidence reused from another candidate", "EVIDENCE-OTHER-CANDIDATE", mapWhere (isKind "release-selection") (setPath [ "statement"; "candidate" ] (JStr otherSha)) f
      // Beyond the nine required controls: the owner's decision only counts when it is the owner's and attested.
      "owner decision transcribed by an agent",
      "TRANSCRIBED-DECISION",
      mapWhere (isKind "owner-approval") (setPath [ "provenance"; "class" ] (JStr "agent-assertion") >> setPath [ "actor"; "id" ] (JStr implementer)) f
      "owner attestation not observed by CI", "UNATTESTED-OWNER-DECISION", { f with Attestations = setPath [ "runId" ] (JStr "another-run") f.Attestations }
      "a log line claiming verified-acceptance", "TRANSCRIBED-DECISION", mapWhere (isKind "owner-approval") (setPath [ "provenance"; "class" ] (JStr "verified-acceptance")) f ]

let private codesOf (assessment: Json) =
    arr "obligations" assessment
    |> List.collect (arr "categories")
    |> List.filter (fun c -> strOpt "status" c <> Some "verified")
    |> List.collect (arr "diagnostics")
    |> List.map (str "code")
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
    let noBindings = ProcessVerification.assess (inputs committedPlan f)
    let results =
        controls f
        |> List.map (fun (name, code, mutated) ->
            let a = ProcessVerification.assess (inputs proposedPlan mutated)
            name, code, a)
    let readiness plan = PlanReadiness.assess plan None None
    let summaryOf (a: Json) = field "summary" a |> Option.defaultValue JNull

    let failures =
        [ if str "verdict" positive <> "VERIFIED" then
              yield sprintf "process simulation positive: verdict %s, unverified %A, codes %A" (str "verdict" positive) (unverified positive) (codesOf positive)
          if (arr "obligations" positive).Length <> (arr "testBindings" proposal).Length then
              yield sprintf "process simulation positive: %d obligations decided, the proposal binds %d" (arr "obligations" positive).Length (arr "testBindings" proposal).Length
          if str "verdict" noBindings = "VERIFIED" then yield "process simulation: the committed plan (no producer bindings) verified process evidence"
          if VerificationPlan.processRecordKinds <> ProcessVerification.recordKinds then yield "process record kinds differ between VerificationPlan and ProcessVerification"
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
              let permitted = PlanReadiness.assess proposedPlan (Some(permit "deferred to the release checkpoint" "DF-TEST-1" (str "obligation" g))) None
              if intOpt "permitted" (summaryOf permitted) <> Some 1L then yield "readiness: a valid permit was not applied"
              if intOpt "unresolved" (summaryOf permitted) <> Some(int64 ((arr "unresolved" gate).Length - 1)) then yield "readiness: a permit removed more than its own gap"
              let stale = PlanReadiness.assess proposedPlan (Some(permit "x" "DF-TEST-1" "NO-SUCH-O1")) None
              if strs "problems" stale |> List.exists (fun p -> p.Contains "matches no unresolved") |> not then yield "readiness: a stale permit was accepted"
              let reasonless = PlanReadiness.assess proposedPlan (Some(permit "" "" (str "obligation" g))) None
              if (strs "problems" reasonless).Length < 2 || str "verdict" reasonless <> "BLOCKED" then yield "readiness: a permit without reason and decision was accepted"
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

    let gate plan = PlanReadiness.assess plan None None
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
      if not (problemsWith (JArr [ binding "AR-012-O1" "praxis:assignment" "process" [ "process-record" ] ])).IsEmpty then
          yield sprintf "producer binding control: a valid producer binding is refused: %A" (problemsWith (JArr [ binding "AR-012-O1" "praxis:assignment" "process" [ "process-record" ] ])) ]
