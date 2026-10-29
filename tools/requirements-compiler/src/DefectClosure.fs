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
/// - DCL-005 every regression test of the closure is declared, and not skipped, in the candidate's test tree: its id
///   occurs on word boundaries in a test title (JS `test(`/`it(`, F# ``let ``...`` ``), never in a comment, a string
///   elsewhere, a skipped form (`test.skip`, `test.fixme`, `it.skip`, `xit`, `[<Fact(Skip = ...)>]`, or a skip call
///   anywhere in the body or on the declaration line: `test.skip()`, `testInfo.skip()`, `test.info().skip()`, a
///   name bound to one; see TestBlocks), or as the prefix of a longer id (DCT-004-A is not DCT-004-AX). A test file
///   TestBlocks cannot read consistently (UNPARSEABLE: it ends inside a literal or comment, a string runs past its
///   line, brackets do not balance) declares nothing: a test named only there is refused, and any such file under the
///   tests path makes the verdict at best INCOMPLETE;
/// - DCL-006 a records commit, when given, descends from the candidate;
/// - DCL-007 the review evidence exists (in the records commit when given, else the candidate), is not the closures
///   record itself (compared as normalised repository paths: `./`, `a//b` and `x/../` collapsed, a path leaving the
///   root refused), is a review record (ReviewRecord: an `indy-init.independent-review/1` document naming a reviewer
///   and a verdict, not shaped like a closures record, so an edited copy of the closures is refused), and was added or
///   changed by a commit that descends from the fix commit;
/// - DCL-008 the review records the closure in a field, not in prose: a `defectClasses[]` entry with the class id
///   (`defectClass`), the full fix SHA (`fixSha`) and `verdict: "CLOSED"` (Rehearsal 6's final review lists the classes
///   it checked the same way, without closing any);
/// - DCL-009 with a freeze time, `closedAt` is no later than the freeze and no earlier than the fix commit;
/// - DCL-010 the fix commit descends from the base (kickoff) commit and is not the base: a commit from before the
///   rehearsal (the initial commit) fixes nothing in it. The base is bound by the kickoff record exactly as the
///   assertion ratchet's is (Kickoff): `--base` is optional, and one that is not the bound base REFUSES the run
///   (`baseVerified` false), as does a history without the record;
/// - DCL-011 the fix commit changes something other than the closures record;
/// - DCL-012 the closure names at least one regression test;
/// - DCL-013 the status is one of the known statuses (an unknown status is refused, not treated as open);
/// - DCL-014 every class the curation names appears in the closures (a dropped class is MISSING).
///
/// A class with a known status other than PASS is OPEN: reported, not refused (G7 decides what an open class blocks).
/// The verdict is REFUSED when any closure is refused or a curated class is missing; otherwise VERIFIED only when the
/// curation and a freeze time were supplied and there is at least one class, else INCOMPLETE: a gate never passes on
/// checks it was not given the inputs to make.
///
/// Inherent limits (history shows what changed, not why; also in every report's `limits`): any commit that descends
/// from the base and changes something can be named as a class's fix, though it fixed something else; review evidence
/// the implementing agent wrote itself in a review record's shape, including in a records commit, cannot be told from
/// an independent reviewer's without reviewer identity or an attestation (a signed review, a pull-request review
/// fetched by trusted CI); and history rewritten before the run moves the kickoff-bound base unless the kickoff commit
/// is pinned outside the candidate (an owner record or tag). They remain for the independent review.
module DefectClosure

open System
open System.Globalization
open System.Text
open System.Text.RegularExpressions
open Json

type Inputs =
    { Repo: string
      /// The base the caller expects; the kickoff record binds it (Kickoff): None takes the bound base, another one is
      /// REFUSED. Every fix is made after it.
      Base: string option
      Candidate: string
      /// A records-only descendant of the candidate holding the review evidence (D-12), if any.
      Records: string option
      Closures: Json
      /// Where the closures were read from (`<commit>:<path>` or a file), and the sha256 of their bytes.
      ClosuresSource: string
      ClosuresSha256: string
      /// The closures record's path in the repository: it cannot be its own review evidence.
      ClosuresPath: string
      /// Indy-init's `defect-class-tests.json` curation, naming each class's regression tests.
      Curation: Json option
      FreezeAt: DateTimeOffset option
      /// Where regression tests live in the candidate's tree. Searching the whole tree would find a test id in the
      /// closure record itself.
      TestsPath: string }

/// Statuses a closure may carry. Only PASS claims a closure.
let knownStatuses = set [ "PASS"; "FAIL"; "OPEN"; "NOT_RUN"; "BLOCKED" ]

let private isFullSha (text: string) = Regex.IsMatch(text, "^[0-9a-f]{40}$")

let private text (bytes: byte[]) = Encoding.UTF8.GetString bytes

let private lines (bytes: byte[]) =
    (text bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

/// True when `ancestor` is `descendant` or one of its ancestors.
let private isAncestor (repo: string) (ancestor: string) (descendant: string) =
    Git.run repo [ "merge-base"; "--is-ancestor"; ancestor; descendant ] |> Result.isOk

/// The full SHA of a commit-ish, if it names a commit of the repository.
let resolveCommit (repo: string) (rev: string) =
    Git.run repo [ "rev-parse"; "--verify"; "--quiet"; rev + "^{commit}" ] |> Result.map (fun b -> (text b).Trim()) |> Result.toOption

/// The tests under `path` at `commit`: the titles declared and not skipped, and the files that could not be read
/// consistently (UNPARSEABLE: path, problem, text), whose tests are known neither to be declared nor to run. Only spec
/// files hold JS tests a runner collects; every F# file under the path may declare tests.
type TestTree = { Declared: string list; Unparseable: (string * string * string) list }

let declaredTests (repo: string) (commit: string) (path: string) : TestTree =
    let prefix = path.TrimEnd('/') + "/"

    match Git.listFiles repo commit with
    | Error e -> failwithf "cannot list %s: %s" commit e
    | Ok files ->
        let readings =
            files
            |> List.filter (fun f ->
                f.StartsWith(prefix, StringComparison.Ordinal)
                && (match TestBlocks.languageOf f with
                    | Some TestBlocks.Js -> TestBlocks.isSpec f
                    | Some TestBlocks.FSharp -> true
                    | None -> false))
            |> List.choose (fun f ->
                match Git.showFile repo commit f with
                | Ok bytes -> TestBlocks.parse f (text bytes) |> Option.map (fun r -> f, text bytes, r)
                | Error _ -> None)

        { Declared =
            readings
            |> List.collect (function
                | _, _, Ok blocks -> blocks |> List.filter (fun b -> b.Kind = TestBlocks.Test && not b.Skipped) |> List.map _.Title
                | _ -> [])
          Unparseable = readings |> List.choose (function f, body, Error problem -> Some(f, problem, body) | _ -> None) }

/// True when `id` occurs in `title` as a whole id: not inside, or as the prefix of, a longer one.
let namesTest (id: string) (title: string) =
    Regex.IsMatch(title, @"(?<![A-Za-z0-9_]-?)" + Regex.Escape id + @"(?!-?[A-Za-z0-9_])")

let private curatedTests (curation: Json option) (classId: string) =
    curation
    |> Option.map (arr "tests" >> List.filter (fun t -> strOpt "defectClass" t = Some classId) >> List.map (str "id"))
    |> Option.defaultValue []

let private curatedClasses (curation: Json option) =
    curation
    |> Option.map (arr "tests" >> List.choose (strOpt "defectClass") >> List.distinct)
    |> Option.defaultValue []

/// True when a commit that descends from `fix` (and is in `commit`'s history) added or changed `path`.
let private changedAfter (repo: string) (fix: string) (commit: string) (path: string) =
    match Git.run repo [ "rev-list"; "--ancestry-path"; sprintf "%s..%s" fix commit; "--"; path ] with
    | Ok bytes -> not (lines bytes).IsEmpty
    | Error _ -> false

/// The paths a commit changes against its first parent (every path of a root commit).
let private changedBy (repo: string) (commit: string) =
    let args =
        match Git.run repo [ "rev-parse"; "--verify"; "--quiet"; commit + "^1" ] with
        | Ok _ -> [ "diff"; "--name-only"; commit + "^1"; commit ]
        | Error _ -> [ "show"; "--name-only"; "--format="; commit ]

    match Git.run repo args with
    | Ok bytes -> lines bytes
    | Error _ -> []

let private committedAt (repo: string) (commit: string) =
    Git.run repo [ "show"; "-s"; "--format=%cI"; commit ]
    |> Result.toOption
    |> Option.map (fun b -> DateTimeOffset.Parse((text b).Trim(), CultureInfo.InvariantCulture))

let private closedAt (closure: Json) =
    strOpt "closedAt" closure
    |> Option.bind (fun text ->
        match DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) with
        | true, at -> Some at
        | _ -> None)

/// The problems of one claimed (PASS) closure.
let private problemsOf (inputs: Inputs) (baseCommit: string option) (tree: TestTree) (recordsValid: bool) (closure: Json) : string list =
    let id = strOpt "id" closure |> Option.defaultValue ""
    let fix = strOpt "fixSha" closure |> Option.defaultValue ""
    let tests = strs "tests" closure
    let evidence = strOpt "reviewerEvidence" closure |> Option.defaultValue ""
    let evidenceCommit = if recordsValid then inputs.Records |> Option.defaultValue inputs.Candidate else inputs.Candidate
    let fixUsable = isFullSha fix && Git.hasCommit inputs.Repo fix

    [ if not (isFullSha fix) then
          yield sprintf "DCL-001 %s: fix `%s` is not a full commit SHA" id fix
      elif not (Git.hasCommit inputs.Repo fix) then
          yield sprintf "DCL-002 %s: fix commit %s is not in this repository's history" id fix
      else
          if not (isAncestor inputs.Repo fix inputs.Candidate) then
              yield sprintf "DCL-003 %s: fix commit %s is not the candidate or one of its ancestors" id fix

          match baseCommit with
          | None -> yield sprintf "DCL-010 %s: the base commit %s is not in this repository, so the fix cannot be shown to follow it" id (inputs.Base |> Option.defaultValue "(none)")
          | Some b when b = fix || not (isAncestor inputs.Repo b fix) ->
              yield sprintf "DCL-010 %s: fix commit %s does not descend from the base %s (it predates the rehearsal or is the base itself)" id fix b
          | Some _ -> ()

          if changedBy inputs.Repo fix |> List.forall (fun p -> p = inputs.ClosuresPath) then
              yield sprintf "DCL-011 %s: fix commit %s changes nothing but the closures record" id fix

      if tests.IsEmpty then
          yield sprintf "DCL-012 %s: the closure names no regression test" id

      for test in curatedTests inputs.Curation id do
          if not (List.contains test tests) then
              yield sprintf "DCL-004 %s: curated regression test %s is not part of the closure" id test

      for test in tests do
          if not (tree.Declared |> List.exists (namesTest test)) then
              match tree.Unparseable |> List.filter (fun (_, _, body) -> namesTest test body) with
              | [] -> yield sprintf "DCL-005 %s: regression test %s is not declared as a (not skipped) test under %s/ in the candidate" id test inputs.TestsPath
              | files ->
                  for f, problem, _ in files do
                      yield sprintf "DCL-005 %s: regression test %s is named only in %s, which is UNPARSEABLE (%s), so it is not shown to be declared and run" id test f problem

      match evidence with
      | "" -> yield sprintf "DCL-007 %s: no review evidence is recorded" id
      | reference ->
          match ReviewRecord.read inputs.Repo evidenceCommit [ inputs.ClosuresPath ] reference with
          | Error why -> yield sprintf "DCL-007 %s: review evidence %s" id why
          | Ok(path, review) ->
              if fixUsable && not (changedAfter inputs.Repo fix evidenceCommit path) then
                  yield sprintf "DCL-007 %s: review evidence %s was not added or changed by any commit after the fix %s" id reference fix

              let closes (entry: Json) =
                  strOpt "defectClass" entry = Some id && strOpt "fixSha" entry = Some fix && strOpt "verdict" entry = Some "CLOSED"

              if not (arr "defectClasses" review |> List.exists closes) then
                  yield sprintf "DCL-008 %s: review %s records no `defectClasses[]` entry closing it (defectClass %s, fixSha %s, verdict CLOSED)" id reference id fix

      match inputs.FreezeAt with
      | None -> ()
      | Some freeze ->
          match closedAt closure with
          | None -> yield sprintf "DCL-009 %s: no valid closedAt, so the closure cannot be shown to precede the freeze" id
          | Some at when at > freeze -> yield sprintf "DCL-009 %s: closed at %s, after the freeze at %s" id (at.ToString("o")) (freeze.ToString("o"))
          | Some at ->
              match (if fixUsable then committedAt inputs.Repo fix else None) with
              | Some fixedAt when at < fixedAt ->
                  yield sprintf "DCL-009 %s: closed at %s, before its fix commit was made (%s)" id (at.ToString("o")) (fixedAt.ToString("o"))
              | _ -> () ]

/// What this verification cannot establish, stated in every report.
let limits =
    [ "a fix commit is shown to descend from the base and to change something, not to fix the class: an unrelated commit can be named"
      "review evidence is checked for a review record's structure, not for who wrote it: without a signed review or reviewer attestation the implementing agent can write it, in the candidate or a records commit"
      "the base is the commit that first added kickoff/indy-init.kickoff.json in the candidate's first-parent history: history rewritten before the run moves it, unless the kickoff commit is pinned outside the candidate (an owner record or tag)"
      "tests are read lexically: whether a declared, unskipped test's body actually exercises the fix is not checked" ]

/// The `indy-init.defect-closure-verification/4` report.
let assess (given: Inputs) : Json =
    let full rev = resolveCommit given.Repo rev |> Option.defaultValue rev
    let candidate = full given.Candidate
    let baseText, kickoff, baseVerified, baseProblem = Kickoff.bind given.Repo candidate Kickoff.defaultPath given.Base candidate
    // Every commit as a full SHA from here on, so the report echoes full SHAs.
    let inputs = { given with Candidate = candidate; Records = given.Records |> Option.map full; Base = Some baseText }

    let recordsProblems =
        match inputs.Records with
        | Some records when not (Git.hasCommit inputs.Repo records && isAncestor inputs.Repo inputs.Candidate records) ->
            [ sprintf "DCL-006 records commit %s does not descend from the candidate %s" records inputs.Candidate ]
        | _ -> []

    let baseCommit = resolveCommit inputs.Repo baseText
    let tree = declaredTests inputs.Repo inputs.Candidate inputs.TestsPath

    let claimed =
        arr "defectClasses" inputs.Closures
        |> List.map (fun closure ->
            let id = strOpt "id" closure |> Option.defaultValue ""

            match strOpt "status" closure with
            | Some "PASS" ->
                let problems = recordsProblems @ problemsOf inputs baseCommit tree recordsProblems.IsEmpty closure
                id, (if problems.IsEmpty then "VERIFIED" else "REFUSED"), problems
            | Some status when knownStatuses.Contains status -> id, "OPEN", []
            | status ->
                let shown = status |> Option.map (sprintf "`%s`") |> Option.defaultValue "(none)"
                id, "REFUSED", [ sprintf "DCL-013 %s: status %s is not one of %s" id shown (String.Join(", ", knownStatuses)) ])

    let present = claimed |> List.map (fun (id, _, _) -> id) |> Set.ofList

    let missing =
        curatedClasses inputs.Curation
        |> List.filter (present.Contains >> not)
        |> List.map (fun id -> id, "MISSING", [ sprintf "DCL-014 %s: the curation names this class but the closures do not" id ])

    let classes = claimed @ missing
    let count state = classes |> List.filter (fun (_, s, _) -> s = state) |> List.length
    let refused = count "REFUSED" + count "MISSING"

    let incomplete =
        [ if inputs.Curation.IsNone then yield "no curation was supplied, so no class is known to be required"
          if inputs.FreezeAt.IsNone then yield "no freeze time was supplied, so no closure is shown to precede the freeze"
          if claimed.IsEmpty then yield "the closures record holds no defect class"
          for f, problem, _ in tree.Unparseable do
              yield sprintf "%s is UNPARSEABLE (%s): which tests it declares, and whether they run, is unknown" f problem ]

    let verdict =
        if refused > 0 || not baseVerified then "REFUSED"
        elif not incomplete.IsEmpty then "INCOMPLETE"
        else "VERIFIED"

    JObj
        [ "schema", JStr "indy-init.defect-closure-verification/4"
          "base", JStr(baseCommit |> Option.defaultValue baseText)
          "baseVerified", JBool baseVerified
          "kickoff", Kickoff.toJson Kickoff.defaultPath kickoff
          "baseProblem", jopt JStr baseProblem
          "candidate", JStr candidate
          "records", jopt JStr inputs.Records
          "closures", JObj [ "source", JStr inputs.ClosuresSource; "sha256", JStr inputs.ClosuresSha256 ]
          "curationSupplied", JBool inputs.Curation.IsSome
          "freezeAt", jopt (fun (at: DateTimeOffset) -> JStr(at.ToString("o"))) inputs.FreezeAt
          "verdict", JStr verdict
          "incomplete", jstrs incomplete
          "unparseable", JArr(tree.Unparseable |> List.map (fun (f, problem, _) -> JObj [ "path", JStr f; "problem", JStr problem ]))
          "limits", jstrs limits
          "summary",
          JObj
              [ "verified", jint (count "VERIFIED")
                "refused", jint (count "REFUSED")
                "missing", jint (count "MISSING")
                "open", jint (count "OPEN") ]
          "classes",
          JArr(
              classes
              |> List.map (fun (id, state, problems) -> JObj [ "id", JStr id; "state", JStr state; "problems", jstrs problems ])
          ) ]
