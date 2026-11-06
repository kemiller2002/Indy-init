// owner-r9-ratify.fsx: the owner's two Rehearsal 9 records, in one pull request.
//
//   1. ratify   Ratify the content requirements INV-019 … INV-038. The entries of
//               requirements/catalogs/causa-investigation-content.proposed.requirement-catalog.json are copied,
//               byte for byte, into the ratified catalog requirements/catalogs/causa-investigation.requirement-catalog.json;
//               the catalog's description is corrected; the proposed file is deleted
//               (docs/requirement-catalogs/CAUSA-INVESTIGATION-CONTENT.md, OD-INDY-19..23).
//   2. pin      Pin the process-evidence rules: processEvidence.rulesSha256 in kickoff/rehearsal-009/toolchain.lock.json
//               becomes the SHA-256 of kickoff/rehearsal-009/process-evidence-rules.json
//               (docs/rehearsal-009-preflight-smoke/FINAL-CORRECTIONS.md, Owner action).
//
// Every decision is yours. The script shows what it will write and records nothing until you type the confirmation
// it asks for. It checks its own output before writing: the merged catalog must parse, keep every ratified entry
// unchanged, add exactly the proposed entries, and compile with Praxis's catalog compiler against the ratified
// manifest (no ID dropped or renamed). It then commits as you (git's configured user, no agent trailers), pushes a
// branch and opens a pull request for you to merge.
//
// Run it from the directory that holds a clean clone of kemiller2002/Indy-init on an up-to-date main, with git and gh
// signed in as you and Praxis 3.25.0 or later on PATH (it compiles the catalog). Run it from the parent directory, not
// from inside the clone: the clone's global.json pins an SDK that `dotnet fsi` would otherwise require.
//
//   dotnet fsi Indy-init/tools/owner-r9-ratify.fsx -- status --repo Indy-init            # read-only; anyone may run it
//   dotnet fsi Indy-init/tools/owner-r9-ratify.fsx -- all --repo Indy-init --dry-run     # shows the result; writes nothing
//   dotnet fsi Indy-init/tools/owner-r9-ratify.fsx -- all --repo Indy-init               # ratify + pin, one pull request
//   dotnet fsi Indy-init/tools/owner-r9-ratify.fsx -- ratify --repo Indy-init            # only the catalog
//   dotnet fsi Indy-init/tools/owner-r9-ratify.fsx -- pin --repo Indy-init               # only the rules pin
//
// Options: --repo DIR (default: current directory), --dry-run (check and print; no commit, no push), --praxis PATH
// (the praxis executable; default: praxis on PATH).
//
// Only the owner records (RDP-003): ratify, pin and all refuse to write when run by an agent (CLAUDECODE is set) or
// without a terminal to type into. status and --dry-run work for anyone, so an agent can prepare and check a step.

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Nodes

// ---- configuration -------------------------------------------------------------------------------------------------

let ratifiedPath = "requirements/catalogs/causa-investigation.requirement-catalog.json"
let proposedPath = "requirements/catalogs/causa-investigation-content.proposed.requirement-catalog.json"
let lockPath = "kickoff/rehearsal-009/toolchain.lock.json"
let rulesPath = "kickoff/rehearsal-009/process-evidence-rules.json"
let githubRepo = "kemiller2002/Indy-init"
let minimumPraxis = Version(3, 25, 0)

/// The corrected catalog description. You are asked to accept it or type your own.
let proposedDescription =
    "Field-level decomposition of the authoritative Causa investigation requirements: creation and workspace "
    + "(SCREEN-CONTRACTS S01, S02, S04, S05; REQUIREMENTS R1-R3, R10; CAUSA-UI-EXPERIENCE UI-006; CAUSA-PERSISTENCE "
    + "PERSIST-007, PERSIST-021, PERSIST-030), INV-001..INV-018; and investigation content, the Rehearsal 9 content "
    + "gaps and the answers to the catalog's open questions (OD-INDY-19..21, OD-INDY-23), INV-019..INV-038. "
    + "Drafted by an agent; ratified by the owner."

// ---- results, processes and files ----------------------------------------------------------------------------------

exception Stop of string
let stop message = raise (Stop message)

type Run = { Code: int; Out: string; Err: string }

/// Runs a program with arguments (never through a shell).
let run (dir: string) (program: string) (args: string list) : Run =
    let info = ProcessStartInfo(program, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    args |> List.iter info.ArgumentList.Add
    use p =
        try Process.Start info
        with :? ComponentModel.Win32Exception ->
            let hint =
                if program = "git" || program = "gh" then $"install {program} and sign in as yourself"
                else "install Praxis 3.25.0 beside your other versions:\n  curl -fsSL https://raw.githubusercontent.com/kemiller2002/praxis/main/scripts/install-native.sh | sh -s -- --version 3.25.0 --install-base ~/.echelon --no-activate\nthen pass --praxis ~/.echelon/tools/praxis/3.25.0/praxis"
            stop $"cannot start '{program}' (not found on PATH); {hint}"
    let out = p.StandardOutput.ReadToEndAsync()
    let err = p.StandardError.ReadToEnd()
    p.WaitForExit()
    { Code = p.ExitCode; Out = out.Result; Err = err }

let runOk dir program args =
    let r = run dir program args
    if r.Code <> 0 then stop $"""{program} {String.Join(" ", args)} failed ({r.Code}): {r.Err.Trim()}"""
    r.Out.Trim()

let sha256Hex (bytes: byte[]) = Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()
let jsonOptions = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
let ids (catalog: JsonNode) = catalog["requirements"].AsArray() |> Seq.map (fun r -> r["id"].GetValue<string>()) |> List.ofSeq
let canonical (node: JsonNode) = if isNull node then "null" else node.ToJsonString()

// ---- ratify: a pure text splice, then a structural proof that it is exactly the intended merge ---------------------

/// The lines of the proposed catalog's requirement objects: everything between `"requirements": [` and its closing `]`.
let proposedEntryLines (proposedText: string) =
    let lines = proposedText.Replace("\r\n", "\n").Split '\n' |> List.ofArray
    let start = lines |> List.findIndex (fun l -> l.Trim() = "\"requirements\": [")
    let close = lines |> List.findIndexBack (fun l -> l = "  ]" || l = "  ],")
    lines.[start + 1 .. close - 1]

/// Inserts the proposed entries after the ratified catalog's last requirement, and replaces its description.
let splice (ratifiedText: string) (proposedText: string) (description: string) =
    let lines = ratifiedText.Replace("\r\n", "\n").Split '\n' |> List.ofArray
    let start = lines |> List.findIndex (fun l -> l.Trim() = "\"requirements\": [")
    let close = start + (lines |> List.skip start |> List.findIndex (fun l -> l = "  ],"))
    let lastEntry = lines.[close - 1]
    if lastEntry <> "    }" then stop $"unexpected ratified catalog layout before line {close + 1}: '{lastEntry}'"
    let descriptionLine = lines |> List.findIndex (fun l -> l.TrimStart().StartsWith "\"description\":")
    let encodedDescription = JsonSerializer.Serialize(description, jsonOptions)
    let withDescription = lines |> List.mapi (fun i l -> if i = descriptionLine then $"  \"description\": {encodedDescription}," else l)
    let before, after = List.splitAt close withDescription
    String.Join("\n", List.concat [ List.take (before.Length - 1) before; [ "    },"]; proposedEntryLines proposedText; after ])

/// Proves the merged text is the ratified catalog plus exactly the proposed entries, with only the description changed.
let verifyMerge (ratifiedText: string) (proposedText: string) (mergedText: string) (description: string) =
    let ratified = JsonNode.Parse ratifiedText
    let proposed = JsonNode.Parse proposedText
    let merged = try JsonNode.Parse mergedText with e -> stop $"the merged catalog is not valid JSON: {e.Message}"
    let entries (n: JsonNode) = n["requirements"].AsArray() |> Seq.map canonical |> List.ofSeq
    let expectedIds = ids ratified @ ids proposed
    let problems =
        [ if ids merged <> expectedIds then "the merged IDs are not the ratified IDs followed by the proposed IDs"
          if (expectedIds |> List.distinct).Length <> expectedIds.Length then "an ID appears in both catalogs"
          if expectedIds <> (expectedIds |> List.sort) then "the IDs are not in ascending order"
          if entries merged <> entries ratified @ entries proposed then "a requirement entry differs from its source"
          if merged["description"].GetValue<string>() <> description then "the description was not written as given"
          if merged["status"].GetValue<string>() <> "ratified" then "the merged catalog is not ratified"
          for key in [ "schema"; "catalogId"; "idPattern"; "screens"; "facts"; "retired" ] do
              if canonical merged[key] <> canonical ratified[key] then $"'{key}' changed"
          for screen in proposed["screens"].AsArray() do
              if not (ratified["screens"].AsArray() |> Seq.exists (fun s -> canonical s = canonical screen)) then
                  $"""the proposed screen {canonical screen} is not in the ratified catalog"""
          for fact in proposed["facts"].AsArray() do
              if not (ratified["facts"].AsArray() |> Seq.exists (fun f -> canonical f = canonical fact)) then
                  $"""the proposed fact {canonical fact} is not in the ratified catalog""" ]
    if not problems.IsEmpty then stop ("the merge check failed: " + String.Join("; ", problems))

/// Compiles the ratified catalog, then the merged one with --previous, so Praxis refuses a dropped or renamed ID.
let compileCheck (praxis: string) (repo: string) (mergedText: string) =
    let version = runOk repo praxis [ "--version" ]
    let parsed = version.Split(' ', StringSplitOptions.RemoveEmptyEntries) |> Array.tryLast |> Option.bind (fun v -> match Version.TryParse v with | true, x -> Some x | _ -> None)
    match parsed with
    | Some v when v >= minimumPraxis -> ()
    | _ -> stop $"Praxis {minimumPraxis} or later is required to compile the catalog (found '{version}'); pass --praxis PATH"
    let scratch = Directory.CreateTempSubdirectory("owner-r9-ratify-").FullName
    let head = runOk repo "git" [ "rev-parse"; "HEAD" ]
    let mergedPath = Path.Combine(scratch, "merged.requirement-catalog.json")
    File.WriteAllText(mergedPath, mergedText)
    let compile catalog output extra =
        run repo praxis ([ "requirements"; "manifest"; "compile"; "--catalog"; catalog; "--source-commit"; head; "--output"; output ] @ extra)
    let before = compile (Path.Combine(repo, ratifiedPath)) (Path.Combine(scratch, "ratified.manifest.json")) []
    if before.Code <> 0 then stop $"Praxis cannot compile the ratified catalog ({before.Code}): {before.Err.Trim()}"
    let after = compile mergedPath (Path.Combine(scratch, "merged.manifest.json")) [ "--previous"; Path.Combine(scratch, "ratified.manifest.json") ]
    if after.Code <> 0 then stop $"Praxis refuses the merged catalog ({after.Code}): {after.Err.Trim()} {after.Out.Trim()}"
    let manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(scratch, "merged.manifest.json")))
    $"""{version}: the merged catalog compiles against the ratified manifest; {manifest["requirements"].AsArray().Count} requirements, no ID dropped or renamed"""

// ---- pin: set exactly two fields of the lock -----------------------------------------------------------------------

let pinLock (lockText: string) (rulesSha: string) (date: string) =
    let lock = JsonNode.Parse lockText
    let evidence = lock["processEvidence"].AsObject()
    evidence["rulesSha256"] <- JsonValue.Create rulesSha
    evidence["rulesStatus"] <- JsonValue.Create $"CONFIRMED by the owner on {date}: the rules file reviewed and pinned by its SHA-256"
    let written = lock.ToJsonString jsonOptions + "\n"
    // Only the two fields may differ from the committed lock.
    let strip (n: JsonNode) = let c = JsonNode.Parse(n.ToJsonString()) in c["processEvidence"].AsObject().Remove "rulesSha256" |> ignore; c["processEvidence"].AsObject().Remove "rulesStatus" |> ignore; canonical c
    if strip (JsonNode.Parse lockText) <> strip (JsonNode.Parse written) then stop "pinning would change more of the lock than rulesSha256 and rulesStatus"
    written

// ---- interaction ---------------------------------------------------------------------------------------------------

let ownerOnly () =
    if not (String.IsNullOrEmpty(Environment.GetEnvironmentVariable "CLAUDECODE")) then
        stop "an agent is running this script (CLAUDECODE is set); only the owner ratifies or pins (RDP-003). status and --dry-run are allowed"
    if Console.IsInputRedirected then stop "there is no terminal to type into; only the owner, at a terminal, ratifies or pins"

let ask (prompt: string) = printf "%s" prompt; (Console.ReadLine() |> Option.ofObj |> Option.defaultValue "").Trim()

let confirm (word: string) (what: string) =
    if ask $"\nType '{word}' to {what}, anything else to stop: " <> word then stop "stopped; nothing was written"

// ---- commands ------------------------------------------------------------------------------------------------------

type Options = { Command: string; Repo: string; DryRun: bool; Praxis: string }

let parse (args: string list) =
    let rec go opts = function
        | "--repo" :: dir :: rest -> go { opts with Repo = dir } rest
        | "--praxis" :: path :: rest -> go { opts with Praxis = path } rest
        | "--dry-run" :: rest -> go { opts with DryRun = true } rest
        | [] -> opts
        | other :: _ -> stop $"unknown argument '{other}'"
    match args with
    | command :: rest when List.contains command [ "status"; "ratify"; "pin"; "all" ] -> go { Command = command; Repo = "."; DryRun = false; Praxis = "praxis" } rest
    | _ -> stop "usage: owner-r9-ratify.fsx -- status|ratify|pin|all [--repo DIR] [--dry-run] [--praxis PATH]"

let read repo path = File.ReadAllText(Path.Combine(repo, path))

let status repo =
    let ratified = JsonNode.Parse(read repo ratifiedPath)
    printfn "Ratified catalog %s: %d requirements (%s … %s), status %s" ratifiedPath (ids ratified).Length (List.head (ids ratified)) (List.last (ids ratified)) (ratified["status"].GetValue<string>())
    if File.Exists(Path.Combine(repo, proposedPath)) then
        let proposed = JsonNode.Parse(read repo proposedPath)
        printfn "Proposed additions %s: %s" proposedPath (String.Join(", ", ids proposed))
    else printfn "Proposed additions: none (the file is gone, so they are ratified or withdrawn)"
    let lock = JsonNode.Parse(read repo lockPath)
    let pinned = lock["processEvidence"]["rulesSha256"]
    let actual = sha256Hex (File.ReadAllBytes(Path.Combine(repo, rulesPath)))
    printfn "Rules %s: sha256 %s" rulesPath actual
    printfn "Lock pin: %s" (if isNull pinned then "null (not pinned)" elif pinned.GetValue<string>() = actual then "pinned, matches the file" else $"pinned to {pinned.GetValue<string>()}, which does NOT match the file")

/// Refuses unless the clone is clean, on main, and equal to origin/main.
let preflight repo =
    if runOk repo "git" [ "status"; "--porcelain" ] <> "" then stop "the clone has uncommitted changes; start from a clean clone"
    if runOk repo "git" [ "rev-parse"; "--abbrev-ref"; "HEAD" ] <> "main" then stop "check out main first"
    runOk repo "git" [ "fetch"; "--quiet"; "origin"; "main" ] |> ignore
    if runOk repo "git" [ "rev-parse"; "HEAD" ] <> runOk repo "git" [ "rev-parse"; "origin/main" ] then stop "main is not origin/main; pull first"

let ratifyStep opts =
    let ratifiedText = read opts.Repo ratifiedPath
    if not (File.Exists(Path.Combine(opts.Repo, proposedPath))) then stop "there is no proposed catalog to ratify"
    let proposedText = read opts.Repo proposedPath
    let proposed = JsonNode.Parse proposedText
    printfn "\n== Ratify INV-019 … INV-038 ==\nThese %d proposed requirements join CAT-CAUSA-INVESTIGATION unchanged:" (ids proposed).Length
    proposed["requirements"].AsArray() |> Seq.iter (fun r -> printfn "  %s  %-14s %s" (r["id"].GetValue<string>()) (r["type"].GetValue<string>()) (r["name"].GetValue<string>()))
    printfn "Read them in full in %s and docs/requirement-catalogs/CAUSA-INVESTIGATION-CONTENT.md before ratifying." proposedPath
    printfn "\nThe catalog's description becomes:\n  %s" proposedDescription
    let description =
        if opts.DryRun then proposedDescription
        else
            let typed = ask "\nPress Enter to accept it, or type the description you want: "
            if typed = "" then proposedDescription else typed
    let mergedText = splice ratifiedText proposedText description
    verifyMerge ratifiedText proposedText mergedText description
    printfn "Check: the merged catalog keeps INV-001 … INV-018 byte for byte and adds exactly the proposed entries."
    printfn "Check: %s" (compileCheck opts.Praxis opts.Repo mergedText)
    if not opts.DryRun then confirm "ratify" "ratify INV-019 … INV-038 and delete the proposed file"
    mergedText

let pinStep opts =
    let rulesBytes = File.ReadAllBytes(Path.Combine(opts.Repo, rulesPath))
    let sha = sha256Hex rulesBytes
    let rules = JsonNode.Parse rulesBytes
    printfn "\n== Pin the process-evidence rules ==\n%s, sha256 %s\nIt names (check each one):" rulesPath sha
    rules.AsObject() |> Seq.iter (fun kv -> printfn "  %s: %s" kv.Key (let s = canonical kv.Value in if s.Length > 160 then s.Substring(0, 157) + "..." else s))
    printfn "Read the whole file before pinning. If anything is wrong, stop here and correct the file first."
    let written = pinLock (read opts.Repo lockPath) sha (DateTime.UtcNow.ToString "yyyy-MM-dd")
    printfn "Check: only processEvidence.rulesSha256 and rulesStatus change in %s." lockPath
    if not opts.DryRun then confirm "pin" $"pin rulesSha256 to {sha}"
    written, sha

let commitAll opts (changes: (string * string option) list) (messages: string list) =
    let branch = $"""owner/r9-ratify-{DateTime.UtcNow.ToString "yyyyMMdd-HHmm"}"""
    runOk opts.Repo "git" [ "switch"; "--quiet"; "-c"; branch ] |> ignore
    for (path, content), message in List.zip changes messages do
        match content with
        | Some text -> File.WriteAllText(Path.Combine(opts.Repo, path), text); runOk opts.Repo "git" [ "add"; path ] |> ignore
        | None -> runOk opts.Repo "git" [ "rm"; "--quiet"; path ] |> ignore
        if message <> "" then runOk opts.Repo "git" [ "commit"; "--quiet"; "-m"; message ] |> ignore
    runOk opts.Repo "git" [ "push"; "--quiet"; "-u"; "origin"; branch ] |> ignore
    let body = "The owner's Rehearsal 9 records, made with tools/owner-r9-ratify.fsx:\n\n" + String.Join("\n", messages |> List.filter ((<>) "") |> List.map (fun m -> "- " + m.Split('\n').[0]))
    let url = runOk opts.Repo "gh" [ "pr"; "create"; "--repo"; githubRepo; "--base"; "main"; "--head"; branch; "--title"; "Owner: Rehearsal 9 ratification and rules pin"; "--body"; body ]
    printfn "\nPushed %s and opened %s. Merge it when its checks pass." branch url

let main argv =
    let opts = parse (List.ofArray argv)
    let repo = Path.GetFullPath opts.Repo
    let opts = { opts with Repo = repo }
    if not (File.Exists(Path.Combine(repo, ratifiedPath))) then stop $"{repo} is not an Indy-init clone (no {ratifiedPath})"
    match opts.Command with
    | "status" -> status repo
    | command ->
        if not opts.DryRun then ownerOnly (); preflight repo
        let ratify = command = "ratify" || command = "all"
        let pin = command = "pin" || command = "all"
        let catalog = if ratify then Some (ratifyStep opts) else None
        let lock = if pin then Some (pinStep opts) else None
        if opts.DryRun then printfn "\nDry run: every check passed; nothing was written."
        else
            let changes, messages =
                List.unzip (
                    [ match catalog with
                      | Some text ->
                          (ratifiedPath, Some text), ""
                          (proposedPath, None), "Ratify INV-019..INV-038 into CAT-CAUSA-INVESTIGATION (OD-INDY-19..23)\n\nThe proposed content requirements join the ratified catalog unchanged, the catalog's description is corrected, and the proposed file is retired."
                      | None -> ()
                      match lock with
                      | Some (text, sha) -> (lockPath, Some text), $"Pin the Rehearsal 9 process-evidence rules (sha256 {sha})\n\nprocessEvidence.rulesSha256 in {lockPath} is the SHA-256 of {rulesPath}, which the owner reviewed."
                      | None -> () ])
            commitAll opts changes messages

try main (fsi.CommandLineArgs |> Array.skip 1 |> Array.filter ((<>) "--")) with Stop message -> eprintfn "owner-r9-ratify: %s" message; exit 1
