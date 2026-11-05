module Causa.Ui.Tests.Support

open System.IO
open System.Text.Json

/// The repository root, found from the test binary (bin/Debug/net10.0 under proposals/r10-contract-first-ui/tests).
let repoRoot =
    let rec up (d: DirectoryInfo) = if File.Exists(Path.Combine(d.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(d.FullName, "requirements-compiled")) then d.FullName else up d.Parent
    up (DirectoryInfo(System.AppContext.BaseDirectory))

let json (relative: string) = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, relative))).RootElement
let proposal (relative: string) = json ("proposals/r10-contract-first-ui/" + relative)

let arr (e: JsonElement) (k: string) =
    match e.TryGetProperty k with
    | true, v when v.ValueKind = JsonValueKind.Array -> v.EnumerateArray() |> List.ofSeq
    | _ -> []
let str (e: JsonElement) (k: string) =
    match e.TryGetProperty k with
    | true, v when v.ValueKind = JsonValueKind.String -> Some(v.GetString() |> string)
    | _ -> None
let strs (e: JsonElement) (k: string) = arr e k |> List.map (fun x -> x.GetString() |> string)
