/// `release-comparison` (R7-REM-010): a pre-lock record of the newest release of every selected system. Rehearsal 6
/// verified which release it used but never compared it with the newest one before the lock (VER-001-O1, VER-002-O2).
/// The independent review's bypasses of the first version (Rehearsal 7: a hand-edited `latest`, a bogus source and
/// digest, a deviation with an empty decision) are the TAMPERED and deviation cases; per-component tag prefixes,
/// npm/NuGet sources and semver prerelease order are its other findings.
module ReleaseComparisonTests

open System
open Json
open ReleaseComparison

let private set (components: (string * string * string) list) =
    JObj
        [ "components",
          JArr(
              components
              |> List.map (fun (id, version, tag) ->
                  JObj
                      [ "systemId", JStr id
                        "version", JStr version
                        "tag", JStr tag
                        "role", JStr "host-tool"
                        "repository", JStr $"kemiller2002/{id}"
                        "distribution", JObj [ "mechanism", JStr "github-release" ] ])
          ) ]

let private release (tag: string) (draft: bool) (prerelease: bool) =
    JObj [ "tag_name", JStr tag; "draft", JBool draft; "prerelease", JBool prerelease; "published_at", JStr "2026-10-28T12:00:00Z" ]

let private lockAt = DateTimeOffset(2026, 10, 29, 20, 0, 0, TimeSpan.Zero)
let private before = lockAt.AddMinutes -10.0
let private releaseSet = set [ "praxis", "3.23.0", "v3.23.0"; "forma", "0.11.0", "v0.11.0" ]
let private selectedOf (rs: Json) (system: string) = selectedSystems rs |> List.find (fun s -> s.System = system)

/// An honest observation of `answer`, as `observe` records it.
let private honest (s: Selected) (answer: string) (at: DateTimeOffset) : Observation =
    { System = s.System
      Source = sourceOf s |> Option.defaultValue ""
      Latest = derive s answer |> Result.toOption |> Option.bind _.Latest
      PublishedAt = None
      ObservedAt = at
      AnswerSha256 = Some(sha256 answer)
      Answer = Some answer
      Error = None }

/// A GitHub release list whose newest stable release is `latest` (with a newer draft and a newer prerelease).
let private releases (prefix: string) (latest: string) =
    serialize (
        JArr
            [ release $"{prefix}99.0.0" true false
              release $"{prefix}98.0.0" false true
              release $"{prefix}{latest}" false false
              release $"{prefix}0.0.1" false false ]
    )

let private seen system latest (at: DateTimeOffset) = honest (selectedOf releaseSet system) (releases "v" latest) at

let private statusOf system (report: Json) =
    arr "systems" report |> List.find (fun r -> str "system" r = system) |> str "status"

let private assessOf rs observations deviations =
    assess rs observations deviations None lockAt (TimeSpan.FromHours 6.0)

let private assess' observations deviations = assessOf releaseSet observations deviations

let all () : string list =
    let current = assess' [ seen "praxis" "3.23.0" before; seen "forma" "0.11.0" before ] []
    let behind = assess' [ seen "praxis" "3.24.0" before; seen "forma" "0.11.0" before ] []

    let deviation (decision: string) (approver: string) =
        JObj [ "system", JStr "praxis"; "decision", JStr decision; "approvedBy", JStr approver; "selected", JStr "3.23.0"; "latest", JStr "3.24.0" ]

    let approved = assess' [ seen "praxis" "3.24.0" before; seen "forma" "0.11.0" before ] [ deviation "DEC-R7-001" "owner" ]
    let praxis = seen "praxis" "3.23.0" before

    [ if str "verdict" current <> "PASS" then yield sprintf "a set equal to the newest releases (drafts, prereleases skipped) does not PASS: %A" (arr "systems" current |> List.map (str "why"))
      if str "verdict" behind <> "FAIL" || statusOf "praxis" behind <> "DEVIATION_UNAPPROVED" then yield "a version behind the newest release passes without a deviation"
      if str "verdict" approved <> "PASS" || statusOf "praxis" approved <> "DEVIATION_APPROVED" then yield "an owner-approved deviation is not honoured"
      if arr "systems" approved |> List.exists (fun r -> str "system" r = "praxis" && (strOpt "decision" r <> Some "DEC-R7-001" || strOpt "approvedBy" r <> Some "owner")) then
          yield "an approved row does not record its decision and approver"
      for label, d in [ "an empty decision", deviation "" "owner"; "no approver", deviation "DEC-R7-001" " " ] do
          if statusOf "praxis" (assess' [ seen "praxis" "3.24.0" before; seen "forma" "0.11.0" before ] [ d ]) <> "DEVIATION_UNAPPROVED" then
              yield sprintf "a deviation with %s approves a version behind the newest release" label
      let wrongDeviation = assess' [ seen "praxis" "3.25.0" before; seen "forma" "0.11.0" before ] [ deviation "DEC-R7-001" "owner" ]
      if statusOf "praxis" wrongDeviation <> "DEVIATION_UNAPPROVED" then yield "a deviation approved for another newest release is reused"
      if (field "deviations" (assess releaseSet [] [] (Some "ab") lockAt (TimeSpan.FromHours 6.0)) |> Option.bind (strOpt "sha256")) <> Some "ab" then
          yield "the report does not record the deviations file's sha256"
      let after = assess' [ seen "praxis" "3.23.0" (lockAt.AddMinutes 5.0); seen "forma" "0.11.0" before ] []
      if statusOf "praxis" after <> "OBSERVED_AFTER_LOCK" || str "verdict" after <> "FAIL" then yield "a query made after the lock proves the lock (R6's post-lock check)"
      let stale = assess' [ seen "praxis" "3.23.0" (lockAt.AddDays -2.0); seen "forma" "0.11.0" before ] []
      if statusOf "praxis" stale <> "STALE_OBSERVATION" then yield "a query days before the lock counts as immediately before it"
      let missing = assess' [ seen "praxis" "3.23.0" before ] []
      if statusOf "forma" missing <> "NOT_OBSERVED" || str "verdict" missing <> "FAIL" then yield "a system with no query passes"

      // The review's hand-edited observations (obs-hand.json): each must be TAMPERED.
      for label, o in
          [ "a hand-edited latest", { praxis with Latest = Some "3.20.0" }
            "a source other than the documented one", { praxis with Source = "https://example.invalid/anything" }
            "a digest that is not the answer's", { praxis with AnswerSha256 = Some "not-a-digest" }
            "no stored answer", { praxis with Answer = None }
            "an answer edited after its digest", { praxis with Answer = Some(releases "v" "3.20.0") } ] do
          let report = assessOf (set [ "praxis", "3.20.0", "v3.20.0" ]) [ o ] []
          if statusOf "praxis" report <> "TAMPERED" || str "verdict" report <> "FAIL" then
              yield sprintf "%s is not TAMPERED (status %s)" label (statusOf "praxis" report)
      if statusOf "praxis" (assess' [ praxis; praxis; seen "forma" "0.11.0" before ] []) <> "DUPLICATE_OBSERVATION" then
          yield "two observations of one system are accepted"
      let roundTrip = observationOfJson (parseBytes (Text.Encoding.UTF8.GetBytes(serialize (observationToJson praxis))))
      if statusOf "praxis" (assess' [ roundTrip; seen "forma" "0.11.0" before ] []) <> "CURRENT" then
          yield "an observation written and read back no longer re-checks"

      // Per-component tag prefixes: a repository releasing several components.
      let multi = set [ "dokimos", "0.9.0", "dokimos-v0.9.0" ]
      let dokimos = selectedOf multi "dokimos"
      let mixed =
          serialize (
              JArr
                  [ release "v5.0.0" false false
                    release "other-v9.0.0" false false
                    release "dokimos-v1.0.0" true false
                    release "dokimos-v0.10.0-rc.1" false true
                    release "dokimos-v0.9.1" false false
                    release "dokimos-v0.9.0" false false ]
          )
      if dokimos.TagPrefix <> Some "dokimos-v" then yield sprintf "the tag prefix of dokimos-v0.9.0 is %A, not dokimos-v" dokimos.TagPrefix
      if sourceOf dokimos <> Some "https://api.github.com/repos/kemiller2002/dokimos/releases?per_page=100" then yield "a GitHub component is not read from its release list"
      let dokimosReport = assessOf multi [ honest dokimos mixed before ] []
      if arr "systems" dokimosReport |> List.exists (fun r -> strOpt "latest" r <> Some "0.9.1") then
          yield sprintf "the newest dokimos release is not 0.9.1 among other components' tags, drafts and prereleases: %A" (arr "systems" dokimosReport |> List.map (strOpt "latest"))

      // N5: two components of one repository with one tag prefix (limen-fsharp and the limen npm package, both `v`).
      let shared =
          JObj
              [ "components",
                JArr
                    [ JObj [ "systemId", JStr "limen-fsharp"; "version", JStr "0.9.0"; "tag", JStr "v0.9.0"; "repository", JStr "kemiller2002/limen"; "distribution", JObj [ "mechanism", JStr "github-release" ] ]
                      JObj [ "systemId", JStr "limen-npm"; "version", JStr "0.9.0"; "tag", JStr "v0.9.0"; "repository", JStr "kemiller2002/limen"; "distribution", JObj [ "mechanism", JStr "github-release" ] ] ] ]
      let sharedReport = assessOf shared (selectedSystems shared |> List.map (fun s -> honest s (releases "v" "0.9.0") before)) []
      if str "verdict" sharedReport = "PASS" || [ "limen-fsharp"; "limen-npm" ] |> List.exists (fun s -> statusOf s sharedReport <> "AMBIGUOUS_COMPONENT") then
          yield "two components sharing a repository and a tag prefix are not AMBIGUOUS_COMPONENT"
      let distinct = set [ "dokimos", "0.9.0", "dokimos-v0.9.0" ]
      let twoPrefixes =
          JObj [ "components", JArr(arr "components" distinct @ [ JObj [ "systemId", JStr "praxis"; "version", JStr "3.23.0"; "tag", JStr "v3.23.0"; "repository", JStr "kemiller2002/dokimos"; "distribution", JObj [ "mechanism", JStr "github-release" ] ] ]) ]
      if selectedSystems twoPrefixes |> List.exists (fun s -> statusOf s.System (assessOf twoPrefixes [] []) = "AMBIGUOUS_COMPONENT") then
          yield "components of one repository with different tag prefixes are AMBIGUOUS_COMPONENT"
      // Third review (N5 low): the limen npm package beside limen-fsharp's GitHub releases has its own source.
      let mixedSources =
          JObj
              [ "components",
                JArr
                    [ JObj [ "systemId", JStr "limen-fsharp"; "version", JStr "0.9.0"; "tag", JStr "v0.9.0"; "repository", JStr "kemiller2002/limen"; "distribution", JObj [ "mechanism", JStr "github-release" ] ]
                      JObj [ "systemId", JStr "limen-npm"; "version", JStr "0.9.0"; "tag", JStr "v0.9.0"; "repository", JStr "kemiller2002/limen"; "distribution", JObj [ "mechanism", JStr "npm"; "package", JStr "@echelon-foundry/limen" ] ] ] ]
      if selectedSystems mixedSources |> List.exists (fun s -> statusOf s.System (assessOf mixedSources [] []) = "AMBIGUOUS_COMPONENT") then
          yield "a component read from npm is AMBIGUOUS_COMPONENT beside a GitHub-release component of the same repository"
      if strs "limits" current |> List.isEmpty then
          yield "the report does not state its inherent limits"
      // N4 is inherent: the report says the observations are not attested.
      if not ((strOpt "attestation" current |> Option.defaultValue "").StartsWith("none:", StringComparison.Ordinal)) then
          yield "the report does not state that its observations carry no attestation"

      // npm: dist-tags.latest must be the highest stable version.
      let npmSet =
          JObj [ "components", JArr [ JObj [ "systemId", JStr "limen"; "version", JStr "0.9.1"; "distribution", JObj [ "mechanism", JStr "npm"; "package", JStr "@echelon-foundry/limen" ] ] ] ]
      let limen = selectedOf npmSet "limen"
      let npmAnswer latest = serialize (JObj [ "dist-tags", JObj [ "latest", JStr latest ]; "versions", JObj [ "0.9.1", JObj []; "0.9.2", JObj []; "1.0.0-rc.1", JObj [] ] ])
      if statusOf "limen" (assessOf npmSet [ honest limen (npmAnswer "0.9.1") before ] []) <> "NPM_LATEST_NOT_HIGHEST" then
          yield "an npm latest dist-tag below the highest stable version passes"
      if statusOf "limen" (assessOf npmSet [ honest limen (npmAnswer "0.9.2") before ] []) <> "DEVIATION_UNAPPROVED" then
          yield "an npm package behind its latest dist-tag is not a deviation"

      // NuGet: the flat-container index, highest stable.
      let nugetSet =
          JObj [ "components", JArr [ JObj [ "systemId", JStr "aegis"; "version", JStr "1.2.0"; "distribution", JObj [ "mechanism", JStr "nuget"; "package", JStr "EchelonFoundry.Aegis.Core" ] ] ] ]
      let aegis = selectedOf nugetSet "aegis"
      if sourceOf aegis <> Some "https://api.nuget.org/v3-flatcontainer/echelonfoundry.aegis.core/index.json" then yield "a NuGet package is not read from its flat-container index"
      let nuget = serialize (JObj [ "versions", jstrs [ "1.0.0"; "1.2.0"; "1.3.0-beta.1" ] ])
      if statusOf "aegis" (assessOf nugetSet [ honest aegis nuget before ] []) <> "CURRENT" then yield "the highest stable NuGet version is not the newest release"
      if statusOf "aegis" (assessOf nugetSet [ honest aegis (serialize (JObj [ "versions", jstrs [ "1.2.0"; "1.10.0" ] ])) before ] []) <> "DEVIATION_UNAPPROVED" then
          yield "a newer NuGet version is not found"

      if compareVersions "0.11.0" "0.11.0-rc.1" <> Some 1 then yield "a prerelease sorts before its release"
      if compareVersions "1.10.0" "1.9.0" <> Some 1 then yield "versions compare numerically, not as text"
      if compareVersions "1.0.0-rc.10" "1.0.0-rc.9" <> Some 1 then yield "numeric prerelease identifiers compare as text (rc.10 < rc.9)"
      if compareVersions "1.0.0-alpha" "1.0.0-alpha.1" <> Some -1 then yield "a shorter prerelease is not lower"
      if compareVersions "1.0.0-1" "1.0.0-alpha" <> Some -1 then yield "a numeric prerelease identifier does not sort before an alphanumeric one"
      if parseLockedAt "2026-10-29T20:00:00" |> Result.isOk then yield "a lock time without an offset is accepted"
      if parseLockedAt "2026-10-29T20:00:00Z" <> Ok lockAt || parseLockedAt "2026-10-29T22:00:00+02:00" <> Ok lockAt then yield "a lock time with an offset is refused"
      if maxAgeOf 48.0 |> Result.isOk || maxAgeOf 0.0 |> Result.isOk then yield "a maximum observation age outside (0, 24] hours is accepted"
      if maxAgeOf 24.0 <> Ok(TimeSpan.FromHours 24.0) then yield "a 24-hour maximum age is refused" ]
    |> List.map (sprintf "release comparison: %s")
