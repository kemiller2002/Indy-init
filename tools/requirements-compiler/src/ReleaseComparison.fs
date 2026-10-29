/// Pre-lock release comparison (VER-001, VER-002; Rehearsal 7 remediation R7-REM-010).
///
/// Rehearsal 6 queried each system's release source before locking its stack, but recorded only which release it
/// verified, never the newest release available, so VER-001-O1 and VER-002-O2 failed and could not be proven
/// afterwards. This records, for every system in the selected release set, the newest stable release observed at
/// its documented source before the lock, compares it with the selected version, and requires an owner-approved
/// deviation for anything behind (or ahead of) it.
///
/// The comparison is pure; `observe` is the only I/O (GitHub releases/latest, the npm registry), and its raw answers
/// are digested so the record can be re-checked.
module ReleaseComparison

open System
open System.Globalization
open System.Net.Http
open System.Security.Cryptography
open System.Text
open Json

/// One query of a system's release source.
type Observation =
    { System: string
      Source: string
      /// The newest stable release the source reported, normalized (no leading `v`).
      Latest: string option
      PublishedAt: string option
      ObservedAt: DateTimeOffset
      /// sha256 of the raw answer, so the record can be re-checked against it.
      AnswerSha256: string option
      Error: string option }

let normalize (version: string) =
    let v = version.Trim()
    if v.StartsWith("v", StringComparison.OrdinalIgnoreCase) then v.Substring 1 else v

/// -1, 0 or 1 when both versions are numeric dotted versions (a prerelease suffix sorts first); None otherwise.
let compareVersions (a: string) (b: string) : int option =
    let parse (v: string) =
        let core, pre =
            match (normalize v).Split('-', 2) with
            | [| core; pre |] -> core, Some pre
            | parts -> parts.[0], None

        let numbers = core.Split('.') |> Array.map (fun part -> Int32.TryParse part)

        if numbers |> Array.forall fst then Some(numbers |> Array.map snd |> List.ofArray, pre) else None

    match parse a, parse b with
    | Some(x, px), Some(y, py) ->
        let width = max x.Length y.Length
        let pad (xs: int list) = xs @ List.replicate (width - xs.Length) 0

        match compare (pad x) (pad y) with
        | 0 ->
            match px, py with
            | None, None -> Some 0
            | Some _, None -> Some -1
            | None, Some _ -> Some 1
            | Some p, Some q -> Some(sign (String.CompareOrdinal(p, q)))
        | c -> Some(sign c)
    | _ -> None

/// The selected systems of a resolved release set: id, version, repository and distribution.
let selectedSystems (releaseSet: Json) =
    arr "components" releaseSet
    |> List.map (fun c ->
        let distribution = field "distribution" c |> Option.defaultValue (JObj [])

        {| System = str "systemId" c
           Version = normalize (str "version" c)
           Repository = strOpt "repository" c
           Mechanism = strOpt "mechanism" distribution |> Option.defaultValue ""
           Package = strOpt "package" distribution
           Role = strOpt "role" c |> Option.defaultValue "" |})

let private sha256 (text: string) =
    SHA256.HashData(Encoding.UTF8.GetBytes text) |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

/// Queries one system's documented release source: an npm package's `latest` dist-tag, otherwise the GitHub
/// repository's latest (non-prerelease, non-draft) release. Never the newest commit (VER-002-O3).
let observe (http: HttpClient) (now: unit -> DateTimeOffset) (system: string) (repository: string option) (mechanism: string) (package: string option) =
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

    let source, read =
        match mechanism, package, repository with
        | "npm", Some pkg, _ ->
            sprintf "https://registry.npmjs.org/%s" (Uri.EscapeDataString(pkg).Replace("%40", "@")),
            fun (doc: Json) ->
                field "dist-tags" doc |> Option.bind (strOpt "latest"),
                field "dist-tags" doc
                |> Option.bind (strOpt "latest")
                |> Option.bind (fun v -> field "time" doc |> Option.bind (strOpt v))
        | _, _, Some repo ->
            sprintf "https://api.github.com/repos/%s/releases/latest" repo,
            fun (doc: Json) -> strOpt "tag_name" doc, strOpt "published_at" doc
        | _ -> "", fun _ -> None, None

    let at = now ()

    if source = "" then
        { System = system; Source = ""; Latest = None; PublishedAt = None; ObservedAt = at; AnswerSha256 = None; Error = Some "no documented release source" }
    else
        match fetch source with
        | Ok body ->
            let latest, published = read (parseBytes (Encoding.UTF8.GetBytes body))

            { System = system
              Source = source
              Latest = latest |> Option.map normalize
              PublishedAt = published
              ObservedAt = at
              AnswerSha256 = Some(sha256 body)
              Error = if latest.IsNone then Some "the source named no release" else None }
        | Error e -> { System = system; Source = source; Latest = None; PublishedAt = None; ObservedAt = at; AnswerSha256 = None; Error = Some e }

let observationToJson (o: Observation) =
    JObj
        [ "system", JStr o.System
          "source", JStr o.Source
          "latest", jopt JStr o.Latest
          "publishedAt", jopt JStr o.PublishedAt
          "observedAt", JStr(o.ObservedAt.ToString("o", CultureInfo.InvariantCulture))
          "answerSha256", jopt JStr o.AnswerSha256
          "error", jopt JStr o.Error ]

let observationOfJson (j: Json) =
    { System = str "system" j
      Source = str "source" j
      Latest = strOpt "latest" j
      PublishedAt = strOpt "publishedAt" j
      ObservedAt = DateTimeOffset.Parse(str "observedAt" j, CultureInfo.InvariantCulture)
      AnswerSha256 = strOpt "answerSha256" j
      Error = strOpt "error" j }

/// The comparison of the selected set with the observations, made against the lock time. Deviations are the
/// owner's approval records (`{system, decision, selected, latest}`); an agent never writes them (RDP-003).
let assess (releaseSet: Json) (observations: Observation list) (deviations: Json list) (lockedAt: DateTimeOffset) (maxAge: TimeSpan) : Json =
    let approved (system: string) (selected: string) (latest: string) =
        deviations
        |> List.tryFind (fun d ->
            str "system" d = system
            && strOpt "selected" d |> Option.map normalize = Some selected
            && strOpt "latest" d |> Option.map normalize = Some latest)
        |> Option.map (str "decision")

    let rows =
        selectedSystems releaseSet
        |> List.map (fun s ->
            let o = observations |> List.tryFind (fun o -> o.System = s.System)

            let status, why =
                match o with
                | None -> "NOT_OBSERVED", "no pre-lock query of the release source"
                | Some o when o.ObservedAt > lockedAt -> "OBSERVED_AFTER_LOCK", "queried after the baseline lock; it cannot prove the lock's choice"
                | Some o when lockedAt - o.ObservedAt > maxAge -> "STALE_OBSERVATION", sprintf "queried %s before the lock (more than %s)" (string (lockedAt - o.ObservedAt)) (string maxAge)
                | Some { Error = Some e } -> "NOT_OBSERVED", e
                | Some { Latest = Some latest } ->
                    match compareVersions s.Version latest with
                    | Some 0 -> "CURRENT", "the selected version is the newest stable release"
                    | Some c ->
                        let relation = if c < 0 then "behind" else "ahead of"

                        match approved s.System s.Version latest with
                        | Some decision -> "DEVIATION_APPROVED", sprintf "%s the newest release %s under %s" relation latest decision
                        | None -> "DEVIATION_UNAPPROVED", sprintf "%s the newest release %s, with no owner-approved deviation" relation latest
                    | None -> "INCOMPARABLE", sprintf "cannot compare %s with %s" s.Version latest
                | Some _ -> "NOT_OBSERVED", "the source named no release"

            JObj
                [ "system", JStr s.System
                  "role", JStr s.Role
                  "selected", JStr s.Version
                  "latest", o |> Option.bind _.Latest |> jopt JStr
                  "source", o |> Option.map (fun o -> JStr o.Source) |> Option.defaultValue JNull
                  "observedAt", o |> Option.map (fun o -> JStr(o.ObservedAt.ToString("o", CultureInfo.InvariantCulture))) |> Option.defaultValue JNull
                  "answerSha256", o |> Option.bind _.AnswerSha256 |> jopt JStr
                  "status", JStr status
                  "why", JStr why ])

    let passing = set [ "CURRENT"; "DEVIATION_APPROVED" ]
    let failing = rows |> List.filter (fun r -> not (passing.Contains(str "status" r)))

    JObj
        [ "schema", JStr "indy-init.release-comparison/1"
          "lockedAt", JStr(lockedAt.ToString("o", CultureInfo.InvariantCulture))
          "maxObservationAge", JStr(string maxAge)
          "verdict", JStr(if failing.IsEmpty then "PASS" else "FAIL")
          "summary",
          JObj(
              rows
              |> List.countBy (str "status")
              |> List.sortBy fst
              |> List.map (fun (status, n) -> status, jint n)
          )
          "systems", JArr rows ]
