namespace Causa.ContractReadiness

open System
open System.IO
open System.Text.RegularExpressions

/// One argument of a union case: `reason: NonEmptyText` gives (Some "reason", "NonEmptyText").
type CaseArg = { Label: string option; Type: string }

type UnionCase = { Name: string; Args: CaseArg list }

/// The parts of a compiled contract file this validator reads. It parses the F# text of the actual package files,
/// so a package edit (a removed case, a narrowed argument) changes the result without any hand-kept copy.
type FsContract =
    { Path: string
      Sha256: string
      Unions: Map<string, UnionCase list>
      Records: Map<string, (string * string) list>
      OpaqueTypes: Set<string> }

module FsContract =
    let private caseLine = Regex(@"^\s+\|\s+([A-Z][A-Za-z0-9_]*)(?:\s+of\s+(.+?))?\s*(?://.*)?$", RegexOptions.CultureInvariant)
    let private typeHead = Regex(@"^(?:and|type)\s+(?:\[<[^>]+>\]\s+)?([A-Z][A-Za-z0-9_]*)\s*=\s*(.*)$", RegexOptions.CultureInvariant)
    let private opaque = Regex(@"^type\s+([A-Z][A-Za-z0-9_]*)\s*=\s*class\s+end", RegexOptions.CultureInvariant)

    let private parseArgs (text: string) =
        text.Split([| " * " |], StringSplitOptions.None)
        |> Array.map (fun a ->
            let a = a.Trim()
            match a.IndexOf ':' with
            | -1 -> { Label = None; Type = a }
            | k -> { Label = Some(a.Substring(0, k).Trim()); Type = a.Substring(k + 1).Trim() })
        |> List.ofArray

    let private parseRecordBody (body: string) =
        body.Trim().TrimStart('{').TrimEnd('}').Split(';')
        |> Array.choose (fun p ->
            match p.IndexOf ':' with
            | -1 -> None
            | k -> Some(p.Substring(0, k).Trim(), p.Substring(k + 1).Trim()))
        |> List.ofArray

    /// Parses top-level `type X =` unions (cases on following indented `| Case of ...` lines), one-line and
    /// multi-line records, and `type X = class end` opaque stubs.
    let parseText (path: string) (text: string) : FsContract =
        let lines = text.Replace("\r", "").Split('\n') |> List.ofArray
        let rec walk (ls: string list) (unions: Map<_, _>) (records: Map<_, _>) (opaques: Set<string>) =
            match ls with
            | [] -> unions, records, opaques
            | l :: rest ->
                let o = opaque.Match l
                if o.Success then walk rest unions records (opaques.Add o.Groups.[1].Value)
                else
                    let m = typeHead.Match(l.TrimStart())
                    if m.Success && (l.StartsWith "type" || l.StartsWith "and" || l.StartsWith "    type") then
                        let name = m.Groups.[1].Value
                        let tail = m.Groups.[2].Value.Trim()
                        if tail.StartsWith "{" && tail.EndsWith "}" then
                            walk rest unions (records.Add(name, parseRecordBody tail)) opaques
                        elif tail.StartsWith "{" || (tail = "" && (rest |> List.tryHead |> Option.map (fun x -> x.Trim().StartsWith "{") |> Option.defaultValue false)) then
                            // Multi-line record: collect until the closing brace.
                            let body, after =
                                let all = (if tail = "" then [] else [ tail ]) @ rest
                                let idx = all |> List.tryFindIndex (fun x -> x.Contains "}")
                                match idx with
                                | Some k -> String.Join(";", all |> List.take (k + 1) |> List.map (fun x -> x.Trim())), all |> List.skip (k + 1)
                                | None -> "", rest
                            let fields =
                                body.Replace("{", "").Replace("}", "").Split(';')
                                |> Array.choose (fun p ->
                                    let p = (match p.IndexOf "///" with -1 -> p | k -> p.Substring(0, k)).Trim()
                                    match p.IndexOf ':' with
                                    | -1 -> None
                                    | k when p.Substring(0, k).Trim() |> Seq.forall (fun c -> Char.IsLetterOrDigit c || c = '_') && p.Substring(0, k).Trim() <> "" ->
                                        Some(p.Substring(0, k).Trim(), p.Substring(k + 1).Trim())
                                    | _ -> None)
                                |> List.ofArray
                            walk after unions (records.Add(name, fields)) opaques
                        elif tail = "" || tail.StartsWith "|" || tail = "private" then
                            let caseLines =
                                rest
                                |> List.takeWhile (fun x -> x.Trim().StartsWith "|" || x.Trim().StartsWith "///" || x.Trim() = "private" || x.Trim().StartsWith "[<")
                                |> List.filter (fun x -> x.Trim().StartsWith "|")
                            let inline' = if tail.StartsWith "|" then [ "    " + tail ] else []
                            let cases =
                                inline' @ caseLines
                                |> List.choose (fun x ->
                                    let c = caseLine.Match x
                                    if c.Success then
                                        Some { Name = c.Groups.[1].Value
                                               Args = if c.Groups.[2].Success then parseArgs c.Groups.[2].Value else [] }
                                    else None)
                            if List.isEmpty cases then walk rest unions records opaques
                            else walk rest (unions.Add(name, cases)) records opaques
                        else walk rest unions records opaques
                    else walk rest unions records opaques
        let unions, records, opaques = walk lines Map.empty Map.empty Set.empty
        { Path = path
          Sha256 = Json.sha256Text text
          Unions = unions
          Records = records
          OpaqueTypes = opaques }

    let parseFile (path: string) = parseText path (File.ReadAllText path)

    /// Parses every `.fs` file of a compiled package's contracts directory into one view (later files win on name).
    let parsePackage (contractsDir: string) : FsContract =
        let files = Directory.GetFiles(contractsDir, "*.fs") |> Array.sort |> List.ofArray
        let parts = files |> List.map parseFile
        { Path = contractsDir
          Sha256 = parts |> List.map (fun p -> p.Sha256) |> String.concat "" |> Json.sha256Text
          Unions = parts |> List.fold (fun acc p -> Map.fold (fun a k v -> Map.add k v a) acc p.Unions) Map.empty
          Records = parts |> List.fold (fun acc p -> Map.fold (fun a k v -> Map.add k v a) acc p.Records) Map.empty
          OpaqueTypes = parts |> List.fold (fun acc p -> Set.union acc p.OpaqueTypes) Set.empty }

    let cases (c: FsContract) (unionName: string) = c.Unions |> Map.tryFind unionName |> Option.defaultValue []
    let caseNames c u = cases c u |> List.map (fun x -> x.Name)
    let tryCase c u name = cases c u |> List.tryFind (fun x -> x.Name = name)
