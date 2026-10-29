/// The assertion ratchet (Rehearsal 7 remediation R7-REM-011, Rehearsal 6 review finding RF-F-8).
///
/// In Rehearsal 6 an agent deleted UI-034-O2's selection assertion and loosened UI-034-O1's `toHaveCount(1)` to
/// `count() >= 1` in one "acceptance test fixes" commit (e79f0a4). Only the final independent review noticed the
/// first, and nothing noticed the second: no tool compared a bound test with its earlier versions. Here every
/// assertion any version of a test held between the base (the kickoff) and the candidate must still be in the
/// candidate, unless a review tag change that PlanVerification would accept covers that test. A rewritten assertion
/// counts as removed: the check is on text, so a loosening is never mistaken for an equivalent.
///
/// What counts as held (see TestBlocks for the reading):
/// - every code file under the roots is read, specs and helpers alike; a helper file (`_lib.js`, `*.mjs`, an F#
///   helper module), and the lines of a spec outside any test, are tracked as the pseudo-test `<file>::<module>`;
/// - comments, including multi-line `/* ... */` and `(* ... *)` blocks, are removed before reading;
/// - an assertion is its whole statement, continuation lines joined, so `.not` on a continuation line or an
///   `if (false)` put in front of an unbraced assertion changes it;
/// - an assertion is recognised in the statement with string, template and regex literal contents masked, so one
///   moved into a template literal (or any string) is no longer held;
/// - a test declared with, or containing, `test.skip`/`test.fixme`/`test.fail`/`test.only` (or `it.skip`, `xit`,
///   `describe.skip`, `test.describe.skip`, F# `Skip =`), any `.skip(`/`.fixme(`/`.fail(` call in its body or on its
///   declaration line (`testInfo.skip()`, `test.info().skip()`), a call of a name the file binds to a marker
///   (`const { skip } = test; skip();`), or inside a skipped describe, holds nothing: adding such a marker removes
///   every assertion it held, and moving an assertion into a skipped test removes it. A marker reached through a
///   helper in another file or a computed property is not seen. This holds for the candidate (fail closed). In the
///   base and later versions a block skipped only by a conditional call (`test.skip(isMobile, "...")`) may have run,
///   so its assertions count as held; unconditionally skipped blocks hold nothing there either, and the report counts
///   the tests and assertions so excluded (`skippedBeforeCandidate`);
/// - a file the lexer cannot read consistently (UNPARSEABLE: it ends inside a literal or comment, a string or regular
///   expression runs past its line, parentheses do not balance) holds nothing, and the verdict is at best INCOMPLETE:
///   a desynchronised reading (a `/` misread as a regular expression or division, an unbalanced `${...}`) can hide a
///   skip or show a dead assertion as live, so it is never trusted. JSX/TSX files are not read at all (`unsupported`),
///   with the same effect.
///
/// The base is read from the repository, never asserted: the kickoff record, only ever `kickoff/indy-init.kickoff.json`
/// (any other path is REFUSED: a caller-chosen path, such as an ROS execution envelope, would bind whatever commit
/// added it), is located in the candidate's first-parent history, and the commit that first added it is the base,
/// unless the record as added there names an earlier commit (`base`, `baseCommit`, `ratchetBase`, or a receipt's
/// `initialRepository.head`) that is an ancestor of that commit. Later edits of the record are not read. A `--base`
/// that is not this commit is REFUSED (`baseVerified` false), as is a history without the record.
///
/// Review tag changes are read from the repository too: `acceptance/reviews/final/review-tag-changes.json` at the
/// records commit (a descendant of the candidate) or the candidate. A change is honoured only when PlanVerification
/// would accept it and its `review` is a reference to a review record (ReviewRecord) in that commit that names the
/// obligation and is not the tag-change record itself; a free-text review ("agent-self-review") is not one. Tag
/// changes from a file on disk are honoured but the verdict is at best INCOMPLETE.
///
/// Inherent limits (a lexical ratchet, not an interpreter; also in every report's `limits`): it cannot see that an
/// unchanged assertion became dead code (an early `return` before it, a braced `if (false) { ... }` around it, a
/// changed helper argument or fixture in another file, a loop over an emptied collection), that a conditional skip's
/// condition changed, or that what an unchanged assertion inspects was weakened elsewhere; history rewritten before
/// the run moves the kickoff commit unless it is pinned outside the candidate (an owner record or tag); and a review
/// record's structure does not show who wrote it. Those remain for the independent review.
module AssertionRatchet

open System
open System.Text
open System.Text.RegularExpressions
open Json

type Inputs =
    { Repo: string
      /// The base the caller expects; None takes the base the kickoff record binds.
      Base: string option
      Candidate: string
      Roots: string list
      /// Review tag changes supplied from a file on disk instead of the repository: honoured, but the verdict is at
      /// best INCOMPLETE, since nothing binds the file to the candidate.
      TagChangesFile: Json option
      /// A records-only descendant of the candidate that holds the review tag changes and their reviews, if any.
      Records: string option
      /// The verification plan, for PlanVerification's `rebound` resolutions (tests bound to the obligation).
      Plan: Json option
      /// The kickoff record's path in the repository: the base is derived from the commit that first added it in the
      /// candidate's first-parent history.
      KickoffPath: string option }

/// The only kickoff record that binds the base (Kickoff.defaultPath).
let defaultKickoffPath = Kickoff.defaultPath

/// Where the review tag changes are read from, in the records commit when given, else the candidate (Rehearsal 6's
/// final review wrote them there).
let tagChangesPath = "acceptance/reviews/final/review-tag-changes.json"

let private resolve = Kickoff.resolve
let private isAncestor = Kickoff.isAncestor
let kickoffIn = Kickoff.find

let private underRoots (roots: string list) (path: string) =
    roots |> List.exists (fun r -> path.StartsWith(r.TrimEnd('/') + "/", StringComparison.Ordinal))

let private tracked (roots: string list) (path: string) =
    underRoots roots path && ((TestBlocks.languageOf path).IsSome || TestBlocks.isUnsupported path)

/// Which side of the ratchet a version is read for. The candidate is read fail-closed: any skipped block holds
/// nothing. An earlier version (the base or a later commit before the candidate) is read for what it held: a block
/// skipped only conditionally (`test.skip(isMobile, ...)`) may have run, so it counts as live; an unconditionally
/// skipped block holds nothing, and what it excluded is reported.
type Side =
    | Candidate
    | Earlier

/// One version of one file: title -> each assertion statement and how many times the block holds it (counted,
/// because a test may repeat an assertion and lose one copy, as UI-034-O2 held its selection check twice); the
/// blocks skipped unconditionally with the assertions they would hold; the conditionally skipped blocks counted as
/// live. Error (`UNPARSEABLE` or `unsupported`, and why) when the file is not read.
type Reading =
    { Held: Map<string, Map<string, int>>
      Excluded: (string * string list) list
      TreatedLive: string list }

let read (side: Side) (path: string) (text: string) : Result<Reading, string * string> =
    let count (held: Map<string, int>) (assertions: string list) =
        assertions |> List.fold (fun (m: Map<string, int>) key -> m.Add(key, (m.TryFind key |> Option.defaultValue 0) + 1)) held

    match TestBlocks.parse path text with
    | None -> Ok { Held = Map.empty; Excluded = []; TreatedLive = [] }
    | Some(TestBlocks.Unparseable problem) -> Error("UNPARSEABLE", problem)
    | Some(TestBlocks.Unsupported reason) -> Error("unsupported", reason)
    | Some(TestBlocks.Blocks parsed) ->
        parsed
        |> List.fold
            (fun (r: Reading) block ->
                let held = r.Held.TryFind block.Title |> Option.defaultValue Map.empty
                let assertions = TestBlocks.assertions path block
                let live = not block.Skipped || (side = Earlier && block.SkipConditional)

                { Held = r.Held.Add(block.Title, (if live then count held assertions else held))
                  Excluded = (if live || assertions.IsEmpty then r.Excluded else (block.Title, assertions) :: r.Excluded)
                  TreatedLive = (if block.Skipped && live then block.Title :: r.TreatedLive else r.TreatedLive) })
            { Held = Map.empty; Excluded = []; TreatedLive = [] }
        |> Ok

/// What each block of one file holds, read as the candidate is (see `read`); an UNPARSEABLE file holds nothing.
let blocks (path: string) (text: string) : Map<string, Map<string, int>> =
    match read Candidate path text with
    | Ok r -> r.Held
    | Error _ -> Map.empty

let private textAt (repo: string) (commit: string) (path: string) =
    Git.showFile repo commit path |> Result.map Encoding.UTF8.GetString |> Result.toOption

let private filesAt (repo: string) (commit: string) (roots: string list) =
    match Git.listFiles repo commit with
    | Ok files -> files |> List.filter (tracked roots)
    | Error e -> failwithf "cannot list %s: %s" commit e

let private obligationOf (title: string) = Regex.Match(title, @"^[A-Z][A-Z0-9-]*-O\d+").Value

/// Why a tag change's review does not stand, if it does not: its `review` (and its resolution's `review`, when it
/// names one) must be a reference (`path#anchor`) to a review record (ReviewRecord) at `commit`, not the tag-change
/// record itself, that names the change's obligation. A free-text `review` ("agent-self-review") names no record.
let reviewProblem (repo: string) (commit: string) (source: string option) (c: Json) : string option =
    let obligation = strOpt "obligation" c |> Option.defaultValue ""

    let check (reference: string) =
        match ReviewRecord.read repo commit (Option.toList source) reference with
        | Error why -> Some(sprintf "review %s" why)
        | Ok(_, doc) when obligation <> "" && not ((serialize doc).Contains(obligation, StringComparison.Ordinal)) ->
            Some(sprintf "review %s does not name %s" reference obligation)
        | Ok _ -> None

    [ strOpt "review" c; field "resolution" c |> Option.bind (strOpt "review") ]
    |> List.choose id
    |> function
        | [] -> Some "the change names no review"
        | references -> references |> List.tryPick check

/// The resolved tag change that covers a test, if any. A change names the test (`test`: its title, or a full
/// `... > title`) or, without `test`, its obligation (the `-O<n>` prefix of the title). It must satisfy exactly
/// PlanVerification's rule (tagChangeAdmissible): the change names its review, and its resolution is `rebound` to
/// tests the plan binds to the obligation or `not-rebound` with a reason, each naming the review; and that review
/// must stand (reviewProblem). A free-text resolution (`"ok"`) resolves nothing.
let private exemption (entries: Json list) (standing: Json -> bool) (plan: Json option) (title: string) : Json option =
    let bound = plan |> Option.map PlanVerification.boundByObligation |> Option.defaultValue Map.empty
    let obligation = obligationOf title

    let covers (c: Json) =
        match strOpt "test" c |> Option.map (fun t -> t.Trim()) with
        | Some test when test <> "" -> test = title || test.EndsWith(" > " + title, StringComparison.Ordinal)
        | _ -> obligation <> "" && strOpt "obligation" c = Some obligation

    entries |> List.tryFind (fun c -> covers c && PlanVerification.tagChangeAdmissible bound entries c && standing c)

/// What this ratchet cannot establish, stated in every report.
let limits =
    [ "dead code is not seen: an unchanged assertion after an early return, inside a braced if (false) { ... }, or reached through a helper in another file whose argument or fixture changed still counts as held"
      "skip markers reached through a helper in another file, an argument or a computed property are not seen; a conditional skip's condition is not evaluated"
      "the base is the commit that first added kickoff/indy-init.kickoff.json in the candidate's first-parent history: history rewritten before the run moves it, unless the kickoff commit is pinned outside the candidate (an owner record or tag)"
      "a tag change's review is checked for a review record's structure, not for who wrote it: without a signed review or reviewer attestation the implementing agent can write it"
      "the JavaScript/TypeScript scanner is a heuristic lexer that fails closed where it detects ambiguity; an adversarially written file may still desynchronise it undetected, so the independent review must inspect the test files it relies on"
      "JSX/TSX files are not read (unsupported): a tracked path holding one leaves the verdict at best INCOMPLETE" ]

/// The `indy-init.assertion-ratchet/5` report.
let assess (inputs: Inputs) : Json =
    let repo = inputs.Repo
    let roots = inputs.Roots
    let full rev = resolve repo rev |> Option.defaultWith (fun () -> failwithf "%s is not a commit of %s" rev repo)
    let candidate = full inputs.Candidate
    let kickoff = inputs.KickoffPath |> Option.map (kickoffIn repo candidate)
    let derived = kickoff |> Option.bind Result.toOption |> Option.map _.BoundBase

    let baseCommit =
        match inputs.Base, derived with
        | Some rev, _ -> full rev
        | None, Some b -> b
        // An unusable kickoff record refuses the run; the report still says why (nothing is walked).
        | None, None when kickoff.IsSome -> candidate
        | None, None -> failwith "no --base, and no kickoff record in the candidate's history to derive it from"

    let baseVerified = derived = Some baseCommit

    let lines (bytes: byte[]) =
        Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

    // Only commits that touched the test roots, oldest first.
    let history =
        match Git.run repo ([ "rev-list"; "--reverse"; sprintf "%s..%s" baseCommit candidate; "--" ] @ roots) with
        | Ok bytes -> lines bytes
        | Error e -> failwithf "cannot walk %s..%s: %s" baseCommit candidate e

    let changedIn commit =
        match Git.run repo [ "diff-tree"; "--no-commit-id"; "--name-only"; "-r"; "-m"; commit ] with
        | Ok bytes -> lines bytes |> List.filter (tracked roots) |> List.distinct
        | Error e -> failwithf "cannot read %s: %s" commit e

    // Each file's reading at a commit: (commit, path, reading or the UNPARSEABLE problem).
    let readingsOf side commit (paths: string list) =
        paths |> List.choose (fun path -> textAt repo commit path |> Option.map (fun text -> commit, path, read side path text))

    let testsOf readings =
        readings
        |> List.collect (function
            | _, path, Ok(r: Reading) -> r.Held |> Map.toList |> List.map (fun (title, held) -> (path, title), held)
            | _ -> [])

    // The most copies of each assertion any version held, and the last commit that held that many.
    let remember commit (acc: Map<string * string * string, int * string>) (tests: ((string * string) * Map<string, int>) list) =
        tests
        |> List.fold
            (fun acc ((path, title), held) ->
                held
                |> Map.fold
                    (fun (acc: Map<string * string * string, int * string>) line count ->
                        match acc.TryFind(path, title, line) with
                        | Some(most, _) when most > count -> acc
                        | _ -> acc.Add((path, title, line), (count, commit)))
                    acc)
            acc

    // The base's files, then each changed file of each later commit, read for what they held.
    let earlier =
        (baseCommit, filesAt repo baseCommit roots) :: (history |> List.map (fun commit -> commit, changedIn commit))
        |> List.map (fun (commit, paths) -> commit, readingsOf Earlier commit paths)

    let seen = earlier |> List.fold (fun acc (commit, readings) -> testsOf readings |> remember commit acc) Map.empty
    let candidateReadings = readingsOf Candidate candidate (filesAt repo candidate roots)
    let final = testsOf candidateReadings |> Map.ofList
    let allReadings = (earlier |> List.collect snd) @ candidateReadings

    // Each file not read, once per version (the candidate is read both as the last commit walked and as the candidate).
    let notRead kind =
        allReadings
        |> List.choose (function commit, path, Error(k, why) when k = kind -> Some(commit, path, why) | _ -> None)
        |> List.distinct

    let unparseable = notRead "UNPARSEABLE"
    let unsupported = notRead "unsupported"

    let earlierReadings = earlier |> List.collect snd |> List.choose (function _, path, Ok r -> Some(path, r) | _ -> None)
    let excluded = earlierReadings |> List.collect (fun (path, r) -> r.Excluded |> List.map (fun (title, assertions) -> path, title, assertions))
    let excludedTests = excluded |> List.map (fun (path, title, _) -> path, title) |> List.distinct
    let excludedAssertions = excluded |> List.collect (fun (path, title, assertions) -> assertions |> List.map (fun a -> path, title, a)) |> List.distinct
    let treatedLive = earlierReadings |> List.collect (fun (path, r) -> r.TreatedLive |> List.map (fun title -> path, title)) |> List.distinct

    // The review tag changes: from the records commit (when it descends from the candidate) or the candidate, at
    // tagChangesPath; a file from disk is honoured but leaves the verdict at best INCOMPLETE.
    let recordsProblem =
        match inputs.Records with
        | Some records when not ((resolve repo records |> Option.exists (fun r -> isAncestor repo candidate r))) ->
            Some(sprintf "records commit %s does not descend from the candidate %s" records candidate)
        | _ -> None

    let reviewCommit =
        match inputs.Records, recordsProblem with
        | Some records, None -> full records
        | _ -> candidate

    let tagChanges, tagSource =
        match inputs.TagChangesFile with
        | Some file -> file, "file"
        | None ->
            match Git.showFile repo reviewCommit tagChangesPath with
            | Ok bytes -> parseBytes bytes, sprintf "%s:%s" reviewCommit tagChangesPath
            | Error _ -> JNull, "none"

    let entries = arr "changes" tagChanges
    let reviewProblems = entries |> List.map (fun c -> c, reviewProblem repo reviewCommit (Some tagChangesPath) c)
    let standing (c: Json) = (reviewProblem repo reviewCommit (Some tagChangesPath) c).IsNone

    let removed =
        seen
        |> Map.toList
        |> List.filter (fun ((path, title, line), (most, _)) ->
            let now = final.TryFind(path, title) |> Option.bind (Map.tryFind line) |> Option.defaultValue 0
            now < most)
        |> List.map (fun ((path, title, line), (_, lastSeen)) ->
            let covered = exemption entries standing inputs.Plan title

            JObj
                [ "path", JStr path
                  "test", JStr title
                  "obligation", JStr(obligationOf title)
                  "assertion", JStr line
                  "lastSeenAt", JStr lastSeen
                  "state", JStr(if covered.IsSome then "RESOLVED_TAG_CHANGE" else "UNRECORDED")
                  "tagChange", jopt id covered ])

    let unrecorded = removed |> List.filter (fun r -> str "state" r = "UNRECORDED")

    let incomplete =
        [ if kickoff.IsNone then yield "no kickoff record binds the base"
          for commit, path, reason in unsupported do
              yield sprintf "%s at %s is unsupported (%s): what it holds is unknown" path commit reason
          for commit, path, problem in unparseable do
              yield sprintf "%s at %s is UNPARSEABLE (%s): what it holds is unknown" path commit problem
          if tagSource = "file" then yield "the review tag changes came from a file on disk, not from the candidate or records commit"
          match recordsProblem with
          | Some p -> yield p
          | None -> () ]

    let verdict =
        if kickoff.IsSome && not baseVerified then "REFUSED"
        elif not unrecorded.IsEmpty then "FAIL"
        elif not incomplete.IsEmpty then "INCOMPLETE"
        else "PASS"

    JObj
        [ "schema", JStr "indy-init.assertion-ratchet/5"
          "base", JStr baseCommit
          "candidate", JStr candidate
          "baseVerified", JBool baseVerified
          "kickoff",
          (match inputs.KickoffPath, kickoff with
           | Some path, Some(Ok k) ->
               JObj
                   [ "path", JStr path
                     "addedAt", JStr k.AddedAt
                     "field", jopt JStr k.Field
                     "recordedBase", jopt JStr k.RecordedBase
                     "base", JStr k.BoundBase ]
           | Some path, Some(Error e) -> JObj [ "path", JStr path; "problem", JStr e ]
           | _ -> JNull)
          "baseProblem",
          (match kickoff with
           | Some(Error e) -> JStr e
           | Some(Ok k) when k.BoundBase <> baseCommit -> JStr(sprintf "--base %s is not the base %s the kickoff record binds" baseCommit k.BoundBase)
           | _ -> JNull)
          "roots", jstrs roots
          "commitsWalked", jint (history.Length + 1)
          "tagChanges",
          JObj
              [ "source", JStr tagSource
                "changes", jint entries.Length
                "reviewProblems",
                JArr(
                    reviewProblems
                    |> List.choose (fun (c, p) ->
                        p |> Option.map (fun why -> JObj [ "obligation", jopt JStr (strOpt "obligation" c); "test", jopt JStr (strOpt "test" c); "problem", JStr why ]))
                ) ]
          "verdict", JStr verdict
          "incomplete", jstrs incomplete
          "unparseable", JArr(unparseable |> List.map (fun (commit, path, problem) -> JObj [ "commit", JStr commit; "path", JStr path; "problem", JStr problem ]))
          "unsupported", JArr(unsupported |> List.map (fun (commit, path, reason) -> JObj [ "commit", JStr commit; "path", JStr path; "reason", JStr reason ]))
          "skippedBeforeCandidate",
          JObj
              [ "testsExcluded", jint excludedTests.Length
                "assertionsExcluded", jint excludedAssertions.Length
                "conditionalSkipsTreatedAsLive", jint treatedLive.Length
                "tests", JArr(excludedTests |> List.map (fun (path, title) -> JObj [ "path", JStr path; "test", JStr title ]))
                "treatedAsLive", JArr(treatedLive |> List.map (fun (path, title) -> JObj [ "path", JStr path; "test", JStr title ])) ]
          "summary", JObj [ "removed", jint removed.Length; "unrecorded", jint unrecorded.Length ]
          "limits", jstrs limits
          "removed", JArr removed ]
