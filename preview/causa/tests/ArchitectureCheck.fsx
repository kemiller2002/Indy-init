// Architecture check: the Causa application is F#. Every non-F# file in the application is one of the documented
// exceptions (ARCHITECTURE.md), and each exception stays mechanism-only. Run: dotnet fsi tests/ArchitectureCheck.fsx
// Exit code 0 only when every check passes. Run it after `dotnet fsi build.fsx` to also check dist/.
open System
open System.IO
open System.Text.RegularExpressions

let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let rel (p: string) = Path.GetRelativePath(root, p).Replace('\\', '/')
let read p = File.ReadAllText(Path.Combine(root, p))
let mutable failures = 0
let check id (title: string) (problems: string list) =
    if problems.IsEmpty then printfn "PASS %s %s" id title
    else
        failures <- failures + 1
        printfn "FAIL %s %s" id title
        problems |> List.iter (printfn "     - %s")

let excluded (p: string) =
    let r = rel p
    [ "web/node_modules/"; "dist/"; "build/"; "tests/" ] |> List.exists r.StartsWith || r.Contains "/bin/" || r.Contains "/obj/"

let appFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories) |> Array.filter (excluded >> not) |> Array.map rel |> Array.sort |> List.ofArray

// A1: only the documented non-F# files exist in application source.
let allowedNonFSharp = set [ "web/main.js"; "web/index.html"; "web/app.css"; "src/Causa.Preview.Wasm/Program.cs" ]
let codeExtensions = set [ ".js"; ".mjs"; ".cjs"; ".ts"; ".tsx"; ".jsx"; ".cs"; ".html"; ".css"; ".vue"; ".svelte" ]
check "A1" "Application source is F#, except the documented exceptions JS-1, CS-1 and the markup/style files"
    (appFiles |> List.filter (fun f -> codeExtensions.Contains(Path.GetExtension f) && not (allowedNonFSharp.Contains f)) |> List.map (fun f -> $"{f} is not an allowed non-F# file"))

// A2: the JavaScript composition root only constructs Limen's kernel and Limen's .NET transport.
let mainJs = read "web/main.js"
let code = mainJs.Split('\n') |> Array.filter (fun l -> let t = l.Trim() in t <> "" && not (t.StartsWith "//")) |> List.ofArray
let forbidden = [ "fetch"; "XMLHttpRequest"; "localStorage"; "sessionStorage"; "Date"; "setTimeout"; "setInterval"; "innerHTML"; "textContent"; "addEventListener"; "querySelector"; "JSON."; "history"; "location"; "function"; "class "; "if "; "switch"; "for "; "while"; "dataset" ]
let imports = Regex.Matches(mainJs, @"(?:from\s+|import\()\s*""([^""]+)""") |> Seq.map (fun m -> m.Groups[1].Value) |> Set.ofSeq
check "A2" "JS-1 main.js is a composition root: at most 4 code lines, Limen and dotnet.js imports only, no application or browser logic"
    ([ if code.Length > 4 then $"{code.Length} code lines (at most 4)" ]
     @ (forbidden |> List.filter (fun t -> code |> List.exists (fun l -> l.Contains t)) |> List.map (fun t -> $"uses '{t}'"))
     @ (imports - set [ "@echelon-foundry/limen"; "@echelon-foundry/limen/hosts/dotnet-wasm"; "./_framework/dotnet.js" ] |> Set.toList |> List.map (fun i -> $"imports {i}"))
     @ [ if (Regex.Matches(mainJs, "=>")).Count > 1 then "more than one arrow function" ])

// A3: the C# file is a single forwarding [JSExport] (the JSExport generator is C#-only).
let cs = read "src/Causa.Preview.Wasm/Program.cs"
let csCode = Regex.Replace(Regex.Replace(cs, @"///[^\n]*", ""), @"//[^\n]*", "")
let csForbidden = [ "if", @"\bif\b"; "switch", @"\bswitch\b"; "for", @"\bfor\b"; "while", @"\bwhile\b"; "new", @"\bnew\b"; "?:", @"\?"; "Console", @"\bConsole\b"; "JSImport", @"\bJSImport\b" ]
check "A3" "CS-1 Program.cs is one [JSExport] that forwards to Causa.Preview.Dispatch.handle and decides nothing"
    ([ if Regex.Matches(csCode, @"\[JSExport\]").Count <> 1 then "must declare exactly one [JSExport]" ]
     @ [ if not (Regex.IsMatch(csCode, @"\[JSExport\]\s*public static string Dispatch\(string messageJson\)\s*=>\s*Causa\.Preview\.Dispatch\.handle\(messageJson\);")) then "the export must be exactly Dispatch(messageJson) => Causa.Preview.Dispatch.handle(messageJson)" ]
     @ (csForbidden |> List.filter (fun (_, rx) -> Regex.IsMatch(csCode, rx)) |> List.map (fun (t, _) -> "contains " + t)))

// A4: the page carries no script of its own beyond the import map and the composition root.
let html = read "web/index.html"
let scripts = Regex.Matches(html, @"<script\b([^>]*)>([\s\S]*?)</script>") |> Seq.map (fun m -> m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim()) |> List.ofSeq
let isAllowedScript (attrs: string, body: string) = attrs = "type=\"importmap\"" || (attrs = "type=\"module\" src=\"./main.js\"" && body = "")
let scriptProblems = scripts |> List.filter (isAllowedScript >> not) |> List.map (fun (a, _) -> "unexpected <script " + a + ">")
let handlerProblems = Regex.Matches(html, @"\s(on[a-z]+)\s*=") |> Seq.map (fun m -> "inline handler " + m.Groups[1].Value) |> List.ofSeq
let urlProblems = if html.Contains "javascript:" then [ "javascript: URL" ] else []
check "A4" "index.html: only the import map and <script type=module src=./main.js>; no inline handlers or javascript: URLs" (scriptProblems @ handlerProblems @ urlProblems)

// A5: the F# engine touches no browser API: every effect crosses the Limen boundary as data.
let engineSources = Directory.GetFiles(Path.Combine(root, "src/Causa.Preview.Engine"), "*.fs") |> Array.map (fun f -> rel f, File.ReadAllText f) |> List.ofArray
check "A5" "The F# engine references no JavaScript interop or browser object"
    (engineSources |> List.collect (fun (f, t) -> [ "JSImport"; "JSExport"; "JSObject"; "System.Runtime.InteropServices.JavaScript"; "IJSRuntime" ] |> List.filter t.Contains |> List.map (fun x -> $"{f} uses {x}")))

// A6: every value the page binds is projected by the F# engine (a missing data-if key would silently unmount).
let viewSource = read "src/Causa.Preview.Engine/View.fs"
let bound = Regex.Matches(html, @"data-(?:text|if|each|bind-[a-z]+)=""([A-Za-z][A-Za-z0-9]*)""") |> Seq.map (fun m -> m.Groups[1].Value) |> Set.ofSeq
let itemFields = set [ "title"; "detail"; "id"; "text"; "state"; "type"; "at"; "actor"; "key"; "version" ]
check "A6" "Every data-text/if/each/bind key in index.html is projected by View.fs"
    (bound |> Set.filter (fun k -> not (viewSource.Contains $"\"{k}\"") && not (itemFields.Contains k)) |> Set.toList |> List.map (fun k -> $"'{k}' is bound but never projected"))

// A7: the built site contains no application JavaScript beyond the composition root.
let dist = Path.Combine(root, "dist")
if Directory.Exists dist then
    let js = Directory.GetFiles(dist, "*.js", SearchOption.AllDirectories) |> Array.map (fun f -> Path.GetRelativePath(dist, f).Replace('\\', '/')) |> List.ofArray
    check "A7" "dist/: JavaScript is only main.js, Limen (limen/), the .NET runtime (_framework/) and Forma (forma/)"
        (js |> List.filter (fun f -> not (f = "main.js" || f.StartsWith "limen/" || f.StartsWith "_framework/" || f.StartsWith "forma/")) |> List.map (fun f -> $"dist/{f}"))
else printfn "SKIP A7 dist/ not built"

printfn "%s" (if failures = 0 then "architecture: PASS" else $"architecture: {failures} check(s) FAILED")
exit (if failures = 0 then 0 else 1)
