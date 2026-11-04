// launch-preflight.fsx: may Rehearsal 010 be launched? Read-only: it checks, it never records, pins or starts anything.
//
//   dotnet fsi tools/r10/launch-preflight.fsx -- --indy-init DIR [--competition DIR] [--live-evidence DIR] [--out F]
//
// READY only when every check below passes; otherwise REFUSED with each cause. Exit 0 READY, 4 REFUSED.
//   LP-01  the owner's lock kickoff/rehearsal-010/toolchain.lock.json is committed and is not a proposal
//   LP-02  v4.1.4 is tagged, the lock's tagCommit is that tag's commit, and the package digest is the lock's
//   LP-03  every contract digest in the lock is the digest of the file it names
//   LP-04  the scope manifest's scopeSha256 is the lock's
//   LP-05  every Rehearsal 010 owner decision and ODR-V414-01..03 is recorded by the owner in the decisions approvals
//          file; every pin a decision recorded is the current file's digest (and the lock's, where the lock pins it)
//   LP-06  the resolved set carries Dokimos 0.11.1 or later
//   LP-07  the test repository is isolated (not the competition repository) and named
//   LP-08  live-provider preflight evidence: J1-J3 on Chromium and WebKit against api.github.com, every P0 assertion
//          passed, bound to the fixture candidate (--live-evidence; LIVE-PROVIDER-PREFLIGHT.md)
//   LP-09  the adversarial suite is VALIDATED in every case (docs/rehearsal-010-readiness/evidence/adversarial)
//   LP-10  the competition repository is clean: no T0 record and no commit beyond its initial one (--competition)

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text.Json.Nodes

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let opt (name: string) = args |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let src = opt "--indy-init" |> Option.defaultWith (fun () -> failwith "missing --indy-init") |> Path.GetFullPath
let path (rel: string) = Path.Combine(src, rel)

let run (dir: string) (exe: string) (a: string list) =
    let info = ProcessStartInfo(exe, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    a |> List.iter info.ArgumentList.Add
    use p = Process.Start info
    let out = p.StandardOutput.ReadToEnd()
    p.StandardError.ReadToEnd() |> ignore
    p.WaitForExit()
    p.ExitCode, out.Trim()
let git dir a = run dir "git" a
let sha256 (file: string) = if File.Exists file then (SHA256.HashData(File.ReadAllBytes file) |> Convert.ToHexString).ToLowerInvariant() else ""
let json (file: string) = if File.Exists file then Some(JsonNode.Parse(File.ReadAllText file)) else None
let str (n: JsonNode) (k: string) = match n with null -> "" | n -> (match n[k] with null -> "" | v -> v.ToString())
let node (n: JsonNode) (k: string) : JsonNode = match n with null -> null | n -> n[k]
let items (n: JsonNode) (k: string) : JsonNode list = match node n k with :? JsonArray as a -> a |> Seq.toList | _ -> []

type Check = { Id: string; Title: string; Causes: string list }
let check id title causes = { Id = id; Title = title; Causes = causes }
let fail (cond: bool) (cause: string) = if cond then [] else [ cause ]

let lockRel = "kickoff/rehearsal-010/toolchain.lock.json"
let lock = json (path lockRel) |> Option.toObj
let approvalsRel = "kickoff/rehearsal-007/approvals.causa.json"
let requestsRel = "kickoff/rehearsal-010/proposals/owner-decision-requests.json"
let operator = "kemiller2002"

let lp01 =
    let tracked = fst (git src [ "ls-files"; "--error-unmatch"; lockRel ]) = 0
    check "LP-01" "the owner's lock is committed" (
        fail (not (isNull lock)) $"{lockRel} does not exist (ODR-RG-04: the owner commits it)"
        @ fail (isNull lock || tracked) $"{lockRel} is not committed"
        @ fail (isNull lock || not ((str lock "status").StartsWith "PROPOSED")) $"{lockRel} is still a proposal (status PROPOSED)")

let lp02 =
    let pkg = node lock "requirementsPackage"
    let tag = (str pkg "tag").Split(' ').[0]
    let code, tagCommit = git src [ "rev-parse"; "--verify"; "--quiet"; tag + "^{commit}" ]
    check "LP-02" "v4.1.4 is frozen and tagged" (
        if isNull lock then [ "no lock" ] else
        fail (str pkg "packageVersion" = "4.1.4") $"""the lock pins package {str pkg "packageVersion"}, not 4.1.4"""
        @ fail (code = 0) $"tag {tag} does not exist (ODR-V414-03, owner-freeze.fsx tag v4.1.4)"
        @ fail (code <> 0 || str pkg "tagCommit" = tagCommit) $"""the lock's tagCommit {str pkg "tagCommit"} is not {tag}'s commit {tagCommit}"""
        @ fail (sha256 (path (Path.Combine(str pkg "path", "package-manifest.json"))) = str pkg "manifestSha256") "the v4.1.4 package manifest differs from the lock's manifestSha256")

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

let lp05 =
    let approvals = json (path approvalsRel) |> Option.toObj
    let changes =
        items approvals "approvals"
        |> List.filter (fun a -> str (node a "authority") "id" = operator)
        |> List.collect (fun a -> items a "changes")
    let requests = json (path requestsRel) |> Option.map (fun r -> items r "requests") |> Option.defaultValue []
    let recordedFor id = changes |> List.filter (fun c -> str c "decision_id" = id) |> List.tryLast
    let lockDigests = pinned |> List.map (fun (s, f, k) -> str (node lock s) f, str (node lock s) k) |> Map.ofList
    let packageDecisions =
        ([ "ODR-V414-01"; "ODR-V414-02" ] |> List.collect (fun id -> fail (Option.isSome (recordedFor id)) $"{id} is not recorded (owner-freeze.fsx decide v4.1.4)"))
        @ fail (changes |> List.exists (fun c -> str c "decision_id" = "ODR-V414-03" && str c "action" = "approve-package")) "ODR-V414-03 (approve v4.1.4) is not recorded (owner-freeze.fsx approve v4.1.4)"
    let r10 =
        if requests.IsEmpty then [ $"{requestsRel} lists no request" ] else
        requests |> List.collect (fun r ->
            let id = str r "id"
            match recordedFor id with
            | None -> [ $"{id} is not recorded (owner-freeze.fsx decide rehearsal-010)" ]
            | Some c when str c "action" = "decline" -> [ $"{id} was declined" ]
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
    check "LP-05" "every owner decision is recorded, and what it approved is what is pinned" (packageDecisions @ r10)

let lp06 =
    let v = str (node (node lock "resolvedSet") "expectedTools") "dokimos"
    let atLeast (a: string) (b: string) = (Version.TryParse a |> function | true, x -> Some x | _ -> None) |> Option.exists (fun x -> x >= Version.Parse b)
    check "LP-06" "the resolved set carries Dokimos 0.11.1" (if isNull lock then [ "no lock" ] else fail (atLeast v "0.11.1") $"the resolved set's Dokimos is {v} (ODR-RG-03: release 0.11.1 and register a set carrying it)")

let lp07 =
    let t = node lock "testRepository"
    check "LP-07" "the test repository is isolated" (
        if isNull lock then [ "no lock" ] else
        fail (str t "isolated" = "true") "testRepository.isolated is not true (ODR-R10-09)"
        @ fail (str t "owner" <> "" && not ((str t "owner").StartsWith "(")) "testRepository.owner is not named"
        @ fail (str t "repo" <> "" && not ((str t "repo").StartsWith "(")) "testRepository.repo is not named"
        @ fail (not ((str t "repo").StartsWith "indy-init-rehearsal")) "the test repository is a competition repository")

let lp08 =
    let dir = opt "--live-evidence"
    let results = dir |> Option.bind (fun d -> json (Path.Combine(d, "journey-results.json"))) |> Option.toObj
    let rows = items results "results"
    let provider = node results "provider"
    let needed = [ for j in [ "J1"; "J2"; "J3" ] do for e in [ "chromium"; "webkit" ] -> j, e ]
    let outcome (j, e) = rows |> List.tryFind (fun r -> str r "journey" = j && str r "engine" = e) |> Option.map (fun r -> str r "status")
    check "LP-08" "live-provider preflight passed (J1-J3, Chromium and WebKit, api.github.com)" (
        match dir with
        | None -> [ "no live-provider preflight evidence (--live-evidence; LIVE-PROVIDER-PREFLIGHT.md)" ]
        | Some d when isNull results -> [ $"{d}/journey-results.json does not exist" ]
        | Some _ ->
            fail (str provider "mode" = "github" && str provider "baseUrl" = "https://api.github.com") $"""the journeys ran against {str provider "baseUrl"} ({str provider "mode"}), not https://api.github.com (a double is never live evidence)"""
            @ (needed |> List.collect (fun (j, e) ->
                match outcome (j, e) with
                | Some "passed" -> []
                | Some other -> [ $"{j} on {e}: {other}" ]
                | None -> [ $"{j} did not run on {e}" ])))

let lp09 =
    let tsv = path "docs/rehearsal-010-readiness/evidence/adversarial/cases.tsv"
    let rows = if File.Exists tsv then File.ReadAllLines tsv |> Array.skip 1 |> Array.filter (fun l -> l.Trim() <> "") |> List.ofArray else []
    let bad = rows |> List.filter (fun l -> not (l.EndsWith "VALIDATED")) |> List.map (fun l -> l.Split('\t').[0])
    check "LP-09" "the adversarial suite is validated" (fail (not rows.IsEmpty) "no adversarial results" @ fail bad.IsEmpty $"""cases not VALIDATED: {String.Join(", ", bad)}""")

let lp10 =
    check "LP-10" "the competition repository is clean (no T0, no clock)" (
        match opt "--competition" with
        | None -> [ "no competition repository named (--competition)" ]
        | Some d ->
            let _, count = git d [ "rev-list"; "--count"; "HEAD" ]
            fail (not (File.Exists(Path.Combine(d, "evidence", "t0", "t0.json")))) "a T0 record exists: the clock has started"
            @ fail (count = "0" || count = "1" || count = "") $"the competition repository has {count} commits; it must be empty or hold only its initial commit")

let checks = [ lp01; lp02; lp03; lp04; lp05; lp06; lp07; lp08; lp09; lp10 ]
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
