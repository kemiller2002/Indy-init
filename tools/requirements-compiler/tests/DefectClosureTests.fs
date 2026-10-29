/// `verify-defect-closures` (R7-REM-008) against a throwaway git history: a closure verifies only with a fix commit
/// in the candidate's own ancestry, its regression tests present as tests (not comments), and review evidence that
/// names the class and the fix. Rehearsal 6's own closures are the R5-SHA case (DCL-002).
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

/// main: base, fix, candidate, records; a side branch holds a commit that is not the candidate's ancestor.
type private History =
    { Repo: string
      Fix: string
      Candidate: string
      Records: string
      Side: string }

let private history () =
    let repo = Path.Combine(Path.GetTempPath(), $"defect-closure-{Guid.NewGuid():N}")
    Directory.CreateDirectory repo |> ignore
    git repo [ "init"; "-q"; "-b"; "main" ] |> ignore
    write repo "tests/Regression.fs" "// DCT-T-B is described here only in a comment\nlet ``DCT-T-A a held conflict keeps the change`` () = ()\n"
    commit repo "base" |> ignore
    write repo "src/Store.fs" "let fixed = true\n"
    let fix = commit repo "fix the class"
    write repo "src/Other.fs" "let more = 1\n"
    let candidate = commit repo "candidate"
    write repo "acceptance/reviews/final.md" $"## DC-T\nClosed by {fix.Substring(0, 10)}; regression DCT-T-A.\n"
    let records = commit repo "records only"
    git repo [ "checkout"; "-q"; "-b"; "side"; fix ] |> ignore
    write repo "src/Side.fs" "let side = 1\n"
    let side = commit repo "side"
    git repo [ "checkout"; "-q"; "main" ] |> ignore

    { Repo = repo
      Fix = fix
      Candidate = candidate
      Records = records
      Side = side }

let private closure (fields: (string * Json) list) =
    let defaults =
        [ "id", JStr "DC-T"
          "status", JStr "PASS"
          "fixSha", JNull
          "tests", jstrs [ "DCT-T-A" ]
          "reviewerEvidence", JStr "acceptance/reviews/final.md#dc-t" ]

    let overridden = defaults |> List.map (fun (k, v) -> k, (fields |> List.tryFind (fst >> (=) k) |> Option.map snd |> Option.defaultValue v))
    let extra = fields |> List.filter (fun (k, _) -> not (List.exists (fst >> (=) k) defaults))
    JObj [ "schema", JStr "causa.acceptance-defect-classes/1"; "defectClasses", JArr [ JObj(overridden @ extra) ] ]

let private curation =
    JObj [ "tests", JArr [ JObj [ "id", JStr "DCT-T-A"; "defectClass", JStr "DC-T" ]; JObj [ "id", JStr "DCT-T-C"; "defectClass", JStr "DC-OTHER" ] ] ]

let private assess (h: History) (records: string option) (freeze: DateTimeOffset option) (cur: Json option) (closures: Json) =
    DefectClosure.assess
        { Repo = h.Repo
          Candidate = h.Candidate
          Records = records
          Closures = closures
          Curation = cur
          FreezeAt = freeze
          TestsPath = "tests" }

let private problems (report: Json) =
    arr "classes" report |> List.collect (strs "problems")

let private has (rule: string) (report: Json) =
    str "verdict" report = "REFUSED" && (problems report |> List.exists (fun p -> p.StartsWith(rule, StringComparison.Ordinal)))

let all () : string list =
    let h = history ()

    try
        let fix = JStr h.Fix
        let ok = assess h (Some h.Records) None (Some curation) (closure [ "fixSha", fix ])

        [ if str "verdict" ok <> "VERIFIED" then
              yield sprintf "a closure with its own fix, test and review is not VERIFIED: %A" (problems ok)
          if not (has "DCL-001" (assess h (Some h.Records) None None (closure [ "fixSha", JStr(h.Fix.Substring(0, 7)) ]))) then
              yield "DCL-001: a short fix SHA is accepted"
          if not (has "DCL-002" (assess h (Some h.Records) None None (closure [ "fixSha", JStr(String('5', 40)) ]))) then
              yield "DCL-002: a fix commit from another repository's history (Rehearsal 6's R5 SHAs) is accepted"
          if not (has "DCL-003" (assess h (Some h.Records) None None (closure [ "fixSha", JStr h.Side ]))) then
              yield "DCL-003: a fix commit that is not the candidate's ancestor is accepted"
          if not (has "DCL-004" (assess h (Some h.Records) None (Some curation) (closure [ "fixSha", fix; "tests", jstrs [] ]))) then
              yield "DCL-004: a closure that drops its curated regression test is accepted"
          if not (has "DCL-005" (assess h (Some h.Records) None None (closure [ "fixSha", fix; "tests", jstrs [ "DCT-T-B" ] ]))) then
              yield "DCL-005: a regression test named only in a comment (R6's DCT-004-A) is accepted"
          if not (has "DCL-005" (assess h (Some h.Records) None None (closure [ "fixSha", fix; "tests", jstrs [ "DCT-T-Z" ] ]))) then
              yield "DCL-005: an absent regression test is accepted"
          if not (has "DCL-006" (assess h (Some h.Side) None None (closure [ "fixSha", fix ]))) then
              yield "DCL-006: a records commit that does not descend from the candidate is accepted"
          if not (has "DCL-007" (assess h None None None (closure [ "fixSha", fix ]))) then
              yield "DCL-007: review evidence absent from the candidate (and no records commit) is accepted"
          if not (has "DCL-008" (assess h (Some h.Records) None None (closure [ "id", JStr "DC-U"; "fixSha", fix ]))) then
              yield "DCL-008: review evidence that does not name the class is accepted"

          let freeze = DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero)

          if not (has "DCL-009" (assess h (Some h.Records) (Some freeze) None (closure [ "fixSha", fix ]))) then
              yield "DCL-009: a closure without closedAt is accepted under a freeze"
          if not (has "DCL-009" (assess h (Some h.Records) (Some freeze) None (closure [ "fixSha", fix; "closedAt", JStr "2026-10-29T12:30:00Z" ]))) then
              yield "DCL-009: a closure recorded after the freeze is accepted"
          if str "verdict" (assess h (Some h.Records) (Some freeze) None (closure [ "fixSha", fix; "closedAt", JStr "2026-10-29T11:30:00Z" ])) <> "VERIFIED" then
              yield "DCL-009: a closure recorded before the freeze is refused"

          let open' = assess h (Some h.Records) None None (closure [ "status", JStr "FAIL" ])

          if str "verdict" open' <> "VERIFIED" || (arr "classes" open' |> List.map (str "state")) <> [ "OPEN" ] then
              yield "an unclaimed (FAIL) class is not reported OPEN without refusing the verdict" ]
        |> List.map (sprintf "defect closure: %s")
    finally
        try
            Directory.Delete(h.Repo, true)
        with _ -> ()
