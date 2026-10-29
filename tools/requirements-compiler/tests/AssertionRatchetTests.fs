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

let private withRepo (f: string -> 'a) =
    let repo = Path.Combine(Path.GetTempPath(), $"ratchet-{Guid.NewGuid():N}")
    Directory.CreateDirectory repo |> ignore
    git repo [ "init"; "-q"; "-b"; "main" ] |> ignore

    try
        f repo
    finally
        try
            Directory.Delete(repo, true)
        with _ -> ()

let private tagChange obligation (resolution: Json) =
    JObj [ "changes", JArr [ JObj [ "obligation", JStr obligation; "resolution", resolution ] ] ]

/// The base bound by a kickoff record naming it (abbreviated, as a person would write it).
let private assessWith repo (baseCommit: string) candidate tags (kickoff: Json option) =
    AssertionRatchet.assess
        { Repo = repo
          Base = baseCommit.Substring(0, 7)
          Candidate = candidate
          Roots = [ "tests" ]
          TagChanges = tags
          Plan = None
          Kickoff = kickoff |> Option.map (fun k -> "kickoff.json", k) }

let private assess repo (baseCommit: string) candidate tags =
    assessWith repo baseCommit candidate tags (Some(JObj [ "base", JStr(baseCommit.Substring(0, 7)) ]))

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
      "a `test.only` elsewhere in the file", zzBase + "test.only(\"ZZ-ONLY\", async () => {});\n" ]

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
            let resolved = assess repo base' removedOne (Some(tagChange "UI-034-O2" (JStr "owner decision DEC-R7-009")))
            if str "verdict" resolved <> "PASS" || (arr "removed" resolved |> List.map (str "state")) <> [ "RESOLVED_TAG_CHANGE" ] then
                yield "a resolved tag change is not honoured"
            let review = JObj [ "kind", JStr "not-rebound"; "reason", JStr "superseded by UI-034-O3"; "review", JStr "R7-final" ]
            if str "verdict" (assess repo base' removedOne (Some(tagChange "UI-034-O2" review))) <> "PASS" then
                yield "a PlanVerification `not-rebound` review resolution is not honoured"
            let byTitle = JObj [ "changes", JArr [ JObj [ "test", JStr "tests/UI-034.spec.js > UI-034-O2"; "resolution", JStr "DEC-R7-010" ] ] ]
            if str "verdict" (assess repo base' removedOne (Some byTitle)) <> "PASS" then
                yield "a tag change keyed on the full test title is not honoured"
            let otherTitle = JObj [ "changes", JArr [ JObj [ "test", JStr "UI-034-O1"; "obligation", JStr "UI-034-O2"; "resolution", JStr "DEC-R7-010" ] ] ]
            if str "verdict" (assess repo base' removedOne (Some otherTitle)) <> "FAIL" then
                yield "a tag change naming another test excuses this test's removal through its obligation"
            let unbound = assessWith repo base' base' None None
            if str "verdict" unbound <> "INCOMPLETE" || field "baseVerified" unbound <> Some(JBool false) then
                yield "a ratchet with no kickoff record binding the base is not INCOMPLETE"
            let wrongBase = assessWith repo base' base' None (Some(JObj [ "base", JStr removedOne ]))
            if str "verdict" wrongBase <> "INCOMPLETE" then
                yield "a kickoff record naming another base does not make the ratchet INCOMPLETE"
            if str "verdict" (assessWith repo base' base' None (Some(JObj [ "initialRepository", JObj [ "head", JStr base' ] ]))) <> "PASS" then
                yield "a kickoff receipt's initialRepository.head does not bind the base"
            if str "base" report <> base' then
                yield "the report does not echo the full base SHA" ])
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
          let skipped = commitFile repo "tests/StoreTests.fs" (fsTest.Replace("[<Fact>]", "[<Fact(Skip = \"later\")>]"))
          let commented = commitFile repo "tests/StoreTests.fs" (fsTest.Replace("    Assert.True(kept)\n", "    (* Assert.True(kept) *)\n"))

          [ if verdict repo base' weakened <> "FAIL" then
                yield "a removed F# Assert is not caught"
            if verdict repo weakened skipped <> "FAIL" then
                yield "an F# test given [<Fact(Skip = ...)>] keeps its assertions"
            if verdict repo base' commented <> "FAIL" then
                yield "an F# Assert put in a (* ... *) comment is not caught" ])
      withRepo (fun repo ->
          let helper = "export const settled = async (page) => {\n  await expect(page.locator(\"#boot\")).toHaveCount(0);\n};\n"
          let base' = commitFile repo "tests/_lib.js" helper
          let weakened = commitFile repo "tests/_lib.js" (helper.Replace("toHaveCount(0)", "toHaveCount(0).catch(() => {})"))
          let fsHelper = commitFile repo "tests/Support.fs" "module Support\nlet held x =\n    Assert.True(x)\n"
          let fsWeakened = commitFile repo "tests/Support.fs" "module Support\nlet held x =\n    ignore x\n"

          [ if verdict repo base' weakened <> "FAIL" then
                yield "a weakened assertion in a helper file (_lib.js) is not caught"
            if verdict repo fsHelper fsWeakened <> "FAIL" then
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
