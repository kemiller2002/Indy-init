/// Reading the actual causa-data checkout. Nothing here generates event data: every history comes from the
/// repository's own files (historical cases, synthetic fixtures, negative fixtures, golden states).
module Source

open System
open System.IO
open Causa.HistoricalCompatibility

type Case =
    { CaseKey: string
      InvestigationId: string
      Path: string
      ExpectedVersion: int64
      ExpectedLifecycle: string
      ExpectedStateSha256: string
      EventsSha256: string }

let readJsonFile (path: string) : JValue =
    match Json.parse (File.ReadAllText(path).TrimEnd('\n')) with
    | Ok v -> v
    | Error e -> failwith $"{path}: {e.Detail}"

let private str (k: string) (o: (string * JValue) list) = match Json.tryField k o with Some(JStr s) -> s | _ -> failwith $"missing string '{k}'"
let private int (k: string) (o: (string * JValue) list) = match Json.tryField k o with Some(JInt n) -> n | _ -> failwith $"missing integer '{k}'"
let objs (k: string) (v: JValue) = match v with JObj o -> (match Json.tryField k o with Some(JArr xs) -> xs |> List.choose (function JObj m -> Some m | _ -> None) | _ -> []) | _ -> []
let field k (o: (string * JValue) list) = str k o
let intField k (o: (string * JValue) list) = int k o

/// A file-system DocumentSource: the reference implementation of raw document retrieval.
let fileSystemSource (workspaceInvestigations: string) : DocumentSource =
    { ListInvestigations =
        fun () ->
            if Directory.Exists workspaceInvestigations then
                Ok(Directory.GetDirectories workspaceInvestigations |> Array.map Path.GetFileName |> Array.toList)
            else Error $"{workspaceInvestigations} does not exist"
      ReadInvestigation =
        fun name ->
            let dir = Path.Combine(workspaceInvestigations, name)
            if not (Directory.Exists dir) then Error $"{dir} does not exist"
            else
                let entries =
                    Directory.GetFileSystemEntries dir
                    // A subdirectory keeps a trailing "/" so it can never pass for one of the three files (CONTRACT §3.3).
                    |> Array.map (fun p -> if File.Exists p then Path.GetFileName p, File.ReadAllBytes p else Path.GetFileName p + "/", [||])
                    |> Array.toList
                Ok { DirectoryName = name; Entries = entries } }

let readInvestigation (dir: string) : InvestigationFiles =
    match (fileSystemSource (Path.GetDirectoryName dir)).ReadInvestigation(Path.GetFileName dir) with
    | Ok f -> f
    | Error e -> failwith e

/// The historical catalog manifest (catalog/historical-v1/manifest.json): the independent expectations.
let historicalCases (root: string) : Case list =
    readJsonFile (Path.Combine(root, "catalog/historical-v1/manifest.json"))
    |> objs "cases"
    |> List.map (fun c ->
        { CaseKey = field "caseKey" c
          InvestigationId = field "investigationId" c
          Path = Path.Combine(root, field "path" c)
          ExpectedVersion = intField "expectedVersion" c
          ExpectedLifecycle = field "expectedLifecycle" c
          ExpectedStateSha256 = field "expectedStateSha256" c
          EventsSha256 = field "eventsSha256" c })

/// The synthetic frozen fixtures (fixtures/v1.0.0/manifest.json), used only to reach the five event types the
/// historical cases never use, and checked against the independent golden states.
let fixtureCases (root: string) : Case list =
    readJsonFile (Path.Combine(root, "fixtures/v1.0.0/manifest.json"))
    |> objs "investigations"
    |> List.map (fun c ->
        let id = field "investigationId" c
        { CaseKey = field "scenarioKey" c
          InvestigationId = id
          Path = Path.Combine(root, "fixtures/v1.0.0/investigations", id)
          ExpectedVersion = intField "expectedVersion" c
          ExpectedLifecycle = field "expectedLifecycle" c
          ExpectedStateSha256 = field "expectedStateSha256" c
          EventsSha256 = field "eventsSha256" c })

type NegativeFixture =
    { Name: string
      Kind: string
      ExpectedCode: string
      ExpectedDomainCode: string option
      BaseScenarioKey: string option
      Files: InvestigationFiles option }

let negativeFixtures (root: string) : NegativeFixture list =
    Directory.GetFiles(Path.Combine(root, "fixtures/v1.0.0/negative"), "*.json")
    |> Array.sortWith (fun a b -> String.CompareOrdinal(a, b))
    |> Array.toList
    |> List.map (fun p ->
        match readJsonFile p with
        | JObj o ->
            let files =
                match Json.tryField "files" o with
                | Some(JObj fs) ->
                    let dir = match Json.tryField "directoryId" o with Some(JStr d) -> d | _ -> "00000000-0000-0000-0000-000000000000"
                    let entries = fs |> List.map (fun (name, v) -> name, (match v with JObj b -> Convert.FromBase64String(str "base64" b) | _ -> [||]))
                    Some { DirectoryName = dir; Entries = entries }
                | _ -> None
            { Name = str "name" o
              Kind = str "kind" o
              ExpectedCode = str "expectedErrorCode" o
              ExpectedDomainCode = (match Json.tryField "expectedDomainCode" o with Some(JStr s) -> Some s | _ -> None)
              BaseScenarioKey = (match Json.tryField "baseScenarioKey" o with Some(JStr s) -> Some s | _ -> None)
              Files = files }
        | _ -> failwith $"{p} is not an object")

let lines (files: InvestigationFiles) =
    let text = Text.Encoding.UTF8.GetString(files.Entries |> List.find (fst >> (=) "events.ndjson") |> snd)
    text.TrimEnd('\n').Split('\n') |> Array.toList

let withEntry (name: string) (bytes: byte[]) (files: InvestigationFiles) =
    { files with Entries = files.Entries |> List.map (fun (n, b) -> if n = name then n, bytes else n, b) }

let withLines (ls: string list) (files: InvestigationFiles) =
    withEntry "events.ndjson" (Text.Encoding.UTF8.GetBytes(String.Join("\n", ls) + "\n")) files
