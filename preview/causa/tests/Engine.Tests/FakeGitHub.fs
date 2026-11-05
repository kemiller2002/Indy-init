/// An in-process GitHub double for engine tests: branches point at commits, commits hold whole trees, and
/// createCommitOnBranch is conditional on expectedHeadOid exactly as the real API is. Test-only; never evidence of
/// real-provider behaviour.
module FakeGitHub

open System
open System.Text
open Causa.HistoricalCompatibility

type Repo =
    { Branches: Map<string, string>
      Commits: Map<string, Map<string, byte[]>> }

type World =
    { Repos: Map<string, Repo>
      Counter: int
      /// Fault injection.
      CorruptReadBack: bool
      UnknownCommitOutcome: bool
      Requests: string list }

let seed (repos: (string * Map<string, byte[]>) list) =
    { Repos = repos |> List.map (fun (name, tree) -> name, { Branches = Map.ofList [ "main", "c0" ]; Commits = Map.ofList [ "c0", tree ] }) |> Map.ofList
      Counter = 1; CorruptReadBack = false; UnknownCommitOutcome = false; Requests = [] }

/// A commit made outside the application (another writer), moving the branch head.
let externalCommit (repo: string) (path: string) (bytes: byte[]) (w: World) =
    let r = w.Repos[repo]
    let head = r.Branches["main"]
    let oid = $"x{w.Counter}"
    let r' = { Branches = r.Branches.Add("main", oid); Commits = r.Commits.Add(oid, r.Commits[head].Add(path, bytes)) }
    { w with Repos = w.Repos.Add(repo, r'); Counter = w.Counter + 1 }

let private success (status: int) (body: string) = JObj [ "kind", JStr "Success"; "status", JInt(int64 status); "body", JStr body ]
let private notFound () = success 404 "{\"message\":\"Not Found\"}"

/// Answers one Limen Http effect request (as JSON) with a Limen EffectOutcome (as JSON).
let answer (w: World) (effect: (string * JValue) list) : World * JValue =
    let str k = match Json.tryField k effect with Some(JStr s) -> s | _ -> ""
    let url = Uri(str "url")
    let w = { w with Requests = w.Requests @ [ str "method" + " " + url.AbsolutePath ] }
    let parts = url.AbsolutePath.Trim('/').Split('/') |> Array.map Uri.UnescapeDataString
    let query = Web.HttpUtility.ParseQueryString url.Query
    match str "method", parts with
    | "GET", [| "repos"; o; n; "branches"; b |] ->
        match w.Repos.TryFind $"{o}/{n}" |> Option.bind (fun r -> r.Branches.TryFind b) with
        | Some head -> w, success 200 (Json.canonical (JObj [ "name", JStr b; "commit", JObj [ "sha", JStr head ] ]))
        | None -> w, notFound ()
    | "GET", p when p.Length >= 5 && p[0] = "repos" && p[3] = "contents" ->
        let repo = $"{p[1]}/{p[2]}"
        let filePath = String.Join("/", p[4..])
        match w.Repos.TryFind repo |> Option.bind (fun r -> r.Commits.TryFind(query["ref"])) with
        | None -> w, notFound ()
        | Some tree ->
            match tree.TryFind filePath with
            | Some bytes when str "response" = "base64" ->
                let bytes = if w.CorruptReadBack && filePath.EndsWith "events.ndjson" && query["ref"].StartsWith "n" then Array.append bytes "{}\n"B else bytes
                w, success 200 (Convert.ToBase64String bytes)
            | _ ->
                let prefix = filePath + "/"
                let children =
                    tree |> Map.toList |> List.map fst |> List.filter (fun k -> k.StartsWith prefix)
                    |> List.map (fun k -> k.Substring(prefix.Length).Split('/')) |> List.map (fun xs -> xs[0], (if xs.Length > 1 then "dir" else "file")) |> List.distinct
                if children.IsEmpty then w, notFound ()
                else w, success 200 (Json.canonical (JArr(children |> List.map (fun (n, t) -> JObj [ "name", JStr n; "type", JStr t ]))))
    | "POST", [| "graphql" |] ->
        if w.UnknownCommitOutcome then w, JObj [ "kind", JStr "OutcomeUnknown"; "reason", JStr "connection-lost" ]
        else
            match Json.parse (str "body") with
            | Ok(JObj b) ->
                let input = match Json.tryField "variables" b with Some(JObj v) -> (match Json.tryField "input" v with Some(JObj i) -> i | _ -> []) | _ -> []
                let branch = match Json.tryField "branch" input with Some(JObj br) -> br | _ -> []
                let repo = match Json.tryField "repositoryNameWithOwner" branch with Some(JStr s) -> s | _ -> ""
                let expected = match Json.tryField "expectedHeadOid" input with Some(JStr s) -> s | _ -> ""
                let r = w.Repos[repo]
                let head = r.Branches["main"]
                if expected <> head then
                    w, success 200 (Json.canonical (JObj [ "errors", JArr [ JObj [ "message", JStr $"Expected branch to point to \"{expected}\" but it did not." ] ] ]))
                else
                    let additions =
                        match Json.tryField "fileChanges" input with
                        | Some(JObj fc) -> (match Json.tryField "additions" fc with Some(JArr xs) -> xs | _ -> [])
                        | _ -> []
                        |> List.choose (function
                            | JObj a -> (match Json.tryField "path" a, Json.tryField "contents" a with Some(JStr p), Some(JStr c) -> Some(p, Convert.FromBase64String c) | _ -> None)
                            | _ -> None)
                    let oid = $"n{w.Counter}"
                    let tree = additions |> List.fold (fun (t: Map<string, byte[]>) (p, c) -> t.Add(p, c)) r.Commits[head]
                    let r' = { Branches = r.Branches.Add("main", oid); Commits = r.Commits.Add(oid, tree) }
                    { w with Repos = w.Repos.Add(repo, r'); Counter = w.Counter + 1 },
                    success 200 (Json.canonical (JObj [ "data", JObj [ "createCommitOnBranch", JObj [ "commit", JObj [ "oid", JStr oid ] ] ] ]))
            | _ -> w, success 400 "{\"message\":\"bad request\"}"
    | _ -> w, notFound ()

let fileOf (w: World) (repo: string) (path: string) =
    let r = w.Repos[repo]
    r.Commits[r.Branches["main"]].TryFind path

let headOf (w: World) (repo: string) = w.Repos[repo].Branches["main"]

let utf8 (b: byte[]) = Encoding.UTF8.GetString b
