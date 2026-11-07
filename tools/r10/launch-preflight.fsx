// launch-preflight.fsx: may Rehearsal 010 (or the 7 November competition) be launched? Read-only: it checks, it never
// records, pins or starts anything.
//
//   dotnet fsi tools/r10/launch-preflight.fsx -- --indy-init DIR --competition URL --live-run RUN_ID --adversarial-run RUN_ID
//     [--run rehearsal-010|competition] [--origin URL] [--gh PATH] [--require-signatures true] [--offline true] [--out F]
//
// --run names the run judged (default rehearsal-010, whose checks are exactly those below). --run competition judges
// kickoff/competition/toolchain.lock.json instead: v4.1.6 (APR-V416-OWNER-1, -2), the competition decisions
// (APR-COMP-OWNER-1: ODR-COMP-P01..P14; ODR-COMP-P15 A or B, -P16 A; ODR-COMP-SCOPE, -CATALOG-CONTRACT, -LOCK) and the
// Rehearsal 010 decisions whose files the competition lock carries (ODR-R10-05, -06, -07, -09, -14); with P15 B the
// competition STARTUP must state the stack-drift waiver, with A its stack-drift command must check the lock's
// dependencyLock; LP-03 also checks the lock's dependencyLock and assertionMergeNeeds pins; LP-07 and LP-10 judge the lock's
// testRepository and competitionRepository (--competition defaults to its url, which must hold only its initialCommit);
// LP-08 requires the live run to have used the competition lock; and LP-11 requires the lock to be the proposal
// ODR-COMP-LOCK approved (only status and indyInit.commit filled) with Conditor v0.10.21's digests those of its
// release's checksums.txt.
//
// --offline true is the structural mode: nothing is fetched (no origin, no gh). What needs the network is reported
// PENDING, never PASS; while the lock is not committed the rest is judged on the proposal (LP-01 still refuses). An
// offline run is never READY (exit 4).
//
// Owner provenance (with --require-signatures true) is a signature, not a name. By default (the owner's decision of
// 2026-11-04 not to sign commits) it is GitHub's record that the owner committed and authored the change, which an
// agent writing the owner's email as committer can imitate: an accepted risk. The rest of this paragraph describes
// the signed mode: every change of the decisions approvals file (after 55f2467) or of the
// lock, along main's first-parent history, must be a commit (or a merge that brings in only such commits) that GitHub
// verifies as committed and signed by the owner (not web-flow, no agent trailer) AND whose signature verifies locally
// against the owner's signing keys registered on GitHub. An agent can write any author or committer name; it cannot
// sign with the owner's key.
// The owner merges owner-freeze pull requests with a merge commit (not squash or rebase), so the signed commits stay.
//
// READY only when every check below passes; otherwise REFUSED with each cause. Exit 0 READY, 4 REFUSED.
//   LP-00  the Indy-init checkout is clean and is main of the published repository (--origin)
//   LP-01  the owner's lock kickoff/rehearsal-010/toolchain.lock.json is committed, owner-signed in every change, not a proposal
//   LP-02  v4.1.5's tag exists on the published repository, the lock's tagCommit is that tag's commit, and the package
//          manifest at the tag has the lock's digest
//   LP-03  every digest in the lock (contracts, rules, the owner-decision requests) is the digest of the file it names
//   LP-04  the scope manifest's scopeSha256 is the lock's
//   LP-05  every decision a launch needs (fixed in this file, with the options each can launch with) and
//          ODR-V415-01, -03, -04, -IF-VIEW and -02 (approve) are recorded by the operator; every change of the approvals file since the last owner record
//          before Rehearsal 010 (55f2467) is committed, and signed, by the owner; no approval id repeats; every pin a decision recorded is the current
//          file's digest (and the lock's, where the lock pins it)
//   LP-06  the resolved set carries Dokimos 0.11.1 or later
//   LP-07  the test repository is isolated, named, not a competition repository, and the one ODR-R10-09 approved
//   LP-08  the live-provider preflight run (--live-run, fetched with gh): the live-provider workflow, dispatched on main,
//          successful, of a commit in this history whose journeys and workflow are unchanged since, bound to that
//          commit (candidate fixture-<sha>), against the lock's test repository; J1-J3 on Chromium and WebKit against
//          api.github.com, each passed, once
//   LP-09  the adversarial suite (--adversarial-run: the Preflight tooling CI run of a commit in this history whose
//          tools and verification assets are unchanged since): VALIDATED in every case tools/r10/adversarial.fsx defines
//   LP-10  the published competition repository: branches and tags at one commit, its initial one, with no T0 record

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text.Json.Nodes
open System.Text.RegularExpressions

// No replace refs or grafts: the history judged is the history as committed.
Environment.SetEnvironmentVariable("GIT_NO_REPLACE_OBJECTS", "1")
Environment.SetEnvironmentVariable("GIT_GRAFT_FILE", "/nonexistent/launch-preflight-no-grafts")
let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let opt (name: string) = args |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let src = opt "--indy-init" |> Option.defaultWith (fun () -> failwith "missing --indy-init") |> Path.GetFullPath
let path (rel: string) = Path.Combine(src, rel)
let gh = opt "--gh" |> Option.defaultValue "gh"
let origin = opt "--origin" |> Option.defaultValue "https://github.com/kemiller2002/Indy-init.git"
let indyInitRepo = "kemiller2002/Indy-init"
let liveWorkflow = "Live-provider preflight (Rehearsal 010)"
/// The run judged: rehearsal-010 (default) or competition.
let runName = opt "--run" |> Option.defaultValue "rehearsal-010"
let competition = runName = "competition"
if runName <> "rehearsal-010" && not competition then (eprintfn "launch-preflight: --run must be rehearsal-010 or competition"; exit 2)
/// The structural mode: no network; never READY.
let offline = opt "--offline" = Some "true"
let offlineNote (what: string) = if offline then [ $"offline: {what}" ] else []

let runEnv (env: (string * string) list) (dir: string) (exe: string) (a: string list) =
    try
        let info = ProcessStartInfo(exe, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        a |> List.iter info.ArgumentList.Add
        env |> List.iter (fun (k, v) -> info.Environment[k] <- v)
        use p = Process.Start info
        let out = p.StandardOutput.ReadToEnd()
        p.StandardError.ReadToEnd() |> ignore
        p.WaitForExit()
        p.ExitCode, out.Trim()
    with ex -> 127, ex.Message
let run = runEnv []
let git dir a = run dir "git" a
let sha256Bytes (b: byte[]) = (SHA256.HashData b |> Convert.ToHexString).ToLowerInvariant()
let sha256 (file: string) = if File.Exists file then sha256Bytes (File.ReadAllBytes file) else ""
let json (file: string) = if File.Exists file then (try Some(JsonNode.Parse(File.ReadAllText file)) with _ -> None) else None
let str (n: JsonNode) (k: string) = match n with null -> "" | n -> (match n[k] with null -> "" | v -> v.ToString())
let node (n: JsonNode) (k: string) : JsonNode = match n with null -> null | n -> n[k]
let items (n: JsonNode) (k: string) : JsonNode list = match node n k with :? JsonArray as a -> a |> Seq.toList | _ -> []
let tempDir (name: string) = let d = Path.Combine(Path.GetTempPath(), $"launch-preflight-{name}-{Guid.NewGuid():N}") in Directory.CreateDirectory d |> ignore; d

type Check = { Id: string; Title: string; Causes: string list; Pending: string list }
let check id title causes = { Id = id; Title = title; Causes = causes; Pending = [] }
/// A check with what it could not judge (offline): PENDING when nothing else refuses it.
let checkPending id title causes pending = { Id = id; Title = title; Causes = causes; Pending = pending }
let fail (cond: bool) (cause: string) = if cond then [] else [ cause ]

let lockRel = $"kickoff/{runName}/toolchain.lock.json"
let proposalRel = $"kickoff/{runName}/toolchain.lock.proposed.json"
let committedLock = json (path lockRel) |> Option.toObj
/// Offline, a lock not yet committed is judged on its proposal (LP-01 refuses it either way).
let judgingProposal = offline && isNull committedLock && File.Exists(path proposalRel)
let lock = if judgingProposal then json (path proposalRel) |> Option.toObj else committedLock
let approvalsRel = "kickoff/rehearsal-007/approvals.causa.json"
let operator = "kemiller2002"
/// The requirements package the run builds on (fixed per run, never read from the lock).
let packageVersion = if competition then "4.1.6" else "4.1.5"

/// GitHub's verdict: the signature is valid and the COMMITTER (whose key GitHub checked; the author is free text) is
/// the operator. GitHub's own commits (committer web-flow: web edits, API writes, squash merges) are not the owner's.
let githubSaysOwnerSigned (commit: string) =
    let code, out = run src gh [ "api"; $"repos/{indyInitRepo}/commits/{commit}"; "--jq"; "[.commit.verification.verified, .commit.verification.reason, .committer.login, .commit.message] | @json" ]
    if code <> 0 then false else
    try
        match JsonNode.Parse out with
        | :? JsonArray as a when a.Count = 4 ->
            a[0].GetValue<bool>() && a[1].GetValue<string>() = "valid" && a[2].GetValue<string>() = operator
            && not (Regex.IsMatch(a[3].GetValue<string>(), @"Claude-Session:|Co-Authored-By:\s*Claude", RegexOptions.IgnoreCase))
        | _ -> false
    with _ -> false

let mutable keyFetchFailures : string list = []
/// The operator's own signing keys, as registered on GitHub (SSH signing keys and GPG keys), in a private keyring and
/// an allowed-signers file: a commit verifies locally only against these, never against GitHub's or an agent's key.
let ownerKeys =
    lazy (
        let home = tempDir "owner-keys"
        run home "chmod" [ "700"; home ] |> ignore
        let keysOf (endpoint: string) (field: string) =
            let code, out = run src gh [ "api"; $"users/{operator}/{endpoint}"; "--jq"; $"[.[].{field}] | @json" ]
            if code <> 0 then (keyFetchFailures <- $"users/{operator}/{endpoint}: {out}" :: keyFetchFailures; [])
            else
                try (JsonNode.Parse out).AsArray() |> Seq.map (fun k -> k.GetValue<string>()) |> List.ofSeq
                with _ -> (keyFetchFailures <- $"users/{operator}/{endpoint}: unreadable answer" :: keyFetchFailures; [])
        let allowed = Path.Combine(home, "allowed-signers")
        File.WriteAllLines(allowed, keysOf "ssh_signing_keys" "key" |> List.map (fun k -> $"{operator} namespaces=\"git\" {k}"))
        keysOf "gpg_keys" "raw_key" |> List.iteri (fun i k ->
            let f = Path.Combine(home, $"key-{i}.asc")
            File.WriteAllText(f, k)
            runEnv [ "GNUPGHOME", home ] home "gpg" [ "--batch"; "--quiet"; "--import"; f ] |> ignore)
        home, allowed)

/// Whether the commit's signature verifies locally against the operator's registered keys.
let locallyOwnerSigned (commit: string) =
    let home, allowed = ownerKeys.Force()
    // The verifying programs are pinned: a configured gpg.program or gpg.ssh.program (an agent's signer, say) never
    // decides; only the owner's registered keys verify.
    // Global and system config are ignored, and every verifier is pinned (x509 is never the owner's): only the owner's
    // registered keys can verify.
    let env = [ "GNUPGHOME", home; "GIT_CONFIG_GLOBAL", "/dev/null"; "GIT_CONFIG_NOSYSTEM", "1" ]
    fst (runEnv env src "git" [ "-c"; "gpg.program=gpg"; "-c"; "gpg.ssh.program=ssh-keygen"; "-c"; "gpg.x509.program=false"; "-c"; $"gpg.ssh.allowedSignersFile={allowed}"; "verify-commit"; commit ]) = 0

/// Owner-signed: GitHub verifies the owner as committer AND the signature verifies against the owner's own keys.
/// GitHub's record without a signature (the owner's decision, 2026-11-04: no commit signing): the committer and the
/// author are the operator (not web-flow, not an agent account) and the message carries no agent trailer. ACCEPTED
/// RISK: a commit whose committer email is the owner's is recorded as the owner's, so an agent that writes the owner's
/// email as committer is not told apart (FINAL-READINESS §6).
let githubSaysOwnerCommitted (commit: string) =
    let code, out = run src gh [ "api"; $"repos/{indyInitRepo}/commits/{commit}"; "--jq"; "[.committer.login, .author.login, .commit.message] | @json" ]
    if code <> 0 then false else
    try
        match JsonNode.Parse out with
        | :? JsonArray as a when a.Count = 3 && not (isNull a[0]) && not (isNull a[1]) ->
            a[0].GetValue<string>() = operator && a[1].GetValue<string>() = operator
            && not (Regex.IsMatch(a[2].GetValue<string>(), @"Claude-Session:|Co-Authored-By:\s*Claude", RegexOptions.IgnoreCase))
        | _ -> false
    with _ -> false

/// --require-signatures true restores the full check (GitHub-verified signature by the owner as committer, and the
/// signature verified locally against the owner's registered keys). Off by default: the owner does not sign commits.
let requireSignatures = opt "--require-signatures" = Some "true"
let ownerSigned (commit: string) =
    if requireSignatures then githubSaysOwnerSigned commit && locallyOwnerSigned commit
    else githubSaysOwnerCommitted commit

/// The blob of `file` at `commit` ("" when absent).
let blobAt (commit: string) (file: string) =
    let code, out = git src [ "rev-parse"; "--verify"; "--quiet"; $"{commit}:{file}" ]
    if code = 0 then out else ""
let parentsOf (commit: string) =
    (snd (git src [ "rev-list"; "--parents"; "-n"; "1"; commit ])).Split(' ', StringSplitOptions.RemoveEmptyEntries) |> Array.skip 1 |> List.ofArray

/// Whether the owner merged this merge commit: GitHub records the pull request it merged as merged by the operator (a
/// signature shows who wrote a change, not who decided to merge it; an abandoned owner branch merged by anyone fails).
let mergedByOwner (commit: string) =
    // commits/{sha}/pulls lists abbreviated pull requests (no merged_by): find the one this commit merged, then read
    // the full pull request.
    let code, out = run src gh [ "api"; $"repos/{indyInitRepo}/commits/{commit}/pulls"; "--jq"; $"[.[] | select(.merge_commit_sha == \"{commit}\") | .number] | @json" ]
    let numbers = if code <> 0 then [] else (try (JsonNode.Parse out).AsArray() |> Seq.map (fun n -> n.GetValue<int>()) |> List.ofSeq with _ -> [])
    numbers |> List.exists (fun n ->
        let c, pr = run src gh [ "api"; $"repos/{indyInitRepo}/pulls/{n}"; "--jq"; "[.merged, .merged_by.login] | @json" ]
        c = 0 && (try (match JsonNode.Parse pr with
                       | :? JsonArray as x when x.Count = 2 && not (isNull x[1]) -> x[0].GetValue<bool>() && x[1].GetValue<string>() = operator
                       | _ -> false) with _ -> false))

/// The commits that changed `file` (against their first parent) on the first-parent line `range`, oldest first.
let rec changesOn (file: string) (range: string list) =
    let _, revs = git src ([ "rev-list"; "--reverse"; "--first-parent" ] @ range)
    revs.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
    |> List.filter (fun c -> blobAt c file <> (match parentsOf c with p :: _ -> blobAt p file | [] -> ""))

/// A change of `file` is acceptable when its commit is owner-signed, or when it is a merge that only brings in the
/// owner's signed changes from a branch: the result is the branch's file, main did not change the file since the
/// branch point, and every change of the file on that branch is itself acceptable. Anything else (a merge that rolls
/// the file back to an older owner version, an evil merge, an unsigned commit) is not.
and acceptable (file: string) (commit: string) =
    ownerSigned commit
    || (match parentsOf commit with
        | [ p1; p2 ] ->
            let mb = snd (git src [ "merge-base"; p1; p2 ])
            mb <> "" && blobAt commit file = blobAt p2 file && blobAt p1 file = blobAt mb file
            && mergedByOwner commit
            && (changesOn file [ $"{mb}..{p2}" ] |> List.forall (acceptable file))
        | _ -> false)

/// The changes of `file` after `baseline` (the whole history when the baseline is "" or not in it) on main's
/// first-parent line that are not acceptable.
let unsignedChanges (file: string) (baseline: string) =
    let range = if baseline <> "" && fst (git src [ "merge-base"; "--is-ancestor"; baseline; "HEAD" ]) = 0 then [ $"{baseline}..HEAD" ] else [ "HEAD" ]
    changesOn file range |> List.filter (acceptable file >> not)

let lp00 =
    let _, status = git src [ "status"; "--porcelain"; "--untracked-files=no" ]
    let _, head = git src [ "rev-parse"; "HEAD" ]
    let code, remote = if offline then 0, head else git src [ "ls-remote"; origin; "refs/heads/main" ]
    let remoteMain = remote.Split('\t').[0]
    let _, shallow = git src [ "rev-parse"; "--is-shallow-repository" ]
    checkPending "LP-00" "the Indy-init checkout is clean, complete and is origin/main" (
        fail (status = "") "the Indy-init checkout has uncommitted changes"
        @ fail (shallow = "false") "the Indy-init checkout is shallow: its history cannot be judged (git fetch --unshallow)"
        @ fail (code = 0 && remoteMain <> "") $"main of {origin} cannot be read"
        @ fail (code <> 0 || remoteMain = head) $"HEAD {head} is not origin/main {remoteMain}: judge what is published")
        (offlineNote $"HEAD {head} is not compared with main of {origin}")

let lp01 =
    let tracked = fst (git src [ "ls-files"; "--error-unmatch"; lockRel ]) = 0
    let lock = committedLock
    let who = if competition then "ODR-COMP-LOCK A, then the owner commits it as ODR-RG-04" else "ODR-RG-04: the owner commits it"
    checkPending "LP-01" "the owner's lock is committed" (
        fail (not (isNull lock)) $"{lockRel} does not exist ({who})"
        @ fail (isNull lock || tracked) $"{lockRel} is not committed"
        @ fail (isNull lock || not ((str lock "status").StartsWith "PROPOSED")) $"{lockRel} is still a proposal (status PROPOSED)"
        @ (if isNull lock || offline then [] else unsignedChanges lockRel "" |> List.map (fun c -> $"{lockRel} was changed in {c.Substring(0, 12)}, which GitHub does not verify as signed by {operator}")))
        ((if isNull lock then [] else offlineNote $"who committed {lockRel} is not asked of GitHub")
         @ (if judgingProposal then [ $"LP-02..LP-07 and LP-11 judge {proposalRel}, the proposal, until the owner commits {lockRel}" ] else []))

let lp02 =
    let pkg = node lock "requirementsPackage"
    let tag = (str pkg "tag").Split(' ').[0]
    let code, remote =
        if offline then (let c, o = git src [ "rev-parse"; "--verify"; "--quiet"; $"refs/tags/{tag}^{{commit}}" ] in c, (if c = 0 then $"{o}\trefs/tags/{tag}^{{}}" else ""))
        else git src [ "ls-remote"; "--tags"; origin; $"refs/tags/{tag}"; $"refs/tags/{tag}^{{}}" ]
    let lines = remote.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.map (fun l -> l.Split('\t'))
    let tagCommit = lines |> Array.tryFind (fun l -> l.[1].EndsWith "^{}") |> Option.orElse (Array.tryHead lines) |> Option.map (fun l -> l.[0]) |> Option.defaultValue ""
    let decision = if competition then "ODR-V416-02" else "ODR-V415-02"
    // The manifest at the tag is the working file (same blob), whose sha256 is the lock's (LP-00: the tree is main).
    let manifestRel = $"""{str pkg "path"}/package-manifest.json"""
    let tcode, blobAtTag = git src [ "rev-parse"; "--verify"; "--quiet"; $"{tagCommit}:{manifestRel}" ]
    let _, blobNow = git src [ "hash-object"; manifestRel ]
    checkPending "LP-02" $"v{packageVersion} is frozen and tagged on origin" (
        if isNull lock then [ "no lock" ] else
        fail (str pkg "packageVersion" = packageVersion) $"""the lock pins package {str pkg "packageVersion"}, not {packageVersion}"""
        @ fail (not competition || (str pkg "path" = $"requirements-compiled/v{packageVersion}" && tag = $"indy-init-requirements-v{packageVersion}")) $"""the lock pins {str pkg "path"} at tag {tag}, not requirements-compiled/v{packageVersion} at indy-init-requirements-v{packageVersion}"""
        @ fail (code = 0 && tagCommit <> "") $"tag {tag} does not exist on origin ({decision}, owner-freeze.fsx tag v{packageVersion})"
        @ fail (tagCommit = "" || str pkg "tagCommit" = tagCommit) $"""the lock's tagCommit {str pkg "tagCommit"} is not {tag}'s commit {tagCommit}"""
        @ fail (tagCommit = "" || tcode = 0) $"the tag's commit {tagCommit} is not in this clone (git fetch --tags origin)"
        @ fail (tagCommit = "" || tcode <> 0 || (blobAtTag = blobNow && sha256 (path manifestRel) = str pkg "manifestSha256")) $"the v{packageVersion} package manifest at the tag differs from the lock's manifestSha256")
        (offlineNote $"tag {tag} was read from this clone, not from {origin}")

let pinned =
    [ "catalogVerification", "contract", "contractSha256"
      "designContract", "contract", "contractSha256"
      "deadlinePolicy", "path", "sha256"
      "journeys", "bindings", "bindingsSha256"
      "processEvidence", "rules", "rulesSha256"
      "ownerDecisionRequests", "path", "sha256" ]
    // The competition's package carries the WDL-013 plan supplement (ODR-V416-WDL013 A), which its scope is compiled from;
    // its lock pins the successor dependency lock (ODR-COMP-P15) and the assertion merge needs (ODR-COMP-P16).
    @ (if competition then
           [ "requirementsPackage", "verificationPlanSupplement", "verificationPlanSupplementSha256"
             "dependencyLock", "path", "sha256"
             "assertionMergeNeeds", "path", "sha256" ]
       else [])
let lp03 =
    check "LP-03" "every contract digest in the lock is its file's" (
        if isNull lock then [ "no lock" ] else
        (pinned |> List.collect (fun (section, fileKey, shaKey) ->
            let s = node lock section
            let file, want = str s fileKey, str s shaKey
            if file = "" then [ $"the lock names no {section}.{fileKey}" ]
            elif want = "" then [ $"{section}.{shaKey} is not pinned" ]
            else fail (sha256 (path file) = want) $"{file}: sha256 {sha256 (path file)}, the lock pins {want}"))
        // The dependency lock's source, which ODR-COMP-P15 pins with it, exists where the lock says.
        @ (match str (node lock "dependencyLock") "source" with
           | "" -> []
           | s -> fail (File.Exists(path s)) $"the lock's dependencyLock.source {s} does not exist"))

let lp04 =
    let manifest = json (path (str (node lock "scope") "manifest")) |> Option.toObj
    check "LP-04" "the scope is the approved one" (
        if isNull lock then [ "no lock" ] else
        fail (not (isNull manifest)) "the lock names no scope manifest that exists"
        @ fail (isNull manifest || str manifest "scopeSha256" = str (node lock "scope") "scopeSha256") $"""the scope manifest has scopeSha256 {str manifest "scopeSha256"}, the lock pins {str (node lock "scope") "scopeSha256"}""")

/// The last change of the approvals file before Rehearsal 010 (APR-V412-OWNER-3, the owner's v4.1.2 approval): every
/// later change must be owner-signed, whatever it adds (an approval id, a decision id or an edit).
let approvalsBaseline = "55f2467f5ff7f39f8216d2ba291dbeb58b506a5f"
let approvalsUnsigned = if offline then [] else unsignedChanges approvalsRel approvalsBaseline
/// The approvals that count: by the operator, in a file whose every change since the baseline is owner-signed. An
/// unsigned change voids them all (an in-place edit, a reused id or an appended change cannot be told apart from the
/// owner's own).
let ownerApprovals =
    let approvals = json (path approvalsRel) |> Option.toObj
    if not approvalsUnsigned.IsEmpty then [] else
    items approvals "approvals" |> List.filter (fun a -> str (node a "authority") "id" = operator)
let duplicateIds =
    items (json (path approvalsRel) |> Option.toObj) "approvals" |> List.map (fun a -> str a "id") |> List.countBy id |> List.filter (fun (_, n) -> n > 1) |> List.map fst
let ownerChanges = ownerApprovals |> List.collect (fun a -> items a "changes")
let recordedFor id = ownerChanges |> List.filter (fun c -> str c "decision_id" = id) |> List.tryLast
let withoutApproval (s: string) =
    try
        let n = JsonNode.Parse s
        match n with
        | :? JsonObject as o -> o.Remove "approval" |> ignore; o.ToJsonString()
        | n -> n.ToJsonString()
    with _ -> s

/// The Rehearsal 010 decisions a launch needs, and the options each can launch with. Fixed here, not read from the
/// requests file (which the lock pins, LP-03, so the questions asked are the questions approved).
let requiredDecisions =
    [ "ODR-RG-03", [ "A" ]; "ODR-RG-05", [ "A" ]; "ODR-R10-02", [ "A"; "B" ]; "ODR-R10-03", [ "A" ]; "ODR-R10-04", [ "A" ]
      "ODR-R10-05", [ "A" ]; "ODR-R10-06", [ "A" ]; "ODR-R10-07", [ "A" ]; "ODR-R10-08", [ "A" ]; "ODR-R10-09", [ "A" ]
      "ODR-R10-10", [ "A" ]; "ODR-R10-11", [ "A" ]; "ODR-R10-12", [ "A" ]; "ODR-R10-13", [ "A" ]; "ODR-R10-14", [ "A" ]
      "ODR-R10-15", [ "A" ]; "ODR-R10-16", [ "A" ]; "ODR-R10-17", [ "A" ]; "ODR-R10-18", [ "A" ]; "ODR-R10-01", [ "A" ] ]

/// The competition's decisions, each with the option it launches with: v4.1.6 as approved (APR-V416-OWNER-1), the
/// competition decisions (APR-COMP-OWNER-1; ODR-COMP-P15 A or B, -P16, -SCOPE, -CATALOG-CONTRACT and -LOCK), and the Rehearsal 010
/// decisions whose files the competition lock carries unchanged (design contract, deadline policy, journey bindings,
/// test repository, process-evidence rules).
let competitionDecisions =
    let launchableWith (o: string) = List.map (fun (id: string) -> id, [ o ])
    ([ "ODR-V416-01", "A"; "ODR-V416-RC1", "A"; "ODR-V416-S1", "B"; "ODR-V416-S8", "B"; "ODR-V416-RC3", "A"; "ODR-V416-RC4", "A"
       "ODR-V416-RC5", "A"; "ODR-V416-RC6", "A"; "ODR-V416-RC7", "A"; "ODR-V416-S2", "B"; "ODR-V416-S3", "A"; "ODR-V416-S4", "A"
       "ODR-V416-S5", "A"; "ODR-V416-S6", "A"; "ODR-V416-S7", "A"; "ODR-V416-S9", "A"; "ODR-V416-CAT", "A"; "ODR-V416-WDL013", "A"
       "ODR-V416-SCOPE-SELECTORS", "A"; "ODR-V416-FSCORE", "A" ]
     |> List.map (fun (id, o) -> id, [ o ]))
    @ launchableWith "A" [ for i in 1..14 -> $"ODR-COMP-P{i:D2}" ]
    // The successor dependency lock: A adopts it; B keeps the 1.37.0 lock and waives the stack-drift preflight.
    @ [ "ODR-COMP-P15", [ "A"; "B" ] ]
    @ launchableWith "A" [ "ODR-COMP-P16"; "ODR-COMP-SCOPE"; "ODR-COMP-CATALOG-CONTRACT"; "ODR-COMP-LOCK" ]
    @ launchableWith "A" [ "ODR-R10-05"; "ODR-R10-06"; "ODR-R10-07"; "ODR-R10-09"; "ODR-R10-14" ]
/// The competition STARTUP, which must say what ODR-COMP-P15 decided: with A its stack-drift command checks the lock's
/// dependencyLock; with B the preflight is waived.
let startupRel = "docs/competition-readiness/STARTUP-INSTRUCTIONS.md"
let p15Waiver = "with ODR-COMP-P15 B the stack-drift preflight is waived"
let p15Startup (option: string) =
    let text = if File.Exists(path startupRel) then File.ReadAllText(path startupRel) else ""
    let depLock = str (node lock "dependencyLock") "path"
    match option with
    | "A" -> fail (depLock <> "" && text.Contains $"stack-drift.fsx -- check --lock SRC/{depLock}") $"ODR-COMP-P15 is A, but {startupRel}'s stack-drift preflight does not check the lock's dependencyLock (check --lock SRC/{depLock})"
    | "B" -> fail (text.Contains p15Waiver) $"ODR-COMP-P15 is B, but {startupRel} does not waive the stack-drift preflight ('{p15Waiver}')"
    | _ -> []
/// The owner-freeze.fsx set that records a decision.
let decideSet (id: string) =
    if not competition then "rehearsal-010"
    elif id.StartsWith "ODR-V416-" then "v4.1.6"
    elif id.StartsWith "ODR-COMP-" then "competition"
    else "rehearsal-010"

let lp05 =
    let requests = if competition then competitionDecisions else requiredDecisions
    let pkg = node lock "requirementsPackage"
    let lockDigests = pinned |> List.map (fun (s, f, k) -> str (node lock s) f, str (node lock s) k) |> Map.ofList
    let packageDecisions =
        if competition then
            fail (ownerChanges |> List.exists (fun c ->
                    str c "decision_id" = "ODR-V416-02" && str c "action" = "approve-package"
                    && str c "package" = str pkg "path" && str c "manifest_sha256" = str pkg "manifestSha256"))
                $"""ODR-V416-02 (approve v4.1.6 with the manifest the lock pins, {str pkg "manifestSha256"}) is not recorded by the owner (owner-freeze.fsx approve v4.1.6)"""
        else
        ([ "ODR-V415-01"; "ODR-V415-03"; "ODR-V415-04"; "ODR-V415-IF-VIEW" ] |> List.collect (fun id -> fail (Option.isSome (recordedFor id)) $"{id} is not recorded by the owner (owner-freeze.fsx decide v4.1.5)"))
        @ fail (ownerChanges |> List.exists (fun c -> str c "decision_id" = "ODR-V415-02" && str c "action" = "approve-package")) "ODR-V415-02 (approve v4.1.5) is not recorded by the owner (owner-freeze.fsx approve v4.1.5)"
    let r10 =
        requests |> List.collect (fun (id, launchable) ->
            match recordedFor id with
            | None -> [ $"{id} is not recorded by the owner (owner-freeze.fsx decide {decideSet id})" ]
            | Some c when not (List.contains (str c "option") launchable) ->
                [ $"""{id} is recorded as option {str c "option"} ({str c "action"}); {(if competition then "the competition" else "Rehearsal 010")} launches only with {String.Join(" or ", launchable)}""" ]
            | Some c ->
                (if competition && id = "ODR-COMP-P15" then p15Startup (str c "option") else [])
                @ (items c "pins" |> List.collect (fun p ->
                    let file = str p "path"
                    let field = str p "field"
                    if field <> "" then
                        let now = json (path file) |> Option.map (fun j -> match j[field] with null -> "" | v -> v.ToJsonString()) |> Option.defaultValue ""
                        fail (now = str p "value") $"""{id} approved {file} {field} = {str p "value"}; it is now {now}"""
                    else
                        fail (sha256 (path file) = str p "sha256") $"""{id} approved {file} at sha256 {str p "sha256"}; it is now {sha256 (path file)}"""
                        @ (match lockDigests.TryFind file with
                           | Some d when d <> "" && d <> str p "sha256" -> [ $"""{id} approved {file} at {str p "sha256"}; the lock pins {d}""" ]
                           | _ -> []))))
    let provenance =
        (approvalsUnsigned |> List.map (fun c -> $"{approvalsRel} was changed in {c.Substring(0, 12)}, which GitHub does not verify as signed by {operator}: no record in it counts"))
        @ (duplicateIds |> List.map (fun i -> $"approval id {i} appears more than once"))
        @ (keyFetchFailures |> List.map (fun e -> $"the owner's signing keys could not all be read from GitHub ({e}): signatures by those keys cannot be verified"))
    checkPending "LP-05" "every owner decision is recorded in owner-signed commits, launchable, and what it approved is what is pinned" (provenance @ packageDecisions @ r10)
        (offlineNote $"who committed each change of {approvalsRel} is not asked of GitHub")

let lp06 =
    let v = str (node (node lock "resolvedSet") "expectedTools") "dokimos"
    let atLeast (a: string) (b: string) = (Version.TryParse a |> function | true, x -> Some x | _ -> None) |> Option.exists (fun x -> x >= Version.Parse b)
    check "LP-06" "the resolved set carries Dokimos 0.11.1" (if isNull lock then [ "no lock" ] else fail (atLeast v "0.11.1") $"the resolved set's Dokimos is {v} (ODR-RG-03: release 0.11.1 and register a set carrying it)")

let lp07 =
    let t = node lock "testRepository"
    let approved = recordedFor "ODR-R10-09" |> Option.bind (fun c -> items c "pins" |> List.tryFind (fun p -> str p "field" = "testRepository")) |> Option.map (fun p -> str p "value")
    let c = node lock "competitionRepository"
    let named (n: JsonNode) (k: string) = str n k <> "" && not ((str n k).StartsWith "(")
    check "LP-07" "the test repository is isolated and the one approved" (
        if isNull lock then [ "no lock" ] else
        fail (str t "isolated" = "true") "testRepository.isolated is not true (ODR-R10-09)"
        @ fail (named t "owner") "testRepository.owner is not named"
        @ fail (named t "repo") "testRepository.repo is not named"
        @ (if competition then
               fail (named c "owner" && named c "repo") "competitionRepository.owner and .repo are not named (ODR-COMP-P07)"
               @ fail (not (String.Equals($"""{str t "owner"}/{str t "repo"}""", $"""{str c "owner"}/{str c "repo"}""", StringComparison.OrdinalIgnoreCase)))
                   $"""the test repository {str t "owner"}/{str t "repo"} is the competition repository"""
           else fail (not ((str t "repo").StartsWith "indy-init-rehearsal")) "the test repository is a competition repository")
        @ (match approved with
           | None -> [ "ODR-R10-09 recorded no testRepository" ]
           | Some v -> fail (isNull t || withoutApproval v = withoutApproval (t.ToJsonString())) "the lock's testRepository is not the one ODR-R10-09 approved"))

let lp08 =
    let rows (results: JsonNode) = items results "results"
    let needed = [ for j in [ "J1"; "J2"; "J3" ] do for e in [ "chromium"; "webkit" ] -> j, e ]
    let judge (meta: JsonNode) (results: JsonNode) (info: JsonNode) =
        let provider = node results "provider"
        let headSha = str meta "headSha"
        let ancestor = headSha <> "" && fst (git src [ "merge-base"; "--is-ancestor"; headSha; "HEAD" ]) = 0
        let unchanged = ancestor && fst (git src [ "diff"; "--quiet"; headSha; "HEAD"; "--"; "verification"; ".github/workflows/live-provider-preflight.yml" ]) = 0
        let withoutApproval' (n: JsonNode) = match n with null -> "" | n -> withoutApproval (n.ToJsonString())
        fail (str meta "workflowName" = liveWorkflow) $"""run is workflow "{str meta "workflowName"}", not "{liveWorkflow}" """
        @ fail (str meta "headBranch" = "main") $"""run is on {str meta "headBranch"}, not main"""
        @ fail (str meta "event" = "workflow_dispatch") $"""run was triggered by {str meta "event"}, not a dispatch"""
        @ fail (str meta "conclusion" = "success") $"""run concluded {str meta "conclusion"}"""
        @ fail (str results "candidate" = "fixture-" + headSha) $"""the results are bound to {str results "candidate"}, not fixture-{headSha}"""
        @ fail ancestor $"the run's commit {headSha} is not in this history"
        @ fail (not ancestor || unchanged) $"the journeys or the live workflow changed since the run's commit {headSha}: run the preflight again"
        @ fail (not (isNull info)) "the run recorded no run-info.json (which lock and test repository it used)"
        @ fail (isNull info || isNull lock || withoutApproval' (node info "testRepository") = withoutApproval' (node lock "testRepository")) "the run used another test repository than the lock names"
        @ fail (not competition || isNull info || str info "lock" = lockRel) $"""the run used the lock {str info "lock"}, not {lockRel} (dispatch it with lock={lockRel})"""
        @ fail (str provider "mode" = "github" && str provider "baseUrl" = "https://api.github.com") $"""the journeys ran against {str provider "baseUrl"} ({str provider "mode"}), not https://api.github.com (a double is never live evidence)"""
        @ (needed |> List.collect (fun (j, e) ->
            match rows results |> List.filter (fun r -> str r "journey" = j && str r "engine" = e) |> List.map (fun r -> str r "status") with
            | [ "passed" ] -> []
            | [] -> [ $"{j} did not run on {e}" ]
            | [ other ] -> [ $"{j} on {e}: {other}" ]
            | many -> [ $"""{j} on {e} is reported {many.Length} times ({String.Join(", ", many)})""" ]))
    let title = "live-provider preflight passed (the governed run: J1-J3, Chromium and WebKit, api.github.com)"
    if offline then checkPending "LP-08" title [] (offlineNote $"""the live-provider run {opt "--live-run" |> Option.defaultValue "(none named)"} is not fetched""") else
    check "LP-08" title (
        match opt "--live-run" with
        | None -> [ "no live-provider preflight run (--live-run RUN_ID; LIVE-PROVIDER-PREFLIGHT.md)" ]
        | Some id ->
            let code, metaText = run src gh [ "run"; "view"; id; "--repo"; indyInitRepo; "--json"; "workflowName,headBranch,event,conclusion,headSha" ]
            if code <> 0 then [ $"gh run view {id} failed: {metaText}" ] else
            let _, wfPath = run src gh [ "api"; $"repos/{indyInitRepo}/actions/runs/{id}"; "--jq"; ".path" ]
            if wfPath <> ".github/workflows/live-provider-preflight.yml" then [ $"run {id} is of workflow file {wfPath}, not .github/workflows/live-provider-preflight.yml" ] else
            let dir = tempDir "live"
            let dcode, dout = run src gh [ "run"; "download"; id; "--repo"; indyInitRepo; "--dir"; dir ]
            let find name = if Directory.Exists dir then Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories) |> Seq.tryHead else None
            let file = find "journey-results.json"
            let info = find "run-info.json" |> Option.bind json |> Option.toObj
            match dcode, file with
            | 0, Some f -> (match json f with Some results -> judge (JsonNode.Parse metaText) results info | None -> [ "the run's journey-results.json does not parse" ])
            | 0, None -> [ $"run {id} has no journey-results.json artifact" ]
            | _ -> [ $"gh run download {id} failed: {dout}" ])

let lp09 =
    let defined =
        let f = path "tools/r10/adversarial.fsx"
        if File.Exists f then Regex.Matches(File.ReadAllText f, "\\{ Id = \"(C[0-9]+)\"") |> Seq.map (fun m -> m.Groups[1].Value) |> Set.ofSeq else Set.empty
    /// The shard tables the run published (adv-evidence/adversarial-shard-N/cases.tsv). Every case the suite defines must
    /// appear exactly once across them, and every row must be VALIDATED.
    let judged (meta: JsonNode) (tsvs: string list) =
        let headSha = str meta "headSha"
        let ancestor = headSha <> "" && fst (git src [ "merge-base"; "--is-ancestor"; headSha; "HEAD" ]) = 0
        let unchanged = ancestor && fst (git src [ "diff"; "--quiet"; headSha; "HEAD"; "--"; "tools"; "verification"; ".github/workflows/preflight-tooling.yml" ]) = 0
        let rows =
            tsvs
            |> List.collect (fun tsv -> File.ReadAllLines tsv |> Array.skip 1 |> Array.filter (fun l -> l.Trim() <> "") |> List.ofArray)
            |> List.map (fun l -> let cells = l.Split('\t') in cells.[0], cells.[cells.Length - 1].Trim())
        let validated = rows |> List.filter (fun (_, verdict) -> verdict = "VALIDATED") |> List.map fst |> Set.ofList
        let missing = Set.difference defined validated
        // Every case once, and only VALIDATED: a second row can never hide a failure (as LP-08 for journeys).
        let notValidated = rows |> List.filter (fun (_, verdict) -> verdict <> "VALIDATED") |> List.map fst |> List.distinct
        let repeated = rows |> List.countBy fst |> List.filter (fun (_, n) -> n > 1) |> List.map fst
        fail (str meta "workflowName" = "Preflight tooling") $"""run is workflow "{str meta "workflowName"}", not "Preflight tooling" """
        @ fail (str meta "headBranch" = "main") $"""run is on {str meta "headBranch"}, not main"""
        @ fail (str meta "conclusion" = "success") $"""run concluded {str meta "conclusion"}"""
        @ fail ancestor $"the run's commit {headSha} is not in this history"
        @ fail (not ancestor || unchanged) $"the tools or the verification assets changed since the run's commit {headSha}: use a run of this tree"
        @ fail missing.IsEmpty $"""cases not VALIDATED in the run: {String.Join(", ", missing)}"""
        @ fail notValidated.IsEmpty $"""cases the run did not validate: {String.Join(", ", notValidated)}"""
        @ fail repeated.IsEmpty $"""cases reported more than once across the shards: {String.Join(", ", repeated)}"""
    let title = "the adversarial suite is validated in every case it defines (a CI run of this tree)"
    if offline then checkPending "LP-09" title (fail (not defined.IsEmpty) "tools/r10/adversarial.fsx defines no case") (offlineNote $"""the Preflight tooling run {opt "--adversarial-run" |> Option.defaultValue "(none named)"} is not fetched""") else
    check "LP-09" title (
        if defined.IsEmpty then [ "tools/r10/adversarial.fsx defines no case" ] else
        match opt "--adversarial-run" with
        | None -> [ "no adversarial CI run (--adversarial-run RUN_ID: the Preflight tooling run of this tree)" ]
        | Some id ->
            let code, metaText = run src gh [ "run"; "view"; id; "--repo"; indyInitRepo; "--json"; "workflowName,headBranch,event,conclusion,headSha" ]
            if code <> 0 then [ $"gh run view {id} failed: {metaText}" ] else
            let _, wfPath = run src gh [ "api"; $"repos/{indyInitRepo}/actions/runs/{id}"; "--jq"; ".path" ]
            if wfPath <> ".github/workflows/preflight-tooling.yml" then [ $"run {id} is of workflow file {wfPath}, not .github/workflows/preflight-tooling.yml" ] else
            let dir = tempDir "adversarial"
            let dcode, dout = run src gh [ "run"; "download"; id; "--repo"; indyInitRepo; "--name"; "r10-adversarial"; "--dir"; dir ]
            // Only the shard tables: adv-evidence/adversarial-shard-N/cases.tsv (another evidence folder can never validate a case).
            let shardTables =
                if Directory.Exists dir then
                    Directory.EnumerateDirectories(dir, "adversarial-shard-*", SearchOption.AllDirectories)
                    |> Seq.filter (fun d -> Path.GetFileName(Path.GetDirectoryName d) = "adv-evidence")
                    |> Seq.map (fun d -> Path.Combine(d, "cases.tsv"))
                    |> Seq.filter File.Exists
                    |> Seq.sort |> List.ofSeq
                else []
            let tsvs = shardTables
            let result =
                match dcode, tsvs with
                | 0, [] -> [ $"run {id} has no adversarial shard table (r10-adversarial adv-evidence/adversarial-shard-N/cases.tsv)" ]
                | 0, ts -> judged (JsonNode.Parse metaText) ts
                | _ -> [ $"gh run download {id} failed: {dout}" ]
            (try Directory.Delete(dir, true) with _ -> ())
            result)

let lp10 =
    let title = "the competition repository is clean (no T0, no clock), as published"
    let c = node lock "competitionRepository"
    // The competition's repository is pre-created by the owner (ODR-COMP-P07 A): it holds exactly the lock's initialCommit.
    let initial = if competition then str c "initialCommit" else ""
    let named = opt "--competition" |> Option.orElse (if competition && str c "url" <> "" then Some(str c "url") else None)
    if offline then checkPending "LP-10" title [] (offlineNote $"""the competition repository {named |> Option.defaultValue "(none named)"} is not cloned""") else
    check "LP-10" title (
        match named with
        | None -> [ "no competition repository named (--competition URL|DIR)" ]
        | Some c ->
            let mirror = tempDir "competition"
            let code, out = git src [ "clone"; "--quiet"; "--mirror"; c; mirror ]
            if code <> 0 then [ $"the competition repository {c} cannot be read: {out}" ] else
            let _, refs = git mirror [ "for-each-ref"; "--format=%(refname)"; "refs/heads"; "refs/tags" ]
            let refList = refs.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
            let tips = refList |> List.map (fun r -> snd (git mirror [ "rev-parse"; r + "^{commit}" ])) |> List.distinct
            let result =
              fail (tips.Length <= 1) $"the competition repository's branches and tags point at {tips.Length} different commits; it must hold one initial commit"
              @ (if competition && isNull lock then [ "no lock: the competition repository's initial commit is the lock's competitionRepository.initialCommit" ]
                 else fail (not competition || tips = [ initial ]) $"""the competition repository holds {String.Join(", ", tips)}, not only the owner's initial commit {initial} (competitionRepository.initialCommit)""")
              @ (refList |> List.collect (fun r ->
                let _, count = git mirror [ "rev-list"; "--count"; r ]
                let t0 = fst (git mirror [ "cat-file"; "-e"; $"{r}:evidence/t0/t0.json" ]) = 0
                fail (not t0) $"{r} holds a T0 record: the clock has started"
                @ fail (count = "0" || count = "1") $"{r} has {count} commits; the competition repository must be empty or hold only its initial commit"))
            (try Directory.Delete(mirror, true) with _ -> ())
            result)

/// LP-11 (the competition): the committed lock is the proposal ODR-COMP-LOCK approved, with only status and
/// indyInit.commit filled; indyInit.commit is in this history; Conditor is v0.10.21 and every artifact digest is the one
/// in that release's checksums.txt.
let lp11 =
    let proposal = json (path proposalRel) |> Option.toObj
    let filled (n: JsonNode) =
        match n with
        | null -> ""
        | n ->
            let o = n.DeepClone().AsObject()
            o.Remove "status" |> ignore
            (match o["indyInit"] with :? JsonObject as i -> i.Remove "commit" |> ignore | _ -> ())
            o.ToJsonString()
    let conditor = node lock "conditor"
    let commit = str (node lock "indyInit") "commit"
    let isCommit = Regex.IsMatch(commit, "^[0-9a-f]{40}$")
    let inHistory = isCommit && fst (git src [ "merge-base"; "--is-ancestor"; commit; "HEAD" ]) = 0
    let artifacts =
        match node conditor "artifacts" with
        | :? JsonObject as o -> o |> Seq.map (fun kv -> str kv.Value "name", str kv.Value "sha256") |> List.ofSeq
        | _ -> []
    let released =
        if offline || isNull lock then None else
        let dir = tempDir "conditor"
        let code, out = run src gh [ "release"; "download"; str conditor "tag"; "--repo"; "kemiller2002/conditor"; "--pattern"; "checksums.txt"; "--dir"; dir ]
        let file = Path.Combine(dir, "checksums.txt")
        let sums =
            if code = 0 && File.Exists file then
                File.ReadAllLines file |> Array.choose (fun l ->
                    match l.Split(' ', StringSplitOptions.RemoveEmptyEntries) with
                    | [| sum; name |] -> Some(name.TrimStart '*', sum.ToLowerInvariant())
                    | _ -> None) |> Map.ofArray |> Ok
            else Error $"""the release {str conditor "tag"} checksums.txt cannot be read: {out}"""
        (try Directory.Delete(dir, true) with _ -> ())
        Some sums
    checkPending "LP-11" "the lock is the proposal ODR-COMP-LOCK approved, and Conditor v0.10.21 is the released one" (
        if isNull lock then [ "no lock" ] else
        fail (not (isNull proposal)) $"{proposalRel}, the proposal ODR-COMP-LOCK approved, does not exist"
        @ fail (isNull proposal || filled lock = filled proposal) $"{lockRel} differs from {proposalRel} beyond status and indyInit.commit: the owner commits the approved proposal"
        @ fail isCommit $"indyInit.commit {commit} is not a commit (the owner fills the governing Indy-init commit)"
        @ fail (not isCommit || inHistory) $"indyInit.commit {commit} is not in this history"
        @ fail (str conditor "version" = "0.10.21" && str conditor "tag" = "v0.10.21") $"""the lock pins Conditor {str conditor "version"} ({str conditor "tag"}), not 0.10.21 (ODR-COMP-P14)"""
        @ fail (not artifacts.IsEmpty) "the lock pins no Conditor artifact"
        @ (match released with
           | None | Some(Error _) when offline -> []
           | Some(Error e) -> [ e ]
           | Some(Ok sums) ->
               artifacts |> List.collect (fun (name, sha) ->
                   match sums.TryFind name with
                   | Some s -> fail (s = sha) $"Conditor {name}: the lock pins {sha}, the release's checksums.txt has {s}"
                   | None -> [ $"Conditor {name} is not in the release's checksums.txt" ])
           | None -> []))
        (offlineNote $"""Conditor's digests are not compared with the {str conditor "tag"} release's checksums.txt""")

let checks = [ lp00; lp01; lp02; lp03; lp04; lp05; lp06; lp07; lp08; lp09; lp10 ] @ (if competition then [ lp11 ] else [])
let status (c: Check) = if not c.Causes.IsEmpty then "REFUSED" elif not c.Pending.IsEmpty then "PENDING" else "PASS"
checks |> List.iter (fun c ->
    printfn "%s %-8s %s" c.Id (status c) c.Title
    c.Causes |> List.iter (printfn "        %s")
    c.Pending |> List.iter (printfn "        pending: %s"))
let ready = not offline && checks |> List.forall (fun c -> c.Causes.IsEmpty && c.Pending.IsEmpty)
let verdict = if ready then "READY" elif offline && checks |> List.forall (fun c -> c.Causes.IsEmpty) then "NOT READY (offline: structural checks only; the pending ones need the network)" else "REFUSED"
opt "--out" |> Option.iter (fun out ->
    let j =
        JsonObject(
            [ Collections.Generic.KeyValuePair("schema", JsonValue.Create "indy-init.launch-preflight/1" :> JsonNode)
              Collections.Generic.KeyValuePair("verdict", JsonValue.Create(if ready then "READY" else "REFUSED") :> JsonNode)
              Collections.Generic.KeyValuePair("checks", JsonArray(checks |> List.map (fun c -> JsonObject([ Collections.Generic.KeyValuePair("id", JsonValue.Create c.Id :> JsonNode); Collections.Generic.KeyValuePair("title", JsonValue.Create c.Title :> JsonNode); Collections.Generic.KeyValuePair("causes", JsonArray(c.Causes |> List.map (fun x -> JsonValue.Create x :> JsonNode) |> Array.ofList) :> JsonNode) ]) :> JsonNode) |> Array.ofList) :> JsonNode) ])
    // The run and mode, and what an offline run could not judge, only when they differ from Rehearsal 010's online form.
    if competition || offline then
        j["run"] <- JsonValue.Create runName
        j["verdict"] <- JsonValue.Create(if ready then "READY" elif verdict = "REFUSED" then "REFUSED" else "NOT READY")
        j["offline"] <- JsonValue.Create offline
        checks |> List.iter (fun c ->
            let o = (j["checks"].AsArray() |> Seq.find (fun n -> str n "id" = c.Id)).AsObject()
            o["status"] <- JsonValue.Create(status c)
            o["pending"] <- JsonArray(c.Pending |> List.map (fun x -> JsonValue.Create x :> JsonNode) |> Array.ofList))
    File.WriteAllText(out, j.ToJsonString(Text.Json.JsonSerializerOptions(WriteIndented = true)) + "\n"))
if ownerKeys.IsValueCreated then (try Directory.Delete(fst ownerKeys.Value, true) with _ -> ())
printfn "launch-preflight: %s" verdict
exit (if ready then 0 else 4)
