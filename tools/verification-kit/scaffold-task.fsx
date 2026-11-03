// scaffold-task.fsx: writes Dokimos verification skeletons into the test files of ONE task, and nothing else.
//
// Released Dokimos 0.10.0 `verification scaffold --write true` writes every work package at once, and the T0 bootstrap
// creates every planned test file as an empty namespace (so without --append nothing is written; with it every file
// would fill with failing skeletons). This script runs the scaffold on a throwaway copy of the working tree and copies
// back only the files you name. A named file still as the bootstrap wrote it (comments and a namespace line) is
// written afresh, with its module; any other named file gets the missing tests appended. The skeletons fail by construction (DOKIMOS-UNIMPLEMENTED) until you replace their
// bodies with the real tests, and the coverage report shows them as `invalid`, never as a silent "not run".
//
// Run it from the competition repository, with Dokimos on PATH (installed by `conditor workstation apply` from
// echelon-current 1.35.0) and this script from an Indy-init clone:
//
//   dotnet fsi <Indy-init>/tools/verification-kit/scaffold-task.fsx -- \
//     --files tests/Causa.Acceptance/Deterministic/AR035Tests.fs[,more...] \
//     [--obligations evidence/verification/obligations.json] [--root .] [--first REQ,...] [--import MODULE] [--dokimos PATH]
//
// It prints the identities it wrote per file. Exit 0 on success (also when a file needs nothing), 2 on bad arguments,
// 1 when Dokimos fails. It never touches a file you did not name, and never touches .git.

open System
open System.Diagnostics
open System.IO
open System.Text.Json

type Options =
    { Files: string list
      Obligations: string
      Root: string
      First: string option
      Import: string option
      Dokimos: string }

let argv = fsi.CommandLineArgs |> Array.toList |> List.tail |> List.filter ((<>) "--")

let option name = argv |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd

let split (s: string) = s.Split(',', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) |> List.ofArray

let normalize (path: string) = path.Replace('\\', '/').TrimStart('.', '/')

let parse () : Result<Options, string> =
    match option "--files" with
    | None -> Error "--files is required: the test files of your task, comma-separated, relative to the repository root"
    | Some files ->
        let root = option "--root" |> Option.defaultValue "." |> Path.GetFullPath
        Ok
            { Files = split files |> List.map normalize
              Obligations = option "--obligations" |> Option.defaultValue "evidence/verification/obligations.json" |> fun p -> Path.GetFullPath(p, root)
              Root = root
              First = option "--first"
              Import = option "--import"
              Dokimos = option "--dokimos" |> Option.defaultValue "dokimos" }

/// The directories a scaffold never needs and a copy must not carry.
let skipped = set [ ".git"; "bin"; "obj"; "node_modules"; "test-results"; "playwright-report" ]

let rec copyTree (source: string) (target: string) =
    Directory.CreateDirectory target |> ignore
    Directory.GetFiles source |> Array.iter (fun f -> File.Copy(f, Path.Combine(target, Path.GetFileName f), true))
    Directory.GetDirectories source
    |> Array.filter (fun d -> not (skipped.Contains(Path.GetFileName d)))
    |> Array.iter (fun d -> copyTree d (Path.Combine(target, Path.GetFileName d)))

let run (program: string) (args: string list) : Result<string, string> =
    let info = ProcessStartInfo(program, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    args |> List.iter info.ArgumentList.Add
    try
        use p = Process.Start info
        let out = p.StandardOutput.ReadToEndAsync()
        let err = p.StandardError.ReadToEndAsync()
        p.WaitForExit()
        if p.ExitCode = 0 then Ok out.Result else Error(sprintf "%s exited %d: %s%s" program p.ExitCode out.Result err.Result)
    with ex -> Error(sprintf "%s could not start: %s" program ex.Message)

/// (file, identities) of every work package the scaffold planned.
let packages (workJson: string) : (string * string list) list =
    use doc = JsonDocument.Parse workJson
    doc.RootElement.GetProperty("packages").EnumerateArray()
    |> Seq.map (fun p ->
        normalize (p.GetProperty("file").GetString()),
        p.GetProperty("identities").EnumerateArray() |> Seq.map (fun i -> i.GetString()) |> List.ofSeq)
    |> List.ofSeq

/// An untouched bootstrap file: only comments and one namespace line. Dokimos 0.10.0 `--append` would add indented
/// tests to it without the module they belong to (it does not compile, and the bound identity's class is missing), so
/// the scaffold writes such a file afresh instead, with its namespace, `open` and module.
let isBootstrapStub (path: string) =
    File.Exists path
    && (File.ReadAllLines path
        |> Array.map (fun l -> l.Trim())
        |> Array.filter (fun l -> l <> "" && not (l.StartsWith "//"))
        |> fun code -> code.Length <= 1 && code |> Array.forall (fun l -> l.StartsWith "namespace "))

let bytesOf (path: string) = if File.Exists path then Some(File.ReadAllBytes path) else None

let scaffold (o: Options) : Result<(string * string list * bool) list, string> =
    if not (File.Exists o.Obligations) then Error(sprintf "%s does not exist; the coordinator compiles it at T0 (tools/verification-kit/README.md)" o.Obligations)
    else
        let work = Path.Combine(Path.GetTempPath(), "scaffold-task-" + Guid.NewGuid().ToString("N"))
        try
            copyTree o.Root work
            o.Files |> List.map (fun f -> Path.Combine(work, f)) |> List.filter isBootstrapStub |> List.iter File.Delete
            let args =
                [ "verification"; "scaffold"; "--obligations"; o.Obligations; "--root"; work; "--write"; "true"; "--append"; "true" ]
                @ (o.First |> Option.map (fun f -> [ "--first"; f ]) |> Option.defaultValue [])
                @ (o.Import |> Option.map (fun m -> [ "--import"; m ]) |> Option.defaultValue [])
            run o.Dokimos args
            |> Result.map (fun json ->
                let planned = packages json
                o.Files
                |> List.map (fun file ->
                    let identities = planned |> List.filter (fst >> (=) file) |> List.collect snd
                    let source, target = Path.Combine(work, file), Path.Combine(o.Root, file)
                    let changed = bytesOf source <> bytesOf target && File.Exists source
                    if changed then
                        Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
                        File.Copy(source, target, true)
                    file, identities, changed))
        finally
            if Directory.Exists work then Directory.Delete(work, true)

let report (results: (string * string list * bool) list) =
    results
    |> List.iter (fun (file, identities, changed) ->
        match changed, identities with
        | true, ids -> printfn "%s: wrote %d skeleton(s); replace each body with the real test:\n%s" file ids.Length (ids |> List.map (sprintf "  %s") |> String.concat "\n")
        | false, [] -> printfn "%s: nothing to scaffold (no missing or invalid bound identity in this file)" file
        | false, ids -> printfn "%s: %d identity(ies) planned but the file is unchanged (already present)" file ids.Length)

let exitCode =
    match parse () with
    | Error message -> eprintfn "usage error: %s" message; 2
    | Ok options ->
        match scaffold options with
        | Ok results -> report results; 0
        | Error message -> eprintfn "stopped: %s" message; 1

exit exitCode
