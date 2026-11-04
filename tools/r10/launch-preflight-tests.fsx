// launch-preflight-tests.fsx: launch-preflight's negative controls. A synthetic Indy-init and competition repository
// that satisfy every check (positive control: READY), then one mutation per refusal (each must be REFUSED by the
// check it names). Synthetic fixtures only; nothing real is read or written.
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
let git dir a = run dir "git" ([ "-c"; "user.name=fixture"; "-c"; "user.email=fixture@example.invalid"; "-c"; "commit.gpgsign=false" ] @ a)
let write (file: string) (text: string) = Directory.CreateDirectory(Path.GetDirectoryName file) |> ignore; File.WriteAllText(file, text)
let sha (text: string) = (SHA256.HashData(Text.Encoding.UTF8.GetBytes text) |> Convert.ToHexString).ToLowerInvariant()

/// The positive fixture, with optional edits applied after it is built (name, edit of (indy-init dir, competition dir)).
let fixture (name: string) (edit: string -> string -> unit) =
    let dir = Path.Combine(work, name)
    let ii, comp, live = Path.Combine(dir, "indy-init"), Path.Combine(dir, "competition"), Path.Combine(dir, "live")
    let files =
        [ "kickoff/rehearsal-010/catalog-verification-contract.json", "{\"contract\":\"catalog\"}\n"
          "kickoff/rehearsal-010/design-contract.json", "{\"contract\":\"design\"}\n"
          "kickoff/rehearsal-010/deadline-policy.json", "{\"windowMinutes\":420}\n"
          "verification/journeys/bindings.json", "{\"journeys\":[\"J1\",\"J2\",\"J3\"]}\n"
          "kickoff/rehearsal-010/process-evidence-rules.json", "{\"repository\":\"indy-init-rehearsal-010\"}\n"
          "requirements-compiled/v4.1.4/package-manifest.json", "{\"package\":\"4.1.4\"}\n"
          "docs/rehearsal-010-readiness/scope-manifest.json", "{\"scopeSha256\":\"scope-final\",\"counts\":{\"requirements\":205}}\n" ]
    files |> List.iter (fun (rel, text) -> write (Path.Combine(ii, rel)) text)
    let digest rel = files |> List.find (fst >> (=) rel) |> snd |> sha
    let pin rel = $"{{\"path\":\"{rel}\",\"sha256\":\"{digest rel}\"}}"
    let requests = [ "ODR-RG-03"; "ODR-R10-03"; "ODR-R10-05"; "ODR-R10-01" ]
    write (Path.Combine(ii, "kickoff/rehearsal-010/proposals/owner-decision-requests.json"))
        ("{\"requests\":[" + String.Join(",", requests |> List.map (fun r -> $"{{\"id\":\"{r}\"}}")) + "]}\n")
    let change (id: string) (pins: string list) = $"{{\"decision_id\":\"{id}\",\"action\":\"decide\",\"option\":\"A\",\"pins\":[{String.Join(',', pins)}]}}"
    let changes =
        [ "{\"decision_id\":\"ODR-V414-01\",\"action\":\"select\",\"option\":\"A\"}"
          "{\"decision_id\":\"ODR-V414-02\",\"action\":\"select\",\"option\":\"A\"}"
          "{\"decision_id\":\"ODR-V414-03\",\"action\":\"approve-package\",\"package\":\"requirements-compiled/v4.1.4\"}"
          change "ODR-RG-03" []
          change "ODR-R10-03" [ pin "kickoff/rehearsal-010/catalog-verification-contract.json" ]
          change "ODR-R10-05" [ pin "kickoff/rehearsal-010/design-contract.json" ]
          change "ODR-R10-01" [ "{\"path\":\"docs/rehearsal-010-readiness/scope-manifest.json\",\"field\":\"scopeSha256\",\"value\":\"\\\"scope-final\\\"\",\"sha256\":\"x\"}" ] ]
    write (Path.Combine(ii, "kickoff/rehearsal-007/approvals.causa.json"))
        ("{\"approvals\":[{\"id\":\"APR-R10-OWNER-1\",\"authority\":{\"kind\":\"operator\",\"id\":\"kemiller2002\"},\"changes\":[" + String.Join(",", changes) + "]}]}\n")
    write (Path.Combine(ii, "docs/rehearsal-010-readiness/evidence/adversarial/cases.tsv")) "case\ttitle\tverdict\nC01\tzero tests\tVALIDATED\nC02\tgate\tVALIDATED\n"
    git ii [ "init"; "-q"; "-b"; "main" ] |> ignore
    git ii [ "add"; "-A" ] |> ignore
    git ii [ "commit"; "-qm"; "fixture" ] |> ignore
    git ii [ "tag"; "indy-init-requirements-v4.1.4" ] |> ignore
    let _, tagCommit = git ii [ "rev-parse"; "HEAD" ]
    let lock =
        $"""{{"status":"OWNER-PINNED","requirementsPackage":{{"path":"requirements-compiled/v4.1.4","packageVersion":"4.1.4","tag":"indy-init-requirements-v4.1.4","tagCommit":"{tagCommit.Trim()}","manifestSha256":"{digest "requirements-compiled/v4.1.4/package-manifest.json"}"}},
  "scope":{{"manifest":"docs/rehearsal-010-readiness/scope-manifest.json","scopeSha256":"scope-final"}},
  "catalogVerification":{{"contract":"kickoff/rehearsal-010/catalog-verification-contract.json","contractSha256":"{digest "kickoff/rehearsal-010/catalog-verification-contract.json"}"}},
  "designContract":{{"contract":"kickoff/rehearsal-010/design-contract.json","contractSha256":"{digest "kickoff/rehearsal-010/design-contract.json"}"}},
  "deadlinePolicy":{{"path":"kickoff/rehearsal-010/deadline-policy.json","sha256":"{digest "kickoff/rehearsal-010/deadline-policy.json"}"}},
  "journeys":{{"bindings":"verification/journeys/bindings.json","bindingsSha256":"{digest "verification/journeys/bindings.json"}"}},
  "processEvidence":{{"rules":"kickoff/rehearsal-010/process-evidence-rules.json","rulesSha256":"{digest "kickoff/rehearsal-010/process-evidence-rules.json"}"}},
  "resolvedSet":{{"expectedTools":{{"dokimos":"0.11.1"}}}},
  "testRepository":{{"owner":"kemiller2002","repo":"causa-acceptance-r10","branch":"causa-acceptance","isolated":true}}}}
"""
    write (Path.Combine(ii, "kickoff/rehearsal-010/toolchain.lock.json")) lock
    git ii [ "add"; "-A" ] |> ignore
    git ii [ "commit"; "-qm"; "owner lock" ] |> ignore
    let row j e status = $"{{\"journey\":\"{j}\",\"engine\":\"{e}\",\"status\":\"{status}\"}}"
    let rows = [ for j in [ "J1"; "J2"; "J3" ] do for e in [ "chromium"; "webkit" ] -> row j e "passed" ]
    write (Path.Combine(live, "journey-results.json")) ("{\"schema\":\"indy-init.journey-results/1\",\"provider\":{\"mode\":\"github\",\"baseUrl\":\"https://api.github.com\"},\"results\":[" + String.Join(",", rows) + "]}\n")
    Directory.CreateDirectory comp |> ignore
    git comp [ "init"; "-q"; "-b"; "main" ] |> ignore
    write (Path.Combine(comp, "README.md")) "competition\n"
    git comp [ "add"; "-A" ] |> ignore
    git comp [ "commit"; "-qm"; "initial" ] |> ignore
    edit ii comp
    let code, out = run work "dotnet" [ "fsi"; preflight; "--"; "--indy-init"; ii; "--competition"; comp; "--live-evidence"; live ]
    evidence |> Option.iter (fun d -> write (Path.Combine(d, name + ".log")) out)
    code, out

let commitAll (dir: string) = git dir [ "add"; "-A" ] |> ignore; git dir [ "commit"; "-qm"; "edit" ] |> ignore
let replaceIn (file: string) (a: string) (b: string) =
    let t = File.ReadAllText file
    if not (t.Contains a) then failwithf "%s: missing %s" file a
    File.WriteAllText(file, t.Replace(a, b))

type Case = { Name: string; Expect: string; Check: string; Edit: string -> string -> unit }
let cases =
    [ { Name = "positive-control"; Expect = "READY"; Check = ""; Edit = fun _ _ -> () }
      { Name = "missing-owner-approval"; Expect = "REFUSED"; Check = "LP-05"
        Edit = fun ii _ -> replaceIn (Path.Combine(ii, "kickoff/rehearsal-007/approvals.causa.json")) "\"decision_id\":\"ODR-R10-05\"" "\"decision_id\":\"ODR-R10-XX\""; commitAll ii }
      { Name = "approval-by-someone-else"; Expect = "REFUSED"; Check = "LP-05"
        Edit = fun ii _ -> replaceIn (Path.Combine(ii, "kickoff/rehearsal-007/approvals.causa.json")) "\"id\":\"kemiller2002\"" "\"id\":\"claude\""; commitAll ii }
      { Name = "altered-design-contract"; Expect = "REFUSED"; Check = "LP-03"
        Edit = fun ii _ -> write (Path.Combine(ii, "kickoff/rehearsal-010/design-contract.json")) "{\"contract\":\"weakened\"}\n"; commitAll ii }
      { Name = "altered-scope"; Expect = "REFUSED"; Check = "LP-04"
        Edit = fun ii _ -> replaceIn (Path.Combine(ii, "docs/rehearsal-010-readiness/scope-manifest.json")) "scope-final" "scope-other"; commitAll ii }
      { Name = "lock-still-proposed"; Expect = "REFUSED"; Check = "LP-01"
        Edit = fun ii _ -> replaceIn (Path.Combine(ii, "kickoff/rehearsal-010/toolchain.lock.json")) "OWNER-PINNED" "PROPOSED: not an owner pin"; commitAll ii }
      { Name = "v4.1.4-not-tagged"; Expect = "REFUSED"; Check = "LP-02"
        Edit = fun ii _ -> git ii [ "tag"; "-d"; "indy-init-requirements-v4.1.4" ] |> ignore }
      { Name = "dokimos-0.11.0"; Expect = "REFUSED"; Check = "LP-06"
        Edit = fun ii _ -> replaceIn (Path.Combine(ii, "kickoff/rehearsal-010/toolchain.lock.json")) "\"dokimos\":\"0.11.1\"" "\"dokimos\":\"0.11.0\""; commitAll ii }
      { Name = "test-repository-not-isolated"; Expect = "REFUSED"; Check = "LP-07"
        Edit = fun ii _ -> replaceIn (Path.Combine(ii, "kickoff/rehearsal-010/toolchain.lock.json")) "\"isolated\":true" "\"isolated\":false"; commitAll ii }
      { Name = "absent-credential-no-live-run"; Expect = "REFUSED"; Check = "LP-08"
        Edit = fun ii _ -> File.Delete(Path.Combine(Path.GetDirectoryName ii, "live", "journey-results.json")) }
      { Name = "live-run-against-a-double"; Expect = "REFUSED"; Check = "LP-08"
        Edit = fun ii _ -> replaceIn (Path.Combine(Path.GetDirectoryName ii, "live", "journey-results.json")) "https://api.github.com" "http://127.0.0.1:4610" }
      { Name = "failed-gate-journey"; Expect = "REFUSED"; Check = "LP-08"
        Edit = fun ii _ -> replaceIn (Path.Combine(Path.GetDirectoryName ii, "live", "journey-results.json")) "{\"journey\":\"J2\",\"engine\":\"webkit\",\"status\":\"passed\"}" "{\"journey\":\"J2\",\"engine\":\"webkit\",\"status\":\"failed\"}" }
      { Name = "adversarial-case-failed"; Expect = "REFUSED"; Check = "LP-09"
        Edit = fun ii _ -> replaceIn (Path.Combine(ii, "docs/rehearsal-010-readiness/evidence/adversarial/cases.tsv")) "C02\tgate\tVALIDATED" "C02\tgate\tFAILED"; commitAll ii }
      { Name = "clock-already-started"; Expect = "REFUSED"; Check = "LP-10"
        Edit = fun _ comp -> write (Path.Combine(comp, "evidence", "t0", "t0.json")) "{\"t0\":\"2026-11-05T00:00:00Z\"}\n"; commitAll comp } ]

if Directory.Exists work then failwithf "%s exists; give a new directory" work
Directory.CreateDirectory work |> ignore
let outcomes =
    cases |> List.map (fun c ->
        let code, out = fixture c.Name c.Edit
        let verdictOk = (c.Expect = "READY" && code = 0 && out.Contains "launch-preflight: READY") || (c.Expect = "REFUSED" && code = 4 && out.Contains "launch-preflight: REFUSED")
        // a refusal must come from the check the case targets (the others stay PASS)
        let refusedChecks = out.Split('\n') |> Array.filter (fun l -> l.Contains " REFUSED ") |> Array.map (fun l -> l.Trim().Split(' ').[0]) |> Set.ofArray
        let byTarget = c.Expect = "READY" || (refusedChecks.Contains c.Check)
        let ok = verdictOk && byTarget
        printfn "%-32s expected %-8s exit %d  refused by %-20s %s" c.Name c.Expect code (String.Join(",", refusedChecks)) (if ok then "PASS" else "FAIL <<<")
        ok)
let failed = outcomes |> List.filter not |> List.length
printfn "%d case(s), %d failed" outcomes.Length failed
exit (if failed = 0 then 0 else 1)
