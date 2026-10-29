/// Pre-lock release comparison (VER-001, VER-002; Rehearsal 7 remediation R7-REM-010).
///
/// Rehearsal 6 queried each system's release source before locking its stack, but recorded only which release it
/// verified, never the newest release available, so VER-001-O1 and VER-002-O2 failed and could not be proven
/// afterwards. This records, for every system in the selected release set, the newest stable release observed at
/// its documented source before the lock, compares it with the selected version, and requires an owner-approved
/// deviation for anything behind (or ahead of) it.
///
/// Sources: a GitHub repository's release list (`/releases?per_page=100`, drafts and prereleases skipped, only tags
/// with the component's own prefix, e.g. `dokimos-v` for `dokimos-v0.9.0`, the highest stable semver taken), an npm
/// package (`dist-tags.latest`, which must also be the highest stable version published), or a NuGet package (the
/// flat-container version index, highest stable). Never the newest commit (VER-002-O3).
///
/// `observe` is the only I/O. It stores each raw answer with its sha256; `assess` is pure over its inputs and
/// re-checks every observation: the answer's digest, the source URL the selected set implies, and the newest release
/// re-derived from the answer. A mismatch is TAMPERED. Two selected components read from one repository's release
/// list whose tags carry the same prefix (`v` for both) cannot be told apart there, so both are AMBIGUOUS_COMPONENT
/// (not PASS) until their tag prefixes differ; a component read from npm or NuGet (the limen npm package beside
/// limen-fsharp's GitHub releases) has its own source and is not ambiguous.
///
/// Inherent limits (stated in every report's `attestation`): the observations are caller-supplied. The digest proves
/// the record is consistent with the answer it carries, not that the source sent that answer: a forged answer with
/// its own digest re-derives cleanly. `observedAt` is the observer's clock, and the lock record, the deviations'
/// `approvedBy` and the deviations file are written by whoever runs `assess`. Proof needs `observe` run in trusted CI
/// with signed output (or the CI run's own logs); this module does not provide it. npm's newest release is its
/// `dist-tags.latest`, which the publisher sets (a mismatch with the highest stable version is reported, not
/// resolved). GitHub release lists are paged (100 a page, at most 10 pages: the newest 1000 releases); the stored
/// answer is the pages' releases combined into one array.
module ReleaseComparison

open System
open System.Globalization
open System.Net.Http
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open Json

/// One query of a system's release source.
type Observation =
    { System: string
      Source: string
      /// The newest stable release the source reported, normalized (no tag prefix).
      Latest: string option
      PublishedAt: string option
      ObservedAt: DateTimeOffset
      /// sha256 of the raw answer.
      AnswerSha256: string option
      /// The raw answer, kept so the record can be re-derived from it.
      Answer: string option
      Error: string option }

/// One selected system of a resolved release set.
type Selected =
    { System: string
      Version: string
      Repository: string option
      Mechanism: string
      Package: string option
      /// What precedes the version in the component's release tag (`v`, `dokimos-v`), from the set's `tag`.
      TagPrefix: string option
      Role: string }

let normalize (version: string) =
    let v = version.Trim()
    if v.StartsWith("v", StringComparison.OrdinalIgnoreCase) then v.Substring 1 else v

/// The version a release tag names, read with the component's prefix (`dokimos-v0.9.0` -> `0.9.0`). Without a known
/// prefix only plain tags (`1.2.3`, `v1.2.3`) are read. None when the tag is not this component's.
let versionOfTag (prefix: string option) (tag: string) : string option =
    match prefix with
    | Some p when p <> "" -> if tag.StartsWith(p, StringComparison.Ordinal) then Some(tag.Substring p.Length) else None
    | _ -> if Regex.IsMatch(tag, @"^v?\d") then Some(normalize tag) else None

/// The tag prefix of a selected component: everything before the version in its `tag`.
let tagPrefixOf (tag: string option) (version: string) : string option =
    tag
    |> Option.filter (fun t -> t.Length > version.Length && t.EndsWith(version, StringComparison.Ordinal))
    |> Option.map (fun t -> t.Substring(0, t.Length - version.Length))

type private Parsed = { Core: int list; Pre: string list option }

let private parseVersion (v: string) : Parsed option =
    let noBuild = (normalize v).Split('+').[0]

    let core, pre =
        match noBuild.Split('-', 2) with
        | [| core; pre |] -> core, Some pre
        | parts -> parts.[0], None

    let numbers = core.Split('.') |> Array.map (fun part -> Int32.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture))

    if core <> "" && numbers |> Array.forall fst && pre <> Some "" then
        Some { Core = numbers |> Array.map snd |> List.ofArray; Pre = pre |> Option.map (fun p -> p.Split('.') |> List.ofArray) }
    else
        None

/// Semver precedence of prerelease identifier lists: numeric identifiers compare numerically and sort before
/// alphanumeric ones; a shorter list that is a prefix sorts first.
let rec private comparePre (a: string list) (b: string list) =
    match a, b with
    | [], [] -> 0
    | [], _ -> -1
    | _, [] -> 1
    | x :: xs, y :: ys ->
        let c =
            match Int64.TryParse x, Int64.TryParse y with
            | (true, m), (true, n) -> compare m n
            | (true, _), _ -> -1
            | _, (true, _) -> 1
            | _ -> String.CompareOrdinal(x, y)

        if c <> 0 then sign c else comparePre xs ys

/// -1, 0 or 1 by semver precedence when both versions are numeric dotted versions (a prerelease sorts before its
/// release; build metadata is ignored); None otherwise.
let compareVersions (a: string) (b: string) : int option =
    match parseVersion a, parseVersion b with
    | Some x, Some y ->
        let width = max x.Core.Length y.Core.Length
        let pad (xs: int list) = xs @ List.replicate (width - xs.Length) 0

        match compare (pad x.Core) (pad y.Core) with
        | 0 ->
            match x.Pre, y.Pre with
            | None, None -> Some 0
            | Some _, None -> Some -1
            | None, Some _ -> Some 1
            | Some p, Some q -> Some(comparePre p q)
        | c -> Some(sign c)
    | _ -> None

let isStable (version: string) =
    parseVersion version |> Option.exists (fun p -> p.Pre.IsNone)

/// The highest stable version among `versions` (each with its payload).
let private highest (versions: (string * 'a) list) : (string * 'a) option =
    versions
    |> List.filter (fst >> isStable)
    |> List.fold
        (fun best (v, x) ->
            match best with
            | Some(b, _) when compareVersions v b <> Some 1 -> best
            | _ -> Some(v, x))
        None

/// The selected systems of a resolved release set.
let selectedSystems (releaseSet: Json) : Selected list =
    arr "components" releaseSet
    |> List.map (fun c ->
        let distribution = field "distribution" c |> Option.defaultValue (JObj [])
        let version = normalize (str "version" c)

        { System = str "systemId" c
          Version = version
          Repository = strOpt "repository" c
          Mechanism = strOpt "mechanism" distribution |> Option.defaultValue ""
          Package = strOpt "package" distribution
          TagPrefix = tagPrefixOf (strOpt "tag" c) version
          Role = strOpt "role" c |> Option.defaultValue "" })

/// The documented release source of a selected system, as a URL.
let sourceOf (s: Selected) : string option =
    match s.Mechanism, s.Package, s.Repository with
    | "npm", Some pkg, _ -> Some(sprintf "https://registry.npmjs.org/%s" (Uri.EscapeDataString(pkg).Replace("%40", "@")))
    | "nuget", Some pkg, _ -> Some(sprintf "https://api.nuget.org/v3-flatcontainer/%s/index.json" (pkg.ToLowerInvariant()))
    | _, _, Some repo -> Some(sprintf "https://api.github.com/repos/%s/releases?per_page=100" repo)
    | _ -> None

/// What an answer says: the release the source calls newest, the highest stable version it lists, and when the
/// newest was published.
type Derived =
    { Latest: string option
      Highest: string option
      PublishedAt: string option }

/// Re-derives the newest release from a raw answer of `s`'s source. Pure.
let derive (s: Selected) (answer: string) : Result<Derived, string> =
    try
        let doc = parseBytes (Encoding.UTF8.GetBytes answer)

        match s.Mechanism, s.Package, s.Repository with
        | "npm", Some _, _ ->
            let latest = field "dist-tags" doc |> Option.bind (strOpt "latest")

            let versions =
                match field "versions" doc with
                | Some(JObj props) -> props |> List.map (fun (v, _) -> v, ())
                | _ -> []

            Ok
                { Latest = latest
                  Highest = highest versions |> Option.map fst
                  PublishedAt = latest |> Option.bind (fun v -> field "time" doc |> Option.bind (strOpt v)) }
        | "nuget", Some _, _ ->
            let top = highest (strs "versions" doc |> List.map (fun v -> v, ())) |> Option.map fst
            Ok { Latest = top; Highest = top; PublishedAt = None }
        | _, _, Some _ ->
            match doc with
            | JArr releases ->
                let candidates =
                    releases
                    |> List.filter (fun r -> not (boolOr "draft" false r) && not (boolOr "prerelease" false r))
                    |> List.choose (fun r -> strOpt "tag_name" r |> Option.bind (versionOfTag s.TagPrefix) |> Option.map (fun v -> v, strOpt "published_at" r))

                let top = highest candidates
                Ok { Latest = top |> Option.map fst; Highest = top |> Option.map fst; PublishedAt = top |> Option.bind snd }
            | _ -> Error "the answer is not a release list"
        | _ -> Error "no documented release source"
    with ex ->
        Error(sprintf "the answer is not readable JSON (%s)" ex.Message)

let sha256 (text: string) =
    SHA256.HashData(Encoding.UTF8.GetBytes text) |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

/// Queries one system's documented release source now and keeps the raw answer.
let observe (http: HttpClient) (now: unit -> DateTimeOffset) (s: Selected) : Observation =
    let fetch (url: string) =
        try
            use request = new HttpRequestMessage(HttpMethod.Get, url)
            request.Headers.UserAgent.ParseAdd "indy-init-requirements-compiler"

            match Environment.GetEnvironmentVariable "GITHUB_TOKEN" with
            | token when not (String.IsNullOrEmpty token) && url.StartsWith("https://api.github.com/", StringComparison.Ordinal) ->
                request.Headers.Authorization <- Headers.AuthenticationHeaderValue("Bearer", token)
            | _ -> ()

            use response = http.Send request
            let body = response.Content.ReadAsStringAsync().Result
            if response.IsSuccessStatusCode then Ok body else Error(sprintf "HTTP %d" (int response.StatusCode))
        with ex -> Error ex.Message

    // A GitHub release list, page by page until a short page (at most 10), combined into one array.
    let fetchAll (url: string) =
        if not (url.StartsWith("https://api.github.com/", StringComparison.Ordinal)) then
            fetch url
        else
            let rec pages page (acc: Json list) =
                match fetch (sprintf "%s&page=%d" url page) with
                | Error e -> Error e
                | Ok body ->
                    match (try parseBytes (Encoding.UTF8.GetBytes body) with _ -> JNull) with
                    | JArr items when items.Length = 100 && page < 10 -> pages (page + 1) (acc @ items)
                    | JArr items -> Ok(serialize (JArr(acc @ items)))
                    | _ -> Ok body

            pages 1 []

    let at = now ()
    let failed source e = { System = s.System; Source = source; Latest = None; PublishedAt = None; ObservedAt = at; AnswerSha256 = None; Answer = None; Error = Some e }

    match sourceOf s with
    | None -> failed "" "no documented release source"
    | Some source ->
        match fetchAll source with
        | Error e -> failed source e
        | Ok body ->
            let derived = derive s body

            { System = s.System
              Source = source
              Latest = derived |> Result.toOption |> Option.bind _.Latest
              PublishedAt = derived |> Result.toOption |> Option.bind _.PublishedAt
              ObservedAt = at
              AnswerSha256 = Some(sha256 body)
              Answer = Some body
              Error =
                match derived with
                | Error e -> Some e
                | Ok { Latest = None } -> Some "the source named no stable release"
                | Ok _ -> None }

let observationToJson (o: Observation) =
    JObj
        [ "system", JStr o.System
          "source", JStr o.Source
          "latest", jopt JStr o.Latest
          "publishedAt", jopt JStr o.PublishedAt
          "observedAt", JStr(o.ObservedAt.ToString("o", CultureInfo.InvariantCulture))
          "answerSha256", jopt JStr o.AnswerSha256
          "error", jopt JStr o.Error
          "answer", jopt JStr o.Answer ]

let observationOfJson (j: Json) : Observation =
    { System = str "system" j
      Source = strOpt "source" j |> Option.defaultValue ""
      Latest = strOpt "latest" j
      PublishedAt = strOpt "publishedAt" j
      ObservedAt = DateTimeOffset.Parse(str "observedAt" j, CultureInfo.InvariantCulture)
      AnswerSha256 = strOpt "answerSha256" j
      Answer = strOpt "answer" j
      Error = strOpt "error" j }

/// A lock time with an explicit UTC offset (`Z` or `+hh:mm`): a bare local time is ambiguous and refused.
let parseLockedAt (text: string) : Result<DateTimeOffset, string> =
    let t = text.Trim()

    if not (Regex.IsMatch(t, @"(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase)) then
        Error(sprintf "the lock time '%s' has no explicit UTC offset" text)
    else
        match DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, at -> Ok at
        | _ -> Error(sprintf "the lock time '%s' is not an ISO-8601 time" text)

/// The longest an observation may precede the lock: more than 0 and at most 24 hours.
let maxAgeOf (hours: float) : Result<TimeSpan, string> =
    if Double.IsNaN hours || hours <= 0.0 || hours > 24.0 then Error(sprintf "--max-age-hours %g is not in (0, 24]" hours)
    else Ok(TimeSpan.FromHours hours)

/// The comparison of the selected set with the observations, made against the lock time. Deviations are the
/// owner's approval records (`{system, decision, approvedBy, selected, latest}`); an agent never writes them
/// (RDP-003). A deviation without a decision id or an approver approves nothing.
let assess
    (releaseSet: Json)
    (observations: Observation list)
    (deviations: Json list)
    (deviationsSha256: string option)
    (lockedAt: DateTimeOffset)
    (maxAge: TimeSpan)
    : Json =
    let nonEmpty key d = strOpt key d |> Option.map (fun s -> s.Trim()) |> Option.filter ((<>) "")

    let approved (system: string) (selected: string) (latest: string) =
        deviations
        |> List.tryFind (fun d ->
            str "system" d = system
            && strOpt "selected" d |> Option.map normalize = Some selected
            && strOpt "latest" d |> Option.map normalize = Some latest
            && (nonEmpty "decision" d).IsSome
            && (nonEmpty "approvedBy" d).IsSome)
        |> Option.map (fun d -> (nonEmpty "decision" d).Value, (nonEmpty "approvedBy" d).Value)

    let compareWith (s: Selected) (latest: string) =
        match compareVersions s.Version latest with
        | Some 0 -> "CURRENT", "the selected version is the newest stable release", None
        | Some c ->
            let relation = if c < 0 then "behind" else "ahead of"

            match approved s.System s.Version latest with
            | Some(decision, by) -> "DEVIATION_APPROVED", sprintf "%s the newest release %s under %s (approved by %s)" relation latest decision by, Some(decision, by)
            | None -> "DEVIATION_UNAPPROVED", sprintf "%s the newest release %s, with no owner-approved deviation (decision and approver)" relation latest, None
        | None -> "INCOMPARABLE", sprintf "cannot compare %s with %s" s.Version latest, None

    /// Status, reason, the re-derived newest release and the approving deviation.
    let check (s: Selected) (o: Observation) =
        let tampered why = "TAMPERED", why, None, None

        match o with
        | o when o.ObservedAt > lockedAt -> "OBSERVED_AFTER_LOCK", "queried after the baseline lock; it cannot prove the lock's choice", None, None
        | o when lockedAt - o.ObservedAt > maxAge ->
            "STALE_OBSERVATION", sprintf "queried %s before the lock (more than %s)" (string (lockedAt - o.ObservedAt)) (string maxAge), None, None
        | { Error = Some e } -> "NOT_OBSERVED", e, None, None
        | o when Some o.Source <> sourceOf s ->
            tampered (sprintf "recorded source %s is not the documented source %s" o.Source (sourceOf s |> Option.defaultValue "(none)"))
        | { Answer = None } -> tampered "no raw answer is stored, so the observation cannot be re-checked"
        | { Answer = Some answer } when Some(sha256 answer) <> o.AnswerSha256 ->
            tampered (sprintf "the stored answer's sha256 is %s, not the recorded %s" (sha256 answer) (o.AnswerSha256 |> Option.defaultValue "(none)"))
        | { Answer = Some answer } ->
            match derive s answer with
            | Error e -> tampered (sprintf "the stored answer cannot be read: %s" e)
            | Ok d when d.Latest <> o.Latest ->
                tampered (sprintf "recorded newest release %s, but the stored answer says %s" (o.Latest |> Option.defaultValue "(none)") (d.Latest |> Option.defaultValue "(none)"))
            | Ok { Latest = None } -> "NOT_OBSERVED", "the source named no stable release", None, None
            | Ok { Latest = Some latest; Highest = top } when s.Mechanism = "npm" && top <> Some latest ->
                "NPM_LATEST_NOT_HIGHEST", sprintf "dist-tags.latest is %s but the highest stable version published is %s" latest (top |> Option.defaultValue "(none)"), Some latest, None
            | Ok { Latest = Some latest } ->
                let status, why, deviation = compareWith s latest
                status, why, Some latest, deviation

    let selected = selectedSystems releaseSet

    // Components read from one release list (the same GitHub repository) with one tag prefix (no prefix, `` and `v`
    // read the same tags). A component read from npm or NuGet has its own source and is not confused with them.
    let prefixKey (s: Selected) =
        match s.TagPrefix with
        | Some p when p <> "" && p <> "v" -> p
        | _ -> "v"

    let sharing (s: Selected) =
        selected
        |> List.filter (fun o -> o.System <> s.System && (sourceOf s).IsSome && sourceOf o = sourceOf s && prefixKey o = prefixKey s)
        |> List.map _.System

    let rows =
        selected
        |> List.map (fun s ->
            let mine = observations |> List.filter (fun o -> o.System = s.System)

            let status, why, latest, deviation =
                match mine, sharing s with
                | _, (_ :: _ as others) ->
                    "AMBIGUOUS_COMPONENT",
                    sprintf "shares repository %s and tag prefix `%s` with %s: its releases cannot be told apart from theirs" s.Repository.Value (prefixKey s) (String.Join(", ", others)),
                    None,
                    None
                | [], _ -> "NOT_OBSERVED", "no pre-lock query of the release source", None, None
                | [ o ], _ -> check s o
                | many, _ -> "DUPLICATE_OBSERVATION", sprintf "%d observations of one system: which one the lock relied on is ambiguous" many.Length, None, None

            let o = match mine with [ o ] -> Some o | _ -> None

            JObj
                [ "system", JStr s.System
                  "role", JStr s.Role
                  "selected", JStr s.Version
                  "tagPrefix", jopt JStr s.TagPrefix
                  "latest", jopt JStr latest
                  "source", o |> Option.map (fun o -> JStr o.Source) |> Option.defaultValue JNull
                  "observedAt", o |> Option.map (fun o -> JStr(o.ObservedAt.ToString("o", CultureInfo.InvariantCulture))) |> Option.defaultValue JNull
                  "answerSha256", o |> Option.bind _.AnswerSha256 |> jopt JStr
                  "decision", deviation |> Option.map fst |> jopt JStr
                  "approvedBy", deviation |> Option.map snd |> jopt JStr
                  "status", JStr status
                  "why", JStr why ])

    let passing = set [ "CURRENT"; "DEVIATION_APPROVED" ]
    let failing = rows |> List.filter (fun r -> not (passing.Contains(str "status" r)))

    JObj
        [ "schema", JStr "indy-init.release-comparison/4"
          "lockedAt", JStr(lockedAt.ToString("o", CultureInfo.InvariantCulture))
          "maxObservationAge", JStr(string maxAge)
          "limits",
          jstrs
              [ "observations, observedAt, the lock record, approvedBy and the deviations are caller-supplied; a forged answer with its own digest re-derives cleanly"
                "npm's newest release is dist-tags.latest as the publisher set it"
                "GitHub release lists are read to at most 1000 releases (10 pages of 100)" ]
          "attestation",
          JStr "none: observations, observedAt, the lock record, approvedBy and the deviations are caller-supplied; the digest shows the record matches the answer it carries, not that the source sent it. Run observe in trusted CI with signed output for proof"
          "deviations", JObj [ "count", jint deviations.Length; "sha256", jopt JStr deviationsSha256 ]
          "verdict", JStr(if failing.IsEmpty && not rows.IsEmpty then "PASS" else "FAIL")
          "summary",
          JObj(
              rows
              |> List.countBy (str "status")
              |> List.sortBy fst
              |> List.map (fun (status, n) -> status, jint n)
          )
          "systems", JArr rows ]
