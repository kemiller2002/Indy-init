// owner-freeze.fsx: the owner's steps to freeze Indy-init's compiled requirements packages v4, v4.1, v4.1.1, v4.1.2 and v4.1.4
// (the FREEZE.md of each package under requirements-compiled/).
//
// Every decision is yours. The script shows each owner decision request as the package states it and records
// exactly the choice you type. Nothing has a default, and nothing is written until you confirm the summary. It
// writes the records in the format the compiler accepts, makes the commit as you (with no agent trailers, which
// the compiler would reject), pushes a branch and opens a pull request for you to merge.
//
// Run it against a clean clone of kemiller2002/Indy-init, with git and gh signed in as you. Run it from the directory
// that holds the clone, not from inside it: the clone's global.json pins an SDK that `dotnet fsi` would otherwise
// require. From the parent directory of a clone named Indy-init:
//
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- status --repo Indy-init
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4 --repo Indy-init     # ODR-V4-02..11
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4 --repo Indy-init    # ODR-V4-01, after the agent applied and recompiled
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4 --repo Indy-init        # tags the first commit on main whose manifest reads READY-FOR-OWNER-FREEZE
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1 --repo Indy-init   # ODR-V41-02..09, each asked once (after v4 is frozen)
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4.1 --repo Indy-init  # ODR-V41-01
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4.1 --repo Indy-init --commit <merge commit of the ODR-V41-01 pull request>
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1.1 --repo Indy-init  # ODR-V411-02
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4.1.1 --repo Indy-init # ODR-V411-01, after ODR-V411-02
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4.1.1 --repo Indy-init --commit <merge commit of the ODR-V411-01 pull request>
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- freeze v4.1.1 --repo Indy-init   # all three v4.1.1 steps in order: decide, merge,
//                                                                                  # approve, merge, tag; each waits for you to type
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1.2 --repo Indy-init  # ODR-V412-01..03 and -05; then the agent compiles v4.1.2
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4.1.2 --repo Indy-init # ODR-V412-04, after the compiled package is reviewed
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4.1.2 --repo Indy-init --commit <merge commit of the ODR-V412-04 pull request>
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1.4 --repo Indy-init  # ODR-V414-01, -02; then the agent compiles v4.1.4
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4.1.4 --repo Indy-init # ODR-V414-03, after the compiled package is reviewed
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4.1.4 --repo Indy-init --commit <merge commit of the ODR-V414-03 pull request>
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide rehearsal-010 --repo Indy-init  # ODR-RG-03, -05 and ODR-R10-01..18
//        (kickoff/rehearsal-010/proposals/owner-decision-requests.json). A request that pins a file records the sha256
//        of that file at origin/main, shown before you answer; ODR-R10-01 is asked only once v4.1.4 is tagged.
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- ratify rehearsal-010 --repo Indy-init  # applies ODR-R10-02 (A): the
//        successor catalog becomes the ratified catalog, only if it is the file you approved and keeps every
//        ratified requirement unchanged
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide historical-compatibility --repo Indy-init  # OD-HC-01..07
//        (proposals/r10-historical-data-compatibility/proposals/owner-decision-requests.json, once on origin/main).
//        A request that pins a file records its sha256 at origin/main, shown before you answer.
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- acceptance --repo Indy-init     # the scope-change approvals the acceptance verifier
//                                                                                  # reads (none today), and their pin in conditor.acceptance.json
//
// Options: --repo DIR (default: current directory), --dry-run (print what would be written; no commit, no push).
//
// Only the owner records (RDP-003): decide, approve, tag and acceptance refuse to write when run by an agent (CLAUDECODE is set) or
// without a terminal to type into. status and --dry-run work for anyone, so an agent can prepare and check a step.

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions

Environment.SetEnvironmentVariable("GIT_NO_REPLACE_OBJECTS", "1")
Environment.SetEnvironmentVariable("GIT_GRAFT_FILE", "/nonexistent/owner-freeze-no-grafts")

// ---- configuration -------------------------------------------------------------------------------------------------

let operatorId = "kemiller2002"
let approvalsPath = "kickoff/rehearsal-007/approvals.causa.json"
/// The acceptance verifier's approvals: requirement scope changes only (causa.requirements-approvals/1, each change a
/// requirement_id and an action, each approval an immutable source). Package and decision records stay in approvalsPath,
/// which the verifier does not read (R9-READINESS-HANDOFF.md §F item 1, owner's choice of 2026-11-02: separate files).
let acceptanceApprovalsPath = "kickoff/acceptance/approvals.causa.json"
let acceptanceContractPath = "conditor.acceptance.json"
let githubRepo = "kemiller2002/Indy-init"

type Package =
    { Name: string                 // "v4", "v4.1", "v4.1.1" or "v4.1.2"
      Dir: string                  // requirements-compiled/<name>
      ApprovalPrefix: string       // APR-V4-OWNER / APR-V41-OWNER
      PackageApprovalId: string    // ODR-V4-01 / ODR-V41-01 / ODR-V411-01 / ODR-V412-04
      Tag: string }

let packages =
    [ { Name = "v4"; Dir = "requirements-compiled/v4"; ApprovalPrefix = "APR-V4-OWNER"; PackageApprovalId = "ODR-V4-01"; Tag = "indy-init-requirements-v4.0.0" }
      { Name = "v4.1"; Dir = "requirements-compiled/v4.1"; ApprovalPrefix = "APR-V41-OWNER"; PackageApprovalId = "ODR-V41-01"; Tag = "indy-init-requirements-v4.1.0" }
      { Name = "v4.1.1"; Dir = "requirements-compiled/v4.1.1"; ApprovalPrefix = "APR-V411-OWNER"; PackageApprovalId = "ODR-V411-01"; Tag = "indy-init-requirements-v4.1.1" }
      { Name = "v4.1.2"; Dir = "requirements-compiled/v4.1.2"; ApprovalPrefix = "APR-V412-OWNER"; PackageApprovalId = "ODR-V412-04"; Tag = "indy-init-requirements-v4.1.2" }
      // v4.1.4 (docs/release-gate-repair): corrected suite runners. 4.1.3 belonged to the withdrawn proposal.
      { Name = "v4.1.4"; Dir = "requirements-compiled/v4.1.4"; ApprovalPrefix = "APR-V414-OWNER"; PackageApprovalId = "ODR-V414-03"; Tag = "indy-init-requirements-v4.1.4" }
      // Rehearsal 010's owner decisions (docs/rehearsal-010-readiness/OWNER-APPROVAL-EXECUTION.md): decisions only, no
      // package approval and no tag.
      { Name = "rehearsal-010"; Dir = "kickoff/rehearsal-010"; ApprovalPrefix = "APR-R10-OWNER"; PackageApprovalId = ""; Tag = "" }
      // Historical-compatibility decisions OD-HC-01..07 (proposals/r10-historical-data-compatibility/OWNER-DECISIONS-REQUIRED.md):
      // decisions only, no package approval and no tag. A successor package that applies them gets its own entry.
      { Name = "historical-compatibility"; Dir = "proposals/r10-historical-data-compatibility"; ApprovalPrefix = "APR-HC-OWNER"; PackageApprovalId = ""; Tag = "" } ]

// ---- processes, files and JSON -------------------------------------------------------------------------------------

exception Stop of string
let stop message = raise (Stop message)

/// Runs a program with arguments (never through a shell) and returns stdout bytes, or stops with its error.
let runBytes (dir: string) (program: string) (args: string list) : byte[] =
    let info = ProcessStartInfo(program, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    args |> List.iter info.ArgumentList.Add
    use p = Process.Start info
    use out = new MemoryStream()
    p.StandardOutput.BaseStream.CopyTo out
    let err = p.StandardError.ReadToEnd()
    p.WaitForExit()
    if p.ExitCode = 0 then out.ToArray() else stop $"{program} {String.Join(' ', args)} failed: {err.Trim()}"

let run dir program args = runBytes dir program args |> Encoding.UTF8.GetString |> fun s -> s.Trim()
let git dir args = run dir "git" args
let gitOk dir args = try git dir args |> ignore; true with Stop _ -> false

let stamp () = DateTime.UtcNow.ToString "yyyyMMdd-HHmm"
let sha256Hex (bytes: byte[]) = (SHA256.HashData bytes |> Convert.ToHexString).ToLowerInvariant()
let parse (bytes: byte[]) = JsonNode.Parse(Encoding.UTF8.GetString bytes)
let atMain repo path = runBytes repo "git" [ "show"; $"origin/main:{path}" ]
let atCommit repo commit path = runBytes repo "git" [ "show"; $"{commit}:{path}" ]

let str (node: JsonNode) (name: string) =
    Option.ofObj node |> Option.bind (fun n -> Option.ofObj n[name]) |> Option.map string |> Option.defaultValue ""
let items (node: JsonNode) (name: string) : JsonNode list =
    match Option.ofObj node |> Option.bind (fun n -> Option.ofObj n[name]) with
    | Some(:? JsonArray as a) -> a |> Seq.toList
    | _ -> []

let jsonOptions = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
let render (node: JsonNode) = node.ToJsonString jsonOptions + "\n"

let obj (pairs: (string * JsonNode) list) =
    JsonObject(pairs |> List.map (fun (k, v) -> Collections.Generic.KeyValuePair(k, v))) :> JsonNode
let jstr (s: string) = JsonValue.Create s :> JsonNode
let jarr (xs: JsonNode list) = JsonArray(Array.ofList xs) :> JsonNode

// ---- console -------------------------------------------------------------------------------------------------------

let say (s: string) = Console.WriteLine s
let rule () = say (String('─', 100))

/// Asks until the answer is one of `valid` (case-insensitive). No default: an empty answer asks again.
let rec choose (prompt: string) (valid: string list) : string =
    Console.Write($"{prompt} [{String.Join('/', valid)}]: ")
    match Console.ReadLine() with
    | null -> stop "input ended before an answer was given; nothing was written"
    | answer ->
        match valid |> List.tryFind (fun v -> v.Equals(answer.Trim(), StringComparison.OrdinalIgnoreCase)) with
        | Some v -> v
        | None -> say "  Please type one of the listed answers."; choose prompt valid

/// Asks for free text until `check` accepts it.
let rec askText (prompt: string) (check: string -> string option) : string =
    Console.Write($"{prompt}: ")
    match Console.ReadLine() with
    | null -> stop "input ended before an answer was given; nothing was written"
    | answer ->
        match check (answer.Trim()) with
        | None -> answer.Trim()
        | Some problem -> say $"  {problem}"; askText prompt check

let wrap (indent: string) (text: string) =
    let words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
    let lines =
        words
        |> List.fold (fun (acc: string list) w ->
            match acc with
            | current :: rest when current.Length + w.Length + 1 <= 96 - indent.Length -> (current + " " + w) :: rest
            | _ -> w :: acc) []
        |> List.rev
    lines |> List.iter (fun l -> say (indent + l))

// ---- repository checks ---------------------------------------------------------------------------------------------

let prepare (repo: string) =
    let origin = git repo [ "remote"; "get-url"; "origin" ]
    if not (origin.ToLowerInvariant().Contains "indy-init") then stop $"{repo} is not a clone of {githubRepo} (origin {origin})"
    if git repo [ "status"; "--porcelain" ] <> "" then stop "the working tree has uncommitted changes; commit or stash them first"
    if git repo [ "config"; "user.name" ] = "" || git repo [ "config"; "user.email" ] = "" then stop "git user.name and user.email must be set to you"
    git repo [ "fetch"; "--quiet"; "origin"; "main"; "--tags" ] |> ignore

let approvalsAtMain repo = atMain repo approvalsPath |> parse

/// Every change already recorded by an operator approval, flattened.
let recordedChanges (approvals: JsonNode) : JsonNode list =
    items approvals "approvals" |> List.collect (fun a -> items a "changes")

let nextApprovalId (approvals: JsonNode) (prefix: string) =
    let used =
        items approvals "approvals"
        |> List.choose (fun a -> let m = Regex.Match(str a "id", $"^{Regex.Escape prefix}-(\\d+)$") in if m.Success then Some(int m.Groups[1].Value) else None)
    $"{prefix}-{(List.fold max 0 used) + 1}"

/// The approvals file with one more operator approval holding `changes`; the existing content is kept as it is.
let withApproval (approvals: JsonNode) (approvalId: string) (changes: JsonNode list) =
    let approval =
        obj [ "id", jstr approvalId
              "authority", obj [ "kind", jstr "operator"; "id", jstr operatorId ]
              "date", jstr (DateTime.UtcNow.ToString "yyyy-MM-dd")
              "changes", jarr changes ]
    let existing = items approvals "approvals" |> List.map (fun a -> a.DeepClone())
    approvals.AsObject()
    |> Seq.map (fun kv -> kv.Key, (if kv.Key = "approvals" then jarr (existing @ [ approval ]) else Option.ofObj kv.Value |> Option.map (fun v -> v.DeepClone()) |> Option.toObj))
    |> List.ofSeq
    |> fun pairs -> if pairs |> List.exists (fst >> (=) "approvals") then pairs else pairs @ [ ("approvals", jarr [ approval ]) ]
    |> obj

/// Writes `files` (path, content) on a new branch from origin/main, commits as you, pushes and opens a pull request.
let commitFilesAndPropose (repo: string) (dryRun: bool) (branch: string) (title: string) (body: string) (files: (string * string) list) : string option =
    if dryRun then
        rule ()
        files |> List.iter (fun (path, content) -> say $"DRY RUN: {path} would become:"; say content)
        say $"DRY RUN: commit \"{title}\" on branch {branch}; nothing was written, committed or pushed."
        None
    else
        git repo [ "switch"; "--quiet"; "-c"; branch; "origin/main" ] |> ignore
        files |> List.iter (fun (path, content) ->
            let full = Path.Combine(repo, path)
            Directory.CreateDirectory(Path.GetDirectoryName full) |> ignore
            File.WriteAllText(full, content)
            git repo [ "add"; path ] |> ignore)
        git repo [ "commit"; "--quiet"; "-m"; title; "-m"; body ] |> ignore
        let message = git repo [ "log"; "-1"; "--format=%B" ]
        if message.Contains "Claude-Session:" || message.Contains "Co-Authored-By: Claude" then
            stop "the commit message carries an agent trailer (a git hook may have added it); the compiler would not count these records. The commit was not pushed."
        git repo [ "push"; "--quiet"; "-u"; "origin"; branch ] |> ignore
        let url = try run repo "gh" [ "pr"; "create"; "--repo"; githubRepo; "--base"; "main"; "--head"; branch; "--title"; title; "--body"; body ] with Stop e -> $"(gh could not open the pull request: {e}) open it at https://github.com/{githubRepo}/compare/main...{branch}"
        say $"Committed as you on {branch} and pushed. Pull request: {url}"
        say "Review the diff there and merge it yourself."
        Some branch

/// Writes the approvals file on a new branch from origin/main, commits as you, pushes and opens a pull request.
let commitAndPropose repo dryRun branch title body (content: string) =
    commitFilesAndPropose repo dryRun branch title body [ approvalsPath, content ]

// ---- status ----------------------------------------------------------------------------------------------------------

/// A package whose requests file is not on origin/main yet (its pull request is unmerged) has no requests.
let requestsOf repo (p: Package) =
    match (try Some(atMain repo $"{p.Dir}/proposals/owner-decision-requests.json") with Stop _ -> None) with
    | Some bytes -> items (parse bytes) "requests"
    | None when p.Name = "historical-compatibility" -> say $"  ({p.Dir}/proposals/owner-decision-requests.json is not on origin/main yet: merge its pull request first)"; []
    | None -> stop $"{p.Dir}/proposals/owner-decision-requests.json is missing on origin/main"

let isRecorded (changes: JsonNode list) (request: JsonNode) =
    let id = str request "id"
    let byDecision = changes |> List.exists (fun c -> str c "decision_id" = id)
    let obligations = match request["approvalRecord"] with :? JsonArray as a -> a |> Seq.map (fun r -> str r "obligation_id") |> List.ofSeq | _ -> []
    byDecision || (obligations <> [] && obligations |> List.forall (fun o -> changes |> List.exists (fun c -> str c "obligation_id" = o && str c "action" = "amend")))

let status repo =
    prepare repo
    let changes = recordedChanges (approvalsAtMain repo)
    packages |> List.iter (fun p ->
        rule ()
        say $"""{p.Name}  (origin/main {git repo [ "rev-parse"; "--short"; "origin/main" ]})"""
        requestsOf repo p |> List.iter (fun r -> say $"""  {str r "id"}  {(if isRecorded changes r then "recorded" else "PENDING")}  {str r "title"}""")
        if p.Name = "v4" then
            let m = atMain repo $"{p.Dir}/freeze-manifest.json" |> parse
            say $"""  freeze-manifest status: {str m "status"}"""
            items m "freezeGate" |> List.filter (fun g -> str g "status" <> "PASS") |> List.iter (fun g -> say $"""    {str g "id"} {str g "status"}: {str g "detail"}""")
        if p.Tag <> "" then
            let tagged = git repo [ "tag"; "-l"; p.Tag ]
            say $"""  tag {p.Tag}: {(if tagged = "" then "not created" else "exists")}""")

// ---- decide ----------------------------------------------------------------------------------------------------------

type Answer =
    | Approve of JsonNode list     // amendment records as proposed
    | Decline
    | Select of JsonNode           // one decision change
    | Skip

let letters (request: JsonNode) =
    items request "options" |> List.choose (fun o -> let s = string o in match s.IndexOf ':' with n when n > 0 && n <= 2 -> Some(s.Substring(0, n).Trim()) | _ -> None)

let show (request: JsonNode) =
    rule ()
    say $"""{str request "id"}  ({str request "kind"})  {str request "title"}"""
    [ "question", "Question"; "rationale", "Why"; "impact", "Impact"; "recommendation", "Agent's recommendation (not a decision)" ]
    |> List.iter (fun (k, label) -> if str request k <> "" then (say $"  {label}:"; wrap "    " (str request k)))
    items request "options" |> List.iter (fun o -> wrap "    • " (string o))

let decideAmendment (request: JsonNode) =
    let records = match request["approvalRecord"] with :? JsonArray as a -> a |> Seq.toList | _ -> []
    say "  Proposed amendment(s), written exactly as below if you approve:"
    records |> List.iter (fun r ->
        say $"""    {str r "obligation_id"}:"""; wrap "      " (str r "criterion")
        if str r "combinedWith" <> "" then say $"""      (if {str r "combinedWith"} is approved too, its combined criterion is written instead; shown with that request)""")
    match choose "  Approve, decline, or skip (leave pending)" [ "approve"; "decline"; "skip" ] with
    | "approve" -> Approve records
    | "decline" -> Decline
    | _ -> Skip

let decideTopology (request: JsonNode) =
    let topologies = Regex.Matches(str request["approvalRecord"] "how", "\"([A-C]-[a-z0-9-]+)\"") |> Seq.map (fun m -> m.Groups[1].Value) |> List.ofSeq
    match choose "  Topology, or skip (leave pending)" (topologies @ [ "skip" ]) with
    | "skip" -> Skip
    | t -> Select(obj [ "decision_id", jstr (str request "id"); "action", jstr "select-topology"; "topology", jstr t ])

let packageList (answer: string) =
    let parts = answer.Split(',', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) |> List.ofArray
    if parts.IsEmpty then Some "Name at least one package as id@version."
    elif parts |> List.forall (fun p -> Regex.IsMatch(p, @"^(@[^@\s/]+/)?[^@\s/]+@[0-9][^@\s]*$")) then None
    else Some "Each entry must be id@version, for example Name.Of.Package@1.2.3 or @scope/name@1.2.3 (comma-separated)."

let decideOption (action: string) (request: JsonNode) =
    let id = str request "id"
    match choose "  Option, or skip (leave pending)" (letters request @ [ "skip" ]) with
    | "skip" -> Skip
    | option ->
        let extra =
            match id, str request "kind" with
            | "ODR-V41-06", _ -> [ ("organizer_confirmation", jstr (askText "  Who confirmed, and when" (fun s -> if s = "" then Some "This record needs it." else None))) ]
            // Every dependency-lock request (ODR-V41-07, ODR-V41-09) records the exact packages the owner locks.
            | _, "dependency-lock" ->
                let packages = askText "  Exact packages to lock, as id@version, comma-separated" packageList
                [ ("packages", jarr (packages.Split(',', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) |> Seq.map jstr |> List.ofSeq)) ]
            | _ -> []
        Select(obj ([ "decision_id", jstr id; "action", jstr action; "option", jstr option ] @ extra))

/// The files a request pins, at origin/main: (path, sha256, field, value of that field as JSON, a field to display).
let pinsOf repo (request: JsonNode) =
    items request "pins"
    |> List.map (fun pin ->
        let path = str pin "path"
        let bytes = atMain repo path
        let field = str pin "field"
        let value = if field = "" then "" else (match (parse bytes)[field] with null -> "" | n -> n.ToJsonString())
        path, sha256Hex bytes, field, value, str pin "show")

/// A Rehearsal 010 decision: shows exactly what it pins, then records the option with those digests. A request that
/// names requiresTag waits until that tag exists (the digest it pins changes until then).
/// Why the scope manifest at origin/main is not yet the one to approve: it must be compiled from the frozen package
/// and the ratified catalog now on main, and, once ODR-R10-02 chose the successor, that catalog must be the ratified
/// successor. None when it is current.
let scopeNotCurrent repo =
    let scope = atMain repo "docs/rehearsal-010-readiness/scope-manifest.json" |> parse
    let frozen = scope["frozen"]
    let catalogNow = sha256Hex (atMain repo (str frozen "catalog"))
    let packageDir = str frozen "package"
    let packageNow = sha256Hex (atMain repo $"{packageDir}/package-manifest.json")
    let successorChosen =
        recordedChanges (approvalsAtMain repo) |> List.filter (fun c -> str c "decision_id" = "ODR-R10-02") |> List.tryLast |> Option.exists (fun c -> str c "option" = "A")
    let ids (n: JsonNode) = items n "requirements" |> List.map (fun r -> str r "id") |> Set.ofList
    let successorIn () =
        let succ = atMain repo "docs/rehearsal-010-readiness/proposals/causa-investigation.requirement-catalog.successor.proposed.json" |> parse
        Set.isSubset (ids succ) (ids (atMain repo (str frozen "catalog") |> parse))
    if str frozen "catalogSha256" <> catalogNow then Some "the scope manifest was compiled from another catalog than the ratified one on main (the agent recompiles it)"
    elif str frozen "packageManifestSha256" <> packageNow then Some "the scope manifest was compiled from another v4.1.4 than the one on main (the agent recompiles it)"
    elif successorChosen && not (successorIn ()) then Some "ODR-R10-02 chose the successor, which is not ratified yet (ratify rehearsal-010)"
    else None

let decideR10 repo (earlier: (JsonNode * Answer) list) (request: JsonNode) =
    let tag = str request "requiresTag"
    // An answer given earlier in this run changes what a later request pins: ODR-R10-02 = A means the scope is
    // recompiled after ratification, so ODR-R10-01 waits for the next run.
    let successorChosenNow =
        earlier |> List.exists (fun (r, a) -> str r "id" = "ODR-R10-02" && (match a with Select c -> str c "option" = "A" | _ -> false))
    if tag <> "" && git repo [ "tag"; "-l"; tag ] = "" then
        say $"  Not asked yet: it needs the tag {tag} first (what it pins changes until then). It stays pending."
        Skip
    elif str request "requiresCurrentScope" = "true" && successorChosenNow then
        say "  Not asked yet: ODR-R10-02 was just answered A, so the scope is recompiled after ratification. It stays pending."
        Skip
    elif str request "requiresCurrentScope" = "true" && (scopeNotCurrent repo).IsSome then
        say $"  Not asked yet: {(scopeNotCurrent repo).Value}. It stays pending."
        Skip
    else
        let pins = pinsOf repo request
        pins |> List.iter (fun (path, sha, field, value, showKey) ->
            say $"  Pins, at origin/main: {path}"
            say $"    sha256 {sha}"
            if field <> "" then say $"    {field} = {value}"
            if showKey <> "" then (match (parse (atMain repo path))[showKey] with null -> () | n -> say $"    {showKey}: {n.ToJsonString()}"))
        match decideOption "decide" request with
        | Select change when not pins.IsEmpty ->
            let pinNodes =
                pins |> List.map (fun (path, sha, field, value, _) ->
                    obj ([ "path", jstr path; "sha256", jstr sha ] @ (if field = "" then [] else [ "field", jstr field; "value", jstr value ])))
            change.AsObject().Add("pins", jarr pinNodes)
            Select change
        | answer -> answer

let decideOne repo (p: Package) (earlier: (JsonNode * Answer) list) (request: JsonNode) =
    show request
    match str request "kind" with
    | _ when p.Name = "rehearsal-010" || p.Name = "historical-compatibility" -> decideR10 repo earlier request
    | "obligation-amendment" -> decideAmendment request
    | "scope-or-topology" -> decideTopology request
    | "scope" -> decideOption "decide" request
    | _ when p.Name = "v4.1" || p.Name.StartsWith "v4.1." -> decideOption "select" request
    | kind -> stop $"""{str request "id"}: unknown request kind '{kind}'; ask the agent to update this script"""

/// The changes to write, from the answers. Where two approved requests amend the same obligation, its combined
/// criterion is written once (v4 recordFormat); otherwise each record is written exactly as proposed.
let changesOf (answers: (JsonNode * Answer) list) : JsonNode list =
    let approvedIds = answers |> List.choose (fun (r, a) -> match a with Approve _ -> Some(str r "id") | _ -> None) |> Set.ofList
    let amendment (r: JsonNode) =
        let combined = str r "combinedWith" <> "" && approvedIds.Contains(str r "combinedWith")
        str r "obligation_id",
        obj [ "obligation_id", jstr (str r "obligation_id")
              "action", jstr "amend"
              "criterion", jstr (if combined then str r "combinedCriterion" else str r "criterion")
              "rationale", jstr (str r "rationale") ]
    let amendments =
        answers
        |> List.collect (fun (_, a) -> match a with Approve records -> records |> List.map amendment | _ -> [])
        |> List.distinctBy fst
        |> List.map snd
    let decisions =
        answers |> List.choose (fun (r, a) ->
            match a with
            | Decline -> Some(obj [ "decision_id", jstr (str r "id"); "action", jstr "decline" ])
            | Select change -> Some change
            | _ -> None)
    amendments @ decisions

/// Whether a recorded decision pinned a file whose digest (or pinned field) at origin/main is no longer what it approved.
let staleRecord repo (change: JsonNode) =
    items change "pins"
    |> List.exists (fun pin ->
        let path = str pin "path"
        let bytes = try Some(atMain repo path) with Stop _ -> None
        match bytes with
        | None -> true
        | Some b when str pin "field" <> "" -> (match (parse b)[str pin "field"] with null -> "" | n -> n.ToJsonString()) <> str pin "value"
        | Some b -> sha256Hex b <> str pin "sha256")

/// The last change of the approvals file before Rehearsal 010 (APR-V412-OWNER-3). Every later change is a v4.1.4 or
/// Rehearsal 010 record, and must be one GitHub verifies as committed and signed by you.
let approvalsBaseline = "55f2467f5ff7f39f8216d2ba291dbeb58b506a5f"

/// Whether GitHub verifies `commit` as committed and signed by you (verification valid, committer you, not web-flow).
/// Without commit signing (your decision of 2026-11-04), "yours" is GitHub's record that you committed and authored it.
/// With --require-signatures, it must also be a valid signature (launch-preflight verifies the key itself).
let signedByYou repo (commit: string) =
    if fsi.CommandLineArgs |> Array.contains "--require-signatures" then
        let out = try run repo "gh" [ "api"; $"repos/{githubRepo}/commits/{commit}"; "--jq"; "[.commit.verification.verified, .commit.verification.reason, .committer.login] | @json" ] with Stop _ -> ""
        out = $"[true,\"valid\",\"{operatorId}\"]"
    else
        let out = try run repo "gh" [ "api"; $"repos/{githubRepo}/commits/{commit}"; "--jq"; "[.committer.login, .author.login] | @json" ] with Stop _ -> ""
        out = $"[\"{operatorId}\",\"{operatorId}\"]"

/// The changes of the approvals file after the baseline on origin/main's first-parent line that are neither signed by
/// you nor a merge bringing in only your signed changes from a branch (a rollback or evil merge is caught).
let unsignedApprovalChanges repo =
    let blob (c: string) = try git repo [ "rev-parse"; "--verify"; "--quiet"; $"{c}:{approvalsPath}" ] with Stop _ -> ""
    let parents (c: string) = (git repo [ "rev-list"; "--parents"; "-n"; "1"; c ]).Split(' ', StringSplitOptions.RemoveEmptyEntries) |> Array.skip 1 |> List.ofArray
    let rec changes (range: string) =
        (git repo [ "rev-list"; "--reverse"; "--first-parent"; range ]).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
        |> List.filter (fun c -> blob c <> (match parents c with p :: _ -> blob p | [] -> ""))
    and acceptable (c: string) =
        signedByYou repo c
        || (match parents c with
            | [ p1; p2 ] ->
                let mb = git repo [ "merge-base"; p1; p2 ]
                blob c = blob p2 && blob p1 = blob mb && (changes $"{mb}..{p2}" |> List.forall acceptable)
            | _ -> false)
    changes $"{approvalsBaseline}..origin/main" |> List.filter (acceptable >> not)

/// Refuses to build on an approvals file that holds a change you did not sign (v4.1.4 and Rehearsal 010 only).
let requireSignedApprovals repo (p: Package) =
    if p.Name = "v4.1.4" || p.Name = "rehearsal-010" || p.Name = "historical-compatibility" then
        match unsignedApprovalChanges repo with
        | [] -> ()
        | unsigned ->
            stop $"""{approvalsPath} was changed after {approvalsBaseline.Substring(0, 12)} by commit(s) GitHub does not verify as committed and signed by you: {String.Join(", ", unsigned |> List.map (fun c -> c.Substring(0, 12)))}. Their records are not yours: revert them before recording (and turn on commit signing, OWNER-APPROVAL-EXECUTION step 0)"""

let decide repo dryRun (p: Package) =
    prepare repo
    requireSignedApprovals repo p
    let approvals = approvalsAtMain repo
    let recorded = recordedChanges approvals
    let open' =
        requestsOf repo p
        |> List.filter (fun r -> str r "kind" <> "package-approval")
        |> List.filter (fun r ->
            let done' = isRecorded recorded r
            // A Rehearsal 010 decision whose pinned file has changed since it was recorded is asked again; the
            // later record supersedes it (the lock and launch-preflight read the latest).
            let stale =
                done' && p.Name = "rehearsal-010"
                && (recorded |> List.filter (fun c -> str c "decision_id" = str r "id") |> List.tryLast
                    |> Option.exists (fun c -> staleRecord repo c))
            if stale then
                say $"""{str r "id"}: recorded, but a file it approved has changed since; asked again. What changed:"""
                recorded |> List.filter (fun c -> str c "decision_id" = str r "id") |> List.tryLast
                |> Option.iter (fun c -> items c "pins" |> List.iter (fun pin ->
                    let path = str pin "path"
                    let now = try sha256Hex (atMain repo path) with Stop _ -> "(missing)"
                    if now <> str pin "sha256" || str pin "field" <> "" then
                        say $"""    {path}: approved sha256 {str pin "sha256"}, now {now}"""
                        if str pin "field" <> "" then say $"""      {str pin "field"} approved as {str pin "value"}"""
                        let changes = try git repo [ "log"; "--oneline"; "-5"; "origin/main"; "--"; path ] with Stop _ -> ""
                        changes.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.iter (fun l -> say $"      changed by {l}")))
            elif done' then say $"""{str r "id"}: already recorded; not asked again."""
            not done' || stale)
    if open'.IsEmpty then (say $"No open decision request in {p.Name}."; None) else
    say $"{open'.Length} open request(s) in {p.Name}. Each answer is yours; skip leaves a request pending."
    let answers = open' |> List.fold (fun acc r -> acc @ [ r, decideOne repo p acc r ]) []
    let changes = changesOf answers
    rule ()
    if changes.IsEmpty then (say "Nothing to record: every request was skipped."; None) else
    let approvalId = nextApprovalId approvals p.ApprovalPrefix
    say $"Summary: operator approval {approvalId} by {operatorId} with {changes.Length} change(s):"
    answers |> List.iter (fun (r, a) ->
        let text = match a with Approve _ -> "approve the amendment(s) as proposed" | Decline -> "decline" | Select c -> (c.ToJsonString()) | Skip -> "skipped (stays pending)"
        say $"""  {str r "id"}: {text}""")
    if choose "Type 'record' to write these as your decisions, or 'abort'" [ "record"; "abort" ] = "abort" then (say "Aborted; nothing was written."; None) else
    let ids = answers |> List.filter (fun (_, a) -> a <> Skip) |> List.map (fst >> fun r -> str r "id")
    let title = $"""Owner decisions {String.Join(", ", ids)} ({approvalId})"""
    let body =
        $"Operator approval {approvalId} in {approvalsPath}, written and committed by the owner.\n\n" +
        (if p.Name = "rehearsal-010" then "Next (agent): apply these decisions as OWNER-APPROVAL-EXECUTION.md orders them (the lock pins exactly the recorded digests).\n"
         elif p.Name = "historical-compatibility" then $"Next (agent): act on these decisions as {p.Dir}/OWNER-DECISIONS-REQUIRED.md describes (for OD-HC-01 A: compile the successor package from SUCCESSOR-CONTRACT.md).\n"
         elif p.Name = "v4" then "Next (agent): apply the approved amendments in the curation's obligationAmendments, re-pin the approvals commit, recompile, and run a fresh independent review if the plan changed."
         else $"Next (agent): apply these decisions in {p.Dir} (FREEZE.md step 2), recompile {p.Name}, and run a fresh independent review.")
    commitAndPropose repo dryRun $"owner/{p.Name}-decisions-{stamp ()}" title body (render (withApproval approvals approvalId changes))

// ---- approve ---------------------------------------------------------------------------------------------------------

let decisionsOnly (p: Package) =
    if p.Tag = "" then stop $"{p.Name} has decisions only (decide {p.Name}); it has no package approval or tag"

let approve repo dryRun (p: Package) =
    decisionsOnly p
    prepare repo
    requireSignedApprovals repo p
    let approvals = approvalsAtMain repo
    let pending =
        requestsOf repo p
        |> List.filter (fun r -> str r "kind" <> "package-approval" && not (isRecorded (recordedChanges approvals) r))
        |> List.map (fun r -> str r "id")
    if not pending.IsEmpty then say $"""Note: still pending in {p.Name}: {String.Join(", ", pending)}."""
    // v4.1.1: a requirement conflict must be decided before the package that depends on it is approved.
    let conflicts = requestsOf repo p |> List.filter (fun r -> str r "kind" = "requirement-conflict" && not (isRecorded (recordedChanges approvals) r)) |> List.map (fun r -> str r "id")
    if p.Name = "v4.1.1" && not conflicts.IsEmpty then stop $"""decide {String.Join(", ", conflicts)} first (owner-freeze.fsx decide v4.1.1): the package approval depends on it"""
    // v4.1.2: the package applies its decisions (ODR-V412-01..03, -05), so each must be recorded, and the package recompiled
    // from them, before it can be approved.
    if (p.Name = "v4.1.2" || p.Name = "v4.1.4") && not pending.IsEmpty then stop $"""decide {String.Join(", ", pending)} first (owner-freeze.fsx decide {p.Name}); the agent then compiles {p.Name} from your records"""
    rule ()
    let change, shown =
        if p.Name = "v4" then
            let manifest = atMain repo $"{p.Dir}/freeze-manifest.json" |> parse
            say $"""v4 freeze-manifest on origin/main: status {str manifest "status"}"""
            items manifest "freezeGate" |> List.iter (fun g -> say $"""  {str g "id"} {str g "status"}  {str g "condition"}""")
            let digest = str manifest["verificationPlan"] "contentDigest"
            let review = atMain repo $"{p.Dir}/analysis/independent-review.json" |> sha256Hex
            let tests = atMain repo $"{p.Dir}/analysis/compiler-test-record.json" |> sha256Hex
            obj [ "decision_id", jstr "ODR-V4-01"; "action", jstr "approve-package"; "package", jstr p.Dir
                  "plan_content_digest", jstr digest; "review_sha256", jstr review; "test_record_sha256", jstr tests ],
            [ $"verification plan content digest {digest}"; $"independent-review.json sha256 {review}"; $"compiler-test-record.json sha256 {tests}" ]
        else
            let manifest = atMain repo $"{p.Dir}/package-manifest.json" |> sha256Hex
            let review = atMain repo $"{p.Dir}/analysis/independent-review.json" |> sha256Hex
            obj [ "decision_id", jstr p.PackageApprovalId; "action", jstr "approve-package"; "package", jstr p.Dir
                  "manifest_sha256", jstr manifest; "review_sha256", jstr review ],
            [ $"package-manifest.json sha256 {manifest}"; $"independent-review.json sha256 {review}" ]
    // A record counts only while its digests match the package (FG-05); one naming an earlier state is stale, and the
    // package is approved again by a new record beside it.
    let earlier = recordedChanges approvals |> List.filter (fun c -> str c "decision_id" = p.PackageApprovalId)
    let matchesChange (c: JsonNode) = change.AsObject() |> Seq.forall (fun kv -> str c kv.Key = str change kv.Key)
    if earlier |> List.exists matchesChange then (say $"{p.PackageApprovalId} is already recorded on main for the package as it now stands; nothing to do."; None) else
    if not earlier.IsEmpty then
        say $"Note: {p.PackageApprovalId} was recorded earlier for other digests. That record no longer matches the package, so approving records it again."
    say $"""You are approving {p.Dir} as on origin/main ({git repo [ "rev-parse"; "--short"; "origin/main" ]}), bound to:"""
    shown |> List.iter (fun s -> say $"  {s}")
    say $"Read {p.Dir}/FREEZE.md and independent-review.md first. If you want changes instead, do not approve here."
    if choose $"Type 'approve' to record {p.PackageApprovalId}, or 'abort'" [ "approve"; "abort" ] = "abort" then (say "Aborted; nothing was written."; None) else
    let approvalId = nextApprovalId approvals p.ApprovalPrefix
    let title = $"Owner approval {p.PackageApprovalId}: {p.Dir} ({approvalId})"
    let body =
        $"Operator approval {approvalId} in {approvalsPath}, written and committed by the owner. It binds the approval to:\n" +
        String.Join("\n", shown |> List.map (sprintf "- %s")) +
        (if p.Name = "v4" then "\n\nNext (agent): after this merges, set analysis/freeze-input.json freezeCommit to the merge commit and recompile until the status reads READY-FOR-OWNER-FREEZE; then the owner runs `owner-freeze.fsx tag v4`." else $"\n\nNext: after this merges, the owner runs `owner-freeze.fsx tag {p.Name} --commit <merge commit>`.")
    commitAndPropose repo dryRun $"owner/{p.Name}-approval-{stamp ()}" title body (render (withApproval approvals approvalId [ change ]))

// ---- tag -------------------------------------------------------------------------------------------------------------

let tag repo dryRun (p: Package) (commitArg: string option) =
    decisionsOnly p
    prepare repo
    if git repo [ "tag"; "-l"; p.Tag ] <> "" then stop $"tag {p.Tag} already exists"
    let commit =
        if p.Name = "v4" then
            let manifest = atMain repo $"{p.Dir}/freeze-manifest.json" |> parse
            if str manifest "status" <> "READY-FOR-OWNER-FREEZE" then stop $"""v4 status on origin/main is {str manifest "status"}, not READY-FOR-OWNER-FREEZE; the agent's recompile step is not done"""
            let freezeCommit = str (atMain repo $"{p.Dir}/analysis/freeze-input.json" |> parse) "freezeCommit"
            if freezeCommit = "" then stop "analysis/freeze-input.json has no freezeCommit"
            // The tag goes on the first commit on main whose freeze manifest reads READY-FOR-OWNER-FREEZE (the merge that
            // recorded freezeCommit), because AG-13 reads the manifest at the tag; freezeCommit still carries the CANDIDATE one.
            let readyAt (commit: string) = str (atCommit repo commit $"{p.Dir}/freeze-manifest.json" |> parse) "status" = "READY-FOR-OWNER-FREEZE"
            let firstReady =
                (git repo [ "log"; "--first-parent"; "--format=%H"; "origin/main"; "--"; $"{p.Dir}/freeze-manifest.json" ]).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                |> List.ofArray
                |> List.takeWhile readyAt
                |> List.tryLast
                |> Option.defaultWith (fun () -> stop "no commit on main carries a READY-FOR-OWNER-FREEZE manifest")
            if not (gitOk repo [ "merge-base"; "--is-ancestor"; freezeCommit; firstReady ]) then stop $"freezeCommit {freezeCommit} is not an ancestor of {firstReady}"
            firstReady
        else commitArg |> Option.defaultWith (fun () -> stop $"give --commit <merge commit of the pull request that carries {p.PackageApprovalId}>")
    let full = git repo [ "rev-parse"; $"{commit}^{{commit}}" ]
    if not (gitOk repo [ "merge-base"; "--is-ancestor"; full; "origin/main" ]) then stop $"{full} is not on origin/main"
    let holds =
        recordedChanges (atCommit repo full approvalsPath |> parse)
        |> List.exists (fun c -> str c "decision_id" = p.PackageApprovalId && str c "action" = "approve-package")
    if not holds then stop $"the approvals file at {full} has no {p.PackageApprovalId} approve-package record"
    // v4.1: the approval counts only for the package it names. The tagged commit's manifest and review record must be
    // the ones an ODR-V41-01 record binds (v4's compiler checks this itself, FG-05).
    if p.Name <> "v4" then
        let manifest = atCommit repo full $"{p.Dir}/package-manifest.json" |> sha256Hex
        let review = atCommit repo full $"{p.Dir}/analysis/independent-review.json" |> sha256Hex
        let bound =
            recordedChanges (atCommit repo full approvalsPath |> parse)
            |> List.exists (fun c ->
                str c "decision_id" = p.PackageApprovalId && str c "action" = "approve-package"
                && str c "manifest_sha256" = manifest && str c "review_sha256" = review)
        if not bound then
            stop $"no {p.PackageApprovalId} record at {full} names this commit's package-manifest.json ({manifest}) and independent-review.json ({review}): the package changed after the approval, or --commit names another commit"
    say $"""Tag {p.Tag} on {full} ({git repo [ "log"; "-1"; "--format=%s"; full ]})."""
    if choose "Type 'tag' to create and push it, or 'abort'" [ "tag"; "abort" ] = "abort" then say "Aborted; no tag created." else
    if dryRun then say $"DRY RUN: git tag -a {p.Tag} {full}; git push origin {p.Tag}" else
    git repo [ "tag"; "-a"; p.Tag; full; "-m"; $"Indy-init compiled requirements package {p.Name}, frozen by the owner" ] |> ignore
    git repo [ "push"; "--quiet"; "origin"; p.Tag ] |> ignore
    say $"Created and pushed {p.Tag}."

// ---- freeze (v4.1.1 end to end) --------------------------------------------------------------------------------------

/// Runs a program and returns its exit code and combined output, without stopping on a non-zero exit.
let probe (dir: string) (program: string) (args: string list) : int * string =
    let info = ProcessStartInfo(program, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    args |> List.iter info.ArgumentList.Add
    use p = Process.Start info
    let out = p.StandardOutput.ReadToEndAsync()
    let err = p.StandardError.ReadToEnd()
    p.WaitForExit()
    p.ExitCode, (out.Result + err).Trim()

/// Waits until every check on the pull request has passed (gh exits 0), polling every 30 seconds for up to 90 minutes.
/// A failed check stops the run: nothing is merged.
let waitForChecks repo (number: string) =
    let started = DateTime.UtcNow
    let rec poll (lastReport: DateTime) =
        match probe repo "gh" [ "pr"; "checks"; number; "--repo"; githubRepo ] with
        | 0, _ -> say $"All checks passed on #{number}."
        | code, output when code = 8 || output.Contains "no checks reported" ->
            if DateTime.UtcNow - started > TimeSpan.FromMinutes 90.0 then stop $"checks on #{number} still pending after 90 minutes; nothing was merged"
            let report = if DateTime.UtcNow - lastReport > TimeSpan.FromMinutes 2.0 then (say $"  still waiting for checks on #{number} ({int (DateTime.UtcNow - started).TotalMinutes} min)"; DateTime.UtcNow) else lastReport
            Threading.Thread.Sleep 30000
            poll report
        | _, output -> stop $"a check failed on #{number}; nothing was merged.\n{output}"
    poll started

/// The pull request the script opened from `branch`: waits for its checks, asks you to type 'merge', merges it with a
/// merge commit (as the repository's pull requests are merged), and returns to an up-to-date main.
let mergeOwnerPr repo (branch: string) =
    let number = run repo "gh" [ "pr"; "view"; branch; "--repo"; githubRepo; "--json"; "number"; "--jq"; ".number" ]
    say $"Pull request #{number} (branch {branch}): waiting for its checks. This usually takes about 20 minutes."
    Threading.Thread.Sleep 20000
    waitForChecks repo number
    if choose $"Type 'merge' to merge #{number} into main, or 'abort'" [ "merge"; "abort" ] = "abort" then stop $"stopped before merging #{number}; run freeze v4.1.1 again to continue"
    run repo "gh" [ "pr"; "merge"; number; "--repo"; githubRepo; "--merge" ] |> ignore
    git repo [ "switch"; "--quiet"; "main" ] |> ignore
    git repo [ "pull"; "--quiet"; "--ff-only"; "origin"; "main" ] |> ignore
    git repo [ "fetch"; "--quiet"; "origin"; "main"; "--tags" ] |> ignore
    say $"Merged #{number}; main is up to date."

/// The merge commit on main that first carried the package approval binding the package as it now stands on main.
let approvalMergeCommit repo (p: Package) =
    let manifest = atMain repo $"{p.Dir}/package-manifest.json" |> sha256Hex
    let review = atMain repo $"{p.Dir}/analysis/independent-review.json" |> sha256Hex
    let binds (commit: string) =
        try
            recordedChanges (atCommit repo commit approvalsPath |> parse)
            |> List.exists (fun c ->
                str c "decision_id" = p.PackageApprovalId && str c "action" = "approve-package"
                && str c "manifest_sha256" = manifest && str c "review_sha256" = review)
        with Stop _ -> false
    (git repo [ "log"; "--first-parent"; "--format=%H"; "origin/main"; "--"; approvalsPath ]).Split('\n', StringSplitOptions.RemoveEmptyEntries)
    |> List.ofArray
    |> List.takeWhile binds
    |> List.tryLast

/// v4.1.1 from start to finish: decide ODR-V411-02, merge; approve (ODR-V411-01), merge; tag. Every decision, approval,
/// merge and the tag waits for your typed answer, exactly as the separate steps do. Run it again after a stop: a step
/// already done on main is skipped.
let freeze repo dryRun (p: Package) =
    decisionsOnly p
    if p.Name <> "v4.1.1" then stop "freeze runs the v4.1.1 steps; for v4, v4.1 and v4.1.2 use decide, approve and tag (v4.1.2 is compiled between them)"
    if dryRun then
        say "DRY RUN: step 1 (decide) as it would be asked; a real run then merges, approves, merges and tags."
        decide repo true p |> ignore
    else
    rule (); say "Step 1 of 3: decide ODR-V411-02."
    decide repo false p |> Option.iter (mergeOwnerPr repo)
    git repo [ "fetch"; "--quiet"; "origin"; "main" ] |> ignore
    match recordedChanges (approvalsAtMain repo) |> List.tryFind (fun c -> str c "decision_id" = "ODR-V411-02") |> Option.map (fun c -> str c "option") with
    | None -> stop "ODR-V411-02 is not recorded on main, so v4.1.1 cannot be approved yet; run freeze v4.1.1 again to decide it"
    | Some o when o <> "A" -> stop $"you recorded option {o} for ODR-V411-02. The package needs a change before it can be approved (FREEZE.md step 2): tell the agent, and run freeze v4.1.1 again after that change merges"
    | Some _ -> say "ODR-V411-02 is recorded on main (option A)."
    rule (); say "Step 2 of 3: approve v4.1.1 (ODR-V411-01)."
    approve repo false p |> Option.iter (mergeOwnerPr repo)
    git repo [ "fetch"; "--quiet"; "origin"; "main"; "--tags" ] |> ignore
    let commit = approvalMergeCommit repo p |> Option.defaultWith (fun () -> stop "no ODR-V411-01 approval of the package as it stands is on main; run freeze v4.1.1 again to approve it")
    rule (); say $"Step 3 of 3: tag {p.Tag} on the approval's merge commit {commit}."
    if git repo [ "tag"; "-l"; p.Tag ] <> "" then say $"{p.Tag} already exists; nothing to do." else
    tag repo false p (Some commit)
    say "v4.1.1 is decided, approved and tagged."

// ---- acceptance ----------------------------------------------------------------------------------------------------

/// conditor.acceptance.json with its approvals pin moved to `path` at `sha256`; every other byte is kept.
let repinApprovals (contract: string) (path: string) (sha256: string) =
    let pattern = Regex("\"approvals\": \\{ \"path\": \"[^\"]*\", \"sha256\": \"[0-9a-f]{64}\" \\}")
    if pattern.Matches(contract).Count <> 1 then stop $"{acceptanceContractPath} does not hold exactly one approvals pin in its usual form; re-pin it by hand"
    pattern.Replace(contract, $"\"approvals\": {{ \"path\": \"{path}\", \"sha256\": \"{sha256}\" }}")

/// The acceptance verifier's approvals file: requirement scope changes for the pinned baseline. None exist today
/// (no baseline requirement is excluded or re-prioritized), so the file holds no approval; it is bound to the baseline
/// by digest. Written once, by the owner, with the contract's approvals pin moved to it in the same commit.
let acceptance repo dryRun =
    prepare repo
    let contractText = atMain repo acceptanceContractPath |> Encoding.UTF8.GetString
    let contract = JsonNode.Parse contractText
    let baselinePath = str contract["baseline"] "path"
    let baselineSha = str contract["baseline"] "sha256"
    let actual = atMain repo baselinePath |> sha256Hex
    if actual <> baselineSha then stop $"{baselinePath} on main has sha256 {actual}, but {acceptanceContractPath} pins {baselineSha}"
    if gitOk repo [ "cat-file"; "-e"; $"origin/main:{acceptanceApprovalsPath}" ] then
        stop $"{acceptanceApprovalsPath} already exists on main; a scope change is recorded by editing it, not by this command"
    let content =
        obj [ "schema", jstr "causa.requirements-approvals/1"
              "baseline_sha256", jstr baselineSha
              "description", jstr $"Operator approvals of requirement scope changes (exclude, priority-change) for the baseline {baselinePath}, the only approvals the acceptance verifier reads. It holds none: no baseline requirement's scope has been changed. Package and decision approvals are in {approvalsPath}, which the verifier does not read. Written by the owner with tools/owner-freeze.fsx acceptance (RDP-003); an agent must not write, edit or infer a record here."
              "approvals", jarr [] ]
        |> render
    let digest = Encoding.UTF8.GetBytes content |> sha256Hex
    let repinned = repinApprovals contractText acceptanceApprovalsPath digest
    rule ()
    say $"This writes {acceptanceApprovalsPath}: no approval, bound to {baselinePath} (sha256 {baselineSha}), sha256 {digest}."
    let pinnedNow = str contract["approvals"] "path"
    say $"It moves the approvals pin in {acceptanceContractPath} from {pinnedNow} to it."
    say $"{approvalsPath} is not changed; it keeps your package and decision records."
    if not dryRun && choose "Type 'record' to write and pin this as your acceptance approvals, or 'abort'" [ "record"; "abort" ] = "abort" then (say "Aborted; nothing was written."; None) else
    let title = "Owner acceptance approvals: scope changes only (none), pinned in conditor.acceptance.json"
    let body =
        $"{acceptanceApprovalsPath} holds the requirement scope-change approvals the acceptance verifier reads. There are none, so it holds no approval and is bound to the baseline by digest. {acceptanceContractPath} now pins it (sha256 {digest}). Package and decision records stay in {approvalsPath}. Written and committed by the owner.\n\nNext: conditor acceptance preflight (or the verifier's --preflight) reads READY on these inputs."
    commitFilesAndPropose repo dryRun $"owner/acceptance-approvals-{stamp ()}" title body [ acceptanceApprovalsPath, content; acceptanceContractPath, repinned ]

// ---- ratify rehearsal-010 (ODR-R10-02) ------------------------------------------------------------------------------

let successorCatalogPath = "docs/rehearsal-010-readiness/proposals/causa-investigation.requirement-catalog.successor.proposed.json"
let ratifiedCatalogPath = "requirements/catalogs/causa-investigation.requirement-catalog.json"

/// Ratifies the successor catalog you approved in ODR-R10-02 (option A): only the exact file you approved (its sha256
/// at origin/main equals the recorded pin), and only if every ratified requirement is unchanged. The successor's
/// requirements and screens become the ratified catalog, with status "ratified".
let ratifyR10 repo dryRun =
    prepare repo
    requireSignedApprovals repo (packages |> List.find (fun p -> p.Name = "rehearsal-010"))
    let recorded = recordedChanges (approvalsAtMain repo) |> List.filter (fun c -> str c "decision_id" = "ODR-R10-02")
    match recorded |> List.tryLast with
    | None -> stop "ODR-R10-02 is not recorded: run decide rehearsal-010 first"
    | Some c when str c "option" <> "A" -> stop $"""ODR-R10-02 is recorded as option {str c "option"}, not A: there is nothing to ratify"""
    | Some c ->
        let bytes = atMain repo successorCatalogPath
        let approved = items c "pins" |> List.tryFind (fun p -> str p "path" = successorCatalogPath) |> Option.map (fun p -> str p "sha256") |> Option.defaultValue ""
        if approved <> sha256Hex bytes then
            stop $"the successor at origin/main has sha256 {sha256Hex bytes}, but ODR-R10-02 approved {approved}; nothing was written"
        let successor = parse bytes
        let ratified = atMain repo ratifiedCatalogPath |> parse
        let byId (n: JsonNode) = items n "requirements" |> List.map (fun r -> str r "id", r.ToJsonString()) |> Map.ofList
        let before, after = byId ratified, byId successor
        let changed = before |> Map.toList |> List.filter (fun (id, text) -> after.TryFind id <> Some text) |> List.map fst
        if not changed.IsEmpty then stop $"""the successor changes or drops ratified requirement(s) {String.Join(", ", changed)}; nothing was written"""
        let added = after |> Map.toList |> List.map fst |> List.filter (before.ContainsKey >> not)
        rule ()
        say $"""Ratify: {before.Count} ratified requirement(s) unchanged; {added.Length} added ({String.Join(", ", added)}); the successor's screens and facts become the ratified catalog's."""
        let out = successor.DeepClone()
        out.AsObject()["status"] <- jstr "ratified"
        if not dryRun && choose "Type 'ratify' to write the ratified catalog as yours, or 'abort'" [ "ratify"; "abort" ] = "abort" then say "Aborted; nothing was written."
        else
            let title = "Owner ratification of the CAT-CAUSA-INVESTIGATION successor (ODR-R10-02)"
            let body = $"{ratifiedCatalogPath} becomes the successor approved in ODR-R10-02 (sha256 {approved}), status ratified. Next (agent): recompile the scope manifest from the frozen v4.1.4 and this catalog; ODR-R10-01 approves it."
            commitFilesAndPropose repo dryRun $"owner/rehearsal-010-ratify-{stamp ()}" title body [ ratifiedCatalogPath, render out ] |> ignore

// ---- main --------------------------------------------------------------------------------------------------------------

let argv = fsi.CommandLineArgs |> Array.toList |> List.tail
let flag name = List.contains name argv
let option name = argv |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let repo = option "--repo" |> Option.defaultValue (Directory.GetCurrentDirectory()) |> Path.GetFullPath
let packageNamed name = packages |> List.tryFind (fun p -> p.Name = name) |> Option.defaultWith (fun () -> stop "the package must be v4, v4.1, v4.1.1, v4.1.2, v4.1.4, rehearsal-010 or historical-compatibility (historical-compatibility needs PR #120 merged and your clone on the new main)")

/// RDP-003: a record or tag is the owner's own act. Refuses to write when an agent runs the script or nobody can type.
let ownerAtTerminal () =
    if not (String.IsNullOrEmpty(Environment.GetEnvironmentVariable "CLAUDECODE")) then
        stop "an agent is running this script (CLAUDECODE is set); only the owner records or tags (RDP-003). status and --dry-run are allowed"
    if Console.IsInputRedirected then
        stop "input is not a terminal; run this yourself and type your answers. status and --dry-run are allowed"

let writes = not (flag "--dry-run")

let exitCode =
    try
        if writes && argv |> List.exists (fun a -> List.contains a [ "decide"; "approve"; "tag"; "freeze"; "acceptance"; "ratify" ]) then ownerAtTerminal ()
        match argv |> List.filter (fun a -> not (a.StartsWith "--")) |> List.except (option "--repo" |> Option.toList) |> List.except (option "--commit" |> Option.toList) with
        | [ "status" ] -> status repo; 0
        | [ "acceptance" ] -> acceptance repo (flag "--dry-run") |> ignore; 0
        | [ "ratify"; "rehearsal-010" ] -> ratifyR10 repo (flag "--dry-run"); 0
        | [ "decide"; name ] -> decide repo (flag "--dry-run") (packageNamed name) |> ignore; 0
        | [ "approve"; name ] -> approve repo (flag "--dry-run") (packageNamed name) |> ignore; 0
        | [ "freeze"; name ] -> freeze repo (flag "--dry-run") (packageNamed name); 0
        | [ "tag"; name ] -> tag repo (flag "--dry-run") (packageNamed name) (option "--commit"); 0
        | _ -> say "usage: dotnet fsi Indy-init/tools/owner-freeze.fsx -- status | decide v4|v4.1|v4.1.1|v4.1.2|v4.1.4|rehearsal-010|historical-compatibility | approve v4|v4.1|v4.1.1|v4.1.2|v4.1.4 | tag v4|v4.1|v4.1.1|v4.1.2|v4.1.4 [--commit SHA] | freeze v4.1.1 | ratify rehearsal-010 | acceptance [--repo DIR] [--dry-run]"; 2
    with Stop message -> eprintfn "stopped: %s" message; 1

exit exitCode
