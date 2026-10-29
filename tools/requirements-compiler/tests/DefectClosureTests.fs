/// `verify-defect-closures` (R7-REM-008) against a throwaway git history: a closure verifies only with a fix commit
/// made after the base in the candidate's own ancestry, its regression tests declared as running tests (not comments,
/// skipped forms or longer ids), and review evidence made after the fix that names the class and the fix. Rehearsal
/// 6's own closures are the R5-SHA case (DCL-002); the independent review's bypasses of the first version (Rehearsal
/// 7) are the DCL-005, DCL-007, DCL-009..014 and INCOMPLETE cases.
module DefectClosureTests

open System
open System.IO
open Json

let private git (repo: string) (args: string list) =
    match Git.run repo ([ "-c"; "user.name=defect-closure-tests"; "-c"; "user.email=tests@example.invalid"; "-c"; "commit.gpgsign=false" ] @ args) with
    | Ok bytes -> Text.Encoding.UTF8.GetString(bytes).Trim()
    | Error e -> failwithf "git %A: %s" args e

let private write (repo: string) (path: string) (text: string) =
    let full = Path.Combine(repo, path)
    Directory.CreateDirectory(Path.GetDirectoryName full) |> ignore
    File.WriteAllText(full, text)

let private commit (repo: string) (message: string) =
    git repo [ "add"; "-A" ] |> ignore
    git repo [ "commit"; "-q"; "-m"; message ] |> ignore
    git repo [ "rev-parse"; "HEAD" ]

/// main: root, base, fix, closures-only, candidate, records; a side branch holds a commit that is not the
/// candidate's ancestor.
type private History =
    { Repo: string
      Root: string
      Base: string
      Fix: string
      ClosuresOnly: string
      Candidate: string
      Records: string
      Side: string
      FixedAt: DateTimeOffset }

let private specs =
    String.concat
        "\n"
        [ "import { test, expect } from \"@playwright/test\";"
          "test.skip(\"DCT-T-X never runs\", async () => { expect(1).toBe(1); });"
          "test(\"DCT-T-QX is a longer id\", async () => { expect(1).toBe(1); });"
          "test.fixme(\"DCT-T-F is a fixme\", async () => {});"
          "test(\"DCT-T-B skipped in its body\", async () => {"
          "  test.skip();"
          "  expect(1).toBe(1);"
          "});"
          "/* test(\"DCT-T-K in a block comment\", async () => {}); */"
          "test(\"DCT-T-I skipped through its fixture\", async ({ page }, info) => {"
          "  info.skip();"
          "  expect(1).toBe(1);"
          "});"
          "test(\"DCT-T-O skipped on its declaration line\", async () => { test.skip(); expect(1).toBe(1); });"
          "test(\"DCT-T-N skipped through test.info()\", async () => {"
          "  test.info().fixme();"
          "});"
          "const { skip: later } = test;"
          "test(\"DCT-T-L skipped through an alias\", async () => {"
          "  later();"
          "});"
          "test("
          "  \"DCT-T-M declared with its title on the next line\","
          "  async () => {"
          "    expect(1).toBe(1);"
          "  },"
          ");"
          "" ]

let private history () =
    let repo = Path.Combine(Path.GetTempPath(), $"defect-closure-{Guid.NewGuid():N}")
    Directory.CreateDirectory repo |> ignore
    git repo [ "init"; "-q"; "-b"; "main" ] |> ignore
    write repo "README.md" "initial\n"
    write repo "acceptance/reviews/old.md" "## DC-T\nreviewed long ago\n"
    let root = commit repo "root"

    write
        repo
        "tests/Regression.fs"
        "module Regression\n// DCT-T-B is described here only in a comment\nlet ``DCT-T-A a held conflict keeps the change`` () = ()\n[<Fact(Skip = \"later\")>]\nlet ``DCT-T-S a skipped fact`` () = ()\n"

    write repo "tests/ids.spec.js" specs
    let base' = commit repo "base (kickoff)"
    write repo "src/Store.fs" "let fixed = true\n"
    let fix = commit repo "fix the class"
    write repo "acceptance/defect-classes.json" "{}\n"
    let closuresOnly = commit repo "closures only"
    write repo "src/Other.fs" "let more = 1\n"
    let candidate = commit repo "candidate"
    write repo "acceptance/reviews/final.md" $"## DC-T\nClosed by {fix.Substring(0, 10)}; regression DCT-T-A.\nOld review updated after {fix.Substring(0, 10)}.\n"
    write repo "acceptance/reviews/old.md" $"## DC-T\nreviewed long ago; DC-T closed by {fix.Substring(0, 10)}\n"
    write repo "acceptance/copy.json" "{}\n"
    let records = commit repo "records only"
    git repo [ "checkout"; "-q"; "-b"; "side"; fix ] |> ignore
    write repo "src/Side.fs" "let side = 1\n"
    let side = commit repo "side"
    git repo [ "checkout"; "-q"; "main" ] |> ignore

    { Repo = repo
      Root = root
      Base = base'
      Fix = fix
      ClosuresOnly = closuresOnly
      Candidate = candidate
      Records = records
      Side = side
      FixedAt = DateTimeOffset.Parse(git repo [ "show"; "-s"; "--format=%cI"; fix ], Globalization.CultureInfo.InvariantCulture) }

let private closureOf (fields: (string * Json) list) =
    let defaults =
        [ "id", JStr "DC-T"
          "status", JStr "PASS"
          "fixSha", JNull
          "tests", jstrs [ "DCT-T-A" ]
          "reviewerEvidence", JStr "acceptance/reviews/final.md#dc-t" ]

    let overridden = defaults |> List.map (fun (k, v) -> k, (fields |> List.tryFind (fst >> (=) k) |> Option.map snd |> Option.defaultValue v))
    let extra = fields |> List.filter (fun (k, _) -> not (List.exists (fst >> (=) k) defaults)) |> List.distinctBy fst
    JObj(overridden @ extra)

let private closures (classes: Json list) =
    JObj [ "schema", JStr "causa.acceptance-defect-classes/1"; "defectClasses", JArr classes ]

let private closure fields = closures [ closureOf fields ]

let private curationOf (pairs: (string * string) list) =
    JObj [ "tests", JArr(pairs |> List.map (fun (test, cls) -> JObj [ "id", JStr test; "defectClass", JStr cls ])) ]

let private curation = curationOf [ "DCT-T-A", "DC-T" ]

type private Run =
    { Records: string option
      Freeze: DateTimeOffset option
      Curation: Json option
      Base: string option }

let private assessWith (h: History) (run: Run) (doc: Json) =
    DefectClosure.assess
        { Repo = h.Repo
          Base = run.Base |> Option.defaultValue h.Base
          Candidate = h.Candidate
          Records = run.Records
          Closures = doc
          ClosuresSource = "test"
          ClosuresSha256 = "00"
          ClosuresPath = "acceptance/defect-classes.json"
          Curation = run.Curation
          FreezeAt = run.Freeze
          TestsPath = "tests" }

let private problems (report: Json) =
    arr "classes" report |> List.collect (strs "problems")

let private has (rule: string) (report: Json) =
    str "verdict" report = "REFUSED" && (problems report |> List.exists (fun p -> p.StartsWith(rule, StringComparison.Ordinal)))

let all () : string list =
    let h = history ()

    try
        let fix = JStr h.Fix
        let freeze = h.FixedAt.AddHours 2.0
        let closedAt = JStr(h.FixedAt.AddHours(1.0).ToString("o"))
        let full = { Records = Some h.Records; Freeze = Some freeze; Curation = Some curation; Base = None }
        let assess = assessWith h full
        let ok = assess (closure [ "fixSha", fix; "closedAt", closedAt ])
        let good extra = closure (extra @ [ "fixSha", fix; "closedAt", closedAt ])

        [ if str "verdict" ok <> "VERIFIED" then
              yield sprintf "a closure with its own fix, test and review is not VERIFIED: %A" (problems ok)
          if str "verdict" (assess (good [ "tests", jstrs [ "DCT-T-A"; "DCT-T-M" ] ])) <> "VERIFIED" then
              yield "DCL-005: a test declared with its title on the line after test( is not found"
          if not (has "DCL-001" (assess (good [ "fixSha", JStr(h.Fix.Substring(0, 7)) ]))) then
              yield "DCL-001: a short fix SHA is accepted"
          if not (has "DCL-002" (assess (good [ "fixSha", JStr(String('5', 40)) ]))) then
              yield "DCL-002: a fix commit from another repository's history (Rehearsal 6's R5 SHAs) is accepted"
          if not (has "DCL-003" (assess (good [ "fixSha", JStr h.Side ]))) then
              yield "DCL-003: a fix commit that is not the candidate's ancestor is accepted"
          if not (has "DCL-004" (assess (good [ "tests", jstrs [ "DCT-T-A2" ] ]))) then
              yield "DCL-004: a closure that drops its curated regression test is accepted"
          if not (has "DCL-005" (assess (good [ "tests", jstrs [ "DCT-T-A"; "DCT-T-B" ] ]))) then
              yield "DCL-005: a regression test named only in a comment, or skipped in its body, is accepted"
          if not (has "DCL-005" (assess (good [ "tests", jstrs [ "DCT-T-A"; "DCT-T-Z" ] ]))) then
              yield "DCL-005: an absent regression test is accepted"

          for skipped in [ "DCT-T-X"; "DCT-T-F"; "DCT-T-S"; "DCT-T-K"; "DCT-T-I"; "DCT-T-O"; "DCT-T-N"; "DCT-T-L" ] do
              if not (has "DCL-005" (assess (good [ "tests", jstrs [ "DCT-T-A"; skipped ] ]))) then
                  yield sprintf "DCL-005: %s, declared only in a skipped form or a block comment, is accepted" skipped

          if not (has "DCL-005" (assess (good [ "tests", jstrs [ "DCT-T-A"; "DCT-T-Q" ] ]))) then
              yield "DCL-005: a test id that is only the prefix of a longer id (DCT-004-A in DCT-004-AX) is accepted"
          if not (has "DCL-006" (assessWith h { full with Records = Some h.Side } (good []))) then
              yield "DCL-006: a records commit that does not descend from the candidate is accepted"
          if not (has "DCL-007" (assessWith h { full with Records = None } (good []))) then
              yield "DCL-007: review evidence absent from the candidate (and no records commit) is accepted"
          if not (has "DCL-007" (assess (good [ "reviewerEvidence", JStr "acceptance/defect-classes.json" ]))) then
              yield "DCL-007: the closures record cited as its own review evidence is accepted"

          // The Rehearsal 7 second review's `./acceptance/defect-classes.json#self` and its variants.
          for spelled in [ "./acceptance/defect-classes.json#self"; "acceptance//defect-classes.json"; "acceptance/reviews/../defect-classes.json"; "acceptance/copy.json" ] do
              let report = assess (good [ "reviewerEvidence", JStr spelled ])

              if not (has "DCL-007" report && problems report |> List.exists (fun p -> p.Contains "closures record")) then
                  yield sprintf "DCL-007: the closures record cited as its own review evidence as `%s` is accepted" spelled

          if not (has "DCL-007" (assess (good [ "reviewerEvidence", JStr "../outside/review.md" ]))) then
              yield "DCL-007: review evidence outside the repository root is accepted"

          let oldReview = assessWith h { full with Records = Some h.Candidate } (good [ "reviewerEvidence", JStr "acceptance/reviews/old.md" ])

          if not (has "DCL-007" oldReview) then
              yield "DCL-007: review evidence that no commit after the fix added or changed is accepted"
          if not (has "DCL-008" (assess (closures [ closureOf [ "id", JStr "DC-U"; "fixSha", fix; "closedAt", closedAt ]; closureOf [ "fixSha", fix; "closedAt", closedAt ] ]))) then
              yield "DCL-008: review evidence that does not name the class is accepted"
          if not (has "DCL-009" (assess (closure [ "fixSha", fix ]))) then
              yield "DCL-009: a closure without closedAt is accepted under a freeze"
          if not (has "DCL-009" (assess (good [ "closedAt", JStr(h.FixedAt.AddHours(3.0).ToString("o")) ]))) then
              yield "DCL-009: a closure recorded after the freeze is accepted"
          if not (has "DCL-009" (assess (good [ "closedAt", JStr "2020-01-01T00:00:00Z" ]))) then
              yield "DCL-009: a closedAt earlier than the fix commit (a self-asserted past date) is accepted"
          if not (has "DCL-010" (assess (good [ "fixSha", JStr h.Root ]))) then
              yield "DCL-010: a fix commit from before the base (the initial commit) is accepted"
          if not (has "DCL-010" (assess (good [ "fixSha", JStr h.Base ]))) then
              yield "DCL-010: the base commit itself is accepted as a fix"
          if not (has "DCL-011" (assess (good [ "fixSha", JStr h.ClosuresOnly ]))) then
              yield "DCL-011: a fix commit that changes only the closures record is accepted"
          if not (has "DCL-012" (assessWith h { full with Curation = Some(curationOf []) } (good [ "tests", jstrs [] ]))) then
              yield "DCL-012: a closure with no regression test is accepted"
          if not (has "DCL-013" (assess (closures [ closureOf [ "fixSha", fix; "closedAt", closedAt ]; closureOf [ "id", JStr "DC-V"; "status", JStr "Pass" ] ]))) then
              yield "DCL-013: an unknown status (`Pass`) is treated as open instead of refused"

          let missing = assessWith h { full with Curation = Some(curationOf [ "DCT-T-A", "DC-T"; "DCT-T-C", "DC-OTHER" ]) } (good [])

          if not (has "DCL-014" missing) || not (arr "classes" missing |> List.exists (fun c -> str "id" c = "DC-OTHER" && str "state" c = "MISSING")) then
              yield "DCL-014: a curated class absent from the closures is not MISSING"

          for label, run in [ "no freeze", { full with Freeze = None }; "no curation", { full with Curation = None } ] do
              let report = assessWith h run (good [])

              if str "verdict" report <> "INCOMPLETE" then
                  yield sprintf "with %s the verdict is %s, not INCOMPLETE" label (str "verdict" report)

          if str "verdict" (assessWith h { full with Curation = Some(curationOf []) } (closures [])) <> "INCOMPLETE" then
              yield "an empty closures record is not INCOMPLETE"

          let open' = assessWith h { full with Curation = Some(curationOf []) } (closure [ "status", JStr "FAIL" ])

          if str "verdict" open' <> "VERIFIED" || (arr "classes" open' |> List.map (str "state")) <> [ "OPEN" ] then
              yield "an unclaimed (FAIL) class is not reported OPEN without refusing the verdict"
          if (field "closures" ok |> Option.map (str "sha256")) <> Some "00" then
              yield "the report does not record the closures' source digest"
          if str "base" ok <> h.Base then
              yield "the report does not echo the full base SHA" ]
        |> List.map (sprintf "defect closure: %s")
    finally
        try
            Directory.Delete(h.Repo, true)
        with _ -> ()
