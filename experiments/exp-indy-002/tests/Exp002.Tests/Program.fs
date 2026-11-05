module Exp002.Tests.Program

open Exp002.Tests.Runner

let allTests: Test list = StartTests.tests

[<EntryPoint>]
let main _ =
    let results = allTests |> List.map run
    results
    |> List.iter (fun (t, r) ->
        match r with
        | Ok() -> printfn "PASS %s %s" t.Id t.Name
        | Error e -> printfn "FAIL %s %s: %s" t.Id t.Name e)
    let failed = results |> List.filter (snd >> Result.isError) |> List.length
    printfn "TOTAL %d PASS %d FAIL %d" results.Length (results.Length - failed) failed
    if failed = 0 then 0 else 1
