/// Serves the deployed Rehearsal 5 site on 127.0.0.1 for the audit's post-rehearsal browser runs. The candidate's
/// non-live browser suite aborts every non-local request (tests/browser/support/fixtures.js), so its CAUSA_BASE_URL
/// mode cannot reach a deployed site. This proxy forwards GET requests byte for byte to the deployed origin and
/// copies status and content type; it changes nothing it forwards. Post-rehearsal tooling only.
module Rehearsal005Audit.DeployedProxy

open System
open System.Net
open System.Net.Http

let private forward (client: HttpClient) (origin: string) (ctx: HttpListenerContext) =
    task {
        let path = ctx.Request.Url.PathAndQuery.TrimStart('/')
        use! upstream = client.GetAsync(origin + path)
        let! body = upstream.Content.ReadAsByteArrayAsync()
        ctx.Response.StatusCode <- int upstream.StatusCode
        upstream.Content.Headers.ContentType |> Option.ofObj |> Option.iter (fun t -> ctx.Response.ContentType <- string t)
        ctx.Response.Headers.Add("x-audit-upstream", origin + path)
        do! ctx.Response.OutputStream.WriteAsync(body, 0, body.Length)
        ctx.Response.Close()
    }

[<EntryPoint>]
let main argv =
    match argv with
    | [| origin; port |] ->
        use client = new HttpClient()
        use listener = new HttpListener()
        listener.Prefixes.Add(sprintf "http://127.0.0.1:%s/" port)
        listener.Start()
        printfn "forwarding http://127.0.0.1:%s/ -> %s" port origin
        let rec serve () =
            let ctx = listener.GetContext()
            forward client origin ctx |> ignore
            serve ()
        serve ()
    | _ ->
        eprintfn "usage: r5-deployed-proxy <origin-with-trailing-slash> <port>"
        2
