// Runs the engine's artifact-URL predicate (Decide.fs: prefix test + Uri.IsWellFormedUriString Absolute) on stdin lines (JSON strings).
open System
open System.Text.Json
let wellFormed (u: string) =
    (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    && Uri.IsWellFormedUriString(u, UriKind.Absolute)
let mutable line = Console.ReadLine()
while not (isNull line) do
    let u = JsonSerializer.Deserialize<string>(line)
    printfn "%s" (JsonSerializer.Serialize({| url = u; accepted = wellFormed u |}))
    line <- Console.ReadLine()
