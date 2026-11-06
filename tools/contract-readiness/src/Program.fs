module Causa.ContractReadiness.Program

open System
open System.IO

let private usage =
    """contract-readiness: Causa pre-competition contract readiness validator (F#, no packages)

  generate --causa-data <dir> [--indy-root <dir>]
      Regenerates FIELD-REGISTRY.json, HISTORICAL-DATA-INVENTORY.json, VERIFICATION-MATRIX.json,
      CONTRACT-READINESS.json and PACKAGE-MANIFEST.json in docs/contract-readiness/causa-precompetition.

  check --causa-data <dir> [--indy-root <dir>] [--require-pass]
      Writes nothing. Runs every contract check and negative control, and requires the committed generated files
      and the package checksum to equal a fresh generation.
      Exit 0: the suite ran, every check passed or is declared UNVERIFIED, every negative control failed as expected,
              and the package reproduces. The readiness verdict is printed (PASS, BLOCKED or UNVERIFIED).
      Exit 1: a check failed, a negative control was not detected, or the package does not reproduce.
      Exit 2: with --require-pass, the verdict is not PASS.
      Exit 64: usage error.

  The causa-data checkout must be kemiller2002/causa-data at e8259df74add723765c57a5b1a5888d9c3f707af."""

let private arg (name: string) (argv: string list) =
    argv |> List.pairwise |> List.tryPick (fun (a, b) -> if a = name then Some b else None)

let rec private findIndyRoot (dir: string) =
    if File.Exists(Path.Combine(dir, "global.json")) && Directory.Exists(Path.Combine(dir, "requirements-compiled")) then Some dir
    else
        match Directory.GetParent dir with
        | null -> None
        | p -> findIndyRoot p.FullName

/// HEAD of a git checkout without running git: a detached SHA, or the ref it names (loose or packed).
let private gitHead (repo: string) =
    let gitDir = Path.Combine(repo, ".git")
    let headFile = Path.Combine(gitDir, "HEAD")
    if not (File.Exists headFile) then None
    else
        let head = File.ReadAllText(headFile).Trim()
        if not (head.StartsWith "ref: ") then Some head
        else
            let r = head.Substring 5
            let loose = Path.Combine(gitDir, r)
            if File.Exists loose then Some(File.ReadAllText(loose).Trim())
            else
                let packed = Path.Combine(gitDir, "packed-refs")
                if File.Exists packed then
                    File.ReadAllLines packed |> Array.tryPick (fun l -> let p = l.Split(' ') in if p.Length = 2 && p.[1] = r then Some p.[0] else None)
                else None

let pinnedCommit = "e8259df74add723765c57a5b1a5888d9c3f707af"

let private generatedNames = [ "FIELD-REGISTRY.json"; "HISTORICAL-DATA-INVENTORY.json"; "VERIFICATION-MATRIX.json"; "CONTRACT-READINESS.json" ]

let run (mode: string) (dataRoot: string) (indyRoot: string) (requirePass: bool) =
    let pkgDir = Path.Combine(indyRoot, "docs", "contract-readiness", "causa-precompetition")
    let approvalsPath = Path.Combine(indyRoot, "kickoff", "rehearsal-007", "approvals.causa.json")
    let recorded = Package.recordedDecisions approvalsPath
    // A recorded decision stays in force only while every file it pinned is unchanged (owner-freeze re-asks it otherwise).
    // A pinned PACKAGE-MANIFEST.json is generated, so it is compared by content: the pinned version is found in git
    // history, and every authored package file and validator source it lists must still have its pinned sha256.
    let generatedFiles = Set.ofList [ "FIELD-REGISTRY.json"; "HISTORICAL-DATA-INVENTORY.json"; "VERIFICATION-MATRIX.json"; "CONTRACT-READINESS.json"; "proposals/owner-decision-requests.json" ]
    let gitText (args: string list) =
        try
            let psi = Diagnostics.ProcessStartInfo("git", ["-C"; indyRoot] @ args, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
            use p = Diagnostics.Process.Start psi
            let out = p.StandardOutput.ReadToEnd()
            p.WaitForExit()
            if p.ExitCode = 0 then Some out else None
        with _ -> None
    let changedSince (path: string) (sha: string) : string list option =
        let rel = path
        let full = Path.Combine(indyRoot, rel)
        if rel.EndsWith "/PACKAGE-MANIFEST.json" then
            let pinned =
                gitText [ "log"; "--format=%H"; "--"; rel ]
                |> Option.map (fun s -> s.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray)
                |> Option.defaultValue []
                |> List.tryPick (fun c -> gitText [ "show"; c + ":" + rel ] |> Option.filter (fun t -> Json.sha256Text t = sha))
            pinned
            |> Option.map (fun text ->
                let m = Json.parse text
                let dir = Path.GetDirectoryName rel
                (Json.arrOf "files" m |> List.filter (fun f -> not (generatedFiles.Contains(Json.fieldStr "path" f))) |> List.map (fun f -> Path.Combine(dir, Json.fieldStr "path" f), Json.fieldStr "sha256" f))
                @ (Json.arrOf "validator" m |> List.map (fun f -> Json.fieldStr "path" f, Json.fieldStr "sha256" f))
                |> List.filter (fun (p, s) -> not (File.Exists(Path.Combine(indyRoot, p))) || Json.sha256File (Path.Combine(indyRoot, p)) <> s)
                |> List.map fst)
        elif File.Exists full then Some(if Json.sha256File full = sha then [] else [ rel ])
        else Some [ rel ]
    let statusOf (id: string) =
        recorded.TryFind id
        |> Option.map (fun (approval, option, pins) ->
            let changes = pins |> List.map (fun (p, s) -> p, changedSince p s)
            match changes |> List.tryFind (snd >> Option.isNone) with
            | Some(p, _) -> sprintf "open (recorded %s in %s, but the pinned version of %s is not in this checkout's history: unverifiable)" option approval p
            | None ->
                match changes |> List.collect (snd >> Option.defaultValue []) with
                | [] -> sprintf "recorded %s (%s)" option approval
                | changed -> sprintf "open (stale: recorded %s in %s, but pinned content changed since: %s; the owner confirms it again with owner-freeze decide contract-readiness)" option approval (String.Join(", ", changed |> List.map Path.GetFileName |> List.distinct)))
    let pkg = Package.load pkgDir statusOf
    // The owner-freeze request file (`owner-freeze.fsx decide contract-readiness`), generated from
    // OWNER-DECISIONS.proposed.json so the two never disagree. ODR-V415-02 is recorded by `approve v4.1.5` instead.
    let requestsText =
        let od = Json.parseFile (Path.Combine(pkgDir, "OWNER-DECISIONS.proposed.json"))
        let requests =
            Json.arrOf "decisions" od
            |> List.filter (fun d -> (Json.fieldStr "decisionId" d).StartsWith "OD-CR-")
            |> List.map (fun d ->
                Json.o
                    ([ "id", Json.s (Json.fieldStr "decisionId" d)
                       "kind", Json.s "decision"
                       "title", Json.s (Json.fieldStr "title" d)
                       "question", Json.s (Json.fieldStr "question" d)
                       "options", Json.field "alternatives" d
                       "recommendation", Json.s (Json.fieldStr "recommendation" d)
                       "rationale", Json.s (Json.fieldStr "conflictingEvidence" d)
                       "impact", Json.s (sprintf "Compatibility: %s Deferral: %s Affects: %s." (Json.fieldStr "compatibilityImpact" d) (Json.fieldStr "deferralConsequence" d) (String.Join(", ", Json.strs "affects" d))) ]
                     @ (match Json.tryField "pins" d with Some p -> [ "pins", p ] | None -> [])))
        Json.render (
            Json.o
                [ "schema", Json.s "indy-init.v41-owner-decision-requests/1"
                  "status", Json.s "Pending: every request below. The owner records them with tools/owner-freeze.fsx decide contract-readiness (once this file is on origin/main). No request here is recorded by an agent (RDP-003). Generated by tools/contract-readiness from OWNER-DECISIONS.proposed.json; do not edit by hand. ODR-V415-02 is recorded separately with owner-freeze.fsx approve v4.1.5."
                  "recordFormat", Json.s "One operator approval in kickoff/rehearsal-007/approvals.causa.json (prefix APR-CR-OWNER), one change per decision {decision_id, action: decide, option}, with the sha256 at origin/main of each file a request pins, committed by the owner without agent trailers."
                  "requests", JArr requests ])
    let schemaRoot = Path.Combine(dataRoot, "contract", "v1.0.0", "schemas")
    let fieldMapping = Path.Combine(dataRoot, "contract", "v1.0.0", "FIELD-MAPPING.md")
    let schemas = Schema.compile schemaRoot
    let reg = FieldRegistry.fromSchemas schemas fieldMapping pkg.ProposedFields pkg.ProposedTypes
    let path (p: string) = Path.Combine(indyRoot, p)
    let v414 = FsContract.parsePackage (path "requirements-compiled/v4.1.4/contracts")
    let v415 = FsContract.parsePackage (path "requirements-compiled/v4.1.5/contracts")
    let head = gitHead dataRoot
    let sdk = Json.parseFile (path "global.json") |> Json.field "sdk" |> Json.fieldStr "version"
    let pin =
        Json.o
            [ "causaDataRepository", Json.s "https://github.com/kemiller2002/causa-data"
              "causaDataCommitPinned", Json.s pinnedCommit
              "schemaBundleSha256", Json.s schemas.BundleSha256
              "storageContract", Json.s "causa-storage v1.0.0 (contract/v1.0.0)"
              "successorCandidate", Json.s "requirements-compiled/v4.1.5 (CANDIDATE)"
              "successorContractsSha256", Json.s v415.Sha256
              "frozenBaseline", Json.s "requirements-compiled/v4.1.4 (tag indy-init-requirements-v4.1.4)"
              "frozenContractsSha256", Json.s v414.Sha256
              "catalogSha256", Json.s (Json.sha256File (path "requirements/catalogs/causa-investigation.requirement-catalog.json"))
              "screenManifestSha256", Json.s (Json.sha256File (path "proposals/r10-contract-first-ui/SCREEN-MANIFEST.json"))
              "dotnetSdk", Json.s sdk ]
    let registryText = Json.render (FieldRegistry.toJson pin reg)
    let committed name =
        let p = Path.Combine(pkgDir, name)
        if File.Exists p then File.ReadAllText p else ""
    let active = Json.parseFile (Path.Combine(dataRoot, "catalog", "active.json")) |> Json.fieldStr "activeCatalog"
    let _, catalogIndex = Historical.discover dataRoot
    let packageTexts =
        Directory.GetFiles(pkgDir, "*", SearchOption.AllDirectories)
        |> Array.sort
        |> Array.map (fun f -> Path.GetRelativePath(pkgDir, f), File.ReadAllText f)
        |> List.ofArray
    let inputs =
        { Registry = reg
          GeneratedRegistryText = registryText
          CommittedRegistryText = (if mode = "generate" then registryText else committed "FIELD-REGISTRY.json")
          Package = pkg
          V414 = v414
          V415 = v415
          LoadHistories = fun r vs -> Historical.loadAll r vs dataRoot |> fst
          Catalogs = catalogIndex
          ActiveCatalog = active
          DataRoot = dataRoot
          IndyRoot = indyRoot
          Catalog = Json.parseFile (path "requirements/catalogs/causa-investigation.requirement-catalog.json")
          ScreenManifest = Json.parseFile (path "proposals/r10-contract-first-ui/SCREEN-MANIFEST.json")
          UiMap = Json.parseFile (Path.Combine(pkgDir, "UI-FIELD-MAP.json"))
          SourcePin = Json.parseFile (path "proposals/r10-historical-data-compatibility/source-pin.json")
          PackageTexts = packageTexts }
    let headCheck =
        { CheckId = "CR-SRC-02"
          Title = "The causa-data checkout is the pinned commit"
          Category = "sources"
          Outcome =
            match head with
            | Some h when h = pinnedCommit -> Pass
            | Some h -> Fail [ sprintf "causa-data HEAD is %s, pinned %s" h pinnedCommit ]
            | None -> Unverified "the causa-data directory is not a git checkout; file digests (CR-SRC-01, schema bundle) still apply"
          Metrics = [ "head", Json.opt Json.s head ] }
    let checks, facts = Checks.run inputs
    let checks = headCheck :: checks
    let controls = NegativeControls.run inputs
    let histories = inputs.LoadHistories reg pkg.Variants
    let inventoryText =
        Json.render (Gate.inventory inputs histories (Historical.negativeFixtures reg dataRoot) (Historical.provenance dataRoot histories) pin)
    let matrixJson = Gate.matrix pkg checks controls
    let matrixText = Json.render matrixJson
    // Reproducibility of the generated files other than the readiness report, which is derived from this run.
    let reproducibleBefore =
        mode = "generate"
        || ([ "proposals/owner-decision-requests.json", requestsText; "FIELD-REGISTRY.json", registryText; "HISTORICAL-DATA-INVENTORY.json", inventoryText; "VERIFICATION-MATRIX.json", matrixText ]
            |> List.forall (fun (n, t) -> committed n = t))
    let verdict, blocked, unverified = Gate.decide pkg checks controls reproducibleBefore
    let count status = checks |> List.filter (fun c -> Gate.outcomeText c.Outcome = status) |> List.length
    let bindingsCovered = pkg.Bindings |> List.filter (fun b -> b.Status.StartsWith "covered") |> List.length
    let readiness =
        Json.o
            [ "schema", Json.s "causa.contract-readiness.readiness/1"
              "generatedBy", Json.s "tools/contract-readiness (dotnet run -- generate); do not edit by hand"
              "contractPackage", Json.s ("causa-precompetition " + pkg.Version)
              "verdict", Json.s (Gate.verdictText verdict)
              "blockedReasons", Json.ss blocked
              "unverifiedReasons", Json.ss unverified
              "waivers", JArr []
              "waiverPolicy", Json.s "No waiver exists. BLOCKED and UNVERIFIED change only when the cause is removed: an owner record, a released contract, or executed evidence."
              "source", pin
              "criteria",
              JArr
                  [ Json.o [ "criterion", Json.s "Every mandatory contract obligation is represented"; "met", Json.b (count "FAIL" = 0 && pkg.Bindings |> List.forall (fun b -> b.Status <> "blocked")) ]
                    Json.o [ "criterion", Json.s "All pinned historical records are compatible (lossless under the successor candidate)"; "met", Json.b (checks |> List.filter (fun c -> c.CheckId.StartsWith "CR-HIST" || c.CheckId = "CR-REP-01") |> List.forall (fun c -> c.Outcome = Pass)) ]
                    Json.o [ "criterion", Json.s "Required fields and commands are covered"; "met", Json.b (pkg.Bindings |> List.forall (fun b -> b.Status <> "blocked") && pkg.Commands |> List.forall (fun c -> c.Status <> "proposed-successor")) ]
                    Json.o [ "criterion", Json.s "Negative controls fail as expected"; "met", Json.b (controls |> List.forall (fun r -> List.isEmpty r.DidNotFail)) ]
                    Json.o [ "criterion", Json.s "No unresolved blocking owner decision remains"; "met", Json.b (pkg.Decisions |> List.forall (fun d -> not d.Blocking || not (d.Status.StartsWith "open"))) ]
                    Json.o [ "criterion", Json.s "The contract package is versioned and reproducible"; "met", Json.b reproducibleBefore ] ]
              "answers",
              Json.o
                  [ "historicalEventsExamined", Json.o [ "historicalCases", facts.["eventsHistorical"]; "activeCatalog", facts.["eventsActiveCatalog"]; "allDistinctHistories", facts.["eventsDistinct"]; "allCopies", facts.["eventsAllCopies"] ]
                    "representedLosslessly", Json.o [ "successorCandidateV415", facts.["rep415All"]; "frozenV414HistoricalCases", facts.["rep414Historical"] ]
                    "fieldsDefined", Json.o [ "total", Json.i reg.Fields.Length; "wire", Json.i (FieldRegistry.wireFields reg).Length; "proposed", Json.i pkg.ProposedFields.Length ]
                    "commandsDefined", Json.o [ "total", Json.i pkg.Commands.Length; "definedInV415", Json.i (pkg.Commands |> List.filter (fun c -> c.Status = "defined-v4.1.5") |> List.length); "proposed", Json.i (pkg.Commands |> List.filter (fun c -> c.Status = "proposed-successor") |> List.length); "uiOnly", Json.i (pkg.Commands |> List.filter (fun c -> c.Status = "defined-ui-only") |> List.length); "deferredOrRefused", Json.i (pkg.Commands |> List.filter (fun c -> c.Status = "deferred-not-required" || c.Status = "refused-no-wire-form") |> List.length) ]
                    "eventsDefined", Json.o [ "variants", Json.i pkg.Variants.Length; "wire", Json.i (Checks.wireVariants pkg).Length; "proposed", Json.i (pkg.Variants |> List.filter (fun v -> v.Status = FieldRegistry.statusProposed) |> List.length); "v415CoreEventCases", Json.i (FsContract.caseNames v415 "CoreEvent").Length ]
                    "uiBindings", Json.o [ "total", Json.i pkg.Bindings.Length; "covered", Json.i bindingsCovered; "blocked", Json.i (pkg.Bindings |> List.filter (fun b -> b.Status = "blocked") |> List.length); "notApplicable", Json.i (pkg.Bindings |> List.filter (fun b -> b.Status = "not-applicable-v1.0.0") |> List.length) ]
                    "verificationObligations", Json.field "counts" matrixJson
                    "checks", Json.o [ "total", Json.i checks.Length; "passed", Json.i (count "PASS"); "failed", Json.i (count "FAIL"); "unverified", Json.i (count "UNVERIFIED") ]
                    "negativeControls", Json.o [ "total", Json.i controls.Length; "failedAsExpected", Json.i (controls |> List.filter (fun r -> List.isEmpty r.DidNotFail) |> List.length) ]
                    "openBlockingDecisions", Json.ss (pkg.Decisions |> List.filter (fun d -> d.Blocking && d.Status.StartsWith "open") |> List.map (fun d -> d.DecisionId))
                    "readyToFreeze", Json.b (verdict = VerdictPass) ]
              "checks", JArr(checks |> List.map Gate.checkJson)
              "negativeControls", JArr(controls |> List.map Gate.controlJson) ]
    let readinessText = Json.render readiness
    let outputs = [ "proposals/owner-decision-requests.json", requestsText; "FIELD-REGISTRY.json", registryText; "HISTORICAL-DATA-INVENTORY.json", inventoryText; "VERIFICATION-MATRIX.json", matrixText; "CONTRACT-READINESS.json", readinessText ]
    let manifestText (texts: (string * string) list) =
        let pkgFiles =
            let onDisk = Directory.GetFiles(pkgDir, "*", SearchOption.AllDirectories) |> Array.map (fun f -> Path.GetRelativePath(pkgDir, f).Replace('\\', '/')) |> List.ofArray
            (onDisk @ (texts |> List.map fst)) |> List.distinct |> List.filter ((<>) "PACKAGE-MANIFEST.json") |> List.sort
        let shaOf (name: string) =
            match texts |> List.tryFind (fst >> (=) name) with
            | Some(_, t) -> Json.sha256Text t
            | None -> Json.sha256File (Path.Combine(pkgDir, name))
        let toolDir = Path.Combine(indyRoot, "tools", "contract-readiness")
        let toolFiles =
            Directory.GetFiles(toolDir, "*", SearchOption.AllDirectories)
            |> Array.map (fun f -> Path.GetRelativePath(indyRoot, f).Replace('\\', '/'))
            |> Array.filter (fun f -> not (f.Contains "/bin/" || f.Contains "/obj/"))
            |> Array.sort |> List.ofArray
        let entries = pkgFiles |> List.map (fun n -> n, shaOf n)
        let toolEntries = toolFiles |> List.map (fun n -> n, Json.sha256File (Path.Combine(indyRoot, n)))
        let checksum = (entries @ toolEntries) |> List.map (fun (n, s) -> s + "  " + n + "\n") |> String.concat "" |> Json.sha256Text
        Json.render (
            Json.o
                [ "schema", Json.s "causa.contract-readiness.package-manifest/1"
                  "contractPackage", Json.s ("causa-precompetition " + pkg.Version)
                  "packageChecksum", Json.s checksum
                  "checksumRule", Json.s "sha256 of the lines '<sha256>  <path>\\n' for every package file (except this manifest) and every validator source file, in this order"
                  "dependencies", pin
                  "schemaVersions", Json.o [ "causa-storage", Json.s "1.0.0 (wire); 1.1.0 proposed (OD-CR-01)"; "formatVersion", Json.s "1.0.0"; "semanticVersion", Json.s "1.0.0"; "projectionVersion", Json.i 1; "requirementsPackage", Json.s "v4.1.5 CANDIDATE (successor of frozen v4.1.4)"; "catalog", Json.s "CAT-CAUSA-INVESTIGATION INV-001..044" ]
                  "files", JArr(entries |> List.map (fun (n, s) -> Json.o [ "path", Json.s n; "sha256", Json.s s ]))
                  "validator", JArr(toolEntries |> List.map (fun (n, s) -> Json.o [ "path", Json.s n; "sha256", Json.s s ])) ])
    let manifest = manifestText outputs
    let failedChecks = checks |> List.filter (fun c -> match c.Outcome with Fail _ -> true | _ -> false)
    let undetected = controls |> List.filter (fun r -> not (List.isEmpty r.DidNotFail))
    printfn "contract-readiness %s: causa-data %s, SDK %s" mode (defaultArg head "(not a git checkout)") sdk
    checks |> List.iter (fun c ->
        printfn "  %-11s %-10s %s" c.CheckId (Gate.outcomeText c.Outcome) c.Title
        match c.Outcome with
        | Fail ps -> ps |> List.truncate 8 |> List.iter (printfn "      - %s")
        | _ -> ())
    controls |> List.iter (fun r -> printfn "  %-11s %-10s %s -> %s" r.Control.ControlId (if List.isEmpty r.DidNotFail then "DETECTED" else "MISSED") r.Control.Description (String.Join(",", r.Control.MustFail)))
    match mode with
    | "generate" ->
        outputs |> List.iter (fun (n, t) -> Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(pkgDir, n))) |> ignore; File.WriteAllText(Path.Combine(pkgDir, n), t))
        File.WriteAllText(Path.Combine(pkgDir, "PACKAGE-MANIFEST.json"), manifest)
        printfn "verdict: %s (%d blocked reasons, %d unverified)" (Gate.verdictText verdict) blocked.Length unverified.Length
        printfn "package checksum: %s" (Json.parse manifest |> Json.fieldStr "packageChecksum")
        if failedChecks.IsEmpty && undetected.IsEmpty then 0 else 1
    | _ ->
        let diffs = (outputs @ [ "PACKAGE-MANIFEST.json", manifest ]) |> List.filter (fun (n, t) -> committed n <> t) |> List.map fst
        diffs |> List.iter (printfn "  NOT REPRODUCED: %s differs from a fresh generation")
        printfn "package checksum: %s (committed %s)" (Json.parse manifest |> Json.fieldStr "packageChecksum") (match committed "PACKAGE-MANIFEST.json" with "" -> "none" | t -> Json.parse t |> Json.fieldStr "packageChecksum")
        printfn "verdict: %s" (Gate.verdictText verdict)
        blocked |> List.iter (printfn "  BLOCKED: %s")
        unverified |> List.iter (printfn "  UNVERIFIED: %s")
        if not failedChecks.IsEmpty || not undetected.IsEmpty || not diffs.IsEmpty then 1
        elif requirePass && verdict <> VerdictPass then 2
        else 0

[<EntryPoint>]
let main argv =
    let args = List.ofArray argv
    match args with
    | mode :: rest when mode = "generate" || mode = "check" ->
        match arg "--causa-data" rest with
        | None -> eprintfn "%s" usage; 64
        | Some data ->
            let indy = arg "--indy-root" rest |> Option.orElse (findIndyRoot (Directory.GetCurrentDirectory()))
            match indy with
            | None -> eprintfn "cannot find the Indy-init root (global.json + requirements-compiled); pass --indy-root"; 64
            | Some root -> run mode (Path.GetFullPath data) (Path.GetFullPath root) (List.contains "--require-pass" rest)
    | _ -> eprintfn "%s" usage; 64
