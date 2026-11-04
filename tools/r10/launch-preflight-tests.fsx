// launch-preflight-tests.fsx: launch-preflight's negative controls. A synthetic Indy-init (with its origin), a
// published competition repository and a stand-in for gh that serves one live-provider run satisfy every check
// (positive control: READY); then one mutation per refusal, each REFUSED by the check it names. Synthetic fixtures
// only: nothing real is read, written or fetched.
//
//   dotnet fsi tools/r10/launch-preflight-tests.fsx -- --indy-init DIR --work NEW-DIR [--evidence DIR]

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let opt (name: string) = args |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let src = opt "--indy-init" |> Option.defaultWith (fun () -> failwith "missing --indy-init") |> Path.GetFullPath
let work = opt "--work" |> Option.defaultWith (fun () -> failwith "missing --work") |> Path.GetFullPath
let evidence = opt "--evidence"
let preflight = Path.Combine(src, "tools", "r10", "launch-preflight.fsx")

let run (dir: string) (exe: string) (a: string list) =
    let info = ProcessStartInfo(exe, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    a |> List.iter info.ArgumentList.Add
    use p = Process.Start info
    let out = p.StandardOutput.ReadToEnd()
    let err = p.StandardError.ReadToEnd()
    p.WaitForExit()
    p.ExitCode, out + err
let gitAs (who: string) dir a = run dir "git" ([ "-c"; $"user.name={who}"; "-c"; $"user.email={who}@example.invalid"; "-c"; "commit.gpgsign=false" ] @ a)
let git dir a = gitAs "owner" dir a
let write (file: string) (text: string) = Directory.CreateDirectory(Path.GetDirectoryName file) |> ignore; File.WriteAllText(file, text)
let sha (text: string) = (SHA256.HashData(Text.Encoding.UTF8.GetBytes text) |> Convert.ToHexString).ToLowerInvariant()
let replaceIn (file: string) (a: string) (b: string) =
    let t = File.ReadAllText file
    if not (t.Contains a) then failwithf "%s: missing %s" file a
    File.WriteAllText(file, t.Replace(a, b))

type Fixture = { Indy: string; Origin: string; Competition: string; Run: string; Gh: string }
let commitPush (f: Fixture) (message: string) =
    git f.Indy [ "add"; "-A" ] |> ignore
    git f.Indy [ "commit"; "-qm"; message ] |> ignore
    git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore

let testRepository = "{\"owner\":\"kemiller2002\",\"repo\":\"causa-acceptance-r10\",\"branch\":\"causa-acceptance\",\"isolated\":true}"

/// The positive fixture, then `edit` (a mutation), then launch-preflight on it.
let fixture (name: string) (edit: Fixture -> unit) (extraArgs: Fixture -> string list option) =
    let dir = Path.Combine(work, name)
    let f = { Indy = Path.Combine(dir, "Indy-init"); Origin = Path.Combine(dir, "origin.git"); Competition = Path.Combine(dir, "competition.git"); Run = Path.Combine(dir, "run"); Gh = Path.Combine(dir, "gh") }
    let files =
        [ "kickoff/rehearsal-010/catalog-verification-contract.json", "{\"contract\":\"catalog\"}\n"
          "kickoff/rehearsal-010/design-contract.json", "{\"contract\":\"design\"}\n"
          "kickoff/rehearsal-010/deadline-policy.json", "{\"windowMinutes\":420}\n"
          "verification/journeys/bindings.json", "{\"journeys\":[\"J1\",\"J2\",\"J3\"]}\n"
          "kickoff/rehearsal-010/process-evidence-rules.json", "{\"repository\":\"indy-init-rehearsal-010\"}\n"
          "requirements-compiled/v4.1.4/package-manifest.json", "{\"package\":\"4.1.4\"}\n"
          "docs/rehearsal-010-readiness/scope-manifest.json", "{\"scopeSha256\":\"scope-final\",\"counts\":{\"requirements\":205}}\n"
          "tools/r10/adversarial.fsx", "// fixture\n    [ { Id = \"C01\"; Title = \"a\" }\n      { Id = \"C02\"; Title = \"b\" } ]\n" ]
    files |> List.iter (fun (rel, text) -> write (Path.Combine(f.Indy, rel)) text)
    let digest rel = files |> List.find (fst >> (=) rel) |> snd |> sha
    let pin rel = $"{{\"path\":\"{rel}\",\"sha256\":\"{digest rel}\"}}"
    let requests = [ "ODR-RG-03", ""; "ODR-R10-02", ",\"launchable\":[\"A\",\"B\"]"; "ODR-R10-03", ""; "ODR-R10-05", ""; "ODR-R10-09", ""; "ODR-R10-01", "" ]
    write (Path.Combine(f.Indy, "kickoff/rehearsal-010/proposals/owner-decision-requests.json"))
        ("{\"requests\":[" + String.Join(",", requests |> List.map (fun (r, extra) -> $"{{\"id\":\"{r}\"{extra}}}")) + "]}\n")
    let change (id: string) (option: string) (pins: string list) = $"{{\"decision_id\":\"{id}\",\"action\":\"decide\",\"option\":\"{option}\",\"pins\":[{String.Join(',', pins)}]}}"
    let field (path: string) (fieldName: string) (value: string) = $"{{\"path\":\"{path}\",\"field\":\"{fieldName}\",\"value\":{Text.Json.JsonSerializer.Serialize value},\"sha256\":\"x\"}}"
    let changes =
        [ "{\"decision_id\":\"ODR-V414-01\",\"action\":\"select\",\"option\":\"A\"}"
          "{\"decision_id\":\"ODR-V414-02\",\"action\":\"select\",\"option\":\"A\"}"
          "{\"decision_id\":\"ODR-V414-03\",\"action\":\"approve-package\",\"package\":\"requirements-compiled/v4.1.4\"}"
          change "ODR-RG-03" "A" []
          change "ODR-R10-02" "B" []
          change "ODR-R10-03" "A" [ pin "kickoff/rehearsal-010/catalog-verification-contract.json" ]
          change "ODR-R10-05" "A" [ pin "kickoff/rehearsal-010/design-contract.json" ]
          change "ODR-R10-09" "A" [ field "kickoff/rehearsal-010/toolchain.lock.json" "testRepository" testRepository ]
          change "ODR-R10-01" "A" [ field "docs/rehearsal-010-readiness/scope-manifest.json" "scopeSha256" "\"scope-final\"" ] ]
    write (Path.Combine(f.Indy, "kickoff/rehearsal-007/approvals.causa.json"))
        ("{\"approvals\":[{\"id\":\"APR-R10-OWNER-1\",\"authority\":{\"kind\":\"operator\",\"id\":\"kemiller2002\"},\"changes\":[" + String.Join(",", changes) + "]}]}\n")
    write (Path.Combine(f.Indy, "docs/rehearsal-010-readiness/evidence/adversarial/cases.tsv")) "case\ttitle\tverdict\nC01\tzero tests\tVALIDATED\nC02\tgate\tVALIDATED\n"
    run dir "git" [ "init"; "-q"; "--bare"; "-b"; "main"; f.Origin ] |> ignore
    git f.Indy [ "init"; "-q"; "-b"; "main" ] |> ignore
    git f.Indy [ "remote"; "add"; "origin"; f.Origin ] |> ignore
    commitPush f "fixture: contracts and owner records"
    git f.Indy [ "tag"; "indy-init-requirements-v4.1.4" ] |> ignore
    git f.Indy [ "push"; "-q"; "origin"; "indy-init-requirements-v4.1.4" ] |> ignore
    let _, tagCommit = git f.Indy [ "rev-parse"; "HEAD" ]
    write (Path.Combine(f.Indy, "kickoff/rehearsal-010/toolchain.lock.json"))
        ($"""{{"status":"OWNER-PINNED","requirementsPackage":{{"path":"requirements-compiled/v4.1.4","packageVersion":"4.1.4","tag":"indy-init-requirements-v4.1.4","tagCommit":"{tagCommit.Trim()}","manifestSha256":"{digest "requirements-compiled/v4.1.4/package-manifest.json"}"}},
  "scope":{{"manifest":"docs/rehearsal-010-readiness/scope-manifest.json","scopeSha256":"scope-final"}},
  "catalogVerification":{{"contract":"kickoff/rehearsal-010/catalog-verification-contract.json","contractSha256":"{digest "kickoff/rehearsal-010/catalog-verification-contract.json"}"}},
  "designContract":{{"contract":"kickoff/rehearsal-010/design-contract.json","contractSha256":"{digest "kickoff/rehearsal-010/design-contract.json"}"}},
  "deadlinePolicy":{{"path":"kickoff/rehearsal-010/deadline-policy.json","sha256":"{digest "kickoff/rehearsal-010/deadline-policy.json"}"}},
  "journeys":{{"bindings":"verification/journeys/bindings.json","bindingsSha256":"{digest "verification/journeys/bindings.json"}"}},
  "processEvidence":{{"rules":"kickoff/rehearsal-010/process-evidence-rules.json","rulesSha256":"{digest "kickoff/rehearsal-010/process-evidence-rules.json"}"}},
  "resolvedSet":{{"expectedTools":{{"dokimos":"0.11.1"}}}},
  "testRepository":""" + testRepository + "}\n")
    commitPush f "owner: the Rehearsal 010 lock"
    // The live-provider run as gh would serve it: its metadata and its artifact.
    write (Path.Combine(f.Run, "run.json")) "{\"workflowName\":\"Live-provider preflight (Rehearsal 010)\",\"headBranch\":\"main\",\"event\":\"workflow_dispatch\",\"conclusion\":\"success\",\"headSha\":\"abc\"}\n"
    let row j e status = $"{{\"journey\":\"{j}\",\"engine\":\"{e}\",\"status\":\"{status}\"}}"
    let rows = [ for j in [ "J1"; "J2"; "J3" ] do for e in [ "chromium"; "webkit" ] -> row j e "passed" ]
    write (Path.Combine(f.Run, "artifact", "live-provider-preflight-abc", "journey-results.json"))
        ("{\"schema\":\"indy-init.journey-results/1\",\"candidate\":\"fixture-abc\",\"provider\":{\"mode\":\"github\",\"baseUrl\":\"https://api.github.com\"},\"results\":[" + String.Join(",", rows) + "]}\n")
    write f.Gh ($"#!/bin/sh\n# gh stand-in (fixture): run view prints the run, run download copies its artifact\ncase \"$1 $2\" in\n  \"run view\") cat '{f.Run}/run.json' ;;\n  \"run download\") while [ \"$1\" != \"--dir\" ]; do shift; done; cp -r '{f.Run}/artifact/.' \"$2\" ;;\n  *) exit 2 ;;\nesac\n")
    run dir "chmod" [ "+x"; f.Gh ] |> ignore
    // The competition repository as published: its initial commit only.
    let compWork = Path.Combine(dir, "competition-work")
    run dir "git" [ "init"; "-q"; "--bare"; "-b"; "main"; f.Competition ] |> ignore
    git dir [ "init"; "-q"; "-b"; "main"; compWork ] |> ignore
    write (Path.Combine(compWork, "README.md")) "competition\n"
    git compWork [ "add"; "-A" ] |> ignore
    git compWork [ "commit"; "-qm"; "initial" ] |> ignore
    git compWork [ "push"; "-q"; f.Competition; "main" ] |> ignore
    edit f
    let defaults = [ "--indy-init"; f.Indy; "--competition"; f.Competition; "--live-run"; "4242"; "--gh"; f.Gh ]
    let a = extraArgs f |> Option.defaultValue defaults
    let code, out = run work "dotnet" ([ "fsi"; preflight; "--" ] @ a)
    evidence |> Option.iter (fun d -> write (Path.Combine(d, name + ".log")) out)
    code, out

type Case = { Name: string; Expect: string; Check: string; Edit: Fixture -> unit; Args: Fixture -> string list option }
let case name check edit = { Name = name; Expect = "REFUSED"; Check = check; Edit = edit; Args = fun _ -> None }
let approvals f = Path.Combine(f.Indy, "kickoff/rehearsal-007/approvals.causa.json")
let lockFile f = Path.Combine(f.Indy, "kickoff/rehearsal-010/toolchain.lock.json")
let results f = Path.Combine(f.Run, "artifact", "live-provider-preflight-abc", "journey-results.json")
let cases =
    [ { Name = "positive-control"; Expect = "READY"; Check = ""; Edit = ignore; Args = fun _ -> None }
      case "missing-owner-approval" "LP-05" (fun f -> replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\"" "\"decision_id\":\"ODR-R10-XX\""; commitPush f "edit")
      case "approval-by-someone-else" "LP-05" (fun f -> replaceIn (approvals f) "\"id\":\"kemiller2002\"" "\"id\":\"claude\""; commitPush f "edit")
      case "approval-committed-by-an-agent" "LP-05" (fun f ->
          // the same records, first committed by an agent (a later owner commit does not launder them)
          let text = File.ReadAllText(approvals f)
          File.WriteAllText(approvals f, "{\"approvals\":[]}\n"); commitPush f "reset"
          File.WriteAllText(approvals f, text.Replace("APR-R10-OWNER-1", "APR-R10-OWNER-2"))
          git f.Indy [ "add"; "-A" ] |> ignore
          git f.Indy [ "commit"; "-qm"; "owner decisions"; "-m"; "Claude-Session: https://claude.ai/code/x" ] |> ignore
          git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore)
      case "decision-recorded-as-do-not-approve" "LP-05" (fun f -> replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"B\""; commitPush f "edit")
      case "altered-design-contract" "LP-03" (fun f -> write (Path.Combine(f.Indy, "kickoff/rehearsal-010/design-contract.json")) "{\"contract\":\"weakened\"}\n"; commitPush f "edit")
      case "altered-scope" "LP-04" (fun f -> replaceIn (Path.Combine(f.Indy, "docs/rehearsal-010-readiness/scope-manifest.json")) "scope-final" "scope-other"; commitPush f "edit")
      case "uncommitted-change" "LP-00" (fun f -> write (Path.Combine(f.Indy, "kickoff/rehearsal-010/design-contract.json")) "{\"contract\":\"local\"}\n")
      case "checkout-is-not-origin-main" "LP-00" (fun f -> write (Path.Combine(f.Indy, "NOTE.md")) "local\n"; git f.Indy [ "add"; "-A" ] |> ignore; git f.Indy [ "commit"; "-qm"; "local only" ] |> ignore)
      case "lock-still-proposed" "LP-01" (fun f -> replaceIn (lockFile f) "OWNER-PINNED" "PROPOSED: not an owner pin"; commitPush f "edit")
      case "v4.1.4-not-tagged-on-origin" "LP-02" (fun f -> git f.Indy [ "push"; "-q"; "origin"; ":refs/tags/indy-init-requirements-v4.1.4" ] |> ignore)
      case "dokimos-0.11.0" "LP-06" (fun f -> replaceIn (lockFile f) "\"dokimos\":\"0.11.1\"" "\"dokimos\":\"0.11.0\""; commitPush f "edit")
      case "test-repository-not-isolated" "LP-07" (fun f -> replaceIn (lockFile f) "\"isolated\":true" "\"isolated\":false"; commitPush f "edit")
      case "test-repository-not-the-approved-one" "LP-07" (fun f -> replaceIn (lockFile f) "\"repo\":\"causa-acceptance-r10\"" "\"repo\":\"causa-acceptance-other\""; commitPush f "edit")
      { case "absent-credential-no-live-run" "LP-08" ignore with
          Args = fun f -> Some [ "--indy-init"; f.Indy; "--competition"; f.Competition; "--gh"; f.Gh ] }
      case "live-run-against-a-double" "LP-08" (fun f -> replaceIn (results f) "https://api.github.com" "http://127.0.0.1:4610")
      case "live-run-on-a-branch" "LP-08" (fun f -> replaceIn (Path.Combine(f.Run, "run.json")) "\"headBranch\":\"main\"" "\"headBranch\":\"agent-branch\"")
      case "live-run-failed" "LP-08" (fun f -> replaceIn (Path.Combine(f.Run, "run.json")) "\"conclusion\":\"success\"" "\"conclusion\":\"failure\"")
      case "failed-gate-journey" "LP-08" (fun f -> replaceIn (results f) "{\"journey\":\"J2\",\"engine\":\"webkit\",\"status\":\"passed\"}" "{\"journey\":\"J2\",\"engine\":\"webkit\",\"status\":\"failed\"}")
      case "duplicate-journey-row-hides-a-failure" "LP-08" (fun f -> replaceIn (results f) "{\"journey\":\"J3\",\"engine\":\"webkit\",\"status\":\"passed\"}" "{\"journey\":\"J3\",\"engine\":\"webkit\",\"status\":\"passed\"},{\"journey\":\"J3\",\"engine\":\"webkit\",\"status\":\"failed\"}")
      case "adversarial-case-failed" "LP-09" (fun f -> replaceIn (Path.Combine(f.Indy, "docs/rehearsal-010-readiness/evidence/adversarial/cases.tsv")) "C02\tgate\tVALIDATED" "C02\tgate\tFAILED"; commitPush f "edit")
      case "adversarial-case-missing" "LP-09" (fun f -> replaceIn (Path.Combine(f.Indy, "docs/rehearsal-010-readiness/evidence/adversarial/cases.tsv")) "C02\tgate\tVALIDATED\n" ""; commitPush f "edit")
      case "clock-already-started" "LP-10" (fun f ->
          let w = Path.Combine(Path.GetDirectoryName f.Indy, "competition-work")
          write (Path.Combine(w, "evidence", "t0", "t0.json")) "{\"t0\":\"2026-11-05T00:00:00Z\"}\n"
          git w [ "add"; "-A" ] |> ignore; git w [ "commit"; "-qm"; "T0 bootstrap" ] |> ignore; git w [ "push"; "-q"; f.Competition; "main" ] |> ignore)
      { case "competition-repository-unreadable" "LP-10" ignore with
          Args = fun f -> Some [ "--indy-init"; f.Indy; "--competition"; Path.Combine(f.Indy, "no-such-repository"); "--live-run"; "4242"; "--gh"; f.Gh ] } ]

if Directory.Exists work then failwithf "%s exists; give a new directory" work
Directory.CreateDirectory work |> ignore
let outcomes =
    cases |> List.map (fun c ->
        let code, out = fixture c.Name c.Edit c.Args
        let verdictOk = (c.Expect = "READY" && code = 0 && out.Contains "launch-preflight: READY") || (c.Expect = "REFUSED" && code = 4 && out.Contains "launch-preflight: REFUSED")
        let refusedChecks = out.Split('\n') |> Array.filter (fun l -> l.Contains " REFUSED ") |> Array.map (fun l -> l.Trim().Split(' ').[0]) |> Set.ofArray
        let ok = verdictOk && (c.Expect = "READY" || refusedChecks.Contains c.Check)
        printfn "%-40s expected %-8s exit %d  refused by %-22s %s" c.Name c.Expect code (String.Join(",", refusedChecks)) (if ok then "PASS" else "FAIL <<<")
        ok)
let failed = outcomes |> List.filter not |> List.length
printfn "%d case(s), %d failed" outcomes.Length failed
exit (if failed = 0 then 0 else 1)
