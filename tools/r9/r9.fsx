// r9.fsx: Rehearsal 009 readiness gates (FINAL-CORRECTIONS.md in docs/rehearsal-009-preflight-smoke).
//
//   dotnet fsi tools/r9/r9.fsx -- install-conditor --lock LOCK --dir DIR [--platform RID]
//   dotnet fsi tools/r9/r9.fsx -- workstation --lock LOCK --conditor EXE --home DIR [--platform RID] [--target-id ID] [--require-registration true]
//   dotnet fsi tools/r9/r9.fsx -- govern --indy-init SRC --target REPO [--lock kickoff/rehearsal-0NN/toolchain.lock.json]
//   dotnet fsi tools/r9/r9.fsx -- check-governance --indy-init SRC --target REPO [--base-scope FILE]
//   dotnet fsi tools/r9/r9.fsx -- gate --obligations F --root REPO --results F --dokimos EXE --out DIR
//        (--mode release | --mode development --scope-file F) [--expect-candidate SHA]
//        [--work-item ID --scope REQ,...] [--matrix F]
//        [--rc REQUIREMENTS-COMPILER.dll --process-plan F --process-plan-digest HEX --process-rules F --process-rules-sha256 HEX
//         --process-attestations F --process-trusted-run-id ID [--process-events F --process-ancestry F [--process-executions DIR]]]
//        [--probes F,... --require-fault-sensitive CATEGORY,...]
//        Evidence type comes only from the plan's suite runner: executable obligations need Dokimos at HEAD, record
//        obligations (praxis:*) need verify-process VERIFIED at HEAD, mixed ones need both; verifier:* is acceptance-only.
//   dotnet fsi tools/r9/r9.fsx -- run-suites --obligations F --root REPO --out DIR [--suites ID,...]
//   dotnet fsi tools/r9/r9.fsx -- suite-preflight --obligations F --root REPO [--out F]
//        Executes every executable suite command in list mode; refuses readiness if any cannot execute.
//   dotnet fsi tools/r9/r9.fsx -- authorize-implementation --target REPO --catalog F --obligations F --suite-preflight F
//        --spec-preflight F --acceptance-preflight F [--work-plan F] [--manifest F] [--scope F] [--praxis EXE]
//        Pre-implementation gate (Praxis ordering); writes the immutable .echelon/r9-initialization-checkpoint.json.
//   dotnet fsi tools/r9/r9.fsx -- check-sequencing --target REPO [--out F]
//        Reports every implementation commit made before implementation was authorized as a process violation.
//   dotnet fsi tools/r9/r9.fsx -- acceptance-status --candidate SHA [--catalog-verification F] [--gate-dir DIR]
//        [--scope F --obligations F --approvals F] [--owner-verdict F] [--out F] [--require catalog,scope,fullPlan,owner]
//        Four separate acceptance levels, each with its own status and evidence.
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
            // Owner decision (2026-11-03): for Rehearsal 009 registration UNAVAILABLE is accepted as a limitation. It
            // is reported as such and never as a registration; installation, pins and tool identities stay mandatory.
            let registered = registrationLines |> List.filter (fun l -> Regex.IsMatch(l, @"registration \S+: (recorded|replayed|unchanged|submitted)"))
            let registrationVerdict =
                if not registrationLines.IsEmpty && registered.Length = registrationLines.Length then "REGISTERED"
                else $"UNAVAILABLE ({registrationLines.Length - registered.Length} of {registrationLines.Length} components not registered: no Project Administration integration; accepted limitation for Rehearsal 009, not a registration)"
            if causes.IsEmpty then
                Pass ([ $"installation: VERIFIED (pinned set, digests, tool identities)"; $"registration: {registrationVerdict}"
                        $"resolved set {url} sha256 {actual} = lock"; $"plan {digest.Groups[1].Value}"; $"target id {targetId}" ]
                      @ (registrationLines |> List.map (fun l -> l.Trim())) @ (tools |> List.map (fun (t, v) -> $"{t} {v} installed")) @ [ $"status: {status.Trim()}" ])
            else Refused causes

// ---- govern / check-governance: the authoritative completion policy ------------------------------------------------

let governanceOf (src: string) = readJson (Path.Combine(src, "tools", "r9", "governance.json"))

let govern () =
    let lockPath = opt "--lock" |> Option.defaultValue "kickoff/rehearsal-009/toolchain.lock.json"
    if not (Regex.IsMatch(lockPath, @"^kickoff/[a-z0-9-]+/toolchain\.lock\.json$")) then
        eprintfn "govern: --lock must be kickoff/<rehearsal>/toolchain.lock.json inside Indy-init, not %s" lockPath
        exit 2
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
                  // The rehearsal's toolchain lock (relative to Indy-init): the gate workflow reads its toolchain and
                  // requirements package from it, so the governed repository cannot choose another one.
                  Collections.Generic.KeyValuePair("lock", JsonValue.Create(lockPath) :> JsonNode)
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
        // The record pins the governing Indy-init commit and the rehearsal's lock; it is written once at T0.
        @ (let _, log = git target [ "log"; "--format=%H"; "--"; ".echelon/r9-governance.json" ]
           let commits = log.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length
           check (commits <= 1) $".echelon/r9-governance.json was changed after T0 ({commits} commits touch it): the governance record is written once, by r9.fsx govern")
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

// ---- gate: obligation-type-aware verification at the trusted candidate ---------------------------------------------

/// What kind of evidence an identity is, read only from the authoritative plan: the runner of the suite it is bound in.
/// Never inferred from a missing test, an empty matrix or an agent's claim.
type EvidenceType =
    | Executable // dotnet-test / playwright suites: Dokimos judges them from execution results
    | Record // evidence-record suites, praxis:<kind>: verify-process judges them from the Praxis log
    | IndependentVerifier // evidence-record suite SU-VERIFIER, verifier:*: only the acceptance verifier judges them

let classify (plan: JsonNode) =
    let runners = arr plan "suites" |> List.map (fun s -> str s "id", str s "runner") |> Map.ofList
    let typeOf (t: JsonNode) =
        let id = str t "id"
        match runners.TryFind(str t "suite") with
        | Some "dotnet-test"
        | Some "playwright" -> Ok Executable
        | Some "evidence-record" when id.StartsWith "praxis:" -> Ok Record
        | Some "evidence-record" when id.StartsWith "verifier:" -> Ok IndependentVerifier
        | runner -> Error $"""{id}: its suite {str t "suite"} (runner {runner}) gives it no evidence type in the plan"""
    let typed = arr plan "tests" |> List.map (fun t -> t, typeOf t)
    let errors = typed |> List.choose (fun (_, r) -> match r with Error e -> Some e | Ok _ -> None)
    let types =
        typed
        |> List.collect (fun (t, r) -> match r with Ok k -> arr t "obligations" |> List.map (fun o -> o.GetValue<string>(), k) | Error _ -> [])
        |> List.groupBy fst |> List.map (fun (o, ks) -> o, ks |> List.map snd |> Set.ofList) |> Map.ofList
    let typesOf (obligation: string) = types.TryFind obligation |> Option.defaultValue Set.empty
    errors, typesOf, typed

/// The frozen plan as Dokimos judges it: only executable identities, and only obligations that have one.
let executableView (plan: JsonNode) typed (typesOf: string -> Set<EvidenceType>) =
    let view = plan.DeepClone()
    view["tests"] <- JsonArray(typed |> List.filter (fun (_, r) -> r = Ok Executable) |> List.map (fun (t: JsonNode, _) -> t.DeepClone()) |> Array.ofList)
    view["obligations"] <- JsonArray(arr plan "obligations" |> List.filter (fun o -> (typesOf (str o "id")).Contains Executable) |> List.map (fun o -> o.DeepClone()) |> Array.ofList)
    view

let matrixRow (dimension: string) (met: bool) (evidence: JsonNode list) =
    JsonObject(
        [ Collections.Generic.KeyValuePair("dimension", JsonValue.Create dimension :> JsonNode)
          Collections.Generic.KeyValuePair("evidence", JsonArray(evidence |> Array.ofList) :> JsonNode)
          Collections.Generic.KeyValuePair("status", JsonValue.Create(if met then "met" else "not-met") :> JsonNode) ]) :> JsonNode
let evidenceItem (kind: string) (reference: string) (result: string) =
    JsonObject(
        [ Collections.Generic.KeyValuePair("kind", JsonValue.Create kind :> JsonNode)
          Collections.Generic.KeyValuePair("reference", JsonValue.Create reference :> JsonNode)
          Collections.Generic.KeyValuePair("result", JsonValue.Create result :> JsonNode) ]) :> JsonNode

/// Runs the authoritative record verifier (requirements-compiler verify-process) for HEAD with the pinned plan and
/// rules; the candidate is never taken from the caller. Returns the assessment, or the reason there is none.
let processAssessment (root: string) (head: string) (out: string) =
    let needed = [ "--rc"; "--process-plan"; "--process-plan-digest"; "--process-rules"; "--process-rules-sha256"; "--process-attestations"; "--process-trusted-run-id" ]
    match needed |> List.filter (opt >> Option.isNone) with
    | missing when not missing.IsEmpty -> Error $"""record evidence is required but the pinned record-verifier inputs are missing: {String.Join(", ", missing)}"""
    | _ ->
        let path = Path.Combine(out, "process-assessment.json")
        let source =
            match opt "--process-events" with
            | Some events -> [ "--events"; events; "--ancestry"; req "--process-ancestry" ] @ (opt "--process-executions" |> Option.map (fun e -> [ "--executions"; e ]) |> Option.defaultValue [])
            | None -> [ "--repo"; root ]
        let argv =
            [ req "--rc"; "verify-process"; "--plan"; req "--process-plan"; "--plan-digest"; req "--process-plan-digest"; "--rules"; req "--process-rules"
              "--rules-sha256"; req "--process-rules-sha256"; "--candidate"; head; "--attestations"; req "--process-attestations"
              "--trusted-run-id"; req "--process-trusted-run-id"; "--out"; path ] @ source
        let code, output = run "dotnet" argv root
        File.WriteAllText(Path.Combine(out, "verify-process.log"), $"dotnet {String.Join(' ', argv)}\nexit {code}\n{output}")
        if File.Exists path then Ok(readJson path, path, code) else Error $"verify-process produced no assessment (exit {code}): {output}"

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
    let classificationErrors, typesOf, typed = classify frozen
    let obligations = executableView frozen typed typesOf
    let viewPath = Path.Combine(out, "executable-obligations.json")
    File.WriteAllText(viewPath, obligations.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
    let scope =
        match mode, opt "--scope", opt "--scope-file" with
        | _, Some s, _ -> s.Split(',', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
        | "development", None, Some f -> arr (readJson f) "requirements" |> List.map (fun n -> n.GetValue<string>())
        | _ -> []
    let inScope (o: JsonNode) = scope.IsEmpty || List.contains (str o "requirement") scope
    let inScopeIds = arr frozen "obligations" |> List.filter inScope |> List.map (fun o -> str o "id")
    let ofType k = inScopeIds |> List.filter (fun o -> (typesOf o).Contains k)
    let executableIds, recordIds, verifierIds = ofType Executable, ofType Record, ofType IndependentVerifier
    let candidateCauses =
        check (headCode = 0 && isSha head) $"the repository HEAD is not a commit ({headOut.Trim()})"
        @ check (dirtyCode = 0 && dirty.Trim() = "") $"the working tree differs from HEAD, so HEAD is not what ran:\n{dirty}"
        @ (match expected with
           | Some e -> check (isSha e) $"--expect-candidate '{e}' is not a 40-character SHA" @ check (e = head) $"CI's candidate {e} is not the checked-out HEAD {head}"
           | None -> [])
        @ check (mode = "release" || mode = "development") $"--mode must be release or development, not '{mode}'"
        @ check (mode <> "development" || not scope.IsEmpty || opt "--scope-file" |> Option.isSome) "development mode needs --scope-file"
        @ check (mode <> "release" || opt "--scope" |> Option.isNone || opt "--work-item" |> Option.isSome) "release mode verifies the whole plan; --scope is only for a work item's matrix"
        @ classificationErrors
        @ (inScopeIds |> List.filter (typesOf >> Set.isEmpty) |> List.map (fun o -> $"{o} has no bound identity of a known evidence type in the plan; its type is never inferred"))
    let results = readJson resultsPath
    let runs = arr results "runs"
    let resultsDir = Path.GetDirectoryName(Path.GetFullPath resultsPath)
    let scopedExecutable = Set.ofList executableIds
    let neededSuites =
        arr obligations "tests"
        |> List.filter (fun t -> arr t "obligations" |> List.exists (fun o -> scopedExecutable.Contains(o.GetValue<string>())))
        |> List.map (fun t -> str t "suite") |> List.distinct
    let resultCauses =
        if executableIds.IsEmpty then []
        else
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
        let dimensions = (opt "--dimensions" |> Option.defaultValue "happy-path,negative,corruption,compatibility,recovery,concurrency").Split(',') |> List.ofArray
        let matrixOut = opt "--work-item" |> Option.map (fun _ -> Path.Combine(out, "praxis-matrix.json"))
        // Executable obligations: Dokimos at HEAD, --require-verified true.
        let coveragePath = Path.Combine(out, "coverage.json")
        let dokimosCode, dokimosOut =
            if executableIds.IsEmpty then 0, "no executable obligation in scope"
            else
                let dokimosArgs =
                    [ "verification"; "coverage"; "--obligations"; viewPath; "--root"; root; "--results"; Path.GetFullPath resultsPath
                      "--candidate"; head; "--require-verified"; "true"; "--out"; coveragePath ]
                    @ (if scope.IsEmpty then [] else [ "--scope"; String.Join(",", scope) ])
                    @ (opt "--probes" |> Option.map (fun ps -> ps.Split(',', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray |> List.collect (fun p -> [ "--probes"; Path.GetFullPath p ])) |> Option.defaultValue [])
                    @ (match matrixOut, opt "--work-item" with
                       | Some m, Some wi -> [ "--praxis-matrix"; m; "--work-item"; wi; "--dimensions"; String.Join(",", dimensions) ]
                       | _ -> [])
                let code, output = run (req "--dokimos") dokimosArgs root
                File.WriteAllText(Path.Combine(out, "dokimos.log"), $"dokimos {String.Join(' ', dokimosArgs)}\nexit {code}\n{output}")
                code, output
        let cov = if File.Exists coveragePath && not executableIds.IsEmpty then readJson coveragePath else null
        let obligationsCov = arr cov "obligations"
        let unverified = obligationsCov |> List.filter (fun o -> str o "status" <> "verified")
        let coverageCauses =
            if executableIds.IsEmpty then []
            elif isNull cov then [ $"dokimos verification coverage produced no report (exit {dokimosCode}): {dokimosOut}" ]
            else
                check (str cov "candidate" = head) $"""the coverage report names candidate '{str cov "candidate"}', not HEAD {head}"""
                @ check (str cov "planId" = str obligations "planId" && str cov "planDigest" = str obligations "planDigest") "the coverage report was computed for another plan"
                @ check (arr cov "rejectedRuns" |> List.isEmpty) $"""Dokimos rejected runs: {canonical cov.["rejectedRuns"]}"""
                @ check (dokimosCode = 0 || not unverified.IsEmpty) $"dokimos verification coverage --require-verified true exited {dokimosCode}: {dokimosOut}"
                @ (unverified |> List.map (fun o ->
                    $"""{str o "id"} ({str o "requirement"}) is {str o "status"}: {String.Join("; ", arr o "reasons" |> List.map (fun r -> r.GetValue<string>()))}"""))
        // Categories whose obligations must also be fault-sensitive (a Dokimos fault probe at HEAD was detected): a test
        // that passes whatever the content is cannot verify a content round trip (FINAL-READINESS, content readiness).
        let sensitiveCategories = opt "--require-fault-sensitive" |> Option.map (fun c -> c.Split(',', StringSplitOptions.RemoveEmptyEntries) |> Set.ofArray) |> Option.defaultValue Set.empty
        let categoriesOf = arr frozen "obligations" |> List.map (fun o -> str o "id", arr o "categories" |> List.map (fun c -> c.GetValue<string>()) |> Set.ofList) |> Map.ofList
        let sensitivityCauses =
            obligationsCov
            |> List.filter (fun o -> not (Set.intersect sensitiveCategories (categoriesOf.TryFind(str o "id") |> Option.defaultValue Set.empty)).IsEmpty)
            |> List.filter (fun o -> str o "quality" <> "fault-sensitive")
            |> List.map (fun o -> $"""{str o "id"} must be fault-sensitive (a fault probe at HEAD detected by its test), but its quality is '{str o "quality"}'""")
        // Record obligations: the authoritative record verifier at HEAD, with the pinned plan and rules. Record evidence
        // is bound to the candidate it reviews, so it is judged when a work item completes and at release, never
        // re-judged on every development merge (where a review of an earlier commit is correctly stale).
        let judgeRecords = mode = "release" || opt "--work-item" |> Option.isSome
        let assessment = if recordIds.IsEmpty || not judgeRecords then None else Some(processAssessment root head out)
        let recordStatus =
            match assessment with
            | Some(Ok(a, _, _)) ->
                let decided =
                    arr a "obligations"
                    |> List.map (fun o ->
                        str o "obligation",
                        arr o "categories" |> List.map (fun c -> str c "category", str c "status", arr c "diagnostics" |> List.map (fun d -> $"""{str d "code"} {str d "subject"}: {str d "detail"}""")))
                    |> Map.ofList
                recordIds |> List.map (fun id ->
                    match decided.TryFind id with
                    | _ when str a "verdict" = "INVALID" -> id, false, $"""the record verifier found the inputs INVALID: {String.Join("; ", arr a "problems" |> List.map (fun p -> p.GetValue<string>()))}"""
                    | None -> id, false, "the record verifier decided no category for it"
                    | Some cats when cats |> List.forall (fun (_, st, _) -> st = "verified") -> id, true, ""
                    | Some cats ->
                        id, false,
                        String.Join("; ", cats |> List.filter (fun (_, st, _) -> st <> "verified") |> List.map (fun (c, _, ds) -> $"""{c} unverified ({String.Join(" | ", ds)})""")))
            | Some(Error why) -> recordIds |> List.map (fun id -> id, false, why)
            | None -> []
        let recordOk = judgeRecords && recordStatus |> List.forall (fun (_, ok, _) -> ok)
        let recordCauses = recordStatus |> List.filter (fun (_, ok, _) -> not ok) |> List.map (fun (id, _, why) -> $"{id}: record evidence not verified: {why}")
        // verifier:* evidence exists only after deployment and is judged only by the acceptance verifier.
        let verifierCauses =
            if mode = "release" && opt "--work-item" |> Option.isNone then []
            else verifierIds |> List.map (fun id -> $"{id} needs independent-verifier evidence (verifier:*), which only the acceptance verifier judges; it cannot be completed here")
        let emptyCauses = check (not inScopeIds.IsEmpty) "no obligation is in scope: an empty verification proves nothing"
        // The work item's matrix: Dokimos's rows for executable obligations, each also bound to the record verdict.
        let recordReference =
            let a = match assessment with | Some(Ok(a, _, _)) -> a | _ -> null
            $"""requirements-compiler verify-process --candidate {head} (plan {str (if isNull a then null else a.["plan"]) "digest"}, rules sha256 {str (if isNull a then null else a.["rules"]) "sha256"}): {(if recordOk then "VERIFIED" else "NOT VERIFIED")} for {recordIds.Length} record obligation(s)"""
        let assessmentLocation = match assessment with | Some(Ok(_, p, _)) -> Some p | _ -> None
        let recordEvidence =
            if recordIds.IsEmpty then []
            else [ evidenceItem "command" recordReference (if recordOk then "verified" else "not verified") ] @ (assessmentLocation |> Option.map (fun p -> [ evidenceItem "location" p "recorded" ]) |> Option.defaultValue [])
        match matrixOut, opt "--work-item" with
        | Some m, Some wi when not executableIds.IsEmpty && File.Exists m ->
            let matrix = readJson m
            let rows = arr matrix "rows" |> List.map (fun r -> matrixRow (str r "dimension") (str r "status" = "met" && recordOk && sensitivityCauses.IsEmpty) ((arr r "evidence" |> List.map (fun e -> e.DeepClone())) @ (recordEvidence |> List.map (fun e -> e.DeepClone()))))
            matrix["rows"] <- JsonArray(rows |> Array.ofList)
            File.WriteAllText(m, matrix.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
        | Some m, Some wi when executableIds.IsEmpty && not recordIds.IsEmpty ->
            // Record-only, as the plan classifies every in-scope obligation: the record verdict is the evidence.
            let matrix =
                JsonObject(
                    [ Collections.Generic.KeyValuePair("schema", JsonValue.Create "praxis.verification-matrix/1" :> JsonNode)
                      Collections.Generic.KeyValuePair("workItem", JsonValue.Create wi :> JsonNode)
                      Collections.Generic.KeyValuePair("rows", JsonArray(dimensions |> List.map (fun d -> matrixRow d (recordOk && verifierIds.IsEmpty) (recordEvidence |> List.map (fun e -> e.DeepClone()))) |> Array.ofList) :> JsonNode) ])
            File.WriteAllText(m, matrix.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
        | _ -> ()
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
                check (not commands.IsEmpty && commands |> List.forall (fun c -> c.Contains $"--candidate {head} ")) $"the matrix's verification evidence does not name HEAD {head} (stale or hand-written matrix)"
                @ check (recordIds.IsEmpty || commands |> List.exists (fun c -> c.StartsWith "requirements-compiler verify-process")) "the matrix omits the record verifier's verdict for this scope's record obligations"
                @ (tests |> List.collect (fun e ->
                    let id = str e "reference"
                    match statusOf.TryFind id with
                    | None -> [ $"the matrix cites {id}, which is not a bound identity of the in-scope obligations" ]
                    | Some s -> check (s = str e "result") $"""the matrix says {id} {str e "result"}, Dokimos says {s} at HEAD"""))
        let causes = candidateCauses @ resultCauses @ coverageCauses @ sensitivityCauses @ recordCauses @ verifierCauses @ emptyCauses @ matrixCauses
        let deferred = if mode = "release" && opt "--work-item" |> Option.isNone then verifierIds else []
        let strings (xs: string list) = JsonArray(xs |> List.map (fun x -> JsonValue.Create x :> JsonNode) |> Array.ofList) :> JsonNode
        let verdict =
            JsonObject(
                [ Collections.Generic.KeyValuePair("schema", JsonValue.Create "indy-init.r9-gate-verdict/2" :> JsonNode)
                  Collections.Generic.KeyValuePair("verdict", JsonValue.Create(if causes.IsEmpty then "PASS" else "REFUSED") :> JsonNode)
                  Collections.Generic.KeyValuePair("mode", JsonValue.Create mode :> JsonNode)
                  Collections.Generic.KeyValuePair("candidate", JsonValue.Create head :> JsonNode)
                  Collections.Generic.KeyValuePair("planId", JsonValue.Create(str frozen "planId") :> JsonNode)
                  Collections.Generic.KeyValuePair("scope", strings scope)
                  Collections.Generic.KeyValuePair("workItem", JsonValue.Create(opt "--work-item" |> Option.defaultValue "") :> JsonNode)
                  Collections.Generic.KeyValuePair("obligations", JsonValue.Create inScopeIds.Length :> JsonNode)
                  Collections.Generic.KeyValuePair("executable", JsonValue.Create executableIds.Length :> JsonNode)
                  Collections.Generic.KeyValuePair("executableVerified", JsonValue.Create(obligationsCov |> List.filter (fun o -> str o "status" = "verified") |> List.length) :> JsonNode)
                  Collections.Generic.KeyValuePair("record", JsonValue.Create recordIds.Length :> JsonNode)
                  Collections.Generic.KeyValuePair("recordJudged", JsonValue.Create judgeRecords :> JsonNode)
                  Collections.Generic.KeyValuePair("recordVerified", JsonValue.Create(recordStatus |> List.filter (fun (_, ok, _) -> ok) |> List.length) :> JsonNode)
                  Collections.Generic.KeyValuePair("mixed", JsonValue.Create(executableIds |> List.filter (fun o -> List.contains o recordIds) |> List.length) :> JsonNode)
                  Collections.Generic.KeyValuePair("deferredToIndependentVerifier", strings deferred)
                  Collections.Generic.KeyValuePair("dokimosExit", JsonValue.Create dokimosCode :> JsonNode)
                  Collections.Generic.KeyValuePair("frozenPlanSha256", JsonValue.Create(sha256File obligationsPath) :> JsonNode)
                  Collections.Generic.KeyValuePair("coverageSha256", JsonValue.Create(if File.Exists coveragePath then sha256File coveragePath else "") :> JsonNode)
                  Collections.Generic.KeyValuePair("processAssessmentSha256", JsonValue.Create(assessmentLocation |> Option.map sha256File |> Option.defaultValue "") :> JsonNode)
                  Collections.Generic.KeyValuePair("resultsSha256", JsonValue.Create(sha256File resultsPath) :> JsonNode)
                  Collections.Generic.KeyValuePair("matrixSha256", JsonValue.Create(matrixPath |> Option.filter File.Exists |> Option.map sha256File |> Option.defaultValue "") :> JsonNode)
                  Collections.Generic.KeyValuePair("causes", strings causes) ])
        File.WriteAllText(Path.Combine(out, "gate.json"), verdict.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
        let summary =
            let scopeText = if scope.IsEmpty then ", whole plan" else ", scope " + String.Join(",", scope)
            [ $"candidate {head} ({mode}{scopeText})"
              $"""executable: {str verdict "executableVerified"}/{executableIds.Length} verified by Dokimos; record: {str verdict "recordVerified"}/{recordIds.Length} verified by verify-process{(if judgeRecords || recordIds.IsEmpty then "" else " (not re-judged on a development merge: judged at completion and at release)")}; mixed: {str verdict "mixed"}; {deferred.Length} deferred to the independent verifier"""
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

// ---- suite-preflight: every suite command of the plan must be able to execute ---------------------------------------

/// Executes each executable suite's own command in list mode, against a repository, before readiness is claimed
/// (docs/release-gate-repair). A dotnet-test suite runs `COMMAND --list-tests` (the SDK resolves the target, restores
/// and builds it); a Playwright suite runs `COMMAND --list --pass-with-no-tests` (Playwright resolves the configuration
/// and every --project). Listing needs no test to exist yet, so this runs before implementation; a command that cannot
/// execute (Rehearsal 009: MSB1008, 'Project(s) "webkit" not found') refuses readiness. Writes the report to --out.
let suitePreflight () =
    let root = Path.GetFullPath(req "--root")
    let obligations = readJson (req "--obligations")
    let suites = arr obligations "suites" |> List.filter (fun s -> str s "runner" = "dotnet-test" || str s "runner" = "playwright")
    let probe (s: JsonNode) =
        let id, command = str s "id", str s "command"
        let listed = if str s "runner" = "dotnet-test" then $"{command} --list-tests" else $"{command} --list --pass-with-no-tests"
        let code, output = run "sh" [ "-c"; listed ] root
        let cause =
            output.Split('\n')
            |> Array.tryFind (fun l -> Regex.IsMatch(l, @"error|Error:|not found", RegexOptions.IgnoreCase))
            |> Option.map (fun l -> l.Trim())
            |> Option.defaultValue $"exit {code}"
        id, listed, code, (if code = 0 then "" else cause)
    let results = suites |> List.map probe
    let report =
        JsonObject(
            [ Collections.Generic.KeyValuePair("schema", JsonValue.Create "indy-init.r9-suite-preflight/1" :> JsonNode)
              Collections.Generic.KeyValuePair("plan", JsonValue.Create(str obligations "planId") :> JsonNode)
              Collections.Generic.KeyValuePair("root", JsonValue.Create root :> JsonNode)
              Collections.Generic.KeyValuePair(
                  "suites",
                  JsonArray(
                      results
                      |> List.map (fun (id, cmd, code, cause) ->
                          JsonObject(
                              [ Collections.Generic.KeyValuePair("id", JsonValue.Create id :> JsonNode)
                                Collections.Generic.KeyValuePair("command", JsonValue.Create cmd :> JsonNode)
                                Collections.Generic.KeyValuePair("exit", JsonValue.Create(code: int) :> JsonNode)
                                Collections.Generic.KeyValuePair("executable", JsonValue.Create((code = 0): bool) :> JsonNode)
                                Collections.Generic.KeyValuePair("cause", JsonValue.Create cause :> JsonNode) ]) :> JsonNode)
                      |> Array.ofList) :> JsonNode) ])
    opt "--out" |> Option.iter (fun f -> File.WriteAllText(f, report.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n"))
    let refused = results |> List.filter (fun (_, _, code, _) -> code <> 0) |> List.map (fun (id, cmd, code, cause) -> $"suite {id} cannot execute (exit {code}): {cause}  [$ {cmd}]")
    if suites.IsEmpty then Unusable "the obligations name no executable suite"
    elif refused.IsEmpty then Pass (results |> List.map (fun (id, _, _, _) -> $"{id}: executable") )
    else Refused refused

// ---- small JSON builders used by the sequencing and acceptance commands ---------------------------------------------

let jstr (v: string) : JsonNode = JsonValue.Create(v: string) :> JsonNode
let jbool (v: bool) : JsonNode = JsonValue.Create(v: bool) :> JsonNode
let jint (v: int) : JsonNode = JsonValue.Create(v: int) :> JsonNode
let jarr (xs: JsonNode list) : JsonNode = JsonArray(xs |> Array.ofList) :> JsonNode
let jstrs (xs: string list) = xs |> List.map jstr |> jarr
let jobj (fields: (string * JsonNode) list) : JsonNode =
    let o = JsonObject()
    fields |> List.iter (fun (k, v) -> o[k] <- (if isNull v || isNull v.Parent then v else v.DeepClone()))
    o :> JsonNode
let writeJson (path: string) (n: JsonNode) = File.WriteAllText(path, n.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
let strList (n: JsonNode) key = arr n key |> List.map (fun v -> v.GetValue<string>())

// ---- authorize-implementation: Praxis ordering before any implementation (docs/release-gate-repair) -----------------

/// The repository paths whose change is implementation (application, tests, build). Requirements, records, docs,
/// evidence and CI are not.
let isImplementationPath (path: string) =
    let name = path.Substring(path.LastIndexOf '/' + 1)
    [ "src/"; "web/"; "tests/" ] |> List.exists (fun p -> path.StartsWith(p, StringComparison.Ordinal))
    || List.contains path [ "build.fsx"; "playwright.config.ts"; "playwright.config.js"; "package.json"; "package-lock.json"; "global.json" ]
    || ([ ".slnx"; ".sln"; ".fsproj"; ".csproj" ] |> List.exists (fun x -> path.EndsWith(x, StringComparison.Ordinal)))
    || (name.StartsWith("Directory.", StringComparison.Ordinal) && (name.EndsWith(".props", StringComparison.Ordinal) || name.EndsWith(".targets", StringComparison.Ordinal)))

let checkpointPath = ".echelon/r9-initialization-checkpoint.json"

/// Refuses implementation until, in order: Conditor initialized the repository; the catalog manifest is compiled and
/// current and Praxis validates; work items exist; every work item is linked to requirements and obligations that
/// exist, and every in-scope obligation is linked; the verification matrix is executable (every bound suite passed
/// suite-preflight); and the specification, suite and acceptance preflights passed. On PASS it writes the immutable
/// initialization checkpoint; it never overwrites one. Verification evidence is NOT required: it comes after
/// implementation.
let authorizeImplementation () =
    let target = Path.GetFullPath(req "--target")
    let praxis = opt "--praxis" |> Option.defaultValue "praxis"
    let workPlanPath = Path.Combine(target, opt "--work-plan" |> Option.defaultValue ".echelon/r9-work-plan.json")
    let manifestPath = Path.Combine(target, opt "--manifest" |> Option.defaultValue "requirements/verification/verification-manifest.json")
    let ck = Path.Combine(target, checkpointPath)
    let exists p = File.Exists p
    let step1 =
        check (exists (Path.Combine(target, "conditor.json")) && exists (Path.Combine(target, ".conditor", "lock.json")))
            "1 Conditor: conditor.json and .conditor/lock.json are missing; run conditor init first"
        @ check (exists (Path.Combine(target, ".echelon", "r9-governance.json"))) "1 governance: .echelon/r9-governance.json is missing; run r9.fsx govern at T0"
    let step2 =
        if not (exists manifestPath) then [ $"2 requirements: {manifestPath} is missing; compile the catalog with praxis requirements manifest compile" ]
        else
            let code, out = run praxis [ "requirements"; "manifest"; "check"; "--catalog"; req "--catalog"; "--manifest"; manifestPath ] target
            let vcode, vout = run praxis [ "validate"; "--root"; target ] target
            check (code = 0) $"2 requirements: the manifest is not what the pinned catalog compiles to (praxis requirements manifest check exit {code}): {out.Trim()}"
            @ check (vcode = 0) $"2 Praxis: praxis validate failed (exit {vcode}): {vout.Trim()}"
            @ check (exists (req "--obligations")) "2 plan: the compiled frozen-plan obligations file is missing"
    let workPlan = if exists workPlanPath then readJson workPlanPath else null
    let items = arr workPlan "workItems"
    let praxisItems =
        let code, out = run praxis [ "work"; "--root"; target ] target
        if code <> 0 then []
        else
            try (JsonNode.Parse out :?> JsonArray) |> Seq.map (fun n -> str n "id") |> List.ofSeq with _ -> []
    let lifecycle (id: string) = id.StartsWith "COND-MISSION" || id.StartsWith "ROS-INSTALL"
    let step3 =
        check (not (isNull workPlan)) $"3 work items: {workPlanPath} (indy-init.r9-work-plan/1) is missing"
        @ check (praxisItems |> List.exists (lifecycle >> not)) "3 work items: Praxis holds no work item beyond its own lifecycle records; create them before implementing"
        @ (items |> List.choose (fun w -> let id = str w "id" in if List.contains id praxisItems then None else Some $"3 work items: {id} is in the work plan but not in Praxis"))
    let manifest = if exists manifestPath then readJson manifestPath else null
    let catalogObligations = arr manifest "requirements" |> List.collect (fun r -> arr r "obligations" |> List.map (fun o -> str o "id", str r "id")) |> Map.ofList
    let plan = if exists (req "--obligations") then readJson (req "--obligations") else null
    let planObligations = arr plan "obligations" |> List.map (fun o -> str o "id", str o "requirement") |> Map.ofList
    let known o = catalogObligations.ContainsKey o || planObligations.ContainsKey o
    let scope =
        match opt "--scope" with
        | Some f -> readJson f
        | None -> null
    let scopeRequirements = if isNull scope then [] else strList scope "requirements" @ strList scope "catalogRequirements"
    let knownRequirements = (catalogObligations |> Map.toList |> List.map snd) @ (planObligations |> Map.toList |> List.map snd) |> Set.ofList
    let scopeCauses =
        if isNull scope then []
        elif scopeRequirements.IsEmpty then [ "4 links: the scope names no requirement (requirements, catalogRequirements): an empty scope authorizes nothing" ]
        else scopeRequirements |> List.filter (knownRequirements.Contains >> not) |> List.map (fun r -> $"4 links: the scope names {r}, which neither the catalog manifest nor the plan defines")
    let inScope =
        if isNull scope then (catalogObligations |> Map.toList |> List.map fst) @ (planObligations |> Map.toList |> List.map fst)
        else
            let reqs = scopeRequirements |> Set.ofList
            (catalogObligations |> Map.toList) @ (planObligations |> Map.toList) |> List.filter (fun (_, r) -> reqs.Contains r) |> List.map fst
    let linked = items |> List.collect (fun w -> strList w "obligations") |> Set.ofList
    let step4 =
        scopeCauses
        @ (items |> List.collect (fun w ->
            let id = str w "id"
            check (not (strList w "requirements").IsEmpty) $"4 links: {id} names no requirement"
            @ check (not (strList w "obligations").IsEmpty) $"4 links: {id} names no verification obligation"
            @ (strList w "obligations" |> List.filter (known >> not) |> List.map (fun o -> $"4 links: {id} names obligation {o}, which neither the catalog manifest nor the plan defines"))))
        @ (match inScope |> List.filter (linked.Contains >> not) with
           | [] -> []
           | missing -> [ $"""4 links: {missing.Length} in-scope obligation(s) are linked to no work item (first: {String.Join(", ", missing |> List.truncate 5)})""" ])
    let suitePre = opt "--suite-preflight" |> Option.filter exists |> Option.map readJson
    let executable = suitePre |> Option.map (fun p -> arr p "suites" |> List.map (fun s -> str s "id", str s "executable" = "true") |> Map.ofList) |> Option.defaultValue Map.empty
    let suiteOfIdentity = arr plan "tests" |> List.map (fun t -> str t "id", str t "suite") |> Map.ofList
    let executableSuites = arr plan "suites" |> List.filter (fun s -> str s "runner" = "dotnet-test" || str s "runner" = "playwright") |> List.map (fun s -> str s "id")
    let step5 =
        match suitePre with
        | None -> [ "5 matrix: no suite-preflight report (r9.fsx suite-preflight) was supplied" ]
        | Some report ->
            // The report must be of this plan, against this repository, and cover every executable suite.
            check (str report "plan" = str plan "planId") $"""5 matrix: the suite-preflight report is for plan {str report "plan"}, not {str plan "planId"}"""
            @ check (str report "root" = target) $"""5 matrix: the suite-preflight report ran against {str report "root"}, not {target}"""
            @ (executableSuites |> List.filter (executable.ContainsKey >> not) |> List.map (fun s -> $"5 matrix: suite {s} is missing from the suite-preflight report"))
            @ (arr plan "tests"
               |> List.filter (fun t -> not (arr t "obligations").IsEmpty && (arr t "obligations" |> List.exists (fun o -> List.contains (o.GetValue<string>()) inScope)))
               |> List.choose (fun t -> let s = str t "suite" in match executable.TryFind s with Some true -> None | Some false -> Some s | None -> None)
               |> List.distinct
               |> List.map (fun s -> $"5 matrix: suite {s} binds in-scope obligations but its command cannot execute (suite-preflight)"))
    let step6 =
        // Parsed, never substring-matched: the spec report's own verdict field, and Conditor's verdict line.
        let specOk =
            opt "--spec-preflight" |> Option.filter exists
            |> Option.map (fun f -> try (let r = readJson f in str r "schema" = "indy-init.r9-spec-preflight/1" && str r "verdict" = "PASS") with _ -> false)
            |> Option.defaultValue false
        let accOk =
            opt "--acceptance-preflight" |> Option.filter exists
            |> Option.map (fun f -> File.ReadAllLines f |> Array.exists (fun l -> l.Trim() = "acceptance preflight: READY"))
            |> Option.defaultValue false
        check specOk "6 preflight: the specification preflight (r9.fsx spec-preflight) report is missing or not PASS"
        @ check (suitePre |> Option.exists (fun p -> arr p "suites" |> List.forall (fun s -> str s "executable" = "true"))) "6 preflight: suite-preflight did not pass for every suite"
        @ check accOk "6 preflight: conditor acceptance preflight is missing or not READY"
    let _, status = git target [ "status"; "--porcelain"; "--untracked-files=no" ]
    let _, headOut = git target [ "rev-parse"; "HEAD" ]
    let head = headOut.Trim()
    let causes =
        check (not (exists ck)) $"{checkpointPath} already exists: the initialization checkpoint is immutable"
        @ step1 @ step2 @ step3 @ step4 @ step5 @ step6
        @ check (status.Trim() = "") "the tracked tree is not clean: authorize from a committed state"
    if not causes.IsEmpty then Refused causes
    else
        let digest (p: string) = jobj [ "path", jstr (Path.GetRelativePath(target, Path.GetFullPath p)); "sha256", jstr (sha256File p) ]
        let record =
            jobj
                [ "schema", jstr "indy-init.r9-initialization-checkpoint/1"
                  "authorizedAt", jstr (DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                  "authorizedAtCommit", jstr head
                  "meaning", jstr "Implementation is authorized from the commit that adds this file. Every commit before it that changes implementation paths (src/, web/, tests/, build files), other than the T0 bootstrap, is a process violation (r9.fsx check-sequencing). Verification evidence is produced after implementation and is not part of this checkpoint."
                  "workItems", jstrs (items |> List.map (fun w -> str w "id"))
                  "inScopeObligations", jint inScope.Length
                  "inputs", jarr ([ digest manifestPath; digest workPlanPath; digest (req "--obligations") ] @ ([ "--suite-preflight"; "--spec-preflight"; "--acceptance-preflight"; "--scope" ] |> List.choose opt |> List.map digest)) ]
        writeJson ck record
        Pass [ $"checkpoint {ck} written at {head}: commit it as its own commit before any implementation"; $"{items.Length} work item(s), {inScope.Length} in-scope obligation(s) linked" ]

/// Process-violation report. Every non-merge commit reachable from HEAD that changes an implementation path must be
/// either the T0 bootstrap (the commit that adds .echelon/r9-governance.json) or a strict descendant of the commit that
/// adds the initialization checkpoint. Anything else is a process violation: implementation before governance,
/// between the bootstrap and the checkpoint, inside the checkpoint commit, or on a branch that forked before the
/// checkpoint and was merged later (judged by ancestry, not by commit order). Also refuses a checkpoint that was
/// edited after it was committed or that records another authorizing commit.
let checkSequencing () =
    let target = Path.GetFullPath(req "--target")
    let lines (out: string) = out.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
    let firstAdding (path: string) =
        let _, out = git target [ "log"; "--diff-filter=A"; "--format=%H"; "--reverse"; "--"; path ]
        lines out |> List.tryHead
    let touching (path: string) =
        let _, out = git target [ "log"; "--format=%H"; "--"; path ]
        lines out
    match firstAdding ".echelon/r9-governance.json" with
    | None -> Unusable "no commit adds .echelon/r9-governance.json: the repository was not bootstrapped under r9 governance"
    | Some bootstrap ->
        let checkpoint = firstAdding checkpointPath
        let isAncestor (a: string) (b: string) = fst (git target [ "merge-base"; "--is-ancestor"; a; b ]) = 0
        let _, revs = git target [ "rev-list"; "--reverse"; "HEAD" ]
        let violations =
            lines revs
            |> List.choose (fun c ->
                // A merge is judged by what it changes beyond its parents (--cc: an evil merge or a conflict resolution).
                let _, parents = git target [ "rev-list"; "--parents"; "-n"; "1"; c ]
                let isMerge = (lines parents |> List.head).Split(' ').Length > 2
                let _, files =
                    if isMerge then git target [ "diff-tree"; "--cc"; "--no-commit-id"; "--name-only"; c ]
                    else git target [ "diff-tree"; "--no-commit-id"; "--name-only"; "-r"; "--root"; c ]
                let impl = lines files |> List.filter isImplementationPath
                let authorized =
                    c = bootstrap
                    || (match checkpoint with
                        | Some ck -> c <> ck && isAncestor ck c
                        | None -> false)
                if impl.IsEmpty || authorized then None
                else
                    let _, subject = git target [ "log"; "-1"; "--format=%s"; c ]
                    Some(c, subject.Trim(), impl))
        let checkpointCauses =
            match checkpoint with
            // Before authorization a repository may hold only its bootstrap: no checkpoint is a cause only once
            // implementation has been committed without one.
            | None when violations.IsEmpty -> []
            | None -> [ $"no initialization checkpoint ({checkpointPath}): implementation was committed but never authorized (r9.fsx authorize-implementation)" ]
            | Some c ->
                let edits = touching checkpointPath
                check (edits.Length = 1) $"{checkpointPath} was changed after it was committed ({edits.Length} commits touch it): the checkpoint is immutable"
                @ (let _, parent = git target [ "rev-parse"; c + "^" ]
                   let _, recordedText = git target [ "show"; c + ":" + checkpointPath ]
                   let recorded = try str (JsonNode.Parse recordedText) "authorizedAtCommit" with _ -> ""
                   check (recorded = parent.Trim()) $"the checkpoint records authorization at {recorded}, but it was committed on top of {parent.Trim()}")
        let report =
            jobj
                [ "schema", jstr "indy-init.r9-sequencing/1"
                  "bootstrapCommit", jstr bootstrap
                  "checkpointCommit", jstr (checkpoint |> Option.defaultValue "")
                  "processViolations",
                  jarr (violations |> List.map (fun (c, s, files) -> jobj [ "commit", jstr c; "subject", jstr s; "implementationFiles", jint files.Length; "examples", jstrs (files |> List.truncate 5) ])) ]
        opt "--out" |> Option.iter (fun f -> writeJson f report)
        let causes =
            checkpointCauses
            @ (violations |> List.map (fun (c, s, files) -> $"process violation: {c.Substring(0, 12)} \"{s}\" changed {files.Length} implementation file(s) without authorization"))
        if causes.IsEmpty then
            match checkpoint with
            | Some c -> Pass [ $"bootstrap {bootstrap.Substring(0, 12)}, checkpoint {c.Substring(0, 12)}: no implementation before authorization" ]
            | None -> Pass [ $"bootstrap {bootstrap.Substring(0, 12)}: implementation not yet authorized, and none committed" ]
        else Refused causes

// ---- acceptance-status: four separate acceptance levels, never inferred from one another ----------------------------

/// Reports, each with its own status, candidate and evidence digests:
///   catalog      Dokimos requirement verification of the catalog (dokimos.requirement-verification/1);
///   scope        competition-scope release acceptance: every obligation of an owner-approved scope verified at the
///                candidate in the release gate's coverage and process assessment (a proposed, unapproved scope is
///                reported SCOPE-NOT-APPROVED, never accepted);
///   fullPlan     the release gate's verdict on the whole frozen plan (mode release, no scope);
///   owner        the computed acceptance verdict (tools/compute-acceptance-verdict.py) with its trust inputs, or
///                NOT-COMPUTED.
/// Missing or stale evidence is an explicit refusal of that level. `--require LEVEL,...` exits 4 unless each named
/// level is ACCEPTED.
let acceptanceStatus () =
    let candidate = req "--candidate"
    if not (isSha candidate) then
        eprintfn "acceptance-status: --candidate must be a full 40-hex commit SHA, not %s" candidate
        exit 2
    let present (o: string) = opt o |> Option.filter File.Exists
    let evidence (paths: string list) = paths |> List.map (fun p -> jobj [ "path", jstr p; "sha256", jstr (sha256File p) ]) |> jarr
    let level (status: string) (causes: string list) (paths: string list) (extra: (string * JsonNode) list) =
        jobj ([ "status", jstr status; "candidate", jstr candidate; "causes", jstrs causes; "evidence", evidence paths ] @ extra)
    let catalog =
        match present "--catalog-verification" with
        | None -> level "REFUSED" [ "missing evidence: no catalog requirement verification (dokimos.requirement-verification/1) was supplied" ] [] []
        | Some f ->
            let r = readJson f
            let accepted = str (r["acceptance"]) "accepted" = "true"
            let causes =
                check (str r "schema" = "dokimos.requirement-verification/1") $"{f} is not dokimos.requirement-verification/1"
                @ check (str r "candidate" = candidate) $"""stale evidence: the catalog verification is for {str r "candidate"}, not {candidate}"""
                @ check accepted "Dokimos did not accept the catalog"
            level (if causes.IsEmpty then "ACCEPTED" else "REFUSED") causes [ f ] [ "summary", (match r["summary"] with null -> null | s -> s.DeepClone()); "catalog", jstr (str (r["manifest"]) "catalogId") ]
    let gateDir = opt "--gate-dir"
    let gateFile = gateDir |> Option.map (fun d -> Path.Combine(d, "gate.json")) |> Option.filter File.Exists
    let fullPlan =
        match gateFile with
        | None -> level "REFUSED" [ "missing evidence: no release-gate verdict (gate.json) was supplied" ] [] []
        | Some f ->
            let g = readJson f
            let causes =
                check (str g "candidate" = candidate) $"""stale evidence: the gate verdict is for {str g "candidate"}, not {candidate}"""
                @ check (str g "mode" = "release" && (arr g "scope").IsEmpty && str g "workItem" = "") "the gate verdict is not a release-mode verdict on the whole plan"
                @ check (str g "verdict" = "PASS") $"""the release gate verdict is {str g "verdict"} (executable {str g "executableVerified"}/{str g "executable"}, record {str g "recordVerified"}/{str g "record"})"""
            level (if causes.IsEmpty then "ACCEPTED" else "REFUSED") causes [ f ] [ "obligations", jstr (str g "obligations") ]
    let owner =
        match present "--owner-verdict" with
        | None -> level "NOT-COMPUTED" [ "no computed acceptance verdict: tools/compute-acceptance-verdict.py needs the owner-pinned approvals and evidence manifest of this rehearsal" ] [] []
        | Some f ->
            let text = File.ReadAllText f
            let m = Regex.Match(text, "acceptance-verdict:\\s*([A-Z]+)")
            let v = if m.Success then m.Groups[1].Value else "INVALID"
            // The computed report names the trusted source commit it judged; a verdict for another commit is stale.
            let bound = Regex.IsMatch(text, $@"(?<![0-9a-f]){candidate}(?![0-9a-f])")
            let causes = (if v = "ACCEPTED" then [] else [ $"the computed verdict is {v}" ]) @ check bound $"stale evidence: the computed verdict does not name the candidate {candidate}"
            level (if v = "ACCEPTED" && bound then "ACCEPTED" elif not bound then "REFUSED" else v) causes [ f ] []
    let scope =
        match present "--scope" with
        | None -> level "REFUSED" [ "missing evidence: no competition scope was supplied" ] [] []
        | Some sf ->
            let s = readJson sf
            let approval = str s "approval"
            // A scope is approved only by an owner record in the pinned approvals file: {decision_id, action:
            // approve-scope, scope_sha256}. The scope file's own words never approve it.
            let scopeSha = sha256File sf
            let approvalRecorded =
                present "--approvals"
                |> Option.map (fun af ->
                    let text = File.ReadAllText af
                    let a = JsonNode.Parse text
                    let records = match a with :? JsonArray as xs -> xs |> Seq.toList | o -> arr o "changes" @ arr o "approvals"
                    records |> List.exists (fun r -> str r "decision_id" = approval && str r "action" = "approve-scope" && str r "scope_sha256" = scopeSha))
                |> Option.defaultValue false
            let approved = approval <> "" && approvalRecorded
            let reqs = strList s "requirements" |> Set.ofList
            let cov = gateDir |> Option.map (fun d -> Path.Combine(d, "coverage.json")) |> Option.filter File.Exists
            let proc = gateDir |> Option.map (fun d -> Path.Combine(d, "process-assessment.json")) |> Option.filter File.Exists
            let catalogReqs = strList s "catalogRequirements" |> Set.ofList
            let execStatus = cov |> Option.map (fun c -> arr (readJson c) "obligations" |> List.map (fun o -> str o "id", (str o "requirement", str o "status"))) |> Option.defaultValue []
            let recStatus =
                proc
                |> Option.map (fun p -> arr (readJson p) "obligations" |> List.map (fun o -> str o "obligation", (arr o "categories" |> List.forall (fun c -> str c "status" = "verified"))))
                |> Option.defaultValue []
            let planDoc = present "--obligations" |> Option.map readJson
            let planObligations = planDoc |> Option.map (fun p -> arr p "obligations" |> List.map (fun o -> str o "id", str o "requirement")) |> Option.defaultValue []
            let planRequirements = planObligations |> List.map snd |> Set.ofList
            let inScope = planObligations |> List.filter (fun (_, r) -> reqs.Contains r)
            // The evidence an obligation needs comes from the plan: the runner of each bound identity's suite.
            let suiteRunner = planDoc |> Option.map (fun p -> arr p "suites" |> List.map (fun s -> str s "id", str s "runner") |> Map.ofList) |> Option.defaultValue Map.empty
            let classesOf =
                planDoc
                |> Option.map (fun p ->
                    arr p "tests"
                    |> List.collect (fun t ->
                        let cls =
                            if (str t "id").StartsWith "verifier:" then "verifier"
                            else
                                match suiteRunner.TryFind(str t "suite") with
                                | Some "dotnet-test" | Some "playwright" -> "executable"
                                | Some "evidence-record" when (str t "id").StartsWith "praxis:" -> "record"
                                | _ -> "other"
                        strList t "obligations" |> List.map (fun o -> o, cls))
                    |> List.groupBy fst |> List.map (fun (o, xs) -> o, xs |> List.map snd |> Set.ofList) |> Map.ofList)
                |> Option.defaultValue Map.empty
            let execMap = Map.ofList execStatus
            let recMap = Map.ofList recStatus
            let unverified =
                inScope
                |> List.choose (fun (o, _) ->
                    let classes = classesOf.TryFind o |> Option.defaultValue Set.empty
                    let exe =
                        if classes.Contains "executable" then
                            match execMap.TryFind o with
                            | Some(_, "verified") -> None
                            | Some(_, st) -> Some $"executable {st}"
                            | None -> Some "no executable evidence"
                        else None
                    let rcd =
                        if classes.Contains "record" then
                            match recMap.TryFind o with
                            | Some true -> None
                            | Some false -> Some "record not verified"
                            | None -> Some "no record assessment"
                        else None
                    // Independent-verifier evidence (verifier:*) is judged only by the acceptance verifier: such an
                    // obligation is verified for a scope only when the computed owner verdict is ACCEPTED.
                    let vrf =
                        if classes.Contains "verifier" && str owner "status" <> "ACCEPTED" then
                            Some $"""independent-verifier evidence, judged only by the computed owner verdict ({str owner "status"})"""
                        else None
                    match [ exe; rcd; vrf ] |> List.choose id with
                    | [] when classes.IsEmpty -> Some $"{o}: the plan binds no evidence to it"
                    | [] -> None
                    | xs -> Some($"{o}: " + String.Join(", ", xs)))
            let catalogCauses =
                if catalogReqs.IsEmpty then []
                else
                    match present "--catalog-verification" with
                    | None -> [ "missing evidence: the scope includes catalog requirements but no catalog verification was supplied" ]
                    | Some f ->
                        let r = readJson f
                        // A catalog requirement counts by its mandatory obligations, as Dokimos's own acceptance does.
                        arr r "requirements"
                        |> List.filter (fun q -> catalogReqs.Contains(str q "id"))
                        |> List.collect (fun q ->
                            arr q "obligations"
                            |> List.filter (fun o -> str o "mandatory" = "true" && str o "status" <> "PASS" && str o "status" <> "NOT_APPLICABLE")
                            |> List.map (fun o -> $"""catalog {str o "id"} (mandatory) is {str o "status"}"""))
                        |> fun xs ->
                            let reported = arr r "requirements" |> List.map (fun q -> str q "id") |> Set.ofList
                            xs
                            @ (catalogReqs |> Set.toList |> List.filter (reported.Contains >> not) |> List.map (fun q -> $"catalog {q} is in the scope but not in the catalog verification"))
                            @ check (str r "candidate" = candidate) "stale evidence: the catalog verification is for another candidate"
            let covCandidate = cov |> Option.map (fun c -> str (readJson c) "candidate")
            let causes =
                check (not (reqs.IsEmpty && catalogReqs.IsEmpty)) "the scope names no requirement: an empty scope accepts nothing"
                @ check (reqs.IsEmpty || planDoc.IsSome) "missing evidence: the scope names plan requirements but no compiled plan (--obligations) was supplied"
                @ (reqs |> Set.toList |> List.filter (fun r -> planDoc.IsSome && not (planRequirements.Contains r)) |> List.map (fun r -> $"the scope names {r}, which the plan does not define"))
                @ check (reqs.IsEmpty || cov.IsSome) "missing evidence: the scope names plan requirements but no release-gate coverage (coverage.json) was supplied"
                @ (match covCandidate with
                   | Some c when c <> candidate -> [ $"stale evidence: the release-gate coverage is for {c}, not {candidate}" ]
                   | _ -> [])
                @ (if approved then [] else [ $"""the scope ({str s "status"}) has no owner approval record (approve-scope, scope_sha256 {scopeSha.Substring(0, 12)}) in the pinned approvals: until the owner approves it, the competition scope is the whole frozen plan""" ])
                @ (match gateFile with
                   | Some f when str (readJson f) "candidate" <> candidate -> [ "stale evidence: the gate output is for another candidate" ]
                   | _ -> [])
                @ (if unverified.IsEmpty then [] else [ $"""{unverified.Length} in-scope plan obligation(s) are not verified (first: {String.Join("; ", unverified |> List.truncate 3)})""" ])
                @ catalogCauses
            let status = if not approved then "SCOPE-NOT-APPROVED" elif causes.IsEmpty then "ACCEPTED" else "REFUSED"
            level status causes ([ sf ] @ (cov |> Option.toList) @ (proc |> Option.toList)) [ "scope", jstr (str s "id"); "planObligationsInScope", jint inScope.Length; "catalogRequirementsInScope", jint catalogReqs.Count; "claimsFullPlan", jbool false ]
    let report =
        jobj
            [ "schema", jstr "indy-init.r9-acceptance-status/1"
              "candidate", jstr candidate
              "rule", jstr "Each level has its own status and evidence. No level is inferred from another: catalog acceptance is never release acceptance, a scoped release never claims the full plan, and only the computed owner verdict is the final acceptance verdict."
              "catalog", catalog
              "competitionScope", scope
              "fullPlan", fullPlan
              "ownerVerdict", owner ]
    opt "--out" |> Option.iter (fun f -> writeJson f report)
    let statusOf (n: JsonNode) = str n "status"
    let lines =
        [ "catalog", catalog; "competitionScope", scope; "fullPlan", fullPlan; "ownerVerdict", owner ]
        |> List.map (fun (k, n) -> $"""{k}: {statusOf n}{(if (arr n "causes").IsEmpty then "" else " (" + String.Join("; ", arr n "causes" |> List.map (fun c -> c.GetValue<string>()) |> List.truncate 2) + ")")}""")
    let required = opt "--require" |> Option.map (fun r -> r.Split(',', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray) |> Option.defaultValue []
    let byName = Map.ofList [ "catalog", catalog; "scope", scope; "competitionScope", scope; "fullPlan", fullPlan; "full", fullPlan; "owner", owner; "ownerVerdict", owner ]
    let unmet = required |> List.filter (fun r -> byName.TryFind r |> Option.map statusOf <> Some "ACCEPTED")
    lines |> List.iter (printfn "  %s")
    if unmet.IsEmpty then Pass [ (if required.IsEmpty then "report only (no --require)" else $"""required levels accepted: {String.Join(", ", required)}""") ]
    else Refused (unmet |> List.map (fun r -> $"""required level {r} is {byName.TryFind r |> Option.map statusOf |> Option.defaultValue "unknown"}"""))

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
| "suite-preflight" :: _ -> suitePreflight () |> finish "suite-preflight"
| "authorize-implementation" :: _ -> authorizeImplementation () |> finish "authorize-implementation"
| "check-sequencing" :: _ -> checkSequencing () |> finish "check-sequencing"
| "acceptance-status" :: _ -> acceptanceStatus () |> finish "acceptance-status"
| "spec-preflight" :: _ -> specPreflight () |> finish "spec-preflight"
| _ ->
    eprintfn "usage: dotnet fsi tools/r9/r9.fsx -- install-conditor|workstation|govern|check-governance|gate|spec-preflight ... (see the header)"
    exit 2
