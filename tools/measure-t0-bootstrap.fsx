// measure-t0-bootstrap.fsx: times the v4.1.1 T0 bootstrap (T10's mechanical part) on a fresh empty repository.
//
// It runs the sequence the plan prescribes and prints one tab-separated line per step (name, seconds, exit code,
// command), the same shape as requirements-compiled/v4.1.1/analysis/bootstrap-measurement.json. It never touches a
// competition repository: the target must be a new directory, and nothing is pushed or published.
//
//   dotnet fsi tools/measure-t0-bootstrap.fsx -- --indy-init DIR --target NEW-DIR
//
// The tools are taken from PATH: conditor 0.10.18, praxis 3.24.0, ordo 1.19.0 and percepta-repo 0.1.0, the release
// binaries the Registry set echelon-current pins (check each against its SHA-256 before use). No credential is needed;
// GH_TOKEN, GITHUB_TOKEN and CONDITOR_GITHUB_TOKEN are removed from the steps' environment.

open System
open System.Diagnostics
open System.IO

let argv = fsi.CommandLineArgs |> Array.toList |> List.tail
let option name = argv |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd

let usage () =
    eprintfn "usage: dotnet fsi tools/measure-t0-bootstrap.fsx -- --indy-init DIR --target NEW-DIR"
    exit 2

let indyInit = option "--indy-init" |> Option.defaultWith usage |> Path.GetFullPath
let target = option "--target" |> Option.defaultWith usage |> Path.GetFullPath

if Directory.Exists target && not (Seq.isEmpty (Directory.EnumerateFileSystemEntries target)) then
    eprintfn "%s is not empty; the measurement needs a new directory" target
    exit 2

Directory.CreateDirectory target |> ignore

let compiler = Path.Combine(indyInit, "tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll")

/// One step: (name, program, arguments). Run in the target directory without credentials.
let steps =
    [ "1-git-init", "git", [ "init"; "-q"; "-b"; "main" ]
      "2-conditor-init", "conditor", [ "init"; "--preset"; "indy-init"; "--target"; "." ]
      "3-v411-bootstrap", "dotnet", [ compiler; "v41"; "--source"; indyInit; "--package"; Path.Combine(indyInit, "requirements-compiled/v4.1.1"); "--base"; Path.Combine(indyInit, "requirements-compiled/v4"); "--check"; "--bootstrap"; "." ]
      "4-build-Causa", "dotnet", [ "build"; "Causa.slnx" ]
      "4-build-App", "dotnet", [ "build"; "App.slnx" ]
      "5-ci-policy", "conditor", [ "ci-policy"; "check"; "--target"; "." ]
      "6-verify", "conditor", [ "verify"; "--target"; "." ]
      "7-commit", "git", [ "-c"; "user.name=t0"; "-c"; "user.email=t0@example.invalid"; "-c"; "commit.gpgsign=false"; "commit"; "-q"; "-m"; "T0 bootstrap" ] ]

let run (name: string, program: string, args: string list) =
    let info = ProcessStartInfo(program, WorkingDirectory = target, RedirectStandardOutput = true, RedirectStandardError = true)
    args |> List.iter info.ArgumentList.Add
    [ "GH_TOKEN"; "GITHUB_TOKEN"; "CONDITOR_GITHUB_TOKEN" ] |> List.iter (info.Environment.Remove >> ignore)
    let clock = Stopwatch.StartNew()
    use p = Process.Start info
    let output = p.StandardOutput.ReadToEndAsync()
    let errors = p.StandardError.ReadToEndAsync()
    p.WaitForExit()
    clock.Stop()
    File.WriteAllText(Path.Combine(target, "..", Path.GetFileName target + "." + name + ".log"), output.Result + errors.Result)
    name, clock.Elapsed.TotalSeconds, p.ExitCode, program + " " + String.Join(" ", args)

// Step 7 is `git add -A` and `git commit`, timed together as the recorded runs timed them.
let results =
    steps
    |> List.map (fun ((name, _, _) as step) ->
        if name = "7-commit" then
            let _, addSeconds, addCode, _ = run ("7-git-add", "git", [ "add"; "-A" ])
            let n, commitSeconds, commitCode, cmd = run step
            n, addSeconds + commitSeconds, (if addCode <> 0 then addCode else commitCode), "git add -A && " + cmd
        else run step)

results |> List.iter (fun (n, s, c, cmd) -> printfn "%s\t%.2f\t%d\t%s" n s c cmd)
printfn "total\t%.1f" (results |> List.sumBy (fun (_, s, _, _) -> s))
exit (if results |> List.forall (fun (_, _, c, _) -> c = 0) then 0 else 1)
