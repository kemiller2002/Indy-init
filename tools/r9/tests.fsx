// tests.fsx: end-to-end positive and negative cases for tools/r9/r9.fsx, on the released binaries.
//
//   dotnet fsi tools/r9/tests.fsx -- --indy-init SRC --bin DIR --conditor EXE --work NEW-DIR --evidence DIR
//
// DIR holds praxis 3.25.0, dokimos 0.11.0 and percepta 0.4.0 (installed by `r9.fsx workstation`). Everything is
// written under --work (a synthetic repository, a disposable Conditor home); nothing outside it is touched.
// Each case records its command, exit code and expectation in --evidence/cases.tsv; exit 1 if any case fails.

open System
open System.Diagnostics
open System.IO
open System.Text.Json.Nodes

let argv = fsi.CommandLineArgs |> Array.toList |> List.tail
let opt name = argv |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd |> Option.defaultWith (fun () -> failwithf "missing %s" name)
let src = Path.GetFullPath(opt "--indy-init")
let bin = Path.GetFullPath(opt "--bin")
let conditor = Path.GetFullPath(opt "--conditor")
let work = Directory.CreateDirectory(Path.GetFullPath(opt "--work")).FullName
let evidence = Directory.CreateDirectory(Path.GetFullPath(opt "--evidence")).FullName
let r9 = Path.Combine(src, "tools", "r9", "r9.fsx")
let lock = Path.Combine(src, "kickoff", "rehearsal-009", "toolchain.lock.json")
let praxis, dokimos, percepta = Path.Combine(bin, "praxis"), Path.Combine(bin, "dokimos"), Path.Combine(bin, "percepta")

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

/// Runs one case: the exit code must be one of `expected`, and `mustContain` must appear in the output.
let case (name: string) (expected: int list) (mustContain: string) (exe: string) (args: string list) (cwd: string) =
    let code, output = run exe args cwd
    let ok = List.contains code expected && output.Contains mustContain
    let command = exe + " " + String.Join(" ", args |> List.map (fun a -> if a.Contains " " then $"'{a}'" else a))
    let verdict = if ok then "PASS" else "FAIL"
    let expectedText = String.Join("|", expected)
    File.WriteAllText(Path.Combine(evidence, name + ".log"), $"# cmd: {command}\n# cwd: {cwd}\n{output}\n# exit: {code}\n# expected: {expectedText} containing '{mustContain}'\n# result: {verdict}\n")
    File.AppendAllText(tsv, String.Join("\t", [ name; expectedText; string code; verdict; command ]) + "\n")
    printfn "%-44s expected %-6s got %-3d %s" name (String.Join("|", expected)) code (if ok then "PASS" else "FAIL  <<<")
    cases <- (name, ok) :: cases
    code, output

let r9case name expected mustContain args cwd = case name expected mustContain "dotnet" ([ "fsi"; r9; "--" ] @ args) cwd
let git (repo: string) args = run "git" ([ "-C"; repo; "-c"; "user.name=r9-tests"; "-c"; "user.email=r9@example.invalid"; "-c"; "commit.gpgsign=false" ] @ args) repo |> snd
let write (path: string) (text: string) =
    Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
    File.WriteAllText(path, text)

// ---- Conditor pin and workstation ---------------------------------------------------------------------------------

let cdir = Path.Combine(work, "conditor-bin")
r9case "conditor-install-pinned" [ 0 ] "= lock" [ "install-conditor"; "--lock"; lock; "--dir"; cdir ] work |> ignore
let installed = Path.Combine(cdir, "conditor")
let identity = if File.Exists installed then Text.Encoding.UTF8.GetString(File.ReadAllBytes installed) else ""
// The installed binary must carry exactly the lock's version and source commit, and not the release it replaced.
let lockConditor = (JsonNode.Parse(File.ReadAllText lock)).["conditor"]
let pinnedVersion = lockConditor.["version"].GetValue<string>()
let pinnedIdentity = pinnedVersion + "+" + lockConditor.["commit"].GetValue<string>()
let pinnedLinuxSha = lockConditor.["artifacts"].["linux-x64"].["sha256"].GetValue<string>()
let identityCase = "conditor-identity-" + pinnedVersion + "-not-0.10.19"
cases <- (identityCase, identity.Contains pinnedIdentity && not (identity.Contains "0.10.19+")) :: cases
printfn "%-44s %s" identityCase (if fst cases.Head = identityCase && snd cases.Head then "PASS" else "FAIL  <<<")
let tampered = Path.Combine(work, "tampered.lock.json")
write tampered ((File.ReadAllText lock).Replace(pinnedLinuxSha, String('0', 64)))
r9case "conditor-install-digest-mismatch-refused" [ 4 ] "the lock pins" [ "install-conditor"; "--lock"; tampered; "--dir"; Path.Combine(work, "never") ] work |> ignore
cases <- ("conditor-tampered-writes-nothing", not (Directory.Exists(Path.Combine(work, "never")))) :: cases
let home = Path.Combine(work, "home")
r9case "workstation-fresh-home" [ 0 ] "registration: UNAVAILABLE" [ "workstation"; "--lock"; lock; "--conditor"; conditor; "--home"; home ] work |> ignore
r9case "workstation-repeat-idempotent" [ 0 ] "target id" [ "workstation"; "--lock"; lock; "--conditor"; conditor; "--home"; home ] work |> ignore

// ---- Percepta specification preflight -----------------------------------------------------------------------------

let spec = Path.Combine(work, "spec")
let specOut name = Path.Combine(spec, name + ".json")
Directory.CreateDirectory spec |> ignore
r9case "spec-v412-frozen-package" [ 0 ] "spec-preflight: PASS" [ "spec-preflight"; "--indy-init"; src; "--lock"; lock; "--percepta"; percepta; "--out"; specOut "v412" ] work |> ignore
let v412 name = Path.Combine(src, "requirements-compiled", name)
let explicitInputs (m: string) (i: string) (f: string) (c: string) = [ "--machines"; m; "--invariants"; i; "--interfaces"; f; "--catalog"; c ]
let frozen = explicitInputs (v412 "v4.1.2/state-machine-contracts.json") (v412 "v4/invariants.json") (v412 "v4.1.2/analysis/interface-specifications.json") (Path.Combine(src, "docs", "COMMAND-EVENT-CATALOG.md"))
r9case "spec-missing-input-path" [ 4 ] "does not exist" ([ "spec-preflight"; "--percepta"; percepta; "--out"; specOut "missing" ] @ (frozen |> List.map (fun a -> if a.EndsWith "invariants.json" then a + ".absent" else a))) work |> ignore
r9case "spec-no-inputs" [ 4 ] "no --machines input" [ "spec-preflight"; "--percepta"; percepta; "--out"; specOut "none" ] work |> ignore
let empty = Path.Combine(spec, "empty")
[ "m.json"; "i.json"; "f.json"; "c.md" ] |> List.iter (fun f -> write (Path.Combine(empty, f)) "")
r9case "spec-empty-input-files" [ 4 ] "is empty" ([ "spec-preflight"; "--percepta"; percepta; "--out"; specOut "empty" ] @ explicitInputs (Path.Combine(empty, "m.json")) (Path.Combine(empty, "i.json")) (Path.Combine(empty, "f.json")) (Path.Combine(empty, "c.md"))) work |> ignore
let hollow = Path.Combine(spec, "hollow")
write (Path.Combine(hollow, "m.json")) "{\"machines\": []}"
write (Path.Combine(hollow, "i.json")) "{\"invariants\": []}"
write (Path.Combine(hollow, "f.json")) "{\"interfaces\": []}"
write (Path.Combine(hollow, "c.md")) "# no commands\n"
r9case "spec-well-formed-but-empty" [ 4 ] "an empty specification proves nothing" ([ "spec-preflight"; "--percepta"; percepta; "--out"; specOut "hollow" ] @ explicitInputs (Path.Combine(hollow, "m.json")) (Path.Combine(hollow, "i.json")) (Path.Combine(hollow, "f.json")) (Path.Combine(hollow, "c.md"))) work |> ignore
let synth (name: string) (selfLoopEffect: string) (envelope: string) =
    let d = Path.Combine(spec, name)
    write (Path.Combine(d, "m.json")) ("""{"machines":[{"id":"SM-SMK-PUMP","initial":"Idle","pairs":[
 {"state":"Idle","event":"Start","outcome":"legal","transitions":[{"id":"T-SMK-1","guard":"true","next":"Pumping","effects":["one pump stroke"]}]},
 {"state":"Pumping","event":"Stall","outcome":"legal","transitions":[{"id":"T-SMK-2","guard":"true","next":"Stalled","effects":["log the stall"]}]},
 {"state":"Stalled","event":"Retry","outcome":"legal","transitions":[{"id":"T-SMK-3","guard":"operator retried","next":"Stalled","effects":["__EFFECT__"]}]}]}]}""".Replace("__EFFECT__", selfLoopEffect))
    write (Path.Combine(d, "i.json")) """{"invariants":[{"id":"INV-SMK-01","machines":["SM-SMK-PUMP"],"statement":"Each start is logged; under a stuck valve at most three pump strokes in 30 seconds."}]}"""
    write (Path.Combine(d, "f.json")) ("""{"domainStubs":{"id":"DOMAIN-STUBS","fsharp":"type ValveId = ValveId of Guid\n__ENVELOPE__\ntype Outbox = { Pending: SmokeEnvelope list }"},"interfaces":[{"id":"IF-SMK-KERNEL","fsharp":"","operations":[{"name":"Decide","signature":"Policy -> SmokeEnvelope -> Decision"}]}]}""".Replace("__ENVELOPE__", envelope.Replace("\n", "\\n")))
    write (Path.Combine(d, "c.md")) "Every command is carried inside SmokeEnvelope.\n\n| Command | Primary event(s) |\n| --- | --- |\n| OpenValve | ValveOpened |\n| CloseValve | ValveClosed |\n| Flush | Flushed |\n"
    explicitInputs (Path.Combine(d, "m.json")) (Path.Combine(d, "i.json")) (Path.Combine(d, "f.json")) (Path.Combine(d, "c.md"))
let identityOnly = "type SmokeEnvelope = { CommandId: Guid; Target: ValveId; Version: int64 option }"
let withCommand = "type SmokeCommand =\n    | OpenValve\n    | CloseValve\n    | Flush\ntype SmokeEnvelope = { CommandId: Guid; Command: SmokeCommand }"
r9case "spec-contradictory-invariant" [ 4 ] "rate-bound-vs-unbounded-loop" ([ "spec-preflight"; "--percepta"; percepta; "--out"; specOut "contradiction" ] @ synth "contradiction" "one pump stroke" withCommand) work |> ignore
r9case "spec-incomplete-command-carrier" [ 4 ] "carrier-cannot-discriminate" ([ "spec-preflight"; "--percepta"; percepta; "--out"; specOut "carrier" ] @ synth "carrier" "log the retry" identityOnly) work |> ignore
r9case "spec-synthetic-control-sound" [ 0 ] "spec-preflight: PASS" ([ "spec-preflight"; "--percepta"; percepta; "--out"; specOut "sound" ] @ synth "sound" "log the retry" withCommand) work |> ignore

// ---- Synthetic competition repository: governance, Dokimos gate, Praxis completion --------------------------------

let repo = Path.Combine(work, "repo")
Directory.CreateDirectory repo |> ignore
git repo [ "init"; "-q"; "-b"; "main" ] |> ignore
write (Path.Combine(repo, ".gitignore")) "bin/\nobj/\nTestResults/\n"
write (Path.Combine(repo, "global.json")) (File.ReadAllText(Path.Combine(src, "global.json")))
write (Path.Combine(repo, "src/Smoke/Smoke.fsproj")) "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>\n  <ItemGroup><Compile Include=\"Ledger.fs\" /></ItemGroup>\n</Project>\n"
write (Path.Combine(repo, "src/Smoke/Ledger.fs")) "namespace Smoke\n\n/// Synthetic requirement SMK-001: an append-only ledger whose total is the sum of its entries.\nmodule Ledger =\n    let empty : int list = []\n    let append (amount: int) (ledger: int list) = ledger @ [ amount ]\n    let total (ledger: int list) = List.sum ledger\n"
write (Path.Combine(repo, "tests/Smoke.Tests/Smoke.Tests.fsproj")) "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup><TargetFramework>net10.0</TargetFramework><IsPackable>false</IsPackable></PropertyGroup>\n  <ItemGroup><Compile Include=\"SMK001Tests.fs\" /></ItemGroup>\n  <ItemGroup>\n    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.12.0\" />\n    <PackageReference Include=\"xunit\" Version=\"2.9.2\" />\n    <PackageReference Include=\"xunit.runner.visualstudio\" Version=\"2.8.2\" />\n  </ItemGroup>\n  <ItemGroup><ProjectReference Include=\"../../src/Smoke/Smoke.fsproj\" /></ItemGroup>\n</Project>\n"
let obligations = Path.Combine(work, "obligations.json")
write obligations """{
  "schema": "dokimos.verification-obligations/1", "planId": "VP-R9-FIX", "planDigest": "",
  "namespaceRoots": [ { "namespace": "Smoke.Tests", "directory": "tests/Smoke.Tests" } ],
  "suites": [ { "id": "SU-SMOKE-UNIT", "runner": "dotnet-test", "command": "dotnet test tests/Smoke.Tests", "paths": [ "tests/Smoke.Tests" ], "fixture": "none", "requiredBoundary": [ "Ledger" ] } ],
  "obligations": [ { "id": "SMK-001-O1", "requirement": "SMK-001", "priority": "P0", "criterion": "appending 3 then 4 to an empty ledger yields a total of 7.", "categories": [ "unit" ] } ],
  "tests": [ { "id": "dotnet:Smoke.Tests.SMK001Tests.SMK-001-O1", "suite": "SU-SMOKE-UNIT", "kind": "unit", "obligations": [ "SMK-001-O1" ], "categories": [ "unit" ] } ]
}
"""
case "dokimos-compile-synthetic" [ 0 ] "SMK-001-O1" dokimos [ "verification"; "compile"; "--obligations"; obligations; "--out"; Path.Combine(work, "compiled.json") ] work |> ignore
let compiled = Path.Combine(work, "compiled.json")
case "praxis-init" [ 0 ] "init" praxis [ "init"; "--root"; repo ] repo |> ignore
r9case "govern-applies-authoritative-policy" [ 0 ] "verification-matrix-satisfied" [ "govern"; "--indy-init"; src; "--target"; repo ] repo |> ignore
write (Path.Combine(repo, ".echelon", "verification-scope.json")) "{\n  \"schema\": \"indy-init.r9-verification-scope/1\",\n  \"requirements\": [\"SMK-001\", \"AR-001\"]\n}\n"
git repo [ "add"; "-A" ] |> ignore
git repo [ "commit"; "-qm"; "base: synthetic SMK-001, Praxis, R9 governance" ] |> ignore

let body (assertion: string) (attribute: string) (name: string) (moduleName: string) =
    $"namespace Smoke.Tests\n\nopen Xunit\nopen Smoke\n\nmodule {moduleName} =\n    /// SMK-001-O1: appending 3 then 4 to an empty ledger yields a total of 7.\n    {attribute}\n    let ``{name}`` () : unit =\n        {assertion}\n"
let real = "let ledger = Ledger.empty |> Ledger.append 3 |> Ledger.append 4\n        Assert.Equal(7, Ledger.total ledger)"
let variants =
    [ "pass", body real "[<Fact>]" "SMK-001-O1" "SMK001Tests"
      "skip", body real "[<Fact(Skip = \"R9: deliberately skipped\")>]" "SMK-001-O1" "SMK001Tests"
      "fail", body (real.Replace("Assert.Equal(7,", "Assert.Equal(8,")) "[<Fact>]" "SMK-001-O1" "SMK001Tests"
      "missing", body real "[<Fact>]" "ledger totals entries" "SMK001Tests"
      "placeholder", body "failwith \"DOKIMOS-UNIMPLEMENTED dotnet:Smoke.Tests.SMK001Tests.SMK-001-O1\"" "[<Fact>]" "SMK-001-O1" "SMK001Tests"
      "identity", body real "[<Fact>]" "SMK-001-O1" "SMK001LedgerTests"
      "mixed", body real "[<Fact>]" "SMK-001-O1" "SMK001Tests" + "\n    /// AR-002-O1: the executable half of a mixed obligation.\n    [<Fact>]\n    let ``AR-002-O1`` () : unit =\n        Assert.Equal(3, Ledger.total (Ledger.append 3 Ledger.empty))\n" ]

/// Commits a variant on its own branch from main and runs the plan's suite at it; returns (sha, results manifest).
let prepare (name: string) (text: string) =
    git repo [ "checkout"; "-q"; "-B"; "case-" + name; "main" ] |> ignore
    write (Path.Combine(repo, "tests/Smoke.Tests/SMK001Tests.fs")) text
    git repo [ "add"; "tests/Smoke.Tests/SMK001Tests.fs" ] |> ignore
    git repo [ "commit"; "-qm"; "case " + name ] |> ignore
    let sha = (git repo [ "rev-parse"; "HEAD" ]).Trim()
    let out = Path.Combine(work, "results", name)
    r9case ("run-suites-" + name) [ 0 ] "results" [ "run-suites"; "--obligations"; compiled; "--root"; repo; "--out"; out ] repo |> ignore
    sha, Path.Combine(out, "results.json")

let gateArgs results out extra = [ "gate"; "--mode"; "release"; "--obligations"; compiled; "--root"; repo; "--results"; results; "--dokimos"; dokimos; "--out"; out ] @ extra
let gateOut name = Path.Combine(work, "gate", name)

let prepared = variants |> List.map (fun (n, t) -> n, prepare n t) |> Map.ofList
let shaOf n = fst prepared[n]
let resultsOf n = snd prepared[n]
let at n = git repo [ "checkout"; "-q"; "case-" + n ] |> ignore

at "pass"
r9case "gate-pass-correctly-bound" [ 0 ] "executable: 1/1 verified by Dokimos" (gateArgs (resultsOf "pass") (gateOut "pass") [ "--expect-candidate"; shaOf "pass" ]) repo |> ignore
[ "skip", "skipped"; "fail", "failed"; "missing", "not-generated"; "placeholder", "invalid"; "identity", "not-generated" ]
|> List.iter (fun (n, why) ->
    at n
    r9case ("gate-refuses-" + n) [ 4 ] why (gateArgs (resultsOf n) (gateOut n) [ "--expect-candidate"; shaOf n ]) repo |> ignore)

// Stale evidence: the passing results and matrix, offered for a later commit.
at "pass"
r9case "gate-matrix-for-praxis-WI-VALID" [ 0 ] "PASS" [ "gate"; "--mode"; "release"; "--obligations"; compiled; "--root"; repo; "--results"; resultsOf "pass"; "--dokimos"; dokimos; "--out"; Path.Combine(repo, ".echelon", "verification", "WI-VALID"); "--work-item"; "WI-VALID"; "--scope"; "SMK-001"; "--expect-candidate"; shaOf "pass" ] repo |> ignore
let validMatrix = Path.Combine(repo, ".echelon", "verification", "WI-VALID", "praxis-matrix.json")
git repo [ "checkout"; "-q"; "-B"; "case-later"; "case-pass" ] |> ignore
write (Path.Combine(repo, "src/Smoke/Ledger.fs")) ((File.ReadAllText(Path.Combine(repo, "src/Smoke/Ledger.fs"))) + "// a later commit\n")
git repo [ "commit"; "-qam"; "a later commit" ] |> ignore
let later = (git repo [ "rev-parse"; "HEAD" ]).Trim()
r9case "gate-refuses-stale-sha-results" [ 4 ] "stale evidence" (gateArgs (resultsOf "pass") (gateOut "stale") [ "--expect-candidate"; later ]) repo |> ignore
let freshLater = Path.Combine(work, "results", "later")
r9case "run-suites-later" [ 0 ] "results" [ "run-suites"; "--obligations"; compiled; "--root"; repo; "--out"; freshLater ] repo |> ignore
r9case "gate-refuses-stale-matrix" [ 4 ] "stale or hand-written matrix" (gateArgs (Path.Combine(freshLater, "results.json")) (gateOut "stale-matrix") [ "--expect-candidate"; later; "--matrix"; validMatrix ]) repo |> ignore
r9case "gate-refuses-previous-commit-sha" [ 4 ] "is not the checked-out HEAD" (gateArgs (Path.Combine(freshLater, "results.json")) (gateOut "prev") [ "--expect-candidate"; shaOf "pass" ]) repo |> ignore
r9case "gate-refuses-malformed-sha" [ 4 ] "is not a 40-character SHA" (gateArgs (Path.Combine(freshLater, "results.json")) (gateOut "malformed") [ "--expect-candidate"; "1234abc" ]) repo |> ignore
r9case "gate-refuses-other-candidate-evidence" [ 4 ] "not HEAD" (gateArgs (resultsOf "fail") (gateOut "other") [ "--expect-candidate"; later ]) repo |> ignore
let noSha = Path.Combine(work, "results", "later", "nosha.json")
write noSha ((File.ReadAllText(Path.Combine(freshLater, "results.json"))).Replace(later, ""))
r9case "gate-refuses-missing-sha" [ 4 ] "not HEAD" (gateArgs noSha (gateOut "nosha") [ "--expect-candidate"; later ]) repo |> ignore
let noRuns = Path.Combine(work, "results", "later", "noruns.json")
write noRuns ("{\"schema\":\"dokimos.verification-results/1\",\"candidate\":\"" + later + "\",\"runs\":[]}")
r9case "gate-refuses-missing-execution-evidence" [ 4 ] "no execution evidence" (gateArgs noRuns (gateOut "noruns") [ "--expect-candidate"; later ]) repo |> ignore
let goneTrx = Path.Combine(work, "results", "later", "gone.json")
write goneTrx ("{\"schema\":\"dokimos.verification-results/1\",\"candidate\":\"" + later + "\",\"runs\":[{\"suite\":\"SU-SMOKE-UNIT\",\"format\":\"trx\",\"path\":\"SU-SMOKE-UNIT/absent.trx\",\"sourceSha\":\"" + later + "\"}]}")
r9case "gate-refuses-missing-result-file" [ 4 ] "does not exist" (gateArgs goneTrx (gateOut "gone") [ "--expect-candidate"; later ]) repo |> ignore
write (Path.Combine(repo, "src/Smoke/Ledger.fs")) ((File.ReadAllText(Path.Combine(repo, "src/Smoke/Ledger.fs"))) + "// uncommitted\n")
r9case "gate-refuses-dirty-tree" [ 4 ] "differs from HEAD" (gateArgs (Path.Combine(freshLater, "results.json")) (gateOut "dirty") [ "--expect-candidate"; later ]) repo |> ignore
git repo [ "checkout"; "-q"; "--"; "src/Smoke/Ledger.fs" ] |> ignore
let emptyScope = Path.Combine(work, "empty-scope.json")
write emptyScope "{\"requirements\": []}"
r9case "gate-development-empty-scope-claims-nothing" [ 0 ] "nothing is claimed verified" [ "gate"; "--mode"; "development"; "--scope-file"; emptyScope; "--obligations"; compiled; "--root"; repo; "--results"; Path.Combine(freshLater, "results.json"); "--dokimos"; dokimos; "--out"; gateOut "dev-empty" ] repo |> ignore
at "fail"
r9case "gate-development-scope-refuses-failure" [ 4 ] "failed" [ "gate"; "--mode"; "development"; "--scope-file"; Path.Combine(repo, ".echelon", "verification-scope.json"); "--obligations"; compiled; "--root"; repo; "--results"; resultsOf "fail"; "--dokimos"; dokimos; "--out"; gateOut "dev-fail" ] repo |> ignore

// Praxis completion under the governed policy. No item declares risk metadata.
let ts = "2026-11-03T12:00:00Z"
let startItem id (kind: string) =
    run praxis [ "work"; "capture"; "--id"; id; "--title"; id; "--occurred-at"; ts ] repo |> ignore
    run praxis [ "work"; "backlog-transition"; "--id"; id; "--action"; "ready"; "--occurred-at"; ts ] repo |> ignore
    run praxis [ "work"; "start"; "--id"; id; "--type"; kind; "--occurred-at"; ts ] repo |> ignore
let complete name id expected mustContain (evidenceArgs: string list) =
    case name expected mustContain praxis ([ "work"; "complete"; "--id"; id; "--occurred-at"; ts; "--evidence"; "implementation=src/Smoke/Ledger.fs"; "--evidence"; "tests=tests/Smoke.Tests/SMK001Tests.fs" ] @ evidenceArgs) repo |> ignore

at "pass"
startItem "WI-VALID" "feature"
complete "praxis-valid-matrix-completes" "WI-VALID" [ 0 ] "complete" [ "--evidence"; "verification-matrix=.echelon/verification/WI-VALID/praxis-matrix.json" ]
r9case "check-governance-after-valid-completion" [ 0 ] "PASS" [ "check-governance"; "--indy-init"; src; "--target"; repo ] repo |> ignore

// ---- obligation types: executable (Dokimos), record (verify-process), mixed (both) ---------------------------------
// The type comes only from the plan: the runner of the suite each identity is bound in. Record evidence is judged by
// the existing authoritative verifier, requirements-compiler verify-process, against the frozen v4 plan, on the Praxis
// log recorded by tools/process-evidence/simulate-rehearsal.sh, relabelled to the synthetic candidate.
let fixture = Path.Combine(src, "tools", "fixtures", "process-evidence", "simulated-rehearsal")
let fixtureCandidate = (File.ReadAllText(Path.Combine(fixture, "candidate.txt"))).Trim()
let rc = Path.Combine(src, "tools", "requirements-compiler", "src", "bin", "Release", "net10.0", "requirements-compiler.dll")
let processPlan = v412 "v4/verification-plan.json"
let processPlanDigest = (JsonNode.Parse(File.ReadAllText processPlan)).["planIdentity"].["digest"].GetValue<string>()
let sha256 (path: string) = Security.Cryptography.SHA256.HashData(File.ReadAllBytes path) |> Convert.ToHexString |> fun h -> h.ToLowerInvariant()
let trustedRun = (JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, "attestations.json")))).["runId"].GetValue<string>()
/// The recorded log with its candidate relabelled to `sha` and `mutate` applied to every file.
let processLog (name: string) (sha: string) (mutate: string -> string) =
    let d = Path.Combine(work, "process", name)
    Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories)
    |> Seq.iter (fun f ->
        let relative = Path.GetRelativePath(fixture, f)
        let relabelled = (File.ReadAllText f).Replace(fixtureCandidate, sha)
        // Mutations change only what agents record (the Praxis log and its execution envelopes), never the frozen rules
        // or the CI-observed attestations, so each control fails for the reason it names.
        let recorded = relative = "events.jsonl" || relative.StartsWith "executions"
        write (Path.Combine(d, relative)) (if recorded then mutate relabelled else relabelled))
    d
let processArgs (d: string) (rulesSha: string) =
    [ "--rc"; rc; "--process-plan"; processPlan; "--process-plan-digest"; processPlanDigest; "--process-rules"; Path.Combine(d, "rules.json")
      "--process-rules-sha256"; rulesSha; "--process-attestations"; Path.Combine(d, "attestations.json"); "--process-trusted-run-id"; trustedRun
      "--process-events"; Path.Combine(d, "events.jsonl"); "--process-ancestry"; Path.Combine(d, "ancestry.txt"); "--process-executions"; Path.Combine(d, "executions") ]
let rulesPin = sha256 (Path.Combine(fixture, "rules.json"))
let typedPlan = Path.Combine(work, "typed-obligations.json")
write typedPlan """{
  "schema": "dokimos.verification-obligations/1", "planId": "VP-R9-TYPED", "planDigest": "",
  "namespaceRoots": [ { "namespace": "Smoke.Tests", "directory": "tests/Smoke.Tests" } ],
  "suites": [
    { "id": "SU-SMOKE-UNIT", "runner": "dotnet-test", "command": "dotnet test tests/Smoke.Tests", "paths": [ "tests/Smoke.Tests" ], "fixture": "none", "requiredBoundary": [ "Ledger" ] },
    { "id": "SU-REVIEW-RECORDS", "runner": "evidence-record", "command": "Praxis work evidence --kind independent-review / owner-approval", "paths": [], "fixture": "the run's Praxis event log" } ],
  "obligations": [
    { "id": "SMK-001-O1", "requirement": "SMK-001", "priority": "P0", "criterion": "appending 3 then 4 yields 7.", "categories": [ "unit" ] },
    { "id": "AR-001-O1", "requirement": "AR-001", "priority": "P0", "criterion": "record-only: an independent review of the candidate.", "categories": [ "independent-acceptance" ] },
    { "id": "AR-002-O1", "requirement": "AR-002", "priority": "P0", "criterion": "mixed: a test and an independent review.", "categories": [ "unit", "independent-acceptance" ] },
    { "id": "SMK-002-O1", "requirement": "SMK-002", "priority": "P0", "criterion": "executable only; its test is not written.", "categories": [ "unit" ] } ],
  "tests": [
    { "id": "dotnet:Smoke.Tests.SMK001Tests.SMK-001-O1", "suite": "SU-SMOKE-UNIT", "kind": "unit", "obligations": [ "SMK-001-O1" ], "categories": [ "unit" ] },
    { "id": "praxis:independent-review", "suite": "SU-REVIEW-RECORDS", "kind": "review", "obligations": [ "AR-001-O1", "AR-002-O1" ], "categories": [ "independent-acceptance" ] },
    { "id": "dotnet:Smoke.Tests.SMK001Tests.AR-002-O1", "suite": "SU-SMOKE-UNIT", "kind": "unit", "obligations": [ "AR-002-O1" ], "categories": [ "unit" ] },
    { "id": "dotnet:Smoke.Tests.SMK002Tests.SMK-002-O1", "suite": "SU-SMOKE-UNIT", "kind": "unit", "obligations": [ "SMK-002-O1" ], "categories": [ "unit" ] } ]
}
"""
case "dokimos-compile-typed-plan" [ 0 ] "AR-002-O1" dokimos [ "verification"; "compile"; "--obligations"; typedPlan; "--out"; Path.Combine(work, "typed-compiled.json") ] work |> ignore
let typed = Path.Combine(work, "typed-compiled.json")
let typedGate (name: string) (scope: string) (extra: string list) =
    [ "gate"; "--mode"; "development"; "--scope"; scope; "--work-item"; "WI-" + name.ToUpperInvariant(); "--obligations"; typed; "--root"; repo; "--results"; resultsOf "pass"; "--dokimos"; dokimos; "--out"; gateOut name ] @ extra
let atPass = shaOf "pass"
let validLog = processLog "valid" atPass id
r9case "gate-record-only-valid-records" [ 0 ] "record: 1/1 verified by verify-process" ([ "gate"; "--mode"; "development"; "--scope"; "AR-001"; "--obligations"; typed; "--root"; repo; "--results"; resultsOf "pass"; "--dokimos"; dokimos; "--out"; Path.Combine(repo, ".echelon", "verification", "WI-RECORD"); "--work-item"; "WI-RECORD"; "--expect-candidate"; atPass ] @ processArgs validLog rulesPin) repo |> ignore
startItem "WI-RECORD" "task"
complete "praxis-record-only-item-completes" "WI-RECORD" [ 0 ] "complete" [ "--evidence"; "verification-matrix=.echelon/verification/WI-RECORD/praxis-matrix.json" ]
r9case "check-governance-after-record-only-completion" [ 0 ] "PASS" [ "check-governance"; "--indy-init"; src; "--target"; repo ] repo |> ignore
r9case "gate-record-missing-review-refused" [ 4 ] "MISSING-INDEPENDENT-REVIEW" (typedGate "rec-missing" "AR-001" (processArgs (processLog "missing" atPass (fun t -> t.Split('\n') |> Array.filter (fun l -> not (l.Contains "\"kind\":\"independent-review\"")) |> String.concat "\n")) rulesPin)) repo |> ignore
r9case "gate-record-invalid-authority-refused" [ 4 ] "independent-acceptance unverified" (typedGate "rec-authority" "AR-001" (processArgs (processLog "authority" atPass (fun t -> t.Replace("example/review-agent", "example/impostor-agent"))) rulesPin)) repo |> ignore
r9case "gate-record-unpinned-rules-refused" [ 4 ] "not the frozen 0000" (typedGate "rec-rules" "AR-001" (processArgs validLog (String('0', 64)))) repo |> ignore
r9case "gate-record-wrong-candidate-refused" [ 4 ] "STALE-REVIEW" (typedGate "rec-candidate" "AR-001" (processArgs (processLog "other-candidate" fixtureCandidate id) rulesPin)) repo |> ignore
r9case "gate-record-without-verifier-inputs-refused" [ 4 ] "record-verifier inputs are missing" (typedGate "rec-noinputs" "AR-001" []) repo |> ignore
r9case "gate-mixed-missing-executable-refused" [ 4 ] "not-generated" (typedGate "mixed-noexec" "AR-002" (processArgs validLog rulesPin)) repo |> ignore
r9case "gate-executable-missing-test-never-record" [ 4 ] "not-generated" (typedGate "exec-missing" "SMK-002" []) repo |> ignore
let unknownPlan = Path.Combine(work, "typed-unknown.json")
write unknownPlan ((File.ReadAllText typed).Replace("\"runner\": \"evidence-record\"", "\"runner\": \"manual-checklist\""))
r9case "gate-unclassified-evidence-type-refused" [ 4 ] "gives it no evidence type" [ "gate"; "--mode"; "development"; "--scope"; "AR-001"; "--obligations"; unknownPlan; "--root"; repo; "--results"; resultsOf "pass"; "--dokimos"; dokimos; "--out"; gateOut "unknown" ] repo |> ignore
// Content round trips must be fault-sensitive: a probe that loses the content must be caught by the bound test. The
// synthetic ledger stands in for the content path (a probe drops the appended entry).
let contentPlan = Path.Combine(work, "content-obligations.json")
let contentNode = JsonNode.Parse(File.ReadAllText compiled)
contentNode.["obligations"].AsArray() |> Seq.iter (fun o -> o.["categories"] <- JsonArray(JsonValue.Create "content-round-trip"))
write contentPlan (contentNode.ToJsonString())
let probeSpec = Path.Combine(work, "probes.json")
let dotnetExe = (run "sh" [ "-c"; "command -v dotnet" ] work |> snd).Trim()
write probeSpec ("""{ "schema": "dokimos.fault-probes/1", "probes": [ { "id": "P-CONTENT-DROPPED", "obligations": [ "SMK-001-O1" ],
  "file": "src/Smoke/Ledger.fs", "find": "ledger @ [ amount ]", "replace": "ledger",
  "build": [ "DOTNET", "build", "tests/Smoke.Tests" ], "command": [ "DOTNET", "test", "tests/Smoke.Tests", "--no-build" ] } ] }""".Replace("DOTNET", dotnetExe))
let probeResults = Path.Combine(work, "probe-results.json")
case "dokimos-probe-content-dropped-detected" [ 0 ] "detected" dokimos [ "verification"; "probe"; "--spec"; probeSpec; "--sha"; atPass; "--root"; repo; "--out"; probeResults ] repo |> ignore
let contentGate (name: string) (extra: string list) = [ "gate"; "--mode"; "development"; "--scope"; "SMK-001"; "--work-item"; "WI-" + name.ToUpperInvariant(); "--obligations"; contentPlan; "--root"; repo; "--results"; resultsOf "pass"; "--dokimos"; dokimos; "--out"; gateOut name; "--expect-candidate"; atPass; "--require-fault-sensitive"; "content-round-trip" ] @ extra
r9case "gate-content-without-probe-refused" [ 4 ] "must be fault-sensitive" (contentGate "content-noprobe" []) repo |> ignore
r9case "gate-content-with-detected-probe-passes" [ 0 ] "executable: 1/1 verified by Dokimos" (contentGate "content-probe" [ "--probes"; probeResults ]) repo |> ignore
r9case "gate-development-merge-does-not-rejudge-records" [ 0 ] "not re-judged on a development merge" [ "gate"; "--mode"; "development"; "--scope"; "SMK-001,AR-001"; "--obligations"; typed; "--root"; repo; "--results"; resultsOf "pass"; "--dokimos"; dokimos; "--out"; gateOut "dev-merge"; "--expect-candidate"; atPass ] repo |> ignore
at "mixed"
r9case "gate-mixed-both-verified" [ 0 ] "mixed: 1" ([ "gate"; "--mode"; "development"; "--scope"; "AR-002"; "--obligations"; typed; "--root"; repo; "--results"; resultsOf "mixed"; "--dokimos"; dokimos; "--out"; gateOut "mixed"; "--work-item"; "WI-MIXED"; "--expect-candidate"; shaOf "mixed" ] @ processArgs (processLog "mixed" (shaOf "mixed") id) rulesPin) repo |> ignore
r9case "gate-mixed-missing-record-refused" [ 4 ] "MISSING-INDEPENDENT-REVIEW" ([ "gate"; "--mode"; "development"; "--scope"; "AR-002"; "--work-item"; "WI-MIXED-NORECORD"; "--obligations"; typed; "--root"; repo; "--results"; resultsOf "mixed"; "--dokimos"; dokimos; "--out"; gateOut "mixed-norecord" ] @ processArgs (processLog "mixed-norecord" (shaOf "mixed") (fun t -> t.Split('\n') |> Array.filter (fun l -> not (l.Contains "\"kind\":\"independent-review\"")) |> String.concat "\n")) rulesPin) repo |> ignore
at "pass"
startItem "WI-NOMATRIX" "feature"
complete "praxis-missing-matrix-refused" "WI-NOMATRIX" [ 3 ] "no verification-matrix evidence" []
startItem "WI-NORISK" "research"
complete "praxis-no-risk-metadata-research-refused" "WI-NORISK" [ 3 ] "verification-matrix-satisfied" []
at "fail"
r9case "gate-matrix-for-praxis-WI-FAILED" [ 4 ] "failed" [ "gate"; "--mode"; "release"; "--obligations"; compiled; "--root"; repo; "--results"; resultsOf "fail"; "--dokimos"; dokimos; "--out"; Path.Combine(repo, ".echelon", "verification", "WI-FAILED"); "--work-item"; "WI-FAILED"; "--scope"; "SMK-001"; "--expect-candidate"; shaOf "fail" ] repo |> ignore
startItem "WI-FAILED" "feature"
complete "praxis-failed-matrix-refused" "WI-FAILED" [ 3 ] "not met" [ "--evidence"; "verification-matrix=.echelon/verification/WI-FAILED/praxis-matrix.json" ]
at "pass"
let forged = Path.Combine(repo, ".echelon", "verification", "WI-FORGED", "praxis-matrix.json")
let copied = Path.Combine(repo, ".echelon", "verification", "WI-COPIED", "praxis-matrix.json")
write copied (File.ReadAllText validMatrix)
startItem "WI-COPIED" "feature"
complete "praxis-refuses-matrix-of-another-item" "WI-COPIED" [ 3 ] "is for work item 'WI-VALID'" [ "--evidence"; "verification-matrix=.echelon/verification/WI-COPIED/praxis-matrix.json" ]
write forged ((File.ReadAllText validMatrix).Replace("dotnet:Smoke.Tests.SMK001Tests.SMK-001-O1", "dotnet:Smoke.Tests.SMK001Tests.SMK-999-O1").Replace("WI-VALID", "WI-FORGED"))
r9case "gate-refuses-incorrect-obligation-identity" [ 4 ] "not a bound identity" (gateArgs (resultsOf "pass") (gateOut "forged") [ "--expect-candidate"; shaOf "pass"; "--matrix"; forged ]) repo |> ignore
startItem "WI-FORGED" "feature"
complete "praxis-accepts-forged-matrix-locally" "WI-FORGED" [ 0 ] "complete" [ "--evidence"; "verification-matrix=.echelon/verification/WI-FORGED/praxis-matrix.json" ]
r9case "check-governance-blocks-forged-identity" [ 4 ] "matches no PASS gate verdict" [ "check-governance"; "--indy-init"; src; "--target"; repo ] repo |> ignore
// An agent strips the required facet to complete without verification.
let ros = Path.Combine(repo, "ros.json")
let node = JsonNode.Parse(File.ReadAllText ros)
let qe: JsonNode = node["workProtocol"]["qualityEvidence"]
qe["requiredFacets"] <- JsonArray()
File.WriteAllText(ros, node.ToJsonString())
git repo [ "commit"; "-qm"; "agent: drop the required verification facet"; "--"; "ros.json" ] |> ignore
startItem "WI-STRIPPED" "feature"
complete "praxis-completes-after-facet-removal" "WI-STRIPPED" [ 0 ] "complete" []
r9case "check-governance-blocks-facet-removal" [ 4 ] "differs from Indy-init tools/r9/governance.json" [ "check-governance"; "--indy-init"; src; "--target"; repo ] repo |> ignore
let shrunk = Path.Combine(work, "base-scope.json")
write shrunk "{\"requirements\": [\"SMK-001\", \"SMK-002\"]}"
r9case "check-governance-scope-only-grows" [ 4 ] "may only grow" [ "check-governance"; "--indy-init"; src; "--target"; repo; "--base-scope"; shrunk ] repo |> ignore

// The release gate over the real frozen v4.1.2 plan, on a repository with no application code: the plan classifies
// 541 obligations as executable and 119 as record-bound (47 of them mixed); nothing is verified and nothing is deferred.
let realPlan = Path.Combine(work, "v412-obligations.json")
case "dokimos-compile-frozen-v412" [ 0 ] "VP-" dokimos [ "verification"; "compile"; "--indy-execution-plan"; v412 "v4.1.2/verification-execution-plan.json"; "--indy-verification-plan"; v412 "v4/verification-plan.json"; "--out"; realPlan ] work |> ignore
let blank = Path.Combine(work, "blank")
Directory.CreateDirectory blank |> ignore
git blank [ "init"; "-q"; "-b"; "main" ] |> ignore
write (Path.Combine(blank, "README.md")) "no application code\n"
git blank [ "add"; "-A" ] |> ignore
git blank [ "commit"; "-qm"; "blank" ] |> ignore
let blankSha = (git blank [ "rev-parse"; "HEAD" ]).Trim()
let blankResults = Path.Combine(work, "blank-results.json")
write blankResults ("{\"schema\":\"dokimos.verification-results/1\",\"candidate\":\"" + blankSha + "\",\"runs\":[]}")
r9case "gate-release-real-v412-plan-blank-refused" [ 4 ] "executable: 0/541 verified by Dokimos; record: 0/119 verified by verify-process; mixed: 47" [ "gate"; "--mode"; "release"; "--obligations"; realPlan; "--root"; blank; "--results"; blankResults; "--dokimos"; dokimos; "--out"; gateOut "real-v412"; "--expect-candidate"; blankSha ] blank |> ignore

// ---- verdict ------------------------------------------------------------------------------------------------------

let failed = cases |> List.filter (snd >> not) |> List.map fst
printfn "\n%d case(s), %d failed%s" cases.Length failed.Length (if failed.IsEmpty then "" else ": " + String.Join(", ", List.rev failed))
File.AppendAllText(tsv, $"# {cases.Length} cases, {failed.Length} failed\n")
exit (if failed.IsEmpty then 0 else 1)
