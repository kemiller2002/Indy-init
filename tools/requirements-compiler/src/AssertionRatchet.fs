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
///   helper in another file or a computed property is not seen.
///
/// The base is read from the repository, never asserted: the kickoff record (`--kickoff-path`, by default
/// `kickoff/indy-init.kickoff.json`) is located in the candidate's first-parent history, and the commit that first
/// added it is the base, unless the record as added there names an earlier commit (`base`, `baseCommit`,
/// `ratchetBase`, or a receipt's `initialRepository.head`) that is an ancestor of that commit. Later edits of the
/// record are not read. A `--base` that is not this commit is REFUSED (`baseVerified` false), as is a record the
/// candidate's history does not hold; with no kickoff path at all the verdict is at best INCOMPLETE. What remains
/// self-asserted is the path's role: a history whose first kickoff record was added late, or at another path, binds a
/// late base, which the record's path and adding commit in the report let a reviewer see.
///
/// Inherent limits (a lexical ratchet, not an interpreter): it cannot see that an unchanged assertion became dead
/// code (an early `return` before it, a braced `if (false) { ... }` around it, a changed helper argument or
/// fixture, a loop over an emptied collection), that a conditional skip's condition changed, or that what an
/// unchanged assertion inspects was weakened elsewhere. Those remain for the independent review.
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
      /// Review tag changes (`changes[]` with `obligation` and/or `test`, and `resolution`).
      TagChanges: Json option
      /// The verification plan, for PlanVerification's `rebound` resolutions (tests bound to the obligation).
      Plan: Json option
      /// The kickoff record's path in the repository: the base is derived from the commit that first added it in the
      /// candidate's first-parent history.
      KickoffPath: string option }

/// The kickoff record path the CLI reads when none is given.
let defaultKickoffPath = "kickoff/indy-init.kickoff.json"

/// The base commit a kickoff record names, and the field it is in: `base`, `baseCommit`, `ratchetBase`, or a
/// kickoff receipt's `initialRepository.head`.
let kickoffBase (kickoff: Json) : (string * string) option =
    [ "base"; "baseCommit"; "ratchetBase" ]
    |> List.tryPick (fun k -> strOpt k kickoff |> Option.map (fun v -> k, v))
    |> Option.orElse (field "initialRepository" kickoff |> Option.bind (strOpt "head") |> Option.map (fun v -> "initialRepository.head", v))

let private resolve (repo: string) (rev: string) =
    match Git.run repo [ "rev-parse"; "--verify"; "--quiet"; rev + "^{commit}" ] with
    | Ok bytes -> Some(Encoding.UTF8.GetString(bytes).Trim())
    | Error _ -> None

let private isAncestor (repo: string) (ancestor: string) (descendant: string) =
    Git.run repo [ "merge-base"; "--is-ancestor"; ancestor; descendant ] |> Result.isOk

/// The kickoff record as the candidate's history holds it: the commit that first added `path` in the candidate's
/// first-parent history, the record as added, and the base it binds (that commit, or an ancestor the record names).
/// Error when the history holds no such record, or the record names a base that is not an ancestor of it.
type Kickoff =
    { Path: string
      AddedAt: string
      Field: string option
      RecordedBase: string option
      BoundBase: string }

let kickoffIn (repo: string) (candidate: string) (path: string) : Result<Kickoff, string> =
    let added =
        match Git.run repo [ "log"; "--first-parent"; "--diff-filter=A"; "--format=%H"; candidate; "--"; path ] with
        | Ok bytes -> Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.tryLast
        | Error _ -> None

    match added with
    | None -> Error(sprintf "no commit in %s's first-parent history adds the kickoff record %s" candidate path)
    | Some addedAt ->
        let record =
            Git.showFile repo addedAt path |> Result.toOption |> Option.bind (fun bytes -> try Some(parseBytes bytes) with _ -> None)

        match record with
        | None -> Error(sprintf "the kickoff record %s as added at %s is not JSON" path addedAt)
        | Some record ->
            match kickoffBase record with
            | None -> Ok { Path = path; AddedAt = addedAt; Field = None; RecordedBase = None; BoundBase = addedAt }
            | Some(key, value) ->
                match resolve repo value with
                | Some named when isAncestor repo named addedAt ->
                    Ok { Path = path; AddedAt = addedAt; Field = Some key; RecordedBase = Some value; BoundBase = named }
                | _ -> Error(sprintf "the kickoff record %s as added at %s names %s `%s`, which is not an ancestor of that commit" path addedAt key value)

let private underRoots (roots: string list) (path: string) =
    roots |> List.exists (fun r -> path.StartsWith(r.TrimEnd('/') + "/", StringComparison.Ordinal))

let private tracked (roots: string list) (path: string) =
    underRoots roots path && (TestBlocks.languageOf path).IsSome

/// Each block of one file (tests, describes and the module pseudo-test): title -> each assertion statement and how
/// many times the block holds it. Counted, because a test may repeat an assertion and lose one copy (UI-034-O2 held
/// its selection check twice). A skipped block holds nothing.
let blocks (path: string) (text: string) : Map<string, Map<string, int>> =
    TestBlocks.parse path text
    |> Option.defaultValue []
    |> List.fold
        (fun (acc: Map<string, Map<string, int>>) block ->
            let held = acc.TryFind block.Title |> Option.defaultValue Map.empty
            let assertions = if block.Skipped then [] else TestBlocks.assertions path block

            let held' =
                assertions
                |> List.fold (fun (m: Map<string, int>) key -> m.Add(key, (m.TryFind key |> Option.defaultValue 0) + 1)) held

            acc.Add(block.Title, held'))
        Map.empty

let private textAt (repo: string) (commit: string) (path: string) =
    Git.showFile repo commit path |> Result.map Encoding.UTF8.GetString |> Result.toOption

let private filesAt (repo: string) (commit: string) (roots: string list) =
    match Git.listFiles repo commit with
    | Ok files -> files |> List.filter (tracked roots)
    | Error e -> failwithf "cannot list %s: %s" commit e

let private obligationOf (title: string) = Regex.Match(title, @"^[A-Z][A-Z0-9-]*-O\d+").Value

/// The resolved tag change that covers a test, if any. A change names the test (`test`: its title, or a full
/// `... > title`) or, without `test`, its obligation (the `-O<n>` prefix of the title). It must satisfy exactly
/// PlanVerification's rule (tagChangeAdmissible): the change names its review, and its resolution is `rebound` to
/// tests the plan binds to the obligation or `not-rebound` with a reason, each naming the review. A free-text
/// resolution (`"ok"`) resolves nothing.
let private exemption (tagChanges: Json option) (plan: Json option) (title: string) : Json option =
    let entries = tagChanges |> Option.map (arr "changes") |> Option.defaultValue []
    let bound = plan |> Option.map PlanVerification.boundByObligation |> Option.defaultValue Map.empty
    let obligation = obligationOf title

    let covers (c: Json) =
        match strOpt "test" c |> Option.map (fun t -> t.Trim()) with
        | Some test when test <> "" -> test = title || test.EndsWith(" > " + title, StringComparison.Ordinal)
        | _ -> obligation <> "" && strOpt "obligation" c = Some obligation

    entries |> List.tryFind (fun c -> covers c && PlanVerification.tagChangeAdmissible bound entries c)

/// The `indy-init.assertion-ratchet/3` report.
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

    let testsOf commit (paths: string list) =
        paths
        |> List.collect (fun path ->
            textAt repo commit path
            |> Option.map (blocks path >> Map.toList >> List.map (fun (title, held) -> (path, title), held))
            |> Option.defaultValue [])

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

    // Every assertion any version held, with the last commit that held it:
    // the base's tests, then each changed file of each later commit.
    let seen =
        history
        |> List.fold (fun acc commit -> testsOf commit (changedIn commit) |> remember commit acc) (testsOf baseCommit (filesAt repo baseCommit roots) |> remember baseCommit Map.empty)

    let final = testsOf candidate (filesAt repo candidate roots) |> Map.ofList

    let removed =
        seen
        |> Map.toList
        |> List.filter (fun ((path, title, line), (most, _)) ->
            let now = final.TryFind(path, title) |> Option.bind (Map.tryFind line) |> Option.defaultValue 0
            now < most)
        |> List.map (fun ((path, title, line), (_, lastSeen)) ->
            let covered = exemption inputs.TagChanges inputs.Plan title

            JObj
                [ "path", JStr path
                  "test", JStr title
                  "obligation", JStr(obligationOf title)
                  "assertion", JStr line
                  "lastSeenAt", JStr lastSeen
                  "state", JStr(if covered.IsSome then "RESOLVED_TAG_CHANGE" else "UNRECORDED")
                  "tagChange", jopt id covered ])

    let unrecorded = removed |> List.filter (fun r -> str "state" r = "UNRECORDED")

    let verdict =
        if kickoff.IsSome && not baseVerified then "REFUSED"
        elif not unrecorded.IsEmpty then "FAIL"
        elif kickoff.IsNone then "INCOMPLETE"
        else "PASS"

    JObj
        [ "schema", JStr "indy-init.assertion-ratchet/3"
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
          "verdict", JStr verdict
          "summary", JObj [ "removed", jint removed.Length; "unrecorded", jint unrecorded.Length ]
          "removed", JArr removed ]
