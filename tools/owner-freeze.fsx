// owner-freeze.fsx: the owner's steps to freeze Indy-init's compiled requirements packages v4 and v4.1
// (requirements-compiled/v4/FREEZE.md and requirements-compiled/v4.1/FREEZE.md).
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
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4 --repo Indy-init        # after freezeCommit is set and the status reads READY-FOR-OWNER-FREEZE
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- decide v4.1 --repo Indy-init   # ODR-V41-02..08 (after v4 is frozen)
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- approve v4.1 --repo Indy-init  # ODR-V41-01
//   dotnet fsi Indy-init/tools/owner-freeze.fsx -- tag v4.1 --repo Indy-init --commit <merge commit of the ODR-V41-01 pull request>
//
// Options: --repo DIR (default: current directory), --dry-run (print what would be written; no commit, no push).
//
// Only the owner records (RDP-003): decide, approve and tag refuse to write when run by an agent (CLAUDECODE is set) or
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

// ---- configuration -------------------------------------------------------------------------------------------------

let operatorId = "kemiller2002"
let approvalsPath = "kickoff/rehearsal-007/approvals.causa.json"
let githubRepo = "kemiller2002/Indy-init"

type Package =
    { Name: string                 // "v4" or "v4.1"
      Dir: string                  // requirements-compiled/<name>
      ApprovalPrefix: string       // APR-V4-OWNER / APR-V41-OWNER
      PackageApprovalId: string    // ODR-V4-01 / ODR-V41-01
      Tag: string }

let packages =
    [ { Name = "v4"; Dir = "requirements-compiled/v4"; ApprovalPrefix = "APR-V4-OWNER"; PackageApprovalId = "ODR-V4-01"; Tag = "indy-init-requirements-v4.0.0" }
      { Name = "v4.1"; Dir = "requirements-compiled/v4.1"; ApprovalPrefix = "APR-V41-OWNER"; PackageApprovalId = "ODR-V41-01"; Tag = "indy-init-requirements-v4.1.0" } ]

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

/// Writes the approvals file on a new branch from origin/main, commits as you, pushes and opens a pull request.
let commitAndPropose (repo: string) (dryRun: bool) (branch: string) (title: string) (body: string) (content: string) =
    if dryRun then
        rule (); say $"DRY RUN: {approvalsPath} would become:"; say content
        say $"DRY RUN: commit \"{title}\" on branch {branch}; nothing was written, committed or pushed."
    else
        git repo [ "switch"; "--quiet"; "-c"; branch; "origin/main" ] |> ignore
        File.WriteAllText(Path.Combine(repo, approvalsPath), content)
        git repo [ "add"; approvalsPath ] |> ignore
        git repo [ "commit"; "--quiet"; "-m"; title; "-m"; body ] |> ignore
        let message = git repo [ "log"; "-1"; "--format=%B" ]
        if message.Contains "Claude-Session:" || message.Contains "Co-Authored-By: Claude" then
            stop "the commit message carries an agent trailer (a git hook may have added it); the compiler would not count these records. The commit was not pushed."
        git repo [ "push"; "--quiet"; "-u"; "origin"; branch ] |> ignore
        let url = try run repo "gh" [ "pr"; "create"; "--repo"; githubRepo; "--base"; "main"; "--head"; branch; "--title"; title; "--body"; body ] with Stop e -> $"(gh could not open the pull request: {e}) open it at https://github.com/{githubRepo}/compare/main...{branch}"
        say $"Committed as you on {branch} and pushed. Pull request: {url}"
        say "Review the diff there and merge it yourself."

// ---- status ----------------------------------------------------------------------------------------------------------

let requestsOf repo (p: Package) = items (atMain repo $"{p.Dir}/proposals/owner-decision-requests.json" |> parse) "requests"

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
    elif parts |> List.forall (fun p -> Regex.IsMatch(p, @"^[^@\s]+@[0-9][^@\s]*$")) then None
    else Some "Each entry must be id@version, for example Name.Of.Package@1.2.3 (comma-separated)."

let decideOption (action: string) (request: JsonNode) =
    let id = str request "id"
    match choose "  Option, or skip (leave pending)" (letters request @ [ "skip" ]) with
    | "skip" -> Skip
    | option ->
        let extra =
            match id with
            | "ODR-V41-06" -> [ ("organizer_confirmation", jstr (askText "  Who confirmed, and when" (fun s -> if s = "" then Some "This record needs it." else None))) ]
            | "ODR-V41-07" ->
                let packages = askText "  Exact packages to lock, as id@version, comma-separated" packageList
                [ ("packages", jarr (packages.Split(',', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) |> Seq.map jstr |> List.ofSeq)) ]
            | _ -> []
        Select(obj ([ "decision_id", jstr id; "action", jstr action; "option", jstr option ] @ extra))

let decideOne (p: Package) (request: JsonNode) =
    show request
    match str request "kind" with
    | "obligation-amendment" -> decideAmendment request
    | "scope-or-topology" -> decideTopology request
    | "scope" -> decideOption "decide" request
    | _ when p.Name = "v4.1" -> decideOption "select" request
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

let decide repo dryRun (p: Package) =
    prepare repo
    let approvals = approvalsAtMain repo
    let recorded = recordedChanges approvals
    let open' =
        requestsOf repo p
        |> List.filter (fun r -> str r "kind" <> "package-approval")
        |> List.filter (fun r ->
            let done' = isRecorded recorded r
            if done' then say $"""{str r "id"}: already recorded; not asked again."""
            not done')
    if open'.IsEmpty then say $"No open decision request in {p.Name}." else
    say $"{open'.Length} open request(s) in {p.Name}. Each answer is yours; skip leaves a request pending."
    let answers = open' |> List.map (fun r -> r, decideOne p r)
    let changes = changesOf answers
    rule ()
    if changes.IsEmpty then say "Nothing to record: every request was skipped." else
    let approvalId = nextApprovalId approvals p.ApprovalPrefix
    say $"Summary: operator approval {approvalId} by {operatorId} with {changes.Length} change(s):"
    answers |> List.iter (fun (r, a) ->
        let text = match a with Approve _ -> "approve the amendment(s) as proposed" | Decline -> "decline" | Select c -> (c.ToJsonString()) | Skip -> "skipped (stays pending)"
        say $"""  {str r "id"}: {text}""")
    if choose "Type 'record' to write these as your decisions, or 'abort'" [ "record"; "abort" ] = "abort" then say "Aborted; nothing was written." else
    let ids = answers |> List.filter (fun (_, a) -> a <> Skip) |> List.map (fst >> fun r -> str r "id")
    let title = $"""Owner decisions {String.Join(", ", ids)} ({approvalId})"""
    let body =
        $"Operator approval {approvalId} in {approvalsPath}, written and committed by the owner.\n\n" +
        "Next (agent): apply the approved amendments in the curation's obligationAmendments, re-pin the approvals commit, recompile, and run a fresh independent review if the plan changed."
    commitAndPropose repo dryRun $"owner/{p.Name}-decisions-{stamp ()}" title body (render (withApproval approvals approvalId changes))

// ---- approve ---------------------------------------------------------------------------------------------------------

let approve repo dryRun (p: Package) =
    prepare repo
    let approvals = approvalsAtMain repo
    if recordedChanges approvals |> List.exists (fun c -> str c "decision_id" = p.PackageApprovalId) then
        say $"{p.PackageApprovalId} is already recorded on main."
    else
    let pending =
        requestsOf repo p
        |> List.filter (fun r -> str r "kind" <> "package-approval" && not (isRecorded (recordedChanges approvals) r))
        |> List.map (fun r -> str r "id")
    if not pending.IsEmpty then say $"""Note: still pending in {p.Name}: {String.Join(", ", pending)}."""
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
            obj [ "decision_id", jstr "ODR-V41-01"; "action", jstr "approve-package"; "package", jstr p.Dir
                  "manifest_sha256", jstr manifest; "review_sha256", jstr review ],
            [ $"package-manifest.json sha256 {manifest}"; $"independent-review.json sha256 {review}" ]
    say $"""You are approving {p.Dir} as on origin/main ({git repo [ "rev-parse"; "--short"; "origin/main" ]}), bound to:"""
    shown |> List.iter (fun s -> say $"  {s}")
    say $"Read {p.Dir}/FREEZE.md and independent-review.md first. If you want changes instead, do not approve here."
    if choose $"Type 'approve' to record {p.PackageApprovalId}, or 'abort'" [ "approve"; "abort" ] = "abort" then say "Aborted; nothing was written." else
    let approvalId = nextApprovalId approvals p.ApprovalPrefix
    let title = $"Owner approval {p.PackageApprovalId}: {p.Dir} ({approvalId})"
    let body =
        $"Operator approval {approvalId} in {approvalsPath}, written and committed by the owner. It binds the approval to:\n" +
        String.Join("\n", shown |> List.map (sprintf "- %s")) +
        (if p.Name = "v4" then "\n\nNext (agent): after this merges, set analysis/freeze-input.json freezeCommit to the merge commit and recompile until the status reads READY-FOR-OWNER-FREEZE; then the owner runs `owner-freeze.fsx tag v4`." else "\n\nNext: after this merges, the owner runs `owner-freeze.fsx tag v4.1 --commit <merge commit>`.")
    commitAndPropose repo dryRun $"owner/{p.Name}-approval-{stamp ()}" title body (render (withApproval approvals approvalId [ change ]))

// ---- tag -------------------------------------------------------------------------------------------------------------

let tag repo dryRun (p: Package) (commitArg: string option) =
    prepare repo
    if git repo [ "tag"; "-l"; p.Tag ] <> "" then stop $"tag {p.Tag} already exists"
    let commit =
        if p.Name = "v4" then
            let manifest = atMain repo $"{p.Dir}/freeze-manifest.json" |> parse
            if str manifest "status" <> "READY-FOR-OWNER-FREEZE" then stop $"""v4 status on origin/main is {str manifest "status"}, not READY-FOR-OWNER-FREEZE; the agent's recompile step is not done"""
            let c = str (atMain repo $"{p.Dir}/analysis/freeze-input.json" |> parse) "freezeCommit"
            if c = "" then stop "analysis/freeze-input.json has no freezeCommit"
            c
        else commitArg |> Option.defaultWith (fun () -> stop "give --commit <merge commit of the pull request that carries ODR-V41-01>")
    let full = git repo [ "rev-parse"; $"{commit}^{{commit}}" ]
    if not (gitOk repo [ "merge-base"; "--is-ancestor"; full; "origin/main" ]) then stop $"{full} is not on origin/main"
    let holds =
        recordedChanges (atCommit repo full approvalsPath |> parse)
        |> List.exists (fun c -> str c "decision_id" = p.PackageApprovalId && str c "action" = "approve-package")
    if not holds then stop $"the approvals file at {full} has no {p.PackageApprovalId} approve-package record"
    say $"""Tag {p.Tag} on {full} ({git repo [ "log"; "-1"; "--format=%s"; full ]})."""
    if choose "Type 'tag' to create and push it, or 'abort'" [ "tag"; "abort" ] = "abort" then say "Aborted; no tag created." else
    if dryRun then say $"DRY RUN: git tag -a {p.Tag} {full}; git push origin {p.Tag}" else
    git repo [ "tag"; "-a"; p.Tag; full; "-m"; $"Indy-init compiled requirements package {p.Name}, frozen by the owner" ] |> ignore
    git repo [ "push"; "--quiet"; "origin"; p.Tag ] |> ignore
    say $"Created and pushed {p.Tag}."

// ---- main --------------------------------------------------------------------------------------------------------------

let argv = fsi.CommandLineArgs |> Array.toList |> List.tail
let flag name = List.contains name argv
let option name = argv |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let repo = option "--repo" |> Option.defaultValue (Directory.GetCurrentDirectory()) |> Path.GetFullPath
let packageNamed name = packages |> List.tryFind (fun p -> p.Name = name) |> Option.defaultWith (fun () -> stop "the package must be v4 or v4.1")

/// RDP-003: a record or tag is the owner's own act. Refuses to write when an agent runs the script or nobody can type.
let ownerAtTerminal () =
    if not (String.IsNullOrEmpty(Environment.GetEnvironmentVariable "CLAUDECODE")) then
        stop "an agent is running this script (CLAUDECODE is set); only the owner records or tags (RDP-003). status and --dry-run are allowed"
    if Console.IsInputRedirected then
        stop "input is not a terminal; run this yourself and type your answers. status and --dry-run are allowed"

let writes = not (flag "--dry-run")

let exitCode =
    try
        if writes && argv |> List.exists (fun a -> List.contains a [ "decide"; "approve"; "tag" ]) then ownerAtTerminal ()
        match argv |> List.filter (fun a -> not (a.StartsWith "--")) |> List.except (option "--repo" |> Option.toList) |> List.except (option "--commit" |> Option.toList) with
        | [ "status" ] -> status repo; 0
        | [ "decide"; name ] -> decide repo (flag "--dry-run") (packageNamed name); 0
        | [ "approve"; name ] -> approve repo (flag "--dry-run") (packageNamed name); 0
        | [ "tag"; name ] -> tag repo (flag "--dry-run") (packageNamed name) (option "--commit"); 0
        | _ -> say "usage: dotnet fsi Indy-init/tools/owner-freeze.fsx -- status | decide v4|v4.1 | approve v4|v4.1 | tag v4|v4.1 [--commit SHA] [--repo DIR] [--dry-run]"; 2
    with Stop message -> eprintfn "stopped: %s" message; 1

exit exitCode
