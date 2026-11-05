// Serves dist/ on http://localhost:PORT (default 4180) with correct MIME types for .wasm/.js/.mjs/.json.
// Usage: dotnet fsi serve.fsx [port]
open System
open System.IO
open System.Net

let root = Path.Combine(__SOURCE_DIRECTORY__, "dist")
let port = fsi.CommandLineArgs |> Array.tryItem 1 |> Option.defaultValue "4180"

let mime (path: string) =
    match Path.GetExtension(path).ToLowerInvariant() with
    | ".html" -> "text/html; charset=utf-8"
    | ".js" | ".mjs" -> "text/javascript; charset=utf-8"
    | ".css" -> "text/css; charset=utf-8"
    | ".json" -> "application/json"
    | ".wasm" -> "application/wasm"
    | ".svg" -> "image/svg+xml"
    | ".dat" | ".blat" -> "application/octet-stream"
    | _ -> "application/octet-stream"

let resolve (urlPath: string) =
    let rel = Uri.UnescapeDataString(urlPath.TrimStart '/')
    let full = Path.GetFullPath(Path.Combine(root, (if rel = "" then "index.html" else rel)))
    if full.StartsWith(Path.GetFullPath root) && File.Exists full then Some full else None

let listener = new HttpListener()
listener.Prefixes.Add(sprintf "http://localhost:%s/" port)
listener.Start()
printfn "serving %s at http://localhost:%s/" root port

while true do
    let ctx = listener.GetContext()
    try
        match resolve ctx.Request.Url.AbsolutePath with
        | Some file ->
            let bytes = File.ReadAllBytes file
            ctx.Response.ContentType <- mime file
            ctx.Response.ContentLength64 <- int64 bytes.Length
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length)
        | None -> ctx.Response.StatusCode <- 404
    finally
        ctx.Response.Close()
