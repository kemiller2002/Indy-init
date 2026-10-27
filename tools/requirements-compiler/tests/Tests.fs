/// Tests for requirements-compiler.
///
/// Offline (indy-init history only; run in CI):
///   dotnet run --project tools/requirements-compiler/tests -- --offline --source .
///     - the F# baseline port reproduces the committed Python-generated baseline byte for byte;
///     - unit cases ported from tools/test-requirements-baseline.py;
///     - the preflight verdict rule.
///
/// Full (needs the checkouts named in curation/sources.json):
///   dotnet run --project tools/requirements-compiler/tests -- <the compiler's own arguments>
///     - offline tests, plus negative controls: each case copies the curation into a temporary directory,
///       injects one defect, compiles, and asserts the named check fails and the compiler exits as expected;
///     - determinism (two compilations are byte-identical) and `--check` against the committed package.
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
      { case "baseline-obligation-tampered" ignore 3 [ "V01" ] with
          SuppliedBaseline = Some(fun text -> text.Replace("Use these initial canonical palette values:", "Use these optional palette values:")) } ]

// ---- verification plan (offline: committed baseline, committed verification manifest, git history) ----

let private r4BaselineCommit = "aab8f80c7743392e794b3b631542903ed4ed6f64"
let private r4BaselineSha256 = "621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9"

/// Plan inputs from committed files only, so CI can build the plan without the other checkouts.
let planInputs (source: string) (baselineText: string) : VerificationPlan.Inputs =
    let v1 = Path.Combine(source, "requirements-compiled", "v1")
    let spec = parseFile (Path.Combine(v1, "curation", "verification-plan.json"))
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
      Manifest = parseFile (Path.Combine(v1, "verification-manifest.json"))
      Spec = spec
      Approvals =
        { Repository = "kemiller2002/indy-init"
          Commit = str "commit" a
          Path = str "path" a
          Sha256 = Git.sha256Hex approvals
          TrustedOperators = strs "trustedOperators" a
          Document = parseBytes approvals } }

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
        let committedPlan = Path.Combine(source, "requirements-compiled", "v1", "verification-plan.json")
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
        let bound = withSpec i "testBindings" (JArr [ JObj [ "obligation", JStr "AR-001-O2"; "tests", JArr [ JObj [ "id", JStr "t"; "kind", JStr "deterministic" ] ] ] ])
        let ids (p: Json) = arr "requirements" p |> List.collect (arr "obligations") |> List.map (str "id")
        [ if VerificationPlan.obligationId "AR-001" "AR-001-O1" text1 <> "VPO-a5795f29e0de763c" then yield "stable ids: AR-001-O1 id changed"
          if str "id" (obligation "AR-001-O1" (entryOf "AR-001" plan)) <> "VPO-a5795f29e0de763c" then yield "stable ids: plan AR-001-O1 id changed"
          if ids (VerificationPlan.build bound) <> ids plan then yield "stable ids: a test binding changed obligation ids"
          if VerificationPlan.obligationId "AR-001" "AR-001-O1" (text1 + " ") = "VPO-a5795f29e0de763c" then yield "stable ids: a text change kept the id"
          if (ids plan |> List.distinct).Length <> (ids plan).Length then yield "stable ids: obligation ids collide" ]

    // Missing-test detection: unbound obligations are 'missing'; a binding makes exactly that one 'bound'.
    let missingTests =
        let binding o tests = JObj [ "obligation", JStr o; "tests", JArr tests ]
        let t id kind = JObj [ "id", JStr id; "kind", JStr kind ]
        let one = withSpec i "testBindings" (JArr [ binding "AR-001-O1" [ t "core: AR-001 pins the baseline" "deterministic" ] ])
        let p1 = VerificationPlan.build one
        let liveOne = VerificationPlan.build (withSpec i "testBindings" (JArr [ binding "AR-001-O1" [ t "live: AR-001" "live" ] ]))
        [ if scopeInt "missingTestObligations" plan <> scopeInt "obligations" plan || scopeInt "boundObligations" plan <> 0L then yield "missing tests: unbound plan must report every obligation missing"
          if arr "requirements" plan |> List.exists (fun e -> tiStatus e <> "missing") then yield "missing tests: a requirement without bindings is not 'missing'"
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
          if not (pp (reseal (mapRequirements (List.map tamperObligation) plan)) |> hasProblem "obligations differ") then yield "preservation: an altered obligation is not detected"
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

// ---- offline tests -----------------------------------------------------------------------------

let private offline (source: string) : string list =
    let committedPath = Path.Combine(source, "requirements", "causa-requirements-baseline.json")
    let committed = File.ReadAllText committedPath
    let pinned = parseBytes (Text.Encoding.UTF8.GetBytes committed) |> field "source" |> Option.map (str "commit") |> Option.defaultValue ""

    let equivalence =
        try
            let rebuilt = buildBaselineAt source pinned
            [ if rebuilt <> committed then yield sprintf "baseline port: output at %s differs from the committed Python-generated baseline" pinned ]
        with ex -> [ "baseline port: " + ex.Message ]

    // Cases ported from tools/test-requirements-baseline.py.
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

    equivalence @ units @ verdicts @ plan

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
    | "--offline" :: rest ->
        match Program.parseArgs rest Program.defaults with
        | Error e ->
            eprintfn "%s" e
            2
        | Ok o -> report (offline o.Source) "offline tests (baseline equivalence, baseline units, preflight verdict, verification plan)"
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
