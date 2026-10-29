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

/// A tag change that names its review, as PlanVerification requires.
let private reviewedTagChange obligation (resolution: Json) =
    JObj [ "changes", JArr [ JObj [ "obligation", JStr obligation; "review", JStr "R7-final"; "resolution", resolution ] ] ]

/// The base as the caller states it (abbreviated, as a person would write it), checked against the kickoff record.
let private assessWith repo (baseCommit: string option) candidate tags (kickoff: string option) =
    AssertionRatchet.assess
        { Repo = repo
          Base = baseCommit |> Option.map (fun b -> b.Substring(0, 7))
          Candidate = candidate
          Roots = [ "tests" ]
          TagChanges = tags
          Plan = None
          KickoffPath = kickoff }

let private assess repo (baseCommit: string) candidate tags =
    assessWith repo (Some baseCommit) candidate tags (Some kickoffPath)

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
      zz [ "  test.info().skip();"; held "a" ] [ held "b" ] [ held "c" ] [ "  await expect(page.locator(\"d\"))"; "    .toHaveCount(1);" ] [ held "e" ] [] readyHelper ]

let all () : string list =
    [ withRepo (fun repo ->
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
            let review = JObj [ "kind", JStr "not-rebound"; "reason", JStr "superseded by UI-034-O3"; "review", JStr "R7-final" ]
            if str "verdict" (assess repo base' removedOne (Some(tagChange "UI-034-O2" review))) <> "FAIL" then
                yield "a tag change that names no review of its own (PlanVerification REVIEW-TAG-CHANGE) excuses a removed assertion"
            let resolved = assess repo base' removedOne (Some(reviewedTagChange "UI-034-O2" review))
            if str "verdict" resolved <> "PASS" || (arr "removed" resolved |> List.map (str "state")) <> [ "RESOLVED_TAG_CHANGE" ] then
                yield "a PlanVerification `not-rebound` review resolution is not honoured"
            let byTitle = JObj [ "changes", JArr [ JObj [ "test", JStr "tests/UI-034.spec.js > UI-034-O2"; "review", JStr "R7-final"; "resolution", review ] ] ]
            if str "verdict" (assess repo base' removedOne (Some byTitle)) <> "PASS" then
                yield "a tag change keyed on the full test title is not honoured"
            let otherTitle = JObj [ "changes", JArr [ JObj [ "test", JStr "UI-034-O1"; "obligation", JStr "UI-034-O2"; "review", JStr "R7-final"; "resolution", review ] ] ]
            if str "verdict" (assess repo base' removedOne (Some otherTitle)) <> "FAIL" then
                yield "a tag change naming another test excuses this test's removal through its obligation"
            let unbound = assessWith repo (Some base') base' None None
            if str "verdict" unbound <> "INCOMPLETE" || field "baseVerified" unbound <> Some(JBool false) then
                yield "a ratchet with no kickoff record binding the base is not INCOMPLETE"
            // N2: the second review forged a kickoff file naming the candidate's parent; the base is now read from git.
            let parent = assessWith repo (Some base') removedOne None (Some kickoffPath)
            if str "verdict" (assessWith repo (Some removedOne) removedOne None (Some kickoffPath)) <> "REFUSED" then
                yield "a --base other than the commit that added the kickoff record is not REFUSED"
            if str "verdict" parent <> "FAIL" || field "baseVerified" parent <> Some(JBool true) then
                yield "the commit that added the kickoff record does not bind the base"
            let derived = assessWith repo None removedOne None (Some kickoffPath)
            if str "base" derived <> base' || field "baseVerified" derived <> Some(JBool true) then
                yield "with no --base the base is not derived from the kickoff record"
            if str "verdict" (assessWith repo (Some base') removedOne None (Some "kickoff/elsewhere.json")) <> "REFUSED" then
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

          [ if str "verdict" (assessWith repo (Some start) candidate None (Some kickoffPath)) <> "FAIL" then
                yield "a kickoff receipt's initialRepository.head does not bind the base"
            if str "verdict" (assessWith repo (Some receipt) candidate None (Some kickoffPath)) <> "REFUSED" then
                yield "a later edit of the kickoff record moves the base"
            if str "base" (assessWith repo None edited None (Some kickoffPath)) <> start then
                yield "the base derived from a kickoff receipt is not its initialRepository.head" ])
      withRepo (fun repo ->
          File.Delete(Path.Combine(repo, kickoffPath))
          let start = commitFile repo "README.md" "start\n"
          let candidate = commitFile repo kickoffPath (serialize (JObj [ "base", JStr "HEAD" ]))
          commitFile repo "README.md" "later\n" |> ignore

          [ if str "verdict" (assessWith repo (Some start) candidate None (Some kickoffPath)) <> "REFUSED" then
                yield "a kickoff record naming a commit that is not its ancestor binds a base" ])
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
    |> List.map (sprintf "assertion ratchet: %s")
