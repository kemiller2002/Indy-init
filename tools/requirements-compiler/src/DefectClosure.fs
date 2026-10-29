/// Defect-class closure verification (DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES DCC-005, DCC-006; Rehearsal 7
/// remediation R7-REM-008).
///
/// Rehearsal 6 failed G7 because `acceptance/defect-classes.json` in the candidate closed every class with fix
/// commits from Rehearsal 5, which are not in Rehearsal 6's history, cited a regression test (DCT-004-A) that no
/// longer existed, and was not re-reviewed before the freeze. A closure is evidence about this rehearsal's own
/// history, so it is checked against that history:
///
/// - DCL-001 the fix is a full 40-hex commit SHA;
/// - DCL-002 the fix commit is in this repository;
/// - DCL-003 the fix commit is the candidate or an ancestor of it;
/// - DCL-004 every regression test the curation names for the class is part of the closure;
/// - DCL-005 every regression test of the closure is present in the candidate's test tree;
/// - DCL-006 a records commit, when given, descends from the candidate;
/// - DCL-007 the review evidence file exists (in the records commit when given, else the candidate);
/// - DCL-008 the review evidence names the class and its fix commit;
/// - DCL-009 with a freeze time, the closure records `closedAt` no later than the freeze.
///
/// A class whose status is not PASS is OPEN: reported, not refused (G7 decides what an open class blocks). The
/// verdict is VERIFIED when no claimed closure is refused, REFUSED otherwise.
module DefectClosure

open System
open System.Globalization
open System.Text
open System.Text.RegularExpressions
open Json

type Inputs =
    { Repo: string
      Candidate: string
      /// A records-only descendant of the candidate holding the review evidence (D-12), if any.
      Records: string option
      Closures: Json
      /// Indy-init's `defect-class-tests.json` curation, naming each class's regression tests.
      Curation: Json option
      FreezeAt: DateTimeOffset option
      /// Where regression tests live in the candidate's tree. Searching the whole tree would find a test id in the
      /// closure record itself.
      TestsPath: string }

let private isFullSha (text: string) = Regex.IsMatch(text, "^[0-9a-f]{40}$")

/// True when `ancestor` is `descendant` or one of its ancestors.
let private isAncestor (repo: string) (ancestor: string) (descendant: string) =
    Git.run repo [ "merge-base"; "--is-ancestor"; ancestor; descendant ] |> Result.isOk

/// True when the literal `text` occurs on a line that is not a comment, in a tracked file of `commit` under `path`.
/// A comment is not a test: Rehearsal 6's candidate named DCT-004-A only in a comment saying it was superseded.
/// This is a tree check; the stronger evidence is the CI run's discovered tests (bindings-coverage).
let private occursAt (repo: string) (commit: string) (path: string) (text: string) =
    let comment (line: string) =
        let line = line.TrimStart()
        [ "//"; "#"; "*"; "/*"; "--"; "<!--"; "(*" ] |> List.exists (fun marker -> line.StartsWith(marker, StringComparison.Ordinal))

    match Git.run repo [ "grep"; "-h"; "-F"; "-e"; text; commit; "--"; path ] with
    | Ok bytes -> Encoding.UTF8.GetString(bytes).Split('\n') |> Array.exists (fun line -> line.Contains(text, StringComparison.Ordinal) && not (comment line))
    | Error _ -> false

let private curatedTests (curation: Json option) (classId: string) =
    curation
    |> Option.map (arr "tests" >> List.filter (fun t -> strOpt "defectClass" t = Some classId) >> List.map (str "id"))
    |> Option.defaultValue []

let private evidenceText (repo: string) (commit: string) (reference: string) =
    let path = reference.Split('#').[0]

    Git.showFile repo commit path
    |> Result.map Encoding.UTF8.GetString
    |> Result.toOption

let private closedAt (closure: Json) =
    strOpt "closedAt" closure
    |> Option.bind (fun text ->
        match DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) with
        | true, at -> Some at
        | _ -> None)

/// The problems of one claimed (PASS) closure.
let private problemsOf (inputs: Inputs) (recordsValid: bool) (closure: Json) : string list =
    let id = str "id" closure
    let fix = strOpt "fixSha" closure |> Option.defaultValue ""
    let tests = strs "tests" closure
    let evidence = strOpt "reviewerEvidence" closure |> Option.defaultValue ""
    let evidenceCommit = if recordsValid then inputs.Records |> Option.defaultValue inputs.Candidate else inputs.Candidate

    [ if not (isFullSha fix) then
          yield sprintf "DCL-001 %s: fix `%s` is not a full commit SHA" id fix
      elif not (Git.hasCommit inputs.Repo fix) then
          yield sprintf "DCL-002 %s: fix commit %s is not in this repository's history" id fix
      elif not (isAncestor inputs.Repo fix inputs.Candidate) then
          yield sprintf "DCL-003 %s: fix commit %s is not the candidate or one of its ancestors" id fix

      for test in curatedTests inputs.Curation id do
          if not (List.contains test tests) then
              yield sprintf "DCL-004 %s: curated regression test %s is not part of the closure" id test

      for test in tests do
          if not (occursAt inputs.Repo inputs.Candidate inputs.TestsPath test) then
              yield sprintf "DCL-005 %s: regression test %s is not present under %s/ in the candidate" id test inputs.TestsPath

      match evidence with
      | "" -> yield sprintf "DCL-007 %s: no review evidence is recorded" id
      | reference ->
          match evidenceText inputs.Repo evidenceCommit reference with
          | None -> yield sprintf "DCL-007 %s: review evidence %s does not exist at %s" id reference evidenceCommit
          | Some text ->
              let namesFix = isFullSha fix && text.Contains(fix.Substring(0, 7), StringComparison.Ordinal)

              if not (text.Contains(id, StringComparison.Ordinal) && namesFix) then
                  yield sprintf "DCL-008 %s: review evidence %s does not name the class and its fix commit" id reference

      match inputs.FreezeAt with
      | None -> ()
      | Some freeze ->
          match closedAt closure with
          | None -> yield sprintf "DCL-009 %s: no valid closedAt, so the closure cannot be shown to precede the freeze" id
          | Some at when at > freeze -> yield sprintf "DCL-009 %s: closed at %s, after the freeze at %s" id (at.ToString("o")) (freeze.ToString("o"))
          | Some _ -> () ]

/// The `indy-init.defect-closure-verification/1` report.
let assess (inputs: Inputs) : Json =
    let recordsProblems =
        match inputs.Records with
        | Some records when not (Git.hasCommit inputs.Repo records && isAncestor inputs.Repo inputs.Candidate records) ->
            [ sprintf "DCL-006 records commit %s does not descend from the candidate %s" records inputs.Candidate ]
        | _ -> []

    let classes =
        arr "defectClasses" inputs.Closures
        |> List.map (fun closure ->
            let id = str "id" closure

            if strOpt "status" closure <> Some "PASS" then
                id, "OPEN", []
            else
                let problems = recordsProblems @ problemsOf inputs recordsProblems.IsEmpty closure
                id, (if problems.IsEmpty then "VERIFIED" else "REFUSED"), problems)

    let count state = classes |> List.filter (fun (_, s, _) -> s = state) |> List.length
    let refused = count "REFUSED"

    JObj
        [ "schema", JStr "indy-init.defect-closure-verification/1"
          "candidate", JStr inputs.Candidate
          "records", jopt JStr inputs.Records
          "freezeAt", jopt (fun (at: DateTimeOffset) -> JStr(at.ToString("o"))) inputs.FreezeAt
          "verdict", JStr(if refused = 0 then "VERIFIED" else "REFUSED")
          "summary", JObj [ "verified", jint (count "VERIFIED"); "refused", jint refused; "open", jint (count "OPEN") ]
          "classes",
          JArr(
              classes
              |> List.map (fun (id, state, problems) -> JObj [ "id", JStr id; "state", JStr state; "problems", jstrs problems ])
          ) ]
