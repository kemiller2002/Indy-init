/// Runs every check against a causa-data checkout and writes (or, without --write, verifies) the two
/// machine-readable reports next to this proposal. Exit code 0 only when every check passes and, in verify mode,
/// the committed reports equal the regenerated ones.
///
///   dotnet run --project proposals/r10-historical-data-compatibility/tests -- --causa-data <causa-data checkout> [--write]
module Program

open System
open System.IO
open Causa.HistoricalCompatibility
open Checks

let private arg (name: string) (argv: string list) =
    argv |> List.pairwise |> List.tryPick (fun (a, b) -> if a = name then Some b else None)

let private proposalDir () =
    let rec up (d: DirectoryInfo) =
        if isNull d then failwith "proposal directory not found"
        elif File.Exists(Path.Combine(d.FullName, "source-pin.json")) then d.FullName
        else up d.Parent
    up (DirectoryInfo(AppContext.BaseDirectory))

let private report (results: CheckResult list) : JValue =
    JObj
        [ "schema", JStr "r10-historical-data-compatibility/historical-coverage/1"
          "generatedBy", JStr "dotnet run --project proposals/r10-historical-data-compatibility/tests -- --causa-data <checkout> --write"
          "source", JStr "kemiller2002/causa-data@e8259df74add723765c57a5b1a5888d9c3f707af"
          "passed", JBool(results |> List.forall (fun r -> r.Failures.IsEmpty))
          "checks",
          JArr(
              results
              |> List.map (fun r ->
                  JObj
                      [ "id", JStr r.Id
                        "missionCheck", JStr r.MissionCheck
                        "title", JStr r.Title
                        "passed", JBool r.Failures.IsEmpty
                        "failures", JArr(r.Failures |> List.truncate 25 |> List.map JStr)
                        "evidence", JObj r.Evidence ])
          ) ]

[<EntryPoint>]
let main argv =
    let args = List.ofArray argv
    match arg "--causa-data" args with
    | None ->
        eprintfn "usage: --causa-data <causa-data checkout> [--write]"
        2
    | Some root ->
        let dir = proposalDir ()
        let pin = Path.Combine(dir, "source-pin.json")
        let cases = Source.historicalCases root
        let historical = cases |> List.map loadCase
        let fixtures = Source.fixtureCases root |> List.map loadCase
        let apollo1 = historical |> List.find (fun l -> l.Case.CaseKey = "apollo-1")
        let results =
            [ sourcePin pin cases
              allLoad historical
              accounted pin historical
              discriminators historical fixtures
              orderPreserved historical
              identities historical
              requiredFields historical
              negatives root
              unknownTypes apollo1
              deterministic historical
              provenance root historical
              noLoss historical
              replayIndependent historical root fixtures
              byteIdentity root cases
              tampering apollo1
              frozenCompatibility historical
              successorContract historical
              acceptanceLevels historical ]
        let coverage = Json.pretty (report results) + "\n"
        let matrix = Json.pretty (Matrix.build historical fixtures) + "\n"
        let outputs = [ Path.Combine(dir, "HISTORICAL-COVERAGE.json"), coverage; Path.Combine(dir, "EVENT-COMPATIBILITY-MATRIX.json"), matrix ]
        results |> List.iter (fun r -> printfn "%s %-4s %s" (if r.Failures.IsEmpty then "PASS" else "FAIL") r.Id r.Title; r.Failures |> List.truncate 10 |> List.iter (printfn "       - %s"))
        let stale =
            if List.contains "--write" args then
                outputs |> List.iter (fun (p, text) -> File.WriteAllText(p, text))
                []
            else outputs |> List.filter (fun (p, text) -> not (File.Exists p) || File.ReadAllText p <> text) |> List.map fst
        stale |> List.iter (printfn "STALE %s (run with --write and review the diff)")
        let passed = results |> List.forall (fun r -> r.Failures.IsEmpty)
        printfn "%d/%d checks passed" (results |> List.filter (fun r -> r.Failures.IsEmpty) |> List.length) (List.length results)
        if passed && stale.IsEmpty then 0 else 1
