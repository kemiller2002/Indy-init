/// Phase Eight replay: runs the verifier pipeline (tools/compute-acceptance-verdict.py, unmodified, plus the
/// plan assessment) over a frozen candidate's published evidence, then over negative controls made from
/// copies of that evidence. The candidate's evidence and repository are only read; every mutation is
/// applied to a temporary copy.
///
///   dotnet run --project tools/requirements-compiler/tests -- --replay \
///     --source . --evidence-root DIR --candidate-repo DIR --trusted-source-sha SHA --out FILE
module Replay

open System
open System.Diagnostics
open System.IO
open Json

type Options =
    { Source: string
      EvidenceRoot: string
      CandidateRepo: string
      TrustedSourceSha: string
      Out: string }

let private sha (b: byte[]) = Git.sha256Hex b
let private fileSha (p: string) = File.ReadAllBytes p |> sha

let rec private copyTree (src: string) (dst: string) =
    Directory.CreateDirectory dst |> ignore
    Directory.GetFiles src |> Array.iter (fun f -> File.Copy(f, Path.Combine(dst, Path.GetFileName f), true))
    Directory.GetDirectories src |> Array.iter (fun d -> copyTree d (Path.Combine(dst, Path.GetFileName d)))

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps when ps |> List.exists (fun (k, _) -> k = key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | JObj ps -> JObj(ps @ [ key, value ])
    | j -> j

let private rewrite (path: string) (f: Json -> Json) = File.WriteAllText(path, serialize (f (parseFile path)))

/// Re-pins one manifest entry to the current bytes of its file (models a producer that re-hashed a stale artifact).
let private repin (dir: string) (rel: string) =
    let digest = fileSha (Path.Combine(dir, rel))
    rewrite (Path.Combine(dir, "evidence-manifest.json")) (fun m ->
        setIn "entries" (JArr(arr "entries" m |> List.map (fun e -> if str "path" e = rel then setIn "sha256" (JStr digest) e else e))) m)

let private mapRows (f: Json -> Json option) (ledger: Json) =
    setIn "requirements" (JArr(arr "requirements" ledger |> List.choose f)) ledger

type Case =
    { Name: string
      Mutation: string
      Mutate: string -> unit
      ExpectPython: string
      ExpectPlan: string
      /// Extra assertions over the plan assessment.
      Check: Json -> string list }

let private python (o: Options) (dir: string) =
    let baseline = Path.Combine(o.Source, "requirements", "causa-requirements-baseline.json")
    let approvals = Path.Combine(o.Source, "kickoff", "rehearsal-004", "approvals.causa.json")
    let manifest = Path.Combine(dir, "evidence-manifest.json")
    let args =
        [ "-B"; Path.Combine(o.Source, "tools", "compute-acceptance-verdict.py"); Path.Combine(dir, "ledger.json")
          "--repo"; o.CandidateRepo
          "--baseline"; baseline; "--baseline-sha256"; "621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9"
          "--baseline-source"; o.Source
          "--approvals"; approvals; "--approvals-sha256"; "3e70dcd2e87811c4f3744ee97585855639741c3ba7a63d98f4b842c7e86bace7"
          "--evidence-manifest"; manifest; "--evidence-manifest-sha256"; fileSha manifest
          "--evidence-root"; dir
          "--trusted-source-sha"; o.TrustedSourceSha
          "--trusted-operator-id"; "kemiller2002" ]
    let psi = ProcessStartInfo("python3")
    args |> List.iter psi.ArgumentList.Add
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    use p = Process.Start psi
    let out = p.StandardOutput.ReadToEnd()
    p.StandardError.ReadToEnd() |> ignore
    p.WaitForExit()
    p.ExitCode, (try parseBytes (Text.Encoding.UTF8.GetBytes out) with _ -> JObj [ "verdict", JStr "UNREADABLE" ])

let private planAssessment (o: Options) (dir: string) =
    let read (rel: string) =
        let full = Path.Combine(dir, rel)
        if rel.Contains("..") || not (File.Exists full) then None else Some(File.ReadAllBytes full)
    let baselineBytes = File.ReadAllBytes(Path.Combine(o.Source, "requirements", "causa-requirements-baseline.json"))
    let manifest = File.ReadAllBytes(Path.Combine(dir, "evidence-manifest.json"))
    PlanVerification.assess
        { Plan = parseFile (Path.Combine(o.Source, "requirements-compiled", "v2", "verification-plan.json"))
          Baseline = parseBytes baselineBytes
          BaselineSha256 = sha baselineBytes
          Ledger = parseFile (Path.Combine(dir, "ledger.json"))
          ManifestRaw = manifest
          ExpectedManifestSha256 = sha manifest
          ReadEvidence = read
          TrustedSourceSha = o.TrustedSourceSha
          ApprovalsRaw = File.ReadAllBytes(Path.Combine(o.Source, "kickoff", "rehearsal-004", "approvals.causa.json"))
          TrustedOperators = [ "kemiller2002" ]
          ProcessCovered = Map.empty
          ReviewTagChanges = JNull }

let private statusOf (rid: string) (a: Json) =
    arr "requirements" a |> List.tryFind (fun r -> str "requirement" r = rid) |> Option.map (str "status") |> Option.defaultValue "?"

let private cases (ledgerFails: string list) (ledgerNotRun: string list) : Case list =
    let ledger d = Path.Combine(d, "ledger.json")
    let ev = "requirements/EXT-DATA-002.json"
    let olderCandidate = "5b48de2c56230a1d21daaf041ece6a4fb9ca2475"
    let hasIntegrity (needle: string) (a: Json) =
        if strs "integrityProblems" a |> List.exists (fun p -> p.Contains(needle, StringComparison.Ordinal)) then []
        else [ sprintf "expected an integrity problem containing '%s'" needle ]
    let hasCode (code: string) (subject: string) (a: Json) =
        if arr "diagnostics" a |> List.exists (fun d -> str "code" d = code && str "subject" d = subject) then []
        else [ sprintf "expected diagnostic %s for %s" code subject ]
    let noneVerified (a: Json) =
        let verified = arr "requirements" a |> List.collect (arr "obligations") |> List.filter (fun o -> strOpt "verificationStatus" o = Some "verified")
        if verified.IsEmpty then [] else [ sprintf "%d obligations verified without bound tests" verified.Length ]

    [ { Name = "r4-as-published"
        Mutation = "none: the evidence the R4 release-candidate job published for ab08b76"
        Mutate = ignore
        ExpectPython = "FAILED"
        // The R4 ledger names tests for every row, and the plan binds none: each claim is an incorrect test mapping.
        ExpectPlan = "INVALID"
        Check =
          fun a ->
              [ for rid in ledgerFails do
                    if statusOf rid a <> "FAIL" then yield sprintf "failed obligation did not stay failed: %s is %s" rid (statusOf rid a)
                for rid in ledgerNotRun do
                    if statusOf rid a <> "NOT_RUN" then yield sprintf "missing evidence did not stay missing: %s is %s" rid (statusOf rid a)
                if statusOf "EXT-DATA-002" a <> "INVALID" then yield "EXT-DATA-002 must be INVALID: its PASS cites tests the plan does not bind to its obligations"
                yield! hasCode "TEST-MAPPING" "EXT-DATA-002" a
                yield! noneVerified a
                if not (strs "integrityProblems" a).IsEmpty then yield sprintf "unexpected integrity problems %A" (strs "integrityProblems" a) ] }
      { Name = "evidence-digest-mutated"
        Mutation = "one byte appended to requirements/EXT-DATA-002.json; the manifest keeps the published digest"
        Mutate = fun d -> File.AppendAllText(Path.Combine(d, ev), " ")
        ExpectPython = "INVALID"
        ExpectPlan = "INVALID"
        Check = fun a -> if statusOf "EXT-DATA-002" a = "INVALID" then [] else [ "EXT-DATA-002 must be INVALID" ] }
      { Name = "unauthorized-exclusion"
        Mutation = "PF-005 (a FAIL) removed from the ledger and declared excluded with approval APR-INVENTED, which no operator approval contains"
        Mutate =
          fun d ->
              rewrite (ledger d) (fun l ->
                  l
                  |> mapRows (fun r -> if str "id" r = "PF-005" then None else Some r)
                  |> setIn "scope_changes" (JArr [ JObj [ "requirement_id", JStr "PF-005"; "action", JStr "exclude"; "approval_id", JStr "APR-INVENTED" ] ]))
        ExpectPython = "INVALID"
        ExpectPlan = "INVALID"
        Check = fun a -> hasIntegrity "unauthorized exclusion of PF-005" a @ hasCode "UNAUTHORIZED-EXCLUSION" "PF-005" a @ (if statusOf "PF-005" a = "INVALID" then [] else [ "PF-005 must be INVALID" ]) }
      { Name = "stale-evidence-repinned"
        Mutation = "requirements/EXT-DATA-002.json rebound to the older candidate 5b48de2 and its manifest digest re-pinned (digest valid, evidence stale)"
        Mutate =
          fun d ->
              rewrite (Path.Combine(d, ev)) (setIn "source_sha" (JStr olderCandidate))
              repin d ev
        ExpectPython = "INVALID"
        ExpectPlan = "INVALID"
        Check = fun a -> hasCode "STALE-EVIDENCE" "EXT-DATA-002" a @ noneVerified a @ (if statusOf "EXT-DATA-002" a = "STALE" then [] else [ "EXT-DATA-002 must be STALE" ]) }
      { Name = "stale-manifest"
        Mutation = "evidence-manifest.json source_sha set to the older candidate 5b48de2"
        Mutate = fun d -> rewrite (Path.Combine(d, "evidence-manifest.json")) (setIn "source_sha" (JStr olderCandidate))
        ExpectPython = "INVALID"
        ExpectPlan = "INVALID"
        Check = fun a -> hasIntegrity "different source revision" a @ hasCode "STALE-EVIDENCE" "candidate" a }
      { Name = "evidence-removed"
        Mutation = "requirements/EXT-DATA-002.json deleted from the bundle (manifest unchanged)"
        Mutate = fun d -> File.Delete(Path.Combine(d, ev))
        ExpectPython = "INVALID"
        ExpectPlan = "INVALID"
        Check = fun a -> if statusOf "EXT-DATA-002" a = "MISSING" then [] else [ "EXT-DATA-002 must be MISSING" ] }
      { Name = "fail-relabelled-pass"
        Mutation = "PF-005 status changed from FAIL to PASS with no evidence"
        Mutate = fun d -> rewrite (ledger d) (mapRows (fun r -> Some(if str "id" r = "PF-005" then setIn "status" (JStr "PASS") r else r)))
        ExpectPython = "INVALID"
        ExpectPlan = "INVALID"
        Check = fun a -> if statusOf "PF-005" a = "MISSING" then [] else [ sprintf "PF-005 must be MISSING, is %s" (statusOf "PF-005" a) ] } ]

let run (o: Options) : int =
    let ledger = parseFile (Path.Combine(o.EvidenceRoot, "ledger.json"))
    let p0Rows status = arr "requirements" ledger |> List.filter (fun r -> strOpt "priority" r = Some "P0" && strOpt "status" r = Some status) |> List.map (str "id")
    let tmpRoot = Path.Combine(Path.GetTempPath(), "rc-replay")
    let mutable published = JNull

    let results =
        cases (p0Rows "FAIL") (p0Rows "NOT_RUN")
        |> List.map (fun c ->
            let dir = Path.Combine(tmpRoot, c.Name)
            if Directory.Exists dir then Directory.Delete(dir, true)
            copyTree o.EvidenceRoot dir
            c.Mutate dir
            let pyExit, py = python o dir
            let plan = planAssessment o dir
            if c.Name = "r4-as-published" then published <- plan
            let problems =
                [ if str "verdict" py <> c.ExpectPython then yield sprintf "compute-acceptance-verdict.py returned %s, expected %s" (str "verdict" py) c.ExpectPython
                  if str "verdict" plan <> c.ExpectPlan then yield sprintf "plan assessment returned %s, expected %s" (str "verdict" plan) c.ExpectPlan
                  if str "verdict" py = "ACCEPTED" || str "verdict" plan = "ACCEPTED" then yield "a verifier accepted the candidate"
                  yield! c.Check plan ]
            Directory.Delete(dir, true)
            printfn "%-26s python %-9s plan %-9s %s" c.Name (str "verdict" py) (str "verdict" plan) (if problems.IsEmpty then "as expected" else String.concat "; " problems)

            JObj
                [ "case", JStr c.Name
                  "mutation", JStr c.Mutation
                  "expected", JObj [ "computeAcceptanceVerdict", JStr c.ExpectPython; "planAssessment", JStr c.ExpectPlan ]
                  "computeAcceptanceVerdict",
                  JObj
                      [ "exit", jint pyExit
                        "verdict", JStr(str "verdict" py)
                        "nonPassReasons", jint (arr "reasons" py).Length
                        "integrityReasons",
                        jstrs (strs "reasons" py |> List.filter (fun r -> not (r.EndsWith(": NOT_RUN", StringComparison.Ordinal) || r.EndsWith(": FAIL", StringComparison.Ordinal)))) ]
                  "planAssessment",
                  JObj
                      [ "verdict", JStr(str "verdict" plan)
                        "summary", field "summary" plan |> Option.defaultValue JNull
                        "integrityProblems", JArr(arr "integrityProblems" plan) ]
                  "outcome", JStr(if problems.IsEmpty then "as-expected" else "UNEXPECTED")
                  "problems", jstrs problems ])

    let ok = results |> List.forall (fun r -> strOpt "outcome" r = Some "as-expected")
    let planJ = parseFile (Path.Combine(o.Source, "requirements-compiled", "v2", "verification-plan.json"))

    let record =
        JObj
            [ "schema", JStr "indy-init.verification-replay-evidence/1"
              "purpose", JStr "Phase Eight: the verifier pipeline (compute-acceptance-verdict.py unmodified, plus the verification-plan assessment against requirements-compiled/v2) re-run over the frozen Rehearsal 4 candidate evidence, with negative controls on temporary copies"
              "plan", field "planIdentity" planJ |> Option.defaultValue JNull
              "candidate",
              JObj
                  [ "repository", JStr "kemiller2002/indy-init-rehearsal-004"
                    "sourceSha", JStr o.TrustedSourceSha
                    "evidenceSource", JStr("branch acceptance-evidence, directory " + o.TrustedSourceSha + "/ (read with git archive; not modified)")
                    "ledgerSha256", JStr(fileSha (Path.Combine(o.EvidenceRoot, "ledger.json")))
                    "evidenceManifestSha256", JStr(fileSha (Path.Combine(o.EvidenceRoot, "evidence-manifest.json"))) ]
              "trustInputs",
              JObj
                  [ "baseline", JStr "requirements/causa-requirements-baseline.json (BL-CAUSA-INDY-INIT-aab8f80c7743)"
                    "baselineSha256", JStr(fileSha (Path.Combine(o.Source, "requirements", "causa-requirements-baseline.json")))
                    "approvals", JStr "kickoff/rehearsal-004/approvals.causa.json"
                    "approvalsSha256", JStr(fileSha (Path.Combine(o.Source, "kickoff", "rehearsal-004", "approvals.causa.json")))
                    "trustedOperator", JStr "kemiller2002" ]
              "command",
              JStr "dotnet run -c Release --no-build --project tools/requirements-compiler/tests -- --replay --source . --evidence-root <acceptance-evidence>/<sha> --candidate-repo <indy-init-rehearsal-004 clone> --trusted-source-sha <sha> --out <this file>"
              "result", JStr(if ok then "PASS: every case produced the expected verdicts; no case was accepted" else "FAIL: see cases")
              "cases", JArr results
              "publishedAssessment", published ]

    File.WriteAllText(o.Out, serialize record)
    printfn "replay: %s" (if ok then "all cases as expected" else "UNEXPECTED results")
    if ok then 0 else 1
