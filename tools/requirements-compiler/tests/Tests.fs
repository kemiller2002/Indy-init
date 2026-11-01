/// Tests for requirements-compiler.
///
/// Offline (indy-init history only; run in CI):
///   dotnet run --project tools/requirements-compiler/tests -- --offline --source .
///     - the F# baseline port reproduces the committed Python-generated baseline byte for byte;
///     - unit cases ported from tools/test-requirements-baseline.py (removed 2026-11-01);
///     - the preflight verdict rule;
///     - defect-class closures checked against a throwaway git history (R7-REM-008);
///     - the pre-lock release comparison (R7-REM-010);
///     - token economics (Amendment D) over the recorded fixture in tests/fixtures/token-economics;
///     - the assertion ratchet over a throwaway git history (R7-REM-011, RF-F-8);
///     - the verification plan, and the EII-C acceptance-contract negative controls (missing obligation, incorrect
///       test mapping, altered values, swapped requirement and obligation identities, stale evidence,
///       unauthorized exclusion, related-test-is-not-proof, unclassified obligations).
///
/// Full (needs the checkouts named in curation/sources.json):
///   dotnet run --project tools/requirements-compiler/tests -- <the compiler's own arguments>
///     - offline tests, plus negative controls: each case copies the curation into a temporary directory,
///       injects one defect, compiles, and asserts the named check fails and the compiler exits as expected;
///     - determinism (two compilations are byte-identical) and `--check` against the committed package.
///
/// EII-C validation against the Rehearsal 4 and 5 baselines (see RehearsalBaselines.fs):
///   dotnet run --project tools/requirements-compiler/tests -- --rehearsal-baselines --source . --r4 DIR --r5 DIR --out FILE
module Tests

open System
open System.IO
open System.Text.RegularExpressions
open Json

let private copyDir (src: string) (dst: string) =
    Directory.CreateDirectory dst |> ignore
    Directory.GetFiles src |> Array.iter (fun f -> File.Copy(f, Path.Combine(dst, Path.GetFileName f), true))

/// Replaces exactly one occurrence; a mutation that does not apply is itself a test failure.
let private edit (path: string) (oldText: string) (newText: string) =
    let text = File.ReadAllText path
    let at = text.IndexOf(oldText, StringComparison.Ordinal)
    if at < 0 then failwithf "mutation target not found in %s: %s" path oldText
    File.WriteAllText(path, text.Substring(0, at) + newText + text.Substring(at + oldText.Length))

/// Structural mutation of a curation list: f receives and returns the list under listKey.
let private mutateList (path: string) (listKey: string) (f: Json list -> Json list) =
    match parseFile path with
    | JObj top -> File.WriteAllText(path, serialize (JObj(top |> List.map (fun (k, v) -> if k = listKey then k, JArr(f (arr listKey (JObj top))) else k, v))))
    | _ -> failwithf "%s is not a JSON object" path

/// Structural mutation: replaces one existing field of the item with the given id.
let private setField (path: string) (listKey: string) (id: string) (key: string) (value: Json) =
    mutateList path listKey (fun items ->
        if not (items |> List.exists (fun j -> strOpt "id" j = Some id)) then failwithf "mutation target %s not found in %s" id path
        items
        |> List.map (function
            | JObj props when strOpt "id" (JObj props) = Some id ->
                if not (props |> List.exists (fun (k, _) -> k = key)) then failwithf "%s has no field %s" id key
                JObj(props |> List.map (fun (k, v) -> if k = key then k, value else k, v))
            | j -> j))

let private quiet (f: unit -> 'a) =
    let out, err = Console.Out, Console.Error
    Console.SetOut TextWriter.Null
    Console.SetError TextWriter.Null
    try f () finally
        Console.SetOut out
        Console.SetError err

type Case =
    { Name: string
      Mutate: string -> unit
      SuppliedBaseline: (string -> string) option
      ExpectExit: int
      ExpectFailing: string list }

let private case name mutate exit failing =
    { Name = name; Mutate = mutate; SuppliedBaseline = None; ExpectExit = exit; ExpectFailing = failing }

let private failingChecks (outDir: string) =
    let report = Path.Combine(outDir, "requirements-validation-report.json")
    if File.Exists report then parseFile report |> arr "checks" |> List.filter (fun c -> str "status" c = "FAIL") |> List.map (str "id")
    else []

let private sourceCommit (curationDir: string) =
    parseFile (Path.Combine(curationDir, "sources.json")) |> arr "repositories" |> List.find (fun r -> str "name" r = "indy-init") |> str "commit"

let private buildBaselineAt (source: string) (commit: string) =
    let read p =
        match Git.showFile source commit p with
        | Ok b -> Text.utf8 b
        | Error e -> failwithf "cannot read %s at %s: %s" p commit e
    Baseline.build read commit |> serializePythonStyle

let private runCase (o: Program.Options) (c: Case) : string list =
    let tmp = Path.Combine(Path.GetTempPath(), "rc-test-" + c.Name)
    if Directory.Exists tmp then Directory.Delete(tmp, true)
    let curation = Path.Combine(tmp, "curation")
    copyDir o.Curation curation

    let result =
        try
            c.Mutate curation
            let baseline =
                match c.SuppliedBaseline with
                | None -> ""
                | Some transform ->
                    let p = Path.Combine(tmp, "baseline.json")
                    File.WriteAllText(p, transform (buildBaselineAt o.Source (sourceCommit curation)))
                    p
            let out = Path.Combine(tmp, "out")
            let code = quiet (fun () -> Program.compile { o with Curation = curation; Baseline = baseline; Out = out; Check = false })
            let failing = failingChecks out
            [ if code <> c.ExpectExit then yield sprintf "%s: exit %d, expected %d" c.Name code c.ExpectExit
              for id in c.ExpectFailing do
                  if not (List.contains id failing) then yield sprintf "%s: expected %s to fail; failing were [%s]" c.Name id (String.concat ", " failing)
              if c.ExpectFailing.IsEmpty && c.ExpectExit <> 2 && not failing.IsEmpty then yield sprintf "%s: unexpected failing checks [%s]" c.Name (String.concat ", " failing) ]
        with ex -> [ sprintf "%s: %s" c.Name ex.Message ]
    Directory.Delete(tmp, true)
    result

let private cases: Case list =
    let cur (f: string) (dir: string) = Path.Combine(dir, f)
    let dep task reason = JObj [ "task", JStr task; "reason", JStr reason ]

    [ case "positive-control" ignore 0 []
      case "citation-not-verbatim" (fun d -> edit (cur "findings.json" d) "published by a non-forced ref update" "published by a forced ref update") 3 [ "V11" ]
      case "dependency-cycle" (fun d -> setField (cur "tasks.json" d) "tasks" "T00" "dependsOn" (JArr [ dep "T34" "injected cycle" ])) 3 [ "V05"; "V09" ]
      case "requirement-out-of-scope" (fun d -> setField (cur "tasks.json" d) "tasks" "T25" "covers" (jstrs [ "UI-001" ])) 3 [ "V09" ]
      case "fixed-decision-unsourced" (fun d -> setField (cur "decisions.json" d) "decisions" "DEC-020" "sources" (JArr [])) 3 [ "V08" ]
      case "contract-over-claimed" (fun d -> setField (cur "contracts.json" d) "contracts" "CT-009" "status" (JStr "verified")) 3 [ "V07" ]
      case "identity-mismatch" (fun d -> edit (cur "contracts.json" d) "8c361929cfb429c31c059c92d6148036bf2b5861f81dace635a1f7de18dbf87c\"" "0000000000000000000000000000000000000000000000000000000000000000\"") 3 [ "V07" ]
      case "conflict-silently-accepted" (fun d -> setField (cur "findings.json" d) "findings" "F-011" "decisions" (jstrs [ "DEC-013"; "DEC-001" ])) 3 [ "V03" ]
      case "conflict-resolved-without-source" (fun d -> setField (cur "findings.json" d) "findings" "F-007" "status" (JStr "resolved-by-precedence")) 3 [ "V03" ]
      case "unknown-reference" (fun d -> setField (cur "decisions.json" d) "decisions" "DEC-020" "requirements" (jstrs [ "R9.1"; "UI-999" ])) 3 [ "V04" ]
      case "repository-revision-mismatch" (fun d -> edit (cur "sources.json" d) "\"commit\": \"22260f429501d0d6963fc33302ed1e64d742a374\"" "\"commit\": \"1111111111111111111111111111111111111111\"") 2 []
      case "undefined-metric" (fun d -> edit (cur "experiment.json" d) "\"perceptaMetric\": \"tool.calls\"" "\"perceptaMetric\": \"tool.invented_calls\"") 3 [ "V13" ]
      case "verification-rule-removed" (fun d ->
          edit (cur "verification-rules.json" d) "\"match\": [\"all\"]" "\"match\": [\"origin:baseline\"]"
          edit (cur "verification-rules.json" d) "\"match\": [\"origin:external\"]" "\"match\": [\"origin:none\"]") 3 [ "V06" ]
      // No readiness blocker remains in the package, so the case makes one: F-011, open and material,
      // raised to critical, carries no blocker class.
      case "blocker-unclassified" (fun d -> setField (cur "findings.json" d) "findings" "F-011" "severity" (JStr "critical")) 3 [ "V15" ]
      case "decision-without-recommendation" (fun d ->
          setField (cur "decisions.json" d) "decisions" "DEC-022" "classification" (JStr "unresolved")
          setField (cur "decisions.json" d) "decisions" "DEC-022" "recommendation" (JStr "")) 3 [ "V15" ]
      case "defect-class-test-missing" (fun d -> mutateList (cur "defect-class-tests.json" d) "tests" (List.filter (fun t -> str "defectClass" t <> "DC-006"))) 3 [ "V16" ]
      case "preflight-depends-on-timed" (fun d -> setField (cur "tasks.json" d) "tasks" "T05" "dependsOn" (JArr [ dep "T12" "injected" ])) 3 [ "V16" ]
      case "exclusion-by-undecided-decision" (fun d -> setField (cur "decisions.json" d) "decisions" "DEC-022" "classification" (JStr "unresolved")) 3 [ "V16" ]
      // Verification plan (V18): an exclusion without an authenticated operator approval, and a test
      // binding to an obligation that is not a baseline P0 obligation, each fail compilation.
      case "plan-unauthorized-exclusion" (fun d ->
          mutateList (cur "verification-plan.json" d) "exclusions" (fun _ -> [ JObj [ "requirement", JStr "PF-005"; "approval", JStr "APR-INVENTED" ] ])) 3 [ "V18" ]
      case "plan-binding-unknown-obligation" (fun d ->
          mutateList (cur "verification-plan.json" d) "testBindings" (fun _ ->
              [ JObj [ "obligation", JStr "PF-999-O1"; "tests", JArr [ JObj [ "id", JStr "core: invented"; "kind", JStr "deterministic" ] ] ] ])) 3 [ "V18" ]
      // Evidence categories (EII-C): a rule assigning an undeclared category, and a binding that declares no
      // category or one its kind cannot provide, each fail compilation.
      case "plan-category-undeclared" (fun d ->
          mutateList (cur "evidence-categories.json" d) "rules" (fun rs ->
              rs @ [ JObj [ "id", JStr "EC-INVENTED"; "category", JStr "performance"; "when", JObj [ "rules", jstrs [ "VR-UI" ] ]; "source", JStr "injected" ] ])) 3 [ "V18" ]
      case "plan-binding-without-category" (fun d ->
          mutateList (cur "verification-plan.json" d) "testBindings" (fun _ ->
              [ JObj [ "obligation", JStr "PF-015-O1"; "tests", JArr [ JObj [ "id", JStr "live: injected"; "kind", JStr "live" ] ] ] ])) 3 [ "V18" ]
      case "plan-binding-kind-cannot-provide" (fun d ->
          mutateList (cur "verification-plan.json" d) "testBindings" (fun _ ->
              [ JObj [ "obligation", JStr "PF-015-O1"; "tests", JArr [ JObj [ "id", JStr "core: injected"; "kind", JStr "deterministic"; "categories", jstrs [ "live-provider" ] ] ] ] ])) 3 [ "V18" ]
      { case "baseline-obligation-tampered" ignore 3 [ "V01" ] with
          SuppliedBaseline = Some(fun text -> text.Replace("Use these initial canonical palette values:", "Use these optional palette values:")) } ]

// ---- verification plan (offline: committed baseline, committed verification manifest, git history) ----

let private r4BaselineCommit = "aab8f80c7743392e794b3b631542903ed4ed6f64"
let private r4BaselineSha256 = "621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9"

/// Plan inputs from committed files only, so CI can build the plan without the other checkouts.
let planInputs (source: string) (baselineText: string) : VerificationPlan.Inputs =
    let v2 = Path.Combine(source, "requirements-compiled", "v2")
    let spec = parseFile (Path.Combine(v2, "curation", "verification-plan.json"))
    let categoriesPath = Path.Combine(v2, "curation", "evidence-categories.json")
    let a = field "approvals" spec |> Option.defaultValue JNull
    let approvals =
        match Git.showFile source (str "commit" a) (str "path" a) with
        | Ok b -> b
        | Error e -> failwithf "approvals unreadable: %s" e
    let baselineBytes = Text.Encoding.UTF8.GetBytes baselineText
    let baseline = parseBytes baselineBytes
    let src = field "source" baseline |> Option.defaultValue JNull

    { Repository = str "repository" src
      Commit = str "commit" src
      BaselineId = str "id" baseline
      BaselineSha256 = Git.sha256Hex baselineBytes
      Baseline = baseline
      Manifest = parseFile (Path.Combine(v2, "verification-manifest.json"))
      Spec = spec
      Approvals =
        { Repository = "kemiller2002/indy-init"
          Commit = str "commit" a
          Path = str "path" a
          Sha256 = Git.sha256Hex approvals
          TrustedOperators = strs "trustedOperators" a
          Document = parseBytes approvals }
      Categories = { Path = "curation/evidence-categories.json"; Sha256 = Git.sha256Hex (File.ReadAllBytes categoriesPath); Table = parseFile categoriesPath } }

let private withSpec (i: VerificationPlan.Inputs) (key: string) (value: Json) =
    match i.Spec with
    | JObj ps -> { i with Spec = JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v)) }
    | _ -> i

let private mapRequirements (f: Json list -> Json list) (plan: Json) =
    match plan with
    | JObj ps -> JObj(ps |> List.map (fun (k, v) -> if k = "requirements" then k, JArr(f (arr "requirements" plan)) else k, v))
    | j -> j

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | j -> j

let private mutateJsonList (key: string) (f: Json list -> Json list) (j: Json) = setIn key (JArr(f (arr key j))) j

/// Re-seals a mutated plan with a correct identity, so a preservation failure cannot hide behind a digest failure.
let private reseal (plan: Json) =
    let d = VerificationPlan.digestOf plan
    let identity = field "planIdentity" plan |> Option.defaultValue (JObj []) |> setIn "digest" (JStr d) |> setIn "planId" (JStr("VP-" + d.Substring(0, 12)))
    setIn "planIdentity" identity plan

let private planTests (source: string) : string list =
    let committedBaseline = File.ReadAllText(Path.Combine(source, "requirements", "causa-requirements-baseline.json"))
    let i = planInputs source committedBaseline
    let plan = VerificationPlan.build i
    let text = serialize plan
    let entryOf rid (p: Json) = arr "requirements" p |> List.find (fun e -> str "requirement" e = rid)
    let obligation oid (e: Json) = arr "obligations" e |> List.find (fun o -> str "sourceObligation" o = oid)
    let tiStatus (j: Json) = field "testIdentity" j |> Option.map (str "status") |> Option.defaultValue ""
    let scopeInt key (p: Json) = field "scope" p |> Option.bind (intOpt key) |> Option.defaultValue -1L
    let hasProblem (needle: string) (ps: string list) = ps |> List.exists (fun p -> p.Contains(needle, StringComparison.Ordinal))

    // Determinism: built twice from the same inputs, byte-identical, and equal to the committed artifact.
    let determinism =
        let committedPlan = Path.Combine(source, "requirements-compiled", "v2", "verification-plan.json")
        [ if serialize (VerificationPlan.build i) <> text then yield "determinism: two builds differ"
          if File.Exists committedPlan && File.ReadAllText committedPlan <> text then yield "determinism: committed verification-plan.json differs from a rebuild" ]

    // No missing P0: at the R4 baseline (aab8f80) exactly 108 P0 requirements, all represented once.
    let completeness =
        let r4 = buildBaselineAt source r4BaselineCommit
        let r4Plan = VerificationPlan.build (planInputs source r4)
        [ if Git.sha256Hex (Text.Encoding.UTF8.GetBytes r4) <> r4BaselineSha256 then yield "R4 baseline: rebuilt baseline sha256 differs from 621e2f96...57b9"
          if (VerificationPlan.p0Of (parseBytes (Text.Encoding.UTF8.GetBytes r4))).Length <> 108 then yield "R4 baseline: expected 108 P0 requirements"
          if scopeInt "requirements" r4Plan <> 108L || (arr "requirements" r4Plan).Length <> 108 then yield "R4 baseline: plan does not carry exactly 108 P0 requirements"
          if not (VerificationPlan.preservationProblems (parseBytes (Text.Encoding.UTF8.GetBytes r4)) r4Plan).IsEmpty then yield "R4 baseline: plan fails preservation"
          if not (VerificationPlan.problems i).IsEmpty then yield sprintf "committed inputs: unexpected problems %A" (VerificationPlan.problems i)
          if not (VerificationPlan.preservationProblems i.Baseline (parseBytes (Text.Encoding.UTF8.GetBytes text))).IsEmpty then yield "committed inputs: plan fails preservation" ]

    // Stable obligation ids: a fixed value, independent of bindings and order, and sensitive to the text.
    let stableIds =
        let text1 = "Every run must pin authoritative requirements and their source repository, commit, and content digest."
        let bound = withSpec i "testBindings" (JArr [ JObj [ "obligation", JStr "AR-001-O2"; "tests", JArr [ JObj [ "id", JStr "t"; "kind", JStr "deterministic"; "categories", jstrs [ "unit" ] ] ] ] ])
        let ids (p: Json) = arr "requirements" p |> List.collect (arr "obligations") |> List.map (str "id")
        [ if VerificationPlan.obligationId "AR-001" "AR-001-O1" text1 <> "VPO-a5795f29e0de763c" then yield "stable ids: AR-001-O1 id changed"
          if str "id" (obligation "AR-001-O1" (entryOf "AR-001" plan)) <> "VPO-a5795f29e0de763c" then yield "stable ids: plan AR-001-O1 id changed"
          if ids (VerificationPlan.build bound) <> ids plan then yield "stable ids: a test binding changed obligation ids"
          if VerificationPlan.obligationId "AR-001" "AR-001-O1" (text1 + " ") = "VPO-a5795f29e0de763c" then yield "stable ids: a text change kept the id"
          if (ids plan |> List.distinct).Length <> (ids plan).Length then yield "stable ids: obligation ids collide" ]

    // Missing-test detection: unbound obligations are 'missing'; a binding makes exactly that one 'bound'.
    let missingTests =
        let binding o tests = JObj [ "obligation", JStr o; "tests", JArr tests ]
        let t id kind = JObj [ "id", JStr id; "kind", JStr kind; "categories", jstrs [ (if kind = "review" then "independent-acceptance" else "integration") ] ]
        let one = withSpec i "testBindings" (JArr [ binding "AR-001-O1" [ t "core: AR-001 pins the baseline" "deterministic" ] ])
        let p1 = VerificationPlan.build one
        let liveOne = VerificationPlan.build (withSpec i "testBindings" (JArr [ binding "AR-001-O1" [ t "live: AR-001" "live" ] ]))
        // Built from the same inputs with no bindings, so the check holds whether or not the committed curation binds tests.
        let unbound = VerificationPlan.build (withSpec i "testBindings" (JArr []))
        [ if scopeInt "missingTestObligations" unbound <> scopeInt "obligations" unbound || scopeInt "boundObligations" unbound <> 0L then yield "missing tests: unbound plan must report every obligation missing"
          if arr "requirements" unbound |> List.exists (fun e -> tiStatus e <> "missing") then yield "missing tests: a requirement without bindings is not 'missing'"
          if tiStatus (obligation "AR-001-O1" (entryOf "AR-001" p1)) <> "bound" then yield "missing tests: bound obligation not 'bound'"
          if tiStatus (obligation "AR-001-O2" (entryOf "AR-001" p1)) <> "missing" then yield "missing tests: sibling obligation not 'missing'"
          if tiStatus (entryOf "AR-001" p1) <> "partial" then yield "missing tests: partly bound requirement not 'partial'"
          if scopeInt "boundObligations" p1 <> 1L then yield "missing tests: bound count not 1"
          if field "freshness" (entryOf "AR-001" liveOne) |> Option.map (str "policy") <> Some "FP-LIVE-PER-COMMIT" then yield "freshness: a live test does not force FP-LIVE-PER-COMMIT"
          if not (VerificationPlan.problems (withSpec i "testBindings" (JArr [ binding "AR-001-O1" [] ])) |> hasProblem "names no test") then yield "missing tests: an empty binding is accepted"
          if not (VerificationPlan.problems (withSpec i "testBindings" (JArr [ binding "XX-001-O1" [ t "x" "deterministic" ] ])) |> hasProblem "unknown or non-P0 obligation") then yield "missing tests: a binding to an unknown obligation is accepted"
          if not (VerificationPlan.problems (withSpec i "testBindings" (JArr [ binding "AR-001-O1" [ t "x" "flaky" ] ])) |> hasProblem "unknown kind") then yield "missing tests: an unknown test kind is accepted" ]

    // Requirements preservation: any loss or alteration of a P0 requirement is detected, even when re-sealed.
    let preservation =
        let pp p = VerificationPlan.preservationProblems i.Baseline p
        let tamperObligation (e: Json) =
            if str "requirement" e <> "PF-005" then e
            else setIn "obligations" (JArr(arr "obligations" e |> List.mapi (fun k o -> if k = 0 then setIn "text" (JStr "Weakened text.") o else o))) e
        [ if not (pp (reseal (mapRequirements (List.filter (fun e -> str "requirement" e <> "PF-005")) plan)) |> hasProblem "missing P0 requirement: PF-005") then yield "preservation: a dropped P0 requirement is not detected"
          if not (pp (reseal (mapRequirements (List.map (fun e -> if str "requirement" e = "PF-005" then setIn "priority" (JStr "P1") e else e)) plan)) |> hasProblem "priority changed") then yield "preservation: a downgraded priority is not detected"
          if not (pp (reseal (mapRequirements (List.map tamperObligation) plan)) |> List.exists (fun p -> p.Contains("[ALTERED-VALUE] PF-005-O1", StringComparison.Ordinal) && p.Contains("text differs", StringComparison.Ordinal))) then yield "preservation: an altered obligation is not detected"
          if not (pp (mapRequirements (List.filter (fun e -> str "requirement" e <> "PF-005")) plan) |> hasProblem "digest does not match") then yield "preservation: an unsealed edit is not detected by the plan identity"
          if not (pp (reseal (mapRequirements (fun es -> es @ [ setIn "requirement" (JStr "PF-999") es.Head ]) plan)) |> hasProblem "not a baseline P0 requirement") then yield "preservation: an invented requirement is not detected" ]

    // Unauthorized exclusion: rejected and never applied; an authenticated operator approval is honoured.
    let exclusions =
        let excl rid approval = JArr [ JObj [ "requirement", JStr rid; "approval", JStr approval ] ]
        let unauthorized = withSpec i "exclusions" (excl "PF-005" "APR-INVENTED")
        let approvalDoc kind id =
            JObj
                [ "schema", JStr "causa.requirements-approvals/1"
                  "baseline_sha256", JStr i.BaselineSha256
                  "approvals",
                  JArr
                      [ JObj
                            [ "id", JStr "APR-TEST"
                              "authority", JObj [ "kind", JStr kind; "id", JStr id ]
                              "changes", JArr [ JObj [ "requirement_id", JStr "PF-005"; "action", JStr "exclude" ] ] ] ] ]
        let withApprovals doc (x: VerificationPlan.Inputs) = { x with Approvals = { x.Approvals with Document = doc } }
        let authorized = withSpec i "exclusions" (excl "PF-005" "APR-TEST") |> withApprovals (approvalDoc "operator" "kemiller2002")
        let wrongAuthority = withSpec i "exclusions" (excl "PF-005" "APR-TEST") |> withApprovals (approvalDoc "agent" "claude")
        let excludedStatus (p: Json) = field "exclusion" (entryOf "PF-005" p) |> Option.map (str "status")
        [ if not (VerificationPlan.problems unauthorized |> hasProblem "unauthorized exclusion of P0 requirement PF-005") then yield "exclusion: an unauthorized exclusion is accepted"
          if excludedStatus (VerificationPlan.build unauthorized) <> Some "in-scope" then yield "exclusion: an unauthorized exclusion was applied"
          if not (VerificationPlan.problems wrongAuthority |> hasProblem "unauthorized exclusion") then yield "exclusion: a non-operator approval is accepted"
          if not (VerificationPlan.problems (withSpec i "exclusions" (excl "UI-999" "APR-TEST")) |> hasProblem "unknown or non-P0") then yield "exclusion: an exclusion of an unknown requirement is accepted"
          if not (VerificationPlan.problems authorized).IsEmpty then yield sprintf "exclusion: an authorized exclusion is rejected %A" (VerificationPlan.problems authorized)
          if excludedStatus (VerificationPlan.build authorized) <> Some "excluded" then yield "exclusion: an authorized exclusion is not recorded"
          if (arr "requirements" (VerificationPlan.build authorized)).Length <> 108 then yield "exclusion: an excluded requirement left the plan" ]

    determinism @ completeness @ stableIds @ missingTests @ preservation @ exclusions |> List.map (sprintf "verification plan: %s")

// ---- executable acceptance contracts (EII-C): negative controls over the plan and the checker ----------------

/// A synthetic candidate for the checker: ledger, evidence manifest and evidence files held in memory. Test ids
/// here are fixtures of this test, never bindings of the committed plan.
type private Candidate =
    { Plan: Json
      Ledger: Json
      Manifest: Json
      Evidence: Map<string, Json>
      Process: Map<string, Set<string>> }

let private candidateSha = "1111111111111111111111111111111111111111"
let private runId = "run-eii-c-1"

let private assessCandidate (i: VerificationPlan.Inputs) (approvalsRaw: byte[]) (c: Candidate) =
    let files = c.Evidence |> Map.map (fun _ j -> Text.Encoding.UTF8.GetBytes(serialize j))
    let manifestRaw = Text.Encoding.UTF8.GetBytes(serialize c.Manifest)
    PlanVerification.assess
        { Plan = c.Plan
          Baseline = i.Baseline
          BaselineSha256 = i.BaselineSha256
          Ledger = c.Ledger
          ManifestRaw = manifestRaw
          ExpectedManifestSha256 = Git.sha256Hex manifestRaw
          ReadEvidence = fun p -> files.TryFind p
          TrustedSourceSha = candidateSha
          ApprovalsRaw = approvalsRaw
          TrustedOperators = [ "kemiller2002" ]
          ProcessCovered = c.Process
          ReviewTagChanges = JNull }

/// Re-pins every manifest entry to the evidence as it now is (a producer that re-hashed what it wrote).
let private repinAll (c: Candidate) =
    let entries = c.Evidence |> Map.toList |> List.map (fun (p, j) -> JObj [ "path", JStr p; "sha256", JStr(Git.sha256Hex (Text.Encoding.UTF8.GetBytes(serialize j))) ])
    { c with Manifest = setIn "entries" (JArr entries) c.Manifest }

let private contractTests (source: string) : string list =
    let committedBaseline = File.ReadAllText(Path.Combine(source, "requirements", "causa-requirements-baseline.json"))
    let i0 = planInputs source committedBaseline
    let approvalsRaw =
        let a = field "approvals" i0.Spec |> Option.defaultValue JNull
        match Git.showFile source (str "commit" a) (str "path" a) with
        | Ok b -> b
        | Error e -> failwithf "approvals unreadable: %s" e
    let hasCode (code: string) (subject: string) (ds: string list) =
        ds |> List.exists (fun d -> d.StartsWith("[" + code + "] " + subject, StringComparison.Ordinal))
    let codesOf (a: Json) = arr "diagnostics" a |> List.map (str "code") |> List.distinct
    let entryOf rid (p: Json) = arr "requirements" p |> List.find (fun e -> str "requirement" e = rid)
    let obligationsOf rid (p: Json) = arr "obligations" (entryOf rid p)
    let contract (o: Json) = field "acceptanceContract" o |> Option.defaultValue JNull
    let pp (p: Json) = VerificationPlan.preservationProblems i0.Baseline p
    let plain = VerificationPlan.build i0
    let curated = arr "testBindings" i0.Spec |> List.map (str "obligation") |> Set.ofList

    // -- the plan carries a complete, derived acceptance contract on every obligation --------------------------
    let fields =
        let os = arr "requirements" plain |> List.collect (arr "obligations")
        let keys = [ "requirement"; "obligation"; "obligationId"; "priority"; "verificationMethod"; "requiredEvidenceCategories"; "categoryDerivation"; "testIdentity"; "dependencies"; "freshness"; "verificationStatus"; "missingCoverage" ]
        [ if os.Length <> 326 then yield sprintf "contract: expected 326 obligations, found %d" os.Length
          for o in os do
              let c = contract o
              for k in keys do
                  if (field k c).IsNone then yield sprintf "contract: %s lacks %s" (str "sourceObligation" o) k
              if strOpt "verificationStatus" c = Some "verified" then yield sprintf "contract: %s is verified in the plan" (str "sourceObligation" o)
              let expected = if curated.Contains(str "sourceObligation" o) then "bound" else "unbound"
              if field "testIdentity" c |> Option.bind (strOpt "status") <> Some expected then yield sprintf "contract: %s test identity is not explicitly %s" (str "sourceObligation" o) expected
              for c' in strs "requiredEvidenceCategories" c do
                  if not (List.contains c' EvidenceCategories.vocabulary) then yield sprintf "contract: %s has category %s outside the vocabulary" (str "sourceObligation" o) c'
              let derived = arr "categoryDerivation" c |> List.map (str "category")
              if derived <> strs "requiredEvidenceCategories" c then yield sprintf "contract: %s categories are not each derived by a declared rule" (str "sourceObligation" o)
          let ui011 = contract (obligationsOf "UI-011" plain).Head
          if strs "requiredEvidenceCategories" ui011 <> [ "browser"; "accessibility" ] then yield sprintf "contract: UI-011-O1 categories %A" (strs "requiredEvidenceCategories" ui011)
          let pf015 = contract (obligationsOf "PF-015" plain).Head
          if strs "requiredEvidenceCategories" pf015 <> [ "integration"; "live-provider"; "security" ] then yield sprintf "contract: PF-015-O1 categories %A" (strs "requiredEvidenceCategories" pf015)
          if field "freshness" pf015 |> Option.bind (strOpt "policy") <> Some "FP-LIVE-PER-COMMIT" then yield "contract: PF-015-O1 freshness is not live per commit"
          if not (pp plain).IsEmpty then yield sprintf "contract: the committed-input plan fails preservation %A" (pp plain |> List.truncate 3) ]

    // -- unclassified: an obligation no declared rule reaches is reported, never assigned ---------------------
    let unclassified =
        let table = i0.Categories.Table
        let stripped = mutateJsonList "rules" (List.filter (fun r -> not ((str "id" r).StartsWith("EC-UI-", StringComparison.Ordinal)))) table
        let p = VerificationPlan.build { i0 with Categories = { i0.Categories with Table = stripped } }
        let c = contract (obligationsOf "UI-001" p).Head
        let scopeInt k = field "scope" p |> Option.bind (intOpt k) |> Option.defaultValue -1L
        [ if strOpt "verificationStatus" c <> Some "unclassified" then yield "unclassified: UI-001-O1 is not unclassified when no rule reaches it"
          if arr "missingCoverage" c |> List.map (str "category") <> [ "unclassified" ] then yield "unclassified: missing coverage does not name 'unclassified'"
          if not (strs "requiredEvidenceCategories" c).IsEmpty then yield "unclassified: a category was assigned without a rule"
          if scopeInt "unclassifiedObligations" <= 0L then yield "unclassified: the scope does not count unclassified obligations" ]

    // -- compile-time binding and table checks ----------------------------------------------------------------
    let bindingChecks =
        let bind o (tests: Json list) = withSpec i0 "testBindings" (JArr [ JObj [ "obligation", JStr o; "tests", JArr tests ] ])
        let t id kind cats = JObj [ "id", JStr id; "kind", JStr kind; "categories", jstrs cats ]
        let hasProblem (needle: string) (ps: string list) = ps |> List.exists (fun p -> p.Contains(needle, StringComparison.Ordinal))
        let withTable f = { i0 with Categories = { i0.Categories with Table = f i0.Categories.Table } }
        let addRule r = mutateJsonList "rules" (fun rs -> rs @ [ r ])
        [ if not (VerificationPlan.problems (bind "PF-015-O1" [ JObj [ "id", JStr "x"; "kind", JStr "live" ] ]) |> hasProblem "declares no evidence category") then yield "binding: a test without categories is accepted"
          if not (VerificationPlan.problems (bind "PF-015-O1" [ t "x" "deterministic" [ "live-provider" ] ]) |> hasProblem "cannot provide live-provider") then yield "binding: a deterministic test claiming live-provider evidence is accepted"
          if not (VerificationPlan.problems (bind "PF-015-O1" [ t "x" "live" [ "magic" ] ]) |> hasProblem "unknown evidence category") then yield "binding: an unknown category is accepted"
          if not (VerificationPlan.problems (withTable (addRule (JObj [ "id", JStr "EC-X"; "category", JStr "magic"; "when", JObj [ "rules", jstrs [ "VR-UI" ] ]; "source", JStr "x" ]))) |> hasProblem "not a declared category") then yield "table: a rule with an undeclared category is accepted"
          if not (VerificationPlan.problems (withTable (addRule (JObj [ "id", JStr "EC-X"; "category", JStr "unit"; "when", JObj []; "source", JStr "x" ]))) |> hasProblem "states no condition") then yield "table: an unconditional rule is accepted"
          if not (VerificationPlan.problems (withTable (addRule (JObj [ "id", JStr "EC-X"; "category", JStr "unit"; "when", JObj [ "rules", jstrs [ "VR-NONE" ] ]; "source", JStr "x" ]))) |> hasProblem "unknown verification rule") then yield "table: a rule naming an unknown verification rule is accepted"
          if not (VerificationPlan.problems (withTable (addRule (JObj [ "id", JStr "EC-X"; "category", JStr "unit"; "when", JObj [ "rules", jstrs [ "VR-UI" ] ]; "source", JStr "" ]))) |> hasProblem "cites no source") then yield "table: a rule without a source is accepted" ]

    // -- negative controls over a re-sealed plan: each yields its own diagnostic code --------------------------
    let mapEntry rid (f: Json -> Json) (p: Json) = mapRequirements (List.map (fun e -> if str "requirement" e = rid then f e else e)) p
    let mapObligations (f: Json list -> Json list) (e: Json) = setIn "obligations" (JArr(f (arr "obligations" e))) e
    let mapObligation oid (f: Json -> Json) = mapObligations (List.map (fun o -> if str "sourceObligation" o = oid then f o else o))
    let swapField key (a: Json) (b: Json) = setIn key (field key b |> Option.get) a, setIn key (field key a |> Option.get) b
    let swapObligations keys (e: Json) =
        mapObligations (fun os ->
            match os with
            | a :: b :: rest ->
                let a', b' = keys |> List.fold (fun (x, y) k -> swapField k x y) (a, b)
                a' :: b' :: rest
            | os -> os) e

    let planControls =
        let expect name code subject (p: Json) =
            let ds = pp (reseal p)
            if hasCode code subject ds then [] else [ sprintf "control %s: expected [%s] %s, got %A" name code subject (ds |> List.truncate 4) ]
        let swapRequirementIds (p: Json) =
            mapRequirements (fun es ->
                es
                |> List.map (fun e ->
                    match str "requirement" e with
                    | "AR-001" -> setIn "requirement" (JStr "AR-002") e
                    | "AR-002" -> setIn "requirement" (JStr "AR-001") e
                    | _ -> e)
                |> List.sortBy (str "requirement")) p
        let ui016Swap (o: Json) =
            o
            |> setIn "text" (JStr "Ensure WCAG AA contrast for normal text (3:1), large text (4.5:1), and relevant non-text indicators (3:1).")
        let roleSwap (o: Json) =
            o |> setIn "text" (JStr((str "text" o).Replace("A reviewer distinct from the implementer", "A implementer distinct from the reviewer")))
        [ yield! expect "missing-obligation" "MISSING-OBLIGATION" "PF-005-O1" (mapEntry "PF-005" (mapObligations List.tail) plain)
          yield! expect "missing-requirement" "MISSING-REQUIREMENT" "PF-005" (mapRequirements (List.filter (fun e -> str "requirement" e <> "PF-005")) plain)
          yield! expect "altered-title" "ALTERED-VALUE" "PF-005" (mapEntry "PF-005" (setIn "title" (JStr "Provider convenience")) plain)
          yield! expect "altered-section-hash" "ALTERED-VALUE" "PF-005" (mapEntry "PF-005" (setIn "sectionSha256" (JStr(String('0', 64)))) plain)
          yield! expect "altered-priority" "ALTERED-VALUE" "PF-005" (mapEntry "PF-005" (setIn "priority" (JStr "P1")) plain)
          yield! expect "altered-kind" "ALTERED-VALUE" "PF-005-O1" (mapEntry "PF-005" (mapObligation "PF-005-O1" (setIn "kind" (JStr "guidance"))) plain)
          yield! expect "altered-value-tokens" "ALTERED-VALUE" "UI-011-O2" (mapEntry "UI-011" (mapObligation "UI-011-O2" (setIn "tokens" (jstrs [ "150%" ]))) plain)
          yield! expect "swapped-value-tokens" "ALTERED-VALUE" "UI-016-O11" (mapEntry "UI-016" (mapObligation "UI-016-O11" (setIn "tokens" (jstrs [ "3:1"; "4.5:1" ]))) plain)
          yield! expect "altered-text" "ALTERED-VALUE" "PF-005-O1" (mapEntry "PF-005" (mapObligation "PF-005-O1" (setIn "text" (JStr "UI and domain tests should avoid depending on GitHub."))) plain)
          yield! expect "value-swap-in-text" "ALTERED-VALUE" "UI-016-O11" (mapEntry "UI-016" (mapObligation "UI-016-O11" ui016Swap) plain)
          yield! expect "role-swap-in-text" "ALTERED-VALUE" "AR-008-O1" (mapEntry "AR-008" (mapObligation "AR-008-O1" roleSwap) plain)
          yield! expect "swapped-requirement-ids" "SWAPPED-REQUIREMENT-IDENTITY" "AR-001" (swapRequirementIds plain)
          yield! expect "swapped-requirement-ids" "SWAPPED-REQUIREMENT-IDENTITY" "AR-002" (swapRequirementIds plain)
          yield! expect "swapped-obligation-ids" "SWAPPED-OBLIGATION-IDENTITY" "AR-002-O1" (mapEntry "AR-002" (swapObligations [ "text"; "textSha256"; "kind"; "tokens"; "id" ]) plain)
          yield! expect "swapped-obligation-vpo-ids" "SWAPPED-OBLIGATION-IDENTITY" "AR-002-O1" (mapEntry "AR-002" (swapObligations [ "id" ]) plain)
          yield! expect "contract-categories-edited" "CONTRACT-INCONSISTENT" "UI-011-O1" (mapEntry "UI-011" (mapObligation "UI-011-O1" (fun o -> setIn "acceptanceContract" (setIn "requiredEvidenceCategories" (jstrs [ "browser" ]) (contract o)) o)) plain)
          yield! expect "contract-verified-forged" "CONTRACT-INCONSISTENT" "UI-011-O1" (mapEntry "UI-011" (mapObligation "UI-011-O1" (fun o -> setIn "acceptanceContract" (setIn "verificationStatus" (JStr "verified") (contract o)) o)) plain)
          // The swap controls must name the swap, not only a difference.
          let swapped = pp (reseal (mapEntry "AR-002" (swapObligations [ "text"; "textSha256"; "kind"; "tokens"; "id" ]) plain))
          if not (hasCode "SWAPPED-OBLIGATION-IDENTITY" "AR-002-O2" swapped) then yield "control swapped-obligation-ids: AR-002-O2 is not named as swapped" ]

    // -- the checker over a synthetic candidate ---------------------------------------------------------------
    let checker =
        let ver001 = obligationsOf "VER-001" plain |> List.map (str "sourceObligation")
        let ui011 = obligationsOf "UI-011" plain |> List.map (str "sourceObligation")
        let testId (o: string) = sprintf "fixture: %s proven (integration)" o
        let unitId (o: string) = sprintf "fixture: %s engine test (unit only)" o
        let binding o (tests: Json list) = JObj [ "obligation", JStr o; "tests", JArr tests ]
        let t id kind cats = JObj [ "id", JStr id; "kind", JStr kind; "categories", jstrs cats ]
        let spec =
            (ver001 |> List.map (fun o -> binding o [ t (testId o) "deterministic" [ "integration" ]; t "praxis:release-selection" "process" [ "process-record" ] ]))
            @ (ui011 |> List.map (fun o -> binding o [ t (unitId o) "deterministic" [ "unit" ] ]))
        let i = withSpec i0 "testBindings" (JArr spec)
        let plan = VerificationPlan.build i
        let vpo rid o = obligationsOf rid plan |> List.find (fun x -> str "sourceObligation" x = o) |> str "id"
        let evidenceFor rid (entries: (string * string list) list) =
            JObj
                [ "schema", JStr "causa.acceptance-evidence/1"
                  "run_id", JStr runId
                  "source_sha", JStr candidateSha
                  "subject_type", JStr "requirement"
                  "subject_id", JStr rid
                  "status", JStr "PASS"
                  "exit_code", JInt 0L
                  "obligations",
                  entries
                  |> List.map (fun (o, tests) ->
                      JObj [ "id", JStr(vpo rid o); "verification", JStr "PASS"; "tests", tests |> List.map (fun x -> JObj [ "testId", JStr x; "outcome", JStr "passed" ]) |> JArr ])
                  |> JArr ]
        let row rid status (tests: string list) (paths: string list) =
            JObj [ "id", JStr rid; "priority", JStr "P0"; "tests", jstrs tests; "evidence_paths", jstrs paths; "status", JStr status; "reason", JStr "" ]
        let rows =
            arr "requirements" plan
            |> List.map (fun e ->
                match str "requirement" e with
                | "VER-001" -> row "VER-001" "PASS" (ver001 |> List.map testId) [ "evidence/VER-001.json" ]
                | "UI-011" -> row "UI-011" "PASS" (ui011 |> List.map unitId) [ "evidence/UI-011.json" ]
                | rid -> row rid "NOT_RUN" [] [])
        let baseCandidate =
            repinAll
                { Plan = plan
                  Ledger =
                    JObj
                        [ "schema", JStr "causa.acceptance-ledger/2"
                          "run_id", JStr runId
                          "source_commit_sha", JStr candidateSha
                          "requirements_baseline", JObj [ "id", JStr i.BaselineId; "sha256", JStr i.BaselineSha256 ]
                          "scope_changes", JArr []
                          "requirements", JArr rows ]
                  Manifest = JObj [ "schema", JStr "causa.acceptance-evidence-manifest/1"; "run_id", JStr runId; "source_sha", JStr candidateSha; "entries", JArr [] ]
                  Evidence =
                    Map.ofList
                        [ "evidence/VER-001.json", evidenceFor "VER-001" (ver001 |> List.map (fun o -> o, [ testId o ]))
                          "evidence/UI-011.json", evidenceFor "UI-011" (ui011 |> List.map (fun o -> o, [ unitId o ])) ]
                  // verify-process found a qualifying release selection for the candidate (decided by ProcessVerification)
                  Process = ver001 |> List.map (fun o -> vpo "VER-001" o, set [ "process-record" ]) |> Map.ofList }
        let run c = assessCandidate i approvalsRaw c
        let statusOf rid (a: Json) = arr "requirements" a |> List.find (fun r -> str "requirement" r = rid) |> str "status"
        let obligationOf rid o (a: Json) = arr "requirements" a |> List.find (fun r -> str "requirement" r = rid) |> arr "obligations" |> List.find (fun x -> str "sourceObligation" x = o)
        let mapRow rid (f: Json -> Json option) (c: Candidate) = { c with Ledger = setIn "requirements" (JArr(arr "requirements" c.Ledger |> List.choose (fun r -> if str "id" r = rid then f r else Some r))) c.Ledger }
        let mapEvidence path (f: Json -> Json) (c: Candidate) = { c with Evidence = c.Evidence |> Map.map (fun p j -> if p = path then f j else j) }
        let expectCode name code (a: Json) = if List.contains code (codesOf a) then [] else [ sprintf "checker %s: expected diagnostic %s, got %A" name code (codesOf a) ]
        let notAccepted name (a: Json) = if str "verdict" a = "ACCEPTED" then [ sprintf "checker %s: candidate accepted" name ] else []

        let positive = run baseCandidate
        let related = positive
        let ledgerUnbound = run (mapRow "VER-001" (fun r -> Some(setIn "tests" (jstrs (strs "tests" r @ [ "fixture: unplanned test" ])) r)) baseCandidate)
        let boundAbsent = run (mapRow "VER-001" (fun r -> Some(setIn "tests" (jstrs (strs "tests" r |> List.tail)) r)) baseCandidate)
        let crossClaim =
            let ev = evidenceFor "VER-001" (ver001 |> List.mapi (fun k o -> o, [ testId (if k = 1 then ver001.Head else o) ]))
            run (repinAll (mapEvidence "evidence/VER-001.json" (fun _ -> ev) baseCandidate))
        let stale = run (repinAll (mapEvidence "evidence/VER-001.json" (setIn "source_sha" (JStr "2222222222222222222222222222222222222222")) baseCandidate))
        let unauthorized =
            let c = mapRow "UI-011" (fun _ -> None) baseCandidate
            run { c with Ledger = setIn "scope_changes" (JArr [ JObj [ "requirement_id", JStr "UI-011"; "action", JStr "exclude"; "approval_id", JStr "APR-INVENTED" ] ]) c.Ledger }
        let missingObligationPlan = run { baseCandidate with Plan = reseal (mapEntry "VER-001" (mapObligations List.tail) plan) }
        let swappedPlan = run { baseCandidate with Plan = reseal (mapEntry "AR-002" (swapObligations [ "id" ]) plan) }
        let noObligationEntries = run (repinAll (mapEvidence "evidence/VER-001.json" (setIn "obligations" (JArr [])) baseCandidate))

        [ // positive control: every obligation of VER-001 verified from tests bound to it
          if statusOf "VER-001" positive <> "PASS" then yield sprintf "checker positive: VER-001 is %s, expected PASS" (statusOf "VER-001" positive)
          for o in ver001 do
              if obligationOf "VER-001" o positive |> str "verificationStatus" <> "verified" then yield sprintf "checker positive: %s not verified" o
          if not (codesOf positive).IsEmpty then yield sprintf "checker positive: unexpected diagnostics %A" (codesOf positive)
          if str "verdict" positive <> "INCOMPLETE" then yield sprintf "checker positive: verdict %s, expected INCOMPLETE (other rows NOT_RUN)" (str "verdict" positive)
          // R5 5dcb91e: a passing unit test bound to a UI obligation does not verify it
          if statusOf "UI-011" related <> "UNVERIFIED" then yield sprintf "checker related-test: UI-011 is %s, expected UNVERIFIED" (statusOf "UI-011" related)
          for o in ui011 do
              let r = obligationOf "UI-011" o related
              if str "verificationStatus" r <> "unverified" then yield sprintf "checker related-test: %s is %s" o (str "verificationStatus" r)
              if arr "missingCoverage" r |> List.map (str "category") |> List.sort <> [ "accessibility"; "browser" ] then yield sprintf "checker related-test: %s missing coverage %A" o (arr "missingCoverage" r |> List.map (str "category"))
          // incorrect test mappings
          yield! expectCode "ledger-test-unbound" "TEST-MAPPING" ledgerUnbound
          if str "verdict" ledgerUnbound <> "INVALID" || statusOf "VER-001" ledgerUnbound <> "INVALID" then yield "checker ledger-test-unbound: not INVALID"
          yield! expectCode "bound-test-absent" "TEST-MAPPING" boundAbsent
          if str "verdict" boundAbsent <> "INVALID" then yield "checker bound-test-absent: not INVALID"
          yield! expectCode "evidence-cross-claim" "TEST-MAPPING" crossClaim
          if obligationOf "VER-001" ver001.[1] crossClaim |> str "verificationStatus" = "verified" then yield "checker evidence-cross-claim: an obligation was verified by a test bound to another"
          // stale evidence
          yield! expectCode "stale-evidence" "STALE-EVIDENCE" stale
          if statusOf "VER-001" stale <> "STALE" || str "verdict" stale <> "INVALID" then yield "checker stale-evidence: not STALE/INVALID"
          if obligationOf "VER-001" ver001.Head stale |> arr "missingCoverage" |> List.map (str "reason") <> [ "evidence-not-fresh" ] then yield "checker stale-evidence: missing coverage is not evidence-not-fresh"
          // unauthorized exclusion
          yield! expectCode "unauthorized-exclusion" "UNAUTHORIZED-EXCLUSION" unauthorized
          if str "verdict" unauthorized <> "INVALID" then yield "checker unauthorized-exclusion: not INVALID"
          // plan tampering seen by the checker
          yield! expectCode "missing-obligation" "MISSING-OBLIGATION" missingObligationPlan
          yield! expectCode "swapped-obligation" "SWAPPED-OBLIGATION-IDENTITY" swappedPlan
          // requirement-level PASS without per-obligation results verifies nothing
          if statusOf "VER-001" noObligationEntries <> "UNVERIFIED" then yield sprintf "checker no-obligation-entries: VER-001 is %s, expected UNVERIFIED" (statusOf "VER-001" noObligationEntries)
          for (name, a) in [ "ledger-test-unbound", ledgerUnbound; "bound-test-absent", boundAbsent; "evidence-cross-claim", crossClaim; "stale-evidence", stale; "unauthorized-exclusion", unauthorized; "missing-obligation", missingObligationPlan; "swapped-obligation", swappedPlan ] do
              yield! notAccepted name a ]

    fields @ unclassified @ bindingChecks @ planControls @ checker |> List.map (sprintf "acceptance contract: %s")

// ---- offline tests -----------------------------------------------------------------------------

/// The plan compiled from the committed curation with other testBindings (for proposals that are not approved).
let private planWithBindings (source: string) (bindings: Json) =
    let committed = File.ReadAllText(Path.Combine(source, "requirements", "causa-requirements-baseline.json"))
    VerificationPlan.build (withSpec (planInputs source committed) "testBindings" bindings)

/// Tooling is F# only (owner decision 2026-11-01, docs/OWNER-DECISIONS-2026-11-01.md): a tracked script in another
/// language under tools/ or .github/ fails, except the tools still to be ported, listed here until each is.
let private pendingPorts =
    set [ "tools/compute-acceptance-verdict.py"; "tools/validate-acceptance-ledger.py"; "tools/test-acceptance-ledger.py"
          "tools/test-acceptance-verdict.py"; "tools/process-evidence/simulate-rehearsal.sh" ]

let private scriptExtensions =
    [ ".py"; ".pyc"; ".sh"; ".bash"; ".zsh"; ".ps1"; ".psm1"; ".rb"; ".pl"; ".js"; ".mjs"; ".cjs"; ".ts"; ".lua"; ".go"; ".rs"; ".java"; ".kt"; ".cs"; ".vb" ]

let nonFSharpTooling (files: string list) : string list =
    files
    |> List.filter (fun f -> f.StartsWith("tools/", StringComparison.Ordinal) || f.StartsWith(".github/", StringComparison.Ordinal))
    |> List.filter (fun f -> scriptExtensions |> List.exists (fun e -> f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
    |> List.filter (pendingPorts.Contains >> not)
    |> List.map (sprintf "tooling: %s is not F# (tooling is F# only, docs/OWNER-DECISIONS-2026-11-01.md)")

let toolingLanguage (source: string) : string list =
    match Git.run source [ "ls-files" ] with
    | Error e -> [ sprintf "tooling: git ls-files failed: %s" e ]
    | Ok bytes ->
        let tracked = (Text.Encoding.UTF8.GetString bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
        nonFSharpTooling tracked
        @ [ // Control: a new Python or shell tool is rejected; F# and files outside tools/ and .github/ are not.
            if nonFSharpTooling [ "tools/new-tool.py"; "tools/x/run.sh"; "tools/ok.fsx"; "docs/a.py" ] |> List.length <> 2 then
                yield "tooling control: a Python or shell tool under tools/ was not rejected" ]

let private processSimulation (source: string) = ProcessTests.simulation source (planWithBindings source)

let private r6Readiness (source: string) =
    let committed = File.ReadAllText(Path.Combine(source, "requirements", "causa-requirements-baseline.json"))
    let i = planInputs source committed
    R6ReadinessTests.all source (planWithBindings source) (fun bindings -> VerificationPlan.problems (withSpec i "testBindings" bindings))

let private producerBindings (source: string) =
    let committed = File.ReadAllText(Path.Combine(source, "requirements", "causa-requirements-baseline.json"))
    let i = planInputs source committed
    ProcessTests.producerBindingControls (fun bindings -> VerificationPlan.problems (withSpec i "testBindings" bindings))

let private offline (source: string) : string list =
    let committedPath = Path.Combine(source, "requirements", "causa-requirements-baseline.json")
    let committed = File.ReadAllText committedPath
    let pinned = parseBytes (Text.Encoding.UTF8.GetBytes committed) |> field "source" |> Option.map (str "commit") |> Option.defaultValue ""

    let equivalence =
        try
            let rebuilt = buildBaselineAt source pinned
            [ if rebuilt <> committed then yield sprintf "baseline port: output at %s differs from the committed Python-generated baseline" pinned ]
        with ex -> [ "baseline port: " + ex.Message ]

    // Rehearsal 7 (ADR-R7-001): the committed r7 baseline is the port's output at the commit it pins, so the
    // persistence document is in it and every earlier document is read at that same commit.
    let r7 =
        let text = File.ReadAllText(Path.Combine(source, "requirements", "causa-requirements-baseline-r7.json"))
        let commit = parseBytes (Text.Encoding.UTF8.GetBytes text) |> field "source" |> Option.map (str "commit") |> Option.defaultValue ""
        try
            [ if buildBaselineAt source commit <> text then
                  yield sprintf "r7 baseline: the port's output at %s differs from requirements/causa-requirements-baseline-r7.json" commit ]
        with ex -> [ "r7 baseline: " + ex.Message ]

    // Cases ported from tools/test-requirements-baseline.py (removed 2026-11-01, when the F# generator became authoritative).
    let units =
        let lines =
            [| "## Architectural contract"; ""; "**PF-001 [P0] One.** Define a port. It must not leak."; ""
               "**PF-002 [P0] Two.** Levels:"; "1. Domain."; ""; "Each level must have a command."; ""; "## Fixture catalog" |]
        let regex = Regex(@"^\*\*(PF-\d{3}) ")
        let table = [ "## UI-016: Colors [P0]"; "| Token role | Hex |"; "| --- | --- |"; "| Navy | `#14283F` |" ]
        let tableUnits = Baseline.units table true ""

        [ if Baseline.section lines 2 regex <> [ lines.[2] ] then yield "a paragraph anchor ends at the next requirement line"
          if Baseline.section lines 4 regex <> List.ofArray lines.[4..7] then yield "a paragraph anchor runs to the next heading, blank tail removed"
          if Baseline.units (Baseline.section lines 2 regex) false "**PF-001 [P0] One.** " <> [ "Define a port."; "It must not leak." ] then yield "sentences are verbatim units"
          if not (List.contains "| Navy | `#14283F` |" tableUnits) || List.contains "| --- | --- |" tableUnits then yield "table data rows are units, separators are not"
          if Baseline.tokensOf "contrast 4.5:1 at 200% within 60 minutes, HTTP 422, #14283F, v1.0.0, five frozen" <> [ "#14283F"; "4.5:1"; "200%"; "60 minutes"; "v1.0.0"; "422"; "five" ] then yield "concrete values become tokens"
          if Baseline.sha256 "a\r\nb" <> Baseline.sha256 "a\nb" then yield "hashes normalize line endings" ]
        |> List.map (sprintf "baseline unit: %s")

    let verdicts =
        let k id = JObj [ "id", JStr id ]
        let o s : Preflight.Outcome = { Status = s; Detail = "" }
        [ if Preflight.verdict [ k "A", o "PASS"; k "PRE-ORG-01", o "NOT_RUN" ] <> "READY" then yield "event-only NOT_RUN still READY"
          if Preflight.verdict [ k "A", o "PASS"; k "B", o "NOT_RUN" ] <> "BLOCKED" then yield "NOT_RUN blocks"
          if Preflight.verdict [ k "A", o "BLOCKED"; k "B", o "FAIL" ] <> "FAIL" then yield "FAIL dominates" ]
        |> List.map (sprintf "preflight verdict: %s")

    let plan = try planTests source with ex -> [ "verification plan: " + ex.Message ]
    let contracts = try contractTests source with ex -> [ "acceptance contract: " + ex.Message ]

    let closures = try DefectClosureTests.all () with ex -> [ "defect closure: " + ex.Message ]

    let releases = try ReleaseComparisonTests.all () with ex -> [ "release comparison: " + ex.Message ]

    let ratchet = try AssertionRatchetTests.all () with ex -> [ "assertion ratchet: " + ex.Message ]

    equivalence @ r7 @ units @ verdicts @ plan @ contracts @ closures @ releases @ ratchet

[<EntryPoint>]
let main argv =
    let report (failures: string list) label =
        failures |> List.iter (printfn "FAIL %s")
        printfn "%s: %s" label (if failures.IsEmpty then "all passed" else sprintf "%d failure(s)" failures.Length)
        if failures.IsEmpty then 0 else 1

    match List.ofArray argv with
    | "--replay" :: rest ->
        let rec go (xs: string list) (o: Replay.Options) =
            match xs with
            | "--source" :: v :: r -> go r { o with Source = v }
            | "--evidence-root" :: v :: r -> go r { o with EvidenceRoot = v }
            | "--candidate-repo" :: v :: r -> go r { o with CandidateRepo = v }
            | "--trusted-source-sha" :: v :: r -> go r { o with TrustedSourceSha = v }
            | "--out" :: v :: r -> go r { o with Out = v }
            | [] -> Some o
            | _ -> None
        match go rest { Source = "."; EvidenceRoot = ""; CandidateRepo = ""; TrustedSourceSha = ""; Out = "" } with
        | Some o when o.EvidenceRoot <> "" && o.CandidateRepo <> "" && o.TrustedSourceSha <> "" && o.Out <> "" -> Replay.run o
        | _ ->
            eprintfn "usage: --replay --source DIR --evidence-root DIR --candidate-repo DIR --trusted-source-sha SHA --out FILE"
            2
    | "--rehearsal-baselines" :: rest ->
        let rec go (xs: string list) (o: RehearsalBaselines.Options) =
            match xs with
            | "--source" :: v :: r -> go r { o with Source = v }
            | "--r4" :: v :: r -> go r { o with R4 = v }
            | "--r5" :: v :: r -> go r { o with R5 = v }
            | "--out" :: v :: r -> go r { o with Out = v }
            | [] -> Some o
            | _ -> None
        match go rest { Source = "."; R4 = ""; R5 = ""; Out = "" } with
        | Some o when o.R4 <> "" && o.R5 <> "" && o.Out <> "" ->
            let committed = File.ReadAllText(Path.Combine(o.Source, "requirements", "causa-requirements-baseline.json"))
            let template = planInputs o.Source committed
            let build (baselineText: string) =
                let bytes = Text.Encoding.UTF8.GetBytes baselineText
                let baseline = parseBytes bytes
                let src = field "source" baseline |> Option.defaultValue JNull
                VerificationPlan.build
                    { template with
                        Repository = str "repository" src
                        Commit = str "commit" src
                        BaselineId = str "id" baseline
                        BaselineSha256 = Git.sha256Hex bytes
                        Baseline = baseline }
            RehearsalBaselines.run o build
        | _ ->
            eprintfn "usage: --rehearsal-baselines --source DIR --r4 DIR --r5 DIR --out FILE"
            2
    | "--offline" :: rest ->
        // --record FILE writes the compiler test record the v4 freeze gate reads (FG-08): the result and the digest of
        // the compiler tree it ran on, so a record from another tree is never accepted.
        let record, rest' =
            match rest |> List.tryFindIndex ((=) "--record") with
            | Some n when n + 1 < rest.Length -> Some rest.[n + 1], (rest |> List.removeAt n |> List.removeAt n)
            | _ -> None, rest
        match Program.parseArgs rest' Program.defaults with
        | Error e ->
            eprintfn "%s" e
            2
        | Ok o ->
            let processFailures = snd (processSimulation o.Source) @ producerBindings o.Source @ r6Readiness o.Source @ R6ScopeTests.all o.Source @ V4Tests.all o.Source @ V41Tests.all o.Source @ V411Tests.all o.Source
            let failures = offline o.Source @ processFailures @ TokenEconomicsTests.all o.Source @ toolingLanguage o.Source
            let label = "offline tests (baseline equivalence, baseline units, preflight verdict, verification plan, acceptance contracts, process evidence, v4 scope and controls, v4.1 and v4.1.1 scope and controls, token economics, F#-only tooling)"
            let code = report failures label
            record
            |> Option.iter (fun path ->
                let tree = V4Package.treeSha256 (Program.compilerSources (Path.Combine(o.Source, "tools", "requirements-compiler")))
                JObj
                    [ "schema", JStr "indy-init.v4-compiler-test-record/1"
                      "result", JStr(if failures.IsEmpty then "PASS" else "FAIL")
                      "failures", jint failures.Length
                      "summary", JStr(sprintf "%s: %s" label (if failures.IsEmpty then "all passed" else sprintf "%d failure(s)" failures.Length))
                      "compilerTreeSha256", JStr tree
                      "command", JStr "dotnet run -c Release --project tools/requirements-compiler/tests -- --offline --source ." ]
                |> serialize
                |> fun t -> File.WriteAllText(path, t))
            code
    | "--readiness-replay" :: rest ->
        match rest with
        | [ "--source"; source; "--r5"; r5; "--out"; out ] ->
            let evidence, failures = ProcessTests.readinessReplay source r5 (planWithBindings source)
            File.WriteAllText(out, serialize evidence)
            report failures "readiness replay"
        | _ ->
            eprintfn "usage: --readiness-replay --source DIR --r5 DIR --out FILE"
            2
    | "--process-simulation" :: rest ->
        match rest with
        | [ "--source"; source; "--out"; out ] ->
            let evidence, failures = processSimulation source
            File.WriteAllText(out, serialize evidence)
            report failures "process evidence simulation"
        | _ ->
            eprintfn "usage: --process-simulation --source DIR --out FILE"
            2
    | args ->
        match Program.parseArgs args Program.defaults with
        | Error e ->
            eprintfn "%s" e
            2
        | Ok o ->
            let caseFailures = cases |> List.collect (runCase o)

            let determinism =
                let a = Path.Combine(Path.GetTempPath(), "rc-det-a")
                let b = Path.Combine(Path.GetTempPath(), "rc-det-b")
                [ a; b ] |> List.iter (fun d -> if Directory.Exists d then Directory.Delete(d, true))
                quiet (fun () -> Program.compile { o with Out = a; Check = false }) |> ignore
                quiet (fun () -> Program.compile { o with Out = b; Check = false }) |> ignore
                let files (d: string) = Directory.GetFiles d |> Array.map Path.GetFileName |> Array.sort
                [ if files a <> files b then yield "determinism: different file sets"
                  for f in files a do
                      if File.ReadAllBytes(Path.Combine(a, f)) <> File.ReadAllBytes(Path.Combine(b, f)) then yield sprintf "determinism: %s differs between runs" f ]

            let committed =
                match quiet (fun () -> Program.compile { o with Check = true }) with
                | 0 -> []
                | code -> [ sprintf "committed package: --check exited %d" code ]

            report (offline o.Source @ caseFailures @ determinism @ committed) (sprintf "offline tests, %d negative/positive cases, determinism and committed-package check" cases.Length)
