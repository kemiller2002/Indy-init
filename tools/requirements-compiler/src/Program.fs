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
              Document = parseBytes bytes }
          Categories =
            let path = Path.Combine(o.Curation, "evidence-categories.json")
            if not (File.Exists path) then failwithf "the evidence-categories table %s is missing" path
            { Path = "curation/evidence-categories.json"; Sha256 = fileSha path; Table = parseFile path } }

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
          Title = "The verification plan preserves every baseline P0 requirement and obligation; exclusions are operator-authorized; test bindings resolve and declare valid evidence categories; every obligation carries its derived acceptance contract"
          Critical = true
          Problems =
            VerificationPlan.problems planInputs
            @ VerificationPlan.preservationProblems baseline reparsed.["verification-plan.json"]
            @ VerificationPlan.categoriesTableProblems planInputs reparsed.["verification-plan.json"] }

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
              // The package version is the directory that holds the curation (requirements-compiled/<version>/curation).
              "package", JStr("requirements-compiled/" + Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath o.Curation))))
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
      Curation = "requirements-compiled/v2/curation"
      Out = "requirements-compiled/v2"
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

/// The process verifier's inputs from command-line options named by `key` ("repo", "events", "executions",
/// "ancestry", "rules", "rules-sha256", "plan-digest", "attestations", "trusted-run-id"). The plan digest and the
/// rules hash are the values frozen at kickoff; the verifier recomputes both and refuses a mismatch.
let private processInputs (get: string -> string option) (plan: Json) (candidate: string) : ProcessVerification.Inputs =
    let need k = get k |> Option.defaultWith (fun () -> failwithf "missing %s" k)
    let repo = get "repo" |> Option.map Path.GetFullPath
    let eventsPath =
        match get "events", repo with
        | Some p, _ -> p
        | None, Some r -> Path.Combine(r, ".ros", "events", "events.jsonl")
        | None, None -> ""
    // A missing log is unusable input, not an empty run.
    let events =
        if File.Exists eventsPath then
            File.ReadAllLines eventsPath |> Array.filter (fun l -> l.Trim() <> "") |> Array.map (Text.Encoding.UTF8.GetBytes >> parseBytes) |> List.ofArray
        else failwithf "the Praxis event log %s does not exist" eventsPath
    // Envelopes as Praxis stores them (.ros/executions/<id>/envelope.json) or exported flat (<id>.json).
    let executionsDir =
        match get "executions", repo with
        | Some d, _ -> d
        | None, Some r -> Path.Combine(r, ".ros", "executions")
        | None, None -> ""
    let executions =
        if Directory.Exists executionsDir then
            let nested =
                Directory.GetDirectories executionsDir
                |> Array.choose (fun dir ->
                    let envelope = Path.Combine(dir, "envelope.json")
                    if File.Exists envelope then Some(Path.GetFileName dir, parseFile envelope) else None)
            let flat = Directory.GetFiles(executionsDir, "*.json") |> Array.map (fun f -> Path.GetFileNameWithoutExtension f, parseFile f)
            Array.append nested flat |> Map.ofArray
        else Map.empty
    let rulesBytes = File.ReadAllBytes(need "rules")
    // Ancestry from the candidate repository, or from an exported `git rev-list CANDIDATE` listing.
    let ancestry = get "ancestry" |> Option.map (File.ReadAllLines >> Array.map (fun l -> l.Trim()) >> Set.ofArray)
    // Only full commit ids are compared: a revision expression such as HEAD~1 is not an observed commit.
    let isCommitId (x: string) = x.Length = 40 && x |> Seq.forall (fun ch -> (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f'))
    let isAncestor (commit: string) (c: string) =
        isCommitId commit && isCommitId c
        && (commit = c
        || (match ancestry, repo with
            | Some listed, _ -> c = candidate && listed.Contains commit
            | None, Some r -> Git.run r [ "merge-base"; "--is-ancestor"; commit; c ] |> Result.isOk
            | None, None -> false))
    { Plan = plan
      ExpectedPlanDigest = need "plan-digest"
      Events = events
      Executions = executions
      Rules = parseBytes rulesBytes
      RulesSha256 = Git.sha256Hex rulesBytes
      ExpectedRulesSha256 = need "rules-sha256"
      Attestations = get "attestations" |> Option.map parseFile
      TrustedRunId = get "trusted-run-id"
      Candidate = candidate
      IsAncestor = isAncestor }

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
        eprintfn "usage: requirements-compiler verify-plan %s [--process-plan-digest HEX --process-rules FILE --process-rules-sha256 HEX (--process-repo DIR | --process-events FILE [--process-executions DIR] --process-ancestry FILE) [--process-attestations FILE --process-trusted-run-id ID]] [--review-tag-changes FILE] [--out FILE]" (required |> List.map (fun k -> k + " V") |> String.concat " ")
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
                      TrustedOperators = m.["--trusted-operator-id"]
                      // Process, owner and Praxis-review categories are credited only from an assessment computed here,
                      // for this plan and this candidate, never from a file handed in.
                      ProcessCovered =
                        if m.ContainsKey "--process-rules" then
                            processInputs (fun k -> m.TryFind("--process-" + k) |> Option.map List.last) (parseFile (one "--plan")) (one "--trusted-source-sha")
                            |> ProcessVerification.assess
                            |> ProcessVerification.coveredBy
                        else Map.empty
                      ReviewTagChanges = m.TryFind "--review-tag-changes" |> Option.map (List.last >> parseFile) |> Option.defaultValue JNull }

            let text = serialize assessment
            match m.TryFind "--out" with
            | Some [ out ] -> File.WriteAllText(out, text)
            | _ -> printf "%s" text
            if str "verdict" assessment = "ACCEPTED" then 0 else 3
        with ex ->
            eprintfn "input error: %s" ex.Message
            2

let private optionMap (args: string list) =
    let rec go (xs: string list) (m: Map<string, string list>) =
        match xs with
        | k :: v :: rest when k.StartsWith("--", StringComparison.Ordinal) -> go rest (m.Add(k, (m.TryFind k |> Option.defaultValue []) @ [ v ]))
        | [] -> Ok m
        | x :: _ -> Error(sprintf "unexpected argument '%s'" x)
    go args Map.empty

let private writeOut (m: Map<string, string list>) (j: Json) =
    let text = serialize j
    match m.TryFind "--out" with
    | Some [ out ] -> File.WriteAllText(out, text)
    | _ -> printf "%s" text

/// `verify-process`: Priority One. Decides the process, reviewer and owner obligations of the plan for one candidate
/// from the rehearsal repository's Praxis event log (see ProcessVerification). The candidate repository is read only
/// for commit ancestry. Exit 0 VERIFIED, 3 not verified, 2 unusable input.
let verifyProcessCommand (args: string list) : int =
    let required = [ "--plan"; "--plan-digest"; "--rules"; "--rules-sha256"; "--candidate" ]
    let source (m: Map<string, string list>) = m.ContainsKey "--repo" || (m.ContainsKey "--events" && m.ContainsKey "--ancestry")
    match optionMap args with
    | Error e ->
        eprintfn "%s" e
        2
    | Ok m when required |> List.exists (m.ContainsKey >> not) || not (source m) ->
        eprintfn "usage: requirements-compiler verify-process --plan FILE --plan-digest HEX --rules FILE --rules-sha256 HEX --candidate SHA (--repo DIR | --events FILE [--executions DIR] --ancestry FILE) [--attestations FILE --trusted-run-id ID] [--out FILE]"
        2
    | Ok m ->
        try
            let one k = m.[k] |> List.last
            let assessment = ProcessVerification.assess (processInputs (fun k -> m.TryFind("--" + k) |> Option.map List.last) (parseFile (one "--plan")) (one "--candidate"))
            writeOut m assessment
            match str "verdict" assessment with
            | "VERIFIED" -> 0
            | "INVALID" -> 2
            | _ -> 3
        with ex ->
            eprintfn "input error: %s" ex.Message
            2

/// `verify-defect-closures`: defect-class closures checked against the rehearsal's own history (see DefectClosure,
/// R7-REM-008). Closures are read from the candidate's tree unless `--closures-file` is given. The base is bound by
/// kickoff/indy-init.kickoff.json in the candidate's history (see Kickoff); a different `--base` is REFUSED. Exit 0 VERIFIED,
/// 3 REFUSED or INCOMPLETE, 2 unusable input.
let verifyDefectClosuresCommand (args: string list) : int =
    let usage =
        "usage: requirements-compiler verify-defect-closures --repo DIR --candidate SHA [--base SHA] [--closures PATH | --closures-file FILE] [--records SHA] [--curation FILE] [--tests-path DIR] [--freeze-at ISO-8601] [--out FILE]"

    match optionMap args with
    | Error e ->
        eprintfn "%s" e
        2
    | Ok m when not (m.ContainsKey "--repo" && m.ContainsKey "--candidate") ->
        eprintfn "%s" usage
        2
    | Ok m ->
        try
            let one k = m.TryFind k |> Option.map List.last
            let repo = (one "--repo").Value
            let candidate = DefectClosure.resolveCommit repo (one "--candidate").Value |> Option.defaultValue (one "--candidate").Value
            let path = one "--closures" |> Option.defaultValue "acceptance/defect-classes.json"

            let source, bytes =
                match one "--closures-file" with
                | Some file -> sprintf "file:%s" file, File.ReadAllBytes file
                | None ->
                    match Git.showFile repo candidate path with
                    | Ok bytes -> sprintf "%s:%s" candidate path, bytes
                    | Error e -> failwithf "%s is not readable at %s: %s" path candidate e

            let freezeAt =
                one "--freeze-at"
                |> Option.map (fun text -> DateTimeOffset.Parse(text, Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.AssumeUniversal))

            let report =
                DefectClosure.assess
                    { Repo = repo
                      Base = one "--base"
                      Candidate = candidate
                      Records = one "--records"
                      Closures = parseBytes bytes
                      ClosuresSource = source
                      ClosuresSha256 = Git.sha256Hex bytes
                      ClosuresPath = path
                      Curation = one "--curation" |> Option.map parseFile
                      FreezeAt = freezeAt
                      TestsPath = one "--tests-path" |> Option.defaultValue "tests" }

            writeOut m report
            if str "verdict" report = "VERIFIED" then 0 else 3
        with ex ->
            eprintfn "input error: %s" ex.Message
            2

/// `release-comparison`: the pre-lock newest-release record (see ReleaseComparison, R7-REM-010).
///   observe --selected SET --out FILE        queries every selected system's release source now, keeping each answer
///   assess  --selected SET --observations FILE (--lock-record FILE | --locked-at ISO-with-offset) [--deviations FILE]
///           [--max-age-hours N (<= 24)] [--out FILE]
/// Exit 0 PASS (or observed), 3 FAIL, 2 unusable input.
let releaseComparisonCommand (args: string list) : int =
    let usage =
        "usage: requirements-compiler release-comparison (observe --selected FILE --out FILE | assess --selected FILE --observations FILE (--lock-record FILE | --locked-at ISO-8601-WITH-OFFSET) [--deviations FILE] [--max-age-hours N<=24] [--out FILE])"

    let orFail =
        function
        | Ok x -> x
        | Error(e: string) -> failwith e

    let run (sub: string) (rest: string list) =
        match optionMap rest with
        | Error e ->
            eprintfn "%s" e
            2
        | Ok m ->
            let one k = m.TryFind k |> Option.map List.last

            try
                match sub, one "--selected" with
                | "observe", Some selected when (one "--out").IsSome ->
                    use http = new Net.Http.HttpClient(Timeout = TimeSpan.FromSeconds 30.0)

                    let observations =
                        ReleaseComparison.selectedSystems (parseFile selected)
                        |> List.map (ReleaseComparison.observe http (fun () -> DateTimeOffset.UtcNow))

                    writeOut m (JObj [ "schema", JStr "indy-init.release-observations/2"; "observations", JArr(observations |> List.map ReleaseComparison.observationToJson) ])
                    0
                | "assess", Some selected when (one "--observations").IsSome && ((one "--locked-at").IsSome || (one "--lock-record").IsSome) ->
                    let observations = parseFile (one "--observations").Value |> arr "observations" |> List.map ReleaseComparison.observationOfJson
                    let deviationsBytes = one "--deviations" |> Option.map File.ReadAllBytes
                    let deviations = deviationsBytes |> Option.map (parseBytes >> arr "deviations") |> Option.defaultValue []

                    // A lock record (the lock's own `lockedAt`) is preferred to a time typed on the command line.
                    let lockedAtText =
                        match one "--lock-record" with
                        | Some file -> parseFile file |> strOpt "lockedAt" |> Option.defaultWith (fun () -> failwithf "%s has no lockedAt" file)
                        | None -> (one "--locked-at").Value

                    let lockedAt = ReleaseComparison.parseLockedAt lockedAtText |> orFail
                    let maxAge = one "--max-age-hours" |> Option.map (fun h -> Double.Parse(h, Globalization.CultureInfo.InvariantCulture)) |> Option.defaultValue 6.0 |> ReleaseComparison.maxAgeOf |> orFail
                    let report = ReleaseComparison.assess (parseFile selected) observations deviations (deviationsBytes |> Option.map Git.sha256Hex) lockedAt maxAge
                    writeOut m report
                    if str "verdict" report = "PASS" then 0 else 3
                | _ ->
                    eprintfn "%s" usage
                    2
            with ex ->
                eprintfn "input error: %s" ex.Message
                2

    match args with
    | sub :: rest -> run sub rest
    | [] ->
        eprintfn "%s" usage
        2

/// `assertion-ratchet`: no assertion a bound test held since the base disappears from the candidate without a
/// resolved tag change (see AssertionRatchet, R7-REM-011). The base is derived from kickoff/indy-init.kickoff.json in
/// the candidate's history (`--kickoff-path` may only name that path; any other is REFUSED); a `--base` is checked
/// against it. Review tag changes are read from acceptance/reviews/final/review-tag-changes.json at `--records` (or
/// the candidate); `--tag-changes FILE` from disk leaves the verdict at best INCOMPLETE. Exit 0 PASS, 3 FAIL, REFUSED
/// or INCOMPLETE, 2 unusable input.
let assertionRatchetCommand (args: string list) : int =
    match optionMap args with
    | Error e ->
        eprintfn "%s" e
        2
    | Ok m when not (m.ContainsKey "--repo" && m.ContainsKey "--candidate") ->
        eprintfn "usage: requirements-compiler assertion-ratchet --repo DIR --candidate SHA [--base SHA] [--records SHA] [--path DIR]... [--plan FILE] [--tag-changes FILE (INCOMPLETE at best)] [--out FILE]"
        2
    | Ok m ->
        try
            let one k = m.[k] |> List.last
            let last k = m.TryFind k |> Option.map List.last

            let report =
                AssertionRatchet.assess
                    { Repo = one "--repo"
                      Base = last "--base"
                      Candidate = one "--candidate"
                      Roots = m.TryFind "--path" |> Option.defaultValue [ "tests" ]
                      TagChangesFile = last "--tag-changes" |> Option.map parseFile
                      Records = last "--records"
                      Plan = last "--plan" |> Option.map parseFile
                      KickoffPath = last "--kickoff-path" |> Option.defaultValue AssertionRatchet.defaultKickoffPath |> Some }

            writeOut m report
            if str "verdict" report = "PASS" then 0 else 3
        with ex ->
            eprintfn "input error: %s" ex.Message
            2

/// `plan-readiness`: Priority Two, the implementation-start gate (see PlanReadiness). Exit 0 READY, 3 BLOCKED,
/// 2 unusable input.
let planReadinessCommand (args: string list) : int =
    match optionMap args with
    | Error e ->
        eprintfn "%s" e
        2
    | Ok m when not (m.ContainsKey "--plan" && m.ContainsKey "--plan-digest") || (m.ContainsKey "--rules" <> m.ContainsKey "--rules-sha256") ->
        eprintfn "usage: requirements-compiler plan-readiness --plan FILE --plan-digest HEX [--rules FILE --rules-sha256 HEX] [--out FILE]"
        2
    | Ok m ->
        try
            let one k = m.[k] |> List.last
            let rules =
                m.TryFind "--rules"
                |> Option.map (fun r -> let bytes = File.ReadAllBytes(List.last r) in parseBytes bytes, Git.sha256Hex bytes, one "--rules-sha256")
            let report = PlanReadiness.assess (parseFile (one "--plan")) (one "--plan-digest") rules
            writeOut m report
            if str "verdict" report = "READY" then 0 else 3
        with ex ->
            eprintfn "input error: %s" ex.Message
            2

/// `propose-test-bindings`: ESO-R6-02. Proposes a test identity for every unresolved application-test and reviewer
/// category of the plan (see TestBindingProposal). Writes a proposal; never approves it. Exit 0, 2 unusable input.
let proposeTestBindingsCommand (args: string list) : int =
    match optionMap args with
    | Error e ->
        eprintfn "%s" e
        2
    | Ok m when not (m.ContainsKey "--plan") ->
        eprintfn "usage: requirements-compiler propose-test-bindings --plan FILE [--out FILE]"
        2
    | Ok m ->
        try
            writeOut m (TestBindingProposal.propose (parseFile (List.last m.["--plan"])))
            0
        with ex ->
            eprintfn "input error: %s" ex.Message
            2

/// `review-attestations`: ESO-R6-05. Turns the pull-request reviews the acceptance job fetched in its own run into
/// `indy-init.owner-attestations/1` (see ReviewAttestations). Exit 0, 2 unusable input.
let reviewAttestationsCommand (args: string list) : int =
    let required = [ "--repository"; "--run-id"; "--candidate"; "--pulls" ]
    match optionMap args with
    | Error e ->
        eprintfn "%s" e
        2
    | Ok m when required |> List.exists (m.ContainsKey >> not) ->
        eprintfn "usage: requirements-compiler review-attestations --repository OWNER/REPO --run-id ID --candidate SHA --pulls FILE [--out FILE]"
        2
    | Ok m ->
        try
            let one k = m.[k] |> List.last
            writeOut m (ReviewAttestations.build (one "--repository") (one "--run-id") (one "--candidate") (parseFile (one "--pulls")))
            0
        with ex ->
            eprintfn "input error: %s" ex.Message
            2


/// The F# source files that make up the compiler and its tests, relative to the compiler directory, for the tree
/// digest the compiler test record and the freeze manifest carry.
let compilerSources (compilerDir: string) : (string * byte[]) list =
    [ "src"; "tests" ]
    |> List.collect (fun d ->
        let dir = Path.Combine(compilerDir, d)
        if Directory.Exists dir then
            Directory.GetFiles(dir)
            |> Array.filter (fun f -> f.EndsWith(".fs", StringComparison.Ordinal) || f.EndsWith(".fsproj", StringComparison.Ordinal))
            |> Array.map (fun f -> sprintf "%s/%s" d (Path.GetFileName f), File.ReadAllBytes f)
            |> List.ofArray
        else [])

/// A commit message an agent wrote: it carries the attribution trailers agents add (RDP-003 evidence).
let private agentAttributed (message: string) =
    message.Contains("Claude-Session:", StringComparison.Ordinal) || message.Contains("Co-Authored-By: Claude", StringComparison.Ordinal)

/// True when the commit is reachable from origin/main.
let private onMain (repo: string) (commit: string) =
    // Only the remote-tracking ref fetched from GitHub counts; a local main can be moved by anyone (v4 review RV2-1).
    Git.run repo [ "merge-base"; "--is-ancestor"; commit; "origin/main" ] |> Result.isOk

/// Reads every input of the v4 stage from the source checkout and the package directory.
let v4Inputs (source: string) (package: string) (registry: string option) (r7Evidence: string option) : V4Package.Inputs =
    let pkg (rel: string) = Path.Combine(package, rel)
    let src (rel: string) = Path.Combine(source, rel)
    let orEmpty (path: string) = if File.Exists path then parseFile path else JObj []
    let sources = parseFile (pkg "curation/sources.json")
    let commitOf name = arr "repositories" sources |> List.tryFind (fun r -> str "name" r = name) |> Option.map (str "commit") |> Option.defaultValue ""
    let baselineBytes = File.ReadAllBytes(src "requirements/causa-requirements-baseline-v4.json")
    let baseline = parseBytes baselineBytes
    let approvalsPin = parseFile (pkg "curation/verification-plan.json") |> field "approvals" |> Option.defaultValue JNull
    let approvals =
        match Git.showFile source (str "commit" approvalsPin) (str "path" approvalsPin) with
        | Ok b -> parseBytes b
        | Error e -> failwithf "approvals file %s unreadable at %s: %s" (str "path" approvalsPin) (str "commit" approvalsPin) e
    let ownerDecisions =
        Directory.GetDirectories(src "kickoff")
        |> Array.sort
        |> Array.map (fun d -> Path.Combine(d, "owner-decisions.json"))
        |> Array.filter File.Exists
        |> Array.toList
        |> List.collect (parseFile >> arr "decisions")
    let registryCommit = commitOf "echelon-registry"
    let packageFiles =
        Directory.GetFiles(package, "*", SearchOption.AllDirectories)
        |> Array.map (fun f -> Path.GetRelativePath(package, f).Replace('\\', '/'))
        |> Array.sort
        |> List.ofArray
    { SourceRepository = field "source" baseline |> Option.map (str "repository") |> Option.defaultValue ""
      SourceCommit = field "source" baseline |> Option.map (str "commit") |> Option.defaultValue ""
      Baseline = baseline
      BaselineSha256 = Git.sha256Hex baselineBytes
      Previous = [ "r6", parseFile (src "requirements/causa-requirements-baseline-r6.json"); "r7", parseFile (src "requirements/causa-requirements-baseline-r7.json") ]
      R7Trace = parseFile (src "docs/rehearsal-007/traceability.json")
      OwnerDecisions = ownerDecisions
      Amendments = parseFile (pkg "analysis/amendments.json")
      Plan = parseFile (pkg "verification-plan.json")
      Tasks = arr "tasks" (parseFile (pkg "curation/tasks.json")) |> List.map taskOf
      Contracts = arr "contracts" (parseFile (pkg "curation/contracts.json"))
      Definitions = parseFile (pkg "analysis/operational-definitions.json")
      StateMachines = parseFile (pkg "analysis/state-machines.json")
      Invariants = parseFile (pkg "analysis/invariants.json")
      Lessons = parseFile (pkg "analysis/r7-lessons.json")
      Interfaces = parseFile (pkg "analysis/interfaces.json")
      Policy = parseFile (pkg "analysis/execution-policy.json")
      Risks = parseFile (pkg "analysis/risk-register.json")
      LockSource = parseFile (pkg "analysis/dependency-lock.source.json")
      Proposals = parseFile (pkg "proposals/owner-decision-requests.json")
      Approvals = approvals
      Registry = registry |> Option.map (fun dir -> fun path -> match Git.showFile dir registryCommit path with Ok b -> Some b | Error _ -> None)
      RegistryCommit = registryCommit
      R7Evidence = r7Evidence |> Option.map (fun dir -> fun rel -> let f = Path.Combine(dir, rel) in if File.Exists f then Some(File.ReadAllText f) else None)
      LegacyReport = parseFile (pkg "requirements-validation-report.json")
      CompilerTests = orEmpty (pkg "analysis/compiler-test-record.json")
      CompilerTreeSha256 = V4Package.treeSha256 (compilerSources (src "tools/requirements-compiler"))
      Review = orEmpty (pkg "analysis/independent-review.json")
      FreezeInput = orEmpty (pkg "analysis/freeze-input.json")
      PackageFiles = packageFiles
      TrustedOperators = strs "trustedOperators" approvalsPin
      ApprovalsAgentAuthored =
          match Git.run source [ "log"; "-1"; "--format=%B"; str "commit" approvalsPin; "--"; str "path" approvalsPin ] with
          | Ok b -> agentAttributed (utf8 b)
          | Error _ -> true
      ApprovalsOnMain = onMain source (str "commit" approvalsPin)
      FreezeCommitVerified =
          let fc = (orEmpty (pkg "analysis/freeze-input.json")) |> strOpt "freezeCommit" |> Option.defaultValue ""
          fc <> ""
          && Git.run source [ "merge-base"; "--is-ancestor"; fc; "HEAD" ] |> Result.isOk
          && onMain source fc
          && (match Git.showFile source fc (str "path" approvalsPin) with
              | Ok b -> V4Package.freezeCommitHoldsApproval (strs "trustedOperators" approvalsPin) (parseBytes b)
              | Error _ -> false)
      ReviewSha256 = (let f = pkg "analysis/independent-review.json" in if File.Exists f then fileSha f else "")
      TestRecordSha256 = (let f = pkg "analysis/compiler-test-record.json" in if File.Exists f then fileSha f else "")
      BaseBindings =
          TestBindingProposal.applyTo
              (arr "testBindings" (parseFile (src "requirements-compiled/v3/curation/verification-plan.json")))
              (parseFile (pkg "proposals/r7-test-bindings.proposed.json"))
      CurationBindings = arr "testBindings" (parseFile (pkg "curation/verification-plan.json"))
      ApprovalsPin =
          str "commit" approvalsPin, str "path" approvalsPin,
          (match Git.showFile source (str "commit" approvalsPin) (str "path" approvalsPin) with Ok b -> Git.sha256Hex b | Error _ -> "") }

/// Files of the v3 pipeline that the freeze manifest pins by digest, read as committed.
let private legacyPinned =
    [ "verification-plan.json"; "verification-manifest.json"; "timed-execution-manifest.json"; "dependency-graph.json"
      "normalized-requirements.json"; "requirements-validation-report.json"; "requirements-inventory.json"; "requirements-source-map.json"
      "decision-registry.json"; "contract-bindings.json"; "preflight-manifest.json"; "package-manifest.json" ]

/// `v4`: generates the v4 artifacts, the package validation report and the freeze manifest from the v3-pipeline
/// output already in the package directory. Exit 0 validation PASS (the package may still be a CANDIDATE), 3 a
/// critical check failed, 4 --check found files that differ, 2 unusable input.
let v4Command (args: string list) : int =
    let rec go (xs: string list) (m: Map<string, string>) (check: bool) =
        match xs with
        | "--check" :: rest -> go rest m true
        | k :: v :: rest when k.StartsWith("--", StringComparison.Ordinal) -> go rest (m.Add(k, v)) check
        | [] -> Ok(m, check)
        | a :: _ -> Error(sprintf "unknown argument '%s'" a)
    match go args Map.empty false with
    | Error e ->
        eprintfn "%s" e
        2
    | Ok(m, _) when not (m.ContainsKey "--source" && m.ContainsKey "--package") ->
        eprintfn "usage: requirements-compiler v4 --source DIR --package DIR --registry DIR --r7-evidence DIR [--check] [--apply-bindings true] [--verify-test-record FILE]"
        2
    | Ok(m, check) ->
        try
            let source = m.["--source"]
            let package = m.["--package"]
            let i = v4Inputs source package (m.TryFind "--registry") (m.TryFind "--r7-evidence")
            // --apply-bindings: writes the curation's testBindings as the base bindings extended by the strengthened
            // proposal (check W16); the v3 pipeline must then be re-run so the plan carries them.
            if m.TryFind "--apply-bindings" = Some "true" then
                let curationPath = Path.Combine(package, "curation", "verification-plan.json")
                let curation = parseFile curationPath
                let bindings = JArr(TestBindingProposal.applyTo i.BaseBindings (V4Package.strengthenedProposal i))
                let updated = match curation with JObj ps -> JObj(ps |> List.map (fun (k, v) -> if k = "testBindings" then k, bindings else k, v)) | j -> j
                File.WriteAllText(curationPath, serialize updated + "\n")
                printfn "curation testBindings updated; re-run the v3 pipeline, then v4"
            // --verify-test-record: a record the CI job produced in this run must agree with the committed one.
            let freshProblems =
                match m.TryFind "--verify-test-record" with
                | None -> []
                | Some f ->
                    let fresh = parseFile f
                    [ if strOpt "result" fresh <> Some "PASS" then yield "the compiler tests did not pass in this run"
                      if strOpt "compilerTreeSha256" fresh <> strOpt "compilerTreeSha256" i.CompilerTests then yield "the committed compiler test record is for another compiler tree than the one tested in this run" ]
            freshProblems |> List.iter (eprintfn "test record: %s")
            let built = V4Package.build i
            let report = V4Package.validationReport i built.Checks
            let generated = built.Files @ [ "package-validation-report.json", report ] |> List.map (fun (n, j) -> n, serialize j)
            let sha (text: string) = Text.Encoding.UTF8.GetBytes text |> Git.sha256Hex
            let legacy = legacyPinned |> List.map (fun n -> n, fileSha (Path.Combine(package, n)))
            let digests = legacy @ (generated |> List.map (fun (n, t) -> n, sha t))
            let legacyPass = str "compilationVerdict" i.LegacyReport = "PASS"
            let manifest = V4Package.freezeManifest i built digests legacyPass |> serialize
            let all = generated @ [ "freeze-manifest.json", manifest ]
            built.Checks |> List.iter (fun c ->
                printfn "%s %s  %s" (if c.Problems.IsEmpty then "PASS" else "FAIL") c.Id c.Title
                c.Problems |> List.truncate 10 |> List.iter (printfn "      - %s"))
            let status = parseBytes (Text.Encoding.UTF8.GetBytes manifest) |> str "status"
            printfn "package validation: %s; freeze status: %s" (str "verdict" (parseBytes (Text.Encoding.UTF8.GetBytes (serialize report)))) status
            if check then
                let differing = all |> List.filter (fun (n, t) -> let p = Path.Combine(package, n) in not (File.Exists p) || File.ReadAllText p <> t) |> List.map fst
                differing |> List.iter (eprintfn "differs from the package: %s")
                if not differing.IsEmpty then 4 elif str "verdict" (parseBytes (Text.Encoding.UTF8.GetBytes (serialize report))) = "PASS" && freshProblems.IsEmpty then 0 else 3
            else
                for (n, t) in all do
                    let p = Path.Combine(package, n)
                    Directory.CreateDirectory(Path.GetDirectoryName p) |> ignore
                    File.WriteAllText(p, t)
                if str "verdict" (parseBytes (Text.Encoding.UTF8.GetBytes (serialize report))) = "PASS" then 0 else 3
        with ex ->
            eprintfn "input error: %s" ex.Message
            2

/// Reads the Forma npm tarball in memory (it is never extracted to disk) and returns its SHA-256, dist/all.css and
/// figma/component-contracts.json.
let formaFromTarball (path: string) : string * string * Json =
    let bytes = File.ReadAllBytes path
    use gz = new IO.Compression.GZipStream(new MemoryStream(bytes), IO.Compression.CompressionMode.Decompress)
    use tar = new Formats.Tar.TarReader(gz)
    let rec read (acc: Map<string, byte[]>) =
        match tar.GetNextEntry() with
        | null -> acc
        | e when (e.Name = "package/dist/all.css" || e.Name = "package/figma/component-contracts.json") && not (isNull e.DataStream) ->
            use ms = new MemoryStream()
            e.DataStream.CopyTo ms
            read (acc.Add(e.Name, ms.ToArray()))
        | _ -> read acc
    let entries = read Map.empty
    let get n = match entries.TryFind n with Some b -> b | None -> failwithf "%s is not in the Forma tarball" n
    Git.sha256Hex bytes, Text.Encoding.UTF8.GetString(get "package/dist/all.css"), parseBytes (get "package/figma/component-contracts.json")

/// Reads every input of the v4.1 stage: v4's plan, manifest, machines, tasks and policy, and the authored v4.1 inputs.
let v41Inputs (source: string) (package: string) (basePackage: string) (formaTarball: string option) : V41Package.Inputs =
    let a n = Path.Combine(package, "analysis", n)
    let authored = [ "interface-specifications.json"; "project-structure.json"; "github-error-matrix.json"; "forma-layout-contract.json"; "task-adjustments.json"; "conflict-telemetry.json"; "guard-partitions.json" ]
    // Documents and records the owner approves with the package; the review record exists once a review has run.
    let documents =
        [ "README.md"; "AGENT-RULES.md"; "FREEZE.md"; "optimization-report.md"; "independent-review.md"; "proposals/owner-decision-requests.json"; "analysis/independent-review.json"
          "layout-tests/assertions.mjs"; "layout-tests/fixtures.mjs"; "layout-tests/run.mjs"; "layout-tests/catch-rate.mjs" ]
    let forma = parseFile (a "forma-layout-contract.json")
    let formaSpec = field "forma" forma |> Option.defaultValue JNull
    let inventoryPath = Path.Combine(package, str "inventory" formaSpec)
    let extract =
        formaTarball |> Option.map (fun t ->
            let sha, css, contracts = formaFromTarball t
            if sha <> str "tarballSha256" formaSpec then failwithf "the Forma tarball has sha256 %s, the layout contract names %s" sha (str "tarballSha256" formaSpec)
            V41Package.formaInventory (str "version" formaSpec) sha css contracts)
    let planBytes = File.ReadAllBytes(Path.Combine(basePackage, "verification-plan.json"))
    { PlanBytes = planBytes
      Plan = parseBytes planBytes
      BaseManifest = parseFile (Path.Combine(basePackage, "freeze-manifest.json"))
      Baseline = parseFile (Path.Combine(source, "requirements", "causa-requirements-baseline-v4.json"))
      StateMachines = parseFile (Path.Combine(basePackage, "state-machines.json"))
      Tasks = parseFile (Path.Combine(basePackage, "curation", "tasks.json")) |> arr "tasks" |> List.map taskOf
      Policy = parseFile (Path.Combine(basePackage, "analysis", "execution-policy.json"))
      Interfaces = parseFile (a "interface-specifications.json")
      Structure = parseFile (a "project-structure.json")
      ErrorMatrix = parseFile (a "github-error-matrix.json")
      Forma = forma
      Adjustments = parseFile (a "task-adjustments.json")
      Telemetry = parseFile (a "conflict-telemetry.json")
      GuardPartitions = parseFile (a "guard-partitions.json")
      FormaInventory =
        if File.Exists inventoryPath then parseFile inventoryPath
        else match extract with Some x -> x | None -> failwithf "%s is missing; generate it with --forma-tarball" inventoryPath
      FormaExtract = extract
      ReadSource = fun p -> let f = Path.Combine(source, p) in if File.Exists f then Some(File.ReadAllText f) else None
      AuthoredText =
        (authored |> List.map (fun n -> "analysis/" + n, File.ReadAllText(a n)))
        @ (documents |> List.filter (fun n -> File.Exists(Path.Combine(package, n))) |> List.map (fun n -> n, File.ReadAllText(Path.Combine(package, n)))) }

/// `v41`: generates the v4.1 artifacts and validation report from v4 and the authored v4.1 inputs. Exit 0 validation
/// PASS (the package stays a CANDIDATE), 3 a check failed, 4 --check found files that differ, 2 unusable input.
let v41Command (args: string list) : int =
    let rec go (xs: string list) (m: Map<string, string>) (check: bool) =
        match xs with
        | "--check" :: rest -> go rest m true
        | k :: v :: rest when k.StartsWith("--", StringComparison.Ordinal) -> go rest (m.Add(k, v)) check
        | [] -> Ok(m, check)
        | a :: _ -> Error(sprintf "unknown argument '%s'" a)
    match go args Map.empty false with
    | Error e ->
        eprintfn "%s" e
        2
    | Ok(m, _) when not (m.ContainsKey "--source" && m.ContainsKey "--package" && m.ContainsKey "--base") ->
        eprintfn "usage: requirements-compiler v41 --source DIR --package DIR --base DIR [--forma-tarball FILE] [--check] [--bootstrap EMPTY-DIR]"
        2
    | Ok(m, check) ->
        try
            let package = m.["--package"]
            let i = v41Inputs m.["--source"] package m.["--base"] (m.TryFind "--forma-tarball")
            let built = V41Package.build i
            // --bootstrap: write the empty application repository into a new or empty directory (never the package).
            match m.TryFind "--bootstrap" with
            // An empty application repository may already hold its .git directory (git init, or a clone of an empty
            // repository); anything else is refused, so the bootstrap never overwrites work.
            | Some dir when Directory.Exists dir && Directory.EnumerateFileSystemEntries dir |> Seq.exists (fun e -> Path.GetFileName e <> ".git") ->
                failwithf "--bootstrap needs a new or empty directory (a .git directory is allowed); %s holds other files" dir
            | Some dir ->
                let files = V41Package.bootstrapFiles i (V41Package.ownedFiles i) (File.ReadAllText(Path.Combine(m.["--source"], "global.json")))
                for (n, t) in files do
                    let p = Path.Combine(dir, n)
                    Directory.CreateDirectory(Path.GetDirectoryName p) |> ignore
                    File.WriteAllText(p, t)
                printfn "bootstrap: %d files written to %s" files.Length dir
            | None -> ()
            let report = V41Package.validationReport built.Checks
            let verdict = str "verdict" report
            let inventory =
                match i.FormaExtract with
                | Some x -> [ str "inventory" (field "forma" i.Forma |> Option.defaultValue JNull), serialize x ]
                | None -> []
            let generated = built.Files @ inventory @ [ "validation-report.json", serialize report ]
            let sha (text: string) = Text.Encoding.UTF8.GetBytes text |> Git.sha256Hex
            let authored = i.AuthoredText |> List.map (fun (n, t) -> n, sha t)
            // The inventory is generated only when the tarball is supplied; otherwise the committed one is pinned.
            let inventoryPin =
                match inventory with
                | [] -> let n = str "inventory" (field "forma" i.Forma |> Option.defaultValue JNull) in [ n, sha (File.ReadAllText(Path.Combine(package, n))) ]
                | _ -> []
            let manifest = V41Package.packageManifest i (authored @ inventoryPin @ (generated |> List.map (fun (n, t) -> n, sha t))) verdict |> serialize
            let all = generated @ [ "package-manifest.json", manifest ]
            built.Checks |> List.iter (fun c ->
                printfn "%s %s  %s" (if c.Problems.IsEmpty then "PASS" else "FAIL") c.Id c.Title
                c.Problems |> List.truncate 10 |> List.iter (printfn "      - %s"))
            printfn "v4.1 validation: %s; status: CANDIDATE" verdict
            if check then
                let differing = all |> List.filter (fun (n, t) -> let p = Path.Combine(package, n) in not (File.Exists p) || File.ReadAllText p <> t) |> List.map fst
                differing |> List.iter (eprintfn "differs from the package: %s")
                // A file in the package that the compiler neither generates nor reads is stale or foreign.
                let expected = (all |> List.map fst) @ (i.AuthoredText |> List.map fst) @ (inventoryPin |> List.map fst) |> Set.ofList
                let present = Directory.GetFiles(package, "*", SearchOption.AllDirectories) |> Array.map (fun f -> Path.GetRelativePath(package, f).Replace('\\', '/')) |> Array.sort |> List.ofArray
                let stray = present |> List.filter (expected.Contains >> not)
                stray |> List.iter (eprintfn "not generated or read by the compiler: %s")
                if not differing.IsEmpty || not stray.IsEmpty then 4 elif verdict = "PASS" then 0 else 3
            else
                for (n, t) in all do
                    let p = Path.Combine(package, n)
                    Directory.CreateDirectory(Path.GetDirectoryName p) |> ignore
                    File.WriteAllText(p, t)
                if verdict = "PASS" then 0 else 3
        with ex ->
            eprintfn "input error: %s" ex.Message
            2

[<EntryPoint>]
let main argv =
    match List.ofArray argv with
    | [ "baseline"; root; commit ] -> baselineCommand root commit
    | "verify-plan" :: rest -> verifyPlanCommand rest
    | "verify-process" :: rest -> verifyProcessCommand rest
    | "verify-defect-closures" :: rest -> verifyDefectClosuresCommand rest
    | "release-comparison" :: rest -> releaseComparisonCommand rest
    | "assertion-ratchet" :: rest -> assertionRatchetCommand rest
    | "plan-readiness" :: rest -> planReadinessCommand rest
    | "propose-test-bindings" :: rest -> proposeTestBindingsCommand rest
    | "review-attestations" :: rest -> reviewAttestationsCommand rest
    | "v4" :: rest -> v4Command rest
    | "v41" :: rest -> v41Command rest
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
