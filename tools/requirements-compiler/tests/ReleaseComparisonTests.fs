/// `release-comparison` (R7-REM-010): a pre-lock record of the newest release of every selected system. Rehearsal 6
/// verified which release it used but never compared it with the newest one before the lock (VER-001-O1, VER-002-O2).
module ReleaseComparisonTests

open System
open Json

let private set (components: (string * string) list) =
    JObj
        [ "components",
          JArr(
              components
              |> List.map (fun (id, version) ->
                  JObj
                      [ "systemId", JStr id
                        "version", JStr version
                        "role", JStr "host-tool"
                        "repository", JStr $"kemiller2002/{id}"
                        "distribution", JObj [ "mechanism", JStr "github-release" ] ])
          ) ]

let private lockAt = DateTimeOffset(2026, 10, 29, 20, 0, 0, TimeSpan.Zero)

let private seen system latest (at: DateTimeOffset) : ReleaseComparison.Observation =
    { System = system
      Source = $"https://api.github.com/repos/kemiller2002/{system}/releases/latest"
      Latest = Some latest
      PublishedAt = None
      ObservedAt = at
      AnswerSha256 = Some "00"
      Error = None }

let private statusOf system (report: Json) =
    arr "systems" report |> List.find (fun r -> str "system" r = system) |> str "status"

let private assess observations deviations =
    ReleaseComparison.assess (set [ "praxis", "3.23.0"; "forma", "0.11.0" ]) observations deviations lockAt (TimeSpan.FromHours 6.0)

let all () : string list =
    let before = lockAt.AddMinutes -10.0
    let current = assess [ seen "praxis" "3.23.0" before; seen "forma" "v0.11.0" before ] []
    let behind = assess [ seen "praxis" "3.24.0" before; seen "forma" "0.11.0" before ] []
    let deviation = JObj [ "system", JStr "praxis"; "decision", JStr "DEC-R7-001"; "selected", JStr "3.23.0"; "latest", JStr "3.24.0" ]
    let approved = assess [ seen "praxis" "3.24.0" before; seen "forma" "0.11.0" before ] [ deviation ]

    [ if str "verdict" current <> "PASS" then yield "a set equal to the newest releases (a leading v ignored) does not PASS"
      if str "verdict" behind <> "FAIL" || statusOf "praxis" behind <> "DEVIATION_UNAPPROVED" then yield "a version behind the newest release passes without a deviation"
      if str "verdict" approved <> "PASS" || statusOf "praxis" approved <> "DEVIATION_APPROVED" then yield "an owner-approved deviation is not honoured"
      let wrongDeviation = assess [ seen "praxis" "3.25.0" before; seen "forma" "0.11.0" before ] [ deviation ]
      if statusOf "praxis" wrongDeviation <> "DEVIATION_UNAPPROVED" then yield "a deviation approved for another newest release is reused"
      let after = assess [ seen "praxis" "3.23.0" (lockAt.AddMinutes 5.0); seen "forma" "0.11.0" before ] []
      if statusOf "praxis" after <> "OBSERVED_AFTER_LOCK" || str "verdict" after <> "FAIL" then yield "a query made after the lock proves the lock (R6's post-lock check)"
      let stale = assess [ seen "praxis" "3.23.0" (lockAt.AddDays -2.0); seen "forma" "0.11.0" before ] []
      if statusOf "praxis" stale <> "STALE_OBSERVATION" then yield "a query days before the lock counts as immediately before it"
      let missing = assess [ seen "praxis" "3.23.0" before ] []
      if statusOf "forma" missing <> "NOT_OBSERVED" || str "verdict" missing <> "FAIL" then yield "a system with no query passes"
      if ReleaseComparison.compareVersions "0.11.0" "0.11.0-rc.1" <> Some 1 then yield "a prerelease sorts before its release"
      if ReleaseComparison.compareVersions "1.10.0" "1.9.0" <> Some 1 then yield "versions compare numerically, not as text" ]
    |> List.map (sprintf "release comparison: %s")
