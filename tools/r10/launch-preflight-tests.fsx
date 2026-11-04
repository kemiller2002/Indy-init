// launch-preflight-tests.fsx: launch-preflight's negative controls. A synthetic Indy-init (with its published
// origin), a published competition repository, and a stand-in for gh that verifies commit signatures and serves two
// CI runs (the live-provider preflight and the adversarial suite) satisfy every check (positive control: READY); then
// one mutation per refusal, each REFUSED by the check it names. Synthetic fixtures only: nothing real is read,
// written or fetched.
//
// The stand-in treats a commit as signed by the owner when its author is the owner and its message carries neither an
// agent trailer nor "[unsigned]": a fixture's way of saying "a commit an agent made under the owner's name".
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
let git dir a = gitAs "kemiller2002" dir a
let write (file: string) (text: string) = Directory.CreateDirectory(Path.GetDirectoryName file) |> ignore; File.WriteAllText(file, text)
let sha (text: string) = (SHA256.HashData(Text.Encoding.UTF8.GetBytes text) |> Convert.ToHexString).ToLowerInvariant()
let replaceIn (file: string) (a: string) (b: string) =
    let t = File.ReadAllText file
    if not (t.Contains a) then failwithf "%s: missing %s" file a
    File.WriteAllText(file, t.Replace(a, b))

type Fixture = { Indy: string; Origin: string; Competition: string; CompetitionWork: string; Runs: string; Gh: string }
let commitPushMessage (f: Fixture) (message: string list) =
    git f.Indy [ "add"; "-A" ] |> ignore
    git f.Indy ([ "commit"; "-q" ] @ (message |> List.collect (fun m -> [ "-m"; m ]))) |> ignore
    git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore
let commitPush f (message: string) = commitPushMessage f [ message ]
/// A commit an agent made, though it may carry the owner's name: never owner-signed.
let agentCommitPush f (message: string) = commitPushMessage f [ message; "[unsigned]" ]

let testRepository = "{\"owner\":\"kemiller2002\",\"repo\":\"causa-acceptance-r10\",\"branch\":\"causa-acceptance\",\"isolated\":true}"
let decisions =
    [ "ODR-RG-03"; "ODR-RG-05"; "ODR-R10-02"; "ODR-R10-03"; "ODR-R10-04"; "ODR-R10-05"; "ODR-R10-06"; "ODR-R10-07"; "ODR-R10-08"; "ODR-R10-09"
      "ODR-R10-10"; "ODR-R10-11"; "ODR-R10-12"; "ODR-R10-13"; "ODR-R10-14"; "ODR-R10-15"; "ODR-R10-16"; "ODR-R10-17"; "ODR-R10-18"; "ODR-R10-01" ]

/// The positive fixture, then `edit` (a mutation), then launch-preflight on it.
let fixture (name: string) (edit: Fixture -> unit) (extraArgs: Fixture -> string list option) =
    let dir = Path.Combine(work, name)
    let f = { Indy = Path.Combine(dir, "Indy-init"); Origin = Path.Combine(dir, "origin.git"); Competition = Path.Combine(dir, "competition.git")
              CompetitionWork = Path.Combine(dir, "competition-work"); Runs = Path.Combine(dir, "runs"); Gh = Path.Combine(dir, "gh") }
    let files =
        [ "kickoff/rehearsal-010/catalog-verification-contract.json", "{\"contract\":\"catalog\"}\n"
          "kickoff/rehearsal-010/design-contract.json", "{\"contract\":\"design\"}\n"
          "kickoff/rehearsal-010/deadline-policy.json", "{\"windowMinutes\":420}\n"
          "verification/journeys/bindings.json", "{\"journeys\":[\"J1\",\"J2\",\"J3\"]}\n"
          "kickoff/rehearsal-010/process-evidence-rules.json", "{\"repository\":\"indy-init-rehearsal-010\"}\n"
          "kickoff/rehearsal-010/proposals/owner-decision-requests.json", "{\"requests\":[" + String.Join(",", decisions |> List.map (fun d -> $"{{\"id\":\"{d}\"}}")) + "]}\n"
          "requirements-compiled/v4.1.4/package-manifest.json", "{\"package\":\"4.1.4\"}\n"
          "docs/rehearsal-010-readiness/scope-manifest.json", "{\"scopeSha256\":\"scope-final\",\"counts\":{\"requirements\":205}}\n"
          "tools/r10/adversarial.fsx", "// fixture\n    [ { Id = \"C01\"; Title = \"a\" }\n      { Id = \"C02\"; Title = \"b\" } ]\n" ]
    files |> List.iter (fun (rel, text) -> write (Path.Combine(f.Indy, rel)) text)
    let digest rel = files |> List.find (fst >> (=) rel) |> snd |> sha
    let pin rel = $"{{\"path\":\"{rel}\",\"sha256\":\"{digest rel}\"}}"
    let field (path: string) (fieldName: string) (value: string) = $"{{\"path\":\"{path}\",\"field\":\"{fieldName}\",\"value\":{Text.Json.JsonSerializer.Serialize value},\"sha256\":\"x\"}}"
    let pinsOf d =
        match d with
        | "ODR-R10-03" -> [ pin "kickoff/rehearsal-010/catalog-verification-contract.json" ]
        | "ODR-R10-05" -> [ pin "kickoff/rehearsal-010/design-contract.json" ]
        | "ODR-R10-09" -> [ field "kickoff/rehearsal-010/toolchain.lock.json" "testRepository" testRepository ]
        | "ODR-R10-01" -> [ field "docs/rehearsal-010-readiness/scope-manifest.json" "scopeSha256" "\"scope-final\"" ]
        | _ -> []
    let change (id: string) = $"""{{"decision_id":"{id}","action":"decide","option":"{(if id = "ODR-R10-02" then "B" else "A")}","pins":[{String.Join(',', pinsOf id)}]}}"""
    let changes =
        [ "{\"decision_id\":\"ODR-V414-01\",\"action\":\"select\",\"option\":\"A\"}"
          "{\"decision_id\":\"ODR-V414-02\",\"action\":\"select\",\"option\":\"A\"}"
          "{\"decision_id\":\"ODR-V414-03\",\"action\":\"approve-package\",\"package\":\"requirements-compiled/v4.1.4\"}" ]
        @ (decisions |> List.map change)
    write (Path.Combine(f.Indy, "kickoff/rehearsal-007/approvals.causa.json"))
        ("{\"approvals\":[{\"id\":\"APR-R10-OWNER-1\",\"authority\":{\"kind\":\"operator\",\"id\":\"kemiller2002\"},\"changes\":[\n" + String.Join(",\n", changes) + "\n]}]}\n")
    run dir "git" [ "init"; "-q"; "--bare"; "-b"; "main"; f.Origin ] |> ignore
    git f.Indy [ "init"; "-q"; "-b"; "main" ] |> ignore
    git f.Indy [ "remote"; "add"; "origin"; f.Origin ] |> ignore
    commitPush f "Owner decisions (fixture)"
    git f.Indy [ "tag"; "indy-init-requirements-v4.1.4" ] |> ignore
    git f.Indy [ "push"; "-q"; "origin"; "indy-init-requirements-v4.1.4" ] |> ignore
    let _, h1 = git f.Indy [ "rev-parse"; "HEAD" ]
    let h1 = h1.Trim()
    write (Path.Combine(f.Indy, "kickoff/rehearsal-010/toolchain.lock.json"))
        ($"""{{"status":"OWNER-PINNED","requirementsPackage":{{"path":"requirements-compiled/v4.1.4","packageVersion":"4.1.4","tag":"indy-init-requirements-v4.1.4","tagCommit":"{h1}","manifestSha256":"{digest "requirements-compiled/v4.1.4/package-manifest.json"}"}},
  "scope":{{"manifest":"docs/rehearsal-010-readiness/scope-manifest.json","scopeSha256":"scope-final"}},
  "catalogVerification":{{"contract":"kickoff/rehearsal-010/catalog-verification-contract.json","contractSha256":"{digest "kickoff/rehearsal-010/catalog-verification-contract.json"}"}},
  "designContract":{{"contract":"kickoff/rehearsal-010/design-contract.json","contractSha256":"{digest "kickoff/rehearsal-010/design-contract.json"}"}},
  "deadlinePolicy":{{"path":"kickoff/rehearsal-010/deadline-policy.json","sha256":"{digest "kickoff/rehearsal-010/deadline-policy.json"}"}},
  "journeys":{{"bindings":"verification/journeys/bindings.json","bindingsSha256":"{digest "verification/journeys/bindings.json"}"}},
  "processEvidence":{{"rules":"kickoff/rehearsal-010/process-evidence-rules.json","rulesSha256":"{digest "kickoff/rehearsal-010/process-evidence-rules.json"}"}},
  "ownerDecisionRequests":{{"path":"kickoff/rehearsal-010/proposals/owner-decision-requests.json","sha256":"{digest "kickoff/rehearsal-010/proposals/owner-decision-requests.json"}"}},
  "resolvedSet":{{"expectedTools":{{"dokimos":"0.11.1"}}}},
  "testRepository":""" + testRepository + "}\n")
    commitPush f "Owner lock (fixture)"
    // The two CI runs gh serves, both of commit h1: 4242 the live-provider preflight, 777 the adversarial suite.
    write (Path.Combine(f.Runs, "run-4242.json")) $"{{\"workflowName\":\"Live-provider preflight (Rehearsal 010)\",\"headBranch\":\"main\",\"event\":\"workflow_dispatch\",\"conclusion\":\"success\",\"headSha\":\"{h1}\"}}\n"
    let row j e status = $"{{\"journey\":\"{j}\",\"engine\":\"{e}\",\"status\":\"{status}\"}}"
    let rows = [ for j in [ "J1"; "J2"; "J3" ] do for e in [ "chromium"; "webkit" ] -> row j e "passed" ]
    write (Path.Combine(f.Runs, "4242", "live-provider-preflight", "journey-results.json"))
        ($"{{\"schema\":\"indy-init.journey-results/1\",\"candidate\":\"fixture-{h1}\",\"provider\":{{\"mode\":\"github\",\"baseUrl\":\"https://api.github.com\"}},\"results\":[" + String.Join(",", rows) + "]}\n")
    write (Path.Combine(f.Runs, "4242", "live-provider-preflight", "run-info.json")) ($"{{\"lock\":\"kickoff/rehearsal-010/toolchain.lock.json\",\"sha\":\"{h1}\",\"testRepository\":{testRepository}}}\n")
    write (Path.Combine(f.Runs, "run-777.json")) $"{{\"workflowName\":\"Preflight tooling\",\"headBranch\":\"main\",\"event\":\"push\",\"conclusion\":\"success\",\"headSha\":\"{h1}\"}}\n"
    write (Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "cases.tsv")) "case\ttitle\tverdict\nC01\tzero tests\tVALIDATED\nC02\tgate\tVALIDATED\n"
    let stub =
        [ @"#!/bin/sh"
          @"# gh stand-in (fixture). api: GitHub's verdict on a commit (signed by the owner unless authored otherwise or marked);"
          @"# run view: a run's metadata; run download: a run's artifact."
          @"if [ ""$1"" = api ]; then"
          @"  c=${2##*/}; msg=$(git -C '@INDY@' show -s --format=%B ""$c"" 2>/dev/null) || exit 1"
          @"  who=$(git -C '@INDY@' show -s --format=%an ""$c"")"
          @"  case ""$msg"" in *Claude-Session:*|*'[unsigned]'*) v=false ;; *) v=true ;; esac"
          @"  printf '[%s,""%s"",""""]\n' ""$v"" ""$who""; exit 0"
          @"fi"
          @"case ""$1 $2"" in"
          @"  ""run view"") cat '@RUNS@'/run-$3.json 2>/dev/null || exit 1 ;;"
          @"  ""run download"") id=$3; while [ ""$1"" != ""--dir"" ]; do shift; done; cp -r '@RUNS@'/$id/. ""$2"" ;;"
          @"  *) exit 2 ;;"
          @"esac"
          @"" ]
        |> String.concat "\n"
    write f.Gh (stub.Replace("@INDY@", f.Indy).Replace("@RUNS@", f.Runs))
    run dir "chmod" [ "+x"; f.Gh ] |> ignore
    run dir "git" [ "init"; "-q"; "--bare"; "-b"; "main"; f.Competition ] |> ignore
    git dir [ "init"; "-q"; "-b"; "main"; f.CompetitionWork ] |> ignore
    write (Path.Combine(f.CompetitionWork, "README.md")) "competition\n"
    git f.CompetitionWork [ "add"; "-A" ] |> ignore
    git f.CompetitionWork [ "commit"; "-qm"; "initial" ] |> ignore
    git f.CompetitionWork [ "push"; "-q"; f.Competition; "main" ] |> ignore
    edit f
    let defaults = [ "--indy-init"; f.Indy; "--origin"; f.Origin; "--competition"; f.Competition; "--live-run"; "4242"; "--adversarial-run"; "777"; "--gh"; f.Gh ]
    let a = extraArgs f |> Option.defaultValue defaults
    let code, out = run work "dotnet" ([ "fsi"; preflight; "--" ] @ a)
    evidence |> Option.iter (fun d -> write (Path.Combine(d, name + ".log")) out)
    code, out

type Case = { Name: string; Expect: string; Check: string; Edit: Fixture -> unit; Args: Fixture -> string list option }
let case name check edit = { Name = name; Expect = "REFUSED"; Check = check; Edit = edit; Args = fun _ -> None }
let without (flag: string) (f: Fixture) =
    let all = [ "--indy-init", f.Indy; "--origin", f.Origin; "--competition", f.Competition; "--live-run", "4242"; "--adversarial-run", "777"; "--gh", f.Gh ]
    Some(all |> List.filter (fst >> (<>) flag) |> List.collect (fun (k, v) -> [ k; v ]))
let approvals f = Path.Combine(f.Indy, "kickoff/rehearsal-007/approvals.causa.json")
let lockFile f = Path.Combine(f.Indy, "kickoff/rehearsal-010/toolchain.lock.json")
let liveResults f = Path.Combine(f.Runs, "4242", "live-provider-preflight", "journey-results.json")
let cases =
    [ { Name = "positive-control"; Expect = "READY"; Check = ""; Edit = ignore; Args = fun _ -> None }
      // LP-05: decisions and their provenance
      case "missing-owner-approval" "LP-05" (fun f -> replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\"" "\"decision_id\":\"ODR-R10-XX\""; commitPush f "Owner decisions (edit)")
      case "approval-by-someone-else" "LP-05" (fun f -> replaceIn (approvals f) "\"id\":\"kemiller2002\"" "\"id\":\"claude\""; commitPush f "Owner decisions (edit)")
      case "approval-committed-by-an-agent" "LP-05" (fun f -> replaceIn (approvals f) "APR-R10-OWNER-1" "APR-R10-OWNER-2"; commitPushMessage f [ "Owner decisions"; "Claude-Session: https://claude.ai/code/x" ])
      case "agent-edits-an-owner-approval-in-place" "LP-05" (fun f ->
          // the owner chose B for ODR-R10-05; an agent, under the owner's name, rewrites it to A
          replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"B\""
          commitPush f "Owner decisions: ODR-R10-05 B"
          replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"B\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\""
          agentCommitPush f "Owner decisions")
      case "agent-reuses-an-approval-id" "LP-05" (fun f ->
          replaceIn (approvals f) "]}]}" "]},{\"id\":\"APR-R10-OWNER-1\",\"authority\":{\"kind\":\"operator\",\"id\":\"kemiller2002\"},\"changes\":[{\"decision_id\":\"ODR-R10-18\",\"action\":\"decide\",\"option\":\"A\",\"pins\":[]}]}]}"
          agentCommitPush f "Owner decisions")
      case "duplicate-approval-id-even-if-signed" "LP-05" (fun f ->
          replaceIn (approvals f) "]}]}" "]},{\"id\":\"APR-R10-OWNER-1\",\"authority\":{\"kind\":\"operator\",\"id\":\"kemiller2002\"},\"changes\":[]}]}"
          commitPush f "Owner decisions")
      case "decision-recorded-as-do-not-approve" "LP-05" (fun f -> replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"B\""; commitPush f "Owner decisions (edit)")
      case "requests-file-edited-to-allow-b" "LP-03" (fun f -> replaceIn (Path.Combine(f.Indy, "kickoff/rehearsal-010/proposals/owner-decision-requests.json")) "{\"id\":\"ODR-R10-05\"}" "{\"id\":\"ODR-R10-05\",\"launchable\":[\"A\",\"B\"]}"; agentCommitPush f "requests")
      // LP-00..04, LP-06, LP-07: the lock and what it pins
      case "altered-design-contract" "LP-03" (fun f -> write (Path.Combine(f.Indy, "kickoff/rehearsal-010/design-contract.json")) "{\"contract\":\"weakened\"}\n"; agentCommitPush f "edit")
      case "altered-scope" "LP-04" (fun f -> replaceIn (Path.Combine(f.Indy, "docs/rehearsal-010-readiness/scope-manifest.json")) "scope-final" "scope-other"; agentCommitPush f "edit")
      case "uncommitted-change" "LP-00" (fun f -> write (Path.Combine(f.Indy, "kickoff/rehearsal-010/design-contract.json")) "{\"contract\":\"local\"}\n")
      case "checkout-is-not-the-published-main" "LP-00" (fun f -> write (Path.Combine(f.Indy, "NOTE.md")) "local\n"; git f.Indy [ "add"; "-A" ] |> ignore; git f.Indy [ "commit"; "-qm"; "local only" ] |> ignore)
      case "lock-still-proposed" "LP-01" (fun f -> replaceIn (lockFile f) "OWNER-PINNED" "PROPOSED: not an owner pin"; commitPush f "lock")
      case "lock-changed-by-an-agent" "LP-01" (fun f -> replaceIn (lockFile f) "\"status\":\"OWNER-PINNED\"" "\"status\":\"OWNER-PINNED \""; agentCommitPush f "lock")
      case "v4.1.4-not-tagged-on-origin" "LP-02" (fun f -> git f.Indy [ "push"; "-q"; "origin"; ":refs/tags/indy-init-requirements-v4.1.4" ] |> ignore)
      case "dokimos-0.11.0" "LP-06" (fun f -> replaceIn (lockFile f) "\"dokimos\":\"0.11.1\"" "\"dokimos\":\"0.11.0\""; commitPush f "lock")
      case "test-repository-not-isolated" "LP-07" (fun f -> replaceIn (lockFile f) "\"isolated\":true" "\"isolated\":false"; commitPush f "lock")
      case "test-repository-not-the-approved-one" "LP-07" (fun f -> replaceIn (lockFile f) "\"repo\":\"causa-acceptance-r10\"" "\"repo\":\"causa-acceptance-other\""; commitPush f "lock")
      // LP-08: the live-provider run
      { case "absent-credential-no-live-run" "LP-08" ignore with Args = without "--live-run" }
      case "live-run-against-a-double" "LP-08" (fun f -> replaceIn (liveResults f) "https://api.github.com" "http://127.0.0.1:4610")
      case "live-run-on-a-branch" "LP-08" (fun f -> replaceIn (Path.Combine(f.Runs, "run-4242.json")) "\"headBranch\":\"main\"" "\"headBranch\":\"agent-branch\"")
      case "live-run-failed" "LP-08" (fun f -> replaceIn (Path.Combine(f.Runs, "run-4242.json")) "\"conclusion\":\"success\"" "\"conclusion\":\"failure\"")
      case "live-run-of-another-commit" "LP-08" (fun f -> replaceIn (liveResults f) "\"candidate\":\"fixture-" "\"candidate\":\"fixture-0")
      case "live-run-before-the-journeys-changed" "LP-08" (fun f -> write (Path.Combine(f.Indy, "verification/journeys/j1.spec.mjs")) "// changed\n"; commitPush f "journeys")
      case "live-run-against-another-test-repository" "LP-08" (fun f -> replaceIn (Path.Combine(f.Runs, "4242", "live-provider-preflight", "run-info.json")) "causa-acceptance-r10" "someone-else")
      case "failed-gate-journey" "LP-08" (fun f -> replaceIn (liveResults f) "{\"journey\":\"J2\",\"engine\":\"webkit\",\"status\":\"passed\"}" "{\"journey\":\"J2\",\"engine\":\"webkit\",\"status\":\"failed\"}")
      case "duplicate-journey-row-hides-a-failure" "LP-08" (fun f -> replaceIn (liveResults f) "{\"journey\":\"J3\",\"engine\":\"webkit\",\"status\":\"passed\"}" "{\"journey\":\"J3\",\"engine\":\"webkit\",\"status\":\"passed\"},{\"journey\":\"J3\",\"engine\":\"webkit\",\"status\":\"failed\"}")
      // LP-09: the adversarial suite, from its CI run
      { case "no-adversarial-run" "LP-09" ignore with Args = without "--adversarial-run" }
      case "adversarial-run-failed" "LP-09" (fun f -> replaceIn (Path.Combine(f.Runs, "run-777.json")) "\"conclusion\":\"success\"" "\"conclusion\":\"failure\"")
      case "adversarial-case-not-validated" "LP-09" (fun f -> replaceIn (Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "cases.tsv")) "C02\tgate\tVALIDATED" "C02\tgate\tFAILED")
      case "adversarial-case-missing" "LP-09" (fun f -> replaceIn (Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "cases.tsv")) "C02\tgate\tVALIDATED\n" "")
      case "adversarial-run-of-older-tools" "LP-09" (fun f -> write (Path.Combine(f.Indy, "tools/r10/new-tool.fsx")) "// new\n"; commitPush f "tools")
      // LP-10: the competition repository, as published
      case "clock-already-started" "LP-10" (fun f ->
          write (Path.Combine(f.CompetitionWork, "evidence", "t0", "t0.json")) "{\"t0\":\"2026-11-05T00:00:00Z\"}\n"
          git f.CompetitionWork [ "add"; "-A" ] |> ignore; git f.CompetitionWork [ "commit"; "-qm"; "T0 bootstrap" ] |> ignore
          git f.CompetitionWork [ "push"; "-q"; f.Competition; "main" ] |> ignore)
      case "competition-second-root" "LP-10" (fun f ->
          git f.CompetitionWork [ "checkout"; "-q"; "--orphan"; "other" ] |> ignore
          write (Path.Combine(f.CompetitionWork, "OTHER.md")) "other\n"
          git f.CompetitionWork [ "add"; "-A" ] |> ignore; git f.CompetitionWork [ "commit"; "-qm"; "another root" ] |> ignore
          git f.CompetitionWork [ "push"; "-q"; f.Competition; "other" ] |> ignore)
      { case "competition-repository-unreadable" "LP-10" ignore with
          Args = fun f -> Some [ "--indy-init"; f.Indy; "--origin"; f.Origin; "--competition"; Path.Combine(f.Indy, "no-such-repository"); "--live-run"; "4242"; "--adversarial-run"; "777"; "--gh"; f.Gh ] } ]

if Directory.Exists work then failwithf "%s exists; give a new directory" work
Directory.CreateDirectory work |> ignore
let outcomes =
    cases |> List.map (fun c ->
        let code, out = fixture c.Name c.Edit c.Args
        let verdictOk = (c.Expect = "READY" && code = 0 && out.Contains "launch-preflight: READY") || (c.Expect = "REFUSED" && code = 4 && out.Contains "launch-preflight: REFUSED")
        let refusedChecks = out.Split('\n') |> Array.filter (fun l -> l.Contains " REFUSED ") |> Array.map (fun l -> l.Trim().Split(' ').[0]) |> Set.ofArray
        let ok = verdictOk && (c.Expect = "READY" || refusedChecks.Contains c.Check)
        printfn "%-44s expected %-8s exit %d  refused by %-22s %s" c.Name c.Expect code (String.Join(",", refusedChecks)) (if ok then "PASS" else "FAIL <<<")
        ok)
let failed = outcomes |> List.filter not |> List.length
printfn "%d case(s), %d failed" outcomes.Length failed
exit (if failed = 0 then 0 else 1)
