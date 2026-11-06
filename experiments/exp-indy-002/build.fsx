// Builds the static site into dist/: the published WebAssembly host, the web/ files, and Limen + Forma from the
// pinned packages in web/package.json. Usage: (cd web && npm ci) then `dotnet fsi build.fsx`.
open System.IO
open System.Diagnostics

let here = __SOURCE_DIRECTORY__
let dist = Path.Combine(here, "dist")

let exec (cmd: string) (args: string) =
    use p = Process.Start(ProcessStartInfo(cmd, args, WorkingDirectory = here, UseShellExecute = false))
    p.WaitForExit()
    if p.ExitCode <> 0 then failwithf "`%s %s` exited %d" cmd args p.ExitCode

let rec copyTree (src: string) (dst: string) =
    Directory.CreateDirectory dst |> ignore
    Directory.GetFiles src |> Array.iter (fun f -> File.Copy(f, Path.Combine(dst, Path.GetFileName f), true))
    Directory.GetDirectories src |> Array.iter (fun d -> copyTree d (Path.Combine(dst, Path.GetFileName d)))

if Directory.Exists dist then Directory.Delete(dist, true)
exec "dotnet" "publish src/Exp002.Wasm -c Release -o build/wasm"
copyTree (Path.Combine(here, "build", "wasm", "wwwroot")) dist
[ "index.html"; "main.js"; "app.css" ] |> List.iter (fun f -> File.Copy(Path.Combine(here, "web", f), Path.Combine(dist, f), true))
copyTree (Path.Combine(here, "web", "node_modules", "@echelon-foundry", "limen", "dist")) (Path.Combine(dist, "limen"))
copyTree (Path.Combine(here, "web", "node_modules", "@echelon-foundry", "design-system", "dist")) (Path.Combine(dist, "forma"))
printfn "site built at %s" dist
