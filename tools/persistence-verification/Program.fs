/// T05 persistence verification: the approved GitHub publication contract (OD-INDY-9) exercised against the isolated
/// test repository with the CAUSA_GITHUB_TOKEN secret. Requests are built and answers classified by the approved
/// Causa adapter (indy-init-rehearsal-002 src/engine/GitHub.fs, compiled from a pinned checkout); this file sends
/// them and checks what GitHub did. The credential comes from the environment, travels only in the Authorization
/// header, and is screened out of everything this program writes.
///
///   persistence-verification [--evidence FILE]   exit 0 PASS, 1 FAIL, 3 BLOCKED, 2 usage
///   persistence-verification scan FILE...        exit 0 when no file contains the credential, 1 otherwise
module PersistenceVerification.Program

open System
open System.IO
open System.Net.Http
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open Causa.Engine

type Reply = { Status: int; Body: string; Bytes: byte[]; Scopes: string option }

type Check = { Id: string; Title: string; Status: string; Detail: string }

/// The run so far: recorded checks, facts later checks build on (commit ids, paths), and whether a check stopped it.
type State = { Checks: Check list; Facts: Map<string, string>; Halted: bool }

type Outcome = Result<string * (string * string) list, string * string>

let private productionRepositories = set [ "kemiller2002/causa-data" ]
let private production = Target.create "kemiller2002" "causa-data" "main" "fixtures"
// Assembled at run time so the source holds no token-shaped literal (preflight PRE-SEC-01 scans for github_pat_ + 22 characters).
let private negativeControlToken = "github_pat_" + "invalid_t05_negative_control"
let private jsonAccept = "application/vnd.github+json"
let private rawAccept = "application/vnd.github.raw+json"

// ---- transport ----------------------------------------------------------------------------------------------------

/// One exchange with api.github.com. A fresh read (a ref, the repository) goes out under a unique URL with no-cache,
/// because GitHub answers with max-age=60 (DC-005). Nothing here logs a request or a header.
let private exchange (http: HttpClient) (bearer: string) (accept: string) (request: ApiRequest) : Reply =
    let path =
        if request.Fresh then request.Path + (if request.Path.Contains '?' then "&" else "?") + "t05fresh=" + Guid.NewGuid().ToString "N"
        else request.Path

    use message = new HttpRequestMessage(HttpMethod request.Method, GitHub.apiBase + path)
    message.Headers.Authorization <- Headers.AuthenticationHeaderValue("Bearer", bearer)
    message.Headers.UserAgent.ParseAdd "indy-init-t05-verification"
    message.Headers.Accept.ParseAdd accept
    message.Headers.Add("X-GitHub-Api-Version", GitHub.apiVersion)
    if request.Fresh then message.Headers.CacheControl <- Headers.CacheControlHeaderValue(NoCache = true)
    request.Body |> Option.iter (fun b -> message.Content <- new StringContent(b, Encoding.UTF8, "application/json"))
    use response = http.Send message
    use buffer = new MemoryStream()
    response.Content.ReadAsStream().CopyTo buffer
    let bytes = buffer.ToArray()

    { Status = int response.StatusCode
      Body = Encoding.UTF8.GetString bytes
      Bytes = bytes
      Scopes =
        match response.Headers.TryGetValues "X-OAuth-Scopes" with
        | true, v -> Some(String.Join(",", v))
        | _ -> None }

let private contentsAt (t: Target) (path: string) (commit: string) : ApiRequest =
    let segments = path.Split('/') |> Array.map Uri.EscapeDataString |> String.concat "/"
    { Method = "GET"
      Path = sprintf "/repos/%s/%s/contents/%s?ref=%s" t.Owner t.Repo segments commit
      Body = None
      Fresh = false }

/// Cleanup. The adapter publishes additions only, so the removal is the same guarded mutation with deletions.
let private deleteOnBranch (t: Target) (expectedHead: string) (headline: string) (paths: string list) : ApiRequest =
    use stream = new MemoryStream()

    (use w = new Utf8JsonWriter(stream)
     w.WriteStartObject()
     w.WriteString("query", "mutation($input: CreateCommitOnBranchInput!) { createCommitOnBranch(input: $input) { commit { oid } } }")
     w.WriteStartObject "variables"
     w.WriteStartObject "input"
     w.WriteStartObject "branch"
     w.WriteString("repositoryNameWithOwner", t.Owner + "/" + t.Repo)
     w.WriteString("branchName", t.Branch)
     w.WriteEndObject()
     w.WriteString("expectedHeadOid", expectedHead)
     w.WriteStartObject "message"
     w.WriteString("headline", headline)
     w.WriteEndObject()
     w.WriteStartObject "fileChanges"
     w.WriteStartArray "deletions"
     paths |> List.iter (fun p -> w.WriteStartObject(); w.WriteString("path", p); w.WriteEndObject())
     w.WriteEndArray()
     w.WriteEndObject()
     w.WriteEndObject()
     w.WriteEndObject()
     w.WriteEndObject())

    { Method = "POST"; Path = "/graphql"; Body = Some(Encoding.UTF8.GetString(stream.ToArray())); Fresh = false }

// ---- reading answers the adapter does not interpret ---------------------------------------------------------------

let private read (body: string) (f: JsonElement -> 'T option) : 'T option =
    try
        use doc = JsonDocument.Parse body
        f doc.RootElement
    with _ -> None

let private child (name: string) (e: JsonElement) =
    if e.ValueKind = JsonValueKind.Object then
        match e.TryGetProperty name with
        | true, v -> Some v
        | _ -> None
    else None

let private text (e: JsonElement) = if e.ValueKind = JsonValueKind.String then Option.ofObj (e.GetString()) else None

let private parents body =
    read body (fun e -> child "parents" e |> Option.map (fun p -> [ for x in p.EnumerateArray() -> child "sha" x |> Option.bind text |> Option.defaultValue "" ]))
    |> Option.defaultValue []

let private graphqlErrorType body =
    read body (fun e ->
        child "errors" e
        |> Option.bind (fun errs -> if errs.ValueKind = JsonValueKind.Array && errs.GetArrayLength() > 0 then child "type" errs[0] |> Option.bind text else None))
    |> Option.defaultValue "(none)"

let private published (r: Reply) = GitHub.classify (int64 r.Status) r.Body |> Result.bind GitHub.parseCommitOnBranch

let private sha256 (bytes: byte[]) = Convert.ToHexStringLower(SHA256.HashData bytes)

/// Deterministic test content: the same run id always gives the same bytes. It is not investigation data.
let probe (runId: string) (role: string) =
    Encoding.UTF8.GetBytes(
        sprintf "{\"schema\":\"indy-init.t05-probe/1\",\"run\":\"%s\",\"role\":\"%s\",\"note\":\"T05 persistence verification test content; not investigation data\"}\n" runId role
    )

// ---- check sequencing ---------------------------------------------------------------------------------------------

let private record id title status detail (s: State) =
    { s with Checks = s.Checks @ [ { Id = id; Title = title; Status = status; Detail = detail } ] }

/// Run a check unless an earlier one stopped the run. An exception reports its type only: its message could quote
/// a request, and nothing from a request leaves this process.
let private step (id: string) (title: string) (run: Map<string, string> -> Outcome) (s: State) : State =
    if s.Halted then record id title "NOT_RUN" "an earlier check did not pass" s
    else
        match (try run s.Facts with ex -> Error("FAIL", "request failed: " + ex.GetType().Name)) with
        | Ok(detail, facts) -> { record id title "PASS" detail s with Facts = facts |> List.fold (fun m (k, v) -> Map.add k v m) s.Facts }
        | Error(status, detail) -> { record id title status detail s with Halted = true }

/// Run a check whenever `due` holds, even after a failure (cleanup and the production comparison must still happen).
let private always (id: string) (title: string) (due: State -> bool) (run: Map<string, string> -> Outcome) (s: State) : State =
    if not (due s) then record id title "NOT_RUN" "nothing to do: no write was attempted" s
    else
        match (try run s.Facts with ex -> Error("FAIL", "request failed: " + ex.GetType().Name)) with
        | Ok(detail, facts) -> { record id title "PASS" detail s with Facts = facts |> List.fold (fun m (k, v) -> Map.add k v m) s.Facts }
        | Error(status, detail) -> { record id title status detail s with Halted = true }

/// A recorded observation that does not gate the verdict.
let private note (id: string) (title: string) (observe: Map<string, string> -> string * string) (s: State) =
    if s.Halted then s
    else
        let status, detail = observe s.Facts
        record id title status detail s

let private attempted id (s: State) = s.Checks |> List.exists (fun c -> c.Id = id && c.Status <> "NOT_RUN")

// ---- the verification ---------------------------------------------------------------------------------------------

let verify (http: HttpClient) (token: string) (repository: string) (runId: string) : State =
    let api = exchange http token jsonAccept
    let raw = exchange http token rawAccept
    let owner, name =
        match repository.Split('/') with
        | [| o; n |] -> o, n
        | _ -> "", ""

    let targetOn branch = Target.create owner name branch runId
    let target (f: Map<string, string>) =
        match targetOn f.["branch"] with
        | Ok t -> t
        | Error ps -> failwith (String.Join(" ", ps))

    let probePath (t: Target) role = Target.rootLabel t + "/t05-" + role + ".json"

    let headOf (t: Target) =
        let r = api (GitHub.getRef t)
        if r.Status = 200 then GitHub.parseRef r.Body |> Result.toOption else None

    let treeOf (t: Target) commit = GitHub.parseCommitTree (api (GitHub.getCommit t commit)).Body |> Result.toOption

    let productionHead () =
        match production with
        | Ok p -> headOf p
        | Error _ -> None

    { Checks = []; Facts = Map.empty; Halted = false }
    |> step "T05-01" "Credential and test repository supplied to the authorized workflow" (fun _ ->
        if String.IsNullOrEmpty token then Error("BLOCKED", "CAUSA_GITHUB_TOKEN is not available to this process; nothing was sent (no anonymous fallback)")
        elif owner = "" then Error("BLOCKED", "CAUSA_TEST_REPO is not set to OWNER/REPOSITORY")
        elif productionRepositories.Contains(repository.ToLowerInvariant()) then Error("BLOCKED", repository + " is a production repository; test writes are refused")
        else
            match targetOn "main" with
            | Error ps -> Error("BLOCKED", "test repository or run id invalid: " + String.Join(" ", ps))
            | Ok _ -> Ok(sprintf "credential present (value never read into output); test repository %s; run %s" repository runId, []))
    |> step "T05-02" "GitHub refuses an invalid credential (no rewriting proxy, no anonymous fallback)" (fun _ ->
        let control = exchange http negativeControlToken jsonAccept { Method = "GET"; Path = sprintf "/repos/%s/%s" owner name; Body = None; Fresh = true }
        if control.Status = 401 then Ok("invalid token answered 401", [])
        else Error("BLOCKED", sprintf "invalid token answered %d instead of 401: credentials are rewritten here, so no result would be evidence" control.Status))
    |> step "T05-03" "Authentication: the credential reads the test repository and is not a broad classic token" (fun _ ->
        let probeTarget = targetOn "main" |> Result.toOption |> Option.get
        let r = api (GitHub.getRepository probeTarget)
        match r.Status with
        | 200 ->
            let branch = read r.Body (fun e -> child "default_branch" e |> Option.bind text)
            match GitHub.parseRepository r.Body, branch with
            | Ok access, _ when not (GitHub.sameRepository probeTarget access) -> Error("FAIL", "GitHub answered for a different repository")
            | Ok access, Some b when (targetOn b |> Result.isOk) ->
                if r.Scopes |> Option.exists (fun s -> Regex.IsMatch(s, @"(^|[ ,])(repo|admin:[a-z_]+)($|[ ,])")) then
                    Error("FAIL", "classic token with broad scope (repo/admin); T05 requires a token limited to the test repository")
                else
                    Ok(
                        sprintf "200 for %s; default branch %s; %s; classic scopes %s" access.FullName b (if access.IsPrivate then "private" else "public")
                            (if r.Scopes.IsSome then "narrow" else "absent (fine-grained token)"),
                        [ "branch", b; "private", (if access.IsPrivate then "true" else "false") ]
                    )
            | _ -> Error("FAIL", "the repository answer is malformed or names no usable default branch")
        | 401 -> Error("BLOCKED", "GitHub did not accept the credential (401)")
        | code -> Error("BLOCKED", sprintf "the credential cannot read %s (HTTP %d)" repository code))
    |> note "T05-03a" "Test repository is private" (fun f ->
        if f.["private"] = "true" then "PASS", "private"
        else "ADVISORY", "public: the T05 brief asks for a private test repository where supported (owner action); the content written is test data only")
    |> step "T05-04" "Test repository holds no production data; branch head read (H0)" (fun f ->
        let t = target f
        match headOf t with
        | None -> Error("BLOCKED", sprintf "branch %s has no readable head" t.Branch)
        | Some h0 ->
            match treeOf t h0 with
            | None -> Error("FAIL", "the head commit's tree could not be read")
            | Some tree0 ->
                match GitHub.parseTree (api (GitHub.getTree t tree0)).Body with
                | Error e -> Error("FAIL", "tree listing failed: " + GitHub.describe e)
                | Ok entries ->
                    let paths = entries |> List.filter (fun e -> e.Kind = "blob") |> List.map (fun e -> e.Path)
                    let productionLike =
                        paths
                        |> List.filter (fun p ->
                            [ "catalog/"; "fixtures/"; "contract/" ] |> List.exists (fun d -> p.StartsWith(d, StringComparison.Ordinal))
                            || (p.Contains("/investigations/", StringComparison.Ordinal) && not (p.StartsWith("workspaces/", StringComparison.Ordinal))))
                    if not productionLike.IsEmpty then Error("BLOCKED", "the repository holds production-like paths; nothing was written: " + String.Join(", ", productionLike |> List.truncate 5))
                    else Ok(sprintf "H0 %s, tree %s; %d files: %s" h0 tree0 paths.Length (String.Join(", ", paths |> List.truncate 10)), [ "H0", h0; "tree0", tree0 ]))
    |> step "T05-05" "Production data repository head recorded before any write" (fun _ ->
        match productionHead () with
        | Some h -> Ok("kemiller2002/causa-data main at " + h, [ "productionBefore", h ])
        | None -> Error("BLOCKED", "the head of kemiller2002/causa-data could not be read, so an unintended change could not be ruled out"))
    |> step "T05-06" "Guarded GraphQL commit (createCommitOnBranch, expectedHeadOid = H0) succeeds" (fun f ->
        let t = target f
        let path = probePath t "accepted"
        let r =
            api (GitHub.commitOnBranch t f.["H0"] (sprintf "T05 verification %s: guarded write" runId) "Indy-init T05 persistence verification. Test content only; the cleanup commit of the same run removes it." [ path, probe runId "accepted" ])
        match published r with
        | Ok h1 -> Ok(sprintf "expected head %s; GitHub created %s; wrote %s" f.["H0"] h1 path, [ "H1", h1; "acceptedPath", path ])
        | Error NotFastForward -> Error("FAIL", "the guarded write was refused as stale although the head had just been read")
        | Error(Forbidden false as e) | Error(Unauthorized as e) | Error(NotFound as e) -> Error("BLOCKED", "the credential cannot write to the test repository: " + GitHub.describe e)
        | Error e -> Error("FAIL", "guarded write failed: " + GitHub.describe e))
    |> step "T05-07" "Write applied: the branch names H1, whose only parent is H0" (fun f ->
        let t = target f
        match headOf t, parents (api (GitHub.getCommit t f.["H1"])).Body with
        | Some h, [ p ] when h = f.["H1"] && p = f.["H0"] -> Ok(sprintf "branch at %s; parent %s" h p, [])
        | h, ps -> Error("FAIL", sprintf "branch at %s with parents [%s]; expected %s on %s" (Option.defaultValue "?" h) (String.Join(", ", ps)) f.["H1"] f.["H0"]))
    |> step "T05-08" "Readback: the committed file at H1 equals the submitted bytes" (fun f ->
        let t = target f
        let expected = probe runId "accepted"
        let r = raw (contentsAt t f.["acceptedPath"] f.["H1"])
        if r.Status = 200 && r.Bytes = expected then Ok(sprintf "%d bytes, sha256 %s" expected.Length (sha256 expected), [ "acceptedSha256", sha256 expected ])
        else Error("FAIL", sprintf "readback HTTP %d, %d bytes (sha256 %s), expected %d bytes (sha256 %s)" r.Status r.Bytes.Length (sha256 r.Bytes) expected.Length (sha256 expected)))
    |> step "T05-09" "Stale write (expectedHeadOid = H0 after H1) is rejected by GitHub" (fun f ->
        let t = target f
        let r =
            api (GitHub.commitOnBranch t f.["H0"] (sprintf "T05 verification %s: stale write (must be refused)" runId) "Indy-init T05 persistence verification. GitHub must refuse this commit." [ probePath t "stale", probe runId "stale" ])
        match published r with
        | Error NotFastForward -> Ok(sprintf "refused: HTTP %d, GraphQL error type %s; adapter classification NotFastForward" r.Status (graphqlErrorType r.Body), [ "staleRefusal", graphqlErrorType r.Body ])
        | Ok created -> Error("FAIL", sprintf "GitHub accepted a write based on the stale head %s and created %s: a stale writer can overwrite" f.["H0"] created)
        | Error e -> Error("FAIL", "the stale write failed for another reason than a moved head: " + GitHub.describe e))
    |> step "T05-10" "Repository state preserved after the rejected write" (fun f ->
        let t = target f
        let accepted = raw (contentsAt t f.["acceptedPath"] f.["H1"])
        let stale = raw (contentsAt t (probePath t "stale") f.["H1"])
        match headOf t with
        | Some h when h = f.["H1"] && accepted.Bytes = probe runId "accepted" && stale.Status = 404 ->
            Ok(sprintf "branch still at %s; accepted file intact; stale file absent (404)" h, [])
        | h -> Error("FAIL", sprintf "branch at %s, accepted file HTTP %d, stale file HTTP %d" (Option.defaultValue "?" h) accepted.Status stale.Status))
    |> always "T05-11" "Cleanup: a guarded deletion restores the branch tree to H0's" (attempted "T05-06") (fun f ->
        let t = target f
        match headOf t with
        | None -> Error("FAIL", "the branch head could not be read for cleanup")
        | Some current ->
            let present = [ probePath t "accepted"; probePath t "stale" ] |> List.filter (fun p -> (raw (contentsAt t p current)).Status = 200)
            let finalHead, removal =
                if present.IsEmpty then current, "nothing to remove"
                else
                    match published (api (deleteOnBranch t current (sprintf "T05 verification %s: cleanup" runId) present)) with
                    | Ok h2 -> h2, sprintf "removed %s in %s (expected head %s)" (String.Join(", ", present)) h2 current
                    | Error e -> current, "cleanup commit failed: " + GitHub.describe e
            match headOf t, treeOf t finalHead with
            | Some h, Some tree when h = finalHead && tree = f.["tree0"] -> Ok(sprintf "%s; branch at %s with tree %s = H0's tree" removal h tree, [ "H2", finalHead; "tree2", tree ])
            | h, tree -> Error("FAIL", sprintf "%s; branch at %s with tree %s, H0's tree %s" removal (Option.defaultValue "?" h) (Option.defaultValue "?" tree) f.["tree0"]))
    |> always "T05-12" "Production data repository unchanged" (fun s -> s.Facts.ContainsKey "productionBefore") (fun f ->
        match productionHead () with
        | Some h when h = f.["productionBefore"] -> Ok("kemiller2002/causa-data main still at " + h, [ "productionAfter", h ])
        | h -> Error("FAIL", sprintf "kemiller2002/causa-data main moved from %s to %s" f.["productionBefore"] (Option.defaultValue "?" h)))

// ---- evidence and output ------------------------------------------------------------------------------------------

let private verdictOf (checks: Check list) =
    let gating = checks |> List.filter (fun c -> c.Status <> "ADVISORY")
    if gating |> List.forall (fun c -> c.Status = "PASS") then "PASS"
    elif gating |> List.exists (fun c -> c.Status = "FAIL") then "FAIL"
    else "BLOCKED"

let private evidenceJson (runId: string) (repository: string) (s: State) (checks: Check list) =
    use stream = new MemoryStream()

    (use w = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
     let env name = Environment.GetEnvironmentVariable name |> Option.ofObj |> Option.defaultValue ""
     w.WriteStartObject()
     w.WriteString("schema", "indy-init.t05-persistence-evidence/1")
     w.WriteString("executedAt", DateTime.UtcNow.ToString "yyyy-MM-ddTHH:mm:ssZ")
     w.WriteString("runId", runId)
     w.WriteString("workflowRun", env "GITHUB_SERVER_URL" + "/" + env "GITHUB_REPOSITORY" + "/actions/runs/" + env "GITHUB_RUN_ID")
     w.WriteString("testRepository", repository)
     w.WriteString("branch", s.Facts.TryFind "branch" |> Option.defaultValue "")
     w.WriteStartObject "adapter"
     w.WriteString("repository", "kemiller2002/indy-init-rehearsal-002")
     w.WriteString("commit", env "T05_ADAPTER_COMMIT")
     w.WriteString("file", "src/engine/GitHub.fs")
     w.WriteEndObject()
     w.WriteString("harnessCommit", env "T05_HARNESS_COMMIT")
     w.WriteStartObject "facts"
     s.Facts |> Map.iter (fun k v -> w.WriteString(k, v))
     w.WriteEndObject()
     w.WriteStartArray "checks"
     checks
     |> List.iter (fun c ->
         w.WriteStartObject()
         w.WriteString("id", c.Id)
         w.WriteString("title", c.Title)
         w.WriteString("status", c.Status)
         w.WriteString("detail", c.Detail)
         w.WriteEndObject())
     w.WriteEndArray()
     w.WriteString("verdict", verdictOf checks)
     w.WriteEndObject())

    Encoding.UTF8.GetString(stream.ToArray())

let private contains (secret: string) (text: string) = secret <> "" && text.Contains(secret, StringComparison.Ordinal)

let private run (token: string) (evidencePath: string) =
    let env name = Environment.GetEnvironmentVariable name |> Option.ofObj |> Option.defaultValue ""
    let repository = env "CAUSA_TEST_REPO"
    let runId = "t05-" + (if env "T05_RUN_ID" = "" then DateTime.UtcNow.ToString "yyyyMMddHHmmss" else env "T05_RUN_ID")
    use http = new HttpClient(Timeout = TimeSpan.FromSeconds 30.0)
    let final = verify http token repository runId

    // The credential must not appear in anything this run wrote to GitHub or will print.
    let written = [ probe runId "accepted"; probe runId "stale" ] |> List.map Encoding.UTF8.GetString
    let screened = evidenceJson runId repository final final.Checks
    let exposed = written @ [ screened ] |> List.exists (contains token)

    let screen =
        { Id = "T05-13"
          Title = "No credential in committed content, evidence or output"
          Status = (if exposed then "FAIL" else "PASS")
          Detail = (if exposed then "the credential appeared in output; output withheld" else "credential absent from both probe files and from the evidence") }

    let checks = final.Checks @ [ screen ]
    let evidence = if exposed then "{\"verdict\":\"FAIL\",\"detail\":\"credential exposure; output withheld\"}" else evidenceJson runId repository final checks
    let verdict = if exposed then "FAIL" else verdictOf checks

    if not exposed then checks |> List.iter (fun c -> printfn "%-8s %-9s %s" c.Id c.Status c.Detail)
    printfn "verdict: %s" verdict
    if evidencePath <> "" then File.WriteAllText(evidencePath, evidence)

    match verdict with
    | "PASS" -> 0
    | "FAIL" -> 1
    | _ -> 3

/// Credential screen for files produced elsewhere in the workflow (the preflight's output and evidence).
let private scan (token: string) (files: string list) =
    if token = "" then
        printfn "credential screen: no credential in this environment; nothing to compare"
        0
    else
        match files |> List.filter (fun f -> contains token (File.ReadAllText f)) with
        | [] ->
            printfn "credential screen: clean (%d files)" files.Length
            0
        | hits ->
            printfn "credential screen: credential found in %s" (String.Join(", ", hits |> List.map Path.GetFileName))
            1

[<EntryPoint>]
let main argv =
    let token = Environment.GetEnvironmentVariable "CAUSA_GITHUB_TOKEN" |> Option.ofObj |> Option.defaultValue ""

    match List.ofArray argv with
    | "scan" :: files when not files.IsEmpty -> scan token files
    | [ "--evidence"; path ] -> run token path
    | [] -> run token ""
    | _ ->
        eprintfn "usage: persistence-verification [--evidence FILE] | scan FILE..."
        2
