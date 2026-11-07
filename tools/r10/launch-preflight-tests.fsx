// launch-preflight-tests.fsx: launch-preflight's negative controls. A synthetic Indy-init (with its published
// origin), a published competition repository, and a stand-in for gh that verifies commit signatures and serves two
// CI runs (the live-provider preflight and the adversarial suite) satisfy every check (positive control: READY); then
// one mutation per refusal, each REFUSED by the check it names. Synthetic fixtures only: nothing real is read,
// written or fetched. The Rehearsal 010 cases (the default --run) come first; then the competition's (--run
// competition): its own positive control and one mutation per competition-specific refusal, and the offline mode,
// which is never READY.
//
// The stand-in treats a commit as signed by the owner when its author is the owner and its message carries neither an
// agent trailer nor "[unsigned]": a fixture's way of saying "a commit an agent made under the owner's name".
//
//   dotnet fsi tools/r10/launch-preflight-tests.fsx -- --indy-init DIR --work NEW-DIR [--evidence DIR] [--only NAME-PREFIX]

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

/// The stand-in for gh (fixture). api: GitHub's verdict on a commit or a run's workflow file; run view: a run's
/// metadata; run download: its artifact; release download: a release's checksums.txt.
let writeGh (dir: string) (f: Fixture) =
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
          @"  author=$(git -C '@INDY@' show -s --format=%an ""$c"")"
          @"  case ""$msg"" in *Claude-Session:*) t='Claude-Session:' ;; *) t='' ;; esac"
          @"  case ""$4"" in *verification*) printf '[%s,""valid"",""%s"",""%s""]\n' ""$v"" ""$committer"" ""$t"" ;; *) printf '[""%s"",""%s"",""%s""]\n' ""$committer"" ""$author"" ""$t"" ;; esac; exit 0"
          @"fi"
          @"case ""$1 $2"" in"
          @"  ""run view"") cat '@RUNS@'/run-$3.json 2>/dev/null || exit 1 ;;"
          @"  ""run download"") id=$3; while [ ""$1"" != ""--dir"" ]; do shift; done; cp -r '@RUNS@'/$id/. ""$2"" ;;"
          @"  ""release download"") while [ ""$1"" != ""--dir"" ]; do shift; done; cp '@RUNS@'/checksums.txt ""$2""/ 2>/dev/null || exit 1 ;;"
          @"  *) exit 2 ;;"
          @"esac"
          @"" ]
        |> String.concat "\n"
    write f.Gh (stub.Replace("@INDY@", f.Indy).Replace("@RUNS@", f.Runs))
    run dir "chmod" [ "+x"; f.Gh ] |> ignore

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
          "requirements-compiled/v4.1.5/package-manifest.json", "{\"package\":\"4.1.5\"}\n"
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
        [ "{\"decision_id\":\"ODR-V415-01\",\"action\":\"select\",\"option\":\"A\"}"
          "{\"decision_id\":\"ODR-V415-03\",\"action\":\"decide\",\"option\":\"A\"}"
          "{\"decision_id\":\"ODR-V415-04\",\"action\":\"decide\",\"option\":\"A\"}"
          "{\"decision_id\":\"ODR-V415-IF-VIEW\",\"action\":\"select\",\"option\":\"A\"}"
          "{\"decision_id\":\"ODR-V415-02\",\"action\":\"approve-package\",\"package\":\"requirements-compiled/v4.1.5\"}" ]
        @ (decisions |> List.map change)
    write (Path.Combine(f.Indy, "kickoff/rehearsal-007/approvals.causa.json"))
        ("{\"approvals\":[{\"id\":\"APR-R10-OWNER-1\",\"authority\":{\"kind\":\"operator\",\"id\":\"kemiller2002\"},\"changes\":[\n" + String.Join(",\n", changes) + "\n]}]}\n")
    run dir "git" [ "init"; "-q"; "--bare"; "-b"; "main"; f.Origin ] |> ignore
    git f.Indy [ "init"; "-q"; "-b"; "main" ] |> ignore
    git f.Indy [ "remote"; "add"; "origin"; f.Origin ] |> ignore
    commitPush f "Owner decisions (fixture)"
    git f.Indy [ "tag"; "indy-init-requirements-v4.1.5" ] |> ignore
    git f.Indy [ "push"; "-q"; "origin"; "indy-init-requirements-v4.1.5" ] |> ignore
    let _, h1 = git f.Indy [ "rev-parse"; "HEAD" ]
    let h1 = h1.Trim()
    write (Path.Combine(f.Indy, "kickoff/rehearsal-010/toolchain.lock.json"))
        ($"""{{"status":"OWNER-PINNED","requirementsPackage":{{"path":"requirements-compiled/v4.1.5","packageVersion":"4.1.5","tag":"indy-init-requirements-v4.1.5","tagCommit":"{h1}","manifestSha256":"{digest "requirements-compiled/v4.1.5/package-manifest.json"}"}},
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
    // The layout CI publishes: one table per adversarial shard, each holding only its own cases.
    write (Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "adversarial-shard-0", "cases.tsv")) "case\ttitle\tverdict\nC01\tzero tests\tVALIDATED\n"
    write (Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "adversarial-shard-1", "cases.tsv")) "case\ttitle\tverdict\nC02\tgate\tVALIDATED\n"
    writeGh dir f
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
/// The default arguments with --require-signatures true (the signed mode).
let signed (f: Fixture) =
    Some [ "--indy-init"; f.Indy; "--origin"; f.Origin; "--competition"; f.Competition; "--live-run"; "4242"; "--adversarial-run"; "777"; "--gh"; f.Gh; "--require-signatures"; "true" ]
let lockFile f = Path.Combine(f.Indy, "kickoff/rehearsal-010/toolchain.lock.json")
let liveResults f = Path.Combine(f.Runs, "4242", "live-provider-preflight", "journey-results.json")
let cases =
    [ { Name = "positive-control"; Expect = "READY"; Check = ""; Edit = ignore; Args = fun _ -> None }
      { Name = "positive-control-when-signatures-are-required"; Expect = "READY"; Check = ""; Edit = ignore; Args = signed }
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
      { case "agent-key-signs-as-the-owner-committer-when-signatures-are-required" "LP-05" (fun f ->
          // GitHub (stand-in) is fooled into reporting the owner as committer; the signature is the agent's key
          replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\" "
          gitAsKey "kemiller2002" agentKey f.Indy [ "add"; "-A" ] |> ignore
          gitAsKey "kemiller2002" agentKey f.Indy [ "commit"; "-qm"; "Owner decisions" ] |> ignore
          git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore) with Args = signed }
      { case "planted-git-config-fakes-local-verification-when-signatures-are-required" "LP-05" (fun f ->
          // the agent's key signs, and the checkout's config names a verifier that always says yes
          replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\" "
          gitAsKey "kemiller2002" agentKey f.Indy [ "add"; "-A" ] |> ignore
          gitAsKey "kemiller2002" agentKey f.Indy [ "commit"; "-qm"; "Owner decisions" ] |> ignore
          git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore
          git f.Indy [ "config"; "gpg.program"; "true" ] |> ignore
          git f.Indy [ "config"; "gpg.ssh.program"; "true" ] |> ignore) with Args = signed }
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
      // ACCEPTED RISK (the owner's decision not to sign commits): a commit an agent makes under the owner's name and email
      // is the owner's by GitHub's record, so the unsigned mode reads READY. The signed mode refuses it (above).
      { Name = "accepted-risk-agent-commit-under-the-owners-name"; Expect = "READY"; Check = ""; Args = (fun _ -> None)
        Edit = fun f ->
          replaceIn (approvals f) "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-R10-05\",\"action\":\"decide\",\"option\":\"A\" "
          gitAsKey "kemiller2002" agentKey f.Indy [ "add"; "-A" ] |> ignore
          gitAsKey "kemiller2002" agentKey f.Indy [ "commit"; "-qm"; "Owner decisions" ] |> ignore
          git f.Indy [ "push"; "-q"; "origin"; "main" ] |> ignore }
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
      case "v4.1.5-not-tagged-on-origin" "LP-02" (fun f -> git f.Indy [ "push"; "-q"; "origin"; ":refs/tags/indy-init-requirements-v4.1.5" ] |> ignore)
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
      case "adversarial-case-not-validated" "LP-09" (fun f -> replaceIn (Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "adversarial-shard-1", "cases.tsv")) "C02\tgate\tVALIDATED" "C02\tgate\tFAILED")
      case "adversarial-case-missing" "LP-09" (fun f -> replaceIn (Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "adversarial-shard-1", "cases.tsv")) "C02\tgate\tVALIDATED\n" "")
      case "adversarial-shard-table-missing" "LP-09" (fun f -> File.Delete(Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "adversarial-shard-1", "cases.tsv")))
      case "adversarial-no-shard-tables" "LP-09" (fun f -> Directory.Delete(Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence"), true))
      case "adversarial-duplicate-row-hides-a-failure" "LP-09" (fun f ->
          write (Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "adversarial-shard-2", "cases.tsv")) "case\ttitle\tverdict\nC02\tgate\tFAILED\n")
      case "adversarial-table-outside-the-shards" "LP-09" (fun f ->
          File.Delete(Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "adversarial-shard-1", "cases.tsv"))
          write (Path.Combine(f.Runs, "777", "r10-adversarial", "launch-evidence", "cases.tsv")) "case\ttitle\tverdict\nC02\tgate\tVALIDATED\n")
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

// ---- The competition (--run competition) ----

let compLockRel = "kickoff/competition/toolchain.lock.json"
let compProposalRel = "kickoff/competition/toolchain.lock.proposed.json"
/// Conditor v0.10.21's artifacts as the fixture's release publishes them (checksums.txt) and the lock pins them.
let conditorArtifacts = [ "linux-x64", "conditor-linux-x64", sha "linux-x64 binary"; "osx-arm64", "conditor-osx-arm64", sha "osx-arm64 binary" ]
/// The v4.1.6 decisions as the owner recorded them (APR-V416-OWNER-1).
let v416Decisions =
    [ "ODR-V416-01", "A"; "ODR-V416-RC1", "A"; "ODR-V416-S1", "B"; "ODR-V416-S8", "B"; "ODR-V416-RC3", "A"; "ODR-V416-RC4", "A"
      "ODR-V416-RC5", "A"; "ODR-V416-RC6", "A"; "ODR-V416-RC7", "A"; "ODR-V416-S2", "B"; "ODR-V416-S3", "A"; "ODR-V416-S4", "A"
      "ODR-V416-S5", "A"; "ODR-V416-S6", "A"; "ODR-V416-S7", "A"; "ODR-V416-S9", "A"; "ODR-V416-CAT", "A"; "ODR-V416-WDL013", "A"
      "ODR-V416-SCOPE-SELECTORS", "A"; "ODR-V416-FSCORE", "A" ]

/// The competition STARTUP's stack-drift step as the fixture has it: the ODR-COMP-P15 A command and the B waiver.
let compStartupRel = "docs/competition-readiness/STARTUP-INSTRUCTIONS.md"
let compStartup =
    "# Competition startup\n\n     dotnet fsi SRC/tools/stack-drift/stack-drift.fsx -- check --lock SRC/docs/competition-readiness/dependency-lock.json \\\n"
    + "     Only if the owner recorded ODR-COMP-P15 B instead: with ODR-COMP-P15 B the stack-drift preflight is waived for the competition.\n"
/// The competition's positive fixture (the owner's pre-created competition repository, v4.1.6 tagged, every decision
/// recorded, the approved proposal committed as the lock), then `edit`, then launch-preflight --run competition on it.
let competitionFixture (name: string) (edit: Fixture -> unit) (extraArgs: Fixture -> string list option) =
    let dir = Path.Combine(work, name)
    let f = { Indy = Path.Combine(dir, "Indy-init"); Origin = Path.Combine(dir, "origin.git"); Competition = Path.Combine(dir, "competition.git")
              CompetitionWork = Path.Combine(dir, "competition-work"); Runs = Path.Combine(dir, "runs"); Gh = Path.Combine(dir, "gh") }
    // The owner's pre-created competition repository: one README commit, which the lock pins (ODR-COMP-P07 A).
    Directory.CreateDirectory dir |> ignore
    run dir "git" [ "init"; "-q"; "--bare"; "-b"; "main"; f.Competition ] |> ignore
    git dir [ "init"; "-q"; "-b"; "main"; f.CompetitionWork ] |> ignore
    write (Path.Combine(f.CompetitionWork, "README.md")) "# causa\n"
    git f.CompetitionWork [ "add"; "-A" ] |> ignore
    git f.CompetitionWork [ "commit"; "-qm"; "Initial commit" ] |> ignore
    git f.CompetitionWork [ "push"; "-q"; f.Competition; "main" ] |> ignore
    let initial = (snd (git f.CompetitionWork [ "rev-parse"; "HEAD" ])).Trim()
    let files =
        [ "kickoff/competition/catalog-verification-contract.json", "{\"contract\":\"catalog v4.1.6\"}\n"
          "kickoff/rehearsal-010/design-contract.json", "{\"contract\":\"design\"}\n"
          "kickoff/rehearsal-010/deadline-policy.json", "{\"windowMinutes\":420}\n"
          "verification/journeys/bindings.json", "{\"journeys\":[\"J1\",\"J2\",\"J3\"]}\n"
          "kickoff/rehearsal-010/process-evidence-rules.json", "{\"repository\":\"indy-init-rehearsal-010\"}\n"
          "docs/v416-competition-decisions/proposals/owner-decision-requests.json", "{\"requests\":[{\"id\":\"ODR-COMP-SCOPE\"},{\"id\":\"ODR-COMP-LOCK\"}]}\n"
          "docs/v416-competition-decisions/TEST-DEPLOY-KEY.md", "# Test deploy key\n"
          "requirements-compiled/v4.1.6/package-manifest.json", "{\"package\":\"4.1.6\"}\n"
          "requirements-compiled/v4.1.6/verification-plan-supplement.json", "{\"supplement\":\"WDL-013\"}\n"
          "docs/competition-readiness/scope-manifest.json", "{\"scopeSha256\":\"comp-scope\",\"counts\":{\"requirements\":209}}\n"
          // The successor dependency lock and its source (ODR-COMP-P15), the assertion merge needs (ODR-COMP-P16), and the
          // STARTUP whose stack-drift command checks the lock's dependencyLock and which states the P15 B waiver.
          "docs/competition-readiness/dependency-lock.json", "{\"systems\":{\"conditor\":\"0.10.21\"}}\n"
          "docs/competition-readiness/dependency-lock.source.json", "{\"conditor\":\"v0.10.21\"}\n"
          "docs/competition-readiness/assertion-merge-needs.json", "{\"rules\":[{\"needs\":[\"SL-T22\"]}]}\n"
          "docs/competition-readiness/STARTUP-INSTRUCTIONS.md", compStartup
          "kickoff/rehearsal-010/toolchain.lock.proposed.json", "{\"testRepository\":" + testRepository + "}\n"
          "tools/r10/adversarial.fsx", "// fixture\n    [ { Id = \"C01\"; Title = \"a\" }\n      { Id = \"C02\"; Title = \"b\" } ]\n" ]
    files |> List.iter (fun (rel, text) -> write (Path.Combine(f.Indy, rel)) text)
    let digest rel = files |> List.find (fst >> (=) rel) |> snd |> sha
    run dir "git" [ "init"; "-q"; "--bare"; "-b"; "main"; f.Origin ] |> ignore
    git f.Indy [ "init"; "-q"; "-b"; "main" ] |> ignore
    git f.Indy [ "remote"; "add"; "origin"; f.Origin ] |> ignore
    commitPush f "v4.1.6 and the competition contracts (fixture)"
    git f.Indy [ "tag"; "indy-init-requirements-v4.1.6" ] |> ignore
    git f.Indy [ "push"; "-q"; "origin"; "indy-init-requirements-v4.1.6" ] |> ignore
    let h1 = (snd (git f.Indy [ "rev-parse"; "HEAD" ])).Trim()
    let lockText (status: string) (indyCommit: string) =
        let artifacts = String.Join(",", conditorArtifacts |> List.map (fun (k, n, s) -> $"\"{k}\":{{\"name\":\"{n}\",\"sha256\":\"{s}\"}}"))
        $"""{{"status":"{status}","indyInit":{{"repository":"kemiller2002/Indy-init","commit":"{indyCommit}"}},
  "conditor":{{"version":"0.10.21","tag":"v0.10.21","artifacts":{{{artifacts}}}}},
  "requirementsPackage":{{"path":"requirements-compiled/v4.1.6","packageVersion":"4.1.6","tag":"indy-init-requirements-v4.1.6","tagCommit":"{h1}","manifestSha256":"{digest "requirements-compiled/v4.1.6/package-manifest.json"}","verificationPlanSupplement":"requirements-compiled/v4.1.6/verification-plan-supplement.json","verificationPlanSupplementSha256":"{digest "requirements-compiled/v4.1.6/verification-plan-supplement.json"}"}},
  "scope":{{"manifest":"docs/competition-readiness/scope-manifest.json","scopeSha256":"comp-scope"}},
  "catalogVerification":{{"contract":"kickoff/competition/catalog-verification-contract.json","contractSha256":"{digest "kickoff/competition/catalog-verification-contract.json"}"}},
  "designContract":{{"contract":"kickoff/rehearsal-010/design-contract.json","contractSha256":"{digest "kickoff/rehearsal-010/design-contract.json"}"}},
  "deadlinePolicy":{{"path":"kickoff/rehearsal-010/deadline-policy.json","sha256":"{digest "kickoff/rehearsal-010/deadline-policy.json"}"}},
  "journeys":{{"bindings":"verification/journeys/bindings.json","bindingsSha256":"{digest "verification/journeys/bindings.json"}"}},
  "processEvidence":{{"rules":"kickoff/rehearsal-010/process-evidence-rules.json","rulesSha256":"{digest "kickoff/rehearsal-010/process-evidence-rules.json"}"}},
  "dependencyLock":{{"path":"docs/competition-readiness/dependency-lock.json","sha256":"{digest "docs/competition-readiness/dependency-lock.json"}","source":"docs/competition-readiness/dependency-lock.source.json"}},
  "assertionMergeNeeds":{{"path":"docs/competition-readiness/assertion-merge-needs.json","sha256":"{digest "docs/competition-readiness/assertion-merge-needs.json"}"}},
  "ownerDecisionRequests":{{"path":"docs/v416-competition-decisions/proposals/owner-decision-requests.json","sha256":"{digest "docs/v416-competition-decisions/proposals/owner-decision-requests.json"}"}},
  "resolvedSet":{{"expectedTools":{{"dokimos":"0.11.1"}}}},
  "testRepository":{testRepository},
  "competitionRepository":{{"owner":"kemiller2002","repo":"causa","url":{Text.Json.JsonSerializer.Serialize f.Competition},"initialCommit":"{initial}"}}}}
"""
    let proposal = lockText "PROPOSED: not an owner pin" "(the owner fills)"
    write (Path.Combine(f.Indy, compProposalRel)) proposal
    let pin (rel: string) (text: string) = $"{{\"path\":\"{rel}\",\"sha256\":\"{sha text}\"}}"
    let pinFile rel = pin rel (files |> List.find (fst >> (=) rel) |> snd)
    let field (p: string) (fieldName: string) (value: string) = $"{{\"path\":\"{p}\",\"field\":\"{fieldName}\",\"value\":{Text.Json.JsonSerializer.Serialize value},\"sha256\":\"x\"}}"
    let decide (id: string) (option: string) (pins: string list) = $"""{{"decision_id":"{id}","action":"decide","option":"{option}","pins":[{String.Join(',', pins)}]}}"""
    let approval (id: string) (changes: string list) = $"{{\"id\":\"{id}\",\"authority\":{{\"kind\":\"operator\",\"id\":\"kemiller2002\"}},\"changes\":[\n" + String.Join(",\n", changes) + "\n]}"
    let approvals =
        [ approval "APR-R10-OWNER-1"
              [ decide "ODR-R10-05" "A" [ pinFile "kickoff/rehearsal-010/design-contract.json" ]
                decide "ODR-R10-06" "A" [ pinFile "kickoff/rehearsal-010/deadline-policy.json" ]
                decide "ODR-R10-07" "A" [ pinFile "verification/journeys/bindings.json" ]
                decide "ODR-R10-09" "A" [ field "kickoff/rehearsal-010/toolchain.lock.proposed.json" "testRepository" testRepository ]
                decide "ODR-R10-14" "A" [ pinFile "kickoff/rehearsal-010/process-evidence-rules.json" ] ]
          approval "APR-V416-OWNER-1" (v416Decisions |> List.map (fun (id, o) -> decide id o []))
          approval "APR-V416-OWNER-2"
              [ $"""{{"decision_id":"ODR-V416-02","action":"approve-package","package":"requirements-compiled/v4.1.6","manifest_sha256":"{digest "requirements-compiled/v4.1.6/package-manifest.json"}"}}""" ]
          approval "APR-COMP-OWNER-1"
              [ for i in 1..14 -> decide $"ODR-COMP-P{i:D2}" "A" (if i = 13 then [ pinFile "docs/v416-competition-decisions/TEST-DEPLOY-KEY.md" ] else []) ]
          approval "APR-COMP-OWNER-2"
              [ decide "ODR-COMP-SCOPE" "A" [ field "docs/competition-readiness/scope-manifest.json" "scopeSha256" "\"comp-scope\"" ]
                decide "ODR-COMP-CATALOG-CONTRACT" "A" [ pinFile "kickoff/competition/catalog-verification-contract.json" ]
                decide "ODR-COMP-P15" "A" [ pinFile "docs/competition-readiness/dependency-lock.json"; pinFile "docs/competition-readiness/dependency-lock.source.json" ]
                decide "ODR-COMP-P16" "A" [ pinFile "docs/competition-readiness/assertion-merge-needs.json" ]
                decide "ODR-COMP-LOCK" "A" [ pin compProposalRel proposal ] ] ]
    write (Path.Combine(f.Indy, "kickoff/rehearsal-007/approvals.causa.json")) ("{\"approvals\":[" + String.Join(",\n", approvals) + "]}\n")
    commitPush f "Owner decisions (fixture)"
    let h2 = (snd (git f.Indy [ "rev-parse"; "HEAD" ])).Trim()
    write (Path.Combine(f.Indy, compLockRel)) (lockText "OWNER-PINNED" h2)
    commitPush f "Owner lock (fixture)"
    // The CI runs gh serves, of commit h1, and the Conditor release's checksums.txt.
    write (Path.Combine(f.Runs, "run-4242.json")) $"{{\"workflowName\":\"Live-provider preflight (Rehearsal 010)\",\"headBranch\":\"main\",\"event\":\"workflow_dispatch\",\"conclusion\":\"success\",\"headSha\":\"{h1}\"}}\n"
    let row j e = $"{{\"journey\":\"{j}\",\"engine\":\"{e}\",\"status\":\"passed\"}}"
    let rows = [ for j in [ "J1"; "J2"; "J3" ] do for e in [ "chromium"; "webkit" ] -> row j e ]
    write (Path.Combine(f.Runs, "4242", "live-provider-preflight", "journey-results.json"))
        ($"{{\"schema\":\"indy-init.journey-results/1\",\"candidate\":\"fixture-{h1}\",\"provider\":{{\"mode\":\"github\",\"baseUrl\":\"https://api.github.com\"}},\"results\":[" + String.Join(",", rows) + "]}\n")
    write (Path.Combine(f.Runs, "4242", "live-provider-preflight", "run-info.json")) ($"{{\"lock\":\"{compLockRel}\",\"sha\":\"{h1}\",\"testRepository\":{testRepository}}}\n")
    let _, armored = gpg [ "--armor"; "--export"; ownerKey ]
    write (Path.Combine(f.Runs, "gpg-keys.json")) (Text.Json.JsonSerializer.Serialize [| armored |])
    write (Path.Combine(f.Runs, "path-4242")) ".github/workflows/live-provider-preflight.yml\n"
    write (Path.Combine(f.Runs, "path-777")) ".github/workflows/preflight-tooling.yml\n"
    write (Path.Combine(f.Runs, "run-777.json")) $"{{\"workflowName\":\"Preflight tooling\",\"headBranch\":\"main\",\"event\":\"push\",\"conclusion\":\"success\",\"headSha\":\"{h1}\"}}\n"
    write (Path.Combine(f.Runs, "777", "r10-adversarial", "adv-evidence", "adversarial-shard-0", "cases.tsv")) "case\ttitle\tverdict\nC01\tzero tests\tVALIDATED\nC02\tgate\tVALIDATED\n"
    write (Path.Combine(f.Runs, "checksums.txt")) (String.Join("", conditorArtifacts |> List.map (fun (_, n, s) -> $"{s}  {n}\n")))
    writeGh dir f
    edit f
    // No --competition: the competition repository is the lock's (competitionRepository.url).
    let defaults = [ "--run"; "competition"; "--indy-init"; f.Indy; "--origin"; f.Origin; "--live-run"; "4242"; "--adversarial-run"; "777"; "--gh"; f.Gh ]
    let a = extraArgs f |> Option.defaultValue defaults
    let code, out = run work "dotnet" ([ "fsi"; preflight; "--" ] @ a)
    evidence |> Option.iter (fun d -> write (Path.Combine(d, name + ".log")) out)
    code, out

let compLock f = Path.Combine(f.Indy, compLockRel)
let compDefaults (f: Fixture) = [ "--run"; "competition"; "--indy-init"; f.Indy; "--origin"; f.Origin; "--live-run"; "4242"; "--adversarial-run"; "777"; "--gh"; f.Gh ]
let withArgs (extra: string list) (f: Fixture) = Some(compDefaults f @ extra)
let competitionCases =
    [ { Name = "competition-positive-control"; Expect = "READY"; Check = ""; Edit = ignore; Args = fun _ -> None }
      { Name = "competition-positive-control-when-signatures-are-required"; Expect = "READY"; Check = ""; Edit = ignore; Args = withArgs [ "--require-signatures"; "true" ] }
      // LP-05: the competition's decisions
      case "competition-missing-decision" "LP-05" (fun f -> replaceIn (approvals f) "\"decision_id\":\"ODR-COMP-LOCK\"" "\"decision_id\":\"ODR-COMP-LOCK-X\""; commitPush f "Owner decisions (edit)")
      case "competition-v416-decision-with-another-option" "LP-05" (fun f -> replaceIn (approvals f) "\"decision_id\":\"ODR-V416-S1\",\"action\":\"decide\",\"option\":\"B\"" "\"decision_id\":\"ODR-V416-S1\",\"action\":\"decide\",\"option\":\"A\""; commitPush f "Owner decisions (edit)")
      case "competition-package-approved-with-another-manifest" "LP-05" (fun f -> replaceIn (approvals f) "\"manifest_sha256\":\"" "\"manifest_sha256\":\"0"; commitPush f "Owner decisions (edit)")
      case "competition-decision-committed-by-an-agent" "LP-05" (fun f -> replaceIn (approvals f) "APR-COMP-OWNER-2" "APR-COMP-OWNER-3"; agentCommitPush f "decisions")
      case "competition-stale-scope-digest" "LP-05" (fun f ->
          // the scope is recompiled after ODR-COMP-SCOPE, and the lock follows it: the approval no longer covers it
          replaceIn (Path.Combine(f.Indy, "docs/competition-readiness/scope-manifest.json")) "comp-scope" "comp-scope-recompiled"
          replaceIn (compLock f) "\"scopeSha256\":\"comp-scope\"" "\"scopeSha256\":\"comp-scope-recompiled\""
          commitPush f "Owner lock (scope)")
      // LP-05 / LP-03: the successor dependency lock (ODR-COMP-P15) and the assertion merge needs (ODR-COMP-P16)
      case "competition-p15-missing" "LP-05" (fun f -> replaceIn (approvals f) "\"decision_id\":\"ODR-COMP-P15\"" "\"decision_id\":\"ODR-COMP-P15-X\""; commitPush f "Owner decisions (edit)")
      case "competition-p16-missing" "LP-05" (fun f -> replaceIn (approvals f) "\"decision_id\":\"ODR-COMP-P16\"" "\"decision_id\":\"ODR-COMP-P16-X\""; commitPush f "Owner decisions (edit)")
      case "competition-p16-with-option-b" "LP-05" (fun f -> replaceIn (approvals f) "\"decision_id\":\"ODR-COMP-P16\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-COMP-P16\",\"action\":\"decide\",\"option\":\"B\""; commitPush f "Owner decisions (edit)")
      { Name = "competition-p15-b-with-the-startup-waiver"; Expect = "READY"; Check = ""; Args = fun _ -> None
        Edit = fun f -> replaceIn (approvals f) "\"decision_id\":\"ODR-COMP-P15\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-COMP-P15\",\"action\":\"decide\",\"option\":\"B\""; commitPush f "Owner decisions (edit)" }
      case "competition-p15-b-without-the-startup-waiver" "LP-05" (fun f ->
          replaceIn (approvals f) "\"decision_id\":\"ODR-COMP-P15\",\"action\":\"decide\",\"option\":\"A\"" "\"decision_id\":\"ODR-COMP-P15\",\"action\":\"decide\",\"option\":\"B\""
          replaceIn (Path.Combine(f.Indy, compStartupRel)) "with ODR-COMP-P15 B the stack-drift preflight is waived" "with ODR-COMP-P15 B ask the owner"; commitPush f "Owner decisions (edit)")
      case "competition-p15-a-startup-checks-another-lock" "LP-05" (fun f ->
          replaceIn (Path.Combine(f.Indy, compStartupRel)) "--lock SRC/docs/competition-readiness/dependency-lock.json" "--lock SRC/docs/optimization/echelon-stack-upgrade/lock/dependency-lock.json"; commitPush f "startup")
      case "competition-dependency-lock-digest-wrong" "LP-03" (fun f -> write (Path.Combine(f.Indy, "docs/competition-readiness/dependency-lock.json")) "{\"systems\":{\"conditor\":\"0.10.20\"}}\n"; commitPush f "dependency lock")
      case "competition-dependency-lock-source-missing" "LP-03" (fun f -> File.Delete(Path.Combine(f.Indy, "docs/competition-readiness/dependency-lock.source.json")); commitPush f "no source")
      case "competition-assertion-needs-digest-wrong" "LP-03" (fun f -> write (Path.Combine(f.Indy, "docs/competition-readiness/assertion-merge-needs.json")) "{\"rules\":[{\"needs\":[\"SL-WI-GEN-UI\"]}]}\n"; commitPush f "needs")
      case "competition-lock-pins-no-dependency-lock" "LP-03" (fun f -> replaceIn (compLock f) "\"dependencyLock\":" "\"dependencyLockX\":"; commitPush f "lock")
      // LP-01..LP-04: the lock and what it pins
      case "competition-lock-not-committed" "LP-01" (fun f -> File.Delete(compLock f); commitPush f "no lock")
      case "competition-lock-still-proposed" "LP-01" (fun f -> replaceIn (compLock f) "OWNER-PINNED" "PROPOSED: not an owner pin"; commitPush f "lock")
      case "competition-wrong-package-version" "LP-02" (fun f -> replaceIn (compLock f) "\"packageVersion\":\"4.1.6\"" "\"packageVersion\":\"4.1.5\""; commitPush f "lock")
      case "competition-v4.1.6-not-tagged-on-origin" "LP-02" (fun f -> git f.Indy [ "push"; "-q"; "origin"; ":refs/tags/indy-init-requirements-v4.1.6" ] |> ignore)
      case "competition-altered-plan-supplement" "LP-03" (fun f -> write (Path.Combine(f.Indy, "requirements-compiled/v4.1.6/verification-plan-supplement.json")) "{\"supplement\":\"other\"}\n"; commitPush f "supplement")
      case "competition-scope-is-not-the-locked-one" "LP-04" (fun f -> replaceIn (Path.Combine(f.Indy, "docs/competition-readiness/scope-manifest.json")) "comp-scope" "comp-scope-other"; commitPush f "scope")
      // LP-07, LP-08, LP-10: the repositories and the live run
      case "competition-test-repository-is-the-competition-repository" "LP-07" (fun f -> replaceIn (compLock f) "\"repo\":\"causa\"" "\"repo\":\"causa-acceptance-r10\""; commitPush f "lock")
      case "competition-live-run-used-the-rehearsal-010-lock" "LP-08" (fun f -> replaceIn (Path.Combine(f.Runs, "4242", "live-provider-preflight", "run-info.json")) compLockRel "kickoff/rehearsal-010/toolchain.lock.json")
      case "competition-repository-already-bootstrapped" "LP-10" (fun f ->
          write (Path.Combine(f.CompetitionWork, "evidence", "t0", "t0.json")) "{\"t0\":\"2026-11-07T14:00:00Z\"}\n"
          git f.CompetitionWork [ "add"; "-A" ] |> ignore; git f.CompetitionWork [ "commit"; "-qm"; "T0 bootstrap" ] |> ignore
          git f.CompetitionWork [ "push"; "-q"; f.Competition; "main" ] |> ignore)
      case "competition-repository-replaced" "LP-10" (fun f ->
          // the repository was re-created: one commit, but not the owner's initial commit the lock pins
          git f.CompetitionWork [ "commit"; "-q"; "--amend"; "-m"; "Initial commit (again)" ] |> ignore
          git f.CompetitionWork [ "push"; "-q"; "--force"; f.Competition; "main" ] |> ignore)
      // LP-11: the approved proposal and Conditor
      case "competition-wrong-conditor-digest" "LP-11" (fun f ->
          let _, _, s = conditorArtifacts.Head
          replaceIn (compLock f) s (sha "another binary"); commitPush f "lock")
      case "competition-conditor-digest-not-the-release" "LP-11" (fun f ->
          // the lock is the approved proposal, but the proposal's digest is not the published release's
          let _, n, _ = conditorArtifacts.Head
          let released = sha "the released binary"
          write (Path.Combine(f.Runs, "checksums.txt")) (String.Join("", conditorArtifacts |> List.map (fun (_, n', s) -> $"{(if n' = n then released else s)}  {n'}\n"))))
      case "competition-conditor-release-unreadable" "LP-11" (fun f -> File.Delete(Path.Combine(f.Runs, "checksums.txt")))
      case "competition-lock-is-not-the-approved-proposal" "LP-11" (fun f -> replaceIn (compLock f) "\"dokimos\":\"0.11.1\"" "\"dokimos\":\"0.11.2\""; commitPush f "lock")
      case "competition-indy-init-commit-not-in-history" "LP-11" (fun f ->
          let h = (snd (git f.Indy [ "rev-parse"; "HEAD~1" ])).Trim()
          let zeros = String('0', 40)
          replaceIn (compLock f) $"\"commit\":\"{h}\"" $"\"commit\":\"{zeros}\""; commitPush f "lock")
      // The structural mode: never READY; the lock not yet committed is refused, the rest judged on the proposal.
      { Name = "competition-offline-is-never-ready"; Expect = "NOT READY"; Check = ""; Edit = ignore; Args = withArgs [ "--offline"; "true" ] }
      { case "competition-offline-lock-not-committed" "LP-01" (fun f -> File.Delete(compLock f); commitPush f "no lock") with Args = withArgs [ "--offline"; "true" ] } ]

if Directory.Exists work then failwithf "%s exists; give a new directory" work
Directory.CreateDirectory work |> ignore
Directory.CreateDirectory keyring |> ignore
run work "chmod" [ "700"; keyring ] |> ignore
gpg [ "--quick-gen-key"; "kemiller2002 <kemiller2002@example.invalid>"; "ed25519"; "sign"; "never" ] |> ignore
gpg [ "--quick-gen-key"; "claude <claude@example.invalid>"; "ed25519"; "sign"; "never" ] |> ignore
ownerKey <- keyOf "kemiller2002"
agentKey <- keyOf "claude"
if ownerKey = "" || agentKey = "" then failwith "gpg could not create the fixture keys"
let judge (fixtureOf: string -> (Fixture -> unit) -> (Fixture -> string list option) -> int * string) (c: Case) =
        let code, out = fixtureOf c.Name c.Edit c.Args
        let verdictOk =
            (c.Expect = "READY" && code = 0 && out.Contains "launch-preflight: READY") || (c.Expect = "REFUSED" && code = 4 && out.Contains "launch-preflight: REFUSED")
            || (c.Expect = "NOT READY" && code = 4 && out.Contains "launch-preflight: NOT READY" && not (out.Contains " REFUSED "))
        let refusedChecks = out.Split('\n') |> Array.filter (fun l -> l.Contains " REFUSED ") |> Array.map (fun l -> l.Trim().Split(' ').[0]) |> Set.ofArray
        let ok = verdictOk && (c.Expect <> "REFUSED" || refusedChecks.Contains c.Check)
        printfn "%-44s expected %-8s exit %d  refused by %-22s %s" c.Name c.Expect code (String.Join(",", refusedChecks)) (if ok then "PASS" else "FAIL <<<")
        ok
/// --only PREFIX runs only the cases whose name starts with it (for a quick local run; CI runs every case).
let selected (cs: Case list) = match opt "--only" with Some p -> cs |> List.filter (fun c -> c.Name.StartsWith p) | None -> cs
let outcomes = (selected cases |> List.map (judge fixture)) @ (selected competitionCases |> List.map (judge competitionFixture))
let failed = outcomes |> List.filter not |> List.length
printfn "%d case(s), %d failed" outcomes.Length failed
exit (if failed = 0 then 0 else 1)
