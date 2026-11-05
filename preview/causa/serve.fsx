// Serves dist/ locally for manual and browser tests: dotnet fsi serve.fsx [port]. Static files only; no state.
open System
open System.IO
open System.Net

let root = Path.Combine(__SOURCE_DIRECTORY__, "dist")
let port = match fsi.CommandLineArgs with [| _; p |] -> int p | _ -> 4173
let types =
    dict [ ".html", "text/html; charset=utf-8"; ".js", "text/javascript"; ".mjs", "text/javascript"; ".css", "text/css"; ".json", "application/json"
           ".wasm", "application/wasm"; ".dat", "application/octet-stream"; ".svg", "image/svg+xml"; ".woff2", "font/woff2"; ".map", "application/json" ]
let listener = new HttpListener()
listener.Prefixes.Add $"http://+:{port}/"
listener.Start()
printfn "serving %s on http://localhost:%d/" root port
while true do
    let ctx = listener.GetContext()
    let rel = Uri.UnescapeDataString(ctx.Request.Url.AbsolutePath.TrimStart('/'))
    let path = Path.GetFullPath(Path.Combine(root, (if rel = "" then "index.html" else rel)))
    let res = ctx.Response
    if path.StartsWith(root) && File.Exists path then
        let bytes = File.ReadAllBytes path
        res.ContentType <- (match types.TryGetValue(Path.GetExtension path) with | true, t -> t | _ -> "application/octet-stream")
        res.ContentLength64 <- int64 bytes.Length
        res.OutputStream.Write(bytes, 0, bytes.Length)
    else res.StatusCode <- 404
    res.Close()
