/// Executes the Rehearsal 4 preflight checks (curation/preflight-checks.json) and returns evidence.
/// Every status comes from an executed check: package state from the compiled package, everything
/// else from git, the .NET SDK, the environment or the GitHub API. Secrets are never read into output.
module Preflight

open System
open System.Diagnostics
open System.Net.Http
open System.Text.RegularExpressions
open Json
open Model

type Outcome = { Status: string; Detail: string }

let private outcome status detail = { Status = status; Detail = detail }

type Context =
    { Compiled: Artifacts.Compiled
      Report: Json
      Source: string
      SourceCommit: string
      Repos: Map<string, string>
      /// The rehearsal kickoff decisions record (kickoff/rehearsal-NNN/decisions.json), when given: its pending
      /// items are owner decisions the run depends on that the compiled curation does not carry.
      KickoffDecisions: Json option }

let private param (k: Json) (name: string) =
    field "params" k |> Option.bind (strOpt name) |> Option.defaultValue ""

let private gitText repo args =
    Git.run repo args |> Result.map (fun b -> Text.Encoding.UTF8.GetString(b))

/// Orders version tags numerically: v3.20.0 > v3.9.1.
let private versionKey (v: string) =
    Regex.Matches(v, @"\d+") |> Seq.map (fun m -> int m.Value) |> List.ofSeq

/// The statuses that count as decided. Anything else, including a missing or unrecognized status, is undecided:
/// the check fails closed.
let decidedStatuses = set [ "decided"; "accepted"; "approved"; "resolved"; "after-ready-preflight" ]

/// The undecided items of a kickoff decisions record: every object with a `status` outside `decidedStatuses`,
/// named by its `id` or by its key path. An item with `coveredBy` is left to the preflight check it names (for
/// example the operator approvals, which PRE-REQ-04 requires), so it is not counted twice.
let pendingKickoffItems (decisions: Json) : string list =
    let isDecided (s: string) = decidedStatuses.Contains(s.Trim().ToLowerInvariant())
    let rec walk (path: string) (j: Json) : string list =
        match j with
        | JObj props ->
            let here =
                match strOpt "status" j, strOpt "coveredBy" j with
                | Some s, None when not (isDecided s) -> [ strOpt "id" j |> Option.defaultValue path ]
                | None, None when (strOpt "id" j).IsSome && (strOpt "subject" j).IsSome && (field "resolution" j).IsNone ->
                    [ strOpt "id" j |> Option.defaultValue path ]
                | _ -> []
            here @ (props |> List.collect (fun (k, v) -> walk (if path = "" then k else path + "." + k) v))
        | JArr items -> items |> List.mapi (fun i v -> walk (sprintf "%s[%d]" path i) v) |> List.concat
        | _ -> []
    walk "" decisions

let private packageState (ctx: Context) (condition: string) =
    match condition with
    | "no-unresolved-decisions" ->
        let s, d = Artifacts.packageState ctx.Compiled condition
        match ctx.KickoffDecisions |> Option.map pendingKickoffItems |> Option.defaultValue [] with
        | [] -> outcome s d
        | pending ->
            let status = if s = "PASS" then "BLOCKED" else s
            outcome status (sprintf "%s; kickoff decisions pending the owner: %s" d (String.concat ", " pending))
    | "compilation-pass" ->
        let v = str "compilationVerdict" ctx.Report
        outcome (if v = "PASS" then "PASS" else "FAIL") ("compilation verdict " + v)
    | "manifests-valid" ->
        let v16 = arr "checks" ctx.Report |> List.tryFind (fun c -> str "id" c = "V16") |> Option.map (str "status")
        outcome (if v16 = Some "PASS" then "PASS" else "FAIL") (sprintf "V16 %s" (Option.defaultValue "missing" v16))
    | c ->
        let s, d = Artifacts.packageState ctx.Compiled c
        outcome s d

let private baselinePinned (ctx: Context) (path: string) =
    let full = IO.Path.Combine(ctx.Source, path)
    if not (IO.File.Exists full) then outcome "FAIL" (path + " does not exist")
    else
        let committed = IO.File.ReadAllBytes full
        let pinned = parseBytes committed |> field "source" |> Option.map (str "commit") |> Option.defaultValue ""

        let read p =
            match Git.showFile ctx.Source ctx.SourceCommit p with
            | Ok b -> Text.utf8 b
            | Error e -> failwithf "cannot read %s: %s" p e

        let rebuilt = Baseline.build read ctx.SourceCommit |> serializePythonStyle |> Text.Encoding.UTF8.GetBytes

        let same = Baseline.content (parseBytes committed) = Baseline.content (parseBytes rebuilt)

        if pinned = ctx.SourceCommit && rebuilt = committed then outcome "PASS" ("pinned at the source commit and byte-identical to the generator's output")
        elif same then outcome "PASS" (sprintf "pinned at %s; no drift: requirements, obligations and document hashes equal the generator's output at %s" pinned ctx.SourceCommit)
        else outcome "FAIL" (sprintf "drift: the baseline pinned at %s differs from the generator's output at the source commit %s (F-003, RDP-001)" pinned ctx.SourceCommit)

let private sdkPinned (ctx: Context) (globalJson: string) =
    let pinned =
        IO.Path.Combine(ctx.Source, globalJson) |> parseFile |> field "sdk" |> Option.map (str "version") |> Option.defaultValue ""

    try
        let psi = ProcessStartInfo("dotnet", "--version", RedirectStandardOutput = true, UseShellExecute = false)
        psi.WorkingDirectory <- ctx.Source
        use p = Process.Start psi
        let installed = p.StandardOutput.ReadToEnd().Trim()
        p.WaitForExit()
        if p.ExitCode = 0 && installed = pinned then outcome "PASS" (sprintf "dotnet %s = global.json %s" installed pinned)
        else outcome "FAIL" (sprintf "dotnet --version gave '%s' (exit %d); global.json pins %s" installed p.ExitCode pinned)
    with ex -> outcome "FAIL" ("dotnet not runnable: " + ex.Message)

/// GitHub token shapes: classic (gh?_), and fine-grained (github_pat_). Only file paths are reported.
let private secretScan (ctx: Context) =
    let pattern = @"(gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{22,})"

    match Git.run ctx.Source [ "grep"; "-l"; "-I"; "-E"; pattern; ctx.SourceCommit; "--" ] with
    | Error "" -> outcome "PASS" (sprintf "no GitHub token pattern in any tracked file at %s" ctx.SourceCommit)
    | Error e -> outcome "FAIL" ("git grep failed: " + e)
    | Ok b ->
        let files = Text.Encoding.UTF8.GetString(b).Split('\n', StringSplitOptions.RemoveEmptyEntries)
        outcome "FAIL" ("token-shaped strings in: " + String.Join(", ", files))

let private latestTag (repo: string) (prefix: string) =
    gitText repo [ "ls-remote"; "--tags"; "origin" ]
    |> Result.map (fun text ->
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        |> Array.choose (fun l ->
            match l.Split('\t') with
            | [| _; r |] when r.StartsWith("refs/tags/" + prefix) && not (r.EndsWith "^{}") -> Some(r.Substring 10)
            | _ -> None)
        |> Array.filter (fun t -> Regex.IsMatch(t, @"^" + Regex.Escape prefix + @"\d+\.\d+\.\d+$"))
        |> Array.sortBy versionKey
        |> Array.tryLast)

let private gitRefContains (ctx: Context) (k: Json) =
    match ctx.Repos.TryFind(param k "repo") with
    | None -> outcome "NOT_RUN" ("no checkout for " + param k "repo")
    | Some repo ->
        let refSpec = param k "ref"

        let tag =
            if refSpec.StartsWith "latest-tag:" then latestTag repo (refSpec.Substring 11)
            else Ok(Some refSpec)

        match tag with
        | Error e -> outcome "FAIL" ("cannot list tags: " + e)
        | Ok None -> outcome "FAIL" "no release tag found"
        | Ok(Some t) ->
            match Git.run repo [ "fetch"; "--quiet"; "--depth"; "1"; "origin"; "tag"; t ] with
            | Error e -> outcome "FAIL" (sprintf "cannot fetch %s: %s" t e)
            | Ok _ ->
                match Git.run repo [ "grep"; "-l"; "-i"; param k "pattern"; t; "--"; param k "path" ] with
                | Ok _ -> outcome "PASS" (sprintf "%s contains '%s' under %s" t (param k "pattern") (param k "path"))
                | Error "" -> outcome "BLOCKED" (sprintf "latest release %s has no '%s' under %s" t (param k "pattern") (param k "path"))
                | Error e -> outcome "FAIL" ("git grep failed: " + e)

let private registrySetContains (ctx: Context) (k: Json) =
    match ctx.Repos.TryFind(param k "repo") with
    | None -> outcome "NOT_RUN" ("no checkout for " + param k "repo")
    | Some repo ->
        match Git.run repo [ "fetch"; "--quiet"; "--depth"; "1"; "origin"; "main" ] with
        | Error e -> outcome "FAIL" ("cannot fetch registry main: " + e)
        | Ok _ ->
            let dir = sprintf "sets/%s/" (param k "set")

            match gitText repo [ "ls-tree"; "--name-only"; "FETCH_HEAD"; dir ] with
            | Error e -> outcome "FAIL" e
            | Ok listing ->
                match listing.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.sortBy versionKey |> Array.tryLast with
                | None -> outcome "FAIL" ("no set under " + dir)
                | Some file ->
                    match Git.showFile repo "FETCH_HEAD" file with
                    | Error e -> outcome "FAIL" e
                    | Ok bytes ->
                        let set = parseBytes bytes
                        let members = arr "components" set |> List.map (str "systemId") |> Set.ofList
                        let wanted = (param k "systems").Split(',') |> List.ofArray
                        let missing = wanted |> List.filter (fun s -> not (members.Contains s))
                        let status = strOpt "status" set |> Option.defaultValue ""

                        if missing.IsEmpty && status = param k "status" then outcome "PASS" (sprintf "%s (%s) contains %s" file status (param k "systems"))
                        else outcome "BLOCKED" (sprintf "newest set %s (status %s) lacks: %s" file status (String.Join(", ", missing)))

let private envPresent (k: Json) =
    let missing =
        (param k "variables").Split(',')
        |> Array.filter (fun v -> String.IsNullOrEmpty(Environment.GetEnvironmentVariable v))

    if missing.Length = 0 then outcome "PASS" "all variables set (values not read)"
    else outcome "BLOCKED" ("not set: " + String.Join(", ", missing))

/// Authenticated read of the test repository. Fails a classic token whose X-OAuth-Scopes grant 'repo' or admin
/// (EXT-DATA-004: no unrestricted token by default); checks the answered repository is the requested one (R2 F6).
let private githubAuthContract (k: Json) =
    let token = Environment.GetEnvironmentVariable(param k "tokenVariable")
    let repo = Environment.GetEnvironmentVariable(param k "repoVariable")

    if String.IsNullOrEmpty token || String.IsNullOrEmpty repo then
        outcome "NOT_RUN" (sprintf "%s and %s are required; nothing was sent" (param k "tokenVariable") (param k "repoVariable"))
    else
        try
            use http = new HttpClient()

            let send (bearer: string) =
                use req = new HttpRequestMessage(HttpMethod.Get, sprintf "https://api.github.com/repos/%s" repo)
                req.Headers.Authorization <- Headers.AuthenticationHeaderValue("Bearer", bearer)
                req.Headers.UserAgent.ParseAdd("indy-init-preflight")
                req.Headers.Accept.ParseAdd("application/vnd.github+json")
                http.Send req

            // Negative control: a known-invalid token must be refused. If it is not, something between this
            // process and GitHub rewrites credentials (an authenticating proxy), and no result here is evidence.
            use control = send "invalid-preflight-control-token"

            if int control.StatusCode <> 401 then
                outcome "NOT_RUN" (sprintf "negative control answered %d instead of 401: credentials are rewritten in this environment; run in the rehearsal repository's CI" (int control.StatusCode))
            else
                use resp = send token
                let code = int resp.StatusCode

                let scopes =
                    match resp.Headers.TryGetValues "X-OAuth-Scopes" with
                    | true, v -> Some(String.Join(",", v))
                    | _ -> None

                let body = (new IO.StreamReader(resp.Content.ReadAsStream())).ReadToEnd()
                let fullName = if code = 200 then parseBytes (Text.Encoding.UTF8.GetBytes body) |> strOpt "full_name" else None
                let broad = scopes |> Option.exists (fun s -> Regex.IsMatch(s, @"(^|[ ,])(repo|admin:[a-z_]+)($|[ ,])"))

                if code <> 200 then outcome "FAIL" (sprintf "GET repository answered %d" code)
                elif fullName |> Option.map (fun n -> not (n.Equals(repo, StringComparison.OrdinalIgnoreCase))) |> Option.defaultValue true then
                    outcome "FAIL" "the answered repository is not the requested one"
                elif broad then outcome "FAIL" "classic token with broad scope (repo/admin); use a fine-grained token limited to the test repository"
                else outcome "PASS" (sprintf "invalid token refused (401); test repository readable (200); classic scopes %s" (if scopes.IsSome then "narrow" else "absent (fine-grained or app token)"))
        with ex -> outcome "FAIL" ("request failed: " + ex.GetType().Name)

let runCheck (ctx: Context) (k: Json) : Outcome =
    try
        match str "type" k with
        | "package-state" -> packageState ctx (param k "condition")
        | "baseline-pinned" -> baselinePinned ctx (param k "baseline")
        | "sdk-pinned" -> sdkPinned ctx (param k "globalJson")
        | "secret-scan" -> secretScan ctx
        | "git-ref-contains" -> gitRefContains ctx k
        | "registry-set-contains" -> registrySetContains ctx k
        | "env-present" -> envPresent k
        | "github-auth-contract" -> githubAuthContract k
        | "manual" -> outcome "NOT_RUN" ("needs a record: " + param k "record")
        | t -> outcome "FAIL" ("unknown check type " + t)
    with ex -> outcome "FAIL" ("check raised: " + ex.Message)

/// READY only when every check passes, except event-only checks (PRE-ORG-01) that may remain NOT_RUN.
let verdict (results: (Json * Outcome) list) =
    let eventOnly = set [ "PRE-ORG-01" ]
    let blocking = results |> List.filter (fun (k, o) -> o.Status <> "PASS" && not (eventOnly.Contains(str "id" k) && o.Status = "NOT_RUN"))
    if blocking.IsEmpty then "READY"
    elif blocking |> List.exists (fun (_, o) -> o.Status = "FAIL") then "FAIL"
    else "BLOCKED"

let evidence (ctx: Context) (executedAt: DateTime) (results: (Json * Outcome) list) =
    JObj
        [ "schema", JStr "indy-init.preflight-evidence/1"
          "executedAt", JStr(executedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"))
          "sourceCommit", JStr ctx.SourceCommit
          "verdict", JStr(verdict results)
          "rule", JStr "READY only when every check is PASS (PRE-ORG-01 may stay NOT_RUN for a rehearsal); any FAIL gives FAIL, otherwise BLOCKED"
          "summary", results |> List.countBy (fun (_, o) -> o.Status) |> List.sortBy fst |> List.map (fun (s, n) -> s, jint n) |> JObj
          "results",
          results
          |> List.map (fun (k, o) ->
              JObj
                  [ "id", JStr(str "id" k)
                    "category", JStr(str "category" k)
                    "type", JStr(str "type" k)
                    "title", JStr(str "title" k)
                    "status", JStr o.Status
                    "detail", JStr o.Detail ])
          |> JArr ]
