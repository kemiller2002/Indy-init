/// Negative controls for requirements-compiler. Each case copies the curation and baseline into a
/// temporary directory, injects one defect, compiles, and asserts that the named check fails and the
/// compiler exits 3. Positive controls assert PASS, byte-identical reruns and a clean --check.
///
///   dotnet run --project tools/requirements-compiler/tests -- <the compiler's own arguments>
module Tests

open System
open System.IO
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

/// Structural mutation: replaces one field of the item with the given id in a curation list.
let private setField (path: string) (listKey: string) (id: string) (key: string) (value: Json) =
    let replaceIn =
        function
        | JObj props when strOpt "id" (JObj props) = Some id ->
            if not (props |> List.exists (fun (k, _) -> k = key)) then failwithf "%s has no field %s" id key
            JObj(props |> List.map (fun (k, v) -> if k = key then k, value else k, v))
        | j -> j

    match parseFile path with
    | JObj top ->
        let items = arr listKey (JObj top)
        if not (items |> List.exists (fun j -> strOpt "id" j = Some id)) then failwithf "mutation target %s not found in %s" id path
        let updated = top |> List.map (fun (k, v) -> if k = listKey then k, JArr(items |> List.map replaceIn) else k, v)
        File.WriteAllText(path, serialize (JObj updated))
    | _ -> failwithf "%s is not a JSON object" path

let private quiet (f: unit -> 'a) =
    let out = Console.Out
    Console.SetOut TextWriter.Null
    try f () finally Console.SetOut out

type Case =
    { Name: string
      Mutate: string -> string -> unit
      ExpectExit: int
      ExpectFailing: string list }

let private failingChecks (outDir: string) =
    let report = Path.Combine(outDir, "requirements-validation-report.json")
    if File.Exists report then
        parseFile report |> arr "checks" |> List.filter (fun c -> str "status" c = "FAIL") |> List.map (str "id")
    else []

let private runCase (o: Program.Options) (c: Case) : string list =
    let tmp = Path.Combine(Path.GetTempPath(), "rc-test-" + c.Name)
    if Directory.Exists tmp then Directory.Delete(tmp, true)
    let curation = Path.Combine(tmp, "curation")
    copyDir o.Curation curation
    let baseline = Path.Combine(tmp, "baseline.json")
    File.Copy(o.Baseline, baseline)
    let result =
        try
            c.Mutate curation baseline
            let out = Path.Combine(tmp, "out")
            let code = quiet (fun () -> Program.compile { o with Curation = curation; Baseline = baseline; Out = out; Check = false })
            let failing = failingChecks out
            [ if code <> c.ExpectExit then yield sprintf "%s: exit %d, expected %d" c.Name code c.ExpectExit
              for id in c.ExpectFailing do
                  if not (List.contains id failing) then yield sprintf "%s: expected %s to fail; failing were [%s]" c.Name id (String.concat ", " failing)
              if c.ExpectFailing.IsEmpty && not failing.IsEmpty then yield sprintf "%s: unexpected failing checks [%s]" c.Name (String.concat ", " failing) ]
        with ex -> [ sprintf "%s: %s" c.Name ex.Message ]
    Directory.Delete(tmp, true)
    result

let private cases: Case list =
    let cur (f: string) (dir: string) = Path.Combine(dir, f)

    [ { Name = "positive-control"; Mutate = (fun _ _ -> ()); ExpectExit = 0; ExpectFailing = [] }
      { Name = "citation-not-verbatim"
        Mutate = fun d _ -> edit (cur "findings.json" d) "On launch, show all five frozen demonstration investigations" "On launch, show all six frozen demonstration investigations"
        ExpectExit = 3; ExpectFailing = [ "V11" ] }
      { Name = "dependency-cycle"
        Mutate = fun d _ -> edit (cur "tasks.json" d) "\"id\": \"T00\", \"title\": \"Owner decisions on the readiness blockers\", \"owner\": \"operator\", \"kind\": \"precondition\", \"satisfiedBy\": \"operator-action\",\n      \"covers\": [\"UI-001\", \"UI-015\", \"EXT-DATA-002\", \"AR-024\", \"AR-003\", \"UI-037\"],\n      \"dependsOn\": []" "\"id\": \"T00\", \"title\": \"Owner decisions on the readiness blockers\", \"owner\": \"operator\", \"kind\": \"precondition\", \"satisfiedBy\": \"operator-action\",\n      \"covers\": [\"UI-001\", \"UI-015\", \"EXT-DATA-002\", \"AR-024\", \"AR-003\", \"UI-037\"],\n      \"dependsOn\": [ { \"task\": \"T34\", \"reason\": \"injected cycle\" } ]"
        ExpectExit = 3; ExpectFailing = [ "V05"; "V09" ] }
      { Name = "requirement-out-of-scope"
        Mutate = fun d _ -> edit (cur "tasks.json" d) "\"covers\": [\"origin:external\"]" "\"covers\": [\"UI-001\"]"
        ExpectExit = 3; ExpectFailing = [ "V09" ] }
      { Name = "fixed-decision-unsourced"
        Mutate = fun d _ -> setField (cur "decisions.json" d) "decisions" "DEC-020" "sources" (JArr [])
        ExpectExit = 3; ExpectFailing = [ "V08" ] }
      { Name = "contract-over-claimed"
        Mutate = fun d _ -> edit (cur "contracts.json" d) "\"status\": \"unverified\", \"requirements\": [\"WASM-LOAD-001\"" "\"status\": \"verified\", \"requirements\": [\"WASM-LOAD-001\""
        ExpectExit = 3; ExpectFailing = [ "V07" ] }
      { Name = "identity-mismatch"
        Mutate = fun d _ -> edit (cur "contracts.json" d) "\"expectedSha256\": \"8c361929cfb429c31c059c92d6148036bf2b5861f81dace635a1f7de18dbf87c\"" "\"expectedSha256\": \"0000000000000000000000000000000000000000000000000000000000000000\""
        ExpectExit = 3; ExpectFailing = [ "V07" ] }
      { Name = "conflict-silently-accepted"
        Mutate = fun d _ -> edit (cur "findings.json" d) "\"DEC-024\",\n        \"DEC-007\"" "\"DEC-024\",\n        \"DEC-007\",\n        \"DEC-001\""
        ExpectExit = 3; ExpectFailing = [ "V03" ] }
      { Name = "conflict-resolved-without-source"
        Mutate = fun d _ -> edit (cur "findings.json" d) "\"id\": \"F-007\",\n      \"type\": \"conflict\",\n      \"severity\": \"major\",\n      \"material\": false,\n      \"status\": \"unresolved\"" "\"id\": \"F-007\",\n      \"type\": \"conflict\",\n      \"severity\": \"major\",\n      \"material\": false,\n      \"status\": \"resolved-by-precedence\""
        ExpectExit = 3; ExpectFailing = [ "V03" ] }
      { Name = "unknown-reference"
        Mutate = fun d _ -> setField (cur "decisions.json" d) "decisions" "DEC-020" "requirements" (jstrs [ "R9.1"; "UI-999" ])
        ExpectExit = 3; ExpectFailing = [ "V04" ] }
      { Name = "repository-revision-mismatch"
        Mutate = fun d _ -> edit (cur "sources.json" d) "\"commit\": \"22260f429501d0d6963fc33302ed1e64d742a374\"" "\"commit\": \"1111111111111111111111111111111111111111\""
        ExpectExit = 2; ExpectFailing = [] }
      { Name = "undefined-metric"
        Mutate = fun d _ -> edit (cur "experiment.json" d) "\"perceptaMetric\": \"tool.calls\"" "\"perceptaMetric\": \"tool.invented_calls\""
        ExpectExit = 3; ExpectFailing = [ "V13" ] }
      { Name = "verification-rule-removed"
        Mutate =
            fun d _ ->
                edit (cur "verification-rules.json" d) "\"match\": [\"all\"]" "\"match\": [\"origin:baseline\"]"
                edit (cur "verification-rules.json" d) "\"match\": [\"origin:external\"]" "\"match\": [\"origin:none\"]"
        ExpectExit = 3; ExpectFailing = [ "V06" ] }
      { Name = "baseline-obligation-tampered"
        Mutate = fun _ b -> edit b "Use these initial canonical palette values:" "Use these optional palette values:"
        ExpectExit = 3; ExpectFailing = [ "V01" ] } ]

[<EntryPoint>]
let main argv =
    match Program.parseArgs (List.ofArray argv) Program.defaults with
    | Error e ->
        eprintfn "%s" e
        2
    | Ok o ->
        let caseFailures = cases |> List.collect (runCase o)

        // Determinism: two compilations of the unmodified inputs are byte-identical.
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

        // The committed package is exactly what the compiler produces from the committed inputs.
        let committed =
            match quiet (fun () -> Program.compile { o with Check = true }) with
            | 0 -> []
            | code -> [ sprintf "committed package: --check exited %d" code ]

        let all = caseFailures @ determinism @ committed
        all |> List.iter (printfn "FAIL %s")
        printfn "%d cases, determinism and committed-package check: %s" cases.Length (if all.IsEmpty then "all passed" else sprintf "%d failure(s)" all.Length)
        if all.IsEmpty then 0 else 1
