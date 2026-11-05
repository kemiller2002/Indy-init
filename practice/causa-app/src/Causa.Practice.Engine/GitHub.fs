// Practice only (FENCE.md). The GitHub contents API as pure request descriptions and response decoders. The kernel
// performs the request; nothing here touches the network. The token lives only in engine memory: it is placed in an
// Authorization header and never projected into the view.
namespace Causa.Practice

open System
open System.Text.Json.Nodes

type Connection =
    { Api: string
      Owner: string
      Repo: string
      Branch: string
      Path: string
      Token: string option }

type HttpRequest =
    { Method: string
      Url: string
      Headers: (string * string) list
      Body: string option }

module GitHub =
    let realApi = "https://api.github.com"

    let private esc (s: string) = Uri.EscapeDataString s
    let private segs (p: string) = p.Split('/', StringSplitOptions.RemoveEmptyEntries) |> Array.map esc |> String.concat "/"

    let private headers (c: Connection) =
        [ "Accept", "application/vnd.github+json"
          "X-GitHub-Api-Version", "2022-11-28" ]
        @ (c.Token |> Option.map (fun t -> [ "Authorization", "Bearer " + t ]) |> Option.defaultValue [])

    /// A unique query parameter on every read: api.github.com answers reads with Cache-Control: private, max-age=60, so
    /// a read-back after a write could otherwise come from the browser's HTTP cache (live preflight run 42068316592).
    let private get (c: Connection) (path: string) (nonce: string) =
        { Method = "GET"; Url = $"{c.Api}/repos/{esc c.Owner}/{esc c.Repo}{path}"; Headers = headers c; Body = None }
        |> fun r -> { r with Url = r.Url + (if r.Url.Contains "?" then "&" else "?") + "_r=" + esc nonce }

    let probe (c: Connection) nonce = get c "" nonce
    let list (c: Connection) nonce = get c $"/contents/{segs c.Path}?ref={esc c.Branch}" nonce
    let read (c: Connection) (dir: string) (name: string) nonce = get c $"/contents/{segs c.Path}/{esc dir}/{esc name}?ref={esc c.Branch}" nonce

    let write (c: Connection) (dir: string) (name: string) (bytes: byte[]) =
        let body = JsonObject()
        body["message"] <- JsonValue.Create $"Causa practice: create {dir}/{name}"
        body["content"] <- JsonValue.Create(Convert.ToBase64String bytes)
        body["branch"] <- JsonValue.Create c.Branch
        { Method = "PUT"; Url = $"{c.Api}/repos/{esc c.Owner}/{esc c.Repo}/contents/{segs c.Path}/{esc dir}/{esc name}"
          Headers = headers c @ [ "Content-Type", "application/json" ]; Body = Some(body.ToJsonString()) }

    /// Directory entry names of a contents listing.
    let names (body: JsonNode) : Result<string list, string> =
        match body with
        | :? JsonArray as a -> a |> Seq.choose (fun e -> match e with null -> None | e -> Option.ofObj (e["name"]) |> Option.map (fun n -> n.GetValue<string>())) |> List.ofSeq |> Ok
        | _ -> Error "the listing is not a directory"

    /// Exact bytes of a contents read (base64; GitHub wraps it in newlines).
    let bytes (body: JsonNode) : Result<byte[], string> =
        match body with
        | :? JsonObject as o ->
            match o["content"], o["encoding"] with
            | null, _ -> Error "no content (file larger than the contents API returns?)"
            | c, e when not (isNull e) && e.GetValue<string>() = "base64" ->
                try Ok(Convert.FromBase64String(c.GetValue<string>().Replace("\n", "").Replace("\r", ""))) with ex -> Error ex.Message
            | _ -> Error "unexpected encoding"
        | _ -> Error "not a file"

    let validApi (s: string) =
        match Uri.TryCreate(s, UriKind.Absolute) with
        | true, u -> u.Scheme = "https" || (u.Scheme = "http" && (u.Host = "localhost" || u.Host = "127.0.0.1"))
        | _ -> false
