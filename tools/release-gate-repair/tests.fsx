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

// ---- 2b. the deterministic fixture and the zero-test refusal (ported from #102, integration) ----------------------

let compileObligations (pkg: string) =
    let out = Path.Combine(work, $"obligations-{pkg}.json")
    let code, output =
        run "dokimos" [ "verification"; "compile"; "--indy-execution-plan"; Path.Combine(src, "requirements-compiled", pkg, "verification-execution-plan.json")
                        "--indy-verification-plan"; Path.Combine(src, "requirements-compiled", "v4", "verification-plan.json"); "--out"; out ] work
    if code <> 0 then failwithf "dokimos verification compile %s: %s" pkg output
    out
let obl412, obl414 = compileObligations "v4.1.2", compileObligations "v4.1.4"
let fixtureAt name extra =
    let dir = Path.Combine(work, name)
    r9case $"suite-fixture-{name}" [ 0 ] [ "suite-fixture: PASS" ] ([ "suite-fixture"; "--out"; dir ] @ extra) work |> ignore
    Directory.CreateSymbolicLink(Path.Combine(dir, "node_modules"), nodeModules) |> ignore
    dir
let fx, fxEmpty, fxNoWebkit = fixtureAt "fx" [], fixtureAt "fx-empty" [ "--empty-project"; "Causa.Acceptance" ], fixtureAt "fx-nowebkit" [ "--without-webkit"; "true" ]
r9case "fixture-v4.1.2-runners-refused-msb1008" [ 4 ] [ "SU-OWN-DOTNET cannot execute"; "SU-DOTNET-LIVE cannot execute"; "MSB1008" ] [ "suite-preflight"; "--obligations"; obl412; "--root"; fx; "--require-tests"; "true" ] fx |> ignore
r9case "fixture-v4.1.4-every-suite-lists-tests" [ 0 ] [ "SU-OWN-DOTNET: executable, 4 test(s)"; "SU-BROWSER-WEBKIT: executable, 2 test(s)"; "SU-DOTNET-LIVE: executable, 2 test(s)" ] [ "suite-preflight"; "--obligations"; obl414; "--root"; fx; "--require-tests"; "true" ] fx |> ignore
r9case "fixture-v4.1.4-missing-webkit-project-refused" [ 4 ] [ "SU-BROWSER-WEBKIT cannot execute"; "Project(s) \"webkit\" not found" ] [ "suite-preflight"; "--obligations"; obl414; "--root"; fxNoWebkit ] fxNoWebkit |> ignore
r9case "suite-preflight-zero-selected-tests-refused" [ 4 ] [ "SU-DOTNET-DETERMINISTIC: the command executes but selects no test" ] [ "suite-preflight"; "--obligations"; obl414; "--root"; fxEmpty; "--require-tests"; "true" ] fxEmpty |> ignore
// Control: before implementation (no --require-tests) an empty suite is executable, so preflight can run at T0.
r9case "suite-preflight-zero-tests-allowed-before-implementation" [ 0 ] [ "SU-DOTNET-DETERMINISTIC: executable, 0 test(s)" ] [ "suite-preflight"; "--obligations"; obl414; "--root"; fxEmpty ] fxEmpty |> ignore
r9case "run-suites-release-zero-executed-refused" [ 4 ] [ "suite SU-DOTNET-DETERMINISTIC executed no test (exit 0)" ] [ "run-suites"; "--obligations"; obl414; "--root"; fxEmpty; "--out"; Path.Combine(work, "runs-empty"); "--suites"; "SU-DOTNET-DETERMINISTIC"; "--require-tests"; "true" ] fxEmpty |> ignore
r9case "run-suites-release-fixture-executes" [ 0 ] [ "SU-OWN-DOTNET: exit 0"; "SU-DOTNET-DETERMINISTIC: exit 0" ] [ "run-suites"; "--obligations"; obl414; "--root"; fx; "--out"; Path.Combine(work, "runs-fx"); "--suites"; "SU-OWN-DOTNET,SU-DOTNET-DETERMINISTIC"; "--require-tests"; "true" ] fx |> ignore
// Rehearsal 010 (Fix 2): a centrally managed F# test project without FSharp.Core builds, but xunit skips the assembly
// ("could not find dependent assembly 'FSharp.Core'") and the command exits 0. Refused in every mode, before and after
// implementation, by suite-preflight and by run-suites.
let fxNoCore = fixtureAt "fx-nofsharpcore" [ "--without-fsharp-core"; "Causa.Acceptance" ]
r9case "suite-preflight-skipped-assembly-refused-without-require-tests" [ 4 ] [ "SU-DOTNET-DETERMINISTIC: test discovery skipped Causa.Acceptance (could not find dependent assembly 'FSharp.Core" ] [ "suite-preflight"; "--obligations"; obl414; "--root"; fxNoCore ] fxNoCore |> ignore
r9case "run-suites-development-skipped-assembly-refused" [ 4 ] [ "suite SU-DOTNET-DETERMINISTIC: test assemblies were skipped and ran no test: Causa.Acceptance" ] [ "run-suites"; "--obligations"; obl414; "--root"; fxNoCore; "--out"; Path.Combine(work, "runs-nofsharpcore"); "--suites"; "SU-DOTNET-DETERMINISTIC,SU-OWN-DOTNET" ] fxNoCore |> ignore

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
write (Path.Combine(app, "global.json")) """{ "sdk": { "version": "10.0.402", "rollForward": "disable" } }"""
write (Path.Combine(app, "Causa.slnx")) "<Solution>\n</Solution>\n"
let bootstrapSha = commitAll app "T0 bootstrap"
// A tiny frozen plan: one requirement with one executable obligation, bound in SU-OWN-DOTNET.
let plan = Path.Combine(work, "plan-obligations.json")
write plan """{"schema":"dokimos.verification-obligations/1","planId":"VP-TEST","obligations":[{"id":"SMK-001-O1","requirement":"SMK-001"}],"tests":[{"id":"dotnet:Causa.Domain.Tests.Smoke.SMK-001-O1","suite":"SU-OWN-DOTNET","obligations":["SMK-001-O1"]}],"suites":[{"id":"SU-OWN-DOTNET","runner":"dotnet-test","command":"dotnet test Causa.slnx --filter \"FullyQualifiedName~Causa.Domain.Tests.\""}]}"""
let appSuites = Path.Combine(work, "app-suite-preflight.json")
r9case "suite-preflight-of-the-application-plan" [ 0 ] [ "SU-OWN-DOTNET: executable" ] [ "suite-preflight"; "--obligations"; plan; "--root"; app; "--out"; appSuites ] app |> ignore
let scopeFile = Path.Combine(work, "scope.json")
write scopeFile """{"schema":"indy-init.competition-scope/1","id":"CS-TEST","status":"proposed","requirements":["SMK-001"],"catalogRequirements":["INV-001"]}"""
let spec = Path.Combine(work, "spec-preflight.json")
write spec """{"schema":"indy-init.r9-spec-preflight/1","verdict":"PASS"}"""
let specRefused = Path.Combine(work, "spec-refused.json")
write specRefused """{"schema":"indy-init.r9-spec-preflight/1","verdict":"REFUSED","report":{"note":"PASS appears in the embedded report"}}"""
let notReady = Path.Combine(work, "acceptance-not-ready.log")
write notReady "acceptance preflight: NOT READY\n"
let acceptance = Path.Combine(work, "acceptance-preflight.log")
write acceptance "acceptance preflight: READY\n"
let authorizeWith (suiteReport: string) =
    [ "authorize-implementation"; "--target"; app; "--catalog"; catalog; "--obligations"; plan; "--suite-preflight"; suiteReport
      "--spec-preflight"; spec; "--acceptance-preflight"; acceptance; "--scope"; scopeFile; "--praxis"; praxis ]
let authorize (_: string list) = authorizeWith appSuites
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
let otherRootSuites = Path.Combine(work, "suite-preflight.json")
r9case "authorize-refuses-a-suite-report-of-another-plan-or-root" [ 4 ] [ "5 matrix: the suite-preflight report ran against" ] (authorizeWith otherRootSuites) app |> ignore
let withPreflights specFile accFile =
    authorize [] |> List.mapi (fun i a -> a) |> fun xs ->
        xs |> List.map (fun a -> if a = spec then specFile elif a = acceptance then accFile else a)
r9case "authorize-refuses-a-refused-spec-report" [ 4 ] [ "6 preflight: the specification preflight" ] (withPreflights specRefused acceptance) app |> ignore
r9case "authorize-refuses-not-ready" [ 4 ] [ "6 preflight: conditor acceptance preflight is missing or not READY" ] (withPreflights spec notReady) app |> ignore
let emptyScope = Path.Combine(work, "empty-scope.json")
write emptyScope """{"schema":"indy-init.competition-scope/1","id":"CS-EMPTY","status":"proposed"}"""
r9case "authorize-refuses-an-empty-scope" [ 4 ] [ "the scope names no requirement" ] (authorize [] |> List.map (fun a -> if a = scopeFile then emptyScope else a)) app |> ignore
// #102's scope checkpoint is required before authorization (integration): refused without it and for another scope.
r9case "authorize-refuses-without-a-scope-checkpoint" [ 4 ] [ "7 scope: .echelon/scope-checkpoint.json is missing" ] (authorize []) app |> ignore
write (Path.Combine(app, ".echelon", "scope-checkpoint.json")) """{"schema":"indy-init.scope-checkpoint/1","scopeSha256":"scope-approved"}"""
commitAll app "scope checkpoint (every work item in Praxis)" |> ignore
r9case "authorize-refuses-a-scope-checkpoint-of-another-scope" [ 4 ] [ "7 scope: the scope checkpoint pins scope-approved, not the approved scope scope-other" ] (authorize [] @ [ "--scope-sha256"; "scope-other" ]) app |> ignore
// Review F1 (669fe04): Praxis "[ID] title" items count for exactly that id, and only the approved scope manifest's
// coordination items (its work items without requirements) may link nothing.
let planWith (extra: string) =
    write (Path.Combine(app, ".echelon", "r9-work-plan.json")) ($"""{{"schema":"indy-init.r9-work-plan/1","workItems":[{{"id":"WI-CONTENT-1","requirements":["INV-001","SMK-001"],"obligations":[{String.Join(",", catalogObligations)},"SMK-001-O1"]}}{extra}]}}""")
let scopeManifestFile = Path.Combine(work, "scope-manifest.json")
write scopeManifestFile """{"schema":"indy-init.scope-manifest/1","scopeSha256":"scope-approved","workItems":[{"id":"WI-CONTENT-1","requirements":["INV-001","SMK-001"]},{"id":"T10","requirements":[]},{"id":"T12","requirements":["SMK-001"]},{"id":"T13","requirements":["SMK-001"]},{"id":"WI-CONTENT-2","requirements":["SMK-001"]}]}"""
let withApprovedManifest = authorize [] @ [ "--scope-sha256"; "scope-approved"; "--scope-manifest"; scopeManifestFile ]
// WI-CONTENT-2 (emptied, but the manifest gives it requirements) keeps every case refused, so none writes a checkpoint.
run praxis [ "add"; "[WI-CONTENT-2] emptied" ] app |> ignore
planWith """,{"id":"T10","requirements":[],"obligations":[]},{"id":"WI-CONTENT-2","requirements":[],"obligations":[]}"""
run praxis [ "add"; "[T1] another item" ] app |> ignore
case "authorize-a-[T1]-title-does-not-count-for-T10" [ 4 ] [ "3 work items: T10 is in the work plan but not in Praxis" ] [] "dotnet" ([ "fsi"; r9; "--" ] @ withApprovedManifest) app |> ignore
run praxis [ "add"; "[T10] coordination item" ] app |> ignore
case "authorize-a-[T10]-title-counts-and-the-coordination-item-is-accepted" [ 4 ] [ "4 links: WI-CONTENT-2 names no requirement" ] [ "T10 is in the work plan"; "4 links: T10" ] "dotnet" ([ "fsi"; r9; "--" ] @ withApprovedManifest) app |> ignore
case "authorize-a-coordination-item-needs-the-approved-manifest" [ 4 ] [ "4 links: T10 names no requirement" ] [] "dotnet" ([ "fsi"; r9; "--" ] @ authorize [] @ [ "--scope-sha256"; "scope-approved" ]) app |> ignore
case "authorize-a-manifest-of-another-scope-exempts-nothing" [ 4 ] [ "4 links: T10 names no requirement"; "the scope manifest has scopeSha256 scope-approved, not the approved scope-other" ] [] "dotnet" ([ "fsi"; r9; "--" ] @ authorize [] @ [ "--scope-sha256"; "scope-other"; "--scope-manifest"; scopeManifestFile ]) app |> ignore
run praxis [ "add"; "[T12] requirements, no obligations" ] app |> ignore
run praxis [ "add"; "[T13] obligations, no requirements" ] app |> ignore
planWith """,{"id":"T10","requirements":[],"obligations":[]},{"id":"T12","requirements":["SMK-001"],"obligations":[]},{"id":"T13","requirements":[],"obligations":["SMK-001-O1"]},{"id":"WI-CONTENT-2","requirements":[],"obligations":[]}"""
case "authorize-refuses-requirements-without-obligations" [ 4 ] [ "4 links: T12 names no verification obligation" ] [] "dotnet" ([ "fsi"; r9; "--" ] @ withApprovedManifest) app |> ignore
case "authorize-refuses-obligations-without-requirements" [ 4 ] [ "4 links: T13 names no requirement" ] [] "dotnet" ([ "fsi"; r9; "--" ] @ withApprovedManifest) app |> ignore
case "authorize-refuses-an-emptied-non-coordination-item" [ 4 ] [ "4 links: WI-CONTENT-2 names no requirement"; "4 links: WI-CONTENT-2 names no verification obligation" ] [ "4 links: T10" ] "dotnet" ([ "fsi"; r9; "--" ] @ withApprovedManifest) app |> ignore
planWith ""
r9case "authorize-passes-and-writes-the-checkpoint" [ 0 ] [ "checkpoint"; "1 work item(s)" ] (authorize [] @ [ "--scope-sha256"; "scope-approved" ]) app |> ignore
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
// An edited scope checkpoint is refused by check-sequencing --scope-manifest (the gate always passes it): the case
// check-sequencing-refuses-an-edited-scope-checkpoint below, on a repository checkpointed against a manifest.

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

// Implementation on a branch forked before the checkpoint and merged after it is still unauthorized.
let branchy = Path.Combine(work, "branchy")
Directory.CreateDirectory branchy |> ignore
git branchy [ "init"; "-q"; "-b"; "main" ] |> ignore
write (Path.Combine(branchy, ".echelon", "r9-governance.json")) "{}\n"
let branchyBoot = commitAll branchy "T0 bootstrap"
git branchy [ "checkout"; "-q"; "-b"; "early-work" ] |> ignore
write (Path.Combine(branchy, "src", "A.fs")) "module A\n"
commitAll branchy "implementation on a branch before authorization" |> ignore
git branchy [ "checkout"; "-q"; "main" ] |> ignore
write (Path.Combine(branchy, ".echelon", "r9-initialization-checkpoint.json")) $"""{{"authorizedAtCommit":"{branchyBoot}"}}"""
commitAll branchy "checkpoint" |> ignore
git branchy [ "merge"; "-q"; "--no-ff"; "-m"; "merge early work"; "early-work" ] |> ignore
r9case "check-sequencing-refuses-a-pre-checkpoint-branch-merged-later" [ 4 ] [ "implementation on a branch before authorization" ] [ "check-sequencing"; "--target"; branchy ] branchy |> ignore
let inCheckpoint = Path.Combine(work, "in-checkpoint")
Directory.CreateDirectory inCheckpoint |> ignore
git inCheckpoint [ "init"; "-q"; "-b"; "main" ] |> ignore
write (Path.Combine(inCheckpoint, "src", "Pre.fs")) "module Pre\n"
commitAll inCheckpoint "implementation before governance" |> ignore
write (Path.Combine(inCheckpoint, ".echelon", "r9-governance.json")) "{}\n"
let icBoot = commitAll inCheckpoint "T0 bootstrap"
write (Path.Combine(inCheckpoint, ".echelon", "r9-initialization-checkpoint.json")) $"""{{"authorizedAtCommit":"{icBoot}"}}"""
write (Path.Combine(inCheckpoint, "src", "Smuggled.fs")) "module Smuggled\n"
commitAll inCheckpoint "checkpoint carrying implementation" |> ignore
r9case "check-sequencing-refuses-implementation-before-governance-and-in-the-checkpoint" [ 4 ] [ "implementation before governance"; "checkpoint carrying implementation" ] [ "check-sequencing"; "--target"; inCheckpoint ] inCheckpoint |> ignore

// Review round 6: a scope checkpoint on a side branch (forked before the initialization checkpoint) merged after
// implementation landed on main does not authorize that implementation.
let side = scopedRepo "scope-side-branch"
git side [ "checkout"; "-q"; "-b"; "side"; "HEAD~1" ] |> ignore
praxisQueue side [ "T10"; "WI-INV-START" ]
scopeCheckpoint side "scope-1"
commitAll side "scope checkpoint on a side branch" |> ignore
git side [ "checkout"; "-q"; "main" ] |> ignore
implementIn side
git side [ "merge"; "-q"; "--no-ff"; "-m"; "merge the side scope checkpoint"; "side" ] |> ignore
r9case "check-sequencing-refuses-checkpoints-on-different-branches" [ 4 ] [ "process violation:" ] (sequencingWithScope side) side |> ignore

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
// The catalog Rehearsal 009 verified (its 38 requirements), read from its own verification evidence: the ratified
// successor's INV-039..044 were added after it ended (ODR-R10-02), so they are not part of what it could accept.
let catalogJson = JsonNode.Parse(File.ReadAllText r9Catalog)
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

let emptyApproved = Path.Combine(work, "empty-approved-scope.json")
write emptyApproved """{"schema":"indy-init.competition-scope/1","id":"CS-EMPTY","status":"approved","approval":"ODR-EMPTY","requirements":[],"catalogRequirements":[]}"""
let emptyApprovals = Path.Combine(work, "empty-approvals.json")
write emptyApprovals ($"""{{"changes":[{{"decision_id":"ODR-EMPTY","action":"approve-scope","scope_sha256":"{sha256 emptyApproved}"}}]}}""")
r9case "acceptance-empty-approved-scope-refused" [ 4 ] [ "the scope names no requirement" ] (status [ "--scope"; emptyApproved; "--approvals"; emptyApprovals; "--require"; "scope" ]) work |> ignore
let planOnly = Path.Combine(work, "plan-scope.json")
write planOnly """{"schema":"indy-init.competition-scope/1","id":"CS-PLAN","status":"approved","approval":"ODR-PLAN","requirements":["AR-014"],"catalogRequirements":[]}"""
let planApprovals = Path.Combine(work, "plan-approvals.json")
write planApprovals ($"""{{"changes":[{{"decision_id":"ODR-PLAN","action":"approve-scope","scope_sha256":"{sha256 planOnly}"}}]}}""")
r9case "acceptance-scope-without-plan-or-coverage-refused" [ 4 ] [ "no compiled plan (--obligations)"; "no release-gate coverage" ] (status [ "--scope"; planOnly; "--approvals"; planApprovals; "--require"; "scope" ]) work |> ignore
let staleGate = Path.Combine(work, "stale-gate")
Directory.CreateDirectory staleGate |> ignore
File.WriteAllText(Path.Combine(staleGate, "coverage.json"), (File.ReadAllText(Path.Combine(r9Gate, "coverage.json"))).Replace(r9Candidate, "0000000000000000000000000000000000000000"))
r9case "acceptance-stale-coverage-refused" [ 4 ] [ "stale evidence: the release-gate coverage is for 0000000000000000000000000000000000000000" ] (status [ "--scope"; planOnly; "--approvals"; planApprovals; "--obligations"; r9Obligations; "--gate-dir"; staleGate; "--require"; "scope" ]) work |> ignore
let ownerOther = Path.Combine(work, "owner-verdict-other.md")
write ownerOther "<!-- acceptance-verdict: ACCEPTED -->\ntrusted source sha 1111111111111111111111111111111111111111\n"
r9case "acceptance-owner-verdict-of-another-candidate-refused" [ 4 ] [ "the computed verdict does not name the candidate" ] (status [ "--owner-verdict"; ownerOther; "--require"; "owner" ]) work |> ignore
let ownerHere = Path.Combine(work, "owner-verdict-here.md")
write ownerHere ($"<!-- acceptance-verdict: ACCEPTED -->\ntrusted source sha {r9Candidate}\n")
r9case "acceptance-owner-verdict-bound-to-the-candidate" [ 0 ] [ "ownerVerdict: ACCEPTED" ] (status [ "--owner-verdict"; ownerHere; "--require"; "owner" ]) work |> ignore

// Independent-verifier evidence: a scope holding a verifier-bound obligation (PERSIST-052-O3) is not accepted while the
// computed owner verdict is absent, even when every executable and record part is verified.
let verifierGate = Path.Combine(work, "verifier-gate")
Directory.CreateDirectory verifierGate |> ignore
let coverageNode = JsonNode.Parse(File.ReadAllText(Path.Combine(r9Gate, "coverage.json"))) |> nonNull
for o in coverageNode["obligations"].AsArray() do
    if (o["id"].GetValue<string>()).StartsWith "PERSIST-052-" then o["status"] <- JsonValue.Create("verified")
File.WriteAllText(Path.Combine(verifierGate, "coverage.json"), coverageNode.ToJsonString())
let planNode = JsonNode.Parse(File.ReadAllText r9Obligations) |> nonNull
let persist052 = planNode["obligations"].AsArray() |> Seq.map (fun o -> o["id"].GetValue<string>()) |> Seq.filter (fun i -> i.StartsWith "PERSIST-052-") |> List.ofSeq
File.WriteAllText(Path.Combine(verifierGate, "process-assessment.json"), "{\"obligations\":[" + String.Join(",", persist052 |> List.map (fun i -> "{\"obligation\":\"" + i + "\",\"categories\":[{\"category\":\"provenance\",\"status\":\"verified\"}]}")) + "]}")
let verifierScope = Path.Combine(work, "verifier-scope.json")
write verifierScope """{"schema":"indy-init.competition-scope/1","id":"CS-VERIFIER","status":"approved","approval":"ODR-VERIFIER","requirements":["PERSIST-052"],"catalogRequirements":[]}"""
let verifierApprovals = Path.Combine(work, "verifier-approvals.json")
write verifierApprovals ($"""{{"changes":[{{"decision_id":"ODR-VERIFIER","action":"approve-scope","scope_sha256":"{sha256 verifierScope}"}}]}}""")
r9case "acceptance-verifier-bound-obligation-needs-the-owner-verdict" [ 4 ] [ "PERSIST-052-O3: independent-verifier evidence, judged only by the computed owner verdict (NOT-COMPUTED)" ] (status [ "--scope"; verifierScope; "--approvals"; verifierApprovals; "--obligations"; r9Obligations; "--gate-dir"; verifierGate; "--require"; "scope" ]) work |> ignore
r9case "acceptance-refuses-a-partial-candidate-sha" [ 2 ] [ "must be a full 40-hex commit SHA" ] [ "acceptance-status"; "--candidate"; "39dc037"; "--owner-verdict"; ownerHere; "--require"; "owner" ] work |> ignore
r9case "govern-refuses-a-lock-outside-kickoff" [ 2 ] [ "--lock must be kickoff/<rehearsal>/toolchain.lock.json" ] [ "govern"; "--indy-init"; src; "--target"; work; "--lock"; "../../etc/lock.json" ] work |> ignore
// An evil merge: a merge commit that itself adds implementation before the checkpoint.
let evil = Path.Combine(work, "evil-merge")
Directory.CreateDirectory evil |> ignore
git evil [ "init"; "-q"; "-b"; "main" ] |> ignore
write (Path.Combine(evil, ".echelon", "r9-governance.json")) "{}\n"
commitAll evil "T0 bootstrap" |> ignore
git evil [ "checkout"; "-q"; "-b"; "side" ] |> ignore
write (Path.Combine(evil, "docs", "note.md")) "note\n"
commitAll evil "side: docs only" |> ignore
git evil [ "checkout"; "-q"; "main" ] |> ignore
git evil [ "merge"; "-q"; "--no-ff"; "--no-commit"; "side" ] |> ignore
write (Path.Combine(evil, "src", "Evil.fs")) "module Evil\n"
git evil [ "add"; "-A" ] |> ignore
git evil [ "commit"; "-qm"; "merge side (with implementation)" ] |> ignore
r9case "check-sequencing-inspects-what-a-merge-adds" [ 4 ] [ "merge side (with implementation)" ] [ "check-sequencing"; "--target"; evil ] evil |> ignore


// ---- re-govern (Rehearsal 010, Fix 5; review F5): a later governance record only with the owner's approval -------

// A synthetic Indy-init (tools/r9, verification/, the lock and the approvals file) whose main holds: the T0 template
// W, a corrected template X and Y, which only adds the owner's re-govern approval of X. The trusted checkout is a
// separate clone of that main; the governing checkout (govSrc) is moved to the commit each record names.
let govSrc = Path.Combine(work, "gov-src")
let approvalsRel = "kickoff/rehearsal-007/approvals.causa.json"
let govApprovals = Path.Combine(govSrc, approvalsRel)
let gateYml = Path.Combine(govSrc, "tools", "r9", "verification-gate.yml")
Directory.CreateDirectory(Path.Combine(govSrc, "tools", "r9")) |> ignore
git govSrc [ "init"; "-q"; "-b"; "main" ] |> ignore
for f in Directory.GetFiles(Path.Combine(src, "tools", "r9")) do File.Copy(f, Path.Combine(govSrc, "tools", "r9", Path.GetFileName f), true)
write (Path.Combine(govSrc, "verification", "journeys", "j1.spec.mjs")) "// journey\n"
write (Path.Combine(govSrc, "kickoff", "rehearsal-010", "toolchain.lock.json")) "{}\n"
let approvalsWith (records: string) = write govApprovals ("{\"schema\":\"causa.requirements-approvals/1\",\"approvals\":[" + records + "]}\n")
approvalsWith ""
let govW = commitAll govSrc "T0 template"
File.AppendAllText(gateYml, "# corrected template\n")
let govX = commitAll govSrc "corrected template"
let regovern (id: string) (operator: string) (commit: string) = $"{{\"id\":\"{id}\",\"authority\":{{\"kind\":\"operator\",\"id\":\"{operator}\"}},\"changes\":[{{\"action\":\"re-govern\",\"indyInitCommit\":\"{commit}\"}}]}}"
approvalsWith (regovern "APR-TEST-REGOVERN" "kemiller2002" govX)
let govY = commitAll govSrc "owner approves the re-govern of the corrected template"
let trustedMain = Path.Combine(work, "gov-trusted-main")
let refreshTrusted () =
    if Directory.Exists trustedMain then git trustedMain [ "pull"; "-q"; "--ff-only"; govSrc; "main" ] |> ignore
    else run "git" [ "clone"; "-q"; "--branch"; "main"; govSrc; trustedMain ] work |> ignore
refreshTrusted ()
let gov = Path.Combine(work, "gov-target")
Directory.CreateDirectory gov |> ignore
git gov [ "init"; "-q"; "-b"; "main" ] |> ignore
write (Path.Combine(gov, "ros.json")) "{\"workProtocol\":{\"qualityEvidence\":{}}}\n"
let lock10 = [ "--lock"; "kickoff/rehearsal-010/toolchain.lock.json" ]
let checkGov (operator: string) = [ "check-governance"; "--indy-init"; govSrc; "--target"; gov; "--trusted-operator"; operator; "--trusted-indy-init"; trustedMain ]
let governAt (commit: string) =
    git govSrc [ "checkout"; "-q"; commit ] |> ignore
    run "dotnet" ([ "fsi"; r9; "--"; "govern"; "--indy-init"; govSrc; "--target"; gov ] @ lock10) gov |> snd
git govSrc [ "checkout"; "-q"; govW ] |> ignore
r9case "govern-at-t0" [ 0 ] [ "govern: PASS" ] ([ "govern"; "--indy-init"; govSrc; "--target"; gov ] @ lock10) gov |> ignore
let t0Commit = commitAll gov "T0: governance"
r9case "check-governance-t0-record" [ 0 ] [ "check-governance: PASS" ] (checkGov "kemiller2002") gov |> ignore
git govSrc [ "checkout"; "-q"; govY ] |> ignore
r9case "re-govern-keeps-the-lock" [ 2 ] [ "a re-govern keeps the rehearsal's lock" ] [ "govern"; "--indy-init"; govSrc; "--target"; gov; "--lock"; "kickoff/rehearsal-009/toolchain.lock.json" ] gov |> ignore
r9case "re-govern-writes-the-corrected-template" [ 0 ] [ "govern: PASS" ] ([ "govern"; "--indy-init"; govSrc; "--target"; gov ] @ lock10) gov |> ignore
let regoverned = commitAll gov "re-govern: the corrected template"
r9case "re-govern-refused-without-the-trusted-main" [ 4 ] [ "was changed after T0 (2 commits touch it)" ] [ "check-governance"; "--indy-init"; govSrc; "--target"; gov; "--trusted-operator"; "kemiller2002" ] gov |> ignore
r9case "re-govern-accepted-with-the-owner-approval-on-main" [ 0 ] [ "check-governance: PASS" ] (checkGov "kemiller2002") gov |> ignore
r9case "re-govern-refused-for-another-operator" [ 4 ] [ $"Indy-init {govY} is not approved" ] (checkGov "someone-else") gov |> ignore
// A forged record: the approved commit, but it whitelists an item as completed before governance.
let recordFile = Path.Combine(gov, ".echelon", "r9-governance.json")
write recordFile ((File.ReadAllText recordFile).Replace("\"completedBeforeGovernance\": []", "\"completedBeforeGovernance\": [ \"WI-FORGED\" ]"))
commitAll gov "forged: an item completed before governance" |> ignore
r9case "re-govern-refused-when-it-changes-completed-items" [ 4 ] [ "changes the items completed before governance" ] (checkGov "kemiller2002") gov |> ignore
let resetGov () = git gov [ "reset"; "-q"; "--hard"; regoverned ] |> ignore
resetGov ()
// A forged approval inside the governing commit: an agent's branch adds an operator re-govern record naming its own
// parent. The governing checkout holds it, the trusted main does not, and the commit is not on main.
git govSrc [ "checkout"; "-q"; "-b"; "agent"; govY ] |> ignore
File.AppendAllText(gateYml, "# agent template\n")
let agentT = commitAll govSrc "agent: another template"
approvalsWith (regovern "APR-TEST-REGOVERN" "kemiller2002" govX + "," + regovern "APR-FORGED" "kemiller2002" agentT)
let agentY = commitAll govSrc "agent: forged owner approval"
governAt agentY |> ignore
commitAll gov "re-govern: a forged approval" |> ignore
r9case "re-govern-refused-for-a-forged-approval-in-the-governing-commit" [ 4 ] [ $"Indy-init {agentY} is not on the trusted Indy-init main"; $"Indy-init {agentY} is not approved" ] (checkGov "kemiller2002") gov |> ignore
resetGov ()
// The approved template plus a changed journey, or a changed lock, merged to main: approval of X does not cover it.
let mainChange (name: string) (path: string) =
    git govSrc [ "checkout"; "-q"; "main" ] |> ignore
    git govSrc [ "reset"; "-q"; "--hard"; govY ] |> ignore
    File.AppendAllText(Path.Combine(govSrc, path), "// changed\n")
    let y2 = commitAll govSrc $"main: {name} changed after the approval"
    git trustedMain [ "fetch"; "-q"; govSrc; "main" ] |> ignore
    git trustedMain [ "reset"; "-q"; "--hard"; "FETCH_HEAD" ] |> ignore
    governAt y2 |> ignore
    commitAll gov $"re-govern: {name} changed" |> ignore
    r9case $"re-govern-refused-when-{name}-changed" [ 4 ] [ $"Indy-init {y2} is not approved" ] (checkGov "kemiller2002") gov |> ignore
    resetGov ()
mainChange "verification" "verification/journeys/j1.spec.mjs"
mainChange "lock" "kickoff/rehearsal-010/toolchain.lock.json"

// ---- verdict ------------------------------------------------------------------------------------------------------

let failed = cases |> List.filter (snd >> not) |> List.map fst
printfn "\n%d case(s), %d failed%s" cases.Length failed.Length (if failed.IsEmpty then "" else ": " + String.Join(", ", List.rev failed))
File.AppendAllText(tsv, $"# {cases.Length} cases, {failed.Length} failed\n")
exit (if failed.IsEmpty then 0 else 1)
