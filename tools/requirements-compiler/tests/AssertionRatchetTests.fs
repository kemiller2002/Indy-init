/// `assertion-ratchet` (R7-REM-011, Rehearsal 6 RF-F-8) against a throwaway git history: an assertion a bound test
/// once held may not disappear from the candidate unless a resolved tag change covers the test. The independent
/// review's bypasses of the first version (Rehearsal 7: a skip marker, a block comment, `if (false)`, a `.not` on a
/// continuation line, a move into a skipped test, a weakened helper, an unbound base) each have a case here.
module AssertionRatchetTests

open System
open System.IO
open Json

let private git (repo: string) (args: string list) =
    match Git.run repo ([ "-c"; "user.name=ratchet-tests"; "-c"; "user.email=tests@example.invalid"; "-c"; "commit.gpgsign=false" ] @ args) with
    | Ok bytes -> Text.Encoding.UTF8.GetString(bytes).Trim()
    | Error e -> failwithf "git %A: %s" args e

let private commitFile (repo: string) (path: string) (text: string) =
    let full = Path.Combine(repo, path)
    Directory.CreateDirectory(Path.GetDirectoryName full) |> ignore
    File.WriteAllText(full, text)
    git repo [ "add"; "-A" ] |> ignore
    git repo [ "commit"; "-q"; "-m"; path ] |> ignore
    git repo [ "rev-parse"; "HEAD" ]

let private spec (o2: string list) =
    String.concat
        "\n"
        ([ "import { test, expect } from \"@playwright/test\";"
           "test(\"UI-034-O1\", async ({ page }) => {"
           "  await expect(box).toHaveCount(1);"
           "});"
           "test(\"UI-034-O2\", { tag: [\"@responsive\"] }, async ({ page }) => {" ]
         @ o2
         @ [ "});"; "" ])

let private selected = "  await expect(other).toHaveAttribute(\"data-selected\", \"true\");"

let private kickoffPath = "kickoff/indy-init.kickoff.json"

/// A throwaway repository whose first commit adds the kickoff record (`{}`), so the first commit binds the base.
let private withRepo (f: string -> 'a) =
    let repo = Path.Combine(Path.GetTempPath(), $"ratchet-{Guid.NewGuid():N}")
    Directory.CreateDirectory(Path.Combine(repo, "kickoff")) |> ignore
    git repo [ "init"; "-q"; "-b"; "main" ] |> ignore
    File.WriteAllText(Path.Combine(repo, kickoffPath), "{}\n")

    try
        f repo
    finally
        try
            Directory.Delete(repo, true)
        with _ -> ()

let private tagChange obligation (resolution: Json) =
    JObj [ "changes", JArr [ JObj [ "obligation", JStr obligation; "resolution", resolution ] ] ]

let private reviewPath = "acceptance/reviews/final-review.json"

/// A review record (ReviewRecord) whose finding names the UI-034 obligations.
let private reviewRef = reviewPath + "#RF-F-8"

let private reviewDoc (obligations: string list) =
    serialize (
        JObj
            [ "schema", JStr "indy-init.independent-review/1"
              "reviewer", JStr "independent-reviewer"
              "role", JStr "review"
              "verdict", JStr "BLOCKING_FINDINGS"
              "findings", JArr [ JObj [ "id", JStr "RF-F-8"; "obligations", jstrs obligations ] ] ]
    )

/// A tag change that names its review, as PlanVerification requires.
let private reviewedTagChange obligation (resolution: Json) =
    JObj [ "changes", JArr [ JObj [ "obligation", JStr obligation; "review", JStr reviewRef; "resolution", resolution ] ] ]

/// A records commit (after everything so far) holding the tag changes at their fixed path, the review record, and an
/// unrelated review that names another obligation.
let private recordsWith repo (tags: Json) =
    Directory.CreateDirectory(Path.Combine(repo, "acceptance/reviews/final")) |> ignore
    File.WriteAllText(Path.Combine(repo, reviewPath), reviewDoc [ "UI-034-O1"; "UI-034-O2" ])
    File.WriteAllText(Path.Combine(repo, "acceptance/reviews/unrelated.json"), reviewDoc [ "UI-999-O1" ])
    commitFile repo AssertionRatchet.tagChangesPath (serialize tags)

/// The base as the caller states it (abbreviated, as a person would write it), checked against the kickoff record.
let private assessWith repo (baseCommit: string option) candidate (tagFile: Json option) (records: string option) (kickoff: string option) =
    AssertionRatchet.assess
        { Repo = repo
          Base = baseCommit |> Option.map (fun b -> b.Substring(0, 7))
          Candidate = candidate
          Roots = [ "tests" ]
          TagChangesFile = tagFile
          Records = records
          Plan = None
          KickoffPath = kickoff }

/// With tag changes, they are committed in a records commit (see recordsWith) and read from it.
let private assess repo (baseCommit: string) candidate (tags: Json option) =
    assessWith repo (Some baseCommit) candidate None (tags |> Option.map (recordsWith repo)) (Some kickoffPath)

let private verdict repo baseCommit candidate = str "verdict" (assess repo baseCommit candidate None)

/// The Rehearsal 7 review's synthetic spec (its branch `bypass`, bb4827b): each test, then one bypass applied.
let private zz (o1: string list) (o2: string list) (o3: string list) (o4: string list) (o5: string list) (dead: string list) (helper: string) =
    String.concat
        "\n"
        ([ "import { test, expect } from \"./_lib.js\";"
           helper
           "test(\"ZZ-001-O1 skip inside\", async ({ page }) => {" ]
         @ o1
         @ [ "});"; "test(\"ZZ-001-O2 block comment\", async ({ page }) => {" ]
         @ o2
         @ [ "});"; "test(\"ZZ-001-O3 if false\", async ({ page }) => {" ]
         @ o3
         @ [ "});"; "test(\"ZZ-001-O4 multiline\", async ({ page }) => {" ]
         @ o4
         @ [ "});"; "test(\"ZZ-001-O5 moved to dead test\", async ({ page }) => {" ]
         @ o5
         @ [ "});" ]
         @ dead
         @ [ "test(\"ZZ-001-O6 helper\", async ({ page }) => {"; "  await ready(page);"; "});"; "" ])

let private held (letter: string) = $"  await expect(page.locator(\"{letter}\")).toHaveCount(1);"
let private readyHelper = "const ready = async (page) => expect(page.locator(\"#boot\")).toHaveCount(0);"
let private zzBase = zz [ held "a" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] readyHelper

let private bypasses =
    [ "a `test.skip()` added inside the test", zz [ "  test.skip();"; held "a" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] readyHelper
      "an assertion put in a /* ... */ block comment", zz [ held "a" ] [ "  /*"; held "b"; "  */" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] readyHelper
      "an `if (false)` put in front of an assertion", zz [ held "a" ] [ held "b" ] [ "  if (false)"; held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] readyHelper
      "a `.not` added on a continuation line", zz [ held "a" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .not.toHaveCount(1);" ] [ held "e" ] [] readyHelper
      "an assertion moved into a `test.skip` test",
      zz [ held "a" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [] [ "test.skip(\"dead\", async ({ page }) => {"; held "e"; "});" ] readyHelper
      "a helper's assertion outside any test weakened",
      zz [ held "a" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] "const ready = async (page) => expect(page.locator(\"#boot\")).toHaveCount(0, { timeout: 1 }).catch(() => {});"
      "a `test.fixme` declaration", (zzBase.Replace("test(\"ZZ-001-O1", "test.fixme(\"ZZ-001-O1"))
      "a `test.describe.skip` around the tests",
      (zzBase.Replace("test(\"ZZ-001-O1", "test.describe.skip(\"all\", () => {\ntest(\"ZZ-001-O1").Replace("test(\"ZZ-001-O2", "});\ntest(\"ZZ-001-O2"))
      "a `test.only` elsewhere in the file", zzBase + "test.only(\"ZZ-ONLY\", async () => {});\n"
      // The second review's branch bypass3 (b685d20..dbdb5e4).
      "an assertion moved into a template literal", zz [ "  const unused = `"; held "a"; "  `;" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] readyHelper
      "a `testInfo.skip()` through the fixture argument",
      (zz [ "  testInfo.skip();"; held "a" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] readyHelper)
          .Replace("\"ZZ-001-O1 skip inside\", async ({ page }) =>", "\"ZZ-001-O1 skip inside\", async ({ page }, testInfo) =>")
      "a `test.skip()` on a one-line test's declaration line",
      zzBase.Replace("test(\"ZZ-001-O1 skip inside\", async ({ page }) => {\n" + held "a", "test(\"ZZ-001-O1 skip inside\", async ({ page }) => { test.skip();" + held "a" + " });\ntest(\"ZZ-001-O1b\", async () => {")
      "a module-level `const { skip } = test` alias called in the test",
      zz [ "  skip();"; held "a" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] (readyHelper + "\nconst { skip } = test;")
      "a `test.info().skip()` in the test",
      zz [ "  test.info().skip();"; held "a" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] readyHelper
      // The third review: /tmp/claude-0/syn/repo ZZW.spec.js and r6c fake3's dct4c.spec.js.
      "an assertion inside a template whose ${...} holds a string with } and a backtick",
      zz [ "  const dead = `${\"}\" + \"`\"}"; held "a"; "  `; const q = \"`\";" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] readyHelper
      "a `test.skip()` hidden behind a regular expression after an `if (...)` head",
      zz [ "  if (true) /`/.test(\"\");"; "  test.skip();"; "  // `"; held "a" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] readyHelper ]

/// What the scanner should make of a spec: its test X read and skipped, read and live, UNPARSEABLE, or unsupported.
type private Expect =
    | SkippedX
    | LiveX
    | Unreadable
    | NotRead

/// The fourth review's lexer cases (E1-E3): an object literal's `}` before a division (r6c fake4, syn/repo3), and the
/// shapes around it; a final line comment without a newline (syn/repo2 eof.spec.js); JSX/TSX.
let private scannerCases =
    let inTest (body: string list) = String.concat "\n" ([ "import { test, expect } from \"x\";"; "test(\"X\", async ({ page }) => {" ] @ body @ [ "});"; "" ])

    [ "an object literal's `}` before a division hides no skip (fake4)", "a.spec.js",
      inTest [ "  const v = {valueOf(){return 4}} / 2; const q = \"/\"; const r = \"`\";"; "  test.skip();"; "  // `" ], SkippedX
      "a template inside an object literal before a division", "a.spec.js",
      inTest [ "  const o = { t: `a ${1} b` } / 2; const q = \"`\";"; "  test.skip();"; "  // `" ], SkippedX
      "an arrow returning an object literal, then divisions", "a.spec.js",
      inTest [ "  const f = () => ({ a: 1 });"; "  const g = items.length / total / 3;"; "  const h = (await page.evaluate(() => 4)) / 2;"; "  test.skip();" ], SkippedX
      "a block's `}` before a regular expression", "a.spec.js",
      inTest [ "  if (ready) { go(); }"; "  /`/.test(\"\");"; "  test.skip();"; "  // `" ], SkippedX
      "a class body's `}` before a `/` (cannot be told)", "a.spec.js",
      inTest [ "  class A { m() { return 1 } }"; "  /`/.test(\"\");"; "  test.skip();"; "  // `" ], Unreadable
      "a function expression's `}` before a `/` (cannot be told)", "a.spec.js",
      inTest [ "  const h = function () { return 1 } / 2; const q = \"/\"; const r = \"`\";"; "  test.skip();"; "  // `" ], Unreadable
      "divisions after parenthesised expressions (syn/repo2 div.spec.js)", "a.spec.js",
      inTest [ "  const half = (await page.evaluate(() => 4)) / 2;"; "  const ratio = items.length / total / 3;"; "  await expect(page).toHaveTitle(\"b\");" ], LiveX
      "a final line comment without a newline (eof.spec.js)", "a.spec.js",
      (inTest [ "  await expect(page).toHaveTitle(\"a\");" ]) + "// end", LiveX
      // G1 (fifth review: r6c fake5, syn/repo4): a block's `}` inside a template's ${...}, then `/`.
      "a block's `}` then `/` inside a template's ${...} (fake5)", "a.spec.js",
      inTest [ "  const label = `${(function () {"; "    if (true) {} /\"/.test(\"\"); const z = \"`\";"; "    test.skip();"; "    // `"; "    return \"x\";"; "  })()}`;" ], Unreadable
      "an object literal inside a template's ${...}", "a.spec.js",
      inTest [ "  const t = `${ {a: 1}.a } and ${ [1].map((x) => { return x; }) }`;"; "  test.skip();" ], SkippedX
      // H1 (final review: r6c fake6, dct4g.spec.js): a postfix `++` before a division.
      "a `/` after a postfix `++` (fake6)", "a.spec.js",
      inTest [ "  let i = 0;"; "  const v = i++ / 2; const q = \"/\"; const r = \"`\";"; "  test.skip();"; "  // `" ], Unreadable
      "a TSX spec", "a.spec.tsx", inTest [ "  render(<p>Don't</p>);"; "  await expect(page).toHaveTitle(\"a\");" ], NotRead ]

let private scannerFailures () =
    [ for label, path, text, expected in scannerCases do
          let actual =
              match TestBlocks.parse path text with
              | Some(TestBlocks.Blocks blocks) ->
                  match blocks |> List.tryFind (fun b -> b.Title = "X") with
                  | Some b when b.Skipped -> Some SkippedX
                  | Some _ -> Some LiveX
                  | None -> None
              | Some(TestBlocks.Unparseable _) -> Some Unreadable
              | Some(TestBlocks.Unsupported _) -> Some NotRead
              | None -> None

          if actual <> Some expected then
              yield sprintf "scanner: %s is read as %A, not %A" label actual expected ]

let all () : string list =
    [ withRepo (fun repo ->
          // E3: a tracked JSX/TSX file is not read, so the ratchet is at best INCOMPLETE.
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])
          let tsx = commitFile repo "tests/Widget.spec.tsx" "test(\"W\", () => { expect(1).toBe(1); });\n"
          let report = assess repo base' tsx None

          [ if str "verdict" report <> "INCOMPLETE" || arr "unsupported" report |> List.isEmpty then
                yield sprintf "a tracked TSX file leaves the verdict %s, not INCOMPLETE with it listed as unsupported" (str "verdict" report) ])
      withRepo (fun repo ->
          // H2: an F# file's backticks in comments (``identifiers``) are not listed.
          let fs = commitFile repo "tests/Support.fs" "module Support\n// see ``held`` below\nlet held = 1\n"

          [ if arr "reviewAttention" (assess repo fs fs None) |> List.exists (fun f -> (str "file" f).StartsWith("tests/Support.fs@", StringComparison.Ordinal)) then
                yield "reviewAttention lists an F# file for a backtick in a comment" ])
      withRepo (fun repo ->
          // Hardening: a file holding a backtick in a comment is listed for the independent review.
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])
          let noted = commitFile repo "tests/UI-034.spec.js" (spec [ selected; "  // a stray `" ])
          let report = assess repo base' noted None
          let listed = arr "reviewAttention" report |> List.map (str "file")

          [ if not (listed |> List.contains $"tests/UI-034.spec.js@{noted}") || listed |> List.contains $"tests/UI-034.spec.js@{base'}" then
                yield sprintf "reviewAttention does not list exactly the version with a backtick in a comment: %A" listed ])
      withRepo (fun repo ->
          // E4: an UNPARSEABLE candidate file is listed once.
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])
          let broken = commitFile repo "tests/Broken.spec.js" "const t = `\n"
          let report = assess repo base' broken None

          [ if arr "unparseable" report |> List.length <> 1 then
                yield sprintf "an UNPARSEABLE file is listed %d times" (arr "unparseable" report |> List.length) ])
      withRepo (fun repo ->
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected; "  await page.keyboard.press(\"Escape\");"; selected ])
          let removedOne = commitFile repo "tests/UI-034.spec.js" (spec [ selected; "  await page.keyboard.press(\"Escape\");" ])
          let report = assess repo base' removedOne None

          [ if str "verdict" report <> "FAIL" then
                yield "RF-F-8: removing one of a test's two identical assertions passes"
            if str "verdict" (assess repo base' removedOne (Some(tagChange "UI-034-O2" JNull))) <> "FAIL" then
                yield "a tag change without a resolution excuses a removed assertion"
            if str "verdict" (assess repo base' removedOne (Some(tagChange "UI-034-O2" (JStr "  ")))) <> "FAIL" then
                yield "a tag change with a blank resolution excuses a removed assertion"
            if str "verdict" (assess repo base' removedOne (Some(tagChange "UI-034-O2" (JObj [ "kind", JStr "not-rebound"; "reason", JStr "superseded" ])))) <> "FAIL" then
                yield "a review resolution that names no review (PlanVerification semantics) excuses a removed assertion"
            if str "verdict" (assess repo base' removedOne (Some(tagChange "UI-034-O2" (JObj [ "kind", JStr "rebound"; "tests", jstrs [ "x" ]; "review", JStr "R-1" ])))) <> "FAIL" then
                yield "a `rebound` resolution to tests no plan binds excuses a removed assertion"
            // F3: only PlanVerification's rule resolves a tag change; free text (the second review's `"ok"`) does not.
            for text in [ "ok"; "owner decision DEC-R7-009" ] do
                if str "verdict" (assess repo base' removedOne (Some(reviewedTagChange "UI-034-O2" (JStr text)))) <> "FAIL" then
                    yield sprintf "a free-text resolution `%s` excuses a removed assertion" text
            let review = JObj [ "kind", JStr "not-rebound"; "reason", JStr "superseded by UI-034-O3"; "review", JStr reviewRef ]
            if str "verdict" (assess repo base' removedOne (Some(tagChange "UI-034-O2" review))) <> "FAIL" then
                yield "a tag change that names no review of its own (PlanVerification REVIEW-TAG-CHANGE) excuses a removed assertion"
            let resolved = assess repo base' removedOne (Some(reviewedTagChange "UI-034-O2" review))
            if str "verdict" resolved <> "PASS" || (arr "removed" resolved |> List.map (str "state")) <> [ "RESOLVED_TAG_CHANGE" ] then
                yield "a PlanVerification `not-rebound` review resolution is not honoured"
            let byTitle = JObj [ "changes", JArr [ JObj [ "test", JStr "tests/UI-034.spec.js > UI-034-O2"; "review", JStr reviewRef; "resolution", review ] ] ]
            if str "verdict" (assess repo base' removedOne (Some byTitle)) <> "PASS" then
                yield "a tag change keyed on the full test title is not honoured"
            let otherTitle = JObj [ "changes", JArr [ JObj [ "test", JStr "UI-034-O1"; "obligation", JStr "UI-034-O2"; "review", JStr reviewRef; "resolution", review ] ] ]
            if str "verdict" (assess repo base' removedOne (Some otherTitle)) <> "FAIL" then
                yield "a tag change naming another test excuses this test's removal through its obligation"
            // D4 (third review, tc3.json): a review that is free text, the tag-change record itself, or a review that
            // does not name the obligation does not stand; tag changes from disk never PASS.
            let selfReview = JObj [ "kind", JStr "not-rebound"; "reason", JStr "looser is fine"; "review", JStr "agent-self-review" ]
            let withReview (reference: string) (resolution: Json) =
                JObj [ "changes", JArr [ JObj [ "obligation", JStr "UI-034-O2"; "test", JStr "UI-034-O2"; "review", JStr reference; "resolution", resolution ] ] ]
            for label, tags in
                [ "a free-text review (agent-self-review)", withReview "agent-self-review" selfReview
                  "the tag-change record as its own review", withReview (AssertionRatchet.tagChangesPath + "#self") (JObj [ "kind", JStr "not-rebound"; "reason", JStr "r"; "review", JStr(AssertionRatchet.tagChangesPath + "#self") ])
                  "a review that does not name the obligation", withReview "acceptance/reviews/unrelated.json" (JObj [ "kind", JStr "not-rebound"; "reason", JStr "r"; "review", JStr "acceptance/reviews/unrelated.json" ]) ] do
                if str "verdict" (assess repo base' removedOne (Some tags)) <> "FAIL" then
                    yield sprintf "a tag change whose review is %s excuses a removed assertion" label
            let records = recordsWith repo (reviewedTagChange "UI-034-O2" review)
            let fromDisk = assessWith repo (Some base') removedOne (Some(reviewedTagChange "UI-034-O2" review)) (Some records) (Some kickoffPath)
            if str "verdict" fromDisk <> "INCOMPLETE" then
                yield sprintf "tag changes supplied from a file on disk leave the verdict %s, not INCOMPLETE" (str "verdict" fromDisk)
            let unbound = assessWith repo (Some base') base' None None None
            if str "verdict" unbound <> "INCOMPLETE" || field "baseVerified" unbound <> Some(JBool false) then
                yield "a ratchet with no kickoff record binding the base is not INCOMPLETE"
            // N2: the second review forged a kickoff file naming the candidate's parent; the base is now read from git.
            let parent = assessWith repo (Some base') removedOne None None (Some kickoffPath)
            if str "verdict" (assessWith repo (Some removedOne) removedOne None None (Some kickoffPath)) <> "REFUSED" then
                yield "a --base other than the commit that added the kickoff record is not REFUSED"
            if str "verdict" parent <> "FAIL" || field "baseVerified" parent <> Some(JBool true) then
                yield "the commit that added the kickoff record does not bind the base"
            let derived = assessWith repo None removedOne None None (Some kickoffPath)
            if str "base" derived <> base' || field "baseVerified" derived <> Some(JBool true) then
                yield "with no --base the base is not derived from the kickoff record"
            if str "verdict" (assessWith repo (Some base') removedOne None None (Some "kickoff/elsewhere.json")) <> "REFUSED" then
                yield "a kickoff record the candidate's history does not hold is not REFUSED"
            if str "base" report <> base' then
                yield "the report does not echo the full base SHA" ])
      withRepo (fun repo ->
          // A record that names an earlier commit binds it; a later edit of the record does not move the base.
          File.Delete(Path.Combine(repo, kickoffPath))
          let start = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])
          let receipt = commitFile repo kickoffPath (serialize (JObj [ "initialRepository", JObj [ "head", JStr start ] ]))
          let edited = commitFile repo kickoffPath (serialize (JObj [ "base", JStr receipt ]))
          let candidate = commitFile repo "tests/UI-034.spec.js" (spec [])

          [ if str "verdict" (assessWith repo (Some start) candidate None None (Some kickoffPath)) <> "FAIL" then
                yield "a kickoff receipt's initialRepository.head does not bind the base"
            if str "verdict" (assessWith repo (Some receipt) candidate None None (Some kickoffPath)) <> "REFUSED" then
                yield "a later edit of the kickoff record moves the base"
            if str "base" (assessWith repo None edited None None (Some kickoffPath)) <> start then
                yield "the base derived from a kickoff receipt is not its initialRepository.head" ])
      withRepo (fun repo ->
          File.Delete(Path.Combine(repo, kickoffPath))
          let start = commitFile repo "README.md" "start\n"
          let candidate = commitFile repo kickoffPath (serialize (JObj [ "base", JStr "HEAD" ]))
          commitFile repo "README.md" "later\n" |> ignore

          [ if str "verdict" (assessWith repo (Some start) candidate None None (Some kickoffPath)) <> "REFUSED" then
                yield "a kickoff record naming a commit that is not its ancestor binds a base" ])
      withRepo (fun repo ->
          // D2: only kickoff/indy-init.kickoff.json binds the base; another file the history adds (an ROS envelope) does not.
          let envelope = commitFile repo ".ros/executions/x/envelope.json" "{}\n"
          let candidate = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])

          [ if str "verdict" (assessWith repo (Some envelope) candidate None None (Some ".ros/executions/x/envelope.json")) <> "REFUSED" then
                yield "a caller-chosen kickoff path (an ROS envelope) binds the base" ])
      withRepo (fun repo ->
          // D1: a file the lexer cannot read consistently is never trusted: the verdict is not PASS.
          let base' = commitFile repo "tests/acceptance/ZZZ.spec.js" zzBase
          let broken = commitFile repo "tests/acceptance/ZZZ.spec.js" (zzBase + "const t = `\n")
          let mended = commitFile repo "tests/acceptance/ZZZ.spec.js" zzBase
          let report = assess repo base' mended None

          [ if verdict repo base' broken = "PASS" then
                yield "an UNPARSEABLE candidate file passes"
            if str "verdict" report <> "INCOMPLETE" || arr "unparseable" report |> List.isEmpty then
                yield sprintf "an UNPARSEABLE version in the history leaves the verdict %s, not INCOMPLETE" (str "verdict" report) ])
      withRepo (fun repo ->
          // D5: a base test skipped only conditionally may run, so its assertions are held; an unconditionally skipped
          // base test holds nothing, and what it excluded is reported.
          let conditional = commitFile repo "tests/UI-034.spec.js" (spec [ "  test.skip(isMobile, \"no hover\");"; selected ])
          let dropped = commitFile repo "tests/UI-034.spec.js" (spec [ "  test.skip(isMobile, \"no hover\");" ])

          [ if verdict repo conditional dropped <> "FAIL" then
                yield "an assertion of a conditionally skipped base test is removed without failing" ])
      withRepo (fun repo ->
          let skipped = commitFile repo "tests/UI-034.spec.js" (spec [ "  test.skip();"; selected ])
          let dropped = commitFile repo "tests/UI-034.spec.js" (spec [ "  test.skip();" ])
          let report = assess repo skipped dropped None
          let counts = field "skippedBeforeCandidate" report |> Option.defaultValue JNull

          [ if str "verdict" report <> "PASS" then
                yield "an unconditionally skipped base test is held to its assertions"
            if intOpt "testsExcluded" counts <> Some 1L || intOpt "assertionsExcluded" counts <> Some 1L then
                yield sprintf "the report does not count the skipped base test and its excluded assertion: %s" (serialize counts)
            if strs "limits" report |> List.isEmpty then
                yield "the report does not state the ratchet's inherent limits" ])
      withRepo (fun repo ->
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])
          let loosened = commitFile repo "tests/UI-034.spec.js" ((spec [ selected ]).Replace("toHaveCount(1)", "count() >= 1 && expect(true)"))

          [ if verdict repo base' loosened <> "FAIL" then
                yield "UI-034-O1's loosened count assertion passes" ])
      withRepo (fun repo ->
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])
          commitFile repo "tests/UI-034.spec.js" (spec [ selected; "  await expect(page).toHaveTitle(\"Causa\");" ]) |> ignore
          let dropped = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])

          [ if verdict repo base' dropped <> "FAIL" then
                yield "an assertion added after the base and later removed is not caught" ])
      withRepo (fun repo ->
          let fsTest = "module T\n[<Fact>]\nlet ``DCT-001-A held conflict`` () =\n    Assert.Equal(1, held)\n    Assert.True(kept)\n"
          let base' = commitFile repo "tests/StoreTests.fs" fsTest
          let weakened = commitFile repo "tests/StoreTests.fs" (fsTest.Replace("    Assert.True(kept)\n", ""))
          let commented = commitFile repo "tests/StoreTests.fs" (fsTest.Replace("    Assert.True(kept)\n", "    (* Assert.True(kept) *)\n"))

          [ if verdict repo base' weakened <> "FAIL" then
                yield "a removed F# Assert is not caught"
            if verdict repo base' commented <> "FAIL" then
                yield "an F# Assert put in a (* ... *) comment is not caught" ])
      withRepo (fun repo ->
          let fsTest = "module T\n[<Fact>]\nlet ``DCT-001-A held conflict`` () =\n    Assert.Equal(1, held)\n"
          let base' = commitFile repo "tests/StoreTests.fs" fsTest
          let skipped = commitFile repo "tests/StoreTests.fs" (fsTest.Replace("[<Fact>]", "[<Fact(Skip = \"later\")>]"))

          [ if verdict repo base' skipped <> "FAIL" then
                yield "an F# test given [<Fact(Skip = ...)>] keeps its assertions" ])
      withRepo (fun repo ->
          let helper = "export const settled = async (page) => {\n  await expect(page.locator(\"#boot\")).toHaveCount(0);\n};\n"
          let base' = commitFile repo "tests/_lib.js" helper
          let weakened = commitFile repo "tests/_lib.js" (helper.Replace("toHaveCount(0)", "toHaveCount(0).catch(() => {})"))

          [ if verdict repo base' weakened <> "FAIL" then
                yield "a weakened assertion in a helper file (_lib.js) is not caught" ])
      withRepo (fun repo ->
          let fsHelper = commitFile repo "tests/Support.fs" "module Support\nlet held x =\n    Assert.True(x)\n"
          let fsWeakened = commitFile repo "tests/Support.fs" "module Support\nlet held x =\n    ignore x\n"

          [ if verdict repo fsHelper fsWeakened <> "FAIL" then
                yield "a weakened assertion in an F# helper module is not caught" ])
      withRepo (fun repo ->
          let base' = commitFile repo "tests/acceptance/ZZZ.spec.js" zzBase

          [ if verdict repo base' base' <> "PASS" then
                yield "the review's synthetic base does not PASS against itself" ])
      for label, text in bypasses do
          withRepo (fun repo ->
              let base' = commitFile repo "tests/acceptance/ZZZ.spec.js" zzBase
              let bypass = commitFile repo "tests/acceptance/ZZZ.spec.js" text

              [ if verdict repo base' bypass <> "FAIL" then
                    yield sprintf "a bypass passes: %s" label ])
      withRepo (fun repo ->
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])
          let unrelated = commitFile repo "tests/UI-034.spec.js" ((spec [ selected ]) + "// a comment\n/* another\n   comment */\n")

          [ if verdict repo base' unrelated <> "PASS" then
                yield "a change that keeps every assertion fails" ]) ]
    |> List.concat
    |> List.append (scannerFailures ())
    |> List.map (sprintf "assertion ratchet: %s")
