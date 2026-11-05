// Builds the static site into dist/: the published WebAssembly app, the web files, Limen and Forma from their pinned
// packages (web/package.json). Run: dotnet fsi build.fsx (after `npm ci` in web/).
open System.IO
open System.Diagnostics

let root = __SOURCE_DIRECTORY__
let run (exe: string) (args: string) (cwd: string) =
    let p = Process.Start(ProcessStartInfo(exe, args, WorkingDirectory = cwd, UseShellExecute = false))
    p.WaitForExit()
    if p.ExitCode <> 0 then failwithf "%s %s failed (%d)" exe args p.ExitCode

let rec copyDir (src: string) (dst: string) =
    Directory.CreateDirectory dst |> ignore
    for f in Directory.GetFiles src do File.Copy(f, Path.Combine(dst, Path.GetFileName f), true)
    for d in Directory.GetDirectories src do copyDir d (Path.Combine(dst, Path.GetFileName d))

let dist = Path.Combine(root, "dist")
if Directory.Exists dist then Directory.Delete(dist, true)
run "dotnet" "publish src/Causa.Preview.Wasm -c Release -o build/wasm" root
copyDir (Path.Combine(root, "build/wasm/wwwroot")) dist
for f in [ "index.html"; "main.js"; "app.css" ] do File.Copy(Path.Combine(root, "web", f), Path.Combine(dist, f), true)
copyDir (Path.Combine(root, "web/node_modules/@echelon-foundry/limen/dist")) (Path.Combine(dist, "limen"))
copyDir (Path.Combine(root, "web/node_modules/@echelon-foundry/design-system/dist")) (Path.Combine(dist, "forma"))
File.WriteAllText(Path.Combine(dist, ".nojekyll"), "")
printfn "built %s" dist
