// EXP-INDY-002 contract and traceability check.
//   dotnet fsi tools/contract-check.fsx [--emit-matrix] [--tests]
// Verifies, against the ratified catalog and the compiled Contracts assembly (reflection):
//   C1 every selected requirement exists in the catalog and has exactly one registry entry, and vice versa;
//   C2 every field entry names a record and property that exist in Exp002.Contracts with a compatible type;
//   C3 every action names a Command case or a UiAction case that exists; every Command case is bound by some entry;
//   C4 every entry has at least one acceptance test ID; with --tests every ID occurs in tests/ sources;
//   C5 every editable catalog field in the slice is a "field" entry (no editable field left untyped).
// --emit-matrix writes docs/experiments/exp-indy-002/REQUIREMENT-MATRIX.json from the catalog and the registry.

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions

let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let repo = Path.GetFullPath(Path.Combine(root, "../.."))
let docs = Path.Combine(repo, "docs/experiments/exp-indy-002")
let args = fsi.CommandLineArgs |> Array.toList

let selected =
    [ "INV-001"; "INV-002"; "INV-004"; "INV-005"; "INV-006"; "INV-007"; "INV-008"; "INV-009"; "INV-010"; "INV-011"
      "INV-012"; "INV-013"; "INV-014"; "INV-015"; "INV-016"; "INV-017"; "INV-018"; "INV-019"; "INV-020"; "INV-021"
      "INV-022"; "INV-023"; "INV-024"; "INV-025"; "INV-026"; "INV-027"; "INV-028"; "INV-029"; "INV-030"; "INV-031"
      "INV-034"; "INV-035"; "INV-036"; "INV-038"; "INV-039"; "INV-040"; "INV-044" ]

let parse (path: string) = JsonNode.Parse(File.ReadAllText path)
let catalog = parse (Path.Combine(repo, "requirements/catalogs/causa-investigation.requirement-catalog.json"))
let registry = parse (Path.Combine(docs, "CONTRACT-REGISTRY.json"))
let arr (n: JsonNode) (k: string) = match n.[k] with :? JsonArray as a -> a |> Seq.toList | _ -> []
let str (n: JsonNode) (k: string) = match n.[k] with null -> None | v when v.GetValueKind() = JsonValueKind.String -> Some(v.GetValue<string>()) | _ -> None
let bool' (n: JsonNode) (k: string) = match n.[k] with null -> false | v -> v.GetValueKind() = JsonValueKind.True

let requirements = arr catalog "requirements" |> List.map (fun r -> (str r "id").Value, r) |> Map.ofList
let entries = arr registry "contracts"

let asm =
    let dll = Path.Combine(root, "src/Exp002.Contracts/bin/Debug/net10.0/Exp002.Contracts.dll")
    Reflection.Assembly.LoadFrom dll
let contractType (name: string) = asm.GetType("Exp002.Contracts+" + name) |> Option.ofObj
let unionCases (name: string) =
    contractType name
    |> Option.map (fun t -> Microsoft.FSharp.Reflection.FSharpType.GetUnionCases t |> Array.map (fun c -> c.Name) |> Set.ofArray)
    |> Option.defaultValue Set.empty
let commandCases = unionCases "Command"
let uiCases = unionCases "UiAction"
let ident = Regex "^[A-Z][A-Za-z]*$"

let friendly (t: Type) =
    if t = typeof<string> then "string"
    elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then t.GetGenericArguments().[0].Name + " option"
    elif t.IsGenericType && t.Name.StartsWith "FSharpList" then t.GetGenericArguments().[0].Name + " list"
    else t.Name

let checkEntry (e: JsonNode) : string list =
    let id = str e "id" |> Option.defaultValue "?"
    let kind = str e "kind" |> Option.defaultValue ""
    let propertyProblems (record: string) (property: string) (fsType: string option) =
        match contractType record with
        | None -> [ $"{id}: C2 record {record} does not exist in Exp002.Contracts" ]
        | Some t ->
            match t.GetProperty property |> Option.ofObj with
            | None -> [ $"{id}: C2 {record}.{property} does not exist" ]
            | Some p ->
                match fsType with
                | Some ft when friendly p.PropertyType <> ft -> [ $"{id}: C2 {record}.{property} is {friendly p.PropertyType}, registry says {ft}" ]
                | _ -> []
    let fieldProblems =
        match kind, str e "record", str e "property" with
        | "field", Some r, Some p -> propertyProblems r p (str e "fsType")
        | "field", _, _ -> [ $"{id}: C2 field has no record/property binding" ]
        | _, Some r, Some p -> propertyProblems r p (str e "fsType")
        | _ -> []
    let nestedProblems = arr e "fields" |> List.collect (fun f -> propertyProblems (str f "record").Value (str f "property").Value (str f "fsType"))
    let tokens k = str e k |> Option.map (fun s -> s.Split([| '|'; ';' |]) |> Array.map (fun x -> x.Trim().Split(' ').[0]) |> Array.toList) |> Option.defaultValue []
    let commandProblems = tokens "command" |> List.filter ident.IsMatch |> List.filter (commandCases.Contains >> not) |> List.map (fun c -> $"{id}: C3 command {c} is not a Command case")
    let uiProblems = tokens "uiAction" |> List.filter (uiCases.Contains >> not) |> List.map (fun c -> $"{id}: C3 UI action {c} is not a UiAction case")
    let actionProblems =
        if kind = "action" && (tokens "command" |> List.isEmpty) && (tokens "uiAction" |> List.isEmpty) then [ $"{id}: C3 action binds no command or UI action" ] else []
    let testProblems = if arr e "tests" |> List.isEmpty then [ $"{id}: C4 no acceptance test ID" ] else []
    let editableProblems =
        match Map.tryFind id requirements with
        | Some r when bool' r "editable" && kind <> "field" -> [ $"{id}: C5 editable catalog field is registered as {kind}" ]
        | _ -> []
    fieldProblems @ nestedProblems @ commandProblems @ uiProblems @ actionProblems @ testProblems @ editableProblems

let registered = entries |> List.choose (fun e -> str e "id")
let coverageProblems =
    (selected |> List.filter (requirements.ContainsKey >> not) |> List.map (sprintf "%s: C1 not in the catalog"))
    @ (selected |> List.filter (fun s -> registered |> List.contains s |> not) |> List.map (sprintf "%s: C1 selected but not registered"))
    @ (registered |> List.filter (fun r -> selected |> List.contains r |> not) |> List.map (sprintf "%s: C1 registered but not selected"))
    @ (registered |> List.countBy id |> List.filter (snd >> (<) 1) |> List.map (fst >> sprintf "%s: C1 registered more than once"))
let boundCommands = entries |> List.collect (fun e -> str e "command" |> Option.map (fun s -> s.Split('|', ';') |> Array.map (fun x -> x.Trim().Split(' ').[0]) |> Array.toList) |> Option.defaultValue []) |> Set.ofList
let unboundCommands = commandCases - boundCommands |> Set.toList |> List.map (sprintf "Command.%s: C3 bound by no registry entry")

let testIds = entries |> List.collect (fun e -> arr e "tests" |> List.map (fun t -> t.GetValue<string>()))
let testSourceProblems =
    if args |> List.contains "--tests" then
        let sources = Directory.GetFiles(Path.Combine(root, "tests"), "*.*", SearchOption.AllDirectories) |> Array.filter (fun f -> not (f.Contains "/bin/" || f.Contains "/obj/")) |> Array.map File.ReadAllText |> String.concat "\n"
        testIds |> List.filter (fun t -> sources.Contains("\"" + t + "\"") |> not) |> List.map (sprintf "%s: C4 test ID not found in tests/")
    else []

let problems = coverageProblems @ (entries |> List.collect checkEntry) @ unboundCommands @ testSourceProblems

if args |> List.contains "--emit-matrix" then
    let byReq = entries |> List.map (fun e -> (str e "id").Value, e) |> Map.ofList
    let matrix =
        JsonArray(
            selected
            |> List.map (fun id ->
                let r = requirements.[id]
                let e = byReq.TryFind id
                JsonObject(
                    dict [
                        "id", JsonValue.Create id :> JsonNode
                        "name", JsonValue.Create((str r "name").Value) :> JsonNode
                        "type", JsonValue.Create((str r "type").Value) :> JsonNode
                        "screen", JsonValue.Create((str r "screen").Value) :> JsonNode
                        "acceptance", (r.["verification"].DeepClone())
                        "validation", (match r.["validation"] with null -> null | v -> v.DeepClone())
                        "notes", (match str r "notes" with Some n -> JsonValue.Create n :> JsonNode | None -> null)
                        "sources", r.["sources"].DeepClone()
                        "contract", (match e with Some e -> JsonValue.Create(defaultArg (str e "command") (defaultArg (str e "function") (defaultArg (str e "uiAction") ""))) :> JsonNode | None -> null)
                        "binding", (match e with Some e -> (match str e "record", str e "property" with Some r, Some p -> JsonValue.Create($"{r}.{p}") :> JsonNode | _ -> null) | None -> null)
                        "tests", (match e with Some e -> e.["tests"].DeepClone() | None -> JsonArray() :> JsonNode)
                    ]) :> JsonNode)
            |> Array.ofList)
    let doc = JsonObject(dict [ "schema", JsonValue.Create "exp-indy-002.requirement-matrix/1" :> JsonNode; "catalogCommit", JsonValue.Create "d4fdef91654d1dde113fbf56ea1c68b331fc81d1" :> JsonNode; "selectedCount", JsonValue.Create(List.length selected) :> JsonNode; "requirements", matrix :> JsonNode ])
    File.WriteAllText(Path.Combine(docs, "REQUIREMENT-MATRIX.json"), doc.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")

printfn "contract-check: %d selected, %d registered, %d Command cases, %d UiAction cases, %d test IDs" selected.Length registered.Length commandCases.Count uiCases.Count testIds.Length
problems |> List.iter (printfn "  FAIL %s")
if List.isEmpty problems then printfn "contract-check: PASS" else (printfn "contract-check: FAIL (%d)" problems.Length; exit 1)
