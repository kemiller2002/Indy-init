// tests.fsx: focused regression cases for the release-gate repair (docs/release-gate-repair). F#, no network.
//
//   dotnet fsi tools/release-gate-repair/tests.fsx -- --indy-init SRC --praxis EXE --node-modules DIR --work NEW-DIR --evidence DIR
//
// --node-modules is a node_modules directory holding @playwright/test 1.63.0 (any application's). Every case writes
// under --work; each records its command, exit code and expectation in --evidence/cases.tsv; exit 1 if any fails.
//
// Cases: the two Rehearsal 009 suite-command defects (MSB1008, a Playwright project no configuration defines) are
// refused by X21 and by suite-preflight, and pass once corrected; frozen packages regenerate unchanged; the Praxis
// ordering gate refuses each missing step, writes an immutable checkpoint, and check-sequencing reports
// implementation before authorization; acceptance-status keeps its four levels apart, refuses missing and stale
// evidence, and gives a scoped verdict that never claims the full plan. Integration with #102: suite-preflight
// --require-tests and the deterministic fixture, and check-sequencing --scope-manifest (the scope checkpoint).

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text.Json.Nodes

let argv = fsi.CommandLineArgs |> Array.toList |> List.tail
let opt name = argv |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd |> Option.defaultWith (fun () -> failwithf "missing %s" name)
let src = Path.GetFullPath(opt "--indy-init")
let praxis = Path.GetFullPath(opt "--praxis")
let nodeModules = Path.GetFullPath(opt "--node-modules")
let work = Directory.CreateDirectory(Path.GetFullPath(opt "--work")).FullName
let evidence = Directory.CreateDirectory(Path.GetFullPath(opt "--evidence")).FullName
let r9 = Path.Combine(src, "tools", "r9", "r9.fsx")
let rc = Path.Combine(src, "tools", "requirements-compiler", "src", "bin", "Release", "net10.0", "requirements-compiler.dll")

let run (exe: string) (args: string list) (cwd: string) =
    let info = ProcessStartInfo(exe, WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true)
    args |> List.iter info.ArgumentList.Add
    use p = Process.Start info
    let o = p.StandardOutput.ReadToEndAsync()
    let e = p.StandardError.ReadToEndAsync()
    p.WaitForExit()
    p.ExitCode, o.Result + e.Result

let mutable cases : (string * bool) list = []
let tsv = Path.Combine(evidence, "cases.tsv")
File.WriteAllText(tsv, "case\texpected\tactual\tresult\tcommand\n")

let record (name: string) (ok: bool) (expected: string) (actual: string) (command: string) (output: string) =
    let verdict = if ok then "PASS" else "FAIL"
    File.WriteAllText(Path.Combine(evidence, name + ".log"), $"# cmd: {command}\n{output}\n# expected: {expected}\n# result: {verdict}\n")
    File.AppendAllText(tsv, String.Join("\t", [ name; expected; actual; (if ok then "PASS" else "FAIL"); command ]) + "\n")
    printfn "%-58s %s" name (if ok then "PASS" else "FAIL  <<<")
    cases <- (name, ok) :: cases

/// The exit code must be one of `expected`, every `mustContain` must appear and no `mustNot` may.
let case name (expected: int list) (mustContain: string list) (mustNot: string list) exe args cwd =
    let code, output = run exe args cwd
    let ok = List.contains code expected && mustContain |> List.forall output.Contains && mustNot |> List.forall (output.Contains >> not)
    record name ok $"""exit {String.Join("|", expected)} containing [{String.Join("; ", mustContain)}]""" (string code) (exe + " " + String.Join(" ", args)) output
    code, output

let r9case name expected contains args cwd = case name expected contains [] "dotnet" ([ "fsi"; r9; "--" ] @ args) cwd
let write (path: string) (text: string) =
    Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
    File.WriteAllText(path, text)
let git (repo: string) args = run "git" ([ "-C"; repo; "-c"; "user.name=rg-tests"; "-c"; "user.email=rg@example.invalid"; "-c"; "commit.gpgsign=false" ] @ args) repo |> snd
let commitAll repo message =
    git repo [ "add"; "-A" ] |> ignore
    git repo [ "commit"; "-qm"; message ] |> ignore
    (git repo [ "rev-parse"; "HEAD" ]).Trim()
let sha256 (path: string) = SHA256.HashData(File.ReadAllBytes path) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()
let rec copyDir (s: string) (t: string) =
    Directory.CreateDirectory t |> ignore
    Directory.GetFiles s |> Array.iter (fun f -> File.Copy(f, Path.Combine(t, Path.GetFileName f), true))
    Directory.GetDirectories s |> Array.iter (fun d -> copyDir d (Path.Combine(t, Path.GetFileName d)))

// ---- 1. Package generation: X21 and the frozen packages ------------------------------------------------------------

case "v41-v4.1.2-regenerates-unchanged" [ 0 ] [ "v4.1.2 validation: PASS" ] [ "X21" ] "dotnet" [ rc; "v41"; "--source"; src; "--package"; Path.Combine(src, "requirements-compiled", "v4.1.2"); "--base"; Path.Combine(src, "requirements-compiled", "v4"); "--check" ] src |> ignore
case "v41-v4.1.4-x21-passes" [ 0 ] [ "PASS X21"; "v4.1.4 validation: PASS" ] [] "dotnet" [ rc; "v41"; "--source"; src; "--package"; Path.Combine(src, "requirements-compiled", "v4.1.4"); "--base"; Path.Combine(src, "requirements-compiled", "v4"); "--check" ] src |> ignore

// The Rehearsal 009 runners under the v4.1.4 checks: a copy of v4.1.4 whose amendment keeps the bootstrapped
// configuration but drops the corrected runners must fail X21 for both defects.
let pkgSource = Path.Combine(work, "pkg-src")
copyDir (Path.Combine(src, "requirements")) (Path.Combine(pkgSource, "requirements"))
copyDir (Path.Combine(src, "requirements-compiled", "v4")) (Path.Combine(pkgSource, "requirements-compiled", "v4"))
copyDir (Path.Combine(src, "requirements-compiled", "v4.1.2")) (Path.Combine(pkgSource, "requirements-compiled", "v4.1.2"))
copyDir (Path.Combine(src, "requirements-compiled", "v4.1.4")) (Path.Combine(pkgSource, "requirements-compiled", "v4.1.4"))
for d in [ "docs"; "kickoff" ] do copyDir (Path.Combine(src, d)) (Path.Combine(pkgSource, d))
let amendmentPath = Path.Combine(pkgSource, "requirements-compiled", "v4.1.4", "analysis", "amendment.json")
let amendment = JsonNode.Parse(File.ReadAllText amendmentPath) |> nonNull
(amendment :?> JsonObject).Remove "suiteRunners" |> ignore
File.WriteAllText(amendmentPath, amendment.ToJsonString())
case "x21-refuses-rehearsal-009-runners" [ 3 ] [ "FAIL X21"; "SU-OWN-DOTNET: dotnet test names 4 targets"; "MSB1008"; "SU-DOTNET-LIVE: dotnet test names 2 targets"; "SU-BROWSER-WEBKIT: the runner does not select the bootstrapped configuration" ] [] "dotnet" [ rc; "v41"; "--source"; pkgSource; "--package"; Path.Combine(pkgSource, "requirements-compiled", "v4.1.4"); "--base"; Path.Combine(pkgSource, "requirements-compiled", "v4") ] pkgSource |> ignore

// The bootstrapped configuration defines exactly the projects the runners name.
let boot = Path.Combine(work, "boot414")
Directory.CreateDirectory boot |> ignore
case "bootstrap-v4.1.4-writes-playwright-config" [ 0 ] [] [] "dotnet" [ rc; "v41"; "--source"; src; "--package"; Path.Combine(src, "requirements-compiled", "v4.1.4"); "--base"; Path.Combine(src, "requirements-compiled", "v4"); "--bootstrap"; boot ] src |> ignore
let config = Path.Combine(boot, "playwright.config.ts")
let configText = if File.Exists config then File.ReadAllText config else ""
record "playwright-config-defines-chromium-and-webkit" (configText.Contains "name: \"chromium\"" && configText.Contains "name: \"webkit\"" && configText.Contains "Desktop Safari") "chromium and webkit projects" (string configText.Length) "inspect playwright.config.ts" configText

// ---- 2. suite-preflight executes the commands --------------------------------------------------------------------

let suiteRepo = Path.Combine(work, "suites")
Directory.CreateDirectory suiteRepo |> ignore
write (Path.Combine(suiteRepo, "global.json")) """{ "sdk": { "version": "10.0.402", "rollForward": "disable" } }"""
write (Path.Combine(suiteRepo, "Causa.slnx")) "<Solution>\n</Solution>\n"
write (Path.Combine(suiteRepo, "package.json")) """{ "name": "suites", "private": true, "type": "module" }"""
// Conditor's scaffold configuration: chromium only (Rehearsal 009).
write (Path.Combine(suiteRepo, "playwright.config.js")) "import { defineConfig, devices } from \"@playwright/test\";\nexport default defineConfig({ testDir: \"./tests/browser\", projects: [{ name: \"chromium\", use: { ...devices[\"Desktop Chrome\"] } }] });\n"
Directory.CreateSymbolicLink(Path.Combine(suiteRepo, "node_modules"), nodeModules) |> ignore
let obligationsWith (name: string) (suites: (string * string * string) list) =
    let path = Path.Combine(work, name + ".json")
    let suiteJson = suites |> List.map (fun (id, runner, command) -> $"""{{"id":"{id}","runner":"{runner}","command":{Text.Json.JsonSerializer.Serialize command}}}""")
    write path ($"""{{"schema":"dokimos.verification-obligations/1","planId":"VP-TEST","obligations":[],"tests":[],"suites":[{String.Join(",", suiteJson)}]}}""")
    path
let r9Runners =
    obligationsWith "r9-runners"
        [ "SU-OWN-DOTNET", "dotnet-test", "dotnet test tests/Causa.Contract.Tests tests/Causa.Domain.Tests tests/Causa.Persistence.Tests tests/Causa.App.Tests"
          "SU-BROWSER-WEBKIT", "playwright", "npx playwright test --project=webkit tests/browser/acceptance tests/browser/invariants" ]
r9case "suite-preflight-refuses-msb1008-and-missing-webkit" [ 4 ] [ "SU-OWN-DOTNET cannot execute"; "MSB1008"; "SU-BROWSER-WEBKIT cannot execute"; "Project(s) \"webkit\" not found" ] [ "suite-preflight"; "--obligations"; r9Runners; "--root"; suiteRepo ] suiteRepo |> ignore
File.Copy(config, Path.Combine(suiteRepo, "playwright.config.ts"), true)
let fixedRunners =
    obligationsWith "v414-runners"
        [ "SU-OWN-DOTNET", "dotnet-test", "dotnet test Causa.slnx --filter \"FullyQualifiedName~Causa.Contract.Tests.|FullyQualifiedName~Causa.Domain.Tests.\""
          "SU-BROWSER-WEBKIT", "playwright", "npx playwright test --config playwright.config.ts --project=webkit tests/browser/acceptance tests/browser/invariants" ]
r9case "suite-preflight-passes-corrected-runners" [ 0 ] [ "SU-OWN-DOTNET: executable"; "SU-BROWSER-WEBKIT: executable" ] [ "suite-preflight"; "--obligations"; fixedRunners; "--root"; suiteRepo; "--out"; Path.Combine(work, "suite-preflight.json") ] suiteRepo |> ignore
let noSuites = obligationsWith "no-suites" []
r9case "suite-preflight-refuses-a-plan-without-suites" [ 2 ] [ "the obligations name no executable suite" ] [ "suite-preflight"; "--obligations"; noSuites; "--root"; suiteRepo ] suiteRepo |> ignore
// #102: a command that executes but selects no test proves nothing (a filtered `dotnet test` exits 0 on no match).
r9case "suite-preflight-require-tests-refuses-an-empty-selection" [ 4 ] [ "selects no test" ] [ "suite-preflight"; "--obligations"; fixedRunners; "--root"; suiteRepo; "--require-tests"; "true" ] suiteRepo |> ignore
// #102: the deterministic fixture (the plan's test layout, one trivial test per suite) executes a package's own
// commands before T0: the frozen v4.1.2 runners are refused (MSB1008), v4.1.4's list a test in every suite, and a
// configuration without the webkit project is refused.
let fixture = Path.Combine(work, "fixture")
let fixtureNoWebkit = Path.Combine(work, "fixture-nowebkit")
r9case "suite-fixture-writes-the-plan-layout" [ 0 ] [ "committed (no application code)" ] [ "suite-fixture"; "--out"; fixture ] work |> ignore
r9case "suite-fixture-without-webkit" [ 0 ] [ "committed (no application code)" ] [ "suite-fixture"; "--out"; fixtureNoWebkit; "--without-webkit"; "true" ] work |> ignore
[ fixture; fixtureNoWebkit ] |> List.iter (fun d -> if Directory.Exists d then Directory.CreateSymbolicLink(Path.Combine(d, "node_modules"), nodeModules) |> ignore)
let planOf (v: string) = Path.Combine(src, "requirements-compiled", v, "verification-execution-plan.json")
r9case "suite-preflight-fixture-refuses-v4.1.2" [ 4 ] [ "SU-OWN-DOTNET cannot execute"; "SU-DOTNET-LIVE cannot execute"; "MSB1008" ] [ "suite-preflight"; "--plan"; planOf "v4.1.2"; "--root"; fixture; "--require-tests"; "true" ] fixture |> ignore
r9case "suite-preflight-fixture-passes-v4.1.4-with-tests" [ 0 ] [ "SU-OWN-DOTNET: executable, 4 test(s) listed"; "SU-DOTNET-LIVE: executable, 2 test(s) listed"; "SU-BROWSER-WEBKIT: executable, 2 test(s) listed" ] [ "suite-preflight"; "--plan"; planOf "v4.1.4"; "--root"; fixture; "--require-tests"; "true" ] fixture |> ignore
r9case "suite-preflight-fixture-refuses-a-missing-webkit-project" [ 4 ] [ "SU-BROWSER-WEBKIT cannot execute"; "Project(s) \"webkit\" not found" ] [ "suite-preflight"; "--plan"; planOf "v4.1.4"; "--root"; fixtureNoWebkit; "--require-tests"; "true" ] fixtureNoWebkit |> ignore

// ---- 3. Praxis ordering: authorize-implementation and check-sequencing -------------------------------------------

let app = Path.Combine(work, "app")
Directory.CreateDirectory app |> ignore
git app [ "init"; "-q"; "-b"; "main" ] |> ignore
write (Path.Combine(app, "README.md")) "competition repository\n"
commitAll app "owner: initial commit" |> ignore
// T0 bootstrap: Conditor's records, governance, the scaffold and the catalog manifest (no implementation).
write (Path.Combine(app, "conditor.json")) "{ \"preset\": \"indy-init\" }\n"
write (Path.Combine(app, ".conditor", "lock.json")) "{ \"schema\": \"conditor.lock/1\" }\n"
write (Path.Combine(app, ".echelon", "r9-governance.json")) "{ \"schema\": \"indy-init.r9-governance-record/1\" }\n"
run praxis [ "init"; "--root"; app ] app |> ignore
let catalog = Path.Combine(src, "requirements", "catalogs", "causa-investigation.requirement-catalog.json")
let manifest = Path.Combine(app, "requirements", "verification", "verification-manifest.json")
Directory.CreateDirectory(Path.GetDirectoryName manifest) |> ignore
case "praxis-compiles-the-catalog-manifest" [ 0 ] [] [] praxis [ "requirements"; "manifest"; "compile"; "--catalog"; catalog; "--source-commit"; "bc3c381df2840187a46799ea59f425ffe1b1ceed"; "--output"; manifest ] app |> ignore
write (Path.Combine(app, "src", "Causa.Domain", "Identity.fs")) "namespace Causa.Domain\n"
let bootstrapSha = commitAll app "T0 bootstrap"
// A tiny frozen plan: one requirement with one executable obligation, bound in SU-OWN-DOTNET.
let plan = Path.Combine(work, "plan-obligations.json")
write plan """{"schema":"dokimos.verification-obligations/1","planId":"VP-TEST","obligations":[{"id":"SMK-001-O1","requirement":"SMK-001"}],"tests":[{"id":"dotnet:Causa.Domain.Tests.Smoke.SMK-001-O1","suite":"SU-OWN-DOTNET","obligations":["SMK-001-O1"]}],"suites":[]}"""
let scopeFile = Path.Combine(work, "scope.json")
write scopeFile """{"schema":"indy-init.competition-scope/1","id":"CS-TEST","status":"proposed","requirements":["SMK-001"],"catalogRequirements":["INV-001"]}"""
let spec = Path.Combine(work, "spec-preflight.json")
write spec """{"schema":"indy-init.r9-spec-preflight/1","verdict":"PASS"}"""
let acceptance = Path.Combine(work, "acceptance-preflight.log")
write acceptance "acceptance preflight: READY\n"
let authorizeWith (suiteReport: string) =
    [ "authorize-implementation"; "--target"; app; "--catalog"; catalog; "--obligations"; plan; "--suite-preflight"; suiteReport
      "--spec-preflight"; spec; "--acceptance-preflight"; acceptance; "--scope"; scopeFile; "--praxis"; praxis ]
let authorize (_: string list) = authorizeWith (Path.Combine(work, "suite-preflight.json"))
r9case "authorize-refuses-without-work-items" [ 4 ] [ "3 work items:"; "r9-work-plan.json"; "Praxis holds no work item" ] (authorize []) app |> ignore
run praxis [ "work"; "begin"; "--id"; "WI-CONTENT-1"; "--occurred-at"; DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"); "--root"; app ] app |> ignore
write (Path.Combine(app, ".echelon", "r9-work-plan.json")) """{"schema":"indy-init.r9-work-plan/1","workItems":[{"id":"WI-CONTENT-1","requirements":["INV-001"],"obligations":["INV-001/V1/mapped"]}]}"""
commitAll app "work plan (partial)" |> ignore
r9case "authorize-refuses-unlinked-in-scope-obligations" [ 4 ] [ "4 links:"; "in-scope obligation(s) are linked to no work item" ] (authorize []) app |> ignore
let manifestJson = JsonNode.Parse(File.ReadAllText manifest)
let catalogObligations =
    manifestJson["requirements"].AsArray()
    |> Seq.filter (fun r -> r["id"].GetValue<string>() = "INV-001")
    |> Seq.collect (fun r -> r["obligations"].AsArray() |> Seq.map (fun o -> "\"" + o["id"].GetValue<string>() + "\""))
    |> List.ofSeq
write (Path.Combine(app, ".echelon", "r9-work-plan.json")) ($"""{{"schema":"indy-init.r9-work-plan/1","workItems":[{{"id":"WI-CONTENT-1","requirements":["INV-001","SMK-001"],"obligations":[{String.Join(",", catalogObligations)},"SMK-001-O1","NOT-AN-OBLIGATION"]}}]}}""")
commitAll app "work plan (bad link)" |> ignore
r9case "authorize-refuses-a-link-to-an-unknown-obligation" [ 4 ] [ "names obligation NOT-AN-OBLIGATION, which neither" ] (authorize []) app |> ignore
write (Path.Combine(app, ".echelon", "r9-work-plan.json")) ($"""{{"schema":"indy-init.r9-work-plan/1","workItems":[{{"id":"WI-CONTENT-1","requirements":["INV-001","SMK-001"],"obligations":[{String.Join(",", catalogObligations)},"SMK-001-O1"]}}]}}""")
commitAll app "work plan" |> ignore
let badSuites = obligationsWith "bad-suite-report" []
write badSuites """{"schema":"indy-init.r9-suite-preflight/1","suites":[{"id":"SU-OWN-DOTNET","executable":false,"cause":"MSB1008"}]}"""
r9case "authorize-refuses-a-non-executable-matrix" [ 4 ] [ "5 matrix: suite SU-OWN-DOTNET binds in-scope obligations but its command cannot execute"; "6 preflight: suite-preflight did not pass" ] (authorizeWith badSuites) app |> ignore
r9case "authorize-passes-and-writes-the-checkpoint" [ 0 ] [ "checkpoint"; "1 work item(s)" ] (authorize []) app |> ignore
let checkpointSha = commitAll app "initialization checkpoint"
r9case "authorize-refuses-a-second-checkpoint" [ 4 ] [ "is immutable" ] (authorize []) app |> ignore
r9case "check-sequencing-passes-ordered-history" [ 0 ] [ "no implementation before authorization" ] [ "check-sequencing"; "--target"; app ] app |> ignore
write (Path.Combine(app, "src", "Causa.Domain", "Content.fs")) "namespace Causa.Domain.Content\n"
commitAll app "implementation after authorization" |> ignore
r9case "check-sequencing-allows-implementation-after-the-checkpoint" [ 0 ] [ "no implementation before authorization" ] [ "check-sequencing"; "--target"; app ] app |> ignore
// Tamper with the checkpoint after it was committed.
File.AppendAllText(Path.Combine(app, ".echelon", "r9-initialization-checkpoint.json"), " ")
commitAll app "agent: touch the checkpoint" |> ignore
r9case "check-sequencing-refuses-an-edited-checkpoint" [ 4 ] [ "was changed after it was committed" ] [ "check-sequencing"; "--target"; app ] app |> ignore

// Implementation before authorization (Rehearsal 009's history): reported as process violations.
let early = Path.Combine(work, "early")
Directory.CreateDirectory early |> ignore
git early [ "init"; "-q"; "-b"; "main" ] |> ignore
write (Path.Combine(early, ".echelon", "r9-governance.json")) "{}\n"
commitAll early "T0 bootstrap" |> ignore
r9case "check-sequencing-passes-a-bootstrap-only-repository" [ 0 ] [ "implementation not yet authorized, and none committed" ] [ "check-sequencing"; "--target"; early ] early |> ignore
write (Path.Combine(early, "src", "App.fs")) "module App\n"
let earlySha = commitAll early "implement before authorizing"
write (Path.Combine(early, ".echelon", "r9-initialization-checkpoint.json")) $"""{{"authorizedAtCommit":"{earlySha}"}}"""
commitAll early "checkpoint after the fact" |> ignore
r9case "check-sequencing-reports-implementation-before-authorization" [ 4 ] [ "process violation:"; "implement before authorizing" ] [ "check-sequencing"; "--target"; early ] early |> ignore

// The scope checkpoint (#102, decision 3): with --scope-manifest, check-sequencing also requires the committed,
// immutable scope checkpoint with the manifest's scopeSha256 and every work item in Praxis, and implementation is
// authorized only from the later of the two checkpoints.
let scopeManifest = Path.Combine(work, "scope-manifest.json")
write scopeManifest """{"schema":"indy-init.approved-scope/1","scopeSha256":"scope-1","workItems":[{"id":"T10"},{"id":"WI-INV-START"}]}"""
let scopedRepo (name: string) =
    let repo = Path.Combine(work, name)
    Directory.CreateDirectory repo |> ignore
    git repo [ "init"; "-q"; "-b"; "main" ] |> ignore
    write (Path.Combine(repo, ".echelon", "r9-governance.json")) "{}\n"
    commitAll repo "T0 bootstrap" |> ignore
    let parent = (git repo [ "rev-parse"; "HEAD" ]).Trim()
    write (Path.Combine(repo, ".echelon", "r9-initialization-checkpoint.json")) $"""{{"authorizedAtCommit":"{parent}"}}"""
    commitAll repo "initialization checkpoint" |> ignore
    repo
let praxisQueue (repo: string) (ids: string list) =
    write (Path.Combine(repo, ".ros", "work", "queue.json")) ($"""{{"items":[{String.Join(",", ids |> List.map (fun i -> $"{{\"title\":\"[{i}] work\"}}"))}]}}""")
let scopeCheckpoint (repo: string) (sha: string) =
    write (Path.Combine(repo, ".echelon", "scope-checkpoint.json")) $"""{{"schema":"indy-init.scope-checkpoint/1","scopeSha256":"{sha}"}}"""
let implementIn (repo: string) = write (Path.Combine(repo, "src", "App.fs")) "module App\n"; commitAll repo "implementation" |> ignore
let sequencingWithScope (repo: string) = [ "check-sequencing"; "--target"; repo; "--scope-manifest"; scopeManifest ]

let noScope = scopedRepo "scope-missing"
implementIn noScope
r9case "check-sequencing-without-scope-manifest-is-unchanged" [ 0 ] [ "no implementation before authorization" ] [ "check-sequencing"; "--target"; noScope ] noScope |> ignore
r9case "check-sequencing-refuses-implementation-without-a-scope-checkpoint" [ 4 ] [ "no scope checkpoint"; "process violation:" ] (sequencingWithScope noScope) noScope |> ignore

let good = scopedRepo "scope-good"
praxisQueue good [ "T10"; "WI-INV-START" ]
scopeCheckpoint good "scope-1"
commitAll good "scope checkpoint" |> ignore
implementIn good
r9case "check-sequencing-passes-with-the-scope-checkpoint" [ 0 ] [ "no implementation before authorization" ] (sequencingWithScope good) good |> ignore

let late = scopedRepo "scope-late"
implementIn late
praxisQueue late [ "T10"; "WI-INV-START" ]
scopeCheckpoint late "scope-1"
commitAll late "scope checkpoint after the fact" |> ignore
r9case "check-sequencing-refuses-implementation-before-the-scope-checkpoint" [ 4 ] [ "process violation:" ] (sequencingWithScope late) late |> ignore

let changed = scopedRepo "scope-changed"
praxisQueue changed [ "T10"; "WI-INV-START" ]
scopeCheckpoint changed "scope-reduced"
commitAll changed "scope checkpoint of a reduced scope" |> ignore
r9case "check-sequencing-refuses-a-scope-hash-mismatch" [ 4 ] [ "the scope changed without an owner record" ] (sequencingWithScope changed) changed |> ignore

let partial = scopedRepo "scope-partial"
praxisQueue partial [ "T10" ]
scopeCheckpoint partial "scope-1"
commitAll partial "scope checkpoint with one work item in Praxis" |> ignore
r9case "check-sequencing-refuses-incomplete-praxis-coverage" [ 4 ] [ "Praxis work-item coverage at the scope checkpoint is incomplete: 1 work item(s) missing (first: WI-INV-START)" ] (sequencingWithScope partial) partial |> ignore

let begun = scopedRepo "scope-begun"
write (Path.Combine(begun, ".ros", "context", "current.json")) """{"workItems":[{"id":"T10","state":"ready"},{"id":"WI-INV-START","state":"ready"}]}"""
scopeCheckpoint begun "scope-1"
commitAll begun "scope checkpoint, work items begun with praxis work begin" |> ignore
implementIn begun
r9case "check-sequencing-counts-work-items-begun-in-praxis" [ 0 ] [ "no implementation before authorization" ] (sequencingWithScope begun) begun |> ignore

File.AppendAllText(Path.Combine(good, ".echelon", "scope-checkpoint.json"), " ")
commitAll good "agent: touch the scope checkpoint" |> ignore
r9case "check-sequencing-refuses-an-edited-scope-checkpoint" [ 4 ] [ "the scope checkpoint is immutable" ] (sequencingWithScope good) good |> ignore

// ---- 4. acceptance-status: four levels, evidence-bound -----------------------------------------------------------

let ev = Path.Combine(src, "docs", "release-gate-repair", "evidence")
let r9Candidate = "39dc037990c8c049ee997fae8384c5bcc9290e2b"
let r9Catalog = Path.Combine(ev, "r9-catalog", "requirement-verification-39dc037.json")
let r9Gate = Path.Combine(ev, "r9-release-gate")
let proposed = Path.Combine(src, "docs", "release-gate-repair", "competition-scope.proposed.json")
let r9Obligations = Path.Combine(work, "v412-obligations.json")
case "dokimos-compile-v4.1.2-plan" [ 0 ] [] [] "dokimos" [ "verification"; "compile"; "--indy-execution-plan"; Path.Combine(src, "requirements-compiled", "v4.1.2", "verification-execution-plan.json"); "--indy-verification-plan"; Path.Combine(src, "requirements-compiled", "v4", "verification-plan.json"); "--out"; r9Obligations ] work |> ignore
let status extra = [ "acceptance-status"; "--candidate"; r9Candidate ] @ extra
r9case "acceptance-rehearsal-009-levels-stay-apart" [ 0 ] [ "catalog: ACCEPTED"; "competitionScope: SCOPE-NOT-APPROVED"; "fullPlan: REFUSED"; "executable 0/541"; "ownerVerdict: NOT-COMPUTED" ]
    (status [ "--catalog-verification"; r9Catalog; "--gate-dir"; r9Gate; "--scope"; proposed; "--obligations"; r9Obligations; "--out"; Path.Combine(work, "status-r9.json") ]) work |> ignore
r9case "acceptance-catalog-is-not-release-acceptance" [ 4 ] [ "required level fullPlan is REFUSED" ] (status [ "--catalog-verification"; r9Catalog; "--gate-dir"; r9Gate; "--require"; "catalog,fullPlan" ]) work |> ignore
r9case "acceptance-missing-catalog-evidence-refused" [ 4 ] [ "missing evidence: no catalog requirement verification"; "required level catalog is REFUSED" ] (status [ "--gate-dir"; r9Gate; "--require"; "catalog" ]) work |> ignore
r9case "acceptance-missing-gate-evidence-refused" [ 4 ] [ "missing evidence: no release-gate verdict" ] (status [ "--catalog-verification"; r9Catalog; "--require"; "fullPlan" ]) work |> ignore
r9case "acceptance-stale-catalog-evidence-refused" [ 4 ] [ "stale evidence: the catalog verification is for" ] [ "acceptance-status"; "--candidate"; "13e1cf84fb1da2cea7a2e90a3b430bbed6536a58"; "--catalog-verification"; r9Catalog; "--require"; "catalog" ] work |> ignore
// A scope approved by an owner record: here a fixture of the catalog requirements only, approved in a scratch
// approvals file. It is accepted as a scope while the full plan stays REFUSED, and says it claims nothing more.
let fixtureScope = Path.Combine(work, "catalog-only-scope.json")
let catalogJson = JsonNode.Parse(File.ReadAllText catalog)
let catalogIds = catalogJson["requirements"].AsArray() |> Seq.map (fun r -> "\"" + r["id"].GetValue<string>() + "\"") |> List.ofSeq
write fixtureScope ($"""{{"schema":"indy-init.competition-scope/1","id":"CS-FIXTURE-CATALOG-ONLY","status":"approved","approval":"ODR-FIXTURE-SCOPE","requirements":[],"catalogRequirements":[{String.Join(",", catalogIds)}]}}""")
let approvals = Path.Combine(work, "approvals.json")
write approvals ($"""{{"changes":[{{"decision_id":"ODR-FIXTURE-SCOPE","action":"approve-scope","scope_sha256":"{sha256 fixtureScope}"}}]}}""")
r9case "acceptance-scoped-verdict-without-full-plan-claim" [ 0 ] [ "competitionScope: ACCEPTED"; "fullPlan: REFUSED" ]
    (status [ "--catalog-verification"; r9Catalog; "--gate-dir"; r9Gate; "--scope"; fixtureScope; "--obligations"; r9Obligations; "--approvals"; approvals; "--out"; Path.Combine(work, "status-scoped.json"); "--require"; "scope" ]) work |> ignore
let scopedPath = Path.Combine(work, "status-scoped.json")
let scoped = if File.Exists scopedPath then JsonNode.Parse(File.ReadAllText scopedPath) |> nonNull else JsonObject() :> JsonNode
let field (n: JsonNode) (a: string) (b: string) = match n[a] with null -> "" | x -> match x[b] with null -> "" | v -> v.ToString()
let claimsFull = field scoped "competitionScope" "claimsFullPlan"
let fullStatus = field scoped "fullPlan" "status"
record "acceptance-scoped-report-claims-no-full-plan" (claimsFull = "false" && fullStatus = "REFUSED") "claimsFullPlan false; fullPlan REFUSED" fullStatus "inspect status-scoped.json" (scoped.ToJsonString())
r9case "acceptance-scoped-requires-full-plan-refused" [ 4 ] [ "required level fullPlan is REFUSED" ] (status [ "--catalog-verification"; r9Catalog; "--gate-dir"; r9Gate; "--scope"; fixtureScope; "--obligations"; r9Obligations; "--approvals"; approvals; "--require"; "scope,fullPlan" ]) work |> ignore
let forged = Path.Combine(work, "forged-approvals.json")
write forged """{"changes":[{"decision_id":"ODR-FIXTURE-SCOPE","action":"approve-scope","scope_sha256":"0000000000000000000000000000000000000000000000000000000000000000"}]}"""
r9case "acceptance-scope-approval-must-match-the-scope-file" [ 4 ] [ "competitionScope: SCOPE-NOT-APPROVED" ] (status [ "--catalog-verification"; r9Catalog; "--gate-dir"; r9Gate; "--scope"; fixtureScope; "--obligations"; r9Obligations; "--approvals"; forged; "--require"; "scope" ]) work |> ignore
let fullScope = Path.Combine(work, "full-scope.json")
write fullScope ((File.ReadAllText proposed).Replace("\"status\": \"proposed\"", "\"status\": \"approved\""))
let fullApprovals = Path.Combine(work, "full-approvals.json")
write fullApprovals ($"""{{"changes":[{{"decision_id":"ODR-RG-01","action":"approve-scope","scope_sha256":"{sha256 fullScope}"}}]}}""")
r9case "acceptance-approved-full-scope-refused-on-rehearsal-009" [ 4 ] [ "competitionScope: REFUSED"; "613 in-scope plan obligation(s) are not verified" ] (status [ "--catalog-verification"; r9Catalog; "--gate-dir"; r9Gate; "--scope"; fullScope; "--obligations"; r9Obligations; "--approvals"; fullApprovals; "--require"; "scope" ]) work |> ignore

// ---- verdict ------------------------------------------------------------------------------------------------------

let failed = cases |> List.filter (snd >> not) |> List.map fst
printfn "\n%d case(s), %d failed%s" cases.Length failed.Length (if failed.IsEmpty then "" else ": " + String.Join(", ", List.rev failed))
File.AppendAllText(tsv, $"# {cases.Length} cases, {failed.Length} failed\n")
exit (if failed.IsEmpty then 0 else 1)
