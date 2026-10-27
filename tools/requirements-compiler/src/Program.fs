/// requirements-compiler: compiles the Causa requirements baseline plus curated analysis into the
/// versioned, deterministic requirements-optimization package.
///
///   requirements-compiler --source DIR --baseline FILE --committed-baseline FILE
///                         --curation DIR --out DIR [--repo NAME=DIR]... [--check]
///
/// Exit codes: 0 compilation PASS; 3 compilation FAIL (a critical check failed);
///             2 unusable input; 4 --check found generated files that differ from the package.
module Program

open System
open System.IO
open Json
open Model
open Text


let private authoredFiles =
    [ "README.md"
      "optimization-analysis.md"
      "readiness/READINESS-REPORT.md"
      "readiness/OWNER-DECISION-REQUESTS.md"
      "readiness/CONTRACT-RESOLUTION.md"
      "readiness/FSHARP-TOOLING-ASSESSMENT.md"
      "readiness/DEPENDENCY-COMPATIBILITY.md"
      "verification-plan.schema.json" ]

type Options =
    { Source: string
      Baseline: string
      CommittedBaseline: string
      Curation: string
      Out: string
      Repos: Map<string, string>
      Check: bool
      Evidence: string }

let rec parseArgs (args: string list) (o: Options) =
    match args with
    | [] -> Ok o
    | "--source" :: v :: rest -> parseArgs rest { o with Source = v }
    | "--baseline" :: v :: rest -> parseArgs rest { o with Baseline = v }
    | "--committed-baseline" :: v :: rest -> parseArgs rest { o with CommittedBaseline = v }
    | "--curation" :: v :: rest -> parseArgs rest { o with Curation = v }
    | "--out" :: v :: rest -> parseArgs rest { o with Out = v }
    | "--repo" :: v :: rest ->
        match v.Split('=', 2) with
        | [| n; p |] -> parseArgs rest { o with Repos = o.Repos.Add(n, p) }
        | _ -> Error(sprintf "--repo expects NAME=DIR, got '%s'" v)
    | "--check" :: rest -> parseArgs rest { o with Check = true }
    | "--evidence" :: v :: rest -> parseArgs rest { o with Evidence = v }
    | a :: _ -> Error(sprintf "unknown argument '%s'" a)

let private fileSha (path: string) = File.ReadAllBytes path |> Git.sha256Hex

let private loadCuration (dir: string) =
    let load name = parseFile (Path.Combine(dir, name))

    { Sources = load "sources.json"
      Findings = arr "findings" (load "findings.json")
      Decisions = arr "decisions" (load "decisions.json")
      Contracts = arr "contracts" (load "contracts.json")
      Tasks = arr "tasks" (load "tasks.json") |> List.map taskOf
      Rules = arr "rules" (load "verification-rules.json") |> List.map ruleOf
      Experiment = load "experiment.json"
      External = arr "documents" (load "external-requirements.json")
      PreflightChecks = arr "checks" (load "preflight-checks.json")
      DefectTests = arr "tests" (load "defect-class-tests.json") }

let private curationFiles (dir: string) =
    Directory.GetFiles(dir, "*.json") |> Array.map Path.GetFileName |> Array.sort |> List.ofArray

type Built =
    { Files: (string * string) list
      Checks: Validate.Check list
      Report: Json
      Inputs: Validate.Inputs
      VerdictPass: bool }

/// Builds the whole package in memory: generated files, validation checks and report.
let buildPackage (o: Options) : Built =
    let curation = loadCuration o.Curation

    // The baseline is either supplied, or built in-process by the F# port at the recorded indy-init commit.
    let baselineBytes, baselineGenerator =
        if o.Baseline <> "" then
            File.ReadAllBytes o.Baseline, "supplied file (" + Path.GetFileName o.Baseline + ")"
        else
            let commit =
                arr "repositories" curation.Sources
                |> List.find (fun r -> str "name" r = "indy-init")
                |> str "commit"

            let read path =
                match Git.showFile o.Source commit path with
                | Ok b -> utf8 b
                | Error e -> failwithf "cannot read %s at %s: %s" path commit e

            Baseline.build read commit |> serializePythonStyle |> Text.Encoding.UTF8.GetBytes,
            "requirements-compiler baseline (F# port; byte-equivalent to tools/build-requirements-baseline.py)"

    let baseline = parseBytes baselineBytes
    let baselineSha = Git.sha256Hex baselineBytes
    let source = field "source" baseline |> Option.defaultValue JNull
    let sourceCommit = str "commit" source
    let repos = o.Repos.Add("indy-init", o.Source)

    let expected =
        arr "repositories" curation.Sources |> List.map (fun r -> str "name" r, str "commit" r) |> Map.ofList

    let repoOf name =
        { Name = name
          Path = repos.TryFind name |> Option.defaultWith (fun () -> failwithf "no checkout supplied for repository '%s'" name)
          Commit = expected.TryFind name |> Option.defaultWith (fun () -> failwithf "repository '%s' is not recorded in sources.json" name) }

    let repoChecks =
        expected
        |> Map.toList
        |> List.map (fun (n, commit) ->
            let present = repos.TryFind n |> Option.map (fun p -> Git.hasCommit p commit) |> Option.defaultValue false
            n, commit, (if present then commit else "absent"))
        |> fun xs ->
            if expected.TryFind "indy-init" <> Some sourceCommit then
                xs @ [ "indy-init (baseline source.commit)", (expected.TryFind "indy-init" |> Option.defaultValue ""), sourceCommit ]
            else xs

    let readAt (repo: Repo) (path: string) =
        match Git.showFile repo.Path repo.Commit path with
        | Ok b -> Some b
        | Error _ -> None

    // Source documents of the baseline, hashed at the source commit.
    let indy = repoOf "indy-init"

    let baselineDocs = arr "documents" source

    let sourceTexts =
        baselineDocs
        |> List.choose (fun d -> readAt indy (str "path" d) |> Option.map (fun b -> str "path" d, normalizeNewlines (utf8 b)))
        |> Map.ofList

    let sourceDocumentHashes =
        baselineDocs
        |> List.map (fun d ->
            let p = str "path" d
            let observed = sourceTexts.TryFind p |> Option.map sha256OfText |> Option.defaultValue "missing"
            p, str "sha256" d, observed)

    // External requirement documents, sectioned at their recorded commit.
    let external =
        curation.External
        |> List.collect (fun spec ->
            let repo = repoOf (str "repo" spec)
            let path = str "path" spec

            match readAt repo path with
            | None -> failwithf "external document %s:%s is unreadable at %s" repo.Name path repo.Commit
            | Some bytes ->
                let text = utf8 bytes
                let sha = sha256OfText text
                if sha <> str "sha256" spec then failwithf "external document %s:%s hashes to %s, expected %s" repo.Name path sha (str "sha256" spec)
                Inventory.sectionDocument (str "key" spec) (sprintf "kemiller2002/%s" repo.Name) repo.Commit path text)

    let requirements = Inventory.fromBaseline baseline @ external |> List.sortBy (fun r -> r.Id)
    let known = requirements |> List.map (fun r -> r.Id) |> Set.ofList

    let categoryRules = arr "categories" curation.Sources |> List.map (fun c -> str "match" c, str "category" c)
    let categories = requirements |> List.map (fun r -> r.Id, Inventory.categoryOf categoryRules r) |> Map.ofList
    let tags = requirements |> List.map (fun r -> r.Id, Inventory.tagsOf r) |> Map.ofList
    let references = requirements |> List.map (fun r -> r.Id, Inventory.referencesOf known r) |> Map.ofList
    let flags = requirements |> List.map (fun r -> r.Id, Normalize.flagsOf requirements categories.[r.Id] tags.[r.Id] r) |> Map.ofList

    let containments = Normalize.containments requirements
    let containedPairs = containments |> List.map (fun g -> g.Requirements.[0], g.Requirements.[1]) |> Set.ofList
    let documentOf = requirements |> List.map (fun r -> r.Id, r.Document) |> Map.ofList

    // An exact duplicate inside one document that is explained by structural containment (a section that
    // spans its sub-sections) is reported once, as the containment, not again as a duplicate group.
    let explained (g: Normalize.OverlapGroup) =
        let sameDocument = (g.Requirements |> List.map (fun r -> documentOf.[r]) |> List.distinct).Length = 1
        let containers = g.Requirements |> List.filter (fun a -> g.Requirements |> List.exists (fun b -> containedPairs.Contains(a, b)))
        sameDocument && (g.Requirements |> List.except containers |> List.length) <= 1 && not containers.IsEmpty

    let duplicates =
        Normalize.exactDuplicates requirements
        |> List.filter (explained >> not)
        |> List.mapi (fun i g -> { g with Id = sprintf "DUP-%04d" (i + 1) })

    let nearDuplicates = Normalize.nearDuplicates (decimal (str "nearDuplicateThreshold" curation.Sources) |> float) requirements

    let findingsByRequirement =
        curation.Findings
        |> List.collect (fun f -> strs "requirements" f |> List.map (fun r -> r, f))
        |> List.groupBy fst
        |> List.map (fun (r, xs) -> r, xs |> List.map snd |> List.sortBy (str "id"))
        |> Map.ofList

    let coverage =
        requirements
        |> List.choose (fun r ->
            match curation.Tasks |> List.filter (fun t -> t.Covers |> List.exists (fun s -> matches s r)) |> List.map (fun t -> t.Id) with
            | [] -> None
            | ts -> Some(r.Id, List.sort ts))
        |> Map.ofList

    let rulesFor =
        requirements
        |> List.map (fun r -> r.Id, curation.Rules |> List.filter (fun x -> x.Match |> List.exists (fun s -> matches s r)))
        |> Map.ofList

    // Citations are verified verbatim against the file at the repository's recorded commit.
    let allCitations =
        (curation.Findings |> List.collect (fun f -> citationsOf "citations" f @ citationsOf "resolutionSources" f))
        @ (curation.Decisions |> List.collect (citationsOf "sources"))
        @ (curation.Contracts |> List.collect (citationsOf "citations"))
        @ (curation.DefectTests |> List.collect (citationsOf "citations"))
        |> List.distinct

    let fileCache =
        allCitations
        |> List.map (fun c -> c.Repo, c.Path)
        |> List.distinct
        |> List.map (fun (rn, p) -> (rn, p), (if expected.ContainsKey rn then readAt (repoOf rn) p |> Option.map utf8 else None))
        |> Map.ofList

    let citationResults =
        allCitations
        |> List.map (fun c ->
            c,
            match fileCache.[(c.Repo, c.Path)] with
            | None -> { Citation = c; Verified = false; Detail = "file not readable at the recorded commit" }
            | Some text when containsQuote text c.Quote -> { Citation = c; Verified = true; Detail = sprintf "found at %s" expected.[c.Repo] }
            | Some _ -> { Citation = c; Verified = false; Detail = "quote not found" })
        |> Map.ofList

    let identityResults =
        curation.Contracts
        |> List.map (fun k ->
            str "id" k,
            arr "identity" k
            |> List.map (fun x ->
                let repo = repoOf (str "repo" x)
                let rev = str "rev" x
                let actual =
                    match Git.showFile repo.Path rev (str "path" x) with
                    | Ok b -> Git.sha256Hex b
                    | Error _ -> "unreadable"

                JObj
                    [ "label", JStr(str "label" x)
                      "repo", JStr repo.Name
                      "rev", JStr rev
                      "path", JStr(str "path" x)
                      "expectedSha256", JStr(str "expectedSha256" x)
                      "actualSha256", JStr actual
                      "match", JBool(actual = str "expectedSha256" x) ]))
        |> Map.ofList

    let metricIds =
        match readAt (repoOf "percepta") "telemetry/metrics.json" with
        | Some b -> arr "metrics" (parseBytes b) |> List.map (str "id") |> Set.ofList
        | None -> Set.empty

    let documents =
        (baselineDocs
         |> List.map (fun d ->
             let p = str "path" d
             JObj
                 [ "path", JStr p
                   "repository", JStr(str "repository" source)
                   "commit", JStr sourceCommit
                   "sha256", JStr(str "sha256" d)
                   "origin", JStr "baseline"
                   "requirements", jint (requirements |> List.filter (fun r -> r.Document = p) |> List.length) ]))
        @ (curation.External
           |> List.map (fun spec ->
               let p = str "path" spec
               JObj
                   [ "path", JStr p
                     "repository", JStr(sprintf "kemiller2002/%s" (str "repo" spec))
                     "commit", JStr expected.[str "repo" spec]
                     "sha256", JStr(str "sha256" spec)
                     "origin", JStr "external"
                     "requirements", jint (requirements |> List.filter (fun r -> r.Document = p && r.Origin = "external") |> List.length) ]))

    let compiled: Artifacts.Compiled =
        { SourceRepo = str "repository" source
          SourceCommit = sourceCommit
          BaselineId = str "id" baseline
          BaselineSha256 = baselineSha
          Documents = documents
          Requirements = requirements
          Categories = categories
          Tags = tags
          References = references
          Flags = flags
          Duplicates = duplicates
          Containments = containments
          NearDuplicates = nearDuplicates
          FindingsByRequirement = findingsByRequirement
          Coverage = coverage
          RulesFor = rulesFor
          Order = Graph.topoOrder curation.Tasks
          CitationResults = citationResults
          IdentityResults = identityResults
          MetricIds = metricIds
          Curation = curation }

    let planSpec key =
        arr "plans" curation.Sources |> List.find (fun p -> str "key" p = key)

    let implementationLanes = set [ "domain"; "ui"; "persistence"; "integration"; "coordinator" ]

    let planA: Artifacts.PlanSpec =
        { Key = "A-time-optimized"
          Name = "Plan A: time optimized"
          Objective = "Minimize elapsed steps: one lane per owner role, critical-path-first list scheduling, integration checkpoints as early as their prerequisites allow"
          LaneOf = fun t -> t.Owner
          Unbounded = set [ "operator"; "upstream" ]
          CiTriggerKinds = set [ "implementation"; "integration-checkpoint"; "verification" ]
          Spec = planSpec "A-time-optimized" }

    let planB: Artifacts.PlanSpec =
        { Key = "B-resource-optimized"
          Name = "Plan B: resource optimized"
          Objective = "Minimize agents, handoffs, repeated reasoning and CI runs: one continuous lead agent, one fresh reviewer at release candidates, CI batched at checkpoints"
          LaneOf = fun t -> if implementationLanes.Contains t.Owner then "lead" else t.Owner
          Unbounded = set [ "operator"; "upstream" ]
          CiTriggerKinds = set [ "integration-checkpoint"; "verification" ]
          Spec = planSpec "B-resource-optimized" }

    let outputs0 =
        [ "requirements-inventory.json", Artifacts.inventory compiled
          "normalized-requirements.json", Artifacts.normalized compiled
          "requirements-source-map.json", Artifacts.sourceMap compiled
          "decision-registry.json", Artifacts.decisionRegistry compiled
          "contract-bindings.json", Artifacts.contractBindings compiled
          "dependency-graph.json", Artifacts.dependencyGraph compiled
          "verification-manifest.json", Artifacts.verificationManifest compiled
          "execution-plan-time.json", Artifacts.executionPlan compiled planA
          "execution-plan-resource.json", Artifacts.executionPlan compiled planB
          "percepta-experiment.json", Artifacts.perceptaExperiment compiled
          "preflight-manifest.json", Artifacts.preflightManifest compiled
          "timed-execution-manifest.json", Artifacts.timedExecutionManifest compiled planB ]

    // Phase Two: the verification plan, derived from the baseline, the verification manifest and the
    // plan curation; the approvals file that authorizes exclusions is read at its pinned commit.
    let planSpec = parseFile (Path.Combine(o.Curation, "verification-plan.json"))
    let planInputs: VerificationPlan.Inputs =
        let a = field "approvals" planSpec |> Option.defaultValue JNull
        let repo = repoOf (str "repo" a)
        let bytes =
            match Git.showFile repo.Path (str "commit" a) (str "path" a) with
            | Ok b -> b
            | Error e -> failwithf "approvals file %s is unreadable at %s: %s" (str "path" a) (str "commit" a) e

        { Repository = str "repository" source
          Commit = sourceCommit
          BaselineId = str "id" baseline
          BaselineSha256 = baselineSha
          Baseline = baseline
          Manifest = Artifacts.verificationManifest compiled
          Spec = planSpec
          Approvals =
            { Repository = sprintf "kemiller2002/%s" repo.Name
              Commit = str "commit" a
              Path = str "path" a
              Sha256 = Git.sha256Hex bytes
              TrustedOperators = strs "trustedOperators" a
              Document = parseBytes bytes } }

    let outputs0 = outputs0 @ [ "verification-plan.json", VerificationPlan.build planInputs ]

    // Validation reads the serialized artifacts back, so it checks what is actually written.
    let reparsed = outputs0 |> List.map (fun (n, j) -> n, serialize j |> Text.Encoding.UTF8.GetBytes |> parseBytes) |> Map.ofList

    let sourceFiles =
        match Git.listFiles indy.Path indy.Commit with
        | Ok fs -> fs
        | Error e -> failwithf "cannot list the source commit: %s" e

    let inputs: Validate.Inputs =
        { Compiled = compiled
          Baseline = baseline
          Outputs = reparsed
          RepoChecks = repoChecks
          SourceDocumentHashes = sourceDocumentHashes
          SourceTexts = sourceTexts
          SourceFiles = sourceFiles
          CommittedBaseline = if File.Exists o.CommittedBaseline then Some(parseFile o.CommittedBaseline) else None }

    let planCheck: Validate.Check =
        { Id = "V18"
          Title = "The verification plan preserves every baseline P0 requirement and obligation; exclusions are operator-authorized and test bindings resolve"
          Critical = true
          Problems = VerificationPlan.problems planInputs @ VerificationPlan.preservationProblems baseline reparsed.["verification-plan.json"] }

    let checks = Validate.run inputs @ [ planCheck ]
    let report = Validate.report inputs checks
    let outputs = outputs0 @ [ "requirements-validation-report.json", report ]
    let rendered = outputs |> List.map (fun (n, j) -> n, serialize j)

    let authored =
        authoredFiles
        |> List.filter (fun f -> File.Exists(Path.Combine(o.Out, f)))
        |> List.map (fun f -> f, fileSha (Path.Combine(o.Out, f)))

    let sdk =
        let g = Path.Combine(o.Source, "global.json")
        if File.Exists g then field "sdk" (parseFile g) |> Option.map (str "version") |> Option.defaultValue "unpinned"
        else "unpinned"

    let manifest =
        JObj
            [ "schema", JStr "indy-init.requirements-package/1"
              "package", JStr "requirements-compiled/v1"
              "source",
              JObj
                  [ "repository", JStr(str "repository" source)
                    "commit", JStr sourceCommit
                    "baselineId", JStr(str "id" baseline)
                    "baselineSha256", JStr baselineSha
                    "baselineGenerator", JStr baselineGenerator ]
              "repositories",
              arr "repositories" curation.Sources
              |> List.map (fun r -> JObj [ "name", JStr(str "name" r); "commit", JStr(str "commit" r); "role", JStr(str "role" r) ])
              |> JArr
              "dependencyVersions", JArr(arr "dependencyVersions" curation.Sources)
              "tool", JObj [ "name", JStr "tools/requirements-compiler"; "language", JStr "F#"; "dotnetSdk", JStr sdk ]
              "inputs",
              curationFiles o.Curation |> List.map (fun f -> JObj [ "path", JStr("curation/" + f); "sha256", JStr(fileSha (Path.Combine(o.Curation, f))) ]) |> JArr
              "authored", authored |> List.map (fun (f, h) -> JObj [ "path", JStr f; "sha256", JStr h ]) |> JArr
              "generated",
              rendered
              |> List.sortBy fst
              |> List.map (fun (n, text) -> JObj [ "path", JStr n; "sha256", JStr(Text.Encoding.UTF8.GetBytes text |> Git.sha256Hex) ])
              |> JArr
              "compilationVerdict", field "compilationVerdict" report |> Option.defaultValue JNull
              "rehearsalReadiness", field "rehearsalReadiness" report |> Option.bind (field "verdict") |> Option.defaultValue JNull ]

    { Files = rendered @ [ "package-manifest.json", serialize manifest ]
      Checks = checks
      Report = report
      Inputs = inputs
      VerdictPass = str "compilationVerdict" report = "PASS" }

let private compileOrThrow (o: Options) : int =
    let built = buildPackage o
    let allFiles = built.Files
    let checks = built.Checks
    let report = built.Report
    let verdictPass = built.VerdictPass

    if o.Check then
        let differing =
            allFiles
            |> List.filter (fun (n, text) ->
                let p = Path.Combine(o.Out, n)
                not (File.Exists p) || File.ReadAllText p <> text)
            |> List.map fst

        differing |> List.iter (eprintfn "differs: %s")
        if not differing.IsEmpty then 4 elif verdictPass then 0 else 3
    else
        Directory.CreateDirectory o.Out |> ignore
        allFiles |> List.iter (fun (n, text) -> File.WriteAllText(Path.Combine(o.Out, n), text))

        checks
        |> List.iter (fun x ->
            let st = if x.Problems.IsEmpty then "PASS" elif x.Critical then "FAIL" else "WARN"
            printfn "%s %s  %s" st x.Id x.Title
            x.Problems |> List.truncate 10 |> List.iter (printfn "      - %s"))

        printfn "compilation: %s; rehearsal readiness: %s" (str "compilationVerdict" report) (field "rehearsalReadiness" report |> Option.map (str "verdict") |> Option.defaultValue "?")
        if verdictPass then 0 else 3

/// Compiles the package; an unusable input (unreadable file, unknown repository, a commit absent from a
/// checkout, a hash mismatch on an external document) is refused with exit 2.
let compile (o: Options) : int =
    try
        compileOrThrow o
    with ex ->
        eprintfn "input error: %s" ex.Message
        2

let defaults =
    { Source = "."
      Baseline = ""
      CommittedBaseline = "requirements/causa-requirements-baseline.json"
      Curation = "requirements-compiled/v1/curation"
      Out = "requirements-compiled/v1"
      Repos = Map.empty
      Check = false
      Evidence = "" }

/// `baseline ROOT COMMIT`: same contract as tools/build-requirements-baseline.py (reads the working
/// tree under ROOT, writes the baseline to stdout; exit 2 on usage, 1 on a rule violation).
let baselineCommand (root: string) (commit: string) : int =
    if not (Baseline.isCommit commit) then
        eprintfn "usage: requirements-compiler baseline ROOT COMMIT(40 hex)"
        2
    else
        try
            let read (path: string) = File.ReadAllText(Path.Combine(root, path), Text.Encoding.UTF8)
            let out = Console.OpenStandardOutput()
            let bytes = Baseline.build read commit |> serializePythonStyle |> Text.Encoding.UTF8.GetBytes
            out.Write(bytes, 0, bytes.Length)
            out.Flush()
            0
        with
        | Baseline.BaselineError msg ->
            eprintfn "%s" msg
            1
        | :? IOException as ex ->
            eprintfn "%s" ex.Message
            1

/// `preflight <compile options> --evidence FILE`: builds the package in memory, executes every preflight
/// check and writes the evidence. Exit 0 READY, 3 BLOCKED or FAIL, 2 unusable input.
let preflightCommand (o: Options) : int =
    try
        let built = buildPackage o
        let ctx: Preflight.Context =
            { Compiled = built.Inputs.Compiled
              Report = built.Report
              Source = o.Source
              SourceCommit = built.Inputs.Compiled.SourceCommit
              Repos = o.Repos.Add("indy-init", o.Source) }

        let results =
            built.Inputs.Compiled.Curation.PreflightChecks
            |> List.sortBy (str "id")
            |> List.map (fun k -> k, Preflight.runCheck ctx k)

        let evidence = Preflight.evidence ctx DateTime.UtcNow results
        results |> List.iter (fun (k, r) -> printfn "%-12s %-12s %s" (str "id" k) r.Status r.Detail)
        let v = str "verdict" evidence
        printfn "preflight: %s" v

        if o.Evidence <> "" then
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath o.Evidence)) |> ignore
            File.WriteAllText(o.Evidence, serialize evidence)

        if v = "READY" then 0 else 3
    with ex ->
        eprintfn "input error: %s" ex.Message
        2

/// `verify-plan`: Phase Eight. Assesses a candidate's ledger and evidence bundle against the verification
/// plan (see PlanVerification). Writes the assessment to --out (or stdout).
/// Exit 0 ACCEPTED, 3 not accepted, 2 unusable input.
let verifyPlanCommand (args: string list) : int =
    let rec opts (xs: string list) (m: Map<string, string list>) =
        match xs with
        | k :: v :: rest when k.StartsWith("--", StringComparison.Ordinal) ->
            opts rest (m.Add(k, (m.TryFind k |> Option.defaultValue []) @ [ v ]))
        | [] -> Ok m
        | x :: _ -> Error(sprintf "unexpected argument '%s'" x)

    let required =
        [ "--plan"; "--baseline"; "--ledger"; "--evidence-manifest"; "--evidence-manifest-sha256"; "--evidence-root"; "--trusted-source-sha"; "--approvals"; "--trusted-operator-id" ]

    match opts args Map.empty with
    | Error e ->
        eprintfn "%s" e
        2
    | Ok m when required |> List.exists (m.ContainsKey >> not) ->
        eprintfn "usage: requirements-compiler verify-plan %s [--out FILE]" (required |> List.map (fun k -> k + " V") |> String.concat " ")
        2
    | Ok m ->
        try
            let one k = m.[k] |> List.last
            let root = Path.GetFullPath(one "--evidence-root")

            let readEvidence (rel: string) =
                let full = Path.GetFullPath(Path.Combine(root, rel))
                if rel.Contains("..") || Path.IsPathRooted rel || not (full.StartsWith(root + string Path.DirectorySeparatorChar, StringComparison.Ordinal)) then None
                elif File.Exists full then Some(File.ReadAllBytes full)
                else None

            let baselineBytes = File.ReadAllBytes(one "--baseline")

            let assessment =
                PlanVerification.assess
                    { Plan = parseFile (one "--plan")
                      Baseline = parseBytes baselineBytes
                      BaselineSha256 = Git.sha256Hex baselineBytes
                      Ledger = parseFile (one "--ledger")
                      ManifestRaw = File.ReadAllBytes(one "--evidence-manifest")
                      ExpectedManifestSha256 = one "--evidence-manifest-sha256"
                      ReadEvidence = readEvidence
                      TrustedSourceSha = one "--trusted-source-sha"
                      ApprovalsRaw = File.ReadAllBytes(one "--approvals")
                      TrustedOperators = m.["--trusted-operator-id"] }

            let text = serialize assessment
            match m.TryFind "--out" with
            | Some [ out ] -> File.WriteAllText(out, text)
            | _ -> printf "%s" text
            if str "verdict" assessment = "ACCEPTED" then 0 else 3
        with ex ->
            eprintfn "input error: %s" ex.Message
            2

[<EntryPoint>]
let main argv =
    match List.ofArray argv with
    | [ "baseline"; root; commit ] -> baselineCommand root commit
    | "verify-plan" :: rest -> verifyPlanCommand rest
    | "preflight" :: rest ->
        match parseArgs rest defaults with
        | Error e ->
            eprintfn "%s" e
            2
        | Ok o -> preflightCommand o
    | "baseline" :: _ ->
        eprintfn "usage: requirements-compiler baseline ROOT COMMIT(40 hex)"
        2
    | args ->
        match parseArgs args defaults with
        | Error e ->
            eprintfn "%s" e
            2
        | Ok o -> compile o
