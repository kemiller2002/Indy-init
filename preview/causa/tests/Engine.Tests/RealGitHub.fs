/// Real-provider executor: performs each Limen Http effect the engine emits against api.github.com, exactly as built,
/// over plain HTTPS. The only change: when the environment provides GITHUB_TOKEN (a CI workflow's own token), it
/// becomes the Authorization header in place of the engine's (empty) token field. The engine never sees a credential.
/// Test harness only.
module RealGitHub

open System
open System.Net.Http
open System.Text
open Causa.HistoricalCompatibility

let private client =
    let c = new HttpClient(Timeout = TimeSpan.FromSeconds 60.)
    c.DefaultRequestHeaders.UserAgent.ParseAdd "causa-preview-real-provider-test"
    c

let private token = Environment.GetEnvironmentVariable "GITHUB_TOKEN" |> Option.ofObj |> Option.filter (String.IsNullOrWhiteSpace >> not)

let answer (effect: (string * JValue) list) : JValue =
    let str k = match Json.tryField k effect with Some(JStr s) -> s | _ -> ""
    use request = new HttpRequestMessage(HttpMethod(str "method"), str "url")
    match Json.tryField "headers" effect with
    | Some(JObj hs) -> hs |> List.iter (fun (k, v) -> match v with JStr s when k <> "Authorization" -> request.Headers.TryAddWithoutValidation(k, s) |> ignore | _ -> ())
    | _ -> ()
    token |> Option.iter (fun t -> request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + t) |> ignore)
    if str "method" = "POST" then request.Content <- new StringContent(str "body", Encoding.UTF8, "application/json")
    try
        use response = client.Send request
        let bytes = response.Content.ReadAsByteArrayAsync().Result
        let status = int response.StatusCode
        let body = if str "response" = "base64" && status >= 200 && status < 300 then Convert.ToBase64String bytes else Encoding.UTF8.GetString bytes
        JObj [ "kind", JStr "Success"; "status", JInt(int64 status); "body", JStr body ]
    with _ -> JObj [ "kind", JStr "Failure"; "reason", JStr "network" ]
