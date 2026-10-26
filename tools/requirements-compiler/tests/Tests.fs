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
      case "conflict-silently-accepted" (fun d -> setField (cur "findings.json" d) "findings" "F-005" "decisions" (jstrs [ "DEC-013"; "DEC-001" ])) 3 [ "V03" ]
      case "conflict-resolved-without-source" (fun d -> setField (cur "findings.json" d) "findings" "F-007" "status" (JStr "resolved-by-precedence")) 3 [ "V03" ]
      case "unknown-reference" (fun d -> setField (cur "decisions.json" d) "decisions" "DEC-020" "requirements" (jstrs [ "R9.1"; "UI-999" ])) 3 [ "V04" ]
      case "repository-revision-mismatch" (fun d -> edit (cur "sources.json" d) "\"commit\": \"22260f429501d0d6963fc33302ed1e64d742a374\"" "\"commit\": \"1111111111111111111111111111111111111111\"") 2 []
      case "undefined-metric" (fun d -> edit (cur "experiment.json" d) "\"perceptaMetric\": \"tool.calls\"" "\"perceptaMetric\": \"tool.invented_calls\"") 3 [ "V13" ]
      case "verification-rule-removed" (fun d ->
          edit (cur "verification-rules.json" d) "\"match\": [\"all\"]" "\"match\": [\"origin:baseline\"]"
          edit (cur "verification-rules.json" d) "\"match\": [\"origin:external\"]" "\"match\": [\"origin:none\"]") 3 [ "V06" ]
      case "blocker-unclassified" (fun d -> setField (cur "findings.json" d) "findings" "F-005" "blockerClass" (JStr "")) 3 [ "V15" ]
      case "decision-without-recommendation" (fun d ->
          setField (cur "decisions.json" d) "decisions" "DEC-022" "classification" (JStr "unresolved")
          setField (cur "decisions.json" d) "decisions" "DEC-022" "recommendation" (JStr "")) 3 [ "V15" ]
      case "defect-class-test-missing" (fun d -> mutateList (cur "defect-class-tests.json" d) "tests" (List.filter (fun t -> str "defectClass" t <> "DC-006"))) 3 [ "V16" ]
      case "preflight-depends-on-timed" (fun d -> setField (cur "tasks.json" d) "tasks" "T05" "dependsOn" (JArr [ dep "T12" "injected" ])) 3 [ "V16" ]
      case "exclusion-by-undecided-decision" (fun d -> setField (cur "decisions.json" d) "decisions" "DEC-022" "classification" (JStr "unresolved")) 3 [ "V16" ]
      { case "baseline-obligation-tampered" ignore 3 [ "V01" ] with
          SuppliedBaseline = Some(fun text -> text.Replace("Use these initial canonical palette values:", "Use these optional palette values:")) } ]

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

    equivalence @ units @ verdicts

[<EntryPoint>]
let main argv =
    let report (failures: string list) label =
        failures |> List.iter (printfn "FAIL %s")
        printfn "%s: %s" label (if failures.IsEmpty then "all passed" else sprintf "%d failure(s)" failures.Length)
        if failures.IsEmpty then 0 else 1

    match List.ofArray argv with
    | "--offline" :: rest ->
        match Program.parseArgs rest Program.defaults with
        | Error e ->
            eprintfn "%s" e
            2
        | Ok o -> report (offline o.Source) "offline tests (baseline equivalence, baseline units, preflight verdict)"
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
