// browser-os-deps.fsx: install the OS packages Playwright's browsers need, on a GitHub Ubuntu runner, without depending
// on one apt mirror.
//
//   dotnet fsi tools/r9/browser-os-deps.fsx -- --dir DIR --browsers chromium,webkit
//        [--mirrors URL,...] [--update-seconds N] [--fetch-seconds N]
//
// DIR holds the Playwright that will run the browsers (its node_modules). The package set is exactly the one
// `npx playwright install --with-deps` installs: Playwright's own `install-deps --dry-run` names what is missing
// (apt's simulation of `apt-get install --no-install-recommends` of Playwright's dependency table), and success is that
// same dry run reporting nothing missing. Only where the packages come from changes:
//   1. Nothing missing: no apt at all (a second install step in the same job costs no mirror round trip).
//   2. Otherwise, for each mirror in order (the runner's configured mirror first, then archive.ubuntu.com and
//      mirrors.edge.kernel.org, all the same signed Ubuntu archive): point the Ubuntu sources at it, `apt-get update` and
//      `apt-get install --download-only`, each bounded (killing apt before dpkg runs is safe; partial downloads carry over
//      to the next mirror). The first mirror that delivers every package wins.
//   3. `apt-get install --no-download` from the downloaded set (dpkg is never killed), then the dry run must report
//      nothing missing.
// Every failure exits 1 with `::error::phase=browser-os-deps: ...` naming the mirrors tried and why; nothing is skipped.
//
//   dotnet fsi tools/r9/browser-os-deps.fsx -- self-test
//        Checks the source rewriting and the dry-run parsing on fixed inputs (no apt, no network).

open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions

let argv = fsi.CommandLineArgs |> Array.skip 1 |> Array.skipWhile ((=) "--")

let opt name =
    argv |> Array.pairwise |> Array.tryFind (fst >> (=) name) |> Option.map snd

let defaultMirrors = [ "http://archive.ubuntu.com/ubuntu/"; "http://mirrors.edge.kernel.org/ubuntu/" ]

/// The Ubuntu archive URIs of a sources file (deb822 `URIs:` fields or one-line `deb` entries) replaced by `mirror`;
/// third-party repositories are left alone.
let ubuntuUri = Regex(@"https?://([a-z0-9-]+\.)*(archive|security)\.ubuntu\.com/ubuntu/?(?=\s|$)", RegexOptions.Multiline)

let rewrite (mirror: string) (text: string) = ubuntuUri.Replace(text, mirror)

let configuredMirror (text: string) =
    ubuntuUri.Matches text |> Seq.map (fun m -> m.Value) |> Seq.tryHead

/// Playwright's `install-deps --dry-run` prints "Missing system dependencies (N):" then one indented package per line.
let missingOf (stdout: string) =
    stdout.Split('\n')
    |> Array.skipWhile (fun l -> not (l.StartsWith "Missing system dependencies"))
    |> fun ls -> if ls.Length > 0 then Array.skip 1 ls else [||]
    |> Array.map (fun l -> l.Trim())
    |> Array.filter (fun l -> Regex.IsMatch(l, @"^[a-z0-9][a-z0-9.+:-]*$"))
    |> Array.toList
let selfTest () =
    let deb822 =
        "Types: deb\nURIs: http://azure.archive.ubuntu.com/ubuntu/\nSuites: noble noble-updates noble-backports\nComponents: main universe restricted multiverse\nSigned-By: /usr/share/keyrings/ubuntu-archive-keyring.gpg\n\nTypes: deb\nURIs: http://security.ubuntu.com/ubuntu\nSuites: noble-security\n"
    let oneLine = "deb http://azure.archive.ubuntu.com/ubuntu/ noble main\ndeb [arch=amd64] https://packages.microsoft.com/ubuntu/24.04/prod noble main\n"
    let dry = "Missing system dependencies (3):\n  fonts-liberation\n  libgtk-4-1\n  libx264-164\n"
    let checks =
        [ "deb822 archive and security point at the mirror",
          rewrite "http://m/ubuntu/" deb822 = deb822.Replace("http://azure.archive.ubuntu.com/ubuntu/", "http://m/ubuntu/").Replace("http://security.ubuntu.com/ubuntu", "http://m/ubuntu/")
          "a third-party repository is untouched", rewrite "http://m/ubuntu/" oneLine = oneLine.Replace("http://azure.archive.ubuntu.com/ubuntu/", "http://m/ubuntu/")
          "the configured mirror is found", configuredMirror deb822 = Some "http://azure.archive.ubuntu.com/ubuntu/"
          "missing packages are parsed", missingOf dry = [ "fonts-liberation"; "libgtk-4-1"; "libx264-164" ]
          "nothing missing parses as empty", missingOf "All system dependencies are installed.\n" = [] ]
    checks |> List.iter (fun (name, ok) -> printfn "%s %s" (if ok then "PASS" else "FAIL") name)
    if checks |> List.forall snd then 0 else 1

/// Runs a command, echoing its output; returns the exit code and stdout.
let run (cwd: string) (exe: string) (args: string list) =
    let info = ProcessStartInfo(exe, WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = false)
    args |> List.iter info.ArgumentList.Add
    use p = Process.Start info
    let out = p.StandardOutput.ReadToEnd()
    p.WaitForExit()
    Console.Write out
    p.ExitCode, out

let fail (msg: string) =
    printfn "::error::phase=browser-os-deps: %s" msg
    1

let sourcesFiles () =
    [ "/etc/apt/sources.list.d/ubuntu.sources"; "/etc/apt/sources.list" ]
    |> List.filter File.Exists
    |> List.map (fun f -> f, File.ReadAllText f)
    |> List.filter (snd >> ubuntuUri.IsMatch)

let writeRoot (path: string) (text: string) =
    let tmp = Path.GetTempFileName()
    File.WriteAllText(tmp, text)
    fst (run "/" "sudo" [ "cp"; tmp; path ]) = 0

let install (dir: string) (browsers: string list) (mirrors: string list) (updateSeconds: int) (fetchSeconds: int) =
    let dryRun () = run dir "npx" ([ "--no-install"; "playwright"; "install-deps"; "--dry-run" ] @ browsers)
    let apt seconds args = fst (run "/" "sudo" ([ "timeout"; "-k"; "10"; string seconds; "apt-get" ] @ args))
    let aptOpts = [ "-o"; "Acquire::Retries=2"; "-o"; "Acquire::http::Timeout=30"; "-q" ]
    match dryRun () with
    | 0, _ ->
        printfn "browser-os-deps: every OS package the browsers need is installed; no apt"
        0
    | _ ->
        let originals = sourcesFiles ()
        let configured = originals |> List.tryPick (snd >> configuredMirror)
        let candidates = (Option.toList configured @ mirrors) |> List.distinct
        // One attempt at a mirror: the packages missing after its index, all downloaded, or why not.
        let attempt mirror =
            printfn "::group::browser-os-deps: mirror %s" mirror
            let result =
                if not (originals |> List.forall (fun (f, t) -> writeRoot f (rewrite mirror t))) then Error "could not write the apt sources"
                elif apt updateSeconds (aptOpts @ [ "update" ]) <> 0 then Error $"apt-get update failed or exceeded {updateSeconds} s"
                else
                    match dryRun () with
                    | 0, _ -> Ok []
                    | _, out ->
                        match missingOf out with
                        | [] -> Error "Playwright's dry run failed without naming missing packages"
                        | missing when apt fetchSeconds (aptOpts @ [ "install"; "-y"; "--no-install-recommends"; "--download-only" ] @ missing) = 0 -> Ok missing
                        | missing -> Error $"downloading {missing.Length} packages failed or exceeded {fetchSeconds} s"
            printfn "::endgroup::"
            mirror, result
        let rec firstOk tried =
            function
            | [] -> Error(List.rev tried)
            | m :: rest ->
                match attempt m with
                | _, Ok missing -> Ok(m, missing)
                | _, Error why -> firstOk ((m, why) :: tried) rest
        match firstOk [] candidates with
        | Error tried ->
            originals |> List.iter (fun (f, t) -> writeRoot f t |> ignore)
            fail ("no mirror delivered the browsers' OS packages: " + (tried |> List.map (fun (m, why) -> $"{m}: {why}") |> String.concat "; "))
        | Ok(mirror, missing) ->
            printfn "browser-os-deps: %d packages from %s" missing.Length mirror
            let installed =
                missing.IsEmpty
                || fst (run "/" "sudo" ([ "apt-get"; "install"; "-y"; "-q"; "--no-install-recommends"; "--no-download" ] @ missing)) = 0
            match installed, dryRun () with
            | false, _ -> fail $"apt-get install --no-download of the {missing.Length} packages downloaded from {mirror} failed"
            | true, (0, _) -> 0
            | true, (_, out) -> fail ("Playwright still reports missing OS packages: " + String.Join(", ", missingOf out))

let exitCode =
    match argv |> Array.tryHead with
    | Some "self-test" -> selfTest ()
    | _ ->
        match opt "--dir", opt "--browsers" with
        | Some dir, Some browsers ->
            install
                dir
                (browsers.Split(',', StringSplitOptions.RemoveEmptyEntries) |> Array.toList)
                (opt "--mirrors" |> Option.map (fun m -> m.Split(',', StringSplitOptions.RemoveEmptyEntries) |> Array.toList) |> Option.defaultValue defaultMirrors)
                (opt "--update-seconds" |> Option.map int |> Option.defaultValue 90)
                (opt "--fetch-seconds" |> Option.map int |> Option.defaultValue 180)
        | _ -> fail "usage: browser-os-deps.fsx -- --dir DIR --browsers chromium,webkit [--mirrors URL,...] [--update-seconds N] [--fetch-seconds N]"

exit exitCode
