/// Command-line entry points for the governed checks (adversarial audit R2, R5, R6; slices; observer).
///
///   requirements-compiler catalog-acceptance --contract F --approved-contract-sha256 SHA --catalog F --candidate SHA
///                         [--manifest F] [--verification F] [--producer F] [--probe-results F] [--probe-spec F] [--declared-facts F] --out F
///   requirements-compiler ledger --manifest F --events F [--facts F] --out F
///   requirements-compiler slice-manifest --manifest F --dag F --journeys F [--events F] --out F
///   requirements-compiler slice-gate --manifest F --events F --slices F --slice ID --candidate SHA --target-base SHA
///                         --target-head SHA [--gate F] [--journeys F] [--design F] [--regression F] [--build F]
///                         [--live F] [--test-integrity F] --out F
///   requirements-compiler completion-gate --manifest F --events F --candidate SHA [--release-gate F] [--catalog F]
///                         [--journeys F] [--design F] [--facts F] --out F
///   requirements-compiler test-integrity --repo DIR --base SHA --head SHA --base-results DIR --head-results DIR --out F
///   requirements-compiler design-contract --contract F --forma-inventory F --root DIR --candidate SHA [--rendered F] --out F
///   requirements-compiler observe --manifest F --events F --repo DIR --t0-record PATH --deadline-policy F --now ISO
///                         [--checkpoint PATH] [--evidence DIR] [--pins F] [--approvals F --trusted-operator ID] --out F
///
/// Exit codes: 0 PASS (or a report written), 4 REFUSED / INCOMPLETE / blocking findings, 2 usage.
module GovernedCommands

open System
open System.IO
open System.Security.Cryptography
open System.Text.RegularExpressions
open Json
open Scope

let private options (args: string list) =
    args |> List.pairwise |> List.filter (fun (k, _) -> k.StartsWith "--") |> Map.ofList

let private sha256 (path: string) = SHA256.HashData(File.ReadAllBytes path) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()
let private optFile (o: Map<string, string>) key = o.TryFind key |> Option.filter File.Exists |> Option.map parseFile
let private require (o: Map<string, string>) (keys: string list) = keys |> List.forall o.ContainsKey

let private eventsOf (path: string) =
    match parseFile path with
    | JArr xs -> xs
    | j -> arr "events" j

let private factsOf (o: Map<string, string>) =
    match optFile o "--facts" with
    | Some(JObj ps) -> ps |> List.map (fun (k, v) -> k, (match v with JBool b -> (if b then "true" else "false") | JStr s -> s | j -> serialize j)) |> Map.ofList
    | _ -> Map.empty

let private report (name: string) (out: string) (json: Json) (refusals: Refusal list) (pass: string) (fail: string) =
    File.WriteAllText(out, serialize json)
    refusals |> List.iter (fun r -> printfn "  REFUSED %s %s" r.Code r.Detail)
    printfn "%s: %s (%s)" name (if refusals.IsEmpty then pass else fail) out
    if refusals.IsEmpty then 0 else 4

let catalogAcceptanceCommand (args: string list) =
    let o = options args
    if not (require o [ "--contract"; "--approved-contract-sha256"; "--catalog"; "--candidate"; "--out" ]) then
        eprintfn "usage: catalog-acceptance --contract F --approved-contract-sha256 SHA --catalog F --candidate SHA [--manifest F] [--verification F] [--producer F] [--probe-results F] [--probe-spec F] [--declared-facts F] --out F"; 2
    else
        let inputs : CatalogAcceptance.Inputs =
            { Contract = CatalogAcceptance.contractOf (parseFile o["--contract"])
              ContractSha256 = sha256 o["--contract"]
              ApprovedContractSha256 = o["--approved-contract-sha256"]
              Candidate = o["--candidate"]
              Manifest = optFile o "--manifest"
              Verification = optFile o "--verification"
              Producer = CatalogAcceptance.producerOf (optFile o "--producer")
              ProbeResults = optFile o "--probe-results"
              ProbeSpecPresent = o.TryFind "--probe-spec" |> Option.exists File.Exists
              DeclaredFacts = match optFile o "--declared-facts" with Some(JObj ps) -> Map.ofList ps | _ -> Map.empty
              Catalog = parseFile o["--catalog"] }
        let v = CatalogAcceptance.judge inputs
        let code = report "catalog-acceptance" o["--out"] (CatalogAcceptance.verdictJson inputs v) v.Refusals "GOVERNED-ACCEPTED" v.Level
        code

let ledgerCommand (args: string list) =
    let o = options args
    if not (require o [ "--manifest"; "--events"; "--out" ]) then eprintfn "usage: ledger --manifest F --events F [--facts F] --out F"; 2
    else
        match Ledger.replay (parseFile o["--manifest"]) (eventsOf o["--events"]) with
        | Error why -> printfn "  REFUSED %s" why; printfn "ledger: REFUSED"; 4
        | Ok s ->
            let r = Ledger.scopeReport s (factsOf o)
            File.WriteAllText(o["--out"], serialize r)
            printfn "ledger: %s (%s open mandatory; %s)" o["--out"] (intOpt "mandatoryOpen" r |> Option.defaultValue 0L |> string) (strOpt "completion" r |> Option.defaultValue "")
            0

let private estimatesOf (dag: Json) = arr "tasks" dag |> List.choose (fun t -> intOpt "estimateMinutes" t |> Option.map (fun m -> str "id" t, m)) |> Map.ofList

let sliceManifestCommand (args: string list) =
    let o = options args
    if not (require o [ "--manifest"; "--dag"; "--journeys"; "--out" ]) then eprintfn "usage: slice-manifest --manifest F --dag F --journeys F [--events F] --out F"; 2
    else
        let manifest = parseFile o["--manifest"]
        let state = o.TryFind "--events" |> Option.map (fun e -> Ledger.replay manifest (eventsOf e))
        match state with
        | Some(Error why) -> printfn "  REFUSED %s" why; printfn "slice-manifest: REFUSED"; 4
        | _ ->
            let json, refusals = Ledger.sliceManifest manifest (estimatesOf (parseFile o["--dag"])) (Ledger.journeysOf (parseFile o["--journeys"])) (state |> Option.bind (function Ok s -> Some s | Error _ -> None))
            report "slice-manifest" o["--out"] json refusals "COMPLETE COVERAGE" "INCOMPLETE COVERAGE"

let sliceGateCommand (args: string list) =
    let o = options args
    if not (require o [ "--manifest"; "--events"; "--slices"; "--slice"; "--candidate"; "--target-base"; "--target-head"; "--out" ]) then
        eprintfn "usage: slice-gate --manifest F --events F --slices F --slice ID --candidate SHA --target-base SHA --target-head SHA [--gate F] [--journeys F] [--design F] [--regression F] [--build F] [--live F] [--test-integrity F] --out F"; 2
    else
        match Ledger.replay (parseFile o["--manifest"]) (eventsOf o["--events"]) with
        | Error why -> printfn "  REFUSED %s" why; printfn "slice-gate: REFUSED"; 4
        | Ok s ->
            match arr "slices" (parseFile o["--slices"]) |> List.tryFind (fun x -> strOpt "id" x = Some o["--slice"]) with
            | None -> printfn "  REFUSED SLICE_UNKNOWN %s is not in the slice manifest" o["--slice"]; printfn "slice-gate: REFUSED"; 4
            | Some slice ->
                let e : Ledger.SliceEvidence =
                    { Slice = slice; Candidate = o["--candidate"]; TargetBase = o["--target-base"]; TargetHead = o["--target-head"]
                      Gate = optFile o "--gate"; Journeys = optFile o "--journeys"; DesignContract = optFile o "--design"; Regression = optFile o "--regression"
                      Build = optFile o "--build"; LiveProvider = optFile o "--live"; TestIntegrity = optFile o "--test-integrity" }
                let refusals = Ledger.sliceGate s e
                report "slice-gate" o["--out"] (Ledger.sliceVerdictJson e refusals) refusals "PASS (slice only; never a release verdict)" "REFUSED (do not merge)"

let completionGateCommand (args: string list) =
    let o = options args
    if not (require o [ "--manifest"; "--events"; "--candidate"; "--out" ]) then
        eprintfn "usage: completion-gate --manifest F --events F --candidate SHA [--release-gate F] [--catalog F] [--journeys F] [--design F] [--facts F] --out F"; 2
    else
        match Ledger.replay (parseFile o["--manifest"]) (eventsOf o["--events"]) with
        | Error why -> printfn "  REFUSED %s" why; printfn "completion-gate: INCOMPLETE"; 4
        | Ok s ->
            let e : Ledger.ReleaseEvidence =
                { Candidate = o["--candidate"]; ReleaseGate = optFile o "--release-gate"; CatalogAcceptance = optFile o "--catalog"
                  Journeys = optFile o "--journeys"; DesignContract = optFile o "--design"; Facts = factsOf o }
            let refusals = Ledger.completionGate s e
            report "completion-gate" o["--out"] (Ledger.completionJson e refusals) refusals "COMPLETE" "INCOMPLETE"

// ---- test integrity -------------------------------------------------------------------------------------------------

/// The test identities a run-suites output directory records as executed: TRX test names and Playwright spec titles
/// (file › title).
let testIdentities (dir: string) : Set<string> =
    if not (Directory.Exists dir) then Set.empty
    else
        let trx =
            Directory.EnumerateFiles(dir, "*.trx", SearchOption.AllDirectories)
            |> Seq.collect (fun f -> Regex.Matches(File.ReadAllText f, "<UnitTestResult[^>]*testName=\"([^\"]+)\"[^>]*outcome=\"(Passed|Failed)\"") |> Seq.map (fun m -> m.Groups[1].Value))
        let rec specs (prefix: string) (suite: Json) : string seq =
            seq {
                let title = strOpt "title" suite |> Option.defaultValue ""
                let here = if prefix = "" then title else prefix + " › " + title
                for s in arr "specs" suite do
                    if arr "tests" s |> List.exists (fun t -> arr "results" t |> List.exists (fun r -> List.contains (strOpt "status" r |> Option.defaultValue "") [ "passed"; "failed" ])) then
                        yield here + " › " + (strOpt "title" s |> Option.defaultValue "")
                for child in arr "suites" suite do yield! specs here child
            }
        let pw =
            Directory.EnumerateFiles(dir, "results.json", SearchOption.AllDirectories)
            |> Seq.filter (fun f -> f <> Path.Combine(dir, "results.json"))
            |> Seq.collect (fun f -> try arr "suites" (parseFile f) |> Seq.collect (specs "") with _ -> Seq.empty)
        Seq.append trx pw |> Set.ofSeq

let private diffLines (repo: string) (baseSha: string) (headSha: string) : Ledger.TestDiffLine list =
    match Git.run repo [ "diff"; "--unified=0"; "--no-color"; baseSha; headSha ] with
    | Error _ -> []
    | Ok bytes ->
        let lines = (Text.Encoding.UTF8.GetString bytes).Split('\n') |> List.ofArray
        lines
        |> List.fold
            (fun (file, acc) (l: string) ->
                if l.StartsWith "+++ b/" then l.Substring 6, acc
                elif l.StartsWith "+++ " || l.StartsWith "--- " then file, acc
                elif l.StartsWith "+" then file, ({ File = file; Added = true; Text = l.Substring 1 }: Ledger.TestDiffLine) :: acc
                elif l.StartsWith "-" then file, ({ File = file; Added = false; Text = l.Substring 1 }: Ledger.TestDiffLine) :: acc
                else file, acc)
            ("", [])
        |> snd
        |> List.rev

let testIntegrityCommand (args: string list) =
    let o = options args
    if not (require o [ "--repo"; "--base"; "--head"; "--base-results"; "--head-results"; "--out" ]) then
        eprintfn "usage: test-integrity --repo DIR --base SHA --head SHA --base-results DIR --head-results DIR --out F"; 2
    else
        let refusals = Ledger.testIntegrity (testIdentities o["--base-results"]) (testIdentities o["--head-results"]) (diffLines o["--repo"] o["--base"] o["--head"])
        report "test-integrity" o["--out"] (Ledger.testIntegrityJson o["--head"] refusals) refusals "PASS" "REFUSED"

// ---- design contract ------------------------------------------------------------------------------------------------

let private skipped = [ "node_modules"; "bin"; "obj"; "vendor"; ".git"; "test-results"; "playwright-report" ]

let sourceFiles (root: string) : DesignContract.SourceFile list =
    let rec walk (dir: string) : string seq =
        seq {
            for f in Directory.EnumerateFiles dir do yield f
            for d in Directory.EnumerateDirectories dir do
                if not (List.contains (Path.GetFileName d) skipped) then yield! walk d
        }
    [ "web"; "dist"; "src" ]
    |> List.map (fun d -> Path.Combine(root, d))
    |> List.filter Directory.Exists
    |> List.collect (fun d -> walk d |> List.ofSeq)
    |> List.filter (fun f -> [ ".html"; ".css"; ".js"; ".mjs"; ".fs" ] |> List.exists (fun x -> f.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
    |> List.map (fun f -> ({ Path = Path.GetRelativePath(root, f).Replace('\\', '/'); Text = File.ReadAllText f } : DesignContract.SourceFile))

let designContractCommand (args: string list) =
    let o = options args
    if not (require o [ "--contract"; "--forma-inventory"; "--root"; "--candidate"; "--out" ]) then
        eprintfn "usage: design-contract --contract F --forma-inventory F --root DIR --candidate SHA [--rendered F] --out F"; 2
    else
        let c = DesignContract.contractOf (parseFile o["--contract"]) (parseFile o["--forma-inventory"])
        let pkg = Path.Combine(o["--root"], "package.json")
        let refusals = DesignContract.check c (sourceFiles o["--root"]) (if File.Exists pkg then Some(parseFile pkg) else None)
        let json = DesignContract.verdictJson o["--candidate"] refusals (optFile o "--rendered")
        File.WriteAllText(o["--out"], serialize json)
        strs "causes" json |> List.iter (printfn "  REFUSED %s")
        let pass = strOpt "verdict" json = Some "PASS"
        printfn "design-contract: %s (%s)" (if pass then "PASS" else "REFUSED") o["--out"]
        if pass then 0 else 4

// ---- observer -------------------------------------------------------------------------------------------------------

let private commitsOf (repo: string) : Observer.Commit list =
    match Git.run repo [ "log"; "--reverse"; "--name-only"; "--format=@%H|%cI|%s" ] with
    | Error _ -> []
    | Ok bytes ->
        (Text.Encoding.UTF8.GetString bytes).Split('@', StringSplitOptions.RemoveEmptyEntries)
        |> Array.choose (fun block ->
            let lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            match lines[0].Split('|', 3) with
            | [| sha; at; msg |] -> Some({ Sha = sha; At = DateTimeOffset.Parse at; Message = msg; Files = lines |> Array.skip 1 |> Array.map (fun l -> l.Trim()) |> List.ofArray }: Observer.Commit)
            | _ -> None)
        |> List.ofArray

let private versionsOf (repo: string) (path: string) : (string * Json) list =
    match Git.run repo [ "log"; "--reverse"; "--format=%H"; "--"; path ] with
    | Error _ -> []
    | Ok bytes ->
        (Text.Encoding.UTF8.GetString bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries)
        |> List.ofArray
        |> List.choose (fun sha -> match Git.run repo [ "show"; sprintf "%s:%s" sha path ] with Ok b -> (try Some(sha, parseBytes b) with _ -> None) | Error _ -> None)

let observeCommand (args: string list) =
    let o = options args
    if not (require o [ "--manifest"; "--events"; "--repo"; "--t0-record"; "--deadline-policy"; "--now"; "--out" ]) then
        eprintfn "usage: observe --manifest F --events F --repo DIR --t0-record PATH --deadline-policy F --now ISO [--checkpoint PATH] [--evidence DIR] [--pins F] [--approvals F --trusted-operator ID] --out F"; 2
    else
        let repo = o["--repo"]
        let t0Versions = versionsOf repo o["--t0-record"]
        let policy = parseFile o["--deadline-policy"]
        let clock =
            match t0Versions with
            | (_, first) :: _ -> Continuation.boundClock first policy None None |> Result.toOption
            | [] -> None
        let now = DateTimeOffset.Parse o["--now"]
        let clock' = clock |> Option.defaultValue { T0 = now; Deadline = now }
        let evidence = o.TryFind "--evidence" |> Option.filter Directory.Exists
        let files pattern = evidence |> Option.map (fun d -> Directory.EnumerateFiles(d, pattern, SearchOption.AllDirectories) |> Seq.sort |> Seq.map parseFile |> List.ofSeq) |> Option.defaultValue []
        let bySchema (schema: string) = files "*.json" |> List.filter (fun j -> strOpt "schema" j = Some schema)
        let checkpoint = o.TryFind "--checkpoint" |> Option.map (fun p -> Path.Combine(repo, p)) |> Option.filter File.Exists
        let pins = optFile o "--pins"
        let pinned = match pins |> Option.bind (field "digests") with Some(JObj ps) -> ps |> List.map (fun (k, v) -> k, asStr v) |> Map.ofList | _ -> Map.empty
        let head = pinned |> Map.map (fun path _ -> let p = Path.Combine(repo, path) in if File.Exists p then sha256 p else "")
        let manifest = parseFile o["--manifest"]
        let slices = bySchema "indy-init.slice-execution-manifest/1" |> List.tryLast
        let remaining =
            slices |> Option.map (fun m -> arr "slices" m |> List.filter (fun x -> strOpt "status" x <> Some "integrated") |> List.sumBy (fun x -> intOpt "estimateMinutes" x |> Option.defaultValue 0L |> int))
        let observation : Observer.Observation =
            { Now = now; Clock = clock'; Manifest = manifest
              CheckpointScope = checkpoint |> Option.bind (fun p -> strOpt "scopeSha256" (parseFile p))
              CheckpointAt = checkpoint |> Option.bind (fun p -> strOpt "at" (parseFile p)) |> Option.map DateTimeOffset.Parse
              T0Versions = t0Versions; PinnedDigests = pinned; HeadDigests = head
              Approvals = (match o.TryFind "--approvals", o.TryFind "--trusted-operator" with Some a, Some op -> Continuation.approvalChanges (parseFile a) (set [ op ]) | _ -> [])
              Events = eventsOf o["--events"]; Commits = commitsOf repo
              Gates = bySchema "indy-init.r9-gate/1" @ (files "gate.json")
              CatalogAcceptance = bySchema "indy-init.catalog-acceptance/1" |> List.tryLast
              DesignContract = bySchema "indy-init.design-contract/1" |> List.tryLast
              LiveResults = bySchema "indy-init.journey-results/1"
              StopDecisions = bySchema "indy-init.stop-decision/1"
              TestIntegrity = bySchema "indy-init.test-integrity/1"
              Claims = bySchema "indy-init.builder-claim/1"
              SliceManifest = slices; RemainingEstimate = remaining }
        let findings = Observer.observe observation
        File.WriteAllText(o["--out"], serialize (Observer.findingsJson observation findings))
        findings |> List.iter (fun x -> printfn "  %s %s %s" (x.Severity.ToUpperInvariant()) x.Code x.Detail)
        let blocking = findings |> List.filter (fun x -> x.Severity = "blocking") |> List.length
        printfn "observe: %d finding(s), %d blocking (%s)" findings.Length blocking o["--out"]
        if blocking = 0 then 0 else 4

// ---- Forma inventory of an approved release --------------------------------------------------------------------------

/// `forma-inventory --tarball F --out F`: the published surface of a Forma npm tarball (V41Package.formaInventory), so
/// the design contract can accept a newer compatible Forma whose tokens or classes differ.
let formaInventoryCommand (args: string list) =
    let o = options args
    if not (require o [ "--tarball"; "--out" ]) then eprintfn "usage: forma-inventory --tarball F --out F"; 2
    else
        use gz = new IO.Compression.GZipStream(File.OpenRead o["--tarball"], IO.Compression.CompressionMode.Decompress)
        use tar = new Formats.Tar.TarReader(gz)
        let entries =
            Seq.unfold (fun () -> match tar.GetNextEntry() with null -> None | e -> Some(e, ())) ()
            |> Seq.filter (fun e -> not (isNull e.DataStream))
            |> Seq.map (fun e -> use r = new StreamReader(e.DataStream) in e.Name, r.ReadToEnd())
            |> Map.ofSeq
        let pkg = parseBytes (Text.Encoding.UTF8.GetBytes entries["package/package.json"])
        match entries.TryFind "package/dist/all.css" with
        | None -> printfn "  REFUSED the tarball has no package/dist/all.css"; 4
        | Some css ->
            let contracts = entries.TryFind "package/figma/component-contracts.json" |> Option.map (Text.Encoding.UTF8.GetBytes >> parseBytes) |> Option.defaultValue (JObj [])
            File.WriteAllText(o["--out"], serialize (V41Package.formaInventory (str "version" pkg) (sha256 o["--tarball"]) css contracts))
            printfn "forma-inventory: %s %s (%s)" (str "version" pkg) (sha256 o["--tarball"]) o["--out"]
            0
