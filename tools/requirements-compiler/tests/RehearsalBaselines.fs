/// EII-C validation: compiles the verification plan against the frozen Rehearsal 4 baseline and, independently,
/// against the baseline Rehearsal 5 pins, and compares both rehearsals' own committed derivation records and
/// derived requirement documents with the baseline, obligation by obligation. The rehearsal checkouts are only
/// read (files in the working tree at their checked-out commit; nothing is written to them).
///
///   dotnet run --project tools/requirements-compiler/tests -- --rehearsal-baselines \
///     --source . --r4 DIR --r5 DIR --out FILE
module RehearsalBaselines

open System
open System.IO
open System.Text.RegularExpressions
open Json

type Options = { Source: string; R4: string; R5: string; Out: string }

let r4Commit = "aab8f80c7743392e794b3b631542903ed4ed6f64"
let private sha (b: byte[]) = Git.sha256Hex b
let private shaText (s: string) = sha (Text.Encoding.UTF8.GetBytes s)

/// One derived requirement document of a rehearsal (Praxis RQ form): front matter plus a criterion table.
type Derived =
    { File: string
      Source: string
      Priority: string
      Rows: (string * string) list }

let private unquote (s: string) =
    let t = s.Trim()
    if t.Length >= 2 && t.StartsWith "\"" && t.EndsWith "\"" then t.Substring(1, t.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\") else t

/// Parsed here, independently of the rehearsals' own tools: `key: value` front matter lines, and table rows
/// `| <id> | <text> |` whose text unescapes `\|`.
let parseDerived (file: string) (text: string) : Derived =
    let lines = text.Replace("\r\n", "\n").Split('\n')
    let front key =
        lines |> Array.tryPick (fun l -> let m = Regex.Match(l, "^" + key + @":\s*(.*)$") in if m.Success then Some(unquote m.Groups.[1].Value) else None) |> Option.defaultValue ""
    let row (l: string) =
        let t = l.Trim()
        if not (t.StartsWith "| ") || not (t.EndsWith " |") || t.StartsWith "| ID |" || t.StartsWith "|---" then None
        else
            let inner = t.Substring(2, t.Length - 4)
            let sep = inner.IndexOf " | "
            if sep <= 0 then None else Some(inner.Substring(0, sep), inner.Substring(sep + 3).Replace("\\|", "|"))
    // Rehearsal 5 states the source in front matter; Rehearsal 4 only in the body sentence.
    let source =
        match front "source" with
        | "" -> lines |> Array.tryPick (fun l -> let m = Regex.Match(l, @"baseline requirement `([^`]+)`") in if m.Success then Some m.Groups.[1].Value else None) |> Option.defaultValue ""
        | s -> s
    { File = file; Source = source; Priority = front "priority"; Rows = lines |> Array.choose row |> List.ofArray }

let private praxisPriority (p: string option) =
    match p with
    | Some "P0" -> "critical"
    | Some "P1" -> "high"
    | _ -> "medium"

/// Derived documents against the baseline: every requirement carried once, priority mapped, every obligation
/// carried verbatim under its own id, nothing extra.
let compareDerived (baseline: Json) (docs: Derived list) =
    let reqs = arr "requirements" baseline
    let bySource = docs |> List.groupBy (fun d -> d.Source) |> Map.ofList
    let baseIds = reqs |> List.map (str "id") |> Set.ofList
    let differences =
        [ for r in reqs do
              let rid = str "id" r
              match bySource.TryFind rid with
              | None -> yield sprintf "%s: no derived document" rid
              | Some(_ :: _ :: _) -> yield sprintf "%s: more than one derived document" rid
              | Some [ d ] ->
                  if d.Priority <> praxisPriority (strOpt "priority" r) then
                      yield sprintf "%s: priority %A carried as %s" rid (strOpt "priority" r) d.Priority
                  let expected = arr "obligations" r |> List.map (fun o -> str "id" o, str "text" o)
                  if List.sort d.Rows = List.sort expected then ()
                  else
                      let missing = expected |> List.filter (fun (o, _) -> not (d.Rows |> List.exists (fst >> (=) o)))
                      let differ = expected |> List.filter (fun (o, t) -> d.Rows |> List.exists (fun (o2, t2) -> o2 = o && t2 <> t))
                      let extra = d.Rows |> List.filter (fun (o, _) -> not (expected |> List.exists (fst >> (=) o)))
                      for (o, _) in missing do yield sprintf "%s: obligation %s not carried" rid o
                      for (o, _) in differ do yield sprintf "%s: obligation %s text differs" rid o
                      for (o, _) in extra do yield sprintf "%s: extra criterion %s" rid o
                      if missing.IsEmpty && differ.IsEmpty && extra.IsEmpty then yield sprintf "%s: duplicated criteria" rid
              | Some [] -> ()
          for d in docs do
              if not (baseIds.Contains d.Source) then yield sprintf "%s: derived from %s, which is not a baseline requirement" d.File d.Source ]
    let p0 = reqs |> List.filter (fun r -> strOpt "priority" r = Some "P0")
    let p0Carried =
        p0
        |> List.filter (fun r ->
            match bySource.TryFind(str "id" r) with
            | Some [ d ] -> d.Priority = "critical" && List.sort d.Rows = List.sort (arr "obligations" r |> List.map (fun o -> str "id" o, str "text" o))
            | _ -> false)
    JObj
        [ "documents", jint docs.Length
          "requirementsCarried", jint (reqs |> List.filter (fun r -> bySource.ContainsKey(str "id" r)) |> List.length)
          "criticalDocuments", jint (docs |> List.filter (fun d -> d.Priority = "critical") |> List.length)
          "criteria", jint (docs |> List.sumBy (fun d -> d.Rows.Length))
          "p0RequirementsCarriedVerbatim", jint p0Carried.Length
          "p0ObligationsCarriedVerbatim", jint (p0Carried |> List.sumBy (fun r -> (arr "obligations" r).Length))
          "rowOrderDiffersFromBaseline",
          jint (
              reqs
              |> List.filter (fun r ->
                  let expected = arr "obligations" r |> List.map (fun o -> str "id" o, str "text" o)
                  match bySource.TryFind(str "id" r) with
                  | Some [ d ] -> List.sort d.Rows = List.sort expected && d.Rows <> expected
                  | _ -> false)
              |> List.length
          )
          "differences", jint differences.Length
          "differenceDetail", jstrs (List.truncate 50 differences) ]

let private readJson (path: string) = if File.Exists path then Some(parseFile path) else None
let private regexIn (path: string) (pattern: string) =
    if File.Exists path then
        let m = Regex.Match(File.ReadAllText path, pattern, RegexOptions.Multiline)
        if m.Success then Some m.Groups.[1].Value else None
    else None

/// What a rehearsal's own committed files say its baseline is.
let private pins (dir: string) =
    let record = readJson (Path.Combine(dir, "research", "requirements-preservation", "derivation-record.json"))
    let disposition = readJson (Path.Combine(dir, "research", "requirements-preservation", "disposition.json"))
    let b (j: Json option) = j |> Option.bind (field "baseline") |> Option.defaultValue JNull
    let ledgers =
        let root = Path.Combine(dir, "evidence", "acceptance")
        if Directory.Exists root then
            Directory.GetFiles(root, "ledger.json", SearchOption.AllDirectories)
            |> Array.sort
            |> Array.map (fun p ->
                let l = parseFile p
                Path.GetRelativePath(dir, p), field "requirements_baseline" l |> Option.bind (strOpt "sha256"), field "verification_plan" l |> Option.bind (strOpt "planId"))
            |> List.ofArray
        else []
    let shas =
        [ "derivation-record.json baseline.sha256", strOpt "sha256" (b record)
          "disposition.json baseline.sha256", strOpt "sha256" (b disposition)
          "tools/acceptance/ci/acceptance.sh BASELINE_SHA256", regexIn (Path.Combine(dir, "tools", "acceptance", "ci", "acceptance.sh")) @"^BASELINE_SHA256=([0-9a-f]{64})"
          "tools/acceptance/verify.sh BASELINE_SHA256", regexIn (Path.Combine(dir, "tools", "acceptance", "verify.sh")) @"--baseline-sha256 ([0-9a-f]{64})" ]
        @ (ledgers |> List.map (fun (p, s, _) -> p + " requirements_baseline.sha256", s))
        |> List.choose (fun (k, v) -> v |> Option.map (fun x -> k, x))
    let commits = [ strOpt "commit" (b record); strOpt "commit" (b disposition) ] |> List.choose id |> List.distinct
    let ids = [ strOpt "id" (b record); strOpt "id" (b disposition) ] |> List.choose id |> List.distinct
    shas, commits, ids, record, ledgers |> List.choose (fun (_, _, p) -> p) |> List.distinct

let private derivedDocs (dir: string) =
    let d = Path.Combine(dir, "research", "requirements")
    if Directory.Exists d then
        Directory.GetFiles(d, "*.md") |> Array.sort |> Array.map (fun f -> parseDerived (Path.GetFileName f) (File.ReadAllText f)) |> List.ofArray
    else []

let private planSummary (plan: Json) =
    let scope = field "scope" plan |> Option.defaultValue JNull
    let os = arr "requirements" plan |> List.collect (arr "obligations")
    JObj
        [ "planId", JStr(field "planIdentity" plan |> Option.map (str "planId") |> Option.defaultValue "")
          "digest", JStr(field "planIdentity" plan |> Option.map (str "digest") |> Option.defaultValue "")
          "requirements", JInt(intOpt "requirements" scope |> Option.defaultValue -1L)
          "obligations", JInt(intOpt "obligations" scope |> Option.defaultValue -1L)
          "obligationsWithAcceptanceContract", jint (os |> List.filter (fun o -> (field "acceptanceContract" o).IsSome) |> List.length)
          "unclassifiedObligations", JInt(intOpt "unclassifiedObligations" scope |> Option.defaultValue -1L)
          "verifiedObligations", JInt(intOpt "verifiedObligations" scope |> Option.defaultValue -1L)
          "boundObligations", JInt(intOpt "boundObligations" scope |> Option.defaultValue -1L)
          "obligationsByCategory", field "obligationsByCategory" scope |> Option.defaultValue JNull ]

let run (o: Options) (buildPlan: string -> Json) : int =
    let gitText (rev: string) (path: string) =
        match Git.showFile o.Source rev path with
        | Ok b -> Text.utf8 b
        | Error e -> failwithf "cannot read %s at %s: %s" path rev e

    // R4: the frozen baseline, regenerated from source by the F# port at the R4 freeze commit.
    let r4Text = Baseline.build (gitText r4Commit) r4Commit |> serializePythonStyle
    let r4Sha = shaText r4Text
    let r4Baseline = parseBytes (Text.Encoding.UTF8.GetBytes r4Text)
    let r4Shas, r4Commits, r4Ids, _, r4Plans = pins o.R4

    // R5: the baseline file at the Indy-init ref R5's CI pins (INDY_INIT_REF), read from git, checked against R5's own pins.
    let r5Workflow = Path.Combine(o.R5, ".github", "workflows", "build-and-test.yml")
    let r5Ref = regexIn r5Workflow @"^\s*INDY_INIT_REF:\s*([0-9a-f]{40})" |> Option.defaultWith (fun () -> failwith "R5 INDY_INIT_REF not found")
    let r5Text = gitText r5Ref "requirements/causa-requirements-baseline.json"
    let r5Sha = shaText r5Text
    let r5Baseline = parseBytes (Text.Encoding.UTF8.GetBytes r5Text)
    let r5Shas, r5Commits, r5Ids, r5Record, r5Plans = pins o.R5

    let r4Plan = buildPlan r4Text
    let r5Plan = buildPlan r5Text
    let p0 (b: Json) = VerificationPlan.p0Of b
    let obligations (b: Json) = p0 b |> List.sumBy (fun r -> (arr "obligations" r).Length)
    let planIds (p: Json) = arr "requirements" p |> List.map (str "requirement")
    let pres (b: Json) (p: Json) = VerificationPlan.preservationProblems b p

    let r4Derived = compareDerived r4Baseline (derivedDocs o.R4)
    let r5Derived = compareDerived r5Baseline (derivedDocs o.R5)
    let identical = r4Text = r5Text
    let gitHead (dir: string) = match Git.headCommit dir with Ok s -> s | Error e -> "unknown: " + e

    let checks =
        [ "R4 regenerated baseline sha256 equals every R4 pin", r4Shas |> List.forall (fun (_, s) -> s = r4Sha) && not r4Shas.IsEmpty
          "R5 baseline sha256 equals every R5 pin", r5Shas |> List.forall (fun (_, s) -> s = r5Sha) && not r5Shas.IsEmpty
          "R4 baseline has 108 P0 requirements", (p0 r4Baseline).Length = 108
          "R4 plan carries all 108 P0 requirements, in baseline order", planIds r4Plan = (p0 r4Baseline |> List.map (str "id"))
          "R4 plan carries every P0 obligation with an acceptance contract", (field "scope" r4Plan |> Option.bind (intOpt "obligations")) = Some(int64 (obligations r4Baseline))
          "R4 plan passes preservation", (pres r4Baseline r4Plan).IsEmpty
          "R5 plan carries all P0 requirements of R5's baseline", planIds r5Plan = (p0 r5Baseline |> List.map (str "id"))
          "R5 plan passes preservation", (pres r5Baseline r5Plan).IsEmpty ]

    let record =
        JObj
            [ "schema", JStr "indy-init.eii-c-baseline-validation/1"
              "purpose", JStr "EII-C requirement 5: the v2 verification plan compiled against the frozen Rehearsal 4 baseline and, independently, against the baseline Rehearsal 5 pins; both rehearsals' committed derivations compared with the baseline"
              "command", JStr "dotnet run -c Release --no-build --project tools/requirements-compiler/tests -- --rehearsal-baselines --source . --r4 <indy-init-rehearsal-004 clone> --r5 <indy-init-rehearsal-005 clone> --out <this file>"
              "rehearsal4",
              JObj
                  [ "checkout", JObj [ "repository", JStr "kemiller2002/indy-init-rehearsal-004"; "head", JStr(gitHead o.R4) ]
                    "baseline",
                    JObj
                        [ "source", JStr(sprintf "regenerated by the F# baseline port from kemiller2002/indy-init at %s" r4Commit)
                          "sha256", JStr r4Sha
                          "id", JStr(str "id" r4Baseline)
                          "requirements", jint (arr "requirements" r4Baseline).Length
                          "p0Requirements", jint (p0 r4Baseline).Length
                          "p0Obligations", jint (obligations r4Baseline) ]
                    "pins", r4Shas |> List.map (fun (k, v) -> JObj [ "where", JStr k; "sha256", JStr v; "matches", JBool(v = r4Sha) ]) |> JArr
                    "pinnedCommits", jstrs r4Commits
                    "pinnedIds", jstrs r4Ids
                    "plansNamedByLedgers", jstrs r4Plans
                    "derivedDocuments", r4Derived
                    "plan", planSummary r4Plan
                    "preservationProblems", jstrs (pres r4Baseline r4Plan) ]
              "rehearsal5",
              JObj
                  [ "checkout", JObj [ "repository", JStr "kemiller2002/indy-init-rehearsal-005"; "head", JStr(gitHead o.R5) ]
                    "baseline",
                    JObj
                        [ "source", JStr(sprintf "kemiller2002/indy-init %s:requirements/causa-requirements-baseline.json (the INDY_INIT_REF of R5's .github/workflows/build-and-test.yml)" r5Ref)
                          "sha256", JStr r5Sha
                          "id", JStr(str "id" r5Baseline)
                          "sourceCommit", JStr(field "source" r5Baseline |> Option.map (str "commit") |> Option.defaultValue "")
                          "requirements", jint (arr "requirements" r5Baseline).Length
                          "p0Requirements", jint (p0 r5Baseline).Length
                          "p0Obligations", jint (obligations r5Baseline) ]
                    "pins", r5Shas |> List.map (fun (k, v) -> JObj [ "where", JStr k; "sha256", JStr v; "matches", JBool(v = r5Sha) ]) |> JArr
                    "pinnedCommits", jstrs r5Commits
                    "pinnedIds", jstrs r5Ids
                    "plansNamedByLedgers", jstrs r5Plans
                    "derivationRecordCounts", r5Record |> Option.bind (field "counts") |> Option.defaultValue JNull
                    "derivedDocuments", r5Derived
                    "plan", planSummary r5Plan
                    "preservationProblems", jstrs (pres r5Baseline r5Plan) ]
              "comparison",
              JObj
                  [ "baselinesByteIdentical", JBool identical
                    "plansIdentical", JBool(serialize r4Plan = serialize r5Plan)
                    "differences",
                    jstrs
                        [ if not identical then yield "the R4 and R5 baseline bytes differ"
                          if serialize r4Plan <> serialize r5Plan then yield "the plans compiled from the two baselines differ"
                          if r4Plans <> r5Plans then
                              yield
                                  sprintf
                                      "the rehearsals' ledgers name different plans: R4 %s, R5 %s (this package recompiles the plan as revision 2; neither rehearsal used it)"
                                      (String.concat ", " r4Plans)
                                      (String.concat ", " r5Plans) ] ]
              "checks", checks |> List.map (fun (k, ok) -> JObj [ "check", JStr k; "pass", JBool ok ]) |> JArr
              "result", JStr(if checks |> List.forall snd then "PASS" else "FAIL") ]

    File.WriteAllText(o.Out, serialize record)
    checks |> List.iter (fun (k, ok) -> printfn "%s %s" (if ok then "PASS" else "FAIL") k)
    printfn "R4 baseline %s, R5 baseline %s, identical %b" r4Sha r5Sha identical
    if checks |> List.forall snd then 0 else 1
