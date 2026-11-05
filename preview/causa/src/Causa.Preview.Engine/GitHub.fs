/// GitHub requests and answers as pure data. The engine never performs I/O: it emits Limen Http effect requests
/// and interprets their outcomes. The token is sent only in the Authorization header and never stored.
module Causa.Preview.GitHub

open System
open Causa.HistoricalCompatibility

type RepoRef =
    { Owner: string
      Name: string
      Branch: string }

module RepoRef =
    /// "owner/name" and a branch; None when malformed.
    let parse (ownerName: string) (branch: string) : RepoRef option =
        match ownerName.Trim().Split('/') with
        | [| o; n |] when o <> "" && n <> "" && branch.Trim() <> "" -> Some { Owner = o; Name = n; Branch = branch.Trim() }
        | _ -> None

    let text (r: RepoRef) = $"{r.Owner}/{r.Name}@{r.Branch}"

/// One HTTP request, in Limen's HttpEffectRequest vocabulary.
type HttpRequest =
    { Method: string
      Url: string
      Headers: (string * string) list
      Body: string option
      Response: string }

let private api = "https://api.github.com"

let private seg (s: string) = Uri.EscapeDataString s

let private path (p: string) = String.Join("/", p.Split('/') |> Array.map seg)

let private headers (token: string) (accept: string) =
    [ "Accept", accept; "X-GitHub-Api-Version", "2022-11-28" ]
    @ (if String.IsNullOrWhiteSpace token then [] else [ "Authorization", "Bearer " + token.Trim() ])

/// The branch head (PERSIST-005: read the head, then read files at that commit).
let branchHead (token: string) (r: RepoRef) =
    { Method = "GET"; Url = $"{api}/repos/{seg r.Owner}/{seg r.Name}/branches/{seg r.Branch}"
      Headers = headers token "application/vnd.github+json"; Body = None; Response = "text" }

/// A file's exact bytes at a commit (base64 representation keeps every byte, CONTRACT §2.1).
let fileAt (token: string) (r: RepoRef) (commit: string) (filePath: string) =
    { Method = "GET"; Url = $"{api}/repos/{seg r.Owner}/{seg r.Name}/contents/{path filePath}?ref={seg commit}"
      Headers = headers token "application/vnd.github.raw+json"; Body = None; Response = "base64" }

/// A directory listing at a commit.
let listAt (token: string) (r: RepoRef) (commit: string) (dirPath: string) =
    { Method = "GET"; Url = $"{api}/repos/{seg r.Owner}/{seg r.Name}/contents/{path dirPath}?ref={seg commit}"
      Headers = headers token "application/vnd.github+json"; Body = None; Response = "text" }

/// PERSIST-006: one createCommitOnBranch per save, conditional on the head the writer read.
let createCommit (token: string) (r: RepoRef) (expectedHead: string) (message: string) (files: (string * byte[]) list) =
    let additions = JArr(files |> List.map (fun (p, b) -> JObj [ "path", JStr p; "contents", JStr(Convert.ToBase64String b) ]))
    let input =
        JObj
            [ "branch", JObj [ "repositoryNameWithOwner", JStr $"{r.Owner}/{r.Name}"; "branchName", JStr r.Branch ]
              "expectedHeadOid", JStr expectedHead
              "message", JObj [ "headline", JStr message ]
              "fileChanges", JObj [ "additions", additions ] ]
    let body =
        JObj
            [ "query", JStr "mutation($input: CreateCommitOnBranchInput!) { createCommitOnBranch(input: $input) { commit { oid } } }"
              "variables", JObj [ "input", input ] ]
    { Method = "POST"; Url = $"{api}/graphql"; Headers = headers token "application/json"; Body = Some(Json.canonical body); Response = "text" }

// ---------------------------------------------------------------- answers

type Answer =
    | Ok200 of status: int * body: string
    | HttpStatus of status: int * body: string
    | NoAnswer of reason: string
    /// The server may or may not have applied a write (Limen OutcomeUnknown). Never reported as success.
    | Unknown of reason: string

let private field k (o: (string * JValue) list) = Json.tryField k o

let parseJson (body: string) : Result<JValue, string> =
    // GitHub JSON may contain floats (e.g. scores); none of the fields read here are floats, so a strict parse failure
    // is reported rather than guessed around.
    Json.parse body |> Result.mapError (fun e -> $"unreadable JSON ({e.Detail})")

/// The commit SHA of a branch answer.
let headOf (body: string) : Result<string, string> =
    parseJson body
    |> Result.bind (function
        | JObj o ->
            match field "commit" o with
            | Some(JObj c) -> (match field "sha" c with Some(JStr s) -> Ok s | _ -> Error "branch answer has no commit sha")
            | _ -> Error "branch answer has no commit"
        | _ -> Error "branch answer is not an object")

/// Directory entries (name, type) of a listing answer.
let entriesOf (body: string) : Result<(string * string) list, string> =
    parseJson body
    |> Result.bind (function
        | JArr items ->
            Ok(items |> List.choose (function JObj o -> (match field "name" o, field "type" o with Some(JStr n), Some(JStr t) -> Some(n, t) | _ -> None) | _ -> None))
        | _ -> Error "listing answer is not an array")

/// The new commit oid of a createCommitOnBranch answer, or the GraphQL error message.
let commitOidOf (body: string) : Result<string, string> =
    parseJson body
    |> Result.bind (function
        | JObj o ->
            match field "errors" o with
            | Some(JArr(JObj e :: _)) -> Error(match field "message" e with Some(JStr m) -> m | _ -> "GraphQL error")
            | _ ->
                match field "data" o with
                | Some(JObj d) ->
                    match field "createCommitOnBranch" d with
                    | Some(JObj c) ->
                        match field "commit" c with
                        | Some(JObj k) -> (match field "oid" k with Some(JStr s) -> Ok s | _ -> Error "no commit oid")
                        | _ -> Error "no commit"
                    | _ -> Error "no createCommitOnBranch result"
                | _ -> Error "no data"
        | _ -> Error "GraphQL answer is not an object")

/// GitHub's message for a failed request, without echoing request headers or the token.
let messageOf (body: string) =
    match Json.parse body with
    | Ok(JObj o) -> (match field "message" o with Some(JStr m) -> m | _ -> "request refused")
    | _ -> "request refused"
