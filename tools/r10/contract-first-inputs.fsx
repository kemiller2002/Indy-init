// contract-first-inputs.fsx: derive this model's inputs from PR #121's published schedule inputs (Agent 2), and
// tabulate its capability times from model results. Nothing of PR #121 is committed here; the inputs are rebuilt
// from that PR's commit on demand.
//
//   dotnet fsi tools/r10/contract-first-inputs.fsx -- prepare <pr121-proposal-dir> <successor-manifest> <out-dir>
//   dotnet fsi tools/r10/contract-first-inputs.fsx -- table <caps.json> <result.json>...
//
// prepare writes, per roles.<name>.json: roles.<name>.json in this model's format ({ role }), lanes.<name> (one lane
// per role), caps.<name>.json (the capability slice sets); and succ.contract-first.json: every ui-boundary slice is
// built after SL-T10, its declared dependsOn moved to mergeDependsOn (PR #121 SCHEDULE-INPUTS section 6, approximated:
// UI predecessors must be merged, not merely built, before a slice merges).

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let parse (p: string) = JsonNode.Parse(File.ReadAllText p)
let write (p: string) (n: JsonNode) = File.WriteAllText(p, n.ToJsonString(JsonSerializerOptions(WriteIndented = true)))

let prepare (dir: string) (successor: string) (out: string) =
    Directory.CreateDirectory out |> ignore
    let inputs = Path.Combine(dir, "schedule-inputs")
    for f in Directory.GetFiles(inputs, "roles.*.json") do
        let name = Path.GetFileNameWithoutExtension(f).Substring("roles.".Length)
        let src = parse f
        let roles = src["roles"].AsObject() |> Seq.map (fun kv -> kv.Key, kv.Value.ToString()) |> List.ofSeq
        let obj = JsonObject()
        roles |> List.iter (fun (k, v) -> obj[k] <- JsonObject(dict [ "role", JsonValue.Create v :> JsonNode; "basis", JsonValue.Create("PR #121 " + name) :> JsonNode ]))
        write (Path.Combine(out, $"roles.{name}.json")) (JsonObject(dict [ "schema", JsonValue.Create "indy-init.slice-roles/1" :> JsonNode; "roles", obj :> JsonNode ]))
        let lanes = (roles |> List.map snd) @ [ "integration"; "domain"; "persistence"; "reviewer"; "ui"; "ui-2" ] |> List.distinct |> List.sort
        File.WriteAllText(Path.Combine(out, $"lanes.{name}"), lanes |> List.map (fun l -> l + ":" + l) |> String.concat ",")
        write (Path.Combine(out, $"caps.{name}.json")) (src["capabilities"].DeepClone())
    let m = parse successor
    for s in m["slices"].AsArray() do
        if s["implementationBoundary"].ToString() = "ui" && s["id"].ToString() <> "SL-T10" then
            s["mergeDependsOn"] <- s["dependsOn"].DeepClone()
            s["dependsOn"] <- JsonArray(JsonValue.Create "SL-T10")
    m["status"] <- JsonValue.Create "SENSITIVITY ONLY: contract-first per PR #121 SCHEDULE-INPUTS section 6 (approximation): ui-boundary slices built after SL-T10, merged after their declared dependsOn and journey merge dependencies."
    write (Path.Combine(out, "succ.contract-first.json")) m
    printfn "prepared %s" out

let table (caps: string) (results: string list) =
    let c = parse caps
    let names = [ "data-visible"; "data-understood"; "data-editable" ]
    for r in results do
        let doc = parse r
        let cell (k: string) =
            doc["scenarios"].AsArray() |> Seq.map (fun sc ->
                let tl = sc["timeline"].AsArray() |> Seq.map (fun e -> e["slice"].ToString(), e["integrated"].GetValue<float>()) |> Map.ofSeq
                c[k].AsArray() |> Seq.map string |> Seq.choose (fun x -> Map.tryFind x tl) |> Seq.max |> int |> string)
            |> String.concat "/"
        let full = doc["scenarios"].AsArray() |> Seq.map (fun sc -> sc["allIntegratedAt"].GetValue<float>() |> int |> string) |> String.concat "/"
        printfn "| %s | %s | %s |" (Path.GetFileNameWithoutExtension r) (names |> List.map cell |> String.concat " | ") full

match args with
| [ "prepare"; dir; successor; out ] -> prepare dir successor out
| "table" :: caps :: results -> table caps results
| _ -> eprintfn "usage: contract-first-inputs.fsx -- (prepare DIR SUCCESSOR OUT | table CAPS RESULT...)"; exit 2
