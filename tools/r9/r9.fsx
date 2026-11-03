// r9.fsx: Rehearsal 009 readiness gates (FINAL-CORRECTIONS.md in docs/rehearsal-009-preflight-smoke).
//
//   dotnet fsi tools/r9/r9.fsx -- install-conditor --lock LOCK --dir DIR [--platform RID]
//   dotnet fsi tools/r9/r9.fsx -- workstation --lock LOCK --conditor EXE --home DIR [--platform RID] [--target-id ID] [--require-registration true]
//   dotnet fsi tools/r9/r9.fsx -- govern --indy-init SRC --target REPO
//   dotnet fsi tools/r9/r9.fsx -- check-governance --indy-init SRC --target REPO [--base-scope FILE]
//   dotnet fsi tools/r9/r9.fsx -- gate --obligations F --root REPO --results F --dokimos EXE --out DIR
//        (--mode release | --mode development --scope-file F) [--expect-candidate SHA]
//        [--work-item ID --scope REQ,...] [--matrix F]
//   dotnet fsi tools/r9/r9.fsx -- run-suites --obligations F --root REPO --out DIR [--suites ID,...]
//   dotnet fsi tools/r9/r9.fsx -- spec-preflight --percepta EXE --out F (--indy-init SRC --lock LOCK |
//        --machines F --invariants F --interfaces F --catalog F)
//
// Exit codes: 0 pass, 4 the gate refused (each refusal printed with its cause), 2 unusable input.
// Every command is read-only on its inputs; only govern writes, and only into the target repository.

open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions

// ---- plumbing ------------------------------------------------------------------------------------------------------

type Outcome =
    | Pass of string list
    | Refused of string list
    | Unusable of string

let args = fsi.CommandLineArgs |> Array.toList |> List.tail
let options = args |> List.pairwise |> List.filter (fun (k, _) -> k.StartsWith "--") |> Map.ofList
let opt name = options.TryFind name
let req name = opt name |> Option.defaultWith (fun () -> eprintfn "missing %s" name; exit 2)

let sha256Bytes (bytes: byte[]) = SHA256.HashData bytes |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()
let sha256File path = File.ReadAllBytes path |> sha256Bytes
let isSha (s: string) = not (isNull s) && Regex.IsMatch(s, "^[0-9a-f]{40}$")

let readJson path : JsonNode = JsonNode.Parse(File.ReadAllText path)
let str (n: JsonNode) (key: string) =
    match n with
    | null -> ""
    | n ->
        match n[key] with
        | null -> ""
        | v when v.GetValueKind() = JsonValueKind.String -> v.GetValue<string>()
        | v -> v.ToJsonString()
let arr (n: JsonNode) (key: string) : JsonNode list =
    match n with
    | null -> []
    | n ->
        match n[key] with
        | :? JsonArray as a -> a |> Seq.toList
        | _ -> []
let canonical (n: JsonNode) = if isNull n then "null" else n.ToJsonString(JsonSerializerOptions(WriteIndented = false))

let run (exe: string) (argv: string list) (cwd: string) : int * string =
    let info = ProcessStartInfo(exe, WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true)
    argv |> List.iter info.ArgumentList.Add
    use p = Process.Start info
    let out = p.StandardOutput.ReadToEndAsync()
    let err = p.StandardError.ReadToEndAsync()
    p.WaitForExit()
    p.ExitCode, out.Result + err.Result

let git root argv = run "git" ([ "-C"; root ] @ argv) root

let finish (name: string) (outcome: Outcome) =
    match outcome with
    | Pass notes ->
        notes |> List.iter (printfn "  %s")
        printfn "%s: PASS" name
        exit 0
    | Refused causes ->
        causes |> List.iter (printfn "  REFUSED %s")
        printfn "%s: REFUSED (%d cause(s))" name causes.Length
        exit 4
    | Unusable why ->
        eprintfn "%s: UNUSABLE INPUT: %s" name why
        exit 2

let check (cond: bool) (cause: string) = if cond then [] else [ cause ]

let platform () =
    opt "--platform"
    |> Option.defaultWith (fun () ->
        let os = if OperatingSystem.IsWindows() then "win" elif OperatingSystem.IsMacOS() then "osx" else "linux"
        let cpu = if Runtime.InteropServices.RuntimeInformation.OSArchitecture = Runtime.InteropServices.Architecture.Arm64 then "arm64" else "x64"
        $"{os}-{cpu}")

let download (url: string) : byte[] =
    use client = new HttpClient()
    client.DefaultRequestHeaders.UserAgent.ParseAdd "indy-init-r9"
    client.GetByteArrayAsync(url).Result

// ---- install-conditor: exactly the pinned binary, no fallback --------------------------------------------------------

let installConditor () =
    let lock = readJson (req "--lock")
    let c = lock["conditor"]
    let rid = platform ()
    match c["artifacts"][rid] with
    | null -> Unusable $"the lock pins no Conditor artifact for {rid}"
    | a ->
        let name, expected = str a "name", str a "sha256"
        let url = $"""{str c "downloadBase"}/{name}"""
        let bytes = download url
        let actual = sha256Bytes bytes
        let identity = $"""{str c "version"}+{str c "commit"}"""
        let embedded = (Encoding.UTF8.GetString bytes).Contains identity
        let causes =
            check (actual = expected) $"{url} has sha256 {actual}, the lock pins {expected}"
            @ check embedded $"the binary does not carry the pinned identity {identity}"
        if not causes.IsEmpty then Refused causes
        else
            let dir = Directory.CreateDirectory(req "--dir").FullName
            let exe = Path.Combine(dir, (if rid.StartsWith "win" then "conditor.exe" else "conditor"))
            File.WriteAllBytes(exe, bytes)
            if not (OperatingSystem.IsWindows()) then File.SetUnixFileMode(exe, File.GetUnixFileMode exe ||| UnixFileMode.UserExecute ||| UnixFileMode.GroupExecute)
            Pass [ $"installed {exe}"; $"conditor {identity} ({rid}) sha256 {actual} = lock"; $"source {url}" ]

// ---- workstation: plan, apply with an explicit logical target id, read back --------------------------------------

let resolvedSet (lock: JsonNode) (rid: string) (dir: string) =
    let rs = lock["resolvedSet"]
    let p = rs["platforms"][rid]
    let url = $"""https://raw.githubusercontent.com/{str rs "registry"}/{str rs "commit"}/{str p "path"}"""
    let bytes = download url
    let path = Path.Combine(dir, $"resolved-set-{rid}.json")
    File.WriteAllBytes(path, bytes)
    path, sha256Bytes bytes, str p "sha256", url

let workstation () =
    let lock = readJson (req "--lock")
    let conditor = req "--conditor"
    let home = Directory.CreateDirectory(req "--home").FullName
    let targetId = opt "--target-id" |> Option.defaultValue (str lock "workstationTargetId")
    let rid = platform ()
    let set, actual, expected, url = resolvedSet lock rid home
    if actual <> expected then Refused [ $"{url} has sha256 {actual}, the lock pins {expected}" ]
    else
        let setArgs = [ "--resolved-set"; set; "--resolved-set-sha256"; expected; "--home"; home ]
        let planCode, plan = run conditor ([ "workstation"; "plan" ] @ setArgs) home
        let digest = Regex.Match(plan, "--authorize (sha256:[0-9a-f]{64})")
        if planCode <> 0 || not digest.Success then Refused [ $"workstation plan failed (exit {planCode}): {plan}" ]
        else
            let applyCode, apply = run conditor ([ "workstation"; "apply"; "--authorize"; digest.Groups[1].Value; "--target-id"; targetId ] @ setArgs) home
            let _, status = run conditor [ "workstation"; "status"; "--home"; home; "--json" ] home
            let expectedNode: JsonNode = lock["resolvedSet"]["expectedTools"]
            let expectedTools = expectedNode.AsObject()
            let tools = expectedTools |> Seq.map (fun kv -> kv.Key, kv.Value.GetValue<string>()) |> List.ofSeq
            let bin = Path.Combine(home, ".local", "bin")
            let versionOf (tool: string) =
                let exe = Path.Combine(bin, (if tool = "percepta-semantic" then "percepta" else tool))
                if not (File.Exists exe) then "" else snd (run exe (if tool = "dokimos" then [ "version" ] else [ "--version" ]) home)
            // Read back from Conditor's append-only ledger: the latest registration outcome of each component.
            let ledger = Path.Combine(home, ".conditor", "workstation", "ledger.jsonl")
            let registrationLines =
                (if File.Exists ledger then File.ReadAllLines ledger |> List.ofArray else [])
                |> List.filter (fun l -> l.Contains "\"entry\":\"registration\"")
                |> List.map JsonNode.Parse
                |> List.fold (fun (m: Map<string, JsonNode>) e -> m.Add(str e "component", e)) Map.empty
                |> Map.toList
                |> List.map (fun (c, e) -> $"""registration {c}: {str e "outcome"} ({str e "detail"})""")
            let causes =
                check (applyCode = 0) $"workstation apply exited {applyCode}: {apply}"
                // recorded/replayed/unchanged/submitted succeed; invalid/refused/misconfigured are defects; unavailable
                // (no Project Administration integration) is optional by Praxis's contract unless registration is required.
                @ (registrationLines |> List.filter (fun l -> Regex.IsMatch(l, @"registration \S+: (invalid|refused|misconfigured)")) |> List.map (fun l -> $"registration failed: {l.Trim()}"))
                @ (if opt "--require-registration" = Some "true" then registrationLines |> List.filter (fun l -> not (Regex.IsMatch(l, @"registration \S+: (recorded|replayed|unchanged|submitted)"))) |> List.map (fun l -> $"registration required but not recorded: {l.Trim()}") else [])
                @ check (not registrationLines.IsEmpty) "the ledger records no registration attempt"
                @ (tools |> List.collect (fun (t, v) -> check ((versionOf t).Contains v) $"{t} does not report {v}"))
            if causes.IsEmpty then
                Pass ([ $"resolved set {url} sha256 {actual} = lock"; $"plan {digest.Groups[1].Value}"; $"target id {targetId}" ]
                      @ (registrationLines |> List.map (fun l -> l.Trim())) @ (tools |> List.map (fun (t, v) -> $"{t} {v} installed")) @ [ $"status: {status.Trim()}" ])
            else Refused causes

// ---- govern / check-governance: the authoritative completion policy ------------------------------------------------

let governanceOf (src: string) = readJson (Path.Combine(src, "tools", "r9", "governance.json"))

let govern () =
    let src = Path.GetFullPath(req "--indy-init")
    let target = Path.GetFullPath(req "--target")
    let g = governanceOf src
    let rosPath = Path.Combine(target, "ros.json")
    if not (File.Exists rosPath) then Unusable $"{rosPath} does not exist; run conditor init first"
    else
        let ros = readJson rosPath
        ros["workProtocol"]["qualityEvidence"] <- g["qualityEvidence"].DeepClone()
        File.WriteAllText(rosPath, ros.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
        let workflow = Path.Combine(target, ".github", "workflows", "verification-gate.yml")
        Directory.CreateDirectory(Path.GetDirectoryName workflow) |> ignore
        File.Copy(Path.Combine(src, str g "workflow"), workflow, true)
        let vdir = Directory.CreateDirectory(Path.Combine(target, str g "verificationDirectory")).FullName
        let scope = Path.Combine(target, ".echelon", "verification-scope.json")
        if not (File.Exists scope) then File.WriteAllText(scope, "{\n  \"schema\": \"indy-init.r9-verification-scope/1\",\n  \"requirements\": []\n}\n")
        let current = Path.Combine(target, ".ros", "context", "current.json")
        let preexisting =
            if File.Exists current then
                arr (readJson current) "workItems" |> List.filter (fun w -> str w "state" = "complete") |> List.map (fun w -> JsonValue.Create(str w "id") :> JsonNode)
            else []
        let _, srcCommit = git src [ "rev-parse"; "HEAD" ]
        let record =
            JsonObject(
                [ Collections.Generic.KeyValuePair("schema", JsonValue.Create "indy-init.r9-governance-record/1" :> JsonNode)
                  Collections.Generic.KeyValuePair("indyInitCommit", JsonValue.Create(srcCommit.Trim()) :> JsonNode)
                  Collections.Generic.KeyValuePair("policySha256", JsonValue.Create(sha256Bytes (Encoding.UTF8.GetBytes(canonical g.["qualityEvidence"]))) :> JsonNode)
                  Collections.Generic.KeyValuePair("workflowSha256", JsonValue.Create(sha256File workflow) :> JsonNode)
                  Collections.Generic.KeyValuePair("completedBeforeGovernance", JsonArray(preexisting |> Array.ofList) :> JsonNode) ])
        File.WriteAllText(Path.Combine(target, ".echelon", "r9-governance.json"), record.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
        let policy = canonical g.["qualityEvidence"]
        Pass [ $"ros.json qualityEvidence = {policy}"; $"wrote {workflow}"; $"verification evidence directory {vdir}"
               $"{preexisting.Length} item(s) completed before governance are recorded and left as they are" ]

/// The gate verdicts committed under the verification directory, keyed by the matrix sha256 they passed.
let passedMatrices (target: string) (vdir: string) =
    let dir = Path.Combine(target, vdir)
    if not (Directory.Exists dir) then Map.empty
    else
        Directory.EnumerateFiles(dir, "gate.json", SearchOption.AllDirectories)
        |> Seq.map readJson
        |> Seq.filter (fun v -> str v "verdict" = "PASS" && str v "matrixSha256" <> "")
        |> Seq.map (fun v -> "sha256:" + str v "matrixSha256", v)
        |> Map.ofSeq

let checkGovernance () =
    let src = Path.GetFullPath(req "--indy-init")
    let target = Path.GetFullPath(req "--target")
    let g = governanceOf src
    let ros = readJson (Path.Combine(target, "ros.json"))
    let recordPath = Path.Combine(target, ".echelon", "r9-governance.json")
    let workflow = Path.Combine(target, ".github", "workflows", "verification-gate.yml")
    let policyCauses =
        check (canonical ros.["workProtocol"].["qualityEvidence"] = canonical g.["qualityEvidence"])
            $"""ros.json workProtocol.qualityEvidence differs from Indy-init tools/r9/governance.json (found {canonical ros.["workProtocol"].["qualityEvidence"]}); restore it with `r9.fsx govern`, never by editing the authoritative copy"""
        @ check (File.Exists workflow && sha256File workflow = sha256File (Path.Combine(src, str g "workflow")))
            ".github/workflows/verification-gate.yml is missing or differs from Indy-init tools/r9/verification-gate.yml"
        @ check (File.Exists recordPath) ".echelon/r9-governance.json is missing: the repository was not governed at T0"
    let record = if File.Exists recordPath then readJson recordPath else null
    let before = arr record "completedBeforeGovernance" |> List.map (fun n -> n.GetValue<string>()) |> Set.ofList
    let current = Path.Combine(target, ".ros", "context", "current.json")
    let verdicts = passedMatrices target (str g "verificationDirectory")
    let items = if File.Exists current then arr (readJson current) "workItems" |> List.filter (fun w -> str w "state" = "complete") else []
    let itemCauses (w: JsonNode) =
        let id = str w "id"
        let cr = w["completionReadiness"]
        let facets = arr (if isNull cr then null else cr["policy"]) "requiredFacets" |> List.map (fun n -> n.GetValue<string>())
        let matrixStatus = if isNull cr then "" else str cr.["facets"].["verificationMatrixSatisfied"] "status"
        let consumed = arr cr "consumedEvidence" |> List.filter (fun e -> str e "type" = "verification-matrix") |> List.map (fun e -> str e "sha256")
        if before.Contains id then []
        else
            check (not (isNull cr)) $"{id} was completed without a completion-readiness record (completed under no policy)"
            @ check (List.contains "verification-matrix-satisfied" facets) $"{id} was completed under a policy without verification-matrix-satisfied (the facet was removed)"
            @ check (matrixStatus = "satisfied") $"{id}: verification-matrix-satisfied is '{matrixStatus}', not satisfied"
            @ check (consumed |> List.exists verdicts.ContainsKey)
                $"""{id}: its verification matrix ({String.Join(", ", consumed)}) matches no PASS gate verdict under {str g "verificationDirectory"}; run `r9.fsx gate --work-item {id}` at the candidate and commit its output"""
    let scopeCauses =
        match opt "--base-scope" with
        | Some basePath when File.Exists basePath ->
            let reqsOf p = arr (readJson p) "requirements" |> List.map (fun n -> n.GetValue<string>()) |> Set.ofList
            let removed = Set.difference (reqsOf basePath) (reqsOf (Path.Combine(target, ".echelon", "verification-scope.json")))
            check removed.IsEmpty $"""the development verification scope may only grow; removed: {String.Join(", ", removed)}"""
        | _ -> []
    let declared =
        let p = Path.Combine(target, ".echelon", "verification-scope.json")
        if File.Exists p then arr (readJson p) "requirements" |> List.map (fun n -> n.GetValue<string>()) |> Set.ofList else Set.empty
    let undeclared =
        verdicts |> Map.toList |> List.collect (fun (_, v) -> arr v "scope" |> List.map (fun n -> n.GetValue<string>()))
        |> List.filter (declared.Contains >> not) |> List.distinct
    let declarationCauses =
        check undeclared.IsEmpty $"""requirements verified for completed work are missing from .echelon/verification-scope.json, so later merges would not re-verify them: {String.Join(", ", undeclared)}"""
    let causes = policyCauses @ (items |> List.collect itemCauses) @ scopeCauses @ declarationCauses
    if causes.IsEmpty then Pass [ $"policy, workflow and governance record match Indy-init; {items.Length} completed item(s) checked" ] else Refused causes

// ---- gate: Dokimos coverage at the trusted candidate, with the trust-boundary checks ------------------------------

let executable (identity: string) = identity.StartsWith "dotnet:" || identity.StartsWith "playwright:"

/// The frozen plan as Dokimos can verify it: record identities (praxis:*, verifier:*), which Dokimos reports as
/// blocked for their producer and never judges, are removed, and obligations bound only to record evidence are listed
/// as deferred to the acceptance verifier. Nothing else changes, so every executable identity is still required.
let executableView (obligations: JsonNode) =
    let view = obligations.DeepClone()
    let tests = arr obligations "tests"
    let deferredIdentities = tests |> List.filter (fun t -> not (executable (str t "id"))) |> List.map (fun t -> str t "id") |> List.distinct
    let bound = tests |> List.filter (fun t -> executable (str t "id")) |> List.collect (fun t -> arr t "obligations" |> List.map (fun o -> o.GetValue<string>())) |> Set.ofList
    let deferredObligations = arr obligations "obligations" |> List.map (fun o -> str o "id") |> List.filter (bound.Contains >> not)
    view["tests"] <- JsonArray(tests |> List.filter (fun t -> executable (str t "id")) |> List.map (fun t -> t.DeepClone()) |> Array.ofList)
    view["obligations"] <- JsonArray(arr obligations "obligations" |> List.filter (fun o -> bound.Contains(str o "id")) |> List.map (fun o -> o.DeepClone()) |> Array.ofList)
    view, deferredObligations, deferredIdentities

let gate () =
    let root = Path.GetFullPath(req "--root")
    let out = Directory.CreateDirectory(req "--out").FullName
    let obligationsPath = req "--obligations"
    let resultsPath = req "--results"
    let mode = req "--mode"
    let headCode, headOut = git root [ "rev-parse"; "HEAD" ]
    let head = headOut.Trim()
    // Only tracked source counts: Praxis state and verification evidence are written during the run.
    let dirtyCode, dirty = git root [ "status"; "--porcelain"; "--untracked-files=no"; "--"; "."; ":(exclude).ros"; ":(exclude).echelon/verification" ]
    let expected = opt "--expect-candidate"
    let frozen = readJson obligationsPath
    let obligations, deferredRecord, deferredIdentities = executableView frozen
    let viewPath = Path.Combine(out, "executable-obligations.json")
    File.WriteAllText(viewPath, obligations.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
    let scope =
        match mode, opt "--scope", opt "--scope-file" with
        | _, Some s, _ -> s.Split(',', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
        | "development", None, Some f -> arr (readJson f) "requirements" |> List.map (fun n -> n.GetValue<string>())
        | _ -> []
    let candidateCauses =
        check (headCode = 0 && isSha head) $"the repository HEAD is not a commit ({headOut.Trim()})"
        @ check (dirtyCode = 0 && dirty.Trim() = "") $"the working tree differs from HEAD, so HEAD is not what ran:\n{dirty}"
        @ (match expected with
           | Some e -> check (isSha e) $"--expect-candidate '{e}' is not a 40-character SHA" @ check (e = head) $"CI's candidate {e} is not the checked-out HEAD {head}"
           | None -> [])
        @ check (mode = "release" || mode = "development") $"--mode must be release or development, not '{mode}'"
        @ check (mode <> "development" || not scope.IsEmpty || opt "--scope-file" |> Option.isSome) "development mode needs --scope-file"
        @ check (mode <> "release" || opt "--scope" |> Option.isNone || opt "--work-item" |> Option.isSome) "release mode verifies the whole plan; --scope is only for a work item's matrix"
    let results = readJson resultsPath
    let runs = arr results "runs"
    let resultsDir = Path.GetDirectoryName(Path.GetFullPath resultsPath)
    let inScope (o: JsonNode) = scope.IsEmpty || List.contains (str o "requirement") scope
    let scopedObligations = arr obligations "obligations" |> List.filter inScope |> List.map (fun o -> str o "id") |> Set.ofList
    let neededSuites =
        arr obligations "tests"
        |> List.filter (fun t -> executable (str t "id") && (arr t "obligations" |> List.exists (fun o -> scopedObligations.Contains(o.GetValue<string>()))))
        |> List.map (fun t -> str t "suite") |> List.distinct
    let resultCauses =
        check (str results "schema" = "dokimos.verification-results/1") "the results manifest is not dokimos.verification-results/1"
        @ check (str results "candidate" = head) $"""the results manifest names candidate '{str results "candidate"}', not HEAD {head}"""
        @ (runs |> List.collect (fun r ->
            check (str r "sourceSha" = head) $"""run {str r "suite"} was recorded at '{str r "sourceSha"}', not HEAD {head} (stale evidence)"""
            @ check (File.Exists(Path.Combine(resultsDir, str r "path"))) $"""run {str r "suite"}: result file {str r "path"} does not exist"""))
        @ (neededSuites |> List.collect (fun s -> check (runs |> List.exists (fun r -> str r "suite" = s)) $"suite {s} has in-scope obligations but no execution evidence in the results manifest"))
    let emptyDevelopmentScope = mode = "development" && scope.IsEmpty && opt "--scope" |> Option.isNone
    if not candidateCauses.IsEmpty then Refused candidateCauses
    elif emptyDevelopmentScope then
        let causes = check (str results "schema" = "dokimos.verification-results/1" && str results "candidate" = head) "the results manifest is not dokimos.verification-results/1 at HEAD"
        if causes.IsEmpty then Pass [ $"candidate {head}: the development scope (.echelon/verification-scope.json) is empty, so nothing is claimed verified yet; every completed work item adds its requirements (check-governance enforces it)" ]
        else Refused causes
    else
        let coveragePath = Path.Combine(out, "coverage.json")
        let matrixOut = opt "--work-item" |> Option.map (fun _ -> Path.Combine(out, "praxis-matrix.json"))
        let dokimosArgs =
            [ "verification"; "coverage"; "--obligations"; viewPath; "--root"; root; "--results"; Path.GetFullPath resultsPath
              "--candidate"; head; "--require-verified"; "true"; "--out"; coveragePath ]
            @ (if scope.IsEmpty then [] else [ "--scope"; String.Join(",", scope) ])
            @ (match matrixOut, opt "--work-item" with
               | Some m, Some wi -> [ "--praxis-matrix"; m; "--work-item"; wi; "--dimensions"; opt "--dimensions" |> Option.defaultValue "happy-path,negative,corruption,compatibility,recovery,concurrency" ]
               | _ -> [])
        let dokimosCode, dokimosOut = run (req "--dokimos") dokimosArgs root
        File.WriteAllText(Path.Combine(out, "dokimos.log"), $"dokimos {String.Join(' ', dokimosArgs)}\nexit {dokimosCode}\n{dokimosOut}")
        if not (File.Exists coveragePath) then Refused (resultCauses @ [ $"dokimos verification coverage produced no report (exit {dokimosCode}): {dokimosOut}" ])
        else
            let cov = readJson coveragePath
            let obligationsCov = arr cov "obligations"
            let inScopeIds = arr frozen "obligations" |> List.filter inScope |> List.map (fun o -> str o "id") |> Set.ofList
            let deferred = deferredRecord |> List.filter inScopeIds.Contains
            let unverified = obligationsCov |> List.filter (fun o -> str o "status" <> "verified")
            let coverageCauses =
                check (str cov "candidate" = head) $"""the coverage report names candidate '{str cov "candidate"}', not HEAD {head}"""
                @ check (str cov "planId" = str obligations "planId" && str cov "planDigest" = str obligations "planDigest") "the coverage report was computed for another plan"
                @ check (arr cov "rejectedRuns" |> List.isEmpty) $"""Dokimos rejected runs: {canonical cov.["rejectedRuns"]}"""
                @ check (dokimosCode = 0 || not unverified.IsEmpty) $"dokimos verification coverage --require-verified true exited {dokimosCode}: {dokimosOut}"
                @ (unverified |> List.map (fun o ->
                    $"""{str o "id"} ({str o "requirement"}) is {str o "status"}: {String.Join("; ", arr o "reasons" |> List.map (fun r -> r.GetValue<string>()))}"""))
                @ check (not obligationsCov.IsEmpty) "no executable obligation is in scope: an empty verification proves nothing (record-only obligations are judged by the acceptance verifier, not here)"
            let matrixPath = opt "--matrix" |> Option.orElse matrixOut
            let matrixCauses =
                match matrixPath with
                | None -> []
                | Some m when not (File.Exists m) -> [ $"verification matrix {m} does not exist" ]
                | Some m ->
                    let matrix = readJson m
                    let statusOf = obligationsCov |> List.collect (fun o -> arr o "identities") |> List.map (fun i -> str i "id", str i "status") |> Map.ofList
                    let evidence = arr matrix "rows" |> List.collect (fun r -> arr r "evidence")
                    let commands = evidence |> List.filter (fun e -> str e "kind" = "command") |> List.map (fun e -> str e "reference")
                    let tests = evidence |> List.filter (fun e -> (str e "kind").EndsWith "-test")
                    check (not commands.IsEmpty && commands |> List.forall (fun c -> c.Contains $"--candidate {head} ")) $"the matrix's coverage evidence does not name HEAD {head} (stale or hand-written matrix)"
                    @ (tests |> List.collect (fun e ->
                        let id = str e "reference"
                        match statusOf.TryFind id with
                        | None -> [ $"the matrix cites {id}, which is not a bound identity of the in-scope obligations" ]
                        | Some s -> check (s = str e "result") $"""the matrix says {id} {str e "result"}, Dokimos says {s} at HEAD"""))
            let causes = resultCauses @ coverageCauses @ matrixCauses
            let verdict =
                JsonObject(
                    [ Collections.Generic.KeyValuePair("schema", JsonValue.Create "indy-init.r9-gate-verdict/1" :> JsonNode)
                      Collections.Generic.KeyValuePair("verdict", JsonValue.Create(if causes.IsEmpty then "PASS" else "REFUSED") :> JsonNode)
                      Collections.Generic.KeyValuePair("mode", JsonValue.Create mode :> JsonNode)
                      Collections.Generic.KeyValuePair("candidate", JsonValue.Create head :> JsonNode)
                      Collections.Generic.KeyValuePair("planId", JsonValue.Create(str obligations "planId") :> JsonNode)
                      Collections.Generic.KeyValuePair("scope", JsonArray(scope |> List.map (fun s -> JsonValue.Create s :> JsonNode) |> Array.ofList) :> JsonNode)
                      Collections.Generic.KeyValuePair("workItem", JsonValue.Create(opt "--work-item" |> Option.defaultValue "") :> JsonNode)
                      Collections.Generic.KeyValuePair("dokimosExit", JsonValue.Create dokimosCode :> JsonNode)
                      Collections.Generic.KeyValuePair("obligations", JsonValue.Create obligationsCov.Length :> JsonNode)
                      Collections.Generic.KeyValuePair("verified", JsonValue.Create(obligationsCov |> List.filter (fun o -> str o "status" = "verified") |> List.length) :> JsonNode)
                      Collections.Generic.KeyValuePair("deferredToAcceptanceVerifier", JsonArray(deferred |> List.map (fun d -> JsonValue.Create d :> JsonNode) |> Array.ofList) :> JsonNode)
                      Collections.Generic.KeyValuePair("recordIdentitiesNotJudgedByDokimos", JsonValue.Create deferredIdentities.Length :> JsonNode)
                      Collections.Generic.KeyValuePair("frozenPlanSha256", JsonValue.Create(sha256File obligationsPath) :> JsonNode)
                      Collections.Generic.KeyValuePair("coverageSha256", JsonValue.Create(sha256File coveragePath) :> JsonNode)
                      Collections.Generic.KeyValuePair("resultsSha256", JsonValue.Create(sha256File resultsPath) :> JsonNode)
                      Collections.Generic.KeyValuePair("matrixSha256", JsonValue.Create(matrixPath |> Option.filter File.Exists |> Option.map sha256File |> Option.defaultValue "") :> JsonNode)
                      Collections.Generic.KeyValuePair("causes", JsonArray(causes |> List.map (fun c -> JsonValue.Create c :> JsonNode) |> Array.ofList) :> JsonNode) ])
            File.WriteAllText(Path.Combine(out, "gate.json"), verdict.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
            let summary =
                let scopeText = if scope.IsEmpty then ", whole plan" else ", scope " + String.Join(",", scope)
                [ $"candidate {head} ({mode}{scopeText})"
                  $"""{str verdict "verified"}/{obligationsCov.Length} obligations verified; {deferred.Length} deferred to the acceptance verifier (record evidence only)"""
                  $"""verdict {Path.Combine(out, "gate.json")}""" ]
            if causes.IsEmpty then Pass summary else (summary |> List.iter (printfn "  %s"); Refused causes)

// ---- run-suites: execute the plan's own suite commands and record machine-readable results at HEAD -----------------

/// Runs each selected executable suite exactly as the frozen plan states it, adding only a machine-readable logger,
/// and writes dokimos.verification-results/1 with sourceSha = the HEAD that ran (never an asserted SHA).
let runSuites () =
    let root = Path.GetFullPath(req "--root")
    let out = Directory.CreateDirectory(req "--out").FullName
    let obligations = readJson (req "--obligations")
    let selected = opt "--suites" |> Option.map (fun s -> s.Split(',', StringSplitOptions.RemoveEmptyEntries) |> Set.ofArray)
    let _, headOut = git root [ "rev-parse"; "HEAD" ]
    let head = headOut.Trim()
    let suites =
        arr obligations "suites"
        |> List.filter (fun s -> (str s "runner" = "dotnet-test" || str s "runner" = "playwright") && (selected |> Option.forall (fun set -> set.Contains(str s "id"))))
    let runOne (s: JsonNode) =
        let id = str s "id"
        let dir = Directory.CreateDirectory(Path.Combine(out, id)).FullName
        let command, format =
            if str s "runner" = "dotnet-test" then $"""{str s "command"} --logger trx --results-directory "{dir}" """, "trx"
            else $"""PLAYWRIGHT_JSON_OUTPUT_NAME="{dir}/results.json" {str s "command"} --reporter=json""", "playwright-json"
        let code, output = run "sh" [ "-c"; command ] root
        File.WriteAllText(Path.Combine(dir, "run.log"), $"$ {command}\nexit {code}\n{output}")
        let files = Directory.EnumerateFiles(dir, (if format = "trx" then "*.trx" else "results.json"), SearchOption.AllDirectories) |> List.ofSeq
        id, code, files |> List.map (fun f -> Path.GetRelativePath(out, f), format)
    let ran = suites |> List.map runOne
    let runs =
        ran |> List.collect (fun (id, _, files) ->
            files |> List.map (fun (path, format) ->
                JsonObject(
                    [ Collections.Generic.KeyValuePair("suite", JsonValue.Create id :> JsonNode)
                      Collections.Generic.KeyValuePair("format", JsonValue.Create format :> JsonNode)
                      Collections.Generic.KeyValuePair("path", JsonValue.Create path :> JsonNode)
                      Collections.Generic.KeyValuePair("sourceSha", JsonValue.Create head :> JsonNode) ]) :> JsonNode))
    let manifest =
        JsonObject(
            [ Collections.Generic.KeyValuePair("schema", JsonValue.Create "dokimos.verification-results/1" :> JsonNode)
              Collections.Generic.KeyValuePair("candidate", JsonValue.Create head :> JsonNode)
              Collections.Generic.KeyValuePair("runs", JsonArray(runs |> Array.ofList) :> JsonNode) ])
    File.WriteAllText(Path.Combine(out, "results.json"), manifest.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
    // A failing suite is not a gate failure here: the gate judges every identity from the recorded results.
    let missing = ran |> List.filter (fun (_, _, files) -> files.IsEmpty) |> List.map (fun (id, code, _) -> $"suite {id} produced no result file (exit {code}); see {id}/run.log")
    if suites.IsEmpty then Unusable "no executable suite selected"
    elif missing.IsEmpty then Pass (ran |> List.map (fun (id, code, files) -> $"{id}: exit {code}, {files.Length} result file(s)") |> fun l -> l @ [ $"results {out}/results.json at {head}" ])
    else Refused missing

// ---- spec-preflight: Percepta must analyse the intended, non-empty package -----------------------------------------

let legalTransitions (machines: JsonNode) =
    arr machines "machines" |> List.sumBy (fun m -> arr m "pairs" |> List.filter (fun p -> str p "outcome" = "legal") |> List.sumBy (fun p -> (arr p "transitions").Length))

let catalogCommands (markdown: string) =
    markdown.Replace("\r", "").Split('\n')
    |> Array.fold
        (fun (inTable, n) (line: string) ->
            let cells = line.Trim().Trim('|').Split('|') |> Array.map (fun c -> c.Trim().Trim('`'))
            if line.TrimStart().StartsWith "|" && cells.Length > 1 && cells[0] = "Command" then true, n
            elif line.TrimStart().StartsWith "|" && inTable then true, (if Regex.IsMatch(cells[0], "^[A-Z][A-Za-z]+$") then Set.add cells[0] n else n)
            else false, n)
        (false, Set.empty)
    |> snd
    |> Set.count // distinct names, as Percepta reads them

let specPreflight () =
    let lock = opt "--lock" |> Option.map readJson
    let src = opt "--indy-init" |> Option.map Path.GetFullPath
    let package, packageCauses =
        match lock, src with
        | Some l, Some s ->
            let p = l["requirementsPackage"]
            let dir = Path.Combine(s, str p "path")
            let manifest = Path.Combine(dir, "package-manifest.json")
            let actual = if File.Exists manifest then sha256File manifest else "(missing)"
            Some(dir, Path.Combine(s, str p "base"), s),
            check (actual = str p "manifestSha256") $"""{manifest} has sha256 {actual}, the lock pins package {str p "packageVersion"} at {str p "manifestSha256"}"""
        | _ -> None, []
    let input name fromPackage =
        match opt name, package with
        | Some f, _ -> Some f
        | None, Some(dir, b, s) -> Some(fromPackage dir b s)
        | None, None -> None
    let inputs =
        [ "--machines", input "--machines" (fun d _ _ -> Path.Combine(d, "state-machine-contracts.json"))
          "--invariants", input "--invariants" (fun _ b _ -> Path.Combine(b, "invariants.json"))
          "--interfaces", input "--interfaces" (fun d _ _ -> Path.Combine(d, "analysis", "interface-specifications.json"))
          "--catalog", input "--catalog" (fun _ _ s -> Path.Combine(s, "docs", "COMMAND-EVENT-CATALOG.md")) ]
    let inputCauses =
        inputs |> List.collect (fun (flag, f) ->
            match f with
            | None -> [ $"no {flag} input: name the package (--indy-init and --lock) or every input file" ]
            | Some f when not (File.Exists f) -> [ $"{flag} {f} does not exist" ]
            | Some f when (FileInfo f).Length = 0L -> [ $"{flag} {f} is empty" ]
            | Some _ -> [])
    if not (inputCauses.IsEmpty && packageCauses.IsEmpty) then Refused (packageCauses @ inputCauses)
    else
        let path flag = inputs |> List.find (fst >> (=) flag) |> snd |> Option.get
        let parsed = try Ok(readJson (path "--machines"), readJson (path "--invariants"), readJson (path "--interfaces")) with e -> Error e.Message
        match parsed with
        | Error e -> Refused [ $"an input is not JSON: {e}" ]
        | Ok(machines, invariants, interfaces) ->
            let expected =
                [ "machines", (arr machines "machines").Length
                  "transitions", legalTransitions machines
                  "invariants", (arr invariants "invariants").Length
                  "operations", arr interfaces "interfaces" |> List.sumBy (fun i -> (arr i "operations").Length)
                  "catalogCommands", catalogCommands (File.ReadAllText(path "--catalog")) ]
            let reportPath = req "--out"
            let rawPath = reportPath + ".percepta.json"
            let code, output = run (req "--percepta") ([ "spec-preflight" ] @ (inputs |> List.collect (fun (f, p) -> [ f; Path.GetFullPath(Option.get p) ])) @ [ "--out"; Path.GetFullPath rawPath ]) Environment.CurrentDirectory
            if not (File.Exists rawPath) then Refused [ $"percepta spec-preflight wrote no report (exit {code}): {output}" ]
            else
                let report = readJson rawPath
                let analysed = report["analysed"]
                let count (key: string) = match analysed with | null -> -1 | a -> (match a[key] with | null -> -1 | v -> v.GetValue<int>())
                let blockingSections = [ "blockingContradictions"; "missingInterfaces"; "missingDecisions"; "unsupportedVerificationObligations" ]
                let findings = blockingSections |> List.collect (fun s -> arr report s |> List.filter (fun f -> str f "blocking" <> "false") |> List.map (fun f -> $"""{s}: {str f "rule"} {str f "detail"}"""))
                let causes =
                    check (str report "schema" = "percepta.specification-preflight/1") "the report is not percepta.specification-preflight/1"
                    @ check (code = 0) $"percepta spec-preflight exited {code}"
                    @ check (str report "blocked" = "false") "the report is blocked"
                    @ findings
                    @ (expected |> List.collect (fun (k, n) ->
                        check (n > 0) $"the input holds no {k}: an empty specification proves nothing"
                        @ check (count k = n) $"Percepta analysed {count k} {k}; the inputs hold {n}"))
                    @ check (count "types" > 0) "Percepta analysed no types"
                let envelope =
                    JsonObject(
                        [ Collections.Generic.KeyValuePair("schema", JsonValue.Create "indy-init.r9-spec-preflight/1" :> JsonNode)
                          Collections.Generic.KeyValuePair("verdict", JsonValue.Create(if causes.IsEmpty then "PASS" else "REFUSED") :> JsonNode)
                          Collections.Generic.KeyValuePair("package", JsonValue.Create(match lock with | Some l -> $"""{str l.["requirementsPackage"] "path"} {str l.["requirementsPackage"] "packageVersion"} manifest {str l.["requirementsPackage"] "manifestSha256"}""" | None -> "(explicit inputs, no package)") :> JsonNode)
                          Collections.Generic.KeyValuePair("inputs", JsonObject(inputs |> List.map (fun (f, p) -> Collections.Generic.KeyValuePair(f.TrimStart('-'), JsonValue.Create($"{Option.get p} sha256 {sha256File (Option.get p)}") :> JsonNode))) :> JsonNode)
                          Collections.Generic.KeyValuePair("expected", JsonObject(expected |> List.map (fun (k, n) -> Collections.Generic.KeyValuePair(k, JsonValue.Create n :> JsonNode))) :> JsonNode)
                          Collections.Generic.KeyValuePair("perceptaExit", JsonValue.Create code :> JsonNode)
                          Collections.Generic.KeyValuePair("report", report.DeepClone())
                          Collections.Generic.KeyValuePair("causes", JsonArray(causes |> List.map (fun c -> JsonValue.Create c :> JsonNode) |> Array.ofList) :> JsonNode) ])
                File.WriteAllText(reportPath, envelope.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
                let summary = expected |> List.map (fun (k, n) -> $"{k}: {count k} analysed / {n} in the inputs") |> fun l -> l @ [ $"""types: {count "types"}"""; $"report {reportPath}" ]
                if causes.IsEmpty then Pass summary else (summary |> List.iter (printfn "  %s"); Refused causes)

// ---- dispatch ------------------------------------------------------------------------------------------------------

match args with
| "install-conditor" :: _ -> installConditor () |> finish "install-conditor"
| "workstation" :: _ -> workstation () |> finish "workstation"
| "govern" :: _ -> govern () |> finish "govern"
| "check-governance" :: _ -> checkGovernance () |> finish "check-governance"
| "gate" :: _ -> gate () |> finish "gate"
| "run-suites" :: _ -> runSuites () |> finish "run-suites"
| "spec-preflight" :: _ -> specPreflight () |> finish "spec-preflight"
| _ ->
    eprintfn "usage: dotnet fsi tools/r9/r9.fsx -- install-conditor|workstation|govern|check-governance|gate|spec-preflight ... (see the header)"
    exit 2
