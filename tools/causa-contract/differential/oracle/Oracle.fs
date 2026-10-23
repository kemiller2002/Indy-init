/// Differential oracle: runs the pinned engine's History.validate over wire-format histories.
/// Usage: oracle <events.ndjson>...  -> one JSON line per file: {"file","decode","codes":[...]}
module Oracle.Program

open System.IO
open IndyInitRootCauseInvestigator.Engine
open CleanConsumer

let private esc (s: string) = s.Replace("\\", "\\\\").Replace("\"", "\\\"")

let private judge (path: string) : string =
    let lines = File.ReadAllText(path).Split('\n') |> Array.filter (fun l -> l <> "") |> List.ofArray
    let decoded = lines |> Domain.traverse (fun l -> Json.parse l |> Result.bind Domain.decodeEvent)
    match decoded with
    | Error e -> sprintf "{\"file\":\"%s\",\"decode\":\"%s\",\"codes\":[]}" (esc path) (esc e)
    | Ok events ->
        let codes =
            try History.validate events |> List.map (fun e -> "\"" + esc e.Code + "\"") |> String.concat ","
            with ex -> "\"EXCEPTION:" + esc (ex.GetType().Name) + "\""
        sprintf "{\"file\":\"%s\",\"decode\":null,\"codes\":[%s]}" (esc path) codes

[<EntryPoint>]
let main argv =
    argv |> Array.iter (judge >> printfn "%s")
    0
