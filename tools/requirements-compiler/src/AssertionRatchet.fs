/// The assertion ratchet (Rehearsal 7 remediation R7-REM-011, Rehearsal 6 review finding RF-F-8).
///
/// In Rehearsal 6 an agent deleted UI-034-O2's selection assertion and loosened UI-034-O1's `toHaveCount(1)` to
/// `count() >= 1` in one "acceptance test fixes" commit (e79f0a4). Only the final independent review noticed the
/// first, and nothing noticed the second: no tool compared a bound test with its earlier versions. Here every
/// assertion any version of a test held between the base (the kickoff) and the candidate must still be in the
/// candidate, unless a tag change with an approved resolution covers that test. A rewritten assertion counts as
/// removed: the check is on text, so a loosening is never mistaken for an equivalent.
module AssertionRatchet

open System
open System.Text
open System.Text.RegularExpressions
open Json

/// Test blocks of one file: title -> each normalized assertion line and how many times the test holds it. Counted,
/// because a test may repeat an assertion and lose one copy (UI-034-O2 held its selection check twice).
let private blocks (path: string) (text: string) : Map<string, Map<string, int>> =
    let isJs = Regex.IsMatch(path, @"\.(spec|test)\.(m?js|ts)$")
    let start = if isJs then Regex(@"^\s*test\(\s*([""'`])(.+?)\1") else Regex(@"^\s*let\s+``(.+?)``")
    let assertion = if isJs then Regex(@"\bexpect\(|\bassert[.(]") else Regex(@"\bAssert\.|\bExpect\.|test\s*<@|\|>\s*should\b")
    let collapse (line: string) = Regex.Replace(line.Trim(), @"\s+", " ")

    text.Replace("\r\n", "\n").Split('\n')
    |> Array.fold
        (fun (current: string option, acc: Map<string, Map<string, int>>) line ->
            let m = start.Match line

            if m.Success then
                let title = if isJs then m.Groups.[2].Value else m.Groups.[1].Value
                Some title, (if acc.ContainsKey title then acc else acc.Add(title, Map.empty))
            else
                match current with
                | Some title when assertion.IsMatch line && not (line.TrimStart().StartsWith "//") ->
                    let held = acc.[title]
                    let key = collapse line
                    current, acc.Add(title, held.Add(key, (held.TryFind key |> Option.defaultValue 0) + 1))
                | _ -> current, acc)
        (None, Map.empty)
    |> snd

let private textAt (repo: string) (commit: string) (path: string) =
    Git.showFile repo commit path |> Result.map Encoding.UTF8.GetString |> Result.toOption

let private filesAt (repo: string) (commit: string) (roots: string list) =
    match Git.listFiles repo commit with
    | Ok files ->
        files
        |> List.filter (fun f ->
            roots |> List.exists (fun r -> f.StartsWith(r.TrimEnd('/') + "/", StringComparison.Ordinal))
            && Regex.IsMatch(f, @"(\.(spec|test)\.(m?js|ts)|Tests?\.fs)$"))
    | Error e -> failwithf "cannot list %s: %s" commit e

/// Obligations whose tag change carries an approved resolution (`resolution` set).
let private resolved (tagChanges: Json option) =
    tagChanges
    |> Option.map (arr "changes" >> List.filter (fun c -> (field "resolution" c |> Option.defaultValue JNull) <> JNull) >> List.map (str "obligation") >> Set.ofList)
    |> Option.defaultValue Set.empty

/// The `indy-init.assertion-ratchet/1` report.
let assess (repo: string) (baseCommit: string) (candidate: string) (roots: string list) (tagChanges: Json option) : Json =
    let lines (bytes: byte[]) =
        Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

    let isTest (path: string) =
        roots |> List.exists (fun r -> path.StartsWith(r.TrimEnd('/') + "/", StringComparison.Ordinal))
        && Regex.IsMatch(path, @"(\.(spec|test)\.(m?js|ts)|Tests?\.fs)$")

    // Only commits that touched the test roots, oldest first.
    let history =
        match Git.run repo ([ "rev-list"; "--reverse"; sprintf "%s..%s" baseCommit candidate; "--" ] @ roots) with
        | Ok bytes -> lines bytes
        | Error e -> failwithf "cannot walk %s..%s: %s" baseCommit candidate e

    let changedIn commit =
        match Git.run repo [ "diff-tree"; "--no-commit-id"; "--name-only"; "-r"; commit ] with
        | Ok bytes -> lines bytes |> List.filter isTest
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

    let testsAt commit = testsOf commit (filesAt repo commit roots)

    let final = testsAt candidate |> Map.ofList
    let accepted = resolved tagChanges
    let obligationOf (title: string) = Regex.Match(title, @"^[A-Z][A-Z0-9-]*-O\d+").Value

    let removed =
        seen
        |> Map.toList
        |> List.filter (fun ((path, title, line), (most, _)) ->
            let now =
                final.TryFind(path, title) |> Option.bind (Map.tryFind line) |> Option.defaultValue 0

            now < most)
        |> List.map (fun ((path, title, line), (_, lastSeen)) ->
            let obligation = obligationOf title
            let state = if obligation <> "" && accepted.Contains obligation then "RESOLVED_TAG_CHANGE" else "UNRECORDED"

            JObj
                [ "path", JStr path
                  "test", JStr title
                  "obligation", JStr obligation
                  "assertion", JStr line
                  "lastSeenAt", JStr lastSeen
                  "state", JStr state ])

    let unrecorded = removed |> List.filter (fun r -> str "state" r = "UNRECORDED")

    JObj
        [ "schema", JStr "indy-init.assertion-ratchet/1"
          "base", JStr baseCommit
          "candidate", JStr candidate
          "roots", jstrs roots
          "commitsWalked", jint (history.Length + 1)
          "verdict", JStr(if unrecorded.IsEmpty then "PASS" else "FAIL")
          "summary", JObj [ "removed", jint removed.Length; "unrecorded", jint unrecorded.Length ]
          "removed", JArr removed ]
