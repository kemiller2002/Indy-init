// Appends one contract lifecycle transition to docs/experiments/exp-indy-002/CONTRACT-LIFECYCLE.jsonl.
//   dotnet fsi tools/lifecycle.fsx <group> <STATUS> [actor] [note]
// The hash is SHA-256 over Contracts.fs plus the group's registry entries, so any change to a released contract
// changes the hash; the version increments only when that hash differs from the group's last recorded hash.
open System
open System.IO
open System.Text.Json.Nodes
open System.Security.Cryptography

let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let docs = Path.GetFullPath(Path.Combine(root, "../../docs/experiments/exp-indy-002"))
let log = Path.Combine(docs, "CONTRACT-LIFECYCLE.jsonl")

let groups =
    Map [
        "CG-1-START", [ "INV-001"; "INV-002"; "INV-004"; "INV-005"; "INV-006"; "INV-019"; "INV-020"; "INV-021"; "INV-022"; "INV-023" ]
        "CG-2-HEADER", [ "INV-007"; "INV-008"; "INV-018"; "INV-034"; "INV-035"; "INV-036" ]
        "CG-3-CONTENT", [ "INV-009"; "INV-010"; "INV-011"; "INV-012"; "INV-013"; "INV-014"; "INV-024"; "INV-025"; "INV-026"; "INV-027"; "INV-028"; "INV-029" ]
        "CG-4-RELATIONSHIP", [ "INV-030"; "INV-031"; "INV-038"; "INV-044" ]
        "CG-5-PERSISTENCE", [ "INV-015"; "INV-016"; "INV-017"; "INV-039"; "INV-040" ]
        "CG-6-CONCLUSION", [ "INV-032"; "INV-033" ]
    ]
let statuses = set [ "DEFINED"; "VALIDATED"; "RELEASED"; "IMPLEMENTING"; "STAGED"; "INTEGRATION_READY"; "VERIFIED"; "BLOCKED" ]

match fsi.CommandLineArgs |> Array.toList |> List.tail with
| group :: status :: rest when groups.ContainsKey group && statuses.Contains status ->
    let actor, note = match rest with a :: n -> a, String.Join(" ", n) | [] -> "coordinator", ""
    let ids = groups.[group]
    let registry = JsonNode.Parse(File.ReadAllText(Path.Combine(docs, "CONTRACT-REGISTRY.json")))
    let entries = registry.["contracts"].AsArray() |> Seq.filter (fun e -> ids |> List.contains (e.["id"].GetValue<string>())) |> Seq.map (fun e -> e.ToJsonString()) |> String.concat "\n"
    let contracts = File.ReadAllText(Path.Combine(root, "src/Exp002.Contracts/Contracts.fs"))
    let hash = SHA256.HashData(Text.Encoding.UTF8.GetBytes(contracts + "\n" + entries)) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()
    let previous =
        (if File.Exists log then File.ReadAllLines log else [||])
        |> Array.map JsonNode.Parse
        |> Array.filter (fun n -> n.["group"].GetValue<string>() = group)
        |> Array.tryLast
    let version =
        match previous with
        | Some p when p.["hash"].GetValue<string>() = hash -> p.["version"].GetValue<int>()
        | Some p -> p.["version"].GetValue<int>() + 1
        | None -> 1
    let line =
        JsonObject(
            dict [ "t", JsonValue.Create(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")) :> JsonNode
                   "group", JsonValue.Create group :> JsonNode
                   "version", JsonValue.Create version :> JsonNode
                   "status", JsonValue.Create status :> JsonNode
                   "hash", JsonValue.Create hash :> JsonNode
                   "requirements", JsonArray(ids |> List.map (fun i -> JsonValue.Create i :> JsonNode) |> Array.ofList) :> JsonNode
                   "actor", JsonValue.Create actor :> JsonNode
                   "note", JsonValue.Create note :> JsonNode ])
    File.AppendAllText(log, line.ToJsonString() + "\n")
    printfn "%s v%d %s %s" group version status (hash.Substring(0, 12))
| _ -> eprintfn "usage: lifecycle.fsx <%s> <STATUS> [actor] [note]" (String.Join("|", groups.Keys)); exit 2
