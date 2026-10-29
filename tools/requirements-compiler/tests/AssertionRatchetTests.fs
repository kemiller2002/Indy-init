/// `assertion-ratchet` (R7-REM-011, Rehearsal 6 RF-F-8) against a throwaway git history: an assertion a bound test
/// once held may not disappear from the candidate unless a tag change with a resolution covers its obligation.
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

let private assess repo baseCommit candidate tags =
    AssertionRatchet.assess repo baseCommit candidate [ "tests" ] tags

let all () : string list =
    [ withRepo (fun repo ->
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected; "  await page.keyboard.press(\"Escape\");"; selected ])
          let removedOne = commitFile repo "tests/UI-034.spec.js" (spec [ selected; "  await page.keyboard.press(\"Escape\");" ])
          let report = assess repo base' removedOne None

          [ if str "verdict" report <> "FAIL" then
                yield "RF-F-8: removing one of a test's two identical assertions passes"
            if str "verdict" (assess repo base' removedOne (Some(tagChange "UI-034-O2" JNull))) <> "FAIL" then
                yield "a tag change without a resolution excuses a removed assertion"
            let resolved = assess repo base' removedOne (Some(tagChange "UI-034-O2" (JStr "owner decision DEC-R7-009")))
            if str "verdict" resolved <> "PASS" || (arr "removed" resolved |> List.map (str "state")) <> [ "RESOLVED_TAG_CHANGE" ] then
                yield "a resolved tag change is not honoured" ])
      withRepo (fun repo ->
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])
          let loosened = commitFile repo "tests/UI-034.spec.js" ((spec [ selected ]).Replace("toHaveCount(1)", "count() >= 1 && expect(true)"))

          [ if str "verdict" (assess repo base' loosened None) <> "FAIL" then
                yield "UI-034-O1's loosened count assertion passes" ])
      withRepo (fun repo ->
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])
          commitFile repo "tests/UI-034.spec.js" (spec [ selected; "  await expect(page).toHaveTitle(\"Causa\");" ]) |> ignore
          let dropped = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])

          [ if str "verdict" (assess repo base' dropped None) <> "FAIL" then
                yield "an assertion added after the base and later removed is not caught" ])
      withRepo (fun repo ->
          let fsTest = "module T\nlet ``DCT-001-A held conflict`` () =\n    Assert.Equal(1, held)\n    Assert.True(kept)\n"
          let base' = commitFile repo "tests/StoreTests.fs" fsTest
          let weakened = commitFile repo "tests/StoreTests.fs" (fsTest.Replace("    Assert.True(kept)\n", ""))

          [ if str "verdict" (assess repo base' weakened None) <> "FAIL" then
                yield "a removed F# Assert is not caught" ])
      withRepo (fun repo ->
          let base' = commitFile repo "tests/UI-034.spec.js" (spec [ selected ])
          let unrelated = commitFile repo "tests/UI-034.spec.js" ((spec [ selected ]) + "// a comment\n")

          [ if str "verdict" (assess repo base' unrelated None) <> "PASS" then
                yield "a change that keeps every assertion fails" ]) ]
    |> List.concat
    |> List.map (sprintf "assertion ratchet: %s")
