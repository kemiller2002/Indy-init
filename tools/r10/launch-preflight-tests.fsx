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

let runEnv (env: (string * string) list) (dir: string) (exe: string) (a: string list) =
    let info = ProcessStartInfo(exe, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    a |> List.iter info.ArgumentList.Add
    env |> List.iter (fun (k, v) -> info.Environment[k] <- v)
    use p = Process.Start info
    let out = p.StandardOutput.ReadToEnd()
    let err = p.StandardError.ReadToEnd()
    p.WaitForExit()
    p.ExitCode, out + err
let run = runEnv []

/// The fixture's signing keys: the owner's (the one registered on "GitHub") and an agent's, in one keyring.
let keyring = Path.Combine(work, "gnupg")
let gpg (a: string list) = runEnv [ "GNUPGHOME", keyring ] work "gpg" ([ "--batch"; "--quiet"; "--pinentry-mode"; "loopback"; "--passphrase"; "" ] @ a)
let keyOf (who: string) =
    let _, out = gpg [ "--list-secret-keys"; "--with-colons"; $"{who}@example.invalid" ]
    out.Split('\n') |> Array.tryFind (fun l -> l.StartsWith "fpr:") |> Option.map (fun l -> l.Split(':').[9]) |> Option.defaultValue ""
/// A git command as `who`, signing with `key` ("" = unsigned).
let gitAsKey (who: string) (key: string) dir a =
    let signing = if key = "" then [ "-c"; "commit.gpgsign=false" ] else [ "-c"; "commit.gpgsign=true"; "-c"; "gpg.format=openpgp"; "-c"; $"user.signingkey={key}"; "-c"; "gpg.program=gpg" ]
    runEnv [ "GNUPGHOME", keyring ] dir "git" ([ "-c"; $"user.name={who}"; "-c"; $"user.email={who}@example.invalid" ] @ signing @ a)
let mutable ownerKey = ""
let mutable agentKey = ""
let git dir a = gitAsKey "kemiller2002" ownerKey dir a
let gitAs (who: string) dir a = gitAsKey who (if who = "kemiller2002" then ownerKey else agentKey) dir a
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
let agentCommitPush f (message: string) =
    gitAsKey "claude" agentKey f.Indy [ "add"; "-A" ] |> ignore
    gitAsKey "claude" agentKey f.Indy [ "commit"; "-q"; "-m"; message; "-m"; "[unsigned]" ] |> ignore
    git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore

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
    let _, armored = gpg [ "--armor"; "--export"; ownerKey ]
    write (Path.Combine(f.Runs, "gpg-keys.json")) (Text.Json.JsonSerializer.Serialize [| armored |])
    write (Path.Combine(f.Runs, "path-4242")) ".github/workflows/live-provider-preflight.yml\n"
    write (Path.Combine(f.Runs, "path-777")) ".github/workflows/preflight-tooling.yml\n"
    write (Path.Combine(f.Runs, "run-777.json")) $"{{\"workflowName\":\"Preflight tooling\",\"headBranch\":\"main\",\"event\":\"push\",\"conclusion\":\"success\",\"headSha\":\"{h1}\"}}\n"
    write (Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "cases.tsv")) "case\ttitle\tverdict\nC01\tzero tests\tVALIDATED\nC02\tgate\tVALIDATED\n"
    let stub =
        [ @"#!/bin/sh"
          @"# gh stand-in (fixture). api: GitHub's verdict on a commit (valid, committer = git's committer name, unless marked"
          @"# [unsigned] or carrying an agent trailer) or a run's workflow file; run view: a run's metadata; run download: its artifact."
          @"if [ ""$1"" = api ]; then"
          @"  case ""$2"" in */actions/runs/*) cat '@RUNS@'/path-${2##*/} 2>/dev/null || exit 1; exit 0 ;; esac"
          @"  case ""$2"" in */commits/*/pulls) p=${2%/pulls}; cat '@RUNS@'/pulls-${p##*/} 2>/dev/null || echo '[]'; exit 0 ;; esac"
          @"  case ""$2"" in */pulls/*) cat '@RUNS@'/pr-${2##*/} 2>/dev/null || exit 1; exit 0 ;; esac"
          @"  case ""$2"" in users/*/gpg_keys) cat '@RUNS@'/gpg-keys.json; exit 0 ;; users/*/ssh_signing_keys) echo '[]'; exit 0 ;; esac"
          @"  c=${2##*/}; msg=$(git -C '@INDY@' show -s --format=%B ""$c"" 2>/dev/null) || exit 1"
          @"  committer=$(git -C '@INDY@' show -s --format=%cn ""$c"")"
          @"  case ""$msg"" in *Claude-Session:*|*'[unsigned]'*) v=false ;; *) v=true ;; esac"
          @"  printf '[%s,""valid"",""%s"",""""]\n' ""$v"" ""$committer""; exit 0"
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
      case "agent-signed-commit-with-the-owner-as-author" "LP-05" (fun f ->
          // signed and verified, but by the agent's own key: GitHub reports the agent as committer
          replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\" "
          git f.Indy [ "add"; "-A" ] |> ignore
          gitAs "claude" f.Indy [ "commit"; "-q"; "--author"; "kemiller2002 <kemiller2002@example.invalid>"; "-m"; "Owner decisions" ] |> ignore
          git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore)
      case "agent-key-signs-as-the-owner-committer" "LP-05" (fun f ->
          // GitHub (stand-in) is fooled into reporting the owner as committer; the signature is the agent's key
          replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\" "
          gitAsKey "kemiller2002" agentKey f.Indy [ "add"; "-A" ] |> ignore
          gitAsKey "kemiller2002" agentKey f.Indy [ "commit"; "-qm"; "Owner decisions" ] |> ignore
          git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore)
      case "planted-git-config-fakes-local-verification" "LP-05" (fun f ->
          // the agent's key signs, and the checkout's config names a verifier that always says yes
          replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\" "
          gitAsKey "kemiller2002" agentKey f.Indy [ "add"; "-A" ] |> ignore
          gitAsKey "kemiller2002" agentKey f.Indy [ "commit"; "-qm"; "Owner decisions" ] |> ignore
          git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore
          git f.Indy [ "config"; "gpg.program"; "true" ] |> ignore
          git f.Indy [ "config"; "gpg.ssh.program"; "true" ] |> ignore)
      case "merge-rolls-back-an-owner-decision" "LP-05" (fun f ->
          // the owner's first record (A), then the owner revokes it (B); an agent merges the first record back in
          let _, o1 = git f.Indy [ "rev-list"; "--max-parents=0"; "HEAD" ]
          replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"B\""
          commitPush f "Owner decisions: revoke ODR-R10-05"
          git f.Indy [ "checkout"; o1.Trim(); "--"; "kickoff/rehearsal-007/approvals.causa.json" ] |> ignore
          gitAsKey "claude" agentKey f.Indy [ "add"; "-A" ] |> ignore
          let _, tree = git f.Indy [ "write-tree" ]
          let _, merge = gitAsKey "claude" agentKey f.Indy [ "commit-tree"; tree.Trim(); "-p"; "HEAD"; "-p"; o1.Trim(); "-m"; "merge" ]
          git f.Indy [ "reset"; "-q"; "--hard"; merge.Trim() ] |> ignore
          git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore)
      { Name = "owner-pr-merged-with-a-merge-commit"; Expect = "READY"; Check = ""; Args = (fun _ -> None)
        Edit = fun f ->
          // the owner's signed record on a branch, merged by GitHub (web-flow, unsigned): a legitimate flow
          git f.Indy [ "checkout"; "-q"; "-b"; "owner/decisions" ] |> ignore
          replaceIn (approvals f) "]}]}" "]},{\"id\":\"APR-R10-OWNER-2\",\"authority\":{\"kind\":\"operator\",\"id\":\"kemiller2002\"},\"changes\":[{\"decision_id\":\"ODR-R10-18\",\"action\":\"decide\",\"option\":\"A\",\"pins\":[]}]}]}"
          git f.Indy [ "add"; "-A" ] |> ignore
          git f.Indy [ "commit"; "-qm"; "Owner decisions ODR-R10-18 (APR-R10-OWNER-2)" ] |> ignore
          git f.Indy [ "checkout"; "-q"; "main" ] |> ignore
          gitAsKey "web-flow" "" f.Indy [ "merge"; "-q"; "--no-ff"; "-m"; "Merge pull request #1"; "owner/decisions" ] |> ignore
          git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore
          // GitHub records the owner as the one who merged the pull request
          let _, m = git f.Indy [ "rev-parse"; "HEAD" ]
          write (Path.Combine(f.Runs, "pulls-" + m.Trim())) "[1]\n"
          write (Path.Combine(f.Runs, "pr-1")) "[true,\"kemiller2002\"]\n" }
      case "abandoned-owner-branch-merged-by-someone-else" "LP-05" (fun f ->
          git f.Indy [ "checkout"; "-q"; "-b"; "owner/abandoned" ] |> ignore
          replaceIn (approvals f) "]}]}" "]},{\"id\":\"APR-R10-OWNER-2\",\"authority\":{\"kind\":\"operator\",\"id\":\"kemiller2002\"},\"changes\":[{\"decision_id\":\"ODR-R10-18\",\"action\":\"decide\",\"option\":\"A\",\"pins\":[]}]}]}"
          git f.Indy [ "add"; "-A" ] |> ignore
          git f.Indy [ "commit"; "-qm"; "Owner decisions (abandoned)" ] |> ignore
          git f.Indy [ "checkout"; "-q"; "main" ] |> ignore
          gitAsKey "web-flow" "" f.Indy [ "merge"; "-q"; "--no-ff"; "-m"; "Merge pull request #2"; "owner/abandoned" ] |> ignore
          git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore)
      case "graft-hides-an-agent-commit" "LP-05" (fun f ->
          // an agent's change, then an owner commit grafted onto the agent commit's parent so history skips it
          let _, before = git f.Indy [ "rev-parse"; "HEAD" ]
          replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\" "
          agentCommitPush f "decisions"
          write (Path.Combine(f.Indy, "NOTE.md")) "owner note\n"
          commitPush f "Owner note"
          let _, o = git f.Indy [ "rev-parse"; "HEAD" ]
          write (Path.Combine(f.Indy, ".git", "info", "grafts")) $"{o.Trim()} {before.Trim()}\n")
      case "record-planted-under-another-id" "LP-05" (fun f ->
          replaceIn (approvals f) "]}]}" "]},{\"id\":\"APR-X-1\",\"authority\":{\"kind\":\"operator\",\"id\":\"kemiller2002\"},\"changes\":[{\"decision_id\":\"ODR-R10-18\",\"action\":\"decide\",\"option\":\"A\",\"pins\":[]}]}]}"
          agentCommitPush f "decisions")
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
      case "live-run-of-another-workflow-file" "LP-08" (fun f -> write (Path.Combine(f.Runs, "path-4242")) ".github/workflows/lookalike.yml\n")
      case "live-run-failed" "LP-08" (fun f -> replaceIn (Path.Combine(f.Runs, "run-4242.json")) "\"conclusion\":\"success\"" "\"conclusion\":\"failure\"")
      case "live-run-of-another-commit" "LP-08" (fun f -> replaceIn (liveResults f) "\"candidate\":\"fixture-" "\"candidate\":\"fixture-0")
      case "live-run-before-the-journeys-changed" "LP-08" (fun f -> write (Path.Combine(f.Indy, "verification/journeys/j1.spec.mjs")) "// changed\n"; commitPush f "journeys")
      case "live-run-against-another-test-repository" "LP-08" (fun f -> replaceIn (Path.Combine(f.Runs, "4242", "live-provider-preflight", "run-info.json")) "causa-acceptance-r10" "someone-else")
      case "failed-gate-journey" "LP-08" (fun f -> replaceIn (liveResults f) "{\"journey\":\"J2\",\"engine\":\"webkit\",\"status\":\"passed\"}" "{\"journey\":\"J2\",\"engine\":\"webkit\",\"status\":\"failed\"}")
      case "duplicate-journey-row-hides-a-failure" "LP-08" (fun f -> replaceIn (liveResults f) "{\"journey\":\"J3\",\"engine\":\"webkit\",\"status\":\"passed\"}" "{\"journey\":\"J3\",\"engine\":\"webkit\",\"status\":\"passed\"},{\"journey\":\"J3\",\"engine\":\"webkit\",\"status\":\"failed\"}")
      // LP-09: the adversarial suite, from its CI run
      { case "no-adversarial-run" "LP-09" ignore with Args = without "--adversarial-run" }
      case "adversarial-run-of-another-workflow-file" "LP-09" (fun f -> write (Path.Combine(f.Runs, "path-777")) ".github/workflows/lookalike.yml\n")
      case "adversarial-run-on-a-branch" "LP-09" (fun f -> replaceIn (Path.Combine(f.Runs, "run-777.json")) "\"headBranch\":\"main\"" "\"headBranch\":\"agent-branch\"")
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
Directory.CreateDirectory keyring |> ignore
run work "chmod" [ "700"; keyring ] |> ignore
gpg [ "--quick-gen-key"; "kemiller2002 <kemiller2002@example.invalid>"; "ed25519"; "sign"; "never" ] |> ignore
gpg [ "--quick-gen-key"; "claude <claude@example.invalid>"; "ed25519"; "sign"; "never" ] |> ignore
ownerKey <- keyOf "kemiller2002"
agentKey <- keyOf "claude"
if ownerKey = "" || agentKey = "" then failwith "gpg could not create the fixture keys"
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
