// launch-preflight.fsx: may Rehearsal 010 be launched? Read-only: it checks, it never records, pins or starts anything.
//
//   dotnet fsi tools/r10/launch-preflight.fsx -- --indy-init DIR --competition URL|DIR --live-run RUN_ID [--gh PATH] [--out F]
//
// READY only when every check below passes; otherwise REFUSED with each cause. Exit 0 READY, 4 REFUSED.
//   LP-00  the Indy-init checkout is clean and is origin/main (what is judged is what is published)
//   LP-01  the owner's lock kickoff/rehearsal-010/toolchain.lock.json is committed and is not a proposal
//   LP-02  v4.1.4's tag exists on origin, the lock's tagCommit is that tag's commit, and the package manifest at the tag
//          has the lock's digest
//   LP-03  every contract digest in the lock is the digest of the file it names
//   LP-04  the scope manifest's scopeSha256 is the lock's
//   LP-05  every Rehearsal 010 owner decision and ODR-V414-01..03 is recorded by the operator in an approval committed
//          by the owner (no agent author or trailer); the recorded option is one the request can launch with; every
//          pin it recorded is the current file's digest (and the lock's, where the lock pins it)
//   LP-06  the resolved set carries Dokimos 0.11.1 or later
//   LP-07  the test repository is isolated, named, not a competition repository, and the one ODR-R10-09 approved
//   LP-08  the live-provider preflight run (--live-run, fetched with gh): the live-provider workflow, dispatched on main,
//          successful; its journey results: J1-J3 on Chromium and WebKit against api.github.com, each passed, once
//   LP-09  the adversarial suite is VALIDATED in every case it defines (docs/rehearsal-010-readiness/evidence/adversarial)
//   LP-10  the competition repository (every ref, as published) holds no T0 record and no commit beyond its initial one

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text.Json.Nodes
open System.Text.RegularExpressions

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let opt (name: string) = args |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let src = opt "--indy-init" |> Option.defaultWith (fun () -> failwith "missing --indy-init") |> Path.GetFullPath
let path (rel: string) = Path.Combine(src, rel)
let gh = opt "--gh" |> Option.defaultValue "gh"
let indyInitRepo = "kemiller2002/Indy-init"
let liveWorkflow = "Live-provider preflight (Rehearsal 010)"

let run (dir: string) (exe: string) (a: string list) =
    try
        let info = ProcessStartInfo(exe, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        a |> List.iter info.ArgumentList.Add
        use p = Process.Start info
        let out = p.StandardOutput.ReadToEnd()
        p.StandardError.ReadToEnd() |> ignore
        p.WaitForExit()
        p.ExitCode, out.Trim()
    with ex -> 127, ex.Message
let git dir a = run dir "git" a
let sha256Bytes (b: byte[]) = (SHA256.HashData b |> Convert.ToHexString).ToLowerInvariant()
let sha256 (file: string) = if File.Exists file then sha256Bytes (File.ReadAllBytes file) else ""
let json (file: string) = if File.Exists file then (try Some(JsonNode.Parse(File.ReadAllText file)) with _ -> None) else None
let str (n: JsonNode) (k: string) = match n with null -> "" | n -> (match n[k] with null -> "" | v -> v.ToString())
let node (n: JsonNode) (k: string) : JsonNode = match n with null -> null | n -> n[k]
let items (n: JsonNode) (k: string) : JsonNode list = match node n k with :? JsonArray as a -> a |> Seq.toList | _ -> []
let tempDir (name: string) = let d = Path.Combine(Path.GetTempPath(), $"launch-preflight-{name}-{Guid.NewGuid():N}") in Directory.CreateDirectory d |> ignore; d

type Check = { Id: string; Title: string; Causes: string list }
let check id title causes = { Id = id; Title = title; Causes = causes }
let fail (cond: bool) (cause: string) = if cond then [] else [ cause ]

let lockRel = "kickoff/rehearsal-010/toolchain.lock.json"
let lock = json (path lockRel) |> Option.toObj
let approvalsRel = "kickoff/rehearsal-007/approvals.causa.json"
let requestsRel = "kickoff/rehearsal-010/proposals/owner-decision-requests.json"
let operator = "kemiller2002"

let lp00 =
    let _, status = git src [ "status"; "--porcelain"; "--untracked-files=no" ]
    let _, head = git src [ "rev-parse"; "HEAD" ]
    let code, remote = git src [ "ls-remote"; "origin"; "refs/heads/main" ]
    let remoteMain = remote.Split('\t').[0]
    check "LP-00" "the Indy-init checkout is clean and is origin/main" (
        fail (status = "") "the Indy-init checkout has uncommitted changes"
        @ fail (code = 0 && remoteMain <> "") "origin/main cannot be read (git ls-remote origin)"
        @ fail (code <> 0 || remoteMain = head) $"HEAD {head} is not origin/main {remoteMain}: judge what is published")

let lp01 =
    let tracked = fst (git src [ "ls-files"; "--error-unmatch"; lockRel ]) = 0
    check "LP-01" "the owner's lock is committed" (
        fail (not (isNull lock)) $"{lockRel} does not exist (ODR-RG-04: the owner commits it)"
        @ fail (isNull lock || tracked) $"{lockRel} is not committed"
        @ fail (isNull lock || not ((str lock "status").StartsWith "PROPOSED")) $"{lockRel} is still a proposal (status PROPOSED)")

let lp02 =
    let pkg = node lock "requirementsPackage"
    let tag = (str pkg "tag").Split(' ').[0]
    let code, remote = git src [ "ls-remote"; "--tags"; "origin"; $"refs/tags/{tag}"; $"refs/tags/{tag}^{{}}" ]
    let lines = remote.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.map (fun l -> l.Split('\t'))
    let tagCommit = lines |> Array.tryFind (fun l -> l.[1].EndsWith "^{}") |> Option.orElse (Array.tryHead lines) |> Option.map (fun l -> l.[0]) |> Option.defaultValue ""
    // The manifest at the tag is the working file (same blob), whose sha256 is the lock's (LP-00: the tree is main).
    let manifestRel = $"""{str pkg "path"}/package-manifest.json"""
    let tcode, blobAtTag = git src [ "rev-parse"; "--verify"; "--quiet"; $"{tagCommit}:{manifestRel}" ]
    let _, blobNow = git src [ "hash-object"; manifestRel ]
    check "LP-02" "v4.1.4 is frozen and tagged on origin" (
        if isNull lock then [ "no lock" ] else
        fail (str pkg "packageVersion" = "4.1.4") $"""the lock pins package {str pkg "packageVersion"}, not 4.1.4"""
        @ fail (code = 0 && tagCommit <> "") $"tag {tag} does not exist on origin (ODR-V414-03, owner-freeze.fsx tag v4.1.4)"
        @ fail (tagCommit = "" || str pkg "tagCommit" = tagCommit) $"""the lock's tagCommit {str pkg "tagCommit"} is not {tag}'s commit {tagCommit}"""
        @ fail (tagCommit = "" || tcode = 0) $"the tag's commit {tagCommit} is not in this clone (git fetch --tags origin)"
        @ fail (tagCommit = "" || tcode <> 0 || (blobAtTag = blobNow && sha256 (path manifestRel) = str pkg "manifestSha256")) "the v4.1.4 package manifest at the tag differs from the lock's manifestSha256")

let pinned =
    [ "catalogVerification", "contract", "contractSha256"
      "designContract", "contract", "contractSha256"
      "deadlinePolicy", "path", "sha256"
      "journeys", "bindings", "bindingsSha256"
      "processEvidence", "rules", "rulesSha256" ]
let lp03 =
    check "LP-03" "every contract digest in the lock is its file's" (
        if isNull lock then [ "no lock" ] else
        pinned |> List.collect (fun (section, fileKey, shaKey) ->
            let s = node lock section
            let file, want = str s fileKey, str s shaKey
            if file = "" then [ $"the lock names no {section}.{fileKey}" ]
            elif want = "" then [ $"{section}.{shaKey} is not pinned" ]
            else fail (sha256 (path file) = want) $"{file}: sha256 {sha256 (path file)}, the lock pins {want}"))

let lp04 =
    let manifest = json (path (str (node lock "scope") "manifest")) |> Option.toObj
    check "LP-04" "the scope is the approved one" (
        if isNull lock then [ "no lock" ] else
        fail (not (isNull manifest)) "the lock names no scope manifest that exists"
        @ fail (isNull manifest || str manifest "scopeSha256" = str (node lock "scope") "scopeSha256") $"""the scope manifest has scopeSha256 {str manifest "scopeSha256"}, the lock pins {str (node lock "scope") "scopeSha256"}""")

/// The approvals that count: by the operator, in a commit the owner made (the first commit whose file holds the
/// approval id has no agent author and no agent trailer, the same rule the compiler applies to owner records).
let ownerApprovals =
    let approvals = json (path approvalsRel) |> Option.toObj
    let agentMade (commit: string) =
        let _, meta = git src [ "show"; "-s"; "--format=%an%n%ae%n%B"; commit ]
        Regex.IsMatch(meta, @"Claude-Session:|Co-Authored-By:\s*Claude|^Claude\b", RegexOptions.Multiline)
    items approvals "approvals"
    |> List.filter (fun a -> str (node a "authority") "id" = operator)
    |> List.filter (fun a ->
        let id = str a "id"
        let _, commits = git src [ "log"; "--reverse"; "--format=%H"; "-G"; Regex.Escape($"\"{id}\""); "--"; approvalsRel ]
        match commits.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.tryHead with
        | Some c -> not (agentMade c)
        | None -> false)
let ownerChanges = ownerApprovals |> List.collect (fun a -> items a "changes")
let recordedFor id = ownerChanges |> List.filter (fun c -> str c "decision_id" = id) |> List.tryLast
let withoutApproval (s: string) =
    try
        let n = JsonNode.Parse s
        match n with
        | :? JsonObject as o -> o.Remove "approval" |> ignore; o.ToJsonString()
        | n -> n.ToJsonString()
    with _ -> s

let lp05 =
    let requests = json (path requestsRel) |> Option.map (fun r -> items r "requests") |> Option.defaultValue []
    let lockDigests = pinned |> List.map (fun (s, f, k) -> str (node lock s) f, str (node lock s) k) |> Map.ofList
    let packageDecisions =
        ([ "ODR-V414-01"; "ODR-V414-02" ] |> List.collect (fun id -> fail (Option.isSome (recordedFor id)) $"{id} is not recorded by the owner (owner-freeze.fsx decide v4.1.4)"))
        @ fail (ownerChanges |> List.exists (fun c -> str c "decision_id" = "ODR-V414-03" && str c "action" = "approve-package")) "ODR-V414-03 (approve v4.1.4) is not recorded by the owner (owner-freeze.fsx approve v4.1.4)"
    let r10 =
        if requests.IsEmpty then [ $"{requestsRel} lists no request" ] else
        requests |> List.collect (fun r ->
            let id = str r "id"
            let launchable = match items r "launchable" with [] -> [ "A" ] | xs -> xs |> List.map string
            match recordedFor id with
            | None -> [ $"{id} is not recorded by the owner (owner-freeze.fsx decide rehearsal-010)" ]
            | Some c when not (List.contains (str c "option") launchable) ->
                [ $"""{id} is recorded as option {str c "option"} ({str c "action"}); Rehearsal 010 launches only with {String.Join(" or ", launchable)}""" ]
            | Some c ->
                items c "pins" |> List.collect (fun p ->
                    let file = str p "path"
                    let field = str p "field"
                    if field <> "" then
                        let now = json (path file) |> Option.map (fun j -> match j[field] with null -> "" | v -> v.ToJsonString()) |> Option.defaultValue ""
                        fail (now = str p "value") $"""{id} approved {file} {field} = {str p "value"}; it is now {now}"""
                    else
                        fail (sha256 (path file) = str p "sha256") $"""{id} approved {file} at sha256 {str p "sha256"}; it is now {sha256 (path file)}"""
                        @ (match lockDigests.TryFind file with
                           | Some d when d <> "" && d <> str p "sha256" -> [ $"""{id} approved {file} at {str p "sha256"}; the lock pins {d}""" ]
                           | _ -> [])))
    check "LP-05" "every owner decision is recorded by the owner, launchable, and what it approved is what is pinned" (packageDecisions @ r10)

let lp06 =
    let v = str (node (node lock "resolvedSet") "expectedTools") "dokimos"
    let atLeast (a: string) (b: string) = (Version.TryParse a |> function | true, x -> Some x | _ -> None) |> Option.exists (fun x -> x >= Version.Parse b)
    check "LP-06" "the resolved set carries Dokimos 0.11.1" (if isNull lock then [ "no lock" ] else fail (atLeast v "0.11.1") $"the resolved set's Dokimos is {v} (ODR-RG-03: release 0.11.1 and register a set carrying it)")

let lp07 =
    let t = node lock "testRepository"
    let approved = recordedFor "ODR-R10-09" |> Option.bind (fun c -> items c "pins" |> List.tryFind (fun p -> str p "field" = "testRepository")) |> Option.map (fun p -> str p "value")
    check "LP-07" "the test repository is isolated and the one approved" (
        if isNull lock then [ "no lock" ] else
        fail (str t "isolated" = "true") "testRepository.isolated is not true (ODR-R10-09)"
        @ fail (str t "owner" <> "" && not ((str t "owner").StartsWith "(")) "testRepository.owner is not named"
        @ fail (str t "repo" <> "" && not ((str t "repo").StartsWith "(")) "testRepository.repo is not named"
        @ fail (not ((str t "repo").StartsWith "indy-init-rehearsal")) "the test repository is a competition repository"
        @ (match approved with
           | None -> [ "ODR-R10-09 recorded no testRepository" ]
           | Some v -> fail (isNull t || withoutApproval v = withoutApproval (t.ToJsonString())) "the lock's testRepository is not the one ODR-R10-09 approved"))

let lp08 =
    let rows (results: JsonNode) = items results "results"
    let needed = [ for j in [ "J1"; "J2"; "J3" ] do for e in [ "chromium"; "webkit" ] -> j, e ]
    let judge (meta: JsonNode) (results: JsonNode) =
        let provider = node results "provider"
        fail (str meta "workflowName" = liveWorkflow) $"""run is workflow "{str meta "workflowName"}", not "{liveWorkflow}" """
        @ fail (str meta "headBranch" = "main") $"""run is on {str meta "headBranch"}, not main"""
        @ fail (str meta "event" = "workflow_dispatch") $"""run was triggered by {str meta "event"}, not a dispatch"""
        @ fail (str meta "conclusion" = "success") $"""run concluded {str meta "conclusion"}"""
        @ fail ((str results "candidate").StartsWith "fixture-") $"""the results are bound to {str results "candidate"}, not the fixture candidate"""
        @ fail (str provider "mode" = "github" && str provider "baseUrl" = "https://api.github.com") $"""the journeys ran against {str provider "baseUrl"} ({str provider "mode"}), not https://api.github.com (a double is never live evidence)"""
        @ (needed |> List.collect (fun (j, e) ->
            match rows results |> List.filter (fun r -> str r "journey" = j && str r "engine" = e) |> List.map (fun r -> str r "status") with
            | [ "passed" ] -> []
            | [] -> [ $"{j} did not run on {e}" ]
            | [ other ] -> [ $"{j} on {e}: {other}" ]
            | many -> [ $"""{j} on {e} is reported {many.Length} times ({String.Join(", ", many)})""" ]))
    check "LP-08" "live-provider preflight passed (the governed run: J1-J3, Chromium and WebKit, api.github.com)" (
        match opt "--live-run" with
        | None -> [ "no live-provider preflight run (--live-run RUN_ID; LIVE-PROVIDER-PREFLIGHT.md)" ]
        | Some id ->
            let code, metaText = run src gh [ "run"; "view"; id; "--repo"; indyInitRepo; "--json"; "workflowName,headBranch,event,conclusion,headSha" ]
            if code <> 0 then [ $"gh run view {id} failed: {metaText}" ] else
            let dir = tempDir "live"
            let dcode, dout = run src gh [ "run"; "download"; id; "--repo"; indyInitRepo; "--dir"; dir ]
            let file = if Directory.Exists dir then Directory.EnumerateFiles(dir, "journey-results.json", SearchOption.AllDirectories) |> Seq.tryHead else None
            match dcode, file with
            | 0, Some f -> (match json f with Some results -> judge (JsonNode.Parse metaText) results | None -> [ "the run's journey-results.json does not parse" ])
            | 0, None -> [ $"run {id} has no journey-results.json artifact" ]
            | _ -> [ $"gh run download {id} failed: {dout}" ])

let lp09 =
    let tsv = path "docs/rehearsal-010-readiness/evidence/adversarial/cases.tsv"
    let defined =
        let f = path "tools/r10/adversarial.fsx"
        if File.Exists f then Regex.Matches(File.ReadAllText f, "\\{ Id = \"(C[0-9]+)\"") |> Seq.map (fun m -> m.Groups[1].Value) |> Set.ofSeq else Set.empty
    let rows = if File.Exists tsv then File.ReadAllLines tsv |> Array.skip 1 |> Array.filter (fun l -> l.Trim() <> "") |> List.ofArray else []
    let validated = rows |> List.filter (fun l -> l.EndsWith "VALIDATED") |> List.map (fun l -> l.Split('\t').[0]) |> Set.ofList
    let missing = Set.difference defined validated
    check "LP-09" "the adversarial suite is validated in every case it defines" (
        fail (not defined.IsEmpty) "tools/r10/adversarial.fsx defines no case"
        @ fail missing.IsEmpty $"""cases not VALIDATED in the evidence: {String.Join(", ", missing)}""")

let lp10 =
    check "LP-10" "the competition repository is clean (no T0, no clock), as published" (
        match opt "--competition" with
        | None -> [ "no competition repository named (--competition URL|DIR)" ]
        | Some c ->
            let mirror = tempDir "competition"
            let code, out = git src [ "clone"; "--quiet"; "--mirror"; c; mirror ]
            if code <> 0 then [ $"the competition repository {c} cannot be read: {out}" ] else
            let _, refs = git mirror [ "for-each-ref"; "--format=%(refname)" ]
            refs.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray |> List.collect (fun r ->
                let _, count = git mirror [ "rev-list"; "--count"; r ]
                let t0 = fst (git mirror [ "cat-file"; "-e"; $"{r}:evidence/t0/t0.json" ]) = 0
                fail (not t0) $"{r} holds a T0 record: the clock has started"
                @ fail (count = "0" || count = "1") $"{r} has {count} commits; the competition repository must be empty or hold only its initial commit"))

let checks = [ lp00; lp01; lp02; lp03; lp04; lp05; lp06; lp07; lp08; lp09; lp10 ]
checks |> List.iter (fun c ->
    printfn "%s %-8s %s" c.Id (if c.Causes.IsEmpty then "PASS" else "REFUSED") c.Title
    c.Causes |> List.iter (printfn "        %s"))
let ready = checks |> List.forall (fun c -> c.Causes.IsEmpty)
opt "--out" |> Option.iter (fun out ->
    let j =
        JsonObject(
            [ Collections.Generic.KeyValuePair("schema", JsonValue.Create "indy-init.launch-preflight/1" :> JsonNode)
              Collections.Generic.KeyValuePair("verdict", JsonValue.Create(if ready then "READY" else "REFUSED") :> JsonNode)
              Collections.Generic.KeyValuePair("checks", JsonArray(checks |> List.map (fun c -> JsonObject([ Collections.Generic.KeyValuePair("id", JsonValue.Create c.Id :> JsonNode); Collections.Generic.KeyValuePair("title", JsonValue.Create c.Title :> JsonNode); Collections.Generic.KeyValuePair("causes", JsonArray(c.Causes |> List.map (fun x -> JsonValue.Create x :> JsonNode) |> Array.ofList) :> JsonNode) ]) :> JsonNode) |> Array.ofList) :> JsonNode) ])
    File.WriteAllText(out, j.ToJsonString(Text.Json.JsonSerializerOptions(WriteIndented = true)) + "\n"))
printfn "launch-preflight: %s" (if ready then "READY" else "REFUSED")
exit (if ready then 0 else 4)
